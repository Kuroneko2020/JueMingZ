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
        private static LegacyUiElement DrawInformationPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;
            _informationStylePopupAnchorVisible = false;

            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "敌怪显名", "information.enemy_name_labels", settings.InformationEnemyNameLabelsEnabled, "敌怪头顶显示名字", InformationStyleHelper.EnemyNameFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "动物显名", "information.critter_name_labels", settings.InformationCritterNameLabelsEnabled, "小动物头顶显示名字", InformationStyleHelper.CritterNameFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationNpcNameModeRow(spriteBatch, area, mouse, elements, y, settings.InformationNpcNameLabelsMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationChestNameModeRow(spriteBatch, area, mouse, elements, y, settings.InformationChestNameLabelsMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationSignTextModeRow(spriteBatch, area, mouse, elements, y, settings.InformationSignTextLabelsMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            if (ShouldDrawInformationSignTextLimitRow(settings.InformationSignTextLabelsMode))
            {
                hovered = DrawInformationSignTextLimitRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
                y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            }

            hovered = DrawInformationTombstoneTextModeRow(spriteBatch, area, mouse, elements, y, settings.InformationTombstoneTextLabelsMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            if (ShouldDrawInformationSignTextLimitRow(settings.InformationTombstoneTextLabelsMode))
            {
                hovered = DrawInformationTombstoneTextLimitRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
                y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            }

            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "显示生命水晶", "information.highlight_life_crystal", settings.InformationHighlightLifeCrystalEnabled, "需要金属探测器，高亮生命水晶") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "显示魔力水晶", "information.highlight_mana_crystal", settings.InformationHighlightManaCrystalEnabled, "需要金属探测器，高亮魔力水晶") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "显示碎岩龟", "information.highlight_digtoise", settings.InformationHighlightDigtoiseEnabled, "需要金属探测器，高亮碎岩龟") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "显示生命果", "information.highlight_life_fruit", settings.InformationHighlightLifeFruitEnabled, "需要金属探测器，高亮生命果") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "显示龙蛋", "information.highlight_dragon_egg", settings.InformationHighlightDragonEggEnabled, "需要金属探测器，高亮龙蛋") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;

            DrawInformationDivider(spriteBatch, area, y);
            y += InformationDividerHeight + LegacyUiMetrics.SectionGap;

            hovered = DrawInformationStartButtonRow(spriteBatch, area, mouse, elements, y) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "群系显示", "information.biome_display", settings.InformationBiomeDisplayEnabled, "显示玩家当前所在群系", InformationStyleHelper.BiomeDisplayFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "世界感染", "information.world_infection", settings.InformationWorldInfectionEnabled, "显示感染占比，需要树妖", InformationStyleHelper.WorldInfectionFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "幸运值", "information.luck_value", settings.InformationLuckValueEnabled, "显示幸运值，需要巫师", InformationStyleHelper.LuckValueFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "完整鱼获", "information.fishing_catches", settings.InformationFishingCatchesEnabled, "显示当前水域所有可能鱼获", InformationStyleHelper.FishingCatchesFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "过滤鱼获", "information.fishing_filtered_catches", settings.InformationFishingFilteredCatchesEnabled, "显示按当前钓鱼过滤模式筛选后的当前可能鱼获。", InformationStyleHelper.FishingFilteredCatchesFeatureId) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawInformationToggleRow(spriteBatch, area, mouse, elements, y, "渔夫任务", "information.angler_quest", settings.InformationAnglerQuestEnabled, "显示当天渔夫任务，需要渔夫", InformationStyleHelper.AnglerQuestFeatureId) ?? hovered;

            RegisterInformationStylePopupOverlay(area, settings);

            return hovered;
        }

        private static LegacyUiElement DrawInformationToggleRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string label, string featureId, bool enabled, string enabledTooltip, string styleFeatureId = null)
        {
            return DrawBinaryModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                label,
                enabled,
                "information-toggle:" + featureId + ":",
                enabledTooltip,
                styleFeatureId);
        }

        private static LegacyUiElement DrawInformationNpcNameModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "NPC显名",
                NormalizeInformationNpcNameLabelsMode(mode),
                new[] { "名字", "类型", "关闭" },
                new[] { "Name", "Type", "Off" },
                "information-npc-name-label-mode:",
                new[] { "NPC头顶显示名字", "NPC头顶显示类型", null },
                InformationStyleHelper.NpcNameFeatureId);
        }

        private static LegacyUiElement DrawInformationChestNameModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "宝箱显名",
                NormalizeInformationChestNameLabelsMode(mode),
                new[] { "始终", "开过", "关闭" },
                new[] { "Always", "Opened", "Off" },
                "information-chest-name-label-mode:",
                new[] { "始终显示宝箱名字，需要金属探测器", "显示开过的宝箱名称，无需金属探测器", null },
                InformationStyleHelper.ChestNameFeatureId);
        }

        private static LegacyUiElement DrawInformationSignTextModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "牌子显示",
                InformationSignTextModes.Normalize(mode),
                new[] { "全部", "前几行", "前几字", "关闭" },
                new[] { InformationSignTextModes.All, InformationSignTextModes.Lines, InformationSignTextModes.Characters, InformationSignTextModes.Off },
                "information-sign-text-label-mode:",
                new[] { "显示最多十行内容", "只显示前几行，用加减按钮调整", "只显示前几个字，用加减按钮调整", null },
                InformationStyleHelper.SignTextFeatureId);
        }

        private static LegacyUiElement DrawInformationTombstoneTextModeRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, string mode)
        {
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "墓碑显示",
                InformationSignTextModes.Normalize(mode),
                new[] { "全部", "前几行", "前几字", "关闭" },
                new[] { InformationSignTextModes.All, InformationSignTextModes.Lines, InformationSignTextModes.Characters, InformationSignTextModes.Off },
                "information-tombstone-text-label-mode:",
                new[] { "显示最多十行内容", "只显示前几行，用加减按钮调整", "只显示前几个字，用加减按钮调整", null },
                InformationStyleHelper.TombstoneTextFeatureId);
        }

        private static bool ShouldDrawInformationSignTextLimitRow(string mode)
        {
            var normalized = InformationSignTextModes.Normalize(mode);
            return string.Equals(normalized, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase);
        }

        private static LegacyUiElement DrawInformationSignTextLimitRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var mode = InformationSignTextModes.Normalize(settings == null ? null : settings.InformationSignTextLabelsMode);
            var isLineMode = string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase);
            var label = isLineMode ? "牌子行数" : "牌子字数";
            var valueText = isLineMode
                ? "前 " + InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines).ToString(CultureInfo.InvariantCulture) + " 行"
                : "前 " + InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters).ToString(CultureInfo.InvariantCulture) + " 字";

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var buttonY = RowModeButtonY(row);
            var smallWidth = 34;
            var valueWidth = Math.Max(92, UiTextRenderer.EstimateTextWidth(valueText, 0.78f) + 20);
            var gap = 6;
            var totalWidth = smallWidth + gap + valueWidth + gap + smallWidth;
            var x = row.Right - totalWidth - 10;
            var labelWidth = Math.Max(60, x - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var hovered = (LegacyUiElement)null;
            var decreaseRect = new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight);
            hovered = DrawInformationSignTextLimitButton(spriteBatch, area, mouse, elements, decreaseRect, "information-sign-text-limit-decrease", label + ":减少", "-", "减少显示上限") ?? hovered;
            var valueRect = new LegacyUiRect(decreaseRect.Right + gap, buttonY, valueWidth, RowModeButtonHeight);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, valueRect, false, false, true, false, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, valueText, valueRect.X + 3, valueRect.Y, valueRect.Width - 6, valueRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 255, 245, 205, 255, 0.78f);
            var increaseRect = new LegacyUiRect(valueRect.Right + gap, buttonY, smallWidth, RowModeButtonHeight);
            hovered = DrawInformationSignTextLimitButton(spriteBatch, area, mouse, elements, increaseRect, "information-sign-text-limit-increase", label + ":增加", "+", isLineMode ? "增加显示行数，原版悬停最多 10 行" : "增加显示字数，最多 1200 字") ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawInformationTombstoneTextLimitRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            var mode = InformationSignTextModes.Normalize(settings == null ? null : settings.InformationTombstoneTextLabelsMode);
            var isLineMode = string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase);
            var label = isLineMode ? "墓碑行数" : "墓碑字数";
            var valueText = isLineMode
                ? "前 " + InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines).ToString(CultureInfo.InvariantCulture) + " 行"
                : "前 " + InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters).ToString(CultureInfo.InvariantCulture) + " 字";

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            var buttonY = RowModeButtonY(row);
            var smallWidth = 34;
            var valueWidth = Math.Max(92, UiTextRenderer.EstimateTextWidth(valueText, 0.78f) + 20);
            var gap = 6;
            var totalWidth = smallWidth + gap + valueWidth + gap + smallWidth;
            var x = row.Right - totalWidth - 10;
            var labelWidth = Math.Max(60, x - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var hovered = (LegacyUiElement)null;
            var decreaseRect = new LegacyUiRect(x, buttonY, smallWidth, RowModeButtonHeight);
            hovered = DrawInformationSignTextLimitButton(spriteBatch, area, mouse, elements, decreaseRect, "information-tombstone-text-limit-decrease", label + ":减少", "-", "减少显示上限") ?? hovered;
            var valueRect = new LegacyUiRect(decreaseRect.Right + gap, buttonY, valueWidth, RowModeButtonHeight);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, valueRect, false, false, true, false, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, valueText, valueRect.X + 3, valueRect.Y, valueRect.Width - 6, valueRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 255, 245, 205, 255, 0.78f);
            var increaseRect = new LegacyUiRect(valueRect.Right + gap, buttonY, smallWidth, RowModeButtonHeight);
            hovered = DrawInformationSignTextLimitButton(spriteBatch, area, mouse, elements, increaseRect, "information-tombstone-text-limit-increase", label + ":增加", "+", isLineMode ? "增加显示行数，最多 10 行" : "增加显示字数，最多 1200 字") ?? hovered;
            return hovered;
        }

        private static LegacyUiElement DrawInformationSignTextLimitButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, string id, string label, string text, string tooltip)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, text, rect.X + 3, rect.Y, rect.Width - 6, rect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 230, 232, 224, 255, 0.84f);
            var element = AddFrameElement(elements, id, label, "button", elementRect, tooltipLines: string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static void DrawInformationDivider(object spriteBatch, LegacyScrollArea area, int contentY)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, InformationDividerHeight);
            if (!area.IsVisible(rect))
            {
                return;
            }

            var lineY = rect.Y + rect.Height / 2;
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 12, lineY - 1, Math.Max(0, rect.Width - 24), 2, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 156, 168, 194, 190);
        }

        private static LegacyUiElement DrawInformationStartButtonRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY)
        {
            var row = new LegacyUiRect(area.Viewport.X, area.ToScreenY(contentY), area.Viewport.Width, LegacyUiMetrics.RowHeight);
            if (!area.IsVisible(row))
            {
                return null;
            }

            LegacyUiTheme.DrawRowClipped(spriteBatch, row, area.Viewport);
            const string label = "调整信息窗位置";
            const string buttonLabel = "开始";
            const int buttonWidth = 76;
            var buttonRect = new LegacyUiRect(row.Right - buttonWidth - 10, RowModeButtonY(row), buttonWidth, RowModeButtonHeight);
            var labelWidth = Math.Max(60, buttonRect.X - row.X - 20);
            UiTextRenderer.DrawAlignedTextClipped(spriteBatch, label, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 238, 238, 226, 255, 0.86f);

            var hit = buttonRect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : buttonRect;
            var hovered = IsFrameElementHovered("information-info-panel-position-start", elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, buttonRect, hovered, hovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, buttonLabel, buttonRect.X + 3, buttonRect.Y, buttonRect.Width - 6, buttonRect.Height, area.Viewport.X, area.Viewport.Y, area.Viewport.Width, area.Viewport.Height, 230, 232, 224, 255, 0.78f);

            var element = AddFrameElement(elements, "information-info-panel-position-start", label + ":" + buttonLabel, "button", elementRect, tooltipLines: new[] { "点击开始拖动信息窗口" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static bool RegisterInformationStylePopupOverlay(LegacyScrollArea area, AppSettings settings)
        {
            var featureId = _informationStylePopupFeatureId;
            if (!InformationStyleHelper.IsConfigurable(featureId) || !_informationStylePopupAnchorVisible || area == null)
            {
                return false;
            }

            var popup = CalculateInformationStylePopupRect(area.Viewport, _informationStylePopupAnchor);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = "information-style-popup",
                OwnerPageId = "information",
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 20,
                CacheSignature = BuildInformationStylePopupCacheSignature(settings, featureId),
                State = area,
                Draw = DrawInformationStylePopupOverlay
            });
        }

        private static void DrawInformationStylePopupOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            var area = request == null ? null : request.State as LegacyScrollArea;
            var elements = context == null ? null : context.Elements as List<LegacyUiElement>;
            if (context == null || area == null || elements == null)
            {
                return;
            }

            DrawInformationStylePopup(context.SpriteBatch, area, context.Mouse, elements);
        }

        private static LegacyUiElement DrawInformationStylePopup(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var featureId = _informationStylePopupFeatureId;
            if (!InformationStyleHelper.IsConfigurable(featureId) || !_informationStylePopupAnchorVisible || area == null)
            {
                return null;
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var popup = CalculateInformationStylePopupRect(area.Viewport, _informationStylePopupAnchor);
            var inputId = "information-style-html:" + featureId;
            var colorHex = InformationStyleHelper.GetColorHex(settings, featureId);
            string typedColor;
            if (LegacyHexColorInput.Update(inputId, colorHex, out typedColor))
            {
                InformationStyleHelper.SetColorHex(settings, featureId, typedColor);
                ConfigService.SaveAll();
                colorHex = InformationStyleHelper.GetColorHex(settings, featureId);
            }

            var color = InformationColorHelper.ParseHex(colorHex, new InformationColor(255, 255, 255, 255));
            int hue;
            int saturation;
            int lightness;
            InformationStyleHelper.ColorToHsl(color, out hue, out saturation, out lightness);

            var hueId = "information-style-h:" + featureId;
            var saturationId = "information-style-s:" + featureId;
            var lightnessId = "information-style-l:" + featureId;
            var displayHue = LegacyUiInput.GetSliderDisplayValue(hueId, hue);
            var displaySaturation = LegacyUiInput.GetSliderDisplayValue(saturationId, saturation);
            var displayLightness = LegacyUiInput.GetSliderDisplayValue(lightnessId, lightness);
            var previewHex = InformationStyleHelper.ColorFromHsl(displayHue, displaySaturation, displayLightness);
            var previewColor = InformationColorHelper.ParseHex(previewHex, color);
            var fontScale = InformationStyleHelper.GetFontScale(settings, featureId);
            var hovered = (LegacyUiElement)null;

            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, popup.X, popup.Y, popup.Width, popup.Height, LegacyUiTheme.Radius, 48, 58, 88, 238);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, popup.X + 1, popup.Y + 1, popup.Width - 2, popup.Height - 2, LegacyUiTheme.Radius - 1, 18, 23, 38, 238);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, popup.X + 8, popup.Y + 32, popup.Width - 16, 1, 116, 136, 176, 145);
            AddFrameElement(elements, "information-style-popup", "信息样式配置", "blocker", popup);
            UiTextRenderer.DrawText(spriteBatch, InformationStyleHelper.GetDisplayName(featureId) + " 配置", popup.X + 14, popup.Y + 10, 246, 242, 220, 255, 0.82f);
            hovered = DrawInformationStyleSmallButton(spriteBatch, mouse, elements, "information-style-reset:" + featureId, "回归默认", popup.Right - 86, popup.Y + 7, 72, 22) ?? hovered;

            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, popup.X + 16, popup.Y + 48, 50, 50, 6, previewColor.R, previewColor.G, previewColor.B, 245);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, popup.X + 16, popup.Y + 48, 50, 50, 2, 220, 226, 240, 210);

            var sliderLabelX = popup.X + 82;
            var sliderValueX = popup.X + 106;
            var sliderX = popup.X + 158;
            var sliderWidth = popup.Right - sliderX - 20;
            hovered = DrawInformationStyleSlider(spriteBatch, mouse, elements, hueId, "H", displayHue, 0, 360, sliderLabelX, sliderValueX, sliderX, popup.Y + 40, sliderWidth, string.Empty) ?? hovered;
            hovered = DrawInformationStyleSlider(spriteBatch, mouse, elements, saturationId, "S", displaySaturation, 0, 100, sliderLabelX, sliderValueX, sliderX, popup.Y + 74, sliderWidth, "%") ?? hovered;
            hovered = DrawInformationStyleSlider(spriteBatch, mouse, elements, lightnessId, "L", displayLightness, 0, 100, sliderLabelX, sliderValueX, sliderX, popup.Y + 108, sliderWidth, "%") ?? hovered;

            var inputRect = new LegacyUiRect(popup.X + 74, popup.Y + 142, 118, 28);
            var inputHovered = IsFrameElementHovered(inputId, inputRect, mouse);
            var inputFocused = LegacyHexColorInput.IsFocused(inputId);
            UiTextRenderer.DrawAlignedText(spriteBatch, "HTML", popup.X + 16, inputRect.Y, 54, inputRect.Height, UiTextHorizontalAlignment.Left, 220, 226, 238, 240, 0.62f);
            UiTextRenderer.DrawAlignedText(spriteBatch, LegacyHexColorInput.GetDisplayText(inputId, colorHex), inputRect.X + 1, inputRect.Y, inputRect.Width - 2, inputRect.Height - 3, UiTextHorizontalAlignment.Left, 242, 244, 236, 255, 0.66f);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, inputRect.X, inputRect.Bottom - 3, inputRect.Width, inputFocused ? 2 : 1, inputFocused ? 255 : 126, inputFocused ? 230 : 142, inputFocused ? 160 : 176, inputHovered || inputFocused ? 235 : 180);
            var inputElement = AddFrameElement(elements, inputId, InformationStyleHelper.GetDisplayName(featureId) + ":HTML颜色", "button", inputRect, tooltipLines: new[] { "点击后输入 6 位 HTML 颜色代码" });
            RecordFrameElementHover(inputElement, inputHovered);
            if (inputHovered)
            {
                hovered = inputElement;
            }

            var fontSummaryX = popup.X + 214;
            UiTextRenderer.DrawAlignedText(spriteBatch, "字号 " + InformationStyleHelper.FormatFontScale(fontScale), fontSummaryX, inputRect.Y, Math.Max(1, popup.Right - fontSummaryX - 16), inputRect.Height, UiTextHorizontalAlignment.Left, 235, 238, 226, 245, 0.66f);
            var fontButtonY = popup.Y + 184;
            hovered = DrawInformationStyleButton(spriteBatch, mouse, elements, "information-style-font-decrease:" + featureId, "缩小字号", popup.X + 16, fontButtonY, 96) ?? hovered;
            UiTextRenderer.DrawCenteredText(spriteBatch, InformationStyleHelper.FormatFontScale(fontScale), popup.X + 124, fontButtonY + 1, 86, 26, 238, 242, 232, 245, 0.70f);
            hovered = DrawInformationStyleButton(spriteBatch, mouse, elements, "information-style-font-increase:" + featureId, "加大字号", popup.X + 222, fontButtonY, 100) ?? hovered;

            return hovered;
        }

        private static int BuildInformationStylePopupCacheSignature(AppSettings settings, string featureId)
        {
            unchecked
            {
                settings = settings ?? AppSettings.CreateDefault();
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(featureId ?? string.Empty);
                hash = hash * 31 + settings.ConfigVersion;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(InformationStyleHelper.GetColorHex(settings, featureId) ?? string.Empty);
                hash = hash * 31 + (int)Math.Round(InformationStyleHelper.GetFontScale(settings, featureId) * 1000d);
                hash = hash * 31 + (LegacyHexColorInput.IsFocused("information-style-html:" + featureId) ? 1 : 0);
                return hash;
            }
        }

        private static LegacyUiRect CalculateInformationStylePopupRect(LegacyUiRect viewport, LegacyUiRect anchor)
        {
            var x = ClampInt(anchor.X, viewport.X + 6, viewport.Right - InformationStylePopupWidth - 6);
            var y = anchor.Y - InformationStylePopupHeight - 8;
            if (y < viewport.Y + 6)
            {
                y = anchor.Bottom + 8;
            }

            y = ClampInt(y, viewport.Y + 6, viewport.Bottom - InformationStylePopupHeight - 6);
            return new LegacyUiRect(x, y, InformationStylePopupWidth, InformationStylePopupHeight);
        }

        internal static bool RegisterInformationStylePopupOverlayForTesting(LegacyScrollArea area, LegacyUiRect anchor, string featureId)
        {
            _informationStylePopupFeatureId = featureId;
            _informationStylePopupAnchor = anchor;
            _informationStylePopupAnchorVisible = true;
            return RegisterInformationStylePopupOverlay(area, AppSettings.CreateDefault());
        }

        internal static void ResetInformationStylePopupForTesting()
        {
            LegacyHexColorInput.ClearFocus();
            _informationStylePopupFeatureId = string.Empty;
            _informationStylePopupAnchor = new LegacyUiRect();
            _informationStylePopupAnchorVisible = false;
        }

        private static LegacyUiElement DrawInformationStyleSlider(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, string id, string label, int value, int minValue, int maxValue, int labelX, int valueX, int x, int y, int width, string suffix)
        {
            var slider = new LegacySliderControl
            {
                Id = id,
                Label = label,
                Kind = "slider",
                Bounds = new LegacyUiRect(x, y, width, InformationStyleSliderHeight),
                IntValue = value,
                MinValue = minValue,
                MaxValue = maxValue,
                TooltipLines = new[] { label + " 调整" }
            };
            var element = slider.RegisterAndUpdate(new LegacyUiContext(spriteBatch, mouse, LegacyMainUiState.WindowRect, LegacyMainUiState.SelectedPageId, ConfigService.AppSettings ?? AppSettings.CreateDefault(), elements));
            var dragging = string.Equals(LegacyUiInput.ActiveSliderId, slider.Id, StringComparison.Ordinal);
            var hovered = IsFrameElementHovered(slider.Id, slider.Bounds, mouse);
            DrawInformationStyleSliderTrack(spriteBatch, slider.Bounds, value, minValue, maxValue, hovered, dragging);
            UiTextRenderer.DrawAlignedText(
                spriteBatch,
                label,
                labelX,
                y,
                Math.Max(1, valueX - labelX - 4),
                InformationStyleSliderHeight,
                UiTextHorizontalAlignment.Left,
                hovered ? 255 : 235,
                hovered ? 245 : 235,
                210,
                255,
                0.78f);
            UiTextRenderer.DrawAlignedText(
                spriteBatch,
                value.ToString(CultureInfo.InvariantCulture) + (suffix ?? string.Empty),
                valueX,
                y,
                Math.Max(1, x - valueX - 8),
                InformationStyleSliderHeight,
                UiTextHorizontalAlignment.Right,
                hovered ? 255 : 235,
                hovered ? 245 : 235,
                210,
                255,
                0.74f);
            return hovered ? element : null;
        }

        private static void DrawInformationStyleSliderTrack(object spriteBatch, LegacyUiRect rect, int value, int minValue, int maxValue, bool hovered, bool dragging)
        {
            value = LegacyMainUiState.Clamp(value, minValue, maxValue);
            var trackY = rect.Y + rect.Height / 2 - 3;
            var trackX = rect.X + 10;
            var trackWidth = Math.Max(1, rect.Width - 24);
            var fillWidth = (int)Math.Round(trackWidth * ((value - minValue) / (double)(maxValue - minValue)));
            var knobX = trackX + fillWidth - 7;
            object texture;
            if (VanillaUiSkinCompat.TryGetColorBarTexture(out texture))
            {
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, trackX, trackY, trackWidth, 7, rect.X, rect.Y, rect.Width, rect.Height, 255, 255, 255, 210);
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, trackX, trackY, Math.Max(7, fillWidth), 7, rect.X, rect.Y, rect.Width, rect.Height, 255, 255, 255, 246);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, trackX, trackY, trackWidth, 7, rect.X, rect.Y, rect.Width, rect.Height, 4, 15, 21, 44, 220);
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, trackX, trackY, Math.Max(7, fillWidth), 7, rect.X, rect.Y, rect.Width, rect.Height, 4, 95, 132, 188, 230);
                UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, trackX, trackY, trackWidth, 7, 1, rect.X, rect.Y, rect.Width, rect.Height, 115, 143, 198, 220);
            }

            if (VanillaUiSkinCompat.TryGetColorSliderTexture(out texture) ||
                VanillaUiSkinCompat.TryGetColorBlipTexture(out texture))
            {
                UiPrimitiveRenderer.DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, knobX, rect.Y + 5, 14, rect.Height - 10, rect.X, rect.Y, rect.Width, rect.Height, 255, 255, 255, dragging ? 255 : hovered ? 244 : 230);
            }
            else
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, knobX, rect.Y + 5, 14, rect.Height - 10, rect.X, rect.Y, rect.Width, rect.Height, 7, 65, 54, 32, dragging ? 255 : hovered ? 244 : 230);
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, knobX + 1, rect.Y + 6, 12, rect.Height - 12, rect.X, rect.Y, rect.Width, rect.Height, 6, dragging ? 232 : 196, dragging ? 224 : 208, 160, 238);
            }
        }

        private static LegacyUiElement DrawInformationStyleSmallButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, string id, string label, int x, int y, int width, int height)
        {
            var rect = new LegacyUiRect(x, y, width, height);
            var hovered = IsFrameElementHovered(id, rect, mouse);
            LegacyUiTheme.DrawButton(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true);
            UiTextRenderer.DrawCenteredText(spriteBatch, label, rect.X + 4, rect.Y, rect.Width - 8, rect.Height, 232, 236, 224, 255, 0.62f);
            var element = AddFrameElement(elements, id, label, "button", rect, tooltipLines: new[] { "恢复默认颜色和字号" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawInformationStyleButton(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, string id, string label, int x, int y, int width)
        {
            var rect = new LegacyUiRect(x, y, width, LegacyUiMetrics.ButtonHeight);
            var hovered = IsFrameElementHovered(id, rect, mouse);
            LegacyUiTheme.DrawButton(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true);
            UiTextRenderer.DrawCenteredText(spriteBatch, label, rect.X + 4, rect.Y, rect.Width - 8, rect.Height, 232, 236, 224, 255, 0.66f);
            var element = AddFrameElement(elements, id, label, "button", rect, tooltipLines: new[] { "每次调整 0.10" });
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }
    }
}
