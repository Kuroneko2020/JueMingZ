using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Automation.Information;

namespace JueMingZ.Compat
{
    internal static class AutoMiningCompat
    {
        // Mining helpers prove cursor/tile facts for automation gates only;
        // unproven terrain must make callers yield.
        public const int TileSize = 16;
        private const int VanillaPlayerWidth = 20;
        private const int VanillaPlayerHeight = 42;
        private const int SimpleTileReachLimit = 20;
        private const int DefaultMaxTilesX = 8400;
        private const int DefaultMaxTilesY = 2400;
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<int> FallbackMineableOreTiles = new HashSet<int>
        {
            6, 7, 8, 9, 22, 37, 58, 107, 108, 111, 166, 167, 168, 169, 204, 211, 221, 222, 223,
            407, 408, 56, 404, 123, 224, 63, 64, 65, 66, 67, 68, 178, 566
        };
        private static readonly Dictionary<int, bool> MineableOreCache = new Dictionary<int, bool>();
        private static bool _tileReachSettingsResolved;
        private static object _tileReachSettingsSimple;
        private static MethodInfo _tileReachRegionByRefMethod;
        private static MethodInfo _tileReachRegionRectangleMethod;
        private static bool _worldGenCanKillTileResolved;
        private static MethodInfo _worldGenCanKillTileMethod;

        internal struct MiningReachProfile
        {
            public bool Available;
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public float CenterX;
            public float CenterY;
            public float MaxDistanceWorld;
        }

        private enum AutoMiningReachSource
        {
            Unavailable = 0,
            VanillaTileReachRegion = 1,
            FallbackMiningRange = 2
        }

        public static bool TryGetTileContext(out Type mainType, out object tiles, out int maxTilesX, out int maxTilesY, out string message)
        {
            mainType = null;
            tiles = null;
            maxTilesX = 0;
            maxTilesY = 0;
            message = string.Empty;

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                message = "Terraria runtime types unavailable: " + TerrariaRuntimeTypes.LastError;
                return false;
            }

            mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            tiles = InformationReflection.GetStaticMember(mainType, "tile");
            if (tiles == null)
            {
                message = "Main.tile unavailable.";
                return false;
            }

            if (!InformationReflection.TryReadStaticInt(mainType, "maxTilesX", out maxTilesX) || maxTilesX <= 0)
            {
                maxTilesX = 8400;
            }

            if (!InformationReflection.TryReadStaticInt(mainType, "maxTilesY", out maxTilesY) || maxTilesY <= 0)
            {
                maxTilesY = 2400;
            }

            return true;
        }

        public static bool TryReadCursorTile(out int tileX, out int tileY, out string message)
        {
            tileX = -1;
            tileY = -1;
            message = string.Empty;

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            if (!TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                return false;
            }

            float screenX;
            float screenY;
            if (!TerrariaInputCompat.TryGetScreenPosition(out screenX, out screenY))
            {
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            int mouseX;
            int mouseY;
            if (!InformationReflection.TryReadStaticInt(mainType, "mouseX", out mouseX) ||
                !InformationReflection.TryReadStaticInt(mainType, "mouseY", out mouseY))
            {
                message = "Main.mouseX/Y unavailable.";
                return false;
            }

            tileX = Clamp((int)Math.Floor((screenX + mouseX) / TileSize), 0, maxTilesX - 1);
            tileY = Clamp((int)Math.Floor((screenY + mouseY) / TileSize), 0, maxTilesY - 1);
            return true;
        }

        public static bool TryReadTile(object tiles, int x, int y, out bool active, out int tileType)
        {
            active = false;
            tileType = -1;
            var tile = InformationTileAccess.GetTileAt(tiles, x, y);
            if (tile == null)
            {
                return false;
            }

            active = InformationTileAccess.IsActive(tile);
            tileType = InformationTileAccess.ReadType(tile);
            return true;
        }

        public static bool IsActiveTileOfType(object tiles, int x, int y, int tileType)
        {
            bool active;
            int actualType;
            return TryReadTile(tiles, x, y, out active, out actualType) &&
                   active &&
                   actualType == tileType;
        }

        internal static bool TryReadActiveTileMatchingGroup(
            object tiles,
            int x,
            int y,
            AutoMiningTileMatchGroup matchGroup,
            out int actualType)
        {
            bool active;
            return TryReadTile(tiles, x, y, out active, out actualType) &&
                   active &&
                   matchGroup.Matches(actualType);
        }

        public static bool TryGetCursorMineableOre(out int tileX, out int tileY, out int tileType, out string message)
        {
            tileX = -1;
            tileY = -1;
            tileType = -1;

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            if (!TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                return false;
            }

            if (!TryReadCursorTile(out tileX, out tileY, out message))
            {
                return false;
            }

            bool active;
            if (!TryReadTile(tiles, tileX, tileY, out active, out tileType) || !active)
            {
                message = "cursor tile is not active";
                return false;
            }

            if (!IsMineableOreTileType(tileType))
            {
                message = "cursor tile is not a supported ore or gem";
                return false;
            }

            return true;
        }

        public static bool TryGetSelectedPickaxe(out AutoMiningPickaxeProfile profile, out string message)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                profile = new AutoMiningPickaxeProfile();
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            return TryGetSelectedPickaxe(player, out profile, out message);
        }

        public static bool TryGetSelectedPickaxe(object player, out AutoMiningPickaxeProfile profile, out string message)
        {
            profile = new AutoMiningPickaxeProfile();
            message = string.Empty;
            if (player == null)
            {
                message = "player unavailable";
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                message = "selected item unavailable";
                return false;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) ||
                inventory == null ||
                selectedSlot >= inventory.Count)
            {
                message = string.IsNullOrWhiteSpace(message) ? "inventory unavailable" : message;
                return false;
            }

            var item = inventory[selectedSlot];
            int itemType;
            string itemName;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                message = "selected item fields unavailable";
                return false;
            }

            int pick;
            InformationReflection.TryReadInt(item, "pick", out pick);
            int tileBoost;
            InformationReflection.TryReadInt(item, "tileBoost", out tileBoost);
            profile.SelectedSlot = selectedSlot;
            profile.ItemType = itemType;
            profile.ItemName = itemName ?? string.Empty;
            profile.Stack = stack;
            profile.PickPower = Math.Max(0, pick);
            profile.TileBoost = tileBoost;
            if (!profile.IsUsablePickaxe)
            {
                message = "selected item is not a usable pickaxe";
                return false;
            }

            return true;
        }

        public static bool TryGetSelectedSlot(out int selectedSlot, out string message)
        {
            selectedSlot = -1;
            message = string.Empty;

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                message = "selected item unavailable";
                return false;
            }

            return true;
        }

        public static bool TryGetLocalPlayer(out object player, out string message)
        {
            message = string.Empty;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            return true;
        }

        public static bool TryGetPlayerCenterTile(out int tileX, out int tileY, out string message)
        {
            tileX = -1;
            tileY = -1;
            object player;
            if (!TryGetLocalPlayer(out player, out message))
            {
                return false;
            }

            if (!TerrariaInputCompat.TryGetPlayerCenterTile(player, out tileX, out tileY))
            {
                message = TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            return true;
        }

        public static bool IsTileInMiningReach(object player, int tileX, int tileY)
        {
            return IsTileInMiningReach(player, tileX, tileY, 0);
        }

        public static bool IsTileInMiningReach(object player, int tileX, int tileY, int tileBoost)
        {
            AutoMiningReachSource source;
            return IsTileInMiningReach(player, tileX, tileY, tileBoost, out source);
        }

        private static bool IsTileInMiningReach(object player, int tileX, int tileY, int tileBoost, out AutoMiningReachSource source)
        {
            source = AutoMiningReachSource.Unavailable;
            if (player == null)
            {
                return false;
            }

            int left;
            int top;
            int right;
            int bottom;
            float centerX;
            float centerY;
            float maxDistanceWorld;
            return TryGetMiningReachProfile(
                       player,
                       tileBoost,
                       out left,
                       out top,
                       out right,
                       out bottom,
                       out centerX,
                       out centerY,
                       out maxDistanceWorld,
                       out source) &&
                   (source == AutoMiningReachSource.VanillaTileReachRegion
                       ? IsTileInsideReachRegion(tileX, tileY, left, top, right, bottom)
                       : IsTileInsideReachShape(tileX, tileY, left, top, right, bottom, centerX, centerY, maxDistanceWorld));
        }

        public static bool CanMineTileWithPickaxe(object player, int tileX, int tileY, int tileType, int pickPower, int tileBoost)
        {
            MiningReachProfile reachProfile;
            if (!TryBuildMiningTakeoverReachProfile(player, tileBoost, out reachProfile))
            {
                return false;
            }

            return CanMineTileWithPickaxe(reachProfile, tileX, tileY, tileType, pickPower);
        }

        internal static bool CanMineTileWithPickaxe(MiningReachProfile reachProfile, int tileX, int tileY, int tileType, int pickPower)
        {
            // Auto-mining takeover is stricter than generic interaction reach: a vanilla reach
            // rectangle edge can still produce an ItemCheck swing that does not damage the tile.
            return pickPower > 0 &&
                   IsTileInsideMiningTakeoverReach(reachProfile, tileX, tileY) &&
                   IsPickPowerSufficientForTile(tileType, tileY, pickPower) &&
                   CanKillTile(tileX, tileY);
        }

        public static bool CanKillTile(int tileX, int tileY)
        {
            MethodInfo method;
            if (!TryResolveWorldGenCanKillTile(out method))
            {
                return true;
            }

            object raw;
            try
            {
                raw = method.Invoke(null, new object[] { tileX, tileY });
                if (raw != null)
                {
                    return Convert.ToBoolean(raw);
                }
            }
            catch
            {
            }

            return true;
        }

        private static bool TryResolveWorldGenCanKillTile(out MethodInfo method)
        {
            lock (SyncRoot)
            {
                if (!_worldGenCanKillTileResolved)
                {
                    _worldGenCanKillTileResolved = true;
                    var worldGenType = InformationReflection.FindType("Terraria.WorldGen");
                    if (worldGenType != null)
                    {
                        var methods = worldGenType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        for (var index = 0; index < methods.Length; index++)
                        {
                            var candidate = methods[index];
                            var parameters = candidate.GetParameters();
                            if (string.Equals(candidate.Name, "CanKillTile", StringComparison.Ordinal) &&
                                candidate.ReturnType == typeof(bool) &&
                                parameters.Length == 2 &&
                                parameters[0].ParameterType == typeof(int) &&
                                parameters[1].ParameterType == typeof(int))
                            {
                                _worldGenCanKillTileMethod = candidate;
                                break;
                            }
                        }
                    }
                }

                method = _worldGenCanKillTileMethod;
            }

            return method != null;
        }

        private static bool IsTileInStrictMiningTakeoverReach(object player, int tileX, int tileY, int tileBoost)
        {
            MiningReachProfile reachProfile;
            return TryBuildMiningTakeoverReachProfile(player, tileBoost, out reachProfile) &&
                   IsTileInsideMiningTakeoverReach(reachProfile, tileX, tileY);
        }

        internal static bool TryBuildMiningTakeoverReachProfile(object player, int tileBoost, out MiningReachProfile reachProfile)
        {
            reachProfile = new MiningReachProfile();
            if (player == null)
            {
                return false;
            }

            int left;
            int top;
            int right;
            int bottom;
            float centerX;
            float centerY;
            float maxDistanceWorld;
            AutoMiningReachSource source;
            if (!TryGetMiningReachProfile(
                    player,
                    tileBoost,
                    out left,
                    out top,
                    out right,
                    out bottom,
                    out centerX,
                    out centerY,
                    out maxDistanceWorld,
                    out source))
            {
                return false;
            }

            reachProfile.Available = true;
            reachProfile.Left = left;
            reachProfile.Top = top;
            reachProfile.Right = right;
            reachProfile.Bottom = bottom;
            reachProfile.CenterX = centerX;
            reachProfile.CenterY = centerY;
            reachProfile.MaxDistanceWorld = maxDistanceWorld;
            return true;
        }

        internal static bool IsTileInsideMiningTakeoverReach(MiningReachProfile reachProfile, int tileX, int tileY)
        {
            return reachProfile.Available &&
                   IsTileInsideReachShape(
                       tileX,
                       tileY,
                       reachProfile.Left,
                       reachProfile.Top,
                       reachProfile.Right,
                       reachProfile.Bottom,
                       reachProfile.CenterX,
                       reachProfile.CenterY,
                       reachProfile.MaxDistanceWorld);
        }

        public static bool IsMineableOreTileType(int tileType)
        {
            if (tileType < 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                bool cached;
                if (MineableOreCache.TryGetValue(tileType, out cached))
                {
                    return cached;
                }

                bool value;
                if (TryReadTileSetBool("Ore", tileType, out value) && value)
                {
                    MineableOreCache[tileType] = true;
                    return true;
                }

                if (TryReadTileSetBool("Gems", tileType, out value) && value)
                {
                    MineableOreCache[tileType] = true;
                    return true;
                }

                var fallback = FallbackMineableOreTiles.Contains(tileType);
                MineableOreCache[tileType] = fallback;
                return fallback;
            }
        }

        public static bool IsGravityAffectedMiningTileType(int tileType)
        {
            return tileType == 123 ||
                   tileType == 224;
        }

        public static long ReadGameUpdateCount()
        {
            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly() || TerrariaRuntimeTypes.MainType == null)
            {
                return 0;
            }

            ulong updateCount;
            if (InformationReflection.TryReadStaticULong(TerrariaRuntimeTypes.MainType, "GameUpdateCount", out updateCount))
            {
                return updateCount > long.MaxValue ? long.MaxValue : (long)updateCount;
            }

            int updateCountInt;
            return InformationReflection.TryReadStaticInt(TerrariaRuntimeTypes.MainType, "GameUpdateCount", out updateCountInt)
                ? updateCountInt
                : 0;
        }

        internal static bool IsTileInsideReachShapeForTesting(
            int tileX,
            int tileY,
            int left,
            int top,
            int right,
            int bottom,
            float centerX,
            float centerY,
            float maxDistanceWorld)
        {
            return IsTileInsideReachShape(tileX, tileY, left, top, right, bottom, centerX, centerY, maxDistanceWorld);
        }

        internal static bool IsTileInMiningReachForTesting(object player, int tileX, int tileY, int tileBoost, out string source)
        {
            AutoMiningReachSource reachSource;
            var reachable = IsTileInMiningReach(player, tileX, tileY, tileBoost, out reachSource);
            source = FormatReachSource(reachSource);
            return reachable;
        }

        internal static bool TryGetVanillaTileReachRegionForTesting(
            object player,
            int tileBoost,
            int maxTilesX,
            int maxTilesY,
            out int left,
            out int top,
            out int right,
            out int bottom)
        {
            return TryGetVanillaTileReachRegion(player, tileBoost, maxTilesX, maxTilesY, out left, out top, out right, out bottom);
        }

        internal static bool IsPickPowerSufficientForTileForTesting(int tileType, int tileY, int pickPower)
        {
            return IsPickPowerSufficientForTile(tileType, tileY, pickPower);
        }

        internal static bool TryGetMiningCenterWorldForTesting(object player, out float centerX, out float centerY)
        {
            return TryGetMiningCenterWorld(player, out centerX, out centerY);
        }

        public static bool TryGetMiningCenterWorld(object player, out float centerX, out float centerY)
        {
            centerX = 0f;
            centerY = 0f;

            int left;
            int top;
            int right;
            int bottom;
            float maxDistanceWorld;
            return TryGetMiningReachProfile(
                player,
                0,
                out left,
                out top,
                out right,
                out bottom,
                out centerX,
                out centerY,
                out maxDistanceWorld);
        }

        private static bool TryGetMiningReachProfile(
            object player,
            int tileBoost,
            out int left,
            out int top,
            out int right,
            out int bottom,
            out float centerX,
            out float centerY,
            out float maxDistanceWorld)
        {
            AutoMiningReachSource source;
            return TryGetMiningReachProfile(
                player,
                tileBoost,
                out left,
                out top,
                out right,
                out bottom,
                out centerX,
                out centerY,
                out maxDistanceWorld,
                out source);
        }

        private static bool TryGetMiningReachProfile(
            object player,
            int tileBoost,
            out int left,
            out int top,
            out int right,
            out int bottom,
            out float centerX,
            out float centerY,
            out float maxDistanceWorld,
            out AutoMiningReachSource source)
        {
            left = 0;
            top = 0;
            right = -1;
            bottom = -1;
            centerX = 0f;
            centerY = 0f;
            maxDistanceWorld = 0f;
            source = AutoMiningReachSource.Unavailable;
            if (player == null)
            {
                return false;
            }

            // Preserve vanilla item.tileBoost exactly; negative boosts intentionally shrink mining reach.
            int maxTilesX;
            int maxTilesY;
            TryReadWorldTileBounds(out maxTilesX, out maxTilesY);
            if (TryGetVanillaTileReachRegion(player, tileBoost, maxTilesX, maxTilesY, out left, out top, out right, out bottom))
            {
                float rawPositionX;
                float rawPositionY;
                int rawWidth;
                int rawHeight;
                if (TryReadPlayerBounds(player, out rawPositionX, out rawPositionY, out rawWidth, out rawHeight))
                {
                    centerX = rawPositionX + rawWidth / 2f;
                    centerY = rawPositionY + rawHeight / 2f;
                }
                else
                {
                    centerX = (left + right + 1) * TileSize / 2f;
                    centerY = (top + bottom + 1) * TileSize / 2f;
                }

                int vanillaRangeX;
                int vanillaRangeY;
                TryReadMiningRange(out vanillaRangeX, out vanillaRangeY);
                maxDistanceWorld = Math.Max(vanillaRangeX + tileBoost, vanillaRangeY + tileBoost) * TileSize;
                source = AutoMiningReachSource.VanillaTileReachRegion;
                return true;
            }

            // Fallback is compatibility-only when vanilla reach APIs are unavailable; it must stay conservative and never expand auto-mining range.
            float positionX;
            float positionY;
            int width;
            int height;
            if (!TryGetMiningPlayerBounds(player, out positionX, out positionY, out width, out height))
            {
                return false;
            }

            int rangeX;
            int rangeY;
            TryReadMiningRange(out rangeX, out rangeY);
            var extraX = rangeX + tileBoost;
            var extraY = rangeY + tileBoost;

            left = (int)Math.Floor(positionX / TileSize) - extraX;
            right = (int)Math.Ceiling((positionX + width) / TileSize) - 1 + extraX;
            top = (int)Math.Floor(positionY / TileSize) - extraY;
            bottom = (int)Math.Ceiling((positionY + height) / TileSize) - 1 + extraY;
            centerX = positionX + width / 2f;
            centerY = positionY + height / 2f;
            maxDistanceWorld = Math.Max(extraX, extraY) * TileSize;
            source = AutoMiningReachSource.FallbackMiningRange;
            return true;
        }

        private static bool TryGetVanillaTileReachRegion(
            object player,
            int tileBoost,
            int maxTilesX,
            int maxTilesY,
            out int left,
            out int top,
            out int right,
            out int bottom)
        {
            left = 0;
            top = 0;
            right = -1;
            bottom = -1;
            if (player == null)
            {
                return false;
            }

            object simple;
            MethodInfo byRefMethod;
            MethodInfo rectangleMethod;
            if (!TryResolveTileReachRegionMembers(out simple, out byRefMethod, out rectangleMethod))
            {
                return false;
            }

            if (TryInvokeTileReachRegionByRef(simple, byRefMethod, player, tileBoost, out left, out top, out right, out bottom) ||
                TryInvokeTileReachRegionRectangle(simple, rectangleMethod, player, tileBoost, out left, out top, out right, out bottom))
            {
                maxTilesX = maxTilesX <= 0 ? DefaultMaxTilesX : maxTilesX;
                maxTilesY = maxTilesY <= 0 ? DefaultMaxTilesY : maxTilesY;
                left = Clamp(left, 0, maxTilesX - 1);
                right = Clamp(right, 0, maxTilesX - 1);
                top = Clamp(top, 0, maxTilesY - 1);
                bottom = Clamp(bottom, 0, maxTilesY - 1);
                return right >= left && bottom >= top;
            }

            return false;
        }

        private static bool TryResolveTileReachRegionMembers(out object simple, out MethodInfo byRefMethod, out MethodInfo rectangleMethod)
        {
            lock (SyncRoot)
            {
                if (!_tileReachSettingsResolved)
                {
                    _tileReachSettingsResolved = true;
                    var settingsType = TerrariaTypeCache.Find("Terraria.DataStructures.TileReachCheckSettings");
                    if (settingsType != null)
                    {
                        _tileReachSettingsSimple = InformationReflection.GetStaticMember(settingsType, "Simple");
                        var methods = settingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        for (var index = 0; index < methods.Length; index++)
                        {
                            var method = methods[index];
                            if (_tileReachRegionByRefMethod == null && IsTileReachRegionByRefMethod(method))
                            {
                                _tileReachRegionByRefMethod = method;
                            }
                            else if (_tileReachRegionRectangleMethod == null && IsTileReachRegionRectangleMethod(method))
                            {
                                _tileReachRegionRectangleMethod = method;
                            }
                        }
                    }
                }

                simple = _tileReachSettingsSimple;
                byRefMethod = _tileReachRegionByRefMethod;
                rectangleMethod = _tileReachRegionRectangleMethod;
            }

            return simple != null && (byRefMethod != null || rectangleMethod != null);
        }

        private static bool TryInvokeTileReachRegionByRef(
            object simple,
            MethodInfo method,
            object player,
            int tileBoost,
            out int left,
            out int top,
            out int right,
            out int bottom)
        {
            left = 0;
            top = 0;
            right = -1;
            bottom = -1;
            if (!CanInvokeTileReachRegionMethod(method, player))
            {
                return false;
            }

            try
            {
                var args = new object[] { player, 0, 0, 0, 0, tileBoost };
                method.Invoke(simple, args);
                left = Convert.ToInt32(args[1]);
                top = Convert.ToInt32(args[2]);
                right = Convert.ToInt32(args[3]);
                bottom = Convert.ToInt32(args[4]);
                return right >= left && bottom >= top;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeTileReachRegionRectangle(
            object simple,
            MethodInfo method,
            object player,
            int tileBoost,
            out int left,
            out int top,
            out int right,
            out int bottom)
        {
            left = 0;
            top = 0;
            right = -1;
            bottom = -1;
            if (!CanInvokeTileReachRegionMethod(method, player))
            {
                return false;
            }

            try
            {
                var region = method.Invoke(simple, new object[] { player, tileBoost });
                int x;
                int y;
                int width;
                int height;
                if (!InformationReflection.TryReadRectangle(region, out x, out y, out width, out height) || width <= 0 || height <= 0)
                {
                    return false;
                }

                left = x;
                top = y;
                right = x + width;
                bottom = y + height;
                return right >= left && bottom >= top;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTileReachRegionByRefMethod(MethodInfo method)
        {
            if (method == null ||
                method.IsStatic ||
                method.ReturnType != typeof(void) ||
                !string.Equals(method.Name, "GetTileRegion", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 6 &&
                   parameters[1].ParameterType.IsByRef &&
                   parameters[2].ParameterType.IsByRef &&
                   parameters[3].ParameterType.IsByRef &&
                   parameters[4].ParameterType.IsByRef &&
                   parameters[5].ParameterType == typeof(int);
        }

        private static bool IsTileReachRegionRectangleMethod(MethodInfo method)
        {
            if (method == null ||
                method.IsStatic ||
                method.ReturnType == typeof(void) ||
                !string.Equals(method.Name, "GetTileRegion", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 2 &&
                   parameters[1].ParameterType == typeof(int);
        }

        private static bool CanInvokeTileReachRegionMethod(MethodInfo method, object player)
        {
            if (method == null || player == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length > 0 &&
                   parameters[0].ParameterType.IsAssignableFrom(player.GetType());
        }

        private static void TryReadWorldTileBounds(out int maxTilesX, out int maxTilesY)
        {
            maxTilesX = DefaultMaxTilesX;
            maxTilesY = DefaultMaxTilesY;
            var mainType = InformationReflection.FindType("Terraria.Main");
            int value;
            if (InformationReflection.TryReadStaticInt(mainType, "maxTilesX", out value) && value > 0)
            {
                maxTilesX = value;
            }

            if (InformationReflection.TryReadStaticInt(mainType, "maxTilesY", out value) && value > 0)
            {
                maxTilesY = value;
            }
        }

        private static bool TryGetMiningPlayerBounds(object player, out float positionX, out float positionY, out int width, out int height)
        {
            if (!TryReadPlayerBounds(player, out positionX, out positionY, out width, out height))
            {
                return false;
            }

            if (IsPlayerMounted(player))
            {
                // Terraria expands player.height for mounts; fallback reach keeps using the rider body so it cannot expand mining reach.
                var normalizedCenterX = positionX + width / 2f;
                var bottomY = positionY + height;
                width = VanillaPlayerWidth;
                height = VanillaPlayerHeight;
                positionX = normalizedCenterX - width / 2f;
                positionY = bottomY - height;
            }

            return true;
        }

        private static bool TryReadPlayerBounds(object player, out float positionX, out float positionY, out int width, out int height)
        {
            positionX = 0f;
            positionY = 0f;
            width = VanillaPlayerWidth;
            height = VanillaPlayerHeight;
            if (!InformationReflection.TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                return false;
            }

            if (!InformationReflection.TryReadInt(player, "width", out width) || width <= 0)
            {
                width = VanillaPlayerWidth;
            }

            if (!InformationReflection.TryReadInt(player, "height", out height) || height <= 0)
            {
                height = VanillaPlayerHeight;
            }

            return true;
        }

        private static void TryReadMiningRange(out int rangeX, out int rangeY)
        {
            rangeX = 5;
            rangeY = 4;
            var playerType = TerrariaRuntimeTypes.PlayerType;
            int value;
            if (playerType != null && InformationReflection.TryReadStaticInt(playerType, "tileRangeX", out value) && value > 0)
            {
                rangeX = Clamp(value, 1, SimpleTileReachLimit);
            }

            if (playerType != null && InformationReflection.TryReadStaticInt(playerType, "tileRangeY", out value) && value > 0)
            {
                rangeY = Clamp(value, 1, SimpleTileReachLimit);
            }
        }

        private static bool IsPlayerMounted(object player)
        {
            var mount = InformationReflection.GetMember(player, "mount");
            bool active;
            return mount != null &&
                   InformationReflection.TryReadBool(mount, "Active", out active) &&
                   active;
        }

        private static bool IsTileInsideReachShape(
            int tileX,
            int tileY,
            int left,
            int top,
            int right,
            int bottom,
            float centerX,
            float centerY,
            float maxDistanceWorld)
        {
            if (tileX < left ||
                tileX > right ||
                tileY < top ||
                tileY > bottom ||
                maxDistanceWorld <= 0f)
            {
                return false;
            }

            var tileCenterX = tileX * TileSize + TileSize / 2f;
            var tileCenterY = tileY * TileSize + TileSize / 2f;
            var dx = tileCenterX - centerX;
            var dy = tileCenterY - centerY;
            var maxDistanceSquared = maxDistanceWorld * maxDistanceWorld;
            return dx * dx + dy * dy <= maxDistanceSquared + 0.01f;
        }

        private static bool IsTileInsideReachRegion(int tileX, int tileY, int left, int top, int right, int bottom)
        {
            return tileX >= left &&
                   tileX <= right &&
                   tileY >= top &&
                   tileY <= bottom;
        }

        private static string FormatReachSource(AutoMiningReachSource source)
        {
            switch (source)
            {
                case AutoMiningReachSource.VanillaTileReachRegion:
                    return "vanillaTileReachRegion";
                case AutoMiningReachSource.FallbackMiningRange:
                    return "fallbackMiningRange";
                default:
                    return "unavailable";
            }
        }

        internal static bool IsPickPowerSufficientForTile(int tileType, int tileY, int pickPower)
        {
            if (tileType < 0 || pickPower <= 0)
            {
                return false;
            }

            return pickPower >= GetRequiredPickPower(tileType, tileY);
        }

        private static int GetRequiredPickPower(int tileType, int tileY)
        {
            switch (tileType)
            {
                case 22:
                case 204:
                    return 55;
                case 37:
                    return 50;
                case 56:
                    return 55;
                case 58:
                    return 65;
                case 107:
                case 221:
                    return 100;
                case 108:
                case 222:
                    return 110;
                case 111:
                case 223:
                    return 150;
                case 211:
                    return 200;
                default:
                    return 0;
            }
        }

        private static bool TryReadTileSetBool(string memberName, int tileType, out bool value)
        {
            value = false;
            var setsType = InformationReflection.FindType("Terraria.ID.TileID+Sets");
            var set = InformationReflection.GetStaticMember(setsType, memberName);
            var raw = InformationReflection.GetIndexedValue(set, tileType);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
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
