using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.Records;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    public static class JueMingZRuntime
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private const long DiagnosticOverlaySnapshotRefreshTicks = 15;
        private const long HiddenDiagnosticSnapshotProbeTicks = 60;
        private static long _lastDiagnosticOverlaySnapshotTick = -DiagnosticOverlaySnapshotRefreshTicks;
        private static long _nextHiddenDiagnosticSnapshotProbeTick;
        private static long _lastRuntimeUpdateStartTimestamp;
        private static readonly RuntimeTickPipeline TickPipeline = CreateTickPipeline();

        public const string Version = "0.758-map-footprints-thinner-line";

        public static RuntimeState State { get; private set; } = new RuntimeState();
        public static FeatureRegistry FeatureRegistry { get; private set; }
        public static FeatureManager FeatureManager { get; private set; }
        public static InputActionQueue ActionQueue { get; private set; }
        public static string TestRunId { get; private set; } = string.Empty;

        public static void Initialize()
        {
            Initialize(FeatureRegistry.CreateDefault(), null, new InputActionQueue());
        }

        public static void Initialize(FeatureRegistry registry, FeatureManager manager, InputActionQueue actionQueue)
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                FeatureRegistry = registry ?? FeatureRegistry.CreateDefault();
                FeatureManager = manager ?? new FeatureManager(FeatureRegistry);
                ActionQueue = actionQueue ?? new InputActionQueue();
                TestRunId = CreateTestRunId();
                DiagnosticActionRecorder.Initialize(TestRunId);
                RuntimeFeatureCatalogSnapshotCache.Refresh(FeatureRegistry);
                State.MarkInitialized();
                RuntimeDiagnostics.ClearError();
                _initialized = true;
                Logger.Info("Runtime", "Runtime initialized.");
            }

            FeatureCatalogWriter.WriteOnce(FeatureRegistry);
        }

        public static void Update()
        {
            var runtimeStartTimestamp = Stopwatch.GetTimestamp();
            var context = new RuntimeTickContext(runtimeStartTimestamp)
            {
                UpdateStartGapMs = MeasureUpdateStartGap(runtimeStartTimestamp)
            };

            try
            {
                if (!_initialized)
                {
                    Initialize();
                }

                TickPipeline.Run(context);
            }
            catch (Exception error)
            {
                context.StopRuntimeWatch();
                RecordRuntimePerformance(context);
                State.LastError = error.ToString();
                RuntimeDiagnostics.RecordError("Runtime.Update", error);
                LogThrottle.ErrorThrottled(
                    "runtime-update-error",
                    TimeSpan.FromSeconds(10),
                    "Runtime",
                    "Runtime Update failed; exception swallowed.", error);
            }
        }

        private static double MeasureUpdateStartGap(long runtimeStartTimestamp)
        {
            var previousStartTimestamp = _lastRuntimeUpdateStartTimestamp;
            _lastRuntimeUpdateStartTimestamp = runtimeStartTimestamp;
            return RuntimeTickContext.GetElapsedMilliseconds(previousStartTimestamp, runtimeStartTimestamp);
        }

        private static RuntimeTickPipeline CreateTickPipeline()
        {
            // Stage order is the runtime contract: read state, gate focus, dispatch services, then publish diagnostics.
            return new RuntimeTickPipeline(
                new RuntimeTickStage("runtime-state", UpdateRuntimeState),
                new RuntimeTickStage("ui-text-resource-monitor", UpdateUiTextResources),
                new RuntimeTickStage("post-terraria-input-guards", RunPostTerrariaInputGuards),
                new RuntimeTickStage("game-state-read", ReadGameState),
                new RuntimeTickStage("player-world-playtime-sampling", RunPlayerWorldPlaytimeSampling),
                new RuntimeTickStage("player-world-footprints-recording", RunPlayerWorldFootprintsRecording),
                new RuntimeTickStage("player-world-footprints-render-cache", RunPlayerWorldFootprintsRenderCache),
                new RuntimeTickStage("player-world-exploration-scan", RunPlayerWorldExplorationScan),
                new RuntimeTickStage("search-chest-locator-lifecycle", RunSearchChestLocatorLifecycleGuards),
                new RuntimeTickStage("input-focus-guard", RunInputFocusGuard),
                new RuntimeTickStage("targeting-and-ui-actions", RunTargetingAndUiActions),
                new RuntimeTickStage("automation-request-dispatch", DispatchAutomationRequests),
                new RuntimeTickStage("action-queue-update", UpdateActionQueue),
                new RuntimeTickStage("feature-manager-update", UpdateFeatureManager),
                new RuntimeTickStage("runtime-diagnostics", PublishRuntimeDiagnostics));
        }

        private static void UpdateRuntimeState(RuntimeTickContext context)
        {
            context.UpdateTick = State == null ? 0 : State.UpdateCount;
            context.SettingsSnapshot = RuntimeSettingsSnapshotProvider.GetCurrent();
            var gameModeDescription = State.LateBootstrapCompleted
                ? (GameStateReader.LastSnapshot == null ? "LateRuntime" : GameStateReader.LastSnapshot.NetModeDescription)
                : "EarlyRuntime: Terraria.Main static state has not been read";
            State.MarkUpdate(gameModeDescription);
            WorldGenDebugCompat.TryKeepEnabled();

            if (!State.FirstUpdateLogged)
            {
                State.FirstUpdateLogged = true;
                Logger.Info("Runtime", "Runtime Update first tick.");
            }

            if (ShouldLogHeartbeat())
            {
                State.MarkHeartbeat();
                Logger.Info(
                    "Runtime",
                    "Runtime Update heartbeat: updateCount=" + State.UpdateCount +
                    ", mode=" + State.LastGameModeDescription);
            }
        }

        private static void UpdateUiTextResources(RuntimeTickContext context)
        {
            UiTextRenderer.TickMainThreadResourceMonitor();
        }

        private static void RunPostTerrariaInputGuards(RuntimeTickContext context)
        {
            LegacyHotbarScrollGuard.ApplyPostTerrariaUpdate();
            LegacyMainUiState.HideIfMainMenu("Runtime.PostTerrariaInputGuard");
            LegacyMainUiState.HideIfFullscreenMapOpen(
                "Runtime.PostTerrariaInputGuard",
                TerrariaMainCompat.IsMapFullscreenOpen);
            DebugHotkeyService.Update();
        }

        private static void ReadGameState(RuntimeTickContext context)
        {
            var gameStateStart = Stopwatch.GetTimestamp();
            context.DiagnosticSnapshotDue = ShouldPublishDiagnosticSnapshot(State.UpdateCount);
            context.GameState = GameStateReader.Read(
                State.LateBootstrapCompleted,
                RuntimeGameStateReadOptionsBuilder.Build(context.SettingsSnapshot, context.DiagnosticSnapshotDue)).Snapshot;
            context.GameStateReadMs = RuntimeTickContext.GetElapsedMilliseconds(gameStateStart, Stopwatch.GetTimestamp());
        }

        private static void RunSearchChestLocatorLifecycleGuards(RuntimeTickContext context)
        {
            ClearSearchChestLocatorHighlightIfChestOpen(context == null ? null : context.GameState);
        }

        private static void RunPlayerWorldPlaytimeSampling(RuntimeTickContext context)
        {
            PlayerWorldPlaytimeService.Tick(
                context == null ? null : context.GameState,
                State);
        }

        private static void RunPlayerWorldExplorationScan(RuntimeTickContext context)
        {
            PlayerWorldExplorationService.Tick(
                context == null ? null : context.GameState,
                State);
        }

        private static void RunPlayerWorldFootprintsRecording(RuntimeTickContext context)
        {
            PlayerWorldFootprintService.Tick(
                context == null ? null : context.GameState,
                State);
        }

        private static void RunPlayerWorldFootprintsRenderCache(RuntimeTickContext context)
        {
            MapFootprintRenderCache.Tick(
                context == null ? null : context.SettingsSnapshot,
                context == null ? null : context.GameState,
                State == null ? 0L : State.UpdateCount);
        }

        internal static bool ClearSearchChestLocatorHighlightIfChestOpenForTesting(GameStateSnapshot snapshot)
        {
            return ClearSearchChestLocatorHighlightIfChestOpen(snapshot);
        }

        private static bool ClearSearchChestLocatorHighlightIfChestOpen(GameStateSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Ui == null || !snapshot.Ui.ChestOpen)
            {
                return false;
            }

            return SearchChestLocatorUiState.ClearSnapshotAfterChestOpened();
        }

        private static void RunInputFocusGuard(RuntimeTickContext context)
        {
            if (IsGameInputAvailable(context.GameState))
            {
                return;
            }

            LegacyUiInput.ResetInteractionState();
            UiMouseCaptureService.ReleaseForOperationWindow();
        }

        private static void RunTargetingAndUiActions(RuntimeTickContext context)
        {
            // Keep the focus gate ahead of targeting and UI services; inactive
            // input frames must not pay for weapon profiles or target scans.
            RuntimeAutomationDispatcher.RunTargetingAndUiActions(
                context,
                State,
                ActionQueue,
                IsGameInputAvailable(context.GameState));
        }

        private static void DispatchAutomationRequests(RuntimeTickContext context)
        {
            // The dispatch gate and per-service cadence are the idle-path budget;
            // do not move service scans ahead of RuntimeServiceScheduler checks.
            RuntimeAutomationDispatcher.DispatchAutomationRequests(context, State, ActionQueue);
        }

        private static void UpdateActionQueue(RuntimeTickContext context)
        {
            if (ActionQueue == null)
            {
                context.ActionSnapshot = InputActionQueueFastState.Empty;
                return;
            }

            var actionQueueStart = Stopwatch.GetTimestamp();
            ActionQueue.Update(context.GameState);
            context.ActionQueueUpdateMs = RuntimeTickContext.GetElapsedMilliseconds(actionQueueStart, Stopwatch.GetTimestamp());
            context.ActionSnapshot = ActionQueue.GetFastState();
            context.InputActionUpdateMs = context.ActionSnapshot.LastInputActionUpdateMs;
            AutoRecoveryService.AfterActionQueueUpdate(context.ActionSnapshot);
            FishingAutomationService.AfterActionQueueUpdate(context.ActionSnapshot);
            AutoCaptureCritterService.AfterActionQueueUpdate(context.ActionSnapshot, State == null ? 0 : State.UpdateCount);
            MovementSafeLandingService.AfterActionQueueUpdate(context.ActionSnapshot);
        }

        private static void UpdateFeatureManager(RuntimeTickContext context)
        {
            if (FeatureManager == null)
            {
                return;
            }

            var featureContext = new FeatureRuntimeContext
            {
                UtcNow = DateTime.UtcNow,
                UpdateUtc = DateTime.UtcNow,
                GameModeDescription = State.GameModeDescription,
                GameState = context.GameState,
                ActionQueue = ActionQueue,
                RuntimeState = State
            };
            FeatureManager.Update(featureContext);
        }

        private static void PublishRuntimeDiagnostics(RuntimeTickContext context)
        {
            context.ActionSnapshot = ActionQueue == null ? InputActionQueueFastState.Empty : ActionQueue.GetFastState();
            context.StopRuntimeWatch();
            RecordRuntimePerformance(context);
            RuntimeDiagnostics.RecordUpdate(State.UpdateCount, State.LateBootstrapCompleted, context.ActionSnapshot.LastActionResult);
            if (!context.DiagnosticSnapshotDue)
            {
                return;
            }

            var snapshot = GetDiagnosticSnapshot();
            if (DiagnosticsOverlay.Visible)
            {
                _lastDiagnosticOverlaySnapshotTick = State.UpdateCount;
            }

            DiagnosticsOverlay.UpdateFromSnapshot(snapshot);
            DiagnosticSnapshotWriter.WriteThrottled(snapshot);
        }

        private static bool ShouldPublishDiagnosticSnapshot(long updateTick)
        {
            if (!DiagnosticsOverlay.Visible)
            {
                if (updateTick > 0 && updateTick < _nextHiddenDiagnosticSnapshotProbeTick)
                {
                    return false;
                }

                if (updateTick > 0)
                {
                    _nextHiddenDiagnosticSnapshotProbeTick = updateTick + HiddenDiagnosticSnapshotProbeTicks;
                }

                return DiagnosticSnapshotWriter.ShouldWriteNow();
            }

            if (DiagnosticSnapshotWriter.ShouldWriteNow())
            {
                return true;
            }

            if (updateTick <= 0 || _lastDiagnosticOverlaySnapshotTick < 0 || updateTick < _lastDiagnosticOverlaySnapshotTick)
            {
                return true;
            }

            return updateTick - _lastDiagnosticOverlaySnapshotTick >= DiagnosticOverlaySnapshotRefreshTicks;
        }

        private static bool IsGameInputAvailable(GameStateSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Ui != null)
            {
                return snapshot.Ui.GameInputAvailable;
            }

            return TerrariaMainCompat.AllowsInputProcessing;
        }

        private static void RecordRuntimePerformance(RuntimeTickContext context)
        {
            var informationDrawMs = InformationOverlayService.GetLastDrawElapsedMs();
            RuntimePerformanceDiagnostics.Record(
                context.RuntimeElapsedMs,
                context.UpdateStartGapMs,
                context.GameStateReadMs,
                context.ActionQueueUpdateMs,
                context.InputActionUpdateMs,
                informationDrawMs,
                context.SlowestStageName,
                context.SlowestStageElapsedMs,
                context.SlowestOperationName,
                context.SlowestOperationElapsedMs);
            PerformanceHitchRecorder.RecordIfNeeded(
                context.UpdateStartGapMs,
                context.RuntimeElapsedMs,
                context.GameStateReadMs,
                context.ActionQueueUpdateMs,
                context.InputActionUpdateMs,
                informationDrawMs,
                () => BuildPerformanceHitchSample(context));
        }

        private static PerformanceHitchSample BuildPerformanceHitchSample(RuntimeTickContext context)
        {
            var gameState = context.GameState ?? GameStateReader.LastSnapshot ?? GameStateSnapshot.Unknown("Not read yet.");
            var actionSnapshot = context.ActionSnapshot ?? (ActionQueue == null ? InputActionQueueFastState.Empty : ActionQueue.GetFastState());
            var information = InformationOverlayService.GetDiagnostics();
            var settings = context.SettingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();

            return new PerformanceHitchSample
            {
                UtcNow = DateTime.UtcNow,
                TestRunId = TestRunId,
                RuntimeVersion = Version,
                UpdateCount = State == null ? 0 : State.UpdateCount,
                UpdateStartGapMs = context.UpdateStartGapMs,
                RuntimeUpdateMs = context.RuntimeElapsedMs,
                GameStateReadMs = context.GameStateReadMs,
                ActionQueueUpdateMs = context.ActionQueueUpdateMs,
                InputActionUpdateMs = context.InputActionUpdateMs,
                SlowestStageName = context.SlowestStageName,
                SlowestStageElapsedMs = context.SlowestStageElapsedMs,
                SlowestOperationName = context.SlowestOperationName,
                SlowestOperationElapsedMs = context.SlowestOperationElapsedMs,
                InformationLastDrawElapsedMs = information == null ? 0d : information.LastDrawElapsedMs,
                InformationEnabledSummary = information == null ? string.Empty : information.EnabledSummary,
                FishingAutomationNeedsTick = settings.FishingAutomationNeedsTick,
                FishingDisplayNeedsCatchResolver = settings.FishingDisplayNeedsCatchResolver,
                FishingHasResidualState = FishingAutomationService.HasResidualState,
                InformationLastSkipReason = information == null ? string.Empty : information.LastSkipReason,
                LateBootstrapCompleted = State != null && State.LateBootstrapCompleted,
                IsInWorld = gameState.IsInWorld,
                IsInMainMenu = gameState.IsInMainMenu,
                NetModeDescription = gameState.NetModeDescription,
                DiagnosticsOverlayVisible = DiagnosticsOverlay.Visible,
                LegacyMainUiVisible = LegacyMainUiState.Visible,
                LegacyMainUiPageId = LegacyMainUiState.SelectedPageId,
                CombatAimAssistRadius = settings.CombatAimAssistRadius,
                CursorAimRadius = settings.CursorAimRadius,
                PlayerAimRadius = settings.PlayerAimRadius,
                CombatAimMarkerEnabled = settings.CombatAimMarkerEnabled,
                CombatEquipmentWarningEnabled = settings.CombatEquipmentWarningEnabled,
                PendingActionCount = actionSnapshot.PendingCount,
                RunningActionKind = actionSnapshot.RunningActionKind,
                LastActionKind = actionSnapshot.LastActionKind,
                LastActionResultCode = actionSnapshot.LastActionResultCode,
                ActionQueueOccupiedChannels = actionSnapshot.OccupiedChannels,
                ActionQueueBridgeBusyChannels = actionSnapshot.BridgeBusyChannels
            };
        }

        public static GameStateReadOptions BuildGameStateReadOptionsForTesting(
            object snapshot = null, bool diagnosticSnapshotDue = false,
            object extra1 = null, object extra2 = null)
        {
            var settingsSnapshot = snapshot as RuntimeSettingsSnapshot;
            if (settingsSnapshot == null)
            {
                var settings = snapshot as AppSettings ?? ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var legacyMiscUiNeedsInventory = extra1 is bool && (bool)extra1;
                settingsSnapshot = RuntimeSettingsSnapshot.FromSettings(settings, false, string.Empty, legacyMiscUiNeedsInventory);
            }

            return RuntimeGameStateReadOptionsBuilder.Build(settingsSnapshot, diagnosticSnapshotDue);
        }

        public static bool ShouldRunServiceForTesting(string serviceName, bool enabled, int priority, int maxPriority)
        {
            return RuntimeServiceScheduler.ShouldRun(serviceName, enabled, priority, maxPriority);
        }

        public static bool IsGameInputAvailableForTesting(GameStateSnapshot snapshot)
        {
            return IsGameInputAvailable(snapshot);
        }

        public static bool ShouldDispatchAutomationForTesting(GameStateSnapshot snapshot)
        {
            return RuntimeAutomationDispatcher.ShouldDispatchAutomation(snapshot);
        }

        internal static bool ShouldDispatchFishingAutomationForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState)
        {
            return RuntimeAutomationDispatcher.ShouldDispatchFishingAutomation(settings, hasResidualState, 0);
        }

        internal static bool ShouldDispatchFishingAutomationForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return RuntimeAutomationDispatcher.ShouldDispatchFishingAutomation(settings, hasResidualState, tick);
        }

        internal static int GetFishingAutomationDispatchCadenceForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return RuntimeAutomationDispatcher.GetFishingAutomationDispatchCadence(settings, hasResidualState, tick);
        }

        internal static string GetFishingAutomationDispatchReasonForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return RuntimeAutomationDispatcher.GetFishingAutomationDispatchReason(settings, hasResidualState, tick);
        }

        internal static RuntimeDispatchStep[] GetTargetingDispatchContractForTesting()
        {
            return RuntimeAutomationDispatcher.GetTargetingDispatchContractForTesting();
        }

        internal static RuntimeDispatchStep[] GetAutomationDispatchContractForTesting()
        {
            return RuntimeAutomationDispatcher.GetAutomationDispatchContractForTesting();
        }

        internal static bool ShouldRunAutomationDispatchStepForTesting(
            string serviceName,
            bool enabled,
            GameStateSnapshot snapshot,
            long tick)
        {
            return RuntimeAutomationDispatcher.ShouldRunAutomationDispatchStepForTesting(
                serviceName,
                enabled,
                snapshot,
                tick);
        }

        public static void ResetServiceSchedulerForTesting()
        {
            RuntimeServiceScheduler.ResetForTesting();
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    return;
                }

                PlayerWorldPlaytimeService.FlushPending();
                PlayerWorldFootprintService.FlushPending();
                PlayerWorldExplorationService.FlushPending();
                State.MarkShutdown();
                _initialized = false;
                Logger.Info("Runtime", "Runtime shutdown completed.");
            }
        }

        public static void MarkLateBootstrapCompleted()
        {
            lock (SyncRoot)
            {
                State.MarkLateBootstrapCompleted();
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            WorldGenDebugCompat.TryEnableAfterLateBootstrap(
                settings.DiagnosticsWorldGenDebugViewerEnabled,
                settings.DiagnosticsDeveloperDebugCommandsEnabled);
        }

        public static DiagnosticSnapshot GetDiagnosticSnapshot()
        {
            // The builder owns the low-frequency runtime-snapshot field contract;
            // Runtime keeps only the publication cadence and write route.
            var featureCatalogStats = RuntimeFeatureCatalogSnapshotCache.Current;
            return RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
            {
                Initialized = _initialized,
                Version = Version,
                TestRunId = TestRunId,
                State = State,
                FeatureManager = FeatureManager,
                ActionQueue = ActionQueue,
                FeatureCatalogCount = featureCatalogStats.FeatureCatalogCount,
                ImplementedFeatureCount = featureCatalogStats.ImplementedFeatureCount,
                VisibleFeatureCount = featureCatalogStats.VisibleFeatureCount,
                HotkeyVisibleFeatureCount = featureCatalogStats.HotkeyVisibleFeatureCount,
                UserCategoryCounts = featureCatalogStats.UserCategoryCounts,
                CodeDomainCounts = featureCatalogStats.CodeDomainCounts
            });
        }

        private static bool ShouldLogHeartbeat()
        {
            if (State.UpdateCount <= 0)
            {
                return false;
            }

            if (State.UpdateCount % 600 == 0)
            {
                return true;
            }

            if (!State.LastHeartbeatUtc.HasValue)
            {
                return DateTime.UtcNow - (State.FirstUpdateUtc ?? DateTime.UtcNow) >= TimeSpan.FromSeconds(10);
            }

            return DateTime.UtcNow - State.LastHeartbeatUtc.Value >= TimeSpan.FromSeconds(10);
        }

        private static string GetProcessName()
        {
            try
            {
                return Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        private static string CreateTestRunId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) +
                   "-" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant();
        }
    }
}
