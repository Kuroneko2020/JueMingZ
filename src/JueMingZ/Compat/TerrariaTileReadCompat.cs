using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaTileReadCompat
    {
        public const int TileSize = 16;

        // Tile reads are fail-closed snapshots; callers should skip automation
        // when a tile cannot be proven instead of synthesizing active terrain.
        public static bool TryGetTile(int tileX, int tileY, out Tile tile)
        {
            return TerrariaMainCompat.TryGetTile(tileX, tileY, out tile);
        }

        public static Tile GetTileSafely(int tileX, int tileY)
        {
            return Framing.GetTileSafely(tileX, tileY);
        }

        public static bool IsActive(Tile tile)
        {
            return tile != null && tile.active();
        }

        public static bool IsActiveAndUnactuated(Tile tile)
        {
            return tile != null && tile.nactive();
        }

        public static int Type(Tile tile)
        {
            return tile == null ? -1 : tile.type;
        }

        public static int Wall(Tile tile)
        {
            return tile == null ? -1 : tile.wall;
        }

        public static int LiquidAmount(Tile tile)
        {
            return tile == null ? 0 : tile.liquid;
        }

        public static int LiquidType(Tile tile)
        {
            return tile == null ? -1 : tile.liquidType();
        }

        public static int FrameX(Tile tile)
        {
            return tile == null ? 0 : tile.frameX;
        }

        public static int FrameY(Tile tile)
        {
            return tile == null ? 0 : tile.frameY;
        }

        public static bool IsHalfBlock(Tile tile)
        {
            return tile != null && tile.halfBrick();
        }

        public static int Slope(Tile tile)
        {
            return tile == null ? 0 : tile.slope();
        }

        public static bool IsActuated(Tile tile)
        {
            return tile != null && tile.inActive();
        }

        public static bool HasWater(Tile tile)
        {
            return tile != null && tile.water();
        }

        public static bool HasLava(Tile tile)
        {
            return tile != null && tile.lava();
        }

        public static bool HasHoney(Tile tile)
        {
            return tile != null && tile.honey();
        }

        public static bool HasShimmer(Tile tile)
        {
            return tile != null && tile.shimmer();
        }

        public static bool TryReadTile(int tileX, int tileY, out bool active, out int tileType)
        {
            active = false;
            tileType = -1;

            Tile tile;
            if (!TryGetTile(tileX, tileY, out tile))
            {
                return false;
            }

            active = IsActive(tile);
            tileType = Type(tile);
            return true;
        }
    }
}
