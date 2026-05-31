using System.Collections.Generic;

namespace JueMingZ.UI
{
    public static class DiagnosticTestButtonPanel
    {
        public const int StartX = 20;
        public const int StartY = 338;
        public const int ContentPaddingX = 14;
        public const int ContentPaddingY = 44;
        public const int SectionGap = 10;
        public const int RowHeight = 34;
        public const int ButtonHeight = 28;
        public const int ButtonGap = 8;
        public const int HitPaddingX = 6;
        public const int HitPaddingY = 5;
        public const int RowCount = 11;

        public static List<DiagnosticTestButton> BuildButtons(DiagnosticsOverlayModel model)
        {
            OperationWindowState.EnsureLoaded();
            var buttons = new List<DiagnosticTestButton>(24);
            var diagnosticsOn = model == null || model.EnableDiagnosticInputTests;
            var toggleLabel = diagnosticsOn ? "关闭诊断" : "开启诊断";
            var autoHealLabel = "自动回血 " + (model != null && model.AutoHealEnabled ? "ON" : "OFF");
            var autoManaLabel = "自动回蓝 " + (model != null && model.AutoManaEnabled ? "ON" : "OFF");
            var autoBuffLabel = "自动增益 " + (model != null && model.AutoBuffEnabled ? "ON" : "OFF");
            var x = OperationWindowState.X + ContentPaddingX;
            var y = OperationWindowState.Y + ContentPaddingY;
            var col1 = 0;
            var col2 = 126;
            var col3 = 252;
            var col4 = 378;

            Add(buttons, x, y, "prev-test-slot", "上一测试格", "切换测试快捷栏到上一格。", col1, 0, 118, diagnosticsOn);
            Add(buttons, x, y, "next-test-slot", "下一测试格", "切换测试快捷栏到下一格。", col2, 0, 118, diagnosticsOn);
            Add(buttons, x, y, "use-hotbar-item", "使用测试格物品", "通过 ItemCheck 使用测试快捷栏物品。", col3, 0, 146, diagnosticsOn);

            Add(buttons, x, y, "quick-heal", "QuickHeal 测试", "调用 Terraria 原版 QuickHeal。", col1, 1, 118, diagnosticsOn);
            Add(buttons, x, y, "quick-mana", "QuickMana 测试", "调用 Terraria 原版 QuickMana。", col2, 1, 118, diagnosticsOn);
            Add(buttons, x, y, "quick-buff-once", "QuickBuff 一次", "等价原版 B，会喝多个可用 Buff 药。", col3, 1, 128, diagnosticsOn);
            Add(buttons, x, y, "mouse-target-dry-run", "MouseTarget 测试", "只捕获、覆盖并恢复鼠标目标，不改变世界。", col4, 1, 128, diagnosticsOn);

            Add(buttons, x, y + SectionGap, "auto-heal-toggle", autoHealLabel, "切换自动回血；当前走背包恢复药选择和原版恢复物品流程。", col1, 3, 118, true);
            Add(buttons, x, y + SectionGap, "auto-mana-toggle", autoManaLabel, "切换自动回蓝；手持耗蓝武器下一次魔力不足且当前可恢复缺蓝时使用背包回蓝药。", col2, 3, 118, true);
            Add(buttons, x, y + SectionGap, "auto-buff-toggle", autoBuffLabel, "切换自动增益策略；不会立即 QuickBuff。", col3, 3, 128, true);

            Add(buttons, x, y + SectionGap * 2, "buff-refresh-candidates", "刷新候选", "扫描背包和虚空袋中的 Buff 药水候选。", col1, 5, 118, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 2, "buff-prev-candidate", "上一个候选", "选择上一个 Buff 药水候选。", col2, 5, 118, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 2, "buff-next-candidate", "下一个候选", "选择下一个 Buff 药水候选。", col3, 5, 118, diagnosticsOn);

            Add(buttons, x, y + SectionGap * 2, "buff-add-whitelist", "添加白名单", "把当前候选加入自动增益白名单。", col1, 6, 118, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 2, "buff-remove-whitelist", "移除白名单", "从白名单移除当前候选。", col2, 6, 118, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 2, "buff-clear-whitelist", "清空白名单", "清空自动增益白名单。", col3, 6, 118, diagnosticsOn);

            Add(buttons, x, y + SectionGap * 2, "buff-use-selected-once", "手动用选中药水一次", "只对当前选中的 Buff 药水做一次受控本地使用。", col1, 7, 176, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 2, "quick-buff-once-bottom", "QuickBuff 一次", "等价原版 B，会喝多个可用 Buff 药。", col3, 7, 128, diagnosticsOn);

            Add(buttons, x, y + SectionGap * 3, "toggle-diagnostic-input", toggleLabel, "切换诊断输入开关。", col1, 9, 118, true);
            Add(buttons, x, y + SectionGap * 3, "noop", "空动作", "验证动作队列可以接收请求。", col2, 9, 90, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 3, "select-test-slot", "切到测试格并恢复", "临时切到测试快捷栏，再恢复原手持格。", col3, 9, 146, diagnosticsOn);
            Add(buttons, x, y + SectionGap * 3, "use-selected-item", "使用手上物品", "通过 ItemCheck 使用当前手持物品。", col4, 9, 118, diagnosticsOn);
            return buttons;
        }

        public static DiagnosticTestButton HitTest(IReadOnlyList<DiagnosticTestButton> buttons, int mouseX, int mouseY)
        {
            if (buttons == null)
            {
                return null;
            }

            DiagnosticTestButton best = null;
            var bestDistance = double.MaxValue;
            for (var index = 0; index < buttons.Count; index++)
            {
                var button = buttons[index];
                if (button != null && button.ContainsHit(mouseX, mouseY))
                {
                    var distance = button.DistanceToCenter(mouseX, mouseY);
                    if (best == null || distance < bestDistance)
                    {
                        best = button;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        private static void Add(
            List<DiagnosticTestButton> buttons,
            int baseX,
            int baseY,
            string id,
            string label,
            string hint,
            int columnOffset,
            int row,
            int width,
            bool enabled)
        {
            buttons.Add(new DiagnosticTestButton
            {
                Id = id,
                Label = label,
                Hint = hint,
                X = baseX + columnOffset,
                Y = baseY + row * RowHeight,
                Width = width,
                Height = ButtonHeight,
                HitPaddingX = HitPaddingX,
                HitPaddingY = HitPaddingY,
                Enabled = enabled
            });
        }
    }
}
