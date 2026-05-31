namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimFlailEligibility
    {
        public bool Eligible { get; private set; }
        public string Reason { get; private set; }

        private CombatAimFlailEligibility(bool eligible, string reason)
        {
            Eligible = eligible;
            Reason = reason ?? string.Empty;
        }

        public static CombatAimFlailEligibility Allow()
        {
            return new CombatAimFlailEligibility(true, "eligible:flailAiStyle15");
        }

        public static CombatAimFlailEligibility Reject(string reason)
        {
            return new CombatAimFlailEligibility(false, string.IsNullOrWhiteSpace(reason) ? "notFlail:unknown" : reason);
        }
    }

    public static class CombatAimFlailPolicy
    {
        public static CombatAimFlailEligibility Evaluate(CombatAimWeaponProfile profile, int projectileAiStyle, bool isYoyo)
        {
            if (profile == null || profile.IsEmpty)
            {
                return CombatAimFlailEligibility.Reject("notFlail:noWeapon");
            }

            if (profile.IsPlacementItem || profile.IsSentryPlacementWeapon || profile.IsSummonPlacementWeapon)
            {
                return CombatAimFlailEligibility.Reject("notFlail:placementOrSummon");
            }

            if (profile.IsToolOrFishingItem)
            {
                return CombatAimFlailEligibility.Reject("notFlail:toolOrFishing");
            }

            if (profile.IsAmmoItem)
            {
                return CombatAimFlailEligibility.Reject("notFlail:ammoItem");
            }

            if (!profile.IsCoinGun && profile.Damage <= 0)
            {
                return CombatAimFlailEligibility.Reject("notFlail:damageNotPositive");
            }

            if (!profile.Channel)
            {
                return CombatAimFlailEligibility.Reject("notFlail:notChannel");
            }

            if (profile.Shoot <= 0)
            {
                return CombatAimFlailEligibility.Reject("notFlail:noProjectile");
            }

            if (profile.UseAmmo > 0)
            {
                return CombatAimFlailEligibility.Reject("notFlail:usesAmmo");
            }

            if (isYoyo)
            {
                return CombatAimFlailEligibility.Reject("notFlail:yoyo");
            }

            if (projectileAiStyle != 15)
            {
                return CombatAimFlailEligibility.Reject("notFlail:notFlailAiStyle15");
            }

            return CombatAimFlailEligibility.Allow();
        }
    }
}
