using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Search
{
    internal static class ItemRecipeIndex
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static Dictionary<int, RecipeSnapshot> _recipesByIndex = new Dictionary<int, RecipeSnapshot>();
        private static Dictionary<int, List<int>> _sourceRecipeIndexesByItem = new Dictionary<int, List<int>>();
        private static Dictionary<int, List<RecipeUsageSnapshot>> _usageRecipeIndexesByItem = new Dictionary<int, List<RecipeUsageSnapshot>>();

        public static IList<ItemQueryRecipeSummary> GetCraftingSources(int itemType)
        {
            EnsureInitialized();

            List<int> recipeIndexes;
            lock (SyncRoot)
            {
                if (!_sourceRecipeIndexesByItem.TryGetValue(itemType, out recipeIndexes))
                {
                    return new List<ItemQueryRecipeSummary>();
                }

                recipeIndexes = new List<int>(recipeIndexes);
            }

            var result = new List<ItemQueryRecipeSummary>();
            for (var index = 0; index < recipeIndexes.Count; index++)
            {
                RecipeSnapshot snapshot;
                lock (SyncRoot)
                {
                    if (!_recipesByIndex.TryGetValue(recipeIndexes[index], out snapshot))
                    {
                        continue;
                    }
                }

                result.Add(BuildSummary(snapshot, "source", 0, itemType));
            }

            return result;
        }

        public static IList<ItemQueryRecipeSummary> GetCraftingUses(int itemType)
        {
            EnsureInitialized();

            List<RecipeUsageSnapshot> usages;
            lock (SyncRoot)
            {
                if (!_usageRecipeIndexesByItem.TryGetValue(itemType, out usages))
                {
                    return new List<ItemQueryRecipeSummary>();
                }

                usages = new List<RecipeUsageSnapshot>(usages);
            }

            var result = new List<ItemQueryRecipeSummary>();
            for (var index = 0; index < usages.Count; index++)
            {
                RecipeSnapshot snapshot;
                lock (SyncRoot)
                {
                    if (!_recipesByIndex.TryGetValue(usages[index].RecipeIndex, out snapshot))
                    {
                        continue;
                    }
                }

                result.Add(BuildSummary(snapshot, usages[index].MatchKind, usages[index].RecipeGroupId, itemType));
            }

            return result;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
                _recipesByIndex = new Dictionary<int, RecipeSnapshot>();
                _sourceRecipeIndexesByItem = new Dictionary<int, List<int>>();
                _usageRecipeIndexesByItem = new Dictionary<int, List<RecipeUsageSnapshot>>();
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                var recipes = new Dictionary<int, RecipeSnapshot>();
                var sources = new Dictionary<int, List<int>>();
                var uses = new Dictionary<int, List<RecipeUsageSnapshot>>();
                try
                {
                    BuildIndexes(recipes, sources, uses);
                }
                catch
                {
                    recipes.Clear();
                    sources.Clear();
                    uses.Clear();
                }

                _recipesByIndex = recipes;
                _sourceRecipeIndexesByItem = sources;
                _usageRecipeIndexesByItem = uses;
                _initialized = true;
            }
        }

        private static void BuildIndexes(
            Dictionary<int, RecipeSnapshot> recipes,
            Dictionary<int, List<int>> sources,
            Dictionary<int, List<RecipeUsageSnapshot>> uses)
        {
            var mainType = SearchReflection.FindType("Terraria.Main");
            var recipeArray = SearchReflection.GetStaticMember(mainType, "recipe");
            var recipeCount = ReadRecipeCount(recipeArray);
            var recipeGroups = ReadRecipeGroups();

            for (var recipeIndex = 0; recipeIndex < recipeCount; recipeIndex++)
            {
                var recipe = SearchReflection.GetIndexedValue(recipeArray, recipeIndex);
                if (recipe == null)
                {
                    continue;
                }

                var snapshot = BuildRecipeSnapshot(recipeIndex, recipe, recipeGroups);
                if (snapshot == null || snapshot.CreateItemType <= 0)
                {
                    continue;
                }

                recipes[recipeIndex] = snapshot;
                AddSource(sources, snapshot.CreateItemType, recipeIndex);
                AddUses(uses, snapshot);
            }
        }

        private static int ReadRecipeCount(object recipeArray)
        {
            var recipeType = SearchReflection.FindType("Terraria.Recipe");
            var raw = SearchReflection.GetStaticMember(recipeType, "numRecipes");
            int count;
            if (!SearchReflection.TryConvertInt(raw, out count) || count < 0)
            {
                count = SearchReflection.GetCollectionCount(recipeArray);
            }

            var capacity = SearchReflection.GetCollectionCount(recipeArray);
            if (capacity > 0 && count > capacity)
            {
                count = capacity;
            }

            return Math.Max(0, count);
        }

        private static RecipeSnapshot BuildRecipeSnapshot(
            int recipeIndex,
            object recipe,
            IDictionary<int, RecipeGroupSnapshot> recipeGroups)
        {
            var createItem = SearchReflection.GetMember(recipe, "createItem");
            int createItemType;
            if (!SearchReflection.TryReadInt(createItem, "type", out createItemType) || createItemType <= 0)
            {
                return null;
            }

            int createStack;
            SearchReflection.TryReadInt(createItem, "stack", out createStack);
            if (createStack <= 0)
            {
                createStack = 1;
            }

            var snapshot = new RecipeSnapshot
            {
                RecipeIndex = recipeIndex,
                CreateItemType = createItemType,
                CreateStack = createStack
            };

            ReadRequiredItems(recipe, snapshot);
            ReadAcceptedGroups(recipe, snapshot, recipeGroups);
            return snapshot;
        }

        private static void ReadRequiredItems(object recipe, RecipeSnapshot snapshot)
        {
            var requiredItems = SearchReflection.GetMember(recipe, "requiredItem");
            var count = SearchReflection.GetCollectionCount(requiredItems);
            for (var index = 0; index < count; index++)
            {
                var item = SearchReflection.GetIndexedValue(requiredItems, index);
                if (item == null)
                {
                    continue;
                }

                int itemType;
                if (!SearchReflection.TryReadInt(item, "type", out itemType) || itemType <= 0)
                {
                    // Terraria recipes terminate requiredItem with an empty item; do not scan array capacity as facts.
                    break;
                }

                int stack;
                SearchReflection.TryReadInt(item, "stack", out stack);
                if (stack <= 0)
                {
                    stack = 1;
                }

                snapshot.Ingredients.Add(new IngredientSnapshot
                {
                    ItemType = itemType,
                    Stack = stack
                });
            }
        }

        private static void ReadAcceptedGroups(
            object recipe,
            RecipeSnapshot snapshot,
            IDictionary<int, RecipeGroupSnapshot> recipeGroups)
        {
            var acceptedGroups = SearchReflection.GetMember(recipe, "acceptedGroups");
            var groupIds = ReadIntCollection(acceptedGroups, true);
            for (var index = 0; index < groupIds.Count; index++)
            {
                RecipeGroupSnapshot group;
                if (!recipeGroups.TryGetValue(groupIds[index], out group))
                {
                    continue;
                }

                snapshot.Ingredients.Add(new IngredientSnapshot
                {
                    IsRecipeGroup = true,
                    RecipeGroupId = group.GroupId,
                    RecipeGroupName = group.Name,
                    AcceptedItemTypes = new List<int>(group.ValidItems),
                    Stack = 1
                });
            }
        }

        private static Dictionary<int, RecipeGroupSnapshot> ReadRecipeGroups()
        {
            var result = new Dictionary<int, RecipeGroupSnapshot>();
            var recipeGroupType = SearchReflection.FindType("Terraria.RecipeGroup");
            var rawGroups = SearchReflection.GetStaticMember(recipeGroupType, "recipeGroups");
            var enumerable = rawGroups as IEnumerable;
            if (enumerable == null)
            {
                return result;
            }

            foreach (var rawEntry in enumerable)
            {
                object key;
                object group;
                if (!SearchReflection.TryReadDictionaryEntry(rawEntry, out key, out group) || group == null)
                {
                    continue;
                }

                int groupId;
                if (!SearchReflection.TryConvertInt(key, out groupId) || groupId < 0)
                {
                    continue;
                }

                var validItems = ReadRecipeGroupItems(group);
                if (validItems.Count <= 0)
                {
                    continue;
                }

                result[groupId] = new RecipeGroupSnapshot
                {
                    GroupId = groupId,
                    Name = ReadRecipeGroupName(group, groupId),
                    ValidItems = validItems
                };
            }

            return result;
        }

        private static List<int> ReadRecipeGroupItems(object group)
        {
            var validItems = SearchReflection.GetMember(group, "ValidItems") ??
                             SearchReflection.GetMember(group, "Items");
            var items = ReadIntCollection(validItems, false);
            var result = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] <= 0 || seen.Contains(items[index]))
                {
                    continue;
                }

                seen.Add(items[index]);
                result.Add(items[index]);
            }

            result.Sort();
            return result;
        }

        private static string ReadRecipeGroupName(object group, int groupId)
        {
            object raw;
            if (SearchReflection.TryInvokeInstance(group, "GetText", null, out raw) && raw != null)
            {
                var value = raw.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            var name = SearchReflection.TryReadString(group, "Name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            var key = SearchReflection.TryReadString(group, "Key");
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key.Trim();
            }

            return "RecipeGroup#" + groupId.ToString(CultureInfo.InvariantCulture);
        }

        private static List<int> ReadIntCollection(object source, bool stopAtNegative)
        {
            var result = new List<int>();
            if (source == null)
            {
                return result;
            }

            var enumerable = source as IEnumerable;
            if (enumerable == null || source is string)
            {
                return result;
            }

            foreach (var raw in enumerable)
            {
                int value;
                if (!SearchReflection.TryConvertInt(raw, out value))
                {
                    continue;
                }

                if (stopAtNegative && value < 0)
                {
                    break;
                }

                if (value >= 0)
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static void AddSource(Dictionary<int, List<int>> sources, int itemType, int recipeIndex)
        {
            List<int> indexes;
            if (!sources.TryGetValue(itemType, out indexes))
            {
                indexes = new List<int>();
                sources[itemType] = indexes;
            }

            indexes.Add(recipeIndex);
        }

        private static void AddUses(Dictionary<int, List<RecipeUsageSnapshot>> uses, RecipeSnapshot snapshot)
        {
            var recipeItemMatches = new HashSet<int>();
            for (var index = 0; index < snapshot.Ingredients.Count; index++)
            {
                var ingredient = snapshot.Ingredients[index];
                if (ingredient.IsRecipeGroup)
                {
                    for (var validIndex = 0; validIndex < ingredient.AcceptedItemTypes.Count; validIndex++)
                    {
                        AddUse(uses, recipeItemMatches, ingredient.AcceptedItemTypes[validIndex], snapshot.RecipeIndex, "recipeGroup", ingredient.RecipeGroupId);
                    }

                    continue;
                }

                AddUse(uses, recipeItemMatches, ingredient.ItemType, snapshot.RecipeIndex, "direct", 0);
            }
        }

        private static void AddUse(
            Dictionary<int, List<RecipeUsageSnapshot>> uses,
            ISet<int> recipeItemMatches,
            int itemType,
            int recipeIndex,
            string matchKind,
            int recipeGroupId)
        {
            if (itemType <= 0 || recipeItemMatches.Contains(itemType))
            {
                return;
            }

            recipeItemMatches.Add(itemType);
            List<RecipeUsageSnapshot> snapshots;
            if (!uses.TryGetValue(itemType, out snapshots))
            {
                snapshots = new List<RecipeUsageSnapshot>();
                uses[itemType] = snapshots;
            }

            snapshots.Add(new RecipeUsageSnapshot
            {
                RecipeIndex = recipeIndex,
                MatchKind = matchKind,
                RecipeGroupId = recipeGroupId
            });
        }

        private static ItemQueryRecipeSummary BuildSummary(
            RecipeSnapshot snapshot,
            string matchKind,
            int matchedRecipeGroupId,
            int queriedItemType)
        {
            var summary = new ItemQueryRecipeSummary
            {
                RecipeIndex = snapshot.RecipeIndex,
                CreateItem = ItemCatalogIndex.CreateReference(snapshot.CreateItemType, snapshot.CreateStack),
                CreateStack = snapshot.CreateStack,
                MatchKind = matchKind ?? string.Empty,
                MatchedRecipeGroupId = matchedRecipeGroupId
            };

            for (var index = 0; index < snapshot.Ingredients.Count; index++)
            {
                summary.Ingredients.Add(BuildIngredientSummary(snapshot.Ingredients[index], queriedItemType));
            }

            return summary;
        }

        private static ItemQueryIngredientSummary BuildIngredientSummary(IngredientSnapshot ingredient, int queriedItemType)
        {
            var summary = new ItemQueryIngredientSummary
            {
                IsRecipeGroup = ingredient.IsRecipeGroup,
                RecipeGroupId = ingredient.RecipeGroupId,
                RecipeGroupName = ingredient.RecipeGroupName ?? string.Empty,
                Stack = ingredient.Stack,
                MatchesQueriedItem = ingredient.IsRecipeGroup
                    ? ingredient.AcceptedItemTypes.Contains(queriedItemType)
                    : ingredient.ItemType == queriedItemType
            };

            if (ingredient.IsRecipeGroup)
            {
                // RecipeGroup usage is a value snapshot of accepted item ids; do not expose the mutable RecipeGroup object.
                for (var index = 0; index < ingredient.AcceptedItemTypes.Count; index++)
                {
                    var reference = ItemCatalogIndex.CreateReference(ingredient.AcceptedItemTypes[index], 0);
                    if (reference != null)
                    {
                        summary.AcceptedItems.Add(reference);
                    }
                }
            }
            else
            {
                summary.Item = ItemCatalogIndex.CreateReference(ingredient.ItemType, ingredient.Stack);
            }

            return summary;
        }

        private sealed class RecipeSnapshot
        {
            public int RecipeIndex;
            public int CreateItemType;
            public int CreateStack;
            public readonly List<IngredientSnapshot> Ingredients = new List<IngredientSnapshot>();
        }

        private sealed class IngredientSnapshot
        {
            public int ItemType;
            public int Stack;
            public bool IsRecipeGroup;
            public int RecipeGroupId;
            public string RecipeGroupName = string.Empty;
            public List<int> AcceptedItemTypes = new List<int>();
        }

        private sealed class RecipeGroupSnapshot
        {
            public int GroupId;
            public string Name = string.Empty;
            public List<int> ValidItems = new List<int>();
        }

        private sealed class RecipeUsageSnapshot
        {
            public int RecipeIndex;
            public string MatchKind = string.Empty;
            public int RecipeGroupId;
        }
    }
}
