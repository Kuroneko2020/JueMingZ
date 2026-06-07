using System;
using System.Collections.Generic;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class DiagnosticUiInteractionBridge
    {
        private static readonly object SyncRoot = new object();
        private static readonly Queue<DiagnosticButtonCommand> PendingCommands = new Queue<DiagnosticButtonCommand>();
        private static bool _wasLeftDown;

        public static void UpdatePrefixGuard()
        {
            // Capture marks UI ownership to prevent click-through; it does not grant
            // permission to execute a gameplay action.
            if (!DiagnosticsOverlay.Visible)
            {
                return;
            }

            var buttonHover = !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.HoveredButtonId);
            var overlayHover = DiagnosticInteractionDiagnostics.OverlayHoverAtDraw;
            if (!buttonHover && !overlayHover)
            {
                return;
            }

            var captured = UiMouseCaptureService.CaptureForOperationWindow();
            DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                true,
                "UpdatePrefix+Draw",
                captured,
                buttonHover,
                overlayHover);
        }

        public static DiagnosticButtonHitTestResult UpdateFromDraw(IReadOnlyList<DiagnosticTestButton> buttons)
        {
            var mouse = DiagnosticMouseStateReader.Read();
            var windowInteraction = OperationWindowState.UpdateMouse(mouse);
            var hit = HitTest(buttons, mouse);
            var anyLeftDown = mouse.OsLeftDown || mouse.TerrariaLeftDown;
            var clickSource = GetClickSource(mouse);
            var overlayHover = IsOverlayHover(mouse, hit, windowInteraction);

            DiagnosticInteractionDiagnostics.RecordMouse(mouse, hit, clickSource, overlayHover);

            if (hit.HasButton || overlayHover)
            {
                var captured = UiMouseCaptureService.CaptureForOperationWindow();
                var hadPrefix = DiagnosticInteractionDiagnostics.UiClickSuppressionAttempted &&
                                DiagnosticInteractionDiagnostics.UiClickSuppressionMode.IndexOf("UpdatePrefix", StringComparison.OrdinalIgnoreCase) >= 0;
                DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                    true,
                    hadPrefix ? "UpdatePrefix+Draw" : "DrawOnly",
                    captured || (hadPrefix && DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded),
                    hit.HasButton,
                    overlayHover);
            }
            else
            {
                DiagnosticInteractionDiagnostics.RecordUiClickSuppression(false, string.Empty, false, false, false);
            }

            var clicked = hit.HasButton && anyLeftDown && !_wasLeftDown && !windowInteraction.StartedDrag && !windowInteraction.StartedResize && !windowInteraction.Dragging && !windowInteraction.Resizing;
            _wasLeftDown = anyLeftDown;
            if (clicked)
            {
                EnqueueCommand(hit, mouse, clickSource, captured: DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded);
            }

            return hit;
        }

        public static bool TryDrainButtonCommand(out DiagnosticButtonCommand command)
        {
            lock (SyncRoot)
            {
                if (PendingCommands.Count <= 0)
                {
                    command = null;
                    return false;
                }

                command = PendingCommands.Dequeue();
                return true;
            }
        }

        private static void EnqueueCommand(DiagnosticButtonHitTestResult hit, DiagnosticMouseState mouse, string clickSource, bool captured)
        {
            // Draw-layer clicks become bounded diagnostic commands; execution is
            // drained later by DiagnosticButtonActionService.
            var button = hit == null ? null : hit.Button;
            if (button == null)
            {
                return;
            }

            var command = new DiagnosticButtonCommand
            {
                ButtonId = button.Id,
                ButtonLabel = button.Label,
                HitTestMode = hit.HitTestMode,
                HitTestX = hit.HitTestX,
                HitTestY = hit.HitTestY,
                HitTestConflict = hit.HitTestConflict,
                CandidateHits = hit.CandidateSummary,
                VisualRectX = button.X,
                VisualRectY = button.Y,
                VisualRectWidth = button.Width,
                VisualRectHeight = button.Height,
                HitRectX = button.HitX,
                HitRectY = button.HitY,
                HitRectWidth = button.HitWidth,
                HitRectHeight = button.HitHeight,
                TerrariaMouseX = mouse == null ? -1 : mouse.TerrariaMouseX,
                TerrariaMouseY = mouse == null ? -1 : mouse.TerrariaMouseY,
                TerrariaLeftDown = mouse != null && mouse.TerrariaLeftDown,
                OsClientMouseX = mouse == null ? -1 : mouse.OsClientMouseX,
                OsClientMouseY = mouse == null ? -1 : mouse.OsClientMouseY,
                OsLeftDown = mouse != null && mouse.OsLeftDown,
                UiScale = mouse == null ? 1d : mouse.UiScale,
                ClickSource = clickSource,
                UiMouseCaptureAvailable = captured,
                UiMouseCaptureMessage = captured ? "UI mouse capture marked." : "UI mouse capture unavailable.",
                UiWindow = "OperationWindow",
                UiElementId = button.Id,
                MouseCaptured = captured
            };

            lock (SyncRoot)
            {
                while (PendingCommands.Count >= 16)
                {
                    PendingCommands.Dequeue();
                }

                PendingCommands.Enqueue(command);
            }

            DiagnosticInteractionDiagnostics.RecordButton(
                button.Id,
                button.Label,
                hit.HitTestMode,
                clickSource,
                hit.HitTestX,
                hit.HitTestY,
                hit.HitTestConflict,
                hit.CandidateSummary,
                button);
            Logger.Info(
                "DiagnosticUiInteractionBridge",
                "Diagnostic button command queued: buttonId=" + button.Id +
                ", label=" + button.Label +
                ", hitTestMode=" + hit.HitTestMode +
                ", conflict=" + hit.HitTestConflict +
                ", clickSource=" + clickSource + ".");
        }

        private static DiagnosticButtonHitTestResult HitTest(IReadOnlyList<DiagnosticTestButton> buttons, DiagnosticMouseState mouse)
        {
            var miss = new DiagnosticButtonHitTestResult();
            if (buttons == null || mouse == null)
            {
                return miss;
            }

            var candidates = new List<HitCandidate>(4);
            AddCandidate(candidates, buttons, mouse.TerrariaReadAvailable, mouse.TerrariaMouseX, mouse.TerrariaMouseY, "TerrariaRaw", "T");
            AddCandidate(candidates, buttons, mouse.OsReadAvailable, mouse.OsClientMouseX, mouse.OsClientMouseY, "OsClientRaw", "OS");

            var scale = mouse.UiScale > 0.01d ? mouse.UiScale : 1d;
            AddCandidate(candidates, buttons, mouse.TerrariaReadAvailable, Scale(mouse.TerrariaMouseX, scale), Scale(mouse.TerrariaMouseY, scale), "TerrariaDivUiScale", "T/scale");
            AddCandidate(candidates, buttons, mouse.OsReadAvailable, Scale(mouse.OsClientMouseX, scale), Scale(mouse.OsClientMouseY, scale), "OsClientDivUiScale", "OS/scale");

            miss.CandidateSummary = BuildCandidateSummary(candidates);
            var winner = ChooseWinner(candidates);
            if (winner == null || winner.Button == null)
            {
                return miss;
            }

            var conflict = HasButtonConflict(candidates);
            return new DiagnosticButtonHitTestResult
            {
                Button = winner.Button,
                HitTestMode = winner.Mode,
                HitTestX = winner.X,
                HitTestY = winner.Y,
                HitTestConflict = conflict,
                CandidateSummary = miss.CandidateSummary,
                VisualRectX = winner.Button.X,
                VisualRectY = winner.Button.Y,
                VisualRectWidth = winner.Button.Width,
                VisualRectHeight = winner.Button.Height,
                HitRectX = winner.Button.HitX,
                HitRectY = winner.Button.HitY,
                HitRectWidth = winner.Button.HitWidth,
                HitRectHeight = winner.Button.HitHeight
            };
        }

        private static void AddCandidate(List<HitCandidate> candidates, IReadOnlyList<DiagnosticTestButton> buttons, bool available, int x, int y, string mode, string summaryKey)
        {
            if (candidates == null)
            {
                return;
            }

            var candidate = new HitCandidate
            {
                Available = available,
                Mode = mode ?? string.Empty,
                SummaryKey = summaryKey ?? string.Empty,
                X = x,
                Y = y
            };

            if (available && !IsClearlyInvalidCoordinate(x, y))
            {
                var button = DiagnosticTestButtonPanel.HitTest(buttons, x, y);
                candidate.Button = button;
                candidate.DistanceToButtonCenter = button == null ? double.MaxValue : button.DistanceToCenter(x, y);
            }
            else
            {
                candidate.DistanceToButtonCenter = double.MaxValue;
            }

            candidates.Add(candidate);
        }

        private static HitCandidate ChooseWinner(List<HitCandidate> candidates)
        {
            HitCandidate terrariaRaw = null;
            HitCandidate osScaled = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.Button == null)
                {
                    continue;
                }

                if (string.Equals(candidate.Mode, "TerrariaRaw", StringComparison.Ordinal))
                {
                    terrariaRaw = candidate;
                }
                else if (string.Equals(candidate.Mode, "OsClientDivUiScale", StringComparison.Ordinal))
                {
                    osScaled = candidate;
                }
            }

            if (terrariaRaw != null && osScaled != null && Distance(terrariaRaw.X, terrariaRaw.Y, osScaled.X, osScaled.Y) <= 12d)
            {
                return terrariaRaw;
            }

            HitCandidate best = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate.Button == null)
                {
                    continue;
                }

                if (best == null ||
                    candidate.DistanceToButtonCenter < best.DistanceToButtonCenter ||
                    (Math.Abs(candidate.DistanceToButtonCenter - best.DistanceToButtonCenter) < 0.01d && IsPreferredMode(candidate.Mode, best.Mode)))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static bool HasButtonConflict(List<HitCandidate> candidates)
        {
            string first = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var button = candidates[index].Button;
                if (button == null)
                {
                    continue;
                }

                if (first == null)
                {
                    first = button.Id ?? string.Empty;
                    continue;
                }

                if (!string.Equals(first, button.Id ?? string.Empty, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildCandidateSummary(List<HitCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();
            for (var index = 0; index < candidates.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                var candidate = candidates[index];
                builder.Append(candidate.SummaryKey).Append("=");
                if (!candidate.Available)
                {
                    builder.Append("unavailable");
                }
                else
                {
                    builder.Append(candidate.Button == null ? "none" : candidate.Button.Id);
                }
            }

            return builder.ToString();
        }

        private static bool IsPreferredMode(string candidateMode, string currentMode)
        {
            return ModeRank(candidateMode) < ModeRank(currentMode);
        }

        private static int ModeRank(string mode)
        {
            if (string.Equals(mode, "TerrariaRaw", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(mode, "OsClientDivUiScale", StringComparison.Ordinal))
            {
                return 1;
            }

            if (string.Equals(mode, "TerrariaDivUiScale", StringComparison.Ordinal))
            {
                return 2;
            }

            if (string.Equals(mode, "OsClientRaw", StringComparison.Ordinal))
            {
                return 3;
            }

            return 4;
        }

        private static bool IsClearlyInvalidCoordinate(int x, int y)
        {
            return x < -32 || y < -32 || x > 8192 || y > 8192;
        }

        private static bool IsOverlayHover(DiagnosticMouseState mouse, DiagnosticButtonHitTestResult hit, OperationWindowInteraction windowInteraction)
        {
            if (hit != null && hit.HasButton)
            {
                return true;
            }

            if (windowInteraction != null && windowInteraction.CapturesMouse)
            {
                return true;
            }

            if (mouse == null)
            {
                return false;
            }

            if (mouse.TerrariaReadAvailable && IsOperationWindowPoint(mouse.TerrariaMouseX, mouse.TerrariaMouseY))
            {
                return true;
            }

            if (mouse.OsReadAvailable && IsOperationWindowPoint(mouse.OsClientMouseX, mouse.OsClientMouseY))
            {
                return true;
            }

            var scale = mouse.UiScale > 0.01d ? mouse.UiScale : 1d;
            return (mouse.TerrariaReadAvailable && IsOperationWindowPoint(Scale(mouse.TerrariaMouseX, scale), Scale(mouse.TerrariaMouseY, scale))) ||
                   (mouse.OsReadAvailable && IsOperationWindowPoint(Scale(mouse.OsClientMouseX, scale), Scale(mouse.OsClientMouseY, scale)));
        }

        private static bool IsOperationWindowPoint(int x, int y)
        {
            return OperationWindowState.ContainsPoint(x, y);
        }

        private static int Scale(int value, double scale)
        {
            return (int)Math.Round(value / scale);
        }

        private static double Distance(int x1, int y1, int x2, int y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string GetClickSource(DiagnosticMouseState mouse)
        {
            if (mouse != null && mouse.OsLeftDown && mouse.TerrariaLeftDown)
            {
                return "Both";
            }

            if (mouse != null && mouse.OsLeftDown)
            {
                return "OsLeft";
            }

            if (mouse != null && mouse.TerrariaLeftDown)
            {
                return "TerrariaLeft";
            }

            return "none";
        }

        private sealed class HitCandidate
        {
            public bool Available;
            public string Mode;
            public string SummaryKey;
            public int X;
            public int Y;
            public DiagnosticTestButton Button;
            public double DistanceToButtonCenter;
        }
    }
}
