using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JueMingZ.Diagnostics
{
    public static partial class DiagnosticSnapshotWriter
    {
        // JSON serialization preserves field names used in user diagnostics and stays append-free.
        private static string ToJson(DiagnosticSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            // Runtime-snapshot field names are a user-return contract. This
            // writer serializes prepared values only and must not trigger reads.
            Append(builder, "Loaded", snapshot.Loaded, true);
            Append(builder, "Version", snapshot.Version, true);
            Append(builder, "RuntimeVersion", string.IsNullOrWhiteSpace(snapshot.RuntimeVersion) ? snapshot.Version : snapshot.RuntimeVersion, true);
            Append(builder, "TestRunId", snapshot.TestRunId, true);
            Append(builder, "ProcessName", snapshot.ProcessName, true);
            Append(builder, "BaseDirectory", snapshot.BaseDirectory, true);
            Append(builder, "LogDirectory", snapshot.LogDirectory, true);
            Append(builder, "TerrariaDetected", snapshot.TerrariaDetected, true);
            Append(builder, "TerrariaVersion", snapshot.TerrariaVersion, true);
            Append(builder, "NetModeDescription", snapshot.NetModeDescription, true);
            Append(builder, "UpdateCount", snapshot.UpdateCount, true);
            Append(builder, "LateBootstrapCompleted", snapshot.LateBootstrapCompleted, true);
            Append(builder, "SafeBootstrapStarted", snapshot.SafeBootstrapStarted, true);
            Append(builder, "HarmonyLoaded", snapshot.HarmonyLoaded, true);
            Append(builder, "SafeBootstrapHookInstalled", snapshot.SafeBootstrapHookInstalled, true);
            Append(builder, "HookUpdateInstalled", snapshot.HookUpdateInstalled, true);
            Append(builder, "DrawHookInstalled", snapshot.DrawHookInstalled, true);
            Append(builder, "InterfaceLayerHookInstalled", snapshot.InterfaceLayerHookInstalled, true);
            Append(builder, "ItemCheckHookInstalled", snapshot.ItemCheckHookInstalled, true);
            Append(builder, "ItemCheckHookMethod", snapshot.ItemCheckHookMethod, true);
            Append(builder, "GoblinExecutionHookInstalled", snapshot.GoblinExecutionHookInstalled, true);
            Append(builder, "GoblinExecutionHookMethod", snapshot.GoblinExecutionHookMethod, true);
            Append(builder, "PlayerWorldDeathHookInstalled", snapshot.PlayerWorldDeathHookInstalled, true);
            Append(builder, "PlayerWorldDeathHookMethod", snapshot.PlayerWorldDeathHookMethod, true);
            Append(builder, "PlayerWorldDeathHookMessage", snapshot.PlayerWorldDeathHookMessage, true);
            Append(builder, "PlayerWorldDeathLastRecordStatus", snapshot.PlayerWorldDeathLastRecordStatus, true);
            Append(builder, "PlayerWorldDeathLastRecordMessage", snapshot.PlayerWorldDeathLastRecordMessage, true);
            Append(builder, "PlayerWorldDeathLastEventId", snapshot.PlayerWorldDeathLastEventId, true);
            Append(builder, "PlayerWorldDeathLastPairId", snapshot.PlayerWorldDeathLastPairId, true);
            Append(builder, "PlayerWorldDeathLastDeathCount", snapshot.PlayerWorldDeathLastDeathCount, true);
            Append(builder, "PlayerWorldDeathHistoryReadFailed", snapshot.PlayerWorldDeathHistoryReadFailed, true);
            Append(builder, "PlayerWorldDeathMarkerLayerInstalled", snapshot.PlayerWorldDeathMarkerLayerInstalled, true);
            Append(builder, "PlayerWorldDeathMarkerLayerMessage", snapshot.PlayerWorldDeathMarkerLayerMessage, true);
            Append(builder, "PlayerWorldDeathMarkerLastStatus", snapshot.PlayerWorldDeathMarkerLastStatus, true);
            Append(builder, "PlayerWorldDeathMarkerLastMessage", snapshot.PlayerWorldDeathMarkerLastMessage, true);
            Append(builder, "PlayerWorldDeathMarkerLastPairId", snapshot.PlayerWorldDeathMarkerLastPairId, true);
            Append(builder, "PlayerWorldDeathMarkerCachedCount", snapshot.PlayerWorldDeathMarkerCachedCount, true);
            Append(builder, "PlayerWorldDeathMarkerDrawnCount", snapshot.PlayerWorldDeathMarkerDrawnCount, true);
            Append(builder, "PlayerWorldDeathMarkerCulledByLimit", snapshot.PlayerWorldDeathMarkerCulledByLimit, true);
            Append(builder, "PlayerWorldDeathMarkerHistoryReadFailed", snapshot.PlayerWorldDeathMarkerHistoryReadFailed, true);
            Append(builder, "PlayerWorldDeathMarkerLastDrawUtc", FormatDate(snapshot.PlayerWorldDeathMarkerLastDrawUtc), true);
            Append(builder, "PlayerWorldDeathHistoryLastStatus", snapshot.PlayerWorldDeathHistoryLastStatus, true);
            Append(builder, "PlayerWorldDeathHistoryLastMessage", snapshot.PlayerWorldDeathHistoryLastMessage, true);
            Append(builder, "PlayerWorldDeathHistoryLastPairId", snapshot.PlayerWorldDeathHistoryLastPairId, true);
            Append(builder, "PlayerWorldDeathHistoryDeathCount", snapshot.PlayerWorldDeathHistoryDeathCount, true);
            Append(builder, "PlayerWorldDeathHistoryTotalEventCount", snapshot.PlayerWorldDeathHistoryTotalEventCount, true);
            Append(builder, "PlayerWorldDeathHistoryPageIndex", snapshot.PlayerWorldDeathHistoryPageIndex, true);
            Append(builder, "PlayerWorldDeathHistoryPageCount", snapshot.PlayerWorldDeathHistoryPageCount, true);
            Append(builder, "PlayerWorldDeathHistorySummaryReadFailed", snapshot.PlayerWorldDeathHistorySummaryReadFailed, true);
            Append(builder, "PlayerWorldDeathHistoryPageReadFailed", snapshot.PlayerWorldDeathHistoryPageReadFailed, true);
            Append(builder, "PlayerWorldDeathHistoryLastReadUtc", FormatDate(snapshot.PlayerWorldDeathHistoryLastReadUtc), true);
            Append(builder, "PlayerWorldPlaytimeLastStatus", snapshot.PlayerWorldPlaytimeLastStatus, true);
            Append(builder, "PlayerWorldPlaytimeLastMessage", snapshot.PlayerWorldPlaytimeLastMessage, true);
            Append(builder, "PlayerWorldPlaytimeLastPairId", snapshot.PlayerWorldPlaytimeLastPairId, true);
            Append(builder, "PlayerWorldPlaytimeTotalGameTicks", snapshot.PlayerWorldPlaytimeTotalGameTicks, true);
            Append(builder, "PlayerWorldPlaytimeWholeDayCount", snapshot.PlayerWorldPlaytimeWholeDayCount, true);
            Append(builder, "PlayerWorldPlaytimeReadFailed", snapshot.PlayerWorldPlaytimeReadFailed, true);
            Append(builder, "PlayerWorldPlaytimeWriteFailed", snapshot.PlayerWorldPlaytimeWriteFailed, true);
            Append(builder, "PlayerWorldPlaytimeLastDeltaGameTicks", snapshot.PlayerWorldPlaytimeLastDeltaGameTicks, true);
            Append(builder, "PlayerWorldPlaytimeLastSkippedDeltaReason", snapshot.PlayerWorldPlaytimeLastSkippedDeltaReason, true);
            Append(builder, "PlayerWorldPlaytimeLastSampleUtc", FormatDate(snapshot.PlayerWorldPlaytimeLastSampleUtc), true);
            Append(builder, "PlayerWorldPlaytimeLastWriteUtc", FormatDate(snapshot.PlayerWorldPlaytimeLastWriteUtc), true);
            Append(builder, "PlayerWorldExplorationLastStatus", snapshot.PlayerWorldExplorationLastStatus, true);
            Append(builder, "PlayerWorldExplorationLastMessage", snapshot.PlayerWorldExplorationLastMessage, true);
            Append(builder, "PlayerWorldExplorationLastPairId", snapshot.PlayerWorldExplorationLastPairId, true);
            Append(builder, "PlayerWorldExplorationWorldWidth", snapshot.PlayerWorldExplorationWorldWidth, true);
            Append(builder, "PlayerWorldExplorationWorldHeight", snapshot.PlayerWorldExplorationWorldHeight, true);
            Append(builder, "PlayerWorldExplorationTotalTileCount", snapshot.PlayerWorldExplorationTotalTileCount, true);
            Append(builder, "PlayerWorldExplorationRevealedTileCount", snapshot.PlayerWorldExplorationRevealedTileCount, true);
            Append(builder, "PlayerWorldExplorationWorkingRevealedTileCount", snapshot.PlayerWorldExplorationWorkingRevealedTileCount, true);
            Append(builder, "PlayerWorldExplorationScannedTileCount", snapshot.PlayerWorldExplorationScannedTileCount, true);
            Append(builder, "PlayerWorldExplorationNextTileIndex", snapshot.PlayerWorldExplorationNextTileIndex, true);
            Append(builder, "PlayerWorldExplorationLastScannedTileBudget", snapshot.PlayerWorldExplorationLastScannedTileBudget, true);
            Append(builder, "PlayerWorldExplorationRevealedPercent", snapshot.PlayerWorldExplorationRevealedPercent, true);
            Append(builder, "PlayerWorldExplorationScanComplete", snapshot.PlayerWorldExplorationScanComplete, true);
            Append(builder, "PlayerWorldExplorationReadFailed", snapshot.PlayerWorldExplorationReadFailed, true);
            Append(builder, "PlayerWorldExplorationWriteFailed", snapshot.PlayerWorldExplorationWriteFailed, true);
            Append(builder, "PlayerWorldExplorationLastScanUtc", FormatDate(snapshot.PlayerWorldExplorationLastScanUtc), true);
            Append(builder, "PlayerWorldExplorationLastCompletedScanUtc", FormatDate(snapshot.PlayerWorldExplorationLastCompletedScanUtc), true);
            Append(builder, "PlayerWorldExplorationLastWriteUtc", FormatDate(snapshot.PlayerWorldExplorationLastWriteUtc), true);
            Append(builder, "TeleportRodHookInstalled", snapshot.MovementTeleportCorrectionHookInstalled, true);
            Append(builder, "TeleportRodHookMethod", snapshot.MovementTeleportCorrectionHookMethod, true);
            Append(builder, "DiagnosticsOverlayVisible", snapshot.DiagnosticsOverlayVisible, true);
            Append(builder, "DrawCallCount", snapshot.DrawCallCount, true);
            Append(builder, "LastDrawUtc", FormatDate(snapshot.LastDrawUtc), true);
            Append(builder, "LastUpdateUtc", FormatDate(snapshot.LastUpdateUtc), true);
            Append(builder, "LastHeartbeatUtc", FormatDate(snapshot.LastHeartbeatUtc), true);
            Append(builder, "FeatureCount", snapshot.FeatureCount, true);
            Append(builder, "EnabledFeatureCount", snapshot.EnabledFeatureCount, true);
            Append(builder, "AppSettingsEnabledFeatureCount", snapshot.AppSettingsEnabledFeatureCount, true);
            Append(builder, "FeatureSettingsEnabledFeatureCount", snapshot.FeatureSettingsEnabledFeatureCount, true);
            Append(builder, "EffectiveEnabledFeatureCount", snapshot.EffectiveEnabledFeatureCount, true);
            Append(builder, "FeatureCatalogCount", snapshot.FeatureCatalogCount, true);
            Append(builder, "ImplementedFeatureCount", snapshot.ImplementedFeatureCount, true);
            Append(builder, "VisibleFeatureCount", snapshot.VisibleFeatureCount, true);
            Append(builder, "HotkeyVisibleFeatureCount", snapshot.HotkeyVisibleFeatureCount, true);
            AppendDictionary(builder, "UserCategoryCounts", snapshot.UserCategoryCounts, true);
            AppendDictionary(builder, "CodeDomainCounts", snapshot.CodeDomainCounts, true);
            Append(builder, "WorldGenDebugViewerConfiguredEnabled", snapshot.WorldGenDebugViewerConfiguredEnabled, true);
            Append(builder, "DeveloperDebugCommandsConfiguredEnabled", snapshot.DeveloperDebugCommandsConfiguredEnabled, true);
            Append(builder, "WorldGenDebugViewerSessionConfiguredEnabled", snapshot.WorldGenDebugViewerSessionConfiguredEnabled, true);
            Append(builder, "DeveloperDebugCommandsSessionConfiguredEnabled", snapshot.DeveloperDebugCommandsSessionConfiguredEnabled, true);
            Append(builder, "WorldGenDebugAttempted", snapshot.WorldGenDebugAttempted, true);
            Append(builder, "WorldGenDebugFieldEnabled", snapshot.WorldGenDebugFieldEnabled, true);
            Append(builder, "WorldGenDebugStatus", snapshot.WorldGenDebugStatus, true);
            Append(builder, "WorldGenDebugMessage", snapshot.WorldGenDebugMessage, true);
            Append(builder, "WorldGenDebugFieldOwner", snapshot.WorldGenDebugFieldOwner, true);
            Append(builder, "WorldGenDebugLastAttemptUtc", FormatDate(snapshot.WorldGenDebugLastAttemptUtc), true);
            Append(builder, "FeatureManagerUpdateCount", snapshot.FeatureManagerUpdateCount, true);
            Append(builder, "IsInMainMenu", snapshot.IsInMainMenu, true);
            Append(builder, "IsInWorld", snapshot.IsInWorld, true);
            Append(builder, "GameInputAvailable", snapshot.GameInputAvailable, true);
            Append(builder, "DiagnosticInputSkipped", snapshot.DiagnosticInputSkipped, true);
            Append(builder, "DiagnosticInputGateStatus", snapshot.DiagnosticInputGateStatus, true);
            Append(builder, "DiagnosticInputSkipReason", snapshot.DiagnosticInputSkipReason, true);
            Append(builder, "DiagnosticInputSkipUtc", FormatDate(snapshot.DiagnosticInputSkipUtc), true);
            Append(builder, "PlayerLife", snapshot.PlayerLife, true);
            Append(builder, "PlayerLifeMax", snapshot.PlayerLifeMax, true);
            Append(builder, "PlayerMana", snapshot.PlayerMana, true);
            Append(builder, "PlayerManaMax", snapshot.PlayerManaMax, true);
            Append(builder, "SelectedItemType", snapshot.SelectedItemType, true);
            Append(builder, "SelectedItemName", snapshot.SelectedItemName, true);
            Append(builder, "InventoryNonEmptyCount", snapshot.InventoryNonEmptyCount, true);
            Append(builder, "ActiveBuffCount", snapshot.ActiveBuffCount, true);
            Append(builder, "ActiveNpcCount", snapshot.ActiveNpcCount, true);
            Append(builder, "TownNpcCount", snapshot.TownNpcCount, true);
            Append(builder, "HostileNpcCount", snapshot.HostileNpcCount, true);
            Append(builder, "CritterCount", snapshot.CritterCount, true);
            Append(builder, "LastGameStateReadUtc", FormatDate(snapshot.LastGameStateReadUtc), true);
            Append(builder, "LastGameStateReadError", snapshot.LastGameStateReadError, true);
            Append(builder, "LastGameStateInventoryProfile", snapshot.LastGameStateInventoryProfile, true);
            Append(builder, "LastGameStateNpcProfile", snapshot.LastGameStateNpcProfile, true);
            Append(builder, "LastGameStateTileProfile", snapshot.LastGameStateTileProfile, true);
            Append(builder, "PendingActionCount", snapshot.PendingActionCount, true);
            Append(builder, "ActionQueueUpdateCount", snapshot.ActionQueueUpdateCount, true);
            Append(builder, "RunningAction", snapshot.RunningAction, true);
            Append(builder, "RunningActionKind", snapshot.RunningActionKind, true);
            Append(builder, "RunningActionSource", snapshot.RunningActionSource, true);
            Append(builder, "RunningActionStatus", snapshot.RunningActionStatus, true);
            Append(builder, "LastActionKind", snapshot.LastActionKind, true);
            Append(builder, "LastActionStatus", snapshot.LastActionStatus, true);
            Append(builder, "LastActionMessage", snapshot.LastActionMessage, true);
            Append(builder, "LastActionUserMessage", snapshot.LastActionUserMessage, true);
            Append(builder, "LastActionResultCode", snapshot.LastActionResultCode, true);
            Append(builder, "LastActionDurationMs", snapshot.LastActionDurationMs, true);
            Append(builder, "RecentActionResultsCount", snapshot.RecentActionResultsCount, true);
            Append(builder, "LastActionResult", snapshot.LastActionResult, true);
            Append(builder, "RecentActionLine1", snapshot.RecentActionLine1, true);
            Append(builder, "RecentActionLine2", snapshot.RecentActionLine2, true);
            Append(builder, "RecentActionLine3", snapshot.RecentActionLine3, true);
            Append(builder, "ActionQueueChannelLeaseCount", snapshot.ActionQueueChannelLeaseCount, true);
            Append(builder, "ActionQueueRunningChannels", snapshot.ActionQueueRunningChannels, true);
            Append(builder, "ActionQueueOccupiedChannels", snapshot.ActionQueueOccupiedChannels, true);
            Append(builder, "ActionQueueBridgeBusyChannels", snapshot.ActionQueueBridgeBusyChannels, true);
            Append(builder, "ActionQueueRunningLeaseChannels", snapshot.ActionQueueRunningLeaseChannels, true);
            Append(builder, "ActionQueueBlockedPendingCount", snapshot.ActionQueueBlockedPendingCount, true);
            Append(builder, "ActionQueueLastChannelDecision", snapshot.ActionQueueLastChannelDecision, true);
            Append(builder, "ActionQueueLastChannelBlockedReason", snapshot.ActionQueueLastChannelBlockedReason, true);
            Append(builder, "ActionQueueChannelOwnerSummary", snapshot.ActionQueueChannelOwnerSummary, true);
            Append(builder, "ActionQueueBridgeBusySummary", snapshot.ActionQueueBridgeBusySummary, true);
            Append(builder, "ActionQueuePendingChannelSummary", snapshot.ActionQueuePendingChannelSummary, true);
            Append(builder, "ActionQueuePendingOwnerSummary", snapshot.ActionQueuePendingOwnerSummary, true);
            Append(builder, "ActionQueueLastAdmissionStatus", snapshot.ActionQueueLastAdmissionStatus, true);
            Append(builder, "ActionQueueLastAdmissionDecision", snapshot.ActionQueueLastAdmissionDecision, true);
            Append(builder, "ActionQueueLastAdmissionReason", snapshot.ActionQueueLastAdmissionReason, true);
            Append(builder, "ActionQueueLastAdmissionKind", snapshot.ActionQueueLastAdmissionKind, true);
            Append(builder, "ActionQueueLastAdmissionSource", snapshot.ActionQueueLastAdmissionSource, true);
            Append(builder, "ActionQueueLastAdmissionScenario", snapshot.ActionQueueLastAdmissionScenario, true);
            Append(builder, "ActionQueueLastAdmissionKey", snapshot.ActionQueueLastAdmissionKey, true);
            Append(builder, "ActionQueueLastAdmissionRequiredChannels", snapshot.ActionQueueLastAdmissionRequiredChannels, true);
            Append(builder, "ActionQueueLastAdmissionBlockingChannels", snapshot.ActionQueueLastAdmissionBlockingChannels, true);
            Append(builder, "ActionQueueLastAdmissionConflictChannels", snapshot.ActionQueueLastAdmissionConflictChannels, true);
            Append(builder, "ActionQueueLastAdmissionPendingConflictSummary", snapshot.ActionQueueLastAdmissionPendingConflictSummary, true);
            Append(builder, "ActionQueueLastAdmissionRunningConflictSummary", snapshot.ActionQueueLastAdmissionRunningConflictSummary, true);
            Append(builder, "ActionQueueLastAdmissionBridgeBusySummary", snapshot.ActionQueueLastAdmissionBridgeBusySummary, true);
            Append(builder, "ActionQueueLastAdmissionOwnerSummary", snapshot.ActionQueueLastAdmissionOwnerSummary, true);
            Append(builder, "ActionQueueLastAdmissionSupersededRequestId", snapshot.ActionQueueLastAdmissionSupersededRequestId, true);
            Append(builder, "ActionQueueLastAdmissionCoalescedRequestId", snapshot.ActionQueueLastAdmissionCoalescedRequestId, true);
            Append(builder, "ActionQueueSupersededPendingCount", snapshot.ActionQueueSupersededPendingCount, true);
            Append(builder, "ActionQueueCoalescedPendingCount", snapshot.ActionQueueCoalescedPendingCount, true);
            Append(builder, "SchedulerLastSelectedRequest", snapshot.SchedulerLastSelectedRequest, true);
            Append(builder, "SchedulerLastSupersededRequest", snapshot.SchedulerLastSupersededRequest, true);
            Append(builder, "SchedulerLastFairnessBucket", snapshot.SchedulerLastFairnessBucket, true);
            Append(builder, "WorldAutomationLastWinner", snapshot.WorldAutomationLastWinner, true);
            Append(builder, "WorldAutomationFairnessDebt", snapshot.WorldAutomationFairnessDebt, true);
            Append(builder, "WorldAutomationFairnessDecisionUtc", FormatDate(snapshot.WorldAutomationFairnessDecisionUtc), true);
            Append(builder, "BackgroundRequestCoalescedCount", snapshot.BackgroundRequestCoalescedCount, true);
            Append(builder, "ExpiredPendingDroppedCount", snapshot.ExpiredPendingDroppedCount, true);
            Append(builder, "ActionQueueCleanupLeaseCount", snapshot.ActionQueueCleanupLeaseCount, true);
            Append(builder, "ActionQueueCleanupLeaseChannels", snapshot.ActionQueueCleanupLeaseChannels, true);
            Append(builder, "ActionQueueLastCleanupOwner", snapshot.ActionQueueLastCleanupOwner, true);
            Append(builder, "ActionQueueLastCleanupReason", snapshot.ActionQueueLastCleanupReason, true);
            Append(builder, "ActionQueueDirectEnqueueCount", snapshot.ActionQueueDirectEnqueueCount, true);
            Append(builder, "ActionQueueLastDirectEnqueueKind", snapshot.ActionQueueLastDirectEnqueueKind, true);
            Append(builder, "ActionQueueLastDirectEnqueueSource", snapshot.ActionQueueLastDirectEnqueueSource, true);
            Append(builder, "ActionQueueLastDirectEnqueueScenario", snapshot.ActionQueueLastDirectEnqueueScenario, true);
            Append(builder, "ActionQueueLastDirectEnqueueAdmissionKey", snapshot.ActionQueueLastDirectEnqueueAdmissionKey, true);
            Append(builder, "ActionQueueLastDirectEnqueueRequiredChannels", snapshot.ActionQueueLastDirectEnqueueRequiredChannels, true);
            Append(builder, "ActionQueueExpiredPendingCount", snapshot.ActionQueueExpiredPendingCount, true);
            Append(builder, "ActionQueueLastPendingExpiryReason", snapshot.ActionQueueLastPendingExpiryReason, true);
            Append(builder, "ItemUseBridgeLastStatus", snapshot.ItemUseBridgeLastStatus, true);
            Append(builder, "ItemUseBridgeLastMessage", snapshot.ItemUseBridgeLastMessage, true);
            Append(builder, "ItemUseBridgeLastRequestId", snapshot.ItemUseBridgeLastRequestId, true);
            Append(builder, "ItemUseBridgePendingRequestId", snapshot.ItemUseBridgePendingRequestId, true);
            Append(builder, "ItemUseBridgePendingAgeMs", snapshot.ItemUseBridgePendingAgeMs, true);
            Append(builder, "ItemUseBridgeConsumeCount", snapshot.ItemUseBridgeConsumeCount, true);
            Append(builder, "ItemUseBridgeSucceededCount", snapshot.ItemUseBridgeSucceededCount, true);
            Append(builder, "ItemUseBridgeAttemptedButUnverifiedCount", snapshot.ItemUseBridgeAttemptedButUnverifiedCount, true);
            Append(builder, "ItemUseBridgeFailedCount", snapshot.ItemUseBridgeFailedCount, true);
            Append(builder, "ItemCheckWriterOwner", snapshot.ItemCheckWriterOwner, true);
            Append(builder, "ItemCheckWriterOwnerRequestId", snapshot.ItemCheckWriterOwnerRequestId, true);
            Append(builder, "ItemCheckWriterPhase", snapshot.ItemCheckWriterPhase, true);
            Append(builder, "ItemCheckWriterDecisionReason", snapshot.ItemCheckWriterDecisionReason, true);
            Append(builder, "ItemCheckWriterBlockedCandidates", snapshot.ItemCheckWriterBlockedCandidates, true);
            Append(builder, "ItemCheckWriterDecisionUtc", FormatDate(snapshot.ItemCheckWriterDecisionUtc), true);
            Append(builder, "EnableDiagnosticInputTests", snapshot.EnableDiagnosticInputTests, true);
            Append(builder, "DiagnosticInputTests", snapshot.EnableDiagnosticInputTests, true);
            Append(builder, "DiagnosticInputTestSlot", snapshot.DiagnosticInputTestSlot, true);
            Append(builder, "DiagnosticInputTestSlotDisplay", snapshot.DiagnosticInputTestSlotDisplay, true);
            Append(builder, "DiagnosticTestSlot", snapshot.DiagnosticTestSlot, true);
            Append(builder, "DiagnosticTestSlotDisplay", snapshot.DiagnosticTestSlotDisplay, true);
            Append(builder, "DiagnosticTestSlotItemType", snapshot.DiagnosticTestSlotItemType, true);
            Append(builder, "DiagnosticTestSlotItemName", snapshot.DiagnosticTestSlotItemName, true);
            Append(builder, "DiagnosticTestSlotItemStack", snapshot.DiagnosticTestSlotItemStack, true);
            Append(builder, "DiagnosticTestSlotSuitability", snapshot.DiagnosticTestSlotSuitability, true);
            Append(builder, "DiagnosticTestSlotHint", snapshot.DiagnosticTestSlotHint, true);
            Append(builder, "ActionEventsPath", snapshot.ActionEventsPath, true);
            Append(builder, "LastActionEventWrittenAtUtc", FormatDate(snapshot.LastActionEventWrittenAtUtc), true);
            Append(builder, "LastDiagnosticSourceKind", snapshot.LastDiagnosticSourceKind, true);
            Append(builder, "LastDiagnosticButtonId", snapshot.LastDiagnosticButtonId, true);
            Append(builder, "LastDiagnosticButtonLabel", snapshot.LastDiagnosticButtonLabel, true);
            Append(builder, "LastButtonClickUtc", FormatDate(snapshot.LastButtonClickUtc), true);
            Append(builder, "LastButtonResultCode", snapshot.LastButtonResultCode, true);
            Append(builder, "LastButtonMessage", snapshot.LastButtonMessage, true);
            Append(builder, "UiPrimitiveRendererReady", snapshot.UiPrimitiveRendererReady, true);
            Append(builder, "UiPrimitiveRendererLastMessage", snapshot.UiPrimitiveRendererLastMessage, true);
            Append(builder, "UiMouseReadAvailable", snapshot.UiMouseReadAvailable, true);
            Append(builder, "UiMouseReadLastMessage", snapshot.UiMouseReadLastMessage, true);
            Append(builder, "UiMouseCaptureAvailable", snapshot.UiMouseCaptureAvailable, true);
            Append(builder, "UiMouseCaptureLastMessage", snapshot.UiMouseCaptureLastMessage, true);
            Append(builder, "UiClickSuppressionAttempted", snapshot.UiClickSuppressionAttempted, true);
            Append(builder, "UiClickSuppressionMode", snapshot.UiClickSuppressionMode, true);
            Append(builder, "UiClickSuppressionSucceeded", snapshot.UiClickSuppressionSucceeded, true);
            Append(builder, "ButtonHoverAtUpdatePrefix", snapshot.ButtonHoverAtUpdatePrefix, true);
            Append(builder, "OverlayHoverAtUpdatePrefix", snapshot.OverlayHoverAtUpdatePrefix, true);
            Append(builder, "LastMouseX", snapshot.LastMouseX, true);
            Append(builder, "LastMouseY", snapshot.LastMouseY, true);
            Append(builder, "TerrariaMouseX", snapshot.TerrariaMouseX, true);
            Append(builder, "TerrariaMouseY", snapshot.TerrariaMouseY, true);
            Append(builder, "TerrariaLeftDown", snapshot.TerrariaLeftDown, true);
            Append(builder, "TerrariaLeftReleaseAvailable", snapshot.TerrariaLeftReleaseAvailable, true);
            Append(builder, "TerrariaLeftRelease", snapshot.TerrariaLeftRelease, true);
            Append(builder, "OsClientMouseX", snapshot.OsClientMouseX, true);
            Append(builder, "OsClientMouseY", snapshot.OsClientMouseY, true);
            Append(builder, "OsLeftDown", snapshot.OsLeftDown, true);
            Append(builder, "UiScale", snapshot.UiScale, true);
            Append(builder, "UiScaleAvailable", snapshot.UiScaleAvailable, true);
            Append(builder, "UiScaleMatrixAvailable", snapshot.UiScaleMatrixAvailable, true);
            Append(builder, "MouseReadMode", snapshot.MouseReadMode, true);
            Append(builder, "MouseReadLastError", snapshot.MouseReadLastError, true);
            Append(builder, "HitTestMode", snapshot.HitTestMode, true);
            Append(builder, "HitTestX", snapshot.HitTestX, true);
            Append(builder, "HitTestY", snapshot.HitTestY, true);
            Append(builder, "HitTestConflict", snapshot.HitTestConflict, true);
            Append(builder, "HitTestCandidateSummary", snapshot.HitTestCandidateSummary, true);
            Append(builder, "ClickSource", snapshot.ClickSource, true);
            Append(builder, "LastButtonHitTestMode", snapshot.LastButtonHitTestMode, true);
            Append(builder, "LastButtonClickSource", snapshot.LastButtonClickSource, true);
            Append(builder, "HoveredButtonId", snapshot.HoveredButtonId, true);
            Append(builder, "HoveredButtonLabel", snapshot.HoveredButtonLabel, true);
            Append(builder, "HoveredButtonHint", snapshot.HoveredButtonHint, true);
            Append(builder, "HoveredButtonEnabled", snapshot.HoveredButtonEnabled, true);
            Append(builder, "HoveredButtonVisualX", snapshot.HoveredButtonVisualX, true);
            Append(builder, "HoveredButtonVisualY", snapshot.HoveredButtonVisualY, true);
            Append(builder, "HoveredButtonVisualWidth", snapshot.HoveredButtonVisualWidth, true);
            Append(builder, "HoveredButtonVisualHeight", snapshot.HoveredButtonVisualHeight, true);
            Append(builder, "HoveredButtonHitX", snapshot.HoveredButtonHitX, true);
            Append(builder, "HoveredButtonHitY", snapshot.HoveredButtonHitY, true);
            Append(builder, "HoveredButtonHitWidth", snapshot.HoveredButtonHitWidth, true);
            Append(builder, "HoveredButtonHitHeight", snapshot.HoveredButtonHitHeight, true);
            Append(builder, "LegacyUiLayoutCacheHitCount", snapshot.LegacyUiLayoutCacheHitCount, true);
            Append(builder, "LegacyUiLayoutCacheMissCount", snapshot.LegacyUiLayoutCacheMissCount, true);
            Append(builder, "LegacyUiLastFrameVisibleElementCount", snapshot.LegacyUiLastFrameVisibleElementCount, true);
            Append(builder, "LegacyUiHoverReuseCount", snapshot.LegacyUiHoverReuseCount, true);
            Append(builder, "LegacyUiHoverTooltipCacheHitCount", snapshot.LegacyUiHoverTooltipCacheHitCount, true);
            Append(builder, "LegacyUiHoverTooltipCacheMissCount", snapshot.LegacyUiHoverTooltipCacheMissCount, true);
            Append(builder, "LegacyUiHoverDiagnosticSuppressedCount", snapshot.LegacyUiHoverDiagnosticSuppressedCount, true);
            Append(builder, "LegacyUiScrollSnapshotSkippedCount", snapshot.LegacyUiScrollSnapshotSkippedCount, true);
            Append(builder, "LegacyUiScrollEventCoalescedCount", snapshot.LegacyUiScrollEventCoalescedCount, true);
            Append(builder, "LegacyUiRetainedFrameCacheHitCount", snapshot.LegacyUiRetainedFrameCacheHitCount, true);
            Append(builder, "LegacyUiRetainedFrameCacheMissCount", snapshot.LegacyUiRetainedFrameCacheMissCount, true);
            Append(builder, "LegacyUiRetainedFrameFallbackCount", snapshot.LegacyUiRetainedFrameFallbackCount, true);
            Append(builder, "LegacyUiRetainedFrameVisibleElementCount", snapshot.LegacyUiRetainedFrameVisibleElementCount, true);
            Append(builder, "LegacyUiActionUpdateSkippedCount", snapshot.LegacyUiActionUpdateSkippedCount, true);
            Append(builder, "LegacyUiActionUpdateRanCount", snapshot.LegacyUiActionUpdateRanCount, true);
            Append(builder, "LegacyUiPendingCommandCountLast", snapshot.LegacyUiPendingCommandCountLast, true);
            Append(builder, "LegacyUiDispatchedCommandCountLast", snapshot.LegacyUiDispatchedCommandCountLast, true);
            Append(builder, "LegacyUiDispatchElapsedMsLast", snapshot.LegacyUiDispatchElapsedMsLast, true);
            Append(builder, "LegacyUiCommandCoalescedCount", snapshot.LegacyUiCommandCoalescedCount, true);
            Append(builder, "LegacyUiDragFrameActionSkipCount", snapshot.LegacyUiDragFrameActionSkipCount, true);
            Append(builder, "LegacyImePanelFocused", snapshot.LegacyImePanelFocused, true);
            Append(builder, "LegacyImePanelDiagnosticMessage", snapshot.LegacyImePanelDiagnosticMessage, true);
            Append(builder, "LegacyImePanelLastStatus", snapshot.LegacyImePanelLastStatus, true);
            Append(builder, "LegacyImePanelLastMessage", snapshot.LegacyImePanelLastMessage, true);
            Append(builder, "LegacyImePanelAnchorAttachedThisFrame", snapshot.LegacyImePanelAnchorAttachedThisFrame, true);
            Append(builder, "LegacyImePanelDrawnThisFrame", snapshot.LegacyImePanelDrawnThisFrame, true);
            Append(builder, "LegacyImePanelReflectionResolveCount", snapshot.LegacyImePanelReflectionResolveCount, true);
            Append(builder, "LegacyImePanelCadenceSummary", snapshot.LegacyImePanelCadenceSummary, true);
            Append(builder, "LastDiagnosticHotkey", snapshot.LastDiagnosticHotkey, true);
            Append(builder, "LastDiagnosticHotkeyUtc", FormatDate(snapshot.LastDiagnosticHotkeyUtc), true);
            Append(builder, "LastDiagnosticHotkeyMessage", snapshot.LastDiagnosticHotkeyMessage, true);
            Append(builder, "QuickActionLastKind", snapshot.QuickActionLastKind, true);
            Append(builder, "QuickActionLastStatus", snapshot.QuickActionLastStatus, true);
            Append(builder, "QuickActionLastResultCode", snapshot.QuickActionLastResultCode, true);
            Append(builder, "QuickActionLastMessage", snapshot.QuickActionLastMessage, true);
            Append(builder, "MouseTargetLastStatus", snapshot.MouseTargetLastStatus, true);
            Append(builder, "MouseTargetLastResultCode", snapshot.MouseTargetLastResultCode, true);
            Append(builder, "MouseTargetLastMessage", snapshot.MouseTargetLastMessage, true);
            Append(builder, "RuntimeUpdateCount", snapshot.RuntimeUpdateCount, true);
            Append(builder, "AverageRuntimeUpdateMs", snapshot.AverageRuntimeUpdateMs, true);
            Append(builder, "LastRuntimeUpdateMs", snapshot.LastRuntimeUpdateMs, true);
            Append(builder, "LastUpdateStartGapMs", snapshot.LastUpdateStartGapMs, true);
            Append(builder, "LastGameStateReadMs", snapshot.LastGameStateReadMs, true);
            Append(builder, "LastActionQueueUpdateMs", snapshot.LastActionQueueUpdateMs, true);
            Append(builder, "LastInputActionUpdateMs", snapshot.LastInputActionUpdateMs, true);
            Append(builder, "LastInformationDrawMs", snapshot.LastInformationDrawMs, true);
            Append(builder, "RecentPerformanceWindowCapacitySamples", snapshot.RecentPerformanceWindowCapacitySamples, true);
            Append(builder, "RecentPerformanceWindowSampleCount", snapshot.RecentPerformanceWindowSampleCount, true);
            Append(builder, "RecentRuntimeUpdateAverageMs", snapshot.RecentRuntimeUpdateAverageMs, true);
            Append(builder, "RecentGameStateReadAverageMs", snapshot.RecentGameStateReadAverageMs, true);
            Append(builder, "RecentActionQueueUpdateAverageMs", snapshot.RecentActionQueueUpdateAverageMs, true);
            Append(builder, "RecentInputActionUpdateAverageMs", snapshot.RecentInputActionUpdateAverageMs, true);
            Append(builder, "RecentInformationDrawAverageMs", snapshot.RecentInformationDrawAverageMs, true);
            Append(builder, "UiTextFastPathHitCount", snapshot.UiTextFastPathHitCount, true);
            Append(builder, "UiTextFallbackCount", snapshot.UiTextFallbackCount, true);
            Append(builder, "LastSlowestStageName", snapshot.LastSlowestStageName, true);
            Append(builder, "LastSlowestStageElapsedMs", snapshot.LastSlowestStageElapsedMs, true);
            Append(builder, "LastSlowestOperationName", snapshot.LastSlowestOperationName, true);
            Append(builder, "LastSlowestOperationElapsedMs", snapshot.LastSlowestOperationElapsedMs, true);
            Append(builder, "PerformanceEventsPath", snapshot.PerformanceEventsPath, true);
            Append(builder, "PerformanceHitchCount", snapshot.PerformanceHitchCount, true);
            Append(builder, "LastPerformanceHitchUtc", FormatDate(snapshot.LastPerformanceHitchUtc), true);
            Append(builder, "LastPerformanceHitchReason", snapshot.LastPerformanceHitchReason, true);
            Append(builder, "LastPerformanceHitchUpdateGapMs", snapshot.LastPerformanceHitchUpdateGapMs, true);
            Append(builder, "LastPerformanceHitchRuntimeUpdateMs", snapshot.LastPerformanceHitchRuntimeUpdateMs, true);
            Append(builder, "LastPerformanceHitchGameStateReadMs", snapshot.LastPerformanceHitchGameStateReadMs, true);
            Append(builder, "LastPerformanceHitchActionQueueUpdateMs", snapshot.LastPerformanceHitchActionQueueUpdateMs, true);
            Append(builder, "LastPerformanceHitchInputActionUpdateMs", snapshot.LastPerformanceHitchInputActionUpdateMs, true);
            Append(builder, "LastPerformanceHitchInformationDrawMs", snapshot.LastPerformanceHitchInformationDrawMs, true);
            Append(builder, "LastPerformanceHitchSlowestStageName", snapshot.LastPerformanceHitchSlowestStageName, true);
            Append(builder, "LastPerformanceHitchSlowestStageMs", snapshot.LastPerformanceHitchSlowestStageMs, true);
            Append(builder, "LastPerformanceHitchSlowestOperationName", snapshot.LastPerformanceHitchSlowestOperationName, true);
            Append(builder, "LastPerformanceHitchSlowestOperationMs", snapshot.LastPerformanceHitchSlowestOperationMs, true);
            Append(builder, "PerformanceOperationEventCount", snapshot.PerformanceOperationEventCount, true);
            Append(builder, "LastPerformanceOperationScenario", snapshot.LastPerformanceOperationScenario, true);
            Append(builder, "LastPerformanceOperationUtc", FormatDate(snapshot.LastPerformanceOperationUtc), true);
            Append(builder, "LastPerformanceOperationElapsedMs", snapshot.LastPerformanceOperationElapsedMs, true);
            Append(builder, "LastPerformanceOperationThresholdMs", snapshot.LastPerformanceOperationThresholdMs, true);
            Append(builder, "LastPerformanceOperationReason", snapshot.LastPerformanceOperationReason, true);
            Append(builder, "LastPerformanceOperationOwnerSummary", snapshot.LastPerformanceOperationOwnerSummary, true);
            Append(builder, "ReflectionCacheReady", snapshot.ReflectionCacheReady, true);
            Append(builder, "ReflectionCacheMissCount", snapshot.ReflectionCacheMissCount, true);
            Append(builder, "ReflectionCacheLastMissKey", snapshot.ReflectionCacheLastMissKey, true);
            Append(builder, "ReflectionCacheLastMissUtc", FormatDate(snapshot.ReflectionCacheLastMissUtc), true);
            Append(builder, "ReflectionCacheLastError", snapshot.ReflectionCacheLastError, true);
            Append(builder, "InputCompatReady", snapshot.InputCompatReady, true);
            Append(builder, "SelectedItemGetterReady", snapshot.SelectedItemGetterReady, true);
            Append(builder, "SelectedItemSelectorReady", snapshot.SelectedItemSelectorReady, true);
            Append(builder, "SelectedItemAccessorReady", snapshot.SelectedItemAccessorReady, true);
            Append(builder, "PlayerTypeName", snapshot.PlayerTypeName, true);
            Append(builder, "LastInputCompatError", snapshot.LastInputCompatError, true);
            Append(builder, "ConfigLastSaveUtc", FormatDate(snapshot.ConfigLastSaveUtc), true);
            Append(builder, "ConfigLastSaveSucceeded", snapshot.ConfigLastSaveSucceeded, true);
            Append(builder, "ConfigLastSaveSummary", snapshot.ConfigLastSaveSummary, true);
            Append(builder, "ConfigLastSaveAppSettingsSucceeded", snapshot.ConfigLastSaveAppSettingsSucceeded, true);
            Append(builder, "ConfigLastSaveAppSettingsPath", snapshot.ConfigLastSaveAppSettingsPath, true);
            Append(builder, "ConfigLastSaveAppSettingsError", snapshot.ConfigLastSaveAppSettingsError, true);
            Append(builder, "ConfigLastSaveFeatureSettingsSucceeded", snapshot.ConfigLastSaveFeatureSettingsSucceeded, true);
            Append(builder, "ConfigLastSaveFeatureSettingsPath", snapshot.ConfigLastSaveFeatureSettingsPath, true);
            Append(builder, "ConfigLastSaveFeatureSettingsError", snapshot.ConfigLastSaveFeatureSettingsError, true);
            Append(builder, "ConfigLastSaveHotkeySettingsSucceeded", snapshot.ConfigLastSaveHotkeySettingsSucceeded, true);
            Append(builder, "ConfigLastSaveHotkeySettingsPath", snapshot.ConfigLastSaveHotkeySettingsPath, true);
            Append(builder, "ConfigLastSaveHotkeySettingsError", snapshot.ConfigLastSaveHotkeySettingsError, true);
            Append(builder, "AutoStackLastDecision", snapshot.AutoStackLastDecision, true);
            Append(builder, "AutoStackLastInventorySignature", snapshot.AutoStackLastInventorySignature, true);
            Append(builder, "AutoStackLastPendingItemIds", snapshot.AutoStackLastPendingItemIds, true);
            Append(builder, "AutoStackLastDetectedItemIds", snapshot.AutoStackLastDetectedItemIds, true);
            Append(builder, "AutoStackPendingSinceTick", snapshot.AutoStackPendingSinceTick, true);
            Append(builder, "AutoStackLastPendingChangeTick", snapshot.AutoStackLastPendingChangeTick, true);
            Append(builder, "AutoStackLastPendingClearReason", snapshot.AutoStackLastPendingClearReason, true);
            Append(builder, "AutoStackPendingTransactionState", snapshot.AutoStackPendingTransactionState, true);
            Append(builder, "AutoStackPendingRetryCount", snapshot.AutoStackPendingRetryCount, true);
            Append(builder, "AutoStackLastSubmitRequestId", snapshot.AutoStackLastSubmitRequestId, true);
            Append(builder, "AutoStackLastResult", snapshot.AutoStackLastResult, true);
            Append(builder, "AutoStackLastUnverifiedReason", snapshot.AutoStackLastUnverifiedReason, true);
            Append(builder, "AutoStackInventoryTransactionSlots", snapshot.AutoStackInventoryTransactionSlots, true);
            Append(builder, "AutoStackInventoryTransactionBlockingReason", snapshot.AutoStackInventoryTransactionBlockingReason, true);
            Append(builder, "AutoStackActionResultDeliveryMode", snapshot.AutoStackActionResultDeliveryMode, true);
            Append(builder, "AutoStackLastDecisionUtc", FormatDate(snapshot.AutoStackLastDecisionUtc), true);
            Append(builder, "AutoSellLastDecision", snapshot.AutoSellLastDecision, true);
            Append(builder, "AutoSellLastInventorySignature", snapshot.AutoSellLastInventorySignature, true);
            Append(builder, "AutoSellLastItemIds", snapshot.AutoSellLastItemIds, true);
            Append(builder, "AutoSellLastDecisionUtc", FormatDate(snapshot.AutoSellLastDecisionUtc), true);
            Append(builder, "AutoDiscardLastDecision", snapshot.AutoDiscardLastDecision, true);
            Append(builder, "AutoDiscardLastInventorySignature", snapshot.AutoDiscardLastInventorySignature, true);
            Append(builder, "AutoDiscardLastItemIds", snapshot.AutoDiscardLastItemIds, true);
            Append(builder, "AutoDiscardLastDecisionUtc", FormatDate(snapshot.AutoDiscardLastDecisionUtc), true);
            Append(builder, "QuickReforgeLastDecision", snapshot.QuickReforgeLastDecision, true);
            Append(builder, "QuickReforgeLastTargetPrefixes", snapshot.QuickReforgeLastTargetPrefixes, true);
            Append(builder, "QuickReforgeLastMatchedPrefix", snapshot.QuickReforgeLastMatchedPrefix, true);
            Append(builder, "QuickReforgeLastDecisionUtc", FormatDate(snapshot.QuickReforgeLastDecisionUtc), true);
            Append(builder, "AutoTaxCollectLastDecision", snapshot.AutoTaxCollectLastDecision, true);
            Append(builder, "AutoTaxCollectLastDecisionUtc", FormatDate(snapshot.AutoTaxCollectLastDecisionUtc), true);
            Append(builder, "AutoTaxCollectTargetNpcIndex", snapshot.AutoTaxCollectTargetNpcIndex, true);
            Append(builder, "AutoTaxCollectTargetWhoAmI", snapshot.AutoTaxCollectTargetWhoAmI, true);
            Append(builder, "AutoTaxCollectTargetName", snapshot.AutoTaxCollectTargetName, true);
            Append(builder, "AutoTaxCollectTaxMoney", snapshot.AutoTaxCollectTaxMoney, true);
            Append(builder, "AutoTaxCollectLastRequestId", snapshot.AutoTaxCollectLastRequestId, true);
            Append(builder, "AutoCaptureCritterLastDecision", snapshot.AutoCaptureCritterLastDecision, true);
            Append(builder, "AutoCaptureCritterLastDecisionUtc", FormatDate(snapshot.AutoCaptureCritterLastDecisionUtc), true);
            Append(builder, "AutoCaptureCritterBugNetSlot", snapshot.AutoCaptureCritterBugNetSlot, true);
            Append(builder, "AutoCaptureCritterBugNetItemType", snapshot.AutoCaptureCritterBugNetItemType, true);
            Append(builder, "AutoCaptureCritterTargetNpcIndex", snapshot.AutoCaptureCritterTargetNpcIndex, true);
            Append(builder, "AutoCaptureCritterTargetNpcType", snapshot.AutoCaptureCritterTargetNpcType, true);
            Append(builder, "AutoCaptureCritterFishingProtectionState", snapshot.AutoCaptureCritterFishingProtectionState, true);
            Append(builder, "AutoHarvestLastDecision", snapshot.AutoHarvestLastDecision, true);
            Append(builder, "AutoHarvestLastDecisionUtc", FormatDate(snapshot.AutoHarvestLastDecisionUtc), true);
            Append(builder, "AutoHarvestLastAction", snapshot.AutoHarvestLastAction, true);
            Append(builder, "AutoHarvestToolSlot", snapshot.AutoHarvestToolSlot, true);
            Append(builder, "AutoHarvestToolItemType", snapshot.AutoHarvestToolItemType, true);
            Append(builder, "AutoHarvestTargetTileX", snapshot.AutoHarvestTargetTileX, true);
            Append(builder, "AutoHarvestTargetTileY", snapshot.AutoHarvestTargetTileY, true);
            Append(builder, "AutoHarvestTargetSeedItemType", snapshot.AutoHarvestTargetSeedItemType, true);
            Append(builder, "AutoHarvestPendingReplantCount", snapshot.AutoHarvestPendingReplantCount, true);
            Append(builder, "QuickBagOpenLastDecision", snapshot.QuickBagOpenLastDecision, true);
            Append(builder, "QuickBagOpenLastDecisionUtc", FormatDate(snapshot.QuickBagOpenLastDecisionUtc), true);
            Append(builder, "QuickBagOpenBagSlot", snapshot.QuickBagOpenBagSlot, true);
            Append(builder, "QuickBagOpenBagItemType", snapshot.QuickBagOpenBagItemType, true);
            Append(builder, "QuickBagOpenBagItemName", snapshot.QuickBagOpenBagItemName, true);
            Append(builder, "AutoDepositCoinsLastDecision", snapshot.AutoDepositCoinsLastDecision, true);
            Append(builder, "AutoDepositCoinsLastDecisionUtc", FormatDate(snapshot.AutoDepositCoinsLastDecisionUtc), true);
            Append(builder, "AutoDepositCoinsLastInventorySignature", snapshot.AutoDepositCoinsLastInventorySignature, true);
            Append(builder, "AutoDepositCoinsLastCoinItemIds", snapshot.AutoDepositCoinsLastCoinItemIds, true);
            Append(builder, "AutoExtractinatorLastDecision", snapshot.AutoExtractinatorLastDecision, true);
            Append(builder, "AutoExtractinatorLastDecisionUtc", FormatDate(snapshot.AutoExtractinatorLastDecisionUtc), true);
            Append(builder, "AutoExtractinatorItemSlot", snapshot.AutoExtractinatorItemSlot, true);
            Append(builder, "AutoExtractinatorItemType", snapshot.AutoExtractinatorItemType, true);
            Append(builder, "AutoExtractinatorTileX", snapshot.AutoExtractinatorTileX, true);
            Append(builder, "AutoExtractinatorTileY", snapshot.AutoExtractinatorTileY, true);
            Append(builder, "AutoExtractinatorTileType", snapshot.AutoExtractinatorTileType, true);
            Append(builder, "KeepFavoritedLastDecision", snapshot.KeepFavoritedLastDecision, true);
            Append(builder, "KeepFavoritedLastDecisionUtc", FormatDate(snapshot.KeepFavoritedLastDecisionUtc), true);
            Append(builder, "KeepFavoritedSlot", snapshot.KeepFavoritedSlot, true);
            Append(builder, "KeepFavoritedItemType", snapshot.KeepFavoritedItemType, true);
            Append(builder, "KeepFavoritedSignature", snapshot.KeepFavoritedSignature, true);
            Append(builder, "InformationEnabledSummary", snapshot.InformationEnabledSummary, true);
            Append(builder, "InformationNpcLabelsDrawn", snapshot.InformationNpcLabelsDrawn, true);
            Append(builder, "InformationChestLabelsDrawn", snapshot.InformationChestLabelsDrawn, true);
            Append(builder, "InformationSignTextLabelsDrawn", snapshot.InformationSignTextLabelsDrawn, true);
            Append(builder, "InformationTombstoneTextLabelsDrawn", snapshot.InformationTombstoneTextLabelsDrawn, true);
            Append(builder, "InformationTileHighlightsDrawn", snapshot.InformationTileHighlightsDrawn, true);
            Append(builder, "InformationStatusLinesDrawn", snapshot.InformationStatusLinesDrawn, true);
            Append(builder, "InformationLastDrawElapsedMs", snapshot.InformationLastDrawElapsedMs, true);
            Append(builder, "InformationLastSkipReason", snapshot.InformationLastSkipReason, true);
            Append(builder, "InformationStatusPanelLayoutCacheHitCount", snapshot.InformationStatusPanelLayoutCacheHitCount, true);
            Append(builder, "InformationStatusPanelLayoutCacheMissCount", snapshot.InformationStatusPanelLayoutCacheMissCount, true);
            Append(builder, "InformationSignTextLayoutCacheHitCount", snapshot.InformationSignTextLayoutCacheHitCount, true);
            Append(builder, "InformationSignTextLayoutCacheMissCount", snapshot.InformationSignTextLayoutCacheMissCount, true);
            Append(builder, "InformationWorldLabelSnapshotRefreshCount", snapshot.InformationWorldLabelSnapshotRefreshCount, true);
            Append(builder, "InformationNpcLabelSnapshotRefreshCount", snapshot.InformationNpcLabelSnapshotRefreshCount, true);
            Append(builder, "InformationChestLabelSnapshotRefreshCount", snapshot.InformationChestLabelSnapshotRefreshCount, true);
            Append(builder, "InformationChestLabelSortRefreshCount", snapshot.InformationChestLabelSortRefreshCount, true);
            Append(builder, "InformationChestAlwaysScanCacheHitCount", snapshot.InformationChestAlwaysScanCacheHitCount, true);
            Append(builder, "InformationChestAlwaysScanCacheMissCount", snapshot.InformationChestAlwaysScanCacheMissCount, true);
            Append(builder, "InformationChestAlwaysLastDirtyReason", snapshot.InformationChestAlwaysLastDirtyReason, true);
            Append(builder, "InformationChestAlwaysSafeRefreshCount", snapshot.InformationChestAlwaysSafeRefreshCount, true);
            Append(builder, "InformationChestAlwaysTilesVisitedLast", snapshot.InformationChestAlwaysTilesVisitedLast, true);
            Append(builder, "InformationChestAlwaysTypedTileFastPathStatus", snapshot.InformationChestAlwaysTypedTileFastPathStatus, true);
            Append(builder, "InformationChestAlwaysNameCacheHitCount", snapshot.InformationChestAlwaysNameCacheHitCount, true);
            Append(builder, "InformationChestAlwaysNameCacheMissCount", snapshot.InformationChestAlwaysNameCacheMissCount, true);
            Append(builder, "InformationChestAlwaysPartialScanFrameCount", snapshot.InformationChestAlwaysPartialScanFrameCount, true);
            Append(builder, "InformationChestAlwaysPartialScanPendingCount", snapshot.InformationChestAlwaysPartialScanPendingCount, true);
            Append(builder, "InformationChestAlwaysStableSnapshotId", snapshot.InformationChestAlwaysStableSnapshotId, true);
            Append(builder, "InformationWorldContextCacheHitCount", snapshot.InformationWorldContextCacheHitCount, true);
            Append(builder, "InformationWorldContextCacheMissCount", snapshot.InformationWorldContextCacheMissCount, true);
            Append(builder, "InformationWorldContextProfile", snapshot.InformationWorldContextProfile, true);
            Append(builder, "InformationWorldContextFileDataRefreshCount", snapshot.InformationWorldContextFileDataRefreshCount, true);
            Append(builder, "InformationStatusLineCacheHitCount", snapshot.InformationStatusLineCacheHitCount, true);
            Append(builder, "InformationStatusLineCacheMissCount", snapshot.InformationStatusLineCacheMissCount, true);
            Append(builder, "InformationFishingCatchEarlyCacheHitCount", snapshot.InformationFishingCatchEarlyCacheHitCount, true);
            Append(builder, "InformationFishingCatchEarlyCacheMissCount", snapshot.InformationFishingCatchEarlyCacheMissCount, true);
            Append(builder, "InformationFishingWaterScanCount", snapshot.InformationFishingWaterScanCount, true);
            Append(builder, "InformationFishingConditionsReadCount", snapshot.InformationFishingConditionsReadCount, true);
            Append(builder, "InformationFishingBobberObserverFreshInactiveSkipCount", snapshot.InformationFishingBobberObserverFreshInactiveSkipCount, true);
            Append(builder, "InformationFishingProjectileFallbackScanCount", snapshot.InformationFishingProjectileFallbackScanCount, true);
            Append(builder, "SearchChestLocatorOverlayEnabled", snapshot.SearchChestLocatorOverlayEnabled, true);
            Append(builder, "SearchChestLocatorOverlayQueryVersion", snapshot.SearchChestLocatorOverlayQueryVersion, true);
            Append(builder, "SearchChestLocatorOverlaySnapshotStatus", snapshot.SearchChestLocatorOverlaySnapshotStatus, true);
            Append(builder, "SearchChestLocatorOverlayCandidateChestCount", snapshot.SearchChestLocatorOverlayCandidateChestCount, true);
            Append(builder, "SearchChestLocatorOverlayScannedChestCount", snapshot.SearchChestLocatorOverlayScannedChestCount, true);
            Append(builder, "SearchChestLocatorOverlayHitCount", snapshot.SearchChestLocatorOverlayHitCount, true);
            Append(builder, "SearchChestLocatorOverlayDrawnHitCount", snapshot.SearchChestLocatorOverlayDrawnHitCount, true);
            Append(builder, "SearchChestLocatorOverlaySkipReason", snapshot.SearchChestLocatorOverlaySkipReason, true);
            Append(builder, "SearchChestLocatorOverlayRecentElapsedBucket", snapshot.SearchChestLocatorOverlayRecentElapsedBucket, true);
            Append(builder, "SearchChestLocatorOverlaySnapshotAgeTicks", snapshot.SearchChestLocatorOverlaySnapshotAgeTicks, true);
            Append(builder, "SearchChestLocatorSectionRequestEnabled", snapshot.SearchChestLocatorSectionRequestEnabled, true);
            Append(builder, "SearchChestLocatorSectionRequestMultiplayerClient", snapshot.SearchChestLocatorSectionRequestMultiplayerClient, true);
            Append(builder, "SearchChestLocatorSectionRequestAttempted", snapshot.SearchChestLocatorSectionRequestAttempted, true);
            Append(builder, "SearchChestLocatorSectionRequestSent", snapshot.SearchChestLocatorSectionRequestSent, true);
            Append(builder, "SearchChestLocatorSectionRequestThrottled", snapshot.SearchChestLocatorSectionRequestThrottled, true);
            Append(builder, "SearchChestLocatorSectionRequestStatus", snapshot.SearchChestLocatorSectionRequestStatus, true);
            Append(builder, "SearchChestLocatorSectionRequestFailureReason", snapshot.SearchChestLocatorSectionRequestFailureReason, true);
            Append(builder, "SearchChestLocatorSectionRequestSectionKey", snapshot.SearchChestLocatorSectionRequestSectionKey, true);
            Append(builder, "SearchChestLocatorSectionRequestSectionX", snapshot.SearchChestLocatorSectionRequestSectionX, true);
            Append(builder, "SearchChestLocatorSectionRequestSectionY", snapshot.SearchChestLocatorSectionRequestSectionY, true);
            Append(builder, "SearchChestLocatorSectionRequestQueryVersion", snapshot.SearchChestLocatorSectionRequestQueryVersion, true);
            Append(builder, "SearchChestLocatorSectionRequestTick", snapshot.SearchChestLocatorSectionRequestTick, true);
            Append(builder, "SearchChestLocatorSectionRequestCooldownRemainingTicks", snapshot.SearchChestLocatorSectionRequestCooldownRemainingTicks, true);
            Append(builder, "MapQuickAnnouncementLastTriggered", snapshot.MapQuickAnnouncementLastTriggered, true);
            Append(builder, "MapQuickAnnouncementLastResultCode", snapshot.MapQuickAnnouncementLastResultCode, true);
            Append(builder, "MapQuickAnnouncementLastTargetKind", snapshot.MapQuickAnnouncementLastTargetKind, true);
            Append(builder, "MapQuickAnnouncementLastTargetName", snapshot.MapQuickAnnouncementLastTargetName, true);
            Append(builder, "MapQuickAnnouncementLastTargetSummary", snapshot.MapQuickAnnouncementLastTargetSummary, true);
            Append(builder, "MapQuickAnnouncementLastTargetCount", snapshot.MapQuickAnnouncementLastTargetCount, true);
            Append(builder, "MapQuickAnnouncementLastResolveDetail", snapshot.MapQuickAnnouncementLastResolveDetail, true);
            Append(builder, "MapQuickAnnouncementLastTargetSource", snapshot.MapQuickAnnouncementLastTargetSource, true);
            Append(builder, "MapQuickAnnouncementLastUiHoverSource", snapshot.MapQuickAnnouncementLastUiHoverSource, true);
            Append(builder, "MapQuickAnnouncementLastUiHoverState", snapshot.MapQuickAnnouncementLastUiHoverState, true);
            Append(builder, "MapQuickAnnouncementLastUiHoverHookStatus", snapshot.MapQuickAnnouncementLastUiHoverHookStatus, true);
            Append(builder, "MapQuickAnnouncementLastPendingState", snapshot.MapQuickAnnouncementLastPendingState, true);
            Append(builder, "MapQuickAnnouncementLastHoverCacheAgeUpdates", snapshot.MapQuickAnnouncementLastHoverCacheAgeUpdates, true);
            Append(builder, "MapQuickAnnouncementLastPlacementLookupSource", snapshot.MapQuickAnnouncementLastPlacementLookupSource, true);
            Append(builder, "MapQuickAnnouncementLastFallbackReason", snapshot.MapQuickAnnouncementLastFallbackReason, true);
            Append(builder, "MapQuickAnnouncementLastIsAir", snapshot.MapQuickAnnouncementLastIsAir, true);
            Append(builder, "MapQuickAnnouncementLastCooldownBlocked", snapshot.MapQuickAnnouncementLastCooldownBlocked, true);
            Append(builder, "MapQuickAnnouncementLastSendSucceeded", snapshot.MapQuickAnnouncementLastSendSucceeded, true);
            Append(builder, "MapQuickAnnouncementLastFailureReason", snapshot.MapQuickAnnouncementLastFailureReason, true);
            Append(builder, "MapQuickAnnouncementLastHotkeySummary", snapshot.MapQuickAnnouncementLastHotkeySummary, true);
            Append(builder, "MapQuickAnnouncementLastInputConsumed", snapshot.MapQuickAnnouncementLastInputConsumed, true);
            Append(builder, "MapQuickAnnouncementLastInputConsumeResult", snapshot.MapQuickAnnouncementLastInputConsumeResult, true);
            Append(builder, "MapQuickAnnouncementLastDecisionUtc", FormatDate(snapshot.MapQuickAnnouncementLastDecisionUtc), true);
            Append(builder, "FishingAutomationNeedsTick", snapshot.FishingAutomationNeedsTick, true);
            Append(builder, "FishingDisplayNeedsCatchResolver", snapshot.FishingDisplayNeedsCatchResolver, true);
            Append(builder, "FishingHasResidualState", snapshot.FishingHasResidualState, true);
            Append(builder, "FishingSessionActive", snapshot.FishingSessionActive, true);
            Append(builder, "FishingLastDecision", snapshot.FishingLastDecision, true);
            Append(builder, "FishingLastSkipReason", snapshot.FishingLastSkipReason, true);
            Append(builder, "FishingCurrentBobberIdentity", snapshot.FishingCurrentBobberIdentity, true);
            Append(builder, "FishingLastProcessedHookIdentity", snapshot.FishingLastProcessedHookIdentity, true);
            Append(builder, "FishingWaitingForBobberGone", snapshot.FishingWaitingForBobberGone, true);
            Append(builder, "FishingRecastDelayTicks", snapshot.FishingRecastDelayTicks, true);
            Append(builder, "FishingRecastWaitingForBobber", snapshot.FishingRecastWaitingForBobber, true);
            Append(builder, "FishingRecastBobberWaitTicks", snapshot.FishingRecastBobberWaitTicks, true);
            Append(builder, "FishingRecastRetryCount", snapshot.FishingRecastRetryCount, true);
            Append(builder, "FishingFilterSkipInProgress", snapshot.FishingFilterSkipInProgress, true);
            Append(builder, "FishingFilterSkipRequestId", snapshot.FishingFilterSkipRequestId, true);
            Append(builder, "FishingFilterSkipWaitingForBobberGone", snapshot.FishingFilterSkipWaitingForBobberGone, true);
            Append(builder, "FishingFilterSkipTemporarySlot", snapshot.FishingFilterSkipTemporarySlot, true);
            Append(builder, "FishingFilterSkipLastResult", snapshot.FishingFilterSkipLastResult, true);
            Append(builder, "FishingFilterSkipRestoreFailureReason", snapshot.FishingFilterSkipRestoreFailureReason, true);
            Append(builder, "FishingCastWorldX", snapshot.FishingCastWorldX, true);
            Append(builder, "FishingCastWorldY", snapshot.FishingCastWorldY, true);
            Append(builder, "FishingOriginalLoadoutIndex", snapshot.FishingOriginalLoadoutIndex, true);
            Append(builder, "FishingTargetLoadoutIndex", snapshot.FishingTargetLoadoutIndex, true);
            Append(builder, "FishingAutoEquipmentApplied", snapshot.FishingAutoEquipmentApplied, true);
            Append(builder, "FishingAutoEquipmentPendingRestoreCount", snapshot.FishingAutoEquipmentPendingRestoreCount, true);
            Append(builder, "FishingAutoEquipmentLastDecision", snapshot.FishingAutoEquipmentLastDecision, true);
            Append(builder, "FishingAutoEquipmentLastSkipReason", snapshot.FishingAutoEquipmentLastSkipReason, true);
            Append(builder, "FishingAutoEquipmentAppliedMoveCount", snapshot.FishingAutoEquipmentAppliedMoveCount, true);
            Append(builder, "FishingAutoEquipmentStillHoldingOriginalRod", snapshot.FishingAutoEquipmentStillHoldingOriginalRod, true);
            Append(builder, "FishingAutoEquipmentManualInventoryInteractionDetected", snapshot.FishingAutoEquipmentManualInventoryInteractionDetected, true);
            Append(builder, "FishingQuestFishStoreCooldownTicks", snapshot.FishingQuestFishStoreCooldownTicks, true);
            Append(builder, "FishingQuestFishLastItemId", snapshot.FishingQuestFishLastItemId, true);
            Append(builder, "FishingQuestFishLastSlotCount", snapshot.FishingQuestFishLastSlotCount, true);
            Append(builder, "FishingAutoStoreLastMode", snapshot.FishingAutoStoreLastMode, true);
            Append(builder, "FishingAutoStoreLastInventorySignature", snapshot.FishingAutoStoreLastInventorySignature, true);
            Append(builder, "FishingAutoStoreLastPendingItemIds", snapshot.FishingAutoStoreLastPendingItemIds, true);
            Append(builder, "FishingAutoStoreLastDiagnosticMessage", snapshot.FishingAutoStoreLastDiagnosticMessage, true);
            Append(builder, "FishingHookInstalled", snapshot.FishingHookInstalled, true);
            Append(builder, "FishingHookLastObservationTick", snapshot.FishingHookLastObservationTick, true);
            Append(builder, "FishingFallbackScanExecutedCount", snapshot.FishingFallbackScanExecutedCount, true);
            Append(builder, "FishingFallbackScanSkippedHookFreshCount", snapshot.FishingFallbackScanSkippedHookFreshCount, true);
            Append(builder, "FishingFallbackScanForcedDisappearanceConfirmationCount", snapshot.FishingFallbackScanForcedDisappearanceConfirmationCount, true);
            Append(builder, "FishingAutomationDispatchReason", snapshot.FishingAutomationDispatchReason, true);
            Append(builder, "FishingAutomationDispatchCadenceTicks", snapshot.FishingAutomationDispatchCadenceTicks, true);
            Append(builder, "FishingAutomationIdleFastSkipCount", snapshot.FishingAutomationIdleFastSkipCount, true);
            Append(builder, "FishingAutomationIdleWatchdogTickCount", snapshot.FishingAutomationIdleWatchdogTickCount, true);
            Append(builder, "FishingObserverFreshActiveCount", snapshot.FishingObserverFreshActiveCount, true);
            Append(builder, "FishingObserverFreshInactiveSkipCount", snapshot.FishingObserverFreshInactiveSkipCount, true);
            Append(builder, "FishingFallbackScanIdleSkippedCount", snapshot.FishingFallbackScanIdleSkippedCount, true);
            Append(builder, "FishingFallbackScanHookStaleCount", snapshot.FishingFallbackScanHookStaleCount, true);
            Append(builder, "FishingTickSubpathLast", snapshot.FishingTickSubpathLast, true);
            Append(builder, "FishingResidualStateMask", snapshot.FishingResidualStateMask, true);
            Append(builder, "FishingFilterMode", snapshot.FishingFilterMode, true);
            Append(builder, "FishingFilterMatchMode", snapshot.FishingFilterMatchMode, true);
            Append(builder, "FishingFilterCatchKind", snapshot.FishingFilterCatchKind, true);
            Append(builder, "FishingFilterCatchId", snapshot.FishingFilterCatchId, true);
            Append(builder, "FishingFilterCatchName", snapshot.FishingFilterCatchName, true);
            Append(builder, "FishingFilterDecision", snapshot.FishingFilterDecision, true);
            Append(builder, "FishingFilterDecisionReason", snapshot.FishingFilterDecisionReason, true);
            Append(builder, "FishingFilterMatchedRule", snapshot.FishingFilterMatchedRule, true);
            Append(builder, "FishingFilterDryRun", snapshot.FishingFilterDryRun, true);
            Append(builder, "FishingFilterCutRodSkipEnabled", snapshot.FishingFilterCutRodSkipEnabled, true);
            Append(builder, "MovementSimulatedJumpEnabled", snapshot.MovementSimulatedJumpEnabled, true);
            Append(builder, "MovementSimulatedJumpLastTriggered", snapshot.MovementSimulatedJumpLastTriggered, true);
            Append(builder, "MovementSimulatedJumpLastTriggerUtc", FormatDate(snapshot.MovementSimulatedJumpLastTriggerUtc), true);
            Append(builder, "MovementSimulatedJumpLastDecision", snapshot.MovementSimulatedJumpLastDecision, true);
            Append(builder, "MovementSimulatedJumpLastSkipReason", snapshot.MovementSimulatedJumpLastSkipReason, true);
            Append(builder, "MovementSimulatedJumpLastDecisionUtc", FormatDate(snapshot.MovementSimulatedJumpLastDecisionUtc), true);
            Append(builder, "MovementSimulatedJumpLastTick", snapshot.MovementSimulatedJumpLastTick, true);
            Append(builder, "MovementSimulatedJumpPendingActionCount", snapshot.MovementSimulatedJumpPendingActionCount, true);
            Append(builder, "MovementSimulatedJumpRunningActionKind", snapshot.MovementSimulatedJumpRunningActionKind, true);
            Append(builder, "MovementSimulatedJumpItemUseBridgeBusy", snapshot.MovementSimulatedJumpItemUseBridgeBusy, true);
            Append(builder, "MovementSimulatedJumpTextInputFocused", snapshot.MovementSimulatedJumpTextInputFocused, true);
            Append(builder, "MovementSimulatedJumpTextInputReason", snapshot.MovementSimulatedJumpTextInputReason, true);
            Append(builder, "MovementSimulatedJumpHeld", snapshot.MovementSimulatedJumpHeld, true);
            Append(builder, "MovementSimulatedJumpDownHeld", snapshot.MovementSimulatedJumpDownHeld, true);
            Append(builder, "MovementSimulatedJumpPlayerControllable", snapshot.MovementSimulatedJumpPlayerControllable, true);
            Append(builder, "MovementSimulatedJumpAvailableOpportunity", snapshot.MovementSimulatedJumpAvailableOpportunity, true);
            Append(builder, "MovementSimulatedJumpGroundedOrSliding", snapshot.MovementSimulatedJumpGroundedOrSliding, true);
            Append(builder, "MovementSimulatedJumpAerialWindow", snapshot.MovementSimulatedJumpAerialWindow, true);
            Append(builder, "MovementSimulatedJumpHasAirJump", snapshot.MovementSimulatedJumpHasAirJump, true);
            Append(builder, "MovementSimulatedJumpHasRocketJump", snapshot.MovementSimulatedJumpHasRocketJump, true);
            Append(builder, "MovementSimulatedJumpHasWingFlight", snapshot.MovementSimulatedJumpHasWingFlight, true);
            Append(builder, "MovementSimulatedJumpMountActive", snapshot.MovementSimulatedJumpMountActive, true);
            Append(builder, "MovementSimulatedJumpMountCanFlyKnown", snapshot.MovementSimulatedJumpMountCanFlyKnown, true);
            Append(builder, "MovementSimulatedJumpMountCanFly", snapshot.MovementSimulatedJumpMountCanFly, true);
            Append(builder, "MovementSimulatedJumpCapabilitySummary", snapshot.MovementSimulatedJumpCapabilitySummary, true);
            Append(builder, "MovementSimulatedJumpSubmittedCount", snapshot.MovementSimulatedJumpSubmittedCount, true);
            Append(builder, "MovementSimulatedJumpSkippedCount", snapshot.MovementSimulatedJumpSkippedCount, true);
            Append(builder, "MovementContinuousDashEnabled", snapshot.MovementContinuousDashEnabled, true);
            Append(builder, "MovementContinuousDashMode", snapshot.MovementContinuousDashMode, true);
            Append(builder, "MovementContinuousDashLastTriggered", snapshot.MovementContinuousDashLastTriggered, true);
            Append(builder, "MovementContinuousDashLastTriggerDirection", snapshot.MovementContinuousDashLastTriggerDirection, true);
            Append(builder, "MovementContinuousDashLastTriggerUtc", FormatDate(snapshot.MovementContinuousDashLastTriggerUtc), true);
            Append(builder, "MovementContinuousDashLastDecision", snapshot.MovementContinuousDashLastDecision, true);
            Append(builder, "MovementContinuousDashLastSkipReason", snapshot.MovementContinuousDashLastSkipReason, true);
            Append(builder, "MovementContinuousDashLastDecisionUtc", FormatDate(snapshot.MovementContinuousDashLastDecisionUtc), true);
            Append(builder, "MovementContinuousDashLastTick", snapshot.MovementContinuousDashLastTick, true);
            Append(builder, "MovementContinuousDashPendingActionCount", snapshot.MovementContinuousDashPendingActionCount, true);
            Append(builder, "MovementContinuousDashRunningActionKind", snapshot.MovementContinuousDashRunningActionKind, true);
            Append(builder, "MovementContinuousDashTextInputFocused", snapshot.MovementContinuousDashTextInputFocused, true);
            Append(builder, "MovementContinuousDashTextInputReason", snapshot.MovementContinuousDashTextInputReason, true);
            Append(builder, "MovementContinuousDashPlayerControllable", snapshot.MovementContinuousDashPlayerControllable, true);
            Append(builder, "MovementContinuousDashLeftHeld", snapshot.MovementContinuousDashLeftHeld, true);
            Append(builder, "MovementContinuousDashRightHeld", snapshot.MovementContinuousDashRightHeld, true);
            Append(builder, "MovementContinuousDashHeldDirection", snapshot.MovementContinuousDashHeldDirection, true);
            Append(builder, "MovementContinuousDashHasDashAbility", snapshot.MovementContinuousDashHasDashAbility, true);
            Append(builder, "MovementContinuousDashAbilitySource", snapshot.MovementContinuousDashAbilitySource, true);
            Append(builder, "MovementContinuousDashDashType", snapshot.MovementContinuousDashDashType, true);
            Append(builder, "MovementContinuousDashDashDelay", snapshot.MovementContinuousDashDashDelay, true);
            Append(builder, "MovementContinuousDashCooldownReady", snapshot.MovementContinuousDashCooldownReady, true);
            Append(builder, "MovementContinuousDashMountActive", snapshot.MovementContinuousDashMountActive, true);
            Append(builder, "MovementContinuousDashMountType", snapshot.MovementContinuousDashMountType, true);
            Append(builder, "MovementContinuousDashMountCanDashKnown", snapshot.MovementContinuousDashMountCanDashKnown, true);
            Append(builder, "MovementContinuousDashMountCanDash", snapshot.MovementContinuousDashMountCanDash, true);
            Append(builder, "MovementContinuousDashCapabilitySummary", snapshot.MovementContinuousDashCapabilitySummary, true);
            Append(builder, "MovementContinuousDashArmedDirection", snapshot.MovementContinuousDashArmedDirection, true);
            Append(builder, "MovementContinuousDashArmedCancelReason", snapshot.MovementContinuousDashArmedCancelReason, true);
            Append(builder, "MovementContinuousDashArmedCancelCount", snapshot.MovementContinuousDashArmedCancelCount, true);
            Append(builder, "MovementContinuousDashHookInstalled", snapshot.MovementContinuousDashHookInstalled, true);
            Append(builder, "MovementContinuousDashHookMessage", snapshot.MovementContinuousDashHookMessage, true);
            Append(builder, "MovementContinuousDashQueuedPulsePending", snapshot.MovementContinuousDashQueuedPulsePending, true);
            Append(builder, "MovementContinuousDashLastPulseApplied", snapshot.MovementContinuousDashLastPulseApplied, true);
            Append(builder, "MovementContinuousDashLastPulseDirection", snapshot.MovementContinuousDashLastPulseDirection, true);
            Append(builder, "MovementContinuousDashLastPulseUtc", FormatDate(snapshot.MovementContinuousDashLastPulseUtc), true);
            Append(builder, "MovementContinuousDashLastPulseMessage", snapshot.MovementContinuousDashLastPulseMessage, true);
            Append(builder, "MovementContinuousDashLastPulseWasFallback", snapshot.MovementContinuousDashLastPulseWasFallback, true);
            Append(builder, "MovementContinuousDashLastPulseResetMessage", snapshot.MovementContinuousDashLastPulseResetMessage, true);
            Append(builder, "MovementContinuousDashLastCompatError", snapshot.MovementContinuousDashLastCompatError, true);
            Append(builder, "MovementContinuousDashSubmittedCount", snapshot.MovementContinuousDashSubmittedCount, true);
            Append(builder, "MovementContinuousDashSkippedCount", snapshot.MovementContinuousDashSkippedCount, true);
            Append(builder, "MovementTeleportCorrectionEnabled", snapshot.MovementTeleportCorrectionEnabled, true);
            Append(builder, "MovementTeleportCorrectionHookInstalled", snapshot.MovementTeleportCorrectionHookInstalled, true);
            Append(builder, "MovementTeleportCorrectionHookMethod", snapshot.MovementTeleportCorrectionHookMethod, true);
            Append(builder, "MovementTeleportCorrectionHookMessage", snapshot.MovementTeleportCorrectionHookMessage, true);
            Append(builder, "MovementTeleportCorrectionLastDecision", snapshot.MovementTeleportCorrectionLastDecision, true);
            Append(builder, "MovementTeleportCorrectionLastSkipReason", snapshot.MovementTeleportCorrectionLastSkipReason, true);
            Append(builder, "MovementTeleportCorrectionLastDecisionUtc", FormatDate(snapshot.MovementTeleportCorrectionLastDecisionUtc), true);
            Append(builder, "MovementTeleportCorrectionItemType", snapshot.MovementTeleportCorrectionItemType, true);
            Append(builder, "MovementTeleportCorrectionItemName", snapshot.MovementTeleportCorrectionItemName, true);
            Append(builder, "MovementTeleportCorrectionOriginalMouseWorldX", snapshot.MovementTeleportCorrectionOriginalMouseWorldX, true);
            Append(builder, "MovementTeleportCorrectionOriginalMouseWorldY", snapshot.MovementTeleportCorrectionOriginalMouseWorldY, true);
            Append(builder, "MovementTeleportCorrectionOriginalMouseScreenX", snapshot.MovementTeleportCorrectionOriginalMouseScreenX, true);
            Append(builder, "MovementTeleportCorrectionOriginalMouseScreenY", snapshot.MovementTeleportCorrectionOriginalMouseScreenY, true);
            Append(builder, "MovementTeleportCorrectionOriginalTopLeftX", snapshot.MovementTeleportCorrectionOriginalTopLeftX, true);
            Append(builder, "MovementTeleportCorrectionOriginalTopLeftY", snapshot.MovementTeleportCorrectionOriginalTopLeftY, true);
            Append(builder, "MovementTeleportCorrectionOriginalSafe", snapshot.MovementTeleportCorrectionOriginalSafe, true);
            Append(builder, "MovementTeleportCorrectionSearchRadiusPixels", snapshot.MovementTeleportCorrectionSearchRadiusPixels, true);
            Append(builder, "MovementTeleportCorrectionSearchStepPixels", snapshot.MovementTeleportCorrectionSearchStepPixels, true);
            Append(builder, "MovementTeleportCorrectionCandidateCount", snapshot.MovementTeleportCorrectionCandidateCount, true);
            Append(builder, "MovementTeleportCorrectionValidCandidateCount", snapshot.MovementTeleportCorrectionValidCandidateCount, true);
            Append(builder, "MovementTeleportCorrectionNearestCandidateDistance", snapshot.MovementTeleportCorrectionNearestCandidateDistance, true);
            Append(builder, "MovementTeleportCorrectionCorrectedTopLeftX", snapshot.MovementTeleportCorrectionCorrectedTopLeftX, true);
            Append(builder, "MovementTeleportCorrectionCorrectedTopLeftY", snapshot.MovementTeleportCorrectionCorrectedTopLeftY, true);
            Append(builder, "MovementTeleportCorrectionCorrectedMouseWorldX", snapshot.MovementTeleportCorrectionCorrectedMouseWorldX, true);
            Append(builder, "MovementTeleportCorrectionCorrectedMouseWorldY", snapshot.MovementTeleportCorrectionCorrectedMouseWorldY, true);
            Append(builder, "MovementTeleportCorrectionCorrectedMouseScreenX", snapshot.MovementTeleportCorrectionCorrectedMouseScreenX, true);
            Append(builder, "MovementTeleportCorrectionCorrectedMouseScreenY", snapshot.MovementTeleportCorrectionCorrectedMouseScreenY, true);
            Append(builder, "MovementTeleportCorrectionMouseCaptureSucceeded", snapshot.MovementTeleportCorrectionMouseCaptureSucceeded, true);
            Append(builder, "MovementTeleportCorrectionMouseApplySucceeded", snapshot.MovementTeleportCorrectionMouseApplySucceeded, true);
            Append(builder, "MovementTeleportCorrectionMouseRestoreSucceeded", snapshot.MovementTeleportCorrectionMouseRestoreSucceeded, true);
            Append(builder, "MovementTeleportCorrectionVanillaContinued", snapshot.MovementTeleportCorrectionVanillaContinued, true);
            Append(builder, "MovementTeleportCorrectionLastCompatError", snapshot.MovementTeleportCorrectionLastCompatError, true);
            Append(builder, "MovementTeleportCorrectionAppliedCount", snapshot.MovementTeleportCorrectionAppliedCount, true);
            Append(builder, "MovementTeleportCorrectionSkippedCount", snapshot.MovementTeleportCorrectionSkippedCount, true);
            Append(builder, "MovementSafeLandingEnabled", snapshot.MovementSafeLandingEnabled, true);
            Append(builder, "MovementSafeLandingLastTriggered", snapshot.MovementSafeLandingLastTriggered, true);
            Append(builder, "MovementSafeLandingLastTriggerUtc", FormatDate(snapshot.MovementSafeLandingLastTriggerUtc), true);
            Append(builder, "MovementSafeLandingLastDecision", snapshot.MovementSafeLandingLastDecision, true);
            Append(builder, "MovementSafeLandingLastSkipReason", snapshot.MovementSafeLandingLastSkipReason, true);
            Append(builder, "MovementSafeLandingLastDecisionUtc", FormatDate(snapshot.MovementSafeLandingLastDecisionUtc), true);
            Append(builder, "MovementSafeLandingLastTick", snapshot.MovementSafeLandingLastTick, true);
            Append(builder, "MovementSafeLandingPendingActionCount", snapshot.MovementSafeLandingPendingActionCount, true);
            Append(builder, "MovementSafeLandingRunningActionKind", snapshot.MovementSafeLandingRunningActionKind, true);
            Append(builder, "MovementSafeLandingTextInputFocused", snapshot.MovementSafeLandingTextInputFocused, true);
            Append(builder, "MovementSafeLandingTextInputReason", snapshot.MovementSafeLandingTextInputReason, true);
            Append(builder, "MovementSafeLandingPlayerControllable", snapshot.MovementSafeLandingPlayerControllable, true);
            Append(builder, "MovementSafeLandingDangerous", snapshot.MovementSafeLandingDangerous, true);
            Append(builder, "MovementSafeLandingRescueWindow", snapshot.MovementSafeLandingRescueWindow, true);
            Append(builder, "MovementSafeLandingAlreadySafe", snapshot.MovementSafeLandingAlreadySafe, true);
            Append(builder, "MovementSafeLandingSafeReason", snapshot.MovementSafeLandingSafeReason, true);
            Append(builder, "MovementSafeLandingRawCreativeGodMode", snapshot.MovementSafeLandingRawCreativeGodMode, true);
            Append(builder, "MovementSafeLandingRawNoFallDmg", snapshot.MovementSafeLandingRawNoFallDmg, true);
            Append(builder, "MovementSafeLandingRawSlowFall", snapshot.MovementSafeLandingRawSlowFall, true);
            Append(builder, "MovementSafeLandingRawWet", snapshot.MovementSafeLandingRawWet, true);
            Append(builder, "MovementSafeLandingRawHoneyWet", snapshot.MovementSafeLandingRawHoneyWet, true);
            Append(builder, "MovementSafeLandingRawShimmering", snapshot.MovementSafeLandingRawShimmering, true);
            Append(builder, "MovementSafeLandingRawWebbed", snapshot.MovementSafeLandingRawWebbed, true);
            Append(builder, "MovementSafeLandingRawStoned", snapshot.MovementSafeLandingRawStoned, true);
            Append(builder, "MovementSafeLandingRawGrapCount", snapshot.MovementSafeLandingRawGrapCount, true);
            Append(builder, "MovementSafeLandingRawEquippedWingCount", snapshot.MovementSafeLandingRawEquippedWingCount, true);
            Append(builder, "MovementSafeLandingRawMountNoFallDamage", snapshot.MovementSafeLandingRawMountNoFallDamage, true);
            Append(builder, "MovementSafeLandingRawExtraFall", snapshot.MovementSafeLandingRawExtraFall, true);
            Append(builder, "MovementSafeLandingFallingSpeed", snapshot.MovementSafeLandingFallingSpeed, true);
            Append(builder, "MovementSafeLandingVelocityY", snapshot.MovementSafeLandingVelocityY, true);
            Append(builder, "MovementSafeLandingGravityDirection", snapshot.MovementSafeLandingGravityDirection, true);
            Append(builder, "MovementSafeLandingImpactFound", snapshot.MovementSafeLandingImpactFound, true);
            Append(builder, "MovementSafeLandingImpactDistancePixels", snapshot.MovementSafeLandingImpactDistancePixels, true);
            Append(builder, "MovementSafeLandingImpactTicks", snapshot.MovementSafeLandingImpactTicks, true);
            Append(builder, "MovementSafeLandingEstimatedFallTiles", snapshot.MovementSafeLandingEstimatedFallTiles, true);
            Append(builder, "MovementSafeLandingActiveCapabilitySummary", snapshot.MovementSafeLandingActiveCapabilitySummary, true);
            Append(builder, "MovementSafeLandingSelectedStrategyId", snapshot.MovementSafeLandingSelectedStrategyId, true);
            Append(builder, "MovementSafeLandingSelectedPriority", snapshot.MovementSafeLandingSelectedPriority, true);
            Append(builder, "MovementSafeLandingSelectedActionType", snapshot.MovementSafeLandingSelectedActionType, true);
            Append(builder, "MovementSafeLandingHasFlyingCarpet", snapshot.MovementSafeLandingHasFlyingCarpet, true);
            Append(builder, "MovementSafeLandingHasFlyingCarpetAvailable", snapshot.MovementSafeLandingHasFlyingCarpetAvailable, true);
            Append(builder, "MovementSafeLandingFlyingCarpetTime", snapshot.MovementSafeLandingFlyingCarpetTime, true);
            Append(builder, "MovementSafeLandingHasGravityGlobe", snapshot.MovementSafeLandingHasGravityGlobe, true);
            Append(builder, "MovementSafeLandingHasGravityFlipOpportunity", snapshot.MovementSafeLandingHasGravityFlipOpportunity, true);
            Append(builder, "MovementSafeLandingHasEquippedFlyingMount", snapshot.MovementSafeLandingHasEquippedFlyingMount, true);
            Append(builder, "MovementSafeLandingHasEquippedSafeMount", snapshot.MovementSafeLandingHasEquippedSafeMount, true);
            Append(builder, "MovementSafeLandingHasEquippedGrapple", snapshot.MovementSafeLandingHasEquippedGrapple, true);
            Append(builder, "MovementSafeLandingHasInventoryGrapple", snapshot.MovementSafeLandingHasInventoryGrapple, true);
            Append(builder, "MovementSafeLandingHasTeleportRod", snapshot.MovementSafeLandingHasTeleportRod, true);
            Append(builder, "MovementSafeLandingTeleportRodInventorySlot", snapshot.MovementSafeLandingTeleportRodInventorySlot, true);
            Append(builder, "MovementSafeLandingTeleportRodItemType", snapshot.MovementSafeLandingTeleportRodItemType, true);
            Append(builder, "MovementSafeLandingTeleportTargetKnown", snapshot.MovementSafeLandingTeleportTargetKnown, true);
            Append(builder, "MovementSafeLandingTeleportTargetTileX", snapshot.MovementSafeLandingTeleportTargetTileX, true);
            Append(builder, "MovementSafeLandingTeleportTargetTileY", snapshot.MovementSafeLandingTeleportTargetTileY, true);
            Append(builder, "MovementSafeLandingTeleportTargetWorldX", snapshot.MovementSafeLandingTeleportTargetWorldX, true);
            Append(builder, "MovementSafeLandingTeleportTargetWorldY", snapshot.MovementSafeLandingTeleportTargetWorldY, true);
            Append(builder, "MovementSafeLandingHasCushionBlock", snapshot.MovementSafeLandingHasCushionBlock, true);
            Append(builder, "MovementSafeLandingCushionBlockInventorySlot", snapshot.MovementSafeLandingCushionBlockInventorySlot, true);
            Append(builder, "MovementSafeLandingCushionBlockHotbarSlot", snapshot.MovementSafeLandingCushionBlockHotbarSlot, true);
            Append(builder, "MovementSafeLandingCushionBlockItemType", snapshot.MovementSafeLandingCushionBlockItemType, true);
            Append(builder, "MovementSafeLandingCushionBlockCreateTile", snapshot.MovementSafeLandingCushionBlockCreateTile, true);
            Append(builder, "MovementSafeLandingBlockPlacementTargetKnown", snapshot.MovementSafeLandingBlockPlacementTargetKnown, true);
            Append(builder, "MovementSafeLandingBlockPlacementTileX", snapshot.MovementSafeLandingBlockPlacementTileX, true);
            Append(builder, "MovementSafeLandingBlockPlacementTileY", snapshot.MovementSafeLandingBlockPlacementTileY, true);
            Append(builder, "MovementSafeLandingBlockPlacementWorldX", snapshot.MovementSafeLandingBlockPlacementWorldX, true);
            Append(builder, "MovementSafeLandingBlockPlacementWorldY", snapshot.MovementSafeLandingBlockPlacementWorldY, true);
            Append(builder, "MovementSafeLandingGravityRestorePending", snapshot.MovementSafeLandingGravityRestorePending, true);
            Append(builder, "MovementSafeLandingGravityRestoreOriginalDirection", snapshot.MovementSafeLandingGravityRestoreOriginalDirection, true);
            Append(builder, "MovementSafeLandingGravityRestorePendingTicks", snapshot.MovementSafeLandingGravityRestorePendingTicks, true);
            Append(builder, "MovementSafeLandingGravityRestoreLastDecision", snapshot.MovementSafeLandingGravityRestoreLastDecision, true);
            Append(builder, "MovementSafeLandingGravityRestoreLastSkipReason", snapshot.MovementSafeLandingGravityRestoreLastSkipReason, true);
            Append(builder, "MovementSafeLandingConfigSummary", snapshot.MovementSafeLandingConfigSummary, true);
            Append(builder, "MovementSafeLandingStageSummary", snapshot.MovementSafeLandingStageSummary, true);
                        Append(builder, "MovementSafeLandingLandingSurfaceKnown", snapshot.MovementSafeLandingLandingSurfaceKnown, true);
            Append(builder, "MovementSafeLandingLandingContactWorldX", snapshot.MovementSafeLandingLandingContactWorldX, true);
            Append(builder, "MovementSafeLandingLandingContactWorldY", snapshot.MovementSafeLandingLandingContactWorldY, true);
            Append(builder, "MovementSafeLandingLandingContactTileX", snapshot.MovementSafeLandingLandingContactTileX, true);
            Append(builder, "MovementSafeLandingLandingContactTileY", snapshot.MovementSafeLandingLandingContactTileY, true);
            Append(builder, "MovementSafeLandingLandingSurfaceKind", snapshot.MovementSafeLandingLandingSurfaceKind, true);
            Append(builder, "MovementSafeLandingLandingSlopeType", snapshot.MovementSafeLandingLandingSlopeType, true);
            Append(builder, "MovementSafeLandingLandingSlopeDirection", snapshot.MovementSafeLandingLandingSlopeDirection, true);
            Append(builder, "MovementSafeLandingLandingContactSample", snapshot.MovementSafeLandingLandingContactSample, true);
            Append(builder, "MovementSafeLandingLandingMovingIntoSlope", snapshot.MovementSafeLandingLandingMovingIntoSlope, true);
            Append(builder, "MovementSafeLandingLandingMovingWithSlope", snapshot.MovementSafeLandingLandingMovingWithSlope, true);
            Append(builder, "MovementSafeLandingLandingSurfaceSummary", snapshot.MovementSafeLandingLandingSurfaceSummary, true);
            Append(builder, "MovementSafeLandingGrappleHookSpeed", snapshot.MovementSafeLandingGrappleHookSpeed, true);
            Append(builder, "MovementSafeLandingGrappleTargetSource", snapshot.MovementSafeLandingGrappleTargetSource, true);
            Append(builder, "MovementSafeLandingGrappleTargetFromLandingSurface", snapshot.MovementSafeLandingGrappleTargetFromLandingSurface, true);
            Append(builder, "MovementSafeLandingGrappleTargetDistancePixels", snapshot.MovementSafeLandingGrappleTargetDistancePixels, true);
            Append(builder, "MovementSafeLandingGrappleHookVerticalSpeed", snapshot.MovementSafeLandingGrappleHookVerticalSpeed, true);
            Append(builder, "MovementSafeLandingGrappleRelativeDownSpeed", snapshot.MovementSafeLandingGrappleRelativeDownSpeed, true);
            Append(builder, "MovementSafeLandingGrappleRequiredLeadTicks", snapshot.MovementSafeLandingGrappleRequiredLeadTicks, true);
            Append(builder, "MovementSafeLandingGrappleRequiredLeadPixels", snapshot.MovementSafeLandingGrappleRequiredLeadPixels, true);
            Append(builder, "MovementSafeLandingGrappleEstimatedTicksToTarget", snapshot.MovementSafeLandingGrappleEstimatedTicksToTarget, true);
            Append(builder, "MovementSafeLandingGrappleTooEarly", snapshot.MovementSafeLandingGrappleTooEarly, true);
            Append(builder, "MovementSafeLandingGrappleTooLate", snapshot.MovementSafeLandingGrappleTooLate, true);
            Append(builder, "MovementSafeLandingGrappleTooSlowForDownwardSurface", snapshot.MovementSafeLandingGrappleTooSlowForDownwardSurface, true);
            Append(builder, "MovementSafeLandingGrappleTimingSummary", snapshot.MovementSafeLandingGrappleTimingSummary, true);
            Append(builder, "MovementSafeLandingEquippedGrappleShootSpeed", snapshot.MovementSafeLandingEquippedGrappleShootSpeed, true);
            Append(builder, "MovementSafeLandingInventoryGrappleShootSpeed", snapshot.MovementSafeLandingInventoryGrappleShootSpeed, true);
            Append(builder, "MovementSafeLandingEquippedGrappleProjectileType", snapshot.MovementSafeLandingEquippedGrappleProjectileType, true);
            Append(builder, "MovementSafeLandingInventoryGrappleProjectileType", snapshot.MovementSafeLandingInventoryGrappleProjectileType, true);
            Append(builder, "MovementSafeLandingMaxFallSpeed", snapshot.MovementSafeLandingMaxFallSpeed, true);Append(builder, "MovementSafeLandingStrategyCatalogVersion", snapshot.MovementSafeLandingStrategyCatalogVersion, true);
            Append(builder, "MovementSafeLandingStrategyEvaluationSummary", snapshot.MovementSafeLandingStrategyEvaluationSummary, true);
            Append(builder, "MovementSafeLandingCandidateSummary", snapshot.MovementSafeLandingCandidateSummary, true);
            Append(builder, "MovementSafeLandingSelectedPlanSummary", snapshot.MovementSafeLandingSelectedPlanSummary, true);
            Append(builder, "MovementSafeLandingRejectedStrategiesSummary", snapshot.MovementSafeLandingRejectedStrategiesSummary, true);
            Append(builder, "MovementSafeLandingPostApplyVerificationSummary", snapshot.MovementSafeLandingPostApplyVerificationSummary, true);
            Append(builder, "MovementSafeLandingRecoveryStateSummary", snapshot.MovementSafeLandingRecoveryStateSummary, true);
            Append(builder, "MovementSafeLandingSubmittedCount", snapshot.MovementSafeLandingSubmittedCount, true);
            Append(builder, "MovementSafeLandingSkippedCount", snapshot.MovementSafeLandingSkippedCount, true);
            Append(builder, "MovementSafeLandingFullAnalysisCount", snapshot.MovementSafeLandingFullAnalysisCount, true);
            Append(builder, "MovementSafeLandingCheapPrecheckSkipCount", snapshot.MovementSafeLandingCheapPrecheckSkipCount, true);
            Append(builder, "MovementSafeLandingLandingProbeCount", snapshot.MovementSafeLandingLandingProbeCount, true);
            Append(builder, "MovementSafeLandingConfigSummaryCacheHitCount", snapshot.MovementSafeLandingConfigSummaryCacheHitCount, true);
            Append(builder, "MovementSafeLandingConfigSummaryCacheMissCount", snapshot.MovementSafeLandingConfigSummaryCacheMissCount, true);
            Append(builder, "MovementSafeLandingStageSummaryCacheHitCount", snapshot.MovementSafeLandingStageSummaryCacheHitCount, true);
            Append(builder, "MovementSafeLandingCheapSkipDiagnosticSuppressedCount", snapshot.MovementSafeLandingCheapSkipDiagnosticSuppressedCount, true);
            Append(builder, "MovementSafeLandingCheapSkipDiagnosticWrittenCount", snapshot.MovementSafeLandingCheapSkipDiagnosticWrittenCount, true);
            Append(builder, "MovementSafeLandingCheapSkipLastReason", snapshot.MovementSafeLandingCheapSkipLastReason, true);
            Append(builder, "MovementSafeLandingCheapSkipDiagnosticCadenceTicks", snapshot.MovementSafeLandingCheapSkipDiagnosticCadenceTicks, true);
            Append(builder, "MovementSafeLandingRecoverySummarySkippedCount", snapshot.MovementSafeLandingRecoverySummarySkippedCount, true);
            Append(builder, "MovementSafeLandingLastCompatError", snapshot.MovementSafeLandingLastCompatError, true);
            Append(builder, "MovementSafeLandingCollisionFastPathStatus", snapshot.MovementSafeLandingCollisionFastPathStatus, true);
            Append(builder, "MovementSafeLandingPlayerUpdateHookInstalled", snapshot.MovementSafeLandingPlayerUpdateHookInstalled, true);
            Append(builder, "MovementSafeLandingPlayerUpdateHookMessage", snapshot.MovementSafeLandingPlayerUpdateHookMessage, true);
            Append(builder, "MovementSafeLandingQueuedJumpPulseActive", snapshot.MovementSafeLandingQueuedJumpPulseActive, true);
            Append(builder, "MovementSafeLandingQueuedJumpPulseStatus", snapshot.MovementSafeLandingQueuedJumpPulseStatus, true);
            Append(builder, "MovementSafeLandingQueuedJumpPulseApplySite", snapshot.MovementSafeLandingQueuedJumpPulseApplySite, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentApplied", snapshot.MovementSafeLandingTemporaryEquipmentApplied, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentPendingRestoreCount", snapshot.MovementSafeLandingTemporaryEquipmentPendingRestoreCount, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentPendingRestoreNoSpaceCount", snapshot.MovementSafeLandingTemporaryEquipmentPendingRestoreNoSpaceCount, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentLastDecision", snapshot.MovementSafeLandingTemporaryEquipmentLastDecision, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentLastSkipReason", snapshot.MovementSafeLandingTemporaryEquipmentLastSkipReason, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedCategory", snapshot.MovementSafeLandingTemporaryEquipmentSelectedCategory, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedSourceKind", snapshot.MovementSafeLandingTemporaryEquipmentSelectedSourceKind, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedSourceSlot", snapshot.MovementSafeLandingTemporaryEquipmentSelectedSourceSlot, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedTargetKind", snapshot.MovementSafeLandingTemporaryEquipmentSelectedTargetKind, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedTargetSlot", snapshot.MovementSafeLandingTemporaryEquipmentSelectedTargetSlot, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedItemType", snapshot.MovementSafeLandingTemporaryEquipmentSelectedItemType, true);
            Append(builder, "MovementSafeLandingTemporaryEquipmentSelectedMountType", snapshot.MovementSafeLandingTemporaryEquipmentSelectedMountType, true);
            Append(builder, "CombatAutoFacingEnabled", snapshot.CombatAutoFacingEnabled, true);
            Append(builder, "CombatAutoFacingLastDecision", snapshot.CombatAutoFacingLastDecision, true);
            Append(builder, "CombatAutoFacingLastSkipReason", snapshot.CombatAutoFacingLastSkipReason, true);
            Append(builder, "CombatAutoFacingLastDecisionUtc", FormatDate(snapshot.CombatAutoFacingLastDecisionUtc), true);
            Append(builder, "CombatAutoFacingLastTick", snapshot.CombatAutoFacingLastTick, true);
            Append(builder, "CombatAutoFacingSelectedSlot", snapshot.CombatAutoFacingSelectedSlot, true);
            Append(builder, "CombatAutoFacingItemType", snapshot.CombatAutoFacingItemType, true);
            Append(builder, "CombatAutoFacingItemName", snapshot.CombatAutoFacingItemName, true);
            Append(builder, "CombatAutoFacingCurrentDirection", snapshot.CombatAutoFacingCurrentDirection, true);
            Append(builder, "CombatAutoFacingDesiredDirection", snapshot.CombatAutoFacingDesiredDirection, true);
            Append(builder, "CombatAutoFacingTargetSource", snapshot.CombatAutoFacingTargetSource, true);
            Append(builder, "CombatAutoFacingTargetWhoAmI", snapshot.CombatAutoFacingTargetWhoAmI, true);
            Append(builder, "CombatAutoFacingTargetType", snapshot.CombatAutoFacingTargetType, true);
            Append(builder, "CombatAutoFacingTargetName", snapshot.CombatAutoFacingTargetName, true);
            Append(builder, "CombatAutoFacingSubmittedCount", snapshot.CombatAutoFacingSubmittedCount, true);
            Append(builder, "CombatAutoFacingSkippedCount", snapshot.CombatAutoFacingSkippedCount, true);
            Append(builder, "CombatPerfectRevolverLastDecision", snapshot.CombatPerfectRevolverLastDecision, true);
            Append(builder, "CombatPerfectRevolverLastSkipReason", snapshot.CombatPerfectRevolverLastSkipReason, true);
            Append(builder, "CombatPerfectRevolverLastDecisionUtc", FormatDate(snapshot.CombatPerfectRevolverLastDecisionUtc), true);
            Append(builder, "CombatFlailComboEnabled", snapshot.CombatFlailComboEnabled, true);
            Append(builder, "CombatFlailComboRightHeld", snapshot.CombatFlailComboRightHeld, true);
            Append(builder, "CombatFlailComboEligible", snapshot.CombatFlailComboEligible, true);
            Append(builder, "CombatFlailComboLastDecision", snapshot.CombatFlailComboLastDecision, true);
            Append(builder, "CombatFlailComboLastReason", snapshot.CombatFlailComboLastReason, true);
            Append(builder, "CombatFlailComboLastDecisionUtc", FormatDate(snapshot.CombatFlailComboLastDecisionUtc), true);
            Append(builder, "CombatFlailComboItemType", snapshot.CombatFlailComboItemType, true);
            Append(builder, "CombatFlailComboProjectileType", snapshot.CombatFlailComboProjectileType, true);
            Append(builder, "CombatFlailComboProjectileAi0", snapshot.CombatFlailComboProjectileAi0, true);
            Append(builder, "CombatFlailComboHitDetected", snapshot.CombatFlailComboHitDetected, true);
            Append(builder, "CombatFlailComboCollisionDetected", snapshot.CombatFlailComboCollisionDetected, true);
            Append(builder, "CombatFlailComboVanillaRightClickBlocked", snapshot.CombatFlailComboVanillaRightClickBlocked, true);
            Append(builder, "CombatFlailComboUiBlocked", snapshot.CombatFlailComboUiBlocked, true);
            Append(builder, "CombatFlailComboScopedPress", snapshot.CombatFlailComboScopedPress, true);
            Append(builder, "CombatFlailComboScopedRelease", snapshot.CombatFlailComboScopedRelease, true);
            Append(builder, "CombatFlailComboRestoreOk", snapshot.CombatFlailComboRestoreOk, true);
            Append(builder, "CombatFlailComboAppliedCount", snapshot.CombatFlailComboAppliedCount, true);
            Append(builder, "CombatFlailComboSkippedCount", snapshot.CombatFlailComboSkippedCount, true);
            Append(builder, "CombatPhasebladeQuickSwitchEnabled", snapshot.CombatPhasebladeQuickSwitchEnabled, true);
            Append(builder, "CombatPhasebladeQuickSwitchRightHeld", snapshot.CombatPhasebladeQuickSwitchRightHeld, true);
            Append(builder, "CombatPhasebladeQuickSwitchEligible", snapshot.CombatPhasebladeQuickSwitchEligible, true);
            Append(builder, "CombatPhasebladeQuickSwitchLastDecision", snapshot.CombatPhasebladeQuickSwitchLastDecision, true);
            Append(builder, "CombatPhasebladeQuickSwitchLastReason", snapshot.CombatPhasebladeQuickSwitchLastReason, true);
            Append(builder, "CombatPhasebladeQuickSwitchLastDecisionUtc", FormatDate(snapshot.CombatPhasebladeQuickSwitchLastDecisionUtc), true);
            Append(builder, "CombatPhasebladeQuickSwitchCurrentSlot", snapshot.CombatPhasebladeQuickSwitchCurrentSlot, true);
            Append(builder, "CombatPhasebladeQuickSwitchNextSlot", snapshot.CombatPhasebladeQuickSwitchNextSlot, true);
            Append(builder, "CombatPhasebladeQuickSwitchEligibleSlotCount", snapshot.CombatPhasebladeQuickSwitchEligibleSlotCount, true);
            Append(builder, "CombatPhasebladeQuickSwitchIntervalTicks", snapshot.CombatPhasebladeQuickSwitchIntervalTicks, true);
            Append(builder, "CombatPhasebladeQuickSwitchScopedPress", snapshot.CombatPhasebladeQuickSwitchScopedPress, true);
            Append(builder, "CombatPhasebladeQuickSwitchScopedRelease", snapshot.CombatPhasebladeQuickSwitchScopedRelease, true);
            Append(builder, "CombatPhasebladeQuickSwitchRestoreOk", snapshot.CombatPhasebladeQuickSwitchRestoreOk, true);
            Append(builder, "CombatPhasebladeQuickSwitchAppliedCount", snapshot.CombatPhasebladeQuickSwitchAppliedCount, true);
            Append(builder, "CombatPhasebladeQuickSwitchSkippedCount", snapshot.CombatPhasebladeQuickSwitchSkippedCount, true);
            Append(builder, "CombatItemCheckAutoClickerLastDecision", snapshot.CombatItemCheckAutoClickerLastDecision, true);
            Append(builder, "CombatItemCheckAutoClickerLastReason", snapshot.CombatItemCheckAutoClickerLastReason, true);
            Append(builder, "CombatItemCheckAutoClickerLastDecisionUtc", FormatDate(snapshot.CombatItemCheckAutoClickerLastDecisionUtc), true);
            Append(builder, "CombatItemCheckAutoClickerLastItemType", snapshot.CombatItemCheckAutoClickerLastItemType, true);
            Append(builder, "CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable", snapshot.CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable, true);
            Append(builder, "CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons", snapshot.CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons, true);
            Append(builder, "CombatItemCheckAutoClickerScopedPress", snapshot.CombatItemCheckAutoClickerScopedPress, true);
            Append(builder, "CombatItemCheckAutoClickerScopedRelease", snapshot.CombatItemCheckAutoClickerScopedRelease, true);
            Append(builder, "CombatItemCheckAutoClickerRestored", snapshot.CombatItemCheckAutoClickerRestored, true);
            Append(builder, "CombatItemCheckAutoClickerAppliedCount", snapshot.CombatItemCheckAutoClickerAppliedCount, true);
            Append(builder, "CombatItemCheckAutoClickerSkippedCount", snapshot.CombatItemCheckAutoClickerSkippedCount, true);
            Append(builder, "CombatMagicStringClickerLastDecision", snapshot.CombatMagicStringClickerLastDecision, true);
            Append(builder, "CombatMagicStringClickerLastSkipReason", snapshot.CombatMagicStringClickerLastSkipReason, true);
            Append(builder, "CombatMagicStringClickerLastDecisionUtc", FormatDate(snapshot.CombatMagicStringClickerLastDecisionUtc), true);
            AppendAutoRecovery(builder, snapshot, true);
            Append(builder, "LastError", snapshot.LastError, false);
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static void Append(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": \"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Append(StringBuilder builder, string name, double value, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendDictionary(StringBuilder builder, string name, IDictionary<string, int> values, bool comma)
        {
            builder.Append("  \"").Append(Escape(name)).Append("\": {");

            if (values != null && values.Count > 0)
            {
                var first = true;
                foreach (var pair in values)
                {
                    if (!first)
                    {
                        builder.Append(",");
                    }

                    builder.Append(" \"").Append(Escape(pair.Key)).Append("\": ")
                        .Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                    first = false;
                }

                builder.Append(" ");
            }

            builder.Append("}");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendAutoRecovery(StringBuilder builder, DiagnosticSnapshot snapshot, bool comma)
        {
            builder.AppendLine("  \"AutoRecovery\": {");
            AppendNested(builder, "autoHealEnabled", snapshot.AutoHealEnabled, true);
            AppendNested(builder, "autoManaEnabled", snapshot.AutoManaEnabled, true);
            AppendNested(builder, "autoBuffEnabled", snapshot.AutoBuffEnabled, true);
            AppendNested(builder, "autoNurseEnabled", snapshot.AutoNurseEnabled, true);
            AppendNested(builder, "autoStationBuffEnabled", snapshot.AutoStationBuffEnabled, true);
            AppendNested(builder, "autoHealMode", snapshot.AutoHealMode, true);
            AppendNested(builder, "autoManaMode", snapshot.AutoManaMode, true);
            AppendNested(builder, "autoHealThresholdPercent", snapshot.AutoHealThresholdPercent, true);
            AppendNested(builder, "autoManaThresholdPercent", snapshot.AutoManaThresholdPercent, true);
            AppendNested(builder, "autoHealCooldownTicks", snapshot.AutoHealCooldownTicks, true);
            AppendNested(builder, "autoManaCooldownTicks", snapshot.AutoManaCooldownTicks, true);
            AppendNested(builder, "autoBuffCooldownTicks", snapshot.AutoBuffCooldownTicks, true);
            AppendNested(builder, "lastAutoHealResult", snapshot.LastAutoHealResult, true);
            AppendNested(builder, "lastAutoManaResult", snapshot.LastAutoManaResult, true);
            AppendNested(builder, "lastAutoBuffResult", snapshot.LastAutoBuffResult, true);
            AppendNested(builder, "lastAutoNurseResult", snapshot.LastAutoNurseResult, true);
            AppendNested(builder, "lastAutoStationBuffResult", snapshot.LastAutoStationBuffResult, true);
            AppendNested(builder, "lastAutoHealTick", snapshot.LastAutoHealTick, true);
            AppendNested(builder, "lastAutoManaTick", snapshot.LastAutoManaTick, true);
            AppendNested(builder, "lastAutoBuffTick", snapshot.LastAutoBuffTick, true);
            AppendNested(builder, "lastAutoNurseTick", snapshot.LastAutoNurseTick, true);
            AppendNested(builder, "lastAutoStationBuffTick", snapshot.LastAutoStationBuffTick, true);
            AppendNested(builder, "autoStationBuffCooldownFastSkipCount", snapshot.AutoStationBuffCooldownFastSkipCount, true);
            AppendNested(builder, "autoStationBuffActiveBuffFastSkipCount", snapshot.AutoStationBuffActiveBuffFastSkipCount, true);
            AppendNested(builder, "autoStationBuffScanCount", snapshot.AutoStationBuffScanCount, true);
            AppendNested(builder, "autoStationBuffScanCacheHitCount", snapshot.AutoStationBuffScanCacheHitCount, true);
            AppendNested(builder, "autoStationBuffScanCacheMissCount", snapshot.AutoStationBuffScanCacheMissCount, true);
            AppendNested(builder, "autoStationBuffTilesVisitedLast", snapshot.AutoStationBuffTilesVisitedLast, true);
            AppendNested(builder, "autoStationBuffLastScanMs", snapshot.AutoStationBuffLastScanMs, true);
            AppendNested(builder, "autoStationBuffTileFastPathStatus", snapshot.AutoStationBuffTileFastPathStatus, true);
            AppendNested(builder, "autoStationBuffLastDecision", snapshot.AutoStationBuffLastDecision, true);
            AppendNested(builder, "lastAutoBuffCountBefore", snapshot.LastAutoBuffCountBefore, true);
            AppendNested(builder, "lastAutoBuffCountAfter", snapshot.LastAutoBuffCountAfter, true);
            AppendNested(builder, "quickHealCapability", snapshot.QuickHealCapability, true);
            AppendNested(builder, "quickManaCapability", snapshot.QuickManaCapability, true);
            AppendNested(builder, "quickBuffCapability", snapshot.QuickBuffCapability, false);
            builder.Append("  }");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendNested(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("    \"").Append(Escape(name)).Append("\": \"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendNested(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("    \"").Append(Escape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendNested(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("    \"").Append(Escape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendNested(StringBuilder builder, string name, long value, bool comma)
        {
            builder.Append("    \"").Append(Escape(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void AppendNested(StringBuilder builder, string name, double value, bool comma)
        {
            builder.Append("    \"").Append(Escape(name)).Append("\": ").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
