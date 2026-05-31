using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimBallisticSolver
    {
        private const int MainInventoryEndExclusive = 54;
        private const int CoinInventoryStart = 50;
        private const int CoinInventoryEndExclusive = 54;
        private const int AmmoInventoryStart = 54;
        private const int AmmoInventoryEndExclusive = 58;
        private const float DefaultProjectileSpeed = 8f;
        private const float MaxLeadTicks = 45f;
        private const float MaxSpecialLeadTicks = 36f;
        private const float MaxGravityCompensationPixels = 180f;

        private static readonly object CacheSync = new object();
        private static readonly Dictionary<int, ProjectileDefaults> ProjectileDefaultsCache = new Dictionary<int, ProjectileDefaults>();
        private static readonly Dictionary<string, int> StaticIdCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool[]> ItemSetBoolCache = new Dictionary<string, bool[]>(StringComparer.Ordinal);

        public static CombatAimBallisticSolution Solve(object player, CombatAimItemCheckDecision decision)
        {
            return Solve(
                player,
                decision == null ? null : decision.WeaponProfile,
                decision == null ? null : (decision.Selection == null ? decision.Target : decision.Selection.BallisticTarget ?? decision.Target));
        }

        public static CombatAimBallisticSolution Solve(object player, CombatAimWeaponProfile weapon, CombatTargetSnapshot target)
        {
            return Solve(Prepare(player, weapon), target);
        }

        public static CombatAimBallisticContext Prepare(object player, CombatAimWeaponProfile weapon)
        {
            var context = new CombatAimBallisticContext
            {
                Weapon = weapon
            };

            try
            {
                float playerCenterX;
                float playerCenterY;
                if (!TryReadPlayerCenter(player, out playerCenterX, out playerCenterY))
                {
                    context.FallbackReason = "playerCenterUnavailable";
                    return context;
                }

                context.HasPlayerCenter = true;
                context.PlayerCenterX = playerCenterX;
                context.PlayerCenterY = playerCenterY;

                if (weapon == null)
                {
                    context.FallbackReason = "weaponProfileUnavailable";
                    return context;
                }

                AmmoSnapshot ammo;
                var hasAmmo = TryFindAmmo(player, weapon.UseAmmo, weapon.CoinAmmoType, out ammo);
                var projectileType = ResolveProjectileType(weapon, hasAmmo ? ammo : null);
                var defaults = ResolveProjectileDefaults(projectileType);
                var projectileSpeed = ResolveProjectileSpeed(weapon, hasAmmo ? ammo : null);
                var extraUpdates = defaults.HasValue && defaults.ExtraUpdates > 0 ? defaults.ExtraUpdates : 0;
                var effectiveSpeed = projectileSpeed * (extraUpdates + 1);
                if (effectiveSpeed < 0.5f)
                {
                    effectiveSpeed = projectileSpeed < 0.5f ? DefaultProjectileSpeed : projectileSpeed;
                }

                context.ProjectileType = projectileType;
                context.ProjectileName = defaults.Name ?? string.Empty;
                context.ProjectileAiStyle = defaults.AiStyle;
                context.ProjectileExtraUpdates = extraUpdates;
                context.ProjectileDefaultsAvailable = defaults.HasValue;
                context.ProjectileNoGravity = defaults.NoGravity;
                context.ProjectileArrow = defaults.Arrow;
                context.ProjectileTileCollide = defaults.TileCollide;
                context.ProjectileWidth = defaults.Width;
                context.ProjectileHeight = defaults.Height;
                context.ProjectileFriendly = defaults.Friendly;
                context.ProjectileHostile = defaults.Hostile;
                context.ProjectileSpeed = projectileSpeed;
                context.EffectiveProjectileSpeed = effectiveSpeed;
                context.AmmoAvailable = hasAmmo;
                context.AmmoType = hasAmmo ? ammo.AmmoType : 0;
                context.AmmoItemType = hasAmmo ? ammo.ItemType : 0;
                context.AmmoItemName = hasAmmo ? ammo.ItemName : string.Empty;
                context.AmmoProjectileType = hasAmmo ? ammo.Shoot : 0;
                context.AmmoSlot = hasAmmo ? ammo.Slot : -1;
                context.AmmoShootSpeed = hasAmmo ? ammo.ShootSpeed : 0f;
                context.AmmoArrowLike = IsArrowLikeAmmo(weapon, hasAmmo ? ammo : null, defaults);
                context.AmmoBulletLike = IsBulletLikeAmmo(weapon, hasAmmo ? ammo : null);
                context.Prepared = true;
                return context;
            }
            catch (Exception error)
            {
                context.FallbackReason = "prepareFailed:" + error.Message;
                LogThrottle.WarnThrottled(
                    "combat-aim-ballistic-prepare-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimBallisticSolver",
                    "Combat aim ballistic prepare failed: " + error.Message);
                return context;
            }
        }

        public static CombatAimBallisticSolution Solve(CombatAimBallisticContext prepared, CombatTargetSnapshot target)
        {
            var solution = new CombatAimBallisticSolution();
            try
            {
                if (target == null)
                {
                    solution.Mode = "centerFallback";
                    solution.FallbackReason = "missingDecisionOrTarget";
                    return solution;
                }

                solution.TargetVelocityX = target.SmoothedVelocityAvailable ? target.SmoothedVelocityX : target.VelocityX;
                solution.TargetVelocityY = target.SmoothedVelocityAvailable ? target.SmoothedVelocityY : target.VelocityY;
                solution.PredictedTargetX = target.CenterX;
                solution.PredictedTargetY = target.CenterY;
                solution.AimWorldX = target.CenterX;
                solution.AimWorldY = target.CenterY;

                if (prepared == null || !prepared.HasPlayerCenter)
                {
                    return Center(solution, "centerFallback", prepared == null || string.IsNullOrWhiteSpace(prepared.FallbackReason) ? "playerCenterUnavailable" : prepared.FallbackReason);
                }

                var weapon = prepared.Weapon;
                solution.PlayerCenterX = prepared.PlayerCenterX;
                solution.PlayerCenterY = prepared.PlayerCenterY;

                if (weapon == null)
                {
                    return Center(solution, "centerFallback", "weaponProfileUnavailable");
                }

                var defaults = FromPreparedDefaults(prepared);
                var projectileType = prepared.ProjectileType;
                var projectileSpeed = prepared.ProjectileSpeed;
                var extraUpdates = prepared.ProjectileExtraUpdates;
                var effectiveSpeed = prepared.EffectiveProjectileSpeed;

                solution.ProjectileType = prepared.ProjectileType;
                solution.ProjectileName = prepared.ProjectileName ?? string.Empty;
                solution.ProjectileAiStyle = prepared.ProjectileAiStyle;
                solution.ProjectileExtraUpdates = prepared.ProjectileExtraUpdates;
                solution.ProjectileDefaultsAvailable = prepared.ProjectileDefaultsAvailable;
                solution.ProjectileNoGravity = prepared.ProjectileNoGravity;
                solution.ProjectileArrow = prepared.ProjectileArrow;
                solution.ProjectileTileCollide = prepared.ProjectileTileCollide;
                solution.ProjectileWidth = prepared.ProjectileWidth;
                solution.ProjectileHeight = prepared.ProjectileHeight;
                solution.ProjectileFriendly = prepared.ProjectileFriendly;
                solution.ProjectileHostile = prepared.ProjectileHostile;
                solution.ProjectileSpeed = prepared.ProjectileSpeed;
                solution.EffectiveProjectileSpeed = prepared.EffectiveProjectileSpeed;
                solution.AmmoAvailable = prepared.AmmoAvailable;
                solution.AmmoType = prepared.AmmoType;
                solution.AmmoItemType = prepared.AmmoItemType;
                solution.AmmoItemName = prepared.AmmoItemName ?? string.Empty;
                solution.AmmoProjectileType = prepared.AmmoProjectileType;
                solution.AmmoSlot = prepared.AmmoSlot;
                solution.AmmoShootSpeed = prepared.AmmoShootSpeed;
                solution.AmmoArrowLike = prepared.AmmoArrowLike;
                solution.AmmoBulletLike = prepared.AmmoBulletLike;
                ApplyProjectileRoleMetadata(solution, weapon);

                var specialRule = ResolveSpecialWeaponRule(weapon, solution, defaults);
                if (specialRule != null)
                {
                    return SolveSpecialWeapon(solution, target, weapon, specialRule, effectiveSpeed, extraUpdates);
                }

                string conservativeReason;
                if (ShouldUseConservativeCenter(weapon, out conservativeReason))
                {
                    return Center(solution, "centerConservative", conservativeReason);
                }

                if (projectileType <= 0 || weapon.Shoot <= 0 && weapon.UseAmmo <= 0)
                {
                    return Center(solution, "centerNoProjectile", "noProjectileSemantics");
                }

                if (solution.AmmoArrowLike && !defaults.NoGravity)
                {
                    return SolveArrowGravity(solution, target, effectiveSpeed, extraUpdates);
                }

                if (solution.AmmoBulletLike || IsLikelyStraightHighSpeed(weapon, projectileSpeed, effectiveSpeed))
                {
                    return SolveLinear(solution, target, effectiveSpeed, "linearHighSpeed");
                }

                if (weapon.Magic && projectileSpeed <= 12f)
                {
                    return SolveLinear(solution, target, effectiveSpeed, "linearSlowMagic");
                }

                if ((weapon.Ranged || weapon.Thrown) && projectileSpeed >= 8f)
                {
                    return SolveLinear(solution, target, effectiveSpeed, "linearBasic");
                }

                return Center(solution, "centerUnknownSpecial", "unclassifiedProjectile");
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-ballistic-solver-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimBallisticSolver",
                    "Combat aim ballistic solve failed: " + error.Message);
                solution.Mode = "centerFallback";
                solution.FallbackReason = "solverFailed:" + error.Message;
                return solution;
            }
        }

        private static CombatAimBallisticSolution SolveLinear(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            string mode)
        {
            var leadTicks = EstimateInterceptTicks(
                solution.PlayerCenterX,
                solution.PlayerCenterY,
                target.CenterX,
                target.CenterY,
                solution.TargetVelocityX,
                solution.TargetVelocityY,
                speed);

            leadTicks = Clamp(leadTicks, 0f, MaxLeadTicks);
            solution.LeadTicks = leadTicks;
            solution.PredictedTargetX = target.CenterX + solution.TargetVelocityX * leadTicks;
            solution.PredictedTargetY = target.CenterY + solution.TargetVelocityY * leadTicks;
            solution.AimWorldX = solution.PredictedTargetX;
            solution.AimWorldY = solution.PredictedTargetY;
            solution.Mode = mode;
            solution.Solved = true;
            solution.AimAdjusted = Distance(solution.AimWorldX, solution.AimWorldY, target.CenterX, target.CenterY) > 1f;
            return solution;
        }

        private static CombatAimBallisticSolution SolveSpecialWeapon(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            CombatAimWeaponProfile weapon,
            SpecialWeaponRule rule,
            float effectiveSpeed,
            int extraUpdates)
        {
            if (rule == null)
            {
                return Center(solution, "centerFallback", "missingSpecialRule");
            }

            ApplySpecialMetadata(solution, rule);

            var speed = effectiveSpeed < 0.5f ? DefaultProjectileSpeed : effectiveSpeed;
            if (rule.UseWeaponShootSpeedOnly && weapon != null && weapon.ShootSpeed > 0f)
            {
                speed = weapon.ShootSpeed * Math.Max(1, extraUpdates + 1);
                solution.ProjectileSpeed = weapon.ShootSpeed;
                solution.EffectiveProjectileSpeed = speed;
            }

            if (rule.FixedLeadTicks > 0f)
            {
                return SolvePointAim(solution, target, rule.Mode, rule.FixedLeadTicks);
            }

            if (rule.RainFromSky)
            {
                return SolveRainFromSky(solution, target, rule);
            }

            if (rule.HeavyGravity)
            {
                return SolveHeavyGravity(solution, target, speed, extraUpdates, rule);
            }

            if (rule.ArrowGravity && solution.AmmoArrowLike)
            {
                SolveArrowGravity(solution, target, speed, extraUpdates);
                solution.Mode = rule.Mode;
                solution.SpecialLeadTicks = solution.LeadTicks;
                solution.SpecialAimApplied = true;
                return solution;
            }

            SolveLinear(solution, target, speed, rule.Mode);
            solution.SpecialLeadTicks = solution.LeadTicks;
            solution.SpecialAimApplied = true;
            return solution;
        }

        private static CombatAimBallisticSolution SolveRainFromSky(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            SpecialWeaponRule rule)
        {
            var targetSpeedSq = solution.TargetVelocityX * solution.TargetVelocityX + solution.TargetVelocityY * solution.TargetVelocityY;
            var leadTicks = rule.FixedLeadTicks > 0f
                ? rule.FixedLeadTicks
                : targetSpeedSq > 36f ? 6f : targetSpeedSq > 9f ? 9f : 12f;
            leadTicks = Clamp(leadTicks, 0f, MaxSpecialLeadTicks);

            solution.Mode = rule.Mode;
            solution.LeadTicks = leadTicks;
            solution.SpecialLeadTicks = leadTicks;
            solution.PredictedTargetX = target.CenterX + solution.TargetVelocityX * leadTicks;
            solution.PredictedTargetY = target.CenterY + solution.TargetVelocityY * leadTicks;
            solution.AimWorldX = solution.PredictedTargetX;
            solution.AimWorldY = solution.PredictedTargetY;
            solution.Solved = true;
            solution.SpecialCursorTarget = true;
            solution.SpecialAimApplied = true;
            solution.AimAdjusted = Distance(solution.AimWorldX, solution.AimWorldY, target.CenterX, target.CenterY) > 1f;
            return solution;
        }

        private static CombatAimBallisticSolution SolvePointAim(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            string mode,
            float leadTicks)
        {
            leadTicks = Clamp(leadTicks, 0f, MaxSpecialLeadTicks);
            solution.Mode = mode ?? "specialPointAim";
            solution.LeadTicks = leadTicks;
            solution.SpecialLeadTicks = leadTicks;
            solution.PredictedTargetX = target.CenterX + solution.TargetVelocityX * leadTicks;
            solution.PredictedTargetY = target.CenterY + solution.TargetVelocityY * leadTicks;
            solution.AimWorldX = solution.PredictedTargetX;
            solution.AimWorldY = solution.PredictedTargetY;
            solution.Solved = true;
            solution.SpecialAimApplied = true;
            solution.AimAdjusted = Distance(solution.AimWorldX, solution.AimWorldY, target.CenterX, target.CenterY) > 1f;
            return solution;
        }

        private static CombatAimBallisticSolution SolveHeavyGravity(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            int extraUpdates,
            SpecialWeaponRule rule)
        {
            SolveLinear(solution, target, speed, rule.Mode);
            var activeGravityTicks = Math.Max(0f, solution.LeadTicks - rule.GravityDelayTicks);
            var gravity = rule.GravityPerTick * Math.Max(1, extraUpdates + 1);
            var drop = 0.5f * gravity * activeGravityTicks * activeGravityTicks;
            drop = Clamp(drop, 0f, MaxGravityCompensationPixels);

            solution.GravityPerTick = gravity;
            solution.GravityCompensationPixels = drop;
            solution.AimWorldY -= drop;
            solution.SpecialLeadTicks = solution.LeadTicks;
            solution.SpecialAimApplied = true;
            solution.AimAdjusted = solution.AimAdjusted || drop > 0.5f;
            return solution;
        }

        private static CombatAimBallisticSolution SolveArrowGravity(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            int extraUpdates)
        {
            SolveLinear(solution, target, speed, "arrowGravity");
            var activeGravityTicks = Math.Max(0f, solution.LeadTicks - 10f);
            var gravity = 0.1f * Math.Max(1, extraUpdates + 1);
            var drop = 0.5f * gravity * activeGravityTicks * activeGravityTicks;
            drop = Clamp(drop, 0f, MaxGravityCompensationPixels);

            solution.GravityPerTick = gravity;
            solution.GravityCompensationPixels = drop;
            solution.AimWorldY -= drop;
            solution.AimAdjusted = solution.AimAdjusted || drop > 0.5f;
            return solution;
        }

        private static CombatAimBallisticSolution Center(CombatAimBallisticSolution solution, string mode, string reason)
        {
            solution.Mode = mode ?? string.Empty;
            solution.FallbackReason = reason ?? string.Empty;
            solution.ConservativeCenter = true;
            solution.Solved = true;
            return solution;
        }

        private static float EstimateInterceptTicks(
            float originX,
            float originY,
            float targetX,
            float targetY,
            float targetVelocityX,
            float targetVelocityY,
            float projectileSpeed)
        {
            var speed = projectileSpeed < 0.5f ? DefaultProjectileSpeed : projectileSpeed;
            var rx = targetX - originX;
            var ry = targetY - originY;
            var a = targetVelocityX * targetVelocityX + targetVelocityY * targetVelocityY - speed * speed;
            var b = 2f * (rx * targetVelocityX + ry * targetVelocityY);
            var c = rx * rx + ry * ry;

            if (Math.Abs(a) < 0.0001f)
            {
                if (Math.Abs(b) < 0.0001f)
                {
                    return (float)Math.Sqrt(c) / speed;
                }

                var linear = -c / b;
                return linear > 0f ? linear : (float)Math.Sqrt(c) / speed;
            }

            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return (float)Math.Sqrt(c) / speed;
            }

            var sqrt = (float)Math.Sqrt(discriminant);
            var t1 = (-b - sqrt) / (2f * a);
            var t2 = (-b + sqrt) / (2f * a);
            var result = float.MaxValue;
            if (t1 > 0f)
            {
                result = t1;
            }

            if (t2 > 0f && t2 < result)
            {
                result = t2;
            }

            return result == float.MaxValue ? (float)Math.Sqrt(c) / speed : result;
        }

        private static SpecialWeaponRule ResolveSpecialWeaponRule(
            CombatAimWeaponProfile weapon,
            CombatAimBallisticSolution solution,
            ProjectileDefaults defaults)
        {
            if (weapon == null || weapon.ItemType <= 0)
            {
                return null;
            }

            if (weapon.IsCoinGun)
            {
                return new SpecialWeaponRule
                {
                    Kind = "coinGun",
                    Name = "CoinGun",
                    Rule = "coinLinearWeaponVelocityOnly",
                    Mode = "specialCoinGunLinear",
                    ShotCount = 1,
                    UseWeaponShootSpeedOnly = true
                };
            }

            if (IsRainFromSkyWeapon(weapon.ItemType, out var rainName))
            {
                return new SpecialWeaponRule
                {
                    Kind = "rainFromSky",
                    Name = rainName,
                    Rule = "cursorTargetWithVelocityLead",
                    Mode = "specialRainFromSky",
                    ShotCount = 1,
                    RainFromSky = true,
                    CursorTarget = true
                };
            }

            SpecialWeaponRule multiRule;
            if (TryResolveParallelMultiShotRule(weapon.ItemType, out multiRule))
            {
                return multiRule;
            }

            if (TryResolveSpreadMultiShotRule(weapon.ItemType, out multiRule))
            {
                return multiRule;
            }

            if (IsGuidedCursorWeapon(weapon.ItemType, out var guidedName))
            {
                return new SpecialWeaponRule
                {
                    Kind = "guidedCursor",
                    Name = guidedName,
                    Rule = "shortLeadCursorControl",
                    Mode = "specialGuidedCursor",
                    ShotCount = 1,
                    FixedLeadTicks = 5f,
                    CursorTarget = true
                };
            }

            if (IsHomingProjectile(solution == null ? 0 : solution.ProjectileType, defaults.AiStyle))
            {
                return new SpecialWeaponRule
                {
                    Kind = "homingOrSelfCorrecting",
                    Name = "ProjectileHoming",
                    Rule = "shortLeadPointAim",
                    Mode = "specialHomingPoint",
                    ShotCount = 1,
                    FixedLeadTicks = 5f
                };
            }

            if (IsBeamLikeProjectile(solution == null ? 0 : solution.ProjectileType, defaults.AiStyle))
            {
                return new SpecialWeaponRule
                {
                    Kind = "beamOrInstant",
                    Name = "BeamOrInstant",
                    Rule = "nearInstantPointAim",
                    Mode = "specialBeamOrInstant",
                    ShotCount = 1,
                    FixedLeadTicks = 2f
                };
            }

            if (IsHeavyGravityProjectile(weapon, solution, defaults))
            {
                return new SpecialWeaponRule
                {
                    Kind = "heavyGravity",
                    Name = "HeavyGravityProjectile",
                    Rule = "strongGravityCompensation",
                    Mode = "specialHeavyGravity",
                    ShotCount = 1,
                    HeavyGravity = true,
                    GravityPerTick = defaults.AiStyle == 16 || defaults.AiStyle == 68 ? 0.2f : 0.14f,
                    GravityDelayTicks = defaults.AiStyle == 1 ? 10f : 0f
                };
            }

            return null;
        }

        private static void ApplySpecialMetadata(CombatAimBallisticSolution solution, SpecialWeaponRule rule)
        {
            if (solution == null || rule == null)
            {
                return;
            }

            solution.SpecialWeaponKind = rule.Kind ?? string.Empty;
            solution.SpecialWeaponName = rule.Name ?? string.Empty;
            solution.SpecialWeaponRule = rule.Rule ?? string.Empty;
            solution.SpecialShotCount = rule.ShotCount < 1 ? 1 : rule.ShotCount;
            solution.SpecialSpreadDegrees = rule.SpreadDegrees;
            solution.SpecialParallelSpacingPixels = rule.ParallelSpacingPixels;
            solution.SpecialCursorTarget = rule.CursorTarget;
            solution.SpecialWeaponUsesWeaponShoot = rule.UsesWeaponShoot;
            solution.SpecialWeaponUsesAmmoShoot = rule.UsesAmmoShoot;
            if (rule.UsesWeaponShoot &&
                solution.WeaponShootProjectileType > 0 &&
                solution.WeaponShootProjectileType != solution.ProjectileType)
            {
                solution.SecondaryProjectileType = solution.WeaponShootProjectileType;
                solution.SecondaryProjectileName = solution.WeaponShootProjectileName ?? string.Empty;
                solution.SecondaryProjectileRole = "weaponAssist";
            }

            if (rule.UsesAmmoShoot &&
                solution.PrimaryProjectileType <= 0 &&
                solution.AmmoProjectileType > 0)
            {
                solution.PrimaryProjectileType = solution.AmmoProjectileType;
                solution.PrimaryProjectileName = solution.AmmoProjectileName ?? string.Empty;
                solution.PrimaryProjectileRole = "ammoPrimary";
            }
        }

        private static void ApplyProjectileRoleMetadata(CombatAimBallisticSolution solution, CombatAimWeaponProfile weapon)
        {
            if (solution == null)
            {
                return;
            }

            var weaponShoot = weapon == null ? 0 : weapon.Shoot;
            solution.WeaponShootProjectileType = weaponShoot;
            if (weaponShoot > 0)
            {
                var weaponDefaults = ResolveProjectileDefaults(weaponShoot);
                solution.WeaponShootProjectileName = weaponDefaults.Name ?? string.Empty;
            }

            if (solution.AmmoProjectileType > 0)
            {
                var ammoDefaults = ResolveProjectileDefaults(solution.AmmoProjectileType);
                solution.AmmoProjectileName = ammoDefaults.Name ?? string.Empty;
            }

            if (solution.ProjectileType > 0 &&
                solution.AmmoProjectileType > 0 &&
                solution.ProjectileType == solution.AmmoProjectileType)
            {
                solution.ResolvedProjectileRole = "ammoProjectile";
                solution.PrimaryProjectileType = solution.ProjectileType;
                solution.PrimaryProjectileName = solution.ProjectileName ?? string.Empty;
                solution.PrimaryProjectileRole = "ammoPrimary";
                return;
            }

            if (solution.ProjectileType > 0 && solution.ProjectileType == weaponShoot)
            {
                solution.ResolvedProjectileRole = "weaponShootProjectile";
                solution.PrimaryProjectileType = solution.ProjectileType;
                solution.PrimaryProjectileName = solution.ProjectileName ?? string.Empty;
                solution.PrimaryProjectileRole = "weaponPrimary";
                return;
            }

            solution.ResolvedProjectileRole = solution.ProjectileType > 0 ? "resolvedProjectile" : "none";
            solution.PrimaryProjectileType = solution.ProjectileType;
            solution.PrimaryProjectileName = solution.ProjectileName ?? string.Empty;
            solution.PrimaryProjectileRole = solution.ProjectileType > 0 ? "resolvedPrimary" : "none";
        }

        private static bool ShouldUseConservativeCenter(CombatAimWeaponProfile weapon, out string reason)
        {
            reason = string.Empty;
            if (weapon == null)
            {
                reason = "missingWeapon";
                return true;
            }

            if (weapon.Channel)
            {
                reason = "channelWeapon";
                return true;
            }

            if (weapon.NoUseGraphic && weapon.NoMelee && !weapon.Ranged && !weapon.Magic && !weapon.Thrown)
            {
                reason = "unclassifiedSpecialUse";
                return true;
            }

            return false;
        }

        private static bool IsRainFromSkyWeapon(int itemType, out string name)
        {
            name = string.Empty;
            if (MatchesItemId(itemType, "DaedalusStormbow"))
            {
                name = "DaedalusStormbow";
                return true;
            }

            if (MatchesItemId(itemType, "BloodRainBow"))
            {
                name = "BloodRainBow";
                return true;
            }

            if (MatchesItemId(itemType, "MeteorStaff"))
            {
                name = "MeteorStaff";
                return true;
            }

            if (MatchesItemId(itemType, "Starfury"))
            {
                name = "Starfury";
                return true;
            }

            if (MatchesItemId(itemType, "StarWrath"))
            {
                name = "StarWrath";
                return true;
            }

            if (MatchesItemId(itemType, "LunarFlareBook"))
            {
                name = "LunarFlareBook";
                return true;
            }

            return false;
        }

        private static bool TryResolveParallelMultiShotRule(int itemType, out SpecialWeaponRule rule)
        {
            rule = null;
            if (MatchesItemId(itemType, "Tsunami"))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "Tsunami", "parallelArrowLeadGravity", "specialParallelMultiShot", 5, 0f, 9f, true);
                return true;
            }

            if (MatchesItemId(itemType, "ChlorophyteShotbow"))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "ChlorophyteShotbow", "parallelArrowLeadGravity", "specialParallelMultiShot", 3, 0f, 8f, true);
                return true;
            }

            if (MatchesItemId(itemType, "Phantasm"))
            {
                rule = CreateMultiShotRule("parallelMultiShot", "Phantasm", "parallelArrowLeadGravityPlusHoming", "specialParallelMultiShot", 4, 0f, 7f, true);
                return true;
            }

            return false;
        }

        private static bool TryResolveSpreadMultiShotRule(int itemType, out SpecialWeaponRule rule)
        {
            rule = null;
            if (MatchesItemId(itemType, "Xenopopper"))
            {
                rule = CreateMultiShotRule("cursorSpawnBurst", "Xenopopper", "cursorSpawnBubbleBullet", "specialCursorSpawnBurst", 4, 0f, 0f, false);
                rule.CursorTarget = true;
                rule.FixedLeadTicks = 4f;
                rule.UsesWeaponShoot = true;
                rule.UsesAmmoShoot = true;
                return true;
            }

            if (MatchesItemId(itemType, "Shotgun"))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "Shotgun", "spreadBulletLead", "specialSpreadMultiShot", 4, 10f, 0f, false);
                return true;
            }

            if (MatchesItemId(itemType, "Boomstick"))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "Boomstick", "spreadBulletLead", "specialSpreadMultiShot", 4, 14f, 0f, false);
                return true;
            }

            if (MatchesItemId(itemType, "QuadBarrelShotgun"))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "QuadBarrelShotgun", "spreadBulletLead", "specialSpreadMultiShot", 6, 18f, 0f, false);
                return true;
            }

            if (MatchesItemId(itemType, "OnyxBlaster"))
            {
                rule = CreateMultiShotRule("spreadMultiShot", "OnyxBlaster", "spreadBulletLeadWithDarkBolt", "specialSpreadMultiShot", 5, 12f, 0f, false);
                rule.UsesWeaponShoot = true;
                rule.UsesAmmoShoot = true;
                return true;
            }

            if (MatchesItemId(itemType, "VortexBeater"))
            {
                rule = CreateMultiShotRule("dualProjectileSpread", "VortexBeater", "bulletSpreadWithRocketAssist", "specialSpreadMultiShot", 3, 8f, 0f, false);
                rule.UsesWeaponShoot = true;
                rule.UsesAmmoShoot = true;
                return true;
            }

            return false;
        }

        private static SpecialWeaponRule CreateMultiShotRule(
            string kind,
            string name,
            string rule,
            string mode,
            int shotCount,
            float spreadDegrees,
            float parallelSpacingPixels,
            bool arrowGravity)
        {
            return new SpecialWeaponRule
            {
                Kind = kind,
                Name = name,
                Rule = rule,
                Mode = mode,
                ShotCount = shotCount,
                SpreadDegrees = spreadDegrees,
                ParallelSpacingPixels = parallelSpacingPixels,
                ArrowGravity = arrowGravity
            };
        }

        private static bool IsGuidedCursorWeapon(int itemType, out string name)
        {
            name = string.Empty;
            if (MatchesItemId(itemType, "MagicMissile"))
            {
                name = "MagicMissile";
                return true;
            }

            if (MatchesItemId(itemType, "Flamelash"))
            {
                name = "Flamelash";
                return true;
            }

            if (MatchesItemId(itemType, "RainbowRod"))
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

            return MatchesProjectileId(projectileType, "ChlorophyteBullet") ||
                   MatchesProjectileId(projectileType, "ChlorophyteArrow") ||
                   MatchesProjectileId(projectileType, "VortexBeaterRocket") ||
                   MatchesProjectileId(projectileType, "PhantasmArrow") ||
                   MatchesProjectileId(projectileType, "SpectreWrath") ||
                   MatchesProjectileId(projectileType, "Bat") ||
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
                   MatchesProjectileId(projectileType, "HeatRay") ||
                   MatchesProjectileId(projectileType, "LaserMachinegunLaser") ||
                   MatchesProjectileId(projectileType, "MartianWalkerLaser") ||
                   MatchesProjectileId(projectileType, "LastPrismLaser");
        }

        private static bool IsHeavyGravityProjectile(CombatAimWeaponProfile weapon, CombatAimBallisticSolution solution, ProjectileDefaults defaults)
        {
            if (weapon == null || solution == null || solution.ProjectileType <= 0 || defaults.NoGravity)
            {
                return false;
            }

            if (defaults.AiStyle == 2 || defaults.AiStyle == 16 || defaults.AiStyle == 68)
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

        private static bool IsLikelyStraightHighSpeed(CombatAimWeaponProfile weapon, float projectileSpeed, float effectiveSpeed)
        {
            if (weapon == null)
            {
                return false;
            }

            if (weapon.Ranged && projectileSpeed >= 10f)
            {
                return true;
            }

            return effectiveSpeed >= 14f && !weapon.Melee;
        }

        private static bool IsArrowLikeAmmo(CombatAimWeaponProfile weapon, AmmoSnapshot ammo, ProjectileDefaults defaults)
        {
            if (defaults.HasValue && defaults.Arrow)
            {
                return true;
            }

            var weaponUseAmmo = weapon == null ? 0 : weapon.UseAmmo;
            if (weaponUseAmmo > 0 &&
                (weaponUseAmmo == ReadAmmoId("Arrow") ||
                 weaponUseAmmo == ReadAmmoId("Stake") ||
                 weaponUseAmmo == ReadAmmoId("Dart")))
            {
                return true;
            }

            return ammo != null &&
                   (ammo.AmmoType == ReadAmmoId("Arrow") ||
                    ammo.AmmoType == ReadAmmoId("Stake") ||
                    ammo.AmmoType == ReadAmmoId("Dart"));
        }

        private static bool IsBulletLikeAmmo(CombatAimWeaponProfile weapon, AmmoSnapshot ammo)
        {
            var bullet = ReadAmmoId("Bullet");
            if (bullet <= 0)
            {
                return false;
            }

            return weapon != null && weapon.UseAmmo == bullet ||
                   ammo != null && ammo.AmmoType == bullet;
        }

        private static int ResolveProjectileType(CombatAimWeaponProfile weapon, AmmoSnapshot ammo)
        {
            if (weapon == null)
            {
                return 0;
            }

            if (ammo != null && ammo.Shoot > 0)
            {
                return ammo.Shoot;
            }

            return weapon.Shoot;
        }

        private static float ResolveProjectileSpeed(CombatAimWeaponProfile weapon, AmmoSnapshot ammo)
        {
            var speed = weapon == null || weapon.ShootSpeed <= 0f ? DefaultProjectileSpeed : weapon.ShootSpeed;
            if (ammo != null && ammo.ShootSpeed > 0f && !ReadItemSetFlag("gunProj", weapon == null ? 0 : weapon.ItemType))
            {
                speed += ammo.ShootSpeed;
            }

            return speed < 0.5f ? DefaultProjectileSpeed : speed;
        }

        private static bool TryReadPlayerCenter(object player, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (player == null)
            {
                return false;
            }

            if (GameStateReflection.TryReadVector2(GameStateReflection.GetMember(player, "Center"), out x, out y))
            {
                return true;
            }

            float positionX;
            float positionY;
            if (!GameStateReflection.TryReadVector2(GameStateReflection.GetMember(player, "position"), out positionX, out positionY))
            {
                return false;
            }

            int width;
            int height;
            GameStateReflection.TryGetInt(player, "width", out width);
            GameStateReflection.TryGetInt(player, "height", out height);
            x = positionX + Math.Max(1, width) / 2f;
            y = positionY + Math.Max(1, height) / 2f;
            return true;
        }

        private static bool TryFindAmmo(object player, int ammoType, int coinAmmoType, out AmmoSnapshot ammo)
        {
            ammo = null;
            if (player == null || ammoType <= 0)
            {
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null)
            {
                return false;
            }

            if (ammoType == coinAmmoType &&
                TryFindAmmoInRange(inventory, ammoType, true, CoinInventoryStart, CoinInventoryEndExclusive, out ammo))
            {
                return true;
            }

            if (TryFindAmmoInRange(inventory, ammoType, false, AmmoInventoryStart, AmmoInventoryEndExclusive, out ammo))
            {
                return true;
            }

            return TryFindAmmoInRange(inventory, ammoType, ammoType == coinAmmoType, 0, MainInventoryEndExclusive, out ammo);
        }

        private static bool TryFindAmmoInRange(IList inventory, int ammoType, bool allowCoinItemType, int start, int endExclusive, out AmmoSnapshot ammo)
        {
            ammo = null;
            if (inventory == null || start < 0)
            {
                return false;
            }

            var end = inventory.Count < endExclusive ? inventory.Count : endExclusive;
            for (var index = start; index < end; index++)
            {
                var item = inventory[index];
                if (item == null)
                {
                    continue;
                }

                var itemType = ReadInt(item, "type", 0);
                var stack = ReadInt(item, "stack", 0);
                if (itemType <= 0 || stack <= 0)
                {
                    continue;
                }

                var itemAmmo = ReadInt(item, "ammo", 0);
                if (itemAmmo != ammoType && !(allowCoinItemType && IsVanillaCoinItemType(itemType)))
                {
                    continue;
                }

                ammo = new AmmoSnapshot
                {
                    Slot = index,
                    ItemType = itemType,
                    ItemName = ReadItemName(item),
                    AmmoType = itemAmmo,
                    Shoot = ReadInt(item, "shoot", 0),
                    ShootSpeed = ReadFloat(item, "shootSpeed", 0f)
                };
                return true;
            }

            return false;
        }

        private static ProjectileDefaults ResolveProjectileDefaults(int projectileType)
        {
            if (projectileType <= 0)
            {
                return ProjectileDefaults.Empty;
            }

            lock (CacheSync)
            {
                ProjectileDefaults cached;
                if (ProjectileDefaultsCache.TryGetValue(projectileType, out cached))
                {
                    return cached;
                }
            }

            var resolved = ReadProjectileDefaults(projectileType);
            lock (CacheSync)
            {
                ProjectileDefaultsCache[projectileType] = resolved;
            }

            return resolved;
        }

        private static ProjectileDefaults FromPreparedDefaults(CombatAimBallisticContext prepared)
        {
            if (prepared == null)
            {
                return ProjectileDefaults.Empty;
            }

            return new ProjectileDefaults
            {
                HasValue = prepared.ProjectileDefaultsAvailable,
                Name = prepared.ProjectileName ?? string.Empty,
                ExtraUpdates = prepared.ProjectileExtraUpdates,
                Arrow = prepared.ProjectileArrow,
                AiStyle = prepared.ProjectileAiStyle,
                NoGravity = prepared.ProjectileNoGravity,
                TileCollide = prepared.ProjectileTileCollide,
                Width = prepared.ProjectileWidth,
                Height = prepared.ProjectileHeight,
                Friendly = prepared.ProjectileFriendly,
                Hostile = prepared.ProjectileHostile
            };
        }

        private static ProjectileDefaults ReadProjectileDefaults(int projectileType)
        {
            try
            {
                var projectileTypeObject = FindType("Terraria.Projectile");
                if (projectileTypeObject == null)
                {
                    return ProjectileDefaults.Empty;
                }

                var projectile = Activator.CreateInstance(projectileTypeObject);
                var setDefaults = projectileTypeObject.GetMethod("SetDefaults", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (setDefaults == null)
                {
                    return ProjectileDefaults.Empty;
                }

                setDefaults.Invoke(projectile, new object[] { projectileType });
                return new ProjectileDefaults
                {
                    HasValue = true,
                    Name = ReadProjectileName(projectile),
                    ExtraUpdates = ReadInt(projectile, "extraUpdates", 0),
                    Arrow = ReadBool(projectile, "arrow", false),
                    AiStyle = ReadInt(projectile, "aiStyle", 0),
                    Width = ReadInt(projectile, "width", 2),
                    Height = ReadInt(projectile, "height", 2),
                    TileCollide = ReadBool(projectile, "tileCollide", true),
                    NoGravity = ReadBool(projectile, "noGravity", false),
                    Friendly = ReadBool(projectile, "friendly", false),
                    Hostile = ReadBool(projectile, "hostile", false)
                };
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-projectile-defaults-failed-" + projectileType.ToString(CultureInfo.InvariantCulture),
                    TimeSpan.FromSeconds(30),
                    "CombatAimBallisticSolver",
                    "Projectile defaults unavailable for type " + projectileType.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                return ProjectileDefaults.Empty;
            }
        }

        private static string ReadItemName(object item)
        {
            try
            {
                var name = GameStateReflection.GetMember(item, "Name") ??
                           GameStateReflection.GetMember(item, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadProjectileName(object projectile)
        {
            try
            {
                var name = GameStateReflection.GetMember(projectile, "Name") ??
                           GameStateReflection.GetMember(projectile, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ReadItemId(string memberName)
        {
            return ResolveStaticInt("Terraria.ID.ItemID", memberName);
        }

        private static int ReadAmmoId(string memberName)
        {
            return ResolveStaticInt("Terraria.ID.AmmoID", memberName);
        }

        private static int ReadProjectileId(string memberName)
        {
            return ResolveStaticInt("Terraria.ID.ProjectileID", memberName);
        }

        private static bool MatchesItemId(int itemType, string memberName)
        {
            var id = ReadItemId(memberName);
            if (id <= 0)
            {
                id = ReadKnownItemId(memberName);
            }

            return id > 0 && itemType == id;
        }

        private static int ReadKnownItemId(string memberName)
        {
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

            return 0;
        }

        private static bool MatchesProjectileId(int projectileType, string memberName)
        {
            var id = ReadProjectileId(memberName);
            return id > 0 && projectileType == id;
        }

        private static bool MatchesAmmoId(int ammoType, string memberName)
        {
            var id = ReadAmmoId(memberName);
            return id > 0 && ammoType == id;
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

        private static bool ReadItemSetFlag(string memberName, int index)
        {
            if (index < 0)
            {
                return false;
            }

            var boolArray = ResolveItemSetBoolArray(memberName);
            return boolArray != null && index < boolArray.Length && boolArray[index];
        }

        private static bool[] ResolveItemSetBoolArray(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            lock (CacheSync)
            {
                bool[] cached;
                if (ItemSetBoolCache.TryGetValue(memberName, out cached))
                {
                    return cached;
                }
            }

            bool[] resolved = null;
            try
            {
                var type = FindType("Terraria.ID.ItemID+Sets");
                if (type != null)
                {
                    resolved = GameStateReflection.GetStaticMember(type, memberName) as bool[];
                }
            }
            catch
            {
                resolved = null;
            }

            lock (CacheSync)
            {
                ItemSetBoolCache[memberName] = resolved;
            }

            return resolved;
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(source, name, out value) ? value : fallback;
        }

        private static float ReadFloat(object source, string name, float fallback)
        {
            float value;
            return GameStateReflection.TryGetFloat(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(source, name, out value) ? value : fallback;
        }

        private static bool IsVanillaCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class AmmoSnapshot
        {
            public int Slot;
            public int ItemType;
            public string ItemName;
            public int AmmoType;
            public int Shoot;
            public float ShootSpeed;
        }

        private sealed class SpecialWeaponRule
        {
            public string Kind;
            public string Name;
            public string Rule;
            public string Mode;
            public int ShotCount;
            public float SpreadDegrees;
            public float ParallelSpacingPixels;
            public float FixedLeadTicks;
            public float GravityPerTick;
            public float GravityDelayTicks;
            public bool ArrowGravity;
            public bool RainFromSky;
            public bool HeavyGravity;
            public bool CursorTarget;
            public bool UseWeaponShootSpeedOnly;
            public bool UsesWeaponShoot;
            public bool UsesAmmoShoot;

            public SpecialWeaponRule()
            {
                Kind = string.Empty;
                Name = string.Empty;
                Rule = string.Empty;
                Mode = string.Empty;
                ShotCount = 1;
            }
        }

        private struct ProjectileDefaults
        {
            public static readonly ProjectileDefaults Empty = new ProjectileDefaults();

            public bool HasValue;
            public string Name;
            public int ExtraUpdates;
            public bool Arrow;
            public int AiStyle;
            public int Width;
            public int Height;
            public bool TileCollide;
            public bool NoGravity;
            public bool Friendly;
            public bool Hostile;
        }
    }
}
