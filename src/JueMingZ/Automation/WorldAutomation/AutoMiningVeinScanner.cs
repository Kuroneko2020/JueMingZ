using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.WorldAutomation
{
    public delegate bool AutoMiningTileReader(int x, int y, out bool active, out int tileType);

    public sealed class AutoMiningVeinScanResult
    {
        public int TileType { get; set; }
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public List<AutoMiningTile> Tiles { get; private set; }

        public AutoMiningVeinScanResult()
        {
            Tiles = new List<AutoMiningTile>();
        }
    }

    public static class AutoMiningVeinScanner
    {
        public const int LinkedGapTiles = 3;
        public const int MaxVeinTiles = 512;

        public static AutoMiningVeinScanResult Scan(
            int seedX,
            int seedY,
            int tileType,
            int minX,
            int minY,
            int maxX,
            int maxY,
            AutoMiningTileReader readTile)
        {
            var result = new AutoMiningVeinScanResult
            {
                TileType = tileType,
                MinX = seedX,
                MinY = seedY,
                MaxX = seedX,
                MaxY = seedY
            };

            if (tileType < 0 || readTile == null || minX > maxX || minY > maxY)
            {
                return result;
            }

            var matchGroup = AutoMiningTileMatchGroup.ForSeedTileType(tileType);
            var queue = new Queue<AutoMiningTilePoint>();
            var queued = new HashSet<long>();
            EnqueueSeedCandidates(seedX, seedY, matchGroup, minX, minY, maxX, maxY, readTile, queue, queued);

            var accepted = new HashSet<long>();
            while (queue.Count > 0 && result.Tiles.Count < MaxVeinTiles)
            {
                var point = queue.Dequeue();
                if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
                {
                    continue;
                }

                var key = BuildPointKey(point.X, point.Y);
                if (accepted.Contains(key))
                {
                    continue;
                }

                int actualTileType;
                if (!TryReadMatchingActiveTile(readTile, point.X, point.Y, matchGroup, out actualTileType))
                {
                    continue;
                }

                accepted.Add(key);
                result.Tiles.Add(new AutoMiningTile(point.X, point.Y, actualTileType));
                result.MinX = Math.Min(result.MinX, point.X);
                result.MinY = Math.Min(result.MinY, point.Y);
                result.MaxX = Math.Max(result.MaxX, point.X);
                result.MaxY = Math.Max(result.MaxY, point.Y);

                for (var dx = -LinkedGapTiles; dx <= LinkedGapTiles; dx++)
                {
                    for (var dy = -LinkedGapTiles; dy <= LinkedGapTiles; dy++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        var nx = point.X + dx;
                        var ny = point.Y + dy;
                        if (nx < minX || nx > maxX || ny < minY || ny > maxY)
                        {
                            continue;
                        }

                        var nextKey = BuildPointKey(nx, ny);
                        if (accepted.Contains(nextKey) || queued.Contains(nextKey))
                        {
                            continue;
                        }

                        queued.Add(nextKey);
                        if (TryReadMatchingActiveTile(readTile, nx, ny, matchGroup, out actualTileType))
                        {
                            queue.Enqueue(new AutoMiningTilePoint(nx, ny));
                        }
                    }
                }
            }

            return result;
        }

        private static void EnqueueSeedCandidates(
            int seedX,
            int seedY,
            AutoMiningTileMatchGroup matchGroup,
            int minX,
            int minY,
            int maxX,
            int maxY,
            AutoMiningTileReader readTile,
            Queue<AutoMiningTilePoint> queue,
            ISet<long> queued)
        {
            for (var dx = -LinkedGapTiles; dx <= LinkedGapTiles; dx++)
            {
                for (var dy = -LinkedGapTiles; dy <= LinkedGapTiles; dy++)
                {
                    var x = seedX + dx;
                    var y = seedY + dy;
                    if (x < minX || x > maxX || y < minY || y > maxY)
                    {
                        continue;
                    }

                    int actualTileType;
                    if (!TryReadMatchingActiveTile(readTile, x, y, matchGroup, out actualTileType))
                    {
                        continue;
                    }

                    var key = BuildPointKey(x, y);
                    if (queued.Contains(key))
                    {
                        continue;
                    }

                    queued.Add(key);
                    queue.Enqueue(new AutoMiningTilePoint(x, y));
                }
            }
        }

        private static bool TryReadMatchingActiveTile(
            AutoMiningTileReader readTile,
            int x,
            int y,
            AutoMiningTileMatchGroup matchGroup,
            out int tileType)
        {
            tileType = -1;
            bool active;
            return readTile != null &&
                   readTile(x, y, out active, out tileType) &&
                   active &&
                   matchGroup.Matches(tileType);
        }

        private static long BuildPointKey(int x, int y)
        {
            unchecked
            {
                return ((long)(x & 0x7fffffff) << 32) | (uint)y;
            }
        }
    }
}
