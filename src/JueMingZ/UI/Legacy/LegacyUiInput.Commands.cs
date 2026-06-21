using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyUiInput
    {
        public static bool TryDrainCommand(out LegacyUiCommand command)
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

        internal static int CountPendingCommandsByElementPrefix(string elementIdPrefix)
        {
            if (string.IsNullOrWhiteSpace(elementIdPrefix))
            {
                return 0;
            }

            lock (SyncRoot)
            {
                var count = 0;
                foreach (var command in PendingCommands)
                {
                    if (command != null &&
                        !string.IsNullOrWhiteSpace(command.ElementId) &&
                        command.ElementId.StartsWith(elementIdPrefix, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static bool TryDrainCommandByElementPrefix(string elementIdPrefix, out LegacyUiCommand command)
        {
            command = null;
            if (string.IsNullOrWhiteSpace(elementIdPrefix))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (PendingCommands.Count <= 0)
                {
                    return false;
                }

                var kept = new Queue<LegacyUiCommand>(PendingCommands.Count);
                while (PendingCommands.Count > 0)
                {
                    var next = PendingCommands.Dequeue();
                    if (command == null &&
                        next != null &&
                        !string.IsNullOrWhiteSpace(next.ElementId) &&
                        next.ElementId.StartsWith(elementIdPrefix, StringComparison.Ordinal))
                    {
                        command = next;
                    }
                    else
                    {
                        kept.Enqueue(next);
                    }
                }

                while (kept.Count > 0)
                {
                    PendingCommands.Enqueue(kept.Dequeue());
                }

                return command != null;
            }
        }

        public static void EnqueueClick(LegacyUiElement element, LegacyMouseSnapshot mouse, bool captured)
        {
            // Captured UI clicks are buffered as commands; the bounded queue prevents
            // stale UI input from growing across frames.
            if (element == null || mouse == null || !element.Enabled)
            {
                return;
            }

            lock (SyncRoot)
            {
                EnqueueLocked(CreateCommand(element, mouse, captured));
            }
        }

        public static void CaptureIfNeeded(bool inWindow)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return;
            }

            if (inWindow || IsActiveInteraction())
            {
                var captured = UiMouseCaptureService.CaptureForOperationWindow();
                DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                    true,
                    "LegacyMainUi.Draw",
                    captured,
                    false,
                    inWindow || IsActiveInteraction());
            }
        }

        public static bool CaptureCurrentMouseForWindow(string mode)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            if (!LegacyMainUiState.Visible)
            {
                return false;
            }

            if (LegacyMainUiState.HideIfMainMenu(string.IsNullOrWhiteSpace(mode) ? "LegacyMainUi.InputGuard" : mode))
            {
                return false;
            }

            var raw = DiagnosticMouseStateReader.Read();
            var mouse = BuildMouseSnapshot(raw, false);
            var inWindow = IsMouseInWindow(mouse);
            var active = IsActiveInteraction();
            if (!inWindow && !active)
            {
                return false;
            }

            var captured = UiMouseCaptureService.CaptureForOperationWindow();
            DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                true,
                string.IsNullOrWhiteSpace(mode) ? "LegacyMainUi.InputGuard" : mode,
                captured,
                false,
                inWindow || active);
            return captured;
        }

        public static bool SuppressCurrentMouseTextForWindow(string mode)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            if (!LegacyMainUiState.Visible)
            {
                return false;
            }

            if (LegacyMainUiState.HideIfMainMenu(string.IsNullOrWhiteSpace(mode) ? "LegacyMainUi.MouseTextGuard" : mode))
            {
                return false;
            }

            var raw = DiagnosticMouseStateReader.Read();
            var mouse = BuildMouseSnapshot(raw, false);
            if (!ShouldSuppressVanillaMouseText(mouse))
            {
                return false;
            }

            return UiMouseCaptureService.SuppressPendingMouseTextForOperationWindow();
        }

        private static LegacyUiCommand CreateCommand(LegacyUiElement element, LegacyMouseSnapshot mouse, bool captured)
        {
            var now = DateTime.UtcNow;
            var isDoubleClick =
                !string.IsNullOrWhiteSpace(_lastClickElementId) &&
                string.Equals(_lastClickElementId, element.Id, StringComparison.Ordinal) &&
                (now - _lastClickUtc).TotalMilliseconds <= 500d &&
                Math.Abs(mouse.X - _lastClickX) <= 6 &&
                Math.Abs(mouse.Y - _lastClickY) <= 6;

            var command = new LegacyUiCommand
            {
                ElementId = element.Id,
                Label = element.Label,
                Kind = element.Kind,
                Rect = element.Rect,
                MouseX = mouse.X,
                MouseY = mouse.Y,
                MouseReadMode = mouse.ReadMode,
                IntValue = element.IntValue,
                MouseCaptured = captured,
                IsDoubleClick = isDoubleClick,
                Candidate = element.Candidate == null ? null : element.Candidate.Clone(),
                WhitelistEntry = element.WhitelistEntry == null ? null : new BuffPotionWhitelistEntry
                {
                    ItemType = element.WhitelistEntry.ItemType,
                    BuffType = element.WhitelistEntry.BuffType,
                    ItemName = element.WhitelistEntry.ItemName,
                    BuffName = element.WhitelistEntry.BuffName
                }
            };

            _lastClickElementId = element.Id ?? string.Empty;
            _lastClickUtc = now;
            _lastClickX = mouse.X;
            _lastClickY = mouse.Y;
            return command;
        }

        private static void EnqueueLocked(LegacyUiCommand command)
        {
            while (PendingCommands.Count >= 32)
            {
                PendingCommands.Dequeue();
                _commandCoalescedCount++;
            }

            PendingCommands.Enqueue(command);
            RecordElementSource(command, command.MouseCaptured);
        }

        private static void RecordElementSource(LegacyUiCommand command, bool captured)
        {
            if (command == null)
            {
                return;
            }

            var button = new DiagnosticTestButton
            {
                Id = command.ElementId,
                Label = command.Label,
                X = command.Rect.X,
                Y = command.Rect.Y,
                Width = command.Rect.Width,
                Height = command.Rect.Height,
                Enabled = true
            };
            DiagnosticInteractionDiagnostics.RecordUiClickSuppression(true, "LegacyMainUi.Draw", captured, true, true);
            DiagnosticInteractionDiagnostics.RecordButton(
                command.ElementId,
                command.Label,
                string.IsNullOrWhiteSpace(command.MouseReadMode) ? "LegacyMainUi" : "LegacyMainUi/" + command.MouseReadMode,
                "Mouse",
                command.MouseX,
                command.MouseY,
                false,
                command.ElementId,
                button);
        }
    }
}
