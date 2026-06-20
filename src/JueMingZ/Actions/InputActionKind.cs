namespace JueMingZ.Actions
{
    public enum InputActionKind
    {
        // New kinds need an executor and an explicit channel resolver branch.
        // Falling through is intentionally treated as unknown/high-risk work.
        None,
        DiagnosticNoop,
        ItemUse,
        UseHotbarItem,
        UseInventoryItem,
        QuickHeal,
        QuickMana,
        QuickBuff,
        BuffPotionDirectUse,
        TileInteract,
        NpcInteract,
        InventorySlot,
        SelectHotbarSlot,
        Chest,
        Shop,
        Reforge,
        TrashSlot,
        Aim,
        MouseTarget,
        MouseTargetDryRun,
        Movement,
        Jump,
        Dash,
        TeleportCorrection,
        RawInput,
        PlayerRename,
        BlueprintAutoPlace
    }
}
