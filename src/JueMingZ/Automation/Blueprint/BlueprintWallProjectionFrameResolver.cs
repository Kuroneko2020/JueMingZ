using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintWallProjectionFrameResolver
    {
        private const int FrameSize = 36;
        private const int Up = 1;
        private const int Left = 2;
        private const int Right = 4;
        private const int Down = 8;

        private static readonly int[,] CenterWallFrameLookup =
        {
            { 2, 0, 0 },
            { 0, 1, 4 },
            { 0, 3, 0 }
        };

        private static readonly int[,] WallFrameLookupX =
        {
            { 9, 10, 11, 6 },
            { 6, 7, 8, 4 },
            { 12, 12, 12, 12 },
            { 1, 3, 5, 3 },
            { 9, 9, 9, 9 },
            { 0, 2, 4, 2 },
            { 6, 7, 8, 5 },
            { 1, 2, 3, 3 },
            { 6, 7, 8, 6 },
            { 5, 5, 5, 5 },
            { 1, 3, 5, 1 },
            { 4, 4, 4, 4 },
            { 0, 2, 4, 0 },
            { 0, 0, 0, 0 },
            { 1, 2, 3, 1 },
            { 1, 2, 3, 2 },
            { 6, 7, 8, 7 },
            { 6, 7, 8, 8 },
            { 10, 10, 10, 10 },
            { 11, 11, 11, 11 }
        };

        private static readonly int[,] WallFrameLookupY =
        {
            { 3, 3, 3, 6 },
            { 3, 3, 3, 6 },
            { 0, 1, 2, 5 },
            { 4, 4, 4, 6 },
            { 0, 1, 2, 5 },
            { 4, 4, 4, 6 },
            { 4, 4, 4, 6 },
            { 2, 2, 2, 5 },
            { 0, 0, 0, 5 },
            { 0, 1, 2, 5 },
            { 3, 3, 3, 6 },
            { 0, 1, 2, 5 },
            { 3, 3, 3, 6 },
            { 0, 1, 2, 5 },
            { 0, 0, 0, 5 },
            { 1, 1, 1, 5 },
            { 1, 1, 1, 5 },
            { 2, 2, 2, 5 },
            { 0, 1, 2, 5 },
            { 0, 1, 2, 5 }
        };

        public static void Apply(IList<BlueprintProjectionCellSnapshot> layers)
        {
            if (layers == null || layers.Count <= 0)
            {
                return;
            }

            var wallsByCoordinate = new Dictionary<string, BlueprintProjectionCellSnapshot>(StringComparer.Ordinal);
            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (IsWall(layer))
                {
                    wallsByCoordinate[BuildCoordinateKey(layer.WorldTileX, layer.WorldTileY)] = layer;
                }
            }

            if (wallsByCoordinate.Count <= 0)
            {
                return;
            }

            for (var index = 0; index < layers.Count; index++)
            {
                var layer = layers[index];
                if (!IsWall(layer))
                {
                    continue;
                }

                int frameX;
                int frameY;
                ResolveFrame(
                    layer.WorldTileX,
                    layer.WorldTileY,
                    HasWallAt(wallsByCoordinate, layer.WorldTileX, layer.WorldTileY - 1),
                    HasWallAt(wallsByCoordinate, layer.WorldTileX - 1, layer.WorldTileY),
                    HasWallAt(wallsByCoordinate, layer.WorldTileX + 1, layer.WorldTileY),
                    HasWallAt(wallsByCoordinate, layer.WorldTileX, layer.WorldTileY + 1),
                    out frameX,
                    out frameY);
                layer.FrameX = frameX;
                layer.FrameY = frameY;
            }
        }

        internal static int[] ResolveFrameForTesting(int worldTileX, int worldTileY, bool up, bool left, bool right, bool down)
        {
            int frameX;
            int frameY;
            ResolveFrame(worldTileX, worldTileY, up, left, right, down, out frameX, out frameY);
            return new[] { frameX, frameY };
        }

        private static void ResolveFrame(
            int worldTileX,
            int worldTileY,
            bool up,
            bool left,
            bool right,
            bool down,
            out int frameX,
            out int frameY)
        {
            var lookup = 0;
            if (up) lookup |= Up;
            if (left) lookup |= Left;
            if (right) lookup |= Right;
            if (down) lookup |= Down;

            if (lookup == 15)
            {
                lookup += ResolveCenterOffset(worldTileX, worldTileY);
            }

            lookup = Math.Max(0, Math.Min(WallFrameLookupX.GetLength(0) - 1, lookup));
            var variant = ResolveFrameVariant(worldTileX, worldTileY);
            frameX = WallFrameLookupX[lookup, variant] * FrameSize;
            frameY = WallFrameLookupY[lookup, variant] * FrameSize;
        }

        private static int ResolveCenterOffset(int worldTileX, int worldTileY)
        {
            return CenterWallFrameLookup[PositiveModulo(worldTileX, 3), PositiveModulo(worldTileY, 3)];
        }

        private static int ResolveFrameVariant(int worldTileX, int worldTileY)
        {
            unchecked
            {
                var hash = worldTileX * 397 ^ worldTileY * 263;
                return PositiveModulo(hash, 3);
            }
        }

        private static bool HasWallAt(Dictionary<string, BlueprintProjectionCellSnapshot> wallsByCoordinate, int worldTileX, int worldTileY)
        {
            return wallsByCoordinate != null && wallsByCoordinate.ContainsKey(BuildCoordinateKey(worldTileX, worldTileY));
        }

        private static bool IsWall(BlueprintProjectionCellSnapshot layer)
        {
            return layer != null &&
                   string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase) &&
                   layer.ContentId > 0;
        }

        private static string BuildCoordinateKey(int worldTileX, int worldTileY)
        {
            return worldTileX.ToString(CultureInfo.InvariantCulture) + ":" +
                   worldTileY.ToString(CultureInfo.InvariantCulture);
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 0)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }
    }
}
