using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Ui;
using JueMingZ.Hooks;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void CombatPerfectRevolverItemCheckTakeoverMirrorsHelperCadence()
        {
            var profile = new CombatPerfectRevolverService.PerfectRevolverItemProfile
            {
                SelectedSlot = 0,
                ItemType = 2269,
                ItemName = "Revolver",
                UseStyle = 5,
                UseAnimation = 22,
                UseTime = 22,
                ItemStack = 1,
                ItemAnimation = 0,
                ItemTime = 0,
                ItemTimeIsZero = true,
                ReuseDelay = 0,
                DelayUseItem = false
            };

            var idle = CombatPerfectRevolverService.CreateAttackPlan(profile, 100, 0);
            if (!idle.PressAttack || !idle.Ready || idle.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to press attack while idle.");
            }

            profile.ItemAnimation = 8;
            profile.ItemTime = 8;
            profile.ItemTimeIsZero = false;
            var cooling = CombatPerfectRevolverService.CreateAttackPlan(profile, 101, 0);
            if (cooling.PressAttack || cooling.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to release attack while outside the helper fire window.");
            }

            profile.ItemAnimation = 2;
            profile.ItemTime = 2;
            var arming = CombatPerfectRevolverService.CreateAttackPlan(profile, 102, 0);
            if (arming.PressAttack || !arming.ScheduleNextTick || arming.NextScheduledPressTick != 103)
            {
                throw new InvalidOperationException("Expected perfect revolver to release this tick and schedule the next tick in the 2 tick fire window.");
            }

            var scheduled = CombatPerfectRevolverService.CreateAttackPlan(profile, 103, 103);
            if (!scheduled.PressAttack || !scheduled.ScheduledForThisTick || scheduled.NextScheduledPressTick != 0)
            {
                throw new InvalidOperationException("Expected perfect revolver to press attack on the scheduled helper fire tick.");
            }
        }

        private static void CombatPerfectRevolverSchedulesOnlyInFireWindow()
        {
            var profile = new CombatPerfectRevolverService.PerfectRevolverItemProfile
            {
                ItemAnimation = 3,
                ItemTime = 3,
                ItemTimeIsZero = false,
                ReuseDelay = 0,
                DelayUseItem = false
            };

            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait outside the 2 tick helper fire window.");
            }

            profile.ItemAnimation = 2;
            profile.ItemTime = 2;
            if (!CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                !CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to arm next tick inside the 2 tick helper fire window.");
            }

            profile.DelayUseItem = true;
            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait while delayUseItem is active.");
            }

            profile.DelayUseItem = false;
            profile.ReuseDelay = 1;
            if (CombatPerfectRevolverService.IsPerfectFireWindow(profile) ||
                CombatPerfectRevolverService.ShouldScheduleNextTick(profile))
            {
                throw new InvalidOperationException("Expected perfect revolver to wait while reuseDelay is active.");
            }
        }

        private static void CombatAutoClickerInputProbeTargetsReportedItems()
        {
            if (!CombatAutoClickerItemCheckInputProbe.IsProbeItemForTesting(29) ||
                !CombatAutoClickerItemCheckInputProbe.IsProbeItemForTesting(495) ||
                !CombatAutoClickerItemCheckInputProbe.IsProbeItemForTesting(5335))
            {
                throw new InvalidOperationException("Expected auto clicker input probe to cover the reported sample items.");
            }

            if (CombatAutoClickerItemCheckInputProbe.IsProbeItemForTesting(2269) ||
                CombatAutoClickerItemCheckInputProbe.IsProbeItemForTesting(1))
            {
                throw new InvalidOperationException("Expected auto clicker input probe to stay scoped away from ordinary combat samples.");
            }

            if (!string.Equals(CombatAutoClickerItemCheckInputProbe.BuildAnimationBucketForTesting(0), "0", StringComparison.Ordinal) ||
                !string.Equals(CombatAutoClickerItemCheckInputProbe.BuildAnimationBucketForTesting(2), "2", StringComparison.Ordinal) ||
                !string.Equals(CombatAutoClickerItemCheckInputProbe.BuildAnimationBucketForTesting(8), "6-10", StringComparison.Ordinal) ||
                !string.Equals(CombatAutoClickerItemCheckInputProbe.BuildAnimationBucketForTesting(24), "21+", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto clicker input probe animation buckets to remain stable.");
            }
        }

        private static void CombatItemCheckAutoClickerCorePressesReadyItem()
        {
            var profile = CreateAutoClickerProfile();
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, false);
            if (!decision.ApplyTakeover || !decision.PressAttack ||
                !string.Equals(decision.Reason, "ready", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ItemCheck auto clicker core to press a ready item.");
            }
        }

        private static void CombatItemCheckAutoClickerCoreReleasesCooldownItem()
        {
            var profile = CreateAutoClickerProfile();
            profile.ItemAnimation = 12;
            profile.ItemTime = 7;
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, false);
            if (!decision.ApplyTakeover || decision.PressAttack ||
                !string.Equals(decision.Reason, "cooldown", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ItemCheck auto clicker core to release during cooldown.");
            }
        }

        private static void CombatItemCheckAutoClickerCoreDisabledNoOp()
        {
            var profile = CreateAutoClickerProfile();
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, false, true, false);
            if (decision.ApplyTakeover || decision.PressAttack ||
                !string.Equals(decision.Reason, "disabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected disabled ItemCheck auto clicker core to no-op.");
            }
        }

        private static void CombatItemCheckAutoClickerFourQuadrants()
        {
            var profile = CreateAutoClickerProfile();
            ExpectAutoClickDecision(profile, false, false, false, false, "disabled", "vanilla off / feature off");
            ExpectAutoClickDecision(profile, false, true, false, false, "disabled", "vanilla on / feature off");
            ExpectAutoClickDecision(profile, true, false, true, true, "ready", "vanilla off / feature on");
            ExpectAutoClickDecision(profile, true, true, true, true, "ready", "vanilla on / feature on补非原版覆盖物品");
        }

        private static void CombatItemCheckAutoClickerRespectsVanillaAutoReuse()
        {
            var autoReuseItem = CreateAutoClickerProfile(3507, 24, true, false, false, 0);
            ExpectAutoClickDecision(autoReuseItem, true, true, false, false, "itemAutoReuse", "item.autoReuse with vanilla auto reuse on");
            ExpectAutoClickDecision(autoReuseItem, true, false, true, true, "ready", "item.autoReuse with vanilla auto reuse off");

            var weapon = CreateAutoClickerProfile(1, 10, false, false, false, 0);
            ExpectAutoClickDecision(weapon, true, true, false, false, "vanillaAutoReuseCovered", "ordinary damage weapon with vanilla auto reuse on");
            ExpectAutoClickDecision(weapon, true, false, true, true, "ready", "ordinary damage weapon with vanilla auto reuse off");
        }

        private static void CombatItemCheckAutoClickerSamplesAndHardExcludes()
        {
            ExpectAutoClickDecision(CreateAutoClickerProfile(29), true, true, true, true, "ready", "life crystal sample");
            ExpectAutoClickDecision(CreateAutoClickerProfile(5335), true, true, true, true, "ready", "Rod of Harmony sample");

            var rainbowRod = CreateAutoClickerProfile(495, 74, false, true, true, 0);
            ExpectAutoClickDecision(rainbowRod, true, true, false, false, "excludedChannelItem", "Rainbow Rod while vanilla channel is already active");

            var channelWeapon = CreateAutoClickerProfile(113, 35, false, true, false, 0);
            ExpectAutoClickDecision(channelWeapon, true, false, false, false, "excludedChannelItem", "channel weapon with vanilla auto reuse off");
            ExpectAutoClickDecision(channelWeapon, true, true, false, false, "excludedChannelItem", "channel weapon with vanilla auto reuse on");

            ExpectAutoClickDecision(CreateAutoClickerProfile(2294, 0, false, false, false, 45), true, false, false, false, "excludedFishingRod", "fishing pole field exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(2289, 0, false, false, false, 0), true, false, false, false, "excludedFishingRod", "known fishing pole id fallback");
            ExpectAutoClickDecision(CreateAutoClickerProfile(2269, 15, false, false, false, 0), true, false, false, false, "excludedRevolver", "perfect revolver exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(itemType: 3509, pick: 55), true, false, false, false, "excludedToolItem", "pickaxe tool exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(itemType: 10, axe: 9), true, false, false, false, "excludedToolItem", "axe tool exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(itemType: 7, hammer: 35), true, false, false, false, "excludedToolItem", "hammer tool exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(itemType: 990, pick: 200, axe: 110), true, false, false, false, "excludedToolItem", "pickaxe-axe tool exclusion");
            ExpectAutoClickDecision(CreateAutoClickerProfile(itemType: 2176, pick: 200), true, false, false, false, "excludedToolItem", "drill/claw pick-field tool exclusion");

            var phaseblade = CreateAutoClickerProfile(itemType: 5535, damage: 30, shootsOnUseRelease: true);
            ExpectAutoClickDecision(phaseblade, true, false, false, false, "excludedShootsOnUseReleaseItem", "phaseblade release-on-use exclusion with vanilla auto reuse off");
            ExpectAutoClickDecision(phaseblade, true, true, false, false, "excludedShootsOnUseReleaseItem", "phaseblade release-on-use exclusion with vanilla auto reuse on");

            if (!CombatItemCheckAutoClickService.IsKnownFishingRodItemTypeForTesting(4442) ||
                CombatItemCheckAutoClickService.IsKnownFishingRodItemTypeForTesting(1))
            {
                throw new InvalidOperationException("Expected auto clicker fishing rod fallback id table to stay scoped.");
            }
        }

        private static void CombatItemCheckAutoClickerReadsToolFields()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousAutoReuseAll = Terraria.Main.SettingsEnabled_AutoReuseAllItems;
            try
            {
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = false;
                var player = new FakePlayer
                {
                    selectedItem = 0,
                    active = true
                };
                player.inventory[0] = new FakeItem
                {
                    type = 2176,
                    stack = 1,
                    useStyle = 1,
                    useAnimation = 20,
                    useTime = 20,
                    pick = 200,
                    axe = 50,
                    hammer = 30,
                    Name = "Mushroom Claw"
                };

                CombatItemCheckAutoClickService.ItemCheckAutoClickProfile profile;
                string reason;
                if (!CombatItemCheckAutoClickService.TryReadProfileForTesting(player, out profile, out reason))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker to read tool fields: " + reason);
                }

                if (profile == null ||
                    !profile.Available ||
                    profile.Eligible ||
                    profile.Pick != 200 ||
                    profile.Axe != 50 ||
                    profile.Hammer != 30 ||
                    !string.Equals(profile.Reason, "excludedToolItem", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected tool profile fields to be read and excluded, got pick=" +
                                                        (profile == null ? 0 : profile.Pick) +
                                                        " axe=" + (profile == null ? 0 : profile.Axe) +
                                                        " hammer=" + (profile == null ? 0 : profile.Hammer) +
                                                        " reason=" + (profile == null ? string.Empty : profile.Reason) + ".");
                }

                var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, profile.VanillaAutoReuseAllWeapons);
                if (decision.ApplyTakeover ||
                    !string.Equals(decision.Reason, "excludedToolItem", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected read tool profile to be excluded, got " + decision.Reason + ".");
                }
            }
            finally
            {
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = previousAutoReuseAll;
                restoreRuntimeTypes();
            }
        }

        private static void CombatItemCheckAutoClickerReadsShootsOnReleaseSet()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousAutoReuseAll = Terraria.Main.SettingsEnabled_AutoReuseAllItems;
            var previousSetValue = Terraria.ID.ItemID.Sets.ShootsOnUseRelease[5535];
            try
            {
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = false;
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[5535] = true;
                var player = new FakePlayer
                {
                    selectedItem = 0,
                    active = true
                };
                player.inventory[0] = new FakeItem
                {
                    type = 5535,
                    stack = 1,
                    useStyle = 1,
                    useAnimation = 15,
                    useTime = 15,
                    damage = 30,
                    Name = "Pink Phaseblade"
                };

                CombatItemCheckAutoClickService.ItemCheckAutoClickProfile profile;
                string reason;
                if (!CombatItemCheckAutoClickService.TryReadProfileForTesting(player, out profile, out reason))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker to read ShootsOnUseRelease set: " + reason);
                }

                if (profile == null ||
                    !profile.Available ||
                    profile.Eligible ||
                    !profile.ShootsOnUseRelease ||
                    !string.Equals(profile.Reason, "excludedShootsOnUseReleaseItem", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected ShootsOnUseRelease profile to be excluded, got shootsOnUseRelease=" +
                                                        (profile != null && profile.ShootsOnUseRelease) +
                                                        " reason=" + (profile == null ? string.Empty : profile.Reason) + ".");
                }

                var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, profile.VanillaAutoReuseAllWeapons);
                if (decision.ApplyTakeover ||
                    !string.Equals(decision.Reason, "excludedShootsOnUseReleaseItem", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected ShootsOnUseRelease profile to be excluded, got " + decision.Reason + ".");
                }
            }
            finally
            {
                Terraria.ID.ItemID.Sets.ShootsOnUseRelease[5535] = previousSetValue;
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = previousAutoReuseAll;
                restoreRuntimeTypes();
            }
        }

        private static void CombatItemCheckAutoClickerFailsClosedWhenVanillaSwitchUnavailable()
        {
            var profile = CreateAutoClickerProfile();
            profile.VanillaAutoReuseAllAvailable = false;
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, false);
            if (decision.ApplyTakeover ||
                !string.Equals(decision.Reason, "vanillaAutoReuseUnavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected ItemCheck auto clicker to fail closed when vanilla auto reuse state is unavailable.");
            }
        }

        private static void CombatItemCheckAutoClickerReadsMouseItemSlot()
        {
            // Regression guard: inventory-open auto-click must read Main.mouseItem
            // when selectedItem is Terraria's mouse-slot sentinel.
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousMouseItem = Terraria.Main.mouseItem;
            var previousAutoReuseAll = Terraria.Main.SettingsEnabled_AutoReuseAllItems;
            try
            {
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = false;
                Terraria.Main.mouseItem = new FakeItem
                {
                    type = 5335,
                    stack = 1,
                    useStyle = 4,
                    useAnimation = 20,
                    useTime = 20,
                    Name = "Rod of Harmony"
                };

                var player = new FakePlayer
                {
                    selectedItem = 58,
                    active = true
                };

                CombatItemCheckAutoClickService.ItemCheckAutoClickProfile profile;
                string reason;
                if (!CombatItemCheckAutoClickService.TryReadProfileForTesting(player, out profile, out reason))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker to read Main.mouseItem: " + reason);
                }

                if (profile == null ||
                    !profile.Available ||
                    !profile.Eligible ||
                    !profile.UsesMouseItem ||
                    profile.SelectedSlot != 58 ||
                    profile.ItemType != 5335)
                {
                    throw new InvalidOperationException("Expected mouse item slot 58 profile, got slot=" +
                                                        (profile == null ? -1 : profile.SelectedSlot) +
                                                        " itemType=" + (profile == null ? 0 : profile.ItemType) +
                                                        " reason=" + (profile == null ? string.Empty : profile.Reason));
                }

                var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, profile.VanillaAutoReuseAllWeapons);
                if (!decision.ApplyTakeover || !decision.PressAttack)
                {
                    throw new InvalidOperationException("Expected mouse-held item profile to request a fresh click, got " + decision.Reason + ".");
                }
            }
            finally
            {
                Terraria.Main.mouseItem = previousMouseItem;
                Terraria.Main.SettingsEnabled_AutoReuseAllItems = previousAutoReuseAll;
                restoreRuntimeTypes();
            }
        }

        private static void CombatItemCheckAutoClickerYieldsToAdjacentScopedUse()
        {
            if (!ItemUseHookCallbacks.ShouldAttemptAutoClickerTakeoverForTesting(false, false, false, false, false, false))
            {
                throw new InvalidOperationException("Expected auto clicker to run when no adjacent scoped use is active.");
            }

            AssertAutoClickerYield(true, false, false, false, false, false, "bridge pending at start");
            AssertAutoClickerYield(false, true, false, false, false, false, "bridge pending now");
            AssertAutoClickerYield(false, false, true, false, false, false, "UseItemPulseBridge");
            AssertAutoClickerYield(false, false, false, true, false, false, "auto mining");
            AssertAutoClickerYield(false, false, false, false, true, false, "auto harvest");
            AssertAutoClickerYield(false, false, false, false, false, true, "auto capture");
        }

        private static void CombatItemCheckAutoClickerTakeoverRestoresInputState()
        {
            try
            {
                // Scoped ItemCheck clicks must restore both Player and Main mouse
                // state after the synthetic fresh click.
                var player = new Terraria.Player
                {
                    active = true,
                    controlUseItem = false,
                    releaseUseItem = false
                };
                ResetFakeMainMouse(false, false);

                var decision = CombatItemCheckAutoClickService.CreateDecision(CreateAutoClickerProfile(), true, true, false);
                if (!decision.ApplyTakeover || !decision.PressAttack)
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker core to request a fresh click before restore test.");
                }

                TerrariaInputCompat.ScopedUseItemTakeover takeover;
                if (!TerrariaInputCompat.TryBeginScopedUseItemClickTakeover(player, decision.PressAttack, "CombatAutoClickerItemCheck", out takeover))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker takeover to apply: " + TerrariaInputCompat.LastInputCompatError);
                }

                if (!player.controlUseItem || !player.releaseUseItem ||
                    !Terraria.Main.mouseLeft || !Terraria.Main.mouseLeftRelease ||
                    takeover == null || !takeover.Pressed ||
                    !string.Equals(takeover.ScopeName, "CombatAutoClickerItemCheck", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker takeover to apply a fresh click scope.");
                }

                if (!TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover))
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker takeover restore to succeed: " + TerrariaInputCompat.LastInputCompatError);
                }

                if (player.controlUseItem || player.releaseUseItem ||
                    Terraria.Main.mouseLeft || Terraria.Main.mouseLeftRelease)
                {
                    throw new InvalidOperationException("Expected ItemCheck auto clicker takeover to restore player and Main mouse state.");
                }
            }
            finally
            {
                ResetFakeMainMouse(false, true);
            }
        }

        private static void CombatItemCheckAutoClickerDiagnosticsRecordScopedDecision()
        {
            var profile = CreateAutoClickerProfile(29);
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, true, true, false);
            CombatItemCheckAutoClickService.RecordDecisionForTesting(decision, profile);

            var diagnostics = CombatItemCheckAutoClickService.GetDiagnostics();
            if (!string.Equals(diagnostics.LastDecision, "scopedPress", StringComparison.Ordinal) ||
                !string.Equals(diagnostics.LastReason, "ready", StringComparison.Ordinal) ||
                diagnostics.LastItemType != 29 ||
                !diagnostics.LastVanillaAutoReuseAllAvailable ||
                diagnostics.LastVanillaAutoReuseAllWeapons ||
                !diagnostics.LastScopedPress ||
                diagnostics.LastScopedRelease ||
                diagnostics.LastRestored)
            {
                throw new InvalidOperationException("Expected ItemCheck auto clicker diagnostics to record scoped press decision.");
            }

            CombatItemCheckAutoClickService.RecordRestoreStatus(true);
            diagnostics = CombatItemCheckAutoClickService.GetDiagnostics();
            if (!diagnostics.LastRestored)
            {
                throw new InvalidOperationException("Expected ItemCheck auto clicker diagnostics to record restore status.");
            }
        }

        private static void ExpectAutoClickDecision(
            CombatItemCheckAutoClickService.ItemCheckAutoClickProfile profile,
            bool featureEnabled,
            bool vanillaAutoReuseAllWeapons,
            bool expectedApply,
            bool expectedPress,
            string expectedReason,
            string label)
        {
            var decision = CombatItemCheckAutoClickService.CreateDecision(profile, featureEnabled, true, vanillaAutoReuseAllWeapons);
            if (decision.ApplyTakeover != expectedApply ||
                decision.PressAttack != expectedPress ||
                !string.Equals(decision.Reason, expectedReason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Expected auto clicker decision " + label + " to be " +
                    expectedApply + "/" + expectedPress + "/" + expectedReason +
                    ", got " + decision.ApplyTakeover + "/" + decision.PressAttack + "/" + decision.Reason + ".");
            }
        }

        private static void AssertAutoClickerYield(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied,
            string label)
        {
            if (ItemUseHookCallbacks.ShouldAttemptAutoClickerTakeoverForTesting(
                bridgePendingAtStart,
                bridgePendingNow,
                pulseApplied,
                autoMiningApplied,
                autoHarvestApplied,
                autoCaptureApplied))
            {
                throw new InvalidOperationException("Expected auto clicker to yield to " + label + ".");
            }
        }

        private static CombatItemCheckAutoClickService.ItemCheckAutoClickProfile CreateAutoClickerProfile(
            int itemType = 29,
            int damage = 0,
            bool autoReuse = false,
            bool channel = false,
            bool playerChannel = false,
            int fishingPole = 0,
            int pick = 0,
            int axe = 0,
            int hammer = 0,
            bool shootsOnUseRelease = false)
        {
            return new CombatItemCheckAutoClickService.ItemCheckAutoClickProfile
            {
                Available = true,
                Eligible = true,
                SelectedSlot = 0,
                ItemType = itemType,
                ItemStack = 1,
                UseStyle = 4,
                UseAnimation = 20,
                UseTime = 20,
                Damage = damage,
                AutoReuse = autoReuse,
                Channel = channel,
                PlayerChannel = playerChannel,
                FishingPole = fishingPole,
                Pick = pick,
                Axe = axe,
                Hammer = hammer,
                ShootsOnUseRelease = shootsOnUseRelease,
                ItemAnimation = 0,
                ItemTime = 0,
                ReuseDelay = 0,
                DelayUseItem = false,
                VanillaAutoReuseAllAvailable = true
            };
        }


    }
}
