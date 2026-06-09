using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Combat
{
    // Hitbox samples are bounded scoring evidence; they do not simulate projectile flight or mutate NPC state.
    internal static class CombatAimPredictedHitboxSampler
    {
        public const string SampleSpaceCurrent = "current";
        public const string SampleSpacePredicted = "predicted";

        private const int MaxSamplesPerSpace = 6;

        public static CombatAimHitboxSampleSet BuildSamples(
            CombatTargetSnapshot target,
            float rangeCenterX,
            float rangeCenterY,
            CombatAimBallisticSolution ballistic)
        {
            var result = new CombatAimHitboxSampleSet();
            if (target == null)
            {
                return result;
            }

            var current = CreateHitbox(target.HitboxX, target.HitboxY, target.HitboxWidth, target.HitboxHeight, target.CenterX, target.CenterY);
            result.ProjectileHitRadius = ResolveProjectileHitRadius(ballistic);
            AddSpaceSamples(result.Samples, SampleSpaceCurrent, current, rangeCenterX, rangeCenterY, result.ProjectileHitRadius);

            if (!CanUsePredictedSpace(ballistic, target))
            {
                result.PredictedHitboxCenterX = target.CenterX;
                result.PredictedHitboxCenterY = target.CenterY;
                return result;
            }

            var deltaX = ballistic.PredictedTargetX - target.CenterX;
            var deltaY = ballistic.PredictedTargetY - target.CenterY;
            var predicted = CreateHitbox(
                target.HitboxX + deltaX,
                target.HitboxY + deltaY,
                target.HitboxWidth,
                target.HitboxHeight,
                ballistic.PredictedTargetX,
                ballistic.PredictedTargetY);
            result.PredictedAvailable = true;
            result.PredictedHitboxCenterX = predicted.CenterX;
            result.PredictedHitboxCenterY = predicted.CenterY;
            AddSpaceSamples(result.Samples, SampleSpacePredicted, predicted, rangeCenterX, rangeCenterY, result.ProjectileHitRadius);
            return result;
        }

        private static bool CanUsePredictedSpace(CombatAimBallisticSolution ballistic, CombatTargetSnapshot target)
        {
            if (ballistic == null ||
                target == null ||
                !ballistic.Solved ||
                ballistic.ConservativeCenter ||
                ballistic.LeadTicks <= 0.01f)
            {
                return false;
            }

            return Distance(ballistic.PredictedTargetX, ballistic.PredictedTargetY, target.CenterX, target.CenterY) >= 0.5f;
        }

        private static void AddSpaceSamples(
            List<CombatAimHitboxSample> samples,
            string space,
            HitboxFrame frame,
            float rangeCenterX,
            float rangeCenterY,
            float projectileHitRadius)
        {
            if (samples == null)
            {
                return;
            }

            var before = samples.Count;
            AddSample(samples, space, "center", frame.CenterX, frame.CenterY, frame, projectileHitRadius);
            AddSample(samples, space, "topMid", frame.CenterX, frame.Top + frame.Height * 0.24f, frame, projectileHitRadius);
            AddSample(samples, space, "bottomMid", frame.CenterX, frame.Bottom - frame.Height * 0.24f, frame, projectileHitRadius);
            AddSample(samples, space, "leftMid", frame.Left + frame.Width * 0.24f, frame.CenterY, frame, projectileHitRadius);
            AddSample(samples, space, "rightMid", frame.Right - frame.Width * 0.24f, frame.CenterY, frame, projectileHitRadius);
            AddSample(samples, space, "nearestHitboxPoint", Clamp(rangeCenterX, frame.Left, frame.Right), Clamp(rangeCenterY, frame.Top, frame.Bottom), frame, projectileHitRadius);

            if (samples.Count - before > MaxSamplesPerSpace)
            {
                samples.RemoveRange(before + MaxSamplesPerSpace, samples.Count - before - MaxSamplesPerSpace);
            }
        }

        private static void AddSample(
            List<CombatAimHitboxSample> samples,
            string space,
            string name,
            float x,
            float y,
            HitboxFrame frame,
            float projectileHitRadius)
        {
            x = Clamp(x, frame.Left, frame.Right);
            y = Clamp(y, frame.Top, frame.Bottom);
            var centerDistance = Distance(x, y, frame.CenterX, frame.CenterY);
            var tolerance = Math.Max(1f, Math.Min(frame.Width, frame.Height) * 0.5f + Math.Max(0f, projectileHitRadius));
            var coverage = 1f - Clamp(centerDistance / tolerance, 0f, 1f);
            samples.Add(new CombatAimHitboxSample
            {
                Space = string.IsNullOrWhiteSpace(space) ? SampleSpaceCurrent : space,
                Name = name ?? string.Empty,
                X = x,
                Y = y,
                HitboxLeft = frame.Left,
                HitboxTop = frame.Top,
                HitboxWidth = frame.Width,
                HitboxHeight = frame.Height,
                HitboxCenterX = frame.CenterX,
                HitboxCenterY = frame.CenterY,
                CenterDistanceRatio = Clamp(centerDistance / Math.Max(1f, Math.Min(frame.Width, frame.Height) * 0.5f), 0f, 2f),
                CoverageScore = coverage
            });
        }

        private static HitboxFrame CreateHitbox(float left, float top, float width, float height, float centerX, float centerY)
        {
            width = Math.Max(1f, width);
            height = Math.Max(1f, height);
            return new HitboxFrame
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                CenterX = Clamp(centerX, left, left + width),
                CenterY = Clamp(centerY, top, top + height)
            };
        }

        private static float ResolveProjectileHitRadius(CombatAimBallisticSolution ballistic)
        {
            if (ballistic == null)
            {
                return 0f;
            }

            if (ballistic.ProjectileRadiusForHit > 0f)
            {
                return ballistic.ProjectileRadiusForHit;
            }

            var width = ballistic.ProjectileWidth <= 0 ? 0f : ballistic.ProjectileWidth * 0.5f;
            var height = ballistic.ProjectileHeight <= 0 ? 0f : ballistic.ProjectileHeight * 0.5f;
            return Math.Max(width, height);
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

        private struct HitboxFrame
        {
            public float Left;
            public float Top;
            public float Width;
            public float Height;
            public float CenterX;
            public float CenterY;

            public float Right
            {
                get { return Left + Width; }
            }

            public float Bottom
            {
                get { return Top + Height; }
            }
        }
    }

    internal sealed class CombatAimHitboxSampleSet
    {
        public readonly List<CombatAimHitboxSample> Samples = new List<CombatAimHitboxSample>(12);
        public bool PredictedAvailable;
        public float PredictedHitboxCenterX;
        public float PredictedHitboxCenterY;
        public float ProjectileHitRadius;
    }

    internal sealed class CombatAimHitboxSample
    {
        public string Space = CombatAimPredictedHitboxSampler.SampleSpaceCurrent;
        public string Name = string.Empty;
        public float X;
        public float Y;
        public float HitboxLeft;
        public float HitboxTop;
        public float HitboxWidth;
        public float HitboxHeight;
        public float HitboxCenterX;
        public float HitboxCenterY;
        public float CenterDistanceRatio;
        public float CoverageScore;
    }
}
