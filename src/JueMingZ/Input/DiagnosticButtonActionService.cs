using System;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI;

namespace JueMingZ.Input
{
    public static class DiagnosticButtonActionService
    {
        public static void Update(InputActionQueue queue, GameStateSnapshot snapshot)
        {
            try
            {
                DiagnosticButtonCommand command;
                while (DiagnosticUiInteractionBridge.TryDrainButtonCommand(out command))
                {
                    DispatchButton(command, queue, snapshot);
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "diagnostic-button-action-error",
                    TimeSpan.FromSeconds(10),
                    "DiagnosticButtonActionService",
                    "Diagnostic button update failed.", error);
            }
        }

        private static void DispatchButton(DiagnosticButtonCommand command, InputActionQueue queue, GameStateSnapshot snapshot)
        {
            if (command == null)
            {
                return;
            }

            Logger.Info(
                "DiagnosticButtonActionService",
                "Diagnostic button clicked: buttonId=" + command.ButtonId +
                ", label=" + command.ButtonLabel +
                ", hitTestMode=" + command.HitTestMode +
                ", clickSource=" + command.ClickSource + ".");
            var source = DiagnosticActionSource.ForButton(command);
            switch (command.ButtonId)
            {
                case "toggle-diagnostic-input":
                    DiagnosticActionDispatcher.ToggleDiagnosticInput(source);
                    break;
                case "noop":
                    DiagnosticActionDispatcher.EnqueueDiagnosticNoop(queue, source);
                    break;
                case "select-test-slot":
                    DiagnosticActionDispatcher.EnqueueSelectHotbarSlot(queue, snapshot, source);
                    break;
                case "use-selected-item":
                    DiagnosticActionDispatcher.EnqueueUseSelectedItem(queue, snapshot, source);
                    break;
                case "prev-test-slot":
                    DiagnosticActionDispatcher.ChangeDiagnosticTestSlot(-1, snapshot, source);
                    break;
                case "next-test-slot":
                    DiagnosticActionDispatcher.ChangeDiagnosticTestSlot(1, snapshot, source);
                    break;
                case "use-hotbar-item":
                    DiagnosticActionDispatcher.EnqueueUseHotbarItem(queue, snapshot, source);
                    break;
                case "quick-heal":
                    DiagnosticActionDispatcher.EnqueueQuickHeal(queue, snapshot, source);
                    break;
                case "quick-mana":
                    DiagnosticActionDispatcher.EnqueueQuickMana(queue, snapshot, source);
                    break;
                case "quick-buff":
                    DiagnosticActionDispatcher.EnqueueQuickBuffOnce(queue, snapshot, source);
                    break;
                case "quick-buff-once":
                case "quick-buff-once-bottom":
                    DiagnosticActionDispatcher.EnqueueQuickBuffOnce(queue, snapshot, source);
                    break;
                case "buff-refresh-candidates":
                    DiagnosticActionDispatcher.RefreshBuffPotionCandidates(source);
                    break;
                case "buff-prev-candidate":
                    DiagnosticActionDispatcher.MoveBuffPotionCandidate(-1, source);
                    break;
                case "buff-next-candidate":
                    DiagnosticActionDispatcher.MoveBuffPotionCandidate(1, source);
                    break;
                case "buff-add-whitelist":
                    DiagnosticActionDispatcher.AddSelectedBuffPotionToWhitelist(source);
                    break;
                case "buff-remove-whitelist":
                    DiagnosticActionDispatcher.RemoveSelectedBuffPotionFromWhitelist(source);
                    break;
                case "buff-clear-whitelist":
                    DiagnosticActionDispatcher.ClearBuffPotionWhitelist(source);
                    break;
                case "buff-use-selected-once":
                    DiagnosticActionDispatcher.EnqueueBuffPotionUseSelectedOnce(queue, snapshot, source);
                    break;
                case "mouse-target-dry-run":
                    DiagnosticActionDispatcher.EnqueueMouseTargetDryRun(queue, snapshot, source);
                    break;
                case "auto-heal-toggle":
                    DiagnosticActionDispatcher.ToggleAutoHeal(source);
                    break;
                case "auto-mana-toggle":
                    DiagnosticActionDispatcher.ToggleAutoMana(source);
                    break;
                case "auto-buff-toggle":
                    DiagnosticActionDispatcher.ToggleAutoBuff(source);
                    break;
                default:
                    Logger.Warn("DiagnosticButtonActionService", "Unsupported diagnostic button: " + command.ButtonId);
                    DiagnosticActionDispatcher.RecordUnsupportedButton(source);
                    break;
            }
        }
    }
}
