using System;
using System.Collections.Generic;
using System.Text;

namespace JueMingZ.Actions.Channels
{
    public static class InputActionChannelFormatter
    {
        public static readonly InputActionChannel AllKnown =
            InputActionChannel.GlobalExclusive |
            InputActionChannel.MouseTarget |
            InputActionChannel.UseItem |
            InputActionChannel.UseTile |
            InputActionChannel.QuickAction |
            InputActionChannel.InventorySlot |
            InputActionChannel.HotbarSelection |
            InputActionChannel.ChestInteraction |
            InputActionChannel.NpcInteraction |
            InputActionChannel.BuffMutation |
            InputActionChannel.Jump |
            InputActionChannel.Dash |
            InputActionChannel.Direction |
            InputActionChannel.QuickMount |
            InputActionChannel.GravityFlip |
            InputActionChannel.RawInput |
            InputActionChannel.BridgeItemUse |
            InputActionChannel.BridgeUseItemPulse |
            InputActionChannel.Grapple;

        private static readonly InputActionChannel[] OrderedChannels =
        {
            InputActionChannel.GlobalExclusive,
            InputActionChannel.MouseTarget,
            InputActionChannel.UseItem,
            InputActionChannel.UseTile,
            InputActionChannel.QuickAction,
            InputActionChannel.InventorySlot,
            InputActionChannel.HotbarSelection,
            InputActionChannel.ChestInteraction,
            InputActionChannel.NpcInteraction,
            InputActionChannel.BuffMutation,
            InputActionChannel.Jump,
            InputActionChannel.Dash,
            InputActionChannel.Direction,
            InputActionChannel.QuickMount,
            InputActionChannel.GravityFlip,
            InputActionChannel.RawInput,
            InputActionChannel.BridgeItemUse,
            InputActionChannel.BridgeUseItemPulse,
            InputActionChannel.Grapple
        };

        public static string Format(InputActionChannel channels)
        {
            if (channels == InputActionChannel.None)
            {
                return "None";
            }

            var builder = new StringBuilder();
            for (var index = 0; index < OrderedChannels.Length; index++)
            {
                var channel = OrderedChannels[index];
                if ((channels & channel) == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("|");
                }

                builder.Append(channel);
            }

            var unknown = channels & ~AllKnown;
            if (unknown != InputActionChannel.None)
            {
                if (builder.Length > 0)
                {
                    builder.Append("|");
                }

                builder.Append("Unknown(").Append(((int)unknown).ToString()).Append(")");
            }

            return builder.Length == 0 ? channels.ToString() : builder.ToString();
        }

        internal static IEnumerable<InputActionChannel> Enumerate(InputActionChannel channels)
        {
            for (var index = 0; index < OrderedChannels.Length; index++)
            {
                var channel = OrderedChannels[index];
                if ((channels & channel) != 0)
                {
                    yield return channel;
                }
            }
        }
    }
}
