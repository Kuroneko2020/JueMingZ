using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
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
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void WorldGenDebugViewerAndDeveloperMenuAlwaysAvailable()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.DiagnosticsWorldGenDebugViewer, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected WorldGen Debug Viewer feature to be registered.");
            }

            if (!feature.DefaultEnabled)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer must default to enabled.");
            }

            if (!feature.VisibleInMainUi ||
                feature.IsInternalPlatform ||
                feature.HasConfig ||
                feature.CodeDomain != FeatureCodeDomain.Diagnostics ||
                feature.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer must be a visible informational Diagnostics-domain row on the Misc UI page.");
            }

            FeatureDefinition developerCommands;
            if (!registry.TryGet(FeatureIds.DiagnosticsDeveloperDebugCommands, out developerCommands) || developerCommands == null)
            {
                throw new InvalidOperationException("Expected developer debug commands feature to be registered separately.");
            }

            if (!developerCommands.DefaultEnabled)
            {
                throw new InvalidOperationException("Developer debug commands must default to available.");
            }

            if (!developerCommands.VisibleInMainUi ||
                developerCommands.HasConfig ||
                developerCommands.CodeDomain != FeatureCodeDomain.Diagnostics ||
                developerCommands.UserCategory != FeatureUserCategory.Misc)
            {
                throw new InvalidOperationException("Developer debug commands must stay a visible Diagnostics-domain open action on the Misc UI page.");
            }

            var defaults = AppSettings.CreateDefault();
            if (!defaults.DiagnosticsWorldGenDebugViewerEnabled || !defaults.DiagnosticsDeveloperDebugCommandsEnabled)
            {
                throw new InvalidOperationException("WorldGen Debug Viewer and developer debug commands must both be available by default.");
            }

            if (!LateBootstrap.ShouldInstallDebugUiLocalizationHooks(defaults))
            {
                throw new InvalidOperationException("Debug UI localization hooks must install for the default WorldGen viewer and developer menu entries.");
            }

            defaults.DiagnosticsWorldGenDebugViewerEnabled = false;
            defaults.DiagnosticsDeveloperDebugCommandsEnabled = false;
            if (!defaults.DiagnosticsWorldGenDebugViewerEnabled || !defaults.DiagnosticsDeveloperDebugCommandsEnabled)
            {
                throw new InvalidOperationException("Legacy switch setters must not disable the always-available debug entries.");
            }

            if (!LateBootstrap.ShouldInstallDebugUiLocalizationHooks(defaults))
            {
                throw new InvalidOperationException("Legacy switch setters must not disable debug UI localization hooks.");
            }
        }

        private static void DiagnosticSnapshotWritesWorldGenDebugState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                WorldGenDebugViewerConfiguredEnabled = true,
                DeveloperDebugCommandsConfiguredEnabled = true,
                WorldGenDebugViewerSessionConfiguredEnabled = true,
                DeveloperDebugCommandsSessionConfiguredEnabled = false,
                WorldGenDebugAttempted = true,
                WorldGenDebugFieldEnabled = true,
                WorldGenDebugStatus = "enabled",
                WorldGenDebugMessage = "enableDebugCommands set to true",
                WorldGenDebugFieldOwner = "Terraria.Testing.DebugOptions.enableDebugCommands",
                WorldGenDebugLastAttemptUtc = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"WorldGenDebugViewerConfiguredEnabled\": true");
            AssertContains(json, "\"DeveloperDebugCommandsConfiguredEnabled\": true");
            AssertContains(json, "\"WorldGenDebugViewerSessionConfiguredEnabled\": true");
            AssertContains(json, "\"DeveloperDebugCommandsSessionConfiguredEnabled\": false");
            AssertContains(json, "\"WorldGenDebugAttempted\": true");
            AssertContains(json, "\"WorldGenDebugFieldEnabled\": true");
            AssertContains(json, "\"WorldGenDebugStatus\": \"enabled\"");
            AssertContains(json, "\"WorldGenDebugMessage\": \"enableDebugCommands set to true\"");
            AssertContains(json, "\"WorldGenDebugFieldOwner\": \"Terraria.Testing.DebugOptions.enableDebugCommands\"");
            AssertContains(json, "\"WorldGenDebugLastAttemptUtc\": \"2026-05-25T00:00:00.0000000Z\"");
        }

        private static void DiagnosticSnapshotWritesActionQueueAdmissionState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                ActionQueueLastAdmissionStatus = "Denied",
                ActionQueueLastAdmissionDecision = "DeniedBridgeBusy",
                ActionQueueLastAdmissionReason = "bridgeBusy",
                ActionQueueLastAdmissionKind = "ItemUse",
                ActionQueueLastAdmissionSource = "test.source",
                ActionQueueLastAdmissionScenario = "Test.Scenario",
                ActionQueueLastAdmissionKey = "test-key",
                ActionQueueLastAdmissionRequiredChannels = "UseItem|BridgeItemUse",
                ActionQueueLastAdmissionBlockingChannels = "UseItem",
                ActionQueueLastAdmissionConflictChannels = "UseItem|InventorySlot",
                ActionQueueLastAdmissionPendingConflictSummary = "pending:UseItem",
                ActionQueueLastAdmissionRunningConflictSummary = "running:Chest",
                ActionQueueLastAdmissionBridgeBusySummary = "ItemUseBridge:request",
                ActionQueueLastAdmissionOwnerSummary = "owner:UseItem",
                ActionQueueLastAdmissionSupersededRequestId = "superseded-request",
                ActionQueueLastAdmissionCoalescedRequestId = "coalesced-request",
                ActionQueueSupersededPendingCount = 2,
                ActionQueueCoalescedPendingCount = 4,
                SchedulerLastSelectedRequest = "UseHotbarItem:inventory.quick_item_hotkeys:quick-hotkey",
                SchedulerLastSupersededRequest = "RawInput:automation.auto_harvest:automation.auto_harvest.harvest.sustained",
                SchedulerLastFairnessBucket = "P2:UserExplicitCommand",
                WorldAutomationLastWinner = "AutoHarvest",
                WorldAutomationFairnessDebt = "autoCapture=1; autoHarvest=0",
                WorldAutomationFairnessDecisionUtc = new DateTime(2026, 6, 6, 6, 7, 8, DateTimeKind.Utc),
                BackgroundRequestCoalescedCount = 4,
                ExpiredPendingDroppedCount = 5,
                ActionQueueCleanupLeaseCount = 1,
                ActionQueueCleanupLeaseChannels = "UseItem|HotbarSelection",
                ActionQueueLastCleanupOwner = "UseHotbarItem:inventory.quick_item_hotkeys:test-key",
                ActionQueueLastCleanupReason = "AttemptedButUnverified:restore failed",
                ActionQueueDirectEnqueueCount = 3,
                ActionQueueLastDirectEnqueueKind = "Chest",
                ActionQueueLastDirectEnqueueSource = "inventory.auto_stack",
                ActionQueueLastDirectEnqueueScenario = ScenarioNames.InventoryAutoStack,
                ActionQueueLastDirectEnqueueAdmissionKey = FeatureIds.InventoryAutoStack,
                ActionQueueLastDirectEnqueueRequiredChannels = "InventorySlot|ChestInteraction"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            // Diagnostic field names are external troubleshooting contracts; keep
            // every ActionQueue admission, cleanup, and direct-enqueue key serialized.
            AssertContains(json, "\"ActionQueueLastAdmissionKind\": \"ItemUse\"");
            AssertContains(json, "\"ActionQueueLastAdmissionDecision\": \"DeniedBridgeBusy\"");
            AssertContains(json, "\"ActionQueueLastAdmissionSource\": \"test.source\"");
            AssertContains(json, "\"ActionQueueLastAdmissionScenario\": \"Test.Scenario\"");
            AssertContains(json, "\"ActionQueueLastAdmissionKey\": \"test-key\"");
            AssertContains(json, "\"ActionQueueLastAdmissionRequiredChannels\": \"UseItem|BridgeItemUse\"");
            AssertContains(json, "\"ActionQueueLastAdmissionBlockingChannels\": \"UseItem\"");
            AssertContains(json, "\"ActionQueueLastAdmissionConflictChannels\": \"UseItem|InventorySlot\"");
            AssertContains(json, "\"ActionQueueLastAdmissionPendingConflictSummary\": \"pending:UseItem\"");
            AssertContains(json, "\"ActionQueueLastAdmissionRunningConflictSummary\": \"running:Chest\"");
            AssertContains(json, "\"ActionQueueLastAdmissionBridgeBusySummary\": \"ItemUseBridge:request\"");
            AssertContains(json, "\"ActionQueueLastAdmissionOwnerSummary\": \"owner:UseItem\"");
            AssertContains(json, "\"ActionQueueLastAdmissionSupersededRequestId\": \"superseded-request\"");
            AssertContains(json, "\"ActionQueueLastAdmissionCoalescedRequestId\": \"coalesced-request\"");
            AssertContains(json, "\"ActionQueueSupersededPendingCount\": 2");
            AssertContains(json, "\"ActionQueueCoalescedPendingCount\": 4");
            AssertContains(json, "\"SchedulerLastSelectedRequest\": \"UseHotbarItem:inventory.quick_item_hotkeys:quick-hotkey\"");
            AssertContains(json, "\"SchedulerLastSupersededRequest\": \"RawInput:automation.auto_harvest:automation.auto_harvest.harvest.sustained\"");
            AssertContains(json, "\"SchedulerLastFairnessBucket\": \"P2:UserExplicitCommand\"");
            AssertContains(json, "\"WorldAutomationLastWinner\": \"AutoHarvest\"");
            AssertContains(json, "\"WorldAutomationFairnessDebt\": \"autoCapture=1; autoHarvest=0\"");
            AssertContains(json, "\"WorldAutomationFairnessDecisionUtc\": \"2026-06-06T06:07:08.0000000Z\"");
            AssertContains(json, "\"BackgroundRequestCoalescedCount\": 4");
            AssertContains(json, "\"ExpiredPendingDroppedCount\": 5");
            AssertContains(json, "\"ActionQueueCleanupLeaseCount\": 1");
            AssertContains(json, "\"ActionQueueCleanupLeaseChannels\": \"UseItem|HotbarSelection\"");
            AssertContains(json, "\"ActionQueueLastCleanupOwner\": \"UseHotbarItem:inventory.quick_item_hotkeys:test-key\"");
            AssertContains(json, "\"ActionQueueLastCleanupReason\": \"AttemptedButUnverified:restore failed\"");
            AssertContains(json, "\"ActionQueueDirectEnqueueCount\": 3");
            AssertContains(json, "\"ActionQueueLastDirectEnqueueKind\": \"Chest\"");
            AssertContains(json, "\"ActionQueueLastDirectEnqueueSource\": \"inventory.auto_stack\"");
            AssertContains(json, "\"ActionQueueLastDirectEnqueueScenario\": \"Inventory.AutoStack\"");
            AssertContains(json, "\"ActionQueueLastDirectEnqueueAdmissionKey\": \"inventory.auto_stack\"");
            AssertContains(json, "\"ActionQueueLastDirectEnqueueRequiredChannels\": \"InventorySlot|ChestInteraction\"");
        }

        private static void DiagnosticSnapshotWritesItemCheckWriterState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                ItemCheckWriterOwner = "ItemUseBridge",
                ItemCheckWriterOwnerRequestId = "writer-request",
                ItemCheckWriterPhase = "press",
                ItemCheckWriterDecisionReason = "bridgePendingAtStart",
                ItemCheckWriterBlockedCandidates = "CombatPerfectRevolver:blockedByItemUseBridge",
                ItemCheckWriterDecisionUtc = new DateTime(2026, 6, 6, 5, 6, 7, DateTimeKind.Utc)
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"ItemCheckWriterOwner\": \"ItemUseBridge\"");
            AssertContains(json, "\"ItemCheckWriterOwnerRequestId\": \"writer-request\"");
            AssertContains(json, "\"ItemCheckWriterPhase\": \"press\"");
            AssertContains(json, "\"ItemCheckWriterDecisionReason\": \"bridgePendingAtStart\"");
            AssertContains(json, "\"ItemCheckWriterBlockedCandidates\": \"CombatPerfectRevolver:blockedByItemUseBridge\"");
            AssertContains(json, "\"ItemCheckWriterDecisionUtc\": \"2026-06-06T05:06:07.0000000Z\"");
        }

        private static void DiagnosticSnapshotWritesAutoStackState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoStackLastDecision = "waiting for inventory-open auto stack settle",
                AutoStackLastDecisionUtc = new DateTime(2026, 5, 26, 2, 3, 4, DateTimeKind.Utc),
                AutoStackLastInventorySignature = "3:12x2",
                AutoStackLastPendingItemIds = "12",
                AutoStackLastDetectedItemIds = "12,99",
                AutoStackPendingSinceTick = 120,
                AutoStackLastPendingChangeTick = 124,
                AutoStackLastPendingClearReason = "submitted auto stack request",
                AutoStackPendingTransactionState = "RetryPending",
                AutoStackPendingRetryCount = 1,
                AutoStackLastSubmitRequestId = "request-456",
                AutoStackLastResult = "AttemptedButUnverified:AttemptedButUnverified:quick stack invoked",
                AutoStackLastUnverifiedReason = "quick stack invoked",
                AutoStackInventoryTransactionSlots = "3,11",
                AutoStackInventoryTransactionBlockingReason = "quick stack invoked",
                AutoStackActionResultDeliveryMode = "RequestIdLookup"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            // AutoStack transaction fields are used to distinguish pending,
            // verified, and unverified QuickStack outcomes in user reports.
            AssertContains(json, "\"AutoStackLastDecision\": \"waiting for inventory-open auto stack settle\"");
            AssertContains(json, "\"AutoStackLastDecisionUtc\": \"2026-05-26T02:03:04.0000000Z\"");
            AssertContains(json, "\"AutoStackLastInventorySignature\": \"3:12x2\"");
            AssertContains(json, "\"AutoStackLastPendingItemIds\": \"12\"");
            AssertContains(json, "\"AutoStackLastDetectedItemIds\": \"12,99\"");
            AssertContains(json, "\"AutoStackPendingSinceTick\": 120");
            AssertContains(json, "\"AutoStackLastPendingChangeTick\": 124");
            AssertContains(json, "\"AutoStackLastPendingClearReason\": \"submitted auto stack request\"");
            AssertContains(json, "\"AutoStackPendingTransactionState\": \"RetryPending\"");
            AssertContains(json, "\"AutoStackPendingRetryCount\": 1");
            AssertContains(json, "\"AutoStackLastSubmitRequestId\": \"request-456\"");
            AssertContains(json, "\"AutoStackLastResult\": \"AttemptedButUnverified:AttemptedButUnverified:quick stack invoked\"");
            AssertContains(json, "\"AutoStackLastUnverifiedReason\": \"quick stack invoked\"");
            AssertContains(json, "\"AutoStackInventoryTransactionSlots\": \"3,11\"");
            AssertContains(json, "\"AutoStackInventoryTransactionBlockingReason\": \"quick stack invoked\"");
            AssertContains(json, "\"AutoStackActionResultDeliveryMode\": \"RequestIdLookup\"");
        }

        private static void DiagnosticSnapshotWritesAutoDepositCoinsState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoDepositCoinsLastDecision = "submitted auto deposit coins request",
                AutoDepositCoinsLastDecisionUtc = new DateTime(2026, 5, 26, 3, 4, 5, DateTimeKind.Utc),
                AutoDepositCoinsLastInventorySignature = "0:71x88|3:72x32|15:74x2",
                AutoDepositCoinsLastCoinItemIds = "71,72,74"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoDepositCoinsLastDecision\": \"submitted auto deposit coins request\"");
            AssertContains(json, "\"AutoDepositCoinsLastDecisionUtc\": \"2026-05-26T03:04:05.0000000Z\"");
            AssertContains(json, "\"AutoDepositCoinsLastInventorySignature\": \"0:71x88|3:72x32|15:74x2\"");
            AssertContains(json, "\"AutoDepositCoinsLastCoinItemIds\": \"71,72,74\"");
        }

        private static void DiagnosticSnapshotWritesAutoTaxCollectState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoTaxCollectLastDecision = "submitted auto tax collect request",
                AutoTaxCollectLastDecisionUtc = new DateTime(2026, 6, 2, 1, 2, 3, DateTimeKind.Utc),
                AutoTaxCollectTargetNpcIndex = 8,
                AutoTaxCollectTargetWhoAmI = 77,
                AutoTaxCollectTargetName = "Tax Collector",
                AutoTaxCollectTaxMoney = 12345,
                AutoTaxCollectLastRequestId = "request-123"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoTaxCollectLastDecision\": \"submitted auto tax collect request\"");
            AssertContains(json, "\"AutoTaxCollectLastDecisionUtc\": \"2026-06-02T01:02:03.0000000Z\"");
            AssertContains(json, "\"AutoTaxCollectTargetNpcIndex\": 8");
            AssertContains(json, "\"AutoTaxCollectTargetWhoAmI\": 77");
            AssertContains(json, "\"AutoTaxCollectTargetName\": \"Tax Collector\"");
            AssertContains(json, "\"AutoTaxCollectTaxMoney\": 12345");
            AssertContains(json, "\"AutoTaxCollectLastRequestId\": \"request-123\"");
        }

        private static void DiagnosticSnapshotWritesAutoCaptureCritterState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoCaptureCritterLastDecision = "submitted sustained capture request",
                AutoCaptureCritterLastDecisionUtc = new DateTime(2026, 5, 25, 2, 3, 4, DateTimeKind.Utc),
                AutoCaptureCritterBugNetSlot = 8,
                AutoCaptureCritterBugNetItemType = TerrariaBugNetCompat.BugNetItemType,
                AutoCaptureCritterTargetNpcIndex = 12,
                AutoCaptureCritterTargetNpcType = 616,
                AutoCaptureCritterFishingProtectionState = "waiting=false,recast=false,poleSlot=0,poleItemType=0,bobber=-1"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoCaptureCritterLastDecision\": \"submitted sustained capture request\"");
            AssertContains(json, "\"AutoCaptureCritterLastDecisionUtc\": \"2026-05-25T02:03:04.0000000Z\"");
            AssertContains(json, "\"AutoCaptureCritterBugNetSlot\": 8");
            AssertContains(json, "\"AutoCaptureCritterBugNetItemType\": 1991");
            AssertContains(json, "\"AutoCaptureCritterTargetNpcIndex\": 12");
            AssertContains(json, "\"AutoCaptureCritterTargetNpcType\": 616");
            AssertContains(json, "\"AutoCaptureCritterFishingProtectionState\": \"waiting=false,recast=false,poleSlot=0,poleItemType=0,bobber=-1\"");
        }

        private static void DiagnosticSnapshotWritesAutoHarvestState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                AutoHarvestLastDecision = "submitted replant request",
                AutoHarvestLastDecisionUtc = new DateTime(2026, 5, 26, 1, 2, 3, DateTimeKind.Utc),
                AutoHarvestLastAction = "Replant",
                AutoHarvestToolSlot = 5,
                AutoHarvestToolItemType = 5295,
                AutoHarvestTargetTileX = 120,
                AutoHarvestTargetTileY = 210,
                AutoHarvestTargetSeedItemType = 2357,
                AutoHarvestPendingReplantCount = 2
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"AutoHarvestLastDecision\": \"submitted replant request\"");
            AssertContains(json, "\"AutoHarvestLastDecisionUtc\": \"2026-05-26T01:02:03.0000000Z\"");
            AssertContains(json, "\"AutoHarvestLastAction\": \"Replant\"");
            AssertContains(json, "\"AutoHarvestToolSlot\": 5");
            AssertContains(json, "\"AutoHarvestToolItemType\": 5295");
            AssertContains(json, "\"AutoHarvestTargetTileX\": 120");
            AssertContains(json, "\"AutoHarvestTargetTileY\": 210");
            AssertContains(json, "\"AutoHarvestTargetSeedItemType\": 2357");
            AssertContains(json, "\"AutoHarvestPendingReplantCount\": 2");
        }

        private static void DiagnosticSnapshotWritesCombatItemCheckAutoClickerState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                CombatItemCheckAutoClickerLastDecision = "scopedPress",
                CombatItemCheckAutoClickerLastReason = "ready",
                CombatItemCheckAutoClickerLastDecisionUtc = new DateTime(2026, 6, 5, 2, 3, 4, DateTimeKind.Utc),
                CombatItemCheckAutoClickerLastItemType = 29,
                CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable = true,
                CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons = false,
                CombatItemCheckAutoClickerScopedPress = true,
                CombatItemCheckAutoClickerScopedRelease = false,
                CombatItemCheckAutoClickerRestored = true,
                CombatItemCheckAutoClickerAppliedCount = 3,
                CombatItemCheckAutoClickerSkippedCount = 5
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"CombatItemCheckAutoClickerLastDecision\": \"scopedPress\"");
            AssertContains(json, "\"CombatItemCheckAutoClickerLastReason\": \"ready\"");
            AssertContains(json, "\"CombatItemCheckAutoClickerLastDecisionUtc\": \"2026-06-05T02:03:04.0000000Z\"");
            AssertContains(json, "\"CombatItemCheckAutoClickerLastItemType\": 29");
            AssertContains(json, "\"CombatItemCheckAutoClickerVanillaAutoReuseAllAvailable\": true");
            AssertContains(json, "\"CombatItemCheckAutoClickerVanillaAutoReuseAllWeapons\": false");
            AssertContains(json, "\"CombatItemCheckAutoClickerScopedPress\": true");
            AssertContains(json, "\"CombatItemCheckAutoClickerScopedRelease\": false");
            AssertContains(json, "\"CombatItemCheckAutoClickerRestored\": true");
            AssertContains(json, "\"CombatItemCheckAutoClickerAppliedCount\": 3");
            AssertContains(json, "\"CombatItemCheckAutoClickerSkippedCount\": 5");
        }

        private static void DiagnosticSnapshotWritesCombatFlailComboState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                CombatFlailComboEnabled = true,
                CombatFlailComboRightHeld = true,
                CombatFlailComboEligible = true,
                CombatFlailComboLastDecision = "scopedRelease",
                CombatFlailComboLastReason = "recallRelease",
                CombatFlailComboLastDecisionUtc = new DateTime(2026, 6, 5, 3, 4, 5, DateTimeKind.Utc),
                CombatFlailComboItemType = 5526,
                CombatFlailComboProjectileType = 1058,
                CombatFlailComboProjectileAi0 = 1d,
                CombatFlailComboHitDetected = true,
                CombatFlailComboCollisionDetected = false,
                CombatFlailComboVanillaRightClickBlocked = false,
                CombatFlailComboUiBlocked = false,
                CombatFlailComboScopedPress = false,
                CombatFlailComboScopedRelease = true,
                CombatFlailComboRestoreOk = true,
                CombatFlailComboAppliedCount = 4,
                CombatFlailComboSkippedCount = 7
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"CombatFlailComboEnabled\": true");
            AssertContains(json, "\"CombatFlailComboRightHeld\": true");
            AssertContains(json, "\"CombatFlailComboEligible\": true");
            AssertContains(json, "\"CombatFlailComboLastDecision\": \"scopedRelease\"");
            AssertContains(json, "\"CombatFlailComboLastReason\": \"recallRelease\"");
            AssertContains(json, "\"CombatFlailComboLastDecisionUtc\": \"2026-06-05T03:04:05.0000000Z\"");
            AssertContains(json, "\"CombatFlailComboItemType\": 5526");
            AssertContains(json, "\"CombatFlailComboProjectileType\": 1058");
            AssertContains(json, "\"CombatFlailComboProjectileAi0\": 1");
            AssertContains(json, "\"CombatFlailComboHitDetected\": true");
            AssertContains(json, "\"CombatFlailComboScopedRelease\": true");
            AssertContains(json, "\"CombatFlailComboRestoreOk\": true");
            AssertContains(json, "\"CombatFlailComboAppliedCount\": 4");
            AssertContains(json, "\"CombatFlailComboSkippedCount\": 7");
        }

        private static void DiagnosticSnapshotWritesCombatPhasebladeQuickSwitchState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                CombatPhasebladeQuickSwitchEnabled = true,
                CombatPhasebladeQuickSwitchRightHeld = true,
                CombatPhasebladeQuickSwitchEligible = true,
                CombatPhasebladeQuickSwitchLastDecision = "submitted",
                CombatPhasebladeQuickSwitchLastReason = "ready",
                CombatPhasebladeQuickSwitchLastDecisionUtc = new DateTime(2026, 6, 9, 4, 5, 6, DateTimeKind.Utc),
                CombatPhasebladeQuickSwitchCurrentSlot = 1,
                CombatPhasebladeQuickSwitchNextSlot = 4,
                CombatPhasebladeQuickSwitchEligibleSlotCount = 3,
                CombatPhasebladeQuickSwitchIntervalTicks = 12,
                CombatPhasebladeQuickSwitchScopedPress = true,
                CombatPhasebladeQuickSwitchScopedRelease = false,
                CombatPhasebladeQuickSwitchRestoreOk = true,
                CombatPhasebladeQuickSwitchAppliedCount = 8,
                CombatPhasebladeQuickSwitchSkippedCount = 2
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"CombatPhasebladeQuickSwitchEnabled\": true");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchRightHeld\": true");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchEligible\": true");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchLastDecision\": \"submitted\"");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchLastReason\": \"ready\"");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchLastDecisionUtc\": \"2026-06-09T04:05:06.0000000Z\"");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchCurrentSlot\": 1");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchNextSlot\": 4");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchEligibleSlotCount\": 3");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchIntervalTicks\": 12");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchScopedPress\": true");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchScopedRelease\": false");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchRestoreOk\": true");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchAppliedCount\": 8");
            AssertContains(json, "\"CombatPhasebladeQuickSwitchSkippedCount\": 2");
        }

        private static void DiagnosticSnapshotWritesFishingIdlePipelineState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                FishingAutomationDispatchReason = "idleWatchdog",
                FishingAutomationDispatchCadenceTicks = 10,
                FishingAutomationIdleFastSkipCount = 21,
                FishingAutomationIdleWatchdogTickCount = 3,
                FishingObserverFreshActiveCount = 4,
                FishingObserverFreshInactiveSkipCount = 17,
                FishingFallbackScanIdleSkippedCount = 18,
                FishingFallbackScanHookStaleCount = 2,
                FishingTickSubpathLast = "idleFastSkip:freshInactiveNoLocalBobber",
                FishingResidualStateMask = 512
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"FishingAutomationDispatchReason\": \"idleWatchdog\"");
            AssertContains(json, "\"FishingAutomationDispatchCadenceTicks\": 10");
            AssertContains(json, "\"FishingAutomationIdleFastSkipCount\": 21");
            AssertContains(json, "\"FishingAutomationIdleWatchdogTickCount\": 3");
            AssertContains(json, "\"FishingObserverFreshActiveCount\": 4");
            AssertContains(json, "\"FishingObserverFreshInactiveSkipCount\": 17");
            AssertContains(json, "\"FishingFallbackScanIdleSkippedCount\": 18");
            AssertContains(json, "\"FishingFallbackScanHookStaleCount\": 2");
            AssertContains(json, "\"FishingTickSubpathLast\": \"idleFastSkip:freshInactiveNoLocalBobber\"");
            AssertContains(json, "\"FishingResidualStateMask\": 512");
        }

        private static void PerformanceHitchRecorderDetectsRuntimeGaps()
        {
            var normal = new PerformanceHitchSample
            {
                UpdateStartGapMs = PerformanceHitchRecorder.UpdateStartGapThresholdMs - 1d,
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs - 1d,
                GameStateReadMs = PerformanceHitchRecorder.GameStateReadThresholdMs - 1d,
                ActionQueueUpdateMs = PerformanceHitchRecorder.ActionQueueUpdateThresholdMs - 1d,
                InputActionUpdateMs = PerformanceHitchRecorder.InputActionUpdateThresholdMs - 1d,
                InformationLastDrawElapsedMs = PerformanceHitchRecorder.InformationDrawThresholdMs - 1d
            };

            if (PerformanceHitchRecorder.ShouldRecord(normal))
            {
                throw new InvalidOperationException("Normal runtime timings must not produce a hitch event.");
            }

            if (PerformanceHitchRecorder.ShouldRecordFast(
                normal.UpdateStartGapMs,
                normal.RuntimeUpdateMs,
                normal.GameStateReadMs,
                normal.ActionQueueUpdateMs,
                normal.InputActionUpdateMs,
                normal.InformationLastDrawElapsedMs))
            {
                throw new InvalidOperationException("Normal runtime timings must not pass the fast hitch check.");
            }

            var factoryCalls = 0;
            PerformanceHitchRecorder.RecordIfNeeded(
                normal.UpdateStartGapMs,
                normal.RuntimeUpdateMs,
                normal.GameStateReadMs,
                normal.ActionQueueUpdateMs,
                normal.InputActionUpdateMs,
                normal.InformationLastDrawElapsedMs,
                () =>
                {
                    factoryCalls++;
                    return normal;
                });
            if (factoryCalls != 0)
            {
                throw new InvalidOperationException("Performance hitch sample factory must stay lazy below thresholds.");
            }

            var gap = new PerformanceHitchSample
            {
                UpdateStartGapMs = PerformanceHitchRecorder.UpdateStartGapThresholdMs
            };

            if (!PerformanceHitchRecorder.ShouldRecord(gap))
            {
                throw new InvalidOperationException("A long interval between Runtime.Update starts must produce a hitch event.");
            }

            if (!PerformanceHitchRecorder.ShouldRecordFast(
                0d,
                PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                0d,
                0d,
                0d,
                0d))
            {
                throw new InvalidOperationException("Runtime update threshold must pass the fast hitch check.");
            }

            if (!PerformanceHitchRecorder.ShouldRecordFast(
                0d,
                0d,
                0d,
                0d,
                0d,
                PerformanceHitchRecorder.InformationDrawThresholdMs))
            {
                throw new InvalidOperationException("Information draw threshold must pass the fast hitch check.");
            }

            var reason = PerformanceHitchRecorder.BuildReason(new PerformanceHitchSample
            {
                RuntimeUpdateMs = PerformanceHitchRecorder.RuntimeUpdateThresholdMs,
                InformationLastDrawElapsedMs = PerformanceHitchRecorder.InformationDrawThresholdMs
            });

            AssertContains(reason, "runtimeUpdate");
            AssertContains(reason, "informationDraw");
        }

        private static void PerformanceOperationRecorderUsesScenarioThresholds()
        {
            if (PerformanceHitchRecorder.ShouldRecordOperationFast(
                PerformanceHitchRecorder.ActionQueueAdmissionThresholdMs - 0.001d,
                PerformanceHitchRecorder.ActionQueueAdmissionThresholdMs))
            {
                throw new InvalidOperationException("Below-threshold action queue admission must not produce an operation event.");
            }

            if (!PerformanceHitchRecorder.ShouldRecordOperationFast(
                PerformanceHitchRecorder.ItemCheckWriterResolveThresholdMs,
                PerformanceHitchRecorder.ItemCheckWriterResolveThresholdMs))
            {
                throw new InvalidOperationException("ItemCheck writer resolve threshold must produce an operation event.");
            }

            RuntimePerformanceDiagnostics.ResetForTesting();
            RuntimePerformanceDiagnostics.RecordOperation(
                new PerformanceOperationSample
                {
                    UtcNow = new DateTime(2026, 6, 6, 8, 9, 10, DateTimeKind.Utc),
                    Scenario = "Performance.InventoryTransaction.Verify",
                    ElapsedMs = 12.5d,
                    ThresholdMs = PerformanceHitchRecorder.InventoryTransactionVerifyThresholdMs,
                    Reason = "result:AttemptedButUnverified",
                    OwnerSummary = "nearby container unavailable"
                },
                "diagnostics/performance-events-test.jsonl");

            if (RuntimePerformanceDiagnostics.PerformanceOperationEventCount != 1 ||
                !string.Equals(RuntimePerformanceDiagnostics.LastPerformanceOperationScenario, "Performance.InventoryTransaction.Verify", StringComparison.Ordinal) ||
                Math.Abs(RuntimePerformanceDiagnostics.LastPerformanceOperationElapsedMs - 12.5d) > 0.001d ||
                !string.Equals(RuntimePerformanceDiagnostics.LastPerformanceOperationReason, "result:AttemptedButUnverified", StringComparison.Ordinal) ||
                !string.Equals(RuntimePerformanceDiagnostics.LastPerformanceOperationOwnerSummary, "nearby container unavailable", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime performance diagnostics must retain the latest slow operation summary.");
            }

            RuntimePerformanceDiagnostics.ResetForTesting();
        }

        private static void GameStateReadOptionsMapCoinAutomationToCoinsProfile()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoDepositCoinsEnabled = true;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);

            if ((options.InventoryProfile & InventoryReadProfile.CoinsOnly) != InventoryReadProfile.CoinsOnly)
            {
                throw new InvalidOperationException("Auto deposit coins must request the coins inventory profile.");
            }

            if ((options.InventoryProfile & InventoryReadProfile.RecoveryFields) == InventoryReadProfile.RecoveryFields)
            {
                throw new InvalidOperationException("Coins-only profile must not request recovery item fields.");
            }
        }

        private static void GameStateReadOptionsKeepAutoTaxCollectLightweight()
        {
            var settings = AppSettings.CreateDefault();
            settings.NpcAutoTaxCollectEnabled = true;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);

            if (options.InventoryProfile != InventoryReadProfile.None ||
                options.NpcProfile != NpcReadProfile.None ||
                options.TileProfile != TileReadProfile.None)
            {
                throw new InvalidOperationException("Auto tax collect must not upgrade GameState inventory, NPC, or tile profiles.");
            }
        }

        private static void GameStateReadOptionsMergeCaptureAndStackProfiles()
        {
            var settings = AppSettings.CreateDefault();
            settings.InventoryAutoStackEnabled = true;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);

            if ((options.InventoryProfile & InventoryReadProfile.StackCandidates) != InventoryReadProfile.StackCandidates)
            {
                throw new InvalidOperationException("Auto stack must request stack candidate inventory fields.");
            }

            if ((options.InventoryProfile & InventoryReadProfile.BugNetOnly) != InventoryReadProfile.BugNetOnly)
            {
                throw new InvalidOperationException("Auto capture critter must request bug net inventory fields.");
            }

            if ((options.NpcProfile & NpcReadProfile.CatchableCritters) != NpcReadProfile.CatchableCritters)
            {
                throw new InvalidOperationException("Auto capture critter must request catchable critter NPC data.");
            }
        }

        private static void GameStateReadOptionsKeepDiagnosticsFullProfile()
        {
            var settings = AppSettings.CreateDefault();
            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, true);

            if (options.InventoryProfile != InventoryReadProfile.Full ||
                options.NpcProfile != NpcReadProfile.Full ||
                options.TileProfile != TileReadProfile.Full ||
                !options.IncludeWorldSummary)
            {
                throw new InvalidOperationException("Diagnostic snapshots must still request a full GameState profile.");
            }
        }

        private static void DiagnosticSnapshotWritesGameStateReadProfiles()
        {
            var snapshot = new DiagnosticSnapshot
            {
                LastGameStateInventoryProfile = "CoinsOnly",
                LastGameStateNpcProfile = "CatchableCrittersOnly",
                LastGameStateTileProfile = "None"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"LastGameStateInventoryProfile\": \"CoinsOnly\"");
            AssertContains(json, "\"LastGameStateNpcProfile\": \"CatchableCrittersOnly\"");
            AssertContains(json, "\"LastGameStateTileProfile\": \"None\"");
        }

        private static void RuntimeSettingsSnapshotNormalizesHotPathFields()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoHealEnabled = true;
            settings.AutoHealMode = AutoRecoverySettings.HealModeSmart;
            settings.AutoManaEnabled = true;
            settings.AutoManaMode = AutoRecoverySettings.ManaModeManaFlower;
            settings.CursorAimRadius = 99;
            settings.PlayerAimRadius = -3;
            settings.CombatAimAssistRadius = 75;
            settings.AimRangeOrigin = "invalid";
            settings.AimTargetPriority = CombatAimModes.TargetPriorityNearest;
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Auto;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;
            settings.MiscAutoCaptureCritterBaitEnabled = false;
            settings.NpcAutoTaxCollectEnabled = true;
            settings.CombatPhasebladeQuickSwitchEnabled = true;
            settings.CombatPhasebladeQuickSwitchIntervalTicks = 99;

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);

            if (!snapshot.RecoveryAnyEnabled ||
                !string.Equals(snapshot.AutoHealMode, AutoRecoverySettings.HealModeSmart, StringComparison.Ordinal) ||
                !string.Equals(snapshot.AutoManaMode, AutoRecoverySettings.ManaModeManaFlower, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize recovery modes.");
            }

            if (snapshot.CursorAimRadius != 50 ||
                snapshot.PlayerAimRadius != 0 ||
                snapshot.CombatAimAssistRadius != 50 ||
                !snapshot.CombatAimAnyEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must clamp combat aim radii.");
            }

            if (!string.Equals(snapshot.AimRangeOrigin, CombatAimModes.RangeOriginCursor, StringComparison.Ordinal) ||
                !string.Equals(snapshot.AimTargetPriority, CombatAimModes.TargetPriorityNearest, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize combat aim modes.");
            }

            if (!snapshot.WorldAutomationAutoMiningEnabled ||
                !snapshot.WorldAutomationAutoCaptureCritterEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose normalized world automation enabled flags.");
            }

            if (snapshot.WorldAutomationAutoCaptureCritterBaitEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose auto capture category flags.");
            }

            if (!snapshot.NpcAutoTaxCollectEnabled || !snapshot.NpcAutomationAnyEnabled)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose auto tax collect as NPC automation.");
            }

            if (!snapshot.CombatPhasebladeQuickSwitchEnabled ||
                snapshot.CombatPhasebladeQuickSwitchIntervalTicks != CombatPhasebladeQuickSwitchSettings.MaxIntervalTicks)
            {
                throw new InvalidOperationException("Runtime settings snapshot must expose and clamp phaseblade quick switch settings.");
            }

            settings.CombatPhasebladeQuickSwitchIntervalTicks = 1;
            var lowInterval = RuntimeSettingsSnapshot.FromSettings(settings);
            if (lowInterval.CombatPhasebladeQuickSwitchIntervalTicks != CombatPhasebladeQuickSwitchSettings.MinIntervalTicks)
            {
                throw new InvalidOperationException("Runtime settings snapshot must clamp phaseblade quick switch interval to the lower bound.");
            }

            settings.CombatPhasebladeQuickSwitchIntervalTicks = 0;
            var defaultInterval = RuntimeSettingsSnapshot.FromSettings(settings);
            if (defaultInterval.CombatPhasebladeQuickSwitchIntervalTicks != CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks)
            {
                throw new InvalidOperationException("Runtime settings snapshot must normalize missing phaseblade quick switch interval to the default.");
            }
        }

        private static void RuntimeSettingsSnapshotBuildsGameStateProfile()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoHealEnabled = true;
            settings.AutoHealMode = AutoRecoverySettings.HealModeQuick;
            settings.InventoryAutoDepositCoinsEnabled = true;
            settings.InventoryAutoStackEnabled = true;
            settings.MiscAutoCaptureCritterEnabled = true;
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;
            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(snapshot, false);

            if ((options.InventoryProfile & InventoryReadProfile.RecoveryItems) != InventoryReadProfile.RecoveryItems ||
                (options.InventoryProfile & InventoryReadProfile.CoinsOnly) != InventoryReadProfile.CoinsOnly ||
                (options.InventoryProfile & InventoryReadProfile.StackCandidates) != InventoryReadProfile.StackCandidates ||
                (options.InventoryProfile & InventoryReadProfile.BugNetOnly) != InventoryReadProfile.BugNetOnly)
            {
                throw new InvalidOperationException("Runtime settings snapshot must drive merged inventory read profiles.");
            }

            if ((options.NpcProfile & NpcReadProfile.CatchableCritters) != NpcReadProfile.CatchableCritters)
            {
                throw new InvalidOperationException("Runtime settings snapshot must drive catchable critter NPC profile.");
            }
        }

        private static void RuntimeSettingsSnapshotSplitsFishingDispatchLayers()
        {
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.DenyList;

            var filterOnly = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!filterOnly.FishingFilterEnabled ||
                filterOnly.FishingAutomationNeedsTick ||
                filterOnly.FishingAnyEnabled ||
                filterOnly.FishingDisplayNeedsCatchResolver)
            {
                throw new InvalidOperationException("Filter configuration alone must not be treated as fishing automation or display resolver work.");
            }

            settings.InformationFishingFilteredCatchesEnabled = true;
            var display = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!display.FishingDisplayNeedsCatchResolver || display.FishingAutomationNeedsTick)
            {
                throw new InvalidOperationException("Information fishing display must be separate from fishing automation tick work.");
            }

            settings.FishingAutoFishEnabled = true;
            var automation = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!automation.FishingAutomationNeedsTick || !automation.FishingAnyEnabled)
            {
                throw new InvalidOperationException("Auto fishing must keep the automation tick enabled.");
            }
        }

        private static void RuntimeFishingDispatchSkipsFilterOnlySettings()
        {
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;

            var filterOnly = RuntimeSettingsSnapshot.FromSettings(settings);
            if (JueMingZRuntime.ShouldDispatchFishingAutomationForTesting(filterOnly, false))
            {
                throw new InvalidOperationException("Pure fishing filter settings must not dispatch the full fishing automation service.");
            }

            if (!JueMingZRuntime.ShouldDispatchFishingAutomationForTesting(filterOnly, true))
            {
                throw new InvalidOperationException("Fishing residual state must keep runtime dispatch alive.");
            }

            settings.InformationFishingCatchesEnabled = true;
            var displayOnly = RuntimeSettingsSnapshot.FromSettings(settings);
            if (JueMingZRuntime.ShouldDispatchFishingAutomationForTesting(displayOnly, false))
            {
                throw new InvalidOperationException("Information-only fishing catch display must stay outside fishing automation dispatch.");
            }

            settings.FishingAutoFishEnabled = true;
            var autoFishing = RuntimeSettingsSnapshot.FromSettings(settings);
            if (!JueMingZRuntime.ShouldDispatchFishingAutomationForTesting(autoFishing, false))
            {
                throw new InvalidOperationException("Auto fishing must dispatch fishing automation even when filter is enabled.");
            }
        }

        private static void FishingResidualStateKeepsRuntimeDispatchAlive()
        {
            var requestId = Guid.NewGuid();
            try
            {
                FishingLoadoutService.SetRestoreSessionForTesting(requestId, 1, 0);
                if (!FishingAutomationService.HasResidualState)
                {
                    throw new InvalidOperationException("Loadout restore state must be visible as fishing residual runtime work.");
                }
            }
            finally
            {
                FishingLoadoutService.ResetForTesting();
            }
        }

        private static void RuntimeFishingDispatchUsesIdleWatchdogCadence()
        {
            try
            {
                FishingAutomationDiagnostics.ResetForTesting();
                FishingBobberObserver.RemoveMissing(null);
                var settings = AppSettings.CreateDefault();
                settings.FishingAutoFishEnabled = true;
                var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);

                if (!JueMingZRuntime.ShouldDispatchFishingAutomationForTesting(snapshot, false, 50))
                {
                    throw new InvalidOperationException("Auto fishing idle watchdog must keep runtime dispatch enabled.");
                }

                var cadence = JueMingZRuntime.GetFishingAutomationDispatchCadenceForTesting(snapshot, false, 50);
                if (cadence != FishingAutomationService.IdleWatchdogCadenceTicks)
                {
                    throw new InvalidOperationException("Auto fishing idle dispatch must use watchdog cadence.");
                }

                var reason = JueMingZRuntime.GetFishingAutomationDispatchReasonForTesting(snapshot, false, 50);
                if (!string.Equals(reason, "idleWatchdog", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Auto fishing idle dispatch must expose idleWatchdog reason.");
                }
            }
            finally
            {
                FishingBobberObserver.RemoveMissing(null);
                FishingAutomationDiagnostics.ResetForTesting();
            }
        }

        private static void RuntimeFishingDispatchPromotesFreshActiveBobber()
        {
            try
            {
                Terraria.Main.GameUpdateCount = 200;
                FishingAutomationDiagnostics.ResetForTesting();
                FishingAutomationDiagnostics.MarkHookInstalled();
                FishingBobberObserver.RemoveMissing(null);
                FishingBobberObserver.Observe(new FishingBobberObservation
                {
                    Identity = 700,
                    WhoAmI = 3,
                    GameUpdateCount = 200,
                    Active = true,
                    Bobber = true,
                    InLiquid = true,
                    LiquidStateKnown = true
                });

                var settings = AppSettings.CreateDefault();
                settings.FishingAutoFishEnabled = true;
                var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
                var cadence = JueMingZRuntime.GetFishingAutomationDispatchCadenceForTesting(snapshot, false, 201);
                if (cadence != 1)
                {
                    throw new InvalidOperationException("Fresh active bobber must promote fishing dispatch to per-tick cadence.");
                }

                var reason = JueMingZRuntime.GetFishingAutomationDispatchReasonForTesting(snapshot, false, 201);
                if (!string.Equals(reason, "freshActiveBobber", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Fresh active bobber dispatch must expose freshActiveBobber reason.");
                }
            }
            finally
            {
                FishingBobberObserver.RemoveMissing(null);
                FishingAutomationDiagnostics.ResetForTesting();
                Terraria.Main.GameUpdateCount = 0;
            }
        }

        private static void FishingIdleFastPathSkipsBaitAndEquipmentDetails()
        {
            try
            {
                Terraria.Main.GameUpdateCount = 300;
                FishingAutomationService.ResetForTesting();
                FishingBobberObserver.RemoveMissing(null);
                FishingAutomationDiagnostics.MarkHookInstalled();
                FishingBobberObserver.MarkNoActiveObservation(300);
                TerrariaFishingCompat.ResetTruffleWormQueryCountForTesting();
                FishingAutomationService.RecordDispatchState("idleWatchdog", FishingAutomationService.IdleWatchdogCadenceTicks);

                var settings = AppSettings.CreateDefault();
                settings.FishingAutoFishEnabled = true;
                settings.FishingAutoEquipmentEnabled = true;
                settings.FishingAutoLoadoutEnabled = true;
                var settingsSnapshot = RuntimeSettingsSnapshot.FromSettings(settings);
                var gameState = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Player = new PlayerStateSnapshot
                    {
                        Exists = true,
                        Active = true
                    }
                };
                var runtimeState = new RuntimeState { UpdateCount = 300 };

                FishingAutomationService.Tick(null, gameState, runtimeState, settingsSnapshot);

                var diagnostics = FishingAutomationService.GetDiagnostics();
                if (diagnostics.FishingAutomationIdleFastSkipCount <= 0)
                {
                    throw new InvalidOperationException("Fresh inactive observer should use the fishing idle fast path.");
                }

                if (TerrariaFishingCompat.TruffleWormQueryCountForTesting != 0)
                {
                    throw new InvalidOperationException("Fishing idle fast path must return before bait/truffle worm queries.");
                }
            }
            finally
            {
                TerrariaFishingCompat.ResetTruffleWormQueryCountForTesting();
                FishingBobberObserver.RemoveMissing(null);
                FishingAutomationService.ResetForTesting();
                Terraria.Main.GameUpdateCount = 0;
            }
        }

        private static void RuntimeSettingsSnapshotProviderRebuildsAfterConfigMutation()
        {
            var settings = ConfigService.AppSettings;
            var originalCursorAimRadius = settings.CursorAimRadius;
            var originalPlayerAimRadius = settings.PlayerAimRadius;
            var originalPhasebladeQuickSwitchIntervalTicks = settings.CombatPhasebladeQuickSwitchIntervalTicks;

            try
            {
                RuntimeSettingsSnapshotProvider.ResetForTesting();
                settings.CursorAimRadius = 3;
                settings.PlayerAimRadius = 4;
                settings.CombatPhasebladeQuickSwitchIntervalTicks = CombatPhasebladeQuickSwitchSettings.DefaultIntervalTicks;
                var first = RuntimeSettingsSnapshotProvider.GetCurrent();

                settings.CursorAimRadius = 8;
                var second = RuntimeSettingsSnapshotProvider.GetCurrent();

                if (object.ReferenceEquals(first, second))
                {
                    throw new InvalidOperationException("Runtime settings snapshot provider must rebuild after config mutation.");
                }

                if (second.CursorAimRadius != 8 || second.PlayerAimRadius != 4)
                {
                    throw new InvalidOperationException("Runtime settings snapshot provider returned stale normalized values.");
                }

                settings.CombatPhasebladeQuickSwitchIntervalTicks = 20;
                var third = RuntimeSettingsSnapshotProvider.GetCurrent();
                if (object.ReferenceEquals(second, third) ||
                    third.CombatPhasebladeQuickSwitchIntervalTicks != 20)
                {
                    throw new InvalidOperationException("Runtime settings snapshot provider must rebuild after phaseblade quick switch interval mutation.");
                }
            }
            finally
            {
                settings.CursorAimRadius = originalCursorAimRadius;
                settings.PlayerAimRadius = originalPlayerAimRadius;
                settings.CombatPhasebladeQuickSwitchIntervalTicks = originalPhasebladeQuickSwitchIntervalTicks;
                RuntimeSettingsSnapshotProvider.ResetForTesting();
            }
        }

        private static void RuntimeSettingsSnapshotProviderSkipsDisabledListHashes()
        {
            var settings = ConfigService.AppSettings;
            var originalAutoSellEnabled = settings.InventoryAutoSellEnabled;
            var originalAutoDiscardEnabled = settings.InventoryAutoDiscardEnabled;
            var originalQuickReforgeEnabled = settings.NpcAutoReforgeEnabled;
            var originalAutoSellIds = settings.InventoryAutoSellItemIds;
            var originalAutoDiscardIds = settings.InventoryAutoDiscardItemIds;
            var originalQuickReforgePrefixes = settings.NpcAutoReforgePrefixes;

            try
            {
                RuntimeSettingsSnapshotProvider.ResetForTesting();
                settings.InventoryAutoSellEnabled = false;
                settings.InventoryAutoDiscardEnabled = false;
                settings.NpcAutoReforgeEnabled = false;
                settings.InventoryAutoSellItemIds = new List<int> { 1 };
                settings.InventoryAutoDiscardItemIds = new List<int> { 2 };
                settings.NpcAutoReforgePrefixes = new List<string> { "Demonic" };

                var first = RuntimeSettingsSnapshotProvider.GetCurrent();
                settings.InventoryAutoSellItemIds.Add(3);
                settings.InventoryAutoDiscardItemIds.Add(4);
                settings.NpcAutoReforgePrefixes.Add("Legendary");
                var second = RuntimeSettingsSnapshotProvider.GetCurrent();

                if (!object.ReferenceEquals(first, second))
                {
                    throw new InvalidOperationException("Disabled list-only mutations must not rebuild the hot-path runtime settings snapshot.");
                }

                settings.InventoryAutoSellEnabled = true;
                var third = RuntimeSettingsSnapshotProvider.GetCurrent();
                if (object.ReferenceEquals(second, third) || !third.InventoryAutoSellEnabled)
                {
                    throw new InvalidOperationException("Enabling a list-backed feature must rebuild the runtime settings snapshot.");
                }
            }
            finally
            {
                settings.InventoryAutoSellEnabled = originalAutoSellEnabled;
                settings.InventoryAutoDiscardEnabled = originalAutoDiscardEnabled;
                settings.NpcAutoReforgeEnabled = originalQuickReforgeEnabled;
                settings.InventoryAutoSellItemIds = originalAutoSellIds;
                settings.InventoryAutoDiscardItemIds = originalAutoDiscardIds;
                settings.NpcAutoReforgePrefixes = originalQuickReforgePrefixes;
                RuntimeSettingsSnapshotProvider.ResetForTesting();
            }
        }

        private static void RuntimeServiceSchedulerHonorsCadenceAndDisabledCleanup()
        {
            JueMingZRuntime.ResetServiceSchedulerForTesting();

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 0))
            {
                throw new InvalidOperationException("A newly enabled service must run immediately.");
            }

            if (JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 1))
            {
                throw new InvalidOperationException("Service cadence must skip ticks before the interval elapses.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 3))
            {
                throw new InvalidOperationException("Service cadence must run when the interval elapses.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", false, 3, 4))
            {
                throw new InvalidOperationException("A just-disabled service must get one cleanup tick.");
            }

            if (JueMingZRuntime.ShouldRunServiceForTesting("test-service", false, 3, 5))
            {
                throw new InvalidOperationException("A disabled service must not keep running after cleanup.");
            }

            if (!JueMingZRuntime.ShouldRunServiceForTesting("test-service", true, 3, 6))
            {
                throw new InvalidOperationException("A re-enabled service must run immediately.");
            }
        }

        private static void RuntimeAutomationDispatcherPreservesDispatchContract()
        {
            AssertDispatchContract(
                JueMingZRuntime.GetTargetingDispatchContractForTesting(),
                new[]
                {
                    "targeting.combat-release-hold|targeting.combat-release-hold|1",
                    "targeting.combat-auto-aim|targeting.combat-auto-aim|1",
                    "targeting.combat-flail-control|targeting.combat-flail-control|1",
                    "targeting.startup-diagnostic-noop|targeting.startup-diagnostic-noop|1",
                    "targeting.diagnostic-button-actions|targeting.diagnostic-button-actions|1",
                    "targeting.legacy-ui-actions|targeting.legacy-ui-actions|1",
                    "targeting.diagnostic-hotkeys|targeting.diagnostic-hotkeys|1"
                });

            AssertDispatchContract(
                JueMingZRuntime.GetAutomationDispatchContractForTesting(),
                new[]
                {
                    "travel-menu|dispatch.travel-menu|1",
                    "travel-menu-pause-automation|dispatch.travel-menu-pause-automation|0",
                    "auto-recovery|dispatch.auto-recovery|1",
                    "fishing-automation|dispatch.fishing-automation|0",
                    "quick-item-hotkeys|dispatch.quick-item-hotkeys|1",
                    "auto-capture-critter|dispatch.auto-capture-critter|4",
                    "auto-harvest|dispatch.auto-harvest|1",
                    "auto-mining|dispatch.auto-mining|1",
                    "auto-stack|dispatch.auto-stack|5",
                    "auto-sell|dispatch.auto-sell|15",
                    "auto-discard|dispatch.auto-discard|15",
                    "quick-bag-open|dispatch.quick-bag-open|1",
                    "auto-deposit-coins|dispatch.auto-deposit-coins|15",
                    "auto-extractinator|dispatch.auto-extractinator|3",
                    "keep-favorited|dispatch.keep-favorited|2",
                    "quick-reforge|dispatch.quick-reforge|1",
                    "auto-tax-collect|dispatch.auto-tax-collect|30",
                    "combat-perfect-revolver|dispatch.combat-perfect-revolver|1",
                    "combat-magic-string|dispatch.combat-magic-string|1",
                    "combat-auto-facing|dispatch.combat-auto-facing|1",
                    "combat-phaseblade-quick-switch|dispatch.combat-phaseblade-quick-switch|1",
                    "combat-equipment-warning|dispatch.combat-equipment-warning|1",
                    "first-world-load-prompt|dispatch.first-world-load-prompt|0",
                    "movement-safe-landing|dispatch.movement-safe-landing|1",
                    "movement-continuous-dash|dispatch.movement-continuous-dash|1",
                    "movement-simulated-jump|dispatch.movement-simulated-jump|1"
                });
        }

        private static void RuntimeAutomationDispatcherPreservesLaneContract()
        {
            AssertAutomationDispatchLaneContract(
                JueMingZRuntime.GetAutomationDispatchContractForTesting(),
                new[]
                {
                    "travel-menu|AlwaysMaintenance",
                    "travel-menu-pause-automation|AlwaysMaintenance",
                    "auto-recovery|ActionSubmitting",
                    "fishing-automation|ActionSubmitting",
                    "quick-item-hotkeys|ActionSubmitting",
                    "auto-capture-critter|ActionSubmitting",
                    "auto-harvest|ActionSubmitting",
                    "auto-mining|ActionSubmitting",
                    "auto-stack|ActionSubmitting",
                    "auto-sell|ActionSubmitting",
                    "auto-discard|ActionSubmitting",
                    "quick-bag-open|ActionSubmitting",
                    "auto-deposit-coins|ActionSubmitting",
                    "auto-extractinator|ActionSubmitting",
                    "keep-favorited|ActionSubmitting",
                    "quick-reforge|ActionSubmitting",
                    "auto-tax-collect|ActionSubmitting",
                    "combat-perfect-revolver|ActionSubmitting",
                    "combat-magic-string|ActionSubmitting",
                    "combat-auto-facing|ActionSubmitting",
                    "combat-phaseblade-quick-switch|ActionSubmitting",
                    "combat-equipment-warning|ReadOnlyDisplay",
                    "first-world-load-prompt|AlwaysMaintenance",
                    "movement-safe-landing|ActionSubmitting",
                    "movement-continuous-dash|ActionSubmitting",
                    "movement-simulated-jump|ActionSubmitting"
                });
        }

        private static void AssertDispatchContract(RuntimeDispatchStep[] actual, string[] expected)
        {
            if (actual.Length != expected.Length)
            {
                throw new InvalidOperationException(
                    "Runtime dispatch contract length changed. Expected " + expected.Length + ", got " + actual.Length + ".");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                var row = actual[index].ServiceName + "|" + actual[index].OperationTimingName + "|" + actual[index].CadenceTicks;
                if (!string.Equals(row, expected[index], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Runtime dispatch contract changed at index " + index + ". Expected " + expected[index] + ", got " + row + ".");
                }
            }
        }

        private static void AssertAutomationDispatchLaneContract(RuntimeDispatchStep[] actual, string[] expected)
        {
            if (actual.Length != expected.Length)
            {
                throw new InvalidOperationException(
                    "Runtime dispatch lane contract length changed. Expected " + expected.Length + ", got " + actual.Length + ".");
            }

            for (var index = 0; index < expected.Length; index++)
            {
                var row = actual[index].ServiceName + "|" + actual[index].Lane.ToString();
                if (!string.Equals(row, expected[index], StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Runtime dispatch lane contract changed at index " + index + ". Expected " + expected[index] + ", got " + row + ".");
                }
            }
        }

        private static void RuntimeInputFocusGuardUsesGameStateFocus()
        {
            var focused = new GameStateSnapshot
            {
                Ui = new UiStateSnapshot { GameInputAvailable = true }
            };
            if (!JueMingZRuntime.IsGameInputAvailableForTesting(focused))
            {
                throw new InvalidOperationException("Focused game state must allow input dispatch.");
            }

            var unfocused = new GameStateSnapshot
            {
                Ui = new UiStateSnapshot { GameInputAvailable = false }
            };
            if (JueMingZRuntime.IsGameInputAvailableForTesting(unfocused))
            {
                throw new InvalidOperationException("Unfocused game state must pause physical/user input dispatch.");
            }

            if (!JueMingZRuntime.ShouldDispatchAutomationForTesting(unfocused))
            {
                throw new InvalidOperationException("Unfocused game state must still allow background automation dispatch.");
            }
        }

        private static void RuntimeDispatchLaneGateKeepsMaintenanceAndBlocksActionSubmitters()
        {
            JueMingZRuntime.ResetServiceSchedulerForTesting();

            try
            {
                var unfocused = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot { GameInputAvailable = false }
                };

                if (!JueMingZRuntime.ShouldDispatchAutomationForTesting(unfocused))
                {
                    throw new InvalidOperationException("Unfocused snapshots must keep the maintenance lane alive.");
                }

                if (!JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("travel-menu", true, unfocused, 1))
                {
                    throw new InvalidOperationException("AlwaysMaintenance lane must still dispatch while input is unavailable.");
                }

                if (!JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("combat-equipment-warning", true, unfocused, 1))
                {
                    throw new InvalidOperationException("ReadOnlyDisplay lane must not depend on user input focus.");
                }

                if (JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("auto-recovery", true, unfocused, 1))
                {
                    throw new InvalidOperationException("ActionSubmitting lane must not dispatch when game input is unavailable.");
                }

                var chatBlocked = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot
                    {
                        GameInputAvailable = true,
                        ChatOpen = true,
                        HasBlockingUi = true
                    }
                };

                if (JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("auto-recovery", true, chatBlocked, 2))
                {
                    throw new InvalidOperationException("ActionSubmitting lane must not dispatch while chat owns input.");
                }

                var available = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot { GameInputAvailable = true }
                };

                if (!JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("auto-recovery", true, available, 3))
                {
                    throw new InvalidOperationException("ActionSubmitting lane must resume as soon as the base gate is available.");
                }

                var chestUi = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot
                    {
                        GameInputAvailable = true,
                        ChestOpen = true,
                        HasBlockingUi = true
                    }
                };

                if (!JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("auto-stack", true, chestUi, 4))
                {
                    throw new InvalidOperationException("Chest UI is a service-specific action context, not a global lane blocker.");
                }

                var mainMenu = new GameStateSnapshot
                {
                    IsInWorld = false,
                    IsInMainMenu = true,
                    Ui = new UiStateSnapshot
                    {
                        GameInputAvailable = true,
                        IsInMainMenu = true
                    }
                };

                if (JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("quick-item-hotkeys", true, mainMenu, 5))
                {
                    throw new InvalidOperationException("ActionSubmitting lane must not dispatch outside the world.");
                }

                if (JueMingZRuntime.ShouldRunAutomationDispatchStepForTesting("combat-equipment-warning", true, mainMenu, 5))
                {
                    throw new InvalidOperationException("ReadOnlyDisplay lane must not scan while outside the world.");
                }
            }
            finally
            {
                JueMingZRuntime.ResetServiceSchedulerForTesting();
            }
        }

        private static void RuntimeTargetingInputGateRecordsSkipWithoutSubmittingDiagnosticCommand()
        {
            RuntimeTargetingDiagnostics.ResetForTesting();
            DrainDiagnosticButtonCommandsForTesting();
            JueMingZRuntime.ResetServiceSchedulerForTesting();

            try
            {
                var queue = new InputActionQueue();
                var state = new RuntimeState();
                var blockedGameState = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot { GameInputAvailable = false }
                };
                QueueDiagnosticNoopButtonCommandForTesting();

                var blockedContext = new RuntimeTickContext(Stopwatch.GetTimestamp())
                {
                    GameState = blockedGameState,
                    SettingsSnapshot = RuntimeSettingsSnapshot.FromSettings(AppSettings.CreateDefault()),
                    UpdateTick = 10
                };

                RuntimeAutomationDispatcher.RunTargetingAndUiActions(blockedContext, state, queue, false);

                if (!RuntimeTargetingDiagnostics.DiagnosticInputSkipped ||
                    !string.Equals(RuntimeTargetingDiagnostics.DiagnosticInputGateStatus, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(RuntimeTargetingDiagnostics.DiagnosticInputSkipReason, "gameInputAvailable=false", StringComparison.Ordinal) ||
                    !RuntimeTargetingDiagnostics.DiagnosticInputSkipUtc.HasValue)
                {
                    throw new InvalidOperationException("Expected diagnostic input skip state when game input is unavailable.");
                }

                if (queue.GetFastState().PendingCount != 0)
                {
                    throw new InvalidOperationException("gameInputAvailable=false must not drain diagnostic commands or submit actions.");
                }

                var skippedSnapshot = RuntimeDiagnosticSnapshotBuilder.Build(new RuntimeDiagnosticSnapshotContext
                {
                    Version = "test-runtime-targeting-diagnostics"
                });
                if (!skippedSnapshot.DiagnosticInputSkipped ||
                    !string.Equals(skippedSnapshot.DiagnosticInputGateStatus, "skipped", StringComparison.Ordinal) ||
                    !string.Equals(skippedSnapshot.DiagnosticInputSkipReason, "gameInputAvailable=false", StringComparison.Ordinal) ||
                    !skippedSnapshot.DiagnosticInputSkipUtc.HasValue)
                {
                    throw new InvalidOperationException("Runtime snapshot must expose diagnostic input skip state.");
                }

                var json = InvokeDiagnosticSnapshotJson(skippedSnapshot);
                AssertContains(json, "\"DiagnosticInputSkipped\": true");
                AssertContains(json, "\"DiagnosticInputGateStatus\": \"skipped\"");
                AssertContains(json, "\"DiagnosticInputSkipReason\": \"gameInputAvailable=false\"");

                var availableGameState = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Ui = new UiStateSnapshot { GameInputAvailable = true }
                };
                var availableContext = new RuntimeTickContext(Stopwatch.GetTimestamp())
                {
                    GameState = availableGameState,
                    SettingsSnapshot = RuntimeSettingsSnapshot.FromSettings(AppSettings.CreateDefault()),
                    UpdateTick = 11
                };

                RuntimeAutomationDispatcher.RunTargetingAndUiActions(availableContext, state, queue, true);

                if (RuntimeTargetingDiagnostics.DiagnosticInputSkipped ||
                    !string.Equals(RuntimeTargetingDiagnostics.DiagnosticInputGateStatus, "available", StringComparison.Ordinal) ||
                    !string.IsNullOrEmpty(RuntimeTargetingDiagnostics.DiagnosticInputSkipReason) ||
                    RuntimeTargetingDiagnostics.DiagnosticInputSkipUtc.HasValue)
                {
                    throw new InvalidOperationException("Diagnostic input skip state must clear after input becomes available.");
                }

                if (queue.GetFastState().PendingCount <= 0)
                {
                    throw new InvalidOperationException("Available game input should allow the pending diagnostic noop command to submit.");
                }
            }
            finally
            {
                DrainDiagnosticButtonCommandsForTesting();
                RuntimeTargetingDiagnostics.ResetForTesting();
                JueMingZRuntime.ResetServiceSchedulerForTesting();
            }
        }

        private static void QueueDiagnosticNoopButtonCommandForTesting()
        {
            var button = new DiagnosticTestButton
            {
                Id = "noop",
                Label = "空动作",
                Hint = "test",
                X = 10,
                Y = 10,
                Width = 80,
                Height = 24,
                Enabled = true
            };
            var hit = new DiagnosticButtonHitTestResult
            {
                Button = button,
                HitTestMode = "Test",
                HitTestX = 12,
                HitTestY = 14,
                CandidateSummary = "T=noop",
                VisualRectX = button.X,
                VisualRectY = button.Y,
                VisualRectWidth = button.Width,
                VisualRectHeight = button.Height,
                HitRectX = button.HitX,
                HitRectY = button.HitY,
                HitRectWidth = button.HitWidth,
                HitRectHeight = button.HitHeight
            };
            var mouse = new DiagnosticMouseState
            {
                TerrariaReadAvailable = true,
                TerrariaMouseX = 12,
                TerrariaMouseY = 14,
                TerrariaLeftDown = true,
                GameInputAvailable = true,
                ReadMode = "Test"
            };
            var method = typeof(DiagnosticUiInteractionBridge).GetMethod(
                "EnqueueCommand",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException("DiagnosticUiInteractionBridge.EnqueueCommand reflection hook missing.");
            }

            method.Invoke(null, new object[] { hit, mouse, "Test", true });
        }

        private static void DrainDiagnosticButtonCommandsForTesting()
        {
            DiagnosticButtonCommand command;
            while (DiagnosticUiInteractionBridge.TryDrainButtonCommand(out command))
            {
            }
        }

        private static void DiagnosticSnapshotWritesPerformanceHitchState()
        {
            var snapshot = new DiagnosticSnapshot
            {
                LastUpdateStartGapMs = 87.5d,
                LastInformationDrawMs = 4.25d,
                RecentPerformanceWindowCapacitySamples = 600,
                RecentPerformanceWindowSampleCount = 420,
                RecentRuntimeUpdateAverageMs = 2.5d,
                RecentGameStateReadAverageMs = 1.125d,
                RecentActionQueueUpdateAverageMs = 0.75d,
                RecentInputActionUpdateAverageMs = 0.25d,
                RecentInformationDrawAverageMs = 3.5d,
                UiTextFastPathHitCount = 101,
                UiTextFallbackCount = 17,
                InformationStatusPanelLayoutCacheHitCount = 11,
                InformationStatusPanelLayoutCacheMissCount = 3,
                InformationSignTextLayoutCacheHitCount = 19,
                InformationSignTextLayoutCacheMissCount = 5,
                InformationWorldLabelSnapshotRefreshCount = 7,
                InformationNpcLabelSnapshotRefreshCount = 4,
                InformationChestLabelSnapshotRefreshCount = 3,
                InformationChestLabelSortRefreshCount = 2,
                InformationChestAlwaysScanCacheHitCount = 18,
                InformationChestAlwaysScanCacheMissCount = 3,
                InformationChestAlwaysLastDirtyReason = "screenChunkChanged",
                InformationChestAlwaysSafeRefreshCount = 1,
                InformationChestAlwaysTilesVisitedLast = 1776,
                InformationChestAlwaysTypedTileFastPathStatus = "typed=1776;fallback=0;failed=0",
                InformationChestAlwaysNameCacheHitCount = 5,
                InformationChestAlwaysNameCacheMissCount = 2,
                InformationChestAlwaysPartialScanFrameCount = 6,
                InformationChestAlwaysPartialScanPendingCount = 128,
                InformationChestAlwaysStableSnapshotId = 9,
                InformationWorldContextCacheHitCount = 23,
                InformationWorldContextCacheMissCount = 9,
                InformationWorldContextProfile = "status",
                InformationWorldContextFileDataRefreshCount = 2,
                InformationStatusLineCacheHitCount = 17,
                InformationStatusLineCacheMissCount = 4,
                InformationFishingCatchEarlyCacheHitCount = 14,
                InformationFishingCatchEarlyCacheMissCount = 5,
                InformationFishingWaterScanCount = 6,
                InformationFishingConditionsReadCount = 7,
                InformationFishingBobberObserverFreshInactiveSkipCount = 8,
                InformationFishingProjectileFallbackScanCount = 9,
                SearchChestLocatorOverlayEnabled = true,
                SearchChestLocatorOverlayQueryVersion = 12,
                SearchChestLocatorOverlaySnapshotStatus = "ok",
                SearchChestLocatorOverlayCandidateChestCount = 4,
                SearchChestLocatorOverlayScannedChestCount = 3,
                SearchChestLocatorOverlayHitCount = 2,
                SearchChestLocatorOverlayDrawnHitCount = 1,
                SearchChestLocatorOverlaySkipReason = "offscreen",
                SearchChestLocatorOverlayRecentElapsedBucket = "<1ms",
                SearchChestLocatorOverlaySnapshotAgeTicks = 9,
                SearchChestLocatorSectionRequestEnabled = true,
                SearchChestLocatorSectionRequestMultiplayerClient = true,
                SearchChestLocatorSectionRequestAttempted = true,
                SearchChestLocatorSectionRequestSent = true,
                SearchChestLocatorSectionRequestThrottled = false,
                SearchChestLocatorSectionRequestStatus = "sent",
                SearchChestLocatorSectionRequestFailureReason = "",
                SearchChestLocatorSectionRequestSectionKey = "world-record-a:1:1",
                SearchChestLocatorSectionRequestSectionX = 1,
                SearchChestLocatorSectionRequestSectionY = 1,
                SearchChestLocatorSectionRequestQueryVersion = 12,
                SearchChestLocatorSectionRequestTick = 100,
                SearchChestLocatorSectionRequestCooldownRemainingTicks = 0,
                LegacyUiLayoutCacheHitCount = 13,
                LegacyUiLayoutCacheMissCount = 6,
                LegacyUiLastFrameVisibleElementCount = 42,
                LegacyUiHoverReuseCount = 8,
                LegacyUiHoverTooltipCacheHitCount = 9,
                LegacyUiHoverTooltipCacheMissCount = 3,
                LegacyUiHoverDiagnosticSuppressedCount = 7,
                LegacyUiScrollSnapshotSkippedCount = 12,
                LegacyUiScrollEventCoalescedCount = 6,
                LegacyUiRetainedFrameCacheHitCount = 14,
                LegacyUiRetainedFrameCacheMissCount = 5,
                LegacyUiRetainedFrameFallbackCount = 2,
                LegacyUiRetainedFrameVisibleElementCount = 38,
                LegacyUiActionUpdateSkippedCount = 21,
                LegacyUiActionUpdateRanCount = 5,
                LegacyUiPendingCommandCountLast = 2,
                LegacyUiDispatchedCommandCountLast = 2,
                LegacyUiDispatchElapsedMsLast = 0.375d,
                LegacyUiCommandCoalescedCount = 1,
                LegacyUiDragFrameActionSkipCount = 4,
                MovementSafeLandingLandingProbeCount = 29,
                MovementSafeLandingConfigSummaryCacheHitCount = 31,
                MovementSafeLandingConfigSummaryCacheMissCount = 2,
                MovementSafeLandingStageSummaryCacheHitCount = 30,
                MovementSafeLandingCheapSkipDiagnosticSuppressedCount = 120,
                MovementSafeLandingCheapSkipDiagnosticWrittenCount = 4,
                MovementSafeLandingCheapSkipLastReason = "notFallingFastEnough:cheap",
                MovementSafeLandingCheapSkipDiagnosticCadenceTicks = 30,
                MovementSafeLandingRecoverySummarySkippedCount = 119,
                LastSlowestStageName = "game-state-read",
                LastSlowestStageElapsedMs = 12.25d,
                PerformanceEventsPath = "diagnostics/performance-events-20260525.jsonl",
                PerformanceHitchCount = 3,
                LastPerformanceHitchUtc = new DateTime(2026, 5, 25, 1, 2, 3, DateTimeKind.Utc),
                LastPerformanceHitchReason = "updateGap+gameStateRead",
                LastPerformanceHitchUpdateGapMs = 91.125d,
                LastPerformanceHitchRuntimeUpdateMs = 8.5d,
                LastPerformanceHitchGameStateReadMs = 11.75d,
                LastPerformanceHitchActionQueueUpdateMs = 1.25d,
                LastPerformanceHitchInputActionUpdateMs = 0.5d,
                LastPerformanceHitchInformationDrawMs = 3.25d,
                LastPerformanceHitchSlowestStageName = "game-state-read",
                LastPerformanceHitchSlowestStageMs = 11.75d,
                PerformanceOperationEventCount = 2,
                LastPerformanceOperationScenario = "Performance.ItemCheckWriter.Resolve",
                LastPerformanceOperationUtc = new DateTime(2026, 6, 6, 8, 9, 10, DateTimeKind.Utc),
                LastPerformanceOperationElapsedMs = 10.5d,
                LastPerformanceOperationThresholdMs = 10d,
                LastPerformanceOperationReason = "worldAutomationFairness:autoHarvest",
                LastPerformanceOperationOwnerSummary = "AutoHarvestSustainedUse"
            };

            var json = InvokeDiagnosticSnapshotJson(snapshot);

            AssertContains(json, "\"LastUpdateStartGapMs\": 87.5");
            AssertContains(json, "\"LastInformationDrawMs\": 4.25");
            AssertContains(json, "\"RecentPerformanceWindowCapacitySamples\": 600");
            AssertContains(json, "\"RecentPerformanceWindowSampleCount\": 420");
            AssertContains(json, "\"RecentRuntimeUpdateAverageMs\": 2.5");
            AssertContains(json, "\"RecentInformationDrawAverageMs\": 3.5");
            AssertContains(json, "\"UiTextFastPathHitCount\": 101");
            AssertContains(json, "\"UiTextFallbackCount\": 17");
            AssertContains(json, "\"InformationStatusPanelLayoutCacheHitCount\": 11");
            AssertContains(json, "\"InformationSignTextLayoutCacheMissCount\": 5");
            AssertContains(json, "\"InformationWorldLabelSnapshotRefreshCount\": 7");
            AssertContains(json, "\"InformationChestAlwaysScanCacheHitCount\": 18");
            AssertContains(json, "\"InformationChestAlwaysScanCacheMissCount\": 3");
            AssertContains(json, "\"InformationChestAlwaysLastDirtyReason\": \"screenChunkChanged\"");
            AssertContains(json, "\"InformationChestAlwaysSafeRefreshCount\": 1");
            AssertContains(json, "\"InformationChestAlwaysTilesVisitedLast\": 1776");
            AssertContains(json, "\"InformationChestAlwaysTypedTileFastPathStatus\": \"typed=1776;fallback=0;failed=0\"");
            AssertContains(json, "\"InformationChestAlwaysNameCacheHitCount\": 5");
            AssertContains(json, "\"InformationChestAlwaysNameCacheMissCount\": 2");
            AssertContains(json, "\"InformationChestAlwaysPartialScanFrameCount\": 6");
            AssertContains(json, "\"InformationChestAlwaysPartialScanPendingCount\": 128");
            AssertContains(json, "\"InformationChestAlwaysStableSnapshotId\": 9");
            AssertContains(json, "\"InformationWorldContextCacheHitCount\": 23");
            AssertContains(json, "\"InformationWorldContextProfile\": \"status\"");
            AssertContains(json, "\"InformationWorldContextFileDataRefreshCount\": 2");
            AssertContains(json, "\"InformationStatusLineCacheHitCount\": 17");
            AssertContains(json, "\"InformationStatusLineCacheMissCount\": 4");
            AssertContains(json, "\"InformationFishingCatchEarlyCacheHitCount\": 14");
            AssertContains(json, "\"InformationFishingCatchEarlyCacheMissCount\": 5");
            AssertContains(json, "\"InformationFishingWaterScanCount\": 6");
            AssertContains(json, "\"InformationFishingConditionsReadCount\": 7");
            AssertContains(json, "\"InformationFishingBobberObserverFreshInactiveSkipCount\": 8");
            AssertContains(json, "\"InformationFishingProjectileFallbackScanCount\": 9");
            AssertContains(json, "\"SearchChestLocatorOverlayEnabled\": true");
            AssertContains(json, "\"SearchChestLocatorOverlayQueryVersion\": 12");
            AssertContains(json, "\"SearchChestLocatorOverlaySnapshotStatus\": \"ok\"");
            AssertContains(json, "\"SearchChestLocatorOverlayCandidateChestCount\": 4");
            AssertContains(json, "\"SearchChestLocatorOverlayScannedChestCount\": 3");
            AssertContains(json, "\"SearchChestLocatorOverlayHitCount\": 2");
            AssertContains(json, "\"SearchChestLocatorOverlayDrawnHitCount\": 1");
            AssertContains(json, "\"SearchChestLocatorOverlaySkipReason\": \"offscreen\"");
            AssertContains(json, "\"SearchChestLocatorOverlayRecentElapsedBucket\": \"<1ms\"");
            AssertContains(json, "\"SearchChestLocatorOverlaySnapshotAgeTicks\": 9");
            AssertContains(json, "\"SearchChestLocatorSectionRequestEnabled\": true");
            AssertContains(json, "\"SearchChestLocatorSectionRequestMultiplayerClient\": true");
            AssertContains(json, "\"SearchChestLocatorSectionRequestAttempted\": true");
            AssertContains(json, "\"SearchChestLocatorSectionRequestSent\": true");
            AssertContains(json, "\"SearchChestLocatorSectionRequestThrottled\": false");
            AssertContains(json, "\"SearchChestLocatorSectionRequestStatus\": \"sent\"");
            AssertContains(json, "\"SearchChestLocatorSectionRequestSectionKey\": \"world-record-a:1:1\"");
            AssertContains(json, "\"SearchChestLocatorSectionRequestSectionX\": 1");
            AssertContains(json, "\"SearchChestLocatorSectionRequestSectionY\": 1");
            AssertContains(json, "\"SearchChestLocatorSectionRequestQueryVersion\": 12");
            AssertContains(json, "\"SearchChestLocatorSectionRequestTick\": 100");
            AssertContains(json, "\"SearchChestLocatorSectionRequestCooldownRemainingTicks\": 0");
            AssertContains(json, "\"LegacyUiLastFrameVisibleElementCount\": 42");
            AssertContains(json, "\"LegacyUiHoverReuseCount\": 8");
            AssertContains(json, "\"LegacyUiHoverTooltipCacheHitCount\": 9");
            AssertContains(json, "\"LegacyUiHoverTooltipCacheMissCount\": 3");
            AssertContains(json, "\"LegacyUiHoverDiagnosticSuppressedCount\": 7");
            AssertContains(json, "\"LegacyUiScrollSnapshotSkippedCount\": 12");
            AssertContains(json, "\"LegacyUiScrollEventCoalescedCount\": 6");
            AssertContains(json, "\"LegacyUiRetainedFrameCacheHitCount\": 14");
            AssertContains(json, "\"LegacyUiRetainedFrameCacheMissCount\": 5");
            AssertContains(json, "\"LegacyUiRetainedFrameFallbackCount\": 2");
            AssertContains(json, "\"LegacyUiRetainedFrameVisibleElementCount\": 38");
            AssertContains(json, "\"LegacyUiActionUpdateSkippedCount\": 21");
            AssertContains(json, "\"LegacyUiActionUpdateRanCount\": 5");
            AssertContains(json, "\"LegacyUiPendingCommandCountLast\": 2");
            AssertContains(json, "\"LegacyUiDispatchedCommandCountLast\": 2");
            AssertContains(json, "\"LegacyUiDispatchElapsedMsLast\": 0.375");
            AssertContains(json, "\"LegacyUiCommandCoalescedCount\": 1");
            AssertContains(json, "\"LegacyUiDragFrameActionSkipCount\": 4");
            AssertContains(json, "\"MovementSafeLandingLandingProbeCount\": 29");
            AssertContains(json, "\"MovementSafeLandingConfigSummaryCacheHitCount\": 31");
            AssertContains(json, "\"MovementSafeLandingConfigSummaryCacheMissCount\": 2");
            AssertContains(json, "\"MovementSafeLandingStageSummaryCacheHitCount\": 30");
            AssertContains(json, "\"MovementSafeLandingCheapSkipDiagnosticSuppressedCount\": 120");
            AssertContains(json, "\"MovementSafeLandingCheapSkipDiagnosticWrittenCount\": 4");
            AssertContains(json, "\"MovementSafeLandingCheapSkipLastReason\": \"notFallingFastEnough:cheap\"");
            AssertContains(json, "\"MovementSafeLandingCheapSkipDiagnosticCadenceTicks\": 30");
            AssertContains(json, "\"MovementSafeLandingRecoverySummarySkippedCount\": 119");
            AssertContains(json, "\"LastSlowestStageName\": \"game-state-read\"");
            AssertContains(json, "\"PerformanceEventsPath\": \"diagnostics/performance-events-20260525.jsonl\"");
            AssertContains(json, "\"PerformanceHitchCount\": 3");
            AssertContains(json, "\"LastPerformanceHitchUtc\": \"2026-05-25T01:02:03.0000000Z\"");
            AssertContains(json, "\"LastPerformanceHitchReason\": \"updateGap+gameStateRead\"");
            AssertContains(json, "\"LastPerformanceHitchUpdateGapMs\": 91.125");
            AssertContains(json, "\"LastPerformanceHitchSlowestStageName\": \"game-state-read\"");
            AssertContains(json, "\"PerformanceOperationEventCount\": 2");
            AssertContains(json, "\"LastPerformanceOperationScenario\": \"Performance.ItemCheckWriter.Resolve\"");
            AssertContains(json, "\"LastPerformanceOperationUtc\": \"2026-06-06T08:09:10.0000000Z\"");
            AssertContains(json, "\"LastPerformanceOperationElapsedMs\": 10.5");
            AssertContains(json, "\"LastPerformanceOperationThresholdMs\": 10");
            AssertContains(json, "\"LastPerformanceOperationReason\": \"worldAutomationFairness:autoHarvest\"");
            AssertContains(json, "\"LastPerformanceOperationOwnerSummary\": \"AutoHarvestSustainedUse\"");
        }


    }
}
