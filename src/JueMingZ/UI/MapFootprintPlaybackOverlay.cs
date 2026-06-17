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
                var utcNow = DateTime.UtcNow;
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var visible = ShouldUseOverlay(Main.mapFullscreen, settings.MapFootprintsDisplayEnabled);
                var snapshot = MapFootprintRenderCache.GetSnapshot();
                MapFootprintPlaybackState.Advance(snapshot, visible, utcNow);
                if (!visible)
                {
                    return;
                }

                var frame = ReadFullscreenUiFrame(true);
                var mouse = frame.Mouse;
                var layout = MapFootprintPlaybackState.CalculateLayout(frame.ScreenWidth, frame.ScreenHeight);
                var beforeCapture = CaptureMouseState();
                var interaction = MapFootprintPlaybackState.HandleInput(layout, mouse, snapshot, visible, utcNow);
                ApplyInputCapture(interaction);
                var afterCapture = CaptureMouseState();
                RecordOverlayDiagnostics(interaction, "prefix");
                RecordPrefixInputDiagnostics(mouse, interaction, beforeCapture, afterCapture, utcNow);
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
                    playback.DisplayTimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    false,
                    false,
                    playback.LastInteraction);
                return;
            }

            UiInputFrameClock.BeginDrawFrame("MapFootprintPlaybackOverlay");
            var drawFrame = ReadFullscreenUiFrame(false);
            var drawMouse = drawFrame.Mouse;
            var drawLayout = MapFootprintPlaybackState.CalculateLayout(drawFrame.ScreenWidth, drawFrame.ScreenHeight);
            var drawHit = MapFootprintPlaybackState.HitTest(drawLayout, drawMouse.X, drawMouse.Y);
            RecordDrawInputDiagnostics(drawMouse, drawHit, DateTime.UtcNow);

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
                    playback.DisplayTimelineEndTicks,
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
                    playback.DisplayTimelineEndTicks,
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
                playback = MapFootprintPlaybackState.GetSnapshotForRender(renderSnapshot, visible, DateTime.UtcNow);
                DrawOverlay(spriteBatch, drawLayout, playback, drawHit);
                PlayerWorldFootprintDiagnostics.RecordPlaybackOverlay(
                    "ready",
                    "playback overlay ready",
                    playback.PairId,
                    playback.Paused,
                    playback.PlaybackRate,
                    playback.CursorTicks,
                    playback.TimelineStartTicks,
                    playback.TimelineEndTicks,
                    playback.DisplayTimelineEndTicks,
                    playback.IsAtLatest,
                    playback.Dragging,
                    drawHit.BarHovered || playback.Dragging,
                    drawHit.BarHovered,
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
                    playback.DisplayTimelineEndTicks,
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
                            playback.DisplayTimelineEndTicks,
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

        internal static MapFootprintFullscreenUiFrame BuildFullscreenUiFrameForTesting(
            DiagnosticMouseState raw,
            int screenWidth,
            int screenHeight,
            bool scaleScreenFromUiMatrix)
        {
            return BuildFullscreenUiFrame(raw, screenWidth, screenHeight, scaleScreenFromUiMatrix);
        }

        internal static int CalculateTrackFilledWidthForTesting(LegacyUiRect track, MapFootprintPlaybackSnapshot playback)
        {
            return CalculateTrackFilledWidth(track, playback);
        }

        internal static void ApplyInputCaptureForTesting(MapFootprintPlaybackInteraction interaction)
        {
            ApplyInputCapture(interaction);
        }

        private static bool ShouldUseOverlay(bool mapFullscreen, bool displayEnabled)
        {
            return mapFullscreen && displayEnabled;
        }

        private static MapFootprintFullscreenUiFrame ReadFullscreenUiFrame(bool scaleScreenFromUiMatrix)
        {
            return BuildFullscreenUiFrame(
                DiagnosticMouseStateReader.ReadForFullscreenMapOverlay(),
                TerrariaMainCompat.ScreenWidth,
                TerrariaMainCompat.ScreenHeight,
                scaleScreenFromUiMatrix);
        }

        private static MapFootprintFullscreenUiFrame BuildFullscreenUiFrame(
            DiagnosticMouseState raw,
            int screenWidth,
            int screenHeight,
            bool scaleScreenFromUiMatrix)
        {
            raw = raw ?? new DiagnosticMouseState();
            var scaleX = ResolveUiScale(raw.UiScaleX, raw.UiScale);
            var scaleY = ResolveUiScale(raw.UiScaleY, raw.UiScale);
            return new MapFootprintFullscreenUiFrame
            {
                Mouse = BuildFullscreenUiMouse(raw, scaleX, scaleY),
                ScreenWidth = ResolveFullscreenUiExtent(screenWidth, scaleX, raw.UiTranslateX, scaleScreenFromUiMatrix),
                ScreenHeight = ResolveFullscreenUiExtent(screenHeight, scaleY, raw.UiTranslateY, scaleScreenFromUiMatrix)
            };
        }

        private static LegacyMouseSnapshot BuildFullscreenUiMouse(DiagnosticMouseState raw, double scaleX, double scaleY)
        {
            raw = raw ?? new DiagnosticMouseState();
            var coordinate = ResolveFullscreenUiMouse(raw, scaleX, scaleY);
            var inputAvailable = raw.GameInputAvailable || raw.TerrariaReadAvailable;
            // Fullscreen map already owns vanilla UI input; when the global
            // automation gate is closed, trust only Terraria's in-process mouse
            // state and keep OS physical fallback disabled.
            var down = raw.GameInputAvailable
                ? raw.TerrariaLeftDown || raw.OsLeftDown
                : raw.TerrariaReadAvailable && raw.TerrariaLeftDown;
            var scrollDelta = raw.GameInputAvailable || raw.TerrariaScrollWheelAvailable
                ? raw.ScrollDelta
                : 0;
            return new LegacyMouseSnapshot
            {
                X = coordinate.X,
                Y = coordinate.Y,
                LeftDown = down,
                LeftPressed = down,
                LeftReleased = inputAvailable && !down,
                ScrollDelta = scrollDelta,
                ReadAvailable = raw.TerrariaReadAvailable || raw.OsReadAvailable,
                ReadMode = coordinate.Mode + "/FullscreenUi",
                WindowHit = false
            };
        }

        private static FullscreenMouseCoordinate ResolveFullscreenUiMouse(DiagnosticMouseState raw, double scaleX, double scaleY)
        {
            var scaleActive = raw.UiScaleAvailable &&
                              (Math.Abs(scaleX - 1d) > 0.01d ||
                               Math.Abs(scaleY - 1d) > 0.01d ||
                               Math.Abs(raw.UiTranslateX) > 0.01d ||
                               Math.Abs(raw.UiTranslateY) > 0.01d);
            var hasTerraria = raw.TerrariaReadAvailable && raw.TerrariaMouseX >= 0 && raw.TerrariaMouseY >= 0;
            var hasOs = raw.OsReadAvailable && raw.OsClientMouseX >= 0 && raw.OsClientMouseY >= 0;
            if (hasTerraria && hasOs && scaleActive)
            {
                var osLogicalX = ScreenToUiCoordinate(raw.OsClientMouseX, scaleX, raw.UiTranslateX);
                var osLogicalY = ScreenToUiCoordinate(raw.OsClientMouseY, scaleY, raw.UiTranslateY);
                var rawDistance = DistanceSquared(raw.TerrariaMouseX, raw.TerrariaMouseY, raw.OsClientMouseX, raw.OsClientMouseY);
                var logicalDistance = DistanceSquared(raw.TerrariaMouseX, raw.TerrariaMouseY, osLogicalX, osLogicalY);
                var close = CloseDistanceSquared(scaleX, scaleY);
                if (logicalDistance <= close && logicalDistance <= rawDistance)
                {
                    return new FullscreenMouseCoordinate(raw.TerrariaMouseX, raw.TerrariaMouseY, BuildMouseMode(raw, "TerrariaLogical"));
                }

                if (rawDistance <= close || rawDistance < logicalDistance)
                {
                    return new FullscreenMouseCoordinate(
                        ScreenToUiCoordinate(raw.TerrariaMouseX, scaleX, raw.UiTranslateX),
                        ScreenToUiCoordinate(raw.TerrariaMouseY, scaleY, raw.UiTranslateY),
                        BuildMouseMode(raw, "TerrariaScreenToUi"));
                }
            }

            if (hasTerraria)
            {
                return new FullscreenMouseCoordinate(raw.TerrariaMouseX, raw.TerrariaMouseY, BuildMouseMode(raw, "TerrariaRaw"));
            }

            if (hasOs)
            {
                if (scaleActive)
                {
                    return new FullscreenMouseCoordinate(
                        ScreenToUiCoordinate(raw.OsClientMouseX, scaleX, raw.UiTranslateX),
                        ScreenToUiCoordinate(raw.OsClientMouseY, scaleY, raw.UiTranslateY),
                        BuildMouseMode(raw, "OsClientScreenToUi"));
                }

                return new FullscreenMouseCoordinate(raw.OsClientMouseX, raw.OsClientMouseY, BuildMouseMode(raw, "OsClientRaw"));
            }

            return new FullscreenMouseCoordinate(-1, -1, BuildMouseMode(raw, "none"));
        }

        private static double ResolveUiScale(double axisScale, double fallbackScale)
        {
            if (axisScale > 0.01d)
            {
                return axisScale;
            }

            return fallbackScale > 0.01d ? fallbackScale : 1d;
        }

        private static int ResolveFullscreenUiExtent(int screenSize, double scale, double translate, bool scaleScreenFromUiMatrix)
        {
            var safeSize = Math.Max(1, screenSize);
            if (!scaleScreenFromUiMatrix || scale <= 0.01d || Math.Abs(scale - 1d) <= 0.01d)
            {
                return safeSize;
            }

            var scaled = (int)Math.Round((safeSize - translate) / scale);
            return scaled > 0 ? scaled : safeSize;
        }

        private static int ScreenToUiCoordinate(int value, double scale, double translate)
        {
            if (value < 0)
            {
                return value;
            }

            if (scale <= 0.01d)
            {
                return value;
            }

            return (int)Math.Round((value - translate) / scale);
        }

        private static int DistanceSquared(int ax, int ay, int bx, int by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return dx * dx + dy * dy;
        }

        private static int CloseDistanceSquared(double scaleX, double scaleY)
        {
            var scale = Math.Max(Math.Abs(scaleX), Math.Abs(scaleY));
            var tolerance = Math.Max(4, (int)Math.Ceiling(scale * 3d));
            return tolerance * tolerance;
        }

        private static string BuildMouseMode(DiagnosticMouseState raw, string source)
        {
            var mode = raw == null ? string.Empty : raw.ReadMode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mode))
            {
                mode = "none";
            }

            if (raw != null && !string.IsNullOrWhiteSpace(raw.UiScaleSource))
            {
                return mode + "/" + source + "/" + raw.UiScaleSource;
            }

            return mode + "/" + source;
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
                TerrariaUiMouseCompat.TryConsumeMouseTriggerInput("MouseLeft", out message);
            }

            TerrariaUiMouseCompat.TryMarkUiMouseCapture();
            if (interaction.ScrollConsumed)
            {
                TerrariaUiMouseCompat.TryConsumeUiScroll();
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

            var displayEnd = playback.DisplayTimelineEndTicks > 0L ? playback.DisplayTimelineEndTicks : playback.TimelineEndTicks;
            displayEnd = Math.Max(playback.TimelineStartTicks, Math.Min(playback.TimelineEndTicks, displayEnd));
            var duration = Math.Max(0L, displayEnd - playback.TimelineStartTicks);
            if (duration <= 0L)
            {
                return track.Width;
            }

            var cursor = Math.Max(playback.TimelineStartTicks, Math.Min(displayEnd, playback.CursorTicks));
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
                state == null ? 0L : state.DisplayTimelineEndTicks,
                state != null && state.IsAtLatest,
                state != null && state.Dragging,
                interaction != null && interaction.MouseCaptured,
                interaction != null && interaction.BarHovered,
                state == null ? string.Empty : state.LastInteraction);
        }

        private static void RecordPrefixInputDiagnostics(
            LegacyMouseSnapshot mouse,
            MapFootprintPlaybackInteraction interaction,
            PlaybackMouseCaptureState beforeCapture,
            PlaybackMouseCaptureState afterCapture,
            DateTime utcNow)
        {
            var data = new MapFootprintPlaybackPrefixInputDiagnosticsData();
            mouse = mouse ?? new LegacyMouseSnapshot();
            interaction = interaction ?? new MapFootprintPlaybackInteraction();
            beforeCapture = beforeCapture ?? new PlaybackMouseCaptureState();
            afterCapture = afterCapture ?? new PlaybackMouseCaptureState();
            data.HitTarget = interaction.HitTarget;
            data.MouseReadMode = mouse.ReadMode;
            data.MouseX = mouse.X;
            data.MouseY = mouse.Y;
            data.MouseReadAvailable = mouse.ReadAvailable;
            data.BarHovered = interaction.BarHovered;
            data.MouseCaptured = interaction.MouseCaptured;
            data.ClickConsumed = interaction.ClickConsumed;
            data.ScrollConsumed = interaction.ScrollConsumed;
            data.LeftDown = mouse.LeftDown;
            data.LeftPressed = interaction.LeftPressed;
            data.LeftReleased = interaction.LeftReleased;
            data.ScrollDelta = mouse.ScrollDelta;
            data.GameUpdateCount = ReadGameUpdateCount();
            data.MainMouseLeftBefore = beforeCapture.MainMouseLeft;
            data.MainMouseLeftAfter = afterCapture.MainMouseLeft;
            data.MainMouseLeftReleaseBefore = beforeCapture.MainMouseLeftRelease;
            data.MainMouseLeftReleaseAfter = afterCapture.MainMouseLeftRelease;
            data.MainMouseInterfaceBefore = beforeCapture.MainMouseInterface;
            data.MainMouseInterfaceAfter = afterCapture.MainMouseInterface;
            data.MainBlockMouseBefore = beforeCapture.MainBlockMouse;
            data.MainBlockMouseAfter = afterCapture.MainBlockMouse;
            data.PlayerMouseInterfaceBefore = beforeCapture.PlayerMouseInterface;
            data.PlayerMouseInterfaceAfter = afterCapture.PlayerMouseInterface;
            data.Utc = utcNow.ToUniversalTime();
            PlayerWorldFootprintDiagnostics.RecordPlaybackPrefixInput(data);
        }

        private static void RecordDrawInputDiagnostics(
            LegacyMouseSnapshot mouse,
            MapFootprintPlaybackHitTest hit,
            DateTime utcNow)
        {
            var current = CaptureMouseState();
            mouse = mouse ?? new LegacyMouseSnapshot();
            hit = hit ?? new MapFootprintPlaybackHitTest();
            PlayerWorldFootprintDiagnostics.RecordPlaybackDrawInput(new MapFootprintPlaybackDrawInputDiagnosticsData
            {
                HitTarget = hit.Target,
                MouseReadMode = mouse.ReadMode,
                MouseX = mouse.X,
                MouseY = mouse.Y,
                MouseReadAvailable = mouse.ReadAvailable,
                BarHovered = hit.BarHovered,
                MainMouseLeft = current.MainMouseLeft,
                MainMouseLeftRelease = current.MainMouseLeftRelease,
                MainMouseInterface = current.MainMouseInterface,
                MainBlockMouse = current.MainBlockMouse,
                PlayerMouseInterface = current.PlayerMouseInterface,
                GameUpdateCount = ReadGameUpdateCount(),
                Utc = utcNow.ToUniversalTime()
            });
        }

        private static PlaybackMouseCaptureState CaptureMouseState()
        {
            var current = TerrariaUiMouseCompat.ReadCaptureDiagnosticsSnapshot();
            current = current ?? new TerrariaUiMouseCaptureDiagnosticsSnapshot();
            return new PlaybackMouseCaptureState
            {
                MainMouseLeft = current.MainMouseLeft,
                MainMouseLeftRelease = current.MainMouseLeftRelease,
                MainMouseInterface = current.MainMouseInterface,
                MainBlockMouse = current.MainBlockMouse,
                PlayerMouseInterface = current.PlayerMouseInterface
            };
        }

        private static long ReadGameUpdateCount()
        {
            ulong updateCount;
            if (!TerrariaMainCompat.TryReadGameUpdateCount(out updateCount))
            {
                return 0L;
            }

            return updateCount > long.MaxValue ? long.MaxValue : (long)updateCount;
        }

        private sealed class PlaybackMouseCaptureState
        {
            public bool MainMouseLeft { get; set; }
            public bool MainMouseLeftRelease { get; set; }
            public bool MainMouseInterface { get; set; }
            public bool MainBlockMouse { get; set; }
            public bool PlayerMouseInterface { get; set; }
        }

        private sealed class FullscreenMouseCoordinate
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public string Mode { get; private set; }

            public FullscreenMouseCoordinate(int x, int y, string mode)
            {
                X = x;
                Y = y;
                Mode = mode ?? string.Empty;
            }
        }
    }

    internal sealed class MapFootprintFullscreenUiFrame
    {
        public LegacyMouseSnapshot Mouse { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }

        public MapFootprintFullscreenUiFrame()
        {
            Mouse = new LegacyMouseSnapshot();
            ScreenWidth = 1;
            ScreenHeight = 1;
        }
    }
}
