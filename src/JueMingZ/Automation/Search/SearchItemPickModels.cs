using System;
using System.Collections.Generic;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Search
{
    internal enum SearchItemPickSelectionState
    {
        Idle = 0,
        WaitingButtonRelease = 1,
        ArmedForNextLeftClick = 2,
        Resolved = 3,
        CancelledOrFailed = 4
    }

    internal sealed class SearchItemPickResolveContext
    {
        public SearchItemPickResolveContext()
        {
            WorldItems = new List<SearchItemPickWorldItemTarget>();
            UiItemSource = string.Empty;
            UiMouseSource = string.Empty;
            WorldMouseSource = string.Empty;
            CoordinateSourceSummary = string.Empty;
        }

        public int UiItemType { get; set; }
        public int UiItemStack { get; set; }
        public string UiItemSource { get; set; }
        public int RawMouseX { get; set; }
        public int RawMouseY { get; set; }
        public int UiMouseX { get; set; }
        public int UiMouseY { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public int MouseTileX { get; set; }
        public int MouseTileY { get; set; }
        public string UiMouseSource { get; set; }
        public string WorldMouseSource { get; set; }
        public string CoordinateSourceSummary { get; set; }
        public ulong GameUpdateCount { get; set; }
        public IList<SearchItemPickWorldItemTarget> WorldItems { get; private set; }
        public SearchItemPickTileTarget Tile { get; set; }
        public SearchItemPickWallTarget Wall { get; set; }
    }

    internal sealed class SearchItemPickClickContext
    {
        private SearchItemPickClickContext()
        {
            FailureReason = string.Empty;
            UiMouseSource = string.Empty;
            WorldMouseSource = string.Empty;
            CoordinateSourceSummary = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string FailureReason { get; private set; }
        public int RawMouseX { get; private set; }
        public int RawMouseY { get; private set; }
        public int UiMouseX { get; private set; }
        public int UiMouseY { get; private set; }
        public float MouseWorldX { get; private set; }
        public float MouseWorldY { get; private set; }
        public int MouseScreenX { get; private set; }
        public int MouseScreenY { get; private set; }
        public int MouseTileX { get; private set; }
        public int MouseTileY { get; private set; }
        public string UiMouseSource { get; private set; }
        public string WorldMouseSource { get; private set; }
        public string CoordinateSourceSummary { get; private set; }
        public ulong GameUpdateCount { get; private set; }

        public static SearchItemPickClickContext Success(
            int mouseScreenX,
            int mouseScreenY,
            float mouseWorldX,
            float mouseWorldY,
            int mouseTileX,
            int mouseTileY,
            ulong gameUpdateCount)
        {
            return Success(
                mouseScreenX,
                mouseScreenY,
                mouseScreenX,
                mouseScreenY,
                mouseWorldX,
                mouseWorldY,
                mouseTileX,
                mouseTileY,
                gameUpdateCount,
                "raw=legacyMouse;ui=legacyMouse;world=provided");
        }

        public static SearchItemPickClickContext Success(
            int rawMouseX,
            int rawMouseY,
            int uiMouseX,
            int uiMouseY,
            float mouseWorldX,
            float mouseWorldY,
            int mouseTileX,
            int mouseTileY,
            ulong gameUpdateCount,
            string coordinateSourceSummary)
        {
            var tileX = WorldToTile(mouseWorldX);
            var tileY = WorldToTile(mouseWorldY);
            return new SearchItemPickClickContext
            {
                Succeeded = true,
                RawMouseX = rawMouseX,
                RawMouseY = rawMouseY,
                UiMouseX = uiMouseX,
                UiMouseY = uiMouseY,
                MouseScreenX = rawMouseX,
                MouseScreenY = rawMouseY,
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY,
                MouseTileX = tileX,
                MouseTileY = tileY,
                GameUpdateCount = gameUpdateCount,
                UiMouseSource = string.Empty,
                WorldMouseSource = string.Empty,
                CoordinateSourceSummary = coordinateSourceSummary ?? string.Empty
            };
        }

        public static SearchItemPickClickContext Success(SearchItemPickMouseCoordinateSnapshot snapshot)
        {
            snapshot = snapshot ?? new SearchItemPickMouseCoordinateSnapshot();
            var tileX = WorldToTile(snapshot.WorldMouseX);
            var tileY = WorldToTile(snapshot.WorldMouseY);
            return new SearchItemPickClickContext
            {
                Succeeded = true,
                RawMouseX = snapshot.RawMouseX,
                RawMouseY = snapshot.RawMouseY,
                UiMouseX = snapshot.UiMouseX,
                UiMouseY = snapshot.UiMouseY,
                MouseScreenX = snapshot.RawMouseX,
                MouseScreenY = snapshot.RawMouseY,
                MouseWorldX = snapshot.WorldMouseX,
                MouseWorldY = snapshot.WorldMouseY,
                MouseTileX = tileX,
                MouseTileY = tileY,
                GameUpdateCount = snapshot.GameUpdateCount,
                UiMouseSource = snapshot.UiMouseSource ?? string.Empty,
                WorldMouseSource = snapshot.WorldMouseSource ?? string.Empty,
                CoordinateSourceSummary = snapshot.SourceSummary ?? string.Empty
            };
        }

        public static SearchItemPickClickContext Failed(
            string reason,
            int mouseScreenX,
            int mouseScreenY,
            ulong gameUpdateCount)
        {
            return new SearchItemPickClickContext
            {
                Succeeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "clickContextUnavailable" : reason,
                RawMouseX = mouseScreenX,
                RawMouseY = mouseScreenY,
                UiMouseX = mouseScreenX,
                UiMouseY = mouseScreenY,
                MouseScreenX = mouseScreenX,
                MouseScreenY = mouseScreenY,
                GameUpdateCount = gameUpdateCount,
                CoordinateSourceSummary = "failed=" + (string.IsNullOrWhiteSpace(reason) ? "clickContextUnavailable" : reason)
            };
        }

        private static int WorldToTile(float worldCoordinate)
        {
            return (int)Math.Floor(worldCoordinate / TerrariaTileReadCompat.TileSize);
        }
    }

    internal sealed class SearchItemPickMouseCoordinateInput
    {
        public SearchItemPickMouseCoordinateInput()
        {
            TerrariaMouseX = -1;
            TerrariaMouseY = -1;
            OsClientMouseX = -1;
            OsClientMouseY = -1;
            UiScale = 1d;
            UiScaleX = 1d;
            UiScaleY = 1d;
            UiScaleSource = string.Empty;
            ReadMode = string.Empty;
            WorldMouseSource = string.Empty;
        }

        public bool TerrariaReadAvailable { get; set; }
        public int TerrariaMouseX { get; set; }
        public int TerrariaMouseY { get; set; }
        public bool OsReadAvailable { get; set; }
        public int OsClientMouseX { get; set; }
        public int OsClientMouseY { get; set; }
        public bool UiScaleAvailable { get; set; }
        public double UiScale { get; set; }
        public double UiScaleX { get; set; }
        public double UiScaleY { get; set; }
        public double UiTranslateX { get; set; }
        public double UiTranslateY { get; set; }
        public string UiScaleSource { get; set; }
        public string ReadMode { get; set; }
        public bool WorldMouseAvailable { get; set; }
        public float WorldMouseX { get; set; }
        public float WorldMouseY { get; set; }
        public string WorldMouseSource { get; set; }
        public float ScreenX { get; set; }
        public float ScreenY { get; set; }
        public ulong GameUpdateCount { get; set; }
    }

    internal sealed class SearchItemPickMouseCoordinateSnapshot
    {
        public SearchItemPickMouseCoordinateSnapshot()
        {
            RawMouseX = -1;
            RawMouseY = -1;
            UiMouseX = -1;
            UiMouseY = -1;
            UiMouseSource = string.Empty;
            WorldMouseSource = string.Empty;
            SourceSummary = string.Empty;
        }

        public int RawMouseX { get; set; }
        public int RawMouseY { get; set; }
        public int UiMouseX { get; set; }
        public int UiMouseY { get; set; }
        public float WorldMouseX { get; set; }
        public float WorldMouseY { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public string UiMouseSource { get; set; }
        public string WorldMouseSource { get; set; }
        public string SourceSummary { get; set; }
        public ulong GameUpdateCount { get; set; }
    }

    internal sealed class SearchItemPickWorldItemTarget
    {
        public int ItemType { get; set; }
        public int Stack { get; set; }
        public float HitboxX { get; set; }
        public float HitboxY { get; set; }
        public float HitboxWidth { get; set; }
        public float HitboxHeight { get; set; }

        public bool IsActive
        {
            get { return ItemType > 0 && Stack > 0; }
        }

        public bool Contains(float x, float y)
        {
            return IsActive &&
                   HitboxWidth > 0f &&
                   HitboxHeight > 0f &&
                   x >= HitboxX &&
                   y >= HitboxY &&
                   x < HitboxX + HitboxWidth &&
                   y < HitboxY + HitboxHeight;
        }
    }

    internal sealed class SearchItemPickTileTarget
    {
        public bool Active { get; set; }
        public int TileType { get; set; }
        public int TileStyle { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public int PlacementItemType { get; set; }
    }

    internal sealed class SearchItemPickWallTarget
    {
        public bool Active { get; set; }
        public int WallType { get; set; }
        public int PlacementItemType { get; set; }
    }

    internal sealed class SearchItemPickResolveResult
    {
        public SearchItemPickResolveResult()
        {
            SourceKind = string.Empty;
            SourceSummary = string.Empty;
        }

        public int ItemType { get; set; }
        public string SourceKind { get; set; }
        public string SourceSummary { get; set; }
    }

    internal sealed class SearchItemPickResolveAttempt
    {
        private SearchItemPickResolveAttempt()
        {
            FailureReason = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public SearchItemPickResolveResult Result { get; private set; }
        public string FailureReason { get; private set; }

        public static SearchItemPickResolveAttempt Success(SearchItemPickResolveResult result)
        {
            return new SearchItemPickResolveAttempt
            {
                Succeeded = result != null && result.ItemType > 0,
                Result = result,
                FailureReason = result == null || result.ItemType <= 0 ? "itemTypeUnavailable" : string.Empty
            };
        }

        public static SearchItemPickResolveAttempt Failed(string reason)
        {
            return new SearchItemPickResolveAttempt
            {
                Succeeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "noSearchableItem" : reason
            };
        }
    }

    internal sealed class SearchItemPickInputConsumeResult
    {
        private SearchItemPickInputConsumeResult()
        {
            Message = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }

        public static SearchItemPickInputConsumeResult Success(string message)
        {
            return new SearchItemPickInputConsumeResult
            {
                Succeeded = true,
                Message = message ?? string.Empty
            };
        }

        public static SearchItemPickInputConsumeResult Failed(string message)
        {
            return new SearchItemPickInputConsumeResult
            {
                Succeeded = false,
                Message = string.IsNullOrWhiteSpace(message) ? "input consume failed" : message
            };
        }
    }
}
