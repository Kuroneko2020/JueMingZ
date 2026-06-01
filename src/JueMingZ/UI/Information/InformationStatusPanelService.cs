using System;
using System.Collections.Generic;
using System.Threading;
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
        private static CachedStatusPanelLayout _layoutCache;
        private static int _layoutRebuildCount;
        private static long _layoutCacheHitCount;
        private static long _layoutCacheMissCount;

        internal static long LayoutCacheHitCount
        {
            get { return Interlocked.Read(ref _layoutCacheHitCount); }
        }

        internal static long LayoutCacheMissCount
        {
            get { return Interlocked.Read(ref _layoutCacheMissCount); }
        }

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

        internal static void ResetLayoutCacheForTesting()
        {
            _layoutCache = null;
            _layoutRebuildCount = 0;
            Interlocked.Exchange(ref _layoutCacheHitCount, 0);
            Interlocked.Exchange(ref _layoutCacheMissCount, 0);
        }

        internal static InformationStatusPanelLayoutSnapshot BuildLayoutSnapshotForTesting(
            IList<InformationStatusLine> lines,
            int screenWidth,
            int screenHeight,
            bool positionInitialized,
            int panelX,
            int panelY,
            bool adjusting)
        {
            var hasLines = lines != null && lines.Count > 0;
            if (!adjusting && !hasLines)
            {
                return new InformationStatusPanelLayoutSnapshot(0, 0, 0, 0, 0, string.Empty, 0, 0, _layoutRebuildCount);
            }

            var layout = GetOrBuildLayout(lines, hasLines, screenWidth, screenHeight, positionInitialized, adjusting);
            var rect = CreatePanelRect(layout.Width, layout.Height, screenWidth, screenHeight, positionInitialized, panelX, panelY);
            var first = layout.Rows.Length > 0 ? layout.Rows[0] : null;
            return new InformationStatusPanelLayoutSnapshot(
                layout.Width,
                layout.Height,
                rect.X,
                rect.Y,
                layout.Rows.Length,
                first == null ? string.Empty : first.Text,
                first == null ? 0 : rect.X + first.OffsetX,
                first == null ? 0 : rect.Y + first.OffsetY,
                _layoutRebuildCount);
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

                var layout = GetOrBuildLayout(
                    lines,
                    hasLines,
                    context.ScreenWidth,
                    context.ScreenHeight,
                    settings.InformationPanelPositionInitialized,
                    adjusting);
                var rect = CreatePanelRect(
                    layout.Width,
                    layout.Height,
                    context.ScreenWidth,
                    context.ScreenHeight,
                    settings.InformationPanelPositionInitialized,
                    settings.InformationPanelX,
                    settings.InformationPanelY);
                HandleAdjustment(settings, context, rect);
                DrawAdjustmentFrame(spriteBatch, rect, adjusting);
                DrawContent(spriteBatch, rect, layout);
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

        private static InformationStatusPanelLayout GetOrBuildLayout(
            IList<InformationStatusLine> lines,
            bool hasLines,
            int screenWidth,
            int screenHeight,
            bool positionInitialized,
            bool adjusting)
        {
            var key = BuildLayoutKey(lines, hasLines, screenWidth, screenHeight, positionInitialized, adjusting);
            var cached = _layoutCache;
            if (cached != null && cached.Key.Matches(key))
            {
                Interlocked.Increment(ref _layoutCacheHitCount);
                return cached.Layout;
            }

            Interlocked.Increment(ref _layoutCacheMissCount);
            var layout = BuildLayout(lines, hasLines, adjusting);
            _layoutCache = new CachedStatusPanelLayout(key, layout);
            unchecked
            {
                _layoutRebuildCount++;
            }

            return layout;
        }

        private static InformationStatusPanelLayoutKey BuildLayoutKey(
            IList<InformationStatusLine> lines,
            bool hasLines,
            int screenWidth,
            int screenHeight,
            bool positionInitialized,
            bool adjusting)
        {
            var hash = AddHash(17, hasLines ? 1 : 0);
            if (hasLines && lines != null)
            {
                for (var index = 0; index < lines.Count; index++)
                {
                    var line = lines[index];
                    if (line == null)
                    {
                        hash = AddHash(hash, unchecked((int)0x85ebca6b));
                        continue;
                    }

                    var text = line.Text ?? string.Empty;
                    hash = AddHash(hash, HashText(text));
                    hash = AddHash(hash, line.Color.R);
                    hash = AddHash(hash, line.Color.G);
                    hash = AddHash(hash, line.Color.B);
                    hash = AddHash(hash, line.Color.A);
                    hash = AddHash(hash, ScaleKey(GetLineScale(line)));
                }
            }

            return new InformationStatusPanelLayoutKey(
                hasLines,
                lines == null ? 0 : lines.Count,
                hash,
                screenWidth,
                screenHeight,
                positionInitialized,
                adjusting,
                UiTextRenderer.FontSignatureForLayoutCache,
                UiTextRenderer.CacheGenerationForLayoutCache);
        }

        private static InformationStatusPanelLayout BuildLayout(IList<InformationStatusLine> lines, bool hasLines, bool adjusting)
        {
            if (!hasLines || lines == null)
            {
                var scale = (float)DefaultFontScale;
                var row = adjusting
                    ? new[]
                    {
                        new InformationStatusPanelRow("信息窗", new InformationColor(255, 244, 180, 245), scale, Padding, Padding)
                    }
                    : new InformationStatusPanelRow[0];
                return new InformationStatusPanelLayout(PanelWidth, Padding * 2 + GetLineHeight(scale), row);
            }

            var width = PanelWidth;
            var height = Padding * 2;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var scale = GetLineScale(line);
                width = Math.Max(width, (int)Math.Ceiling((double)UiTextRenderer.EstimateTextWidth(line.Text, scale)) + Padding * 2 + 8);
                height += GetLineHeight(scale);
            }

            width = Math.Min(640, Math.Max(180, width));
            height = Math.Max(Padding * 2 + GetLineHeight((float)DefaultFontScale), height);

            var rows = new List<InformationStatusPanelRow>(lines.Count);
            var y = Padding;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (line == null || string.IsNullOrWhiteSpace(line.Text))
                {
                    continue;
                }

                var scale = GetLineScale(line);
                var lineHeight = GetLineHeight(scale);
                rows.Add(new InformationStatusPanelRow(
                    UiTextRenderer.Ellipsize(line.Text, width - Padding * 2 - 4, scale),
                    line.Color,
                    scale,
                    Padding,
                    y));
                y += lineHeight;
            }

            return new InformationStatusPanelLayout(width, height, rows.ToArray());
        }

        private static LegacyUiRect CreatePanelRect(
            int width,
            int height,
            int screenWidth,
            int screenHeight,
            bool positionInitialized,
            int panelX,
            int panelY)
        {
            var x = Clamp(panelX, 0, Math.Max(0, screenWidth - MinVisibleSize));
            var y = Clamp(panelY, 0, Math.Max(0, screenHeight - MinVisibleSize));
            if (!positionInitialized)
            {
                x = 20;
                y = Clamp((int)Math.Round(screenHeight * 0.45d), 40, Math.Max(40, screenHeight - height - 20));
            }

            if (x + width > screenWidth)
            {
                x = Math.Max(0, screenWidth - width);
            }

            if (y + height > screenHeight)
            {
                y = Math.Max(0, screenHeight - height);
            }

            return new LegacyUiRect(x, y, width, height);
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

        private static void DrawContent(object spriteBatch, LegacyUiRect rect, InformationStatusPanelLayout layout)
        {
            if (layout == null || layout.Rows.Length <= 0)
            {
                return;
            }

            for (var index = 0; index < layout.Rows.Length; index++)
            {
                var row = layout.Rows[index];
                var color = row.Color;
                UiTextRenderer.DrawText(
                    spriteBatch,
                    row.Text,
                    rect.X + row.OffsetX,
                    rect.Y + row.OffsetY,
                    color.R,
                    color.G,
                    color.B,
                    color.A,
                    row.Scale);
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

        private static int HashText(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                if (text == null)
                {
                    return hash;
                }

                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return hash;
            }
        }

        private static int AddHash(int hash, int value)
        {
            unchecked
            {
                return (hash * 16777619) ^ value;
            }
        }

        private static int ScaleKey(float scale)
        {
            return (int)Math.Round(scale * 10000f);
        }

        private sealed class CachedStatusPanelLayout
        {
            public CachedStatusPanelLayout(InformationStatusPanelLayoutKey key, InformationStatusPanelLayout layout)
            {
                Key = key;
                Layout = layout;
            }

            public InformationStatusPanelLayoutKey Key { get; private set; }

            public InformationStatusPanelLayout Layout { get; private set; }
        }

        private sealed class InformationStatusPanelLayout
        {
            public InformationStatusPanelLayout(int width, int height, InformationStatusPanelRow[] rows)
            {
                Width = width;
                Height = height;
                Rows = rows ?? new InformationStatusPanelRow[0];
            }

            public int Width { get; private set; }

            public int Height { get; private set; }

            public InformationStatusPanelRow[] Rows { get; private set; }
        }

        private sealed class InformationStatusPanelRow
        {
            public InformationStatusPanelRow(string text, InformationColor color, float scale, int offsetX, int offsetY)
            {
                Text = text ?? string.Empty;
                Color = color;
                Scale = scale;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public string Text { get; private set; }

            public InformationColor Color { get; private set; }

            public float Scale { get; private set; }

            public int OffsetX { get; private set; }

            public int OffsetY { get; private set; }
        }

        private struct InformationStatusPanelLayoutKey
        {
            private readonly bool _hasLines;
            private readonly int _lineCount;
            private readonly int _lineHash;
            private readonly int _screenWidth;
            private readonly int _screenHeight;
            private readonly bool _positionInitialized;
            private readonly bool _adjusting;
            private readonly string _fontSignature;
            private readonly int _cacheGeneration;

            public InformationStatusPanelLayoutKey(
                bool hasLines,
                int lineCount,
                int lineHash,
                int screenWidth,
                int screenHeight,
                bool positionInitialized,
                bool adjusting,
                string fontSignature,
                int cacheGeneration)
            {
                _hasLines = hasLines;
                _lineCount = lineCount;
                _lineHash = lineHash;
                _screenWidth = screenWidth;
                _screenHeight = screenHeight;
                _positionInitialized = positionInitialized;
                _adjusting = adjusting;
                _fontSignature = fontSignature ?? string.Empty;
                _cacheGeneration = cacheGeneration;
            }

            public bool Matches(InformationStatusPanelLayoutKey other)
            {
                return _hasLines == other._hasLines &&
                       _lineCount == other._lineCount &&
                       _lineHash == other._lineHash &&
                       _screenWidth == other._screenWidth &&
                       _screenHeight == other._screenHeight &&
                       _positionInitialized == other._positionInitialized &&
                       _adjusting == other._adjusting &&
                       _cacheGeneration == other._cacheGeneration &&
                       string.Equals(_fontSignature, other._fontSignature, StringComparison.Ordinal);
            }
        }

        internal sealed class InformationStatusPanelLayoutSnapshot
        {
            public InformationStatusPanelLayoutSnapshot(
                int width,
                int height,
                int x,
                int y,
                int rowCount,
                string firstRowText,
                int firstRowDrawX,
                int firstRowDrawY,
                int rebuildCount)
            {
                Width = width;
                Height = height;
                X = x;
                Y = y;
                RowCount = rowCount;
                FirstRowText = firstRowText ?? string.Empty;
                FirstRowDrawX = firstRowDrawX;
                FirstRowDrawY = firstRowDrawY;
                RebuildCount = rebuildCount;
            }

            public int Width { get; private set; }

            public int Height { get; private set; }

            public int X { get; private set; }

            public int Y { get; private set; }

            public int RowCount { get; private set; }

            public string FirstRowText { get; private set; }

            public int FirstRowDrawX { get; private set; }

            public int FirstRowDrawY { get; private set; }

            public int RebuildCount { get; private set; }
        }
    }
}
