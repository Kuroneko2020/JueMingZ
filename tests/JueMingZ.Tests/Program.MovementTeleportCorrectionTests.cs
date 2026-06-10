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
        private static void MovementTeleportCorrectionRequiresVanillaUseFrame()
        {
            var player = new FakePlayer();
            string reason;
            if (MovementTeleportCorrectionService.IsVanillaTeleportRodUseFrameForTesting(player, out reason) ||
                !string.Equals(reason, "notUseFrame:itemAnimation", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected teleport correction to skip when itemAnimation is zero, got " + reason + ".");
            }

            player.itemAnimation = 8;
            player.itemTime = 3;
            if (MovementTeleportCorrectionService.IsVanillaTeleportRodUseFrameForTesting(player, out reason) ||
                !string.Equals(reason, "notUseFrame:itemTime", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected teleport correction to skip while itemTime is still active, got " + reason + ".");
            }

            player.itemTime = 0;
            if (!MovementTeleportCorrectionService.IsVanillaTeleportRodUseFrameForTesting(player, out reason))
            {
                throw new InvalidOperationException("Expected teleport correction to allow the original use frame, got " + reason + ".");
            }
        }


    }
}
