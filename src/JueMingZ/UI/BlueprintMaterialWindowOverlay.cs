using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    public static class BlueprintMaterialWindowOverlay
    {
        private const string VisualContract = "aggregate-materials+main-inventory+void-bag+drag-opacity-close+mouse-consume";
        private static bool _lastLeftDown;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    return true;
                }

                var snapshot = BlueprintMaterialService.GetCachedSnapshotForDraw();
                var frame = BlueprintMaterialWindowState.BuildFrame(
                    snapshot,
                    Math.Max(1, TerrariaMainCompat.ScreenWidth),
                    Math.Max(1, TerrariaMainCompat.ScreenHeight),
                    -1,
                    -1);
                if (frame == null || !frame.Visible)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("BlueprintMaterialWindowOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawWindow(spriteBatch, frame);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintMaterialWindowOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-material-window-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintMaterialWindowOverlay",
                    "Blueprint material window overlay draw failed; exception swallowed.", error);
            }

            return true;
        }

        public static void UpdatePrefixGuard()
        {
            UpdateInputGuard("BlueprintMaterialWindowOverlay.UpdatePrefixGuard", true, false, null);
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            UpdateInputGuard("BlueprintMaterialWindowOverlay.UpdateAfterPlayerInputGuard", false, false, null);
        }

        public static bool ShouldSuppressHotbarScrollFromHook()
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    return false;
                }

                var raw = DiagnosticMouseStateReader.Read();
                var mouse = ReadOverlayMouse(raw);
                var frame = BuildFrame(mouse);
                if (!BlueprintMaterialWindowState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
                {
                    return false;
                }

                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(mouse.ScrollDelta);
                var rawScrollDelta = scroll == null ? 0 : scroll.EffectiveScrollDelta;
                if (rawScrollDelta == 0)
                {
                    return false;
                }

                BlueprintMaterialWindowState.HandleInput(
                    frame,
                    mouse.X,
                    mouse.Y,
                    mouse.LeftDown,
                    false,
                    false,
                    rawScrollDelta,
                    Math.Max(1, TerrariaMainCompat.ScreenWidth),
                    Math.Max(1, TerrariaMainCompat.ScreenHeight));
                UiMouseCaptureService.CaptureForOperationWindow();
                UiMouseCaptureService.ConsumeScrollForOperationWindow();
                TerrariaUiMouseCompat.MarkScrollHotbarHookSuppressed();
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("BlueprintMaterialWindowOverlay.ShouldSuppressHotbarScrollFromHook", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-material-window-scroll-hook-error",
                    TimeSpan.FromSeconds(10),
                    "BlueprintMaterialWindowOverlay",
                    "Blueprint material window scroll guard failed; exception swallowed.",
                    error);
                return false;
            }
        }

        internal static bool ShouldRegisterUiOverlayForTesting()
        {
            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        internal static BlueprintMaterialWindowFrame BuildFrameForTesting(BlueprintMaterialSnapshot snapshot, int screenWidth, int screenHeight, int mouseX, int mouseY)
        {
            return BlueprintMaterialWindowState.BuildFrame(snapshot, screenWidth, screenHeight, mouseX, mouseY);
        }

        internal static BlueprintMaterialWindowInteraction HandleInputForTesting(
            BlueprintMaterialWindowFrame frame,
            int mouseX,
            int mouseY,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            int rawScrollDelta,
            int screenWidth,
            int screenHeight)
        {
            return BlueprintMaterialWindowState.HandleInput(frame, mouseX, mouseY, leftDown, leftPressed, leftReleased, rawScrollDelta, screenWidth, screenHeight);
        }

        internal static bool ShouldCaptureMouseForTesting(BlueprintMaterialWindowFrame frame, int mouseX, int mouseY)
        {
            return BlueprintMaterialWindowState.ShouldCaptureMouse(frame, mouseX, mouseY);
        }

        internal static void ResetForTesting()
        {
            _lastLeftDown = false;
            BlueprintMaterialWindowState.ResetForTesting();
        }

        private static void UpdateInputGuard(string source, bool handleState, bool handleScroll, DiagnosticMouseState raw)
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    if (handleState)
                    {
                        _lastLeftDown = false;
                    }

                    return;
                }

                var rawState = raw ?? DiagnosticMouseStateReader.Read();
                var mouse = ReadOverlayMouse(rawState);
                var frame = BuildFrame(mouse);
                if (frame == null || !frame.Visible)
                {
                    _lastLeftDown = false;
                    return;
                }

                if (!handleState)
                {
                    if (BlueprintMaterialWindowState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
                    {
                        UiMouseCaptureService.CaptureForOperationWindow();
                        if (mouse.LeftDown)
                        {
                            UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
                        }
                    }

                    return;
                }

                var leftPressed = mouse.LeftDown && !_lastLeftDown;
                var leftReleased = !mouse.LeftDown && _lastLeftDown;
                var interaction = BlueprintMaterialWindowState.HandleInput(
                    frame,
                    mouse.X,
                    mouse.Y,
                    mouse.LeftDown,
                    leftPressed,
                    leftReleased,
                    0,
                    Math.Max(1, TerrariaMainCompat.ScreenWidth),
                    Math.Max(1, TerrariaMainCompat.ScreenHeight));
                _lastLeftDown = mouse.LeftDown;

                if (interaction == null || !interaction.CapturedMouse)
                {
                    return;
                }

                var preserveLeftHold = interaction.DragStarted || interaction.Dragging;
                if (preserveLeftHold)
                {
                    UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons();
                }
                else
                {
                    UiMouseCaptureService.CaptureForOperationWindow();
                }

                if (leftPressed || interaction.Closed || interaction.OpacityChanged || interaction.ScrollChanged)
                {
                    UiMouseCaptureService.ConsumeMouseTriggerForOperationWindow("MouseLeft", out _);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError(source, error);
                LogThrottle.ErrorThrottled(
                    "blueprint-material-window-input-guard-error",
                    TimeSpan.FromSeconds(10),
                    "BlueprintMaterialWindowOverlay",
                    "Blueprint material window input guard failed; exception swallowed.",
                    error);
            }
        }

        private static BlueprintMaterialWindowFrame BuildFrame(LegacyMouseSnapshot mouse)
        {
            mouse = mouse ?? new LegacyMouseSnapshot();
            return BlueprintMaterialWindowState.BuildFrame(
                BlueprintMaterialService.GetCachedSnapshotForDraw(),
                Math.Max(1, TerrariaMainCompat.ScreenWidth),
                Math.Max(1, TerrariaMainCompat.ScreenHeight),
                mouse.X,
                mouse.Y);
        }

        private static LegacyMouseSnapshot ReadOverlayMouse(DiagnosticMouseState raw)
        {
            return LegacyUiInput.ReadMouseForOverlay(raw, LegacyMainUiScale.Resolve(raw));
        }

        private static bool ShouldUseOverlay()
        {
            return BlueprintMaterialWindowState.Visible &&
                   !TerrariaMainCompat.IsInMainMenu &&
                   !TerrariaMainCompat.IsMapFullscreenOpen;
        }

        private static void DrawWindow(object spriteBatch, BlueprintMaterialWindowFrame frame)
        {
            var rect = frame.WindowRect;
            var alpha = ScaleAlpha(225, frame.OpacityPercent);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 6, 72, 88, 116, ScaleAlpha(230, frame.OpacityPercent));
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, 5, 26, 32, 46, alpha);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X + 6, rect.Y + 6, rect.Width - 12, frame.HeaderRect.Height - 6, 42, 54, 78, ScaleAlpha(216, frame.OpacityPercent));
            UiTextRenderer.DrawText(spriteBatch, "蓝图材料", rect.X + 12, rect.Y + 8, 246, 242, 220, ScaleAlpha(255, frame.OpacityPercent), 0.72f);
            DrawSmallButton(spriteBatch, frame.OpacityDownRect, "-", frame.OpacityPercent);
            DrawSmallButton(spriteBatch, frame.OpacityUpRect, "+", frame.OpacityPercent);
            DrawSmallButton(spriteBatch, frame.CloseRect, "x", frame.OpacityPercent);

            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                frame.SummaryLine ?? string.Empty,
                rect.X + 12,
                rect.Y + 40,
                rect.Width - 24,
                17,
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                206,
                224,
                238,
                ScaleAlpha(238, frame.OpacityPercent),
                0.60f);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                frame.MessageLine ?? string.Empty,
                rect.X + 12,
                rect.Y + 57,
                rect.Width - 24,
                16,
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                218,
                198,
                128,
                ScaleAlpha(230, frame.OpacityPercent),
                0.56f);

            var body = frame.BodyRect;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, body.X, body.Y, body.Width, body.Height, 4, 16, 20, 30, ScaleAlpha(142, frame.OpacityPercent));
            var items = frame.Items;
            var start = Math.Max(0, frame.FirstVisibleItemIndex);
            var max = Math.Min(items == null ? 0 : items.Count, start + frame.VisibleItemCount);
            if (items == null || items.Count <= 0)
            {
                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    "暂无缺失材料",
                    body.X + 8,
                    body.Y + 10,
                    body.Width - 16,
                    18,
                    body.X,
                    body.Y,
                    body.Width,
                    body.Height,
                    206,
                    214,
                    226,
                    ScaleAlpha(230, frame.OpacityPercent),
                    0.62f);
            }
            else
            {
                for (var index = start; index < max; index++)
                {
                    DrawMaterialRow(spriteBatch, body, frame, items[index], index - start);
                }
            }

            DrawSmallButton(spriteBatch, frame.ScrollUpRect, "^", frame.OpacityPercent);
            DrawSmallButton(spriteBatch, frame.ScrollDownRect, "v", frame.OpacityPercent);
        }

        private static void DrawMaterialRow(object spriteBatch, LegacyUiRect body, BlueprintMaterialWindowFrame frame, BlueprintMaterialItemSnapshot item, int rowIndex)
        {
            if (item == null)
            {
                return;
            }

            var y = body.Y + 6 + rowIndex * 23;
            if (y + 21 > body.Bottom)
            {
                return;
            }

            var rowAlpha = ScaleAlpha(rowIndex % 2 == 0 ? 84 : 58, frame.OpacityPercent);
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, body.X + 5, y, body.Width - 10, 21, 22, 28, 42, rowAlpha);
            var nameWidth = Math.Max(60, body.Width - 154);
            var name = UiTextRenderer.Ellipsize(item.DisplayName ?? string.Empty, nameWidth, 0.56f);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                name,
                body.X + 9,
                y + 4,
                nameWidth,
                14,
                body.X,
                body.Y,
                body.Width,
                body.Height,
                238,
                238,
                226,
                ScaleAlpha(246, frame.OpacityPercent),
                0.56f);
            var count = "需 " + item.RequiredStack + "  有 " + item.AvailableStack + "  缺 " + item.MissingStack;
            UiTextRenderer.DrawAlignedTextClipped(
                spriteBatch,
                count,
                body.Right - 140,
                y + 3,
                132,
                15,
                UiTextHorizontalAlignment.Right,
                body.X,
                body.Y,
                body.Width,
                body.Height,
                item.MissingStack > 0 ? 238 : 156,
                item.MissingStack > 0 ? 184 : 222,
                item.MissingStack > 0 ? 132 : 168,
                ScaleAlpha(238, frame.OpacityPercent),
                0.52f);
        }

        private static void DrawSmallButton(object spriteBatch, LegacyUiRect rect, string label, int opacityPercent)
        {
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 4, 88, 104, 136, ScaleAlpha(206, opacityPercent));
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, 3, 34, 42, 62, ScaleAlpha(210, opacityPercent));
            UiTextRenderer.DrawCenteredText(spriteBatch, label ?? string.Empty, rect.X, rect.Y - 1, rect.Width, rect.Height, 238, 238, 226, ScaleAlpha(245, opacityPercent), 0.62f);
        }

        private static int ScaleAlpha(int alpha, int opacityPercent)
        {
            return Math.Max(0, Math.Min(255, alpha * Math.Max(0, Math.Min(100, opacityPercent)) / 100));
        }
    }
}
