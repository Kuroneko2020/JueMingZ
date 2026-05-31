using System;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimPersistentCursorEligibility
    {
        public bool Eligible { get; private set; }
        public string Reason { get; private set; }
        public string Class { get; private set; }
        public bool YoyoDetected { get; private set; }
        public bool AllowsMainUpdateFallback { get; private set; }
        public bool AllowsMainUpdateFallbackWithProjectileHook { get; private set; }
        public bool AllowsProjectileAiScoped { get; private set; }
        public bool VisibleCursorHijackRisk { get; private set; }
        public bool VisibleCursorHijackRiskMitigated { get; private set; }
        public string CursorOwnershipMode { get; private set; }
        public bool AllowsAnimationScopedWithoutHeld { get; private set; }

        private CombatAimPersistentCursorEligibility(
            bool eligible,
            string reason,
            string classification,
            bool yoyoDetected,
            bool allowsMainUpdateFallback,
            bool allowsMainUpdateFallbackWithProjectileHook,
            bool allowsProjectileAiScoped,
            bool visibleCursorHijackRisk,
            bool visibleCursorHijackRiskMitigated,
            string cursorOwnershipMode,
            bool allowsAnimationScopedWithoutHeld)
        {
            Eligible = eligible;
            Reason = reason ?? string.Empty;
            Class = classification ?? string.Empty;
            YoyoDetected = yoyoDetected;
            AllowsMainUpdateFallback = allowsMainUpdateFallback;
            AllowsMainUpdateFallbackWithProjectileHook = allowsMainUpdateFallbackWithProjectileHook;
            AllowsProjectileAiScoped = allowsProjectileAiScoped;
            VisibleCursorHijackRisk = visibleCursorHijackRisk;
            VisibleCursorHijackRiskMitigated = visibleCursorHijackRiskMitigated;
            CursorOwnershipMode = cursorOwnershipMode ?? string.Empty;
            AllowsAnimationScopedWithoutHeld = allowsAnimationScopedWithoutHeld;
        }

        public static CombatAimPersistentCursorEligibility EligibleYoyo()
        {
            return new CombatAimPersistentCursorEligibility(
                true,
                "yoyo",
                "yoyo",
                true,
                true,
                false,
                true,
                false,
                false,
                "persistentCursor:yoyo",
                false);
        }

        public static CombatAimPersistentCursorEligibility EligibleChannelProjectileScoped()
        {
            return new CombatAimPersistentCursorEligibility(
                true,
                "eligible:projectileAiScoped",
                "channelProjectileWeapon",
                false,
                false,
                false,
                true,
                true,
                true,
                "projectileAiScoped",
                false);
        }

        public static CombatAimPersistentCursorEligibility EligibleSpecialProjectileScoped()
        {
            return new CombatAimPersistentCursorEligibility(
                true,
                "eligible:specialProjectileAiScoped",
                "specialProjectileWeapon",
                false,
                false,
                false,
                true,
                true,
                true,
                "projectileAiScoped",
                true);
        }

        public static CombatAimPersistentCursorEligibility EligibleFlailProjectileScoped()
        {
            return new CombatAimPersistentCursorEligibility(
                true,
                "eligible:flailProjectileAiScoped",
                "flailAiStyle15",
                false,
                false,
                false,
                true,
                true,
                true,
                "projectileAiScoped",
                true);
        }

        public static CombatAimPersistentCursorEligibility NotEligible(string reason, bool yoyoDetected)
        {
            var value = string.IsNullOrWhiteSpace(reason) ? "notEligible:unknown" : reason;
            return new CombatAimPersistentCursorEligibility(
                false,
                value,
                "none",
                yoyoDetected,
                false,
                false,
                false,
                false,
                false,
                "userCursor",
                false);
        }
    }

    public static class CombatAimPersistentCursorPolicy
    {
        public static CombatAimPersistentCursorEligibility Evaluate(object player, CombatAimWeaponProfile profile)
        {
            bool yoyoDetected;
            string yoyoReason;
            var yoyoEligible = CombatAimYoyoCompat.IsYoyoWeapon(player, profile, out yoyoDetected, out yoyoReason);
            return Evaluate(profile, yoyoEligible, yoyoDetected, yoyoReason);
        }

        internal static CombatAimPersistentCursorEligibility Evaluate(
            CombatAimWeaponProfile profile,
            bool yoyoEligible,
            bool yoyoDetected,
            string yoyoReason)
        {
            if (yoyoEligible)
            {
                return CombatAimPersistentCursorEligibility.EligibleYoyo();
            }

            if (yoyoDetected)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:yoyoNotReady", true);
            }

            if (profile == null || profile.IsEmpty)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:noWeapon", false);
            }

            if (profile.IsPlacementItem || profile.IsSentryPlacementWeapon || profile.IsSummonPlacementWeapon)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:placementOrSummon", false);
            }

            if (profile.IsToolOrFishingItem)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:toolOrFishing", false);
            }

            if (profile.IsAmmoItem)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:ammoItem", false);
            }

            if (!profile.IsCoinGun && profile.Damage <= 0)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:damageNotPositive", false);
            }

            if (profile.Shoot <= 0)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:noProjectile", false);
            }

            if (string.Equals(yoyoReason, "flailAiStyle15Release", StringComparison.Ordinal) ||
                string.Equals(yoyoReason, "flailProjectileAiScoped", StringComparison.Ordinal))
            {
                return CombatAimPersistentCursorEligibility.EligibleFlailProjectileScoped();
            }

            if (IsSpecialProjectileScopedWeapon(profile))
            {
                return CombatAimPersistentCursorEligibility.EligibleSpecialProjectileScoped();
            }

            if (!profile.Channel)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:notChannelProjectile", false);
            }

            if (profile.UseAmmo > 0)
            {
                return CombatAimPersistentCursorEligibility.NotEligible("notEligible:usesAmmo", false);
            }

            return CombatAimPersistentCursorEligibility.EligibleChannelProjectileScoped();
        }

        public static bool IsSpecialProjectileScopedWeapon(CombatAimWeaponProfile profile)
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

            if (profile.Shoot <= 0)
            {
                return false;
            }

            CombatAimSpecialWeaponRule rule;
            return CombatAimSpecialWeaponRuleResolver.TryResolve(profile, out rule) &&
                   rule.AllowsProjectileAiScoped;
        }

        public static bool AllowsMainUpdateFallback(string hook, CombatAimPersistentCursorEligibility eligibility)
        {
            if (eligibility == null || !eligibility.Eligible)
            {
                return false;
            }

            if (string.Equals(hook, PersistentCursorHooks.MainUpdateFallback, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hook, PersistentCursorHooks.None, StringComparison.OrdinalIgnoreCase))
            {
                return eligibility.AllowsMainUpdateFallback;
            }

            return eligibility.AllowsMainUpdateFallbackWithProjectileHook;
        }

        public static bool AllowsProjectileAiScoped(string hook, CombatAimPersistentCursorEligibility eligibility)
        {
            if (eligibility == null || !eligibility.Eligible || !eligibility.AllowsProjectileAiScoped)
            {
                return false;
            }

            return string.Equals(hook, PersistentCursorHooks.ProjectileAI, StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(hook, PersistentCursorHooks.AI099, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(eligibility.Class, "yoyo", StringComparison.Ordinal));
        }
    }
}
