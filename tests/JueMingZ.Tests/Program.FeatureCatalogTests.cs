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
        private static void FeatureCatalogExposesAutoDiscard()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.InventoryAutoDiscard, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto discard feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto discard must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow)
            {
                throw new InvalidOperationException("Auto discard must use list config UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.InventoryAndItems ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto discard must stay InventoryAndItems code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto discard must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesQuickReforge()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.NpcAutoReforge, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected quick reforge feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Quick reforge must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.ListConfigWindow)
            {
                throw new InvalidOperationException("Quick reforge must use list config UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.NpcServices ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Quick reforge must stay NpcServices code-domain and Misc UI category.");
            }
        }

        private static void FeatureCatalogExposesAutoTaxCollect()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.NpcAutoTaxCollect, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto tax collect feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto tax collect must be visible and implemented.");
            }

            if (feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Auto tax collect must default to disabled.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.NpcServices ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto tax collect must stay NpcServices code-domain and Misc UI category.");
            }

            if (feature.RequiredActions.Count != 1 || feature.RequiredActions[0] != InputActionKind.NpcInteract)
            {
                throw new InvalidOperationException("Auto tax collect must require only NpcInteract.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.None)
            {
                throw new InvalidOperationException("Auto tax collect must use a simple inline switch without a config window.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto tax collect must use original-action multiplayer metadata.");
            }

            AssertStringEquals(feature.DisplayName, "自动收税", "auto tax collect display name");
            AssertStringEquals(feature.Description, "靠近税收官且有可领取税款时自动领取", "auto tax collect description");
        }

        private static void FeatureCatalogExposesAutoMining()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoMining, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto mining feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto mining must be visible and implemented.");
            }

            if (feature.ConfigUiKind != FeatureConfigUiKind.InlineHotkey)
            {
                throw new InvalidOperationException("Auto mining must use inline hotkey UI metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto mining must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto mining must use original-action multiplayer metadata.");
            }

            var hasRawInput = false;
            for (var index = 0; index < feature.RequiredActions.Count; index++)
            {
                if (feature.RequiredActions[index] == InputActionKind.RawInput)
                {
                    hasRawInput = true;
                    break;
                }
            }

            if (!hasRawInput)
            {
                throw new InvalidOperationException("Auto mining must declare RawInput after sustained use migration.");
            }
        }

        private static void FeatureCatalogExposesAutoCaptureCritter()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoCaptureCritter, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto capture critter feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto capture critter must be visible and implemented.");
            }

            if (!string.Equals(feature.DisplayName, "自动捕捉", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto capture critter display name must be 自动捕捉.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto capture critter must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto capture critter must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesAutoHarvest()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.WorldAutomationAutoHarvest, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected auto harvest feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Auto harvest must be visible and implemented.");
            }

            if (!string.Equals(feature.DisplayName, "自动收获", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto harvest display name must be 自动收获.");
            }

            if (feature.HasHotkey || feature.HotkeyListVisible)
            {
                throw new InvalidOperationException("Auto harvest should be a standard switch, not a hotkey feature.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Auto harvest must stay WorldAutomation code-domain and Misc UI category.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Auto harvest must use original-action multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesTravelMenu()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition travelMenu;
            if (!registry.TryGet(FeatureIds.WorldAutomationTravelMenu, out travelMenu) || travelMenu == null)
            {
                throw new InvalidOperationException("Expected misc travel menu feature to be registered.");
            }

            if (!travelMenu.VisibleInMainUi)
            {
                throw new InvalidOperationException("Travel menu should be visible in the main UI after resuming the feature.");
            }

            if (!travelMenu.IsImplemented)
            {
                throw new InvalidOperationException("Travel menu should be marked implemented after resuming the feature.");
            }

            if (travelMenu.LifecycleStatus != FeatureLifecycleStatus.Implemented)
            {
                throw new InvalidOperationException("Expected implemented lifecycle for travel menu.");
            }

            if (travelMenu.MultiplayerSupport != FeatureMultiplayerSupport.SinglePlayerFallbackOnly)
            {
                throw new InvalidOperationException("Expected travel menu to remain single-player-only metadata.");
            }

            if (travelMenu.CodeDomain != FeatureCodeDomain.WorldAutomation ||
                travelMenu.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Travel menu must stay WorldAutomation code-domain and Misc UI category.");
            }
        }

        private static void TravelMenuRuntimePathIsResumed()
        {
            if (TravelMenuService.IsSuspended)
            {
                throw new InvalidOperationException("Travel menu should not remain suspended after the CreativeUI input bypass fix.");
            }

            var result = TravelMenuService.SetEnabledFromUi(true, true);
            if (string.Equals(result.ResultCode, TravelMenuService.SuspendedResultCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Travel menu enable path should no longer return suspended.");
            }

            var diagnostics = TravelMenuService.GetDiagnostics();
            if (diagnostics.Enabled ||
                diagnostics.SessionActive)
            {
                throw new InvalidOperationException("Failed enable attempt in tests should not leave an active travel menu session.");
            }
        }

        private static void FeatureCatalogExposesImplementedMiscInventoryAutomation()
        {
            var registry = FeatureRegistry.CreateDefault();
            AssertImplementedFeatureVisible(registry, "inventory.continuous_bag_open");
            AssertImplementedFeatureVisible(registry, "inventory.auto_deposit_coins");
            AssertImplementedFeatureVisible(registry, "inventory.auto_extractinator");
            AssertImplementedFeatureVisible(registry, "inventory.keep_favorited");
        }

        private static void FeatureCatalogExposesGoblinExecution()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.CombatGoblinExecution, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected goblin execution feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Goblin execution must be visible and implemented.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.Combat ||
                feature.UserCategory != FeatureUserCategory.Combat)
            {
                throw new InvalidOperationException("Goblin execution must stay Combat code-domain and Combat UI category.");
            }

            if (feature.RequiredActions.Count != 1 || feature.RequiredActions[0] != InputActionKind.None)
            {
                throw new InvalidOperationException("Goblin execution must not require ActionQueue actions.");
            }

            if (feature.MultiplayerSupport != FeatureMultiplayerSupport.SupportedByOriginalAction)
            {
                throw new InvalidOperationException("Goblin execution must use original hit-path multiplayer metadata.");
            }
        }

        private static void FeatureCatalogExposesPhasebladeQuickSwitchConfig()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.CombatPhasebladeQuickSwitch, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected phaseblade quick switch feature to be registered.");
            }

            if (!feature.VisibleInMainUi || !feature.IsImplemented)
            {
                throw new InvalidOperationException("Phaseblade quick switch config must be visible in the combat UI.");
            }

            if (feature.DefaultEnabled)
            {
                throw new InvalidOperationException("Phaseblade quick switch must default to disabled.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.Combat ||
                feature.UserCategory != FeatureUserCategory.Combat)
            {
                throw new InvalidOperationException("Phaseblade quick switch must stay Combat code-domain and Combat UI category.");
            }

            if (!HasRequiredAction(feature, InputActionKind.ItemUse) ||
                !HasRequiredAction(feature, InputActionKind.SelectHotbarSlot) ||
                !HasRequiredAction(feature, InputActionKind.RawInput))
            {
                throw new InvalidOperationException("Phaseblade quick switch must declare item use, hotbar selection, and raw input requirements.");
            }

            AssertStringEquals(feature.Description, "按住右键快切快捷栏的光剑", "phaseblade quick switch description");
        }

        private static void AssertPlannedFeatureHidden(FeatureRegistry registry, string featureId)
        {
            FeatureDefinition feature;
            if (!registry.TryGet(featureId, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected planned feature to be registered: " + featureId);
            }

            if (feature.IsImplemented)
            {
                throw new InvalidOperationException("Expected feature to be planned, not implemented: " + featureId);
            }

            if (feature.VisibleInMainUi)
            {
                throw new InvalidOperationException("Planned feature must not be visible in main UI: " + featureId);
            }

            if (feature.HasHotkey || feature.HotkeyListVisible)
            {
                throw new InvalidOperationException("Planned feature must not expose hotkey UI: " + featureId);
            }

            if (feature.LifecycleStatus != FeatureLifecycleStatus.Planned)
            {
                throw new InvalidOperationException("Expected planned lifecycle for: " + featureId);
            }
        }

        private static void AssertImplementedFeatureVisible(FeatureRegistry registry, string featureId)
        {
            FeatureDefinition feature;
            if (!registry.TryGet(featureId, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected implemented feature to be registered: " + featureId);
            }

            if (!feature.IsImplemented)
            {
                throw new InvalidOperationException("Expected feature to be implemented: " + featureId);
            }

            if (!feature.VisibleInMainUi)
            {
                throw new InvalidOperationException("Implemented feature must be visible in main UI: " + featureId);
            }

            if (feature.LifecycleStatus != FeatureLifecycleStatus.Implemented)
            {
                throw new InvalidOperationException("Expected implemented lifecycle for implemented feature: " + featureId);
            }
        }

        private static bool HasRequiredAction(FeatureDefinition feature, InputActionKind kind)
        {
            if (feature == null || feature.RequiredActions == null)
            {
                return false;
            }

            for (var index = 0; index < feature.RequiredActions.Count; index++)
            {
                if (feature.RequiredActions[index] == kind)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
