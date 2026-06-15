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

            if (!enabled || !CanInteract(snapshot))
            {
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

            lock (SyncRoot)
            {
                _placement = new MapCustomMarkerPendingPlacement
                {
                    TileX = point.TileX,
                    TileY = point.TileY,
                    ScreenX = point.ScreenX,
                    ScreenY = point.ScreenY,
                    WorldSizeX = point.WorldSizeX,
                    WorldSizeY = point.WorldSizeY
                };
                _pendingIconItemId = null;
                _ignoreRightCloseUntilReleased = true;
            }

            ConsumeRightClick();
            PlayerWorldMapMarkerDiagnostics.RecordPickerOpen();
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

        private static bool CanInteract(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                return false;
            }

            if (LegacyMainUiState.Visible || LegacyTextInput.IsAnyFocused)
            {
                return false;
            }

            var ui = snapshot.Ui;
            return ui == null ||
                   (ui.GameInputAvailable &&
                    !ui.ChatOpen &&
                    !ui.ChestOpen &&
                    !ui.NpcChatOpen);
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
                RecordStatus("identityUnavailable", identity == null ? "identity unavailable" : identity.FailureReason);
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
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }

        public MapCustomMarkerPendingPlacement Clone()
        {
            return new MapCustomMarkerPendingPlacement
            {
                TileX = TileX,
                TileY = TileY,
                ScreenX = ScreenX,
                ScreenY = ScreenY,
                WorldSizeX = WorldSizeX,
                WorldSizeY = WorldSizeY
            };
        }
    }
}
