using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Fishing
{
    internal static class FishingBobberObserver
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, ObservationRecord> Observations =
            new Dictionary<int, ObservationRecord>();
        private static readonly List<int> RemoveIdentities = new List<int>();
        private static long _lastObservationTick;
        private static long _lastNoActiveObservationTick;
        private static long _scanGeneration;
        private static bool _hasLatest;
        private static int _latestIdentity;
        private static long _latestGameUpdateCount;
        private static int _latestWhoAmI;

        public static long LastObservationTick
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastObservationTick;
                }
            }
        }

        public static bool HasActiveObservation
        {
            get
            {
                lock (SyncRoot)
                {
                    return HasActiveObservationLocked();
                }
            }
        }

        public static long LastNoActiveObservationTick
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastNoActiveObservationTick;
                }
            }
        }

        public static void Observe(FishingBobberObservation observation)
        {
            if (observation == null || !observation.Active || !observation.Bobber)
            {
                return;
            }

            lock (SyncRoot)
            {
                UpsertLocked(observation, _scanGeneration);
            }
        }

        public static void MarkNoActiveObservation(long gameUpdateCount)
        {
            if (gameUpdateCount <= 0)
            {
                return;
            }

            lock (SyncRoot)
            {
                _lastNoActiveObservationTick = Math.Max(_lastNoActiveObservationTick, gameUpdateCount);
            }
        }

        public static bool TryGetLatest(out FishingBobberObservation observation)
        {
            lock (SyncRoot)
            {
                if (Observations.Count == 0)
                {
                    observation = null;
                    return false;
                }

                ObservationRecord latest;
                if (_hasLatest &&
                    Observations.TryGetValue(_latestIdentity, out latest) &&
                    latest != null &&
                    IsActiveBobber(latest.Observation))
                {
                    observation = Clone(latest.Observation);
                    return true;
                }

                FishingBobberObservation rebuilt;
                if (RebuildLatestLocked(out rebuilt))
                {
                    observation = Clone(rebuilt);
                    return true;
                }

                observation = null;
                return false;
            }
        }

        public static bool TryGetByIdentity(int identity, out FishingBobberObservation observation)
        {
            lock (SyncRoot)
            {
                ObservationRecord value;
                if (Observations.TryGetValue(identity, out value) && value != null)
                {
                    observation = Clone(value.Observation);
                    return true;
                }
            }

            observation = null;
            return false;
        }

        public static void RemoveMissing(IReadOnlyList<FishingBobberObservation> current)
        {
            lock (SyncRoot)
            {
                if (current == null || current.Count == 0)
                {
                    Observations.Clear();
                    RemoveIdentities.Clear();
                    _lastNoActiveObservationTick = 0;
                    ResetLatestLocked();
                    return;
                }

                var generation = ++_scanGeneration;
                for (var index = 0; index < current.Count; index++)
                {
                    if (current[index] != null)
                    {
                        UpsertLocked(current[index], generation);
                    }
                }

                RemoveIdentities.Clear();
                foreach (var pair in Observations)
                {
                    if (pair.Value == null || pair.Value.Generation != generation)
                    {
                        RemoveIdentities.Add(pair.Key);
                    }
                }

                for (var index = 0; index < RemoveIdentities.Count; index++)
                {
                    Observations.Remove(RemoveIdentities[index]);
                }

                RemoveIdentities.Clear();
                FishingBobberObservation latest;
                RebuildLatestLocked(out latest);
            }
        }

        public static bool HasFreshActiveObservation(long currentGameUpdateCount, int maxAgeTicks)
        {
            lock (SyncRoot)
            {
                if (!HasActiveObservationLocked() ||
                    _lastObservationTick <= 0 ||
                    currentGameUpdateCount <= 0)
                {
                    return false;
                }

                var age = currentGameUpdateCount - _lastObservationTick;
                return age >= 0 && age <= Math.Max(0, maxAgeTicks);
            }
        }

        public static bool HasFreshNoActiveObservation(long currentGameUpdateCount, int maxAgeTicks)
        {
            lock (SyncRoot)
            {
                if (_lastNoActiveObservationTick <= 0 ||
                    currentGameUpdateCount <= 0 ||
                    _lastObservationTick > _lastNoActiveObservationTick ||
                    HasActiveObservationLocked())
                {
                    return false;
                }

                var age = currentGameUpdateCount - _lastNoActiveObservationTick;
                return age >= 0 && age <= Math.Max(0, maxAgeTicks);
            }
        }

        private static void UpsertLocked(FishingBobberObservation observation, long generation)
        {
            var clone = Clone(observation);
            ObservationRecord record;
            if (Observations.TryGetValue(clone.Identity, out record) && record != null)
            {
                record.Observation = clone;
                record.Generation = generation;
            }
            else
            {
                Observations[clone.Identity] = new ObservationRecord
                {
                    Observation = clone,
                    Generation = generation
                };
            }

            _lastObservationTick = Math.Max(_lastObservationTick, clone.GameUpdateCount);
            if (IsActiveBobber(clone) && IsNewerThanLatestLocked(clone))
            {
                SetLatestLocked(clone);
            }
            else if (_hasLatest && clone.Identity == _latestIdentity)
            {
                SetLatestLocked(clone);
            }
        }

        private static bool RebuildLatestLocked(out FishingBobberObservation observation)
        {
            FishingBobberObservation best = null;
            foreach (var pair in Observations)
            {
                var current = pair.Value == null ? null : pair.Value.Observation;
                if (!IsActiveBobber(current))
                {
                    continue;
                }

                if (best == null || IsNewer(current, best))
                {
                    best = current;
                }
            }

            if (best == null)
            {
                ResetLatestLocked();
                observation = null;
                return false;
            }

            SetLatestLocked(best);
            observation = best;
            return true;
        }

        private static bool IsNewerThanLatestLocked(FishingBobberObservation observation)
        {
            return !_hasLatest ||
                observation.GameUpdateCount > _latestGameUpdateCount ||
                (observation.GameUpdateCount == _latestGameUpdateCount && observation.WhoAmI > _latestWhoAmI);
        }

        private static bool IsNewer(FishingBobberObservation candidate, FishingBobberObservation current)
        {
            return candidate.GameUpdateCount > current.GameUpdateCount ||
                (candidate.GameUpdateCount == current.GameUpdateCount && candidate.WhoAmI > current.WhoAmI);
        }

        private static bool IsActiveBobber(FishingBobberObservation observation)
        {
            return observation != null && observation.Active && observation.Bobber;
        }

        private static bool HasActiveObservationLocked()
        {
            if (Observations.Count == 0)
            {
                return false;
            }

            ObservationRecord latest;
            if (_hasLatest &&
                Observations.TryGetValue(_latestIdentity, out latest) &&
                latest != null &&
                IsActiveBobber(latest.Observation))
            {
                return true;
            }

            foreach (var pair in Observations)
            {
                if (pair.Value != null && IsActiveBobber(pair.Value.Observation))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetLatestLocked(FishingBobberObservation observation)
        {
            _hasLatest = true;
            _latestIdentity = observation.Identity;
            _latestGameUpdateCount = observation.GameUpdateCount;
            _latestWhoAmI = observation.WhoAmI;
        }

        private static void ResetLatestLocked()
        {
            _hasLatest = false;
            _latestIdentity = 0;
            _latestGameUpdateCount = 0;
            _latestWhoAmI = 0;
        }

        private static FishingBobberObservation Clone(FishingBobberObservation source)
        {
            if (source == null)
            {
                return null;
            }

            return new FishingBobberObservation
            {
                GameUpdateCount = source.GameUpdateCount,
                Identity = source.Identity,
                WhoAmI = source.WhoAmI,
                Type = source.Type,
                Owner = source.Owner,
                Active = source.Active,
                Bobber = source.Bobber,
                InLiquid = source.InLiquid,
                LiquidStateKnown = source.LiquidStateKnown,
                LiquidKind = source.LiquidKind,
                Ai1 = source.Ai1,
                LocalAi1 = source.LocalAi1,
                CenterX = source.CenterX,
                CenterY = source.CenterY
            };
        }

        private sealed class ObservationRecord
        {
            public FishingBobberObservation Observation { get; set; }
            public long Generation { get; set; }
        }
    }
}
