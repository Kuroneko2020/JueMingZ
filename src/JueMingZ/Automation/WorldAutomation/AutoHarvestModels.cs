using System;
using JueMingZ.Compat;

namespace JueMingZ.Automation.WorldAutomation
{
    // Harvest models carry tool, target, and replant evidence; tile and item writes remain queued action work.
    internal sealed class AutoHarvestToolCandidate
    {
        public int Slot;
        public int ItemType;
        public string ItemName;
        public int Stack;
        public int TileBoost;
        public bool IsSelected;

        public AutoHarvestToolCandidate()
        {
            ItemName = string.Empty;
        }
    }

    internal sealed class AutoHarvestHerbTarget
    {
        public int TileX;
        public int TileY;
        public int TileType;
        public int HerbStyle;
        public int SeedItemType;
        public int SupportTileType;
    }

    internal sealed class AutoHarvestPlantSpot
    {
        public bool TileAvailable;
        public bool Active;
        public int TileType;
        public int HerbStyle;
        public bool Supported;
        public int SupportTileType;

        public bool HasSameHerb(int herbStyle)
        {
            return Active &&
                   (TileType == AutoHarvestCompat.ImmatureHerbsTileType ||
                    TileType == AutoHarvestCompat.MatureHerbsTileType ||
                    TileType == AutoHarvestCompat.BloomingHerbsTileType) &&
                   HerbStyle == herbStyle;
        }
    }

    internal sealed class AutoHarvestPendingReplant
    {
        public int TileX;
        public int TileY;
        public int HerbStyle;
        public int SeedItemType;
        public int SupportTileType;
        public long CreatedTick;
        public long ExpiresTick;

        public bool Matches(AutoHarvestHerbTarget target)
        {
            return target != null &&
                   TileX == target.TileX &&
                   TileY == target.TileY;
        }
    }

    public sealed class AutoHarvestDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public string LastAction { get; set; }
        public int ToolSlot { get; set; }
        public int ToolItemType { get; set; }
        public int TargetTileX { get; set; }
        public int TargetTileY { get; set; }
        public int TargetSeedItemType { get; set; }
        public int PendingReplantCount { get; set; }

        public AutoHarvestDiagnostics()
        {
            LastDecision = string.Empty;
            LastAction = string.Empty;
            ToolSlot = -1;
            TargetTileX = -1;
            TargetTileY = -1;
        }
    }
}
