using System;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimWeaponFamilies
    {
        public const string DirectProjectile = "DirectProjectile";
        public const string ChannelProjectile = "ChannelProjectile";
        public const string FlailAiStyle15 = "FlailAiStyle15";
        public const string SpecialCursorSpawnBurst = "SpecialCursorSpawnBurst";
        public const string SpecialDualProjectile = "SpecialDualProjectile";
        public const string SpearAiStyle19 = "SpearAiStyle19";
        public const string ReturningBoomerangAiStyle3 = "ReturningBoomerangAiStyle3";
        public const string Yoyo = "Yoyo";
        public const string RainFromSky = "RainFromSky";
        public const string GuidedCursor = "GuidedCursor";
        public const string Homing = "Homing";
        public const string Beam = "Beam";
        public const string UnclassifiedSpecialMelee = "UnclassifiedSpecialMelee";
        public const string Unsupported = "Unsupported";
    }

    public sealed class CombatAimWeaponFamilyResult
    {
        public string Family { get; private set; }
        public string Reason { get; private set; }
        public int ProjectileAiStyle { get; private set; }
        public string SpecialWeaponRuleKind { get; private set; }

        public CombatAimWeaponFamilyResult(
            string family,
            string reason,
            int projectileAiStyle,
            string specialWeaponRuleKind)
        {
            Family = family ?? CombatAimWeaponFamilies.Unsupported;
            Reason = reason ?? string.Empty;
            ProjectileAiStyle = projectileAiStyle;
            SpecialWeaponRuleKind = specialWeaponRuleKind ?? string.Empty;
        }
    }

    public static class CombatAimWeaponFamilyResolver
    {
        public static CombatAimWeaponFamilyResult Resolve(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            return Resolve(profile, solution, false, string.Empty);
        }

        public static CombatAimWeaponFamilyResult Resolve(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            bool yoyoDetected,
            string yoyoReason)
        {
            var projectileAiStyle = solution == null ? 0 : solution.ProjectileAiStyle;
            var specialRuleKind = ResolveSpecialRuleKind(profile, solution);
            return Resolve(profile, solution, projectileAiStyle, specialRuleKind, yoyoDetected, yoyoReason);
        }

        private static CombatAimWeaponFamilyResult Resolve(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution,
            int projectileAiStyle,
            string specialRuleKind,
            bool yoyoDetected,
            string yoyoReason)
        {
            if (profile == null)
            {
                return Result(CombatAimWeaponFamilies.Unsupported, "unsupported:weaponProfileUnavailable", projectileAiStyle, specialRuleKind);
            }

            if (profile.IsEmpty)
            {
                return Result(CombatAimWeaponFamilies.Unsupported, "unsupported:emptyWeapon", projectileAiStyle, specialRuleKind);
            }

            string unsupportedReason;
            if (TryResolveUnsupported(profile, out unsupportedReason))
            {
                return Result(CombatAimWeaponFamilies.Unsupported, unsupportedReason, projectileAiStyle, specialRuleKind);
            }

            // Keep specialized projectile families ahead of the generic fallback; flails, yoyos, spears, and beams need distinct aim paths.
            if (IsYoyo(profile, projectileAiStyle, yoyoDetected))
            {
                var reason = string.IsNullOrWhiteSpace(yoyoReason)
                    ? "yoyo:projectileAiStyleOrCompat"
                    : "yoyo:" + yoyoReason;
                return Result(CombatAimWeaponFamilies.Yoyo, reason, projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "cursorSpawnBurst", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.SpecialCursorSpawnBurst, "specialWeaponRuleKind=cursorSpawnBurst", projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "dualProjectileSpread", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.SpecialDualProjectile, "specialWeaponRuleKind=dualProjectileSpread", projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "rainFromSky", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.RainFromSky, "specialWeaponRuleKind=rainFromSky", projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "guidedCursor", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.GuidedCursor, "specialWeaponRuleKind=guidedCursor", projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "homingOrSelfCorrecting", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.Homing, "specialWeaponRuleKind=homingOrSelfCorrecting", projectileAiStyle, specialRuleKind);
            }

            if (string.Equals(specialRuleKind, "beamOrInstant", StringComparison.Ordinal))
            {
                return Result(CombatAimWeaponFamilies.Beam, "specialWeaponRuleKind=beamOrInstant", projectileAiStyle, specialRuleKind);
            }

            if (profile.Channel && projectileAiStyle == 15)
            {
                return Result(CombatAimWeaponFamilies.FlailAiStyle15, "channel=true;projectileAiStyle=15", projectileAiStyle, specialRuleKind);
            }

            if (projectileAiStyle == 19)
            {
                return Result(CombatAimWeaponFamilies.SpearAiStyle19, "projectileAiStyle=19", projectileAiStyle, specialRuleKind);
            }

            if (projectileAiStyle == 3)
            {
                return Result(CombatAimWeaponFamilies.ReturningBoomerangAiStyle3, "projectileAiStyle=3", projectileAiStyle, specialRuleKind);
            }

            if (profile.Channel && profile.Shoot > 0)
            {
                return Result(CombatAimWeaponFamilies.ChannelProjectile, "channel=true;shoot>0", projectileAiStyle, specialRuleKind);
            }

            if (IsUnclassifiedSpecialMelee(profile))
            {
                return Result(CombatAimWeaponFamilies.UnclassifiedSpecialMelee, "meleeProjectileWithoutKnownAiStyle", projectileAiStyle, specialRuleKind);
            }

            if (HasDirectProjectileSemantics(profile, solution))
            {
                return Result(
                    CombatAimWeaponFamilies.DirectProjectile,
                    "projectileSemantics:shoot=" + profile.Shoot + ";useAmmo=" + profile.UseAmmo,
                    projectileAiStyle,
                    specialRuleKind);
            }

            return Result(CombatAimWeaponFamilies.Unsupported, "unsupported:noProjectileSemantics", projectileAiStyle, specialRuleKind);
        }

        private static CombatAimWeaponFamilyResult Result(
            string family,
            string reason,
            int projectileAiStyle,
            string specialRuleKind)
        {
            return new CombatAimWeaponFamilyResult(family, reason, projectileAiStyle, specialRuleKind);
        }

        private static string ResolveSpecialRuleKind(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            if (solution != null && !string.IsNullOrWhiteSpace(solution.SpecialWeaponKind))
            {
                return solution.SpecialWeaponKind;
            }

            CombatAimSpecialWeaponRule rule;
            return CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out rule)
                ? rule.Kind
                : string.Empty;
        }

        private static bool TryResolveUnsupported(CombatAimWeaponProfile profile, out string reason)
        {
            reason = string.Empty;
            if (profile.IsPlacementItem)
            {
                reason = "unsupported:placementItem";
                return true;
            }

            if (profile.IsSentryPlacementWeapon || profile.IsSummonPlacementWeapon)
            {
                reason = "unsupported:placementOrSummon";
                return true;
            }

            if (profile.IsToolOrFishingItem)
            {
                reason = "unsupported:toolOrFishing";
                return true;
            }

            if (profile.IsAmmoItem)
            {
                reason = "unsupported:ammoItem";
                return true;
            }

            if (!profile.IsCoinGun && profile.Damage <= 0)
            {
                reason = "unsupported:damageNotPositive";
                return true;
            }

            return false;
        }

        private static bool IsYoyo(
            CombatAimWeaponProfile profile,
            int projectileAiStyle,
            bool yoyoDetected)
        {
            return yoyoDetected ||
                   projectileAiStyle == 99 ||
                   profile != null && CombatAimYoyoCompat.IsYoyoProjectileType(profile.Shoot);
        }

        private static bool IsUnclassifiedSpecialMelee(CombatAimWeaponProfile profile)
        {
            return profile != null &&
                   profile.Melee &&
                   profile.Shoot > 0 &&
                   (profile.NoMelee || profile.NoUseGraphic || !profile.Ranged && !profile.Magic && !profile.Thrown);
        }

        private static bool HasDirectProjectileSemantics(
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            return profile != null &&
                   (profile.Shoot > 0 ||
                    profile.UseAmmo > 0 ||
                    solution != null && solution.ProjectileType > 0);
        }
    }
}
