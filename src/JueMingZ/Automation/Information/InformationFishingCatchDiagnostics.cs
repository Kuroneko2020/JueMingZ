using System.Threading;

namespace JueMingZ.Automation.Information
{
    internal struct InformationFishingCatchDiagnosticsSnapshot
    {
        public long EarlyCacheHitCount;
        public long EarlyCacheMissCount;
        public long WaterScanCount;
        public long ConditionsReadCount;
    }

    internal static class InformationFishingCatchDiagnostics
    {
        private static long _earlyCacheHitCount;
        private static long _earlyCacheMissCount;
        private static long _waterScanCount;
        private static long _conditionsReadCount;

        public static long EarlyCacheHitCount
        {
            get { return Interlocked.Read(ref _earlyCacheHitCount); }
        }

        public static long EarlyCacheMissCount
        {
            get { return Interlocked.Read(ref _earlyCacheMissCount); }
        }

        public static long WaterScanCount
        {
            get { return Interlocked.Read(ref _waterScanCount); }
        }

        public static long ConditionsReadCount
        {
            get { return Interlocked.Read(ref _conditionsReadCount); }
        }

        public static InformationFishingCatchDiagnosticsSnapshot ReadSnapshot()
        {
            return new InformationFishingCatchDiagnosticsSnapshot
            {
                EarlyCacheHitCount = EarlyCacheHitCount,
                EarlyCacheMissCount = EarlyCacheMissCount,
                WaterScanCount = WaterScanCount,
                ConditionsReadCount = ConditionsReadCount
            };
        }

        public static void IncrementEarlyCacheHit()
        {
            Interlocked.Increment(ref _earlyCacheHitCount);
        }

        public static void IncrementEarlyCacheMiss()
        {
            Interlocked.Increment(ref _earlyCacheMissCount);
        }

        public static void IncrementWaterScan()
        {
            Interlocked.Increment(ref _waterScanCount);
        }

        public static void IncrementConditionsRead()
        {
            Interlocked.Increment(ref _conditionsReadCount);
        }

        public static void ResetForTesting()
        {
            Interlocked.Exchange(ref _earlyCacheHitCount, 0);
            Interlocked.Exchange(ref _earlyCacheMissCount, 0);
            Interlocked.Exchange(ref _waterScanCount, 0);
            Interlocked.Exchange(ref _conditionsReadCount, 0);
        }
    }
}
