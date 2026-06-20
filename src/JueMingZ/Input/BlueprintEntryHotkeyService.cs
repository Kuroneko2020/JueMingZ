using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Actions;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    internal static class BlueprintEntryHotkeyService
    {
        private const string Scenario = "Hotkey.BlueprintEntry";
        private const int VkAlt = 0x12;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        private static readonly object SyncRoot = new object();
        private static string _sourceChord = string.Empty;
        private static FeatureToggleHotkeyChord _chord;
        private static bool _wasDown;

        public static bool HasActiveBinding
        {
            get { return TryReadBinding(ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(), out _); }
        }

        public static void Tick(GameStateSnapshot gameState)
        {
            var result = TickCore(
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(),
                gameState,
                IsRuntimeGateAvailable(gameState, out var gateReason),
                gateReason,
                false,
                IsCurrentProcessForeground(),
                IsKeyDown,
                true,
                true,
                true);
            if (result != null)
            {
                // Runtime records event diagnostics; tests inspect the returned decision.
            }
        }

        internal static BlueprintEntryHotkeyDispatchResult TickForTesting(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            IDictionary<int, bool> downKeys,
            bool gameInputAvailable,
            string gateReason,
            bool textInputFocused)
        {
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                hotkeySettings ?? HotkeySettings.CreateDefault(),
                null,
                gameInputAvailable,
                gateReason,
                textInputFocused,
                true,
                key => downKeys != null && downKeys.TryGetValue(key, out var down) && down,
                true,
                false,
                false);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _sourceChord = string.Empty;
                _chord = null;
                _wasDown = false;
            }
        }

        private static BlueprintEntryHotkeyDispatchResult TickCore(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            GameStateSnapshot gameState,
            bool gameInputAvailable,
            string gateReason,
            bool textInputFocused,
            bool foreground,
            Func<int, bool> isKeyDown,
            bool applyEntryState,
            bool applyUi,
            bool recordDiagnostic)
        {
            FeatureToggleHotkeyChord chord;
            if (!TryReadBinding(hotkeySettings, out chord))
            {
                SetWasDown(false);
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            var down = IsChordDown(chord, isKeyDown);
            var wasDown = GetWasDown();
            SetWasDown(down);
            if (!down || wasDown)
            {
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            var effectiveGateReason = ResolveGateReason(gameState, gameInputAvailable, gateReason, textInputFocused, foreground);
            BlueprintEntryHotkeyDispatchResult result;
            if (!string.IsNullOrWhiteSpace(effectiveGateReason))
            {
                result = BlueprintEntryHotkeyDispatchResult.Blocked(
                    chord,
                    effectiveGateReason,
                    IsUiGateReason(effectiveGateReason)
                        ? DiagnosticResultCode.BlockedByUi
                        : DiagnosticResultCode.BlockedByEnvironment);
            }
            else
            {
                var entry = applyEntryState
                    ? BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenEntryHotkey, appSettings)
                    : BlueprintEntryCommandResult.Create(true, false, true, "opened", "蓝图入口已打开。", BlueprintEntryModes.Tool);
                if (applyUi)
                {
                    LegacyMainUiState.SetVisible(true);
                    LegacyMainUiState.SelectPage("blueprint");
                }

                result = BlueprintEntryHotkeyDispatchResult.FromEntry(chord, entry);
            }

            if (recordDiagnostic)
            {
                DiagnosticActionRecorder.RecordHotkeyEvent(
                    chord.Display,
                    Scenario,
                    result.DiagnosticResultCode,
                    result.Message);
            }

            return result;
        }

        private static bool TryReadBinding(HotkeySettings hotkeySettings, out FeatureToggleHotkeyChord chord)
        {
            chord = null;
            var hotkeys = hotkeySettings == null ? null : hotkeySettings.HotkeysByFeatureId;
            if (hotkeys == null || hotkeys.Count <= 0)
            {
                return false;
            }

            string source;
            if (!hotkeys.TryGetValue(FeatureIds.BlueprintMain, out source) ||
                string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (string.Equals(_sourceChord, source, StringComparison.Ordinal) && _chord != null)
                {
                    chord = _chord;
                    return true;
                }

                FeatureToggleHotkeyChord parsed;
                if (!FeatureToggleHotkeyChord.TryParse(source, out parsed))
                {
                    _sourceChord = string.Empty;
                    _chord = null;
                    return false;
                }

                _sourceChord = source;
                _chord = parsed;
                chord = parsed;
                return true;
            }
        }

        private static bool GetWasDown()
        {
            lock (SyncRoot)
            {
                return _wasDown;
            }
        }

        private static void SetWasDown(bool value)
        {
            lock (SyncRoot)
            {
                _wasDown = value;
            }
        }

        private static bool IsRuntimeGateAvailable(GameStateSnapshot gameState, out string reason)
        {
            reason = ResolveGameStateGateReason(gameState);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            if (LegacyTextInput.IsAnyFocused || LegacyHexColorInput.IsAnyFocused || LegacyMultilineTextInput.IsAnyFocused)
            {
                reason = "textInputFocused";
                return false;
            }

            if (LegacyUiInput.IsActiveInteraction())
            {
                reason = "legacyUiActive";
                return false;
            }

            if (LegacyUiOverlayCoordinator.Current.HasAnyActiveModal())
            {
                reason = "legacyModalActive";
                return false;
            }

            if (LegacyMainWindow.IsAnyHotkeyCaptureActive())
            {
                reason = "hotkeyCaptureActive";
                return false;
            }

            return true;
        }

        private static string ResolveGateReason(
            GameStateSnapshot gameState,
            bool gameInputAvailable,
            string gateReason,
            bool textInputFocused,
            bool foreground)
        {
            if (!foreground)
            {
                return "notForeground";
            }

            if (!gameInputAvailable)
            {
                return string.IsNullOrWhiteSpace(gateReason) ? "gameInputUnavailable" : gateReason;
            }

            if (textInputFocused)
            {
                return "textInputFocused";
            }

            return ResolveGameStateGateReason(gameState);
        }

        private static string ResolveGameStateGateReason(GameStateSnapshot gameState)
        {
            if (gameState == null)
            {
                return string.Empty;
            }

            if (!gameState.IsInWorld)
            {
                return "notInWorld";
            }

            if (gameState.IsInMainMenu)
            {
                return "mainMenu";
            }

            var ui = gameState.Ui;
            if (ui == null)
            {
                return "uiUnavailable";
            }

            if (!ui.GameInputAvailable)
            {
                return "gameInputUnavailable";
            }

            if (ui.IsInMainMenu)
            {
                return "mainMenu";
            }

            if (ui.ChatOpen)
            {
                return "chatOpen";
            }

            return string.Empty;
        }

        private static bool IsUiGateReason(string reason)
        {
            return string.Equals(reason, "textInputFocused", StringComparison.Ordinal) ||
                   string.Equals(reason, "legacyUiActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "legacyModalActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "hotkeyCaptureActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "chatOpen", StringComparison.Ordinal);
        }

        private static bool IsChordDown(FeatureToggleHotkeyChord chord, Func<int, bool> isKeyDown)
        {
            if (chord == null || isKeyDown == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(chord.Modifier) && !isKeyDown(ModifierToVirtualKey(chord.Modifier)))
            {
                return false;
            }

            var key = MainKeyToVirtualKey(chord.Key);
            return key > 0 && isKeyDown(key);
        }

        private static int ModifierToVirtualKey(string modifier)
        {
            if (string.Equals(modifier, "Alt", StringComparison.Ordinal))
            {
                return VkAlt;
            }

            if (string.Equals(modifier, "Ctrl", StringComparison.Ordinal))
            {
                return VkControl;
            }

            return string.Equals(modifier, "Shift", StringComparison.Ordinal) ? VkShift : 0;
        }

        private static int MainKeyToVirtualKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (key.Length == 1)
            {
                var ch = char.ToUpperInvariant(key[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    return ch;
                }
            }

            if (key.Length >= 2 &&
                key[0] == 'F' &&
                int.TryParse(key.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey) &&
                functionKey >= 1 &&
                functionKey <= 24)
            {
                return 0x70 + functionKey - 1;
            }

            return 0;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return virtualKey > 0 && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsCurrentProcessForeground()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return true;
                }

                int processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                return processId == Process.GetCurrentProcess().Id;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);
    }

    internal sealed class BlueprintEntryHotkeyDispatchResult
    {
        public static readonly BlueprintEntryHotkeyDispatchResult NoOp =
            new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = false,
                DiagnosticResultCode = DiagnosticResultCode.NotApplicable,
                Message = string.Empty,
                Reason = string.Empty,
                Chord = string.Empty
            };

        public bool Triggered { get; private set; }
        public bool Applied { get; private set; }
        public string Chord { get; private set; }
        public string Reason { get; private set; }
        public string Message { get; private set; }
        public DiagnosticResultCode DiagnosticResultCode { get; private set; }

        public static BlueprintEntryHotkeyDispatchResult FromEntry(
            FeatureToggleHotkeyChord chord,
            BlueprintEntryCommandResult entry)
        {
            return new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = true,
                Applied = entry != null && entry.Succeeded,
                Chord = chord == null ? string.Empty : chord.Display,
                Reason = entry == null ? "unknown" : entry.ResultCode,
                DiagnosticResultCode = entry != null && entry.Succeeded
                    ? DiagnosticResultCode.Succeeded
                    : DiagnosticResultCode.NotApplicable,
                Message = entry == null ? "Blueprint entry hotkey ignored." : entry.Message
            };
        }

        public static BlueprintEntryHotkeyDispatchResult Blocked(
            FeatureToggleHotkeyChord chord,
            string reason,
            DiagnosticResultCode resultCode)
        {
            return new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = true,
                Applied = false,
                Chord = chord == null ? string.Empty : chord.Display,
                Reason = reason ?? string.Empty,
                DiagnosticResultCode = resultCode,
                Message = "Blueprint entry hotkey blocked: " + (reason ?? string.Empty) + "."
            };
        }
    }
}
