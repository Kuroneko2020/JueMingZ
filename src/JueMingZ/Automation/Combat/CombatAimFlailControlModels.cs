using System.Collections;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    public static partial class CombatAimFlailControlService
    {
        internal sealed class FlailProjectileSnapshot
        {
            public object Raw;
            public int WhoAmI = -1;
            public int Type;
            public int AiStyle;
            public int Owner = -1;
            public int Identity = -1;
            public bool Active;
            public bool Friendly;
            public bool Hostile;
            public int Width;
            public int Height;
            public float Ai0;
            public float VelocityX;
            public float VelocityY;
            public object Position;
            public object Velocity;
            public IList LocalNpcImmunity;
        }

    }

    internal sealed class CombatAimFlailProjectileFrame
    {
        public int WhoAmI = -1;
        public int Type;
        public int Identity = -1;
        public bool Active;
        public float Ai0;
        public float VelocityX;
        public float VelocityY;

        public static CombatAimFlailProjectileFrame None()
        {
            return new CombatAimFlailProjectileFrame();
        }

        public static CombatAimFlailProjectileFrame ForTesting(
            bool active,
            int whoAmI,
            int type,
            int identity,
            float ai0,
            float velocityX,
            float velocityY)
        {
            return new CombatAimFlailProjectileFrame
            {
                Active = active,
                WhoAmI = whoAmI,
                Type = type,
                Identity = identity,
                Ai0 = ai0,
                VelocityX = velocityX,
                VelocityY = velocityY
            };
        }

        internal static CombatAimFlailProjectileFrame FromSnapshot(CombatAimFlailControlService.FlailProjectileSnapshot snapshot)
        {
            return snapshot == null
                ? None()
                : ForTesting(snapshot.Active, snapshot.WhoAmI, snapshot.Type, snapshot.Identity, snapshot.Ai0, snapshot.VelocityX, snapshot.VelocityY);
        }
    }

    public static class FlailControlStates
    {
        public const string Idle = "Idle";
        public const string ReadyToLaunch = "ReadyToLaunch";
        public const string SpinHold = "SpinHold";
        public const string LaunchPulse = "LaunchPulse";
        public const string ProjectileActive = "ProjectileActive";
        public const string ProjectileFlying = "ProjectileFlying";
        public const string ReleaseAfterLaunch = "ReleaseAfterLaunch";
        public const string ReleaseToTarget = "ReleaseToTarget";
        public const string WaitHitOrCollision = "WaitHitOrCollision";
        public const string ReattackPulse = "ReattackPulse";
        public const string StuckRecoveryRelease = "StuckRecoveryRelease";
        public const string ReleaseAfterPulse = "ReleaseAfterPulse";
        public const string Cooldown = "Cooldown";
        public const string Disabled = "Disabled";
    }

    public static class FlailReleaseKinds
    {
        public const string Launch = "launch";
        public const string Reattack = "reattack";
    }

    public sealed class CombatAimFlailControlDecision
    {
        public string State { get; private set; }
        public bool AttackPulse { get; private set; }
        public bool AttackRelease { get; private set; }
        public bool AttackSuppressed { get; private set; }
        public bool AttackRestored { get; private set; }
        public string BlockedReason { get; private set; }
        public string InputMode { get; private set; }
        public string PulseReason { get; private set; }
        public string ReleaseKind { get; private set; }

        private CombatAimFlailControlDecision(
            string state,
            bool attackPulse,
            bool attackRelease,
            bool attackSuppressed,
            bool attackRestored,
            string blockedReason,
            string inputMode,
            string pulseReason,
            string releaseKind)
        {
            State = state ?? string.Empty;
            AttackPulse = attackPulse;
            AttackRelease = attackRelease;
            AttackSuppressed = attackSuppressed;
            AttackRestored = attackRestored;
            BlockedReason = blockedReason ?? string.Empty;
            InputMode = inputMode ?? string.Empty;
            PulseReason = pulseReason ?? string.Empty;
            ReleaseKind = releaseKind ?? string.Empty;
        }

        public static CombatAimFlailControlDecision None(string state, string reason)
        {
            return new CombatAimFlailControlDecision(state, false, false, false, false, reason, "observe", string.Empty, string.Empty);
        }

        public static CombatAimFlailControlDecision Release(string state, string reason, bool restored)
        {
            return new CombatAimFlailControlDecision(state, false, true, true, restored, reason, "controlledUseItemRelease", string.Empty, string.Empty);
        }

        public static CombatAimFlailControlDecision Pulse(string state, string reason, string releaseKind)
        {
            return new CombatAimFlailControlDecision(state, true, false, false, false, reason, "controlledUseItemPulse", reason, releaseKind);
        }
    }

    public sealed class CombatAimFlailDiagnostics
    {
        public int ItemType;
        public string ItemName;
        public bool Eligible;
        public string Reason;
        public bool Active;
        public string State;
        public int ProjectileWhoAmI;
        public int ProjectileType;
        public int ProjectileAiStyle;
        public float ProjectileAi0;
        public float ProjectileVelocityX;
        public float ProjectileVelocityY;
        public int ProjectileIdentity;
        public bool HitDetected;
        public bool CollisionDetected;
        public bool LocalNpcImmunityChanged;
        public bool TileCollisionDetected;
        public bool AttackPulse;
        public bool AttackRelease;
        public bool AttackSuppressed;
        public bool AttackRestored;
        public string BlockedReason;
        public string InputMode;
        public string InputPhase;
        public string TakeoverScope;
        public string StuckRecovery;
        public bool ReleaseSuppressedPhysicalInput;
        public bool PhysicalUseItemHeld;
        public bool PhysicalReleasePending;
        public string PulseReason;
        public bool CachedReleaseAim;
        public int CachedReleaseAimAgeTicks;
        public string CachedReleaseAimReason;

        public static CombatAimFlailDiagnostics Empty()
        {
            return new CombatAimFlailDiagnostics
            {
                ItemType = 0,
                ItemName = string.Empty,
                Reason = "notEvaluated",
                State = FlailControlStates.Idle,
                ProjectileWhoAmI = -1,
                ProjectileIdentity = -1,
                BlockedReason = string.Empty,
                InputMode = "observe",
                InputPhase = string.Empty,
                TakeoverScope = "none",
                StuckRecovery = "none",
                PhysicalUseItemHeld = false,
                PhysicalReleasePending = false,
                PulseReason = string.Empty,
                CachedReleaseAim = false,
                CachedReleaseAimAgeTicks = -1,
                CachedReleaseAimReason = string.Empty
            };
        }

        public CombatAimFlailDiagnostics Clone()
        {
            return new CombatAimFlailDiagnostics
            {
                ItemType = ItemType,
                ItemName = ItemName ?? string.Empty,
                Eligible = Eligible,
                Reason = Reason ?? string.Empty,
                Active = Active,
                State = State ?? string.Empty,
                ProjectileWhoAmI = ProjectileWhoAmI,
                ProjectileType = ProjectileType,
                ProjectileAiStyle = ProjectileAiStyle,
                ProjectileAi0 = ProjectileAi0,
                ProjectileVelocityX = ProjectileVelocityX,
                ProjectileVelocityY = ProjectileVelocityY,
                ProjectileIdentity = ProjectileIdentity,
                HitDetected = HitDetected,
                CollisionDetected = CollisionDetected,
                LocalNpcImmunityChanged = LocalNpcImmunityChanged,
                TileCollisionDetected = TileCollisionDetected,
                AttackPulse = AttackPulse,
                AttackRelease = AttackRelease,
                AttackSuppressed = AttackSuppressed,
                AttackRestored = AttackRestored,
                BlockedReason = BlockedReason ?? string.Empty,
                InputMode = InputMode ?? string.Empty,
                InputPhase = InputPhase ?? string.Empty,
                TakeoverScope = TakeoverScope ?? string.Empty,
                StuckRecovery = StuckRecovery ?? string.Empty,
                ReleaseSuppressedPhysicalInput = ReleaseSuppressedPhysicalInput,
                PhysicalUseItemHeld = PhysicalUseItemHeld,
                PhysicalReleasePending = PhysicalReleasePending,
                PulseReason = PulseReason ?? string.Empty,
                CachedReleaseAim = CachedReleaseAim,
                CachedReleaseAimAgeTicks = CachedReleaseAimAgeTicks,
                CachedReleaseAimReason = CachedReleaseAimReason ?? string.Empty
            };
        }
    }

    internal struct CombatAimFlailRuntimeFrame
    {
        public AppSettings Settings;
        public object Player;
        public TerrariaUiInputContext UiContext;
        public CombatAimWeaponProfile Profile;
        public CombatAimBallisticContext Prepared;
        public CombatAimFlailEligibility Eligibility;
        public CombatAimTargetSelection Selection;
        public CombatAimUseInputSnapshot Input;
        public CombatAimFlailProjectileFrame Projectile;
        public CombatAimFlailControlService.FlailProjectileSnapshot RawProjectile;
        public int SelectedSlot;
        public long Tick;
        public int ProjectileAiStyle;
        public bool PhysicalHeld;
        public bool PhysicalReleasePending;
        public bool ReleaseInFlight;
        public bool CurrentTargetAvailable;
        public bool CachedReleaseAimAvailable;
        public bool InCooldown;
        public bool HasProjectile;
        public bool ItemReady;
        public bool HitDetected;
        public bool CollisionDetected;
        public int StuckTicks;
        public bool StuckRecovery;
    }

    internal struct CombatAimFlailDecisionContext
    {
        public CombatAimFlailProjectileFrame Projectile;
        public bool ItemReady;
        public bool InCooldown;
        public bool HitDetected;
        public bool CollisionDetected;
        public bool StuckRecovery;
        public bool PhysicalHeld;
        public bool PhysicalReleasePending;
        public bool ReleaseInFlight;
    }

    internal sealed class CombatAimFlailReleaseAimSnapshot
    {
        public int ItemType;
        public string ItemName = string.Empty;
        public int Shoot;
        public int SelectedSlot = -1;
        public long RecordedGameUpdateCount;
        public float AimWorldX;
        public float AimWorldY;
        public CombatAimWeaponProfile WeaponProfile;
        public CombatAimTargetSelection Selection;
        public CombatAimBallisticSolution BallisticSolution;
    }

    internal sealed class CombatAimFlailCachedDecisionContext
    {
        public object Player;
        public CombatAimWeaponProfile Profile;
        public CombatAimBallisticContext Prepared;
        public CombatAimFlailReleaseAimSnapshot CachedAim;
        public AppSettings Settings;
        public CombatAimUseInputSnapshot Input;
        public long Tick;
    }

    internal sealed class CombatAimFlailProjectileSnapshot
    {
        public CombatAimFlailControlService.FlailProjectileSnapshot RawSnapshot;
        public CombatAimFlailProjectileFrame Frame;
        public bool HitDetected;
        public bool CollisionDetected;
        public int StuckTicks;
    }

    internal struct CombatAimFlailCollisionResult
    {
        public bool CollisionDetected;
        public string Reason;
    }

    internal sealed class CombatAimFlailTakeoverContext
    {
        public object Player;
        public CombatAimItemCheckDecision Decision;
        public CombatAimFlailDiagnostics Diagnostics;
        public string Scope = string.Empty;
        public bool RememberReleaseTail;
    }
}
