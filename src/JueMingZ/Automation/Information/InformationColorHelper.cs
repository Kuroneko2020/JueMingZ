using System;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationColorHelper
    {
        private static readonly string[] Presets =
        {
            "#CD5C5C",
            "#90EE90",
            "#5DADEC",
            "#FFD966",
            "#FFD700",
            "#FFA500",
            "#FF5555",
            "#FF69B4",
            "#7CFC00",
            "#9370DB",
            "#87CEFA",
            "#FFB366"
        };

        public static InformationColor EnemyName(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationEnemyNameColor, new InformationColor(205, 92, 92, 255));
        }

        public static InformationColor CritterName(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationCritterNameColor, new InformationColor(93, 173, 236, 255));
        }

        public static InformationColor GoldCritterName()
        {
            return new InformationColor(255, 215, 0, 255);
        }

        public static InformationColor NpcName(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationNpcNameColor, new InformationColor(144, 238, 144, 255));
        }

        public static InformationColor ChestName(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationChestNameColor, new InformationColor(255, 165, 0, 255));
        }

        public static InformationColor SignText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationSignTextColor, new InformationColor(230, 193, 106, 255));
        }

        public static InformationColor TombstoneText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationTombstoneTextColor, new InformationColor(255, 85, 85, 255));
        }

        public static InformationColor LifeCrystal(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationLifeCrystalHighlightColor, new InformationColor(255, 105, 180, 255));
        }

        public static InformationColor ManaCrystal(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationManaCrystalHighlightColor, new InformationColor(102, 204, 255, 255));
        }

        public static InformationColor Digtoise(AppSettings settings)
        {
            return new InformationColor(255, 196, 96, 255);
        }

        public static InformationColor LifeFruit(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationLifeFruitHighlightColor, new InformationColor(124, 252, 0, 255));
        }

        public static InformationColor DragonEgg(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationDragonEggHighlightColor, new InformationColor(147, 112, 219, 255));
        }

        public static InformationColor BiomeText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationBiomeTextColor, new InformationColor(144, 238, 144, 255));
        }

        public static InformationColor WorldInfectionText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationWorldInfectionTextColor, new InformationColor(221, 160, 221, 255));
        }

        public static InformationColor LuckText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationLuckTextColor, new InformationColor(250, 250, 210, 255));
        }

        public static InformationColor FishingCatchesText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationFishingCatchesTextColor, new InformationColor(135, 206, 250, 255));
        }

        public static InformationColor FishingFilteredCatchesText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationFishingFilteredCatchesTextColor, new InformationColor(255, 179, 102, 255));
        }

        public static InformationColor AnglerQuestText(AppSettings settings)
        {
            return ParseHex(settings == null ? null : settings.InformationAnglerQuestTextColor, new InformationColor(224, 255, 255, 255));
        }

        public static string CyclePreset(string current)
        {
            var normalized = NormalizeHex(current);
            for (var index = 0; index < Presets.Length; index++)
            {
                if (string.Equals(Presets[index], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return Presets[(index + 1) % Presets.Length];
                }
            }

            return Presets[0];
        }

        public static InformationColor ParseHex(string hex, InformationColor fallback)
        {
            var normalized = NormalizeHex(hex);
            if (normalized.Length != 7)
            {
                return fallback;
            }

            try
            {
                var r = Convert.ToInt32(normalized.Substring(1, 2), 16);
                var g = Convert.ToInt32(normalized.Substring(3, 2), 16);
                var b = Convert.ToInt32(normalized.Substring(5, 2), 16);
                return new InformationColor(r, g, b, fallback.A <= 0 ? 255 : fallback.A);
            }
            catch
            {
                return fallback;
            }
        }

        private static string NormalizeHex(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim();
            return text.StartsWith("#", StringComparison.Ordinal) ? text : "#" + text;
        }
    }
}
