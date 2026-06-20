using System;
using System.Collections.Generic;
using JueMingZ.Actions;

namespace JueMingZ.Automation.WorldAutomation
{
    // Mining models describe targets and overlays only; Tile mutation and pick use must stay in Actions/Compat.
    internal struct AutoMiningTilePoint
    {
        public int X;
        public int Y;

        public AutoMiningTilePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public sealed class AutoMiningTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int TileType { get; set; }

        public AutoMiningTile()
        {
            TileType = -1;
        }

        public AutoMiningTile(int x, int y)
            : this(x, y, -1)
        {
        }

        public AutoMiningTile(int x, int y, int tileType)
        {
            X = x;
            Y = y;
            TileType = tileType;
        }
    }

    internal sealed class AutoMiningGravityRelocation
    {
        public int SourceX { get; set; }
        public int SourceY { get; set; }
        public int TileType { get; set; }
        public long CreatedTick { get; set; }
    }

    internal enum AutoMiningTileMatchGroupKind
    {
        SingleTileType = 0,
        GemCluster = 1
    }

    internal struct AutoMiningTileMatchGroup
    {
        public int SeedTileType { get; private set; }
        public AutoMiningTileMatchGroupKind Kind { get; private set; }

        public static AutoMiningTileMatchGroup ForSeedTileType(int seedTileType)
        {
            return new AutoMiningTileMatchGroup
            {
                SeedTileType = seedTileType,
                Kind = IsGemClusterTileType(seedTileType)
                    ? AutoMiningTileMatchGroupKind.GemCluster
                    : AutoMiningTileMatchGroupKind.SingleTileType
            };
        }

        public bool Matches(int actualTileType)
        {
            if (actualTileType < 0)
            {
                return false;
            }

            if (Kind == AutoMiningTileMatchGroupKind.GemCluster)
            {
                return IsGemClusterTileType(actualTileType);
            }

            return actualTileType == SeedTileType;
        }

        public static bool IsGemClusterTileType(int tileType)
        {
            // GemCluster is the only mixed-type auto-mining vein; normal ores remain single-tile-type selections.
            return (tileType >= 63 && tileType <= 68) ||
                   tileType == 178 ||
                   tileType == 566;
        }
    }

    internal sealed class AutoMiningPickaxeProfile
    {
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public int Stack { get; set; }
        public int PickPower { get; set; }
        public int TileBoost { get; set; }
        public string ItemName { get; set; }

        public AutoMiningPickaxeProfile()
        {
            SelectedSlot = -1;
            ItemName = string.Empty;
        }

        public bool IsUsablePickaxe
        {
            get { return SelectedSlot >= 0 && ItemType > 0 && Stack > 0 && PickPower > 0; }
        }
    }

    internal sealed class AutoMiningVeinSelection
    {
        public int TileType { get; set; }
        public AutoMiningTileMatchGroup MatchGroup { get; set; }
        public int PickItemType { get; set; }
        public int PickSlot { get; set; }
        public int PickPower { get; set; }
        public int PickTileBoost { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public long CreatedTick { get; set; }
        public string SourceMode { get; set; }
        public string SourceHotkey { get; set; }
        public List<AutoMiningTile> Tiles { get; private set; }
        public List<AutoMiningGravityRelocation> PendingGravityRelocations { get; private set; }

        public AutoMiningVeinSelection()
        {
            SourceMode = string.Empty;
            SourceHotkey = string.Empty;
            Tiles = new List<AutoMiningTile>();
            PendingGravityRelocations = new List<AutoMiningGravityRelocation>();
            MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(-1);
        }

        public bool Matches(int actualTileType)
        {
            return MatchGroup.Matches(actualTileType);
        }
    }

    public sealed class AutoMiningOverlayTile
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int TileType { get; set; }
    }

    public sealed class AutoMiningOverlaySnapshot
    {
        public int TileType { get; set; }
        public int PickPower { get; set; }
        public int PickTileBoost { get; set; }
        public string Mode { get; set; }
        public IReadOnlyList<AutoMiningOverlayTile> Tiles { get; set; }

        public AutoMiningOverlaySnapshot()
        {
            Mode = string.Empty;
            Tiles = new List<AutoMiningOverlayTile>();
        }
    }

    public sealed class AutoMiningDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public string LastHotkey { get; set; }
        public string LastHotkeyResultCode { get; set; }
        public string LastHotkeyBlockedReason { get; set; }
        public DateTime? LastHotkeyDecisionUtc { get; set; }

        public AutoMiningDiagnostics()
        {
            LastDecision = string.Empty;
            LastHotkey = string.Empty;
            LastHotkeyResultCode = string.Empty;
            LastHotkeyBlockedReason = string.Empty;
        }
    }

    internal sealed class AutoMiningHotkeyInputResult
    {
        public bool PressedEdge { get; set; }
        public bool Accepted { get; set; }
        public bool Down { get; set; }
        public string Display { get; set; }
        public string Normalized { get; set; }
        public string Reason { get; set; }
        public DiagnosticResultCode DiagnosticResultCode { get; set; }

        public AutoMiningHotkeyInputResult()
        {
            Display = string.Empty;
            Normalized = string.Empty;
            Reason = string.Empty;
            DiagnosticResultCode = DiagnosticResultCode.NotApplicable;
        }
    }
}
