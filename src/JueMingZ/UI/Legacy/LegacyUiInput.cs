using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyUiInput
    {
        private static readonly object SyncRoot = new object();
        private static readonly Queue<LegacyUiCommand> PendingCommands = new Queue<LegacyUiCommand>();
        private static bool _wasLeftDown;
        private static bool _lastHoverInWindow;
        private static string _activeMode = string.Empty;
        private static string _activeSliderId = string.Empty;
        private static int _activeSliderValue;
        private static string _pendingSliderId = string.Empty;
        private static int _pendingSliderValue;
        private static int _dragOffsetX;
        private static int _dragOffsetY;
        private static int _resizeStartMouseX;
        private static int _resizeStartMouseY;
        private static int _resizeStartWidth;
        private static int _resizeStartHeight;
        private static int _resizeStartX;
        private static int _resizeStartY;
        private static int _windowStartX;
        private static int _windowStartY;
        private static int _windowStartWidth;
        private static int _windowStartHeight;
        private static int _pendingUiScrollDelta;
        private static bool _hasLastRawScrollWheel;
        private static int _lastRawScrollWheel;
        private static bool _wheelConsumedThisFrame;
        private static int _lastPlayerInputScrollDelta;
        private static int _lastMainScrollDelta;
        private static bool _lastPlayerInputCleared;
        private static bool _lastMainScrollSuppressed;
        private static bool _lastScrollHotbarHookSuppressed;
        private static string _lastClickElementId = string.Empty;
        private static DateTime _lastClickUtc = DateTime.MinValue;
        private static int _lastClickX = -1;
        private static int _lastClickY = -1;
        private const int ScrollActionEventCoalesceMs = 100;
        private static long _scrollSnapshotSkippedCount;
        private static long _scrollEventCoalescedCount;
        private static DateTime _lastScrollActionEventUtc = DateTime.MinValue;
        private static string _lastScrollActionEventSignature = string.Empty;

        public static string ActiveMode
        {
            get { lock (SyncRoot) { return _activeMode; } }
        }

        public static string ActiveSliderId
        {
            get { lock (SyncRoot) { return _activeSliderId; } }
        }

        public static int ActiveSliderValue
        {
            get { lock (SyncRoot) { return _activeSliderValue; } }
        }

        public static bool WheelConsumedThisFrame
        {
            get { lock (SyncRoot) { return _wheelConsumedThisFrame; } }
        }

        public static long ScrollSnapshotSkippedCount
        {
            get { lock (SyncRoot) { return _scrollSnapshotSkippedCount; } }
        }

        public static long ScrollEventCoalescedCount
        {
            get { lock (SyncRoot) { return _scrollEventCoalescedCount; } }
        }

        private sealed class MouseCoordinate
        {
            public int X { get; private set; }
            public int Y { get; private set; }
            public string Mode { get; private set; }

            public MouseCoordinate(int x, int y, string mode)
            {
                X = x;
                Y = y;
                Mode = mode ?? string.Empty;
            }
        }

    }
}
