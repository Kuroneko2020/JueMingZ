using System;
using JueMingZ.Compat;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;

namespace JueMingZ.Diagnostics
{
    public sealed class DiagnosticHotbarSlotInfo
    {
        public int Slot { get; set; }
        public int SlotDisplay { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public int ItemStack { get; set; }
        public string Suitability { get; set; }
        public string Hint { get; set; }
        public bool IsEmpty { get; set; }
        public bool IsLikelyPlacementItem { get; set; }

        public DiagnosticHotbarSlotInfo()
        {
            Slot = 0;
            SlotDisplay = 1;
            ItemName = string.Empty;
            Suitability = "不确定";
            Hint = "建议用武器、工具或药水测试，避免用方块或火把。";
        }

        public string ItemDisplay
        {
            get
            {
                return IsEmpty
                    ? "空"
                    : (string.IsNullOrWhiteSpace(ItemName) ? "未命名物品" : ItemName) + " x" + ItemStack;
            }
        }
    }

    public static class DiagnosticHotbarInfo
    {
        public static DiagnosticHotbarSlotInfo FromSnapshot(GameStateSnapshot snapshot, int slot)
        {
            InventoryItemSnapshot item = null;
            if (snapshot != null && snapshot.Inventory != null && snapshot.Inventory.Items != null)
            {
                for (var index = 0; index < snapshot.Inventory.Items.Count; index++)
                {
                    var candidate = snapshot.Inventory.Items[index];
                    if (candidate != null && candidate.SlotIndex == slot)
                    {
                        item = candidate;
                        break;
                    }
                }
            }

            return FromItem(
                slot,
                item == null ? 0 : item.Type,
                item == null ? string.Empty : item.Name,
                item == null ? 0 : item.Stack,
                item == null ? 0 : item.UseStyle,
                item != null && item.Consumable,
                item == null ? 0 : item.HealLife,
                item == null ? 0 : item.HealMana,
                item == null ? 0 : item.BuffType,
                item == null ? 0 : item.BuffTime,
                item == null ? -1 : item.CreateTile,
                item == null ? -1 : item.CreateWall);
        }

        public static DiagnosticHotbarSlotInfo FromItemUseState(int slot, ItemUseVerificationState state)
        {
            return FromItem(
                slot,
                state == null ? 0 : state.ItemType,
                state == null ? string.Empty : state.ItemName,
                state == null ? 0 : state.ItemStack,
                state == null ? 0 : state.UseStyle,
                state != null && state.Consumable,
                state == null ? 0 : state.HealLife,
                state == null ? 0 : state.HealMana,
                state == null ? 0 : state.BuffType,
                state == null ? 0 : state.BuffTime,
                state == null ? -1 : state.CreateTile,
                state == null ? -1 : state.CreateWall);
        }

        public static DiagnosticHotbarSlotInfo FromItem(
            int slot,
            int itemType,
            string itemName,
            int itemStack,
            int useStyle,
            bool consumable,
            int healLife,
            int healMana,
            int buffType,
            int buffTime,
            int createTile,
            int createWall)
        {
            var info = new DiagnosticHotbarSlotInfo
            {
                Slot = ClampSlot(slot),
                SlotDisplay = ClampSlot(slot) + 1,
                ItemType = itemType,
                ItemName = itemName ?? string.Empty,
                ItemStack = itemStack,
                IsEmpty = itemType <= 0 || itemStack <= 0,
                IsLikelyPlacementItem = IsLikelyPlacementItem(itemName, createTile, createWall)
            };

            if (info.IsEmpty)
            {
                info.Suitability = "不适合";
                info.Hint = "该格为空；点击“使用测试格物品”会返回 MissingRequiredItem，请切换到有物品的快捷栏。";
            }
            else if (info.IsLikelyPlacementItem)
            {
                info.Suitability = "不适合";
                info.Hint = "该物品可能需要鼠标指向可放置位置；若测试无明显变化，建议换成武器、工具或药水。";
            }
            else if (IsLikelyGoodTestItem(useStyle, consumable, healLife, healMana, buffType, buffTime))
            {
                info.Suitability = "适合";
                info.Hint = "该物品适合点击“使用测试格物品”按钮测试。";
            }
            else
            {
                info.Suitability = "不确定";
                info.Hint = "建议用武器、工具或药水测试，避免用方块或火把。";
            }

            return info;
        }

        public static bool IsLikelyPlacementItem(ItemUseVerificationState state)
        {
            return state != null && IsLikelyPlacementItem(state.ItemName, state.CreateTile, state.CreateWall);
        }

        private static bool IsLikelyGoodTestItem(int useStyle, bool consumable, int healLife, int healMana, int buffType, int buffTime)
        {
            return useStyle > 0 ||
                   consumable ||
                   healLife > 0 ||
                   healMana > 0 ||
                   buffType > 0 ||
                   buffTime > 0;
        }

        private static bool IsLikelyPlacementItem(string itemName, int createTile, int createWall)
        {
            if (createTile >= 0 || createWall >= 0)
            {
                return true;
            }

            var name = itemName ?? string.Empty;
            return Contains(name, "火把") ||
                   Contains(name, "方块") ||
                   Contains(name, "平台") ||
                   Contains(name, "torch") ||
                   Contains(name, "block") ||
                   Contains(name, "platform");
        }

        private static bool Contains(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ClampSlot(int slot)
        {
            if (slot < 0)
            {
                return 0;
            }

            return slot > 9 ? 9 : slot;
        }
    }
}
