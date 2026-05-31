namespace JueMingZ.Automation.Fishing
{
    internal static class FishingAutoEquipmentScorer
    {
        public static bool TryScore(object player, object item, int targetEquipmentSlot, bool expertOrMasterMode, out int score, out string effectGroup, out string reason)
        {
            return TryScore(player, item, targetEquipmentSlot, expertOrMasterMode, FishingLiquidKind.Unknown, out score, out effectGroup, out reason);
        }

        public static bool TryScore(object player, object item, int targetEquipmentSlot, bool expertOrMasterMode, FishingLiquidKind liquidKind, out int score, out string effectGroup, out string reason)
        {
            return FishingEquipmentCatalog.TryScoreItemForSlot(
                player,
                item,
                targetEquipmentSlot,
                expertOrMasterMode,
                liquidKind,
                out score,
                out effectGroup,
                out reason);
        }

        public static int ScoreEquipped(object player, object item, int targetEquipmentSlot, bool expertOrMasterMode)
        {
            return ScoreEquipped(player, item, targetEquipmentSlot, expertOrMasterMode, FishingLiquidKind.Unknown);
        }

        public static int ScoreEquipped(object player, object item, int targetEquipmentSlot, bool expertOrMasterMode, FishingLiquidKind liquidKind)
        {
            return FishingEquipmentCatalog.ScoreEquippedSlot(player, item, targetEquipmentSlot, expertOrMasterMode, liquidKind);
        }
    }
}
