using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int BlueprintStatusBarHeight = 212;
        private const int BlueprintLibraryToolbarHeight = 34;
        private const int BlueprintLibraryPreviewHeight = 92;
        private const int BlueprintLibraryEmptyHeight = 58;
        private const int BlueprintLibraryRowHeight = 48;
        private const int BlueprintLibraryRowGap = 5;
        private const int BlueprintLibrarySubmenuHeaderHeight = 42;
        private const int BlueprintLibraryCardColumns = 2;
        private const int BlueprintLibraryCardHeight = 166;
        private const int BlueprintLibraryCardGap = 8;
        private const int BlueprintLibraryCardPadding = 8;
        private const int BlueprintLibraryCardButtonHeight = 22;
        private const int BlueprintLibraryCardButtonGap = 4;
        private const int BlueprintLibraryCardActionWidth = 38;
        private const int BlueprintLibraryMaterialButtonWidth = 58;
        private const float BlueprintLibraryCardSummaryTextScale = 0.62f;
        private const string BlueprintLibraryMaterialModalElementId = "blueprint-library-material-modal";
        private const int BlueprintLibraryMaterialModalMinWidth = 280;
        private const int BlueprintLibraryMaterialModalMaxWidth = 420;
        private const int BlueprintLibraryMaterialModalHeaderHeight = 42;
        private const int BlueprintLibraryMaterialModalRowHeight = 22;
        private const int BlueprintLibraryMaterialModalPadding = 14;
        private const int BlueprintLibraryMaterialModalColumnGap = 10;
        private const int BlueprintLibraryPreviewMaxDrawCells = 768;
        private const int BlueprintPlacedMaterialPanelMinHeight = 64;
        private const int BlueprintPlacedMaterialPanelHeaderHeight = 30;
        private const int BlueprintPlacedMaterialPanelRowHeight = 24;
        private const int BlueprintPlacedMaterialPanelBottomPadding = 10;
        private const float BlueprintPlacedMaterialLineTextScale = 0.84f;
        private const string BlueprintPlacedMaterialModalElementId = "blueprint-placed-material-modal";
        private const int BlueprintPlacedEmptyHeight = 58;
        private const int BlueprintPlacedCardColumns = BlueprintLibraryCardColumns;
        private const int BlueprintPlacedRowHeight = BlueprintLibraryCardHeight;
        private const int BlueprintPlacedRowGap = BlueprintLibraryCardGap;
        private const int BlueprintLibraryPreviewGridSize = 70;
        private const string BlueprintStatusBarElementId = "blueprint-status-bar";
        private const string BlueprintLibraryVisualContract = "main-menu-hotkey-open-row+same-content-submenu+stage06-two-column-fixed-cards+preview-scales-to-fit+stage07-name-edit-delete-confirm+stage08-import-export-windows-dialog+stage09-layout-use-real-template-snapshot+stage02-title-row-tools+card-material-toggle+card-material-modal+card-buttons-no-tooltips+summary-placed-count+summary-only+no-empty-gap-text+raw-gap-flags-hidden+larger-card-summary+use-closes-f5+mutual-submenus+hit-owned+state-paged-6+scroll-header-nonblocking+material-modal-body-scroll";
        private const string BlueprintPlacedInstanceVisualContract = "main-menu-open-row+same-content-submenu+current-world-list+paged-5+stage03-title-count+stage03-header-page-back+stage03-material-total-all-lines+stage03-card-preview+stage03-library-sized-cards+read-only-name+hide-show-cancel-place+card-material-modal+template-snapshot-isolated+material-comparison+materials-cache-only+scroll-header-nonblocking+material-modal-body-scroll";
        private const string BlueprintCreationVisualContract = "world-mask+single-toggle+drag-toggle+multi-region+ui-owned-consume+world-hover+air-select+low-alpha-no-border+continuous-mask+clear-selection+finish-cancel+placement-preview+center-anchor+left-click-instance";
        private const string BlueprintProjectionVisualContract = "world-projection+appearance-ghost+original-missing+red-conflict+gray-unavailable+fulfilled-no-mask+completed-progress+no-cell-border+move-floating-follow-preview+hidden-skip+layer-cover+wire-actuator-original-color+missing-no-state-block+multitile-object-group-conflict";
        private const string BlueprintMaterialVisualContract = "aggregate-materials+main-inventory+void-bag+floating-window+placed-list-comparison+cache-only-draw";
        private const string BlueprintEraseVisualContract = "instance-erase-region+selected-priority+top-layer-fallback+store-mask-only+stage07-continuous-region-edit+cursor-red-follow-mask+cancel-only-exit";
        private const int BlueprintTopSettingRowCount = 3;
        private const int BlueprintActionShortcutRowCount = 6;
        private const int BlueprintMenuOpenRowCount = 1;
        private const int BlueprintActionShortcutOuterPadding = 10;
        private const int BlueprintActionShortcutControlGap = 6;
        private const int BlueprintActionShortcutLabelMinWidth = 60;
        private const int BlueprintActionHotkeyInputMinWidth = 86;
        private const int BlueprintActionHotkeyInputMaxWidth = 132;
        private const string BlueprintEntryElementPrefix = "blueprint-entry:";
        private const string BlueprintActionHotkeyElementPrefix = "blueprint-action-hotkey:";
        private const string BlueprintActionEntryElementPrefix = "blueprint-action-entry:";
        private const string BlueprintActionShortcutVisualContract = "stage06-f5-create-save-move-library-rows+stage07-region-modify-row+stage08-mirror-row+short-hotkey-fields+right-adjacent-hotkey-fields+full-label-space+auto-mining-hotkey-shape+action-hotkeys-not-blueprint-main+real-create-save-library-entry+real-move-entry+real-region-entry+real-mirror-entry+start-create-toggle-exit-label+move-toggle-cancel-label+region-toggle-cancel-label+mirror-toggle-cancel-label";
        private const string BlueprintActionHotkeyTooltipPrimary = "双击录入采集按键。";
        private const string BlueprintActionHotkeyTooltipCancel = "Esc 取消录入。";
        private const string BlueprintActionHotkeyTooltipClear = "Backspace 删除绑定。";
        private const string BlueprintCreateActionButtonText = "开始";
        private const string BlueprintCreateActionButtonTooltip = "左键按住滑动选区，可多选";
        private const string BlueprintExitCreateActionButtonText = "退出";
        private const string BlueprintExitCreateActionButtonTooltip = "退出创建蓝图";
        private const string BlueprintSaveActionButtonTooltip = "保存当前选区为蓝图";
        private const string BlueprintMoveActionButtonText = "开始";
        private const string BlueprintMoveActionButtonTooltip = "点击蓝图使其进入浮动状态重新放置";
        private const string BlueprintMoveActionDisabledTooltip = "当前世界没有可移动的已放置蓝图。";
        private const string BlueprintCancelMoveActionButtonText = "取消";
        private const string BlueprintCancelMoveActionButtonTooltip = "取消移动并回到原位置";
        private const string BlueprintRegionActionButtonText = "开始";
        private const string BlueprintRegionActionButtonTooltip = "修改已放置的蓝图";
        private const string BlueprintRegionActionDisabledTooltip = "当前世界没有可修改的已放置蓝图。";
        private const string BlueprintCancelRegionActionButtonText = "取消";
        private const string BlueprintCancelRegionActionButtonTooltip = "取消蓝图修改";
        private const string BlueprintMirrorActionButtonText = "开始";
        private const string BlueprintMirrorActionButtonTooltip = "选择已放置蓝图并水平镜像一次";
        private const string BlueprintMirrorActionDisabledTooltip = "当前世界没有可镜像的已放置蓝图。";
        private const string BlueprintCancelMirrorActionButtonText = "取消";
        private const string BlueprintCancelMirrorActionButtonTooltip = "取消镜像选择";
        private const string BlueprintLibraryActionButtonTooltip = "打开蓝图库。";
        private const string BlueprintReplacementConfigPopupElementId = "blueprint-replacement-config-popup";
        private const string BlueprintReplacementVisualContract = "same-kind-replacement+config-popup+8-disabled-default-categories";
        private const string BlueprintAutoPlacementVisualContract = "auto-placement-stage15-replacement-rules+dependency-order+item-use-bridge+projection-verification+main-inventory-execution+void-bag-fail-closed";
        // 镜像已从 fail-closed 改为尝试翻转 frame（TileObjectData 方向查表），不可翻转时清零回退。 
        private const string BlueprintMirrorVisualContract = "preview-horizontal-mirror+anchor-flip+slope-map+tileObjectData-direction-flip+frame-reset-fallback";
        private static readonly BlueprintReplacementOptionDefinition[] BlueprintReplacementOptions =
        {
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Torch, "火把", "缺少原火把时允许使用同类火把。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Platform, "平台", "缺少原平台时允许使用同类平台。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.WorkBench, "工作台", "缺少原工作台时允许使用同类工作台。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Chair, "椅子", "缺少原椅子时允许使用同类椅子。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Door, "门", "缺少原关门时允许使用同类关门。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Table, "桌子", "缺少原桌子时允许使用同类桌子。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Chest, "箱子", "缺少原箱子时允许使用同类箱子。"),
            new BlueprintReplacementOptionDefinition(BlueprintReplacementCategories.Sign, "牌子", "缺少原牌子时允许使用同类牌子。")
        };
        private static readonly object BlueprintMaterialModalScrollSyncRoot = new object();
        private static string _blueprintLibraryMaterialModalScrollTemplateId = string.Empty;
        private static int _blueprintLibraryMaterialModalScrollOffset;
        private static LegacyUiRect _blueprintLibraryMaterialModalScrollViewport;
        private static int _blueprintLibraryMaterialModalMaxScroll;
        private static string _blueprintPlacedMaterialModalScrollInstanceId = string.Empty;
        private static int _blueprintPlacedMaterialModalScrollOffset;
        private static LegacyUiRect _blueprintPlacedMaterialModalScrollViewport;
        private static int _blueprintPlacedMaterialModalMaxScroll;

        private sealed class BlueprintReplacementOptionDefinition
        {
            public BlueprintReplacementOptionDefinition(string id, string label, string tooltip)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Tooltip = tooltip ?? string.Empty;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string Tooltip { get; private set; }
        }

        private sealed class BlueprintLibraryMaterialModalState
        {
            public BlueprintLibraryMaterialModalState(LegacyScrollArea area, BlueprintTemplateRecord template)
            {
                Area = area;
                Template = template;
            }

            public LegacyScrollArea Area { get; private set; }
            public BlueprintTemplateRecord Template { get; private set; }
        }

        private sealed class BlueprintPlacedMaterialModalState
        {
            public BlueprintPlacedMaterialModalState(LegacyScrollArea area, BlueprintWorldInstanceRecord instance)
            {
                Area = area;
                Instance = instance;
            }

            public LegacyScrollArea Area { get; private set; }
            public BlueprintWorldInstanceRecord Instance { get; private set; }
        }

        private static LegacyUiElement DrawBlueprintPage(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements)
        {
            UpdateBlueprintEntryHotkeyCapture();

            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;
            _blueprintReplacementConfigAnchorVisible = false;
            if (BlueprintLibraryUiState.IsOpen)
            {
                return DrawBlueprintLibrarySubmenu(spriteBatch, area, mouse, elements);
            }

            var entrySnapshot = BlueprintEntryState.GetSnapshot(settings);
            if (entrySnapshot != null && string.Equals(entrySnapshot.Mode, BlueprintEntryModes.PlacedManagement, StringComparison.Ordinal))
            {
                return DrawBlueprintPlacedSubmenu(spriteBatch, area, mouse, elements);
            }

            hovered = DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "快捷入口",
                settings.BlueprintHandheldEntryEnabled,
                "blueprint-handheld-entry-mode:",
                "手持凝胶显示快捷开始栏") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "自动放置",
                settings.BlueprintAutoPlacementEnabled,
                "blueprint-auto-placement-mode:",
                null) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBlueprintReplacementRow(spriteBatch, area, mouse, elements, y, settings.BlueprintReplacementEnabled) ?? hovered;
            RegisterBlueprintReplacementConfigPopupOverlay(area, settings);
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            var createActionIsExit = IsBlueprintCreateActionExitState(settings);
            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "创建蓝图",
                FeatureIds.BlueprintCreateAction,
                BlueprintEntryCommands.StartCreate,
                "双击采集按键",
                "按下采集按键...",
                createActionIsExit ? BlueprintExitCreateActionButtonText : BlueprintCreateActionButtonText,
                createActionIsExit ? BlueprintExitCreateActionButtonTooltip : BlueprintCreateActionButtonTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "保存蓝图",
                FeatureIds.BlueprintSaveAction,
                BlueprintEntryCommands.FinishCreateSave,
                "双击保存按键",
                "按下保存按键...",
                "保存",
                BlueprintSaveActionButtonTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            var moveActionIsCancel = IsBlueprintMoveActionCancelState();
            var moveActionEnabled = moveActionIsCancel || HasBlueprintPlacedInstancesForActionShortcut();
            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "移动已放置蓝图",
                FeatureIds.BlueprintMoveAction,
                BlueprintEntryCommands.StartMove,
                "双击移动按键",
                "按下移动按键...",
                moveActionIsCancel ? BlueprintCancelMoveActionButtonText : BlueprintMoveActionButtonText,
                moveActionIsCancel ? BlueprintCancelMoveActionButtonTooltip : BlueprintMoveActionButtonTooltip,
                moveActionEnabled,
                moveActionEnabled ? string.Empty : BlueprintMoveActionDisabledTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            var regionActionIsCancel = IsBlueprintRegionActionCancelState();
            var regionActionEnabled = regionActionIsCancel || HasBlueprintPlacedInstancesForActionShortcut();
            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "修改已放置蓝图区域",
                FeatureIds.BlueprintRegionAction,
                BlueprintEntryCommands.StartRegionModify,
                "双击修改按键",
                "按下修改按键...",
                regionActionIsCancel ? BlueprintCancelRegionActionButtonText : BlueprintRegionActionButtonText,
                regionActionIsCancel ? BlueprintCancelRegionActionButtonTooltip : BlueprintRegionActionButtonTooltip,
                regionActionEnabled,
                regionActionEnabled ? string.Empty : BlueprintRegionActionDisabledTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            var mirrorActionIsCancel = IsBlueprintMirrorActionCancelState();
            var mirrorActionEnabled = mirrorActionIsCancel || HasBlueprintPlacedInstancesForActionShortcut();
            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "镜像已放置蓝图",
                FeatureIds.BlueprintMirrorAction,
                BlueprintEntryCommands.StartMirror,
                "双击镜像按键",
                "按下镜像按键...",
                mirrorActionIsCancel ? BlueprintCancelMirrorActionButtonText : BlueprintMirrorActionButtonText,
                mirrorActionIsCancel ? BlueprintCancelMirrorActionButtonTooltip : BlueprintMirrorActionButtonTooltip,
                mirrorActionEnabled,
                mirrorActionEnabled ? string.Empty : BlueprintMirrorActionDisabledTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SectionGap;

            hovered = DrawBlueprintActionShortcutRow(
                spriteBatch,
                area,
                mouse,
                elements,
                y,
                "蓝图库",
                FeatureIds.BlueprintLibraryAction,
                BlueprintEntryCommands.OpenLibrary,
                "双击打开按键",
                "按下打开按键...",
                "打开",
                BlueprintLibraryActionButtonTooltip) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SectionGap;

            hovered = DrawBlueprintOpenRow(spriteBatch, area, mouse, elements, y, "已放置蓝图列表", BlueprintEntryCommands.OpenPlacedInstances, "打开已放置蓝图列表。") ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawBlueprintActionShortcutRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            string label,
            string hotkeyTargetId,
            string action,
            string emptyText,
            string capturingText,
            string buttonText,
            string buttonTooltip,
            bool buttonEnabled = true,
            string buttonDisabledTooltip = "")
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);

            LegacyUiRect labelRect;
            LegacyUiRect inputRect;
            LegacyUiRect buttonRect;
            CalculateBlueprintActionShortcutRowRects(row, buttonText, out labelRect, out inputRect, out buttonRect);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, labelRect.X, labelRect.Y, labelRect.Width, labelRect.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, LegacyUiMetrics.RowLabelTextScale);
            var capturing = _blueprintEntryHotkeyCaptureActive &&
                            string.Equals(NormalizeBlueprintHotkeyTargetId(_blueprintHotkeyCaptureTargetId), NormalizeBlueprintHotkeyTargetId(hotkeyTargetId), StringComparison.Ordinal);
            var hotkey = GetUnifiedHotkeyDisplay(UnifiedHotkeyBindingIds.ForBlueprintAction(hotkeyTargetId));
            var inputText = capturing
                ? capturingText
                : string.IsNullOrWhiteSpace(hotkey) ? emptyText : hotkey;

            var hovered = (LegacyUiElement)null;
            hovered = DrawBlueprintActionHotkeyInput(spriteBatch, mouse, elements, area.Viewport, inputRect, hotkeyTargetId, label, inputText, capturing) ?? hovered;
            hovered = DrawBlueprintActionButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                buttonRect,
                action,
                label,
                buttonText,
                buttonEnabled,
                buttonEnabled || string.IsNullOrWhiteSpace(buttonDisabledTooltip) ? buttonTooltip : buttonDisabledTooltip) ?? hovered;
            return hovered;
        }

        private static void CalculateBlueprintActionShortcutRowRects(
            LegacyUiRect row,
            string buttonText,
            out LegacyUiRect labelRect,
            out LegacyUiRect inputRect,
            out LegacyUiRect buttonRect)
        {
            var buttonY = RowModeButtonY(row);
            var buttonWidth = ModeButtonWidth(buttonText);
            buttonRect = new LegacyUiRect(
                row.Right - buttonWidth - BlueprintActionShortcutOuterPadding,
                buttonY,
                buttonWidth,
                RowModeButtonHeight);

            var labelX = row.X + BlueprintActionShortcutOuterPadding;
            var inputAvailableWidth =
                buttonRect.X -
                BlueprintActionShortcutControlGap -
                labelX -
                BlueprintActionShortcutLabelMinWidth -
                BlueprintActionShortcutControlGap;
            var inputWidth = inputAvailableWidth >= BlueprintActionHotkeyInputMinWidth
                ? ResolveBlueprintActionHotkeyInputWidth(inputAvailableWidth)
                : Math.Max(0, inputAvailableWidth);
            inputRect = new LegacyUiRect(
                buttonRect.X - BlueprintActionShortcutControlGap - inputWidth,
                buttonY,
                inputWidth,
                RowModeButtonHeight);

            var labelWidth = Math.Max(
                BlueprintActionShortcutLabelMinWidth,
                inputRect.X - labelX - BlueprintActionShortcutControlGap);
            labelRect = new LegacyUiRect(labelX, row.Y, labelWidth, row.Height);
        }

        private static LegacyUiElement DrawBlueprintActionHotkeyInput(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string hotkeyTargetId,
            string label,
            string text,
            bool capturing)
        {
            var elementId = BlueprintActionHotkeyElementPrefix + NormalizeBlueprintHotkeyTargetId(hotkeyTargetId);
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse != null && mouse.LeftDown, capturing, true, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, capturing, true);
            var scale = ResolveAutoMiningInputScale(text, rect.Width - 16);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                text ?? string.Empty,
                rect.X + 8,
                contentRect.Y + 3,
                rect.Width - 16,
                Math.Max(1, contentRect.Height - 6),
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                capturing ? 255 : 230,
                capturing ? 245 : 232,
                capturing ? 205 : 224,
                255,
                scale);
            if (capturing)
            {
                LegacyUiTheme.DrawSelectedTextMarkersClipped(
                    spriteBatch,
                    new LegacyUiRect(rect.X + 8, contentRect.Y + 3, rect.Width - 16, Math.Max(1, contentRect.Height - 6)),
                    clip,
                    text ?? string.Empty,
                    scale);
            }

            var element = AddFrameElement(elements, elementId, "蓝图:" + label + ":快捷键", "button", elementRect, selected: capturing, tooltipLines: GetBlueprintActionHotkeyTooltipLines());
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static int ResolveBlueprintActionHotkeyInputWidth(int availableWidth)
        {
            return Math.Max(BlueprintActionHotkeyInputMinWidth, Math.Min(BlueprintActionHotkeyInputMaxWidth, availableWidth));
        }

        private static LegacyUiElement DrawBlueprintActionButton(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string action,
            string label,
            string text,
            bool enabled,
            string tooltip)
        {
            var elementId = BlueprintActionEntryElementPrefix + (action ?? string.Empty);
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = enabled && IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse != null && mouse.LeftDown, false, enabled, clip);
            var contentRect = LegacyUiTheme.GetSelectedButtonContentRect(rect, false, enabled);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, text, rect.X + 3, contentRect.Y, rect.Width - 6, contentRect.Height, clip.X, clip.Y, clip.Width, clip.Height, enabled ? 230 : 150, enabled ? 232 : 156, enabled ? 224 : 170, 255, LegacyUiMetrics.RowButtonTextScale);
            var element = AddFrameElement(elements, elementId, "蓝图:" + label + ":" + text, "button", elementRect, enabled: enabled, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static string[] GetBlueprintActionHotkeyTooltipLines()
        {
            return new[] { BlueprintActionHotkeyTooltipPrimary, BlueprintActionHotkeyTooltipCancel, BlueprintActionHotkeyTooltipClear };
        }

        private static bool IsBlueprintCreateActionExitState(AppSettings settings)
        {
            var snapshot = BlueprintEntryState.GetSnapshot(settings ?? AppSettings.CreateDefault());
            return snapshot != null &&
                   string.Equals(snapshot.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal);
        }

        private static string GetBlueprintCreateActionButtonText(AppSettings settings)
        {
            return IsBlueprintCreateActionExitState(settings)
                ? BlueprintExitCreateActionButtonText
                : BlueprintCreateActionButtonText;
        }

        private static string GetBlueprintCreateActionButtonTooltip(AppSettings settings)
        {
            return IsBlueprintCreateActionExitState(settings)
                ? BlueprintExitCreateActionButtonTooltip
                : BlueprintCreateActionButtonTooltip;
        }

        private static bool IsBlueprintMoveActionCancelState()
        {
            var snapshot = BlueprintPlacedInstanceTransformState.GetSnapshot();
            return snapshot != null &&
                   snapshot.Active &&
                   string.Equals(snapshot.Mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal);
        }

        private static bool IsBlueprintRegionActionCancelState()
        {
            var snapshot = BlueprintEraseRegionState.GetSnapshot();
            return snapshot != null && snapshot.Active;
        }

        private static bool IsBlueprintMirrorActionCancelState()
        {
            var snapshot = BlueprintPlacedInstanceTransformState.GetSnapshot();
            return snapshot != null &&
                   snapshot.Active &&
                   string.Equals(snapshot.Mode, BlueprintPlacedInstanceTransformModes.Mirror, StringComparison.Ordinal);
        }

        private static bool HasBlueprintPlacedInstancesForActionShortcut()
        {
            var placed = BlueprintPlacedInstanceUiState.GetCachedSummary();
            var projection = BlueprintProjectionService.GetDiagnostics();
            return BlueprintPlacedInstanceActivity.HasActionablePlacedBlueprint(
                placed == null ? 0 : placed.InstanceCount,
                projection);
        }

        private static LegacyUiElement DrawBlueprintOpenRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            string label,
            string command,
            string tooltip)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                label,
                string.Empty,
                new[] { "打开" },
                new[] { command },
                BlueprintEntryElementPrefix,
                new[] { tooltip });
        }

        private static LegacyUiElement DrawBlueprintLibrarySubmenu(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements)
        {
            var snapshot = BlueprintLibraryUiState.GetSnapshot();
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(0), area.Viewport.Width, BlueprintLibrarySubmenuHeaderHeight);
            var hovered = (LegacyUiElement)null;
            if (IsBlueprintSubmenuHeaderVisible(area))
            {
                LegacyUiRect importRect;
                LegacyUiRect prevRect;
                LegacyUiRect nextRect;
                LegacyUiRect backRect;
                CalculateBlueprintLibraryHeaderButtonRects(row, out importRect, out prevRect, out nextRect, out backRect);

                LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    "蓝图库",
                    row.X + 10,
                    row.Y + 5,
                    Math.Max(1, importRect.X - row.X - 18),
                    16,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    238,
                    238,
                    226,
                    255,
                    0.76f);
                var count = snapshot == null || snapshot.Templates == null ? 0 : snapshot.Templates.Count;
                var placed = BlueprintPlacedInstanceUiState.GetCachedSummary();
                var placedCount = placed == null ? 0 : placed.InstanceCount;
                var detail = BuildBlueprintLibraryHeaderSummary(count, placedCount);

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    detail,
                    row.X + 10,
                    row.Y + 24,
                    Math.Max(1, importRect.X - row.X - 18),
                    12,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    218,
                    238,
                    230,
                    0.54f);

                hovered = DrawBlueprintSmallButton(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    importRect,
                    BlueprintLibraryUiState.BuildCommandId("import", string.Empty),
                    "导入",
                    null) ?? hovered;
                hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, prevRect, "blueprint-library:page-prev", "上一页", null) ?? hovered;
                hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, nextRect, "blueprint-library:page-next", "下一页", null) ?? hovered;
                hovered = DrawBlueprintSmallButton(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    backRect,
                    BlueprintLibraryUiState.BuildCommandId("back", string.Empty),
                    "返回",
                    "返回蓝图主菜单") ?? hovered;
            }

            var y = BlueprintLibrarySubmenuHeaderHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBlueprintLibraryList(spriteBatch, area, mouse, elements, y, snapshot) ?? hovered;
            RegisterBlueprintLibraryMaterialModalOverlay(area, snapshot);
            return hovered;
        }

        private static LegacyUiElement DrawBlueprintPlacedSubmenu(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements)
        {
            var snapshot = BlueprintPlacedInstanceUiState.GetSnapshot();
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(0), area.Viewport.Width, BlueprintLibrarySubmenuHeaderHeight);
            var hovered = (LegacyUiElement)null;
            if (IsBlueprintSubmenuHeaderVisible(area))
            {
                LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
                LegacyUiRect prevRect;
                LegacyUiRect nextRect;
                LegacyUiRect backRect;
                CalculateBlueprintPlacedHeaderButtonRects(row, out prevRect, out nextRect, out backRect);
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    "已放置蓝图列表",
                    row.X + 10,
                    row.Y + 5,
                    Math.Max(1, prevRect.X - row.X - 18),
                    16,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    238,
                    238,
                    226,
                    255,
                    0.76f);
                var count = snapshot == null || snapshot.Instances == null ? 0 : snapshot.Instances.Count;
                var detail = snapshot != null && snapshot.LoadSucceeded
                    ? BuildBlueprintPlacedHeaderSummary(count)
                    : "当前世界实例读取失败";

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    detail,
                    row.X + 10,
                    row.Y + 24,
                    Math.Max(1, prevRect.X - row.X - 18),
                    12,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    218,
                    238,
                    230,
                    0.54f);

                hovered = DrawBlueprintSmallButton(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    prevRect,
                    BlueprintPlacedInstanceUiState.BuildCommandId("page-prev", string.Empty),
                    "上一页",
                    null) ?? hovered;
                hovered = DrawBlueprintSmallButton(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    nextRect,
                    BlueprintPlacedInstanceUiState.BuildCommandId("page-next", string.Empty),
                    "下一页",
                    null) ?? hovered;
                hovered = DrawBlueprintSmallButton(
                    spriteBatch,
                    mouse,
                    elements,
                    area.Viewport,
                    backRect,
                    BlueprintEntryElementPrefix + BlueprintEntryCommands.Cancel,
                    "返回",
                    "返回蓝图主菜单") ?? hovered;
            }

            var y = BlueprintLibrarySubmenuHeaderHeight + LegacyUiMetrics.SettingRowGap;
            var materialHeight = CalculateBlueprintPlacedMaterialPanelHeight(BlueprintMaterialService.GetCachedSnapshotForDraw());
            hovered = DrawBlueprintPlacedMaterialPanel(spriteBatch, area, y) ?? hovered;
            y += materialHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBlueprintPlacedList(spriteBatch, area, mouse, elements, y, snapshot) ?? hovered;
            RegisterBlueprintPlacedMaterialModalOverlay(area, snapshot);
            return hovered;
        }

        private static bool IsBlueprintSubmenuHeaderVisible(LegacyScrollArea area)
        {
            if (area == null)
            {
                return false;
            }

            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(0), area.Viewport.Width, BlueprintLibrarySubmenuHeaderHeight);
            return area.IsVisible(row);
        }

        private static LegacyUiElement DrawBlueprintReplacementRow(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            bool enabled)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var labels = new[] { "配置", "开启", "关闭" };
            var values = new[] { "Config", "On", "Off" };
            var totalWidth = 0;
            for (var index = 0; index < labels.Length; index++)
            {
                totalWidth += ModeButtonWidth(labels[index]);
                if (index > 0)
                {
                    totalWidth += 6;
                }
            }

            var x = row.Right - totalWidth - 10;
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            LegacySettingRowControl.DrawBackgroundAndLabel(context, row, "同类替换", x);

            var hovered = (LegacyUiElement)null;
            var buttonY = RowModeButtonY(row);
            for (var index = 0; index < labels.Length; index++)
            {
                var width = ModeButtonWidth(labels[index]);
                var rect = new LegacyUiRect(x, buttonY, width, RowModeButtonHeight);
                var selected = index == 0 ? _blueprintReplacementConfigOpen : enabled == string.Equals(values[index], "On", StringComparison.Ordinal);
                var element = new LegacyButtonControl
                {
                    Id = "blueprint-replacement-mode:" + values[index],
                    Label = labels[index],
                    Text = labels[index],
                    ElementLabel = "同类替换:" + labels[index],
                    Kind = "button",
                    Bounds = rect,
                    Selected = selected,
                    TextScale = LegacyUiMetrics.RowButtonTextScale,
                    TooltipLines = index == 0
                        ? new[] { "选择允许同类替换的物品类别" }
                        : index == 1
                            ? new[] { "自动放置允许使用同类物品" }
                            : new[] { "关闭同类替换" }
                }.Draw(context);
                if (index == 0 && _blueprintReplacementConfigOpen)
                {
                    _blueprintReplacementConfigAnchor = rect;
                    _blueprintReplacementConfigAnchorVisible = true;
                }

                if (element != null && context.IsElementHovered(element.Id, rect))
                {
                    hovered = element;
                }

                x += width + 6;
            }

            return context.HoveredElement ?? hovered;
        }

        private static bool RegisterBlueprintReplacementConfigPopupOverlay(LegacyScrollArea area, AppSettings settings)
        {
            if (!_blueprintReplacementConfigOpen || !_blueprintReplacementConfigAnchorVisible || area == null)
            {
                return false;
            }

            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateBlueprintReplacementPopupRect(
                area.Viewport,
                _blueprintReplacementConfigAnchor,
                BlueprintReplacementOptions.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = BlueprintReplacementConfigPopupElementId,
                OwnerPageId = "blueprint",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 20,
                CacheSignature = BuildBlueprintReplacementPopupCacheSignature(settings),
                State = area,
                Draw = DrawBlueprintReplacementConfigPopupOverlay
            });
        }

        private static void DrawBlueprintReplacementConfigPopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var area = request == null ? null : request.State as LegacyScrollArea;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || area == null || elements == null)
            {
                return;
            }

            DrawBlueprintReplacementConfigPopup(context.SpriteBatch, area, context.Mouse, elements);
        }

        private static LegacyUiElement DrawBlueprintReplacementConfigPopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            if (!_blueprintReplacementConfigOpen || !_blueprintReplacementConfigAnchorVisible || area == null)
            {
                return null;
            }

            int columns;
            int optionWidth;
            int columnGap;
            int rowGap;
            var popup = CalculateBlueprintReplacementPopupRect(
                area.Viewport,
                _blueprintReplacementConfigAnchor,
                BlueprintReplacementOptions.Length,
                out columns,
                out optionWidth,
                out columnGap,
                out rowGap);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = BlueprintReplacementConfigPopupElementId,
                Label = "同类替换配置",
                Kind = "blocker",
                Bounds = popup
            }.Draw(context);

            UiTextRenderer.DrawText(spriteBatch, "同类替换配置", popup.X + 16, popup.Y + 11, 238, 238, 226, 255, 0.82f);
            var hovered = (LegacyUiElement)null;
            var close = new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20);
            hovered = DrawBlueprintReplacementSmallButton(context, close, "blueprint-replacement-mode:Config", "关闭", "关闭配置") ?? hovered;

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var startX = popup.X + BlueprintReplacementPopupHorizontalPadding;
            var startY = popup.Y + BlueprintReplacementPopupContentStartY;
            for (var index = 0; index < BlueprintReplacementOptions.Length; index++)
            {
                var option = BlueprintReplacementOptions[index];
                if (option == null)
                {
                    continue;
                }

                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    startX + column * (optionWidth + columnGap),
                    startY + row * (BlueprintReplacementOptionHeight + rowGap),
                    optionWidth,
                    BlueprintReplacementOptionHeight);
                hovered = DrawBlueprintReplacementOption(context, rect, option, GetBlueprintReplacementCategoryEnabled(settings, option.Id)) ?? hovered;
            }

            return hovered;
        }

        private static int BuildBlueprintReplacementPopupCacheSignature(AppSettings settings)
        {
            unchecked
            {
                settings = settings ?? AppSettings.CreateDefault();
                var hash = 17;
                hash = hash * 31 + settings.ConfigVersion;
                hash = hash * 31 + BlueprintReplacementOptions.Length;
                for (var index = 0; index < BlueprintReplacementOptions.Length; index++)
                {
                    var option = BlueprintReplacementOptions[index];
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(option == null ? string.Empty : option.Id ?? string.Empty);
                    hash = hash * 31 + (option != null && GetBlueprintReplacementCategoryEnabled(settings, option.Id) ? 1 : 0);
                }

                return hash;
            }
        }

        private static LegacyUiElement DrawBlueprintReplacementSmallButton(LegacyUiContext context, LegacyUiRect rect, string id, string label, string tooltip)
        {
            var element = new LegacySmallButtonControl
            {
                Id = id,
                Label = label,
                Kind = "button",
                Bounds = rect,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static LegacyUiElement DrawBlueprintReplacementOption(LegacyUiContext context, LegacyUiRect rect, BlueprintReplacementOptionDefinition option, bool enabled)
        {
            var element = new LegacyCheckboxButtonControl
            {
                Id = "blueprint-replacement-category:" + option.Id,
                Label = option.Label,
                Kind = "button",
                Bounds = rect,
                Selected = enabled,
                TextScale = LegacyUiMetrics.SmallButtonTextScale,
                TooltipLines = string.IsNullOrWhiteSpace(option.Tooltip) ? null : new[] { option.Tooltip }
            }.Draw(context);
            if (element != null)
            {
                element.Label = "同类替换:" + option.Label;
            }

            return element != null && context.IsElementHovered(element.Id, rect) ? element : null;
        }

        private static bool GetBlueprintReplacementCategoryEnabled(AppSettings settings, string category)
        {
            settings = settings ?? AppSettings.CreateDefault();
            if (string.Equals(category, BlueprintReplacementCategories.Torch, StringComparison.Ordinal)) return settings.BlueprintReplacementTorchesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Platform, StringComparison.Ordinal)) return settings.BlueprintReplacementPlatformsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.WorkBench, StringComparison.Ordinal)) return settings.BlueprintReplacementWorkBenchesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Chair, StringComparison.Ordinal)) return settings.BlueprintReplacementChairsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Door, StringComparison.Ordinal)) return settings.BlueprintReplacementDoorsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Table, StringComparison.Ordinal)) return settings.BlueprintReplacementTablesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Chest, StringComparison.Ordinal)) return settings.BlueprintReplacementChestsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Sign, StringComparison.Ordinal)) return settings.BlueprintReplacementSignsEnabled;
            return false;
        }

        private static LegacyUiRect CalculateBlueprintReplacementPopupRect(
            LegacyUiRect viewport,
            LegacyUiRect anchor,
            int optionCount,
            out int columns,
            out int optionWidth,
            out int columnGap,
            out int rowGap)
        {
            optionCount = Math.Max(1, optionCount);
            columnGap = BlueprintReplacementPopupColumnGap;
            rowGap = BlueprintReplacementPopupRowGap;
            columns = optionCount <= 8 ? 2 : 3;
            if (viewport.Width < 420)
            {
                columns = Math.Min(columns, 2);
            }

            columns = Math.Max(1, Math.Min(columns, optionCount));
            var desiredWidth = BlueprintReplacementPopupHorizontalPadding * 2 +
                               columns * BlueprintReplacementOptionMinWidth +
                               (columns - 1) * columnGap;
            var maxWidth = Math.Min(BlueprintReplacementPopupMaxWidth, Math.Max(BlueprintReplacementPopupMinWidth, viewport.Width - 12));
            var width = ClampInt(desiredWidth, BlueprintReplacementPopupMinWidth, maxWidth);
            optionWidth = Math.Max(
                BlueprintReplacementOptionMinWidth,
                (width - BlueprintReplacementPopupHorizontalPadding * 2 - (columns - 1) * columnGap) / columns);

            var rows = (optionCount + columns - 1) / columns;
            var desiredHeight = BlueprintReplacementPopupContentStartY +
                                rows * BlueprintReplacementOptionHeight +
                                Math.Max(0, rows - 1) * rowGap +
                                BlueprintReplacementPopupBottomPadding;
            var maxHeight = Math.Min(BlueprintReplacementPopupMaxHeight, Math.Max(BlueprintReplacementPopupMinHeight, viewport.Height - 12));
            var height = ClampInt(desiredHeight, BlueprintReplacementPopupMinHeight, maxHeight);
            var x = ClampInt(anchor.X - width + anchor.Width, viewport.X + 6, Math.Max(viewport.X + 6, viewport.Right - width - 6));
            var y = anchor.Bottom + 8;
            if (y + height > viewport.Bottom - 6)
            {
                y = anchor.Y - height - 8;
            }

            y = ClampInt(y, viewport.Y + 6, Math.Max(viewport.Y + 6, viewport.Bottom - height - 6));
            return new LegacyUiRect(x, y, width, height);
        }

        private static LegacyUiElement DrawBlueprintStatusBar(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintStatusBarHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            var clip = area.Viewport;
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, LegacyUiTheme.Radius, 34, 42, 62, 225);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, clip.X, clip.Y, clip.Width, clip.Height, 100, 116, 150, 160);
            AddUiBlocker(elements, BlueprintStatusBarElementId, "蓝图:底部状态栏", rect.Intersect(clip));

            var snapshot = BlueprintEntryState.GetSnapshot(settings);
            var creation = BlueprintCreationMaskState.GetSnapshot();
            var placement = BlueprintPlacementPreviewState.GetSnapshot();
            var erase = BlueprintEraseRegionState.GetSnapshot();
            var projection = BlueprintProjectionService.GetDiagnostics();
            var materials = BlueprintMaterialService.GetDiagnostics();
            var autoPlacement = BlueprintAutoPlacementService.GetDiagnostics();
            UiTextRenderer.DrawTextClipped(spriteBatch, "状态：" + snapshot.ModeDisplayName, rect.X + 12, rect.Y + 10, rect.Width - 24, 20, clip.X, clip.Y, clip.Width, clip.Height, 246, 242, 220, 255, 0.76f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BlueprintEntryState.BuildSettingsSummary(settings), rect.X + 12, rect.Y + 32, rect.Width - 24, 18, clip.X, clip.Y, clip.Width, clip.Height, 206, 218, 238, 230, 0.64f);
            UiTextRenderer.DrawTextClipped(spriteBatch, snapshot.LastNotice ?? string.Empty, rect.X + 12, rect.Y + 52, rect.Width - 24, 18, clip.X, clip.Y, clip.Width, clip.Height, 218, 198, 128, 235, 0.62f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintCreationSummary(creation), rect.X + 12, rect.Y + 73, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 166, 206, 255, 235, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintPlacementSummary(placement), rect.X + 12, rect.Y + 91, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 160, 224, 176, 235, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintProjectionSummary(projection), rect.X + 12, rect.Y + 109, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 238, 184, 132, 235, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintMaterialSummary(materials), rect.X + 12, rect.Y + 127, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 210, 226, 186, 235, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintEraseSummary(erase), rect.X + 12, rect.Y + 145, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 244, 160, 178, 235, 0.58f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintAutoPlacementSummary(autoPlacement), rect.X + 12, rect.Y + 163, rect.Width - 24, 16, clip.X, clip.Y, clip.Width, clip.Height, 176, 216, 246, 235, 0.58f);

            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, settings);
            var buttonY = rect.Bottom - 31;
            var hovered = (LegacyUiElement)null;
            if (string.Equals(snapshot.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                var buttonWidth = rect.Width < 430 ? 64 : 74;
                var gap = 6;
                var total = buttonWidth * 4 + gap * 3;
                var x = rect.Right - total - 12;
                hovered = DrawBlueprintStatusButton(context, new LegacyUiRect(x, buttonY, buttonWidth, 24), "blueprint-entry:finish-create-save", "完成保存", "采集选区并保存为模板。") ?? hovered;
                x += buttonWidth + gap;
                hovered = DrawBlueprintStatusButton(context, new LegacyUiRect(x, buttonY, buttonWidth, 24), "blueprint-entry:finish-create-use", "完成使用", "保存模板并进入摆放预览。") ?? hovered;
                x += buttonWidth + gap;
                hovered = DrawBlueprintStatusButton(context, new LegacyUiRect(x, buttonY, buttonWidth, 24), "blueprint-entry:clear-selection", "清空选区", "清空当前创建 mask。") ?? hovered;
                x += buttonWidth + gap;
                hovered = DrawBlueprintStatusButton(context, new LegacyUiRect(x, buttonY, buttonWidth, 24), "blueprint-entry:cancel", "取消", "取消创建并回到工具入口。") ?? hovered;
            }
            else if (string.Equals(snapshot.Mode, BlueprintEntryModes.PlacementPreview, StringComparison.Ordinal))
            {
                var buttonWidth = rect.Width < 430 ? 76 : 84;
                var gap = 6;
                var total = buttonWidth * 2 + gap;
                var x = rect.Right - total - 12;
                var mirrorRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                x += buttonWidth + gap;
                var cancelRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                hovered = DrawBlueprintStatusButton(context, mirrorRect, "blueprint-entry:mirror-preview-horizontal", "水平镜像", "仅镜像当前摆放预览；方向 frame 不明确时会拒绝。") ?? hovered;
                hovered = DrawBlueprintStatusButton(context, cancelRect, "blueprint-entry:cancel", "取消摆放", "取消当前蓝图摆放预览。") ?? hovered;
            }
            else if (string.Equals(snapshot.Mode, BlueprintEntryModes.EraseRegion, StringComparison.Ordinal))
            {
                var cancelRect = new LegacyUiRect(rect.Right - 96, buttonY, 84, 24);
                hovered = DrawBlueprintStatusButton(context, cancelRect, "blueprint-entry:cancel", "取消擦除", "取消当前实例擦除模式。") ?? hovered;
            }
            else
            {
                var buttonWidth = rect.Width < 430 ? 58 : 66;
                var gap = 6;
                var total = buttonWidth * 5 + gap * 4;
                var x = rect.Right - total - 12;
                var startRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                x += buttonWidth + gap;
                var libraryRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                x += buttonWidth + gap;
                var placedRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                x += buttonWidth + gap;
                var materialsRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                x += buttonWidth + gap;
                var eraseRect = new LegacyUiRect(x, buttonY, buttonWidth, 24);
                hovered = DrawBlueprintStatusButton(context, startRect, "blueprint-entry:start-create", "开始创建", "进入 05 mask 选择状态。") ?? hovered;
                hovered = DrawBlueprintStatusButton(context, libraryRect, "blueprint-entry:open-library", "蓝图库", "打开蓝图库模板管理。") ?? hovered;
                hovered = DrawBlueprintStatusButton(context, placedRect, "blueprint-entry:open-placed", "已放置", "打开当前世界实例列表。") ?? hovered;
                hovered = DrawBlueprintStatusButton(context, materialsRect, "blueprint-entry:open-materials", "材料", "打开材料统计浮窗。") ?? hovered;
                hovered = DrawBlueprintStatusButton(context, eraseRect, "blueprint-entry:start-erase", "擦除", "进入当前选中实例的区域擦除模式。") ?? hovered;
            }

            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiElement DrawBlueprintLibraryToolbar(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintLibraryUiSnapshot snapshot)
        {
            snapshot = snapshot ?? BlueprintLibraryUiState.GetSnapshot();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintLibraryToolbarHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, rect, area.Viewport);
            LegacyUiRect importRect;
            LegacyUiRect prevRect;
            LegacyUiRect nextRect;
            LegacyUiRect backRect;
            CalculateBlueprintLibraryHeaderButtonRects(rect, out importRect, out prevRect, out nextRect, out backRect);
            var hovered = (LegacyUiElement)null;
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                importRect,
                BlueprintLibraryUiState.BuildCommandId("import", string.Empty),
                "导入",
                null) ?? hovered;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, prevRect, "blueprint-library:page-prev", "上一页", null) ?? hovered;
            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, nextRect, "blueprint-library:page-next", "下一页", null) ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawBlueprintLibraryPreview(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintLibraryUiSnapshot snapshot)
        {
            snapshot = snapshot ?? BlueprintLibraryUiState.GetSnapshot();
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintLibraryPreviewHeight);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
            var template = BlueprintLibraryUiState.GetSelectedOrFirstTemplate(snapshot);
            if (template == null)
            {
                UiTextRenderer.DrawTextClipped(spriteBatch, snapshot.LoadSucceeded ? "蓝图库为空" : "蓝图库读取失败", rect.X + 12, rect.Y + 12, rect.Width - 24, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.74f);
                UiTextRenderer.DrawTextClipped(spriteBatch, snapshot.LoadSucceeded ? "创建阶段保存模板后会显示在这里。" : snapshot.LoadMessage, rect.X + 12, rect.Y + 40, rect.Width - 24, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 218, 238, 230, 0.62f);
                return null;
            }

            var selected = string.Equals(template.TemplateId, snapshot.SelectedTemplateId, StringComparison.OrdinalIgnoreCase);
            var title = (selected ? "预览：" : "预览候选：") + template.Name;
            UiTextRenderer.DrawTextClipped(spriteBatch, UiTextRenderer.Ellipsize(title, Math.Max(1, rect.Width - BlueprintLibraryPreviewGridSize - 44), 0.74f), rect.X + 12, rect.Y + 10, rect.Width - BlueprintLibraryPreviewGridSize - 44, 20, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 246, 242, 220, 255, 0.74f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BlueprintLibraryUiState.BuildTemplateSummary(template), rect.X + 12, rect.Y + 34, rect.Width - BlueprintLibraryPreviewGridSize - 44, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 218, 238, 230, 0.62f);
            UiTextRenderer.DrawTextClipped(spriteBatch, BuildBlueprintCapabilityText(template), rect.X + 12, rect.Y + 56, rect.Width - BlueprintLibraryPreviewGridSize - 44, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 218, 198, 128, 235, 0.58f);

            var grid = new LegacyUiRect(rect.Right - BlueprintLibraryPreviewGridSize - 12, rect.Y + 11, BlueprintLibraryPreviewGridSize, BlueprintLibraryPreviewGridSize);
            DrawBlueprintTemplatePreviewGrid(spriteBatch, grid, area.Viewport, template);
            return null;
        }

        private static LegacyUiElement DrawBlueprintLibraryList(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            int contentY,
            BlueprintLibraryUiSnapshot snapshot)
        {
            snapshot = snapshot ?? BlueprintLibraryUiState.GetSnapshot();
            if (!snapshot.LoadSucceeded || snapshot.Templates == null || snapshot.Templates.Count <= 0)
            {
                var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, BlueprintLibraryEmptyHeight);
                if (area.IsVisible(rect))
                {
                    LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, area.Viewport);
                    var text = snapshot.LoadSucceeded ? "暂无模板" : "模板库读取失败";
                    var detail = snapshot.LoadSucceeded ? "创建并保存后的模板会进入蓝图库。" : snapshot.LoadMessage;
                    UiTextRenderer.DrawTextClipped(spriteBatch, text, rect.X + 12, rect.Y + 10, rect.Width - 24, 18, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.72f);
                    UiTextRenderer.DrawTextClipped(spriteBatch, detail, rect.X + 12, rect.Y + 34, rect.Width - 24, 16, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 206, 218, 238, 230, 0.58f);
                }

                return null;
            }

            var hovered = (LegacyUiElement)null;
            var end = Math.Min(snapshot.Templates.Count, snapshot.VisibleStartIndex + snapshot.VisibleCount);
            var baseY = area.ToScreenY(contentY);
            for (var index = snapshot.VisibleStartIndex; index < end; index++)
            {
                var visibleIndex = index - snapshot.VisibleStartIndex;
                hovered = DrawBlueprintTemplateCard(
                    spriteBatch,
                    area,
                    mouse,
                    elements,
                    CalculateBlueprintLibraryCardRect(area.Viewport, baseY, visibleIndex),
                    snapshot.Templates[index],
                    snapshot,
                    index) ?? hovered;
            }

            return hovered;
        }

        private static LegacyUiElement DrawBlueprintTemplateCard(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect card,
            BlueprintTemplateRecord template,
            BlueprintLibraryUiSnapshot snapshot,
            int index)
        {
            if (template == null || !area.IsVisible(card))
            {
                return null;
            }

            var selected = string.Equals(template.TemplateId, snapshot.SelectedTemplateId, StringComparison.OrdinalIgnoreCase);
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, area.Viewport);
            if (selected)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, card.X + 1, card.Y + 1, card.Width - 2, card.Height - 2, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, LegacyUiTheme.RadiusSmall - 1, 86, 112, 72, 58);
            }

            AddUiBlocker(elements, "blueprint-library:card:" + template.TemplateId, "蓝图库:模板卡片" + index.ToString(CultureInfo.InvariantCulture), card.Intersect(area.Viewport));

            var innerX = card.X + BlueprintLibraryCardPadding;
            var innerWidth = Math.Max(1, card.Width - BlueprintLibraryCardPadding * 2);
            var buttonY = card.Y + BlueprintLibraryCardPadding;
            var actionWidth = Math.Min(BlueprintLibraryCardActionWidth, Math.Max(28, (innerWidth - 3 * BlueprintLibraryCardButtonGap) / 4));
            var deleteRect = new LegacyUiRect(card.Right - BlueprintLibraryCardPadding - actionWidth, buttonY, actionWidth, BlueprintLibraryCardButtonHeight);
            var exportRect = new LegacyUiRect(deleteRect.X - BlueprintLibraryCardButtonGap - actionWidth, buttonY, actionWidth, BlueprintLibraryCardButtonHeight);
            var useRect = new LegacyUiRect(exportRect.X - BlueprintLibraryCardButtonGap - actionWidth, buttonY, actionWidth, BlueprintLibraryCardButtonHeight);
            var nameWidth = Math.Max(44, useRect.X - BlueprintLibraryCardButtonGap - innerX);
            var nameRect = new LegacyUiRect(innerX, buttonY, nameWidth, BlueprintLibraryCardButtonHeight);

            var hovered = (LegacyUiElement)null;
            var nameInputId = BlueprintLibraryUiState.BuildNameInputId(template.TemplateId);
            var nameEditing = LegacyTextInput.IsFocused(nameInputId);
            if (nameEditing)
            {
                hovered = DrawBlueprintTemplateNameInput(spriteBatch, area, mouse, elements, nameRect, template, index) ?? hovered;
                hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, useRect, BlueprintLibraryUiState.BuildCommandId(BlueprintLibraryUiState.ConfirmNameAction, template.TemplateId), "保存", "保存模板名称。") ?? hovered;
            }
            else
            {
                hovered = DrawBlueprintTemplateNameShellButton(spriteBatch, mouse, elements, area.Viewport, nameRect, template, index, selected) ?? hovered;
                hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, useRect, BlueprintLibraryUiState.BuildCommandId("layout-use", template.TemplateId), "放置", null) ?? hovered;
            }

            hovered = DrawBlueprintSmallButton(spriteBatch, mouse, elements, area.Viewport, exportRect, BlueprintLibraryUiState.BuildCommandId("layout-export", template.TemplateId), "导出", null) ?? hovered;
            var deleteConfirming = string.Equals(snapshot.DeleteConfirmTemplateId, template.TemplateId, StringComparison.OrdinalIgnoreCase);
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                deleteRect,
                BlueprintLibraryUiState.BuildCommandId("delete", template.TemplateId),
                deleteConfirming ? "确认" : "删除",
                null,
                deleteConfirming) ?? hovered;

            var previewRect = new LegacyUiRect(innerX, buttonY + BlueprintLibraryCardButtonHeight + 6, innerWidth, 68);
            DrawBlueprintTemplatePreviewGrid(spriteBatch, previewRect, area.Viewport, template);
            var materialExpanded = string.Equals(snapshot.ExpandedMaterialTemplateId, template.TemplateId, StringComparison.OrdinalIgnoreCase);
            var materialButtonRect = new LegacyUiRect(
                innerX + Math.Max(0, innerWidth - BlueprintLibraryMaterialButtonWidth),
                previewRect.Bottom + 4,
                Math.Min(BlueprintLibraryMaterialButtonWidth, innerWidth),
                BlueprintLibraryCardButtonHeight);
            var textWidth = Math.Max(1, materialButtonRect.X - BlueprintLibraryCardButtonGap - innerX);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                BlueprintLibraryUiState.BuildTemplateSummary(template),
                innerX,
                previewRect.Bottom + 5,
                textWidth,
                13,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                198,
                210,
                228,
                226,
                BlueprintLibraryCardSummaryTextScale);
            hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                materialButtonRect,
                BlueprintLibraryUiState.BuildCommandId("layout-materials", template.TemplateId),
                "材料",
                null,
                materialExpanded) ?? hovered;
            var capability = BuildBlueprintCapabilityText(template);
            if (!string.IsNullOrWhiteSpace(capability))
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    capability,
                    innerX,
                    previewRect.Bottom + 25,
                    innerWidth,
                    13,
                    area.Viewport.X,
                    area.Viewport.Y,
                    area.Viewport.Width,
                    area.Viewport.Height,
                    206,
                    198,
                    128,
                    226,
                    0.50f);
            }
            return hovered;
        }

        private static bool RegisterBlueprintLibraryMaterialModalOverlay(LegacyScrollArea area, BlueprintLibraryUiSnapshot snapshot)
        {
            var template = BlueprintLibraryUiState.GetExpandedMaterialTemplate(snapshot);
            if (area == null || template == null)
            {
                return false;
            }

            var bounds = CalculateBlueprintLibraryMaterialModalRect(area.Viewport, template);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = BlueprintLibraryMaterialModalElementId,
                OwnerPageId = "blueprint",
                Bounds = bounds,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 24,
                CacheSignature = BuildBlueprintLibraryMaterialModalCacheSignature(template),
                State = new BlueprintLibraryMaterialModalState(area, template),
                Draw = DrawBlueprintLibraryMaterialModalOverlay,
                TryConsumeScroll = TryConsumeBlueprintLibraryMaterialModalScroll
            });
        }

        private static void DrawBlueprintLibraryMaterialModalOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var state = request == null ? null : request.State as BlueprintLibraryMaterialModalState;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || state == null || state.Area == null || state.Template == null || elements == null)
            {
                return;
            }

            DrawBlueprintLibraryMaterialModal(context.SpriteBatch, state.Area, context.Mouse, elements, state.Template);
        }

        private static LegacyUiElement DrawBlueprintLibraryMaterialModal(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            BlueprintTemplateRecord template)
        {
            var bounds = CalculateBlueprintLibraryMaterialModalRect(area.Viewport, template);
            var context = LegacyUiContext.ForScrollArea(spriteBatch, mouse, area, elements, ConfigService.AppSettings ?? AppSettings.CreateDefault());
            new LegacyPopupPanelControl
            {
                Id = BlueprintLibraryMaterialModalElementId,
                Label = "蓝图库材料清单",
                Kind = "blocker",
                Bounds = bounds
            }.Draw(context);

            var title = string.IsNullOrWhiteSpace(template.Name) ? "材料清单" : "材料清单：" + template.Name.Trim();
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(title, Math.Max(1, bounds.Width - 84), 0.82f),
                bounds.X + 16,
                bounds.Y + 11,
                Math.Max(1, bounds.Width - 84),
                20,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.82f);

            var closeRect = new LegacyUiRect(bounds.Right - 58, bounds.Y + 8, 44, 22);
            var hovered = DrawBlueprintSmallButton(
                spriteBatch,
                mouse,
                elements,
                area.Viewport,
                closeRect,
                BlueprintLibraryUiState.BuildCommandId("materials-close", template.TemplateId),
                "关闭",
                "关闭材料清单") ?? null;

            var lines = BlueprintLibraryUiState.BuildTemplateMaterialLines(template);
            var columns = ResolveBlueprintLibraryMaterialModalColumns(bounds.Width, lines.Count);
            var body = CalculateBlueprintMaterialModalBodyRect(bounds);
            var contentHeight = CalculateBlueprintMaterialModalContentHeight(lines.Count, columns);
            SetBlueprintLibraryMaterialModalViewport(template, body.Intersect(area.Viewport), contentHeight);
            var scrollOffset = GetBlueprintLibraryMaterialModalScrollOffset(template);
            var clip = body.Intersect(area.Viewport);
            var contentWidth = Math.Max(1, body.Width);
            var columnWidth = Math.Max(1, (contentWidth - Math.Max(0, columns - 1) * BlueprintLibraryMaterialModalColumnGap) / columns);
            for (var index = 0; index < lines.Count; index++)
            {
                var column = index % columns;
                var row = index / columns;
                var rect = new LegacyUiRect(
                    body.X + column * (columnWidth + BlueprintLibraryMaterialModalColumnGap),
                    body.Y + row * BlueprintLibraryMaterialModalRowHeight - scrollOffset,
                    columnWidth,
                    BlueprintLibraryMaterialModalRowHeight);
                if (!rect.Intersects(clip))
                {
                    continue;
                }

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    UiTextRenderer.Ellipsize(lines[index], Math.Max(1, rect.Width - 4), 0.66f),
                    rect.X + 2,
                    rect.Y + 3,
                    rect.Width - 4,
                    rect.Height - 4,
                    clip.X,
                    clip.Y,
                    clip.Width,
                    clip.Height,
                    218,
                    226,
                    238,
                    245,
                    0.66f);
            }

            return context.HoveredElement ?? hovered;
        }

        private static LegacyUiElement DrawBlueprintTemplateNameShellButton(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            LegacyUiRect rect,
            BlueprintTemplateRecord template,
            int index,
            bool selected)
        {
            var templateId = template == null ? string.Empty : template.TemplateId;
            var elementId = BlueprintLibraryUiState.BuildCommandId("name", templateId);
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse != null && mouse.LeftDown, selected, true, clip);
            var name = template == null ? string.Empty : template.Name ?? string.Empty;
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(name, Math.Max(1, rect.Width - 14), 0.62f),
                rect.X + 7,
                rect.Y + 4,
                rect.Width - 14,
                Math.Max(1, rect.Height - 8),
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                selected ? 246 : 230,
                selected ? 242 : 232,
                selected ? 220 : 224,
                255,
                0.62f);
            var element = AddFrameElement(
                elements,
                elementId,
                "蓝图库:模板名称" + index.ToString(CultureInfo.InvariantCulture),
                "button",
                elementRect,
                selected: selected,
                tooltipLines: new[] { "双击重命名模板。" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawBlueprintTemplateNameInput(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            BlueprintTemplateRecord template,
            int index)
        {
            var templateId = template == null ? string.Empty : template.TemplateId;
            var inputId = BlueprintLibraryUiState.BuildNameInputId(templateId);
            var focused = LegacyTextInput.IsFocused(inputId);
            if (focused)
            {
                LegacyTextInput.Update(inputId);
            }

            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var elementId = BlueprintLibraryUiState.NameElementPrefix + templateId;
            var hovered = IsFrameElementHovered(elementId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse != null && mouse.LeftDown, focused, true, area.Viewport);
            var content = LegacyUiTheme.GetSelectedButtonContentRect(rect, focused, true);
            var text = focused
                ? LegacyTextInput.GetDisplayText(inputId, template == null ? string.Empty : template.Name)
                : template == null ? string.Empty : template.Name ?? string.Empty;
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                UiTextRenderer.Ellipsize(text, Math.Max(1, rect.Width - 16), 0.66f),
                rect.X + 8,
                content.Y + 4,
                rect.Width - 16,
                Math.Max(1, content.Height - 8),
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                focused ? 255 : 230,
                focused ? 245 : 232,
                focused ? 205 : 224,
                255,
                0.66f);
            TryAttachLegacyTextInputImePanel(inputId, rect, area.Viewport);

            var element = AddFrameElement(
                elements,
                elementId,
                "蓝图库:模板名称" + index.ToString(CultureInfo.InvariantCulture),
                "button",
                elementRect,
                selected: focused,
                tooltipLines: new[] { "双击重命名模板。" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static void DrawBlueprintTemplatePreviewGrid(
            object spriteBatch,
            LegacyUiRect rect,
            LegacyUiRect clip,
            BlueprintTemplateRecord template)
        {
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, rect, clip);
            int originX;
            int originY;
            int drawWidth;
            int drawHeight;
            double scale;
            if (!TryResolveBlueprintTemplatePreviewLayout(template, rect, out originX, out originY, out drawWidth, out drawHeight, out scale))
            {
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "空", rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, 198, 210, 228, 226, 0.66f);
                return;
            }

            var columns = Math.Max(1, template.Width);
            var rows = Math.Max(1, template.Height);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, originX, originY, drawWidth, drawHeight, 1, clip.X, clip.Y, clip.Width, clip.Height, 86, 112, 150, 92);
            var maxCells = Math.Min(template.Cells.Count, BlueprintLibraryPreviewMaxDrawCells);
            for (var sample = 0; sample < maxCells; sample++)
            {
                var index = maxCells >= template.Cells.Count
                    ? sample
                    : (int)Math.Floor(sample * (template.Cells.Count - 1) / (double)Math.Max(1, maxCells - 1));
                var cell = template.Cells[index];
                if (cell == null || cell.X < 0 || cell.Y < 0 || cell.X >= columns || cell.Y >= rows)
                {
                    continue;
                }

                var color = ResolveBlueprintPreviewColor(cell);
                var x0 = originX + (int)Math.Floor(cell.X * scale);
                var y0 = originY + (int)Math.Floor(cell.Y * scale);
                var x1 = originX + (int)Math.Ceiling((cell.X + 1) * scale);
                var y1 = originY + (int)Math.Ceiling((cell.Y + 1) * scale);
                UiPrimitiveRenderer.DrawFilledRectClipped(
                    spriteBatch,
                    x0,
                    y0,
                    Math.Max(1, x1 - x0),
                    Math.Max(1, y1 - y0),
                    clip.X,
                    clip.Y,
                    clip.Width,
                    clip.Height,
                    color[0],
                    color[1],
                    color[2],
                    220);
            }
        }

        private static bool TryResolveBlueprintTemplatePreviewLayout(
            BlueprintTemplateRecord template,
            LegacyUiRect rect,
            out int originX,
            out int originY,
            out int drawWidth,
            out int drawHeight,
            out double scale)
        {
            originX = rect.X;
            originY = rect.Y;
            drawWidth = 0;
            drawHeight = 0;
            scale = 0d;
            if (template == null || template.Cells == null || template.Cells.Count <= 0 || template.Width <= 0 || template.Height <= 0)
            {
                return false;
            }

            // Preview uses only serialized template cells. Empty or invalid templates
            // fail closed, and every cell is mapped through the same scale so wide and
            // tall blueprints fit the card without resizing the layout.
            var columns = Math.Max(1, template.Width);
            var rows = Math.Max(1, template.Height);
            var availableWidth = Math.Max(1, rect.Width - 10);
            var availableHeight = Math.Max(1, rect.Height - 10);
            scale = Math.Min(availableWidth / (double)columns, availableHeight / (double)rows);
            if (scale <= 0d)
            {
                return false;
            }

            drawWidth = Math.Max(1, Math.Min(availableWidth, (int)Math.Ceiling(columns * scale)));
            drawHeight = Math.Max(1, Math.Min(availableHeight, (int)Math.Ceiling(rows * scale)));
            originX = rect.X + (rect.Width - drawWidth) / 2;
            originY = rect.Y + (rect.Height - drawHeight) / 2;
            return true;
        }

        private static LegacyUiElement DrawBlueprintSmallButton(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string id,
            string text,
            string tooltip)
        {
            return DrawBlueprintSmallButton(spriteBatch, mouse, elements, clip, rect, id, text, tooltip, false);
        }

        private static LegacyUiElement DrawBlueprintSmallButton(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect clip,
            LegacyUiRect rect,
            string id,
            string text,
            string tooltip,
            bool selected)
        {
            var hit = rect.Intersect(clip);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse != null && mouse.LeftDown, selected, true, clip);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                text,
                rect.X + 3,
                rect.Y,
                rect.Width - 6,
                rect.Height,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                selected ? LegacyUiTheme.SelectedTextR : 230,
                selected ? LegacyUiTheme.SelectedTextG : 232,
                selected ? LegacyUiTheme.SelectedTextB : 224,
                255,
                ResolveBlueprintButtonScale(text, rect.Width - 6));
            var element = AddFrameElement(elements, id, text, "button", elementRect, selected: selected, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawBlueprintStatusButton(
            LegacyUiContext context,
            LegacyUiRect rect,
            string id,
            string text,
            string tooltip)
        {
            var element = new LegacyButtonControl
            {
                Id = id,
                Label = text,
                Text = text,
                ElementLabel = "蓝图:" + text,
                Kind = "button",
                Bounds = rect,
                TextScale = ResolveBlueprintButtonScale(text, rect.Width - 8),
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(id, rect) ? element : null;
        }

        private static int CalculateBlueprintContentHeight()
        {
            if (BlueprintLibraryUiState.IsOpen)
            {
                var library = BlueprintLibraryUiState.GetSnapshot();
                return BlueprintLibrarySubmenuHeaderHeight +
                       LegacyUiMetrics.SettingRowGap +
                       CalculateBlueprintLibraryListHeight(library) +
                       PageContentBottomPadding;
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var entry = BlueprintEntryState.GetSnapshot(settings);
            if (entry != null && string.Equals(entry.Mode, BlueprintEntryModes.PlacedManagement, StringComparison.Ordinal))
            {
                var placed = BlueprintPlacedInstanceUiState.GetSnapshot();
                return BlueprintLibrarySubmenuHeaderHeight +
                       LegacyUiMetrics.SettingRowGap +
                       CalculateBlueprintPlacedMaterialPanelHeight(BlueprintMaterialService.GetCachedSnapshotForDraw()) +
                       LegacyUiMetrics.SettingRowGap +
                       CalculateBlueprintPlacedListHeight(placed) +
                       PageContentBottomPadding;
            }

            return CalculateBlueprintStackedRowsHeight(BlueprintTopSettingRowCount + BlueprintActionShortcutRowCount) +
                   LegacyUiMetrics.SectionGap +
                   CalculateBlueprintStackedRowsHeight(BlueprintMenuOpenRowCount) +
                   PageContentBottomPadding;
        }

        private static int CalculateBlueprintStackedRowsHeight(int rowCount)
        {
            return rowCount <= 0
                ? 0
                : rowCount * LegacyUiMetrics.RowHeight + (rowCount - 1) * LegacyUiMetrics.SettingRowGap;
        }

        private static int CalculateBlueprintLibraryListHeight(BlueprintLibraryUiSnapshot library)
        {
            library = library ?? BlueprintLibraryUiState.GetSnapshot();
            return CalculateBlueprintLibraryListHeight(library.LoadSucceeded, library.Templates == null ? 0 : library.Templates.Count, library.VisibleCount);
        }

        private static int CalculateBlueprintLibraryListHeight(bool loadSucceeded, int templateCount, int visibleCount)
        {
            if (!loadSucceeded || templateCount <= 0 || visibleCount <= 0)
            {
                return BlueprintLibraryEmptyHeight;
            }

            var rows = (visibleCount + BlueprintLibraryCardColumns - 1) / BlueprintLibraryCardColumns;
            return rows * BlueprintLibraryCardHeight + Math.Max(0, rows - 1) * BlueprintLibraryCardGap;
        }

        private static string BuildBlueprintLibraryHeaderSummary(int templateCount, int placedCount)
        {
            return "共保存" + Math.Max(0, templateCount).ToString(CultureInfo.InvariantCulture) +
                   "个蓝图，已放置" + Math.Max(0, placedCount).ToString(CultureInfo.InvariantCulture) + "个蓝图";
        }

        private static void CalculateBlueprintLibraryHeaderButtonRects(
            LegacyUiRect headerRect,
            out LegacyUiRect importRect,
            out LegacyUiRect prevRect,
            out LegacyUiRect nextRect,
            out LegacyUiRect backRect)
        {
            var buttonY = headerRect.Y + 8;
            const int gap = 6;
            var backWidth = 54;
            var pageWidth = 52;
            var importWidth = Math.Max(42, ModeButtonWidth("导入"));
            backRect = new LegacyUiRect(headerRect.Right - 10 - backWidth, buttonY, backWidth, RowModeButtonHeight);
            nextRect = new LegacyUiRect(backRect.X - gap - pageWidth, buttonY, pageWidth, RowModeButtonHeight);
            prevRect = new LegacyUiRect(nextRect.X - gap - pageWidth, buttonY, pageWidth, RowModeButtonHeight);
            importRect = new LegacyUiRect(prevRect.X - gap - importWidth, buttonY, importWidth, RowModeButtonHeight);
        }

        private static LegacyUiRect CalculateBlueprintLibraryImportButtonRect(LegacyUiRect headerRect)
        {
            LegacyUiRect importRect;
            LegacyUiRect prevRect;
            LegacyUiRect nextRect;
            LegacyUiRect backRect;
            CalculateBlueprintLibraryHeaderButtonRects(headerRect, out importRect, out prevRect, out nextRect, out backRect);
            return importRect;
        }

        private static LegacyUiRect CalculateBlueprintLibraryCardRect(LegacyUiRect viewport, int screenY, int visibleIndex)
        {
            visibleIndex = Math.Max(0, visibleIndex);
            var column = visibleIndex % BlueprintLibraryCardColumns;
            var row = visibleIndex / BlueprintLibraryCardColumns;
            var totalGap = BlueprintLibraryCardGap * (BlueprintLibraryCardColumns - 1);
            var width = Math.Max(1, (viewport.Width - totalGap) / BlueprintLibraryCardColumns);
            return new LegacyUiRect(
                viewport.X + column * (width + BlueprintLibraryCardGap),
                screenY + row * (BlueprintLibraryCardHeight + BlueprintLibraryCardGap),
                width,
                BlueprintLibraryCardHeight);
        }

        private static LegacyUiRect CalculateBlueprintLibraryMaterialModalRect(LegacyUiRect viewport, BlueprintTemplateRecord template)
        {
            var lines = BlueprintLibraryUiState.BuildTemplateMaterialLines(template);
            var width = ClampInt(
                Math.Min(BlueprintLibraryMaterialModalMaxWidth, Math.Max(BlueprintLibraryMaterialModalMinWidth, viewport.Width - 24)),
                Math.Min(BlueprintLibraryMaterialModalMinWidth, Math.Max(1, viewport.Width - 12)),
                Math.Max(1, viewport.Width - 12));
            var columns = ResolveBlueprintLibraryMaterialModalColumns(width, lines.Count);
            var rows = (lines.Count + columns - 1) / columns;
            var desiredHeight = BlueprintLibraryMaterialModalHeaderHeight +
                                rows * BlueprintLibraryMaterialModalRowHeight +
                                BlueprintLibraryMaterialModalPadding;
            var height = ClampInt(desiredHeight, 96, Math.Max(96, viewport.Height - 12));
            var x = viewport.X + Math.Max(0, (viewport.Width - width) / 2);
            var y = viewport.Y + Math.Max(0, (viewport.Height - height) / 2);
            return new LegacyUiRect(x, y, width, height);
        }

        private static int ResolveBlueprintLibraryMaterialModalColumns(int width, int lineCount)
        {
            return width >= 360 && lineCount > 8 ? 2 : 1;
        }

        private static LegacyUiRect CalculateBlueprintMaterialModalBodyRect(LegacyUiRect bounds)
        {
            return new LegacyUiRect(
                bounds.X + BlueprintLibraryMaterialModalPadding,
                bounds.Y + BlueprintLibraryMaterialModalHeaderHeight,
                Math.Max(0, bounds.Width - BlueprintLibraryMaterialModalPadding * 2),
                Math.Max(0, bounds.Height - BlueprintLibraryMaterialModalHeaderHeight - BlueprintLibraryMaterialModalPadding));
        }

        private static int CalculateBlueprintMaterialModalContentHeight(int lineCount, int columns)
        {
            columns = Math.Max(1, columns);
            var rows = Math.Max(1, (Math.Max(0, lineCount) + columns - 1) / columns);
            return rows * BlueprintLibraryMaterialModalRowHeight;
        }

        private static void SetBlueprintLibraryMaterialModalViewport(BlueprintTemplateRecord template, LegacyUiRect viewport, int contentHeight)
        {
            var targetId = BlueprintTemplateLibraryStore.NormalizeId(template == null ? string.Empty : template.TemplateId);
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                if (!string.Equals(_blueprintLibraryMaterialModalScrollTemplateId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    _blueprintLibraryMaterialModalScrollTemplateId = targetId;
                    _blueprintLibraryMaterialModalScrollOffset = 0;
                }

                _blueprintLibraryMaterialModalScrollViewport = viewport;
                _blueprintLibraryMaterialModalMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                _blueprintLibraryMaterialModalScrollOffset = ClampInt(_blueprintLibraryMaterialModalScrollOffset, 0, _blueprintLibraryMaterialModalMaxScroll);
            }
        }

        private static void SetBlueprintPlacedMaterialModalViewport(BlueprintWorldInstanceRecord instance, LegacyUiRect viewport, int contentHeight)
        {
            var targetId = BlueprintTemplateLibraryStore.NormalizeId(instance == null ? string.Empty : instance.InstanceId);
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                if (!string.Equals(_blueprintPlacedMaterialModalScrollInstanceId, targetId, StringComparison.OrdinalIgnoreCase))
                {
                    _blueprintPlacedMaterialModalScrollInstanceId = targetId;
                    _blueprintPlacedMaterialModalScrollOffset = 0;
                }

                _blueprintPlacedMaterialModalScrollViewport = viewport;
                _blueprintPlacedMaterialModalMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                _blueprintPlacedMaterialModalScrollOffset = ClampInt(_blueprintPlacedMaterialModalScrollOffset, 0, _blueprintPlacedMaterialModalMaxScroll);
            }
        }

        private static int GetBlueprintLibraryMaterialModalScrollOffset(BlueprintTemplateRecord template)
        {
            var targetId = BlueprintTemplateLibraryStore.NormalizeId(template == null ? string.Empty : template.TemplateId);
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return string.Equals(_blueprintLibraryMaterialModalScrollTemplateId, targetId, StringComparison.OrdinalIgnoreCase)
                    ? _blueprintLibraryMaterialModalScrollOffset
                    : 0;
            }
        }

        private static int GetBlueprintPlacedMaterialModalScrollOffset(BlueprintWorldInstanceRecord instance)
        {
            var targetId = BlueprintTemplateLibraryStore.NormalizeId(instance == null ? string.Empty : instance.InstanceId);
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return string.Equals(_blueprintPlacedMaterialModalScrollInstanceId, targetId, StringComparison.OrdinalIgnoreCase)
                    ? _blueprintPlacedMaterialModalScrollOffset
                    : 0;
            }
        }

        private static bool TryConsumeBlueprintLibraryMaterialModalScroll(LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return TryConsumeBlueprintMaterialModalScrollLocked(
                    _blueprintLibraryMaterialModalScrollViewport,
                    _blueprintLibraryMaterialModalMaxScroll,
                    ref _blueprintLibraryMaterialModalScrollOffset,
                    mouse,
                    rawScrollDelta);
            }
        }

        private static bool TryConsumeBlueprintPlacedMaterialModalScroll(LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return TryConsumeBlueprintMaterialModalScrollLocked(
                    _blueprintPlacedMaterialModalScrollViewport,
                    _blueprintPlacedMaterialModalMaxScroll,
                    ref _blueprintPlacedMaterialModalScrollOffset,
                    mouse,
                    rawScrollDelta);
            }
        }

        private static bool TryConsumeBlueprintMaterialModalScrollLocked(
            LegacyUiRect viewport,
            int maxScroll,
            ref int scrollOffset,
            LegacyMouseSnapshot mouse,
            int rawScrollDelta)
        {
            if (viewport.Width <= 0 ||
                viewport.Height <= 0 ||
                mouse == null ||
                rawScrollDelta == 0 ||
                !viewport.Contains(mouse.X, mouse.Y))
            {
                return false;
            }

            var next = ClampInt(scrollOffset + ConvertBlueprintMaterialModalWheelDelta(rawScrollDelta), 0, Math.Max(0, maxScroll));
            if (next == scrollOffset)
            {
                return false;
            }

            scrollOffset = next;
            return true;
        }

        private static int ConvertBlueprintMaterialModalWheelDelta(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }

        private static int BuildBlueprintLibraryMaterialModalCacheSignature(BlueprintTemplateRecord template)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(template == null ? string.Empty : template.TemplateId ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(template == null ? string.Empty : template.Name ?? string.Empty);
                var lines = BlueprintLibraryUiState.BuildTemplateMaterialLines(template);
                for (var index = 0; index < lines.Count; index++)
                {
                    hash = hash * 31 + StringComparer.Ordinal.GetHashCode(lines[index] ?? string.Empty);
                }

                hash = hash * 31 + GetBlueprintLibraryMaterialModalScrollOffset(template);
                return hash;
            }
        }

        private static int CalculateBlueprintPlacedListHeight(BlueprintPlacedInstanceUiSnapshot placed)
        {
            placed = placed ?? BlueprintPlacedInstanceUiState.GetSnapshot();
            return CalculateBlueprintPlacedListHeight(
                placed.LoadSucceeded,
                placed.Instances == null ? 0 : placed.Instances.Count,
                placed.VisibleCount);
        }

        private static int CalculateBlueprintPlacedListHeight(bool loadSucceeded, int instanceCount, int visibleCount)
        {
            if (!loadSucceeded || instanceCount <= 0 || visibleCount <= 0)
            {
                return BlueprintPlacedEmptyHeight;
            }

            var rows = (visibleCount + BlueprintPlacedCardColumns - 1) / BlueprintPlacedCardColumns;
            return rows * BlueprintPlacedRowHeight + Math.Max(0, rows - 1) * BlueprintPlacedRowGap;
        }

        private static int CalculateBlueprintPlacedMaterialPanelHeight(BlueprintMaterialSnapshot snapshot)
        {
            var lineCount = BuildBlueprintPlacedMaterialLines(snapshot, int.MaxValue).Count;
            var contentLines = Math.Max(1, lineCount);
            var desired = BlueprintPlacedMaterialPanelHeaderHeight +
                          contentLines * BlueprintPlacedMaterialPanelRowHeight +
                          BlueprintPlacedMaterialPanelBottomPadding;
            return Math.Max(BlueprintPlacedMaterialPanelMinHeight, desired);
        }

        private static void CalculateBlueprintPlacedHeaderButtonRects(
            LegacyUiRect headerRect,
            out LegacyUiRect prevRect,
            out LegacyUiRect nextRect,
            out LegacyUiRect backRect)
        {
            var buttonY = headerRect.Y + 8;
            const int gap = 6;
            var backWidth = 54;
            var pageWidth = 52;
            backRect = new LegacyUiRect(headerRect.Right - 10 - backWidth, buttonY, backWidth, RowModeButtonHeight);
            nextRect = new LegacyUiRect(backRect.X - gap - pageWidth, buttonY, pageWidth, RowModeButtonHeight);
            prevRect = new LegacyUiRect(nextRect.X - gap - pageWidth, buttonY, pageWidth, RowModeButtonHeight);
        }

        private static LegacyUiRect CalculateBlueprintPlacedCardRect(LegacyUiRect viewport, int screenY, int visibleIndex)
        {
            visibleIndex = Math.Max(0, visibleIndex);
            var column = visibleIndex % BlueprintPlacedCardColumns;
            var row = visibleIndex / BlueprintPlacedCardColumns;
            var totalGap = BlueprintPlacedRowGap * (BlueprintPlacedCardColumns - 1);
            var width = Math.Max(1, (viewport.Width - totalGap) / BlueprintPlacedCardColumns);
            return new LegacyUiRect(
                viewport.X + column * (width + BlueprintPlacedRowGap),
                screenY + row * (BlueprintPlacedRowHeight + BlueprintPlacedRowGap),
                width,
                BlueprintPlacedRowHeight);
        }

        private static string BuildBlueprintPlacedHeaderSummary(int placedCount)
        {
            return "当前世界已放置" + Math.Max(0, placedCount).ToString(CultureInfo.InvariantCulture) + "个蓝图";
        }

        private static string BuildBlueprintCapabilityText(BlueprintTemplateRecord template)
        {
            _ = template;
            return string.Empty;
        }

        private static string BuildBlueprintCreationSummary(BlueprintCreationMaskSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "创建 mask：未开始";
            }

            var prefix = snapshot.Active ? "创建 mask：选择中" : snapshot.CompletedPendingCapture ? "创建 mask：待采集保存" : "创建 mask：未开始";
            var count = snapshot.SelectedCount.ToString(CultureInfo.InvariantCulture) + " 格";
            var bounds = snapshot.HasBounds
                ? " / 范围 " + snapshot.MinX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.MinY.ToString(CultureInfo.InvariantCulture) +
                  " - " + snapshot.MaxX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.MaxY.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            var dragging = snapshot.Dragging
                ? " / 拖选 " + snapshot.DragStartX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.DragStartY.ToString(CultureInfo.InvariantCulture) +
                  " -> " + snapshot.DragCurrentX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.DragCurrentY.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            return prefix + " / 已选 " + count + bounds + dragging;
        }

        private static string BuildBlueprintPlacementSummary(BlueprintPlacementPreviewSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "摆放预览：未开始";
            }

            if (snapshot.Active)
            {
                var hover = snapshot.HoverTileHit
                    ? " / 鼠标 " + snapshot.HoverTileX.ToString(CultureInfo.InvariantCulture) +
                      "," + snapshot.HoverTileY.ToString(CultureInfo.InvariantCulture) +
                      " / 原点 " + snapshot.OriginTileX.ToString(CultureInfo.InvariantCulture) +
                      "," + snapshot.OriginTileY.ToString(CultureInfo.InvariantCulture)
                    : " / 等待鼠标命中世界格";
                return "摆放预览：" + snapshot.TemplateName +
                       " / " + snapshot.Width.ToString(CultureInfo.InvariantCulture) +
                       "x" + snapshot.Height.ToString(CultureInfo.InvariantCulture) +
                       " / 锚点 " + snapshot.AnchorX.ToString(CultureInfo.InvariantCulture) +
                       "," + snapshot.AnchorY.ToString(CultureInfo.InvariantCulture) +
                       hover +
                       (string.Equals(snapshot.MirrorLastStatus, "mirrorBlocked", StringComparison.Ordinal)
                           ? " / 镜像阻止 " + snapshot.MirrorBlockedReason
                           : string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(snapshot.LastPlacedInstanceId))
            {
                return "摆放预览：已创建实例 " + snapshot.LastPlacedInstanceName;
            }

            return "摆放预览：未开始";
        }

        private static string BuildBlueprintProjectionSummary(BlueprintProjectionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "投影：未就绪";
            }

            if (!snapshot.LoadSucceeded)
            {
                return "投影：不可用 / " + snapshot.ResultCode;
            }

            if (snapshot.InstanceCount <= 0)
            {
                return "投影：当前世界无已放置实例";
            }

            if (snapshot.VisibleInstanceCount <= 0)
            {
                return "投影：实例均隐藏 / 跳过 " + snapshot.HiddenInstanceCount.ToString(CultureInfo.InvariantCulture);
            }

            return "投影：有效 " + snapshot.EffectiveLayerCount.ToString(CultureInfo.InvariantCulture) +
                   " 层 / 完成 " + snapshot.FulfilledLayerCount.ToString(CultureInfo.InvariantCulture) +
                   " / 缺失 " + snapshot.MissingLayerCount.ToString(CultureInfo.InvariantCulture) +
                   " / 冲突 " + snapshot.ConflictLayerCount.ToString(CultureInfo.InvariantCulture) +
                   " / 覆盖 " + snapshot.CoveredLayerCount.ToString(CultureInfo.InvariantCulture) +
                   " / 隐藏 " + snapshot.HiddenInstanceCount.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildBlueprintMaterialSummary(BlueprintMaterialSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "材料：未就绪";
            }

            if (!snapshot.LoadSucceeded && string.Equals(snapshot.ResultCode, "projectionUnavailable", StringComparison.Ordinal))
            {
                return "材料：投影不可用 / " + snapshot.ProjectionResultCode;
            }

            if (snapshot.RequiredItemCount <= 0)
            {
                return "材料：当前无缺失材料 / 跳过已完成 " + snapshot.SkippedFulfilledLayerCount.ToString(CultureInfo.InvariantCulture) +
                       " 层";
            }

            return "材料：需求 " + snapshot.RequiredItemCount.ToString(CultureInfo.InvariantCulture) +
                   " 项 / 总量 " + snapshot.RequiredStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 已有 " + snapshot.AvailableStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 仍缺 " + snapshot.MissingStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 主包 " + snapshot.InventoryMainStackTotal.ToString(CultureInfo.InvariantCulture) +
                   " / 虚空袋 " + snapshot.InventoryVoidBagStackTotal.ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildBlueprintEraseSummary(BlueprintEraseRegionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "擦除：未开始";
            }

            if (!snapshot.Active)
            {
                return snapshot.TotalEraseCellCount > 0
                    ? "擦除：最近目标 " + snapshot.TargetInstanceName + " / 已擦除 " + snapshot.TotalEraseCellCount.ToString(CultureInfo.InvariantCulture) + " 格"
                    : "擦除：未开始";
            }

            var target = string.IsNullOrWhiteSpace(snapshot.TargetInstanceName)
                ? "随鼠标选择目标"
                : snapshot.TargetInstanceName;
            var dragging = snapshot.Dragging
                ? " / 拖选 " + snapshot.DragStartX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.DragStartY.ToString(CultureInfo.InvariantCulture) +
                  " -> " + snapshot.DragCurrentX.ToString(CultureInfo.InvariantCulture) + "," + snapshot.DragCurrentY.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            return "擦除：目标 " + target +
                   " / 已擦除 " + snapshot.TotalEraseCellCount.ToString(CultureInfo.InvariantCulture) +
                   " 格" + dragging;
        }

        private static string BuildBlueprintAutoPlacementSummary(BlueprintAutoPlacementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "自动摆放：未运行";
            }

            if (!snapshot.Enabled)
            {
                return "自动摆放：关闭 / " + snapshot.ResultCode;
            }

            if (snapshot.CandidateCount <= 0)
            {
                return "自动摆放：无候选 / 跳过完成 " + snapshot.SkippedFulfilledLayerCount.ToString(CultureInfo.InvariantCulture) +
                       " / 冲突 " + snapshot.SkippedConflictLayerCount.ToString(CultureInfo.InvariantCulture) +
                       " / 暂不支持 " + snapshot.SkippedUnsupportedLayerCount.ToString(CultureInfo.InvariantCulture) +
                       " / 缺材料 " + snapshot.SkippedInsufficientMaterialLayerCount.ToString(CultureInfo.InvariantCulture) +
                       " / 虚空袋需移入主包 " + snapshot.SkippedVoidBagOnlyLayerCount.ToString(CultureInfo.InvariantCulture);
            }

            return "自动摆放：候选 " + snapshot.CandidateCount.ToString(CultureInfo.InvariantCulture) +
                   " / 下一层 " + snapshot.SelectedLayerKind +
                   " @" + snapshot.SelectedWorldTileX.ToString(CultureInfo.InvariantCulture) +
                   "," + snapshot.SelectedWorldTileY.ToString(CultureInfo.InvariantCulture) +
                   (snapshot.SelectedReplacementApplied ? " / 替换 " + snapshot.SelectedReplacementCategory : string.Empty) +
                   " / admission " + snapshot.LastAdmissionStatus +
                   " / result " + snapshot.LastResultCode +
                   " / ok " + snapshot.SucceededCount.ToString(CultureInfo.InvariantCulture) +
                   " / unverified " + snapshot.AttemptedButUnverifiedCount.ToString(CultureInfo.InvariantCulture);
        }

        private static int[] ResolveBlueprintPreviewColor(BlueprintCellRecord cell)
        {
            var layer = cell == null || cell.Layers == null || cell.Layers.Count <= 0 ? null : cell.Layers[0];
            var kind = layer == null ? string.Empty : layer.LayerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 96, 134, 188 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 218, 94, 92 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 196, 164, 84 };
            }

            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 136, 196, 126 };
            }

            return new[] { 178, 188, 156 };
        }

        private static string GetBlueprintEntryHotkeyDisplay()
        {
            return GetBlueprintEntryHotkeyDisplay(ConfigService.HotkeySettings);
        }

        private static string GetBlueprintEntryHotkeyDisplay(HotkeySettings settings)
        {
            return string.Empty;
        }

        private static float ResolveBlueprintButtonScale(string text, int availableWidth)
        {
            var scale = 0.72f;
            if (availableWidth <= 0 || string.IsNullOrWhiteSpace(text))
            {
                return scale;
            }

            var width = UiTextRenderer.EstimateTextWidth(text, scale);
            return width <= availableWidth ? scale : Math.Max(0.56f, scale * availableWidth / Math.Max(1, width));
        }

        internal static int CalculateBlueprintContentHeightForTesting()
        {
            return CalculateBlueprintContentHeight();
        }

        internal static int GetBlueprintTopSettingRowCountForTesting()
        {
            return BlueprintTopSettingRowCount;
        }

        internal static int GetBlueprintActionShortcutRowCountForTesting()
        {
            return BlueprintActionShortcutRowCount;
        }

        internal static int GetBlueprintMenuOpenRowCountForTesting()
        {
            return BlueprintMenuOpenRowCount;
        }

        internal static string GetBlueprintCreateActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintCreateAction;
        }

        internal static string GetBlueprintSaveActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintSaveAction;
        }

        internal static string GetBlueprintLibraryActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintLibraryAction;
        }

        internal static string GetBlueprintMoveActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintMoveAction;
        }

        internal static string GetBlueprintRegionActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintRegionAction;
        }

        internal static string GetBlueprintMirrorActionHotkeyElementIdForTesting()
        {
            return BlueprintActionHotkeyElementPrefix + FeatureIds.BlueprintMirrorAction;
        }

        internal static string GetBlueprintCreateActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.StartCreate;
        }

        internal static string GetBlueprintSaveActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.FinishCreateSave;
        }

        internal static string GetBlueprintLibraryActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.OpenLibrary;
        }

        internal static string GetBlueprintMoveActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.StartMove;
        }

        internal static string GetBlueprintRegionActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.StartRegionModify;
        }

        internal static string GetBlueprintMirrorActionElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.StartMirror;
        }

        internal static int GetBlueprintActionHotkeyInputMaxWidthForTesting()
        {
            return BlueprintActionHotkeyInputMaxWidth;
        }

        internal static int ResolveBlueprintActionHotkeyInputWidthForTesting(int availableWidth)
        {
            return ResolveBlueprintActionHotkeyInputWidth(availableWidth);
        }

        internal static void CalculateBlueprintActionShortcutRowRectsForTesting(
            LegacyUiRect row,
            string buttonText,
            out LegacyUiRect labelRect,
            out LegacyUiRect inputRect,
            out LegacyUiRect buttonRect)
        {
            CalculateBlueprintActionShortcutRowRects(row, buttonText, out labelRect, out inputRect, out buttonRect);
        }

        internal static string[] GetBlueprintActionHotkeyTooltipLinesForTesting()
        {
            return GetBlueprintActionHotkeyTooltipLines();
        }

        internal static string[] GetBlueprintCreateSaveButtonTooltipsForTesting()
        {
            return new[] { GetBlueprintCreateActionButtonTooltip(AppSettings.CreateDefault()), BlueprintSaveActionButtonTooltip };
        }

        internal static string GetBlueprintCreateActionButtonTextForTesting(AppSettings settings)
        {
            return GetBlueprintCreateActionButtonText(settings);
        }

        internal static string GetBlueprintCreateActionButtonTooltipForTesting(AppSettings settings)
        {
            return GetBlueprintCreateActionButtonTooltip(settings);
        }

        internal static string GetBlueprintMoveActionButtonTextForTesting()
        {
            return IsBlueprintMoveActionCancelState()
                ? BlueprintCancelMoveActionButtonText
                : BlueprintMoveActionButtonText;
        }

        internal static string GetBlueprintMoveActionButtonTooltipForTesting()
        {
            return IsBlueprintMoveActionCancelState()
                ? BlueprintCancelMoveActionButtonTooltip
                : BlueprintMoveActionButtonTooltip;
        }

        internal static string GetBlueprintRegionActionButtonTextForTesting()
        {
            return IsBlueprintRegionActionCancelState()
                ? BlueprintCancelRegionActionButtonText
                : BlueprintRegionActionButtonText;
        }

        internal static string GetBlueprintRegionActionButtonTooltipForTesting()
        {
            return IsBlueprintRegionActionCancelState()
                ? BlueprintCancelRegionActionButtonTooltip
                : BlueprintRegionActionButtonTooltip;
        }

        internal static string GetBlueprintMirrorActionButtonTextForTesting()
        {
            return IsBlueprintMirrorActionCancelState()
                ? BlueprintCancelMirrorActionButtonText
                : BlueprintMirrorActionButtonText;
        }

        internal static string GetBlueprintMirrorActionButtonTooltipForTesting()
        {
            return IsBlueprintMirrorActionCancelState()
                ? BlueprintCancelMirrorActionButtonTooltip
                : BlueprintMirrorActionButtonTooltip;
        }

        internal static string GetBlueprintLibraryOpenElementIdForTesting()
        {
            return BlueprintActionEntryElementPrefix + BlueprintEntryCommands.OpenLibrary;
        }

        internal static string GetBlueprintLibraryBackElementIdForTesting()
        {
            return BlueprintLibraryUiState.BuildCommandId("back", string.Empty);
        }

        internal static string GetBlueprintLibraryImportElementIdForTesting()
        {
            return BlueprintLibraryUiState.BuildCommandId("import", string.Empty);
        }

        internal static LegacyUiRect CalculateBlueprintLibraryImportButtonRectForTesting(LegacyUiRect toolbarRect)
        {
            return CalculateBlueprintLibraryImportButtonRect(toolbarRect);
        }

        internal static LegacyUiRect CalculateBlueprintLibraryPreviousButtonRectForTesting(LegacyUiRect headerRect)
        {
            LegacyUiRect importRect;
            LegacyUiRect prevRect;
            LegacyUiRect nextRect;
            LegacyUiRect backRect;
            CalculateBlueprintLibraryHeaderButtonRects(headerRect, out importRect, out prevRect, out nextRect, out backRect);
            return prevRect;
        }

        internal static LegacyUiRect CalculateBlueprintLibraryNextButtonRectForTesting(LegacyUiRect headerRect)
        {
            LegacyUiRect importRect;
            LegacyUiRect prevRect;
            LegacyUiRect nextRect;
            LegacyUiRect backRect;
            CalculateBlueprintLibraryHeaderButtonRects(headerRect, out importRect, out prevRect, out nextRect, out backRect);
            return nextRect;
        }

        internal static string[] GetBlueprintLibraryHeaderToolTooltipLinesForTesting(string action)
        {
            if (string.Equals(action, "import", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "page-prev", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "page-next", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new[] { "返回蓝图主菜单" };
        }

        internal static int GetBlueprintLibraryCardColumnsForTesting()
        {
            return BlueprintLibraryCardColumns;
        }

        internal static int GetBlueprintLibraryCardHeightForTesting()
        {
            return BlueprintLibraryCardHeight;
        }

        internal static int GetBlueprintLibraryCardGapForTesting()
        {
            return BlueprintLibraryCardGap;
        }

        internal static int CalculateBlueprintLibraryListHeightForTesting(bool loadSucceeded, int templateCount, int visibleCount)
        {
            return CalculateBlueprintLibraryListHeight(loadSucceeded, templateCount, visibleCount);
        }

        internal static LegacyUiRect CalculateBlueprintLibraryCardRectForTesting(LegacyUiRect viewport, int screenY, int visibleIndex)
        {
            return CalculateBlueprintLibraryCardRect(viewport, screenY, visibleIndex);
        }

        internal static string BuildBlueprintLibraryLayoutCommandIdForTesting(string action, string templateId)
        {
            return BlueprintLibraryUiState.BuildCommandId("layout-" + (action ?? string.Empty), templateId);
        }

        internal static string BuildBlueprintLibraryMaterialCloseCommandIdForTesting(string templateId)
        {
            return BlueprintLibraryUiState.BuildCommandId("materials-close", templateId);
        }

        internal static string GetBlueprintLibraryMaterialModalElementIdForTesting()
        {
            return BlueprintLibraryMaterialModalElementId;
        }

        internal static bool RegisterBlueprintLibraryMaterialModalOverlayForTesting(LegacyScrollArea area)
        {
            return RegisterBlueprintLibraryMaterialModalOverlay(area, BlueprintLibraryUiState.GetSnapshot());
        }

        internal static int GetBlueprintLibraryMaterialModalScrollOffsetForTesting()
        {
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return _blueprintLibraryMaterialModalScrollOffset;
            }
        }

        internal static string BuildBlueprintLibraryHeaderSummaryForTesting(int templateCount, int placedCount)
        {
            return BuildBlueprintLibraryHeaderSummary(templateCount, placedCount);
        }

        internal static string BuildBlueprintCapabilityTextForTesting(BlueprintTemplateRecord template)
        {
            return BuildBlueprintCapabilityText(template);
        }

        internal static float GetBlueprintLibraryCardSummaryTextScaleForTesting()
        {
            return BlueprintLibraryCardSummaryTextScale;
        }

        internal static LegacyUiRect GetBlueprintLibrarySubmenuContentBoundsForTesting(LegacyUiRect viewport)
        {
            return viewport;
        }

        internal static bool IsBlueprintSubmenuHeaderVisibleForTesting(LegacyScrollArea area)
        {
            return IsBlueprintSubmenuHeaderVisible(area);
        }

        internal static LegacyUiElement DrawBlueprintLibrarySubmenuForTesting(
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements)
        {
            return DrawBlueprintLibrarySubmenu(null, area, mouse, elements);
        }

        internal static LegacyUiElement DrawBlueprintPlacedSubmenuForTesting(
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements)
        {
            return DrawBlueprintPlacedSubmenu(null, area, mouse, elements);
        }

        internal static string GetBlueprintPlacedOpenElementIdForTesting()
        {
            return BlueprintEntryElementPrefix + BlueprintEntryCommands.OpenPlacedInstances;
        }

        internal static string GetBlueprintReplacementConfigPopupElementIdForTesting()
        {
            return BlueprintReplacementConfigPopupElementId;
        }

        internal static string GetBlueprintReplacementOptionElementIdForTesting(string category)
        {
            return "blueprint-replacement-category:" + (category ?? string.Empty);
        }

        internal static bool RegisterBlueprintReplacementConfigPopupOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor)
        {
            _blueprintReplacementConfigOpen = true;
            _blueprintReplacementConfigAnchor = anchor;
            _blueprintReplacementConfigAnchorVisible = true;
            return RegisterBlueprintReplacementConfigPopupOverlay(area, AppSettings.CreateDefault());
        }

        internal static void ResetBlueprintReplacementConfigPopupForTesting()
        {
            _blueprintReplacementConfigOpen = false;
            _blueprintReplacementConfigAnchor = new LegacyUiRect();
            _blueprintReplacementConfigAnchorVisible = false;
        }

        internal static string GetBlueprintStatusBarElementIdForTesting()
        {
            return BlueprintStatusBarElementId;
        }

        internal static int GetBlueprintLibraryPageSizeForTesting()
        {
            return BlueprintLibraryUiState.GetPageSizeForTesting();
        }

        internal static string BuildBlueprintLibraryNameInputIdForTesting(string templateId)
        {
            return BlueprintLibraryUiState.BuildNameInputId(templateId);
        }

        internal static string BuildBlueprintLibraryCommandIdForTesting(string action, string templateId)
        {
            return BlueprintLibraryUiState.BuildCommandId(action, templateId);
        }

        internal static string GetBlueprintLibraryVisualContractForTesting()
        {
            return BlueprintLibraryVisualContract;
        }

        internal static string GetBlueprintCreationVisualContractForTesting()
        {
            return BlueprintCreationVisualContract;
        }

        internal static string GetBlueprintActionShortcutVisualContractForTesting()
        {
            return BlueprintActionShortcutVisualContract;
        }

        internal static string GetBlueprintProjectionVisualContractForTesting()
        {
            return BlueprintProjectionVisualContract;
        }

        internal static string GetBlueprintMaterialVisualContractForTesting()
        {
            return BlueprintMaterialVisualContract;
        }

        internal static string GetBlueprintEraseVisualContractForTesting()
        {
            return BlueprintEraseVisualContract;
        }

        internal static string GetBlueprintReplacementVisualContractForTesting()
        {
            return BlueprintReplacementVisualContract;
        }

        internal static string GetBlueprintAutoPlacementVisualContractForTesting()
        {
            return BlueprintAutoPlacementVisualContract;
        }

        internal static string GetBlueprintMirrorVisualContractForTesting()
        {
            return BlueprintMirrorVisualContract;
        }

        internal static string GetBlueprintPlacedInstanceVisualContractForTesting()
        {
            return BlueprintPlacedInstanceVisualContract;
        }

        internal static int GetBlueprintPlacedInstancePageSizeForTesting()
        {
            return BlueprintPlacedInstanceUiState.GetPageSizeForTesting();
        }

        internal static string BuildBlueprintPlacedInstanceCommandIdForTesting(string action, string instanceId)
        {
            return BlueprintPlacedInstanceUiState.BuildCommandId(action, instanceId);
        }

        internal static string GetBlueprintPlacedMaterialModalElementIdForTesting()
        {
            return BlueprintPlacedMaterialModalElementId;
        }

        internal static string BuildBlueprintPlacedMaterialCloseCommandIdForTesting(string instanceId)
        {
            return BlueprintPlacedInstanceUiState.BuildCommandId("materials-close", instanceId);
        }

        internal static bool RegisterBlueprintPlacedMaterialModalOverlayForTesting(LegacyScrollArea area)
        {
            return RegisterBlueprintPlacedMaterialModalOverlay(area, BlueprintPlacedInstanceUiState.GetSnapshot());
        }

        internal static int GetBlueprintPlacedMaterialModalScrollOffsetForTesting()
        {
            lock (BlueprintMaterialModalScrollSyncRoot)
            {
                return _blueprintPlacedMaterialModalScrollOffset;
            }
        }

        internal static int CalculateBlueprintPlacedListHeightForTesting(bool loadSucceeded, int instanceCount, int visibleCount)
        {
            return CalculateBlueprintPlacedListHeight(loadSucceeded, instanceCount, visibleCount);
        }

        internal static int CalculateBlueprintPlacedMaterialPanelHeightForTesting()
        {
            return CalculateBlueprintPlacedMaterialPanelHeight(BlueprintMaterialService.GetCachedSnapshotForDraw());
        }

        internal static string BuildBlueprintPlacedHeaderSummaryForTesting(int placedCount)
        {
            return BuildBlueprintPlacedHeaderSummary(placedCount);
        }

        internal static string BuildBlueprintPlacedMaterialSummaryForTesting()
        {
            return BuildBlueprintPlacedMaterialSummary(BlueprintMaterialService.GetCachedSnapshotForDraw());
        }

        internal static float GetBlueprintPlacedMaterialLineTextScaleForTesting()
        {
            return BlueprintPlacedMaterialLineTextScale;
        }

        internal static LegacyUiRect CalculateBlueprintPlacedCardRectForTesting(LegacyUiRect viewport, int screenY, int visibleIndex)
        {
            return CalculateBlueprintPlacedCardRect(viewport, screenY, visibleIndex);
        }

        internal static string BuildBlueprintPlacedInstanceMaterialListTextForTesting(BlueprintWorldInstanceRecord instance)
        {
            return string.Join("\n", BuildBlueprintPlacedInstanceMaterialLines(instance).ToArray());
        }

        internal static string BuildBlueprintPlacementSummaryForTesting()
        {
            return BuildBlueprintPlacementSummary(BlueprintPlacementPreviewState.GetSnapshot());
        }

        internal static string BuildBlueprintProjectionSummaryForTesting()
        {
            return BuildBlueprintProjectionSummary(BlueprintProjectionService.GetSnapshot());
        }

        internal static string BuildBlueprintMaterialSummaryForTesting()
        {
            return BuildBlueprintMaterialSummary(BlueprintMaterialService.GetSnapshot());
        }

        internal static string BuildBlueprintPlacedMaterialComparisonForTesting(int maxItems)
        {
            return string.Join("\n", BuildBlueprintPlacedMaterialLines(BlueprintMaterialService.GetCachedSnapshotForDraw(), maxItems).ToArray());
        }

        internal static string BuildBlueprintEraseSummaryForTesting()
        {
            return BuildBlueprintEraseSummary(BlueprintEraseRegionState.GetSnapshot());
        }

        internal static string BuildBlueprintAutoPlacementSummaryForTesting()
        {
            return BuildBlueprintAutoPlacementSummary(BlueprintAutoPlacementService.GetDiagnostics());
        }

        internal static string BuildBlueprintTemplateSummaryForTesting(BlueprintTemplateRecord template)
        {
            return BlueprintLibraryUiState.BuildTemplateSummary(template);
        }

        internal static string BuildBlueprintTemplateMaterialListTextForTesting(BlueprintTemplateRecord template, int maxItems)
        {
            return BlueprintLibraryUiState.BuildTemplateMaterialListText(template, maxItems);
        }

        internal static bool TryResolveBlueprintTemplatePreviewLayoutForTesting(
            BlueprintTemplateRecord template,
            LegacyUiRect rect,
            out int originX,
            out int originY,
            out int drawWidth,
            out int drawHeight,
            out double scale)
        {
            return TryResolveBlueprintTemplatePreviewLayout(template, rect, out originX, out originY, out drawWidth, out drawHeight, out scale);
        }

        internal static string GetBlueprintEntryHotkeyDisplayForTesting()
        {
            return GetBlueprintEntryHotkeyDisplay();
        }

        internal static string GetBlueprintActionHotkeyDisplayForTesting(HotkeySettings settings, string targetId)
        {
            return GetBlueprintHotkeyDisplay(settings, targetId);
        }
    }
}
