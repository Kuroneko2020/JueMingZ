using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishDropRuleEvaluator
    {
        public const int MaxCatchItems = 96;

        private static FieldInfo _fishRulesField;
        private static readonly Dictionary<Type, MethodInfo> MeetsConditionsMethods = new Dictionary<Type, MethodInfo>();

        public static bool TryGetFishDropRules(InformationWorldContext context, out IList rules)
        {
            rules = null;
            var fishDropDb = context == null
                ? null
                : InformationReflection.GetStaticMember(context.MainType, "FishDropsDB");
            if (fishDropDb == null)
            {
                return false;
            }

            try
            {
                if (_fishRulesField != null && _fishRulesField.DeclaringType.IsInstanceOfType(fishDropDb))
                {
                    rules = _fishRulesField.GetValue(fishDropDb) as IList;
                    if (rules != null)
                    {
                        return true;
                    }
                }

                var fields = fishDropDb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                for (var index = 0; index < fields.Length; index++)
                {
                    var value = fields[index].GetValue(fishDropDb) as IList;
                    if (!LooksLikeFishRuleList(value))
                    {
                        continue;
                    }

                    _fishRulesField = fields[index];
                    rules = value;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void AddMatchingRuleItems(IList rules, object fishingContext, IList<int> itemIds, ISet<int> seen)
        {
            if (rules == null || fishingContext == null || itemIds == null || seen == null)
            {
                return;
            }

            for (var index = 0; index < rules.Count && itemIds.Count < MaxCatchItems; index++)
            {
                var rule = rules[index];
                if (rule == null || !RuleMeetsConditions(rule, fishingContext))
                {
                    continue;
                }

                AddPossibleItems(rule, itemIds, seen);
                bool stopper;
                if (TryConvertBool(InformationReflection.GetMember(rule, "IsStopper"), out stopper) && stopper)
                {
                    break;
                }
            }
        }

        public static void AddPossibleItemsUnbounded(object rule, IList<int> itemIds, ISet<int> seen)
        {
            if (rule == null || itemIds == null || seen == null)
            {
                return;
            }

            var possible = InformationReflection.GetMember(rule, "PossibleItems");
            var array = possible as Array;
            if (array != null)
            {
                for (var index = 0; index < array.Length; index++)
                {
                    AddItemIdUnbounded(array.GetValue(index), itemIds, seen);
                }

                return;
            }

            var list = possible as IList;
            if (list == null)
            {
                return;
            }

            for (var index = 0; index < list.Count; index++)
            {
                AddItemIdUnbounded(list[index], itemIds, seen);
            }
        }

        public static void AddItemIdUnbounded(object raw, IList<int> itemIds, ISet<int> seen)
        {
            if (itemIds == null || seen == null)
            {
                return;
            }

            var itemId = ToInt(raw, 0);
            if (itemId > 0 && seen.Add(itemId))
            {
                itemIds.Add(itemId);
            }
        }

        internal static int MeetsConditionsMethodCacheCountForTesting
        {
            get { return MeetsConditionsMethods.Count; }
        }

        internal static void ResetReflectionCacheForTesting()
        {
            _fishRulesField = null;
            MeetsConditionsMethods.Clear();
        }

        internal static bool LooksLikeFishRuleList(IList value)
        {
            if (value == null)
            {
                return false;
            }

            for (var index = 0; index < value.Count && index < 8; index++)
            {
                var item = value[index];
                if (item == null)
                {
                    continue;
                }

                return item.GetType().FullName.IndexOf("FishDropRule", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       InformationReflection.GetMember(item, "PossibleItems") != null;
            }

            return value.GetType().FullName.IndexOf("FishDropRule", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool RuleMeetsConditions(object rule, object fishingContext)
        {
            try
            {
                MethodInfo method;
                var type = rule.GetType();
                if (!MeetsConditionsMethods.TryGetValue(type, out method))
                {
                    method = FindMeetsConditionsMethod(type);
                    MeetsConditionsMethods[type] = method;
                }

                if (method == null)
                {
                    return false;
                }

                var raw = method.Invoke(rule, new[] { fishingContext, (object)true });
                bool value;
                return TryConvertBool(raw, out value) && value;
            }
            catch
            {
                return false;
            }
        }

        private static MethodInfo FindMeetsConditionsMethod(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "MeetsConditions", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
                {
                    return method;
                }
            }

            return null;
        }

        private static void AddPossibleItems(object rule, IList<int> itemIds, ISet<int> seen)
        {
            var possible = InformationReflection.GetMember(rule, "PossibleItems");
            var array = possible as Array;
            if (array != null)
            {
                for (var index = 0; index < array.Length && itemIds.Count < MaxCatchItems; index++)
                {
                    AddItemId(array.GetValue(index), itemIds, seen);
                }

                return;
            }

            var list = possible as IList;
            if (list == null)
            {
                return;
            }

            for (var index = 0; index < list.Count && itemIds.Count < MaxCatchItems; index++)
            {
                AddItemId(list[index], itemIds, seen);
            }
        }

        private static void AddItemId(object raw, IList<int> itemIds, ISet<int> seen)
        {
            var itemId = ToInt(raw, 0);
            if (itemId > 0 && seen.Add(itemId))
            {
                itemIds.Add(itemId);
            }
        }

        private static int ToInt(object raw, int fallback)
        {
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

        private static bool TryConvertBool(object raw, out bool value)
        {
            value = false;
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
