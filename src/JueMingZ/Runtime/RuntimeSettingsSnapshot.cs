using System;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Config;

namespace JueMingZ.Runtime
{
    internal sealed class RuntimeSettingsSnapshot
    {
        private RuntimeSettingsSnapshot()
        {
        }

        public static RuntimeSettingsSnapshot FromSettings(AppSettings settings)
        {
            return FromSettings(settings, false, string.Empty, false);
        }

        public static RuntimeSettingsSnapshot FromSettings(
            AppSettings settings,
            bool legacyMainUiVisible,
            string legacySelectedPageId,
            bool legacyMiscUiNeedsInventorySnapshot)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var autoRecovery = AutoRecoverySettings.FromSettings(settings);
            var autoMiningMode = AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode);
            var autoCaptureMode = AutoCaptureCritterModes.Normalize(
                settings.WorldAutomationAutoCaptureCritterMode,
                settings.MiscAutoCaptureCritterEnabled);
            var fishingAutoStoreMode = FishingAutoStoreModes.Normalize(
                settings.FishingAutoStoreMode,
                settings.FishingAutoStoreQuestFishEnabled);
            var fishingFilterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var fishingFilterMatchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var aimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin);
            var aimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority);
            var cursorAimRadius = Clamp(settings.CursorAimRadius, 0, 50);
            var playerAimRadius = Clamp(settings.PlayerAimRadius, 0, 50);
            var combatAimAssistRadius = Clamp(settings.CombatAimAssistRadius, 0, 50);
            var combatAimAnyEnabled = cursorAimRadius > 0 ||
                                      playerAimRadius > 0 ||
                                      combatAimAssistRadius > 0;

            return new RuntimeSettingsSnapshot
            {
                SourceSettings = settings,
                AutoRecovery = autoRecovery,
                AutoHealMode = autoRecovery.AutoHealMode,
                AutoManaMode = autoRecovery.AutoManaMode,
                RecoveryAnyEnabled = autoRecovery.AnyEnabled,
                AutoBuffEnabled = autoRecovery.AutoBuffEnabled,
                CombatAimAssistRadius = combatAimAssistRadius,
                CursorAimRadius = cursorAimRadius,
                PlayerAimRadius = playerAimRadius,
                CombatAimAnyEnabled = combatAimAnyEnabled,
                CombatAimTrackDummyEnabled = settings.CombatAimTrackDummyEnabled,
                CombatAimMarkerEnabled = settings.CombatAimMarkerEnabled,
                CombatAutoClickerEnabled = settings.CombatAutoClickerEnabled,
                CombatPerfectRevolverEnabled = settings.CombatPerfectRevolverEnabled,
                CombatMagicStringClickerEnabled = settings.CombatMagicStringClickerEnabled,
                CombatAutoFacingEnabled = settings.CombatAutoFacingEnabled,
                CombatEquipmentWarningEnabled = settings.CombatEquipmentWarningEnabled,
                AimRangeOrigin = aimRangeOrigin,
                AimTargetPriority = aimTargetPriority,
                ReleaseHoldTicks = Clamp(settings.ReleaseHoldTicks, 0, 20),
                PersistentCursorAimEnabled = settings.PersistentCursorAimEnabled,
                CombatAimSelectionSettingsKey = BuildCombatAimSelectionSettingsKey(
                    cursorAimRadius,
                    playerAimRadius,
                    settings.CombatAimTrackDummyEnabled,
                    settings.CombatAimMarkerEnabled,
                    aimRangeOrigin,
                    aimTargetPriority),
                InventoryQuickItemHotkeysEnabled = settings.InventoryQuickItemHotkeysEnabled,
                InventoryAutoStackEnabled = settings.InventoryAutoStackEnabled,
                InventoryAutoSellEnabled = settings.InventoryAutoSellEnabled,
                InventoryAutoDiscardEnabled = settings.InventoryAutoDiscardEnabled,
                InventoryQuickBagOpenEnabled = settings.InventoryQuickBagOpenEnabled,
                InventoryAutoDepositCoinsEnabled = settings.InventoryAutoDepositCoinsEnabled,
                InventoryAutoExtractinatorEnabled = settings.InventoryAutoExtractinatorEnabled,
                InventoryKeepFavoritedEnabled = settings.InventoryKeepFavoritedEnabled,
                InventoryAutomationAnyEnabled = settings.InventoryQuickItemHotkeysEnabled ||
                                                settings.InventoryAutoStackEnabled ||
                                                settings.InventoryAutoSellEnabled ||
                                                settings.InventoryAutoDiscardEnabled ||
                                                settings.InventoryQuickBagOpenEnabled ||
                                                settings.InventoryAutoDepositCoinsEnabled ||
                                                settings.InventoryAutoExtractinatorEnabled ||
                                                settings.InventoryKeepFavoritedEnabled,
                NpcAutoReforgeEnabled = settings.NpcAutoReforgeEnabled,
                NpcAutomationAnyEnabled = settings.NpcAutoReforgeEnabled,
                WorldAutomationTravelMenuEnabled = settings.WorldAutomationTravelMenuEnabled,
                WorldAutomationAutoMiningMode = autoMiningMode,
                WorldAutomationAutoMiningEnabled = AutoMiningModes.IsEnabled(autoMiningMode),
                WorldAutomationAutoCaptureCritterMode = autoCaptureMode,
                WorldAutomationAutoCaptureCritterEnabled = AutoCaptureCritterModes.IsEnabled(autoCaptureMode),
                WorldAutomationAutoHarvestEnabled = settings.WorldAutomationAutoHarvestEnabled,
                FishingAutoFishEnabled = settings.FishingAutoFishEnabled,
                FishingAutoLoadoutEnabled = settings.FishingAutoLoadoutEnabled,
                FishingAutoEquipmentEnabled = settings.FishingAutoEquipmentEnabled,
                FishingAutoStoreMode = fishingAutoStoreMode,
                FishingAutoStoreEnabled = FishingAutoStoreModes.IsEnabled(fishingAutoStoreMode),
                FishingFilterMode = fishingFilterMode,
                FishingFilterMatchMode = fishingFilterMatchMode,
                FishingFilterCrateRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule),
                FishingFilterQuestFishRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule),
                FishingFilterEnemyRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule),
                FishingFilterEnabled = !string.Equals(fishingFilterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase),
                FishingFilterCutRodSkipEnabled = settings.FishingFilterCutRodSkipEnabled,
                FishingAnyEnabled = settings.FishingAutoFishEnabled ||
                                    settings.FishingAutoLoadoutEnabled ||
                                    settings.FishingAutoEquipmentEnabled ||
                                    FishingAutoStoreModes.IsEnabled(fishingAutoStoreMode),
                MovementSimulatedMultiJumpEnabled = settings.MovementSimulatedMultiJumpEnabled,
                MovementContinuousDashEnabled = settings.MovementContinuousDashEnabled,
                MovementContinuousDashMode = MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode),
                MovementTeleportCorrectionEnabled = settings.MovementTeleportCorrectionEnabled,
                MovementSafeLandingEnabled = settings.MovementSafeLandingEnabled,
                MovementAnyEnabled = settings.MovementSimulatedMultiJumpEnabled ||
                                     settings.MovementContinuousDashEnabled ||
                                     settings.MovementTeleportCorrectionEnabled ||
                                     settings.MovementSafeLandingEnabled,
                InformationOverlayAnyEnabled = HasInformationOverlayEnabled(settings),
                InformationEnemyNameFontScale = NormalizeFontScale(settings.InformationEnemyNameFontScale, 0.70d),
                InformationCritterNameFontScale = NormalizeFontScale(settings.InformationCritterNameFontScale, 0.70d),
                InformationNpcNameFontScale = NormalizeFontScale(settings.InformationNpcNameFontScale, 0.70d),
                InformationChestNameFontScale = NormalizeFontScale(settings.InformationChestNameFontScale, 0.70d),
                InformationSignTextFontScale = NormalizeFontScale(settings.InformationSignTextFontScale, 0.70d),
                InformationTombstoneTextFontScale = NormalizeFontScale(settings.InformationTombstoneTextFontScale, 0.70d),
                InformationBiomeTextFontScale = NormalizeFontScale(settings.InformationBiomeTextFontScale, 0.72d),
                InformationWorldInfectionTextFontScale = NormalizeFontScale(settings.InformationWorldInfectionTextFontScale, 0.72d),
                InformationLuckTextFontScale = NormalizeFontScale(settings.InformationLuckTextFontScale, 0.72d),
                InformationFishingCatchesTextFontScale = NormalizeFontScale(settings.InformationFishingCatchesTextFontScale, 0.72d),
                InformationFishingFilteredCatchesTextFontScale = NormalizeFontScale(settings.InformationFishingFilteredCatchesTextFontScale, 0.72d),
                InformationAnglerQuestTextFontScale = NormalizeFontScale(settings.InformationAnglerQuestTextFontScale, 0.72d),
                DiagnosticsWorldGenDebugViewerEnabled = settings.DiagnosticsWorldGenDebugViewerEnabled,
                DiagnosticsDeveloperDebugCommandsEnabled = settings.DiagnosticsDeveloperDebugCommandsEnabled,
                EnableDiagnosticInputTests = settings.EnableDiagnosticInputTests,
                DiagnosticInputTestSlot = Clamp(settings.DiagnosticInputTestSlot, 0, 9),
                LegacyMainUiVisible = legacyMainUiVisible,
                LegacySelectedPageId = legacySelectedPageId ?? string.Empty,
                LegacyMiscUiNeedsInventorySnapshot = legacyMiscUiNeedsInventorySnapshot
            };
        }

        public AppSettings SourceSettings { get; private set; }
        public AutoRecoverySettings AutoRecovery { get; private set; }
        public string AutoHealMode { get; private set; }
        public string AutoManaMode { get; private set; }
        public bool RecoveryAnyEnabled { get; private set; }
        public bool AutoBuffEnabled { get; private set; }
        public int CombatAimAssistRadius { get; private set; }
        public int CursorAimRadius { get; private set; }
        public int PlayerAimRadius { get; private set; }
        public bool CombatAimAnyEnabled { get; private set; }
        public bool CombatAimTrackDummyEnabled { get; private set; }
        public bool CombatAimMarkerEnabled { get; private set; }
        public bool CombatAutoClickerEnabled { get; private set; }
        public bool CombatPerfectRevolverEnabled { get; private set; }
        public bool CombatMagicStringClickerEnabled { get; private set; }
        public bool CombatAutoFacingEnabled { get; private set; }
        public bool CombatEquipmentWarningEnabled { get; private set; }
        public string AimRangeOrigin { get; private set; }
        public string AimTargetPriority { get; private set; }
        public int ReleaseHoldTicks { get; private set; }
        public bool PersistentCursorAimEnabled { get; private set; }
        public string CombatAimSelectionSettingsKey { get; private set; }
        public bool InventoryQuickItemHotkeysEnabled { get; private set; }
        public bool InventoryAutoStackEnabled { get; private set; }
        public bool InventoryAutoSellEnabled { get; private set; }
        public bool InventoryAutoDiscardEnabled { get; private set; }
        public bool InventoryQuickBagOpenEnabled { get; private set; }
        public bool InventoryAutoDepositCoinsEnabled { get; private set; }
        public bool InventoryAutoExtractinatorEnabled { get; private set; }
        public bool InventoryKeepFavoritedEnabled { get; private set; }
        public bool InventoryAutomationAnyEnabled { get; private set; }
        public bool NpcAutoReforgeEnabled { get; private set; }
        public bool NpcAutomationAnyEnabled { get; private set; }
        public bool WorldAutomationTravelMenuEnabled { get; private set; }
        public string WorldAutomationAutoMiningMode { get; private set; }
        public bool WorldAutomationAutoMiningEnabled { get; private set; }
        public string WorldAutomationAutoCaptureCritterMode { get; private set; }
        public bool WorldAutomationAutoCaptureCritterEnabled { get; private set; }
        public bool WorldAutomationAutoHarvestEnabled { get; private set; }
        public bool FishingAutoFishEnabled { get; private set; }
        public bool FishingAutoLoadoutEnabled { get; private set; }
        public bool FishingAutoEquipmentEnabled { get; private set; }
        public string FishingAutoStoreMode { get; private set; }
        public bool FishingAutoStoreEnabled { get; private set; }
        public string FishingFilterMode { get; private set; }
        public string FishingFilterMatchMode { get; private set; }
        public string FishingFilterCrateRule { get; private set; }
        public string FishingFilterQuestFishRule { get; private set; }
        public string FishingFilterEnemyRule { get; private set; }
        public bool FishingFilterEnabled { get; private set; }
        public bool FishingFilterCutRodSkipEnabled { get; private set; }
        public bool FishingAnyEnabled { get; private set; }
        public bool MovementSimulatedMultiJumpEnabled { get; private set; }
        public bool MovementContinuousDashEnabled { get; private set; }
        public string MovementContinuousDashMode { get; private set; }
        public bool MovementTeleportCorrectionEnabled { get; private set; }
        public bool MovementSafeLandingEnabled { get; private set; }
        public bool MovementAnyEnabled { get; private set; }
        public bool InformationOverlayAnyEnabled { get; private set; }
        public double InformationEnemyNameFontScale { get; private set; }
        public double InformationCritterNameFontScale { get; private set; }
        public double InformationNpcNameFontScale { get; private set; }
        public double InformationChestNameFontScale { get; private set; }
        public double InformationSignTextFontScale { get; private set; }
        public double InformationTombstoneTextFontScale { get; private set; }
        public double InformationBiomeTextFontScale { get; private set; }
        public double InformationWorldInfectionTextFontScale { get; private set; }
        public double InformationLuckTextFontScale { get; private set; }
        public double InformationFishingCatchesTextFontScale { get; private set; }
        public double InformationFishingFilteredCatchesTextFontScale { get; private set; }
        public double InformationAnglerQuestTextFontScale { get; private set; }
        public bool DiagnosticsWorldGenDebugViewerEnabled { get; private set; }
        public bool DiagnosticsDeveloperDebugCommandsEnabled { get; private set; }
        public bool EnableDiagnosticInputTests { get; private set; }
        public int DiagnosticInputTestSlot { get; private set; }
        public bool LegacyMainUiVisible { get; private set; }
        public string LegacySelectedPageId { get; private set; }
        public bool LegacyMiscUiNeedsInventorySnapshot { get; private set; }

        private static string BuildCombatAimSelectionSettingsKey(
            int cursorAimRadius,
            int playerAimRadius,
            bool trackDummy,
            bool markerEnabled,
            string aimRangeOrigin,
            string aimTargetPriority)
        {
            return cursorAimRadius.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                   (trackDummy ? "1" : "0") + "|" +
                   (markerEnabled ? "1" : "0") + "|" +
                   (aimRangeOrigin ?? string.Empty) + "|" +
                   (aimTargetPriority ?? string.Empty) + "|" +
                   cursorAimRadius.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                   playerAimRadius.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool HasInformationOverlayEnabled(AppSettings settings)
        {
            return settings.InformationEnemyNameLabelsEnabled ||
                   settings.InformationCritterNameLabelsEnabled ||
                   !string.Equals(settings.InformationNpcNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(settings.InformationChestNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase) ||
                   InformationSignTextModes.IsEnabled(settings.InformationSignTextLabelsMode) ||
                   InformationSignTextModes.IsEnabled(settings.InformationTombstoneTextLabelsMode) ||
                   settings.InformationHighlightLifeCrystalEnabled ||
                   settings.InformationHighlightManaCrystalEnabled ||
                   settings.InformationHighlightDigtoiseEnabled ||
                   settings.InformationHighlightLifeFruitEnabled ||
                   settings.InformationHighlightDragonEggEnabled ||
                   settings.InformationBiomeDisplayEnabled ||
                   settings.InformationWorldInfectionEnabled ||
                   settings.InformationLuckValueEnabled ||
                   settings.InformationFishingCatchesEnabled ||
                   settings.InformationFishingFilteredCatchesEnabled ||
                   settings.InformationAnglerQuestEnabled;
        }

        private static double NormalizeFontScale(double value, double fallback)
        {
            return InformationStyleHelper.NormalizeFontScale(value, fallback);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
