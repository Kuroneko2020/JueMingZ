using System;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Combat
{
    // Release-hold state remembers button windows only; ItemCheck takeover remains the boundary for controlled release input.
    public static class CombatAimReleaseHoldService
    {
        private const int DefaultPendingTicks = 8;
        private const int MaxPendingTicks = 20;
        private const int MaxApplyCount = 2;
        private const float RelaxedRangeMarginPixels = 96f;

        private static readonly object SyncRoot = new object();
        private static ReleaseHoldSnapshot _state;
        private static CombatAimUseInputSnapshot _lastInput;

        public static void Tick(bool inWorld)
        {
            Tick(inWorld, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(bool inWorld, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var aimEnabled = settingsSnapshot.CombatAimAnyEnabled;
            if (!inWorld || !aimEnabled)
            {
                lock (SyncRoot)
                {
                    _state = null;
                    _lastInput = null;
                }

                return;
            }

            CombatAimUseInputSnapshot input = null;
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TerrariaInputCompat.TryReadCombatAimUseInputSnapshot(player, out input);
            }

            lock (SyncRoot)
            {
                AdvanceLocked(input);
                if (input != null && input.Available)
                {
                    _lastInput = input;
                }
            }
        }

        public static void DecorateDecision(CombatAimItemCheckDecision decision, CombatAimUseInputSnapshot input)
        {
            if (decision == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                decision.ReleaseHoldState = _state == null ? ReleaseHoldStates.Idle : _state.State;
                decision.ReleaseHoldArmed = _state != null && string.Equals(_state.State, ReleaseHoldStates.ArmedWhileHeld, StringComparison.Ordinal);
                decision.ReleaseHoldPending = _state != null && string.Equals(_state.State, ReleaseHoldStates.ReleasedPending, StringComparison.Ordinal);
                decision.ReleaseHoldConsumed = _state != null && string.Equals(_state.State, ReleaseHoldStates.Consumed, StringComparison.Ordinal);
                decision.ReleaseHoldApplyCount = _state == null ? 0 : _state.ApplyCount;
                decision.WasUseItemHeldLastTick = _lastInput != null && _lastInput.UseItemHeld;
                decision.ReleasedThisTick = IsReleasedThisTickLocked(input);
                decision.ReleaseDetected = decision.ReleasedThisTick ||
                                           input != null &&
                                           input.Available &&
                                           !input.UseItemHeld &&
                                           input.UseItemReleased &&
                                           (input.ItemAnimation <= 0 || input.ItemTime <= 0);
            }
        }

        public static void Record(CombatAimItemCheckDecision decision, int holdTicks)
        {
            if (decision == null || decision.Target == null || holdTicks <= 0 || !decision.UseItemHeld)
            {
                return;
            }

            lock (SyncRoot)
            {
                _state = new ReleaseHoldSnapshot
                {
                    State = ReleaseHoldStates.ArmedWhileHeld,
                    ItemType = decision.ItemType,
                    SelectedSlot = decision.SelectedSlot,
                    TargetWhoAmI = decision.Target.WhoAmI,
                    TargetType = decision.Target.Type,
                    TargetName = decision.Target.Name ?? string.Empty,
                    AimWorldX = decision.AimWorldX,
                    AimWorldY = decision.AimWorldY,
                    SelectedSamplePoint = decision.Selection == null ? string.Empty : decision.Selection.SelectedSamplePoint,
                    RangeCenterWorldX = decision.RangeCenterWorldX,
                    RangeCenterWorldY = decision.RangeCenterWorldY,
                    AimRangeOrigin = decision.AimRangeOrigin,
                    AimTargetPriority = decision.AimTargetPriority,
                    ActiveRangeMode = decision.ActiveRangeMode,
                    RadiusTiles = decision.RadiusTiles,
                    CursorAimRadius = decision.CursorAimRadius,
                    PlayerAimRadius = decision.PlayerAimRadius,
                    PlayerScreenMarginTiles = decision.PlayerScreenMarginTiles,
                    PlayerScreenRadiusTiles = decision.PlayerScreenRadiusTiles,
                    HoldTicks = Clamp(holdTicks <= 0 ? DefaultPendingTicks : holdTicks, 1, MaxPendingTicks),
                    TicksRemaining = Clamp(holdTicks <= 0 ? DefaultPendingTicks : holdTicks, 1, MaxPendingTicks),
                    RecordedGameUpdateCount = decision.GameUpdateCount,
                    WeaponClassification = decision.WeaponProfile == null ? string.Empty : decision.WeaponProfile.Classification,
                    BallisticMode = decision.BallisticSolution == null ? string.Empty : decision.BallisticSolution.Mode,
                    TargetScore = decision.Selection == null ? 0f : decision.Selection.TargetScore,
                    ApplyCount = 0
                };

                decision.ReleaseHoldState = _state.State;
                decision.ReleaseHoldArmed = true;
                decision.ReleaseHoldTicksRemaining = _state.TicksRemaining;
                decision.ReleaseHoldApplyCount = 0;
            }
        }

        public static bool TryApply(
            object player,
            CombatAimItemCheckDecision decision,
            CombatAimReadResult readResult,
            CombatAimUseInputSnapshot input,
            CombatAimRangeResolveResult range,
            AppSettings settings,
            out string reason)
        {
            reason = string.Empty;
            ReleaseHoldSnapshot snapshot;
            lock (SyncRoot)
            {
                EnsurePendingLocked(input, decision);
                snapshot = _state == null ? null : _state.Clone();
            }

            if (snapshot == null)
            {
                reason = "releaseHoldInactive";
                return false;
            }

            DecorateDecision(decision, input);
            decision.ReleaseHoldTicksRemaining = snapshot.TicksRemaining;
            decision.ReleaseHoldApplyCount = snapshot.ApplyCount;

            if (!string.Equals(snapshot.State, ReleaseHoldStates.ReleasedPending, StringComparison.Ordinal))
            {
                reason = "releaseHoldNotPending:" + snapshot.State;
                return false;
            }

            if (snapshot.ApplyCount >= MaxApplyCount)
            {
                MarkConsumed();
                reason = "releaseHoldConsumed";
                return false;
            }

            if (input == null || !input.Available)
            {
                reason = "releaseHoldInputUnavailable";
                return false;
            }

            if (input.SelectedSlot != snapshot.SelectedSlot || input.ItemType != snapshot.ItemType)
            {
                Expire("itemChanged");
                reason = "releaseHoldItemChanged";
                return false;
            }

            if (decision.ItemType != snapshot.ItemType || decision.SelectedSlot != snapshot.SelectedSlot)
            {
                Expire("decisionItemChanged");
                reason = "releaseHoldDecisionItemChanged";
                return false;
            }

            var target = FindTarget(readResult, snapshot.TargetWhoAmI, snapshot.TargetType);
            string targetValidationReason;
            if (!IsTargetValidForReleaseHold(target, decision.TrackDummy, out targetValidationReason))
            {
                Expire("targetInvalid:" + targetValidationReason);
                decision.ReleaseHoldValidationMode = CombatAimReleaseValidationModes.Failed;
                decision.ReleaseHoldValidationReason = targetValidationReason;
                reason = "releaseHoldTargetInvalid:" + targetValidationReason;
                return false;
            }

            if (range == null || !range.Enabled)
            {
                reason = "releaseHoldRangeDisabled";
                return false;
            }

            float playerCenterX;
            float playerCenterY;
            var hasPlayerCenter = CombatAimPlayerContext.TryReadPlayerCenter(player, out playerCenterX, out playerCenterY);
            var strictSelection = CombatAimTargetSelector.Select(
                readResult,
                range.RadiusTiles,
                decision.TrackDummy,
                decision.MarkerEnabled,
                new CombatAimTargetSelectionContext
                {
                    AimRangeOrigin = decision.AimRangeOrigin,
                    AimTargetPriority = decision.AimTargetPriority,
                    CursorAimRadius = decision.CursorAimRadius,
                    PlayerAimRadius = decision.PlayerAimRadius,
                    HasPlayerCenter = hasPlayerCenter,
                    PlayerCenterX = playerCenterX,
                    PlayerCenterY = playerCenterY,
                    Player = player,
                    WeaponProfile = decision.WeaponProfile,
                    IncludeBallisticScoring = true,
                    HasResolvedRange = true,
                    Range = range,
                    SelectionPurpose = "ReleaseHold",
                    PreferredTargetWhoAmI = snapshot.TargetWhoAmI,
                    PreferredTargetType = snapshot.TargetType,
                    RequirePreferredTarget = true,
                    AllowRecordedAimFallback = true,
                    AllowRelaxedReleaseValidation = true,
                    BallisticContext = CombatAimBallisticSolver.Prepare(player, decision.WeaponProfile)
                });

            if (IsStrictSelectionValid(strictSelection, snapshot, out reason))
            {
                ApplySelection(decision, strictSelection, CombatAimReleaseValidationModes.Strict, target.IsTargetDummy ? "targetDummyAllowed:strictRecomputed" : "strictRecomputed");
                decision.ReleaseHoldRecomputedAimUsed = true;
                IncrementApplyCount();
                return true;
            }

            if (CanUseRelaxedRecordedAim(snapshot, target, input, range, strictSelection, out var relaxedReason))
            {
                if (target.IsTargetDummy)
                {
                    relaxedReason = "targetDummyAllowed:" + relaxedReason;
                }

                ApplyRecorded(decision, snapshot, readResult, target, CombatAimReleaseValidationModes.RelaxedFallback, relaxedReason);
                decision.ReleaseHoldRecordedAimUsed = true;
                IncrementApplyCount();
                return true;
            }

            decision.ReleaseHoldValidationMode = CombatAimReleaseValidationModes.Failed;
            decision.ReleaseHoldValidationReason = string.IsNullOrWhiteSpace(reason) ? relaxedReason : reason;
            reason = decision.ReleaseHoldValidationReason;
            return false;
        }

        private static void AdvanceLocked(CombatAimUseInputSnapshot input)
        {
            if (_state == null)
            {
                return;
            }

            EnsurePendingLocked(input, null);
            if (_state == null)
            {
                return;
            }

            if (string.Equals(_state.State, ReleaseHoldStates.ReleasedPending, StringComparison.Ordinal))
            {
                _state.TicksRemaining--;
                if (_state.TicksRemaining <= 0)
                {
                    _state.State = ReleaseHoldStates.Expired;
                    _state = null;
                }
            }

            if (input != null &&
                input.Available &&
                input.UseItemHeld &&
                (input.SelectedSlot != _state.SelectedSlot || input.ItemType != _state.ItemType))
            {
                _state = null;
            }
        }

        private static void EnsurePendingLocked(CombatAimUseInputSnapshot input, CombatAimItemCheckDecision decision)
        {
            if (_state == null ||
                !string.Equals(_state.State, ReleaseHoldStates.ArmedWhileHeld, StringComparison.Ordinal) ||
                input == null ||
                !input.Available)
            {
                return;
            }

            if (input.SelectedSlot != _state.SelectedSlot || input.ItemType != _state.ItemType)
            {
                _state.State = ReleaseHoldStates.Expired;
                _state = null;
                return;
            }

            var releasedThisTick = IsReleasedThisTickLocked(input);
            var readyRelease = !input.UseItemHeld &&
                               input.UseItemReleased &&
                               (input.ItemAnimation <= 0 || input.ItemTime <= 0);
            if (!releasedThisTick && !readyRelease)
            {
                return;
            }

            _state.State = ReleaseHoldStates.ReleasedPending;
            _state.TicksRemaining = Clamp(_state.HoldTicks <= 0 ? DefaultPendingTicks : _state.HoldTicks, 1, MaxPendingTicks);
            _state.ReleaseDetectedGameUpdateCount = input.GameUpdateCount;
            _state.ApplyCount = 0;

            if (decision != null)
            {
                decision.ReleaseDetected = true;
                decision.ReleasedThisTick = releasedThisTick;
                decision.ReleaseHoldState = _state.State;
                decision.ReleaseHoldPending = true;
                decision.ReleaseHoldTicksRemaining = _state.TicksRemaining;
            }
        }

        private static bool IsReleasedThisTickLocked(CombatAimUseInputSnapshot input)
        {
            return input != null &&
                   input.Available &&
                   _lastInput != null &&
                   _lastInput.Available &&
                   _lastInput.UseItemHeld &&
                   !input.UseItemHeld;
        }

        private static bool IsStrictSelectionValid(CombatAimTargetSelection selection, ReleaseHoldSnapshot snapshot, out string reason)
        {
            reason = string.Empty;
            if (selection == null || selection.Target == null)
            {
                reason = "strictSelectionMissing";
                return false;
            }

            if (selection.Target.WhoAmI != snapshot.TargetWhoAmI || selection.Target.Type != snapshot.TargetType)
            {
                reason = "strictTargetChanged";
                return false;
            }

            if (selection.LineClearAvailable && !selection.LineClear)
            {
                reason = "strictLineBlocked";
                return false;
            }

            return true;
        }

        private static bool CanUseRelaxedRecordedAim(
            ReleaseHoldSnapshot snapshot,
            CombatTargetSnapshot target,
            CombatAimUseInputSnapshot input,
            CombatAimRangeResolveResult range,
            CombatAimTargetSelection strictSelection,
            out string reason)
        {
            reason = string.Empty;
            if (snapshot == null || target == null || input == null || range == null)
            {
                reason = "relaxedMissingContext";
                return false;
            }

            var age = input.GameUpdateCount - snapshot.ReleaseDetectedGameUpdateCount;
            if (age < 0 || age > 2)
            {
                reason = "relaxedReleaseWindowExpired";
                return false;
            }

            var hitboxDistance = HitboxDistance(range.RangeCenterWorldX, range.RangeCenterWorldY, target);
            if (hitboxDistance > range.RadiusPixels + RelaxedRangeMarginPixels)
            {
                reason = "relaxedTargetOutOfRange";
                return false;
            }

            if (strictSelection != null && strictSelection.LineClearAvailable && !strictSelection.LineClear)
            {
                reason = "relaxedSevereLineBlocked";
                return false;
            }

            reason = "relaxedRecordedAimWithinReleaseWindow";
            return true;
        }

        private static void ApplySelection(CombatAimItemCheckDecision decision, CombatAimTargetSelection selection, string mode, string reason)
        {
            decision.Selection = selection;
            decision.BallisticSolution = selection.BallisticSolution;
            decision.AimWorldX = selection.BallisticSolution == null ? selection.SelectedSampleWorldX : selection.BallisticSolution.AimWorldX;
            decision.AimWorldY = selection.BallisticSolution == null ? selection.SelectedSampleWorldY : selection.BallisticSolution.AimWorldY;
            decision.AimApplyMode = CombatAimApplyModes.ReleaseHold;
            decision.ReleaseHoldActive = true;
            decision.ReleaseHoldValidationMode = mode;
            decision.ReleaseHoldValidationReason = reason;
            decision.AttackQualified = true;
        }

        private static void ApplyRecorded(
            CombatAimItemCheckDecision decision,
            ReleaseHoldSnapshot snapshot,
            CombatAimReadResult readResult,
            CombatTargetSnapshot target,
            string mode,
            string reason)
        {
            var selection = new CombatAimTargetSelection
            {
                Enabled = true,
                RadiusTiles = decision.RadiusTiles,
                TrackDummy = decision.TrackDummy,
                MarkerEnabled = decision.MarkerEnabled,
                AimRangeOrigin = decision.AimRangeOrigin,
                AimTargetPriority = decision.AimTargetPriority,
                ActiveRangeMode = decision.ActiveRangeMode,
                CursorAimRadius = decision.CursorAimRadius,
                PlayerAimRadius = decision.PlayerAimRadius,
                PlayerScreenMarginTiles = decision.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = decision.PlayerScreenRadiusTiles,
                CursorWorldX = readResult == null ? 0f : readResult.CursorWorldX,
                CursorWorldY = readResult == null ? 0f : readResult.CursorWorldY,
                RangeCenterWorldX = decision.RangeCenterWorldX,
                RangeCenterWorldY = decision.RangeCenterWorldY,
                CandidateCount = readResult == null || readResult.Candidates == null ? 0 : readResult.Candidates.Count,
                ResultCode = "ReleaseHold",
                SkipReason = "none",
                Target = target,
                BallisticTarget = target.CloneForAimSample(snapshot.AimWorldX, snapshot.AimWorldY),
                SelectedSamplePoint = string.IsNullOrWhiteSpace(snapshot.SelectedSamplePoint) ? "releaseHoldRecorded" : snapshot.SelectedSamplePoint,
                SelectedSampleWorldX = snapshot.AimWorldX,
                SelectedSampleWorldY = snapshot.AimWorldY,
                SelectedReason = "releaseHoldRecorded",
                TargetDistanceFromRangeCenterTiles = HitboxDistance(decision.RangeCenterWorldX, decision.RangeCenterWorldY, target) / 16f,
                LockedTargetId = CombatAimTargetLockService.LockedTargetId,
                LockedTargetType = CombatAimTargetLockService.LockedTargetType,
                LockedTargetStillValid = CombatAimTargetLockService.IsLockedTarget(target.WhoAmI, target.Type),
                TargetLockAgeTicks = CombatAimTargetLockService.LockAgeTicks,
                TargetHoldTicksRemaining = CombatAimTargetLockService.HoldTicksRemaining,
                SelectionPurpose = "ReleaseHold"
            };

            decision.Selection = selection;
            decision.AimWorldX = snapshot.AimWorldX;
            decision.AimWorldY = snapshot.AimWorldY;
            decision.AimApplyMode = CombatAimApplyModes.ReleaseHold;
            decision.ReleaseHoldActive = true;
            decision.ReleaseHoldValidationMode = mode;
            decision.ReleaseHoldValidationReason = reason;
            decision.AttackQualified = true;
        }

        private static void IncrementApplyCount()
        {
            lock (SyncRoot)
            {
                if (_state == null)
                {
                    return;
                }

                _state.ApplyCount++;
                if (_state.ApplyCount >= MaxApplyCount)
                {
                    _state.State = ReleaseHoldStates.Consumed;
                }
            }
        }

        private static void MarkConsumed()
        {
            lock (SyncRoot)
            {
                if (_state != null)
                {
                    _state.State = ReleaseHoldStates.Consumed;
                }
            }
        }

        private static void Expire(string reason)
        {
            lock (SyncRoot)
            {
                if (_state != null)
                {
                    _state.State = ReleaseHoldStates.Expired;
                    _state = null;
                }
            }

            LogThrottle.InfoThrottled(
                "combat-aim-release-hold-expired-" + reason,
                TimeSpan.FromSeconds(2),
                "CombatAimReleaseHoldService",
                "ReleaseHold expired: " + reason);
        }

        private static CombatTargetSnapshot FindTarget(CombatAimReadResult readResult, int whoAmI, int type)
        {
            if (readResult == null || readResult.Candidates == null)
            {
                return null;
            }

            for (var index = 0; index < readResult.Candidates.Count; index++)
            {
                var target = readResult.Candidates[index];
                if (target != null && target.WhoAmI == whoAmI && target.Type == type)
                {
                    return target;
                }
            }

            return null;
        }

        internal static bool IsTargetValidForReleaseHold(CombatTargetSnapshot target, bool trackDummy, out string reason)
        {
            reason = string.Empty;
            if (target == null)
            {
                reason = "targetMissing";
                return false;
            }

            if (!target.Active)
            {
                reason = "targetInactive";
                return false;
            }

            if (target.IsTargetDummy)
            {
                if (!trackDummy)
                {
                    reason = "targetDummyDisabled";
                    return false;
                }

                reason = "targetDummyAllowed";
                return true;
            }

            if (target.Life <= 0)
            {
                reason = "deadOrNoLife";
                return false;
            }

            if (target.Friendly)
            {
                reason = "friendly";
                return false;
            }

            if (target.TownNpc)
            {
                reason = "townNpc";
                return false;
            }

            if (target.Hide)
            {
                reason = "hidden";
                return false;
            }

            if (target.DontTakeDamage)
            {
                reason = "dontTakeDamage";
                return false;
            }

            if (target.Immortal)
            {
                reason = "immortal";
                return false;
            }

            reason = "attackable";
            return true;
        }

        private static float HitboxDistance(float x, float y, CombatTargetSnapshot target)
        {
            var right = target.HitboxX + Math.Max(1f, target.HitboxWidth);
            var bottom = target.HitboxY + Math.Max(1f, target.HitboxHeight);
            var nearestX = Clamp(x, target.HitboxX, right);
            var nearestY = Clamp(y, target.HitboxY, bottom);
            var dx = x - nearestX;
            var dy = y - nearestY;
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

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class ReleaseHoldSnapshot
        {
            public string State = ReleaseHoldStates.Idle;
            public int ItemType;
            public int SelectedSlot;
            public int TargetWhoAmI;
            public int TargetType;
            public string TargetName = string.Empty;
            public float AimWorldX;
            public float AimWorldY;
            public string SelectedSamplePoint = string.Empty;
            public float RangeCenterWorldX;
            public float RangeCenterWorldY;
            public string AimRangeOrigin = string.Empty;
            public string AimTargetPriority = string.Empty;
            public string ActiveRangeMode = string.Empty;
            public int RadiusTiles;
            public int CursorAimRadius;
            public int PlayerAimRadius;
            public int PlayerScreenMarginTiles;
            public int PlayerScreenRadiusTiles;
            public int HoldTicks;
            public int TicksRemaining;
            public long RecordedGameUpdateCount;
            public long ReleaseDetectedGameUpdateCount;
            public string WeaponClassification = string.Empty;
            public string BallisticMode = string.Empty;
            public float TargetScore;
            public int ApplyCount;

            public ReleaseHoldSnapshot Clone()
            {
                return (ReleaseHoldSnapshot)MemberwiseClone();
            }
        }
    }

    public static class ReleaseHoldStates
    {
        public const string Idle = "Idle";
        public const string ArmedWhileHeld = "ArmedWhileHeld";
        public const string ReleasedPending = "ReleasedPending";
        public const string Consumed = "Consumed";
        public const string Expired = "Expired";
    }

    public static class CombatAimReleaseValidationModes
    {
        public const string Strict = "Strict";
        public const string RelaxedFallback = "RelaxedFallback";
        public const string Failed = "Failed";
    }
}
