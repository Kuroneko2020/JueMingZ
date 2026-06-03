using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyUiInput
    {
        public static void UpdatePrefixGuard()
        {
            UiInputFrameClock.BeginUpdateFrame("LegacyMainUi.UpdatePrefix");
            try
            {
                ResetWheelFrameState();
                if (!LegacyMainUiState.Visible)
                {
                    return;
                }

                if (LegacyMainUiState.HideIfMainMenu("LegacyMainUi.UpdatePrefix"))
                {
                    return;
                }

                var raw = DiagnosticMouseStateReader.Read();
                var mouse = BuildMouseSnapshot(raw, false);
                var inWindow = IsMouseInWindow(mouse);
                var active = IsActiveInteraction();
                var diagnosticMainScrollDelta = ReadRawScrollDeltaLocked(raw);
                if (!inWindow && !active)
                {
                    return;
                }

                LegacyHotbarScrollGuard.ArmBeforeTerrariaUpdate(inWindow, active);
                var captured = UiMouseCaptureService.CaptureForOperationWindow();
                if (diagnosticMainScrollDelta != 0)
                {
                    var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(diagnosticMainScrollDelta);
                    var rawScrollDelta = scroll.EffectiveScrollDelta;
                    if (rawScrollDelta != 0)
                    {
                        ConsumeWheelDelta(scroll, captured, false, inWindow, active, mouse);
                    }
                }
                else
                {
                    RecordScrollSnapshotSkipped();
                }

                DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                    true,
                    "LegacyMainUi.UpdatePrefix",
                    captured,
                    false,
                    inWindow || active);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-ui-prefix-guard-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyUiInput",
                    "Legacy UI prefix guard failed.", error);
            }
        }

        public static void UpdateAfterPlayerInputGuard(string source)
        {
            UiInputFrameClock.BeginInputFrame(string.IsNullOrWhiteSpace(source) ? "LegacyMainUi.PlayerInputScroll" : source);
            try
            {
                if (!LegacyMainUiState.Visible)
                {
                    return;
                }

                if (LegacyMainUiState.HideIfMainMenu(string.IsNullOrWhiteSpace(source) ? "LegacyMainUi.PlayerInputScroll" : source))
                {
                    return;
                }

                var raw = DiagnosticMouseStateReader.Read();
                var mouse = BuildMouseSnapshot(raw, false);
                var inWindow = IsMouseInWindow(mouse);
                var active = IsActiveInteraction();
                if (!inWindow && !active)
                {
                    return;
                }

                var diagnosticMainScrollDelta = ReadRawScrollDeltaLocked(raw);
                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(diagnosticMainScrollDelta);
                if (scroll.EffectiveScrollDelta == 0)
                {
                    return;
                }

                var captured = UiMouseCaptureService.CaptureForOperationWindow();
                if (!WheelConsumedThisFrame)
                {
                    ConsumeWheelDelta(scroll, captured, false, inWindow, active, mouse);
                }
                else
                {
                    TerrariaUiMouseCompat.TryConsumeUiScroll();
                }

                DiagnosticInteractionDiagnostics.RecordUiClickSuppression(
                    true,
                    string.IsNullOrWhiteSpace(source) ? "LegacyMainUi.PlayerInputScroll" : source,
                    captured,
                    false,
                    inWindow || active);
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-ui-player-input-scroll-guard-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyUiInput",
                    "Legacy UI PlayerInput scroll guard failed.", error);
            }
        }

        public static bool ShouldSuppressHotbarScrollFromHook()
        {
            UiInputFrameClock.BeginInputFrame("LegacyMainUi.ScrollHotbarHook");
            try
            {
                if (!LegacyMainUiState.Visible)
                {
                    return false;
                }

                if (LegacyMainUiState.HideIfMainMenu("LegacyMainUi.ScrollHotbarHook"))
                {
                    return false;
                }

                var raw = DiagnosticMouseStateReader.Read();
                var mouse = BuildMouseSnapshot(raw, false);
                var inWindow = IsMouseInWindow(mouse);
                var active = IsActiveInteraction();
                if (!inWindow && !active)
                {
                    return false;
                }

                var diagnosticMainScrollDelta = ReadRawScrollDeltaLocked(raw);
                var scroll = TerrariaUiMouseCompat.ReadScrollSnapshot(diagnosticMainScrollDelta);
                var rawScrollDelta = scroll.EffectiveScrollDelta;
                if ((inWindow || active) && rawScrollDelta != 0 && !WheelConsumedThisFrame)
                {
                    var captured = UiMouseCaptureService.CaptureForOperationWindow();
                    ConsumeWheelDelta(scroll, captured, true, inWindow, active, mouse);
                }

                if ((inWindow || active) && (WheelConsumedThisFrame || rawScrollDelta != 0))
                {
                    TerrariaUiMouseCompat.MarkScrollHotbarHookSuppressed();
                    lock (SyncRoot)
                    {
                        _lastScrollHotbarHookSuppressed = true;
                    }

                    if (rawScrollDelta == 0)
                    {
                        RecordHotbarSuppressedByHookOnly(scroll.PlayerInputScrollDelta, scroll.PlayerInputScrollDeltaForUI, scroll.MainScrollDelta);
                    }

                    return true;
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-ui-scroll-hotbar-hook-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyUiInput",
                    "Legacy UI ScrollHotbar suppression hook failed.", error);
            }

            return false;
        }

        public static bool SuppressHotbarScroll()
        {
            return TerrariaUiMouseCompat.TryConsumeUiScroll();
        }

        private static int ReadRawScrollDeltaLocked(DiagnosticMouseState raw)
        {
            if (raw == null || !raw.TerrariaScrollWheelAvailable)
            {
                return raw == null ? 0 : raw.ScrollDelta;
            }

            lock (SyncRoot)
            {
                var current = raw.TerrariaScrollWheel;
                var delta = _hasLastRawScrollWheel ? current - _lastRawScrollWheel : raw.ScrollDelta;
                _lastRawScrollWheel = current;
                _hasLastRawScrollWheel = true;
                return delta;
            }
        }

        private static void ResetWheelFrameState()
        {
            lock (SyncRoot)
            {
                _wheelConsumedThisFrame = false;
                _lastPlayerInputScrollDelta = 0;
                _lastMainScrollDelta = 0;
                _lastPlayerInputCleared = false;
                _lastMainScrollSuppressed = false;
                _lastScrollHotbarHookSuppressed = false;
            }
        }

        private static void RecordScrollSnapshotSkipped()
        {
            lock (SyncRoot)
            {
                unchecked
                {
                    _scrollSnapshotSkippedCount++;
                }
            }
        }

        private static void ConsumeWheelDelta(UiScrollDeltaSnapshot scroll, bool mouseCaptured, bool fromScrollHotbarHook, bool mouseInLegacyWindow, bool activeInteraction, LegacyMouseSnapshot mouse)
        {
            if (scroll == null || scroll.EffectiveScrollDelta == 0)
            {
                return;
            }

            var rawScrollDelta = scroll.EffectiveScrollDelta;
            var hotbarSlotGuarded = LegacyHotbarScrollGuard.CaptureBeforeTerrariaUpdate(scroll, mouseInLegacyWindow, activeInteraction);
            var before = LegacyMainUiState.ScrollOffset;
            var nestedScrollConsumed = string.Equals(LegacyMainUiState.SelectedPageId, "fishing", StringComparison.Ordinal) &&
                                       FishingFilterUiState.TryConsumeNestedScroll(mouse, rawScrollDelta);
            var after = before;
            if (!nestedScrollConsumed)
            {
                var scrollDelta = WheelDeltaToScrollOffset(rawScrollDelta);
                after = LegacyMainUiState.ScrollByKnownMax(scrollDelta);
                if (after == before && LegacyMainUiState.MaxScroll <= 0)
                {
                    lock (SyncRoot)
                    {
                        _pendingUiScrollDelta += rawScrollDelta;
                    }
                }
            }

            var consumed = TerrariaUiMouseCompat.TryConsumeUiScroll();
            if (fromScrollHotbarHook)
            {
                TerrariaUiMouseCompat.MarkScrollHotbarHookSuppressed();
            }

            lock (SyncRoot)
            {
                _wheelConsumedThisFrame = true;
                _lastPlayerInputScrollDelta = scroll.PlayerInputScrollDelta;
                _lastMainScrollDelta = scroll.MainScrollDelta;
                _lastPlayerInputCleared = TerrariaUiMouseCompat.LastPlayerInputCleared;
                _lastMainScrollSuppressed = TerrariaUiMouseCompat.LastMainScrollSuppressed;
                _lastScrollHotbarHookSuppressed = fromScrollHotbarHook || TerrariaUiMouseCompat.LastScrollHotbarHookSuppressed;
            }

            RecordWheelCaptured(
                scroll,
                mouseCaptured || consumed,
                TerrariaUiMouseCompat.LastPlayerInputCleared,
                TerrariaUiMouseCompat.LastMainScrollSuppressed,
                fromScrollHotbarHook,
                hotbarSlotGuarded,
                mouseInLegacyWindow,
                activeInteraction,
                before,
                after);
        }

        private static int WheelDeltaToScrollOffset(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }

        private static void RecordWheelCaptured(UiScrollDeltaSnapshot scroll, bool mouseCaptured, bool playerInputCleared, bool mainScrollSuppressed, bool scrollHotbarHookSuppressed, bool hotbarSlotGuarded, bool mouseInLegacyWindow, bool activeInteraction, int before, int after)
        {
            var hotbarScrollSuppressed = playerInputCleared || mainScrollSuppressed || scrollHotbarHookSuppressed;
            var consumedDelta = scroll == null ? 0 : scroll.EffectiveScrollDelta;
            var playerInputDelta = scroll == null ? 0 : scroll.PlayerInputScrollDelta;
            var playerInputDeltaForUi = scroll == null ? 0 : scroll.PlayerInputScrollDeltaForUI;
            var mainScrollDelta = scroll == null ? 0 : scroll.MainScrollDelta;
            if (!ShouldRecordScrollActionEvent(LegacyMainUiState.SelectedPageId, hotbarScrollSuppressed, before, after))
            {
                return;
            }

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.WheelCaptured",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Legacy main window captured mouse wheel before Terraria hotbar handling.",
                0,
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"scrollDelta\":" + consumedDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDelta\":" + playerInputDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDeltaForUI\":" + playerInputDeltaForUi.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"mainScrollDelta\":" + mainScrollDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"scrollOffsetBefore\":" + before.ToString(CultureInfo.InvariantCulture) +
                "}",
                LegacyMainUiState.BuildUiStateJson(),
                "{" +
                    "\"mouseCaptured\":" + (mouseCaptured ? "true" : "false") + "," +
                    "\"wheelConsumedThisFrame\":true," +
                    "\"playerInputCleared\":" + (playerInputCleared ? "true" : "false") + "," +
                    "\"mainScrollSuppressed\":" + (mainScrollSuppressed ? "true" : "false") + "," +
                    "\"scrollHotbarHookSuppressed\":" + (scrollHotbarHookSuppressed ? "true" : "false") + "," +
                    "\"scrollOffsetAfter\":" + after.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"hotbarScrollSuppressed\":" + (hotbarScrollSuppressed ? "true" : "false") + "," +
                    "\"hotbarSlotGuarded\":" + (hotbarSlotGuarded ? "true" : "false") + "," +
                    "\"mouseInLegacyWindow\":" + (mouseInLegacyWindow ? "true" : "false") + "," +
                    "\"activeInteraction\":" + (activeInteraction ? "true" : "false") +
                "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);

            if (before != after)
            {
                RecordUiScroll(playerInputDelta, playerInputDeltaForUi, mainScrollDelta, consumedDelta, before, after, playerInputCleared, mainScrollSuppressed, scrollHotbarHookSuppressed, hotbarScrollSuppressed);
            }

            if (hotbarScrollSuppressed)
            {
                DiagnosticActionRecorder.RecordCustomEvent(
                    Guid.Empty,
                    "Ui.HotbarScrollSuppressed",
                    "UI",
                    string.Empty,
                    "Succeeded",
                    "Succeeded",
                    "Terraria hotbar scroll delta was synchronized away for this UI wheel event.",
                    0,
                    "{" +
                        "\"window\":\"LegacyMainWindow\"," +
                        "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                        "\"scrollDelta\":" + consumedDelta.ToString(CultureInfo.InvariantCulture) + "," +
                        "\"playerInputScrollDelta\":" + playerInputDelta.ToString(CultureInfo.InvariantCulture) + "," +
                        "\"playerInputScrollDeltaForUI\":" + playerInputDeltaForUi.ToString(CultureInfo.InvariantCulture) + "," +
                        "\"mainScrollDelta\":" + mainScrollDelta.ToString(CultureInfo.InvariantCulture) +
                    "}",
                    LegacyMainUiState.BuildUiStateJson(),
                    "{" +
                        "\"mouseCaptured\":true," +
                        "\"playerInputCleared\":" + (playerInputCleared ? "true" : "false") + "," +
                        "\"mainScrollSuppressed\":" + (mainScrollSuppressed ? "true" : "false") + "," +
                        "\"scrollHotbarHookSuppressed\":" + (scrollHotbarHookSuppressed ? "true" : "false") + "," +
                        "\"hotbarScrollSuppressed\":true" +
                    "}",
                    "UI",
                    "LegacyMainWindow",
                    string.Empty,
                    string.Empty);
            }
        }

        private static void RecordUiScroll(int playerInputDelta, int playerInputDeltaForUi, int mainScrollDelta, int consumedDelta, int before, int after, bool playerInputCleared, bool mainScrollSuppressed, bool scrollHotbarHookSuppressed, bool hotbarScrollSuppressed)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.Scroll",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Legacy main window consumed mouse wheel.",
                0,
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"scrollDelta\":" + consumedDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDelta\":" + playerInputDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDeltaForUI\":" + playerInputDeltaForUi.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"mainScrollDelta\":" + mainScrollDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"scrollOffsetBefore\":" + before.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"scrollOffsetAfter\":" + after.ToString(CultureInfo.InvariantCulture) +
                "}",
                "{" +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"mouseCaptured\":true," +
                    "\"wheelConsumedThisFrame\":true," +
                    "\"playerInputCleared\":" + (playerInputCleared ? "true" : "false") + "," +
                    "\"mainScrollSuppressed\":" + (mainScrollSuppressed ? "true" : "false") + "," +
                    "\"scrollHotbarHookSuppressed\":" + (scrollHotbarHookSuppressed ? "true" : "false") + "," +
                    "\"hotbarScrollSuppressed\":" + (hotbarScrollSuppressed ? "true" : "false") +
                "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static bool ShouldRecordScrollActionEvent(string pageId, bool hotbarScrollSuppressed, int before, int after)
        {
            var signature = (pageId ?? string.Empty) + "|" + (hotbarScrollSuppressed ? "hotbar" : "scroll") + "|" + (before == after ? "stable" : "moved");
            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                if (string.Equals(_lastScrollActionEventSignature, signature, StringComparison.Ordinal) &&
                    now - _lastScrollActionEventUtc < TimeSpan.FromMilliseconds(ScrollActionEventCoalesceMs))
                {
                    unchecked
                    {
                        _scrollEventCoalescedCount++;
                    }

                    return false;
                }

                _lastScrollActionEventSignature = signature;
                _lastScrollActionEventUtc = now;
                return true;
            }
        }

        internal static bool ShouldRecordScrollActionEventForTesting(string pageId, bool hotbarScrollSuppressed, int before, int after)
        {
            return ShouldRecordScrollActionEvent(pageId, hotbarScrollSuppressed, before, after);
        }

        private static void RecordHotbarSuppressedByHookOnly(int playerInputDelta, int playerInputDeltaForUi, int mainScrollDelta)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.HotbarScrollSuppressed",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Terraria Player.ScrollHotbar was skipped because the Legacy UI consumed this frame's wheel.",
                0,
                "{" +
                    "\"window\":\"LegacyMainWindow\"," +
                    "\"pageId\":\"" + EscapeJson(LegacyMainUiState.SelectedPageId) + "\"," +
                    "\"playerInputScrollDelta\":" + playerInputDelta.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"playerInputScrollDeltaForUI\":" + playerInputDeltaForUi.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"mainScrollDelta\":" + mainScrollDelta.ToString(CultureInfo.InvariantCulture) +
                "}",
                LegacyMainUiState.BuildUiStateJson(),
                "{" +
                    "\"mouseCaptured\":true," +
                    "\"wheelConsumedThisFrame\":true," +
                    "\"playerInputCleared\":" + (TerrariaUiMouseCompat.LastPlayerInputCleared ? "true" : "false") + "," +
                    "\"mainScrollSuppressed\":" + (TerrariaUiMouseCompat.LastMainScrollSuppressed ? "true" : "false") + "," +
                    "\"scrollHotbarHookSuppressed\":true," +
                    "\"hotbarScrollSuppressed\":true" +
                "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
