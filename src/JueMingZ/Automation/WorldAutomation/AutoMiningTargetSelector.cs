using System;
using System.Collections.Generic;
using JueMingZ.Compat;

namespace JueMingZ.Automation.WorldAutomation
{
    internal static class AutoMiningTargetSelector
    {
        public static Dictionary<long, AutoMiningTile> BuildActiveTileLookup(IList<AutoMiningTile> tiles, Func<AutoMiningTile, bool> isActive)
        {
            var activeTiles = new Dictionary<long, AutoMiningTile>();
            if (tiles == null)
            {
                return activeTiles;
            }

            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (tile == null ||
                    isActive == null ||
                    !isActive(tile))
                {
                    continue;
                }

                activeTiles[EncodeTileKey(tile.X, tile.Y)] = tile;
            }

            return activeTiles;
        }

        public static bool TryChooseNextTarget(
            IList<AutoMiningTile> tiles,
            Func<AutoMiningTile, bool> isActive,
            Func<AutoMiningTile, bool> isReachable,
            float playerCenterX,
            float playerCenterY,
            out AutoMiningTile target,
            out int remaining)
        {
            target = null;
            remaining = 0;

            var activeTiles = BuildActiveTileLookup(tiles, isActive);
            remaining = activeTiles.Count;
            if (remaining <= 0)
            {
                return false;
            }

            AutoMiningTile bestFrontier = null;
            var bestFrontierDistance = float.MaxValue;

            foreach (var tile in activeTiles.Values)
            {
                // Frontier is a cheap topology gate; keep it before reach checks so dense veins do not call expensive per-tile compat paths.
                if (!IsImmediateFrontierTile(activeTiles, tile))
                {
                    continue;
                }

                var distanceSquared = ComputeDistanceSquared(tile, playerCenterX, playerCenterY);
                if (isReachable != null && !isReachable(tile))
                {
                    continue;
                }

                if (ShouldReplace(tile, distanceSquared, bestFrontier, bestFrontierDistance))
                {
                    bestFrontier = tile;
                    bestFrontierDistance = distanceSquared;
                }
            }

            // Only frontier tiles are allowed to reach ItemCheck; enclosed fallback targets can look selected
            // but fail to damage the visible ore, which reads as an empty swing.
            target = bestFrontier;
            return target != null;
        }

        public static bool IsImmediateFrontierTile(IReadOnlyDictionary<long, AutoMiningTile> activeTiles, AutoMiningTile tile)
        {
            return tile != null && IsImmediateFrontierTile(activeTiles, tile.X, tile.Y);
        }

        public static bool IsImmediateFrontierTile(IReadOnlyDictionary<long, AutoMiningTile> activeTiles, int tileX, int tileY)
        {
            if (activeTiles == null)
            {
                return false;
            }

            for (var offsetY = -1; offsetY <= 1; offsetY++)
            {
                for (var offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    if (!activeTiles.ContainsKey(EncodeTileKey(tileX + offsetX, tileY + offsetY)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ShouldReplace(
            AutoMiningTile candidate,
            float candidateDistanceSquared,
            AutoMiningTile current,
            float currentDistanceSquared)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            if (candidateDistanceSquared < currentDistanceSquared - 0.01f)
            {
                return true;
            }

            if (candidateDistanceSquared > currentDistanceSquared + 0.01f)
            {
                return false;
            }

            return candidate.Y < current.Y ||
                   (candidate.Y == current.Y && candidate.X < current.X);
        }

        private static float ComputeDistanceSquared(AutoMiningTile tile, float playerCenterX, float playerCenterY)
        {
            var tileCenterX = tile.X * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            var tileCenterY = tile.Y * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            var deltaX = tileCenterX - playerCenterX;
            var deltaY = tileCenterY - playerCenterY;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static long EncodeTileKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }
    }
}
