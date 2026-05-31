namespace JueMingZ.Features
{
    public static class FeatureCodeDomainExtensions
    {
        public static bool IsPublicDomain(this FeatureCodeDomain domain)
        {
            switch (domain)
            {
                case FeatureCodeDomain.Information:
                case FeatureCodeDomain.Fishing:
                case FeatureCodeDomain.Combat:
                case FeatureCodeDomain.Movement:
                case FeatureCodeDomain.BuffAndRecovery:
                case FeatureCodeDomain.InventoryAndItems:
                case FeatureCodeDomain.WorldAutomation:
                case FeatureCodeDomain.NpcServices:
                case FeatureCodeDomain.MapEnhancement:
                case FeatureCodeDomain.Blueprint:
                case FeatureCodeDomain.Search:
                    return true;
                default:
                    return false;
            }
        }

        public static string ToCanonicalName(this FeatureCodeDomain domain)
        {
            switch (domain)
            {
                case FeatureCodeDomain.Information:
                    return "Information";
                case FeatureCodeDomain.Fishing:
                    return "Fishing";
                case FeatureCodeDomain.Combat:
                    return "Combat";
                case FeatureCodeDomain.Movement:
                    return "Movement";
                case FeatureCodeDomain.BuffAndRecovery:
                    return "BuffAndRecovery";
                case FeatureCodeDomain.InventoryAndItems:
                    return "InventoryAndItems";
                case FeatureCodeDomain.WorldAutomation:
                    return "WorldAutomation";
                case FeatureCodeDomain.NpcServices:
                    return "NpcServices";
                case FeatureCodeDomain.MapEnhancement:
                    return "MapEnhancement";
                case FeatureCodeDomain.Blueprint:
                    return "Blueprint";
                case FeatureCodeDomain.Search:
                    return "Search";
                default:
                    return "Information";
            }
        }
    }
}
