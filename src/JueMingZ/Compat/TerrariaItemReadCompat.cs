using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaItemReadCompat
    {
        // Read-only item adapter; neutral fallbacks support snapshot skips, not
        // proof that an item exists or should be mutated.
        public static bool IsActive(Item item)
        {
            return item != null && item.type > 0 && item.stack > 0;
        }

        public static bool IsAir(Item item)
        {
            return !IsActive(item);
        }

        public static int Type(Item item)
        {
            return item == null ? 0 : item.type;
        }

        public static int Stack(Item item)
        {
            return item == null ? 0 : item.stack;
        }

        public static string Name(Item item)
        {
            return item == null ? string.Empty : item.Name ?? string.Empty;
        }

        public static int Prefix(Item item)
        {
            return item == null ? 0 : item.prefix;
        }

        public static bool IsFavorited(Item item)
        {
            return item != null && item.favorited;
        }

        public static int Damage(Item item)
        {
            return item == null ? 0 : item.damage;
        }

        public static int UseStyle(Item item)
        {
            return item == null ? 0 : item.useStyle;
        }

        public static int UseTime(Item item)
        {
            return item == null ? 0 : item.useTime;
        }

        public static int UseAnimation(Item item)
        {
            return item == null ? 0 : item.useAnimation;
        }

        public static int PickPower(Item item)
        {
            return item == null ? 0 : item.pick;
        }

        public static int AxePower(Item item)
        {
            return item == null ? 0 : item.axe;
        }

        public static int HammerPower(Item item)
        {
            return item == null ? 0 : item.hammer;
        }

        public static int TileBoost(Item item)
        {
            return item == null ? 0 : item.tileBoost;
        }

        public static int BaitPower(Item item)
        {
            return item == null ? 0 : item.bait;
        }

        public static int HealLife(Item item)
        {
            return item == null ? 0 : item.healLife;
        }

        public static int HealMana(Item item)
        {
            return item == null ? 0 : item.healMana;
        }

        public static int BuffType(Item item)
        {
            return item == null ? 0 : item.buffType;
        }

        public static int BuffTime(Item item)
        {
            return item == null ? 0 : item.buffTime;
        }

        public static int CatchTool(Item item)
        {
            // Terraria 1.4.5.6 has no Item.catchTool member; callers map known bug-net item types.
            return 0;
        }

        public static int MaxStack(Item item)
        {
            return item == null ? 0 : item.maxStack;
        }

        public static int CreateTile(Item item)
        {
            return item == null ? -1 : item.createTile;
        }

        public static int CreateWall(Item item)
        {
            return item == null ? -1 : item.createWall;
        }

        public static bool IsConsumable(Item item)
        {
            return item != null && item.consumable;
        }

        public static bool IsPotion(Item item)
        {
            return item != null && item.potion;
        }

        public static bool IsAccessory(Item item)
        {
            return item != null && item.accessory;
        }

        public static int WingSlot(Item item)
        {
            return item == null ? -1 : item.wingSlot;
        }

        public static int Defense(Item item)
        {
            return item == null ? 0 : item.defense;
        }

        public static bool IsStackable(Item item)
        {
            return IsActive(item) && item.maxStack > 1;
        }

        public static bool IsUsable(Item item)
        {
            return IsActive(item) && item.useStyle > 0 && item.useAnimation > 0 && item.useTime >= 0;
        }

        public static bool IsPickaxe(Item item)
        {
            return IsActive(item) && item.pick > 0;
        }

        public static bool IsHealingPotionCandidate(Item item)
        {
            return IsActive(item) && item.healLife > 0;
        }

        public static bool IsManaPotionCandidate(Item item)
        {
            return IsActive(item) && item.healMana > 0;
        }

        public static bool IsBuffPotionCandidate(Item item)
        {
            return IsActive(item) && item.buffType > 0 && item.buffTime > 0;
        }
    }
}
