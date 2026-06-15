using System;
using JueMingZ.Compat;
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
                    LastJumpRequestedTileX = _snapshot.LastJumpRequestedTileX,
                    LastJumpRequestedTileY = _snapshot.LastJumpRequestedTileY,
                    LastJumpWrittenMapPosX = _snapshot.LastJumpWrittenMapPosX,
                    LastJumpWrittenMapPosY = _snapshot.LastJumpWrittenMapPosY,
                    LastJumpScale = _snapshot.LastJumpScale,
                    LastJumpReleasedUiCapture = _snapshot.LastJumpReleasedUiCapture,
                    LastJumpClearedPanState = _snapshot.LastJumpClearedPanState,
                    LastJumpConsumedButtonPulse = _snapshot.LastJumpConsumedButtonPulse,
                    LastJumpVanillaMapInputHandoff = _snapshot.LastJumpVanillaMapInputHandoff,
                    LastBlockedReason = _snapshot.LastBlockedReason,
                    LastTransformRoute = _snapshot.LastTransformRoute,
                    LastTransformScreenWidth = _snapshot.LastTransformScreenWidth,
                    LastTransformScreenHeight = _snapshot.LastTransformScreenHeight,
                    LastTransformMapTopLeftX = _snapshot.LastTransformMapTopLeftX,
                    LastTransformMapTopLeftY = _snapshot.LastTransformMapTopLeftY,
                    LastTransformScale = _snapshot.LastTransformScale,
                    LastTransformMapFullscreenPosX = _snapshot.LastTransformMapFullscreenPosX,
                    LastTransformMapFullscreenPosY = _snapshot.LastTransformMapFullscreenPosY,
                    LastTransformGameUpdateCount = _snapshot.LastTransformGameUpdateCount,
                    LastTransformUtc = _snapshot.LastTransformUtc,
                    LastRightClickMouseX = _snapshot.LastRightClickMouseX,
                    LastRightClickMouseY = _snapshot.LastRightClickMouseY,
                    LastRightClickTileX = _snapshot.LastRightClickTileX,
                    LastRightClickTileY = _snapshot.LastRightClickTileY,
                    LastRightClickTransformSource = _snapshot.LastRightClickTransformSource,
                    LastRightClickFallbackReason = _snapshot.LastRightClickFallbackReason,
                    LastRightClickMapFullscreenPosX = _snapshot.LastRightClickMapFullscreenPosX,
                    LastRightClickMapFullscreenPosY = _snapshot.LastRightClickMapFullscreenPosY,
                    LastRightClickMapScale = _snapshot.LastRightClickMapScale,
                    LastRightClickTransformAgeUpdates = _snapshot.LastRightClickTransformAgeUpdates,
                    UiOnlyActionCount = _snapshot.UiOnlyActionCount,
                    PickerOpen = _snapshot.PickerOpen,
                    PickerAnchorScreenX = _snapshot.PickerAnchorScreenX,
                    PickerAnchorScreenY = _snapshot.PickerAnchorScreenY,
                    PickerPanelX = _snapshot.PickerPanelX,
                    PickerPanelY = _snapshot.PickerPanelY,
                    PickerPanelClamped = _snapshot.PickerPanelClamped,
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

        public static void RecordJumpSafety(bool releasedUiCapture, bool clearedPanState)
        {
            lock (SyncRoot)
            {
                _snapshot.LastJumpReleasedUiCapture = releasedUiCapture;
                _snapshot.LastJumpClearedPanState = clearedPanState;
                _snapshot.LastJumpVanillaMapInputHandoff = releasedUiCapture && clearedPanState;
            }
        }

        public static void RecordJumpState(
            int requestedTileX,
            int requestedTileY,
            float writtenMapPosX,
            float writtenMapPosY,
            float scale,
            bool releasedUiCapture,
            bool clearedPanState,
            bool consumedButtonPulse,
            bool vanillaMapInputHandoff)
        {
            lock (SyncRoot)
            {
                _snapshot.LastJumpRequestedTileX = requestedTileX;
                _snapshot.LastJumpRequestedTileY = requestedTileY;
                _snapshot.LastJumpWrittenMapPosX = writtenMapPosX;
                _snapshot.LastJumpWrittenMapPosY = writtenMapPosY;
                _snapshot.LastJumpScale = scale;
                _snapshot.LastJumpReleasedUiCapture = releasedUiCapture;
                _snapshot.LastJumpClearedPanState = clearedPanState;
                _snapshot.LastJumpConsumedButtonPulse = consumedButtonPulse;
                _snapshot.LastJumpVanillaMapInputHandoff = vanillaMapInputHandoff;
            }
        }

        public static void RecordBlockedReason(string reason)
        {
            lock (SyncRoot)
            {
                _snapshot.LastBlockedReason = reason ?? string.Empty;
            }
        }

        public static void RecordFullscreenTransform(MapCustomMarkerFullscreenTransformSnapshot transform)
        {
            lock (SyncRoot)
            {
                if (transform == null)
                {
                    return;
                }

                _snapshot.LastTransformRoute = transform.Route ?? string.Empty;
                _snapshot.LastTransformScreenWidth = transform.ScreenWidth;
                _snapshot.LastTransformScreenHeight = transform.ScreenHeight;
                _snapshot.LastTransformMapTopLeftX = transform.MapTopLeftX;
                _snapshot.LastTransformMapTopLeftY = transform.MapTopLeftY;
                _snapshot.LastTransformScale = transform.MapScale;
                _snapshot.LastTransformMapFullscreenPosX = transform.MapFullscreenPosX;
                _snapshot.LastTransformMapFullscreenPosY = transform.MapFullscreenPosY;
                _snapshot.LastTransformGameUpdateCount = transform.GameUpdateCount;
                _snapshot.LastTransformUtc = transform.Utc;
            }
        }

        public static void RecordRightClick(MapCustomMarkerMapPoint point)
        {
            lock (SyncRoot)
            {
                if (point == null)
                {
                    return;
                }

                _snapshot.LastRightClickMouseX = point.ScreenX;
                _snapshot.LastRightClickMouseY = point.ScreenY;
                _snapshot.LastRightClickTileX = point.TileX;
                _snapshot.LastRightClickTileY = point.TileY;
                _snapshot.LastRightClickTransformSource = point.TransformSource ?? string.Empty;
                _snapshot.LastRightClickFallbackReason = point.FallbackReason ?? string.Empty;
                _snapshot.LastRightClickMapFullscreenPosX = point.CurrentMapFullscreenPosX;
                _snapshot.LastRightClickMapFullscreenPosY = point.CurrentMapFullscreenPosY;
                _snapshot.LastRightClickMapScale = point.CurrentMapScale;
                _snapshot.LastRightClickTransformAgeUpdates = point.TransformAgeUpdates;
            }
        }

        public static void RecordPickerAnchor(int anchorScreenX, int anchorScreenY, int panelX, int panelY, bool clamped)
        {
            lock (SyncRoot)
            {
                _snapshot.PickerAnchorScreenX = anchorScreenX;
                _snapshot.PickerAnchorScreenY = anchorScreenY;
                _snapshot.PickerPanelX = panelX;
                _snapshot.PickerPanelY = panelY;
                _snapshot.PickerPanelClamped = clamped;
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
        public int LastJumpRequestedTileX { get; set; }
        public int LastJumpRequestedTileY { get; set; }
        public float LastJumpWrittenMapPosX { get; set; }
        public float LastJumpWrittenMapPosY { get; set; }
        public float LastJumpScale { get; set; }
        public bool LastJumpReleasedUiCapture { get; set; }
        public bool LastJumpClearedPanState { get; set; }
        public bool LastJumpConsumedButtonPulse { get; set; }
        public bool LastJumpVanillaMapInputHandoff { get; set; }
        public string LastBlockedReason { get; set; }
        public string LastTransformRoute { get; set; }
        public int LastTransformScreenWidth { get; set; }
        public int LastTransformScreenHeight { get; set; }
        public float LastTransformMapTopLeftX { get; set; }
        public float LastTransformMapTopLeftY { get; set; }
        public float LastTransformScale { get; set; }
        public float LastTransformMapFullscreenPosX { get; set; }
        public float LastTransformMapFullscreenPosY { get; set; }
        public long LastTransformGameUpdateCount { get; set; }
        public DateTime? LastTransformUtc { get; set; }
        public int LastRightClickMouseX { get; set; }
        public int LastRightClickMouseY { get; set; }
        public int LastRightClickTileX { get; set; }
        public int LastRightClickTileY { get; set; }
        public string LastRightClickTransformSource { get; set; }
        public string LastRightClickFallbackReason { get; set; }
        public float LastRightClickMapFullscreenPosX { get; set; }
        public float LastRightClickMapFullscreenPosY { get; set; }
        public float LastRightClickMapScale { get; set; }
        public long LastRightClickTransformAgeUpdates { get; set; }
        public int UiOnlyActionCount { get; set; }
        public bool PickerOpen { get; set; }
        public int PickerAnchorScreenX { get; set; }
        public int PickerAnchorScreenY { get; set; }
        public int PickerPanelX { get; set; }
        public int PickerPanelY { get; set; }
        public bool PickerPanelClamped { get; set; }
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
            LastBlockedReason = string.Empty;
            LastTransformRoute = string.Empty;
            LastTransformGameUpdateCount = -1;
            LastRightClickTransformSource = string.Empty;
            LastRightClickFallbackReason = string.Empty;
            LastRightClickTransformAgeUpdates = -1;
            PickerDrawRoute = string.Empty;
            PickerDrawSkippedReason = string.Empty;
            PickerLastCloseReason = string.Empty;
        }
    }
}
