using System.Collections.Concurrent;
using System.Collections.Generic;

namespace YouTubeParty.Hubs
{
    public class PlaybackSyncTracker
    {
        private readonly ConcurrentDictionary<string, ConcurrentBag<(string Username, double Time)>> _reports = new();
        private readonly ConcurrentDictionary<string, int> _consecutiveDriftRounds = new();

        public void StartNewRound(string roomId)
        {
            _reports[roomId] = new ConcurrentBag<(string, double)>();
        }

        public void AddReport(string roomId, string username, double time)
        {
            if (_reports.TryGetValue(roomId, out var bag))
            {
                bag.Add((username, time));
            }
        }

        public List<(string Username, double Time)> GetReports(string roomId)
        {
            return _reports.TryGetValue(roomId, out var bag) ? new List<(string, double)>(bag) : new List<(string, double)>();
        }

        // Bu turda da fark tespit edildiğini işaretler, güncel art arda sayısını döner.
        public int RegisterDriftRound(string roomId)
        {
            return _consecutiveDriftRounds.AddOrUpdate(roomId, 1, (_, count) => count + 1);
        }

        // Bu turda fark yoktu — sayaç sıfırlanır.
        public void ResetDrift(string roomId)
        {
            _consecutiveDriftRounds[roomId] = 0;
        }
    }
}
