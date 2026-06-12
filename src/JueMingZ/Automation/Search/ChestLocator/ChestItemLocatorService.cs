using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal static class ChestItemLocatorService
    {
        private const int CacheMovementChunkPixels = 64;

        private static readonly object SyncRoot = new object();
        private static readonly List<ChestScanCandidate> CandidateBuffer = new List<ChestScanCandidate>();
        private static readonly HashSet<long> CandidateKeys = new HashSet<long>();

        private static ChestItemLocatorSnapshot _cachedSnapshot = ChestItemLocatorSnapshot.Empty;
        private static ChestItemLocatorScanSignature _lastSignature;
        private static uint _lastSignatureHash;
        private static ulong _lastScanTick;

        public static ChestItemLocatorSnapshot GetSnapshot(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion)
        {
            return GetSnapshot(queryResult, context, queryVersion, ChestItemLocatorScanOptions.Default, null, false);
        }

        internal static ChestItemLocatorSnapshot GetSnapshotForTesting(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion,
            ChestItemLocatorScanOptions options,
            ChestItemLocatorScanPorts ports,
            bool forceRefresh)
        {
            return GetSnapshot(queryResult, context, queryVersion, options, ports, forceRefresh);
        }

        internal static string BuildCacheSignatureForTesting(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion)
        {
            return BuildSignature(queryResult, context, queryVersion).Hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        internal static bool ShouldRefreshForTesting(
            ulong lastScanTick,
            uint previousSignatureHash,
            ulong currentTick,
            uint currentSignatureHash,
            ulong safeRefreshTicks,
            out string reason)
        {
            return ShouldRefreshCore(
                lastScanTick,
                previousSignatureHash,
                currentTick,
                currentSignatureHash,
                new ChestItemLocatorScanSignature(),
                new ChestItemLocatorScanSignature { Hash = currentSignatureHash },
                safeRefreshTicks,
                out reason);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                CandidateBuffer.Clear();
                CandidateKeys.Clear();
                _cachedSnapshot = ChestItemLocatorSnapshot.Empty;
                _lastSignature = new ChestItemLocatorScanSignature();
                _lastSignatureHash = 0;
                _lastScanTick = 0;
            }
        }

        private static ChestItemLocatorSnapshot GetSnapshot(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion,
            ChestItemLocatorScanOptions options,
            ChestItemLocatorScanPorts ports,
            bool forceRefresh)
        {
            if (queryResult == null || !queryResult.Succeeded || queryResult.MatchItemTypes.Count <= 0)
            {
                return BuildEmptySnapshot(ChestItemLocatorSnapshot.StatusQueryNotReady, "queryNotReady", queryVersion, context);
            }

            if (context == null || context.MainType == null || context.LocalPlayer == null)
            {
                return BuildEmptySnapshot(ChestItemLocatorSnapshot.StatusContextUnavailable, "contextUnavailable", queryVersion, context);
            }

            options = options ?? ChestItemLocatorScanOptions.Default;
            ports = NormalizePorts(ports);

            lock (SyncRoot)
            {
                var signature = BuildSignature(queryResult, context, queryVersion);
                var reason = string.Empty;
                var refresh = forceRefresh ||
                              ShouldRefreshCore(
                                  _lastScanTick,
                                  _lastSignatureHash,
                                  context.GameUpdateCount,
                                  signature.Hash,
                                  _lastSignature,
                                  signature,
                                  options.SafeRefreshTicks,
                                  out reason);
                if (!refresh)
                {
                    return _cachedSnapshot;
                }

                if (forceRefresh)
                {
                    reason = "forced";
                }

                var snapshot = ScanNow(queryResult, context, queryVersion, options, ports, reason);
                _cachedSnapshot = snapshot;
                _lastSignature = signature;
                _lastSignatureHash = signature.Hash;
                _lastScanTick = context.GameUpdateCount;
                return snapshot;
            }
        }

        private static ChestItemLocatorSnapshot ScanNow(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion,
            ChestItemLocatorScanOptions options,
            ChestItemLocatorScanPorts ports,
            string refreshReason)
        {
            CandidateBuffer.Clear();
            CandidateKeys.Clear();

            int tilesVisited;
            int typedTileReads;
            int fallbackTileReads;
            int failedTileReads;
            bool candidateLimitReached;
            if (!CollectCandidates(context, options.MaxCandidateChests, out tilesVisited, out typedTileReads, out fallbackTileReads, out failedTileReads, out candidateLimitReached))
            {
                return BuildEmptySnapshot(ChestItemLocatorSnapshot.StatusTilesUnavailable, "tilesUnavailable", queryVersion, context);
            }

            var typedTileFastPathStatus = InformationChestTileScanner.BuildTypedTileFastPathStatus(typedTileReads, fallbackTileReads, failedTileReads);
            var matchSet = queryResult.CreateMatchSet();
            var candidateNames = BuildCandidateNameLookup(queryResult);
            var hits = new List<ChestItemLocatorHit>();
            var scannedChestCount = 0;
            var unreadableChestCount = 0;
            var matchedSlotCount = 0;
            var totalStack = 0;
            var hitLimitReached = false;
            var languageSignature = InformationChestNameResolver.BuildLanguageSignature();

            for (var index = 0; index < CandidateBuffer.Count; index++)
            {
                if (hits.Count >= options.MaxHits)
                {
                    hitLimitReached = true;
                    break;
                }

                var candidate = CandidateBuffer[index];
                int chestIndex;
                object chest;
                if (!ports.TryFindChestIndex(candidate.ChestX, candidate.ChestY, out chestIndex) ||
                    !ports.TryGetChest(chestIndex, out chest) ||
                    !IsChestRecordAt(chest, candidate.ChestX, candidate.ChestY))
                {
                    unreadableChestCount++;
                    continue;
                }

                scannedChestCount++;

                ChestItemLocatorHit hit;
                if (!TryBuildHit(context, candidate, chestIndex, chest, matchSet, candidateNames, languageSignature, out hit))
                {
                    continue;
                }

                hits.Add(hit);
                matchedSlotCount += hit.MatchedSlotCount;
                totalStack += hit.TotalStack;
            }

            return new ChestItemLocatorSnapshot(
                ChestItemLocatorSnapshot.StatusOk,
                refreshReason,
                queryVersion,
                context.GameUpdateCount,
                context.WorldKey,
                context.WorldRecordKey,
                candidateLimitReached,
                hitLimitReached,
                tilesVisited,
                typedTileFastPathStatus,
                CandidateBuffer.Count,
                scannedChestCount,
                unreadableChestCount,
                hits.Count,
                matchedSlotCount,
                totalStack,
                hits);
        }

        private static bool CollectCandidates(
            InformationWorldContext context,
            int maxCandidateChests,
            out int tilesVisited,
            out int typedTileReads,
            out int fallbackTileReads,
            out int failedTileReads,
            out bool candidateLimitReached)
        {
            tilesVisited = 0;
            typedTileReads = 0;
            fallbackTileReads = 0;
            failedTileReads = 0;
            candidateLimitReached = false;

            var tiles = InformationReflection.GetStaticMember(context.MainType, "tile");
            if (tiles == null)
            {
                return false;
            }

            int minX;
            int maxX;
            int minY;
            int maxY;
            if (!InformationChestTileScanner.TryGetScanBounds(context, tiles, out minX, out maxX, out minY, out maxY))
            {
                return false;
            }

            var allowTypedTileRead = InformationChestTileScanner.CanUseTypedTileRead(tiles);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    InformationChestTileScanner.CollectVisibleCandidate(
                        context,
                        tiles,
                        CandidateKeys,
                        CandidateBuffer,
                        x,
                        y,
                        allowTypedTileRead,
                        ref tilesVisited,
                        ref typedTileReads,
                        ref fallbackTileReads,
                        ref failedTileReads);

                    if (CandidateBuffer.Count >= maxCandidateChests)
                    {
                        candidateLimitReached = true;
                        return true;
                    }
                }
            }

            return true;
        }

        private static bool TryBuildHit(
            InformationWorldContext context,
            ChestScanCandidate candidate,
            int chestIndex,
            object chest,
            ISet<int> matchSet,
            IDictionary<int, string> candidateNames,
            string languageSignature,
            out ChestItemLocatorHit hit)
        {
            hit = null;
            object items;
            if (!TryReadChestItems(chest, out items))
            {
                return false;
            }

            var maxItems = GetItemCollectionCount(items);
            int explicitMaxItems;
            if (InformationReflection.TryReadInt(chest, "maxItems", out explicitMaxItems) && explicitMaxItems > 0)
            {
                maxItems = Math.Min(maxItems, explicitMaxItems);
            }

            if (maxItems <= 0)
            {
                return false;
            }

            var itemBuild = new Dictionary<int, ChestItemLocatorMatchedItemBuilder>();
            for (var slot = 0; slot < maxItems; slot++)
            {
                object rawItem;
                if (!TryGetIndexedObject(items, slot, out rawItem))
                {
                    continue;
                }

                int itemType;
                int stack;
                string itemName;
                if (!TryReadActiveItem(rawItem, out itemType, out stack, out itemName) ||
                    !matchSet.Contains(itemType))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(itemName))
                {
                    candidateNames.TryGetValue(itemType, out itemName);
                }

                ChestItemLocatorMatchedItemBuilder builder;
                if (!itemBuild.TryGetValue(itemType, out builder))
                {
                    builder = new ChestItemLocatorMatchedItemBuilder(itemType, itemName, slot);
                    itemBuild[itemType] = builder;
                }

                builder.Add(stack, itemName);
            }

            if (itemBuild.Count <= 0)
            {
                return false;
            }

            var matchedItems = new List<ChestItemLocatorMatchedItem>();
            var itemTypes = new List<int>(itemBuild.Keys);
            itemTypes.Sort();
            for (var index = 0; index < itemTypes.Count; index++)
            {
                matchedItems.Add(itemBuild[itemTypes[index]].Build());
            }

            var containerName = InformationChestNameResolver.ResolveNameWithCache(context, candidate, languageSignature);
            hit = new ChestItemLocatorHit(
                chestIndex,
                candidate.ChestX,
                candidate.ChestY,
                candidate.TileType,
                candidate.TileStyle,
                candidate.WorldX,
                candidate.WorldY,
                containerName,
                matchedItems);
            return hit.MatchedSlotCount > 0;
        }

        private static bool TryReadChestItems(object chest, out object items)
        {
            items = null;
            if (chest == null)
            {
                return false;
            }

            try
            {
                items = InformationReflection.GetMember(chest, "item");
                return items != null;
            }
            catch
            {
                items = null;
                return false;
            }
        }

        private static bool TryReadActiveItem(object rawItem, out int itemType, out int stack, out string itemName)
        {
            itemType = 0;
            stack = 0;
            itemName = string.Empty;
            if (rawItem == null)
            {
                return false;
            }

            var typedItem = rawItem as Item;
            if (typedItem != null)
            {
                itemType = TerrariaItemReadCompat.Type(typedItem);
                stack = TerrariaItemReadCompat.Stack(typedItem);
                itemName = TerrariaItemReadCompat.Name(typedItem);
                return itemType > 0 && stack > 0;
            }

            if (!InformationReflection.TryReadInt(rawItem, "type", out itemType) ||
                !InformationReflection.TryReadInt(rawItem, "stack", out stack))
            {
                return false;
            }

            itemName = FirstNonEmpty(
                InformationReflection.TryReadString(rawItem, "Name"),
                InformationReflection.TryReadString(rawItem, "HoverName"),
                InformationReflection.TryReadString(rawItem, "name"));
            return itemType > 0 && stack > 0;
        }

        private static Dictionary<int, string> BuildCandidateNameLookup(ChestItemLocatorQueryResult queryResult)
        {
            var result = new Dictionary<int, string>();
            if (queryResult == null)
            {
                return result;
            }

            for (var index = 0; index < queryResult.Candidates.Count; index++)
            {
                var candidate = queryResult.Candidates[index];
                if (candidate == null || candidate.ItemType <= 0)
                {
                    continue;
                }

                result[candidate.ItemType] = FirstNonEmpty(candidate.DisplayName, candidate.InternalName, candidate.IdText);
            }

            return result;
        }

        private static bool IsChestRecordAt(object chest, int chestX, int chestY)
        {
            if (chest == null)
            {
                return false;
            }

            int x;
            int y;
            return InformationReflection.TryReadInt(chest, "x", out x) &&
                   InformationReflection.TryReadInt(chest, "y", out y) &&
                   x == chestX &&
                   y == chestY;
        }

        private static int GetItemCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var array = source as Array;
            if (array != null)
            {
                return array.Rank == 1 ? array.GetLength(0) : 0;
            }

            var list = source as IList;
            return list == null ? 0 : list.Count;
        }

        private static bool TryGetIndexedObject(object source, int index, out object value)
        {
            value = null;
            if (source == null || index < 0)
            {
                return false;
            }

            var array = source as Array;
            if (array != null)
            {
                if (array.Rank != 1 || index >= array.GetLength(0))
                {
                    return false;
                }

                value = array.GetValue(index);
                return value != null;
            }

            var list = source as IList;
            if (list == null || index >= list.Count)
            {
                return false;
            }

            value = list[index];
            return value != null;
        }

        private static ChestItemLocatorScanPorts NormalizePorts(ChestItemLocatorScanPorts ports)
        {
            if (ports == null)
            {
                ports = new ChestItemLocatorScanPorts();
            }

            if (ports.TryFindChestIndex == null)
            {
                ports.TryFindChestIndex = TerrariaMainCompat.TryFindChestIndex;
            }

            if (ports.TryGetChest == null)
            {
                ports.TryGetChest = TryGetChestObject;
            }

            return ports;
        }

        private static bool TryGetChestObject(int chestIndex, out object chest)
        {
            chest = null;
            Chest typedChest;
            if (!TerrariaMainCompat.TryGetChest(chestIndex, out typedChest))
            {
                return false;
            }

            chest = typedChest;
            return true;
        }

        private static ChestItemLocatorSnapshot BuildEmptySnapshot(
            string status,
            string reason,
            long queryVersion,
            InformationWorldContext context)
        {
            return new ChestItemLocatorSnapshot(
                status,
                reason,
                queryVersion,
                context == null ? 0 : context.GameUpdateCount,
                context == null ? string.Empty : context.WorldKey,
                context == null ? string.Empty : context.WorldRecordKey,
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
        }

        private static ChestItemLocatorScanSignature BuildSignature(
            ChestItemLocatorQueryResult queryResult,
            InformationWorldContext context,
            long queryVersion)
        {
            var matchSignature = BuildMatchSignature(queryResult);
            var worldKey = context == null ? string.Empty : context.WorldKey ?? string.Empty;
            var worldRecordKey = context == null ? string.Empty : context.WorldRecordKey ?? string.Empty;
            var screenChunkX = BuildScreenChunkX(context);
            var screenChunkY = BuildScreenChunkY(context);
            var screenWidth = context == null ? 0 : Math.Max(0, context.ScreenWidth);
            var screenHeight = context == null ? 0 : Math.Max(0, context.ScreenHeight);
            var playerChunkX = BuildPlayerChunkX(context);
            var playerChunkY = BuildPlayerChunkY(context);

            unchecked
            {
                var hash = 2166136261u;
                AddHashInt(ref hash, (int)queryVersion);
                AddHashInt(ref hash, (int)(queryVersion >> 32));
                AddHashValue(ref hash, matchSignature);
                AddHashValue(ref hash, worldKey);
                AddHashValue(ref hash, worldRecordKey);
                AddHashInt(ref hash, screenChunkX);
                AddHashInt(ref hash, screenChunkY);
                AddHashInt(ref hash, screenWidth);
                AddHashInt(ref hash, screenHeight);
                AddHashInt(ref hash, playerChunkX);
                AddHashInt(ref hash, playerChunkY);
                return new ChestItemLocatorScanSignature
                {
                    Hash = hash,
                    QueryVersion = queryVersion,
                    MatchSignature = matchSignature,
                    WorldKey = worldKey,
                    WorldRecordKey = worldRecordKey,
                    ScreenChunkX = screenChunkX,
                    ScreenChunkY = screenChunkY,
                    ScreenWidth = screenWidth,
                    ScreenHeight = screenHeight,
                    PlayerChunkX = playerChunkX,
                    PlayerChunkY = playerChunkY
                };
            }
        }

        private static bool ShouldRefreshCore(
            ulong lastScanTick,
            uint previousSignatureHash,
            ulong currentTick,
            uint currentSignatureHash,
            ChestItemLocatorScanSignature previousSignature,
            ChestItemLocatorScanSignature currentSignature,
            ulong safeRefreshTicks,
            out string reason)
        {
            if (lastScanTick == 0)
            {
                reason = "initial";
                return true;
            }

            if (previousSignatureHash != currentSignatureHash)
            {
                reason = DescribeSignatureChange(previousSignature, currentSignature);
                return true;
            }

            if (currentTick == 0)
            {
                reason = "cacheHit";
                return false;
            }

            if (currentTick < lastScanTick)
            {
                reason = "tickReset";
                return true;
            }

            if (currentTick - lastScanTick >= safeRefreshTicks)
            {
                reason = "safeRefresh";
                return true;
            }

            reason = "cacheHit";
            return false;
        }

        private static string DescribeSignatureChange(
            ChestItemLocatorScanSignature previous,
            ChestItemLocatorScanSignature current)
        {
            if (previous.QueryVersion != current.QueryVersion)
            {
                return "queryVersionChanged";
            }

            if (!string.Equals(previous.MatchSignature, current.MatchSignature, StringComparison.Ordinal))
            {
                return "matchSetChanged";
            }

            if (!string.Equals(previous.WorldKey, current.WorldKey, StringComparison.Ordinal) ||
                !string.Equals(previous.WorldRecordKey, current.WorldRecordKey, StringComparison.Ordinal))
            {
                return "worldChanged";
            }

            if (previous.ScreenWidth != current.ScreenWidth ||
                previous.ScreenHeight != current.ScreenHeight)
            {
                return "screenSizeChanged";
            }

            if (previous.ScreenChunkX != current.ScreenChunkX ||
                previous.ScreenChunkY != current.ScreenChunkY)
            {
                return "screenChunkChanged";
            }

            if (previous.PlayerChunkX != current.PlayerChunkX ||
                previous.PlayerChunkY != current.PlayerChunkY)
            {
                return "playerChunkChanged";
            }

            return previous.Hash == current.Hash ? "cacheHit" : "signatureChanged";
        }

        private static string BuildMatchSignature(ChestItemLocatorQueryResult queryResult)
        {
            if (queryResult == null || queryResult.MatchItemTypes.Count <= 0)
            {
                return string.Empty;
            }

            var values = new List<int>(queryResult.MatchItemTypes);
            values.Sort();
            var parts = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                parts[index] = values[index].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }

        private static int BuildScreenChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenX), CacheMovementChunkPixels);
        }

        private static int BuildScreenChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.ScreenY), CacheMovementChunkPixels);
        }

        private static int BuildPlayerChunkX(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterX), CacheMovementChunkPixels);
        }

        private static int BuildPlayerChunkY(InformationWorldContext context)
        {
            return FloorDiv((int)Math.Floor(context == null ? 0f : context.PlayerCenterY), CacheMovementChunkPixels);
        }

        private static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0)
            {
                return 0;
            }

            if (value >= 0)
            {
                return value / divisor;
            }

            return -(((-value) + divisor - 1) / divisor);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static void AddHashValue(ref uint hash, string value)
        {
            unchecked
            {
                var text = value ?? string.Empty;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                hash ^= 31u;
                hash *= 16777619u;
            }
        }

        private static void AddHashInt(ref uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 16777619u;
                hash ^= (uint)(value >> 16);
                hash *= 16777619u;
                hash ^= 31u;
                hash *= 16777619u;
            }
        }

        private struct ChestItemLocatorScanSignature
        {
            public uint Hash;
            public long QueryVersion;
            public string MatchSignature;
            public string WorldKey;
            public string WorldRecordKey;
            public int ScreenChunkX;
            public int ScreenChunkY;
            public int ScreenWidth;
            public int ScreenHeight;
            public int PlayerChunkX;
            public int PlayerChunkY;
        }

        private sealed class ChestItemLocatorMatchedItemBuilder
        {
            private readonly int _itemType;
            private readonly int _firstSlot;
            private string _itemName;
            private int _totalStack;
            private int _slotCount;

            public ChestItemLocatorMatchedItemBuilder(int itemType, string itemName, int firstSlot)
            {
                _itemType = itemType;
                _itemName = itemName ?? string.Empty;
                _firstSlot = firstSlot;
            }

            public void Add(int stack, string itemName)
            {
                _totalStack += Math.Max(0, stack);
                _slotCount++;
                if (string.IsNullOrWhiteSpace(_itemName) && !string.IsNullOrWhiteSpace(itemName))
                {
                    _itemName = itemName.Trim();
                }
            }

            public ChestItemLocatorMatchedItem Build()
            {
                return new ChestItemLocatorMatchedItem(_itemType, _itemName, _totalStack, _slotCount, _firstSlot);
            }
        }
    }
}
