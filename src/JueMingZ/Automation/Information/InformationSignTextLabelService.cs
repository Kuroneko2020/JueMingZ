using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationSignTextLabelService
    {
        internal const int MaxSignLabelsPerFrame = 40;
        internal const int MaxTombstoneLabelsPerFrame = 40;
        internal const float LabelMaxDistance = 1600f;

        private const int TileSize = 16;
        private const ulong ScanIntervalTicks = 60;
        private static readonly object SyncRoot = new object();
        private static readonly InformationWorldLabelRenderer LabelRenderer = new InformationWorldLabelRenderer();
        private static readonly SignTextLabel[] EmptyLabels = new SignTextLabel[0];
        private static readonly List<SignTextLabel> LabelBuildBuffer = new List<SignTextLabel>();
        private static SignTextLabel[] _cachedSignLabels = EmptyLabels;
        private static SignTextLabel[] _cachedTombstoneLabels = EmptyLabels;
        private static ulong _lastSignScanTick;
        private static ulong _lastTombstoneScanTick;
        private static bool _tombstoneTileTypeResolved;
        private static int _tombstoneTileType = 85;

        internal static SignTextLabel[] GetSignLabels(InformationWorldContext context)
        {
            lock (SyncRoot)
            {
                if (CanReuseCache(context, _lastSignScanTick))
                {
                    return _cachedSignLabels;
                }

                LabelBuildBuffer.Clear();
                AddAllLabels(context, LabelBuildBuffer, false);
                _cachedSignLabels = LabelBuildBuffer.Count == 0 ? EmptyLabels : LabelBuildBuffer.ToArray();
                _lastSignScanTick = context == null ? 0 : context.GameUpdateCount;
                return _cachedSignLabels;
            }
        }

        internal static SignTextLabel[] GetTombstoneLabels(InformationWorldContext context)
        {
            lock (SyncRoot)
            {
                if (CanReuseCache(context, _lastTombstoneScanTick))
                {
                    return _cachedTombstoneLabels;
                }

                LabelBuildBuffer.Clear();
                AddAllLabels(context, LabelBuildBuffer, true);
                _cachedTombstoneLabels = LabelBuildBuffer.Count == 0 ? EmptyLabels : LabelBuildBuffer.ToArray();
                _lastTombstoneScanTick = context == null ? 0 : context.GameUpdateCount;
                return _cachedTombstoneLabels;
            }
        }

        internal static bool IsTombstoneTileTypeForTesting(int tileType)
        {
            return IsTombstoneTileType(tileType);
        }

        private static bool CanReuseCache(InformationWorldContext context, ulong lastScanTick)
        {
            return context != null &&
                   context.GameUpdateCount != 0 &&
                   lastScanTick != 0 &&
                   context.GameUpdateCount >= lastScanTick &&
                   context.GameUpdateCount - lastScanTick < ScanIntervalTicks;
        }

        private static void AddAllLabels(InformationWorldContext context, IList<SignTextLabel> labels, bool tombstoneLabels)
        {
            if (context == null || context.MainType == null || labels == null)
            {
                return;
            }

            var signs = InformationReflection.GetStaticMember(context.MainType, "sign");
            var count = GetCollectionCount(signs);
            for (var index = 0; index < count; index++)
            {
                var sign = InformationReflection.GetIndexedValue(signs, index);
                if (sign == null)
                {
                    continue;
                }

                int signX;
                int signY;
                if (!InformationReflection.TryReadInt(sign, "x", out signX) ||
                    !InformationReflection.TryReadInt(sign, "y", out signY) ||
                    signX <= 0 ||
                    signY <= 0)
                {
                    continue;
                }

                var text = InformationReflection.TryReadString(sign, "text");
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var worldLeft = signX * TileSize;
                var worldTop = signY * TileSize;
                var worldRight = worldLeft + TileSize * 2;
                if (!LabelRenderer.CanDraw(context, worldLeft, worldTop, LabelMaxDistance, false))
                {
                    continue;
                }

                if (!IsValidSignTile(context, signX, signY, tombstoneLabels))
                {
                    continue;
                }

                labels.Add(new SignTextLabel
                {
                    TileX = signX,
                    TileY = signY,
                    WorldLeft = worldLeft,
                    WorldTop = worldTop,
                    WorldRight = worldRight,
                    Text = text,
                    TextHash = InformationSignTextLayoutCache.HashText(text)
                });
            }
        }

        private static bool IsValidSignTile(InformationWorldContext context, int tileX, int tileY, bool tombstoneLabels)
        {
            if (context == null || context.MainType == null)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            bool active;
            int tileType;
            if (!TryReadTileActiveType(tiles, tileX, tileY, out active, out tileType) || !active)
            {
                return false;
            }

            if (tileType < 0)
            {
                return false;
            }

            if (!IsTileSignType(context.MainType, tileType))
            {
                return false;
            }

            var isTombstone = IsTombstoneTileType(tileType);
            return tombstoneLabels ? isTombstone : !isTombstone;
        }

        private static bool TryReadTileActiveType(object tiles, int x, int y, out bool active, out int tileType)
        {
            active = false;
            tileType = -1;

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

            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            if (tile == null)
            {
                return false;
            }

            active = InformationTileAccess.IsActive(tile);
            tileType = InformationTileAccess.ReadType(tile);
            return true;
        }

        private static bool IsTileSignType(Type mainType, int tileType)
        {
            var tileSign = InformationReflection.GetStaticMember(mainType, "tileSign");
            var raw = InformationReflection.GetIndexedValue(tileSign, tileType);
            try
            {
                return raw != null && Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTombstoneTileType(int tileType)
        {
            EnsureTombstoneTileTypeResolved();
            return tileType == _tombstoneTileType;
        }

        private static void EnsureTombstoneTileTypeResolved()
        {
            if (_tombstoneTileTypeResolved)
            {
                return;
            }

            _tombstoneTileType = ReadTileId("Tombstones", 85);
            _tombstoneTileTypeResolved = true;
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

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 ? array.GetLength(0) : 0;
        }
    }
}
