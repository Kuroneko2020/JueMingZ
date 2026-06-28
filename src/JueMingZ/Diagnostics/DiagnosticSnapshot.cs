using System;
using System.Collections.Generic;

namespace JueMingZ.Diagnostics
{
    // Runtime snapshot is a user-return contract; keep this DTO passive and populated from cached summaries.
    public sealed class DiagnosticSnapshot
    {
        public bool Loaded { get; set; }
        public string Version { get; set; }
        public string RuntimeVersion { get; set; }
        public string TestRunId { get; set; }
        public string ProcessName { get; set; }
        public string BaseDirectory { get; set; }
        public string LogDirectory { get; set; }
        public bool TerrariaDetected { get; set; }
        public string TerrariaVersion { get; set; }
        public string NetModeDescription { get; set; }
        public long UpdateCount { get; set; }
        public bool LateBootstrapCompleted { get; set; }
        public bool SafeBootstrapStarted { get; set; }
        public bool HarmonyLoaded { get; set; }
        public bool SafeBootstrapHookInstalled { get; set; }
        public bool HookUpdateInstalled { get; set; }
        public bool DrawHookInstalled { get; set; }
        public bool InterfaceLayerHookInstalled { get; set; }
        public bool ItemCheckHookInstalled { get; set; }
        public string ItemCheckHookMethod { get; set; }
        public bool GoblinExecutionHookInstalled { get; set; }
        public string GoblinExecutionHookMethod { get; set; }
        public bool PlayerWorldDeathHookInstalled { get; set; }
        public string PlayerWorldDeathHookMethod { get; set; }
        public string PlayerWorldDeathHookMessage { get; set; }
        public string PlayerWorldDeathLastRecordStatus { get; set; }
        public string PlayerWorldDeathLastRecordMessage { get; set; }
        public string PlayerWorldDeathLastEventId { get; set; }
        public string PlayerWorldDeathLastPairId { get; set; }
        public int PlayerWorldDeathLastDeathCount { get; set; }
        public bool PlayerWorldDeathHistoryReadFailed { get; set; }
        public bool PlayerWorldDeathMarkerLayerInstalled { get; set; }
        public string PlayerWorldDeathMarkerLayerMessage { get; set; }
        public string PlayerWorldDeathMarkerLastStatus { get; set; }
        public string PlayerWorldDeathMarkerLastMessage { get; set; }
        public string PlayerWorldDeathMarkerLastPairId { get; set; }
        public int PlayerWorldDeathMarkerCachedCount { get; set; }
        public int PlayerWorldDeathMarkerDrawnCount { get; set; }
        public bool PlayerWorldDeathMarkerCulledByLimit { get; set; }
        public bool PlayerWorldDeathMarkerHistoryReadFailed { get; set; }
        public DateTime? PlayerWorldDeathMarkerLastDrawUtc { get; set; }
        public string PlayerWorldDeathHistoryLastStatus { get; set; }
        public string PlayerWorldDeathHistoryLastMessage { get; set; }
        public string PlayerWorldDeathHistoryLastPairId { get; set; }
        public int PlayerWorldDeathHistoryDeathCount { get; set; }
        public int PlayerWorldDeathHistoryTotalEventCount { get; set; }
        public int PlayerWorldDeathHistoryPageIndex { get; set; }
        public int PlayerWorldDeathHistoryPageCount { get; set; }
        public bool PlayerWorldDeathHistorySummaryReadFailed { get; set; }
        public bool PlayerWorldDeathHistoryPageReadFailed { get; set; }
        public DateTime? PlayerWorldDeathHistoryLastReadUtc { get; set; }
        public string PlayerWorldPlaytimeLastStatus { get; set; }
        public string PlayerWorldPlaytimeLastMessage { get; set; }
        public string PlayerWorldPlaytimeLastPairId { get; set; }
        public double PlayerWorldPlaytimeTotalGameTicks { get; set; }
        public int PlayerWorldPlaytimeWholeDayCount { get; set; }
        public bool PlayerWorldPlaytimeReadFailed { get; set; }
        public bool PlayerWorldPlaytimeWriteFailed { get; set; }
        public double PlayerWorldPlaytimeLastDeltaGameTicks { get; set; }
        public string PlayerWorldPlaytimeLastSkippedDeltaReason { get; set; }
        public DateTime? PlayerWorldPlaytimeLastSampleUtc { get; set; }
        public DateTime? PlayerWorldPlaytimeLastWriteUtc { get; set; }
        public string PlayerWorldExplorationLastStatus { get; set; }
        public string PlayerWorldExplorationLastMessage { get; set; }
        public string PlayerWorldExplorationLastPairId { get; set; }
        public int PlayerWorldExplorationWorldWidth { get; set; }
        public int PlayerWorldExplorationWorldHeight { get; set; }
        public long PlayerWorldExplorationTotalTileCount { get; set; }
        public long PlayerWorldExplorationRevealedTileCount { get; set; }
        public long PlayerWorldExplorationWorkingRevealedTileCount { get; set; }
        public long PlayerWorldExplorationScannedTileCount { get; set; }
        public long PlayerWorldExplorationNextTileIndex { get; set; }
        public int PlayerWorldExplorationLastScannedTileBudget { get; set; }
        public string PlayerWorldExplorationScanMode { get; set; }
        public string PlayerWorldExplorationControlState { get; set; }
        public bool PlayerWorldExplorationPausedByUser { get; set; }
        public bool PlayerWorldExplorationIdleComplete { get; set; }
        public double PlayerWorldExplorationLastScanElapsedMs { get; set; }
        public int PlayerWorldExplorationLastScanTileCount { get; set; }
        public double PlayerWorldExplorationCurrentTimeBudgetMs { get; set; }
        public long PlayerWorldExplorationCurrentCadenceTicks { get; set; }
        public bool PlayerWorldExplorationBackoffApplied { get; set; }
        public string PlayerWorldExplorationLastUserCommand { get; set; }
        public bool PlayerWorldExplorationAutoRescanDisabled { get; set; }
        public double PlayerWorldExplorationRevealedPercent { get; set; }
        public bool PlayerWorldExplorationScanComplete { get; set; }
        public bool PlayerWorldExplorationReadFailed { get; set; }
        public bool PlayerWorldExplorationWriteFailed { get; set; }
        public DateTime? PlayerWorldExplorationLastScanUtc { get; set; }
        public DateTime? PlayerWorldExplorationLastCompletedScanUtc { get; set; }
        public DateTime? PlayerWorldExplorationLastWriteUtc { get; set; }
        public bool PlayerWorldMapMarkersEnabled { get; set; }
        public string PlayerWorldMapMarkersLastStatus { get; set; }
        public string PlayerWorldMapMarkersLastMessage { get; set; }
        public string PlayerWorldMapMarkersLastPairId { get; set; }
        public int PlayerWorldMapMarkersCount { get; set; }
        public bool PlayerWorldMapMarkersReadFailed { get; set; }
        public bool PlayerWorldMapMarkersWriteFailed { get; set; }
        public bool PlayerWorldMapMarkersLimitExceeded { get; set; }
        public bool PlayerWorldMapMarkersCulledByCacheLimit { get; set; }
        public string PlayerWorldMapMarkersLastOperation { get; set; }
        public string PlayerWorldMapMarkersLastUiAction { get; set; }
        public string PlayerWorldMapMarkersLastJumpResult { get; set; }
        public int MapMarkerLastJumpRequestedTileX { get; set; }
        public int MapMarkerLastJumpRequestedTileY { get; set; }
        public double MapMarkerLastJumpWrittenMapPosX { get; set; }
        public double MapMarkerLastJumpWrittenMapPosY { get; set; }
        public double MapMarkerLastJumpScale { get; set; }
        public bool MapMarkerLastJumpReleasedUiCapture { get; set; }
        public bool MapMarkerLastJumpClearedPanState { get; set; }
        public bool MapMarkerLastJumpConsumedButtonPulse { get; set; }
        public bool MapMarkerLastJumpVanillaMapInputHandoff { get; set; }
        public string MapMarkerLastBlockedReason { get; set; }
        public string MapMarkerLastTransformRoute { get; set; }
        public int MapMarkerLastTransformScreenWidth { get; set; }
        public int MapMarkerLastTransformScreenHeight { get; set; }
        public double MapMarkerLastTransformMapTopLeftX { get; set; }
        public double MapMarkerLastTransformMapTopLeftY { get; set; }
        public double MapMarkerLastTransformScale { get; set; }
        public double MapMarkerLastTransformMapFullscreenPosX { get; set; }
        public double MapMarkerLastTransformMapFullscreenPosY { get; set; }
        public long MapMarkerLastTransformGameUpdateCount { get; set; }
        public DateTime? MapMarkerLastTransformUtc { get; set; }
        public int MapMarkerLastRightClickMouseX { get; set; }
        public int MapMarkerLastRightClickMouseY { get; set; }
        public int MapMarkerLastRightClickTileX { get; set; }
        public int MapMarkerLastRightClickTileY { get; set; }
        public string MapMarkerLastRightClickTransformSource { get; set; }
        public string MapMarkerLastRightClickFallbackReason { get; set; }
        public double MapMarkerLastRightClickMapFullscreenPosX { get; set; }
        public double MapMarkerLastRightClickMapFullscreenPosY { get; set; }
        public double MapMarkerLastRightClickMapScale { get; set; }
        public long MapMarkerLastRightClickTransformAgeUpdates { get; set; }
        public int PlayerWorldMapMarkersUiOnlyActionCount { get; set; }
        public bool MapMarkerPickerOpen { get; set; }
        public int MapMarkerPickerAnchorScreenX { get; set; }
        public int MapMarkerPickerAnchorScreenY { get; set; }
        public int MapMarkerPickerPanelX { get; set; }
        public int MapMarkerPickerPanelY { get; set; }
        public bool MapMarkerPickerPanelClamped { get; set; }
        public DateTime? MapMarkerPickerLastDraw { get; set; }
        public DateTime? MapMarkerPickerLastFullscreenDraw { get; set; }
        public string MapMarkerPickerDrawRoute { get; set; }
        public string MapMarkerPickerDrawSkippedReason { get; set; }
        public DateTime? MapMarkerPickerLastClick { get; set; }
        public string MapMarkerPickerLastCloseReason { get; set; }
        public DateTime? PlayerWorldMapMarkersLastReadUtc { get; set; }
        public DateTime? PlayerWorldMapMarkersLastWriteUtc { get; set; }
        public string MapMarkerTraceEventsPath { get; set; }
        public DateTime? MapMarkerLastTraceEventWrittenAtUtc { get; set; }
        public string MapMarkerLastTraceEventType { get; set; }
        public string MapMarkerLastTraceMarkerId { get; set; }
        public long MapDirectionHintTargetScanCadenceTicks { get; set; }
        public bool MapRareCreatureDirectionEnabled { get; set; }
        public string MapRareCreatureDirectionStatus { get; set; }
        public string MapRareCreatureDirectionMessage { get; set; }
        public string MapRareCreatureDirectionGateReason { get; set; }
        public bool MapRareCreatureDirectionHasLifeformAnalyzer { get; set; }
        public bool MapRareCreatureDirectionInfoAccessoryHidden { get; set; }
        public bool MapRareCreatureDirectionTargetActive { get; set; }
        public int MapRareCreatureDirectionTargetWhoAmI { get; set; }
        public int MapRareCreatureDirectionTargetType { get; set; }
        public string MapRareCreatureDirectionTargetName { get; set; }
        public int MapRareCreatureDirectionTargetRarity { get; set; }
        public double MapRareCreatureDirectionTargetWorldX { get; set; }
        public double MapRareCreatureDirectionTargetWorldY { get; set; }
        public bool MapRareCreatureDirectionOnScreen { get; set; }
        public bool MapRareCreatureDirectionShouldDrawLabel { get; set; }
        public double MapRareCreatureDirectionDistancePixels { get; set; }
        public string MapRareCreatureDirectionDistanceText { get; set; }
        public double MapRareCreatureDirectionArrowScreenX { get; set; }
        public double MapRareCreatureDirectionArrowScreenY { get; set; }
        public double MapRareCreatureDirectionDirectionX { get; set; }
        public double MapRareCreatureDirectionDirectionY { get; set; }
        public string MapRareCreatureDirectionArrowGlyph { get; set; }
        public string MapRareCreatureDirectionLabelLine1 { get; set; }
        public string MapRareCreatureDirectionLabelLine2 { get; set; }
        public long MapRareCreatureDirectionLastScanTick { get; set; }
        public long MapRareCreatureDirectionLastScanAgeTicks { get; set; }
        public string MapRareCreatureDirectionDrawStatus { get; set; }
        public DateTime? MapRareCreatureDirectionLastTargetUtc { get; set; }
        public DateTime? MapRareCreatureDirectionLastDrawUtc { get; set; }
        public bool MapTravellingMerchantDirectionEnabled { get; set; }
        public string MapTravellingMerchantDirectionStatus { get; set; }
        public string MapTravellingMerchantDirectionMessage { get; set; }
        public bool MapTravellingMerchantDirectionTargetActive { get; set; }
        public int MapTravellingMerchantDirectionTargetWhoAmI { get; set; }
        public int MapTravellingMerchantDirectionTargetType { get; set; }
        public string MapTravellingMerchantDirectionTargetName { get; set; }
        public double MapTravellingMerchantDirectionTargetWorldX { get; set; }
        public double MapTravellingMerchantDirectionTargetWorldY { get; set; }
        public bool MapTravellingMerchantDirectionOnScreen { get; set; }
        public double MapTravellingMerchantDirectionDistancePixels { get; set; }
        public string MapTravellingMerchantDirectionDistanceText { get; set; }
        public double MapTravellingMerchantDirectionEdgeScreenX { get; set; }
        public double MapTravellingMerchantDirectionEdgeScreenY { get; set; }
        public double MapTravellingMerchantDirectionDirectionX { get; set; }
        public double MapTravellingMerchantDirectionDirectionY { get; set; }
        public string MapTravellingMerchantDirectionLabelLine1 { get; set; }
        public string MapTravellingMerchantDirectionLabelLine2 { get; set; }
        public string MapTravellingMerchantDirectionLabelLine3 { get; set; }
        public string MapTravellingMerchantDirectionTownLabel { get; set; }
        public string MapTravellingMerchantDirectionTownLabelSource { get; set; }
        public string MapTravellingMerchantDirectionTownLabelConfidence { get; set; }
        public string MapTravellingMerchantDirectionMatchedPylonType { get; set; }
        public double MapTravellingMerchantDirectionMatchedPylonDistanceTiles { get; set; }
        public int MapTravellingMerchantDirectionNearbyTownNpcCount { get; set; }
        public long MapTravellingMerchantDirectionLastScanTick { get; set; }
        public long MapTravellingMerchantDirectionLastScanAgeTicks { get; set; }
        public string MapTravellingMerchantDirectionDrawStatus { get; set; }
        public DateTime? MapTravellingMerchantDirectionLastTargetUtc { get; set; }
        public DateTime? MapTravellingMerchantDirectionLastDrawUtc { get; set; }
        public bool MapFootprintsDisplayEnabled { get; set; }
        public string PlayerWorldFootprintsLastStatus { get; set; }
        public string PlayerWorldFootprintsLastDecision { get; set; }
        public string PlayerWorldFootprintsLastMessage { get; set; }
        public string PlayerWorldFootprintsLastPairId { get; set; }
        public bool PlayerWorldFootprintsIdentityResolved { get; set; }
        public bool PlayerWorldFootprintsIsRecording { get; set; }
        public bool PlayerWorldFootprintsReadFailed { get; set; }
        public bool PlayerWorldFootprintsWriteFailed { get; set; }
        public bool PlayerWorldFootprintsRetentionTrimmed { get; set; }
        public long PlayerWorldFootprintsMaxRetainedHours { get; set; }
        public double PlayerWorldFootprintsRetainedHours { get; set; }
        public int PlayerWorldFootprintsSegmentCount { get; set; }
        public int PlayerWorldFootprintsPointCount { get; set; }
        public int PlayerWorldFootprintsBreakCount { get; set; }
        public long PlayerWorldFootprintsTimelineStartTicks { get; set; }
        public long PlayerWorldFootprintsTimelineEndTicks { get; set; }
        public double PlayerWorldFootprintsLastPointTileX { get; set; }
        public double PlayerWorldFootprintsLastPointTileY { get; set; }
        public long PlayerWorldFootprintsLastPointDurationTicks { get; set; }
        public long PlayerWorldFootprintsLastRecordRuntimeTick { get; set; }
        public string PlayerWorldFootprintsLastFlushStatus { get; set; }
        public DateTime? PlayerWorldFootprintsLastReadUtc { get; set; }
        public DateTime? PlayerWorldFootprintsLastRecordUtc { get; set; }
        public DateTime? PlayerWorldFootprintsLastWriteUtc { get; set; }
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
        public bool DiagnosticsOverlayVisible { get; set; }
        public long DrawCallCount { get; set; }
        public DateTime? LastDrawUtc { get; set; }
        public DateTime? LastUpdateUtc { get; set; }
        public DateTime? LastHeartbeatUtc { get; set; }
        public int FeatureCount { get; set; }
        public int EnabledFeatureCount { get; set; }
        public int AppSettingsEnabledFeatureCount { get; set; }
        public int FeatureSettingsEnabledFeatureCount { get; set; }
        public int EffectiveEnabledFeatureCount { get; set; }
        public int FeatureCatalogCount { get; set; }
        public int ImplementedFeatureCount { get; set; }
        public int VisibleFeatureCount { get; set; }
        public int HotkeyVisibleFeatureCount { get; set; }
        public Dictionary<string, int> UserCategoryCounts { get; set; }
        public Dictionary<string, int> CodeDomainCounts { get; set; }
        public long FeatureManagerUpdateCount { get; set; }
        public bool WorldGenDebugViewerConfiguredEnabled { get; set; }
        public bool DeveloperDebugCommandsConfiguredEnabled { get; set; }
        public bool WorldGenDebugViewerSessionConfiguredEnabled { get; set; }
        public bool DeveloperDebugCommandsSessionConfiguredEnabled { get; set; }
        public bool WorldGenDebugAttempted { get; set; }
        public bool WorldGenDebugFieldEnabled { get; set; }
        public string WorldGenDebugStatus { get; set; }
        public string WorldGenDebugMessage { get; set; }
        public string WorldGenDebugFieldOwner { get; set; }
        public DateTime? WorldGenDebugLastAttemptUtc { get; set; }
        public bool IsInMainMenu { get; set; }
        public bool IsInWorld { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool DiagnosticInputSkipped { get; set; }
        public string DiagnosticInputGateStatus { get; set; }
        public string DiagnosticInputSkipReason { get; set; }
        public DateTime? DiagnosticInputSkipUtc { get; set; }
        public int PlayerLife { get; set; }
        public int PlayerLifeMax { get; set; }
        public int PlayerMana { get; set; }
        public int PlayerManaMax { get; set; }
        public int SelectedItemType { get; set; }
        public string SelectedItemName { get; set; }
        public int InventoryNonEmptyCount { get; set; }
        public int ActiveBuffCount { get; set; }
        public int ActiveNpcCount { get; set; }
        public int TownNpcCount { get; set; }
        public int HostileNpcCount { get; set; }
        public int CritterCount { get; set; }
        public DateTime? LastGameStateReadUtc { get; set; }
        public string LastGameStateReadError { get; set; }
        public int PendingActionCount { get; set; }
        public long ActionQueueUpdateCount { get; set; }
        public string RunningAction { get; set; }
        public string RunningActionKind { get; set; }
        public string RunningActionSource { get; set; }
        public string RunningActionStatus { get; set; }
        public string LastActionKind { get; set; }
        public string LastActionStatus { get; set; }
        public string LastActionMessage { get; set; }
        public string LastActionUserMessage { get; set; }
        public string LastActionResultCode { get; set; }
        public long LastActionDurationMs { get; set; }
        public int RecentActionResultsCount { get; set; }
        public string LastActionResult { get; set; }
        public string RecentActionLine1 { get; set; }
        public string RecentActionLine2 { get; set; }
        public string RecentActionLine3 { get; set; }
        public int ActionQueueChannelLeaseCount { get; set; }
        public string ActionQueueRunningChannels { get; set; }
        public string ActionQueueOccupiedChannels { get; set; }
        public string ActionQueueBridgeBusyChannels { get; set; }
        public string ActionQueueRunningLeaseChannels { get; set; }
        public int ActionQueueBlockedPendingCount { get; set; }
        public string ActionQueueLastChannelDecision { get; set; }
        public string ActionQueueLastChannelBlockedReason { get; set; }
        public string ActionQueueChannelOwnerSummary { get; set; }
        public string ActionQueueBridgeBusySummary { get; set; }
        public string ActionQueuePendingChannelSummary { get; set; }
        public string ActionQueuePendingOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionStatus { get; set; }
        public string ActionQueueLastAdmissionDecision { get; set; }
        public string ActionQueueLastAdmissionReason { get; set; }
        public string ActionQueueLastAdmissionKind { get; set; }
        public string ActionQueueLastAdmissionSource { get; set; }
        public string ActionQueueLastAdmissionScenario { get; set; }
        public string ActionQueueLastAdmissionKey { get; set; }
        public string ActionQueueLastAdmissionRequiredChannels { get; set; }
        public string ActionQueueLastAdmissionBlockingChannels { get; set; }
        public string ActionQueueLastAdmissionConflictChannels { get; set; }
        public string ActionQueueLastAdmissionPendingConflictSummary { get; set; }
        public string ActionQueueLastAdmissionRunningConflictSummary { get; set; }
        public string ActionQueueLastAdmissionBridgeBusySummary { get; set; }
        public string ActionQueueLastAdmissionOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionSupersededRequestId { get; set; }
        public string ActionQueueLastAdmissionCoalescedRequestId { get; set; }
        public int ActionQueueSupersededPendingCount { get; set; }
        public int ActionQueueCoalescedPendingCount { get; set; }
        public string SchedulerLastSelectedRequest { get; set; }
        public string SchedulerLastSupersededRequest { get; set; }
        public string SchedulerLastFairnessBucket { get; set; }
        public string WorldAutomationLastWinner { get; set; }
        public string WorldAutomationFairnessDebt { get; set; }
        public DateTime? WorldAutomationFairnessDecisionUtc { get; set; }
        public int BackgroundRequestCoalescedCount { get; set; }
        public int ExpiredPendingDroppedCount { get; set; }
        public int ActionQueueCleanupLeaseCount { get; set; }
        public string ActionQueueCleanupLeaseChannels { get; set; }
        public string ActionQueueLastCleanupOwner { get; set; }
        public string ActionQueueLastCleanupReason { get; set; }
        public int ActionQueueDirectEnqueueCount { get; set; }
        public string ActionQueueLastDirectEnqueueKind { get; set; }
        public string ActionQueueLastDirectEnqueueSource { get; set; }
        public string ActionQueueLastDirectEnqueueScenario { get; set; }
        public string ActionQueueLastDirectEnqueueAdmissionKey { get; set; }
        public string ActionQueueLastDirectEnqueueRequiredChannels { get; set; }
        public int ActionQueueExpiredPendingCount { get; set; }
        public string ActionQueueLastPendingExpiryReason { get; set; }
        public string ItemUseBridgeLastStatus { get; set; }
        public string ItemUseBridgeLastMessage { get; set; }
        public string ItemUseBridgeLastRequestId { get; set; }
        public string ItemUseBridgePendingRequestId { get; set; }
        public long ItemUseBridgePendingAgeMs { get; set; }
        public int ItemUseBridgeConsumeCount { get; set; }
        public int ItemUseBridgeSucceededCount { get; set; }
        public int ItemUseBridgeAttemptedButUnverifiedCount { get; set; }
        public int ItemUseBridgeFailedCount { get; set; }
        public string ItemCheckWriterOwner { get; set; }
        public string ItemCheckWriterOwnerRequestId { get; set; }
        public string ItemCheckWriterPhase { get; set; }
        public string ItemCheckWriterDecisionReason { get; set; }
        public string ItemCheckWriterBlockedCandidates { get; set; }
        public DateTime? ItemCheckWriterDecisionUtc { get; set; }
        public bool EnableDiagnosticInputTests { get; set; }
        public int DiagnosticInputTestSlot { get; set; }
        public int DiagnosticInputTestSlotDisplay { get; set; }
        public int DiagnosticTestSlot { get; set; }
        public int DiagnosticTestSlotDisplay { get; set; }
        public int DiagnosticTestSlotItemType { get; set; }
        public string DiagnosticTestSlotItemName { get; set; }
        public int DiagnosticTestSlotItemStack { get; set; }
        public string DiagnosticTestSlotSuitability { get; set; }
        public string DiagnosticTestSlotHint { get; set; }
        public string ActionEventsPath { get; set; }
        public DateTime? LastActionEventWrittenAtUtc { get; set; }
        public string LastDiagnosticSourceKind { get; set; }
        public string LastDiagnosticButtonId { get; set; }
        public string LastDiagnosticButtonLabel { get; set; }
        public DateTime? LastButtonClickUtc { get; set; }
        public string LastButtonResultCode { get; set; }
        public string LastButtonMessage { get; set; }
        public bool UiPrimitiveRendererReady { get; set; }
        public string UiPrimitiveRendererLastMessage { get; set; }
        public bool UiMouseReadAvailable { get; set; }
        public string UiMouseReadLastMessage { get; set; }
        public bool UiMouseCaptureAvailable { get; set; }
        public string UiMouseCaptureLastMessage { get; set; }
        public bool UiClickSuppressionAttempted { get; set; }
        public string UiClickSuppressionMode { get; set; }
        public bool UiClickSuppressionSucceeded { get; set; }
        public bool ButtonHoverAtUpdatePrefix { get; set; }
        public bool OverlayHoverAtUpdatePrefix { get; set; }
        public int LastMouseX { get; set; }
        public int LastMouseY { get; set; }
        public int TerrariaMouseX { get; set; }
        public int TerrariaMouseY { get; set; }
        public bool TerrariaLeftDown { get; set; }
        public bool TerrariaLeftReleaseAvailable { get; set; }
        public bool TerrariaLeftRelease { get; set; }
        public int OsClientMouseX { get; set; }
        public int OsClientMouseY { get; set; }
        public bool OsLeftDown { get; set; }
        public double UiScale { get; set; }
        public bool UiScaleAvailable { get; set; }
        public bool UiScaleMatrixAvailable { get; set; }
        public string MouseReadMode { get; set; }
        public string MouseReadLastError { get; set; }
        public string HitTestMode { get; set; }
        public int HitTestX { get; set; }
        public int HitTestY { get; set; }
        public bool HitTestConflict { get; set; }
        public string HitTestCandidateSummary { get; set; }
        public string ClickSource { get; set; }
        public string LastButtonHitTestMode { get; set; }
        public string LastButtonClickSource { get; set; }
        public string HoveredButtonId { get; set; }
        public string HoveredButtonLabel { get; set; }
        public string HoveredButtonHint { get; set; }
        public bool HoveredButtonEnabled { get; set; }
        public int HoveredButtonVisualX { get; set; }
        public int HoveredButtonVisualY { get; set; }
        public int HoveredButtonVisualWidth { get; set; }
        public int HoveredButtonVisualHeight { get; set; }
        public int HoveredButtonHitX { get; set; }
        public int HoveredButtonHitY { get; set; }
        public int HoveredButtonHitWidth { get; set; }
        public int HoveredButtonHitHeight { get; set; }
        public long LegacyUiLayoutCacheHitCount { get; set; }
        public long LegacyUiLayoutCacheMissCount { get; set; }
        public int LegacyUiLastFrameVisibleElementCount { get; set; }
        public long LegacyUiHoverReuseCount { get; set; }
        public long LegacyUiHoverTooltipCacheHitCount { get; set; }
        public long LegacyUiHoverTooltipCacheMissCount { get; set; }
        public long LegacyUiHoverDiagnosticSuppressedCount { get; set; }
        public long LegacyUiScrollSnapshotSkippedCount { get; set; }
        public long LegacyUiScrollEventCoalescedCount { get; set; }
        public long LegacyUiRetainedFrameCacheHitCount { get; set; }
        public long LegacyUiRetainedFrameCacheMissCount { get; set; }
        public long LegacyUiRetainedFrameFallbackCount { get; set; }
        public int LegacyUiRetainedFrameVisibleElementCount { get; set; }
        public long LegacyUiActionUpdateSkippedCount { get; set; }
        public long LegacyUiActionUpdateRanCount { get; set; }
        public int LegacyUiPendingCommandCountLast { get; set; }
        public int LegacyUiDispatchedCommandCountLast { get; set; }
        public double LegacyUiDispatchElapsedMsLast { get; set; }
        public long LegacyUiCommandCoalescedCount { get; set; }
        public long LegacyUiDragFrameActionSkipCount { get; set; }
        public string LegacyMainUiLastF5HotkeyDecision { get; set; }
        public string LegacyMainUiLastF5HotkeyReason { get; set; }
        public bool LegacyMainUiLastF5HotkeyDown { get; set; }
        public bool LegacyMainUiLastF5HotkeyWasDown { get; set; }
        public int LegacyMainUiLastF5HotkeyDebounceRemainingMs { get; set; }
        public DateTime? LegacyMainUiLastF5HotkeyUtc { get; set; }
        public bool LegacyImePanelFocused { get; set; }
        public string LegacyImePanelDiagnosticMessage { get; set; }
        public string LegacyImePanelLastStatus { get; set; }
        public string LegacyImePanelLastMessage { get; set; }
        public bool LegacyImePanelAnchorAttachedThisFrame { get; set; }
        public bool LegacyImePanelDrawnThisFrame { get; set; }
        public int LegacyImePanelReflectionResolveCount { get; set; }
        public string LegacyImePanelCadenceSummary { get; set; }
        public string LastDiagnosticHotkey { get; set; }
        public DateTime? LastDiagnosticHotkeyUtc { get; set; }
        public string LastDiagnosticHotkeyMessage { get; set; }
        public string QuickActionLastKind { get; set; }
        public string QuickActionLastStatus { get; set; }
        public string QuickActionLastResultCode { get; set; }
        public string QuickActionLastMessage { get; set; }
        public string MouseTargetLastStatus { get; set; }
        public string MouseTargetLastResultCode { get; set; }
        public string MouseTargetLastMessage { get; set; }
        public long RuntimeUpdateCount { get; set; }
        public double AverageRuntimeUpdateMs { get; set; }
        public double LastRuntimeUpdateMs { get; set; }
        public double LastUpdateStartGapMs { get; set; }
        public double LastGameStateReadMs { get; set; }
        public double LastActionQueueUpdateMs { get; set; }
        public double LastInputActionUpdateMs { get; set; }
        public double LastInformationDrawMs { get; set; }
        public int RecentPerformanceWindowCapacitySamples { get; set; }
        public int RecentPerformanceWindowSampleCount { get; set; }
        public double RecentRuntimeUpdateAverageMs { get; set; }
        public double RecentGameStateReadAverageMs { get; set; }
        public double RecentActionQueueUpdateAverageMs { get; set; }
        public double RecentInputActionUpdateAverageMs { get; set; }
        public double RecentInformationDrawAverageMs { get; set; }
        public long UiTextFastPathHitCount { get; set; }
        public long UiTextFallbackCount { get; set; }
        public string LastSlowestStageName { get; set; }
        public double LastSlowestStageElapsedMs { get; set; }
        public string LastSlowestOperationName { get; set; }
        public double LastSlowestOperationElapsedMs { get; set; }
        public string PerformanceEventsPath { get; set; }
        public long PerformanceHitchCount { get; set; }
        public DateTime? LastPerformanceHitchUtc { get; set; }
        public string LastPerformanceHitchReason { get; set; }
        public double LastPerformanceHitchUpdateGapMs { get; set; }
        public double LastPerformanceHitchRuntimeUpdateMs { get; set; }
        public double LastPerformanceHitchGameStateReadMs { get; set; }
        public double LastPerformanceHitchActionQueueUpdateMs { get; set; }
        public double LastPerformanceHitchInputActionUpdateMs { get; set; }
        public double LastPerformanceHitchInformationDrawMs { get; set; }
        public string LastPerformanceHitchSlowestStageName { get; set; }
        public double LastPerformanceHitchSlowestStageMs { get; set; }
        public string LastPerformanceHitchSlowestOperationName { get; set; }
        public double LastPerformanceHitchSlowestOperationMs { get; set; }
        public long PerformanceOperationEventCount { get; set; }
        public string LastPerformanceOperationScenario { get; set; }
        public DateTime? LastPerformanceOperationUtc { get; set; }
        public double LastPerformanceOperationElapsedMs { get; set; }
        public double LastPerformanceOperationThresholdMs { get; set; }
        public string LastPerformanceOperationReason { get; set; }
        public string LastPerformanceOperationOwnerSummary { get; set; }
        public bool ReflectionCacheReady { get; set; }
        public int ReflectionCacheMissCount { get; set; }
        public string ReflectionCacheLastMissKey { get; set; }
        public DateTime? ReflectionCacheLastMissUtc { get; set; }
        public string ReflectionCacheLastError { get; set; }
        public bool InputCompatReady { get; set; }
        public bool SelectedItemGetterReady { get; set; }
        public bool SelectedItemSelectorReady { get; set; }
        public bool SelectedItemAccessorReady { get; set; }
        public string PlayerTypeName { get; set; }
        public string LastInputCompatError { get; set; }
        public DateTime? ConfigLastSaveUtc { get; set; }
        public bool ConfigLastSaveSucceeded { get; set; }
        public string ConfigLastSaveSummary { get; set; }
        public bool ConfigLastSaveAppSettingsSucceeded { get; set; }
        public string ConfigLastSaveAppSettingsPath { get; set; }
        public string ConfigLastSaveAppSettingsError { get; set; }
        public bool ConfigLastSaveFeatureSettingsSucceeded { get; set; }
        public string ConfigLastSaveFeatureSettingsPath { get; set; }
        public string ConfigLastSaveFeatureSettingsError { get; set; }
        public bool ConfigLastSaveHotkeySettingsSucceeded { get; set; }
        public string ConfigLastSaveHotkeySettingsPath { get; set; }
        public string ConfigLastSaveHotkeySettingsError { get; set; }
        public string AutoStackLastDecision { get; set; }
        public string AutoStackLastInventorySignature { get; set; }
        public string AutoStackLastPendingItemIds { get; set; }
        public string AutoStackLastDetectedItemIds { get; set; }
        public long AutoStackPendingSinceTick { get; set; }
        public long AutoStackLastPendingChangeTick { get; set; }
        public string AutoStackLastPendingClearReason { get; set; }
        public string AutoStackPendingTransactionState { get; set; }
        public int AutoStackPendingRetryCount { get; set; }
        public string AutoStackLastSubmitRequestId { get; set; }
        public string AutoStackLastResult { get; set; }
        public string AutoStackLastUnverifiedReason { get; set; }
        public string AutoStackInventoryTransactionSlots { get; set; }
        public string AutoStackInventoryTransactionBlockingReason { get; set; }
        public string AutoStackActionResultDeliveryMode { get; set; }
        public DateTime? AutoStackLastDecisionUtc { get; set; }
        public string AutoSellLastDecision { get; set; }
        public string AutoSellLastInventorySignature { get; set; }
        public string AutoSellLastItemIds { get; set; }
        public DateTime? AutoSellLastDecisionUtc { get; set; }
        public string AutoDiscardLastDecision { get; set; }
        public string AutoDiscardLastInventorySignature { get; set; }
        public string AutoDiscardLastItemIds { get; set; }
        public DateTime? AutoDiscardLastDecisionUtc { get; set; }
        public string QuickReforgeLastDecision { get; set; }
        public string QuickReforgeLastTargetPrefixes { get; set; }
        public string QuickReforgeLastMatchedPrefix { get; set; }
        public DateTime? QuickReforgeLastDecisionUtc { get; set; }
        public string AutoTaxCollectLastDecision { get; set; }
        public DateTime? AutoTaxCollectLastDecisionUtc { get; set; }
        public int AutoTaxCollectTargetNpcIndex { get; set; }
        public int AutoTaxCollectTargetWhoAmI { get; set; }
        public string AutoTaxCollectTargetName { get; set; }
        public int AutoTaxCollectTaxMoney { get; set; }
        public string AutoTaxCollectLastRequestId { get; set; }
        public string AutoCaptureCritterLastDecision { get; set; }
        public DateTime? AutoCaptureCritterLastDecisionUtc { get; set; }
        public int AutoCaptureCritterBugNetSlot { get; set; }
        public int AutoCaptureCritterBugNetItemType { get; set; }
        public int AutoCaptureCritterTargetNpcIndex { get; set; }
        public int AutoCaptureCritterTargetNpcType { get; set; }
        public string AutoCaptureCritterFishingProtectionState { get; set; }
        public string AutoMiningLastDecision { get; set; }
        public DateTime? AutoMiningLastDecisionUtc { get; set; }
        public string AutoMiningLastHotkey { get; set; }
        public string AutoMiningLastHotkeyResultCode { get; set; }
        public string AutoMiningLastHotkeyBlockedReason { get; set; }
        public DateTime? AutoMiningLastHotkeyDecisionUtc { get; set; }
        public string AutoHarvestLastDecision { get; set; }
        public DateTime? AutoHarvestLastDecisionUtc { get; set; }
        public string AutoHarvestLastAction { get; set; }
        public int AutoHarvestToolSlot { get; set; }
        public int AutoHarvestToolItemType { get; set; }
        public int AutoHarvestTargetTileX { get; set; }
        public int AutoHarvestTargetTileY { get; set; }
        public int AutoHarvestTargetSeedItemType { get; set; }
        public int AutoHarvestPendingReplantCount { get; set; }
        public string QuickBagOpenLastDecision { get; set; }
        public DateTime? QuickBagOpenLastDecisionUtc { get; set; }
        public int QuickBagOpenBagSlot { get; set; }
        public int QuickBagOpenBagItemType { get; set; }
        public string QuickBagOpenBagItemName { get; set; }
        public string AutoDepositCoinsLastDecision { get; set; }
        public DateTime? AutoDepositCoinsLastDecisionUtc { get; set; }
        public string AutoDepositCoinsLastInventorySignature { get; set; }
        public string AutoDepositCoinsLastCoinItemIds { get; set; }
        public string AutoExtractinatorLastDecision { get; set; }
        public DateTime? AutoExtractinatorLastDecisionUtc { get; set; }
        public int AutoExtractinatorItemSlot { get; set; }
        public int AutoExtractinatorItemType { get; set; }
        public int AutoExtractinatorTileX { get; set; }
        public int AutoExtractinatorTileY { get; set; }
        public int AutoExtractinatorTileType { get; set; }
        public string KeepFavoritedLastDecision { get; set; }
        public DateTime? KeepFavoritedLastDecisionUtc { get; set; }
        public int KeepFavoritedSlot { get; set; }
        public int KeepFavoritedItemType { get; set; }
        public string KeepFavoritedSignature { get; set; }
        public string InformationEnabledSummary { get; set; }
        public int InformationNpcLabelsDrawn { get; set; }
        public int InformationChestLabelsDrawn { get; set; }
        public int InformationSignTextLabelsDrawn { get; set; }
        public int InformationTombstoneTextLabelsDrawn { get; set; }
        public int InformationTileHighlightsDrawn { get; set; }
        public int InformationStatusLinesDrawn { get; set; }
        public double InformationLastDrawElapsedMs { get; set; }
        public string InformationLastSkipReason { get; set; }
        public long InformationStatusPanelLayoutCacheHitCount { get; set; }
        public long InformationStatusPanelLayoutCacheMissCount { get; set; }
        public long InformationSignTextLayoutCacheHitCount { get; set; }
        public long InformationSignTextLayoutCacheMissCount { get; set; }
        public long InformationWorldLabelSnapshotRefreshCount { get; set; }
        public long InformationNpcLabelSnapshotRefreshCount { get; set; }
        public long InformationChestLabelSnapshotRefreshCount { get; set; }
        public long InformationChestLabelSortRefreshCount { get; set; }
        public long InformationChestAlwaysScanCacheHitCount { get; set; }
        public long InformationChestAlwaysScanCacheMissCount { get; set; }
        public string InformationChestAlwaysLastDirtyReason { get; set; }
        public long InformationChestAlwaysSafeRefreshCount { get; set; }
        public int InformationChestAlwaysTilesVisitedLast { get; set; }
        public string InformationChestAlwaysTypedTileFastPathStatus { get; set; }
        public long InformationChestAlwaysNameCacheHitCount { get; set; }
        public long InformationChestAlwaysNameCacheMissCount { get; set; }
        public int InformationChestAlwaysPartialScanFrameCount { get; set; }
        public int InformationChestAlwaysPartialScanPendingCount { get; set; }
        public long InformationChestAlwaysStableSnapshotId { get; set; }
        public long InformationWorldContextCacheHitCount { get; set; }
        public long InformationWorldContextCacheMissCount { get; set; }
        public string InformationWorldContextProfile { get; set; }
        public long InformationWorldContextFileDataRefreshCount { get; set; }
        public long InformationStatusLineCacheHitCount { get; set; }
        public long InformationStatusLineCacheMissCount { get; set; }
        public long InformationFishingCatchEarlyCacheHitCount { get; set; }
        public long InformationFishingCatchEarlyCacheMissCount { get; set; }
        public long InformationFishingWaterScanCount { get; set; }
        public long InformationFishingConditionsReadCount { get; set; }
        public long InformationFishingBobberObserverFreshInactiveSkipCount { get; set; }
        public long InformationFishingProjectileFallbackScanCount { get; set; }
        public bool SearchChestLocatorOverlayEnabled { get; set; }
        public long SearchChestLocatorOverlayQueryVersion { get; set; }
        public string SearchChestLocatorOverlaySnapshotStatus { get; set; }
        public int SearchChestLocatorOverlayCandidateChestCount { get; set; }
        public int SearchChestLocatorOverlayScannedChestCount { get; set; }
        public int SearchChestLocatorOverlayHitCount { get; set; }
        public int SearchChestLocatorOverlayDrawnHitCount { get; set; }
        public string SearchChestLocatorOverlaySkipReason { get; set; }
        public string SearchChestLocatorOverlayRecentElapsedBucket { get; set; }
        public long SearchChestLocatorOverlaySnapshotAgeTicks { get; set; }
        public bool SearchChestLocatorSectionRequestEnabled { get; set; }
        public bool SearchChestLocatorSectionRequestMultiplayerClient { get; set; }
        public bool SearchChestLocatorSectionRequestAttempted { get; set; }
        public bool SearchChestLocatorSectionRequestSent { get; set; }
        public bool SearchChestLocatorSectionRequestThrottled { get; set; }
        public string SearchChestLocatorSectionRequestStatus { get; set; }
        public string SearchChestLocatorSectionRequestFailureReason { get; set; }
        public string SearchChestLocatorSectionRequestSectionKey { get; set; }
        public int SearchChestLocatorSectionRequestSectionX { get; set; }
        public int SearchChestLocatorSectionRequestSectionY { get; set; }
        public long SearchChestLocatorSectionRequestQueryVersion { get; set; }
        public long SearchChestLocatorSectionRequestTick { get; set; }
        public long SearchChestLocatorSectionRequestCooldownRemainingTicks { get; set; }
        public bool MapQuickAnnouncementLastTriggered { get; set; }
        public string MapQuickAnnouncementLastResultCode { get; set; }
        public string MapQuickAnnouncementLastTargetKind { get; set; }
        public string MapQuickAnnouncementLastTargetName { get; set; }
        public string MapQuickAnnouncementLastTargetSummary { get; set; }
        public int MapQuickAnnouncementLastTargetCount { get; set; }
        public string MapQuickAnnouncementLastResolveDetail { get; set; }
        public string MapQuickAnnouncementLastTargetSource { get; set; }
        public string MapQuickAnnouncementLastUiHoverSource { get; set; }
        public string MapQuickAnnouncementLastUiHoverState { get; set; }
        public string MapQuickAnnouncementLastUiHoverHookStatus { get; set; }
        public string MapQuickAnnouncementLastPendingState { get; set; }
        public int MapQuickAnnouncementLastHoverCacheAgeUpdates { get; set; }
        public string MapQuickAnnouncementLastPlacementLookupSource { get; set; }
        public string MapQuickAnnouncementLastFallbackReason { get; set; }
        public bool MapQuickAnnouncementLastIsAir { get; set; }
        public bool MapQuickAnnouncementLastCooldownBlocked { get; set; }
        public bool MapQuickAnnouncementLastSendSucceeded { get; set; }
        public string MapQuickAnnouncementLastFailureReason { get; set; }
        public string MapQuickAnnouncementLastHotkeySummary { get; set; }
        public bool MapQuickAnnouncementLastInputConsumed { get; set; }
        public string MapQuickAnnouncementLastInputConsumeResult { get; set; }
        public string MapQuickAnnouncementLastVisibilityVerdict { get; set; }
        public string MapQuickAnnouncementLastVisibilityReason { get; set; }
        public string MapQuickAnnouncementLastVisibleLayers { get; set; }
        public string MapQuickAnnouncementLastBlockedLayers { get; set; }
        public bool MapQuickAnnouncementLastCircuitOnly { get; set; }
        public string MapQuickAnnouncementLastEchoGate { get; set; }
        public bool MapQuickAnnouncementLastInvisibleAir { get; set; }
        public string MapQuickAnnouncementLastVisibilityUnavailableReason { get; set; }
        public DateTime? MapQuickAnnouncementLastDecisionUtc { get; set; }
        public bool BlueprintHandheldActionBarVisible { get; set; }
        public string BlueprintHandheldActionBarBlockedReason { get; set; }
        public int BlueprintHandheldActionBarToolItemId { get; set; }
        public int BlueprintHandheldActionBarSelectedItemType { get; set; }
        public string BlueprintHandheldActionBarLastAction { get; set; }
        public string BlueprintHandheldActionBarLastResultCode { get; set; }
        public string BlueprintHandheldActionBarHoveredButtonId { get; set; }
        public string BlueprintHandheldActionBarPressedButtonId { get; set; }
        public string BlueprintHandheldActionBarLastMouseReadMode { get; set; }
        public string BlueprintHandheldActionBarLastOwnershipReason { get; set; }
        public string BlueprintHandheldActionBarLastInputTrace { get; set; }
        public string BlueprintHandheldActionBarLastOwnershipTrace { get; set; }
        public string BlueprintWorldOverlayLastInputTrace { get; set; }
        public string BlueprintCreationPrefixWorldOverlayInputTrace { get; set; }
        public string BlueprintCreationAfterPlayerInputWorldOverlayInputTrace { get; set; }
        public string BlueprintCreationLastClearReasonTrace { get; set; }
        public string BlueprintMirrorLastStatus { get; set; }
        public string BlueprintMirrorLastMessage { get; set; }
        public string BlueprintMirrorMode { get; set; }
        public string BlueprintMirrorTemplateId { get; set; }
        public string BlueprintMirrorTemplateName { get; set; }
        public string BlueprintMirrorBlockedReason { get; set; }
        public string BlueprintMirrorWarningReason { get; set; }
        public int BlueprintMirrorMirroredCellCount { get; set; }
        public int BlueprintMirrorMirroredLayerCount { get; set; }
        public int BlueprintMirrorRejectedLayerCount { get; set; }
        public int BlueprintMirrorWarningLayerCount { get; set; }
        public DateTime? BlueprintMirrorLastAttemptedUtc { get; set; }
        public string BlueprintDiagnosticsTemplateReadStatus { get; set; }
        public string BlueprintDiagnosticsTemplateReadMessage { get; set; }
        public int BlueprintDiagnosticsTemplateCount { get; set; }
        public int BlueprintDiagnosticsInstanceCount { get; set; }
        public int BlueprintDiagnosticsVisibleInstanceCount { get; set; }
        public int BlueprintDiagnosticsHiddenInstanceCount { get; set; }
        public int BlueprintDiagnosticsEffectiveProjectionLayerCount { get; set; }
        public int BlueprintDiagnosticsErasedProjectionLayerCount { get; set; }
        public int BlueprintDiagnosticsMaterialMissingItemCount { get; set; }
        public int BlueprintDiagnosticsMaterialMissingStackTotal { get; set; }
        public bool BlueprintDiagnosticsAutoPlacementEnabled { get; set; }
        public int BlueprintDiagnosticsAutoPlacementCandidateCount { get; set; }
        public long BlueprintPerformanceSlowEventCount { get; set; }
        public string BlueprintPerformanceLastScenario { get; set; }
        public double BlueprintPerformanceLastElapsedMs { get; set; }
        public string BlueprintProjectionLastStatus { get; set; }
        public string BlueprintProjectionLastMessage { get; set; }
        public string BlueprintProjectionWorldPairKey { get; set; }
        public string BlueprintProjectionWorldKey { get; set; }
        public int BlueprintProjectionInstanceCount { get; set; }
        public int BlueprintProjectionVisibleInstanceCount { get; set; }
        public int BlueprintProjectionHiddenInstanceCount { get; set; }
        public int BlueprintProjectionEffectiveLayerCount { get; set; }
        public int BlueprintProjectionFulfilledLayerCount { get; set; }
        public int BlueprintProjectionCompletedLayerCount { get; set; }
        public int BlueprintProjectionMissingLayerCount { get; set; }
        public int BlueprintProjectionConflictLayerCount { get; set; }
        public int BlueprintProjectionCoveredLayerCount { get; set; }
        public int BlueprintProjectionErasedLayerCount { get; set; }
        public int BlueprintProjectionUnavailableLayerCount { get; set; }
        public int BlueprintProjectionCacheHitCount { get; set; }
        public int BlueprintProjectionCacheMissCount { get; set; }
        public double BlueprintProjectionLastResolveElapsedMs { get; set; }
        public long BlueprintProjectionResolveCount { get; set; }
        public double BlueprintProjectionAverageResolveElapsedMs { get; set; }
        public DateTime? BlueprintProjectionLastResolvedUtc { get; set; }
        public string BlueprintMaterialsLastStatus { get; set; }
        public string BlueprintMaterialsLastMessage { get; set; }
        public string BlueprintMaterialsWorldPairKey { get; set; }
        public string BlueprintMaterialsWorldKey { get; set; }
        public string BlueprintMaterialsProjectionStatus { get; set; }
        public int BlueprintMaterialsRequiredItemCount { get; set; }
        public int BlueprintMaterialsMissingItemCount { get; set; }
        public int BlueprintMaterialsRequiredStackTotal { get; set; }
        public int BlueprintMaterialsAvailableStackTotal { get; set; }
        public int BlueprintMaterialsMissingStackTotal { get; set; }
        public int BlueprintMaterialsProjectionMissingLayerCount { get; set; }
        public int BlueprintMaterialsMaterializedMissingLayerCount { get; set; }
        public int BlueprintMaterialsSkippedFulfilledLayerCount { get; set; }
        public int BlueprintMaterialsSkippedConflictLayerCount { get; set; }
        public int BlueprintMaterialsSkippedUnavailableLayerCount { get; set; }
        public int BlueprintMaterialsSkippedMissingLayerWithoutMaterialCount { get; set; }
        public bool BlueprintMaterialsInventoryReadSucceeded { get; set; }
        public string BlueprintMaterialsInventoryReadStatus { get; set; }
        public string BlueprintMaterialsInventoryReadMessage { get; set; }
        public int BlueprintMaterialsInventoryMainStackTotal { get; set; }
        public int BlueprintMaterialsInventoryVoidBagStackTotal { get; set; }
        public bool BlueprintMaterialsWindowVisible { get; set; }
        public int BlueprintMaterialsWindowOpacityPercent { get; set; }
        public int BlueprintMaterialsCacheHitCount { get; set; }
        public int BlueprintMaterialsCacheMissCount { get; set; }
        public double BlueprintMaterialsLastResolveElapsedMs { get; set; }
        public long BlueprintMaterialsResolveCount { get; set; }
        public double BlueprintMaterialsAverageResolveElapsedMs { get; set; }
        public DateTime? BlueprintMaterialsLastResolvedUtc { get; set; }
        public bool BlueprintEraseRegionActive { get; set; }
        public bool BlueprintEraseRegionDragging { get; set; }
        public bool BlueprintEraseRegionHasFixedTarget { get; set; }
        public string BlueprintEraseRegionTargetInstanceId { get; set; }
        public string BlueprintEraseRegionTargetInstanceName { get; set; }
        public int BlueprintEraseRegionTargetLayerOrder { get; set; }
        public string BlueprintEraseRegionWorldPairKey { get; set; }
        public string BlueprintEraseRegionWorldKey { get; set; }
        public int BlueprintEraseRegionLastErasedCellCount { get; set; }
        public int BlueprintEraseRegionTotalEraseCellCount { get; set; }
        public string BlueprintEraseRegionLastStatus { get; set; }
        public string BlueprintEraseRegionLastMessage { get; set; }
        public string BlueprintEraseRegionLastInputOwner { get; set; }
        public bool BlueprintAutoPlacementEnabled { get; set; }
        public string BlueprintAutoPlacementLastStatus { get; set; }
        public string BlueprintAutoPlacementLastMessage { get; set; }
        public string BlueprintAutoPlacementWorldPairKey { get; set; }
        public string BlueprintAutoPlacementWorldKey { get; set; }
        public string BlueprintAutoPlacementProjectionStatus { get; set; }
        public int BlueprintAutoPlacementCandidateCount { get; set; }
        public int BlueprintAutoPlacementSkippedFulfilledLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedConflictLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedUnavailableLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedUnsupportedLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedNoMaterialLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedInsufficientMaterialLayerCount { get; set; }
        public int BlueprintAutoPlacementSkippedVoidBagOnlyLayerCount { get; set; }
        public string BlueprintAutoPlacementSelectedInstanceId { get; set; }
        public string BlueprintAutoPlacementSelectedInstanceName { get; set; }
        public int BlueprintAutoPlacementSelectedLayerOrder { get; set; }
        public string BlueprintAutoPlacementSelectedLayerKind { get; set; }
        public int BlueprintAutoPlacementSelectedWorldTileX { get; set; }
        public int BlueprintAutoPlacementSelectedWorldTileY { get; set; }
        public int BlueprintAutoPlacementSelectedMaterialItemId { get; set; }
        public int BlueprintAutoPlacementSelectedOriginalMaterialItemId { get; set; }
        public int BlueprintAutoPlacementSelectedMaterialStack { get; set; }
        public int BlueprintAutoPlacementSelectedMaterialAvailableStack { get; set; }
        public bool BlueprintAutoPlacementSelectedReplacementApplied { get; set; }
        public string BlueprintAutoPlacementSelectedReplacementCategory { get; set; }
        public string BlueprintAutoPlacementLastAdmissionStatus { get; set; }
        public string BlueprintAutoPlacementLastAdmissionReason { get; set; }
        public string BlueprintAutoPlacementLastAdmissionKey { get; set; }
        public string BlueprintAutoPlacementLastRequestId { get; set; }
        public int BlueprintAutoPlacementSubmittedCount { get; set; }
        public int BlueprintAutoPlacementDeniedCount { get; set; }
        public int BlueprintAutoPlacementFailClosedCount { get; set; }
        public int BlueprintAutoPlacementSucceededCount { get; set; }
        public int BlueprintAutoPlacementAttemptedButUnverifiedCount { get; set; }
        public string BlueprintAutoPlacementLastResultCode { get; set; }
        public string BlueprintAutoPlacementLastFailureReason { get; set; }
        public double BlueprintAutoPlacementLastResolveElapsedMs { get; set; }
        public long BlueprintAutoPlacementCandidateScanCount { get; set; }
        public double BlueprintAutoPlacementAverageCandidateScanElapsedMs { get; set; }
        public DateTime? BlueprintAutoPlacementLastResolvedUtc { get; set; }
        public bool FishingAutomationNeedsTick { get; set; }
        public bool FishingDisplayNeedsCatchResolver { get; set; }
        public bool FishingHasResidualState { get; set; }
        public bool FishingSessionActive { get; set; }
        public string FishingLastDecision { get; set; }
        public string FishingLastSkipReason { get; set; }
        public int FishingCurrentBobberIdentity { get; set; }
        public int FishingLastProcessedHookIdentity { get; set; }
        public bool FishingWaitingForBobberGone { get; set; }
        public int FishingRecastDelayTicks { get; set; }
        public bool FishingRecastWaitingForBobber { get; set; }
        public int FishingRecastBobberWaitTicks { get; set; }
        public int FishingRecastRetryCount { get; set; }
        public bool FishingFilterSkipInProgress { get; set; }
        public string FishingFilterSkipRequestId { get; set; }
        public bool FishingFilterSkipWaitingForBobberGone { get; set; }
        public int FishingFilterSkipTemporarySlot { get; set; }
        public string FishingFilterSkipLastResult { get; set; }
        public string FishingFilterSkipRestoreFailureReason { get; set; }
        public double FishingCastWorldX { get; set; }
        public double FishingCastWorldY { get; set; }
        public int FishingOriginalLoadoutIndex { get; set; }
        public int FishingTargetLoadoutIndex { get; set; }
        public bool FishingAutoEquipmentApplied { get; set; }
        public int FishingAutoEquipmentPendingRestoreCount { get; set; }
        public string FishingAutoEquipmentLastDecision { get; set; }
        public string FishingAutoEquipmentLastSkipReason { get; set; }
        public int FishingAutoEquipmentAppliedMoveCount { get; set; }
        public bool FishingAutoEquipmentStillHoldingOriginalRod { get; set; }
        public bool FishingAutoEquipmentManualInventoryInteractionDetected { get; set; }
        public int FishingQuestFishStoreCooldownTicks { get; set; }
        public int FishingQuestFishLastItemId { get; set; }
        public int FishingQuestFishLastSlotCount { get; set; }
        public string FishingAutoStoreLastMode { get; set; }
        public string FishingAutoStoreLastInventorySignature { get; set; }
        public string FishingAutoStoreLastPendingItemIds { get; set; }
        public string FishingAutoStoreLastDiagnosticMessage { get; set; }
        public bool FishingHookInstalled { get; set; }
        public long FishingHookLastObservationTick { get; set; }
        public long FishingFallbackScanExecutedCount { get; set; }
        public long FishingFallbackScanSkippedHookFreshCount { get; set; }
        public long FishingFallbackScanForcedDisappearanceConfirmationCount { get; set; }
        public string FishingAutomationDispatchReason { get; set; }
        public int FishingAutomationDispatchCadenceTicks { get; set; }
        public long FishingAutomationIdleFastSkipCount { get; set; }
        public long FishingAutomationIdleWatchdogTickCount { get; set; }
        public long FishingObserverFreshActiveCount { get; set; }
        public long FishingObserverFreshInactiveSkipCount { get; set; }
        public long FishingFallbackScanIdleSkippedCount { get; set; }
        public long FishingFallbackScanHookStaleCount { get; set; }
        public string FishingTickSubpathLast { get; set; }
        public int FishingResidualStateMask { get; set; }
        public string FishingFilterMode { get; set; }
        public string FishingFilterMatchMode { get; set; }
        public string FishingFilterCatchKind { get; set; }
        public int FishingFilterCatchId { get; set; }
        public string FishingFilterCatchName { get; set; }
        public string FishingFilterDecision { get; set; }
        public string FishingFilterDecisionReason { get; set; }
        public string FishingFilterMatchedRule { get; set; }
        public bool FishingFilterDryRun { get; set; }
        public bool FishingFilterCutRodSkipEnabled { get; set; }
        public bool MovementSimulatedJumpEnabled { get; set; }
        public bool MovementSimulatedJumpLastTriggered { get; set; }
        public DateTime? MovementSimulatedJumpLastTriggerUtc { get; set; }
        public string MovementSimulatedJumpLastDecision { get; set; }
        public string MovementSimulatedJumpLastSkipReason { get; set; }
        public DateTime? MovementSimulatedJumpLastDecisionUtc { get; set; }
        public long MovementSimulatedJumpLastTick { get; set; }
        public int MovementSimulatedJumpPendingActionCount { get; set; }
        public string MovementSimulatedJumpRunningActionKind { get; set; }
        public bool MovementSimulatedJumpItemUseBridgeBusy { get; set; }
        public bool MovementSimulatedJumpTextInputFocused { get; set; }
        public string MovementSimulatedJumpTextInputReason { get; set; }
        public bool MovementSimulatedJumpHeld { get; set; }
        public bool MovementSimulatedJumpDownHeld { get; set; }
        public bool MovementSimulatedJumpPlayerControllable { get; set; }
        public bool MovementSimulatedJumpAvailableOpportunity { get; set; }
        public bool MovementSimulatedJumpGroundedOrSliding { get; set; }
        public bool MovementSimulatedJumpAerialWindow { get; set; }
        public bool MovementSimulatedJumpHasAirJump { get; set; }
        public bool MovementSimulatedJumpHasRocketJump { get; set; }
        public bool MovementSimulatedJumpHasWingFlight { get; set; }
        public bool MovementSimulatedJumpMountActive { get; set; }
        public bool MovementSimulatedJumpMountCanFlyKnown { get; set; }
        public bool MovementSimulatedJumpMountCanFly { get; set; }
        public string MovementSimulatedJumpCapabilitySummary { get; set; }
        public long MovementSimulatedJumpSubmittedCount { get; set; }
        public long MovementSimulatedJumpSkippedCount { get; set; }
        public bool MovementContinuousDashEnabled { get; set; }
        public string MovementContinuousDashMode { get; set; }
        public bool MovementContinuousDashLastTriggered { get; set; }
        public int MovementContinuousDashLastTriggerDirection { get; set; }
        public DateTime? MovementContinuousDashLastTriggerUtc { get; set; }
        public string MovementContinuousDashLastDecision { get; set; }
        public string MovementContinuousDashLastSkipReason { get; set; }
        public DateTime? MovementContinuousDashLastDecisionUtc { get; set; }
        public long MovementContinuousDashLastTick { get; set; }
        public int MovementContinuousDashPendingActionCount { get; set; }
        public string MovementContinuousDashRunningActionKind { get; set; }
        public bool MovementContinuousDashTextInputFocused { get; set; }
        public string MovementContinuousDashTextInputReason { get; set; }
        public bool MovementContinuousDashPlayerControllable { get; set; }
        public bool MovementContinuousDashLeftHeld { get; set; }
        public bool MovementContinuousDashRightHeld { get; set; }
        public int MovementContinuousDashHeldDirection { get; set; }
        public bool MovementContinuousDashHasDashAbility { get; set; }
        public string MovementContinuousDashAbilitySource { get; set; }
        public int MovementContinuousDashDashType { get; set; }
        public int MovementContinuousDashDashDelay { get; set; }
        public bool MovementContinuousDashCooldownReady { get; set; }
        public bool MovementContinuousDashMountActive { get; set; }
        public int MovementContinuousDashMountType { get; set; }
        public bool MovementContinuousDashMountCanDashKnown { get; set; }
        public bool MovementContinuousDashMountCanDash { get; set; }
        public string MovementContinuousDashCapabilitySummary { get; set; }
        public int MovementContinuousDashArmedDirection { get; set; }
        public string MovementContinuousDashArmedCancelReason { get; set; }
        public long MovementContinuousDashArmedCancelCount { get; set; }
        public bool MovementContinuousDashHookInstalled { get; set; }
        public string MovementContinuousDashHookMessage { get; set; }
        public bool MovementContinuousDashQueuedPulsePending { get; set; }
        public bool MovementContinuousDashLastPulseApplied { get; set; }
        public int MovementContinuousDashLastPulseDirection { get; set; }
        public DateTime? MovementContinuousDashLastPulseUtc { get; set; }
        public string MovementContinuousDashLastPulseMessage { get; set; }
        public bool MovementContinuousDashLastPulseWasFallback { get; set; }
        public string MovementContinuousDashLastPulseResetMessage { get; set; }
        public string MovementContinuousDashLastCompatError { get; set; }
        public long MovementContinuousDashSubmittedCount { get; set; }
        public long MovementContinuousDashSkippedCount { get; set; }
        public bool MovementTeleportCorrectionEnabled { get; set; }
        public bool MovementTeleportCorrectionHookInstalled { get; set; }
        public string MovementTeleportCorrectionHookMethod { get; set; }
        public string MovementTeleportCorrectionHookMessage { get; set; }
        public string MovementTeleportCorrectionLastDecision { get; set; }
        public string MovementTeleportCorrectionLastSkipReason { get; set; }
        public DateTime? MovementTeleportCorrectionLastDecisionUtc { get; set; }
        public int MovementTeleportCorrectionItemType { get; set; }
        public string MovementTeleportCorrectionItemName { get; set; }
        public double MovementTeleportCorrectionOriginalMouseWorldX { get; set; }
        public double MovementTeleportCorrectionOriginalMouseWorldY { get; set; }
        public int MovementTeleportCorrectionOriginalMouseScreenX { get; set; }
        public int MovementTeleportCorrectionOriginalMouseScreenY { get; set; }
        public double MovementTeleportCorrectionOriginalTopLeftX { get; set; }
        public double MovementTeleportCorrectionOriginalTopLeftY { get; set; }
        public bool MovementTeleportCorrectionOriginalSafe { get; set; }
        public int MovementTeleportCorrectionSearchRadiusPixels { get; set; }
        public int MovementTeleportCorrectionSearchStepPixels { get; set; }
        public int MovementTeleportCorrectionCandidateCount { get; set; }
        public int MovementTeleportCorrectionValidCandidateCount { get; set; }
        public double MovementTeleportCorrectionNearestCandidateDistance { get; set; }
        public double MovementTeleportCorrectionCorrectedTopLeftX { get; set; }
        public double MovementTeleportCorrectionCorrectedTopLeftY { get; set; }
        public double MovementTeleportCorrectionCorrectedMouseWorldX { get; set; }
        public double MovementTeleportCorrectionCorrectedMouseWorldY { get; set; }
        public int MovementTeleportCorrectionCorrectedMouseScreenX { get; set; }
        public int MovementTeleportCorrectionCorrectedMouseScreenY { get; set; }
        public bool MovementTeleportCorrectionMouseCaptureSucceeded { get; set; }
        public bool MovementTeleportCorrectionMouseApplySucceeded { get; set; }
        public bool MovementTeleportCorrectionMouseRestoreSucceeded { get; set; }
        public bool MovementTeleportCorrectionVanillaContinued { get; set; }
        public string MovementTeleportCorrectionLastCompatError { get; set; }
        public long MovementTeleportCorrectionAppliedCount { get; set; }
        public long MovementTeleportCorrectionSkippedCount { get; set; }
        public bool MovementSafeLandingEnabled { get; set; }
        public bool MovementSafeLandingLastTriggered { get; set; }
        public DateTime? MovementSafeLandingLastTriggerUtc { get; set; }
        public string MovementSafeLandingLastDecision { get; set; }
        public string MovementSafeLandingLastSkipReason { get; set; }
        public DateTime? MovementSafeLandingLastDecisionUtc { get; set; }
        public long MovementSafeLandingLastTick { get; set; }
        public int MovementSafeLandingPendingActionCount { get; set; }
        public string MovementSafeLandingRunningActionKind { get; set; }
        public bool MovementSafeLandingTextInputFocused { get; set; }
        public string MovementSafeLandingTextInputReason { get; set; }
        public bool MovementSafeLandingPlayerControllable { get; set; }
        public bool MovementSafeLandingDangerous { get; set; }
        public bool MovementSafeLandingRescueWindow { get; set; }
        public bool MovementSafeLandingAlreadySafe { get; set; }
        public string MovementSafeLandingSafeReason { get; set; }
        public bool MovementSafeLandingRawCreativeGodMode { get; set; }
        public bool MovementSafeLandingRawNoFallDmg { get; set; }
        public bool MovementSafeLandingRawSlowFall { get; set; }
        public bool MovementSafeLandingRawWet { get; set; }
        public bool MovementSafeLandingRawHoneyWet { get; set; }
        public bool MovementSafeLandingRawShimmering { get; set; }
        public bool MovementSafeLandingRawWebbed { get; set; }
        public bool MovementSafeLandingRawStoned { get; set; }
        public int MovementSafeLandingRawGrapCount { get; set; }
        public int MovementSafeLandingRawEquippedWingCount { get; set; }
        public bool MovementSafeLandingRawMountNoFallDamage { get; set; }
        public int MovementSafeLandingRawExtraFall { get; set; }
        public double MovementSafeLandingFallingSpeed { get; set; }
        public double MovementSafeLandingVelocityY { get; set; }
        public double MovementSafeLandingGravityDirection { get; set; }
        public bool MovementSafeLandingImpactFound { get; set; }
        public int MovementSafeLandingImpactDistancePixels { get; set; }
        public double MovementSafeLandingImpactTicks { get; set; }
        public double MovementSafeLandingEstimatedFallTiles { get; set; }
        public string MovementSafeLandingActiveCapabilitySummary { get; set; }
        public string MovementSafeLandingSelectedStrategyId { get; set; }
        public int MovementSafeLandingSelectedPriority { get; set; }
        public string MovementSafeLandingSelectedActionType { get; set; }
        public bool MovementSafeLandingHasFlyingCarpet { get; set; }
        public bool MovementSafeLandingHasFlyingCarpetAvailable { get; set; }
        public int MovementSafeLandingFlyingCarpetTime { get; set; }
        public bool MovementSafeLandingHasGravityGlobe { get; set; }
        public bool MovementSafeLandingHasGravityFlipOpportunity { get; set; }
        public bool MovementSafeLandingHasEquippedFlyingMount { get; set; }
        public bool MovementSafeLandingHasEquippedSafeMount { get; set; }
        public bool MovementSafeLandingHasEquippedGrapple { get; set; }
        public bool MovementSafeLandingHasInventoryGrapple { get; set; }
        public bool MovementSafeLandingHasTeleportRod { get; set; }
        public int MovementSafeLandingTeleportRodInventorySlot { get; set; }
        public int MovementSafeLandingTeleportRodItemType { get; set; }
        public bool MovementSafeLandingTeleportTargetKnown { get; set; }
        public int MovementSafeLandingTeleportTargetTileX { get; set; }
        public int MovementSafeLandingTeleportTargetTileY { get; set; }
        public double MovementSafeLandingTeleportTargetWorldX { get; set; }
        public double MovementSafeLandingTeleportTargetWorldY { get; set; }
        public bool MovementSafeLandingGravityRestorePending { get; set; }
        public double MovementSafeLandingGravityRestoreOriginalDirection { get; set; }
        public long MovementSafeLandingGravityRestorePendingTicks { get; set; }
        public string MovementSafeLandingGravityRestoreLastDecision { get; set; }
        public string MovementSafeLandingGravityRestoreLastSkipReason { get; set; }
        public string MovementSafeLandingConfigSummary { get; set; }
        public string MovementSafeLandingStageSummary { get; set; }
        public string MovementSafeLandingStrategyCatalogVersion { get; set; }
        public string MovementSafeLandingStrategyEvaluationSummary { get; set; }
        public string MovementSafeLandingCandidateSummary { get; set; }
        public string MovementSafeLandingSelectedPlanSummary { get; set; }
        public string MovementSafeLandingRejectedStrategiesSummary { get; set; }
        public string MovementSafeLandingPostApplyVerificationSummary { get; set; }
        public bool MovementSafeLandingLandingSurfaceKnown { get; set; }
        public float MovementSafeLandingLandingContactWorldX { get; set; }
        public float MovementSafeLandingLandingContactWorldY { get; set; }
        public int MovementSafeLandingLandingContactTileX { get; set; }
        public int MovementSafeLandingLandingContactTileY { get; set; }
        public string MovementSafeLandingLandingSurfaceKind { get; set; }
        public int MovementSafeLandingLandingSlopeType { get; set; }
        public string MovementSafeLandingLandingSlopeDirection { get; set; }
        public string MovementSafeLandingLandingContactSample { get; set; }
        public bool MovementSafeLandingLandingMovingIntoSlope { get; set; }
        public bool MovementSafeLandingLandingMovingWithSlope { get; set; }
        public string MovementSafeLandingLandingSurfaceSummary { get; set; }
        public float MovementSafeLandingGrappleHookSpeed { get; set; }
        public string MovementSafeLandingGrappleTargetSource { get; set; }
        public bool MovementSafeLandingGrappleTargetFromLandingSurface { get; set; }
        public float MovementSafeLandingGrappleTargetDistancePixels { get; set; }
        public float MovementSafeLandingGrappleHookVerticalSpeed { get; set; }
        public float MovementSafeLandingGrappleRelativeDownSpeed { get; set; }
        public float MovementSafeLandingGrappleRequiredLeadTicks { get; set; }
        public int MovementSafeLandingGrappleRequiredLeadPixels { get; set; }
        public bool MovementSafeLandingGrappleTooEarly { get; set; }
        public float MovementSafeLandingGrappleEstimatedTicksToTarget { get; set; }
        public bool MovementSafeLandingGrappleTooLate { get; set; }
        public bool MovementSafeLandingGrappleTooSlowForDownwardSurface { get; set; }
        public string MovementSafeLandingGrappleTimingSummary { get; set; }
        public float MovementSafeLandingEquippedGrappleShootSpeed { get; set; }
        public float MovementSafeLandingInventoryGrappleShootSpeed { get; set; }
        public int MovementSafeLandingEquippedGrappleProjectileType { get; set; }
        public int MovementSafeLandingInventoryGrappleProjectileType { get; set; }
        public float MovementSafeLandingMaxFallSpeed { get; set; }

        public string MovementSafeLandingRecoveryStateSummary { get; set; }
        public long MovementSafeLandingSubmittedCount { get; set; }
        public long MovementSafeLandingSkippedCount { get; set; }
        public long MovementSafeLandingFullAnalysisCount { get; set; }
        public long MovementSafeLandingCheapPrecheckSkipCount { get; set; }
        public long MovementSafeLandingLandingProbeCount { get; set; }
        public long MovementSafeLandingConfigSummaryCacheHitCount { get; set; }
        public long MovementSafeLandingConfigSummaryCacheMissCount { get; set; }
        public long MovementSafeLandingStageSummaryCacheHitCount { get; set; }
        public long MovementSafeLandingCheapSkipDiagnosticSuppressedCount { get; set; }
        public long MovementSafeLandingCheapSkipDiagnosticWrittenCount { get; set; }
        public string MovementSafeLandingCheapSkipLastReason { get; set; }
        public int MovementSafeLandingCheapSkipDiagnosticCadenceTicks { get; set; }
        public long MovementSafeLandingRecoverySummarySkippedCount { get; set; }
        public string MovementSafeLandingLastCompatError { get; set; }
        public string MovementSafeLandingCollisionFastPathStatus { get; set; }
        public bool MovementSafeLandingPlayerUpdateHookInstalled { get; set; }
        public string MovementSafeLandingPlayerUpdateHookMessage { get; set; }
        public bool MovementSafeLandingQueuedJumpPulseActive { get; set; }
        public string MovementSafeLandingQueuedJumpPulseStatus { get; set; }
        public string MovementSafeLandingQueuedJumpPulseApplySite { get; set; }
        public string LastGameStateInventoryProfile { get; set; }
        public string LastGameStateNpcProfile { get; set; }
        public string LastGameStateTileProfile { get; set; }

        public bool MovementSafeLandingTemporaryEquipmentApplied { get; set; }
        public int MovementSafeLandingTemporaryEquipmentPendingRestoreCount { get; set; }
        public int MovementSafeLandingTemporaryEquipmentPendingRestoreNoSpaceCount { get; set; }
        public string MovementSafeLandingTemporaryEquipmentLastDecision { get; set; }
        public string MovementSafeLandingTemporaryEquipmentLastSkipReason { get; set; }
        public string MovementSafeLandingTemporaryEquipmentSelectedCategory { get; set; }
        public string MovementSafeLandingTemporaryEquipmentSelectedSourceKind { get; set; }
        public int MovementSafeLandingTemporaryEquipmentSelectedSourceSlot { get; set; }
        public string MovementSafeLandingTemporaryEquipmentSelectedTargetKind { get; set; }
        public int MovementSafeLandingTemporaryEquipmentSelectedTargetSlot { get; set; }
        public int MovementSafeLandingTemporaryEquipmentSelectedItemType { get; set; }
        public int MovementSafeLandingTemporaryEquipmentSelectedMountType { get; set; }
        public bool CombatAutoFacingEnabled { get; set; }
        public string CombatAutoFacingLastDecision { get; set; }
        public string CombatAutoFacingLastSkipReason { get; set; }
        public DateTime? CombatAutoFacingLastDecisionUtc { get; set; }
        public long CombatAutoFacingLastTick { get; set; }
        public int CombatAutoFacingSelectedSlot { get; set; }
        public int CombatAutoFacingItemType { get; set; }
        public string CombatAutoFacingItemName { get; set; }
        public int CombatAutoFacingCurrentDirection { get; set; }
        public int CombatAutoFacingDesiredDirection { get; set; }
        public string CombatAutoFacingTargetSource { get; set; }
        public int CombatAutoFacingTargetWhoAmI { get; set; }
        public int CombatAutoFacingTargetType { get; set; }
        public string CombatAutoFacingTargetName { get; set; }
        public long CombatAutoFacingSubmittedCount { get; set; }
        public long CombatAutoFacingSkippedCount { get; set; }
        public string CombatPerfectRevolverLastDecision { get; set; }
        public string CombatPerfectRevolverLastSkipReason { get; set; }
        public DateTime? CombatPerfectRevolverLastDecisionUtc { get; set; }
        public bool CombatFlailComboEnabled { get; set; }
        public bool CombatFlailComboRightHeld { get; set; }
        public bool CombatFlailComboEligible { get; set; }
        public string CombatFlailComboLastDecision { get; set; }
        public string CombatFlailComboLastReason { get; set; }
        public DateTime? CombatFlailComboLastDecisionUtc { get; set; }
        public int CombatFlailComboItemType { get; set; }
        public int CombatFlailComboProjectileType { get; set; }
        public double CombatFlailComboProjectileAi0 { get; set; }
        public bool CombatFlailComboHitDetected { get; set; }
        public bool CombatFlailComboCollisionDetected { get; set; }
        public bool CombatFlailComboVanillaRightClickBlocked { get; set; }
        public bool CombatFlailComboUiBlocked { get; set; }
        public bool CombatFlailComboScopedPress { get; set; }
        public bool CombatFlailComboScopedRelease { get; set; }
        public bool CombatFlailComboRestoreOk { get; set; }
        public long CombatFlailComboAppliedCount { get; set; }
        public long CombatFlailComboSkippedCount { get; set; }
        public bool CombatPhasebladeQuickSwitchEnabled { get; set; }
        public bool CombatPhasebladeQuickSwitchRightHeld { get; set; }
        public bool CombatPhasebladeQuickSwitchEligible { get; set; }
        public string CombatPhasebladeQuickSwitchLastDecision { get; set; }
        public string CombatPhasebladeQuickSwitchLastReason { get; set; }
        public DateTime? CombatPhasebladeQuickSwitchLastDecisionUtc { get; set; }
        public int CombatPhasebladeQuickSwitchCurrentSlot { get; set; }
        public int CombatPhasebladeQuickSwitchNextSlot { get; set; }
        public int CombatPhasebladeQuickSwitchEligibleSlotCount { get; set; }
        public int CombatPhasebladeQuickSwitchIntervalTicks { get; set; }
        public bool CombatPhasebladeQuickSwitchScopedPress { get; set; }
        public bool CombatPhasebladeQuickSwitchScopedRelease { get; set; }
        public bool CombatPhasebladeQuickSwitchRestoreOk { get; set; }
        public long CombatPhasebladeQuickSwitchAppliedCount { get; set; }
        public long CombatPhasebladeQuickSwitchSkippedCount { get; set; }
        public string CombatItemCheckAutoClickerLastDecision { get; set; }
        public string CombatItemCheckAutoClickerLastReason { get; set; }
        public DateTime? CombatItemCheckAutoClickerLastDecisionUtc { get; set; }
        public int CombatItemCheckAutoClickerLastItemType { get; set; }
        public bool CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable { get; set; }
        public bool CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons { get; set; }
        public bool CombatItemCheckAutoClickerScopedPress { get; set; }
        public bool CombatItemCheckAutoClickerScopedRelease { get; set; }
        public bool CombatItemCheckAutoClickerRestored { get; set; }
        public long CombatItemCheckAutoClickerAppliedCount { get; set; }
        public long CombatItemCheckAutoClickerSkippedCount { get; set; }
        public string CombatMagicStringClickerLastDecision { get; set; }
        public string CombatMagicStringClickerLastSkipReason { get; set; }
        public DateTime? CombatMagicStringClickerLastDecisionUtc { get; set; }
        public bool CombatAutoBossDamageReportEnabled { get; set; }
        public string CombatAutoBossDamageReportLastDecision { get; set; }
        public string CombatAutoBossDamageReportLastReason { get; set; }
        public DateTime? CombatAutoBossDamageReportLastDecisionUtc { get; set; }
        public int CombatAutoBossDamageReportRecentAttemptCount { get; set; }
        public int CombatAutoBossDamageReportNewAttemptCount { get; set; }
        public int CombatAutoBossDamageReportLastAttemptKey { get; set; }
        public bool CombatAutoBossDamageReportLastSendAttempted { get; set; }
        public bool CombatAutoBossDamageReportLastSendSucceeded { get; set; }
        public string CombatAutoBossDamageReportLastFailureReason { get; set; }
        public long CombatAutoBossDamageReportSentCount { get; set; }
        public long CombatAutoBossDamageReportSkippedCount { get; set; }
        public bool AutoHealEnabled { get; set; }
        public bool AutoManaEnabled { get; set; }
        public bool AutoBuffEnabled { get; set; }
        public bool AutoNurseEnabled { get; set; }
        public bool AutoStationBuffEnabled { get; set; }
        public string AutoHealMode { get; set; }
        public string AutoManaMode { get; set; }
        public int AutoHealThresholdPercent { get; set; }
        public int AutoManaThresholdPercent { get; set; }
        public int AutoHealCooldownTicks { get; set; }
        public int AutoManaCooldownTicks { get; set; }
        public int AutoBuffCooldownTicks { get; set; }
        public string LastAutoHealResult { get; set; }
        public string LastAutoManaResult { get; set; }
        public string LastAutoBuffResult { get; set; }
        public string LastAutoNurseResult { get; set; }
        public string LastAutoStationBuffResult { get; set; }
        public long LastAutoHealTick { get; set; }
        public long LastAutoManaTick { get; set; }
        public long LastAutoBuffTick { get; set; }
        public long LastAutoNurseTick { get; set; }
        public long LastAutoStationBuffTick { get; set; }
        public long AutoStationBuffCooldownFastSkipCount { get; set; }
        public long AutoStationBuffActiveBuffFastSkipCount { get; set; }
        public long AutoStationBuffScanCount { get; set; }
        public long AutoStationBuffScanCacheHitCount { get; set; }
        public long AutoStationBuffScanCacheMissCount { get; set; }
        public int AutoStationBuffTilesVisitedLast { get; set; }
        public double AutoStationBuffLastScanMs { get; set; }
        public string AutoStationBuffTileFastPathStatus { get; set; }
        public string AutoStationBuffLastDecision { get; set; }
        public int LastAutoBuffCountBefore { get; set; }
        public int LastAutoBuffCountAfter { get; set; }
        public string QuickHealCapability { get; set; }
        public string QuickManaCapability { get; set; }
        public string QuickBuffCapability { get; set; }
        public string LastError { get; set; }

        public DiagnosticSnapshot()
        {
            UserCategoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            CodeDomainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SelectedItemName = string.Empty;
            RuntimeVersion = string.Empty;
            TestRunId = string.Empty;
            WorldGenDebugStatus = string.Empty;
            WorldGenDebugMessage = string.Empty;
            WorldGenDebugFieldOwner = string.Empty;
            DiagnosticInputGateStatus = string.Empty;
            DiagnosticInputSkipReason = string.Empty;
            LastGameStateReadError = string.Empty;
            ItemCheckHookMethod = string.Empty;
            PlayerWorldDeathHookMethod = string.Empty;
            PlayerWorldDeathHookMessage = string.Empty;
            PlayerWorldDeathLastRecordStatus = string.Empty;
            PlayerWorldDeathLastRecordMessage = string.Empty;
            PlayerWorldDeathLastEventId = string.Empty;
            PlayerWorldDeathLastPairId = string.Empty;
            PlayerWorldDeathMarkerLayerMessage = string.Empty;
            PlayerWorldDeathMarkerLastStatus = string.Empty;
            PlayerWorldDeathMarkerLastMessage = string.Empty;
            PlayerWorldDeathMarkerLastPairId = string.Empty;
            PlayerWorldDeathHistoryLastStatus = string.Empty;
            PlayerWorldDeathHistoryLastMessage = string.Empty;
            PlayerWorldDeathHistoryLastPairId = string.Empty;
            PlayerWorldPlaytimeLastStatus = string.Empty;
            PlayerWorldPlaytimeLastMessage = string.Empty;
            PlayerWorldPlaytimeLastPairId = string.Empty;
            PlayerWorldPlaytimeLastSkippedDeltaReason = string.Empty;
            PlayerWorldExplorationLastStatus = string.Empty;
            PlayerWorldExplorationLastMessage = string.Empty;
            PlayerWorldExplorationLastPairId = string.Empty;
            PlayerWorldExplorationScanMode = string.Empty;
            PlayerWorldExplorationControlState = string.Empty;
            PlayerWorldExplorationLastUserCommand = string.Empty;
            PlayerWorldExplorationAutoRescanDisabled = true;
            PlayerWorldMapMarkersLastStatus = string.Empty;
            PlayerWorldMapMarkersLastMessage = string.Empty;
            PlayerWorldMapMarkersLastPairId = string.Empty;
            PlayerWorldMapMarkersLastOperation = string.Empty;
            PlayerWorldMapMarkersLastUiAction = string.Empty;
            PlayerWorldMapMarkersLastJumpResult = string.Empty;
            MapMarkerLastBlockedReason = string.Empty;
            MapMarkerLastTransformRoute = string.Empty;
            MapMarkerLastRightClickTransformSource = string.Empty;
            MapMarkerLastRightClickFallbackReason = string.Empty;
            MapMarkerLastTransformGameUpdateCount = -1;
            MapMarkerLastRightClickTransformAgeUpdates = -1;
            MapMarkerPickerDrawRoute = string.Empty;
            MapMarkerPickerDrawSkippedReason = string.Empty;
            MapMarkerPickerLastCloseReason = string.Empty;
            MapMarkerTraceEventsPath = string.Empty;
            MapMarkerLastTraceEventType = string.Empty;
            MapMarkerLastTraceMarkerId = string.Empty;
            MapDirectionHintTargetScanCadenceTicks = -1L;
            MapRareCreatureDirectionStatus = string.Empty;
            MapRareCreatureDirectionMessage = string.Empty;
            MapRareCreatureDirectionGateReason = string.Empty;
            MapRareCreatureDirectionTargetWhoAmI = -1;
            MapRareCreatureDirectionTargetName = string.Empty;
            MapRareCreatureDirectionDistanceText = string.Empty;
            MapRareCreatureDirectionArrowGlyph = string.Empty;
            MapRareCreatureDirectionLabelLine1 = string.Empty;
            MapRareCreatureDirectionLabelLine2 = string.Empty;
            MapRareCreatureDirectionLastScanTick = -1L;
            MapRareCreatureDirectionLastScanAgeTicks = -1L;
            MapRareCreatureDirectionDrawStatus = string.Empty;
            MapTravellingMerchantDirectionStatus = string.Empty;
            MapTravellingMerchantDirectionMessage = string.Empty;
            MapTravellingMerchantDirectionTargetWhoAmI = -1;
            MapTravellingMerchantDirectionTargetName = string.Empty;
            MapTravellingMerchantDirectionDistanceText = string.Empty;
            MapTravellingMerchantDirectionLabelLine1 = string.Empty;
            MapTravellingMerchantDirectionLabelLine2 = string.Empty;
            MapTravellingMerchantDirectionLabelLine3 = string.Empty;
            MapTravellingMerchantDirectionTownLabel = string.Empty;
            MapTravellingMerchantDirectionTownLabelSource = string.Empty;
            MapTravellingMerchantDirectionTownLabelConfidence = string.Empty;
            MapTravellingMerchantDirectionMatchedPylonType = string.Empty;
            MapTravellingMerchantDirectionMatchedPylonDistanceTiles = -1d;
            MapTravellingMerchantDirectionLastScanTick = -1L;
            MapTravellingMerchantDirectionLastScanAgeTicks = -1L;
            MapTravellingMerchantDirectionDrawStatus = string.Empty;
            PlayerWorldFootprintsLastStatus = string.Empty;
            PlayerWorldFootprintsLastDecision = string.Empty;
            PlayerWorldFootprintsLastMessage = string.Empty;
            PlayerWorldFootprintsLastPairId = string.Empty;
            PlayerWorldFootprintsLastFlushStatus = string.Empty;
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
            MapFootprintsPlaybackLastInteraction = string.Empty;
            MapFootprintsPlaybackPrefixHitTarget = string.Empty;
            MapFootprintsPlaybackPrefixMouseReadMode = string.Empty;
            MapFootprintsPlaybackPrefixMouseX = -1;
            MapFootprintsPlaybackPrefixMouseY = -1;
            MapFootprintsPlaybackDrawHitTarget = string.Empty;
            MapFootprintsPlaybackDrawMouseReadMode = string.Empty;
            MapFootprintsPlaybackDrawMouseX = -1;
            MapFootprintsPlaybackDrawMouseY = -1;
            RunningActionKind = string.Empty;
            RunningActionSource = string.Empty;
            RunningActionStatus = string.Empty;
            LastActionKind = string.Empty;
            LastActionStatus = "none";
            LastActionMessage = string.Empty;
            LastActionUserMessage = string.Empty;
            LastActionResultCode = string.Empty;
            RecentActionLine1 = string.Empty;
            RecentActionLine2 = string.Empty;
            RecentActionLine3 = string.Empty;
            ActionQueueRunningChannels = string.Empty;
            ActionQueueOccupiedChannels = string.Empty;
            ActionQueueBridgeBusyChannels = string.Empty;
            LastGameStateInventoryProfile = string.Empty;
            LastGameStateNpcProfile = string.Empty;
            LastGameStateTileProfile = string.Empty;
            ActionQueueRunningLeaseChannels = string.Empty;
            ActionQueueLastChannelDecision = string.Empty;
            ActionQueueLastChannelBlockedReason = string.Empty;
            ActionQueueChannelOwnerSummary = string.Empty;
            ActionQueueBridgeBusySummary = string.Empty;
            ActionQueuePendingChannelSummary = string.Empty;
            ActionQueuePendingOwnerSummary = string.Empty;
            ActionQueueLastAdmissionStatus = string.Empty;
            ActionQueueLastAdmissionDecision = string.Empty;
            ActionQueueLastAdmissionReason = string.Empty;
            ActionQueueLastAdmissionKind = string.Empty;
            ActionQueueLastAdmissionSource = string.Empty;
            ActionQueueLastAdmissionScenario = string.Empty;
            ActionQueueLastAdmissionKey = string.Empty;
            ActionQueueLastAdmissionRequiredChannels = string.Empty;
            ActionQueueLastAdmissionBlockingChannels = string.Empty;
            ActionQueueLastAdmissionConflictChannels = string.Empty;
            ActionQueueLastAdmissionPendingConflictSummary = string.Empty;
            ActionQueueLastAdmissionRunningConflictSummary = string.Empty;
            ActionQueueLastAdmissionBridgeBusySummary = string.Empty;
            ActionQueueLastAdmissionOwnerSummary = string.Empty;
            ActionQueueLastAdmissionSupersededRequestId = string.Empty;
            ActionQueueLastAdmissionCoalescedRequestId = string.Empty;
            SchedulerLastSelectedRequest = string.Empty;
            SchedulerLastSupersededRequest = string.Empty;
            SchedulerLastFairnessBucket = string.Empty;
            WorldAutomationLastWinner = string.Empty;
            WorldAutomationFairnessDebt = string.Empty;
            ActionQueueCleanupLeaseChannels = string.Empty;
            ActionQueueLastCleanupOwner = string.Empty;
            ActionQueueLastCleanupReason = string.Empty;
            ActionQueueLastDirectEnqueueKind = string.Empty;
            ActionQueueLastDirectEnqueueSource = string.Empty;
            ActionQueueLastDirectEnqueueScenario = string.Empty;
            ActionQueueLastDirectEnqueueAdmissionKey = string.Empty;
            ActionQueueLastDirectEnqueueRequiredChannels = string.Empty;
            ActionQueueLastPendingExpiryReason = string.Empty;
            ItemUseBridgeLastStatus = string.Empty;
            ItemUseBridgeLastMessage = string.Empty;
            ItemUseBridgeLastRequestId = string.Empty;
            ItemUseBridgePendingRequestId = string.Empty;
            ItemCheckWriterOwner = string.Empty;
            ItemCheckWriterOwnerRequestId = string.Empty;
            ItemCheckWriterPhase = string.Empty;
            ItemCheckWriterDecisionReason = string.Empty;
            ItemCheckWriterBlockedCandidates = string.Empty;
            LastDiagnosticHotkey = string.Empty;
            LastDiagnosticHotkeyMessage = string.Empty;
            ActionEventsPath = string.Empty;
            LastDiagnosticSourceKind = string.Empty;
            LastDiagnosticButtonId = string.Empty;
            LastDiagnosticButtonLabel = string.Empty;
            LastButtonResultCode = string.Empty;
            LastButtonMessage = string.Empty;
            UiPrimitiveRendererLastMessage = string.Empty;
            UiMouseReadLastMessage = string.Empty;
            UiMouseCaptureLastMessage = string.Empty;
            UiClickSuppressionMode = string.Empty;
            LastMouseX = -1;
            LastMouseY = -1;
            TerrariaMouseX = -1;
            TerrariaMouseY = -1;
            OsClientMouseX = -1;
            OsClientMouseY = -1;
            UiScale = 1d;
            MouseReadMode = string.Empty;
            MouseReadLastError = string.Empty;
            HitTestMode = string.Empty;
            HitTestCandidateSummary = string.Empty;
            HitTestX = -1;
            HitTestY = -1;
            ClickSource = string.Empty;
            LastButtonHitTestMode = string.Empty;
            LastButtonClickSource = string.Empty;
            HoveredButtonId = string.Empty;
            HoveredButtonLabel = string.Empty;
            HoveredButtonHint = string.Empty;
            HoveredButtonVisualX = -1;
            HoveredButtonVisualY = -1;
            HoveredButtonHitX = -1;
            HoveredButtonHitY = -1;
            LegacyMainUiLastF5HotkeyDecision = string.Empty;
            LegacyMainUiLastF5HotkeyReason = string.Empty;
            LegacyImePanelDiagnosticMessage = string.Empty;
            LegacyImePanelLastStatus = string.Empty;
            LegacyImePanelLastMessage = string.Empty;
            LegacyImePanelCadenceSummary = string.Empty;
            DiagnosticTestSlotItemName = string.Empty;
            DiagnosticTestSlotSuitability = string.Empty;
            DiagnosticTestSlotHint = string.Empty;
            QuickActionLastKind = string.Empty;
            QuickActionLastStatus = string.Empty;
            QuickActionLastResultCode = string.Empty;
            QuickActionLastMessage = string.Empty;
            MouseTargetLastStatus = string.Empty;
            MouseTargetLastResultCode = string.Empty;
            MouseTargetLastMessage = string.Empty;
            LastSlowestStageName = string.Empty;
            LastSlowestOperationName = string.Empty;
            PerformanceEventsPath = string.Empty;
            LastPerformanceHitchReason = string.Empty;
            LastPerformanceHitchSlowestStageName = string.Empty;
            LastPerformanceHitchSlowestOperationName = string.Empty;
            LastPerformanceOperationScenario = string.Empty;
            LastPerformanceOperationReason = string.Empty;
            LastPerformanceOperationOwnerSummary = string.Empty;
            ReflectionCacheLastMissKey = string.Empty;
            ReflectionCacheLastError = string.Empty;
            PlayerTypeName = string.Empty;
            LastInputCompatError = string.Empty;
            ConfigLastSaveSummary = string.Empty;
            ConfigLastSaveAppSettingsPath = string.Empty;
            ConfigLastSaveAppSettingsError = string.Empty;
            ConfigLastSaveFeatureSettingsPath = string.Empty;
            ConfigLastSaveFeatureSettingsError = string.Empty;
            ConfigLastSaveHotkeySettingsPath = string.Empty;
            ConfigLastSaveHotkeySettingsError = string.Empty;
            AutoStackLastDecision = string.Empty;
            AutoStackLastInventorySignature = string.Empty;
            AutoStackLastPendingItemIds = string.Empty;
            AutoStackLastDetectedItemIds = string.Empty;
            AutoStackLastPendingChangeTick = -1;
            AutoStackLastPendingClearReason = string.Empty;
            AutoStackPendingTransactionState = string.Empty;
            AutoStackLastSubmitRequestId = string.Empty;
            AutoStackLastResult = string.Empty;
            AutoStackLastUnverifiedReason = string.Empty;
            AutoStackInventoryTransactionSlots = string.Empty;
            AutoStackInventoryTransactionBlockingReason = string.Empty;
            AutoStackActionResultDeliveryMode = string.Empty;
            AutoSellLastDecision = string.Empty;
            AutoSellLastInventorySignature = string.Empty;
            AutoSellLastItemIds = string.Empty;
            AutoDiscardLastDecision = string.Empty;
            AutoDiscardLastInventorySignature = string.Empty;
            AutoDiscardLastItemIds = string.Empty;
            QuickReforgeLastDecision = string.Empty;
            QuickReforgeLastTargetPrefixes = string.Empty;
            QuickReforgeLastMatchedPrefix = string.Empty;
            AutoTaxCollectLastDecision = string.Empty;
            AutoTaxCollectTargetNpcIndex = -1;
            AutoTaxCollectTargetWhoAmI = -1;
            AutoTaxCollectTargetName = string.Empty;
            AutoTaxCollectLastRequestId = string.Empty;
            AutoCaptureCritterLastDecision = string.Empty;
            AutoCaptureCritterBugNetSlot = -1;
            AutoCaptureCritterTargetNpcIndex = -1;
            AutoCaptureCritterFishingProtectionState = string.Empty;
            AutoMiningLastDecision = string.Empty;
            AutoMiningLastHotkey = string.Empty;
            AutoMiningLastHotkeyResultCode = string.Empty;
            AutoMiningLastHotkeyBlockedReason = string.Empty;
            AutoHarvestLastDecision = string.Empty;
            AutoHarvestLastAction = string.Empty;
            AutoHarvestToolSlot = -1;
            AutoHarvestTargetTileX = -1;
            AutoHarvestTargetTileY = -1;
            QuickBagOpenLastDecision = string.Empty;
            QuickBagOpenBagSlot = -1;
            QuickBagOpenBagItemName = string.Empty;
            AutoDepositCoinsLastDecision = string.Empty;
            AutoDepositCoinsLastInventorySignature = string.Empty;
            AutoDepositCoinsLastCoinItemIds = string.Empty;
            AutoExtractinatorLastDecision = string.Empty;
            AutoExtractinatorItemSlot = -1;
            AutoExtractinatorTileX = -1;
            AutoExtractinatorTileY = -1;
            KeepFavoritedLastDecision = string.Empty;
            KeepFavoritedSlot = -1;
            KeepFavoritedSignature = string.Empty;
            InformationEnabledSummary = string.Empty;
            InformationLastSkipReason = string.Empty;
            InformationChestAlwaysLastDirtyReason = string.Empty;
            InformationChestAlwaysTypedTileFastPathStatus = string.Empty;
            InformationWorldContextProfile = string.Empty;
            SearchChestLocatorOverlaySnapshotStatus = string.Empty;
            SearchChestLocatorOverlaySkipReason = string.Empty;
            SearchChestLocatorOverlayRecentElapsedBucket = string.Empty;
            SearchChestLocatorSectionRequestStatus = string.Empty;
            SearchChestLocatorSectionRequestFailureReason = string.Empty;
            SearchChestLocatorSectionRequestSectionKey = string.Empty;
            MapQuickAnnouncementLastResultCode = string.Empty;
            MapQuickAnnouncementLastTargetKind = string.Empty;
            MapQuickAnnouncementLastTargetName = string.Empty;
            MapQuickAnnouncementLastTargetSummary = string.Empty;
            MapQuickAnnouncementLastResolveDetail = string.Empty;
            MapQuickAnnouncementLastTargetSource = string.Empty;
            MapQuickAnnouncementLastUiHoverSource = string.Empty;
            MapQuickAnnouncementLastUiHoverState = string.Empty;
            MapQuickAnnouncementLastUiHoverHookStatus = string.Empty;
            MapQuickAnnouncementLastPendingState = string.Empty;
            MapQuickAnnouncementLastHoverCacheAgeUpdates = -1;
            MapQuickAnnouncementLastPlacementLookupSource = string.Empty;
            MapQuickAnnouncementLastFallbackReason = string.Empty;
            MapQuickAnnouncementLastFailureReason = string.Empty;
            MapQuickAnnouncementLastHotkeySummary = string.Empty;
            MapQuickAnnouncementLastInputConsumeResult = string.Empty;
            MapQuickAnnouncementLastVisibilityVerdict = string.Empty;
            MapQuickAnnouncementLastVisibilityReason = string.Empty;
            MapQuickAnnouncementLastVisibleLayers = string.Empty;
            MapQuickAnnouncementLastBlockedLayers = string.Empty;
            MapQuickAnnouncementLastEchoGate = string.Empty;
            MapQuickAnnouncementLastVisibilityUnavailableReason = string.Empty;
            BlueprintHandheldActionBarBlockedReason = string.Empty;
            BlueprintHandheldActionBarLastAction = string.Empty;
            BlueprintHandheldActionBarLastResultCode = string.Empty;
            BlueprintHandheldActionBarHoveredButtonId = string.Empty;
            BlueprintHandheldActionBarPressedButtonId = string.Empty;
            BlueprintHandheldActionBarLastMouseReadMode = string.Empty;
            BlueprintHandheldActionBarLastOwnershipReason = string.Empty;
            BlueprintHandheldActionBarLastInputTrace = string.Empty;
            BlueprintHandheldActionBarLastOwnershipTrace = string.Empty;
            BlueprintWorldOverlayLastInputTrace = string.Empty;
            BlueprintCreationPrefixWorldOverlayInputTrace = string.Empty;
            BlueprintCreationAfterPlayerInputWorldOverlayInputTrace = string.Empty;
            BlueprintCreationLastClearReasonTrace = string.Empty;
            BlueprintMirrorLastStatus = string.Empty;
            BlueprintMirrorLastMessage = string.Empty;
            BlueprintMirrorMode = string.Empty;
            BlueprintMirrorTemplateId = string.Empty;
            BlueprintMirrorTemplateName = string.Empty;
            BlueprintMirrorBlockedReason = string.Empty;
            BlueprintMirrorWarningReason = string.Empty;
            BlueprintDiagnosticsTemplateReadStatus = string.Empty;
            BlueprintDiagnosticsTemplateReadMessage = string.Empty;
            BlueprintPerformanceLastScenario = string.Empty;
            BlueprintProjectionLastStatus = string.Empty;
            BlueprintProjectionLastMessage = string.Empty;
            BlueprintProjectionWorldPairKey = string.Empty;
            BlueprintProjectionWorldKey = string.Empty;
            BlueprintMaterialsLastStatus = string.Empty;
            BlueprintMaterialsLastMessage = string.Empty;
            BlueprintMaterialsWorldPairKey = string.Empty;
            BlueprintMaterialsWorldKey = string.Empty;
            BlueprintMaterialsProjectionStatus = string.Empty;
            BlueprintMaterialsInventoryReadStatus = string.Empty;
            BlueprintMaterialsInventoryReadMessage = string.Empty;
            BlueprintEraseRegionTargetInstanceId = string.Empty;
            BlueprintEraseRegionTargetInstanceName = string.Empty;
            BlueprintEraseRegionWorldPairKey = string.Empty;
            BlueprintEraseRegionWorldKey = string.Empty;
            BlueprintEraseRegionLastStatus = string.Empty;
            BlueprintEraseRegionLastMessage = string.Empty;
            BlueprintEraseRegionLastInputOwner = string.Empty;
            BlueprintAutoPlacementLastStatus = string.Empty;
            BlueprintAutoPlacementLastMessage = string.Empty;
            BlueprintAutoPlacementWorldPairKey = string.Empty;
            BlueprintAutoPlacementWorldKey = string.Empty;
            BlueprintAutoPlacementProjectionStatus = string.Empty;
            BlueprintAutoPlacementSelectedInstanceId = string.Empty;
            BlueprintAutoPlacementSelectedInstanceName = string.Empty;
            BlueprintAutoPlacementSelectedLayerKind = string.Empty;
            BlueprintAutoPlacementSelectedReplacementCategory = string.Empty;
            BlueprintAutoPlacementLastAdmissionStatus = string.Empty;
            BlueprintAutoPlacementLastAdmissionReason = string.Empty;
            BlueprintAutoPlacementLastAdmissionKey = string.Empty;
            BlueprintAutoPlacementLastRequestId = string.Empty;
            BlueprintAutoPlacementLastResultCode = string.Empty;
            BlueprintAutoPlacementLastFailureReason = string.Empty;
            FishingLastDecision = string.Empty;
            FishingLastSkipReason = string.Empty;
            FishingAutoEquipmentLastDecision = string.Empty;
            FishingAutoEquipmentLastSkipReason = string.Empty;
            FishingAutomationDispatchReason = string.Empty;
            FishingTickSubpathLast = string.Empty;
            FishingFilterMode = string.Empty;
            FishingFilterMatchMode = string.Empty;
            FishingFilterCatchKind = string.Empty;
            FishingFilterCatchName = string.Empty;
            FishingFilterDecision = string.Empty;
            FishingFilterDecisionReason = string.Empty;
            FishingFilterMatchedRule = string.Empty;
            MovementSimulatedJumpLastDecision = string.Empty;
            MovementSimulatedJumpLastSkipReason = string.Empty;
            MovementSimulatedJumpRunningActionKind = string.Empty;
            MovementSimulatedJumpTextInputReason = string.Empty;
            MovementSimulatedJumpCapabilitySummary = string.Empty;
            MovementContinuousDashMode = string.Empty;
            MovementContinuousDashLastDecision = string.Empty;
            MovementContinuousDashLastSkipReason = string.Empty;
            MovementContinuousDashRunningActionKind = string.Empty;
            MovementContinuousDashTextInputReason = string.Empty;
            MovementContinuousDashAbilitySource = string.Empty;
            MovementContinuousDashCapabilitySummary = string.Empty;
            MovementContinuousDashArmedCancelReason = string.Empty;
            MovementContinuousDashHookMessage = string.Empty;
            MovementContinuousDashLastPulseMessage = string.Empty;
            MovementContinuousDashLastPulseResetMessage = string.Empty;
            MovementContinuousDashLastCompatError = string.Empty;
            MovementTeleportCorrectionHookMethod = string.Empty;
            MovementTeleportCorrectionHookMessage = string.Empty;
            MovementTeleportCorrectionLastDecision = string.Empty;
            MovementTeleportCorrectionLastSkipReason = string.Empty;
            MovementTeleportCorrectionItemName = string.Empty;
            MovementTeleportCorrectionLastCompatError = string.Empty;
            MovementTeleportCorrectionOriginalMouseScreenX = -1;
            MovementTeleportCorrectionOriginalMouseScreenY = -1;
            MovementTeleportCorrectionCorrectedMouseScreenX = -1;
            MovementTeleportCorrectionCorrectedMouseScreenY = -1;
            MovementSafeLandingLastDecision = string.Empty;
            MovementSafeLandingLastSkipReason = string.Empty;
            MovementSafeLandingRunningActionKind = string.Empty;
            MovementSafeLandingTextInputReason = string.Empty;
            MovementSafeLandingSafeReason = string.Empty;
            MovementSafeLandingImpactDistancePixels = -1;
            MovementSafeLandingImpactTicks = -1d;
            MovementSafeLandingActiveCapabilitySummary = string.Empty;
            MovementSafeLandingSelectedStrategyId = string.Empty;
            MovementSafeLandingSelectedPriority = -1;
            MovementSafeLandingSelectedActionType = string.Empty;
            MovementSafeLandingTeleportRodInventorySlot = -1;
            MovementSafeLandingTeleportTargetTileX = -1;
            MovementSafeLandingTeleportTargetTileY = -1;
            MovementSafeLandingConfigSummary = string.Empty;
            MovementSafeLandingStageSummary = string.Empty;
            MovementSafeLandingStrategyCatalogVersion = string.Empty;
            MovementSafeLandingStrategyEvaluationSummary = string.Empty;
            MovementSafeLandingCandidateSummary = string.Empty;
            MovementSafeLandingSelectedPlanSummary = string.Empty;
            MovementSafeLandingRejectedStrategiesSummary = string.Empty;
            MovementSafeLandingPostApplyVerificationSummary = string.Empty;
            MovementSafeLandingRecoveryStateSummary = string.Empty;
            MovementSafeLandingCheapSkipLastReason = string.Empty;
            MovementSafeLandingLastCompatError = string.Empty;
            MovementSafeLandingCollisionFastPathStatus = string.Empty;
            MovementSafeLandingPlayerUpdateHookMessage = string.Empty;
            MovementSafeLandingQueuedJumpPulseStatus = string.Empty;
            MovementSafeLandingQueuedJumpPulseApplySite = string.Empty;
            LastGameStateInventoryProfile = string.Empty;
            LastGameStateNpcProfile = string.Empty;
            LastGameStateTileProfile = string.Empty;

            FishingAutoStoreLastMode = string.Empty;
            FishingAutoStoreLastInventorySignature = string.Empty;
            FishingAutoStoreLastPendingItemIds = string.Empty;
            FishingAutoStoreLastDiagnosticMessage = string.Empty;
            FishingFilterSkipRequestId = string.Empty;
            FishingFilterSkipLastResult = string.Empty;
            FishingFilterSkipRestoreFailureReason = string.Empty;
            FishingCurrentBobberIdentity = -1;
            FishingLastProcessedHookIdentity = -1;
            FishingFilterSkipTemporarySlot = -1;
            FishingOriginalLoadoutIndex = -1;
            FishingTargetLoadoutIndex = -1;
            CombatAutoFacingLastDecision = string.Empty;
            CombatAutoFacingLastSkipReason = string.Empty;
            CombatAutoFacingItemName = string.Empty;
            CombatAutoFacingTargetSource = string.Empty;
            CombatAutoFacingTargetName = string.Empty;
            CombatPerfectRevolverLastDecision = string.Empty;
            CombatPerfectRevolverLastSkipReason = string.Empty;
            CombatFlailComboLastDecision = string.Empty;
            CombatFlailComboLastReason = string.Empty;
            CombatFlailComboRestoreOk = true;
            CombatPhasebladeQuickSwitchLastDecision = string.Empty;
            CombatPhasebladeQuickSwitchLastReason = string.Empty;
            CombatPhasebladeQuickSwitchCurrentSlot = -1;
            CombatPhasebladeQuickSwitchNextSlot = -1;
            CombatPhasebladeQuickSwitchRestoreOk = true;
            CombatItemCheckAutoClickerLastDecision = string.Empty;
            CombatItemCheckAutoClickerLastReason = string.Empty;
            CombatMagicStringClickerLastDecision = string.Empty;
            CombatMagicStringClickerLastSkipReason = string.Empty;
            CombatAutoBossDamageReportLastDecision = string.Empty;
            CombatAutoBossDamageReportLastReason = string.Empty;
            CombatAutoBossDamageReportLastFailureReason = string.Empty;
            LastAutoHealResult = string.Empty;
            LastAutoManaResult = string.Empty;
            LastAutoBuffResult = string.Empty;
            LastAutoNurseResult = string.Empty;
            LastAutoStationBuffResult = string.Empty;
            AutoStationBuffTileFastPathStatus = string.Empty;
            AutoStationBuffLastDecision = string.Empty;
            AutoHealMode = string.Empty;
            AutoManaMode = string.Empty;
            QuickHealCapability = "UnknownUntilAttempted";
            QuickManaCapability = "UnknownUntilAttempted";
            QuickBuffCapability = "UnknownUntilAttempted";
        }
    }
}
