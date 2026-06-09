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
        public string AimMode { get; private set; }
        public string SolverKind { get; private set; }
        public string LeadWindowKind { get; private set; }
        public string LeadPolicy { get; private set; }
        public string DiagnosticsReason { get; private set; }
        public bool UsesWeaponProjectile { get; private set; }
        public bool UsesAmmoProjectile { get; private set; }
        public bool UsesCursorTarget { get; private set; }
        public bool AllowsProjectileAiScoped { get; private set; }
        public int ShotCount { get; private set; }
        public float SpreadDegrees { get; private set; }
        public float ParallelSpacingPixels { get; private set; }
        public float FixedLeadTicks { get; private set; }
        public float GravityPerTick { get; private set; }
        public float GravityDelayTicks { get; private set; }
        public bool ArrowGravity { get; private set; }
        public bool RainFromSky { get; private set; }
        public bool HeavyGravity { get; private set; }
        public bool UseWeaponShootSpeedOnly { get; private set; }
        public bool UsesWeaponShoot { get; private set; }
        public bool UsesAmmoShoot { get; private set; }
        public string ReturningPhaseAssumption { get; private set; }

        public CombatAimSpecialWeaponRule(
            string kind,
            string name,
            string rule,
            bool usesWeaponProjectile,
            bool usesAmmoProjectile,
            bool usesCursorTarget,
            bool allowsProjectileAiScoped)
            : this(
                kind,
                name,
                rule,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                rule,
                usesWeaponProjectile,
                usesAmmoProjectile,
                usesCursorTarget,
                allowsProjectileAiScoped,
                1,
                0f,
                0f,
                0f,
                0f,
                0f,
                false,
                false,
                false,
                false,
                usesWeaponProjectile,
                usesAmmoProjectile,
                string.Empty)
        {
        }

        internal CombatAimSpecialWeaponRule(
            string kind,
            string name,
            string rule,
            string aimMode,
            string solverKind,
            string leadWindowKind,
            string leadPolicy,
            string diagnosticsReason,
            bool usesWeaponProjectile,
            bool usesAmmoProjectile,
            bool usesCursorTarget,
            bool allowsProjectileAiScoped,
            int shotCount,
            float spreadDegrees,
            float parallelSpacingPixels,
            float fixedLeadTicks,
            float gravityPerTick,
            float gravityDelayTicks,
            bool arrowGravity,
            bool rainFromSky,
            bool heavyGravity,
            bool useWeaponShootSpeedOnly,
            bool usesWeaponShoot,
            bool usesAmmoShoot,
            string returningPhaseAssumption)
        {
            Kind = kind ?? string.Empty;
            Name = name ?? string.Empty;
            Rule = rule ?? string.Empty;
            AimMode = aimMode ?? string.Empty;
            SolverKind = solverKind ?? string.Empty;
            LeadWindowKind = leadWindowKind ?? string.Empty;
            LeadPolicy = leadPolicy ?? string.Empty;
            DiagnosticsReason = diagnosticsReason ?? string.Empty;
            UsesWeaponProjectile = usesWeaponProjectile;
            UsesAmmoProjectile = usesAmmoProjectile;
            UsesCursorTarget = usesCursorTarget;
            AllowsProjectileAiScoped = allowsProjectileAiScoped;
            ShotCount = shotCount < 1 ? 1 : shotCount;
            SpreadDegrees = spreadDegrees;
            ParallelSpacingPixels = parallelSpacingPixels;
            FixedLeadTicks = fixedLeadTicks;
            GravityPerTick = gravityPerTick;
            GravityDelayTicks = gravityDelayTicks;
            ArrowGravity = arrowGravity;
            RainFromSky = rainFromSky;
            HeavyGravity = heavyGravity;
            UseWeaponShootSpeedOnly = useWeaponShootSpeedOnly;
            UsesWeaponShoot = usesWeaponShoot || usesWeaponProjectile;
            UsesAmmoShoot = usesAmmoShoot || usesAmmoProjectile;
            ReturningPhaseAssumption = returningPhaseAssumption ?? string.Empty;
        }
    }

    public static class CombatAimSpecialWeaponRuleResolver
    {
        private static readonly object CacheSync = new object();
        private static readonly Dictionary<string, int> StaticIdCache = new Dictionary<string, int>(StringComparer.Ordinal);

        public static bool TryResolve(CombatAimWeaponProfile profile, out CombatAimSpecialWeaponRule rule)
        {
            return TryResolve(profile, null, null, out rule);
        }

        public static bool TryResolve(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            CombatAimProjectileProfile projectileProfile,
            out CombatAimSpecialWeaponRule rule)
        {
            rule = null;
            if (!IsCandidate(profile))
            {
                return false;
            }

            if (profile.IsCoinGun)
            {
                rule = CreateRule(
                    "coinGun",
                    "CoinGun",
                    "coinLinearWeaponVelocityOnly",
                    "specialCoinGunLinear",
                    CombatAimBallisticSolverKinds.LinearIntercept,
                    CombatAimLeadWindowKinds.Medium,
                    "weaponShootSpeedOnly",
                    "coinGun",
                    useWeaponShootSpeedOnly: true);
                return true;
            }

            string name;
            if (IsRainFromSkyWeapon(profile.ItemType, out name))
            {
                rule = CreateRule(
                    "rainFromSky",
                    name,
                    "cursorTargetWithVelocityLead",
                    "specialRainFromSky",
                    CombatAimBallisticSolverKinds.PointAim,
                    CombatAimLeadWindowKinds.PointShort,
                    "cursorTargetVelocityLead",
                    "rainFromSky",
                    usesCursorTarget: true,
                    rainFromSky: true);
                return true;
            }

            if (TryResolveParallelMultiShotRule(profile.ItemType, out rule) ||
                TryResolveSpreadMultiShotRule(profile.ItemType, out rule))
            {
                return true;
            }

            if (IsGuidedCursorWeapon(profile.ItemType, out name))
            {
                rule = CreateRule(
                    "guidedCursor",
                    name,
                    "shortLeadCursorControl",
                    "specialGuidedCursor",
                    CombatAimBallisticSolverKinds.PointAim,
                    CombatAimLeadWindowKinds.PointShort,
                    "shortLeadCursorControl",
                    "guidedCursor",
                    fixedLeadTicks: 5f,
                    usesCursorTarget: true);
                return true;
            }

            var projectileAiStyle = ResolveProjectileAiStyle(solution, projectileProfile);
            if (projectileAiStyle == 3)
            {
                rule = CreateRule(
                    "returning",
                    "ProjectileAiStyle3",
                    "returningOutboundOnly",
                    "returningOutboundLead",
                    CombatAimBallisticSolverKinds.ReturningProjectile,
                    CombatAimLeadWindowKinds.ReturningOutbound,
                    "outboundOnly",
                    "returningOutboundOnly",
                    returningPhaseAssumption: "outboundOnly");
                return true;
            }

            if (IsHomingProjectile(ResolveProjectileType(solution, projectileProfile), projectileAiStyle))
            {
                rule = CreateRule(
                    "homingOrSelfCorrecting",
                    "ProjectileHoming",
                    "shortLeadPointAim",
                    "specialHomingPoint",
                    CombatAimBallisticSolverKinds.PointAim,
                    CombatAimLeadWindowKinds.PointShort,
                    "homingShortLead",
                    "homingOrSelfCorrecting",
                    fixedLeadTicks: 5f);
                return true;
            }

            if (IsBeamLikeProjectile(ResolveProjectileType(solution, projectileProfile), projectileAiStyle))
            {
                rule = CreateRule(
                    "beamOrInstant",
                    "BeamOrInstant",
                    "nearInstantPointAim",
                    "specialBeamOrInstant",
                    CombatAimBallisticSolverKinds.PointAim,
                    CombatAimLeadWindowKinds.PointShort,
                    "nearInstantShortLead",
                    "beamOrInstant",
                    fixedLeadTicks: 2f);
                return true;
            }

            if (IsHeavyGravityProjectile(profile, solution, projectileProfile))
            {
                rule = CreateRule(
                    "heavyGravity",
                    "HeavyGravityProjectile",
                    "strongGravityCompensation",
                    "specialHeavyGravity",
                    CombatAimBallisticSolverKinds.GravityArc,
                    CombatAimLeadWindowKinds.GravityArc,
                    "strongGravityCompensation",
                    "heavyGravity",
                    gravityPerTick: projectileAiStyle == 16 || projectileAiStyle == 68 ? 0.2f : 0.14f,
                    gravityDelayTicks: projectileAiStyle == 1 ? 10f : 0f,
                    heavyGravity: true);
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

        private static CombatAimSpecialWeaponRule CreateRule(
            string kind,
            string name,
            string rule,
            string aimMode,
            string solverKind,
            string leadWindowKind,
            string leadPolicy,
            string diagnosticsReason,
            bool usesWeaponProjectile = false,
            bool usesAmmoProjectile = false,
            bool usesCursorTarget = false,
            bool allowsProjectileAiScoped = false,
            int shotCount = 1,
            float spreadDegrees = 0f,
            float parallelSpacingPixels = 0f,
            float fixedLeadTicks = 0f,
            float gravityPerTick = 0f,
            float gravityDelayTicks = 0f,
            bool arrowGravity = false,
            bool rainFromSky = false,
            bool heavyGravity = false,
            bool useWeaponShootSpeedOnly = false,
            bool usesWeaponShoot = false,
            bool usesAmmoShoot = false,
            string returningPhaseAssumption = "")
        {
            return new CombatAimSpecialWeaponRule(
                kind,
                name,
                rule,
                aimMode,
                solverKind,
                leadWindowKind,
                leadPolicy,
                diagnosticsReason,
                usesWeaponProjectile,
                usesAmmoProjectile,
                usesCursorTarget,
                allowsProjectileAiScoped,
                shotCount,
                spreadDegrees,
                parallelSpacingPixels,
                fixedLeadTicks,
                gravityPerTick,
                gravityDelayTicks,
                arrowGravity,
                rainFromSky,
                heavyGravity,
                useWeaponShootSpeedOnly,
                usesWeaponShoot,
                usesAmmoShoot,
                returningPhaseAssumption);
        }

        private static bool TryResolveParallelMultiShotRule(int itemType, out CombatAimSpecialWeaponRule rule)
        {
            rule = null;
            if (MatchesItemId(itemType, "Tsunami", 0))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "Tsunami", "parallelArrowLeadGravity", "specialParallelMultiShot", 5, 0f, 9f, true, false, false);
                return true;
            }

            if (MatchesItemId(itemType, "ChlorophyteShotbow", 0))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "ChlorophyteShotbow", "parallelArrowLeadGravity", "specialParallelMultiShot", 3, 0f, 8f, true, false, false);
                return true;
            }

            if (MatchesItemId(itemType, "Phantasm", 0))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "Phantasm", "parallelArrowLeadGravityPlusHoming", "specialParallelMultiShot", 4, 0f, 7f, true, false, false);
                return true;
            }

            return false;
        }

        private static bool TryResolveSpreadMultiShotRule(int itemType, out CombatAimSpecialWeaponRule rule)
        {
            rule = null;
            if (MatchesItemId(itemType, "Xenopopper", 2797))
            {
                rule = CreateMultiShotRule("cursorSpawnBurst", "Xenopopper", "cursorSpawnBubbleBullet", "specialCursorSpawnBurst", 4, 0f, 0f, false, true, true);
                return true;
            }

            if (MatchesItemId(itemType, "Shotgun", 534))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "Shotgun", "spreadBulletLead", "specialSpreadMultiShot", 4, 10f, 0f, false, false, false);
                return true;
            }

            if (MatchesItemId(itemType, "Boomstick", 964))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "Boomstick", "spreadBulletLead", "specialSpreadMultiShot", 4, 14f, 0f, false, false, false);
                return true;
            }

            if (MatchesItemId(itemType, "QuadBarrelShotgun", 4703))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "QuadBarrelShotgun", "spreadBulletLead", "specialSpreadMultiShot", 6, 18f, 0f, false, false, false);
                return true;
            }

            if (MatchesItemId(itemType, "OnyxBlaster", 3788))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "OnyxBlaster", "spreadBulletLeadWithDarkBolt", "specialSpreadMultiShot", 5, 12f, 0f, false, true, false);
                return true;
            }

            if (MatchesItemId(itemType, "VortexBeater", 3475))
            {
                rule = CreateMultiShotRule("dualProjectileSpread", "VortexBeater", "bulletSpreadWithRocketAssist", "specialSpreadMultiShot", 3, 8f, 0f, false, true, true);
                return true;
            }

            return false;
        }

        private static CombatAimSpecialWeaponRule CreateMultiShotRule(
            string kind,
            string name,
            string rule,
            string aimMode,
            int shotCount,
            float spreadDegrees,
            float parallelSpacingPixels,
            bool arrowGravity,
            bool usesWeaponShoot,
            bool allowsProjectileAiScoped)
        {
            return CreateRule(
                kind,
                name,
                rule,
                aimMode,
                CombatAimBallisticSolverKinds.Spread,
                CombatAimLeadWindowKinds.SpreadCoverage,
                arrowGravity ? "spreadCoverageGravity" : "spreadCoverage",
                rule,
                usesWeaponProjectile: usesWeaponShoot,
                usesAmmoProjectile: usesWeaponShoot,
                usesCursorTarget: string.Equals(kind, "cursorSpawnBurst", StringComparison.Ordinal),
                allowsProjectileAiScoped: allowsProjectileAiScoped,
                shotCount: shotCount,
                spreadDegrees: spreadDegrees,
                parallelSpacingPixels: parallelSpacingPixels,
                fixedLeadTicks: string.Equals(kind, "cursorSpawnBurst", StringComparison.Ordinal) ? 4f : 0f,
                arrowGravity: arrowGravity,
                usesWeaponShoot: usesWeaponShoot,
                usesAmmoShoot: usesWeaponShoot);
        }

        private static bool IsRainFromSkyWeapon(int itemType, out string name)
        {
            name = string.Empty;
            if (MatchesItemId(itemType, "DaedalusStormbow", 0))
            {
                name = "DaedalusStormbow";
                return true;
            }

            if (MatchesItemId(itemType, "BloodRainBow", 0))
            {
                name = "BloodRainBow";
                return true;
            }

            if (MatchesItemId(itemType, "MeteorStaff", 0))
            {
                name = "MeteorStaff";
                return true;
            }

            if (MatchesItemId(itemType, "Starfury", 0))
            {
                name = "Starfury";
                return true;
            }

            if (MatchesItemId(itemType, "StarWrath", 0))
            {
                name = "StarWrath";
                return true;
            }

            if (MatchesItemId(itemType, "LunarFlareBook", 0))
            {
                name = "LunarFlareBook";
                return true;
            }

            return false;
        }

        private static bool IsGuidedCursorWeapon(int itemType, out string name)
        {
            name = string.Empty;
            if (MatchesItemId(itemType, "MagicMissile", 0))
            {
                name = "MagicMissile";
                return true;
            }

            if (MatchesItemId(itemType, "Flamelash", 0))
            {
                name = "Flamelash";
                return true;
            }

            if (MatchesItemId(itemType, "RainbowRod", 495))
            {
                name = "RainbowRod";
                return true;
            }

            return false;
        }

        private static bool IsHomingProjectile(int projectileType, int aiStyle)
        {
            if (projectileType <= 0)
            {
                return false;
            }

            return MatchesProjectileId(projectileType, "ChlorophyteBullet", 0) ||
                   MatchesProjectileId(projectileType, "ChlorophyteArrow", 0) ||
                   MatchesProjectileId(projectileType, "VortexBeaterRocket", 616) ||
                   MatchesProjectileId(projectileType, "PhantasmArrow", 0) ||
                   MatchesProjectileId(projectileType, "SpectreWrath", 0) ||
                   MatchesProjectileId(projectileType, "Bat", 0) ||
                   aiStyle == 99;
        }

        private static bool IsBeamLikeProjectile(int projectileType, int aiStyle)
        {
            if (projectileType <= 0)
            {
                return false;
            }

            return aiStyle == 4 ||
                   aiStyle == 84 ||
                   MatchesProjectileId(projectileType, "HeatRay", 0) ||
                   MatchesProjectileId(projectileType, "LaserMachinegunLaser", 0) ||
                   MatchesProjectileId(projectileType, "MartianWalkerLaser", 0) ||
                   MatchesProjectileId(projectileType, "LastPrismLaser", 0);
        }

        private static bool IsHeavyGravityProjectile(
            CombatAimWeaponProfile weapon,
            CombatAimBallisticSolution solution,
            CombatAimProjectileProfile projectileProfile)
        {
            if (weapon == null ||
                solution == null ||
                projectileProfile == null ||
                solution.ProjectileType <= 0 ||
                projectileProfile.ProjectileNoGravity)
            {
                return false;
            }

            var aiStyle = projectileProfile.ProjectileAiStyle;
            if (aiStyle == 2 || aiStyle == 16 || aiStyle == 68)
            {
                return true;
            }

            return MatchesAmmoId(weapon.UseAmmo, "Snowball") ||
                   MatchesAmmoId(weapon.UseAmmo, "StyngerBolt") ||
                   MatchesAmmoId(weapon.UseAmmo, "JackOLantern") ||
                   MatchesAmmoId(weapon.UseAmmo, "CandyCorn") ||
                   MatchesAmmoId(solution.AmmoType, "Snowball") ||
                   MatchesAmmoId(solution.AmmoType, "StyngerBolt") ||
                   MatchesAmmoId(solution.AmmoType, "JackOLantern") ||
                   MatchesAmmoId(solution.AmmoType, "CandyCorn");
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

            return profile.IsCoinGun || profile.Shoot > 0 || profile.UseAmmo > 0;
        }

        private static int ResolveProjectileType(CombatAimBallisticSolution solution, CombatAimProjectileProfile projectileProfile)
        {
            if (solution != null && solution.ProjectileType > 0)
            {
                return solution.ProjectileType;
            }

            return projectileProfile == null ? 0 : projectileProfile.ProjectileType;
        }

        private static int ResolveProjectileAiStyle(CombatAimBallisticSolution solution, CombatAimProjectileProfile projectileProfile)
        {
            if (projectileProfile != null && projectileProfile.ProjectileAiStyle > 0)
            {
                return projectileProfile.ProjectileAiStyle;
            }

            return solution == null ? 0 : solution.ProjectileAiStyle;
        }

        private static bool MatchesItemId(int itemType, string memberName, int fallback)
        {
            var id = ReadItemId(memberName, fallback);
            return id > 0 && itemType == id;
        }

        private static bool MatchesProjectileId(int projectileType, string memberName, int fallback)
        {
            var id = ReadProjectileId(memberName, fallback);
            return id > 0 && projectileType == id;
        }

        private static bool MatchesAmmoId(int ammoType, string memberName)
        {
            var id = ReadAmmoId(memberName);
            return id > 0 && ammoType == id;
        }

        private static int ReadItemId(string memberName, int fallback)
        {
            var id = ResolveStaticInt("Terraria.ID.ItemID", memberName);
            if (id <= 0)
            {
                id = ReadKnownItemId(memberName);
            }

            return id > 0 ? id : fallback;
        }

        private static int ReadProjectileId(string memberName, int fallback)
        {
            var id = ResolveStaticInt("Terraria.ID.ProjectileID", memberName);
            return id > 0 ? id : fallback;
        }

        private static int ReadAmmoId(string memberName)
        {
            return ResolveStaticInt("Terraria.ID.AmmoID", memberName);
        }

        private static int ReadKnownItemId(string memberName)
        {
            if (string.Equals(memberName, "CoinGun", StringComparison.Ordinal))
            {
                return 905;
            }

            if (string.Equals(memberName, "Xenopopper", StringComparison.Ordinal))
            {
                return 2797;
            }

            if (string.Equals(memberName, "VortexBeater", StringComparison.Ordinal))
            {
                return 3475;
            }

            if (string.Equals(memberName, "OnyxBlaster", StringComparison.Ordinal))
            {
                return 3788;
            }

            if (string.Equals(memberName, "Shotgun", StringComparison.Ordinal))
            {
                return 534;
            }

            if (string.Equals(memberName, "Boomstick", StringComparison.Ordinal))
            {
                return 964;
            }

            if (string.Equals(memberName, "QuadBarrelShotgun", StringComparison.Ordinal))
            {
                return 4703;
            }

            if (string.Equals(memberName, "RainbowRod", StringComparison.Ordinal))
            {
                return 495;
            }

            return 0;
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
