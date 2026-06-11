using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementTargetResolver
    {
        public static MapQuickAnnouncementResolveResult Resolve(MapQuickAnnouncementResolveContext context)
        {
            context = context ?? new MapQuickAnnouncementResolveContext();

            if (context.UiItem != null && context.UiItem.IsActive)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.UiItem,
                    MapQuickAnnouncementTextBuilder.BuildItemText(context.UiItem.Stack, context.UiItem.Name),
                    BuildUiItemDetail(context.UiItem),
                    context.UiItem.Name,
                    context.UiItem.Stack);
            }

            var actors = ResolveActorsAtMouse(context);
            if (actors.Count > 0)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.Actor,
                    MapQuickAnnouncementTextBuilder.BuildActorText(actors),
                    "actor:" + actors.Count.ToString(CultureInfo.InvariantCulture),
                    BuildActorSummary(actors),
                    actors.Count);
            }

            var item = ResolveHitWorldItem(context);
            if (item != null)
            {
                var stack = ResolveNearbyStack(context, item);
                return BuildResult(
                    MapQuickAnnouncementTargetKind.WorldItem,
                    MapQuickAnnouncementTextBuilder.BuildItemText(stack, item.Name),
                    "worldItem",
                    item.Name,
                    stack);
            }

            if (context.Tile != null && context.Tile.HasAnyLayer)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.Tile,
                    MapQuickAnnouncementTextBuilder.BuildTileText(context.Tile),
                    BuildTileDetail(context.Tile),
                    context.Tile.TileName,
                    1);
            }

            if (context.Wall != null && context.Wall.Active)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.Wall,
                    MapQuickAnnouncementTextBuilder.BuildWallText(context.Wall),
                    BuildWallDetail(context.Wall),
                    context.Wall.WallName,
                    1);
            }

            var airText = MapQuickAnnouncementTextBuilder.BuildAirText(ResolveAirPhraseIndex(context));
            return BuildResult(
                MapQuickAnnouncementTargetKind.Air,
                airText,
                "air",
                airText,
                0);
        }

        public static bool TryResolveCurrent(out MapQuickAnnouncementResolveResult result, out string skipReason)
        {
            result = null;
            skipReason = string.Empty;

            MapQuickAnnouncementResolveContext context;
            if (!TryBuildCurrentContext(out context, out skipReason))
            {
                return false;
            }

            result = Resolve(context);
            return result != null && !string.IsNullOrWhiteSpace(result.Body);
        }

        internal static int ResolveAirPhraseIndexForTesting(MapQuickAnnouncementResolveContext context)
        {
            return ResolveAirPhraseIndex(context ?? new MapQuickAnnouncementResolveContext());
        }

        private static MapQuickAnnouncementResolveResult BuildResult(
            MapQuickAnnouncementTargetKind kind,
            string body,
            string detail,
            string targetName,
            int targetCount)
        {
            var resolvedBody = string.IsNullOrWhiteSpace(body) ? MapQuickAnnouncementTextBuilder.BuildAirText(0) : body;
            var resolvedKind = string.IsNullOrWhiteSpace(body) ? MapQuickAnnouncementTargetKind.Air : kind;
            return new MapQuickAnnouncementResolveResult
            {
                Kind = resolvedKind,
                Body = resolvedBody,
                Detail = detail ?? string.Empty,
                TargetName = string.IsNullOrWhiteSpace(targetName) ? resolvedBody : targetName,
                TargetCount = Math.Max(0, targetCount)
            };
        }

        private static string BuildActorSummary(IList<MapQuickAnnouncementActorTarget> actors)
        {
            if (actors == null || actors.Count == 0)
            {
                return string.Empty;
            }

            if (actors.Count == 1)
            {
                return MapQuickAnnouncementTextBuilder.BuildActorPart(actors[0]);
            }

            return actors.Count.ToString(CultureInfo.InvariantCulture) + " actors";
        }

        private static string BuildUiItemDetail(MapQuickAnnouncementItemTarget item)
        {
            if (item == null)
            {
                return "uiItem";
            }

            var source = string.IsNullOrWhiteSpace(item.HoverSource)
                ? "unknown"
                : item.HoverSource.Trim();
            return "uiItem;source=" + source +
                   ";context=" + item.HoverContext.ToString(CultureInfo.InvariantCulture) +
                   ";slot=" + item.HoverSlot.ToString(CultureInfo.InvariantCulture) +
                   ";ageUpdates=" + Math.Max(0, item.HoverAgeUpdates).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildTileDetail(MapQuickAnnouncementTileTarget tile)
        {
            if (tile == null)
            {
                return "tile";
            }

            var source = string.IsNullOrWhiteSpace(tile.NameSource) ? "unknown" : tile.NameSource.Trim();
            var detail = "tile:" + source +
                         ";type=" + tile.TileType.ToString(CultureInfo.InvariantCulture) +
                         ";style=" + tile.TileStyle.ToString(CultureInfo.InvariantCulture) +
                         ";frame=" + tile.FrameX.ToString(CultureInfo.InvariantCulture) +
                         "," + tile.FrameY.ToString(CultureInfo.InvariantCulture);
            if (tile.HasLiquid)
            {
                detail += ";liquid=" +
                          MapQuickAnnouncementTextBuilder.BuildLiquidName(tile.LiquidType) +
                          ":" +
                          tile.LiquidAmount.ToString(CultureInfo.InvariantCulture);
            }

            return detail;
        }

        private static string BuildWallDetail(MapQuickAnnouncementWallTarget wall)
        {
            if (wall == null)
            {
                return "wall";
            }

            var source = string.IsNullOrWhiteSpace(wall.NameSource) ? "unknown" : wall.NameSource.Trim();
            return "wall:" + source +
                   ";type=" + wall.WallType.ToString(CultureInfo.InvariantCulture);
        }

        private static List<MapQuickAnnouncementActorTarget> ResolveActorsAtMouse(MapQuickAnnouncementResolveContext context)
        {
            var result = new List<MapQuickAnnouncementActorTarget>();
            if (context == null || context.Actors == null)
            {
                return result;
            }

            for (var index = 0; index < context.Actors.Count; index++)
            {
                var actor = context.Actors[index];
                if (actor != null && actor.Contains(context.MouseWorldX, context.MouseWorldY))
                {
                    result.Add(actor);
                }
            }

            return result;
        }

        private static MapQuickAnnouncementWorldItemTarget ResolveHitWorldItem(MapQuickAnnouncementResolveContext context)
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

        private static int ResolveNearbyStack(MapQuickAnnouncementResolveContext context, MapQuickAnnouncementWorldItemTarget hitItem)
        {
            if (context == null || context.WorldItems == null || hitItem == null)
            {
                return hitItem == null ? 0 : Math.Max(1, hitItem.Stack);
            }

            var stack = 0;
            for (var index = 0; index < context.WorldItems.Count; index++)
            {
                var item = context.WorldItems[index];
                if (item == null ||
                    !item.IsActive ||
                    item.ItemType != hitItem.ItemType ||
                    Math.Abs(item.TileX - hitItem.TileX) > 1 ||
                    Math.Abs(item.TileY - hitItem.TileY) > 1)
                {
                    continue;
                }

                stack += Math.Max(1, item.Stack);
            }

            return Math.Max(1, stack);
        }

        private static int ResolveAirPhraseIndex(MapQuickAnnouncementResolveContext context)
        {
            var count = MapQuickAnnouncementTextBuilder.AirPhraseCount;
            if (count <= 0)
            {
                return 0;
            }

            if (context != null && context.AirPhraseIndex >= 0)
            {
                return context.AirPhraseIndex % count;
            }

            unchecked
            {
                var seed = 2166136261u;
                if (context != null)
                {
                    seed = Mix(seed, (uint)context.MouseTileX);
                    seed = Mix(seed, (uint)context.MouseTileY);
                    seed = Mix(seed, (uint)context.GameUpdateCount);
                    seed = Mix(seed, (uint)(context.GameUpdateCount >> 32));
                }

                return (int)(seed % (uint)count);
            }
        }

        private static uint Mix(uint seed, uint value)
        {
            unchecked
            {
                seed ^= value;
                seed *= 16777619u;
                return seed;
            }
        }

        private static bool TryBuildCurrentContext(out MapQuickAnnouncementResolveContext context, out string skipReason)
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
            context = new MapQuickAnnouncementResolveContext
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
            AddPlayers(context);
            AddNpcs(context);
            AddWorldItems(context);
            AddTileAndWall(context);
            return true;
        }

        internal static bool TryAddUiHoverItemForTesting(MapQuickAnnouncementResolveContext context)
        {
            return AddUiHoverItem(context);
        }

        private static bool AddUiHoverItem(MapQuickAnnouncementResolveContext context)
        {
            TerrariaUiHoverItemSnapshot snapshot;
            if (context == null ||
                !TerrariaUiMouseCompat.TryReadFreshHoverItemSnapshot(
                    context.GameUpdateCount,
                    context.MouseScreenX,
                    context.MouseScreenY,
                    out snapshot) ||
                snapshot == null)
            {
                return false;
            }

            context.UiItem = new MapQuickAnnouncementItemTarget
            {
                ItemType = snapshot.ItemType,
                Stack = snapshot.Stack,
                Name = MapQuickAnnouncementNameResolver.ResolveItemName(snapshot.ItemType, snapshot.Name),
                HoverSource = NormalizeHoverSource(snapshot.Source),
                HoverContext = snapshot.Context,
                HoverSlot = snapshot.Slot,
                HoverAgeUpdates = ResolveHoverAgeUpdates(context.GameUpdateCount, snapshot.GameUpdateCount)
            };
            return true;
        }

        private static string NormalizeHoverSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "unknown";
            }

            var trimmed = source.Trim();
            var separator = trimmed.IndexOf(':');
            return separator > 0 ? trimmed.Substring(0, separator) : trimmed;
        }

        private static int ResolveHoverAgeUpdates(ulong currentGameUpdateCount, ulong snapshotGameUpdateCount)
        {
            if (currentGameUpdateCount < snapshotGameUpdateCount)
            {
                return -1;
            }

            var age = currentGameUpdateCount - snapshotGameUpdateCount;
            return age > int.MaxValue ? int.MaxValue : (int)age;
        }

        private static void AddPlayers(MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var players = TerrariaMainCompat.Players;
            if (players == null)
            {
                return;
            }

            var localIndex = TerrariaMainCompat.MyPlayerIndex;
            for (var index = 0; index < players.Length; index++)
            {
                var player = players[index];
                if (!TerrariaPlayerReadCompat.IsActive(player) ||
                    TerrariaPlayerReadCompat.IsDead(player) ||
                    TerrariaPlayerReadCompat.IsGhost(player))
                {
                    continue;
                }

                var hitbox = TerrariaPlayerReadCompat.Hitbox(player);
                context.Actors.Add(new MapQuickAnnouncementActorTarget
                {
                    IsPlayer = true,
                    IsLocalPlayer = TerrariaPlayerReadCompat.WhoAmI(player) == localIndex || index == localIndex,
                    WhoAmI = TerrariaPlayerReadCompat.WhoAmI(player),
                    Name = TerrariaPlayerReadCompat.Name(player),
                    Life = TerrariaPlayerReadCompat.CurrentLife(player),
                    LifeMax = TerrariaPlayerReadCompat.MaxLife(player),
                    Mana = TerrariaPlayerReadCompat.CurrentMana(player),
                    ManaMax = TerrariaPlayerReadCompat.MaxMana(player),
                    HitboxX = hitbox.X,
                    HitboxY = hitbox.Y,
                    HitboxWidth = hitbox.Width,
                    HitboxHeight = hitbox.Height
                });
            }
        }

        private static void AddNpcs(MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var npcs = TerrariaMainCompat.Npcs;
            if (npcs == null)
            {
                return;
            }

            for (var index = 0; index < npcs.Length; index++)
            {
                var npc = npcs[index];
                if (!TerrariaNpcReadCompat.IsActive(npc) ||
                    TerrariaNpcReadCompat.IsHidden(npc) ||
                    TerrariaNpcReadCompat.Life(npc) <= 0)
                {
                    continue;
                }

                var hitbox = TerrariaNpcReadCompat.Hitbox(npc);
                var type = TerrariaNpcReadCompat.Type(npc);
                var name = TerrariaNpcReadCompat.Name(npc);
                context.Actors.Add(new MapQuickAnnouncementActorTarget
                {
                    IsPlayer = false,
                    IsTownNpc = TerrariaNpcReadCompat.IsTownNpc(npc),
                    Type = type,
                    WhoAmI = TerrariaNpcReadCompat.WhoAmI(npc),
                    Name = name,
                    TypeName = MapQuickAnnouncementNameResolver.ResolveNpcTypeName(type, name),
                    Life = TerrariaNpcReadCompat.Life(npc),
                    LifeMax = TerrariaNpcReadCompat.LifeMax(npc),
                    HitboxX = hitbox.X,
                    HitboxY = hitbox.Y,
                    HitboxWidth = hitbox.Width,
                    HitboxHeight = hitbox.Height
                });
            }
        }

        private static void AddWorldItems(MapQuickAnnouncementResolveContext context)
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
                MapQuickAnnouncementWorldItemTarget item;
                if (TryReadWorldItem(rawItem, out item))
                {
                    context.WorldItems.Add(item);
                }
            }
        }

        private static bool TryReadWorldItem(object rawItem, out MapQuickAnnouncementWorldItemTarget item)
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
                width = 16;
            }

            if (!InformationReflection.TryReadInt(rawItem, "height", out height))
            {
                height = 16;
            }

            if (width <= 0)
            {
                width = 16;
            }

            if (height <= 0)
            {
                height = 16;
            }

            var centerX = x + width * 0.5f;
            var centerY = y + height * 0.5f;
            var fallbackName = FirstNonEmpty(
                InformationReflection.TryReadString(rawItem, "Name"),
                InformationReflection.TryReadString(rawItem, "HoverName"),
                InformationReflection.TryReadString(rawItem, "name"));
            item = new MapQuickAnnouncementWorldItemTarget
            {
                ItemType = type,
                Stack = stack,
                Name = MapQuickAnnouncementNameResolver.ResolveItemName(type, fallbackName),
                HitboxX = x,
                HitboxY = y,
                HitboxWidth = width,
                HitboxHeight = height,
                TileX = (int)Math.Floor(centerX / TerrariaTileReadCompat.TileSize),
                TileY = (int)Math.Floor(centerY / TerrariaTileReadCompat.TileSize)
            };
            return true;
        }

        private static void AddTileAndWall(MapQuickAnnouncementResolveContext context)
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

            var active = TerrariaTileReadCompat.IsActive(tile);
            var tileType = TerrariaTileReadCompat.Type(tile);
            var frameX = TerrariaTileReadCompat.FrameX(tile);
            var frameY = TerrariaTileReadCompat.FrameY(tile);
            var tileStyle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(tileType, frameX, frameY);
            string tileNameSource = string.Empty;
            var tileName = active
                ? MapQuickAnnouncementNameResolver.ResolveTileName(tileType, tileStyle, string.Empty, out tileNameSource)
                : string.Empty;
            context.Tile = new MapQuickAnnouncementTileTarget
            {
                Active = active,
                TileType = tileType,
                TileStyle = tileStyle,
                FrameX = frameX,
                FrameY = frameY,
                TileName = tileName,
                NameSource = active ? tileNameSource : string.Empty,
                RedWire = TerrariaTileReadCompat.HasRedWire(tile),
                BlueWire = TerrariaTileReadCompat.HasBlueWire(tile),
                GreenWire = TerrariaTileReadCompat.HasGreenWire(tile),
                YellowWire = TerrariaTileReadCompat.HasYellowWire(tile),
                Actuator = TerrariaTileReadCompat.HasActuator(tile),
                LiquidAmount = TerrariaTileReadCompat.LiquidAmount(tile),
                LiquidType = TerrariaTileReadCompat.LiquidType(tile)
            };

            var wallType = TerrariaTileReadCompat.Wall(tile);
            if (wallType > 0)
            {
                string wallNameSource;
                context.Wall = new MapQuickAnnouncementWallTarget
                {
                    Active = true,
                    WallType = wallType,
                    WallName = MapQuickAnnouncementNameResolver.ResolveWallName(wallType, string.Empty, out wallNameSource),
                    NameSource = wallNameSource
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

            var collection = source as System.Collections.ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }
    }
}
