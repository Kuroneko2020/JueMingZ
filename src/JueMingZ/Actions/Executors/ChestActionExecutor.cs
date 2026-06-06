using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class ChestActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.Chest; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, ActionMetadataKeys.Scenario, string.Empty);
            var allMode = string.Equals(scenario, ScenarioNames.FishingAutoStoreAll, StringComparison.Ordinal);
            var questFishMode = string.Equals(scenario, ScenarioNames.FishingAutoStoreQuestFish, StringComparison.Ordinal);
            var autoStackMode = string.Equals(scenario, ScenarioNames.InventoryAutoStack, StringComparison.Ordinal);
            var autoDepositCoinsMode = string.Equals(scenario, ScenarioNames.InventoryAutoDepositCoins, StringComparison.Ordinal);
            if (!questFishMode && !allMode && !autoStackMode && !autoDepositCoinsMode)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotImplemented, DiagnosticResultCode.NotImplemented, "Chest only supports selective quick stack scenarios in this build.", null, 0, string.Empty);
            }

            if (IsBlockedForSelectiveQuickStack(snapshot, autoStackMode || autoDepositCoinsMode))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "Chest transfer blocked by menu/chat/NPC chat, chest UI, or unsafe mouse item state.", null, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
            }

            if (snapshot == null || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Chest transfer blocked because local player is unavailable or inactive.", null, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, "Local player unavailable for chest transfer: " + TerrariaInputCompat.LastInputCompatError, null, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
            }

            if (allMode || autoStackMode || autoDepositCoinsMode)
            {
                var itemIds = ParseItemIdList(GetMetadataString(execution, autoStackMode
                    ? "AutoStackItemIds"
                    : autoDepositCoinsMode ? "AutoDepositCoinItemIds" : "CaughtItemIds", string.Empty));
                var selectiveSlots = ParseSlotList(GetMetadataString(execution, autoStackMode
                    ? "AutoStackInventorySlots"
                    : autoDepositCoinsMode ? "AutoDepositCoinInventorySlots" : "CaughtInventorySlots", string.Empty));
                if (itemIds.Count == 0 || selectiveSlots.Count == 0)
                {
                    var invalidContextMessage = autoStackMode
                        ? "Auto stack request did not include valid item ids and inventory slots."
                        : autoDepositCoinsMode
                            ? "Auto deposit coins request did not include valid coin item ids and inventory slots."
                            : "Caught item storage request did not include valid caught item ids and inventory slots.";
                    return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, invalidContextMessage, null, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
                }

                QuestFishStorageResult allResult;
                // Chest transfers stay on selective QuickStack/bank compat paths.
                // Invocation alone is not success; inventory decrease verifies movement.
                var allOk = autoDepositCoinsMode
                    ? AutoDepositCoinsCompat.TryMoveCoinsToNearbyBanks(player, itemIds, selectiveSlots, out allResult)
                    : autoStackMode
                        ? QuestFishStorageCompat.TrySelectiveQuickStackStackableItems(player, itemIds, selectiveSlots, out allResult)
                        : QuestFishStorageCompat.TrySelectiveQuickStackItems(player, itemIds, selectiveSlots, out allResult);
                if (!allOk)
                {
                    var allCode = IsNotApplicableMessage(allResult == null ? string.Empty : allResult.Message)
                        ? DiagnosticResultCode.NotApplicable
                        : DiagnosticResultCode.Failed;
                    var allStatus = allCode == DiagnosticResultCode.NotApplicable
                        ? InputActionStatus.NotApplicable
                        : InputActionStatus.Failed;
                    return Finish(execution, startedUtc, allStatus, allCode, allResult == null ? "Chest item transfer failed." : allResult.Message, allResult, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
                }

                if (allResult != null && allResult.InventoryCountDecreased)
                {
                    return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, allResult.Message, allResult, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
                }

                return Finish(execution, startedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, allResult == null ? "Chest item transfer invoked but was not verified." : allResult.Message, allResult, 0, ResolveStoreMode(allMode, autoStackMode, autoDepositCoinsMode));
            }

            var questFishId = GetMetadataInt(execution, "QuestFishItemId", -1);
            var slots = ParseSlotList(GetMetadataString(execution, "QuestFishInventorySlots", string.Empty));
            if (questFishId <= 0 || slots.Count == 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Quest fish storage request did not include a valid item id and inventory slot list.", null, questFishId, FishingAutoStoreModes.QuestFish);
            }

            QuestFishStorageResult result;
            // Quest fish storage uses the same selective chest path and must not edit
            // inventory or chest stacks directly.
            var ok = QuestFishStorageCompat.TrySelectiveQuickStackQuestFish(player, questFishId, slots, out result);
            if (!ok)
            {
                var code = IsNotApplicableMessage(result == null ? string.Empty : result.Message)
                    ? DiagnosticResultCode.NotApplicable
                    : DiagnosticResultCode.Failed;
                var status = code == DiagnosticResultCode.NotApplicable
                    ? InputActionStatus.NotApplicable
                    : InputActionStatus.Failed;
                return Finish(execution, startedUtc, status, code, result == null ? "Selective quest fish quick stack failed." : result.Message, result, questFishId, FishingAutoStoreModes.QuestFish);
            }

            if (result != null && result.InventoryCountDecreased)
            {
                return Finish(execution, startedUtc, InputActionStatus.Succeeded, DiagnosticResultCode.Succeeded, result.Message, result, questFishId, FishingAutoStoreModes.QuestFish);
            }

            return Finish(execution, startedUtc, InputActionStatus.AttemptedButUnverified, DiagnosticResultCode.AttemptedButUnverified, result == null ? "Selective quest fish quick stack invoked but was not verified." : result.Message, result, questFishId, FishingAutoStoreModes.QuestFish);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            QuestFishStorageResult result,
            int questFishId,
            string storeMode)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, ActionMetadataKeys.Scenario, ScenarioNames.FishingAutoStoreQuestFish),
                InputActionKind.Chest.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution, result, questFishId, storeMode),
                BuildAfterJson(execution, result, questFishId, storeMode, code.ToString(), message),
                BuildVerificationJson(result, storeMode),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
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
                    message.IndexOf("unavailable", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string ResolveStoreMode(bool allMode, bool autoStackMode, bool autoDepositCoinsMode)
        {
            if (autoStackMode)
            {
                return "AutoStack";
            }

            if (autoDepositCoinsMode)
            {
                return "AutoDepositCoins";
            }

            return allMode ? FishingAutoStoreModes.All : FishingAutoStoreModes.QuestFish;
        }

        internal static bool IsBlockedForSelectiveQuickStackForTesting(GameStateSnapshot snapshot, bool autoStackMode)
        {
            return IsBlockedForSelectiveQuickStack(snapshot, autoStackMode);
        }

        private static bool IsBlockedForSelectiveQuickStack(GameStateSnapshot snapshot, bool autoStackMode)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return true;
            }

            if (snapshot.Ui == null)
            {
                return false;
            }

            if (snapshot.Ui.IsInMainMenu ||
                snapshot.Ui.ChatOpen ||
                snapshot.Ui.NpcChatOpen)
            {
                return true;
            }

            return autoStackMode && snapshot.Ui.ChestOpen;
        }

        private static string BuildBeforeJson(InputActionExecution execution, QuestFishStorageResult result, int questFishId, string storeMode)
        {
            return "{" +
                   "\"storeMode\":\"" + EscapeJson(storeMode) + "\"," +
                   "\"questFishItemId\":" + IntRaw(questFishId) + "," +
                   "\"questFishInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "QuestFishInventorySlots", string.Empty)) + "\"," +
                   "\"caughtItemIds\":\"" + EscapeJson(GetMetadataString(execution, "CaughtItemIds", string.Empty)) + "\"," +
                   "\"caughtInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "CaughtInventorySlots", string.Empty)) + "\"," +
                   "\"autoStackItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoStackItemIds", string.Empty)) + "\"," +
                   "\"autoStackInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoStackInventorySlots", string.Empty)) + "\"," +
                   "\"autoDepositCoinItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinItemIds", string.Empty)) + "\"," +
                   "\"autoDepositCoinInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinInventorySlots", string.Empty)) + "\"," +
                   "\"autoDepositCoinsTransferPath\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinsTransferPath", string.Empty)) + "\"," +
                   "\"autoDepositCoinsEmptyBankFallback\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinsEmptyBankFallback", string.Empty)) + "\"," +
                   "\"inventoryCountBefore\":" + IntRaw(result == null ? 0 : result.InventoryCountBefore) + "," +
                   "\"slotStackBefore\":" + IntRaw(result == null ? 0 : result.SlotStackBefore) + "," +
                   "\"nearbyContainerCountBefore\":" + IntRaw(result == null ? 0 : result.NearbyContainerCountBefore) +
                   "}";
        }

        private static string BuildAfterJson(InputActionExecution execution, QuestFishStorageResult result, int questFishId, string storeMode, string resultCode, string message)
        {
            return "{" +
                   "\"storeMode\":\"" + EscapeJson(storeMode) + "\"," +
                   "\"questFishItemId\":" + IntRaw(questFishId) + "," +
                   "\"questFishInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "QuestFishInventorySlots", string.Empty)) + "\"," +
                   "\"caughtItemIds\":\"" + EscapeJson(GetMetadataString(execution, "CaughtItemIds", string.Empty)) + "\"," +
                   "\"caughtInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "CaughtInventorySlots", string.Empty)) + "\"," +
                   "\"autoStackItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoStackItemIds", string.Empty)) + "\"," +
                   "\"autoStackInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoStackInventorySlots", string.Empty)) + "\"," +
                   "\"autoDepositCoinItemIds\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinItemIds", string.Empty)) + "\"," +
                   "\"autoDepositCoinInventorySlots\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinInventorySlots", string.Empty)) + "\"," +
                   "\"autoDepositCoinsTransferPath\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinsTransferPath", string.Empty)) + "\"," +
                   "\"autoDepositCoinsEmptyBankFallback\":\"" + EscapeJson(GetMetadataString(execution, "AutoDepositCoinsEmptyBankFallback", string.Empty)) + "\"," +
                   "\"inventoryCountAfter\":" + IntRaw(result == null ? 0 : result.InventoryCountAfter) + "," +
                   "\"slotStackAfter\":" + IntRaw(result == null ? 0 : result.SlotStackAfter) + "," +
                   "\"nearbyContainerCountAfter\":" + IntRaw(result == null ? 0 : result.NearbyContainerCountAfter) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(QuestFishStorageResult result, string storeMode)
        {
            var allMode = string.Equals(storeMode, FishingAutoStoreModes.All, StringComparison.OrdinalIgnoreCase);
            var autoStackMode = string.Equals(storeMode, "AutoStack", StringComparison.OrdinalIgnoreCase);
            var autoDepositCoinsMode = string.Equals(storeMode, "AutoDepositCoins", StringComparison.OrdinalIgnoreCase);
            var selectiveQuickStackInvoked = result != null && result.Invoked && !autoDepositCoinsMode;
            var autoDepositCoinsBankMoveCoinsInvoked = result != null && result.Invoked && autoDepositCoinsMode;
            return "{" +
                   "\"storeModeAll\":" + BoolRaw(allMode) + "," +
                   "\"storeModeAutoStack\":" + BoolRaw(autoStackMode) + "," +
                   "\"storeModeAutoDepositCoins\":" + BoolRaw(autoDepositCoinsMode) + "," +
                   "\"quickStackAllMode\":false," +
                   "\"caughtItemsOnly\":" + BoolRaw(allMode) + "," +
                   "\"autoStackOnly\":" + BoolRaw(autoStackMode) + "," +
                   "\"autoDepositCoinsOnly\":" + BoolRaw(autoDepositCoinsMode) + "," +
                   "\"selectiveQuickStackInvoked\":" + BoolRaw(selectiveQuickStackInvoked) + "," +
                   "\"allQuickStackInvoked\":false," +
                   "\"caughtItemsQuickStackInvoked\":" + BoolRaw(allMode && selectiveQuickStackInvoked) + "," +
                   "\"autoStackQuickStackInvoked\":" + BoolRaw(autoStackMode && selectiveQuickStackInvoked) + "," +
                   "\"autoDepositCoinsQuickStackInvoked\":false," +
                   "\"autoDepositCoinsBankMoveCoinsInvoked\":" + BoolRaw(autoDepositCoinsBankMoveCoinsInvoked) + "," +
                   "\"autoDepositCoinsPiggySeedInvoked\":" + BoolRaw(autoDepositCoinsMode && result != null && result.FallbackInvoked && string.Equals(result.FallbackMode, "PiggyBankFirstCoin", StringComparison.Ordinal)) + "," +
                   "\"inventoryCountDecreased\":" + BoolRaw(result != null && result.InventoryCountDecreased) + "," +
                   "\"affectedOnlyRequestedSlots\":true" +
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
