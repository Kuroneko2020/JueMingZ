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
        private static LegacyUiElement DrawBuffPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;

            hovered = DrawHealModeRow(spriteBatch, area, mouse, elements, y, settings.AutoHealMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawManaModeRow(spriteBatch, area, mouse, elements, y, settings.AutoManaMode) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动护士", settings.AutoNurseEnabled, "auto-nurse-mode:", "靠近可交互护士且需要治疗或清除 Debuff 时尝试自动治疗。") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动家具", settings.AutoStationBuffEnabled, "auto-station-buff-mode:", "靠近可交互增益家具且缺少对应 Buff 时尝试自动交互。") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawAutoBuffToggleRow(spriteBatch, area, mouse, elements, y, settings.AutoBuffEnabled) ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SectionGap;

            DrawSection(spriteBatch, area, y, "自动增益列表");
            y += LegacyUiMetrics.SectionHeaderHeight;
            hovered = DrawDualBuffGrid(spriteBatch, area, mouse, elements, y) ?? hovered;

            return hovered;
        }

        private static LegacyUiElement DrawCombatPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var hovered = (LegacyUiElement)null;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var y = 0;

            hovered = DrawCombatAimAssistRow(spriteBatch, area, mouse, elements, y, settings) ?? hovered;
            y += CombatAimRowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动连点", settings.CombatAutoClickerEnabled, "combat-auto-clicker-mode:", "旧路线已清理，等待新核心接入") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "完美左轮", settings.CombatPerfectRevolverEnabled, "combat-perfect-revolver-mode:", "最大程度发挥左轮威力") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "省力魔法绳", settings.CombatMagicStringClickerEnabled, "combat-magic-string-clicker-mode:", "长按实现装备魔法绳连点的效果") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "自动转向", settings.CombatAutoFacingEnabled, "combat-auto-facing-mode:", "什么叫毁灭刃不能转头？") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "装备提示", settings.CombatEquipmentWarningEnabled, "combat-equipment-warning-mode:", "又忘了换装备了？") ?? hovered;
            y += LegacyUiMetrics.RowHeight + LegacyUiMetrics.SettingRowGap;
            hovered = DrawBinaryModeRow(spriteBatch, area, mouse, elements, y, "哥布林必死", settings.CombatGoblinExecutionEnabled, "combat-goblin-execution-mode:", "rnm 还钱！！") ?? hovered;

            return hovered;
        }

    }
}
