namespace JueMingZ.GameState
{
    [System.Flags]
    public enum InventoryReadProfile
    {
        None = 0,
        SelectedItem = 1 << 0,
        InventorySlots = 1 << 1,
        TrashItem = 1 << 2,
        Favorited = 1 << 3,
        Prefix = 1 << 4,
        Name = 1 << 5,
        Stackability = 1 << 6,
        RecoveryFields = 1 << 7,
        BugNetFields = 1 << 8,
        ToolFields = 1 << 9,
        TileCreationFields = 1 << 10,
        EquipmentFields = 1 << 11,
        EquippedItems = 1 << 12,

        SelectedOnly = SelectedItem | Name,
        SignatureOnly = InventorySlots | Prefix | Name,
        RecoveryItems = InventorySlots | Name | RecoveryFields,
        CoinsOnly = InventorySlots | Favorited,
        ExtractinatorItems = InventorySlots | Name,
        ToolsAndSeeds = InventorySlots | Name | ToolFields,
        BugNetOnly = InventorySlots | SelectedItem | Name | BugNetFields,
        StackCandidates = InventorySlots | Favorited | Stackability | EquipmentFields,
        SellDiscardCandidates = InventorySlots | Favorited | Name,
        KeepFavorited = InventorySlots | EquippedItems | TrashItem | Favorited | Prefix | Name,
        Full = SelectedItem |
               InventorySlots |
               TrashItem |
               Favorited |
               Prefix |
               Name |
               Stackability |
               RecoveryFields |
               BugNetFields |
               ToolFields |
               TileCreationFields |
               EquipmentFields
    }

    [System.Flags]
    public enum NpcReadProfile
    {
        None = 0,
        Counts = 1 << 0,
        CatchableCritters = 1 << 1,
        Names = 1 << 2,
        Positions = 1 << 3,

        CountsOnly = Counts,
        CatchableCrittersOnly = Counts | CatchableCritters | Names | Positions,
        Full = Counts | CatchableCritters | Names | Positions
    }

    public enum TileReadProfile
    {
        None = 0,
        DiagnosticsOnly = 1,
        ScreenNearbyInformation = 2,
        ReachTargets = 3,
        Full = DiagnosticsOnly
    }

    public sealed class GameStateReadOptions
    {
        public static readonly GameStateReadOptions Full = new GameStateReadOptions
        {
            InventoryProfile = InventoryReadProfile.Full,
            IncludeActiveBuffs = true,
            NpcProfile = NpcReadProfile.Full,
            TileProfile = TileReadProfile.Full,
            IncludeWorldSummary = true
        };

        public static readonly GameStateReadOptions Minimal = new GameStateReadOptions
        {
            InventoryProfile = InventoryReadProfile.None,
            IncludeActiveBuffs = false,
            NpcProfile = NpcReadProfile.None,
            TileProfile = TileReadProfile.None,
            IncludeWorldSummary = false
        };

        public InventoryReadProfile InventoryProfile { get; set; }
        public bool IncludeActiveBuffs { get; set; }
        public NpcReadProfile NpcProfile { get; set; }
        public TileReadProfile TileProfile { get; set; }
        public bool IncludeWorldSummary { get; set; }

        public bool IncludeInventory
        {
            get { return HasInventory(InventoryReadProfile.InventorySlots); }
            set
            {
                if (value)
                {
                    InventoryProfile |= InventoryReadProfile.Full;
                    return;
                }

                InventoryProfile &= ~(InventoryReadProfile.InventorySlots | InventoryReadProfile.TrashItem);
            }
        }

        public bool IncludeSelectedInventoryItem
        {
            get { return HasInventory(InventoryReadProfile.SelectedItem); }
            set
            {
                if (value)
                {
                    InventoryProfile |= InventoryReadProfile.SelectedOnly;
                    return;
                }

                InventoryProfile &= ~InventoryReadProfile.SelectedItem;
            }
        }

        public bool IncludeNpcSummary
        {
            get { return NpcProfile != NpcReadProfile.None; }
            set
            {
                NpcProfile = value ? NpcReadProfile.CountsOnly : NpcReadProfile.None;
            }
        }

        public bool IncludeCatchableCritters
        {
            get { return HasNpc(NpcReadProfile.CatchableCritters); }
            set
            {
                if (value)
                {
                    NpcProfile |= NpcReadProfile.CatchableCrittersOnly;
                    return;
                }

                NpcProfile &= ~(NpcReadProfile.CatchableCritters | NpcReadProfile.Names | NpcReadProfile.Positions);
            }
        }

        public bool IncludeTileSummary
        {
            get { return TileProfile != TileReadProfile.None; }
            set { TileProfile = value ? TileReadProfile.DiagnosticsOnly : TileReadProfile.None; }
        }

        private bool HasInventory(InventoryReadProfile flag)
        {
            return (InventoryProfile & flag) == flag;
        }

        private bool HasNpc(NpcReadProfile flag)
        {
            return (NpcProfile & flag) == flag;
        }
    }
}
