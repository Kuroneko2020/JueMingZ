using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JueMingZ.Automation.Search
{
    internal sealed class ItemQueryCandidate
    {
        public int ItemType { get; set; }

        public string DisplayName { get; set; }

        public string InternalName { get; set; }

        public string IdText
        {
            get { return "#" + ItemType.ToString(CultureInfo.InvariantCulture); }
        }

        public ItemQueryCandidate()
        {
            DisplayName = string.Empty;
            InternalName = string.Empty;
        }
    }

    internal sealed class ItemQueryReference
    {
        public int ItemType { get; set; }

        public string DisplayName { get; set; }

        public string InternalName { get; set; }

        public int Stack { get; set; }

        public int MaxStack { get; set; }

        public int Rare { get; set; }

        // Terraria Item.value raw copper value. Search UI formats it as base value, not live shop price.
        public int Value { get; set; }

        public bool IsMaterial { get; set; }

        public bool IsConsumable { get; set; }

        public int CreateTile { get; set; }

        public int CreateWall { get; set; }

        public ItemQueryReference()
        {
            DisplayName = string.Empty;
            InternalName = string.Empty;
            CreateTile = -1;
            CreateWall = -1;
        }
    }

    internal sealed class ItemQueryIngredientSummary
    {
        public bool IsRecipeGroup { get; set; }

        public int RecipeGroupId { get; set; }

        public string RecipeGroupName { get; set; }

        public ItemQueryReference Item { get; set; }

        public int Stack { get; set; }

        public bool MatchesQueriedItem { get; set; }

        public IList<ItemQueryReference> AcceptedItems { get; private set; }

        public ItemQueryIngredientSummary()
        {
            RecipeGroupName = string.Empty;
            AcceptedItems = new List<ItemQueryReference>();
        }
    }

    internal sealed class ItemQueryRecipeSummary
    {
        public int RecipeIndex { get; set; }

        public ItemQueryReference CreateItem { get; set; }

        public int CreateStack { get; set; }

        public string MatchKind { get; set; }

        public int MatchedRecipeGroupId { get; set; }

        public IList<ItemQueryIngredientSummary> Ingredients { get; private set; }

        public ItemQueryRecipeSummary()
        {
            MatchKind = string.Empty;
            Ingredients = new List<ItemQueryIngredientSummary>();
        }
    }

    internal sealed class ItemQueryShimmerSummary
    {
        public ItemQueryReference ForwardResult { get; set; }

        public IList<ItemQueryReference> ReverseSources { get; private set; }

        public bool HasAnyRelation
        {
            get { return ForwardResult != null || ReverseSources.Count > 0; }
        }

        public ItemQueryShimmerSummary()
        {
            ReverseSources = new List<ItemQueryReference>();
        }
    }

    internal sealed class ItemQueryResult
    {
        public int ItemType { get; set; }

        public bool Found { get; set; }

        public string Status { get; set; }

        public ItemQueryReference Item { get; set; }

        public IList<ItemAcquisitionSourceSummary> AcquisitionSources { get; private set; }

        public IList<ItemQueryRecipeSummary> CraftingSources { get; private set; }

        public IList<ItemQueryRecipeSummary> CraftingUses { get; private set; }

        public ItemQueryShimmerSummary Shimmer { get; set; }

        public ItemQueryResult()
        {
            Status = string.Empty;
            AcquisitionSources = new List<ItemAcquisitionSourceSummary>();
            CraftingSources = new List<ItemQueryRecipeSummary>();
            CraftingUses = new List<ItemQueryRecipeSummary>();
            Shimmer = new ItemQueryShimmerSummary();
        }
    }

    internal static class ItemValueFormatter
    {
        private const int CopperPerSilver = 100;
        private const int CopperPerGold = CopperPerSilver * 100;
        private const int CopperPerPlatinum = CopperPerGold * 100;

        public static string FormatBaseValue(int copperValue)
        {
            if (copperValue <= 0)
            {
                return "无价值";
            }

            var builder = new StringBuilder(32);
            var remaining = copperValue;
            AppendCoin(builder, ref remaining, CopperPerPlatinum, "铂金币");
            AppendCoin(builder, ref remaining, CopperPerGold, "金币");
            AppendCoin(builder, ref remaining, CopperPerSilver, "银币");
            AppendCoin(builder, ref remaining, 1, "铜币");
            return builder.Length <= 0 ? "无价值" : builder.ToString();
        }

        private static void AppendCoin(StringBuilder builder, ref int remaining, int unitValue, string unitName)
        {
            var count = remaining / unitValue;
            if (count <= 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(count.ToString(CultureInfo.InvariantCulture));
            builder.Append(unitName);
            remaining %= unitValue;
        }
    }
}
