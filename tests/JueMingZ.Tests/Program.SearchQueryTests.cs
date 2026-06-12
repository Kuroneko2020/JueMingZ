using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JueMingZ.Automation.Search;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void SearchQueryUnknownItemDegradesCleanly()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(9999);

                if (result.Found || result.Item != null)
                {
                    throw new InvalidOperationException("Unknown search item must not produce a found query result.");
                }

                AssertStringEquals(result.Status, "unknownItem", "search unknown item status");
                if (result.AcquisitionSources.Count != 0 ||
                    result.CraftingSources.Count != 0 ||
                    result.CraftingUses.Count != 0 ||
                    result.Shimmer.HasAnyRelation)
                {
                    throw new InvalidOperationException("Unknown search item must keep acquisition, recipe, and shimmer facts empty.");
                }
            });
        }

        private static void SearchQueryCandidatesMatchNamesAndIds()
        {
            WithSearchQueryFixture(() =>
            {
                var localized = ItemQueryService.ResolveCandidates("铁", 10);
                AssertSearchCandidate(localized, 100, "localized display name");

                var internalName = ItemQueryService.ResolveCandidates("wood", 10);
                AssertSearchCandidate(internalName, 104, "internal name");

                var hashId = ItemQueryService.ResolveCandidates("#100", 10);
                if (hashId.Count != 1 || hashId[0].ItemType != 100)
                {
                    throw new InvalidOperationException("Search #ID should resolve exactly one item candidate.");
                }

                var plainId = ItemQueryService.ResolveCandidates("104", 10);
                if (plainId.Count != 1 || plainId[0].ItemType != 104)
                {
                    throw new InvalidOperationException("Search plain numeric ID should resolve exactly one item candidate.");
                }

                var limited = ItemQueryService.ResolveCandidates("块", 1);
                if (limited.Count != 1)
                {
                    throw new InvalidOperationException("Search candidate maxResults should limit returned candidates.");
                }
            });
        }

        private static void SearchQueryBuildsCraftingSourceSummaries()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(200);
                if (!result.Found || result.CraftingSources.Count != 1)
                {
                    throw new InvalidOperationException("Crafted item should expose one crafting source recipe.");
                }

                var recipe = result.CraftingSources[0];
                AssertStringEquals(recipe.MatchKind, "source", "search source recipe match kind");
                if (recipe.CreateItem == null || recipe.CreateItem.ItemType != 200 || recipe.CreateStack != 2)
                {
                    throw new InvalidOperationException("Crafting source summary must preserve create item and stack.");
                }

                var iron = recipe.Ingredients.FirstOrDefault(item => item.Item != null && item.Item.ItemType == 100);
                if (iron == null || iron.Stack != 3)
                {
                    throw new InvalidOperationException("Crafting source summary must preserve direct ingredient stack.");
                }
            });
        }

        private static void SearchQueryIndexesDirectAndRecipeGroupUsages()
        {
            WithSearchQueryFixture(() =>
            {
                var direct = ItemQueryService.BuildQuery(101);
                if (direct.CraftingUses.Count != 1 ||
                    direct.CraftingUses[0].CreateItem == null ||
                    direct.CraftingUses[0].CreateItem.ItemType != 200)
                {
                    throw new InvalidOperationException("Direct material should be indexed as crafting usage.");
                }

                AssertStringEquals(direct.CraftingUses[0].MatchKind, "direct", "search direct usage match kind");

                var grouped = ItemQueryService.BuildQuery(102);
                if (grouped.CraftingUses.Count != 1 ||
                    grouped.CraftingUses[0].CreateItem == null ||
                    grouped.CraftingUses[0].CreateItem.ItemType != 300)
                {
                    throw new InvalidOperationException("RecipeGroup material should be indexed as crafting usage.");
                }

                var usage = grouped.CraftingUses[0];
                AssertStringEquals(usage.MatchKind, "recipeGroup", "search recipe group usage match kind");
                if (usage.MatchedRecipeGroupId != 7)
                {
                    throw new InvalidOperationException("RecipeGroup usage should preserve matched group id.");
                }

                var groupIngredient = usage.Ingredients.FirstOrDefault(item => item.IsRecipeGroup);
                if (groupIngredient == null ||
                    !groupIngredient.MatchesQueriedItem ||
                    groupIngredient.AcceptedItems.Count < 2 ||
                    groupIngredient.AcceptedItems.All(item => item.ItemType != 102))
                {
                    throw new InvalidOperationException("RecipeGroup ingredient summary should include accepted item references.");
                }
            });
        }

        private static void SearchQueryIndexesDirectShimmerRelations()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(100);
                if (result.Shimmer.ForwardResult == null || result.Shimmer.ForwardResult.ItemType != 200)
                {
                    throw new InvalidOperationException("Search query should expose direct forward shimmer transform.");
                }

                if (result.Shimmer.ReverseSources.Count != 1 || result.Shimmer.ReverseSources[0].ItemType != 103)
                {
                    throw new InvalidOperationException("Search query should expose direct reverse shimmer sources.");
                }
            });
        }

        private static void SearchQueryEmptyFactsStayEmpty()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(105);
                if (!result.Found)
                {
                    throw new InvalidOperationException("Known item without facts should still be a found query result.");
                }

                if (result.AcquisitionSources.Count != 0 ||
                    result.CraftingSources.Count != 0 ||
                    result.CraftingUses.Count != 0 ||
                    result.Shimmer.HasAnyRelation)
                {
                    throw new InvalidOperationException("Known item without acquisition, recipe, or shimmer facts should degrade to empty sections.");
                }
            });
        }

        private static void SearchQueryAcquisitionSourcesDefaultToEmpty()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(100);
                if (!result.Found || result.AcquisitionSources == null || result.AcquisitionSources.Count != 0)
                {
                    throw new InvalidOperationException("Stage 02 acquisition source hook should return a stable empty source list.");
                }
            });
        }

        private static void SearchQueryIndexesNpcDropSources()
        {
            WithSearchQueryFixture(() =>
            {
                var dropDb = new Terraria.GameContent.ItemDropRules.TestItemDropDatabase();
                Terraria.Main.ItemDropsDB = dropDb;
                Terraria.Lang.NpcNames[10] = "僵尸";
                dropDb.AddNpcRule(
                    10,
                    new Terraria.GameContent.ItemDropRules.TestItemDropRule(
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(100, 1, 2, 0.25f, null)));

                ItemQueryService.ResetForTesting();
                var result = ItemQueryService.BuildQuery(100);
                if (!result.Found || result.AcquisitionSources.Count != 1)
                {
                    throw new InvalidOperationException("Expected item query to expose one NPC drop acquisition source.");
                }

                var source = result.AcquisitionSources[0];
                AssertStringEquals(source.SourceType, ItemAcquisitionSourceTypes.NpcDrop, "NPC drop source type");
                AssertStringEquals(source.Title, "NPC掉落", "NPC drop title");
                AssertStringEquals(source.SourceName, "僵尸", "NPC drop source name");
                AssertStringEquals(source.QuantityText, "1-2个", "NPC drop quantity");
                AssertStringEquals(source.ProbabilityText, "25%", "NPC drop probability");
                if (source.NpcNetId != 10 || source.ItemType != 100 || source.RelatedItemType != 100)
                {
                    throw new InvalidOperationException("NPC drop source must preserve item and NPC ids.");
                }
            });
        }

        private static void SearchQueryNpcDropConditionsDegradeSafely()
        {
            WithSearchQueryFixture(() =>
            {
                var dropDb = new Terraria.GameContent.ItemDropRules.TestItemDropDatabase();
                Terraria.Main.ItemDropsDB = dropDb;
                Terraria.Lang.NpcNames[20] = "测试 Boss";

                dropDb.AddNpcRule(
                    20,
                    new Terraria.GameContent.ItemDropRules.TestItemDropRule(
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(
                            102,
                            1,
                            1,
                            0.3333f,
                            new List<Terraria.GameContent.ItemDropRules.IItemDropRuleCondition>
                            {
                                new Terraria.GameContent.ItemDropRules.TestItemDropCondition("专家模式", true),
                                new Terraria.GameContent.ItemDropRules.TestUnnamedDropCondition()
                            }),
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(
                            103,
                            1,
                            1,
                            0.1f,
                            new List<Terraria.GameContent.ItemDropRules.IItemDropRuleCondition>
                            {
                                new Terraria.GameContent.ItemDropRules.TestItemDropCondition(string.Empty, false)
                            })));

                ItemQueryService.ResetForTesting();
                var expert = ItemQueryService.BuildQuery(102).AcquisitionSources;
                if (expert.Count != 1 ||
                    expert[0].ConditionText.IndexOf("专家模式", StringComparison.Ordinal) < 0 ||
                    expert[0].ConditionText.IndexOf("TestUnnamedDropCondition", StringComparison.Ordinal) < 0 ||
                    !string.Equals(expert[0].ProbabilityText, "33.33%", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("NPC drop source should keep readable conditions and type-name fallback.");
                }

                var hidden = ItemQueryService.BuildQuery(103).AcquisitionSources;
                if (hidden.Count != 1 || !string.Equals(hidden[0].ConditionText, "特殊条件", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("NPC drop source should degrade hidden conditions to a safe summary.");
                }
            });
        }

        private static void SearchQueryNpcDropIndexHandlesGlobalNegativeAndCache()
        {
            WithSearchQueryFixture(() =>
            {
                var dropDb = new Terraria.GameContent.ItemDropRules.TestItemDropDatabase();
                Terraria.Main.ItemDropsDB = dropDb;
                Terraria.Lang.NpcNames[-65] = "大型黄蜂";
                dropDb.AddGlobalRule(
                    new Terraria.GameContent.ItemDropRules.TestItemDropRule(
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(101, 1, 1, 0.01f, null)));
                dropDb.AddNpcRule(
                    -65,
                    new Terraria.GameContent.ItemDropRules.TestItemDropRule(
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(102, 2, 4, 0.5f, null)));

                ItemQueryService.ResetForTesting();
                var global = ItemQueryService.BuildQuery(101).AcquisitionSources;
                var firstQueryCount = dropDb.QueryCount;
                var negative = ItemQueryService.BuildQuery(102).AcquisitionSources;
                var secondQueryCount = dropDb.QueryCount;
                ItemQueryService.BuildQuery(101);
                var thirdQueryCount = dropDb.QueryCount;

                if (global.Count != 1 ||
                    !string.Equals(global[0].SourceName, "任意 NPC", StringComparison.Ordinal) ||
                    !string.Equals(global[0].ContextText, "全局掉落规则", StringComparison.Ordinal) ||
                    global[0].NpcNetId != 0)
                {
                    throw new InvalidOperationException("Global NPC drops should be exposed once as a broad source.");
                }

                if (negative.Count != 1 ||
                    negative[0].NpcNetId != -65 ||
                    !string.Equals(negative[0].SourceName, "大型黄蜂", StringComparison.Ordinal) ||
                    !string.Equals(negative[0].QuantityText, "2-4个", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Negative NPC net id drops should be scanned and named.");
                }

                if (firstQueryCount <= 0 || secondQueryCount != firstQueryCount || thirdQueryCount != firstQueryCount)
                {
                    throw new InvalidOperationException("NPC drop index should be cached after the first lazy build.");
                }
            });
        }

        private static void SearchQueryIndexesCurrentNpcShopSources()
        {
            WithSearchQueryFixture(() =>
            {
                AddOpenSearchShop(1, 5, 40, new Terraria.TestRecipeItem { type = 104, stack = 3 });
                Terraria.Lang.NpcNames[40] = "商人";

                ItemQueryService.ResetForTesting();
                var result = ItemQueryService.BuildQuery(104);
                if (!result.Found || result.AcquisitionSources.Count != 1)
                {
                    throw new InvalidOperationException("Expected current open NPC shop item to appear as an acquisition source.");
                }

                var source = result.AcquisitionSources[0];
                AssertStringEquals(source.SourceType, ItemAcquisitionSourceTypes.NpcShop, "NPC shop source type");
                AssertStringEquals(source.Title, "NPC出售", "NPC shop source title");
                AssertStringEquals(source.SourceName, "商人", "NPC shop source name");
                AssertStringEquals(source.QuantityText, "可购买", "NPC shop quantity");
                AssertStringEquals(source.ConditionText, "当前上下文可售卖", "NPC shop condition");
                AssertContains(source.ContextText, "当前已打开 NPC 商店快照");
                AssertContains(source.ContextText, "Shop #1");
                if (source.NpcNetId != 40 || source.ItemType != 104 || source.RelatedItemType != 104)
                {
                    throw new InvalidOperationException("NPC shop source must preserve item type, related item, and source NPC type.");
                }
            });
        }

        private static void SearchQueryNpcShopSourceRequiresOpenShop()
        {
            WithSearchQueryFixture(() =>
            {
                AddOpenSearchShop(1, 5, 40, new Terraria.TestRecipeItem { type = 104, stack = 3 });
                Terraria.Main.npcShop = 0;

                ItemQueryService.ResetForTesting();
                var result = ItemQueryService.BuildQuery(104);
                if (!result.Found || result.AcquisitionSources.Count != 0)
                {
                    throw new InvalidOperationException("NPC shop acquisition source should stay empty when no NPC shop is currently open.");
                }
            });
        }

        private static void SearchQueryNpcShopSourceCacheFollowsContext()
        {
            WithSearchQueryFixture(() =>
            {
                AddOpenSearchShop(1, 5, 40, new Terraria.TestRecipeItem { type = 104, stack = 1 });

                ItemQueryService.ResetForTesting();
                var first = ItemQueryService.BuildQuery(104).AcquisitionSources;
                var empty = ItemQueryService.BuildQuery(100).AcquisitionSources;

                var shop = (Terraria.Chest)((Terraria.MainInstance)Terraria.Main.instance).shop[1];
                shop.item[0] = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                Terraria.Main.GameUpdateCount++;

                var refreshed = ItemQueryService.BuildQuery(100).AcquisitionSources;
                if (first.Count != 1 || empty.Count != 0 || refreshed.Count != 1 || refreshed[0].ItemType != 100)
                {
                    throw new InvalidOperationException("NPC shop acquisition source cache should rebuild when the current context key changes.");
                }
            });
        }

        private static void SearchQueryIndexesMiningGatheringTags()
        {
            WithSearchQueryFixture(() =>
            {
                AddSearchItem(12, "铜矿", "CopperOre", 999, 0, 0, true, false, -1, -1);

                ItemQueryService.ResetForTesting();
                var result = ItemQueryService.BuildQuery(12);
                if (!result.Found || result.AcquisitionSources.Count != 1)
                {
                    throw new InvalidOperationException("Expected copper ore to expose one mining/gathering acquisition tag.");
                }

                var source = result.AcquisitionSources[0];
                AssertStringEquals(source.SourceType, ItemAcquisitionSourceTypes.MiningGatheringTag, "mining tag source type");
                AssertStringEquals(source.Title, "常见挖掘", "mining tag title");
                AssertStringEquals(source.SourceName, "基础矿脉", "mining tag source name");
                AssertStringEquals(source.QuantityText, "常见来源", "mining tag quantity label");
                AssertContains(source.ConditionText, "地下/洞穴层");
                AssertContains(source.ContextText, "非完整百科");
                if (source.ItemType != 12 || source.RelatedItemType != 12 || source.NpcNetId != -1)
                {
                    throw new InvalidOperationException("Mining/gathering tags should preserve item ids without NPC ownership.");
                }
            });
        }

        private static void SearchQueryMiningGatheringTagsCoverHerbsAndEnvironment()
        {
            WithSearchQueryFixture(() =>
            {
                AddSearchItem(313, "太阳花", "Daybloom", 999, 0, 0, true, true, -1, -1);
                AddSearchItem(3, "石块", "StoneBlock", 999, 0, 0, true, false, 1, -1);

                ItemQueryService.ResetForTesting();
                var herb = ItemQueryService.BuildQuery(313).AcquisitionSources;
                var block = ItemQueryService.BuildQuery(3).AcquisitionSources;
                if (herb.Count != 1 ||
                    !string.Equals(herb[0].Title, "常见采集", StringComparison.Ordinal) ||
                    herb[0].ConditionText.IndexOf("森林", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Mining/gathering tags should cover representative herbs with biome or timing text.");
                }

                if (block.Count != 1 ||
                    !string.Equals(block[0].Title, "常见挖掘", StringComparison.Ordinal) ||
                    block[0].ConditionText.IndexOf("环境块", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Mining/gathering tags should cover representative natural environment blocks.");
                }
            });
        }

        private static void SearchQueryMiningGatheringTagsKeepUnknownItemsEmpty()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(105);
                if (!result.Found || result.AcquisitionSources.Count != 0)
                {
                    throw new InvalidOperationException("Unregistered mining/gathering items should not receive guessed acquisition tags.");
                }
            });
        }

        private static void SearchQueryAcquisitionSourcesKeepDropShopTagOrder()
        {
            WithSearchQueryFixture(() =>
            {
                AddSearchItem(12, "铜矿", "CopperOre", 999, 0, 0, true, false, -1, -1);
                var dropDb = new Terraria.GameContent.ItemDropRules.TestItemDropDatabase();
                Terraria.Main.ItemDropsDB = dropDb;
                Terraria.Lang.NpcNames[10] = "测试史莱姆";
                dropDb.AddNpcRule(
                    10,
                    new Terraria.GameContent.ItemDropRules.TestItemDropRule(
                        new Terraria.GameContent.ItemDropRules.DropRateInfo(12, 1, 1, 0.25f, null)));
                AddOpenSearchShop(1, 5, 40, new Terraria.TestRecipeItem { type = 12, stack = 1 });
                Terraria.Lang.NpcNames[40] = "商人";

                ItemQueryService.ResetForTesting();
                var sources = ItemQueryService.BuildQuery(12).AcquisitionSources;
                if (sources.Count != 3 ||
                    !string.Equals(sources[0].SourceType, ItemAcquisitionSourceTypes.NpcDrop, StringComparison.Ordinal) ||
                    !string.Equals(sources[1].SourceType, ItemAcquisitionSourceTypes.NpcShop, StringComparison.Ordinal) ||
                    !string.Equals(sources[2].SourceType, ItemAcquisitionSourceTypes.MiningGatheringTag, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Acquisition sources should keep NPC drop, current shop, then mining/gathering tag order.");
                }
            });
        }

        private static void SearchQueryFormatsBaseValueAsCoins()
        {
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(0), "无价值", "search base value zero");
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(1), "1铜币", "search base value copper");
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(100), "1银币", "search base value silver");
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(10000), "1金币", "search base value gold");
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(1000000), "1铂金币", "search base value platinum");
            AssertStringEquals(ItemValueFormatter.FormatBaseValue(1020304), "1铂金币 2金币 3银币 4铜币", "search base value mixed coins");
        }

        private static void SearchQueryBasicFactsUseChineseLabelsAndPlacementValues()
        {
            WithSearchQueryFixture(() =>
            {
                var result = ItemQueryService.BuildQuery(200);
                if (result == null || !result.Found || result.Item == null)
                {
                    throw new InvalidOperationException("Expected search fixture item 200 to produce basic facts.");
                }

                var facts = LegacyMainWindow.GetSearchBasicFactLinesForTesting(result.Item);
                var text = string.Join("|", facts);
                AssertContains(text, "内部名：IronAnvil");
                AssertContains(text, "基础价值：15银币");
                AssertContains(text, "可放置方块：Tile#16");
                AssertContains(text, "可放置背景墙：无");
                if (text.IndexOf("createTile", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("createWall", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException("Search basic facts must not expose raw createTile/createWall labels.");
                }
            });
        }

        private static void SearchQueryBasicPanelHeightTracksColumnCount()
        {
            var twoColumn = LegacyMainWindow.CalculateSearchBasicPanelHeightForTesting(600);
            var oneColumn = LegacyMainWindow.CalculateSearchBasicPanelHeightForTesting(320);
            if (oneColumn <= twoColumn)
            {
                throw new InvalidOperationException("Search basic info panel height must grow when facts collapse to one column.");
            }

            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.SelectItem(100);
                var wide = LegacyMainWindow.CalculateSearchContentHeightForTesting(new LegacyUiRect(0, 0, 600, 360));
                var narrow = LegacyMainWindow.CalculateSearchContentHeightForTesting(new LegacyUiRect(0, 0, 320, 360));
                if (narrow <= wide)
                {
                    throw new InvalidOperationException("Search content height must reuse the dynamic basic panel height.");
                }
            });
        }

        private static void SearchQueryRecipeLayoutWrapsIngredientsAndKeepsAllRows()
        {
            WithSearchQueryFixture(() =>
            {
                AddSearchQuerySourceRecipe(500, 100, 101, 102);
                AddSearchQuerySourceRecipe(501, 100, 101, 102, 103, 104);
                AddSearchQueryExtraUsageRecipes(100, 8);
                ItemQueryService.ResetForTesting();

                var threeIngredient = ItemQueryService.BuildQuery(500);
                var fiveIngredient = ItemQueryService.BuildQuery(501);
                if (threeIngredient.CraftingSources.Count != 1 || fiveIngredient.CraftingSources.Count != 1)
                {
                    throw new InvalidOperationException("Expected recipe layout fixture to expose one source recipe per product.");
                }

                var threeIngredientHeight = LegacyMainWindow.CalculateSearchRecipeRowHeightForTesting(threeIngredient.CraftingSources[0], 600);
                var fiveIngredientHeight = LegacyMainWindow.CalculateSearchRecipeRowHeightForTesting(fiveIngredient.CraftingSources[0], 600);
                if (fiveIngredientHeight <= threeIngredientHeight)
                {
                    throw new InvalidOperationException("Search recipe row height must grow when ingredient chips wrap beyond one row.");
                }

                var grid = LegacyMainWindow.GetSearchChipGridMetricsForTesting(320, 5);
                if (grid[0] > 3 || grid[1] < 2 || grid[2] <= 0 || grid[3] <= 0)
                {
                    throw new InvalidOperationException("Search chip grid must cap at three columns and wrap five chips into multiple rows.");
                }

                var uses = ItemQueryService.BuildQuery(100).CraftingUses;
                if (uses.Count <= 6)
                {
                    throw new InvalidOperationException("Expected search usage fixture to exceed the old six-row truncation limit.");
                }

                var fullHeight = LegacyMainWindow.CalculateSearchRecipeSectionHeightForTesting(uses, 600);
                var firstSixHeight = LegacyMainWindow.CalculateSearchRecipeSectionHeightForTesting(uses.Take(6).ToList(), 600);
                if (fullHeight <= firstSixHeight)
                {
                    throw new InvalidOperationException("Search recipe section height must account for every usage row, not only the first six.");
                }
            });
        }

        private static void SearchQueryShimmerLayoutKeepsAllReverseSources()
        {
            WithSearchQueryFixture(() =>
            {
                AddSearchQueryExtraShimmerSources(100, 8);
                ItemQueryService.ResetForTesting();

                var shimmer = ItemQueryService.BuildQuery(100).Shimmer;
                if (shimmer.ReverseSources.Count <= 6)
                {
                    throw new InvalidOperationException("Expected shimmer fixture to exceed the old reverse-source truncation limit.");
                }

                var firstSix = new ItemQueryShimmerSummary
                {
                    ForwardResult = shimmer.ForwardResult
                };
                for (var index = 0; index < 6; index++)
                {
                    firstSix.ReverseSources.Add(shimmer.ReverseSources[index]);
                }

                var fullHeight = LegacyMainWindow.CalculateSearchShimmerSectionHeightForTesting(shimmer);
                var firstSixHeight = LegacyMainWindow.CalculateSearchShimmerSectionHeightForTesting(firstSix);
                if (fullHeight <= firstSixHeight)
                {
                    throw new InvalidOperationException("Search shimmer section height must include every reverse source.");
                }
            });
        }

        private static void SearchQueryAcquisitionModelClonesValueFacts()
        {
            var result = CreateSearchQueryResultWithAcquisitionSource("npcDrop", "掉落来源", "史莱姆", "1-2个", "25%", "白天", "测试上下文");
            SearchItemQueryUiState.ResetForTesting();
            SearchItemQueryUiState.SetSelectedResultForTesting(result);
            result.AcquisitionSources[0].SourceName = "已变更";

            var snapshot = SearchItemQueryUiState.GetSelectedResult();
            if (snapshot == null ||
                snapshot.AcquisitionSources.Count != 1 ||
                !string.Equals(snapshot.AcquisitionSources[0].SourceName, "史莱姆", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search UI state must clone acquisition source value facts instead of sharing mutable test objects.");
            }

            var formatted = LegacyMainWindow.FormatSearchAcquisitionSourceForTesting(snapshot.AcquisitionSources[0]);
            AssertStringEquals(formatted[0], "掉落", "search acquisition source type label");
            AssertStringEquals(formatted[1], "掉落来源：史莱姆", "search acquisition source title");
            AssertStringEquals(formatted[2], "1-2个 / 25% / 白天 / 测试上下文", "search acquisition source detail");
        }

        private static void SearchQueryAcquisitionSourcesAffectLayoutSignature()
        {
            SearchItemQueryUiState.ResetForTesting();
            SearchItemQueryUiState.SetSelectedResultForTesting(CreateSearchQueryResultWithAcquisitionSource("npcDrop", "掉落来源", "史莱姆", "1个", "25%", "白天", string.Empty));
            var first = SearchItemQueryUiState.BuildStateSignature();

            SearchItemQueryUiState.SetSelectedResultForTesting(CreateSearchQueryResultWithAcquisitionSource("npcDrop", "掉落来源", "史莱姆", "1个", "50%", "白天", string.Empty));
            var second = SearchItemQueryUiState.BuildStateSignature();
            if (first == second)
            {
                throw new InvalidOperationException("Search page layout signature must track acquisition source field changes.");
            }
        }

        private static void SearchQueryAcquisitionSectionKeepsOrderAndHeight()
        {
            var order = LegacyMainWindow.GetSearchResultSectionOrderForTesting();
            var expected = new[] { "基础信息", "获取来源", "合成来源", "合成用途", "微光反应" };
            if (order.Length != expected.Length)
            {
                throw new InvalidOperationException("Search result section order test helper returned an unexpected length.");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                AssertStringEquals(order[index], expected[index], "search result section order " + index.ToString(CultureInfo.InvariantCulture));
            }

            AssertStringEquals(LegacyMainWindow.GetSearchAcquisitionEmptyTextForTesting(), "暂无获取来源", "search acquisition empty text");
            var emptyHeight = LegacyMainWindow.CalculateSearchAcquisitionSectionHeightForTesting(new List<ItemAcquisitionSourceSummary>());
            var oneSource = new List<ItemAcquisitionSourceSummary>
            {
                CreateSearchAcquisitionSource("npcShop", "NPC出售", "商人", "1个", string.Empty, "当前上下文", "测试商店")
            };
            var twoSources = new List<ItemAcquisitionSourceSummary>(oneSource)
            {
                CreateSearchAcquisitionSource("miningGatheringTag", "常见采集", "地表草药", string.Empty, string.Empty, "森林", string.Empty)
            };

            if (LegacyMainWindow.CalculateSearchAcquisitionSectionHeightForTesting(oneSource) <= emptyHeight ||
                LegacyMainWindow.CalculateSearchAcquisitionSectionHeightForTesting(twoSources) <= LegacyMainWindow.CalculateSearchAcquisitionSectionHeightForTesting(oneSource))
            {
                throw new InvalidOperationException("Search acquisition section height must grow with source rows.");
            }
        }

        private static void SearchQueryUiHotPathAvoidsAcquisitionSourceReads()
        {
            var repoRoot = ResolveSearchQueryRepoRoot();
            var hotPathFiles = new[]
            {
                Path.Combine(repoRoot, "src", "JueMingZ", "UI", "Legacy", "LegacyMainWindow.Search.cs")
            };
            var forbiddenTerms = new[]
            {
                "ItemDropsDB",
                "FishDropsDB",
                "OpenShop(",
                "SetupShop(",
                "WorldGen",
                "Main.instance.shop",
                "Main.npcShop",
                "OpenFishingCrate",
                "RollExtractinatorDrop",
                "TryDroppingItem",
                "ItemNpcDropSourceIndex",
                "ItemNpcShopSourceIndex",
                "ItemAcquisitionTagIndex"
            };

            for (var fileIndex = 0; fileIndex < hotPathFiles.Length; fileIndex++)
            {
                var path = hotPathFiles[fileIndex];
                var text = File.ReadAllText(path, Encoding.UTF8);
                for (var termIndex = 0; termIndex < forbiddenTerms.Length; termIndex++)
                {
                    var term = forbiddenTerms[termIndex];
                    if (text.IndexOf(term, StringComparison.Ordinal) >= 0)
                    {
                        throw new InvalidOperationException(
                            "Search query UI hot path must consume ItemQueryResult.AcquisitionSources snapshots, not source indexes or vanilla source APIs: " +
                            Path.GetFileName(path) + " contains " + term + ".");
                    }
                }
            }
        }

        private static void SearchQueryUiStateSelectsCandidateAndClears()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.ResetForTesting();
                SearchItemQueryUiState.UpdateDraft("铁");
                if (SearchItemQueryUiState.CandidateCount <= 0)
                {
                    throw new InvalidOperationException("Expected search UI state to resolve candidates from the draft query.");
                }

                if (!SearchItemQueryUiState.SelectItem(100))
                {
                    throw new InvalidOperationException("Expected selecting a known candidate to build a search result.");
                }

                var result = SearchItemQueryUiState.GetSelectedResult();
                if (result == null || !result.Found || result.Item == null || result.Item.ItemType != 100)
                {
                    throw new InvalidOperationException("Expected search UI state to expose the selected item result.");
                }

                AssertStringEquals(SearchItemQueryUiState.QueryText, "铁锭", "search UI selected query text");
                if (SearchItemQueryUiState.CandidateCount != 0)
                {
                    throw new InvalidOperationException("Expected selecting a candidate to close the candidate list.");
                }

                SearchItemQueryUiState.Clear();
                if (SearchItemQueryUiState.HasSelectedResult ||
                    SearchItemQueryUiState.CandidateCount != 0 ||
                    !string.IsNullOrEmpty(SearchItemQueryUiState.QueryText))
                {
                    throw new InvalidOperationException("Expected clearing search UI state to reset query, candidates, and result.");
                }
            });
        }

        private static void SearchQueryUiCandidateScrollKeepsOwnViewport()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.ResetForTesting();
                SearchItemQueryUiState.UpdateDraft("锭");
                if (SearchItemQueryUiState.CandidateCount < 3)
                {
                    throw new InvalidOperationException("Expected the search fixture to expose multiple candidate rows.");
                }

                var mouse = new LegacyMouseSnapshot
                {
                    X = 20,
                    Y = 20,
                    ScrollDelta = -120
                };
                SearchItemQueryUiState.SetCandidateViewport(new LegacyUiRect(10, 10, 100, 50), SearchItemQueryUiState.CandidateCount * 30);
                if (!SearchItemQueryUiState.TryConsumeCandidateScroll(mouse, mouse.ScrollDelta))
                {
                    throw new InvalidOperationException("Expected search candidate viewport to consume wheel while scrollable.");
                }

                if (SearchItemQueryUiState.CandidateScrollOffset <= 0)
                {
                    throw new InvalidOperationException("Expected search candidate scroll offset to move downward.");
                }

                mouse.X = 200;
                mouse.Y = 200;
                if (SearchItemQueryUiState.TryConsumeCandidateScroll(mouse, -120))
                {
                    throw new InvalidOperationException("Expected wheel outside the search candidate viewport to bubble.");
                }
            });
        }

        private static void SearchQueryPageLayoutTracksUiState()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.ResetForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();
                var settings = AppSettings.CreateDefault();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var shell = LegacyMainWindowShell.Create(window);
                var content = shell.ContentRect;

                var initial = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);
                SearchItemQueryUiState.UpdateDraft("铁");
                var candidates = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);
                if (candidates.PageStateSignature == initial.PageStateSignature)
                {
                    throw new InvalidOperationException("Expected search draft candidates to dirty the F5 search page state signature.");
                }

                AddSearchQueryExtraUsageRecipes(100, 8);
                ItemQueryService.ResetForTesting();
                SearchItemQueryUiState.SelectItem(100);
                var selected = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 99999, settings);
                if (selected.ContentHeight <= initial.ContentHeight)
                {
                    throw new InvalidOperationException("Expected selected search result content height to exceed the empty search page.");
                }

                if (selected.MaxScroll <= 0)
                {
                    throw new InvalidOperationException("Expected populated search result page to be scrollable in the default F5 content area.");
                }
            });
        }

        private static void SearchQueryLayoutRhythmKeepsSectionsConsistent()
        {
            var rhythm = LegacyMainWindow.GetSearchLayoutRhythmForTesting();
            if (rhythm.Length != 5 ||
                rhythm[0] <= 0 ||
                rhythm[1] <= 0 ||
                rhythm[2] <= 0 ||
                rhythm[3] <= 0 ||
                rhythm[4] <= 0)
            {
                throw new InvalidOperationException("Expected search layout rhythm metrics to expose positive section, row, chip, and panel spacing.");
            }

            var emptyRecipe = LegacyMainWindow.CalculateSearchRecipeSectionHeightForTesting(new List<ItemQueryRecipeSummary>(), 600);
            var emptyShimmer = LegacyMainWindow.CalculateSearchShimmerSectionHeightForTesting(new ItemQueryShimmerSummary());
            if (emptyRecipe != emptyShimmer)
            {
                throw new InvalidOperationException("Expected empty search recipe and shimmer sections to use the same title-to-content rhythm.");
            }
        }

        private static void SearchQueryLayoutCacheTracksResultDetailChanges()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.ResetForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();
                var settings = AppSettings.CreateDefault();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var shell = LegacyMainWindowShell.Create(window);
                var content = shell.ContentRect;

                ReplaceSearchQuerySourceRecipe(500, 100, 101, 102);
                ItemQueryService.ResetForTesting();
                SearchItemQueryUiState.SelectItem(500);
                var compact = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);

                ReplaceSearchQuerySourceRecipe(500, 100, 101, 102, 103, 104, 105);
                ItemQueryService.ResetForTesting();
                SearchItemQueryUiState.SelectItem(500);
                var expanded = LegacyMainWindow.BuildPageLayoutSnapshotForTesting("search", window, content, 0, settings);

                if (expanded.PageStateSignature == compact.PageStateSignature)
                {
                    throw new InvalidOperationException("Expected search page layout signature to track recipe ingredient detail changes for the same item.");
                }

                if (expanded.RebuildCount <= compact.RebuildCount)
                {
                    throw new InvalidOperationException("Expected search page layout cache to rebuild after same-item result detail changes.");
                }

                if (expanded.ContentHeight <= compact.ContentHeight)
                {
                    throw new InvalidOperationException("Expected search content height cache to refresh when recipe chip wrapping grows.");
                }
            });
        }

        private static void SearchQueryPickEntryUsesSelectionWording()
        {
            var labels = LegacyMainWindow.GetSearchInputRowTextForTesting();
            if (labels.Length < 2)
            {
                throw new InvalidOperationException("Expected search input row testing labels to include the input label and pick button.");
            }

            AssertStringEquals(labels[0], "查询物品", "search input label");
            AssertStringEquals(labels[1], "选择物品", "search pick button label");
            AssertStringEquals(
                LegacyMainWindow.GetSearchPickButtonTooltipForTesting(),
                "点击需要查询的物品",
                "search pick button tooltip");
        }

        private static void SearchQueryPickCommandStartsPendingSelectionAndHidesWindow()
        {
            WithSearchQueryFixture(() =>
            {
                ResetSearchQueryCommandState();
                try
                {
                    TerrariaMainCompat.SetGameUpdateCountOverrideForTesting(345UL);
                    var item = new Terraria.TestRecipeItem { type = 100, stack = 2 };
                    if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 8, 3, 344, 40, 50) ||
                        !SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(345, 40, 50))
                    {
                        throw new InvalidOperationException("Expected fresh hover item to be available before starting search pick mode.");
                    }

                    SearchItemQueryUiState.UpdateDraft("锭");
                    if (SearchItemQueryUiState.CandidateCount <= 0)
                    {
                        throw new InvalidOperationException("Expected search pick command test to start with an open candidate list.");
                    }

                    LegacyTextInput.Focus(SearchItemQueryUiState.InputId, SearchItemQueryUiState.QueryText);
                    LegacyMainUiState.SetVisible(true);
                    EnqueueSearchQueryCommandForTesting(SearchItemQueryUiState.PickItemButtonId);
                    LegacyUiActionService.Update(null, null);

                    if (LegacyUiActionService.DispatchedCommandCountLast != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected search pick command to dispatch exactly one UI command, pending=" +
                            LegacyUiActionService.PendingCommandCountLast.ToString(CultureInfo.InvariantCulture) +
                            ", ran=" + LegacyUiActionService.ActionUpdateRanCount.ToString(CultureInfo.InvariantCulture) +
                            ", skipped=" + LegacyUiActionService.ActionUpdateSkippedCount.ToString(CultureInfo.InvariantCulture) +
                            ", dispatched=" + LegacyUiActionService.DispatchedCommandCountLast.ToString(CultureInfo.InvariantCulture) +
                            ".");
                    }

                    if (LegacyMainUiState.Visible)
                    {
                        throw new InvalidOperationException("Expected search pick command to hide the F5 main window.");
                    }

                    if (!SearchItemQueryUiState.IsSelectionPending ||
                        !SearchItemQueryUiState.SelectionWaitingForMouseRelease ||
                        SearchItemQueryUiState.SelectionStartGameUpdateCount != 345UL)
                    {
                        throw new InvalidOperationException("Expected search pick command to enter pending selection and wait for mouse release.");
                    }

                    if (SearchItemQueryUiState.CandidateCount != 0 || LegacyTextInput.IsAnyFocused)
                    {
                        throw new InvalidOperationException("Expected search pick command to close candidates and clear text input focus.");
                    }

                    if (SearchItemQueryUiState.HasSelectedResult ||
                        SearchItemQueryUiState.SelectedItemType != 0 ||
                        SearchItemQueryUiState.RecentItemHistoryCount != 0)
                    {
                        throw new InvalidOperationException("Expected search pick command not to query or record the current hover item yet.");
                    }

                    var json = SearchItemQueryUiState.BuildUiStateJson();
                    AssertContains(json, "\"selectionPending\":true");
                    AssertContains(json, "\"selectionWaitingForMouseRelease\":true");
                    AssertContains(json, "\"selectionSource\":\"legacyUiButton\"");
                }
                finally
                {
                    TerrariaMainCompat.SetGameUpdateCountOverrideForTesting(null);
                    ResetSearchQueryCommandState();
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                }
            });
        }

        private static void SearchQueryPickStateWaitsReleaseBeforeArming()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(10, "test");
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Failed("shouldNotResolve")
                };

                var held = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 11),
                    probe.ToPorts());
                if (held.SelectionArmed ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.WaitingButtonRelease ||
                    !SearchItemQueryUiState.SelectionWaitingForMouseRelease ||
                    probe.ResolveCount != 0 ||
                    probe.ConsumeCount != 0)
                {
                    throw new InvalidOperationException("Search item pick must not arm or resolve while the original button click is still held.");
                }

                var released = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(false, 12),
                    probe.ToPorts());
                if (!released.SelectionArmed ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.ArmedForNextLeftClick ||
                    SearchItemQueryUiState.SelectionWaitingForMouseRelease ||
                    probe.ResolveCount != 0 ||
                    probe.ConsumeCount != 0)
                {
                    throw new InvalidOperationException("Search item pick must arm only after the button click is released.");
                }
            });
        }

        private static void SearchQueryPickRuntimeConsumesLeftClickAndSelectsItem()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(10, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(11);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                    {
                        ItemType = 100,
                        SourceKind = "uiItem",
                        SourceSummary = "uiItem;source=ItemSlot"
                    })
                };

                var result = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 12),
                    probe.ToPorts());
                var query = SearchItemQueryUiState.GetSelectedResult();
                if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                    !result.InputConsumeAttempted ||
                    !result.InputConsumed ||
                    !result.ResolveAttempted ||
                    probe.ConsumeCount != 1 ||
                    !string.Equals(probe.LastConsumedToken, "MouseLeft", StringComparison.Ordinal) ||
                    probe.ResolveCount != 1 ||
                    probe.RestoreCount != 1 ||
                    !LegacyMainUiState.Visible ||
                    !string.Equals(LegacyMainUiState.SelectedPageId, "search", StringComparison.Ordinal) ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.Resolved ||
                    SearchItemQueryUiState.IsSelectionPending ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 100)
                {
                    throw new InvalidOperationException("Search item pick runtime must consume MouseLeft, restore F5, and query the resolved item.");
                }
            });
        }

        private static void SearchQueryPickRuntimeConsumesAfterPlayerInputRewrite()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var previousLocalPlayer = Terraria.Main.LocalPlayer;
                var previousPlayers = Terraria.Main.player;
                var previousMyPlayer = Terraria.Main.myPlayer;
                try
                {
                    TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(true);
                    TerrariaMainCompat.SetGameUpdateCountOverrideForTesting(120);
                    TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();

                    var player = new Terraria.Player();
                    Terraria.Main.LocalPlayer = player;
                    Terraria.Main.player = new object[256];
                    Terraria.Main.player[0] = player;
                    Terraria.Main.myPlayer = 0;
                    SearchItemQueryUiState.BeginPendingSelection(118, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(119);
                    LegacyMainUiState.SetVisible(false);

                    var prefix = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(false, 120),
                        new RecordingSearchItemPickRuntimeProbe().ToPorts());
                    if (!string.Equals(prefix.ResultCode, "skipped", StringComparison.Ordinal) ||
                        !string.Equals(prefix.SkipReason, "waitingNextLeftClick", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Search item pick prefix guard must not consume before PlayerInput rewrites MouseLeft.");
                    }

                    Terraria.Main.mouseLeft = true;
                    Terraria.Main.mouseLeftRelease = true;
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft = true;
                    Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft = true;
                    player.controlUseItem = true;
                    player.releaseUseItem = false;
                    player.channel = true;

                    var consumeCount = 0;
                    var restoreCount = 0;
                    var afterPlayerInput = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 120),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) => SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                            {
                                ItemType = 100,
                                SourceKind = "uiItem",
                                SourceSummary = "uiItem;source=ItemSlot"
                            }),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) => SearchItemPickResolveAttempt.Failed("shouldNotFallback"),
                            ConsumeMouseTriggerInput = token =>
                            {
                                consumeCount++;
                                string message;
                                return TerrariaUiMouseCompat.TryConsumeMouseTriggerInput(token, out message)
                                    ? SearchItemPickInputConsumeResult.Success(message)
                                    : SearchItemPickInputConsumeResult.Failed(message);
                            },
                            RestoreSearchWindow = () =>
                            {
                                restoreCount++;
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    if (!string.Equals(afterPlayerInput.ResultCode, "resolved", StringComparison.Ordinal) ||
                        consumeCount != 1 ||
                        restoreCount != 1 ||
                        Terraria.Main.mouseLeft ||
                        Terraria.Main.mouseLeftRelease ||
                        !Terraria.Main.mouseInterface ||
                        !Terraria.Main.blockMouse ||
                        !player.mouseInterface ||
                        player.controlUseItem ||
                        !player.releaseUseItem ||
                        player.channel ||
                        Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft ||
                        Terraria.GameInput.PlayerInput.Triggers.JustPressed.MouseLeft)
                    {
                        throw new InvalidOperationException("Search item pick after-PlayerInput guard must consume the rewritten MouseLeft before ItemCheck can use it.");
                    }
                }
                finally
                {
                    TerrariaMainCompat.SetAllowsInputProcessingOverrideForTesting(null);
                    TerrariaMainCompat.SetGameUpdateCountOverrideForTesting(null);
                    TerrariaUiMouseCompat.ResetUiMouseCaptureAccessorsForTesting();
                    Terraria.Main.LocalPlayer = previousLocalPlayer;
                    Terraria.Main.player = previousPlayers;
                    Terraria.Main.myPlayer = previousMyPlayer;
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesPreConsumeUiSlotSnapshot()
        {
            WithSearchQueryFixture(() =>
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    SearchItemQueryUiState.BeginPendingSelection(20, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(21);
                    LegacyMainUiState.SetVisible(false);

                    var hoverItem = new Terraria.TestRecipeItem { type = 101, stack = 5 };
                    if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(hoverItem, 8, 3, 21, 40, 50))
                    {
                        throw new InvalidOperationException("Expected a fresh ItemSlot hover snapshot before the target click is consumed.");
                    }

                    var resolveCount = 0;
                    var fallbackCount = 0;
                    var consumeCount = 0;
                    var restoreCount = 0;
                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 22, 40, 50),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                            {
                                resolveCount++;
                                return SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount);
                            },
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            {
                                fallbackCount++;
                                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                                {
                                    ItemType = 104,
                                    SourceKind = "tile",
                                    SourceSummary = "tile;type=5;style=0"
                                });
                            },
                            ConsumeMouseTriggerInput = token =>
                            {
                                consumeCount++;
                                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                                return SearchItemPickInputConsumeResult.Success("consumed and cleared post-consume UI cache");
                            },
                            RestoreSearchWindow = () =>
                            {
                                restoreCount++;
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    var query = SearchItemQueryUiState.GetSelectedResult();
                    if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                        !result.InputConsumeAttempted ||
                        !result.InputConsumed ||
                        resolveCount != 1 ||
                        consumeCount != 1 ||
                        fallbackCount != 0 ||
                        restoreCount != 1 ||
                        query == null ||
                        !query.Found ||
                        query.Item == null ||
                        query.Item.ItemType != 101)
                    {
                        throw new InvalidOperationException("Search item pick must resolve the fresh UI slot snapshot before target-click consumption can make later UI evidence unavailable.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                }
            });
        }

        private static void SearchQueryPickRuntimeBlocksPreConsumeEmptyUiSlot()
        {
            WithSearchQueryFixture(() =>
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    SearchItemQueryUiState.BeginPendingSelection(30, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(31);
                    LegacyMainUiState.SetVisible(false);

                    var emptyItem = new Terraria.TestRecipeItem { type = 0, stack = 0 };
                    if (TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(emptyItem, 8, 7, 31, 40, 50))
                    {
                        throw new InvalidOperationException("Expected empty ItemSlot capture not to report an active item.");
                    }

                    var fallbackCount = 0;
                    var consumeCount = 0;
                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 32, 40, 50),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                                SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            {
                                fallbackCount++;
                                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                                {
                                    ItemType = 104,
                                    SourceKind = "tile",
                                    SourceSummary = "tile;type=5;style=0"
                                });
                            },
                            ConsumeMouseTriggerInput = token =>
                            {
                                consumeCount++;
                                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                                return SearchItemPickInputConsumeResult.Success("consumed and cleared post-consume UI cache");
                            },
                            RestoreSearchWindow = () =>
                            {
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    if (!string.Equals(result.ResultCode, "failed", StringComparison.Ordinal) ||
                        !string.Equals(result.FailureReason, "uiEmptySlot", StringComparison.Ordinal) ||
                        !result.InputConsumeAttempted ||
                        !result.InputConsumed ||
                        consumeCount != 1 ||
                        fallbackCount != 0 ||
                        SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.CancelledOrFailed ||
                        SearchItemQueryUiState.IsSelectionPending)
                    {
                        throw new InvalidOperationException("Search item pick must treat a pre-consume empty UI slot as a real UI hit and block world fallback.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                }
            });
        }

        private static void SearchQueryPickRuntimeFreezesLayeredClickBeforeConsume()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(40, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(41);
                LegacyMainUiState.SetVisible(false);

                var callOrder = new List<string>();
                var consumeCalled = false;
                SearchItemPickPendingClick observedPending = null;
                var result = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 42, 640, 800),
                    new SearchItemPickRuntimePorts
                    {
                        CaptureClickContext = input =>
                        {
                            callOrder.Add("capture");
                            return CreateSearchPickClickContext(input, 512, 640, 960.5f, 1280.25f);
                        },
                        ResolvePendingUi = (pending, currentGameUpdateCount) =>
                        {
                            callOrder.Add("resolve");
                            if (consumeCalled)
                            {
                                throw new InvalidOperationException("Search item pick must resolve frozen target evidence before consuming MouseLeft.");
                            }

                            observedPending = pending;
                            if (pending == null ||
                                pending.ClickContext == null ||
                                pending.MouseX != 640 ||
                                pending.MouseY != 800 ||
                                pending.UiMouseX != 512 ||
                                pending.UiMouseY != 640 ||
                                Math.Abs(pending.ClickContext.MouseWorldX - 960.5f) > 0.001f ||
                                Math.Abs(pending.ClickContext.MouseWorldY - 1280.25f) > 0.001f ||
                                pending.ClickContext.MouseTileX != 60 ||
                                pending.ClickContext.MouseTileY != 80 ||
                                pending.CoordinateSourceSummary.IndexOf("ui=testUi", StringComparison.Ordinal) < 0)
                            {
                                throw new InvalidOperationException("Search item pick must freeze raw/UI/world click evidence before input consumption.");
                            }

                            return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                            {
                                ItemType = 100,
                                SourceKind = "uiItem",
                                SourceSummary = "uiItem;source=layered-click"
                            });
                        },
                        ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            SearchItemPickResolveAttempt.Failed("shouldNotFallback"),
                        ConsumeMouseTriggerInput = token =>
                        {
                            callOrder.Add("consume");
                            consumeCalled = true;
                            if (observedPending == null ||
                                observedPending.UiMouseX != 512 ||
                                Math.Abs(observedPending.ClickContext.MouseWorldX - 960.5f) > 0.001f)
                            {
                                throw new InvalidOperationException("Search item pick consumption must not be the first step that creates layered click evidence.");
                            }

                            return SearchItemPickInputConsumeResult.Success("consumed");
                        },
                        RestoreSearchWindow = () =>
                        {
                            callOrder.Add("restore");
                            LegacyMainUiState.SelectPage("search");
                            LegacyMainUiState.SetVisible(true);
                        }
                    });

                var query = SearchItemQueryUiState.GetSelectedResult();
                var order = string.Join(">", callOrder);
                if (!string.Equals(order, "capture>resolve>consume>restore", StringComparison.Ordinal) ||
                    !string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                    !result.InputConsumeAttempted ||
                    !result.InputConsumed ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 100)
                {
                    throw new InvalidOperationException("Search item pick must keep capture -> UI resolve -> consume -> restore ordering after coordinate layering.");
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesVisibleInventorySlotWhenHoverStale()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[0] = new Terraria.TestRecipeItem { type = 101, stack = 4 };
                    ResetSearchPickFakeUiState(player);

                    var staleHoverItem = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                    TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(staleHoverItem, 8, 3, 1, 700, 700);

                    SearchItemQueryUiState.BeginPendingSelection(90, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(91);
                    LegacyMainUiState.SetVisible(false);

                    var fallbackCount = 0;
                    var consumeCount = 0;
                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 100, 22, 22),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                                SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            {
                                fallbackCount++;
                                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                                {
                                    ItemType = 104,
                                    SourceKind = "tile",
                                    SourceSummary = "tile;type=5;style=0"
                                });
                            },
                            ConsumeMouseTriggerInput = token =>
                            {
                                consumeCount++;
                                return SearchItemPickInputConsumeResult.Success("consumed");
                            },
                            RestoreSearchWindow = () =>
                            {
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    var query = SearchItemQueryUiState.GetSelectedResult();
                    if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                        !result.InputConsumeAttempted ||
                        !result.InputConsumed ||
                        consumeCount != 1 ||
                        fallbackCount != 0 ||
                        query == null ||
                        !query.Found ||
                        query.Item == null ||
                        query.Item.ItemType != 101)
                    {
                        throw new InvalidOperationException("Search pick must use the directly hit visible inventory slot when the ItemSlot hover cache is stale.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesLayeredUiCoordinateForVisibleSlots()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[0] = new Terraria.TestRecipeItem { type = 101, stack = 4 };
                    player.inventory[1] = new Terraria.TestRecipeItem { type = 102, stack = 5 };
                    ResetSearchPickFakeUiState(player);

                    SearchItemQueryUiState.BeginPendingSelection(90, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(91);
                    LegacyMainUiState.SetVisible(false);

                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 100, 22, 22),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input, 75, 22, 640f, 512f),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                                SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                                SearchItemPickResolveAttempt.Failed("shouldNotFallback"),
                            ConsumeMouseTriggerInput = token => SearchItemPickInputConsumeResult.Success("consumed"),
                            RestoreSearchWindow = () =>
                            {
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    var query = SearchItemQueryUiState.GetSelectedResult();
                    if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                        query == null ||
                        !query.Found ||
                        query.Item == null ||
                        query.Item.ItemType != 102)
                    {
                        throw new InvalidOperationException("Search pick visible slot hit-test must use the layered UI coordinate, not the raw mouse coordinate.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesScaledVisibleInventoryFirstSlot()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[0] = new Terraria.TestRecipeItem { type = 101, stack = 4 };
                    ResetSearchPickFakeUiState(player);
                    ArmSearchPickSelectionForTesting();

                    var uiX = SearchPickInventorySlotCenterX(0);
                    var uiY = SearchPickInventorySlotCenterY(0);
                    int fallbackCount;
                    var result = RunSearchPickVisibleSlotSelection(
                        100,
                        ScaleSearchPickUiCoordinate(uiX),
                        ScaleSearchPickUiCoordinate(uiY),
                        uiX,
                        uiY,
                        out fallbackCount);

                    AssertSearchPickResolvedVisibleSlot(
                        result,
                        101,
                        fallbackCount,
                        "Search pick must keep scaled UI slot 0 clicks on inventory slot 0.");
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesScaledVisibleInventoryNonFirstSlot()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[2] = new Terraria.TestRecipeItem { type = 102, stack = 5 };
                    player.inventory[3] = new Terraria.TestRecipeItem { type = 104, stack = 1 };
                    ResetSearchPickFakeUiState(player);
                    ArmSearchPickSelectionForTesting();

                    var uiX = SearchPickInventorySlotCenterX(2);
                    var uiY = SearchPickInventorySlotCenterY(0);
                    int fallbackCount;
                    var result = RunSearchPickVisibleSlotSelection(
                        100,
                        ScaleSearchPickUiCoordinate(uiX),
                        ScaleSearchPickUiCoordinate(uiY),
                        uiX,
                        uiY,
                        out fallbackCount);

                    AssertSearchPickResolvedVisibleSlot(
                        result,
                        102,
                        fallbackCount,
                        "Search pick must use UI logical coordinates for scaled non-first inventory slots.");
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesScaledVisibleChestNonFirstSlot()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    var chest = CreateSearchPickFakeChest(40);
                    chest.item[12] = new Terraria.TestRecipeItem { type = 103, stack = 6 };
                    chest.item[33] = new Terraria.TestRecipeItem { type = 104, stack = 1 };
                    player.chest = 0;
                    ResetSearchPickFakeUiState(player);
                    Terraria.Main.chest[0] = chest;
                    ArmSearchPickSelectionForTesting();

                    var uiX = SearchPickChestSlotCenterX(2);
                    var uiY = SearchPickChestSlotCenterY(1);
                    int fallbackCount;
                    var result = RunSearchPickVisibleSlotSelection(
                        100,
                        ScaleSearchPickUiCoordinate(uiX),
                        ScaleSearchPickUiCoordinate(uiY),
                        uiX,
                        uiY,
                        out fallbackCount);

                    AssertSearchPickResolvedVisibleSlot(
                        result,
                        103,
                        fallbackCount,
                        "Search pick must use UI logical coordinates for scaled non-first chest slots.");
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeBlocksScaledVisibleEmptyInventorySlot()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[2] = new Terraria.TestRecipeItem { type = 0, stack = 0 };
                    player.inventory[3] = new Terraria.TestRecipeItem { type = 104, stack = 1 };
                    ResetSearchPickFakeUiState(player);
                    ArmSearchPickSelectionForTesting();

                    var uiX = SearchPickInventorySlotCenterX(2);
                    var uiY = SearchPickInventorySlotCenterY(0);
                    int fallbackCount;
                    var result = RunSearchPickVisibleSlotSelection(
                        100,
                        ScaleSearchPickUiCoordinate(uiX),
                        ScaleSearchPickUiCoordinate(uiY),
                        uiX,
                        uiY,
                        out fallbackCount);

                    if (!string.Equals(result.ResultCode, "failed", StringComparison.Ordinal) ||
                        !string.Equals(result.FailureReason, "uiEmptySlot", StringComparison.Ordinal) ||
                        fallbackCount != 0 ||
                        SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.CancelledOrFailed ||
                        SearchItemQueryUiState.IsSelectionPending)
                    {
                        throw new InvalidOperationException("Search pick must treat a scaled visible empty inventory slot as UI proof and block world fallback.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimePrefersScaledVisibleSlotOverOldHoverSlot()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[0] = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                    player.inventory[2] = new Terraria.TestRecipeItem { type = 102, stack = 5 };
                    ResetSearchPickFakeUiState(player);

                    var oldHoverItem = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                    TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(
                        oldHoverItem,
                        0,
                        0,
                        99,
                        SearchPickInventorySlotCenterX(0),
                        SearchPickInventorySlotCenterY(0));

                    ArmSearchPickSelectionForTesting();
                    var uiX = SearchPickInventorySlotCenterX(2);
                    var uiY = SearchPickInventorySlotCenterY(0);
                    int fallbackCount;
                    var result = RunSearchPickVisibleSlotSelection(
                        100,
                        ScaleSearchPickUiCoordinate(uiX),
                        ScaleSearchPickUiCoordinate(uiY),
                        uiX,
                        uiY,
                        out fallbackCount);

                    AssertSearchPickResolvedVisibleSlot(
                        result,
                        102,
                        fallbackCount,
                        "Search pick must prefer the current visible UI click slot over an old ItemSlot hover proof.");
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesVisibleChestSlotWhenHoverStale()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    var chest = CreateSearchPickFakeChest(40);
                    chest.item[0] = new Terraria.TestRecipeItem { type = 102, stack = 6 };
                    player.chest = 0;
                    ResetSearchPickFakeUiState(player);
                    Terraria.Main.chest[0] = chest;

                    var staleHoverItem = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                    TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(staleHoverItem, 8, 3, 1, 700, 700);

                    SearchItemQueryUiState.BeginPendingSelection(90, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(91);
                    LegacyMainUiState.SetVisible(false);

                    var fallbackCount = 0;
                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 100, 75, 260),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                                SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            {
                                fallbackCount++;
                                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                                {
                                    ItemType = 104,
                                    SourceKind = "tile",
                                    SourceSummary = "tile;type=5;style=0"
                                });
                            },
                            ConsumeMouseTriggerInput = token => SearchItemPickInputConsumeResult.Success("consumed"),
                            RestoreSearchWindow = () =>
                            {
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    var query = SearchItemQueryUiState.GetSelectedResult();
                    if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                        fallbackCount != 0 ||
                        query == null ||
                        !query.Found ||
                        query.Item == null ||
                        query.Item.ItemType != 102)
                    {
                        throw new InvalidOperationException("Search pick must use the directly hit visible chest slot when the ItemSlot hover cache is stale.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickRuntimeBlocksVisibleEmptyInventorySlotWhenHoverStale()
        {
            WithSearchQueryFixture(() =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreUiState = CaptureSearchPickFakeUiState();
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                try
                {
                    var player = CreateSearchPickFakePlayer();
                    player.inventory[0] = new Terraria.TestRecipeItem { type = 0, stack = 0 };
                    ResetSearchPickFakeUiState(player);

                    var staleHoverItem = new Terraria.TestRecipeItem { type = 100, stack = 1 };
                    TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(staleHoverItem, 8, 3, 1, 700, 700);

                    SearchItemQueryUiState.BeginPendingSelection(90, "test");
                    SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(91);
                    LegacyMainUiState.SetVisible(false);

                    var fallbackCount = 0;
                    var result = SearchItemPickRuntimeService.TickForTesting(
                        CreateSearchPickRuntimeInput(true, 100, 22, 22),
                        new SearchItemPickRuntimePorts
                        {
                            CaptureClickContext = input => CreateSearchPickClickContext(input),
                            ResolvePendingUi = (pending, currentGameUpdateCount) =>
                                SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                            ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                            {
                                fallbackCount++;
                                return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                                {
                                    ItemType = 104,
                                    SourceKind = "tile",
                                    SourceSummary = "tile;type=5;style=0"
                                });
                            },
                            ConsumeMouseTriggerInput = token => SearchItemPickInputConsumeResult.Success("consumed"),
                            RestoreSearchWindow = () =>
                            {
                                LegacyMainUiState.SelectPage("search");
                                LegacyMainUiState.SetVisible(true);
                            }
                        });

                    if (!string.Equals(result.ResultCode, "failed", StringComparison.Ordinal) ||
                        !string.Equals(result.FailureReason, "uiEmptySlot", StringComparison.Ordinal) ||
                        fallbackCount != 0 ||
                        SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.CancelledOrFailed ||
                        SearchItemQueryUiState.IsSelectionPending)
                    {
                        throw new InvalidOperationException("Search pick must treat a directly hit visible empty inventory slot as UI proof and block world fallback.");
                    }
                }
                finally
                {
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                    restoreUiState();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void SearchQueryPickCoordinateResolverSeparatesScaledUiCoordinates()
        {
            var snapshot = SearchItemPickMouseCoordinateResolver.CaptureForTesting(new SearchItemPickMouseCoordinateInput
            {
                TerrariaReadAvailable = true,
                TerrariaMouseX = 125,
                TerrariaMouseY = 250,
                OsReadAvailable = true,
                OsClientMouseX = 125,
                OsClientMouseY = 250,
                UiScaleAvailable = true,
                UiScale = 1.25d,
                UiScaleX = 1.25d,
                UiScaleY = 1.25d,
                ReadMode = "Terraria+OsClient",
                UiScaleSource = "UIScaleMatrix",
                ScreenX = 1000f,
                ScreenY = 2000f,
                GameUpdateCount = 77
            });

            if (snapshot.RawMouseX != 125 ||
                snapshot.RawMouseY != 250 ||
                snapshot.UiMouseX != 100 ||
                snapshot.UiMouseY != 200 ||
                snapshot.WorldMouseX != 1125f ||
                snapshot.WorldMouseY != 2250f ||
                snapshot.TileX != 70 ||
                snapshot.TileY != 140 ||
                snapshot.UiMouseSource.IndexOf("TerrariaScreenToUi", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Search pick coordinate resolver must split raw screen and UI logical coordinates when UI scale proves Terraria mouse is raw screen space.");
            }
        }

        private static void SearchQueryPickCoordinateResolverUsesMouseWorldSeparately()
        {
            var snapshot = SearchItemPickMouseCoordinateResolver.CaptureForTesting(new SearchItemPickMouseCoordinateInput
            {
                TerrariaReadAvailable = true,
                TerrariaMouseX = 40,
                TerrariaMouseY = 50,
                UiScaleAvailable = true,
                UiScale = 1d,
                WorldMouseAvailable = true,
                WorldMouseX = 640.5f,
                WorldMouseY = 512.25f,
                WorldMouseSource = "Main.MouseWorld",
                ScreenX = 1000f,
                ScreenY = 2000f,
                GameUpdateCount = 88
            });

            if (snapshot.RawMouseX != 40 ||
                snapshot.UiMouseX != 40 ||
                Math.Abs(snapshot.WorldMouseX - 640.5f) > 0.001f ||
                Math.Abs(snapshot.WorldMouseY - 512.25f) > 0.001f ||
                snapshot.TileX != 40 ||
                snapshot.TileY != 32 ||
                snapshot.SourceSummary.IndexOf("worldSource=Main.MouseWorld", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Search pick coordinate resolver must keep world mouse coordinates separate from raw/UI coordinates.");
            }
        }

        private static void SearchQueryPickClickContextDerivesTileFromWorldCoordinates()
        {
            var context = SearchItemPickClickContext.Success(
                40,
                50,
                400,
                500,
                640.5f,
                512.25f,
                2,
                3,
                88,
                "raw=testRaw;ui=testUi;world=Main.MouseWorld");

            if (context.MouseTileX != 40 ||
                context.MouseTileY != 32 ||
                context.MouseTileX == 2 ||
                context.MouseTileY == 3 ||
                context.MouseTileX == context.UiMouseX ||
                context.MouseTileY == context.UiMouseY)
            {
                throw new InvalidOperationException("Search pick click context must derive tile coordinates from final world coordinates, not raw/UI or caller-provided tile values.");
            }
        }

        private static void SearchQueryPickClickContextKeepsLayeredCoordinates()
        {
            var input = CreateSearchPickRuntimeInput(true, 90, 40, 50);
            var context = CreateSearchPickClickContext(input);
            var pending = SearchItemPickPendingClick.Create(input, context);
            if (context.RawMouseX != 40 ||
                context.UiMouseX != 40 ||
                Math.Abs(context.MouseWorldX - 200f) > 0.001f ||
                context.MouseWorldX == context.UiMouseX ||
                pending.MouseX != 40 ||
                pending.UiMouseX != 40 ||
                string.IsNullOrWhiteSpace(pending.CoordinateSourceSummary))
            {
                throw new InvalidOperationException("Search pick click context must preserve layered raw/UI/world coordinates instead of forcing every helper coordinate to the same value.");
            }
        }

        private static void SearchQueryPickTargetResolverUsesUiItem()
        {
            var context = new SearchItemPickResolveContext
            {
                UiItemType = 100,
                UiItemStack = 1,
                UiItemSource = "ItemSlot:8:3",
                MouseWorldX = 15f,
                MouseWorldY = 15f,
                Tile = new SearchItemPickTileTarget
                {
                    Active = true,
                    PlacementItemType = 104
                }
            };
            context.WorldItems.Add(new SearchItemPickWorldItemTarget
            {
                ItemType = 101,
                Stack = 1,
                HitboxX = 10f,
                HitboxY = 10f,
                HitboxWidth = 20f,
                HitboxHeight = 20f
            });

            var result = SearchItemPickTargetResolver.Resolve(context);
            if (!result.Succeeded ||
                result.Result == null ||
                result.Result.ItemType != 100 ||
                !string.Equals(result.Result.SourceKind, "uiItem", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search item pick target resolver must prioritize fresh UI hover item snapshots.");
            }
        }

        private static void SearchQueryPickTargetResolverUsesWorldItemCoordinateWhenUiDiffers()
        {
            var context = new SearchItemPickResolveContext
            {
                RawMouseX = 40,
                RawMouseY = 50,
                UiMouseX = 15,
                UiMouseY = 15,
                MouseWorldX = 640f,
                MouseWorldY = 512f
            };
            context.WorldItems.Add(new SearchItemPickWorldItemTarget
            {
                ItemType = 100,
                Stack = 1,
                HitboxX = 10f,
                HitboxY = 10f,
                HitboxWidth = 20f,
                HitboxHeight = 20f
            });
            context.WorldItems.Add(new SearchItemPickWorldItemTarget
            {
                ItemType = 104,
                Stack = 1,
                HitboxX = 636f,
                HitboxY = 508f,
                HitboxWidth = 16f,
                HitboxHeight = 16f
            });

            var result = SearchItemPickTargetResolver.Resolve(context);
            if (!result.Succeeded ||
                result.Result == null ||
                result.Result.ItemType != 104 ||
                !string.Equals(result.Result.SourceKind, "worldItem", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search item pick world fallback must hit dropped items by MouseWorld coordinates, not raw/UI mouse coordinates.");
            }
        }

        private static void SearchQueryPickTargetResolverUsesWorldItem()
        {
            var context = new SearchItemPickResolveContext
            {
                MouseWorldX = 15f,
                MouseWorldY = 15f
            };
            context.WorldItems.Add(new SearchItemPickWorldItemTarget
            {
                ItemType = 101,
                Stack = 3,
                HitboxX = 10f,
                HitboxY = 10f,
                HitboxWidth = 20f,
                HitboxHeight = 20f
            });

            var result = SearchItemPickTargetResolver.Resolve(context);
            if (!result.Succeeded ||
                result.Result == null ||
                result.Result.ItemType != 101 ||
                !string.Equals(result.Result.SourceKind, "worldItem", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search item pick target resolver must use world dropped item hitboxes.");
            }
        }

        private static void SearchQueryPickTargetResolverUsesTileItemId()
        {
            var result = SearchItemPickTargetResolver.Resolve(new SearchItemPickResolveContext
            {
                Tile = new SearchItemPickTileTarget
                {
                    Active = true,
                    TileType = 1,
                    TileStyle = 0,
                    PlacementItemType = 104
                },
                Wall = new SearchItemPickWallTarget
                {
                    Active = true,
                    PlacementItemType = 105
                }
            });

            if (!result.Succeeded ||
                result.Result == null ||
                result.Result.ItemType != 104 ||
                !string.Equals(result.Result.SourceKind, "tile", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search item pick target resolver must query Tile by placement item id before Wall.");
            }
        }

        private static void SearchQueryPickTargetResolverUsesWallItemId()
        {
            var result = SearchItemPickTargetResolver.Resolve(new SearchItemPickResolveContext
            {
                Wall = new SearchItemPickWallTarget
                {
                    Active = true,
                    WallType = 2,
                    PlacementItemType = 105
                }
            });

            if (!result.Succeeded ||
                result.Result == null ||
                result.Result.ItemType != 105 ||
                !string.Equals(result.Result.SourceKind, "wall", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Search item pick target resolver must query Wall by placement item id.");
            }
        }

        private static void SearchQueryPickRuntimeConsumesFailedTargetAndRestores()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.SelectItem(100);
                SearchItemQueryUiState.BeginPendingSelection(20, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(21);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Failed("noSearchableItem")
                };

                var result = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 22),
                    probe.ToPorts());
                if (!string.Equals(result.ResultCode, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(result.SkipReason, "pendingUiHover", StringComparison.Ordinal) ||
                    !result.InputConsumeAttempted ||
                    !result.InputConsumed ||
                    probe.ConsumeCount != 1 ||
                    probe.ResolveCount != 1 ||
                    probe.RestoreCount != 0 ||
                    LegacyMainUiState.Visible ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.ArmedForNextLeftClick ||
                    !SearchItemQueryUiState.IsSelectionPending ||
                    !SearchItemQueryUiState.HasSelectedResult ||
                    SearchItemQueryUiState.SelectedItemType != 100)
                {
                    throw new InvalidOperationException("Search item pick runtime must keep failed target clicks pending for delayed UI hover evidence.");
                }

                var expired = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(false, 30),
                    probe.ToPorts());
                if (!string.Equals(expired.ResultCode, "failed", StringComparison.Ordinal) ||
                    probe.ResolveCount != 3 ||
                    probe.RestoreCount != 1 ||
                    !LegacyMainUiState.Visible ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.CancelledOrFailed ||
                    SearchItemQueryUiState.IsSelectionPending ||
                    SearchItemQueryUiState.HasSelectedResult ||
                    SearchItemQueryUiState.SelectedItemType != 0 ||
                    SearchItemQueryUiState.SelectionHintText.IndexOf("未识别", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Search item pick runtime must restore F5 and fail only after the delayed UI hover window expires.");
                }
            });
        }

        private static void SearchQueryPickRuntimeDoesNotLetWorldFallbackRaceUiPending()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(20, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(21);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolvePendingFallbackAttempt = SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                    {
                        ItemType = 104,
                        SourceKind = "tile",
                        SourceSummary = "tile;type=5;style=0"
                    })
                };
                probe.ResolveAttempts.Enqueue(SearchItemPickResolveAttempt.Failed("uiHoverPending"));
                probe.ResolveAttempts.Enqueue(SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = 101,
                    SourceKind = "uiItem",
                    SourceSummary = "uiItem;source=ItemSlot:8:3"
                }));

                var pending = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 22),
                    probe.ToPorts());
                if (!string.Equals(pending.ResultCode, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(pending.SkipReason, "pendingUiHover", StringComparison.Ordinal) ||
                    probe.ResolveCount != 1 ||
                    probe.FallbackResolveCount != 0 ||
                    probe.RestoreCount != 0 ||
                    !SearchItemQueryUiState.IsSelectionPending)
                {
                    throw new InvalidOperationException("Search item pick must not let a world target resolve before the UI hover pending window expires.");
                }

                var resolved = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 23),
                    probe.ToPorts());
                var query = SearchItemQueryUiState.GetSelectedResult();
                if (!string.Equals(resolved.ResultCode, "resolved", StringComparison.Ordinal) ||
                    probe.ResolveCount != 2 ||
                    probe.FallbackResolveCount != 0 ||
                    SearchItemQueryUiState.IsSelectionPending ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 101)
                {
                    throw new InvalidOperationException("Search item pick must keep waiting for the UI item instead of selecting the world target underneath.");
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesFrozenClickForFallback()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(40, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(41);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Failed("uiHoverPending"),
                    ResolvePendingFallbackOverride = (pending, currentGameUpdateCount) =>
                    {
                        return pending != null && pending.MouseX == 40 && pending.MouseY == 50
                            ? SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                            {
                                ItemType = 104,
                                SourceKind = "tile",
                                SourceSummary = "tile;type=5;style=0"
                            })
                            : SearchItemPickResolveAttempt.Failed("usedCurrentMouse");
                    }
                };

                var pendingResult = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 42, 40, 50),
                    probe.ToPorts());
                if (!string.Equals(pendingResult.ResultCode, "skipped", StringComparison.Ordinal) ||
                    !SearchItemQueryUiState.IsSelectionPending)
                {
                    throw new InvalidOperationException("Expected first search pick click to enter UI pending before fallback.");
                }

                var resolved = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(false, 50, 300, 350),
                    probe.ToPorts());
                var query = SearchItemQueryUiState.GetSelectedResult();
                if (!string.Equals(resolved.ResultCode, "resolved", StringComparison.Ordinal) ||
                    resolved.ItemType != 104 ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 104)
                {
                    throw new InvalidOperationException("Search item pick fallback must use the frozen click coordinates, not the current mouse position after the click.");
                }
            });
        }

        private static void SearchQueryPickRuntimeUsesFrozenWorldForFallback()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(70, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(71);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Failed("uiHoverPending"),
                    CaptureClickContextOverride = input => SearchItemPickClickContext.Success(
                        input.MouseX,
                        input.MouseY,
                        100,
                        125,
                        640f,
                        512f,
                        1,
                        2,
                        input.CurrentGameUpdateCount,
                        "raw=testRaw;ui=testUi;world=Main.MouseWorld"),
                    ResolvePendingFallbackOverride = (pending, currentGameUpdateCount) =>
                    {
                        var click = pending == null ? null : pending.ClickContext;
                        return click != null &&
                               Math.Abs(click.MouseWorldX - 640f) < 0.001f &&
                               Math.Abs(click.MouseWorldY - 512f) < 0.001f &&
                               click.MouseTileX == 40 &&
                               click.MouseTileY == 32 &&
                               click.UiMouseX == 100
                            ? SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                            {
                                ItemType = 104,
                                SourceKind = "tile",
                                SourceSummary = "tile;type=5;style=0"
                            })
                            : SearchItemPickResolveAttempt.Failed("usedNonFrozenWorldCoordinates");
                    }
                };

                var pendingResult = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 72, 40, 50),
                    probe.ToPorts());
                if (!string.Equals(pendingResult.ResultCode, "skipped", StringComparison.Ordinal) ||
                    !SearchItemQueryUiState.IsSelectionPending)
                {
                    throw new InvalidOperationException("Expected search pick to wait for UI proof before world fallback.");
                }

                var resolved = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(false, 80, 900, 950),
                    probe.ToPorts());
                var query = SearchItemQueryUiState.GetSelectedResult();
                if (!string.Equals(resolved.ResultCode, "resolved", StringComparison.Ordinal) ||
                    resolved.ItemType != 104 ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 104)
                {
                    throw new InvalidOperationException("Search item pick fallback must keep the frozen world coordinate even after the mouse moves.");
                }
            });
        }

        private static void SearchQueryPickRuntimeBlocksWorldFallbackOnUiEmptySlot()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(60, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(61);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe
                {
                    ResolveAttempt = SearchItemPickResolveAttempt.Failed("uiEmptySlot"),
                    ResolvePendingFallbackAttempt = SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                    {
                        ItemType = 104,
                        SourceKind = "tile",
                        SourceSummary = "tile;type=5;style=0"
                    })
                };

                var result = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 62),
                    probe.ToPorts());
                if (!string.Equals(result.ResultCode, "failed", StringComparison.Ordinal) ||
                    !string.Equals(result.FailureReason, "uiEmptySlot", StringComparison.Ordinal) ||
                    probe.FallbackResolveCount != 0 ||
                    !LegacyMainUiState.Visible ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.CancelledOrFailed ||
                    SearchItemQueryUiState.IsSelectionPending)
                {
                    throw new InvalidOperationException("Search item pick must treat a fresh empty UI slot as a UI hit and block world fallback.");
                }
            });
        }

        private static void SearchQueryPickRuntimeWaitsDelayedUiItem()
        {
            WithSearchQueryFixture(() =>
            {
                SearchItemQueryUiState.BeginPendingSelection(20, "test");
                SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(21);
                LegacyMainUiState.SetVisible(false);
                var probe = new RecordingSearchItemPickRuntimeProbe();
                probe.ResolveAttempts.Enqueue(SearchItemPickResolveAttempt.Failed("noSearchableItem"));
                probe.ResolveAttempts.Enqueue(SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                {
                    ItemType = 101,
                    SourceKind = "uiItem",
                    SourceSummary = "uiItem;source=ItemSlot:8:3"
                }));

                var pending = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 22),
                    probe.ToPorts());
                if (!string.Equals(pending.ResultCode, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(pending.SkipReason, "pendingUiHover", StringComparison.Ordinal) ||
                    probe.ResolveCount != 1 ||
                    probe.RestoreCount != 0 ||
                    !SearchItemQueryUiState.IsSelectionPending)
                {
                    throw new InvalidOperationException("Search item pick runtime must wait when the first target read has no UI item.");
                }

                var resolved = SearchItemPickRuntimeService.TickForTesting(
                    CreateSearchPickRuntimeInput(true, 23),
                    probe.ToPorts());
                var query = SearchItemQueryUiState.GetSelectedResult();
                if (!string.Equals(resolved.ResultCode, "resolved", StringComparison.Ordinal) ||
                    !resolved.InputConsumeAttempted ||
                    !resolved.InputConsumed ||
                    probe.ConsumeCount != 2 ||
                    probe.ResolveCount != 2 ||
                    probe.RestoreCount != 1 ||
                    SearchItemQueryUiState.IsSelectionPending ||
                    SearchItemQueryUiState.SelectionState != SearchItemPickSelectionState.Resolved ||
                    query == null ||
                    !query.Found ||
                    query.Item == null ||
                    query.Item.ItemType != 101)
                {
                    throw new InvalidOperationException("Search item pick runtime must resolve a delayed UI item before failing or falling through to the world.");
                }
            });
        }

        private static void SearchQueryHoverEntryRequiresFreshHoverSnapshot()
        {
            WithSearchQueryFixture(() =>
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                SearchItemQueryUiState.ResetForTesting();

                if (SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(10, 20, 30))
                {
                    throw new InvalidOperationException("Expected search hover entry to stay unavailable without an ItemSlot hover snapshot.");
                }

                int itemType;
                if (SearchItemQueryUiState.SelectHoverItem(out itemType) || itemType != 0)
                {
                    throw new InvalidOperationException("Expected search hover selection without a snapshot to be ignored.");
                }

                var item = new Terraria.TestRecipeItem { type = 100, stack = 7 };
                if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 10, 4, 100, 20, 30))
                {
                    throw new InvalidOperationException("Expected search test ItemSlot hover snapshot capture to succeed.");
                }

                if (SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(107, 20, 30))
                {
                    throw new InvalidOperationException("Expected stale ItemSlot hover snapshots not to refresh search hover entry.");
                }

                if (SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(101, 26, 30))
                {
                    throw new InvalidOperationException("Expected moved mouse coordinates not to refresh search hover entry.");
                }

                if (!SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(101, 20, 30))
                {
                    throw new InvalidOperationException("Expected fresh ItemSlot hover snapshot to refresh search hover entry.");
                }

                if (!SearchItemQueryUiState.HasHoverItem ||
                    SearchItemQueryUiState.HoverItemType != 100 ||
                    SearchItemQueryUiState.GetHoverItemLabel().IndexOf("x7", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected search hover entry to store only value facts from the fresh ItemSlot snapshot.");
                }
            });

            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
        }

        private static void SearchQueryHoverEntryIgnoresFreshEmptyUiSlotSnapshot()
        {
            WithSearchQueryFixture(() =>
            {
                TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                SearchItemQueryUiState.ResetForTesting();

                var emptyItem = new Terraria.TestRecipeItem { type = 0, stack = 0 };
                if (TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(emptyItem, 8, 5, 200, 40, 50))
                {
                    throw new InvalidOperationException("Expected empty UI slot capture not to report an active item.");
                }

                TerrariaUiHoverSlotSnapshot slotSnapshot;
                if (!TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(201, 40, 50, out slotSnapshot) ||
                    slotSnapshot == null ||
                    slotSnapshot.HasActiveItem)
                {
                    throw new InvalidOperationException("Expected fresh empty UI slot proof to remain readable for quick announcement.");
                }

                if (SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(201, 40, 50) ||
                    SearchItemQueryUiState.HasHoverItem)
                {
                    throw new InvalidOperationException("Search query hover entry must ignore fresh empty UI slot proof and keep no hover item.");
                }
            });

            TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
        }

        private static void SearchQueryHoverCommandSelectsCurrentHoverItemAndClosesCandidates()
        {
            WithSearchQueryFixture(() =>
            {
                ResetSearchQueryCommandState();
                try
                {
                    var item = new Terraria.TestRecipeItem { type = 100, stack = 2 };
                    if (!TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshotForTesting(item, 8, 3, 200, 40, 50) ||
                        !SearchItemQueryUiState.TryRefreshHoverItemFromFreshSnapshot(201, 40, 50))
                    {
                        throw new InvalidOperationException("Expected fresh hover item to be available for the search hover command.");
                    }

                    SearchItemQueryUiState.UpdateDraft("锭");
                    if (SearchItemQueryUiState.CandidateCount <= 0)
                    {
                        throw new InvalidOperationException("Expected search hover command test to start with an open candidate list.");
                    }

                    LegacyTextInput.Focus(SearchItemQueryUiState.InputId, SearchItemQueryUiState.QueryText);
                    EnqueueSearchQueryCommandForTesting("search-query:hover-item");
                    LegacyUiActionService.Update(null, null);

                    var result = SearchItemQueryUiState.GetSelectedResult();
                    if (LegacyUiActionService.DispatchedCommandCountLast != 1 ||
                        result == null ||
                        !result.Found ||
                        result.Item == null ||
                        result.Item.ItemType != 100)
                    {
                        throw new InvalidOperationException("Expected search hover command to select the current hover item through UI action dispatch.");
                    }

                    if (SearchItemQueryUiState.CandidateCount != 0 || LegacyTextInput.IsAnyFocused)
                    {
                        throw new InvalidOperationException("Expected search hover command to close candidates and clear text input focus.");
                    }

                    var history = SearchItemQueryUiState.GetRecentItemTypes();
                    if (history.Count != 1 || history[0] != 100)
                    {
                        throw new InvalidOperationException("Expected search hover command to record the recent queried item.");
                    }
                }
                finally
                {
                    ResetSearchQueryCommandState();
                    TerrariaUiMouseCompat.ResetHoverItemSnapshotForTesting();
                }
            });
        }

        private static void SearchQueryRelatedItemCommandTracksHistoryAndClosesCandidates()
        {
            WithSearchQueryFixture(() =>
            {
                ResetSearchQueryCommandState();
                try
                {
                    if (!SearchItemQueryUiState.SelectItem(100))
                    {
                        throw new InvalidOperationException("Expected first search item selection to succeed.");
                    }

                    SearchItemQueryUiState.UpdateDraft("锭");
                    if (SearchItemQueryUiState.CandidateCount <= 0)
                    {
                        throw new InvalidOperationException("Expected related item jump test to start with an open candidate list.");
                    }

                    LegacyTextInput.Focus(SearchItemQueryUiState.InputId, SearchItemQueryUiState.QueryText);
                    EnqueueSearchQueryCommandForTesting("search-query:item:200:test-source");
                    LegacyUiActionService.Update(null, null);

                    var result = SearchItemQueryUiState.GetSelectedResult();
                    if (LegacyUiActionService.DispatchedCommandCountLast != 1 ||
                        result == null ||
                        !result.Found ||
                        result.Item == null ||
                        result.Item.ItemType != 200)
                    {
                        throw new InvalidOperationException("Expected related item command to jump to the requested item.");
                    }

                    if (SearchItemQueryUiState.CandidateCount != 0 || LegacyTextInput.IsAnyFocused)
                    {
                        throw new InvalidOperationException("Expected related item jump to close candidates and clear text input focus.");
                    }

                    var history = SearchItemQueryUiState.GetRecentItemTypes();
                    if (history.Count < 2 || history[0] != 200 || history[1] != 100)
                    {
                        throw new InvalidOperationException("Expected related item jump to keep most recent query history.");
                    }
                }
                finally
                {
                    ResetSearchQueryCommandState();
                }
            });
        }

        private static ItemQueryResult CreateSearchQueryResultWithAcquisitionSource(
            string sourceType,
            string title,
            string sourceName,
            string quantityText,
            string probabilityText,
            string conditionText,
            string contextText)
        {
            var result = new ItemQueryResult
            {
                ItemType = 100,
                Found = true,
                Status = "ok",
                Item = new ItemQueryReference
                {
                    ItemType = 100,
                    DisplayName = "铁锭",
                    InternalName = "IronBar",
                    Stack = 1,
                    MaxStack = 999,
                    Rare = 0,
                    Value = 0,
                    IsMaterial = true,
                    IsConsumable = false,
                    CreateTile = -1,
                    CreateWall = -1
                }
            };
            result.AcquisitionSources.Add(CreateSearchAcquisitionSource(sourceType, title, sourceName, quantityText, probabilityText, conditionText, contextText));
            return result;
        }

        private static ItemAcquisitionSourceSummary CreateSearchAcquisitionSource(
            string sourceType,
            string title,
            string sourceName,
            string quantityText,
            string probabilityText,
            string conditionText,
            string contextText)
        {
            return new ItemAcquisitionSourceSummary
            {
                SourceType = sourceType,
                Title = title,
                SourceName = sourceName,
                QuantityText = quantityText,
                ProbabilityText = probabilityText,
                ConditionText = conditionText,
                ContextText = contextText,
                ItemType = 100,
                NpcNetId = 1,
                RelatedItemType = -1
            };
        }

        private static void AddSearchQueryExtraUsageRecipes(int materialItemType, int count)
        {
            var recipes = new List<object>(Terraria.Main.recipe ?? new object[0]);
            for (var index = 0; index < count; index++)
            {
                var itemType = 400 + index;
                AddSearchItem(itemType, "滚动测试产物" + index, "SearchScrollProduct" + index, 99, 0, 100, false, false, -1, -1);
                recipes.Add(new Terraria.Recipe
                {
                    createItem = new Terraria.TestRecipeItem { type = itemType, stack = 1 },
                    requiredItem = new object[]
                    {
                        new Terraria.TestRecipeItem { type = materialItemType, stack = index + 1 },
                        new Terraria.TestRecipeItem { type = 0, stack = 0 }
                    },
                    acceptedGroups = new[] { -1 }
                });
            }

            Terraria.Main.recipe = recipes.ToArray();
            Terraria.Recipe.numRecipes = recipes.Count;
        }

        private static void AddSearchQuerySourceRecipe(int productItemType, params int[] ingredientItemTypes)
        {
            AddSearchItem(productItemType, "布局测试产物" + productItemType.ToString(CultureInfo.InvariantCulture), "SearchLayoutProduct" + productItemType.ToString(CultureInfo.InvariantCulture), 99, 1, 0, false, false, -1, -1);
            var recipes = new List<object>(Terraria.Main.recipe ?? new object[0])
            {
                CreateSearchQuerySourceRecipe(productItemType, ingredientItemTypes)
            };
            Terraria.Main.recipe = recipes.ToArray();
            Terraria.Recipe.numRecipes = recipes.Count;
        }

        private static void ReplaceSearchQuerySourceRecipe(int productItemType, params int[] ingredientItemTypes)
        {
            AddSearchItem(productItemType, "布局测试产物" + productItemType.ToString(CultureInfo.InvariantCulture), "SearchLayoutProduct" + productItemType.ToString(CultureInfo.InvariantCulture), 99, 1, 0, false, false, -1, -1);
            Terraria.Main.recipe = new object[] { CreateSearchQuerySourceRecipe(productItemType, ingredientItemTypes) };
            Terraria.Recipe.numRecipes = 1;
        }

        private static Terraria.Recipe CreateSearchQuerySourceRecipe(int productItemType, params int[] ingredientItemTypes)
        {
            var requiredItems = new object[(ingredientItemTypes == null ? 0 : ingredientItemTypes.Length) + 1];
            for (var index = 0; ingredientItemTypes != null && index < ingredientItemTypes.Length; index++)
            {
                requiredItems[index] = new Terraria.TestRecipeItem
                {
                    type = ingredientItemTypes[index],
                    stack = index + 1
                };
            }

            requiredItems[requiredItems.Length - 1] = new Terraria.TestRecipeItem { type = 0, stack = 0 };
            return new Terraria.Recipe
            {
                createItem = new Terraria.TestRecipeItem { type = productItemType, stack = 1 },
                requiredItem = requiredItems,
                acceptedGroups = new[] { -1 }
            };
        }

        private static void AddSearchQueryExtraShimmerSources(int targetItemType, int count)
        {
            for (var index = 0; index < count; index++)
            {
                var itemType = 500 + index;
                AddSearchItem(itemType, "微光测试来源" + index.ToString(CultureInfo.InvariantCulture), "SearchShimmerSource" + index.ToString(CultureInfo.InvariantCulture), 99, 1, 0, false, false, -1, -1);
                Terraria.ID.ItemID.Sets.ShimmerTransformToItem[itemType] = targetItemType;
            }
        }

        private static void EnqueueSearchQueryCommandForTesting(string elementId)
        {
            LegacyUiInput.EnqueueClick(
                new LegacyUiElement
                {
                    Id = elementId,
                    Label = "Search query test",
                    Kind = "button",
                    Rect = new LegacyUiRect(10, 10, 120, 24),
                    Enabled = true
                },
                new LegacyMouseSnapshot
                {
                    X = 20,
                    Y = 20,
                    LeftDown = true,
                    LeftPressed = true,
                    ReadAvailable = true,
                    WindowHit = true
                },
                true);
        }

        private static void ResetSearchQueryCommandState()
        {
            LegacyMainUiState.SetVisible(false);
            LegacyUiInput.ResetInteractionState();
            LegacyUiInput.ResetActionUpdateGateStateForTesting();
            LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
            LegacyTextInput.ClearFocus();
        }

        private const double SearchPickScaledUiTestScale = 1.25d;

        private static void ArmSearchPickSelectionForTesting()
        {
            SearchItemQueryUiState.BeginPendingSelection(90, "test");
            SearchItemQueryUiState.MarkSelectionArmedForNextLeftClick(91);
            LegacyMainUiState.SetVisible(false);
        }

        private static int SearchPickInventorySlotCenterX(int column)
        {
            return SearchPickSlotCenter(20d, column, 0.85d);
        }

        private static int SearchPickInventorySlotCenterY(int row)
        {
            return SearchPickSlotCenter(20d, row, 0.85d);
        }

        private static int SearchPickChestSlotCenterX(int column)
        {
            return SearchPickSlotCenter(73d, column, 0.755d);
        }

        private static int SearchPickChestSlotCenterY(int row)
        {
            return SearchPickSlotCenter(258d, row, 0.755d);
        }

        private static int SearchPickSlotCenter(double start, int index, double inventoryScale)
        {
            return (int)Math.Round(
                start + index * 56d * inventoryScale + 52d * inventoryScale * 0.5d,
                MidpointRounding.AwayFromZero);
        }

        private static int ScaleSearchPickUiCoordinate(int uiCoordinate)
        {
            return (int)Math.Round(uiCoordinate * SearchPickScaledUiTestScale, MidpointRounding.AwayFromZero);
        }

        private static SearchItemPickRuntimeResult RunSearchPickVisibleSlotSelection(
            ulong updateCount,
            int rawMouseX,
            int rawMouseY,
            int uiMouseX,
            int uiMouseY,
            out int fallbackCount)
        {
            var localFallbackCount = 0;
            var result = SearchItemPickRuntimeService.TickForTesting(
                CreateSearchPickRuntimeInput(true, updateCount, rawMouseX, rawMouseY),
                new SearchItemPickRuntimePorts
                {
                    CaptureClickContext = input => CreateSearchPickClickContext(input, uiMouseX, uiMouseY, 640f, 512f),
                    ResolvePendingUi = (pending, currentGameUpdateCount) =>
                        SearchItemPickTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount),
                    ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                    {
                        localFallbackCount++;
                        return SearchItemPickResolveAttempt.Success(new SearchItemPickResolveResult
                        {
                            ItemType = 104,
                            SourceKind = "tile",
                            SourceSummary = "tile;type=5;style=0"
                        });
                    },
                    ConsumeMouseTriggerInput = token => SearchItemPickInputConsumeResult.Success("consumed"),
                    RestoreSearchWindow = () =>
                    {
                        LegacyMainUiState.SelectPage("search");
                        LegacyMainUiState.SetVisible(true);
                    }
                });
            fallbackCount = localFallbackCount;
            return result;
        }

        private static void AssertSearchPickResolvedVisibleSlot(
            SearchItemPickRuntimeResult result,
            int expectedItemType,
            int fallbackCount,
            string message)
        {
            var query = SearchItemQueryUiState.GetSelectedResult();
            if (!string.Equals(result.ResultCode, "resolved", StringComparison.Ordinal) ||
                !result.InputConsumeAttempted ||
                !result.InputConsumed ||
                fallbackCount != 0 ||
                query == null ||
                !query.Found ||
                query.Item == null ||
                query.Item.ItemType != expectedItemType)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static SearchItemPickRuntimeInput CreateSearchPickRuntimeInput(
            bool mouseLeftDown,
            ulong updateCount,
            int mouseX = 40,
            int mouseY = 50)
        {
            return new SearchItemPickRuntimeInput
            {
                IsInWorld = true,
                GameInputAvailable = true,
                MouseReadAvailable = true,
                MouseLeftDown = mouseLeftDown,
                MouseX = mouseX,
                MouseY = mouseY,
                CurrentGameUpdateCount = updateCount
            };
        }

        private static SearchItemPickClickContext CreateSearchPickClickContext(SearchItemPickRuntimeInput input)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            var worldX = input.MouseX + 160f;
            var worldY = input.MouseY + 320f;
            return CreateSearchPickClickContext(input, input.MouseX, input.MouseY, worldX, worldY);
        }

        private static SearchItemPickClickContext CreateSearchPickClickContext(
            SearchItemPickRuntimeInput input,
            int uiMouseX,
            int uiMouseY,
            float worldX,
            float worldY)
        {
            input = input ?? new SearchItemPickRuntimeInput();
            return SearchItemPickClickContext.Success(
                input.MouseX,
                input.MouseY,
                uiMouseX,
                uiMouseY,
                worldX,
                worldY,
                (int)Math.Floor(worldX / TerrariaTileReadCompat.TileSize),
                (int)Math.Floor(worldY / TerrariaTileReadCompat.TileSize),
                input.CurrentGameUpdateCount,
                "raw=testMouse;ui=testUi;world=testWorld");
        }

        private static Terraria.Player CreateSearchPickFakePlayer()
        {
            var player = new Terraria.Player
            {
                inventory = CreateSearchPickFakeItems(59),
                armor = CreateSearchPickFakeItems(20),
                dye = CreateSearchPickFakeItems(10),
                miscEquips = CreateSearchPickFakeItems(5),
                miscDyes = CreateSearchPickFakeItems(5),
                bank = CreateSearchPickFakeChest(40),
                bank2 = CreateSearchPickFakeChest(40),
                bank3 = CreateSearchPickFakeChest(40),
                bank4 = CreateSearchPickFakeChest(40),
                chest = -1
            };
            return player;
        }

        private static object[] CreateSearchPickFakeItems(int count)
        {
            var items = new object[count];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = new Terraria.TestRecipeItem { type = 0, stack = 0 };
            }

            return items;
        }

        private static Terraria.Chest CreateSearchPickFakeChest(int count)
        {
            return new Terraria.Chest
            {
                item = CreateSearchPickFakeItems(count),
                maxItems = count
            };
        }

        private static void ResetSearchPickFakeUiState(Terraria.Player player)
        {
            Terraria.Main.playerInventory = true;
            Terraria.Main.gameMenu = false;
            Terraria.Main.myPlayer = 0;
            Terraria.Main.LocalPlayer = player;
            Terraria.Main.player = new object[256];
            Terraria.Main.player[0] = player;
            Terraria.Main.chest = new object[1000];
            Terraria.Main.screenWidth = 1280;
            Terraria.Main.screenHeight = 800;
            Terraria.Main.mH = 0;
            Terraria.Main.EquipPage = 0;
            Terraria.Main.instance = new Terraria.MainInstance { invBottom = 258 };
        }

        private static Action CaptureSearchPickFakeUiState()
        {
            var previousPlayerInventory = Terraria.Main.playerInventory;
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousMyPlayer = Terraria.Main.myPlayer;
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousPlayers = Terraria.Main.player;
            var previousChests = Terraria.Main.chest;
            var previousScreenWidth = Terraria.Main.screenWidth;
            var previousScreenHeight = Terraria.Main.screenHeight;
            var previousMH = Terraria.Main.mH;
            var previousEquipPage = Terraria.Main.EquipPage;
            var previousInstance = Terraria.Main.instance;
            return () =>
            {
                Terraria.Main.playerInventory = previousPlayerInventory;
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.myPlayer = previousMyPlayer;
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.player = previousPlayers;
                Terraria.Main.chest = previousChests;
                Terraria.Main.screenWidth = previousScreenWidth;
                Terraria.Main.screenHeight = previousScreenHeight;
                Terraria.Main.mH = previousMH;
                Terraria.Main.EquipPage = previousEquipPage;
                Terraria.Main.instance = previousInstance;
            };
        }

        private sealed class RecordingSearchItemPickRuntimeProbe
        {
            public SearchItemPickResolveAttempt ResolveAttempt = SearchItemPickResolveAttempt.Failed("uiHoverPending");
            public SearchItemPickResolveAttempt ResolvePendingFallbackAttempt = SearchItemPickResolveAttempt.Failed("noSearchableItem");
            public readonly Queue<SearchItemPickResolveAttempt> ResolveAttempts = new Queue<SearchItemPickResolveAttempt>();
            public readonly Queue<SearchItemPickResolveAttempt> ResolvePendingFallbackAttempts = new Queue<SearchItemPickResolveAttempt>();
            public bool ConsumeSucceeds = true;
            public int ResolveCount;
            public int UiResolveCount;
            public int FallbackResolveCount;
            public int CaptureCount;
            public int ConsumeCount;
            public int RestoreCount;
            public string LastConsumedToken = string.Empty;
            public Func<SearchItemPickRuntimeInput, SearchItemPickClickContext> CaptureClickContextOverride;
            public Func<SearchItemPickPendingClick, ulong, SearchItemPickResolveAttempt> ResolvePendingUiOverride;
            public Func<SearchItemPickPendingClick, ulong, SearchItemPickResolveAttempt> ResolvePendingFallbackOverride;

            public SearchItemPickRuntimePorts ToPorts()
            {
                return new SearchItemPickRuntimePorts
                {
                    CaptureClickContext = input =>
                    {
                        CaptureCount++;
                        if (CaptureClickContextOverride != null)
                        {
                            return CaptureClickContextOverride(input);
                        }

                        return CreateSearchPickClickContext(input);
                    },
                    ResolvePendingUi = (pending, currentGameUpdateCount) =>
                    {
                        ResolveCount++;
                        UiResolveCount++;
                        if (ResolvePendingUiOverride != null)
                        {
                            return ResolvePendingUiOverride(pending, currentGameUpdateCount);
                        }

                        if (ResolveAttempts.Count > 0)
                        {
                            return ResolveAttempts.Dequeue();
                        }

                        return ResolveAttempt;
                    },
                    ResolvePendingFallback = (pending, currentGameUpdateCount) =>
                    {
                        ResolveCount++;
                        FallbackResolveCount++;
                        if (ResolvePendingFallbackOverride != null)
                        {
                            return ResolvePendingFallbackOverride(pending, currentGameUpdateCount);
                        }

                        if (ResolvePendingFallbackAttempts.Count > 0)
                        {
                            return ResolvePendingFallbackAttempts.Dequeue();
                        }

                        return ResolvePendingFallbackAttempt;
                    },
                    ConsumeMouseTriggerInput = token =>
                    {
                        ConsumeCount++;
                        LastConsumedToken = token ?? string.Empty;
                        return ConsumeSucceeds
                            ? SearchItemPickInputConsumeResult.Success("consumed")
                            : SearchItemPickInputConsumeResult.Failed("blocked");
                    },
                    RestoreSearchWindow = () =>
                    {
                        RestoreCount++;
                        LegacyMainUiState.SelectPage("search");
                        LegacyMainUiState.SetVisible(true);
                    }
                };
            }
        }

        private static void WithSearchQueryFixture(Action test)
        {
            ResetSearchQueryFakes();
            SearchChestLocatorUiState.ResetForTesting();
            try
            {
                PopulateSearchQueryFakes();
                ItemQueryService.ResetForTesting();
                test();
            }
            finally
            {
                SearchItemQueryUiState.ResetForTesting();
                SearchChestLocatorUiState.ResetForTesting();
                LegacyTextInput.ClearFocus();
                ResetSearchQueryFakes();
                ItemQueryService.ResetForTesting();
            }
        }

        private static void PopulateSearchQueryFakes()
        {
            AddSearchItem(100, "铁锭", "IronBar", 999, 1, 500, true, false, -1, -1);
            AddSearchItem(101, "石块", "StoneBlock", 999, 0, 0, true, false, 1, -1);
            AddSearchItem(102, "铅锭", "LeadBar", 999, 1, 450, true, false, -1, -1);
            AddSearchItem(103, "旧铁锭", "OldIronBar", 999, 1, 100, true, false, -1, -1);
            AddSearchItem(104, "木块", "Wood", 999, 0, 0, true, false, 5, -1);
            AddSearchItem(105, "空白资料物品", "FactlessItem", 1, 0, 0, false, false, -1, -1);
            AddSearchItem(200, "铁砧", "IronAnvil", 99, 0, 1500, false, false, 16, -1);
            AddSearchItem(300, "高级铁砧", "AdvancedAnvil", 99, 2, 3000, false, false, 16, -1);

            Terraria.RecipeGroup.recipeGroups[7] = new Terraria.RecipeGroup
            {
                Name = "任意铁锭",
                ValidItems = new List<int> { 100, 102 }
            };

            Terraria.Main.recipe = new object[]
            {
                new Terraria.Recipe
                {
                    createItem = new Terraria.TestRecipeItem { type = 200, stack = 2 },
                    requiredItem = new object[]
                    {
                        new Terraria.TestRecipeItem { type = 100, stack = 3 },
                        new Terraria.TestRecipeItem { type = 101, stack = 5 },
                        new Terraria.TestRecipeItem { type = 0, stack = 0 }
                    },
                    acceptedGroups = new[] { -1 }
                },
                new Terraria.Recipe
                {
                    createItem = new Terraria.TestRecipeItem { type = 300, stack = 1 },
                    requiredItem = new object[]
                    {
                        new Terraria.TestRecipeItem { type = 104, stack = 12 },
                        new Terraria.TestRecipeItem { type = 0, stack = 0 }
                    },
                    acceptedGroups = new[] { 7, -1 }
                }
            };
            Terraria.Recipe.numRecipes = Terraria.Main.recipe.Length;

            Terraria.ID.ItemID.Sets.ShimmerTransformToItem[100] = 200;
            Terraria.ID.ItemID.Sets.ShimmerTransformToItem[103] = 100;
        }

        private static void AddOpenSearchShop(int shopIndex, int talkNpcIndex, int npcType, params object[] items)
        {
            var instance = (Terraria.MainInstance)Terraria.Main.instance;
            if (shopIndex >= instance.shop.Length)
            {
                Array.Resize(ref instance.shop, shopIndex + 1);
            }

            var shop = new Terraria.Chest();
            for (var index = 0; items != null && index < items.Length && index < shop.item.Length; index++)
            {
                shop.item[index] = items[index];
            }

            instance.shop[shopIndex] = shop;
            Terraria.Main.npcShop = shopIndex;
            Terraria.Main.LocalPlayer = new Terraria.Player { talkNPC = talkNpcIndex };
            Terraria.Main.player[Terraria.Main.myPlayer] = Terraria.Main.LocalPlayer;
            Terraria.Main.npc[talkNpcIndex] = new Terraria.NPC { type = npcType };
        }

        private static void AddSearchItem(
            int itemType,
            string displayName,
            string internalName,
            int maxStack,
            int rare,
            int value,
            bool material,
            bool consumable,
            int createTile,
            int createWall)
        {
            Terraria.ID.ContentSamples.ItemsByType[itemType] = new Terraria.ID.TestContentSampleItem
            {
                type = itemType,
                maxStack = maxStack,
                rare = rare,
                value = value,
                consumable = consumable,
                createTile = createTile,
                createWall = createWall
            };
            Terraria.Lang.ItemNames[itemType] = displayName;
            Terraria.ID.ItemID.Search.SetName(itemType, internalName);
            Terraria.ID.ItemID.Sets.IsAMaterial[itemType] = material;
        }

        private static void ResetSearchQueryFakes()
        {
            Terraria.ID.ContentSamples.ItemsByType.Clear();
            Terraria.Lang.ItemNames.Clear();
            Terraria.Lang.NpcNames.Clear();
            Terraria.ID.ItemID.Search.Clear();
            Terraria.ID.NPCID.Search.Clear();
            Terraria.Recipe.numRecipes = 0;
            Terraria.Main.recipe = new object[0];
            Terraria.Main.ItemDropsDB = null;
            Terraria.Main.npcShop = 0;
            Terraria.Main.npc = new object[200];
            Terraria.Main.player = new object[256];
            Terraria.Main.LocalPlayer = null;
            Terraria.Main.GameUpdateCount = 0;
            Terraria.Main.instance = new Terraria.MainInstance();
            Terraria.RecipeGroup.recipeGroups.Clear();
            Terraria.ID.ItemID.Sets.IsAMaterial = new bool[6000];
            Terraria.ID.ItemID.Sets.ShimmerTransformToItem = CreateEmptySearchShimmerTransforms();
        }

        private static int[] CreateEmptySearchShimmerTransforms()
        {
            var transforms = new int[6000];
            for (var index = 0; index < transforms.Length; index++)
            {
                transforms[index] = -1;
            }

            return transforms;
        }

        private static string ResolveSearchQueryRepoRoot()
        {
            var candidates = new[]
            {
                Directory.GetCurrentDirectory(),
                AppDomain.CurrentDomain.BaseDirectory
            };
            for (var index = 0; index < candidates.Length; index++)
            {
                var directory = new DirectoryInfo(candidates[index]);
                while (directory != null)
                {
                    if (File.Exists(Path.Combine(directory.FullName, "JueMingZ.sln")))
                    {
                        return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }

            throw new InvalidOperationException("Unable to locate JueMingZ.sln for search query source-boundary audit.");
        }

        private static void AssertSearchCandidate(IList<ItemQueryCandidate> candidates, int itemType, string label)
        {
            if (candidates == null || candidates.All(candidate => candidate.ItemType != itemType))
            {
                throw new InvalidOperationException("Expected search candidate " + itemType + " for " + label + ".");
            }
        }
    }
}
