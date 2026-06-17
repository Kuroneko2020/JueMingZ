using System;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void DiagnosticMouseStateReaderReusesSnapshotWithinDrawFrame()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                ResetUiInputFrameTestState();
                Terraria.Main.mouseX = 100;
                Terraria.Main.mouseY = 120;

                UiInputFrameClock.BeginDrawFrame("test.draw");
                var first = DiagnosticMouseStateReader.Read();
                Terraria.Main.mouseX = 140;
                Terraria.Main.mouseY = 160;
                var second = DiagnosticMouseStateReader.Read();

                if (!object.ReferenceEquals(first, second))
                {
                    throw new InvalidOperationException("Expected DiagnosticMouseStateReader to reuse the same snapshot inside one draw frame.");
                }

                if (second.TerrariaMouseX != 100 || second.TerrariaMouseY != 120)
                {
                    throw new InvalidOperationException("Expected same-frame mouse reads to keep the first draw-frame coordinates.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                restoreRuntimeTypes();
            }
        }

        private static void DiagnosticMouseStateReaderRefreshesOnNewFastDrawFrame()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                ResetUiInputFrameTestState();
                Terraria.Main.mouseX = 10;
                Terraria.Main.mouseY = 20;

                UiInputFrameClock.BeginDrawFrame("test.draw.1");
                var first = DiagnosticMouseStateReader.Read();
                Terraria.Main.mouseX = 30;
                Terraria.Main.mouseY = 40;
                UiInputFrameClock.BeginDrawFrame("test.draw.2");
                var second = DiagnosticMouseStateReader.Read();

                if (object.ReferenceEquals(first, second))
                {
                    throw new InvalidOperationException("Expected a new draw frame to force a new mouse snapshot.");
                }

                if (second.TerrariaMouseX != 30 || second.TerrariaMouseY != 40)
                {
                    throw new InvalidOperationException("Expected a new draw frame to refresh mouse coordinates even without a wall-clock delay.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                restoreRuntimeTypes();
            }
        }

        private static void DiagnosticMouseStateReaderRefreshesWhenDrawFrameChangesUnderSameUpdate()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                ResetUiInputFrameTestState();
                Terraria.Main.GameUpdateCount = 77;
                Terraria.Main.mouseX = 200;
                Terraria.Main.mouseY = 210;

                UiInputFrameClock.BeginDrawFrame("test.same-update.1");
                DiagnosticMouseStateReader.Read();
                Terraria.Main.mouseX = 220;
                Terraria.Main.mouseY = 230;
                UiInputFrameClock.BeginDrawFrame("test.same-update.2");
                var refreshed = DiagnosticMouseStateReader.Read();

                if (refreshed.TerrariaMouseX != 220 || refreshed.TerrariaMouseY != 230)
                {
                    throw new InvalidOperationException("Expected DrawFrameId changes to refresh mouse reads even when GameUpdateCount is unchanged.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                restoreRuntimeTypes();
            }
        }

        private static void UiMouseCaptureServiceShortCircuitsWithinDrawFrame()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetUiInputFrameTestState();
                SetFakeTerrariaMouseText("vanilla");

                UiInputFrameClock.BeginDrawFrame("test.capture.same-frame");
                var first = UiMouseCaptureService.CaptureForOperationWindow();
                if (!first || !Terraria.Main.mouseInterface || !Terraria.Main.blockMouse || Terraria.Main.mouseText)
                {
                    throw new InvalidOperationException("Expected first capture call to mark Terraria UI mouse capture and suppress mouse text.");
                }

                Terraria.Main.mouseInterface = false;
                Terraria.Main.blockMouse = false;
                SetFakeTerrariaMouseText("restored-by-terraria");
                var second = UiMouseCaptureService.CaptureForOperationWindow();
                if (!second)
                {
                    throw new InvalidOperationException("Expected same-frame capture cache to report the previous capture success.");
                }

                if (Terraria.Main.mouseInterface || Terraria.Main.blockMouse || !Terraria.Main.mouseText ||
                    !string.Equals(Terraria.Main.hoverItemName, "restored-by-terraria", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected same draw-frame capture calls to short-circuit without rewriting Terraria mouse fields.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static void UiMouseCaptureServiceRewritesCaptureAndSuppressOnNextDrawFrame()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetUiInputFrameTestState();
                SetFakeTerrariaMouseText("vanilla");

                UiInputFrameClock.BeginDrawFrame("test.capture.frame-1");
                if (!UiMouseCaptureService.CaptureForOperationWindow())
                {
                    throw new InvalidOperationException("Expected initial capture to succeed.");
                }

                Terraria.Main.mouseInterface = false;
                Terraria.Main.blockMouse = false;
                SetFakeTerrariaMouseText("cleared-between-frames");
                UiInputFrameClock.BeginDrawFrame("test.capture.frame-2");
                if (!UiMouseCaptureService.CaptureForOperationWindow())
                {
                    throw new InvalidOperationException("Expected next draw frame capture to succeed.");
                }

                if (!Terraria.Main.mouseInterface || !Terraria.Main.blockMouse || Terraria.Main.mouseText ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName) ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName2))
                {
                    throw new InvalidOperationException("Expected next draw frame capture to rewrite capture fields and suppress mouse text.");
                }

                SetFakeTerrariaMouseText("mouse-text-cleared-again");
                UiInputFrameClock.BeginDrawFrame("test.suppress.frame-3");
                if (!UiMouseCaptureService.SuppressMouseTextForOperationWindow())
                {
                    throw new InvalidOperationException("Expected next draw frame suppress call to succeed while capture remains active.");
                }

                if (Terraria.Main.mouseText ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName) ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName2))
                {
                    throw new InvalidOperationException("Expected next draw frame suppress call to clear Terraria mouse text again.");
                }
            }
            finally
            {
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static void UiMouseCaptureServiceClearsPendingMouseTextAndNpcHover()
        {
            var restoreRuntimeTypes = PushUiMouseCompatMainType(typeof(FakePendingMouseTextMain));
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetUiInputFrameTestState();
                FakePendingMouseTextMain.Reset();

                UiInputFrameClock.BeginDrawFrame("test.pending-mouse-text");
                if (!UiMouseCaptureService.SuppressPendingMouseTextForOperationWindow())
                {
                    throw new InvalidOperationException("Expected pending MouseText suppression to find writable fake Terraria members.");
                }

                var cache = FakePendingMouseTextMain.instance.MouseTextCacheForTesting;
                if (FakePendingMouseTextMain.mouseText ||
                    FakePendingMouseTextMain.HoveringOverAnNPC ||
                    !string.IsNullOrEmpty(FakePendingMouseTextMain.hoverItemName) ||
                    !string.IsNullOrEmpty(FakePendingMouseTextMain.hoverItemName2) ||
                    cache.isValid ||
                    cache.noOverride ||
                    !string.IsNullOrEmpty(cache.cursorText) ||
                    !string.IsNullOrEmpty(cache.buffTooltip) ||
                    FakePendingMouseTextMain.instance.mouseNPCIndex != -1 ||
                    FakePendingMouseTextMain.instance.mouseNPCType != -1 ||
                    FakePendingMouseTextMain.instance.currentNPCShowingChatBubble != -1)
                {
                    throw new InvalidOperationException("Expected pending MouseText, hover names, and NPC hover state to be cleared.");
                }
            }
            finally
            {
                FakePendingMouseTextMain.Reset();
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static void LegacyMouseTextGuardSuppressesInsideF5Only()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                ResetUiInputFrameTestState();
                LegacyMainUiState.SetWindow(20, 20, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight, false);
                LegacyMainUiState.SetVisible(true);

                Terraria.Main.mouseX = 40;
                Terraria.Main.mouseY = 40;
                SetFakeTerrariaMouseText("inside-f5");
                LegacyMainWindow.DrawMouseTextGuardLayer();
                if (Terraria.Main.mouseText ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName) ||
                    !string.IsNullOrEmpty(Terraria.Main.hoverItemName2))
                {
                    throw new InvalidOperationException("Expected final MouseText guard to suppress vanilla hover while the mouse is inside F5.");
                }

                SetFakeTerrariaMouseText("outside-f5");
                Terraria.Main.mouseX = 1200;
                Terraria.Main.mouseY = 760;
                LegacyMainWindow.DrawMouseTextGuardLayer();
                if (!Terraria.Main.mouseText ||
                    !string.Equals(Terraria.Main.hoverItemName, "outside-f5", StringComparison.Ordinal) ||
                    !string.Equals(Terraria.Main.hoverItemName2, "outside-f5", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected final MouseText guard to leave vanilla hover intact outside F5.");
                }
            }
            finally
            {
                LegacyMainUiState.SetVisible(false);
                ResetUiInputFrameTestState();
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                restoreRuntimeTypes();
            }
        }

        private static void LegacyMainF5HotkeyEdgeTracksPhysicalPressAcrossGates()
        {
            var now = new DateTime(2026, 6, 15, 6, 30, 0, DateTimeKind.Utc);
            var accepted = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, false, true, false, true, now, DateTime.MinValue);
            AssertF5Decision(accepted, true, "pressed", true, false, 0, true);

            var held = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, true, true, false, true, now.AddMilliseconds(16), now);
            AssertF5Decision(held, false, "held", true, true, -1, false);

            var blockedMap = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, false, true, true, true, now.AddMilliseconds(32), DateTime.MinValue);
            AssertF5Decision(blockedMap, false, "mapFullscreen", true, false, 0, true);

            var heldAfterMapCloses = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, blockedMap.NextWasDown, true, false, true, now.AddMilliseconds(48), DateTime.MinValue);
            AssertF5Decision(heldAfterMapCloses, false, "held", true, true, 0, false);

            var blockedInput = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, false, false, false, true, now.AddSeconds(1), DateTime.MinValue);
            AssertF5Decision(blockedInput, false, "gameInputUnavailable", true, false, 0, true);

            var heldAfterInputReturns = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, blockedInput.NextWasDown, true, false, true, now.AddSeconds(2), DateTime.MinValue);
            AssertF5Decision(heldAfterInputReturns, false, "held", true, true, 0, false);

            var released = DebugHotkeyService.EvaluateF5HotkeyForTesting(false, true, true, false, true, now.AddSeconds(3), now);
            AssertF5Decision(released, false, "released", false, true, 0, true);

            var rapidRepress = DebugHotkeyService.EvaluateF5HotkeyForTesting(true, false, true, false, true, now.AddMilliseconds(80), now);
            AssertF5Decision(rapidRepress, true, "pressed", true, false, 0, true);
        }

        private static void LegacyMainFullscreenMapOpenClosesF5WithoutLatentInteraction()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                ResetUiInputFrameTestState();
                LegacyMainUiState.SetVisible(true);
                LegacyTextInput.Focus("test-text", "abc");
                LegacyHexColorInput.Focus("test-color", "#112233");

                if (!LegacyMainUiState.Visible || !LegacyUiInput.IsActiveInteraction())
                {
                    throw new InvalidOperationException("Expected F5 and text inputs to start visible and active.");
                }

                if (LegacyMainUiState.HideIfFullscreenMapOpen("test.no-map", false))
                {
                    throw new InvalidOperationException("F5 must not close when fullscreen map is not open.");
                }

                if (!LegacyMainUiState.Visible || !LegacyUiInput.IsActiveInteraction())
                {
                    throw new InvalidOperationException("F5 state changed before fullscreen map opened.");
                }

                if (!LegacyMainUiState.HideIfFullscreenMapOpen("test.map", true))
                {
                    throw new InvalidOperationException("Expected fullscreen map gate to close the visible F5 window.");
                }

                if (LegacyMainUiState.Visible || LegacyUiInput.IsActiveInteraction() || LegacyTextInput.IsAnyFocused || LegacyHexColorInput.IsAnyFocused)
                {
                    throw new InvalidOperationException("Fullscreen map close must clear F5 visibility and text interaction state.");
                }

                if (LegacyMainUiState.HideIfFullscreenMapOpen("test.map.again", true))
                {
                    throw new InvalidOperationException("Closing an already hidden F5 window should not report another close.");
                }
            }
            finally
            {
                LegacyMainUiState.SetVisible(false);
                ResetUiInputFrameTestState();
                restoreRuntimeTypes();
            }
        }

        private static void DiagnosticSnapshotWritesLegacyMainF5HotkeyState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                LegacyMainUiLastF5HotkeyDecision = "skipped",
                LegacyMainUiLastF5HotkeyReason = "gameInputUnavailable",
                LegacyMainUiLastF5HotkeyDown = true,
                LegacyMainUiLastF5HotkeyWasDown = false,
                LegacyMainUiLastF5HotkeyDebounceRemainingMs = 123,
                LegacyMainUiLastF5HotkeyUtc = new DateTime(2026, 6, 15, 6, 31, 0, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyDecision\": \"skipped\"");
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyReason\": \"gameInputUnavailable\"");
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyDown\": true");
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyWasDown\": false");
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyDebounceRemainingMs\": 123");
            AssertContains(json, "\"LegacyMainUiLastF5HotkeyUtc\": \"2026-06-15T06:31:00.0000000Z\"");
        }

        private static void LegacyUiUpdatePrefixSkipsScrollSnapshotWhenWheelIdle()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            try
            {
                ResetUiInputFrameTestState();
                LegacyUiInput.ResetActionUpdateGateStateForTesting();
                LegacyMainUiState.SetWindow(40, 80, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight, false);
                LegacyMainUiState.SetVisible(true);
                Terraria.Main.mouseX = 60;
                Terraria.Main.mouseY = 100;
                Terraria.Main.mouseScrollWheel = 0;
                Terraria.Main.oldMouseScrollWheel = 0;

                UiInputFrameClock.BeginUpdateFrame("test.legacy-scroll-prefix-idle");
                LegacyUiInput.UpdatePrefixGuard();

                if (LegacyUiInput.ScrollSnapshotSkippedCount != 1)
                {
                    throw new InvalidOperationException("Expected Legacy UI prefix guard to skip the full scroll snapshot when the wheel is idle inside F5.");
                }

                if (LegacyUiInput.WheelConsumedThisFrame)
                {
                    throw new InvalidOperationException("Expected an idle wheel frame to leave the Legacy UI wheel-consumed flag clear.");
                }
            }
            finally
            {
                LegacyMainUiState.SetVisible(false);
                LegacyUiInput.ResetActionUpdateGateStateForTesting();
                ResetUiInputFrameTestState();
                restoreRuntimeTypes();
            }
        }

        private static void LegacyUiScrollActionEventCoalescesStableWheelDiagnostics()
        {
            LegacyUiInput.ResetActionUpdateGateStateForTesting();

            if (!LegacyUiInput.ShouldRecordScrollActionEventForTesting("fishing", true, 12, 24))
            {
                throw new InvalidOperationException("Expected first stable scroll diagnostic event to be recorded.");
            }

            if (LegacyUiInput.ShouldRecordScrollActionEventForTesting("fishing", true, 12, 24))
            {
                throw new InvalidOperationException("Expected immediate duplicate stable scroll diagnostic event to be coalesced.");
            }

            if (LegacyUiInput.ScrollEventCoalescedCount != 1)
            {
                throw new InvalidOperationException("Expected scroll event coalescing diagnostics to count the duplicate event.");
            }

            if (!LegacyUiInput.ShouldRecordScrollActionEventForTesting("fishing", false, 12, 24))
            {
                throw new InvalidOperationException("Expected changed hotbar suppression status to record a fresh scroll diagnostic event.");
            }
        }

        private static void LegacyMainUiScaleKeepsHighUiScaleWhenScreenFits()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(1.3d, 2560, 1440);
            var visualHeight = LegacyUiMetrics.DefaultHeight * scale.EffectiveScaleY;

            if (Math.Abs(scale.DrawScaleX - 1d) > 0.001d ||
                Math.Abs(scale.DrawScaleY - 1d) > 0.001d ||
                Math.Abs(scale.EffectiveScaleY - 1.3d) > 0.001d ||
                Math.Abs(visualHeight - 975d) > 0.001d ||
                scale.Capped)
            {
                throw new InvalidOperationException("Expected 2560x1440 at 130% Terraria UI scale to keep the F5 window following UI scale without capping.");
            }
        }

        private static void LegacyMainUiScaleCapsHighUiScaleOnlyToScreenFit()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(1.25d, 1536, 864);
            var visualHeight = LegacyUiMetrics.DefaultHeight * scale.EffectiveScaleY;
            var expectedEffectiveScale = (864d - LegacyUiMetrics.VisualScreenMargin) / LegacyUiMetrics.DefaultHeight;

            if (Math.Abs(scale.EffectiveScaleY - expectedEffectiveScale) > 0.001d ||
                Math.Abs(scale.DrawScaleY - expectedEffectiveScale / 1.25d) > 0.001d ||
                Math.Abs(visualHeight - (864d - LegacyUiMetrics.VisualScreenMargin)) > 0.001d ||
                scale.EffectiveScaleY <= 1d)
            {
                throw new InvalidOperationException("Expected 1536x864 at 125% Terraria UI scale to cap only to the screen fit limit, not to the default 750px height.");
            }

            if (!scale.Capped)
            {
                throw new InvalidOperationException("Expected the F5 scale snapshot to report capped=true.");
            }

            scale = LegacyMainUiScale.ResolveForTesting(1.5d, 1280, 720);
            visualHeight = LegacyUiMetrics.DefaultHeight * scale.EffectiveScaleY;
            expectedEffectiveScale = (720d - LegacyUiMetrics.VisualScreenMargin) / LegacyUiMetrics.DefaultHeight;
            if (Math.Abs(scale.EffectiveScaleY - expectedEffectiveScale) > 0.001d ||
                Math.Abs(scale.DrawScaleY - expectedEffectiveScale / 1.5d) > 0.001d ||
                Math.Abs(visualHeight - (720d - LegacyUiMetrics.VisualScreenMargin)) > 0.001d ||
                !scale.Capped)
            {
                throw new InvalidOperationException("Expected 1280x720 at 150% Terraria UI scale to shrink the F5 window only enough to fit the screen.");
            }
        }

        private static void LegacyMainUiScaleKeepsSubDefaultUiScale()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(0.8d, 1536, 864);

            if (Math.Abs(scale.DrawScaleX - 1d) > 0.001d ||
                Math.Abs(scale.DrawScaleY - 1d) > 0.001d ||
                Math.Abs(scale.EffectiveScaleY - 0.8d) > 0.001d ||
                scale.Capped)
            {
                throw new InvalidOperationException("Expected sub-100% Terraria UI scale to pass through unchanged for the F5 window.");
            }
        }

        private static void LegacyMainUiDragBoundsKeepTitleRecoverable()
        {
            var fullVisibleY = LegacyMainUiState.CalculateMaxBasePositionForTesting(
                1440,
                LegacyUiMetrics.DefaultHeight,
                1.3d);
            var recoverableY = LegacyMainUiState.CalculateMaxRecoverableBasePositionForTesting(
                1440,
                LegacyUiMetrics.DefaultHeight,
                1.3d,
                LegacyUiMetrics.DragRecoverableVisibleHeight);

            if (fullVisibleY != 351)
            {
                throw new InvalidOperationException("Expected 2560x1440 at 130% full-visible F5 Y range to stay small because the scaled window is tall.");
            }

            if (recoverableY <= fullVisibleY + 600 || recoverableY != 1067)
            {
                throw new InvalidOperationException("Expected drag bounds to keep the title recoverable without locking the whole scaled F5 window into a narrow top band.");
            }

            var fullVisibleX = LegacyMainUiState.CalculateMaxBasePositionForTesting(
                2560,
                LegacyUiMetrics.DefaultWidth,
                1.3d);
            var recoverableX = LegacyMainUiState.CalculateMaxRecoverableBasePositionForTesting(
                2560,
                LegacyUiMetrics.DefaultWidth,
                1.3d,
                LegacyUiMetrics.DragRecoverableVisibleWidth);
            if (recoverableX <= fullVisibleX || recoverableX != 1867)
            {
                throw new InvalidOperationException("Expected drag bounds to allow more horizontal movement while keeping a recoverable strip visible.");
            }
        }

        private static void UiDrawTransformScalesRectanglesAndTextScale()
        {
            int x;
            int y;
            int width;
            int height;
            UiDrawTransform.TransformRectangleForTesting(10, 20, 100, 50, out x, out y, out width, out height);
            if (x != 10 || y != 20 || width != 100 || height != 50)
            {
                throw new InvalidOperationException("Expected inactive draw transform to leave rectangles unchanged.");
            }

            using (UiDrawTransform.Begin(0.8f, 0.5f))
            {
                UiDrawTransform.TransformRectangleForTesting(10, 20, 100, 50, out x, out y, out width, out height);
                if (x != 8 || y != 10 || width != 80 || height != 25)
                {
                    throw new InvalidOperationException("Expected draw transform to scale rectangle position and size.");
                }

                if (Math.Abs(UiDrawTransform.TransformScaleForTesting(0.9f) - 0.45f) > 0.001f)
                {
                    throw new InvalidOperationException("Expected text scale to use the active draw transform.");
                }
            }

            using (UiDrawTransform.Begin(0.4f, 0.4f))
            {
                UiDrawTransform.TransformRectangleForTesting(10, 20, 1, 1, out x, out y, out width, out height);
                if (width != 1 || height != 1)
                {
                    throw new InvalidOperationException("Expected active draw transform to preserve visible one-pixel UI edges.");
                }
            }

            if (UiDrawTransform.ActiveForTesting)
            {
                throw new InvalidOperationException("Expected draw transform scope to restore the previous state on dispose.");
            }
        }

        private static void ResetUiInputFrameTestState()
        {
            UiInputFrameClock.ResetForTesting();
            DiagnosticMouseStateReader.ResetForTesting();
            UiMouseCaptureService.InvalidateCache();
            TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();
            Terraria.Main.mouseX = 0;
            Terraria.Main.mouseY = 0;
            Terraria.Main.mouseLeft = false;
            Terraria.Main.mouseLeftRelease = false;
            Terraria.Main.mouseRight = false;
            Terraria.Main.mouseRightRelease = false;
            Terraria.Main.mouseInterface = false;
            Terraria.Main.blockMouse = false;
            Terraria.Main.mouseText = false;
            Terraria.Main.hoverItemName = string.Empty;
            Terraria.Main.hoverItemName2 = string.Empty;
            Terraria.Main.HoverItem = null;
            Terraria.Main.hoverItem = null;
            Terraria.Main.mouseScrollWheel = 0;
            Terraria.Main.oldMouseScrollWheel = 0;
            Terraria.Main.gameMenu = false;
            Terraria.Main.ingameOptionsWindow = false;
            Terraria.Main.inFancyUI = false;
            Terraria.Main.gamePaused = false;
            Terraria.Main.netMode = 0;
            Terraria.Main.dedServ = false;
            Terraria.Main.screenWidth = 1280;
            Terraria.Main.screenHeight = 800;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = false;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseRight = false;
            Terraria.GameInput.PlayerInput.Triggers.Current.MouseMiddle = false;
            Terraria.GameInput.PlayerInput.Triggers.Current.Mouse4 = false;
            Terraria.GameInput.PlayerInput.Triggers.Current.Mouse5 = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseRight = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseMiddle = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse4 = false;
            Terraria.GameInput.PlayerInput.Triggers.JustPressed.Mouse5 = false;
        }

        private static void AssertF5Decision(
            F5HotkeyDecision decision,
            bool shouldToggle,
            string reason,
            bool down,
            bool wasDown,
            int debounceRemainingMs,
            bool recordDiagnostic)
        {
            if (decision == null ||
                decision.ShouldToggle != shouldToggle ||
                !string.Equals(decision.Reason, reason, StringComparison.Ordinal) ||
                decision.Down != down ||
                decision.WasDown != wasDown ||
                (debounceRemainingMs >= 0 && decision.DebounceRemainingMs != debounceRemainingMs) ||
                decision.RecordDiagnostic != recordDiagnostic)
            {
                throw new InvalidOperationException(
                    "Unexpected F5 hotkey decision: expected " +
                    reason +
                    ", got " +
                    (decision == null ? "<null>" : decision.Reason) +
                    ".");
            }
        }

        private static Action PushUiMouseCompatMainType(Type mainType)
        {
            var runtimeMainField = typeof(TerrariaRuntimeTypes).GetField("_mainType", BindingFlags.Static | BindingFlags.NonPublic);
            if (runtimeMainField == null)
            {
                throw new InvalidOperationException("Terraria runtime type cache field missing.");
            }

            var previousRuntimeMain = runtimeMainField.GetValue(null);
            runtimeMainField.SetValue(null, mainType);
            TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();
            return () =>
            {
                runtimeMainField.SetValue(null, previousRuntimeMain);
                TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();
            };
        }

        private static void SetFakeTerrariaMouseText(string value)
        {
            Terraria.Main.mouseText = true;
            Terraria.Main.hoverItemName = value;
            Terraria.Main.hoverItemName2 = value;
            Terraria.Main.HoverItem = new object();
            Terraria.Main.hoverItem = new object();
        }

        private sealed class FakePendingMouseTextMain
        {
            public static bool mouseText;
            public static bool HoveringOverAnNPC;
            public static string hoverItemName;
            public static string hoverItemName2;
            public static object HoverItem;
            public static object hoverItem;
            public static FakePendingMouseTextMain instance;

            private MouseTextCache _mouseTextCache;

            public int mouseNPCIndex;
            public int mouseNPCType;
            public int currentNPCShowingChatBubble;

            public FakePendingMouseTextMain()
            {
                _mouseTextCache = new MouseTextCache
                {
                    noOverride = true,
                    isValid = true,
                    cursorText = "npc name",
                    buffTooltip = "npc tooltip"
                };
                mouseNPCIndex = 7;
                mouseNPCType = 22;
                currentNPCShowingChatBubble = 7;
            }

            public MouseTextCache MouseTextCacheForTesting
            {
                get { return _mouseTextCache; }
            }

            public static void Reset()
            {
                mouseText = true;
                HoveringOverAnNPC = true;
                hoverItemName = "npc name";
                hoverItemName2 = "npc name 2";
                HoverItem = new object();
                hoverItem = new object();
                instance = new FakePendingMouseTextMain();
            }

            public struct MouseTextCache
            {
                public bool noOverride;
                public bool isValid;
                public string cursorText;
                public string buffTooltip;
            }
        }
    }
}
