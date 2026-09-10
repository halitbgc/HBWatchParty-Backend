using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeParty.Hubs
{
    public class PlaybackSyncBackgroundService : BackgroundService
    {
        private static readonly TimeSpan RoundInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan CollectWindow = TimeSpan.FromSeconds(2);
        private const double DriftThresholdSeconds = 2.0;

        private const int RequiredConsecutiveRounds = 2;

        private readonly IHubContext<WatchPartyHub> _hubContext;
        private readonly PlaybackSyncTracker _tracker;

        public PlaybackSyncBackgroundService(IHubContext<WatchPartyHub> hubContext, PlaybackSyncTracker tracker)
        {
            _hubContext = hubContext;
            _tracker = tracker;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var activeRooms = WatchPartyHub.GetActiveRoomIds();

                    foreach (var roomId in activeRooms)
                    {
                        _tracker.StartNewRound(roomId);
                        await _hubContext.Clients.Group(roomId).SendAsync("RequestPlaybackTime", cancellationToken: stoppingToken);
                    }

                    await Task.Delay(CollectWindow, stoppingToken);

                    foreach (var roomId in activeRooms)
                    {
                        var reports = _tracker.GetReports(roomId);
                        if (reports.Count < 2) continue;

                        double max = reports.Max(r => r.Time);
                        double min = reports.Min(r => r.Time);

                        if (max - min > DriftThresholdSeconds)
                        {
                            int consecutiveCount = _tracker.RegisterDriftRound(roomId);
                            if (consecutiveCount >= RequiredConsecutiveRounds)
                            {
                                await _hubContext.Clients.Group(roomId).SendAsync("ReceiveSyncWarning", cancellationToken: stoppingToken);
                            }
                        }
                        else
                        {
                            _tracker.ResetDrift(roomId);
                            await _hubContext.Clients.Group(roomId).SendAsync("ReceiveSyncOk", cancellationToken: stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { /* bir round hata verse bile servis tamamen durmasın */ }

                var remaining = RoundInterval - CollectWindow;
                if (remaining > TimeSpan.Zero)
                {
                    try { await Task.Delay(remaining, stoppingToken); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }
    }
}
