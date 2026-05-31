using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI.Information
{
    internal static class InformationStatusPanelService
    {
        private const int PanelWidth = 420;
        private const int Padding = 6;
        private const int MinVisibleSize = 48;
        private const double DefaultFontScale = 0.72d;
        private static readonly object SyncRoot = new object();
        private static bool _adjusting;
        private static bool _dragging;
        private static bool _waitingForFreshPress;
        private static bool _lastLeftDown;
        private static int _dragOffsetX;
        private static int _dragOffsetY;

        public static bool IsAdjusting
        {
            get { lock (SyncRoot) { return _adjusting; } }
        }

        public static void RequestAdjustPosition()
        {
            lock (SyncRoot)
            {
                _adjusting = true;
                _dragging = false;
                _waitingForFreshPress = true;
                _lastLeftDown = false;
                _dragOffsetX = 0;
                _dragOffsetY = 0;
            }
        }

        internal static int DrawPanel(object spriteBatch, InformationWorldContext context, IList<InformationStatusLine> lines)
        {
            if (spriteBatch == null || context == null)
            {
                return 0;
            }

            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var adjusting = IsAdjusting;
                var hasLines = lines != null && lines.Count > 0;
                if (!adjusting && !hasLines)
                {
                    return 0;
                }

                var lineCount = hasLines ? lines.Count : 1;
                var width = CalculatePanelWidth(lines, hasLines);
                var height = CalculatePanelHeight(lines, hasLines, lineCount);
                var x = Clamp(settings.InformationPanelX, 0, Math.Max(0, context.ScreenWidth - MinVisibleSize));
                var y = Clamp(settings.InformationPanelY, 0, Math.Max(0, context.ScreenHeight - MinVisibleSize));
                if (!settings.InformationPanelPositionInitialized)
                {
                    x = 20;
                    y = Clamp((int)Math.Round(context.ScreenHeight * 0.45d), 40, Math.Max(40, context.ScreenHeight - height - 20));
                }

                if (x + width > context.ScreenWidth)
                {
                    x = Math.Max(0, context.ScreenWidth - width);
                }

                if (y + height > context.ScreenHeight)
                {
                    y = Math.Max(0, context.ScreenHeight - height);
                }

                var rect = new LegacyUiRect(x, y, width, height);
                HandleAdjustment(settings, context, rect);
                DrawAdjustmentFrame(spriteBatch, rect, adjusting);
                DrawContent(spriteBatch, rect, lines, adjusting);
                return hasLines ? lines.Count : 0;
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "information-status-panel-draw-error",
                    TimeSpan.FromSeconds(10),
                    "InformationStatusPanelService",
                    "Information status panel draw failed; exception swallowed.", error);
                return 0;
            }
        }

        private static int CalculatePanelWidth(IList<InformationStatusLine> lines, bool hasLines)
        {
            var width = PanelWidth;
            if (hasLines && lines != null)
            {
                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                    {
                        var scale = GetLineScale(line);
                        width = Math.Max(width, (int)Math.Ceiling((double)UiTextRenderer.EstimateTextWidth(line.Text, scale)) + Padding * 2 + 8);
                    }
                }
            }

            return Math.Min(640, Math.Max(180, width));
        }

        private static int CalculatePanelHeight(IList<InformationStatusLine> lines, bool hasLines, int lineCount)
        {
            if (!hasLines || lines == null)
            {
                return Padding * 2 + lineCount * GetLineHeight((float)DefaultFontScale);
            }

            var height = Padding * 2;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                height += GetLineHeight(GetLineScale(line));
            }

            return Math.Max(Padding * 2 + GetLineHeight((float)DefaultFontScale), height);
        }

        private static void DrawAdjustmentFrame(object spriteBatch, LegacyUiRect rect, bool adjusting)
        {
            if (!adjusting)
            {
                return;
            }

            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8, 2, 255, 215, 90, 235);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2, 1, 255, 255, 255, 180);
        }

        private static void DrawContent(object spriteBatch, LegacyUiRect rect, IList<InformationStatusLine> lines, bool adjusting)
        {
            var y = rect.Y + Padding;
            if (lines == null || lines.Count <= 0)
            {
                if (adjusting)
                {
                    UiTextRenderer.DrawText(spriteBatch, "信息窗", rect.X + Padding, y, 255, 244, 180, 245, (float)DefaultFontScale);
                }

                return;
            }

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var color = line.Color;
                var scale = GetLineScale(line);
                UiTextRenderer.DrawText(
                    spriteBatch,
                    UiTextRenderer.Ellipsize(line.Text, rect.Width - Padding * 2 - 4, scale),
                    rect.X + Padding,
                    y,
                    color.R,
                    color.G,
                    color.B,
                    color.A,
                    scale);
                y += GetLineHeight(scale);
            }
        }

        private static float GetLineScale(InformationStatusLine line)
        {
            var value = line == null ? DefaultFontScale : line.FontScale;
            return (float)InformationStyleHelper.NormalizeFontScale(value, DefaultFontScale);
        }

        private static int GetLineHeight(float scale)
        {
            return Math.Max(16, UiTextRenderer.EstimateTextHeight(scale) + 5);
        }

        private static void HandleAdjustment(AppSettings settings, InformationWorldContext context, LegacyUiRect rect)
        {
            var mouse = LegacyUiInput.ReadMouse();
            if (mouse == null)
            {
                return;
            }

            var save = false;
            var restoreMainWindow = false;
            lock (SyncRoot)
            {
                if (!_adjusting)
                {
                    _lastLeftDown = mouse.LeftDown;
                    return;
                }

                UiMouseCaptureService.CaptureForOperationWindow();
                var leftDown = mouse.LeftDown;
                var leftPressed = leftDown && !_lastLeftDown;
                var leftReleased = !leftDown && _lastLeftDown;

                if (_waitingForFreshPress)
                {
                    if (!leftDown)
                    {
                        _waitingForFreshPress = false;
                    }

                    _lastLeftDown = leftDown;
                    return;
                }

                if (!_dragging && leftPressed && rect.Contains(mouse.X, mouse.Y))
                {
                    _dragging = true;
                    _dragOffsetX = mouse.X - rect.X;
                    _dragOffsetY = mouse.Y - rect.Y;
                }

                if (_dragging && leftDown)
                {
                    settings.InformationPanelX = Clamp(mouse.X - _dragOffsetX, 0, Math.Max(0, context.ScreenWidth - MinVisibleSize));
                    settings.InformationPanelY = Clamp(mouse.Y - _dragOffsetY, 0, Math.Max(0, context.ScreenHeight - MinVisibleSize));
                    settings.InformationPanelPositionInitialized = true;
                }

                if (_dragging && leftReleased)
                {
                    _dragging = false;
                    _adjusting = false;
                    _waitingForFreshPress = false;
                    save = true;
                    restoreMainWindow = true;
                }

                _lastLeftDown = leftDown;
            }

            if (save)
            {
                ConfigService.SaveAll();
                RecordPositionSaved(settings);
            }

            if (restoreMainWindow)
            {
                LegacyMainUiState.SetVisible(true);
            }
        }

        private static void RecordPositionSaved(AppSettings settings)
        {
            try
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    Guid.Empty,
                    "Ui.Action.InformationPanelPositionSaved",
                    "UI",
                    string.Empty,
                    "Succeeded",
                    "Succeeded",
                    "信息窗位置已保存。",
                    0,
                    "{}",
                    "{\"informationPanelX\":" + settings.InformationPanelX + ",\"informationPanelY\":" + settings.InformationPanelY + "}",
                    "{\"submitted\":false,\"implemented\":true,\"featureId\":\"information.info_panel_position\"}",
                    "UI",
                    "InformationStatusPanel",
                    string.Empty,
                    string.Empty);
            }
            catch
            {
            }
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
