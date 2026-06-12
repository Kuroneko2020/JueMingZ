using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal sealed class ChestItemLocatorOverlayHitView
    {
        public int ChestX { get; private set; }
        public int ChestY { get; private set; }
        public int ScreenX { get; private set; }
        public int ScreenY { get; private set; }
        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }
        public string Label { get; private set; }
        public int TotalStack { get; private set; }

        public ChestItemLocatorOverlayHitView(
            int chestX,
            int chestY,
            int screenX,
            int screenY,
            int pixelWidth,
            int pixelHeight,
            string label,
            int totalStack)
        {
            ChestX = chestX;
            ChestY = chestY;
            ScreenX = screenX;
            ScreenY = screenY;
            PixelWidth = Math.Max(1, pixelWidth);
            PixelHeight = Math.Max(1, pixelHeight);
            Label = label ?? string.Empty;
            TotalStack = Math.Max(0, totalStack);
        }
    }

    internal sealed class ChestItemLocatorOverlayView
    {
        public bool Enabled { get; private set; }
        public string SkipReason { get; private set; }
        public long QueryVersion { get; private set; }
        public string SnapshotStatus { get; private set; }
        public int CandidateChestCount { get; private set; }
        public int ScannedChestCount { get; private set; }
        public int HitCount { get; private set; }
        public int DrawnHitCount { get; private set; }
        public ulong SnapshotAgeTicks { get; private set; }
        public IReadOnlyList<ChestItemLocatorOverlayHitView> Hits { get; private set; }

        public ChestItemLocatorOverlayView(
            bool enabled,
            string skipReason,
            long queryVersion,
            string snapshotStatus,
            int candidateChestCount,
            int scannedChestCount,
            int hitCount,
            int drawnHitCount,
            ulong snapshotAgeTicks,
            IList<ChestItemLocatorOverlayHitView> hits)
        {
            Enabled = enabled;
            SkipReason = skipReason ?? string.Empty;
            QueryVersion = queryVersion;
            SnapshotStatus = snapshotStatus ?? string.Empty;
            CandidateChestCount = Math.Max(0, candidateChestCount);
            ScannedChestCount = Math.Max(0, scannedChestCount);
            HitCount = Math.Max(0, hitCount);
            DrawnHitCount = Math.Max(0, drawnHitCount);
            SnapshotAgeTicks = snapshotAgeTicks;

            var copy = new List<ChestItemLocatorOverlayHitView>();
            if (hits != null)
            {
                for (var index = 0; index < hits.Count; index++)
                {
                    var hit = hits[index];
                    if (hit != null)
                    {
                        copy.Add(hit);
                    }
                }
            }

            Hits = new ReadOnlyCollection<ChestItemLocatorOverlayHitView>(copy);
        }
    }

    internal sealed class ChestItemLocatorOverlayDiagnostics
    {
        public bool Enabled { get; set; }
        public long QueryVersion { get; set; }
        public string SnapshotStatus { get; set; }
        public int CandidateChestCount { get; set; }
        public int ScannedChestCount { get; set; }
        public int HitCount { get; set; }
        public int DrawnHitCount { get; set; }
        public string SkipReason { get; set; }
        public string RecentElapsedBucket { get; set; }
        public long SnapshotAgeTicks { get; set; }

        public ChestItemLocatorOverlayDiagnostics()
        {
            SnapshotStatus = string.Empty;
            SkipReason = string.Empty;
            RecentElapsedBucket = string.Empty;
        }

        public ChestItemLocatorOverlayDiagnostics Clone()
        {
            return new ChestItemLocatorOverlayDiagnostics
            {
                Enabled = Enabled,
                QueryVersion = QueryVersion,
                SnapshotStatus = SnapshotStatus ?? string.Empty,
                CandidateChestCount = CandidateChestCount,
                ScannedChestCount = ScannedChestCount,
                HitCount = HitCount,
                DrawnHitCount = DrawnHitCount,
                SkipReason = SkipReason ?? string.Empty,
                RecentElapsedBucket = RecentElapsedBucket ?? string.Empty,
                SnapshotAgeTicks = SnapshotAgeTicks
            };
        }
    }
}
