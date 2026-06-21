using JueMingZ.Automation.Blueprint;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        private static void WriteBlueprintProjection(DiagnosticSnapshot snapshot, RuntimeDiagnosticSnapshotSource source)
        {
            var handheld = BlueprintHandheldActionBarState.BuildDiagnostics(
                source.SettingsSnapshot,
                source.GameState,
                BuildBlueprintHandheldActionBarEnvironment(source));
            snapshot.BlueprintHandheldActionBarVisible = handheld.Visible;
            snapshot.BlueprintHandheldActionBarBlockedReason = handheld.BlockedReason;
            snapshot.BlueprintHandheldActionBarToolItemId = handheld.ToolItemId;
            snapshot.BlueprintHandheldActionBarSelectedItemType = handheld.SelectedItemType;
            snapshot.BlueprintHandheldActionBarLastAction = handheld.LastAction;
            snapshot.BlueprintHandheldActionBarLastResultCode = handheld.LastResultCode;
            snapshot.BlueprintHandheldActionBarHoveredButtonId = handheld.HoveredButtonId;
            snapshot.BlueprintHandheldActionBarPressedButtonId = handheld.PressedButtonId;
            snapshot.BlueprintHandheldActionBarLastMouseReadMode = handheld.LastMouseReadMode;
            snapshot.BlueprintHandheldActionBarLastOwnershipReason = handheld.LastOwnershipReason;
            var uiClick = BlueprintUiClickDiagnostics.GetSnapshot();
            snapshot.BlueprintHandheldActionBarLastInputTrace = uiClick.HandheldInputTrace;
            snapshot.BlueprintHandheldActionBarLastOwnershipTrace = uiClick.HandheldOwnershipTrace;
            snapshot.BlueprintWorldOverlayLastInputTrace = uiClick.WorldOverlayInputTrace;

            var mirror = BlueprintMirrorService.GetDiagnostics();
            snapshot.BlueprintMirrorLastStatus = mirror.LastStatus;
            snapshot.BlueprintMirrorLastMessage = mirror.LastMessage;
            snapshot.BlueprintMirrorMode = mirror.LastMode;
            snapshot.BlueprintMirrorTemplateId = mirror.LastTemplateId;
            snapshot.BlueprintMirrorTemplateName = mirror.LastTemplateName;
            snapshot.BlueprintMirrorBlockedReason = mirror.LastBlockedReason;
            snapshot.BlueprintMirrorMirroredCellCount = mirror.LastMirroredCellCount;
            snapshot.BlueprintMirrorMirroredLayerCount = mirror.LastMirroredLayerCount;
            snapshot.BlueprintMirrorRejectedLayerCount = mirror.LastRejectedLayerCount;
            snapshot.BlueprintMirrorLastAttemptedUtc = mirror.LastAttemptedUtc;

            var projection = BlueprintProjectionService.GetDiagnostics();
            snapshot.BlueprintProjectionLastStatus = projection.ResultCode;
            snapshot.BlueprintProjectionLastMessage = projection.Message;
            snapshot.BlueprintProjectionWorldPairKey = projection.WorldPairKey;
            snapshot.BlueprintProjectionWorldKey = projection.WorldKey;
            snapshot.BlueprintProjectionInstanceCount = projection.InstanceCount;
            snapshot.BlueprintProjectionVisibleInstanceCount = projection.VisibleInstanceCount;
            snapshot.BlueprintProjectionHiddenInstanceCount = projection.HiddenInstanceCount;
            snapshot.BlueprintProjectionEffectiveLayerCount = projection.EffectiveLayerCount;
            snapshot.BlueprintProjectionFulfilledLayerCount = projection.FulfilledLayerCount;
            snapshot.BlueprintProjectionMissingLayerCount = projection.MissingLayerCount;
            snapshot.BlueprintProjectionConflictLayerCount = projection.ConflictLayerCount;
            snapshot.BlueprintProjectionCoveredLayerCount = projection.CoveredLayerCount;
            snapshot.BlueprintProjectionErasedLayerCount = projection.ErasedLayerCount;
            snapshot.BlueprintProjectionUnavailableLayerCount = projection.UnavailableLayerCount;
            snapshot.BlueprintProjectionCacheHitCount = projection.CacheHitCount;
            snapshot.BlueprintProjectionCacheMissCount = projection.CacheMissCount;
            snapshot.BlueprintProjectionLastResolveElapsedMs = projection.LastResolveElapsedMs;
            snapshot.BlueprintProjectionLastResolvedUtc = projection.LastResolvedUtc;

            var materials = BlueprintMaterialService.GetDiagnostics();
            snapshot.BlueprintMaterialsLastStatus = materials.ResultCode;
            snapshot.BlueprintMaterialsLastMessage = materials.Message;
            snapshot.BlueprintMaterialsWorldPairKey = materials.WorldPairKey;
            snapshot.BlueprintMaterialsWorldKey = materials.WorldKey;
            snapshot.BlueprintMaterialsProjectionStatus = materials.ProjectionResultCode;
            snapshot.BlueprintMaterialsRequiredItemCount = materials.RequiredItemCount;
            snapshot.BlueprintMaterialsMissingItemCount = materials.MissingItemCount;
            snapshot.BlueprintMaterialsRequiredStackTotal = materials.RequiredStackTotal;
            snapshot.BlueprintMaterialsAvailableStackTotal = materials.AvailableStackTotal;
            snapshot.BlueprintMaterialsMissingStackTotal = materials.MissingStackTotal;
            snapshot.BlueprintMaterialsProjectionMissingLayerCount = materials.ProjectionMissingLayerCount;
            snapshot.BlueprintMaterialsMaterializedMissingLayerCount = materials.MaterializedMissingLayerCount;
            snapshot.BlueprintMaterialsSkippedFulfilledLayerCount = materials.SkippedFulfilledLayerCount;
            snapshot.BlueprintMaterialsSkippedConflictLayerCount = materials.SkippedConflictLayerCount;
            snapshot.BlueprintMaterialsSkippedUnavailableLayerCount = materials.SkippedUnavailableLayerCount;
            snapshot.BlueprintMaterialsSkippedMissingLayerWithoutMaterialCount = materials.SkippedMissingLayerWithoutMaterialCount;
            snapshot.BlueprintMaterialsInventoryReadSucceeded = materials.InventoryReadSucceeded;
            snapshot.BlueprintMaterialsInventoryReadStatus = materials.InventoryReadStatus;
            snapshot.BlueprintMaterialsInventoryReadMessage = materials.InventoryReadMessage;
            snapshot.BlueprintMaterialsInventoryMainStackTotal = materials.InventoryMainStackTotal;
            snapshot.BlueprintMaterialsInventoryVoidBagStackTotal = materials.InventoryVoidBagStackTotal;
            snapshot.BlueprintMaterialsWindowVisible = materials.WindowVisible;
            snapshot.BlueprintMaterialsWindowOpacityPercent = materials.WindowOpacityPercent;
            snapshot.BlueprintMaterialsCacheHitCount = materials.CacheHitCount;
            snapshot.BlueprintMaterialsCacheMissCount = materials.CacheMissCount;
            snapshot.BlueprintMaterialsLastResolveElapsedMs = materials.LastResolveElapsedMs;
            snapshot.BlueprintMaterialsLastResolvedUtc = materials.LastResolvedUtc;

            var erase = BlueprintEraseRegionState.GetDiagnostics();
            snapshot.BlueprintEraseRegionActive = erase.Active;
            snapshot.BlueprintEraseRegionDragging = erase.Dragging;
            snapshot.BlueprintEraseRegionHasFixedTarget = erase.HasFixedTarget;
            snapshot.BlueprintEraseRegionTargetInstanceId = erase.TargetInstanceId;
            snapshot.BlueprintEraseRegionTargetInstanceName = erase.TargetInstanceName;
            snapshot.BlueprintEraseRegionTargetLayerOrder = erase.TargetLayerOrder;
            snapshot.BlueprintEraseRegionWorldPairKey = erase.WorldPairKey;
            snapshot.BlueprintEraseRegionWorldKey = erase.WorldKey;
            snapshot.BlueprintEraseRegionLastErasedCellCount = erase.LastErasedCellCount;
            snapshot.BlueprintEraseRegionTotalEraseCellCount = erase.TotalEraseCellCount;
            snapshot.BlueprintEraseRegionLastStatus = erase.LastResultCode;
            snapshot.BlueprintEraseRegionLastMessage = erase.LastNotice;
            snapshot.BlueprintEraseRegionLastInputOwner = erase.LastInputOwner;

            var autoPlacement = BlueprintAutoPlacementService.GetDiagnostics();
            snapshot.BlueprintAutoPlacementEnabled = autoPlacement.Enabled;
            snapshot.BlueprintAutoPlacementLastStatus = autoPlacement.ResultCode;
            snapshot.BlueprintAutoPlacementLastMessage = autoPlacement.Message;
            snapshot.BlueprintAutoPlacementWorldPairKey = autoPlacement.WorldPairKey;
            snapshot.BlueprintAutoPlacementWorldKey = autoPlacement.WorldKey;
            snapshot.BlueprintAutoPlacementProjectionStatus = autoPlacement.ProjectionResultCode;
            snapshot.BlueprintAutoPlacementCandidateCount = autoPlacement.CandidateCount;
            snapshot.BlueprintAutoPlacementSkippedFulfilledLayerCount = autoPlacement.SkippedFulfilledLayerCount;
            snapshot.BlueprintAutoPlacementSkippedConflictLayerCount = autoPlacement.SkippedConflictLayerCount;
            snapshot.BlueprintAutoPlacementSkippedUnavailableLayerCount = autoPlacement.SkippedUnavailableLayerCount;
            snapshot.BlueprintAutoPlacementSkippedUnsupportedLayerCount = autoPlacement.SkippedUnsupportedLayerCount;
            snapshot.BlueprintAutoPlacementSkippedNoMaterialLayerCount = autoPlacement.SkippedNoMaterialLayerCount;
            snapshot.BlueprintAutoPlacementSkippedInsufficientMaterialLayerCount = autoPlacement.SkippedInsufficientMaterialLayerCount;
            snapshot.BlueprintAutoPlacementSelectedInstanceId = autoPlacement.SelectedInstanceId;
            snapshot.BlueprintAutoPlacementSelectedInstanceName = autoPlacement.SelectedInstanceName;
            snapshot.BlueprintAutoPlacementSelectedLayerOrder = autoPlacement.SelectedLayerOrder;
            snapshot.BlueprintAutoPlacementSelectedLayerKind = autoPlacement.SelectedLayerKind;
            snapshot.BlueprintAutoPlacementSelectedWorldTileX = autoPlacement.SelectedWorldTileX;
            snapshot.BlueprintAutoPlacementSelectedWorldTileY = autoPlacement.SelectedWorldTileY;
            snapshot.BlueprintAutoPlacementSelectedMaterialItemId = autoPlacement.SelectedMaterialItemId;
            snapshot.BlueprintAutoPlacementSelectedOriginalMaterialItemId = autoPlacement.SelectedOriginalMaterialItemId;
            snapshot.BlueprintAutoPlacementSelectedMaterialStack = autoPlacement.SelectedMaterialStack;
            snapshot.BlueprintAutoPlacementSelectedMaterialAvailableStack = autoPlacement.SelectedMaterialAvailableStack;
            snapshot.BlueprintAutoPlacementSelectedReplacementApplied = autoPlacement.SelectedReplacementApplied;
            snapshot.BlueprintAutoPlacementSelectedReplacementCategory = autoPlacement.SelectedReplacementCategory;
            snapshot.BlueprintAutoPlacementLastAdmissionStatus = autoPlacement.LastAdmissionStatus;
            snapshot.BlueprintAutoPlacementLastAdmissionReason = autoPlacement.LastAdmissionReason;
            snapshot.BlueprintAutoPlacementLastAdmissionKey = autoPlacement.LastAdmissionKey;
            snapshot.BlueprintAutoPlacementLastRequestId = autoPlacement.LastRequestId;
            snapshot.BlueprintAutoPlacementSubmittedCount = autoPlacement.SubmittedCount;
            snapshot.BlueprintAutoPlacementDeniedCount = autoPlacement.DeniedCount;
            snapshot.BlueprintAutoPlacementFailClosedCount = autoPlacement.FailClosedCount;
            snapshot.BlueprintAutoPlacementSucceededCount = autoPlacement.SucceededCount;
            snapshot.BlueprintAutoPlacementAttemptedButUnverifiedCount = autoPlacement.AttemptedButUnverifiedCount;
            snapshot.BlueprintAutoPlacementLastResultCode = autoPlacement.LastResultCode;
            snapshot.BlueprintAutoPlacementLastResolveElapsedMs = autoPlacement.LastResolveElapsedMs;
            snapshot.BlueprintAutoPlacementLastResolvedUtc = autoPlacement.LastResolvedUtc;

            var diagnostics = BlueprintDiagnostics.BuildSnapshot(projection, materials, erase, autoPlacement);
            snapshot.BlueprintDiagnosticsTemplateReadStatus = diagnostics.TemplateReadStatus;
            snapshot.BlueprintDiagnosticsTemplateReadMessage = diagnostics.TemplateReadMessage;
            snapshot.BlueprintDiagnosticsTemplateCount = diagnostics.TemplateCount;
            snapshot.BlueprintDiagnosticsInstanceCount = diagnostics.InstanceCount;
            snapshot.BlueprintDiagnosticsVisibleInstanceCount = diagnostics.VisibleInstanceCount;
            snapshot.BlueprintDiagnosticsHiddenInstanceCount = diagnostics.HiddenInstanceCount;
            snapshot.BlueprintDiagnosticsEffectiveProjectionLayerCount = diagnostics.EffectiveProjectionLayerCount;
            snapshot.BlueprintDiagnosticsErasedProjectionLayerCount = diagnostics.ErasedProjectionLayerCount;
            snapshot.BlueprintDiagnosticsMaterialMissingItemCount = diagnostics.MaterialMissingItemCount;
            snapshot.BlueprintDiagnosticsMaterialMissingStackTotal = diagnostics.MaterialMissingStackTotal;
            snapshot.BlueprintDiagnosticsAutoPlacementEnabled = diagnostics.AutoPlacementEnabled;
            snapshot.BlueprintDiagnosticsAutoPlacementCandidateCount = diagnostics.AutoPlacementCandidateCount;
            snapshot.BlueprintPerformanceSlowEventCount = diagnostics.SlowEventCount;
            snapshot.BlueprintPerformanceLastScenario = diagnostics.LastPerformanceScenario;
            snapshot.BlueprintPerformanceLastElapsedMs = diagnostics.LastPerformanceElapsedMs;
            snapshot.BlueprintProjectionResolveCount = diagnostics.ProjectionResolve.Count;
            snapshot.BlueprintProjectionAverageResolveElapsedMs = diagnostics.ProjectionResolve.AverageElapsedMs;
            snapshot.BlueprintMaterialsResolveCount = diagnostics.MaterialsResolve.Count;
            snapshot.BlueprintMaterialsAverageResolveElapsedMs = diagnostics.MaterialsResolve.AverageElapsedMs;
            snapshot.BlueprintAutoPlacementLastFailureReason = diagnostics.AutoPlacementLastFailureReason;
            snapshot.BlueprintAutoPlacementCandidateScanCount = diagnostics.AutoPlacementCandidateScan.Count;
            snapshot.BlueprintAutoPlacementAverageCandidateScanElapsedMs = diagnostics.AutoPlacementCandidateScan.AverageElapsedMs;
        }

        private static BlueprintHandheldActionBarEnvironment BuildBlueprintHandheldActionBarEnvironment(RuntimeDiagnosticSnapshotSource source)
        {
            var gameState = source == null ? null : source.GameState;
            if (source == null || !source.LateBootstrapCompleted)
            {
                return BlueprintHandheldActionBarEnvironment.Unavailable();
            }

            var environment = new BlueprintHandheldActionBarEnvironment
            {
                WorldReady = gameState != null && gameState.IsInWorld && !gameState.IsInMainMenu && TerrariaMainCompat.IsWorldReady,
                GameInputAvailable = IsGameInputAvailable(gameState),
                MapFullscreenOpen = TerrariaMainCompat.IsMapFullscreenOpen,
                LegacyMainUiVisible = LegacyMainUiState.Visible,
                ScreenWidth = TerrariaMainCompat.ScreenWidth,
                ScreenHeight = TerrariaMainCompat.ScreenHeight
            };

            bool blocked;
            string reason;
            if (GameMode.TryReadLegacyUiBlockedByVanillaMenuLateOnly(out blocked, out reason))
            {
                environment.VanillaMenuReadAvailable = true;
                environment.VanillaMenuBlocked = blocked;
                environment.VanillaMenuReason = reason ?? string.Empty;
            }
            else
            {
                environment.VanillaMenuReadAvailable = false;
                environment.VanillaMenuBlocked = true;
                environment.VanillaMenuReason = "unavailable";
            }

            PopulateBlueprintHandheldDynamicState(environment);
            return environment;
        }

        private static void PopulateBlueprintHandheldDynamicState(BlueprintHandheldActionBarEnvironment environment)
        {
            if (environment == null)
            {
                return;
            }

            var creation = BlueprintCreationMaskState.GetSnapshot();
            environment.BlueprintCreationActive = creation != null && creation.Active;
            environment.BlueprintCreationSelectedCount = creation == null ? 0 : creation.SelectedCount;
            environment.BlueprintCreationHasPendingSelection =
                creation != null &&
                (creation.CompletedPendingCapture || creation.SelectedCount > 0);

            var placed = BlueprintPlacedInstanceUiState.GetCachedSummary();
            var projection = BlueprintProjectionService.GetDiagnostics();
            var placedCount = System.Math.Max(
                placed == null ? 0 : placed.InstanceCount,
                projection == null ? 0 : projection.InstanceCount);
            environment.BlueprintPlacedInstanceCount = placedCount;
            environment.BlueprintHasPlacedInstances = placedCount > 0;
        }
    }
}
