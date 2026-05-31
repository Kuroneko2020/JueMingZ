using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Runtime
{
    public static class GameMode
    {
        private static Type _cachedMainType;

        public static bool IsTerrariaLoaded
        {
            get { return FindTerrariaMainType() != null; }
        }

        public static Type FindTerrariaMainType()
        {
            if (_cachedMainType != null)
            {
                return _cachedMainType;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.Equals(assembly.GetName().Name, "Terraria", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var type = assembly.GetType("Terraria.Main", false);
                    if (type != null)
                    {
                        _cachedMainType = type;
                        return _cachedMainType;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public static bool IsSinglePlayerLateOnly
        {
            get
            {
                int netMode;
                return TryReadNetModeLateOnly(out netMode) && netMode == 0 && !IsServerLateOnly;
            }
        }

        public static bool IsMultiplayerClientLateOnly
        {
            get
            {
                int netMode;
                return TryReadNetModeLateOnly(out netMode) && netMode == 1;
            }
        }

        public static bool IsServerLateOnly
        {
            get
            {
                bool dedServ;
                if (TryReadDedServLateOnly(out dedServ) && dedServ)
                {
                    return true;
                }

                int netMode;
                return TryReadNetModeLateOnly(out netMode) && netMode == 2;
            }
        }

        public static bool TryReadNetModeLateOnly(out int value)
        {
            return TryReadStaticIntLateOnly("netMode", out value);
        }

        public static string GetDescriptionLateOnly()
        {
            if (!IsTerrariaLoaded)
            {
                return "Unknown: Terraria.Main not loaded";
            }

            int netMode;
            if (!TryReadNetModeLateOnly(out netMode))
            {
                return "Unknown: netMode unavailable";
            }

            if (IsServerLateOnly)
            {
                return "Server (netMode=" + netMode + ")";
            }

            if (IsMultiplayerClientLateOnly)
            {
                return "MultiplayerClient (netMode=" + netMode + ")";
            }

            if (IsSinglePlayerLateOnly)
            {
                return "SinglePlayer (netMode=" + netMode + ")";
            }

            return "Unknown (netMode=" + netMode + ")";
        }

        public static bool TryReadIsInMainMenuLateOnly(out bool value)
        {
            return TryReadStaticBoolLateOnly("gameMenu", out value);
        }

        public static bool TryReadLegacyUiBlockedByVanillaMenuLateOnly(out bool blocked, out string reason)
        {
            blocked = false;
            reason = string.Empty;

            bool value;
            if (!TryReadStaticBoolLateOnly("gameMenu", out value))
            {
                blocked = true;
                reason = "gameMenuUnavailable";
                return true;
            }

            if (value)
            {
                blocked = true;
                reason = "gameMenu";
                return true;
            }

            if (TryReadStaticBoolLateOnly("ingameOptionsWindow", out value) && value)
            {
                blocked = true;
                reason = "ingameOptionsWindow";
                return true;
            }

            if (TryReadStaticBoolLateOnly("inFancyUI", out value) && value)
            {
                blocked = true;
                reason = "inFancyUI";
                return true;
            }

            return true;
        }

        public static string GetTerrariaVersionLateOnly()
        {
            var mainType = FindTerrariaMainType();
            if (mainType == null)
            {
                return "Unknown";
            }

            var names = new[] { "versionNumber", "versionNumber2", "versionString", "version" };
            foreach (var name in names)
            {
                object value;
                if (TryReadStaticValueLateOnly(mainType, name, out value) && value != null)
                {
                    return value.ToString();
                }
            }

            LogThrottle.WarnThrottled(
                "gamemode-version-missing-late",
                TimeSpan.FromMinutes(1),
                "GameMode",
                "Unable to read Terraria version in late-only mode.");
            return "Unknown";
        }

        private static bool TryReadDedServLateOnly(out bool value)
        {
            return TryReadStaticBoolLateOnly("dedServ", out value);
        }

        private static bool TryReadStaticIntLateOnly(string memberName, out int value)
        {
            value = 0;
            var mainType = FindTerrariaMainType();
            if (mainType == null)
            {
                return false;
            }

            object raw;
            if (!TryReadStaticValueLateOnly(mainType, memberName, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "gamemode-int-convert-" + memberName,
                    TimeSpan.FromMinutes(1),
                    "GameMode",
                    "Failed to convert late-only Terraria.Main." + memberName + " to int: " + error.Message);
                return false;
            }
        }

        private static bool TryReadStaticBoolLateOnly(string memberName, out bool value)
        {
            value = false;
            var mainType = FindTerrariaMainType();
            if (mainType == null)
            {
                return false;
            }

            object raw;
            if (!TryReadStaticValueLateOnly(mainType, memberName, out raw) || raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "gamemode-bool-convert-" + memberName,
                    TimeSpan.FromMinutes(1),
                    "GameMode",
                    "Failed to convert late-only Terraria.Main." + memberName + " to bool: " + error.Message);
                return false;
            }
        }

        // LateOnly methods may trigger Terraria.Main static initialization.
        // They must only be called after LateBootstrap has run from a real
        // Terraria Draw/Update postfix, never during AppDomainManager startup.
        private static bool TryReadStaticValueLateOnly(Type type, string memberName, out object value)
        {
            value = null;

            try
            {
                if (TerrariaMemberCache.TryGetField(type, memberName, true, out var field))
                {
                    value = field.GetValue(null);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, memberName, true, out var property))
                {
                    value = property.GetValue(null, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "gamemode-late-read-" + memberName,
                    TimeSpan.FromMinutes(1),
                    "GameMode",
                    "Late-only read failed for Terraria.Main." + memberName + ": " + error.Message);
            }

            return false;
        }
    }
}
