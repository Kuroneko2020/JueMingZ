using System;
using System.Globalization;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI
{
    public static class UserNotesPinnedOverlay
    {
        private static readonly string[] MouseTriggerTokens =
        {
            "MouseLeft",
            "MouseRight",
            "MouseMiddle",
            "Mouse4",
            "Mouse5"
        };
        private static readonly object SyncRoot = new object();
        private static bool _wasLeftDown;
        private static bool? _shouldUseOverlayOverrideForTesting;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    return true;
                }

                var mouse = ReadOverlayMouse();
                var frame = BuildFrame(mouse);
                if (frame.Items.Count <= 0)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("UserNotesPinnedOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawFrame(spriteBatch, frame);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("UserNotesPinnedOverlay", error);
                LogThrottle.ErrorThrottled(
                    "user-notes-pinned-overlay-draw-error",
                    TimeSpan.FromSeconds(10),
                    "UserNotesPinnedOverlay",
                    "User notes pinned overlay draw failed; exception swallowed.",
                    error);
            }

            return true;
        }

        public static void UpdatePrefixGuard()
        {
            UpdateInputGuard("UserNotesPinnedOverlay.UpdatePrefixGuard", true, true, null);
        }

        public static void UpdateAfterPlayerInputGuard()
        {
            UpdateInputGuard("UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard", true, false, null);
        }

        public static bool ShouldSuppressHotbarScrollFromHook()
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    return false;
                }

                var mouse = ReadOverlayMouse();
                var frame = BuildFrame(mouse);
                if (ShouldLegacyWindowOwnMouse(mouse, frame) ||
                    !UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y))
                {
                    return false;
                }

                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(mouse.ScrollDelta);
                if (scroll == null || scroll.EffectiveScrollDelta == 0)
                {
                    return false;
                }

                var interaction = UserNotesPinnedOverlayState.HandleInput(
                    frame,
                    mouse.X,
                    mouse.Y,
                    mouse.LeftDown,
                    false,
                    false,
                    scroll.EffectiveScrollDelta,
                    SafeScreenWidth(),
                    SafeScreenHeight(),
                    PersistPinnedState);
                UiMouseCaptureService.CaptureForOperationWindow();
                UiMouseCaptureService.ConsumeScrollForOperationWindow();
                TerrariaUiMouseCompat.MarkScrollHotbarHookSuppressed();
                RecordInteraction("Ui.Notes.Wheel", interaction);
                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("UserNotesPinnedOverlay.ShouldSuppressHotbarScrollFromHook", error);
                LogThrottle.ErrorThrottled(
                    "user-notes-pinned-overlay-scroll-hook-error",
                    TimeSpan.FromSeconds(10),
                    "UserNotesPinnedOverlay",
                    "User notes pinned overlay hotbar scroll guard failed; exception swallowed.",
                    error);
                return false;
            }
        }

        internal static UserNotesPinnedOverlayFrame BuildFrameForTesting(UserNotesSnapshot snapshot, int screenWidth, int screenHeight, int mouseX, int mouseY)
        {
            return UserNotesPinnedOverlayState.BuildFrame(snapshot, screenWidth, screenHeight, mouseX, mouseY);
        }

        internal static UserNotePinnedState BuildInitialPinnedStateForTesting(UserNotesSnapshot snapshot, string noteId, LegacyUiRect anchor, int screenWidth, int screenHeight)
        {
            return UserNotesPinnedOverlayState.BuildInitialPinnedState(snapshot, noteId, anchor, screenWidth, screenHeight);
        }

        internal static UserNotesPinnedOverlayInteraction HandleInputForTesting(
            UserNotesPinnedOverlayFrame frame,
            int mouseX,
            int mouseY,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            int rawScrollDelta,
            int screenWidth,
            int screenHeight,
            Func<string, UserNotePinnedState, UserNotesOperationResult> persist)
        {
            return UserNotesPinnedOverlayState.HandleInput(frame, mouseX, mouseY, leftDown, leftPressed, leftReleased, rawScrollDelta, screenWidth, screenHeight, persist);
        }

        internal static void ResetForTesting()
        {
            UserNotesPinnedOverlayState.ResetForTesting();
            ResetTrackedMouseButtons();
            _shouldUseOverlayOverrideForTesting = null;
        }

        internal static LegacyMouseSnapshot ReadOverlayMouseForTesting(DiagnosticMouseState raw)
        {
            return LegacyUiInput.ReadMouseForInterfaceOverlay(raw);
        }

        internal static void UpdateInputGuardForTesting(string source, bool handleState, bool handleScroll, DiagnosticMouseState raw)
        {
            UpdateInputGuard(source, handleState, handleScroll, raw);
        }

        internal static void SetShouldUseOverlayOverrideForTesting(bool? value)
        {
            _shouldUseOverlayOverrideForTesting = value;
        }

        private static void UpdateInputGuard(string source, bool handleState, bool handleScroll, DiagnosticMouseState raw)
        {
            try
            {
                if (!ShouldUseOverlay())
                {
                    if (handleState)
                    {
                        ResetTrackedMouseButtons();
                    }

                    return;
                }

                var mouse = handleState ? ReadOverlayMouseForInput(raw) : ReadOverlayMouse(raw);
                var frame = BuildFrame(mouse);
                if (ShouldLegacyWindowOwnMouse(mouse, frame))
                {
                    return;
                }

                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(mouse.ScrollDelta);
                var rawScrollDelta = scroll == null ? 0 : scroll.EffectiveScrollDelta;
                if (handleState)
                {
                    var stateScrollDelta = handleScroll ? rawScrollDelta : 0;
                    var interaction = UserNotesPinnedOverlayState.HandleInput(
                        frame,
                        mouse.X,
                        mouse.Y,
                        mouse.LeftDown,
                        mouse.LeftPressed,
                        mouse.LeftReleased,
                        stateScrollDelta,
                        SafeScreenWidth(),
                        SafeScreenHeight(),
                        PersistPinnedState);
                    ApplyInputSuppression(frame, mouse, interaction, stateScrollDelta);
                    SuppressScrollOnly(frame, mouse, handleScroll ? 0 : rawScrollDelta);
                    RecordInteraction(ResolveInteractionScenario(interaction), interaction);
                    return;
                }

                if (UserNotesPinnedOverlayState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
                {
                    UiMouseCaptureService.CaptureForOperationWindow();
                    ConsumeMouseButtonsForUi();
                }

                if (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y))
                {
                    SuppressScrollOnly(frame, mouse, rawScrollDelta);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError(source, error);
                LogThrottle.ErrorThrottled(
                    "user-notes-pinned-overlay-input-guard-error",
                    TimeSpan.FromSeconds(10),
                    "UserNotesPinnedOverlay",
                    "User notes pinned overlay input guard failed; exception swallowed.",
                    error);
            }
        }

        private static void SuppressScrollOnly(UserNotesPinnedOverlayFrame frame, LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            if (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y))
            {
                UiMouseCaptureService.ConsumeScrollForOperationWindow();
            }
        }

        private static void ApplyInputSuppression(UserNotesPinnedOverlayFrame frame, LegacyMouseSnapshot mouse, UserNotesPinnedOverlayInteraction interaction, int rawScrollDelta)
        {
            if (interaction == null)
            {
                return;
            }

            if (interaction.CapturedMouse || UserNotesPinnedOverlayState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
            {
                UiMouseCaptureService.CaptureForOperationWindow();
                ConsumeMouseButtonsForUi();
            }

            if (interaction.ScrollConsumed || (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y)))
            {
                UiMouseCaptureService.ConsumeScrollForOperationWindow();
            }
        }

        private static void ConsumeMouseButtonsForUi()
        {
            for (var index = 0; index < MouseTriggerTokens.Length; index++)
            {
                string message;
                TerrariaUiMouseCompat.TryConsumeMouseTriggerInputOnceForUi(MouseTriggerTokens[index], out message);
            }
        }

        private static UserNotesOperationResult PersistPinnedState(string noteId, UserNotePinnedState state)
        {
            UserNoteSnapshot note;
            return UserNotesUiState.UpdatePinnedState(noteId, state, ResolvePersistScenario(state), out note);
        }

        private static string ResolvePersistScenario(UserNotePinnedState state)
        {
            if (state == null || !state.Pinned)
            {
                return "Ui.Notes.Unpin";
            }

            return "Ui.Notes.Pin";
        }

        private static string ResolveInteractionScenario(UserNotesPinnedOverlayInteraction interaction)
        {
            if (interaction == null)
            {
                return string.Empty;
            }

            if (interaction.ScrollConsumed)
            {
                return "Ui.Notes.Wheel";
            }

            if (interaction.DragSaved)
            {
                return "Ui.Notes.Drag";
            }

            if (interaction.OpacityChanged)
            {
                return "Ui.Notes.Opacity";
            }

            if (interaction.Unpinned)
            {
                return "Ui.Notes.Unpin";
            }

            return string.Empty;
        }

        private static void RecordInteraction(string scenario, UserNotesPinnedOverlayInteraction interaction)
        {
            if (interaction == null || string.IsNullOrWhiteSpace(scenario))
            {
                return;
            }

            if (string.Equals(scenario, "Ui.Notes.Wheel", StringComparison.Ordinal) && !interaction.ScrollConsumed)
            {
                return;
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "User notes pinned overlay interaction.",
                0,
                "{" +
                    "\"noteId\":\"" + EscapeJson(interaction.HitNoteId) + "\"," +
                    "\"bodyScrollBefore\":" + interaction.BodyScrollBefore.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"bodyScrollAfter\":" + interaction.BodyScrollAfter.ToString(CultureInfo.InvariantCulture) +
                "}",
                BuildOverlayStateJson(),
                interaction.ToVerificationJson(),
                "UI",
                "UserNotesPinnedOverlay",
                string.Empty,
                string.Empty);
        }

        private static UserNotesPinnedOverlayFrame BuildFrame(LegacyMouseSnapshot mouse)
        {
            var snapshot = UserNotesUiState.Snapshot;
            return UserNotesPinnedOverlayState.BuildFrame(
                snapshot,
                SafeScreenWidth(),
                SafeScreenHeight(),
                mouse == null ? -1 : mouse.X,
                mouse == null ? -1 : mouse.Y);
        }

        private static bool ShouldUseOverlay()
        {
            if (_shouldUseOverlayOverrideForTesting.HasValue)
            {
                return _shouldUseOverlayOverrideForTesting.Value;
            }

            return !TerrariaMainCompat.IsInMainMenu && !TerrariaMainCompat.IsMapFullscreenOpen;
        }

        private static bool ShouldLegacyWindowOwnMouse(LegacyMouseSnapshot mouse, UserNotesPinnedOverlayFrame frame)
        {
            return mouse != null &&
                   frame != null &&
                   !frame.Dragging &&
                   LegacyMainUiState.Visible &&
                   LegacyUiInput.IsMouseInWindow(mouse);
        }

        private static LegacyMouseSnapshot ReadOverlayMouse()
        {
            return ReadOverlayMouse(null);
        }

        private static LegacyMouseSnapshot ReadOverlayMouse(DiagnosticMouseState raw)
        {
            return ReadOverlayMouseForTesting(raw ?? DiagnosticMouseStateReader.Read());
        }

        private static LegacyMouseSnapshot ReadOverlayMouseForInput(DiagnosticMouseState raw)
        {
            var mouse = ReadOverlayMouse(raw);
            TrackMouseButtonEdges(mouse);
            return mouse;
        }

        private static void TrackMouseButtonEdges(LegacyMouseSnapshot mouse)
        {
            lock (SyncRoot)
            {
                if (mouse == null || !mouse.ReadAvailable)
                {
                    _wasLeftDown = false;
                    if (mouse != null)
                    {
                        mouse.LeftPressed = false;
                        mouse.LeftReleased = false;
                    }

                    return;
                }

                var leftDown = mouse.LeftDown;
                mouse.LeftPressed = leftDown && !_wasLeftDown;
                mouse.LeftReleased = !leftDown && _wasLeftDown;
                _wasLeftDown = leftDown;
            }
        }

        private static void ResetTrackedMouseButtons()
        {
            lock (SyncRoot)
            {
                _wasLeftDown = false;
            }
        }

        private static void DrawFrame(object spriteBatch, UserNotesPinnedOverlayFrame frame)
        {
            if (frame == null || frame.Items == null)
            {
                return;
            }

            for (var index = 0; index < frame.Items.Count; index++)
            {
                DrawItem(spriteBatch, frame.Items[index]);
            }
        }

        private static void DrawItem(object spriteBatch, UserNotesPinnedOverlayItem item)
        {
            if (item == null || item.Rect.Width <= 0 || item.Rect.Height <= 0)
            {
                return;
            }

            var backgroundAlpha = Clamp(item.OpacityPercent, 0, 100) * 2;
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.Rect.X, item.Rect.Y, item.Rect.Width, item.Rect.Height, 6, 96, 112, 132, 168);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.Rect.X + 1, item.Rect.Y + 1, item.Rect.Width - 2, item.Rect.Height - 2, 5, 18, 22, 30, backgroundAlpha);
            DrawBody(spriteBatch, item);
            if (item.Hovered)
            {
                DrawControls(spriteBatch, item);
            }
        }

        private static void DrawBody(object spriteBatch, UserNotesPinnedOverlayItem item)
        {
            var lines = item.BodyLines ?? new string[0];
            if (lines.Length <= 0)
            {
                lines = new[] { string.Empty };
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var lineY = item.BodyRect.Y + index * UserNotesPinnedOverlayState.LineHeight - item.BodyScrollOffset;
                if (lineY + UserNotesPinnedOverlayState.LineHeight < item.BodyRect.Y || lineY > item.BodyRect.Bottom)
                {
                    continue;
                }

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    lines[index],
                    item.BodyRect.X,
                    lineY,
                    item.BodyRect.Width,
                    UserNotesPinnedOverlayState.LineHeight,
                    item.BodyRect.X,
                    item.BodyRect.Y,
                    item.BodyRect.Width,
                    item.BodyRect.Height,
                    238,
                    236,
                    218,
                    246,
                    UserNotesPinnedOverlayState.BodyTextScale);
            }
        }

        private static void DrawControls(object spriteBatch, UserNotesPinnedOverlayItem item)
        {
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.ToolbarRect.X, item.ToolbarRect.Y, item.ToolbarRect.Width, item.ToolbarRect.Height, 5, 22, 28, 38, 178);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.DragHandleRect.X, item.DragHandleRect.Y + 3, item.DragHandleRect.Width, 3, 2, 226, 232, 220, 245);
            DrawControlButton(spriteBatch, item.DecreaseOpacityRect, "<");
            DrawControlButton(spriteBatch, item.IncreaseOpacityRect, ">");
            DrawControlButton(spriteBatch, item.CloseRect, "x");
        }

        private static void DrawControlButton(object spriteBatch, LegacyUiRect rect, string label)
        {
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 4, 96, 112, 146, 218);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, 3, 32, 38, 52, 220);
            UiTextRenderer.DrawCenteredText(spriteBatch, label, rect.X, rect.Y - 1, rect.Width, rect.Height, 244, 238, 212, 255, 0.58f);
        }

        private static string BuildOverlayStateJson()
        {
            var snapshot = UserNotesUiState.Snapshot;
            var pinned = 0;
            if (snapshot.Notes != null)
            {
                for (var index = 0; index < snapshot.Notes.Count; index++)
                {
                    var note = snapshot.Notes[index];
                    if (note != null && note.PinnedState != null && note.PinnedState.Pinned)
                    {
                        pinned++;
                    }
                }
            }

            return "{" +
                   "\"featureId\":\"information.user_notes\"," +
                   "\"pinnedCount\":" + pinned.ToString(CultureInfo.InvariantCulture) +
                   "}";
        }

        private static int SafeScreenWidth()
        {
            try
            {
                return TerrariaMainCompat.ScreenWidth;
            }
            catch
            {
                return 1280;
            }
        }

        private static int SafeScreenHeight()
        {
            try
            {
                return TerrariaMainCompat.ScreenHeight;
            }
            catch
            {
                return 720;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                max = min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
