using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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
    public class VoiceSignalingHub : Hub
    {
        private static readonly ConcurrentDictionary<string, (string RoomId, string UserId, string DisplayName)> Peers = new();
        private readonly ApplicationDbContext _dbContext;

        public VoiceSignalingHub(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // [Authorize] sayesinde bağlantı zaten geçerli bir token'a sahip; kimlik her zaman
        // Context.User claim'lerinden çözülür, istemciden gelen bir isme hiç güvenilmez.
        private string ResolveUserId()
        {
            return Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.ConnectionId;
        }

        // Sadece JoinVoiceRoom'da, bağlantı başına BİR KEZ çağrılır (WatchPartyHub ile aynı desen).
        private async Task<(string UserId, string DisplayName)> ResolveIdentityForJoinAsync()
        {
            var userId = ResolveUserId();
            var user = await _dbContext.Users.FindAsync(userId);
            var displayName = user?.GetEffectiveDisplayName();
            return (userId, string.IsNullOrEmpty(displayName) ? "Kullanıcı" : displayName);
        }

        private (string UserId, string DisplayName) GetCachedIdentity()
        {
            if (Peers.TryGetValue(Context.ConnectionId, out var info))
            {
                return (info.UserId, info.DisplayName);
            }

            return (ResolveUserId(), "Kullanıcı");
        }

        public async Task JoinVoiceRoom(string roomId)
        {
            var (userId, displayName) = await ResolveIdentityForJoinAsync();

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            var existingParticipants = Peers.Values
                .Where(p => p.RoomId == roomId)
                .Select(p => new RoomParticipant(p.UserId, p.DisplayName))
                .Distinct()
                .ToList();

            Peers[Context.ConnectionId] = (roomId, userId, displayName);

            await Clients.Caller.SendAsync("ReceiveVoiceRoster", existingParticipants);
            await Clients.OthersInGroup(roomId).SendAsync("VoicePeerJoined", userId, displayName);
        }

        public async Task SendVoiceSignal(string roomId, string targetUserId, string payload)
        {
            var (userId, _) = GetCachedIdentity();

            var targetConnectionId = Peers
                .Where(kvp => kvp.Value.RoomId == roomId && kvp.Value.UserId == targetUserId)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (targetConnectionId != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveVoiceSignal", userId, payload);
            }
        }

        public async Task LeaveVoiceRoom(string roomId)
        {
            var (userId, _) = GetCachedIdentity();

            Peers.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.OthersInGroup(roomId).SendAsync("VoicePeerLeft", userId);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (Peers.TryRemove(Context.ConnectionId, out var info))
            {
                await Clients.OthersInGroup(info.RoomId).SendAsync("VoicePeerLeft", info.UserId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Kullanıcı görünen adını değiştirdiğinde çağrılır (UsersController tarafından).
        public static List<string> UpdateDisplayNameForUser(string userId, string newDisplayName)
        {
            var affectedRoomIds = new List<string>();

            foreach (var kvp in Peers.ToList())
            {
                if (kvp.Value.UserId == userId)
                {
                    Peers[kvp.Key] = (kvp.Value.RoomId, kvp.Value.UserId, newDisplayName);
                    affectedRoomIds.Add(kvp.Value.RoomId);
                }
            }

            return affectedRoomIds.Distinct().ToList();
        }
    }
}
