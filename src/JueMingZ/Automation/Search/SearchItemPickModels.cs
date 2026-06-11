using System;
using System.Collections.Generic;

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
        }

        public int UiItemType { get; set; }
        public int UiItemStack { get; set; }
        public string UiItemSource { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public int MouseTileX { get; set; }
        public int MouseTileY { get; set; }
        public ulong GameUpdateCount { get; set; }
        public IList<SearchItemPickWorldItemTarget> WorldItems { get; private set; }
        public SearchItemPickTileTarget Tile { get; set; }
        public SearchItemPickWallTarget Wall { get; set; }
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
