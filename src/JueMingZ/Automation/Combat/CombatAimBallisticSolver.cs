using System;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    // Ballistic solving is pure aim math; unavailable player, weapon, or target data returns no solution.
    public static class CombatAimBallisticSolver
    {
        private const float DefaultProjectileSpeed = 8f;
        private const float MaxLeadTicks = 45f;
        private const float MaxSpecialLeadTicks = 36f;
        private const float MaxHighSpeedLeadTicks = 24f;
        private const float MaxMediumLeadTicks = 48f;
        private const float MaxSlowLeadTicks = 96f;
        private const float MaxGravityLeadTicks = 72f;
        private const float MaxReturningLeadTicks = 30f;
        private const float MaxSpreadLeadTicks = 18f;
        private const float MaxGravityCompensationPixels = 180f;

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

                var projectileProfile = CombatAimProjectileProfileResolver.Resolve(player, weapon);

                context.ProjectileProfile = projectileProfile;
                context.ProjectileType = projectileProfile.ProjectileType;
                context.ProjectileName = projectileProfile.ProjectileName ?? string.Empty;
                context.ProjectileAiStyle = projectileProfile.ProjectileAiStyle;
                context.ProjectileExtraUpdates = projectileProfile.ProjectileExtraUpdates;
                context.ProjectileDefaultsAvailable = projectileProfile.ProjectileDefaultsAvailable;
                context.ProjectileNoGravity = projectileProfile.ProjectileNoGravity;
                context.ProjectileArrow = projectileProfile.ProjectileArrow;
                context.ProjectileTileCollide = projectileProfile.ProjectileTileCollide;
                context.ProjectileWidth = projectileProfile.ProjectileWidth;
                context.ProjectileHeight = projectileProfile.ProjectileHeight;
                context.ProjectileFriendly = projectileProfile.ProjectileFriendly;
                context.ProjectileHostile = projectileProfile.ProjectileHostile;
                context.BaseProjectileSpeed = projectileProfile.BaseProjectileSpeed;
                context.ProjectileSpeed = projectileProfile.BaseProjectileSpeed;
                context.EffectiveProjectileSpeed = projectileProfile.EffectiveProjectileSpeed;
                context.EffectiveUpdatesPerTick = projectileProfile.EffectiveUpdatesPerTick;
                context.GravityPerTickCandidate = projectileProfile.GravityPerTickCandidate;
                context.ProjectileRadiusForHit = projectileProfile.ProjectileRadiusForHit;
                context.ProfileFamilyHint = projectileProfile.ProfileFamilyHint ?? string.Empty;
                context.ProfileCompleteness = projectileProfile.ProfileCompleteness ?? string.Empty;
                context.ProfileFallbackReason = projectileProfile.ProfileFallbackReason ?? string.Empty;
                context.ProfileSpeedSource = projectileProfile.ProfileSpeedSource ?? string.Empty;
                context.ProfileGunProj = projectileProfile.GunProj;
                context.ProfileAmmoSpeedApplied = projectileProfile.AmmoSpeedApplied;
                context.ProfileMagicQuiverApplied = projectileProfile.MagicQuiverApplied;
                context.ProfileArcheryApplied = projectileProfile.ArcheryApplied;
                context.ProfileArcherySpeedCapped = projectileProfile.ArcherySpeedCapped;
                context.ProfileMagicQuiverEffectiveUpdateApplied = projectileProfile.MagicQuiverEffectiveUpdateApplied;
                context.ProfileSpecificLauncherAmmoProjectileMatch = projectileProfile.SpecificLauncherAmmoProjectileMatch;
                context.ProfileProjectileTransformRole = projectileProfile.ProjectileTransformRole ?? string.Empty;
                context.AmmoAvailable = projectileProfile.AmmoAvailable;
                context.AmmoType = projectileProfile.AmmoType;
                context.AmmoItemType = projectileProfile.AmmoItemType;
                context.AmmoItemName = projectileProfile.AmmoItemName ?? string.Empty;
                context.AmmoProjectileType = projectileProfile.AmmoProjectileType;
                context.AmmoSlot = projectileProfile.AmmoSlot;
                context.AmmoShootSpeed = projectileProfile.AmmoShootSpeed;
                context.AmmoArrowLike = projectileProfile.AmmoArrowLike;
                context.AmmoBulletLike = projectileProfile.AmmoBulletLike;
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

                ApplyTargetMotionMetadata(solution, target);
                ApplyTargetVelocity(solution, target);
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

                var projectileProfile = prepared.ProjectileProfile;
                var projectileType = prepared.ProjectileType;
                var projectileSpeed = prepared.ProjectileSpeed;
                var effectiveUpdatesPerTick = prepared.EffectiveUpdatesPerTick <= 0
                    ? Math.Max(1, prepared.ProjectileExtraUpdates + 1)
                    : prepared.EffectiveUpdatesPerTick;
                var extraUpdates = Math.Max(0, effectiveUpdatesPerTick - 1);
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
                solution.BaseProjectileSpeed = prepared.BaseProjectileSpeed;
                solution.ProjectileSpeed = prepared.ProjectileSpeed;
                solution.EffectiveProjectileSpeed = prepared.EffectiveProjectileSpeed;
                solution.EffectiveUpdatesPerTick = effectiveUpdatesPerTick;
                solution.GravityPerTickCandidate = prepared.GravityPerTickCandidate;
                solution.ProjectileRadiusForHit = prepared.ProjectileRadiusForHit;
                solution.ProjectileProfileFamily = prepared.ProfileFamilyHint ?? string.Empty;
                solution.ProjectileProfileStatus = prepared.ProfileCompleteness ?? string.Empty;
                solution.ProjectileProfileDegradedReason = prepared.ProfileFallbackReason ?? string.Empty;
                solution.ProjectileProfileSpeedSource = prepared.ProfileSpeedSource ?? string.Empty;
                solution.ProjectileProfileGunProj = prepared.ProfileGunProj;
                solution.ProjectileProfileAmmoSpeedApplied = prepared.ProfileAmmoSpeedApplied;
                solution.ProjectileProfileMagicQuiverApplied = prepared.ProfileMagicQuiverApplied;
                solution.ProjectileProfileArcheryApplied = prepared.ProfileArcheryApplied;
                solution.ProjectileProfileArcherySpeedCapped = prepared.ProfileArcherySpeedCapped;
                solution.ProjectileProfileMagicQuiverEffectiveUpdateApplied = prepared.ProfileMagicQuiverEffectiveUpdateApplied;
                solution.ProjectileProfileSpecificLauncherAmmoProjectileMatch = prepared.ProfileSpecificLauncherAmmoProjectileMatch;
                solution.ProjectileProfileTransformRole = prepared.ProfileProjectileTransformRole ?? string.Empty;
                solution.AmmoAvailable = prepared.AmmoAvailable;
                solution.AmmoType = prepared.AmmoType;
                solution.AmmoItemType = prepared.AmmoItemType;
                solution.AmmoItemName = prepared.AmmoItemName ?? string.Empty;
                solution.AmmoProjectileType = prepared.AmmoProjectileType;
                solution.AmmoSlot = prepared.AmmoSlot;
                solution.AmmoShootSpeed = prepared.AmmoShootSpeed;
                solution.AmmoArrowLike = prepared.AmmoArrowLike;
                solution.AmmoBulletLike = prepared.AmmoBulletLike;
                ApplyProjectileRoleMetadata(solution, weapon, projectileProfile);

                var specialRule = ResolveSpecialWeaponRule(weapon, solution, projectileProfile);
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

                var strategy = SelectStrategy(weapon, solution, projectileSpeed, effectiveSpeed);
                return SolveStrategy(solution, target, strategy, effectiveSpeed);
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

        private static void ApplyTargetMotionMetadata(CombatAimBallisticSolution solution, CombatTargetSnapshot target)
        {
            if (solution == null || target == null)
            {
                return;
            }

            solution.TargetNpcAiStyle = target.NpcAiStyle;
            solution.TargetNoGravity = target.NoGravity;
            solution.TargetCollideX = target.CollideX;
            solution.TargetCollideY = target.CollideY;

            var profile = target.MotionProfile;
            if (profile == null)
            {
                solution.TargetMotionProfileKind = CombatAimTargetMotionProfile.Unknown;
                solution.TargetHistoryResetReason = string.Empty;
                return;
            }

            solution.TargetMotionProfileKind = profile.MotionProfileKind ?? CombatAimTargetMotionProfile.Unknown;
            solution.TargetMotionConfidence = profile.MotionConfidence;
            solution.TargetVelocityConfidence = profile.VelocityConfidence;
            solution.TargetAccelerationX = profile.AccelerationX;
            solution.TargetAccelerationY = profile.AccelerationY;
            solution.TargetAccelerationConfidence = profile.AccelerationConfidence;
            solution.TargetRecommendedLeadScale = profile.RecommendedLeadScale;
            solution.TargetRecommendedMaxLeadTicks = profile.RecommendedMaxLeadTicks;
            solution.TargetPreferCurrentVelocity = profile.PreferCurrentVelocity;
            solution.TargetPreferSmoothedVelocity = profile.PreferSmoothedVelocity;
            solution.TargetHistoryResetReason = profile.HistoryResetReason ?? string.Empty;
        }

        private static void ApplyTargetVelocity(CombatAimBallisticSolution solution, CombatTargetSnapshot target)
        {
            if (solution == null || target == null)
            {
                return;
            }

            if (solution.TargetPreferCurrentVelocity)
            {
                solution.TargetVelocityX = target.VelocityX;
                solution.TargetVelocityY = target.VelocityY;
                return;
            }

            if (solution.TargetPreferSmoothedVelocity && target.SmoothedVelocityAvailable)
            {
                solution.TargetVelocityX = target.SmoothedVelocityX;
                solution.TargetVelocityY = target.SmoothedVelocityY;
                return;
            }

            solution.TargetVelocityX = target.SmoothedVelocityAvailable ? target.SmoothedVelocityX : target.VelocityX;
            solution.TargetVelocityY = target.SmoothedVelocityAvailable ? target.SmoothedVelocityY : target.VelocityY;
        }

        private static BallisticStrategy SelectStrategy(
            CombatAimWeaponProfile weapon,
            CombatAimBallisticSolution solution,
            float projectileSpeed,
            float effectiveSpeed)
        {
            if (solution == null)
            {
                return BallisticStrategy.Fallback("centerFallback", "solutionUnavailable");
            }

            var family = solution.ProjectileProfileFamily ?? string.Empty;
            if (IsProfileFamily(family, "ReleaseControlled"))
            {
                return BallisticStrategy.Fallback("centerConservative", "releaseControlledWeapon");
            }

            if (IsProfileFamily(family, "InstantOrBeam") ||
                IsProfileFamily(family, "GuidedCursor") ||
                IsProfileFamily(family, "HomingOrSelfCorrecting"))
            {
                return BallisticStrategy.Point("pointShortLead", 5f);
            }

            if (IsProfileFamily(family, "SpreadOrMultiShot"))
            {
                return BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.Spread,
                    "spreadCoverageLead",
                    CombatAimLeadWindowKinds.SpreadCoverage,
                    MaxSpreadLeadTicks);
            }

            if (IsProfileFamily(family, "Returning") || solution.ProjectileAiStyle == 3)
            {
                return BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.ReturningProjectile,
                    "returningOutboundLead",
                    CombatAimLeadWindowKinds.ReturningOutbound,
                    MaxReturningLeadTicks);
            }

            if (IsProfileFamily(family, "GravityArc") ||
                solution.GravityPerTickCandidate > 0f ||
                solution.AmmoArrowLike && !solution.ProjectileNoGravity)
            {
                return BallisticStrategy.Gravity("arrowGravity", MaxGravityLeadTicks, 10f, 0f);
            }

            if (IsProfileFamily(family, "HighSpeedLinear") ||
                solution.AmmoBulletLike ||
                IsLikelyStraightHighSpeed(weapon, projectileSpeed, effectiveSpeed))
            {
                return BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.LinearIntercept,
                    "linearHighSpeed",
                    CombatAimLeadWindowKinds.HighSpeedShort,
                    MaxHighSpeedLeadTicks);
            }

            if (IsProfileFamily(family, "SlowLinear") ||
                weapon != null && weapon.Magic && projectileSpeed <= 12f)
            {
                return BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.SlowProjectile,
                    weapon != null && weapon.Magic ? "linearSlowMagic" : "linearSlowProjectile",
                    CombatAimLeadWindowKinds.SlowLong,
                    MaxSlowLeadTicks);
            }

            if (IsProfileFamily(family, "MediumLinear") ||
                weapon != null && (weapon.Ranged || weapon.Thrown) && projectileSpeed >= 8f)
            {
                return BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.LinearIntercept,
                    "linearBasic",
                    CombatAimLeadWindowKinds.Medium,
                    MaxMediumLeadTicks);
            }

            return BallisticStrategy.Fallback("centerUnknownSpecial", "unclassifiedProjectile");
        }

        private static CombatAimBallisticSolution SolveStrategy(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            BallisticStrategy strategy,
            float speed)
        {
            if (string.Equals(strategy.SolverKind, CombatAimBallisticSolverKinds.FallbackCenter, StringComparison.Ordinal))
            {
                return Center(solution, strategy.Mode, strategy.FallbackReason);
            }

            if (strategy.FixedLeadTicks > 0f)
            {
                return SolvePointAim(solution, target, strategy);
            }

            if (strategy.UseGravity)
            {
                return SolveGravityArc(solution, target, speed, strategy);
            }

            return SolveLinear(solution, target, speed, strategy);
        }

        private static CombatAimBallisticSolution SolveLinear(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            BallisticStrategy strategy)
        {
            ApplyStrategyMetadata(solution, strategy);
            var rawLeadTicks = EstimateInterceptTicks(
                solution.PlayerCenterX,
                solution.PlayerCenterY,
                target.CenterX,
                target.CenterY,
                solution.TargetVelocityX,
                solution.TargetVelocityY,
                speed);

            var leadClamp = ResolveLeadClamp(solution, rawLeadTicks, strategy);
            ApplyLead(solution, target, leadClamp);
            solution.Mode = strategy.Mode;
            solution.Solved = true;
            solution.AimAdjusted = Distance(solution.AimWorldX, solution.AimWorldY, target.CenterX, target.CenterY) > 1f;
            return solution;
        }

        private static CombatAimBallisticSolution SolveGravityArc(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            BallisticStrategy strategy)
        {
            SolveLinear(solution, target, speed, strategy);
            var gravityDelayTicks = strategy.GravityDelayTicks < 0f ? 0f : strategy.GravityDelayTicks;
            var activeGravityTicks = Math.Max(0f, solution.LeadTicks - gravityDelayTicks);
            var gravityCandidate = strategy.GravityPerTick > 0f
                ? strategy.GravityPerTick
                : solution.GravityPerTickCandidate > 0f
                    ? solution.GravityPerTickCandidate
                    : solution.AmmoArrowLike ? 0.1f : 0.14f;
            var gravity = gravityCandidate * Math.Max(1, solution.EffectiveUpdatesPerTick);
            var rawDrop = 0.5f * gravity * activeGravityTicks * activeGravityTicks;
            var drop = Clamp(rawDrop, 0f, MaxGravityCompensationPixels);

            solution.GravityDelayTicks = gravityDelayTicks;
            solution.GravityPerTick = gravity;
            solution.GravityCompensationPixels = drop;
            solution.AimWorldY -= drop;
            if (rawDrop > drop + 0.001f)
            {
                solution.LeadClampReason = CombatAimLeadClampReasons.GravityCompensationCap;
                solution.LeadClamped = true;
            }

            solution.AimAdjusted = solution.AimAdjusted || drop > 0.5f;
            return solution;
        }

        private static void ApplyStrategyMetadata(CombatAimBallisticSolution solution, BallisticStrategy strategy)
        {
            if (solution == null)
            {
                return;
            }

            solution.SolverKind = strategy.SolverKind ?? CombatAimBallisticSolverKinds.FallbackCenter;
            solution.LeadWindowKind = strategy.LeadWindowKind ?? CombatAimLeadWindowKinds.Fallback;
            solution.PredictionConfidence = ResolvePredictionConfidence(solution);
            if (string.IsNullOrWhiteSpace(solution.LeadClampReason))
            {
                solution.LeadClampReason = CombatAimLeadClampReasons.None;
            }
        }

        private static LeadClamp ResolveLeadClamp(
            CombatAimBallisticSolution solution,
            float rawLeadTicks,
            BallisticStrategy strategy)
        {
            var rawLead = float.IsNaN(rawLeadTicks) || float.IsInfinity(rawLeadTicks) ? 0f : Math.Max(0f, rawLeadTicks);
            var scale = ResolveLeadScale(solution, strategy);
            var scaledLead = rawLead * scale;
            var window = ResolveLeadWindow(solution, strategy);
            var maxLead = window.MaxTicks <= 0f ? MaxLeadTicks : window.MaxTicks;
            var clampedLead = Clamp(scaledLead, 0f, maxLead);
            var reason = CombatAimLeadClampReasons.None;
            var clamped = false;

            if (scale < 0.999f && rawLead > scaledLead + 0.001f)
            {
                reason = CombatAimLeadClampReasons.MotionLeadScale;
                clamped = true;
            }

            if (scaledLead > maxLead + 0.001f)
            {
                reason = string.IsNullOrWhiteSpace(window.ClampReason)
                    ? CombatAimLeadClampReasons.ProjectileFamilyWindow
                    : window.ClampReason;
                clamped = true;
            }

            return new LeadClamp
            {
                RawLeadTicks = rawLead,
                LeadTicks = clampedLead,
                LeadScale = scale,
                MaxLeadTicks = maxLead,
                LeadClampReason = clamped ? reason : CombatAimLeadClampReasons.None,
                LeadClamped = clamped
            };
        }

        private static void ApplyLead(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            LeadClamp leadClamp)
        {
            solution.RawLeadTicks = leadClamp.RawLeadTicks;
            solution.LeadScale = leadClamp.LeadScale;
            solution.LeadWindowMaxTicks = leadClamp.MaxLeadTicks;
            solution.LeadTicks = leadClamp.LeadTicks;
            solution.LeadClampReason = leadClamp.LeadClampReason ?? CombatAimLeadClampReasons.None;
            solution.LeadClamped = leadClamp.LeadClamped;
            solution.PredictedTargetX = target.CenterX + solution.TargetVelocityX * leadClamp.LeadTicks;
            solution.PredictedTargetY = target.CenterY + solution.TargetVelocityY * leadClamp.LeadTicks;
            solution.AimWorldX = solution.PredictedTargetX;
            solution.AimWorldY = solution.PredictedTargetY;
        }

        private static float ResolveLeadScale(CombatAimBallisticSolution solution, BallisticStrategy strategy)
        {
            if (solution == null)
            {
                return 1f;
            }

            var scale = solution.TargetRecommendedLeadScale <= 0f ? 1f : solution.TargetRecommendedLeadScale;
            scale = Clamp(scale, 0.2f, 1f);
            if (string.Equals(strategy.SolverKind, CombatAimBallisticSolverKinds.SlowProjectile, StringComparison.Ordinal) &&
                AllowsLongSlowLead(solution))
            {
                return Math.Max(0.85f, scale);
            }

            return scale;
        }

        private static LeadWindow ResolveLeadWindow(CombatAimBallisticSolution solution, BallisticStrategy strategy)
        {
            var maxLead = strategy.MaxLeadTicks <= 0f ? MaxLeadTicks : strategy.MaxLeadTicks;
            var clampReason = CombatAimLeadClampReasons.ProjectileFamilyWindow;
            if (solution == null)
            {
                return new LeadWindow(maxLead, clampReason);
            }

            if (string.Equals(strategy.SolverKind, CombatAimBallisticSolverKinds.SlowProjectile, StringComparison.Ordinal))
            {
                if (!IsProjectileProfileComplete(solution))
                {
                    return new LeadWindow(Math.Min(maxLead, MaxLeadTicks), CombatAimLeadClampReasons.ProjectileProfileDegraded);
                }

                if (AllowsLongSlowLead(solution))
                {
                    if (string.Equals(solution.TargetMotionProfileKind, CombatAimTargetMotionProfile.LargeOrSegmented, StringComparison.Ordinal))
                    {
                        return new LeadWindow(Math.Min(maxLead, 72f), CombatAimLeadClampReasons.MotionRecommendedMaxLead);
                    }

                    return new LeadWindow(maxLead, clampReason);
                }

                var motionMax = solution.TargetRecommendedMaxLeadTicks > 0f
                    ? solution.TargetRecommendedMaxLeadTicks
                    : MaxLeadTicks;
                return new LeadWindow(Math.Min(maxLead, motionMax), CombatAimLeadClampReasons.MotionRecommendedMaxLead);
            }

            if (IsVeryLowPrediction(solution))
            {
                var confidenceMax = solution.TargetRecommendedMaxLeadTicks > 0f
                    ? Math.Min(solution.TargetRecommendedMaxLeadTicks, 12f)
                    : 12f;
                return new LeadWindow(Math.Min(maxLead, confidenceMax), CombatAimLeadClampReasons.PredictionConfidence);
            }

            if (IsLowPrediction(solution))
            {
                var confidenceMax = solution.TargetRecommendedMaxLeadTicks > 0f
                    ? solution.TargetRecommendedMaxLeadTicks
                    : 24f;
                return new LeadWindow(Math.Min(maxLead, confidenceMax), CombatAimLeadClampReasons.PredictionConfidence);
            }

            if (solution.TargetRecommendedMaxLeadTicks > 0f && solution.TargetRecommendedMaxLeadTicks < maxLead)
            {
                return new LeadWindow(solution.TargetRecommendedMaxLeadTicks, CombatAimLeadClampReasons.MotionRecommendedMaxLead);
            }

            return new LeadWindow(maxLead, clampReason);
        }

        private static CombatAimBallisticSolution SolveSpecialWeapon(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            CombatAimWeaponProfile weapon,
            CombatAimSpecialWeaponRule rule,
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
                return SolvePointAim(solution, target, BallisticStrategy.Point(rule.AimMode, rule.FixedLeadTicks));
            }

            if (rule.RainFromSky)
            {
                return SolveRainFromSky(solution, target, rule);
            }

            if (rule.HeavyGravity)
            {
                var heavyStrategy = BallisticStrategy.Gravity(rule.AimMode, MaxGravityLeadTicks, rule.GravityDelayTicks, rule.GravityPerTick);
                var heavy = SolveGravityArc(solution, target, speed, heavyStrategy);
                heavy.SpecialLeadTicks = heavy.LeadTicks;
                heavy.SpecialAimApplied = true;
                return heavy;
            }

            if (string.Equals(rule.SolverKind, CombatAimBallisticSolverKinds.ReturningProjectile, StringComparison.Ordinal))
            {
                var returningStrategy = BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.ReturningProjectile,
                    rule.AimMode,
                    CombatAimLeadWindowKinds.ReturningOutbound,
                    MaxReturningLeadTicks);
                SolveLinear(solution, target, speed, returningStrategy);
                solution.SpecialLeadTicks = solution.LeadTicks;
                solution.SpecialAimApplied = true;
                return solution;
            }

            if (rule.ArrowGravity && solution.AmmoArrowLike)
            {
                var gravityStrategy = IsSpreadRule(rule)
                    ? BallisticStrategy.SpreadGravity(rule.AimMode, MaxGravityLeadTicks, 10f, 0f)
                    : BallisticStrategy.Gravity(rule.AimMode, MaxGravityLeadTicks, 10f, 0f);
                var gravity = SolveGravityArc(solution, target, speed, gravityStrategy);
                gravity.SpecialLeadTicks = gravity.LeadTicks;
                gravity.SpecialAimApplied = true;
                return gravity;
            }

            var linearStrategy = IsSpreadRule(rule)
                ? BallisticStrategy.Create(
                    CombatAimBallisticSolverKinds.Spread,
                    rule.AimMode,
                    CombatAimLeadWindowKinds.SpreadCoverage,
                    MaxSpreadLeadTicks)
                : BallisticStrategy.Create(
                    string.IsNullOrWhiteSpace(rule.SolverKind) ? CombatAimBallisticSolverKinds.LinearIntercept : rule.SolverKind,
                    rule.AimMode,
                    string.IsNullOrWhiteSpace(rule.LeadWindowKind) ? CombatAimLeadWindowKinds.Medium : rule.LeadWindowKind,
                    MaxSpecialLeadTicks);
            SolveLinear(solution, target, speed, linearStrategy);
            solution.SpecialLeadTicks = solution.LeadTicks;
            solution.SpecialAimApplied = true;
            return solution;
        }

        private static CombatAimBallisticSolution SolveRainFromSky(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            CombatAimSpecialWeaponRule rule)
        {
            var targetSpeedSq = solution.TargetVelocityX * solution.TargetVelocityX + solution.TargetVelocityY * solution.TargetVelocityY;
            var leadTicks = rule.FixedLeadTicks > 0f
                ? rule.FixedLeadTicks
                : targetSpeedSq > 36f ? 6f : targetSpeedSq > 9f ? 9f : 12f;
            leadTicks = Clamp(leadTicks, 0f, MaxSpecialLeadTicks);

            var strategy = BallisticStrategy.Point(rule.AimMode, leadTicks);
            ApplyStrategyMetadata(solution, strategy);
            solution.Mode = rule.AimMode;
            solution.RawLeadTicks = leadTicks;
            solution.LeadTicks = leadTicks;
            solution.LeadScale = 1f;
            solution.LeadWindowMaxTicks = MaxSpecialLeadTicks;
            solution.LeadClampReason = CombatAimLeadClampReasons.FixedPointLead;
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
            BallisticStrategy strategy)
        {
            ApplyStrategyMetadata(solution, strategy);
            var leadTicks = Clamp(strategy.FixedLeadTicks, 0f, strategy.MaxLeadTicks <= 0f ? MaxSpecialLeadTicks : strategy.MaxLeadTicks);
            solution.Mode = strategy.Mode ?? "specialPointAim";
            solution.RawLeadTicks = strategy.FixedLeadTicks;
            solution.LeadTicks = leadTicks;
            solution.LeadScale = 1f;
            solution.LeadWindowMaxTicks = strategy.MaxLeadTicks <= 0f ? MaxSpecialLeadTicks : strategy.MaxLeadTicks;
            solution.LeadClampReason = CombatAimLeadClampReasons.FixedPointLead;
            solution.LeadClamped = Math.Abs(strategy.FixedLeadTicks - leadTicks) > 0.001f;
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

        private static CombatAimBallisticSolution SolveArrowGravity(
            CombatAimBallisticSolution solution,
            CombatTargetSnapshot target,
            float speed,
            int extraUpdates)
        {
            return SolveGravityArc(solution, target, speed, BallisticStrategy.Gravity("arrowGravity", MaxGravityLeadTicks, 10f, 0f));
        }

        private static CombatAimBallisticSolution Center(CombatAimBallisticSolution solution, string mode, string reason)
        {
            solution.Mode = mode ?? string.Empty;
            solution.FallbackReason = reason ?? string.Empty;
            solution.SolverKind = CombatAimBallisticSolverKinds.FallbackCenter;
            solution.LeadWindowKind = CombatAimLeadWindowKinds.Fallback;
            solution.LeadClampReason = CombatAimLeadClampReasons.CenterFallback;
            solution.PredictionConfidence = ResolvePredictionConfidence(solution);
            solution.LeadWindowMaxTicks = 0f;
            solution.LeadScale = 0f;
            solution.LeadClamped = true;
            solution.ConservativeCenter = true;
            solution.Solved = true;
            return solution;
        }

        private static bool IsProfileFamily(string family, string expected)
        {
            return string.Equals(family ?? string.Empty, expected, StringComparison.Ordinal);
        }

        private static bool IsSpreadRule(CombatAimSpecialWeaponRule rule)
        {
            if (rule == null)
            {
                return false;
            }

            return string.Equals(rule.SolverKind, CombatAimBallisticSolverKinds.Spread, StringComparison.Ordinal) ||
                   string.Equals(rule.Kind, "spreadMultiShot", StringComparison.Ordinal) ||
                   string.Equals(rule.Kind, "dualProjectileSpread", StringComparison.Ordinal) ||
                   string.Equals(rule.Kind, "parallelMultiShot", StringComparison.Ordinal);
        }

        private static string ResolvePredictionConfidence(CombatAimBallisticSolution solution)
        {
            if (solution == null)
            {
                return CombatAimPredictionConfidenceKinds.Unknown;
            }

            if (IsHardMotionReset(solution.TargetHistoryResetReason) ||
                string.Equals(solution.TargetMotionProfileKind, CombatAimTargetMotionProfile.TeleportOrDashRecent, StringComparison.Ordinal))
            {
                return CombatAimPredictionConfidenceKinds.VeryLow;
            }

            var motionConfidence = Clamp01(solution.TargetMotionConfidence);
            var velocityConfidence = Clamp01(solution.TargetVelocityConfidence);
            if (motionConfidence <= 0f && velocityConfidence <= 0f)
            {
                return CombatAimPredictionConfidenceKinds.Unknown;
            }

            var combined = motionConfidence <= 0f
                ? velocityConfidence
                : velocityConfidence <= 0f
                    ? motionConfidence
                    : Math.Min(motionConfidence, velocityConfidence);

            if (!IsProjectileProfileComplete(solution))
            {
                combined = Math.Min(combined, 0.45f);
            }

            if (combined >= 0.75f)
            {
                return CombatAimPredictionConfidenceKinds.High;
            }

            if (combined >= 0.5f)
            {
                return CombatAimPredictionConfidenceKinds.Medium;
            }

            if (combined >= 0.3f)
            {
                return CombatAimPredictionConfidenceKinds.Low;
            }

            return CombatAimPredictionConfidenceKinds.VeryLow;
        }

        private static bool AllowsLongSlowLead(CombatAimBallisticSolution solution)
        {
            if (solution == null || !IsProjectileProfileComplete(solution))
            {
                return false;
            }

            if (!string.Equals(solution.PredictionConfidence, CombatAimPredictionConfidenceKinds.High, StringComparison.Ordinal) &&
                !string.Equals(solution.PredictionConfidence, CombatAimPredictionConfidenceKinds.Medium, StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(solution.TargetMotionProfileKind, CombatAimTargetMotionProfile.StableLinear, StringComparison.Ordinal) ||
                   string.Equals(solution.TargetMotionProfileKind, CombatAimTargetMotionProfile.LargeOrSegmented, StringComparison.Ordinal);
        }

        private static bool IsProjectileProfileComplete(CombatAimBallisticSolution solution)
        {
            return solution != null &&
                   string.Equals(solution.ProjectileProfileStatus, "complete", StringComparison.Ordinal);
        }

        private static bool IsVeryLowPrediction(CombatAimBallisticSolution solution)
        {
            return solution == null ||
                   string.Equals(solution.PredictionConfidence, CombatAimPredictionConfidenceKinds.Unknown, StringComparison.Ordinal) ||
                   string.Equals(solution.PredictionConfidence, CombatAimPredictionConfidenceKinds.VeryLow, StringComparison.Ordinal);
        }

        private static bool IsLowPrediction(CombatAimBallisticSolution solution)
        {
            return solution != null &&
                   string.Equals(solution.PredictionConfidence, CombatAimPredictionConfidenceKinds.Low, StringComparison.Ordinal);
        }

        private static bool IsHardMotionReset(string resetReason)
        {
            return string.Equals(resetReason, "teleportDistance", StringComparison.Ordinal) ||
                   string.Equals(resetReason, "staleTickGap", StringComparison.Ordinal) ||
                   string.Equals(resetReason, "measuredVelocitySpike", StringComparison.Ordinal);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
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

        private static CombatAimSpecialWeaponRule ResolveSpecialWeaponRule(
            CombatAimWeaponProfile weapon,
            CombatAimBallisticSolution solution,
            CombatAimProjectileProfile projectileProfile)
        {
            CombatAimSpecialWeaponRule rule;
            return CombatAimSpecialWeaponRuleResolver.TryResolve(weapon, solution, projectileProfile, out rule)
                ? rule
                : null;
        }

        private static void ApplySpecialMetadata(CombatAimBallisticSolution solution, CombatAimSpecialWeaponRule rule)
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
            solution.SpecialCursorTarget = rule.UsesCursorTarget;
            solution.SpecialWeaponUsesWeaponShoot = rule.UsesWeaponShoot;
            solution.SpecialWeaponUsesAmmoShoot = rule.UsesAmmoShoot;
            solution.SpecialWeaponSolverKind = rule.SolverKind ?? string.Empty;
            solution.SpecialWeaponLeadWindowKind = rule.LeadWindowKind ?? string.Empty;
            solution.SpecialWeaponLeadPolicy = rule.LeadPolicy ?? string.Empty;
            solution.SpecialWeaponDiagnosticsReason = rule.DiagnosticsReason ?? string.Empty;
            solution.ReturningPhaseAssumption = rule.ReturningPhaseAssumption ?? string.Empty;
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

        private static void ApplyProjectileRoleMetadata(
            CombatAimBallisticSolution solution,
            CombatAimWeaponProfile weapon,
            CombatAimProjectileProfile projectileProfile)
        {
            if (solution == null)
            {
                return;
            }

            if (projectileProfile != null)
            {
                solution.WeaponShootProjectileType = projectileProfile.WeaponShootProjectileType;
                solution.WeaponShootProjectileName = projectileProfile.WeaponShootProjectileName ?? string.Empty;
                solution.AmmoProjectileName = projectileProfile.AmmoProjectileName ?? string.Empty;
                solution.ResolvedProjectileRole = projectileProfile.ResolvedProjectileRole ?? string.Empty;
                solution.PrimaryProjectileType = projectileProfile.PrimaryProjectileType;
                solution.PrimaryProjectileName = projectileProfile.PrimaryProjectileName ?? string.Empty;
                solution.PrimaryProjectileRole = projectileProfile.PrimaryProjectileRole ?? string.Empty;
                return;
            }

            var weaponShoot = weapon == null ? 0 : weapon.Shoot;
            solution.WeaponShootProjectileType = weaponShoot;
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

        private struct BallisticStrategy
        {
            public string SolverKind;
            public string Mode;
            public string LeadWindowKind;
            public string FallbackReason;
            public float MaxLeadTicks;
            public float FixedLeadTicks;
            public float GravityPerTick;
            public float GravityDelayTicks;
            public bool UseGravity;

            public static BallisticStrategy Create(
                string solverKind,
                string mode,
                string leadWindowKind,
                float maxLeadTicks)
            {
                return new BallisticStrategy
                {
                    SolverKind = solverKind ?? CombatAimBallisticSolverKinds.FallbackCenter,
                    Mode = mode ?? string.Empty,
                    LeadWindowKind = leadWindowKind ?? CombatAimLeadWindowKinds.Fallback,
                    MaxLeadTicks = maxLeadTicks
                };
            }

            public static BallisticStrategy Point(string mode, float fixedLeadTicks)
            {
                var strategy = Create(
                    CombatAimBallisticSolverKinds.PointAim,
                    string.IsNullOrWhiteSpace(mode) ? "specialPointAim" : mode,
                    CombatAimLeadWindowKinds.PointShort,
                    MaxSpecialLeadTicks);
                strategy.FixedLeadTicks = fixedLeadTicks;
                return strategy;
            }

            public static BallisticStrategy Gravity(string mode, float maxLeadTicks, float gravityDelayTicks, float gravityPerTick)
            {
                var strategy = Create(
                    CombatAimBallisticSolverKinds.GravityArc,
                    mode,
                    CombatAimLeadWindowKinds.GravityArc,
                    maxLeadTicks);
                strategy.UseGravity = true;
                strategy.GravityDelayTicks = gravityDelayTicks;
                strategy.GravityPerTick = gravityPerTick;
                return strategy;
            }

            public static BallisticStrategy SpreadGravity(string mode, float maxLeadTicks, float gravityDelayTicks, float gravityPerTick)
            {
                var strategy = Create(
                    CombatAimBallisticSolverKinds.Spread,
                    mode,
                    CombatAimLeadWindowKinds.SpreadCoverage,
                    maxLeadTicks);
                strategy.UseGravity = true;
                strategy.GravityDelayTicks = gravityDelayTicks;
                strategy.GravityPerTick = gravityPerTick;
                return strategy;
            }

            public static BallisticStrategy Fallback(string mode, string reason)
            {
                var strategy = Create(
                    CombatAimBallisticSolverKinds.FallbackCenter,
                    mode,
                    CombatAimLeadWindowKinds.Fallback,
                    0f);
                strategy.FallbackReason = reason ?? string.Empty;
                return strategy;
            }
        }

        private struct LeadWindow
        {
            public readonly float MaxTicks;
            public readonly string ClampReason;

            public LeadWindow(float maxTicks, string clampReason)
            {
                MaxTicks = maxTicks;
                ClampReason = clampReason ?? CombatAimLeadClampReasons.ProjectileFamilyWindow;
            }
        }

        private struct LeadClamp
        {
            public float RawLeadTicks;
            public float LeadTicks;
            public float LeadScale;
            public float MaxLeadTicks;
            public string LeadClampReason;
            public bool LeadClamped;
        }

    }
}
