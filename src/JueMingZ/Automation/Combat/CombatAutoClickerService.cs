using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAutoClickerService
    {
        private const string FeatureId = FeatureIds.CombatAutoClicker;
        private const int MouseItemSelectionSlot = 58;
        private const int MinAttemptCooldownTicks = 4;
        private const int MaxAttemptCooldownTicks = 60;
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static string _lastAttemptKey = string.Empty;
        private static long _nextAllowedTick;
        private static CombatAutomationDecisionDiagnosticInfo _diagnostics = new CombatAutomationDecisionDiagnosticInfo();

        public static CombatAutomationDecisionDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static void UpdatePrefixGuard()
        {
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatAutoClickerEnabled)
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (IsBlockingUiForAutoClicker(ui))
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                bool physicalHeld;
                if (!TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out physicalHeld) || !physicalHeld)
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                AutoClickerItemProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                profile.UseItemHeld = true;
                if (settings.CombatPerfectRevolverEnabled &&
                    !profile.MouseItemPresent &&
                    CombatPerfectRevolverService.IsRevolverItemType(profile.ItemType))
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                if (settings.CombatMagicStringClickerEnabled &&
                    !profile.MouseItemPresent &&
                    CombatMagicStringClickerService.IsYoyoItemType(profile.ItemType))
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                if (!IsEligible(profile, out reason))
                {
                    TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                    return;
                }

                TerrariaInputCompat.TrySuppressHeldUseItemForAutoClicker(player);
            }
            catch (Exception error)
            {
                TerrariaInputCompat.ClearAutoClickerSuppressedUseItem();
                RuntimeDiagnostics.RecordError("CombatAutoClickerService.UpdatePrefixGuard", error);
                LogThrottle.ErrorThrottled(
                    "combat-auto-clicker-prefix-guard-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAutoClickerService",
                    "Combat auto clicker prefix guard failed; exception swallowed.", error);
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatAutoClickerEnabled)
                {
                    ResetThrottle();
                    RecordDecision("disabled", "disabled", tick, false);
                    return;
                }

                if (queue == null)
                {
                    RecordDecision("skipped", "queueUnavailable", tick, false);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld)
                {
                    RecordDecision("skipped", "notInWorld", tick, false);
                    return;
                }

                if (CombatAutomationDecisionDiagnostics.IsBlockingSnapshotUiForCombat(snapshot.Ui))
                {
                    RecordDecision("skipped", CombatAutomationDecisionDiagnostics.BuildSnapshotUiSkipReason(snapshot.Ui), tick, false);
                    return;
                }

                var fast = queue.GetFastState();
                if (fast.PendingCount > 0 ||
                    fast.HasRunningAction ||
                    ItemUseBridge.PendingRequestId != Guid.Empty)
                {
                    RecordDecision("skipped", "queueBusy", tick, false);
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    RecordDecision("skipped", "localPlayerUnavailable", tick, false);
                    return;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (IsBlockingUiForAutoClicker(ui))
                {
                    RecordDecision("skipped", CombatAutomationDecisionDiagnostics.BuildUiSkipReason(ui), tick, false);
                    return;
                }

                AutoClickerItemProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    RecordDecision("skipped", "profile:" + reason, tick, false);
                    return;
                }
                profile.PlayerInventoryOpen = ui.PlayerInventoryOpen;

                if (settings.CombatPerfectRevolverEnabled &&
                    !profile.MouseItemPresent &&
                    CombatPerfectRevolverService.IsRevolverItemType(profile.ItemType))
                {
                    RecordDecision("skipped", "delegated:perfectRevolver", tick, false);
                    return;
                }

                if (settings.CombatMagicStringClickerEnabled &&
                    !profile.MouseItemPresent &&
                    CombatMagicStringClickerService.IsYoyoItemType(profile.ItemType))
                {
                    RecordDecision("skipped", "delegated:magicStringClicker", tick, false);
                    return;
                }

                if (!IsEligible(profile, out reason))
                {
                    RecordDecision("skipped", "ineligible:" + reason, tick, false);
                    return;
                }

                if (!IsReadyToClick(profile))
                {
                    RecordDecision("skipped", "notReady", tick, false);
                    return;
                }

                tick = profile.GameUpdateCount > 0
                    ? profile.GameUpdateCount
                    : runtimeState == null ? 0 : runtimeState.UpdateCount;
                var key = profile.SelectedSlot.ToString(CultureInfo.InvariantCulture) + ":" +
                          profile.ItemType.ToString(CultureInfo.InvariantCulture);
                lock (SyncRoot)
                {
                    if (string.Equals(_lastAttemptKey, key, StringComparison.Ordinal) && tick < _nextAllowedTick)
                    {
                        RecordDecision("skipped", "cooldown", tick, false);
                        return;
                    }
                }

                var request = CreateItemUseRequest(profile);

                queue.Enqueue(request);

                lock (SyncRoot)
                {
                    _lastAttemptKey = key;
                    _nextAllowedTick = tick + GetAttemptCooldownTicks(profile);
                }

                RecordDecision("submitted", string.Empty, tick, true);
            }
            catch (Exception error)
            {
                RecordDecision("exception", "exception:" + error.GetType().Name, tick, false);
                RuntimeDiagnostics.RecordError("CombatAutoClickerService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-auto-clicker-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAutoClickerService",
                    "Combat auto clicker tick failed; exception swallowed.", error);
            }
        }

        internal static InputActionRequest CreateItemUseRequest(AutoClickerItemProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureId,
                Description = "Combat auto clicker uses selected item",
                Timeout = TimeSpan.FromMilliseconds(750),
                IsExclusive = true
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatAutoClicker;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["RequireUseItemHeld"] = "true";
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["AllowCombatAim"] = "true";
            request.Metadata["AutoClickerItemType"] = profile.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerItemName"] = profile.ItemName ?? string.Empty;
            request.Metadata["AutoClickerSelectedSlot"] = profile.SelectedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerItemAutoReuse"] = profile.AutoReuse ? "true" : "false";
            request.Metadata["AutoClickerItemUseStyle"] = profile.UseStyle.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerItemUseAnimation"] = profile.UseAnimation.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerItemUseTime"] = profile.UseTime.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerDelayUseItem"] = profile.DelayUseItem ? "true" : "false";
            request.Metadata["AutoClickerPlayerInventoryOpen"] = profile.PlayerInventoryOpen ? "true" : "false";
            request.Metadata["AutoClickerMouseItemPresent"] = profile.MouseItemPresent ? "true" : "false";
            request.Metadata["AutoClickerMouseItemType"] = profile.MouseItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoClickerMouseItemName"] = profile.MouseItemName ?? string.Empty;
            return request;
        }

        private static bool TryReadProfile(object player, out AutoClickerItemProfile profile, out string reason)
        {
            profile = new AutoClickerItemProfile();
            reason = string.Empty;

            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            bool active;
            bool dead;
            bool ghost;
            GameStateReflection.TryGetBool(player, "active", out active);
            GameStateReflection.TryGetBool(player, "dead", out dead);
            GameStateReflection.TryGetBool(player, "ghost", out ghost);
            if (!active || dead || ghost)
            {
                reason = !active ? "playerInactive" : dead ? "playerDead" : "playerGhost";
                return false;
            }

            CombatAimUseInputSnapshot input;
            if (!TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input) || input == null || !input.Available)
            {
                reason = string.IsNullOrWhiteSpace(TerrariaInputCompat.LastInputCompatError)
                    ? "inputSnapshotUnavailable"
                    : TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.UseItemHeld = input.UseItemHeld;
            profile.UseItemReleased = input.UseItemReleased;
            profile.ItemAnimation = input.ItemAnimation;
            profile.ItemTime = input.ItemTime;
            profile.GameUpdateCount = input.GameUpdateCount;

            var mouseItem = ReadMouseItemProfile(profile);
            if (profile.MouseItemPresent && mouseItem != null)
            {
                profile.SelectedSlot = MouseItemSelectionSlot;
                ReadItemUseProfile(mouseItem, profile);
            }
            else
            {
                int selectedSlot;
                if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
                {
                    reason = TerrariaInputCompat.LastInputCompatError;
                    return false;
                }

                profile.SelectedSlot = selectedSlot;
                if (selectedSlot < 0 || selectedSlot > 9)
                {
                    reason = "selectedSlotNotHotbar";
                    return false;
                }

                var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
                if (inventory == null || selectedSlot >= inventory.Count)
                {
                    reason = "inventoryUnavailable";
                    return false;
                }

                var item = inventory[selectedSlot];
                if (item == null)
                {
                    reason = "selectedItemUnavailable";
                    return false;
                }

                ReadItemUseProfile(item, profile);
            }

            profile.ReuseDelay = ReadInt(player, "reuseDelay", 0);
            profile.DelayUseItem = ReadBool(player, "delayUseItem", false);
            profile.AltFunctionUse = ReadInt(player, "altFunctionUse", 0);
            bool itemTimeIsZero;
            profile.ItemTimeIsZero = GameStateReflection.TryGetBool(player, "ItemTimeIsZero", out itemTimeIsZero)
                ? itemTimeIsZero
                : profile.ItemTime <= 0;
            return true;
        }

        private static bool IsBlockingUiForAutoClicker(TerrariaUiInputContext ui)
        {
            if (ui == null)
            {
                return true;
            }

            return ui.MainTypeUnavailable ||
                   ui.GameMenu ||
                   ui.ChatOpen ||
                   ui.NpcChatOpen ||
                   ui.MouseCapturedByUi;
        }

        private static bool IsEligible(AutoClickerItemProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null || profile.ItemType <= 0 || profile.ItemStack <= 0)
            {
                reason = profile != null && profile.MouseItemPresent
                    ? "mouseItemHeldButSelectedItemEmpty"
                    : "selectedItemEmpty";
                return false;
            }

            if (!profile.UseItemHeld)
            {
                reason = "notHoldingUseItem";
                return false;
            }

            if (profile.Channel)
            {
                reason = "channelItem";
                return false;
            }

            if (profile.AltFunctionUse != 0)
            {
                reason = "altFunctionUse";
                return false;
            }

            if (profile.UseStyle <= 0 || profile.UseAnimation <= 0 || profile.UseTime <= 0)
            {
                reason = "notUsableItem";
                return false;
            }

            if (profile.FishingPole > 0)
            {
                reason = "fishingPole";
                return false;
            }

            return true;
        }

        private static bool IsReadyToClick(AutoClickerItemProfile profile)
        {
            return profile != null &&
                   profile.ItemAnimation <= 0 &&
                   profile.ItemTimeIsZero &&
                   profile.ReuseDelay <= 0;
        }

        private static int GetAttemptCooldownTicks(AutoClickerItemProfile profile)
        {
            if (profile == null)
            {
                return MinAttemptCooldownTicks;
            }

            var ticks = Math.Max(profile.UseAnimation, profile.UseTime);
            if (profile.ItemReuseDelay > 0)
            {
                ticks += profile.ItemReuseDelay;
            }

            if (ticks < MinAttemptCooldownTicks)
            {
                return MinAttemptCooldownTicks;
            }

            return ticks > MaxAttemptCooldownTicks ? MaxAttemptCooldownTicks : ticks;
        }

        private static void ResetThrottle()
        {
            lock (SyncRoot)
            {
                _lastAttemptKey = string.Empty;
                _nextAllowedTick = 0;
            }
        }

        private static void RecordDecision(string decision, string reason, long tick, bool submitted)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new CombatAutomationDecisionDiagnosticInfo() : _diagnostics.Clone();
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                if (submitted)
                {
                    current.SubmittedCount++;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(source, name, out value) ? value : fallback;
        }

        private static void ReadItemUseProfile(object item, AutoClickerItemProfile profile)
        {
            if (item == null || profile == null)
            {
                return;
            }

            profile.ItemType = ReadInt(item, "type", 0);
            profile.ItemStack = ReadInt(item, "stack", 0);
            profile.ItemName = ReadItemName(item);
            profile.UseStyle = ReadInt(item, "useStyle", 0);
            profile.UseAnimation = ReadInt(item, "useAnimation", 0);
            profile.UseTime = ReadInt(item, "useTime", 0);
            profile.ItemReuseDelay = ReadInt(item, "reuseDelay", 0);
            profile.FishingPole = ReadInt(item, "fishingPole", 0);
            profile.AutoReuse = ReadBool(item, "autoReuse", false);
            profile.Channel = ReadBool(item, "channel", false);
        }

        private static object ReadMouseItemProfile(AutoClickerItemProfile profile)
        {
            if (profile == null)
            {
                return null;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return null;
            }

            var mouseItem = GameStateReflection.GetStaticMember(mainType, "mouseItem");
            if (mouseItem == null)
            {
                return null;
            }

            var type = ReadInt(mouseItem, "type", 0);
            var stack = ReadInt(mouseItem, "stack", 0);
            if (type <= 0 || stack <= 0)
            {
                return null;
            }

            profile.MouseItemPresent = true;
            profile.MouseItemType = type;
            profile.MouseItemStack = stack;
            profile.MouseItemName = ReadItemName(mouseItem);
            return mouseItem;
        }

        private static string ReadItemName(object item)
        {
            var name = GameStateReflection.GetMember(item, "Name") ??
                       GameStateReflection.GetMember(item, "name");
            return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
        }

        internal sealed class AutoClickerItemProfile
        {
            public bool UseItemHeld;
            public bool UseItemReleased;
            public int ItemAnimation;
            public int ItemTime;
            public bool ItemTimeIsZero;
            public int ReuseDelay;
            public bool DelayUseItem;
            public int AltFunctionUse;
            public long GameUpdateCount;
            public int SelectedSlot = -1;
            public int ItemType;
            public int ItemStack;
            public string ItemName = string.Empty;
            public int UseStyle;
            public int UseAnimation;
            public int UseTime;
            public int ItemReuseDelay;
            public int FishingPole;
            public bool AutoReuse;
            public bool Channel;
            public bool PlayerInventoryOpen;
            public bool MouseItemPresent;
            public int MouseItemType;
            public int MouseItemStack;
            public string MouseItemName = string.Empty;
        }
    }
}
