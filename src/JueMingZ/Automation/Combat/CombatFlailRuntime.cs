using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatFlailRuntime
    {
        private const int ImmunityCacheLength = 256;
        private const float StationaryVelocityEpsilon = 0.001f;
        private static readonly object ItemSetSyncRoot = new object();
        private static readonly Dictionary<string, bool[]> ItemSetBoolCache = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        private static MethodInfo _tileCollisionMethod;
        private static bool _tileCollisionResolved;

        public static bool TryReadSelectedWeaponProfile(object player, out CombatAimWeaponProfile profile, out object item, out string reason)
        {
            profile = null;
            item = null;
            reason = string.Empty;

            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                reason = "selectedSlotUnavailable:" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            if (selectedSlot > 9)
            {
                reason = "selectedSlotNotHotbar";
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                reason = "inventoryUnavailable";
                return false;
            }

            item = inventory[selectedSlot];
            if (item == null)
            {
                reason = "selectedItemUnavailable";
                return false;
            }

            profile = CombatAimWeaponProfile.Read(player, item);
            if (profile == null || profile.IsEmpty)
            {
                reason = "selectedWeaponUnavailable";
                return false;
            }

            return true;
        }

        public static bool TryEvaluateEligibility(
            object player,
            CombatAimWeaponProfile profile,
            out int projectileAiStyle,
            out bool isYoyo,
            out CombatAimFlailEligibility eligibility)
        {
            projectileAiStyle = 0;
            isYoyo = false;
            eligibility = CombatAimFlailEligibility.Reject("notFlail:noWeapon");
            if (profile == null)
            {
                return false;
            }

            var prepared = CombatAimBallisticSolver.Prepare(player, profile);
            projectileAiStyle = prepared == null ? 0 : prepared.ProjectileAiStyle;
            isYoyo = CombatAimYoyoCompat.IsYoyoProjectileType(profile.Shoot);
            eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
            return true;
        }

        public static bool CanUseSelectedItem(object player)
        {
            if (player == null)
            {
                return false;
            }

            int itemAnimation;
            int itemTime;
            int reuseDelay;
            bool delayUseItem;
            GameStateReflection.TryGetInt(player, "itemAnimation", out itemAnimation);
            GameStateReflection.TryGetInt(player, "itemTime", out itemTime);
            GameStateReflection.TryGetInt(player, "reuseDelay", out reuseDelay);
            GameStateReflection.TryGetBool(player, "delayUseItem", out delayUseItem);
            return itemAnimation <= 0 && itemTime <= 0 && reuseDelay <= 0 && !delayUseItem;
        }

        public static bool TryReadActiveFrame(
            object player,
            int expectedProjectileType,
            CombatFlailProjectileTracker tracker,
            out CombatFlailRuntimeFrame frame)
        {
            frame = CombatFlailRuntimeFrame.None();
            CombatFlailProjectileSnapshot projectile;
            if (!TryFindActiveFlailProjectile(player, expectedProjectileType, out projectile))
            {
                if (tracker != null)
                {
                    tracker.Reset();
                }

                return false;
            }

            var hitDetected = tracker != null && tracker.UpdateHitCache(projectile);
            var stuckTicks = tracker == null ? 0 : tracker.UpdateStuckTracking(projectile);
            var collisionDetected = DetectTileCollision(projectile);
            frame = CombatFlailRuntimeFrame.FromSnapshot(projectile, hitDetected, collisionDetected, stuckTicks);
            return true;
        }

        public static bool IsReturnOrEndingState(float ai0)
        {
            return Math.Abs(ai0 - 4f) < 0.001f ||
                   Math.Abs(ai0 - 5f) < 0.001f ||
                   Math.Abs(ai0 - 6f) < 0.001f;
        }

        public static bool IsStationaryLaunchProjectile(CombatFlailRuntimeFrame frame)
        {
            return frame != null &&
                   frame.HasProjectile &&
                   Math.Abs(frame.ProjectileAi0) < 0.001f &&
                   Math.Abs(frame.ProjectileVelocityX) < StationaryVelocityEpsilon &&
                   Math.Abs(frame.ProjectileVelocityY) < StationaryVelocityEpsilon;
        }

        public static bool HasVanillaRightClickSemantics(int itemType, object player, out string reason)
        {
            reason = string.Empty;
            int altFunctionUse;
            if (player != null && GameStateReflection.TryGetInt(player, "altFunctionUse", out altFunctionUse) && altFunctionUse != 0)
            {
                reason = "altFunctionUseActive";
                return true;
            }

            bool value;
            string itemSetReason;
            if (!TryReadItemSetBool("HasRightFire", itemType, out value, out itemSetReason))
            {
                reason = itemSetReason;
                return true;
            }

            if (value)
            {
                reason = "itemHasRightFire";
                return true;
            }

            if (!TryReadItemSetBool("ItemsThatAllowRepeatedRightClick", itemType, out value, out itemSetReason))
            {
                reason = itemSetReason;
                return true;
            }

            if (value)
            {
                reason = "itemAllowsRepeatedRightClick";
                return true;
            }

            return false;
        }

        private static bool TryReadItemSetBool(string memberName, int itemType, out bool value, out string reason)
        {
            value = false;
            reason = string.Empty;
            if (itemType < 0)
            {
                return true;
            }

            try
            {
                bool[] values;
                lock (ItemSetSyncRoot)
                {
                    if (!ItemSetBoolCache.TryGetValue(memberName, out values))
                    {
                        var setsType = TerrariaTypeCache.Find("Terraria.ID.ItemID+Sets");
                        values = setsType == null ? null : GameStateReflection.GetStaticMember(setsType, memberName) as bool[];
                        ItemSetBoolCache[memberName] = values;
                    }
                }

                if (values == null)
                {
                    reason = "rightClickSetUnavailable:" + memberName;
                    return false;
                }

                value = itemType < values.Length && values[itemType];
                return true;
            }
            catch (Exception error)
            {
                reason = "rightClickSetReadFailed:" + memberName + ":" + error.GetType().Name;
                return false;
            }
        }

        private static bool TryFindActiveFlailProjectile(object player, int expectedProjectileType, out CombatFlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            var projectiles = ReadMainProjectiles();
            if (projectiles == null)
            {
                return false;
            }

            var localOwner = ReadLocalPlayerId(player);
            CombatFlailProjectileSnapshot first = null;
            for (var index = 0; index < projectiles.Count; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null)
                {
                    continue;
                }

                CombatFlailProjectileSnapshot current;
                if (!TryReadFlailProjectile(projectile, out current))
                {
                    continue;
                }

                if (!current.Active || current.Owner != localOwner || current.AiStyle != 15 || !current.Friendly || current.Hostile)
                {
                    continue;
                }

                if (first == null)
                {
                    first = current;
                }

                if (expectedProjectileType > 0 && current.Type == expectedProjectileType)
                {
                    snapshot = current;
                    return true;
                }
            }

            if (first == null)
            {
                return false;
            }

            snapshot = first;
            return true;
        }

        private static bool TryReadFlailProjectile(object projectile, out CombatFlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            if (projectile == null)
            {
                return false;
            }

            var current = new CombatFlailProjectileSnapshot();
            current.Raw = projectile;
            GameStateReflection.TryGetInt(projectile, "whoAmI", out current.WhoAmI);
            GameStateReflection.TryGetInt(projectile, "type", out current.Type);
            GameStateReflection.TryGetInt(projectile, "aiStyle", out current.AiStyle);
            GameStateReflection.TryGetInt(projectile, "owner", out current.Owner);
            GameStateReflection.TryGetInt(projectile, "identity", out current.Identity);
            GameStateReflection.TryGetBool(projectile, "active", out current.Active);
            GameStateReflection.TryGetBool(projectile, "friendly", out current.Friendly);
            GameStateReflection.TryGetBool(projectile, "hostile", out current.Hostile);
            GameStateReflection.TryGetInt(projectile, "width", out current.Width);
            GameStateReflection.TryGetInt(projectile, "height", out current.Height);
            current.Ai0 = ReadAi0(projectile);
            current.Position = GameStateReflection.GetMember(projectile, "position");
            current.Velocity = GameStateReflection.GetMember(projectile, "velocity");
            GameStateReflection.TryReadVector2(current.Velocity, out current.VelocityX, out current.VelocityY);
            current.LocalNpcImmunity = GameStateReflection.AsList(GameStateReflection.GetMember(projectile, "localNPCImmunity"));
            snapshot = current;
            return current.Type > 0;
        }

        private static bool DetectTileCollision(CombatFlailProjectileSnapshot projectile)
        {
            if (projectile == null || Math.Abs(projectile.Ai0 - 1f) > 0.001f)
            {
                return false;
            }

            float velocityX;
            float velocityY;
            if (!GameStateReflection.TryReadVector2(projectile.Velocity, out velocityX, out velocityY) ||
                Math.Abs(velocityX) < 0.001f && Math.Abs(velocityY) < 0.001f)
            {
                return false;
            }

            var method = ResolveTileCollisionMethod();
            if (method == null)
            {
                return false;
            }

            try
            {
                var parameters = method.GetParameters();
                var args = new object[parameters.Length];
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (index == 0)
                    {
                        args[index] = projectile.Position;
                    }
                    else if (index == 1)
                    {
                        args[index] = projectile.Velocity;
                    }
                    else if (index == 2)
                    {
                        args[index] = projectile.Width;
                    }
                    else if (index == 3)
                    {
                        args[index] = projectile.Height;
                    }
                    else if (parameters[index].ParameterType == typeof(bool))
                    {
                        args[index] = false;
                    }
                    else if (parameters[index].ParameterType == typeof(int))
                    {
                        args[index] = 1;
                    }
                    else
                    {
                        args[index] = parameters[index].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[index].ParameterType)
                            : null;
                    }
                }

                var result = method.Invoke(null, args);
                float resultX;
                float resultY;
                if (!GameStateReflection.TryReadVector2(result, out resultX, out resultY))
                {
                    return false;
                }

                return Math.Abs(resultX - velocityX) > 0.001f ||
                       Math.Abs(resultY - velocityY) > 0.001f;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo ResolveTileCollisionMethod()
        {
            if (_tileCollisionResolved)
            {
                return _tileCollisionMethod;
            }

            _tileCollisionResolved = true;
            var type = TerrariaTypeCache.Find("Terraria.Collision");
            if (type == null)
            {
                return null;
            }

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "TileCollision", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 4)
                {
                    continue;
                }

                if (string.Equals(parameters[0].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    string.Equals(parameters[1].ParameterType.Name, "Vector2", StringComparison.Ordinal) &&
                    parameters[2].ParameterType == typeof(int) &&
                    parameters[3].ParameterType == typeof(int))
                {
                    _tileCollisionMethod = method;
                    return _tileCollisionMethod;
                }
            }

            return null;
        }

        private static IList ReadMainProjectiles()
        {
            try
            {
                var mainType = GameMode.FindTerrariaMainType();
                return mainType == null ? null : GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "projectile"));
            }
            catch
            {
                return null;
            }
        }

        private static int ReadLocalPlayerId(object player)
        {
            try
            {
                var mainType = GameMode.FindTerrariaMainType();
                var raw = mainType == null ? null : GameStateReflection.GetStaticMember(mainType, "myPlayer");
                if (raw != null)
                {
                    return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                }
            }
            catch
            {
            }

            int whoAmI;
            return player != null && GameStateReflection.TryGetInt(player, "whoAmI", out whoAmI) ? whoAmI : -1;
        }

        private static float ReadAi0(object projectile)
        {
            var ai = GameStateReflection.AsList(GameStateReflection.GetMember(projectile, "ai"));
            if (ai == null || ai.Count <= 0 || ai[0] == null)
            {
                return 0f;
            }

            return Convert.ToSingle(ai[0], CultureInfo.InvariantCulture);
        }

        internal static void ResetItemSetCacheForTesting()
        {
            lock (ItemSetSyncRoot)
            {
                ItemSetBoolCache.Clear();
            }
        }

        internal sealed class CombatFlailProjectileTracker
        {
            private readonly int[] _lastLocalNpcImmunity = new int[ImmunityCacheLength];
            private int _trackedProjectileWhoAmI = -1;
            private int _trackedProjectileIdentity = -1;
            private int _trackedProjectileType;
            private int _trackedProjectileStuckTicks;

            public bool UpdateHitCache(CombatFlailProjectileSnapshot projectile)
            {
                if (projectile == null || projectile.LocalNpcImmunity == null)
                {
                    return false;
                }

                if (projectile.WhoAmI != _trackedProjectileWhoAmI ||
                    projectile.Identity != _trackedProjectileIdentity ||
                    projectile.Type != _trackedProjectileType)
                {
                    Reset();
                    Track(projectile);
                }

                var detected = false;
                var count = Math.Min(projectile.LocalNpcImmunity.Count, _lastLocalNpcImmunity.Length);
                for (var index = 0; index < count; index++)
                {
                    var raw = projectile.LocalNpcImmunity[index];
                    var value = raw == null ? 0 : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    if (value > _lastLocalNpcImmunity[index])
                    {
                        detected = true;
                    }

                    _lastLocalNpcImmunity[index] = value;
                }

                for (var index = count; index < _lastLocalNpcImmunity.Length; index++)
                {
                    _lastLocalNpcImmunity[index] = 0;
                }

                return detected;
            }

            public int UpdateStuckTracking(CombatFlailProjectileSnapshot projectile)
            {
                if (projectile == null ||
                    projectile.WhoAmI != _trackedProjectileWhoAmI ||
                    projectile.Identity != _trackedProjectileIdentity ||
                    projectile.Type != _trackedProjectileType ||
                    Math.Abs(projectile.Ai0) >= 0.001f ||
                    Math.Abs(projectile.VelocityX) >= StationaryVelocityEpsilon ||
                    Math.Abs(projectile.VelocityY) >= StationaryVelocityEpsilon)
                {
                    _trackedProjectileStuckTicks = 0;
                    return _trackedProjectileStuckTicks;
                }

                _trackedProjectileStuckTicks++;
                return _trackedProjectileStuckTicks;
            }

            public void Reset()
            {
                _trackedProjectileWhoAmI = -1;
                _trackedProjectileIdentity = -1;
                _trackedProjectileType = 0;
                _trackedProjectileStuckTicks = 0;
                Array.Clear(_lastLocalNpcImmunity, 0, _lastLocalNpcImmunity.Length);
            }

            private void Track(CombatFlailProjectileSnapshot projectile)
            {
                if (projectile == null)
                {
                    return;
                }

                _trackedProjectileWhoAmI = projectile.WhoAmI;
                _trackedProjectileIdentity = projectile.Identity;
                _trackedProjectileType = projectile.Type;
            }
        }
    }

    internal sealed class CombatFlailProjectileSnapshot
    {
        public object Raw;
        public int WhoAmI = -1;
        public int Type;
        public int AiStyle;
        public int Owner = -1;
        public int Identity = -1;
        public bool Active;
        public bool Friendly;
        public bool Hostile;
        public int Width;
        public int Height;
        public float Ai0;
        public float VelocityX;
        public float VelocityY;
        public object Position;
        public object Velocity;
        public IList LocalNpcImmunity;
    }

    public sealed class CombatFlailRuntimeFrame
    {
        public bool HasProjectile { get; private set; }
        public int ProjectileWhoAmI { get; private set; }
        public int ProjectileType { get; private set; }
        public int ProjectileAiStyle { get; private set; }
        public int ProjectileIdentity { get; private set; }
        public float ProjectileAi0 { get; private set; }
        public float ProjectileVelocityX { get; private set; }
        public float ProjectileVelocityY { get; private set; }
        public bool HitDetected { get; private set; }
        public bool CollisionDetected { get; private set; }
        public bool LocalNpcImmunityChanged { get; private set; }
        public bool TileCollisionDetected { get; private set; }
        public int StuckTicks { get; private set; }

        public static CombatFlailRuntimeFrame None()
        {
            return new CombatFlailRuntimeFrame
            {
                HasProjectile = false,
                ProjectileWhoAmI = -1,
                ProjectileIdentity = -1
            };
        }

        internal static CombatFlailRuntimeFrame FromSnapshot(
            CombatFlailProjectileSnapshot source,
            bool hitDetected,
            bool collisionDetected,
            int stuckTicks)
        {
            if (source == null)
            {
                return None();
            }

            return new CombatFlailRuntimeFrame
            {
                HasProjectile = true,
                ProjectileWhoAmI = source.WhoAmI,
                ProjectileType = source.Type,
                ProjectileAiStyle = source.AiStyle,
                ProjectileIdentity = source.Identity,
                ProjectileAi0 = source.Ai0,
                ProjectileVelocityX = source.VelocityX,
                ProjectileVelocityY = source.VelocityY,
                HitDetected = hitDetected,
                CollisionDetected = collisionDetected,
                LocalNpcImmunityChanged = hitDetected,
                TileCollisionDetected = collisionDetected,
                StuckTicks = stuckTicks
            };
        }

        public static CombatFlailRuntimeFrame ForTesting(
            bool hasProjectile,
            int projectileWhoAmI,
            int projectileType,
            int projectileIdentity,
            float ai0,
            float velocityX,
            float velocityY,
            bool hitDetected,
            bool collisionDetected,
            int stuckTicks)
        {
            return new CombatFlailRuntimeFrame
            {
                HasProjectile = hasProjectile,
                ProjectileWhoAmI = projectileWhoAmI,
                ProjectileType = projectileType,
                ProjectileAiStyle = hasProjectile ? 15 : 0,
                ProjectileIdentity = projectileIdentity,
                ProjectileAi0 = ai0,
                ProjectileVelocityX = velocityX,
                ProjectileVelocityY = velocityY,
                HitDetected = hitDetected,
                CollisionDetected = collisionDetected,
                LocalNpcImmunityChanged = hitDetected,
                TileCollisionDetected = collisionDetected,
                StuckTicks = stuckTicks
            };
        }
    }
}
