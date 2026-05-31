using System;

namespace JueMingZ.Config
{
    public static class InformationSignTextModes
    {
        public const string Off = "Off";
        public const string All = "All";
        public const string Lines = "Lines";
        public const string Characters = "Characters";

        public const int VanillaDisplayWidthPixels = 460;
        public const int VanillaDisplayMaxLines = 10;
        public const int MinLines = 1;
        public const int MaxLines = VanillaDisplayMaxLines;
        public const int DefaultLines = 3;
        public const int MinCharacters = 1;
        public const int MaxCharacters = 1200;
        public const int DefaultCharacters = 80;
        public const int CharacterStep = 1;

        public static string Normalize(string mode)
        {
            if (string.Equals(mode, All, StringComparison.OrdinalIgnoreCase))
            {
                return All;
            }

            if (string.Equals(mode, Lines, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Line", StringComparison.OrdinalIgnoreCase))
            {
                return Lines;
            }

            if (string.Equals(mode, Characters, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Chars", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Character", StringComparison.OrdinalIgnoreCase))
            {
                return Characters;
            }

            return Off;
        }

        public static bool IsEnabled(string mode)
        {
            return !string.Equals(Normalize(mode), Off, StringComparison.OrdinalIgnoreCase);
        }

        public static int ClampLines(int value)
        {
            return Clamp(value, MinLines, MaxLines);
        }

        public static int ClampCharacters(int value)
        {
            return Clamp(value, MinCharacters, MaxCharacters);
        }

        public static int AdjustLines(int current, int direction)
        {
            return ClampLines(ClampLines(current) + (direction >= 0 ? 1 : -1));
        }

        public static int AdjustCharacters(int current, int direction)
        {
            return ClampCharacters(ClampCharacters(current) + (direction >= 0 ? CharacterStep : -CharacterStep));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
