using JueMingZ.Compat;

namespace JueMingZ.Automation.Combat
{
    // Decisions describe one scoped ItemCheck cursor/input intent; executors and Compat still own real input writes.
    public sealed class CombatAimItemCheckDecision
    {
        public bool Enabled { get; set; }
        public int RadiusTiles { get; set; }
        public string AimRangeOrigin { get; set; }
        public string AimTargetPriority { get; set; }
        public string ActiveRangeMode { get; set; }
        public int CursorAimRadius { get; set; }
        public int PlayerAimRadius { get; set; }
        public int PlayerScreenMarginTiles { get; set; }
        public int PlayerScreenRadiusTiles { get; set; }
        public float RangeCenterWorldX { get; set; }
        public float RangeCenterWorldY { get; set; }
        public bool TrackDummy { get; set; }
        public bool MarkerEnabled { get; set; }
        public bool BridgePending { get; set; }
        public bool UseItemHeld { get; set; }
        public bool UseItemReleased { get; set; }
        public bool WasUseItemHeldLastTick { get; set; }
        public bool ReleasedThisTick { get; set; }
        public bool ReleaseDetected { get; set; }
        public int ItemAnimation { get; set; }
        public int ItemTime { get; set; }
        public long GameUpdateCount { get; set; }
        public string AimApplyMode { get; set; }
        public string ReleaseHoldState { get; set; }
        public bool ReleaseHoldArmed { get; set; }
        public bool ReleaseHoldPending { get; set; }
        public bool ReleaseHoldConsumed { get; set; }
        public bool ReleaseHoldActive { get; set; }
        public int ReleaseHoldTicksRemaining { get; set; }
        public int ReleaseHoldApplyCount { get; set; }
        public string ReleaseHoldValidationMode { get; set; }
        public string ReleaseHoldValidationReason { get; set; }
        public bool ReleaseHoldRecomputedAimUsed { get; set; }
        public bool ReleaseHoldRecordedAimUsed { get; set; }
        public bool PersistentCursorActive { get; set; }
        public string PersistentCursorReason { get; set; }
        public string PersistentHook { get; set; }
        public bool PersistentCursorFrameCached { get; set; }
        public bool SpecialProjectileTailActive { get; set; }
        public bool SpecialProjectileTailRecomputedAim { get; set; }
        public string SpecialProjectileTailExpiredReason { get; set; }
        public bool ContinuousUseWeaponAllowed { get; set; }
        public int YoyoProjectileWhoAmI { get; set; }
        public int YoyoProjectileType { get; set; }
        public int YoyoProjectileAiStyle { get; set; }
        public bool YoyoDetected { get; set; }
        public bool AttackQualified { get; set; }
        public string AttackDisqualifiedReason { get; set; }
        public string SkipReason { get; set; }
        public string ResultCode { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public int ItemStack { get; set; }
        public string ItemName { get; set; }
        public int Damage { get; set; }
        public int Shoot { get; set; }
        public int UseAmmo { get; set; }
        public bool Melee { get; set; }
        public int CreateTile { get; set; }
        public int CreateWall { get; set; }
        public int Pick { get; set; }
        public int Axe { get; set; }
        public int Hammer { get; set; }
        public int FishingPole { get; set; }
        public CombatAimWeaponProfile WeaponProfile { get; set; }
        public float AimWorldX { get; set; }
        public float AimWorldY { get; set; }
        public int AimScreenX { get; set; }
        public int AimScreenY { get; set; }
        public CombatAimBallisticSolution BallisticSolution { get; set; }
        public CombatAimTargetSelection Selection { get; set; }
        public TerrariaUiInputContext UiContext { get; set; }

        public CombatTargetSnapshot Target
        {
            get { return Selection == null ? null : Selection.Target; }
        }

        public CombatAimItemCheckDecision()
        {
            AimRangeOrigin = string.Empty;
            AimTargetPriority = string.Empty;
            ActiveRangeMode = string.Empty;
            AimApplyMode = CombatAimApplyModes.None;
            ReleaseHoldState = ReleaseHoldStates.Idle;
            ReleaseHoldValidationMode = string.Empty;
            ReleaseHoldValidationReason = string.Empty;
            PersistentCursorReason = string.Empty;
            PersistentHook = string.Empty;
            SpecialProjectileTailExpiredReason = string.Empty;
            AttackDisqualifiedReason = string.Empty;
            SkipReason = string.Empty;
            ResultCode = string.Empty;
            SelectedSlot = -1;
            YoyoProjectileWhoAmI = -1;
            ItemName = string.Empty;
            CreateTile = -1;
            CreateWall = -1;
        }
    }
}
