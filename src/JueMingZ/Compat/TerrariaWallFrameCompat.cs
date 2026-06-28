using System;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaWallFrameCompat
    {
        public static bool TryRefreshWallFrameNeighborhood(
            int tileX,
            int tileY,
            out int refreshedCoordinateCount,
            out string message)
        {
            refreshedCoordinateCount = 0;
            message = string.Empty;
            if (!TerrariaMainCompat.IsWorldReady)
            {
                message = "worldNotReady";
                return false;
            }

            if (!TerrariaMainCompat.IsTileCoordinateInWorld(tileX, tileY))
            {
                message = "targetOutOfWorld";
                return false;
            }

            try
            {
                // This refreshes vanilla wall frame data around an already placed
                // wall. It must never be used as a substitute for placing WallType.
                WorldGen.SquareWallFrame(tileX, tileY);
                refreshedCoordinateCount = 9;
                message = "refreshed";
                return true;
            }
            catch (Exception ex)
            {
                message = string.IsNullOrWhiteSpace(ex.Message)
                    ? ex.GetType().Name
                    : ex.GetType().Name + ":" + ex.Message;
                return false;
            }
        }
    }
}
