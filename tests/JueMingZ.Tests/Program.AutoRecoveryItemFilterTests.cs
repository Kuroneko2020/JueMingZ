using System;
using System.Collections.Generic;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;
using JueMingZ.Features;
using JueMingZ.Runtime;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void AutoRecoveryItemFilterDefaultsAllowAllAndTogglesBlocked()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.AutoHealBlockedItemTypes == null ||
                settings.AutoManaBlockedItemTypes == null ||
                settings.AutoHealBlockedItemTypes.Count != 0 ||
                settings.AutoManaBlockedItemTypes.Count != 0)
            {
                throw new InvalidOperationException("Default recovery item blocked lists must be empty.");
            }

            if (!AutoRecoveryItemFilter.IsHealItemEnabled(settings, 28) ||
                !AutoRecoveryItemFilter.IsManaItemEnabled(settings, 110))
            {
                throw new InvalidOperationException("Default recovery item filter must allow all item types.");
            }

            bool enabled;
            var changed = AutoRecoveryItemFilter.ToggleHealItem(settings, 28, out enabled);
            if (!changed || enabled || AutoRecoveryItemFilter.IsHealItemEnabled(settings, 28) ||
                AutoRecoveryItemFilter.CountBlockedHealItems(settings) != 1)
            {
                throw new InvalidOperationException("Toggling a heal item once must disable that item type.");
            }

            settings.AutoHealBlockedItemTypes.Add(28);
            changed = AutoRecoveryItemFilter.ToggleHealItem(settings, 28, out enabled);
            if (!changed || !enabled || !AutoRecoveryItemFilter.IsHealItemEnabled(settings, 28) ||
                AutoRecoveryItemFilter.CountBlockedHealItems(settings) != 0)
            {
                throw new InvalidOperationException("Toggling a blocked heal item must re-enable it and clear duplicate blocks.");
            }

            changed = AutoRecoveryItemFilter.ToggleManaItem(settings, 110, out enabled);
            if (!changed || enabled || AutoRecoveryItemFilter.IsManaItemEnabled(settings, 110) ||
                AutoRecoveryItemFilter.CountBlockedManaItems(settings) != 1)
            {
                throw new InvalidOperationException("Toggling a mana item once must disable that item type.");
            }
        }

        private static void RecoveryPotionSelectionSkipsBlockedHealCandidates()
        {
            var candidates = new List<RecoveryPotionCandidate>
            {
                RecoveryCandidate(1, "Lesser", 0, 50, 0, true),
                RecoveryCandidate(2, "Greater", 1, 150, 0, true),
                RecoveryCandidate(3, "Medium", 2, 120, 0, true)
            };

            RecoveryPotionCandidate selected;
            string message;
            if (!RecoveryPotionCatalog.TrySelectHealPotionFromCandidates("Quick", 100, 400, candidates, null, out selected, out message) ||
                selected == null ||
                selected.ItemType != 2)
            {
                throw new InvalidOperationException("Unfiltered quick heal selection must keep the original highest-heal priority.");
            }

            if (!RecoveryPotionCatalog.TrySelectHealPotionFromCandidates("Quick", 100, 400, candidates, new HashSet<int> { 2 }, out selected, out message) ||
                selected == null ||
                selected.ItemType != 3)
            {
                throw new InvalidOperationException("Blocked heal item types must be skipped before original priority selection.");
            }

            if (RecoveryPotionCatalog.TrySelectHealPotionFromCandidates("Quick", 100, 400, candidates, new HashSet<int> { 1, 2, 3 }, out selected, out message) ||
                !string.Equals(message, "AllHealPotionsDisabledByConfig", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("All blocked heal candidates must report the config-disabled message.");
            }
        }

        private static void RecoveryPotionSelectionSkipsBlockedManaCandidates()
        {
            var candidates = new List<RecoveryPotionCandidate>
            {
                RecoveryCandidate(10, "Lesser Mana", 0, 0, 50, false),
                RecoveryCandidate(11, "Super Mana", 1, 0, 300, false),
                RecoveryCandidate(12, "Greater Mana", 2, 0, 200, false)
            };

            RecoveryPotionCandidate selected;
            string message;
            if (!RecoveryPotionCatalog.TrySelectManaPotionFromCandidates(candidates, null, out selected, out message) ||
                selected == null ||
                selected.ItemType != 11)
            {
                throw new InvalidOperationException("Unfiltered mana selection must keep the original highest-mana priority.");
            }

            if (!RecoveryPotionCatalog.TrySelectManaPotionFromCandidates(candidates, new HashSet<int> { 11 }, out selected, out message) ||
                selected == null ||
                selected.ItemType != 12)
            {
                throw new InvalidOperationException("Blocked mana item types must be skipped before original priority selection.");
            }

            if (RecoveryPotionCatalog.TrySelectManaPotionFromCandidates(candidates, new HashSet<int> { 10, 11, 12 }, out selected, out message) ||
                !string.Equals(message, "AllManaPotionsDisabledByConfig", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("All blocked mana candidates must report the config-disabled message.");
            }
        }

        private static void RuntimeSettingsSnapshotCarriesRecoveryItemFilters()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoHealEnabled = true;
            settings.AutoHealMode = AutoRecoverySettings.HealModeSmart;
            settings.AutoManaEnabled = true;
            settings.AutoManaMode = AutoRecoverySettings.ManaModeManaFlower;
            settings.AutoHealBlockedItemTypes.Add(2);
            settings.AutoManaBlockedItemTypes.Add(11);

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.AutoRecovery == null ||
                snapshot.AutoRecovery.AutoHealBlockedItemTypes == null ||
                snapshot.AutoRecovery.AutoManaBlockedItemTypes == null ||
                !snapshot.AutoRecovery.AutoHealBlockedItemTypes.Contains(2) ||
                !snapshot.AutoRecovery.AutoManaBlockedItemTypes.Contains(11))
            {
                throw new InvalidOperationException("Runtime settings snapshot must carry recovery item filter sets.");
            }
        }

        private static void FeatureCatalogExposesRecoveryItemConfigWindows()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition autoHeal;
            FeatureDefinition autoMana;
            if (!registry.TryGet("buff.auto_heal", out autoHeal) ||
                !registry.TryGet("buff.auto_mana", out autoMana) ||
                autoHeal == null ||
                autoMana == null)
            {
                throw new InvalidOperationException("Expected auto heal and auto mana features to be registered.");
            }

            if (autoHeal.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow ||
                autoMana.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow)
            {
                throw new InvalidOperationException("Auto heal and auto mana must expose list config UI metadata.");
            }
        }

        private static RecoveryPotionCandidate RecoveryCandidate(int itemType, string name, int slot, int healLife, int healMana, bool potion)
        {
            return new RecoveryPotionCandidate
            {
                SourceContainer = "Inventory",
                SourceSlot = slot,
                ItemType = itemType,
                ItemName = name,
                Stack = 1,
                HealLife = healLife,
                HealMana = healMana,
                Potion = potion,
                Consumable = true
            };
        }
    }
}
