using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace YouTubeParty.Hubs
{
    public class VoiceSignalingHub : Hub
    {
        private static readonly ConcurrentDictionary<string, (string RoomId, string Username)> Peers = new();

        public async Task JoinVoiceRoom(string roomId, string username)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            var existingUsernames = Peers.Values
                .Where(p => p.RoomId == roomId)
                .Select(p => p.Username)
                .Distinct()
                .ToList();

            Peers[Context.ConnectionId] = (roomId, username);

            await Clients.Caller.SendAsync("ReceiveVoiceRoster", existingUsernames);
            await Clients.OthersInGroup(roomId).SendAsync("VoicePeerJoined", username);
        }

        public async Task SendVoiceSignal(string roomId, string targetUsername, string payload)
        {
            if (!Peers.TryGetValue(Context.ConnectionId, out var self)) return;

            var targetConnectionId = Peers
                .Where(kvp => kvp.Value.RoomId == roomId && kvp.Value.Username == targetUsername)
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            if (targetConnectionId != null)
            {
                await Clients.Client(targetConnectionId).SendAsync("ReceiveVoiceSignal", self.Username, payload);
            }
        }

        public async Task LeaveVoiceRoom(string roomId, string username)
        {
            Peers.TryRemove(Context.ConnectionId, out _);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            await Clients.OthersInGroup(roomId).SendAsync("VoicePeerLeft", username);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (Peers.TryRemove(Context.ConnectionId, out var info))
            {
                await Clients.OthersInGroup(info.RoomId).SendAsync("VoicePeerLeft", info.Username);
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}