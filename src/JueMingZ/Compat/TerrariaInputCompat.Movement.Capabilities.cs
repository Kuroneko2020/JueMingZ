using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        private static int CountEnabledAirJumpFlags(object player)
        {
            if (player == null || !EnsureAirJumpFields(player))
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < _airJumpFields.Length; index++)
            {
                var field = _airJumpFields[index];
                if (field == null)
                {
                    continue;
                }

                try
                {
                    if (Convert.ToBoolean(field.GetValue(player)))
                    {
                        count++;
                    }
                }
                catch
                {
                }
            }

            return count;
        }

        private static bool EnsureAirJumpFields(object player)
        {
            if (_airJumpFieldsResolved)
            {
                return _airJumpFields.Length > 0;
            }

            _airJumpFieldsResolved = true;
            if (player == null)
            {
                return false;
            }

            try
            {
                var fields = player.GetType().GetFields(InstanceMemberFlags);
                var matches = new List<FieldInfo>();
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    if (field == null || field.FieldType != typeof(bool))
                    {
                        continue;
                    }

                    var name = field.Name ?? string.Empty;
                    if (name.StartsWith("canJumpAgain", StringComparison.Ordinal) ||
                        name.StartsWith("CanJumpAgain", StringComparison.Ordinal))
                    {
                        matches.Add(field);
                    }
                }

                _airJumpFields = matches.ToArray();
                if (_airJumpFields.Length == 0)
                {
                    Logger.Debug("TerrariaInputCompat", "No Player.canJumpAgain_* fields found; air jump detection will be conservative.");
                }

                return _airJumpFields.Length > 0;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Air jump field scan failed: " + error.Message);
                _airJumpFields = new FieldInfo[0];
                return false;
            }
        }

        private static bool TryReadCanUseBootFlyingAbilities(object player, out bool value)
        {
            value = false;
            if (player == null)
            {
                return false;
            }

            if (!_bootFlyingMethodResolved)
            {
                _bootFlyingMethodResolved = true;
                _bootFlyingMethod = player.GetType().GetMethod(
                    "CanUseBootFlyingAbilities",
                    InstanceMemberFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                if (_bootFlyingMethod == null)
                {
                    Logger.Debug("TerrariaInputCompat", "Player.CanUseBootFlyingAbilities() not found; rocket jump detection will use field fallback.");
                }
            }

            if (_bootFlyingMethod == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(_bootFlyingMethod.Invoke(player, null));
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Player.CanUseBootFlyingAbilities() failed: " + error.Message);
                return false;
            }
        }

        private static void ReadMountJumpProfile(object player, JumpInputProfile profile)
        {
            if (player == null || profile == null)
            {
                return;
            }

            var mount = GetMember(player, "mount");
            if (mount == null)
            {
                return;
            }

            bool boolValue;
            int intValue;
            profile.MountActive = TryGetBoolByNames(mount, out boolValue, "Active", "active", "_active") && boolValue;
            profile.MountType = TryGetIntByNames(mount, out intValue, "Type", "type", "_type") ? intValue : -1;

            if (TryGetBoolByNames(mount, out boolValue, "CanFly", "canFly", "_canFly"))
            {
                profile.MountCanFly = boolValue;
                profile.MountCanFlyKnown = true;
            }

            var mountData = GetMember(mount, "_data") ?? GetMember(mount, "data") ?? GetMember(mount, "Data");
            if (mountData != null)
            {
                if (TryGetIntByNames(mountData, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    profile.MountCanFly = true;
                    profile.MountCanFlyKnown = true;
                }

                if (TryGetBoolByNames(mountData, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    profile.MountCanFly = true;
                    profile.MountCanFlyKnown = true;
                }
            }

            if (profile.MountType >= 0 && TryResolveMountNoFallDamage(profile.MountType, out boolValue))
            {
                profile.MountNoFallDamage = boolValue;
                profile.MountNoFallDamageKnown = true;
            }

            profile.HasMountOpportunity = profile.MountActive &&
                                          profile.MountCanFlyKnown &&
                                          profile.MountCanFly &&
                                          profile.AerialJumpWindow;
        }

        private static void ReadEquippedMovementAssistProfile(object player, JumpInputProfile profile)
        {
            if (player == null || profile == null)
            {
                return;
            }

            var miscEquips = GetMember(player, "miscEquips") as IList;
            object item;
            int itemType;
            int mountType;
            bool canFly;

            if (TryGetItemAt(miscEquips, 3, out item) && TryReadItemType(item, out itemType) && itemType > 0)
            {
                profile.EquippedMountItemType = itemType;
                if (TryReadItemMountType(item, out mountType) && mountType >= 0)
                {
                    profile.EquippedMountType = mountType;
                    if (TryResolveMountCanFly(mountType, out canFly))
                    {
                        profile.EquippedMountCanFly = canFly;
                        profile.EquippedMountCanFlyKnown = true;
                    }

                    if (TryResolveMountNoFallDamage(mountType, out bool noFallDamage))
                    {
                        profile.EquippedMountNoFallDamage = noFallDamage;
                        profile.EquippedMountNoFallDamageKnown = true;
                    }
                }
            }

            profile.HasEquippedFlyingMountOpportunity = profile.PlayerControllable &&
                                                        !profile.MountActive &&
                                                        profile.EquippedMountItemType > 0 &&
                                                        profile.EquippedMountCanFlyKnown &&
                                                        profile.EquippedMountCanFly;
            profile.HasEquippedSafeMountOpportunity = profile.PlayerControllable &&
                                                      !profile.MountActive &&
                                                      profile.EquippedMountItemType > 0 &&
                                                      profile.EquippedMountCanFlyKnown &&
                                                      !profile.EquippedMountCanFly &&
                                                      profile.EquippedMountNoFallDamageKnown &&
                                                      profile.EquippedMountNoFallDamage;

            if (TryGetItemAt(miscEquips, 4, out item) &&
                TryReadItemType(item, out itemType) &&
                itemType > 0 &&
                IsGrappleItem(item, itemType))
            {
                profile.HasEquippedGrapple = true;
                profile.EquippedGrappleItemType = itemType;
                profile.EquippedGrappleShootSpeed = TryReadItemShootSpeed(item, out var equippedShootSpeed) ? equippedShootSpeed : 0f;
                profile.EquippedGrappleProjectileType = TryReadItemShoot(item, out var equippedShoot) ? equippedShoot : 0;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory != null)
            {
                var maxQuickGrappleInventorySlot = Math.Min(inventory.Count, 58);
                for (var index = 0; index < maxQuickGrappleInventorySlot; index++)
                {
                    item = inventory[index];
                    if (TryReadItemType(item, out itemType) && itemType > 0 && IsGrappleItem(item, itemType))
                    {
                        profile.HasInventoryGrapple = true;
                        profile.InventoryGrappleItemType = itemType;
                        profile.InventoryGrappleShootSpeed = TryReadItemShootSpeed(item, out var inventoryShootSpeed) ? inventoryShootSpeed : 0f;
                        profile.InventoryGrappleProjectileType = TryReadItemShoot(item, out var inventoryShoot) ? inventoryShoot : 0;
                        break;
                    }
                }
            }

            profile.HasAnyGrapple = profile.HasEquippedGrapple || profile.HasInventoryGrapple;
        }

        private static bool TryGetItemAt(IList items, int index, out object item)
        {
            item = null;
            if (items == null || index < 0 || index >= items.Count)
            {
                return false;
            }

            item = items[index];
            return item != null;
        }

        private static bool TryReadItemType(object item, out int itemType)
        {
            itemType = 0;
            if (item == null)
            {
                return false;
            }

            if (!TryGetIntByNames(item, out itemType, "type", "Type", "netID", "NetID"))
            {
                return false;
            }

            int stack;
            if (TryGetIntByNames(item, out stack, "stack", "Stack") && stack <= 0)
            {
                return false;
            }

            bool isAir;
            if (TryGetBoolByNames(item, out isAir, "IsAir", "isAir") && isAir)
            {
                return false;
            }

            return itemType > 0;
        }

        private static bool TryReadItemShootSpeed(object item, out float shootSpeed)
        {
            return TryGetFloatByNames(item, out shootSpeed, "shootSpeed", "ShootSpeed");
        }

        private static bool TryReadItemShoot(object item, out int shoot)
        {
            return TryGetIntByNames(item, out shoot, "shoot", "Shoot");
        }

        private static bool TryReadItemMountType(object item, out int mountType)
        {
            mountType = -1;
            if (item == null)
            {
                return false;
            }

            if (!TryGetIntByNames(item, out mountType, "mountType", "MountType", "mountId", "MountId"))
            {
                return false;
            }

            return mountType >= 0;
        }

        private static bool TryResolveMountCanFly(int mountType, out bool canFly)
        {
            canFly = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                bool boolValue;
                int intValue;
                float floatValue;
                if (TryGetIntByNames(data, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    canFly = true;
                    return true;
                }

                if (TryGetBoolByNames(data, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    canFly = true;
                    return true;
                }

                if (TryGetFloatByNames(data, out floatValue, "flySpeed", "FlySpeed") && floatValue > 0.1f)
                {
                    canFly = true;
                    return true;
                }

                canFly = false;
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Mount fly detection failed: " + error.Message);
                canFly = false;
                return false;
            }
        }

        private static bool TryResolveMountNoFallDamage(int mountType, out bool noFallDamage)
        {
            noFallDamage = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                float fallDamage;
                if (TryGetFloatByNames(data, out fallDamage, "fallDamage", "FallDamage"))
                {
                    noFallDamage = fallDamage <= 0.001f;
                    return true;
                }

                return false;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Mount no-fall detection failed: " + error.Message);
                noFallDamage = false;
                return false;
            }
        }

        private static bool IsGrappleItem(object item, int itemType)
        {
            if (itemType <= 0)
            {
                return false;
            }

            int shoot;
            if (TryGetIntByNames(item, out shoot, "shoot", "Shoot") && IsHookProjectile(shoot))
            {
                return true;
            }

            try
            {
                var setsType = FindType("Terraria.ID.ItemID+Sets");
                var flags = setsType == null ? null : GetStatic(setsType, "IsAGrapplingHook") as Array;
                if (flags != null && itemType >= 0 && itemType < flags.Length)
                {
                    var raw = flags.GetValue(itemType);
                    if (raw is bool)
                    {
                        return (bool)raw;
                    }
                }
            }
            catch
            {
            }

            if (TryGetIntByNames(item, out shoot, "shoot", "Shoot") && shoot > 0)
            {
                var name = ReadItemName(item);
                if (ContainsGrappleNameHint(name))
                {
                    return true;
                }
            }

            return ContainsGrappleNameHint(ReadItemName(item));
        }

        private static bool IsHookProjectile(int projectileType)
        {
            if (projectileType <= 0)
            {
                return false;
            }

            try
            {
                var mainType = TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
                var flags = mainType == null ? null : GetStatic(mainType, "projHook") as Array;
                if (flags == null || projectileType < 0 || projectileType >= flags.Length)
                {
                    return false;
                }

                var raw = flags.GetValue(projectileType);
                return raw is bool && (bool)raw;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadItemName(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var value = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            return value == null ? string.Empty : value.ToString();
        }

        private static bool ContainsGrappleNameHint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.IndexOf("hook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("grapple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("钩", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("抓", StringComparison.Ordinal) >= 0;
        }
    }
}
