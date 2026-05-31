using System;
using System.Globalization;
using System.Text;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class BuffPotionDirectUseExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.BuffPotionDirectUse; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var sourceContainer = GetMetadataString(execution, "SourceContainer", string.Empty);
            var sourceSlot = GetMetadataInt(execution, "SourceSlot", -1);
            var expectedItemType = GetMetadataInt(execution, "ItemType", 0);
            var expectedBuffType = GetMetadataInt(execution, "BuffType", 0);
            var expectedBuffTime = GetMetadataInt(execution, "BuffTime", 0);
            var itemName = GetMetadataString(execution, "ItemName", string.Empty);
            var buffName = GetMetadataString(execution, "BuffName", string.Empty);
            var scenario = GetMetadataString(execution, "Scenario", "BuffPotion.DirectUse");

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.BlockedByEnvironment,
                    "Local player unavailable for DirectLocalBuffPotion.",
                    sourceContainer,
                    sourceSlot,
                    expectedItemType,
                    itemName,
                    expectedBuffType,
                    buffName,
                    expectedBuffTime,
                    0,
                    0,
                    false,
                    false,
                    false,
                    false,
                    0,
                    0,
                    0,
                    "Unknown",
                    false,
                    false,
                    "NotAttempted",
                    false,
                    scenario);
            }

            bool active;
            bool dead;
            bool ghost;
            PlayerBuffCompat.TryReadPlayerAvailability(player, out active, out dead, out ghost);
            if (!active || dead || ghost)
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.BlockedByEnvironment,
                    "Player is not available for DirectLocalBuffPotion.",
                    sourceContainer,
                    sourceSlot,
                    expectedItemType,
                    itemName,
                    expectedBuffType,
                    buffName,
                    expectedBuffTime,
                    0,
                    0,
                    false,
                    false,
                    false,
                    false,
                    0,
                    0,
                    0,
                    "Unknown",
                    false,
                    false,
                    "NotAttempted",
                    false,
                    scenario);
            }

            object item;
            string itemMessage;
            if (!InventoryMutationCompat.TryGetItem(player, sourceContainer, sourceSlot, out item, out itemMessage))
            {
                return FinishWithItemReadFailure(execution, startedUtc, itemMessage, sourceContainer, sourceSlot, expectedItemType, itemName, expectedBuffType, buffName, expectedBuffTime, scenario);
            }

            int itemType;
            int stackBefore;
            int buffType;
            int buffTime;
            bool summon;
            string actualItemName;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out actualItemName, out stackBefore, out buffType, out buffTime, out summon))
            {
                return FinishWithItemReadFailure(execution, startedUtc, "Cannot read selected buff potion item.", sourceContainer, sourceSlot, expectedItemType, itemName, expectedBuffType, buffName, expectedBuffTime, scenario);
            }

            if (expectedItemType > 0 && itemType != expectedItemType)
            {
                return FinishWithItemReadFailure(execution, startedUtc, "Selected buff potion item changed before use.", sourceContainer, sourceSlot, expectedItemType, itemName, expectedBuffType, buffName, expectedBuffTime, scenario);
            }

            if (itemType <= 0 || stackBefore <= 0 || buffType <= 0 || buffTime <= 0 || summon)
            {
                return FinishWithItemReadFailure(execution, startedUtc, "Selected item is not a supported buff potion.", sourceContainer, sourceSlot, itemType, actualItemName, buffType, buffName, buffTime, scenario);
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = actualItemName;
            }

            if (expectedBuffType > 0 && buffType != expectedBuffType)
            {
                return FinishWithItemReadFailure(execution, startedUtc, "Selected buff potion buffType changed before use.", sourceContainer, sourceSlot, itemType, itemName, buffType, buffName, buffTime, scenario);
            }

            var buffTimeBefore = 0;
            PlayerBuffCompat.TryReadBuffTime(player, buffType, out buffTimeBefore);
            var buffAlreadyActive = buffTimeBefore > 0;

            bool addBuffInvoked;
            string addBuffMessage;
            if (!PlayerBuffCompat.TryAddBuff(player, buffType, buffTime, out addBuffInvoked, out addBuffMessage))
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Failed,
                    DiagnosticResultCode.Failed,
                    addBuffMessage,
                    sourceContainer,
                    sourceSlot,
                    itemType,
                    itemName,
                    buffType,
                    string.IsNullOrWhiteSpace(buffName) ? "Buff " + buffType.ToString(CultureInfo.InvariantCulture) : buffName,
                    buffTime,
                    stackBefore,
                    stackBefore,
                    false,
                    buffAlreadyActive,
                    false,
                    false,
                    buffTimeBefore,
                    buffTimeBefore,
                    0,
                    "Unknown",
                    false,
                    false,
                    "NotAttempted",
                    false,
                    scenario);
            }

            var buffTimeAfter = 0;
            PlayerBuffCompat.TryReadBuffTime(player, buffType, out buffTimeAfter);
            var buffAdded = (!buffAlreadyActive && buffTimeAfter > 0) || (buffAlreadyActive && buffTimeAfter > buffTimeBefore);
            if (!buffAdded)
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.AttemptedButUnverified,
                    DiagnosticResultCode.AttemptedButUnverified,
                    "Player.AddBuff was invoked, but buff addition or refresh was not confirmed; item was not consumed.",
                    sourceContainer,
                    sourceSlot,
                    itemType,
                    itemName,
                    buffType,
                    string.IsNullOrWhiteSpace(buffName) ? "Buff " + buffType.ToString(CultureInfo.InvariantCulture) : buffName,
                    buffTime,
                    stackBefore,
                    stackBefore,
                    false,
                    buffAlreadyActive,
                    false,
                    addBuffInvoked,
                    buffTimeBefore,
                    buffTimeAfter,
                    0,
                    "Unknown",
                    false,
                    false,
                    "NotAttempted",
                    false,
                    scenario);
            }

            bool useSoundInvoked;
            string useSoundMessage;
            var useSoundPlayed = ItemUseSoundCompat.TryPlayUseSound(player, item, out useSoundInvoked, out useSoundMessage);

            int stackAfter;
            string consumeMessage;
            var itemConsumed = InventoryMutationCompat.TryConsumeOneItem(player, sourceContainer, sourceSlot, itemType, out stackBefore, out stackAfter, out consumeMessage);
            if (!itemConsumed)
            {
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.AttemptedButUnverified,
                    DiagnosticResultCode.AttemptedButUnverified,
                    "Buff was added, but item consume failed: " + consumeMessage,
                    sourceContainer,
                    sourceSlot,
                    itemType,
                    itemName,
                    buffType,
                    string.IsNullOrWhiteSpace(buffName) ? "Buff " + buffType.ToString(CultureInfo.InvariantCulture) : buffName,
                    buffTime,
                    stackBefore,
                    stackBefore,
                    true,
                    buffAlreadyActive,
                    false,
                    addBuffInvoked,
                    buffTimeBefore,
                    buffTimeAfter,
                    0,
                    "Unknown",
                    false,
                    false,
                    "NotAttempted",
                    false,
                    scenario);
            }

            int netMode;
            string networkMode;
            bool multiplayerClient;
            InventoryMutationCompat.ReadNetworkState(out netMode, out networkMode, out multiplayerClient);
            bool syncAttempted;
            string syncMethod;
            bool syncSucceeded;
            string syncResult;
            InventoryMutationCompat.DetermineSyncResult(multiplayerClient, out syncAttempted, out syncMethod, out syncSucceeded, out syncResult);

            var status = multiplayerClient && !syncSucceeded
                ? InputActionStatus.AttemptedButUnverified
                : InputActionStatus.Succeeded;
            var code = multiplayerClient && !syncSucceeded
                ? DiagnosticResultCode.AttemptedButUnverified
                : DiagnosticResultCode.Succeeded;
            var message = multiplayerClient && !syncSucceeded
                ? "DirectLocalBuffPotion applied locally and consumed one item, but multiplayer sync is not confirmed."
                : "DirectLocalBuffPotion applied local buff and consumed one item.";

            return Finish(
                execution,
                startedUtc,
                status,
                code,
                message,
                sourceContainer,
                sourceSlot,
                itemType,
                itemName,
                buffType,
                string.IsNullOrWhiteSpace(buffName) ? "Buff " + buffType.ToString(CultureInfo.InvariantCulture) : buffName,
                buffTime,
                stackBefore,
                stackAfter,
                true,
                buffAlreadyActive,
                true,
                addBuffInvoked,
                buffTimeBefore,
                buffTimeAfter,
                netMode,
                networkMode,
                multiplayerClient,
                syncAttempted,
                syncMethod,
                syncSucceeded,
                scenario,
                useSoundInvoked,
                useSoundPlayed,
                useSoundMessage);
        }

        private InputActionExecutionStepResult FinishWithItemReadFailure(
            InputActionExecution execution,
            DateTime startedUtc,
            string message,
            string sourceContainer,
            int sourceSlot,
            int itemType,
            string itemName,
            int buffType,
            string buffName,
            int buffTime,
            string scenario)
        {
            return Finish(
                execution,
                startedUtc,
                InputActionStatus.Failed,
                DiagnosticResultCode.MissingRequiredItem,
                message,
                sourceContainer,
                sourceSlot,
                itemType,
                itemName,
                buffType,
                buffName,
                buffTime,
                0,
                0,
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                "Unknown",
                false,
                false,
                "NotAttempted",
                false,
                scenario);
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string sourceContainer,
            int sourceSlot,
            int itemType,
            string itemName,
            int buffType,
            string buffName,
            int buffTime,
            int stackBefore,
            int stackAfter,
            bool buffAdded,
            bool buffAlreadyActive,
            bool itemConsumed,
            bool addBuffInvoked,
            int buffTimeBefore,
            int buffTimeAfter,
            int netMode,
            string networkMode,
            bool multiplayerClient,
            bool syncAttempted,
            string syncMethod,
            bool syncSucceeded,
            string scenario,
            bool useSoundInvoked = false,
            bool useSoundPlayed = false,
            string useSoundMessage = "")
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            var executionMode = ActionExecutionModes.DirectLocalBuffPotion;
            var syncResult = syncSucceeded ? "Succeeded" : (syncAttempted ? "AttemptedButUnverified" : syncMethod ?? string.Empty);
            BuffPotionDiagnostics.RecordResult(message, executionMode, networkMode, syncResult);

            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                scenario,
                InputActionKind.BuffPotionDirectUse.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(executionMode, sourceContainer, sourceSlot, itemType, itemName, buffType, buffName, buffTime, stackBefore, buffTimeBefore),
                BuildAfterJson(executionMode, sourceContainer, sourceSlot, itemType, itemName, buffType, buffName, buffTime, stackAfter, buffTimeAfter, buffAdded, buffAlreadyActive, itemConsumed, netMode, networkMode, multiplayerClient, syncAttempted, syncMethod, syncSucceeded, code.ToString(), message, useSoundInvoked, useSoundPlayed, useSoundMessage),
                BuildVerificationJson(buffAdded, itemConsumed, addBuffInvoked, syncAttempted, syncSucceeded, useSoundInvoked, useSoundPlayed, useSoundMessage),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildBeforeJson(string executionMode, string sourceContainer, int sourceSlot, int itemType, string itemName, int buffType, string buffName, int buffTime, int stackBefore, int buffTimeBefore)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "executionMode", executionMode, true);
            AppendString(builder, "sourceContainer", sourceContainer, true);
            AppendRaw(builder, "sourceSlot", IntRaw(sourceSlot), true);
            AppendRaw(builder, "itemType", IntRaw(itemType), true);
            AppendString(builder, "itemName", itemName, true);
            AppendRaw(builder, "buffType", IntRaw(buffType), true);
            AppendString(builder, "buffName", buffName, true);
            AppendRaw(builder, "buffTime", IntRaw(buffTime), true);
            AppendRaw(builder, "stackBefore", IntRaw(stackBefore), true);
            AppendRaw(builder, "buffTimeBefore", IntRaw(buffTimeBefore), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildAfterJson(
            string executionMode,
            string sourceContainer,
            int sourceSlot,
            int itemType,
            string itemName,
            int buffType,
            string buffName,
            int buffTime,
            int stackAfter,
            int buffTimeAfter,
            bool buffAdded,
            bool buffAlreadyActive,
            bool itemConsumed,
            int netMode,
            string networkMode,
            bool multiplayerClient,
            bool syncAttempted,
            string syncMethod,
            bool syncSucceeded,
            string resultCode,
            string message,
            bool useSoundInvoked,
            bool useSoundPlayed,
            string useSoundMessage)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "executionMode", executionMode, true);
            AppendString(builder, "sourceContainer", sourceContainer, true);
            AppendRaw(builder, "sourceSlot", IntRaw(sourceSlot), true);
            AppendRaw(builder, "itemType", IntRaw(itemType), true);
            AppendString(builder, "itemName", itemName, true);
            AppendRaw(builder, "buffType", IntRaw(buffType), true);
            AppendString(builder, "buffName", buffName, true);
            AppendRaw(builder, "buffTime", IntRaw(buffTime), true);
            AppendRaw(builder, "stackAfter", IntRaw(stackAfter), true);
            AppendRaw(builder, "buffTimeAfter", IntRaw(buffTimeAfter), true);
            AppendRaw(builder, "buffAdded", BoolRaw(buffAdded), true);
            AppendRaw(builder, "buffAlreadyActive", BoolRaw(buffAlreadyActive), true);
            AppendRaw(builder, "itemConsumed", BoolRaw(itemConsumed), true);
            AppendRaw(builder, "useSoundInvoked", BoolRaw(useSoundInvoked), true);
            AppendRaw(builder, "useSoundPlayed", BoolRaw(useSoundPlayed), true);
            AppendString(builder, "useSoundMessage", useSoundMessage, true);
            AppendRaw(builder, "networkModeValue", IntRaw(netMode), true);
            AppendString(builder, "networkMode", networkMode, true);
            AppendRaw(builder, "multiplayerClient", BoolRaw(multiplayerClient), true);
            AppendRaw(builder, "syncAttempted", BoolRaw(syncAttempted), true);
            AppendString(builder, "syncMethod", syncMethod, true);
            AppendRaw(builder, "syncSucceeded", BoolRaw(syncSucceeded), true);
            AppendString(builder, "resultCode", resultCode, true);
            AppendString(builder, "message", message, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(bool buffAdded, bool itemConsumed, bool addBuffInvoked, bool syncAttempted, bool syncSucceeded, bool useSoundInvoked, bool useSoundPlayed, string useSoundMessage)
        {
            return "{" +
                   "\"observableChange\":" + BoolRaw(buffAdded || itemConsumed) + "," +
                   "\"changedFields\":" + (buffAdded || itemConsumed ? "[\"buffTime\",\"itemStack\"]" : "[]") + "," +
                   "\"addBuffInvoked\":" + BoolRaw(addBuffInvoked) + "," +
                   "\"buffAdded\":" + BoolRaw(buffAdded) + "," +
                   "\"itemConsumed\":" + BoolRaw(itemConsumed) + "," +
                   "\"useSoundInvoked\":" + BoolRaw(useSoundInvoked) + "," +
                   "\"useSoundPlayed\":" + BoolRaw(useSoundPlayed) + "," +
                   "\"useSoundMessage\":\"" + EscapeJson(useSoundMessage) + "\"," +
                   "\"syncAttempted\":" + BoolRaw(syncAttempted) + "," +
                   "\"syncSucceeded\":" + BoolRaw(syncSucceeded) +
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

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(EscapeJson(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
