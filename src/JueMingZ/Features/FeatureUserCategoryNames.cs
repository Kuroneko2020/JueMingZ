namespace JueMingZ.Features
{
    public static class FeatureUserCategoryNames
    {
        public static string GetDisplayName(FeatureUserCategory category)
        {
            switch (category)
            {
                case FeatureUserCategory.Misc:
                    return "杂项";
                case FeatureUserCategory.MapEnhancement:
                    return "地图加强";
                case FeatureUserCategory.Search:
                    return "搜索查询";
                case FeatureUserCategory.Hotkeys:
                    return "快捷按键";
                case FeatureUserCategory.Blueprint:
                    return "蓝图";
                case FeatureUserCategory.Fishing:
                    return "钓鱼";
                case FeatureUserCategory.Combat:
                    return "战斗";
                case FeatureUserCategory.MoreInformation:
                    return "更多信息";
                case FeatureUserCategory.Buff:
                    return "增益";
                case FeatureUserCategory.Movement:
                    return "移动";
                case FeatureUserCategory.Diagnostics:
                    return "诊断";
                default:
                    return category.ToString();
            }
        }
    }
}
