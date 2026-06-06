using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.InventoryAndItems
{
    public static class QuickItemHotkeyService
    {
        private const int VkShift = 0x10;
        private const int VkControl = 0x11;
        private const int VkAlt = 0x12;
        private const string QuickItemScenario = "Hotkey.QuickItemHotkeys";
        private static readonly TimeSpan TriggerDebounce = TimeSpan.FromMilliseconds(220);
        private static readonly Dictionary<string, bool> WasDownByBindingKey = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static DateTime _lastTriggerUtc = DateTime.MinValue;

        internal static InputActionRequest BuildUseRequestForTesting(int slot, int itemType, string itemName, int requestedItemType, string displayName, string sourceHotkey)
        {
            return BuildUseRequest(slot, itemType, itemName, requestedItemType, displayName, sourceHotkey);
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState)
        {
            Tick(queue, gameState, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            _ = runtimeState;
            if (queue == null)
            {
                return;
            }

            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var hotkeySettings = ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault();
            var bindings = hotkeySettings.QuickItemHotkeyBindings;
            if (bindings == null || bindings.Count <= 0)
            {
                SyncTrackedStates(new HashSet<string>(StringComparer.Ordinal));
                return;
            }

            var liveBindingKeys = new HashSet<string>(StringComparer.Ordinal);
            var canTrigger = settingsSnapshot.InventoryQuickItemHotkeysEnabled &&
                             gameState != null &&
                             gameState.IsInWorld &&
                             !IsInputBlocked() &&
                             IsCurrentProcessForeground();
            var now = DateTime.UtcNow;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                HotkeyChord chord;
                if (!TryParseHotkey(binding.Hotkey, out chord))
                {
                    continue;
                }

                var bindingKey = index.ToString(CultureInfo.InvariantCulture) + ":" + chord.Normalized;
                liveBindingKeys.Add(bindingKey);
                var isDown = IsChordDown(chord);
                bool wasDown;
                WasDownByBindingKey.TryGetValue(bindingKey, out wasDown);
                WasDownByBindingKey[bindingKey] = isDown;
                if (!canTrigger || !isDown || wasDown || now - _lastTriggerUtc < TriggerDebounce)
                {
                    continue;
                }

                if (TryHandleBinding(queue, binding, chord))
                {
                    _lastTriggerUtc = now;
                    break;
                }
            }

            SyncTrackedStates(liveBindingKeys);
        }

        private static bool TryHandleBinding(InputActionQueue queue, QuickItemHotkeyBinding binding, HotkeyChord chord)
        {
            if (queue == null || binding == null || chord == null)
            {
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return false;
            }

            var requestedItemType = GetPrimaryItemType(binding.ItemTypes);
            var candidateItemTypes = BuildCandidateItemTypes(binding.ItemTypes);
            if (candidateItemTypes.Count <= 0)
            {
                return false;
            }

            int selectedSlot;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            int slot;
            int itemType;
            string itemName;
            if (!TryFindMatchingSlot(player, selectedSlot, candidateItemTypes, out slot, out itemType, out itemName))
            {
                DiagnosticActionRecorder.RecordHotkeyEvent(
                    chord.Display,
                    "Hotkey.QuickItemHotkeys",
                    DiagnosticResultCode.NotApplicable,
                    "Quick item hotkey pressed, but no matching item was found.");
                return false;
            }

            var request = BuildUseRequest(slot, itemType, itemName, requestedItemType, binding.DisplayName, chord.Display);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                DiagnosticActionRecorder.RecordHotkeyEvent(
                    chord.Display,
                    "Hotkey.QuickItemHotkeys",
                DiagnosticResultCode.Failed,
                    "Quick item hotkey request was not admitted: " + (admission == null ? "unknown" : admission.Reason));
                return false;
            }

            DiagnosticActionRecorder.RecordHotkeyEvent(
                chord.Display,
                "Hotkey.QuickItemHotkeys",
                DiagnosticResultCode.Queued,
                "Quick item hotkey " + admission.Status.ToLowerInvariant() + " UseHotbarItem for slot " + (slot + 1).ToString(CultureInfo.InvariantCulture) + ".");
            return true;
        }

        private static InputActionRequest BuildUseRequest(int slot, int itemType, string itemName, int requestedItemType, string displayName, string sourceHotkey)
        {
            var slotText = slot.ToString(CultureInfo.InvariantCulture);
            var request = new InputActionRequest
            {
                Kind = InputActionKind.UseHotbarItem,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.SupersedePending,
                SourceFeatureId = FeatureIds.InventoryQuickItemHotkeys,
                Description = "Quick item hotkey use",
                QueueTimeout = TimeSpan.FromMilliseconds(400),
                AdmissionKey = FeatureIds.InventoryQuickItemHotkeys + "|" + (sourceHotkey ?? string.Empty) + "|" + requestedItemType.ToString(CultureInfo.InvariantCulture),
                Timeout = TimeSpan.FromSeconds(5)
            };
            request.Metadata["Slot"] = slotText;
            request.Metadata[ActionMetadataKeys.TargetSlot] = slotText;
            request.Metadata[ActionMetadataKeys.Scenario] = QuickItemScenario;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Hotkey";
            request.Metadata["SourceHotkey"] = sourceHotkey ?? string.Empty;
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["QuickItemDisplayName"] = displayName ?? string.Empty;
            request.Metadata["QuickItemItemType"] = itemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["QuickItemItemName"] = itemName ?? string.Empty;
            if (requestedItemType > 0)
            {
                request.Metadata["QuickItemBoundItemType"] = requestedItemType.ToString(CultureInfo.InvariantCulture);
                request.Metadata["TargetItemTypeOverride"] = requestedItemType.ToString(CultureInfo.InvariantCulture);
            }

            return request;
        }

        private static bool TryFindMatchingSlot(
            object player,
            int selectedSlot,
            HashSet<int> candidateItemTypes,
            out int slot,
            out int itemType,
            out string itemName)
        {
            slot = -1;
            itemType = 0;
            itemName = string.Empty;
            if (player == null || candidateItemTypes == null || candidateItemTypes.Count <= 0)
            {
                return false;
            }

            IList inventory;
            string message;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            if (TryMatchSlot(inventory, selectedSlot, candidateItemTypes, out slot, out itemType, out itemName))
            {
                return true;
            }

            for (var index = 0; index <= 9; index++)
            {
                if (index == selectedSlot)
                {
                    continue;
                }

                if (TryMatchSlot(inventory, index, candidateItemTypes, out slot, out itemType, out itemName))
                {
                    return true;
                }
            }

            for (var index = 10; index < 50; index++)
            {
                if (TryMatchSlot(inventory, index, candidateItemTypes, out slot, out itemType, out itemName))
                {
                    return true;
                }
            }

            return TryMatchSlot(inventory, 58, candidateItemTypes, out slot, out itemType, out itemName);
        }

        private static bool TryMatchSlot(
            IList inventory,
            int slot,
            HashSet<int> candidateItemTypes,
            out int itemSlot,
            out int itemType,
            out string itemName)
        {
            itemSlot = -1;
            itemType = 0;
            itemName = string.Empty;
            if (inventory == null ||
                !TerrariaInputCompat.IsSupportedItemUseSlot(slot) ||
                slot < 0 ||
                slot >= inventory.Count)
            {
                return false;
            }

            var item = inventory[slot];
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                return false;
            }

            if (itemType <= 0 || stack <= 0 || !candidateItemTypes.Contains(itemType))
            {
                return false;
            }

            itemSlot = slot;
            return true;
        }

        private static HashSet<int> BuildCandidateItemTypes(List<int> itemTypes)
        {
            var set = new HashSet<int>();
            if (itemTypes == null)
            {
                return set;
            }

            for (var index = 0; index < itemTypes.Count; index++)
            {
                var itemType = itemTypes[index];
                if (itemType <= 0)
                {
                    continue;
                }

                ItemSwapFamilyCompat.AddEquivalentItemTypes(itemType, set);
            }

            return set;
        }

        private static int GetPrimaryItemType(List<int> itemTypes)
        {
            if (itemTypes == null)
            {
                return 0;
            }

            for (var index = 0; index < itemTypes.Count; index++)
            {
                var itemType = itemTypes[index];
                if (itemType > 0)
                {
                    return itemType;
                }
            }

            return 0;
        }

        private static bool IsInputBlocked()
        {
            if (LegacyMainUiState.Visible || LegacyUiInput.IsActiveInteraction() || LegacyTextInput.IsAnyFocused)
            {
                return true;
            }

            bool focused;
            string reason;
            if (TerrariaInputCompat.TryReadTextInputFocus(out focused, out reason) && focused)
            {
                return true;
            }

            return focused;
        }

        private static void SyncTrackedStates(HashSet<string> liveBindingKeys)
        {
            if (liveBindingKeys == null)
            {
                WasDownByBindingKey.Clear();
                return;
            }

            var remove = new List<string>();
            foreach (var pair in WasDownByBindingKey)
            {
                if (!liveBindingKeys.Contains(pair.Key))
                {
                    remove.Add(pair.Key);
                }
            }

            for (var index = 0; index < remove.Count; index++)
            {
                WasDownByBindingKey.Remove(remove[index]);
            }
        }

        private static bool TryParseHotkey(string text, out HotkeyChord chord)
        {
            chord = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split('+');
            var hasCtrl = false;
            var hasAlt = false;
            var hasShift = false;
            var keyToken = string.Empty;
            for (var index = 0; index < parts.Length; index++)
            {
                var part = parts[index];
                if (part == null)
                {
                    continue;
                }

                var token = part.Trim();
                if (token.Length <= 0)
                {
                    continue;
                }

                if (string.Equals(token, "Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(token, "Control", StringComparison.OrdinalIgnoreCase))
                {
                    hasCtrl = true;
                    continue;
                }

                if (string.Equals(token, "Alt", StringComparison.OrdinalIgnoreCase))
                {
                    hasAlt = true;
                    continue;
                }

                if (string.Equals(token, "Shift", StringComparison.OrdinalIgnoreCase))
                {
                    hasShift = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(keyToken))
                {
                    return false;
                }

                keyToken = token;
            }

            if (keyToken.Length <= 0)
            {
                return false;
            }

            int keyCode;
            if (!TryParseVirtualKey(keyToken, out keyCode))
            {
                return false;
            }

            var normalized = (hasCtrl ? "Ctrl+" : string.Empty) +
                             (hasAlt ? "Alt+" : string.Empty) +
                             (hasShift ? "Shift+" : string.Empty) +
                             keyToken.ToUpperInvariant();
            chord = new HotkeyChord
            {
                Ctrl = hasCtrl,
                Alt = hasAlt,
                Shift = hasShift,
                KeyCode = keyCode,
                Normalized = normalized,
                Display = normalized
            };
            return true;
        }

        private static bool TryParseVirtualKey(string token, out int keyCode)
        {
            keyCode = 0;
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var text = token.Trim();
            if (text.Length == 1)
            {
                var c = char.ToUpperInvariant(text[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    keyCode = c;
                    return true;
                }

                if (c >= '0' && c <= '9')
                {
                    keyCode = c;
                    return true;
                }
            }

            var upper = text.ToUpperInvariant();
            if (upper.Length > 1 && upper[0] == 'F')
            {
                int fn;
                if (int.TryParse(upper.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out fn) &&
                    fn >= 1 &&
                    fn <= 24)
                {
                    keyCode = 0x6F + fn;
                    return true;
                }
            }

            switch (upper)
            {
                case "MOUSE1":
                    keyCode = 0x01;
                    return true;
                case "MOUSE2":
                    keyCode = 0x02;
                    return true;
                case "MOUSE3":
                    keyCode = 0x04;
                    return true;
                case "MOUSE4":
                    keyCode = 0x05;
                    return true;
                case "MOUSE5":
                    keyCode = 0x06;
                    return true;
                case "CAPS":
                case "CAPSLOCK":
                    keyCode = 0x14;
                    return true;
                case "LEFT":
                    keyCode = 0x25;
                    return true;
                case "UP":
                    keyCode = 0x26;
                    return true;
                case "RIGHT":
                    keyCode = 0x27;
                    return true;
                case "DOWN":
                    keyCode = 0x28;
                    return true;
                case "SPACE":
                    keyCode = 0x20;
                    return true;
                case "TAB":
                    keyCode = 0x09;
                    return true;
                case "ENTER":
                    keyCode = 0x0D;
                    return true;
                case "ESC":
                case "ESCAPE":
                    keyCode = 0x1B;
                    return true;
            }

            return false;
        }

        private static bool IsChordDown(HotkeyChord chord)
        {
            if (chord == null)
            {
                return false;
            }

            return (!chord.Ctrl || IsKeyDown(VkControl)) &&
                   (!chord.Alt || IsKeyDown(VkAlt)) &&
                   (!chord.Shift || IsKeyDown(VkShift)) &&
                   IsKeyDown(chord.KeyCode);
        }

        private static bool IsKeyDown(int keyCode)
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            return (GetAsyncKeyState(keyCode) & 0x8000) != 0;
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

        private sealed class HotkeyChord
        {
            public bool Ctrl { get; set; }
            public bool Alt { get; set; }
            public bool Shift { get; set; }
            public int KeyCode { get; set; }
            public string Normalized { get; set; }
            public string Display { get; set; }
        }
    }
}
