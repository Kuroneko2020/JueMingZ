using System;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.MapEnhancement;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;

namespace JueMingZ.Runtime
{
    internal static class RuntimeAutomationDispatcher
    {
        private static readonly RuntimeDispatchStep TargetingCombatReleaseHold =
            new RuntimeDispatchStep("targeting.combat-release-hold", "targeting.combat-release-hold", 1);
        private static readonly RuntimeDispatchStep TargetingCombatAutoAim =
            new RuntimeDispatchStep("targeting.combat-auto-aim", "targeting.combat-auto-aim", 1);
        private static readonly RuntimeDispatchStep TargetingCombatFlailControl =
            new RuntimeDispatchStep("targeting.combat-flail-control", "targeting.combat-flail-control", 1);
        private static readonly RuntimeDispatchStep TargetingStartupDiagnosticNoop =
            new RuntimeDispatchStep("targeting.startup-diagnostic-noop", "targeting.startup-diagnostic-noop", 1);
        private static readonly RuntimeDispatchStep TargetingDiagnosticButtonActions =
            new RuntimeDispatchStep("targeting.diagnostic-button-actions", "targeting.diagnostic-button-actions", 1);
        private static readonly RuntimeDispatchStep TargetingLegacyUiActions =
            new RuntimeDispatchStep("targeting.legacy-ui-actions", "targeting.legacy-ui-actions", 1);
        private static readonly RuntimeDispatchStep TargetingBlueprintActionHotkeys =
            new RuntimeDispatchStep("targeting.blueprint-action-hotkeys", "targeting.blueprint-action-hotkeys", 1);
        private static readonly RuntimeDispatchStep TargetingFeatureToggleHotkeys =
            new RuntimeDispatchStep("targeting.feature-toggle-hotkeys", "targeting.feature-toggle-hotkeys", 1);
        private static readonly RuntimeDispatchStep TargetingMapCustomMarkers =
            new RuntimeDispatchStep("targeting.map-custom-markers", "targeting.map-custom-markers", 1);
        private static readonly RuntimeDispatchStep TargetingDiagnosticHotkeys =
            new RuntimeDispatchStep("targeting.diagnostic-hotkeys", "targeting.diagnostic-hotkeys", 1);

        private static readonly RuntimeDispatchStep DispatchTravelMenu =
            new RuntimeDispatchStep(
                "travel-menu",
                "dispatch.travel-menu",
                1,
                RuntimeDispatchLane.AlwaysMaintenance);
        private static readonly RuntimeDispatchStep DispatchTravelMenuPauseAutomation =
            new RuntimeDispatchStep(
                "travel-menu-pause-automation",
                "dispatch.travel-menu-pause-automation",
                0,
                RuntimeDispatchLane.AlwaysMaintenance);
        private static readonly RuntimeDispatchStep DispatchAutoRecovery =
            new RuntimeDispatchStep("auto-recovery", "dispatch.auto-recovery", 1);
        private static readonly RuntimeDispatchStep DispatchFishingAutomation =
            new RuntimeDispatchStep("fishing-automation", "dispatch.fishing-automation", 0);
        private static readonly RuntimeDispatchStep DispatchQuickItemHotkeys =
            new RuntimeDispatchStep("quick-item-hotkeys", "dispatch.quick-item-hotkeys", 1);
        private static readonly RuntimeDispatchStep DispatchAutoCaptureCritter =
            new RuntimeDispatchStep("auto-capture-critter", "dispatch.auto-capture-critter", 4);
        private static readonly RuntimeDispatchStep DispatchAutoHarvest =
            new RuntimeDispatchStep("auto-harvest", "dispatch.auto-harvest", 1);
        private static readonly RuntimeDispatchStep DispatchAutoMining =
            new RuntimeDispatchStep("auto-mining", "dispatch.auto-mining", 1);
        private static readonly RuntimeDispatchStep DispatchBlueprintWorldInstanceLifecycle =
            new RuntimeDispatchStep(
                "blueprint-world-instance-lifecycle",
                "dispatch.blueprint-world-instance-lifecycle",
                10,
                RuntimeDispatchLane.ReadOnlyDisplay);
        private static readonly RuntimeDispatchStep DispatchBlueprintAutoPlacement =
            new RuntimeDispatchStep("blueprint-auto-placement", "dispatch.blueprint-auto-placement", 10);
        private static readonly RuntimeDispatchStep DispatchAutoStack =
            new RuntimeDispatchStep("auto-stack", "dispatch.auto-stack", 5);
        private static readonly RuntimeDispatchStep DispatchAutoSell =
            new RuntimeDispatchStep("auto-sell", "dispatch.auto-sell", 15);
        private static readonly RuntimeDispatchStep DispatchAutoDiscard =
            new RuntimeDispatchStep("auto-discard", "dispatch.auto-discard", 15);
        private static readonly RuntimeDispatchStep DispatchQuickBagOpen =
            new RuntimeDispatchStep("quick-bag-open", "dispatch.quick-bag-open", 1);
        private static readonly RuntimeDispatchStep DispatchAutoDepositCoins =
            new RuntimeDispatchStep("auto-deposit-coins", "dispatch.auto-deposit-coins", 15);
        private static readonly RuntimeDispatchStep DispatchAutoExtractinator =
            new RuntimeDispatchStep("auto-extractinator", "dispatch.auto-extractinator", 3);
        private static readonly RuntimeDispatchStep DispatchKeepFavorited =
            new RuntimeDispatchStep("keep-favorited", "dispatch.keep-favorited", 2);
        private static readonly RuntimeDispatchStep DispatchQuickReforge =
            new RuntimeDispatchStep("quick-reforge", "dispatch.quick-reforge", 1);
        private static readonly RuntimeDispatchStep DispatchAutoTaxCollect =
            new RuntimeDispatchStep("auto-tax-collect", "dispatch.auto-tax-collect", 30);
        private static readonly RuntimeDispatchStep DispatchCombatPerfectRevolver =
            new RuntimeDispatchStep("combat-perfect-revolver", "dispatch.combat-perfect-revolver", 1);
        private static readonly RuntimeDispatchStep DispatchCombatMagicString =
            new RuntimeDispatchStep("combat-magic-string", "dispatch.combat-magic-string", 1);
        private static readonly RuntimeDispatchStep DispatchCombatAutoFacing =
            new RuntimeDispatchStep("combat-auto-facing", "dispatch.combat-auto-facing", 1);
        private static readonly RuntimeDispatchStep DispatchCombatPhasebladeQuickSwitch =
            new RuntimeDispatchStep("combat-phaseblade-quick-switch", "dispatch.combat-phaseblade-quick-switch", 1);
        private static readonly RuntimeDispatchStep DispatchCombatEquipmentWarning =
            new RuntimeDispatchStep(
                "combat-equipment-warning",
                "dispatch.combat-equipment-warning",
                1,
                RuntimeDispatchLane.ReadOnlyDisplay);
        private static readonly RuntimeDispatchStep DispatchCombatAutoBossDamageReport =
            new RuntimeDispatchStep(
                "combat-auto-boss-damage-report",
                "dispatch.combat-auto-boss-damage-report",
                15,
                RuntimeDispatchLane.ReadOnlyDisplay);
        private static readonly RuntimeDispatchStep DispatchFirstWorldLoadPrompt =
            new RuntimeDispatchStep(
                "first-world-load-prompt",
                "dispatch.first-world-load-prompt",
                0,
                RuntimeDispatchLane.AlwaysMaintenance);
        private static readonly RuntimeDispatchStep DispatchMovementSafeLanding =
            new RuntimeDispatchStep("movement-safe-landing", "dispatch.movement-safe-landing", 1);
        private static readonly RuntimeDispatchStep DispatchMovementContinuousDash =
            new RuntimeDispatchStep("movement-continuous-dash", "dispatch.movement-continuous-dash", 1);
        private static readonly RuntimeDispatchStep DispatchMovementSimulatedJump =
            new RuntimeDispatchStep("movement-simulated-jump", "dispatch.movement-simulated-jump", 1);

        private static readonly RuntimeDispatchStep[] TargetingDispatchContract =
        {
            TargetingCombatReleaseHold,
            TargetingCombatAutoAim,
            TargetingCombatFlailControl,
            TargetingStartupDiagnosticNoop,
            TargetingDiagnosticButtonActions,
            TargetingLegacyUiActions,
            TargetingBlueprintActionHotkeys,
            TargetingFeatureToggleHotkeys,
            TargetingMapCustomMarkers,
            TargetingDiagnosticHotkeys
        };

        private static readonly RuntimeDispatchStep[] AutomationDispatchContract =
        {
            DispatchTravelMenu,
            DispatchTravelMenuPauseAutomation,
            DispatchAutoRecovery,
            DispatchFishingAutomation,
            DispatchQuickItemHotkeys,
            DispatchAutoCaptureCritter,
            DispatchAutoHarvest,
            DispatchAutoMining,
            DispatchBlueprintWorldInstanceLifecycle,
            DispatchBlueprintAutoPlacement,
            DispatchAutoStack,
            DispatchAutoSell,
            DispatchAutoDiscard,
            DispatchQuickBagOpen,
            DispatchAutoDepositCoins,
            DispatchAutoExtractinator,
            DispatchKeepFavorited,
            DispatchQuickReforge,
            DispatchAutoTaxCollect,
            DispatchCombatPerfectRevolver,
            DispatchCombatMagicString,
            DispatchCombatAutoFacing,
            DispatchCombatPhasebladeQuickSwitch,
            DispatchCombatEquipmentWarning,
            DispatchCombatAutoBossDamageReport,
            DispatchFirstWorldLoadPrompt,
            DispatchMovementSafeLanding,
            DispatchMovementContinuousDash,
            DispatchMovementSimulatedJump
        };

        public static void RunTargetingAndUiActions(
            RuntimeTickContext context,
            RuntimeState state,
            InputActionQueue actionQueue,
            bool gameInputAvailable)
        {
            if (context == null)
            {
                RuntimeTargetingDiagnostics.RecordDiagnosticInputSkipped("context=null");
                return;
            }

            if (!gameInputAvailable)
            {
                // Keep the input gate fail-closed: diagnostics record why the
                // user-facing input services were skipped. The handheld blueprint
                // bar is a visible UI overlay, so only its already-queued UI command
                // prefix may drain here; hotkeys and gameplay action submitters stay closed.
                RuntimeTargetingDiagnostics.RecordDiagnosticInputSkipped("gameInputAvailable=false");
                var skippedGateGameState = context.GameState;
                var skippedGateOperationStart = Stopwatch.GetTimestamp();
                LegacyUiActionService.UpdateBlueprintHandheldCommandsWhenInputUnavailable(actionQueue, skippedGateGameState);
                RecordOperationTiming(context, TargetingLegacyUiActions, skippedGateOperationStart);
                return;
            }

            RuntimeTargetingDiagnostics.RecordDiagnosticInputAvailable();
            var gameState = context.GameState;
            var settings = context.SettingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingCombatReleaseHold, settings.CombatAimAnyEnabled, context.UpdateTick))
            {
                CombatAimReleaseHoldService.Tick(gameState != null && gameState.IsInWorld, settings);
                RecordOperationTiming(context, TargetingCombatReleaseHold, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingCombatAutoAim, settings.CursorAimRadius > 0, context.UpdateTick))
            {
                CombatAutoAimService.Tick(gameState, state, settings);
                RecordOperationTiming(context, TargetingCombatAutoAim, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingCombatFlailControl, settings.CursorAimRadius > 0, context.UpdateTick))
            {
                CombatAimFlailControlService.Update();
                RecordOperationTiming(context, TargetingCombatFlailControl, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            RuntimeStartupDiagnosticNoop.QueueIfReady(state, actionQueue);
            RecordOperationTiming(context, TargetingStartupDiagnosticNoop, operationStart);
            operationStart = Stopwatch.GetTimestamp();
            DiagnosticButtonActionService.Update(actionQueue, gameState);
            RecordOperationTiming(context, TargetingDiagnosticButtonActions, operationStart);
            operationStart = Stopwatch.GetTimestamp();
            LegacyUiActionService.Update(actionQueue, gameState);
            RecordOperationTiming(context, TargetingLegacyUiActions, operationStart);

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingBlueprintActionHotkeys, BlueprintEntryHotkeyService.HasActiveBinding, context.UpdateTick))
            {
                BlueprintEntryHotkeyService.Tick(gameState);
                RecordOperationTiming(context, TargetingBlueprintActionHotkeys, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingFeatureToggleHotkeys, FeatureToggleHotkeyService.HasActiveBindings, context.UpdateTick))
            {
                FeatureToggleHotkeyService.Tick(gameState);
                RecordOperationTiming(context, TargetingFeatureToggleHotkeys, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(TargetingMapCustomMarkers, settings.MapCustomMarkersEnabled || MapCustomMarkerInteractionService.IsPickerOpen, context.UpdateTick))
            {
                MapCustomMarkerInteractionService.Tick(gameState, settings);
                RecordOperationTiming(context, TargetingMapCustomMarkers, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            DiagnosticActionHotkeyService.Update(actionQueue, gameState);
            RecordOperationTiming(context, TargetingDiagnosticHotkeys, operationStart);
        }

        public static void DispatchAutomationRequests(
            RuntimeTickContext context,
            RuntimeState state,
            InputActionQueue actionQueue)
        {
            if (context == null || !ShouldDispatchAutomation(context.GameState))
            {
                return;
            }

            var gameState = context.GameState;
            var settings = context.SettingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var tick = context.UpdateTick;
            var operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(
                DispatchTravelMenu,
                settings.WorldAutomationTravelMenuEnabled || TravelMenuService.RequiresRuntimeTickWhenDisabled(),
                gameState,
                tick))
            {
                TravelMenuService.Tick(gameState, state);
                RecordOperationTiming(context, DispatchTravelMenu, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();
            if (TravelMenuService.ShouldPauseAutomationForTravelMenu())
            {
                ClearActionQueueForTravelMenu(actionQueue);
                RecordOperationTiming(context, DispatchTravelMenuPauseAutomation, operationStart);
                return;
            }

            if (ShouldRun(DispatchAutoRecovery, settings.RecoveryAnyEnabled, gameState, tick))
            {
                AutoRecoveryService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoRecovery, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            var fishingHasResidualState = FishingAutomationService.HasResidualState;
            var fishingDispatch = GetFishingAutomationDispatchDecision(settings, fishingHasResidualState, tick);
            FishingAutomationService.RecordDispatchState(fishingDispatch.Reason, fishingDispatch.CadenceTicks);
            if (ShouldRun(
                DispatchFishingAutomation,
                fishingDispatch.Enabled,
                fishingDispatch.CadenceTicks,
                gameState,
                tick))
            {
                FishingAutomationService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchFishingAutomation, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchQuickItemHotkeys, settings.InventoryQuickItemHotkeysEnabled, gameState, tick))
            {
                QuickItemHotkeyService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchQuickItemHotkeys, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoCaptureCritter, settings.WorldAutomationAutoCaptureCritterEnabled, gameState, tick))
            {
                AutoCaptureCritterService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoCaptureCritter, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoHarvest, settings.WorldAutomationAutoHarvestEnabled, gameState, tick))
            {
                AutoHarvestService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoHarvest, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            RecordAutoMiningHotkeyRuntimeGate(settings, gameState);
            if (ShouldRun(DispatchAutoMining, settings.WorldAutomationAutoMiningEnabled, gameState, tick))
            {
                AutoMiningService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoMining, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchBlueprintWorldInstanceLifecycle, true, gameState, tick))
            {
                BlueprintWorldInstanceLifecycleService.Tick(gameState);
                RecordOperationTiming(context, DispatchBlueprintWorldInstanceLifecycle, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(
                DispatchBlueprintAutoPlacement,
                settings.BlueprintAutoPlacementEnabled ||
                    (actionQueue != null && actionQueue.IsSourcePendingOrRunning(FeatureIds.BlueprintMain)),
                gameState,
                tick))
            {
                BlueprintAutoPlacementService.Tick(actionQueue, gameState, settings);
                RecordOperationTiming(context, DispatchBlueprintAutoPlacement, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoStack, settings.InventoryAutoStackEnabled, gameState, tick))
            {
                AutoStackService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoStack, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoSell, settings.InventoryAutoSellEnabled, gameState, tick))
            {
                AutoSellService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoSell, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoDiscard, settings.InventoryAutoDiscardEnabled, gameState, tick))
            {
                AutoDiscardService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoDiscard, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchQuickBagOpen, settings.InventoryQuickBagOpenEnabled, gameState, tick))
            {
                QuickBagOpenService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchQuickBagOpen, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoDepositCoins, settings.InventoryAutoDepositCoinsEnabled, gameState, tick))
            {
                AutoDepositCoinsService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoDepositCoins, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchAutoExtractinator, settings.InventoryAutoExtractinatorEnabled, gameState, tick))
            {
                AutoExtractinatorService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoExtractinator, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchKeepFavorited, settings.InventoryKeepFavoritedEnabled, gameState, tick))
            {
                KeepFavoritedService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchKeepFavorited, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchQuickReforge, settings.NpcAutoReforgeEnabled, gameState, tick))
            {
                QuickReforgeService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchQuickReforge, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            operationStart = Stopwatch.GetTimestamp();
            if (ShouldRun(DispatchAutoTaxCollect, settings.NpcAutoTaxCollectEnabled, gameState, tick))
            {
                AutoTaxCollectorService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchAutoTaxCollect, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchCombatPerfectRevolver, settings.CombatPerfectRevolverEnabled, gameState, tick))
            {
                CombatPerfectRevolverService.Tick(actionQueue, gameState, state);
                RecordOperationTiming(context, DispatchCombatPerfectRevolver, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchCombatMagicString, settings.CombatMagicStringClickerEnabled, gameState, tick))
            {
                CombatMagicStringClickerService.Tick(actionQueue, gameState, state);
                RecordOperationTiming(context, DispatchCombatMagicString, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchCombatAutoFacing, settings.CombatAutoFacingEnabled, gameState, tick))
            {
                CombatAutoFacingService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchCombatAutoFacing, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(
                DispatchCombatPhasebladeQuickSwitch,
                settings.CombatPhasebladeQuickSwitchEnabled ||
                    PhasebladeQuickSwitchBridge.HasActiveUse ||
                    (actionQueue != null && actionQueue.IsSourcePendingOrRunning(FeatureIds.CombatPhasebladeQuickSwitch)),
                gameState,
                tick))
            {
                CombatPhasebladeQuickSwitchRuntimeService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchCombatPhasebladeQuickSwitch, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchCombatEquipmentWarning, settings.CombatEquipmentWarningEnabled, gameState, tick))
            {
                CombatEquipmentWarningService.Tick(gameState, state, settings);
                RecordOperationTiming(context, DispatchCombatEquipmentWarning, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchCombatAutoBossDamageReport, settings.CombatAutoBossDamageReportEnabled, gameState, tick))
            {
                CombatAutoBossDamageReportService.Tick(gameState, state, settings);
                RecordOperationTiming(context, DispatchCombatAutoBossDamageReport, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            FirstWorldLoadPromptService.Tick(gameState, state);
            RecordOperationTiming(context, DispatchFirstWorldLoadPrompt, operationStart);
            operationStart = Stopwatch.GetTimestamp();

            if (ShouldRun(
                DispatchMovementSafeLanding,
                settings.MovementSafeLandingEnabled || MovementSafeLandingService.RequiresRuntimeTickWhenDisabled(),
                gameState,
                tick))
            {
                MovementSafeLandingService.Tick(actionQueue, gameState, state);
                RecordOperationTiming(context, DispatchMovementSafeLanding, operationStart);
            }

            operationStart = Stopwatch.GetTimestamp();

            if (ShouldRun(DispatchMovementContinuousDash, settings.MovementContinuousDashEnabled, gameState, tick))
            {
                MovementContinuousDashService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchMovementContinuousDash, operationStart);
                operationStart = Stopwatch.GetTimestamp();
            }

            if (ShouldRun(DispatchMovementSimulatedJump, settings.MovementSimulatedMultiJumpEnabled, gameState, tick))
            {
                MovementSimulatedJumpService.Tick(actionQueue, gameState, state, settings);
                RecordOperationTiming(context, DispatchMovementSimulatedJump, operationStart);
            }
        }

        public static bool ShouldDispatchAutomation(GameStateSnapshot snapshot)
        {
            // The automation stage stays alive for maintenance even when user
            // input is unavailable; per-step lanes below gate action submitters.
            return ShouldDispatchLane(RuntimeDispatchLane.AlwaysMaintenance, snapshot) ||
                   ShouldDispatchLane(RuntimeDispatchLane.ReadOnlyDisplay, snapshot) ||
                   ShouldDispatchLane(RuntimeDispatchLane.ActionSubmitting, snapshot);
        }

        internal static bool ShouldDispatchFishingAutomation(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).Enabled;
        }

        internal static int GetFishingAutomationDispatchCadence(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).CadenceTicks;
        }

        internal static string GetFishingAutomationDispatchReason(RuntimeSettingsSnapshot settings, bool hasResidualState, long tick)
        {
            return GetFishingAutomationDispatchDecision(settings, hasResidualState, tick).Reason;
        }

        internal static RuntimeDispatchStep[] GetTargetingDispatchContractForTesting()
        {
            return CloneContract(TargetingDispatchContract);
        }

        internal static RuntimeDispatchStep[] GetAutomationDispatchContractForTesting()
        {
            return CloneContract(AutomationDispatchContract);
        }

        internal static bool ShouldRunAutomationDispatchStepForTesting(
            string serviceName,
            bool enabled,
            GameStateSnapshot gameState,
            long tick)
        {
            var step = FindAutomationDispatchStep(serviceName);
            return step != null && ShouldRun(step, enabled, gameState, tick);
        }

        private static bool ShouldRun(RuntimeDispatchStep step, bool enabled, long tick)
        {
            return RuntimeServiceScheduler.ShouldRun(step.ServiceName, enabled, step.CadenceTicks, tick);
        }

        private static bool ShouldRun(
            RuntimeDispatchStep step,
            bool enabled,
            GameStateSnapshot gameState,
            long tick)
        {
            return ShouldRun(step, enabled, step.CadenceTicks, gameState, tick);
        }

        private static bool ShouldRun(
            RuntimeDispatchStep step,
            bool enabled,
            int cadenceTicks,
            GameStateSnapshot gameState,
            long tick)
        {
            var laneAllowed = ShouldDispatchLane(step.Lane, gameState);
            var schedulerRun = RuntimeServiceScheduler.ShouldRun(
                step.ServiceName,
                enabled && laneAllowed,
                cadenceTicks,
                tick);

            return laneAllowed && schedulerRun;
        }

        private static bool ShouldDispatchLane(RuntimeDispatchLane lane, GameStateSnapshot gameState)
        {
            switch (lane)
            {
                case RuntimeDispatchLane.AlwaysMaintenance:
                    return true;
                case RuntimeDispatchLane.ReadOnlyDisplay:
                    return gameState != null && gameState.IsInWorld && !gameState.IsInMainMenu;
                case RuntimeDispatchLane.ActionSubmitting:
                    return CanSubmitAutomationAction(gameState);
                default:
                    return false;
            }
        }

        private static bool CanSubmitAutomationAction(GameStateSnapshot gameState)
        {
            if (gameState == null || !gameState.IsInWorld || gameState.IsInMainMenu)
            {
                return false;
            }

            var ui = gameState.Ui;
            if (ui == null || !ui.GameInputAvailable || ui.IsInMainMenu || ui.ChatOpen)
            {
                return false;
            }

            // Chest and NPC dialogs are service-specific contexts for inventory
            // and NPC actions, so they remain inside each service's business gate.
            return true;
        }

        private static void RecordAutoMiningHotkeyRuntimeGate(RuntimeSettingsSnapshot settings, GameStateSnapshot gameState)
        {
            if (settings == null ||
                !settings.WorldAutomationAutoMiningEnabled ||
                !string.Equals(settings.WorldAutomationAutoMiningMode, AutoMiningModes.Hotkey, StringComparison.Ordinal))
            {
                return;
            }

            var reason = GetAutomationActionGateBlockedReason(gameState);
            if (reason.Length > 0)
            {
                AutoMiningService.RecordRuntimeHotkeyGateSkipped(reason);
            }
        }

        private static string GetAutomationActionGateBlockedReason(GameStateSnapshot gameState)
        {
            if (gameState == null)
            {
                return "gameStateUnavailable";
            }

            if (!gameState.IsInWorld)
            {
                return "worldUnavailable";
            }

            if (gameState.IsInMainMenu)
            {
                return "mainMenu";
            }

            var ui = gameState.Ui;
            if (ui == null)
            {
                return "uiUnavailable";
            }

            if (ui.IsInMainMenu)
            {
                return "mainMenu";
            }

            if (ui.ChatOpen)
            {
                return "chatOpen";
            }

            return ui.GameInputAvailable ? string.Empty : "gameInputUnavailable";
        }

        private static void ClearActionQueueForTravelMenu(InputActionQueue actionQueue)
        {
            if (actionQueue == null)
            {
                return;
            }

            var snapshot = actionQueue.GetFastState();
            if (snapshot == null ||
                (snapshot.PendingCount <= 0 && string.IsNullOrWhiteSpace(snapshot.RunningActionKind)))
            {
                return;
            }

            actionQueue.Clear();
            LogThrottle.InfoThrottled(
                "travel-menu-automation-dispatch-paused",
                TimeSpan.FromSeconds(3),
                "Runtime",
                "Travel menu CreativeUI is open; cleared queued input actions and paused automation dispatch.");
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

        private static void RecordOperationTiming(RuntimeTickContext context, RuntimeDispatchStep step, long operationStart)
        {
            if (context == null)
            {
                return;
            }

            context.RecordOperationTiming(
                step.OperationTimingName,
                RuntimeTickContext.GetElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp()));
        }

        private static RuntimeDispatchStep[] CloneContract(RuntimeDispatchStep[] source)
        {
            var copy = new RuntimeDispatchStep[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static RuntimeDispatchStep FindAutomationDispatchStep(string serviceName)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return null;
            }

            for (var index = 0; index < AutomationDispatchContract.Length; index++)
            {
                var step = AutomationDispatchContract[index];
                if (step != null && string.Equals(step.ServiceName, serviceName, StringComparison.Ordinal))
                {
                    return step;
                }
            }

            return null;
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
    }
}
