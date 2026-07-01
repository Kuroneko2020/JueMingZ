using System;
using System.Collections.Generic;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Automation.Information;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeyRuntimeSwitchFeatureToggleUsesUnifiedOnly()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            FeatureToggleHotkeyService.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = false;

            var legacyHotkeys = HotkeySettings.CreateDefault();
            legacyHotkeys.ToggleHotkeysByTargetId[FeatureIds.InventoryAutoStack] = "K";
            var unified = CreateEmptyUnifiedHotkeySettings();

            var ignored = FeatureToggleHotkeyService.TickUnifiedForTesting(
                settings,
                legacyHotkeys,
                unified,
                UnifiedHotkeyDownKeys(0x4B),
                new UnifiedHotkeyRuntimeGateContext());
            AssertFeatureToggleHotkeyNoTrigger(ignored, "legacy feature-toggle binding ignored by unified runtime");
            if (settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Old feature-toggle hotkeys must not enable the unified runtime path.");
            }

            UnifiedHotkeyBindingUpdateResult update;
            if (!unified.TrySetBinding(UnifiedHotkeyBindingIds.ForFeatureToggleTarget(FeatureIds.InventoryAutoStack), "K", out update))
            {
                throw new InvalidOperationException("Expected unified feature-toggle binding to save.");
            }

            UnifiedHotkeyRuntimeService.ResetForTesting();
            var triggered = FeatureToggleHotkeyService.TickUnifiedForTesting(
                settings,
                legacyHotkeys,
                unified,
                UnifiedHotkeyDownKeys(0x4B),
                new UnifiedHotkeyRuntimeGateContext());
            AssertFeatureToggleHotkeyApplied(triggered, FeatureIds.InventoryAutoStack, "On", "unified feature-toggle binding");
            if (!settings.InventoryAutoStackEnabled)
            {
                throw new InvalidOperationException("Unified feature-toggle binding should toggle the target feature.");
            }

            FeatureToggleHotkeyService.TickUnifiedForTesting(
                settings,
                legacyHotkeys,
                unified,
                UnifiedHotkeyDownKeys(),
                new UnifiedHotkeyRuntimeGateContext());
            var blocked = FeatureToggleHotkeyService.TickUnifiedForTesting(
                settings,
                legacyHotkeys,
                unified,
                UnifiedHotkeyDownKeys(0x4B),
                new UnifiedHotkeyRuntimeGateContext { F5TextInputFocused = true });
            AssertFeatureToggleHotkeyBlocked(
                blocked,
                FeatureIds.InventoryAutoStack,
                UnifiedHotkeyRuntimeGate.F5TextInputFocused,
                "unified feature-toggle gate");
        }

        private static void UnifiedHotkeyRuntimeSwitchBlueprintUsesUnifiedOnly()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            BlueprintEntryHotkeyService.ResetForTesting();
            BlueprintEntryState.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            var unified = CreateEmptyUnifiedHotkeySettings();

            var ignored = BlueprintEntryHotkeyService.TickUnifiedForTesting(
                settings,
                unified,
                UnifiedHotkeyDownKeys(0x4B),
                new UnifiedHotkeyRuntimeGateContext());
            if (ignored == null || ignored.Triggered)
            {
                throw new InvalidOperationException("Old blueprint action hotkeys must not trigger without a unified binding.");
            }

            UnifiedHotkeyBindingUpdateResult update;
            if (!unified.TrySetBinding(UnifiedHotkeyBindingIds.ForBlueprintAction(FeatureIds.BlueprintCreateAction), "K", out update))
            {
                throw new InvalidOperationException("Expected unified blueprint create binding to save.");
            }

            UnifiedHotkeyRuntimeService.ResetForTesting();
            var triggered = BlueprintEntryHotkeyService.TickUnifiedForTesting(
                settings,
                unified,
                UnifiedHotkeyDownKeys(0x4B),
                new UnifiedHotkeyRuntimeGateContext());
            if (triggered == null ||
                !triggered.Triggered ||
                !string.Equals(triggered.TargetId, FeatureIds.BlueprintCreateAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unified blueprint create binding to trigger the action service.");
            }

            var snapshot = BlueprintEntryState.GetSnapshot(settings);
            if (snapshot == null || !string.Equals(snapshot.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unified blueprint create binding should enter create mode.");
            }
        }

        private static void UnifiedHotkeyRuntimeSwitchActionBindingsUseUnifiedIds()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            var settings = CreateEmptyUnifiedHotkeySettings();
            UnifiedHotkeyBindingUpdateResult update;
            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.ForQuickItemSlot(0), "MouseMiddle", out update) ||
                !settings.TrySetBinding(UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger, "LAlt+M", out update))
            {
                throw new InvalidOperationException("Expected unified action bindings to save.");
            }

            var signature = settings.CreateCacheSignature();
            var quickItem = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(UnifiedHotkeyDownKeys(0x04)));
            if (!quickItem.PressedEdge || !string.Equals(quickItem.ResultCode, "triggered", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected quick item slot binding id to trigger through unified runtime.");
            }

            UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(UnifiedHotkeyDownKeys()));
            var autoMining = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(UnifiedHotkeyDownKeys(0xA4, 0x4D)));
            if (!autoMining.PressedEdge || !string.Equals(autoMining.Display, "LAlt + M", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto mining trigger binding id to trigger through unified runtime.");
            }
        }

        private static void UnifiedHotkeyRuntimeSwitchQuickAnnouncementConsumesUnifiedTrigger()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            MapQuickAnnouncementRuntimeService.ResetForTesting();
            var settings = UnifiedHotkeySettings.CreateDefault();
            var signature = settings.CreateCacheSignature();
            var trigger = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(UnifiedHotkeyDownKeys(0xA4, 0xA0, 0x01)));
            if (!trigger.PressedEdge || !string.Equals(trigger.ResultCode, "triggered", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected default quick announcement unified chord to trigger.");
            }

            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            input.UseUnifiedHotkeyRuntime = true;
            input.UnifiedHotkeyTrigger = trigger;
            input.UnifiedHotkeyTriggerKey = "MouseLeft";
            input.UnifiedHotkeySignature = trigger.Display;
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            if (result == null ||
                !result.Triggered ||
                !result.InputConsumeAttempted ||
                !result.Delivered)
            {
                throw new InvalidOperationException("Expected quick announcement runtime to consume and deliver from unified trigger state.");
            }
        }

        private static void UnifiedHotkeyRuntimeSwitchQuickAnnouncementRecordsBlockedReason()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            MapQuickAnnouncementRuntimeService.ResetForTesting();
            MapQuickAnnouncementDiagnostics.ResetForTesting();
            var settings = UnifiedHotkeySettings.CreateDefault();
            var signature = settings.CreateCacheSignature();
            var trigger = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger,
                new UnifiedHotkeyRuntimeGateContext
                {
                    TerrariaTextInputFocused = true,
                    TerrariaTextInputReason = UnifiedHotkeyRuntimeGate.TextInputFocused
                },
                UnifiedHotkeyRuntimeInputState.FromDictionary(UnifiedHotkeyDownKeys(0xA4, 0xA0, 0x01)));
            if (!trigger.PressedEdge ||
                !string.Equals(trigger.ResultCode, "blocked", StringComparison.Ordinal) ||
                !string.Equals(trigger.Reason, UnifiedHotkeyRuntimeGate.TextInputFocused, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected default quick announcement unified chord to be blocked by text input focus.");
            }

            var input = CreateQuickAnnouncementRuntimeInput("MouseLeft");
            input.UseUnifiedHotkeyRuntime = true;
            input.UnifiedHotkeyTrigger = trigger;
            input.UnifiedHotkeyTriggerKey = "MouseLeft";
            input.UnifiedHotkeySignature = trigger.Display;
            var probe = new RecordingQuickAnnouncementRuntimeProbe();
            var result = MapQuickAnnouncementRuntimeService.TickForTesting(input, probe.ToPorts());
            var snapshot = MapQuickAnnouncementDiagnostics.GetSnapshot();
            if (result == null ||
                !result.Triggered ||
                !string.Equals(result.ResultCode, "blocked", StringComparison.Ordinal) ||
                !string.Equals(snapshot.LastResultCode, "blocked", StringComparison.Ordinal) ||
                !string.Equals(snapshot.LastFailureReason, UnifiedHotkeyRuntimeGate.TextInputFocused, StringComparison.Ordinal) ||
                !string.Equals(snapshot.LastHotkeySummary, trigger.Display, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected quick announcement blocked unified trigger reason to be recorded in diagnostics.");
            }
        }

        private static UnifiedHotkeySettings CreateEmptyUnifiedHotkeySettings()
        {
            return new UnifiedHotkeySettings
            {
                ConfigVersion = UnifiedHotkeySettings.CurrentConfigVersion,
                BindingsById = new Dictionary<string, string>(StringComparer.Ordinal)
            };
        }

        private static Dictionary<int, bool> UnifiedHotkeyDownKeys(params int[] keys)
        {
            var result = new Dictionary<int, bool>();
            for (var index = 0; keys != null && index < keys.Length; index++)
            {
                if (keys[index] > 0)
                {
                    result[keys[index]] = true;
                }
            }

            return result;
        }
    }
}
