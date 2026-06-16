using System;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingZ.UI
{
    internal static class MapFootprintPlaybackOverlay
    {
        internal const string FullscreenMapRoute = "fullscreenMap";
        private const float TextScale = 0.62f;

        public static void UpdatePrefixGuard()
        {
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var visible = ShouldUseOverlay(Main.mapFullscreen, settings.MapFootprintsDisplayEnabled);
                var snapshot = MapFootprintRenderCache.GetSnapshot();
                MapFootprintPlaybackState.Advance(snapshot, visible, DateTime.UtcNow);
                if (!visible)
                {
                    return;
                }

                var mouse = LegacyUiInput.ReadMouse();
                var layout = MapFootprintPlaybackState.CalculateLayout(TerrariaMainCompat.ScreenWidth, TerrariaMainCompat.ScreenHeight);
                var interaction = MapFootprintPlaybackState.HandleInput(layout, mouse, snapshot, visible, DateTime.UtcNow);
                ApplyInputCapture(interaction);
                RecordOverlayDiagnostics(interaction, "prefix");
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MapFootprintPlaybackOverlay.UpdatePrefixGuard", error);
                LogThrottle.ErrorThrottled(
                    "map-footprint-playback-prefix-error",
                    TimeSpan.FromSeconds(10),
                    "MapFootprintPlaybackOverlay",
                    "Footprint playback overlay prefix input guard failed; exception swallowed.",
                    error);
            }
        }

        public static void DrawFullscreenMapLayer(Vector2 mapTopLeft, float scale)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var visible = ShouldUseOverlay(Main.mapFullscreen, settings.MapFootprintsDisplayEnabled);
            var renderSnapshot = MapFootprintRenderCache.GetSnapshot();
            var playback = MapFootprintPlaybackState.GetSnapshotForRender(renderSnapshot, visible, DateTime.UtcNow);
            if (!visible)
            {
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "hidden",
                    "map footprints playback overlay hidden",
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    false,
                    false,
                    playback.LastInteraction);
                return;
            }

            UiInputFrameClock.BeginDrawFrame("MapFootprintPlaybackOverlay");

            SpriteBatch spriteBatch;
            if (!TerrariaDrawCompat.TryGetSpriteBatch(out spriteBatch))
            {
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "spriteBatchUnavailable",
                    "SpriteBatch unavailable",
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    false,
                    false,
                    playback.LastInteraction);
                return;
            }

            if (!VanillaUiSkinCompat.PrepareForDraw("MapFootprintPlaybackOverlay") ||
                !UiPrimitiveRenderer.EnsureReady(spriteBatch))
            {
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "resourcesNotReady",
                    "playback overlay resources are not ready",
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    false,
                    false,
                    playback.LastInteraction);
                return;
            }

            var begun = false;
            try
            {
                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    BlendState.AlphaBlend,
                    Main.SamplerStateForCursor,
                    DepthStencilState.None,
                    RasterizerState.CullCounterClockwise,
                    null,
                    Main.UIScaleMatrix);
                begun = true;
                var mouse = LegacyUiInput.ReadMouse();
                var layout = MapFootprintPlaybackState.CalculateLayout(TerrariaMainCompat.ScreenWidth, TerrariaMainCompat.ScreenHeight);
                var hit = MapFootprintPlaybackState.HitTest(layout, mouse.X, mouse.Y);
                playback = MapFootprintPlaybackState.GetSnapshotForRender(renderSnapshot, visible, DateTime.UtcNow);
                DrawOverlay(spriteBatch, layout, playback, hit);
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "ready",
                    "playback overlay ready",
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    hit.BarHovered || playback.Dragging,
                    hit.BarHovered,
                    playback.LastInteraction);
            }
            catch (Exception error)
            {
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    begun ? "drawException" : "spriteBatchBeginFailed",
                    error.Message,
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    false,
                    false,
                    playback.LastInteraction);
                UiDrawLifecycleGuard.RecordDrawException("MapFootprintPlaybackOverlay", error);
                LogThrottle.ErrorThrottled(
                    "map-footprint-playback-draw-error",
                    TimeSpan.FromSeconds(10),
                    "MapFootprintPlaybackOverlay",
                    "Footprint playback overlay draw failed; exception swallowed.",
                    error);
            }
            finally
            {
                if (begun)
                {
                    try
                    {
                        spriteBatch.End();
                    }
                    catch (Exception error)
                    {
                        PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                            "spriteBatchEndFailed",
                            error.Message,
                            playback.PairId,
                            playback.Paused,
                            playback.PlaybackRate,
                            playback.CursorTicks,
                            playback.TimelineStartTicks,
                            playback.TimelineEndTicks,
                            playback.IsAtLatest,
                            playback.Dragging,
                            false,
                            false,
                            playback.LastInteraction);
                        LogThrottle.ErrorThrottled(
                            "map-footprint-playback-draw-end-error",
                            TimeSpan.FromSeconds(10),
                            "MapFootprintPlaybackOverlay",
                            "Footprint playback overlay SpriteBatch.End failed; exception swallowed.",
                            error);
                    }
                }
            }
        }

        internal static bool ShouldUseOverlayForTesting(bool mapFullscreen, bool displayEnabled)
        {
            return ShouldUseOverlay(mapFullscreen, displayEnabled);
        }

        internal static MapFootprintPlaybackLayout CalculateLayoutForTesting(int screenWidth, int screenHeight)
        {
            return MapFootprintPlaybackState.CalculateLayout(screenWidth, screenHeight);
        }

        internal static MapFootprintPlaybackHitTest HitTestForTesting(MapFootprintPlaybackLayout layout, int x, int y)
        {
            return MapFootprintPlaybackState.HitTest(layout, x, y);
        }

        private static bool ShouldUseOverlay(bool mapFullscreen, bool displayEnabled)
        {
            return mapFullscreen && displayEnabled;
        }

        private static void ApplyInputCapture(MapFootprintPlaybackInteraction interaction)
        {
            if (interaction == null || !interaction.MouseCaptured)
            {
                return;
            }

            if (interaction.ClickConsumed)
            {
                string message;
                UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out message);
            }

            UiMouseCaptureService.CaptureForOperationWindow();
            if (interaction.ScrollConsumed)
            {
                UiMouseCaptureService.ConsumeScrollForOperationWindow();
            }
        }

        private static void DrawOverlay(
            object spriteBatch,
            MapFootprintPlaybackLayout layout,
            MapFootprintPlaybackSnapshot playback,
            MapFootprintPlaybackHitTest hit)
        {
            LegacyUiTheme.DrawTooltip(spriteBatch, layout.Bar);
            DrawPlayButton(spriteBatch, layout, playback, hit);
            DrawTrack(spriteBatch, layout, playback, hit);
            DrawRateButtons(spriteBatch, layout, playback, hit);
        }

        private static void DrawPlayButton(
            object spriteBatch,
            MapFootprintPlaybackLayout layout,
            MapFootprintPlaybackSnapshot playback,
            MapFootprintPlaybackHitTest hit)
        {
            var hovered = hit != null && string.Equals(hit.Target, MapFootprintPlaybackHitTargets.PlayButton, StringComparison.Ordinal);
            LegacyUiTheme.DrawButton(spriteBatch, layout.PlayButton, hovered, hovered && playback.Dragging, false, true);
            UiTextRenderer.DrawCenteredText(
                spriteBatch,
                playback.Paused ? "播放" : "暂停",
                layout.PlayButton.X + 3,
                layout.PlayButton.Y,
                layout.PlayButton.Width - 6,
                layout.PlayButton.Height,
                232,
                236,
                224,
                255,
                TextScale);
        }

        private static void DrawRateButtons(
            object spriteBatch,
            MapFootprintPlaybackLayout layout,
            MapFootprintPlaybackSnapshot playback,
            MapFootprintPlaybackHitTest hit)
        {
            for (var index = 0; layout.RateButtons != null && index < layout.RateButtons.Length; index++)
            {
                var rect = layout.RateButtons[index];
                var rate = layout.RateValues == null || index >= layout.RateValues.Length ? 0 : layout.RateValues[index];
                var hovered = hit != null &&
                              string.Equals(hit.Target, MapFootprintPlaybackHitTargets.RateButton, StringComparison.Ordinal) &&
                              hit.Rate == rate;
                var selected = rate == playback.PlaybackRate;
                LegacyUiTheme.DrawButton(spriteBatch, rect, hovered, false, selected, true);
                var content = LegacyUiTheme.GetSelectedButtonContentRect(rect, selected, true);
                UiTextRenderer.DrawCenteredText(
                    spriteBatch,
                    rate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x",
                    content.X + 2,
                    content.Y,
                    content.Width - 4,
                    content.Height,
                    selected ? LegacyUiTheme.SelectedTextR : 230,
                    selected ? LegacyUiTheme.SelectedTextG : 232,
                    selected ? LegacyUiTheme.SelectedTextB : 224,
                    255,
                    ResolveRateTextScale(rate, content.Width - 4));
            }
        }

        private static void DrawTrack(
            object spriteBatch,
            MapFootprintPlaybackLayout layout,
            MapFootprintPlaybackSnapshot playback,
            MapFootprintPlaybackHitTest hit)
        {
            var track = layout.Track;
            var hovered = hit != null && string.Equals(hit.Target, MapFootprintPlaybackHitTargets.Track, StringComparison.Ordinal);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, track.X - 1, track.Y - 1, track.Width + 2, track.Height + 2, 6, 92, 116, 142, 210);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, track.X, track.Y, track.Width, track.Height, 6, 16, 24, 34, 210);
            var filled = CalculateTrackFilledWidth(track, playback);
            if (filled > 0)
            {
                UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, track.X, track.Y, filled, track.Height, 6, 48, 255, 96, hovered || playback.Dragging ? 235 : 210);
            }

            var knobX = track.X + filled - layout.KnobSize / 2;
            knobX = Math.Max(track.X - layout.KnobSize / 2, Math.Min(track.Right - layout.KnobSize / 2, knobX));
            var knobY = track.CenterY - layout.KnobSize / 2;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, knobX, knobY, layout.KnobSize, layout.KnobSize, layout.KnobSize / 2, 238, 246, 232, 245);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, knobX + 3, knobY + 3, layout.KnobSize - 6, layout.KnobSize - 6, Math.Max(1, layout.KnobSize / 2 - 3), 48, 255, 96, 230);
        }

        private static int CalculateTrackFilledWidth(LegacyUiRect track, MapFootprintPlaybackSnapshot playback)
        {
            if (playback == null || track.Width <= 0)
            {
                return 0;
            }

            var duration = Math.Max(0L, playback.TimelineEndTicks - playback.TimelineStartTicks);
            if (duration <= 0L)
            {
                return track.Width;
            }

            var cursor = Math.Max(playback.TimelineStartTicks, Math.Min(playback.TimelineEndTicks, playback.CursorTicks));
            var fraction = (cursor - playback.TimelineStartTicks) / (double)duration;
            return Math.Max(0, Math.Min(track.Width, (int)Math.Round(track.Width * fraction)));
        }

        private static float ResolveRateTextScale(int rate, int width)
        {
            var label = rate.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x";
            if (UiTextRenderer.EstimateTextWidth(label, 0.58f) <= width)
            {
                return 0.58f;
            }

            return 0.50f;
        }

        private static void RecordOverlayDiagnostics(MapFootprintPlaybackInteraction interaction, string route)
        {
            var state = interaction == null ? null : interaction.State;
            PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                state == null ? "unavailable" : route ?? string.Empty,
                state == null ? "playback interaction unavailable" : state.Status,
                state == null ? string.Empty : state.PairId,
                state != null && state.Paused,
                state == null ? 1 : state.PlaybackRate,
                state == null ? 0L : state.CursorTicks,
                state == null ? 0L : state.TimelineStartTicks,
                state == null ? 0L : state.TimelineEndTicks,
                state != null && state.IsAtLatest,
                state != null && state.Dragging,
                interaction != null && interaction.MouseCaptured,
                interaction != null && interaction.BarHovered,
                state == null ? string.Empty : state.LastInteraction);
        }
    }
}
