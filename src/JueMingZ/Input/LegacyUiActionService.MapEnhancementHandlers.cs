using System;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleMapQuickAnnouncementMode(LegacyUiCommand command, string payload)
        {
            var before = BuildMapQuickAnnouncementUiStateJson();
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
                BuildMapQuickAnnouncementUiStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"runtimeTriggerImplemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.MapQuickAnnouncement) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
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
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var hotkey = MapQuickAnnouncementSettings.NormalizeHotkey(
                settings.MapQuickAnnouncementHotkeySlot1,
                settings.MapQuickAnnouncementHotkeySlot2,
                settings.MapQuickAnnouncementTriggerKey);
            return "{" +
                   "\"mapQuickAnnouncementEnabled\":" + BoolRaw(settings.MapQuickAnnouncementEnabled) + "," +
                   "\"mapQuickAnnouncementHotkeySlot1\":\"" + EscapeJson(hotkey.Slot1) + "\"," +
                   "\"mapQuickAnnouncementHotkeySlot2\":\"" + EscapeJson(hotkey.Slot2) + "\"," +
                   "\"mapQuickAnnouncementTriggerKey\":\"" + EscapeJson(hotkey.TriggerKey) + "\"," +
                   "\"mapQuickAnnouncementColorHex\":\"" + EscapeJson(MapQuickAnnouncementSettings.NormalizeColorHex(settings.MapQuickAnnouncementColorHex)) + "\"," +
                   "\"mapQuickAnnouncementCooldownMilliseconds\":" + IntRaw(MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(settings.MapQuickAnnouncementCooldownMilliseconds, MapQuickAnnouncementSettings.DefaultCooldownMilliseconds)) + "," +
                   "\"mapQuickAnnouncementAirCooldownMilliseconds\":" + IntRaw(MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(settings.MapQuickAnnouncementAirCooldownMilliseconds, MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds)) +
                   "}";
        }
    }
}
