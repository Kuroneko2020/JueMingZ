using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal sealed class ChestItemLocatorMatchedItem
    {
        public int ItemType { get; private set; }

        public string ItemName { get; private set; }

        public int TotalStack { get; private set; }

        public int SlotCount { get; private set; }

        public int FirstSlot { get; private set; }

        public ChestItemLocatorMatchedItem(int itemType, string itemName, int totalStack, int slotCount, int firstSlot)
        {
            ItemType = itemType;
            ItemName = itemName ?? string.Empty;
            TotalStack = Math.Max(0, totalStack);
            SlotCount = Math.Max(0, slotCount);
            FirstSlot = firstSlot < 0 ? -1 : firstSlot;
        }
    }

    internal sealed class ChestItemLocatorHit
    {
        public int ChestIndex { get; private set; }

        public int ChestX { get; private set; }

        public int ChestY { get; private set; }

        public int TileType { get; private set; }

        public int TileStyle { get; private set; }

        public float WorldX { get; private set; }

        public float WorldY { get; private set; }

        public string ContainerName { get; private set; }

        public int TotalStack { get; private set; }

        public int MatchedSlotCount { get; private set; }

        public IReadOnlyList<ChestItemLocatorMatchedItem> Items { get; private set; }

        public ChestItemLocatorHit(
            int chestIndex,
            int chestX,
            int chestY,
            int tileType,
            int tileStyle,
            float worldX,
            float worldY,
            string containerName,
            IList<ChestItemLocatorMatchedItem> items)
        {
            ChestIndex = chestIndex;
            ChestX = chestX;
            ChestY = chestY;
            TileType = tileType;
            TileStyle = tileStyle;
            WorldX = worldX;
            WorldY = worldY;
            ContainerName = containerName ?? string.Empty;

            var copy = new List<ChestItemLocatorMatchedItem>();
            if (items != null)
            {
                for (var index = 0; index < items.Count; index++)
                {
                    var item = items[index];
                    if (item == null || item.ItemType <= 0 || item.TotalStack <= 0 || item.SlotCount <= 0)
                    {
                        continue;
                    }

                    copy.Add(item);
                    TotalStack += item.TotalStack;
                    MatchedSlotCount += item.SlotCount;
                }
            }

            Items = new ReadOnlyCollection<ChestItemLocatorMatchedItem>(copy);
        }
    }

    internal sealed class ChestItemLocatorSnapshot
    {
        public const string StatusOk = "ok";
        public const string StatusQueryNotReady = "queryNotReady";
        public const string StatusContextUnavailable = "contextUnavailable";
        public const string StatusTilesUnavailable = "tilesUnavailable";

        public static readonly ChestItemLocatorSnapshot Empty = new ChestItemLocatorSnapshot(
            StatusQueryNotReady,
            string.Empty,
            0,
            0,
            string.Empty,
            string.Empty,
            false,
            false,
            0,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            0,
            new List<ChestItemLocatorHit>());

        public string Status { get; private set; }

        public string RefreshReason { get; private set; }

        public long QueryVersion { get; private set; }

        public ulong GeneratedTick { get; private set; }

        public string WorldKey { get; private set; }

        public string WorldRecordKey { get; private set; }

        public bool CandidateLimitReached { get; private set; }

        public bool HitLimitReached { get; private set; }

        public int TilesVisited { get; private set; }

        public string TypedTileFastPathStatus { get; private set; }

        public int CandidateChestCount { get; private set; }

        public int ScannedChestCount { get; private set; }

        public int UnreadableChestCount { get; private set; }

        public int MatchedChestCount { get; private set; }

        public int MatchedSlotCount { get; private set; }

        public int TotalStack { get; private set; }

        public IReadOnlyList<ChestItemLocatorHit> Hits { get; private set; }

        public int HitCount
        {
            get { return Hits.Count; }
        }

        public ChestItemLocatorSnapshot(
            string status,
            string refreshReason,
            long queryVersion,
            ulong generatedTick,
            string worldKey,
            string worldRecordKey,
            bool candidateLimitReached,
            bool hitLimitReached,
            int tilesVisited,
            string typedTileFastPathStatus,
            int candidateChestCount,
            int scannedChestCount,
            int unreadableChestCount,
            int matchedChestCount,
            int matchedSlotCount,
            int totalStack,
            IList<ChestItemLocatorHit> hits)
        {
            Status = string.IsNullOrWhiteSpace(status) ? StatusOk : status;
            RefreshReason = refreshReason ?? string.Empty;
            QueryVersion = queryVersion;
            GeneratedTick = generatedTick;
            WorldKey = worldKey ?? string.Empty;
            WorldRecordKey = worldRecordKey ?? string.Empty;
            CandidateLimitReached = candidateLimitReached;
            HitLimitReached = hitLimitReached;
            TilesVisited = Math.Max(0, tilesVisited);
            TypedTileFastPathStatus = typedTileFastPathStatus ?? string.Empty;
            CandidateChestCount = Math.Max(0, candidateChestCount);
            ScannedChestCount = Math.Max(0, scannedChestCount);
            UnreadableChestCount = Math.Max(0, unreadableChestCount);
            MatchedChestCount = Math.Max(0, matchedChestCount);
            MatchedSlotCount = Math.Max(0, matchedSlotCount);
            TotalStack = Math.Max(0, totalStack);

            var hitCopy = new List<ChestItemLocatorHit>();
            if (hits != null)
            {
                for (var index = 0; index < hits.Count; index++)
                {
                    var hit = hits[index];
                    if (hit != null)
                    {
                        hitCopy.Add(hit);
                    }
                }
            }

            Hits = new ReadOnlyCollection<ChestItemLocatorHit>(hitCopy);
        }

        public string BuildSummary()
        {
            return Status + ";hits=" + HitCount.ToString(CultureInfo.InvariantCulture) +
                   ";slots=" + MatchedSlotCount.ToString(CultureInfo.InvariantCulture) +
                   ";stack=" + TotalStack.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ChestItemLocatorScanOptions
    {
        public const int DefaultMaxCandidateChests = 64;
        public const int MaxCandidateChestsLimit = 128;
        public const int DefaultMaxHits = 24;
        public const int MaxHitsLimit = 64;
        public const ulong DefaultSafeRefreshTicks = 300;

        public static readonly ChestItemLocatorScanOptions Default = new ChestItemLocatorScanOptions(
            DefaultMaxCandidateChests,
            DefaultMaxHits,
            DefaultSafeRefreshTicks);

        public int MaxCandidateChests { get; private set; }

        public int MaxHits { get; private set; }

        public ulong SafeRefreshTicks { get; private set; }

        public ChestItemLocatorScanOptions(int maxCandidateChests, int maxHits, ulong safeRefreshTicks)
        {
            MaxCandidateChests = Clamp(maxCandidateChests, DefaultMaxCandidateChests, MaxCandidateChestsLimit);
            MaxHits = Clamp(maxHits, DefaultMaxHits, MaxHitsLimit);
            SafeRefreshTicks = safeRefreshTicks == 0 ? DefaultSafeRefreshTicks : safeRefreshTicks;
        }

        private static int Clamp(int value, int fallback, int max)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return value > max ? max : value;
        }
    }

    internal delegate bool ChestItemLocatorTryFindChestIndex(int chestX, int chestY, out int chestIndex);

    internal delegate bool ChestItemLocatorTryGetChest(int chestIndex, out object chest);

    internal sealed class ChestItemLocatorScanPorts
    {
        public ChestItemLocatorTryFindChestIndex TryFindChestIndex { get; set; }

        public ChestItemLocatorTryGetChest TryGetChest { get; set; }
    }
}
