using System;

namespace JueMingZ.Config
{
    public static class FishingFilterModes
    {
        public const string Disabled = "Disabled";
        public const string AllowList = "AllowList";
        public const string DenyList = "DenyList";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                string.Equals(value, Disabled, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
            {
                return Disabled;
            }

            if (string.Equals(value, AllowList, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "WhiteList", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Whitelist", StringComparison.OrdinalIgnoreCase))
            {
                return AllowList;
            }

            if (string.Equals(value, DenyList, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "BlackList", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Blacklist", StringComparison.OrdinalIgnoreCase))
            {
                return DenyList;
            }

            return Disabled;
        }

        public static string DisplayName(string value)
        {
            var normalized = Normalize(value);
            if (string.Equals(normalized, Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return "不启用";
            }

            return string.Equals(normalized, DenyList, StringComparison.OrdinalIgnoreCase) ? "黑名单" : "白名单";
        }
    }
}
