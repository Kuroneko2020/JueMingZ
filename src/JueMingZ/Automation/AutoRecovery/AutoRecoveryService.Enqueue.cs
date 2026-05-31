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
        private static Guid EnqueueDecision(InputActionQueue queue, AutoRecoveryDecision decision, long tick)
        {
            var request = new InputActionRequest
            {
                Kind = decision.ActionKind,
                Priority = InputActionPriority.Low,
                SourceFeatureId = decision.FeatureId,
                Description = BuildDecisionDescription(decision),
                Timeout = TimeSpan.FromSeconds(3)
            };

            request.Metadata["Scenario"] = decision.Scenario;
            request.Metadata["SourceKind"] = "Automation";
            request.Metadata["SourceUi"] = string.Empty;
            request.Metadata["ButtonId"] = string.Empty;
            request.Metadata["ButtonLabel"] = string.Empty;
            request.Metadata["SourceHotkey"] = string.Empty;
            request.Metadata["AutoRecoveryMode"] = decision.Mode;
            request.Metadata["AutoRecoveryEnabled"] = "true";
            request.Metadata["AutoHealMode"] = decision.AutoHealMode ?? string.Empty;
            request.Metadata["AutoManaMode"] = decision.AutoManaMode ?? string.Empty;
            request.Metadata["ThresholdPercent"] = decision.ThresholdPercent.ToString(CultureInfo.InvariantCulture);
            request.Metadata["CurrentLife"] = decision.CurrentLife.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MaxLife"] = decision.MaxLife.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MissingLife"] = decision.MissingLife.ToString(CultureInfo.InvariantCulture);
            request.Metadata["LifePercent"] = decision.LifePercent.ToString(CultureInfo.InvariantCulture);
            request.Metadata["CurrentMana"] = decision.CurrentMana.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MaxMana"] = decision.MaxMana.ToString(CultureInfo.InvariantCulture);
            request.Metadata["MissingMana"] = decision.MissingMana.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ManaPercent"] = decision.ManaPercent.ToString(CultureInfo.InvariantCulture);
            request.Metadata["TriggerReason"] = decision.TriggerReason;
            request.Metadata["CooldownBlocked"] = "false";
            request.Metadata["PotionSicknessBlocked"] = decision.PotionSicknessBlocked ? "true" : "false";
            request.Metadata["ManaSicknessBlocked"] = decision.ManaSicknessBlocked ? "true" : "false";
            request.Metadata["PotionDelay"] = decision.PotionDelay.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ManaSickTime"] = decision.ManaSickTime.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoRecoveryCooldownBlocked"] = decision.AutoRecoveryCooldownBlocked ? "true" : "false";
            request.Metadata["PlayerUsingItemBlocked"] = decision.PlayerUsingItemBlocked ? "true" : "false";
            request.Metadata["CooldownTicks"] = decision.CooldownTicks.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BuffCountBefore"] = decision.BuffCountBefore.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BuffTypesBeforeJson"] = decision.BuffTypesBeforeJson;
            request.Metadata["ImmediateReconcile"] = decision.ImmediateReconcile ? "true" : "false";
            request.Metadata["ImmediateTriggerReason"] = decision.ImmediateTriggerReason ?? string.Empty;
            if (decision.ActionKind == InputActionKind.QuickHeal)
            {
                request.Metadata["ExecutionMode"] = "QuickHealCompat";
                request.Metadata["QuickHealAttempted"] = "true";
                request.Metadata["SmartHealAttempted"] = "false";
            }
            else if (decision.ActionKind == InputActionKind.QuickMana)
            {
                request.Metadata["ExecutionMode"] = "QuickManaCompat";
                request.Metadata["QuickManaAttempted"] = "true";
                request.Metadata["ManaFlowerLogicAttempted"] = "true";
            }
            else if (decision.ActionKind == InputActionKind.UseInventoryItem)
            {
                request.Metadata["ExecutionMode"] = "OriginalRecoveryItemUse";
                request.Metadata["UseInventoryItemPurpose"] = decision.Mode;
                request.Metadata["SourceContainer"] = decision.SourceContainer;
                request.Metadata["SourceSlot"] = decision.SourceSlot.ToString(CultureInfo.InvariantCulture);
                request.Metadata["ItemType"] = decision.ItemType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["ItemName"] = decision.ItemName ?? string.Empty;
                request.Metadata["BuffType"] = decision.BuffType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["BuffName"] = decision.BuffName ?? string.Empty;
                request.Metadata["BuffTime"] = decision.BuffTime.ToString(CultureInfo.InvariantCulture);
                request.Metadata["RecoveryPotionUseAttempted"] = "true";
                if (string.Equals(decision.Mode, "AutoMana", StringComparison.OrdinalIgnoreCase))
                {
                    request.Metadata["RequiredMana"] = decision.RequiredMana.ToString(CultureInfo.InvariantCulture);
                    request.Metadata["SelectedItemType"] = decision.SelectedItemType.ToString(CultureInfo.InvariantCulture);
                    request.Metadata["SelectedItemName"] = decision.SelectedItemName ?? string.Empty;
                    request.Metadata["SelectedItemManaCost"] = decision.SelectedItemManaCost.ToString(CultureInfo.InvariantCulture);
                    request.Metadata["CheckManaAvailable"] = decision.CheckManaAvailable ? "true" : "false";
                    request.Metadata["CheckManaResult"] = decision.CheckManaResult ? "true" : "false";
                    request.Metadata["UsedFallbackManaCostCheck"] = decision.UsedFallbackManaCostCheck ? "true" : "false";
                    request.Metadata["ManaCheckReason"] = decision.ManaCheckReason ?? string.Empty;
                }
            }
            else if (decision.ActionKind == InputActionKind.NpcInteract)
            {
                request.Metadata["ExecutionMode"] = "NurseChatHeal";
                request.Metadata["Interaction"] = "NurseHeal";
                request.Metadata["NpcIndex"] = decision.NpcIndex.ToString(CultureInfo.InvariantCulture);
                request.Metadata["RemovableDebuffCount"] = decision.RemovableDebuffCount.ToString(CultureInfo.InvariantCulture);
            }
            else if (decision.ActionKind == InputActionKind.TileInteract)
            {
                request.Metadata["ExecutionMode"] = "TileUse";
                request.Metadata["Interaction"] = "StationBuff";
                request.Metadata["TileX"] = decision.TileX.ToString(CultureInfo.InvariantCulture);
                request.Metadata["TileY"] = decision.TileY.ToString(CultureInfo.InvariantCulture);
                request.Metadata["TileType"] = decision.TileType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["BuffType"] = decision.BuffType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["BuffName"] = decision.BuffName ?? string.Empty;
                request.Metadata["StationBuffTargetCount"] = (decision.StationBuffTargets == null ? 0 : decision.StationBuffTargets.Count).ToString(CultureInfo.InvariantCulture);
                request.Metadata["StationBuffTargets"] = FormatStationBuffTargets(decision.StationBuffTargets);
            }

            if (decision.ActionKind == InputActionKind.BuffPotionDirectUse)
            {
                request.Metadata["ExecutionMode"] = ActionExecutionModes.DirectLocalBuffPotion;
                request.Metadata["SourceContainer"] = decision.SourceContainer;
                request.Metadata["SourceSlot"] = decision.SourceSlot.ToString(CultureInfo.InvariantCulture);
                request.Metadata["ItemType"] = decision.ItemType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["ItemName"] = decision.ItemName ?? string.Empty;
                request.Metadata["BuffType"] = decision.BuffType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["BuffName"] = decision.BuffName ?? string.Empty;
                request.Metadata["BuffTime"] = decision.BuffTime.ToString(CultureInfo.InvariantCulture);
            }

            var requestId = queue.Enqueue(request);
            lock (SyncRoot)
            {
                if (decision.ActionKind == InputActionKind.BuffPotionDirectUse && decision.ItemType > 0)
                {
                    AutoBuffRequestItems[requestId] = decision.ItemType;
                    AutoBuffInflightItemTypes.Add(decision.ItemType);
                    AutoBuffInflightTicks[decision.ItemType] = tick;
                    State.ImmediateBuffInflightCount = AutoBuffInflightItemTypes.Count;
                }

                if (decision.ActionKind == InputActionKind.QuickHeal)
                {
                    State.LastAutoHealTick = tick;
                    State.LastAutoHealResult = "Queued original QuickHeal request " + requestId + ".";
                }
                else if (decision.ActionKind == InputActionKind.QuickMana)
                {
                    State.LastAutoManaTick = tick;
                    State.LastAutoManaResult = "Queued original QuickMana request " + requestId + ".";
                }
                else if (decision.ActionKind == InputActionKind.UseInventoryItem && string.Equals(decision.Mode, "AutoHeal", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoHealTick = tick;
                    State.LastAutoHealResult = "Queued recovery potion " + requestId + " for " + decision.ItemName + ".";
                }
                else if (decision.ActionKind == InputActionKind.UseInventoryItem && string.Equals(decision.Mode, "AutoMana", StringComparison.OrdinalIgnoreCase))
                {
                    State.LastAutoManaTick = tick;
                    State.LastAutoManaResult = "Queued mana potion " + requestId + " for " + decision.ItemName + ".";
                }
                else if (decision.ActionKind == InputActionKind.QuickBuff)
                {
                    State.LastAutoBuffTick = tick;
                    State.LastAutoBuffCountBefore = decision.BuffCountBefore;
                    State.LastAutoBuffResult = "Queued original QuickBuff request " + requestId + ".";
                }
                else if (decision.ActionKind == InputActionKind.BuffPotionDirectUse)
                {
                    State.LastAutoBuffTick = tick;
                    State.LastAutoBuffCountBefore = decision.BuffCountBefore;
                    State.LastAutoBuffResult = "Queued controlled buff potion request " + requestId + " for " + decision.ItemName + ".";
                }
                else if (decision.ActionKind == InputActionKind.NpcInteract)
                {
                    State.LastAutoNurseTick = tick;
                    State.LastAutoNurseResult = "Queued nurse heal request " + requestId + ".";
                }
                else if (decision.ActionKind == InputActionKind.TileInteract)
                {
                    State.LastAutoStationBuffTick = tick;
                    var targetCount = decision.StationBuffTargets == null ? 1 : Math.Max(1, decision.StationBuffTargets.Count);
                    State.LastAutoStationBuffResult = "Queued station buff request " + requestId + " for " + targetCount.ToString(CultureInfo.InvariantCulture) + " station target(s).";
                }
            }

            return requestId;
        }

        private static string BuildDecisionDescription(AutoRecoveryDecision decision)
        {
            if (decision == null)
            {
                return "AutoRecovery request.";
            }

            if (decision.ActionKind == InputActionKind.UseInventoryItem)
            {
                return decision.Mode + " uses " + (string.IsNullOrWhiteSpace(decision.ItemName) ? "inventory recovery item" : decision.ItemName) + ".";
            }

            if (decision.ActionKind == InputActionKind.NpcInteract)
            {
                return "AutoNurse opens nurse heal interaction.";
            }

            if (decision.ActionKind == InputActionKind.TileInteract)
            {
                return "AutoStationBuff interacts with " + (string.IsNullOrWhiteSpace(decision.BuffName) ? "station furniture" : decision.BuffName) + ".";
            }

            if (decision.ActionKind == InputActionKind.BuffPotionDirectUse)
            {
                return decision.ImmediateReconcile
                    ? "Immediate AutoBuff uses " + (string.IsNullOrWhiteSpace(decision.ItemName) ? "whitelisted potion" : decision.ItemName) + "."
                    : "AutoBuff uses " + (string.IsNullOrWhiteSpace(decision.ItemName) ? "whitelisted potion" : decision.ItemName) + ".";
            }

            return decision.Mode + " submits " + decision.ActionKind + ".";
        }

        private static void ApplyHealCooldownState(AutoRecoveryDecision decision)
        {
            ReadRecoveryCooldownFields(decision);
            if (decision != null && decision.PotionDelay > 0)
            {
                decision.CooldownBlocked = true;
                decision.PotionSicknessBlocked = true;
                decision.TriggerReason = decision.TriggerReason + ":potionDelay";
            }
        }

        private static void ApplyManaCooldownState(AutoRecoveryDecision decision)
        {
            ReadRecoveryCooldownFields(decision);
            if (decision != null && decision.ManaSicknessBlocked)
            {
                decision.TriggerReason = decision.TriggerReason + ":manaSicknessPresent";
                decision.ManaSicknessBlocked = false;
            }
        }

        private static void ReadRecoveryCooldownFields(AutoRecoveryDecision decision)
        {
            object player;
            if (decision == null || !TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return;
            }

            int potionDelay;
            bool manaSickness;
            int manaSickTime;
            if (!TerrariaInputCompat.TryReadRecoveryCooldowns(player, out potionDelay, out manaSickness, out manaSickTime))
            {
                return;
            }

            decision.PotionDelay = potionDelay;
            decision.ManaSickTime = manaSickTime;
            decision.ManaSicknessBlocked = manaSickness || manaSickTime > 0;
        }

        private static string GetCapabilityFromResult(InputActionResult result)
        {
            if (result == null)
            {
                return "UnknownUntilAttempted";
            }

            return string.Equals(result.ResultCode, DiagnosticResultCode.NotImplemented.ToString(), StringComparison.OrdinalIgnoreCase)
                ? "Unavailable:" + (result.Message ?? string.Empty)
                : "AvailableOrAttempted";
        }

    }
}
