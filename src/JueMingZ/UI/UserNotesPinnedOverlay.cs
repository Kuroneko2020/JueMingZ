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
        private static PendingToolbarPress _pendingToolbarPress;
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
            UserNotesPinnedOverlayInputDiagnostics.ResetForTesting();
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

                var rawState = raw ?? DiagnosticMouseStateReader.Read();
                var mouse = handleState ? ReadOverlayMouseForInput(rawState) : ReadOverlayMouse(rawState);
                var frame = BuildFrame(mouse);
                var hitDiagnostics = UserNotesPinnedOverlayState.BuildHitDiagnostics(frame, mouse.X, mouse.Y);
                var legacyWindowOwnsMouse = ShouldLegacyWindowOwnMouse(mouse, frame);
                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(mouse.ScrollDelta);
                var rawScrollDelta = scroll == null ? 0 : scroll.EffectiveScrollDelta;
                if (handleState)
                {
                    TryApplyPendingToolbarPress(source, mouse, hitDiagnostics);
                }

                if (legacyWindowOwnsMouse)
                {
                    ClearPendingToolbarPressIfReleased(mouse);
                    UserNotesPinnedOverlayInputDiagnostics.Record(
                        source,
                        handleState,
                        handleScroll,
                        rawState,
                        mouse,
                        hitDiagnostics,
                        null,
                        null,
                        rawScrollDelta,
                        true,
                        "legacyWindowOwnsMouse");
                    return;
                }

                if (handleState)
                {
                    var interaction = UserNotesPinnedOverlayState.HandleInput(
                        frame,
                        mouse.X,
                        mouse.Y,
                        mouse.LeftDown,
                        mouse.LeftPressed,
                        mouse.LeftReleased,
                        rawScrollDelta,
                        SafeScreenWidth(),
                        SafeScreenHeight(),
                        PersistPinnedState);
                    var preserveLeftHold = ShouldPreserveLeftHoldForDrag(frame, interaction) ||
                                           ShouldPreserveLeftHoldForPendingDrag(source, rawState, mouse, frame, hitDiagnostics, interaction);
                    var suppression = ApplyInputSuppression(frame, mouse, interaction, rawScrollDelta, preserveLeftHold);
                    var scrollOnlySuppressed = SuppressScrollOnly(frame, mouse, interaction.ScrollConsumed ? 0 : rawScrollDelta);
                    if (scrollOnlySuppressed)
                    {
                        suppression.ScrollConsumeRequested = true;
                        suppression.ScrollConsumeSucceeded = true;
                    }

                    UpdatePendingToolbarPress(source, rawState, mouse, frame, hitDiagnostics, interaction);
                    RecordInteraction(ResolveInteractionScenario(interaction), interaction);
                    UserNotesPinnedOverlayInputDiagnostics.Record(
                        source,
                        handleState,
                        handleScroll,
                        rawState,
                        mouse,
                        hitDiagnostics,
                        interaction,
                        suppression,
                        rawScrollDelta,
                        false,
                        ResolveTraceResultCode(hitDiagnostics, interaction, suppression, rawScrollDelta));
                    return;
                }

                var captureOnlySuppression = new UserNotesPinnedOverlaySuppressionDiagnostics();
                if (UserNotesPinnedOverlayState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
                {
                    captureOnlySuppression.MouseCaptureRequested = true;
                    captureOnlySuppression.MouseCaptureSucceeded = UiMouseCaptureService.CaptureForOperationWindow();
                    captureOnlySuppression.ButtonConsumeRequested = true;
                    string buttonConsumeMessage;
                    captureOnlySuppression.ButtonConsumeSucceeded = ConsumeMouseButtonsForUi(out buttonConsumeMessage);
                    captureOnlySuppression.ButtonConsumeMessage = buttonConsumeMessage;
                }

                if (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y))
                {
                    captureOnlySuppression.ScrollConsumeRequested = true;
                    captureOnlySuppression.ScrollConsumeSucceeded = SuppressScrollOnly(frame, mouse, rawScrollDelta);
                }

                UserNotesPinnedOverlayInputDiagnostics.Record(
                    source,
                    handleState,
                    handleScroll,
                    rawState,
                    mouse,
                    hitDiagnostics,
                    null,
                    captureOnlySuppression,
                    rawScrollDelta,
                    false,
                    ResolveTraceResultCode(hitDiagnostics, null, captureOnlySuppression, rawScrollDelta));
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

        private static bool SuppressScrollOnly(UserNotesPinnedOverlayFrame frame, LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            if (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y))
            {
                return UiMouseCaptureService.ConsumeScrollForOperationWindow();
            }

            return false;
        }

        private static UserNotesPinnedOverlaySuppressionDiagnostics ApplyInputSuppression(
            UserNotesPinnedOverlayFrame frame,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayInteraction interaction,
            int rawScrollDelta,
            bool preserveLeftHold)
        {
            var diagnostics = new UserNotesPinnedOverlaySuppressionDiagnostics();
            if (interaction == null)
            {
                return diagnostics;
            }

            if (interaction.CapturedMouse || UserNotesPinnedOverlayState.ShouldCaptureMouse(frame, mouse.X, mouse.Y))
            {
                diagnostics.MouseCaptureRequested = true;
                diagnostics.MouseCaptureSucceeded = preserveLeftHold
                    ? UiMouseCaptureService.CaptureForOperationWindowPreserveMouseButtons()
                    : UiMouseCaptureService.CaptureForOperationWindow();
                if (!preserveLeftHold)
                {
                    diagnostics.ButtonConsumeRequested = true;
                    string buttonConsumeMessage;
                    diagnostics.ButtonConsumeSucceeded = ConsumeMouseButtonsForUi(out buttonConsumeMessage);
                    diagnostics.ButtonConsumeMessage = buttonConsumeMessage;
                }
            }

            if (interaction.ScrollConsumed || (rawScrollDelta != 0 && UserNotesPinnedOverlayState.ShouldSuppressHotbarScroll(frame, mouse.X, mouse.Y)))
            {
                diagnostics.ScrollConsumeRequested = true;
                diagnostics.ScrollConsumeSucceeded = UiMouseCaptureService.ConsumeScrollForOperationWindow();
            }

            return diagnostics;
        }

        private static bool ShouldPreserveLeftHoldForDrag(UserNotesPinnedOverlayFrame frame, UserNotesPinnedOverlayInteraction interaction)
        {
            return frame != null && frame.Dragging ||
                   interaction != null && (interaction.DragStarted || interaction.Dragging);
        }

        private static bool ShouldPreserveLeftHoldForPendingDrag(
            string source,
            DiagnosticMouseState raw,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayFrame frame,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction)
        {
            UserNotesPinnedOverlayHitDiagnostics pendingHit;
            return TryResolvePendingToolbarPressCandidate(source, raw, mouse, frame, hit, interaction, out pendingHit) &&
                   pendingHit != null &&
                   string.Equals(pendingHit.ControlId, "drag", StringComparison.Ordinal);
        }

        private static bool ConsumeMouseButtonsForUi(out string message)
        {
            var consumedAny = false;
            message = string.Empty;
            for (var index = 0; index < MouseTriggerTokens.Length; index++)
            {
                string currentMessage;
                var consumed = TerrariaUiMouseCompat.TryConsumeMouseTriggerInputOnceForUi(MouseTriggerTokens[index], out currentMessage);
                consumedAny |= consumed;
                if (!string.IsNullOrWhiteSpace(currentMessage))
                {
                    message = currentMessage;
                }
            }

            return consumedAny;
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

        private static string ResolveTraceResultCode(
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            UserNotesPinnedOverlaySuppressionDiagnostics suppression,
            int rawScrollDelta)
        {
            if (interaction != null)
            {
                if (interaction.Unpinned)
                {
                    return "unpin";
                }

                if (interaction.OpacityChanged)
                {
                    return "opacity";
                }

                if (interaction.DragStarted)
                {
                    return "dragStart";
                }

                if (interaction.DragSaved)
                {
                    return "dragSaved";
                }

                if (interaction.ScrollConsumed)
                {
                    return "scroll";
                }
            }

            if (suppression != null && suppression.ButtonConsumeRequested)
            {
                return "captured";
            }

            if (rawScrollDelta != 0)
            {
                return "scrollObserved";
            }

            return hit != null && hit.MouseInside ? "hover" : "idle";
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
                    _pendingToolbarPress = null;
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
                _pendingToolbarPress = null;
            }
        }

        private static bool TryApplyPendingToolbarPress(string source, LegacyMouseSnapshot mouse, UserNotesPinnedOverlayHitDiagnostics hit)
        {
            if (!IsAfterPlayerInputSource(source) ||
                mouse == null ||
                !mouse.LeftDown ||
                mouse.LeftPressed ||
                hit == null ||
                !IsToolbarActionControl(hit.ControlId))
            {
                ClearPendingToolbarPressIfReleased(mouse);
                return false;
            }

            lock (SyncRoot)
            {
                if (_pendingToolbarPress == null ||
                    !string.Equals(_pendingToolbarPress.NoteId, hit.HitNoteId ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(_pendingToolbarPress.ControlId, hit.ControlId ?? string.Empty, StringComparison.Ordinal))
                {
                    return false;
                }

                mouse.LeftPressed = true;
                _pendingToolbarPress = null;
                return true;
            }
        }

        private static void UpdatePendingToolbarPress(
            string source,
            DiagnosticMouseState raw,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayFrame frame,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction)
        {
            UserNotesPinnedOverlayHitDiagnostics osHit;
            if (!TryResolvePendingToolbarPressCandidate(source, raw, mouse, frame, hit, interaction, out osHit))
            {
                return;
            }

            lock (SyncRoot)
            {
                _pendingToolbarPress = new PendingToolbarPress
                {
                    NoteId = osHit.HitNoteId ?? string.Empty,
                    ControlId = osHit.ControlId ?? string.Empty
                };
            }
        }

        private static bool TryResolvePendingToolbarPressCandidate(
            string source,
            DiagnosticMouseState raw,
            LegacyMouseSnapshot mouse,
            UserNotesPinnedOverlayFrame frame,
            UserNotesPinnedOverlayHitDiagnostics hit,
            UserNotesPinnedOverlayInteraction interaction,
            out UserNotesPinnedOverlayHitDiagnostics osHit)
        {
            osHit = null;
            if (mouse == null || !mouse.LeftDown || mouse.LeftReleased || DidExecuteToolbarPressCommand(interaction))
            {
                ClearPendingToolbarPress();
                return false;
            }

            if (!IsPrefixSource(source) ||
                !mouse.LeftPressed ||
                hit == null ||
                !hit.MouseInside ||
                IsToolbarActionControl(hit.ControlId))
            {
                return false;
            }

            int osX;
            int osY;
            if (!TryResolveOsClientOverlayPoint(raw, out osX, out osY))
            {
                return false;
            }

            osHit = UserNotesPinnedOverlayState.BuildHitDiagnostics(frame, osX, osY);
            return IsToolbarActionControl(osHit.ControlId) &&
                   string.Equals(osHit.HitNoteId ?? string.Empty, hit.HitNoteId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void ClearPendingToolbarPressIfReleased(LegacyMouseSnapshot mouse)
        {
            if (mouse == null || !mouse.LeftDown || mouse.LeftReleased)
            {
                ClearPendingToolbarPress();
            }
        }

        private static void ClearPendingToolbarPress()
        {
            lock (SyncRoot)
            {
                _pendingToolbarPress = null;
            }
        }

        private static bool DidExecuteToolbarPressCommand(UserNotesPinnedOverlayInteraction interaction)
        {
            return interaction != null &&
                   (interaction.Unpinned || interaction.OpacityChanged || interaction.DragStarted);
        }

        private static bool IsPrefixSource(string source)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf("UpdatePrefixGuard", StringComparison.Ordinal) >= 0;
        }

        private static bool IsAfterPlayerInputSource(string source)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf("UpdateAfterPlayerInputGuard", StringComparison.Ordinal) >= 0;
        }

        private static bool IsToolbarActionControl(string controlId)
        {
            return string.Equals(controlId, "close", StringComparison.Ordinal) ||
                   string.Equals(controlId, "opacity-increase", StringComparison.Ordinal) ||
                   string.Equals(controlId, "opacity-decrease", StringComparison.Ordinal) ||
                   string.Equals(controlId, "drag", StringComparison.Ordinal);
        }

        private static bool TryResolveOsClientOverlayPoint(DiagnosticMouseState raw, out int x, out int y)
        {
            x = -1;
            y = -1;
            if (raw == null || !raw.OsReadAvailable || raw.OsClientMouseX < 0 || raw.OsClientMouseY < 0)
            {
                return false;
            }

            var scaleX = raw.UiScaleX > 0.01d ? raw.UiScaleX : raw.UiScale;
            var scaleY = raw.UiScaleY > 0.01d ? raw.UiScaleY : raw.UiScale;
            if (scaleX <= 0.01d)
            {
                scaleX = 1d;
            }

            if (scaleY <= 0.01d)
            {
                scaleY = 1d;
            }

            var scaleActive = raw.UiScaleAvailable &&
                              (Math.Abs(scaleX - 1d) > 0.01d ||
                               Math.Abs(scaleY - 1d) > 0.01d ||
                               Math.Abs(raw.UiTranslateX) > 0.01d ||
                               Math.Abs(raw.UiTranslateY) > 0.01d);
            x = scaleActive ? ScreenToUiCoordinate(raw.OsClientMouseX, scaleX, raw.UiTranslateX) : raw.OsClientMouseX;
            y = scaleActive ? ScreenToUiCoordinate(raw.OsClientMouseY, scaleY, raw.UiTranslateY) : raw.OsClientMouseY;
            return true;
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

            var opacity = Clamp(item.OpacityPercent, 0, 100);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.Rect.X, item.Rect.Y, item.Rect.Width, item.Rect.Height, 6, 96, 112, 132, ScaleAlpha(168, opacity));
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.Rect.X + 1, item.Rect.Y + 1, item.Rect.Width - 2, item.Rect.Height - 2, 5, 18, 22, 30, ScaleAlpha(218, opacity));
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
                    ForegroundAlpha(246),
                    UserNotesPinnedOverlayState.BodyTextScale);
            }
        }

        private static void DrawControls(object spriteBatch, UserNotesPinnedOverlayItem item)
        {
            var opacity = Clamp(item.OpacityPercent, 0, 100);
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.ToolbarRect.X, item.ToolbarRect.Y, item.ToolbarRect.Width, item.ToolbarRect.Height, 5, 22, 28, 38, ScaleAlpha(178, opacity));
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, item.DragHandleRect.X, item.DragHandleRect.Y + 3, item.DragHandleRect.Width, 3, 2, 226, 232, 220, ForegroundAlpha(245));
            DrawControlButton(spriteBatch, item.DecreaseOpacityRect, "<", opacity);
            DrawControlButton(spriteBatch, item.IncreaseOpacityRect, ">", opacity);
            DrawControlButton(spriteBatch, item.CloseRect, "x", opacity);
        }

        private static void DrawControlButton(object spriteBatch, LegacyUiRect rect, string label, int opacityPercent)
        {
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 4, 96, 112, 146, ScaleAlpha(218, opacityPercent));
            UiPrimitiveRenderer.DrawRoundedRect(spriteBatch, rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2, 3, 32, 38, 52, ScaleAlpha(220, opacityPercent));
            UiTextRenderer.DrawCenteredText(spriteBatch, label, rect.X, rect.Y - 1, rect.Width, rect.Height, 244, 238, 212, ForegroundAlpha(255), 0.58f);
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

        internal static int ScaleAlphaForTesting(int baseAlpha, int opacityPercent)
        {
            return ScaleAlpha(baseAlpha, opacityPercent);
        }

        internal static int ForegroundAlphaForTesting(int baseAlpha)
        {
            return ForegroundAlpha(baseAlpha);
        }

        private static int ScaleAlpha(int baseAlpha, int opacityPercent)
        {
            return Clamp((int)Math.Round(Clamp(baseAlpha, 0, 255) * Clamp(opacityPercent, 0, 100) / 100d), 0, 255);
        }

        private static int ForegroundAlpha(int baseAlpha)
        {
            return Clamp(baseAlpha, 0, 255);
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

        private sealed class PendingToolbarPress
        {
            public string NoteId { get; set; }
            public string ControlId { get; set; }
        }
    }
}
