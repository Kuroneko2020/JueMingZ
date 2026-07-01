using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Config;
using JueMingZ.Runtime;
using JueMingZ.UI;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintDiagnosticsAggregateRuntimeSnapshotJson()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-diagnostics-stage17");
            try
            {
                BlueprintDiagnostics.ResetForTesting();
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                BlueprintTemplateRecord template;
                RequireBlueprintSuccess(templateStore.CreateTemplate(CreateSingleMaterialTemplate("诊断护栏", 41, 1101, 2), out template), "create diagnostics template");

                var instanceStore = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord visible;
                BlueprintWorldInstanceRecord hidden;
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-diag17", "world-diag17", template, 10, 20, 0, out visible), "create visible diagnostics instance");
                RequireBlueprintSuccess(instanceStore.CreateInstanceFromTemplate("pair-diag17", "world-diag17", template, 11, 20, 1, out hidden), "create hidden diagnostics instance");

                BlueprintWorldInstanceSnapshot saved;
                RequireBlueprintSuccess(instanceStore.TryLoadWorld("pair-diag17", out saved), "load diagnostics instances");
                var instances = new List<BlueprintWorldInstanceRecord>(saved.Instances);
                instances[1].Hidden = true;
                RequireBlueprintSuccess(instanceStore.SaveWorldInstances("pair-diag17", "world-diag17", instances, out saved), "save hidden diagnostics instance");

                BlueprintProjectionService.SetDependenciesForTesting(
                    instanceStore,
                    BlueprintPlacementWorldContext.Success("pair-diag17", "world-diag17"),
                    new FakeBlueprintWorldTileReader(),
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(
                    new FakeBlueprintMaterialInventoryReader(
                        new Dictionary<int, int>(),
                        new Dictionary<int, int>()),
                    true);

                var settings = AppSettings.CreateDefault();
                settings.BlueprintAutoPlacementEnabled = true;
                BlueprintAutoPlacementService.TickForTesting(
                    new InputActionQueue(),
                    CreateBlueprintInWorldSnapshot(),
                    RuntimeSettingsSnapshot.FromSettings(settings));

                var runtimeSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Initialized = true,
                    Version = "test-blueprint-diagnostics"
                });
                var json = InvokeDiagnosticSnapshotJson(runtimeSnapshot);
                AssertContains(json, "\"BlueprintDiagnosticsTemplateCount\": 1");
                AssertContains(json, "\"BlueprintDiagnosticsInstanceCount\": 2");
                AssertContains(json, "\"BlueprintDiagnosticsVisibleInstanceCount\": 1");
                AssertContains(json, "\"BlueprintDiagnosticsHiddenInstanceCount\": 1");
                AssertContains(json, "\"BlueprintDiagnosticsEffectiveProjectionLayerCount\": 1");
                AssertContains(json, "\"BlueprintDiagnosticsMaterialMissingItemCount\": 1");
                AssertContains(json, "\"BlueprintDiagnosticsMaterialMissingStackTotal\": 2");
                AssertContains(json, "\"BlueprintDiagnosticsAutoPlacementEnabled\": true");
                AssertContains(json, "\"BlueprintDiagnosticsAutoPlacementCandidateCount\": 0");
                AssertContains(json, "\"BlueprintProjectionResolveCount\": 1");
                AssertContains(json, "\"BlueprintProjectionWallTargetLayerCount\": 0");
                AssertContains(json, "\"BlueprintProjectionWallFrameMismatchLayerCount\": 0");
                AssertContains(json, "\"BlueprintMaterialsResolveCount\": 1");
                AssertContains(json, "\"BlueprintAutoPlacementCandidateScanCount\": 1");
                AssertContains(json, "\"BlueprintAutoPlacementLastFailureReason\": \"noCandidate:");
                AssertContains(json, "\"BlueprintAutoPlacementAverageCandidateScanElapsedMs\": ");
                AssertContains(json, "\"BlueprintPerformanceLastScenario\": \"Blueprint.AutoPlacement.CandidateScan\"");
            }
            finally
            {
                BlueprintDiagnostics.ResetForTesting();
                BlueprintAutoPlacementService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintEraseRegionState.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintDiagnosticsPerformanceCountersAverageCosts()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-diagnostics-counters");
            try
            {
                BlueprintDiagnostics.ResetForTesting();
                var projection = new BlueprintProjectionSnapshot
                {
                    ResultCode = "resolved",
                    WorldPairKey = "pair-counter",
                    WorldKey = "world-counter",
                    InstanceCount = 2,
                    VisibleInstanceCount = 1,
                    HiddenInstanceCount = 1,
                    EffectiveLayerCount = 3,
                    LastResolveElapsedMs = 2
                };
                BlueprintDiagnostics.RecordProjectionResolve(projection);
                projection.LastResolveElapsedMs = 4;
                BlueprintDiagnostics.RecordProjectionResolve(projection);

                var materials = new BlueprintMaterialSnapshot
                {
                    ResultCode = "resolved",
                    WorldPairKey = "pair-counter",
                    WorldKey = "world-counter",
                    RequiredItemCount = 1,
                    MissingItemCount = 1,
                    MissingStackTotal = 5,
                    LastResolveElapsedMs = 6
                };
                BlueprintDiagnostics.RecordMaterialResolve(materials);

                var autoPlacement = new BlueprintAutoPlacementSnapshot
                {
                    Enabled = true,
                    ResultCode = "inventoryUnavailable",
                    Message = "no inventory",
                    CandidateCount = 0,
                    LastResolveElapsedMs = 8
                };
                BlueprintDiagnostics.RecordAutoPlacementCandidateScan(autoPlacement);

                var snapshot = BlueprintDiagnostics.BuildSnapshot(projection, materials, null, autoPlacement);
                if (snapshot.ProjectionResolve.Count != 2 ||
                    Math.Abs(snapshot.ProjectionResolve.AverageElapsedMs - 3d) > 0.001d)
                {
                    throw new InvalidOperationException("Expected projection average resolve cost to use incremental diagnostics counters.");
                }

                if (snapshot.MaterialsResolve.Count != 1 ||
                    Math.Abs(snapshot.MaterialsResolve.AverageElapsedMs - 6d) > 0.001d)
                {
                    throw new InvalidOperationException("Expected materials resolve cost to be recorded.");
                }

                if (snapshot.AutoPlacementCandidateScan.Count != 1 ||
                    Math.Abs(snapshot.AutoPlacementCandidateScan.AverageElapsedMs - 8d) > 0.001d ||
                    snapshot.AutoPlacementLastFailureReason.IndexOf("inventoryUnavailable", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Expected auto placement candidate scan cost and failure reason to be recorded.");
                }
            }
            finally
            {
                BlueprintDiagnostics.ResetForTesting();
                restore();
            }
        }

        private static void BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired()
        {
            BlueprintLibraryStage10DiagnosticsAuditContractsStayWired();
            BlueprintHandheldUiClickOwnershipContractsStayWired();
            BlueprintCreationFlickerFixContractsStayWired();
            BlueprintMenuUiStateDoesNotRefreshProjectionOrMaterials();
            BlueprintHandheldActionBarDynamicButtonMatrix();
            BlueprintHandheldActionBarRealCommandsAndDeferredPlacedCommands();
            BlueprintRegionActionShortcutAndHotkeyShareEraseState();
            BlueprintMirrorActionShortcutAndHotkeyShareTransformState();
            BlueprintPlacementConfirmRefreshesProjectionAndPlacedList();
            BlueprintPlacementUiHitConsumesWithoutCreatingInstance();
            BlueprintProjectionStage04LaterInstanceCoversEarlierWithoutMutatingSnapshots();
            BlueprintProjectionUiOverlayAndDiagnosticsContracts();
            BlueprintPlacedListRefreshesMaterialComparisonWithoutDrawScan();
            BlueprintPlacedListStage03LayoutMaterialAndCards();
            BlueprintPlacedInstanceClearAllCurrentWorldKeepsTemplatesAndRefreshesCaches();
            BlueprintWorldInstanceLifecycleRefreshesCacheAfterWorldEntry();
            BlueprintEraseSingleInstanceClipsProjectionAndMaterials();
            BlueprintEraseRegionStage07ContinuousHoverAndCancelOnly();
            BlueprintEraseRegionPhysicalLeftEdgesIgnoreConsumedWorldLeft();
            BlueprintPlacedInstanceMoveKeepsSnapshotStateAndRefreshesCaches();
            BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition();
            BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed();
            BlueprintDiagnosticsAggregateRuntimeSnapshotJson();
        }

        private static void BlueprintFeedbackStage11RegressionDiagnosticsContractsStayWired()
        {
            BlueprintPlacementStage08RegressionDiagnosticsContractsStayWired();
            FeatureCatalogExposesBlueprintEntryAsPlannedPlaceholder();
            BlueprintHandheldActionBarStage04ButtonHitBoundsMatchVisibleRects();
            BlueprintHandheldActionBarStage04NoticeTimingAndScale();
            BlueprintHandheldActionBarStage04MouseReaderCachesPrefixAndPostfixSeparately();
            BlueprintWorldInstanceLifecycleRefreshesCacheAfterWorldEntry();
            BlueprintEraseRegionPhysicalLeftEdgesIgnoreConsumedWorldLeft();
            BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells();
            BlueprintMaterialsStage05SubtractCompletedProgressFromDemand();
            BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement();
            BlueprintAutoPlacementUsesConfiguredReplacementMaterial();
            BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason();
            BlueprintAutoPlacementReplacementFailClosedWhenDisabledOrWrongCategory();
            BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson();
        }

        private static void BlueprintWallObjectStage06RegressionDiagnosticsContractsStayWired()
        {
            BlueprintProjectionWallFramesUseNeighborContinuity();
            BlueprintCaptureExpandsPartialMultitileObjectWithoutWallsOrWires();
            BlueprintCaptureFailsClosedWhenExpandedObjectCellIsIncomplete();
            BlueprintMirrorCompleteMultitileObjectMirrorsAndPartialFailsClosed();
            BlueprintMirrorSupportMatrixMapsSlopesAndFlipsObjectDirection();
            BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed();
            BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson();
            BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement();
            BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason();
        }

        private static void BlueprintProjectionRebuildStage06RegressionAuditContractsStayWired()
        {
            BlueprintProjectionUiOverlayAndDiagnosticsContracts();
            BlueprintProjectionWallFramesUseNeighborContinuity();
            BlueprintProjectionWallGhostUsesWorldLayerBeforeLateProjectionOverlay();
            BlueprintProjectionMultitileObjectConflictMarksWholeGroup();
            BlueprintCaptureExpandsPartialMultitileObjectWithoutWallsOrWires();
            BlueprintCaptureFailsClosedWhenExpandedObjectCellIsIncomplete();
            BlueprintAutoPlacementSkipsWholeMultitileObjectWhenAnyCellConflicts();

            var projectionContract = BlueprintProjectionOverlay.GetVisualContractForTesting();
            AssertContains(projectionContract, "original-missing");
            AssertContains(projectionContract, "missing-no-state-block");
            AssertContains(projectionContract, "wall-world-layer-before-foreground");
            AssertContains(projectionContract, "terraria-foreground-between-wall-and-late-projection");
            AssertContains(projectionContract, "multitile-object-group-conflict");
            AssertDoesNotContain(projectionContract, "yellow-missing");
            AssertDoesNotContain(projectionContract, "topmost");
        }

        private static void BlueprintCaptureSavesWhenRecognizableFurnitureIsPartiallySelected()
        {
            BlueprintCaptureExpandsPartialMultitileObjectWithoutWallsOrWires();
            BlueprintCaptureNormalizesMultitileObjectStyleAcrossSubtiles();
            BlueprintCaptureMergesRepeatedMultitileObjectSelectionWithStyleDrift();
        }

        private static void BlueprintCaptureDoesNotRejectWholeBlueprintForRecoverableObjectIssue()
        {
            BlueprintCaptureSkipsIncompleteExpandedObjectAndSavesOtherContent();
            BlueprintCaptureFailsClosedWhenExpandedObjectCellIsIncomplete();
        }

        private static void BlueprintTemplateObjectGroupMetadataSurvivesCloneImportExport()
        {
            BlueprintObjectGroupMetadataPersistsThroughInstanceExportAndImport();
        }

        private static void BlueprintLegacyPartialFurnitureRepairOrDegradeIsExplicit()
        {
            BlueprintLegacyPartialObjectRepairsMissingCellsAndFlags();
            BlueprintLegacyPartialObjectMarksDegradedWhenUnverifiable();
        }

        private static void BlueprintPlacementPreviewMultitileObjectConflictMarksWholeGroup()
        {
            BlueprintPlacementPreviewMultitileObjectUsesOriginalGhostLayers();
            BlueprintPlacementPreviewObjectGroupConflictMarksWholeGroup();
            BlueprintPlacementPreviewDegradedPartialObjectStaysUnavailable();
        }

        private static void BlueprintProjectionExplicitObjectGroupConflictMarksWholeFurniture()
        {
            BlueprintProjectionExplicitObjectGroupOverridesStyleHeuristic();
            BlueprintProjectionMultitileObjectConflictMarksWholeGroup();
        }

        private static void BlueprintAutoPlacementSkipsExplicitObjectGroupWhenAnyCellConflicts()
        {
            BlueprintAutoPlacementExplicitObjectGroupConflictSkipsRepresentative();
        }

        private static void BlueprintFurnitureSavePlacementRegressionContractsStayWired()
        {
            BlueprintCaptureSavesWhenRecognizableFurnitureIsPartiallySelected();
            BlueprintCaptureDoesNotRejectWholeBlueprintForRecoverableObjectIssue();
            BlueprintTemplateObjectGroupMetadataSurvivesCloneImportExport();
            BlueprintLegacyPartialFurnitureRepairOrDegradeIsExplicit();
            BlueprintPlacementPreviewMultitileObjectConflictMarksWholeGroup();
            BlueprintProjectionExplicitObjectGroupConflictMarksWholeFurniture();
            BlueprintAutoPlacementSkipsExplicitObjectGroupWhenAnyCellConflicts();

            var previewContract = BlueprintPlacementPreviewOverlay.GetVisualContractForTesting();
            AssertContains(previewContract, "foreground-original-ghost");
            AssertContains(previewContract, "draw-snapshot-only");

            var projectionContract = BlueprintProjectionOverlay.GetVisualContractForTesting();
            AssertContains(projectionContract, "original-missing");
            AssertContains(projectionContract, "multitile-object-group-conflict");
            AssertDoesNotContain(projectionContract, "yellow-missing");
            AssertDoesNotContain(projectionContract, "topmost");
        }

        private static void BlueprintWallContinuityStage05RegressionDiagnosticsContractsStayWired()
        {
            BlueprintProjectionWallFramesUseNeighborContinuity();
            BlueprintProjectionWallDiagnosticsSeparateTypePresenceAndFrameMismatch();
            BlueprintProjectionWallDiagnosticsExposeCompletedCurrentMismatch();
            BlueprintProjectionStage05CompletedProgressPersistsAndSkipsDugCells();
            BlueprintAutoPlacementRefreshesWallFramesAfterVerifiedWallUse();
            BlueprintAutoPlacementDoesNotRefreshWallFramesWhenWallTypeMissing();
            BlueprintAutoPlacementRetriesWallMissingOnceAfterUnverifiedUse();
            BlueprintAutoPlacementSubmitsActionQueueAndVerifiesPlacement();
            BlueprintAutoPlacementDiagnosticsWriteRuntimeSnapshotJson();
            BlueprintAutoPlacementVoidBagOnlyMaterialsFailClosedWithReason();
            BlueprintPlacedInstanceMoveBlocksCompletedProgressAndKeepsOriginalPosition();
            BlueprintPlacedInstanceMirrorUsesServiceAndFailsClosed();
        }
    }
}
