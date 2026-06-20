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
        private const int VkAlt = 0x12;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, bool> WasDownByTargetId =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        public static bool HasActiveBinding
        {
            get
            {
                var settings = ConfigService.HotkeySettings;
                FeatureToggleHotkeyChord chord;
                return TryGetActionChord(settings, FeatureIds.BlueprintCreateAction, out chord) ||
                       TryGetActionChord(settings, FeatureIds.BlueprintSaveAction, out chord);
            }
        }

        public static void Tick(GameStateSnapshot gameState)
        {
            var result = TickCore(
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(),
                gameState,
                IsRuntimeGateAvailable(gameState, out var gateReason),
                gateReason,
                IsCurrentProcessForeground(),
                false,
                IsKeyDown);
            if (result != null && result.Triggered)
            {
                DiagnosticActionRecorder.RecordHotkeyEvent(
                    result.Chord,
                    ScenarioNames.BlueprintActionHotkey,
                    result.DiagnosticResultCode,
                    result.Message);
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
                true,
                textInputFocused,
                key => downKeys != null && downKeys.TryGetValue(key, out var down) && down);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                WasDownByTargetId.Clear();
            }
        }

        private static BlueprintEntryHotkeyDispatchResult TickCore(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            GameStateSnapshot gameState,
            bool gameInputAvailable,
            string gateReason,
            bool foreground,
            bool textInputFocused,
            Func<int, bool> isKeyDown)
        {
            var result = TryTickTarget(
                appSettings,
                hotkeySettings,
                gameState,
                FeatureIds.BlueprintCreateAction,
                BlueprintEntryCommands.StartCreate,
                gameInputAvailable,
                gateReason,
                foreground,
                textInputFocused,
                isKeyDown);
            if (result.Triggered)
            {
                return result;
            }

            return TryTickTarget(
                appSettings,
                hotkeySettings,
                gameState,
                FeatureIds.BlueprintSaveAction,
                BlueprintEntryCommands.FinishCreateSave,
                gameInputAvailable,
                gateReason,
                foreground,
                textInputFocused,
                isKeyDown);
        }

        private static BlueprintEntryHotkeyDispatchResult TryTickTarget(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            GameStateSnapshot gameState,
            string targetId,
            string action,
            bool gameInputAvailable,
            string gateReason,
            bool foreground,
            bool textInputFocused,
            Func<int, bool> isKeyDown)
        {
            FeatureToggleHotkeyChord chord;
            if (!TryGetActionChord(hotkeySettings, targetId, out chord))
            {
                SetWasDown(targetId, false);
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            var down = IsChordDown(chord, isKeyDown);
            var wasDown = GetWasDown(targetId);
            SetWasDown(targetId, down);
            if (!down || wasDown)
            {
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            var effectiveGateReason = ResolveGateReason(gameState, gameInputAvailable, gateReason, foreground, textInputFocused);
            if (!string.IsNullOrWhiteSpace(effectiveGateReason))
            {
                return BlueprintEntryHotkeyDispatchResult.Blocked(
                    targetId,
                    action,
                    chord.Display,
                    effectiveGateReason,
                    IsUiGateReason(effectiveGateReason)
                        ? DiagnosticResultCode.BlockedByUi
                        : DiagnosticResultCode.BlockedByEnvironment);
            }

            return ApplyBlueprintAction(appSettings, targetId, action, chord.Display);
        }

        private static BlueprintEntryHotkeyDispatchResult ApplyBlueprintAction(
            AppSettings appSettings,
            string targetId,
            string action,
            string chord)
        {
            var result = BlueprintEntryState.ApplyCommand(action, appSettings ?? AppSettings.CreateDefault());
            BlueprintCaptureResult capture = null;
            if (result.Succeeded &&
                string.Equals(action, BlueprintEntryCommands.FinishCreateSave, StringComparison.Ordinal))
            {
                capture = BlueprintCaptureService.CapturePendingMaskAndSave(false);
                if (capture.Succeeded)
                {
                    BlueprintLibraryUiState.NotifyTemplateCreated(capture.SavedTemplate);
                    result = BlueprintEntryState.MarkCaptureSaved(capture);
                }
                else
                {
                    result = BlueprintEntryState.RecordCaptureFailure(capture);
                }
            }

            return BlueprintEntryHotkeyDispatchResult.FromApply(targetId, action, chord, result, capture);
        }

        private static bool TryGetActionChord(HotkeySettings settings, string targetId, out FeatureToggleHotkeyChord chord)
        {
            chord = null;
            if (settings == null ||
                settings.HotkeysByFeatureId == null ||
                string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string value;
            return settings.HotkeysByFeatureId.TryGetValue(targetId, out value) &&
                   FeatureToggleHotkeyChord.TryParse(value, out chord);
        }

        private static bool IsRuntimeGateAvailable(GameStateSnapshot gameState, out string reason)
        {
            reason = ResolveGameStateGateReason(gameState);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            if (LegacyTextInput.IsAnyFocused || LegacyHexColorInput.IsAnyFocused)
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
            bool foreground,
            bool textInputFocused)
        {
            if (!foreground)
            {
                return "notForeground";
            }

            if (textInputFocused)
            {
                return "textInputFocused";
            }

            if (!gameInputAvailable)
            {
                return string.IsNullOrWhiteSpace(gateReason) ? "gameInputUnavailable" : gateReason;
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

        private static bool GetWasDown(string targetId)
        {
            lock (SyncRoot)
            {
                return WasDownByTargetId.TryGetValue(targetId, out var value) && value;
            }
        }

        private static void SetWasDown(string targetId, bool down)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(targetId))
                {
                    return;
                }

                WasDownByTargetId[targetId] = down;
            }
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
                Applied = false,
                DiagnosticResultCode = DiagnosticResultCode.NotApplicable,
                Message = string.Empty,
                Reason = "directEntryHotkeyDisabled",
                Chord = string.Empty,
                TargetId = string.Empty,
                Action = string.Empty,
                ResultCode = string.Empty
            };

        public bool Triggered { get; private set; }
        public bool Applied { get; private set; }
        public string Chord { get; private set; }
        public string TargetId { get; private set; }
        public string Action { get; private set; }
        public string ResultCode { get; private set; }
        public string Reason { get; private set; }
        public string Message { get; private set; }
        public DiagnosticResultCode DiagnosticResultCode { get; private set; }

        public static BlueprintEntryHotkeyDispatchResult Blocked(
            string targetId,
            string action,
            string chord,
            string reason,
            DiagnosticResultCode diagnosticResultCode)
        {
            return new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = true,
                Applied = false,
                Chord = chord ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                Action = action ?? string.Empty,
                ResultCode = reason ?? string.Empty,
                Reason = reason ?? string.Empty,
                Message = "蓝图动作快捷键被阻止：" + (reason ?? string.Empty) + "。",
                DiagnosticResultCode = diagnosticResultCode
            };
        }

        public static BlueprintEntryHotkeyDispatchResult FromApply(
            string targetId,
            string action,
            string chord,
            BlueprintEntryCommandResult entry,
            BlueprintCaptureResult capture)
        {
            entry = entry ?? BlueprintEntryCommandResult.Create(false, false, false, "invalidResult", "蓝图动作快捷键执行失败。", BlueprintEntryModes.Tool);
            var resultCode = capture == null ? entry.ResultCode : capture.ResultCode;
            var applied = entry.Succeeded && !entry.PlaceholderOnly && (capture == null || capture.Succeeded);
            var diagnostic = applied
                ? DiagnosticResultCode.Succeeded
                : entry.Succeeded ? DiagnosticResultCode.NotApplicable : DiagnosticResultCode.Failed;
            return new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = true,
                Applied = applied,
                Chord = chord ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                Action = action ?? string.Empty,
                ResultCode = resultCode ?? string.Empty,
                Reason = resultCode ?? string.Empty,
                Message = entry.Message ?? string.Empty,
                DiagnosticResultCode = diagnostic
            };
        }
    }
}
