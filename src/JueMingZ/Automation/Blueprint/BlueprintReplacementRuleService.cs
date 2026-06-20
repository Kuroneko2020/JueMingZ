using System;
using System.Collections.Generic;
using JueMingZ.Config;
using Terraria.ID;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintReplacementCategories
    {
        public const string None = "none";
        public const string Torch = "torch";
        public const string Platform = "platform";
        public const string WorkBench = "workbench";
        public const string Chair = "chair";
        public const string Door = "door";
        public const string Table = "table";
        public const string Chest = "chest";
        public const string Sign = "sign";
    }

    internal sealed class BlueprintReplacementSettings
    {
        public bool Enabled { get; set; }
        public bool TorchesEnabled { get; set; }
        public bool PlatformsEnabled { get; set; }
        public bool WorkBenchesEnabled { get; set; }
        public bool ChairsEnabled { get; set; }
        public bool DoorsEnabled { get; set; }
        public bool TablesEnabled { get; set; }
        public bool ChestsEnabled { get; set; }
        public bool SignsEnabled { get; set; }

        public bool IsCategoryEnabled(string category)
        {
            if (!Enabled)
            {
                return false;
            }

            if (string.Equals(category, BlueprintReplacementCategories.Torch, StringComparison.Ordinal)) return TorchesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Platform, StringComparison.Ordinal)) return PlatformsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.WorkBench, StringComparison.Ordinal)) return WorkBenchesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Chair, StringComparison.Ordinal)) return ChairsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Door, StringComparison.Ordinal)) return DoorsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Table, StringComparison.Ordinal)) return TablesEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Chest, StringComparison.Ordinal)) return ChestsEnabled;
            if (string.Equals(category, BlueprintReplacementCategories.Sign, StringComparison.Ordinal)) return SignsEnabled;
            return false;
        }

        public string BuildSignature()
        {
            return "replacement:" +
                   Bool(Enabled) +
                   Bool(TorchesEnabled) +
                   Bool(PlatformsEnabled) +
                   Bool(WorkBenchesEnabled) +
                   Bool(ChairsEnabled) +
                   Bool(DoorsEnabled) +
                   Bool(TablesEnabled) +
                   Bool(ChestsEnabled) +
                   Bool(SignsEnabled);
        }

        private static string Bool(bool value)
        {
            return value ? "1" : "0";
        }
    }

    internal sealed class BlueprintReplacementMaterialChoice
    {
        public BlueprintReplacementMaterialChoice()
        {
            Category = BlueprintReplacementCategories.None;
        }

        public int OriginalMaterialItemId { get; set; }
        public int MaterialItemId { get; set; }
        public int AvailableStack { get; set; }
        public bool ReplacementApplied { get; set; }
        public string Category { get; set; }
    }

    internal static class BlueprintReplacementRuleService
    {
        private const int TileTorches = 4;
        private const int TileClosedDoor = 10;
        private const int TileTables = 14;
        private const int TileChairs = 15;
        private const int TileWorkBenches = 18;
        private const int TilePlatforms = 19;
        private const int TileContainers = 21;
        private const int TileSigns = 55;
        private const int TileContainers2 = 467;
        private const int TileTables2 = 469;
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<string, List<int>> _itemIdsByCategory = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        public static BlueprintReplacementSettings GetSettingsFromCurrentConfig()
        {
            return FromSettings(ConfigService.AppSettings ?? AppSettings.CreateDefault());
        }

        public static BlueprintReplacementSettings FromSettings(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return new BlueprintReplacementSettings
            {
                Enabled = settings.BlueprintReplacementEnabled,
                TorchesEnabled = settings.BlueprintReplacementTorchesEnabled,
                PlatformsEnabled = settings.BlueprintReplacementPlatformsEnabled,
                WorkBenchesEnabled = settings.BlueprintReplacementWorkBenchesEnabled,
                ChairsEnabled = settings.BlueprintReplacementChairsEnabled,
                DoorsEnabled = settings.BlueprintReplacementDoorsEnabled,
                TablesEnabled = settings.BlueprintReplacementTablesEnabled,
                ChestsEnabled = settings.BlueprintReplacementChestsEnabled,
                SignsEnabled = settings.BlueprintReplacementSignsEnabled
            };
        }

        public static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
                _itemIdsByCategory = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            }
        }

        internal static void SetCandidateItemIdsForTesting(string category, IReadOnlyList<int> itemIds)
        {
            lock (SyncRoot)
            {
                _itemIdsByCategory = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                var normalized = string.IsNullOrWhiteSpace(category) ? BlueprintReplacementCategories.None : category;
                var ids = new List<int>();
                for (var index = 0; itemIds != null && index < itemIds.Count; index++)
                {
                    var itemId = itemIds[index];
                    if (itemId > 0 && !ids.Contains(itemId))
                    {
                        ids.Add(itemId);
                    }
                }

                ids.Sort();
                _itemIdsByCategory[normalized] = ids;
                _initialized = true;
            }
        }

        public static bool TryChooseMaterialForAutoPlacement(
            BlueprintProjectionCellSnapshot layer,
            BlueprintReplacementSettings settings,
            IDictionary<int, int> mainAvailability,
            out BlueprintReplacementMaterialChoice choice)
        {
            choice = BuildOriginalChoice(layer, mainAvailability);
            if (layer == null || layer.MaterialItemId <= 0 || layer.MaterialStack <= 0)
            {
                return false;
            }

            if (choice.AvailableStack >= layer.MaterialStack)
            {
                return true;
            }

            string category;
            if (!CanReplaceLayer(layer, settings, out category))
            {
                return false;
            }

            var candidates = GetCandidateItemIdsForLayer(layer, settings);
            for (var index = 0; index < candidates.Count; index++)
            {
                var itemId = candidates[index];
                if (itemId <= 0 || itemId == layer.MaterialItemId)
                {
                    continue;
                }

                var available = GetAvailable(mainAvailability, itemId);
                if (available < layer.MaterialStack)
                {
                    continue;
                }

                choice = new BlueprintReplacementMaterialChoice
                {
                    OriginalMaterialItemId = layer.MaterialItemId,
                    MaterialItemId = itemId,
                    AvailableStack = available,
                    ReplacementApplied = true,
                    Category = category
                };
                return true;
            }

            return false;
        }

        public static BlueprintReplacementMaterialChoice ChooseMaterialForMaterialList(
            BlueprintProjectionCellSnapshot layer,
            BlueprintReplacementSettings settings,
            Func<int, int> readAvailable,
            IDictionary<int, int> plannedUse)
        {
            var original = layer == null ? 0 : layer.MaterialItemId;
            var required = layer == null ? 0 : layer.MaterialStack;
            var originalAvailable = Math.Max(0, readAvailable == null ? 0 : readAvailable(original));
            var originalRemaining = Math.Max(0, originalAvailable - GetAvailable(plannedUse, original));
            if (layer == null || original <= 0 || required <= 0 || originalRemaining >= required)
            {
                return new BlueprintReplacementMaterialChoice
                {
                    OriginalMaterialItemId = original,
                    MaterialItemId = original,
                    AvailableStack = originalAvailable,
                    ReplacementApplied = false,
                    Category = BlueprintReplacementCategories.None
                };
            }

            string category;
            if (!CanReplaceLayer(layer, settings, out category))
            {
                return new BlueprintReplacementMaterialChoice
                {
                    OriginalMaterialItemId = original,
                    MaterialItemId = original,
                    AvailableStack = originalAvailable,
                    ReplacementApplied = false,
                    Category = BlueprintReplacementCategories.None
                };
            }

            var candidates = GetCandidateItemIdsForLayer(layer, settings);
            for (var index = 0; index < candidates.Count; index++)
            {
                var itemId = candidates[index];
                if (itemId <= 0 || itemId == original)
                {
                    continue;
                }

                var available = Math.Max(0, readAvailable == null ? 0 : readAvailable(itemId));
                var remaining = Math.Max(0, available - GetAvailable(plannedUse, itemId));
                if (remaining < required)
                {
                    continue;
                }

                return new BlueprintReplacementMaterialChoice
                {
                    OriginalMaterialItemId = original,
                    MaterialItemId = itemId,
                    AvailableStack = available,
                    ReplacementApplied = true,
                    Category = category
                };
            }

            return new BlueprintReplacementMaterialChoice
            {
                OriginalMaterialItemId = original,
                MaterialItemId = original,
                AvailableStack = originalAvailable,
                ReplacementApplied = false,
                Category = BlueprintReplacementCategories.None
            };
        }

        public static IReadOnlyList<int> GetCandidateItemIdsForLayer(
            BlueprintProjectionCellSnapshot layer,
            BlueprintReplacementSettings settings)
        {
            string category;
            return CanReplaceLayer(layer, settings, out category)
                ? GetCandidateItemIdsForCategory(category)
                : new List<int>();
        }

        public static bool IsWorldReplacementFulfilled(
            BlueprintCellLayerRecord layer,
            BlueprintWorldTileSnapshot world,
            BlueprintReplacementSettings settings,
            out string category)
        {
            category = BlueprintReplacementCategories.None;
            if (layer == null || world == null || !world.Active)
            {
                return false;
            }

            if (!CanReplaceLayer(
                    layer.LayerKind,
                    layer.ContentId,
                    layer.PaintId,
                    layer.CoatingFlags,
                    layer.Slope,
                    layer.HalfBrick,
                    layer.Inactive,
                    settings,
                    out category))
            {
                return false;
            }

            string worldCategory;
            if (!TryClassifyTileCategory(world.TileType, out worldCategory) ||
                !string.Equals(category, worldCategory, StringComparison.Ordinal))
            {
                return false;
            }

            return world.TilePaintId == layer.PaintId &&
                   BuildTileCoatingFlags(world) == layer.CoatingFlags &&
                   world.Slope == layer.Slope &&
                   world.HalfBrick == layer.HalfBrick &&
                   world.Inactive == layer.Inactive;
        }

        private static bool CanReplaceLayer(BlueprintProjectionCellSnapshot layer, BlueprintReplacementSettings settings, out string category)
        {
            if (layer == null)
            {
                category = BlueprintReplacementCategories.None;
                return false;
            }

            return CanReplaceLayer(
                layer.LayerKind,
                layer.ContentId,
                layer.PaintId,
                layer.CoatingFlags,
                layer.Slope,
                layer.HalfBrick,
                layer.Inactive,
                settings,
                out category);
        }

        private static bool CanReplaceLayer(
            string layerKind,
            int contentId,
            int paintId,
            int coatingFlags,
            int slope,
            bool halfBrick,
            bool inactive,
            BlueprintReplacementSettings settings,
            out string category)
        {
            category = BlueprintReplacementCategories.None;
            if (settings == null || !settings.Enabled)
            {
                return false;
            }

            if (paintId != 0 || coatingFlags != 0 || slope != 0 || halfBrick || inactive)
            {
                return false;
            }

            if (!TryClassifyLayerCategory(layerKind, contentId, out category))
            {
                return false;
            }

            return settings.IsCategoryEnabled(category);
        }

        private static bool TryClassifyLayerCategory(string layerKind, int contentId, out string category)
        {
            category = BlueprintReplacementCategories.None;
            if (contentId <= 0)
            {
                return false;
            }

            var kind = layerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                return contentId == TilePlatforms && TrySet(BlueprintReplacementCategories.Platform, out category);
            }

            if (!string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryClassifyTileCategory(contentId, out category);
        }

        private static bool TryClassifyTileCategory(int tileType, out string category)
        {
            category = BlueprintReplacementCategories.None;
            switch (tileType)
            {
                case TileTorches:
                    category = BlueprintReplacementCategories.Torch;
                    return true;
                case TilePlatforms:
                    category = BlueprintReplacementCategories.Platform;
                    return true;
                case TileWorkBenches:
                    category = BlueprintReplacementCategories.WorkBench;
                    return true;
                case TileChairs:
                    category = BlueprintReplacementCategories.Chair;
                    return true;
                case TileClosedDoor:
                    category = BlueprintReplacementCategories.Door;
                    return true;
                case TileTables:
                case TileTables2:
                    category = BlueprintReplacementCategories.Table;
                    return true;
                case TileContainers:
                case TileContainers2:
                    category = BlueprintReplacementCategories.Chest;
                    return true;
                case TileSigns:
                    category = BlueprintReplacementCategories.Sign;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TrySet(string value, out string category)
        {
            category = value ?? BlueprintReplacementCategories.None;
            return true;
        }

        private static IReadOnlyList<int> GetCandidateItemIdsForCategory(string category)
        {
            EnsureInitialized();
            lock (SyncRoot)
            {
                List<int> ids;
                if (!_itemIdsByCategory.TryGetValue(category ?? string.Empty, out ids) || ids == null)
                {
                    return new List<int>();
                }

                return new List<int>(ids);
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

                var byCategory = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                try
                {
                    var details = ItemID.Sets.DerivedPlacementDetails;
                    for (var itemId = 1; details != null && itemId < details.Length; itemId++)
                    {
                        AddItem(byCategory, itemId, details[itemId].tileType);
                    }

                    foreach (var pair in ContentSamples.ItemsByType)
                    {
                        var item = pair.Value;
                        if (item == null)
                        {
                            continue;
                        }

                        AddItem(byCategory, pair.Key, item.createTile);
                    }
                }
                catch
                {
                    byCategory.Clear();
                }

                foreach (var pair in byCategory)
                {
                    pair.Value.Sort();
                }

                _itemIdsByCategory = byCategory;
                _initialized = true;
            }
        }

        private static void AddItem(IDictionary<string, List<int>> byCategory, int itemId, int tileType)
        {
            if (byCategory == null || itemId <= 0)
            {
                return;
            }

            string category;
            if (!TryClassifyTileCategory(tileType, out category))
            {
                return;
            }

            List<int> ids;
            if (!byCategory.TryGetValue(category, out ids))
            {
                ids = new List<int>();
                byCategory[category] = ids;
            }

            if (!ids.Contains(itemId))
            {
                ids.Add(itemId);
            }
        }

        private static BlueprintReplacementMaterialChoice BuildOriginalChoice(
            BlueprintProjectionCellSnapshot layer,
            IDictionary<int, int> availability)
        {
            var materialItemId = layer == null ? 0 : layer.MaterialItemId;
            return new BlueprintReplacementMaterialChoice
            {
                OriginalMaterialItemId = materialItemId,
                MaterialItemId = materialItemId,
                AvailableStack = GetAvailable(availability, materialItemId),
                ReplacementApplied = false,
                Category = BlueprintReplacementCategories.None
            };
        }

        private static int GetAvailable(IDictionary<int, int> availability, int itemId)
        {
            if (availability == null || itemId <= 0)
            {
                return 0;
            }

            int value;
            return availability.TryGetValue(itemId, out value) ? Math.Max(0, value) : 0;
        }

        private static int BuildTileCoatingFlags(BlueprintWorldTileSnapshot world)
        {
            var flags = 0;
            if (world.TileFullbright)
            {
                flags |= BlueprintCaptureCoatingFlags.Fullbright;
            }

            if (world.TileInvisible)
            {
                flags |= BlueprintCaptureCoatingFlags.Invisible;
            }

            return flags;
        }
    }
}
