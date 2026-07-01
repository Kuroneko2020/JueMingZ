namespace JueMingZ.Config
{
    public static class UnifiedHotkeyBindingIds
    {
        public const string MapQuickAnnouncementTrigger = "map.quick_announcement.trigger";
        public const string WorldAutomationAutoMiningTrigger = "automation.auto_mining.trigger";

        public const string FeatureTogglePrefix = "feature.toggle.";
        public const string BlueprintActionPrefix = "blueprint.action.";
        public const string QuickItemSlotPrefix = "inventory.quick_item.slot";

        public static string ForFeatureToggleTarget(string targetId)
        {
            var normalized = NormalizeIdPart(targetId);
            return normalized.Length <= 0 ? string.Empty : FeatureTogglePrefix + normalized;
        }

        public static string ForBlueprintAction(string targetId)
        {
            var normalized = NormalizeIdPart(targetId);
            return normalized.Length <= 0 ? string.Empty : BlueprintActionPrefix + normalized;
        }

        public static string ForQuickItemSlot(int zeroBasedIndex)
        {
            return zeroBasedIndex < 0 ? string.Empty : QuickItemSlotPrefix + (zeroBasedIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool TryGetFeatureToggleTargetId(string bindingId, out string targetId)
        {
            return TryGetIdPart(bindingId, FeatureTogglePrefix, out targetId);
        }

        public static bool TryGetBlueprintActionTargetId(string bindingId, out string targetId)
        {
            return TryGetIdPart(bindingId, BlueprintActionPrefix, out targetId);
        }

        public static bool TryGetQuickItemSlotNumber(string bindingId, out int oneBasedSlot)
        {
            oneBasedSlot = 0;
            string slotText;
            if (!TryGetIdPart(bindingId, QuickItemSlotPrefix, out slotText))
            {
                return false;
            }

            return int.TryParse(slotText, out oneBasedSlot) && oneBasedSlot > 0;
        }

        private static string NormalizeIdPart(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool TryGetIdPart(string bindingId, string prefix, out string idPart)
        {
            idPart = string.Empty;
            if (string.IsNullOrWhiteSpace(bindingId) ||
                string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            var normalized = bindingId.Trim();
            if (!normalized.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            idPart = normalized.Substring(prefix.Length);
            return idPart.Length > 0;
        }
    }
}
