using System;
using JueMingZ.Common;

namespace JueMingZ.Actions.Channels
{
    public static class InputActionChannelResolver
    {
        public static InputActionChannelProfile Resolve(InputActionRequest request)
        {
            var profile = new InputActionChannelProfile();
            if (request == null)
            {
                profile.RequiredChannels = InputActionChannel.GlobalExclusive;
                profile.ConflictChannels = InputActionChannelFormatter.AllKnown;
                profile.GlobalExclusive = true;
                profile.Reason = "nullRequest";
                return profile;
            }

            profile.SourceFeatureId = request.SourceFeatureId ?? string.Empty;
            profile.Scenario = GetMetadata(request, ActionMetadataKeys.Scenario);

            var required = request.RequiredChannels == InputActionChannel.None
                ? ResolveDefaultRequiredChannels(request)
                : request.RequiredChannels;
            var globalExclusive = (required & InputActionChannel.GlobalExclusive) != 0;
            profile.RequiredChannels = required;
            profile.GlobalExclusive = globalExclusive;
            profile.ConflictChannels = request.ConflictChannels == InputActionChannel.None
                ? ResolveDefaultConflictChannels(required)
                : request.ConflictChannels;
            profile.Reason = ResolveReason(request, required);
            return profile;
        }

        public static InputActionChannel ResolveDefaultRequiredChannels(InputActionRequest request)
        {
            if (request == null)
            {
                return InputActionChannel.GlobalExclusive;
            }

            switch (request.Kind)
            {
                case InputActionKind.DiagnosticNoop:
                    return InputActionChannel.None;

                case InputActionKind.SelectHotbarSlot:
                    return InputActionChannel.HotbarSelection;

                case InputActionKind.ItemUse:
                    return ResolveItemUseChannels(request);

                case InputActionKind.UseHotbarItem:
                    return (InputActionChannel.UseItem |
                            InputActionChannel.HotbarSelection |
                            InputActionChannel.BridgeItemUse) |
                           (HasMouseTarget(request) ? InputActionChannel.MouseTarget : InputActionChannel.None);

                case InputActionKind.UseInventoryItem:
                    return InputActionChannel.UseItem |
                           InputActionChannel.InventorySlot |
                           InputActionChannel.BridgeItemUse;

                case InputActionKind.QuickHeal:
                case InputActionKind.QuickMana:
                case InputActionKind.QuickBuff:
                    return InputActionChannel.QuickAction;

                case InputActionKind.BuffPotionDirectUse:
                    return InputActionChannel.BuffMutation | InputActionChannel.InventorySlot;

                case InputActionKind.TileInteract:
                    return InputActionChannel.MouseTarget | InputActionChannel.UseTile;

                case InputActionKind.NpcInteract:
                    return InputActionChannel.NpcInteraction;

                case InputActionKind.InventorySlot:
                    return ResolveInventorySlotChannels(request);

                case InputActionKind.Chest:
                    return InputActionChannel.ChestInteraction | InputActionChannel.InventorySlot;

                case InputActionKind.Shop:
                    return InputActionChannel.NpcInteraction | InputActionChannel.InventorySlot;

                case InputActionKind.TrashSlot:
                    return InputActionChannel.InventorySlot;

                case InputActionKind.Reforge:
                    return InputActionChannel.NpcInteraction | InputActionChannel.InventorySlot;

                case InputActionKind.MouseTarget:
                case InputActionKind.MouseTargetDryRun:
                    return InputActionChannel.MouseTarget;

                case InputActionKind.Jump:
                    return ResolveJumpChannels(request);

                case InputActionKind.Dash:
                    return InputActionChannel.Dash | InputActionChannel.Direction;

                case InputActionKind.RawInput:
                    return ResolveRawInputChannels(request);

                case InputActionKind.Aim:
                    return InputActionChannel.MouseTarget;

                case InputActionKind.TeleportCorrection:
                    return InputActionChannel.MouseTarget | InputActionChannel.UseItem;

                case InputActionKind.PlayerRename:
                    return InputActionChannel.GlobalExclusive;

                case InputActionKind.None:
                case InputActionKind.Movement:
                default:
                    return InputActionChannel.GlobalExclusive;
            }
        }

        public static InputActionChannel ResolveDefaultConflictChannels(InputActionChannel required)
        {
            if (required == InputActionChannel.None)
            {
                return InputActionChannel.None;
            }

            if ((required & InputActionChannel.GlobalExclusive) != 0)
            {
                return InputActionChannelFormatter.AllKnown;
            }

            var conflicts = required;
            if ((required & InputActionChannel.InventorySlot) != 0)
            {
                conflicts |= InputActionChannel.UseItem |
                             InputActionChannel.HotbarSelection |
                             InputActionChannel.ChestInteraction |
                             InputActionChannel.BuffMutation;
            }

            if ((required & InputActionChannel.HotbarSelection) != 0)
            {
                conflicts |= InputActionChannel.UseItem | InputActionChannel.InventorySlot;
            }

            if ((required & InputActionChannel.UseItem) != 0)
            {
                conflicts |= InputActionChannel.BridgeItemUse |
                             InputActionChannel.BridgeUseItemPulse |
                             InputActionChannel.InventorySlot |
                             InputActionChannel.HotbarSelection |
                             InputActionChannel.UseTile |
                             InputActionChannel.NpcInteraction |
                             InputActionChannel.ChestInteraction;
            }

            if ((required & InputActionChannel.BridgeItemUse) != 0 ||
                (required & InputActionChannel.BridgeUseItemPulse) != 0)
            {
                conflicts |= InputActionChannel.UseItem |
                             InputActionChannel.BridgeItemUse |
                             InputActionChannel.BridgeUseItemPulse;
            }

            if ((required & InputActionChannel.MouseTarget) != 0)
            {
                conflicts |= InputActionChannel.UseTile;
            }

            if ((required & InputActionChannel.UseTile) != 0)
            {
                conflicts |= InputActionChannel.MouseTarget | InputActionChannel.UseItem;
            }

            if ((required & InputActionChannel.Direction) != 0)
            {
                conflicts |= InputActionChannel.Dash | InputActionChannel.Direction;
            }

            if ((required & InputActionChannel.Dash) != 0)
            {
                conflicts |= InputActionChannel.Direction | InputActionChannel.Dash;
            }

            if ((required & InputActionChannel.Jump) != 0)
            {
                conflicts |= InputActionChannel.QuickMount | InputActionChannel.GravityFlip | InputActionChannel.Grapple;
            }

            if ((required & InputActionChannel.QuickMount) != 0 ||
                (required & InputActionChannel.GravityFlip) != 0)
            {
                conflicts |= InputActionChannel.Jump |
                             InputActionChannel.QuickMount |
                             InputActionChannel.GravityFlip |
                             InputActionChannel.Grapple;
            }

            if ((required & InputActionChannel.Grapple) != 0)
            {
                conflicts |= InputActionChannel.Jump |
                             InputActionChannel.QuickMount |
                             InputActionChannel.GravityFlip |
                             InputActionChannel.Grapple |
                             InputActionChannel.MouseTarget |
                             InputActionChannel.UseItem |
                             InputActionChannel.UseTile;
            }

            if ((required & InputActionChannel.NpcInteraction) != 0)
            {
                conflicts |= InputActionChannel.UseItem | InputActionChannel.InventorySlot;
            }

            if ((required & InputActionChannel.ChestInteraction) != 0)
            {
                conflicts |= InputActionChannel.InventorySlot | InputActionChannel.UseItem;
            }

            if ((required & InputActionChannel.BuffMutation) != 0)
            {
                conflicts |= InputActionChannel.InventorySlot;
            }

            if ((required & InputActionChannel.QuickAction) != 0)
            {
                conflicts |= InputActionChannel.UseItem |
                             InputActionChannel.BridgeItemUse |
                             InputActionChannel.BridgeUseItemPulse |
                             InputActionChannel.InventorySlot;
            }

            return conflicts;
        }

        private static InputActionChannel ResolveItemUseChannels(InputActionRequest request)
        {
            var channels = InputActionChannel.UseItem | InputActionChannel.BridgeItemUse;
            if (HasMouseTarget(request) || IsTrue(GetMetadata(request, "AllowCombatAim")))
            {
                channels |= InputActionChannel.MouseTarget;
            }

            return channels;
        }

        private static InputActionChannel ResolveInventorySlotChannels(InputActionRequest request)
        {
            var channels = InputActionChannel.InventorySlot;
            var targetKind = GetMetadata(request, "SafeLandingEquipmentTargetKind");
            if (string.Equals(targetKind, "Hotbar", StringComparison.OrdinalIgnoreCase))
            {
                channels |= InputActionChannel.HotbarSelection;
            }

            return channels;
        }

        private static InputActionChannel ResolveJumpChannels(InputActionRequest request)
        {
            var channels = InputActionChannel.Jump;
            var mode = GetMetadata(request, "JumpMode");
            if (!string.Equals(mode, "SafeLandingTakeover", StringComparison.OrdinalIgnoreCase))
            {
                return channels;
            }

            var actionType = GetMetadata(request, "SafeLandingActionType");
            if (string.Equals(actionType, "quick_mount", StringComparison.OrdinalIgnoreCase))
            {
                return channels | InputActionChannel.QuickMount;
            }

            if (string.Equals(actionType, "gravity_flip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionType, "gravityFlip", StringComparison.OrdinalIgnoreCase))
            {
                return channels | InputActionChannel.GravityFlip;
            }

            if (string.Equals(actionType, "grapple", StringComparison.OrdinalIgnoreCase))
            {
                return channels | InputActionChannel.Grapple | InputActionChannel.MouseTarget;
            }

            return channels;
        }

        private static InputActionChannel ResolveRawInputChannels(InputActionRequest request)
        {
            var scenario = GetMetadata(request, ActionMetadataKeys.Scenario);
            var mode = GetMetadata(request, ActionMetadataKeys.RawInputMode);
            if (string.Equals(mode, "MagicStringClicker", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, ScenarioNames.CombatMagicStringClicker, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionChannel.UseItem |
                       InputActionChannel.RawInput |
                       InputActionChannel.BridgeUseItemPulse;
            }

            if (string.Equals(mode, "AutoFacing", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, ScenarioNames.CombatAutoFacing, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionChannel.Direction | InputActionChannel.RawInput;
            }

            if (string.Equals(mode, "AutoHarvestSustainedUse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, ScenarioNames.WorldAutomationAutoHarvest, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionChannel.UseItem |
                       InputActionChannel.MouseTarget |
                       InputActionChannel.InventorySlot |
                       InputActionChannel.HotbarSelection |
                       InputActionChannel.RawInput;
            }

            if (string.Equals(mode, "AutoCaptureCritterSustainedUse", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(scenario, ScenarioNames.WorldAutomationAutoCaptureCritter, StringComparison.OrdinalIgnoreCase))
            {
                return InputActionChannel.UseItem |
                       InputActionChannel.MouseTarget |
                       InputActionChannel.InventorySlot |
                       InputActionChannel.HotbarSelection |
                       InputActionChannel.RawInput;
            }

            return InputActionChannel.GlobalExclusive | InputActionChannel.RawInput;
        }

        private static string ResolveReason(InputActionRequest request, InputActionChannel required)
        {
            return request.Kind + ":" + InputActionChannelFormatter.Format(required);
        }

        private static bool HasMouseTarget(InputActionRequest request)
        {
            return HasMetadata(request, ActionMetadataKeys.WorldX) ||
                   HasMetadata(request, ActionMetadataKeys.WorldY) ||
                   HasMetadata(request, ActionMetadataKeys.ScreenX) ||
                   HasMetadata(request, ActionMetadataKeys.ScreenY);
        }

        private static bool HasMetadata(InputActionRequest request, string key)
        {
            if (request == null || request.Metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            string value;
            return request.Metadata.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value);
        }

        private static string GetMetadata(InputActionRequest request, string key)
        {
            if (request == null || request.Metadata == null || string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
