using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class ShopActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.Shop; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty);
            if (!string.Equals(scenario, ScenarioNames.InventoryAutoSell, StringComparison.Ordinal))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "Shop only supports Inventory.AutoSell in this build.", null);
            }

            var blockedReason = GetBlockedReason(snapshot);
            if (!string.IsNullOrEmpty(blockedReason))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Auto sell " + blockedReason + ".", null);
            }

            var npcIndex = GetMetadataInt(execution, "NpcIndex", -1);
            var shopIndex = GetMetadataInt(execution, "ShopIndex", -1);
            var itemIds = ParseItemIdList(GetMetadataString(execution, "AutoSellItemIds", string.Empty));
            var slots = ParseSlotList(GetMetadataString(execution, "AutoSellInventorySlots", string.Empty));
            if (npcIndex < 0 || shopIndex <= 0 || itemIds.Count == 0 || slots.Count == 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto sell request did not include a valid NPC, shop index, item ids, and inventory slots.", null);
            }

            AutoSellResult result;
            var sold = AutoSellCompat.TryOpenShopAndSell(npcIndex, shopIndex, itemIds, slots, out result);
            if (sold && result != null && result.SoldStackTotal > 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result);
            }

            if (result != null && result.ShopOpened && result.SellInvoked)
            {
                return Finish(execution, startedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, result.Message, result);
            }

            var code = IsNotApplicableMessage(result == null ? string.Empty : result.Message)
                ? DiagnosticResultCode.NotApplicable
                : DiagnosticResultCode.Failed;
            var status = code == DiagnosticResultCode.NotApplicable
                ? InputActionStatus.NotApplicable
                : InputActionStatus.Failed;
            return Finish(execution, startedUtc, status, code, result == null ? "Auto sell failed." : result.Message, result);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            AutoSellResult result)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.InventoryAutoSell),
                InputActionKind.Shop.ToString(),
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

        private static string GetBlockedReason(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return "blocked: not in world";
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return "blocked: mouse item held";
            }

            if (snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return "blocked: player unavailable";
            }

            if (snapshot.Ui == null)
            {
                return string.Empty;
            }

            if (snapshot.Ui.IsInMainMenu)
            {
                return "blocked: main menu";
            }

            if (snapshot.Ui.ChatOpen)
            {
                return "blocked: chat open";
            }

            if (snapshot.Ui.NpcChatOpen)
            {
                return "blocked: NPC chat open";
            }

            if (snapshot.Ui.ChestOpen)
            {
                return "blocked: chest UI open";
            }

            return string.Empty;
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
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildBeforeJson(InputActionExecution execution, AutoSellResult result)
        {
            return "{" +
                   "\"npcIndex\":" + IntRaw(GetMetadataInt(execution, "NpcIndex", -1)) + "," +
                   "\"npcType\":" + IntRaw(GetMetadataInt(execution, "NpcType", 0)) + "," +
                   "\"shopIndex\":" + IntRaw(GetMetadataInt(execution, "ShopIndex", -1)) + "," +
                   "\"shopNpcName\":\"" + EscapeJson(GetMetadataString(execution, "ShopNpcName", string.Empty)) + "\"," +
                   "\"autoSellItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoSellItemIds", string.Empty)) + "\"," +
                   "\"autoSellInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoSellInventorySlots", string.Empty)) + "\"," +
                   "\"inventorySignature\":\"" + EscapeJson(GetMetadataString(execution, "InventorySignature", string.Empty)) + "\"," +
                   "\"candidateSlotCountBefore\":" + IntRaw(result == null ? GetMetadataInt(execution, "SellSlotCount", 0) : result.CandidateSlotCountBefore) + "," +
                   "\"candidateStackTotalBefore\":" + IntRaw(result == null ? GetMetadataInt(execution, "SellStackTotal", 0) : result.CandidateStackTotalBefore) +
                   "}";
        }

        private static string BuildAfterJson(InputActionExecution execution, AutoSellResult result, string resultCode, string message)
        {
            return "{" +
                   "\"npcIndex\":" + IntRaw(result == null ? GetMetadataInt(execution, "NpcIndex", -1) : result.NpcIndex) + "," +
                   "\"npcType\":" + IntRaw(result == null ? GetMetadataInt(execution, "NpcType", 0) : result.NpcType) + "," +
                   "\"shopIndex\":" + IntRaw(result == null ? GetMetadataInt(execution, "ShopIndex", -1) : result.ShopIndex) + "," +
                   "\"shopNpcName\":\"" + EscapeJson(result == null ? GetMetadataString(execution, "ShopNpcName", string.Empty) : result.NpcName) + "\"," +
                   "\"shopLeftOpen\":" + BoolRaw(result != null && result.ShopLeftOpen) + "," +
                   "\"soldSlots\":\"" + EscapeJson(result == null ? string.Empty : result.SoldSlots) + "\"," +
                   "\"soldItemIds\":\"" + EscapeJson(result == null ? string.Empty : result.SoldItemIds) + "\"," +
                   "\"soldSlotCount\":" + IntRaw(result == null ? 0 : result.SoldSlotCount) + "," +
                   "\"soldStackTotal\":" + IntRaw(result == null ? 0 : result.SoldStackTotal) + "," +
                   "\"zeroValueRemovedCount\":" + IntRaw(result == null ? 0 : result.ZeroValueRemovedCount) + "," +
                   "\"candidateSlotCountAfter\":" + IntRaw(result == null ? 0 : result.CandidateSlotCountAfter) + "," +
                   "\"candidateStackTotalAfter\":" + IntRaw(result == null ? 0 : result.CandidateStackTotalAfter) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(AutoSellResult result)
        {
            return "{" +
                   "\"shopOpened\":" + BoolRaw(result != null && result.ShopOpened) + "," +
                   "\"shopRestored\":" + BoolRaw(result != null && result.ShopRestored) + "," +
                   "\"shopLeftOpen\":" + BoolRaw(result != null && result.ShopLeftOpen) + "," +
                   "\"sellInvoked\":" + BoolRaw(result != null && result.SellInvoked) + "," +
                   "\"inventoryCountDecreased\":" + BoolRaw(result != null && result.CandidateStackTotalAfter < result.CandidateStackTotalBefore) + "," +
                   "\"listedItemsOnly\":true," +
                   "\"originalShopPath\":true" +
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
