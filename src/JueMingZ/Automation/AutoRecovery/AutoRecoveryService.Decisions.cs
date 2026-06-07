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
    // Decision builders preserve cooldown and UI gates; finding a candidate is not permission to mutate player state.
    public static partial class AutoRecoveryService
    {
        private static AutoRecoveryDecision CreateAutoBuffDecision(AutoRecoverySettings settings, GameStateSnapshot snapshot, BuffPotionCandidate candidate, string triggerReason, bool immediate, string immediateTriggerReason)
        {
            return new AutoRecoveryDecision
            {
                Mode = "AutoBuff",
                Scenario = immediate ? "AutoRecovery.AutoBuffImmediate" : "AutoRecovery.AutoBuff",
                FeatureId = AutoBuffFeatureId,
                ActionKind = InputActionKind.BuffPotionDirectUse,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = triggerReason,
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life),
                LifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax),
                CurrentMana = snapshot.Player.Mana,
                MaxMana = snapshot.Player.ManaMax,
                MissingMana = Math.Max(0, snapshot.Player.ManaMax - snapshot.Player.Mana),
                ManaPercent = Percent(snapshot.Player.Mana, snapshot.Player.ManaMax),
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = settings.AutoBuffCooldownTicks,
                SourceContainer = candidate.SourceContainer,
                SourceSlot = candidate.SourceSlot,
                ItemType = candidate.ItemType,
                ItemName = candidate.ItemName,
                BuffType = candidate.BuffType,
                BuffName = candidate.BuffName,
                BuffTime = candidate.BuffTime,
                ImmediateReconcile = immediate,
                ImmediateTriggerReason = immediateTriggerReason ?? string.Empty
            };
        }

        private static bool TryDecideAutoHeal(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (!settings.AutoHealEnabled || snapshot == null || snapshot.Player == null || snapshot.Player.LifeMax <= 0 ||
                string.Equals(settings.AutoHealMode, AutoRecoverySettings.HealModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var missingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life);
            if (missingLife <= 0)
            {
                return false;
            }

            RecoveryPotionCandidate candidate;
            string selectionMessage;
            if (!RecoveryPotionCatalog.TrySelectHealPotion(
                    settings.AutoHealMode,
                    missingLife,
                    snapshot.Player.LifeMax,
                    settings.AutoHealBlockedItemTypes,
                    out candidate,
                    out selectionMessage))
            {
                lock (SyncRoot)
                {
                    State.LastAutoHealResult = selectionMessage == "SmartHealWaitingForMissingLife"
                        ? "Smart heal waiting: missing life " + missingLife.ToString(CultureInfo.InvariantCulture) + " is below the best safe potion threshold."
                        : "AutoHeal idle: " + selectionMessage + ".";
                }

                return false;
            }

            var lifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax);
            decision = new AutoRecoveryDecision
            {
                Mode = "AutoHeal",
                Scenario = "AutoRecovery.AutoHeal",
                FeatureId = AutoHealFeatureId,
                ActionKind = InputActionKind.UseInventoryItem,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = string.Equals(settings.AutoHealMode, AutoRecoverySettings.HealModeSmart, StringComparison.OrdinalIgnoreCase)
                    ? "lifeMissingSmartPotion:" + selectionMessage
                    : "lifeMissingQuickHighestPotion:" + selectionMessage,
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = missingLife,
                LifePercent = lifePercent,
                CurrentMana = snapshot.Player.Mana,
                MaxMana = snapshot.Player.ManaMax,
                MissingMana = Math.Max(0, snapshot.Player.ManaMax - snapshot.Player.Mana),
                ManaPercent = Percent(snapshot.Player.Mana, snapshot.Player.ManaMax),
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = settings.AutoHealCooldownTicks,
                SourceContainer = candidate.SourceContainer,
                SourceSlot = candidate.SourceSlot,
                ItemType = candidate.ItemType,
                ItemName = candidate.ItemName,
                BuffType = candidate.BuffType,
                BuffTime = candidate.BuffTime
            };

            long lastTick;
            lock (SyncRoot)
            {
                lastTick = State.LastAutoHealTick;
            }

            decision.AutoRecoveryCooldownBlocked = tick - lastTick < settings.AutoHealCooldownTicks;
            decision.CooldownBlocked = decision.AutoRecoveryCooldownBlocked;
            ApplyHealCooldownState(decision);
            if (snapshot.Player.IsUsingItem)
            {
                decision.CooldownBlocked = true;
                decision.PlayerUsingItemBlocked = true;
                decision.TriggerReason = decision.TriggerReason + ":playerUsingItem";
            }

            decision.ShouldEnqueue = !decision.CooldownBlocked;
            return true;
        }

        private static bool TryDecideAutoMana(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (!settings.AutoManaEnabled || snapshot == null || snapshot.Player == null || snapshot.Player.ManaMax <= 0 ||
                string.Equals(settings.AutoManaMode, AutoRecoverySettings.ManaModeOff, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                lock (SyncRoot)
                {
                    State.LastAutoManaResult = "AutoMana idle: local player unavailable for selected mana weapon check.";
                }

                return false;
            }

            var manaCheck = SelectedManaWeaponCompat.CheckSelectedItem(player);
            if (manaCheck == null || !manaCheck.IsManaWeapon || !manaCheck.InsufficientMana)
            {
                lock (SyncRoot)
                {
                    State.LastAutoManaResult = "AutoMana idle: " + (manaCheck == null ? "selectedManaWeaponCheckUnavailable" : manaCheck.Reason) + ".";
                }

                return false;
            }

            var currentMana = Math.Max(0, snapshot.Player.Mana);
            var maxMana = Math.Max(currentMana, snapshot.Player.ManaMax);
            var missingMana = Math.Max(0, maxMana - currentMana);
            if (missingMana <= 0)
            {
                lock (SyncRoot)
                {
                    State.LastAutoManaResult = "AutoMana idle: mana is already full.";
                }

                return false;
            }

            RecoveryPotionCandidate candidate;
            string selectionMessage;
            if (!RecoveryPotionCatalog.TrySelectManaPotion(settings.AutoManaBlockedItemTypes, out candidate, out selectionMessage))
            {
                lock (SyncRoot)
                {
                    State.LastAutoManaResult = "AutoMana idle: " + selectionMessage + ".";
                }

                return false;
            }

            var manaPercent = Percent(currentMana, maxMana);
            var triggerReason = (manaCheck.UsedFallbackManaCostCheck ? "selectedItemManaCostFallback" : "manaInsufficientForSelectedItem") + ":" + selectionMessage;
            decision = new AutoRecoveryDecision
            {
                Mode = "AutoMana",
                Scenario = "AutoRecovery.AutoMana",
                FeatureId = AutoManaFeatureId,
                ActionKind = InputActionKind.UseInventoryItem,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = triggerReason,
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life),
                LifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax),
                CurrentMana = currentMana,
                MaxMana = maxMana,
                MissingMana = missingMana,
                ManaPercent = manaPercent,
                SelectedItemType = manaCheck.SelectedItemType,
                SelectedItemName = manaCheck.SelectedItemName,
                SelectedItemManaCost = manaCheck.SelectedItemManaCost,
                RequiredMana = manaCheck.RequiredMana,
                CheckManaAvailable = manaCheck.CheckManaAvailable,
                CheckManaResult = manaCheck.CheckManaResult,
                UsedFallbackManaCostCheck = manaCheck.UsedFallbackManaCostCheck,
                ManaCheckReason = manaCheck.Reason,
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = settings.AutoManaCooldownTicks,
                SourceContainer = candidate.SourceContainer,
                SourceSlot = candidate.SourceSlot,
                ItemType = candidate.ItemType,
                ItemName = candidate.ItemName,
                BuffType = candidate.BuffType,
                BuffTime = candidate.BuffTime
            };

            long lastTick;
            lock (SyncRoot)
            {
                lastTick = State.LastAutoManaTick;
            }

            var effectiveCooldownTicks = Math.Min(
                settings.AutoManaCooldownTicks <= 0 ? AutoRecoverySettings.AutoManaImmediateCooldownTicks : settings.AutoManaCooldownTicks,
                AutoRecoverySettings.AutoManaImmediateCooldownTicks);
            decision.CooldownTicks = effectiveCooldownTicks;
            decision.AutoRecoveryCooldownBlocked = tick - lastTick < effectiveCooldownTicks;
            decision.CooldownBlocked = decision.AutoRecoveryCooldownBlocked;
            ApplyManaCooldownState(decision);
            if (snapshot.Player.IsUsingItem)
            {
                decision.TriggerReason = decision.TriggerReason + ":playerUsingItemAllowed";
            }

            decision.ShouldEnqueue = !decision.CooldownBlocked;
            return true;
        }

        private static bool TryDecideAutoNurse(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (!settings.AutoNurseEnabled || snapshot == null || snapshot.Player == null)
            {
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || !NurseServiceCompat.NeedsNurse(player))
            {
                return false;
            }

            NurseTarget target;
            string message;
            if (!NurseServiceCompat.TryFindReachableNurse(player, out target, out message))
            {
                lock (SyncRoot)
                {
                    State.LastAutoNurseResult = "AutoNurse idle: " + message;
                }

                return false;
            }

            decision = new AutoRecoveryDecision
            {
                Mode = "AutoNurse",
                Scenario = "AutoRecovery.AutoNurse",
                FeatureId = AutoNurseFeatureId,
                ActionKind = InputActionKind.NpcInteract,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = "lifeMissingOrDebuffReachableNurse",
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life),
                LifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax),
                CurrentMana = snapshot.Player.Mana,
                MaxMana = snapshot.Player.ManaMax,
                MissingMana = Math.Max(0, snapshot.Player.ManaMax - snapshot.Player.Mana),
                ManaPercent = Percent(snapshot.Player.Mana, snapshot.Player.ManaMax),
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = AutoNurseCooldownTicks,
                NpcIndex = target.NpcIndex,
                RemovableDebuffCount = NurseServiceCompat.CountRemovableDebuffs(player)
            };

            long lastTick;
            lock (SyncRoot)
            {
                lastTick = State.LastAutoNurseTick;
            }

            decision.AutoRecoveryCooldownBlocked = tick - lastTick < AutoNurseCooldownTicks;
            decision.CooldownBlocked = decision.AutoRecoveryCooldownBlocked;
            decision.ShouldEnqueue = !decision.CooldownBlocked;
            return true;
        }

        private static bool TryDecideAutoStationBuff(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (!settings.AutoStationBuffEnabled || snapshot == null || snapshot.Player == null)
            {
                return false;
            }

            long lastTick;
            lock (SyncRoot)
            {
                lastTick = State.LastAutoStationBuffTick;
            }

            if (tick - lastTick < AutoStationBuffCooldownTicks)
            {
                RecordAutoStationBuffCooldownFastSkip(tick, Math.Max(0, AutoStationBuffCooldownTicks - (tick - lastTick)));
                return false;
            }

            var missingBuffMask = BuildAutoStationBuffMissingMask(snapshot.ActiveBuffs);
            if (missingBuffMask == 0)
            {
                RecordAutoStationBuffActiveBuffFastSkip(tick);
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return false;
            }

            List<StationBuffTarget> targets;
            string message;
            if (!StationBuffCompat.TryFindMissingStationBuffs(player, missingBuffMask, tick, out targets, out message) || targets == null || targets.Count <= 0)
            {
                lock (SyncRoot)
                {
                    State.LastAutoStationBuffResult = "AutoStationBuff idle: " + message;
                }

                return false;
            }

            var target = targets[0];
            decision = new AutoRecoveryDecision
            {
                Mode = "AutoStationBuff",
                Scenario = "AutoRecovery.AutoStationBuff",
                FeatureId = AutoStationBuffFeatureId,
                ActionKind = InputActionKind.TileInteract,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = targets.Count > 1 ? "missingReachableStationBuffBatch" : "missingReachableStationBuff",
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life),
                LifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax),
                CurrentMana = snapshot.Player.Mana,
                MaxMana = snapshot.Player.ManaMax,
                MissingMana = Math.Max(0, snapshot.Player.ManaMax - snapshot.Player.Mana),
                ManaPercent = Percent(snapshot.Player.Mana, snapshot.Player.ManaMax),
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = AutoStationBuffCooldownTicks,
                TileX = target.TileX,
                TileY = target.TileY,
                TileType = target.TileType,
                BuffType = target.BuffType,
                BuffName = target.Name,
                StationBuffTargets = targets
            };

            decision.AutoRecoveryCooldownBlocked = false;
            decision.CooldownBlocked = false;
            decision.ShouldEnqueue = true;
            return true;
        }

        private static bool TryDecideAutoBuff(AutoRecoverySettings settings, GameStateSnapshot snapshot, long tick, out AutoRecoveryDecision decision)
        {
            decision = null;
            if (!settings.AutoBuffEnabled || snapshot == null || snapshot.Player == null)
            {
                return false;
            }

            if (BuffPotionWhitelistService.Count <= 0)
            {
                lock (SyncRoot)
                {
                    State.LastAutoBuffResult = "AutoBuff enabled, but whitelist is empty; idle and will not consume buff potions.";
                }

                return false;
            }

            decision = new AutoRecoveryDecision
            {
                Mode = "AutoBuff",
                Scenario = "AutoRecovery.AutoBuff",
                FeatureId = AutoBuffFeatureId,
                ActionKind = InputActionKind.BuffPotionDirectUse,
                AutoHealMode = settings.AutoHealMode,
                AutoManaMode = settings.AutoManaMode,
                TriggerReason = "autoBuffWhitelistStrategyDue",
                ThresholdPercent = 0,
                CurrentLife = snapshot.Player.Life,
                MaxLife = snapshot.Player.LifeMax,
                MissingLife = Math.Max(0, snapshot.Player.LifeMax - snapshot.Player.Life),
                LifePercent = Percent(snapshot.Player.Life, snapshot.Player.LifeMax),
                CurrentMana = snapshot.Player.Mana,
                MaxMana = snapshot.Player.ManaMax,
                MissingMana = Math.Max(0, snapshot.Player.ManaMax - snapshot.Player.Mana),
                ManaPercent = Percent(snapshot.Player.Mana, snapshot.Player.ManaMax),
                BuffCountBefore = snapshot.ActiveBuffs == null ? 0 : snapshot.ActiveBuffs.Count,
                BuffTypesBeforeJson = BuildBuffTypesJson(snapshot.ActiveBuffs),
                CooldownTicks = settings.AutoBuffCooldownTicks
            };

            long lastTick;
            lock (SyncRoot)
            {
                lastTick = State.LastAutoBuffTick;
            }

            decision.AutoRecoveryCooldownBlocked = tick - lastTick < settings.AutoBuffCooldownTicks;
            decision.CooldownBlocked = decision.AutoRecoveryCooldownBlocked;
            if (snapshot.Player.IsUsingItem)
            {
                decision.CooldownBlocked = true;
                decision.PlayerUsingItemBlocked = true;
                decision.TriggerReason = "autoBuffDueButPlayerUsingItem";
            }

            if (!decision.CooldownBlocked)
            {
                var scan = BuffPotionCatalog.RefreshCandidates();
                RecordMissingWhitelistItemIfNeeded(scan, tick);
                BuffPotionCandidate candidate = null;
                for (var index = 0; index < scan.Candidates.Count; index++)
                {
                    var current = scan.Candidates[index];
                    if (current != null && current.IsWhitelisted && current.CanApply)
                    {
                        candidate = current;
                        break;
                    }
                }

                if (candidate == null)
                {
                    lock (SyncRoot)
                    {
                        State.LastAutoBuffResult = "AutoBuff scanned whitelist, but no missing whitelisted buff potion needs use.";
                    }

                    return false;
                }

                decision.SourceContainer = candidate.SourceContainer;
                decision.SourceSlot = candidate.SourceSlot;
                decision.ItemType = candidate.ItemType;
                decision.ItemName = candidate.ItemName;
                decision.BuffType = candidate.BuffType;
                decision.BuffName = candidate.BuffName;
                decision.BuffTime = candidate.BuffTime;
            }

            decision.ShouldEnqueue = !decision.CooldownBlocked;
            return true;
        }

        private static string FormatStationBuffTargets(List<StationBuffTarget> targets)
        {
            if (targets == null || targets.Count <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(";");
                }

                builder.Append(target.TileX.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.TileY.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.TileType.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.BuffType.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static int BuildAutoStationBuffMissingMask(IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            var missingMask = StationBuffCompat.AllKnownStationBuffMask;
            if (activeBuffs == null || activeBuffs.Count <= 0)
            {
                return missingMask;
            }

            for (var index = 0; index < activeBuffs.Count; index++)
            {
                var buff = activeBuffs[index];
                if (buff == null || buff.BuffType <= 0 || buff.BuffTime <= 0)
                {
                    continue;
                }

                missingMask &= ~StationBuffCompat.GetStationBuffMaskForBuffType(buff.BuffType);
                if (missingMask == 0)
                {
                    return 0;
                }
            }

            return missingMask;
        }

        private static void RecordAutoStationBuffCooldownFastSkip(long tick, long remainingTicks)
        {
            lock (SyncRoot)
            {
                State.AutoStationBuffCooldownFastSkipCount++;
                if (ShouldUpdateAutoStationBuffFastSkipResultLocked(tick))
                {
                    State.LastAutoStationBuffResult = "AutoStationBuff idle: cooldownFastSkip remaining " + remainingTicks.ToString(CultureInfo.InvariantCulture) + " tick(s).";
                    _lastAutoStationBuffFastSkipResultTick = tick;
                }
            }
        }

        private static void RecordAutoStationBuffActiveBuffFastSkip(long tick)
        {
            lock (SyncRoot)
            {
                State.AutoStationBuffActiveBuffFastSkipCount++;
                if (ShouldUpdateAutoStationBuffFastSkipResultLocked(tick))
                {
                    State.LastAutoStationBuffResult = "AutoStationBuff idle: activeBuffFastSkip all known station buffs are active.";
                    _lastAutoStationBuffFastSkipResultTick = tick;
                }
            }
        }

        private static bool ShouldUpdateAutoStationBuffFastSkipResultLocked(long tick)
        {
            return _lastAutoStationBuffFastSkipResultTick == ForceDueTick ||
                   tick < _lastAutoStationBuffFastSkipResultTick ||
                   tick - _lastAutoStationBuffFastSkipResultTick >= AutoStationBuffFastSkipResultThrottleTicks;
        }

        private static int Percent(int current, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            return (int)Math.Floor((current * 100.0d) / max);
        }

    }
}
