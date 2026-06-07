using System.Collections.Generic;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static partial class InformationFishingCatchResolver
    {
        internal static string ResolveFishingItemNameForTesting(int itemId)
        {
            return InformationFishingItemNameResolver.ResolveItemName(itemId);
        }

        internal static string ResolveFishingItemInternalNameForTesting(int itemId)
        {
            return InformationFishingItemNameResolver.ResolveInternalItemName(itemId);
        }

        internal static bool IsFishingCrateItemForTesting(int itemId)
        {
            return InformationFishingItemNameResolver.IsFishingCrateItem(itemId);
        }

        internal static bool MatchesGlobalSearchForTesting(
            int itemId,
            string displayName,
            string internalName,
            string query,
            bool hasItemIdSearch,
            int searchItemId)
        {
            return InformationFishingCatchSearchService.MatchesGlobalSearch(
                itemId,
                displayName,
                internalName,
                query,
                hasItemIdSearch,
                searchItemId);
        }

        internal static bool TryParseSearchItemIdForTesting(string query, out int itemId)
        {
            return InformationFishingCatchSearchService.TryParseSearchItemId(query, out itemId);
        }

        internal static void ResetFishingItemNameResolverForTesting()
        {
            InformationFishingItemNameResolver.ResetForTesting();
        }

        internal static int FishingItemInternalNameCacheCountForTesting
        {
            get { return InformationFishingItemNameResolver.InternalNameCacheCountForTesting; }
        }

        internal static HashSet<int> ReadAnglerQuestFishIdsForTesting(InformationWorldContext context)
        {
            return InformationFishingItemNameResolver.ReadAnglerQuestFishIds(context);
        }

        internal static void ResetCatchCacheForTesting()
        {
            InformationFishingCatchCache.ResetForTesting();
        }

        internal static int CatchCacheLimitForTesting
        {
            get { return InformationFishingCatchCache.CatchCacheLimitForTesting; }
        }

        internal static int EarlyCatchCacheLimitForTesting
        {
            get { return InformationFishingCatchCache.EarlyCatchCacheLimitForTesting; }
        }

        public static long EarlyCacheHitCount
        {
            get { return InformationFishingCatchDiagnostics.EarlyCacheHitCount; }
        }

        public static long EarlyCacheMissCount
        {
            get { return InformationFishingCatchDiagnostics.EarlyCacheMissCount; }
        }

        public static long WaterScanCount
        {
            get { return InformationFishingCatchDiagnostics.WaterScanCount; }
        }

        public static long ConditionsReadCount
        {
            get { return InformationFishingCatchDiagnostics.ConditionsReadCount; }
        }

        internal static string BuildCatchQuerySignatureForTesting(
            InformationWorldContext context,
            int tileX,
            int tileY,
            string liquidKind,
            int waterTiles,
            int finalFishingLevel,
            int poleItemType,
            int baitItemType,
            int questFish,
            string filterSignature)
        {
            return BuildCatchQuerySignatureForTesting(
                context,
                tileX,
                tileY,
                liquidKind,
                waterTiles,
                0,
                300,
                false,
                finalFishingLevel,
                finalFishingLevel,
                0,
                poleItemType,
                0,
                baitItemType,
                false,
                questFish,
                filterSignature);
        }

        internal static string BuildCatchQuerySignatureForTesting(
            InformationWorldContext context,
            int tileX,
            int tileY,
            string liquidKind,
            int waterTiles,
            int chums,
            int waterNeeded,
            bool junkPossible,
            int finalFishingLevel,
            int fishingLevel,
            int polePower,
            int poleItemType,
            int baitPower,
            int baitItemType,
            bool canFishInLava,
            int questFish,
            string filterSignature)
        {
            return BuildCatchQueryKey(
                context,
                tileX,
                tileY,
                liquidKind,
                waterTiles,
                chums,
                waterNeeded,
                junkPossible,
                finalFishingLevel,
                fishingLevel,
                polePower,
                poleItemType,
                baitPower,
                baitItemType,
                canFishInLava,
                questFish,
                filterSignature).Signature;
        }

        internal static string BuildEarlyCatchQuerySignatureForTesting(
            InformationWorldContext context,
            int tileX,
            int tileY,
            int bobberIdentity,
            int polePower,
            int poleItemType,
            int baitPower,
            int baitItemType,
            int questFish)
        {
            return BuildEarlyCatchCacheKey(
                context,
                tileX,
                tileY,
                bobberIdentity,
                polePower,
                poleItemType,
                baitPower,
                baitItemType,
                questFish).Signature;
        }

        internal static void StoreEarlyCatchCacheForTesting(string signature, IList<FishingCatchCandidate> candidates, string message)
        {
            InformationFishingCatchCache.StoreEarlyCatchCache(new FishingCatchEarlyCacheKey(signature), candidates, message);
        }

        internal static bool TryGetEarlyCatchCacheForTesting(string signature, out IList<FishingCatchCandidate> candidates, out string message)
        {
            FishingCatchCacheEntry entry;
            if (InformationFishingCatchCache.TryGetEarlyCatchCache(new FishingCatchEarlyCacheKey(signature), out entry))
            {
                candidates = entry.Candidates;
                message = entry.Message;
                return true;
            }

            candidates = new List<FishingCatchCandidate>();
            message = string.Empty;
            return false;
        }

        internal static void StoreCatchCacheForTesting(string signature, IList<FishingCatchCandidate> candidates, string message)
        {
            InformationFishingCatchCache.StoreCatchCache(new FishingCatchQueryKey(signature), candidates, message);
        }

        internal static bool TryGetCatchCacheForTesting(string signature, out IList<FishingCatchCandidate> candidates, out string message)
        {
            FishingCatchCacheEntry entry;
            if (InformationFishingCatchCache.TryGetCatchCache(new FishingCatchQueryKey(signature), out entry))
            {
                candidates = entry.Candidates;
                message = entry.Message;
                return true;
            }

            candidates = new List<FishingCatchCandidate>();
            message = string.Empty;
            return false;
        }

        internal static bool TryGetCatchCacheAndBackfillEarlyForTesting(string querySignature, string earlySignature, out IList<FishingCatchCandidate> candidates, out string message)
        {
            FishingCatchCacheEntry entry;
            if (InformationFishingCatchCache.TryGetCatchCacheAndStoreEarly(
                new FishingCatchQueryKey(querySignature),
                new FishingCatchEarlyCacheKey(earlySignature),
                out entry))
            {
                candidates = entry.Candidates;
                message = entry.Message;
                return true;
            }

            candidates = new List<FishingCatchCandidate>();
            message = string.Empty;
            return false;
        }

        internal static string ResolveLiquidKindForTesting(bool inLava, bool inHoney)
        {
            return InformationFishingWaterScanner.ResolveLiquidKind(new FishingWaterScan
            {
                InLava = inLava,
                InHoney = inHoney
            });
        }

        internal static FishingWaterPenaltyResult ApplyFishingWaterPenaltyForTesting(
            InformationWorldContext context,
            float bobberWorldY,
            int waterTiles,
            int chums,
            int finalFishingLevel)
        {
            return InformationFishingWaterScanner.ApplyFishingWaterPenalty(
                context,
                bobberWorldY,
                new FishingWaterScan
                {
                    TotalTiles = waterTiles,
                    Chums = chums
                },
                finalFishingLevel);
        }

        internal static FishingWaterScan ScanFishingWaterForTesting(InformationWorldContext context, int tileX, int tileY)
        {
            return InformationFishingWaterScanner.ScanFishingWater(context, tileX, tileY);
        }

        internal static bool CanFishInLavaForTesting(InformationWorldContext context, object fishingConditions)
        {
            return InformationFishingEnvironmentReader.CanFishInLava(context, fishingConditions);
        }
    }
}
