using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int FeatureToggleHotkeyIconButtonSize = 24;
        private const int FeatureToggleHotkeyIconSize = 18;
        private const int FeatureToggleHotkeyReserveWidth = 30;
        private const int FeatureToggleHotkeyModalWidth = 368;
        private const int FeatureToggleHotkeyModalHeight = 184;
        private const string FeatureToggleHotkeyIconId = "keyboard";
        private const string FeatureToggleHotkeyModalId = "feature-toggle-hotkey-modal";
        private const string FeatureToggleHotkeyOpenPrefix = "feature-toggle-hotkey-open:";
        private const string FeatureToggleHotkeyCaptureElementId = "feature-toggle-hotkey-capture:start";
        private const string FeatureToggleHotkeyClearElementId = "feature-toggle-hotkey-clear";
        private const string FeatureToggleHotkeyCloseElementId = "feature-toggle-hotkey-close";
        private const string FeatureToggleHotkeyIntroText = "只切换功能开启/关闭，不执行功能动作。";
        private const string FeatureToggleHotkeyIdleText = "单击开始录入按钮";
        private const string FeatureToggleHotkeyCapturingText = "请按下按键，Backspace 删除，Esc 取消";
        private const string FeatureToggleHotkeyCaptureTooltip = "支持左右 Ctrl / Alt / Shift、小键盘和鼠标键；Backspace 删除绑定";

        private static string _featureToggleHotkeyModalTargetId = string.Empty;
        private static LegacyUiRect _featureToggleHotkeyModalAnchor;
        private static bool _featureToggleHotkeyCaptureActive;
        private static string _featureToggleHotkeyMessage = string.Empty;

        public static void OpenFeatureToggleHotkeyModal(string targetId, LegacyUiRect anchor)
        {
            string normalizedTargetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                CloseFeatureToggleHotkeyModal();
                return;
            }

            StopOtherHotkeyCapturesForFeatureToggleModal();
            LegacyTextInput.ClearFocus();
            _featureToggleHotkeyModalTargetId = normalizedTargetId;
            _featureToggleHotkeyModalAnchor = anchor;
            StopFeatureToggleHotkeyCapture();
            _featureToggleHotkeyMessage = string.Empty;
        }

        public static void CloseFeatureToggleHotkeyModal()
        {
            _featureToggleHotkeyModalTargetId = string.Empty;
            _featureToggleHotkeyModalAnchor = new LegacyUiRect();
            StopFeatureToggleHotkeyCapture();
            _featureToggleHotkeyMessage = string.Empty;
        }

        public static void StartFeatureToggleHotkeyCapture()
        {
            if (string.IsNullOrWhiteSpace(_featureToggleHotkeyModalTargetId))
            {
                return;
            }

            StopOtherHotkeyCapturesForFeatureToggleModal();
            LegacyTextInput.ClearFocus();
            _featureToggleHotkeyCaptureActive = true;
            _featureToggleHotkeyMessage = string.Empty;
            HotkeyCaptureService.Seed(FeatureToggleHotkeyCaptureSession, IsKeyDown);
        }

        public static string GetFeatureToggleHotkeyModalTargetId()
        {
            return _featureToggleHotkeyModalTargetId;
        }

        public static bool IsFeatureToggleHotkeyCaptureActive()
        {
            return _featureToggleHotkeyCaptureActive;
        }

        public static bool IsAnyHotkeyCaptureActive()
        {
            return _featureToggleHotkeyCaptureActive ||
                   _quickItemHotkeyCaptureActive ||
                   _autoMiningHotkeyCaptureActive ||
                   _blueprintEntryHotkeyCaptureActive ||
                   !string.IsNullOrWhiteSpace(_mapQuickAnnouncementHotkeyCaptureSlot);
        }

        public static bool ClearFeatureToggleHotkeyBinding()
        {
            string targetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(_featureToggleHotkeyModalTargetId, out targetId))
            {
                return false;
            }

            bool changed;
            UnifiedHotkeyBindingUpdateResult result;
            ConfigService.TrySaveUnifiedHotkeyBinding(UnifiedHotkeyBindingIds.ForFeatureToggleTarget(targetId), string.Empty, out result);
            changed = result != null && result.Changed;
            StopFeatureToggleHotkeyCapture();
            _featureToggleHotkeyMessage = changed ? "已清除绑定" : "当前未绑定";

            return changed;
        }

        private static void StopOtherHotkeyCapturesForFeatureToggleModal()
        {
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            StopBlueprintEntryHotkeyCapture();
            StopMapQuickAnnouncementHotkeyCapture();
        }

        private static void StopFeatureToggleHotkeyCapture()
        {
            _featureToggleHotkeyCaptureActive = false;
            FeatureToggleHotkeyCaptureSession.Clear();
        }

        private static int GetFeatureToggleHotkeyReserveWidth(string targetId)
        {
            string normalizedTargetId;
            return FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId)
                ? FeatureToggleHotkeyReserveWidth
                : 0;
        }

        private static int GetFeatureToggleModeGroupRight(LegacyUiRect row, string targetId)
        {
            return row.Right - 10 - GetFeatureToggleHotkeyReserveWidth(targetId);
        }

        private static LegacyUiRect CalculateFeatureToggleHotkeyButtonRect(LegacyUiRect row, string targetId)
        {
            if (GetFeatureToggleHotkeyReserveWidth(targetId) <= 0)
            {
                return new LegacyUiRect();
            }

            return new LegacyUiRect(
                row.Right - 10 - FeatureToggleHotkeyIconButtonSize,
                RowModeButtonY(row),
                FeatureToggleHotkeyIconButtonSize,
                FeatureToggleHotkeyIconButtonSize);
        }

        private static LegacyUiElement DrawFeatureToggleHotkeyButton(LegacyUiContext context, LegacyUiRect row, string targetId)
        {
            if (context == null)
            {
                return null;
            }

            string normalizedTargetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                return null;
            }

            var rect = CalculateFeatureToggleHotkeyButtonRect(row, normalizedTargetId);
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return null;
            }

            var clip = context.HasClip ? context.ClipRect : row;
            var elementId = FeatureToggleHotkeyOpenPrefix + normalizedTargetId;
            var hovered = context.IsElementHovered(elementId, rect);
            var selected = string.Equals(_featureToggleHotkeyModalTargetId, normalizedTargetId, StringComparison.Ordinal);
            LegacyUiTheme.DrawButtonClipped(context.SpriteBatch, rect, hovered, hovered && context.Mouse != null && context.Mouse.LeftDown, selected, true, clip);
            var iconRect = new LegacyUiRect(
                rect.X + Math.Max(0, (rect.Width - FeatureToggleHotkeyIconSize) / 2),
                rect.Y + Math.Max(0, (rect.Height - FeatureToggleHotkeyIconSize) / 2),
                FeatureToggleHotkeyIconSize,
                FeatureToggleHotkeyIconSize);
            LegacyVectorIconRenderer.Draw(context.SpriteBatch, FeatureToggleHotkeyIconId, iconRect, clip, selected, true);

            var element = context.RegisterElement(
                elementId,
                FeatureToggleHotkeyTargetCatalog.GetDisplayName(normalizedTargetId) + ":功能主开关快捷键",
                "button",
                rect,
                true,
                selected,
                0,
                0,
                0,
                BuildFeatureToggleHotkeyIconTooltip(normalizedTargetId));
            return hovered ? element : null;
        }

        private static string[] BuildFeatureToggleHotkeyIconTooltip(string targetId)
        {
            return new[]
            {
                "双击打开"
            };
        }

        private static bool RegisterFeatureToggleHotkeyModalOverlay(LegacyScrollArea area, LegacyUiRect contentRect, AppSettings settings)
        {
            string targetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(_featureToggleHotkeyModalTargetId, out targetId) ||
                area == null)
            {
                return false;
            }

            var popup = CalculateFeatureToggleHotkeyModalRect(
                contentRect.Width > 0 && contentRect.Height > 0 ? contentRect : area.Viewport,
                _featureToggleHotkeyModalAnchor);
            return LegacyUiOverlayCoordinator.Current.Register(new LegacyUiOverlayRequest
            {
                Id = FeatureToggleHotkeyModalId,
                OwnerPageId = LegacyMainUiState.SelectedPageId,
                Bounds = popup,
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 50,
                CacheSignature = BuildFeatureToggleHotkeyModalCacheSignature(settings, targetId, popup),
                Draw = DrawFeatureToggleHotkeyModalOverlay
            });
        }

        private static void DrawFeatureToggleHotkeyModalOverlay(LegacyUiContext context, LegacyUiOverlayRequest request)
        {
            if (context == null || request == null)
            {
                return;
            }

            UpdateFeatureToggleHotkeyCapture();
            string targetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(_featureToggleHotkeyModalTargetId, out targetId))
            {
                return;
            }

            DrawFeatureToggleHotkeyModal(context, request.Bounds, targetId);
        }

        private static void DrawFeatureToggleHotkeyModal(LegacyUiContext context, LegacyUiRect popup, string targetId)
        {
            var current = GetUnifiedHotkeyDisplay(UnifiedHotkeyBindingIds.ForFeatureToggleTarget(targetId));
            var displayName = FeatureToggleHotkeyTargetCatalog.GetDisplayName(targetId);
            var captureText = _featureToggleHotkeyCaptureActive ? FeatureToggleHotkeyCapturingText : FeatureToggleHotkeyIdleText;

            UiPrimitiveRenderer.DrawRoundedRect(context.SpriteBatch, popup.X, popup.Y, popup.Width, popup.Height, LegacyUiTheme.Radius, 48, 58, 88, 238);
            UiPrimitiveRenderer.DrawRoundedRect(context.SpriteBatch, popup.X + 1, popup.Y + 1, popup.Width - 2, popup.Height - 2, LegacyUiTheme.Radius - 1, 18, 23, 38, 238);
            UiPrimitiveRenderer.DrawFilledRect(context.SpriteBatch, popup.X + 8, popup.Y + 34, popup.Width - 16, 1, 116, 136, 176, 145);
            context.RegisterElement(FeatureToggleHotkeyModalId, displayName + " 快捷键小窗", "blocker", popup, true, false, 0, 0, 0, null);

            UiTextRenderer.DrawText(context.SpriteBatch, displayName + " 快捷键", popup.X + 14, popup.Y + 11, 246, 242, 220, 255, 0.82f);
            DrawFeatureToggleHotkeyModalButton(context, new LegacyUiRect(popup.Right - 54, popup.Y + 8, 40, 20), FeatureToggleHotkeyCloseElementId, "关闭", false, null);

            UiTextRenderer.DrawText(context.SpriteBatch, FeatureToggleHotkeyIntroText, popup.X + 18, popup.Y + 48, 238, 238, 226, 245, 0.72f);
            UiTextRenderer.DrawText(context.SpriteBatch, "当前：" + (string.IsNullOrWhiteSpace(current) ? "未绑定" : current), popup.X + 18, popup.Y + 75, 206, 218, 238, 240, 0.68f);

            var captureRect = new LegacyUiRect(popup.X + 18, popup.Y + 102, popup.Width - 36, 30);
            DrawFeatureToggleHotkeyModalButton(context, captureRect, FeatureToggleHotkeyCaptureElementId, captureText, _featureToggleHotkeyCaptureActive, FeatureToggleHotkeyCaptureTooltip);

            DrawFeatureToggleHotkeyModalButton(context, new LegacyUiRect(popup.X + 18, popup.Y + 144, 78, 24), FeatureToggleHotkeyClearElementId, "清除", false, null);
            if (!string.IsNullOrWhiteSpace(_featureToggleHotkeyMessage))
            {
                UiTextRenderer.DrawText(context.SpriteBatch, _featureToggleHotkeyMessage, popup.X + 110, popup.Y + 148, 238, 196, 180, 245, 0.64f);
            }
        }

        private static LegacyUiElement DrawFeatureToggleHotkeyModalButton(LegacyUiContext context, LegacyUiRect rect, string id, string text, bool selected, string tooltip)
        {
            var element = new LegacyButtonControl
            {
                Id = id,
                Label = text,
                Text = text,
                ElementLabel = text,
                Kind = "button",
                Bounds = rect,
                Selected = selected,
                TextScale = rect.Width <= 80 ? 0.66f : 0.72f,
                TooltipLines = string.IsNullOrWhiteSpace(tooltip) ? null : new[] { tooltip }
            }.Draw(context);
            return element != null && context.IsElementHovered(id, rect) ? element : null;
        }

        private static LegacyUiRect CalculateFeatureToggleHotkeyModalRect(LegacyUiRect viewport, LegacyUiRect anchor)
        {
            var width = Math.Min(FeatureToggleHotkeyModalWidth, Math.Max(300, viewport.Width - 12));
            var height = Math.Min(FeatureToggleHotkeyModalHeight, Math.Max(150, viewport.Height - 12));
            var hasAnchor = anchor.Width > 0 && anchor.Height > 0;
            var x = hasAnchor ? anchor.X - width + anchor.Width : viewport.X + Math.Max(0, (viewport.Width - width) / 2);
            var y = hasAnchor ? anchor.Bottom + 8 : viewport.Y + Math.Max(0, (viewport.Height - height) / 2);
            if (y + height > viewport.Bottom - 6 && hasAnchor)
            {
                y = anchor.Y - height - 8;
            }

            x = ClampInt(x, viewport.X + 6, Math.Max(viewport.X + 6, viewport.Right - width - 6));
            y = ClampInt(y, viewport.Y + 6, Math.Max(viewport.Y + 6, viewport.Bottom - height - 6));
            return new LegacyUiRect(x, y, width, height);
        }

        private static int BuildFeatureToggleHotkeyModalCacheSignature(AppSettings appSettings, string targetId, LegacyUiRect popup)
        {
            unchecked
            {
                var hotkeySettings = ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
                var unifiedHotkeys = ConfigService.UnifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault();
                var hash = 17;
                AddHash(ref hash, targetId);
                AddHash(ref hash, GetUnifiedHotkeyDisplay(UnifiedHotkeyBindingIds.ForFeatureToggleTarget(targetId)));
                AddHash(ref hash, _featureToggleHotkeyCaptureActive);
                AddHash(ref hash, _featureToggleHotkeyMessage);
                AddHash(ref hash, appSettings == null ? 0 : appSettings.ConfigVersion);
                AddHash(ref hash, hotkeySettings.ConfigVersion);
                AddHash(ref hash, unifiedHotkeys.CreateCacheSignature());
                AddHash(ref hash, popup.X);
                AddHash(ref hash, popup.Y);
                AddHash(ref hash, popup.Width);
                AddHash(ref hash, popup.Height);
                return hash;
            }
        }

        private static void UpdateFeatureToggleHotkeyCapture()
        {
            if (!_featureToggleHotkeyCaptureActive || string.IsNullOrWhiteSpace(_featureToggleHotkeyModalTargetId) || !IsCurrentProcessForeground())
            {
                return;
            }

            var capture = HotkeyCaptureService.Update(FeatureToggleHotkeyCaptureSession, IsKeyDown);
            if (capture == null || !capture.HasResult)
            {
                return;
            }

            string message;
            bool changed;
            if (TryApplyUnifiedHotkeyCaptureResult(
                    UnifiedHotkeyBindingIds.ForFeatureToggleTarget(_featureToggleHotkeyModalTargetId),
                    capture,
                    out message,
                    out changed) &&
                capture.Kind == HotkeyCaptureResultKind.Captured)
            {
                CloseFeatureToggleHotkeyModal();
                return;
            }

            _featureToggleHotkeyMessage = message;
            StopFeatureToggleHotkeyCapture();
        }

        private static bool TrySaveFeatureToggleHotkey(HotkeySettings hotkeySettings, AppSettings appSettings, string targetId, string chordText, out string message, out bool changed)
        {
            changed = false;
            message = string.Empty;
            string normalizedTargetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                message = "未知功能开关";
                return false;
            }

            FeatureToggleHotkeyChord chord;
            if (!FeatureToggleHotkeyChord.TryParse(chordText, out chord))
            {
                message = "不支持这个组合";
                return false;
            }

            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.ToggleHotkeysByTargetId == null)
            {
                hotkeySettings.ToggleHotkeysByTargetId = new Dictionary<string, string>();
            }

            FeatureToggleHotkeyConflict conflict;
            if (FeatureToggleHotkeyConflictRegistry.TryFindConflict(hotkeySettings, appSettings ?? AppSettings.CreateDefault(), normalizedTargetId, chord, out conflict))
            {
                message = BuildFeatureToggleHotkeyConflictMessage(conflict);
                return false;
            }

            var old = GetFeatureToggleHotkeyBinding(hotkeySettings, normalizedTargetId);
            changed = !string.Equals(old, chord.Normalized, StringComparison.Ordinal);
            hotkeySettings.ToggleHotkeysByTargetId[normalizedTargetId] = chord.Normalized;
            message = changed ? "已保存 " + chord.Display : "未变化";
            return true;
        }

        private static bool ClearFeatureToggleHotkeyBinding(HotkeySettings hotkeySettings, string targetId, out bool changed)
        {
            changed = false;
            string normalizedTargetId;
            if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                return false;
            }

            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();
            if (hotkeySettings.ToggleHotkeysByTargetId == null)
            {
                hotkeySettings.ToggleHotkeysByTargetId = new Dictionary<string, string>();
            }

            changed = hotkeySettings.ToggleHotkeysByTargetId.Remove(normalizedTargetId);
            return true;
        }

        private static string BuildFeatureToggleHotkeyConflictMessage(FeatureToggleHotkeyConflict conflict)
        {
            if (conflict == null)
            {
                return "快捷键冲突";
            }

            var owner = string.IsNullOrWhiteSpace(conflict.OwnerDisplayName) ? "其它快捷键" : conflict.OwnerDisplayName;
            switch (conflict.ConflictType)
            {
                case FeatureToggleHotkeyConflictType.FeatureToggle:
                    return "已被 " + owner + " 使用";
                case FeatureToggleHotkeyConflictType.AutoMiningTrigger:
                    return "与 " + owner + "冲突";
                case FeatureToggleHotkeyConflictType.QuickAnnouncement:
                    return "与 " + owner + " 完整组合冲突";
                default:
                    return "与 " + owner + "冲突";
            }
        }

        private static string GetFeatureToggleHotkeyBinding(HotkeySettings settings, string targetId)
        {
            string normalizedTargetId;
            if (settings == null ||
                settings.ToggleHotkeysByTargetId == null ||
                !FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId))
            {
                return string.Empty;
            }

            string value;
            string normalized;
            return settings.ToggleHotkeysByTargetId.TryGetValue(normalizedTargetId, out value) &&
                   FeatureToggleHotkeyChord.TryNormalize(value, out normalized)
                ? normalized
                : string.Empty;
        }

        private static HotkeySettings EnsureHotkeySettings()
        {
            return ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
        }

        internal static string GetFeatureToggleHotkeyOpenElementIdForTesting(string targetId)
        {
            string normalizedTargetId;
            return FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(targetId, out normalizedTargetId)
                ? FeatureToggleHotkeyOpenPrefix + normalizedTargetId
                : string.Empty;
        }

        internal static int GetFeatureToggleHotkeyReserveWidthForTesting()
        {
            return FeatureToggleHotkeyReserveWidth;
        }

        internal static string GetFeatureToggleHotkeyIconIdForTesting()
        {
            return FeatureToggleHotkeyIconId;
        }

        internal static string[] GetFeatureToggleHotkeyModalCopyForTesting()
        {
            return new[] { FeatureToggleHotkeyIntroText, FeatureToggleHotkeyIdleText, FeatureToggleHotkeyCapturingText };
        }

        internal static string[] GetFeatureToggleHotkeyIconTooltipForTesting(string targetId)
        {
            return BuildFeatureToggleHotkeyIconTooltip(targetId);
        }

        internal static string[] GetFeatureToggleHotkeyModalTooltipCopyForTesting()
        {
            return new string[] { null, FeatureToggleHotkeyCaptureTooltip, null };
        }

        internal static LegacyUiRect CalculateFeatureToggleHotkeyButtonRectForTesting(LegacyUiRect row, string targetId)
        {
            return CalculateFeatureToggleHotkeyButtonRect(row, targetId);
        }

        internal static int GetFeatureToggleModeGroupRightForTesting(LegacyUiRect row, string targetId)
        {
            return GetFeatureToggleModeGroupRight(row, targetId);
        }

        internal static bool TryApplyFeatureToggleHotkeyCapturedChordForTesting(HotkeySettings hotkeySettings, AppSettings appSettings, string targetId, string chordText, out string message, out bool changed)
        {
            return TrySaveFeatureToggleHotkey(hotkeySettings, appSettings, targetId, chordText, out message, out changed);
        }

        internal static bool TryClearFeatureToggleHotkeyBindingForTesting(HotkeySettings hotkeySettings, string targetId, out bool changed)
        {
            return ClearFeatureToggleHotkeyBinding(hotkeySettings, targetId, out changed);
        }

        internal static void OpenFeatureToggleHotkeyModalForTesting(string targetId, LegacyUiRect anchor)
        {
            OpenFeatureToggleHotkeyModal(targetId, anchor);
        }

        internal static string GetFeatureToggleHotkeyModalTargetForTesting()
        {
            return GetFeatureToggleHotkeyModalTargetId();
        }

        internal static void StartFeatureToggleHotkeyCaptureForTesting()
        {
            StartFeatureToggleHotkeyCapture();
        }

        internal static bool IsFeatureToggleHotkeyCaptureActiveForTesting()
        {
            return IsFeatureToggleHotkeyCaptureActive();
        }

        internal static bool IsQuickItemHotkeyCaptureActiveForTesting()
        {
            return _quickItemHotkeyCaptureActive;
        }

        internal static bool IsAutoMiningHotkeyCaptureActiveForTesting()
        {
            return _autoMiningHotkeyCaptureActive;
        }

        internal static bool IsBlueprintEntryHotkeyCaptureActiveForTesting()
        {
            return _blueprintEntryHotkeyCaptureActive;
        }

        internal static string GetBlueprintHotkeyCaptureTargetIdForTesting()
        {
            return _blueprintHotkeyCaptureTargetId;
        }

        internal static string BuildFeatureToggleHotkeyConflictMessageForTesting(FeatureToggleHotkeyConflict conflict)
        {
            return BuildFeatureToggleHotkeyConflictMessage(conflict);
        }
    }
}
