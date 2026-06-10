using System.Runtime.Serialization;
using System.Collections.Generic;
using JueMingZ.Automation.WorldAutomation;

namespace JueMingZ.Config
{
    // Persisted settings keep legacy serialized names; runtime code consumes normalized snapshots instead.
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember(Order = 1)]
        public int ConfigVersion { get; set; } = 1;

        [DataMember(Order = 2)]
        public string LogLevel { get; set; } = "Info";

        [DataMember(Order = 3)]
        public bool EnableTraceLog { get; set; }

        [DataMember(Order = 4)]
        public bool EnableDiagnosticsOverlay { get; set; } = true;

        [DataMember(Order = 5)]
        public bool AllowSinglePlayerDirectFallback { get; set; }

        [DataMember(Order = 6)]
        public bool AllowExperimentalFeatures { get; set; }

        [DataMember(Order = 7)]
        public bool EnableDiagnosticInputTests { get; set; } = true;

        [DataMember(Order = 8)]
        public int DiagnosticInputTestSlot { get; set; }

        [DataMember(Order = 9)]
        public bool AutoHealEnabled { get; set; }

        [DataMember(Order = 10)]
        public bool AutoManaEnabled { get; set; }

        [DataMember(Order = 11)]
        public bool AutoBuffEnabled { get; set; }

        [DataMember(Order = 12)]
        public int AutoHealThresholdPercent { get; set; } = 50;

        [DataMember(Order = 13)]
        public int AutoManaThresholdPercent { get; set; } = 35;

        [DataMember(Order = 14)]
        public int AutoHealCooldownTicks { get; set; } = 120;

        [DataMember(Order = 15)]
        public int AutoManaCooldownTicks { get; set; } = 8;

        [DataMember(Order = 16)]
        public int AutoBuffCooldownTicks { get; set; } = 1800;

        [DataMember(Order = 17)]
        public int OperationWindowX { get; set; } = 420;

        [DataMember(Order = 18)]
        public int OperationWindowY { get; set; } = 120;

        [DataMember(Order = 19)]
        public int OperationWindowWidth { get; set; } = 520;

        [DataMember(Order = 20)]
        public int OperationWindowHeight { get; set; } = 420;

        [DataMember(Order = 21)]
        public List<BuffPotionWhitelistEntry> AutoBuffWhitelist { get; set; } = new List<BuffPotionWhitelistEntry>();

        [DataMember(Order = 22)]
        public int LegacyMainWindowX { get; set; } = 320;

        [DataMember(Order = 23)]
        public int LegacyMainWindowY { get; set; } = 80;

        [DataMember(Order = 24)]
        public int LegacyMainWindowWidth { get; set; } = 600;

        [DataMember(Order = 25)]
        public int LegacyMainWindowHeight { get; set; } = 750;

        [DataMember(Order = 26)]
        public string LegacySelectedPageId { get; set; } = "buff";

        [DataMember(Order = 27)]
        public string AutoHealMode { get; set; } = "Off";

        [DataMember(Order = 28)]
        public string AutoManaMode { get; set; } = "Off";

        [DataMember(Order = 173)]
        public List<int> AutoHealBlockedItemTypes { get; set; } = new List<int>();

        [DataMember(Order = 174)]
        public List<int> AutoManaBlockedItemTypes { get; set; } = new List<int>();

        [DataMember(Order = 29)]
        public bool AutoNurseEnabled { get; set; }

        [DataMember(Order = 30)]
        public bool AutoStationBuffEnabled { get; set; }

        [DataMember(Order = 31)]
        public bool AutoBuffFollowAddEnabled { get; set; }

        [DataMember(Order = 32)]
        public bool AutoBuffFollowRemoveEnabled { get; set; }

        [DataMember(Order = 33)]
        public int CombatAimAssistRadius { get; set; }

        [DataMember(Order = 34)]
        public bool CombatAimTrackDummyEnabled { get; set; }

        [DataMember(Order = 35)]
        public bool CombatAimMarkerEnabled { get; set; } = true;

        [DataMember(Order = 36)]
        public bool CombatAutoClickerEnabled { get; set; }

        [DataMember(Order = 175)]
        public bool CombatFlailComboEnabled { get; set; }

        [DataMember(Order = 185)]
        public bool CombatPhasebladeQuickSwitchEnabled { get; set; }

        [DataMember(Order = 186)]
        public int CombatPhasebladeQuickSwitchIntervalTicks { get; set; } = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;

        [DataMember(Order = 37)]
        public bool CombatPerfectRevolverEnabled { get; set; }

        [DataMember(Order = 38)]
        public bool CombatMagicStringClickerEnabled { get; set; }

        [DataMember(Order = 39)]
        public bool CombatAutoFacingEnabled { get; set; }

        [DataMember(Order = 139)]
        public bool CombatEquipmentWarningEnabled { get; set; }

        [DataMember(Order = 171)]
        public bool CombatGoblinExecutionEnabled { get; set; }

        [DataMember(Order = 40)]
        public string AimRangeOrigin { get; set; } = CombatAimModes.RangeOriginPlayer;

        [DataMember(Order = 41)]
        public string AimTargetPriority { get; set; } = CombatAimModes.TargetPriorityNearest;

        [DataMember(Order = 42)]
        public int CursorAimRadius { get; set; }

        [DataMember(Order = 43)]
        public int PlayerAimRadius { get; set; }

        [DataMember(Order = 44)]
        public int ReleaseHoldTicks { get; set; } = 8;

        [DataMember(Order = 45)]
        public bool PersistentCursorAimEnabled { get; set; } = true;

        [DataMember(Order = 46)]
        public bool CombatAimSplitRadiusMigrated { get; set; }

        [DataMember(Order = 47)]
        public bool InformationEnemyNameLabelsEnabled { get; set; }

        [DataMember(Order = 48)]
        public bool InformationCritterNameLabelsEnabled { get; set; }

        [DataMember(Order = 49)]
        public string InformationNpcNameLabelsMode { get; set; } = "Off";

        [DataMember(Order = 50)]
        public bool InformationChestNameLabelsEnabled { get; set; }

        [DataMember(Order = 59)]
        public string InformationChestNameLabelsMode { get; set; } = "Off";

        [DataMember(Order = 139)]
        public string InformationSignTextLabelsMode { get; set; } = InformationSignTextModes.Off;

        [DataMember(Order = 140)]
        public int InformationSignTextMaxLines { get; set; } = InformationSignTextModes.DefaultLines;

        [DataMember(Order = 141)]
        public int InformationSignTextMaxCharacters { get; set; } = InformationSignTextModes.DefaultCharacters;

        [DataMember(Order = 144)]
        public string InformationTombstoneTextLabelsMode { get; set; } = InformationSignTextModes.Off;

        [DataMember(Order = 145)]
        public int InformationTombstoneTextMaxLines { get; set; } = InformationSignTextModes.DefaultLines;

        [DataMember(Order = 146)]
        public int InformationTombstoneTextMaxCharacters { get; set; } = InformationSignTextModes.DefaultCharacters;

        [DataMember(Order = 51)]
        public bool InformationHighlightLifeCrystalEnabled { get; set; }

        [DataMember(Order = 149)]
        public bool InformationHighlightManaCrystalEnabled { get; set; }

        [DataMember(Order = 168)]
        public bool InformationHighlightDigtoiseEnabled { get; set; }

        [DataMember(Order = 52)]
        public bool InformationHighlightLifeFruitEnabled { get; set; }

        [DataMember(Order = 53)]
        public bool InformationHighlightDragonEggEnabled { get; set; }

        [DataMember(Order = 54)]
        public bool InformationBiomeDisplayEnabled { get; set; }

        [DataMember(Order = 55)]
        public bool InformationWorldInfectionEnabled { get; set; }

        [DataMember(Order = 56)]
        public bool InformationLuckValueEnabled { get; set; }

        [DataMember(Order = 57)]
        public bool InformationFishingCatchesEnabled { get; set; }

        [DataMember(Order = 105)]
        public bool InformationFishingFilteredCatchesEnabled { get; set; }

        [DataMember(Order = 58)]
        public bool InformationAnglerQuestEnabled { get; set; }

        [DataMember(Order = 60)]
        public int InformationPanelX { get; set; } = 20;

        [DataMember(Order = 61)]
        public int InformationPanelY { get; set; } = 320;

        [DataMember(Order = 62)]
        public bool InformationPanelPositionInitialized { get; set; }

        [DataMember(Order = 63)]
        public string InformationEnemyNameColor { get; set; } = "#CD5C5C";

        [DataMember(Order = 64)]
        public string InformationCritterNameColor { get; set; } = "#5DADEC";

        [DataMember(Order = 65)]
        public string InformationNpcNameColor { get; set; } = "#90EE90";

        [DataMember(Order = 66)]
        public string InformationChestNameColor { get; set; } = "#FFA500";

        [DataMember(Order = 142)]
        public string InformationSignTextColor { get; set; } = "#E6C16A";

        [DataMember(Order = 147)]
        public string InformationTombstoneTextColor { get; set; } = "#FF5555";

        [DataMember(Order = 67)]
        public string InformationLifeCrystalHighlightColor { get; set; } = "#FF69B4";

        [DataMember(Order = 150)]
        public string InformationManaCrystalHighlightColor { get; set; } = "#66CCFF";

        [DataMember(Order = 151)]
        public bool MiscTravelMenuEnabled { get; set; }

        [DataMember(Order = 152)]
        public bool MiscDeveloperDebugCommandsEnabled { get; set; } = true;

        [DataMember(Order = 161)]
        public bool MiscWorldGenDebugViewerEnabled { get; set; } = true;

        [DataMember(Order = 153)]
        public bool MiscQuickItemHotkeysEnabled { get; set; }

        [DataMember(Order = 154)]
        public bool MiscAutoStackEnabled { get; set; }

        [DataMember(Order = 155)]
        public bool MiscAutoSellEnabled { get; set; }

        [DataMember(Order = 156)]
        public List<int> MiscAutoSellItemIds { get; set; } = new List<int> { 2337, 2338, 2339 };

        [DataMember(Order = 157)]
        public bool MiscAutoDiscardEnabled { get; set; }

        [DataMember(Order = 158)]
        public List<int> MiscAutoDiscardItemIds { get; set; } = new List<int>();

        [DataMember(Order = 159)]
        public bool MiscQuickReforgeEnabled { get; set; }

        [DataMember(Order = 160)]
        public List<string> MiscQuickReforgePrefixes { get; set; } = new List<string>();

        [DataMember(Order = 172)]
        public bool MiscAutoTaxCollectEnabled { get; set; }

        [DataMember(Order = 162)]
        public string MiscAutoMiningMode { get; set; } = "Off";

        [DataMember(Order = 163)]
        public bool MiscAutoCaptureCritterEnabled { get; set; }

        [DataMember(Order = 170)]
        public string MiscAutoCaptureCritterMode { get; set; }

        [DataMember(Order = 176)]
        public bool MiscAutoCaptureCritterCategoryDefaultsMigrated { get; set; }

        [DataMember(Order = 177)]
        public bool MiscAutoCaptureCritterBaitEnabled { get; set; }

        [DataMember(Order = 178)]
        public bool MiscAutoCaptureCritterFairyEnabled { get; set; }

        [DataMember(Order = 179)]
        public bool MiscAutoCaptureCritterGoldCritterEnabled { get; set; }

        [DataMember(Order = 180)]
        public bool MiscAutoCaptureCritterGemCritterEnabled { get; set; }

        [DataMember(Order = 181)]
        public bool MiscAutoCaptureCritterNormalCritterEnabled { get; set; }

        [DataMember(Order = 182)]
        public bool MiscAutoCaptureCritterTruffleWormEnabled { get; set; }

        [DataMember(Order = 183)]
        public bool MiscAutoCaptureCritterEmpressButterflyEnabled { get; set; }

        [DataMember(Order = 184)]
        public bool MiscAutoCaptureCritterOtherEnabled { get; set; }

        [DataMember(Order = 164)]
        public bool MiscAutoHarvestEnabled { get; set; }

        [DataMember(Order = 165)]
        public bool MiscQuickBagOpenEnabled { get; set; }

        [DataMember(Order = 166)]
        public bool MiscAutoExtractinatorEnabled { get; set; }

        [DataMember(Order = 167)]
        public bool MiscKeepFavoritedEnabled { get; set; }

        [DataMember(Order = 169)]
        public bool MiscAutoDepositCoinsEnabled { get; set; }

        // Compatibility-backed code-domain aliases. Keep the serialized Misc*
        // fields so existing user configs do not lose UI page settings.
        [IgnoreDataMember]
        public bool WorldAutomationTravelMenuEnabled
        {
            get { return MiscTravelMenuEnabled; }
            set { MiscTravelMenuEnabled = value; }
        }

        [IgnoreDataMember]
        public bool DiagnosticsDeveloperDebugCommandsEnabled
        {
            get { return true; }
            set { MiscDeveloperDebugCommandsEnabled = true; }
        }

        [IgnoreDataMember]
        public bool DiagnosticsWorldGenDebugViewerEnabled
        {
            get { return true; }
            set { MiscWorldGenDebugViewerEnabled = true; }
        }

        [IgnoreDataMember]
        public bool InventoryQuickItemHotkeysEnabled
        {
            get { return MiscQuickItemHotkeysEnabled; }
            set { MiscQuickItemHotkeysEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryAutoStackEnabled
        {
            get { return MiscAutoStackEnabled; }
            set { MiscAutoStackEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryAutoSellEnabled
        {
            get { return MiscAutoSellEnabled; }
            set { MiscAutoSellEnabled = value; }
        }

        [IgnoreDataMember]
        public List<int> InventoryAutoSellItemIds
        {
            get { return MiscAutoSellItemIds; }
            set { MiscAutoSellItemIds = value; }
        }

        [IgnoreDataMember]
        public bool InventoryAutoDiscardEnabled
        {
            get { return MiscAutoDiscardEnabled; }
            set { MiscAutoDiscardEnabled = value; }
        }

        [IgnoreDataMember]
        public List<int> InventoryAutoDiscardItemIds
        {
            get { return MiscAutoDiscardItemIds; }
            set { MiscAutoDiscardItemIds = value; }
        }

        [IgnoreDataMember]
        public bool NpcAutoReforgeEnabled
        {
            get { return MiscQuickReforgeEnabled; }
            set { MiscQuickReforgeEnabled = value; }
        }

        [IgnoreDataMember]
        public List<string> NpcAutoReforgePrefixes
        {
            get { return MiscQuickReforgePrefixes; }
            set { MiscQuickReforgePrefixes = value; }
        }

        [IgnoreDataMember]
        public bool NpcAutoTaxCollectEnabled
        {
            get { return MiscAutoTaxCollectEnabled; }
            set { MiscAutoTaxCollectEnabled = value; }
        }

        [IgnoreDataMember]
        public string WorldAutomationAutoMiningMode
        {
            get { return MiscAutoMiningMode; }
            set { MiscAutoMiningMode = string.IsNullOrWhiteSpace(value) ? "Off" : value; }
        }

        [IgnoreDataMember]
        public bool WorldAutomationAutoMiningEnabled
        {
            get { return !string.Equals(MiscAutoMiningMode, "Off", System.StringComparison.OrdinalIgnoreCase); }
            set
            {
                if (!value)
                {
                    MiscAutoMiningMode = "Off";
                    return;
                }

                if (string.Equals(MiscAutoMiningMode, "Off", System.StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(MiscAutoMiningMode))
                {
                    MiscAutoMiningMode = "Hotkey";
                }
            }
        }

        [IgnoreDataMember]
        public string WorldAutomationAutoCaptureCritterMode
        {
            get { return AutoCaptureCritterModes.Normalize(MiscAutoCaptureCritterMode, MiscAutoCaptureCritterEnabled); }
            set
            {
                MiscAutoCaptureCritterMode = AutoCaptureCritterModes.Normalize(value, MiscAutoCaptureCritterEnabled);
                MiscAutoCaptureCritterEnabled = AutoCaptureCritterModes.IsEnabled(MiscAutoCaptureCritterMode);
            }
        }

        [IgnoreDataMember]
        public bool WorldAutomationAutoCaptureCritterEnabled
        {
            get { return AutoCaptureCritterModes.IsEnabled(WorldAutomationAutoCaptureCritterMode); }
            set { WorldAutomationAutoCaptureCritterMode = value ? AutoCaptureCritterModes.Auto : AutoCaptureCritterModes.Off; }
        }

        [IgnoreDataMember]
        public bool WorldAutomationAutoHarvestEnabled
        {
            get { return MiscAutoHarvestEnabled; }
            set { MiscAutoHarvestEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryQuickBagOpenEnabled
        {
            get { return MiscQuickBagOpenEnabled; }
            set { MiscQuickBagOpenEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryAutoDepositCoinsEnabled
        {
            get { return MiscAutoDepositCoinsEnabled; }
            set { MiscAutoDepositCoinsEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryAutoExtractinatorEnabled
        {
            get { return MiscAutoExtractinatorEnabled; }
            set { MiscAutoExtractinatorEnabled = value; }
        }

        [IgnoreDataMember]
        public bool InventoryKeepFavoritedEnabled
        {
            get { return MiscKeepFavoritedEnabled; }
            set { MiscKeepFavoritedEnabled = value; }
        }

        [DataMember(Order = 68)]
        public string InformationLifeFruitHighlightColor { get; set; } = "#7CFC00";

        [DataMember(Order = 69)]
        public string InformationDragonEggHighlightColor { get; set; } = "#9370DB";

        [DataMember(Order = 70)]
        public string InformationBiomeTextColor { get; set; } = "#90EE90";

        [DataMember(Order = 71)]
        public string InformationWorldInfectionTextColor { get; set; } = "#DDA0DD";

        [DataMember(Order = 72)]
        public string InformationLuckTextColor { get; set; } = "#FAFAD2";

        [DataMember(Order = 73)]
        public string InformationFishingCatchesTextColor { get; set; } = "#87CEFA";

        [DataMember(Order = 106)]
        public string InformationFishingFilteredCatchesTextColor { get; set; } = "#FFB366";

        [DataMember(Order = 74)]
        public string InformationAnglerQuestTextColor { get; set; } = "#E0FFFF";

        [DataMember(Order = 75)]
        public double InformationEnemyNameFontScale { get; set; } = 0.70d;

        [DataMember(Order = 76)]
        public double InformationCritterNameFontScale { get; set; } = 0.70d;

        [DataMember(Order = 77)]
        public double InformationNpcNameFontScale { get; set; } = 0.70d;

        [DataMember(Order = 78)]
        public double InformationChestNameFontScale { get; set; } = 0.70d;

        [DataMember(Order = 143)]
        public double InformationSignTextFontScale { get; set; } = 0.70d;

        [DataMember(Order = 148)]
        public double InformationTombstoneTextFontScale { get; set; } = 0.70d;

        [DataMember(Order = 79)]
        public double InformationBiomeTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 80)]
        public double InformationWorldInfectionTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 81)]
        public double InformationLuckTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 82)]
        public double InformationFishingCatchesTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 107)]
        public double InformationFishingFilteredCatchesTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 83)]
        public double InformationAnglerQuestTextFontScale { get; set; } = 0.72d;

        [DataMember(Order = 90)]
        public List<string> InformationKnownChestKeys { get; set; } = new List<string>();

        [DataMember(Order = 91)]
        public bool FishingAutoFishEnabled { get; set; }

        [DataMember(Order = 92)]
        public bool FishingAutoLoadoutEnabled { get; set; }

        [DataMember(Order = 93)]
        public bool FishingAutoEquipmentEnabled { get; set; }

        [DataMember(Order = 94)]
        public bool FishingAutoStoreQuestFishEnabled { get; set; }

        [DataMember(Order = 95)]
        public string FishingAutoStoreMode { get; set; } = FishingAutoStoreModes.Off;

        [DataMember(Order = 96)]
        public string FishingFilterMode { get; set; } = FishingFilterModes.Disabled;

        [DataMember(Order = 97)]
        public string FishingFilterMatchMode { get; set; } = FishingFilterMatchModes.Exact;

        [DataMember(Order = 98)]
        public string FishingFilterCrateRule { get; set; } = FishingFilterSpecialRuleModes.Allow;

        [DataMember(Order = 99)]
        public string FishingFilterQuestFishRule { get; set; } = FishingFilterSpecialRuleModes.Allow;

        [DataMember(Order = 100)]
        public string FishingFilterEnemyRule { get; set; } = FishingFilterSpecialRuleModes.Deny;

        [DataMember(Order = 101)]
        public List<FishingFilterExactEntry> FishingFilterAllowExactEntries { get; set; } = new List<FishingFilterExactEntry>();

        [DataMember(Order = 102)]
        public List<FishingFilterExactEntry> FishingFilterDenyExactEntries { get; set; } = new List<FishingFilterExactEntry>();

        [DataMember(Order = 103)]
        public List<string> FishingFilterAllowKeywords { get; set; } = new List<string>();

        [DataMember(Order = 104)]
        public List<string> FishingFilterDenyKeywords { get; set; } = new List<string>();

        [DataMember(Order = 108)]
        public List<FishingFilterPreset> FishingFilterPresets { get; set; } = new List<FishingFilterPreset>();

        [DataMember(Order = 109)]
        public bool FishingFilterCutRodSkipEnabled { get; set; }

        [DataMember(Order = 110)]
        public bool FishingFilterCutRodSkipDefaultMigrated { get; set; }

        [DataMember(Order = 111)]
        public bool MovementSimulatedMultiJumpEnabled { get; set; }

        [DataMember(Order = 112)]
        public bool MovementContinuousDashEnabled { get; set; }

        [DataMember(Order = 113)]
        public bool MovementTeleportCorrectionEnabled { get; set; }

        [DataMember(Order = 115)]
        public string MovementContinuousDashMode { get; set; } = MovementContinuousDashModes.HoldDirection;

        [DataMember(Order = 116)]
        public bool MovementSafeLandingEnabled { get; set; }

        [DataMember(Order = 117)]
        public bool MovementSafeLandingOptionsDefaultMigrated { get; set; }

        [DataMember(Order = 118)]
        public bool MovementSafeLandingDoubleJumpEnabled { get; set; }

        [DataMember(Order = 119)]
        public bool MovementSafeLandingRocketBootsEnabled { get; set; }

        [DataMember(Order = 120)]
        public bool MovementSafeLandingWingsEnabled { get; set; }

        [DataMember(Order = 121)]
        public bool MovementSafeLandingHorseshoeEnabled { get; set; }

        [DataMember(Order = 122)]
        public bool MovementSafeLandingUmbrellaEnabled { get; set; }

        [DataMember(Order = 123)]
        public bool MovementSafeLandingGrappleEnabled { get; set; }

        [DataMember(Order = 124)]
        public bool MovementSafeLandingFlyingMountEnabled { get; set; }

        [DataMember(Order = 125)]
        public bool MovementSafeLandingFairyBootsEnabled { get; set; }

        [DataMember(Order = 126)]
        public bool MovementSafeLandingDamageReductionMountEnabled { get; set; }

        [DataMember(Order = 127)]
        public bool MovementSafeLandingCushionBlockEnabled { get; set; }

        [DataMember(Order = 128)]
        public bool MovementSafeLandingTeleportRodEnabled { get; set; }

        [DataMember(Order = 129)]
        public bool MovementSafeLandingFeatherfallPotionEnabled { get; set; }

        [DataMember(Order = 130)]
        public bool MovementSafeLandingGravityPotionEnabled { get; set; }

        [DataMember(Order = 131)]
        public bool MovementSafeLandingEndurancePotionEnabled { get; set; }

        [DataMember(Order = 132)]
        public bool MovementSafeLandingIronskinPotionEnabled { get; set; }

        [DataMember(Order = 133)]
        public bool MovementSafeLandingFlyingCarpetEnabled { get; set; }

        [DataMember(Order = 134)]
        public bool MovementSafeLandingPriority1ExpansionDefaultMigrated { get; set; }

        [DataMember(Order = 135)]
        public bool MovementSafeLandingGravityGlobeEnabled { get; set; }

        [DataMember(Order = 136)]
        public bool MovementSafeLandingGravityGlobeDefaultMigrated { get; set; }

        [DataMember(Order = 137)]
        public bool UiTextVerticalOffsetOverrideEnabled { get; set; }

        [DataMember(Order = 138)]
        public double UiTextVerticalOffsetOverride { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                ConfigVersion = 1,
                LogLevel = "Info",
                EnableTraceLog = false,
                EnableDiagnosticsOverlay = true,
                AllowSinglePlayerDirectFallback = false,
                AllowExperimentalFeatures = false,
                EnableDiagnosticInputTests = true,
                DiagnosticInputTestSlot = 0,
                AutoHealEnabled = false,
                AutoManaEnabled = false,
                AutoBuffEnabled = false,
                AutoHealThresholdPercent = 50,
                AutoManaThresholdPercent = 35,
                AutoHealCooldownTicks = 120,
                AutoManaCooldownTicks = 8,
                AutoBuffCooldownTicks = 1800,
                OperationWindowX = 420,
                OperationWindowY = 120,
                OperationWindowWidth = 520,
                OperationWindowHeight = 420,
                AutoBuffWhitelist = new List<BuffPotionWhitelistEntry>(),
                LegacyMainWindowX = 320,
                LegacyMainWindowY = 80,
                LegacyMainWindowWidth = 600,
                LegacyMainWindowHeight = 750,
                LegacySelectedPageId = "buff",
                AutoHealMode = "Off",
                AutoManaMode = "Off",
                AutoHealBlockedItemTypes = new List<int>(),
                AutoManaBlockedItemTypes = new List<int>(),
                AutoNurseEnabled = false,
                AutoStationBuffEnabled = false,
                AutoBuffFollowAddEnabled = false,
                AutoBuffFollowRemoveEnabled = false,
                CombatAimAssistRadius = 0,
                CombatAimTrackDummyEnabled = false,
                CombatAimMarkerEnabled = true,
                CombatAutoClickerEnabled = false,
                CombatFlailComboEnabled = false,
                CombatPhasebladeQuickSwitchEnabled = false,
                CombatPhasebladeQuickSwitchIntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks,
                CombatPerfectRevolverEnabled = false,
                CombatMagicStringClickerEnabled = false,
                CombatAutoFacingEnabled = false,
                CombatEquipmentWarningEnabled = false,
                CombatGoblinExecutionEnabled = false,
                AimRangeOrigin = CombatAimModes.RangeOriginPlayer,
                AimTargetPriority = CombatAimModes.TargetPriorityNearest,
                CursorAimRadius = 0,
                PlayerAimRadius = 0,
                ReleaseHoldTicks = 8,
                PersistentCursorAimEnabled = true,
                CombatAimSplitRadiusMigrated = true,
                InformationEnemyNameLabelsEnabled = false,
                InformationCritterNameLabelsEnabled = false,
                InformationNpcNameLabelsMode = "Off",
                InformationChestNameLabelsEnabled = false,
                InformationChestNameLabelsMode = "Off",
                InformationSignTextLabelsMode = InformationSignTextModes.Off,
                InformationSignTextMaxLines = InformationSignTextModes.DefaultLines,
                InformationSignTextMaxCharacters = InformationSignTextModes.DefaultCharacters,
                InformationHighlightLifeCrystalEnabled = false,
                InformationHighlightManaCrystalEnabled = false,
                InformationHighlightDigtoiseEnabled = false,
                InformationHighlightLifeFruitEnabled = false,
                InformationHighlightDragonEggEnabled = false,
                InformationBiomeDisplayEnabled = false,
                InformationWorldInfectionEnabled = false,
                InformationLuckValueEnabled = false,
                InformationFishingCatchesEnabled = false,
                InformationFishingFilteredCatchesEnabled = false,
                InformationAnglerQuestEnabled = false,
                InformationPanelX = 20,
                InformationPanelY = 320,
                InformationPanelPositionInitialized = false,
                InformationEnemyNameColor = "#CD5C5C",
                InformationCritterNameColor = "#5DADEC",
                InformationNpcNameColor = "#90EE90",
                InformationChestNameColor = "#FFA500",
                InformationSignTextColor = "#E6C16A",
                InformationLifeCrystalHighlightColor = "#FF69B4",
                InformationManaCrystalHighlightColor = "#66CCFF",
                MiscTravelMenuEnabled = false,
                MiscDeveloperDebugCommandsEnabled = true,
                MiscWorldGenDebugViewerEnabled = true,
                MiscQuickItemHotkeysEnabled = false,
                MiscAutoStackEnabled = false,
                MiscAutoSellEnabled = false,
                MiscAutoSellItemIds = new List<int> { 2337, 2338, 2339 },
                MiscAutoDiscardEnabled = false,
                MiscAutoDiscardItemIds = new List<int>(),
                MiscQuickReforgeEnabled = false,
                MiscQuickReforgePrefixes = new List<string>(),
                MiscAutoTaxCollectEnabled = false,
                MiscAutoMiningMode = "Off",
                MiscAutoCaptureCritterEnabled = false,
                MiscAutoCaptureCritterMode = AutoCaptureCritterModes.Off,
                MiscAutoCaptureCritterCategoryDefaultsMigrated = true,
                MiscAutoCaptureCritterBaitEnabled = true,
                MiscAutoCaptureCritterFairyEnabled = true,
                MiscAutoCaptureCritterGoldCritterEnabled = true,
                MiscAutoCaptureCritterGemCritterEnabled = true,
                MiscAutoCaptureCritterNormalCritterEnabled = true,
                MiscAutoCaptureCritterTruffleWormEnabled = true,
                MiscAutoCaptureCritterEmpressButterflyEnabled = true,
                MiscAutoCaptureCritterOtherEnabled = true,
                MiscAutoHarvestEnabled = false,
                MiscQuickBagOpenEnabled = false,
                MiscAutoDepositCoinsEnabled = false,
                MiscAutoExtractinatorEnabled = false,
                MiscKeepFavoritedEnabled = false,
                InformationLifeFruitHighlightColor = "#7CFC00",
                InformationDragonEggHighlightColor = "#9370DB",
                InformationBiomeTextColor = "#90EE90",
                InformationWorldInfectionTextColor = "#DDA0DD",
                InformationLuckTextColor = "#FAFAD2",
                InformationFishingCatchesTextColor = "#87CEFA",
                InformationFishingFilteredCatchesTextColor = "#FFB366",
                InformationAnglerQuestTextColor = "#E0FFFF",
                InformationEnemyNameFontScale = 0.70d,
                InformationCritterNameFontScale = 0.70d,
                InformationNpcNameFontScale = 0.70d,
                InformationChestNameFontScale = 0.70d,
                InformationSignTextFontScale = 0.70d,
                InformationBiomeTextFontScale = 0.72d,
                InformationWorldInfectionTextFontScale = 0.72d,
                InformationLuckTextFontScale = 0.72d,
                InformationFishingCatchesTextFontScale = 0.72d,
                InformationFishingFilteredCatchesTextFontScale = 0.72d,
                InformationAnglerQuestTextFontScale = 0.72d,
                InformationKnownChestKeys = new List<string>(),
                FishingAutoFishEnabled = false,
                FishingAutoLoadoutEnabled = false,
                FishingAutoEquipmentEnabled = false,
                FishingAutoStoreQuestFishEnabled = false,
                FishingAutoStoreMode = FishingAutoStoreModes.Off,
                FishingFilterMode = FishingFilterModes.Disabled,
                FishingFilterMatchMode = FishingFilterMatchModes.Exact,
                FishingFilterCrateRule = FishingFilterSpecialRuleModes.Allow,
                FishingFilterQuestFishRule = FishingFilterSpecialRuleModes.Allow,
                FishingFilterEnemyRule = FishingFilterSpecialRuleModes.Deny,
                FishingFilterAllowExactEntries = new List<FishingFilterExactEntry>(),
                FishingFilterDenyExactEntries = new List<FishingFilterExactEntry>(),
                FishingFilterAllowKeywords = new List<string>(),
                FishingFilterDenyKeywords = new List<string>(),
                FishingFilterPresets = new List<FishingFilterPreset>(),
                FishingFilterCutRodSkipEnabled = false,
                FishingFilterCutRodSkipDefaultMigrated = true,
                MovementSimulatedMultiJumpEnabled = false,
                MovementContinuousDashEnabled = false,
                MovementTeleportCorrectionEnabled = false,
                MovementContinuousDashMode = MovementContinuousDashModes.HoldDirection,
                MovementSafeLandingEnabled = false,
                MovementSafeLandingOptionsDefaultMigrated = true,
                MovementSafeLandingDoubleJumpEnabled = true,
                MovementSafeLandingRocketBootsEnabled = true,
                MovementSafeLandingWingsEnabled = true,
                MovementSafeLandingHorseshoeEnabled = true,
                MovementSafeLandingUmbrellaEnabled = true,
                MovementSafeLandingGrappleEnabled = true,
                MovementSafeLandingFlyingMountEnabled = true,
                MovementSafeLandingFairyBootsEnabled = true,
                MovementSafeLandingDamageReductionMountEnabled = true,
                MovementSafeLandingCushionBlockEnabled = true,
                MovementSafeLandingTeleportRodEnabled = true,
                MovementSafeLandingFeatherfallPotionEnabled = false,
                MovementSafeLandingGravityPotionEnabled = false,
                MovementSafeLandingEndurancePotionEnabled = false,
                MovementSafeLandingIronskinPotionEnabled = false,
                MovementSafeLandingFlyingCarpetEnabled = true,
                MovementSafeLandingPriority1ExpansionDefaultMigrated = true,
                MovementSafeLandingGravityGlobeEnabled = true,
                MovementSafeLandingGravityGlobeDefaultMigrated = true,
                UiTextVerticalOffsetOverrideEnabled = false,
                UiTextVerticalOffsetOverride = 0d
            };
        }
    }

    [DataContract]
    public sealed class BuffPotionWhitelistEntry
    {
        [DataMember(Order = 1)]
        public int ItemType { get; set; }

        [DataMember(Order = 2)]
        public int BuffType { get; set; }

        [DataMember(Order = 3)]
        public string ItemName { get; set; }

        [DataMember(Order = 4)]
        public string BuffName { get; set; }

        public BuffPotionWhitelistEntry()
        {
            ItemName = string.Empty;
            BuffName = string.Empty;
        }
    }
}
