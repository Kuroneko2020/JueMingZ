using System;
using JueMingZ.Compat;
using JueMingZ.Config;
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

        private static void LegacyMainUiScaleCapsHighUiScaleToDefaultVisualHeight()
        {
            var scale = LegacyMainUiScale.ResolveForTesting(1.25d, 1536, 864);
            var visualHeight = LegacyUiMetrics.DefaultHeight * scale.EffectiveScaleY;

            if (Math.Abs(scale.DrawScaleY - 0.8d) > 0.001d ||
                Math.Abs(scale.EffectiveScaleY - 1d) > 0.001d ||
                Math.Abs(visualHeight - LegacyUiMetrics.MaxVisualHeight) > 0.001d)
            {
                throw new InvalidOperationException("Expected 125% Terraria UI scale to cap the F5 visual height at the default 750px window height.");
            }

            if (!scale.Capped)
            {
                throw new InvalidOperationException("Expected the F5 scale snapshot to report capped=true.");
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
        }

        private static void SetFakeTerrariaMouseText(string value)
        {
            Terraria.Main.mouseText = true;
            Terraria.Main.hoverItemName = value;
            Terraria.Main.hoverItemName2 = value;
            Terraria.Main.HoverItem = new object();
            Terraria.Main.hoverItem = new object();
        }
    }
}
