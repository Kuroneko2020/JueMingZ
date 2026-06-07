using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Automation.Fishing;

namespace JueMingZ.Compat
{
    internal static class TerrariaFishingCompat
    {
        // Fishing condition reads are cached observations for decisions; missing
        // members must not trigger rod or inventory actions.
        private const int FallbackTruffleWormItemType = 2673;
        private static readonly object BaitSyncRoot = new object();
        private static Type _getFishingConditionsPlayerType;
        private static MethodInfo _getFishingConditionsMethod;
        private static bool _truffleWormItemTypeResolved;
        private static int _truffleWormItemType = FallbackTruffleWormItemType;
        private static long _truffleWormQueryCountForTesting;

        public static bool TryReadBobberObservation(object projectile, out FishingBobberObservation observation)
        {
            observation = null;
            if (projectile == null || !TerrariaMemberCache.EnsureInitializedLateOnly())
            {
                return false;
            }

            bool active;
            bool bobber;
            int owner;
            if (!TryReadBool(projectile, "active", out active) ||
                !TryReadBool(projectile, "bobber", out bobber) ||
                !TryReadInt(projectile, "owner", out owner))
            {
                return false;
            }

            int myPlayer;
            if (!TryReadStaticInt(TerrariaRuntimeTypes.MainType, "myPlayer", out myPlayer))
            {
                return false;
            }

            if (!active || !bobber || owner != myPlayer)
            {
                return false;
            }

            var identity = ReadInt(projectile, "identity", -1);
            var whoAmI = ReadInt(projectile, "whoAmI", -1);
            var type = ReadInt(projectile, "type", 0);
            var ai1 = ReadFloatIndexed(GetMember(projectile, "ai"), 1, 0f);
            var localAi1 = ReadFloatIndexed(GetMember(projectile, "localAI"), 1, 0f);
            float centerX;
            float centerY;
            if (!TryReadVector(GetMember(projectile, "Center"), out centerX, out centerY))
            {
                centerX = ReadFloat(GetMember(projectile, "position"), "X", 0f);
                centerY = ReadFloat(GetMember(projectile, "position"), "Y", 0f);
            }

            bool inLiquid;
            FishingLiquidKind liquidKind;
            var liquidStateKnown = TryReadProjectileLiquidState(projectile, centerX, centerY, out inLiquid, out liquidKind);
            long gameUpdateCount;
            TryReadGameUpdateCount(out gameUpdateCount);
            observation = new FishingBobberObservation
            {
                GameUpdateCount = gameUpdateCount,
                Identity = identity,
                WhoAmI = whoAmI,
                Type = type,
                Owner = owner,
                Active = active,
                Bobber = bobber,
                InLiquid = liquidStateKnown && inLiquid,
                LiquidStateKnown = liquidStateKnown,
                LiquidKind = liquidStateKnown && inLiquid ? liquidKind : FishingLiquidKind.Unknown,
                Ai1 = ai1,
                LocalAi1 = localAi1,
                CenterX = centerX,
                CenterY = centerY
            };
            return true;
        }

        public static bool TryScanLocalBobbers(out List<FishingBobberObservation> observations)
        {
            observations = new List<FishingBobberObservation>();
            var projectiles = GetStatic(TerrariaRuntimeTypes.MainType, "projectile");
            if (projectiles == null)
            {
                return false;
            }

            try
            {
                var count = GetCollectionCount(projectiles);
                for (var index = 0; index < count; index++)
                {
                    FishingBobberObservation observation;
                    if (TryReadBobberObservation(GetIndexed(projectiles, index), out observation))
                    {
                        observations.Add(observation);
                    }
                }

                return true;
            }
            catch
            {
                observations.Clear();
                return false;
            }
        }

        public static bool TryIsLocalBobberGone(int identity, out bool gone)
        {
            gone = false;
            if (identity < 0)
            {
                gone = true;
                return true;
            }

            List<FishingBobberObservation> observations;
            if (TryScanLocalBobbers(out observations))
            {
                FishingBobberObserver.RemoveMissing(observations);
                for (var index = 0; index < observations.Count; index++)
                {
                    if (observations[index] != null && observations[index].Identity == identity)
                    {
                        gone = false;
                        return true;
                    }
                }

                gone = true;
                return true;
            }

            FishingBobberObservation observation;
            if (FishingBobberObserver.TryGetByIdentity(identity, out observation) && observation != null)
            {
                long currentTick;
                if (TryReadGameUpdateCount(out currentTick))
                {
                    gone = currentTick - observation.GameUpdateCount > 5;
                    return true;
                }
            }

            return false;
        }

        public static bool TryReadSelectedFishingPole(out int selectedSlot, out int itemType, out int fishingPole)
        {
            selectedSlot = -1;
            itemType = 0;
            fishingPole = 0;

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                return false;
            }

            var inventory = GetMember(player, "inventory");
            var item = GetIndexed(inventory, selectedSlot);
            if (item == null)
            {
                return false;
            }

            var stack = ReadInt(item, "stack", 0);
            itemType = ReadInt(item, "type", 0);
            fishingPole = ReadInt(item, "fishingPole", 0);
            return itemType > 0 && stack > 0 && fishingPole > 0;
        }

        public static bool TryIsCurrentBaitTruffleWorm(out bool isTruffleWorm, out int baitItemType)
        {
            System.Threading.Interlocked.Increment(ref _truffleWormQueryCountForTesting);
            isTruffleWorm = false;
            baitItemType = 0;

            int baitPower;
            if (!TryReadCurrentBait(out baitItemType, out baitPower))
            {
                return false;
            }

            isTruffleWorm = baitItemType == ResolveTruffleWormItemType();
            return baitItemType > 0;
        }

        internal static long TruffleWormQueryCountForTesting
        {
            get { return System.Threading.Interlocked.Read(ref _truffleWormQueryCountForTesting); }
        }

        internal static void ResetTruffleWormQueryCountForTesting()
        {
            System.Threading.Interlocked.Exchange(ref _truffleWormQueryCountForTesting, 0);
        }

        public static bool TryReadCurrentBait(out int baitItemType, out int baitPower)
        {
            baitItemType = 0;
            baitPower = 0;

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            object conditions;
            if (TryInvokeGetFishingConditions(player, out conditions) && conditions != null)
            {
                baitItemType = ReadInt(conditions, "BaitItemType", 0);
                baitPower = ReadInt(conditions, "BaitPower", 0);
                if (baitItemType > 0)
                {
                    return true;
                }
            }

            var mouseItem = GetStatic(TerrariaRuntimeTypes.MainType, "mouseItem");
            if (TryReadBait(mouseItem, out baitItemType, out baitPower))
            {
                return true;
            }

            var inventory = GetMember(player, "inventory");
            var count = GetCollectionCount(inventory);
            for (var slot = 54; slot < count && slot < 58; slot++)
            {
                if (TryReadBait(GetIndexed(inventory, slot), out baitItemType, out baitPower))
                {
                    return true;
                }
            }

            for (var slot = 0; slot < count && slot < 50; slot++)
            {
                if (TryReadBait(GetIndexed(inventory, slot), out baitItemType, out baitPower))
                {
                    return true;
                }
            }

            baitItemType = 0;
            baitPower = 0;
            return false;
        }

        public static bool TryReadHotbarSlotInfo(int slot, out int itemType, out int stack, out int fishingPole)
        {
            itemType = 0;
            stack = 0;
            fishingPole = 0;
            if (slot < 0 || slot > 9)
            {
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            var inventory = GetMember(player, "inventory");
            var item = GetIndexed(inventory, slot);
            if (item == null)
            {
                return false;
            }

            stack = ReadInt(item, "stack", 0);
            itemType = ReadInt(item, "type", 0);
            fishingPole = ReadInt(item, "fishingPole", 0);
            return true;
        }

        public static bool TryReadMouseWorld(out float worldX, out float worldY)
        {
            worldX = 0f;
            worldY = 0f;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return false;
            }

            if (TryReadVector(GetStatic(mainType, "MouseWorld"), out worldX, out worldY))
            {
                return true;
            }

            int mouseX;
            int mouseY;
            if (!TryReadStaticInt(mainType, "mouseX", out mouseX) ||
                !TryReadStaticInt(mainType, "mouseY", out mouseY))
            {
                return false;
            }

            float screenX;
            float screenY;
            if (!TryReadVector(GetStatic(mainType, "screenPosition"), out screenX, out screenY))
            {
                return false;
            }

            worldX = screenX + mouseX;
            worldY = screenY + mouseY;
            return true;
        }

        public static bool TryReadGameUpdateCount(out long count)
        {
            count = 0;
            try
            {
                object raw = GetStatic(TerrariaRuntimeTypes.MainType, "GameUpdateCount");
                if (raw == null)
                {
                    raw = GetStatic(TerrariaRuntimeTypes.MainType, "gameUpdateCount");
                }

                if (raw == null)
                {
                    return false;
                }

                count = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                count = 0;
                return false;
            }
        }

        private static bool TryInvokeGetFishingConditions(object player, out object conditions)
        {
            conditions = null;
            if (player == null)
            {
                return false;
            }

            // Cache GetFishingConditions per Player type; unavailable reflection
            // must let bait reads fall back or skip instead of guessing state.
            var playerType = player.GetType();
            MethodInfo method;
            lock (BaitSyncRoot)
            {
                if (_getFishingConditionsPlayerType != playerType)
                {
                    _getFishingConditionsPlayerType = playerType;
                    _getFishingConditionsMethod = playerType.GetMethod(
                        "GetFishingConditions",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null);
                }

                method = _getFishingConditionsMethod;
            }

            if (method == null)
            {
                return false;
            }

            try
            {
                conditions = method.Invoke(player, null);
                return conditions != null;
            }
            catch
            {
                conditions = null;
                return false;
            }
        }

        private static bool TryReadBait(object item, out int itemType, out int baitPower)
        {
            itemType = 0;
            baitPower = 0;
            if (item == null || ReadInt(item, "stack", 0) <= 0)
            {
                return false;
            }

            baitPower = ReadInt(item, "bait", 0);
            itemType = ReadInt(item, "type", 0);
            return itemType > 0 && baitPower > 0;
        }

        private static int ResolveTruffleWormItemType()
        {
            lock (BaitSyncRoot)
            {
                // Resolve ItemID.TruffleWorm once; the fallback is a stable
                // compatibility constant, not a per-frame reflection probe.
                if (_truffleWormItemTypeResolved)
                {
                    return _truffleWormItemType;
                }

                _truffleWormItemTypeResolved = true;
                var itemIdType = FindType("Terraria.ID.ItemID");
                int resolved;
                _truffleWormItemType = TryReadStaticInt(itemIdType, "TruffleWorm", out resolved) && resolved > 0
                    ? resolved
                    : FallbackTruffleWormItemType;
                return _truffleWormItemType;
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
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

            return TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanRead
                ? property.GetValue(null, null)
                : null;
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

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanRead
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool TryReadBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
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

        private static bool TryReadStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
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

        private static bool TryInvokeInt(object instance, string name, out int value)
        {
            value = 0;
            if (instance == null)
            {
                return false;
            }

            var method = instance.GetType().GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(method.Invoke(instance, null), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInvokeBool(object instance, string name, out bool value)
        {
            value = false;
            if (instance == null)
            {
                return false;
            }

            var method = instance.GetType().GetMethod(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (method == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(method.Invoke(instance, null), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            int value;
            return TryReadInt(instance, name, out value) ? value : fallback;
        }

        private static float ReadFloat(object instance, string name, float fallback)
        {
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadFloatIndexed(object source, int index, float fallback)
        {
            var raw = GetIndexed(source, index);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static object GetIndexed(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            var list = source as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 && index < array.GetLength(0)
                ? array.GetValue(index)
                : null;
        }

        private static object GetIndexed2D(object source, int x, int y)
        {
            if (source == null || x < 0 || y < 0)
            {
                return null;
            }

            var array = source as Array;
            if (array != null)
            {
                if (array.Rank == 2 &&
                    x < array.GetLength(0) &&
                    y < array.GetLength(1))
                {
                    return array.GetValue(x, y);
                }

                if (array.Rank == 1 && x < array.GetLength(0))
                {
                    return GetIndexed(array.GetValue(x), y);
                }
            }

            return GetIndexed(GetIndexed(source, x), y);
        }

        private static int GetCollectionCount(object source)
        {
            var list = source as IList;
            if (list != null)
            {
                return list.Count;
            }

            var array = source as Array;
            return array == null || array.Rank != 1 ? 0 : array.GetLength(0);
        }

        private static bool TryReadVector(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            var rawX = GetMember(vector, "X");
            var rawY = GetMember(vector, "Y");
            if (rawX == null || rawY == null)
            {
                return false;
            }

            try
            {
                x = Convert.ToSingle(rawX, CultureInfo.InvariantCulture);
                y = Convert.ToSingle(rawY, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadProjectileLiquidState(object projectile, float centerX, float centerY, out bool inLiquid, out FishingLiquidKind liquidKind)
        {
            inLiquid = false;
            liquidKind = FishingLiquidKind.Unknown;
            bool value;
            var known = false;
            if (TryReadBool(projectile, "wet", out value))
            {
                known = true;
                if (value)
                {
                    inLiquid = true;
                    liquidKind = FishingLiquidKind.Water;
                }
            }

            if (TryReadBool(projectile, "lavaWet", out value))
            {
                known = true;
                if (value)
                {
                    inLiquid = true;
                    liquidKind = FishingLiquidKind.Lava;
                }
            }

            if (TryReadBool(projectile, "honeyWet", out value))
            {
                known = true;
                if (value && liquidKind != FishingLiquidKind.Lava)
                {
                    inLiquid = true;
                    liquidKind = FishingLiquidKind.Honey;
                }
            }

            if (TryReadBool(projectile, "shimmerWet", out value))
            {
                known = true;
                if (value && liquidKind != FishingLiquidKind.Lava && liquidKind != FishingLiquidKind.Honey)
                {
                    inLiquid = true;
                    liquidKind = FishingLiquidKind.Shimmer;
                }
            }

            bool tileLiquid;
            FishingLiquidKind tileLiquidKind;
            if (TryReadTileLiquidAtWorld(centerX, centerY, out tileLiquid, out tileLiquidKind))
            {
                known = true;
                if (tileLiquid && !inLiquid)
                {
                    inLiquid = true;
                    liquidKind = tileLiquidKind;
                }
            }

            return known;
        }

        private static bool TryReadTileLiquidAtWorld(float worldX, float worldY, out bool inLiquid, out FishingLiquidKind liquidKind)
        {
            inLiquid = false;
            liquidKind = FishingLiquidKind.Unknown;
            var mainType = TerrariaRuntimeTypes.MainType;
            var tiles = GetStatic(mainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            var tileX = (int)Math.Floor(worldX / 16f);
            var tileY = (int)Math.Floor(worldY / 16f);
            var tile = GetIndexed2D(tiles, tileX, tileY);
            if (tile == null)
            {
                return false;
            }

            int liquid;
            if (!TryReadInt(tile, "liquid", out liquid) &&
                !TryReadInt(tile, "LiquidAmount", out liquid))
            {
                return false;
            }

            inLiquid = liquid > 0;
            liquidKind = inLiquid ? ReadTileLiquidKind(tile) : FishingLiquidKind.Unknown;
            return true;
        }

        private static FishingLiquidKind ReadTileLiquidKind(object tile)
        {
            int liquidType;
            if (TryReadInt(tile, "LiquidType", out liquidType) ||
                TryReadInt(tile, "liquidType", out liquidType) ||
                TryInvokeInt(tile, "liquidType", out liquidType))
            {
                return ToLiquidKind(liquidType);
            }

            bool liquidFlag;
            if (TryInvokeBool(tile, "lava", out liquidFlag) && liquidFlag)
            {
                return FishingLiquidKind.Lava;
            }

            if (TryInvokeBool(tile, "honey", out liquidFlag) && liquidFlag)
            {
                return FishingLiquidKind.Honey;
            }

            return TryInvokeBool(tile, "shimmer", out liquidFlag) && liquidFlag
                ? FishingLiquidKind.Shimmer
                : FishingLiquidKind.Water;
        }

        private static FishingLiquidKind ToLiquidKind(int liquidType)
        {
            switch (liquidType)
            {
                case 1:
                    return FishingLiquidKind.Lava;
                case 2:
                    return FishingLiquidKind.Honey;
                case 3:
                    return FishingLiquidKind.Shimmer;
                default:
                    return FishingLiquidKind.Water;
            }
        }
    }
}
