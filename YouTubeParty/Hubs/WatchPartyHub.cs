using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using YouTubeParty.Data;
using YouTubeParty.Models;

namespace YouTubeParty.Hubs
{
    [Authorize]
    public class WatchPartyHub : Hub
    {
        private static readonly ConcurrentDictionary<string, (string RoomId, string UserId, string DisplayName)> ConnectionRooms = new();
        private static readonly ConcurrentDictionary<string, DateTime> RecentDisconnects = new();
        private static readonly TimeSpan RejoinGracePeriod = TimeSpan.FromSeconds(10);
        private readonly PlaybackSyncTracker _syncTracker;
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<WatchPartyHub> _logger;


        private static string DisconnectKey(string roomId, string userId) => $"{roomId}::{userId}";

        // [Authorize] sayesinde bağlantı zaten geçerli bir token'a sahip; kimlik her zaman
        // Context.User claim'lerinden çözülür, istemciden gelen bir isme hiç güvenilmez.
        private string ResolveUserId()
        {
            return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.ConnectionId;
        }

        // Sadece JoinRoom'da, bağlantı başına BİR KEZ çağrılır: DisplayName artık JWT'de
        // taşınmıyor (isim değişince token yenilemeye/bağlantı koparmaya gerek kalmasın diye),
        // bu yüzden katılım anında DB'den okunup ConnectionRooms'a önbelleklenir.
        private async Task<(string UserId, string DisplayName)> ResolveIdentityForJoinAsync()
        {
            var userId = ResolveUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            var displayName = user?.GetEffectiveDisplayName();
            return (userId, string.IsNullOrEmpty(displayName) ? "Kullanıcı" : displayName);
        }

        // JoinRoom sonrası her çağrıda buradan okunur — DB/claim'e gitmeden, hızlı.
        private (string UserId, string DisplayName) GetCachedIdentity()
        {
            if (ConnectionRooms.TryGetValue(Context.ConnectionId, out var info))
            {
                return (info.UserId, info.DisplayName);
            }

            return (ResolveUserId(), "Kullanıcı");
        }

        public WatchPartyHub(PlaybackSyncTracker syncTracker, ApplicationDbContext dbContext, ILogger<WatchPartyHub> logger)
        {
            _syncTracker = syncTracker;
            _dbContext = dbContext;
            _logger = logger;
        }

        private async Task BroadcastParticipants(string roomId)
        {
            var participants = ConnectionRooms.Values
                .Where(v => v.RoomId == roomId)
                .Select(v => new RoomParticipant(v.UserId, v.DisplayName))
                .ToList();

            await Clients.Group(roomId).SendAsync("ReceiveUserCount", participants.Count);
            await Clients.Group(roomId).SendAsync("ReceiveUserList", participants);
        }

        public async Task JoinRoom(string roomId)
        {
            var (userId, displayName) = await ResolveIdentityForJoinAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            ConnectionRooms[Context.ConnectionId] = (roomId, userId, displayName);

            bool isSeamlessRejoin =
                RecentDisconnects.TryRemove(DisconnectKey(roomId, userId), out var disconnectedAtUtc)
                && DateTime.UtcNow - disconnectedAtUtc <= RejoinGracePeriod;

            if (!isSeamlessRejoin)
            {
                await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"{displayName} odaya katıldı.");
            }

            await BroadcastParticipants(roomId);

            await PersistJoinAsync(roomId, userId);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (ConnectionRooms.TryRemove(Context.ConnectionId, out var info))
            {
                RecentDisconnects[DisconnectKey(info.RoomId, info.UserId)] = DateTime.UtcNow;
                PurgeExpiredDisconnects();
                await BroadcastParticipants(info.RoomId);

                await PersistLeaveAsync(info.RoomId, info.UserId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private static void PurgeExpiredDisconnects()
        {
            var cutoff = DateTime.UtcNow - RejoinGracePeriod;
            foreach (var kvp in RecentDisconnects)
            {
                if (kvp.Value < cutoff)
                {
                    RecentDisconnects.TryRemove(kvp.Key, out _);
                }
            }
        }

        public async Task LeaveRoom(string roomId)
        {
            var (userId, displayName) = GetCachedIdentity();

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            ConnectionRooms.TryRemove(Context.ConnectionId, out _);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"{displayName} odadan ayrıldı.");
            await BroadcastParticipants(roomId);

            await PersistLeaveAsync(roomId, userId);
        }

        public async Task ChangeVideo(string roomId, string videoId)
        {
            var (_, displayName) = GetCachedIdentity();

            await Clients.OthersInGroup(roomId).SendAsync("ReceiveChangeVideo", videoId);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"📺 {displayName} yeni bir video açtı.");
        }

        public async Task SendPlay(string roomId, double time)
        {
            var (_, displayName) = GetCachedIdentity();

            await Clients.OthersInGroup(roomId).SendAsync("ReceivePlay", time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"▶ {displayName} videoyu oynattı.");
        }

        public async Task SendPause(string roomId, double time)
        {
            var (_, displayName) = GetCachedIdentity();

            await Clients.OthersInGroup(roomId).SendAsync("ReceivePause", time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"⏸ {displayName} videoyu durdurdu.");
        }

        public async Task SendSeek(string roomId, double time)
        {
            var (_, displayName) = GetCachedIdentity();

            await Clients.OthersInGroup(roomId).SendAsync("ReceiveSeek", time);
            int roundedTime = (int)Math.Floor(time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"⏩ {displayName} videoyu {roundedTime}. saniyeye sardı.");
        }

        public async Task SendChatMessage(string roomId, string message)
        {
            var (userId, _) = GetCachedIdentity();

            await Clients.Group(roomId).SendAsync("ReceiveChatMessage", userId, message);
        }

        public async Task SendTyping(string roomId)
        {
            var (userId, _) = GetCachedIdentity();

            await Clients.OthersInGroup(roomId).SendAsync("ReceiveTyping", userId);
        }

        public async Task SendSystemLog(string roomId, string message)
        {
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", message);
        }

        public async Task ReportPlaybackTime(string roomId, double time)
        {
            var (userId, _) = GetCachedIdentity();

            _syncTracker.AddReport(roomId, userId, time);
        }

        public static IEnumerable<string> GetActiveRoomIds()
        {
            return ConnectionRooms.Values.Select(v => v.RoomId).Distinct().ToList();
        }

        // Kullanıcı görünen adını değiştirdiğinde çağrılır (UsersController tarafından).
        // O an bağlı olduğu tüm odaların önbelleğini günceller ve etkilenen roomId'leri döner
        // ki çağıran taraf ReceiveDisplayNameChanged'i doğru gruplara yayınlayabilsin.
        public static List<string> UpdateDisplayNameForUser(string userId, string newDisplayName)
        {
            var affectedRoomIds = new List<string>();

            foreach (var kvp in ConnectionRooms.ToList())
            {
                if (kvp.Value.UserId == userId)
                {
                    ConnectionRooms[kvp.Key] = (kvp.Value.RoomId, kvp.Value.UserId, newDisplayName);
                    affectedRoomIds.Add(kvp.Value.RoomId);
                }
            }

            return affectedRoomIds.Distinct().ToList();
        }

        // Best-effort kalıcılık: DB'ye yazılamazsa (bağlantı yok, geçici hata vb.)
        // hatayı yutar ve loglar; gerçek zamanlı SignalR akışını asla etkilemez.
        private async Task PersistJoinAsync(string roomId, string userId)
        {
            try
            {
                var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomCode == roomId);
                if (room is null)
                {
                    room = new Room { RoomCode = roomId, CreatedByUserId = userId };
                    _dbContext.Rooms.Add(room);
                    await _dbContext.SaveChangesAsync();
                }

                var member = await _dbContext.RoomMembers
                    .FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId);

                if (member is null)
                {
                    member = new RoomMember
                    {
                        RoomId = room.Id,
                        UserId = userId,
                        JoinedAtUtc = DateTime.UtcNow,
                        LastSeenAtUtc = DateTime.UtcNow
                    };
                    _dbContext.RoomMembers.Add(member);
                }
                else
                {
                    member.LastSeenAtUtc = DateTime.UtcNow;
                    member.LeftAtUtc = null;
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Room/RoomMember kalıcılığı sırasında hata oluştu (roomId={RoomId})", roomId);
            }
        }

        private async Task PersistLeaveAsync(string roomId, string userId)
        {
            try
            {
                var room = await _dbContext.Rooms.FirstOrDefaultAsync(r => r.RoomCode == roomId);
                if (room is null)
                {
                    return;
                }

                var member = await _dbContext.RoomMembers
                    .FirstOrDefaultAsync(m => m.RoomId == room.Id && m.UserId == userId);

                if (member is not null)
                {
                    member.LeftAtUtc = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Room/RoomMember ayrılma kaydı sırasında hata oluştu (roomId={RoomId})", roomId);
            }
        }
    }
}
