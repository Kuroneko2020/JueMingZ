using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.AutoRecovery
{
    // Blocked diagnostics describe why no action was attempted; they must not refresh inventories or change cooldowns.
    public static partial class AutoRecoveryService
    {
        private static void RecordBlockedIfNeeded(AutoRecoveryDecision decision, long tick)
        {
            if (decision == null)
            {
                return;
            }

            var shouldRecord = false;
            lock (SyncRoot)
            {
                if ((decision.ActionKind == InputActionKind.QuickHeal ||
                     (decision.ActionKind == InputActionKind.UseInventoryItem && string.Equals(decision.Mode, "AutoHeal", StringComparison.OrdinalIgnoreCase))) &&
                    tick - _lastAutoHealBlockedEventTick >= BlockedEventThrottleTicks)
                {
                    _lastAutoHealBlockedEventTick = tick;
                    State.LastAutoHealResult = "BlockedByCooldown: " + decision.TriggerReason + ".";
                    shouldRecord = true;
                }
                else if ((decision.ActionKind == InputActionKind.QuickMana ||
                          (decision.ActionKind == InputActionKind.UseInventoryItem && string.Equals(decision.Mode, "AutoMana", StringComparison.OrdinalIgnoreCase))) &&
                         tick - _lastAutoManaBlockedEventTick >= BlockedEventThrottleTicks)
                {
                    _lastAutoManaBlockedEventTick = tick;
                    State.LastAutoManaResult = "BlockedByCooldown: " + decision.TriggerReason + ".";
                    shouldRecord = true;
                }
                else if (decision.ActionKind == InputActionKind.QuickBuff && tick - _lastAutoBuffBlockedEventTick >= BlockedEventThrottleTicks)
                {
                    _lastAutoBuffBlockedEventTick = tick;
                    State.LastAutoBuffResult = "BlockedByCooldown: " + decision.TriggerReason + ".";
                    shouldRecord = true;
                }
                else if (decision.ActionKind == InputActionKind.BuffPotionDirectUse)
                {
                    State.LastAutoBuffResult = "AutoBuff idle: " + decision.TriggerReason + ".";
                    shouldRecord = false;
                }
                else if (decision.ActionKind == InputActionKind.NpcInteract)
                {
                    State.LastAutoNurseResult = "BlockedByCooldown: " + decision.TriggerReason + ".";
                    shouldRecord = false;
                }
                else if (decision.ActionKind == InputActionKind.TileInteract)
                {
                    State.LastAutoStationBuffResult = "BlockedByCooldown: " + decision.TriggerReason + ".";
                    shouldRecord = false;
                }
            }

            if (!shouldRecord)
            {
                return;
            }

            var message = decision.Mode + " skipped by bounded cooldown/debounce; no vanilla quick action was submitted.";
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                decision.Scenario,
                decision.ActionKind.ToString(),
                string.Empty,
                InputActionStatus.NotApplicable.ToString(),
                DiagnosticResultCode.BlockedByCooldown.ToString(),
                message,
                0,
                BuildDecisionBeforeJson(decision, true),
                BuildDecisionAfterJson(decision, false, DiagnosticResultCode.BlockedByCooldown.ToString(), message),
                "{\"observableChange\":false,\"changedFields\":[],\"cooldownBlocked\":true}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

    }
}
