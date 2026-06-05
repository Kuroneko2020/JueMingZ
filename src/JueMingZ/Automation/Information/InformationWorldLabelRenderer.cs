using System;
using System.Collections.Generic;
using JueMingZ.UI;

namespace JueMingZ.Automation.Information
{
    internal sealed class InformationWorldLabelRenderer
    {
        private const float LabelScale = 0.70f;
        private const float ScreenCullPadding = 80f;
        private const int MaxMeasureCacheEntries = 384;
        private const float TightStackedLineAdvanceBasePixels = 13f;
        private const float TightStackedLineAdvanceMinPixels = 6f;
        private readonly Dictionary<string, InformationLabelMeasure> _measureCache = new Dictionary<string, InformationLabelMeasure>(StringComparer.Ordinal);
        private readonly Queue<string> _measureOrder = new Queue<string>();

        public bool CanDraw(InformationWorldContext context, float worldX, float worldY, float maxDistance, bool relaxedDistance)
        {
            if (context == null || context.LocalPlayer == null)
            {
                return false;
            }

            var screenX = worldX - context.ScreenX;
            var screenY = worldY - context.ScreenY;
            if (screenX < -ScreenCullPadding ||
                screenY < -ScreenCullPadding ||
                screenX > context.ScreenWidth + ScreenCullPadding ||
                screenY > context.ScreenHeight + ScreenCullPadding)
            {
                return false;
            }

            if (relaxedDistance)
            {
                return true;
            }

            var dx = context.PlayerCenterX - worldX;
            var dy = context.PlayerCenterY - worldY;
            return dx * dx + dy * dy <= maxDistance * maxDistance;
        }

        public bool DrawWorldLabel(object spriteBatch, InformationWorldContext context, float worldX, float worldY, string label, InformationColor color, float maxDistance, bool relaxedDistance, float verticalOffset)
        {
            return DrawWorldLabel(spriteBatch, context, worldX, worldY, label, color, maxDistance, relaxedDistance, verticalOffset, LabelScale);
        }

        public bool DrawWorldLabel(object spriteBatch, InformationWorldContext context, float worldX, float worldY, string label, InformationColor color, float maxDistance, bool relaxedDistance, float verticalOffset, float scale)
        {
            if (string.IsNullOrWhiteSpace(label) || !CanDraw(context, worldX, worldY, maxDistance, relaxedDistance))
            {
                return false;
            }

            return DrawWorldLabelPrechecked(spriteBatch, context, worldX, worldY, label, color, verticalOffset, scale);
        }

        public bool DrawWorldLabelPrechecked(object spriteBatch, InformationWorldContext context, float worldX, float worldY, string label, InformationColor color, float verticalOffset)
        {
            return DrawWorldLabelPrechecked(spriteBatch, context, worldX, worldY, label, color, verticalOffset, LabelScale);
        }

        public bool DrawWorldLabelPrechecked(object spriteBatch, InformationWorldContext context, float worldX, float worldY, string label, InformationColor color, float verticalOffset, float scale)
        {
            if (spriteBatch == null || context == null || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            scale = scale <= 0.05f ? LabelScale : scale;
            var size = Measure(label, scale);
            var screenX = worldX - context.ScreenX;
            var screenY = worldY - context.ScreenY;
            var drawX = screenX - size.Width * 0.5f;
            var drawY = screenY - size.Height + verticalOffset;
            return UiTextRenderer.DrawText(spriteBatch, label, drawX, drawY, color.R, color.G, color.B, color.A, scale);
        }

        public bool DrawWorldLabelWithSubLabel(
            object spriteBatch,
            InformationWorldContext context,
            float worldX,
            float worldY,
            string label,
            string subLabel,
            InformationColor color,
            float maxDistance,
            bool relaxedDistance,
            float verticalOffset,
            float scale,
            float subScale)
        {
            if (string.IsNullOrWhiteSpace(subLabel))
            {
                return DrawWorldLabel(spriteBatch, context, worldX, worldY, label, color, maxDistance, relaxedDistance, verticalOffset, scale);
            }

            if (string.IsNullOrWhiteSpace(label) || !CanDraw(context, worldX, worldY, maxDistance, relaxedDistance))
            {
                return false;
            }

            return DrawWorldLabelWithSubLabelPrechecked(spriteBatch, context, worldX, worldY, label, subLabel, color, verticalOffset, scale, subScale);
        }

        private bool DrawWorldLabelWithSubLabelPrechecked(
            object spriteBatch,
            InformationWorldContext context,
            float worldX,
            float worldY,
            string label,
            string subLabel,
            InformationColor color,
            float verticalOffset,
            float scale,
            float subScale)
        {
            if (spriteBatch == null || context == null || string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(subLabel))
            {
                return false;
            }

            scale = NormalizeScale(scale);
            subScale = NormalizeScale(subScale);

            var labelSize = Measure(label, scale);
            var subLabelSize = Measure(subLabel, subScale);
            var lineAdvance = ResolveTightStackedLineAdvance(scale, subScale);
            var totalHeight = Math.Max(labelSize.Height, lineAdvance + subLabelSize.Height);
            var screenX = worldX - context.ScreenX;
            var screenY = worldY - context.ScreenY;
            var drawY = screenY - totalHeight + verticalOffset;
            var labelX = screenX - labelSize.Width * 0.5f;
            var subLabelX = screenX - subLabelSize.Width * 0.5f;
            var subLabelY = drawY + lineAdvance;

            var ok = UiTextRenderer.DrawText(spriteBatch, label, labelX, drawY, color.R, color.G, color.B, color.A, scale);
            ok |= UiTextRenderer.DrawText(spriteBatch, subLabel, subLabelX, subLabelY, color.R, color.G, color.B, color.A, subScale);
            return ok;
        }

        private InformationLabelMeasure Measure(string label, float scale)
        {
            var cacheKey = label + "\u001f" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            InformationLabelMeasure cached;
            if (_measureCache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            var measure = new InformationLabelMeasure(
                UiTextRenderer.EstimateTextWidth(label, scale),
                UiTextRenderer.EstimateTextHeight(scale));
            _measureCache[cacheKey] = measure;
            _measureOrder.Enqueue(cacheKey);

            while (_measureOrder.Count > MaxMeasureCacheEntries)
            {
                var oldest = _measureOrder.Dequeue();
                _measureCache.Remove(oldest);
            }

            return measure;
        }

        private static float NormalizeScale(float scale)
        {
            return scale <= 0.05f || float.IsNaN(scale) || float.IsInfinity(scale) ? LabelScale : scale;
        }

        private static float ResolveTightStackedLineAdvance(float scale, float subScale)
        {
            var safeScale = Math.Max(NormalizeScale(scale), NormalizeScale(subScale));
            return Math.Max(TightStackedLineAdvanceMinPixels, safeScale * TightStackedLineAdvanceBasePixels);
        }

        internal static float ResolveTightStackedLineAdvanceForTesting(float scale, float subScale)
        {
            return ResolveTightStackedLineAdvance(scale, subScale);
        }
    }
}
