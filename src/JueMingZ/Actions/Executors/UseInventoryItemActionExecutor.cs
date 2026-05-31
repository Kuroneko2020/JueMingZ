using System;
using System.Globalization;
using System.Text;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class UseInventoryItemActionExecutor : InputActionExecutorBase
    {
        public override InputActionKind Kind { get { return InputActionKind.UseInventoryItem; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            var scenario = GetMetadataString(execution, "Scenario", "UseInventoryItem.Recovery");
            var purpose = GetMetadataString(execution, "UseInventoryItemPurpose", string.Empty);
            var sourceContainer = GetMetadataString(execution, "SourceContainer", string.Empty);
            var sourceSlot = GetMetadataInt(execution, "SourceSlot", -1);
            var expectedItemType = GetMetadataInt(execution, "ItemType", 0);
            var itemName = GetMetadataString(execution, "ItemName", string.Empty);

            ItemUseVerificationState before = null;
            ItemUseVerificationState after = null;
            if (IsBlockedForRecoveryInventoryUse(snapshot))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "UseInventoryItem blocked by world/UI state.", scenario, purpose, sourceContainer, sourceSlot, expectedItemType, itemName, 0, 0, 0, false, false, false, false, false, false, false, 0, 0, before, after);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Local player unavailable for UseInventoryItem.", scenario, purpose, sourceContainer, sourceSlot, expectedItemType, itemName, 0, 0, 0, false, false, false, false, false, false, false, 0, 0, before, after);
            }

            int selectedSlot;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);
            TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out before);

            object item;
            string itemMessage;
            if (!InventoryMutationCompat.TryGetItem(player, sourceContainer, sourceSlot, out item, out itemMessage))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.MissingRequiredItem, itemMessage, scenario, purpose, sourceContainer, sourceSlot, expectedItemType, itemName, 0, 0, 0, false, false, false, false, false, false, false, 0, 0, before, after);
            }

            int itemType;
            int stackBefore;
            int healLife;
            int healMana;
            bool potion;
            bool consumable;
            int buffType;
            int buffTime;
            string actualItemName;
            if (!InventoryMutationCompat.TryReadRecoveryItemFields(item, out itemType, out actualItemName, out stackBefore, out healLife, out healMana, out potion, out consumable, out buffType, out buffTime))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.MissingRequiredItem, "Cannot read recovery item fields.", scenario, purpose, sourceContainer, sourceSlot, expectedItemType, itemName, 0, 0, 0, false, false, false, false, false, false, false, 0, 0, before, after);
            }

            if (string.IsNullOrWhiteSpace(itemName))
            {
                itemName = actualItemName;
            }

            if (expectedItemType > 0 && itemType != expectedItemType)
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.MissingRequiredItem, "Selected recovery item changed before use.", scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, false, false, false, false, false, stackBefore, stackBefore, before, after);
            }

            if (itemType <= 0 || stackBefore <= 0 || (healLife <= 0 && healMana <= 0))
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Selected item is not a recovery item.", scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, false, false, false, false, false, stackBefore, stackBefore, before, after);
            }

            if (string.Equals(purpose, "AutoHeal", StringComparison.OrdinalIgnoreCase) && healLife <= 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Selected item cannot heal life.", scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, false, false, false, false, false, stackBefore, stackBefore, before, after);
            }

            if (string.Equals(purpose, "AutoMana", StringComparison.OrdinalIgnoreCase) && healMana <= 0)
            {
                return Finish(execution, startedUtc, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Selected item cannot restore mana.", scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, false, false, false, false, false, stackBefore, stackBefore, before, after);
            }

            var stackAfter = stackBefore;
            bool tryStartInvoked;
            string useMessage;
            if (!RecoveryItemUseCompat.TryStartUse(player, item, out tryStartInvoked, out useMessage))
            {
                if (!string.Equals(purpose, "AutoMana", StringComparison.OrdinalIgnoreCase))
                {
                    TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
                    return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByCooldown, useMessage, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, false, false, false, false, stackBefore, stackBefore, before, after);
                }

                var manaFallbackMessage = useMessage;
                bool canConsumeForMana;
                if (!RecoveryItemUseCompat.TryCanConsume(player, item, out canConsumeForMana, out useMessage) || !canConsumeForMana)
                {
                    TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
                    var blockedMessage = "AutoMana fallback did not consume " + itemName + " after ItemCheck_TryStartUse rejection: " + useMessage;
                    return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByCooldown, blockedMessage, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, false, false, false, false, stackBefore, stackBefore, before, after, false, false, string.Empty, true, manaFallbackMessage);
                }

                bool fallbackLifeManaApplied;
                if (!RecoveryItemUseCompat.TryApplyLifeAndOrMana(player, item, out fallbackLifeManaApplied, out useMessage))
                {
                    TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
                    return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, useMessage, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, false, false, fallbackLifeManaApplied, false, stackBefore, stackBefore, before, after, false, false, string.Empty, true, manaFallbackMessage);
                }

                bool fallbackUseSoundInvoked;
                string fallbackUseSoundMessage;
                var fallbackUseSoundPlayed = ItemUseSoundCompat.TryPlayUseSound(player, item, out fallbackUseSoundInvoked, out fallbackUseSoundMessage);

                string fallbackConsumeMessage;
                var fallbackItemConsumed = InventoryMutationCompat.TryConsumeOneItem(player, sourceContainer, sourceSlot, itemType, out stackBefore, out stackAfter, out fallbackConsumeMessage);
                if (!fallbackItemConsumed)
                {
                    useMessage = fallbackConsumeMessage;
                }

                TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
                var fallbackObservableRecovery = HasObservableRecovery(purpose, before, after);
                var fallbackObservableInventoryChange = fallbackItemConsumed || stackAfter < stackBefore;
                var fallbackStatus = fallbackObservableRecovery || fallbackObservableInventoryChange ? InputActionStatus.Succeeded : InputActionStatus.AttemptedButUnverified;
                var fallbackCode = fallbackStatus == InputActionStatus.Succeeded ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.AttemptedButUnverified;
                var fallbackMessage = fallbackObservableRecovery
                    ? "UseInventoryItem applied original mana recovery fallback for " + itemName + " after ItemCheck_TryStartUse rejection."
                    : "UseInventoryItem attempted original mana recovery fallback for " + itemName + " after ItemCheck_TryStartUse rejection, but mana recovery was not observed.";

                return Finish(execution, startedUtc, fallbackStatus, fallbackCode, fallbackMessage, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, false, false, fallbackLifeManaApplied, fallbackItemConsumed, stackBefore, stackAfter, before, after, fallbackUseSoundInvoked, fallbackUseSoundPlayed, fallbackUseSoundMessage, true, manaFallbackMessage);
            }

            bool potionDelayApplied = false;
            if (potion)
            {
                RecoveryItemUseCompat.TryApplyPotionDelay(player, item, out potionDelayApplied, out useMessage);
            }

            bool lifeManaApplied;
            if (!RecoveryItemUseCompat.TryApplyLifeAndOrMana(player, item, out lifeManaApplied, out useMessage))
            {
                TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, useMessage, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, potionDelayApplied, false, lifeManaApplied, false, stackBefore, stackBefore, before, after);
            }

            var itemBuffApplied = false;
            if (buffType > 0)
            {
                bool itemBuffInvoked;
                PlayerBuffCompat.TryApplyItemBuff(player, buffType, buffTime, out itemBuffInvoked, out useMessage);
                itemBuffApplied = itemBuffInvoked;
            }

            if (itemType == 5)
            {
                RecoveryItemUseCompat.TryResetHungerToNeutral(player);
            }

            bool useSoundInvoked;
            string useSoundMessage;
            var useSoundPlayed = ItemUseSoundCompat.TryPlayUseSound(player, item, out useSoundInvoked, out useSoundMessage);

            bool canConsume;
            var canConsumeRead = RecoveryItemUseCompat.TryCanConsume(player, item, out canConsume, out useMessage);
            var itemConsumed = false;
            if (canConsumeRead && canConsume)
            {
                string consumeMessage;
                itemConsumed = InventoryMutationCompat.TryConsumeOneItem(player, sourceContainer, sourceSlot, itemType, out stackBefore, out stackAfter, out consumeMessage);
                if (!itemConsumed)
                {
                    useMessage = consumeMessage;
                }
            }

            TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out after);
            var observableRecovery = HasObservableRecovery(purpose, before, after);
            var observableInventoryChange = itemConsumed || stackAfter < stackBefore;
            var status = observableRecovery || observableInventoryChange ? InputActionStatus.Succeeded : InputActionStatus.AttemptedButUnverified;
            var code = status == InputActionStatus.Succeeded ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.AttemptedButUnverified;
            var message = observableRecovery
                ? "UseInventoryItem applied original recovery item path for " + itemName + "."
                : "UseInventoryItem attempted original recovery item path for " + itemName + ", but recovery was not observed.";

            return Finish(execution, startedUtc, status, code, message, scenario, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackBefore, potion, consumable, tryStartInvoked, potionDelayApplied, itemBuffApplied, lifeManaApplied, itemConsumed, stackBefore, stackAfter, before, after, useSoundInvoked, useSoundPlayed, useSoundMessage);
        }

        private static bool HasObservableRecovery(string purpose, ItemUseVerificationState before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            if (string.Equals(purpose, "AutoMana", StringComparison.OrdinalIgnoreCase))
            {
                return after.Mana > before.Mana;
            }

            return after.Life > before.Life;
        }

        private InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            DateTime startedUtc,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message,
            string scenario,
            string purpose,
            string sourceContainer,
            int sourceSlot,
            int itemType,
            string itemName,
            int healLife,
            int healMana,
            int stack,
            bool potion,
            bool consumable,
            bool tryStartInvoked,
            bool potionDelayApplied,
            bool itemBuffApplied,
            bool lifeManaApplied,
            bool itemConsumed,
            int stackBefore,
            int stackAfter,
            ItemUseVerificationState before,
            ItemUseVerificationState after,
            bool useSoundInvoked = false,
            bool useSoundPlayed = false,
            string useSoundMessage = "",
            bool tryStartRejectedManaFallback = false,
            string manaFallbackMessage = "")
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            int netMode;
            string networkMode;
            bool multiplayerClient;
            bool syncAttempted;
            string syncMethod;
            bool syncSucceeded;
            string syncResult;
            InventoryMutationCompat.ReadNetworkState(out netMode, out networkMode, out multiplayerClient);
            InventoryMutationCompat.DetermineSyncResult(multiplayerClient, out syncAttempted, out syncMethod, out syncSucceeded, out syncResult);
            if (status == InputActionStatus.Succeeded && multiplayerClient && !syncSucceeded && (lifeManaApplied || itemConsumed))
            {
                status = InputActionStatus.AttemptedButUnverified;
                code = DiagnosticResultCode.AttemptedButUnverified;
                message = (message ?? string.Empty) + " Multiplayer sync is not confirmed for this recovery item path.";
                SetResultCode(execution, code);
            }

            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                scenario,
                InputActionKind.UseInventoryItem.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stack, potion, consumable, before),
                BuildAfterJson(execution, purpose, sourceContainer, sourceSlot, itemType, itemName, healLife, healMana, stackAfter, potionDelayApplied, itemBuffApplied, lifeManaApplied, itemConsumed, netMode, networkMode, multiplayerClient, syncAttempted, syncMethod, syncSucceeded, syncResult, code.ToString(), message, after, useSoundInvoked, useSoundPlayed, useSoundMessage, tryStartRejectedManaFallback, manaFallbackMessage),
                BuildVerificationJson(execution, tryStartInvoked, potionDelayApplied, itemBuffApplied, lifeManaApplied, itemConsumed, syncAttempted, syncMethod, syncSucceeded, syncResult, before, after, useSoundInvoked, useSoundPlayed, useSoundMessage, tryStartRejectedManaFallback, manaFallbackMessage),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildBeforeJson(InputActionExecution execution, string purpose, string sourceContainer, int sourceSlot, int itemType, string itemName, int healLife, int healMana, int stack, bool potion, bool consumable, ItemUseVerificationState before)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "purpose", purpose, true);
            AppendString(builder, "sourceContainer", sourceContainer, true);
            AppendRaw(builder, "sourceSlot", IntRaw(sourceSlot), true);
            AppendRaw(builder, "itemType", IntRaw(itemType), true);
            AppendString(builder, "itemName", itemName, true);
            AppendRaw(builder, "healLife", IntRaw(healLife), true);
            AppendRaw(builder, "healMana", IntRaw(healMana), true);
            AppendRaw(builder, "stackBefore", IntRaw(stack), true);
            AppendRaw(builder, "potion", BoolRaw(potion), true);
            AppendRaw(builder, "consumable", BoolRaw(consumable), true);
            AppendRaw(builder, "lifeBefore", IntRaw(before == null ? 0 : before.Life), true);
            AppendRaw(builder, "manaBefore", IntRaw(before == null ? 0 : before.Mana), true);
            AppendString(builder, "triggerReason", GetMetadataString(execution, "TriggerReason", string.Empty), true);
            AppendRaw(builder, "currentMana", IntRaw(GetMetadataInt(execution, "CurrentMana", before == null ? 0 : before.Mana)), true);
            AppendRaw(builder, "requiredMana", IntRaw(GetMetadataInt(execution, "RequiredMana", 0)), true);
            AppendRaw(builder, "selectedItemType", IntRaw(GetMetadataInt(execution, "SelectedItemType", 0)), true);
            AppendString(builder, "selectedItemName", GetMetadataString(execution, "SelectedItemName", string.Empty), true);
            AppendRaw(builder, "selectedItemManaCost", IntRaw(GetMetadataInt(execution, "SelectedItemManaCost", 0)), true);
            AppendRaw(builder, "checkManaAvailable", BoolRaw(GetMetadataBool(execution, "CheckManaAvailable", false)), true);
            AppendRaw(builder, "checkManaResult", BoolRaw(GetMetadataBool(execution, "CheckManaResult", false)), true);
            AppendRaw(builder, "usedFallbackManaCostCheck", BoolRaw(GetMetadataBool(execution, "UsedFallbackManaCostCheck", false)), true);
            AppendString(builder, "manaCheckReason", GetMetadataString(execution, "ManaCheckReason", string.Empty), true);
            AppendRaw(builder, "buffCountBefore", IntRaw(before == null ? 0 : before.ActiveBuffCount), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildAfterJson(InputActionExecution execution, string purpose, string sourceContainer, int sourceSlot, int itemType, string itemName, int healLife, int healMana, int stackAfter, bool potionDelayApplied, bool itemBuffApplied, bool lifeManaApplied, bool itemConsumed, int netMode, string networkMode, bool multiplayerClient, bool syncAttempted, string syncMethod, bool syncSucceeded, string syncResult, string resultCode, string message, ItemUseVerificationState after, bool useSoundInvoked, bool useSoundPlayed, string useSoundMessage, bool tryStartRejectedManaFallback, string manaFallbackMessage)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "purpose", purpose, true);
            AppendString(builder, "sourceContainer", sourceContainer, true);
            AppendRaw(builder, "sourceSlot", IntRaw(sourceSlot), true);
            AppendRaw(builder, "itemType", IntRaw(itemType), true);
            AppendString(builder, "itemName", itemName, true);
            AppendRaw(builder, "healLife", IntRaw(healLife), true);
            AppendRaw(builder, "healMana", IntRaw(healMana), true);
            AppendRaw(builder, "stackAfter", IntRaw(stackAfter), true);
            AppendRaw(builder, "potionDelayApplied", BoolRaw(potionDelayApplied), true);
            AppendRaw(builder, "itemBuffApplied", BoolRaw(itemBuffApplied), true);
            AppendRaw(builder, "lifeManaApplied", BoolRaw(lifeManaApplied), true);
            AppendRaw(builder, "itemConsumed", BoolRaw(itemConsumed), true);
            AppendRaw(builder, "useSoundInvoked", BoolRaw(useSoundInvoked), true);
            AppendRaw(builder, "useSoundPlayed", BoolRaw(useSoundPlayed), true);
            AppendString(builder, "useSoundMessage", useSoundMessage, true);
            AppendRaw(builder, "tryStartRejectedManaFallback", BoolRaw(tryStartRejectedManaFallback), true);
            AppendString(builder, "manaFallbackMessage", manaFallbackMessage, true);
            AppendRaw(builder, "lifeAfter", IntRaw(after == null ? 0 : after.Life), true);
            AppendRaw(builder, "manaAfter", IntRaw(after == null ? 0 : after.Mana), true);
            AppendString(builder, "triggerReason", GetMetadataString(execution, "TriggerReason", string.Empty), true);
            AppendRaw(builder, "currentMana", IntRaw(GetMetadataInt(execution, "CurrentMana", after == null ? 0 : after.Mana)), true);
            AppendRaw(builder, "requiredMana", IntRaw(GetMetadataInt(execution, "RequiredMana", 0)), true);
            AppendRaw(builder, "selectedItemType", IntRaw(GetMetadataInt(execution, "SelectedItemType", 0)), true);
            AppendString(builder, "selectedItemName", GetMetadataString(execution, "SelectedItemName", string.Empty), true);
            AppendRaw(builder, "selectedItemManaCost", IntRaw(GetMetadataInt(execution, "SelectedItemManaCost", 0)), true);
            AppendRaw(builder, "checkManaAvailable", BoolRaw(GetMetadataBool(execution, "CheckManaAvailable", false)), true);
            AppendRaw(builder, "checkManaResult", BoolRaw(GetMetadataBool(execution, "CheckManaResult", false)), true);
            AppendRaw(builder, "usedFallbackManaCostCheck", BoolRaw(GetMetadataBool(execution, "UsedFallbackManaCostCheck", false)), true);
            AppendString(builder, "manaCheckReason", GetMetadataString(execution, "ManaCheckReason", string.Empty), true);
            AppendRaw(builder, "buffCountAfter", IntRaw(after == null ? 0 : after.ActiveBuffCount), true);
            AppendRaw(builder, "networkModeValue", IntRaw(netMode), true);
            AppendString(builder, "networkMode", networkMode, true);
            AppendRaw(builder, "multiplayerClient", BoolRaw(multiplayerClient), true);
            AppendRaw(builder, "syncAttempted", BoolRaw(syncAttempted), true);
            AppendString(builder, "syncMethod", syncMethod, true);
            AppendRaw(builder, "syncSucceeded", BoolRaw(syncSucceeded), true);
            AppendString(builder, "syncResult", syncResult, true);
            AppendString(builder, "resultCode", resultCode, true);
            AppendString(builder, "message", message, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildVerificationJson(InputActionExecution execution, bool tryStartInvoked, bool potionDelayApplied, bool itemBuffApplied, bool lifeManaApplied, bool itemConsumed, bool syncAttempted, string syncMethod, bool syncSucceeded, string syncResult, ItemUseVerificationState before, ItemUseVerificationState after, bool useSoundInvoked, bool useSoundPlayed, string useSoundMessage, bool tryStartRejectedManaFallback, string manaFallbackMessage)
        {
            var lifeChanged = before != null && after != null && after.Life > before.Life;
            var manaChanged = before != null && after != null && after.Mana > before.Mana;
            return "{" +
                   "\"tryStartUseInvoked\":" + BoolRaw(tryStartInvoked) + "," +
                   "\"potionDelayApplied\":" + BoolRaw(potionDelayApplied) + "," +
                   "\"itemBuffApplied\":" + BoolRaw(itemBuffApplied) + "," +
                   "\"lifeManaApplied\":" + BoolRaw(lifeManaApplied) + "," +
                   "\"itemConsumed\":" + BoolRaw(itemConsumed) + "," +
                   "\"useSoundInvoked\":" + BoolRaw(useSoundInvoked) + "," +
                   "\"useSoundPlayed\":" + BoolRaw(useSoundPlayed) + "," +
                   "\"useSoundMessage\":\"" + EscapeJson(useSoundMessage) + "\"," +
                   "\"tryStartRejectedManaFallback\":" + BoolRaw(tryStartRejectedManaFallback) + "," +
                   "\"manaFallbackMessage\":\"" + EscapeJson(manaFallbackMessage) + "\"," +
                   "\"requiredMana\":" + IntRaw(GetMetadataInt(execution, "RequiredMana", 0)) + "," +
                   "\"selectedItemType\":" + IntRaw(GetMetadataInt(execution, "SelectedItemType", 0)) + "," +
                   "\"selectedItemName\":\"" + EscapeJson(GetMetadataString(execution, "SelectedItemName", string.Empty)) + "\"," +
                   "\"selectedItemManaCost\":" + IntRaw(GetMetadataInt(execution, "SelectedItemManaCost", 0)) + "," +
                   "\"checkManaAvailable\":" + BoolRaw(GetMetadataBool(execution, "CheckManaAvailable", false)) + "," +
                   "\"checkManaResult\":" + BoolRaw(GetMetadataBool(execution, "CheckManaResult", false)) + "," +
                   "\"usedFallbackManaCostCheck\":" + BoolRaw(GetMetadataBool(execution, "UsedFallbackManaCostCheck", false)) + "," +
                   "\"manaCheckReason\":\"" + EscapeJson(GetMetadataString(execution, "ManaCheckReason", string.Empty)) + "\"," +
                   "\"syncAttempted\":" + BoolRaw(syncAttempted) + "," +
                   "\"syncMethod\":\"" + EscapeJson(syncMethod) + "\"," +
                   "\"syncSucceeded\":" + BoolRaw(syncSucceeded) + "," +
                   "\"syncResult\":\"" + EscapeJson(syncResult) + "\"," +
                   "\"observableChange\":" + BoolRaw(lifeChanged || manaChanged || itemConsumed) + "," +
                   "\"changedFields\":" + BuildChangedFieldsJson(lifeChanged, manaChanged, itemConsumed) +
                   "}";
        }

        private static string BuildChangedFieldsJson(bool lifeChanged, bool manaChanged, bool itemConsumed)
        {
            var builder = new StringBuilder();
            builder.Append("[");
            var first = true;
            AppendChanged(builder, ref first, "life", lifeChanged);
            AppendChanged(builder, ref first, "mana", manaChanged);
            AppendChanged(builder, ref first, "itemStack", itemConsumed);
            builder.Append("]");
            return builder.ToString();
        }

        private static void AppendChanged(StringBuilder builder, ref bool first, string name, bool changed)
        {
            if (!changed)
            {
                return;
            }

            if (!first)
            {
                builder.Append(",");
            }

            builder.Append("\"").Append(name).Append("\"");
            first = false;
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static bool GetMetadataBool(InputActionExecution execution, string key, bool fallback)
        {
            var value = GetMetadataString(execution, key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
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
