using System;

namespace JueMingZ.Actions.Channels
{
    [Flags]
    public enum InputActionChannel
    {
        None = 0,
        GlobalExclusive = 1 << 0,
        MouseTarget = 1 << 1,
        UseItem = 1 << 2,
        UseTile = 1 << 3,
        QuickAction = 1 << 4,
        InventorySlot = 1 << 5,
        HotbarSelection = 1 << 6,
        ChestInteraction = 1 << 7,
        NpcInteraction = 1 << 8,
        BuffMutation = 1 << 9,
        Jump = 1 << 10,
        Dash = 1 << 11,
        Direction = 1 << 12,
        QuickMount = 1 << 13,
        GravityFlip = 1 << 14,
        RawInput = 1 << 15,
        BridgeItemUse = 1 << 16,
        BridgeUseItemPulse = 1 << 17,
        Grapple = 1 << 18
    }
}
