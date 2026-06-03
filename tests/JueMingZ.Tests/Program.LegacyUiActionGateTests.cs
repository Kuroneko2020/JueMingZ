using System;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void LegacyUiActionUpdateGateSkipsIdleFrames()
        {
            ResetLegacyUiActionGateTestState(false);

            LegacyUiActionService.Update(null, null);

            if (LegacyUiActionService.ActionUpdateSkippedCount != 1 ||
                LegacyUiActionService.ActionUpdateRanCount != 0 ||
                LegacyUiActionService.PendingCommandCountLast != 0 ||
                LegacyUiActionService.DispatchedCommandCountLast != 0)
            {
                throw new InvalidOperationException("Expected an invisible idle Legacy UI action update to be skipped.");
            }
        }

        private static void LegacyUiActionUpdateGateRunsPendingCommandsWhenHidden()
        {
            ResetLegacyUiActionGateTestState(false);
            EnqueueUnknownLegacyUiCommandForTesting();

            LegacyUiActionService.Update(null, null);

            if (LegacyUiActionService.ActionUpdateSkippedCount != 0 ||
                LegacyUiActionService.ActionUpdateRanCount != 1 ||
                LegacyUiActionService.PendingCommandCountLast != 1 ||
                LegacyUiActionService.DispatchedCommandCountLast != 1)
            {
                throw new InvalidOperationException("Expected pending Legacy UI commands to run even when the F5 window is hidden.");
            }
        }

        private static void LegacyUiActionUpdateGateSkipsDragDispatchWithoutCommands()
        {
            ResetLegacyUiActionGateTestState(true);
            LegacyUiInput.HandleWindowFrame(
                new LegacyMouseSnapshot
                {
                    X = 72,
                    Y = 92,
                    LeftDown = true,
                    LeftPressed = true,
                    ReadAvailable = true,
                    WindowHit = true
                },
                new LegacyUiRect(40, 80, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.TitleHeight),
                new LegacyUiRect(0, 0, 0, 0));

            LegacyUiActionService.Update(null, null);

            if (LegacyUiActionService.ActionUpdateSkippedCount != 0 ||
                LegacyUiActionService.ActionUpdateRanCount != 1 ||
                LegacyUiActionService.DragFrameActionSkipCount != 1 ||
                LegacyUiActionService.DispatchedCommandCountLast != 0 ||
                LegacyUiActionService.PendingCommandCountLast != 0)
            {
                throw new InvalidOperationException("Expected an active F5 window drag frame to skip command dispatch when no command is pending.");
            }
        }

        private static void EnqueueUnknownLegacyUiCommandForTesting()
        {
            LegacyUiInput.EnqueueClick(
                new LegacyUiElement
                {
                    Id = "legacy-action-gate-test:unknown",
                    Label = "Legacy action gate test",
                    Kind = "test",
                    Rect = new LegacyUiRect(8, 8, 120, 24),
                    Enabled = true
                },
                new LegacyMouseSnapshot
                {
                    X = 16,
                    Y = 16,
                    LeftDown = true,
                    LeftPressed = true,
                    ReadAvailable = true,
                    WindowHit = true
                },
                true);
        }

        private static void ResetLegacyUiActionGateTestState(bool visible)
        {
            LegacyMainUiState.SetVisible(visible);
            LegacyUiInput.ResetInteractionState();
            LegacyUiInput.ResetActionUpdateGateStateForTesting();
            LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
        }
    }
}
