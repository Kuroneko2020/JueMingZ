using System.Collections.Generic;

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

        public AutoMiningTile()
        {
        }

        public AutoMiningTile(int x, int y)
        {
            X = x;
            Y = y;
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

        public AutoMiningVeinSelection()
        {
            SourceMode = string.Empty;
            SourceHotkey = string.Empty;
            Tiles = new List<AutoMiningTile>();
        }
    }

    public sealed class AutoMiningOverlayTile
    {
        public int X { get; set; }
        public int Y { get; set; }
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
}
