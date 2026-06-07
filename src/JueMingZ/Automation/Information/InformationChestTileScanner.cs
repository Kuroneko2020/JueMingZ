using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    // Chest scans stay bounded to visible label work; unreadable tiles fail closed instead of mutating or widening scans.
    internal static class InformationChestTileScanner
    {
        internal const int TileSize = 16;
        internal const int TileFrameSize = 18;
        internal const int TileTypeContainers = 21;
        internal const int TileTypeDressers = 88;
        internal const int TileTypeFakeContainers = 441;
        internal const int TileTypeContainers2 = 467;
        internal const int TileTypeFakeContainers2 = 468;
        internal const int FrameColumns = 2;
        internal const int FrameRows = 2;
        internal const int DresserFrameColumns = 3;
        internal const int ScanMarginTiles = 6;
        internal const float CacheCullPadding = ScanMarginTiles * TileSize;

        private const int StyleFrameWidth = FrameColumns * TileFrameSize;
        private const int DresserStyleFrameWidth = DresserFrameColumns * TileFrameSize;

        internal static bool CanCacheLabel(InformationWorldContext context, float worldX, float worldY)
        {
            if (context == null || context.LocalPlayer == null)
            {
                return false;
            }

            var screenX = worldX - context.ScreenX;
            var screenY = worldY - context.ScreenY;
            if (screenX < -CacheCullPadding ||
                screenY < -CacheCullPadding ||
                screenX > context.ScreenWidth + CacheCullPadding ||
                screenY > context.ScreenHeight + CacheCullPadding)
            {
                return false;
            }

            var maxDistance = InformationChestLabelService.LabelMaxDistance + CacheCullPadding;
            return DistanceSquared(worldX, worldY, context.PlayerCenterX, context.PlayerCenterY) <= maxDistance * maxDistance;
        }

        internal static bool TryGetScanBounds(InformationWorldContext context, object tiles, out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = 0;
            maxX = -1;
            minY = 0;
            maxY = -1;

            int worldMaxX;
            int worldMaxY;
            if (!TryReadTileWorldBounds(context == null ? null : context.MainType, tiles, out worldMaxX, out worldMaxY) ||
                worldMaxX <= 0 ||
                worldMaxY <= 0)
            {
                return false;
            }

            minX = Math.Max(0, (int)Math.Floor(((context == null ? 0f : context.ScreenX) - CacheCullPadding) / TileSize) - 2);
            maxX = Math.Min(worldMaxX - 1, (int)Math.Ceiling(((context == null ? 0f : context.ScreenX) + (context == null ? 0 : context.ScreenWidth) + CacheCullPadding) / TileSize) + 2);
            minY = Math.Max(0, (int)Math.Floor(((context == null ? 0f : context.ScreenY) - CacheCullPadding) / TileSize) - 2);
            maxY = Math.Min(worldMaxY - 1, (int)Math.Ceiling(((context == null ? 0f : context.ScreenY) + (context == null ? 0 : context.ScreenHeight) + CacheCullPadding) / TileSize) + 2);
            return maxX >= minX && maxY >= minY;
        }

        internal static void CollectVisibleCandidates(
            InformationWorldContext context,
            object tiles,
            ISet<long> added,
            IList<ChestScanCandidate> candidates,
            int minX,
            int maxX,
            int minY,
            int maxY,
            out int tilesVisited,
            out int typedTileReads,
            out int fallbackTileReads,
            out int failedTileReads)
        {
            tilesVisited = 0;
            typedTileReads = 0;
            fallbackTileReads = 0;
            failedTileReads = 0;
            var allowTypedTileRead = CanUseTypedTileRead(tiles);
            if (context == null || context.MainType == null || tiles == null || candidates == null)
            {
                return;
            }

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    CollectVisibleCandidate(
                        context,
                        tiles,
                        added,
                        candidates,
                        x,
                        y,
                        allowTypedTileRead,
                        ref tilesVisited,
                        ref typedTileReads,
                        ref fallbackTileReads,
                        ref failedTileReads);
                }
            }
        }

        internal static void CollectVisibleCandidate(
            InformationWorldContext context,
            object tiles,
            ISet<long> added,
            IList<ChestScanCandidate> candidates,
            int x,
            int y,
            bool allowTypedTileRead,
            ref int tilesVisited,
            ref int typedTileReads,
            ref int fallbackTileReads,
            ref int failedTileReads)
        {
            tilesVisited++;
            bool active;
            int tileType;
            int frameX;
            int frameY;
            bool usedTypedTileRead;
            if (!TryReadTileActiveTypeAndFrame(
                    tiles,
                    x,
                    y,
                    out active,
                    out tileType,
                    out frameX,
                    out frameY,
                    allowTypedTileRead,
                    out usedTypedTileRead))
            {
                failedTileReads++;
                return;
            }

            if (usedTypedTileRead)
            {
                typedTileReads++;
            }
            else
            {
                fallbackTileReads++;
            }

            if (!active || !IsChestTileType(tileType))
            {
                return;
            }

            int chestX;
            int chestY;
            if (!TryNormalizeOriginFromFrame(tileType, x, y, frameX, frameY, out chestX, out chestY))
            {
                return;
            }

            var key = InformationChestNameResolver.BuildPositionKey(chestX, chestY);
            if (added != null && added.Contains(key))
            {
                return;
            }

            var worldX = BuildLabelWorldX(chestX, tileType);
            var worldY = BuildLabelWorldY(chestY, tileType);
            if (!CanCacheLabel(context, worldX, worldY))
            {
                return;
            }

            if (added != null)
            {
                added.Add(key);
            }

            candidates.Add(new ChestScanCandidate
            {
                ChestX = chestX,
                ChestY = chestY,
                Key = key,
                TileType = tileType,
                TileStyle = BuildTileStyle(tileType, frameX),
                WorldX = worldX,
                WorldY = worldY
            });
        }

        internal static string BuildTypedTileFastPathStatus(int typedTileReads, int fallbackTileReads, int failedTileReads)
        {
            if (typedTileReads <= 0 && fallbackTileReads <= 0 && failedTileReads <= 0)
            {
                return "none";
            }

            return "typed=" + typedTileReads.ToString(CultureInfo.InvariantCulture) +
                   ";fallback=" + fallbackTileReads.ToString(CultureInfo.InvariantCulture) +
                   ";failed=" + failedTileReads.ToString(CultureInfo.InvariantCulture);
        }

        internal static int BuildTileStyle(int tileType, int frameX)
        {
            if (tileType < 0 || frameX < 0)
            {
                return 0;
            }

            return Math.Max(0, frameX / GetTileStyleFrameWidth(tileType));
        }

        internal static float BuildLabelWorldX(int chestX, int tileType)
        {
            return (chestX * TileSize) + (GetFrameColumns(tileType) * TileSize * 0.5f);
        }

        internal static float BuildLabelWorldY(int chestY, int tileType)
        {
            return (chestY * TileSize) + (GetFrameRows(tileType) * TileSize * 0.5f);
        }

        internal static bool TryResolveTileInfoAt(InformationWorldContext context, int tileX, int tileY, out int tileType, out int tileStyle)
        {
            tileType = TileTypeContainers;
            tileStyle = 0;
            if (context == null || context.MainType == null || tileX < 0 || tileY < 0)
            {
                return false;
            }

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            bool active;
            int frameX;
            int frameY;
            if (!TryReadTileActiveTypeAndFrame(
                    tiles,
                    tileX,
                    tileY,
                    out active,
                    out tileType,
                    out frameX,
                    out frameY) ||
                !active ||
                frameX < 0 ||
                frameY < 0 ||
                !IsChestTileType(tileType))
            {
                tileType = TileTypeContainers;
                tileStyle = 0;
                return false;
            }

            tileStyle = BuildTileStyle(tileType, frameX);
            return true;
        }

        internal static bool TryNormalizeOriginFromFrame(int tileType, int tileX, int tileY, int frameX, int frameY, out int chestX, out int chestY)
        {
            chestX = tileX - PositiveModulo(frameX / TileFrameSize, GetFrameColumns(tileType));
            chestY = tileY - PositiveModulo(frameY / TileFrameSize, GetFrameRows(tileType));
            return chestX >= 0 && chestY >= 0;
        }

        internal static bool IsChestTileType(int tileType)
        {
            return tileType == TileTypeContainers ||
                   tileType == TileTypeContainers2 ||
                   tileType == TileTypeDressers ||
                   tileType == TileTypeFakeContainers ||
                   tileType == TileTypeFakeContainers2;
        }

        internal static bool IsDresserTileType(int tileType)
        {
            return tileType == TileTypeDressers;
        }

        internal static int GetFrameColumns(int tileType)
        {
            return IsDresserTileType(tileType) ? DresserFrameColumns : FrameColumns;
        }

        internal static int GetFrameRows(int tileType)
        {
            return FrameRows;
        }

        private static int GetTileStyleFrameWidth(int tileType)
        {
            return IsDresserTileType(tileType) ? DresserStyleFrameWidth : StyleFrameWidth;
        }

        private static bool TryReadTileWorldBounds(Type mainType, object tiles, out int maxX, out int maxY)
        {
            maxX = 0;
            maxY = 0;
            try
            {
                maxX = TerrariaMainCompat.MaxTilesX;
                maxY = TerrariaMainCompat.MaxTilesY;
                if (maxX > 0 && maxY > 0)
                {
                    return true;
                }
            }
            catch
            {
            }

            if (InformationReflection.TryReadStaticInt(mainType, "maxTilesX", out maxX) &&
                InformationReflection.TryReadStaticInt(mainType, "maxTilesY", out maxY) &&
                maxX > 0 &&
                maxY > 0)
            {
                return true;
            }

            var array = tiles as Array;
            if (array != null)
            {
                if (array.Rank == 2)
                {
                    maxX = array.GetLength(0);
                    maxY = array.GetLength(1);
                    return maxX > 0 && maxY > 0;
                }

                if (array.Rank == 1 && array.GetLength(0) > 0)
                {
                    maxX = array.GetLength(0);
                    maxY = GetCollectionCount(array.GetValue(0));
                    return maxY > 0;
                }
            }

            var list = tiles as IList;
            if (list != null && list.Count > 0)
            {
                maxX = list.Count;
                maxY = GetCollectionCount(list[0]);
                return maxY > 0;
            }

            return false;
        }

        private static bool TryReadTileActiveTypeAndFrame(object tiles, int x, int y, out bool active, out int tileType, out int frameX, out int frameY)
        {
            bool usedTypedTileRead;
            return TryReadTileActiveTypeAndFrame(
                tiles,
                x,
                y,
                out active,
                out tileType,
                out frameX,
                out frameY,
                true,
                out usedTypedTileRead);
        }

        private static bool TryReadTileActiveTypeAndFrame(object tiles, int x, int y, out bool active, out int tileType, out int frameX, out int frameY, bool allowTypedTileRead, out bool usedTypedTileRead)
        {
            active = false;
            tileType = -1;
            frameX = 0;
            frameY = 0;
            usedTypedTileRead = false;

            if (allowTypedTileRead)
            {
                try
                {
                    Tile typedTile;
                    if (TerrariaTileReadCompat.TryGetTile(x, y, out typedTile))
                    {
                        active = TerrariaTileReadCompat.IsActive(typedTile);
                        tileType = TerrariaTileReadCompat.Type(typedTile);
                        frameX = TerrariaTileReadCompat.FrameX(typedTile);
                        frameY = TerrariaTileReadCompat.FrameY(typedTile);
                        usedTypedTileRead = true;
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
            frameX = InformationTileAccess.ReadFrameX(tile);
            frameY = InformationTileAccess.ReadFrameY(tile);
            return true;
        }

        internal static bool CanUseTypedTileRead(object tiles)
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

        private static int PositiveModulo(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            var result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static float DistanceSquared(float leftX, float leftY, float rightX, float rightY)
        {
            var dx = leftX - rightX;
            var dy = leftY - rightY;
            return dx * dx + dy * dy;
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
