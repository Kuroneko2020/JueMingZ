using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Input
{
    internal static class FeatureToggleHotkeyService
    {
        private const string Scenario = "Hotkey.FeatureToggle";
        private const int VkAlt = 0x12;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        public static bool HasActiveBindings
        {
            get
            {
                return HasActiveUnifiedBindings(UnifiedHotkeyRuntimeService.GetBindingsSnapshot());
            }
        }

        public static void Tick(GameStateSnapshot gameState)
        {
            var result = TickCore(
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(),
                ConfigService.UnifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault(),
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(bindingId),
                true,
                true);
            if (result != null)
            {
                // Result is intentionally ignored here; diagnostics are event-level
                // and tests use TickForTesting to inspect decisions without I/O.
            }
        }

        internal static FeatureToggleHotkeyDispatchResult TickForTesting(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            IDictionary<int, bool> downKeys,
            bool gameInputAvailable,
            string gateReason)
        {
            var unifiedHotkeys = CreateUnifiedSettingsFromLegacyFeatureToggleSettings(hotkeySettings);
            var gate = CreateGateContextForTesting(gameInputAvailable, gateReason);
            var input = CreateUnifiedInputStateFromLegacyDownKeys(downKeys);
            var signature = unifiedHotkeys.CreateCacheSignature();
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                hotkeySettings ?? HotkeySettings.CreateDefault(),
                unifiedHotkeys,
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(unifiedHotkeys, signature, bindingId, gate, input),
                false,
                false);
        }

        internal static FeatureToggleHotkeyDispatchResult TickUnifiedForTesting(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            UnifiedHotkeySettings unifiedHotkeySettings,
            IDictionary<int, bool> downKeys,
            UnifiedHotkeyRuntimeGateContext gateContext)
        {
            unifiedHotkeySettings = unifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault();
            var signature = unifiedHotkeySettings.CreateCacheSignature();
            var input = UnifiedHotkeyRuntimeInputState.FromDictionary(downKeys);
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                hotkeySettings ?? HotkeySettings.CreateDefault(),
                unifiedHotkeySettings,
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(
                    unifiedHotkeySettings,
                    signature,
                    bindingId,
                    gateContext ?? new UnifiedHotkeyRuntimeGateContext(),
                    input),
                false,
                false);
        }

        internal static void ResetForTesting()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
        }

        private static FeatureToggleHotkeyDispatchResult TickCore(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            UnifiedHotkeySettings unifiedHotkeySettings,
            Func<string, UnifiedHotkeyRuntimeTriggerResult> queryBinding,
            bool saveConfig,
            bool recordDiagnostic)
        {
            if (queryBinding == null ||
                !HasActiveUnifiedFeatureToggleSettings(unifiedHotkeySettings))
            {
                return FeatureToggleHotkeyDispatchResult.NoOp;
            }

            foreach (var target in FeatureToggleHotkeyTargetCatalog.All)
            {
                if (target == null)
                {
                    continue;
                }

                var bindingId = UnifiedHotkeyBindingIds.ForFeatureToggleTarget(target.TargetId);
                var trigger = queryBinding(bindingId);
                if (!trigger.PressedEdge)
                {
                    continue;
                }

                FeatureToggleHotkeyDispatchResult result;
                if (string.Equals(trigger.ResultCode, "blocked", StringComparison.Ordinal))
                {
                    result = FeatureToggleHotkeyDispatchResult.Blocked(
                        target.TargetId,
                        target.DisplayName,
                        trigger.Display,
                        trigger.Reason,
                        UnifiedHotkeyReasonCatalog.IsUiGateReason(trigger.Reason)
                            ? DiagnosticResultCode.BlockedByUi
                            : DiagnosticResultCode.BlockedByEnvironment);
                }
                else if (!string.Equals(trigger.ResultCode, "triggered", StringComparison.Ordinal))
                {
                    continue;
                }
                else
                {
                    var apply = FeatureToggleProfile.Toggle(
                        appSettings,
                        hotkeySettings,
                        unifiedHotkeySettings,
                        target.TargetId,
                        saveConfig);
                    result = FeatureToggleHotkeyDispatchResult.FromApply(
                        target.TargetId,
                        target.DisplayName,
                        trigger.Display,
                        apply);
                    if (apply.Applied && saveConfig && !apply.ConfigSaved)
                    {
                        ConfigService.SaveAll();
                    }
                }

                if (recordDiagnostic)
                {
                    DiagnosticActionRecorder.RecordHotkeyEvent(
                        result.Chord,
                        Scenario,
                        result.DiagnosticResultCode,
                        result.Message,
                        BuildHotkeyDiagnosticMetadata(result));
                }

                return result;
            }

            return FeatureToggleHotkeyDispatchResult.NoOp;
        }

        private static bool HasActiveUnifiedBindings(UnifiedHotkeyRuntimeBinding[] bindings)
        {
            if (bindings == null || bindings.Length <= 0)
            {
                return false;
            }

            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                string targetId;
                string normalizedTargetId;
                if (binding != null &&
                    UnifiedHotkeyBindingIds.TryGetFeatureToggleTargetId(binding.BindingId, out targetId) &&
                    FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasActiveUnifiedFeatureToggleSettings(UnifiedHotkeySettings settings)
        {
            var bindings = settings == null ? null : settings.BindingsById;
            if (bindings == null || bindings.Count <= 0)
            {
                return false;
            }

            foreach (var pair in bindings)
            {
                string targetId;
                string normalizedTargetId;
                if (!string.IsNullOrWhiteSpace(pair.Value) &&
                    UnifiedHotkeyBindingIds.TryGetFeatureToggleTargetId(pair.Key, out targetId) &&
                    FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
                {
                    return true;
                }
            }

            return false;
        }

        private static UnifiedHotkeySettings CreateUnifiedSettingsFromLegacyFeatureToggleSettings(HotkeySettings hotkeySettings)
        {
            // Test-only bridge for historical regression cases. Production feature-toggle dispatch
            // reads ConfigService.UnifiedHotkeySettings and must not migrate old hotkeys.json bindings.
            var unified = new UnifiedHotkeySettings
            {
                ConfigVersion = UnifiedHotkeySettings.CurrentConfigVersion,
                BindingsById = new Dictionary<string, string>(StringComparer.Ordinal)
            };
            var source = hotkeySettings == null ? null : hotkeySettings.ToggleHotkeysByTargetId;
            if (source != null)
            {
                foreach (var pair in source)
                {
                    string targetId;
                    if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(pair.Key, out targetId))
                    {
                        continue;
                    }

                    UnifiedHotkeyBindingUpdateResult update;
                    unified.TrySetBinding(
                        UnifiedHotkeyBindingIds.ForFeatureToggleTarget(targetId),
                        NormalizeLegacyChordForUnified(pair.Value),
                        out update);
                }
            }

            if (hotkeySettings != null && hotkeySettings.HotkeysByFeatureId != null)
            {
                string miningHotkey;
                if (hotkeySettings.HotkeysByFeatureId.TryGetValue(FeatureIds.WorldAutomationAutoMining, out miningHotkey))
                {
                    UnifiedHotkeyBindingUpdateResult update;
                    unified.TrySetBinding(
                        UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                        NormalizeLegacyChordForUnified(miningHotkey),
                        out update);
                }
            }

            return unified;
        }

        private static string NormalizeLegacyChordForUnified(string chordText)
        {
            FeatureToggleHotkeyChord legacy;
            if (!FeatureToggleHotkeyChord.TryParse(chordText, out legacy) || legacy == null)
            {
                return chordText ?? string.Empty;
            }

            var modifier = string.Empty;
            if (string.Equals(legacy.Modifier, "Alt", StringComparison.Ordinal))
            {
                modifier = "LAlt+";
            }
            else if (string.Equals(legacy.Modifier, "Ctrl", StringComparison.Ordinal))
            {
                modifier = "LCtrl+";
            }
            else if (string.Equals(legacy.Modifier, "Shift", StringComparison.Ordinal))
            {
                modifier = "LShift+";
            }

            return modifier + (legacy.Key ?? string.Empty);
        }

        private static UnifiedHotkeyRuntimeInputState CreateUnifiedInputStateFromLegacyDownKeys(IDictionary<int, bool> downKeys)
        {
            return new UnifiedHotkeyRuntimeInputState(key => IsLegacyOrUnifiedKeyDown(downKeys, key));
        }

        private static bool IsLegacyOrUnifiedKeyDown(IDictionary<int, bool> downKeys, int key)
        {
            if (downKeys == null)
            {
                return false;
            }

            bool down;
            if (downKeys.TryGetValue(key, out down) && down)
            {
                return true;
            }

            switch (key)
            {
                case 0xA2:
                case 0xA3:
                    return downKeys.TryGetValue(VkControl, out down) && down;
                case 0xA4:
                case 0xA5:
                    return downKeys.TryGetValue(VkAlt, out down) && down;
                case 0xA0:
                case 0xA1:
                    return downKeys.TryGetValue(VkShift, out down) && down;
                default:
                    return false;
            }
        }

        private static UnifiedHotkeyRuntimeGateContext CreateGateContextForTesting(bool gameInputAvailable, string gateReason)
        {
            var gate = new UnifiedHotkeyRuntimeGateContext
            {
                GameInputAvailable = gameInputAvailable
            };
            if (gameInputAvailable || string.IsNullOrWhiteSpace(gateReason))
            {
                return gate;
            }

            gate.GameInputAvailable = true;
            ApplyGateReasonForTesting(gate, gateReason);
            return gate;
        }

        private static void ApplyGateReasonForTesting(UnifiedHotkeyRuntimeGateContext gate, string reason)
        {
            if (gate == null)
            {
                return;
            }

            if (string.Equals(reason, UnifiedHotkeyRuntimeGate.TextInputFocused, StringComparison.Ordinal) ||
                string.Equals(reason, "chatOpen", StringComparison.Ordinal))
            {
                gate.TerrariaTextInputFocused = true;
                gate.TerrariaTextInputReason = reason;
                return;
            }

            if (string.Equals(reason, "legacyUiActive", StringComparison.Ordinal))
            {
                gate.LegacyUiActiveInteraction = true;
                return;
            }

            if (string.Equals(reason, "legacyModalActive", StringComparison.Ordinal) ||
                string.Equals(reason, UnifiedHotkeyRuntimeGate.LegacyModalOpen, StringComparison.Ordinal))
            {
                gate.LegacyModalOpen = true;
                return;
            }

            if (string.Equals(reason, "hotkeyCaptureActive", StringComparison.Ordinal))
            {
                gate.HotkeyCaptureActive = true;
                return;
            }

            gate.GameInputAvailable = false;
        }

        private static string BuildHotkeyDiagnosticMetadata(FeatureToggleHotkeyDispatchResult result)
        {
            result = result ?? FeatureToggleHotkeyDispatchResult.NoOp;
            var reason = result.Reason ?? string.Empty;
            return UnifiedHotkeyReasonCatalog.BuildDiagnosticMetadataJson(
                "bindingId", string.IsNullOrWhiteSpace(result.TargetId) ? string.Empty : UnifiedHotkeyBindingIds.ForFeatureToggleTarget(result.TargetId),
                "targetId", result.TargetId,
                "resultCode", result.DiagnosticResultCode.ToString(),
                "reason", reason,
                "reasonCode", UnifiedHotkeyReasonCatalog.NormalizeRuntimeReasonCode(reason),
                "blockedReason", result.Applied ? string.Empty : reason,
                "playerMessage", result.Message);
        }

    }

    internal static class FeatureToggleProfile
    {
        public static FeatureToggleApplyResult Toggle(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            UnifiedHotkeySettings unifiedHotkeySettings,
            string targetId,
            bool saveConfig)
        {
            settings = settings ?? AppSettings.CreateDefault();
            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            unifiedHotkeySettings = unifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault();

            switch (targetId)
            {
                case "buff.auto_heal":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoRecoverySettings.NormalizeHealMode(settings.AutoHealMode, settings.AutoHealEnabled),
                        mode => AutoRecoveryService.SetAutoHealMode(mode),
                        AutoRecoverySettings.HealModeOff,
                        true);
                case "buff.auto_mana":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoRecoverySettings.NormalizeManaMode(settings.AutoManaMode, settings.AutoManaEnabled),
                        mode => AutoRecoveryService.SetAutoManaMode(mode),
                        AutoRecoverySettings.ManaModeOff,
                        true);
                case "buff.nurse_auto_heal":
                    return ToggleBinary(settings.AutoNurseEnabled, value => AutoRecoveryService.SetAutoNurseEnabled(value), targetId, true);
                case "buff.auto_station_buff":
                    return ToggleBinary(settings.AutoStationBuffEnabled, value => AutoRecoveryService.SetAutoStationBuffEnabled(value), targetId, true);
                case "buff.auto_buff":
                    return ToggleBinary(settings.AutoBuffEnabled, value =>
                    {
                        if (settings.AutoBuffEnabled != value)
                        {
                            AutoRecoveryService.ToggleAutoBuff();
                        }
                    }, targetId, true);
                case FeatureIds.InventoryQuickItemHotkeys:
                    return ToggleBinary(settings.InventoryQuickItemHotkeysEnabled, value => settings.InventoryQuickItemHotkeysEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoStack:
                    return ToggleBinary(settings.InventoryAutoStackEnabled, value => settings.InventoryAutoStackEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoSell:
                    return ToggleBinary(settings.InventoryAutoSellEnabled, value => settings.InventoryAutoSellEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoDiscard:
                    return ToggleBinary(settings.InventoryAutoDiscardEnabled, value => settings.InventoryAutoDiscardEnabled = value, targetId, false);
                case FeatureIds.WorldAutomationAutoCaptureCritter:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled),
                        mode =>
                        {
                            settings.WorldAutomationAutoCaptureCritterMode = mode;
                            if (string.Equals(mode, AutoCaptureCritterModes.Off, StringComparison.Ordinal))
                            {
                                AutoCaptureCritterService.ClearState("feature toggle hotkey disabled");
                            }
                        },
                        AutoCaptureCritterModes.Off,
                        false);
                case FeatureIds.WorldAutomationAutoHarvest:
                    return ToggleBinary(settings.WorldAutomationAutoHarvestEnabled, value =>
                    {
                        settings.WorldAutomationAutoHarvestEnabled = value;
                        if (!value)
                        {
                            AutoHarvestService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryQuickBagOpen:
                    return ToggleBinary(settings.InventoryQuickBagOpenEnabled, value =>
                    {
                        settings.InventoryQuickBagOpenEnabled = value;
                        if (!value)
                        {
                            QuickBagOpenService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryAutoDepositCoins:
                    return ToggleBinary(settings.InventoryAutoDepositCoinsEnabled, value =>
                    {
                        settings.InventoryAutoDepositCoinsEnabled = value;
                        if (!value)
                        {
                            AutoDepositCoinsService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryAutoExtractinator:
                    return ToggleBinary(settings.InventoryAutoExtractinatorEnabled, value =>
                    {
                        settings.InventoryAutoExtractinatorEnabled = value;
                        if (!value)
                        {
                            AutoExtractinatorService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryKeepFavorited:
                    return ToggleBinary(settings.InventoryKeepFavoritedEnabled, value =>
                    {
                        settings.InventoryKeepFavoritedEnabled = value;
                        if (!value)
                        {
                            KeepFavoritedService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.NpcAutoReforge:
                    return ToggleBinary(settings.NpcAutoReforgeEnabled, value => settings.NpcAutoReforgeEnabled = value, targetId, false);
                case FeatureIds.WorldAutomationAutoMining:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode),
                        mode =>
                        {
                            settings.WorldAutomationAutoMiningMode = mode;
                            if (string.Equals(mode, AutoMiningModes.Off, StringComparison.Ordinal))
                            {
                                AutoMiningService.ClearSelection("feature toggle hotkey disabled");
                            }
                        },
                        AutoMiningModes.Off,
                        false,
                        mode => ValidateAutoMiningMode(mode, unifiedHotkeySettings));
                case FeatureIds.NpcAutoTaxCollect:
                    return ToggleBinary(settings.NpcAutoTaxCollectEnabled, value =>
                    {
                        settings.NpcAutoTaxCollectEnabled = value;
                        if (!value)
                        {
                            AutoTaxCollectorService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.WorldAutomationTravelMenu:
                    return ToggleBinary(settings.WorldAutomationTravelMenuEnabled, value => settings.WorldAutomationTravelMenuEnabled = value, targetId, false);
                case FeatureIds.MapPersistentDeathMarkers:
                    return ToggleBinary(settings.MapPersistentDeathMarkersEnabled, value => settings.MapPersistentDeathMarkersEnabled = value, targetId, false);
                case FeatureIds.MapFootprints:
                    return ToggleBinary(settings.MapFootprintsDisplayEnabled, value => settings.MapFootprintsDisplayEnabled = value, targetId, false);
                case FeatureIds.MapRareCreatureDirection:
                    return ToggleBinary(settings.MapRareCreatureDirectionEnabled, value => settings.MapRareCreatureDirectionEnabled = value, targetId, false);
                case FeatureIds.MapTravellingMerchantDirection:
                    return ToggleBinary(settings.MapTravellingMerchantDirectionEnabled, value => settings.MapTravellingMerchantDirectionEnabled = value, targetId, false);
                case FeatureIds.MapQuickAnnouncement:
                    return ToggleBinary(settings.MapQuickAnnouncementEnabled, value => settings.MapQuickAnnouncementEnabled = value, targetId, false);
                case FeatureIds.MapCustomMarkers:
                    return ToggleBinary(settings.MapCustomMarkersEnabled, value => settings.MapCustomMarkersEnabled = value, targetId, false);
                case FeatureIds.FishingAutoFish:
                    return ToggleBinary(settings.FishingAutoFishEnabled, value => settings.FishingAutoFishEnabled = value, targetId, false);
                case FeatureIds.FishingAutoLoadout:
                    return ToggleBinary(settings.FishingAutoLoadoutEnabled, value =>
                    {
                        settings.FishingAutoLoadoutEnabled = value;
                        if (value)
                        {
                            settings.FishingAutoEquipmentEnabled = false;
                        }
                    }, targetId, false);
                case FeatureIds.FishingAutoEquipment:
                    return ToggleBinary(settings.FishingAutoEquipmentEnabled, value =>
                    {
                        settings.FishingAutoEquipmentEnabled = value;
                        if (value)
                        {
                            settings.FishingAutoLoadoutEnabled = false;
                        }
                    }, targetId, false);
                case FeatureIds.FishingAutoStoreQuestFish:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled),
                        mode =>
                        {
                            settings.FishingAutoStoreMode = mode;
                            settings.FishingAutoStoreQuestFishEnabled = FishingAutoStoreModes.IsEnabled(mode);
                        },
                        FishingAutoStoreModes.Off,
                        false);
                case "fishing.cut_rod_skip":
                    return ToggleBinary(settings.FishingFilterCutRodSkipEnabled, value => settings.FishingFilterCutRodSkipEnabled = value, targetId, false);
                case "information.enemy_name_labels":
                    return ToggleBinary(settings.InformationEnemyNameLabelsEnabled, value => settings.InformationEnemyNameLabelsEnabled = value, targetId, false);
                case "information.critter_name_labels":
                    return ToggleBinary(settings.InformationCritterNameLabelsEnabled, value => settings.InformationCritterNameLabelsEnabled = value, targetId, false);
                case "information.npc_name_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => NormalizeInformationNpcNameLabelsMode(settings.InformationNpcNameLabelsMode),
                        mode => settings.InformationNpcNameLabelsMode = mode,
                        "Off",
                        false);
                case "information.chest_name_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => NormalizeInformationChestNameLabelsMode(settings.InformationChestNameLabelsMode, settings.InformationChestNameLabelsEnabled),
                        mode =>
                        {
                            settings.InformationChestNameLabelsMode = mode;
                            settings.InformationChestNameLabelsEnabled = !string.Equals(mode, "Off", StringComparison.Ordinal);
                        },
                        "Off",
                        false);
                case "information.sign_text_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => InformationSignTextModes.Normalize(settings.InformationSignTextLabelsMode),
                        mode =>
                        {
                            settings.InformationSignTextLabelsMode = mode;
                            settings.InformationSignTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines);
                            settings.InformationSignTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters);
                        },
                        InformationSignTextModes.Off,
                        false);
                case "information.tombstone_text_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => InformationSignTextModes.Normalize(settings.InformationTombstoneTextLabelsMode),
                        mode =>
                        {
                            settings.InformationTombstoneTextLabelsMode = mode;
                            settings.InformationTombstoneTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines);
                            settings.InformationTombstoneTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters);
                        },
                        InformationSignTextModes.Off,
                        false);
                case "information.highlight_life_crystal":
                    return ToggleBinary(settings.InformationHighlightLifeCrystalEnabled, value => settings.InformationHighlightLifeCrystalEnabled = value, targetId, false);
                case "information.highlight_mana_crystal":
                    return ToggleBinary(settings.InformationHighlightManaCrystalEnabled, value => settings.InformationHighlightManaCrystalEnabled = value, targetId, false);
                case FeatureIds.InformationHighlightDigtoise:
                    return ToggleBinary(settings.InformationHighlightDigtoiseEnabled, value => settings.InformationHighlightDigtoiseEnabled = value, targetId, false);
                case "information.highlight_life_fruit":
                    return ToggleBinary(settings.InformationHighlightLifeFruitEnabled, value => settings.InformationHighlightLifeFruitEnabled = value, targetId, false);
                case "information.highlight_dragon_egg":
                    return ToggleBinary(settings.InformationHighlightDragonEggEnabled, value => settings.InformationHighlightDragonEggEnabled = value, targetId, false);
                case "information.biome_display":
                    return ToggleBinary(settings.InformationBiomeDisplayEnabled, value => settings.InformationBiomeDisplayEnabled = value, targetId, false);
                case "information.world_infection":
                    return ToggleBinary(settings.InformationWorldInfectionEnabled, value => settings.InformationWorldInfectionEnabled = value, targetId, false);
                case "information.luck_value":
                    return ToggleBinary(settings.InformationLuckValueEnabled, value => settings.InformationLuckValueEnabled = value, targetId, false);
                case "information.fishing_catches":
                    return ToggleBinary(settings.InformationFishingCatchesEnabled, value => settings.InformationFishingCatchesEnabled = value, targetId, false);
                case "information.fishing_filtered_catches":
                    return ToggleBinary(settings.InformationFishingFilteredCatchesEnabled, value => settings.InformationFishingFilteredCatchesEnabled = value, targetId, false);
                case "information.angler_quest":
                    return ToggleBinary(settings.InformationAnglerQuestEnabled, value => settings.InformationAnglerQuestEnabled = value, targetId, false);
                case FeatureIds.CombatAutoClicker:
                    return ToggleBinary(settings.CombatAutoClickerEnabled, value => settings.CombatAutoClickerEnabled = value, targetId, false);
                case FeatureIds.CombatFlailCombo:
                    return ToggleBinary(settings.CombatFlailComboEnabled, value => settings.CombatFlailComboEnabled = value, targetId, false);
                case FeatureIds.CombatPhasebladeQuickSwitch:
                    return ToggleBinary(settings.CombatPhasebladeQuickSwitchEnabled, value => settings.CombatPhasebladeQuickSwitchEnabled = value, targetId, false);
                case FeatureIds.CombatPerfectRevolver:
                    return ToggleBinary(settings.CombatPerfectRevolverEnabled, value => settings.CombatPerfectRevolverEnabled = value, targetId, false);
                case FeatureIds.CombatMagicStringClicker:
                    return ToggleBinary(settings.CombatMagicStringClickerEnabled, value => settings.CombatMagicStringClickerEnabled = value, targetId, false);
                case FeatureIds.CombatAutoFacing:
                    return ToggleBinary(settings.CombatAutoFacingEnabled, value => settings.CombatAutoFacingEnabled = value, targetId, false);
                case FeatureIds.CombatEquipmentWarning:
                    return ToggleBinary(settings.CombatEquipmentWarningEnabled, value => settings.CombatEquipmentWarningEnabled = value, targetId, false);
                case FeatureIds.CombatAutoBossDamageReport:
                    return ToggleBinary(settings.CombatAutoBossDamageReportEnabled, value => settings.CombatAutoBossDamageReportEnabled = value, targetId, false);
                case FeatureIds.CombatGoblinExecution:
                    return ToggleBinary(settings.CombatGoblinExecutionEnabled, value => settings.CombatGoblinExecutionEnabled = value, targetId, false);
                case FeatureIds.MovementSimulatedMultiJump:
                    return ToggleBinary(settings.MovementSimulatedMultiJumpEnabled, value =>
                    {
                        settings.MovementSimulatedMultiJumpEnabled = value;
                        if (!value)
                        {
                            MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(Guid.Empty, "feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.MovementContinuousDash:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => settings.MovementContinuousDashEnabled ? MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode) : MovementContinuousDashModes.Off,
                        mode =>
                        {
                            var off = string.Equals(mode, MovementContinuousDashModes.Off, StringComparison.Ordinal);
                            settings.MovementContinuousDashEnabled = !off;
                            if (!off)
                            {
                                settings.MovementContinuousDashMode = MovementContinuousDashModes.Normalize(mode);
                            }
                        },
                        MovementContinuousDashModes.Off,
                        false);
                case FeatureIds.MovementTeleportCorrection:
                    return ToggleBinary(settings.MovementTeleportCorrectionEnabled, value => settings.MovementTeleportCorrectionEnabled = value, targetId, false);
                case FeatureIds.MovementSafeLanding:
                    return ToggleBinary(settings.MovementSafeLandingEnabled, value =>
                    {
                        settings.MovementSafeLandingEnabled = value;
                        if (!value)
                        {
                            MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                default:
                    return FeatureToggleApplyResult.Blocked(targetId, "unknownTarget");
            }
        }

        private static FeatureToggleApplyResult ToggleBinary(
            bool current,
            Action<bool> setValue,
            string targetId,
            bool setterSaves)
        {
            var next = !current;
            setValue(next);
            return FeatureToggleApplyResult.CreateApplied(targetId, current ? "On" : "Off", next ? "On" : "Off", next, setterSaves);
        }

        private static FeatureToggleApplyResult ToggleMulti(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            string targetId,
            Func<string> getMode,
            Action<string> setMode,
            string offMode,
            bool setterSaves)
        {
            return ToggleMulti(settings, hotkeySettings, targetId, getMode, setMode, offMode, setterSaves, null);
        }

        private static FeatureToggleApplyResult ToggleMulti(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            string targetId,
            Func<string> getMode,
            Action<string> setMode,
            string offMode,
            bool setterSaves,
            Func<string, FeatureToggleModeGateResult> modeGate)
        {
            var current = getMode == null ? offMode : getMode();
            if (!IsOffMode(current, offMode))
            {
                string normalizedCurrent;
                if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(targetId, current, out normalizedCurrent))
                {
                    return FeatureToggleApplyResult.Blocked(targetId, "invalidCurrentMode");
                }

                var gate = modeGate == null ? FeatureToggleModeGateResult.Allow : modeGate(normalizedCurrent);
                if (!gate.Allowed)
                {
                    return FeatureToggleApplyResult.Blocked(targetId, gate.Reason);
                }

                EnsureLastModeMap(hotkeySettings)[targetId] = normalizedCurrent;
                setMode(offMode);
                return FeatureToggleApplyResult.CreateApplied(targetId, normalizedCurrent, offMode, false, setterSaves);
            }

            string lastMode;
            if (hotkeySettings.LastNonOffModeByTargetId == null ||
                !hotkeySettings.LastNonOffModeByTargetId.TryGetValue(targetId, out lastMode) ||
                !FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(targetId, lastMode, out lastMode))
            {
                // Multi-mode hotkeys intentionally do not guess a default mode.
                return FeatureToggleApplyResult.Blocked(targetId, "noLastNonOffMode");
            }

            var restoreGate = modeGate == null ? FeatureToggleModeGateResult.Allow : modeGate(lastMode);
            if (!restoreGate.Allowed)
            {
                return FeatureToggleApplyResult.Blocked(targetId, restoreGate.Reason);
            }

            setMode(lastMode);
            return FeatureToggleApplyResult.CreateApplied(targetId, offMode, lastMode, true, setterSaves);
        }

        private static Dictionary<string, string> EnsureLastModeMap(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings.LastNonOffModeByTargetId == null)
            {
                hotkeySettings.LastNonOffModeByTargetId = new Dictionary<string, string>();
            }

            return hotkeySettings.LastNonOffModeByTargetId;
        }

        private static FeatureToggleModeGateResult ValidateAutoMiningMode(string mode, UnifiedHotkeySettings unifiedHotkeySettings)
        {
            if (!string.Equals(mode, AutoMiningModes.Hotkey, StringComparison.Ordinal))
            {
                return FeatureToggleModeGateResult.Allow;
            }

            return unifiedHotkeySettings != null &&
                   !string.IsNullOrWhiteSpace(unifiedHotkeySettings.GetBinding(UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger))
                ? FeatureToggleModeGateResult.Allow
                : FeatureToggleModeGateResult.Blocked("missingMiningTriggerHotkey");
        }

        private static bool IsOffMode(string mode, string offMode)
        {
            return string.Equals(mode ?? string.Empty, offMode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeInformationNpcNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return "Type";
            }

            return string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase) ? "Name" : "Off";
        }

        private static string NormalizeInformationChestNameLabelsMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                return "Always";
            }

            if (string.Equals(mode, "Opened", StringComparison.OrdinalIgnoreCase) || legacyEnabled)
            {
                return "Opened";
            }

            return "Off";
        }
    }

    internal sealed class FeatureToggleApplyResult
    {
        private FeatureToggleApplyResult()
        {
            TargetId = string.Empty;
            PreviousMode = string.Empty;
            NewMode = string.Empty;
            Reason = string.Empty;
        }

        public bool Applied { get; private set; }
        public bool NewEnabled { get; private set; }
        public bool ConfigSaved { get; private set; }
        public string TargetId { get; private set; }
        public string PreviousMode { get; private set; }
        public string NewMode { get; private set; }
        public string Reason { get; private set; }

        public static FeatureToggleApplyResult CreateApplied(
            string targetId,
            string previousMode,
            string newMode,
            bool newEnabled,
            bool configSaved)
        {
            return new FeatureToggleApplyResult
            {
                Applied = true,
                TargetId = targetId ?? string.Empty,
                PreviousMode = previousMode ?? string.Empty,
                NewMode = newMode ?? string.Empty,
                NewEnabled = newEnabled,
                ConfigSaved = configSaved
            };
        }

        public static FeatureToggleApplyResult Blocked(string targetId, string reason)
        {
            return new FeatureToggleApplyResult
            {
                Applied = false,
                TargetId = targetId ?? string.Empty,
                Reason = reason ?? string.Empty
            };
        }
    }

    internal sealed class FeatureToggleHotkeyDispatchResult
    {
        public static readonly FeatureToggleHotkeyDispatchResult NoOp =
            new FeatureToggleHotkeyDispatchResult
            {
                Triggered = false,
                DiagnosticResultCode = DiagnosticResultCode.NotApplicable,
                Message = string.Empty
            };

        public bool Triggered { get; private set; }
        public bool Applied { get; private set; }
        public bool NewEnabled { get; private set; }
        public string TargetId { get; private set; }
        public string DisplayName { get; private set; }
        public string Chord { get; private set; }
        public string Reason { get; private set; }
        public string NewMode { get; private set; }
        public string Message { get; private set; }
        public DiagnosticResultCode DiagnosticResultCode { get; private set; }

        public static FeatureToggleHotkeyDispatchResult FromApply(
            string targetId,
            string displayName,
            string chord,
            FeatureToggleApplyResult apply)
        {
            targetId = targetId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            chord = chord ?? string.Empty;
            apply = apply ?? FeatureToggleApplyResult.Blocked(targetId, "unknown");
            return new FeatureToggleHotkeyDispatchResult
            {
                Triggered = true,
                Applied = apply.Applied,
                NewEnabled = apply.NewEnabled,
                TargetId = targetId,
                DisplayName = displayName,
                Chord = chord,
                Reason = apply.Applied ? "applied" : apply.Reason,
                NewMode = apply.NewMode,
                DiagnosticResultCode = apply.Applied ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                Message = apply.Applied
                    ? displayName + " feature toggle hotkey switched to " + apply.NewMode + "."
                    : displayName + " feature toggle hotkey ignored: " + apply.Reason + "."
            };
        }

        public static FeatureToggleHotkeyDispatchResult Blocked(
            string targetId,
            string displayName,
            string chord,
            string reason,
            DiagnosticResultCode resultCode)
        {
            targetId = targetId ?? string.Empty;
            displayName = displayName ?? string.Empty;
            chord = chord ?? string.Empty;
            return new FeatureToggleHotkeyDispatchResult
            {
                Triggered = true,
                Applied = false,
                TargetId = targetId,
                DisplayName = displayName,
                Chord = chord,
                Reason = reason ?? string.Empty,
                DiagnosticResultCode = resultCode,
                Message = UnifiedHotkeyReasonCatalog.BuildRuntimeGateMessage(displayName + " 功能主开关快捷键", reason)
            };
        }
    }

    internal struct FeatureToggleModeGateResult
    {
        public static readonly FeatureToggleModeGateResult Allow = new FeatureToggleModeGateResult(true, string.Empty);

        private FeatureToggleModeGateResult(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
        }

        public bool Allowed { get; private set; }
        public string Reason { get; private set; }

        public static FeatureToggleModeGateResult Blocked(string reason)
        {
            return new FeatureToggleModeGateResult(false, reason);
        }
    }
}
