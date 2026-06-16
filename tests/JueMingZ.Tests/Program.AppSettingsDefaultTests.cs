using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FirstRunAppSettingsDefaultsMatchRequestedUiBaseline()
        {
            var settings = AppSettings.CreateDefault();

            AssertDefault(!settings.MiscQuickItemHotkeysEnabled, "misc quick item hotkeys off");
            AssertDefault(!settings.MiscAutoStackEnabled, "misc auto stack off");
            AssertDefault(!settings.MiscAutoSellEnabled, "misc auto sell off");
            AssertDefault(!settings.MiscAutoDiscardEnabled, "misc auto discard off");
            AssertDefault(!settings.MiscQuickReforgeEnabled, "misc quick reforge off");
            AssertDefault(!settings.MiscAutoTaxCollectEnabled, "misc auto tax collect off");
            AssertDefault(!settings.WorldAutomationAutoMiningEnabled, "misc auto mining off");
            AssertDefault(!settings.MiscAutoCaptureCritterEnabled, "misc auto capture critter off");
            AssertStringEquals(settings.WorldAutomationAutoCaptureCritterMode, AutoCaptureCritterModes.Off, "misc auto capture critter mode off");
            AssertDefault(settings.MiscAutoCaptureCritterCategoryDefaultsMigrated, "misc auto capture critter category defaults migrated");
            AssertDefault(AutoCaptureCritterCategoryCatalog.CountDisabled(settings) == 0, "misc auto capture critter categories on");
            AssertDefault(!settings.MiscAutoHarvestEnabled, "misc auto harvest off");
            AssertDefault(!settings.MiscQuickBagOpenEnabled, "misc quick bag open off");
            AssertDefault(!settings.MiscAutoDepositCoinsEnabled, "misc auto deposit coins off");
            AssertDefault(!settings.MiscAutoExtractinatorEnabled, "misc auto extractinator off");
            AssertDefault(!settings.MiscKeepFavoritedEnabled, "misc keep favorited off");
            AssertDefault(settings.DiagnosticsWorldGenDebugViewerEnabled, "worldgen debug viewer available");
            AssertDefault(settings.DiagnosticsDeveloperDebugCommandsEnabled, "developer debug commands available");

            AssertDefault(!settings.FishingAutoFishEnabled, "fishing auto fish off");
            AssertDefault(!settings.FishingAutoLoadoutEnabled, "fishing auto loadout off");
            AssertDefault(!settings.FishingAutoEquipmentEnabled, "fishing auto equipment off");
            AssertDefault(!settings.FishingAutoStoreQuestFishEnabled, "fishing quest fish store off");
            AssertStringEquals(FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled), FishingAutoStoreModes.Off, "fishing store mode");
            AssertDefault(!settings.FishingFilterCutRodSkipEnabled, "fishing cut rod skip off");
            AssertStringEquals(FishingFilterModes.Normalize(settings.FishingFilterMode), FishingFilterModes.Disabled, "fishing filter mode");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule), FishingFilterSpecialRuleModes.Allow, "fishing crate rule");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule), FishingFilterSpecialRuleModes.Deny, "fishing enemy rule");
            AssertStringEquals(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule), FishingFilterSpecialRuleModes.Allow, "fishing quest fish rule");

            AssertDefault(settings.CombatAimAssistRadius == 0, "combat aim radius zero");
            AssertDefault(!settings.CombatAimTrackDummyEnabled, "combat aim track dummy off");
            AssertDefault(settings.CombatAimMarkerEnabled, "combat aim marker on");
            AssertDefault(!settings.CombatAutoClickerEnabled, "combat auto clicker off");
            AssertDefault(!settings.CombatPhasebladeQuickSwitchEnabled, "combat phaseblade quick switch off");
            AssertDefault(settings.CombatPhasebladeQuickSwitchIntervalTicks == CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks, "combat phaseblade quick switch default interval");
            AssertDefault(!settings.CombatPerfectRevolverEnabled, "combat perfect revolver off");
            AssertDefault(!settings.CombatMagicStringClickerEnabled, "combat magic string clicker off");
            AssertDefault(!settings.CombatAutoFacingEnabled, "combat auto facing off");
            AssertDefault(!settings.CombatEquipmentWarningEnabled, "combat equipment warning off");
            AssertDefault(!settings.CombatAutoBossDamageReportEnabled, "combat auto boss damage report off");
            AssertDefault(!settings.CombatGoblinExecutionEnabled, "combat goblin execution off");
            AssertStringEquals(CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority), CombatAimModes.TargetPriorityNearest, "combat aim target priority");
            AssertStringEquals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, "combat aim range origin");

            AssertDefault(!settings.InformationEnemyNameLabelsEnabled, "information enemy labels off");
            AssertDefault(!settings.InformationCritterNameLabelsEnabled, "information critter labels off");
            AssertStringEquals(settings.InformationNpcNameLabelsMode, "Off", "information npc labels off");
            AssertStringEquals(settings.InformationChestNameLabelsMode, "Off", "information chest labels off");
            AssertStringEquals(settings.InformationSignTextLabelsMode, InformationSignTextModes.Off, "information sign text labels off");
            AssertStringEquals(settings.InformationTombstoneTextLabelsMode, InformationSignTextModes.Off, "information tombstone text labels off");
            AssertDefault(!settings.InformationHighlightLifeCrystalEnabled, "information life crystal off");
            AssertDefault(!settings.InformationHighlightManaCrystalEnabled, "information mana crystal off");
            AssertDefault(!settings.InformationHighlightDigtoiseEnabled, "information digtoise off");
            AssertDefault(!settings.InformationHighlightLifeFruitEnabled, "information life fruit off");
            AssertDefault(!settings.InformationHighlightDragonEggEnabled, "information dragon egg off");
            AssertDefault(!settings.InformationBiomeDisplayEnabled, "information biome off");
            AssertDefault(!settings.InformationWorldInfectionEnabled, "information infection off");
            AssertDefault(!settings.InformationLuckValueEnabled, "information luck off");
            AssertDefault(!settings.InformationFishingCatchesEnabled, "information fishing catches off");
            AssertDefault(!settings.InformationFishingFilteredCatchesEnabled, "information filtered catches off");
            AssertDefault(!settings.InformationAnglerQuestEnabled, "information angler quest off");

            AssertDefault(!settings.AutoHealEnabled, "auto heal off");
            AssertDefault(!settings.AutoManaEnabled, "auto mana off");
            AssertDefault(!settings.AutoBuffEnabled, "auto buff off");
            AssertDefault(!settings.AutoNurseEnabled, "auto nurse off");
            AssertDefault(!settings.AutoStationBuffEnabled, "auto station buff off");
            AssertDefault(!settings.AutoBuffFollowAddEnabled, "auto buff follow add off");
            AssertDefault(!settings.AutoBuffFollowRemoveEnabled, "auto buff follow remove off");
            AssertStringEquals(settings.AutoHealMode, "Off", "auto heal mode");
            AssertStringEquals(settings.AutoManaMode, "Off", "auto mana mode");

            AssertDefault(!settings.MovementSimulatedMultiJumpEnabled, "movement simulated multi jump off");
            AssertDefault(!settings.MovementContinuousDashEnabled, "movement continuous dash off");
            AssertDefault(!settings.MovementTeleportCorrectionEnabled, "movement teleport correction off");
            AssertDefault(!settings.MovementSafeLandingEnabled, "movement safe landing off");
        }

        private static void AutoCaptureCritterModeAliasesPreserveLegacyBool()
        {
            var settings = AppSettings.CreateDefault();

            settings.MiscAutoCaptureCritterEnabled = true;
            settings.MiscAutoCaptureCritterMode = null;
            AssertStringEquals(settings.WorldAutomationAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, "legacy enabled auto capture mode");
            AssertDefault(settings.WorldAutomationAutoCaptureCritterEnabled, "legacy enabled auto capture feature");

            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Manual;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Manual, "manual mode storage");
            AssertDefault(settings.MiscAutoCaptureCritterEnabled, "manual mode enabled bool");

            settings.WorldAutomationAutoCaptureCritterEnabled = false;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Off, "disabled mode storage");
            AssertDefault(!settings.MiscAutoCaptureCritterEnabled, "disabled mode legacy bool");

            settings.WorldAutomationAutoCaptureCritterEnabled = true;
            AssertStringEquals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, "enabled bool maps to auto mode");
        }

        private static void AppSettingsCodeDomainAliasesPreserveMiscStorage()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = true;
            settings.InventoryAutoSellEnabled = true;
            settings.InventoryAutoDiscardEnabled = true;
            settings.InventoryQuickItemHotkeysEnabled = true;
            settings.InventoryAutoDepositCoinsEnabled = true;
            settings.NpcAutoReforgeEnabled = true;
            settings.NpcAutoTaxCollectEnabled = true;
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Auto;
            settings.WorldAutomationAutoCaptureCritterEnabled = true;
            settings.WorldAutomationTravelMenuEnabled = true;
            settings.DiagnosticsDeveloperDebugCommandsEnabled = true;
            settings.DiagnosticsWorldGenDebugViewerEnabled = true;

            if (!settings.MiscAutoStackEnabled ||
                !settings.MiscAutoSellEnabled ||
                !settings.MiscAutoDiscardEnabled ||
                !settings.MiscQuickItemHotkeysEnabled ||
                !settings.MiscAutoDepositCoinsEnabled ||
                !settings.MiscQuickReforgeEnabled ||
                !settings.MiscAutoTaxCollectEnabled ||
                !settings.WorldAutomationAutoMiningEnabled ||
                !string.Equals(settings.MiscAutoMiningMode, AutoMiningModes.Auto, StringComparison.Ordinal) ||
                !settings.MiscAutoCaptureCritterEnabled ||
                !string.Equals(settings.MiscAutoCaptureCritterMode, AutoCaptureCritterModes.Auto, StringComparison.Ordinal) ||
                !settings.MiscTravelMenuEnabled ||
                !settings.MiscDeveloperDebugCommandsEnabled ||
                !settings.MiscWorldGenDebugViewerEnabled ||
                !settings.DiagnosticsWorldGenDebugViewerEnabled)
            {
                throw new InvalidOperationException("Code-domain aliases must write through existing Misc storage fields.");
            }

            settings.MiscAutoSellItemIds = new List<int> { 2337 };
            settings.MiscAutoDiscardItemIds = new List<int> { 12 };
            settings.MiscQuickReforgePrefixes = new List<string> { "虚幻" };

            if (settings.InventoryAutoSellItemIds.Count != 1 ||
                settings.InventoryAutoSellItemIds[0] != 2337 ||
                settings.InventoryAutoDiscardItemIds.Count != 1 ||
                settings.InventoryAutoDiscardItemIds[0] != 12 ||
                settings.NpcAutoReforgePrefixes.Count != 1 ||
                !string.Equals(settings.NpcAutoReforgePrefixes[0], "虚幻", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Code-domain list aliases must read existing Misc storage lists.");
            }
        }


    }
}
