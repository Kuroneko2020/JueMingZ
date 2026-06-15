using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainUiState
    {
        public static string BuildUiStateJson()
        {
            EnsureLoaded();
            lock (SyncRoot)
            {
                return "{" +
                       "\"windowX\":" + IntRaw(_x) + "," +
                       "\"windowY\":" + IntRaw(_y) + "," +
                       "\"windowWidth\":" + IntRaw(_width) + "," +
                       "\"windowHeight\":" + IntRaw(_height) + "," +
                       "\"selectedPage\":\"" + EscapeJson(_selectedPageId) + "\"," +
                       "\"candidateCount\":" + IntRaw(_lastScan == null ? 0 : _lastScan.Candidates.Count) + "," +
                       "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) +
                       "}";
            }
        }

        private static void RecordWindowToggle(bool visible)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.MainWindow.Toggle",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                visible ? "Legacy main UI opened." : "Legacy main UI closed.",
                0,
                "{}",
                BuildUiStateJson(),
                "{\"submitted\":false,\"mouseCaptured\":false}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static void RecordWindowMainMenuSuppressed(string source, string reason)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.MainWindow.MainMenuSuppressed",
                "UI",
                string.Empty,
                "BlockedByEnvironment",
                "BlockedByEnvironment",
                "Legacy main UI hidden or ignored because Terraria is showing a vanilla menu.",
                0,
                "{\"source\":\"" + EscapeJson(source) + "\",\"vanillaMenuReason\":\"" + EscapeJson(reason) + "\",\"inMainMenu\":" + (string.Equals(reason, "gameMenu", StringComparison.Ordinal) ? "true" : "false") + "}",
                BuildUiStateJson(),
                "{\"submitted\":false,\"mouseCaptured\":false}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static void RecordWindowFullscreenMapSuppressed(string source, bool releasedUiCapture)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.MainWindow.FullscreenMapSuppressed",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Legacy main UI closed because the fullscreen map is open.",
                0,
                "{\"source\":\"" + EscapeJson(source) + "\",\"mapFullscreen\":true}",
                BuildUiStateJson(),
                "{\"submitted\":false,\"releasedUiCapture\":" + (releasedUiCapture ? "true" : "false") + "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
