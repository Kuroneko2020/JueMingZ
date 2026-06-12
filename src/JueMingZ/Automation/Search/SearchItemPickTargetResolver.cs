using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Search
{
    internal static class SearchItemPickTargetResolver
    {
        public static SearchItemPickResolveAttempt Resolve(SearchItemPickResolveContext context)
        {
            context = context ?? new SearchItemPickResolveContext();

            if (context.UiItemType > 0 && context.UiItemStack > 0)
            {
                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = context.UiItemType,
                    SourceKind = "uiItem",
                    SourceSummary = "uiItem;source=" + NormalizeSource(context.UiItemSource)
                });
            }

            var worldItem = ResolveHitWorldItem(context);
            if (worldItem != null)
            {
                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = worldItem.ItemType,
                    SourceKind = "worldItem",
                    SourceSummary = "worldItem"
                });
            }

            if (context.Tile != null && context.Tile.Active && context.Tile.PlacementItemType > 0)
            {
                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = context.Tile.PlacementItemType,
                    SourceKind = "tile",
                    SourceSummary = "tile;type=" + context.Tile.TileType.ToString(CultureInfo.InvariantCulture) +
                                    ";style=" + context.Tile.TileStyle.ToString(CultureInfo.InvariantCulture)
                });
            }

            if (context.Wall != null && context.Wall.Active && context.Wall.PlacementItemType > 0)
            {
                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = context.Wall.PlacementItemType,
                    SourceKind = "wall",
                    SourceSummary = "wall;type=" + context.Wall.WallType.ToString(CultureInfo.InvariantCulture)
                });
            }

            return SearchItemPickResolveAttempt.Failed("noSearchableItem");
        }

        public static bool TryResolveCurrent(out SearchItemPickResolveResult result, out string skipReason)
        {
            result = null;
            skipReason = string.Empty;

            SearchItemPickResolveContext context;
            if (!TryBuildCurrentContext(out context, out skipReason))
            {
                return false;
            }

            var attempt = Resolve(context);
            if (attempt == null || !attempt.Succeeded || attempt.Result == null)
            {
                skipReason = attempt == null ? "resolveUnavailable" : attempt.FailureReason;
                return false;
            }

            result = attempt.Result;
            return result.ItemType > 0;
        }

        internal static bool TryCaptureCurrentClickContext(
            int mouseScreenX,
            int mouseScreenY,
            ulong fallbackGameUpdateCount,
            out SearchItemPickClickContext clickContext,
            out string skipReason)
        {
            clickContext = null;
            skipReason = string.Empty;

            InformationWorldContext worldContext;
            if (!InformationWorldContextProvider.TryBuild(InformationWorldContextProfile.Status, out worldContext, out skipReason))
            {
                clickContext = SearchItemPickClickContext.Failed(
                    skipReason,
                    mouseScreenX,
                    mouseScreenY,
                    fallbackGameUpdateCount);
                return false;
            }

            var mouseWorldX = worldContext.ScreenX + mouseScreenX;
            var mouseWorldY = worldContext.ScreenY + mouseScreenY;
            clickContext = SearchItemPickClickContext.Success(
                mouseScreenX,
                mouseScreenY,
                mouseWorldX,
                mouseWorldY,
                (int)Math.Floor(mouseWorldX / TerrariaTileReadCompat.TileSize),
                (int)Math.Floor(mouseWorldY / TerrariaTileReadCompat.TileSize),
                worldContext.GameUpdateCount);
            return true;
        }

        internal static SearchItemPickResolveAttempt ResolveUiHoverFromPending(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount)
        {
            if (pending == null || pending.ClickContext == null || !pending.ClickContext.Succeeded)
            {
                return SearchItemPickResolveAttempt.Failed("pendingClickContextUnavailable");
            }

            var context = CreateContextFromClick(pending.ClickContext, currentGameUpdateCount);
            TerrariaUiHoverSlotSnapshot slotSnapshot;
            if (!TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(
                    context.GameUpdateCount,
                    context.MouseScreenX,
                    context.MouseScreenY,
                    out slotSnapshot) ||
                slotSnapshot == null)
            {
                return SearchItemPickResolveAttempt.Failed("uiHoverPending");
            }

            if (!slotSnapshot.HasActiveItem ||
                slotSnapshot.ItemSnapshot == null ||
                slotSnapshot.ItemSnapshot.ItemType <= 0 ||
                slotSnapshot.ItemSnapshot.Stack <= 0)
            {
                return SearchItemPickResolveAttempt.Failed("uiEmptySlot");
            }

            context.UiItemType = slotSnapshot.ItemSnapshot.ItemType;
            context.UiItemStack = slotSnapshot.ItemSnapshot.Stack;
            context.UiItemSource = slotSnapshot.ItemSnapshot.Source ?? string.Empty;
            return Resolve(context);
        }

        internal static bool TryResolvePendingFallback(
            SearchItemPickPendingClick pending,
            ulong currentGameUpdateCount,
            out SearchItemPickResolveResult result,
            out string skipReason)
        {
            result = null;
            skipReason = string.Empty;
            if (pending == null || pending.ClickContext == null || !pending.ClickContext.Succeeded)
            {
                skipReason = "pendingClickContextUnavailable";
                return false;
            }

            var context = CreateContextFromClick(pending.ClickContext, currentGameUpdateCount);
            AddWorldItems(context);
            AddTileAndWall(context);
            var attempt = Resolve(context);
            if (attempt == null || !attempt.Succeeded || attempt.Result == null)
            {
                skipReason = attempt == null ? "resolveUnavailable" : attempt.FailureReason;
                return false;
            }

            result = attempt.Result;
            return result.ItemType > 0;
        }

        private static SearchItemPickWorldItemTarget ResolveHitWorldItem(SearchItemPickResolveContext context)
        {
            if (context == null || context.WorldItems == null)
            {
                return null;
            }

            for (var index = 0; index < context.WorldItems.Count; index++)
            {
                var item = context.WorldItems[index];
                if (item != null && item.Contains(context.MouseWorldX, context.MouseWorldY))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool TryBuildCurrentContext(out SearchItemPickResolveContext context, out string skipReason)
        {
            context = null;
            skipReason = string.Empty;

            InformationWorldContext worldContext;
            if (!InformationWorldContextProvider.TryBuild(InformationWorldContextProfile.Status, out worldContext, out skipReason))
            {
                return false;
            }

            var mouseScreenX = TerrariaMainCompat.MouseX;
            var mouseScreenY = TerrariaMainCompat.MouseY;
            var mouseWorldX = worldContext.ScreenX + mouseScreenX;
            var mouseWorldY = worldContext.ScreenY + mouseScreenY;
            context = new SearchItemPickResolveContext
            {
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY,
                MouseScreenX = mouseScreenX,
                MouseScreenY = mouseScreenY,
                MouseTileX = (int)Math.Floor(mouseWorldX / TerrariaTileReadCompat.TileSize),
                MouseTileY = (int)Math.Floor(mouseWorldY / TerrariaTileReadCompat.TileSize),
                GameUpdateCount = worldContext.GameUpdateCount
            };

            AddUiHoverItem(context);
            AddWorldItems(context);
            AddTileAndWall(context);
            return true;
        }

        private static SearchItemPickResolveContext CreateContextFromClick(
            SearchItemPickClickContext clickContext,
            ulong gameUpdateCount)
        {
            clickContext = clickContext ?? SearchItemPickClickContext.Failed("clickContextUnavailable", 0, 0, gameUpdateCount);
            if (gameUpdateCount < clickContext.GameUpdateCount)
            {
                gameUpdateCount = clickContext.GameUpdateCount;
            }

            return new SearchItemPickResolveContext
            {
                MouseWorldX = clickContext.MouseWorldX,
                MouseWorldY = clickContext.MouseWorldY,
                MouseScreenX = clickContext.MouseScreenX,
                MouseScreenY = clickContext.MouseScreenY,
                MouseTileX = clickContext.MouseTileX,
                MouseTileY = clickContext.MouseTileY,
                GameUpdateCount = gameUpdateCount
            };
        }

        private static bool AddUiHoverItem(SearchItemPickResolveContext context)
        {
            TerrariaUiHoverItemSnapshot snapshot;
            if (context == null ||
                !TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(
                    context.GameUpdateCount,
                    context.MouseScreenX,
                    context.MouseScreenY,
                    out snapshot) ||
                snapshot == null ||
                snapshot.ItemType <= 0 ||
                snapshot.Stack <= 0)
            {
                return false;
            }

            context.UiItemType = snapshot.ItemType;
            context.UiItemStack = snapshot.Stack;
            context.UiItemSource = snapshot.Source ?? string.Empty;
            return true;
        }

        private static void AddWorldItems(SearchItemPickResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var items = InformationReflection.GetStaticMember(TerrariaRuntimeTypes.MainType, "item");
            var count = GetCollectionCount(items);
            for (var index = 0; index < count; index++)
            {
                var rawItem = InformationReflection.GetIndexedValue(items, index);
                SearchItemPickWorldItemTarget item;
                if (TryReadWorldItem(rawItem, out item))
                {
                    context.WorldItems.Add(item);
                }
            }
        }

        private static bool TryReadWorldItem(object rawItem, out SearchItemPickWorldItemTarget item)
        {
            item = null;
            if (rawItem == null)
            {
                return false;
            }

            int type;
            int stack;
            if (!InformationReflection.TryReadInt(rawItem, "type", out type) ||
                !InformationReflection.TryReadInt(rawItem, "stack", out stack) ||
                type <= 0 ||
                stack <= 0)
            {
                return false;
            }

            float x;
            float y;
            if (!InformationReflection.TryReadVectorMember(rawItem, "position", out x, out y))
            {
                return false;
            }

            int width;
            int height;
            if (!InformationReflection.TryReadInt(rawItem, "width", out width))
            {
                width = TerrariaTileReadCompat.TileSize;
            }

            if (!InformationReflection.TryReadInt(rawItem, "height", out height))
            {
                height = TerrariaTileReadCompat.TileSize;
            }

            item = new SearchItemPickWorldItemTarget
            {
                ItemType = type,
                Stack = stack,
                HitboxX = x,
                HitboxY = y,
                HitboxWidth = width <= 0 ? TerrariaTileReadCompat.TileSize : width,
                HitboxHeight = height <= 0 ? TerrariaTileReadCompat.TileSize : height
            };
            return true;
        }

        private static void AddTileAndWall(SearchItemPickResolveContext context)
        {
            if (context == null ||
                !TerrariaMainCompat.IsTileCoordinateInWorld(context.MouseTileX, context.MouseTileY))
            {
                return;
            }

            Tile tile;
            if (!TerrariaTileReadCompat.TryGetTile(context.MouseTileX, context.MouseTileY, out tile))
            {
                return;
            }

            if (TerrariaTileReadCompat.IsActive(tile))
            {
                var tileType = TerrariaTileReadCompat.Type(tile);
                var frameX = TerrariaTileReadCompat.FrameX(tile);
                var frameY = TerrariaTileReadCompat.FrameY(tile);
                var style = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(tileType, frameX, frameY);
                int itemType;
                context.Tile = new SearchItemPickTileTarget
                {
                    Active = true,
                    TileType = tileType,
                    TileStyle = style,
                    FrameX = frameX,
                    FrameY = frameY,
                    PlacementItemType = MapQuickAnnouncementPlacementNameCache.TryResolveTileItem(tileType, style, out itemType)
                        ? itemType
                        : 0
                };
            }

            var wallType = TerrariaTileReadCompat.Wall(tile);
            if (wallType > 0)
            {
                int itemType;
                context.Wall = new SearchItemPickWallTarget
                {
                    Active = true,
                    WallType = wallType,
                    PlacementItemType = MapQuickAnnouncementPlacementNameCache.TryResolveWallItem(wallType, out itemType)
                        ? itemType
                        : 0
                };
            }
        }

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var array = source as Array;
            if (array != null)
            {
                return array.Rank == 1 ? array.GetLength(0) : 0;
            }

            var collection = source as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static string NormalizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "unknown";
            }

            var trimmed = source.Trim();
            var separator = trimmed.IndexOf(':');
            return separator > 0 ? trimmed.Substring(0, separator) : trimmed;
        }
    }
}
