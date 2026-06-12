namespace JueMingZ.Common
{
    // Feature ids are config keys and diagnostic identifiers; renames require explicit migration aliases.
    public static class FeatureIds
    {
        public const string CombatAutoAim = "combat.auto_aim";
        public const string CombatAutoClicker = "combat.auto_clicker";
        public const string CombatFlailCombo = "combat.flail_combo";
        public const string CombatPhasebladeQuickSwitch = "combat.phaseblade_quick_switch";
        public const string CombatPerfectRevolver = "combat.perfect_revolver";
        public const string CombatMagicStringClicker = "combat.magic_string_clicker";
        public const string CombatAutoFacing = "combat.auto_facing";
        public const string CombatEquipmentWarning = "combat.equipment_warning";
        public const string CombatGoblinExecution = "combat.goblin_execution";
        public const string DiagnosticsWorldGenDebugViewer = "diagnostics.worldgen_debug_viewer";
        public const string DiagnosticsDeveloperDebugCommands = "diagnostics.developer_debug_commands";
        public const string InventoryQuickItemHotkeys = "inventory.quick_item_hotkeys";
        public const string InventoryAutoStack = "inventory.auto_stack";
        public const string InventoryAutoSell = "inventory.auto_sell";
        public const string InventoryAutoDiscard = "inventory.auto_discard";
        public const string InventoryQuickBagOpen = "inventory.continuous_bag_open";
        public const string InventoryAutoDepositCoins = "inventory.auto_deposit_coins";
        public const string InventoryAutoExtractinator = "inventory.auto_extractinator";
        public const string InventoryKeepFavorited = "inventory.keep_favorited";
        public const string NpcAutoReforge = "npc.auto_reforge";
        public const string NpcAutoTaxCollect = "npc.auto_tax_collect";
        public const string WorldAutomationAutoMining = "automation.auto_mining";
        public const string WorldAutomationAutoCaptureCritter = "automation.auto_capture_critter";
        public const string WorldAutomationAutoHarvest = "automation.auto_harvest";
        public const string WorldAutomationTravelMenu = "misc.travel_menu";
        [System.Obsolete("Use WorldAutomationTravelMenu. The feature id remains misc.travel_menu for compatibility.")]
        public const string MiscTravelMenu = WorldAutomationTravelMenu;
        public const string FishingAutoFish = "fishing.auto_fish";
        public const string FishingAutoLoadout = "fishing.auto_loadout";
        public const string FishingAutoEquipment = "fishing.auto_equipment";
        public const string FishingAutoStoreQuestFish = "fishing.auto_store_quest_fish";
        public const string FishingFilter = "fishing.filter";
        public const string FishingQuickRename = "fishing.quick_rename";
        public const string InformationHighlightDigtoise = "information.highlight_digtoise";
        public const string MapQuickAnnouncement = "map.quick_announcement";
        public const string SearchMain = "search.main";
        public const string SearchChestItemLocator = "search.chest_item_locator";
        public const string MovementSimulatedMultiJump = "movement.simulated_multi_jump";
        public const string MovementContinuousDash = "movement.continuous_dash";
        public const string MovementTeleportCorrection = "movement.teleport_correction";
        public const string MovementSafeLanding = "movement.fall_protection";
    }
}
