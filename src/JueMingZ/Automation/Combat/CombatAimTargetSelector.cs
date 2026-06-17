using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimTargetSelector
    {
        private const int MaxCheapCandidates = 32;
        private const int MaxExpensiveCandidates = 4;
        private const float PreviousTargetBonusValue = 90f;

        public static CombatAimTargetSelection Select(
            CombatAimReadResult readResult,
            int radiusTiles,
            bool trackDummy,
            bool markerEnabled)
        {
            return Select(
                readResult,
                radiusTiles,
                trackDummy,
                markerEnabled,
                new CombatAimTargetSelectionContext
                {
                    AimRangeOrigin = CombatAimModes.RangeOriginCursor,
                    AimTargetPriority = CombatAimModes.TargetPriorityClearLine,
                    CursorAimRadius = radiusTiles,
                    PlayerAimRadius = radiusTiles
                });
        }

        public static CombatAimTargetSelection Select(
            CombatAimReadResult readResult,
            int radiusTiles,
            bool trackDummy,
            bool markerEnabled,
            CombatAimTargetSelectionContext context)
        {
            context = context ?? new CombatAimTargetSelectionContext();
            var rangeOrigin = CombatAimModes.NormalizeRangeOrigin(context.AimRangeOrigin);
            var priority = CombatAimModes.NormalizeTargetPriority(context.AimTargetPriority);
            var resolvedRange = context.HasResolvedRange ? context.Range : null;
            if (resolvedRange != null)
            {
                radiusTiles = resolvedRange.RadiusTiles;
                rangeOrigin = CombatAimModes.NormalizeRangeOrigin(resolvedRange.AimRangeOrigin);
            }

            var selection = new CombatAimTargetSelection
            {
                Enabled = resolvedRange == null ? radiusTiles > 0 : resolvedRange.Enabled,
                RadiusTiles = radiusTiles,
                TrackDummy = trackDummy,
                MarkerEnabled = markerEnabled,
                AimRangeOrigin = rangeOrigin,
                AimTargetPriority = priority,
                CursorAimRadius = resolvedRange == null ? context.CursorAimRadius : resolvedRange.CursorAimRadius,
                PlayerAimRadius = resolvedRange == null ? context.PlayerAimRadius : resolvedRange.PlayerAimRadius,
                ActiveRangeMode = resolvedRange == null
                    ? (string.Equals(rangeOrigin, CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase) ? CombatAimRangeResolver.RangeModePlayerScreen : CombatAimRangeResolver.RangeModeCursorSlider)
                    : resolvedRange.RangeMode,
                PlayerScreenMarginTiles = resolvedRange == null ? 0 : resolvedRange.PlayerScreenMarginTiles,
                PlayerScreenRadiusTiles = resolvedRange == null ? 0 : resolvedRange.PlayerScreenRadiusTiles,
                LockedTargetId = CombatAimTargetLockService.LockedTargetId,
                LockedTargetType = CombatAimTargetLockService.LockedTargetType,
                TargetLockAgeTicks = CombatAimTargetLockService.LockAgeTicks,
                TargetHoldTicksRemaining = CombatAimTargetLockService.HoldTicksRemaining,
                SelectionPurpose = string.IsNullOrWhiteSpace(context.SelectionPurpose) ? "Marker" : context.SelectionPurpose,
                SelectionCacheHit = context.SelectionCacheHit,
                SelectionCacheKey = context.SelectionCacheKey ?? string.Empty,
                DecisionCacheRevalidationReason = context.DecisionCacheRejectedReason ?? string.Empty,
                PreferredTargetWhoAmI = context.PreferredTargetWhoAmI,
                PreferredTargetType = context.PreferredTargetType
            };

            if (readResult != null)
            {
                selection.HasCursorWorld = readResult.HasCursorWorld;
                selection.CursorWorldX = readResult.CursorWorldX;
                selection.CursorWorldY = readResult.CursorWorldY;
                selection.MouseScreenX = readResult.MouseScreenX;
                selection.MouseScreenY = readResult.MouseScreenY;
                selection.ScreenPositionX = readResult.ScreenPositionX;
                selection.ScreenPositionY = readResult.ScreenPositionY;
                selection.ScreenWidth = readResult.ScreenWidth;
                selection.ScreenHeight = readResult.ScreenHeight;
                selection.CandidateCount = readResult.Candidates == null ? 0 : readResult.Candidates.Count;
                selection.SkipReason = readResult.SkipReason ?? string.Empty;
            }

            // Disabled or unavailable frames stop before scoring samples and
            // line-of-sight work; selection diagnostics should stay cheap.
            if (!selection.Enabled || radiusTiles <= 0)
            {
                selection.ResultCode = "Disabled";
                selection.SkipReason = resolvedRange == null || string.IsNullOrWhiteSpace(resolvedRange.DisabledReason)
                    ? "radiusOff"
                    : resolvedRange.DisabledReason;
                return selection;
            }

            if (readResult == null || !readResult.CanSearch)
            {
                selection.ResultCode = "ReadUnavailable";
                selection.SkipReason = string.IsNullOrWhiteSpace(selection.SkipReason) ? "readUnavailable" : selection.SkipReason;
                return selection;
            }

            float rangeCenterX;
            float rangeCenterY;
            if (resolvedRange != null)
            {
                rangeCenterX = resolvedRange.RangeCenterWorldX;
                rangeCenterY = resolvedRange.RangeCenterWorldY;
            }
            else if (string.Equals(rangeOrigin, CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase))
            {
                if (!context.HasPlayerCenter)
                {
                    selection.ResultCode = "ReadUnavailable";
                    selection.SkipReason = CombineSkipReason(selection.SkipReason, "playerCenterUnavailable");
                    return selection;
                }

                rangeCenterX = context.PlayerCenterX;
                rangeCenterY = context.PlayerCenterY;
            }
            else
            {
                rangeCenterX = readResult.CursorWorldX;
                rangeCenterY = readResult.CursorWorldY;
            }

            selection.RangeCenterWorldX = rangeCenterX;
            selection.RangeCenterWorldY = rangeCenterY;

            var radiusPixels = radiusTiles * 16f;
            var inRange = BuildInRangeCandidates(readResult, rangeCenterX, rangeCenterY, radiusPixels);
            selection.InRangeCandidateCount = inRange.Count;
            if (inRange.Count == 0)
            {
                selection.ResultCode = "NoTarget";
                selection.SkipReason = CombineSkipReason(selection.SkipReason, selection.CandidateCount > 0 ? "outsideRadius" : "noCandidates");
                return selection;
            }

            inRange.Sort((left, right) =>
            {
                var distanceCompare = left.RangeDistance.CompareTo(right.RangeDistance);
                return distanceCompare != 0 ? distanceCompare : left.Target.WhoAmI.CompareTo(right.Target.WhoAmI);
            });

            var cheapLimit = Math.Min(MaxCheapCandidates, inRange.Count);
            if (inRange.Count > cheapLimit)
            {
                inRange.RemoveRange(cheapLimit, inRange.Count - cheapLimit);
            }

            for (var cheapIndex = 0; cheapIndex < inRange.Count; cheapIndex++)
            {
                inRange[cheapIndex].CheapScore = ScoreCheapCandidate(inRange[cheapIndex], readResult, context, priority, rangeCenterX, rangeCenterY);
            }

            inRange.Sort((left, right) =>
            {
                var scoreCompare = right.CheapScore.CompareTo(left.CheapScore);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }

                var distanceCompare = left.RangeDistance.CompareTo(right.RangeDistance);
                return distanceCompare != 0 ? distanceCompare : left.Target.WhoAmI.CompareTo(right.Target.WhoAmI);
            });

            selection.CheapCandidateCount = cheapLimit;
            var expensive = BuildExpensiveCandidates(inRange, cheapLimit, context);
            selection.ExpensiveCandidateCount = expensive.Count;
            selection.EvaluatedCandidateCount = expensive.Count;
            ScoredSample best = null;
            var lineOfSightRejectedSampleCount = 0;
            var nearestHitboxPointPenaltyApplied = false;
            var centerPreferred = false;
            for (var index = 0; index < expensive.Count; index++)
            {
                var candidate = expensive[index];
                var candidateBallistic = SolveCandidateCenterBallistic(candidate, context);
                var sampleSet = CombatAimPredictedHitboxSampler.BuildSamples(candidate.Target, rangeCenterX, rangeCenterY, candidateBallistic);
                var scoredSamples = new List<ScoredSample>(sampleSet.Samples.Count);
                var visibleSampleCount = 0;
                for (var sampleIndex = 0; sampleIndex < sampleSet.Samples.Count; sampleIndex++)
                {
                    var scored = ScoreSample(candidate, sampleSet.Samples[sampleIndex], readResult, context, priority, rangeCenterX, rangeCenterY, candidateBallistic, sampleSet);
                    if (scored == null)
                    {
                        continue;
                    }

                    if (scored.LineClearAvailable && scored.LineClear)
                    {
                        visibleSampleCount++;
                    }

                    scoredSamples.Add(scored);
                }

                for (var scoredIndex = 0; scoredIndex < scoredSamples.Count; scoredIndex++)
                {
                    var scored = scoredSamples[scoredIndex];
                    scored.VisibleSampleCount = visibleSampleCount;
                    if (scored.BallisticSolution != null)
                    {
                        scored.BallisticSolution.VisibleSampleCount = visibleSampleCount;
                    }

                    nearestHitboxPointPenaltyApplied = nearestHitboxPointPenaltyApplied || scored.NearestHitboxPointPenaltyApplied;
                    centerPreferred = centerPreferred || scored.CenterPreferred;
                    if (scored.Rejected)
                    {
                        lineOfSightRejectedSampleCount++;
                        continue;
                    }

                    if (best == null ||
                        scored.Score > best.Score ||
                        (NearlyEqual(scored.Score, best.Score) && scored.RangeDistance < best.RangeDistance) ||
                        (NearlyEqual(scored.Score, best.Score) && NearlyEqual(scored.RangeDistance, best.RangeDistance) && scored.Target.WhoAmI < best.Target.WhoAmI))
                    {
                        best = scored;
                    }
                }
            }

            selection.LineOfSightRejectedSampleCount = lineOfSightRejectedSampleCount;
            selection.NearestHitboxPointPenaltyApplied = nearestHitboxPointPenaltyApplied;
            selection.CenterPreferred = centerPreferred;
            if (best == null)
            {
                selection.ResultCode = "NoTarget";
                selection.SkipReason = CombineSkipReason(selection.SkipReason, lineOfSightRejectedSampleCount > 0 ? "blockedByLineOfSight" : "noScoredSample");
                return selection;
            }

            selection.Target = best.Target;
            selection.BallisticTarget = best.BallisticTarget;
            selection.BallisticSolution = best.BallisticSolution;
            selection.CenterDistanceTiles = Distance(rangeCenterX, rangeCenterY, best.Target.CenterX, best.Target.CenterY) / 16f;
            selection.HitboxDistanceTiles = HitboxDistance(rangeCenterX, rangeCenterY, best.Target) / 16f;
            selection.TargetDistanceFromRangeCenterTiles = best.RangeDistance / 16f;
            selection.TargetScore = best.Score;
            selection.LineClear = best.LineClear;
            selection.LineClearAvailable = best.LineClearAvailable;
            selection.LosCacheHit = best.LosCacheHit;
            selection.DistanceToPlayerCursorRay = best.DistanceToPlayerCursorRay;
            selection.InForwardCone = best.InForwardCone;
            selection.PreviousTargetBonus = best.PreviousTargetBonus;
            selection.SelectedSamplePoint = best.Sample.Name;
            selection.AttackSamplePoint = best.Sample.Name;
            selection.SelectionSamplePoint = best.Sample.Name;
            selection.SampleSpace = best.Sample.Space;
            selection.SelectedSampleWorldX = best.Sample.X;
            selection.SelectedSampleWorldY = best.Sample.Y;
            selection.PredictedHitboxCenterX = best.PredictedHitboxCenterX;
            selection.PredictedHitboxCenterY = best.PredictedHitboxCenterY;
            selection.VisibleSampleCount = best.VisibleSampleCount;
            selection.ProjectileHitRadius = best.ProjectileHitRadius;
            selection.SelectedReason = best.Reason;
            selection.LockedTargetStillValid = CombatAimTargetLockService.IsLockedTarget(best.Target.WhoAmI, best.Target.Type);
            selection.AttackTargetWhoAmI = best.Target.WhoAmI;
            selection.AttackTargetType = best.Target.Type;
            selection.MarkerTargetWhoAmI = context.PreferredTargetWhoAmI;
            selection.MarkerTargetType = context.PreferredTargetType;
            selection.MarkerAttackTargetMismatch = context.PreferredTargetWhoAmI >= 0 &&
                                                   (context.PreferredTargetWhoAmI != best.Target.WhoAmI ||
                                                    context.PreferredTargetType != best.Target.Type);
            selection.MarkerTargetChangedForAttack = selection.MarkerAttackTargetMismatch &&
                                                     string.Equals(selection.SelectionPurpose, "Attack", StringComparison.OrdinalIgnoreCase);
            selection.MarkerAttackMismatchReason = ResolveMarkerAttackMismatchReason(selection, context);
            selection.ResultCode = "TargetSelected";
            selection.SkipReason = string.IsNullOrWhiteSpace(selection.SkipReason) ? "none" : selection.SkipReason;
            return selection;
        }

        private static string ResolveMarkerAttackMismatchReason(
            CombatAimTargetSelection selection,
            CombatAimTargetSelectionContext context)
        {
            if (selection == null || !selection.MarkerAttackTargetMismatch)
            {
                return string.Empty;
            }

            var rejectedReason = context == null ? string.Empty : context.DecisionCacheRejectedReason ?? string.Empty;
            if (StartsWithOrdinal(rejectedReason, "targetStale"))
            {
                return "targetStale";
            }

            if (StartsWithOrdinal(rejectedReason, "lineOfSightChanged"))
            {
                return "lineOfSightChanged";
            }

            if (StartsWithOrdinal(rejectedReason, "weaponProfileChanged"))
            {
                return "weaponProfileChanged";
            }

            if (string.Equals(selection.SelectionPurpose, "PersistentCursor", StringComparison.OrdinalIgnoreCase))
            {
                return "persistentCursorEligibilityChanged";
            }

            return "itemCheckAttackRequiresStricterPath";
        }

        private static List<CandidateRange> BuildInRangeCandidates(CombatAimReadResult readResult, float rangeCenterX, float rangeCenterY, float radiusPixels)
        {
            var result = new List<CandidateRange>();
            if (readResult == null || readResult.Candidates == null)
            {
                return result;
            }

            for (var index = 0; index < readResult.Candidates.Count; index++)
            {
                var candidate = readResult.Candidates[index];
                if (candidate == null)
                {
                    continue;
                }

                var hitboxDistance = HitboxDistance(rangeCenterX, rangeCenterY, candidate);
                if (hitboxDistance > radiusPixels)
                {
                    continue;
                }

                result.Add(new CandidateRange
                {
                    Target = candidate,
                    RangeDistance = hitboxDistance
                });
            }

            return result;
        }

        private static List<CandidateRange> BuildExpensiveCandidates(List<CandidateRange> inRange, int cheapLimit, CombatAimTargetSelectionContext context)
        {
            var result = new List<CandidateRange>();
            if (inRange == null || cheapLimit <= 0)
            {
                return result;
            }

            if (context != null && context.PreferredTargetWhoAmI >= 0)
            {
                for (var index = 0; index < cheapLimit && index < inRange.Count; index++)
                {
                    var candidate = inRange[index];
                    if (candidate != null &&
                        candidate.Target != null &&
                        candidate.Target.WhoAmI == context.PreferredTargetWhoAmI &&
                        candidate.Target.Type == context.PreferredTargetType)
                    {
                        result.Add(candidate);
                        return result;
                    }
                }

                if (context.RequirePreferredTarget)
                {
                    return result;
                }
            }

            var limit = Math.Min(MaxExpensiveCandidates, cheapLimit);
            for (var index = 0; index < limit && index < inRange.Count; index++)
            {
                result.Add(inRange[index]);
            }

            return result;
        }

        private static float ScoreCheapCandidate(
            CandidateRange candidate,
            CombatAimReadResult readResult,
            CombatAimTargetSelectionContext context,
            string priority,
            float rangeCenterX,
            float rangeCenterY)
        {
            var target = candidate == null ? null : candidate.Target;
            if (target == null || readResult == null)
            {
                return float.MinValue;
            }

            var playerX = context != null && context.HasPlayerCenter ? context.PlayerCenterX : rangeCenterX;
            var playerY = context != null && context.HasPlayerCenter ? context.PlayerCenterY : rangeCenterY;
            var nearestX = Clamp(rangeCenterX, target.HitboxX, target.HitboxX + Math.Max(1f, target.HitboxWidth));
            var nearestY = Clamp(rangeCenterY, target.HitboxY, target.HitboxY + Math.Max(1f, target.HitboxHeight));
            var rangeDistance = Distance(rangeCenterX, rangeCenterY, nearestX, nearestY);
            var rayDistance = DistanceToRay(playerX, playerY, readResult.CursorWorldX, readResult.CursorWorldY, target.CenterX, target.CenterY);
            var forward = IsForward(playerX, playerY, readResult.CursorWorldX, readResult.CursorWorldY, target.CenterX, target.CenterY);
            var previousBonus = CombatAimTargetLockService.IsLockedTarget(target.WhoAmI, target.Type) ? PreviousTargetBonusValue : 0f;
            var preferredBonus = context != null &&
                                 context.PreferredTargetWhoAmI == target.WhoAmI &&
                                 context.PreferredTargetType == target.Type
                ? 250f
                : 0f;
            var threatBonus = target.LifeMax >= 2000 ? 24f : target.LifeMax >= 400 ? 10f : 0f;
            var playerMode = context != null &&
                             string.Equals(CombatAimModes.NormalizeRangeOrigin(context.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(priority, CombatAimModes.TargetPriorityNearest, StringComparison.OrdinalIgnoreCase))
            {
                return 10000f - rangeDistance + previousBonus * 0.45f + preferredBonus + threatBonus;
            }

            var score = 0f;
            if (playerMode)
            {
                score += forward ? 160f : -220f;
                score += Math.Max(0f, 520f - rayDistance) * 0.95f;
                score += Math.Max(0f, 900f - rangeDistance) * 0.22f;
            }
            else
            {
                score += forward ? 70f : -90f;
                score += Math.Max(0f, 560f - rangeDistance) * 0.82f;
                score += Math.Max(0f, 440f - rayDistance) * 0.48f;
            }

            score += previousBonus;
            score += preferredBonus;
            score += threatBonus;
            return score;
        }

        private static ScoredSample ScoreSample(
            CandidateRange candidate,
            CombatAimHitboxSample sample,
            CombatAimReadResult readResult,
            CombatAimTargetSelectionContext context,
            string priority,
            float rangeCenterX,
            float rangeCenterY,
            CombatAimBallisticSolution candidateBallistic,
            CombatAimHitboxSampleSet sampleSet)
        {
            if (candidate == null || candidate.Target == null || sample == null || readResult == null || context == null)
            {
                return null;
            }

            var playerX = context.HasPlayerCenter ? context.PlayerCenterX : rangeCenterX;
            var playerY = context.HasPlayerCenter ? context.PlayerCenterY : rangeCenterY;
            var rangeDistance = Distance(rangeCenterX, rangeCenterY, sample.X, sample.Y);
            var cursorRayDistance = DistanceToRay(playerX, playerY, readResult.CursorWorldX, readResult.CursorWorldY, sample.X, sample.Y);
            var inForwardCone = IsForward(playerX, playerY, readResult.CursorWorldX, readResult.CursorWorldY, sample.X, sample.Y);
            var nearestPriority = string.Equals(priority, CombatAimModes.TargetPriorityNearest, StringComparison.OrdinalIgnoreCase);
            var clearLinePriority = string.Equals(priority, CombatAimModes.TargetPriorityClearLine, StringComparison.OrdinalIgnoreCase);
            var lineClear = false;
            var lineAvailable = false;
            var losCacheHit = false;
            if (!nearestPriority)
            {
                lineAvailable = CombatAimLineOfSight.TryCanHitLine(playerX, playerY, sample.X, sample.Y, out lineClear);
                losCacheHit = CombatAimLineOfSight.LastCacheHit;
            }

            var previousBonus = CombatAimTargetLockService.IsLockedTarget(candidate.Target.WhoAmI, candidate.Target.Type) ? PreviousTargetBonusValue : 0f;
            bool nearestHitboxPenalty;
            bool centerPreferred;
            var sampleBias = GetSampleBias(sample.Name, out nearestHitboxPenalty, out centerPreferred);
            var ballisticTarget = CreateBallisticTarget(candidate.Target, sample);
            var predictedHitboxCenterX = sampleSet == null ? sample.HitboxCenterX : sampleSet.PredictedHitboxCenterX;
            var predictedHitboxCenterY = sampleSet == null ? sample.HitboxCenterY : sampleSet.PredictedHitboxCenterY;

            if (clearLinePriority && lineAvailable && !lineClear)
            {
                return new ScoredSample
                {
                    Target = candidate.Target,
                    BallisticTarget = ballisticTarget,
                    BallisticSolution = null,
                    Sample = sample,
                    Score = float.MinValue,
                    RangeDistance = rangeDistance,
                    LineClear = lineClear,
                    LineClearAvailable = lineAvailable,
                    LosCacheHit = losCacheHit,
                    DistanceToPlayerCursorRay = cursorRayDistance,
                    InForwardCone = inForwardCone,
                    PreviousTargetBonus = previousBonus,
                    Reason = "blockedByLineOfSight",
                    Rejected = true,
                    NearestHitboxPointPenaltyApplied = nearestHitboxPenalty,
                    CenterPreferred = centerPreferred,
                    ProjectileHitRadius = sampleSet == null ? 0f : sampleSet.ProjectileHitRadius,
                    PredictedHitboxCenterX = predictedHitboxCenterX,
                    PredictedHitboxCenterY = predictedHitboxCenterY
                };
            }

            CombatAimBallisticSolution ballistic = null;
            if (context.IncludeBallisticScoring && context.Player != null && context.WeaponProfile != null)
            {
                ballistic = string.Equals(sample.Space, CombatAimPredictedHitboxSampler.SampleSpacePredicted, StringComparison.Ordinal)
                    ? BindPredictedSampleBallistic(candidateBallistic, sample)
                    : context.BallisticContext == null
                        ? CombatAimBallisticSolver.Solve(context.Player, context.WeaponProfile, ballisticTarget)
                        : CombatAimBallisticSolver.Solve(context.BallisticContext, ballisticTarget);
            }

            BindSampleMetadata(ballistic, sample, sampleSet);

            float score;
            string reason;
            if (string.Equals(priority, CombatAimModes.TargetPriorityNearest, StringComparison.OrdinalIgnoreCase))
            {
                score = 10000f - rangeDistance + sampleBias;
                if (lineAvailable && lineClear)
                {
                    score += 20f;
                }

                score += previousBonus * 0.5f;
                reason = "nearest";
            }
            else
            {
                var playerMode = string.Equals(CombatAimModes.NormalizeRangeOrigin(context.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase);
                score = sampleBias + ResolveSampleSpaceBias(sample, ballistic);
                score += sample.CoverageScore * (IsLargeTarget(candidate.Target) ? 58f : 82f);
                score -= sample.CenterDistanceRatio * ResolveCenterDistancePenalty(candidate.Target, ballistic);
                if (lineAvailable)
                {
                    score += lineClear ? 360f : -460f;
                    if (lineClear && IsLargeTarget(candidate.Target))
                    {
                        score += 95f;
                    }
                }

                if (playerMode)
                {
                    score += inForwardCone ? 190f : -250f;
                    score += Math.Max(0f, 520f - cursorRayDistance) * 1.05f;
                    score += Math.Max(0f, 820f - rangeDistance) * 0.22f;
                    score += Math.Max(0f, 680f - Distance(playerX, playerY, sample.X, sample.Y)) * 0.12f;
                }
                else
                {
                    score += inForwardCone ? 80f : -110f;
                    score += Math.Max(0f, 600f - rangeDistance) * 0.78f;
                    score += Math.Max(0f, 420f - cursorRayDistance) * 0.52f;
                    score += Math.Max(0f, 520f - Distance(playerX, playerY, sample.X, sample.Y)) * 0.08f;
                }

                score += candidate.Target.LifeMax >= 2000 ? 24f : candidate.Target.LifeMax >= 400 ? 10f : 0f;
                score += previousBonus;
                if (ballistic != null)
                {
                    score += ballistic.Solved ? 50f : -30f;
                    score += ballistic.ConservativeCenter ? -20f : 20f;
                    score -= Math.Min(80f, ballistic.GravityCompensationPixels * 0.15f);
                    score -= Math.Min(70f, ballistic.LeadTicks * 0.35f);
                }

                reason = "clearLine";
            }

            return new ScoredSample
            {
                Target = candidate.Target,
                BallisticTarget = ballisticTarget,
                BallisticSolution = ballistic,
                Sample = sample,
                Score = score,
                RangeDistance = rangeDistance,
                LineClear = lineClear,
                LineClearAvailable = lineAvailable,
                LosCacheHit = losCacheHit,
                DistanceToPlayerCursorRay = cursorRayDistance,
                InForwardCone = inForwardCone,
                PreviousTargetBonus = previousBonus,
                Reason = reason,
                Rejected = false,
                NearestHitboxPointPenaltyApplied = nearestHitboxPenalty,
                CenterPreferred = centerPreferred,
                ProjectileHitRadius = sampleSet == null ? 0f : sampleSet.ProjectileHitRadius,
                PredictedHitboxCenterX = predictedHitboxCenterX,
                PredictedHitboxCenterY = predictedHitboxCenterY
            };
        }

        private static CombatAimBallisticSolution SolveCandidateCenterBallistic(
            CandidateRange candidate,
            CombatAimTargetSelectionContext context)
        {
            if (candidate == null ||
                candidate.Target == null ||
                context == null ||
                !context.IncludeBallisticScoring ||
                context.Player == null ||
                context.WeaponProfile == null)
            {
                return null;
            }

            return context.BallisticContext == null
                ? CombatAimBallisticSolver.Solve(context.Player, context.WeaponProfile, candidate.Target)
                : CombatAimBallisticSolver.Solve(context.BallisticContext, candidate.Target);
        }

        private static CombatTargetSnapshot CreateBallisticTarget(CombatTargetSnapshot target, CombatAimHitboxSample sample)
        {
            var clone = target.CloneForAimSample(sample.X, sample.Y);
            clone.PositionX = sample.HitboxLeft;
            clone.PositionY = sample.HitboxTop;
            clone.HitboxX = sample.HitboxLeft;
            clone.HitboxY = sample.HitboxTop;
            clone.HitboxWidth = sample.HitboxWidth;
            clone.HitboxHeight = sample.HitboxHeight;
            return clone;
        }

        private static CombatAimBallisticSolution BindPredictedSampleBallistic(
            CombatAimBallisticSolution candidateBallistic,
            CombatAimHitboxSample sample)
        {
            if (candidateBallistic == null || sample == null)
            {
                return null;
            }

            var clone = candidateBallistic.Clone();
            clone.AimWorldX = sample.X;
            clone.AimWorldY = sample.Y;
            clone.AimAdjusted = clone.AimAdjusted || Distance(sample.X, sample.Y, sample.HitboxCenterX, sample.HitboxCenterY) > 1f;
            return clone;
        }

        private static void BindSampleMetadata(
            CombatAimBallisticSolution ballistic,
            CombatAimHitboxSample sample,
            CombatAimHitboxSampleSet sampleSet)
        {
            if (ballistic == null || sample == null)
            {
                return;
            }

            ballistic.SampleSpace = sample.Space ?? string.Empty;
            ballistic.SelectedSamplePoint = sample.Name ?? string.Empty;
            ballistic.SelectedSampleWorldX = sample.X;
            ballistic.SelectedSampleWorldY = sample.Y;
            ballistic.PredictedHitboxCenterX = sampleSet == null ? sample.HitboxCenterX : sampleSet.PredictedHitboxCenterX;
            ballistic.PredictedHitboxCenterY = sampleSet == null ? sample.HitboxCenterY : sampleSet.PredictedHitboxCenterY;
            ballistic.ProjectileHitRadius = sampleSet == null ? 0f : sampleSet.ProjectileHitRadius;
        }

        private static float ResolveSampleSpaceBias(CombatAimHitboxSample sample, CombatAimBallisticSolution ballistic)
        {
            if (sample == null || !string.Equals(sample.Space, CombatAimPredictedHitboxSampler.SampleSpacePredicted, StringComparison.Ordinal))
            {
                return IsLowConfidence(ballistic) ? 36f : 0f;
            }

            var leadDistance = ballistic == null
                ? 0f
                : Distance(ballistic.PredictedTargetX, ballistic.PredictedTargetY, ballistic.AimWorldX, ballistic.AimWorldY);
            if (ballistic == null ||
                string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.Unknown, StringComparison.Ordinal) ||
                string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.VeryLow, StringComparison.Ordinal))
            {
                return -220f - Math.Min(90f, leadDistance * 0.3f);
            }

            if (string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.Low, StringComparison.Ordinal))
            {
                return -85f - Math.Min(55f, leadDistance * 0.16f);
            }

            if (string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.Medium, StringComparison.Ordinal))
            {
                return 82f - Math.Min(35f, leadDistance * 0.06f);
            }

            return 142f - Math.Min(30f, leadDistance * 0.04f);
        }

        private static float ResolveCenterDistancePenalty(CombatTargetSnapshot target, CombatAimBallisticSolution ballistic)
        {
            if (IsLowConfidence(ballistic))
            {
                return 78f;
            }

            return IsLargeTarget(target) ? 20f : 34f;
        }

        private static bool IsLowConfidence(CombatAimBallisticSolution ballistic)
        {
            return ballistic == null ||
                   string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.Unknown, StringComparison.Ordinal) ||
                   string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.VeryLow, StringComparison.Ordinal) ||
                   string.Equals(ballistic.PredictionConfidence, CombatAimPredictionConfidenceKinds.Low, StringComparison.Ordinal);
        }

        private static bool IsLargeTarget(CombatTargetSnapshot target)
        {
            if (target == null)
            {
                return false;
            }

            if (target.HitboxWidth >= 96f || target.HitboxHeight >= 96f)
            {
                return true;
            }

            return target.MotionProfile != null &&
                   string.Equals(target.MotionProfile.MotionProfileKind, CombatAimTargetMotionProfile.LargeOrSegmented, StringComparison.Ordinal);
        }

        private static float GetSampleBias(string sampleName, out bool nearestHitboxPenaltyApplied, out bool centerPreferred)
        {
            nearestHitboxPenaltyApplied = false;
            centerPreferred = false;
            if (string.Equals(sampleName, "center", StringComparison.OrdinalIgnoreCase))
            {
                centerPreferred = true;
                return 180f;
            }

            if (string.Equals(sampleName, "topMid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sampleName, "bottomMid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sampleName, "leftMid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sampleName, "rightMid", StringComparison.OrdinalIgnoreCase))
            {
                return 45f;
            }

            if (string.Equals(sampleName, "nearestHitboxPoint", StringComparison.OrdinalIgnoreCase))
            {
                nearestHitboxPenaltyApplied = true;
                return -280f;
            }

            return 0f;
        }

        private static float HitboxDistance(float x, float y, CombatTargetSnapshot target)
        {
            var right = target.HitboxX + Math.Max(1f, target.HitboxWidth);
            var bottom = target.HitboxY + Math.Max(1f, target.HitboxHeight);
            var nearestX = Clamp(x, target.HitboxX, right);
            var nearestY = Clamp(y, target.HitboxY, bottom);
            return Distance(x, y, nearestX, nearestY);
        }

        private static float DistanceToRay(float originX, float originY, float cursorX, float cursorY, float pointX, float pointY)
        {
            var dx = cursorX - originX;
            var dy = cursorY - originY;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 16f)
            {
                return Distance(originX, originY, pointX, pointY);
            }

            var t = ((pointX - originX) * dx + (pointY - originY) * dy) / lenSq;
            var projectedX = originX + dx * t;
            var projectedY = originY + dy * t;
            return Distance(projectedX, projectedY, pointX, pointY);
        }

        private static bool IsForward(float originX, float originY, float cursorX, float cursorY, float pointX, float pointY)
        {
            var dx = cursorX - originX;
            var dy = cursorY - originY;
            var lenSq = dx * dx + dy * dy;
            if (lenSq < 16f)
            {
                return true;
            }

            return (pointX - originX) * dx + (pointY - originY) * dy >= -32f;
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

        private static bool NearlyEqual(float a, float b)
        {
            return Math.Abs(a - b) <= 0.001f;
        }

        private static bool StartsWithOrdinal(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(prefix) &&
                   value.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static string CombineSkipReason(string existing, string reason)
        {
            if (string.IsNullOrWhiteSpace(existing) || string.Equals(existing, "none", StringComparison.OrdinalIgnoreCase))
            {
                return reason ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return existing;
            }

            return existing + ";" + reason;
        }

        private sealed class CandidateRange
        {
            public CombatTargetSnapshot Target;
            public float RangeDistance;
            public float CheapScore;
        }

        private sealed class ScoredSample
        {
            public CombatTargetSnapshot Target;
            public CombatTargetSnapshot BallisticTarget;
            public CombatAimBallisticSolution BallisticSolution;
            public CombatAimHitboxSample Sample;
            public float Score;
            public float RangeDistance;
            public bool LineClear;
            public bool LineClearAvailable;
            public bool LosCacheHit;
            public float DistanceToPlayerCursorRay;
            public bool InForwardCone;
            public float PreviousTargetBonus;
            public string Reason;
            public bool Rejected;
            public bool NearestHitboxPointPenaltyApplied;
            public bool CenterPreferred;
            public int VisibleSampleCount;
            public float ProjectileHitRadius;
            public float PredictedHitboxCenterX;
            public float PredictedHitboxCenterY;
        }
    }
}
