using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void ChannelResolverUnknownActionDefaultsGlobalExclusive()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest
            {
                Kind = (InputActionKind)999,
                SourceFeatureId = "test.unknown"
            });

            AssertHas(profile.RequiredChannels, InputActionChannel.GlobalExclusive, "unknown required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "unknown conflicts");
        }

        private static void ChannelResolverDiagnosticNoopUsesNoChannel()
        {
            var profile = InputActionChannelResolver.Resolve(InputActionRequest.CreateDiagnosticNoop("test", "noop"));
            if (profile.RequiredChannels != InputActionChannel.None || profile.GlobalExclusive)
            {
                throw new InvalidOperationException("Expected DiagnosticNoop to require no channel.");
            }
        }

        private static void ChannelResolverUseHotbarItemUsesItemAndHotbar()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.UseHotbarItem });
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "use hotbar required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "use hotbar required");
            AssertHas(profile.RequiredChannels, InputActionChannel.BridgeItemUse, "use hotbar required");
        }

        private static void ChannelResolverInventorySlotConflictsWithUseItem()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.InventorySlot });
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "inventory required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "inventory conflicts");
        }

        private static void ChannelResolverSafeLandingQuickMount()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "quick_mount";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "quick mount required");
            AssertHas(profile.RequiredChannels, InputActionChannel.QuickMount, "quick mount required");
        }

        private static void ChannelResolverSafeLandingGravityFlip()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "gravity_flip";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "gravity flip required");
            AssertHas(profile.RequiredChannels, InputActionChannel.GravityFlip, "gravity flip required");
        }

        private static void ChannelResolverSafeLandingGrapple()
        {
            var request = new InputActionRequest { Kind = InputActionKind.Jump };
            request.Metadata["JumpMode"] = "SafeLandingTakeover";
            request.Metadata["SafeLandingActionType"] = "grapple";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.Jump, "grapple required");
            AssertHas(profile.RequiredChannels, InputActionChannel.Grapple, "grapple required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "grapple required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "grapple conflicts");
        }

        private static void ChannelResolverDashUsesDashAndDirection()
        {
            var profile = InputActionChannelResolver.Resolve(new InputActionRequest { Kind = InputActionKind.Dash });
            AssertHas(profile.RequiredChannels, InputActionChannel.Dash, "dash required");
            AssertHas(profile.RequiredChannels, InputActionChannel.Direction, "dash required");
        }

        private static void ChannelResolverMagicStringUsesPulseBridge()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata["Scenario"] = "Combat.MagicStringClicker";
            request.Metadata["RawInputMode"] = "MagicStringClicker";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "magic string required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "magic string required");
            AssertHas(profile.RequiredChannels, InputActionChannel.BridgeUseItemPulse, "magic string required");
        }

        private static void ChannelResolverAutoHarvestSustainedUse()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoHarvest;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoHarvestSustainedUse";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "auto harvest required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "auto harvest required");
        }

        private static void ChannelResolverAutoMiningSustainedUse()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoMining;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoMiningSustainedUse";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "auto mining required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "auto mining required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "auto mining required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "auto mining required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "auto mining required");
        }

        private static void ChannelResolverAutoCaptureCritterSustainedUse()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoCaptureCritter;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoCaptureCritterSustainedUse";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "auto capture required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "auto capture required");
        }

        private static void ChannelResolverPhasebladeQuickSwitch()
        {
            var request = new InputActionRequest { Kind = InputActionKind.RawInput };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.CombatPhasebladeQuickSwitch;
            request.Metadata[ActionMetadataKeys.RawInputMode] = "PhasebladeQuickSwitch";

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.UseItem, "phaseblade required");
            AssertHas(profile.RequiredChannels, InputActionChannel.HotbarSelection, "phaseblade required");
            AssertHas(profile.RequiredChannels, InputActionChannel.MouseTarget, "phaseblade required");
            AssertHas(profile.RequiredChannels, InputActionChannel.RawInput, "phaseblade required");
            if ((profile.RequiredChannels & InputActionChannel.InventorySlot) != 0)
            {
                throw new InvalidOperationException("Phaseblade quick switch must not reserve general inventory slots.");
            }
        }

        private static void ChannelResolverShopUsesNpcAndInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Shop,
                SourceFeatureId = FeatureIds.InventoryAutoSell
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoSell;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.NpcInteraction, "shop required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "shop required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "shop conflicts");
        }

        private static void ChannelResolverTrashSlotUsesInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.TrashSlot,
                SourceFeatureId = FeatureIds.InventoryAutoDiscard
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoDiscard;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "trash slot required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "trash slot conflicts");
        }

        private static void ChannelResolverReforgeUsesNpcAndInventory()
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Reforge,
                SourceFeatureId = FeatureIds.NpcAutoReforge
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.NpcQuickReforge;

            var profile = InputActionChannelResolver.Resolve(request);
            AssertHas(profile.RequiredChannels, InputActionChannel.NpcInteraction, "reforge required");
            AssertHas(profile.RequiredChannels, InputActionChannel.InventorySlot, "reforge required");
            AssertHas(profile.ConflictChannels, InputActionChannel.UseItem, "reforge conflicts");
        }


    }
}
