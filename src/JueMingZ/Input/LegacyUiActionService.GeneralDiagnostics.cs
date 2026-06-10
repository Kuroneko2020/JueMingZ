using System;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleAboutCopyFeedbackGroup(LegacyUiCommand command)
        {
            string detail;
            var copied = ClipboardCompat.TrySetText(LegacyMainWindow.AboutFeedbackGroupNumber, out detail);
            Record(
                command,
                "Ui.About.CopyFeedbackGroup",
                "UI",
                copied ? "Succeeded" : "Failed",
                copied ? "Feedback QQ group copied to clipboard." : "Feedback QQ group copy failed: " + detail,
                "{}",
                "{}",
                "{\"submitted\":false,\"copied\":" + BoolRaw(copied) + ",\"group\":\"" + EscapeJson(LegacyMainWindow.AboutFeedbackGroupNumber) + "\",\"detail\":\"" + EscapeJson(detail) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscDeveloperEasterEgg(LegacyUiCommand command, string payload)
        {
            // Developer-menu buttons only request vanilla diagnostics UI; they must
            // not become a back door for changing game state from the UI layer.
            var mode = string.IsNullOrWhiteSpace(payload) ? "open" : payload.Trim();
            var before = BuildUiOptionStateJson();

            if (string.Equals(mode, "debugOn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "debugOff", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "blocked", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "Diagnostics",
                    "NotApplicable",
                    "Developer menu startup switch has been removed; use the open button instead.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"removedSwitch\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            var pending = LegacyMainWindow.IsDeveloperEasterEggConfirmPending();
            if (!pending && string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(true);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "UI",
                    "Succeeded",
                    "Developer menu armed. Click again to run vanilla /hh.",
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"armed\",\"pendingConfirmation\":true,\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            if (pending && (string.Equals(mode, "confirm", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(mode, "open", StringComparison.OrdinalIgnoreCase)))
            {
                var opened = DebugCommandsCompat.TryOpenDebugCommandsHelp(out var detail);
                LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
                Record(
                    command,
                    "Ui.Toggle.MiscDeveloperEasterEgg",
                    "Diagnostics",
                    opened ? "Succeeded" : "Failed",
                    opened ? "Vanilla /hh debug command list requested." : "Vanilla /hh open failed: " + detail,
                    before,
                    BuildUiOptionStateJson(),
                    "{\"submitted\":false,\"step\":\"confirm\",\"opened\":" + BoolRaw(opened) + ",\"pendingConfirmation\":false,\"detail\":\"" + EscapeJson(detail) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                    "Button");
                return;
            }

            Record(
                command,
                "Ui.Toggle.MiscDeveloperEasterEgg",
                "UI",
                "Rejected",
                "Unknown developer menu payload.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"step\":\"unknown\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscDebugCommandsMode(LegacyUiCommand command, string payload)
        {
            var mode = string.IsNullOrWhiteSpace(payload) ? string.Empty : payload.Trim();
            var before = BuildUiOptionStateJson();
            LegacyMainWindow.SetDeveloperEasterEggConfirmPending(false);
            Record(
                command,
                "Ui.Toggle.MiscDebugCommandsSwitch",
                "Diagnostics",
                "NotApplicable",
                "Developer menu startup switch has been removed; no configuration was changed.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"enabled\":true,\"changed\":false,\"resultCode\":\"removedSwitch\",\"payload\":\"" + EscapeJson(mode) + "\",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscWorldGenerationDetails(LegacyUiCommand command, string payload)
        {
            var mode = string.IsNullOrWhiteSpace(payload) ? string.Empty : payload.Trim();
            var before = BuildUiOptionStateJson();
            if (string.Equals(mode, "hint", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "status", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "locked", StringComparison.OrdinalIgnoreCase))
            {
                LegacyMainWindow.SetWorldGenerationDetailsHintAlternate(true);
            }

            Record(
                command,
                "Ui.Toggle.MiscWorldGenerationDetails",
                "Diagnostics",
                "NotApplicable",
                "WorldGen Debug Viewer is always enabled; this row is informational only.",
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.DiagnosticsWorldGenDebugViewer) + "\",\"enabled\":true,\"changed\":false,\"uiOnly\":true,\"payload\":\"" + EscapeJson(mode) + "\",\"sessionConfiguredEnabled\":" + BoolRaw(WorldGenDebugCompat.WorldGenSessionConfiguredEnabled) + ",\"sharedDebugFieldEnabled\":" + BoolRaw(WorldGenDebugCompat.Enabled) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }
    }
}
