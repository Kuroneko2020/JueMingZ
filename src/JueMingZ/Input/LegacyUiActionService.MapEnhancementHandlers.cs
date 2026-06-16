using System;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Records;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleMapPersistentDeathMarkersMode(LegacyUiCommand command, string payload)
        {
            var before = BuildMapEnhancementUiStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.MapPersistentDeathMarkersEnabled != enabled;
            settings.MapPersistentDeathMarkersEnabled = enabled;
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.Toggle.MapPersistentDeathMarkers",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled ? "Persistent death markers enabled." : "Persistent death markers disabled.",
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapPersistentDeathMarkers) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"deathRecordingAffected\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapQuickAnnouncementMode(LegacyUiCommand command, string payload)
        {
            var before = BuildMapEnhancementUiStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.MapQuickAnnouncementEnabled != enabled;
            settings.MapQuickAnnouncementEnabled = enabled;
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.Toggle.MapQuickAnnouncement",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled ? "Map quick announcement enabled." : "Map quick announcement disabled.",
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"runtimeTriggerImplemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapQuickAnnouncement) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapCustomMarkersMode(LegacyUiCommand command, string payload)
        {
            var before = BuildMapEnhancementUiStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.MapCustomMarkersEnabled != enabled;
            settings.MapCustomMarkersEnabled = enabled;
            ConfigService.SaveAll();

            Record(
                command,
                "Ui.Toggle.MapCustomMarkers",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                enabled ? "Map custom markers enabled." : "Map custom markers disabled.",
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapCustomMarkers) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapCustomMarkersPage(LegacyUiCommand command, string action)
        {
            var before = BuildMapEnhancementUiStateJson();
            action = (action ?? string.Empty).Trim();
            var outcome = "Succeeded";
            var message = "Map custom marker page command handled.";
            var delta = 0;
            if (string.Equals(action, "prev", StringComparison.OrdinalIgnoreCase))
            {
                delta = -1;
                message = "Map custom marker list moved to previous page.";
            }
            else if (string.Equals(action, "next", StringComparison.OrdinalIgnoreCase))
            {
                delta = 1;
                message = "Map custom marker list moved to next page.";
            }
            else
            {
                outcome = "Rejected";
                message = "Map custom marker page command rejected because the action was invalid.";
            }

            if (delta != 0)
            {
                LegacyMainWindow.MoveMapCustomMarkerPage(delta);
                LegacyTextInput.ClearFocus();
            }

            Record(
                command,
                "Ui.MapCustomMarkers.Page",
                "UI",
                outcome,
                message,
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapCustomMarkers) + "\",\"action\":\"" + EscapeJson(action) + "\",\"pageIndex\":" + IntRaw(LegacyMainWindow.GetMapCustomMarkerPageIndex()) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapCustomMarkerCommand(LegacyUiCommand command, string payload)
        {
            var before = BuildMapEnhancementUiStateJson();
            string action;
            string markerId;
            SplitMapCustomMarkerPayload(payload, out action, out markerId);
            action = (action ?? string.Empty).Trim();
            markerId = (markerId ?? string.Empty).Trim();

            PlayerWorldIdentityResolution identity;
            var hasIdentity = PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) &&
                              identity != null &&
                              identity.IsResolved &&
                              !string.IsNullOrWhiteSpace(identity.PairId);
            if (!hasIdentity)
            {
                LegacyTextInput.ClearFocus();
                Record(
                    command,
                    "Ui.MapCustomMarkers." + BuildMapCustomMarkerActionLabel(action),
                    "UI",
                    "Failed",
                    "Map custom marker command failed because player-world identity is unavailable.",
                    before,
                    BuildMapEnhancementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapCustomMarkers) + "\",\"action\":\"" + EscapeJson(action) + "\",\"markerId\":\"" + EscapeJson(markerId) + "\",\"identityResolved\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            HandleMapCustomMarkerCommandForPair(command, action, markerId, identity.PairId, before);
        }

        private static void HandleMapCustomMarkerCommandForPair(LegacyUiCommand command, string action, string markerId, string pairId, string before)
        {
            var outcome = "Succeeded";
            var message = "Map custom marker command handled.";
            var metadataAction = action ?? string.Empty;
            PlayerWorldMapMarkerWriteResult write = null;
            MapFullscreenJumpResult jump = null;
            var resultCode = string.Empty;
            var markerTileX = 0;
            var markerTileY = 0;
            var requestedTileX = 0;
            var requestedTileY = 0;
            var jumpScale = 0f;
            var jumpWrittenMapPosX = 0f;
            var jumpWrittenMapPosY = 0f;
            var jumpAttempted = false;
            var jumpReleasedUiCapture = false;
            var jumpClosedF5 = false;
            var jumpClearedPanState = false;
            var jumpConsumedButtonPulse = false;
            var jumpVanillaMapInputHandoff = false;
            var jumpButtonPulseMessage = string.Empty;

            if (string.Equals(action, "name", StringComparison.OrdinalIgnoreCase))
            {
                var inputId = LegacyMainWindow.BuildMapMarkerNameInputId(markerId);
                if (LegacyTextInput.IsFocused(inputId))
                {
                    write = PlayerWorldMapMarkerStore.RenameMarkerForPair(pairId, markerId, LegacyTextInput.GetDraft(inputId));
                    if (write.Succeeded)
                    {
                        LegacyTextInput.ClearFocus();
                    }

                    outcome = write.Succeeded ? (write.Changed ? "Succeeded" : "NotApplicable") : "Failed";
                    message = write.Succeeded ? "Map custom marker name saved." : "Map custom marker name save failed: " + write.Message;
                }
                else if (command.IsDoubleClick)
                {
                    var read = PlayerWorldMapMarkerStore.ReadForPair(pairId);
                    var marker = FindMapMarker(read, markerId);
                    if (marker == null)
                    {
                        outcome = "Failed";
                        message = "Map custom marker name edit failed because marker was not found.";
                    }
                    else
                    {
                        LegacyTextInput.Focus(inputId, marker.Name);
                        outcome = "Succeeded";
                        message = "Map custom marker name input focused.";
                    }
                }
                else
                {
                    outcome = "NotApplicable";
                    message = "Double-click the marker name field to edit.";
                }
            }
            else if (string.Equals(action, "confirm-name", StringComparison.OrdinalIgnoreCase))
            {
                var inputId = LegacyMainWindow.BuildMapMarkerNameInputId(markerId);
                if (LegacyTextInput.IsFocused(inputId))
                {
                    write = PlayerWorldMapMarkerStore.RenameMarkerForPair(pairId, markerId, LegacyTextInput.GetDraft(inputId));
                    if (write.Succeeded)
                    {
                        LegacyTextInput.ClearFocus();
                    }

                    outcome = write.Succeeded ? (write.Changed ? "Succeeded" : "NotApplicable") : "Failed";
                    message = write.Succeeded ? "Map custom marker name confirmed." : "Map custom marker name confirm failed: " + write.Message;
                }
                else
                {
                    outcome = "NotApplicable";
                    resultCode = "nameInputNotFocused";
                    message = "Map custom marker name confirm skipped because the name field is not being edited.";
                }
            }
            else if (string.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
            {
                write = PlayerWorldMapMarkerStore.DeleteMarkerForPair(pairId, markerId);
                if (write.Succeeded)
                {
                    LegacyTextInput.ClearFocus();
                }

                outcome = write.Succeeded ? "Succeeded" : "Failed";
                message = write.Succeeded ? "Map custom marker deleted." : "Map custom marker delete failed: " + write.Message;
            }
            else if (string.Equals(action, "jump", StringComparison.OrdinalIgnoreCase))
            {
                jumpAttempted = true;
                TrySaveFocusedMapMarkerName(pairId, markerId);
                var read = PlayerWorldMapMarkerCache.ReadForPair(pairId);
                var marker = FindMapMarker(read, markerId);
                if (marker == null)
                {
                    outcome = "Failed";
                    resultCode = read == null || !read.IdentityResolved ? "identityUnavailable" : "markerNotFound";
                    message = "Map custom marker jump failed because the marker was not found.";
                }
                else if (MapFullscreenCompat.TryJumpToTile(marker.TileX, marker.TileY, out jump))
                {
                    outcome = "Succeeded";
                    resultCode = jump.ResultCode;
                    requestedTileX = marker.TileX;
                    requestedTileY = marker.TileY;
                    markerTileX = jump.TileX;
                    markerTileY = jump.TileY;
                    jumpScale = jump.Scale;
                    jumpWrittenMapPosX = jump.WrittenMapPosX;
                    jumpWrittenMapPosY = jump.WrittenMapPosY;
                    jumpClearedPanState = jump.ClearedPanState;
                    message = "Map custom marker jump opened the fullscreen map.";
                    // Jump stays a fullscreen-map UI state change only. Consume
                    // only the F5 button's left-click pulse, then vanilla owns
                    // fullscreen map drag and ping input after the button release.
                    var release = LegacyMainUiState.HideForMapCustomMarkerJumpAndReleaseCapture();
                    jumpReleasedUiCapture = release != null && release.ReleasedUiCapture;
                    jumpClosedF5 = release != null && release.F5WasVisible;
                    jumpConsumedButtonPulse = release != null && release.ConsumedJumpButtonPulse;
                    jumpVanillaMapInputHandoff = release != null && release.VanillaMapInputHandoff;
                    jumpButtonPulseMessage = release == null ? string.Empty : release.ConsumeJumpButtonPulseMessage ?? string.Empty;
                }
                else
                {
                    outcome = "Failed";
                    resultCode = jump == null ? "failed" : jump.ResultCode;
                    requestedTileX = marker.TileX;
                    requestedTileY = marker.TileY;
                    markerTileX = marker.TileX;
                    markerTileY = marker.TileY;
                    jumpScale = jump == null ? 0f : jump.Scale;
                    jumpWrittenMapPosX = jump == null ? 0f : jump.WrittenMapPosX;
                    jumpWrittenMapPosY = jump == null ? 0f : jump.WrittenMapPosY;
                    jumpClearedPanState = jump != null && jump.ClearedPanState;
                    message = jump == null ? "Map custom marker jump failed." : jump.Message;
                }
            }
            else if (IsMapCustomMarkerUiOnlyAction(action))
            {
                TrySaveFocusedMapMarkerName(pairId, markerId);
                outcome = "NotImplemented";
                resultCode = "uiOnlyNotImplemented";
                // Navigation, teleport and autopilot are intentionally UI-only
                // in this plan. They must not scan paths, consume potions,
                // move the player, or submit movement input from this handler.
                message = "Map custom marker " + action + " is a UI-only placeholder in this stage.";
            }
            else
            {
                outcome = "Rejected";
                resultCode = "invalidAction";
                message = "Map custom marker command rejected because the action was invalid.";
            }

            PlayerWorldMapMarkerDiagnostics.RecordUiAction(metadataAction, resultCode, IsMapCustomMarkerUiOnlyAction(action));
            if (jumpAttempted)
            {
                PlayerWorldMapMarkerDiagnostics.RecordJumpState(
                    requestedTileX,
                    requestedTileY,
                    jumpWrittenMapPosX,
                    jumpWrittenMapPosY,
                    jumpScale,
                    jumpReleasedUiCapture,
                    jumpClearedPanState,
                    jumpConsumedButtonPulse,
                    jumpVanillaMapInputHandoff);
            }

            Record(
                command,
                "Ui.MapCustomMarkers." + BuildMapCustomMarkerActionLabel(action),
                "UI",
                outcome,
                message,
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":" + BoolRaw(!IsMapCustomMarkerUiOnlyAction(action)) + ",\"uiOnly\":" + BoolRaw(IsMapCustomMarkerUiOnlyAction(action)) + ",\"featureId\":\"" + EscapeJson(FeatureIds.MapCustomMarkers) + "\",\"action\":\"" + EscapeJson(metadataAction) + "\",\"markerId\":\"" + EscapeJson(markerId) + "\",\"pairId\":\"" + EscapeJson(pairId) + "\",\"requestedTileX\":" + IntRaw(requestedTileX) + ",\"requestedTileY\":" + IntRaw(requestedTileY) + ",\"tileX\":" + IntRaw(markerTileX) + ",\"tileY\":" + IntRaw(markerTileY) + ",\"writtenMapPosX\":" + jumpWrittenMapPosX.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"writtenMapPosY\":" + jumpWrittenMapPosY.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"scale\":" + jumpScale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"resultCode\":\"" + EscapeJson(resultCode) + "\",\"writeStatus\":\"" + EscapeJson(write == null ? string.Empty : write.Status) + "\",\"changed\":" + BoolRaw(write != null && write.Changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + ",\"releasedUiCapture\":" + BoolRaw(jumpReleasedUiCapture) + ",\"closedF5\":" + BoolRaw(jumpClosedF5) + ",\"clearedPanState\":" + BoolRaw(jumpClearedPanState) + ",\"consumedJumpButtonPulse\":" + BoolRaw(jumpConsumedButtonPulse) + ",\"vanillaMapInputHandoff\":" + BoolRaw(jumpVanillaMapInputHandoff) + ",\"jumpButtonPulseMessage\":\"" + EscapeJson(jumpButtonPulseMessage) + "\"}",
                "Button");
        }

        private static void HandleMapDeathHistoryCommand(LegacyUiCommand command, string action)
        {
            var before = BuildMapEnhancementUiStateJson();
            action = (action ?? string.Empty).Trim();
            var outcome = "Succeeded";
            var message = "Map death history command handled.";

            if (string.Equals(action, "toggle", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.ToggleMapDeathHistoryPopup();
                message = LegacyMainWindow.IsMapDeathHistoryPopupOpen()
                    ? "Map death history popup opened."
                    : "Map death history popup closed.";
            }
            else if (string.Equals(action, "close", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.CloseMapDeathHistoryPopup();
                message = "Map death history popup closed.";
            }
            else if (string.Equals(action, "prev", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.MoveMapDeathHistoryPage(-1);
                message = "Map death history moved to previous page.";
            }
            else if (string.Equals(action, "next", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.MoveMapDeathHistoryPage(1);
                message = "Map death history moved to next page.";
            }
            else
            {
                outcome = "Rejected";
                message = "Map death history command rejected because the action was invalid.";
            }

            Record(
                command,
                "Ui.MapDeathHistory." + (string.IsNullOrWhiteSpace(action) ? "Invalid" : action),
                "UI",
                outcome,
                message,
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapDeathHistory) + "\",\"action\":\"" + EscapeJson(action) + "\",\"popupOpen\":" + BoolRaw(LegacyMainWindow.IsMapDeathHistoryPopupOpen()) + ",\"pageIndex\":" + IntRaw(LegacyMainWindow.GetMapDeathHistoryPageIndex()) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapRevealedAreaRatioCommand(LegacyUiCommand command, string action)
        {
            var before = BuildMapEnhancementUiStateJson();
            action = (action ?? string.Empty).Trim();
            var outcome = "Succeeded";
            var message = "Map revealed area ratio command handled.";

            if (string.Equals(action, "toggle", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "value", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.ToggleMapRevealedAreaDetailsPopup();
                message = LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen()
                    ? "Map revealed area details popup opened."
                    : "Map revealed area details popup closed.";
            }
            else if (string.Equals(action, "close", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.CloseMapRevealedAreaDetailsPopup();
                message = "Map revealed area details popup closed.";
            }
            else if (action.StartsWith("mode:", StringComparison.OrdinalIgnoreCase))
            {
                var mode = NormalizeMapRevealedAreaCommandMode(action.Substring("mode:".Length));
                PlayerWorldExplorationService.SetMode(mode);
                message = "Map revealed area scan mode switched to " + mode + ".";
            }
            else if (string.Equals(action, "pause", StringComparison.OrdinalIgnoreCase))
            {
                PlayerWorldExplorationService.PauseScanning();
                message = "Map revealed area scan paused.";
            }
            else if (string.Equals(action, "start", StringComparison.OrdinalIgnoreCase))
            {
                PlayerWorldExplorationService.StartScanning();
                message = "Map revealed area scan started.";
            }
            else
            {
                outcome = "Rejected";
                message = "Map revealed area command rejected because the action was invalid.";
            }

            var control = PlayerWorldExplorationService.GetControlSnapshot();
            var modeAfter = control == null ? string.Empty : control.ScanMode;
            var paused = control != null &&
                         string.Equals(control.ControlState, PlayerWorldExplorationControlStates.PausedByUser, StringComparison.Ordinal);
            Record(
                command,
                "Ui.MapRevealedAreaRatio." + (string.IsNullOrWhiteSpace(action) ? "Invalid" : action),
                "UI",
                outcome,
                message,
                before,
                BuildMapEnhancementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapRevealedAreaRatio) + "\",\"action\":\"" + EscapeJson(action) + "\",\"mode\":\"" + EscapeJson(modeAfter) + "\",\"paused\":" + BoolRaw(paused) + ",\"popupOpen\":" + BoolRaw(LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen()) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMapQuickAnnouncementKeySlot(LegacyUiCommand command, string slot)
        {
            var before = BuildMapQuickAnnouncementUiStateJson();
            var slotId = MapQuickAnnouncementSettings.NormalizeHotkeySlotId(slot);
            if (slotId.Length <= 0)
            {
                Record(
                    command,
                    "Ui.MapQuickAnnouncement.HotkeySlot",
                    "UI",
                    "Rejected",
                    "Map quick announcement hotkey capture rejected because the slot was invalid.",
                    before,
                    BuildMapQuickAnnouncementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"runtimeTriggerImplemented\":true,\"action\":\"capture-start\",\"resultCode\":\"invalidSlot\",\"slot\":\"" + EscapeJson(slot) + "\",\"doubleClick\":" + BoolRaw(command.IsDoubleClick) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!command.IsDoubleClick)
            {
                Record(
                    command,
                    "Ui.MapQuickAnnouncement.HotkeySlot",
                    "UI",
                    "NotApplicable",
                    "Map quick announcement hotkey capture starts only on double click.",
                    before,
                    BuildMapQuickAnnouncementUiStateJson(),
                    "{\"submitted\":false,\"implemented\":true,\"runtimeTriggerImplemented\":true,\"action\":\"capture-start\",\"resultCode\":\"needsDoubleClick\",\"slot\":\"" + EscapeJson(slotId) + "\",\"doubleClick\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyMainWindow.StartMapQuickAnnouncementHotkeyCapture(slotId);
            Record(
                command,
                "Ui.MapQuickAnnouncement.HotkeySlot",
                "UI",
                "Succeeded",
                "Map quick announcement hotkey capture started.",
                before,
                BuildMapQuickAnnouncementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"runtimeTriggerImplemented\":true,\"action\":\"capture-start\",\"resultCode\":\"started\",\"slot\":\"" + EscapeJson(slotId) + "\",\"doubleClick\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static string BuildMapQuickAnnouncementUiStateJson()
        {
            return BuildMapEnhancementUiStateJson();
        }

        private static string BuildMapEnhancementUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            var exploration = PlayerWorldExplorationService.GetControlSnapshot();
            return "{" +
                   "\"mapPersistentDeathMarkersEnabled\":" + BoolRaw(settings.MapPersistentDeathMarkersEnabled) + "," +
                   "\"mapDeathHistoryPopupOpen\":" + BoolRaw(LegacyMainWindow.IsMapDeathHistoryPopupOpen()) + "," +
                   "\"mapDeathHistoryPageIndex\":" + IntRaw(LegacyMainWindow.GetMapDeathHistoryPageIndex()) + "," +
                   "\"mapRevealedAreaDetailsPopupOpen\":" + BoolRaw(LegacyMainWindow.IsMapRevealedAreaDetailsPopupOpen()) + "," +
                   "\"mapRevealedAreaScanMode\":\"" + EscapeJson(exploration == null ? string.Empty : exploration.ScanMode) + "\"," +
                   "\"mapRevealedAreaControlState\":\"" + EscapeJson(exploration == null ? string.Empty : exploration.ControlState) + "\"," +
                   "\"mapCustomMarkersEnabled\":" + BoolRaw(settings.MapCustomMarkersEnabled) + "," +
                   "\"mapCustomMarkersSignature\":" + IntRaw(PlayerWorldMapMarkerCache.LastStateSignature) + "," +
                   "\"mapCustomMarkerPageIndex\":" + IntRaw(LegacyMainWindow.GetMapCustomMarkerPageIndex()) + "," +
                   "\"mapCustomMarkerNameInputActive\":" + BoolRaw(LegacyTextInput.IsAnyFocused) + "," +
                   "\"mapQuickAnnouncementEnabled\":" + BoolRaw(settings.MapQuickAnnouncementEnabled) + "," +
                   "\"mapQuickAnnouncementHotkeySlot1\":\"" + EscapeJson(hotkey.Slot1) + "\"," +
                   "\"mapQuickAnnouncementHotkeySlot2\":\"" + EscapeJson(hotkey.Slot2) + "\"," +
                   "\"mapQuickAnnouncementTriggerKey\":\"" + EscapeJson(hotkey.TriggerKey) + "\"," +
                   "\"mapQuickAnnouncementColorHex\":\"" + EscapeJson(MapQuickAnnouncementSettings.NormalizeColorHex(settings.MapQuickAnnouncementColorHex)) + "\"," +
                   "\"mapQuickAnnouncementCooldownMilliseconds\":" + IntRaw(MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(settings.MapQuickAnnouncementCooldownMilliseconds, MapQuickAnnouncementSettings.DefaultCooldownMilliseconds)) + "," +
                   "\"mapQuickAnnouncementAirCooldownMilliseconds\":" + IntRaw(MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(settings.MapQuickAnnouncementAirCooldownMilliseconds, MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds)) +
                   "}";
        }

        private static string NormalizeMapRevealedAreaCommandMode(string mode)
        {
            if (string.Equals(mode, "fast", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, PlayerWorldExplorationScanModes.Fast, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerWorldExplorationScanModes.Fast;
            }

            return PlayerWorldExplorationScanModes.Performance;
        }

        private static void SplitMapCustomMarkerPayload(string payload, out string action, out string markerId)
        {
            payload = payload ?? string.Empty;
            var split = payload.IndexOf(':');
            if (split < 0)
            {
                action = payload;
                markerId = string.Empty;
                return;
            }

            action = payload.Substring(0, split);
            markerId = payload.Substring(split + 1);
        }

        private static PlayerWorldMapMarkerRecord FindMapMarker(PlayerWorldMapMarkerReadResult read, string markerId)
        {
            if (read == null || read.Markers == null || string.IsNullOrWhiteSpace(markerId))
            {
                return null;
            }

            for (var index = 0; index < read.Markers.Count; index++)
            {
                var marker = read.Markers[index];
                if (marker != null &&
                    string.Equals(marker.MarkerId, markerId, StringComparison.Ordinal))
                {
                    return marker;
                }
            }

            return null;
        }

        private static bool TrySaveFocusedMapMarkerName(string pairId, string markerId)
        {
            var inputId = LegacyMainWindow.BuildMapMarkerNameInputId(markerId);
            if (!LegacyTextInput.IsFocused(inputId))
            {
                return false;
            }

            var write = PlayerWorldMapMarkerStore.RenameMarkerForPair(pairId, markerId, LegacyTextInput.GetDraft(inputId));
            if (write.Succeeded)
            {
                LegacyTextInput.ClearFocus();
            }

            return write.Succeeded;
        }

        private static bool IsMapCustomMarkerUiOnlyAction(string action)
        {
            return string.Equals(action, "navigate", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, "teleport", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(action, "autopilot", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildMapCustomMarkerActionLabel(string action)
        {
            return string.IsNullOrWhiteSpace(action) ? "Invalid" : action.Trim();
        }

        internal static bool IsMapCustomMarkerUiOnlyActionForTesting(string action)
        {
            return IsMapCustomMarkerUiOnlyAction(action);
        }
    }
}
