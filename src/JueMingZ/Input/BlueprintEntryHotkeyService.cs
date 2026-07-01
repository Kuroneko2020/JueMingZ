using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Input.Hotkeys;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    internal static class BlueprintEntryHotkeyService
    {
        private const int VkAlt = 0x12;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        public static bool HasActiveBinding
        {
            get
            {
                return HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintCreateAction) ||
                       HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintSaveAction) ||
                       HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintMoveAction) ||
                       HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintRegionAction) ||
                       HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintMirrorAction) ||
                       HasActiveUnifiedBlueprintAction(FeatureIds.BlueprintLibraryAction);
            }
        }

        public static void Tick(GameStateSnapshot gameState)
        {
            var result = TickCore(
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(bindingId));
            if (result != null && result.Triggered)
            {
                RecordBlueprintActionHotkeyEvent(result);
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
            var unifiedHotkeys = CreateUnifiedSettingsFromLegacyBlueprintHotkeys(hotkeySettings);
            var gate = CreateGateContextForTesting(gameInputAvailable, gateReason, textInputFocused);
            var input = CreateUnifiedInputStateFromLegacyDownKeys(downKeys);
            var signature = unifiedHotkeys.CreateCacheSignature();
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(unifiedHotkeys, signature, bindingId, gate, input));
        }

        internal static BlueprintEntryHotkeyDispatchResult TickUnifiedForTesting(
            AppSettings appSettings,
            UnifiedHotkeySettings unifiedHotkeySettings,
            IDictionary<int, bool> downKeys,
            UnifiedHotkeyRuntimeGateContext gateContext)
        {
            unifiedHotkeySettings = unifiedHotkeySettings ?? UnifiedHotkeySettings.CreateDefault();
            var signature = unifiedHotkeySettings.CreateCacheSignature();
            var input = UnifiedHotkeyRuntimeInputState.FromDictionary(downKeys);
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                bindingId => UnifiedHotkeyRuntimeService.QueryBinding(
                    unifiedHotkeySettings,
                    signature,
                    bindingId,
                    gateContext ?? new UnifiedHotkeyRuntimeGateContext(),
                    input));
        }

        internal static void ResetForTesting()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
        }

        private static BlueprintEntryHotkeyDispatchResult TickCore(
            AppSettings appSettings,
            Func<string, UnifiedHotkeyRuntimeTriggerResult> queryBinding)
        {
            var result = TryTickTarget(
                appSettings,
                FeatureIds.BlueprintCreateAction,
                BlueprintEntryCommands.StartCreate,
                queryBinding);
            if (result.Triggered)
            {
                return result;
            }

            result = TryTickTarget(
                appSettings,
                FeatureIds.BlueprintSaveAction,
                BlueprintEntryCommands.FinishCreateSave,
                queryBinding);
            if (result.Triggered)
            {
                return result;
            }

            result = TryTickTarget(
                appSettings,
                FeatureIds.BlueprintMoveAction,
                BlueprintEntryCommands.StartMove,
                queryBinding);
            if (result.Triggered)
            {
                return result;
            }

            result = TryTickTarget(
                appSettings,
                FeatureIds.BlueprintRegionAction,
                BlueprintEntryCommands.StartRegionModify,
                queryBinding);
            if (result.Triggered)
            {
                return result;
            }

            result = TryTickTarget(
                appSettings,
                FeatureIds.BlueprintMirrorAction,
                BlueprintEntryCommands.StartMirror,
                queryBinding);
            if (result.Triggered)
            {
                return result;
            }

            return TryTickTarget(
                appSettings,
                FeatureIds.BlueprintLibraryAction,
                BlueprintEntryCommands.OpenLibrary,
                queryBinding);
        }

        private static BlueprintEntryHotkeyDispatchResult TryTickTarget(
            AppSettings appSettings,
            string targetId,
            string action,
            Func<string, UnifiedHotkeyRuntimeTriggerResult> queryBinding)
        {
            if (queryBinding == null)
            {
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            var trigger = queryBinding(UnifiedHotkeyBindingIds.ForBlueprintAction(targetId));
            if (!trigger.PressedEdge)
            {
                return BlueprintEntryHotkeyDispatchResult.NoOp;
            }

            if (string.Equals(trigger.ResultCode, "blocked", StringComparison.Ordinal))
            {
                return BlueprintEntryHotkeyDispatchResult.Blocked(
                    targetId,
                    action,
                    trigger.Display,
                    trigger.Reason,
                    UnifiedHotkeyReasonCatalog.IsUiGateReason(trigger.Reason)
                        ? DiagnosticResultCode.BlockedByUi
                        : DiagnosticResultCode.BlockedByEnvironment);
            }

            return string.Equals(trigger.ResultCode, "triggered", StringComparison.Ordinal)
                ? ApplyBlueprintAction(appSettings, targetId, action, trigger.Display)
                : BlueprintEntryHotkeyDispatchResult.NoOp;
        }

        private static BlueprintEntryHotkeyDispatchResult ApplyBlueprintAction(
            AppSettings appSettings,
            string targetId,
            string action,
            string chord)
        {
            if (string.Equals(action, BlueprintEntryCommands.StartMove, StringComparison.Ordinal))
            {
                return ApplyBlueprintMoveAction(appSettings, targetId, action, chord);
            }

            if (string.Equals(action, BlueprintEntryCommands.StartRegionModify, StringComparison.Ordinal))
            {
                return ApplyBlueprintRegionAction(appSettings, targetId, action, chord);
            }

            if (string.Equals(action, BlueprintEntryCommands.StartMirror, StringComparison.Ordinal))
            {
                return ApplyBlueprintMirrorAction(appSettings, targetId, action, chord);
            }

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
            else if (result.Succeeded &&
                     string.Equals(action, BlueprintEntryCommands.OpenLibrary, StringComparison.Ordinal))
            {
                var library = BlueprintLibraryUiState.OpenLibrary();
                if (library != null && !library.Succeeded)
                {
                    result = BlueprintEntryCommandResult.Create(false, false, false, library.ResultCode, library.Message, result.Mode);
                }
                else
                {
                    LegacyMainUiState.SelectPage("blueprint");
                    LegacyMainUiState.SetScrollOffset(0, 0);
                    LegacyMainUiState.SetVisible(true);
                }
            }

            return BlueprintEntryHotkeyDispatchResult.FromApply(targetId, action, chord, result, capture);
        }

        private static BlueprintEntryHotkeyDispatchResult ApplyBlueprintMoveAction(
            AppSettings appSettings,
            string targetId,
            string action,
            string chord)
        {
            var transform = BlueprintPlacedInstanceTransformState.GetSnapshot();
            BlueprintPlacedInstanceTransformCommandResult move;
            BlueprintEntryCommandResult entry = null;
            var startedMove = false;
            if (transform != null &&
                transform.Active &&
                string.Equals(transform.Mode, BlueprintPlacedInstanceTransformModes.Move, StringComparison.Ordinal))
            {
                move = BlueprintPlacedInstanceTransformState.Cancel();
            }
            else
            {
                BlueprintCreationMaskState.Cancel();
                BlueprintPlacementPreviewState.Cancel();
                BlueprintEraseRegionState.Cancel();
                move = BlueprintPlacedInstanceTransformState.BeginMove();
                if (move != null && move.Succeeded)
                {
                    entry = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenPlacedInstances, appSettings ?? AppSettings.CreateDefault());
                    LegacyMainUiState.SetVisible(false);
                    startedMove = true;
                }
            }

            return BlueprintEntryHotkeyDispatchResult.FromTransform(targetId, action, chord, move, entry, startedMove);
        }

        private static BlueprintEntryHotkeyDispatchResult ApplyBlueprintRegionAction(
            AppSettings appSettings,
            string targetId,
            string action,
            string chord)
        {
            var before = BlueprintEraseRegionState.GetSnapshot();
            var result = BlueprintEntryState.ApplyCommand(action, appSettings ?? AppSettings.CreateDefault());
            var after = BlueprintEraseRegionState.GetSnapshot();
            var startedRegion = result.Succeeded &&
                                (before == null || !before.Active) &&
                                after != null &&
                                after.Active;
            if (startedRegion)
            {
                LegacyMainUiState.SetVisible(false);
            }

            return BlueprintEntryHotkeyDispatchResult.FromApply(targetId, action, chord, result, null);
        }

        private static BlueprintEntryHotkeyDispatchResult ApplyBlueprintMirrorAction(
            AppSettings appSettings,
            string targetId,
            string action,
            string chord)
        {
            var transform = BlueprintPlacedInstanceTransformState.GetSnapshot();
            BlueprintPlacedInstanceTransformCommandResult mirror;
            BlueprintEntryCommandResult entry = null;
            var startedMirror = false;
            if (transform != null &&
                transform.Active &&
                string.Equals(transform.Mode, BlueprintPlacedInstanceTransformModes.Mirror, StringComparison.Ordinal))
            {
                mirror = BlueprintPlacedInstanceTransformState.Cancel();
            }
            else
            {
                BlueprintCreationMaskState.Cancel();
                BlueprintPlacementPreviewState.Cancel();
                BlueprintEraseRegionState.Cancel();
                mirror = BlueprintPlacedInstanceTransformState.BeginMirror();
                if (mirror != null && mirror.Succeeded)
                {
                    entry = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenPlacedInstances, appSettings ?? AppSettings.CreateDefault());
                    LegacyMainUiState.SetVisible(false);
                    startedMirror = true;
                }
            }

            return BlueprintEntryHotkeyDispatchResult.FromTransform(targetId, action, chord, mirror, entry, startedMirror);
        }

        private static bool HasActiveUnifiedBlueprintAction(string targetId)
        {
            UnifiedHotkeyRuntimeBinding binding;
            return UnifiedHotkeyRuntimeService.TryGetBinding(
                UnifiedHotkeyBindingIds.ForBlueprintAction(targetId),
                out binding);
        }

        private static UnifiedHotkeySettings CreateUnifiedSettingsFromLegacyBlueprintHotkeys(HotkeySettings hotkeySettings)
        {
            // Test-only bridge for old blueprint regression fixtures. Runtime dispatch now consumes
            // blueprint.action.* unified bindings directly and never promotes legacy hotkeys.json values.
            var unified = new UnifiedHotkeySettings
            {
                ConfigVersion = UnifiedHotkeySettings.CurrentConfigVersion,
                BindingsById = new Dictionary<string, string>(StringComparer.Ordinal)
            };

            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintCreateAction);
            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintSaveAction);
            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintMoveAction);
            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintRegionAction);
            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintMirrorAction);
            AddLegacyBlueprintHotkey(unified, hotkeySettings, FeatureIds.BlueprintLibraryAction);
            return unified;
        }

        private static void AddLegacyBlueprintHotkey(
            UnifiedHotkeySettings unified,
            HotkeySettings hotkeySettings,
            string targetId)
        {
            if (unified == null ||
                hotkeySettings == null ||
                hotkeySettings.HotkeysByFeatureId == null)
            {
                return;
            }

            string hotkey;
            if (!hotkeySettings.HotkeysByFeatureId.TryGetValue(targetId, out hotkey))
            {
                return;
            }

            UnifiedHotkeyBindingUpdateResult update;
            unified.TrySetBinding(
                UnifiedHotkeyBindingIds.ForBlueprintAction(targetId),
                NormalizeLegacyChordForUnified(hotkey),
                out update);
        }

        private static string NormalizeLegacyChordForUnified(string chordText)
        {
            FeatureToggleHotkeyChord legacy;
            if (!FeatureToggleHotkeyChord.TryParse(chordText, out legacy) || legacy == null)
            {
                return chordText ?? string.Empty;
            }

            var modifier = string.Empty;
            if (string.Equals(legacy.Modifier, "Alt", StringComparison.Ordinal))
            {
                modifier = "LAlt+";
            }
            else if (string.Equals(legacy.Modifier, "Ctrl", StringComparison.Ordinal))
            {
                modifier = "LCtrl+";
            }
            else if (string.Equals(legacy.Modifier, "Shift", StringComparison.Ordinal))
            {
                modifier = "LShift+";
            }

            return modifier + (legacy.Key ?? string.Empty);
        }

        private static UnifiedHotkeyRuntimeInputState CreateUnifiedInputStateFromLegacyDownKeys(IDictionary<int, bool> downKeys)
        {
            return new UnifiedHotkeyRuntimeInputState(key => IsLegacyOrUnifiedKeyDown(downKeys, key));
        }

        private static bool IsLegacyOrUnifiedKeyDown(IDictionary<int, bool> downKeys, int key)
        {
            if (downKeys == null)
            {
                return false;
            }

            bool down;
            if (downKeys.TryGetValue(key, out down) && down)
            {
                return true;
            }

            switch (key)
            {
                case 0xA2:
                case 0xA3:
                    return downKeys.TryGetValue(VkControl, out down) && down;
                case 0xA4:
                case 0xA5:
                    return downKeys.TryGetValue(VkAlt, out down) && down;
                case 0xA0:
                case 0xA1:
                    return downKeys.TryGetValue(VkShift, out down) && down;
                default:
                    return false;
            }
        }

        private static UnifiedHotkeyRuntimeGateContext CreateGateContextForTesting(
            bool gameInputAvailable,
            string gateReason,
            bool textInputFocused)
        {
            var gate = new UnifiedHotkeyRuntimeGateContext
            {
                GameInputAvailable = gameInputAvailable,
                F5TextInputFocused = textInputFocused
            };
            if (gameInputAvailable || string.IsNullOrWhiteSpace(gateReason))
            {
                return gate;
            }

            gate.GameInputAvailable = true;
            if (string.Equals(gateReason, "textInputFocused", StringComparison.Ordinal))
            {
                gate.F5TextInputFocused = true;
            }
            else if (string.Equals(gateReason, "legacyUiActive", StringComparison.Ordinal))
            {
                gate.LegacyUiActiveInteraction = true;
            }
            else if (string.Equals(gateReason, "legacyModalActive", StringComparison.Ordinal) ||
                     string.Equals(gateReason, UnifiedHotkeyRuntimeGate.LegacyModalOpen, StringComparison.Ordinal))
            {
                gate.LegacyModalOpen = true;
            }
            else if (string.Equals(gateReason, "hotkeyCaptureActive", StringComparison.Ordinal))
            {
                gate.HotkeyCaptureActive = true;
            }
            else
            {
                gate.GameInputAvailable = false;
            }

            return gate;
        }

        private static void RecordBlueprintActionHotkeyEvent(BlueprintEntryHotkeyDispatchResult result)
        {
            if (result == null)
            {
                return;
            }

            var metadata = BuildBlueprintActionHotkeyMetadata(result);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                ScenarioNames.BlueprintActionHotkey,
                "Diagnostic",
                result.Chord,
                result.DiagnosticResultCode.ToString(),
                result.DiagnosticResultCode.ToString(),
                result.Message,
                0,
                "{}",
                "{}",
                metadata,
                "Hotkey",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string BuildBlueprintActionHotkeyMetadata(BlueprintEntryHotkeyDispatchResult result)
        {
            result = result ?? BlueprintEntryHotkeyDispatchResult.NoOp;
            var trace = BlueprintUiClickDiagnostics.GetSnapshot();
            return
                "{" +
                "\"bindingId\":\"" + EscapeJson(string.IsNullOrWhiteSpace(result.TargetId) ? string.Empty : UnifiedHotkeyBindingIds.ForBlueprintAction(result.TargetId)) + "\"," +
                "\"targetId\":\"" + EscapeJson(result.TargetId) + "\"," +
                "\"action\":\"" + EscapeJson(result.Action) + "\"," +
                "\"resultCode\":\"" + EscapeJson(result.ResultCode) + "\"," +
                "\"reason\":\"" + EscapeJson(result.Reason) + "\"," +
                "\"reasonCode\":\"" + EscapeJson(UnifiedHotkeyReasonCatalog.NormalizeRuntimeReasonCode(result.Reason)) + "\"," +
                "\"blockedReason\":\"" + EscapeJson(IsBlockedDiagnostic(result.DiagnosticResultCode) ? result.Reason : string.Empty) + "\"," +
                "\"applied\":" + BoolRaw(result.Applied) + "," +
                "\"creationClearTrace\":\"" + EscapeJson(trace.CreationLastClearReasonTrace) + "\"" +
                "}";
        }

        internal static string BuildBlueprintActionHotkeyMetadataForTesting(BlueprintEntryHotkeyDispatchResult result)
        {
            return BuildBlueprintActionHotkeyMetadata(result);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool IsBlockedDiagnostic(DiagnosticResultCode resultCode)
        {
            return resultCode == DiagnosticResultCode.BlockedByUi ||
                   resultCode == DiagnosticResultCode.BlockedByEnvironment;
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

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
                Message = UnifiedHotkeyReasonCatalog.BuildRuntimeGateMessage("蓝图动作快捷键", reason),
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

        public static BlueprintEntryHotkeyDispatchResult FromTransform(
            string targetId,
            string action,
            string chord,
            BlueprintPlacedInstanceTransformCommandResult transform,
            BlueprintEntryCommandResult entry,
            bool startedMove)
        {
            transform = transform ?? BlueprintPlacedInstanceTransformCommandResult.Create(false, false, BlueprintPlacedInstanceTransformModes.Move, string.Empty, "transformUnknown", "蓝图移动快捷键执行失败。", string.Empty, string.Empty);
            var applied = transform.Succeeded && transform.Changed && (entry == null || entry.Succeeded);
            var diagnostic = applied
                ? DiagnosticResultCode.Succeeded
                : transform.Succeeded ? DiagnosticResultCode.NotApplicable : DiagnosticResultCode.Failed;
            return new BlueprintEntryHotkeyDispatchResult
            {
                Triggered = true,
                Applied = applied,
                Chord = chord ?? string.Empty,
                TargetId = targetId ?? string.Empty,
                Action = action ?? string.Empty,
                ResultCode = transform.ResultCode ?? string.Empty,
                Reason = transform.ResultCode ?? string.Empty,
                Message = transform.Message ?? string.Empty,
                DiagnosticResultCode = diagnostic
            };
        }
    }
}
