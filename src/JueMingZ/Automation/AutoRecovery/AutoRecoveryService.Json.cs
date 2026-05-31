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
        private static string BuildBuffTypesJson(IReadOnlyList<BuffSnapshot> buffs)
        {
            if (buffs == null || buffs.Count == 0)
            {
                return "[]";
            }

            var builder = new StringBuilder();
            builder.Append("[");
            var first = true;
            for (var index = 0; index < buffs.Count; index++)
            {
                var buff = buffs[index];
                if (buff == null || buff.BuffType <= 0)
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(",");
                }

                builder.Append(buff.BuffType.ToString(CultureInfo.InvariantCulture));
                first = false;
            }

            builder.Append("]");
            return builder.ToString();
        }

        private static string BuildDecisionBeforeJson(AutoRecoveryDecision decision, bool cooldownBlocked)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "enabled", "true", true);
            AppendString(builder, "autoHealMode", decision.AutoHealMode, true);
            AppendString(builder, "autoManaMode", decision.AutoManaMode, true);
            AppendRaw(builder, "thresholdPercent", decision.ThresholdPercent <= 0 ? "null" : decision.ThresholdPercent.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "currentLife", decision.CurrentLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "maxLife", decision.MaxLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "missingLife", decision.MissingLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "lifePercent", decision.LifePercent.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "currentMana", decision.CurrentMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "maxMana", decision.MaxMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "missingMana", decision.MissingMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaPercent", decision.ManaPercent.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedItemType", decision.SelectedItemType.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "selectedItemName", decision.SelectedItemName, true);
            AppendRaw(builder, "selectedItemManaCost", decision.SelectedItemManaCost.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "requiredMana", decision.RequiredMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "checkManaAvailable", decision.CheckManaAvailable ? "true" : "false", true);
            AppendRaw(builder, "checkManaResult", decision.CheckManaResult ? "true" : "false", true);
            AppendRaw(builder, "usedFallbackManaCostCheck", decision.UsedFallbackManaCostCheck ? "true" : "false", true);
            AppendString(builder, "manaCheckReason", decision.ManaCheckReason, true);
            AppendString(builder, "triggerReason", decision.TriggerReason, true);
            AppendRaw(builder, "cooldownBlocked", cooldownBlocked ? "true" : "false", true);
            AppendRaw(builder, "potionSicknessBlocked", decision.PotionSicknessBlocked ? "true" : "false", true);
            AppendRaw(builder, "manaSicknessBlocked", decision.ManaSicknessBlocked ? "true" : "false", true);
            AppendRaw(builder, "autoRecoveryCooldownBlocked", decision.AutoRecoveryCooldownBlocked ? "true" : "false", true);
            AppendRaw(builder, "immediateReconcileTriggered", decision.ImmediateReconcile ? "true" : "false", true);
            AppendString(builder, "immediateTriggerReason", decision.ImmediateTriggerReason, true);
            AppendRaw(builder, "playerUsingItemBlocked", decision.PlayerUsingItemBlocked ? "true" : "false", true);
            AppendRaw(builder, "potionDelay", decision.PotionDelay.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaSickTime", decision.ManaSickTime.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "sourceContainer", decision.SourceContainer, true);
            AppendRaw(builder, "sourceSlot", decision.SourceSlot.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemType", decision.ItemType.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "itemName", decision.ItemName, true);
            AppendRaw(builder, "buffType", decision.BuffType.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "buffName", decision.BuffName, true);
            AppendRaw(builder, "buffTime", decision.BuffTime.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "lifeBefore", decision.CurrentLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaBefore", decision.CurrentMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffCountBefore", decision.BuffCountBefore.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffTypesBefore", string.IsNullOrWhiteSpace(decision.BuffTypesBeforeJson) ? "[]" : decision.BuffTypesBeforeJson, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildDecisionAfterJson(AutoRecoveryDecision decision, bool attempted, string resultCode, string message)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "quickHealAttempted", decision.ActionKind == InputActionKind.QuickHeal && attempted ? "true" : "false", true);
            AppendRaw(builder, "quickManaAttempted", decision.ActionKind == InputActionKind.QuickMana && attempted ? "true" : "false", true);
            AppendRaw(builder, "quickBuffAttempted", decision.ActionKind == InputActionKind.QuickBuff && attempted ? "true" : "false", true);
            AppendRaw(builder, "buffPotionDirectUseAttempted", decision.ActionKind == InputActionKind.BuffPotionDirectUse && attempted ? "true" : "false", true);
            AppendRaw(builder, "smartHealAttempted", string.Equals(decision.AutoHealMode, AutoRecoverySettings.HealModeSmart, StringComparison.OrdinalIgnoreCase) && decision.ActionKind == InputActionKind.QuickHeal && attempted ? "true" : "false", true);
            AppendRaw(builder, "manaFlowerLogicAttempted", decision.ActionKind == InputActionKind.QuickMana && attempted ? "true" : "false", true);
            AppendString(builder, "quickHealResultCode", decision.ActionKind == InputActionKind.QuickHeal ? resultCode : string.Empty, true);
            AppendString(builder, "quickManaResultCode", decision.ActionKind == InputActionKind.QuickMana ? resultCode : string.Empty, true);
            AppendString(builder, "quickBuffResultCode", decision.ActionKind == InputActionKind.QuickBuff ? resultCode : string.Empty, true);
            AppendString(builder, "buffPotionDirectUseResultCode", decision.ActionKind == InputActionKind.BuffPotionDirectUse ? resultCode : string.Empty, true);
            AppendRaw(builder, "lifeAfter", decision.CurrentLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaAfter", decision.CurrentMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffCountAfter", decision.BuffCountBefore.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemStackChangeObservable", "false", true);
            AppendRaw(builder, "inventoryChangeObservable", "false", true);
            AppendRaw(builder, "observableChange", "false", true);
            AppendString(builder, "message", message, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static int ExtractInt(string text, int fallback)
        {
            return fallback;
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
