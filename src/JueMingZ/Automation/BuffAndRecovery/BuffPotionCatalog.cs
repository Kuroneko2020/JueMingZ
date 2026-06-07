using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.BuffAndRecovery
{
    // Catalog scans read candidate inventory and active buffs only; actual AddBuff and stack changes belong to Actions/Compat.
    public static class BuffPotionCatalog
    {
        private static readonly object LangSyncRoot = new object();
        private static bool _langResolved;
        private static MethodInfo _getBuffNameMethod;
        private static MethodInfo _getBuffDescriptionMethod;

        public static BuffPotionScanResult RefreshCandidates()
        {
            var result = ScanLocalPlayer();
            BuffPotionDiagnostics.UpdateFromScan(result);
            return result;
        }

        public static BuffPotionScanResult ScanLocalPlayer()
        {
            var result = new BuffPotionScanResult();
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                result.Error = TerrariaInputCompat.LastInputCompatError;
                result.Message = "Local player unavailable.";
                return result;
            }

            result.PlayerAvailable = true;
            int netMode;
            string networkMode;
            bool multiplayerClient;
            InventoryMutationCompat.ReadNetworkState(out netMode, out networkMode, out multiplayerClient);
            result.NetworkMode = networkMode;

            var activeBuffs = ReadActiveBuffTypes(player);
            var whitelist = BuffPotionWhitelistService.GetWhitelistedItemTypes();
            ScanContainer(player, "Inventory", activeBuffs, whitelist, result);

            if (InventoryMutationCompat.TryPlayerUsesVoidBag(player))
            {
                ScanContainer(player, "VoidBag", activeBuffs, whitelist, result);
                result.VoidBagScanned = true;
            }

            result.Message = "Scanned " + result.Candidates.Count.ToString(CultureInfo.InvariantCulture) + " buff potion candidates.";
            return result;
        }

        public static HashSet<int> ReadActiveBuffTypesForUi()
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return new HashSet<int>();
            }

            return ReadActiveBuffTypes(player);
        }

        private static void ScanContainer(object player, string sourceContainer, HashSet<int> activeBuffs, HashSet<int> whitelist, BuffPotionScanResult result)
        {
            IList items;
            string message;
            if (!InventoryMutationCompat.TryGetContainerItems(player, sourceContainer, out items, out message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Logger.Debug("BuffPotionCatalog", sourceContainer + " skipped: " + message);
                }

                return;
            }

            for (var slot = 0; slot < items.Count; slot++)
            {
                try
                {
                    var item = items[slot];
                    int itemType;
                    int stack;
                    int buffType;
                    int buffTime;
                    bool summon;
                    string itemName;
                    if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
                    {
                        continue;
                    }

                    if (itemType <= 0 || stack <= 0 || buffType <= 0 || buffTime <= 0 || summon)
                    {
                        continue;
                    }

                    var candidate = new BuffPotionCandidate
                    {
                        SourceContainer = sourceContainer,
                        SourceSlot = slot,
                        ItemType = itemType,
                        ItemName = string.IsNullOrWhiteSpace(itemName) ? "Item " + itemType.ToString(CultureInfo.InvariantCulture) : itemName,
                        Stack = stack,
                        BuffType = buffType,
                        BuffName = ReadBuffNameSafe(buffType),
                        BuffTime = buffTime,
                        EstimatedDurationSeconds = Math.Max(0, buffTime / 60),
                        IsActive = activeBuffs.Contains(buffType),
                        IsWhitelisted = whitelist.Contains(itemType),
                        CanApply = true,
                        NetworkMode = result.NetworkMode
                    };

                    ApplyBasicConflictState(candidate, activeBuffs, result);
                    result.Candidates.Add(candidate);
                }
                catch (Exception error)
                {
                    result.Error = "Buff potion scan error: " + error.Message;
                    Logger.Warn("BuffPotionCatalog", result.Error);
                }
            }
        }

        private static void ApplyBasicConflictState(BuffPotionCandidate candidate, HashSet<int> activeBuffs, BuffPotionScanResult result)
        {
            if (candidate == null)
            {
                return;
            }

            if (candidate.IsActive)
            {
                candidate.CanApply = false;
                candidate.SkipReason = "AlreadyActive";
                return;
            }

            var conflictGroup = BuffPotionConflictCompat.FindConflictGroup(candidate.BuffType, activeBuffs, out var supported);
            if (!supported)
            {
                result.UnsupportedConflictCheck = true;
            }

            if (!string.IsNullOrWhiteSpace(conflictGroup))
            {
                candidate.CanApply = false;
                candidate.ConflictGroup = conflictGroup;
                candidate.SkipReason = "Conflict:" + conflictGroup;
            }
        }

        private static HashSet<int> ReadActiveBuffTypes(object player)
        {
            var set = new HashSet<int>();
            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null)
            {
                return set;
            }

            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            for (var index = 0; index < max; index++)
            {
                var type = Convert.ToInt32(buffTypes[index]);
                var time = Convert.ToInt32(buffTimes[index]);
                if (type > 0 && time > 0)
                {
                    set.Add(type);
                }
            }

            return set;
        }

        public static string ReadBuffNameSafe(int buffType)
        {
            var name = InvokeLangString("GetBuffName", buffType);
            return string.IsNullOrWhiteSpace(name)
                ? "Buff " + buffType.ToString(CultureInfo.InvariantCulture)
                : name;
        }

        public static string ReadBuffDescriptionSafe(int buffType)
        {
            return InvokeLangString("GetBuffDescription", buffType);
        }

        private static string InvokeLangString(string methodName, int buffType)
        {
            if (buffType <= 0)
            {
                return string.Empty;
            }

            try
            {
                EnsureLangAccessors();
                var method = string.Equals(methodName, "GetBuffDescription", StringComparison.Ordinal)
                    ? _getBuffDescriptionMethod
                    : _getBuffNameMethod;
                if (method == null)
                {
                    return string.Empty;
                }

                var value = method.Invoke(null, new object[] { buffType });
                return value == null ? string.Empty : value.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void EnsureLangAccessors()
        {
            lock (LangSyncRoot)
            {
                if (_langResolved)
                {
                    return;
                }

                _langResolved = true;
                var langType = FindType("Terraria.Lang");
                if (langType == null)
                {
                    return;
                }

                _getBuffNameMethod = FindLangMethod(langType, "GetBuffName");
                _getBuffDescriptionMethod = FindLangMethod(langType, "GetBuffDescription");
            }
        }

        private static MethodInfo FindLangMethod(Type langType, string name)
        {
            var methods = langType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                var parameters = method.GetParameters();
                if (string.Equals(method.Name, name, StringComparison.Ordinal) &&
                    parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(int))
                {
                    return method;
                }
            }

            return null;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
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
    }
}
