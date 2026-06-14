using System;
using JueMingZ.Common;
using JueMingZ.Config;
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
    }
}
