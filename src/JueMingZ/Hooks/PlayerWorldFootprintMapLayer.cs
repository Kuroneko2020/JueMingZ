using System;
using System.Reflection;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Map;

namespace JueMingZ.Hooks
{
    internal sealed class PlayerWorldFootprintMapLayer : IMapLayer
    {
        private const float LineThicknessPixels = 2f;
        private static readonly object TransformFieldSyncRoot = new object();
        private static bool _transformFieldsResolved;
        private static FieldInfo _mapPositionField;
        private static FieldInfo _mapOffsetField;
        private static FieldInfo _mapScaleField;
        private static FieldInfo _opacityField;

        public void Draw(ref MapOverlayDrawContext context, ref string text)
        {
            if (!Main.mapFullscreen)
            {
                PlayerWorldFootprintDiagnostics.RecordMapDraw("notFullscreen", "footprint lines draw only on fullscreen map", string.Empty, 0, 0, 0, 0, 0, false);
                return;
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.MapFootprintsDisplayEnabled)
            {
                PlayerWorldFootprintDiagnostics.RecordMapDraw("displayHidden", "map footprints display is off", string.Empty, 0, 0, 0, 0, 0, false);
                return;
            }

            Texture2D pixel;
            if (!TryGetMagicPixel(out pixel))
            {
                PlayerWorldFootprintDiagnostics.RecordMapDraw("textureUnavailable", "TextureAssets.MagicPixel is unavailable", string.Empty, 0, 0, 0, 0, 0, false);
                return;
            }

            MapFootprintDrawTransform transform;
            if (!TryReadTransform(ref context, out transform))
            {
                PlayerWorldFootprintDiagnostics.RecordMapDraw("transformUnavailable", "MapOverlayDrawContext transform fields are unavailable", string.Empty, 0, 0, 0, 0, 0, false);
                return;
            }

            var snapshot = MapFootprintRenderCache.GetSnapshot();
            var playback = MapFootprintPlaybackState.GetSnapshotForRender(snapshot, Main.mapFullscreen, DateTime.UtcNow);
            var screen = new Rectangle(0, 0, Math.Max(1, Main.screenWidth), Math.Max(1, Main.screenHeight));
            var plan = MapFootprintRenderCache.BuildDrawPlan(
                snapshot,
                transform,
                screen,
                MapFootprintRenderCache.DefaultMaxDrawnLines,
                MapFootprintRenderCache.DefaultMinDrawPixelStep,
                playback.CursorTicks);

            for (var index = 0; plan.Commands != null && index < plan.Commands.Length; index++)
            {
                DrawLine(pixel, plan.Commands[index], transform.Opacity);
            }

            PlayerWorldFootprintDiagnostics.RecordMapDraw(
                plan.Status,
                plan.Message,
                plan.PairId,
                plan.CachedLineCount,
                plan.DrawnLineCount,
                plan.CulledLineCount,
                plan.ThinnedLineCount,
                plan.DrawLimitSkippedLineCount,
                plan.DrawLimitHit);
        }

        private static bool TryGetMagicPixel(out Texture2D pixel)
        {
            pixel = null;
            try
            {
                if (TextureAssets.MagicPixel == null || !TextureAssets.MagicPixel.IsLoaded)
                {
                    return false;
                }

                pixel = TextureAssets.MagicPixel.Value;
                return pixel != null;
            }
            catch
            {
                pixel = null;
                return false;
            }
        }

        private static void DrawLine(Texture2D pixel, MapFootprintDrawCommand command, float opacity)
        {
            if (pixel == null || command == null || Main.spriteBatch == null)
            {
                return;
            }

            var delta = command.End - command.Start;
            var length = delta.Length();
            if (length <= 0.1f || float.IsNaN(length) || float.IsInfinity(length))
            {
                return;
            }

            var alpha = Math.Max(0f, Math.Min(1f, opacity));
            var color = new Color(48, 255, 96, 220) * alpha;
            Main.spriteBatch.Draw(
                pixel,
                command.Start,
                null,
                color,
                (float)Math.Atan2(delta.Y, delta.X),
                new Vector2(0f, 0.5f),
                new Vector2(length, LineThicknessPixels),
                SpriteEffects.None,
                0f);
        }

        private static bool TryReadTransform(ref MapOverlayDrawContext context, out MapFootprintDrawTransform transform)
        {
            transform = new MapFootprintDrawTransform();
            if (!EnsureTransformFields())
            {
                return false;
            }

            try
            {
                var boxed = (object)context;
                transform = new MapFootprintDrawTransform
                {
                    MapPosition = (Vector2)_mapPositionField.GetValue(boxed),
                    MapOffset = (Vector2)_mapOffsetField.GetValue(boxed),
                    MapScale = (float)_mapScaleField.GetValue(boxed),
                    Opacity = (float)_opacityField.GetValue(boxed)
                };

                return transform.MapScale > 0f && transform.Opacity > 0f;
            }
            catch
            {
                transform = new MapFootprintDrawTransform();
                return false;
            }
        }

        private static bool EnsureTransformFields()
        {
            if (_transformFieldsResolved)
            {
                return _mapPositionField != null &&
                       _mapOffsetField != null &&
                       _mapScaleField != null &&
                       _opacityField != null;
            }

            lock (TransformFieldSyncRoot)
            {
                if (_transformFieldsResolved)
                {
                    return _mapPositionField != null &&
                           _mapOffsetField != null &&
                           _mapScaleField != null &&
                           _opacityField != null;
                }

                var type = typeof(MapOverlayDrawContext);
                TerrariaMemberCache.TryGetField(type, "_mapPosition", false, out _mapPositionField);
                TerrariaMemberCache.TryGetField(type, "_mapOffset", false, out _mapOffsetField);
                TerrariaMemberCache.TryGetField(type, "_mapScale", false, out _mapScaleField);
                TerrariaMemberCache.TryGetField(type, "_opacity", false, out _opacityField);
                _transformFieldsResolved = true;
                return _mapPositionField != null &&
                       _mapOffsetField != null &&
                       _mapScaleField != null &&
                       _opacityField != null;
            }
        }
    }
}
