using JueMingZ.Automation.Information;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static void UpdateMapQuickAnnouncementHotkeyCapture(AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(_mapQuickAnnouncementHotkeyCaptureSlot) || !IsCurrentProcessForeground())
            {
                return;
            }

            settings = settings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
            string token;
            if (!MapQuickAnnouncementHotkeyTokens.TryCapturePressedToken(MapQuickAnnouncementCaptureWasDown, IsKeyDown, out token))
            {
                return;
            }

            if (string.Equals(token, "Escape", System.StringComparison.Ordinal))
            {
                StopMapQuickAnnouncementHotkeyCapture();
                return;
            }

            MapQuickAnnouncementHotkey hotkey;
            string resultCode;
            if (string.Equals(token, "Backspace", System.StringComparison.Ordinal))
            {
                if (MapQuickAnnouncementSettings.TryClearHotkeySlot(
                        settings.MapQuickAnnouncementHotkeySlot1,
                        settings.MapQuickAnnouncementHotkeySlot2,
                        settings.MapQuickAnnouncementTriggerKey,
                        _mapQuickAnnouncementHotkeyCaptureSlot,
                        out hotkey,
                        out resultCode))
                {
                    settings.MapQuickAnnouncementHotkeySlot1 = hotkey.Slot1;
                    settings.MapQuickAnnouncementHotkeySlot2 = hotkey.Slot2;
                    settings.MapQuickAnnouncementTriggerKey = hotkey.TriggerKey;
                    if (string.Equals(resultCode, "cleared", System.StringComparison.Ordinal))
                    {
                        ConfigService.SaveAll();
                    }
                }

                StopMapQuickAnnouncementHotkeyCapture();
                return;
            }

            if (MapQuickAnnouncementSettings.TryApplyCapturedHotkeyToken(
                    settings.MapQuickAnnouncementHotkeySlot1,
                    settings.MapQuickAnnouncementHotkeySlot2,
                    settings.MapQuickAnnouncementTriggerKey,
                    _mapQuickAnnouncementHotkeyCaptureSlot,
                    token,
                    out hotkey,
                    out resultCode))
            {
                settings.MapQuickAnnouncementHotkeySlot1 = hotkey.Slot1;
                settings.MapQuickAnnouncementHotkeySlot2 = hotkey.Slot2;
                settings.MapQuickAnnouncementTriggerKey = hotkey.TriggerKey;
                ConfigService.SaveAll();
            }

            StopMapQuickAnnouncementHotkeyCapture();
        }

        internal static bool TryApplyMapQuickAnnouncementCapturedTokenForTesting(
            AppSettings settings,
            string slot,
            string token,
            out string resultCode)
        {
            settings = settings ?? AppSettings.CreateDefault();
            MapQuickAnnouncementHotkey hotkey;
            if (!MapQuickAnnouncementSettings.TryApplyCapturedHotkeyToken(
                    settings.MapQuickAnnouncementHotkeySlot1,
                    settings.MapQuickAnnouncementHotkeySlot2,
                    settings.MapQuickAnnouncementTriggerKey,
                    slot,
                    token,
                    out hotkey,
                    out resultCode))
            {
                return false;
            }

            settings.MapQuickAnnouncementHotkeySlot1 = hotkey.Slot1;
            settings.MapQuickAnnouncementHotkeySlot2 = hotkey.Slot2;
            settings.MapQuickAnnouncementTriggerKey = hotkey.TriggerKey;
            return true;
        }

        internal static bool TryClearMapQuickAnnouncementHotkeySlotForTesting(
            AppSettings settings,
            string slot,
            out string resultCode)
        {
            settings = settings ?? AppSettings.CreateDefault();
            MapQuickAnnouncementHotkey hotkey;
            if (!MapQuickAnnouncementSettings.TryClearHotkeySlot(
                    settings.MapQuickAnnouncementHotkeySlot1,
                    settings.MapQuickAnnouncementHotkeySlot2,
                    settings.MapQuickAnnouncementTriggerKey,
                    slot,
                    out hotkey,
                    out resultCode))
            {
                return false;
            }

            settings.MapQuickAnnouncementHotkeySlot1 = hotkey.Slot1;
            settings.MapQuickAnnouncementHotkeySlot2 = hotkey.Slot2;
            settings.MapQuickAnnouncementTriggerKey = hotkey.TriggerKey;
            return true;
        }
    }
}
