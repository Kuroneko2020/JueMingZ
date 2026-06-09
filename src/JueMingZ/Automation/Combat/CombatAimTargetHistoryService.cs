using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Combat
{
    // Target history enriches scoring only; stale or missing history must not force a target lock or an attack.
    public static class CombatAimTargetHistoryService
    {
        private const long StaleTicks = 180;
        private const long StaleGapTicks = 20;
        private const float TeleportDistancePixels = 360f;
        private const float MaxMeasuredVelocityPixelsPerTick = 34f;
        private const float MaxAccelerationPerTick = 3f;
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
                    target.LastReadTick = entry.LastSeenTick;
                    target.MotionProfile = entry.MotionProfile == null ? null : entry.MotionProfile.Clone();
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
                    if (!Entries.TryGetValue(target.WhoAmI, out previous))
                    {
                        previous = CreateInitialEntry(target, tick, string.Empty);
                    }
                    else if (previous.Type != target.Type)
                    {
                        previous = CreateInitialEntry(target, tick, "typeChanged");
                    }
                    else
                    {
                        var tickDelta = Math.Max(1, tick - previous.LastSeenTick);
                        var measuredX = (target.CenterX - previous.CenterX) / tickDelta;
                        var measuredY = (target.CenterY - previous.CenterY) / tickDelta;
                        var moved = Distance(target.CenterX, target.CenterY, previous.CenterX, previous.CenterY);
                        var measuredSpeed = Distance(measuredX, measuredY, 0f, 0f);
                        var resetReason = string.Empty;
                        if (tickDelta > StaleGapTicks)
                        {
                            resetReason = "staleTickGap";
                        }
                        else if (moved > TeleportDistancePixels)
                        {
                            resetReason = "teleportDistance";
                        }
                        else if (measuredSpeed > MaxMeasuredVelocityPixelsPerTick)
                        {
                            resetReason = "measuredVelocitySpike";
                        }

                        if (!string.IsNullOrWhiteSpace(resetReason))
                        {
                            previous = CreateInitialEntry(target, tick, resetReason);
                        }
                        else
                        {
                            var previousSmoothedX = previous.SmoothedVelocityX;
                            var previousSmoothedY = previous.SmoothedVelocityY;
                            var entityAccelerationX = target.VelocityX - previous.LastVelocityX;
                            var entityAccelerationY = target.VelocityY - previous.LastVelocityY;
                            previous.SmoothedVelocityX = previous.SmoothedVelocityX * 0.65f + measuredX * 0.35f;
                            previous.SmoothedVelocityY = previous.SmoothedVelocityY * 0.65f + measuredY * 0.35f;
                            previous.AccelerationX = (previous.SmoothedVelocityX - previousSmoothedX) * 0.65f + entityAccelerationX * 0.35f;
                            previous.AccelerationY = (previous.SmoothedVelocityY - previousSmoothedY) * 0.65f + entityAccelerationY * 0.35f;
                            ClampAcceleration(ref previous.AccelerationX, ref previous.AccelerationY);
                            previous.MotionProfile = BuildMotionProfile(
                                target,
                                true,
                                measuredX,
                                measuredY,
                                previous.SmoothedVelocityX,
                                previous.SmoothedVelocityY,
                                previous.AccelerationX,
                                previous.AccelerationY,
                                string.Empty);

                            previous.CenterX = target.CenterX;
                            previous.CenterY = target.CenterY;
                            previous.LastSeenTick = tick;
                            previous.HasVelocity = true;
                            previous.LastVelocityX = target.VelocityX;
                            previous.LastVelocityY = target.VelocityY;
                        }
                    }

                    target.SmoothedVelocityAvailable = previous.HasVelocity;
                    target.SmoothedVelocityX = previous.SmoothedVelocityX;
                    target.SmoothedVelocityY = previous.SmoothedVelocityY;
                    target.LastReadTick = previous.LastSeenTick;
                    target.MotionProfile = previous.MotionProfile == null ? null : previous.MotionProfile.Clone();
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

        private static Entry CreateInitialEntry(CombatTargetSnapshot target, long tick, string resetReason)
        {
            var entry = new Entry
            {
                Type = target.Type,
                CenterX = target.CenterX,
                CenterY = target.CenterY,
                LastVelocityX = target.VelocityX,
                LastVelocityY = target.VelocityY,
                SmoothedVelocityX = target.VelocityX,
                SmoothedVelocityY = target.VelocityY,
                AccelerationX = 0f,
                AccelerationY = 0f,
                HasVelocity = true,
                LastSeenTick = tick
            };

            entry.MotionProfile = BuildMotionProfile(
                target,
                false,
                target.VelocityX,
                target.VelocityY,
                entry.SmoothedVelocityX,
                entry.SmoothedVelocityY,
                0f,
                0f,
                resetReason);
            return entry;
        }

        private static CombatAimTargetMotionProfile BuildMotionProfile(
            CombatTargetSnapshot target,
            bool hasHistory,
            float measuredVelocityX,
            float measuredVelocityY,
            float smoothedVelocityX,
            float smoothedVelocityY,
            float accelerationX,
            float accelerationY,
            string resetReason)
        {
            var hardReset = IsHardMotionReset(resetReason);
            var velocityConfidence = hardReset ? 0.25f : ComputeVelocityConfidence(target, hasHistory, measuredVelocityX, measuredVelocityY);
            var accelerationMagnitude = Distance(accelerationX, accelerationY, 0f, 0f);
            var accelerationConfidence = !hasHistory || hardReset
                ? 0f
                : Clamp01(1f - accelerationMagnitude / MaxAccelerationPerTick);
            var kind = ResolveKind(target, hardReset, accelerationMagnitude, smoothedVelocityX, smoothedVelocityY);

            var profile = new CombatAimTargetMotionProfile
            {
                MotionProfileKind = kind,
                VelocityConfidence = velocityConfidence,
                AccelerationX = accelerationX,
                AccelerationY = accelerationY,
                AccelerationConfidence = accelerationConfidence,
                HistoryResetReason = resetReason ?? string.Empty
            };

            ApplyRecommendation(profile, kind, velocityConfidence);
            return profile;
        }

        private static string ResolveKind(
            CombatTargetSnapshot target,
            bool hardReset,
            float accelerationMagnitude,
            float smoothedVelocityX,
            float smoothedVelocityY)
        {
            if (hardReset)
            {
                return CombatAimTargetMotionProfile.TeleportOrDashRecent;
            }

            var width = Math.Max(1f, Math.Max(target.HitboxWidth, target.Width));
            var height = Math.Max(1f, Math.Max(target.HitboxHeight, target.Height));
            if (width >= 120f || height >= 120f || width * height >= 12000f)
            {
                return CombatAimTargetMotionProfile.LargeOrSegmented;
            }

            var speed = Distance(smoothedVelocityX, smoothedVelocityY, 0f, 0f);
            var verticalSpeed = Math.Abs(target.VelocityY);
            if (target.NpcAiStyle == 1)
            {
                return target.CollideY || verticalSpeed < 0.35f
                    ? CombatAimTargetMotionProfile.JumpingGrounded
                    : CombatAimTargetMotionProfile.JumpingAirborne;
            }

            if (!target.NoGravity && verticalSpeed >= 1.2f)
            {
                return CombatAimTargetMotionProfile.JumpingAirborne;
            }

            if (target.NoGravity && speed <= 2.5f && Math.Abs(smoothedVelocityY) <= 1.25f)
            {
                return CombatAimTargetMotionProfile.HoveringOscillating;
            }

            if (target.NoGravity || accelerationMagnitude >= 0.45f)
            {
                return CombatAimTargetMotionProfile.FlyingAccelerating;
            }

            return CombatAimTargetMotionProfile.StableLinear;
        }

        private static void ApplyRecommendation(CombatAimTargetMotionProfile profile, string kind, float velocityConfidence)
        {
            if (string.Equals(kind, CombatAimTargetMotionProfile.StableLinear, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.9f * velocityConfidence);
                profile.RecommendedLeadScale = 1f;
                profile.RecommendedMaxLeadTicks = 45f;
                profile.PreferSmoothedVelocity = true;
                return;
            }

            if (string.Equals(kind, CombatAimTargetMotionProfile.JumpingGrounded, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.5f * Math.Max(0.35f, velocityConfidence));
                profile.RecommendedLeadScale = 0.55f;
                profile.RecommendedMaxLeadTicks = 18f;
                profile.PreferCurrentVelocity = true;
                return;
            }

            if (string.Equals(kind, CombatAimTargetMotionProfile.JumpingAirborne, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.65f * Math.Max(0.45f, velocityConfidence));
                profile.RecommendedLeadScale = 0.65f;
                profile.RecommendedMaxLeadTicks = 24f;
                profile.PreferCurrentVelocity = true;
                return;
            }

            if (string.Equals(kind, CombatAimTargetMotionProfile.FlyingAccelerating, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.55f * Math.Max(0.45f, velocityConfidence));
                profile.RecommendedLeadScale = 0.5f;
                profile.RecommendedMaxLeadTicks = 20f;
                profile.PreferCurrentVelocity = true;
                return;
            }

            if (string.Equals(kind, CombatAimTargetMotionProfile.HoveringOscillating, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.55f * Math.Max(0.4f, velocityConfidence));
                profile.RecommendedLeadScale = 0.45f;
                profile.RecommendedMaxLeadTicks = 16f;
                profile.PreferSmoothedVelocity = true;
                return;
            }

            if (string.Equals(kind, CombatAimTargetMotionProfile.LargeOrSegmented, StringComparison.Ordinal))
            {
                profile.MotionConfidence = Clamp01(0.7f * Math.Max(0.5f, velocityConfidence));
                profile.RecommendedLeadScale = 0.85f;
                profile.RecommendedMaxLeadTicks = 36f;
                profile.PreferSmoothedVelocity = true;
                return;
            }

            profile.MotionConfidence = 0.2f;
            profile.VelocityConfidence = Math.Min(profile.VelocityConfidence, 0.35f);
            profile.RecommendedLeadScale = 0.2f;
            profile.RecommendedMaxLeadTicks = 8f;
            profile.PreferCurrentVelocity = true;
        }

        private static bool IsHardMotionReset(string resetReason)
        {
            return string.Equals(resetReason, "teleportDistance", StringComparison.Ordinal) ||
                   string.Equals(resetReason, "staleTickGap", StringComparison.Ordinal) ||
                   string.Equals(resetReason, "measuredVelocitySpike", StringComparison.Ordinal);
        }

        private static float ComputeVelocityConfidence(
            CombatTargetSnapshot target,
            bool hasHistory,
            float measuredVelocityX,
            float measuredVelocityY)
        {
            if (!hasHistory)
            {
                return 0.45f;
            }

            var diff = Distance(target.VelocityX, target.VelocityY, measuredVelocityX, measuredVelocityY);
            var measuredSpeed = Distance(measuredVelocityX, measuredVelocityY, 0f, 0f);
            var scale = Math.Max(1.5f, measuredSpeed * 0.5f + 1f);
            return Clamp01(1f - diff / scale);
        }

        private static void ClampAcceleration(ref float x, ref float y)
        {
            var magnitude = Distance(x, y, 0f, 0f);
            if (magnitude <= MaxAccelerationPerTick || magnitude <= 0.0001f)
            {
                return;
            }

            var scale = MaxAccelerationPerTick / magnitude;
            x *= scale;
            y *= scale;
        }

        private static float Distance(float ax, float ay, float bx, float by)
        {
            var dx = ax - bx;
            var dy = ay - by;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        private struct Entry
        {
            public int Type;
            public float CenterX;
            public float CenterY;
            public float LastVelocityX;
            public float LastVelocityY;
            public float SmoothedVelocityX;
            public float SmoothedVelocityY;
            public float AccelerationX;
            public float AccelerationY;
            public bool HasVelocity;
            public long LastSeenTick;
            public CombatAimTargetMotionProfile MotionProfile;
        }
    }
}
