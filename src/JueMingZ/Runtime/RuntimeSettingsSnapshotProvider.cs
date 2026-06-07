using System;
using System.Collections.Generic;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    // Caches normalized settings by signature; unchanged config should be a cheap snapshot read.
    internal static class RuntimeSettingsSnapshotProvider
    {
        private static readonly object SyncRoot = new object();
        private static RuntimeSettingsSnapshot _current;
        private static RuntimeSettingsSignature _signature;

        public static RuntimeSettingsSnapshot Current
        {
            get { return GetCurrent(); }
        }

        public static RuntimeSettingsSnapshot GetCurrent()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var legacyMainUiVisible = LegacyMainUiState.Visible;
            var legacySelectedPageId = LegacyMainUiState.SelectedPageId ?? string.Empty;
            var legacyMiscUiNeedsInventory =
                legacyMainUiVisible &&
                string.Equals(legacySelectedPageId, "misc", StringComparison.Ordinal) &&
                LegacyMainWindow.NeedsMiscInventorySnapshot();
            var signature = RuntimeSettingsSignature.Capture(
                settings,
                legacyMainUiVisible,
                legacySelectedPageId,
                legacyMiscUiNeedsInventory);

            // Normalize once per signature so high-frequency services can read
            // stable flags without rebuilding defaults or full settings each tick.
            lock (SyncRoot)
            {
                if (_current == null ||
                    !object.ReferenceEquals(_current.SourceSettings, settings) ||
                    !_signature.Equals(signature))
                {
                    _current = RuntimeSettingsSnapshot.FromSettings(
                        settings,
                        legacyMainUiVisible,
                        legacySelectedPageId,
                        legacyMiscUiNeedsInventory);
                    _signature = signature;
                }

                return _current;
            }
        }

        public static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _current = null;
                _signature = default(RuntimeSettingsSignature);
            }
        }

        private struct RuntimeSettingsSignature : IEquatable<RuntimeSettingsSignature>
        {
            private int _configVersion;
            private bool _autoHealEnabled;
            private bool _autoManaEnabled;
            private bool _autoBuffEnabled;
            private bool _autoNurseEnabled;
            private bool _autoStationBuffEnabled;
            private string _autoHealMode;
            private string _autoManaMode;
            private int _autoHealThresholdPercent;
            private int _autoManaThresholdPercent;
            private int _autoHealCooldownTicks;
            private int _autoManaCooldownTicks;
            private int _autoBuffCooldownTicks;
            private int _autoHealBlockedItemTypesHash;
            private int _autoManaBlockedItemTypesHash;
            private int _combatAimAssistRadius;
            private bool _combatAimTrackDummyEnabled;
            private bool _combatAimMarkerEnabled;
            private bool _combatAutoClickerEnabled;
            private bool _combatFlailComboEnabled;
            private bool _combatPerfectRevolverEnabled;
            private bool _combatMagicStringClickerEnabled;
            private bool _combatAutoFacingEnabled;
            private bool _combatEquipmentWarningEnabled;
            private string _aimRangeOrigin;
            private string _aimTargetPriority;
            private int _cursorAimRadius;
            private int _playerAimRadius;
            private int _releaseHoldTicks;
            private bool _persistentCursorAimEnabled;
            private bool _inventoryQuickItemHotkeysEnabled;
            private bool _inventoryAutoStackEnabled;
            private bool _inventoryAutoSellEnabled;
            private int _inventoryAutoSellItemIdsHash;
            private bool _inventoryAutoDiscardEnabled;
            private int _inventoryAutoDiscardItemIdsHash;
            private bool _inventoryQuickBagOpenEnabled;
            private bool _inventoryAutoDepositCoinsEnabled;
            private bool _inventoryAutoExtractinatorEnabled;
            private bool _inventoryKeepFavoritedEnabled;
            private bool _npcAutoReforgeEnabled;
            private int _npcAutoReforgePrefixesHash;
            private bool _npcAutoTaxCollectEnabled;
            private bool _worldAutomationTravelMenuEnabled;
            private string _worldAutomationAutoMiningMode;
            private bool _worldAutomationAutoCaptureCritterEnabled;
            private string _worldAutomationAutoCaptureCritterMode;
            private bool _worldAutomationAutoHarvestEnabled;
            private bool _fishingAutoFishEnabled;
            private bool _fishingAutoLoadoutEnabled;
            private bool _fishingAutoEquipmentEnabled;
            private bool _fishingAutoStoreQuestFishEnabled;
            private string _fishingAutoStoreMode;
            private string _fishingFilterMode;
            private string _fishingFilterMatchMode;
            private string _fishingFilterCrateRule;
            private string _fishingFilterQuestFishRule;
            private string _fishingFilterEnemyRule;
            private bool _fishingFilterCutRodSkipEnabled;
            private bool _movementSimulatedMultiJumpEnabled;
            private bool _movementContinuousDashEnabled;
            private string _movementContinuousDashMode;
            private bool _movementTeleportCorrectionEnabled;
            private bool _movementSafeLandingEnabled;
            private bool _informationEnemyNameLabelsEnabled;
            private bool _informationCritterNameLabelsEnabled;
            private string _informationNpcNameLabelsMode;
            private string _informationChestNameLabelsMode;
            private string _informationSignTextLabelsMode;
            private string _informationTombstoneTextLabelsMode;
            private bool _informationHighlightLifeCrystalEnabled;
            private bool _informationHighlightManaCrystalEnabled;
            private bool _informationHighlightDigtoiseEnabled;
            private bool _informationHighlightLifeFruitEnabled;
            private bool _informationHighlightDragonEggEnabled;
            private bool _informationBiomeDisplayEnabled;
            private bool _informationWorldInfectionEnabled;
            private bool _informationLuckValueEnabled;
            private bool _informationFishingCatchesEnabled;
            private bool _informationFishingFilteredCatchesEnabled;
            private bool _informationAnglerQuestEnabled;
            private double _informationEnemyNameFontScale;
            private double _informationCritterNameFontScale;
            private double _informationNpcNameFontScale;
            private double _informationChestNameFontScale;
            private double _informationSignTextFontScale;
            private double _informationTombstoneTextFontScale;
            private double _informationBiomeTextFontScale;
            private double _informationWorldInfectionTextFontScale;
            private double _informationLuckTextFontScale;
            private double _informationFishingCatchesTextFontScale;
            private double _informationFishingFilteredCatchesTextFontScale;
            private double _informationAnglerQuestTextFontScale;
            private bool _diagnosticsWorldGenDebugViewerEnabled;
            private bool _diagnosticsDeveloperDebugCommandsEnabled;
            private bool _enableDiagnosticInputTests;
            private int _diagnosticInputTestSlot;
            private bool _legacyMainUiVisible;
            private string _legacySelectedPageId;
            private bool _legacyMiscUiNeedsInventorySnapshot;

            public static RuntimeSettingsSignature Capture(
                AppSettings settings,
                bool legacyMainUiVisible,
                string legacySelectedPageId,
                bool legacyMiscUiNeedsInventorySnapshot)
            {
                settings = settings ?? AppSettings.CreateDefault();
                return new RuntimeSettingsSignature
                {
                    _configVersion = settings.ConfigVersion,
                    _autoHealEnabled = settings.AutoHealEnabled,
                    _autoManaEnabled = settings.AutoManaEnabled,
                    _autoBuffEnabled = settings.AutoBuffEnabled,
                    _autoNurseEnabled = settings.AutoNurseEnabled,
                    _autoStationBuffEnabled = settings.AutoStationBuffEnabled,
                    _autoHealMode = settings.AutoHealMode,
                    _autoManaMode = settings.AutoManaMode,
                    _autoHealThresholdPercent = settings.AutoHealThresholdPercent,
                    _autoManaThresholdPercent = settings.AutoManaThresholdPercent,
                    _autoHealCooldownTicks = settings.AutoHealCooldownTicks,
                    _autoManaCooldownTicks = settings.AutoManaCooldownTicks,
                    _autoBuffCooldownTicks = settings.AutoBuffCooldownTicks,
                    _autoHealBlockedItemTypesHash = HashInts(settings.AutoHealBlockedItemTypes),
                    _autoManaBlockedItemTypesHash = HashInts(settings.AutoManaBlockedItemTypes),
                    _combatAimAssistRadius = settings.CombatAimAssistRadius,
                    _combatAimTrackDummyEnabled = settings.CombatAimTrackDummyEnabled,
                    _combatAimMarkerEnabled = settings.CombatAimMarkerEnabled,
                    _combatAutoClickerEnabled = settings.CombatAutoClickerEnabled,
                    _combatFlailComboEnabled = settings.CombatFlailComboEnabled,
                    _combatPerfectRevolverEnabled = settings.CombatPerfectRevolverEnabled,
                    _combatMagicStringClickerEnabled = settings.CombatMagicStringClickerEnabled,
                    _combatAutoFacingEnabled = settings.CombatAutoFacingEnabled,
                    _combatEquipmentWarningEnabled = settings.CombatEquipmentWarningEnabled,
                    _aimRangeOrigin = settings.AimRangeOrigin,
                    _aimTargetPriority = settings.AimTargetPriority,
                    _cursorAimRadius = settings.CursorAimRadius,
                    _playerAimRadius = settings.PlayerAimRadius,
                    _releaseHoldTicks = settings.ReleaseHoldTicks,
                    _persistentCursorAimEnabled = settings.PersistentCursorAimEnabled,
                    _inventoryQuickItemHotkeysEnabled = settings.InventoryQuickItemHotkeysEnabled,
                    _inventoryAutoStackEnabled = settings.InventoryAutoStackEnabled,
                    _inventoryAutoSellEnabled = settings.InventoryAutoSellEnabled,
                    _inventoryAutoSellItemIdsHash = settings.InventoryAutoSellEnabled ? HashInts(settings.InventoryAutoSellItemIds) : 0,
                    _inventoryAutoDiscardEnabled = settings.InventoryAutoDiscardEnabled,
                    _inventoryAutoDiscardItemIdsHash = settings.InventoryAutoDiscardEnabled ? HashInts(settings.InventoryAutoDiscardItemIds) : 0,
                    _inventoryQuickBagOpenEnabled = settings.InventoryQuickBagOpenEnabled,
                    _inventoryAutoDepositCoinsEnabled = settings.InventoryAutoDepositCoinsEnabled,
                    _inventoryAutoExtractinatorEnabled = settings.InventoryAutoExtractinatorEnabled,
                    _inventoryKeepFavoritedEnabled = settings.InventoryKeepFavoritedEnabled,
                    _npcAutoReforgeEnabled = settings.NpcAutoReforgeEnabled,
                    _npcAutoReforgePrefixesHash = settings.NpcAutoReforgeEnabled ? HashStrings(settings.NpcAutoReforgePrefixes) : 0,
                    _npcAutoTaxCollectEnabled = settings.NpcAutoTaxCollectEnabled,
                    _worldAutomationTravelMenuEnabled = settings.WorldAutomationTravelMenuEnabled,
                    _worldAutomationAutoMiningMode = settings.WorldAutomationAutoMiningMode,
                    _worldAutomationAutoCaptureCritterEnabled = settings.MiscAutoCaptureCritterEnabled,
                    _worldAutomationAutoCaptureCritterMode = settings.WorldAutomationAutoCaptureCritterMode,
                    _worldAutomationAutoHarvestEnabled = settings.WorldAutomationAutoHarvestEnabled,
                    _fishingAutoFishEnabled = settings.FishingAutoFishEnabled,
                    _fishingAutoLoadoutEnabled = settings.FishingAutoLoadoutEnabled,
                    _fishingAutoEquipmentEnabled = settings.FishingAutoEquipmentEnabled,
                    _fishingAutoStoreQuestFishEnabled = settings.FishingAutoStoreQuestFishEnabled,
                    _fishingAutoStoreMode = settings.FishingAutoStoreMode,
                    _fishingFilterMode = settings.FishingFilterMode,
                    _fishingFilterMatchMode = settings.FishingFilterMatchMode,
                    _fishingFilterCrateRule = settings.FishingFilterCrateRule,
                    _fishingFilterQuestFishRule = settings.FishingFilterQuestFishRule,
                    _fishingFilterEnemyRule = settings.FishingFilterEnemyRule,
                    _fishingFilterCutRodSkipEnabled = settings.FishingFilterCutRodSkipEnabled,
                    _movementSimulatedMultiJumpEnabled = settings.MovementSimulatedMultiJumpEnabled,
                    _movementContinuousDashEnabled = settings.MovementContinuousDashEnabled,
                    _movementContinuousDashMode = settings.MovementContinuousDashMode,
                    _movementTeleportCorrectionEnabled = settings.MovementTeleportCorrectionEnabled,
                    _movementSafeLandingEnabled = settings.MovementSafeLandingEnabled,
                    _informationEnemyNameLabelsEnabled = settings.InformationEnemyNameLabelsEnabled,
                    _informationCritterNameLabelsEnabled = settings.InformationCritterNameLabelsEnabled,
                    _informationNpcNameLabelsMode = settings.InformationNpcNameLabelsMode,
                    _informationChestNameLabelsMode = settings.InformationChestNameLabelsMode,
                    _informationSignTextLabelsMode = settings.InformationSignTextLabelsMode,
                    _informationTombstoneTextLabelsMode = settings.InformationTombstoneTextLabelsMode,
                    _informationHighlightLifeCrystalEnabled = settings.InformationHighlightLifeCrystalEnabled,
                    _informationHighlightManaCrystalEnabled = settings.InformationHighlightManaCrystalEnabled,
                    _informationHighlightDigtoiseEnabled = settings.InformationHighlightDigtoiseEnabled,
                    _informationHighlightLifeFruitEnabled = settings.InformationHighlightLifeFruitEnabled,
                    _informationHighlightDragonEggEnabled = settings.InformationHighlightDragonEggEnabled,
                    _informationBiomeDisplayEnabled = settings.InformationBiomeDisplayEnabled,
                    _informationWorldInfectionEnabled = settings.InformationWorldInfectionEnabled,
                    _informationLuckValueEnabled = settings.InformationLuckValueEnabled,
                    _informationFishingCatchesEnabled = settings.InformationFishingCatchesEnabled,
                    _informationFishingFilteredCatchesEnabled = settings.InformationFishingFilteredCatchesEnabled,
                    _informationAnglerQuestEnabled = settings.InformationAnglerQuestEnabled,
                    _informationEnemyNameFontScale = settings.InformationEnemyNameFontScale,
                    _informationCritterNameFontScale = settings.InformationCritterNameFontScale,
                    _informationNpcNameFontScale = settings.InformationNpcNameFontScale,
                    _informationChestNameFontScale = settings.InformationChestNameFontScale,
                    _informationSignTextFontScale = settings.InformationSignTextFontScale,
                    _informationTombstoneTextFontScale = settings.InformationTombstoneTextFontScale,
                    _informationBiomeTextFontScale = settings.InformationBiomeTextFontScale,
                    _informationWorldInfectionTextFontScale = settings.InformationWorldInfectionTextFontScale,
                    _informationLuckTextFontScale = settings.InformationLuckTextFontScale,
                    _informationFishingCatchesTextFontScale = settings.InformationFishingCatchesTextFontScale,
                    _informationFishingFilteredCatchesTextFontScale = settings.InformationFishingFilteredCatchesTextFontScale,
                    _informationAnglerQuestTextFontScale = settings.InformationAnglerQuestTextFontScale,
                    _diagnosticsWorldGenDebugViewerEnabled = settings.DiagnosticsWorldGenDebugViewerEnabled,
                    _diagnosticsDeveloperDebugCommandsEnabled = settings.DiagnosticsDeveloperDebugCommandsEnabled,
                    _enableDiagnosticInputTests = settings.EnableDiagnosticInputTests,
                    _diagnosticInputTestSlot = settings.DiagnosticInputTestSlot,
                    _legacyMainUiVisible = legacyMainUiVisible,
                    _legacySelectedPageId = legacySelectedPageId ?? string.Empty,
                    _legacyMiscUiNeedsInventorySnapshot = legacyMiscUiNeedsInventorySnapshot
                };
            }

            public bool Equals(RuntimeSettingsSignature other)
            {
                return _configVersion == other._configVersion &&
                       _autoHealEnabled == other._autoHealEnabled &&
                       _autoManaEnabled == other._autoManaEnabled &&
                       _autoBuffEnabled == other._autoBuffEnabled &&
                       _autoNurseEnabled == other._autoNurseEnabled &&
                       _autoStationBuffEnabled == other._autoStationBuffEnabled &&
                       Same(_autoHealMode, other._autoHealMode) &&
                       Same(_autoManaMode, other._autoManaMode) &&
                       _autoHealThresholdPercent == other._autoHealThresholdPercent &&
                       _autoManaThresholdPercent == other._autoManaThresholdPercent &&
                       _autoHealCooldownTicks == other._autoHealCooldownTicks &&
                       _autoManaCooldownTicks == other._autoManaCooldownTicks &&
                       _autoBuffCooldownTicks == other._autoBuffCooldownTicks &&
                       _autoHealBlockedItemTypesHash == other._autoHealBlockedItemTypesHash &&
                       _autoManaBlockedItemTypesHash == other._autoManaBlockedItemTypesHash &&
                       _combatAimAssistRadius == other._combatAimAssistRadius &&
                       _combatAimTrackDummyEnabled == other._combatAimTrackDummyEnabled &&
                       _combatAimMarkerEnabled == other._combatAimMarkerEnabled &&
                       _combatAutoClickerEnabled == other._combatAutoClickerEnabled &&
                       _combatFlailComboEnabled == other._combatFlailComboEnabled &&
                       _combatPerfectRevolverEnabled == other._combatPerfectRevolverEnabled &&
                       _combatMagicStringClickerEnabled == other._combatMagicStringClickerEnabled &&
                       _combatAutoFacingEnabled == other._combatAutoFacingEnabled &&
                       _combatEquipmentWarningEnabled == other._combatEquipmentWarningEnabled &&
                       Same(_aimRangeOrigin, other._aimRangeOrigin) &&
                       Same(_aimTargetPriority, other._aimTargetPriority) &&
                       _cursorAimRadius == other._cursorAimRadius &&
                       _playerAimRadius == other._playerAimRadius &&
                       _releaseHoldTicks == other._releaseHoldTicks &&
                       _persistentCursorAimEnabled == other._persistentCursorAimEnabled &&
                       _inventoryQuickItemHotkeysEnabled == other._inventoryQuickItemHotkeysEnabled &&
                       _inventoryAutoStackEnabled == other._inventoryAutoStackEnabled &&
                       _inventoryAutoSellEnabled == other._inventoryAutoSellEnabled &&
                       _inventoryAutoSellItemIdsHash == other._inventoryAutoSellItemIdsHash &&
                       _inventoryAutoDiscardEnabled == other._inventoryAutoDiscardEnabled &&
                       _inventoryAutoDiscardItemIdsHash == other._inventoryAutoDiscardItemIdsHash &&
                       _inventoryQuickBagOpenEnabled == other._inventoryQuickBagOpenEnabled &&
                       _inventoryAutoDepositCoinsEnabled == other._inventoryAutoDepositCoinsEnabled &&
                       _inventoryAutoExtractinatorEnabled == other._inventoryAutoExtractinatorEnabled &&
                       _inventoryKeepFavoritedEnabled == other._inventoryKeepFavoritedEnabled &&
                       _npcAutoReforgeEnabled == other._npcAutoReforgeEnabled &&
                       _npcAutoReforgePrefixesHash == other._npcAutoReforgePrefixesHash &&
                       _npcAutoTaxCollectEnabled == other._npcAutoTaxCollectEnabled &&
                       _worldAutomationTravelMenuEnabled == other._worldAutomationTravelMenuEnabled &&
                       Same(_worldAutomationAutoMiningMode, other._worldAutomationAutoMiningMode) &&
                       _worldAutomationAutoCaptureCritterEnabled == other._worldAutomationAutoCaptureCritterEnabled &&
                       Same(_worldAutomationAutoCaptureCritterMode, other._worldAutomationAutoCaptureCritterMode) &&
                       _worldAutomationAutoHarvestEnabled == other._worldAutomationAutoHarvestEnabled &&
                       _fishingAutoFishEnabled == other._fishingAutoFishEnabled &&
                       _fishingAutoLoadoutEnabled == other._fishingAutoLoadoutEnabled &&
                       _fishingAutoEquipmentEnabled == other._fishingAutoEquipmentEnabled &&
                       _fishingAutoStoreQuestFishEnabled == other._fishingAutoStoreQuestFishEnabled &&
                       Same(_fishingAutoStoreMode, other._fishingAutoStoreMode) &&
                       Same(_fishingFilterMode, other._fishingFilterMode) &&
                       Same(_fishingFilterMatchMode, other._fishingFilterMatchMode) &&
                       Same(_fishingFilterCrateRule, other._fishingFilterCrateRule) &&
                       Same(_fishingFilterQuestFishRule, other._fishingFilterQuestFishRule) &&
                       Same(_fishingFilterEnemyRule, other._fishingFilterEnemyRule) &&
                       _fishingFilterCutRodSkipEnabled == other._fishingFilterCutRodSkipEnabled &&
                       _movementSimulatedMultiJumpEnabled == other._movementSimulatedMultiJumpEnabled &&
                       _movementContinuousDashEnabled == other._movementContinuousDashEnabled &&
                       Same(_movementContinuousDashMode, other._movementContinuousDashMode) &&
                       _movementTeleportCorrectionEnabled == other._movementTeleportCorrectionEnabled &&
                       _movementSafeLandingEnabled == other._movementSafeLandingEnabled &&
                       _informationEnemyNameLabelsEnabled == other._informationEnemyNameLabelsEnabled &&
                       _informationCritterNameLabelsEnabled == other._informationCritterNameLabelsEnabled &&
                       Same(_informationNpcNameLabelsMode, other._informationNpcNameLabelsMode) &&
                       Same(_informationChestNameLabelsMode, other._informationChestNameLabelsMode) &&
                       Same(_informationSignTextLabelsMode, other._informationSignTextLabelsMode) &&
                       Same(_informationTombstoneTextLabelsMode, other._informationTombstoneTextLabelsMode) &&
                       _informationHighlightLifeCrystalEnabled == other._informationHighlightLifeCrystalEnabled &&
                       _informationHighlightManaCrystalEnabled == other._informationHighlightManaCrystalEnabled &&
                       _informationHighlightDigtoiseEnabled == other._informationHighlightDigtoiseEnabled &&
                       _informationHighlightLifeFruitEnabled == other._informationHighlightLifeFruitEnabled &&
                       _informationHighlightDragonEggEnabled == other._informationHighlightDragonEggEnabled &&
                       _informationBiomeDisplayEnabled == other._informationBiomeDisplayEnabled &&
                       _informationWorldInfectionEnabled == other._informationWorldInfectionEnabled &&
                       _informationLuckValueEnabled == other._informationLuckValueEnabled &&
                       _informationFishingCatchesEnabled == other._informationFishingCatchesEnabled &&
                       _informationFishingFilteredCatchesEnabled == other._informationFishingFilteredCatchesEnabled &&
                       _informationAnglerQuestEnabled == other._informationAnglerQuestEnabled &&
                       _informationEnemyNameFontScale.Equals(other._informationEnemyNameFontScale) &&
                       _informationCritterNameFontScale.Equals(other._informationCritterNameFontScale) &&
                       _informationNpcNameFontScale.Equals(other._informationNpcNameFontScale) &&
                       _informationChestNameFontScale.Equals(other._informationChestNameFontScale) &&
                       _informationSignTextFontScale.Equals(other._informationSignTextFontScale) &&
                       _informationTombstoneTextFontScale.Equals(other._informationTombstoneTextFontScale) &&
                       _informationBiomeTextFontScale.Equals(other._informationBiomeTextFontScale) &&
                       _informationWorldInfectionTextFontScale.Equals(other._informationWorldInfectionTextFontScale) &&
                       _informationLuckTextFontScale.Equals(other._informationLuckTextFontScale) &&
                       _informationFishingCatchesTextFontScale.Equals(other._informationFishingCatchesTextFontScale) &&
                       _informationFishingFilteredCatchesTextFontScale.Equals(other._informationFishingFilteredCatchesTextFontScale) &&
                       _informationAnglerQuestTextFontScale.Equals(other._informationAnglerQuestTextFontScale) &&
                       _diagnosticsWorldGenDebugViewerEnabled == other._diagnosticsWorldGenDebugViewerEnabled &&
                       _diagnosticsDeveloperDebugCommandsEnabled == other._diagnosticsDeveloperDebugCommandsEnabled &&
                       _enableDiagnosticInputTests == other._enableDiagnosticInputTests &&
                       _diagnosticInputTestSlot == other._diagnosticInputTestSlot &&
                       _legacyMainUiVisible == other._legacyMainUiVisible &&
                       Same(_legacySelectedPageId, other._legacySelectedPageId) &&
                       _legacyMiscUiNeedsInventorySnapshot == other._legacyMiscUiNeedsInventorySnapshot;
            }

            public override bool Equals(object obj)
            {
                return obj is RuntimeSettingsSignature && Equals((RuntimeSettingsSignature)obj);
            }

            public override int GetHashCode()
            {
                return _configVersion;
            }

            private static bool Same(string left, string right)
            {
                return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
            }

            private static int HashInts(IList<int> values)
            {
                if (values == null || values.Count <= 0)
                {
                    return 0;
                }

                unchecked
                {
                    var hash = 17;
                    for (var index = 0; index < values.Count; index++)
                    {
                        hash = hash * 31 + values[index];
                    }

                    return hash * 31 + values.Count;
                }
            }

            private static int HashStrings(IList<string> values)
            {
                if (values == null || values.Count <= 0)
                {
                    return 0;
                }

                unchecked
                {
                    var hash = 17;
                    for (var index = 0; index < values.Count; index++)
                    {
                        hash = hash * 31 + StringComparer.Ordinal.GetHashCode(values[index] ?? string.Empty);
                    }

                    return hash * 31 + values.Count;
                }
            }
        }
    }
}
