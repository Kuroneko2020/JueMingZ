using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Automation.Combat
{
    public static class CombatAimLineOfSight
    {
        private static readonly object SyncRoot = new object();
        private static long _cacheGameUpdateCount = long.MinValue;
        private static readonly Dictionary<LineKey, bool> FrameCache = new Dictionary<LineKey, bool>();

        public static bool LastCacheHit { get; private set; }

        public static bool TryCanHitLine(float fromX, float fromY, float toX, float toY, out bool lineClear)
        {
            LastCacheHit = false;
            lineClear = false;
            if (!IsLateBootstrapCompleted())
            {
                return false;
            }

            long gameUpdateCount = 0;
            try
            {
                gameUpdateCount = TerrariaMainCompat.GameUpdateCount;
            }
            catch
            {
                gameUpdateCount = Environment.TickCount;
            }

            var key = BuildCacheKey(fromX, fromY, toX, toY);
            lock (SyncRoot)
            {
                if (_cacheGameUpdateCount != gameUpdateCount)
                {
                    _cacheGameUpdateCount = gameUpdateCount;
                    FrameCache.Clear();
                }

                bool cached;
                if (FrameCache.TryGetValue(key, out cached))
                {
                    lineClear = cached;
                    LastCacheHit = true;
                    return true;
                }
            }

            try
            {
                lineClear = Collision.CanHitLine(new Vector2(fromX, fromY), 1, 1, new Vector2(toX, toY), 1, 1);
                lock (SyncRoot)
                {
                    FrameCache[key] = lineClear;
                }

                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-line-of-sight-failed",
                    TimeSpan.FromSeconds(30),
                    "CombatAimLineOfSight",
                    "Collision line-of-sight check failed: " + error.Message);
                return false;
            }
        }

        private static LineKey BuildCacheKey(float fromX, float fromY, float toX, float toY)
        {
            return new LineKey(Quantize(fromX), Quantize(fromY), Quantize(toX), Quantize(toY));
        }

        private static int Quantize(float value)
        {
            return (int)Math.Round(value / 16f);
        }

        private static bool IsLateBootstrapCompleted()
        {
            try
            {
                return JueMingZRuntime.State != null && JueMingZRuntime.State.LateBootstrapCompleted;
            }
            catch
            {
                return false;
            }
        }

        private struct LineKey : IEquatable<LineKey>
        {
            private readonly int _fromX;
            private readonly int _fromY;
            private readonly int _toX;
            private readonly int _toY;

            public LineKey(int fromX, int fromY, int toX, int toY)
            {
                _fromX = fromX;
                _fromY = fromY;
                _toX = toX;
                _toY = toY;
            }

            public bool Equals(LineKey other)
            {
                return _fromX == other._fromX &&
                       _fromY == other._fromY &&
                       _toX == other._toX &&
                       _toY == other._toY;
            }

            public override bool Equals(object obj)
            {
                return obj is LineKey && Equals((LineKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = _fromX;
                    hash = (hash * 397) ^ _fromY;
                    hash = (hash * 397) ^ _toX;
                    hash = (hash * 397) ^ _toY;
                    return hash;
                }
            }
        }
    }
}
