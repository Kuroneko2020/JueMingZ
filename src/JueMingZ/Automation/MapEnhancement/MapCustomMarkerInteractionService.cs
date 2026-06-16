using System;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Records;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapCustomMarkerInteractionService
    {
        private static readonly object SyncRoot = new object();
        private static MapCustomMarkerPendingPlacement _placement;
        private static int? _pendingIconItemId;
        private static bool _rightDownLastTick;
        private static bool _ignoreRightCloseUntilReleased;
        private static string _lastStatus = "idle";
        private static string _lastMessage = string.Empty;

        public static bool IsPickerOpen
        {
            get
            {
                lock (SyncRoot)
                {
                    return _placement != null;
                }
            }
        }

        public static void Tick(GameStateSnapshot snapshot, RuntimeSettingsSnapshot settings)
        {
            settings = settings ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var enabled = settings.MapCustomMarkersEnabled;

            var blockedReason = string.Empty;
            if (!enabled || !CanInteract(snapshot, out blockedReason))
            {
                if (!enabled)
                {
                    blockedReason = "disabled";
                }

                PlayerWorldMapMarkerDiagnostics.RecordBlockedReason(blockedReason);
                ClosePicker("blocked");
                _rightDownLastTick = false;
                return;
            }

            ConsumePendingSelection();

            bool rightPressed;
            string rightMessage;
            if (!TerrariaUiMouseCompat.TryReadMouseRightPressedEdge(out rightPressed, out rightMessage))
            {
                RecordStatus("inputUnavailable", rightMessage);
                return;
            }

            var rightDown = TerrariaUiMouseCompat.IsMouseRightCurrentlyDown();
            ReleaseRightCloseGateIfNeeded(rightDown);
            var edge = rightPressed || (rightDown && !_rightDownLastTick);
            _rightDownLastTick = rightDown;
            if (!edge)
            {
                return;
            }

            if (IsPickerOpen)
            {
                if (ShouldIgnoreRightClose())
                {
                    ConsumeRightClick();
                    RecordStatus("rightClickIgnored", "map marker style picker close ignored until right button release");
                    return;
                }

                ClosePicker("rightClickClose");
                ConsumeRightClick();
                return;
            }

            MapCustomMarkerMapPoint point;
            string message;
            if (!MapCustomMarkerMapCompat.TryReadFullscreenMapMouseTile(out point, out message))
            {
                RecordStatus("mapUnavailable", message);
                return;
            }

            PlayerWorldMapMarkerDiagnostics.RecordRightClick(point);
            var placement = CreatePlacement(point);
            lock (SyncRoot)
            {
                _placement = placement;
                _pendingIconItemId = null;
                _ignoreRightCloseUntilReleased = true;
            }

            ConsumeRightClick();
            PlayerWorldMapMarkerDiagnostics.RecordPickerOpen();
            PlayerWorldMapMarkerTraceRecorder.Record(CreateTraceEvent("pickerOpen", placement));
            RecordStatus("pickerOpen", "map marker style picker opened");
        }

        public static MapCustomMarkerPendingPlacement GetPlacementSnapshot()
        {
            lock (SyncRoot)
            {
                return _placement == null ? null : _placement.Clone();
            }
        }

        public static void RequestStyleSelection(int iconItemId)
        {
            lock (SyncRoot)
            {
                if (_placement == null)
                {
                    return;
                }

                _pendingIconItemId = PlayerWorldMapMarkerConstants.NormalizeIconItemId(iconItemId);
                PlayerWorldMapMarkerDiagnostics.RecordPickerClick();
            }
        }

        public static void ClosePicker(string reason)
        {
            lock (SyncRoot)
            {
                _placement = null;
                _pendingIconItemId = null;
                _ignoreRightCloseUntilReleased = false;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerClosed(reason);
                RecordStatus("pickerClosed", reason);
            }
            else
            {
                PlayerWorldMapMarkerDiagnostics.RecordPickerClosed("selectionCommitted");
            }
        }

        internal static bool ShouldOpenPickerForTesting(bool enabled, bool canInteract, bool rightPressed, bool rightDown, bool previousRightDown)
        {
            return enabled && canInteract && (rightPressed || (rightDown && !previousRightDown));
        }

        internal static bool ShouldClosePickerForTesting(bool pickerOpen, bool rightPressed, bool rightDown, bool previousRightDown, bool ignoreRightCloseUntilReleased)
        {
            var edge = rightPressed || (rightDown && !previousRightDown);
            return pickerOpen && edge && !ignoreRightCloseUntilReleased;
        }

        internal static bool ShouldReleaseRightCloseGateForTesting(bool ignoreRightCloseUntilReleased, bool rightDown)
        {
            return ignoreRightCloseUntilReleased && !rightDown;
        }

        internal static string GetLastStatusForTesting()
        {
            return _lastStatus;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _placement = null;
                _pendingIconItemId = null;
                _ignoreRightCloseUntilReleased = false;
            }

            _rightDownLastTick = false;
            _lastStatus = "idle";
            _lastMessage = string.Empty;
        }

        internal static MapCustomMarkerPendingPlacement CreatePlacementForTesting(MapCustomMarkerMapPoint point)
        {
            return CreatePlacement(point);
        }

        private static bool CanInteract(GameStateSnapshot snapshot, out string blockedReason)
        {
            blockedReason = string.Empty;
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                blockedReason = "mapClosed";
                return false;
            }

            if (LegacyMainUiState.Visible)
            {
                blockedReason = "f5Visible";
                return false;
            }

            if (LegacyTextInput.IsAnyFocused)
            {
                blockedReason = "textInputFocused";
                return false;
            }

            var ui = snapshot.Ui;
            if (ui == null)
            {
                return true;
            }

            if (!ui.GameInputAvailable)
            {
                blockedReason = "gameInputUnavailable";
                return false;
            }

            if (ui.ChatOpen)
            {
                blockedReason = "chatOpen";
                return false;
            }

            if (ui.ChestOpen)
            {
                blockedReason = "chestOpen";
                return false;
            }

            if (ui.NpcChatOpen)
            {
                blockedReason = "npcChatOpen";
                return false;
            }

            return true;
        }

        private static void ConsumePendingSelection()
        {
            MapCustomMarkerPendingPlacement placement;
            int? iconItemId;
            lock (SyncRoot)
            {
                placement = _placement == null ? null : _placement.Clone();
                iconItemId = _pendingIconItemId;
                _pendingIconItemId = null;
            }

            if (placement == null || !iconItemId.HasValue)
            {
                return;
            }

            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                identity == null ||
                !identity.IsResolved ||
                string.IsNullOrWhiteSpace(identity.PairId))
            {
                var identityMessage = identity == null ? "identity unavailable" : identity.FailureReason;
                PlayerWorldMapMarkerTraceRecorder.Record(CreateTraceEvent(
                    "markerCreate",
                    placement,
                    iconItemId.Value,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    "identityUnavailable",
                    identityMessage));
                RecordStatus("identityUnavailable", identityMessage);
                return;
            }

            var now = PlayerWorldMapMarkerConstants.FormatUtc(DateTime.UtcNow);
            var marker = new PlayerWorldMapMarkerRecord
            {
                MarkerId = Guid.NewGuid().ToString("N"),
                TileX = placement.TileX,
                TileY = placement.TileY,
                IconItemId = iconItemId.Value,
                Name = string.Empty,
                CreatedUtc = now,
                UpdatedUtc = now,
                SortOrder = 0
            };

            var write = PlayerWorldMapMarkerStore.AddMarkerForPair(
                identity.PairId,
                placement.WorldSizeX,
                placement.WorldSizeY,
                marker);
            PlayerWorldMapMarkerTraceRecorder.Record(CreateTraceEvent(
                "markerCreate",
                placement,
                iconItemId.Value,
                identity.PairId,
                marker.MarkerId,
                true,
                write != null && write.Succeeded,
                write == null ? "writeUnavailable" : write.Status,
                write == null ? "write unavailable" : write.Message));

            if (write != null && write.Succeeded)
            {
                ClosePicker(string.Empty);
                RecordStatus("markerCreated", "map marker created");
            }
            else
            {
                RecordStatus(write == null ? "writeFailed" : write.Status, write == null ? "write unavailable" : write.Message);
            }
        }

        private static MapCustomMarkerPendingPlacement CreatePlacement(MapCustomMarkerMapPoint point)
        {
            if (point == null)
            {
                return null;
            }

            return new MapCustomMarkerPendingPlacement
            {
                TileX = point.TileX,
                TileY = point.TileY,
                ScreenX = point.ScreenX,
                ScreenY = point.ScreenY,
                ScreenWidth = point.ScreenWidth,
                ScreenHeight = point.ScreenHeight,
                WorldSizeX = point.WorldSizeX,
                WorldSizeY = point.WorldSizeY,
                TransformSource = point.TransformSource ?? string.Empty,
                FallbackReason = point.FallbackReason ?? string.Empty,
                MapTopLeftX = point.MapTopLeftX,
                MapTopLeftY = point.MapTopLeftY,
                MapScale = point.MapScale,
                CurrentMapFullscreenPosX = point.CurrentMapFullscreenPosX,
                CurrentMapFullscreenPosY = point.CurrentMapFullscreenPosY,
                CurrentMapScale = point.CurrentMapScale,
                CurrentGameUpdateCount = point.CurrentGameUpdateCount,
                TransformAgeUpdates = point.TransformAgeUpdates
            };
        }

        private static PlayerWorldMapMarkerTraceEvent CreateTraceEvent(
            string eventType,
            MapCustomMarkerPendingPlacement placement,
            int iconItemId = -1,
            string pairId = "",
            string markerId = "",
            bool writeAttempted = false,
            bool writeSucceeded = false,
            string writeStatus = "",
            string writeMessage = "")
        {
            if (placement == null)
            {
                return null;
            }

            return new PlayerWorldMapMarkerTraceEvent
            {
                UtcNow = DateTime.UtcNow,
                RuntimeVersion = JueMingZRuntime.Version,
                EventType = eventType ?? string.Empty,
                PairId = pairId ?? string.Empty,
                MarkerId = markerId ?? string.Empty,
                IconItemId = iconItemId,
                WriteAttempted = writeAttempted,
                WriteSucceeded = writeSucceeded,
                WriteStatus = writeStatus ?? string.Empty,
                WriteMessage = writeMessage ?? string.Empty,
                TileX = placement.TileX,
                TileY = placement.TileY,
                ScreenX = placement.ScreenX,
                ScreenY = placement.ScreenY,
                ScreenWidth = placement.ScreenWidth,
                ScreenHeight = placement.ScreenHeight,
                WorldSizeX = placement.WorldSizeX,
                WorldSizeY = placement.WorldSizeY,
                TransformSource = placement.TransformSource,
                FallbackReason = placement.FallbackReason,
                MapTopLeftX = placement.MapTopLeftX,
                MapTopLeftY = placement.MapTopLeftY,
                MapScale = placement.MapScale,
                CurrentMapFullscreenPosX = placement.CurrentMapFullscreenPosX,
                CurrentMapFullscreenPosY = placement.CurrentMapFullscreenPosY,
                CurrentMapScale = placement.CurrentMapScale,
                CurrentGameUpdateCount = placement.CurrentGameUpdateCount,
                TransformAgeUpdates = placement.TransformAgeUpdates
            };
        }

        private static void ReleaseRightCloseGateIfNeeded(bool rightDown)
        {
            lock (SyncRoot)
            {
                if (_ignoreRightCloseUntilReleased && !rightDown)
                {
                    _ignoreRightCloseUntilReleased = false;
                }
            }
        }

        private static bool ShouldIgnoreRightClose()
        {
            lock (SyncRoot)
            {
                return _ignoreRightCloseUntilReleased;
            }
        }

        private static void ConsumeRightClick()
        {
            string message;
            TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("MouseRight", out message);
        }

        private static void RecordStatus(string status, string message)
        {
            _lastStatus = status ?? string.Empty;
            _lastMessage = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                LogThrottle.InfoThrottled(
                    "map-custom-marker-interaction-" + _lastStatus,
                    TimeSpan.FromSeconds(2),
                    "MapCustomMarkerInteractionService",
                    _lastMessage + " (featureId=" + FeatureIds.MapCustomMarkers + ")");
            }
        }
    }

    internal sealed class MapCustomMarkerPendingPlacement
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int ScreenX { get; set; }
        public int ScreenY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public string TransformSource { get; set; }
        public string FallbackReason { get; set; }
        public float MapTopLeftX { get; set; }
        public float MapTopLeftY { get; set; }
        public float MapScale { get; set; }
        public float CurrentMapFullscreenPosX { get; set; }
        public float CurrentMapFullscreenPosY { get; set; }
        public float CurrentMapScale { get; set; }
        public long CurrentGameUpdateCount { get; set; }
        public long TransformAgeUpdates { get; set; }

        public MapCustomMarkerPendingPlacement Clone()
        {
            return new MapCustomMarkerPendingPlacement
            {
                TileX = TileX,
                TileY = TileY,
                ScreenX = ScreenX,
                ScreenY = ScreenY,
                ScreenWidth = ScreenWidth,
                ScreenHeight = ScreenHeight,
                WorldSizeX = WorldSizeX,
                WorldSizeY = WorldSizeY,
                TransformSource = TransformSource ?? string.Empty,
                FallbackReason = FallbackReason ?? string.Empty,
                MapTopLeftX = MapTopLeftX,
                MapTopLeftY = MapTopLeftY,
                MapScale = MapScale,
                CurrentMapFullscreenPosX = CurrentMapFullscreenPosX,
                CurrentMapFullscreenPosY = CurrentMapFullscreenPosY,
                CurrentMapScale = CurrentMapScale,
                CurrentGameUpdateCount = CurrentGameUpdateCount,
                TransformAgeUpdates = TransformAgeUpdates
            };
        }
    }
}
