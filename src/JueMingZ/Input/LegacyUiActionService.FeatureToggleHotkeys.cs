using System;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleFeatureToggleHotkeyOpen(LegacyUiCommand command, string targetId)
        {
            var before = BuildFeatureToggleHotkeyUiStateJson();
            string normalizedTargetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                Record(
                    command,
                    "Ui.FeatureToggleHotkey.Open",
                    "UI",
                    "Rejected",
                    "Feature toggle hotkey modal rejected because the target was invalid.",
                    before,
                    BuildFeatureToggleHotkeyUiStateJson(),
                    "{\"submitted\":false,\"action\":\"open\",\"resultCode\":\"invalidTarget\",\"targetId\":\"" + EscapeJson(targetId) + "\",\"doubleClick\":" + BoolRaw(command.IsDoubleClick) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (!command.IsDoubleClick)
            {
                Record(
                    command,
                    "Ui.FeatureToggleHotkey.Open",
                    "UI",
                    "NotApplicable",
                    "Feature toggle hotkey modal opens only on double click.",
                    before,
                    BuildFeatureToggleHotkeyUiStateJson(),
                    "{\"submitted\":false,\"action\":\"open\",\"resultCode\":\"needsDoubleClick\",\"targetId\":\"" + EscapeJson(normalizedTargetId) + "\",\"doubleClick\":false,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            LegacyMainWindow.OpenFeatureToggleHotkeyModal(normalizedTargetId, command.Rect);
            Record(
                command,
                "Ui.FeatureToggleHotkey.Open",
                "UI",
                "Succeeded",
                "Feature toggle hotkey modal opened.",
                before,
                BuildFeatureToggleHotkeyUiStateJson(),
                "{\"submitted\":false,\"action\":\"open\",\"resultCode\":\"opened\",\"targetId\":\"" + EscapeJson(normalizedTargetId) + "\",\"doubleClick\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFeatureToggleHotkeyCaptureStart(LegacyUiCommand command)
        {
            var before = BuildFeatureToggleHotkeyUiStateJson();
            LegacyMainWindow.StartFeatureToggleHotkeyCapture();
            Record(
                command,
                "Ui.FeatureToggleHotkey.Capture",
                "UI",
                "Succeeded",
                "Feature toggle hotkey capture started.",
                before,
                BuildFeatureToggleHotkeyUiStateJson(),
                "{\"submitted\":false,\"action\":\"capture-start\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFeatureToggleHotkeyClear(LegacyUiCommand command)
        {
            var before = BuildFeatureToggleHotkeyUiStateJson();
            var changed = LegacyMainWindow.ClearFeatureToggleHotkeyBinding();
            Record(
                command,
                "Ui.FeatureToggleHotkey.Clear",
                "UI",
                changed ? "Succeeded" : "NotApplicable",
                changed ? "Feature toggle hotkey binding cleared." : "Feature toggle hotkey binding was already empty.",
                before,
                BuildFeatureToggleHotkeyUiStateJson(),
                "{\"submitted\":false,\"action\":\"clear\",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleFeatureToggleHotkeyClose(LegacyUiCommand command)
        {
            var before = BuildFeatureToggleHotkeyUiStateJson();
            LegacyMainWindow.CloseFeatureToggleHotkeyModal();
            Record(
                command,
                "Ui.FeatureToggleHotkey.Close",
                "UI",
                "Succeeded",
                "Feature toggle hotkey modal closed.",
                before,
                BuildFeatureToggleHotkeyUiStateJson(),
                "{\"submitted\":false,\"action\":\"close\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static string BuildFeatureToggleHotkeyUiStateJson()
        {
            return "{\"selectedPageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                   "\"modalTargetId\":\"" + EscapeJson(LegacyMainWindow.GetFeatureToggleHotkeyModalTargetId()) + "\"," +
                   "\"captureActive\":" + BoolRaw(LegacyMainWindow.IsFeatureToggleHotkeyCaptureActive()) + "}";
        }
    }
}
