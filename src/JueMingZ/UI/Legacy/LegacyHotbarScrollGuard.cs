using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static class LegacyHotbarScrollGuard
    {
        private const int VerificationFrames = 3;
        private static readonly object SyncRoot = new object();
        private static bool _active;
        private static int _remainingFrames;
        private static int _guardedSlot = -1;
        private static int _slotBeforeUpdate = -1;
        private static bool _mouseInLegacyWindow;
        private static bool _activeInteraction;
        private static int _wheelDelta;
        private static int _playerInputScrollDelta;
        private static int _playerInputScrollDeltaForUI;
        private static int _mainScrollDelta;
        private static string _candidateSummary = string.Empty;
        private static bool _armed;
        private static int _armedSlot = -1;
        private static bool _armedMouseInLegacyWindow;
        private static bool _armedActiveInteraction;
        private static DateTime _lastVerifiedEventUtc = DateTime.MinValue;

        public static bool ArmBeforeTerrariaUpdate(bool mouseInLegacyWindow, bool activeInteraction)
        {
            if (!mouseInLegacyWindow && !activeInteraction)
            {
                return false;
            }

            object player;
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) ||
                !TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) ||
                selectedSlot < 0 ||
                selectedSlot > 9)
            {
                LogThrottle.WarnThrottled(
                    "legacy-hotbar-scroll-guard-arm-failed",
                    TimeSpan.FromSeconds(10),
                    "LegacyHotbarScrollGuard",
                    "Could not arm selectedItem for UI wheel guard: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            lock (SyncRoot)
            {
                _armed = true;
                _armedSlot = selectedSlot;
                _armedMouseInLegacyWindow = mouseInLegacyWindow;
                _armedActiveInteraction = activeInteraction;
            }

            return true;
        }

        public static bool CaptureBeforeTerrariaUpdate(UiScrollDeltaSnapshot scroll, bool mouseInLegacyWindow, bool activeInteraction)
        {
            if (scroll == null || scroll.EffectiveScrollDelta == 0)
            {
                return false;
            }

            object player;
            int selectedSlot;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) ||
                !TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) ||
                selectedSlot < 0 ||
                selectedSlot > 9)
            {
                LogThrottle.WarnThrottled(
                    "legacy-hotbar-scroll-guard-capture-failed",
                    TimeSpan.FromSeconds(10),
                    "LegacyHotbarScrollGuard",
                    "Could not capture selectedItem for UI wheel guard: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            lock (SyncRoot)
            {
                _active = true;
                _remainingFrames = VerificationFrames;
                _guardedSlot = selectedSlot;
                _slotBeforeUpdate = selectedSlot;
                _mouseInLegacyWindow = mouseInLegacyWindow;
                _activeInteraction = activeInteraction;
                _wheelDelta = scroll.EffectiveScrollDelta;
                _playerInputScrollDelta = scroll.PlayerInputScrollDelta;
                _playerInputScrollDeltaForUI = scroll.PlayerInputScrollDeltaForUI;
                _mainScrollDelta = scroll.MainScrollDelta;
                _candidateSummary = scroll.CandidateSummary ?? string.Empty;
            }

            return true;
        }

        public static bool RestoreLateUiWheelIfNeeded(UiScrollDeltaSnapshot scroll, bool mouseInLegacyWindow, bool activeInteraction)
        {
            if (scroll == null || scroll.EffectiveScrollDelta == 0 || (!mouseInLegacyWindow && !activeInteraction))
            {
                return false;
            }

            int guardedSlot;
            bool armedMouseInWindow;
            bool armedActive;
            lock (SyncRoot)
            {
                if (!_armed || _armedSlot < 0 || _armedSlot > 9)
                {
                    return false;
                }

                guardedSlot = _armedSlot;
                armedMouseInWindow = _armedMouseInLegacyWindow || mouseInLegacyWindow;
                armedActive = _armedActiveInteraction || activeInteraction;
                _armed = false;
                _armedSlot = -1;
            }

            object player;
            int selectedAfterUpdate;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) ||
                !TerrariaInputCompat.TryGetSelectedItem(player, out selectedAfterUpdate))
            {
                LogThrottle.WarnThrottled(
                    "legacy-hotbar-scroll-guard-late-restore-read-failed",
                    TimeSpan.FromSeconds(10),
                    "LegacyHotbarScrollGuard",
                    "Could not verify selectedItem during late UI wheel guard: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            var snapshot = new GuardSnapshot
            {
                GuardedSlot = guardedSlot,
                SlotBeforeUpdate = guardedSlot,
                MouseInLegacyWindow = armedMouseInWindow,
                ActiveInteraction = armedActive,
                WheelDelta = scroll.EffectiveScrollDelta,
                PlayerInputScrollDelta = scroll.PlayerInputScrollDelta,
                PlayerInputScrollDeltaForUI = scroll.PlayerInputScrollDeltaForUI,
                MainScrollDelta = scroll.MainScrollDelta,
                CandidateSummary = scroll.CandidateSummary ?? string.Empty,
                RemainingFrames = 0
            };

            if (selectedAfterUpdate == guardedSlot)
            {
                RecordHotbarSlotGuardVerified(snapshot, selectedAfterUpdate);
                return true;
            }

            var restoreSucceeded = TerrariaInputCompat.TrySelectInventorySlot(player, guardedSlot);
            var slotAfterRestore = selectedAfterUpdate;
            int verifiedSlot;
            if (TerrariaInputCompat.TryGetSelectedItem(player, out verifiedSlot))
            {
                slotAfterRestore = verifiedSlot;
                restoreSucceeded = restoreSucceeded && verifiedSlot == guardedSlot;
            }

            RecordHotbarSlotRestored(snapshot, selectedAfterUpdate, slotAfterRestore, restoreSucceeded);
            return restoreSucceeded;
        }

        public static void ApplyPostTerrariaUpdate()
        {
            GuardSnapshot snapshot;
            lock (SyncRoot)
            {
                if (!_active || _remainingFrames <= 0)
                {
                    return;
                }

                snapshot = new GuardSnapshot
                {
                    GuardedSlot = _guardedSlot,
                    SlotBeforeUpdate = _slotBeforeUpdate,
                    MouseInLegacyWindow = _mouseInLegacyWindow,
                    ActiveInteraction = _activeInteraction,
                    WheelDelta = _wheelDelta,
                    PlayerInputScrollDelta = _playerInputScrollDelta,
                    PlayerInputScrollDeltaForUI = _playerInputScrollDeltaForUI,
                    MainScrollDelta = _mainScrollDelta,
                    CandidateSummary = _candidateSummary,
                    RemainingFrames = _remainingFrames
                };
            }

            object player;
            int selectedAfterUpdate;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) ||
                !TerrariaInputCompat.TryGetSelectedItem(player, out selectedAfterUpdate))
            {
                LogThrottle.WarnThrottled(
                    "legacy-hotbar-scroll-guard-verify-failed",
                    TimeSpan.FromSeconds(10),
                    "LegacyHotbarScrollGuard",
                    "Could not verify selectedItem after Terraria update: " + TerrariaInputCompat.LastInputCompatError);
                FinishVerificationFrame();
                return;
            }

            if (selectedAfterUpdate != snapshot.GuardedSlot)
            {
                var restoreSucceeded = TerrariaInputCompat.TrySelectInventorySlot(player, snapshot.GuardedSlot);
                var slotAfterRestore = selectedAfterUpdate;
                int verifiedSlot;
                if (TerrariaInputCompat.TryGetSelectedItem(player, out verifiedSlot))
                {
                    slotAfterRestore = verifiedSlot;
                    restoreSucceeded = restoreSucceeded && verifiedSlot == snapshot.GuardedSlot;
                }

                RecordHotbarSlotRestored(snapshot, selectedAfterUpdate, slotAfterRestore, restoreSucceeded);
                FinishVerificationFrame();
                return;
            }

            RecordHotbarSlotGuardVerified(snapshot, selectedAfterUpdate);
            FinishVerificationFrame();
        }

        private static void FinishVerificationFrame()
        {
            lock (SyncRoot)
            {
                _remainingFrames--;
                if (_remainingFrames <= 0)
                {
                    _active = false;
                    _guardedSlot = -1;
                    _slotBeforeUpdate = -1;
                }
            }
        }

        private static void RecordHotbarSlotRestored(GuardSnapshot snapshot, int slotAfterUpdate, int slotAfterRestore, bool restoreSucceeded)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.HotbarSlotRestored",
                "UI",
                string.Empty,
                restoreSucceeded ? "Succeeded" : "Failed",
                restoreSucceeded ? "Succeeded" : "Failed",
                restoreSucceeded
                    ? "Legacy UI wheel guard restored the hotbar slot after Terraria processed scroll input."
                    : "Legacy UI wheel guard detected hotbar slot drift but could not restore it.",
                0,
                BuildInputJson(snapshot),
                "{" +
                    "\"slotAfterTerrariaUpdate\":" + slotAfterUpdate.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"slotAfterRestore\":" + slotAfterRestore.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"restored\":true," +
                    "\"restoreSucceeded\":" + BoolRaw(restoreSucceeded) +
                "}",
                BuildContextJson(snapshot),
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static void RecordHotbarSlotGuardVerified(GuardSnapshot snapshot, int slotAfterUpdate)
        {
            if (DateTime.UtcNow - _lastVerifiedEventUtc < TimeSpan.FromSeconds(1))
            {
                return;
            }

            _lastVerifiedEventUtc = DateTime.UtcNow;
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "Ui.HotbarSlotGuardVerified",
                "UI",
                string.Empty,
                "Succeeded",
                "Succeeded",
                "Legacy UI wheel guard verified Terraria did not change the hotbar slot.",
                0,
                BuildInputJson(snapshot),
                "{" +
                    "\"slotAfterTerrariaUpdate\":" + slotAfterUpdate.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"slotAfterRestore\":" + slotAfterUpdate.ToString(CultureInfo.InvariantCulture) + "," +
                    "\"restored\":false," +
                    "\"restoreSucceeded\":true" +
                "}",
                BuildContextJson(snapshot),
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static string BuildInputJson(GuardSnapshot snapshot)
        {
            return "{" +
                "\"slotBeforeUpdate\":" + snapshot.SlotBeforeUpdate.ToString(CultureInfo.InvariantCulture) + "," +
                "\"guardedSlot\":" + snapshot.GuardedSlot.ToString(CultureInfo.InvariantCulture) + "," +
                "\"wheelDelta\":" + snapshot.WheelDelta.ToString(CultureInfo.InvariantCulture) + "," +
                "\"playerInputScrollDelta\":" + snapshot.PlayerInputScrollDelta.ToString(CultureInfo.InvariantCulture) + "," +
                "\"playerInputScrollDeltaForUI\":" + snapshot.PlayerInputScrollDeltaForUI.ToString(CultureInfo.InvariantCulture) + "," +
                "\"mainScrollDelta\":" + snapshot.MainScrollDelta.ToString(CultureInfo.InvariantCulture) +
            "}";
        }

        private static string BuildContextJson(GuardSnapshot snapshot)
        {
            return "{" +
                "\"mouseInLegacyWindow\":" + BoolRaw(snapshot.MouseInLegacyWindow) + "," +
                "\"activeInteraction\":" + BoolRaw(snapshot.ActiveInteraction) + "," +
                "\"remainingFrames\":" + snapshot.RemainingFrames.ToString(CultureInfo.InvariantCulture) + "," +
                "\"selectionMethod\":\"" + EscapeJson(TerrariaInputCompat.LastSelectionMethod) + "\"," +
                "\"scrollCandidateSummary\":\"" + EscapeJson(snapshot.CandidateSummary) + "\"" +
            "}";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
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

        private sealed class GuardSnapshot
        {
            public int GuardedSlot;
            public int SlotBeforeUpdate;
            public bool MouseInLegacyWindow;
            public bool ActiveInteraction;
            public int WheelDelta;
            public int PlayerInputScrollDelta;
            public int PlayerInputScrollDeltaForUI;
            public int MainScrollDelta;
            public string CandidateSummary;
            public int RemainingFrames;
        }
    }
}
