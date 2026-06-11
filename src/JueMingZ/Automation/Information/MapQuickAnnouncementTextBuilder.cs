using System;
using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementTextBuilder
    {
        private static readonly string[] AirPhrases =
        {
            "这里毛都没有",
            "这里只有空气",
            "这里空空如也",
            "这里什么都没有",
            "这里啥也没有",
            "这里只有寂寞",
            "这里空得很彻底",
            "这里一片安静",
            "这里没有目标",
            "这里看起来很干净",
            "这里什么也没发现",
            "这里只有风经过",
            "这里空无一物",
            "这里没有东西",
            "这里连影子都没有",
            "这里值得路过",
            "这里确认是空气",
            "？！滚木！？",
            "一位慈祥的老奶奶"
        };

        public static int AirPhraseCount
        {
            get { return AirPhrases.Length; }
        }

        public static string BuildItemText(int stack, string itemName)
        {
            return "这里有 " +
                   Math.Max(1, stack).ToString(CultureInfo.InvariantCulture) +
                   " 个 " +
                   NormalizeName(itemName, "物品");
        }

        public static string BuildActorText(IList<MapQuickAnnouncementActorTarget> actors)
        {
            if (actors == null || actors.Count <= 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(actors.Count);
            for (var index = 0; index < actors.Count; index++)
            {
                var part = BuildActorPart(actors[index]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            return parts.Count == 0 ? string.Empty : "这里有 " + string.Join("，", parts.ToArray());
        }

        public static string BuildTileText(MapQuickAnnouncementTileTarget tile)
        {
            if (tile == null || !tile.HasAnyLayer)
            {
                return string.Empty;
            }

            var parts = new List<string>(6);
            if (tile.Active)
            {
                parts.Add(NormalizeName(tile.TileName, "物块"));
            }

            var wires = new List<string>(4);
            if (tile.RedWire)
            {
                wires.Add("红线");
            }

            if (tile.BlueWire)
            {
                wires.Add("蓝线");
            }

            if (tile.GreenWire)
            {
                wires.Add("绿线");
            }

            if (tile.YellowWire)
            {
                wires.Add("黄线");
            }

            if (wires.Count > 0)
            {
                parts.Add(string.Join("、", wires.ToArray()));
            }

            if (tile.Actuator)
            {
                parts.Add("执行器");
            }

            return parts.Count == 0 ? string.Empty : "这里有 " + string.Join("，", parts.ToArray());
        }

        public static string BuildWallText(MapQuickAnnouncementWallTarget wall)
        {
            if (wall == null || !wall.Active)
            {
                return string.Empty;
            }

            return "这里有 " + NormalizeName(wall.WallName, "背景墙");
        }

        public static string BuildAirText(int index)
        {
            if (AirPhrases.Length == 0)
            {
                return "这里什么都没有";
            }

            if (index < 0)
            {
                index = 0;
            }

            return AirPhrases[index % AirPhrases.Length];
        }

        internal static string BuildActorPart(MapQuickAnnouncementActorTarget actor)
        {
            if (actor == null)
            {
                return string.Empty;
            }

            if (actor.IsPlayer)
            {
                var text = NormalizeName(actor.Name, "玩家") + " " + BuildHealth(actor.Life, actor.LifeMax);
                if (actor.IsLocalPlayer && actor.ManaMax > 0)
                {
                    text += " " + BuildHealth(actor.Mana, actor.ManaMax);
                }

                return text.Trim();
            }

            var typeName = NormalizeName(actor.TypeName, actor.Type > 0 ? actor.Type.ToString(CultureInfo.InvariantCulture) : "生物");
            var name = NormalizeName(actor.Name, string.Empty);
            var label = typeName;
            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, typeName, StringComparison.Ordinal))
            {
                label = typeName + "/" + name;
            }

            return (label + " " + BuildHealth(actor.Life, actor.LifeMax)).Trim();
        }

        internal static string NormalizeName(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return value.Length == 0 ? (fallback ?? string.Empty) : value;
        }

        private static string BuildHealth(int current, int max)
        {
            if (max <= 0)
            {
                return Math.Max(0, current).ToString(CultureInfo.InvariantCulture) + "/0";
            }

            return Math.Max(0, current).ToString(CultureInfo.InvariantCulture) +
                   "/" +
                   Math.Max(0, max).ToString(CultureInfo.InvariantCulture);
        }
    }
}
