using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace YouTubeParty.Hubs
{
    public class WatchPartyHub : Hub
    {
        private static readonly ConcurrentDictionary<string, (string RoomId, string Username)> ConnectionRooms = new();
        private static readonly ConcurrentDictionary<string, DateTime> RecentDisconnects = new();
        private static readonly TimeSpan RejoinGracePeriod = TimeSpan.FromSeconds(10);
        private readonly PlaybackSyncTracker _syncTracker;


        private static string DisconnectKey(string roomId, string username) => $"{roomId}::{username}";

        public WatchPartyHub(PlaybackSyncTracker syncTracker)
        {
            _syncTracker = syncTracker;
        }

        private async Task BroadcastUserCount(string roomId)
        {
            var usernames = ConnectionRooms.Values
                .Where(v => v.RoomId == roomId)
                .Select(v => v.Username)
                .ToList();

            await Clients.Group(roomId).SendAsync("ReceiveUserCount", usernames.Count);
            await Clients.Group(roomId).SendAsync("ReceiveUserList", usernames);
        }
        public async Task JoinRoom(string roomId, string username)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            ConnectionRooms[Context.ConnectionId] = (roomId, username);

            bool isSeamlessRejoin =
                RecentDisconnects.TryRemove(DisconnectKey(roomId, username), out var disconnectedAtUtc)
                && DateTime.UtcNow - disconnectedAtUtc <= RejoinGracePeriod;

            if (!isSeamlessRejoin)
            {
                await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"{username} odaya katıldı.");
            }

            await BroadcastUserCount(roomId);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (ConnectionRooms.TryRemove(Context.ConnectionId, out var info))
            {
                RecentDisconnects[DisconnectKey(info.RoomId, info.Username)] = DateTime.UtcNow;
                PurgeExpiredDisconnects();
                await BroadcastUserCount(info.RoomId);
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

        public async Task LeaveRoom(string roomId, string username)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            ConnectionRooms.TryRemove(Context.ConnectionId, out _);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"{username} odadan ayrıldı.");
            await BroadcastUserCount(roomId);
        }

        public async Task ChangeVideo(string roomId, string videoId, string username)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveChangeVideo", videoId);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"📺 {username} yeni bir video açtı.");
        }

        public async Task SendPlay(string roomId, double time, string username)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceivePlay", time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"▶ {username} videoyu oynattı.");
        }

        public async Task SendPause(string roomId, double time, string username)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceivePause", time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"⏸ {username} videoyu durdurdu.");
        }

        public async Task SendSeek(string roomId, double time, string username)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveSeek", time);
            int roundedTime = (int)Math.Floor(time);
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", $"⏩ {username} videoyu {roundedTime}. saniyeye sardı.");
        }

        public async Task SendChatMessage(string roomId, string username, string message)
        {
            await Clients.Group(roomId).SendAsync("ReceiveChatMessage", username, message);
        }

        public async Task SendTyping(string roomId, string username)
        {
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveTyping", username);
        }

        public async Task SendSystemLog(string roomId, string message)
        {
            await Clients.Group(roomId).SendAsync("ReceiveSystemMessage", message);
        }

        public async Task ReportPlaybackTime(string roomId, double time, string username)
        {
            _syncTracker.AddReport(roomId, username, time);
        }

        public static IEnumerable<string> GetActiveRoomIds()
        {
            return ConnectionRooms.Values.Select(v => v.RoomId).Distinct().ToList();
        }
    }
}