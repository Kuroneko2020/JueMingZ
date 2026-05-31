using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.GameState;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public static class BuffPotionStrategyService
    {
        public static bool TryCreateAutoBuffRequest(GameStateSnapshot snapshot, long tick, int cooldownTicks, out InputActionRequest request, out string message)
        {
            request = null;
            message = string.Empty;

            if (BuffPotionWhitelistService.Count <= 0)
            {
                message = "WaitingForWhitelist";
                return false;
            }

            var scan = BuffPotionCatalog.RefreshCandidates();
            if (!scan.PlayerAvailable)
            {
                message = string.IsNullOrWhiteSpace(scan.Error) ? "LocalPlayerUnavailable" : scan.Error;
                return false;
            }

            BuffPotionCandidate selected = null;
            for (var index = 0; index < scan.Candidates.Count; index++)
            {
                var candidate = scan.Candidates[index];
                if (candidate == null || !candidate.IsWhitelisted)
                {
                    continue;
                }

                if (!candidate.CanApply)
                {
                    continue;
                }

                selected = candidate;
                break;
            }

            if (selected == null)
            {
                message = "NoWhitelistedBuffPotionNeedsUse";
                return false;
            }

            request = new InputActionRequest
            {
                Kind = InputActionKind.BuffPotionDirectUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = "buff.auto_buff",
                Description = "AutoBuff controlled buff potion use",
                Timeout = TimeSpan.FromSeconds(3)
            };

            request.Metadata["Scenario"] = "AutoRecovery.AutoBuff";
            request.Metadata["SourceKind"] = "Automation";
            request.Metadata["SourceUi"] = string.Empty;
            request.Metadata["ButtonId"] = string.Empty;
            request.Metadata["ButtonLabel"] = string.Empty;
            request.Metadata["SourceHotkey"] = string.Empty;
            request.Metadata["ExecutionMode"] = ActionExecutionModes.DirectLocalBuffPotion;
            request.Metadata["SourceContainer"] = selected.SourceContainer;
            request.Metadata["SourceSlot"] = selected.SourceSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ItemType"] = selected.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ItemName"] = selected.ItemName ?? string.Empty;
            request.Metadata["BuffType"] = selected.BuffType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BuffName"] = selected.BuffName ?? string.Empty;
            request.Metadata["BuffTime"] = selected.BuffTime.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SelectedCandidateIndex"] = FindCandidateIndex(scan, selected).ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoRecoveryMode"] = "AutoBuff";
            request.Metadata["AutoRecoveryEnabled"] = "true";
            request.Metadata["CooldownTicks"] = cooldownTicks.ToString(CultureInfo.InvariantCulture);
            request.Metadata["TriggerReason"] = "whitelistedBuffMissing";
            request.Metadata["BuffCountBefore"] = snapshot == null || snapshot.ActiveBuffs == null
                ? "0"
                : snapshot.ActiveBuffs.Count.ToString(CultureInfo.InvariantCulture);
            message = "Queued controlled buff potion use for " + selected.ItemName + ".";
            return true;
        }

        private static int FindCandidateIndex(BuffPotionScanResult scan, BuffPotionCandidate selected)
        {
            if (scan == null || selected == null || scan.Candidates == null)
            {
                return -1;
            }

            for (var index = 0; index < scan.Candidates.Count; index++)
            {
                var candidate = scan.Candidates[index];
                if (candidate != null &&
                    candidate.SourceSlot == selected.SourceSlot &&
                    candidate.ItemType == selected.ItemType &&
                    string.Equals(candidate.SourceContainer, selected.SourceContainer, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
