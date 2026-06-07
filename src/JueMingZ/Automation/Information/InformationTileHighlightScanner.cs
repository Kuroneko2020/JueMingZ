using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationTileHighlightScanner
    {
        private static readonly HashSet<long> Visited = new HashSet<long>();
        private static readonly List<TilePoint> Stack = new List<TilePoint>(64);

        private static bool _dragonEggMissingLogged;
        private static bool _tileIdsResolved;
        private static int _lifeCrystalTileType = 12;
        private static int _manaCrystalTileType = 639;
        private static int _digtoiseTileType = 751;
        private static int _lifeFruitTileType = 236;
        private static int _dragonEggTileType = -1;

        internal static void Scan(InformationWorldContext context, AppSettings settings, TileHighlightScanBounds bounds, TileHighlightColors colors, IList<TileHighlight> results)
        {
            // Tile highlighting only reads the bounded visible range and builds
            // draw models; it must not edit tiles or expand scan cadence.
            if (context == null || context.MainType == null || settings == null || results == null)
            {
                return;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                LogThrottle.WarnThrottled(
                    "information-main-tile-unavailable",
                    TimeSpan.FromSeconds(30),
                    "InformationTileHighlightScanner",
                    "Main.tile is unavailable; tile highlights skipped.");
                return;
            }

            EnsureTileIdsResolved();
            var lifeCrystalType = _lifeCrystalTileType;
            var manaCrystalType = _manaCrystalTileType;
            var digtoiseType = _digtoiseTileType;
            var lifeFruitType = _lifeFruitTileType;
            var dragonEggType = _dragonEggTileType;
            if (dragonEggType < 0 && settings.InformationHighlightDragonEggEnabled && !_dragonEggMissingLogged)
            {
                _dragonEggMissingLogged = true;
                LogThrottle.WarnThrottled(
                    "information-dragon-egg-tileid-unavailable",
                    TimeSpan.FromMinutes(1),
                    "InformationTileHighlightScanner",
                    "TileID.DragonEgg is unavailable; dragon egg highlight skipped.");
            }

            var minX = bounds.MinX;
            var minY = bounds.MinY;
            var maxX = bounds.MaxX;
            var maxY = bounds.MaxY;
            var allowTypedTileRead = CanUseTypedTileRead(tiles);

            Visited.Clear();
            Stack.Clear();
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    bool active;
                    int tileType;
                    if (!TryReadTileActiveType(tiles, x, y, allowTypedTileRead, out active, out tileType) || !active)
                    {
                        continue;
                    }

                    if (settings.InformationHighlightLifeCrystalEnabled && tileType == lifeCrystalType)
                    {
                        AddGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.LifeCrystal, allowTypedTileRead, results);
                    }
                    else if (settings.InformationHighlightManaCrystalEnabled && tileType == manaCrystalType)
                    {
                        AddGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.ManaCrystal, allowTypedTileRead, results);
                    }
                    else if (settings.InformationHighlightDigtoiseEnabled && tileType == digtoiseType)
                    {
                        AddGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.Digtoise, allowTypedTileRead, results);
                    }
                    else if (settings.InformationHighlightLifeFruitEnabled && tileType == lifeFruitType)
                    {
                        AddGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.LifeFruit, allowTypedTileRead, results);
                    }
                    else if (settings.InformationHighlightDragonEggEnabled && dragonEggType >= 0 && tileType == dragonEggType)
                    {
                        AddGroup(tiles, x, y, minX, minY, maxX, maxY, tileType, colors.DragonEgg, allowTypedTileRead, results);
                    }
                }
            }

            Stack.Clear();
            Visited.Clear();
        }

        internal static bool IsManaCrystalTileTypeForTesting(int tileType)
        {
            EnsureTileIdsResolved();
            return tileType == _manaCrystalTileType;
        }

        internal static void ResetForTesting()
        {
            Visited.Clear();
            Stack.Clear();
            _dragonEggMissingLogged = false;
            _tileIdsResolved = false;
            _lifeCrystalTileType = 12;
            _manaCrystalTileType = 639;
            _digtoiseTileType = 751;
            _lifeFruitTileType = 236;
            _dragonEggTileType = -1;
        }

        private static void AddGroup(object tiles, int startX, int startY, int minX, int minY, int maxX, int maxY, int tileType, InformationColor color, bool allowTypedTileRead, IList<TileHighlight> results)
        {
            var startKey = BuildVisitKey(tileType, startX, startY);
            if (Visited.Contains(startKey))
            {
                return;
            }

            Stack.Clear();
            Stack.Add(new TilePoint(startX, startY));
            var groupMinX = startX;
            var groupMaxX = startX;
            var groupMinY = startY;
            var groupMaxY = startY;
            var matched = 0;

            while (Stack.Count > 0 && matched < 64)
            {
                var last = Stack.Count - 1;
                var point = Stack[last];
                Stack.RemoveAt(last);

                if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
                {
                    continue;
                }

                var key = BuildVisitKey(tileType, point.X, point.Y);
                if (Visited.Contains(key))
                {
                    continue;
                }

                bool active;
                int currentTileType;
                if (!TryReadTileActiveType(tiles, point.X, point.Y, allowTypedTileRead, out active, out currentTileType) ||
                    !active ||
                    currentTileType != tileType)
                {
                    continue;
                }

                Visited.Add(key);
                matched++;
                groupMinX = Math.Min(groupMinX, point.X);
                groupMaxX = Math.Max(groupMaxX, point.X);
                groupMinY = Math.Min(groupMinY, point.Y);
                groupMaxY = Math.Max(groupMaxY, point.Y);

                Stack.Add(new TilePoint(point.X - 1, point.Y));
                Stack.Add(new TilePoint(point.X + 1, point.Y));
                Stack.Add(new TilePoint(point.X, point.Y - 1));
                Stack.Add(new TilePoint(point.X, point.Y + 1));
            }

            if (matched <= 0)
            {
                return;
            }

            results.Add(new TileHighlight(
                groupMinX,
                groupMinY,
                groupMaxX - groupMinX + 1,
                groupMaxY - groupMinY + 1,
                color));
        }

        private static bool TryReadTileActiveType(object tiles, int x, int y, bool allowTypedTileRead, out bool active, out int tileType)
        {
            active = false;
            tileType = -1;

            if (allowTypedTileRead)
            {
                try
                {
                    Tile typedTile;
                    if (TerrariaTileReadCompat.TryGetTile(x, y, out typedTile))
                    {
                        active = TerrariaTileReadCompat.IsActive(typedTile);
                        tileType = TerrariaTileReadCompat.Type(typedTile);
                        return true;
                    }
                }
                catch
                {
                }
            }

            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            if (tile == null)
            {
                return false;
            }

            active = InformationTileAccess.IsActive(tile);
            tileType = InformationTileAccess.ReadType(tile);
            return true;
        }

        private static bool CanUseTypedTileRead(object tiles)
        {
            try
            {
                return tiles != null && ReferenceEquals(tiles, TerrariaMainCompat.Tiles);
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureTileIdsResolved()
        {
            if (_tileIdsResolved)
            {
                return;
            }

            _lifeCrystalTileType = ReadTileId("LifeCrystal", 12);
            _manaCrystalTileType = ReadTileId("ManaCrystal", 639);
            _digtoiseTileType = ReadTileId("PalworldDigtoiseSleeping", 751);
            _lifeFruitTileType = ReadTileId("LifeFruit", 236);
            _dragonEggTileType = ReadTileId("DragonEgg", -1);
            _tileIdsResolved = true;
        }

        private static int ReadTileId(string name, int fallback)
        {
            var tileIdType = InformationReflection.FindType("Terraria.ID.TileID");
            int value;
            return TryReadStaticInt(tileIdType, name, out value) ? value : fallback;
        }

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = InformationReflection.GetStaticMember(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static long BuildVisitKey(int tileType, int x, int y)
        {
            unchecked
            {
                return ((long)(tileType & 0xffff) << 48) |
                       ((long)(x & 0x00ffffff) << 24) |
                       (uint)(y & 0x00ffffff);
            }
        }
    }
}
