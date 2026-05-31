using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class TrashSlotActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.TrashSlot; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty);
            if (!string.Equals(scenario, ScenarioNames.InventoryAutoDiscard, StringComparison.Ordinal))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "TrashSlot only supports Inventory.AutoDiscard in this build.", null);
            }

            if (IsBlocked(snapshot))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto discard blocked by menu/chat/NPC chat, inventory UI, chest UI, or unsafe mouse item state.", null);
            }

            var itemIds = ParseItemIdList(GetMetadataString(execution, "AutoDiscardItemIds", string.Empty));
            var slots = ParseSlotList(GetMetadataString(execution, "AutoDiscardInventorySlots", string.Empty));
            if (itemIds.Count == 0 || slots.Count == 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto discard request did not include valid item ids and inventory slots.", null);
            }

            AutoDiscardResult result;
            var discarded = AutoDiscardCompat.TryMoveInventorySlotsToTrash(itemIds, slots, out result);
            if (discarded && result != null && result.DiscardedStackTotal > 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            if (result != null && result.OriginalTrashSlotPathInvoked)
            {
                return Finish(execution, startedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, result.Message, result);
            }

            var code = IsNotApplicableMessage(result == null ? string.Empty : result.Message)
                ? DiagnosticResultCode.NotApplicable
                : DiagnosticResultCode.Failed;
            var status = code == DiagnosticResultCode.NotApplicable
                ? InputActionStatus.NotApplicable
                : InputActionStatus.Failed;
            return Finish(execution, startedUtc, status, code, result == null ? "Auto discard failed." : result.Message, result);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            AutoDiscardResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoDiscard),
                InputActionKind.TrashSlot.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution, result),
                BuildAfterJson(execution, result, code.ToString(), message),
                BuildVerificationJson(result),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static bool IsBlocked(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return true;
            }

            if (snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return true;
            }

            if (snapshot.Ui == null)
            {
                return false;
            }

            return snapshot.Ui.IsInMainMenu ||
                   snapshot.Ui.ChatOpen ||
                   snapshot.Ui.NpcChatOpen ||
                   snapshot.Ui.ChestOpen;
        }

        private static List<int> ParseSlotList(string raw)
        {
            var slots = new List<int>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return slots;
            }

            var parts = raw.Split(',');
            for (var index = 0; index < parts.Length; index++)
            {
                int slot;
                if (int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out slot) &&
                    slot >= 0 &&
                    slot < 58 &&
                    !slots.Contains(slot))
                {
                    slots.Add(slot);
                }
            }

            return slots;
        }

        private static List<int> ParseItemIdList(string raw)
        {
            var itemIds = new List<int>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return itemIds;
            }

            var parts = raw.Split(',');
            for (var index = 0; index < parts.Length; index++)
            {
                int itemId;
                if (int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId) &&
                    itemId > 0 &&
                    !itemIds.Contains(itemId))
                {
                    itemIds.Add(itemId);
                }
            }

            return itemIds;
        }

        private static bool IsNotApplicableMessage(string message)
        {
            return !string.IsNullOrWhiteSpace(message) &&
                   (message.IndexOf("No ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("empty", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("no longer contain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("non-listed", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildBeforeJson(InputActionExecution execution, AutoDiscardResult result)
        {
            return "{" +
                   "\"autoDiscardItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoDiscardItemIds", string.Empty)) + "\"," +
                   "\"autoDiscardInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoDiscardInventorySlots", string.Empty)) + "\"," +
                   "\"inventorySignature\":\"" + EscapeJson(GetMetadataString(execution, "InventorySignature", string.Empty)) + "\"," +
                   "\"candidateSlotCountBefore\":" + IntRaw(result == null ? GetMetadataInt(execution, "DiscardSlotCount", 0) : result.CandidateSlotCountBefore) + "," +
                   "\"candidateStackTotalBefore\":" + IntRaw(result == null ? GetMetadataInt(execution, "DiscardStackTotal", 0) : result.CandidateStackTotalBefore) + "," +
                   "\"trashItemTypeBefore\":" + IntRaw(result == null ? 0 : result.TrashItemTypeBefore) +
                   "}";
        }

        private static string BuildAfterJson(InputActionExecution execution, AutoDiscardResult result, string resultCode, string message)
        {
            return "{" +
                   "\"discardedSlots\":\"" + EscapeJson(result == null ? string.Empty : result.DiscardedSlots) + "\"," +
                   "\"discardedItemIds\":\"" + EscapeJson(result == null ? string.Empty : result.DiscardedItemIds) + "\"," +
                   "\"discardedSlotCount\":" + IntRaw(result == null ? 0 : result.DiscardedSlotCount) + "," +
                   "\"discardedStackTotal\":" + IntRaw(result == null ? 0 : result.DiscardedStackTotal) + "," +
                   "\"candidateSlotCountAfter\":" + IntRaw(result == null ? 0 : result.CandidateSlotCountAfter) + "," +
                   "\"candidateStackTotalAfter\":" + IntRaw(result == null ? 0 : result.CandidateStackTotalAfter) + "," +
                   "\"trashItemTypeAfter\":" + IntRaw(result == null ? 0 : result.TrashItemTypeAfter) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(AutoDiscardResult result)
        {
            return "{" +
                   "\"originalTrashSlotPathInvoked\":" + BoolRaw(result != null && result.OriginalTrashSlotPathInvoked) + "," +
                   "\"inventoryCountDecreased\":" + BoolRaw(result != null && result.CandidateStackTotalAfter < result.CandidateStackTotalBefore) + "," +
                   "\"listedItemsOnly\":true," +
                   "\"originalItemSlotTrashPath\":true" +
                   "}";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
