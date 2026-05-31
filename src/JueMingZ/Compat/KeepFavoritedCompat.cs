using System;
using System.Collections;
using System.Collections.Generic;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;

namespace JueMingZ.Compat
{
    internal static class KeepFavoritedCompat
    {
        private const int MaxTrackedCount = 512;
        private const long ExpireTicks = 36000;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, long> TrackedFavoritedSignatures = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> TrackedFavoritedGlobalSignatures = new Dictionary<string, long>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> TrashRoundTripSignatures = new Dictionary<string, long>(StringComparer.Ordinal);

        public static void ClearState()
        {
            lock (SyncRoot)
            {
                TrackedFavoritedSignatures.Clear();
                TrackedFavoritedGlobalSignatures.Clear();
                TrashRoundTripSignatures.Clear();
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
            slot = -1;
            itemType = 0;
            signature = string.Empty;
            message = string.Empty;

            ObserveFavorited(snapshot, tick);
            var inventory = snapshot == null || snapshot.Inventory == null ? null : snapshot.Inventory.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            var max = Math.Min(50, inventory.Count);
            for (var index = 0; index < max; index++)
            {
                var item = inventory[index];
                if (item == null || item.Type <= 0 || item.Stack <= 0 || item.Favorited)
                {
                    continue;
                }

                var currentSignature = BuildSignature(item);
                if (string.IsNullOrWhiteSpace(currentSignature) || !IsTracked(item.SlotIndex, currentSignature))
                {
                    continue;
                }

                slot = item.SlotIndex;
                itemType = item.Type;
                signature = currentSignature;
                return true;
            }

            message = "no lost favorited item in inventory";
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
            restored = false;
            message = string.Empty;
            if (player == null || slot < 0 || expectedItemType <= 0 || string.IsNullOrWhiteSpace(expectedSignature))
            {
                message = "invalid restore inputs";
                return false;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return false;
            }

            if (slot >= inventory.Count)
            {
                message = "slot outside inventory bounds";
                return false;
            }

            var item = inventory[slot];
            if (item == null)
            {
                message = "inventory slot item unavailable";
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
                message = "failed to read inventory slot item";
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
            var inventory = snapshot == null || snapshot.Inventory == null ? null : snapshot.Inventory.Items;
            if (inventory == null)
            {
                return;
            }

            var playerInventoryOpen = snapshot != null &&
                                      snapshot.Ui != null &&
                                      snapshot.Ui.PlayerInventoryOpen;
            var trashItem = snapshot == null || snapshot.Inventory == null ? null : snapshot.Inventory.TrashItem;

            lock (SyncRoot)
            {
                ObserveTrashItemLocked(trashItem, tick);

                var max = Math.Min(50, inventory.Count);
                for (var index = 0; index < max; index++)
                {
                    var item = inventory[index];
                    if (item == null || item.Type <= 0 || item.Stack <= 0)
                    {
                        continue;
                    }

                    var signature = BuildSignature(item);
                    if (string.IsNullOrWhiteSpace(signature))
                    {
                        continue;
                    }

                    var trackedKey = BuildTrackedKey(item.SlotIndex, signature);
                    if (item.Favorited)
                    {
                        TrackedFavoritedSignatures[trackedKey] = tick;
                        TrackedFavoritedGlobalSignatures[signature] = tick;
                        TrashRoundTripSignatures.Remove(signature);
                        continue;
                    }

                    if (playerInventoryOpen)
                    {
                        if (TrashRoundTripSignatures.ContainsKey(signature))
                        {
                            continue;
                        }

                        RemoveSignatureLocked(item.SlotIndex, signature);
                    }
                }

                PruneExpired(tick);
                PruneOverflow();
            }
        }

        private static bool IsTracked(int slot, string signature)
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(signature))
            {
                return false;
            }

            lock (SyncRoot)
            {
                return TrackedFavoritedSignatures.ContainsKey(BuildTrackedKey(slot, signature)) ||
                       TrackedFavoritedGlobalSignatures.ContainsKey(signature);
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

        private static void RemoveSignatureLocked(int slot, string signature)
        {
            if (string.IsNullOrWhiteSpace(signature))
            {
                return;
            }

            if (slot >= 0)
            {
                TrackedFavoritedSignatures.Remove(BuildTrackedKey(slot, signature));
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

        private static string BuildTrackedKey(int slot, string signature)
        {
            if (slot < 0 || string.IsNullOrWhiteSpace(signature))
            {
                return string.Empty;
            }

            return slot.ToString() + "|" + signature;
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
            PruneExpired(TrashRoundTripSignatures, tick);
        }

        private static void PruneOverflow()
        {
            PruneOverflow(TrackedFavoritedSignatures);
            PruneOverflow(TrackedFavoritedGlobalSignatures);
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
