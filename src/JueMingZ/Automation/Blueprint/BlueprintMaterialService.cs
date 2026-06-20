using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintMaterialItemSnapshot
    {
        public BlueprintMaterialItemSnapshot()
        {
            DisplayName = string.Empty;
        }

        public int ItemId { get; set; }
        public string DisplayName { get; set; }
        public int RequiredStack { get; set; }
        public int AvailableStack { get; set; }
        public int MissingStack { get; set; }
        public int MainInventoryStack { get; set; }
        public int VoidBagStack { get; set; }
        public int MissingLayerCount { get; set; }

        public BlueprintMaterialItemSnapshot Clone()
        {
            return new BlueprintMaterialItemSnapshot
            {
                ItemId = ItemId,
                DisplayName = DisplayName ?? string.Empty,
                RequiredStack = RequiredStack,
                AvailableStack = AvailableStack,
                MissingStack = MissingStack,
                MainInventoryStack = MainInventoryStack,
                VoidBagStack = VoidBagStack,
                MissingLayerCount = MissingLayerCount
            };
        }
    }

    internal sealed class BlueprintMaterialSnapshot
    {
        public BlueprintMaterialSnapshot()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            ProjectionResultCode = string.Empty;
            InventoryReadStatus = string.Empty;
            InventoryReadMessage = string.Empty;
            Signature = string.Empty;
            Items = new List<BlueprintMaterialItemSnapshot>();
            LastResolvedUtc = DateTime.UtcNow;
        }

        public bool LoadSucceeded { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string ProjectionResultCode { get; set; }
        public bool InventoryReadSucceeded { get; set; }
        public string InventoryReadStatus { get; set; }
        public string InventoryReadMessage { get; set; }
        public int RequiredItemCount { get; set; }
        public int MissingItemCount { get; set; }
        public int RequiredStackTotal { get; set; }
        public int AvailableStackTotal { get; set; }
        public int MissingStackTotal { get; set; }
        public int ProjectionMissingLayerCount { get; set; }
        public int MaterializedMissingLayerCount { get; set; }
        public int SkippedFulfilledLayerCount { get; set; }
        public int SkippedConflictLayerCount { get; set; }
        public int SkippedUnavailableLayerCount { get; set; }
        public int SkippedMissingLayerWithoutMaterialCount { get; set; }
        public int InventoryMainStackTotal { get; set; }
        public int InventoryVoidBagStackTotal { get; set; }
        public int CacheHitCount { get; set; }
        public int CacheMissCount { get; set; }
        public double LastResolveElapsedMs { get; set; }
        public DateTime? LastResolvedUtc { get; set; }
        public bool WindowVisible { get; set; }
        public int WindowOpacityPercent { get; set; }
        public string Signature { get; set; }
        public IReadOnlyList<BlueprintMaterialItemSnapshot> Items { get; set; }

        public BlueprintMaterialSnapshot Clone()
        {
            return Clone(true);
        }

        public BlueprintMaterialSnapshot CloneSummary()
        {
            return Clone(false);
        }

        private BlueprintMaterialSnapshot Clone(bool includeItems)
        {
            return new BlueprintMaterialSnapshot
            {
                LoadSucceeded = LoadSucceeded,
                ResultCode = ResultCode ?? string.Empty,
                Message = Message ?? string.Empty,
                WorldPairKey = WorldPairKey ?? string.Empty,
                WorldKey = WorldKey ?? string.Empty,
                ProjectionResultCode = ProjectionResultCode ?? string.Empty,
                InventoryReadSucceeded = InventoryReadSucceeded,
                InventoryReadStatus = InventoryReadStatus ?? string.Empty,
                InventoryReadMessage = InventoryReadMessage ?? string.Empty,
                RequiredItemCount = RequiredItemCount,
                MissingItemCount = MissingItemCount,
                RequiredStackTotal = RequiredStackTotal,
                AvailableStackTotal = AvailableStackTotal,
                MissingStackTotal = MissingStackTotal,
                ProjectionMissingLayerCount = ProjectionMissingLayerCount,
                MaterializedMissingLayerCount = MaterializedMissingLayerCount,
                SkippedFulfilledLayerCount = SkippedFulfilledLayerCount,
                SkippedConflictLayerCount = SkippedConflictLayerCount,
                SkippedUnavailableLayerCount = SkippedUnavailableLayerCount,
                SkippedMissingLayerWithoutMaterialCount = SkippedMissingLayerWithoutMaterialCount,
                InventoryMainStackTotal = InventoryMainStackTotal,
                InventoryVoidBagStackTotal = InventoryVoidBagStackTotal,
                CacheHitCount = CacheHitCount,
                CacheMissCount = CacheMissCount,
                LastResolveElapsedMs = LastResolveElapsedMs,
                LastResolvedUtc = LastResolvedUtc,
                WindowVisible = WindowVisible,
                WindowOpacityPercent = WindowOpacityPercent,
                Signature = Signature ?? string.Empty,
                Items = includeItems ? CloneItems(Items) : new List<BlueprintMaterialItemSnapshot>()
            };
        }

        private static IReadOnlyList<BlueprintMaterialItemSnapshot> CloneItems(IReadOnlyList<BlueprintMaterialItemSnapshot> source)
        {
            var clone = new List<BlueprintMaterialItemSnapshot>();
            for (var index = 0; source != null && index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    clone.Add(source[index].Clone());
                }
            }

            return clone;
        }
    }

    internal sealed class BlueprintMaterialInventorySnapshot
    {
        private readonly Dictionary<int, int> _mainStacks = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _voidBagStacks = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _displayNames = new Dictionary<int, string>();

        public BlueprintMaterialInventorySnapshot()
        {
            Status = string.Empty;
            Message = string.Empty;
        }

        public bool Succeeded { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public int MainStackTotal { get; set; }
        public int VoidBagStackTotal { get; set; }

        public void AddMainStack(int itemId, int stack, string displayName)
        {
            AddStack(_mainStacks, itemId, stack);
            MainStackTotal += Math.Max(0, stack);
            AddDisplayName(itemId, displayName);
        }

        public void AddVoidBagStack(int itemId, int stack, string displayName)
        {
            AddStack(_voidBagStacks, itemId, stack);
            VoidBagStackTotal += Math.Max(0, stack);
            AddDisplayName(itemId, displayName);
        }

        public int GetMainStack(int itemId)
        {
            return GetStack(_mainStacks, itemId);
        }

        public int GetVoidBagStack(int itemId)
        {
            return GetStack(_voidBagStacks, itemId);
        }

        public int GetAvailableStack(int itemId)
        {
            return GetMainStack(itemId) + GetVoidBagStack(itemId);
        }

        public string GetDisplayName(int itemId)
        {
            string name;
            return _displayNames.TryGetValue(itemId, out name) ? name ?? string.Empty : string.Empty;
        }

        private void AddDisplayName(int itemId, string displayName)
        {
            if (itemId <= 0 || string.IsNullOrWhiteSpace(displayName) || _displayNames.ContainsKey(itemId))
            {
                return;
            }

            _displayNames[itemId] = displayName.Trim();
        }

        private static void AddStack(IDictionary<int, int> stacks, int itemId, int stack)
        {
            if (stacks == null || itemId <= 0 || stack <= 0)
            {
                return;
            }

            int old;
            stacks.TryGetValue(itemId, out old);
            stacks[itemId] = old + stack;
        }

        private static int GetStack(IDictionary<int, int> stacks, int itemId)
        {
            if (stacks == null || itemId <= 0)
            {
                return 0;
            }

            int value;
            return stacks.TryGetValue(itemId, out value) ? value : 0;
        }
    }

    internal interface IBlueprintMaterialInventoryReader
    {
        bool TryReadStacks(IReadOnlyCollection<int> requiredItemIds, out BlueprintMaterialInventorySnapshot snapshot, out string message);
    }

    internal sealed class BlueprintTerrariaMaterialInventoryReader : IBlueprintMaterialInventoryReader
    {
        private const int MainInventorySlotLimit = 58;

        public bool TryReadStacks(IReadOnlyCollection<int> requiredItemIds, out BlueprintMaterialInventorySnapshot snapshot, out string message)
        {
            snapshot = new BlueprintMaterialInventorySnapshot();
            message = string.Empty;
            var player = TerrariaMainCompat.LocalPlayer;
            if (player == null)
            {
                snapshot.Status = "playerUnavailable";
                snapshot.Message = "Local player unavailable.";
                message = snapshot.Message;
                return false;
            }

            var required = BuildRequiredSet(requiredItemIds);
            string mainMessage;
            IList mainItems;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out mainItems, out mainMessage))
            {
                snapshot.Status = "inventoryUnavailable";
                snapshot.Message = mainMessage;
                message = mainMessage;
                return false;
            }

            ReadContainerStacks(mainItems, MainInventorySlotLimit, required, snapshot.AddMainStack);

            string voidBagMessage;
            IList voidBagItems;
            if (InventoryMutationCompat.TryGetContainerItems(player, "VoidBag", out voidBagItems, out voidBagMessage))
            {
                ReadContainerStacks(voidBagItems, int.MaxValue, required, snapshot.AddVoidBagStack);
                snapshot.Status = "read";
                snapshot.Message = "Main inventory and void bag read.";
            }
            else
            {
                snapshot.Status = "readWithoutVoidBag";
                snapshot.Message = string.IsNullOrWhiteSpace(voidBagMessage)
                    ? "Main inventory read; void bag unavailable."
                    : "Main inventory read; void bag skipped: " + voidBagMessage;
            }

            snapshot.Succeeded = true;
            message = snapshot.Message;
            return true;
        }

        private static HashSet<int> BuildRequiredSet(IReadOnlyCollection<int> requiredItemIds)
        {
            var set = new HashSet<int>();
            if (requiredItemIds == null)
            {
                return set;
            }

            foreach (var itemId in requiredItemIds)
            {
                if (itemId > 0)
                {
                    set.Add(itemId);
                }
            }

            return set;
        }

        private static void ReadContainerStacks(
            IList items,
            int slotLimit,
            ISet<int> requiredItemIds,
            Action<int, int, string> addStack)
        {
            if (items == null || addStack == null || requiredItemIds == null || requiredItemIds.Count <= 0)
            {
                return;
            }

            var limit = Math.Min(items.Count, Math.Max(0, slotLimit));
            for (var index = 0; index < limit; index++)
            {
                var item = items[index];
                int itemType;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                string itemName;
                if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                    itemType <= 0 ||
                    stack <= 0 ||
                    !requiredItemIds.Contains(itemType))
                {
                    continue;
                }

                addStack(itemType, stack, itemName);
            }
        }
    }

    internal static class BlueprintMaterialService
    {
        private const int CacheCadenceMs = 250;
        private static readonly object SyncRoot = new object();
        private static IBlueprintMaterialInventoryReader _testingInventoryReader;
        private static BlueprintMaterialSnapshot _lastSnapshot;
        private static string _lastSignature = string.Empty;
        private static DateTime _lastResolveUtc = DateTime.MinValue;
        private static int _cacheHitCount;
        private static int _cacheMissCount;

        public static BlueprintMaterialSnapshot GetSnapshot()
        {
            var projection = BlueprintProjectionService.GetSnapshot();
            lock (SyncRoot)
            {
                return ResolveSnapshotLocked(projection, false).Clone();
            }
        }

        public static BlueprintMaterialSnapshot GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return CloneLastSnapshotSummaryLocked();
            }
        }

        internal static BlueprintMaterialSnapshot GetCachedSnapshotForDraw()
        {
            lock (SyncRoot)
            {
                // Draw and input hit-test paths must not refresh projection or inventory;
                // the material window is refreshed explicitly when it opens.
                return _lastSnapshot;
            }
        }

        internal static BlueprintMaterialSnapshot ForceRefreshForMaterialWindow()
        {
            var projection = BlueprintProjectionService.GetSnapshot();
            lock (SyncRoot)
            {
                return ResolveSnapshotLocked(projection, true).Clone();
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetDiagnostics();
            var builder = new StringBuilder();
            builder.Append('{');
            builder.Append("\"loadSucceeded\":").Append(BoolRaw(snapshot.LoadSucceeded)).Append(',');
            builder.Append("\"resultCode\":\"").Append(EscapeJson(snapshot.ResultCode)).Append("\",");
            builder.Append("\"worldPairKey\":\"").Append(EscapeJson(snapshot.WorldPairKey)).Append("\",");
            builder.Append("\"worldKey\":\"").Append(EscapeJson(snapshot.WorldKey)).Append("\",");
            builder.Append("\"requiredItemCount\":").Append(snapshot.RequiredItemCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"missingItemCount\":").Append(snapshot.MissingItemCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"requiredStackTotal\":").Append(snapshot.RequiredStackTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"availableStackTotal\":").Append(snapshot.AvailableStackTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"missingStackTotal\":").Append(snapshot.MissingStackTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"inventoryReadSucceeded\":").Append(BoolRaw(snapshot.InventoryReadSucceeded)).Append(',');
            builder.Append("\"inventoryReadStatus\":\"").Append(EscapeJson(snapshot.InventoryReadStatus)).Append("\",");
            builder.Append("\"projectionMissingLayerCount\":").Append(snapshot.ProjectionMissingLayerCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"materializedMissingLayerCount\":").Append(snapshot.MaterializedMissingLayerCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"cacheHitCount\":").Append(snapshot.CacheHitCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"cacheMissCount\":").Append(snapshot.CacheMissCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"windowVisible\":").Append(BoolRaw(snapshot.WindowVisible)).Append(',');
            builder.Append("\"windowOpacityPercent\":").Append(snapshot.WindowOpacityPercent.ToString(CultureInfo.InvariantCulture)).Append(',');
            builder.Append("\"items\":[");
            var max = Math.Min(snapshot.Items == null ? 0 : snapshot.Items.Count, 12);
            for (var index = 0; index < max; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                var item = snapshot.Items[index];
                builder.Append('{');
                builder.Append("\"itemId\":").Append(item.ItemId.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append("\"name\":\"").Append(EscapeJson(item.DisplayName)).Append("\",");
                builder.Append("\"required\":").Append(item.RequiredStack.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append("\"available\":").Append(item.AvailableStack.ToString(CultureInfo.InvariantCulture)).Append(',');
                builder.Append("\"missing\":").Append(item.MissingStack.ToString(CultureInfo.InvariantCulture));
                builder.Append('}');
            }

            builder.Append("]}");
            return builder.ToString();
        }

        public static int BuildStateSignature()
        {
            var snapshot = GetDiagnostics();
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.ResultCode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(snapshot.WorldPairKey ?? string.Empty);
                hash = hash * 31 + snapshot.RequiredItemCount;
                hash = hash * 31 + snapshot.MissingItemCount;
                hash = hash * 31 + snapshot.RequiredStackTotal;
                hash = hash * 31 + snapshot.AvailableStackTotal;
                hash = hash * 31 + snapshot.MissingStackTotal;
                hash = hash * 31 + snapshot.ProjectionMissingLayerCount;
                hash = hash * 31 + snapshot.MaterializedMissingLayerCount;
                hash = hash * 31 + snapshot.SkippedFulfilledLayerCount;
                hash = hash * 31 + snapshot.SkippedConflictLayerCount;
                hash = hash * 31 + snapshot.SkippedUnavailableLayerCount;
                hash = hash * 31 + (snapshot.WindowVisible ? 1 : 0);
                hash = hash * 31 + snapshot.WindowOpacityPercent;
                return hash;
            }
        }

        internal static void SetInventoryReaderForTesting(IBlueprintMaterialInventoryReader reader, bool reload)
        {
            lock (SyncRoot)
            {
                _testingInventoryReader = reader;
                if (reload)
                {
                    ResetCacheLocked();
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _testingInventoryReader = null;
                ResetCacheLocked();
            }
        }

        private static BlueprintMaterialSnapshot ResolveSnapshotLocked(BlueprintProjectionSnapshot projection, bool forceRefresh)
        {
            projection = projection ?? new BlueprintProjectionSnapshot
            {
                LoadSucceeded = false,
                ResultCode = "projectionUnavailable",
                Message = "蓝图投影不可用。"
            };

            var signature = BuildMaterialSignature(projection);
            var now = DateTime.UtcNow;
            if (!forceRefresh &&
                _lastSnapshot != null &&
                string.Equals(_lastSignature, signature, StringComparison.Ordinal) &&
                (now - _lastResolveUtc).TotalMilliseconds < CacheCadenceMs)
            {
                _cacheHitCount++;
                _lastSnapshot.CacheHitCount = _cacheHitCount;
                _lastSnapshot.WindowVisible = BlueprintMaterialWindowState.Visible;
                _lastSnapshot.WindowOpacityPercent = BlueprintMaterialWindowState.OpacityPercent;
                return _lastSnapshot;
            }

            _cacheMissCount++;
            var watch = Stopwatch.StartNew();
            var result = BuildMaterialSnapshot(projection, signature);
            watch.Stop();
            result.LastResolveElapsedMs = watch.Elapsed.TotalMilliseconds;
            result.LastResolvedUtc = now;
            result.CacheHitCount = _cacheHitCount;
            result.CacheMissCount = _cacheMissCount;
            _lastSignature = signature;
            _lastResolveUtc = now;
            _lastSnapshot = result;
            BlueprintDiagnostics.RecordMaterialResolve(result);
            return _lastSnapshot;
        }

        private static BlueprintMaterialSnapshot BuildMaterialSnapshot(BlueprintProjectionSnapshot projection, string signature)
        {
            var snapshot = new BlueprintMaterialSnapshot
            {
                LoadSucceeded = projection.LoadSucceeded,
                ResultCode = projection.LoadSucceeded ? "resolved" : "projectionUnavailable",
                Message = projection.LoadSucceeded ? "蓝图材料统计已解析。" : "蓝图投影不可用：" + projection.Message,
                WorldPairKey = projection.WorldPairKey ?? string.Empty,
                WorldKey = projection.WorldKey ?? string.Empty,
                ProjectionResultCode = projection.ResultCode ?? string.Empty,
                InventoryReadStatus = "skipped",
                InventoryReadMessage = "No missing material requires inventory read.",
                WindowVisible = BlueprintMaterialWindowState.Visible,
                WindowOpacityPercent = BlueprintMaterialWindowState.OpacityPercent,
                Signature = signature ?? string.Empty
            };

            if (!projection.LoadSucceeded)
            {
                return snapshot;
            }

            var replacementSettings = BlueprintReplacementRuleService.GetSettingsFromCurrentConfig();
            var requirements = new List<BlueprintMaterialRequirement>();
            var requiredItemIds = new HashSet<int>();
            var layers = projection.AllProjectedLayers ?? projection.ProjectedLayers;
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
                {
                    snapshot.SkippedFulfilledLayerCount++;
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    snapshot.SkippedConflictLayerCount++;
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Unavailable, StringComparison.Ordinal))
                {
                    snapshot.SkippedUnavailableLayerCount++;
                    continue;
                }

                if (!string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
                {
                    continue;
                }

                snapshot.ProjectionMissingLayerCount++;
                if (layer.MaterialItemId <= 0 || layer.MaterialStack <= 0)
                {
                    snapshot.SkippedMissingLayerWithoutMaterialCount++;
                    continue;
                }

                snapshot.MaterializedMissingLayerCount++;
                requirements.Add(new BlueprintMaterialRequirement(layer, ResolveMaterialDisplayName(layer)));
                requiredItemIds.Add(layer.MaterialItemId);
                var replacementIds = BlueprintReplacementRuleService.GetCandidateItemIdsForLayer(layer, replacementSettings);
                for (var replacementIndex = 0; replacementIndex < replacementIds.Count; replacementIndex++)
                {
                    var replacementItemId = replacementIds[replacementIndex];
                    if (replacementItemId > 0)
                    {
                        requiredItemIds.Add(replacementItemId);
                    }
                }
            }

            if (requirements.Count <= 0)
            {
                snapshot.ResultCode = snapshot.ProjectionMissingLayerCount <= 0 ? "noMissingMaterials" : "missingWithoutMaterial";
                snapshot.Message = snapshot.ProjectionMissingLayerCount <= 0
                    ? "当前世界投影没有缺失材料。"
                    : "缺失投影层没有可统计材料。";
                snapshot.InventoryReadSucceeded = true;
                snapshot.InventoryReadStatus = "skipped";
                snapshot.Items = new List<BlueprintMaterialItemSnapshot>();
                return snapshot;
            }

            BlueprintMaterialInventorySnapshot inventory = null;
            string inventoryMessage = string.Empty;
            var reader = ResolveInventoryReaderLocked();
            if (reader == null || !reader.TryReadStacks(requiredItemIds, out inventory, out inventoryMessage) || inventory == null || !inventory.Succeeded)
            {
                snapshot.LoadSucceeded = false;
                snapshot.ResultCode = "inventoryUnavailable";
                snapshot.Message = "材料需求已解析，但背包读取失败：" + inventoryMessage;
                snapshot.InventoryReadSucceeded = false;
                snapshot.InventoryReadStatus = inventory == null ? "inventoryUnavailable" : inventory.Status;
                snapshot.InventoryReadMessage = inventoryMessage ?? string.Empty;
                snapshot.Items = BuildItems(requirements, replacementSettings, null, snapshot);
                return snapshot;
            }

            snapshot.InventoryReadSucceeded = true;
            snapshot.InventoryReadStatus = inventory.Status;
            snapshot.InventoryReadMessage = inventory.Message;
            snapshot.InventoryMainStackTotal = inventory.MainStackTotal;
            snapshot.InventoryVoidBagStackTotal = inventory.VoidBagStackTotal;
            snapshot.Items = BuildItems(requirements, replacementSettings, inventory, snapshot);
            snapshot.ResultCode = snapshot.MissingStackTotal <= 0 ? "complete" : "missing";
            snapshot.Message = snapshot.MissingStackTotal <= 0
                ? "当前主背包和虚空袋已有全部缺失材料。"
                : "仍缺少材料堆叠 " + snapshot.MissingStackTotal.ToString(CultureInfo.InvariantCulture) + "。";
            return snapshot;
        }

        private static IReadOnlyList<BlueprintMaterialItemSnapshot> BuildItems(
            IList<BlueprintMaterialRequirement> requirements,
            BlueprintReplacementSettings replacementSettings,
            BlueprintMaterialInventorySnapshot inventory,
            BlueprintMaterialSnapshot snapshot)
        {
            var items = new List<BlueprintMaterialItemSnapshot>();
            if (requirements == null)
            {
                return items;
            }

            var required = new Dictionary<int, BlueprintMaterialAccumulator>();
            var plannedUse = new Dictionary<int, int>();
            for (var index = 0; index < requirements.Count; index++)
            {
                var requirement = requirements[index];
                if (requirement == null || requirement.Layer == null || requirement.Layer.MaterialItemId <= 0 || requirement.Layer.MaterialStack <= 0)
                {
                    continue;
                }

                var choice = BlueprintReplacementRuleService.ChooseMaterialForMaterialList(
                    requirement.Layer,
                    replacementSettings,
                    itemId => inventory == null ? 0 : inventory.GetAvailableStack(itemId),
                    plannedUse);
                var selectedItemId = choice == null || choice.MaterialItemId <= 0
                    ? requirement.Layer.MaterialItemId
                    : choice.MaterialItemId;
                var selectedDisplayName = choice != null && choice.ReplacementApplied
                    ? FirstNonEmpty(inventory == null ? string.Empty : inventory.GetDisplayName(selectedItemId), "#" + selectedItemId.ToString(CultureInfo.InvariantCulture))
                    : requirement.DisplayName;

                BlueprintMaterialAccumulator selected;
                if (!required.TryGetValue(selectedItemId, out selected))
                {
                    selected = new BlueprintMaterialAccumulator(selectedItemId, selectedDisplayName);
                    required[selectedItemId] = selected;
                }

                selected.RequiredStack += Math.Max(0, requirement.Layer.MaterialStack);
                selected.MissingLayerCount++;
                AddPlannedUse(plannedUse, selectedItemId, requirement.Layer.MaterialStack);
            }

            foreach (var pair in required)
            {
                var requiredItem = pair.Value;
                if (requiredItem == null || requiredItem.ItemId <= 0 || requiredItem.RequiredStack <= 0)
                {
                    continue;
                }

                var mainStack = inventory == null ? 0 : inventory.GetMainStack(requiredItem.ItemId);
                var voidBagStack = inventory == null ? 0 : inventory.GetVoidBagStack(requiredItem.ItemId);
                var available = mainStack + voidBagStack;
                var missing = Math.Max(0, requiredItem.RequiredStack - available);
                var inventoryName = inventory == null ? string.Empty : inventory.GetDisplayName(requiredItem.ItemId);
                var item = new BlueprintMaterialItemSnapshot
                {
                    ItemId = requiredItem.ItemId,
                    DisplayName = FirstNonEmpty(requiredItem.DisplayName, inventoryName, "#" + requiredItem.ItemId.ToString(CultureInfo.InvariantCulture)),
                    RequiredStack = requiredItem.RequiredStack,
                    AvailableStack = available,
                    MissingStack = missing,
                    MainInventoryStack = mainStack,
                    VoidBagStack = voidBagStack,
                    MissingLayerCount = requiredItem.MissingLayerCount
                };
                items.Add(item);
                snapshot.RequiredStackTotal += item.RequiredStack;
                snapshot.AvailableStackTotal += item.AvailableStack;
                snapshot.MissingStackTotal += item.MissingStack;
                if (item.MissingStack > 0)
                {
                    snapshot.MissingItemCount++;
                }
            }

            items.Sort(CompareMaterialItems);
            snapshot.RequiredItemCount = items.Count;
            return items;
        }

        private static int CompareMaterialItems(BlueprintMaterialItemSnapshot left, BlueprintMaterialItemSnapshot right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            var missingCompare = right.MissingStack.CompareTo(left.MissingStack);
            if (missingCompare != 0) return missingCompare;
            var requiredCompare = right.RequiredStack.CompareTo(left.RequiredStack);
            if (requiredCompare != 0) return requiredCompare;
            return left.ItemId.CompareTo(right.ItemId);
        }

        private static string BuildMaterialSignature(BlueprintProjectionSnapshot projection)
        {
            var builder = new StringBuilder();
            builder.Append(projection.Signature ?? string.Empty).Append('|');
            builder.Append(BlueprintReplacementRuleService.GetSettingsFromCurrentConfig().BuildSignature()).Append('|');
            builder.Append(projection.LastResolvedUtc.HasValue ? projection.LastResolvedUtc.Value.Ticks.ToString(CultureInfo.InvariantCulture) : "0").Append('|');
            builder.Append(projection.ResultCode ?? string.Empty).Append('|');
            builder.Append(projection.FulfilledLayerCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            builder.Append(projection.MissingLayerCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            builder.Append(projection.ConflictLayerCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            builder.Append(projection.UnavailableLayerCount.ToString(CultureInfo.InvariantCulture)).Append('|');
            var layers = projection.AllProjectedLayers ?? projection.ProjectedLayers;
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                builder.Append(layer.Status ?? string.Empty).Append(':');
                builder.Append(layer.MaterialItemId.ToString(CultureInfo.InvariantCulture)).Append(':');
                builder.Append(layer.MaterialStack.ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            return builder.ToString();
        }

        private static IBlueprintMaterialInventoryReader ResolveInventoryReaderLocked()
        {
            return _testingInventoryReader ?? new BlueprintTerrariaMaterialInventoryReader();
        }

        private static void ResetCacheLocked()
        {
            _lastSnapshot = null;
            _lastSignature = string.Empty;
            _lastResolveUtc = DateTime.MinValue;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
        }

        private static BlueprintMaterialSnapshot CloneLastSnapshotSummaryLocked()
        {
            return (_lastSnapshot ?? CreateNotResolvedSnapshot()).CloneSummary();
        }

        private static BlueprintMaterialSnapshot CreateNotResolvedSnapshot()
        {
            return new BlueprintMaterialSnapshot
            {
                LoadSucceeded = false,
                ResultCode = "notResolved",
                Message = "蓝图材料统计尚未刷新。",
                ProjectionResultCode = "notResolved",
                InventoryReadSucceeded = false,
                InventoryReadStatus = "notRead",
                InventoryReadMessage = "Blueprint materials were not refreshed.",
                WindowVisible = BlueprintMaterialWindowState.Visible,
                WindowOpacityPercent = BlueprintMaterialWindowState.OpacityPercent,
                LastResolvedUtc = null
            };
        }

        private static string ResolveMaterialDisplayName(BlueprintProjectionCellSnapshot layer)
        {
            if (layer == null)
            {
                return string.Empty;
            }

            return FirstNonEmpty(layer.MaterialDisplayName, "#" + layer.MaterialItemId.ToString(CultureInfo.InvariantCulture));
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (var index = 0; values != null && index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }

        private static void AddPlannedUse(IDictionary<int, int> plannedUse, int itemId, int stack)
        {
            if (plannedUse == null || itemId <= 0 || stack <= 0)
            {
                return;
            }

            int old;
            plannedUse.TryGetValue(itemId, out old);
            plannedUse[itemId] = old + stack;
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class BlueprintMaterialAccumulator
        {
            public BlueprintMaterialAccumulator(int itemId, string displayName)
            {
                ItemId = itemId;
                DisplayName = displayName ?? string.Empty;
            }

            public int ItemId { get; private set; }
            public string DisplayName { get; private set; }
            public int RequiredStack { get; set; }
            public int MissingLayerCount { get; set; }
        }

        private sealed class BlueprintMaterialRequirement
        {
            public BlueprintMaterialRequirement(BlueprintProjectionCellSnapshot layer, string displayName)
            {
                Layer = layer;
                DisplayName = displayName ?? string.Empty;
            }

            public BlueprintProjectionCellSnapshot Layer { get; private set; }
            public string DisplayName { get; private set; }
        }
    }
}
