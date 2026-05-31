using System;
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
    public static class CombatPerfectRevolverService
    {
        private const string FeatureId = FeatureIds.CombatPerfectRevolver;
        private const int RevolverItemId = 2269;
        private const int DefaultRevolverCooldownTicks = 22;
        private const int PerfectFireWindowTicks = 2;
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static bool _directTakeoverActive;
        private static bool _lastAttackPressed;
        private static long _scheduledPressTick;
        private static CombatAutomationDecisionDiagnosticInfo _diagnostics = new CombatAutomationDecisionDiagnosticInfo();

        public static CombatAutomationDecisionDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static bool IsRevolverItemType(int itemType)
        {
            return itemType == RevolverItemId;
        }

        public static void UpdatePrefixGuard()
        {
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatPerfectRevolverEnabled)
                {
                    ResetItemCheckTakeoverState("disabled", 0);
                    return;
                }
            }
            catch (Exception error)
            {
                ResetItemCheckTakeoverState("exception:" + error.GetType().Name, 0);
                RuntimeDiagnostics.RecordError("CombatPerfectRevolverService.UpdatePrefixGuard", error);
                LogThrottle.ErrorThrottled(
                    "combat-perfect-revolver-prefix-guard-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatPerfectRevolverService",
                    "Combat perfect revolver prefix guard failed; exception swallowed.", error);
            }
        }

        public static bool TryBeginItemCheckTakeover(object player, out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            takeover = null;
            long tick = 0;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatPerfectRevolverEnabled)
                {
                    ResetItemCheckTakeoverState("disabled", tick);
                    return false;
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    ResetItemCheckTakeoverState("localPlayerUnavailable", tick);
                    return false;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable ||
                    ui.GameMenu ||
                    ui.ChatOpen ||
                    ui.NpcChatOpen ||
                    ui.MouseCapturedByUi)
                {
                    ResetItemCheckTakeoverState(CombatAutomationDecisionDiagnostics.BuildUiSkipReason(ui), tick);
                    return false;
                }

                bool physicalHeld;
                if (!TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out physicalHeld) || !physicalHeld)
                {
                    ResetItemCheckTakeoverState("notHoldingPhysicalUseItem", tick);
                    return false;
                }

                PerfectRevolverItemProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    ResetItemCheckTakeoverState("profile:" + reason, tick);
                    return false;
                }

                tick = profile.GameUpdateCount;
                if (!IsEligible(profile, out reason))
                {
                    ResetItemCheckTakeoverState("ineligible:" + reason, tick);
                    return false;
                }

                var plan = CreateAttackPlan(profile, tick, GetScheduledPressTick());
                if (!TerrariaInputCompat.TryBeginScopedUseItemClickTakeover(player, plan.PressAttack, "PerfectRevolverItemCheck", out takeover))
                {
                    RecordDecision("failed", "itemCheckTakeoverFailed:" + TerrariaInputCompat.LastInputCompatError, tick, false);
                    return false;
                }

                SetScheduledPressTick(plan.NextScheduledPressTick);
                SetDirectTakeoverActive(true, plan.PressAttack);
                RecordDecision(plan.PressAttack ? "pressed" : "released", plan.Reason, tick, plan.PressAttack);
                if (plan.PressAttack || plan.ScheduleNextTick)
                {
                    RecordDirectTakeoverEvent(profile, plan, tick);
                }

                return true;
            }
            catch (Exception error)
            {
                ResetItemCheckTakeoverState("exception:" + error.GetType().Name, tick);
                RuntimeDiagnostics.RecordError("CombatPerfectRevolverService.TryBeginItemCheckTakeover", error);
                LogThrottle.ErrorThrottled(
                    "combat-perfect-revolver-itemcheck-takeover-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatPerfectRevolverService",
                    "Combat perfect revolver ItemCheck takeover failed; exception swallowed.", error);
                return false;
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            // Perfect revolver is handled in the Player.ItemCheck prefix. Main.Update prefix is too
            // early because vanilla input polling can rewrite held mouse state before ItemCheck.
        }

        private static bool TryReadProfile(object player, out PerfectRevolverItemProfile profile, out string reason)
        {
            profile = new PerfectRevolverItemProfile();
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

            profile.ItemType = ReadInt(item, "type", 0);
            profile.ItemStack = ReadInt(item, "stack", 0);
            profile.ItemName = ReadItemName(item);
            profile.UseStyle = ReadInt(item, "useStyle", 0);
            profile.UseAnimation = ReadInt(item, "useAnimation", 0);
            profile.UseTime = ReadInt(item, "useTime", 0);
            profile.ItemReuseDelay = ReadInt(item, "reuseDelay", 0);
            profile.Channel = ReadBool(item, "channel", false);

            profile.ReuseDelay = ReadInt(player, "reuseDelay", 0);
            profile.DelayUseItem = ReadBool(player, "delayUseItem", false);
            profile.AltFunctionUse = ReadInt(player, "altFunctionUse", 0);
            profile.RevolverCritChanceBonus = ReadInt(player, "revolverCritChanceBonus", 0);
            bool itemTimeIsZero;
            profile.ItemTimeIsZero = GameStateReflection.TryGetBool(player, "ItemTimeIsZero", out itemTimeIsZero)
                ? itemTimeIsZero
                : profile.ItemTime <= 0;
            return true;
        }

        private static bool IsEligible(PerfectRevolverItemProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null || profile.ItemType <= 0 || profile.ItemStack <= 0)
            {
                reason = "selectedItemEmpty";
                return false;
            }

            if (!IsRevolverItemType(profile.ItemType))
            {
                reason = "notRevolver";
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

            return true;
        }

        internal static bool IsReadyToFire(PerfectRevolverItemProfile profile)
        {
            return profile != null &&
                   profile.ItemAnimation <= 0 &&
                   (profile.ItemTime <= 0 || profile.ItemTimeIsZero) &&
                   profile.ReuseDelay <= 0 &&
                   !profile.DelayUseItem;
        }

        internal static bool IsPerfectFireWindow(PerfectRevolverItemProfile profile)
        {
            if (profile == null || profile.DelayUseItem || profile.ReuseDelay > 0)
            {
                return false;
            }

            return IsWithinWindow(profile.ItemAnimation, PerfectFireWindowTicks) &&
                   IsWithinWindow(profile.ItemTime, PerfectFireWindowTicks);
        }

        internal static bool ShouldScheduleNextTick(PerfectRevolverItemProfile profile)
        {
            if (profile == null || profile.DelayUseItem || profile.ReuseDelay > 0)
            {
                return false;
            }

            if (profile.ItemAnimation <= 0 && profile.ItemTime <= 0)
            {
                return false;
            }

            return NormalizeFireWindow(profile.ItemAnimation) == 0 &&
                   NormalizeFireWindow(profile.ItemTime) == 0;
        }

        internal static PerfectRevolverAttackPlan CreateAttackPlan(PerfectRevolverItemProfile profile, long currentTick, long scheduledPressTick)
        {
            if (profile == null)
            {
                return new PerfectRevolverAttackPlan
                {
                    PressAttack = false,
                    NextScheduledPressTick = 0,
                    Reason = "profileUnavailable"
                };
            }

            var nextScheduledTick = scheduledPressTick;
            var scheduledForThisTick = scheduledPressTick != 0 && scheduledPressTick == currentTick;
            if (scheduledPressTick != 0 && currentTick >= scheduledPressTick)
            {
                nextScheduledTick = 0;
            }

            if (scheduledForThisTick && IsPerfectFireWindow(profile))
            {
                return new PerfectRevolverAttackPlan
                {
                    PressAttack = true,
                    NextScheduledPressTick = 0,
                    Reason = "scheduledFireWindow",
                    ScheduledForThisTick = true,
                    FireWindow = true
                };
            }

            if (!IsReadyToFire(profile))
            {
                var scheduleNextTick = ShouldScheduleNextTick(profile);
                return new PerfectRevolverAttackPlan
                {
                    PressAttack = false,
                    NextScheduledPressTick = scheduleNextTick ? currentTick + 1 : nextScheduledTick,
                    Reason = scheduleNextTick ? "armingNextTick" : "coolingDown",
                    ScheduledForThisTick = scheduledForThisTick,
                    FireWindow = IsPerfectFireWindow(profile),
                    ScheduleNextTick = scheduleNextTick
                };
            }

            return new PerfectRevolverAttackPlan
            {
                PressAttack = true,
                NextScheduledPressTick = 0,
                Reason = "ready",
                Ready = true,
                FireWindow = true
            };
        }

        private static bool IsWithinWindow(int value, int windowTicks)
        {
            if (windowTicks <= 0)
            {
                return value <= 0;
            }

            return Math.Max(0, value) <= windowTicks;
        }

        private static int NormalizeFireWindow(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            value -= PerfectFireWindowTicks;
            return value <= 0 ? 0 : value;
        }

        private static int GetRatedCooldownTicks(PerfectRevolverItemProfile profile)
        {
            if (profile == null)
            {
                return DefaultRevolverCooldownTicks;
            }

            var ticks = Math.Max(profile.UseAnimation, profile.UseTime);
            if (ticks <= 0)
            {
                ticks = DefaultRevolverCooldownTicks;
            }

            if (profile.ItemReuseDelay > 0)
            {
                ticks += profile.ItemReuseDelay;
            }

            return ticks;
        }

        private static long GetScheduledPressTick()
        {
            lock (SyncRoot)
            {
                return _scheduledPressTick;
            }
        }

        private static void SetScheduledPressTick(long tick)
        {
            lock (SyncRoot)
            {
                _scheduledPressTick = tick;
            }
        }

        private static void SetDirectTakeoverActive(bool active, bool pressed)
        {
            lock (SyncRoot)
            {
                _directTakeoverActive = active;
                _lastAttackPressed = active && pressed;
            }
        }

        private static void ResetItemCheckTakeoverState(string reason, long tick)
        {
            lock (SyncRoot)
            {
                _directTakeoverActive = false;
                _lastAttackPressed = false;
                _scheduledPressTick = 0;
            }

            TerrariaInputCompat.ClearPerfectRevolverSuppressedUseItem();
            RecordDecision("skipped", reason, tick, false);
        }

        private static void RecordDirectTakeoverEvent(PerfectRevolverItemProfile profile, PerfectRevolverAttackPlan plan, long tick)
        {
            if (profile == null || plan == null)
            {
                return;
            }

            var before = "{" +
                         "\"selectedSlot\":" + profile.SelectedSlot.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"selectedSlotDisplay\":" + (profile.SelectedSlot + 1).ToString(CultureInfo.InvariantCulture) + "," +
                         "\"itemType\":" + profile.ItemType.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"itemName\":\"" + EscapeJson(profile.ItemName ?? string.Empty) + "\"," +
                         "\"itemAnimation\":" + profile.ItemAnimation.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"itemTime\":" + profile.ItemTime.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"reuseDelay\":" + profile.ReuseDelay.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"revolverCritChanceBonus\":" + profile.RevolverCritChanceBonus.ToString(CultureInfo.InvariantCulture) + "," +
                         "\"delayUseItem\":" + (profile.DelayUseItem ? "true" : "false") +
                         "}";

            var verification = "{" +
                               "\"directAttackTakeover\":true," +
                               "\"itemCheckScopedTakeover\":true," +
                               "\"attackPressed\":" + (plan.PressAttack ? "true" : "false") + "," +
                               "\"attackReleasedWhileCooling\":" + (!plan.PressAttack ? "true" : "false") + "," +
                               "\"scheduledForThisTick\":" + (plan.ScheduledForThisTick ? "true" : "false") + "," +
                               "\"scheduleNextTick\":" + (plan.ScheduleNextTick ? "true" : "false") + "," +
                               "\"fireWindow\":" + (plan.FireWindow ? "true" : "false") + "," +
                               "\"ready\":" + (plan.Ready ? "true" : "false") + "," +
                               "\"fireWindowTicks\":" + PerfectFireWindowTicks.ToString(CultureInfo.InvariantCulture) + "," +
                               "\"ratedCooldownTicks\":" + GetRatedCooldownTicks(profile).ToString(CultureInfo.InvariantCulture) + "," +
                               "\"revolverCritChanceBonusBefore\":" + profile.RevolverCritChanceBonus.ToString(CultureInfo.InvariantCulture) + "," +
                               "\"nextScheduledPressTick\":" + plan.NextScheduledPressTick.ToString(CultureInfo.InvariantCulture) + "," +
                               "\"gameUpdateCount\":" + tick.ToString(CultureInfo.InvariantCulture) + "," +
                               "\"allowCombatAim\":true," +
                               "\"noItemUseBridge\":true," +
                               "\"channelWritten\":false," +
                               "\"reason\":\"" + EscapeJson(plan.Reason ?? string.Empty) + "\"" +
                               "}";

            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                ScenarioNames.CombatPerfectRevolver,
                "RawInput",
                string.Empty,
                plan.PressAttack ? "Applied" : "Observed",
                DiagnosticResultCode.Succeeded.ToString(),
                plan.PressAttack
                    ? "Perfect revolver applied ItemCheck attack press."
                    : "Perfect revolver applied ItemCheck attack release.",
                0,
                before,
                "{}",
                verification,
                "Automation",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
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

        private static string ReadItemName(object item)
        {
            var name = GameStateReflection.GetMember(item, "Name") ??
                       GameStateReflection.GetMember(item, "name");
            return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
        }

        internal sealed class PerfectRevolverItemProfile
        {
            public bool UseItemHeld;
            public bool UseItemReleased;
            public int ItemAnimation;
            public int ItemTime;
            public bool ItemTimeIsZero;
            public int ReuseDelay;
            public bool DelayUseItem;
            public int AltFunctionUse;
            public int RevolverCritChanceBonus;
            public long GameUpdateCount;
            public int SelectedSlot = -1;
            public int ItemType;
            public int ItemStack;
            public string ItemName = string.Empty;
            public int UseStyle;
            public int UseAnimation;
            public int UseTime;
            public int ItemReuseDelay;
            public bool Channel;
        }

        internal sealed class PerfectRevolverAttackPlan
        {
            public bool PressAttack;
            public long NextScheduledPressTick;
            public string Reason = string.Empty;
            public bool ScheduledForThisTick;
            public bool ScheduleNextTick;
            public bool FireWindow;
            public bool Ready;
        }
    }
}
