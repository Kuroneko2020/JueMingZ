using System;
using System.Collections;
using System.Threading;
using JueMingZ.Automation.Fishing;

namespace JueMingZ.Automation.Information
{
    internal static class InformationBobberLocator
    {
        private const ulong ObserverFreshTicks = 2;

        private static long _observerFreshInactiveSkipCount;
        private static long _projectileFallbackScanCount;

        internal static long ObserverFreshInactiveSkipCount
        {
            get { return Interlocked.Read(ref _observerFreshInactiveSkipCount); }
        }

        internal static long ProjectileFallbackScanCount
        {
            get { return Interlocked.Read(ref _projectileFallbackScanCount); }
        }

        internal static bool TryFindLocalBobber(InformationWorldContext context, out float x, out float y)
        {
            int identity;
            return TryFindLocalBobber(context, out x, out y, out identity);
        }

        internal static bool TryFindLocalBobber(InformationWorldContext context, out float x, out float y, out int identity)
        {
            x = 0f;
            y = 0f;
            identity = -1;
            if (context == null || context.MainType == null)
            {
                return false;
            }

            int myPlayer;
            InformationReflection.TryReadStaticInt(context.MainType, "myPlayer", out myPlayer);
            FishingBobberObservation observation;
            if (FishingBobberObserver.TryGetLatest(out observation) &&
                TryUseObservedLocalBobber(observation, myPlayer, context.GameUpdateCount, out x, out y, out identity))
            {
                return true;
            }

            if (ShouldSkipProjectileFallbackForFreshInactiveObserver(context.GameUpdateCount))
            {
                Interlocked.Increment(ref _observerFreshInactiveSkipCount);
                return false;
            }

            Interlocked.Increment(ref _projectileFallbackScanCount);
            var projectiles = InformationReflection.GetStaticMember(context.MainType, "projectile");
            if (projectiles == null)
            {
                return false;
            }

            var count = GetCollectionCount(projectiles);
            for (var index = 0; index < count; index++)
            {
                var projectile = InformationReflection.GetIndexedValue(projectiles, index);
                if (projectile == null)
                {
                    continue;
                }

                bool active;
                bool bobber;
                int owner;
                InformationReflection.TryReadBool(projectile, "active", out active);
                InformationReflection.TryReadBool(projectile, "bobber", out bobber);
                InformationReflection.TryReadInt(projectile, "owner", out owner);
                if (!active || !bobber || owner != myPlayer)
                {
                    continue;
                }

                if (InformationReflection.TryReadVectorMember(projectile, "Center", out x, out y))
                {
                    InformationReflection.TryReadInt(projectile, "identity", out identity);
                    return true;
                }

                if (InformationReflection.TryReadVectorMember(projectile, "position", out x, out y))
                {
                    InformationReflection.TryReadInt(projectile, "identity", out identity);
                    return true;
                }
            }

            MarkNoActiveFishingBobberObservation(context.GameUpdateCount);
            return false;
        }

        internal static bool TryUseObservedLocalBobberForTesting(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y)
        {
            int identity;
            return TryUseObservedLocalBobber(observation, myPlayer, currentGameUpdateCount, out x, out y, out identity);
        }

        internal static void ResetDiagnosticsForTesting()
        {
            Interlocked.Exchange(ref _observerFreshInactiveSkipCount, 0);
            Interlocked.Exchange(ref _projectileFallbackScanCount, 0);
        }

        private static bool TryUseObservedLocalBobber(FishingBobberObservation observation, int myPlayer, ulong currentGameUpdateCount, out float x, out float y, out int identity)
        {
            x = 0f;
            y = 0f;
            identity = -1;
            if (observation == null ||
                !observation.Active ||
                !observation.Bobber ||
                observation.Owner != myPlayer ||
                !observation.LiquidStateKnown ||
                !observation.InLiquid ||
                !IsFreshObservedBobber(observation, currentGameUpdateCount) ||
                !IsFinite(observation.CenterX) ||
                !IsFinite(observation.CenterY))
            {
                return false;
            }

            x = observation.CenterX;
            y = observation.CenterY;
            identity = observation.Identity;
            return true;
        }

        private static bool ShouldSkipProjectileFallbackForFreshInactiveObserver(ulong currentGameUpdateCount)
        {
            if (currentGameUpdateCount == 0 || currentGameUpdateCount > long.MaxValue)
            {
                return false;
            }

            return FishingBobberObserver.HasFreshNoActiveObservation(
                (long)currentGameUpdateCount,
                (int)ObserverFreshTicks);
        }

        private static void MarkNoActiveFishingBobberObservation(ulong currentGameUpdateCount)
        {
            if (currentGameUpdateCount == 0 || currentGameUpdateCount > long.MaxValue)
            {
                return;
            }

            FishingBobberObserver.MarkNoActiveObservation((long)currentGameUpdateCount);
        }

        private static bool IsFreshObservedBobber(FishingBobberObservation observation, ulong currentGameUpdateCount)
        {
            if (observation == null || observation.GameUpdateCount < 0 || currentGameUpdateCount > long.MaxValue)
            {
                return false;
            }

            var age = (long)currentGameUpdateCount - observation.GameUpdateCount;
            return age >= 0 && age <= (long)ObserverFreshTicks;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static int GetCollectionCount(object source)
        {
            var collection = source as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            var array = source as Array;
            return array == null ? 0 : array.Length;
        }
    }
}
