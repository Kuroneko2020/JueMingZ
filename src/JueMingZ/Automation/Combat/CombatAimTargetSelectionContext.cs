namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimTargetSelectionContext
    {
        public string AimRangeOrigin { get; set; }
        public string AimTargetPriority { get; set; }
        public int CursorAimRadius { get; set; }
        public int PlayerAimRadius { get; set; }
        public bool HasPlayerCenter { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }
        public object Player { get; set; }
        public CombatAimWeaponProfile WeaponProfile { get; set; }
        public bool IncludeBallisticScoring { get; set; }
        public bool HasResolvedRange { get; set; }
        public CombatAimRangeResolveResult Range { get; set; }
        public string SelectionPurpose { get; set; }
        public int PreferredTargetWhoAmI { get; set; }
        public int PreferredTargetType { get; set; }
        public bool RequirePreferredTarget { get; set; }
        public bool AllowRecordedAimFallback { get; set; }
        public bool AllowRelaxedReleaseValidation { get; set; }
        public bool SelectionCacheHit { get; set; }
        public string SelectionCacheKey { get; set; }
        public string DecisionCacheRejectedReason { get; set; }
        public CombatAimBallisticContext BallisticContext { get; set; }

        public CombatAimTargetSelectionContext()
        {
            AimRangeOrigin = string.Empty;
            AimTargetPriority = string.Empty;
            SelectionPurpose = string.Empty;
            PreferredTargetWhoAmI = -1;
            PreferredTargetType = 0;
            SelectionCacheKey = string.Empty;
            DecisionCacheRejectedReason = string.Empty;
        }
    }
}
