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
        private const float LineThicknessPixels = 1f;
        private const string DrawDiagnosticsRoute = "mapLayer";
        private static readonly Rectangle MagicPixelSourceRectangle = new Rectangle(0, 0, 1, 1);
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
            if (string.Equals(plan.Status, "ready", StringComparison.Ordinal))
            {
                PlayerWorldFootprintDiagnostics.RecordMapDrawDetail(BuildDrawDiagnostics(plan, transform, screen));
            }
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
            // Terraria's MagicPixel asset is 1x1000; crop it to one pixel so scale.Y remains the actual line thickness.
            Main.spriteBatch.Draw(
                pixel,
                command.Start,
                MagicPixelSourceRectangle,
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

        private static MapFootprintDrawDiagnosticsData BuildDrawDiagnostics(
            MapFootprintDrawPlan plan,
            MapFootprintDrawTransform transform,
            Rectangle screen)
        {
            var data = new MapFootprintDrawDiagnosticsData
            {
                Route = DrawDiagnosticsRoute,
                ScreenWidth = screen.Width,
                ScreenHeight = screen.Height,
                MapFullscreenPosX = Main.mapFullscreenPos.X,
                MapFullscreenPosY = Main.mapFullscreenPos.Y,
                MapFullscreenScale = Main.mapFullscreenScale,
                TransformMapPositionX = transform.MapPosition.X,
                TransformMapPositionY = transform.MapPosition.Y,
                TransformMapOffsetX = transform.MapOffset.X,
                TransformMapOffsetY = transform.MapOffset.Y,
                TransformMapScale = transform.MapScale,
                TransformOpacity = transform.Opacity
            };

            ulong gameUpdateCount;
            if (TerrariaMainCompat.TryReadGameUpdateCount(out gameUpdateCount))
            {
                data.GameUpdateCount = gameUpdateCount > long.MaxValue ? long.MaxValue : (long)gameUpdateCount;
            }

            var diagonal = Math.Sqrt((double)Math.Max(1, screen.Width) * Math.Max(1, screen.Width) +
                                     (double)Math.Max(1, screen.Height) * Math.Max(1, screen.Height));
            var longLineThreshold = Math.Max(512d, diagonal * 0.90d);
            data.LongLineThresholdPixels = longLineThreshold;

            var commands = plan == null ? null : plan.Commands;
            if (commands == null || commands.Length <= 0)
            {
                return data;
            }

            data.CommandSampleCount = commands.Length;
            ApplyCommandSample(commands[0], "first", data);
            ApplyCommandSample(commands[commands.Length - 1], "last", data);

            var maxLength = 0d;
            MapFootprintDrawCommand longest = null;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command == null)
                {
                    continue;
                }

                var length = (command.End - command.Start).Length();
                if (float.IsNaN(length) || float.IsInfinity(length))
                {
                    data.AbnormalLongLineCount++;
                    continue;
                }

                if (length >= longLineThreshold)
                {
                    data.AbnormalLongLineCount++;
                }

                if (length > maxLength)
                {
                    maxLength = length;
                    longest = command;
                }
            }

            data.MaxLinePixels = maxLength;
            if (longest != null)
            {
                data.MaxLineSegmentIndex = longest.SegmentIndex;
                ApplyCommandSample(longest, "longest", data);
            }

            return data;
        }

        private static void ApplyCommandSample(MapFootprintDrawCommand command, string slot, MapFootprintDrawDiagnosticsData data)
        {
            if (command == null || data == null)
            {
                return;
            }

            if (string.Equals(slot, "first", StringComparison.Ordinal))
            {
                data.FirstSegmentIndex = command.SegmentIndex;
                data.FirstStartTileX = command.StartTileX;
                data.FirstStartTileY = command.StartTileY;
                data.FirstEndTileX = command.EndTileX;
                data.FirstEndTileY = command.EndTileY;
                data.FirstStartScreenX = command.Start.X;
                data.FirstStartScreenY = command.Start.Y;
                data.FirstEndScreenX = command.End.X;
                data.FirstEndScreenY = command.End.Y;
                return;
            }

            if (string.Equals(slot, "last", StringComparison.Ordinal))
            {
                data.LastSegmentIndex = command.SegmentIndex;
                data.LastStartTileX = command.StartTileX;
                data.LastStartTileY = command.StartTileY;
                data.LastEndTileX = command.EndTileX;
                data.LastEndTileY = command.EndTileY;
                data.LastStartScreenX = command.Start.X;
                data.LastStartScreenY = command.Start.Y;
                data.LastEndScreenX = command.End.X;
                data.LastEndScreenY = command.End.Y;
                return;
            }

            data.LongestSegmentIndex = command.SegmentIndex;
            data.LongestStartTileX = command.StartTileX;
            data.LongestStartTileY = command.StartTileY;
            data.LongestEndTileX = command.EndTileX;
            data.LongestEndTileY = command.EndTileY;
            data.LongestStartScreenX = command.Start.X;
            data.LongestStartScreenY = command.Start.Y;
            data.LongestEndScreenX = command.End.X;
            data.LongestEndScreenY = command.End.Y;
        }
    }
}
