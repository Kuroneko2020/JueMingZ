using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class DiagnosticsOverlay
    {
        private static readonly object SyncRoot = new object();
        private static bool _visible;
        private static DiagnosticsOverlayModel _model = new DiagnosticsOverlayModel();
        private static long _drawCallCount;
        private static DateTime? _lastDrawUtc;

        public static bool Visible
        {
            get
            {
                lock (SyncRoot)
                {
                    return _visible;
                }
            }
        }

        public static long DrawCallCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _drawCallCount;
                }
            }
        }

        public static DateTime? LastDrawUtc
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastDrawUtc;
                }
            }
        }

        public static bool ToggleVisible()
        {
            lock (SyncRoot)
            {
                _visible = !_visible;
                return _visible;
            }
        }

        public static void UpdateFromSnapshot(DiagnosticSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                _model = DiagnosticsOverlayModel.FromSnapshot(snapshot);
            }
        }

        public static DiagnosticsOverlayModel GetCurrentModel()
        {
            lock (SyncRoot)
            {
                return _model;
            }
        }

        public static bool DrawInterfaceLayer()
        {
            // Diagnostic overlay draws inside Terraria's active interface SpriteBatch;
            // button hits are queued for the runtime action phase.
            DiagnosticsOverlayModel model;
            bool visible;
            long drawCallCount;

            try
            {
                lock (SyncRoot)
                {
                    _drawCallCount++;
                    _lastDrawUtc = DateTime.UtcNow;
                    drawCallCount = _drawCallCount;
                    model = _model;
                    visible = _visible;
                }

                if (drawCallCount == 1)
                {
                    Logger.Info("DiagnosticsOverlay", "Diagnostics overlay draw first call.");
                }

                if (!visible)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("DiagnosticsOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawTextBlock(model, visible, drawCallCount, spriteBatch);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("DiagnosticsOverlay", error);
                LogThrottle.ErrorThrottled(
                    "diagnostics-overlay-draw-error",
                    TimeSpan.FromSeconds(10),
                    "DiagnosticsOverlay",
                    "Diagnostics overlay draw failed.", error);
            }

            return true;
        }

        private static void DrawTextBlock(DiagnosticsOverlayModel model, bool visible, long drawCallCount, object spriteBatch)
        {
            if (spriteBatch == null)
            {
                return;
            }

            var buttons = DiagnosticTestButtonPanel.BuildButtons(model);
            var interaction = DiagnosticUiInteractionBridge.UpdateFromDraw(buttons);
            var lines = new[]
            {
                "决明-Z 测试面板 " + (string.IsNullOrWhiteSpace(model.Version) ? "unknown" : model.Version),
                "诊断输入：" + (model.EnableDiagnosticInputTests ? "开启" : "关闭") + GetDiagnosticInputGateLine(model),
                "测试快捷栏：第 " + model.DiagnosticInputTestSlotDisplay + " 格（内部 slot=" + model.DiagnosticInputTestSlot + "）",
                "测试物品：" + GetTestItemLine(model),
                "测试建议：" + model.DiagnosticTestSlotSuitability,
                "提示：" + Shorten(model.DiagnosticTestSlotHint, 90),
                "",
                "最近来源：" + GetSourceLine(model),
                "最近点击：" + GetLastClickedButtonLine(model),
                "最近动作：" + GetActionLabel(model),
                "结果：" + Shorten(model.LastActionResultCode ?? "none", 48),
                "说明：" + Shorten(model.LastActionUserMessage ?? "暂无动作结果。", 100),
                "自动恢复：" + GetAutoRecoveryStatusLine(model),
                "自动恢复最近：" + Shorten(GetAutoRecoveryRecentLine(model), 110),
                "",
                "按钮区："
            };

            for (var index = 0; index < lines.Length; index++)
            {
                DrawLine(spriteBatch, lines[index], 20f, 56f + index * 20f);
            }

            DrawOperationWindowFrame(spriteBatch);
            DrawButtons(model, spriteBatch, buttons, interaction);

            var tailY = DiagnosticTestButtonPanel.StartY + DiagnosticTestButtonPanel.RowHeight * DiagnosticTestButtonPanel.RowCount + 18f;
            DrawLine(spriteBatch, "Hook：" + GetHookLabel(model), 20f, tailY);
            DrawLine(spriteBatch, "日志：" + Shorten(GetFileName(model.ActionEventsPath), 80), 20f, tailY + 18f);
            DrawLine(spriteBatch, "Terraria 鼠标：" + model.TerrariaMouseX + "," + model.TerrariaMouseY + " 左键=" + BoolText(model.TerrariaLeftDown), 20f, tailY + 36f);
            DrawLine(spriteBatch, "OS 客户区鼠标：" + model.OsClientMouseX + "," + model.OsClientMouseY + " 左键=" + BoolText(model.OsLeftDown), 20f, tailY + 54f);
            DrawLine(spriteBatch, "UI scale：" + model.UiScale.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "  MouseReadMode：" + model.MouseReadMode + "  HitTestMode：" + model.HitTestMode + " @ " + model.HitTestX + "," + model.HitTestY, 20f, tailY + 72f);
            DrawLine(spriteBatch, "悬停按钮：" + GetHoveredButtonLine(model, interaction) + "  最近点击：" + GetLastClickedButtonLine(model) + "  点击来源：" + model.ClickSource, 20f, tailY + 90f);
            DrawLine(spriteBatch, "HitTestConflict=" + BoolText(model.HitTestConflict) + "  CandidateHits：" + Shorten(model.HitTestCandidateSummary, 95), 20f, tailY + 108f);
            DrawLine(spriteBatch, "UI 鼠标占用：" + GetMouseCaptureLine(model), 20f, tailY + 126f);
            DrawLine(spriteBatch, "PrimitiveRenderer：" + GetPrimitiveRendererLine(model), 20f, tailY + 144f);
            DrawLine(spriteBatch, "提示：" + Shorten(GetHoverHintLine(model, interaction), 105), 20f, tailY + 162f);
            DrawLine(spriteBatch, "点按钮即可测试；点完后上传 logs、runtime-snapshot.json、action-events.jsonl，我方可从日志判断结果。", 20f, tailY + 180f);
        }

        private static void DrawOperationWindowFrame(object spriteBatch)
        {
            OperationWindowState.EnsureLoaded();
            var x = OperationWindowState.X;
            var y = OperationWindowState.Y;
            var width = OperationWindowState.Width;
            var height = OperationWindowState.Height;

            if (UiPrimitiveRenderer.EnsureReady(spriteBatch))
            {
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x, y, width, height, 18, 22, 28, 210);
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x, y, width, OperationWindowState.TitleHeight, 38, 48, 62, 230);
                UiPrimitiveRenderer.DrawRectBorder(spriteBatch, x, y, width, height, 2, 166, 184, 205, 230);
                UiPrimitiveRenderer.DrawFilledRect(
                    spriteBatch,
                    x + width - OperationWindowState.ResizeGripSize,
                    y + height - OperationWindowState.ResizeGripSize,
                    OperationWindowState.ResizeGripSize,
                    OperationWindowState.ResizeGripSize,
                    82,
                    96,
                    112,
                    220);
            }

            DrawLine(spriteBatch, "决明-Z 操作窗口", x + 12f, y + 7f);
            DrawLine(spriteBatch, "resize", x + width - OperationWindowState.ResizeGripSize + 2f, y + height - 17f);
        }

        private static void DrawButtons(DiagnosticsOverlayModel model, object spriteBatch, System.Collections.Generic.IReadOnlyList<DiagnosticTestButton> buttons, DiagnosticButtonHitTestResult interaction)
        {
            var primitivesReady = UiPrimitiveRenderer.EnsureReady(spriteBatch);
            for (var index = 0; index < buttons.Count; index++)
            {
                var button = buttons[index];
                var hovered = interaction != null && interaction.Button != null && button.Id == interaction.Button.Id;
                var recentlyClicked = button.Id == model.LastDiagnosticButtonId;
                var label = hovered ? "▶ " + button.Label : button.Label;
                if (!button.Enabled && button.Id != "toggle-diagnostic-input")
                {
                    label = button.Label + "：需先开启诊断";
                }

                if (primitivesReady)
                {
                    DrawButtonBackground(spriteBatch, button, hovered, recentlyClicked);
                    DrawLine(spriteBatch, label, button.X + 8f, button.Y + 7f);
                }
                else
                {
                    DrawLine(spriteBatch, "可点击文字：" + label, button.X, button.Y);
                }
            }

            if (!primitivesReady)
            {
                DrawLine(spriteBatch, "按钮背景绘制失败：当前仅显示可点击文字，请上传日志。", 20f, DiagnosticTestButtonPanel.StartY - 24f);
            }
        }

        private static void DrawButtonBackground(object spriteBatch, DiagnosticTestButton button, bool hovered, bool recentlyClicked)
        {
            if (button == null)
            {
                return;
            }

            if (!button.Enabled && button.Id != "toggle-diagnostic-input")
            {
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, button.X, button.Y, button.Width, button.Height, 28, 30, 34, 190);
                UiPrimitiveRenderer.DrawRectBorder(spriteBatch, button.X, button.Y, button.Width, button.Height, 1, 90, 94, 102, 220);
                return;
            }

            if (hovered)
            {
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, button.X, button.Y, button.Width, button.Height, 62, 88, 118, 220);
                UiPrimitiveRenderer.DrawRectBorder(spriteBatch, button.X, button.Y, button.Width, button.Height, 2, 230, 242, 255, 245);
                return;
            }

            if (recentlyClicked)
            {
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, button.X, button.Y, button.Width, button.Height, 48, 74, 72, 215);
                UiPrimitiveRenderer.DrawRectBorder(spriteBatch, button.X, button.Y, button.Width, button.Height, 1, 190, 232, 214, 230);
                return;
            }

            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, button.X, button.Y, button.Width, button.Height, 34, 38, 44, 205);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, button.X, button.Y, button.Width, button.Height, 1, 172, 180, 190, 225);
        }

        private static void DrawLine(object spriteBatch, string text, float x, float y)
        {
            UiTextRenderer.DrawText(spriteBatch, text, x, y, 255, 255, 255, 255, 1f);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        private static string GetTestItemLine(DiagnosticsOverlayModel model)
        {
            if (model == null || model.DiagnosticTestSlotItemType <= 0 || model.DiagnosticTestSlotItemStack <= 0)
            {
                return "空";
            }

            var name = string.IsNullOrWhiteSpace(model.DiagnosticTestSlotItemName)
                ? "未命名物品"
                : model.DiagnosticTestSlotItemName;
            return name + " x" + model.DiagnosticTestSlotItemStack;
        }

        private static string GetDiagnosticInputGateLine(DiagnosticsOverlayModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.DiagnosticInputGateStatus))
            {
                return string.Empty;
            }

            if (string.Equals(model.DiagnosticInputGateStatus, "skipped", StringComparison.Ordinal))
            {
                return "，输入门禁：跳过(" + Shorten(model.DiagnosticInputSkipReason, 48) + ")";
            }

            if (string.Equals(model.DiagnosticInputGateStatus, "available", StringComparison.Ordinal))
            {
                return "，输入门禁：可用";
            }

            return string.Empty;
        }

        private static string GetActionLabel(DiagnosticsOverlayModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.LastActionKind) || model.LastActionKind == "none")
            {
                return "暂无";
            }

            if (model.LastActionKind == "ItemUse")
            {
                return "UseSelectedItem";
            }

            if (model.LastActionKind == "MouseTargetDryRun")
            {
                return "MouseTarget dry-run";
            }

            return model.LastActionKind;
        }

        private static string GetSourceLine(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "暂无";
            }

            if (string.Equals(model.LastDiagnosticSourceKind, "Button", StringComparison.OrdinalIgnoreCase))
            {
                return "按钮 " + (string.IsNullOrWhiteSpace(model.LastDiagnosticButtonId) ? "unknown" : model.LastDiagnosticButtonId);
            }

            if (!string.IsNullOrWhiteSpace(model.LastDiagnosticHotkey) && model.LastDiagnosticHotkey != "none")
            {
                return "热键 " + model.LastDiagnosticHotkey;
            }

            return "暂无";
        }

        private static string GetLastClickedButtonLine(DiagnosticsOverlayModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.LastDiagnosticButtonLabel) || model.LastDiagnosticButtonLabel == "none")
            {
                return "暂无";
            }

            return model.LastDiagnosticButtonLabel;
        }

        private static string GetHoveredButtonLine(DiagnosticsOverlayModel model, DiagnosticButtonHitTestResult interaction)
        {
            if (interaction != null && interaction.Button != null)
            {
                return interaction.Button.Label + (interaction.Button.Enabled ? "" : "（不可用）");
            }

            if (model == null || string.IsNullOrWhiteSpace(model.HoveredButtonLabel) || model.HoveredButtonLabel == "none")
            {
                return "无";
            }

            return model.HoveredButtonLabel + (model.HoveredButtonEnabled ? "" : "（不可用）");
        }

        private static string GetHoverHintLine(DiagnosticsOverlayModel model, DiagnosticButtonHitTestResult interaction)
        {
            if (model == null)
            {
                return "点按钮即可测试。";
            }

            if (!model.UiPrimitiveRendererReady)
            {
                return "按钮背景绘制失败：" + model.UiPrimitiveRendererLastMessage;
            }

            if (!model.UiMouseReadAvailable)
            {
                return "鼠标状态读取失败：" + model.UiMouseReadLastMessage;
            }

            if (interaction != null && interaction.Button != null && !string.IsNullOrWhiteSpace(interaction.Button.Hint))
            {
                return interaction.Button.Hint;
            }

            if (!string.IsNullOrWhiteSpace(model.HoveredButtonHint) && model.HoveredButtonHint != "none")
            {
                return model.HoveredButtonHint;
            }

            return "移动鼠标到按钮上可查看提示。";
        }

        private static string GetMouseCaptureLine(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "未知";
            }

            if (model.UiMouseCaptureAvailable)
            {
                return "OK " + model.UiClickSuppressionMode;
            }

            var message = string.IsNullOrWhiteSpace(model.UiMouseCaptureLastMessage)
                ? "不可用，点击按钮时可能同时触发一次游戏操作。"
                : "不可用，点击按钮时可能同时触发一次游戏操作。 " + model.UiMouseCaptureLastMessage;
            return Shorten(message, 95);
        }

        private static string GetPrimitiveRendererLine(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "未知";
            }

            if (model.UiPrimitiveRendererReady)
            {
                return "OK";
            }

            var message = string.IsNullOrWhiteSpace(model.UiPrimitiveRendererLastMessage)
                ? "不可用，当前为可点击文字 fallback，请上传日志。"
                : "不可用，当前为可点击文字 fallback，请上传日志。 " + model.UiPrimitiveRendererLastMessage;
            return Shorten(message, 95);
        }

        private static string GetHookLabel(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "Update=Unknown，UI=Unknown，ItemCheck=Unknown";
            }

            return "Update=" + OkLabel(model.UpdateHookInstalled) +
                   "，UI=" + OkLabel(model.InterfaceLayerHookInstalled) +
                   "，ItemCheck=" + OkLabel(model.ItemCheckHookInstalled);
        }

        private static string GetAutoRecoveryStatusLine(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "Heal OFF / Mana OFF / Buff OFF";
            }

            var lifePercent = PercentText(model.PlayerLife, model.PlayerLifeMax);
            var manaPercent = PercentText(model.PlayerMana, model.PlayerManaMax);
            return "Heal " + OnOff(model.AutoHealEnabled) + " 当前 " + lifePercent +
                   "；Mana " + OnOff(model.AutoManaEnabled) + " 当前 " + manaPercent +
                   "；Nurse " + OnOff(model.AutoNurseEnabled) +
                   "；Furniture " + OnOff(model.AutoStationBuffEnabled) +
                   "；Buff " + OnOff(model.AutoBuffEnabled);
        }

        private static string GetAutoRecoveryRecentLine(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "none";
            }

            return "H=" + Shorten(model.LastAutoHealResult, 32) +
                   " | M=" + Shorten(model.LastAutoManaResult, 32) +
                   " | N=" + Shorten(model.LastAutoNurseResult, 24) +
                   " | F=" + Shorten(model.LastAutoStationBuffResult, 24) +
                   " | B=" + Shorten(model.LastAutoBuffResult, 32);
        }

        private static string OnOff(bool value)
        {
            return value ? "ON" : "OFF";
        }

        private static string PercentText(int value, int max)
        {
            if (max <= 0)
            {
                return "0%";
            }

            var percent = (int)System.Math.Floor((value * 100.0d) / max);
            return percent + "%";
        }

        private static string OkLabel(bool ok)
        {
            return ok ? "OK" : "Missing";
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }

        private static string GetGameStateLabel(DiagnosticsOverlayModel model)
        {
            if (model == null)
            {
                return "Unknown";
            }

            if (model.IsInWorld)
            {
                return "InWorld";
            }

            if (model.IsInMainMenu)
            {
                return "MainMenu";
            }

            return "Unknown";
        }

        private static string GetFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "none";
            }

            var lastSlash = path.LastIndexOf('\\');
            var lastForwardSlash = path.LastIndexOf('/');
            var index = Math.Max(lastSlash, lastForwardSlash);
            return index >= 0 && index + 1 < path.Length ? path.Substring(index + 1) : path;
        }
    }
}
