using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    internal static class UserNotesPinnedOverlayInputDiagnostics
    {
        private const int IdleTraceCoalesceMs = 250;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, DateTime> LastTraceUtcBySignature =
            new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private static bool _recordActionEvents = true;

        public static void Record(
            string source,
            bool handleState,
            bool handleScroll,
            DiagnosticMouseState raw,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta,
            bool legacyWindowOwnsMouse,
            string resultCode)
        {
            if (!_recordActionEvents)
            {
                return;
            }

            hit = hit ?? new UserNotesPinnedOverlayHitDiagnostics();
            mouse = mouse ?? new LegacyMouseSnapshot();
            suppression = suppression ?? new UserNotesPinnedOverlaySuppressionDiagnostics();
            interaction = interaction ?? new UserNotesPinnedOverlayInteraction();

            if (!ShouldRecord(source, mouse, hit, interaction, suppression, rawScrollDelta, legacyWindowOwnsMouse, resultCode))
            {
                return;
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.Notes.InputTrace",
                "UI",
                string.Empty,
                "Observed",
                string.IsNullOrWhiteSpace(resultCode) ? "observed" : resultCode,
                "User notes pinned overlay input trace.",
                0,
                BuildRawJson(raw),
                BuildHitJson(hit),
                BuildVerificationJson(source, handleState, handleScroll, mouse, hit, interaction, suppression, rawScrollDelta, legacyWindowOwnsMouse),
                "UI",
                "UserNotesPinnedOverlay",
                hit.ControlId ?? string.Empty,
                string.Empty);
        }

        internal static void SetRecordActionEventsForTesting(bool enabled)
        {
            _recordActionEvents = enabled;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                LastTraceUtcBySignature.Clear();
            }
        }

        internal static string BuildVerificationJsonForTesting(
            string source,
            bool handleState,
            bool handleScroll,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta,
            bool legacyWindowOwnsMouse)
        {
            return BuildVerificationJson(source, handleState, handleScroll, mouse, hit, interaction, suppression, rawScrollDelta, legacyWindowOwnsMouse);
        }

        internal static bool ShouldRecordForTesting(
            string source,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta,
            bool legacyWindowOwnsMouse,
            string resultCode)
        {
            return ShouldRecord(source, mouse, hit, interaction, suppression, rawScrollDelta, legacyWindowOwnsMouse, resultCode);
        }

        private static bool ShouldRecord(
            string source,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta,
            bool legacyWindowOwnsMouse,
            string resultCode)
        {
            var active =
                hit.MouseInside ||
                interaction.MouseInside ||
                interaction.CapturedMouse ||
                interaction.ScrollConsumed ||
                interaction.DragStarted ||
                interaction.Dragging ||
                interaction.DragSaved ||
                interaction.OpacityChanged ||
                interaction.Unpinned ||
                suppression.MouseCaptureRequested ||
                suppression.ButtonConsumeRequested ||
                suppression.ScrollConsumeRequested;
            if (!active)
            {
                return false;
            }

            var signature =
                (source ?? string.Empty) + "|" +
                (hit.ControlId ?? string.Empty) + "|" +
                BoolRaw(mouse.LeftDown) + "|" +
                BoolRaw(mouse.LeftPressed) + "|" +
                BoolRaw(mouse.LeftReleased) + "|" +
                rawScrollDelta.ToString(CultureInfo.InvariantCulture) + "|" +
                BoolRaw(legacyWindowOwnsMouse) + "|" +
                BoolRaw(suppression.MouseCaptureRequested) + "|" +
                BoolRaw(suppression.ButtonConsumeRequested) + "|" +
                (resultCode ?? string.Empty);
            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                DateTime lastTraceUtc;
                if (!LastTraceUtcBySignature.TryGetValue(signature, out lastTraceUtc) ||
                    now - lastTraceUtc >= TimeSpan.FromMilliseconds(IdleTraceCoalesceMs))
                {
                    LastTraceUtcBySignature[signature] = now;
                    return true;
                }
            }

            return false;
        }

        private static string BuildRawJson(DiagnosticMouseState raw)
        {
            if (raw == null)
            {
                return "{}";
            }

            return "{" +
                   "\"gameInputAvailable\":" + BoolRaw(raw.GameInputAvailable) + "," +
                   "\"terrariaReadAvailable\":" + BoolRaw(raw.TerrariaReadAvailable) + "," +
                   "\"terrariaMouseX\":" + IntRaw(raw.TerrariaMouseX) + "," +
                   "\"terrariaMouseY\":" + IntRaw(raw.TerrariaMouseY) + "," +
                   "\"terrariaLeftDown\":" + BoolRaw(raw.TerrariaLeftDown) + "," +
                   "\"terrariaLeftReleaseAvailable\":" + BoolRaw(raw.TerrariaLeftReleaseAvailable) + "," +
                   "\"terrariaLeftRelease\":" + BoolRaw(raw.TerrariaLeftRelease) + "," +
                   "\"osReadAvailable\":" + BoolRaw(raw.OsReadAvailable) + "," +
                   "\"osClientMouseX\":" + IntRaw(raw.OsClientMouseX) + "," +
                   "\"osClientMouseY\":" + IntRaw(raw.OsClientMouseY) + "," +
                   "\"osLeftDown\":" + BoolRaw(raw.OsLeftDown) + "," +
                   "\"scrollDelta\":" + IntRaw(raw.ScrollDelta) + "," +
                   "\"uiScale\":" + raw.UiScale.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"uiScaleX\":" + raw.UiScaleX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"uiScaleY\":" + raw.UiScaleY.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"uiTranslateX\":" + raw.UiTranslateX.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"uiTranslateY\":" + raw.UiTranslateY.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   "\"readMode\":\"" + EscapeJson(raw.ReadMode) + "\"," +
                   "\"lastError\":\"" + EscapeJson(raw.LastError) + "\"" +
                   "}";
        }

        private static string BuildHitJson(UserNotesPinnedOverlayHitDiagnostics hit)
        {
            hit = hit ?? new UserNotesPinnedOverlayHitDiagnostics();
            return "{" +
                   "\"featureId\":\"information.user_notes\"," +
                   "\"noteId\":\"" + EscapeJson(hit.HitNoteId) + "\"," +
                   "\"mouseX\":" + IntRaw(hit.MouseX) + "," +
                   "\"mouseY\":" + IntRaw(hit.MouseY) + "," +
                   "\"overlayScreenWidth\":" + IntRaw(hit.OverlayScreenWidth) + "," +
                   "\"overlayScreenHeight\":" + IntRaw(hit.OverlayScreenHeight) + "," +
                   "\"coordinateMode\":\"" + EscapeJson(hit.CoordinateMode) + "\"," +
                   "\"mouseInside\":" + BoolRaw(hit.MouseInside) + "," +
                   "\"controlId\":\"" + EscapeJson(hit.ControlId) + "\"," +
                   "\"bodyHit\":" + BoolRaw(hit.BodyHit) + "," +
                   "\"toolbarHit\":" + BoolRaw(hit.ToolbarHit) + "," +
                   "\"dragHandleHit\":" + BoolRaw(hit.DragHandleHit) + "," +
                   "\"decreaseOpacityHit\":" + BoolRaw(hit.DecreaseOpacityHit) + "," +
                   "\"increaseOpacityHit\":" + BoolRaw(hit.IncreaseOpacityHit) + "," +
                   "\"closeHit\":" + BoolRaw(hit.CloseHit) + "," +
                   "\"itemRect\":" + RectJson(hit.ItemRect) + "," +
                   "\"toolbarRect\":" + RectJson(hit.ToolbarRect) + "," +
                   "\"controlRect\":" + RectJson(hit.ControlRect) +
                   "}";
        }

        private static string BuildVerificationJson(
            string source,
            bool handleState,
            bool handleScroll,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta,
            bool legacyWindowOwnsMouse)
        {
            mouse = mouse ?? new LegacyMouseSnapshot();
            hit = hit ?? new UserNotesPinnedOverlayHitDiagnostics();
            interaction = interaction ?? new UserNotesPinnedOverlayInteraction();
            suppression = suppression ?? new UserNotesPinnedOverlaySuppressionDiagnostics();
            return "{" +
                   "\"source\":\"" + EscapeJson(source) + "\"," +
                   "\"handleState\":" + BoolRaw(handleState) + "," +
                   "\"handleScroll\":" + BoolRaw(handleScroll) + "," +
                   "\"readAvailable\":" + BoolRaw(mouse.ReadAvailable) + "," +
                   "\"readMode\":\"" + EscapeJson(mouse.ReadMode) + "\"," +
                   "\"mouseX\":" + IntRaw(mouse.X) + "," +
                   "\"mouseY\":" + IntRaw(mouse.Y) + "," +
                   "\"leftDown\":" + BoolRaw(mouse.LeftDown) + "," +
                   "\"leftPressed\":" + BoolRaw(mouse.LeftPressed) + "," +
                   "\"leftReleased\":" + BoolRaw(mouse.LeftReleased) + "," +
                   "\"windowHit\":" + BoolRaw(mouse.WindowHit) + "," +
                   "\"rawScrollDelta\":" + IntRaw(rawScrollDelta) + "," +
                   "\"legacyWindowOwnsMouse\":" + BoolRaw(legacyWindowOwnsMouse) + "," +
                   "\"controlId\":\"" + EscapeJson(hit.ControlId) + "\"," +
                   "\"hitNoteId\":\"" + EscapeJson(hit.HitNoteId) + "\"," +
                   "\"capturedMouse\":" + BoolRaw(interaction.CapturedMouse) + "," +
                   "\"dragStarted\":" + BoolRaw(interaction.DragStarted) + "," +
                   "\"dragging\":" + BoolRaw(interaction.Dragging) + "," +
                   "\"dragSaved\":" + BoolRaw(interaction.DragSaved) + "," +
                   "\"opacityChanged\":" + BoolRaw(interaction.OpacityChanged) + "," +
                   "\"unpinned\":" + BoolRaw(interaction.Unpinned) + "," +
                   "\"persistResultCode\":\"" + EscapeJson(interaction.PersistResult == null ? string.Empty : interaction.PersistResult.ResultCode) + "\"," +
                   "\"mouseCaptureRequested\":" + BoolRaw(suppression.MouseCaptureRequested) + "," +
                   "\"mouseCaptureSucceeded\":" + BoolRaw(suppression.MouseCaptureSucceeded) + "," +
                   "\"buttonConsumeRequested\":" + BoolRaw(suppression.ButtonConsumeRequested) + "," +
                   "\"buttonConsumeSucceeded\":" + BoolRaw(suppression.ButtonConsumeSucceeded) + "," +
                   "\"buttonConsumeMessage\":\"" + EscapeJson(suppression.ButtonConsumeMessage) + "\"," +
                   "\"scrollConsumeRequested\":" + BoolRaw(suppression.ScrollConsumeRequested) + "," +
                   "\"scrollConsumeSucceeded\":" + BoolRaw(suppression.ScrollConsumeSucceeded) +
                   "}";
        }

        private static string RectJson(LegacyUiRect rect)
        {
            return "{" +
                   "\"x\":" + IntRaw(rect.X) + "," +
                   "\"y\":" + IntRaw(rect.Y) + "," +
                   "\"width\":" + IntRaw(rect.Width) + "," +
                   "\"height\":" + IntRaw(rect.Height) +
                   "}";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }

    internal sealed class UserNotesPinnedOverlaySuppressionDiagnostics
    {
        public bool MouseCaptureRequested { get; set; }
        public bool MouseCaptureSucceeded { get; set; }
        public bool ButtonConsumeRequested { get; set; }
        public bool ButtonConsumeSucceeded { get; set; }
        public string ButtonConsumeMessage { get; set; }
        public bool ScrollConsumeRequested { get; set; }
        public bool ScrollConsumeSucceeded { get; set; }

        public UserNotesPinnedOverlaySuppressionDiagnostics()
        {
            ButtonConsumeMessage = string.Empty;
        }
    }
}
