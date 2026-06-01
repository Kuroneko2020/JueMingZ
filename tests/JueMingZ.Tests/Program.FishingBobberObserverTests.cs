using System;
using System.Collections.Generic;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void FishingBobberObserverSelectsLatestObservation()
        {
            FishingBobberObserver.RemoveMissing(null);

            FishingBobberObserver.Observe(BobberObservation(10, 3, 2));
            FishingBobberObserver.Observe(BobberObservation(20, 4, 1));

            FishingBobberObservation latest;
            if (!FishingBobberObserver.TryGetLatest(out latest) || latest == null || latest.Identity != 20)
            {
                throw new InvalidOperationException("Expected newest bobber identity 20.");
            }

            latest.Active = false;
            FishingBobberObservation byIdentity;
            if (!FishingBobberObserver.TryGetByIdentity(20, out byIdentity) || byIdentity == null || !byIdentity.Active)
            {
                throw new InvalidOperationException("Expected public bobber observations to be cloned.");
            }
        }

        private static void FishingBobberObserverTieBreaksByWhoAmI()
        {
            FishingBobberObserver.RemoveMissing(null);

            FishingBobberObserver.Observe(BobberObservation(10, 7, 3));
            FishingBobberObserver.Observe(BobberObservation(20, 7, 9));
            FishingBobberObserver.Observe(BobberObservation(30, 6, 99));

            FishingBobberObservation latest;
            if (!FishingBobberObserver.TryGetLatest(out latest) || latest == null || latest.Identity != 20)
            {
                throw new InvalidOperationException("Expected same-tick tie-break to choose larger WhoAmI.");
            }
        }

        private static void FishingBobberObserverRemoveMissingRebuildsLatest()
        {
            FishingBobberObserver.RemoveMissing(null);

            var older = BobberObservation(10, 5, 2);
            var newer = BobberObservation(20, 6, 3);
            FishingBobberObserver.Observe(older);
            FishingBobberObserver.Observe(newer);

            FishingBobberObserver.RemoveMissing(new List<FishingBobberObservation> { older });

            FishingBobberObservation latest;
            if (!FishingBobberObserver.TryGetLatest(out latest) || latest == null || latest.Identity != 10)
            {
                throw new InvalidOperationException("Expected latest pointer to rebuild after removing newest bobber.");
            }

            FishingBobberObservation removed;
            if (FishingBobberObserver.TryGetByIdentity(20, out removed))
            {
                throw new InvalidOperationException("Expected missing bobber identity 20 to be removed.");
            }
        }

        private static void FishingBobberObserverEmptyScanClearsObservations()
        {
            FishingBobberObserver.RemoveMissing(null);

            FishingBobberObserver.Observe(BobberObservation(10, 5, 2));
            if (!FishingBobberObserver.HasActiveObservation)
            {
                throw new InvalidOperationException("Expected observer to report an active bobber after Observe.");
            }

            FishingBobberObserver.RemoveMissing(new List<FishingBobberObservation>());

            FishingBobberObservation latest;
            if (FishingBobberObserver.TryGetLatest(out latest))
            {
                throw new InvalidOperationException("Expected empty fallback scan to clear latest bobber.");
            }

            FishingBobberObservation byIdentity;
            if (FishingBobberObserver.TryGetByIdentity(10, out byIdentity))
            {
                throw new InvalidOperationException("Expected empty fallback scan to clear identity lookup.");
            }

            if (FishingBobberObserver.HasActiveObservation)
            {
                throw new InvalidOperationException("Expected empty fallback scan to clear active observation state.");
            }
        }

        private static void InformationFishingBobberUsesFreshObserver()
        {
            var observation = BobberObservation(10, 100, 2);
            observation.Owner = 3;
            observation.CenterX = 240f;
            observation.CenterY = 384f;

            float x;
            float y;
            if (!InformationOverlayService.TryUseObservedLocalBobberForTesting(observation, 3, 102, out x, out y) ||
                Math.Abs(x - 240f) > 0.01f ||
                Math.Abs(y - 384f) > 0.01f)
            {
                throw new InvalidOperationException("Expected information overlay to reuse a fresh local liquid bobber observation.");
            }

            if (InformationOverlayService.TryUseObservedLocalBobberForTesting(observation, 3, 103, out x, out y))
            {
                throw new InvalidOperationException("Expected stale observer bobbers to fall back to projectile scanning.");
            }

            if (InformationOverlayService.TryUseObservedLocalBobberForTesting(observation, 4, 102, out x, out y))
            {
                throw new InvalidOperationException("Expected non-local observer bobbers to fall back to projectile scanning.");
            }

            observation.InLiquid = false;
            if (InformationOverlayService.TryUseObservedLocalBobberForTesting(observation, 3, 102, out x, out y))
            {
                throw new InvalidOperationException("Expected non-liquid observer bobbers to fall back to projectile scanning.");
            }
        }

        private static void FishingFallbackScanGateSkipsFreshHookObservations()
        {
            if (!FishingAutomationService.ShouldSkipFallbackScanForTesting(
                    hookInstalled: true,
                    observerHasActiveObservation: true,
                    hookLastObservationTick: 100,
                    currentGameUpdateCount: 102,
                    forceBobberTransitionScan: false))
            {
                throw new InvalidOperationException("Expected fresh hook observation to skip fallback scan.");
            }

            if (FishingAutomationService.ShouldSkipFallbackScanForTesting(
                    hookInstalled: true,
                    observerHasActiveObservation: true,
                    hookLastObservationTick: 100,
                    currentGameUpdateCount: 103,
                    forceBobberTransitionScan: false))
            {
                throw new InvalidOperationException("Expected stale hook observation to keep fallback scan.");
            }
        }

        private static void FishingFallbackScanGateKeepsOldFallbackForSensitiveStages()
        {
            if (FishingAutomationService.ShouldSkipFallbackScanForTesting(
                    hookInstalled: false,
                    observerHasActiveObservation: true,
                    hookLastObservationTick: 100,
                    currentGameUpdateCount: 100,
                    forceBobberTransitionScan: false))
            {
                throw new InvalidOperationException("Expected missing hook installation to keep fallback scan.");
            }

            if (FishingAutomationService.ShouldSkipFallbackScanForTesting(
                    hookInstalled: true,
                    observerHasActiveObservation: false,
                    hookLastObservationTick: 100,
                    currentGameUpdateCount: 100,
                    forceBobberTransitionScan: false))
            {
                throw new InvalidOperationException("Expected missing observer state to keep fallback scan.");
            }

            if (FishingAutomationService.ShouldSkipFallbackScanForTesting(
                    hookInstalled: true,
                    observerHasActiveObservation: true,
                    hookLastObservationTick: 100,
                    currentGameUpdateCount: 100,
                    forceBobberTransitionScan: true))
            {
                throw new InvalidOperationException("Expected bobber transition confirmation to force fallback scan.");
            }
        }

        private static FishingBobberObservation BobberObservation(int identity, long gameUpdateCount, int whoAmI)
        {
            return new FishingBobberObservation
            {
                Identity = identity,
                GameUpdateCount = gameUpdateCount,
                WhoAmI = whoAmI,
                Type = TestFishingBobber,
                Owner = 0,
                Active = true,
                Bobber = true,
                InLiquid = true,
                LiquidStateKnown = true,
                LiquidKind = FishingLiquidKind.Water,
                CenterX = identity * 16f,
                CenterY = 128f
            };
        }
    }
}
