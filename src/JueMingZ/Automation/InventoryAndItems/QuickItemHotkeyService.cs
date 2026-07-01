using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Input.Hotkeys;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.InventoryAndItems
{
    // Hotkeys are explicit user commands, but they still enqueue item use instead of writing selectedItem or input flags here.
    public static class QuickItemHotkeyService
    {
        private const string QuickItemScenario = "Hotkey.QuickItemHotkeys";
        private static readonly TimeSpan TriggerDebounce = TimeSpan.FromMilliseconds(220);
        private static DateTime _lastTriggerUtc = DateTime.MinValue;

        internal static InputActionRequest BuildUseRequestForTesting(int slot, int itemType, string itemName, int requestedItemType, string displayName, string sourceHotkey)
        {
            return BuildUseRequest(slot, itemType, itemName, requestedItemType, displayName, sourceHotkey);
        }

        internal static bool TryNormalizeHotkeyForTesting(string text, out string normalized)
        {
            var parse = HotkeyParser.Parse(text);
            if (parse.Succeeded)
            {
                normalized = parse.Normalized;
                return true;
            }

            normalized = string.Empty;
            return false;
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
            // Rows in hotkeys.json still describe the bound item set; the trigger chord lives only in
            // unified-hotkeys.json as inventory.quick_item.slotN and is consumed through the runtime cache.
            var bindings = hotkeySettings.QuickItemHotkeyBindings;
            if (bindings == null || bindings.Count <= 0)
            {
                return;
            }

            var canTrigger = settingsSnapshot.InventoryQuickItemHotkeysEnabled &&
                             gameState != null &&
                             gameState.IsInWorld &&
                             !gameState.IsInMainMenu;
            if (!canTrigger)
            {
                return;
            }

            var now = DateTime.UtcNow;
            for (var index = 0; index < bindings.Count; index++)
            {
                var binding = bindings[index];
                if (binding == null || !binding.Enabled)
                {
                    continue;
                }

                var bindingId = UnifiedHotkeyBindingIds.ForQuickItemSlot(index);
                var trigger = UnifiedHotkeyRuntimeService.QueryBinding(bindingId);
                if (!trigger.PressedEdge)
                {
                    continue;
                }

                if (string.Equals(trigger.ResultCode, "blocked", StringComparison.Ordinal))
                {
                    RecordQuickItemHotkeyEvent(
                        trigger.Display,
                        UnifiedHotkeyReasonCatalog.IsEnvironmentGateReason(trigger.Reason)
                            ? DiagnosticResultCode.BlockedByEnvironment
                            : DiagnosticResultCode.BlockedByUi,
                        UnifiedHotkeyReasonCatalog.BuildRuntimeGateMessage("快捷物品 " + (index + 1).ToString(CultureInfo.InvariantCulture), trigger.Reason),
                        bindingId,
                        trigger.ResultCode,
                        trigger.Reason,
                        binding == null ? string.Empty : binding.DisplayName);
                    continue;
                }

                if (!string.Equals(trigger.ResultCode, "triggered", StringComparison.Ordinal) ||
                    now - _lastTriggerUtc < TriggerDebounce)
                {
                    continue;
                }

                if (TryHandleBinding(queue, binding, trigger.Display, bindingId))
                {
                    _lastTriggerUtc = now;
                    break;
                }
            }
        }

        private static bool TryHandleBinding(InputActionQueue queue, QuickItemHotkeyBinding binding, string sourceHotkey, string bindingId)
        {
            sourceHotkey = sourceHotkey ?? string.Empty;
            bindingId = bindingId ?? string.Empty;
            if (queue == null || binding == null || sourceHotkey.Length <= 0)
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
                RecordQuickItemHotkeyEvent(
                    sourceHotkey,
                    DiagnosticResultCode.NotApplicable,
                    "Quick item hotkey pressed, but no matching item was found.",
                    bindingId,
                    "triggered",
                    "noMatchingItem",
                    binding.DisplayName);
                return false;
            }

            var request = BuildUseRequest(slot, itemType, itemName, requestedItemType, binding.DisplayName, sourceHotkey);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                var reason = admission == null ? "admissionUnknown" : admission.Reason;
                RecordQuickItemHotkeyEvent(
                    sourceHotkey,
                    DiagnosticResultCode.Failed,
                    "Quick item hotkey request was not admitted: " + (admission == null ? "unknown" : admission.Reason),
                    bindingId,
                    "triggered",
                    reason,
                    binding.DisplayName);
                return false;
            }

            RecordQuickItemHotkeyEvent(
                sourceHotkey,
                DiagnosticResultCode.Queued,
                "Quick item hotkey " + admission.Status.ToLowerInvariant() + " UseHotbarItem for slot " + (slot + 1).ToString(CultureInfo.InvariantCulture) + ".",
                bindingId,
                "triggered",
                string.Empty,
                binding.DisplayName);
            return true;
        }

        private static void RecordQuickItemHotkeyEvent(
            string hotkey,
            DiagnosticResultCode resultCode,
            string message,
            string bindingId,
            string hotkeyResultCode,
            string reason,
            string displayName)
        {
            hotkey = hotkey ?? string.Empty;
            reason = reason ?? string.Empty;
            message = message ?? string.Empty;
            DiagnosticActionRecorder.RecordHotkeyEvent(
                hotkey,
                QuickItemScenario,
                resultCode,
                message,
                UnifiedHotkeyReasonCatalog.BuildDiagnosticMetadataJson(
                    "bindingId", bindingId,
                    "resultCode", resultCode.ToString(),
                    "hotkeyResultCode", hotkeyResultCode,
                    "reason", reason,
                    "reasonCode", UnifiedHotkeyReasonCatalog.NormalizeRuntimeReasonCode(reason),
                    "blockedReason", resultCode == DiagnosticResultCode.BlockedByUi || resultCode == DiagnosticResultCode.BlockedByEnvironment ? reason : string.Empty,
                    "displayName", displayName,
                    "playerMessage", message));
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

    }
}
