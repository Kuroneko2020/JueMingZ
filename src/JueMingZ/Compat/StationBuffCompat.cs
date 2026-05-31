using System;
using System.Collections.Generic;

namespace JueMingZ.Compat
{
    public sealed class StationBuffTarget
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileType { get; set; }
        public int BuffType { get; set; }
        public int OriginX { get; set; }
        public int OriginY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public float DistanceSquared { get; set; }
        public string Name { get; set; }

        public StationBuffTarget()
        {
            Name = string.Empty;
        }
    }

    public static class StationBuffCompat
    {
        public static bool TryFindMissingStationBuff(object player, out StationBuffTarget target, out string message)
        {
            target = null;
            List<StationBuffTarget> targets;
            if (!TryFindMissingStationBuffs(player, out targets, out message))
            {
                return false;
            }

            target = targets.Count > 0 ? targets[0] : null;
            if (target == null)
            {
                message = "No reachable missing station buff furniture found.";
                return false;
            }

            return true;
        }

        public static bool TryFindMissingStationBuffs(object player, out List<StationBuffTarget> targets, out string message)
        {
            targets = new List<StationBuffTarget>();
            message = string.Empty;
            if (player == null)
            {
                message = "Local player unavailable.";
                return false;
            }

            int left;
            int top;
            int right;
            int bottom;
            var usingFallbackReach = false;
            if (!NurseServiceCompat.TryGetTileReachRegion(player, out left, out top, out right, out bottom))
            {
                BuildFallbackTileReachRegion(player, out left, out top, out right, out bottom);
                usingFallbackReach = true;
            }

            var tiles = GetStatic(TerrariaRuntimeTypes.MainType, "tile") as Array;
            if (tiles == null)
            {
                message = "Main.tile is unavailable.";
                return false;
            }

            var maxX = tiles.GetLength(0) - 1;
            var maxY = tiles.GetLength(1) - 1;
            left = Clamp(left, 0, maxX);
            right = Clamp(right, 0, maxX);
            top = Clamp(top, 0, maxY);
            bottom = Clamp(bottom, 0, maxY);

            var playerCenterX = ReadCenterX(player);
            var playerCenterY = ReadCenterY(player);
            var bestByBuff = new Dictionary<int, StationBuffTarget>();
            var bestDistanceByBuff = new Dictionary<int, float>();
            for (var x = left; x <= right; x++)
            {
                for (var y = top; y <= bottom; y++)
                {
                    var tile = tiles.GetValue(x, y);
                    if (tile == null || !IsTileActive(tile))
                    {
                        continue;
                    }

                    var tileType = ReadTileType(tile);
                    int buffType;
                    string name;
                    if (!TryMapStation(tileType, out buffType, out name) || PlayerBuffCompat.HasActiveBuff(player, buffType))
                    {
                        continue;
                    }

                    int width;
                    int height;
                    TryGetStationDimensions(tileType, out width, out height);
                    var frameX = ReadInt(tile, "frameX", 0);
                    var frameY = ReadInt(tile, "frameY", 0);
                    var originX = x;
                    var originY = y;
                    NormalizeStationOrigin(x, y, tileType, frameX, frameY, out originX, out originY);
                    var interactionX = originX + Math.Max(0, width / 2);
                    var interactionY = originY + Math.Max(0, height - 1);
                    var dx = interactionX * 16f + 8f - playerCenterX;
                    var dy = interactionY * 16f + 8f - playerCenterY;
                    var distance = dx * dx + dy * dy;
                    float bestDistance;
                    if (bestDistanceByBuff.TryGetValue(buffType, out bestDistance) && distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistanceByBuff[buffType] = distance;
                    bestByBuff[buffType] = new StationBuffTarget
                    {
                        TileX = x,
                        TileY = y,
                        TileType = tileType,
                        BuffType = buffType,
                        OriginX = originX,
                        OriginY = originY,
                        Width = width,
                        Height = height,
                        FrameX = frameX,
                        FrameY = frameY,
                        DistanceSquared = distance,
                        Name = name
                    };
                }
            }

            if (bestByBuff.Count <= 0)
            {
                message = usingFallbackReach
                    ? "No reachable missing station buff furniture found in fallback tile reach region."
                    : "No reachable missing station buff furniture found.";
                return false;
            }

            foreach (var pair in bestByBuff)
            {
                targets.Add(pair.Value);
            }

            targets.Sort(CompareTargetsByDistance);
            message = usingFallbackReach
                ? "Selected station buff furniture batch by fallback tile reach region."
                : "Selected station buff furniture batch by Terraria tile reach region.";
            return true;
        }

        public static bool OpensInventoryPanel(int tileType)
        {
            return tileType == 125 ||
                   tileType == 699;
        }

        public static List<Tuple<int, int>> BuildInteractionPoints(int tileX, int tileY, int tileType, out string message)
        {
            message = string.Empty;
            var result = new List<Tuple<int, int>>();
            AddUnique(result, tileX, tileY);

            var tiles = GetStatic(TerrariaRuntimeTypes.MainType, "tile") as Array;
            if (tiles == null)
            {
                message = "Main.tile is unavailable; using target tile only.";
                return result;
            }

            var tile = GetTile(tiles, tileX, tileY);
            if (tile == null)
            {
                message = "Target tile is unavailable; using target tile only.";
                return result;
            }

            int width;
            int height;
            TryGetStationDimensions(tileType, out width, out height);
            var frameX = ReadInt(tile, "frameX", 0);
            var frameY = ReadInt(tile, "frameY", 0);
            int originX;
            int originY;
            NormalizeStationOrigin(tileX, tileY, tileType, frameX, frameY, out originX, out originY);

            AddUnique(result, originX + Math.Max(0, width / 2), originY + Math.Max(0, height - 1));
            AddUnique(result, originX, originY);
            AddUnique(result, originX + Math.Max(0, width - 1), originY);
            AddUnique(result, originX, originY + Math.Max(0, height - 1));
            AddUnique(result, originX + Math.Max(0, width - 1), originY + Math.Max(0, height - 1));

            for (var y = originY; y < originY + height; y++)
            {
                for (var x = originX; x < originX + width; x++)
                {
                    var candidateTile = GetTile(tiles, x, y);
                    if (candidateTile != null && IsTileActive(candidateTile) && ReadTileType(candidateTile) == tileType)
                    {
                        AddUnique(result, x, y);
                    }
                }
            }

            for (var y = tileY - 2; y <= tileY + 2; y++)
            {
                for (var x = tileX - 2; x <= tileX + 2; x++)
                {
                    var candidateTile = GetTile(tiles, x, y);
                    if (candidateTile != null && IsTileActive(candidateTile) && ReadTileType(candidateTile) == tileType)
                    {
                        AddUnique(result, x, y);
                    }
                }
            }

            message = "Interaction points built from frame " + frameX + "," + frameY + " origin " + originX + "," + originY + " size " + width + "x" + height + ".";
            return result;
        }

        public static bool TryMapStation(int tileType, out int buffType, out string name)
        {
            switch (tileType)
            {
                case 125:
                    buffType = 29;
                    name = "Crystal Ball";
                    return true;
                case 287:
                    buffType = 93;
                    name = "Ammo Box";
                    return true;
                case 354:
                    buffType = 150;
                    name = "Bewitching Table";
                    return true;
                case 377:
                    buffType = 159;
                    name = "Sharpening Station";
                    return true;
                case 464:
                    buffType = 348;
                    name = "War Table";
                    return true;
                case 621:
                    buffType = 192;
                    name = "Slice of Cake";
                    return true;
                case 699:
                    buffType = 366;
                    name = "Potion Station";
                    return true;
                default:
                    buffType = 0;
                    name = string.Empty;
                    return false;
            }
        }

        public static bool TryGetStationDimensions(int tileType, out int width, out int height)
        {
            switch (tileType)
            {
                case 125:
                case 287:
                    width = 2;
                    height = 2;
                    return true;
                case 354:
                case 377:
                case 464:
                case 699:
                    width = 3;
                    height = 2;
                    return true;
                case 621:
                    width = 2;
                    height = 1;
                    return true;
                default:
                    width = 1;
                    height = 1;
                    return false;
            }
        }

        private static void NormalizeStationOrigin(int tileX, int tileY, int tileType, int frameX, int frameY, out int originX, out int originY)
        {
            int width;
            int height;
            TryGetStationDimensions(tileType, out width, out height);
            var localX = PositiveModulo(frameX / 18, Math.Max(1, width));
            var localY = PositiveModulo(frameY / 18, Math.Max(1, height));
            originX = tileX - localX;
            originY = tileY - localY;
        }

        private static int PositiveModulo(int value, int modulo)
        {
            if (modulo <= 1)
            {
                return 0;
            }

            var result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void AddUnique(List<Tuple<int, int>> points, int x, int y)
        {
            if (points == null || x < 0 || y < 0)
            {
                return;
            }

            for (var index = 0; index < points.Count; index++)
            {
                if (points[index].Item1 == x && points[index].Item2 == y)
                {
                    return;
                }
            }

            points.Add(Tuple.Create(x, y));
        }

        private static int CompareTargetsByDistance(StationBuffTarget left, StationBuffTarget right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var distanceCompare = left.DistanceSquared.CompareTo(right.DistanceSquared);
            return distanceCompare != 0 ? distanceCompare : left.TileType.CompareTo(right.TileType);
        }

        private static object GetTile(Array tiles, int x, int y)
        {
            if (tiles == null || x < 0 || y < 0 || x >= tiles.GetLength(0) || y >= tiles.GetLength(1))
            {
                return null;
            }

            return tiles.GetValue(x, y);
        }

        private static bool IsTileActive(object tile)
        {
            var method = tile.GetType().GetMethod("active", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null)
            {
                try
                {
                    return Convert.ToBoolean(method.Invoke(tile, new object[0]));
                }
                catch
                {
                    return false;
                }
            }

            return ReadBool(tile, "active", false);
        }

        private static int ReadTileType(object tile)
        {
            return ReadInt(tile, "type", 0);
        }

        private static float ReadCenterX(object instance)
        {
            return ReadFloat(instance, "position", "X") + ReadInt(instance, "width", 0) / 2f;
        }

        private static float ReadCenterY(object instance)
        {
            return ReadFloat(instance, "position", "Y") + ReadInt(instance, "height", 0) / 2f;
        }

        private static void BuildFallbackTileReachRegion(object player, out int left, out int top, out int right, out int bottom)
        {
            var centerTileX = (int)(ReadCenterX(player) / 16f);
            var centerTileY = (int)(ReadCenterY(player) / 16f);
            const int horizontalReachTiles = 10;
            const int verticalReachTiles = 8;
            left = centerTileX - horizontalReachTiles;
            right = centerTileX + horizontalReachTiles;
            top = centerTileY - verticalReachTiles;
            bottom = centerTileY + verticalReachTiles;
        }

        private static float ReadFloat(object instance, string vectorName, string componentName)
        {
            var vector = GetMember(instance, vectorName);
            var raw = GetMember(vector, componentName);
            try { return raw == null ? 0f : Convert.ToSingle(raw); }
            catch { return 0f; }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToInt32(raw); }
            catch { return fallback; }
        }

        private static bool ReadBool(object instance, string name, bool fallback)
        {
            var raw = GetMember(instance, name);
            try { return raw == null ? fallback : Convert.ToBoolean(raw); }
            catch { return fallback; }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static object GetStatic(Type type, string name)
        {
            if (type == null)
            {
                return null;
            }

            if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
            {
                return field.GetValue(null);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property)
                ? property.GetValue(null, null)
                : null;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
