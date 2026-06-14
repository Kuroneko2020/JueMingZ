using System;

namespace JueMingZ.Records
{
    internal interface IPlayerWorldExplorationMapReader
    {
        bool TryReadDimensions(out int width, out int height, out string message);
        bool TryIsRevealed(int x, int y, out bool revealed, out string message);
    }

    internal sealed class PlayerWorldExplorationMapReader : IPlayerWorldExplorationMapReader
    {
        public static readonly PlayerWorldExplorationMapReader Instance = new PlayerWorldExplorationMapReader();

        private PlayerWorldExplorationMapReader()
        {
        }

        public bool TryReadDimensions(out int width, out int height, out string message)
        {
            width = 0;
            height = 0;
            message = string.Empty;

            try
            {
                var map = Terraria.Main.Map;
                if (map == null)
                {
                    message = "mapUnavailable";
                    return false;
                }

                var mapWidth = Math.Max(0, map.MaxWidth);
                var mapHeight = Math.Max(0, map.MaxHeight);
                width = Terraria.Main.maxTilesX > 0 ? Terraria.Main.maxTilesX : mapWidth;
                height = Terraria.Main.maxTilesY > 0 ? Terraria.Main.maxTilesY : mapHeight;
                if (width <= 0 || height <= 0)
                {
                    message = "worldSizeUnavailable";
                    return false;
                }

                if (mapWidth > 0 && mapHeight > 0 && (width > mapWidth || height > mapHeight))
                {
                    message = "worldSizeExceedsMap";
                    return false;
                }

                message = "ok";
                return true;
            }
            catch (Exception error)
            {
                message = error.GetType().Name + ": " + error.Message;
                width = 0;
                height = 0;
                return false;
            }
        }

        public bool TryIsRevealed(int x, int y, out bool revealed, out string message)
        {
            revealed = false;
            message = string.Empty;

            try
            {
                var map = Terraria.Main.Map;
                if (map == null)
                {
                    message = "mapUnavailable";
                    return false;
                }

                if (x < 0 || y < 0 || x >= map.MaxWidth || y >= map.MaxHeight)
                {
                    message = "coordinateOutOfRange";
                    return false;
                }

                // WorldMap.IsRevealed is the read-only Light > 0 check frozen in stage 01.
                revealed = map.IsRevealed(x, y);
                message = "ok";
                return true;
            }
            catch (Exception error)
            {
                message = error.GetType().Name + ": " + error.Message;
                revealed = false;
                return false;
            }
        }
    }
}
