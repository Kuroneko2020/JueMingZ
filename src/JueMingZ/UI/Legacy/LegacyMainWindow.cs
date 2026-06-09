using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int BuffPaneHeaderHeight = 44;
        private const int BuffPaneTitleHeight = 34;
        private const int BuffPaneHeaderButtonHeight = 22;
        private const int AutoBuffCooldownMin = 300;
        private const int AutoBuffCooldownMax = 3600;
        private const int CombatAimRowHeight = 82;
        private const int CombatAimRadiusMin = 0;
        private const int CombatAimRadiusMax = 50;
        private const int CombatSmallToggleHeight = 24;
        private const int CombatAimValueTextWidth = 112;
        private const int CombatAimValueTextHeight = 22;
        private const int InformationDividerHeight = 22;
        private const int InformationStyleConfigButtonWidth = 64;
        private const int InformationStylePopupWidth = 368;
        private const int InformationStylePopupHeight = 226;
        private const int InformationStyleSliderHeight = 30;
        private const int MovementSafeLandingPopupMaxWidth = 460;
        private const int MovementSafeLandingPopupMinWidth = 332;
        private const int MovementSafeLandingPopupMaxHeight = 320;
        private const int MovementSafeLandingPopupMinHeight = 168;
        private const int MovementSafeLandingPopupHorizontalPadding = 14;
        private const int MovementSafeLandingPopupContentStartY = 46;
        private const int MovementSafeLandingPopupBottomPadding = 12;
        private const int MovementSafeLandingPopupColumnGap = 10;
        private const int MovementSafeLandingPopupRowGap = 6;
        private const int MovementSafeLandingOptionMinWidth = 96;
        private const int MovementSafeLandingOptionHeight = 32;
        private const int AutoCaptureCritterPopupMaxWidth = 460;
        private const int AutoCaptureCritterPopupMinWidth = 332;
        private const int AutoCaptureCritterPopupMaxHeight = 260;
        private const int AutoCaptureCritterPopupMinHeight = 168;
        private const int AutoCaptureCritterPopupHorizontalPadding = 14;
        private const int AutoCaptureCritterPopupContentStartY = 46;
        private const int AutoCaptureCritterPopupBottomPadding = 12;
        private const int AutoCaptureCritterPopupColumnGap = 10;
        private const int AutoCaptureCritterPopupRowGap = 6;
        private const int AutoCaptureCritterOptionMinWidth = 112;
        private const int AutoCaptureCritterOptionHeight = 32;
        private const int AutoRecoveryItemPopupMaxWidth = 520;
        private const int AutoRecoveryItemPopupMinWidth = 360;
        private const int AutoRecoveryItemPopupMaxHeight = 360;
        private const int AutoRecoveryItemPopupMinHeight = 168;
        private const int AutoRecoveryItemPopupHorizontalPadding = 14;
        private const int AutoRecoveryItemPopupContentStartY = 46;
        private const int AutoRecoveryItemPopupBottomPadding = 12;
        private const int AutoRecoveryItemPopupColumnGap = 10;
        private const int AutoRecoveryItemPopupRowGap = 6;
        private const int AutoRecoveryItemOptionMinWidth = 132;
        private const int AutoRecoveryItemOptionHeight = 34;
        private const int FishingFilterPanelHeight = 296;
        private const int FishingFilterSettingsMinWidth = 132;
        private const int FishingFilterSettingsMaxWidth = 144;
        private const int FishingFilterModePaneHeight = 78;
        private const int FishingFilterSidePaneGap = 8;
        private const int RowModeButtonHeight = 24;
        private const int FishingFilterActionButtonHeight = RowModeButtonHeight + 4;
        private const int FishingFilterPickerMaxHeight = 177;
        private const int FishingFilterPickerHeaderHeight = 30;
        private const int FishingFilterPickerCandidateHeight = 28;
        private const int FishingFilterPresetMaxHeight = 168;
        private const int FishingFilterPresetRowHeight = 28;
        private const int FishingFilterGlobalSearchMaxResults = 96;
        private const int FishingFilterExactEntryHeight = 30;
        private const int FishingFilterKeywordEntryHeight = 28;
        private const int FishingFilterFloatingGap = 7;
        private const int FishingFilterEntryColumnGap = 6;
        private const int FishingFilterPickerColumnCount = 2;
        private const int FishingFilterCloseButtonSize = 20;
        private const int FishingFilterLinkR = 218;
        private const int FishingFilterLinkG = 198;
        private const int FishingFilterLinkB = 128;
        private const int FishingFilterLinkA = 220;
        private const int QuickItemCardHeight = 32;
        private const int QuickItemCardGap = 5;
        private const int QuickItemCardMinWidth = 160;
        private const int QuickItemIconCellSize = 20;
        private const int AutoSellGridColumnCount = 8;
        private const int AutoSellGridCellHeight = 32;
        private const int AutoSellGridCellMinWidth = 42;
        private const int AutoSellGridIconCellSize = 24;
        private const int AutoItemPickerColumnCount = 11;
        private const int AutoItemPickerCellGap = 4;
        private const int AutoItemPickerCellMinSize = 28;
        private const int QuickItemHotkeyCellMinWidth = 64;
        private const int QuickItemCaptureHintHeight = 28;
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkAlt = 0x12;
        private const string FishingQuickRenameTextInputId = "fishing-quick-rename:name";
        private const string MiscQuickReforgeTextInputId = "misc-quick-reforge:prefix";
        private const int RowLabelTextHeight = 20;
        private static readonly int[] QuickItemCaptureAdditionalKeys =
        {
            0x14,
            0x20,
            0x09,
            0x0D,
            0x1B,
            0x25,
            0x26,
            0x27,
            0x28,
            0x05,
            0x06
        };
        private static string _informationStylePopupFeatureId = string.Empty;
        private static LegacyUiRect _informationStylePopupAnchor;
        private static bool _informationStylePopupAnchorVisible;
        private static bool _movementSafeLandingConfigOpen;
        private static LegacyUiRect _movementSafeLandingConfigAnchor;
        private static bool _movementSafeLandingConfigAnchorVisible;
        private static bool _autoCaptureCritterConfigOpen;
        private static LegacyUiRect _autoCaptureCritterConfigAnchor;
        private static bool _autoCaptureCritterConfigAnchorVisible;
        private static string _autoRecoveryItemConfigKind = string.Empty;
        private static LegacyUiRect _autoRecoveryItemConfigAnchor;
        private static bool _autoRecoveryItemConfigAnchorVisible;
        private static bool _developerEasterEggConfirmPending;
        private static bool _worldGenerationDetailsHintAlternate;
        private static bool _quickItemPickerOpen;
        private static int _quickItemPickerBindingIndex = -1;
        private static bool _autoSellPickerOpen;
        private static int _autoSellPickerIndex = -1;
        private static bool _autoDiscardPickerOpen;
        private static int _autoDiscardPickerIndex = -1;
        private static bool _quickItemHotkeyCaptureActive;
        private static int _quickItemHotkeyCaptureBindingIndex = -1;
        private static bool _autoMiningHotkeyCaptureActive;
        private static readonly Dictionary<int, bool> QuickItemCaptureWasDown = new Dictionary<int, bool>();
        private static readonly Dictionary<int, bool> AutoMiningCaptureWasDown = new Dictionary<int, bool>();
        private static readonly TimeSpan PickerCandidateCacheWindow = TimeSpan.FromMilliseconds(120);
        private static DateTime _quickItemPickerCandidateCacheUtc = DateTime.MinValue;
        private static List<QuickItemInventoryCandidate> _quickItemPickerCandidateCache;
        private static DateTime _autoSellPickerCandidateCacheUtc = DateTime.MinValue;
        private static List<QuickItemInventoryCandidate> _autoSellPickerCandidateCache;
        private static DateTime _autoDiscardPickerCandidateCacheUtc = DateTime.MinValue;
        private static List<QuickItemInventoryCandidate> _autoDiscardPickerCandidateCache;
        private static readonly List<int> AutoSellPickerPendingItemTypes = new List<int>();
        private static readonly List<int> AutoDiscardPickerPendingItemTypes = new List<int>();
        private static readonly List<LegacyUiElement> FrameElements = new List<LegacyUiElement>(128);
        private static string _contentHeightCachePageId = string.Empty;
        private static int _contentHeightCacheWidth;
        private static int _contentHeightCacheHeight;
        private static int _contentHeightCacheSettingsVersion;
        private static int _contentHeightCacheSignature;
        private static int _contentHeightCacheValue;
        private static bool _contentHeightCacheValid;

    }
}
