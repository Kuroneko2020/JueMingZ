using System;

namespace JueMingZ.Automation.Combat
{
    // The cache is shared aim evidence only; ItemCheck and ProjectileAI paths
    // must revalidate live target state before steering mouse input.
    internal static class CombatAimDecisionCache
    {
        internal const long AttackSelectionTtlTicks = 4;
        internal const long MarkerSelectionTtlTicks = 6;

        private static readonly object SyncRoot = new object();
        private static string _key = string.Empty;
        private static long _tick = long.MinValue;
        private static string _source = string.Empty;
        private static CombatAimTargetSelection _selection;

        internal static void StoreSelection(string key, long tick, CombatAimTargetSelection selection, string source)
        {
            if (string.IsNullOrWhiteSpace(key) || selection == null || !selection.HasTarget)
            {
                return;
            }

            lock (SyncRoot)
            {
                _key = key;
                _tick = tick;
                _source = source ?? string.Empty;
                _selection = CloneSelection(selection);
            }
        }

        internal static bool TryGetSelection(string key, long tick, out CombatAimTargetSelection selection)
        {
            return TryGetSelection(key, tick, AttackSelectionTtlTicks, out selection);
        }

        internal static bool TryGetSelection(string key, long tick, long ttlTicks, out CombatAimTargetSelection selection)
        {
            selection = null;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_selection == null || !string.Equals(_key, key, StringComparison.Ordinal))
                {
                    return false;
                }

                return TryCloneIfFreshLocked(tick, ttlTicks, out selection);
            }
        }

        internal static bool TryGetRecentSelectionForDifferentKey(string key, long tick, long ttlTicks, out CombatAimTargetSelection selection)
        {
            selection = null;
            lock (SyncRoot)
            {
                if (_selection == null || string.Equals(_key, key ?? string.Empty, StringComparison.Ordinal))
                {
                    return false;
                }

                return TryCloneIfFreshLocked(tick, ttlTicks, out selection);
            }
        }

        internal static bool TryGetRecentMarkerSelection(long tick, out CombatAimTargetSelection selection)
        {
            selection = null;
            lock (SyncRoot)
            {
                if (_selection == null || !_selection.MarkerEnabled || !_selection.HasTarget)
                {
                    return false;
                }

                return TryCloneIfFreshLocked(tick, MarkerSelectionTtlTicks, out selection);
            }
        }

        internal static void Clear()
        {
            lock (SyncRoot)
            {
                _key = string.Empty;
                _tick = long.MinValue;
                _source = string.Empty;
                _selection = null;
            }
        }

        internal static void ResetForTesting()
        {
            Clear();
        }

        private static bool TryCloneIfFreshLocked(long tick, long ttlTicks, out CombatAimTargetSelection selection)
        {
            selection = null;
            var age = tick - _tick;
            if (age < 0 || age > ttlTicks)
            {
                return false;
            }

            selection = CloneSelection(_selection);
            if (selection == null)
            {
                return false;
            }

            selection.SelectionCacheHit = true;
            selection.SelectionCacheKey = _key;
            selection.DecisionCacheSource = _source;
            selection.DecisionCacheAgeTicks = age;
            selection.DecisionCacheRevalidationReason = "cacheHit";
            return true;
        }

        private static CombatAimTargetSelection CloneSelection(CombatAimTargetSelection source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatAimTargetSelection
            {
                Enabled = source.Enabled,
                RadiusTiles = source.RadiusTiles,
                TrackDummy = source.TrackDummy,
                MarkerEnabled = source.MarkerEnabled,
                AimRangeOrigin = source.AimRangeOrigin,
                AimTargetPriority = source.AimTargetPriority,
                CursorAimRadius = source.CursorAimRadius,
                PlayerAimRadius = source.PlayerAimRadius,
                ActiveRangeMode = source.ActiveRangeMode,
                PlayerScreenMarginTiles = source.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = source.PlayerScreenRadiusTiles,
                CursorWorldX = source.CursorWorldX,
                CursorWorldY = source.CursorWorldY,
                RangeCenterWorldX = source.RangeCenterWorldX,
                RangeCenterWorldY = source.RangeCenterWorldY,
                MouseScreenX = source.MouseScreenX,
                MouseScreenY = source.MouseScreenY,
                ScreenPositionX = source.ScreenPositionX,
                ScreenPositionY = source.ScreenPositionY,
                ScreenWidth = source.ScreenWidth,
                ScreenHeight = source.ScreenHeight,
                CandidateCount = source.CandidateCount,
                CheapCandidateCount = source.CheapCandidateCount,
                ExpensiveCandidateCount = source.ExpensiveCandidateCount,
                EvaluatedCandidateCount = source.EvaluatedCandidateCount,
                InRangeCandidateCount = source.InRangeCandidateCount,
                LosCacheHit = source.LosCacheHit,
                SkipReason = source.SkipReason,
                ResultCode = source.ResultCode,
                Target = CloneTarget(source.Target),
                BallisticTarget = CloneTarget(source.BallisticTarget),
                BallisticSolution = source.BallisticSolution == null ? null : source.BallisticSolution.Clone(),
                CenterDistanceTiles = source.CenterDistanceTiles,
                HitboxDistanceTiles = source.HitboxDistanceTiles,
                TargetDistanceFromRangeCenterTiles = source.TargetDistanceFromRangeCenterTiles,
                TargetScore = source.TargetScore,
                LineClear = source.LineClear,
                LineClearAvailable = source.LineClearAvailable,
                DistanceToPlayerCursorRay = source.DistanceToPlayerCursorRay,
                InForwardCone = source.InForwardCone,
                PreviousTargetBonus = source.PreviousTargetBonus,
                SelectedSamplePoint = source.SelectedSamplePoint,
                AttackSamplePoint = source.AttackSamplePoint,
                SelectionSamplePoint = source.SelectionSamplePoint,
                SampleSpace = source.SampleSpace,
                LineOfSightRejectedSampleCount = source.LineOfSightRejectedSampleCount,
                VisibleSampleCount = source.VisibleSampleCount,
                NearestHitboxPointPenaltyApplied = source.NearestHitboxPointPenaltyApplied,
                CenterPreferred = source.CenterPreferred,
                SelectedSampleWorldX = source.SelectedSampleWorldX,
                SelectedSampleWorldY = source.SelectedSampleWorldY,
                PredictedHitboxCenterX = source.PredictedHitboxCenterX,
                PredictedHitboxCenterY = source.PredictedHitboxCenterY,
                ProjectileHitRadius = source.ProjectileHitRadius,
                SelectedReason = source.SelectedReason,
                LockedTargetId = source.LockedTargetId,
                LockedTargetType = source.LockedTargetType,
                LockedTargetStillValid = source.LockedTargetStillValid,
                TargetLockAgeTicks = source.TargetLockAgeTicks,
                TargetHoldTicksRemaining = source.TargetHoldTicksRemaining,
                SelectionPurpose = source.SelectionPurpose,
                SelectionCacheHit = source.SelectionCacheHit,
                SelectionCacheKey = source.SelectionCacheKey,
                PreferredTargetWhoAmI = source.PreferredTargetWhoAmI,
                PreferredTargetType = source.PreferredTargetType,
                MarkerTargetWhoAmI = source.MarkerTargetWhoAmI,
                MarkerTargetType = source.MarkerTargetType,
                AttackTargetWhoAmI = source.AttackTargetWhoAmI,
                AttackTargetType = source.AttackTargetType,
                MarkerAttackTargetMismatch = source.MarkerAttackTargetMismatch,
                MarkerTargetChangedForAttack = source.MarkerTargetChangedForAttack,
                MarkerAttackMismatchReason = source.MarkerAttackMismatchReason,
                DecisionCacheSource = source.DecisionCacheSource,
                DecisionCacheAgeTicks = source.DecisionCacheAgeTicks,
                DecisionCacheRevalidationReason = source.DecisionCacheRevalidationReason
            };
        }

        private static CombatTargetSnapshot CloneTarget(CombatTargetSnapshot target)
        {
            return target == null ? null : target.CloneForAimSample(target.CenterX, target.CenterY);
        }
    }
}
