using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Information
{
    // Luck breakdown lines are read-only display; unavailable contributors are omitted rather than fabricated.
    internal static class InformationLuckBreakdownBuilder
    {
        private const double ContributionEpsilon = 0.0005d;
        private const int DefaultSourceLineMaxChars = 44;
        private const int DefaultLadyBugGoodLuckTime = 43200;
        private const int DefaultLadyBugBadLuckTime = -10800;

        public static bool TryBuildDisplayLines(InformationWorldContext context, out IList<string> lines)
        {
            lines = new List<string>();
            if (context == null || context.LocalPlayer == null)
            {
                return false;
            }

            double actualLuck;
            if (!TryReadNumber(context.LocalPlayer, "luck", out actualLuck))
            {
                return false;
            }

            var contributions = BuildContributions(context);
            lines = BuildDisplayLines(actualLuck, contributions, DefaultSourceLineMaxChars);
            return lines.Count > 0;
        }

        internal static double CalculateCoinLuckContributionForTesting(double coinLuck)
        {
            return CalculateCoinLuckContribution(coinLuck);
        }

        internal static double CalculateLadyBugContributionForTesting(int timeLeft, int goodLuckTime, int badLuckTime)
        {
            return CalculateLadyBugContribution(timeLeft, goodLuckTime, badLuckTime);
        }

        internal static string[] BuildDisplayLinesForTesting(double actualLuck, IList<InformationLuckContribution> contributions, int sourceLineMaxChars)
        {
            var lines = BuildDisplayLines(actualLuck, contributions, Math.Max(12, sourceLineMaxChars));
            var result = new string[lines.Count];
            for (var index = 0; index < lines.Count; index++)
            {
                result[index] = lines[index];
            }

            return result;
        }

        private static IList<InformationLuckContribution> BuildContributions(InformationWorldContext context)
        {
            var result = new List<InformationLuckContribution>();
            var player = context.LocalPlayer;

            int ladyBugTimeLeft;
            if (InformationReflection.TryReadInt(player, "ladyBugLuckTimeLeft", out ladyBugTimeLeft) && ladyBugTimeLeft != 0)
            {
                var goodLuckTime = ReadNpcLuckTime("ladyBugGoodLuckTime", DefaultLadyBugGoodLuckTime);
                var badLuckTime = ReadNpcLuckTime("ladyBugBadLuckTime", DefaultLadyBugBadLuckTime);
                AddContribution(
                    result,
                    "瓢虫",
                    CalculateLadyBugContribution(ladyBugTimeLeft, goodLuckTime, badLuckTime),
                    "剩" + FormatTicks(Math.Abs(ladyBugTimeLeft)));
            }

            double torchLuck;
            if (TryReadNumber(player, "torchLuck", out torchLuck))
            {
                AddContribution(result, "火把", torchLuck * 0.2d, "原值" + FormatSigned(torchLuck));
            }

            int luckPotion;
            if (InformationReflection.TryReadInt(player, "luckPotion", out luckPotion))
            {
                AddContribution(result, "幸运药水", luckPotion * 0.1d, "等级" + luckPotion.ToString(CultureInfo.InvariantCulture));
            }

            int kiteLuckLevel;
            if (InformationReflection.TryReadInt(player, "kiteLuckLevel", out kiteLuckLevel))
            {
                AddContribution(result, "风筝", kiteLuckLevel * 0.1d / 3d, "等级" + kiteLuckLevel.ToString(CultureInfo.InvariantCulture));
            }

            bool usedGalaxyPearl;
            if (InformationReflection.TryReadBool(player, "usedGalaxyPearl", out usedGalaxyPearl) && usedGalaxyPearl)
            {
                AddContribution(result, "银河珍珠", 0.03d, string.Empty);
            }

            if (ReadLanternNightActive(context))
            {
                AddContribution(result, "灯笼夜", 0.3d, string.Empty);
            }

            bool hasGardenGnomeNearby;
            if (InformationReflection.TryReadBool(player, "HasGardenGnomeNearby", out hasGardenGnomeNearby) && hasGardenGnomeNearby)
            {
                AddContribution(result, "花园侏儒", 0.2d, string.Empty);
            }

            bool stinky;
            if (InformationReflection.TryReadBool(player, "stinky", out stinky) && stinky)
            {
                AddContribution(result, "臭味", -0.25d, string.Empty);
            }

            double equipmentBasedLuckBonus;
            if (TryReadNumber(player, "equipmentBasedLuckBonus", out equipmentBasedLuckBonus))
            {
                AddContribution(result, "装备", equipmentBasedLuckBonus, string.Empty);
            }

            double coinLuck;
            if (TryReadNumber(player, "coinLuck", out coinLuck))
            {
                AddContribution(result, "钱币", CalculateCoinLuckContribution(coinLuck), "原值" + FormatNumber(coinLuck) + "/" + DescribeCoinLuckTier(coinLuck));
            }

            bool brokenMirrorBadLuck;
            if (InformationReflection.TryReadBool(player, "brokenMirrorBadLuck", out brokenMirrorBadLuck) && brokenMirrorBadLuck)
            {
                AddContribution(result, "破镜坏运", -0.25d, string.Empty);
            }

            return result;
        }

        private static IList<string> BuildDisplayLines(double actualLuck, IList<InformationLuckContribution> contributions, int sourceLineMaxChars)
        {
            var visibleContributions = new List<InformationLuckContribution>();
            var knownTotal = 0d;
            if (contributions != null)
            {
                for (var index = 0; index < contributions.Count; index++)
                {
                    var contribution = contributions[index];
                    if (contribution == null || Math.Abs(contribution.Amount) < ContributionEpsilon)
                    {
                        continue;
                    }

                    knownTotal += contribution.Amount;
                    visibleContributions.Add(contribution);
                }
            }

            var lines = new List<string>();
            lines.Add("幸运值: " + FormatSigned(actualLuck));

            var unresolved = actualLuck - knownTotal;
            if (Math.Abs(unresolved) >= ContributionEpsilon)
            {
                visibleContributions.Add(new InformationLuckContribution
                {
                    Label = "其他/未解析",
                    Amount = unresolved,
                    Detail = "player.luck差异"
                });
            }

            AddSourceLines(lines, visibleContributions, sourceLineMaxChars);
            return lines;
        }

        private static void AddSourceLines(ICollection<string> lines, IList<InformationLuckContribution> contributions, int sourceLineMaxChars)
        {
            if (contributions == null || contributions.Count <= 0)
            {
                lines.Add("  来源: 无");
                return;
            }

            var prefix = "  来源: ";
            var continuationPrefix = "    ";
            var current = prefix;
            for (var index = 0; index < contributions.Count; index++)
            {
                var text = FormatContribution(contributions[index]);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var separator = string.Equals(current, prefix, StringComparison.Ordinal) ||
                                string.Equals(current, continuationPrefix, StringComparison.Ordinal)
                    ? string.Empty
                    : "，";
                var candidate = current + separator + text;
                if (candidate.Length > sourceLineMaxChars &&
                    !string.Equals(current, prefix, StringComparison.Ordinal) &&
                    !string.Equals(current, continuationPrefix, StringComparison.Ordinal))
                {
                    lines.Add(current);
                    current = continuationPrefix + text;
                }
                else
                {
                    current = candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(current) &&
                !string.Equals(current, prefix, StringComparison.Ordinal) &&
                !string.Equals(current, continuationPrefix, StringComparison.Ordinal))
            {
                lines.Add(current);
            }
        }

        private static void AddContribution(ICollection<InformationLuckContribution> result, string label, double amount, string detail)
        {
            if (Math.Abs(amount) < ContributionEpsilon)
            {
                return;
            }

            result.Add(new InformationLuckContribution
            {
                Label = label ?? string.Empty,
                Amount = amount,
                Detail = detail ?? string.Empty
            });
        }

        private static double CalculateLadyBugContribution(int timeLeft, int goodLuckTime, int badLuckTime)
        {
            if (timeLeft > 0 && goodLuckTime != 0)
            {
                return timeLeft / (double)goodLuckTime * 0.2d;
            }

            if (timeLeft < 0 && badLuckTime != 0)
            {
                return -timeLeft / (double)badLuckTime * 0.2d;
            }

            return 0d;
        }

        private static double CalculateCoinLuckContribution(double coinLuck)
        {
            if (Math.Abs(coinLuck) < double.Epsilon)
            {
                return 0d;
            }

            if (coinLuck > 249000d) return 0.2d;
            if (coinLuck > 24900d) return 0.175d;
            if (coinLuck > 2490d) return 0.15d;
            if (coinLuck > 249d) return 0.125d;
            if (coinLuck > 24.9d) return 0.1d;
            if (coinLuck > 2.49d) return 0.075d;
            if (coinLuck > 0.249d) return 0.05d;
            return 0.025d;
        }

        private static string DescribeCoinLuckTier(double coinLuck)
        {
            if (Math.Abs(coinLuck) < double.Epsilon) return "0";
            if (coinLuck > 249000d) return ">249000";
            if (coinLuck > 24900d) return ">24900";
            if (coinLuck > 2490d) return ">2490";
            if (coinLuck > 249d) return ">249";
            if (coinLuck > 24.9d) return ">24.9";
            if (coinLuck > 2.49d) return ">2.49";
            if (coinLuck > 0.249d) return ">0.249";
            return "非0";
        }

        private static bool ReadLanternNightActive(InformationWorldContext context)
        {
            var type = InformationReflection.FindType("Terraria.GameContent.Events.LanternNight");
            bool active;
            return InformationReflection.TryReadStaticBool(type, "LanternsUp", out active) && active;
        }

        private static int ReadNpcLuckTime(string fieldName, int fallback)
        {
            var npcType = InformationReflection.FindType("Terraria.NPC");
            int value;
            return InformationReflection.TryReadStaticInt(npcType, fieldName, out value) && value != 0 ? value : fallback;
        }

        private static bool TryReadNumber(object instance, string name, out double value)
        {
            value = 0d;
            var raw = InformationReflection.GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FormatContribution(InformationLuckContribution contribution)
        {
            if (contribution == null || string.IsNullOrWhiteSpace(contribution.Label))
            {
                return string.Empty;
            }

            var text = contribution.Label + " " + FormatSigned(contribution.Amount);
            if (!string.IsNullOrWhiteSpace(contribution.Detail))
            {
                text += "（" + contribution.Detail + "）";
            }

            return text;
        }

        private static string FormatSigned(double value)
        {
            if (Math.Abs(value) < ContributionEpsilon)
            {
                return "0";
            }

            return (value > 0d ? "+" : string.Empty) + FormatNumber(value);
        }

        private static string FormatNumber(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatTicks(int ticks)
        {
            var seconds = Math.Max(0, ticks) / 60;
            var minutes = seconds / 60;
            var remainderSeconds = seconds % 60;
            return minutes.ToString(CultureInfo.InvariantCulture) + "m" +
                   remainderSeconds.ToString("00", CultureInfo.InvariantCulture) + "s";
        }
    }

    internal sealed class InformationLuckContribution
    {
        public string Label { get; set; }
        public double Amount { get; set; }
        public string Detail { get; set; }

        public InformationLuckContribution()
        {
            Label = string.Empty;
            Detail = string.Empty;
        }
    }
}
