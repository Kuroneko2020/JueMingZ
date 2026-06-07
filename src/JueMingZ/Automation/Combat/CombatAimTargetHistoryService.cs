using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Combat
{
    // Target history enriches scoring only; stale or missing history must not force a target lock or an attack.
    public static class CombatAimTargetHistoryService
    {
        private const long StaleTicks = 180;
        private const float TeleportDistancePixels = 360f;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();

        public static void Clear()
        {
            lock (SyncRoot)
            {
                Entries.Clear();
            }
        }

        public static void Enrich(IList<CombatTargetSnapshot> targets)
        {
            if (targets == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                for (var index = 0; index < targets.Count; index++)
                {
                    var target = targets[index];
                    if (target == null)
                    {
                        continue;
                    }

                    Entry entry;
                    if (!Entries.TryGetValue(target.WhoAmI, out entry) || entry.Type != target.Type)
                    {
                        continue;
                    }

                    target.SmoothedVelocityAvailable = entry.HasVelocity;
                    target.SmoothedVelocityX = entry.SmoothedVelocityX;
                    target.SmoothedVelocityY = entry.SmoothedVelocityY;
                }
            }
        }

        public static void UpdateFromRead(CombatAimReadResult readResult, long tick)
        {
            if (readResult == null || readResult.Candidates == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var seen = new HashSet<int>();
                for (var index = 0; index < readResult.Candidates.Count; index++)
                {
                    var target = readResult.Candidates[index];
                    if (target == null)
                    {
                        continue;
                    }

                    seen.Add(target.WhoAmI);
                    Entry previous;
                    if (!Entries.TryGetValue(target.WhoAmI, out previous) || previous.Type != target.Type)
                    {
                        previous = new Entry
                        {
                            Type = target.Type,
                            CenterX = target.CenterX,
                            CenterY = target.CenterY,
                            SmoothedVelocityX = target.VelocityX,
                            SmoothedVelocityY = target.VelocityY,
                            HasVelocity = true,
                            LastSeenTick = tick
                        };
                    }
                    else
                    {
                        var tickDelta = Math.Max(1, tick - previous.LastSeenTick);
                        var measuredX = (target.CenterX - previous.CenterX) / tickDelta;
                        var measuredY = (target.CenterY - previous.CenterY) / tickDelta;
                        var moved = Distance(target.CenterX, target.CenterY, previous.CenterX, previous.CenterY);
                        if (moved > TeleportDistancePixels || tickDelta > 20)
                        {
                            previous.SmoothedVelocityX = target.VelocityX;
                            previous.SmoothedVelocityY = target.VelocityY;
                        }
                        else
                        {
                            previous.SmoothedVelocityX = previous.SmoothedVelocityX * 0.65f + measuredX * 0.35f;
                            previous.SmoothedVelocityY = previous.SmoothedVelocityY * 0.65f + measuredY * 0.35f;
                        }

                        previous.CenterX = target.CenterX;
                        previous.CenterY = target.CenterY;
                        previous.LastSeenTick = tick;
                        previous.HasVelocity = true;
                    }

                    target.SmoothedVelocityAvailable = previous.HasVelocity;
                    target.SmoothedVelocityX = previous.SmoothedVelocityX;
                    target.SmoothedVelocityY = previous.SmoothedVelocityY;
                    Entries[target.WhoAmI] = previous;
                }

                var stale = new List<int>();
                foreach (var pair in Entries)
                {
                    if (tick - pair.Value.LastSeenTick > StaleTicks || !seen.Contains(pair.Key))
                    {
                        stale.Add(pair.Key);
                    }
                }

                for (var index = 0; index < stale.Count; index++)
                {
                    Entries.Remove(stale[index]);
                }
            }
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private struct Entry
        {
            public int Type;
            public float CenterX;
            public float CenterY;
            public float SmoothedVelocityX;
            public float SmoothedVelocityY;
            public bool HasVelocity;
            public long LastSeenTick;
        }
    }
}
