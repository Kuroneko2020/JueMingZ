using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static int CalculateQuickItemPanelHeight(int viewportWidth, int bindingCount, bool captureActive, bool pickerOpen, int pickerCandidateCount)
        {
            var height = LegacyUiMetrics.SectionHeaderHeight + CalculateQuickItemCardsBodyHeight(viewportWidth, bindingCount);
            if (captureActive)
            {
                height += QuickItemCaptureHintHeight + 6;
            }

            if (pickerOpen)
            {
                height += CalculateAutoItemPickerPanelHeight(viewportWidth, pickerCandidateCount) + 8;
            }

            return height;
        }

        private static int CalculateQuickItemCardsBodyHeight(int viewportWidth, int bindingCount)
        {
            if (bindingCount <= 0)
            {
                return 44;
            }

            int columns;
            int rows;
            int cardWidth;
            ComputeQuickItemCardLayout(viewportWidth, bindingCount, out columns, out rows, out cardWidth);
            return rows * QuickItemCardHeight + Math.Max(0, rows - 1) * QuickItemCardGap + 16;
        }

        private static void ComputeQuickItemCardLayout(int viewportWidth, int bindingCount, out int columns, out int rows, out int cardWidth)
        {
            var innerWidth = Math.Max(120, viewportWidth - 16);
            columns = Math.Max(1, Math.Min(3, (innerWidth + QuickItemCardGap) / (QuickItemCardMinWidth + QuickItemCardGap)));
            while (columns > 1)
            {
                var width = (innerWidth - (columns - 1) * QuickItemCardGap) / columns;
                if (width >= QuickItemCardMinWidth)
                {
                    break;
                }

                columns--;
            }

            cardWidth = Math.Max(QuickItemCardMinWidth, (innerWidth - (columns - 1) * QuickItemCardGap) / columns);
            rows = bindingCount <= 0 ? 0 : (bindingCount + columns - 1) / columns;
        }

        private static int ResolveQuickItemHotkeyWidth(string hotkeyText, bool captureSelected, int cardWidth)
        {
            var text = string.IsNullOrWhiteSpace(hotkeyText) ? "+" : hotkeyText.Trim();
            var desired = QuickItemHotkeyCellMinWidth;
            if (captureSelected)
            {
                desired = 98;
            }
            else if (text == "+")
            {
                desired = 58;
            }
            else if (text.Length <= 4)
            {
                desired = 70;
            }
            else if (text.Length <= 7)
            {
                desired = 80;
            }
            else if (text.Length <= 9)
            {
                desired = 90;
            }
            else
            {
                desired = 100;
            }

            desired = Math.Max(QuickItemHotkeyCellMinWidth, Math.Min(118, desired));
            var maxByCard = Math.Max(QuickItemHotkeyCellMinWidth, cardWidth - 74);
            return Math.Min(desired, maxByCard);
        }

        private static string BuildQuickItemBindingDisplayLabel(QuickItemHotkeyBinding binding, int itemType)
        {
            if (itemType <= 0)
            {
                return "未选择";
            }

            var name = string.IsNullOrWhiteSpace(binding == null ? null : binding.DisplayName)
                ? itemType.ToString(CultureInfo.InvariantCulture)
                : binding.DisplayName.Trim();
            return name;
        }

        private static List<QuickItemHotkeyBinding> GetQuickItemBindings()
        {
            var hotkeySettings = ConfigService.HotkeySettings;
            if (hotkeySettings == null)
            {
                return new List<QuickItemHotkeyBinding>();
            }

            if (hotkeySettings.QuickItemHotkeyBindings == null)
            {
                hotkeySettings.QuickItemHotkeyBindings = new List<QuickItemHotkeyBinding>();
            }

            return hotkeySettings.QuickItemHotkeyBindings;
        }

        private static int GetQuickItemBindingPrimaryItemType(QuickItemHotkeyBinding binding)
        {
            if (binding == null || binding.ItemTypes == null)
            {
                return 0;
            }

            for (var index = 0; index < binding.ItemTypes.Count; index++)
            {
                if (binding.ItemTypes[index] > 0)
                {
                    return binding.ItemTypes[index];
                }
            }

            return 0;
        }

        private static List<QuickItemInventoryCandidate> BuildQuickItemInventoryCandidates()
        {
            var snapshotCandidates = BuildQuickItemInventoryCandidatesFromSnapshot();
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
            for (var slot = 0; slot < inventory.Count; slot++)
            {
                if (!TerrariaInputCompat.IsSupportedItemUseSlot(slot))
                {
                    continue;
                }

                var item = inventory[slot];
                int itemType;
                string itemName;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) ||
                    itemType <= 0 ||
                    stack <= 0)
                {
                    continue;
                }

                var family = ItemSwapFamilyCompat.GetVisibleSwapFamily(itemType);
                if (family != null && family.Length > 0)
                {
                    for (var familyIndex = 0; familyIndex < family.Length; familyIndex++)
                    {
                        var familyType = family[familyIndex];
                        if (familyType <= 0 || !seen.Add(familyType))
                        {
                            continue;
                        }

                        result.Add(new QuickItemInventoryCandidate
                        {
                            ItemType = familyType,
                            ItemName = ItemSwapFamilyCompat.ResolveItemDisplayName(familyType, itemName),
                            Slot = slot,
                            Stack = stack
                        });
                    }

                    continue;
                }

                if (!seen.Add(itemType))
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

        private static List<QuickItemInventoryCandidate> GetQuickItemPickerCandidates()
        {
            if (!_quickItemPickerOpen)
            {
                _quickItemPickerCandidateCache = null;
                _quickItemPickerCandidateCacheUtc = DateTime.MinValue;
                return null;
            }

            var now = DateTime.UtcNow;
            if (_quickItemPickerCandidateCache != null &&
                now - _quickItemPickerCandidateCacheUtc <= PickerCandidateCacheWindow)
            {
                return _quickItemPickerCandidateCache;
            }

            _quickItemPickerCandidateCache = BuildQuickItemInventoryCandidates();
            _quickItemPickerCandidateCacheUtc = now;
            return _quickItemPickerCandidateCache;
        }

        private static List<QuickItemInventoryCandidate> BuildQuickItemInventoryCandidatesFromSnapshot()
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
                if (!TerrariaInputCompat.IsSupportedItemUseSlot(slot))
                {
                    continue;
                }

                var item = inventory[slot];
                if (item == null)
                {
                    continue;
                }

                var itemType = item.Type;
                var stack = item.Stack;
                if (itemType <= 0 || stack <= 0)
                {
                    continue;
                }

                var rawName = string.IsNullOrWhiteSpace(item.Name)
                    ? itemType.ToString(CultureInfo.InvariantCulture)
                    : item.Name.Trim();
                var family = ItemSwapFamilyCompat.GetVisibleSwapFamily(itemType);
                if (family != null && family.Length > 0)
                {
                    for (var familyIndex = 0; familyIndex < family.Length; familyIndex++)
                    {
                        var familyType = family[familyIndex];
                        if (familyType <= 0 || !seen.Add(familyType))
                        {
                            continue;
                        }

                        result.Add(new QuickItemInventoryCandidate
                        {
                            ItemType = familyType,
                            ItemName = ItemSwapFamilyCompat.ResolveItemDisplayName(familyType, rawName),
                            Slot = slot,
                            Stack = stack
                        });
                    }

                    continue;
                }

                if (!seen.Add(itemType))
                {
                    continue;
                }

                result.Add(new QuickItemInventoryCandidate
                {
                    ItemType = itemType,
                    ItemName = ItemSwapFamilyCompat.ResolveItemDisplayName(itemType, rawName),
                    Slot = slot,
                    Stack = stack
                });
            }

            return result;
        }
    }
}
