using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class TerrariaDashCompat
    {
        private const BindingFlags InstanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags StaticMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, int> ItemIdCache = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> MountIdCache = new Dictionary<string, int>(StringComparer.Ordinal);

        private static Guid _queuedRequestId = Guid.Empty;
        private static int _queuedDirection;
        private static string _queuedMode = string.Empty;
        private static long _queuedUntilTick;
        private static DateTime _queuedExpiresUtc = DateTime.MinValue;

        private static Guid _immediatePulseRequestId = Guid.Empty;
        private static long _immediatePulseTick;

        private static bool _dashMovementHookInstalled;
        private static string _dashMovementHookMessage = string.Empty;
        private static string _lastDashCompatError = string.Empty;
        private static bool _lastPulseApplySucceeded;
        private static int _lastPulseApplyDirection;
        private static DateTime? _lastPulseApplyUtc;
        private static string _lastPulseApplyMessage = string.Empty;
        private static bool _lastPulseWasFallback;
        private static string _lastPulseResetMessage = string.Empty;

        public static bool DashMovementHookInstalled { get { lock (SyncRoot) { return _dashMovementHookInstalled; } } }
        public static string DashMovementHookMessage { get { lock (SyncRoot) { return _dashMovementHookMessage; } } }
        public static string LastDashCompatError { get { lock (SyncRoot) { return _lastDashCompatError; } } }
        public static bool LastPulseApplySucceeded { get { lock (SyncRoot) { return _lastPulseApplySucceeded; } } }
        public static int LastPulseApplyDirection { get { lock (SyncRoot) { return _lastPulseApplyDirection; } } }
        public static DateTime? LastPulseApplyUtc { get { lock (SyncRoot) { return _lastPulseApplyUtc; } } }
        public static string LastPulseApplyMessage { get { lock (SyncRoot) { return _lastPulseApplyMessage; } } }
        public static bool LastPulseWasFallback { get { lock (SyncRoot) { return _lastPulseWasFallback; } } }
        public static string LastPulseResetMessage { get { lock (SyncRoot) { return _lastPulseResetMessage; } } }

        public static void MarkDashMovementHookResult(bool installed, string message)
        {
            lock (SyncRoot)
            {
                _dashMovementHookInstalled = installed;
                _dashMovementHookMessage = message ?? string.Empty;
            }
        }

        public static bool HasQueuedContinuousDashPulse()
        {
            lock (SyncRoot)
            {
                ClearExpiredQueuedPulseLocked();
                return _queuedRequestId != Guid.Empty;
            }
        }

        public static void ClearQueuedContinuousDashPulse(string reason)
        {
            lock (SyncRoot)
            {
                _queuedRequestId = Guid.Empty;
                _queuedDirection = 0;
                _queuedMode = string.Empty;
                _queuedUntilTick = 0;
                _queuedExpiresUtc = DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    _lastDashCompatError = reason;
                }
            }
        }

        public static bool TryReadDashInputProfile(object player, out DashInputProfile profile)
        {
            profile = new DashInputProfile();
            if (player == null)
            {
                return Fail("Cannot read dash profile: player unavailable.");
            }

            try
            {
                bool boolValue;
                int intValue;
                profile.PlayerActive = !TryGetBool(player, "active", out boolValue) || boolValue;
                profile.PlayerDead = TryGetBool(player, "dead", out boolValue) && boolValue;
                profile.PlayerGhost = TryGetBool(player, "ghost", out boolValue) && boolValue;
                profile.PlayerCrowdControlled = TryGetBool(player, "CCed", out boolValue) && boolValue;
                profile.PlayerNoItems = TryGetBool(player, "noItems", out boolValue) && boolValue;
                profile.PlayerFrozen = TryGetBool(player, "frozen", out boolValue) && boolValue;
                profile.PlayerStoned = TryGetBool(player, "stoned", out boolValue) && boolValue;
                profile.PlayerWebbed = TryGetBool(player, "webbed", out boolValue) && boolValue;
                profile.ControlLeft = TryGetBool(player, "controlLeft", out boolValue) && boolValue;
                profile.ControlRight = TryGetBool(player, "controlRight", out boolValue) && boolValue;
                profile.ControlDash = TryGetBool(player, "controlDash", out boolValue) && boolValue;
                profile.ReleaseDash = TryGetBool(player, "releaseDash", out boolValue) && boolValue;
                profile.HeldDirection = profile.ControlLeft == profile.ControlRight ? 0 : profile.ControlRight ? 1 : -1;
                profile.CurrentDirection = TryGetInt(player, "direction", out intValue) ? NormalizeDirection(intValue) : 0;
                profile.DashDelay = TryGetInt(player, "dashDelay", out intValue) ? intValue : -1;
                profile.DashType = TryGetInt(player, "dashType", out intValue) ? intValue : 0;
                profile.HasCurrentDashType = profile.DashType > 0;
                profile.DashCooldownReady = profile.DashDelay == 0;

                ReadMountDashProfile(player, profile);
                ResolveVanillaDashAbility(player, profile);
                profile.CapabilitySummary =
                    "dashType=" + profile.DashType.ToString(CultureInfo.InvariantCulture) +
                    ",fallbackDashType=" + profile.FallbackDashType.ToString(CultureInfo.InvariantCulture) +
                    ",source=" + profile.DashAbilitySource +
                    ",mountActive=" + BoolText(profile.MountActive) +
                    ",mountCanDash=" + BoolText(profile.MountCanDash) +
                    ",cooldownReady=" + BoolText(profile.DashCooldownReady);
                return ClearError();
            }
            catch (Exception error)
            {
                return Fail("Read dash profile failed: " + error.Message);
            }
        }

        public static bool TryRequestContinuousDashPulse(Guid requestId, int direction, string mode, long currentTick, out string message)
        {
            message = string.Empty;
            var normalized = NormalizeDirection(direction);
            if (requestId == Guid.Empty || normalized == 0)
            {
                message = "Cannot queue dash pulse: request id or direction was invalid.";
                return Fail(message);
            }

            if (currentTick <= 0)
            {
                TerrariaInputCompat.TryReadGameUpdateCount(out currentTick);
            }

            lock (SyncRoot)
            {
                _queuedRequestId = requestId;
                _queuedDirection = normalized;
                _queuedMode = MovementContinuousDashModes.Normalize(mode);
                _queuedUntilTick = currentTick + 3;
                _queuedExpiresUtc = DateTime.UtcNow.AddMilliseconds(250);
                _lastDashCompatError = string.Empty;
            }

            message = "Dash pulse queued for DashMovement prefix.";
            return true;
        }

        public static bool TryApplyQueuedContinuousDashPulseBeforeDashMovement(object player, out DashPulseApplyResult result)
        {
            result = new DashPulseApplyResult();
            Guid requestId;
            int direction;
            string mode;
            lock (SyncRoot)
            {
                ClearExpiredQueuedPulseLocked();
                if (_queuedRequestId == Guid.Empty)
                {
                    result.Message = "No queued dash pulse.";
                    return false;
                }

                requestId = _queuedRequestId;
                direction = _queuedDirection;
                mode = _queuedMode;
                _queuedRequestId = Guid.Empty;
                _queuedDirection = 0;
                _queuedMode = string.Empty;
                _queuedUntilTick = 0;
                _queuedExpiresUtc = DateTime.MinValue;
            }

            var applied = TryApplyDashPulse(player, requestId, direction, mode, false, out result);
            if (result != null)
            {
                result.Queued = true;
            }

            return applied;
        }

        public static bool TryApplyImmediateContinuousDashPulse(object player, Guid requestId, int direction, string mode, out DashPulseApplyResult result)
        {
            long tick;
            TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            lock (SyncRoot)
            {
                _immediatePulseRequestId = requestId;
                _immediatePulseTick = tick;
            }

            return TryApplyDashPulse(player, requestId, NormalizeDirection(direction), mode, true, out result);
        }

        public static void ResetDashPulseAfterDashMovement(object player, DashPulseApplyResult applied)
        {
            if (player == null || applied == null || !applied.Applied)
            {
                return;
            }

            var ok = TrySetMember(player, "controlDash", false);
            ok &= TrySetMember(player, "releaseDash", true);
            lock (SyncRoot)
            {
                _lastPulseResetMessage = ok
                    ? "DashMovement postfix reset controlDash=false, releaseDash=true."
                    : "DashMovement postfix reset failed: " + _lastDashCompatError;
            }
        }

        public static void ResetImmediatePulseIfStale(object player)
        {
            Guid requestId;
            long pulseTick;
            lock (SyncRoot)
            {
                requestId = _immediatePulseRequestId;
                pulseTick = _immediatePulseTick;
                if (requestId == Guid.Empty)
                {
                    return;
                }
            }

            long currentTick;
            if (!TerrariaInputCompat.TryReadGameUpdateCount(out currentTick) || currentTick <= pulseTick)
            {
                return;
            }

            var ok = player != null &&
                     TrySetMember(player, "controlDash", false) &&
                     TrySetMember(player, "releaseDash", true);
            lock (SyncRoot)
            {
                if (_immediatePulseRequestId == requestId)
                {
                    _immediatePulseRequestId = Guid.Empty;
                    _immediatePulseTick = 0;
                    _lastPulseResetMessage = ok
                        ? "Fallback dash pulse reset controlDash=false, releaseDash=true."
                        : "Fallback dash pulse reset skipped or failed.";
                }
            }
        }

        public static void ResetAllPulseState(object player, string reason)
        {
            var shouldResetPlayerInput = false;
            lock (SyncRoot)
            {
                shouldResetPlayerInput = _queuedRequestId != Guid.Empty || _immediatePulseRequestId != Guid.Empty;
                _queuedRequestId = Guid.Empty;
                _queuedDirection = 0;
                _queuedMode = string.Empty;
                _queuedUntilTick = 0;
                _queuedExpiresUtc = DateTime.MinValue;
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    _lastDashCompatError = reason;
                }
            }

            if (shouldResetPlayerInput && player != null)
            {
                TrySetMember(player, "controlDash", false);
                TrySetMember(player, "releaseDash", true);
            }

            lock (SyncRoot)
            {
                _immediatePulseRequestId = Guid.Empty;
                _immediatePulseTick = 0;
                _lastPulseResetMessage = reason ?? string.Empty;
            }
        }

        private static bool TryApplyDashPulse(object player, Guid requestId, int direction, string mode, bool fallback, out DashPulseApplyResult result)
        {
            result = new DashPulseApplyResult
            {
                RequestId = requestId,
                Direction = direction,
                Mode = MovementContinuousDashModes.Normalize(mode)
            };

            if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                result.Message = "Dash pulse skipped: local player unavailable.";
                RecordPulse(false, direction, result.Message, fallback);
                return Fail(result.Message);
            }

            DashInputProfile before;
            if (!TryReadDashInputProfile(player, out before))
            {
                result.Message = "Dash pulse skipped: " + LastDashCompatError;
                RecordPulse(false, direction, result.Message, fallback);
                return false;
            }

            result.BeforeProfile = before;
            if (!before.IsDirectionHeld(direction))
            {
                result.Message = "Dash pulse skipped: held direction changed.";
                RecordPulse(false, direction, result.Message, fallback);
                return Fail(result.Message);
            }

            if (!before.CanDashInDirection(direction))
            {
                result.Message = "Dash pulse skipped: dash is not ready.";
                RecordPulse(false, direction, result.Message, fallback);
                return Fail(result.Message);
            }

            string dashTypeMessage;
            if (!EnsureDashType(player, before, out dashTypeMessage))
            {
                result.Message = "Dash pulse skipped: " + dashTypeMessage;
                RecordPulse(false, direction, result.Message, fallback);
                return Fail(result.Message);
            }

            int beforeDirection;
            int afterDirection;
            string directionMethod;
            var directionOk = TerrariaInputCompat.TryChangePlayerDirection(player, direction, true, out beforeDirection, out afterDirection, out directionMethod);
            var ok = directionOk;
            ok &= TrySetMember(player, "releaseDash", true);
            ok &= TrySetMember(player, "controlDash", true);

            DashInputProfile after;
            TryReadDashInputProfile(player, out after);
            result.AfterProfile = after;
            result.Applied = ok;
            result.Message = ok
                ? "Dash pulse applied before DashMovement via " + directionMethod + ". " + dashTypeMessage
                : "Dash pulse apply failed: " + LastDashCompatError;
            RecordPulse(ok, direction, result.Message, fallback);
            return ok;
        }

        private static bool EnsureDashType(object player, DashInputProfile profile, out string message)
        {
            message = "dashType already available.";
            if (player == null || profile == null)
            {
                message = "player or profile unavailable.";
                return false;
            }

            if (profile.DashType > 0)
            {
                return true;
            }

            if (profile.MountActive && profile.MountCanDashKnown && profile.MountCanDash)
            {
                message = "dashType unavailable; using vanilla dash-capable mount context.";
                return true;
            }

            if (profile.FallbackDashType <= 0)
            {
                message = "no safe fallback dashType is available.";
                return false;
            }

            var ok = TrySetMember(player, "dashType", profile.FallbackDashType);
            message = ok
                ? "dashType restored from verified vanilla source: " + profile.FallbackDashType.ToString(CultureInfo.InvariantCulture) + "."
                : "dashType fallback write failed: " + LastDashCompatError;
            return ok;
        }

        private static void ResolveVanillaDashAbility(object player, DashInputProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (profile.MountActive)
            {
                profile.MountAllowsDashContext = profile.MountCanDashKnown && profile.MountCanDash;
                profile.HasMountDash = profile.MountAllowsDashContext;
                profile.HasDashAbility = profile.MountAllowsDashContext && (profile.HasCurrentDashType || profile.HasMountDash);
                profile.DashAbilitySource = profile.HasDashAbility ? "mount:" + profile.MountType.ToString(CultureInfo.InvariantCulture) : "mountBlocked";
                profile.FallbackDashType = ResolveIntrinsicMountDashType(profile.MountType);
                return;
            }

            profile.MountAllowsDashContext = true;
            if (profile.HasCurrentDashType)
            {
                profile.HasDashAbility = true;
                profile.DashAbilitySource = "dashType:" + profile.DashType.ToString(CultureInfo.InvariantCulture);
                return;
            }

            int fallbackType;
            string source;
            if (TryResolveEquippedAccessoryDashType(player, out fallbackType, out source))
            {
                profile.HasAccessoryDash = true;
                profile.HasDashAbility = true;
                profile.FallbackDashType = fallbackType;
                profile.DashAbilitySource = source;
                return;
            }

            if (TryResolveEquippedArmorDashType(player, out fallbackType, out source))
            {
                profile.HasArmorDash = true;
                profile.HasDashAbility = true;
                profile.FallbackDashType = fallbackType;
                profile.DashAbilitySource = source;
                return;
            }

            profile.HasDashAbility = false;
            profile.DashAbilitySource = "none";
        }

        private static bool TryResolveEquippedAccessoryDashType(object player, out int dashType, out string source)
        {
            dashType = 0;
            source = string.Empty;
            var armor = GetMember(player, "armor") as IList;
            if (armor == null)
            {
                return false;
            }

            var tabi = ResolveItemId("Tabi");
            var masterNinjaGear = ResolveItemId("MasterNinjaGear");
            var eocShield = ResolveItemId("EoCShield");
            var count = Math.Min(10, armor.Count);
            for (var index = 3; index < count; index++)
            {
                bool usable;
                if (!FishingLoadoutCompat.TryIsItemSlotUnlockedAndUsable(player, index, out usable) || !usable)
                {
                    continue;
                }

                var item = armor[index];
                int type;
                int stack;
                if (!TryGetInt(item, "type", out type) || type <= 0 ||
                    (TryGetInt(item, "stack", out stack) && stack <= 0))
                {
                    continue;
                }

                if (type == tabi || type == masterNinjaGear)
                {
                    dashType = 1;
                    source = type == tabi ? "accessory:Tabi" : "accessory:MasterNinjaGear";
                    return true;
                }

                if (type == eocShield)
                {
                    dashType = 2;
                    source = "accessory:EoCShield";
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveEquippedArmorDashType(object player, out int dashType, out string source)
        {
            dashType = 0;
            source = string.Empty;
            var armor = GetMember(player, "armor") as IList;
            if (armor == null || armor.Count < 3)
            {
                return false;
            }

            var head = ReadItemType(GetIndexed(armor, 0));
            var body = ReadItemType(GetIndexed(armor, 1));
            var legs = ReadItemType(GetIndexed(armor, 2));
            var solarHead = ResolveItemId("SolarFlareHelmet");
            var solarBody = ResolveItemId("SolarFlareBreastplate");
            var solarLegs = ResolveItemId("SolarFlareLeggings");
            var crystalHead = ResolveItemId("CrystalNinjaHelmet");
            var crystalBody = ResolveItemId("CrystalNinjaChestplate");
            var crystalLegs = ResolveItemId("CrystalNinjaLeggings");

            if (solarHead > 0 &&
                solarBody > 0 &&
                solarLegs > 0 &&
                head == solarHead &&
                body == solarBody &&
                legs == solarLegs)
            {
                dashType = 3;
                source = "armor:SolarFlare";
                return true;
            }

            if (crystalHead > 0 &&
                crystalBody > 0 &&
                crystalLegs > 0 &&
                head == crystalHead &&
                body == crystalBody &&
                legs == crystalLegs)
            {
                dashType = 5;
                source = "armor:CrystalNinja";
                return true;
            }

            return false;
        }

        private static void ReadMountDashProfile(object player, DashInputProfile profile)
        {
            var mount = GetMember(player, "mount");
            if (mount == null || profile == null)
            {
                return;
            }

            bool boolValue;
            int intValue;
            profile.MountActive = TryGetBoolByNames(mount, out boolValue, "Active", "active", "_active") && boolValue;
            profile.MountType = TryGetIntByNames(mount, out intValue, "Type", "type", "_type") ? intValue : -1;
            if (!profile.MountActive)
            {
                return;
            }

            bool canDash;
            if (TryReadMountCanDash(profile.MountType, out canDash))
            {
                profile.MountCanDashKnown = true;
                profile.MountCanDash = canDash;
            }
        }

        private static bool TryReadMountCanDash(int mountType, out bool canDash)
        {
            canDash = false;
            if (mountType < 0)
            {
                return false;
            }

            var setsType = FindType("Terraria.ID.MountID+Sets");
            var canDashSet = GetStaticMember(setsType, "CanDash");
            return TryReadBoolIndexed(canDashSet, mountType, out canDash);
        }

        private static int ResolveIntrinsicMountDashType(int mountType)
        {
            if (mountType < 0)
            {
                return 0;
            }

            var chillet = ResolveMountId("Chillet");
            var chilletIgnis = ResolveMountId("ChilletIgnis");
            return mountType == chillet || mountType == chilletIgnis ? 6 : 0;
        }

        private static int ResolveItemId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 0;
            }

            lock (ItemIdCache)
            {
                int cached;
                if (ItemIdCache.TryGetValue(name, out cached))
                {
                    return cached;
                }
            }

            var itemIdType = FindType("Terraria.ID.ItemID");
            var resolved = ReadStaticInt(itemIdType, name, 0);
            lock (ItemIdCache)
            {
                ItemIdCache[name] = resolved;
            }

            return resolved;
        }

        private static int ResolveMountId(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return -1;
            }

            lock (MountIdCache)
            {
                int cached;
                if (MountIdCache.TryGetValue(name, out cached))
                {
                    return cached;
                }
            }

            var mountIdType = FindType("Terraria.ID.MountID");
            var resolved = ReadStaticInt(mountIdType, name, -1);
            lock (MountIdCache)
            {
                MountIdCache[name] = resolved;
            }

            return resolved;
        }

        private static void RecordPulse(bool succeeded, int direction, string message, bool fallback)
        {
            lock (SyncRoot)
            {
                _lastPulseApplySucceeded = succeeded;
                _lastPulseApplyDirection = direction;
                _lastPulseApplyUtc = DateTime.UtcNow;
                _lastPulseApplyMessage = message ?? string.Empty;
                _lastPulseWasFallback = fallback;
                if (!succeeded)
                {
                    _lastDashCompatError = message ?? string.Empty;
                }
            }
        }

        private static void ClearExpiredQueuedPulseLocked()
        {
            if (_queuedRequestId == Guid.Empty)
            {
                return;
            }

            long tick;
            var tickKnown = TerrariaInputCompat.TryReadGameUpdateCount(out tick);
            if ((tickKnown && _queuedUntilTick > 0 && tick > _queuedUntilTick) ||
                (_queuedExpiresUtc != DateTime.MinValue && DateTime.UtcNow > _queuedExpiresUtc))
            {
                _queuedRequestId = Guid.Empty;
                _queuedDirection = 0;
                _queuedMode = string.Empty;
                _queuedUntilTick = 0;
                _queuedExpiresUtc = DateTime.MinValue;
                _lastDashCompatError = "queuedPulseExpired";
            }
        }

        private static int NormalizeDirection(int direction)
        {
            if (direction > 0)
            {
                return 1;
            }

            return direction < 0 ? -1 : 0;
        }

        private static int ReadItemType(object item)
        {
            int type;
            int stack;
            return TryGetInt(item, "type", out type) &&
                   type > 0 &&
                   (!TryGetInt(item, "stack", out stack) || stack > 0)
                ? type
                : 0;
        }

        private static object GetIndexed(object source, int index)
        {
            if (source == null || index < 0)
            {
                return null;
            }

            var list = source as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            var array = source as Array;
            return array != null && array.Rank == 1 && index < array.GetLength(0)
                ? array.GetValue(index)
                : null;
        }

        private static bool TryReadBoolIndexed(object source, int index, out bool value)
        {
            value = false;
            var item = GetIndexed(source, index);
            if (item == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(item, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    return field.GetValue(instance);
                }

                PropertyInfo property;
                return TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanRead
                    ? property.GetValue(instance, null)
                    : null;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return null;
            }
        }

        private static bool TrySetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return Fail("Instance unavailable for " + name + ".");
            }

            try
            {
                var type = instance.GetType();
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, false, out field))
                {
                    field.SetValue(instance, value);
                    return ClearError();
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, false, out property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return ClearError();
                }

                return Fail("Member not found: " + name + ".");
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        private static bool TryGetInt(object instance, string name, out int value)
        {
            value = 0;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryGetBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryGetBoolByNames(object instance, out bool value, params string[] names)
        {
            value = false;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetBool(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetIntByNames(object instance, out int value, params string[] names)
        {
            value = 0;
            if (names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetInt(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    return field.GetValue(null);
                }

                PropertyInfo property;
                return TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanRead
                    ? property.GetValue(null, null)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static int ReadStaticInt(Type type, string name, int fallback)
        {
            var raw = GetStaticMember(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool ClearError()
        {
            lock (SyncRoot)
            {
                _lastDashCompatError = string.Empty;
            }

            return true;
        }

        private static bool Fail(string message)
        {
            lock (SyncRoot)
            {
                _lastDashCompatError = message ?? string.Empty;
            }

            return false;
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
