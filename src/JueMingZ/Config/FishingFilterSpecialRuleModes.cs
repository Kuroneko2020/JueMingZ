using System;

namespace JueMingZ.Config
{
    public static class FishingFilterSpecialRuleModes
    {
        public const string Follow = "Follow";
        public const string Allow = "Allow";
        public const string Deny = "Deny";

        public static string Normalize(string value)
        {
            if (string.Equals(value, Allow, StringComparison.OrdinalIgnoreCase))
            {
                return Allow;
            }

            if (string.Equals(value, Deny, StringComparison.OrdinalIgnoreCase))
            {
                return Deny;
            }

            return Follow;
        }
    }
}
