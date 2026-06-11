using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementTileStyleResolver
    {
        private static readonly object SyncRoot = new object();
        private static Dictionary<int, TileStyleFrameInfo> _frameInfoByTileType = new Dictionary<int, TileStyleFrameInfo>();

        public static int ResolveTileStyle(int tileType, int frameX, int frameY)
        {
            int style;
            TileStyleFrameInfo frameInfo;
            if (TryGetFrameInfo(tileType, out frameInfo) &&
                TryResolveTileStyleFromFrame(
                    frameX,
                    frameY,
                    frameInfo.CoordinateFullWidth,
                    frameInfo.CoordinateFullHeight,
                    frameInfo.StyleHorizontal,
                    frameInfo.StyleWrapLimit,
                    out style))
            {
                return style;
            }

            return TryResolveKnownSpecialTileStyle(tileType, frameX, frameY, out style) ? style : 0;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _frameInfoByTileType = new Dictionary<int, TileStyleFrameInfo>();
            }
        }

        internal static bool TryResolveTileStyleFromFrame(
            int frameX,
            int frameY,
            int coordinateFullWidth,
            int coordinateFullHeight,
            bool styleHorizontal,
            int styleWrapLimit,
            out int style)
        {
            style = 0;
            if (coordinateFullWidth <= 0 || coordinateFullHeight <= 0)
            {
                return false;
            }

            var styleColumn = Math.Max(0, frameX) / coordinateFullWidth;
            var styleRow = Math.Max(0, frameY) / coordinateFullHeight;
            var wrapLimit = styleWrapLimit <= 0 ? 1 : styleWrapLimit;
            style = styleHorizontal
                ? styleRow * wrapLimit + styleColumn
                : styleColumn * wrapLimit + styleRow;
            return style >= 0;
        }

        private static bool TryGetFrameInfo(int tileType, out TileStyleFrameInfo frameInfo)
        {
            frameInfo = null;
            if (tileType < 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (_frameInfoByTileType.TryGetValue(tileType, out frameInfo))
                {
                    return frameInfo != null && frameInfo.Available;
                }
            }

            var resolved = BuildFrameInfo(tileType);
            lock (SyncRoot)
            {
                _frameInfoByTileType[tileType] = resolved;
            }

            frameInfo = resolved;
            return frameInfo != null && frameInfo.Available;
        }

        private static TileStyleFrameInfo BuildFrameInfo(int tileType)
        {
            try
            {
                var tileObjectDataType = InformationReflection.FindType("Terraria.ObjectData.TileObjectData");
                var dataByType = InformationReflection.GetStaticMember(tileObjectDataType, "_data");
                var data = InformationReflection.GetIndexedValue(dataByType, tileType);
                if (data == null)
                {
                    return TileStyleFrameInfo.Unavailable;
                }

                int coordinateFullWidth;
                int coordinateFullHeight;
                bool styleHorizontal;
                int styleWrapLimit;
                if (!InformationReflection.TryReadInt(data, "CoordinateFullWidth", out coordinateFullWidth) ||
                    !InformationReflection.TryReadInt(data, "CoordinateFullHeight", out coordinateFullHeight) ||
                    !InformationReflection.TryReadBool(data, "StyleHorizontal", out styleHorizontal))
                {
                    return TileStyleFrameInfo.Unavailable;
                }

                if (!InformationReflection.TryReadInt(data, "StyleWrapLimit", out styleWrapLimit))
                {
                    styleWrapLimit = 1;
                }

                return TileStyleFrameInfo.Create(coordinateFullWidth, coordinateFullHeight, styleHorizontal, styleWrapLimit);
            }
            catch
            {
                return TileStyleFrameInfo.Unavailable;
            }
        }

        private static bool TryResolveKnownSpecialTileStyle(int tileType, int frameX, int frameY, out int style)
        {
            // These fallbacks mirror Terraria 1.4.5.6 TileObjectData contracts for the user-named samples.
            // They keep trophy/painting names useful even if TileObjectData reflection is unavailable.
            switch (tileType)
            {
                case 91:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 18, 54, true, 111, out style);
                case 240:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 54, 54, true, 36, out style);
                case 241:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 72, 54, true, 36, out style);
                case 242:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 108, 72, true, 27, out style);
                case 245:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 36, 54, true, 36, out style);
                case 246:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 54, 36, true, 36, out style);
                case 617:
                    return TryResolveTileStyleFromFrame(frameX, frameY, 54, 72, false, 2, out style);
                default:
                    style = 0;
                    return false;
            }
        }

        private sealed class TileStyleFrameInfo
        {
            public static readonly TileStyleFrameInfo Unavailable = new TileStyleFrameInfo(false, 0, 0, true, 1);

            private TileStyleFrameInfo(
                bool available,
                int coordinateFullWidth,
                int coordinateFullHeight,
                bool styleHorizontal,
                int styleWrapLimit)
            {
                Available = available;
                CoordinateFullWidth = coordinateFullWidth;
                CoordinateFullHeight = coordinateFullHeight;
                StyleHorizontal = styleHorizontal;
                StyleWrapLimit = styleWrapLimit;
            }

            public bool Available { get; private set; }
            public int CoordinateFullWidth { get; private set; }
            public int CoordinateFullHeight { get; private set; }
            public bool StyleHorizontal { get; private set; }
            public int StyleWrapLimit { get; private set; }

            public static TileStyleFrameInfo Create(
                int coordinateFullWidth,
                int coordinateFullHeight,
                bool styleHorizontal,
                int styleWrapLimit)
            {
                return coordinateFullWidth <= 0 || coordinateFullHeight <= 0
                    ? Unavailable
                    : new TileStyleFrameInfo(true, coordinateFullWidth, coordinateFullHeight, styleHorizontal, styleWrapLimit);
            }
        }
    }
}
