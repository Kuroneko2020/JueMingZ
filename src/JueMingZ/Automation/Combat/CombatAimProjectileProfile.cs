using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Combat
{
    public sealed class CombatAimProjectileProfile
    {
        public bool Resolved { get; set; }
        public int WeaponItemType { get; set; }
        public string WeaponName { get; set; }
        public int WeaponDamage { get; set; }
        public int WeaponUseTime { get; set; }
        public int WeaponUseAnimation { get; set; }
        public int WeaponUseStyle { get; set; }
        public bool WeaponChannel { get; set; }
        public int WeaponShootProjectileType { get; set; }
        public string WeaponShootProjectileName { get; set; }
        public float WeaponShootSpeed { get; set; }
        public int WeaponUseAmmo { get; set; }
        public string WeaponDamageTypeName { get; set; }
        public bool WeaponRanged { get; set; }
        public bool WeaponMagic { get; set; }
        public bool WeaponThrown { get; set; }
        public bool WeaponMelee { get; set; }
        public bool AmmoAvailable { get; set; }
        public int AmmoSlot { get; set; }
        public int AmmoItemType { get; set; }
        public string AmmoItemName { get; set; }
        public int AmmoType { get; set; }
        public int AmmoProjectileType { get; set; }
        public string AmmoProjectileName { get; set; }
        public float AmmoShootSpeed { get; set; }
        public int AmmoDamage { get; set; }
        public float AmmoKnockBack { get; set; }
        public bool AmmoArrowLike { get; set; }
        public bool AmmoBulletLike { get; set; }
        public int ProjectileType { get; set; }
        public string ProjectileName { get; set; }
        public int ProjectileAiStyle { get; set; }
        public int ProjectileExtraUpdates { get; set; }
        public bool ProjectileDefaultsAvailable { get; set; }
        public bool ProjectileNoGravity { get; set; }
        public bool ProjectileArrow { get; set; }
        public bool ProjectileTileCollide { get; set; }
        public int ProjectileWidth { get; set; }
        public int ProjectileHeight { get; set; }
        public bool ProjectileFriendly { get; set; }
        public bool ProjectileHostile { get; set; }
        public float BaseProjectileSpeed { get; set; }
        public float EffectiveProjectileSpeed { get; set; }
        public int EffectiveUpdatesPerTick { get; set; }
        public float GravityPerTickCandidate { get; set; }
        public float ProjectileRadiusForHit { get; set; }
        public string ProfileFamilyHint { get; set; }
        public string ProfileCompleteness { get; set; }
        public string ProfileFallbackReason { get; set; }
        public string ProfileSpeedSource { get; set; }
        public bool GunProj { get; set; }
        public bool AmmoSpeedApplied { get; set; }
        public bool MagicQuiverApplied { get; set; }
        public bool ArcheryApplied { get; set; }
        public bool ArcherySpeedCapped { get; set; }
        public bool MagicQuiverEffectiveUpdateApplied { get; set; }
        public bool SpecificLauncherAmmoProjectileMatch { get; set; }
        public bool RocketProjectileTransform { get; set; }
        public bool SolutionProjectileTransform { get; set; }
        public string ProjectileTransformRole { get; set; }
        public string ResolvedProjectileRole { get; set; }
        public int PrimaryProjectileType { get; set; }
        public string PrimaryProjectileName { get; set; }
        public string PrimaryProjectileRole { get; set; }

        public CombatAimProjectileProfile()
        {
            WeaponName = string.Empty;
            WeaponShootProjectileName = string.Empty;
            WeaponDamageTypeName = string.Empty;
            AmmoSlot = -1;
            AmmoItemName = string.Empty;
            AmmoProjectileName = string.Empty;
            ProjectileName = string.Empty;
            ProfileFamilyHint = "Unknown";
            ProfileCompleteness = "unknown";
            ProfileFallbackReason = string.Empty;
            ProfileSpeedSource = string.Empty;
            ProjectileTransformRole = string.Empty;
            ResolvedProjectileRole = string.Empty;
            PrimaryProjectileName = string.Empty;
            PrimaryProjectileRole = string.Empty;
            EffectiveUpdatesPerTick = 1;
        }
    }

    public static class CombatAimProjectileProfileResolver
    {
        private const int MainInventoryEndExclusive = 54;
        private const int CoinInventoryStart = 50;
        private const int CoinInventoryEndExclusive = 54;
        private const int AmmoInventoryStart = 54;
        private const int AmmoInventoryEndExclusive = 58;
        private const float DefaultProjectileSpeed = 8f;

        private static readonly object CacheSync = new object();
        private static readonly Dictionary<int, ProjectileDefaults> ProjectileDefaultsCache = new Dictionary<int, ProjectileDefaults>();
        private static readonly Dictionary<string, int> StaticIdCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool[]> ItemSetBoolCache = new Dictionary<string, bool[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, bool[]> AmmoSetBoolCache = new Dictionary<string, bool[]>(StringComparer.Ordinal);

        public static CombatAimProjectileProfile Resolve(object player, CombatAimWeaponProfile weapon)
        {
            var profile = new CombatAimProjectileProfile();
            try
            {
                if (weapon == null)
                {
                    MarkUnavailable(profile, "weaponProfileUnavailable");
                    return profile;
                }

                FillWeapon(profile, weapon);
                AmmoSnapshot ammo;
                var hasAmmo = TryFindAmmo(player, weapon.UseAmmo, weapon.CoinAmmoType, out ammo);
                FillAmmo(profile, hasAmmo ? ammo : null);

                profile.GunProj = ReadItemSetFlag("gunProj", weapon.ItemType);
                profile.ProjectileType = ResolveProjectileType(player, weapon, hasAmmo ? ammo : null, profile);
                var defaults = ResolveProjectileDefaults(profile.ProjectileType);
                FillProjectileDefaults(profile, defaults);
                FillProjectileRole(profile);

                profile.AmmoArrowLike = IsArrowLikeAmmo(weapon, hasAmmo ? ammo : null, defaults);
                profile.AmmoBulletLike = IsBulletLikeAmmo(weapon, hasAmmo ? ammo : null);
                ResolveSpeeds(player, weapon, hasAmmo ? ammo : null, defaults, profile);
                profile.GravityPerTickCandidate = ResolveGravityCandidate(defaults, profile);
                profile.ProjectileRadiusForHit = Math.Max(1f, Math.Max(profile.ProjectileWidth, profile.ProjectileHeight) / 2f);
                profile.ProfileFamilyHint = ResolveFamilyHint(weapon, defaults, profile);

                if (profile.ProjectileType <= 0)
                {
                    profile.ProfileCompleteness = "none";
                    profile.ProfileFallbackReason = "noProjectileSemantics";
                }
                else if (defaults.HasValue)
                {
                    profile.ProfileCompleteness = "complete";
                    profile.ProfileFallbackReason = string.Empty;
                }
                else
                {
                    profile.ProfileCompleteness = "degraded";
                    profile.ProfileFallbackReason = "projectileDefaultsUnavailable";
                }

                profile.Resolved = true;
                return profile;
            }
            catch (Exception error)
            {
                profile.Resolved = false;
                profile.ProfileCompleteness = "failed";
                profile.ProfileFallbackReason = "profileFailed:" + error.Message;
                LogThrottle.WarnThrottled(
                    "combat-aim-projectile-profile-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimProjectileProfileResolver",
                    "Combat aim projectile profile failed: " + error.Message);
                return profile;
            }
        }

        private static void MarkUnavailable(CombatAimProjectileProfile profile, string reason)
        {
            profile.Resolved = false;
            profile.ProfileCompleteness = "unavailable";
            profile.ProfileFallbackReason = reason ?? string.Empty;
            profile.ProfileSpeedSource = "unavailable";
        }

        private static void FillWeapon(CombatAimProjectileProfile profile, CombatAimWeaponProfile weapon)
        {
            profile.WeaponItemType = weapon.ItemType;
            profile.WeaponName = weapon.Name ?? string.Empty;
            profile.WeaponDamage = weapon.Damage;
            profile.WeaponUseTime = weapon.UseTime;
            profile.WeaponUseAnimation = weapon.UseAnimation;
            profile.WeaponUseStyle = weapon.UseStyle;
            profile.WeaponChannel = weapon.Channel;
            profile.WeaponShootProjectileType = weapon.Shoot;
            profile.WeaponShootSpeed = weapon.ShootSpeed;
            profile.WeaponUseAmmo = weapon.UseAmmo;
            profile.WeaponDamageTypeName = weapon.DamageTypeName ?? string.Empty;
            profile.WeaponRanged = weapon.Ranged;
            profile.WeaponMagic = weapon.Magic;
            profile.WeaponThrown = weapon.Thrown;
            profile.WeaponMelee = weapon.Melee;
            if (weapon.Shoot > 0)
            {
                var weaponDefaults = ResolveProjectileDefaults(weapon.Shoot);
                profile.WeaponShootProjectileName = weaponDefaults.Name ?? string.Empty;
            }
        }

        private static void FillAmmo(CombatAimProjectileProfile profile, AmmoSnapshot ammo)
        {
            if (ammo == null)
            {
                return;
            }

            profile.AmmoAvailable = true;
            profile.AmmoSlot = ammo.Slot;
            profile.AmmoItemType = ammo.ItemType;
            profile.AmmoItemName = ammo.ItemName ?? string.Empty;
            profile.AmmoType = ammo.AmmoType;
            profile.AmmoProjectileType = ammo.Shoot;
            profile.AmmoShootSpeed = ammo.ShootSpeed;
            profile.AmmoDamage = ammo.Damage;
            profile.AmmoKnockBack = ammo.KnockBack;
            if (ammo.Shoot > 0)
            {
                var ammoDefaults = ResolveProjectileDefaults(ammo.Shoot);
                profile.AmmoProjectileName = ammoDefaults.Name ?? string.Empty;
            }
        }

        private static void FillProjectileDefaults(CombatAimProjectileProfile profile, ProjectileDefaults defaults)
        {
            profile.ProjectileName = defaults.Name ?? string.Empty;
            profile.ProjectileAiStyle = defaults.AiStyle;
            profile.ProjectileExtraUpdates = defaults.ExtraUpdates;
            profile.ProjectileDefaultsAvailable = defaults.HasValue;
            profile.ProjectileNoGravity = defaults.NoGravity;
            profile.ProjectileArrow = defaults.Arrow;
            profile.ProjectileTileCollide = defaults.TileCollide;
            profile.ProjectileWidth = defaults.Width;
            profile.ProjectileHeight = defaults.Height;
            profile.ProjectileFriendly = defaults.Friendly;
            profile.ProjectileHostile = defaults.Hostile;
        }

        private static void FillProjectileRole(CombatAimProjectileProfile profile)
        {
            if (profile.ProjectileType > 0 &&
                profile.AmmoProjectileType > 0 &&
                profile.ProjectileType == profile.AmmoProjectileType)
            {
                profile.ResolvedProjectileRole = "ammoProjectile";
                profile.PrimaryProjectileType = profile.ProjectileType;
                profile.PrimaryProjectileName = profile.ProjectileName ?? string.Empty;
                profile.PrimaryProjectileRole = "ammoPrimary";
                return;
            }

            if (profile.ProjectileType > 0 &&
                profile.WeaponShootProjectileType > 0 &&
                profile.ProjectileType == profile.WeaponShootProjectileType)
            {
                profile.ResolvedProjectileRole = "weaponShootProjectile";
                profile.PrimaryProjectileType = profile.ProjectileType;
                profile.PrimaryProjectileName = profile.ProjectileName ?? string.Empty;
                profile.PrimaryProjectileRole = "weaponPrimary";
                return;
            }

            profile.ResolvedProjectileRole = profile.ProjectileType > 0 ? "resolvedProjectile" : "none";
            profile.PrimaryProjectileType = profile.ProjectileType;
            profile.PrimaryProjectileName = profile.ProjectileName ?? string.Empty;
            profile.PrimaryProjectileRole = profile.ProjectileType > 0 ? "resolvedPrimary" : "none";
        }

        private static int ResolveProjectileType(
            object player,
            CombatAimWeaponProfile weapon,
            AmmoSnapshot ammo,
            CombatAimProjectileProfile profile)
        {
            if (weapon == null)
            {
                return 0;
            }

            var projectileType = weapon.Shoot;
            if (ammo == null)
            {
                return projectileType;
            }

            int matchedProjectile;
            if (TryResolveSpecificLauncherAmmoProjectile(weapon.ItemType, ammo.ItemType, out matchedProjectile))
            {
                profile.SpecificLauncherAmmoProjectileMatch = true;
                projectileType = matchedProjectile;
            }
            else if (weapon.UseAmmo == ReadAmmoId("Rocket"))
            {
                profile.RocketProjectileTransform = true;
                projectileType += ammo.Shoot;
            }
            else if (weapon.UseAmmo == ReadAmmoId("Solution"))
            {
                profile.SolutionProjectileTransform = true;
                projectileType += ammo.Shoot;
            }
            else if (ammo.Shoot > 0)
            {
                projectileType = ammo.Shoot;
            }

            if (ReadPlayerBool(player, "hasMoltenQuiver") && projectileType == ReadProjectileId("WoodenArrowFriendly", 1))
            {
                profile.ProjectileTransformRole = "moltenQuiverArrowTransform";
            }

            return projectileType;
        }

        private static void ResolveSpeeds(
            object player,
            CombatAimWeaponProfile weapon,
            AmmoSnapshot ammo,
            ProjectileDefaults defaults,
            CombatAimProjectileProfile profile)
        {
            var speed = weapon == null || weapon.ShootSpeed <= 0f ? DefaultProjectileSpeed : weapon.ShootSpeed;
            profile.ProfileSpeedSource = weapon == null || weapon.ShootSpeed <= 0f ? "default" : "weapon";

            if (profile.GunProj)
            {
                profile.ProfileSpeedSource = "weaponGunProjOnly";
            }
            else if (ammo != null && ammo.ShootSpeed > 0f)
            {
                speed += ammo.ShootSpeed;
                profile.AmmoSpeedApplied = true;
                profile.ProfileSpeedSource = profile.ProfileSpeedSource + "+ammo";
            }

            if (!profile.GunProj && IsMagicQuiverActive(player) && IsQuiverAmmo(weapon))
            {
                speed *= 1.1f;
                profile.MagicQuiverApplied = true;
                profile.ProfileSpeedSource = profile.ProfileSpeedSource + "+magicQuiver";
            }

            if (!profile.GunProj && IsArcheryActive(player) && IsAmmoSetArrow(profile.AmmoType > 0 ? profile.AmmoType : profile.WeaponUseAmmo) && speed < 20f)
            {
                speed *= 1.2f;
                if (speed > 20f)
                {
                    speed = 20f;
                    profile.ArcherySpeedCapped = true;
                }

                profile.ArcheryApplied = true;
                profile.ProfileSpeedSource = profile.ProfileSpeedSource + "+archery";
            }

            if (speed < 0.5f)
            {
                speed = DefaultProjectileSpeed;
                profile.ProfileSpeedSource = "default";
            }

            profile.BaseProjectileSpeed = speed;
            var effectiveUpdates = defaults.ExtraUpdates > 0 ? defaults.ExtraUpdates + 1 : 1;
            // Magic Quiver effectively grants friendly arrows at least one
            // extra update; keep defaults separate so diagnostics can explain both facts.
            if (profile.MagicQuiverApplied && defaults.Arrow && defaults.Friendly && effectiveUpdates < 2)
            {
                effectiveUpdates = 2;
                profile.MagicQuiverEffectiveUpdateApplied = true;
            }

            profile.EffectiveUpdatesPerTick = effectiveUpdates < 1 ? 1 : effectiveUpdates;
            profile.EffectiveProjectileSpeed = speed * profile.EffectiveUpdatesPerTick;
        }

        private static float ResolveGravityCandidate(ProjectileDefaults defaults, CombatAimProjectileProfile profile)
        {
            if (!defaults.HasValue || defaults.NoGravity)
            {
                return 0f;
            }

            if (profile != null && profile.AmmoArrowLike)
            {
                return 0.1f;
            }

            if (defaults.AiStyle == 16 || defaults.AiStyle == 68)
            {
                return 0.2f;
            }

            if (defaults.AiStyle == 2)
            {
                return 0.14f;
            }

            return 0f;
        }

        private static string ResolveFamilyHint(
            CombatAimWeaponProfile weapon,
            ProjectileDefaults defaults,
            CombatAimProjectileProfile profile)
        {
            if (weapon == null || profile == null || profile.ProjectileType <= 0)
            {
                return "Unknown";
            }

            CombatAimSpecialWeaponRule specialRule;
            if (CombatAimSpecialWeaponRuleResolver.TryResolve(weapon, out specialRule))
            {
                if (string.Equals(specialRule.Kind, "cursorSpawnBurst", StringComparison.Ordinal) ||
                    string.Equals(specialRule.Kind, "dualProjectileSpread", StringComparison.Ordinal) ||
                    string.Equals(specialRule.Kind, "spreadMultiShot", StringComparison.Ordinal) ||
                    string.Equals(specialRule.Kind, "parallelMultiShot", StringComparison.Ordinal))
                {
                    return "SpreadOrMultiShot";
                }

                if (string.Equals(specialRule.Kind, "guidedCursor", StringComparison.Ordinal))
                {
                    return "GuidedCursor";
                }
            }

            if (weapon.Channel && defaults.AiStyle == 15)
            {
                return "ReleaseControlled";
            }

            if (defaults.AiStyle == 3)
            {
                return "Returning";
            }

            if (defaults.AiStyle == 99)
            {
                return "GuidedCursor";
            }

            if (defaults.AiStyle == 4 || defaults.AiStyle == 84)
            {
                return "InstantOrBeam";
            }

            if (profile.AmmoArrowLike || profile.GravityPerTickCandidate > 0f)
            {
                return "GravityArc";
            }

            if (profile.AmmoBulletLike || profile.EffectiveProjectileSpeed >= 14f)
            {
                return "HighSpeedLinear";
            }

            if (weapon.Magic && profile.BaseProjectileSpeed <= 12f)
            {
                return "SlowLinear";
            }

            return profile.EffectiveProjectileSpeed >= 8f ? "MediumLinear" : "SlowLinear";
        }

        private static bool TryFindAmmo(object player, int ammoType, int coinAmmoType, out AmmoSnapshot ammo)
        {
            ammo = null;
            if (player == null || ammoType <= 0)
            {
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null)
            {
                return false;
            }

            if (ammoType == coinAmmoType &&
                TryFindAmmoInRange(inventory, ammoType, true, CoinInventoryStart, CoinInventoryEndExclusive, out ammo))
            {
                return true;
            }

            if (TryFindAmmoInRange(inventory, ammoType, false, AmmoInventoryStart, AmmoInventoryEndExclusive, out ammo))
            {
                return true;
            }

            return TryFindAmmoInRange(inventory, ammoType, ammoType == coinAmmoType, 0, MainInventoryEndExclusive, out ammo);
        }

        private static bool TryFindAmmoInRange(IList inventory, int ammoType, bool allowCoinItemType, int start, int endExclusive, out AmmoSnapshot ammo)
        {
            ammo = null;
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

                var itemType = ReadInt(item, "type", 0);
                var stack = ReadInt(item, "stack", 0);
                if (itemType <= 0 || stack <= 0)
                {
                    continue;
                }

                var itemAmmo = ReadInt(item, "ammo", 0);
                if (itemAmmo != ammoType && !(allowCoinItemType && IsVanillaCoinItemType(itemType)))
                {
                    continue;
                }

                ammo = new AmmoSnapshot
                {
                    Slot = index,
                    ItemType = itemType,
                    ItemName = ReadItemName(item),
                    AmmoType = itemAmmo,
                    Shoot = ReadInt(item, "shoot", 0),
                    ShootSpeed = ReadFloat(item, "shootSpeed", 0f),
                    Damage = ReadInt(item, "damage", 0),
                    KnockBack = ReadFloat(item, "knockBack", 0f)
                };
                return true;
            }

            return false;
        }

        private static ProjectileDefaults ResolveProjectileDefaults(int projectileType)
        {
            if (projectileType <= 0)
            {
                return ProjectileDefaults.Empty;
            }

            lock (CacheSync)
            {
                ProjectileDefaults cached;
                if (ProjectileDefaultsCache.TryGetValue(projectileType, out cached))
                {
                    return cached;
                }
            }

            var resolved = ReadProjectileDefaults(projectileType);
            lock (CacheSync)
            {
                ProjectileDefaultsCache[projectileType] = resolved;
            }

            return resolved;
        }

        private static ProjectileDefaults ReadProjectileDefaults(int projectileType)
        {
            try
            {
                var projectileTypeObject = FindType("Terraria.Projectile");
                if (projectileTypeObject == null)
                {
                    return ProjectileDefaults.Empty;
                }

                var projectile = Activator.CreateInstance(projectileTypeObject);
                var setDefaults = projectileTypeObject.GetMethod("SetDefaults", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(int) }, null);
                if (setDefaults == null)
                {
                    return ProjectileDefaults.Empty;
                }

                setDefaults.Invoke(projectile, new object[] { projectileType });
                return new ProjectileDefaults
                {
                    HasValue = true,
                    Name = ReadProjectileName(projectile),
                    ExtraUpdates = ReadInt(projectile, "extraUpdates", 0),
                    Arrow = ReadBool(projectile, "arrow", false),
                    AiStyle = ReadInt(projectile, "aiStyle", 0),
                    Width = ReadInt(projectile, "width", 2),
                    Height = ReadInt(projectile, "height", 2),
                    TileCollide = ReadBool(projectile, "tileCollide", true),
                    NoGravity = ReadBool(projectile, "noGravity", false),
                    Friendly = ReadBool(projectile, "friendly", false),
                    Hostile = ReadBool(projectile, "hostile", false)
                };
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "combat-aim-projectile-defaults-failed-" + projectileType.ToString(CultureInfo.InvariantCulture),
                    TimeSpan.FromSeconds(30),
                    "CombatAimProjectileProfileResolver",
                    "Projectile defaults unavailable for type " + projectileType.ToString(CultureInfo.InvariantCulture) + ": " + error.Message);
                return ProjectileDefaults.Empty;
            }
        }

        private static bool TryResolveSpecificLauncherAmmoProjectile(int launcherItemType, int ammoItemType, out int projectileType)
        {
            projectileType = 0;
            var setsType = FindType("Terraria.ID.AmmoID+Sets");
            var rawMatches = setsType == null ? null : GameStateReflection.GetStaticMember(setsType, "SpecificLauncherAmmoProjectileMatches");
            object launcherMatches;
            if (!TryReadDictionaryValue(rawMatches, launcherItemType, out launcherMatches))
            {
                return false;
            }

            object projectile;
            if (!TryReadDictionaryValue(launcherMatches, ammoItemType, out projectile))
            {
                return false;
            }

            try
            {
                projectileType = Convert.ToInt32(projectile, CultureInfo.InvariantCulture);
                return projectileType > 0;
            }
            catch
            {
                projectileType = 0;
                return false;
            }
        }

        private static bool TryReadDictionaryValue(object dictionary, int key, out object value)
        {
            value = null;
            var enumerable = dictionary as IEnumerable;
            if (enumerable == null)
            {
                return false;
            }

            foreach (var entry in enumerable)
            {
                var currentKey = GameStateReflection.GetMember(entry, "Key");
                if (currentKey == null)
                {
                    continue;
                }

                int intKey;
                try
                {
                    intKey = Convert.ToInt32(currentKey, CultureInfo.InvariantCulture);
                }
                catch
                {
                    continue;
                }

                if (intKey != key)
                {
                    continue;
                }

                value = GameStateReflection.GetMember(entry, "Value");
                return value != null;
            }

            return false;
        }

        private static bool IsMagicQuiverActive(object player)
        {
            return ReadPlayerBool(player, "magicQuiver");
        }

        private static bool IsArcheryActive(object player)
        {
            if (ReadPlayerBool(player, "archery"))
            {
                return true;
            }

            return HasActiveBuff(player, ReadBuffId("Archery", 16));
        }

        private static bool IsQuiverAmmo(CombatAimWeaponProfile weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            return weapon.UseAmmo == ReadAmmoId("Arrow") ||
                   weapon.UseAmmo == ReadAmmoId("Stake");
        }

        private static bool IsArrowLikeAmmo(CombatAimWeaponProfile weapon, AmmoSnapshot ammo, ProjectileDefaults defaults)
        {
            if (defaults.HasValue && defaults.Arrow)
            {
                return true;
            }

            var weaponUseAmmo = weapon == null ? 0 : weapon.UseAmmo;
            if (weaponUseAmmo > 0 && IsAmmoSetArrow(weaponUseAmmo))
            {
                return true;
            }

            return ammo != null && IsAmmoSetArrow(ammo.AmmoType);
        }

        private static bool IsBulletLikeAmmo(CombatAimWeaponProfile weapon, AmmoSnapshot ammo)
        {
            var weaponUseAmmo = weapon == null ? 0 : weapon.UseAmmo;
            if (weaponUseAmmo > 0 && IsAmmoSetBullet(weaponUseAmmo))
            {
                return true;
            }

            return ammo != null && IsAmmoSetBullet(ammo.AmmoType);
        }

        private static bool IsAmmoSetArrow(int ammoType)
        {
            if (ammoType <= 0)
            {
                return false;
            }

            if (ReadAmmoSetFlag("IsArrow", ammoType))
            {
                return true;
            }

            return ammoType == ReadAmmoId("Arrow") || ammoType == ReadAmmoId("Stake");
        }

        private static bool IsAmmoSetBullet(int ammoType)
        {
            if (ammoType <= 0)
            {
                return false;
            }

            if (ReadAmmoSetFlag("IsBullet", ammoType))
            {
                return true;
            }

            return ammoType == ReadAmmoId("Bullet") || ammoType == ReadAmmoId("CandyCorn");
        }

        private static bool HasActiveBuff(object player, int buffId)
        {
            if (player == null || buffId <= 0)
            {
                return false;
            }

            var buffTypes = GameStateReflection.AsList(GameStateReflection.GetMember(player, "buffType"));
            var buffTimes = GameStateReflection.AsList(GameStateReflection.GetMember(player, "buffTime"));
            if (buffTypes == null)
            {
                return false;
            }

            for (var index = 0; index < buffTypes.Count; index++)
            {
                int current;
                try
                {
                    current = Convert.ToInt32(buffTypes[index], CultureInfo.InvariantCulture);
                }
                catch
                {
                    continue;
                }

                if (current != buffId)
                {
                    continue;
                }

                if (buffTimes == null || index >= buffTimes.Count)
                {
                    return true;
                }

                try
                {
                    return Convert.ToInt32(buffTimes[index], CultureInfo.InvariantCulture) > 0;
                }
                catch
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReadPlayerBool(object player, string name)
        {
            bool value;
            return GameStateReflection.TryGetBool(player, name, out value) && value;
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

        private static string ReadProjectileName(object projectile)
        {
            try
            {
                var name = GameStateReflection.GetMember(projectile, "Name") ??
                           GameStateReflection.GetMember(projectile, "name");
                return name == null ? string.Empty : Convert.ToString(name, CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int ReadAmmoId(string memberName)
        {
            return ResolveStaticInt("Terraria.ID.AmmoID", memberName);
        }

        private static int ReadBuffId(string memberName, int fallback)
        {
            var value = ResolveStaticInt("Terraria.ID.BuffID", memberName);
            return value > 0 ? value : fallback;
        }

        private static int ReadProjectileId(string memberName, int fallback)
        {
            var value = ResolveStaticInt("Terraria.ID.ProjectileID", memberName);
            return value > 0 ? value : fallback;
        }

        private static int ResolveStaticInt(string typeName, string memberName)
        {
            var key = typeName + "." + memberName;
            lock (CacheSync)
            {
                int cached;
                if (StaticIdCache.TryGetValue(key, out cached))
                {
                    return cached;
                }
            }

            var value = 0;
            try
            {
                var type = FindType(typeName);
                if (type != null)
                {
                    var raw = GameStateReflection.GetStaticMember(type, memberName);
                    if (raw != null)
                    {
                        value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                    }
                }
            }
            catch
            {
                value = 0;
            }

            lock (CacheSync)
            {
                StaticIdCache[key] = value;
            }

            return value;
        }

        private static bool ReadItemSetFlag(string memberName, int index)
        {
            if (index < 0)
            {
                return false;
            }

            var boolArray = ResolveBoolArray("Terraria.ID.ItemID+Sets", memberName, ItemSetBoolCache);
            return boolArray != null && index < boolArray.Length && boolArray[index];
        }

        private static bool ReadAmmoSetFlag(string memberName, int index)
        {
            if (index < 0)
            {
                return false;
            }

            var boolArray = ResolveBoolArray("Terraria.ID.AmmoID+Sets", memberName, AmmoSetBoolCache);
            return boolArray != null && index < boolArray.Length && boolArray[index];
        }

        private static bool[] ResolveBoolArray(string typeName, string memberName, Dictionary<string, bool[]> cache)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            lock (CacheSync)
            {
                bool[] cached;
                if (cache.TryGetValue(memberName, out cached))
                {
                    return cached;
                }
            }

            bool[] resolved = null;
            try
            {
                var type = FindType(typeName);
                if (type != null)
                {
                    resolved = GameStateReflection.GetStaticMember(type, memberName) as bool[];
                }
            }
            catch
            {
                resolved = null;
            }

            lock (CacheSync)
            {
                cache[memberName] = resolved;
            }

            return resolved;
        }

        private static Type FindType(string fullName)
        {
            return TerrariaTypeCache.Find(fullName);
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

        private static bool IsVanillaCoinItemType(int itemType)
        {
            return itemType >= 71 && itemType <= 74;
        }

        private sealed class AmmoSnapshot
        {
            public int Slot;
            public int ItemType;
            public string ItemName;
            public int AmmoType;
            public int Shoot;
            public float ShootSpeed;
            public int Damage;
            public float KnockBack;
        }

        private struct ProjectileDefaults
        {
            public static readonly ProjectileDefaults Empty = new ProjectileDefaults();

            public bool HasValue;
            public string Name;
            public int ExtraUpdates;
            public bool Arrow;
            public int AiStyle;
            public int Width;
            public int Height;
            public bool TileCollide;
            public bool NoGravity;
            public bool Friendly;
            public bool Hostile;
        }
    }
}
