using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiElement DrawAutoSellRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var enabled = settings != null && settings.InventoryAutoSellEnabled;
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动出售",
                enabled ? "On" : "Off",
                new[] { "添加", "开启", "关闭" },
                new[] { "add-empty", "On", "Off" },
                "misc-auto-sell-row:",
                new[]
                {
                    "从背包批量选择要加入自动出售名单的物品。",
                    "尝试出售名单里的物品",
                    "关闭自动出售功能。"
                });
        }

        private static LegacyUiElement DrawAutoDiscardRow(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, AppSettings settings)
        {
            var enabled = settings != null && settings.InventoryAutoDiscardEnabled;
            return DrawRightModeRow(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                "自动丢弃",
                enabled ? "On" : "Off",
                new[] { "添加", "开启", "关闭" },
                new[] { "add-empty", "On", "Off" },
                "misc-auto-discard-row:",
                new[]
                {
                    "从背包批量选择要加入自动丢弃名单的物品。",
                    "尝试丢弃名单里的物品",
                    "关闭自动丢弃功能。"
                });
        }

        private static LegacyUiElement DrawAutoSellListPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, out int consumedHeight)
        {
            return DrawMiscItemTypeListPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                out consumedHeight,
                GetAutoSellItemIds(),
                _autoSellPickerOpen,
                _autoSellPickerIndex,
                CloseAutoSellPicker,
                GetAutoSellPickerCandidates,
                DrawAutoSellItemCard,
                DrawAutoSellInventoryPickerPanel);
        }

        private static LegacyUiElement DrawAutoSellItemCard(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, int itemType, int index, LegacyUiRect card)
        {
            return DrawMiscItemTypeCard(
                spriteBatch,
                mouse,
                elements,
                clip,
                itemType,
                index,
                card,
                _autoSellPickerOpen && _autoSellPickerIndex == index,
                "misc-auto-sell:picker-open:",
                "自动出售:选择物品",
                "misc-auto-sell:remove:",
                "从自动出售名单移除",
                BuildAutoSellItemDisplayLabel(itemType),
                "点击修改这个出售名单物品。",
                "点击从背包选择要加入出售名单的物品。",
                "从自动出售名单移除这个物品。",
                "删除这个空位。");
        }

        private static LegacyUiElement DrawAutoSellInventoryPickerPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, List<QuickItemInventoryCandidate> candidates, List<int> itemIds)
        {
            var itemIndex = _autoSellPickerIndex;
            return DrawMiscInventoryIconPickerPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                rect,
                candidates,
                new MiscInventoryIconPickerOptions
                {
                    Title = "点击选择物品",
                    CloseId = "misc-auto-sell:picker-close",
                    CloseLabel = "关闭出售物品选择",
                    CloseTooltip = "关闭出售物品选择窗口。",
                    ConfirmId = itemIndex < 0 ? "misc-auto-sell:picker-confirm" : null,
                    ConfirmLabel = "确定",
                    ConfirmTooltip = "把已选物品加入自动出售名单。",
                    EmptyText = "背包里没有可加入出售名单的物品。",
                    ToggleIdPrefix = "misc-auto-sell:picker-toggle:",
                    ToggleLabel = "切换自动出售物品选择",
                    SelectIdPrefix = "misc-auto-sell:picker-select:",
                    SelectLabel = "选择自动出售物品",
                    TooltipPrefix = itemIndex < 0 ? "加入出售名单：" : "替换为：",
                    TargetIndex = itemIndex,
                    IsSelected = candidate => itemIndex < 0 && AutoSellPickerPendingItemTypes.Contains(candidate.ItemType)
                });
        }

        private static LegacyUiElement DrawAutoDiscardListPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, int contentY, out int consumedHeight)
        {
            return DrawMiscItemTypeListPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                contentY,
                out consumedHeight,
                GetAutoDiscardItemIds(),
                _autoDiscardPickerOpen,
                _autoDiscardPickerIndex,
                CloseAutoDiscardPicker,
                GetAutoDiscardPickerCandidates,
                DrawAutoDiscardItemCard,
                DrawAutoDiscardInventoryPickerPanel);
        }

        private static LegacyUiElement DrawAutoDiscardItemCard(object spriteBatch, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect clip, int itemType, int index, LegacyUiRect card)
        {
            return DrawMiscItemTypeCard(
                spriteBatch,
                mouse,
                elements,
                clip,
                itemType,
                index,
                card,
                _autoDiscardPickerOpen && _autoDiscardPickerIndex == index,
                "misc-auto-discard:picker-open:",
                "自动丢弃:选择物品",
                "misc-auto-discard:remove:",
                "从自动丢弃名单移除",
                BuildAutoDiscardItemDisplayLabel(itemType),
                "点击修改这个丢弃名单物品。",
                "点击从背包选择要加入丢弃名单的物品。",
                "从自动丢弃名单移除这个物品。",
                "删除这个空位。");
        }

        private static LegacyUiElement DrawAutoDiscardInventoryPickerPanel(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements, LegacyUiRect rect, List<QuickItemInventoryCandidate> candidates, List<int> itemIds)
        {
            var itemIndex = _autoDiscardPickerIndex;
            return DrawMiscInventoryIconPickerPanel(
                spriteBatch,
                area,
                mouse,
                elements,
                rect,
                candidates,
                new MiscInventoryIconPickerOptions
                {
                    Title = "点击选择物品",
                    CloseId = "misc-auto-discard:picker-close",
                    CloseLabel = "关闭丢弃物品选择",
                    CloseTooltip = "关闭丢弃物品选择窗口。",
                    ConfirmId = itemIndex < 0 ? "misc-auto-discard:picker-confirm" : null,
                    ConfirmLabel = "确定",
                    ConfirmTooltip = "把已选物品加入自动丢弃名单。",
                    EmptyText = "背包里没有可加入丢弃名单的物品。",
                    ToggleIdPrefix = "misc-auto-discard:picker-toggle:",
                    ToggleLabel = "切换自动丢弃物品选择",
                    SelectIdPrefix = "misc-auto-discard:picker-select:",
                    SelectLabel = "选择自动丢弃物品",
                    TooltipPrefix = itemIndex < 0 ? "加入丢弃名单：" : "替换为：",
                    TargetIndex = itemIndex,
                    IsSelected = candidate => itemIndex < 0 && AutoDiscardPickerPendingItemTypes.Contains(candidate.ItemType)
                });
        }

        private static List<int> GetAutoSellItemIds()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.InventoryAutoSellItemIds == null)
            {
                settings.InventoryAutoSellItemIds = new List<int>(AutoSellCompat.DefaultAutoSellItemIds);
            }

            return settings.InventoryAutoSellItemIds;
        }

        private static List<int> GetAutoDiscardItemIds()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (settings.InventoryAutoDiscardItemIds == null)
            {
                settings.InventoryAutoDiscardItemIds = new List<int>();
            }

            return settings.InventoryAutoDiscardItemIds;
        }

        private static string BuildAutoSellItemDisplayLabel(int itemType)
        {
            return itemType <= 0
                ? "未选择"
                : ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, itemType.ToString(CultureInfo.InvariantCulture));
        }

        private static string BuildAutoDiscardItemDisplayLabel(int itemType)
        {
            return BuildAutoSellItemDisplayLabel(itemType);
        }

        private static List<QuickItemInventoryCandidate> BuildAutoSellInventoryCandidates()
        {
            var snapshotCandidates = BuildAutoSellInventoryCandidatesFromSnapshot();
            if (snapshotCandidates != null)
            {
                return snapshotCandidates;
            }

            var result = new List<QuickItemInventoryCandidate>();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return result;
            }

            IList inventory;
            string message;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                return result;
            }

            var seen = new HashSet<int>();
            var max = Math.Min(58, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                var item = inventory[slot];
                int itemType;
                string itemName;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                    itemType <= 0 ||
                    stack <= 0 ||
                    IsCoinItemType(itemType) ||
                    !seen.Add(itemType))
                {
                    continue;
                }

                result.Add(new QuickItemInventoryCandidate
                {
                    ItemType = itemType,
                    ItemName = ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, itemName),
                    Slot = slot,
                    Stack = stack
                });
            }

            return result;
        }

        private static List<QuickItemInventoryCandidate> BuildAutoSellInventoryCandidatesFromSnapshot()
        {
            var snapshot = GameStateReader.LastSnapshot;
            var inventory = snapshot == null || snapshot.Inventory == null ? null : snapshot.Inventory.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                return null;
            }

            var result = new List<QuickItemInventoryCandidate>();
            var seen = new HashSet<int>();
            var max = Math.Min(58, inventory.Count);
            for (var slot = 0; slot < max; slot++)
            {
                var item = inventory[slot];
                if (item == null)
                {
                    continue;
                }

                var itemType = item.Type;
                var stack = item.Stack;
                if (itemType <= 0 || stack <= 0 || IsCoinItemType(itemType) || !seen.Add(itemType))
                {
                    continue;
                }

                var fallbackName = itemType.ToString(CultureInfo.InvariantCulture);
                var resolvedName = string.IsNullOrWhiteSpace(item.Name)
                    ? ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, fallbackName)
                    : item.Name.Trim();
                result.Add(new QuickItemInventoryCandidate
                {
                    ItemType = itemType,
                    ItemName = resolvedName,
                    Slot = slot,
                    Stack = stack
                });
            }

            return result;
        }

        private static List<QuickItemInventoryCandidate> BuildAutoDiscardInventoryCandidates()
        {
            return BuildAutoSellInventoryCandidates();
        }

        private static List<QuickItemInventoryCandidate> GetAutoSellPickerCandidates()
        {
            if (!_autoSellPickerOpen)
            {
                _autoSellPickerCandidateCache = null;
                _autoSellPickerCandidateCacheUtc = DateTime.MinValue;
                return null;
            }

            var now = DateTime.UtcNow;
            if (_autoSellPickerCandidateCache != null &&
                now - _autoSellPickerCandidateCacheUtc <= PickerCandidateCacheWindow)
            {
                return _autoSellPickerCandidateCache;
            }

            _autoSellPickerCandidateCache = FilterAutoItemPickerCandidates(BuildAutoSellInventoryCandidates(), GetAutoSellItemIds());
            _autoSellPickerCandidateCacheUtc = now;
            return _autoSellPickerCandidateCache;
        }

        private static List<QuickItemInventoryCandidate> GetAutoDiscardPickerCandidates()
        {
            if (!_autoDiscardPickerOpen)
            {
                _autoDiscardPickerCandidateCache = null;
                _autoDiscardPickerCandidateCacheUtc = DateTime.MinValue;
                return null;
            }

            var now = DateTime.UtcNow;
            if (_autoDiscardPickerCandidateCache != null &&
                now - _autoDiscardPickerCandidateCacheUtc <= PickerCandidateCacheWindow)
            {
                return _autoDiscardPickerCandidateCache;
            }

            _autoDiscardPickerCandidateCache = FilterAutoItemPickerCandidates(BuildAutoDiscardInventoryCandidates(), GetAutoDiscardItemIds());
            _autoDiscardPickerCandidateCacheUtc = now;
            return _autoDiscardPickerCandidateCache;
        }

        private static List<QuickItemInventoryCandidate> FilterAutoItemPickerCandidates(List<QuickItemInventoryCandidate> candidates, IList<int> itemIds)
        {
            if (candidates == null || candidates.Count <= 0)
            {
                return candidates;
            }

            var result = new List<QuickItemInventoryCandidate>(candidates.Count);
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate == null ||
                    candidate.ItemType <= 0 ||
                    ContainsAutoSellItemType(itemIds, candidate.ItemType, -1))
                {
                    continue;
                }

                result.Add(candidate);
            }

            return result;
        }

        private static bool ContainsAutoSellItemType(IList<int> itemIds, int itemType, int exceptIndex)
        {
            if (itemIds == null || itemType <= 0)
            {
                return false;
            }

            for (var index = 0; index < itemIds.Count; index++)
            {
                if (index != exceptIndex && itemIds[index] == itemType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAutoDiscardItemType(IList<int> itemIds, int itemType, int exceptIndex)
        {
            return ContainsAutoSellItemType(itemIds, itemType, exceptIndex);
        }

        private static bool IsCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }
    }
}
