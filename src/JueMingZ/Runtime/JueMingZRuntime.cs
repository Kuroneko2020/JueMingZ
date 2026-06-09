using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    public static class JueMingZRuntime
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static bool _startupNoopQueued;
        private static int _featureCatalogCount;
        private static int _implementedFeatureCount;
        private static int _visibleFeatureCount;
        private static int _hotkeyVisibleFeatureCount;
        private static Dictionary<string, int> _userCategoryCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, int> _codeDomainCounts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private const long DiagnosticOverlaySnapshotRefreshTicks = 15;
        private const long HiddenDiagnosticSnapshotProbeTicks = 60;
        private static long _lastDiagnosticOverlaySnapshotTick = -DiagnosticOverlaySnapshotRefreshTicks;
        private static long _nextHiddenDiagnosticSnapshotProbeTick;
        private static long _lastRuntimeUpdateStartTimestamp;
        private static readonly RuntimeTickPipeline TickPipeline = CreateTickPipeline();
        private static readonly object ServiceSchedulerSyncRoot = new object();
        private static readonly Dictionary<string, bool> ServiceSchedulerLastEnabled =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> ServiceSchedulerLastRunTick =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public const string Version = "1.7.488-shoots-on-release-autoclick-guard";

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
                CacheFeatureCatalogStats();
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
            DebugHotkeyService.Update();
        }

        private static void ReadGameState(RuntimeTickContext context)
        {
            var gameStateStart = Stopwatch.GetTimestamp();
            context.DiagnosticSnapshotDue = ShouldPublishDiagnosticSnapshot(State.UpdateCount);
            context.GameState = GameStateReader.Read(
                State.LateBootstrapCompleted,
                BuildGameStateReadOptions(context.SettingsSnapshot, context.DiagnosticSnapshotDue)).Snapshot;
            context.GameStateReadMs = RuntimeTickContext.GetElapsedMilliseconds(gameStateStart, Stopwatch.GetTimestamp());
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
            if (!IsGameInputAvailable(context.GameState))
            {
                return;
            }

            var gameState = context.GameState;
            var settings = context.SettingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var operationStart = Stopwatch.GetTimestamp();
            if (ShouldRunService("targeting.combat-release-hold", settings.CombatAimAnyEnabled, 1, context.UpdateTick))
            {
                CombatAimReleaseHoldService.Tick(gameState != null && gameState.IsInWorld, settings);
                RecordOperationTiming(context, "targeting.combat-release-hold", operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRunService(
                "targeting.combat-auto-aim",
                settings.CursorAimRadius > 0,
                1,
                context.UpdateTick))
            {
                CombatAutoAimService.Tick(gameState, State, settings);
                RecordOperationTiming(context, "targeting.combat-auto-aim", operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRunService("targeting.combat-flail-control", settings.CursorAimRadius > 0, 1, context.UpdateTick))
            {
                CombatAimFlailControlService.Update();
                RecordOperationTiming(context, "targeting.combat-flail-control", operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            QueueStartupDiagnosticNoopIfReady();
            RecordOperationTiming(context, "targeting.startup-diagnostic-noop", operationStart);
            operationStart = Stopwatch.GetTimestamp();
            DiagnosticButtonActionService.Update(ActionQueue, gameState);
            RecordOperationTiming(context, "targeting.diagnostic-button-actions", operationStart);
            operationStart = Stopwatch.GetTimestamp();
            LegacyUiActionService.Update(ActionQueue, gameState);
            RecordOperationTiming(context, "targeting.legacy-ui-actions", operationStart);
            operationStart = Stopwatch.GetTimestamp();
            DiagnosticActionHotkeyService.Update(ActionQueue, gameState);
            RecordOperationTiming(context, "targeting.diagnostic-hotkeys", operationStart);
        }

        private static void DispatchAutomationRequests(RuntimeTickContext context)
        {
            // The dispatch gate and per-service cadence are the idle-path budget;
            // do not move service scans ahead of ShouldRunService checks.
            if (!ShouldDispatchAutomation(context.GameState))
            {
                return;
            }

            var gameState = context.GameState;
            var settings = context.SettingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var tick = context.UpdateTick;
            var operationStart = Stopwatch.GetTimestamp();
            if (ShouldRunService(
                "travel-menu",
                settings.WorldAutomationTravelMenuEnabled || TravelMenuService.RequiresRuntimeTickWhenDisabled(),
                1,
                tick))
            {
                TravelMenuService.Tick(gameState, State);
                RecordOperationTiming(context, "dispatch.travel-menu", operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (TravelMenuService.ShouldPauseAutomationForTravelMenu())
            {
                ClearActionQueueForTravelMenu();
                RecordOperationTiming(context, "dispatch.travel-menu-pause-automation", operationStart);
                return;
            }

            if (ShouldRunService("auto-recovery", settings.RecoveryAnyEnabled, 1, tick))
            {
                AutoRecoveryService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-recovery", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            var fishingHasResidualState = FishingAutomationService.HasResidualState;
            var fishingDispatch = GetFishingAutomationDispatchDecision(settings, fishingHasResidualState, tick);
            FishingAutomationService.RecordDispatchState(fishingDispatch.Reason, fishingDispatch.CadenceTicks);
            if (ShouldRunService(
                "fishing-automation",
                fishingDispatch.Enabled,
                fishingDispatch.CadenceTicks,
                tick))
            {
                FishingAutomationService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.fishing-automation", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("quick-item-hotkeys", settings.InventoryQuickItemHotkeysEnabled, 1, tick))
            {
                QuickItemHotkeyService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.quick-item-hotkeys", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-capture-critter", settings.WorldAutomationAutoCaptureCritterEnabled, 4, tick))
            {
                AutoCaptureCritterService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-capture-critter", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-harvest", settings.WorldAutomationAutoHarvestEnabled, 1, tick))
            {
                AutoHarvestService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-harvest", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-mining", settings.WorldAutomationAutoMiningEnabled, 1, tick))
            {
                AutoMiningService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-mining", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-stack", settings.InventoryAutoStackEnabled, 5, tick))
            {
                AutoStackService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-stack", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-sell", settings.InventoryAutoSellEnabled, 15, tick))
            {
                AutoSellService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-sell", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-discard", settings.InventoryAutoDiscardEnabled, 15, tick))
            {
                AutoDiscardService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-discard", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("quick-bag-open", settings.InventoryQuickBagOpenEnabled, 1, tick))
            {
                QuickBagOpenService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.quick-bag-open", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-deposit-coins", settings.InventoryAutoDepositCoinsEnabled, 15, tick))
            {
                AutoDepositCoinsService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-deposit-coins", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("auto-extractinator", settings.InventoryAutoExtractinatorEnabled, 3, tick))
            {
                AutoExtractinatorService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-extractinator", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("keep-favorited", settings.InventoryKeepFavoritedEnabled, 2, tick))
            {
                KeepFavoritedService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.keep-favorited", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("quick-reforge", settings.NpcAutoReforgeEnabled, 1, tick))
            {
                QuickReforgeService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.quick-reforge", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRunService("auto-tax-collect", settings.NpcAutoTaxCollectEnabled, 30, tick))
            {
                AutoTaxCollectorService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.auto-tax-collect", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("combat-perfect-revolver", settings.CombatPerfectRevolverEnabled, 1, tick))
            {
                CombatPerfectRevolverService.Tick(ActionQueue, gameState, State);
                RecordOperationTiming(context, "dispatch.combat-perfect-revolver", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("combat-magic-string", settings.CombatMagicStringClickerEnabled, 1, tick))
            {
                CombatMagicStringClickerService.Tick(ActionQueue, gameState, State);
                RecordOperationTiming(context, "dispatch.combat-magic-string", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("combat-auto-facing", settings.CombatAutoFacingEnabled, 1, tick))
            {
                CombatAutoFacingService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.combat-auto-facing", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("combat-equipment-warning", settings.CombatEquipmentWarningEnabled, 1, tick))
            {
                CombatEquipmentWarningService.Tick(gameState, State, settings);
                RecordOperationTiming(context, "dispatch.combat-equipment-warning", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            FirstWorldLoadPromptService.Tick(gameState, State);
            RecordOperationTiming(context, "dispatch.first-world-load-prompt", operationStart);
            operationStart = Stopwatch.GetTimestamp();

            if (ShouldRunService(
                "movement-safe-landing",
                settings.MovementSafeLandingEnabled || MovementSafeLandingService.RequiresRuntimeTickWhenDisabled(),
                1,
                tick))
            {
                MovementSafeLandingService.Tick(ActionQueue, gameState, State);
                RecordOperationTiming(context, "dispatch.movement-safe-landing", operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();

            if (ShouldRunService("movement-continuous-dash", settings.MovementContinuousDashEnabled, 1, tick))
            {
                MovementContinuousDashService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.movement-continuous-dash", operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRunService("movement-simulated-jump", settings.MovementSimulatedMultiJumpEnabled, 1, tick))
            {
                MovementSimulatedJumpService.Tick(ActionQueue, gameState, State, settings);
                RecordOperationTiming(context, "dispatch.movement-simulated-jump", operationStart);
            }
        }

        private static void ClearActionQueueForTravelMenu()
        {
            if (ActionQueue == null)
            {
                return;
            }

            var snapshot = ActionQueue.GetFastState();
            if (snapshot == null ||
                (snapshot.PendingCount <= 0 && string.IsNullOrWhiteSpace(snapshot.RunningActionKind)))
            {
                return;
            }

            ActionQueue.Clear();
            LogThrottle.InfoThrottled(
                "travel-menu-automation-dispatch-paused",
                TimeSpan.FromSeconds(3),
                "Runtime",
                "Travel menu CreativeUI is open; cleared queued input actions and paused automation dispatch.");
        }

        private static void RecordOperationTiming(RuntimeTickContext context, string operationName, long operationStart)
        {
            if (context == null)
            {
                return;
            }

            context.RecordOperationTiming(
                operationName,
                RuntimeTickContext.GetElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp()));
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

        private static bool ShouldDispatchAutomation(GameStateSnapshot snapshot)
        {
            // Window focus gates physical/user input only; background automation keeps its own safety checks.
            return true;
        }

        private static FishingDispatchDecision GetFishingAutomationDispatchDecision(
            RuntimeSettingsSnapshot settings,
            bool hasResidualState,
            long tick)
        {
            settings = settings ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var residualMask = FishingAutomationService.GetResidualStateMask();
            if (hasResidualState || residualMask != 0)
            {
                return FishingDispatchDecision.EnabledDecision(
                    FishingAutomationService.GetDispatchReasonForResidualMask(residualMask),
                    1);
            }

            if (!settings.FishingAutomationNeedsTick)
            {
                return FishingDispatchDecision.Disabled("disabled");
            }

            if (FishingAutomationService.HasFreshActiveBobberForRuntime(tick))
            {
                return FishingDispatchDecision.EnabledDecision("freshActiveBobber", 1);
            }

            return FishingDispatchDecision.EnabledDecision(
                "idleWatchdog",
                FishingAutomationService.IdleWatchdogCadenceTicks);
        }

        private struct FishingDispatchDecision
        {
            public bool Enabled;
            public string Reason;
            public int CadenceTicks;

            public static FishingDispatchDecision EnabledDecision(string reason, int cadenceTicks)
            {
                return new FishingDispatchDecision
                {
                    Enabled = true,
                    Reason = reason ?? string.Empty,
                    CadenceTicks = cadenceTicks <= 0 ? 1 : cadenceTicks
                };
            }

            public static FishingDispatchDecision Disabled(string reason)
            {
                return new FishingDispatchDecision
                {
                    Enabled = false,
                    Reason = reason ?? string.Empty,
                    CadenceTicks = 1
                };
            }
        }

        private static GameStateReadOptions BuildGameStateReadOptions(RuntimeSettingsSnapshot settingsSnapshot, bool diagnosticSnapshotDue)
        {
            // Read profiles are the runtime cost budget; full reads stay behind feature and snapshot gates.
            var settings = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var sourceSettings = settings.SourceSettings ?? AppSettings.CreateDefault();
            if (diagnosticSnapshotDue)
            {
                return GameStateReadOptions.Full;
            }

            var inventoryProfile = InventoryReadProfile.None;
            var npcProfile = NpcReadProfile.None;

            if (settings.LegacyMiscUiNeedsInventorySnapshot)
            {
                inventoryProfile |= InventoryReadProfile.Full;
            }

            if (settings.RecoveryAnyEnabled)
            {
                inventoryProfile |= InventoryReadProfile.RecoveryItems;
            }

            if (settings.FishingAutoFishEnabled)
            {
                inventoryProfile |= InventoryReadProfile.SignatureOnly;
            }

            if (settings.InventoryAutoStackEnabled)
            {
                inventoryProfile |= InventoryReadProfile.StackCandidates;
            }

            if (settings.InventoryAutoSellEnabled || settings.InventoryAutoDiscardEnabled)
            {
                inventoryProfile |= InventoryReadProfile.SellDiscardCandidates;
            }

            if (settings.InventoryAutoDepositCoinsEnabled)
            {
                inventoryProfile |= InventoryReadProfile.CoinsOnly;
            }

            if (settings.InventoryAutoExtractinatorEnabled)
            {
                inventoryProfile |= InventoryReadProfile.ExtractinatorItems;
            }

            if (settings.InventoryKeepFavoritedEnabled)
            {
                inventoryProfile |= InventoryReadProfile.KeepFavorited;
            }

            if (settings.WorldAutomationAutoCaptureCritterEnabled)
            {
                inventoryProfile |= InventoryReadProfile.BugNetOnly;
                npcProfile |= NpcReadProfile.CatchableCrittersOnly;
            }

            if (settings.WorldAutomationAutoHarvestEnabled)
            {
                inventoryProfile |= InventoryReadProfile.ToolsAndSeeds;
            }

            var fishingFilterNeedsActiveBuffs =
                settings.FishingAutoFishEnabled &&
                FishingAutomationService.IsFishingFilterEnabled(sourceSettings);

            return new GameStateReadOptions
            {
                InventoryProfile = inventoryProfile,
                IncludeActiveBuffs = settings.AutoBuffEnabled ||
                                     (settings.AutoRecovery != null && settings.AutoRecovery.AutoStationBuffEnabled) ||
                                     fishingFilterNeedsActiveBuffs,
                NpcProfile = npcProfile,
                TileProfile = TileReadProfile.None,
                IncludeWorldSummary = false
            };
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

        private static bool ShouldRunService(string serviceName, bool enabled, int cadenceTicks, long updateTick)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return enabled;
            }

            if (cadenceTicks <= 0)
            {
                cadenceTicks = 1;
            }

            lock (ServiceSchedulerSyncRoot)
            {
                bool wasEnabled;
                ServiceSchedulerLastEnabled.TryGetValue(serviceName, out wasEnabled);

                if (!enabled)
                {
                    ServiceSchedulerLastEnabled[serviceName] = false;
                    if (wasEnabled)
                    {
                        ServiceSchedulerLastRunTick[serviceName] = updateTick;
                        return true;
                    }

                    return false;
                }

                ServiceSchedulerLastEnabled[serviceName] = true;
                if (!wasEnabled)
                {
                    ServiceSchedulerLastRunTick[serviceName] = updateTick;
                    return true;
                }

                long lastRunTick;
                if (!ServiceSchedulerLastRunTick.TryGetValue(serviceName, out lastRunTick) ||
                    updateTick < lastRunTick ||
                    updateTick - lastRunTick >= cadenceTicks)
                {
                    ServiceSchedulerLastRunTick[serviceName] = updateTick;
                    return true;
                }

                return false;
            }
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

            return BuildGameStateReadOptions(settingsSnapshot, diagnosticSnapshotDue);
        }

        public static bool ShouldRunServiceForTesting(string serviceName, bool enabled, int priority, int maxPriority)
        {
            return ShouldRunService(serviceName, enabled, priority, maxPriority);
        }

        public static bool IsGameInputAvailableForTesting(GameStateSnapshot snapshot)
        {
            return IsGameInputAvailable(snapshot);
        }

        public static bool ShouldDispatchAutomationForTesting(GameStateSnapshot snapshot)
        {
            return ShouldDispatchAutomation(snapshot);
        }

        internal static bool ShouldDispatchFishingAutomationForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, 0).Enabled;
        }

        internal static bool ShouldDispatchFishingAutomationForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).Enabled;
        }

        internal static int GetFishingAutomationDispatchCadenceForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).CadenceTicks;
        }

        internal static string GetFishingAutomationDispatchReasonForTesting(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).Reason;
        }

        public static void ResetServiceSchedulerForTesting()
        {
            lock (ServiceSchedulerSyncRoot)
            {
                ServiceSchedulerLastEnabled.Clear();
                ServiceSchedulerLastRunTick.Clear();
            }
        }
        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_initialized)
                {
                    return;
                }

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
            // Snapshot publishing reads cached module summaries. Adding fields
            // here must not submit actions, append events, or start new scans.
            var featureInfo = FeatureManager == null ? FeatureManagerDiagnosticInfo.Empty : FeatureManager.GetDiagnosticInfo();
            var actionSnapshot = ActionQueue == null ? InputActionQueueSnapshot.Empty : ActionQueue.GetSnapshot();
            var gameState = GameStateReader.LastSnapshot ?? GameStateSnapshot.Unknown("Not read yet.");
            var lateBootstrapCompleted = State != null && State.LateBootstrapCompleted;
            var diagnosticSlot = ConfigService.AppSettings.DiagnosticInputTestSlot;
            var diagnosticSlotInfo = DiagnosticHotbarInfo.FromSnapshot(gameState, diagnosticSlot);
            var lastActionUserMessage = GetLastActionUserMessage(actionSnapshot);
            var lastActionResultCode = GetLastActionResultCode(actionSnapshot);
            var lastActionKind = GetLastActionKind(actionSnapshot);
            var autoRecovery = AutoRecoveryService.GetStateSnapshot();
            var stationBuff = StationBuffCompat.GetDiagnostics();
            var configSave = ConfigService.LastSaveSummary;
            var configAppSave = configSave == null ? null : configSave.AppSettings;
            var configFeatureSave = configSave == null ? null : configSave.FeatureSettings;
            var configHotkeySave = configSave == null ? null : configSave.HotkeySettings;
            var autoStack = AutoStackService.GetDiagnostics();
            var autoSell = AutoSellService.GetDiagnostics();
            var autoDiscard = AutoDiscardService.GetDiagnostics();
            var quickReforge = QuickReforgeService.GetDiagnostics();
            var autoCaptureCritter = AutoCaptureCritterService.GetDiagnostics();
            var autoHarvest = AutoHarvestService.GetDiagnostics();
            var quickBagOpen = QuickBagOpenService.GetDiagnostics();
            var autoDepositCoins = AutoDepositCoinsService.GetDiagnostics();
            var autoExtractinator = AutoExtractinatorService.GetDiagnostics();
            var keepFavorited = KeepFavoritedService.GetDiagnostics();
            var autoTaxCollect = AutoTaxCollectorService.GetDiagnostics();
            var autoFacing = CombatAutoFacingService.GetDiagnostics();
            var perfectRevolver = CombatPerfectRevolverService.GetDiagnostics();
            var flailCombo = CombatFlailComboService.GetDiagnostics();
            var itemCheckAutoClicker = CombatItemCheckAutoClickService.GetDiagnostics();
            var itemCheckWriter = ItemCheckWriterArbiter.GetLastDecision();
            var worldAutomationFairness = WorldAutomationFairnessCoordinator.GetSnapshot();
            var magicStringClicker = CombatMagicStringClickerService.GetDiagnostics();
            var information = InformationOverlayService.GetDiagnostics();
            var fishing = FishingAutomationService.GetDiagnostics();
            var settingsSnapshot = RuntimeSettingsSnapshotProvider.GetCurrent();
            var fishingHasResidualState = FishingAutomationService.HasResidualState;
            var simulatedJump = MovementSimulatedJumpService.GetDiagnostics();
            var continuousDash = MovementContinuousDashService.GetDiagnostics();
            var teleportCorrection = MovementTeleportCorrectionService.GetDiagnostics();
            var safeLanding = MovementSafeLandingService.GetDiagnostics();

            return new DiagnosticSnapshot
            {
                Loaded = _initialized,
                Version = Version,
                RuntimeVersion = Version,
                TestRunId = TestRunId,
                ProcessName = GetProcessName(),
                BaseDirectory = AppDomain.CurrentDomain.BaseDirectory,
                LogDirectory = Logger.LogDirectory,
                TerrariaDetected = GameMode.IsTerrariaLoaded,
                TerrariaVersion = lateBootstrapCompleted ? GameMode.GetTerrariaVersionLateOnly() : "EarlyUnavailable",
                NetModeDescription = lateBootstrapCompleted ? GameMode.GetDescriptionLateOnly() : "EarlyUnavailable",
                UpdateCount = State == null ? 0 : State.UpdateCount,
                LateBootstrapCompleted = lateBootstrapCompleted,
                SafeBootstrapStarted = AssemblyLoadTracker.SafeBootstrapStarted,
                HarmonyLoaded = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony") != null,
                SafeBootstrapHookInstalled = HookDiagnostics.SafeBootstrapHookInstalled,
                HookUpdateInstalled = HookDiagnostics.HookUpdateInstalled,
                DrawHookInstalled = HookDiagnostics.DrawHookInstalled,
                InterfaceLayerHookInstalled = HookDiagnostics.InterfaceLayerHookInstalled,
                ItemCheckHookInstalled = HookDiagnostics.ItemCheckHookInstalled,
                ItemCheckHookMethod = HookDiagnostics.ItemCheckHookMethod,
                GoblinExecutionHookInstalled = HookDiagnostics.GoblinExecutionHookInstalled,
                GoblinExecutionHookMethod = HookDiagnostics.GoblinExecutionHookMethod,
                DiagnosticsOverlayVisible = DiagnosticsOverlay.Visible,
                DrawCallCount = DiagnosticsOverlay.DrawCallCount,
                LastDrawUtc = DiagnosticsOverlay.LastDrawUtc,
                LastUpdateUtc = RuntimeDiagnostics.LastUpdateUtc,
                LastHeartbeatUtc = State == null ? null : State.LastHeartbeatUtc,
                FeatureCount = featureInfo.TotalFeatures,
                EnabledFeatureCount = featureInfo.EnabledFeatures,
                AppSettingsEnabledFeatureCount = ConfigService.CountAppSettingsEnabledFeatures(),
                FeatureSettingsEnabledFeatureCount = ConfigService.CountFeatureSettingsEnabledFeatures(),
                EffectiveEnabledFeatureCount = ConfigService.CountEffectiveEnabledFeatures(),
                FeatureCatalogCount = _featureCatalogCount,
                ImplementedFeatureCount = _implementedFeatureCount,
                VisibleFeatureCount = _visibleFeatureCount,
                HotkeyVisibleFeatureCount = _hotkeyVisibleFeatureCount,
                UserCategoryCounts = _userCategoryCounts,
                CodeDomainCounts = _codeDomainCounts,
                FeatureManagerUpdateCount = featureInfo.UpdateCount,
                WorldGenDebugViewerConfiguredEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.DiagnosticsWorldGenDebugViewerEnabled,
                DeveloperDebugCommandsConfiguredEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.DiagnosticsDeveloperDebugCommandsEnabled,
                WorldGenDebugViewerSessionConfiguredEnabled = WorldGenDebugCompat.WorldGenSessionConfiguredEnabled,
                DeveloperDebugCommandsSessionConfiguredEnabled = WorldGenDebugCompat.SessionConfiguredEnabled,
                WorldGenDebugAttempted = WorldGenDebugCompat.Attempted,
                WorldGenDebugFieldEnabled = WorldGenDebugCompat.Enabled,
                WorldGenDebugStatus = WorldGenDebugCompat.Status,
                WorldGenDebugMessage = WorldGenDebugCompat.Message,
                WorldGenDebugFieldOwner = WorldGenDebugCompat.FieldOwner,
                WorldGenDebugLastAttemptUtc = WorldGenDebugCompat.LastAttemptUtc,
                IsInMainMenu = gameState.IsInMainMenu,
                IsInWorld = gameState.IsInWorld,
                GameInputAvailable = IsGameInputAvailable(gameState),
                PlayerLife = gameState.Player == null ? 0 : gameState.Player.Life,
                PlayerLifeMax = gameState.Player == null ? 0 : gameState.Player.LifeMax,
                PlayerMana = gameState.Player == null ? 0 : gameState.Player.Mana,
                PlayerManaMax = gameState.Player == null ? 0 : gameState.Player.ManaMax,
                SelectedItemType = gameState.Inventory == null || gameState.Inventory.SelectedItem == null ? 0 : gameState.Inventory.SelectedItem.Type,
                SelectedItemName = gameState.Inventory == null || gameState.Inventory.SelectedItem == null ? string.Empty : gameState.Inventory.SelectedItem.Name,
                InventoryNonEmptyCount = gameState.Inventory == null ? 0 : gameState.Inventory.NonEmptyCount,
                ActiveBuffCount = gameState.ActiveBuffs == null ? 0 : gameState.ActiveBuffs.Count,
                ActiveNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.ActiveNpcCount,
                TownNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.TownNpcCount,
                HostileNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.HostileNpcCount,
                CritterCount = gameState.Npcs == null ? 0 : gameState.Npcs.CritterCount,
                LastGameStateReadUtc = gameState.LastReadUtc,
                LastGameStateReadError = gameState.LastReadError,
                LastGameStateInventoryProfile = GameStateReader.LastInventoryProfile.ToString(),
                LastGameStateNpcProfile = GameStateReader.LastNpcProfile.ToString(),
                LastGameStateTileProfile = GameStateReader.LastTileProfile.ToString(),
                PendingActionCount = actionSnapshot.PendingCount,
                ActionQueueUpdateCount = actionSnapshot.UpdateCount,
                RunningAction = actionSnapshot.RunningAction,
                RunningActionKind = actionSnapshot.RunningActionKind,
                RunningActionSource = actionSnapshot.RunningActionSource,
                RunningActionStatus = actionSnapshot.RunningActionStatus,
                LastActionKind = lastActionKind,
                LastActionStatus = actionSnapshot.LastActionStatus,
                LastActionMessage = actionSnapshot.LastActionMessage,
                LastActionUserMessage = lastActionUserMessage,
                LastActionResultCode = lastActionResultCode,
                LastActionDurationMs = actionSnapshot.LastActionDurationMs,
                RecentActionResultsCount = actionSnapshot.RecentResultsCount,
                LastActionResult = actionSnapshot.LastActionResult,
                RecentActionLine1 = FirstNonEmpty(DiagnosticActionHotkeyService.GetRecentFeedbackLine(0), actionSnapshot.RecentActionLine1),
                RecentActionLine2 = FirstNonEmpty(DiagnosticActionHotkeyService.GetRecentFeedbackLine(1), actionSnapshot.RecentActionLine2),
                RecentActionLine3 = FirstNonEmpty(DiagnosticActionHotkeyService.GetRecentFeedbackLine(2), actionSnapshot.RecentActionLine3),
                ActionQueueChannelLeaseCount = actionSnapshot.ActionQueueChannelLeaseCount,
                ActionQueueRunningChannels = actionSnapshot.ActionQueueRunningChannels,
                ActionQueueOccupiedChannels = actionSnapshot.ActionQueueOccupiedChannels,
                ActionQueueBridgeBusyChannels = actionSnapshot.ActionQueueBridgeBusyChannels,
                ActionQueueRunningLeaseChannels = actionSnapshot.ActionQueueRunningLeaseChannels,
                ActionQueueBlockedPendingCount = actionSnapshot.ActionQueueBlockedPendingCount,
                ActionQueueLastChannelDecision = actionSnapshot.ActionQueueLastChannelDecision,
                ActionQueueLastChannelBlockedReason = actionSnapshot.ActionQueueLastChannelBlockedReason,
                ActionQueueChannelOwnerSummary = actionSnapshot.ActionQueueChannelOwnerSummary,
                ActionQueueBridgeBusySummary = actionSnapshot.ActionQueueBridgeBusySummary,
                ActionQueuePendingChannelSummary = actionSnapshot.ActionQueuePendingChannelSummary,
                ActionQueuePendingOwnerSummary = actionSnapshot.ActionQueuePendingOwnerSummary,
                ActionQueueLastAdmissionStatus = actionSnapshot.ActionQueueLastAdmissionStatus,
                ActionQueueLastAdmissionDecision = actionSnapshot.ActionQueueLastAdmissionDecision,
                ActionQueueLastAdmissionReason = actionSnapshot.ActionQueueLastAdmissionReason,
                ActionQueueLastAdmissionKind = actionSnapshot.ActionQueueLastAdmissionKind,
                ActionQueueLastAdmissionSource = actionSnapshot.ActionQueueLastAdmissionSource,
                ActionQueueLastAdmissionScenario = actionSnapshot.ActionQueueLastAdmissionScenario,
                ActionQueueLastAdmissionKey = actionSnapshot.ActionQueueLastAdmissionKey,
                ActionQueueLastAdmissionRequiredChannels = actionSnapshot.ActionQueueLastAdmissionRequiredChannels,
                ActionQueueLastAdmissionBlockingChannels = actionSnapshot.ActionQueueLastAdmissionBlockingChannels,
                ActionQueueLastAdmissionConflictChannels = actionSnapshot.ActionQueueLastAdmissionConflictChannels,
                ActionQueueLastAdmissionPendingConflictSummary = actionSnapshot.ActionQueueLastAdmissionPendingConflictSummary,
                ActionQueueLastAdmissionRunningConflictSummary = actionSnapshot.ActionQueueLastAdmissionRunningConflictSummary,
                ActionQueueLastAdmissionBridgeBusySummary = actionSnapshot.ActionQueueLastAdmissionBridgeBusySummary,
                ActionQueueLastAdmissionOwnerSummary = actionSnapshot.ActionQueueLastAdmissionOwnerSummary,
                ActionQueueLastAdmissionSupersededRequestId = actionSnapshot.ActionQueueLastAdmissionSupersededRequestId,
                ActionQueueLastAdmissionCoalescedRequestId = actionSnapshot.ActionQueueLastAdmissionCoalescedRequestId,
                ActionQueueSupersededPendingCount = actionSnapshot.ActionQueueSupersededPendingCount,
                ActionQueueCoalescedPendingCount = actionSnapshot.ActionQueueCoalescedPendingCount,
                SchedulerLastSelectedRequest = actionSnapshot.SchedulerLastSelectedRequest,
                SchedulerLastSupersededRequest = actionSnapshot.SchedulerLastSupersededRequest,
                SchedulerLastFairnessBucket = actionSnapshot.SchedulerLastFairnessBucket,
                WorldAutomationLastWinner = worldAutomationFairness == null ? string.Empty : worldAutomationFairness.LastWinner,
                WorldAutomationFairnessDebt = worldAutomationFairness == null ? string.Empty : worldAutomationFairness.FairnessDebt,
                WorldAutomationFairnessDecisionUtc = worldAutomationFairness == null ? null : worldAutomationFairness.LastDecisionUtc,
                BackgroundRequestCoalescedCount = actionSnapshot.BackgroundRequestCoalescedCount,
                ExpiredPendingDroppedCount = actionSnapshot.ExpiredPendingDroppedCount,
                ActionQueueCleanupLeaseCount = actionSnapshot.ActionQueueCleanupLeaseCount,
                ActionQueueCleanupLeaseChannels = actionSnapshot.ActionQueueCleanupLeaseChannels,
                ActionQueueLastCleanupOwner = actionSnapshot.ActionQueueLastCleanupOwner,
                ActionQueueLastCleanupReason = actionSnapshot.ActionQueueLastCleanupReason,
                ActionQueueDirectEnqueueCount = actionSnapshot.ActionQueueDirectEnqueueCount,
                ActionQueueLastDirectEnqueueKind = actionSnapshot.ActionQueueLastDirectEnqueueKind,
                ActionQueueLastDirectEnqueueSource = actionSnapshot.ActionQueueLastDirectEnqueueSource,
                ActionQueueLastDirectEnqueueScenario = actionSnapshot.ActionQueueLastDirectEnqueueScenario,
                ActionQueueLastDirectEnqueueAdmissionKey = actionSnapshot.ActionQueueLastDirectEnqueueAdmissionKey,
                ActionQueueLastDirectEnqueueRequiredChannels = actionSnapshot.ActionQueueLastDirectEnqueueRequiredChannels,
                ActionQueueExpiredPendingCount = actionSnapshot.ActionQueueExpiredPendingCount,
                ActionQueueLastPendingExpiryReason = actionSnapshot.ActionQueueLastPendingExpiryReason,
                ItemUseBridgeLastStatus = ItemUseBridge.LastStatus,
                ItemUseBridgeLastMessage = ItemUseBridge.LastMessage,
                ItemUseBridgeLastRequestId = ItemUseBridge.LastRequestId == Guid.Empty ? string.Empty : ItemUseBridge.LastRequestId.ToString(),
                ItemUseBridgePendingRequestId = ItemUseBridge.PendingRequestId == Guid.Empty ? string.Empty : ItemUseBridge.PendingRequestId.ToString(),
                ItemUseBridgePendingAgeMs = ItemUseBridge.PendingAgeMs,
                ItemUseBridgeConsumeCount = ItemUseBridge.ConsumeCount,
                ItemUseBridgeSucceededCount = ItemUseBridge.SucceededCount,
                ItemUseBridgeAttemptedButUnverifiedCount = ItemUseBridge.AttemptedButUnverifiedCount,
                ItemUseBridgeFailedCount = ItemUseBridge.FailedCount,
                ItemCheckWriterOwner = itemCheckWriter == null ? string.Empty : itemCheckWriter.OwnerName,
                ItemCheckWriterOwnerRequestId = itemCheckWriter == null || itemCheckWriter.OwnerRequestId == Guid.Empty ? string.Empty : itemCheckWriter.OwnerRequestId.ToString(),
                ItemCheckWriterPhase = itemCheckWriter == null ? string.Empty : itemCheckWriter.Phase,
                ItemCheckWriterDecisionReason = itemCheckWriter == null ? string.Empty : itemCheckWriter.Reason,
                ItemCheckWriterBlockedCandidates = itemCheckWriter == null ? string.Empty : itemCheckWriter.BlockedCandidatesSummary,
                ItemCheckWriterDecisionUtc = itemCheckWriter == null ? null : (DateTime?)itemCheckWriter.DecidedUtc,
                EnableDiagnosticInputTests = ConfigService.AppSettings.EnableDiagnosticInputTests,
                DiagnosticInputTestSlot = diagnosticSlot,
                DiagnosticInputTestSlotDisplay = diagnosticSlot + 1,
                DiagnosticTestSlot = diagnosticSlot,
                DiagnosticTestSlotDisplay = diagnosticSlot + 1,
                DiagnosticTestSlotItemType = diagnosticSlotInfo.ItemType,
                DiagnosticTestSlotItemName = diagnosticSlotInfo.IsEmpty ? string.Empty : diagnosticSlotInfo.ItemName,
                DiagnosticTestSlotItemStack = diagnosticSlotInfo.ItemStack,
                DiagnosticTestSlotSuitability = diagnosticSlotInfo.Suitability,
                DiagnosticTestSlotHint = diagnosticSlotInfo.Hint,
                ActionEventsPath = DiagnosticActionRecorder.ActionEventsPath,
                LastActionEventWrittenAtUtc = DiagnosticActionRecorder.LastActionEventWrittenAtUtc,
                LastDiagnosticSourceKind = DiagnosticInteractionDiagnostics.LastSourceKind,
                LastDiagnosticButtonId = DiagnosticInteractionDiagnostics.LastButtonId,
                LastDiagnosticButtonLabel = DiagnosticInteractionDiagnostics.LastButtonLabel,
                LastButtonClickUtc = DiagnosticInteractionDiagnostics.LastButtonClickUtc,
                LastButtonResultCode = DiagnosticInteractionDiagnostics.LastButtonResultCode,
                LastButtonMessage = DiagnosticInteractionDiagnostics.LastButtonMessage,
                UiPrimitiveRendererReady = UiPrimitiveRenderer.Ready,
                UiPrimitiveRendererLastMessage = UiPrimitiveRenderer.LastError,
                UiMouseReadAvailable = !string.Equals(DiagnosticInteractionDiagnostics.MouseReadMode, "none", StringComparison.OrdinalIgnoreCase),
                UiMouseReadLastMessage = string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.MouseReadLastError)
                    ? DiagnosticInteractionDiagnostics.MouseReadMode
                    : DiagnosticInteractionDiagnostics.MouseReadLastError,
                UiMouseCaptureAvailable = TerrariaUiMouseCompat.UiMouseCaptureAvailable,
                UiMouseCaptureLastMessage = TerrariaUiMouseCompat.UiMouseCaptureLastMessage,
                UiClickSuppressionAttempted = DiagnosticInteractionDiagnostics.UiClickSuppressionAttempted,
                UiClickSuppressionMode = DiagnosticInteractionDiagnostics.UiClickSuppressionMode,
                UiClickSuppressionSucceeded = DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded,
                ButtonHoverAtUpdatePrefix = DiagnosticInteractionDiagnostics.ButtonHoverAtUpdatePrefix,
                OverlayHoverAtUpdatePrefix = DiagnosticInteractionDiagnostics.OverlayHoverAtUpdatePrefix,
                LastMouseX = DiagnosticInteractionDiagnostics.LastMouseX,
                LastMouseY = DiagnosticInteractionDiagnostics.LastMouseY,
                TerrariaMouseX = DiagnosticInteractionDiagnostics.TerrariaMouseX,
                TerrariaMouseY = DiagnosticInteractionDiagnostics.TerrariaMouseY,
                TerrariaLeftDown = DiagnosticInteractionDiagnostics.TerrariaLeftDown,
                TerrariaLeftReleaseAvailable = DiagnosticInteractionDiagnostics.TerrariaLeftReleaseAvailable,
                TerrariaLeftRelease = DiagnosticInteractionDiagnostics.TerrariaLeftRelease,
                OsClientMouseX = DiagnosticInteractionDiagnostics.OsClientMouseX,
                OsClientMouseY = DiagnosticInteractionDiagnostics.OsClientMouseY,
                OsLeftDown = DiagnosticInteractionDiagnostics.OsLeftDown,
                UiScale = DiagnosticInteractionDiagnostics.UiScale,
                UiScaleAvailable = DiagnosticInteractionDiagnostics.UiScaleAvailable,
                UiScaleMatrixAvailable = DiagnosticInteractionDiagnostics.UiScaleMatrixAvailable,
                MouseReadMode = DiagnosticInteractionDiagnostics.MouseReadMode,
                MouseReadLastError = DiagnosticInteractionDiagnostics.MouseReadLastError,
                HitTestMode = DiagnosticInteractionDiagnostics.HitTestMode,
                HitTestX = DiagnosticInteractionDiagnostics.HitTestX,
                HitTestY = DiagnosticInteractionDiagnostics.HitTestY,
                HitTestConflict = DiagnosticInteractionDiagnostics.HitTestConflict,
                HitTestCandidateSummary = DiagnosticInteractionDiagnostics.HitTestCandidateSummary,
                ClickSource = DiagnosticInteractionDiagnostics.ClickSource,
                LastButtonHitTestMode = DiagnosticInteractionDiagnostics.LastButtonHitTestMode,
                LastButtonClickSource = DiagnosticInteractionDiagnostics.LastButtonClickSource,
                HoveredButtonId = DiagnosticInteractionDiagnostics.HoveredButtonId,
                HoveredButtonLabel = DiagnosticInteractionDiagnostics.HoveredButtonLabel,
                HoveredButtonHint = DiagnosticInteractionDiagnostics.HoveredButtonHint,
                HoveredButtonEnabled = DiagnosticInteractionDiagnostics.HoveredButtonEnabled,
                HoveredButtonVisualX = DiagnosticInteractionDiagnostics.HoveredButtonVisualX,
                HoveredButtonVisualY = DiagnosticInteractionDiagnostics.HoveredButtonVisualY,
                HoveredButtonVisualWidth = DiagnosticInteractionDiagnostics.HoveredButtonVisualWidth,
                HoveredButtonVisualHeight = DiagnosticInteractionDiagnostics.HoveredButtonVisualHeight,
                HoveredButtonHitX = DiagnosticInteractionDiagnostics.HoveredButtonHitX,
                HoveredButtonHitY = DiagnosticInteractionDiagnostics.HoveredButtonHitY,
                HoveredButtonHitWidth = DiagnosticInteractionDiagnostics.HoveredButtonHitWidth,
                HoveredButtonHitHeight = DiagnosticInteractionDiagnostics.HoveredButtonHitHeight,
                LegacyUiLayoutCacheHitCount = LegacyMainWindow.PageLayoutCacheHitCount,
                LegacyUiLayoutCacheMissCount = LegacyMainWindow.PageLayoutCacheMissCount,
                LegacyUiLastFrameVisibleElementCount = LegacyUiElementFrame.LastFrameElementCount,
                LegacyUiHoverReuseCount = LegacyUiElementFrame.HoverReuseCount,
                LegacyUiHoverTooltipCacheHitCount = LegacyMainWindow.HoverTooltipCacheHitCount,
                LegacyUiHoverTooltipCacheMissCount = LegacyMainWindow.HoverTooltipCacheMissCount,
                LegacyUiHoverDiagnosticSuppressedCount = LegacyMainWindow.HoverTooltipDiagnosticSuppressedCount,
                LegacyUiScrollSnapshotSkippedCount = LegacyUiInput.ScrollSnapshotSkippedCount,
                LegacyUiScrollEventCoalescedCount = LegacyUiInput.ScrollEventCoalescedCount,
                LegacyUiRetainedFrameCacheHitCount = LegacyMainWindow.RetainedFrameCacheHitCount,
                LegacyUiRetainedFrameCacheMissCount = LegacyMainWindow.RetainedFrameCacheMissCount,
                LegacyUiRetainedFrameFallbackCount = LegacyMainWindow.RetainedFrameFallbackCount,
                LegacyUiRetainedFrameVisibleElementCount = LegacyMainWindow.RetainedFrameVisibleElementCount,
                LegacyUiActionUpdateSkippedCount = LegacyUiActionService.ActionUpdateSkippedCount,
                LegacyUiActionUpdateRanCount = LegacyUiActionService.ActionUpdateRanCount,
                LegacyUiPendingCommandCountLast = LegacyUiActionService.PendingCommandCountLast,
                LegacyUiDispatchedCommandCountLast = LegacyUiActionService.DispatchedCommandCountLast,
                LegacyUiDispatchElapsedMsLast = LegacyUiActionService.DispatchElapsedMsLast,
                LegacyUiCommandCoalescedCount = LegacyUiActionService.CommandCoalescedCount,
                LegacyUiDragFrameActionSkipCount = LegacyUiActionService.DragFrameActionSkipCount,
                LastDiagnosticHotkey = DiagnosticActionHotkeyService.LastDiagnosticHotkey,
                LastDiagnosticHotkeyUtc = DiagnosticActionHotkeyService.LastDiagnosticHotkeyUtc,
                LastDiagnosticHotkeyMessage = DiagnosticActionHotkeyService.LastDiagnosticHotkeyMessage,
                QuickActionLastKind = QuickActionDiagnostics.LastKind,
                QuickActionLastStatus = QuickActionDiagnostics.LastStatus,
                QuickActionLastResultCode = QuickActionDiagnostics.LastResultCode,
                QuickActionLastMessage = QuickActionDiagnostics.LastMessage,
                MouseTargetLastStatus = MouseTargetDiagnostics.LastStatus,
                MouseTargetLastResultCode = MouseTargetDiagnostics.LastResultCode,
                MouseTargetLastMessage = MouseTargetDiagnostics.LastMessage,
                RuntimeUpdateCount = RuntimePerformanceDiagnostics.RuntimeUpdateCount,
                AverageRuntimeUpdateMs = RuntimePerformanceDiagnostics.AverageRuntimeUpdateMs,
                LastRuntimeUpdateMs = RuntimePerformanceDiagnostics.LastRuntimeUpdateMs,
                LastUpdateStartGapMs = RuntimePerformanceDiagnostics.LastUpdateStartGapMs,
                LastGameStateReadMs = RuntimePerformanceDiagnostics.LastGameStateReadMs,
                LastActionQueueUpdateMs = RuntimePerformanceDiagnostics.LastActionQueueUpdateMs,
                LastInputActionUpdateMs = RuntimePerformanceDiagnostics.LastInputActionUpdateMs,
                LastInformationDrawMs = RuntimePerformanceDiagnostics.LastInformationDrawMs,
                RecentPerformanceWindowCapacitySamples = RuntimePerformanceDiagnostics.RecentWindowCapacitySamples,
                RecentPerformanceWindowSampleCount = RuntimePerformanceDiagnostics.RecentWindowSampleCount,
                RecentRuntimeUpdateAverageMs = RuntimePerformanceDiagnostics.RecentRuntimeUpdateAverageMs,
                RecentGameStateReadAverageMs = RuntimePerformanceDiagnostics.RecentGameStateReadAverageMs,
                RecentActionQueueUpdateAverageMs = RuntimePerformanceDiagnostics.RecentActionQueueUpdateAverageMs,
                RecentInputActionUpdateAverageMs = RuntimePerformanceDiagnostics.RecentInputActionUpdateAverageMs,
                RecentInformationDrawAverageMs = RuntimePerformanceDiagnostics.RecentInformationDrawAverageMs,
                UiTextFastPathHitCount = UiTextRenderer.AnchorFreeFastPathHitCount,
                UiTextFallbackCount = UiTextRenderer.AnchorFreeFastPathFallbackCount,
                LastSlowestStageName = RuntimePerformanceDiagnostics.LastSlowestStageName,
                LastSlowestStageElapsedMs = RuntimePerformanceDiagnostics.LastSlowestStageElapsedMs,
                LastSlowestOperationName = RuntimePerformanceDiagnostics.LastSlowestOperationName,
                LastSlowestOperationElapsedMs = RuntimePerformanceDiagnostics.LastSlowestOperationElapsedMs,
                PerformanceEventsPath = string.IsNullOrWhiteSpace(RuntimePerformanceDiagnostics.PerformanceEventsPath)
                    ? PerformanceHitchRecorder.PerformanceEventsPath
                    : RuntimePerformanceDiagnostics.PerformanceEventsPath,
                PerformanceHitchCount = RuntimePerformanceDiagnostics.PerformanceHitchCount,
                LastPerformanceHitchUtc = RuntimePerformanceDiagnostics.LastPerformanceHitchUtc,
                LastPerformanceHitchReason = RuntimePerformanceDiagnostics.LastPerformanceHitchReason,
                LastPerformanceHitchUpdateGapMs = RuntimePerformanceDiagnostics.LastPerformanceHitchUpdateGapMs,
                LastPerformanceHitchRuntimeUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchRuntimeUpdateMs,
                LastPerformanceHitchGameStateReadMs = RuntimePerformanceDiagnostics.LastPerformanceHitchGameStateReadMs,
                LastPerformanceHitchActionQueueUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchActionQueueUpdateMs,
                LastPerformanceHitchInputActionUpdateMs = RuntimePerformanceDiagnostics.LastPerformanceHitchInputActionUpdateMs,
                LastPerformanceHitchInformationDrawMs = RuntimePerformanceDiagnostics.LastPerformanceHitchInformationDrawMs,
                LastPerformanceHitchSlowestStageName = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestStageName,
                LastPerformanceHitchSlowestStageMs = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestStageMs,
                LastPerformanceHitchSlowestOperationName = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationName,
                LastPerformanceHitchSlowestOperationMs = RuntimePerformanceDiagnostics.LastPerformanceHitchSlowestOperationMs,
                PerformanceOperationEventCount = RuntimePerformanceDiagnostics.PerformanceOperationEventCount,
                LastPerformanceOperationScenario = RuntimePerformanceDiagnostics.LastPerformanceOperationScenario,
                LastPerformanceOperationUtc = RuntimePerformanceDiagnostics.LastPerformanceOperationUtc,
                LastPerformanceOperationElapsedMs = RuntimePerformanceDiagnostics.LastPerformanceOperationElapsedMs,
                LastPerformanceOperationThresholdMs = RuntimePerformanceDiagnostics.LastPerformanceOperationThresholdMs,
                LastPerformanceOperationReason = RuntimePerformanceDiagnostics.LastPerformanceOperationReason,
                LastPerformanceOperationOwnerSummary = RuntimePerformanceDiagnostics.LastPerformanceOperationOwnerSummary,
                ReflectionCacheReady = TerrariaMemberCache.IsInitialized,
                ReflectionCacheMissCount = TerrariaMemberCache.CacheMissCount,
                ReflectionCacheLastMissKey = TerrariaMemberCache.LastMissKey,
                ReflectionCacheLastMissUtc = TerrariaMemberCache.LastMissUtc,
                ReflectionCacheLastError = TerrariaMemberCache.LastError,
                InputCompatReady = TerrariaInputCompat.InputCompatReady,
                SelectedItemGetterReady = TerrariaInputCompat.SelectedItemGetterReady,
                SelectedItemSelectorReady = TerrariaInputCompat.SelectedItemSelectorReady,
                SelectedItemAccessorReady = TerrariaInputCompat.SelectedItemAccessorReady,
                PlayerTypeName = TerrariaInputCompat.PlayerTypeName,
                LastInputCompatError = TerrariaInputCompat.LastInputCompatError,
                ConfigLastSaveUtc = configSave == null ? (DateTime?)null : configSave.Utc,
                ConfigLastSaveSucceeded = configSave != null && configSave.Succeeded,
                ConfigLastSaveSummary = configSave == null ? string.Empty : configSave.Summary,
                ConfigLastSaveAppSettingsSucceeded = configAppSave != null && configAppSave.Succeeded,
                ConfigLastSaveAppSettingsPath = configAppSave == null ? string.Empty : configAppSave.Path,
                ConfigLastSaveAppSettingsError = configAppSave == null ? string.Empty : configAppSave.Error,
                ConfigLastSaveFeatureSettingsSucceeded = configFeatureSave != null && configFeatureSave.Succeeded,
                ConfigLastSaveFeatureSettingsPath = configFeatureSave == null ? string.Empty : configFeatureSave.Path,
                ConfigLastSaveFeatureSettingsError = configFeatureSave == null ? string.Empty : configFeatureSave.Error,
                ConfigLastSaveHotkeySettingsSucceeded = configHotkeySave != null && configHotkeySave.Succeeded,
                ConfigLastSaveHotkeySettingsPath = configHotkeySave == null ? string.Empty : configHotkeySave.Path,
                ConfigLastSaveHotkeySettingsError = configHotkeySave == null ? string.Empty : configHotkeySave.Error,
                AutoStackLastDecision = autoStack == null ? string.Empty : autoStack.LastDecision,
                AutoStackLastInventorySignature = autoStack == null ? string.Empty : autoStack.LastInventorySignature,
                AutoStackLastPendingItemIds = autoStack == null ? string.Empty : autoStack.LastPendingItemIds,
                AutoStackLastDetectedItemIds = autoStack == null ? string.Empty : autoStack.LastDetectedItemIds,
                AutoStackPendingSinceTick = autoStack == null ? 0 : autoStack.PendingSinceTick,
                AutoStackLastPendingChangeTick = autoStack == null ? -1 : autoStack.LastPendingChangeTick,
                AutoStackLastPendingClearReason = autoStack == null ? string.Empty : autoStack.LastPendingClearReason,
                AutoStackPendingTransactionState = autoStack == null ? string.Empty : autoStack.PendingTransactionState,
                AutoStackPendingRetryCount = autoStack == null ? 0 : autoStack.PendingRetryCount,
                AutoStackLastSubmitRequestId = autoStack == null ? string.Empty : autoStack.LastSubmitRequestId,
                AutoStackLastResult = autoStack == null ? string.Empty : autoStack.LastResult,
                AutoStackLastUnverifiedReason = autoStack == null ? string.Empty : autoStack.LastUnverifiedReason,
                AutoStackInventoryTransactionSlots = autoStack == null ? string.Empty : autoStack.InventoryTransactionSlots,
                AutoStackInventoryTransactionBlockingReason = autoStack == null ? string.Empty : autoStack.InventoryTransactionBlockingReason,
                AutoStackActionResultDeliveryMode = autoStack == null ? string.Empty : autoStack.ActionResultDeliveryMode,
                AutoStackLastDecisionUtc = autoStack == null ? null : autoStack.LastDecisionUtc,
                AutoSellLastDecision = autoSell == null ? string.Empty : autoSell.LastDecision,
                AutoSellLastInventorySignature = autoSell == null ? string.Empty : autoSell.LastInventorySignature,
                AutoSellLastItemIds = autoSell == null ? string.Empty : autoSell.LastSellItemIds,
                AutoSellLastDecisionUtc = autoSell == null ? null : autoSell.LastDecisionUtc,
                AutoDiscardLastDecision = autoDiscard == null ? string.Empty : autoDiscard.LastDecision,
                AutoDiscardLastInventorySignature = autoDiscard == null ? string.Empty : autoDiscard.LastInventorySignature,
                AutoDiscardLastItemIds = autoDiscard == null ? string.Empty : autoDiscard.LastDiscardItemIds,
                AutoDiscardLastDecisionUtc = autoDiscard == null ? null : autoDiscard.LastDecisionUtc,
                QuickReforgeLastDecision = quickReforge == null ? string.Empty : quickReforge.LastDecision,
                QuickReforgeLastTargetPrefixes = quickReforge == null ? string.Empty : quickReforge.LastTargetPrefixes,
                QuickReforgeLastMatchedPrefix = quickReforge == null ? string.Empty : quickReforge.LastMatchedPrefix,
                QuickReforgeLastDecisionUtc = quickReforge == null ? null : quickReforge.LastDecisionUtc,
                AutoTaxCollectLastDecision = autoTaxCollect == null ? string.Empty : autoTaxCollect.LastDecision,
                AutoTaxCollectLastDecisionUtc = autoTaxCollect == null ? null : autoTaxCollect.LastDecisionUtc,
                AutoTaxCollectTargetNpcIndex = autoTaxCollect == null ? -1 : autoTaxCollect.LastTargetNpcIndex,
                AutoTaxCollectTargetWhoAmI = autoTaxCollect == null ? -1 : autoTaxCollect.LastTargetWhoAmI,
                AutoTaxCollectTargetName = autoTaxCollect == null ? string.Empty : autoTaxCollect.LastTargetName,
                AutoTaxCollectTaxMoney = autoTaxCollect == null ? 0 : autoTaxCollect.LastTaxMoney,
                AutoTaxCollectLastRequestId = autoTaxCollect == null ? string.Empty : autoTaxCollect.LastRequestId,
                AutoCaptureCritterLastDecision = autoCaptureCritter == null ? string.Empty : autoCaptureCritter.LastDecision,
                AutoCaptureCritterLastDecisionUtc = autoCaptureCritter == null ? null : autoCaptureCritter.LastDecisionUtc,
                AutoCaptureCritterBugNetSlot = autoCaptureCritter == null ? -1 : autoCaptureCritter.BugNetSlot,
                AutoCaptureCritterBugNetItemType = autoCaptureCritter == null ? 0 : autoCaptureCritter.BugNetItemType,
                AutoCaptureCritterTargetNpcIndex = autoCaptureCritter == null ? -1 : autoCaptureCritter.TargetNpcIndex,
                AutoCaptureCritterTargetNpcType = autoCaptureCritter == null ? 0 : autoCaptureCritter.TargetNpcType,
                AutoCaptureCritterFishingProtectionState = autoCaptureCritter == null ? string.Empty : autoCaptureCritter.FishingProtectionState,
                AutoHarvestLastDecision = autoHarvest == null ? string.Empty : autoHarvest.LastDecision,
                AutoHarvestLastDecisionUtc = autoHarvest == null ? null : autoHarvest.LastDecisionUtc,
                AutoHarvestLastAction = autoHarvest == null ? string.Empty : autoHarvest.LastAction,
                AutoHarvestToolSlot = autoHarvest == null ? -1 : autoHarvest.ToolSlot,
                AutoHarvestToolItemType = autoHarvest == null ? 0 : autoHarvest.ToolItemType,
                AutoHarvestTargetTileX = autoHarvest == null ? -1 : autoHarvest.TargetTileX,
                AutoHarvestTargetTileY = autoHarvest == null ? -1 : autoHarvest.TargetTileY,
                AutoHarvestTargetSeedItemType = autoHarvest == null ? 0 : autoHarvest.TargetSeedItemType,
                AutoHarvestPendingReplantCount = autoHarvest == null ? 0 : autoHarvest.PendingReplantCount,
                QuickBagOpenLastDecision = quickBagOpen == null ? string.Empty : quickBagOpen.LastDecision,
                QuickBagOpenLastDecisionUtc = quickBagOpen == null ? null : quickBagOpen.LastDecisionUtc,
                QuickBagOpenBagSlot = quickBagOpen == null ? -1 : quickBagOpen.BagSlot,
                QuickBagOpenBagItemType = quickBagOpen == null ? 0 : quickBagOpen.BagItemType,
                QuickBagOpenBagItemName = quickBagOpen == null ? string.Empty : quickBagOpen.BagItemName,
                AutoDepositCoinsLastDecision = autoDepositCoins == null ? string.Empty : autoDepositCoins.LastDecision,
                AutoDepositCoinsLastDecisionUtc = autoDepositCoins == null ? null : autoDepositCoins.LastDecisionUtc,
                AutoDepositCoinsLastInventorySignature = autoDepositCoins == null ? string.Empty : autoDepositCoins.LastInventorySignature,
                AutoDepositCoinsLastCoinItemIds = autoDepositCoins == null ? string.Empty : autoDepositCoins.LastCoinItemIds,
                AutoExtractinatorLastDecision = autoExtractinator == null ? string.Empty : autoExtractinator.LastDecision,
                AutoExtractinatorLastDecisionUtc = autoExtractinator == null ? null : autoExtractinator.LastDecisionUtc,
                AutoExtractinatorItemSlot = autoExtractinator == null ? -1 : autoExtractinator.ItemSlot,
                AutoExtractinatorItemType = autoExtractinator == null ? 0 : autoExtractinator.ItemType,
                AutoExtractinatorTileX = autoExtractinator == null ? -1 : autoExtractinator.ExtractinatorTileX,
                AutoExtractinatorTileY = autoExtractinator == null ? -1 : autoExtractinator.ExtractinatorTileY,
                AutoExtractinatorTileType = autoExtractinator == null ? 0 : autoExtractinator.ExtractinatorTileType,
                KeepFavoritedLastDecision = keepFavorited == null ? string.Empty : keepFavorited.LastDecision,
                KeepFavoritedLastDecisionUtc = keepFavorited == null ? null : keepFavorited.LastDecisionUtc,
                KeepFavoritedSlot = keepFavorited == null ? -1 : keepFavorited.Slot,
                KeepFavoritedItemType = keepFavorited == null ? 0 : keepFavorited.ItemType,
                KeepFavoritedSignature = keepFavorited == null ? string.Empty : keepFavorited.Signature,
                InformationEnabledSummary = information == null ? string.Empty : information.EnabledSummary,
                InformationNpcLabelsDrawn = information == null ? 0 : information.NpcLabelsDrawn,
                InformationChestLabelsDrawn = information == null ? 0 : information.ChestLabelsDrawn,
                InformationSignTextLabelsDrawn = information == null ? 0 : information.SignTextLabelsDrawn,
                InformationTombstoneTextLabelsDrawn = information == null ? 0 : information.TombstoneTextLabelsDrawn,
                InformationTileHighlightsDrawn = information == null ? 0 : information.TileHighlightsDrawn,
                InformationStatusLinesDrawn = information == null ? 0 : information.StatusLinesDrawn,
                InformationLastDrawElapsedMs = information == null ? 0d : information.LastDrawElapsedMs,
                InformationLastSkipReason = information == null ? string.Empty : information.LastSkipReason,
                InformationStatusPanelLayoutCacheHitCount = InformationStatusPanelService.LayoutCacheHitCount,
                InformationStatusPanelLayoutCacheMissCount = InformationStatusPanelService.LayoutCacheMissCount,
                InformationSignTextLayoutCacheHitCount = information == null ? 0 : information.SignTextLayoutCacheHitCount,
                InformationSignTextLayoutCacheMissCount = information == null ? 0 : information.SignTextLayoutCacheMissCount,
                InformationWorldLabelSnapshotRefreshCount = information == null ? 0 : information.WorldLabelSnapshotRefreshCount,
                InformationNpcLabelSnapshotRefreshCount = information == null ? 0 : information.NpcLabelSnapshotRefreshCount,
                InformationChestLabelSnapshotRefreshCount = information == null ? 0 : information.ChestLabelSnapshotRefreshCount,
                InformationChestLabelSortRefreshCount = information == null ? 0 : information.ChestLabelSortRefreshCount,
                InformationChestAlwaysScanCacheHitCount = information == null ? 0 : information.ChestAlwaysScanCacheHitCount,
                InformationChestAlwaysScanCacheMissCount = information == null ? 0 : information.ChestAlwaysScanCacheMissCount,
                InformationChestAlwaysLastDirtyReason = information == null ? string.Empty : information.ChestAlwaysLastDirtyReason,
                InformationChestAlwaysSafeRefreshCount = information == null ? 0 : information.ChestAlwaysSafeRefreshCount,
                InformationChestAlwaysTilesVisitedLast = information == null ? 0 : information.ChestAlwaysTilesVisitedLast,
                InformationChestAlwaysTypedTileFastPathStatus = information == null ? string.Empty : information.ChestAlwaysTypedTileFastPathStatus,
                InformationChestAlwaysNameCacheHitCount = information == null ? 0 : information.ChestAlwaysNameCacheHitCount,
                InformationChestAlwaysNameCacheMissCount = information == null ? 0 : information.ChestAlwaysNameCacheMissCount,
                InformationChestAlwaysPartialScanFrameCount = information == null ? 0 : information.ChestAlwaysPartialScanFrameCount,
                InformationChestAlwaysPartialScanPendingCount = information == null ? 0 : information.ChestAlwaysPartialScanPendingCount,
                InformationChestAlwaysStableSnapshotId = information == null ? 0 : information.ChestAlwaysStableSnapshotId,
                InformationWorldContextCacheHitCount = information == null ? 0 : information.WorldContextCacheHitCount,
                InformationWorldContextCacheMissCount = information == null ? 0 : information.WorldContextCacheMissCount,
                InformationWorldContextProfile = information == null ? string.Empty : information.WorldContextProfile,
                InformationWorldContextFileDataRefreshCount = information == null ? 0 : information.WorldContextFileDataRefreshCount,
                InformationStatusLineCacheHitCount = information == null ? 0 : information.StatusLineCacheHitCount,
                InformationStatusLineCacheMissCount = information == null ? 0 : information.StatusLineCacheMissCount,
                InformationFishingCatchEarlyCacheHitCount = information == null ? 0 : information.FishingCatchEarlyCacheHitCount,
                InformationFishingCatchEarlyCacheMissCount = information == null ? 0 : information.FishingCatchEarlyCacheMissCount,
                InformationFishingWaterScanCount = information == null ? 0 : information.FishingWaterScanCount,
                InformationFishingConditionsReadCount = information == null ? 0 : information.FishingConditionsReadCount,
                InformationFishingBobberObserverFreshInactiveSkipCount = information == null ? 0 : information.FishingBobberObserverFreshInactiveSkipCount,
                InformationFishingProjectileFallbackScanCount = information == null ? 0 : information.FishingProjectileFallbackScanCount,
                FishingAutomationNeedsTick = settingsSnapshot.FishingAutomationNeedsTick,
                FishingDisplayNeedsCatchResolver = settingsSnapshot.FishingDisplayNeedsCatchResolver,
                FishingHasResidualState = fishingHasResidualState,
                FishingSessionActive = fishing != null && fishing.FishingSessionActive,
                FishingLastDecision = fishing == null ? string.Empty : fishing.FishingLastDecision,
                FishingLastSkipReason = fishing == null ? string.Empty : fishing.FishingLastSkipReason,
                FishingCurrentBobberIdentity = fishing == null ? -1 : fishing.FishingCurrentBobberIdentity,
                FishingLastProcessedHookIdentity = fishing == null ? -1 : fishing.FishingLastProcessedHookIdentity,
                FishingWaitingForBobberGone = fishing != null && fishing.FishingWaitingForBobberGone,
                FishingRecastDelayTicks = fishing == null ? 0 : fishing.FishingRecastDelayTicks,
                FishingRecastWaitingForBobber = fishing != null && fishing.FishingRecastWaitingForBobber,
                FishingRecastBobberWaitTicks = fishing == null ? 0 : fishing.FishingRecastBobberWaitTicks,
                FishingRecastRetryCount = fishing == null ? 0 : fishing.FishingRecastRetryCount,
                FishingFilterSkipInProgress = fishing != null && fishing.FishingFilterSkipInProgress,
                FishingFilterSkipRequestId = fishing == null ? string.Empty : fishing.FishingFilterSkipRequestId,
                FishingFilterSkipWaitingForBobberGone = fishing != null && fishing.FishingFilterSkipWaitingForBobberGone,
                FishingFilterSkipTemporarySlot = fishing == null ? -1 : fishing.FishingFilterSkipTemporarySlot,
                FishingFilterSkipLastResult = fishing == null ? string.Empty : fishing.FishingFilterSkipLastResult,
                FishingFilterSkipRestoreFailureReason = fishing == null ? string.Empty : fishing.FishingFilterSkipRestoreFailureReason,
                FishingCastWorldX = fishing == null ? 0f : fishing.FishingCastWorldX,
                FishingCastWorldY = fishing == null ? 0f : fishing.FishingCastWorldY,
                FishingOriginalLoadoutIndex = fishing == null ? -1 : fishing.FishingOriginalLoadoutIndex,
                FishingTargetLoadoutIndex = fishing == null ? -1 : fishing.FishingTargetLoadoutIndex,
                FishingAutoEquipmentApplied = fishing != null && fishing.FishingAutoEquipmentApplied,
                FishingAutoEquipmentPendingRestoreCount = fishing == null ? 0 : fishing.FishingAutoEquipmentPendingRestoreCount,
                FishingAutoEquipmentLastDecision = fishing == null ? string.Empty : fishing.FishingAutoEquipmentLastDecision,
                FishingAutoEquipmentLastSkipReason = fishing == null ? string.Empty : fishing.FishingAutoEquipmentLastSkipReason,
                FishingAutoEquipmentAppliedMoveCount = fishing == null ? 0 : fishing.FishingAutoEquipmentAppliedMoveCount,
                FishingAutoEquipmentStillHoldingOriginalRod = fishing != null && fishing.FishingAutoEquipmentStillHoldingOriginalRod,
                FishingAutoEquipmentManualInventoryInteractionDetected = fishing != null && fishing.FishingAutoEquipmentManualInventoryInteractionDetected,
                FishingQuestFishStoreCooldownTicks = fishing == null ? 0 : fishing.FishingQuestFishStoreCooldownTicks,
                FishingQuestFishLastItemId = fishing == null ? 0 : fishing.FishingQuestFishLastItemId,
                FishingQuestFishLastSlotCount = fishing == null ? 0 : fishing.FishingQuestFishLastSlotCount,
                FishingAutoStoreLastMode = fishing == null ? string.Empty : fishing.FishingAutoStoreLastMode,
                FishingAutoStoreLastInventorySignature = fishing == null ? string.Empty : fishing.FishingAutoStoreLastInventorySignature,
                FishingAutoStoreLastPendingItemIds = fishing == null ? string.Empty : fishing.FishingAutoStoreLastPendingItemIds,
                FishingAutoStoreLastDiagnosticMessage = fishing == null ? string.Empty : fishing.FishingAutoStoreLastDiagnosticMessage,
                FishingHookInstalled = fishing != null && fishing.FishingHookInstalled,
                FishingHookLastObservationTick = fishing == null ? 0 : fishing.FishingHookLastObservationTick,
                FishingFallbackScanExecutedCount = fishing == null ? 0 : fishing.FishingFallbackScanExecutedCount,
                FishingFallbackScanSkippedHookFreshCount = fishing == null ? 0 : fishing.FishingFallbackScanSkippedHookFreshCount,
                FishingFallbackScanForcedDisappearanceConfirmationCount = fishing == null ? 0 : fishing.FishingFallbackScanForcedDisappearanceConfirmationCount,
                FishingAutomationDispatchReason = fishing == null ? string.Empty : fishing.FishingAutomationDispatchReason,
                FishingAutomationDispatchCadenceTicks = fishing == null ? 0 : fishing.FishingAutomationDispatchCadenceTicks,
                FishingAutomationIdleFastSkipCount = fishing == null ? 0 : fishing.FishingAutomationIdleFastSkipCount,
                FishingAutomationIdleWatchdogTickCount = fishing == null ? 0 : fishing.FishingAutomationIdleWatchdogTickCount,
                FishingObserverFreshActiveCount = fishing == null ? 0 : fishing.FishingObserverFreshActiveCount,
                FishingObserverFreshInactiveSkipCount = fishing == null ? 0 : fishing.FishingObserverFreshInactiveSkipCount,
                FishingFallbackScanIdleSkippedCount = fishing == null ? 0 : fishing.FishingFallbackScanIdleSkippedCount,
                FishingFallbackScanHookStaleCount = fishing == null ? 0 : fishing.FishingFallbackScanHookStaleCount,
                FishingTickSubpathLast = fishing == null ? string.Empty : fishing.FishingTickSubpathLast,
                FishingResidualStateMask = fishing == null ? 0 : fishing.FishingResidualStateMask,
                FishingFilterMode = fishing == null || string.IsNullOrWhiteSpace(fishing.FishingFilterMode) ? settingsSnapshot.FishingFilterMode : fishing.FishingFilterMode,
                FishingFilterMatchMode = fishing == null || string.IsNullOrWhiteSpace(fishing.FishingFilterMatchMode) ? settingsSnapshot.FishingFilterMatchMode : fishing.FishingFilterMatchMode,
                FishingFilterCatchKind = fishing == null ? string.Empty : fishing.FishingFilterCatchKind,
                FishingFilterCatchId = fishing == null ? 0 : fishing.FishingFilterCatchId,
                FishingFilterCatchName = fishing == null ? string.Empty : fishing.FishingFilterCatchName,
                FishingFilterDecision = fishing == null ? string.Empty : fishing.FishingFilterDecision,
                FishingFilterDecisionReason = fishing == null ? string.Empty : fishing.FishingFilterDecisionReason,
                FishingFilterMatchedRule = fishing == null ? string.Empty : fishing.FishingFilterMatchedRule,
                FishingFilterDryRun = fishing != null && fishing.FishingFilterDryRun,
                FishingFilterCutRodSkipEnabled = settingsSnapshot.FishingFilterCutRodSkipEnabled,
                MovementSimulatedJumpEnabled = simulatedJump != null && simulatedJump.Enabled,
                MovementSimulatedJumpLastTriggered = simulatedJump != null && simulatedJump.LastTriggered,
                MovementSimulatedJumpLastTriggerUtc = simulatedJump == null ? null : simulatedJump.LastTriggerUtc,
                MovementSimulatedJumpLastDecision = simulatedJump == null ? string.Empty : simulatedJump.LastDecision,
                MovementSimulatedJumpLastSkipReason = simulatedJump == null ? string.Empty : simulatedJump.LastSkipReason,
                MovementSimulatedJumpLastDecisionUtc = simulatedJump == null ? null : simulatedJump.LastDecisionUtc,
                MovementSimulatedJumpLastTick = simulatedJump == null ? 0 : simulatedJump.LastTick,
                MovementSimulatedJumpPendingActionCount = simulatedJump == null ? 0 : simulatedJump.PendingActionCount,
                MovementSimulatedJumpRunningActionKind = simulatedJump == null ? string.Empty : simulatedJump.RunningActionKind,
                MovementSimulatedJumpItemUseBridgeBusy = simulatedJump != null && simulatedJump.ItemUseBridgeBusy,
                MovementSimulatedJumpTextInputFocused = simulatedJump != null && simulatedJump.TextInputFocused,
                MovementSimulatedJumpTextInputReason = simulatedJump == null ? string.Empty : simulatedJump.TextInputReason,
                MovementSimulatedJumpHeld = simulatedJump != null && simulatedJump.JumpHeld,
                MovementSimulatedJumpDownHeld = simulatedJump != null && simulatedJump.DownHeld,
                MovementSimulatedJumpPlayerControllable = simulatedJump != null && simulatedJump.PlayerControllable,
                MovementSimulatedJumpAvailableOpportunity = simulatedJump != null && simulatedJump.AvailableJumpOpportunity,
                MovementSimulatedJumpGroundedOrSliding = simulatedJump != null && simulatedJump.GroundedOrSliding,
                MovementSimulatedJumpAerialWindow = simulatedJump != null && simulatedJump.AerialJumpWindow,
                MovementSimulatedJumpHasAirJump = simulatedJump != null && simulatedJump.HasAirJump,
                MovementSimulatedJumpHasRocketJump = simulatedJump != null && simulatedJump.HasRocketJump,
                MovementSimulatedJumpHasWingFlight = simulatedJump != null && simulatedJump.HasWingFlight,
                MovementSimulatedJumpMountActive = simulatedJump != null && simulatedJump.MountActive,
                MovementSimulatedJumpMountCanFlyKnown = simulatedJump != null && simulatedJump.MountCanFlyKnown,
                MovementSimulatedJumpMountCanFly = simulatedJump != null && simulatedJump.MountCanFly,
                MovementSimulatedJumpCapabilitySummary = simulatedJump == null ? string.Empty : simulatedJump.CapabilitySummary,
                MovementSimulatedJumpSubmittedCount = simulatedJump == null ? 0 : simulatedJump.SubmittedCount,
                MovementSimulatedJumpSkippedCount = simulatedJump == null ? 0 : simulatedJump.SkippedCount,
                MovementContinuousDashEnabled = continuousDash != null && continuousDash.Enabled,
                MovementContinuousDashMode = continuousDash == null ? string.Empty : continuousDash.Mode,
                MovementContinuousDashLastTriggered = continuousDash != null && continuousDash.LastTriggered,
                MovementContinuousDashLastTriggerDirection = continuousDash == null ? 0 : continuousDash.LastTriggerDirection,
                MovementContinuousDashLastTriggerUtc = continuousDash == null ? null : continuousDash.LastTriggerUtc,
                MovementContinuousDashLastDecision = continuousDash == null ? string.Empty : continuousDash.LastDecision,
                MovementContinuousDashLastSkipReason = continuousDash == null ? string.Empty : continuousDash.LastSkipReason,
                MovementContinuousDashLastDecisionUtc = continuousDash == null ? null : continuousDash.LastDecisionUtc,
                MovementContinuousDashLastTick = continuousDash == null ? 0 : continuousDash.LastTick,
                MovementContinuousDashPendingActionCount = continuousDash == null ? 0 : continuousDash.PendingActionCount,
                MovementContinuousDashRunningActionKind = continuousDash == null ? string.Empty : continuousDash.RunningActionKind,
                MovementContinuousDashTextInputFocused = continuousDash != null && continuousDash.TextInputFocused,
                MovementContinuousDashTextInputReason = continuousDash == null ? string.Empty : continuousDash.TextInputReason,
                MovementContinuousDashPlayerControllable = continuousDash != null && continuousDash.PlayerControllable,
                MovementContinuousDashLeftHeld = continuousDash != null && continuousDash.LeftHeld,
                MovementContinuousDashRightHeld = continuousDash != null && continuousDash.RightHeld,
                MovementContinuousDashHeldDirection = continuousDash == null ? 0 : continuousDash.HeldDirection,
                MovementContinuousDashHasDashAbility = continuousDash != null && continuousDash.HasDashAbility,
                MovementContinuousDashAbilitySource = continuousDash == null ? string.Empty : continuousDash.DashAbilitySource,
                MovementContinuousDashDashType = continuousDash == null ? 0 : continuousDash.DashType,
                MovementContinuousDashDashDelay = continuousDash == null ? 0 : continuousDash.DashDelay,
                MovementContinuousDashCooldownReady = continuousDash != null && continuousDash.DashCooldownReady,
                MovementContinuousDashMountActive = continuousDash != null && continuousDash.MountActive,
                MovementContinuousDashMountType = continuousDash == null ? -1 : continuousDash.MountType,
                MovementContinuousDashMountCanDashKnown = continuousDash != null && continuousDash.MountCanDashKnown,
                MovementContinuousDashMountCanDash = continuousDash != null && continuousDash.MountCanDash,
                MovementContinuousDashCapabilitySummary = continuousDash == null ? string.Empty : continuousDash.CapabilitySummary,
                MovementContinuousDashArmedDirection = continuousDash == null ? 0 : continuousDash.ArmedDirection,
                MovementContinuousDashArmedCancelReason = continuousDash == null ? string.Empty : continuousDash.ArmedCancelReason,
                MovementContinuousDashArmedCancelCount = continuousDash == null ? 0 : continuousDash.ArmedCancelCount,
                MovementContinuousDashHookInstalled = continuousDash != null && continuousDash.DashMovementHookInstalled,
                MovementContinuousDashHookMessage = continuousDash == null ? string.Empty : continuousDash.DashMovementHookMessage,
                MovementContinuousDashQueuedPulsePending = continuousDash != null && continuousDash.QueuedPulsePending,
                MovementContinuousDashLastPulseApplied = continuousDash != null && continuousDash.LastPulseApplied,
                MovementContinuousDashLastPulseDirection = continuousDash == null ? 0 : continuousDash.LastPulseDirection,
                MovementContinuousDashLastPulseUtc = continuousDash == null ? null : continuousDash.LastPulseUtc,
                MovementContinuousDashLastPulseMessage = continuousDash == null ? string.Empty : continuousDash.LastPulseMessage,
                MovementContinuousDashLastPulseWasFallback = continuousDash != null && continuousDash.LastPulseWasFallback,
                MovementContinuousDashLastPulseResetMessage = continuousDash == null ? string.Empty : continuousDash.LastPulseResetMessage,
                MovementContinuousDashLastCompatError = continuousDash == null ? string.Empty : continuousDash.LastCompatError,
                MovementContinuousDashSubmittedCount = continuousDash == null ? 0 : continuousDash.SubmittedCount,
                MovementContinuousDashSkippedCount = continuousDash == null ? 0 : continuousDash.SkippedCount,
                MovementTeleportCorrectionEnabled = teleportCorrection != null && teleportCorrection.Enabled,
                MovementTeleportCorrectionHookInstalled = teleportCorrection != null && teleportCorrection.HookInstalled,
                MovementTeleportCorrectionHookMethod = teleportCorrection == null ? string.Empty : teleportCorrection.HookMethod,
                MovementTeleportCorrectionHookMessage = teleportCorrection == null ? string.Empty : teleportCorrection.HookMessage,
                MovementTeleportCorrectionLastDecision = teleportCorrection == null ? string.Empty : teleportCorrection.LastDecision,
                MovementTeleportCorrectionLastSkipReason = teleportCorrection == null ? string.Empty : teleportCorrection.LastSkipReason,
                MovementTeleportCorrectionLastDecisionUtc = teleportCorrection == null ? null : teleportCorrection.LastDecisionUtc,
                MovementTeleportCorrectionItemType = teleportCorrection == null ? 0 : teleportCorrection.ItemType,
                MovementTeleportCorrectionItemName = teleportCorrection == null ? string.Empty : teleportCorrection.ItemName,
                MovementTeleportCorrectionOriginalMouseWorldX = teleportCorrection == null ? 0d : teleportCorrection.OriginalMouseWorldX,
                MovementTeleportCorrectionOriginalMouseWorldY = teleportCorrection == null ? 0d : teleportCorrection.OriginalMouseWorldY,
                MovementTeleportCorrectionOriginalMouseScreenX = teleportCorrection == null ? -1 : teleportCorrection.OriginalMouseScreenX,
                MovementTeleportCorrectionOriginalMouseScreenY = teleportCorrection == null ? -1 : teleportCorrection.OriginalMouseScreenY,
                MovementTeleportCorrectionOriginalTopLeftX = teleportCorrection == null ? 0d : teleportCorrection.OriginalTopLeftX,
                MovementTeleportCorrectionOriginalTopLeftY = teleportCorrection == null ? 0d : teleportCorrection.OriginalTopLeftY,
                MovementTeleportCorrectionOriginalSafe = teleportCorrection != null && teleportCorrection.OriginalSafe,
                MovementTeleportCorrectionSearchRadiusPixels = teleportCorrection == null ? 0 : teleportCorrection.SearchRadiusPixels,
                MovementTeleportCorrectionSearchStepPixels = teleportCorrection == null ? 0 : teleportCorrection.SearchStepPixels,
                MovementTeleportCorrectionCandidateCount = teleportCorrection == null ? 0 : teleportCorrection.CandidateCount,
                MovementTeleportCorrectionValidCandidateCount = teleportCorrection == null ? 0 : teleportCorrection.ValidCandidateCount,
                MovementTeleportCorrectionNearestCandidateDistance = teleportCorrection == null ? 0d : teleportCorrection.NearestCandidateDistance,
                MovementTeleportCorrectionCorrectedTopLeftX = teleportCorrection == null ? 0d : teleportCorrection.CorrectedTopLeftX,
                MovementTeleportCorrectionCorrectedTopLeftY = teleportCorrection == null ? 0d : teleportCorrection.CorrectedTopLeftY,
                MovementTeleportCorrectionCorrectedMouseWorldX = teleportCorrection == null ? 0d : teleportCorrection.CorrectedMouseWorldX,
                MovementTeleportCorrectionCorrectedMouseWorldY = teleportCorrection == null ? 0d : teleportCorrection.CorrectedMouseWorldY,
                MovementTeleportCorrectionCorrectedMouseScreenX = teleportCorrection == null ? -1 : teleportCorrection.CorrectedMouseScreenX,
                MovementTeleportCorrectionCorrectedMouseScreenY = teleportCorrection == null ? -1 : teleportCorrection.CorrectedMouseScreenY,
                MovementTeleportCorrectionMouseCaptureSucceeded = teleportCorrection != null && teleportCorrection.MouseCaptureSucceeded,
                MovementTeleportCorrectionMouseApplySucceeded = teleportCorrection != null && teleportCorrection.MouseApplySucceeded,
                MovementTeleportCorrectionMouseRestoreSucceeded = teleportCorrection != null && teleportCorrection.MouseRestoreSucceeded,
                MovementTeleportCorrectionVanillaContinued = teleportCorrection != null && teleportCorrection.VanillaContinued,
                MovementTeleportCorrectionLastCompatError = teleportCorrection == null ? string.Empty : teleportCorrection.LastCompatError,
                MovementTeleportCorrectionAppliedCount = teleportCorrection == null ? 0 : teleportCorrection.AppliedCount,
                MovementTeleportCorrectionSkippedCount = teleportCorrection == null ? 0 : teleportCorrection.SkippedCount,
                MovementSafeLandingEnabled = safeLanding != null && safeLanding.Enabled,
                MovementSafeLandingLastTriggered = safeLanding != null && safeLanding.LastTriggered,
                MovementSafeLandingLastTriggerUtc = safeLanding == null ? null : safeLanding.LastTriggerUtc,
                MovementSafeLandingLastDecision = safeLanding == null ? string.Empty : safeLanding.LastDecision,
                MovementSafeLandingLastSkipReason = safeLanding == null ? string.Empty : safeLanding.LastSkipReason,
                MovementSafeLandingLastDecisionUtc = safeLanding == null ? null : safeLanding.LastDecisionUtc,
                MovementSafeLandingLastTick = safeLanding == null ? 0 : safeLanding.LastTick,
                MovementSafeLandingPendingActionCount = safeLanding == null ? 0 : safeLanding.PendingActionCount,
                MovementSafeLandingRunningActionKind = safeLanding == null ? string.Empty : safeLanding.RunningActionKind,
                MovementSafeLandingTextInputFocused = safeLanding != null && safeLanding.TextInputFocused,
                MovementSafeLandingTextInputReason = safeLanding == null ? string.Empty : safeLanding.TextInputReason,
                MovementSafeLandingPlayerControllable = safeLanding != null && safeLanding.PlayerControllable,
                MovementSafeLandingDangerous = safeLanding != null && safeLanding.Dangerous,
                MovementSafeLandingRescueWindow = safeLanding != null && safeLanding.RescueWindow,
                MovementSafeLandingAlreadySafe = safeLanding != null && safeLanding.AlreadySafe,
                MovementSafeLandingSafeReason = safeLanding == null ? string.Empty : safeLanding.SafeReason,
                MovementSafeLandingRawCreativeGodMode = safeLanding != null && safeLanding.RawCreativeGodMode,
                MovementSafeLandingRawNoFallDmg = safeLanding != null && safeLanding.RawNoFallDmg,
                MovementSafeLandingRawSlowFall = safeLanding != null && safeLanding.RawSlowFall,
                MovementSafeLandingRawWet = safeLanding != null && safeLanding.RawWet,
                MovementSafeLandingRawHoneyWet = safeLanding != null && safeLanding.RawHoneyWet,
                MovementSafeLandingRawShimmering = safeLanding != null && safeLanding.RawShimmering,
                MovementSafeLandingRawWebbed = safeLanding != null && safeLanding.RawWebbed,
                MovementSafeLandingRawStoned = safeLanding != null && safeLanding.RawStoned,
                MovementSafeLandingRawGrapCount = safeLanding == null ? 0 : safeLanding.RawGrapCount,
                MovementSafeLandingRawEquippedWingCount = safeLanding == null ? 0 : safeLanding.RawEquippedWingCount,
                MovementSafeLandingRawMountNoFallDamage = safeLanding != null && safeLanding.RawMountNoFallDamage,
                MovementSafeLandingRawExtraFall = safeLanding == null ? 0 : safeLanding.RawExtraFall,
                MovementSafeLandingFallingSpeed = safeLanding == null ? 0d : safeLanding.FallingSpeed,
                MovementSafeLandingVelocityY = safeLanding == null ? 0d : safeLanding.VelocityY,
                MovementSafeLandingGravityDirection = safeLanding == null ? 0d : safeLanding.GravityDirection,
                MovementSafeLandingImpactFound = safeLanding != null && safeLanding.ImpactFound,
                MovementSafeLandingImpactDistancePixels = safeLanding == null ? -1 : safeLanding.ImpactDistancePixels,
                MovementSafeLandingImpactTicks = safeLanding == null ? -1d : safeLanding.ImpactTicks,
                MovementSafeLandingEstimatedFallTiles = safeLanding == null ? 0d : safeLanding.EstimatedFallTiles,
                MovementSafeLandingActiveCapabilitySummary = safeLanding == null ? string.Empty : safeLanding.ActiveCapabilitySummary,
                MovementSafeLandingSelectedStrategyId = safeLanding == null ? string.Empty : safeLanding.SelectedStrategyId,
                MovementSafeLandingSelectedPriority = safeLanding == null ? -1 : safeLanding.SelectedPriority,
                MovementSafeLandingSelectedActionType = safeLanding == null ? string.Empty : safeLanding.SelectedActionType,
                MovementSafeLandingHasFlyingCarpet = safeLanding != null && safeLanding.HasFlyingCarpet,
                MovementSafeLandingHasFlyingCarpetAvailable = safeLanding != null && safeLanding.HasFlyingCarpetAvailable,
                MovementSafeLandingFlyingCarpetTime = safeLanding == null ? 0 : safeLanding.FlyingCarpetTime,
                MovementSafeLandingHasGravityGlobe = safeLanding != null && safeLanding.HasGravityGlobe,
                MovementSafeLandingHasGravityFlipOpportunity = safeLanding != null && safeLanding.HasGravityFlipOpportunity,
                MovementSafeLandingHasEquippedFlyingMount = safeLanding != null && safeLanding.HasEquippedFlyingMount,
                MovementSafeLandingHasEquippedSafeMount = safeLanding != null && safeLanding.HasEquippedSafeMount,
                MovementSafeLandingHasEquippedGrapple = safeLanding != null && safeLanding.HasEquippedGrapple,
                MovementSafeLandingHasInventoryGrapple = safeLanding != null && safeLanding.HasInventoryGrapple,
                MovementSafeLandingHasTeleportRod = safeLanding != null && safeLanding.HasTeleportRod,
                MovementSafeLandingTeleportRodInventorySlot = safeLanding == null ? -1 : safeLanding.TeleportRodInventorySlot,
                MovementSafeLandingTeleportRodItemType = safeLanding == null ? 0 : safeLanding.TeleportRodItemType,
                MovementSafeLandingTeleportTargetKnown = safeLanding != null && safeLanding.TeleportTargetKnown,
                MovementSafeLandingTeleportTargetTileX = safeLanding == null ? -1 : safeLanding.TeleportTargetTileX,
                MovementSafeLandingTeleportTargetTileY = safeLanding == null ? -1 : safeLanding.TeleportTargetTileY,
                MovementSafeLandingTeleportTargetWorldX = safeLanding == null ? 0d : safeLanding.TeleportTargetWorldX,
                MovementSafeLandingTeleportTargetWorldY = safeLanding == null ? 0d : safeLanding.TeleportTargetWorldY,
                MovementSafeLandingHasCushionBlock = safeLanding != null && safeLanding.HasCushionBlock,
                MovementSafeLandingCushionBlockInventorySlot = safeLanding == null ? -1 : safeLanding.CushionBlockInventorySlot,
                MovementSafeLandingCushionBlockHotbarSlot = safeLanding == null ? -1 : safeLanding.CushionBlockHotbarSlot,
                MovementSafeLandingCushionBlockItemType = safeLanding == null ? 0 : safeLanding.CushionBlockItemType,
                MovementSafeLandingCushionBlockCreateTile = safeLanding == null ? -1 : safeLanding.CushionBlockCreateTile,
                MovementSafeLandingBlockPlacementTargetKnown = safeLanding != null && safeLanding.BlockPlacementTargetKnown,
                MovementSafeLandingBlockPlacementTileX = safeLanding == null ? -1 : safeLanding.BlockPlacementTileX,
                MovementSafeLandingBlockPlacementTileY = safeLanding == null ? -1 : safeLanding.BlockPlacementTileY,
                MovementSafeLandingBlockPlacementWorldX = safeLanding == null ? 0d : safeLanding.BlockPlacementWorldX,
                MovementSafeLandingBlockPlacementWorldY = safeLanding == null ? 0d : safeLanding.BlockPlacementWorldY,
                MovementSafeLandingGravityRestorePending = safeLanding != null && safeLanding.GravityRestorePending,
                MovementSafeLandingGravityRestoreOriginalDirection = safeLanding == null ? 0d : safeLanding.GravityRestoreOriginalDirection,
                MovementSafeLandingGravityRestorePendingTicks = safeLanding == null ? 0 : safeLanding.GravityRestorePendingTicks,
                MovementSafeLandingGravityRestoreLastDecision = safeLanding == null ? string.Empty : safeLanding.GravityRestoreLastDecision,
                MovementSafeLandingGravityRestoreLastSkipReason = safeLanding == null ? string.Empty : safeLanding.GravityRestoreLastSkipReason,
                MovementSafeLandingConfigSummary = safeLanding == null ? string.Empty : safeLanding.ConfigSummary,
                MovementSafeLandingStageSummary = safeLanding == null ? string.Empty : safeLanding.StageSummary,
                MovementSafeLandingStrategyCatalogVersion = safeLanding == null ? string.Empty : safeLanding.StrategyCatalogVersion,
                MovementSafeLandingStrategyEvaluationSummary = safeLanding == null ? string.Empty : safeLanding.StrategyEvaluationSummary,
                MovementSafeLandingCandidateSummary = safeLanding == null ? string.Empty : safeLanding.CandidateSummary,
                MovementSafeLandingSelectedPlanSummary = safeLanding == null ? string.Empty : safeLanding.SelectedPlanSummary,
                MovementSafeLandingRejectedStrategiesSummary = safeLanding == null ? string.Empty : safeLanding.RejectedStrategiesSummary,
                MovementSafeLandingPostApplyVerificationSummary = safeLanding == null ? string.Empty : safeLanding.PostApplyVerificationSummary,
                MovementSafeLandingRecoveryStateSummary = safeLanding == null ? string.Empty : safeLanding.RecoveryStateSummary,
                MovementSafeLandingSubmittedCount = safeLanding == null ? 0 : safeLanding.SubmittedCount,
                MovementSafeLandingSkippedCount = safeLanding == null ? 0 : safeLanding.SkippedCount,
                MovementSafeLandingFullAnalysisCount = safeLanding == null ? 0 : safeLanding.FullAnalysisCount,
                MovementSafeLandingCheapPrecheckSkipCount = safeLanding == null ? 0 : safeLanding.CheapPrecheckSkipCount,
                MovementSafeLandingLandingProbeCount = safeLanding == null ? 0 : safeLanding.LandingProbeCount,
                MovementSafeLandingConfigSummaryCacheHitCount = safeLanding == null ? 0 : safeLanding.ConfigSummaryCacheHitCount,
                MovementSafeLandingConfigSummaryCacheMissCount = safeLanding == null ? 0 : safeLanding.ConfigSummaryCacheMissCount,
                MovementSafeLandingStageSummaryCacheHitCount = safeLanding == null ? 0 : safeLanding.StageSummaryCacheHitCount,
                MovementSafeLandingCheapSkipDiagnosticSuppressedCount = safeLanding == null ? 0 : safeLanding.CheapSkipDiagnosticSuppressedCount,
                MovementSafeLandingCheapSkipDiagnosticWrittenCount = safeLanding == null ? 0 : safeLanding.CheapSkipDiagnosticWrittenCount,
                MovementSafeLandingCheapSkipLastReason = safeLanding == null ? string.Empty : safeLanding.CheapSkipLastReason,
                MovementSafeLandingCheapSkipDiagnosticCadenceTicks = safeLanding == null ? 0 : safeLanding.CheapSkipDiagnosticCadenceTicks,
                MovementSafeLandingRecoverySummarySkippedCount = safeLanding == null ? 0 : safeLanding.RecoverySummarySkippedCount,
                MovementSafeLandingLastCompatError = safeLanding == null ? string.Empty : safeLanding.LastCompatError,
                MovementSafeLandingCollisionFastPathStatus = safeLanding == null ? string.Empty : safeLanding.CollisionFastPathStatus ?? string.Empty,
                MovementSafeLandingPlayerUpdateHookInstalled = safeLanding != null && safeLanding.PlayerUpdateHookInstalled,
                MovementSafeLandingPlayerUpdateHookMessage = safeLanding == null ? string.Empty : safeLanding.PlayerUpdateHookMessage,
                MovementSafeLandingQueuedJumpPulseActive = safeLanding != null && safeLanding.QueuedJumpPulseActive,
                MovementSafeLandingQueuedJumpPulseStatus = safeLanding == null ? string.Empty : safeLanding.QueuedJumpPulseStatus,
                MovementSafeLandingQueuedJumpPulseApplySite = safeLanding == null ? string.Empty : safeLanding.QueuedJumpPulseApplySite,
                MovementSafeLandingTemporaryEquipmentApplied = safeLanding != null && safeLanding.TemporaryEquipmentApplied,
                MovementSafeLandingTemporaryEquipmentPendingRestoreCount = safeLanding == null ? 0 : safeLanding.TemporaryEquipmentPendingRestoreCount,
                MovementSafeLandingTemporaryEquipmentPendingRestoreNoSpaceCount = safeLanding == null ? 0 : safeLanding.TemporaryEquipmentPendingRestoreNoSpaceCount,
                MovementSafeLandingTemporaryEquipmentLastDecision = safeLanding == null ? string.Empty : safeLanding.TemporaryEquipmentLastDecision,
                MovementSafeLandingTemporaryEquipmentLastSkipReason = safeLanding == null ? string.Empty : safeLanding.TemporaryEquipmentLastSkipReason,
                MovementSafeLandingTemporaryEquipmentSelectedCategory = safeLanding == null ? string.Empty : safeLanding.TemporaryEquipmentSelectedCategory,
                MovementSafeLandingTemporaryEquipmentSelectedSourceKind = safeLanding == null ? string.Empty : safeLanding.TemporaryEquipmentSelectedSourceKind,
                MovementSafeLandingTemporaryEquipmentSelectedSourceSlot = safeLanding == null ? -1 : safeLanding.TemporaryEquipmentSelectedSourceSlot,
                MovementSafeLandingTemporaryEquipmentSelectedTargetKind = safeLanding == null ? string.Empty : safeLanding.TemporaryEquipmentSelectedTargetKind,
                MovementSafeLandingTemporaryEquipmentSelectedTargetSlot = safeLanding == null ? -1 : safeLanding.TemporaryEquipmentSelectedTargetSlot,
                MovementSafeLandingTemporaryEquipmentSelectedItemType = safeLanding == null ? 0 : safeLanding.TemporaryEquipmentSelectedItemType,
                MovementSafeLandingTemporaryEquipmentSelectedMountType = safeLanding == null ? -1 : safeLanding.TemporaryEquipmentSelectedMountType,
                MovementSafeLandingLandingSurfaceKnown = safeLanding != null && safeLanding.LandingSurfaceKnown,
                MovementSafeLandingLandingContactWorldX = safeLanding == null ? 0f : safeLanding.LandingContactWorldX,
                MovementSafeLandingLandingContactWorldY = safeLanding == null ? 0f : safeLanding.LandingContactWorldY,
                MovementSafeLandingLandingContactTileX = safeLanding == null ? -1 : safeLanding.LandingContactTileX,
                MovementSafeLandingLandingContactTileY = safeLanding == null ? -1 : safeLanding.LandingContactTileY,
                MovementSafeLandingLandingSurfaceKind = safeLanding == null ? string.Empty : safeLanding.LandingSurfaceKind ?? string.Empty,
                MovementSafeLandingLandingSlopeType = safeLanding == null ? 0 : safeLanding.LandingSlopeType,
                MovementSafeLandingLandingSlopeDirection = safeLanding == null ? string.Empty : safeLanding.LandingSlopeDirection ?? string.Empty,
                MovementSafeLandingLandingContactSample = safeLanding == null ? string.Empty : safeLanding.LandingContactSample ?? string.Empty,
                MovementSafeLandingLandingMovingIntoSlope = safeLanding != null && safeLanding.LandingMovingIntoSlope,
                MovementSafeLandingLandingMovingWithSlope = safeLanding != null && safeLanding.LandingMovingWithSlope,
                MovementSafeLandingLandingSurfaceSummary = safeLanding == null ? string.Empty : safeLanding.LandingSurfaceSummary ?? string.Empty,
                MovementSafeLandingGrappleHookSpeed = safeLanding == null ? 0f : safeLanding.GrappleHookSpeed,
                MovementSafeLandingGrappleTargetSource = safeLanding == null ? string.Empty : safeLanding.GrappleTargetSource ?? string.Empty,
                MovementSafeLandingGrappleTargetFromLandingSurface = safeLanding != null && safeLanding.GrappleTargetFromLandingSurface,
                MovementSafeLandingGrappleTargetDistancePixels = safeLanding == null ? 0f : safeLanding.GrappleTargetDistancePixels,
                MovementSafeLandingGrappleHookVerticalSpeed = safeLanding == null ? 0f : safeLanding.GrappleHookVerticalSpeed,
                MovementSafeLandingGrappleRelativeDownSpeed = safeLanding == null ? 0f : safeLanding.GrappleRelativeDownSpeed,
                MovementSafeLandingGrappleRequiredLeadTicks = safeLanding == null ? 0f : safeLanding.GrappleRequiredLeadTicks,
                MovementSafeLandingGrappleRequiredLeadPixels = safeLanding == null ? 0 : safeLanding.GrappleRequiredLeadPixels,
                MovementSafeLandingGrappleEstimatedTicksToTarget = safeLanding == null ? 0f : safeLanding.GrappleEstimatedTicksToTarget,
                MovementSafeLandingGrappleTooEarly = safeLanding != null && safeLanding.GrappleTooEarly,
                MovementSafeLandingGrappleTooLate = safeLanding != null && safeLanding.GrappleTooLate,
                MovementSafeLandingGrappleTooSlowForDownwardSurface = safeLanding != null && safeLanding.GrappleTooSlowForDownwardSurface,
                MovementSafeLandingGrappleTimingSummary = safeLanding == null ? string.Empty : safeLanding.GrappleTimingSummary ?? string.Empty,
                MovementSafeLandingEquippedGrappleShootSpeed = safeLanding == null ? 0f : safeLanding.EquippedGrappleShootSpeed,
                MovementSafeLandingInventoryGrappleShootSpeed = safeLanding == null ? 0f : safeLanding.InventoryGrappleShootSpeed,
                MovementSafeLandingEquippedGrappleProjectileType = safeLanding == null ? 0 : safeLanding.EquippedGrappleProjectileType,
                MovementSafeLandingInventoryGrappleProjectileType = safeLanding == null ? 0 : safeLanding.InventoryGrappleProjectileType,
                MovementSafeLandingMaxFallSpeed = safeLanding == null ? 0f : safeLanding.MaxFallSpeed,
                CombatAutoFacingEnabled = autoFacing != null && autoFacing.Enabled,
                CombatAutoFacingLastDecision = autoFacing == null ? string.Empty : autoFacing.LastDecision,
                CombatAutoFacingLastSkipReason = autoFacing == null ? string.Empty : autoFacing.LastSkipReason,
                CombatAutoFacingLastDecisionUtc = autoFacing == null ? null : autoFacing.LastDecisionUtc,
                CombatAutoFacingLastTick = autoFacing == null ? 0 : autoFacing.LastTick,
                CombatAutoFacingSelectedSlot = autoFacing == null ? -1 : autoFacing.SelectedSlot,
                CombatAutoFacingItemType = autoFacing == null ? 0 : autoFacing.ItemType,
                CombatAutoFacingItemName = autoFacing == null ? string.Empty : autoFacing.ItemName,
                CombatAutoFacingCurrentDirection = autoFacing == null ? 0 : autoFacing.CurrentDirection,
                CombatAutoFacingDesiredDirection = autoFacing == null ? 0 : autoFacing.DesiredDirection,
                CombatAutoFacingTargetSource = autoFacing == null ? string.Empty : autoFacing.TargetSource,
                CombatAutoFacingTargetWhoAmI = autoFacing == null ? -1 : autoFacing.TargetWhoAmI,
                CombatAutoFacingTargetType = autoFacing == null ? 0 : autoFacing.TargetType,
                CombatAutoFacingTargetName = autoFacing == null ? string.Empty : autoFacing.TargetName,
                CombatAutoFacingSubmittedCount = autoFacing == null ? 0 : autoFacing.SubmittedCount,
                CombatAutoFacingSkippedCount = autoFacing == null ? 0 : autoFacing.SkippedCount,
                CombatPerfectRevolverLastDecision = perfectRevolver == null ? string.Empty : perfectRevolver.LastDecision,
                CombatPerfectRevolverLastSkipReason = perfectRevolver == null ? string.Empty : perfectRevolver.LastSkipReason,
                CombatPerfectRevolverLastDecisionUtc = perfectRevolver == null ? null : perfectRevolver.LastDecisionUtc,
                CombatFlailComboEnabled = flailCombo != null && flailCombo.Enabled,
                CombatFlailComboRightHeld = flailCombo != null && flailCombo.RightHeld,
                CombatFlailComboEligible = flailCombo != null && flailCombo.Eligible,
                CombatFlailComboLastDecision = flailCombo == null ? string.Empty : flailCombo.LastDecision,
                CombatFlailComboLastReason = flailCombo == null ? string.Empty : flailCombo.LastReason,
                CombatFlailComboLastDecisionUtc = flailCombo == null ? null : flailCombo.LastDecisionUtc,
                CombatFlailComboItemType = flailCombo == null ? 0 : flailCombo.ItemType,
                CombatFlailComboProjectileType = flailCombo == null ? 0 : flailCombo.ProjectileType,
                CombatFlailComboProjectileAi0 = flailCombo == null ? 0d : flailCombo.ProjectileAi0,
                CombatFlailComboHitDetected = flailCombo != null && flailCombo.HitDetected,
                CombatFlailComboCollisionDetected = flailCombo != null && flailCombo.CollisionDetected,
                CombatFlailComboVanillaRightClickBlocked = flailCombo != null && flailCombo.VanillaRightClickBlocked,
                CombatFlailComboUiBlocked = flailCombo != null && flailCombo.UiBlocked,
                CombatFlailComboScopedPress = flailCombo != null && flailCombo.ScopedPress,
                CombatFlailComboScopedRelease = flailCombo != null && flailCombo.ScopedRelease,
                CombatFlailComboRestoreOk = flailCombo == null || flailCombo.RestoreOk,
                CombatFlailComboAppliedCount = flailCombo == null ? 0 : flailCombo.AppliedCount,
                CombatFlailComboSkippedCount = flailCombo == null ? 0 : flailCombo.SkippedCount,
                CombatItemCheckAutoClickerLastDecision = itemCheckAutoClicker == null ? string.Empty : itemCheckAutoClicker.LastDecision,
                CombatItemCheckAutoClickerLastReason = itemCheckAutoClicker == null ? string.Empty : itemCheckAutoClicker.LastReason,
                CombatItemCheckAutoClickerLastDecisionUtc = itemCheckAutoClicker == null ? null : itemCheckAutoClicker.LastDecisionUtc,
                CombatItemCheckAutoClickerLastItemType = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.LastItemType,
                CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable = itemCheckAutoClicker != null && itemCheckAutoClicker.LastVanillaAutoReuseAllAvailable,
                CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons = itemCheckAutoClicker != null && itemCheckAutoClicker.LastVanillaAutoReuseAllWeapons,
                CombatItemCheckAutoClickerScopedPress = itemCheckAutoClicker != null && itemCheckAutoClicker.LastScopedPress,
                CombatItemCheckAutoClickerScopedRelease = itemCheckAutoClicker != null && itemCheckAutoClicker.LastScopedRelease,
                CombatItemCheckAutoClickerRestored = itemCheckAutoClicker != null && itemCheckAutoClicker.LastRestored,
                CombatItemCheckAutoClickerAppliedCount = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.AppliedCount,
                CombatItemCheckAutoClickerSkippedCount = itemCheckAutoClicker == null ? 0 : itemCheckAutoClicker.SkippedCount,
                CombatMagicStringClickerLastDecision = magicStringClicker == null ? string.Empty : magicStringClicker.LastDecision,
                CombatMagicStringClickerLastSkipReason = magicStringClicker == null ? string.Empty : magicStringClicker.LastSkipReason,
                CombatMagicStringClickerLastDecisionUtc = magicStringClicker == null ? null : magicStringClicker.LastDecisionUtc,
                AutoHealEnabled = autoRecovery.AutoHealEnabled,
                AutoManaEnabled = autoRecovery.AutoManaEnabled,
                AutoBuffEnabled = autoRecovery.AutoBuffEnabled,
                AutoNurseEnabled = autoRecovery.AutoNurseEnabled,
                AutoStationBuffEnabled = autoRecovery.AutoStationBuffEnabled,
                AutoHealMode = autoRecovery.AutoHealMode,
                AutoManaMode = autoRecovery.AutoManaMode,
                AutoHealThresholdPercent = autoRecovery.AutoHealThresholdPercent,
                AutoManaThresholdPercent = autoRecovery.AutoManaThresholdPercent,
                AutoHealCooldownTicks = autoRecovery.AutoHealCooldownTicks,
                AutoManaCooldownTicks = autoRecovery.AutoManaCooldownTicks,
                AutoBuffCooldownTicks = autoRecovery.AutoBuffCooldownTicks,
                LastAutoHealResult = autoRecovery.LastAutoHealResult,
                LastAutoManaResult = autoRecovery.LastAutoManaResult,
                LastAutoBuffResult = autoRecovery.LastAutoBuffResult,
                LastAutoNurseResult = autoRecovery.LastAutoNurseResult,
                LastAutoStationBuffResult = autoRecovery.LastAutoStationBuffResult,
                LastAutoHealTick = autoRecovery.LastAutoHealTick,
                LastAutoManaTick = autoRecovery.LastAutoManaTick,
                LastAutoBuffTick = autoRecovery.LastAutoBuffTick,
                LastAutoNurseTick = autoRecovery.LastAutoNurseTick,
                LastAutoStationBuffTick = autoRecovery.LastAutoStationBuffTick,
                AutoStationBuffCooldownFastSkipCount = autoRecovery.AutoStationBuffCooldownFastSkipCount,
                AutoStationBuffActiveBuffFastSkipCount = autoRecovery.AutoStationBuffActiveBuffFastSkipCount,
                AutoStationBuffScanCount = stationBuff == null ? 0 : stationBuff.ScanCount,
                AutoStationBuffScanCacheHitCount = stationBuff == null ? 0 : stationBuff.CacheHitCount,
                AutoStationBuffScanCacheMissCount = stationBuff == null ? 0 : stationBuff.CacheMissCount,
                AutoStationBuffTilesVisitedLast = stationBuff == null ? 0 : stationBuff.TilesVisitedLast,
                AutoStationBuffLastScanMs = stationBuff == null ? 0d : stationBuff.LastScanMs,
                AutoStationBuffTileFastPathStatus = stationBuff == null ? string.Empty : stationBuff.TileFastPathStatus ?? string.Empty,
                AutoStationBuffLastDecision = stationBuff == null ? string.Empty : stationBuff.LastDecision ?? string.Empty,
                LastAutoBuffCountBefore = autoRecovery.LastAutoBuffCountBefore,
                LastAutoBuffCountAfter = autoRecovery.LastAutoBuffCountAfter,
                QuickHealCapability = autoRecovery.QuickHealCapability,
                QuickManaCapability = autoRecovery.QuickManaCapability,
                QuickBuffCapability = autoRecovery.QuickBuffCapability,
                LastError = RuntimeDiagnostics.LastError
            };
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static string GetLastActionUserMessage(InputActionQueueSnapshot actionSnapshot)
        {
            var hotkeyMessage = DiagnosticActionHotkeyService.LastDiagnosticHotkeyMessage;
            var hotkeyUtc = DiagnosticActionHotkeyService.LastDiagnosticHotkeyUtc;
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (hotkeyUtc.HasValue &&
                hotkeyUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(hotkeyMessage))
            {
                return hotkeyMessage;
            }

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonLabel))
            {
                return FirstNonEmpty(
                    DiagnosticInteractionDiagnostics.LastButtonMessage,
                    "�ѵ����ť��" + DiagnosticInteractionDiagnostics.LastButtonLabel + "���ȴ����������");
            }

            return FirstNonEmpty(actionSnapshot == null ? string.Empty : actionSnapshot.LastActionMessage, hotkeyMessage);
        }

        private static string GetLastActionResultCode(InputActionQueueSnapshot actionSnapshot)
        {
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonResultCode))
            {
                return DiagnosticInteractionDiagnostics.LastButtonResultCode;
            }

            return actionSnapshot == null ? string.Empty : actionSnapshot.LastActionResultCode;
        }

        private static string GetLastActionKind(InputActionQueueSnapshot actionSnapshot)
        {
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonLabel))
            {
                return DiagnosticInteractionDiagnostics.LastButtonLabel;
            }

            return actionSnapshot == null ? string.Empty : actionSnapshot.LastActionKind;
        }

        private static void CacheFeatureCatalogStats()
        {
            var userCategoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var codeDomainCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var total = 0;
            var implemented = 0;
            var visible = 0;
            var hotkeyVisible = 0;

            if (FeatureRegistry != null)
            {
                var definitions = FeatureRegistry.GetAll();
                for (var index = 0; index < definitions.Count; index++)
                {
                    var definition = definitions[index];
                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.IsInternalPlatform || !definition.CodeDomain.IsPublicDomain())
                    {
                        continue;
                    }

                    total++;
                    if (definition.IsImplemented)
                    {
                        implemented++;
                    }

                    if (definition.VisibleInMainUi)
                    {
                        visible++;
                    }

                    if (definition.HotkeyListVisible)
                    {
                        hotkeyVisible++;
                    }

                    Increment(userCategoryCounts, definition.UserCategory.ToString());
                    Increment(codeDomainCounts, definition.CodeDomain.ToCanonicalName());
                }
            }

            _featureCatalogCount = total;
            _implementedFeatureCount = implemented;
            _visibleFeatureCount = visible;
            _hotkeyVisibleFeatureCount = hotkeyVisible;
            _userCategoryCounts = userCategoryCounts;
            _codeDomainCounts = codeDomainCounts;
        }

        private static void Increment(Dictionary<string, int> counts, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = "Unknown";
            }

            int value;
            counts.TryGetValue(key, out value);
            counts[key] = value + 1;
        }

        private static void QueueStartupDiagnosticNoopIfReady()
        {
            if (!State.LateBootstrapCompleted || ActionQueue == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_startupNoopQueued)
                {
                    return;
                }

                _startupNoopQueued = true;
            }

            // ACTION_QUEUE_DIRECT_ENQUEUE_EXCEPTION: one-shot startup health noop; owner=diagnostics; migrate_after=02 no-op unless startup diagnostics need admission testing.
            ActionQueue.Enqueue(InputActionRequest.CreateDiagnosticNoop(
                "diagnostics.health_check",
                "M5 queue startup check"));
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
