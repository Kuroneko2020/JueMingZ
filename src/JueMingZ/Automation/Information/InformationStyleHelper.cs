using System;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class InformationStyleHelper
    {
        public const double MinFontScale = 0.50d;
        public const double MaxFontScale = 1.80d;
        public const double FontScaleStep = 0.10d;

        public const string EnemyNameFeatureId = "information.enemy_name_labels";
        public const string CritterNameFeatureId = "information.critter_name_labels";
        public const string NpcNameFeatureId = "information.npc_name_labels";
        public const string ChestNameFeatureId = "information.chest_name_labels";
        public const string SignTextFeatureId = "information.sign_text_labels";
        public const string TombstoneTextFeatureId = "information.tombstone_text_labels";
        public const string BiomeDisplayFeatureId = "information.biome_display";
        public const string WorldInfectionFeatureId = "information.world_infection";
        public const string LuckValueFeatureId = "information.luck_value";
        public const string FishingCatchesFeatureId = "information.fishing_catches";
        public const string FishingFilteredCatchesFeatureId = "information.fishing_filtered_catches";
        public const string AnglerQuestFeatureId = "information.angler_quest";

        public static bool IsConfigurable(string featureId)
        {
            return !string.IsNullOrWhiteSpace(featureId) &&
                   (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal) ||
                    string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal));
        }

        public static string GetDisplayName(string featureId)
        {
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) return "敌怪显名";
            if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) return "动物显名";
            if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) return "NPC显名";
            if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) return "宝箱显名";
            if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) return "牌子显示";
            if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) return "墓碑显示";
            if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) return "群系显示";
            if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) return "世界感染";
            if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) return "幸运值";
            if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) return "完整鱼获";
            if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) return "过滤鱼获";
            if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) return "渔夫任务";
            return string.Empty;
        }

        public static string GetColorHex(AppSettings settings, string featureId)
        {
            settings = settings ?? AppSettings.CreateDefault();
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationEnemyNameColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationCritterNameColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationNpcNameColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationChestNameColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationSignTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationTombstoneTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationBiomeTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationWorldInfectionTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationLuckTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationFishingCatchesTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationFishingFilteredCatchesTextColor, DefaultColorHex(featureId));
            if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) return NormalizeHtmlColor(settings.InformationAnglerQuestTextColor, DefaultColorHex(featureId));
            return "#FFFFFF";
        }

        public static bool SetColorHex(AppSettings settings, string featureId, string colorHex)
        {
            if (settings == null || !IsConfigurable(featureId))
            {
                return false;
            }

            var normalized = NormalizeHtmlColor(colorHex, GetColorHex(settings, featureId));
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) settings.InformationEnemyNameColor = normalized;
            else if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) settings.InformationCritterNameColor = normalized;
            else if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) settings.InformationNpcNameColor = normalized;
            else if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) settings.InformationChestNameColor = normalized;
            else if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) settings.InformationSignTextColor = normalized;
            else if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) settings.InformationTombstoneTextColor = normalized;
            else if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) settings.InformationBiomeTextColor = normalized;
            else if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) settings.InformationWorldInfectionTextColor = normalized;
            else if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) settings.InformationLuckTextColor = normalized;
            else if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) settings.InformationFishingCatchesTextColor = normalized;
            else if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) settings.InformationFishingFilteredCatchesTextColor = normalized;
            else if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) settings.InformationAnglerQuestTextColor = normalized;
            return true;
        }

        public static InformationColor GetColor(AppSettings settings, string featureId)
        {
            return InformationColorHelper.ParseHex(GetColorHex(settings, featureId), new InformationColor(255, 255, 255, 255));
        }

        public static double GetFontScale(AppSettings settings, string featureId)
        {
            settings = settings ?? AppSettings.CreateDefault();
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationEnemyNameFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationCritterNameFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationNpcNameFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationChestNameFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationSignTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationTombstoneTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationBiomeTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationWorldInfectionTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationLuckTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationFishingCatchesTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationFishingFilteredCatchesTextFontScale, DefaultFontScale(featureId));
            if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) return NormalizeFontScale(settings.InformationAnglerQuestTextFontScale, DefaultFontScale(featureId));
            return 0.72d;
        }

        public static bool SetFontScale(AppSettings settings, string featureId, double scale)
        {
            if (settings == null || !IsConfigurable(featureId))
            {
                return false;
            }

            var normalized = NormalizeFontScale(scale, DefaultFontScale(featureId));
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) settings.InformationEnemyNameFontScale = normalized;
            else if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) settings.InformationCritterNameFontScale = normalized;
            else if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) settings.InformationNpcNameFontScale = normalized;
            else if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) settings.InformationChestNameFontScale = normalized;
            else if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) settings.InformationSignTextFontScale = normalized;
            else if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) settings.InformationTombstoneTextFontScale = normalized;
            else if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) settings.InformationBiomeTextFontScale = normalized;
            else if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) settings.InformationWorldInfectionTextFontScale = normalized;
            else if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) settings.InformationLuckTextFontScale = normalized;
            else if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) settings.InformationFishingCatchesTextFontScale = normalized;
            else if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) settings.InformationFishingFilteredCatchesTextFontScale = normalized;
            else if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) settings.InformationAnglerQuestTextFontScale = normalized;
            return true;
        }

        public static bool ResetToDefault(AppSettings settings, string featureId)
        {
            if (settings == null || !IsConfigurable(featureId))
            {
                return false;
            }

            SetColorHex(settings, featureId, DefaultColorHex(featureId));
            SetFontScale(settings, featureId, DefaultFontScale(featureId));
            return true;
        }

        public static double AdjustFontScale(AppSettings settings, string featureId, int direction)
        {
            var current = GetFontScale(settings, featureId);
            var next = current + (direction >= 0 ? FontScaleStep : -FontScaleStep);
            next = NormalizeFontScale(next, DefaultFontScale(featureId));
            SetFontScale(settings, featureId, next);
            return next;
        }

        public static double NormalizeFontScale(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                value = fallback;
            }

            if (value < MinFontScale)
            {
                return MinFontScale;
            }

            return value > MaxFontScale ? MaxFontScale : Math.Round(value, 2);
        }

        public static string FormatFontScale(double scale)
        {
            return NormalizeFontScale(scale, 0.72d).ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string NormalizeHtmlColor(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (!text.StartsWith("#", StringComparison.Ordinal))
            {
                text = "#" + text;
            }

            if (text.Length != 7)
            {
                return fallback;
            }

            for (var index = 1; index < text.Length; index++)
            {
                if (!IsHexChar(text[index]))
                {
                    return fallback;
                }
            }

            return text.ToUpperInvariant();
        }

        public static void ColorToHsl(InformationColor color, out int hue, out int saturation, out int lightness)
        {
            var r = color.R / 255d;
            var g = color.G / 255d;
            var b = color.B / 255d;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var h = 0d;
            var s = 0d;
            var l = (max + min) / 2d;

            if (Math.Abs(max - min) > 0.0001d)
            {
                var d = max - min;
                s = l > 0.5d ? d / (2d - max - min) : d / (max + min);
                if (Math.Abs(max - r) < 0.0001d)
                {
                    h = (g - b) / d + (g < b ? 6d : 0d);
                }
                else if (Math.Abs(max - g) < 0.0001d)
                {
                    h = (b - r) / d + 2d;
                }
                else
                {
                    h = (r - g) / d + 4d;
                }

                h /= 6d;
            }

            hue = ClampInt((int)Math.Round(h * 360d), 0, 360);
            saturation = ClampInt((int)Math.Round(s * 100d), 0, 100);
            lightness = ClampInt((int)Math.Round(l * 100d), 0, 100);
        }

        public static string ColorFromHsl(int hue, int saturation, int lightness)
        {
            var h = ClampInt(hue, 0, 360) / 360d;
            var s = ClampInt(saturation, 0, 100) / 100d;
            var l = ClampInt(lightness, 0, 100) / 100d;
            double r;
            double g;
            double b;
            if (s <= 0.0001d)
            {
                r = l;
                g = l;
                b = l;
            }
            else
            {
                var q = l < 0.5d ? l * (1d + s) : l + s - l * s;
                var p = 2d * l - q;
                r = HueToRgb(p, q, h + 1d / 3d);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1d / 3d);
            }

            return "#" +
                   ToHex((int)Math.Round(r * 255d)) +
                   ToHex((int)Math.Round(g * 255d)) +
                   ToHex((int)Math.Round(b * 255d));
        }

        public static string DefaultColorHex(string featureId)
        {
            if (string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal)) return "#CD5C5C";
            if (string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal)) return "#5DADEC";
            if (string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal)) return "#90EE90";
            if (string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal)) return "#FFA500";
            if (string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal)) return "#E6C16A";
            if (string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal)) return "#FF5555";
            if (string.Equals(featureId, BiomeDisplayFeatureId, StringComparison.Ordinal)) return "#90EE90";
            if (string.Equals(featureId, WorldInfectionFeatureId, StringComparison.Ordinal)) return "#DDA0DD";
            if (string.Equals(featureId, LuckValueFeatureId, StringComparison.Ordinal)) return "#FAFAD2";
            if (string.Equals(featureId, FishingCatchesFeatureId, StringComparison.Ordinal)) return "#87CEFA";
            if (string.Equals(featureId, FishingFilteredCatchesFeatureId, StringComparison.Ordinal)) return "#FFB366";
            if (string.Equals(featureId, AnglerQuestFeatureId, StringComparison.Ordinal)) return "#E0FFFF";
            return "#FFFFFF";
        }

        private static double DefaultFontScale(string featureId)
        {
            return IsWorldLabel(featureId) ? 0.70d : 0.72d;
        }

        private static bool IsWorldLabel(string featureId)
        {
            return string.Equals(featureId, EnemyNameFeatureId, StringComparison.Ordinal) ||
                   string.Equals(featureId, CritterNameFeatureId, StringComparison.Ordinal) ||
                   string.Equals(featureId, NpcNameFeatureId, StringComparison.Ordinal) ||
                   string.Equals(featureId, ChestNameFeatureId, StringComparison.Ordinal) ||
                   string.Equals(featureId, SignTextFeatureId, StringComparison.Ordinal) ||
                   string.Equals(featureId, TombstoneTextFeatureId, StringComparison.Ordinal);
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0d) t += 1d;
            if (t > 1d) t -= 1d;
            if (t < 1d / 6d) return p + (q - p) * 6d * t;
            if (t < 1d / 2d) return q;
            if (t < 2d / 3d) return p + (q - p) * (2d / 3d - t) * 6d;
            return p;
        }

        private static string ToHex(int value)
        {
            return ClampInt(value, 0, 255).ToString("X2", CultureInfo.InvariantCulture);
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min) return min;
            return value > max ? max : value;
        }

        private static bool IsHexChar(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }
    }
}
