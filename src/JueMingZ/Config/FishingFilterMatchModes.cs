using System;

namespace JueMingZ.Config
{
    public static class FishingFilterMatchModes
    {
        public const string Exact = "Exact";
        public const string Keyword = "Keyword";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return Keyword;
            }

            return Exact;
        }

        public static string EditorTitle(string value)
        {
            var normalized = Normalize(value);
            if (string.Equals(normalized, Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return "关键词匹配";
            }

            return "精确匹配";
        }
    }
}
