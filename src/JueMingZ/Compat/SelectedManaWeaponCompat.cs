using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using JueMingZ.GameState;

namespace JueMingZ.Compat
{
    public sealed class SelectedManaWeaponCheck
    {
        public bool IsManaWeapon { get; set; }
        public bool InsufficientMana { get; set; }
        public int CurrentMana { get; set; }
        public int RequiredMana { get; set; }
        public int SelectedItemType { get; set; }
        public string SelectedItemName { get; set; }
        public int SelectedItemManaCost { get; set; }
        public bool CheckManaAvailable { get; set; }
        public bool CheckManaResult { get; set; }
        public bool UsedFallbackManaCostCheck { get; set; }
        public string Reason { get; set; }

        public SelectedManaWeaponCheck()
        {
            SelectedItemName = string.Empty;
            Reason = string.Empty;
        }
    }

    public static class SelectedManaWeaponCompat
    {
        // Mana checks read the selected item only; fallback cost comparison
        // must not spend mana or trigger item use.
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static bool _checkManaResolved;
        private static MethodInfo _checkManaItemAmountPayBlockMethod;
        private static MethodInfo _checkManaItemAmountPayMethod;
        private static MethodInfo _checkManaItemAmountMethod;

        public static SelectedManaWeaponCheck CheckSelectedItem(object player)
        {
            var result = new SelectedManaWeaponCheck();
            if (player == null)
            {
                result.Reason = "playerUnavailable";
                return result;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot) || selectedSlot < 0)
            {
                result.Reason = "selectedSlotUnavailable";
                return result;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                result.Reason = "inventoryUnavailable";
                return result;
            }

            var item = inventory[selectedSlot];
            if (item == null)
            {
                result.Reason = "selectedItemUnavailable";
                return result;
            }

            result.SelectedItemType = ReadInt(item, "type", 0);
            result.SelectedItemName = ReadItemName(item);
            var stack = ReadInt(item, "stack", 0);
            var mana = ReadInt(item, "mana", 0);
            result.SelectedItemManaCost = Math.Max(0, mana);
            result.RequiredMana = result.SelectedItemManaCost;
            result.CurrentMana = ReadInt(player, "statMana", 0);

            if (result.SelectedItemType <= 0 || stack <= 0)
            {
                result.Reason = "selectedItemEmpty";
                return result;
            }

            if (mana <= 0)
            {
                result.Reason = "selectedItemHasNoManaCost";
                return result;
            }

            if (IsNonCombatUtilityItem(item))
            {
                result.Reason = "selectedItemNotManaWeapon";
                return result;
            }

            result.IsManaWeapon = true;
            bool checkManaResult;
            if (TryCheckMana(player, item, out checkManaResult))
            {
                result.CheckManaAvailable = true;
                result.CheckManaResult = checkManaResult;
                result.InsufficientMana = !checkManaResult;
                result.Reason = checkManaResult ? "selectedManaWeaponUsable" : "manaInsufficientForSelectedItem";
                return result;
            }

            result.UsedFallbackManaCostCheck = true;
            result.CheckManaAvailable = false;
            result.CheckManaResult = result.CurrentMana >= result.RequiredMana;
            result.InsufficientMana = !result.CheckManaResult;
            result.Reason = result.InsufficientMana ? "selectedItemManaCostFallback" : "selectedManaWeaponUsableFallback";
            return result;
        }

        private static bool TryCheckMana(object player, object item, out bool enough)
        {
            enough = false;
            if (player == null || item == null)
            {
                return false;
            }

            ResolveCheckManaMethods(player.GetType(), item.GetType());
            try
            {
                if (_checkManaItemAmountPayBlockMethod != null)
                {
                    enough = Convert.ToBoolean(_checkManaItemAmountPayBlockMethod.Invoke(player, new object[] { item, -1, false, true }));
                    return true;
                }

                if (_checkManaItemAmountPayMethod != null)
                {
                    enough = Convert.ToBoolean(_checkManaItemAmountPayMethod.Invoke(player, new object[] { item, -1, false }));
                    return true;
                }

                if (_checkManaItemAmountMethod != null)
                {
                    enough = Convert.ToBoolean(_checkManaItemAmountMethod.Invoke(player, new object[] { item, -1 }));
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static void ResolveCheckManaMethods(Type playerType, Type itemType)
        {
            if (_checkManaResolved)
            {
                return;
            }

            _checkManaResolved = true;
            if (playerType == null || itemType == null)
            {
                return;
            }

            try
            {
                _checkManaItemAmountPayBlockMethod = playerType.GetMethod("CheckMana", InstanceFlags, null, new[] { itemType, typeof(int), typeof(bool), typeof(bool) }, null);
                _checkManaItemAmountPayMethod = playerType.GetMethod("CheckMana", InstanceFlags, null, new[] { itemType, typeof(int), typeof(bool) }, null);
                _checkManaItemAmountMethod = playerType.GetMethod("CheckMana", InstanceFlags, null, new[] { itemType, typeof(int) }, null);
            }
            catch
            {
                _checkManaItemAmountPayBlockMethod = null;
                _checkManaItemAmountPayMethod = null;
                _checkManaItemAmountMethod = null;
            }
        }

        private static bool IsNonCombatUtilityItem(object item)
        {
            if (item == null)
            {
                return true;
            }

            if (ReadInt(item, "createTile", -1) >= 0 || ReadInt(item, "createWall", -1) >= 0)
            {
                return true;
            }

            if (ReadInt(item, "pick", 0) > 0 || ReadInt(item, "axe", 0) > 0 || ReadInt(item, "hammer", 0) > 0 || ReadInt(item, "fishingPole", 0) > 0)
            {
                return true;
            }

            if (ReadInt(item, "ammo", 0) > 0 && ReadInt(item, "useAmmo", 0) <= 0)
            {
                return true;
            }

            var shoot = ReadInt(item, "shoot", 0);
            var damage = ReadInt(item, "damage", 0);
            var magic = ReadBool(item, "magic", false);
            var ranged = ReadBool(item, "ranged", false);
            var thrown = ReadBool(item, "thrown", false);
            var melee = ReadBool(item, "melee", false);
            var useAmmo = ReadInt(item, "useAmmo", 0);
            return damage <= 0 && shoot <= 0 && useAmmo <= 0 && !magic && !ranged && !thrown && !melee;
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(source, name, out value) ? value : fallback;
        }

        private static string ReadItemName(object item)
        {
            try
            {
                var name = GameStateReflection.GetMember(item, "Name") ?? GameStateReflection.GetMember(item, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
