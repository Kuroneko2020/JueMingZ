using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;

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

            var capture = HotkeyCaptureService.Update(MapQuickAnnouncementHotkeyCaptureSession, IsKeyDown);
            if (capture == null || !capture.HasResult)
            {
                return;
            }

            string message;
            bool changed;
            TryApplyUnifiedHotkeyCaptureResult(
                UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger,
                capture,
                out message,
                out changed);
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
