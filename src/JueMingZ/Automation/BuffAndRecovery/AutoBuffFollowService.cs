using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.BuffAndRecovery
{
    // Follow-up reads infer vanilla item-use effects only; unknown buff fields fail closed instead of fabricating triggers.
    public static class AutoBuffFollowService
    {
        public static bool TryCaptureManualItemUse(object player, out AutoBuffFollowItemUseObservation observation)
        {
            observation = null;
            var settings = ConfigService.AppSettings;
            if (settings == null || !settings.AutoBuffFollowAddEnabled || player == null)
            {
                return false;
            }

            if (!TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            if (ItemUseBridge.PendingRequestId != Guid.Empty)
            {
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                return false;
            }

            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, selectedSlot, out before) ||
                !IsBuffPotionUseCandidate(player, selectedSlot, before))
            {
                return false;
            }

            int activeBuffTimeBefore;
            PlayerBuffCompat.TryReadBuffTime(player, before.BuffType, out activeBuffTimeBefore);
            observation = new AutoBuffFollowItemUseObservation
            {
                SelectedSlot = selectedSlot,
                ItemType = before.ItemType,
                ItemName = before.ItemName ?? string.Empty,
                StackBefore = before.ItemStack,
                BuffType = before.BuffType,
                BuffName = BuffPotionCatalog.ReadBuffNameSafe(before.BuffType),
                BuffTime = before.BuffTime,
                ActiveBuffTimeBefore = activeBuffTimeBefore
            };
            return true;
        }

        public static void CompleteManualItemUse(object player, AutoBuffFollowItemUseObservation observation)
        {
            try
            {
                if (player == null || observation == null || observation.ItemType <= 0 || observation.BuffType <= 0)
                {
                    return;
                }

                var settings = ConfigService.AppSettings;
                if (settings == null || !settings.AutoBuffFollowAddEnabled)
                {
                    return;
                }

                ItemUseVerificationState after;
                if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, observation.SelectedSlot, out after))
                {
                    return;
                }

                int activeBuffTimeAfter;
                PlayerBuffCompat.TryReadBuffTime(player, observation.BuffType, out activeBuffTimeAfter);
                var stackDecreased = DidStackDecrease(observation, after);
                var buffActiveAfter = activeBuffTimeAfter > 0;
                var buffGained = observation.ActiveBuffTimeBefore <= 0 && activeBuffTimeAfter > 0;
                var buffTimeExtended = observation.ActiveBuffTimeBefore > 0 && activeBuffTimeAfter > observation.ActiveBuffTimeBefore + 30;
                var manualUseDetected = buffActiveAfter && (stackDecreased || buffGained || buffTimeExtended);
                if (!manualUseDetected)
                {
                    return;
                }

                var alreadyWhitelisted = BuffPotionWhitelistService.ContainsItemType(observation.ItemType);
                var added = false;
                var message = string.Empty;
                if (!alreadyWhitelisted)
                {
                    var candidate = new BuffPotionCandidate
                    {
                        SourceContainer = "Inventory",
                        SourceSlot = observation.SelectedSlot,
                        ItemType = observation.ItemType,
                        ItemName = observation.ItemName,
                        Stack = Math.Max(0, after.ItemType == observation.ItemType ? after.ItemStack : 0),
                        BuffType = observation.BuffType,
                        BuffName = observation.BuffName,
                        BuffTime = observation.BuffTime,
                        EstimatedDurationSeconds = Math.Max(0, observation.BuffTime / 60),
                        IsActive = true,
                        CanApply = false,
                        SkipReason = "AlreadyActive"
                    };
                    added = BuffPotionWhitelistService.Add(candidate, out message);
                }
                else
                {
                    message = "Manual buff potion is already in AutoBuff whitelist.";
                }

                RecordFollowAdd(observation, after, activeBuffTimeAfter, stackDecreased, buffGained, buffTimeExtended, alreadyWhitelisted, added, message);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("AutoBuffFollowService.CompleteManualItemUse", error);
                LogThrottle.ErrorThrottled(
                    "autobuff-follow-add-failed",
                    TimeSpan.FromSeconds(10),
                    "AutoBuffFollowService",
                    "AutoBuff follow-add detection failed.", error);
            }
        }

        public static void NotifyBuffRemovedByPlayerUi(object player, int buffIndex, string hookMethod)
        {
            try
            {
                var settings = ConfigService.AppSettings;
                if (settings == null || !settings.AutoBuffFollowRemoveEnabled || player == null)
                {
                    return;
                }

                if (!TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    return;
                }

                int buffType;
                int buffTime;
                if (!TryReadBuffAtIndex(player, buffIndex, out buffType, out buffTime) ||
                    buffType <= 0 ||
                    buffTime <= 0 ||
                    !BuffPotionWhitelistService.ContainsBuffType(buffType))
                {
                    return;
                }

                bool leftClick;
                bool rightClick;
                string mouseMessage;
                if (!IsLikelyManualBuffCancel(out leftClick, out rightClick, out mouseMessage))
                {
                    return;
                }

                BuffPotionWhitelistEntry removedEntry;
                string removeMessage;
                var removedCount = BuffPotionWhitelistService.RemoveByBuffType(buffType, out removedEntry, out removeMessage);
                if (removedCount <= 0)
                {
                    return;
                }

                RecordFollowRemove(
                    buffIndex,
                    buffType,
                    buffTime,
                    removedEntry,
                    removedCount,
                    hookMethod,
                    leftClick,
                    rightClick,
                    mouseMessage,
                    removeMessage);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("AutoBuffFollowService.NotifyBuffRemovedByPlayerUi", error);
                LogThrottle.ErrorThrottled(
                    "autobuff-follow-remove-failed",
                    TimeSpan.FromSeconds(10),
                    "AutoBuffFollowService",
                    "AutoBuff follow-remove detection failed.", error);
            }
        }

        private static bool IsBuffPotionUseCandidate(object player, int selectedSlot, ItemUseVerificationState state)
        {
            if (state == null ||
                !state.PlayerActive ||
                state.PlayerDead ||
                state.PlayerGhost ||
                state.ItemType <= 0 ||
                state.ItemStack <= 0 ||
                state.BuffType <= 0 ||
                state.BuffTime <= 0)
            {
                return false;
            }

            object item;
            string message;
            if (InventoryMutationCompat.TryGetItem(player, "Inventory", selectedSlot, out item, out message))
            {
                int itemType;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                string itemName;
                if (InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) && summon)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DidStackDecrease(AutoBuffFollowItemUseObservation before, ItemUseVerificationState after)
        {
            if (before == null || after == null)
            {
                return false;
            }

            if (after.ItemType == before.ItemType)
            {
                return after.ItemStack < before.StackBefore;
            }

            return before.StackBefore > 0 && after.ItemType != before.ItemType;
        }

        private static bool TryReadBuffAtIndex(object player, int buffIndex, out int buffType, out int buffTime)
        {
            buffType = 0;
            buffTime = 0;
            if (player == null || buffIndex < 0)
            {
                return false;
            }

            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null || buffIndex >= buffTypes.Count || buffIndex >= buffTimes.Count)
            {
                return false;
            }

            try
            {
                buffType = Convert.ToInt32(buffTypes[buffIndex]);
                buffTime = Convert.ToInt32(buffTimes[buffIndex]);
                return true;
            }
            catch
            {
                buffType = 0;
                buffTime = 0;
                return false;
            }
        }

        private static bool IsLikelyManualBuffCancel(out bool leftClick, out bool rightClick, out string message)
        {
            bool mouseLeft;
            bool mouseLeftRelease;
            bool mouseRight;
            bool mouseRightRelease;
            TryReadMainBool("mouseLeft", out mouseLeft);
            TryReadMainBool("mouseLeftRelease", out mouseLeftRelease);
            TryReadMainBool("mouseRight", out mouseRight);
            TryReadMainBool("mouseRightRelease", out mouseRightRelease);

            leftClick = mouseLeft && mouseLeftRelease;
            rightClick = mouseRight && mouseRightRelease;
            message = "mouseLeft=" + BoolText(mouseLeft) +
                      ", mouseLeftRelease=" + BoolText(mouseLeftRelease) +
                      ", mouseRight=" + BoolText(mouseRight) +
                      ", mouseRightRelease=" + BoolText(mouseRightRelease);
            return leftClick || rightClick;
        }

        private static bool TryReadMainBool(string name, out bool value)
        {
            value = false;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return false;
            }

            object raw = null;
            if (TerrariaMemberCache.TryGetField(mainType, name, true, out var field))
            {
                raw = field.GetValue(null);
            }
            else if (TerrariaMemberCache.TryGetProperty(mainType, name, true, out var property))
            {
                raw = property.GetValue(null, null);
            }

            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                value = false;
                return false;
            }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static void RecordFollowAdd(
            AutoBuffFollowItemUseObservation before,
            ItemUseVerificationState after,
            int activeBuffTimeAfter,
            bool stackDecreased,
            bool buffGained,
            bool buffTimeExtended,
            bool alreadyWhitelisted,
            bool added,
            string message)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "BuffPotion.FollowAdd",
                "BuffPotion",
                string.Empty,
                added ? "Succeeded" : "NotApplicable",
                added ? "Succeeded" : "NotApplicable",
                string.IsNullOrWhiteSpace(message) ? "AutoBuff follow-add observed a manual buff potion use." : message,
                0,
                "{" +
                    "\"followAddEnabled\":true," +
                    "\"selectedSlot\":" + IntRaw(before.SelectedSlot) + "," +
                    "\"itemType\":" + IntRaw(before.ItemType) + "," +
                    "\"itemName\":\"" + EscapeJson(before.ItemName) + "\"," +
                    "\"stackBefore\":" + IntRaw(before.StackBefore) + "," +
                    "\"buffType\":" + IntRaw(before.BuffType) + "," +
                    "\"buffName\":\"" + EscapeJson(before.BuffName) + "\"," +
                    "\"buffTimeBefore\":" + IntRaw(before.ActiveBuffTimeBefore) +
                "}",
                "{" +
                    "\"stackAfter\":" + IntRaw(after == null ? 0 : after.ItemStack) + "," +
                    "\"buffTimeAfter\":" + IntRaw(activeBuffTimeAfter) + "," +
                    "\"alreadyWhitelisted\":" + BoolRaw(alreadyWhitelisted) + "," +
                    "\"addedToWhitelist\":" + BoolRaw(added) + "," +
                    "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) +
                "}",
                "{" +
                    "\"manualItemUseDetected\":true," +
                    "\"stackDecreased\":" + BoolRaw(stackDecreased) + "," +
                    "\"buffGained\":" + BoolRaw(buffGained) + "," +
                    "\"buffTimeExtended\":" + BoolRaw(buffTimeExtended) + "," +
                    "\"submitted\":false" +
                "}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static void RecordFollowRemove(
            int buffIndex,
            int buffType,
            int buffTime,
            BuffPotionWhitelistEntry removedEntry,
            int removedCount,
            string hookMethod,
            bool leftClick,
            bool rightClick,
            string mouseMessage,
            string message)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                "BuffPotion.FollowRemove",
                "BuffPotion",
                string.Empty,
                "Succeeded",
                "Succeeded",
                string.IsNullOrWhiteSpace(message) ? "AutoBuff follow-remove removed a cancelled buff from whitelist." : message,
                0,
                "{" +
                    "\"followRemoveEnabled\":true," +
                    "\"buffIndex\":" + IntRaw(buffIndex) + "," +
                    "\"buffType\":" + IntRaw(buffType) + "," +
                    "\"buffName\":\"" + EscapeJson(removedEntry == null ? BuffPotionCatalog.ReadBuffNameSafe(buffType) : removedEntry.BuffName) + "\"," +
                    "\"buffTimeBefore\":" + IntRaw(buffTime) + "," +
                    "\"hookMethod\":\"" + EscapeJson(hookMethod) + "\"" +
                "}",
                "{" +
                    "\"removedCount\":" + IntRaw(removedCount) + "," +
                    "\"itemType\":" + IntRaw(removedEntry == null ? 0 : removedEntry.ItemType) + "," +
                    "\"itemName\":\"" + EscapeJson(removedEntry == null ? string.Empty : removedEntry.ItemName) + "\"," +
                    "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) +
                "}",
                "{" +
                    "\"manualBuffCancelDetected\":true," +
                    "\"leftClick\":" + BoolRaw(leftClick) + "," +
                    "\"rightClick\":" + BoolRaw(rightClick) + "," +
                    "\"mouseState\":\"" + EscapeJson(mouseMessage) + "\"," +
                    "\"submitted\":false" +
                "}",
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
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
