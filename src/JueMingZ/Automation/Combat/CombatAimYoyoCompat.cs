using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    // Yoyo detection stands down on uncertainty so yoyo and flail takeover semantics cannot be merged accidentally.
    public static class CombatAimYoyoCompat
    {
        private static readonly object SyncRoot = new object();
        private static bool _setsResolved;
        private static Array _yoyoLife;
        private static Array _yoyoRange;
        private static Array _yoyoSpeed;
        private static readonly Dictionary<int, int> ProjectileAiStyleCache = new Dictionary<int, int>();

        public static bool IsYoyoWeapon(object player, CombatAimWeaponProfile weapon, out bool yoyoDetected, out string reason)
        {
            yoyoDetected = false;
            reason = string.Empty;
            if (weapon == null || weapon.Shoot <= 0)
            {
                reason = "weaponHasNoProjectile";
                return false;
            }

            if (!IsYoyoProjectileType(weapon.Shoot))
            {
                reason = "projectileNotYoyo";
                return false;
            }

            yoyoDetected = true;
            if (!HasActiveOwnedYoyoProjectile(player, weapon.Shoot, out reason))
            {
                return false;
            }

            reason = "activeYoyoProjectile";
            return true;
        }

        public static bool IsYoyoProjectileType(int projectileType)
        {
            if (projectileType <= 0)
            {
                return false;
            }

            if (TryReadYoyoSetValue(projectileType, out var setValue) && setValue > 0.001f)
            {
                return true;
            }

            return ReadProjectileAiStyle(projectileType) == 99;
        }

        public static bool TryReadProjectileInfo(object projectile, out int whoAmI, out int type, out int aiStyle, out bool active, out int owner)
        {
            whoAmI = -1;
            type = 0;
            aiStyle = 0;
            active = false;
            owner = -1;
            if (projectile == null)
            {
                return false;
            }

            GameStateReflection.TryGetInt(projectile, "whoAmI", out whoAmI);
            if (!GameStateReflection.TryGetInt(projectile, "type", out type))
            {
                return false;
            }

            GameStateReflection.TryGetInt(projectile, "aiStyle", out aiStyle);
            GameStateReflection.TryGetBool(projectile, "active", out active);
            GameStateReflection.TryGetInt(projectile, "owner", out owner);
            return true;
        }

        public static bool IsLocalOwnedYoyoProjectile(object projectile, out int whoAmI, out int type, out int aiStyle, out string reason)
        {
            reason = string.Empty;
            if (!TryReadProjectileInfo(projectile, out whoAmI, out type, out aiStyle, out var active, out var owner))
            {
                reason = "projectileInfoUnavailable";
                return false;
            }

            if (!active)
            {
                reason = "projectileInactive";
                return false;
            }

            var myPlayer = ReadMyPlayer();
            if (myPlayer < 0 || owner != myPlayer)
            {
                reason = "projectileNotLocalOwner";
                return false;
            }

            if (aiStyle != 99 && !IsYoyoProjectileType(type))
            {
                reason = "projectileNotYoyo";
                return false;
            }

            reason = "localOwnedYoyoProjectile";
            return true;
        }

        private static int ReadMyPlayer()
        {
            try
            {
                var mainType = FindType("Terraria.Main");
                if (mainType == null)
                {
                    return -1;
                }

                var raw = GameStateReflection.GetStaticMember(mainType, "myPlayer");
                return raw == null ? -1 : Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return -1;
            }
        }

        private static bool HasActiveOwnedYoyoProjectile(object player, int preferredProjectileType, out string reason)
        {
            reason = string.Empty;
            int owner = -1;
            GameStateReflection.TryGetInt(player, "whoAmI", out owner);
            if (owner < 0)
            {
                reason = "playerOwnerUnavailable";
                return false;
            }

            var mainType = FindType("Terraria.Main");
            if (mainType == null)
            {
                reason = "mainTypeUnavailable";
                return false;
            }

            var projectiles = GameStateReflection.AsList(GameStateReflection.GetStaticMember(mainType, "projectile"));
            if (projectiles == null)
            {
                reason = "projectileListUnavailable";
                return false;
            }

            for (var index = 0; index < projectiles.Count; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null)
                {
                    continue;
                }

                bool active;
                if (!GameStateReflection.TryGetBool(projectile, "active", out active) || !active)
                {
                    continue;
                }

                int projectileOwner;
                if (!GameStateReflection.TryGetInt(projectile, "owner", out projectileOwner) || projectileOwner != owner)
                {
                    continue;
                }

                int type;
                if (!GameStateReflection.TryGetInt(projectile, "type", out type))
                {
                    continue;
                }

                if (type == preferredProjectileType || IsYoyoProjectileType(type))
                {
                    return true;
                }
            }

            reason = "yoyoProjectileNotDetected";
            return false;
        }

        private static bool TryReadYoyoSetValue(int projectileType, out float value)
        {
            value = 0f;
            EnsureSetsResolved();
            lock (SyncRoot)
            {
                return TryReadFloatArray(_yoyoLife, projectileType, out value) ||
                       TryReadFloatArray(_yoyoRange, projectileType, out value) ||
                       TryReadFloatArray(_yoyoSpeed, projectileType, out value);
            }
        }

        private static void EnsureSetsResolved()
        {
            lock (SyncRoot)
            {
                if (_setsResolved)
                {
                    return;
                }

                _setsResolved = true;
                try
                {
                    var setsType = FindType("Terraria.ID.ProjectileID+Sets");
                    if (setsType == null)
                    {
                        return;
                    }

                    _yoyoLife = GameStateReflection.GetStaticMember(setsType, "YoyosLifeTimeMultiplier") as Array;
                    _yoyoRange = GameStateReflection.GetStaticMember(setsType, "YoyosMaximumRange") as Array;
                    _yoyoSpeed = GameStateReflection.GetStaticMember(setsType, "YoyosTopSpeed") as Array;
                }
                catch (Exception error)
                {
                    LogThrottle.WarnThrottled(
                        "combat-aim-yoyo-sets-resolve-failed",
                        TimeSpan.FromSeconds(30),
                        "CombatAimYoyoCompat",
                        "Yoyo projectile set reflection failed: " + error.Message);
                }
            }
        }

        private static bool TryReadFloatArray(Array array, int index, out float value)
        {
            value = 0f;
            if (array == null || index < 0 || index >= array.Length)
            {
                return false;
            }

            try
            {
                value = Convert.ToSingle(array.GetValue(index), CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                value = 0f;
                return false;
            }
        }

        private static int ReadProjectileAiStyle(int projectileType)
        {
            lock (SyncRoot)
            {
                int cached;
                if (ProjectileAiStyleCache.TryGetValue(projectileType, out cached))
                {
                    return cached;
                }
            }

            var aiStyle = ResolveProjectileAiStyle(projectileType);
            lock (SyncRoot)
            {
                ProjectileAiStyleCache[projectileType] = aiStyle;
            }

            return aiStyle;
        }

        private static int ResolveProjectileAiStyle(int projectileType)
        {
            try
            {
                var projectileTypeObject = FindType("Terraria.Projectile");
                if (projectileTypeObject == null)
                {
                    return 0;
                }

                var projectile = Activator.CreateInstance(projectileTypeObject);
                var setDefaults = projectileTypeObject.GetMethod("SetDefaults", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (setDefaults == null)
                {
                    return 0;
                }

                setDefaults.Invoke(projectile, new object[] { projectileType });
                int aiStyle;
                return GameStateReflection.TryGetInt(projectile, "aiStyle", out aiStyle) ? aiStyle : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }
    }
}
