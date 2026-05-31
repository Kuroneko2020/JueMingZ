using System;
using System.Collections.Generic;
using System.Linq;

namespace JueMingZ.Automation.Fishing
{
    internal static class FishingBobberObserver
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, FishingBobberObservation> Observations =
            new Dictionary<int, FishingBobberObservation>();
        private static long _lastObservationTick;

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

        public static void Observe(FishingBobberObservation observation)
        {
            if (observation == null || !observation.Active || !observation.Bobber)
            {
                return;
            }

            lock (SyncRoot)
            {
                Observations[observation.Identity] = Clone(observation);
                _lastObservationTick = Math.Max(_lastObservationTick, observation.GameUpdateCount);
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

                observation = Clone(Observations.Values
                    .OrderByDescending(value => value.GameUpdateCount)
                    .ThenByDescending(value => value.WhoAmI)
                    .FirstOrDefault());
                return observation != null;
            }
        }

        public static bool TryGetByIdentity(int identity, out FishingBobberObservation observation)
        {
            lock (SyncRoot)
            {
                FishingBobberObservation value;
                if (Observations.TryGetValue(identity, out value))
                {
                    observation = Clone(value);
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
                    return;
                }

                var live = new HashSet<int>();
                for (var index = 0; index < current.Count; index++)
                {
                    if (current[index] != null)
                    {
                        live.Add(current[index].Identity);
                        Observations[current[index].Identity] = Clone(current[index]);
                        _lastObservationTick = Math.Max(_lastObservationTick, current[index].GameUpdateCount);
                    }
                }

                var remove = Observations.Keys.Where(key => !live.Contains(key)).ToList();
                for (var index = 0; index < remove.Count; index++)
                {
                    Observations.Remove(remove[index]);
                }
            }
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
    }
}
