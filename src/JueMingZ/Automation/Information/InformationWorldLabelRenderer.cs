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
    }
}
