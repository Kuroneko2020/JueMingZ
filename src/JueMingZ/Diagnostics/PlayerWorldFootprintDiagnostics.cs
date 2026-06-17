using System;
using JueMingZ.Records;

namespace JueMingZ.Diagnostics
{
    internal static class PlayerWorldFootprintDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldFootprintDiagnosticsSnapshot _snapshot = new PlayerWorldFootprintDiagnosticsSnapshot();

        public static void RecordRead(PlayerWorldFootprintReadResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.ReadFailed = result != null && result.ReadFailed;
                _snapshot.WriteFailed = result != null && result.WriteFailed;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.TimelineStartTicks = result == null ? 0L : result.TimelineStartTicks;
                _snapshot.TimelineEndTicks = result == null ? 0L : result.TimelineEndTicks;
                _snapshot.LastReadUtc = result == null ? (DateTime?)null : result.LastReadUtc;
            }
        }

        public static void RecordWrite(PlayerWorldFootprintWriteResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.WriteFailed = result != null && !result.Succeeded;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.LastWriteUtc = result == null ? (DateTime?)null : result.LastWriteUtc;
            }
        }

        public static void RecordRuntime(PlayerWorldFootprintRecordResult result)
        {
            lock (SyncRoot)
            {
                _snapshot.LastStatus = result == null ? string.Empty : result.Status ?? string.Empty;
                _snapshot.LastDecision = result == null ? string.Empty : result.Decision ?? string.Empty;
                _snapshot.LastMessage = result == null ? string.Empty : result.Message ?? string.Empty;
                _snapshot.LastPairId = result == null ? string.Empty : result.PairId ?? string.Empty;
                _snapshot.IdentityResolved = result != null && result.IdentityResolved;
                _snapshot.IsRecording = result != null && result.Succeeded && result.IdentityResolved;
                _snapshot.WriteFailed = result != null && result.WriteFailed;
                _snapshot.RetentionTrimmed = result != null && result.RetentionTrimmed;
                _snapshot.SegmentCount = result == null ? 0 : result.SegmentCount;
                _snapshot.PointCount = result == null ? 0 : result.PointCount;
                _snapshot.TimelineStartTicks = result == null ? 0L : result.TimelineStartTicks;
                _snapshot.TimelineEndTicks = result == null ? 0L : result.TimelineEndTicks;
                _snapshot.BreakCount = result == null ? 0 : Math.Max(0, result.SegmentCount - 1);
                _snapshot.LastPointTileX = result == null ? 0d : result.LastPointTileX;
                _snapshot.LastPointTileY = result == null ? 0d : result.LastPointTileY;
                _snapshot.LastPointDurationTicks = result == null ? 0L : result.LastPointDurationTicks;
                _snapshot.LastRecordRuntimeTick = result == null ? 0L : result.LastRecordRuntimeTick;
                _snapshot.LastRecordUtc = result == null ? (DateTime?)null : result.LastRecordUtc;
                _snapshot.LastFlushStatus = result != null && result.Flushed ? "saved" : (result != null && result.WriteFailed ? "writeFailed" : "pending");
                _snapshot.LastWriteUtc = result == null ? (DateTime?)null : result.LastWriteUtc;
            }
        }

        public static void RecordRenderCache(
            string status,
            string message,
            string pairId,
            string source,
            bool displayEnabled,
            int segmentCount,
            int pointCount,
            int lineCount,
            int dataSignature,
            bool cacheLimitHit)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsRenderCacheStatus = status ?? string.Empty;
                _snapshot.MapFootprintsRenderCacheMessage = message ?? string.Empty;
                _snapshot.MapFootprintsRenderCachePairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsRenderCacheSource = source ?? string.Empty;
                _snapshot.MapFootprintsDisplayEnabled = displayEnabled;
                _snapshot.MapFootprintsRenderCacheSegmentCount = segmentCount;
                _snapshot.MapFootprintsRenderCachePointCount = pointCount;
                _snapshot.MapFootprintsRenderCacheLineCount = lineCount;
                _snapshot.MapFootprintsRenderCacheDataSignature = dataSignature;
                _snapshot.MapFootprintsRenderCacheLimitHit = cacheLimitHit;
            }
        }

        public static void RecordMapDraw(
            string status,
            string message,
            string pairId,
            int cachedLineCount,
            int drawnLineCount,
            int culledLineCount,
            int thinnedLineCount,
            int drawLimitSkippedLineCount,
            bool drawLimitHit)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsLastDrawStatus = status ?? string.Empty;
                _snapshot.MapFootprintsLastDrawMessage = message ?? string.Empty;
                _snapshot.MapFootprintsLastDrawPairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsCachedLineCount = cachedLineCount;
                _snapshot.MapFootprintsDrawnLineCount = drawnLineCount;
                _snapshot.MapFootprintsCulledLineCount = culledLineCount;
                _snapshot.MapFootprintsThinnedLineCount = thinnedLineCount;
                _snapshot.MapFootprintsDrawLimitSkippedLineCount = drawLimitSkippedLineCount;
                _snapshot.MapFootprintsDrawLimitHit = drawLimitHit;
                _snapshot.MapFootprintsLastDrawUtc = DateTime.UtcNow;
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                {
                    ClearMapDrawDetailLocked();
                }
            }
        }

        public static void RecordMapDrawDetail(MapFootprintDrawDiagnosticsData data)
        {
            lock (SyncRoot)
            {
                ClearMapDrawDetailLocked();
                if (data == null)
                {
                    return;
                }

                _snapshot.MapFootprintsDrawRoute = data.Route ?? string.Empty;
                _snapshot.MapFootprintsDrawScreenWidth = data.ScreenWidth;
                _snapshot.MapFootprintsDrawScreenHeight = data.ScreenHeight;
                _snapshot.MapFootprintsDrawGameUpdateCount = data.GameUpdateCount;
                _snapshot.MapFootprintsDrawMapFullscreenPosX = data.MapFullscreenPosX;
                _snapshot.MapFootprintsDrawMapFullscreenPosY = data.MapFullscreenPosY;
                _snapshot.MapFootprintsDrawMapFullscreenScale = data.MapFullscreenScale;
                _snapshot.MapFootprintsDrawTransformMapPositionX = data.TransformMapPositionX;
                _snapshot.MapFootprintsDrawTransformMapPositionY = data.TransformMapPositionY;
                _snapshot.MapFootprintsDrawTransformMapOffsetX = data.TransformMapOffsetX;
                _snapshot.MapFootprintsDrawTransformMapOffsetY = data.TransformMapOffsetY;
                _snapshot.MapFootprintsDrawTransformMapScale = data.TransformMapScale;
                _snapshot.MapFootprintsDrawTransformOpacity = data.TransformOpacity;
                _snapshot.MapFootprintsDrawCommandSampleCount = data.CommandSampleCount;
                _snapshot.MapFootprintsDrawAbnormalLongLineCount = data.AbnormalLongLineCount;
                _snapshot.MapFootprintsDrawLongLineThresholdPixels = data.LongLineThresholdPixels;
                _snapshot.MapFootprintsDrawMaxLinePixels = data.MaxLinePixels;
                _snapshot.MapFootprintsDrawMaxLineSegmentIndex = data.MaxLineSegmentIndex;
                _snapshot.MapFootprintsDrawFirstSegmentIndex = data.FirstSegmentIndex;
                _snapshot.MapFootprintsDrawFirstStartTileX = data.FirstStartTileX;
                _snapshot.MapFootprintsDrawFirstStartTileY = data.FirstStartTileY;
                _snapshot.MapFootprintsDrawFirstEndTileX = data.FirstEndTileX;
                _snapshot.MapFootprintsDrawFirstEndTileY = data.FirstEndTileY;
                _snapshot.MapFootprintsDrawFirstStartScreenX = data.FirstStartScreenX;
                _snapshot.MapFootprintsDrawFirstStartScreenY = data.FirstStartScreenY;
                _snapshot.MapFootprintsDrawFirstEndScreenX = data.FirstEndScreenX;
                _snapshot.MapFootprintsDrawFirstEndScreenY = data.FirstEndScreenY;
                _snapshot.MapFootprintsDrawLastSegmentIndex = data.LastSegmentIndex;
                _snapshot.MapFootprintsDrawLastStartTileX = data.LastStartTileX;
                _snapshot.MapFootprintsDrawLastStartTileY = data.LastStartTileY;
                _snapshot.MapFootprintsDrawLastEndTileX = data.LastEndTileX;
                _snapshot.MapFootprintsDrawLastEndTileY = data.LastEndTileY;
                _snapshot.MapFootprintsDrawLastStartScreenX = data.LastStartScreenX;
                _snapshot.MapFootprintsDrawLastStartScreenY = data.LastStartScreenY;
                _snapshot.MapFootprintsDrawLastEndScreenX = data.LastEndScreenX;
                _snapshot.MapFootprintsDrawLastEndScreenY = data.LastEndScreenY;
                _snapshot.MapFootprintsDrawLongestSegmentIndex = data.LongestSegmentIndex;
                _snapshot.MapFootprintsDrawLongestStartTileX = data.LongestStartTileX;
                _snapshot.MapFootprintsDrawLongestStartTileY = data.LongestStartTileY;
                _snapshot.MapFootprintsDrawLongestEndTileX = data.LongestEndTileX;
                _snapshot.MapFootprintsDrawLongestEndTileY = data.LongestEndTileY;
                _snapshot.MapFootprintsDrawLongestStartScreenX = data.LongestStartScreenX;
                _snapshot.MapFootprintsDrawLongestStartScreenY = data.LongestStartScreenY;
                _snapshot.MapFootprintsDrawLongestEndScreenX = data.LongestEndScreenX;
                _snapshot.MapFootprintsDrawLongestEndScreenY = data.LongestEndScreenY;
            }
        }

        public static void RecordPlaybackOverlay(
            string status,
            string message,
            string pairId,
            bool paused,
            int playbackRate,
            long cursorTicks,
            long timelineStartTicks,
            long latestTicks,
            long displayTimelineEndTicks,
            bool atLatest,
            bool dragging,
            bool mouseCaptured,
            bool barHovered,
            string interaction)
        {
            lock (SyncRoot)
            {
                _snapshot.MapFootprintsPlaybackOverlayStatus = status ?? string.Empty;
                _snapshot.MapFootprintsPlaybackOverlayMessage = message ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPairId = pairId ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPaused = paused;
                _snapshot.MapFootprintsPlaybackRate = playbackRate;
                _snapshot.MapFootprintsPlaybackCursorTicks = cursorTicks;
                _snapshot.MapFootprintsPlaybackTimelineStartTicks = timelineStartTicks;
                _snapshot.MapFootprintsPlaybackLatestTicks = latestTicks;
                _snapshot.MapFootprintsPlaybackProgress = CalculatePlaybackProgress(timelineStartTicks, displayTimelineEndTicks, cursorTicks);
                _snapshot.MapFootprintsPlaybackAtLatest = atLatest;
                _snapshot.MapFootprintsPlaybackDragging = dragging;
                _snapshot.MapFootprintsPlaybackMouseCaptured = mouseCaptured;
                _snapshot.MapFootprintsPlaybackBarHovered = barHovered;
                _snapshot.MapFootprintsPlaybackLastInteraction = interaction ?? string.Empty;
                _snapshot.MapFootprintsPlaybackLastUpdateUtc = DateTime.UtcNow;
            }
        }

        public static void RecordPlaybackPrefixInput(MapFootprintPlaybackPrefixInputDiagnosticsData data)
        {
            lock (SyncRoot)
            {
                ClearPlaybackPrefixInputLocked();
                if (data == null)
                {
                    return;
                }

                _snapshot.MapFootprintsPlaybackPrefixHitTarget = data.HitTarget ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPrefixMouseReadMode = data.MouseReadMode ?? string.Empty;
                _snapshot.MapFootprintsPlaybackPrefixMouseX = data.MouseX;
                _snapshot.MapFootprintsPlaybackPrefixMouseY = data.MouseY;
                _snapshot.MapFootprintsPlaybackPrefixMouseReadAvailable = data.MouseReadAvailable;
                _snapshot.MapFootprintsPlaybackPrefixBarHovered = data.BarHovered;
                _snapshot.MapFootprintsPlaybackPrefixMouseCaptured = data.MouseCaptured;
                _snapshot.MapFootprintsPlaybackPrefixClickConsumed = data.ClickConsumed;
                _snapshot.MapFootprintsPlaybackPrefixScrollConsumed = data.ScrollConsumed;
                _snapshot.MapFootprintsPlaybackPrefixShouldSuppressLeftInput = data.ShouldSuppressLeftInput;
                _snapshot.MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput = data.ShouldSuppressNonLeftInput;
                _snapshot.MapFootprintsPlaybackPrefixShouldClearPanState = data.ShouldClearPanState;
                _snapshot.MapFootprintsPlaybackPrefixLeftInputSuppressed = data.LeftInputSuppressed;
                _snapshot.MapFootprintsPlaybackPrefixNonLeftInputSuppressed = data.NonLeftInputSuppressed;
                _snapshot.MapFootprintsPlaybackPrefixScrollSuppressed = data.ScrollSuppressed;
                _snapshot.MapFootprintsPlaybackPrefixPanStateClearAttempted = data.PanStateClearAttempted;
                _snapshot.MapFootprintsPlaybackPrefixPanStateClearSucceeded = data.PanStateClearSucceeded;
                _snapshot.MapFootprintsPlaybackPrefixLeftDown = data.LeftDown;
                _snapshot.MapFootprintsPlaybackPrefixLeftPressed = data.LeftPressed;
                _snapshot.MapFootprintsPlaybackPrefixLeftReleased = data.LeftReleased;
                _snapshot.MapFootprintsPlaybackPrefixScrollDelta = data.ScrollDelta;
                _snapshot.MapFootprintsPlaybackPrefixGameUpdateCount = data.GameUpdateCount;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftBefore = data.MainMouseLeftBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftAfter = data.MainMouseLeftAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore = data.MainMouseLeftReleaseBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter = data.MainMouseLeftReleaseAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseRightBefore = data.MainMouseRightBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseRightAfter = data.MainMouseRightAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore = data.MainMouseRightReleaseBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter = data.MainMouseRightReleaseAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore = data.MainMouseScrollWheelBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter = data.MainMouseScrollWheelAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore = data.MainOldMouseScrollWheelBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter = data.MainOldMouseScrollWheelAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseInterfaceBefore = data.MainMouseInterfaceBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainMouseInterfaceAfter = data.MainMouseInterfaceAfter;
                _snapshot.MapFootprintsPlaybackPrefixMainBlockMouseBefore = data.MainBlockMouseBefore;
                _snapshot.MapFootprintsPlaybackPrefixMainBlockMouseAfter = data.MainBlockMouseAfter;
                _snapshot.MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore = data.PlayerMouseInterfaceBefore;
                _snapshot.MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter = data.PlayerMouseInterfaceAfter;
                _snapshot.MapFootprintsPlaybackPrefixUtc = data.Utc;
            }
        }

        public static void RecordPlaybackAfterPlayerInputGuard(MapFootprintPlaybackAfterPlayerInputDiagnosticsData data)
        {
            lock (SyncRoot)
            {
                ClearPlaybackAfterPlayerInputGuardLocked();
                if (data == null)
                {
                    return;
                }

                _snapshot.MapFootprintsPlaybackAfterPlayerInputGuardActive = data.Active;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput = data.ShouldSuppressLeftInput;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput = data.ShouldSuppressNonLeftInput;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputReleaseFrame = data.ReleaseFrame;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore = data.MainMouseLeftBefore;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter = data.MainMouseLeftAfter;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore = data.MainMouseLeftReleaseBefore;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter = data.MainMouseLeftReleaseAfter;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore = data.MainMouseRightBefore;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter = data.MainMouseRightAfter;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore = data.MainMouseRightReleaseBefore;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter = data.MainMouseRightReleaseAfter;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputGameUpdateCount = data.GameUpdateCount;
                _snapshot.MapFootprintsPlaybackAfterPlayerInputUtc = data.Utc;
            }
        }

        public static void RecordPlaybackDrawInput(MapFootprintPlaybackDrawInputDiagnosticsData data)
        {
            lock (SyncRoot)
            {
                ClearPlaybackDrawInputLocked();
                if (data == null)
                {
                    return;
                }

                _snapshot.MapFootprintsPlaybackDrawHitTarget = data.HitTarget ?? string.Empty;
                _snapshot.MapFootprintsPlaybackDrawMouseReadMode = data.MouseReadMode ?? string.Empty;
                _snapshot.MapFootprintsPlaybackDrawMouseX = data.MouseX;
                _snapshot.MapFootprintsPlaybackDrawMouseY = data.MouseY;
                _snapshot.MapFootprintsPlaybackDrawMouseReadAvailable = data.MouseReadAvailable;
                _snapshot.MapFootprintsPlaybackDrawBarHovered = data.BarHovered;
                _snapshot.MapFootprintsPlaybackDrawMainMouseLeft = data.MainMouseLeft;
                _snapshot.MapFootprintsPlaybackDrawMainMouseLeftRelease = data.MainMouseLeftRelease;
                _snapshot.MapFootprintsPlaybackDrawMainMouseRight = data.MainMouseRight;
                _snapshot.MapFootprintsPlaybackDrawMainMouseRightRelease = data.MainMouseRightRelease;
                _snapshot.MapFootprintsPlaybackDrawMainMouseScrollWheel = data.MainMouseScrollWheel;
                _snapshot.MapFootprintsPlaybackDrawMainOldMouseScrollWheel = data.MainOldMouseScrollWheel;
                _snapshot.MapFootprintsPlaybackDrawMainMouseInterface = data.MainMouseInterface;
                _snapshot.MapFootprintsPlaybackDrawMainBlockMouse = data.MainBlockMouse;
                _snapshot.MapFootprintsPlaybackDrawPlayerMouseInterface = data.PlayerMouseInterface;
                _snapshot.MapFootprintsPlaybackDrawGameUpdateCount = data.GameUpdateCount;
                _snapshot.MapFootprintsPlaybackDrawUtc = data.Utc;
            }
        }

        internal static PlayerWorldFootprintDiagnosticsSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _snapshot.Clone();
            }
        }

        internal static PlayerWorldFootprintDiagnosticsSnapshot GetSnapshotForTesting()
        {
            return GetSnapshot();
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new PlayerWorldFootprintDiagnosticsSnapshot();
            }
        }

        private static double CalculatePlaybackProgress(long timelineStartTicks, long latestTicks, long cursorTicks)
        {
            var span = latestTicks - timelineStartTicks;
            if (span <= 0L)
            {
                return 1d;
            }

            var clamped = Math.Max(timelineStartTicks, Math.Min(latestTicks, cursorTicks));
            return (clamped - timelineStartTicks) / (double)span;
        }

        private static void ClearMapDrawDetailLocked()
        {
            _snapshot.MapFootprintsDrawRoute = string.Empty;
            _snapshot.MapFootprintsDrawScreenWidth = 0;
            _snapshot.MapFootprintsDrawScreenHeight = 0;
            _snapshot.MapFootprintsDrawGameUpdateCount = 0L;
            _snapshot.MapFootprintsDrawMapFullscreenPosX = 0d;
            _snapshot.MapFootprintsDrawMapFullscreenPosY = 0d;
            _snapshot.MapFootprintsDrawMapFullscreenScale = 0d;
            _snapshot.MapFootprintsDrawTransformMapPositionX = 0d;
            _snapshot.MapFootprintsDrawTransformMapPositionY = 0d;
            _snapshot.MapFootprintsDrawTransformMapOffsetX = 0d;
            _snapshot.MapFootprintsDrawTransformMapOffsetY = 0d;
            _snapshot.MapFootprintsDrawTransformMapScale = 0d;
            _snapshot.MapFootprintsDrawTransformOpacity = 0d;
            _snapshot.MapFootprintsDrawCommandSampleCount = 0;
            _snapshot.MapFootprintsDrawAbnormalLongLineCount = 0;
            _snapshot.MapFootprintsDrawLongLineThresholdPixels = 0d;
            _snapshot.MapFootprintsDrawMaxLinePixels = 0d;
            _snapshot.MapFootprintsDrawMaxLineSegmentIndex = 0;
            _snapshot.MapFootprintsDrawFirstSegmentIndex = 0;
            _snapshot.MapFootprintsDrawFirstStartTileX = 0d;
            _snapshot.MapFootprintsDrawFirstStartTileY = 0d;
            _snapshot.MapFootprintsDrawFirstEndTileX = 0d;
            _snapshot.MapFootprintsDrawFirstEndTileY = 0d;
            _snapshot.MapFootprintsDrawFirstStartScreenX = 0d;
            _snapshot.MapFootprintsDrawFirstStartScreenY = 0d;
            _snapshot.MapFootprintsDrawFirstEndScreenX = 0d;
            _snapshot.MapFootprintsDrawFirstEndScreenY = 0d;
            _snapshot.MapFootprintsDrawLastSegmentIndex = 0;
            _snapshot.MapFootprintsDrawLastStartTileX = 0d;
            _snapshot.MapFootprintsDrawLastStartTileY = 0d;
            _snapshot.MapFootprintsDrawLastEndTileX = 0d;
            _snapshot.MapFootprintsDrawLastEndTileY = 0d;
            _snapshot.MapFootprintsDrawLastStartScreenX = 0d;
            _snapshot.MapFootprintsDrawLastStartScreenY = 0d;
            _snapshot.MapFootprintsDrawLastEndScreenX = 0d;
            _snapshot.MapFootprintsDrawLastEndScreenY = 0d;
            _snapshot.MapFootprintsDrawLongestSegmentIndex = 0;
            _snapshot.MapFootprintsDrawLongestStartTileX = 0d;
            _snapshot.MapFootprintsDrawLongestStartTileY = 0d;
            _snapshot.MapFootprintsDrawLongestEndTileX = 0d;
            _snapshot.MapFootprintsDrawLongestEndTileY = 0d;
            _snapshot.MapFootprintsDrawLongestStartScreenX = 0d;
            _snapshot.MapFootprintsDrawLongestStartScreenY = 0d;
            _snapshot.MapFootprintsDrawLongestEndScreenX = 0d;
            _snapshot.MapFootprintsDrawLongestEndScreenY = 0d;
        }

        private static void ClearPlaybackPrefixInputLocked()
        {
            _snapshot.MapFootprintsPlaybackPrefixHitTarget = string.Empty;
            _snapshot.MapFootprintsPlaybackPrefixMouseReadMode = string.Empty;
            _snapshot.MapFootprintsPlaybackPrefixMouseX = -1;
            _snapshot.MapFootprintsPlaybackPrefixMouseY = -1;
            _snapshot.MapFootprintsPlaybackPrefixMouseReadAvailable = false;
            _snapshot.MapFootprintsPlaybackPrefixBarHovered = false;
            _snapshot.MapFootprintsPlaybackPrefixMouseCaptured = false;
            _snapshot.MapFootprintsPlaybackPrefixClickConsumed = false;
            _snapshot.MapFootprintsPlaybackPrefixScrollConsumed = false;
            _snapshot.MapFootprintsPlaybackPrefixShouldSuppressLeftInput = false;
            _snapshot.MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput = false;
            _snapshot.MapFootprintsPlaybackPrefixShouldClearPanState = false;
            _snapshot.MapFootprintsPlaybackPrefixLeftInputSuppressed = false;
            _snapshot.MapFootprintsPlaybackPrefixNonLeftInputSuppressed = false;
            _snapshot.MapFootprintsPlaybackPrefixScrollSuppressed = false;
            _snapshot.MapFootprintsPlaybackPrefixPanStateClearAttempted = false;
            _snapshot.MapFootprintsPlaybackPrefixPanStateClearSucceeded = false;
            _snapshot.MapFootprintsPlaybackPrefixLeftDown = false;
            _snapshot.MapFootprintsPlaybackPrefixLeftPressed = false;
            _snapshot.MapFootprintsPlaybackPrefixLeftReleased = false;
            _snapshot.MapFootprintsPlaybackPrefixScrollDelta = 0;
            _snapshot.MapFootprintsPlaybackPrefixGameUpdateCount = 0L;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseRightBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseRightAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore = 0;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter = 0;
            _snapshot.MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore = 0;
            _snapshot.MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter = 0;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseInterfaceBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainMouseInterfaceAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixMainBlockMouseBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixMainBlockMouseAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore = false;
            _snapshot.MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter = false;
            _snapshot.MapFootprintsPlaybackPrefixUtc = null;
        }

        private static void ClearPlaybackAfterPlayerInputGuardLocked()
        {
            _snapshot.MapFootprintsPlaybackAfterPlayerInputGuardActive = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputReleaseFrame = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter = false;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputGameUpdateCount = 0L;
            _snapshot.MapFootprintsPlaybackAfterPlayerInputUtc = null;
        }

        private static void ClearPlaybackDrawInputLocked()
        {
            _snapshot.MapFootprintsPlaybackDrawHitTarget = string.Empty;
            _snapshot.MapFootprintsPlaybackDrawMouseReadMode = string.Empty;
            _snapshot.MapFootprintsPlaybackDrawMouseX = -1;
            _snapshot.MapFootprintsPlaybackDrawMouseY = -1;
            _snapshot.MapFootprintsPlaybackDrawMouseReadAvailable = false;
            _snapshot.MapFootprintsPlaybackDrawBarHovered = false;
            _snapshot.MapFootprintsPlaybackDrawMainMouseLeft = false;
            _snapshot.MapFootprintsPlaybackDrawMainMouseLeftRelease = false;
            _snapshot.MapFootprintsPlaybackDrawMainMouseRight = false;
            _snapshot.MapFootprintsPlaybackDrawMainMouseRightRelease = false;
            _snapshot.MapFootprintsPlaybackDrawMainMouseScrollWheel = 0;
            _snapshot.MapFootprintsPlaybackDrawMainOldMouseScrollWheel = 0;
            _snapshot.MapFootprintsPlaybackDrawMainMouseInterface = false;
            _snapshot.MapFootprintsPlaybackDrawMainBlockMouse = false;
            _snapshot.MapFootprintsPlaybackDrawPlayerMouseInterface = false;
            _snapshot.MapFootprintsPlaybackDrawGameUpdateCount = 0L;
            _snapshot.MapFootprintsPlaybackDrawUtc = null;
        }
    }

    internal sealed class PlayerWorldFootprintDiagnosticsSnapshot
    {
        public string LastStatus { get; set; }
        public string LastDecision { get; set; }
        public string LastMessage { get; set; }
        public string LastPairId { get; set; }
        public bool IdentityResolved { get; set; }
        public bool IsRecording { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public bool RetentionTrimmed { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public int BreakCount { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public double LastPointTileX { get; set; }
        public double LastPointTileY { get; set; }
        public long LastPointDurationTicks { get; set; }
        public long LastRecordRuntimeTick { get; set; }
        public string LastFlushStatus { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? LastRecordUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }
        public bool MapFootprintsDisplayEnabled { get; set; }
        public string MapFootprintsRenderCacheStatus { get; set; }
        public string MapFootprintsRenderCacheMessage { get; set; }
        public string MapFootprintsRenderCachePairId { get; set; }
        public string MapFootprintsRenderCacheSource { get; set; }
        public int MapFootprintsRenderCacheSegmentCount { get; set; }
        public int MapFootprintsRenderCachePointCount { get; set; }
        public int MapFootprintsRenderCacheLineCount { get; set; }
        public int MapFootprintsRenderCacheDataSignature { get; set; }
        public bool MapFootprintsRenderCacheLimitHit { get; set; }
        public string MapFootprintsLastDrawStatus { get; set; }
        public string MapFootprintsLastDrawMessage { get; set; }
        public string MapFootprintsLastDrawPairId { get; set; }
        public int MapFootprintsCachedLineCount { get; set; }
        public int MapFootprintsDrawnLineCount { get; set; }
        public int MapFootprintsCulledLineCount { get; set; }
        public int MapFootprintsThinnedLineCount { get; set; }
        public int MapFootprintsDrawLimitSkippedLineCount { get; set; }
        public bool MapFootprintsDrawLimitHit { get; set; }
        public DateTime? MapFootprintsLastDrawUtc { get; set; }
        public string MapFootprintsDrawRoute { get; set; }
        public int MapFootprintsDrawScreenWidth { get; set; }
        public int MapFootprintsDrawScreenHeight { get; set; }
        public long MapFootprintsDrawGameUpdateCount { get; set; }
        public double MapFootprintsDrawMapFullscreenPosX { get; set; }
        public double MapFootprintsDrawMapFullscreenPosY { get; set; }
        public double MapFootprintsDrawMapFullscreenScale { get; set; }
        public double MapFootprintsDrawTransformMapPositionX { get; set; }
        public double MapFootprintsDrawTransformMapPositionY { get; set; }
        public double MapFootprintsDrawTransformMapOffsetX { get; set; }
        public double MapFootprintsDrawTransformMapOffsetY { get; set; }
        public double MapFootprintsDrawTransformMapScale { get; set; }
        public double MapFootprintsDrawTransformOpacity { get; set; }
        public int MapFootprintsDrawCommandSampleCount { get; set; }
        public int MapFootprintsDrawAbnormalLongLineCount { get; set; }
        public double MapFootprintsDrawLongLineThresholdPixels { get; set; }
        public double MapFootprintsDrawMaxLinePixels { get; set; }
        public int MapFootprintsDrawMaxLineSegmentIndex { get; set; }
        public int MapFootprintsDrawFirstSegmentIndex { get; set; }
        public double MapFootprintsDrawFirstStartTileX { get; set; }
        public double MapFootprintsDrawFirstStartTileY { get; set; }
        public double MapFootprintsDrawFirstEndTileX { get; set; }
        public double MapFootprintsDrawFirstEndTileY { get; set; }
        public double MapFootprintsDrawFirstStartScreenX { get; set; }
        public double MapFootprintsDrawFirstStartScreenY { get; set; }
        public double MapFootprintsDrawFirstEndScreenX { get; set; }
        public double MapFootprintsDrawFirstEndScreenY { get; set; }
        public int MapFootprintsDrawLastSegmentIndex { get; set; }
        public double MapFootprintsDrawLastStartTileX { get; set; }
        public double MapFootprintsDrawLastStartTileY { get; set; }
        public double MapFootprintsDrawLastEndTileX { get; set; }
        public double MapFootprintsDrawLastEndTileY { get; set; }
        public double MapFootprintsDrawLastStartScreenX { get; set; }
        public double MapFootprintsDrawLastStartScreenY { get; set; }
        public double MapFootprintsDrawLastEndScreenX { get; set; }
        public double MapFootprintsDrawLastEndScreenY { get; set; }
        public int MapFootprintsDrawLongestSegmentIndex { get; set; }
        public double MapFootprintsDrawLongestStartTileX { get; set; }
        public double MapFootprintsDrawLongestStartTileY { get; set; }
        public double MapFootprintsDrawLongestEndTileX { get; set; }
        public double MapFootprintsDrawLongestEndTileY { get; set; }
        public double MapFootprintsDrawLongestStartScreenX { get; set; }
        public double MapFootprintsDrawLongestStartScreenY { get; set; }
        public double MapFootprintsDrawLongestEndScreenX { get; set; }
        public double MapFootprintsDrawLongestEndScreenY { get; set; }
        public string MapFootprintsPlaybackOverlayStatus { get; set; }
        public string MapFootprintsPlaybackOverlayMessage { get; set; }
        public string MapFootprintsPlaybackPairId { get; set; }
        public bool MapFootprintsPlaybackPaused { get; set; }
        public int MapFootprintsPlaybackRate { get; set; }
        public long MapFootprintsPlaybackCursorTicks { get; set; }
        public long MapFootprintsPlaybackTimelineStartTicks { get; set; }
        public long MapFootprintsPlaybackLatestTicks { get; set; }
        public double MapFootprintsPlaybackProgress { get; set; }
        public bool MapFootprintsPlaybackAtLatest { get; set; }
        public bool MapFootprintsPlaybackDragging { get; set; }
        public bool MapFootprintsPlaybackMouseCaptured { get; set; }
        public bool MapFootprintsPlaybackBarHovered { get; set; }
        public string MapFootprintsPlaybackLastInteraction { get; set; }
        public DateTime? MapFootprintsPlaybackLastUpdateUtc { get; set; }
        public string MapFootprintsPlaybackPrefixHitTarget { get; set; }
        public string MapFootprintsPlaybackPrefixMouseReadMode { get; set; }
        public int MapFootprintsPlaybackPrefixMouseX { get; set; }
        public int MapFootprintsPlaybackPrefixMouseY { get; set; }
        public bool MapFootprintsPlaybackPrefixMouseReadAvailable { get; set; }
        public bool MapFootprintsPlaybackPrefixBarHovered { get; set; }
        public bool MapFootprintsPlaybackPrefixMouseCaptured { get; set; }
        public bool MapFootprintsPlaybackPrefixClickConsumed { get; set; }
        public bool MapFootprintsPlaybackPrefixScrollConsumed { get; set; }
        public bool MapFootprintsPlaybackPrefixShouldSuppressLeftInput { get; set; }
        public bool MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput { get; set; }
        public bool MapFootprintsPlaybackPrefixShouldClearPanState { get; set; }
        public bool MapFootprintsPlaybackPrefixLeftInputSuppressed { get; set; }
        public bool MapFootprintsPlaybackPrefixNonLeftInputSuppressed { get; set; }
        public bool MapFootprintsPlaybackPrefixScrollSuppressed { get; set; }
        public bool MapFootprintsPlaybackPrefixPanStateClearAttempted { get; set; }
        public bool MapFootprintsPlaybackPrefixPanStateClearSucceeded { get; set; }
        public bool MapFootprintsPlaybackPrefixLeftDown { get; set; }
        public bool MapFootprintsPlaybackPrefixLeftPressed { get; set; }
        public bool MapFootprintsPlaybackPrefixLeftReleased { get; set; }
        public int MapFootprintsPlaybackPrefixScrollDelta { get; set; }
        public long MapFootprintsPlaybackPrefixGameUpdateCount { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseLeftBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseLeftAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseRightBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseRightAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter { get; set; }
        public int MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore { get; set; }
        public int MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter { get; set; }
        public int MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore { get; set; }
        public int MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseInterfaceBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainMouseInterfaceAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixMainBlockMouseBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixMainBlockMouseAfter { get; set; }
        public bool MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore { get; set; }
        public bool MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter { get; set; }
        public DateTime? MapFootprintsPlaybackPrefixUtc { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputGuardActive { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputReleaseFrame { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore { get; set; }
        public bool MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter { get; set; }
        public long MapFootprintsPlaybackAfterPlayerInputGameUpdateCount { get; set; }
        public DateTime? MapFootprintsPlaybackAfterPlayerInputUtc { get; set; }
        public string MapFootprintsPlaybackDrawHitTarget { get; set; }
        public string MapFootprintsPlaybackDrawMouseReadMode { get; set; }
        public int MapFootprintsPlaybackDrawMouseX { get; set; }
        public int MapFootprintsPlaybackDrawMouseY { get; set; }
        public bool MapFootprintsPlaybackDrawMouseReadAvailable { get; set; }
        public bool MapFootprintsPlaybackDrawBarHovered { get; set; }
        public bool MapFootprintsPlaybackDrawMainMouseLeft { get; set; }
        public bool MapFootprintsPlaybackDrawMainMouseLeftRelease { get; set; }
        public bool MapFootprintsPlaybackDrawMainMouseRight { get; set; }
        public bool MapFootprintsPlaybackDrawMainMouseRightRelease { get; set; }
        public int MapFootprintsPlaybackDrawMainMouseScrollWheel { get; set; }
        public int MapFootprintsPlaybackDrawMainOldMouseScrollWheel { get; set; }
        public bool MapFootprintsPlaybackDrawMainMouseInterface { get; set; }
        public bool MapFootprintsPlaybackDrawMainBlockMouse { get; set; }
        public bool MapFootprintsPlaybackDrawPlayerMouseInterface { get; set; }
        public long MapFootprintsPlaybackDrawGameUpdateCount { get; set; }
        public DateTime? MapFootprintsPlaybackDrawUtc { get; set; }

        public PlayerWorldFootprintDiagnosticsSnapshot()
        {
            LastStatus = string.Empty;
            LastDecision = string.Empty;
            LastMessage = string.Empty;
            LastPairId = string.Empty;
            LastFlushStatus = string.Empty;
            MapFootprintsRenderCacheStatus = string.Empty;
            MapFootprintsRenderCacheMessage = string.Empty;
            MapFootprintsRenderCachePairId = string.Empty;
            MapFootprintsRenderCacheSource = string.Empty;
            MapFootprintsLastDrawStatus = string.Empty;
            MapFootprintsLastDrawMessage = string.Empty;
            MapFootprintsLastDrawPairId = string.Empty;
            MapFootprintsDrawRoute = string.Empty;
            MapFootprintsPlaybackOverlayStatus = string.Empty;
            MapFootprintsPlaybackOverlayMessage = string.Empty;
            MapFootprintsPlaybackPairId = string.Empty;
            MapFootprintsPlaybackRate = 1;
            MapFootprintsPlaybackProgress = 1d;
            MapFootprintsPlaybackAtLatest = true;
            MapFootprintsPlaybackLastInteraction = string.Empty;
            MapFootprintsPlaybackPrefixHitTarget = string.Empty;
            MapFootprintsPlaybackPrefixMouseReadMode = string.Empty;
            MapFootprintsPlaybackPrefixMouseX = -1;
            MapFootprintsPlaybackPrefixMouseY = -1;
            MapFootprintsPlaybackDrawHitTarget = string.Empty;
            MapFootprintsPlaybackDrawMouseReadMode = string.Empty;
            MapFootprintsPlaybackDrawMouseX = -1;
            MapFootprintsPlaybackDrawMouseY = -1;
        }

        public PlayerWorldFootprintDiagnosticsSnapshot Clone()
        {
            return new PlayerWorldFootprintDiagnosticsSnapshot
            {
                LastStatus = LastStatus ?? string.Empty,
                LastDecision = LastDecision ?? string.Empty,
                LastMessage = LastMessage ?? string.Empty,
                LastPairId = LastPairId ?? string.Empty,
                IdentityResolved = IdentityResolved,
                IsRecording = IsRecording,
                ReadFailed = ReadFailed,
                WriteFailed = WriteFailed,
                RetentionTrimmed = RetentionTrimmed,
                SegmentCount = SegmentCount,
                PointCount = PointCount,
                BreakCount = BreakCount,
                TimelineStartTicks = TimelineStartTicks,
                TimelineEndTicks = TimelineEndTicks,
                LastPointTileX = LastPointTileX,
                LastPointTileY = LastPointTileY,
                LastPointDurationTicks = LastPointDurationTicks,
                LastRecordRuntimeTick = LastRecordRuntimeTick,
                LastFlushStatus = LastFlushStatus ?? string.Empty,
                LastReadUtc = LastReadUtc,
                LastRecordUtc = LastRecordUtc,
                LastWriteUtc = LastWriteUtc,
                MapFootprintsDisplayEnabled = MapFootprintsDisplayEnabled,
                MapFootprintsRenderCacheStatus = MapFootprintsRenderCacheStatus ?? string.Empty,
                MapFootprintsRenderCacheMessage = MapFootprintsRenderCacheMessage ?? string.Empty,
                MapFootprintsRenderCachePairId = MapFootprintsRenderCachePairId ?? string.Empty,
                MapFootprintsRenderCacheSource = MapFootprintsRenderCacheSource ?? string.Empty,
                MapFootprintsRenderCacheSegmentCount = MapFootprintsRenderCacheSegmentCount,
                MapFootprintsRenderCachePointCount = MapFootprintsRenderCachePointCount,
                MapFootprintsRenderCacheLineCount = MapFootprintsRenderCacheLineCount,
                MapFootprintsRenderCacheDataSignature = MapFootprintsRenderCacheDataSignature,
                MapFootprintsRenderCacheLimitHit = MapFootprintsRenderCacheLimitHit,
                MapFootprintsLastDrawStatus = MapFootprintsLastDrawStatus ?? string.Empty,
                MapFootprintsLastDrawMessage = MapFootprintsLastDrawMessage ?? string.Empty,
                MapFootprintsLastDrawPairId = MapFootprintsLastDrawPairId ?? string.Empty,
                MapFootprintsCachedLineCount = MapFootprintsCachedLineCount,
                MapFootprintsDrawnLineCount = MapFootprintsDrawnLineCount,
                MapFootprintsCulledLineCount = MapFootprintsCulledLineCount,
                MapFootprintsThinnedLineCount = MapFootprintsThinnedLineCount,
                MapFootprintsDrawLimitSkippedLineCount = MapFootprintsDrawLimitSkippedLineCount,
                MapFootprintsDrawLimitHit = MapFootprintsDrawLimitHit,
                MapFootprintsLastDrawUtc = MapFootprintsLastDrawUtc,
                MapFootprintsDrawRoute = MapFootprintsDrawRoute ?? string.Empty,
                MapFootprintsDrawScreenWidth = MapFootprintsDrawScreenWidth,
                MapFootprintsDrawScreenHeight = MapFootprintsDrawScreenHeight,
                MapFootprintsDrawGameUpdateCount = MapFootprintsDrawGameUpdateCount,
                MapFootprintsDrawMapFullscreenPosX = MapFootprintsDrawMapFullscreenPosX,
                MapFootprintsDrawMapFullscreenPosY = MapFootprintsDrawMapFullscreenPosY,
                MapFootprintsDrawMapFullscreenScale = MapFootprintsDrawMapFullscreenScale,
                MapFootprintsDrawTransformMapPositionX = MapFootprintsDrawTransformMapPositionX,
                MapFootprintsDrawTransformMapPositionY = MapFootprintsDrawTransformMapPositionY,
                MapFootprintsDrawTransformMapOffsetX = MapFootprintsDrawTransformMapOffsetX,
                MapFootprintsDrawTransformMapOffsetY = MapFootprintsDrawTransformMapOffsetY,
                MapFootprintsDrawTransformMapScale = MapFootprintsDrawTransformMapScale,
                MapFootprintsDrawTransformOpacity = MapFootprintsDrawTransformOpacity,
                MapFootprintsDrawCommandSampleCount = MapFootprintsDrawCommandSampleCount,
                MapFootprintsDrawAbnormalLongLineCount = MapFootprintsDrawAbnormalLongLineCount,
                MapFootprintsDrawLongLineThresholdPixels = MapFootprintsDrawLongLineThresholdPixels,
                MapFootprintsDrawMaxLinePixels = MapFootprintsDrawMaxLinePixels,
                MapFootprintsDrawMaxLineSegmentIndex = MapFootprintsDrawMaxLineSegmentIndex,
                MapFootprintsDrawFirstSegmentIndex = MapFootprintsDrawFirstSegmentIndex,
                MapFootprintsDrawFirstStartTileX = MapFootprintsDrawFirstStartTileX,
                MapFootprintsDrawFirstStartTileY = MapFootprintsDrawFirstStartTileY,
                MapFootprintsDrawFirstEndTileX = MapFootprintsDrawFirstEndTileX,
                MapFootprintsDrawFirstEndTileY = MapFootprintsDrawFirstEndTileY,
                MapFootprintsDrawFirstStartScreenX = MapFootprintsDrawFirstStartScreenX,
                MapFootprintsDrawFirstStartScreenY = MapFootprintsDrawFirstStartScreenY,
                MapFootprintsDrawFirstEndScreenX = MapFootprintsDrawFirstEndScreenX,
                MapFootprintsDrawFirstEndScreenY = MapFootprintsDrawFirstEndScreenY,
                MapFootprintsDrawLastSegmentIndex = MapFootprintsDrawLastSegmentIndex,
                MapFootprintsDrawLastStartTileX = MapFootprintsDrawLastStartTileX,
                MapFootprintsDrawLastStartTileY = MapFootprintsDrawLastStartTileY,
                MapFootprintsDrawLastEndTileX = MapFootprintsDrawLastEndTileX,
                MapFootprintsDrawLastEndTileY = MapFootprintsDrawLastEndTileY,
                MapFootprintsDrawLastStartScreenX = MapFootprintsDrawLastStartScreenX,
                MapFootprintsDrawLastStartScreenY = MapFootprintsDrawLastStartScreenY,
                MapFootprintsDrawLastEndScreenX = MapFootprintsDrawLastEndScreenX,
                MapFootprintsDrawLastEndScreenY = MapFootprintsDrawLastEndScreenY,
                MapFootprintsDrawLongestSegmentIndex = MapFootprintsDrawLongestSegmentIndex,
                MapFootprintsDrawLongestStartTileX = MapFootprintsDrawLongestStartTileX,
                MapFootprintsDrawLongestStartTileY = MapFootprintsDrawLongestStartTileY,
                MapFootprintsDrawLongestEndTileX = MapFootprintsDrawLongestEndTileX,
                MapFootprintsDrawLongestEndTileY = MapFootprintsDrawLongestEndTileY,
                MapFootprintsDrawLongestStartScreenX = MapFootprintsDrawLongestStartScreenX,
                MapFootprintsDrawLongestStartScreenY = MapFootprintsDrawLongestStartScreenY,
                MapFootprintsDrawLongestEndScreenX = MapFootprintsDrawLongestEndScreenX,
                MapFootprintsDrawLongestEndScreenY = MapFootprintsDrawLongestEndScreenY,
                MapFootprintsPlaybackOverlayStatus = MapFootprintsPlaybackOverlayStatus ?? string.Empty,
                MapFootprintsPlaybackOverlayMessage = MapFootprintsPlaybackOverlayMessage ?? string.Empty,
                MapFootprintsPlaybackPairId = MapFootprintsPlaybackPairId ?? string.Empty,
                MapFootprintsPlaybackPaused = MapFootprintsPlaybackPaused,
                MapFootprintsPlaybackRate = MapFootprintsPlaybackRate,
                MapFootprintsPlaybackCursorTicks = MapFootprintsPlaybackCursorTicks,
                MapFootprintsPlaybackTimelineStartTicks = MapFootprintsPlaybackTimelineStartTicks,
                MapFootprintsPlaybackLatestTicks = MapFootprintsPlaybackLatestTicks,
                MapFootprintsPlaybackProgress = MapFootprintsPlaybackProgress,
                MapFootprintsPlaybackAtLatest = MapFootprintsPlaybackAtLatest,
                MapFootprintsPlaybackDragging = MapFootprintsPlaybackDragging,
                MapFootprintsPlaybackMouseCaptured = MapFootprintsPlaybackMouseCaptured,
                MapFootprintsPlaybackBarHovered = MapFootprintsPlaybackBarHovered,
                MapFootprintsPlaybackLastInteraction = MapFootprintsPlaybackLastInteraction ?? string.Empty,
                MapFootprintsPlaybackLastUpdateUtc = MapFootprintsPlaybackLastUpdateUtc,
                MapFootprintsPlaybackPrefixHitTarget = MapFootprintsPlaybackPrefixHitTarget ?? string.Empty,
                MapFootprintsPlaybackPrefixMouseReadMode = MapFootprintsPlaybackPrefixMouseReadMode ?? string.Empty,
                MapFootprintsPlaybackPrefixMouseX = MapFootprintsPlaybackPrefixMouseX,
                MapFootprintsPlaybackPrefixMouseY = MapFootprintsPlaybackPrefixMouseY,
                MapFootprintsPlaybackPrefixMouseReadAvailable = MapFootprintsPlaybackPrefixMouseReadAvailable,
                MapFootprintsPlaybackPrefixBarHovered = MapFootprintsPlaybackPrefixBarHovered,
                MapFootprintsPlaybackPrefixMouseCaptured = MapFootprintsPlaybackPrefixMouseCaptured,
                MapFootprintsPlaybackPrefixClickConsumed = MapFootprintsPlaybackPrefixClickConsumed,
                MapFootprintsPlaybackPrefixScrollConsumed = MapFootprintsPlaybackPrefixScrollConsumed,
                MapFootprintsPlaybackPrefixShouldSuppressLeftInput = MapFootprintsPlaybackPrefixShouldSuppressLeftInput,
                MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput = MapFootprintsPlaybackPrefixShouldSuppressNonLeftInput,
                MapFootprintsPlaybackPrefixShouldClearPanState = MapFootprintsPlaybackPrefixShouldClearPanState,
                MapFootprintsPlaybackPrefixLeftInputSuppressed = MapFootprintsPlaybackPrefixLeftInputSuppressed,
                MapFootprintsPlaybackPrefixNonLeftInputSuppressed = MapFootprintsPlaybackPrefixNonLeftInputSuppressed,
                MapFootprintsPlaybackPrefixScrollSuppressed = MapFootprintsPlaybackPrefixScrollSuppressed,
                MapFootprintsPlaybackPrefixPanStateClearAttempted = MapFootprintsPlaybackPrefixPanStateClearAttempted,
                MapFootprintsPlaybackPrefixPanStateClearSucceeded = MapFootprintsPlaybackPrefixPanStateClearSucceeded,
                MapFootprintsPlaybackPrefixLeftDown = MapFootprintsPlaybackPrefixLeftDown,
                MapFootprintsPlaybackPrefixLeftPressed = MapFootprintsPlaybackPrefixLeftPressed,
                MapFootprintsPlaybackPrefixLeftReleased = MapFootprintsPlaybackPrefixLeftReleased,
                MapFootprintsPlaybackPrefixScrollDelta = MapFootprintsPlaybackPrefixScrollDelta,
                MapFootprintsPlaybackPrefixGameUpdateCount = MapFootprintsPlaybackPrefixGameUpdateCount,
                MapFootprintsPlaybackPrefixMainMouseLeftBefore = MapFootprintsPlaybackPrefixMainMouseLeftBefore,
                MapFootprintsPlaybackPrefixMainMouseLeftAfter = MapFootprintsPlaybackPrefixMainMouseLeftAfter,
                MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore = MapFootprintsPlaybackPrefixMainMouseLeftReleaseBefore,
                MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter = MapFootprintsPlaybackPrefixMainMouseLeftReleaseAfter,
                MapFootprintsPlaybackPrefixMainMouseRightBefore = MapFootprintsPlaybackPrefixMainMouseRightBefore,
                MapFootprintsPlaybackPrefixMainMouseRightAfter = MapFootprintsPlaybackPrefixMainMouseRightAfter,
                MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore = MapFootprintsPlaybackPrefixMainMouseRightReleaseBefore,
                MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter = MapFootprintsPlaybackPrefixMainMouseRightReleaseAfter,
                MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore = MapFootprintsPlaybackPrefixMainMouseScrollWheelBefore,
                MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter = MapFootprintsPlaybackPrefixMainMouseScrollWheelAfter,
                MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore = MapFootprintsPlaybackPrefixMainOldMouseScrollWheelBefore,
                MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter = MapFootprintsPlaybackPrefixMainOldMouseScrollWheelAfter,
                MapFootprintsPlaybackPrefixMainMouseInterfaceBefore = MapFootprintsPlaybackPrefixMainMouseInterfaceBefore,
                MapFootprintsPlaybackPrefixMainMouseInterfaceAfter = MapFootprintsPlaybackPrefixMainMouseInterfaceAfter,
                MapFootprintsPlaybackPrefixMainBlockMouseBefore = MapFootprintsPlaybackPrefixMainBlockMouseBefore,
                MapFootprintsPlaybackPrefixMainBlockMouseAfter = MapFootprintsPlaybackPrefixMainBlockMouseAfter,
                MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore = MapFootprintsPlaybackPrefixPlayerMouseInterfaceBefore,
                MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter = MapFootprintsPlaybackPrefixPlayerMouseInterfaceAfter,
                MapFootprintsPlaybackPrefixUtc = MapFootprintsPlaybackPrefixUtc,
                MapFootprintsPlaybackAfterPlayerInputGuardActive = MapFootprintsPlaybackAfterPlayerInputGuardActive,
                MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput = MapFootprintsPlaybackAfterPlayerInputShouldSuppressLeftInput,
                MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput = MapFootprintsPlaybackAfterPlayerInputShouldSuppressNonLeftInput,
                MapFootprintsPlaybackAfterPlayerInputReleaseFrame = MapFootprintsPlaybackAfterPlayerInputReleaseFrame,
                MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore = MapFootprintsPlaybackAfterPlayerInputMainMouseLeftBefore,
                MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter = MapFootprintsPlaybackAfterPlayerInputMainMouseLeftAfter,
                MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore = MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseBefore,
                MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter = MapFootprintsPlaybackAfterPlayerInputMainMouseLeftReleaseAfter,
                MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore = MapFootprintsPlaybackAfterPlayerInputMainMouseRightBefore,
                MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter = MapFootprintsPlaybackAfterPlayerInputMainMouseRightAfter,
                MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore = MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseBefore,
                MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter = MapFootprintsPlaybackAfterPlayerInputMainMouseRightReleaseAfter,
                MapFootprintsPlaybackAfterPlayerInputGameUpdateCount = MapFootprintsPlaybackAfterPlayerInputGameUpdateCount,
                MapFootprintsPlaybackAfterPlayerInputUtc = MapFootprintsPlaybackAfterPlayerInputUtc,
                MapFootprintsPlaybackDrawHitTarget = MapFootprintsPlaybackDrawHitTarget ?? string.Empty,
                MapFootprintsPlaybackDrawMouseReadMode = MapFootprintsPlaybackDrawMouseReadMode ?? string.Empty,
                MapFootprintsPlaybackDrawMouseX = MapFootprintsPlaybackDrawMouseX,
                MapFootprintsPlaybackDrawMouseY = MapFootprintsPlaybackDrawMouseY,
                MapFootprintsPlaybackDrawMouseReadAvailable = MapFootprintsPlaybackDrawMouseReadAvailable,
                MapFootprintsPlaybackDrawBarHovered = MapFootprintsPlaybackDrawBarHovered,
                MapFootprintsPlaybackDrawMainMouseLeft = MapFootprintsPlaybackDrawMainMouseLeft,
                MapFootprintsPlaybackDrawMainMouseLeftRelease = MapFootprintsPlaybackDrawMainMouseLeftRelease,
                MapFootprintsPlaybackDrawMainMouseRight = MapFootprintsPlaybackDrawMainMouseRight,
                MapFootprintsPlaybackDrawMainMouseRightRelease = MapFootprintsPlaybackDrawMainMouseRightRelease,
                MapFootprintsPlaybackDrawMainMouseScrollWheel = MapFootprintsPlaybackDrawMainMouseScrollWheel,
                MapFootprintsPlaybackDrawMainOldMouseScrollWheel = MapFootprintsPlaybackDrawMainOldMouseScrollWheel,
                MapFootprintsPlaybackDrawMainMouseInterface = MapFootprintsPlaybackDrawMainMouseInterface,
                MapFootprintsPlaybackDrawMainBlockMouse = MapFootprintsPlaybackDrawMainBlockMouse,
                MapFootprintsPlaybackDrawPlayerMouseInterface = MapFootprintsPlaybackDrawPlayerMouseInterface,
                MapFootprintsPlaybackDrawGameUpdateCount = MapFootprintsPlaybackDrawGameUpdateCount,
                MapFootprintsPlaybackDrawUtc = MapFootprintsPlaybackDrawUtc
            };
        }
    }

    internal sealed class MapFootprintDrawDiagnosticsData
    {
        public string Route { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public long GameUpdateCount { get; set; }
        public double MapFullscreenPosX { get; set; }
        public double MapFullscreenPosY { get; set; }
        public double MapFullscreenScale { get; set; }
        public double TransformMapPositionX { get; set; }
        public double TransformMapPositionY { get; set; }
        public double TransformMapOffsetX { get; set; }
        public double TransformMapOffsetY { get; set; }
        public double TransformMapScale { get; set; }
        public double TransformOpacity { get; set; }
        public int CommandSampleCount { get; set; }
        public int AbnormalLongLineCount { get; set; }
        public double LongLineThresholdPixels { get; set; }
        public double MaxLinePixels { get; set; }
        public int MaxLineSegmentIndex { get; set; }
        public int FirstSegmentIndex { get; set; }
        public double FirstStartTileX { get; set; }
        public double FirstStartTileY { get; set; }
        public double FirstEndTileX { get; set; }
        public double FirstEndTileY { get; set; }
        public double FirstStartScreenX { get; set; }
        public double FirstStartScreenY { get; set; }
        public double FirstEndScreenX { get; set; }
        public double FirstEndScreenY { get; set; }
        public int LastSegmentIndex { get; set; }
        public double LastStartTileX { get; set; }
        public double LastStartTileY { get; set; }
        public double LastEndTileX { get; set; }
        public double LastEndTileY { get; set; }
        public double LastStartScreenX { get; set; }
        public double LastStartScreenY { get; set; }
        public double LastEndScreenX { get; set; }
        public double LastEndScreenY { get; set; }
        public int LongestSegmentIndex { get; set; }
        public double LongestStartTileX { get; set; }
        public double LongestStartTileY { get; set; }
        public double LongestEndTileX { get; set; }
        public double LongestEndTileY { get; set; }
        public double LongestStartScreenX { get; set; }
        public double LongestStartScreenY { get; set; }
        public double LongestEndScreenX { get; set; }
        public double LongestEndScreenY { get; set; }

        public MapFootprintDrawDiagnosticsData()
        {
            Route = string.Empty;
        }
    }

    internal sealed class MapFootprintPlaybackPrefixInputDiagnosticsData
    {
        public string HitTarget { get; set; }
        public string MouseReadMode { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool MouseReadAvailable { get; set; }
        public bool BarHovered { get; set; }
        public bool MouseCaptured { get; set; }
        public bool ClickConsumed { get; set; }
        public bool ScrollConsumed { get; set; }
        public bool ShouldSuppressLeftInput { get; set; }
        public bool ShouldSuppressNonLeftInput { get; set; }
        public bool ShouldClearPanState { get; set; }
        public bool LeftInputSuppressed { get; set; }
        public bool NonLeftInputSuppressed { get; set; }
        public bool ScrollSuppressed { get; set; }
        public bool PanStateClearAttempted { get; set; }
        public bool PanStateClearSucceeded { get; set; }
        public bool LeftDown { get; set; }
        public bool LeftPressed { get; set; }
        public bool LeftReleased { get; set; }
        public int ScrollDelta { get; set; }
        public long GameUpdateCount { get; set; }
        public bool MainMouseLeftBefore { get; set; }
        public bool MainMouseLeftAfter { get; set; }
        public bool MainMouseLeftReleaseBefore { get; set; }
        public bool MainMouseLeftReleaseAfter { get; set; }
        public bool MainMouseRightBefore { get; set; }
        public bool MainMouseRightAfter { get; set; }
        public bool MainMouseRightReleaseBefore { get; set; }
        public bool MainMouseRightReleaseAfter { get; set; }
        public int MainMouseScrollWheelBefore { get; set; }
        public int MainMouseScrollWheelAfter { get; set; }
        public int MainOldMouseScrollWheelBefore { get; set; }
        public int MainOldMouseScrollWheelAfter { get; set; }
        public bool MainMouseInterfaceBefore { get; set; }
        public bool MainMouseInterfaceAfter { get; set; }
        public bool MainBlockMouseBefore { get; set; }
        public bool MainBlockMouseAfter { get; set; }
        public bool PlayerMouseInterfaceBefore { get; set; }
        public bool PlayerMouseInterfaceAfter { get; set; }
        public DateTime? Utc { get; set; }

        public MapFootprintPlaybackPrefixInputDiagnosticsData()
        {
            HitTarget = string.Empty;
            MouseReadMode = string.Empty;
            MouseX = -1;
            MouseY = -1;
        }
    }

    internal sealed class MapFootprintPlaybackAfterPlayerInputDiagnosticsData
    {
        public bool Active { get; set; }
        public bool ShouldSuppressLeftInput { get; set; }
        public bool ShouldSuppressNonLeftInput { get; set; }
        public bool ReleaseFrame { get; set; }
        public bool MainMouseLeftBefore { get; set; }
        public bool MainMouseLeftAfter { get; set; }
        public bool MainMouseLeftReleaseBefore { get; set; }
        public bool MainMouseLeftReleaseAfter { get; set; }
        public bool MainMouseRightBefore { get; set; }
        public bool MainMouseRightAfter { get; set; }
        public bool MainMouseRightReleaseBefore { get; set; }
        public bool MainMouseRightReleaseAfter { get; set; }
        public long GameUpdateCount { get; set; }
        public DateTime? Utc { get; set; }
    }

    internal sealed class MapFootprintPlaybackDrawInputDiagnosticsData
    {
        public string HitTarget { get; set; }
        public string MouseReadMode { get; set; }
        public int MouseX { get; set; }
        public int MouseY { get; set; }
        public bool MouseReadAvailable { get; set; }
        public bool BarHovered { get; set; }
        public bool MainMouseLeft { get; set; }
        public bool MainMouseLeftRelease { get; set; }
        public bool MainMouseRight { get; set; }
        public bool MainMouseRightRelease { get; set; }
        public int MainMouseScrollWheel { get; set; }
        public int MainOldMouseScrollWheel { get; set; }
        public bool MainMouseInterface { get; set; }
        public bool MainBlockMouse { get; set; }
        public bool PlayerMouseInterface { get; set; }
        public long GameUpdateCount { get; set; }
        public DateTime? Utc { get; set; }

        public MapFootprintPlaybackDrawInputDiagnosticsData()
        {
            HitTarget = string.Empty;
            MouseReadMode = string.Empty;
            MouseX = -1;
            MouseY = -1;
        }
    }
}
