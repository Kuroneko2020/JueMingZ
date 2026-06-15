using System;
using JueMingZ.Records;

namespace JueMingZ.Diagnostics
{
    internal static class PlayerWorldMapMarkerDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldMapMarkerSnapshot _snapshot = new PlayerWorldMapMarkerSnapshot();

        public static void RecordRead(PlayerWorldMapMarkerReadResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.MarkerCount = result == null ? 0 : result.MarkerCount;
                _snapshot.ReadFailed = result != null && result.ReadFailed;
                _snapshot.CulledByCacheLimit = result != null && result.CulledByCacheLimit;
                _snapshot.LastReadUtc = result == null ? null : result.LastReadUtc;
                if (string.IsNullOrWhiteSpace(_snapshot.LastOperation))
                {
                    _snapshot.LastOperation = "read";
                }
            }
        }

        public static void RecordWrite(PlayerWorldMapMarkerWriteResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.MarkerCount = result == null ? 0 : result.MarkerCount;
                _snapshot.WriteFailed = result != null && !result.Succeeded;
                _snapshot.LimitExceeded = result != null && result.LimitExceeded;
                _snapshot.LastOperation = result == null ? string.Empty : result.Operation ?? string.Empty;
                _snapshot.LastWriteUtc = result == null ? null : result.LastWriteUtc;
            }
        }

        public static PlayerWorldMapMarkerSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new PlayerWorldMapMarkerSnapshot
                {
                    Enabled = _snapshot.Enabled,
                    LastStatus = _snapshot.LastStatus,
                    LastMessage = _snapshot.LastMessage,
                    LastPairId = _snapshot.LastPairId,
                    MarkerCount = _snapshot.MarkerCount,
                    ReadFailed = _snapshot.ReadFailed,
                    WriteFailed = _snapshot.WriteFailed,
                    LimitExceeded = _snapshot.LimitExceeded,
                    CulledByCacheLimit = _snapshot.CulledByCacheLimit,
                    LastOperation = _snapshot.LastOperation,
                    LastUiAction = _snapshot.LastUiAction,
                    LastJumpResult = _snapshot.LastJumpResult,
                    UiOnlyActionCount = _snapshot.UiOnlyActionCount,
                    PickerOpen = _snapshot.PickerOpen,
                    PickerLastDraw = _snapshot.PickerLastDraw,
                    PickerLastFullscreenDraw = _snapshot.PickerLastFullscreenDraw,
                    PickerDrawRoute = _snapshot.PickerDrawRoute,
                    PickerDrawSkippedReason = _snapshot.PickerDrawSkippedReason,
                    PickerLastClick = _snapshot.PickerLastClick,
                    PickerLastCloseReason = _snapshot.PickerLastCloseReason,
                    LastReadUtc = _snapshot.LastReadUtc,
                    LastWriteUtc = _snapshot.LastWriteUtc
                };
            }
        }

        public static void RecordEnabled(bool enabled)
        {
            lock (SyncRoot)
            {
                _snapshot.Enabled = enabled;
            }
        }

        public static void RecordUiAction(string action, string resultCode, bool uiOnly)
        {
            lock (SyncRoot)
            {
                _snapshot.LastUiAction = action ?? string.Empty;
                if (string.Equals(action, "jump", StringComparison.OrdinalIgnoreCase))
                {
                    _snapshot.LastJumpResult = resultCode ?? string.Empty;
                }

                if (uiOnly)
                {
                    _snapshot.UiOnlyActionCount++;
                }
            }
        }

        public static void RecordPickerOpen()
        {
            lock (SyncRoot)
            {
                _snapshot.PickerOpen = true;
                _snapshot.PickerDrawSkippedReason = string.Empty;
            }
        }

        public static void RecordPickerDraw()
        {
            RecordPickerDraw("uiOverlay");
        }

        public static void RecordPickerDraw(string route)
        {
            lock (SyncRoot)
            {
                var now = DateTime.UtcNow;
                _snapshot.PickerLastDraw = now;
                _snapshot.PickerDrawRoute = route ?? string.Empty;
                _snapshot.PickerDrawSkippedReason = string.Empty;
                if (string.Equals(route, "fullscreenMap", StringComparison.Ordinal))
                {
                    _snapshot.PickerLastFullscreenDraw = now;
                }
            }
        }

        public static void RecordPickerDrawSkipped(string route, string reason)
        {
            lock (SyncRoot)
            {
                _snapshot.PickerDrawRoute = route ?? string.Empty;
                _snapshot.PickerDrawSkippedReason = reason ?? string.Empty;
            }
        }

        public static void RecordPickerClick()
        {
            lock (SyncRoot)
            {
                _snapshot.PickerLastClick = DateTime.UtcNow;
            }
        }

        public static void RecordPickerClosed(string reason)
        {
            lock (SyncRoot)
            {
                _snapshot.PickerOpen = false;
                _snapshot.PickerLastCloseReason = reason ?? string.Empty;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldMapMarkerSnapshot();
            }
        }
    }

    public sealed class PlayerWorldMapMarkerSnapshot
    {
        public bool Enabled { get; set; }
        public string LastStatus { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public int MarkerCount { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public bool LimitExceeded { get; set; }
        public bool CulledByCacheLimit { get; set; }
        public string LastOperation { get; set; }
        public string LastUiAction { get; set; }
        public string LastJumpResult { get; set; }
        public int UiOnlyActionCount { get; set; }
        public bool PickerOpen { get; set; }
        public DateTime? PickerLastDraw { get; set; }
        public DateTime? PickerLastFullscreenDraw { get; set; }
        public string PickerDrawRoute { get; set; }
        public string PickerDrawSkippedReason { get; set; }
        public DateTime? PickerLastClick { get; set; }
        public string PickerLastCloseReason { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldMapMarkerSnapshot()
        {
            LastStatus = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
            LastOperation = string.Empty;
            LastUiAction = string.Empty;
            LastJumpResult = string.Empty;
            PickerDrawRoute = string.Empty;
            PickerDrawSkippedReason = string.Empty;
            PickerLastCloseReason = string.Empty;
        }
    }
}
