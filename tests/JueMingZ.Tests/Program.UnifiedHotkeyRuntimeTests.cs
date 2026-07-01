using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeyRuntimeCacheRefreshesOnlyWhenSignatureChanges()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult update;
            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.ForQuickItemSlot(0), "MouseMiddle", out update))
            {
                throw new InvalidOperationException("Expected runtime cache sample binding to save.");
            }

            var cache = new UnifiedHotkeyRuntimeBindingCache();
            var signature = settings.CreateCacheSignature();
            var first = cache.GetSnapshot(settings, signature);
            var second = cache.GetSnapshot(settings, signature);
            if (!object.ReferenceEquals(first, second) || cache.RebuildCount != 1)
            {
                throw new InvalidOperationException("Runtime hotkey cache must not rebuild while the config signature is unchanged.");
            }

            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.ForQuickItemSlot(0), "LCtrl+Num1", out update))
            {
                throw new InvalidOperationException("Expected runtime cache changed binding to save.");
            }

            var changedSignature = settings.CreateCacheSignature();
            var third = cache.GetSnapshot(settings, changedSignature);
            if (object.ReferenceEquals(second, third) || cache.RebuildCount != 2)
            {
                throw new InvalidOperationException("Runtime hotkey cache must rebuild when the config signature changes.");
            }

            UnifiedHotkeyRuntimeBinding binding;
            if (!cache.TryGetBinding(settings, changedSignature, UnifiedHotkeyBindingIds.ForQuickItemSlot(0), out binding) ||
                binding == null ||
                !string.Equals(binding.Normalized, "LCtrl+Num1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected runtime hotkey cache to expose the updated parsed binding.");
            }
        }

        private static void UnifiedHotkeyRuntimeTriggerDetectsPressedEdgesAndMouseMiddle()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult update;
            var slot0 = UnifiedHotkeyBindingIds.ForQuickItemSlot(0);
            if (!settings.TrySetBinding(slot0, "LCtrl+Num1", out update))
            {
                throw new InvalidOperationException("Expected runtime trigger keyboard binding to save.");
            }

            var signature = settings.CreateCacheSignature();
            var gate = new UnifiedHotkeyRuntimeGateContext();
            var down = new Dictionary<int, bool> { { 0xA2, true }, { 0x61, true } };
            var first = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                gate,
                UnifiedHotkeyRuntimeInputState.FromDictionary(down));
            if (!first.PressedEdge || !string.Equals(first.ResultCode, "triggered", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unified runtime to report the first keyboard chord edge.");
            }

            var held = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                gate,
                UnifiedHotkeyRuntimeInputState.FromDictionary(down));
            if (held.PressedEdge || !held.WasDown || !string.Equals(held.ResultCode, "idle", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unified runtime to suppress held keyboard chords.");
            }

            UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                gate,
                UnifiedHotkeyRuntimeInputState.FromDictionary(new Dictionary<int, bool>()));

            var slot1 = UnifiedHotkeyBindingIds.ForQuickItemSlot(1);
            if (!settings.TrySetBinding(slot1, "MouseMiddle", out update))
            {
                throw new InvalidOperationException("Expected runtime trigger mouse binding to save.");
            }

            signature = settings.CreateCacheSignature();
            var mouse = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot1,
                gate,
                UnifiedHotkeyRuntimeInputState.FromDictionary(new Dictionary<int, bool> { { 0x04, true } }));
            if (!mouse.PressedEdge || !string.Equals(mouse.Display, "MouseMiddle", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unified runtime to detect MouseMiddle as a trigger edge.");
            }
        }

        private static void UnifiedHotkeyRuntimeGateReturnsRequiredReasons()
        {
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { TerrariaTextInputFocused = true, TerrariaTextInputReason = "chat" },
                UnifiedHotkeyRuntimeGate.TextInputFocused);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { MainMenu = true },
                UnifiedHotkeyRuntimeGate.MainMenu);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { NpcChatOpen = true },
                UnifiedHotkeyRuntimeGate.NpcChatOpen);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { LegacyModalOpen = true },
                UnifiedHotkeyRuntimeGate.LegacyModalOpen);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { F5TextInputFocused = true },
                UnifiedHotkeyRuntimeGate.F5TextInputFocused);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { ColorInputFocused = true },
                UnifiedHotkeyRuntimeGate.ColorInputFocused);
            AssertUnifiedHotkeyGateReason(
                new UnifiedHotkeyRuntimeGateContext { NameInputFocused = true },
                UnifiedHotkeyRuntimeGate.NameInputFocused);
        }

        private static void UnifiedHotkeyRuntimeGateBlocksTriggerAndConfigChangeSwapsBinding()
        {
            UnifiedHotkeyRuntimeService.ResetForTesting();
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult update;
            var slot0 = UnifiedHotkeyBindingIds.ForQuickItemSlot(0);
            if (!settings.TrySetBinding(slot0, "LCtrl+Num1", out update))
            {
                throw new InvalidOperationException("Expected runtime gate binding to save.");
            }

            var signature = settings.CreateCacheSignature();
            var downOld = new Dictionary<int, bool> { { 0xA2, true }, { 0x61, true } };
            var blocked = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                new UnifiedHotkeyRuntimeGateContext { NpcChatOpen = true },
                UnifiedHotkeyRuntimeInputState.FromDictionary(downOld));
            if (!string.Equals(blocked.ResultCode, "blocked", StringComparison.Ordinal) ||
                !string.Equals(blocked.Reason, UnifiedHotkeyRuntimeGate.NpcChatOpen, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected unified runtime gate to block triggered hotkeys with npcChatOpen.");
            }

            UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(new Dictionary<int, bool>()));

            if (!settings.TrySetBinding(slot0, "LAlt+Num2", out update))
            {
                throw new InvalidOperationException("Expected runtime gate changed binding to save.");
            }

            signature = settings.CreateCacheSignature();
            var oldKey = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(downOld));
            if (!string.Equals(oldKey.ResultCode, "idle", StringComparison.Ordinal) || oldKey.Down)
            {
                throw new InvalidOperationException("Expected old unified runtime binding to stop matching after config change.");
            }

            var newKey = UnifiedHotkeyRuntimeService.QueryBinding(
                settings,
                signature,
                slot0,
                new UnifiedHotkeyRuntimeGateContext(),
                UnifiedHotkeyRuntimeInputState.FromDictionary(new Dictionary<int, bool> { { 0xA4, true }, { 0x62, true } }));
            if (!newKey.PressedEdge || !string.Equals(newKey.Display, "LAlt + Num2", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected new unified runtime binding to trigger after config change.");
            }
        }

        private static void AssertUnifiedHotkeyGateReason(UnifiedHotkeyRuntimeGateContext context, string expectedReason)
        {
            var gate = UnifiedHotkeyRuntimeGate.Evaluate(context);
            if (!gate.Blocked || !string.Equals(gate.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected unified hotkey gate reason " + expectedReason + ", got " + gate.Reason + ".");
            }
        }
    }
}
