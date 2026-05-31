using System;
using JueMingZ.UI;

namespace JueMingZ.Diagnostics
{
    public static class DiagnosticInteractionDiagnostics
    {
        private static readonly object SyncRoot = new object();

        public static string LastSourceKind { get; private set; } = string.Empty;
        public static string LastButtonId { get; private set; } = string.Empty;
        public static string LastButtonLabel { get; private set; } = string.Empty;
        public static DateTime? LastButtonClickUtc { get; private set; }
        public static string LastButtonResultCode { get; private set; } = string.Empty;
        public static string LastButtonMessage { get; private set; } = string.Empty;
        public static string LastButtonHitTestMode { get; private set; } = string.Empty;
        public static string LastButtonClickSource { get; private set; } = string.Empty;
        public static int LastMouseX { get; private set; } = -1;
        public static int LastMouseY { get; private set; } = -1;
        public static int TerrariaMouseX { get; private set; } = -1;
        public static int TerrariaMouseY { get; private set; } = -1;
        public static bool TerrariaLeftDown { get; private set; }
        public static bool TerrariaLeftReleaseAvailable { get; private set; }
        public static bool TerrariaLeftRelease { get; private set; }
        public static int OsClientMouseX { get; private set; } = -1;
        public static int OsClientMouseY { get; private set; } = -1;
        public static bool OsLeftDown { get; private set; }
        public static double UiScale { get; private set; } = 1d;
        public static bool UiScaleAvailable { get; private set; }
        public static bool UiScaleMatrixAvailable { get; private set; }
        public static string MouseReadMode { get; private set; } = string.Empty;
        public static string MouseReadLastError { get; private set; } = string.Empty;
        public static string HitTestMode { get; private set; } = string.Empty;
        public static int HitTestX { get; private set; } = -1;
        public static int HitTestY { get; private set; } = -1;
        public static bool HitTestConflict { get; private set; }
        public static string HitTestCandidateSummary { get; private set; } = string.Empty;
        public static string ClickSource { get; private set; } = string.Empty;
        public static string HoveredButtonId { get; private set; } = string.Empty;
        public static string HoveredButtonLabel { get; private set; } = string.Empty;
        public static string HoveredButtonHint { get; private set; } = string.Empty;
        public static bool HoveredButtonEnabled { get; private set; }
        public static int HoveredButtonVisualX { get; private set; } = -1;
        public static int HoveredButtonVisualY { get; private set; } = -1;
        public static int HoveredButtonVisualWidth { get; private set; }
        public static int HoveredButtonVisualHeight { get; private set; }
        public static int HoveredButtonHitX { get; private set; } = -1;
        public static int HoveredButtonHitY { get; private set; } = -1;
        public static int HoveredButtonHitWidth { get; private set; }
        public static int HoveredButtonHitHeight { get; private set; }
        public static bool OverlayHoverAtDraw { get; private set; }
        public static bool UiClickSuppressionAttempted { get; private set; }
        public static string UiClickSuppressionMode { get; private set; } = string.Empty;
        public static bool UiClickSuppressionSucceeded { get; private set; }
        public static bool UiMouseCaptureAvailableAtClick { get; private set; }
        public static bool ButtonHoverAtUpdatePrefix { get; private set; }
        public static bool OverlayHoverAtUpdatePrefix { get; private set; }
        public static string LastUiWindow { get; private set; } = string.Empty;
        public static string LastUiElementId { get; private set; } = string.Empty;
        public static bool LastMouseCaptured { get; private set; }

        public static void RecordButton(string buttonId, string buttonLabel)
        {
            lock (SyncRoot)
            {
                LastSourceKind = "Button";
                LastButtonId = buttonId ?? string.Empty;
                LastButtonLabel = buttonLabel ?? string.Empty;
                LastButtonClickUtc = DateTime.UtcNow;
            }
        }

        public static void RecordButton(string buttonId, string buttonLabel, string hitTestMode, string clickSource, int hitTestX, int hitTestY)
        {
            RecordButton(buttonId, buttonLabel, hitTestMode, clickSource, hitTestX, hitTestY, false, string.Empty, null);
        }

        public static void RecordButton(
            string buttonId,
            string buttonLabel,
            string hitTestMode,
            string clickSource,
            int hitTestX,
            int hitTestY,
            bool hitTestConflict,
            string candidateSummary,
            DiagnosticTestButton button)
        {
            lock (SyncRoot)
            {
                LastSourceKind = "Button";
                LastButtonId = buttonId ?? string.Empty;
                LastButtonLabel = buttonLabel ?? string.Empty;
                LastButtonClickUtc = DateTime.UtcNow;
                LastButtonHitTestMode = hitTestMode ?? string.Empty;
                LastButtonClickSource = clickSource ?? string.Empty;
                HitTestMode = hitTestMode ?? string.Empty;
                HitTestX = hitTestX;
                HitTestY = hitTestY;
                HitTestConflict = hitTestConflict;
                HitTestCandidateSummary = candidateSummary ?? string.Empty;
                LastUiWindow = button == null ? string.Empty : "OperationWindow";
                LastUiElementId = button == null ? string.Empty : button.Id ?? string.Empty;
                LastMouseCaptured = UiClickSuppressionSucceeded;
                SetHoveredButtonRect(button);
            }
        }

        public static void RecordButtonOutcome(string buttonId, string buttonLabel, string resultCode, string message)
        {
            lock (SyncRoot)
            {
                LastSourceKind = "Button";
                LastButtonId = buttonId ?? string.Empty;
                LastButtonLabel = buttonLabel ?? string.Empty;
                LastButtonClickUtc = DateTime.UtcNow;
                LastButtonResultCode = resultCode ?? string.Empty;
                LastButtonMessage = message ?? string.Empty;
            }
        }

        public static void RecordButtonOutcome(DiagnosticButtonCommand command, string resultCode, string message)
        {
            if (command == null)
            {
                RecordButtonOutcome(string.Empty, string.Empty, resultCode, message);
                return;
            }

            lock (SyncRoot)
            {
                LastSourceKind = "Button";
                LastButtonId = command.ButtonId ?? string.Empty;
                LastButtonLabel = command.ButtonLabel ?? string.Empty;
                LastButtonClickUtc = DateTime.UtcNow;
                LastButtonResultCode = resultCode ?? string.Empty;
                LastButtonMessage = message ?? string.Empty;
                LastButtonHitTestMode = command.HitTestMode ?? string.Empty;
                LastButtonClickSource = command.ClickSource ?? string.Empty;
                HitTestMode = command.HitTestMode ?? string.Empty;
                HitTestX = command.HitTestX;
                HitTestY = command.HitTestY;
                HitTestConflict = command.HitTestConflict;
                HitTestCandidateSummary = command.CandidateHits ?? string.Empty;
                HoveredButtonVisualX = command.VisualRectX;
                HoveredButtonVisualY = command.VisualRectY;
                HoveredButtonVisualWidth = command.VisualRectWidth;
                HoveredButtonVisualHeight = command.VisualRectHeight;
                HoveredButtonHitX = command.HitRectX;
                HoveredButtonHitY = command.HitRectY;
                HoveredButtonHitWidth = command.HitRectWidth;
                HoveredButtonHitHeight = command.HitRectHeight;
                LastUiWindow = command.UiWindow ?? string.Empty;
                LastUiElementId = command.UiElementId ?? string.Empty;
                LastMouseCaptured = command.MouseCaptured;
            }
        }

        public static void RecordHotkey()
        {
            lock (SyncRoot)
            {
                LastSourceKind = "Hotkey";
            }
        }

        public static void RecordMouse(int mouseX, int mouseY, string buttonId, string buttonLabel, string buttonHint, bool buttonEnabled)
        {
            lock (SyncRoot)
            {
                LastMouseX = mouseX;
                LastMouseY = mouseY;
                HoveredButtonId = buttonId ?? string.Empty;
                HoveredButtonLabel = buttonLabel ?? string.Empty;
                HoveredButtonHint = buttonHint ?? string.Empty;
                HoveredButtonEnabled = buttonEnabled;
            }
        }

        public static void RecordMouse(DiagnosticMouseState mouse, DiagnosticButtonHitTestResult hit, string clickSource)
        {
            RecordMouse(mouse, hit, clickSource, false);
        }

        public static void RecordMouse(DiagnosticMouseState mouse, DiagnosticButtonHitTestResult hit, string clickSource, bool overlayHover)
        {
            lock (SyncRoot)
            {
                if (mouse != null)
                {
                    TerrariaMouseX = mouse.TerrariaMouseX;
                    TerrariaMouseY = mouse.TerrariaMouseY;
                    TerrariaLeftDown = mouse.TerrariaLeftDown;
                    TerrariaLeftReleaseAvailable = mouse.TerrariaLeftReleaseAvailable;
                    TerrariaLeftRelease = mouse.TerrariaLeftRelease;
                    OsClientMouseX = mouse.OsClientMouseX;
                    OsClientMouseY = mouse.OsClientMouseY;
                    OsLeftDown = mouse.OsLeftDown;
                    UiScale = mouse.UiScale;
                    UiScaleAvailable = mouse.UiScaleAvailable;
                    UiScaleMatrixAvailable = mouse.UiScaleMatrixAvailable;
                    MouseReadMode = mouse.ReadMode ?? string.Empty;
                    MouseReadLastError = mouse.LastError ?? string.Empty;
                }

                HitTestMode = hit == null ? "none" : hit.HitTestMode ?? "none";
                HitTestX = hit == null ? -1 : hit.HitTestX;
                HitTestY = hit == null ? -1 : hit.HitTestY;
                HitTestConflict = hit != null && hit.HitTestConflict;
                HitTestCandidateSummary = hit == null ? string.Empty : hit.CandidateSummary ?? string.Empty;
                ClickSource = clickSource ?? "none";
                LastMouseX = HitTestX >= 0 ? HitTestX : (mouse == null ? -1 : mouse.TerrariaMouseX);
                LastMouseY = HitTestY >= 0 ? HitTestY : (mouse == null ? -1 : mouse.TerrariaMouseY);
                OverlayHoverAtDraw = overlayHover;

                var button = hit == null ? null : hit.Button;
                HoveredButtonId = button == null ? string.Empty : button.Id ?? string.Empty;
                HoveredButtonLabel = button == null ? string.Empty : button.Label ?? string.Empty;
                HoveredButtonHint = button == null ? string.Empty : button.Hint ?? string.Empty;
                HoveredButtonEnabled = button != null && button.Enabled;
                LastUiWindow = button == null ? string.Empty : "OperationWindow";
                LastUiElementId = button == null ? string.Empty : button.Id ?? string.Empty;
                SetHoveredButtonRect(button);
            }
        }

        public static void RecordUiClickSuppression(bool attempted, string mode, bool succeeded, bool buttonHoverAtUpdatePrefix, bool overlayHoverAtUpdatePrefix)
        {
            lock (SyncRoot)
            {
                UiClickSuppressionAttempted = attempted;
                UiClickSuppressionMode = mode ?? string.Empty;
                UiClickSuppressionSucceeded = succeeded;
                UiMouseCaptureAvailableAtClick = succeeded;
                ButtonHoverAtUpdatePrefix = buttonHoverAtUpdatePrefix;
                OverlayHoverAtUpdatePrefix = overlayHoverAtUpdatePrefix;
            }
        }

        private static void SetHoveredButtonRect(DiagnosticTestButton button)
        {
            if (button == null)
            {
                HoveredButtonVisualX = -1;
                HoveredButtonVisualY = -1;
                HoveredButtonVisualWidth = 0;
                HoveredButtonVisualHeight = 0;
                HoveredButtonHitX = -1;
                HoveredButtonHitY = -1;
                HoveredButtonHitWidth = 0;
                HoveredButtonHitHeight = 0;
                return;
            }

            HoveredButtonVisualX = button.X;
            HoveredButtonVisualY = button.Y;
            HoveredButtonVisualWidth = button.Width;
            HoveredButtonVisualHeight = button.Height;
            HoveredButtonHitX = button.HitX;
            HoveredButtonHitY = button.HitY;
            HoveredButtonHitWidth = button.HitWidth;
            HoveredButtonHitHeight = button.HitHeight;
        }
    }
}
