using System.Collections;
using System.Collections.Generic;
using JueMingZ.Compat;

namespace JueMingZ.Automation.BuffAndRecovery
{
    public static class RecoveryPotionCatalog
    {
        public static List<RecoveryPotionCandidate> ScanLocalPlayer()
        {
            var result = new List<RecoveryPotionCandidate>();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return result;
            }

            ScanContainer(player, "Inventory", result);
            if (InventoryMutationCompat.TryPlayerUsesVoidBag(player))
            {
                ScanContainer(player, "VoidBag", result);
            }

            return result;
        }

        public static bool TrySelectHealPotion(string mode, int missingLife, int maxLife, out RecoveryPotionCandidate selected, out string message)
        {
            return TrySelectHealPotion(mode, missingLife, maxLife, null, out selected, out message);
        }

        public static bool TrySelectHealPotion(string mode, int missingLife, int maxLife, HashSet<int> blockedItemTypes, out RecoveryPotionCandidate selected, out string message)
        {
            selected = null;
            message = string.Empty;
            if (missingLife <= 0)
            {
                message = "LifeNotMissing";
                return false;
            }

            var candidates = ScanLocalPlayer();
            return TrySelectHealPotionFromCandidates(mode, missingLife, maxLife, candidates, blockedItemTypes, out selected, out message);
        }

        internal static bool TrySelectHealPotionFromCandidates(string mode, int missingLife, int maxLife, IList<RecoveryPotionCandidate> candidates, HashSet<int> blockedItemTypes, out RecoveryPotionCandidate selected, out string message)
        {
            selected = null;
            message = string.Empty;
            if (missingLife <= 0)
            {
                message = "LifeNotMissing";
                return false;
            }

            var healCandidates = new List<RecoveryPotionCandidate>();
            var matchingCount = 0;
            var blockedCount = 0;
            for (var index = 0; candidates != null && index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null && candidate.Stack > 0 && candidate.ItemType > 0 && candidate.Potion && candidate.HealLife > 0)
                {
                    matchingCount++;
                    if (IsBlocked(blockedItemTypes, candidate.ItemType))
                    {
                        blockedCount++;
                        continue;
                    }

                    healCandidates.Add(candidate);
                }
            }

            if (healCandidates.Count <= 0)
            {
                message = matchingCount > 0 && blockedCount == matchingCount
                    ? "AllHealPotionsDisabledByConfig"
                    : "MissingHealPotion";
                return false;
            }

            if (string.Equals(mode, "Quick", System.StringComparison.OrdinalIgnoreCase))
            {
                selected = SelectHighestHeal(healCandidates);
                message = selected == null ? "MissingHealPotion" : "SelectedHighestHealPotion";
                return selected != null;
            }

            selected = SelectSmartHeal(healCandidates, missingLife, maxLife);
            if (selected != null)
            {
                message = "SelectedSmartHealPotion";
                return true;
            }

            message = "SmartHealWaitingForMissingLife";
            return false;
        }

        public static bool TrySelectManaPotion(out RecoveryPotionCandidate selected, out string message)
        {
            return TrySelectManaPotion(null, out selected, out message);
        }

        public static bool TrySelectManaPotion(HashSet<int> blockedItemTypes, out RecoveryPotionCandidate selected, out string message)
        {
            selected = null;
            message = string.Empty;
            var candidates = ScanLocalPlayer();
            return TrySelectManaPotionFromCandidates(candidates, blockedItemTypes, out selected, out message);
        }

        internal static bool TrySelectManaPotionFromCandidates(IList<RecoveryPotionCandidate> candidates, HashSet<int> blockedItemTypes, out RecoveryPotionCandidate selected, out string message)
        {
            selected = null;
            message = string.Empty;
            var matchingCount = 0;
            var blockedCount = 0;
            for (var index = 0; candidates != null && index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null || candidate.Stack <= 0 || candidate.ItemType <= 0 || candidate.HealMana <= 0)
                {
                    continue;
                }

                matchingCount++;
                if (IsBlocked(blockedItemTypes, candidate.ItemType))
                {
                    blockedCount++;
                    continue;
                }

                if (selected == null ||
                    candidate.HealMana > selected.HealMana ||
                    (candidate.HealMana == selected.HealMana && CompareSource(candidate, selected) < 0))
                {
                    selected = candidate;
                }
            }

            message = selected == null
                ? matchingCount > 0 && blockedCount == matchingCount
                    ? "AllManaPotionsDisabledByConfig"
                    : "MissingManaPotion"
                : "SelectedHighestManaPotion";
            return selected != null;
        }

        private static RecoveryPotionCandidate SelectHighestHeal(List<RecoveryPotionCandidate> candidates)
        {
            RecoveryPotionCandidate selected = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (selected == null ||
                    candidate.EffectiveHealMax > selected.EffectiveHealMax ||
                    (candidate.EffectiveHealMax == selected.EffectiveHealMax && CompareSource(candidate, selected) < 0))
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private static RecoveryPotionCandidate SelectSmartHeal(List<RecoveryPotionCandidate> candidates, int missingLife, int maxLife)
        {
            RecoveryPotionCandidate selected = null;
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                var canUseWithoutMeaningfulWaste =
                    candidate.SmartHealTriggerAmount <= missingLife ||
                    candidate.EffectiveHealMax > maxLife;
                if (!canUseWithoutMeaningfulWaste)
                {
                    continue;
                }

                if (selected == null ||
                    IsBetterSmartHealCandidate(candidate, selected, missingLife, maxLife))
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        private static bool IsBetterSmartHealCandidate(RecoveryPotionCandidate candidate, RecoveryPotionCandidate selected, int missingLife, int maxLife)
        {
            var candidateSuper = candidate.EffectiveHealMax > maxLife;
            var selectedSuper = selected.EffectiveHealMax > maxLife;
            if (candidateSuper != selectedSuper)
            {
                return !candidateSuper;
            }

            var candidateWaste = candidate.EffectiveHealMax > missingLife ? candidate.EffectiveHealMax - missingLife : 0;
            var selectedWaste = selected.EffectiveHealMax > missingLife ? selected.EffectiveHealMax - missingLife : 0;
            if (candidateWaste != selectedWaste)
            {
                return candidateWaste < selectedWaste;
            }

            if (candidate.EffectiveHealMax != selected.EffectiveHealMax)
            {
                return candidate.EffectiveHealMax > selected.EffectiveHealMax;
            }

            return CompareSource(candidate, selected) < 0;
        }

        private static void ScanContainer(object player, string sourceContainer, List<RecoveryPotionCandidate> result)
        {
            IList items;
            string message;
            if (!InventoryMutationCompat.TryGetContainerItems(player, sourceContainer, out items, out message) || items == null)
            {
                return;
            }

            for (var slot = 0; slot < items.Count; slot++)
            {
                int itemType;
                string itemName;
                int stack;
                int healLife;
                int healMana;
                bool potion;
                bool consumable;
                int buffType;
                int buffTime;
                if (!InventoryMutationCompat.TryReadRecoveryItemFields(
                        items[slot],
                        out itemType,
                        out itemName,
                        out stack,
                        out healLife,
                        out healMana,
                        out potion,
                        out consumable,
                        out buffType,
                        out buffTime) ||
                    itemType <= 0 ||
                    stack <= 0 ||
                    (healLife <= 0 && healMana <= 0))
                {
                    continue;
                }

                result.Add(new RecoveryPotionCandidate
                {
                    SourceContainer = sourceContainer,
                    SourceSlot = slot,
                    ItemType = itemType,
                    ItemName = itemName,
                    Stack = stack,
                    HealLife = healLife,
                    HealMana = healMana,
                    Potion = potion,
                    Consumable = consumable,
                    BuffType = buffType,
                    BuffTime = buffTime
                });
            }
        }

        private static int CompareSource(RecoveryPotionCandidate left, RecoveryPotionCandidate right)
        {
            var container = ContainerRank(left.SourceContainer).CompareTo(ContainerRank(right.SourceContainer));
            return container != 0 ? container : left.SourceSlot.CompareTo(right.SourceSlot);
        }

        private static int ContainerRank(string sourceContainer)
        {
            return string.Equals(sourceContainer, "Inventory", System.StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        }

        private static bool IsBlocked(HashSet<int> blockedItemTypes, int itemType)
        {
            return itemType > 0 && blockedItemTypes != null && blockedItemTypes.Count > 0 && blockedItemTypes.Contains(itemType);
        }
    }
}
