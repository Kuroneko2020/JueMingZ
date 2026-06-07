using System;
using System.Collections;
using System.Collections.Generic;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;

namespace JueMingZ.Compat
{
    internal static class KeepFavoritedCompat
    {
        // Favorite restoration depends on tracked signatures; never set
        // favorited on a slot whose item identity no longer matches.
        private const int MaxTrackedCount = 512;
        private const long ExpireTicks = 36000;
        private const string InventoryContainer = "Inventory";
        private const string ArmorContainer = "Armor";
        private const string MiscEquipsContainer = "MiscEquips";
        private const string BucketTransformGroup = "bucket";
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, long> TrackedFavoritedSignatures = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> TrackedFavoritedGlobalSignatures = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> TrackedFavoritedTransformLocations = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> TrashRoundTripSignatures = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> PreviousObservedFavoritedLocations = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly List<string> ObservedFavoritedLocationKeysScratch = new List<string>(96);

        public static void ClearState()
        {
            lock (SyncRoot)
            {
                TrackedFavoritedSignatures.Clear();
                TrackedFavoritedGlobalSignatures.Clear();
                TrackedFavoritedTransformLocations.Clear();
                TrashRoundTripSignatures.Clear();
                PreviousObservedFavoritedLocations.Clear();
                ObservedFavoritedLocationKeysScratch.Clear();
            }
        }

        public static bool TryFindLostFavoritedSlot(
            GameStateSnapshot snapshot,
            long tick,
            out int slot,
            out int itemType,
            out string signature,
            out string message)
        {
            string container;
            return TryFindLostFavoritedSlot(snapshot, tick, out container, out slot, out itemType, out signature, out message);
        }

        public static bool TryFindLostFavoritedSlot(
            GameStateSnapshot snapshot,
            long tick,
            out string container,
            out int slot,
            out int itemType,
            out string signature,
            out string message)
        {
            container = InventoryContainer;
            slot = -1;
            itemType = 0;
            signature = string.Empty;
            message = string.Empty;

            ObserveFavorited(snapshot, tick);
            var inventorySnapshot = snapshot == null ? null : snapshot.Inventory;
            var inventory = inventorySnapshot == null ? null : inventorySnapshot.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                message = "inventory snapshot unavailable";
            }
            else if (TryFindLostInContainer(InventoryContainer, inventory, 50, out container, out slot, out itemType, out signature))
            {
                return true;
            }

            if (inventorySnapshot != null &&
                TryFindLostInContainer(ArmorContainer, inventorySnapshot.ArmorItems, int.MaxValue, out container, out slot, out itemType, out signature))
            {
                return true;
            }

            if (inventorySnapshot != null &&
                TryFindLostInContainer(MiscEquipsContainer, inventorySnapshot.MiscEquipItems, int.MaxValue, out container, out slot, out itemType, out signature))
            {
                return true;
            }

            message = string.IsNullOrWhiteSpace(message) ? "no lost favorited item in tracked containers" : message;
            return false;
        }

        private static bool TryFindLostInContainer(
            string containerName,
            IReadOnlyList<InventoryItemSnapshot> items,
            int maxSlots,
            out string container,
            out int slot,
            out int itemType,
            out string signature)
        {
            container = NormalizeContainer(containerName);
            slot = -1;
            itemType = 0;
            signature = string.Empty;
            if (items == null || items.Count <= 0)
            {
                return false;
            }

            var max = maxSlots == int.MaxValue ? items.Count : Math.Min(maxSlots, items.Count);
            for (var index = 0; index < max; index++)
            {
                var item = items[index];
                if (item == null || item.Type <= 0 || item.Stack <= 0 || item.Favorited)
                {
                    continue;
                }

                var currentSignature = BuildSignature(item);
                if (string.IsNullOrWhiteSpace(currentSignature) || !IsTracked(container, item.SlotIndex, currentSignature, item.Type))
                {
                    continue;
                }

                slot = item.SlotIndex;
                itemType = item.Type;
                signature = currentSignature;
                return true;
            }

            return false;
        }

        public static bool TryRestoreFavoritedInInventory(
            object player,
            int slot,
            int expectedItemType,
            string expectedSignature,
            out bool restored,
            out string message)
        {
            return TryRestoreFavoritedInContainer(player, InventoryContainer, slot, expectedItemType, expectedSignature, out restored, out message);
        }

        public static bool TryRestoreFavoritedInContainer(
            object player,
            string containerName,
            int slot,
            int expectedItemType,
            string expectedSignature,
            out bool restored,
            out string message)
        {
            restored = false;
            message = string.Empty;
            var container = NormalizeContainer(containerName);
            if (player == null || slot < 0 || expectedItemType <= 0 || string.IsNullOrWhiteSpace(expectedSignature))
            {
                message = "invalid restore inputs";
                return false;
            }

            IList items;
            if (!TryGetContainerItems(player, container, out items, out message) || items == null)
            {
                return false;
            }

            if (slot >= items.Count)
            {
                message = "slot outside container bounds";
                return false;
            }

            var item = items[slot];
            if (item == null)
            {
                message = "slot item unavailable";
                return false;
            }

            int itemType;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            string itemName;
            if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
            {
                message = "failed to read slot item";
                return false;
            }

            if (itemType <= 0 || stack <= 0 || itemType != expectedItemType)
            {
                message = "slot item changed before restore";
                return false;
            }

            var currentSignature = BuildSignature(itemType, ReadPrefix(item), itemName);
            if (!string.Equals(currentSignature, expectedSignature, StringComparison.Ordinal))
            {
                message = "slot item signature changed before restore";
                return false;
            }

            if (ReadFavorited(item))
            {
                message = "slot item already favorited";
                return true;
            }

            if (!SetFavorited(item, true))
            {
                message = "failed to set slot item favorited";
                return false;
            }

            restored = true;
            return true;
        }

        private static void ObserveFavorited(GameStateSnapshot snapshot, long tick)
        {
            var inventorySnapshot = snapshot == null ? null : snapshot.Inventory;
            if (inventorySnapshot == null)
            {
                return;
            }

            var playerInventoryOpen = snapshot != null &&
                                      snapshot.Ui != null &&
                                      snapshot.Ui.PlayerInventoryOpen;
            var trashItem = inventorySnapshot.TrashItem;

            lock (SyncRoot)
            {
                ObservedFavoritedLocationKeysScratch.Clear();
                ObserveTrashItemLocked(trashItem, tick);
                ObserveContainerLocked(InventoryContainer, inventorySnapshot.Items, 50, playerInventoryOpen, true, tick);
                ObserveContainerLocked(ArmorContainer, inventorySnapshot.ArmorItems, int.MaxValue, playerInventoryOpen, false, tick);
                ObserveContainerLocked(MiscEquipsContainer, inventorySnapshot.MiscEquipItems, int.MaxValue, playerInventoryOpen, false, tick);
                ReplacePreviousObservedFavoritedLocationsLocked(tick);
                PruneExpired(tick);
                PruneOverflow();
            }
        }

        private static void ObserveContainerLocked(
            string containerName,
            IReadOnlyList<InventoryItemSnapshot> items,
            int maxSlots,
            bool playerInventoryOpen,
            bool allowManualUnfavorite,
            long tick)
        {
            if (items == null || items.Count <= 0)
            {
                return;
            }

            var container = NormalizeContainer(containerName);
            var max = maxSlots == int.MaxValue ? items.Count : Math.Min(maxSlots, items.Count);
            for (var index = 0; index < max; index++)
            {
                var item = items[index];
                if (item == null || item.Type <= 0 || item.Stack <= 0)
                {
                    continue;
                }

                var signature = BuildSignature(item);
                if (string.IsNullOrWhiteSpace(signature))
                {
                    continue;
                }

                var trackedKey = BuildTrackedKey(container, item.SlotIndex, signature);
                if (item.Favorited)
                {
                    TrackedFavoritedSignatures[trackedKey] = tick;
                    TrackedFavoritedGlobalSignatures[signature] = tick;
                    TrackTransformLocationLocked(container, item.SlotIndex, item.Type, tick);
                    TrashRoundTripSignatures.Remove(signature);
                    ObservedFavoritedLocationKeysScratch.Add(trackedKey);
                    continue;
                }

                if (allowManualUnfavorite &&
                    playerInventoryOpen &&
                    PreviousObservedFavoritedLocations.ContainsKey(trackedKey))
                {
                    if (TrashRoundTripSignatures.ContainsKey(signature))
                    {
                        continue;
                    }

                    RemoveSignatureLocked(container, item.SlotIndex, signature);
                }
            }
        }

        private static void ReplacePreviousObservedFavoritedLocationsLocked(long tick)
        {
            PreviousObservedFavoritedLocations.Clear();
            for (var index = 0; index < ObservedFavoritedLocationKeysScratch.Count; index++)
            {
                var key = ObservedFavoritedLocationKeysScratch[index];
                if (!string.IsNullOrWhiteSpace(key))
                {
                    PreviousObservedFavoritedLocations[key] = tick;
                }
            }

            ObservedFavoritedLocationKeysScratch.Clear();
        }

        private static bool IsTracked(string containerName, int slot, string signature, int itemType)
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            var container = NormalizeContainer(containerName);
            lock (SyncRoot)
            {
                return TrackedFavoritedSignatures.ContainsKey(BuildTrackedKey(container, slot, signature)) ||
                       TrackedFavoritedGlobalSignatures.ContainsKey(signature) ||
                       TrackedFavoritedTransformLocations.ContainsKey(BuildTransformKey(container, slot, BuildTransformGroup(itemType)));
            }
        }

        private static void ObserveTrashItemLocked(InventoryItemSnapshot item, long tick)
        {
            if (item == null || item.Type <= 0 || item.Stack <= 0)
            {
                return;
            }

            var signature = BuildSignature(item);
            if (string.IsNullOrWhiteSpace(signature))
            {
                return;
            }

            if (item.Favorited)
            {
                TrackedFavoritedGlobalSignatures[signature] = tick;
                TrashRoundTripSignatures[signature] = tick;
                return;
            }

            if (TrackedFavoritedGlobalSignatures.ContainsKey(signature) || HasSlotTrackedSignatureLocked(signature))
            {
                TrashRoundTripSignatures[signature] = tick;
            }
        }

        private static bool HasSlotTrackedSignatureLocked(string signature)
        {
            if (string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            foreach (var entry in TrackedFavoritedSignatures)
            {
                if (entry.Key.EndsWith("|" + signature, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveSignatureLocked(string containerName, int slot, string signature)
        {
            if (string.IsNullOrWhiteSpace(signature))
            {
                return;
            }

            var container = NormalizeContainer(containerName);
            if (slot >= 0)
            {
                TrackedFavoritedSignatures.Remove(BuildTrackedKey(container, slot, signature));
                int itemType;
                if (TryParseSignatureType(signature, out itemType))
                {
                    TrackedFavoritedTransformLocations.Remove(BuildTransformKey(container, slot, BuildTransformGroup(itemType)));
                }
            }

            TrackedFavoritedGlobalSignatures.Remove(signature);
            TrashRoundTripSignatures.Remove(signature);
        }

        private static string BuildSignature(InventoryItemSnapshot item)
        {
            if (item == null || item.Type <= 0)
            {
                return string.Empty;
            }

            return BuildSignature(item.Type, item.Prefix, item.Name);
        }

        private static string BuildSignature(int type, int prefix, string name)
        {
            if (type <= 0)
            {
                return string.Empty;
            }

            return type.ToString() + "|" + prefix.ToString() + "|" + (name ?? string.Empty);
        }

        private static string BuildTrackedKey(string containerName, int slot, string signature)
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(signature))
            {
                return string.Empty;
            }

            return NormalizeContainer(containerName) + "|" + slot.ToString() + "|" + signature;
        }

        private static string BuildTransformKey(string containerName, int slot, string transformGroup)
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(transformGroup))
            {
                return string.Empty;
            }

            return NormalizeContainer(containerName) + "|" + slot.ToString() + "|" + transformGroup;
        }

        private static void TrackTransformLocationLocked(string containerName, int slot, int itemType, long tick)
        {
            var transformGroup = BuildTransformGroup(itemType);
            if (slot < 0 || string.IsNullOrWhiteSpace(transformGroup))
            {
                return;
            }

            TrackedFavoritedTransformLocations[BuildTransformKey(containerName, slot, transformGroup)] = tick;
        }

        private static string BuildTransformGroup(int itemType)
        {
            switch (itemType)
            {
                case 205:
                case 206:
                case 207:
                case 1128:
                    return BucketTransformGroup;
                default:
                    return string.Empty;
            }
        }

        private static bool TryParseSignatureType(string signature, out int itemType)
        {
            itemType = 0;
            if (string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            var separator = signature.IndexOf('|');
            var raw = separator < 0 ? signature : signature.Substring(0, separator);
            return int.TryParse(raw, out itemType);
        }

        private static string NormalizeContainer(string containerName)
        {
            if (string.Equals(containerName, ArmorContainer, StringComparison.OrdinalIgnoreCase))
            {
                return ArmorContainer;
            }

            if (string.Equals(containerName, MiscEquipsContainer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(containerName, "MiscEquip", StringComparison.OrdinalIgnoreCase))
            {
                return MiscEquipsContainer;
            }

            return InventoryContainer;
        }

        private static bool TryGetContainerItems(object player, string containerName, out IList items, out string message)
        {
            items = null;
            message = string.Empty;
            var container = NormalizeContainer(containerName);
            if (string.Equals(container, InventoryContainer, StringComparison.Ordinal))
            {
                return InventoryMutationCompat.TryGetContainerItems(player, InventoryContainer, out items, out message);
            }

            var memberName = string.Equals(container, ArmorContainer, StringComparison.Ordinal)
                ? "armor"
                : "miscEquips";
            items = GetMember(player, memberName) as IList;
            if (items == null)
            {
                message = container + " item list is unavailable.";
                return false;
            }

            return true;
        }

        private static int ReadPrefix(object item)
        {
            if (item == null)
            {
                return 0;
            }

            var raw = GetMember(item, "prefix");
            if (raw == null)
            {
                return 0;
            }

            try
            {
                return Convert.ToInt32(raw);
            }
            catch
            {
                return 0;
            }
        }

        private static bool ReadFavorited(object item)
        {
            if (item == null)
            {
                return false;
            }

            if (TryReadBool(item, "favorited", out var value))
            {
                return value;
            }

            return TryReadBool(item, "favorite", out value) && value;
        }

        private static bool SetFavorited(object item, bool favorited)
        {
            return TrySetBool(item, "favorited", favorited) || TrySetBool(item, "favorite", favorited);
        }

        private static bool TryReadBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TrySetBool(object instance, string name, bool value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var type = instance.GetType();
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var type = instance.GetType();
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    return field.GetValue(instance);
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property))
                {
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static void PruneExpired(long tick)
        {
            PruneExpired(TrackedFavoritedSignatures, tick);
            PruneExpired(TrackedFavoritedGlobalSignatures, tick);
            PruneExpired(TrackedFavoritedTransformLocations, tick);
            PruneExpired(TrashRoundTripSignatures, tick);
        }

        private static void PruneOverflow()
        {
            PruneOverflow(TrackedFavoritedSignatures);
            PruneOverflow(TrackedFavoritedGlobalSignatures);
            PruneOverflow(TrackedFavoritedTransformLocations);
            PruneOverflow(TrashRoundTripSignatures);
        }

        private static void PruneExpired(Dictionary<string, long> values, long tick)
        {
            if (values == null || values.Count <= 0)
            {
                return;
            }

            var keys = new List<string>();
            foreach (var entry in values)
            {
                if (tick <= 0 || entry.Value <= 0 || tick < entry.Value || tick - entry.Value > ExpireTicks)
                {
                    keys.Add(entry.Key);
                }
            }

            for (var index = 0; index < keys.Count; index++)
            {
                values.Remove(keys[index]);
            }
        }

        private static void PruneOverflow(Dictionary<string, long> values)
        {
            if (values == null || values.Count <= MaxTrackedCount)
            {
                return;
            }

            var entries = new List<KeyValuePair<string, long>>(values);
            entries.Sort((left, right) => left.Value.CompareTo(right.Value));
            var removeCount = values.Count - MaxTrackedCount;
            for (var index = 0; index < removeCount && index < entries.Count; index++)
            {
                values.Remove(entries[index].Key);
            }
        }
    }
}
