using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        // Temporary equipment plans are reversible mutations; every apply record
        // must be restorable or the rescue path should skip.
        private const int LegEquipmentSlot = 2;
        private const int FirstAccessorySlot = 3;
        private const int MaxEquipmentSlotExclusive = 10;
        private const int FirstSocialArmorSlot = 10;
        private const int FirstSocialAccessorySlot = 13;
        private const int MountMiscEquipSlot = 3;
        private const int TemporaryEquipmentPriority = 2;
        private const int TemporaryUmbrellaPriority = 3;
        private const int TemporaryRocketBootsPrimeRocketTime = 20;
        private const int TemporaryFlyingCarpetPrimeTime = 20;

        public static string ContainerKindName(MovementSafeLandingEquipmentContainerKind kind)
        {
            switch (kind)
            {
                case MovementSafeLandingEquipmentContainerKind.Inventory:
                    return "Inventory";
                case MovementSafeLandingEquipmentContainerKind.SocialAccessory:
                    return "SocialAccessory";
                case MovementSafeLandingEquipmentContainerKind.Accessory:
                    return "Accessory";
                case MovementSafeLandingEquipmentContainerKind.MiscEquip:
                    return "MiscEquip";
                case MovementSafeLandingEquipmentContainerKind.SocialArmor:
                    return "SocialArmor";
                case MovementSafeLandingEquipmentContainerKind.Hotbar:
                    return "Hotbar";
                default:
                    return "Unknown";
            }
        }
    }
}
