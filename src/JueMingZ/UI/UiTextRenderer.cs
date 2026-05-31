using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using ReLogic.Graphics;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public enum UiTextHorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    public enum UiTextStyle
    {
        Border,
        Shadow
    }

    public static class UiTextRenderer
    {
        private const string CalibrationText = "国测田Agjy0123456789";
        private const float SingleLineVisualHeight = 16f;
        private const float SingleLineVerticalLift = -2f;
        private const int MeasureCacheLimit = 2048;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, CachedTextMeasure> MeasureCache = new Dictionary<string, CachedTextMeasure>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> EllipsizeCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private const int EllipsizeCacheLimit = 1024;

        private static bool _ready;
        private static DateTime _nextRetryUtc = DateTime.MinValue;
        private static DateTime _nextFontSignatureCheckUtc = DateTime.MinValue;
        private static DynamicSpriteFont _measureFont;
        private static string _fontSignature = string.Empty;
        private static string _lastError = "UI text renderer has not initialized.";

        public static string LastError
        {
            get { lock (SyncRoot) { return _lastError; } }
        }

        public static object GetSpriteBatch()
        {
            if (!EnsureReady())
            {
                return null;
            }

            return GetCachedSpriteBatch();
        }

        public static void TickMainThreadResourceMonitor()
        {
            if (!_ready)
            {
                EnsureReady();
                return;
            }

            if (DateTime.UtcNow < _nextFontSignatureCheckUtc)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_ready)
                {
                    RefreshFontSignatureLocked(false);
                }
            }
        }

        public static bool EnsureReady()
        {
            if (_ready)
            {
                if (DateTime.UtcNow < _nextFontSignatureCheckUtc)
                {
                    return true;
                }

                lock (SyncRoot)
                {
                    if (_ready)
                    {
                        RefreshFontSignatureLocked(false);
                    }
                }

                return true;
            }

            if (DateTime.UtcNow < _nextRetryUtc)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_ready)
                {
                    RefreshFontSignatureLocked(false);
                    return true;
                }

                try
                {
                    var spriteBatch = GetCachedSpriteBatch();
                    if (spriteBatch == null)
                    {
                        return FailLocked("Terraria.Main.spriteBatch was not found.");
                    }

                    ResolveMeasureFontLocked();
                    _fontSignature = BuildFontSignatureLocked();
                    _ready = true;
                    _lastError = "UI text renderer ready.";
                    return true;
                }
                catch (Exception error)
                {
                    return FailLocked("UI text renderer init failed: " + GetRootMessage(error));
                }
            }
        }

        public static void InvalidateCachedResources(string reason)
        {
            lock (SyncRoot)
            {
                ResetLocked(reason ?? "UI text renderer resources invalidated.");
            }
        }

        public static bool DrawText(object spriteBatch, string text, float x, float y, int r, int g, int b, int a)
        {
            return DrawText(spriteBatch, text, x, y, r, g, b, a, 1f);
        }

        public static bool DrawText(object spriteBatch, string text, float x, float y, int r, int g, int b, int a, float scale)
        {
            return DrawTextCore(spriteBatch, text, x, y, r, g, b, a, scale, 0f, 0f);
        }

        public static bool DrawTextWithShadow(object spriteBatch, string text, float x, float y, int r, int g, int b, int a, float scale)
        {
            if (spriteBatch == null || string.IsNullOrEmpty(text) || !EnsureReady())
            {
                return false;
            }

            var overrideY = GetVerticalOffsetOverride();
            var shadowAlpha = Math.Min(220, Math.Max(0, a));
            var ok = DrawTextCore(spriteBatch, text, x + 2f, y + 2f + overrideY, 8, 10, 14, shadowAlpha, scale, 0f, 0f);
            ok |= DrawTextCore(spriteBatch, text, x, y + overrideY, r, g, b, a, scale, 0f, 0f);
            return ok;
        }

        public static bool DrawCenteredText(object spriteBatch, string text, int x, int y, int width, int height, int r, int g, int b, int a, float scale)
        {
            return DrawAlignedText(spriteBatch, text, x, y, width, height, UiTextHorizontalAlignment.Center, r, g, b, a, scale);
        }

        public static bool DrawRightAlignedText(object spriteBatch, string text, int x, int y, int width, int height, int r, int g, int b, int a, float scale)
        {
            return DrawAlignedText(spriteBatch, text, x, y, width, height, UiTextHorizontalAlignment.Right, r, g, b, a, scale);
        }

        public static bool DrawAlignedText(object spriteBatch, string text, int x, int y, int width, int height, UiTextHorizontalAlignment alignment, int r, int g, int b, int a, float scale)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var display = Ellipsize(text, width, scale);
            float drawX;
            float drawY;
            CalculateSingleLinePosition(display, x, y, width, height, alignment, scale, out drawX, out drawY);
            return DrawTextCore(spriteBatch, display, drawX, drawY, r, g, b, a, scale, 0f, 0f);
        }

        public static bool DrawCenteredTextClipped(object spriteBatch, string text, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a, float scale)
        {
            return DrawAlignedTextClipped(spriteBatch, text, x, y, width, height, UiTextHorizontalAlignment.Center, clipX, clipY, clipWidth, clipHeight, r, g, b, a, scale);
        }

        public static bool DrawAlignedTextClipped(object spriteBatch, string text, int x, int y, int width, int height, UiTextHorizontalAlignment alignment, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a, float scale)
        {
            if (!RectIntersects(x, y, width, height, clipX, clipY, clipWidth, clipHeight))
            {
                return false;
            }

            var display = Ellipsize(text, width, scale);
            float drawX;
            float drawY;
            CalculateSingleLinePosition(display, x, y, width, height, alignment, scale, out drawX, out drawY);
            if (!TryClampSingleLineDrawYToClip(drawY, scale, clipY, clipHeight, out drawY))
            {
                return false;
            }

            return DrawTextCore(spriteBatch, display, drawX, drawY, r, g, b, a, scale, 0f, 0f);
        }

        public static bool DrawTextClipped(object spriteBatch, string text, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a, float scale)
        {
            if (!RectIntersects(x, y, width, height, clipX, clipY, clipWidth, clipHeight))
            {
                return false;
            }

            if (height > 0 && height <= 24)
            {
                var display = Ellipsize(text, width, scale);
                float drawX;
                float drawY;
                CalculateSingleLinePosition(display, x, y, width, height, UiTextHorizontalAlignment.Left, scale, out drawX, out drawY);
                if (!TryClampSingleLineDrawYToClip(drawY, scale, clipY, clipHeight, out drawY))
                {
                    return false;
                }

                return DrawTextCore(spriteBatch, display, drawX, drawY, r, g, b, a, scale, 0f, 0f);
            }

            if (y < clipY || y + height > clipY + clipHeight)
            {
                return false;
            }

            return DrawTextCore(spriteBatch, Ellipsize(text, width, scale), x, y, r, g, b, a, scale, 0f, 0f);
        }

        internal static bool TryCalculateClippedSingleLinePositionForTesting(
            string text,
            int x,
            int y,
            int width,
            int height,
            UiTextHorizontalAlignment alignment,
            int clipX,
            int clipY,
            int clipWidth,
            int clipHeight,
            float scale,
            out float drawX,
            out float drawY)
        {
            drawX = 0f;
            drawY = 0f;
            if (!RectIntersects(x, y, width, height, clipX, clipY, clipWidth, clipHeight))
            {
                return false;
            }

            var display = Ellipsize(text, width, scale);
            CalculateSingleLinePosition(display, x, y, width, height, alignment, scale, out drawX, out drawY);
            if (!TryClampSingleLineDrawYToClip(drawY, scale, clipY, clipHeight, out drawY))
            {
                return false;
            }

            return true;
        }

        public static string Ellipsize(string text, int maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (maxWidth <= 0)
            {
                return string.Empty;
            }

            var safeScale = Math.Max(0.1f, scale);
            if (EstimateTextWidth(text, safeScale) <= maxWidth)
            {
                return text;
            }

            const string suffix = "...";
            if (EstimateTextWidth(suffix, safeScale) > maxWidth)
            {
                return string.Empty;
            }

            var fontSignature = GetFontSignatureForCache();
            var cacheKey = fontSignature +
                           "\n" + maxWidth.ToString(CultureInfo.InvariantCulture) +
                           "\n" + safeScale.ToString("R", CultureInfo.InvariantCulture) +
                           "\n" + text;
            lock (SyncRoot)
            {
                string cached;
                if (EllipsizeCache.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }
            }

            var low = 0;
            var high = text.Length;
            var best = 0;
            while (low <= high)
            {
                var mid = low + (high - low) / 2;
                var candidate = mid <= 0 ? suffix : text.Substring(0, mid) + suffix;
                if (EstimateTextWidth(candidate, safeScale) <= maxWidth)
                {
                    best = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            var result = best <= 0 ? suffix : text.Substring(0, best) + suffix;
            lock (SyncRoot)
            {
                if (EllipsizeCache.Count >= EllipsizeCacheLimit)
                {
                    EllipsizeCache.Clear();
                }

                EllipsizeCache[cacheKey] = result;
            }

            return result;
        }

        public static int EstimateTextWidth(string text, float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int measuredWidth;
            int measuredHeight;
            if (TryMeasureText(text, scale, out measuredWidth, out measuredHeight))
            {
                return measuredWidth;
            }

            var units = 0d;
            for (var index = 0; index < text.Length; index++)
            {
                units += text[index] <= 0x007f ? 8.2d : 15.5d;
            }

            return (int)Math.Ceiling(units * Math.Max(0.1f, scale));
        }

        public static int EstimateTextHeight(float scale)
        {
            int measuredWidth;
            int measuredHeight;
            if (TryMeasureText(CalibrationText, scale, out measuredWidth, out measuredHeight))
            {
                return measuredHeight;
            }

            return (int)Math.Ceiling(22d * Math.Max(0.1f, scale));
        }

        private static void CalculateSingleLinePosition(string text, int x, int y, int width, int height, UiTextHorizontalAlignment alignment, float scale, out float drawX, out float drawY)
        {
            var safeScale = Math.Max(0.1f, scale);
            var textWidth = Math.Max(0, EstimateTextWidth(text, safeScale));
            var minX = (float)x;
            var maxX = x + width - textWidth;
            if (maxX < minX)
            {
                drawX = minX;
            }
            else if (alignment == UiTextHorizontalAlignment.Right)
            {
                drawX = maxX;
            }
            else if (alignment == UiTextHorizontalAlignment.Center)
            {
                drawX = x + (width - textWidth) * 0.5f;
                drawX = ClampFloat(drawX, minX, maxX);
            }
            else
            {
                drawX = minX;
            }

            var visualHeight = SingleLineVisualHeight * safeScale;
            var minY = (float)y;
            var maxY = y + height - visualHeight;
            var targetY = y + (height - visualHeight) * 0.5f + SingleLineVerticalLift * safeScale + GetVerticalOffsetOverride();
            drawY = maxY < minY ? minY : ClampFloat(targetY, minY, maxY);
            drawX = (float)Math.Round(drawX);
            drawY = (float)Math.Round(drawY);
        }

        private static bool TryClampSingleLineDrawYToClip(float drawY, float scale, int clipY, int clipHeight, out float clippedDrawY)
        {
            var safeScale = Math.Max(0.1f, scale);
            var visualHeight = SingleLineVisualHeight * safeScale;
            var minY = (float)clipY;
            var clipBottom = clipY + clipHeight;
            if (clipBottom <= minY)
            {
                clippedDrawY = drawY;
                return false;
            }

            // Keep the natural text baseline so partially visible rows can show
            // clipped glyphs naturally when they cross the viewport boundary.
            // Only suppress lines that are fully outside the viewport.
            var lineTop = drawY;
            var lineBottom = drawY + visualHeight;
            if (lineBottom <= minY || lineTop >= clipBottom)
            {
                clippedDrawY = drawY;
                return false;
            }

            clippedDrawY = drawY;
            return true;
        }

        private static bool DrawTextCore(object spriteBatch, string text, float x, float y, int r, int g, int b, int a, float scale, float anchorX, float anchorY)
        {
            if (spriteBatch == null || string.IsNullOrEmpty(text) || !EnsureReady())
            {
                return false;
            }

            try
            {
                var drawX = x;
                var drawY = y;
                var effectiveScale = Math.Max(0.1f, scale);
                var batch = spriteBatch as SpriteBatch;
                if (batch == null)
                {
                    return Fail("DrawText failed: SpriteBatch type is unavailable.");
                }

                Utils.DrawBorderString(
                    batch,
                    text,
                    new Vector2(drawX, drawY),
                    CreateColor(r, g, b, a),
                    effectiveScale,
                    anchorX,
                    anchorY);
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawText failed: " + GetRootMessage(error));
            }
        }

        private static bool TryMeasureText(string text, float scale, out int width, out int height)
        {
            width = 0;
            height = 0;
            CachedTextMeasure measure;
            if (!TryGetBaseTextMeasure(text, out measure))
            {
                return false;
            }

            var safeScale = Math.Max(0.1f, scale);
            width = (int)Math.Ceiling(measure.Width * safeScale);
            height = (int)Math.Ceiling(measure.Height * safeScale);
            return width > 0 && height > 0;
        }


        private static bool TryGetBaseTextMeasure(string text, out CachedTextMeasure measure)
        {
            measure = null;
            if (!_ready || string.IsNullOrEmpty(text) || _measureFont == null)
            {
                return false;
            }

            var key = (_fontSignature ?? string.Empty) + "\n" + text;
            lock (SyncRoot)
            {
                if (MeasureCache.TryGetValue(key, out measure))
                {
                    return true;
                }
            }

            try
            {
                var vector = _measureFont.MeasureString(text);

                var measured = new CachedTextMeasure(
                    Math.Max(0f, vector.X),
                    Math.Max(0f, vector.Y));
                if (measured.Width <= 0f || measured.Height <= 0f)
                {
                    return false;
                }

                lock (SyncRoot)
                {
                    if (MeasureCache.Count >= MeasureCacheLimit)
                    {
                        MeasureCache.Clear();
                    }

                    MeasureCache[key] = measured;
                }

                measure = measured;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RefreshFontSignatureLocked(bool force)
        {
            var now = DateTime.UtcNow;
            if (!force && now < _nextFontSignatureCheckUtc)
            {
                return;
            }

            _nextFontSignatureCheckUtc = now.AddSeconds(1);
            var previousSignature = _fontSignature ?? string.Empty;
            ResolveMeasureFontLocked();
            var signature = BuildFontSignatureLocked();
            if (string.IsNullOrEmpty(previousSignature))
            {
                _fontSignature = signature;
                return;
            }

            if (!string.Equals(previousSignature, signature, StringComparison.Ordinal))
            {
                _fontSignature = signature;
                MeasureCache.Clear();
                EllipsizeCache.Clear();
                _lastError = "UI text font resources changed; text caches were invalidated.";
                LogThrottle.InfoThrottled(
                    "ui-text-font-resource-changed",
                    TimeSpan.FromSeconds(5),
                    "UiTextRenderer",
                    "FontAssets.MouseText changed; cleared UI text measure caches.");
            }
        }

        private static string BuildFontSignatureLocked()
        {
            if (_measureFont == null)
            {
                return "font:null";
            }

            var measureX = 0f;
            var measureY = 0f;
            try
            {
                var vector = _measureFont.MeasureString(CalibrationText);
                measureX = vector.X;
                measureY = vector.Y;
            }
            catch
            {
            }

            return (_measureFont.GetType().FullName ?? "unknown") +
                   "|" + _measureFont.GetHashCode().ToString(CultureInfo.InvariantCulture) +
                   "|line=" + ReadNumericMember(_measureFont, "LineSpacing", 0d).ToString("0.###", CultureInfo.InvariantCulture) +
                   "|measure=" + measureX.ToString("0.###", CultureInfo.InvariantCulture) + "x" + measureY.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string GetFontSignatureForCache()
        {
            lock (SyncRoot)
            {
                return _fontSignature ?? string.Empty;
            }
        }

        private static void ResolveMeasureFontLocked()
        {
            _measureFont = null;

            try
            {
                _measureFont = FontAssets.MouseText.Value as DynamicSpriteFont;
                if (_measureFont == null)
                {
                    return;
                }

                var test = _measureFont.MeasureString(CalibrationText);
                if (test.X <= 0f || test.Y <= 0f)
                {
                    _measureFont = null;
                }
            }
            catch
            {
                _measureFont = null;
            }
        }

        private static object GetCachedSpriteBatch()
        {
            try
            {
                return Main.spriteBatch;
            }
            catch
            {
                return null;
            }
        }

        private static Color CreateColor(int r, int g, int b, int a)
        {
            return new Color(ClampColorComponent(r), ClampColorComponent(g), ClampColorComponent(b), ClampColorComponent(a));
        }

        private static int ClampColorComponent(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(null);
            }

            var property = type.GetProperty(name, flags);
            return property != null && property.CanRead ? property.GetValue(null, null) : null;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var type = instance.GetType();
            var property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
            {
                return property.GetValue(instance, null);
            }

            var field = type.GetField(name, flags);
            return field == null ? null : field.GetValue(instance);
        }

        private static double ReadNumericMember(object instance, string name, double fallback)
        {
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsColorComponent(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(int) ||
                   type == typeof(float) ||
                   type == typeof(double);
        }

        private static MethodInfo FindDrawBorderStringMethod(Type utilsType, object spriteBatch, Type vector2Type, Type colorType)
        {
            MethodInfo best = null;
            var methods = utilsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "DrawBorderString", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 4 ||
                    !parameters[0].ParameterType.IsInstanceOfType(spriteBatch) ||
                    parameters[1].ParameterType != typeof(string) ||
                    parameters[2].ParameterType.FullName != vector2Type.FullName ||
                    parameters[3].ParameterType.FullName != colorType.FullName)
                {
                    continue;
                }

                if (best == null || parameters.Length < best.GetParameters().Length)
                {
                    best = method;
                }
            }

            return best;
        }

        private static object[] BuildDrawBorderStringArgs(MethodInfo method, object spriteBatch, string text, object position, object color, float scale, float anchorX, float anchorY)
        {
            var parameters = method.GetParameters();
            var args = new object[parameters.Length];
            args[0] = spriteBatch;
            args[1] = text ?? string.Empty;
            args[2] = position;
            args[3] = color;

            for (var index = 4; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter.ParameterType == typeof(float))
                {
                    if (index == 4)
                    {
                        args[index] = scale;
                    }
                    else if (index == 5)
                    {
                        args[index] = anchorX;
                    }
                    else if (index == 6)
                    {
                        args[index] = anchorY;
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        args[index] = parameter.DefaultValue;
                    }
                    else
                    {
                        args[index] = 0f;
                    }
                }
                else if (parameter.ParameterType == typeof(int))
                {
                    args[index] = -1;
                }
                else if (parameter.ParameterType == typeof(bool))
                {
                    args[index] = false;
                }
                else if (parameter.HasDefaultValue)
                {
                    args[index] = parameter.DefaultValue;
                }
                else
                {
                    args[index] = parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null;
                }
            }

            return args;
        }

        private static bool RectIntersects(int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight)
        {
            return width > 0 && height > 0 && clipWidth > 0 && clipHeight > 0 &&
                   x < clipX + clipWidth &&
                   x + width > clipX &&
                   y < clipY + clipHeight &&
                   y + height > clipY;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static float ClampFloat(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static float GetVerticalOffsetOverride()
        {
            try
            {
                var settings = ConfigService.AppSettings;
                if (settings != null && settings.UiTextVerticalOffsetOverrideEnabled)
                {
                    return (float)ClampFloat((float)settings.UiTextVerticalOffsetOverride, -12f, 12f);
                }
            }
            catch
            {
            }

            return 0f;
        }

        private static string GetRootMessage(Exception error)
        {
            var current = error;
            while (current != null && current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current == null ? string.Empty : current.Message;
        }

        private static bool Fail(string message)
        {
            lock (SyncRoot)
            {
                return FailLocked(message);
            }
        }

        private static bool FailLocked(string message)
        {
            ResetLocked(message ?? "UI text renderer unavailable.");
            LogThrottle.WarnThrottled(
                "ui-text-renderer-unavailable",
                TimeSpan.FromSeconds(10),
                "UiTextRenderer",
                _lastError);
            return false;
        }

        private static void ResetLocked(string message)
        {
            _ready = false;
            _measureFont = null;
            _fontSignature = string.Empty;
            MeasureCache.Clear();
            EllipsizeCache.Clear();
            _lastError = message ?? "UI text renderer unavailable.";
            _nextRetryUtc = DateTime.UtcNow.AddSeconds(1);
            _nextFontSignatureCheckUtc = DateTime.MinValue;
        }

        private sealed class CachedTextMeasure
        {
            public CachedTextMeasure(float width, float height)
            {
                Width = width;
                Height = height;
            }

            public float Width { get; private set; }

            public float Height { get; private set; }
        }

    }
}
