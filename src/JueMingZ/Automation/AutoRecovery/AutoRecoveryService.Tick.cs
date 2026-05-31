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
    public static partial class AutoRecoveryService
    {
        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(queue, snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.AutoRecovery ?? AutoRecoverySettings.FromConfig();
            lock (SyncRoot)
            {
                ApplySettingsLocked(settings);
            }

            if (!settings.AnyEnabled || queue == null || runtimeState == null)
            {
                return;
            }

            if (!CanRun(snapshot, settings))
            {
                return;
            }

            var tick = runtimeState.UpdateCount;
            var queueSnapshot = queue.GetFastState();
            var onlyNpcChatBlocking = IsOnlyNpcChatBlocking(snapshot);
            if (!onlyNpcChatBlocking)
            {
                DetectImmediateAutoBuffTriggers(settings, snapshot, tick);
                if (TryProcessImmediateAutoBuff(queue, settings, snapshot, tick, queueSnapshot))
                {
                    return;
                }
            }

            if (WasF5ControlClickedRecently())
            {
                return;
            }

            queueSnapshot = queue.GetFastState();
            if (queueSnapshot.PendingCount > 0 || queueSnapshot.HasRunningAction)
            {
                return;
            }

            AutoRecoveryDecision decision;
            if (TrySelectReadyDecision(settings, snapshot, tick, out decision))
            {
                EnqueueDecision(queue, decision, tick);
            }
        }

        private static bool TrySelectReadyDecision(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            AutoRecoveryDecision candidate;
            if (IsOnlyNpcChatBlocking(snapshot))
            {
                return TryDecideAutoNurse(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision);
            }

            if (TryDecideAutoHeal(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision))
            {
                return true;
            }

            if (TryDecideAutoMana(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision))
            {
                return true;
            }

            if (TryDecideAutoNurse(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision))
            {
                return true;
            }

            if (!IsOnlyChestBlocking(snapshot) &&
                TryDecideAutoStationBuff(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision))
            {
                return true;
            }

            return TryDecideAutoBuff(settings, snapshot, tick, out candidate) && TryUseOrRecordBlocked(candidate, tick, out decision);
        }

        private static bool TryUseOrRecordBlocked(AutoRecoveryDecision candidate, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (candidate == null)
            {
                return false;
            }

            if (!candidate.CooldownBlocked)
            {
                decision = candidate;
                return true;
            }

            RecordBlockedIfNeeded(candidate, tick);
            return false;
        }

        public static void AfterActionQueueUpdate(InputActionQueueFastState queueSnapshot)
        {
            var result = queueSnapshot == null ? null : queueSnapshot.LastResult;
            if (result == null || string.IsNullOrWhiteSpace(result.Scenario) ||
                !result.Scenario.StartsWith("AutoRecovery.", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (SyncRoot)
            {
                int completedAutoBuffItemType;
                if (AutoBuffRequestItems.TryGetValue(result.RequestId, out completedAutoBuffItemType))
                {
                    AutoBuffRequestItems.Remove(result.RequestId);
                    AutoBuffInflightItemTypes.Remove(completedAutoBuffItemType);
                    AutoBuffInflightTicks.Remove(completedAutoBuffItemType);
                    if (IsSuccessfulAutoBuffResult(result))
                    {
                        AutoBuffLastFailedTicks.Remove(completedAutoBuffItemType);
                    }
                    else
                    {
                        AutoBuffLastFailedTicks[completedAutoBuffItemType] = GetCurrentRuntimeTick();
                    }

                    State.ImmediateBuffInflightCount = AutoBuffInflightItemTypes.Count;
                }

                if (State.LastObservedActionRequestId == result.RequestId)
                {
                    return;
                }

                State.LastObservedActionRequestId = result.RequestId;
                var line = result.Status + "/" + result.ResultCode + ": " + (result.Message ?? string.Empty);
                if (string.Equals(result.Scenario, "AutoRecovery.AutoHeal", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoHealResult = line;
                    State.QuickHealCapability = GetCapabilityFromResult(result);
                }
                else if (string.Equals(result.Scenario, "AutoRecovery.AutoMana", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoManaResult = line;
                    State.QuickManaCapability = GetCapabilityFromResult(result);
                }
                else if (string.Equals(result.Scenario, "AutoRecovery.AutoNurse", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoNurseResult = line;
                }
                else if (string.Equals(result.Scenario, "AutoRecovery.AutoStationBuff", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoStationBuffResult = line;
                }
                else if (IsAutoBuffScenario(result.Scenario))
                {
                    State.LastAutoBuffResult = line;
                    State.LastAutoBuffCountAfter = ExtractInt(result.Message, State.LastAutoBuffCountBefore);
                    State.QuickBuffCapability = GetCapabilityFromResult(result);
                }
            }
        }

        private static long GetCurrentRuntimeTick()
        {
            return JueMingZRuntime.State == null ? 0 : JueMingZRuntime.State.UpdateCount;
        }

        private static bool CanRun(GameStateSnapshot snapshot, AutoRecoverySettings settings)
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.Player == null || snapshot.Ui == null)
            {
                return false;
            }

            if (snapshot.IsInMainMenu || snapshot.Ui.IsInMainMenu)
            {
                return false;
            }

            if (snapshot.Ui.HasBlockingUi &&
                !(settings != null && settings.AutoNurseEnabled && IsOnlyNpcChatBlocking(snapshot)) &&
                !IsOnlyChestBlocking(snapshot))
            {
                return false;
            }

            return snapshot.Player.Exists && snapshot.Player.Active && !snapshot.Player.Dead && !snapshot.Player.Ghost;
        }

        private static bool IsOnlyNpcChatBlocking(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Ui != null &&
                   snapshot.Ui.NpcChatOpen &&
                   !snapshot.Ui.ChatOpen &&
                   !snapshot.Ui.ChestOpen &&
                   !snapshot.Ui.IsInMainMenu;
        }

        private static bool IsOnlyChestBlocking(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.Ui != null &&
                   snapshot.Ui.ChestOpen &&
                   !snapshot.Ui.ChatOpen &&
                   !snapshot.Ui.NpcChatOpen &&
                   !snapshot.Ui.IsInMainMenu;
        }

        private static bool WasF5ControlClickedRecently()
        {
            var lastButtonUtc = DiagnosticInteractionDiagnostics.LastButtonClickUtc;
            if (lastButtonUtc.HasValue && DateTime.UtcNow - lastButtonUtc.Value < TimeSpan.FromMilliseconds(150))
            {
                return true;
            }

            lock (SyncRoot)
            {
                return DateTime.UtcNow - _lastF5ControlUtc < TimeSpan.FromMilliseconds(150);
            }
        }
    }
}
