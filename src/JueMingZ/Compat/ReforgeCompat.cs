using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Compat
{
    public sealed class QuickReforgeResult
    {
        public bool InReforgeMenu { get; set; }
        public bool MouseReforge { get; set; }
        public bool ReforgeInvoked { get; set; }
        public bool CooldownCleared { get; set; }
        public bool MatchedTargetPrefix { get; set; }
        public int PrefixBefore { get; set; }
        public int PrefixAfter { get; set; }
        public string AffixBefore { get; set; }
        public string AffixAfter { get; set; }
        public string MatchedPrefix { get; set; }
        public string Message { get; set; }

        public QuickReforgeResult()
        {
            AffixBefore = string.Empty;
            AffixAfter = string.Empty;
            MatchedPrefix = string.Empty;
            Message = string.Empty;
        }
    }

    public static class ReforgeCompat
    {
        // Reforge automation stays inside the vanilla reforge menu and verifies
        // prefix changes; callers must not edit item prefixes.
        private static readonly object SyncRoot = new object();
        private static MethodInfo _reforgeItemMethod;

        public static bool TryReadReforgeReadyState(out bool ready, out string message, out string currentAffix)
        {
            ready = false;
            message = string.Empty;
            currentAffix = string.Empty;
            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                message = TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                message = "Terraria.Main unavailable.";
                return false;
            }

            var inReforgeMenu = ReadStaticBool(mainType, "InReforgeMenu", false);
            if (!inReforgeMenu)
            {
                message = "not in reforge menu";
                return true;
            }

            var mouseReforge = ReadStaticBool(mainType, "mouseReforge", false);
            if (!mouseReforge)
            {
                message = "reforge button not hovered";
                return true;
            }

            object item;
            if (!TryGetStaticMember(mainType, "reforgeItem", out item) || item == null)
            {
                message = "reforge item unavailable";
                return false;
            }

            currentAffix = ResolveAffixName(item);
            var itemType = ReadInt(item, "type", 0);
            var itemStack = ReadInt(item, "stack", 0);
            if (itemType <= 0 || itemStack <= 0)
            {
                message = "reforge slot empty";
                return true;
            }

            ready = true;
            message = "ready";
            return true;
        }

        public static bool TryQuickReforgeOnce(ICollection<string> targetPrefixes, out QuickReforgeResult result)
        {
            result = new QuickReforgeResult();
            var normalizedTargets = BuildNormalizedTargetSet(targetPrefixes);
            if (normalizedTargets.Count <= 0)
            {
                result.Message = "target prefix list empty";
                return false;
            }

            if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
            {
                result.Message = TerrariaRuntimeTypes.LastError;
                return false;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                result.Message = "Terraria.Main unavailable.";
                return false;
            }

            result.InReforgeMenu = ReadStaticBool(mainType, "InReforgeMenu", false);
            result.MouseReforge = ReadStaticBool(mainType, "mouseReforge", false);
            if (!result.InReforgeMenu)
            {
                result.Message = "not in reforge menu";
                return false;
            }

            if (!result.MouseReforge)
            {
                result.Message = "reforge button not hovered";
                return false;
            }

            object item;
            if (!TryGetStaticMember(mainType, "reforgeItem", out item) || item == null)
            {
                result.Message = "reforge item unavailable";
                return false;
            }

            var itemType = ReadInt(item, "type", 0);
            var itemStack = ReadInt(item, "stack", 0);
            if (itemType <= 0 || itemStack <= 0)
            {
                result.Message = "reforge slot empty";
                return false;
            }

            result.PrefixBefore = ReadInt(item, "prefix", 0);
            result.AffixBefore = ResolveAffixName(item);

            string reforgeMessage;
            if (!TryInvokeReforgeItemInSlot(mainType, out reforgeMessage))
            {
                result.Message = string.IsNullOrWhiteSpace(reforgeMessage)
                    ? "cannot invoke ReforgeItemInReforgeSlot"
                    : reforgeMessage;
                return false;
            }

            result.ReforgeInvoked = true;
            result.PrefixAfter = ReadInt(item, "prefix", 0);
            result.AffixAfter = ResolveAffixName(item);
            result.CooldownCleared = TrySetStaticInt(mainType, "reforgeCooldown", 0);
            string matchedPrefix;
            if (TryMatchTargetPrefixText(normalizedTargets, result.AffixAfter, out matchedPrefix))
            {
                result.MatchedTargetPrefix = true;
                result.MatchedPrefix = matchedPrefix;
            }

            result.Message = result.MatchedTargetPrefix
                ? "matched target prefix"
                : "reforge completed";
            return true;
        }

        public static bool TryMatchTargetPrefixText(IEnumerable<string> targetPrefixes, string affixText, out string matchedPrefix)
        {
            matchedPrefix = string.Empty;
            var normalizedAffix = NormalizePrefixText(affixText);
            if (targetPrefixes == null || string.IsNullOrWhiteSpace(normalizedAffix))
            {
                return false;
            }

            foreach (var raw in targetPrefixes)
            {
                var value = NormalizePrefixText(raw);
                if (string.IsNullOrWhiteSpace(value) ||
                    !PrefixMatchesAffixText(value, normalizedAffix))
                {
                    continue;
                }

                if (value.Length > matchedPrefix.Length)
                {
                    matchedPrefix = value;
                }
            }

            return !string.IsNullOrWhiteSpace(matchedPrefix);
        }

        private static bool PrefixMatchesAffixText(string targetPrefix, string affixText)
        {
            if (string.IsNullOrWhiteSpace(targetPrefix) || string.IsNullOrWhiteSpace(affixText))
            {
                return false;
            }

            if (string.Equals(affixText, targetPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!affixText.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase) ||
                affixText.Length <= targetPrefix.Length)
            {
                return false;
            }

            var next = affixText[targetPrefix.Length];
            return char.IsWhiteSpace(next) || next == ':' || next == '-';
        }

        private static HashSet<string> BuildNormalizedTargetSet(ICollection<string> targetPrefixes)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (targetPrefixes == null)
            {
                return set;
            }

            foreach (var raw in targetPrefixes)
            {
                var value = NormalizePrefixText(raw);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    set.Add(value);
                }
            }

            return set;
        }

        private static string NormalizePrefixText(string raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : raw.Trim();
        }

        private static bool TryInvokeReforgeItemInSlot(Type mainType, out string message)
        {
            message = string.Empty;
            var method = GetReforgeItemMethod(mainType);
            if (method == null)
            {
                message = "Main.ReforgeItemInReforgeSlot not found.";
                return false;
            }

            try
            {
                method.Invoke(null, new object[0]);
                return true;
            }
            catch (Exception error)
            {
                message = error.InnerException == null ? error.Message : error.InnerException.Message;
                return false;
            }
        }

        private static MethodInfo GetReforgeItemMethod(Type mainType)
        {
            if (mainType == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                if (_reforgeItemMethod != null)
                {
                    return _reforgeItemMethod;
                }

                var methods = mainType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "ReforgeItemInReforgeSlot", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (method.GetParameters().Length == 0)
                    {
                        _reforgeItemMethod = method;
                        break;
                    }
                }

                return _reforgeItemMethod;
            }
        }

        private static string ResolveAffixName(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            try
            {
                var methods = item.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (var index = 0; index < methods.Length; index++)
                {
                    var method = methods[index];
                    if (!string.Equals(method.Name, "AffixName", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (method.GetParameters().Length == 0)
                    {
                        var value = method.Invoke(item, new object[0]);
                        return value == null ? string.Empty : value.ToString();
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool TryGetStaticMember(Type type, string name, out object value)
        {
            value = null;
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(type, name, true, out field))
            {
                value = field.GetValue(null);
                return true;
            }

            PropertyInfo property;
            if (TerrariaMemberCache.TryGetProperty(type, name, true, out property))
            {
                value = property.GetValue(null, null);
                return true;
            }

            return false;
        }

        private static bool TrySetStaticInt(Type type, string name, int value)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                FieldInfo field;
                if (TerrariaMemberCache.TryGetField(type, name, true, out field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                PropertyInfo property;
                if (TerrariaMemberCache.TryGetProperty(type, name, true, out property) && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static int ReadInt(object instance, string name, int fallback)
        {
            object raw;
            if (!TryGetMember(instance, name, out raw) || raw == null)
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

        private static bool TryGetMember(object instance, string name, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var type = instance.GetType();
            FieldInfo field;
            if (TerrariaMemberCache.TryGetField(type, name, false, out field))
            {
                value = field.GetValue(instance);
                return true;
            }

            PropertyInfo property;
            if (TerrariaMemberCache.TryGetProperty(type, name, false, out property))
            {
                value = property.GetValue(instance, null);
                return true;
            }

            return false;
        }

        private static bool ReadStaticBool(Type type, string name, bool fallback)
        {
            object raw;
            if (!TryGetStaticMember(type, name, out raw) || raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
