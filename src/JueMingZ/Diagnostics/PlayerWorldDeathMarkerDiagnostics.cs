using System;

namespace JueMingZ.Diagnostics
{
    public static class PlayerWorldDeathMarkerDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldDeathMarkerLayerSnapshot _snapshot = new PlayerWorldDeathMarkerLayerSnapshot();

        public static void MarkLayerInstalled(string message)
        {
            lock (SyncRoot)
            {
                _snapshot.LayerInstalled = true;
                _snapshot.LayerMessage = message ?? string.Empty;
            }
        }

        public static void MarkLayerSkipped(string message)
        {
            lock (SyncRoot)
            {
                _snapshot.LayerInstalled = false;
                _snapshot.LayerMessage = message ?? string.Empty;
            }
        }

        public static void MarkLayerFailed(string message, Exception error)
        {
            lock (SyncRoot)
            {
                _snapshot.LayerInstalled = false;
                _snapshot.LayerMessage = (message ?? string.Empty) + (error == null ? string.Empty : " " + error.GetType().Name + ": " + error.Message);
            }
        }

        public static void RecordDraw(
            string status,
            string message,
            string pairId,
            int cachedCount,
            int drawnCount,
            bool culledByLimit,
            bool historyReadFailed)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = status ?? string.Empty;
                _snapshot.LastMessage = message ?? string.Empty;
                _snapshot.LastPairId = pairId ?? string.Empty;
                _snapshot.CachedCount = cachedCount;
                _snapshot.DrawnCount = drawnCount;
                _snapshot.CulledByLimit = culledByLimit;
                _snapshot.HistoryReadFailed = historyReadFailed;
                _snapshot.LastDrawUtc = DateTime.UtcNow;
            }
        }

        public static PlayerWorldDeathMarkerLayerSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new PlayerWorldDeathMarkerLayerSnapshot
                {
                    LayerInstalled = _snapshot.LayerInstalled,
                    LayerMessage = _snapshot.LayerMessage,
                    LastStatus = _snapshot.LastStatus,
                    LastMessage = _snapshot.LastMessage,
                    LastPairId = _snapshot.LastPairId,
                    CachedCount = _snapshot.CachedCount,
                    DrawnCount = _snapshot.DrawnCount,
                    CulledByLimit = _snapshot.CulledByLimit,
                    HistoryReadFailed = _snapshot.HistoryReadFailed,
                    LastDrawUtc = _snapshot.LastDrawUtc
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldDeathMarkerLayerSnapshot();
            }
        }
    }

    public sealed class PlayerWorldDeathMarkerLayerSnapshot
    {
        public bool LayerInstalled { get; set; }
        public string LayerMessage { get; set; }
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public int CachedCount { get; set; }
        public int DrawnCount { get; set; }
        public bool CulledByLimit { get; set; }
        public bool HistoryReadFailed { get; set; }
        public DateTime? LastDrawUtc { get; set; }

        public PlayerWorldDeathMarkerLayerSnapshot()
        {
            LayerMessage = string.Empty;
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
        }
    }
}
