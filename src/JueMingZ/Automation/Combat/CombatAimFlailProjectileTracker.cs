using System;
using System.Collections;
using System.Globalization;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    internal sealed class CombatAimFlailProjectileTracker
    {
        private const int ImmunityCacheLength = 256;
        private const float StationaryVelocityEpsilon = 0.001f;

        private readonly int[] _lastLocalNpcImmunity = new int[ImmunityCacheLength];
        private int _trackedProjectileWhoAmI = -1;
        private int _trackedProjectileIdentity = -1;
        private int _trackedProjectileType;
        private int _trackedProjectileStuckTicks;

        public bool HasTracking
        {
            get
            {
                return _trackedProjectileWhoAmI >= 0 ||
                       _trackedProjectileIdentity >= 0 ||
                       _trackedProjectileType != 0 ||
                       _trackedProjectileStuckTicks != 0;
            }
        }

        public bool TryFindActiveFlailProjectile(
            object player,
            int expectedProjectileType,
            out CombatAimFlailControlService.FlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            var projectiles = ReadMainProjectiles();
            if (projectiles == null)
            {
                return false;
            }

            var localOwner = ReadLocalPlayerId(player);
            CombatAimFlailControlService.FlailProjectileSnapshot first = null;
            for (var index = 0; index < projectiles.Count; index++)
            {
                var projectile = projectiles[index];
                if (projectile == null)
                {
                    continue;
                }

                CombatAimFlailControlService.FlailProjectileSnapshot current;
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
                    Track(current);
                    return true;
                }
            }

            if (first == null)
            {
                return false;
            }

            snapshot = first;
            Track(first);
            return true;
        }

        public bool UpdateHitCache(CombatAimFlailControlService.FlailProjectileSnapshot projectile)
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

        public int UpdateStuckTracking(CombatAimFlailControlService.FlailProjectileSnapshot projectile)
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
            var hadTracking = HasTracking;
            _trackedProjectileWhoAmI = -1;
            _trackedProjectileIdentity = -1;
            _trackedProjectileType = 0;
            _trackedProjectileStuckTicks = 0;
            if (hadTracking)
            {
                Array.Clear(_lastLocalNpcImmunity, 0, _lastLocalNpcImmunity.Length);
            }
        }

        private static bool TryReadFlailProjectile(
            object projectile,
            out CombatAimFlailControlService.FlailProjectileSnapshot snapshot)
        {
            snapshot = null;
            if (projectile == null)
            {
                return false;
            }

            var current = new CombatAimFlailControlService.FlailProjectileSnapshot();
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

        private void Track(CombatAimFlailControlService.FlailProjectileSnapshot projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (_trackedProjectileWhoAmI == projectile.WhoAmI &&
                _trackedProjectileIdentity == projectile.Identity &&
                _trackedProjectileType == projectile.Type)
            {
                return;
            }

            Reset();
            _trackedProjectileWhoAmI = projectile.WhoAmI;
            _trackedProjectileIdentity = projectile.Identity;
            _trackedProjectileType = projectile.Type;
        }
    }
}
