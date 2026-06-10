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
        private static void ReleaseHoldPendingExpirationClearsStateBeforeHeldInputCheck()
        {
            var restoreRuntimeTypes = PushFakeTerrariaMainType();
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousPlayerZero = Terraria.Main.player[0];
            var previousMyPlayer = Terraria.Main.myPlayer;
            var settings = AppSettings.CreateDefault();
            settings.CursorAimRadius = 25;
            settings.ReleaseHoldTicks = 1;
            var runtimeSettings = RuntimeSettingsSnapshot.FromSettings(settings);
            TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
            try
            {
                CombatAimReleaseHoldService.Tick(false, runtimeSettings);

                var player = new Terraria.Player
                {
                    whoAmI = 0,
                    active = true,
                    selectedItem = 0
                };
                player.inventory[0] = new FakeItem
                {
                    type = 5535,
                    stack = 1
                };
                Terraria.Main.myPlayer = 0;
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.player[0] = player;

                var target = new CombatTargetSnapshot
                {
                    WhoAmI = 3,
                    Type = 488,
                    Name = "Target Dummy",
                    Active = true,
                    IsTargetDummy = true,
                    Life = 100,
                    LifeMax = 100,
                    HitboxWidth = 20,
                    HitboxHeight = 40
                };
                var recordDecision = new CombatAimItemCheckDecision
                {
                    UseItemHeld = true,
                    SelectedSlot = 0,
                    ItemType = 5535,
                    Selection = new CombatAimTargetSelection
                    {
                        Target = target,
                        SelectedSamplePoint = "center"
                    },
                    AimWorldX = 100f,
                    AimWorldY = 120f,
                    GameUpdateCount = 10
                };
                CombatAimReleaseHoldService.Record(recordDecision, 1);

                var releaseInput = new CombatAimUseInputSnapshot
                {
                    Available = true,
                    UseItemHeld = false,
                    UseItemReleased = true,
                    ItemAnimation = 0,
                    ItemTime = 0,
                    SelectedSlot = 0,
                    ItemType = 5535,
                    GameUpdateCount = 11
                };
                var releaseDecision = new CombatAimItemCheckDecision
                {
                    SelectedSlot = 0,
                    ItemType = 5535,
                    TrackDummy = true
                };
                var readResult = new CombatAimReadResult();
                readResult.Candidates.Add(target);

                string reason;
                if (CombatAimReleaseHoldService.TryApply(player, releaseDecision, readResult, releaseInput, null, settings, out reason) ||
                    !string.Equals(reason, "releaseHoldRangeDisabled", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected release-hold to stay pending behind range validation, got " + reason + ".");
                }

                player.controlUseItem = true;
                player.releaseUseItem = false;
                player.selectedItem = 0;
                Terraria.Main.GameUpdateCount = 12;
                CombatAimReleaseHoldService.Tick(true, runtimeSettings);

                var afterDecision = new CombatAimItemCheckDecision();
                CombatAimReleaseHoldService.DecorateDecision(
                    afterDecision,
                    new CombatAimUseInputSnapshot
                    {
                        Available = true,
                        UseItemHeld = true,
                        SelectedSlot = 0,
                        ItemType = 5535
                    });
                if (!string.Equals(afterDecision.ReleaseHoldState, ReleaseHoldStates.Idle, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected expired release-hold state to clear to Idle, got " + afterDecision.ReleaseHoldState + ".");
                }
            }
            finally
            {
                CombatAimReleaseHoldService.Tick(false, runtimeSettings);
                TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.player[0] = previousPlayerZero;
                Terraria.Main.myPlayer = previousMyPlayer;
                restoreRuntimeTypes();
            }
        }

        private static void ReleaseHoldTargetDummyValidationRespectsTrackDummy()
        {
            var dummy = new CombatTargetSnapshot
            {
                Active = true,
                IsTargetDummy = true,
                Friendly = true,
                TownNpc = true
            };

            string reason;
            if (!CombatAimReleaseHoldService.IsTargetValidForReleaseHold(dummy, true, out reason) ||
                !string.Equals(reason, "targetDummyAllowed", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected target dummy to be valid when TrackDummy is enabled, got " + reason);
            }

            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(dummy, false, out reason) ||
                !string.Equals(reason, "targetDummyDisabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected target dummy to be invalid when TrackDummy is disabled, got " + reason);
            }

            var friendly = new CombatTargetSnapshot { Active = true, Life = 100, Friendly = true };
            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(friendly, true, out reason) ||
                !string.Equals(reason, "friendly", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected friendly NPC to remain invalid, got " + reason);
            }

            var townNpc = new CombatTargetSnapshot { Active = true, Life = 100, TownNpc = true };
            if (CombatAimReleaseHoldService.IsTargetValidForReleaseHold(townNpc, true, out reason) ||
                !string.Equals(reason, "townNpc", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected town NPC to remain invalid, got " + reason);
            }
        }


    }
}
