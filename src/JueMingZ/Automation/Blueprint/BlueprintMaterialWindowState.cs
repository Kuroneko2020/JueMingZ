using System;
using System.Collections.Generic;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintMaterialWindowFrame
    {
        public BlueprintMaterialWindowFrame()
        {
            Items = new List<BlueprintMaterialItemSnapshot>();
            SummaryLine = string.Empty;
            MessageLine = string.Empty;
        }

        public bool Visible { get; set; }
        public bool Dragging { get; set; }
        public int OpacityPercent { get; set; }
        public LegacyUiRect WindowRect { get; set; }
        public LegacyUiRect HeaderRect { get; set; }
        public LegacyUiRect BodyRect { get; set; }
        public LegacyUiRect CloseRect { get; set; }
        public LegacyUiRect OpacityDownRect { get; set; }
        public LegacyUiRect OpacityUpRect { get; set; }
        public LegacyUiRect ScrollUpRect { get; set; }
        public LegacyUiRect ScrollDownRect { get; set; }
        public IReadOnlyList<BlueprintMaterialItemSnapshot> Items { get; set; }
        public int FirstVisibleItemIndex { get; set; }
        public int VisibleItemCount { get; set; }
        public string SummaryLine { get; set; }
        public string MessageLine { get; set; }

        public bool Contains(int x, int y)
        {
            return Visible && WindowRect.Contains(x, y);
        }
    }

    internal sealed class BlueprintMaterialWindowInteraction
    {
        public BlueprintMaterialWindowInteraction()
        {
            ResultCode = string.Empty;
        }

        public bool CapturedMouse { get; set; }
        public bool ScrollConsumed { get; set; }
        public bool DragStarted { get; set; }
        public bool Dragging { get; set; }
        public bool DragEnded { get; set; }
        public bool Closed { get; set; }
        public bool OpacityChanged { get; set; }
        public bool ScrollChanged { get; set; }
        public string ResultCode { get; set; }
    }

    internal static class BlueprintMaterialWindowState
    {
        private const int DefaultX = 68;
        private const int DefaultY = 118;
        private const int Width = 328;
        private const int Height = 246;
        private const int HeaderHeight = 30;
        private const int RowHeight = 23;
        private const int Padding = 10;
        private const int ButtonSize = 22;
        private const int MinOpacityPercent = 35;
        private const int MaxOpacityPercent = 100;
        private const int OpacityStep = 10;
        private static readonly object SyncRoot = new object();
        private static bool _visible;
        private static int _x = DefaultX;
        private static int _y = DefaultY;
        private static int _opacityPercent = 86;
        private static bool _dragging;
        private static int _dragOffsetX;
        private static int _dragOffsetY;
        private static int _scrollIndex;

        public static bool Visible
        {
            get
            {
                lock (SyncRoot)
                {
                    return _visible;
                }
            }
        }

        public static int OpacityPercent
        {
            get
            {
                lock (SyncRoot)
                {
                    return _opacityPercent;
                }
            }
        }

        public static void Show()
        {
            lock (SyncRoot)
            {
                _visible = true;
                ClampPositionLocked(1280, 720);
            }
        }

        public static void Hide()
        {
            lock (SyncRoot)
            {
                _visible = false;
                _dragging = false;
            }
        }

        public static BlueprintMaterialWindowFrame BuildFrame(BlueprintMaterialSnapshot snapshot, int screenWidth, int screenHeight, int mouseX, int mouseY)
        {
            lock (SyncRoot)
            {
                ClampPositionLocked(screenWidth, screenHeight);
                snapshot = snapshot ?? new BlueprintMaterialSnapshot();
                var items = snapshot.Items ?? new List<BlueprintMaterialItemSnapshot>();
                var bodyHeight = Height - HeaderHeight - Padding * 2 - 18;
                var visibleCount = Math.Max(1, bodyHeight / RowHeight);
                var maxScroll = Math.Max(0, items.Count - visibleCount);
                if (_scrollIndex > maxScroll)
                {
                    _scrollIndex = maxScroll;
                }

                var window = new LegacyUiRect(_x, _y, Width, Height);
                var header = new LegacyUiRect(_x, _y, Width, HeaderHeight);
                var close = new LegacyUiRect(window.Right - ButtonSize - 6, window.Y + 4, ButtonSize, ButtonSize);
                var opacityUp = new LegacyUiRect(close.X - ButtonSize - 5, close.Y, ButtonSize, ButtonSize);
                var opacityDown = new LegacyUiRect(opacityUp.X - ButtonSize - 5, close.Y, ButtonSize, ButtonSize);
                var scrollDown = new LegacyUiRect(window.Right - ButtonSize - 8, window.Bottom - ButtonSize - 8, ButtonSize, ButtonSize);
                var scrollUp = new LegacyUiRect(scrollDown.X - ButtonSize - 5, scrollDown.Y, ButtonSize, ButtonSize);
                var body = new LegacyUiRect(window.X + Padding, window.Y + HeaderHeight + Padding, window.Width - Padding * 2, Height - HeaderHeight - Padding * 2 - 18);
                return new BlueprintMaterialWindowFrame
                {
                    Visible = _visible,
                    Dragging = _dragging,
                    OpacityPercent = _opacityPercent,
                    WindowRect = window,
                    HeaderRect = header,
                    BodyRect = body,
                    CloseRect = close,
                    OpacityDownRect = opacityDown,
                    OpacityUpRect = opacityUp,
                    ScrollUpRect = scrollUp,
                    ScrollDownRect = scrollDown,
                    Items = items,
                    FirstVisibleItemIndex = _scrollIndex,
                    VisibleItemCount = visibleCount,
                    SummaryLine = BuildSummaryLine(snapshot),
                    MessageLine = snapshot.Message ?? string.Empty
                };
            }
        }

        public static BlueprintMaterialWindowInteraction HandleInput(
            BlueprintMaterialWindowFrame frame,
            int mouseX,
            int mouseY,
            bool leftDown,
            bool leftPressed,
            bool leftReleased,
            int rawScrollDelta,
            int screenWidth,
            int screenHeight)
        {
            var interaction = new BlueprintMaterialWindowInteraction();
            lock (SyncRoot)
            {
                if (frame == null || !_visible)
                {
                    interaction.ResultCode = "hidden";
                    _dragging = false;
                    return interaction;
                }

                var hitWindow = frame.WindowRect.Contains(mouseX, mouseY);
                if (_dragging)
                {
                    interaction.CapturedMouse = true;
                    if (leftDown)
                    {
                        _x = mouseX - _dragOffsetX;
                        _y = mouseY - _dragOffsetY;
                        ClampPositionLocked(screenWidth, screenHeight);
                        interaction.Dragging = true;
                        interaction.ResultCode = "dragging";
                    }
                    else
                    {
                        _dragging = false;
                        interaction.DragEnded = true;
                        interaction.ResultCode = "dragEnded";
                    }

                    return interaction;
                }

                if (!hitWindow)
                {
                    interaction.ResultCode = "outside";
                    return interaction;
                }

                interaction.CapturedMouse = true;
                if (rawScrollDelta != 0)
                {
                    ApplyScrollLocked(frame, rawScrollDelta > 0 ? -1 : 1);
                    interaction.ScrollConsumed = true;
                    interaction.ScrollChanged = true;
                    interaction.ResultCode = "wheel";
                    return interaction;
                }

                if (!leftPressed)
                {
                    interaction.ResultCode = "hover";
                    return interaction;
                }

                if (frame.CloseRect.Contains(mouseX, mouseY))
                {
                    _visible = false;
                    _dragging = false;
                    interaction.Closed = true;
                    interaction.ResultCode = "closed";
                    return interaction;
                }

                if (frame.OpacityDownRect.Contains(mouseX, mouseY))
                {
                    _opacityPercent = Math.Max(MinOpacityPercent, _opacityPercent - OpacityStep);
                    interaction.OpacityChanged = true;
                    interaction.ResultCode = "opacityDown";
                    return interaction;
                }

                if (frame.OpacityUpRect.Contains(mouseX, mouseY))
                {
                    _opacityPercent = Math.Min(MaxOpacityPercent, _opacityPercent + OpacityStep);
                    interaction.OpacityChanged = true;
                    interaction.ResultCode = "opacityUp";
                    return interaction;
                }

                if (frame.ScrollUpRect.Contains(mouseX, mouseY))
                {
                    ApplyScrollLocked(frame, -1);
                    interaction.ScrollChanged = true;
                    interaction.ResultCode = "scrollUp";
                    return interaction;
                }

                if (frame.ScrollDownRect.Contains(mouseX, mouseY))
                {
                    ApplyScrollLocked(frame, 1);
                    interaction.ScrollChanged = true;
                    interaction.ResultCode = "scrollDown";
                    return interaction;
                }

                if (frame.HeaderRect.Contains(mouseX, mouseY))
                {
                    _dragging = true;
                    _dragOffsetX = mouseX - _x;
                    _dragOffsetY = mouseY - _y;
                    interaction.DragStarted = true;
                    interaction.ResultCode = "dragStarted";
                    return interaction;
                }

                interaction.ResultCode = "captured";
                return interaction;
            }
        }

        public static bool ShouldCaptureMouse(BlueprintMaterialWindowFrame frame, int mouseX, int mouseY)
        {
            return frame != null && frame.Visible && frame.WindowRect.Contains(mouseX, mouseY);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _visible = false;
                _x = DefaultX;
                _y = DefaultY;
                _opacityPercent = 86;
                _dragging = false;
                _dragOffsetX = 0;
                _dragOffsetY = 0;
                _scrollIndex = 0;
            }
        }

        private static void ApplyScrollLocked(BlueprintMaterialWindowFrame frame, int direction)
        {
            if (frame == null)
            {
                return;
            }

            var maxScroll = Math.Max(0, (frame.Items == null ? 0 : frame.Items.Count) - frame.VisibleItemCount);
            _scrollIndex = Math.Max(0, Math.Min(maxScroll, _scrollIndex + direction));
        }

        private static void ClampPositionLocked(int screenWidth, int screenHeight)
        {
            screenWidth = Math.Max(Width + Padding * 2, screenWidth);
            screenHeight = Math.Max(Height + Padding * 2, screenHeight);
            _x = Math.Max(Padding, Math.Min(screenWidth - Width - Padding, _x));
            _y = Math.Max(Padding, Math.Min(screenHeight - Height - Padding, _y));
        }

        private static string BuildSummaryLine(BlueprintMaterialSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "材料：无投影数据";
            }

            return "材料：需求 " + snapshot.RequiredItemCount +
                   " 项 / 已有 " + snapshot.AvailableStackTotal +
                   " / 仍缺 " + snapshot.MissingStackTotal;
        }
    }
}
