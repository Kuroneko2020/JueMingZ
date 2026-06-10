using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void InformationFishingCatchQueryKeyTracksEnvironment()
        {
            var player = new TestInformationFishingEnvironmentPlayer
            {
                luck = 0.25d,
                fishingSkill = 12,
                accLavaFishing = true,
                ZoneJungle = true
            };
            var context = new InformationWorldContext
            {
                LocalPlayer = player,
                WorldKey = "world-a#42",
                PlayerCenterY = 1200f
            };

            var signature = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");

            var same = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (!string.Equals(signature, same, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected identical fishing catch query inputs to reuse the same cache signature.");
            }

            var movedTile = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                81,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (string.Equals(signature, movedTile, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected bobber tile changes to dirty the fishing catch cache key.");
            }

            var changedFilter = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-b");
            if (string.Equals(signature, changedFilter, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing filter configuration changes to dirty the fishing catch cache key.");
            }

            player.ZoneJungle = false;
            var changedZone = InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                "water",
                320,
                45,
                TestFishingRod,
                267,
                2454,
                "filter-a");
            if (string.Equals(signature, changedZone, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected player biome changes to dirty the fishing catch cache key.");
            }
        }

        private static void InformationFishingCatchQueryKeyTracksFullBaselineFields()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            TestInformationFishingQueryMain.Reset();
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var player = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true
                };
                var context = new InformationWorldContext
                {
                    LocalPlayer = player,
                    MainType = typeof(TestInformationFishingQueryMain),
                    WorldKey = "query-field-world",
                    PlayerCenterY = 1200f
                };

                var signature = BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "lava", 320, 1, 300, false, 45, 45, false, "filter-a"), "liquid");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 321, 1, 300, false, 45, 45, false, "filter-a"), "water tiles");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 2, 300, false, 45, 45, false, "filter-a"), "chums");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 240, false, 45, 45, false, "filter-a"), "water needed");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, true, 45, 45, false, "filter-a"), "junk");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 44, false, "filter-a"), "effective fishing level");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, true, "filter-a"), "lava capability");

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a"), "language");

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                TestInformationFishingQueryMain.hardMode = true;
                AssertFishingCatchSignatureChanged(signature, BuildDetailedFishingCatchQuerySignature(context, "water", 320, 1, 300, false, 45, 45, false, "filter-a"), "world state");
            }
            finally
            {
                TestInformationFishingQueryMain.Reset();
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingCatchEarlyKeyTracksEnvironment()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                var player = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true,
                    buffType = new[] { 111, 0, 0 },
                    buffTime = new[] { 30, 0, 0 }
                };
                var context = new InformationWorldContext
                {
                    LocalPlayer = player,
                    WorldKey = "world-a#42",
                    PlayerCenterY = 1200f
                };

                var signature = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);

                var same = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (!string.Equals(signature, same, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected identical early fishing catch inputs to reuse the same cache signature.");
                }

                var changedWorldContext = new InformationWorldContext
                {
                    LocalPlayer = player,
                    WorldKey = "world-b#99",
                    PlayerCenterY = 1200f
                };
                var changedWorld = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    changedWorldContext,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedWorld, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected world changes to dirty the early fishing catch cache key.");
                }

                var otherPlayer = new TestInformationFishingEnvironmentPlayer
                {
                    luck = 0.25d,
                    fishingSkill = 12,
                    accLavaFishing = true,
                    ZoneJungle = true,
                    buffType = new[] { 111, 0, 0 },
                    buffTime = new[] { 30, 0, 0 }
                };
                var changedPlayerContext = new InformationWorldContext
                {
                    LocalPlayer = otherPlayer,
                    WorldKey = "world-a#42",
                    PlayerCenterY = 1200f
                };
                var changedPlayer = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    changedPlayerContext,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedPlayer, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected player identity changes to dirty the early fishing catch cache key.");
                }

                var movedBobber = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    81,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, movedBobber, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected bobber tile changes to dirty the early fishing catch cache key.");
                }

                var changedQuest = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2455);
                if (string.Equals(signature, changedQuest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected angler quest fish changes to dirty the early fishing catch cache key.");
                }

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("zh-CN");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                var changedLanguage = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedLanguage, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected language changes to dirty the early fishing catch cache key.");
                }

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
                player.buffType[1] = 222;
                player.buffTime[1] = 60;
                var changedBuff = InformationFishingCatchResolver.BuildEarlyCatchQuerySignatureForTesting(
                    context,
                    80,
                    120,
                    77,
                    20,
                    TestFishingRod,
                    15,
                    267,
                    2454);
                if (string.Equals(signature, changedBuff, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected active player buff changes to dirty the early fishing catch cache key.");
                }

                var settings = AppSettings.CreateDefault();
                settings.InformationFishingFilteredCatchesEnabled = true;
                settings.FishingFilterMode = FishingFilterModes.AllowList;
                var filterSignature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                settings.FishingFilterMode = FishingFilterModes.DenyList;
                var changedFilterSignature = InformationOverlayService.BuildStatusLineCacheSignatureForTesting(context, settings);
                if (string.Equals(filterSignature, changedFilterSignature, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected fishing filter changes to dirty status-line output without being part of the early environment cache.");
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }

        private static void InformationFishingCatchEarlyCacheHitSkipsHeavyCounters()
        {
            // This is a performance regression guard: cache hits must not read
            // water or FishingConditions just to refresh display diagnostics.
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            var candidates = new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 1,
                    DisplayName = "Cached Fish",
                    DisplayNameSnapshot = "Cached Fish"
                }
            };

            InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting("early:test", candidates, "cached");
            IList<FishingCatchCandidate> cached;
            string message;
            if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:test", out cached, out message) ||
                cached.Count != 1 ||
                !string.Equals(message, "cached", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected early fishing catch cache to return stored candidates and message.");
            }

            if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                InformationFishingCatchResolver.EarlyCacheMissCount != 0)
            {
                throw new InvalidOperationException("Expected early fishing catch cache hit diagnostics to increment once.");
            }

            if (InformationFishingCatchResolver.WaterScanCount != 0 ||
                InformationFishingCatchResolver.ConditionsReadCount != 0)
            {
                throw new InvalidOperationException("Expected early fishing catch cache hit to bypass water scan and fishing conditions read counters.");
            }
        }

        private static void InformationFishingCatchCachesKeepConfiguredLimits()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var earlyLimit = InformationFishingCatchResolver.EarlyCatchCacheLimitForTesting;
                for (var index = 0; index <= earlyLimit; index++)
                {
                    InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting(
                        "early:" + index.ToString(CultureInfo.InvariantCulture),
                        CreateSingleFishingCatchCandidate(index + 1, "Early Fish " + index.ToString(CultureInfo.InvariantCulture)),
                        "early message " + index.ToString(CultureInfo.InvariantCulture));
                }

                IList<FishingCatchCandidate> candidates;
                string message;
                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:0", out candidates, out message))
                {
                    throw new InvalidOperationException("Expected early fishing catch cache to evict the oldest entry after exceeding its limit.");
                }

                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:" + earlyLimit.ToString(CultureInfo.InvariantCulture), out candidates, out message) ||
                    candidates.Count != 1 ||
                    candidates[0].Id != earlyLimit + 1 ||
                    !string.Equals(message, "early message " + earlyLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected newest early fishing catch cache entry to survive limit eviction.");
                }

                var queryLimit = InformationFishingCatchResolver.CatchCacheLimitForTesting;
                for (var index = 0; index <= queryLimit; index++)
                {
                    InformationFishingCatchResolver.StoreCatchCacheForTesting(
                        "query:" + index.ToString(CultureInfo.InvariantCulture),
                        CreateSingleFishingCatchCandidate(index + 100, "Query Fish " + index.ToString(CultureInfo.InvariantCulture)),
                        "query message " + index.ToString(CultureInfo.InvariantCulture));
                }

                if (InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:0", out candidates, out message))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache to evict the oldest entry after exceeding its limit.");
                }

                if (!InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:" + queryLimit.ToString(CultureInfo.InvariantCulture), out candidates, out message) ||
                    candidates.Count != 1 ||
                    candidates[0].Id != queryLimit + 100 ||
                    !string.Equals(message, "query message " + queryLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected newest full fishing catch query cache entry to survive limit eviction.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingCatchQueryCacheHitBackfillsEarlyCache()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var candidates = CreateSingleFishingCatchCandidate(501, "Backfilled Fish");
                InformationFishingCatchResolver.StoreCatchCacheForTesting("query:backfill", candidates, "from query cache");

                IList<FishingCatchCandidate> queryCandidates;
                string message;
                if (!InformationFishingCatchResolver.TryGetCatchCacheAndBackfillEarlyForTesting(
                    "query:backfill",
                    "early:backfill",
                    out queryCandidates,
                    out message) ||
                    !object.ReferenceEquals(candidates, queryCandidates) ||
                    !string.Equals(message, "from query cache", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache hit to return the stored entry before backfilling early cache.");
                }

                IList<FishingCatchCandidate> earlyCandidates;
                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:backfill", out earlyCandidates, out message) ||
                    !object.ReferenceEquals(candidates, earlyCandidates) ||
                    !string.Equals(message, "from query cache", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected full fishing catch query cache hit to backfill early fishing catch cache with the same entry semantics.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0)
                {
                    throw new InvalidOperationException("Expected query-cache backfill verification to keep early cache diagnostics to one hit and no miss.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingCatchResetClearsCachesAndCounters()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                var candidates = CreateSingleFishingCatchCandidate(601, "Reset Fish");
                InformationFishingCatchResolver.StoreEarlyCatchCacheForTesting("early:reset", candidates, "early reset");
                InformationFishingCatchResolver.StoreCatchCacheForTesting("query:reset", candidates, "query reset");

                IList<FishingCatchCandidate> cached;
                string message;
                if (!InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected early fishing catch cache setup to hit before reset.");
                }

                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:missing", out cached, out message))
                {
                    throw new InvalidOperationException("Expected missing early fishing catch cache setup probe to miss before reset.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 1 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 1)
                {
                    throw new InvalidOperationException("Expected early fishing catch cache diagnostics setup to record one hit and one miss before reset.");
                }

                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                if (InformationFishingCatchResolver.EarlyCacheHitCount != 0 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0 ||
                    InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear all cache diagnostics counters.");
                }

                if (InformationFishingCatchResolver.TryGetCatchCacheForTesting("query:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear full query cache entries.");
                }

                if (InformationFishingCatchResolver.TryGetEarlyCatchCacheForTesting("early:reset", out cached, out message))
                {
                    throw new InvalidOperationException("Expected fishing catch cache reset to clear early cache entries.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingWaterPenaltyKeepsSourceFormula()
        {
            TestInformationFishingQueryMain.Reset();
            var player = new TestInformationFishingEnvironmentPlayer();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };

            var enough = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 300, 0, 50);
            if (enough.WaterNeeded != 300 ||
                enough.FishingLevel != 50 ||
                enough.JunkPossible)
            {
                throw new InvalidOperationException("Expected sufficient fishing water to keep waterNeeded 300, fishing level, and junk flag stable.");
            }

            var shortPositiveLuck = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 250, 0, 90);
            if (shortPositiveLuck.WaterNeeded != 300 ||
                shortPositiveLuck.FishingLevel != 75 ||
                shortPositiveLuck.JunkPossible)
            {
                throw new InvalidOperationException("Expected water shortage to scale fishing level without enabling junk when luckLevel stays high.");
            }

            player.luck = -0.5d;
            var shortNegativeLuck = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 250, 0, 90);
            if (shortNegativeLuck.WaterNeeded != 300 ||
                shortNegativeLuck.FishingLevel != 75 ||
                !shortNegativeLuck.JunkPossible)
            {
                throw new InvalidOperationException("Expected negative luck to use luckLevel for the water-shortage junk check.");
            }

            player.luck = 0d;
            var chummed = InformationFishingCatchResolver.ApplyFishingWaterPenaltyForTesting(context, 5120f, 300, 3, 50);
            if (chummed.WaterNeeded != 300 ||
                chummed.FishingLevel != 70 ||
                chummed.JunkPossible)
            {
                throw new InvalidOperationException("Expected chum bonuses to keep the existing +11/+6/+3 fishing level behavior.");
            }
        }

        private static void InformationFishingLiquidKindKeepsPriority()
        {
            if (!string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(false, false), "water", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(false, true), "honey", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(true, false), "lava", StringComparison.Ordinal) ||
                !string.Equals(InformationFishingCatchResolver.ResolveLiquidKindForTesting(true, true), "lava", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing liquid kind priority to remain lava, then honey, then water.");
            }
        }

        private static void InformationFishingLavaCapabilityReadsEnvironment()
        {
            var player = new TestInformationFishingEnvironmentPlayer();
            var context = new InformationWorldContext
            {
                LocalPlayer = player
            };
            var conditions = new TestInformationFishingConditions();

            if (InformationFishingCatchResolver.CanFishInLavaForTesting(context, conditions))
            {
                throw new InvalidOperationException("Expected lava fishing capability to stay false without player accessory, lava pole, or lava bait.");
            }

            player.accLavaFishing = true;
            if (!InformationFishingCatchResolver.CanFishInLavaForTesting(context, conditions))
            {
                throw new InvalidOperationException("Expected lava fishing capability to read the player's accLavaFishing flag.");
            }
        }

        private static void InformationFishingWaterScanIncrementsOncePerScan()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                TestInformationFishingQueryMain.Reset();
                var context = new InformationWorldContext
                {
                    MainType = typeof(TestInformationFishingQueryMain)
                };

                var water = InformationFishingCatchResolver.ScanFishingWaterForTesting(context, 80, 120);
                if (water.TotalTiles != 0 ||
                    water.InLava ||
                    water.InHoney)
                {
                    throw new InvalidOperationException("Expected missing tile collection to return an empty fishing water scan.");
                }

                if (InformationFishingCatchResolver.WaterScanCount != 1 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected a real water scan entry to increment WaterScanCount once without reading fishing conditions.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                TestInformationFishingQueryMain.Reset();
            }
        }

        private static void InformationFishingConditionHeightRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain)
            };
            var levels = new int[2];

            AssertHeightLevels(context, 100, levels, new[] { 0 }, "non-remix sky");
            AssertHeightLevels(context, 200, levels, new[] { 1 }, "non-remix surface");
            AssertHeightLevels(context, 400, levels, new[] { 2 }, "non-remix underground");
            AssertHeightLevels(context, 700, levels, new[] { 3 }, "non-remix cavern");
            AssertHeightLevels(context, 950, levels, new[] { 4 }, "non-remix underworld");

            TestInformationFishingQueryMain.remixWorld = true;
            AssertHeightLevels(context, 100, levels, new[] { 0 }, "remix sky");
            AssertHeightLevels(context, 200, levels, new[] { 1 }, "remix surface");
            AssertHeightLevels(context, 400, levels, new[] { 3 }, "remix underground");
            AssertHeightLevels(context, 700, levels, new[] { 2, 1 }, "remix cavern adds surface");
            AssertHeightLevels(context, 950, levels, new[] { 4 }, "remix underworld");
        }

        private static void InformationFishingConditionCorruptionRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            var player = new TestInformationFishingEnvironmentPlayer
            {
                ZoneCorrupt = true,
                ZoneCrimson = true
            };
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };
            var rolls = new CorruptionRoll[2];
            var count = InformationFishingConditionRolls.BuildCorruptionRolls(context, 2, rolls);
            if (count != 2 ||
                !rolls[0].Corrupt ||
                rolls[0].Crimson ||
                rolls[1].Corrupt ||
                !rolls[1].Crimson)
            {
                throw new InvalidOperationException("Expected dual biome corruption rolls to stay corrupt-first then crimson.");
            }

            TestInformationFishingQueryMain.remixWorld = true;
            count = InformationFishingConditionRolls.BuildCorruptionRolls(context, 0, rolls);
            if (count != 1 || rolls[0].Corrupt || rolls[0].Crimson)
            {
                throw new InvalidOperationException("Expected remix sky fishing roll to disable corruption/crimson flags.");
            }
        }

        private static void InformationFishingConditionBooleanRollsKeepOrder()
        {
            TestInformationFishingQueryMain.Reset();
            TestInformationFishingQueryMain.notTheBeesWorld = true;
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain)
            };

            AssertBoolRolls(InformationFishingConditionRolls.BuildHoneyRolls(context, true), new[] { true, false }, "notTheBees honey");
            AssertBoolRolls(InformationFishingConditionRolls.BuildHoneyRolls(context, false), new[] { false }, "dry honey");
            AssertBoolRolls(InformationFishingConditionRolls.BuildBooleanRolls(true), new[] { false, true }, "enabled boolean");
            AssertBoolRolls(InformationFishingConditionRolls.BuildBooleanRolls(false), new[] { false }, "disabled boolean");
        }

        private static void InformationFishingConditionEnumerationKeepsOrderAndStops()
        {
            TestInformationFishingQueryMain.Reset();
            TestInformationFishingQueryMain.notTheBeesWorld = true;
            var player = new TestInformationFishingEnvironmentPlayer
            {
                ZoneCorrupt = true,
                ZoneCrimson = true
            };
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = player
            };
            var summaries = new List<string>();
            InformationFishingConditionRolls.ForEachRoll(
                context,
                400,
                true,
                true,
                true,
                true,
                true,
                true,
                delegate(FishingConditionRoll roll)
                {
                    summaries.Add(SummarizeFishingConditionRoll(roll));
                    return summaries.Count < 5;
                });

            if (summaries.Count != 5)
            {
                throw new InvalidOperationException("Expected condition roll callback stop to prevent further enumeration.");
            }

            AssertStringEquals(summaries[0], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=0|junk=0", "roll 0");
            AssertStringEquals(summaries[1], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=0|junk=1", "roll 1");
            AssertStringEquals(summaries[2], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=1|junk=0", "roll 2");
            AssertStringEquals(summaries[3], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=0|crate=1|junk=1", "roll 3");
            AssertStringEquals(summaries[4], "h2|corrupt=1|crimson=0|honey=1|jungle=1|snow=0|desert=0|infected=1|crate=0|junk=0", "roll 4");
        }

        private static void InformationFishingConditionJunkDisabledStaysFalse()
        {
            TestInformationFishingQueryMain.Reset();
            var context = new InformationWorldContext
            {
                MainType = typeof(TestInformationFishingQueryMain),
                LocalPlayer = new TestInformationFishingEnvironmentPlayer()
            };
            var count = 0;
            InformationFishingConditionRolls.ForEachRoll(
                context,
                400,
                false,
                false,
                false,
                false,
                false,
                false,
                delegate(FishingConditionRoll roll)
                {
                    count++;
                    if (roll.Junk)
                    {
                        throw new InvalidOperationException("Expected junk=false roll space not to include junk=true.");
                    }

                    return true;
                });

            if (count != 2)
            {
                throw new InvalidOperationException("Expected junk-disabled baseline to enumerate only crate false/true, got " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void InformationFishingContextFactoryMapsAttemptSpec()
        {
            InformationFishingContextFactory.ResetTypeCacheForTesting();
            var player = new TestInformationFishingEnvironmentPlayer();
            var fishingConditions = new TestInformationFishingConditions
            {
                PoleItemType = TestFishingRod,
                BaitItemType = 267
            };
            var spec = new FishingAttemptSpec
            {
                Context = new InformationWorldContext
                {
                    MainType = typeof(Terraria.Main),
                    LocalPlayer = player
                },
                FishingConditions = fishingConditions,
                TileX = 12,
                TileY = 34,
                InLava = true,
                InHoney = true,
                WaterTilesCount = 211,
                WaterNeeded = 300,
                Chums = 2,
                FishingLevel = 67,
                CanFishInLava = true,
                QuestFish = 2454,
                Roll = new FishingConditionRoll
                {
                    HeightLevel = 3,
                    Corrupt = true,
                    Crimson = false,
                    Jungle = true,
                    InHoney = true,
                    Snow = true,
                    Desert = false,
                    InfectedDesert = true,
                    RemixOcean = true,
                    Crate = true,
                    Junk = false
                }
            };

            var fishingContext = InformationFishingContextFactory.CreateContext(spec);
            if (fishingContext == null)
            {
                throw new InvalidOperationException("Expected fishing context factory to create a context when Terraria fishing stub types are available.");
            }

            AssertMemberReference(fishingContext, "Player", player, "context player");
            AssertMemberBool(fishingContext, "RolledCorruption", true, "rolled corruption");
            AssertMemberBool(fishingContext, "RolledCrimson", false, "rolled crimson");
            AssertMemberBool(fishingContext, "RolledJungle", true, "rolled jungle");
            AssertMemberBool(fishingContext, "RolledSnow", true, "rolled snow");
            AssertMemberBool(fishingContext, "RolledDesert", false, "rolled desert");
            AssertMemberBool(fishingContext, "RolledInfectedDesert", true, "rolled infected desert");
            AssertMemberBool(fishingContext, "RolledRemixOcean", true, "rolled remix ocean");

            var attempt = InformationReflection.GetMember(fishingContext, "Fisher");
            if (attempt == null)
            {
                throw new InvalidOperationException("Expected fishing context factory to attach a FishingAttempt instance.");
            }

            AssertMemberReference(attempt, "playerFishingConditions", fishingConditions, "attempt fishing conditions");
            AssertMemberInt(attempt, "X", 12, "attempt X");
            AssertMemberInt(attempt, "Y", 34, "attempt Y");
            AssertMemberInt(attempt, "bobberType", 0, "attempt bobber type");
            AssertMemberBool(attempt, "common", false, "attempt common");
            AssertMemberBool(attempt, "uncommon", false, "attempt uncommon");
            AssertMemberBool(attempt, "rare", false, "attempt rare");
            AssertMemberBool(attempt, "veryrare", false, "attempt very rare");
            AssertMemberBool(attempt, "legendary", false, "attempt legendary");
            AssertMemberBool(attempt, "crate", true, "attempt crate");
            AssertMemberBool(attempt, "junk", false, "attempt junk");
            AssertMemberBool(attempt, "inLava", true, "attempt lava");
            AssertMemberBool(attempt, "inHoney", true, "attempt honey");
            AssertMemberInt(attempt, "waterTilesCount", 211, "attempt water tiles");
            AssertMemberInt(attempt, "waterNeededToFish", 300, "attempt water needed");
            AssertMemberFloat(attempt, "waterQuality", 0f, "attempt water quality");
            AssertMemberInt(attempt, "chumsInWater", 2, "attempt chums");
            AssertMemberInt(attempt, "fishingLevel", 67, "attempt fishing level");
            AssertMemberBool(attempt, "CanFishInLava", true, "attempt lava capability");
            AssertMemberFloat(attempt, "atmo", 0f, "attempt atmo");
            AssertMemberInt(attempt, "questFish", 2454, "attempt quest fish");
            AssertMemberInt(attempt, "heightLevel", 3, "attempt height level");
            AssertMemberInt(attempt, "rolledItemDrop", 0, "attempt rolled item");
            AssertMemberInt(attempt, "rolledEnemySpawn", 0, "attempt rolled enemy");
        }

        private static void InformationFishRuleEvaluatorKeepsOrderAndDeduplicates()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var rules = new ArrayList
            {
                new TestInformationFishDropRule(new[] { 5, 3, 5 }),
                new TestInformationFishDropRule(new[] { 3, 7 }),
                new TestInformationFishDropRule(new[] { 9 }) { Allow = false },
                new TestInformationFishDropRule(new[] { 11 }) { IsStopper = true },
                new TestInformationFishDropRule(new[] { 13 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);

            AssertIntSequence(itemIds, new[] { 5, 3, 7, 11 }, "fish rule order and duplicate handling");
        }

        private static void InformationFishRuleEvaluatorRespectsMaxCatchItems()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var possibleItems = new int[InformationFishDropRuleEvaluator.MaxCatchItems + 10];
            for (var index = 0; index < possibleItems.Length; index++)
            {
                possibleItems[index] = index + 1;
            }

            var rules = new ArrayList
            {
                new TestInformationFishDropRule(possibleItems),
                new TestInformationFishDropRule(new[] { 9999 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);

            if (itemIds.Count != InformationFishDropRuleEvaluator.MaxCatchItems)
            {
                throw new InvalidOperationException("Expected fish rule evaluator to stop at MaxCatchItems.");
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                var expected = index + 1;
                if (itemIds[index] != expected)
                {
                    throw new InvalidOperationException("Expected bounded fish rule item " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void InformationFishRuleEvaluatorCachesMeetsConditionsLookup()
        {
            InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
            var rules = new ArrayList
            {
                new TestInformationFishDropRule(new[] { 1 }),
                new TestInformationFishDropRule(new[] { 2 })
            };
            var itemIds = new List<int>();
            var seen = new HashSet<int>();

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);
            if (InformationFishDropRuleEvaluator.MeetsConditionsMethodCacheCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected fish rule evaluator to cache one MeetsConditions lookup for rules of the same type.");
            }

            InformationFishDropRuleEvaluator.AddMatchingRuleItems(rules, new object(), itemIds, seen);
            if (InformationFishDropRuleEvaluator.MeetsConditionsMethodCacheCountForTesting != 1)
            {
                throw new InvalidOperationException("Expected fish rule evaluator cache hits not to add duplicate MethodInfo entries.");
            }
        }

        private static void InformationFishingGlobalEmptyQueryKeepsHeavyCountersIdle()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            try
            {
                bool truncated;
                string message;
                var candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(null, "   ", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "请输入名称或 ID 搜索全游戏可钓鱼获", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected empty global fishing search query to return the stable prompt without candidates.");
                }

                if (InformationFishingCatchResolver.EarlyCacheHitCount != 0 ||
                    InformationFishingCatchResolver.EarlyCacheMissCount != 0 ||
                    InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected empty global fishing search query to avoid catch cache, water scan, and conditions read counters.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
            }
        }

        private static void InformationFishingSearchHelpersKeepStableSemantics()
        {
            int itemId;
            if (!InformationFishingCatchResolver.TryParseSearchItemIdForTesting("  #701  ", out itemId) || itemId != 701)
            {
                throw new InvalidOperationException("Expected global fishing search to parse #item id queries.");
            }

            if (!InformationFishingCatchResolver.TryParseSearchItemIdForTesting("702", out itemId) || itemId != 702)
            {
                throw new InvalidOperationException("Expected global fishing search to parse numeric item id queries.");
            }

            if (InformationFishingCatchResolver.TryParseSearchItemIdForTesting("0", out itemId) ||
                InformationFishingCatchResolver.TryParseSearchItemIdForTesting("70x", out itemId))
            {
                throw new InvalidOperationException("Expected invalid global fishing search id queries to fall back to text matching.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(701, "Crate Fish", "WoodenCrate", "#701", true, 701))
            {
                throw new InvalidOperationException("Expected global fishing search to match parsed item ids.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(702, "Quest Fish", string.Empty, "quest", false, 0))
            {
                throw new InvalidOperationException("Expected global fishing search to match display names with current culture ignore case.");
            }

            if (!InformationFishingCatchResolver.MatchesGlobalSearchForTesting(703, string.Empty, "HardmodeCrateFish", "hardmodecrate", false, 0))
            {
                throw new InvalidOperationException("Expected global fishing search to match internal names ordinal-ignore-case.");
            }

            if (InformationFishingCatchResolver.MatchesGlobalSearchForTesting(704, "Other Fish", "OtherInternal", "missing", false, 0))
            {
                throw new InvalidOperationException("Expected unrelated global fishing search text not to match.");
            }
        }

        private static void InformationFishingItemNameResolverKeepsCacheBoundaries()
        {
            WithGlobalFishingSearchFixture(context =>
            {
                Terraria.Lang.ItemNames[700] = "First Bass";
                var firstName = InformationFishingCatchResolver.ResolveFishingItemNameForTesting(700);
                Terraria.Lang.ItemNames[700] = "Second Bass";
                var secondName = InformationFishingCatchResolver.ResolveFishingItemNameForTesting(700);
                if (!string.Equals(firstName, "First Bass", StringComparison.Ordinal) ||
                    !string.Equals(secondName, "Second Bass", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected fishing display names to follow current language data without a permanent display-name cache.");
                }

                var internalName = InformationFishingCatchResolver.ResolveFishingItemInternalNameForTesting(702);
                Terraria.ID.ItemID.Search.SetName(702, "ChangedInternalFish");
                var cachedInternalName = InformationFishingCatchResolver.ResolveFishingItemInternalNameForTesting(702);
                if (!string.Equals(internalName, "QuestInternalFish", StringComparison.Ordinal) ||
                    !string.Equals(cachedInternalName, "QuestInternalFish", StringComparison.Ordinal) ||
                    InformationFishingCatchResolver.FishingItemInternalNameCacheCountForTesting != 1)
                {
                    throw new InvalidOperationException("Expected fishing internal names to use the stable internal-name cache.");
                }

                if (!InformationFishingCatchResolver.IsFishingCrateItemForTesting(701) ||
                    !InformationFishingCatchResolver.IsFishingCrateItemForTesting(703) ||
                    InformationFishingCatchResolver.IsFishingCrateItemForTesting(700))
                {
                    throw new InvalidOperationException("Expected fishing crate item markers to read normal and hardmode crate sets.");
                }

                var questIds = InformationFishingCatchResolver.ReadAnglerQuestFishIdsForTesting(context);
                if (!questIds.Contains(702) || !questIds.Contains(704) || questIds.Contains(700))
                {
                    throw new InvalidOperationException("Expected fishing quest fish ids to come from Main.anglerQuestItemNetIDs.");
                }
            });
        }

        private static void InformationFishingGlobalSearchKeepsResultSemantics()
        {
            WithGlobalFishingSearchFixture(context =>
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();

                bool truncated;
                string message;
                var candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "fish", 2, out truncated, out message);
                if (candidates.Count != 2 ||
                    !truncated ||
                    !string.Equals(message, "结果较多，请继续输入缩小范围", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to honor maxResults and truncated message.");
                }

                if (InformationFishingCatchResolver.WaterScanCount != 0 ||
                    InformationFishingCatchResolver.ConditionsReadCount != 0)
                {
                    throw new InvalidOperationException("Expected global fishing search not to scan water or read FishingConditions.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "#701", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 701, "Crate Fish", true, false, "id query crate result");
                if (truncated || !string.Equals(message, string.Empty, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected exact id global fishing search to avoid truncated and extra messages.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "questinternal", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 702, "Quest Fish", false, true, "internal name quest result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "hardmodeinternal", 10, out truncated, out message);
                AssertSingleGlobalFishingCandidate(candidates, 703, "Hardmode Crate Fish", true, false, "hardmode crate result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "zombiemerman", 10, out truncated, out message);
                AssertSingleGlobalFishingEnemyCandidate(candidates, Terraria.ID.NPCID.ZombieMerman, "global fishable enemy result");

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(null, "fish", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "环境不可用", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the unavailable environment message.");
                }

                var oldRules = Terraria.Main.FishDropsDB;
                Terraria.Main.FishDropsDB = null;
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "fish", 10, out truncated, out message);
                Terraria.Main.FishDropsDB = oldRules;
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "鱼获规则不可用", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the unavailable rules message.");
                }

                candidates = InformationFishingCatchResolver.ResolveGlobalFishableItemCandidates(context, "does-not-exist", 10, out truncated, out message);
                if (candidates.Count != 0 ||
                    truncated ||
                    !string.Equals(message, "无匹配鱼获", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected global fishing search to keep the no-match message.");
                }
            });
        }

        private static void AssertSingleGlobalFishingCandidate(
            IList<FishingCatchCandidate> candidates,
            int expectedId,
            string expectedName,
            bool expectedCrate,
            bool expectedQuestFish,
            string label)
        {
            if (candidates == null || candidates.Count != 1)
            {
                throw new InvalidOperationException("Expected one " + label + ".");
            }

            var candidate = candidates[0];
            if (candidate.Id != expectedId ||
                !string.Equals(candidate.DisplayName, expectedName, StringComparison.Ordinal) ||
                candidate.IsCrate != expectedCrate ||
                candidate.IsQuestFish != expectedQuestFish ||
                candidate.IsEnemy)
            {
                throw new InvalidOperationException("Unexpected " + label + ".");
            }
        }

        private static void AssertSingleGlobalFishingEnemyCandidate(
            IList<FishingCatchCandidate> candidates,
            int expectedId,
            string label)
        {
            if (candidates == null || candidates.Count != 1)
            {
                throw new InvalidOperationException("Expected one " + label + ".");
            }

            var candidate = candidates[0];
            if (candidate.Id != expectedId ||
                !string.Equals(candidate.Kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase) ||
                !candidate.IsEnemy ||
                candidate.IsCrate ||
                candidate.IsQuestFish)
            {
                throw new InvalidOperationException("Unexpected " + label + ".");
            }
        }

        private static void WithGlobalFishingSearchFixture(Action<InformationWorldContext> test)
        {
            var oldRules = Terraria.Main.FishDropsDB;
            var oldQuestIds = Terraria.Main.anglerQuestItemNetIDs;
            var oldQuestIndex = Terraria.Main.anglerQuest;
            var oldQuestFinished = Terraria.Main.anglerQuestFinished;
            try
            {
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                InformationFishingCatchResolver.ResetFishingItemNameResolverForTesting();
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationFishingEnemyCandidateResolver.ResetForTesting();
                Terraria.Lang.ItemNames.Clear();
                Terraria.Lang.NpcNames.Clear();
                Terraria.ID.ItemID.Search.Clear();
                Terraria.ID.NPCID.Search.Clear();
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrate, 0, Terraria.ID.ItemID.Sets.IsFishingCrate.Length);
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrateHardmode, 0, Terraria.ID.ItemID.Sets.IsFishingCrateHardmode.Length);

                Terraria.Main.FishDropsDB = new TestInformationFishDropsDb(new ArrayList
                {
                    new TestInformationFishDropRule(new[] { 700, 701, 702, 703 })
                });
                Terraria.Main.anglerQuestItemNetIDs = new[] { 702, 704 };
                Terraria.Main.anglerQuest = 0;
                Terraria.Main.anglerQuestFinished = false;

                Terraria.Lang.ItemNames[700] = "Bass Fish";
                Terraria.Lang.ItemNames[701] = "Crate Fish";
                Terraria.Lang.ItemNames[702] = "Quest Fish";
                Terraria.Lang.ItemNames[703] = "Hardmode Crate Fish";
                Terraria.Lang.ItemNames[704] = "Quest Bonus Fish";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.ZombieMerman] = "Zombie Merman";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.EyeballFlyingFish] = "Eyeball Flying Fish";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.GoblinShark] = "Goblin Shark";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.BloodEelHead] = "Blood Eel";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.BloodNautilus] = "Dreadnautilus";
                Terraria.Lang.NpcNames[Terraria.ID.NPCID.TownSlimeRed] = "Surly Slime";
                Terraria.ID.ItemID.Search.SetName(700, "BassInternalFish");
                Terraria.ID.ItemID.Search.SetName(701, "CrateInternalFish");
                Terraria.ID.ItemID.Search.SetName(702, "QuestInternalFish");
                Terraria.ID.ItemID.Search.SetName(703, "HardmodeInternalFish");
                Terraria.ID.ItemID.Search.SetName(704, "QuestBonusInternalFish");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.ZombieMerman, "ZombieMerman");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.EyeballFlyingFish, "EyeballFlyingFish");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.GoblinShark, "GoblinShark");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.BloodEelHead, "BloodEelHead");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.BloodNautilus, "BloodNautilus");
                Terraria.ID.NPCID.Search.SetName(Terraria.ID.NPCID.TownSlimeRed, "TownSlimeRed");
                Terraria.ID.ItemID.Sets.IsFishingCrate[701] = true;
                Terraria.ID.ItemID.Sets.IsFishingCrateHardmode[703] = true;

                test(new InformationWorldContext
                {
                    MainType = typeof(Terraria.Main)
                });
            }
            finally
            {
                Terraria.Main.FishDropsDB = oldRules;
                Terraria.Main.anglerQuestItemNetIDs = oldQuestIds;
                Terraria.Main.anglerQuest = oldQuestIndex;
                Terraria.Main.anglerQuestFinished = oldQuestFinished;
                Terraria.Lang.ItemNames.Clear();
                Terraria.Lang.NpcNames.Clear();
                Terraria.ID.ItemID.Search.Clear();
                Terraria.ID.NPCID.Search.Clear();
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrate, 0, Terraria.ID.ItemID.Sets.IsFishingCrate.Length);
                Array.Clear(Terraria.ID.ItemID.Sets.IsFishingCrateHardmode, 0, Terraria.ID.ItemID.Sets.IsFishingCrateHardmode.Length);
                InformationFishDropRuleEvaluator.ResetReflectionCacheForTesting();
                InformationFishingCatchResolver.ResetFishingItemNameResolverForTesting();
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationFishingEnemyCandidateResolver.ResetForTesting();
            }
        }

        private static void InformationFishingDiagnosticsSnapshotKeepsStableFieldMapping()
        {
            InformationFishingCatchResolver.ResetCatchCacheForTesting();
            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            try
            {
                InformationFishingCatchDiagnostics.IncrementEarlyCacheHit();
                InformationFishingCatchDiagnostics.IncrementEarlyCacheHit();
                InformationFishingCatchDiagnostics.IncrementEarlyCacheMiss();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementWaterScan();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();
                InformationFishingCatchDiagnostics.IncrementConditionsRead();

                var snapshot = InformationFishingCatchDiagnostics.ReadSnapshot();
                if (snapshot.EarlyCacheHitCount != 2 ||
                    snapshot.EarlyCacheMissCount != 1 ||
                    snapshot.WaterScanCount != 3 ||
                    snapshot.ConditionsReadCount != 4)
                {
                    throw new InvalidOperationException("Expected fishing catch diagnostics snapshot to expose existing counters without recalculating them.");
                }

                var diagnostics = InformationOverlayService.GetDiagnostics();
                if (diagnostics.FishingCatchEarlyCacheHitCount != 2 ||
                    diagnostics.FishingCatchEarlyCacheMissCount != 1 ||
                    diagnostics.FishingWaterScanCount != 3 ||
                    diagnostics.FishingConditionsReadCount != 4 ||
                    diagnostics.FishingProjectileFallbackScanCount != 0)
                {
                    throw new InvalidOperationException("Expected information diagnostics to map fishing catch counters without merging projectile fallback diagnostics.");
                }
            }
            finally
            {
                InformationFishingCatchResolver.ResetCatchCacheForTesting();
                InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            }
        }

        private static void InformationFishingBobberFreshInactiveSkipsProjectileFallback()
        {
            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            FishingBobberObserver.RemoveMissing(null);
            FishingBobberObserver.MarkNoActiveObservation(200);
            Terraria.Main.myPlayer = 0;
            Terraria.Main.projectile = new object[]
            {
                new TestInformationProjectile
                {
                    active = true,
                    bobber = true,
                    owner = 0,
                    identity = 91,
                    Center = new Terraria.TestVector2 { X = 320f, Y = 480f }
                }
            };

            var context = new InformationWorldContext
            {
                MainType = typeof(Terraria.Main),
                GameUpdateCount = 201
            };
            float x;
            float y;
            if (InformationOverlayService.TryFindLocalBobberForTesting(context, out x, out y))
            {
                throw new InvalidOperationException("Expected fresh inactive observer state to skip projectile fallback.");
            }

            var diagnostics = InformationOverlayService.GetDiagnostics();
            if (diagnostics.FishingBobberObserverFreshInactiveSkipCount != 1 ||
                diagnostics.FishingProjectileFallbackScanCount != 0)
            {
                throw new InvalidOperationException("Expected fresh inactive observer diagnostics to record a skip without projectile fallback.");
            }

            InformationOverlayService.ResetFishingBobberLookupDiagnosticsForTesting();
            FishingBobberObserver.RemoveMissing(null);
            FishingBobberObserver.MarkNoActiveObservation(200);
            context.GameUpdateCount = 204;
            if (!InformationOverlayService.TryFindLocalBobberForTesting(context, out x, out y) ||
                Math.Abs(x - 320f) > 0.01f ||
                Math.Abs(y - 480f) > 0.01f)
            {
                throw new InvalidOperationException("Expected stale inactive observer state to fall back to projectile scanning.");
            }

            diagnostics = InformationOverlayService.GetDiagnostics();
            if (diagnostics.FishingBobberObserverFreshInactiveSkipCount != 0 ||
                diagnostics.FishingProjectileFallbackScanCount != 1)
            {
                throw new InvalidOperationException("Expected stale inactive observer diagnostics to record projectile fallback.");
            }
        }

        private static void InformationLuckBreakdownFollowsTerrariaSourceFormula()
        {
            AssertNear(InformationLuckBreakdownBuilder.CalculateLadyBugContributionForTesting(43200, 43200, -10800), 0.2d, "positive ladybug luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateLadyBugContributionForTesting(-10800, 43200, -10800), -0.2d, "negative ladybug luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0d), 0d, "zero coin luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0.249d), 0.025d, "minimum nonzero coin luck");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(0.25d), 0.05d, "coin luck first threshold");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(25d), 0.1d, "coin luck mid threshold");
            AssertNear(InformationLuckBreakdownBuilder.CalculateCoinLuckContributionForTesting(249001d), 0.2d, "coin luck maximum threshold");
        }

        private static void InformationLuckBreakdownWrapsSourceDetails()
        {
            var contributions = new List<InformationLuckContribution>
            {
                new InformationLuckContribution { Label = "幸运药水", Amount = 0.3d, Detail = "等级3" },
                new InformationLuckContribution { Label = "花园侏儒", Amount = 0.2d },
                new InformationLuckContribution { Label = "火把", Amount = -0.06d, Detail = "原值-0.3" }
            };

            var lines = InformationLuckBreakdownBuilder.BuildDisplayLinesForTesting(0.54d, contributions, 24);
            if (lines.Length < 3)
            {
                throw new InvalidOperationException("Expected luck breakdown to wrap source details into multiple lines.");
            }

            AssertContains(lines[0], "幸运值: +0.54");
            AssertDoesNotContain(lines[0], "已解析");
            AssertContains(string.Join("|", lines), "其他/未解析 +0.1");
            AssertContains(string.Join("|", lines), "幸运药水 +0.3");
            AssertContains(string.Join("|", lines), "火把 -0.06");
        }

        private static void AssertHeightLevels(InformationWorldContext context, int tileY, int[] buffer, int[] expected, string label)
        {
            var count = InformationFishingConditionRolls.BuildHeightLevels(context, tileY, buffer);
            if (count != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " height roll count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ", got " + count.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < count; index++)
            {
                if (buffer[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " height roll at " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index].ToString(CultureInfo.InvariantCulture) + ", got " + buffer[index].ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static void AssertBoolRolls(bool[] actual, bool[] expected, string label)
        {
            if (actual == null || actual.Length != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " bool roll count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " bool roll at " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index] + ", got " + actual[index] + ".");
                }
            }
        }

        private static void AssertMemberReference(object instance, string name, object expected, string label)
        {
            var actual = InformationReflection.GetMember(instance, name);
            if (!object.ReferenceEquals(actual, expected))
            {
                throw new InvalidOperationException("Expected " + label + " to preserve the original object reference.");
            }
        }

        private static void AssertMemberInt(object instance, string name, int expected, string label)
        {
            var actual = InformationFishingCatchResolver.ToInt(InformationReflection.GetMember(instance, name), int.MinValue);
            if (actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void AssertMemberFloat(object instance, string name, float expected, string label)
        {
            var actual = InformationFishingCatchResolver.ToFloat(InformationReflection.GetMember(instance, name), float.NaN);
            if (float.IsNaN(actual) || Math.Abs(actual - expected) > 0.0001f)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected.ToString(CultureInfo.InvariantCulture) + ", got " + actual.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        private static void AssertMemberBool(object instance, string name, bool expected, string label)
        {
            bool actual;
            if (!InformationFishingCatchResolver.TryConvertBool(InformationReflection.GetMember(instance, name), out actual) || actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to be " + expected + ".");
            }
        }

        private static void AssertIntSequence(IList<int> actual, int[] expected, string label)
        {
            if (actual == null || actual.Count != expected.Length)
            {
                throw new InvalidOperationException("Expected " + label + " count " + expected.Length.ToString(CultureInfo.InvariantCulture) + ".");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    throw new InvalidOperationException("Expected " + label + " item " + index.ToString(CultureInfo.InvariantCulture) + " to be " + expected[index].ToString(CultureInfo.InvariantCulture) + ", got " + actual[index].ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private static string SummarizeFishingConditionRoll(FishingConditionRoll roll)
        {
            return "h" + roll.HeightLevel.ToString(CultureInfo.InvariantCulture) +
                   "|corrupt=" + (roll.Corrupt ? "1" : "0") +
                   "|crimson=" + (roll.Crimson ? "1" : "0") +
                   "|honey=" + (roll.InHoney ? "1" : "0") +
                   "|jungle=" + (roll.Jungle ? "1" : "0") +
                   "|snow=" + (roll.Snow ? "1" : "0") +
                   "|desert=" + (roll.Desert ? "1" : "0") +
                   "|infected=" + (roll.InfectedDesert ? "1" : "0") +
                   "|crate=" + (roll.Crate ? "1" : "0") +
                   "|junk=" + (roll.Junk ? "1" : "0");
        }

        private sealed class TestInformationFishingEnvironmentPlayer
        {
            public double luck;
            public int fishingSkill;
            public bool accLavaFishing;
            public bool ZoneJungle;
            public bool ZoneSnow;
            public bool ZoneDesert;
            public bool ZoneUndergroundDesert;
            public bool ZoneCorrupt;
            public bool ZoneCrimson;
            public int[] buffType = new int[0];
            public int[] buffTime = new int[0];
        }

        private sealed class TestInformationFishingConditions
        {
            public int PoleItemType;
            public int BaitItemType;
        }

        private sealed class TestInformationFishDropRule
        {
            public int[] PossibleItems;
            public bool IsStopper;
            public bool Allow;
            public int MeetsConditionsCallCount;

            public TestInformationFishDropRule(int[] possibleItems)
            {
                PossibleItems = possibleItems;
                Allow = true;
            }

            private bool MeetsConditions(object fishingContext, bool includeHighQuality)
            {
                MeetsConditionsCallCount++;
                return Allow && fishingContext != null && includeHighQuality;
            }
        }

        private sealed class TestInformationFishDropsDb
        {
            public ArrayList Rules;

            public TestInformationFishDropsDb(ArrayList rules)
            {
                Rules = rules;
            }
        }

        private static string BuildDetailedFishingCatchQuerySignature(
            InformationWorldContext context,
            string liquidKind,
            int waterTiles,
            int chums,
            int waterNeeded,
            bool junkPossible,
            int finalFishingLevel,
            int fishingLevel,
            bool canFishInLava,
            string filterSignature)
        {
            return InformationFishingCatchResolver.BuildCatchQuerySignatureForTesting(
                context,
                80,
                120,
                liquidKind,
                waterTiles,
                chums,
                waterNeeded,
                junkPossible,
                finalFishingLevel,
                fishingLevel,
                25,
                TestFishingRod,
                15,
                267,
                canFishInLava,
                2454,
                filterSignature);
        }

        private static void AssertFishingCatchSignatureChanged(string baseline, string changed, string fieldName)
        {
            if (string.Equals(baseline, changed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + fieldName + " changes to dirty the fishing catch query cache key.");
            }
        }

        private static IList<FishingCatchCandidate> CreateSingleFishingCatchCandidate(int id, string name)
        {
            return new List<FishingCatchCandidate>
            {
                new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = id,
                    DisplayName = name,
                    DisplayNameSnapshot = name
                }
            };
        }

        private static class TestInformationFishingQueryMain
        {
            public static bool hardMode;
            public static int moonPhase;
            public static int maxTilesX;
            public static int maxTilesY;
            public static double worldSurface;
            public static double rockLayer;
            public static double time;
            public static bool remixWorld;
            public static bool notTheBeesWorld;

            public static void Reset()
            {
                hardMode = false;
                moonPhase = 1;
                maxTilesX = 4200;
                maxTilesY = 1200;
                worldSurface = 320d;
                rockLayer = 520d;
                time = 7200d;
                remixWorld = false;
                notTheBeesWorld = false;
            }
        }

        private sealed class TestInformationProjectile
        {
            public bool active;
            public bool bobber;
            public int owner;
            public int identity;
            public Terraria.TestVector2 Center;
        }

        private sealed class TestInformationPropertyTile
        {
            public bool HasTile { get; set; }
            public ushort TileType { get; set; }
            public short TileFrameX { get; set; }
            public short TileFrameY { get; set; }
        }

        private sealed class TestInformationMethodTile
        {
            public ushort type = 236;
            public short frameX = 54;
            public short frameY = 72;

            public bool active()
            {
                return true;
            }
        }


    }
}
