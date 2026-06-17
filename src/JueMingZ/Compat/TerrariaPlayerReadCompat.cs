using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaPlayerReadCompat
    {
        // Typed player reads expose neutral fallbacks for snapshots only; direct
        // writes to life, mana, selection, or movement remain forbidden here.
        private static readonly Item[] EmptyItems = new Item[0];
        private static readonly int[] EmptyBuffs = new int[0];

        public static bool IsActive(Player player)
        {
            return player != null && player.active;
        }

        public static bool IsDead(Player player)
        {
            return player != null && player.dead;
        }

        public static bool IsGhost(Player player)
        {
            return player != null && player.ghost;
        }

        public static bool IsAliveLocalPlayer(Player player)
        {
            return IsActive(player) && !player.dead && !player.ghost;
        }

        public static int WhoAmI(Player player)
        {
            return player == null ? -1 : player.whoAmI;
        }

        public static string Name(Player player)
        {
            return player == null ? string.Empty : player.name ?? string.Empty;
        }

        public static Vector2 Position(Player player)
        {
            return player == null ? Vector2.Zero : player.position;
        }

        public static Vector2 Velocity(Player player)
        {
            return player == null ? Vector2.Zero : player.velocity;
        }

        public static Vector2 Center(Player player)
        {
            return player == null ? Vector2.Zero : player.Center;
        }

        public static Rectangle Hitbox(Player player)
        {
            return player == null ? Rectangle.Empty : player.Hitbox;
        }

        public static int Width(Player player)
        {
            return player == null ? 0 : player.width;
        }

        public static int Height(Player player)
        {
            return player == null ? 0 : player.height;
        }

        public static int Direction(Player player)
        {
            return player == null ? 0 : player.direction;
        }

        public static int SelectedItemSlot(Player player)
        {
            return player == null ? -1 : player.selectedItem;
        }

        public static int ChestIndex(Player player)
        {
            return player == null ? -1 : player.chest;
        }

        public static int CurrentLife(Player player)
        {
            return player == null ? 0 : player.statLife;
        }

        public static int CurrentMana(Player player)
        {
            return player == null ? 0 : player.statMana;
        }

        public static int MaxLife(Player player)
        {
            return player == null ? 0 : player.statLifeMax2;
        }

        public static int MaxMana(Player player)
        {
            return player == null ? 0 : player.statManaMax2;
        }

        public static bool IsWet(Player player)
        {
            return player != null && player.wet;
        }

        public static bool IsLavaWet(Player player)
        {
            return player != null && player.lavaWet;
        }

        public static bool IsHoneyWet(Player player)
        {
            return player != null && player.honeyWet;
        }

        public static bool HasMetalDetector(Player player)
        {
            return player != null && player.accOreFinder;
        }

        public static bool HasLifeformAnalyzer(Player player)
        {
            return player != null && player.accCritterGuide;
        }

        public static bool IsInfoAccessoryHidden(Player player, int index)
        {
            if (player == null || player.hideInfo == null || index < 0 || index >= player.hideInfo.Length)
            {
                return true;
            }

            return player.hideInfo[index];
        }

        public static int ItemAnimation(Player player)
        {
            return player == null ? 0 : player.itemAnimation;
        }

        public static int ItemTime(Player player)
        {
            return player == null ? 0 : player.itemTime;
        }

        public static bool IsControlUseItem(Player player)
        {
            return player != null && player.controlUseItem;
        }

        public static Item[] Inventory(Player player)
        {
            return player == null || player.inventory == null ? EmptyItems : player.inventory;
        }

        public static Item TrashItem(Player player)
        {
            return player == null ? null : player.trashItem;
        }

        public static Item[] Armor(Player player)
        {
            return player == null || player.armor == null ? EmptyItems : player.armor;
        }

        public static Item[] MiscEquips(Player player)
        {
            return player == null || player.miscEquips == null ? EmptyItems : player.miscEquips;
        }

        public static int[] BuffTypes(Player player)
        {
            return player == null || player.buffType == null ? EmptyBuffs : player.buffType;
        }

        public static int[] BuffTimes(Player player)
        {
            return player == null || player.buffTime == null ? EmptyBuffs : player.buffTime;
        }

        public static bool HasActiveBuff(Player player, int buffType)
        {
            if (player == null || player.buffType == null || player.buffTime == null || buffType <= 0)
            {
                return false;
            }

            var count = player.buffType.Length < player.buffTime.Length ? player.buffType.Length : player.buffTime.Length;
            for (var index = 0; index < count; index++)
            {
                if (player.buffType[index] == buffType && player.buffTime[index] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetInventoryItem(Player player, int slot, out Item item)
        {
            item = null;
            var inventory = Inventory(player);
            if (slot < 0 || slot >= inventory.Length)
            {
                return false;
            }

            item = inventory[slot];
            return item != null;
        }

        public static bool TryGetSelectedItem(Player player, out Item item)
        {
            item = null;
            var selectedSlot = SelectedItemSlot(player);
            return TryGetInventoryItem(player, selectedSlot, out item);
        }
    }
}
