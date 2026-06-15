using System.Collections.Generic;

namespace JueMingZ.Records
{
    internal static class PlayerWorldMapMarkerStyles
    {
        private static readonly PlayerWorldMapMarkerStyle[] Styles =
        {
            new PlayerWorldMapMarkerStyle(8, "火把"),
            new PlayerWorldMapMarkerStyle(48, "宝箱"),
            new PlayerWorldMapMarkerStyle(50, "魔镜"),
            new PlayerWorldMapMarkerStyle(75, "坠星"),
            new PlayerWorldMapMarkerStyle(171, "标牌"),
            new PlayerWorldMapMarkerStyle(393, "指南针"),
            new PlayerWorldMapMarkerStyle(966, "篝火"),
            new PlayerWorldMapMarkerStyle(29, "生命水晶")
        };

        public static IReadOnlyList<PlayerWorldMapMarkerStyle> All
        {
            get { return Styles; }
        }

        public static string GetDisplayName(int itemId)
        {
            itemId = PlayerWorldMapMarkerConstants.NormalizeIconItemId(itemId);
            for (var index = 0; index < Styles.Length; index++)
            {
                if (Styles[index].IconItemId == itemId)
                {
                    return Styles[index].DisplayName;
                }
            }

            return Styles[0].DisplayName;
        }
    }

    internal sealed class PlayerWorldMapMarkerStyle
    {
        public PlayerWorldMapMarkerStyle(int iconItemId, string displayName)
        {
            IconItemId = iconItemId;
            DisplayName = displayName ?? string.Empty;
        }

        public int IconItemId { get; private set; }
        public string DisplayName { get; private set; }
    }
}
