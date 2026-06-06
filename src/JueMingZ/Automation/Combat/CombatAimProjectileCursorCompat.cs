using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimProjectileCursorMatch
    {
        public bool Matches { get; private set; }
        public string Reason { get; private set; }
        public int ProjectileWhoAmI { get; private set; }
        public int ProjectileType { get; private set; }
        public int ProjectileOwner { get; private set; }
        public int ProjectileAiStyle { get; private set; }

        private CombatAimProjectileCursorMatch(
            bool matches,
            string reason,
            int projectileWhoAmI,
            int projectileType,
            int projectileOwner,
            int projectileAiStyle)
        {
            Matches = matches;
            Reason = reason ?? string.Empty;
            ProjectileWhoAmI = projectileWhoAmI;
            ProjectileType = projectileType;
            ProjectileOwner = projectileOwner;
            ProjectileAiStyle = projectileAiStyle;
        }

        public static CombatAimProjectileCursorMatch Result(
            bool matches,
            string reason,
            int projectileWhoAmI,
            int projectileType,
            int projectileOwner,
            int projectileAiStyle)
        {
            return new CombatAimProjectileCursorMatch(matches, reason, projectileWhoAmI, projectileType, projectileOwner, projectileAiStyle);
        }

        public static CombatAimProjectileCursorMatch NotEvaluated()
        {
            return new CombatAimProjectileCursorMatch(false, "notEvaluated", -1, 0, -1, 0);
        }
    }

    public static class CombatAimProjectileCursorCompat
    {
        // Projectile cursor matching is read-only ownership evidence for scoped
        // cursor tails; it never edits projectile ai, velocity, position, or net state.
        private static readonly ConditionalWeakTable<CombatAimItemCheckDecision, CombatAimProjectileCursorMetadata> DecisionMetadata =
            new ConditionalWeakTable<CombatAimItemCheckDecision, CombatAimProjectileCursorMetadata>();

        public static void AttachDecisionMetadata(
            CombatAimItemCheckDecision decision,
            CombatAimProjectileCursorMatch match,
            bool scopedOverride,
            bool projectileAiScopedAllowed,
            bool visibleCursorHijackRiskMitigated)
        {
            if (decision == null)
            {
                return;
            }

            DecisionMetadata.Remove(decision);
            DecisionMetadata.Add(
                decision,
                new CombatAimProjectileCursorMetadata(
                    scopedOverride,
                    projectileAiScopedAllowed,
                    match != null && match.Matches,
                    match == null ? string.Empty : match.Reason,
                    match == null ? 0 : match.ProjectileType,
                    match == null ? -1 : match.ProjectileOwner,
                    visibleCursorHijackRiskMitigated));
        }

        public static CombatAimProjectileCursorMetadata GetDecisionMetadata(CombatAimItemCheckDecision decision)
        {
            if (decision == null)
            {
                return CombatAimProjectileCursorMetadata.Empty;
            }

            CombatAimProjectileCursorMetadata metadata;
            return DecisionMetadata.TryGetValue(decision, out metadata)
                ? metadata
                : CombatAimProjectileCursorMetadata.Empty;
        }

        public static CombatAimProjectileCursorMatch MatchChannelProjectile(
            object projectile,
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            int whoAmI;
            int projectileType;
            int aiStyle;
            int owner;
            bool active;
            bool friendly;
            bool hostile;
            if (!TryReadProjectile(projectile, out whoAmI, out projectileType, out aiStyle, out owner, out active, out friendly, out hostile))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInfoUnavailable", whoAmI, projectileType, owner, aiStyle);
            }

            if (!active)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInactive", whoAmI, projectileType, owner, aiStyle);
            }

            var localOwner = ReadLocalPlayerId(player);
            if (localOwner < 0 || owner != localOwner)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notLocalOwnedProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (!friendly)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notFriendlyProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (hostile)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:hostileProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (!IsSupportedChannelProjectileWeapon(profile))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notChannelProjectileWeapon", whoAmI, projectileType, owner, aiStyle);
            }

            var expectedType = profile == null ? 0 : profile.Shoot;
            var resolvedType = solution == null ? 0 : solution.ProjectileType;
            if (projectileType != expectedType && (resolvedType <= 0 || projectileType != resolvedType))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileMismatch", whoAmI, projectileType, owner, aiStyle);
            }

            return CombatAimProjectileCursorMatch.Result(true, "matched:channelProjectileWeapon", whoAmI, projectileType, owner, aiStyle);
        }

        public static CombatAimProjectileCursorMatch MatchSpecialWeaponProjectile(
            object projectile,
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            int whoAmI;
            int projectileType;
            int aiStyle;
            int owner;
            bool active;
            bool friendly;
            bool hostile;
            if (!TryReadProjectile(projectile, out whoAmI, out projectileType, out aiStyle, out owner, out active, out friendly, out hostile))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInfoUnavailable", whoAmI, projectileType, owner, aiStyle);
            }

            if (!active)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInactive", whoAmI, projectileType, owner, aiStyle);
            }

            var localOwner = ReadLocalPlayerId(player);
            if (localOwner < 0 || owner != localOwner)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notLocalOwnedProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (hostile)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:hostileProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (!CombatAimPersistentCursorPolicy.IsSpecialProjectileScopedWeapon(profile))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notSpecialProjectileWeapon", whoAmI, projectileType, owner, aiStyle);
            }

            if (!MatchesSpecialWeaponProjectileType(projectileType, profile, solution))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileMismatch", whoAmI, projectileType, owner, aiStyle);
            }

            if (!friendly &&
                !CombatAimSpecialWeaponRuleResolver.AllowsNonFriendlyScopedProjectile(projectileType, profile, solution))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notFriendlyProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            return CombatAimProjectileCursorMatch.Result(true, "matched:specialWeaponProjectile", whoAmI, projectileType, owner, aiStyle);
        }

        public static CombatAimProjectileCursorMatch MatchFlailProjectile(
            object projectile,
            object player,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            int whoAmI;
            int projectileType;
            int aiStyle;
            int owner;
            bool active;
            bool friendly;
            bool hostile;
            if (!TryReadProjectile(projectile, out whoAmI, out projectileType, out aiStyle, out owner, out active, out friendly, out hostile))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInfoUnavailable", whoAmI, projectileType, owner, aiStyle);
            }

            if (!active)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileInactive", whoAmI, projectileType, owner, aiStyle);
            }

            var localOwner = ReadLocalPlayerId(player);
            if (localOwner < 0 || owner != localOwner)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notLocalOwnedProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (!friendly)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notFriendlyProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (hostile)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:hostileProjectile", whoAmI, projectileType, owner, aiStyle);
            }

            if (aiStyle != 15)
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:notFlailAiStyle15", whoAmI, projectileType, owner, aiStyle);
            }

            var projectileAiStyle = solution == null ? aiStyle : solution.ProjectileAiStyle;
            if (projectileAiStyle <= 0)
            {
                projectileAiStyle = aiStyle;
            }

            var isYoyo = profile != null && CombatAimYoyoCompat.IsYoyoProjectileType(profile.Shoot);
            var eligibility = CombatAimFlailPolicy.Evaluate(profile, projectileAiStyle, isYoyo);
            if (!eligibility.Eligible)
            {
                return CombatAimProjectileCursorMatch.Result(false, eligibility.Reason, whoAmI, projectileType, owner, aiStyle);
            }

            var expectedType = profile == null ? 0 : profile.Shoot;
            var resolvedType = solution == null ? 0 : solution.ProjectileType;
            if (projectileType != expectedType && (resolvedType <= 0 || projectileType != resolvedType))
            {
                return CombatAimProjectileCursorMatch.Result(false, "notEligible:projectileMismatch", whoAmI, projectileType, owner, aiStyle);
            }

            return CombatAimProjectileCursorMatch.Result(true, "matched:flailAiStyle15Release", whoAmI, projectileType, owner, aiStyle);
        }

        internal static bool IsSupportedChannelProjectileWeapon(CombatAimWeaponProfile profile)
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

            if (!profile.Channel || profile.Shoot <= 0 || profile.UseAmmo > 0)
            {
                return false;
            }

            return true;
        }

        private static bool MatchesSpecialWeaponProjectileType(
            int projectileType,
            CombatAimWeaponProfile profile,
            CombatAimBallisticSolution solution)
        {
            return CombatAimSpecialWeaponRuleResolver.MatchesScopedProjectile(projectileType, profile, solution);
        }

        private static bool TryReadProjectile(
            object projectile,
            out int whoAmI,
            out int projectileType,
            out int aiStyle,
            out int owner,
            out bool active,
            out bool friendly,
            out bool hostile)
        {
            whoAmI = -1;
            projectileType = 0;
            aiStyle = 0;
            owner = -1;
            active = false;
            friendly = false;
            hostile = false;
            if (projectile == null)
            {
                return false;
            }

            GameStateReflection.TryGetInt(projectile, "whoAmI", out whoAmI);
            GameStateReflection.TryGetInt(projectile, "aiStyle", out aiStyle);
            GameStateReflection.TryGetInt(projectile, "owner", out owner);
            GameStateReflection.TryGetBool(projectile, "active", out active);
            GameStateReflection.TryGetBool(projectile, "friendly", out friendly);
            GameStateReflection.TryGetBool(projectile, "hostile", out hostile);
            return GameStateReflection.TryGetInt(projectile, "type", out projectileType);
        }

        private static int ReadLocalPlayerId(object player)
        {
            var myPlayer = ReadMainMyPlayer();
            if (myPlayer >= 0)
            {
                return myPlayer;
            }

            int whoAmI;
            return player != null && GameStateReflection.TryGetInt(player, "whoAmI", out whoAmI) ? whoAmI : -1;
        }

        private static int ReadMainMyPlayer()
        {
            try
            {
                var mainType = GameMode.FindTerrariaMainType();
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
    }

    public sealed class CombatAimProjectileCursorMetadata
    {
        public static readonly CombatAimProjectileCursorMetadata Empty =
            new CombatAimProjectileCursorMetadata(false, false, false, "notEvaluated", 0, -1, false);

        public bool ScopedOverride { get; private set; }
        public bool ProjectileAiScopedAllowed { get; private set; }
        public bool ProjectileMatch { get; private set; }
        public string ProjectileMatchReason { get; private set; }
        public int ProjectileType { get; private set; }
        public int ProjectileOwner { get; private set; }
        public bool VisibleCursorHijackRiskMitigated { get; private set; }

        public CombatAimProjectileCursorMetadata(
            bool scopedOverride,
            bool projectileAiScopedAllowed,
            bool projectileMatch,
            string projectileMatchReason,
            int projectileType,
            int projectileOwner,
            bool visibleCursorHijackRiskMitigated)
        {
            ScopedOverride = scopedOverride;
            ProjectileAiScopedAllowed = projectileAiScopedAllowed;
            ProjectileMatch = projectileMatch;
            ProjectileMatchReason = projectileMatchReason ?? string.Empty;
            ProjectileType = projectileType;
            ProjectileOwner = projectileOwner;
            VisibleCursorHijackRiskMitigated = visibleCursorHijackRiskMitigated;
        }
    }
}
