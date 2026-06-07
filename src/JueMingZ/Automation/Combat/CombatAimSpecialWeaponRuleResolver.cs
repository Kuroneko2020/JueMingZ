using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    // Special rules narrow or deny aim behavior; unknown weapon facts must not promote a weapon into takeover eligibility.
    public sealed class CombatAimSpecialWeaponRule
    {
        public string Kind { get; private set; }
        public string Name { get; private set; }
        public string Rule { get; private set; }
        public bool UsesWeaponProjectile { get; private set; }
        public bool UsesAmmoProjectile { get; private set; }
        public bool UsesCursorTarget { get; private set; }
        public bool AllowsProjectileAiScoped { get; private set; }

        public CombatAimSpecialWeaponRule(
            string kind,
            string name,
            string rule,
            bool usesWeaponProjectile,
            bool usesAmmoProjectile,
            bool usesCursorTarget,
            bool allowsProjectileAiScoped)
        {
            Kind = kind ?? string.Empty;
            Name = name ?? string.Empty;
            Rule = rule ?? string.Empty;
            UsesWeaponProjectile = usesWeaponProjectile;
            UsesAmmoProjectile = usesAmmoProjectile;
            UsesCursorTarget = usesCursorTarget;
            AllowsProjectileAiScoped = allowsProjectileAiScoped;
        }
    }

    public static class CombatAimSpecialWeaponRuleResolver
    {
        private static readonly object CacheSync = new object();
        private static readonly Dictionary<string, int> StaticIdCache = new Dictionary<string, int>(StringComparer.Ordinal);

        public static bool TryResolve(CombatAimWeaponProfile profile, out CombatAimSpecialWeaponRule rule)
        {
            rule = null;
            if (!IsCandidate(profile))
            {
                return false;
            }

            if (MatchesItemId(profile.ItemType, "Xenopopper", 2797))
            {
                rule = new CombatAimSpecialWeaponRule(
                    "cursorSpawnBurst",
                    "Xenopopper",
                    "cursorSpawnBubbleBullet",
                    true,
                    true,
                    true,
                    true);
                return true;
            }

            if (MatchesItemId(profile.ItemType, "VortexBeater", 3475))
            {
                rule = new CombatAimSpecialWeaponRule(
                    "dualProjectileSpread",
                    "VortexBeater",
                    "bulletSpreadWithRocketAssist",
                    true,
                    true,
                    false,
                    true);
                return true;
            }

            return false;
        }

        public static bool MatchesScopedProjectile(
            int projectileType,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            string role;
            return TryResolveScopedProjectileRole(projectileType, profile, solution, out role);
        }

        public static bool TryResolveScopedProjectileRole(
            int projectileType,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            out string role)
        {
            role = string.Empty;
            if (projectileType <= 0 || profile == null)
            {
                return false;
            }

            CombatAimSpecialWeaponRule rule;
            if (!TryResolve(profile, out rule) || !rule.AllowsProjectileAiScoped)
            {
                return false;
            }

            if (IsAmmoPrimaryProjectile(projectileType, solution))
            {
                role = "ammoPrimary";
                return false;
            }

            if (MatchesWeaponAssistProjectile(projectileType, profile, solution, rule))
            {
                role = "weaponAssist";
                return true;
            }

            return false;
        }

        public static bool AllowsNonFriendlyScopedProjectile(
            int projectileType,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            string role;
            if (!TryResolveScopedProjectileRole(projectileType, profile, solution, out role) ||
                !string.Equals(role, "weaponAssist", StringComparison.Ordinal))
            {
                return false;
            }

            CombatAimSpecialWeaponRule rule;
            var vortexControllerProjectileType = ReadProjectileId("VortexBeater", 615);
            return TryResolve(profile, out rule) &&
                   string.Equals(rule.Name, "VortexBeater", StringComparison.Ordinal) &&
                   (projectileType == vortexControllerProjectileType ||
                    projectileType == 615);
        }

        private static bool MatchesWeaponAssistProjectile(
            int projectileType,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            CombatAimSpecialWeaponRule rule)
        {
            if (projectileType <= 0 || profile == null || rule == null || !rule.UsesWeaponProjectile)
            {
                return false;
            }

            if (solution != null &&
                solution.SecondaryProjectileType > 0 &&
                projectileType == solution.SecondaryProjectileType &&
                !string.Equals(solution.SecondaryProjectileRole, "ammoPrimary", StringComparison.Ordinal))
            {
                return true;
            }

            if (solution != null &&
                solution.WeaponShootProjectileType > 0 &&
                projectileType == solution.WeaponShootProjectileType)
            {
                return true;
            }

            return MatchesKnownWeaponAssistProjectile(projectileType, rule);
        }

        private static bool IsAmmoPrimaryProjectile(int projectileType, CombatAimBallisticSolution solution)
        {
            if (projectileType <= 0 || solution == null)
            {
                return false;
            }

            if (solution.AmmoProjectileType > 0 && projectileType == solution.AmmoProjectileType)
            {
                return true;
            }

            if (solution.PrimaryProjectileType > 0 &&
                projectileType == solution.PrimaryProjectileType &&
                string.Equals(solution.PrimaryProjectileRole, "ammoPrimary", StringComparison.Ordinal))
            {
                return true;
            }

            return solution.ProjectileType > 0 &&
                   projectileType == solution.ProjectileType &&
                   string.Equals(solution.ResolvedProjectileRole, "ammoProjectile", StringComparison.Ordinal);
        }

        private static bool MatchesKnownWeaponAssistProjectile(int projectileType, CombatAimSpecialWeaponRule rule)
        {
            if (projectileType <= 0 || rule == null)
            {
                return false;
            }

            if (string.Equals(rule.Name, "Xenopopper", StringComparison.Ordinal))
            {
                return projectileType == ReadProjectileId("Xenopopper", 444);
            }

            if (string.Equals(rule.Name, "VortexBeater", StringComparison.Ordinal))
            {
                return projectileType == ReadProjectileId("VortexBeater", 615) ||
                       projectileType == ReadProjectileId("VortexBeaterRocket", 616);
            }

            return false;
        }

        private static bool IsCandidate(CombatAimWeaponProfile profile)
        {
            if (profile == null || profile.IsEmpty)
            {
                return false;
            }

            if (profile.IsPlacementItem ||
                profile.IsSentryPlacementWeapon ||
                profile.IsSummonPlacementWeapon ||
                profile.IsToolOrFishingItem ||
                profile.IsAmmoItem)
            {
                return false;
            }

            if (!profile.IsCoinGun && profile.Damage <= 0)
            {
                return false;
            }

            return profile.Shoot > 0 && profile.UseAmmo > 0;
        }

        private static bool MatchesItemId(int itemType, string memberName, int fallback)
        {
            var id = ReadItemId(memberName, fallback);
            return id > 0 && itemType == id;
        }

        private static int ReadItemId(string memberName, int fallback)
        {
            var id = ResolveStaticInt("Terraria.ID.ItemID", memberName);
            return id > 0 ? id : fallback;
        }

        private static int ReadProjectileId(string memberName, int fallback)
        {
            var id = ResolveStaticInt("Terraria.ID.ProjectileID", memberName);
            return id > 0 ? id : fallback;
        }

        private static int ResolveStaticInt(string typeName, string memberName)
        {
            var key = typeName + "." + memberName;
            lock (CacheSync)
            {
                int cached;
                if (StaticIdCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }

            var value = 0;
            try
            {
                var type = FindType(typeName);
                if (type != null)
                {
                    var raw = GameStateReflection.GetStaticMember(type, memberName);
                    if (raw != null)
                    {
                        value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
                value = 0;
            }

            lock (CacheSync)
            {
                StaticIdCache[key] = value;
            }

            return value;
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }
    }
}
