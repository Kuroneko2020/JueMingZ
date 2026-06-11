using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementPlacementNameCache
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<long, int> _tileItemsByKey = new Dictionary<long, int>();
        private static Dictionary<int, int> _wallItemsByType = new Dictionary<int, int>();

        public static bool TryResolveTileItem(int tileType, int tileStyle, out int itemId)
        {
            itemId = 0;
            if (tileType < 0)
            {
                return false;
            }

            EnsureInitialized();
            lock (SyncRoot)
            {
                return _tileItemsByKey.TryGetValue(BuildTileKey(tileType, tileStyle), out itemId) && itemId > 0;
            }
        }

        public static bool TryResolveWallItem(int wallType, out int itemId)
        {
            itemId = 0;
            if (wallType <= 0)
            {
                return false;
            }

            EnsureInitialized();
            lock (SyncRoot)
            {
                return _wallItemsByType.TryGetValue(wallType, out itemId) && itemId > 0;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
                _tileItemsByKey = new Dictionary<long, int>();
                _wallItemsByType = new Dictionary<int, int>();
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                var tileItemsByKey = new Dictionary<long, int>();
                var wallItemsByType = new Dictionary<int, int>();
                try
                {
                    BuildTileLookupFromDerivedPlacementDetails(tileItemsByKey);
                    BuildLookupFromContentSamples(tileItemsByKey, wallItemsByType);
                }
                catch
                {
                    // Placement names are a quality layer; fail closed so the existing map-object path stays reliable.
                    tileItemsByKey.Clear();
                    wallItemsByType.Clear();
                }

                _tileItemsByKey = tileItemsByKey;
                _wallItemsByType = wallItemsByType;
                _initialized = true;
            }
        }

        private static void BuildTileLookupFromDerivedPlacementDetails(Dictionary<long, int> tileItemsByKey)
        {
            var itemSetsType = InformationReflection.FindType("Terraria.ID.ItemID+Sets");
            var details = InformationReflection.GetStaticMember(itemSetsType, "DerivedPlacementDetails");
            var count = GetCollectionCount(details);
            for (var itemId = 1; itemId < count; itemId++)
            {
                var detail = InformationReflection.GetIndexedValue(details, itemId);
                if (detail == null)
                {
                    continue;
                }

                int tileType;
                int tileStyle;
                if (InformationReflection.TryReadInt(detail, "tileType", out tileType) &&
                    InformationReflection.TryReadInt(detail, "tileStyle", out tileStyle))
                {
                    AddTileItem(tileItemsByKey, tileType, tileStyle, itemId);
                }
            }
        }

        private static void BuildLookupFromContentSamples(
            Dictionary<long, int> tileItemsByKey,
            Dictionary<int, int> wallItemsByType)
        {
            var contentSamplesType = InformationReflection.FindType("Terraria.ID.ContentSamples");
            var itemsByType = InformationReflection.GetStaticMember(contentSamplesType, "ItemsByType");
            var samples = ReadContentSamples(itemsByType);
            samples.Sort((left, right) => left.ItemId.CompareTo(right.ItemId));

            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                int tileType;
                if (InformationReflection.TryReadInt(sample.Item, "createTile", out tileType))
                {
                    var tileStyle = 0;
                    InformationReflection.TryReadInt(sample.Item, "placeStyle", out tileStyle);
                    AddTileItem(tileItemsByKey, tileType, tileStyle, sample.ItemId);
                }

                int wallType;
                if (InformationReflection.TryReadInt(sample.Item, "createWall", out wallType))
                {
                    AddWallItem(wallItemsByType, wallType, sample.ItemId);
                }
            }
        }

        private static List<PlacementSample> ReadContentSamples(object itemsByType)
        {
            var result = new List<PlacementSample>();
            var enumerable = itemsByType as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (var entry in enumerable)
            {
                object key;
                object value;
                if (!TryReadDictionaryEntry(entry, out key, out value) || value == null)
                {
                    continue;
                }

                int itemId;
                if (!TryConvertInt(key, out itemId))
                {
                    InformationReflection.TryReadInt(value, "type", out itemId);
                }

                if (itemId > 0)
                {
                    result.Add(new PlacementSample(itemId, value));
                }
            }

            return result;
        }

        private static bool TryReadDictionaryEntry(object entry, out object key, out object value)
        {
            key = null;
            value = null;
            if (entry == null)
            {
                return false;
            }

            if (entry is DictionaryEntry)
            {
                var dictionaryEntry = (DictionaryEntry)entry;
                key = dictionaryEntry.Key;
                value = dictionaryEntry.Value;
                return true;
            }

            key = InformationReflection.GetMember(entry, "Key");
            value = InformationReflection.GetMember(entry, "Value");
            return key != null || value != null;
        }

        private static void AddTileItem(Dictionary<long, int> tileItemsByKey, int tileType, int tileStyle, int itemId)
        {
            if (tileItemsByKey == null || tileType < 0 || itemId <= 0)
            {
                return;
            }

            var key = BuildTileKey(tileType, tileStyle);
            if (!tileItemsByKey.ContainsKey(key))
            {
                tileItemsByKey.Add(key, itemId);
            }
        }

        private static void AddWallItem(Dictionary<int, int> wallItemsByType, int wallType, int itemId)
        {
            if (wallItemsByType == null || wallType <= 0 || itemId <= 0)
            {
                return;
            }

            if (!wallItemsByType.ContainsKey(wallType))
            {
                wallItemsByType.Add(wallType, itemId);
            }
        }

        private static long BuildTileKey(int tileType, int tileStyle)
        {
            return ((long)tileType << 32) ^ (uint)Math.Max(0, tileStyle);
        }

        private static int GetCollectionCount(object source)
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

            var collection = source as ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static bool TryConvertInt(object raw, out int value)
        {
            value = 0;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private sealed class PlacementSample
        {
            public PlacementSample(int itemId, object item)
            {
                ItemId = itemId;
                Item = item;
            }

            public int ItemId { get; private set; }
            public object Item { get; private set; }
        }
    }
}
