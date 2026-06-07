using System.Collections.Generic;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingCatchCache
    {
        private const int CatchCacheLimit = 16;
        private const int EarlyCatchCacheLimit = 8;
        private static readonly object CatchCacheSyncRoot = new object();
        private static readonly Dictionary<FishingCatchQueryKey, FishingCatchCacheEntry> CatchCache =
            new Dictionary<FishingCatchQueryKey, FishingCatchCacheEntry>();
        private static readonly Queue<FishingCatchQueryKey> CatchCacheOrder = new Queue<FishingCatchQueryKey>();
        private static readonly Dictionary<FishingCatchEarlyCacheKey, FishingCatchCacheEntry> EarlyCatchCache =
            new Dictionary<FishingCatchEarlyCacheKey, FishingCatchCacheEntry>();
        private static readonly Queue<FishingCatchEarlyCacheKey> EarlyCatchCacheOrder = new Queue<FishingCatchEarlyCacheKey>();

        public static int CatchCacheLimitForTesting
        {
            get { return CatchCacheLimit; }
        }

        public static int EarlyCatchCacheLimitForTesting
        {
            get { return EarlyCatchCacheLimit; }
        }

        public static bool TryGetEarlyCatchCache(FishingCatchEarlyCacheKey key, out FishingCatchCacheEntry entry)
        {
            lock (CatchCacheSyncRoot)
            {
                if (EarlyCatchCache.TryGetValue(key, out entry) && entry != null)
                {
                    InformationFishingCatchDiagnostics.IncrementEarlyCacheHit();
                    return true;
                }
            }

            InformationFishingCatchDiagnostics.IncrementEarlyCacheMiss();
            entry = null;
            return false;
        }

        public static void StoreEarlyCatchCache(FishingCatchEarlyCacheKey key, IList<FishingCatchCandidate> candidates, string message)
        {
            lock (CatchCacheSyncRoot)
            {
                if (!EarlyCatchCache.ContainsKey(key))
                {
                    EarlyCatchCacheOrder.Enqueue(key);
                }

                EarlyCatchCache[key] = new FishingCatchCacheEntry
                {
                    Candidates = candidates ?? new List<FishingCatchCandidate>(),
                    Message = message ?? string.Empty
                };

                while (EarlyCatchCacheOrder.Count > EarlyCatchCacheLimit)
                {
                    var oldest = EarlyCatchCacheOrder.Dequeue();
                    EarlyCatchCache.Remove(oldest);
                }
            }
        }

        public static bool TryGetCatchCache(FishingCatchQueryKey key, out FishingCatchCacheEntry entry)
        {
            lock (CatchCacheSyncRoot)
            {
                return CatchCache.TryGetValue(key, out entry) && entry != null;
            }
        }

        public static bool TryGetCatchCacheAndStoreEarly(FishingCatchQueryKey queryKey, FishingCatchEarlyCacheKey earlyKey, out FishingCatchCacheEntry entry)
        {
            if (!TryGetCatchCache(queryKey, out entry))
            {
                return false;
            }

            StoreEarlyCatchCache(earlyKey, entry.Candidates, entry.Message);
            return true;
        }

        public static void StoreCatchCache(FishingCatchQueryKey key, IList<FishingCatchCandidate> candidates, string message)
        {
            lock (CatchCacheSyncRoot)
            {
                if (!CatchCache.ContainsKey(key))
                {
                    CatchCacheOrder.Enqueue(key);
                }

                CatchCache[key] = new FishingCatchCacheEntry
                {
                    Candidates = candidates ?? new List<FishingCatchCandidate>(),
                    Message = message ?? string.Empty
                };

                while (CatchCacheOrder.Count > CatchCacheLimit)
                {
                    var oldest = CatchCacheOrder.Dequeue();
                    CatchCache.Remove(oldest);
                }
            }
        }

        public static void ResetForTesting()
        {
            lock (CatchCacheSyncRoot)
            {
                CatchCache.Clear();
                CatchCacheOrder.Clear();
                EarlyCatchCache.Clear();
                EarlyCatchCacheOrder.Clear();
            }

            InformationFishingCatchDiagnostics.ResetForTesting();
        }
    }
}
