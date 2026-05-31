using System;
using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using Terraria;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimWeaponProfile
    {
        private const int FallbackCoinGunItemType = 905;
        private const int FallbackCoinAmmoType = 71;
        private const int CoinInventoryStart = 50;
        private const int CoinInventoryEndExclusive = 54;
        private const int AmmoInventoryStart = 54;
        private const int AmmoInventoryEndExclusive = 58;

        private static bool _coinGunTypeResolved;
        private static int _coinGunType;
        private static bool _coinGunFallbackLogged;
        private static bool _coinAmmoTypeResolved;
        private static int _coinAmmoType;
        private static bool _coinAmmoFallbackLogged;
        private static bool _shootsOnUseReleaseResolved;
        private static bool[] _shootsOnUseRelease;

        public int ItemType { get; private set; }
        public int Stack { get; private set; }
        public string Name { get; private set; }
        public int Damage { get; private set; }
        public int Shoot { get; private set; }
        public float ShootSpeed { get; private set; }
        public int UseAmmo { get; private set; }
        public int Ammo { get; private set; }
        public int UseStyle { get; private set; }
        public int UseTime { get; private set; }
        public int UseAnimation { get; private set; }
        public int ReuseDelay { get; private set; }
        public int Mana { get; private set; }
        public int BuffType { get; private set; }
        public int CreateTile { get; private set; }
        public int CreateWall { get; private set; }
        public int Pick { get; private set; }
        public int Axe { get; private set; }
        public int Hammer { get; private set; }
        public int FishingPole { get; private set; }
        public bool Melee { get; private set; }
        public bool Ranged { get; private set; }
        public bool Magic { get; private set; }
        public bool Summon { get; private set; }
        public bool Thrown { get; private set; }
        public bool Sentry { get; private set; }
        public bool Consumable { get; private set; }
        public bool Channel { get; private set; }
        public bool NoMelee { get; private set; }
        public bool NoUseGraphic { get; private set; }
        public bool ShootsOnUseRelease { get; private set; }
        public string DamageTypeName { get; private set; }
        public bool DamageTypeSummonLike { get; private set; }
        public bool IsAmmoItem { get; private set; }
        public bool IsCoinGun { get; private set; }
        public int CoinGunType { get; private set; }
        public int CoinAmmoType { get; private set; }
        public bool CoinAmmoAvailable { get; private set; }
        public int CoinAmmoSlot { get; private set; }
        public int CoinAmmoItemType { get; private set; }
        public int CoinAmmoStack { get; private set; }
        public string Classification { get; private set; }

        public bool IsEmpty
        {
            get { return ItemType <= 0 || Stack <= 0; }
        }

        public bool IsPlacementItem
        {
            get { return CreateTile >= 0 || CreateWall >= 0; }
        }

        public bool IsToolOrFishingItem
        {
            get { return Pick > 0 || Axe > 0 || Hammer > 0 || FishingPole > 0; }
        }

        public bool IsSentryPlacementWeapon
        {
            get { return Sentry; }
        }

        public bool IsSummonPlacementWeapon
        {
            get { return BuffType > 0 && (Summon || DamageTypeSummonLike); }
        }

        public bool HasWeaponUseSemantics
        {
            get { return Shoot > 0 || UseAmmo > 0 || Melee || Ranged || Magic || Thrown; }
        }

        public CombatAimWeaponProfile()
        {
            Name = string.Empty;
            DamageTypeName = string.Empty;
            Classification = string.Empty;
            CreateTile = -1;
            CreateWall = -1;
            CoinAmmoSlot = -1;
        }

        public static CombatAimWeaponProfile Read(object player, object item)
        {
            CombatAimWeaponProfile typedProfile;
            if (IsLateBootstrapCompleted() && TryReadTyped(player, item, out typedProfile))
            {
                return typedProfile;
            }

            var profile = new CombatAimWeaponProfile();
            if (item == null)
            {
                profile.Classification = "missingItem";
                return profile;
            }

            profile.ItemType = ReadInt(item, "type", 0);
            profile.Stack = ReadInt(item, "stack", 0);
            profile.Name = ReadItemName(item);
            profile.Damage = ReadInt(item, "damage", 0);
            profile.Shoot = ReadInt(item, "shoot", 0);
            profile.ShootSpeed = ReadFloat(item, "shootSpeed", 0f);
            profile.UseAmmo = ReadInt(item, "useAmmo", 0);
            profile.Ammo = ReadInt(item, "ammo", 0);
            profile.UseStyle = ReadInt(item, "useStyle", 0);
            profile.UseTime = ReadInt(item, "useTime", 0);
            profile.UseAnimation = ReadInt(item, "useAnimation", 0);
            profile.ReuseDelay = ReadInt(item, "reuseDelay", 0);
            profile.Mana = ReadInt(item, "mana", 0);
            profile.BuffType = ReadInt(item, "buffType", 0);
            profile.CreateTile = ReadInt(item, "createTile", -1);
            profile.CreateWall = ReadInt(item, "createWall", -1);
            profile.Pick = ReadInt(item, "pick", 0);
            profile.Axe = ReadInt(item, "axe", 0);
            profile.Hammer = ReadInt(item, "hammer", 0);
            profile.FishingPole = ReadInt(item, "fishingPole", 0);
            profile.Melee = ReadBool(item, "melee", false);
            profile.Ranged = ReadBool(item, "ranged", false);
            profile.Magic = ReadBool(item, "magic", false);
            profile.Summon = ReadBool(item, "summon", false);
            profile.Thrown = ReadBool(item, "thrown", false);
            profile.Sentry = ReadBoolAny(item, "sentry", "Sentry");
            profile.Consumable = ReadBool(item, "consumable", false);
            profile.Channel = ReadBool(item, "channel", false);
            profile.NoMelee = ReadBool(item, "noMelee", false);
            profile.NoUseGraphic = ReadBool(item, "noUseGraphic", false);
            profile.ShootsOnUseRelease = ReadItemSetBool("ShootsOnUseRelease", profile.ItemType);
            profile.DamageTypeName = ReadDamageTypeName(item);
            profile.DamageTypeSummonLike = ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Summon") ||
                                           ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Minion") ||
                                           ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Sentry");
            profile.IsAmmoItem = profile.Ammo > 0 && profile.UseAmmo <= 0;
            profile.CoinGunType = ResolveCoinGunType();
            profile.CoinAmmoType = ResolveCoinAmmoType();
            profile.IsCoinGun = profile.ItemType == profile.CoinGunType ||
                                (ContainsOrdinalIgnoreCase(profile.Name, "Coin Gun") && profile.UseAmmo == profile.CoinAmmoType);

            if (profile.IsCoinGun)
            {
                profile.CoinAmmoAvailable = TryFindCoinAmmo(player, profile.CoinAmmoType, out var coinSlot, out var coinItemType, out var coinStack);
                profile.CoinAmmoSlot = coinSlot;
                profile.CoinAmmoItemType = coinItemType;
                profile.CoinAmmoStack = coinStack;
            }

            profile.Classification = ResolveClassification(profile);
            return profile;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool TryReadTyped(object player, object item, out CombatAimWeaponProfile profile)
        {
            profile = null;
            var typedItem = item as Item;
            if (typedItem == null)
            {
                return false;
            }

            var typedPlayer = player as Player;
            profile = new CombatAimWeaponProfile();
            profile.ItemType = typedItem.type;
            profile.Stack = typedItem.stack;
            profile.Name = typedItem.Name ?? string.Empty;
            profile.Damage = typedItem.damage;
            profile.Shoot = typedItem.shoot;
            profile.ShootSpeed = typedItem.shootSpeed;
            profile.UseAmmo = typedItem.useAmmo;
            profile.Ammo = typedItem.ammo;
            profile.UseStyle = typedItem.useStyle;
            profile.UseTime = typedItem.useTime;
            profile.UseAnimation = typedItem.useAnimation;
            profile.ReuseDelay = typedItem.reuseDelay;
            profile.Mana = typedItem.mana;
            profile.BuffType = typedItem.buffType;
            profile.CreateTile = typedItem.createTile;
            profile.CreateWall = typedItem.createWall;
            profile.Pick = typedItem.pick;
            profile.Axe = typedItem.axe;
            profile.Hammer = typedItem.hammer;
            profile.FishingPole = typedItem.fishingPole;
            profile.Melee = typedItem.melee;
            profile.Ranged = typedItem.ranged;
            profile.Magic = typedItem.magic;
            profile.Summon = typedItem.summon;
            profile.Thrown = ReadBool(typedItem, "thrown", false);
            profile.Sentry = ReadBoolAny(typedItem, "sentry", "Sentry");
            profile.Consumable = typedItem.consumable;
            profile.Channel = typedItem.channel;
            profile.NoMelee = typedItem.noMelee;
            profile.NoUseGraphic = typedItem.noUseGraphic;
            profile.ShootsOnUseRelease = ReadItemSetBool("ShootsOnUseRelease", profile.ItemType);
            profile.DamageTypeName = ReadDamageTypeName(typedItem);
            profile.DamageTypeSummonLike = ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Summon") ||
                                           ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Minion") ||
                                           ContainsOrdinalIgnoreCase(profile.DamageTypeName, "Sentry");
            profile.IsAmmoItem = profile.Ammo > 0 && profile.UseAmmo <= 0;
            profile.CoinGunType = ResolveCoinGunType();
            profile.CoinAmmoType = ResolveCoinAmmoType();
            profile.IsCoinGun = profile.ItemType == profile.CoinGunType ||
                                (ContainsOrdinalIgnoreCase(profile.Name, "Coin Gun") && profile.UseAmmo == profile.CoinAmmoType);

            if (profile.IsCoinGun)
            {
                profile.CoinAmmoAvailable = TryFindCoinAmmo(typedPlayer, profile.CoinAmmoType, out var coinSlot, out var coinItemType, out var coinStack);
                profile.CoinAmmoSlot = coinSlot;
                profile.CoinAmmoItemType = coinItemType;
                profile.CoinAmmoStack = coinStack;
            }

            profile.Classification = ResolveClassification(profile);
            return true;
        }

        private static bool IsLateBootstrapCompleted()
        {
            try
            {
                return JueMingZRuntime.State != null && JueMingZRuntime.State.LateBootstrapCompleted;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveClassification(CombatAimWeaponProfile profile)
        {
            if (profile == null || profile.IsEmpty)
            {
                return "empty";
            }

            if (profile.IsCoinGun)
            {
                return "coinGun";
            }

            if (profile.IsSentryPlacementWeapon)
            {
                return "sentryPlacement";
            }

            if (profile.IsSummonPlacementWeapon)
            {
                return "summonPlacement";
            }

            if (profile.IsPlacementItem)
            {
                return "tileOrWallPlacement";
            }

            if (profile.IsToolOrFishingItem)
            {
                return "toolOrFishing";
            }

            if (profile.IsAmmoItem)
            {
                return "ammoItem";
            }

            if (profile.Melee)
            {
                return profile.Shoot > 0 ? "meleeProjectile" : "melee";
            }

            if (profile.Ranged || profile.UseAmmo > 0)
            {
                return "ranged";
            }

            if (profile.Magic)
            {
                return "magic";
            }

            if (profile.Thrown)
            {
                return "thrown";
            }

            return profile.Shoot > 0 ? "projectileWeapon" : "unknownDamageItem";
        }

        private static bool TryFindCoinAmmo(object player, int coinAmmoType, out int slot, out int itemType, out int stack)
        {
            slot = -1;
            itemType = 0;
            stack = 0;
            if (player == null)
            {
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null)
            {
                return false;
            }

            if (TryFindCoinAmmoInRange(inventory, coinAmmoType, CoinInventoryStart, CoinInventoryEndExclusive, out slot, out itemType, out stack))
            {
                return true;
            }

            if (TryFindCoinAmmoInRange(inventory, coinAmmoType, AmmoInventoryStart, AmmoInventoryEndExclusive, out slot, out itemType, out stack))
            {
                return true;
            }

            return TryFindCoinAmmoInRange(inventory, coinAmmoType, 0, CoinInventoryStart, out slot, out itemType, out stack);
        }

        private static bool TryFindCoinAmmo(Player player, int coinAmmoType, out int slot, out int itemType, out int stack)
        {
            slot = -1;
            itemType = 0;
            stack = 0;
            if (player == null || player.inventory == null)
            {
                return false;
            }

            var inventory = player.inventory;
            if (TryFindCoinAmmoInRange(inventory, coinAmmoType, CoinInventoryStart, CoinInventoryEndExclusive, out slot, out itemType, out stack))
            {
                return true;
            }

            if (TryFindCoinAmmoInRange(inventory, coinAmmoType, AmmoInventoryStart, AmmoInventoryEndExclusive, out slot, out itemType, out stack))
            {
                return true;
            }

            return TryFindCoinAmmoInRange(inventory, coinAmmoType, 0, CoinInventoryStart, out slot, out itemType, out stack);
        }

        private static bool TryFindCoinAmmoInRange(IList inventory, int coinAmmoType, int start, int endExclusive, out int slot, out int itemType, out int stack)
        {
            slot = -1;
            itemType = 0;
            stack = 0;
            if (inventory == null || start < 0)
            {
                return false;
            }

            var end = inventory.Count < endExclusive ? inventory.Count : endExclusive;
            for (var index = start; index < end; index++)
            {
                var item = inventory[index];
                if (item == null)
                {
                    continue;
                }

                var currentType = ReadInt(item, "type", 0);
                var currentStack = ReadInt(item, "stack", 0);
                var ammo = ReadInt(item, "ammo", 0);
                if (currentType <= 0 || currentStack <= 0)
                {
                    continue;
                }

                if (ammo == coinAmmoType || IsVanillaCoinItemType(currentType))
                {
                    slot = index;
                    itemType = currentType;
                    stack = currentStack;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindCoinAmmoInRange(Item[] inventory, int coinAmmoType, int start, int endExclusive, out int slot, out int itemType, out int stack)
        {
            slot = -1;
            itemType = 0;
            stack = 0;
            if (inventory == null || start < 0)
            {
                return false;
            }

            var end = inventory.Length < endExclusive ? inventory.Length : endExclusive;
            for (var index = start; index < end; index++)
            {
                var item = inventory[index];
                if (item == null)
                {
                    continue;
                }

                var currentType = item.type;
                var currentStack = item.stack;
                var ammo = item.ammo;
                if (currentType <= 0 || currentStack <= 0)
                {
                    continue;
                }

                if (ammo == coinAmmoType || IsVanillaCoinItemType(currentType))
                {
                    slot = index;
                    itemType = currentType;
                    stack = currentStack;
                    return true;
                }
            }

            return false;
        }

        private static bool IsVanillaCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private static int ResolveCoinGunType()
        {
            if (_coinGunTypeResolved)
            {
                return _coinGunType;
            }

            _coinGunType = ResolveStaticInt("Terraria.ID.ItemID", "CoinGun", FallbackCoinGunItemType, ref _coinGunFallbackLogged, "CoinGun");
            _coinGunTypeResolved = true;
            return _coinGunType;
        }

        private static int ResolveCoinAmmoType()
        {
            if (_coinAmmoTypeResolved)
            {
                return _coinAmmoType;
            }

            _coinAmmoType = ResolveStaticInt("Terraria.ID.AmmoID", "Coin", FallbackCoinAmmoType, ref _coinAmmoFallbackLogged, "AmmoID.Coin");
            _coinAmmoTypeResolved = true;
            return _coinAmmoType;
        }

        private static int ResolveStaticInt(string typeName, string memberName, int fallback, ref bool fallbackLogged, string label)
        {
            try
            {
                var type = FindType(typeName);
                if (type != null)
                {
                    var raw = GameStateReflection.GetStaticMember(type, memberName);
                    if (raw != null)
                    {
                        return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-weapon-profile-id-reflection-" + label,
                    TimeSpan.FromSeconds(30),
                    "CombatAimWeaponProfile",
                    label + " reflection failed: " + error.Message);
            }

            if (!fallbackLogged)
            {
                fallbackLogged = true;
                Logger.Warn(
                    "CombatAimWeaponProfile",
                    label + " unavailable; using fallback id " + fallback.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return fallback;
        }

        private static bool ReadItemSetBool(string memberName, int itemType)
        {
            if (itemType < 0)
            {
                return false;
            }

            try
            {
                if (!_shootsOnUseReleaseResolved)
                {
                    _shootsOnUseReleaseResolved = true;
                    var setsType = FindType("Terraria.ID.ItemID+Sets");
                    _shootsOnUseRelease = setsType == null
                        ? null
                        : GameStateReflection.GetStaticMember(setsType, memberName) as bool[];
                }

                return _shootsOnUseRelease != null &&
                       itemType < _shootsOnUseRelease.Length &&
                       _shootsOnUseRelease[itemType];
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-weapon-profile-item-set-" + memberName,
                    TimeSpan.FromSeconds(30),
                    "CombatAimWeaponProfile",
                    "ItemID.Sets." + memberName + " reflection failed: " + error.Message);
                return false;
            }
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
        }

        private static string ReadDamageTypeName(object item)
        {
            try
            {
                var raw = GameStateReflection.GetMember(item, "DamageType");
                if (raw == null)
                {
                    return string.Empty;
                }

                var typeName = Convert.ToString(GameStateReflection.GetMember(raw, "Name"), CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    return typeName;
                }

                var fullName = raw.GetType() == null ? string.Empty : raw.GetType().FullName;
                if (!string.IsNullOrWhiteSpace(fullName))
                {
                    return fullName;
                }

                return Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ReadInt(object source, string name, int fallback)
        {
            int value;
            return GameStateReflection.TryGetInt(source, name, out value) ? value : fallback;
        }

        private static float ReadFloat(object source, string name, float fallback)
        {
            float value;
            return GameStateReflection.TryGetFloat(source, name, out value) ? value : fallback;
        }

        private static bool ReadBool(object source, string name, bool fallback)
        {
            bool value;
            return GameStateReflection.TryGetBool(source, name, out value) ? value : fallback;
        }

        private static bool ReadBoolAny(object source, string firstName, string secondName)
        {
            bool value;
            if (GameStateReflection.TryGetBool(source, firstName, out value))
            {
                return value;
            }

            return GameStateReflection.TryGetBool(source, secondName, out value) && value;
        }

        private static string ReadItemName(object item)
        {
            try
            {
                var name = GameStateReflection.GetMember(item, "Name") ??
                           GameStateReflection.GetMember(item, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool ContainsOrdinalIgnoreCase(string value, string pattern)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(pattern) &&
                   value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
