using System;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    public static class CombatItemCheckAutoClickService
    {
        private const string ScopeName = "CombatAutoClickerItemCheck";
        private const int LifeCrystalItemType = 29;
        private const int MouseItemSlot = 58;
        private const int RainbowRodItemType = 495;
        private const int RevolverItemType = 2269;
        private const int RodOfHarmonyItemType = 5335;
        private static readonly int[] KnownFishingRodItemTypes =
        {
            2289,
            2291,
            2292,
            2293,
            2294,
            2295,
            2296,
            2421,
            2422,
            4325,
            4442
        };
        private static readonly object DiagnosticsSyncRoot = new object();
        private static ItemCheckAutoClickDiagnostics _diagnostics = new ItemCheckAutoClickDiagnostics();

        public static ItemCheckAutoClickDiagnostics GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static bool TryBeginItemCheckTakeover(object player, out TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            takeover = null;
            try
            {
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                if (!settings.CombatAutoClickerEnabled)
                {
                    RecordDecision(ItemCheckAutoClickDecision.NoOp("disabled"), null);
                    return false;
                }

                if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
                {
                    RecordDecision(ItemCheckAutoClickDecision.NoOp(player == null ? "playerUnavailable" : "notLocalPlayer"), null);
                    return false;
                }

                var ui = TerrariaInputCompat.ReadUiInputContext(player);
                if (ui.MainTypeUnavailable ||
                    ui.GameMenu ||
                    ui.ChatOpen ||
                    ui.NpcChatOpen ||
                    ui.MouseCapturedByUi)
                {
                    RecordDecision(ItemCheckAutoClickDecision.NoOp("uiBlocked"), null);
                    return false;
                }

                bool physicalHeld;
                if (!TerrariaInputCompat.TryReadPhysicalMouseLeftHeld(out physicalHeld))
                {
                    RecordReadFailure("physicalInput:" + TerrariaInputCompat.LastInputCompatError);
                    RecordDecision(ItemCheckAutoClickDecision.NoOp("physicalInputUnavailable"), null);
                    return false;
                }

                ItemCheckAutoClickProfile profile;
                string reason;
                if (!TryReadProfile(player, out profile, out reason))
                {
                    RecordReadFailure("profile:" + reason);
                    RecordDecision(ItemCheckAutoClickDecision.NoOp("profile:" + reason), null);
                    return false;
                }

                var decision = CreateDecision(
                    profile,
                    settings.CombatAutoClickerEnabled,
                    physicalHeld,
                    profile.VanillaAutoReuseAllWeapons);
                RecordDecision(decision, profile);
                if (!decision.ApplyTakeover)
                {
                    return false;
                }

                // This is the new Player.ItemCheck scoped fresh-click path. Do
                // not route it back through the removed input-source policy.
                if (!TerrariaInputCompat.TryBeginScopedUseItemClickTakeover(player, decision.PressAttack, ScopeName, out takeover))
                {
                    RecordReadFailure("takeover:" + TerrariaInputCompat.LastInputCompatError);
                    RecordDecision(ItemCheckAutoClickDecision.NoOp("takeover:" + TerrariaInputCompat.LastInputCompatError), profile);
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatItemCheckAutoClickService.TryBeginItemCheckTakeover", error);
                LogThrottle.ErrorThrottled(
                    "combat-itemcheck-auto-clicker-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatItemCheckAutoClickService",
                    "Combat auto clicker ItemCheck takeover failed; exception swallowed.", error);
                RecordDecision(ItemCheckAutoClickDecision.NoOp("exception:" + error.GetType().Name), null);
                return false;
            }
        }

        public static void RecordRestoreStatus(bool restored)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new ItemCheckAutoClickDiagnostics() : _diagnostics.Clone();
                current.LastRestored = restored;
                _diagnostics = current;
            }
        }

        public static void RecordExternalSkip(string reason)
        {
            RecordDecision(ItemCheckAutoClickDecision.NoOp(string.IsNullOrWhiteSpace(reason) ? "externalScopedUse" : reason), null);
        }

        public static ItemCheckAutoClickDecision CreateDecision(
            ItemCheckAutoClickProfile profile,
            bool featureEnabled,
            bool physicalUseHeld,
            bool vanillaAutoReuseAllWeapons)
        {
            if (!featureEnabled)
            {
                return ItemCheckAutoClickDecision.NoOp("disabled");
            }

            if (!physicalUseHeld)
            {
                return ItemCheckAutoClickDecision.NoOp("physicalUseNotHeld");
            }

            if (profile == null || !profile.Available)
            {
                return ItemCheckAutoClickDecision.NoOp(profile == null ? "profileUnavailable" : profile.Reason);
            }

            if (!profile.VanillaAutoReuseAllAvailable)
            {
                return ItemCheckAutoClickDecision.NoOp("vanillaAutoReuseUnavailable");
            }

            string eligibilityReason;
            var eligible = IsEligible(profile, out eligibilityReason);
            if (!profile.Eligible || !eligible)
            {
                return ItemCheckAutoClickDecision.NoOp(!string.IsNullOrWhiteSpace(profile.Reason) ? profile.Reason : eligibilityReason);
            }

            string vanillaReason;
            if (vanillaAutoReuseAllWeapons && IsCoveredByVanillaAutoReuse(profile, out vanillaReason))
            {
                return ItemCheckAutoClickDecision.NoOp(vanillaReason);
            }

            return IsReady(profile)
                ? ItemCheckAutoClickDecision.Press("ready")
                : ItemCheckAutoClickDecision.Release("cooldown");
        }

        public static bool IsReady(ItemCheckAutoClickProfile profile)
        {
            return profile != null &&
                   profile.ItemAnimation <= 0 &&
                   profile.ItemTime <= 0 &&
                   profile.ReuseDelay <= 0 &&
                   !profile.DelayUseItem;
        }

        internal static bool TryReadProfileForTesting(object player, out ItemCheckAutoClickProfile profile, out string reason)
        {
            return TryReadProfile(player, out profile, out reason);
        }

        internal static bool IsSeedItemTypeForTesting(int itemType)
        {
            return IsSeedItemType(itemType);
        }

        private static bool TryReadProfile(object player, out ItemCheckAutoClickProfile profile, out string reason)
        {
            profile = new ItemCheckAutoClickProfile();
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
                profile.Available = false;
                profile.Reason = !active ? "playerInactive" : dead ? "playerDead" : "playerGhost";
                reason = profile.Reason;
                return true;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                reason = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            profile.SelectedSlot = selectedSlot;
            if (selectedSlot < 0 || (selectedSlot > 9 && selectedSlot != MouseItemSlot))
            {
                profile.Available = false;
                profile.Reason = "selectedSlotNotHotbar";
                reason = profile.Reason;
                return true;
            }

            object item;
            if (selectedSlot == MouseItemSlot)
            {
                // selectedItem == 58 is Terraria's mouse-item slot. Read it for
                // profiling only; do not write inventory selection here.
                if (!TerrariaInputCompat.TryGetMouseItem(out item))
                {
                    reason = "mouseItemUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                    return false;
                }
                profile.UsesMouseItem = true;
            }
            else
            {
                var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
                if (inventory == null || selectedSlot >= inventory.Count)
                {
                    reason = "inventoryUnavailable";
                    return false;
                }

                item = inventory[selectedSlot];
            }

            if (item == null)
            {
                profile.Available = false;
                profile.Reason = selectedSlot == MouseItemSlot ? "mouseItemUnavailable" : "selectedItemUnavailable";
                reason = profile.Reason;
                return true;
            }

            profile.ItemType = ReadInt(item, "type", 0);
            profile.ItemStack = ReadInt(item, "stack", 0);
            profile.UseStyle = ReadInt(item, "useStyle", 0);
            profile.UseAnimation = ReadInt(item, "useAnimation", 0);
            profile.UseTime = ReadInt(item, "useTime", 0);
            profile.Damage = ReadInt(item, "damage", 0);
            profile.FishingPole = ReadInt(item, "fishingPole", 0);
            profile.AutoReuse = ReadBool(item, "autoReuse", false);
            profile.Channel = ReadBool(item, "channel", false);
            profile.PlayerChannel = ReadBool(player, "channel", false);
            profile.ItemAnimation = ReadInt(player, "itemAnimation", 0);
            profile.ItemTime = ReadInt(player, "itemTime", 0);
            profile.ReuseDelay = ReadInt(player, "reuseDelay", 0);
            profile.DelayUseItem = ReadBool(player, "delayUseItem", false);

            bool vanillaAutoReuseAllWeapons;
            if (!TryReadVanillaAutoReuseAllWeapons(player, out vanillaAutoReuseAllWeapons, out reason))
            {
                profile.Available = false;
                profile.Reason = reason;
                profile.VanillaAutoReuseAllAvailable = false;
                reason = profile.Reason;
                return true;
            }

            profile.VanillaAutoReuseAllAvailable = true;
            profile.VanillaAutoReuseAllWeapons = vanillaAutoReuseAllWeapons;

            string eligibilityReason;
            if (!IsEligible(profile, out eligibilityReason))
            {
                profile.Eligible = false;
                profile.Reason = eligibilityReason;
                reason = profile.Reason;
                return true;
            }

            profile.Eligible = true;
            profile.Reason = string.Empty;
            return true;
        }

        private static bool IsSeedItemType(int itemType)
        {
            return itemType == LifeCrystalItemType ||
                   itemType == RainbowRodItemType ||
                   itemType == RodOfHarmonyItemType;
        }

        internal static bool IsKnownFishingRodItemTypeForTesting(int itemType)
        {
            return IsKnownFishingRodItemType(itemType);
        }

        private static bool IsEligible(ItemCheckAutoClickProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                reason = "profileUnavailable";
                return false;
            }

            if (profile.ItemType <= 0 || profile.ItemStack <= 0)
            {
                reason = "notUsable";
                return false;
            }

            if (profile.Channel)
            {
                reason = "excludedChannelItem";
                return false;
            }

            // Fishing rods and vanilla revolver are hard exclusions even when
            // the auto clicker feature is enabled.
            if (IsFishingRod(profile))
            {
                reason = "excludedFishingRod";
                return false;
            }

            if (profile.ItemType == RevolverItemType)
            {
                reason = "excludedRevolver";
                return false;
            }

            if (profile.UseStyle <= 0 || profile.UseAnimation <= 0 || profile.UseTime <= 0)
            {
                reason = "notUsable";
                return false;
            }

            return true;
        }

        private static bool IsCoveredByVanillaAutoReuse(ItemCheckAutoClickProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile == null)
            {
                return false;
            }

            if (profile.AutoReuse)
            {
                reason = "itemAutoReuse";
                return true;
            }

            // Terraria global auto-reuse owns weapons it already covers; this
            // feature stands down instead of double-clicking them.
            if (profile.Damage > 0 && (!profile.Channel || !profile.PlayerChannel))
            {
                reason = "vanillaAutoReuseCovered";
                return true;
            }

            return false;
        }

        private static bool IsFishingRod(ItemCheckAutoClickProfile profile)
        {
            return profile != null &&
                   (profile.FishingPole > 0 || IsKnownFishingRodItemType(profile.ItemType));
        }

        private static bool IsKnownFishingRodItemType(int itemType)
        {
            for (var index = 0; index < KnownFishingRodItemTypes.Length; index++)
            {
                if (KnownFishingRodItemTypes[index] == itemType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadVanillaAutoReuseAllWeapons(object player, out bool enabled, out string reason)
        {
            enabled = false;
            reason = string.Empty;

            if (TryConvertBool(GameStateReflection.GetStaticMember(TerrariaRuntimeTypes.MainType, "SettingsEnabled_AutoReuseAllItems"), out enabled))
            {
                return true;
            }

            if (GameStateReflection.TryGetBool(player, "autoReuseAllWeapons", out enabled))
            {
                return true;
            }

            reason = "vanillaAutoReuseUnavailable";
            return false;
        }

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
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
                return false;
            }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(instance, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(instance, name, out value) ? value : fallback;
        }

        private static void RecordReadFailure(string reason)
        {
            LogThrottle.WarnThrottled(
                "combat-itemcheck-auto-clicker-read-failed",
                TimeSpan.FromSeconds(5),
                "CombatItemCheckAutoClickService",
                "Combat auto clicker ItemCheck core skipped fail-closed: " + (reason ?? string.Empty));
        }

        internal static void RecordDecisionForTesting(ItemCheckAutoClickDecision decision, ItemCheckAutoClickProfile profile)
        {
            RecordDecision(decision, profile);
        }

        private static void RecordDecision(ItemCheckAutoClickDecision decision, ItemCheckAutoClickProfile profile)
        {
            decision = decision ?? ItemCheckAutoClickDecision.NoOp("decisionUnavailable");
            var apply = decision.ApplyTakeover;
            var press = apply && decision.PressAttack;
            var release = apply && !decision.PressAttack;
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new ItemCheckAutoClickDiagnostics() : _diagnostics.Clone();
                current.LastDecision = apply ? (press ? "scopedPress" : "scopedRelease") : "noOp";
                current.LastReason = decision.Reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastItemType = profile == null ? 0 : profile.ItemType;
                current.LastVanillaAutoReuseAllAvailable = profile != null && profile.VanillaAutoReuseAllAvailable;
                current.LastVanillaAutoReuseAllWeapons = profile != null && profile.VanillaAutoReuseAllWeapons;
                current.LastScopedPress = press;
                current.LastScopedRelease = release;
                current.LastRestored = false;
                if (apply)
                {
                    current.AppliedCount++;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }

        public sealed class ItemCheckAutoClickDiagnostics
        {
            public string LastDecision { get; set; }
            public string LastReason { get; set; }
            public DateTime? LastDecisionUtc { get; set; }
            public int LastItemType { get; set; }
            public bool LastVanillaAutoReuseAllAvailable { get; set; }
            public bool LastVanillaAutoReuseAllWeapons { get; set; }
            public bool LastScopedPress { get; set; }
            public bool LastScopedRelease { get; set; }
            public bool LastRestored { get; set; }
            public long AppliedCount { get; set; }
            public long SkippedCount { get; set; }

            public ItemCheckAutoClickDiagnostics()
            {
                LastDecision = string.Empty;
                LastReason = string.Empty;
            }

            public ItemCheckAutoClickDiagnostics Clone()
            {
                return (ItemCheckAutoClickDiagnostics)MemberwiseClone();
            }
        }

        public sealed class ItemCheckAutoClickProfile
        {
            public bool Available { get; set; }
            public bool Eligible { get; set; }
            public string Reason { get; set; }
            public int SelectedSlot { get; set; }
            public bool UsesMouseItem { get; set; }
            public int ItemType { get; set; }
            public int ItemStack { get; set; }
            public int UseStyle { get; set; }
            public int UseAnimation { get; set; }
            public int UseTime { get; set; }
            public int Damage { get; set; }
            public int FishingPole { get; set; }
            public bool AutoReuse { get; set; }
            public bool Channel { get; set; }
            public bool PlayerChannel { get; set; }
            public int ItemAnimation { get; set; }
            public int ItemTime { get; set; }
            public int ReuseDelay { get; set; }
            public bool DelayUseItem { get; set; }
            public bool VanillaAutoReuseAllAvailable { get; set; }
            public bool VanillaAutoReuseAllWeapons { get; set; }

            public ItemCheckAutoClickProfile()
            {
                Available = true;
                VanillaAutoReuseAllAvailable = true;
                Reason = string.Empty;
                SelectedSlot = -1;
            }
        }

        public sealed class ItemCheckAutoClickDecision
        {
            private ItemCheckAutoClickDecision(bool applyTakeover, bool pressAttack, string reason)
            {
                ApplyTakeover = applyTakeover;
                PressAttack = pressAttack;
                Reason = reason ?? string.Empty;
            }

            public bool ApplyTakeover { get; private set; }
            public bool PressAttack { get; private set; }
            public string Reason { get; private set; }

            public static ItemCheckAutoClickDecision NoOp(string reason)
            {
                return new ItemCheckAutoClickDecision(false, false, reason);
            }

            public static ItemCheckAutoClickDecision Press(string reason)
            {
                return new ItemCheckAutoClickDecision(true, true, reason);
            }

            public static ItemCheckAutoClickDecision Release(string reason)
            {
                return new ItemCheckAutoClickDecision(true, false, reason);
            }
        }
    }
}
