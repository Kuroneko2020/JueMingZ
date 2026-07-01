using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static readonly HotkeyCaptureSession QuickItemHotkeyCaptureSession = new HotkeyCaptureSession();
        private static readonly HotkeyCaptureSession AutoMiningHotkeyCaptureSession = new HotkeyCaptureSession();
        private static readonly HotkeyCaptureSession BlueprintEntryHotkeyCaptureSession = new HotkeyCaptureSession();
        private static readonly HotkeyCaptureSession MapQuickAnnouncementHotkeyCaptureSession = new HotkeyCaptureSession();
        private static readonly HotkeyCaptureSession FeatureToggleHotkeyCaptureSession = new HotkeyCaptureSession();

        private static string GetUnifiedHotkeyDisplay(string bindingId)
        {
            var normalized = GetUnifiedHotkeyNormalized(bindingId);
            if (normalized.Length <= 0)
            {
                return string.Empty;
            }

            var parse = HotkeyParser.Parse(normalized);
            return parse.Succeeded ? parse.Display : string.Empty;
        }

        private static string GetUnifiedHotkeyNormalized(string bindingId)
        {
            var settings = ConfigService.UnifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault();
            return settings.GetBinding(bindingId);
        }

        private static bool TryApplyUnifiedHotkeyCaptureResult(
            string bindingId,
            HotkeyCaptureResult capture,
            out string message,
            out bool changed)
        {
            changed = false;
            message = string.Empty;
            if (capture == null || !capture.HasResult)
            {
                return false;
            }

            if (capture.Kind == HotkeyCaptureResultKind.Cancelled)
            {
                message = UnifiedHotkeyReasonCatalog.BuildCaptureCancelledMessage();
                return true;
            }

            if (capture.Kind == HotkeyCaptureResultKind.Failed)
            {
                message = BuildUnifiedHotkeyFailureMessage(capture.ResultCode);
                return false;
            }

            var chordText = capture.Kind == HotkeyCaptureResultKind.Cleared ? string.Empty : capture.Normalized;
            UnifiedHotkeyBindingUpdateResult result;
            var succeeded = ConfigService.TrySaveUnifiedHotkeyBinding(bindingId, chordText, out result);
            changed = result != null && result.Changed;
            message = BuildUnifiedHotkeyUpdateMessage(result);
            return succeeded;
        }

        private static string[] GetUnifiedMapQuickAnnouncementDisplayParts()
        {
            var normalized = GetUnifiedHotkeyNormalized(UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger);
            var parse = HotkeyParser.Parse(normalized);
            if (!parse.Succeeded || parse.Chord == null)
            {
                return new[] { "空", "空", "空" };
            }

            return new[]
            {
                parse.Chord.Modifiers.Count > 0 ? HotkeyDisplayFormatter.FormatToken(parse.Chord.Modifiers[0]) : "空",
                parse.Chord.Modifiers.Count > 1 ? HotkeyDisplayFormatter.FormatToken(parse.Chord.Modifiers[1]) : "空",
                HotkeyDisplayFormatter.FormatToken(parse.Chord.PrimaryKey)
            };
        }

        private static string BuildUnifiedHotkeyUpdateMessage(UnifiedHotkeyBindingUpdateResult result)
        {
            return UnifiedHotkeyReasonCatalog.BuildUpdateMessage(result);
        }

        private static string BuildUnifiedHotkeyFailureMessage(string resultCode)
        {
            return UnifiedHotkeyReasonCatalog.BuildCaptureFailureMessage(resultCode);
        }

        internal static string GetUnifiedHotkeyDisplayForTesting(string bindingId)
        {
            return GetUnifiedHotkeyDisplay(bindingId);
        }

        internal static bool TryApplyUnifiedHotkeyBindingForTesting(
            UnifiedHotkeySettings settings,
            string bindingId,
            string chordText,
            out string resultCode,
            out string display)
        {
            settings = settings ?? UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult result;
            var succeeded = settings.TrySetBinding(bindingId, chordText, out result);
            resultCode = result == null ? string.Empty : result.ResultCode;
            display = result == null ? string.Empty : result.Display;
            return succeeded;
        }

        internal static string BuildUnifiedHotkeyFailureMessageForTesting(string resultCode)
        {
            return BuildUnifiedHotkeyFailureMessage(resultCode);
        }

        internal static string BuildUnifiedHotkeyUpdateMessageForTesting(UnifiedHotkeyBindingUpdateResult result)
        {
            return BuildUnifiedHotkeyUpdateMessage(result);
        }

        internal static string BuildUnifiedHotkeyCancelledMessageForTesting()
        {
            return UnifiedHotkeyReasonCatalog.BuildCaptureCancelledMessage();
        }
    }
}
