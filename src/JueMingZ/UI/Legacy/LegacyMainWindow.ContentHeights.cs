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
        private static int CalculateBuffContentHeight(LegacyUiRect contentRect)
        {
            var viewportWidth = contentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8;
            var paneGap = LegacyUiMetrics.GridCellGap * 2;
            var paneWidth = Math.Max(220, (viewportWidth - paneGap) / 2);
            var availableRows = GridRows(paneWidth, Math.Max(1, LegacyMainUiState.AvailableCandidateCount));
            var whitelistRows = GridRows(paneWidth, Math.Max(1, LegacyMainUiState.WhitelistCount));
            var height = 0;
            height += LegacyUiMetrics.RowHeight * 5 + LegacyUiMetrics.SettingRowGap * 4 + LegacyUiMetrics.SectionGap;
            height += LegacyUiMetrics.SectionHeaderHeight + BuffPaneHeaderHeight + Math.Max(availableRows, whitelistRows) * (LegacyPotionGrid.CellHeight + LegacyUiMetrics.GridCellGap) + LegacyUiMetrics.GridCellGap * 2 + LegacyUiMetrics.SectionGap;
            height += 24;
            return height;
        }

        private static int CalculateCombatContentHeight()
        {
            return CombatAimRowHeight + LegacyUiMetrics.SettingRowGap * 5 + LegacyUiMetrics.RowHeight * 5 + 24;
        }

        private static int CalculateMiscContentHeight(LegacyUiRect contentRect)
        {
            var viewportWidth = Math.Max(120, contentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8);
            var bindings = ConfigService.HotkeySettings == null || ConfigService.HotkeySettings.QuickItemHotkeyBindings == null
                ? new List<QuickItemHotkeyBinding>()
                : ConfigService.HotkeySettings.QuickItemHotkeyBindings;
            var bindingCount = bindings.Count;
            var pickerCandidateCount = 0;
            if (_quickItemPickerOpen)
            {
                var quickItemCandidates = GetQuickItemPickerCandidates();
                pickerCandidateCount = quickItemCandidates == null ? 0 : quickItemCandidates.Count;
            }

            var quickItemPanelHeight = bindingCount > 0 || _quickItemHotkeyCaptureActive || _quickItemPickerOpen
                ? CalculateQuickItemPanelHeight(viewportWidth, bindingCount, _quickItemHotkeyCaptureActive, _quickItemPickerOpen, pickerCandidateCount)
                : 0;
            var autoSellItemIds = GetAutoSellItemIds();
            var autoSellPickerCandidates = _autoSellPickerOpen ? GetAutoSellPickerCandidates() : null;
            var autoSellPickerCandidateCount = autoSellPickerCandidates == null ? 0 : autoSellPickerCandidates.Count;
            var autoSellPanelHeight = autoSellItemIds.Count > 0 || _autoSellPickerOpen
                ? CalculateAutoSellPanelHeight(viewportWidth, autoSellItemIds.Count, _autoSellPickerOpen, autoSellPickerCandidateCount)
                : 0;
            var autoDiscardItemIds = GetAutoDiscardItemIds();
            var autoDiscardPickerCandidates = _autoDiscardPickerOpen ? GetAutoDiscardPickerCandidates() : null;
            var autoDiscardPickerCandidateCount = autoDiscardPickerCandidates == null ? 0 : autoDiscardPickerCandidates.Count;
            var autoDiscardPanelHeight = autoDiscardItemIds.Count > 0 || _autoDiscardPickerOpen
                ? CalculateAutoSellPanelHeight(viewportWidth, autoDiscardItemIds.Count, _autoDiscardPickerOpen, autoDiscardPickerCandidateCount)
                : 0;
            var quickReforgePrefixes = GetQuickReforgePrefixes();
            var quickReforgePanelHeight = quickReforgePrefixes.Count > 0
                ? CalculateQuickReforgePanelHeight(viewportWidth, quickReforgePrefixes.Count)
                : 0;
            return MiscExpandableRowHeight(quickItemPanelHeight) +
                   MiscExpandableRowHeight(autoSellPanelHeight) +
                   MiscExpandableRowHeight(autoDiscardPanelHeight) +
                   MiscExpandableRowHeight(quickReforgePanelHeight) +
                   (LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap) * 9 +
                   LegacyUiMetrics.RowHeight +
                   24;
        }

        private static int CalculateInformationContentHeight(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var rowCount = 18;
            if (ShouldDrawInformationSignTextLimitRow(settings.InformationSignTextLabelsMode))
            {
                rowCount++;
            }

            if (ShouldDrawInformationSignTextLimitRow(settings.InformationTombstoneTextLabelsMode))
            {
                rowCount++;
            }

            return LegacyUiMetrics.RowHeight * rowCount +
                   LegacyUiMetrics.SettingRowGap * Math.Max(0, rowCount - 1) +
                   InformationDividerHeight +
                   LegacyUiMetrics.SectionGap +
                   24;
        }

        internal static int CalculateInformationContentHeightForTesting(AppSettings settings)
        {
            return CalculateInformationContentHeight(settings);
        }

        private static int CalculateFishingContentHeight(LegacyUiRect contentRect)
        {
            var viewportWidth = contentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8;
            return LegacyUiMetrics.RowHeight * 5 +
                   LegacyUiMetrics.SettingRowGap * 4 +
                   LegacyUiMetrics.SectionGap +
                   CalculateFishingFilterLayoutHeight(viewportWidth) +
                   24;
        }

        private static int CalculateMovementContentHeight()
        {
            return LegacyUiMetrics.RowHeight * 5 + LegacyUiMetrics.SettingRowGap * 4 + 24;
        }
    }
}
