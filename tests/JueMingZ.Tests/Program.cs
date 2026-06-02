using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace Terraria
{
    internal static class Main
    {
        public static bool mouseLeft;
        public static bool mouseLeftRelease;
        public static bool mouseRight;
        public static bool mouseRightRelease;
        public static bool mouseInterface;
        public static bool blockMouse;
        public static bool mouseText;
        public static string hoverItemName;
        public static string hoverItemName2;
        public static object HoverItem;
        public static object hoverItem;
        public static object[,] tile;
        public static bool[] tileSolid = new bool[1000];
        public static bool[] tileSolidTop = new bool[1000];
        public static int mouseX;
        public static int mouseY;
        public static int mouseScrollWheel;
        public static int oldMouseScrollWheel;
        public static int screenHeight = 800;
        public static float inventoryScale = 1f;
        public static long GameUpdateCount;
        public static TestVector2 screenPosition = new TestVector2();
        public static int myPlayer;
        public static object LocalPlayer;
        public static object[] player = new object[256];
    }

    internal sealed class TestVector2
    {
        public float X;
        public float Y;
    }
}

namespace Terraria.GameInput
{
    internal static class PlayerInput
    {
        public static TestTriggersPack Triggers = new TestTriggersPack();
    }

    internal sealed class TestTriggersPack
    {
        public TestTriggersSet Current = new TestTriggersSet();
        public TestTriggersSet JustPressed = new TestTriggersSet();
    }

    internal sealed class TestTriggersSet
    {
        public bool MouseLeft;
        public bool MouseRight;
    }
}

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private const int TestFishingRod = 50000;
        private const int TestLavaproofTackleBag = 5064;
        private const int TestAnglerTackleBag = 3721;
        private const int TestHighTestFishingLine = 2373;
        private const int TestAnglerEarring = 2374;
        private const int TestTackleBox = 2375;
        private const int TestLavaFishingHook = 4881;
        private const int TestFishingBobber = 5139;
        private const int TestFishingBobberRainbow = 5146;
        private const int TestLuckyCoin = 855;
        private const int TestCoinRing = 3034;
        private const int TestGreedyRing = 3035;
        private const int TestLuckyHorseshoe = 158;
        private const int TestBlueHorseshoeBalloon = 1250;
        private const int TestHorseshoeBundle = 5331;

        private static int Main()
        {
            var failed = 0;
            Run("dependency resolver loads compressed embedded Harmony", ref failed, DependencyResolverLoadsCompressedEmbeddedHarmony);
            Run("temporary double jump requires a real air jump flag", ref failed, TemporaryDoubleJumpRequiresAirJump);
            Run("temporary rocket boots require rocket ability after equip", ref failed, TemporaryRocketBootsRequireCapability);
            Run("temporary flying carpet requires carpet availability after equip", ref failed, TemporaryFlyingCarpetRequiresCapability);
            Run("temporary flying carpet accepts post-apply capability evidence", ref failed, TemporaryFlyingCarpetAcceptsPostApplyCapabilityEvidence);
            Run("temporary mounts require equipped mount opportunity after equip", ref failed, TemporaryMountsRequireCapability);
            Run("temporary gravity globe requires flip opportunity after equip", ref failed, TemporaryGravityGlobeRequiresCapability);
            Run("temporary gravity globe accepts post-apply capability evidence", ref failed, TemporaryGravityGlobeAcceptsPostApplyCapabilityEvidence);
            Run("equipped double jump waits for near ground distance", ref failed, EquippedDoubleJumpWaitsForNearGroundDistance);
            Run("temporary double jump apply keeps preactivation window", ref failed, TemporaryDoubleJumpApplyKeepsPreactivationWindow);
            Run("temporary double jump activation waits for near ground distance", ref failed, TemporaryDoubleJumpActivationWaitsForNearGroundDistance);
            Run("flying carpet activation waits for lower near ground distance", ref failed, FlyingCarpetActivationWaitsForLowerNearGroundDistance);
            Run("gravity globe activation starts before landing", ref failed, GravityGlobeActivationStartsBeforeLanding);
            Run("safe landing jump pulse can start from press when release is primed", ref failed, SafeLandingJumpPulseCanStartFromPressWhenReleasePrimed);
            Run("safe landing quick mount pulse starts from press with immediate cancel", ref failed, SafeLandingQuickMountPulseStartsFromPressWithImmediateCancel);
            Run("safe landing grapple pulse starts from press with target", ref failed, SafeLandingGrapplePulseStartsFromPressWithTarget);
            Run("temporary double jump apply refreshes functional effect", ref failed, TemporaryDoubleJumpApplyRefreshesFunctionalEffect);
            Run("temporary passive accessory apply refreshes functional effect", ref failed, TemporaryPassiveAccessoryApplyRefreshesFunctionalEffect);
            Run("temporary passive accessory apply waits for tighter near ground window", ref failed, TemporaryPassiveAccessoryApplyWaitsForTighterNearGroundWindow);
            Run("temporary umbrella plan targets selected hotbar slot", ref failed, TemporaryUmbrellaPlanTargetsSelectedHotbarSlot);
            Run("temporary umbrella does not trigger while already held", ref failed, TemporaryUmbrellaDoesNotTriggerWhileAlreadyHeld);
            Run("temporary umbrella apply waits for near ground distance", ref failed, TemporaryUmbrellaApplyWaitsForNearGroundDistance);
            Run("temporary umbrella apply swaps into selected hotbar slot", ref failed, TemporaryUmbrellaApplySwapsIntoSelectedHotbarSlot);
            Run("safe landing treats wing flight state as original safe without equipped wing", ref failed, SafeLandingTreatsWingFlightStateAsOriginalSafeWithoutEquippedWing);
            Run("safe landing treats current equipped wing as already safe", ref failed, SafeLandingTreatsCurrentEquippedWingAsAlreadySafe);
            Run("safe landing active grapple is not already safe while falling fast", ref failed, SafeLandingActiveGrappleIsNotAlreadySafeWhileFallingFast);
            Run("safe landing ignores wing in locked accessory slot", ref failed, SafeLandingIgnoresWingInLockedAccessorySlot);
            Run("safe landing cheap precheck skips slow fall", ref failed, SafeLandingCheapPrecheckSkipsSlowFall);
            Run("safe landing cheap precheck opens full analysis when fast", ref failed, SafeLandingCheapPrecheckOpensFullAnalysisWhenFast);
            Run("safe landing cheap precheck fails open when velocity unavailable", ref failed, SafeLandingCheapPrecheckFailsOpenWhenVelocityUnavailable);
            Run("safe landing cheap precheck fails open when player state unavailable", ref failed, SafeLandingCheapPrecheckFailsOpenWhenPlayerStateUnavailable);
            Run("movement input frame cache reuses profiles within frame", ref failed, MovementInputFrameCacheReusesProfilesWithinFrame);
            Run("safe landing cheap precheck uses cached motion within frame", ref failed, SafeLandingCheapPrecheckUsesCachedMotionWithinFrame);
            Run("safe landing projects horizontal impact probe", ref failed, SafeLandingProjectsHorizontalImpactProbe);
            Run("safe landing manual probe detects sloped platform", ref failed, SafeLandingManualProbeDetectsSlopedPlatform);
            Run("safe landing collision fast path records manual fallback", ref failed, SafeLandingCollisionFastPathRecordsManualFallback);
            Run("safe landing landing surface reports slope contact", ref failed, SafeLandingLandingSurfaceReportsSlopeContact);
            Run("safe landing landing surface reports moving into slope", ref failed, SafeLandingLandingSurfaceReportsMovingIntoSlope);
            Run("safe landing landing surface handles three tile foot coverage", ref failed, SafeLandingLandingSurfaceHandlesThreeTileFootCoverage);
            Run("safe landing landing surface projects horizontal motion", ref failed, SafeLandingLandingSurfaceProjectsHorizontalMotion);
            Run("safe landing landing surface prefers projected motion over current column", ref failed, SafeLandingLandingSurfacePrefersProjectedMotionOverCurrentColumn);
            Run("safe landing jump input profile reads grapple shoot speed", ref failed, SafeLandingJumpInputProfileReadsGrappleShootSpeed);
            Run("safe landing strategy catalog preserves priority order", ref failed, SafeLandingStrategyCatalogPreservesPriorityOrder);
            Run("priority 1 equipped double jump generates jump plan", ref failed, PriorityOneEquippedDoubleJumpGeneratesJumpPlan);
            Run("priority 1 equipped rocket boots generates jump plan", ref failed, PriorityOneEquippedRocketBootsGeneratesJumpPlan);
            Run("priority 1 equipped flying carpet generates jump plan", ref failed, PriorityOneEquippedFlyingCarpetGeneratesJumpPlan);
            Run("priority 1 equipped flying mount generates quick mount plan", ref failed, PriorityOneEquippedFlyingMountGeneratesQuickMountPlan);
            Run("priority 1 equipped gravity globe generates gravity flip plan", ref failed, PriorityOneEquippedGravityGlobeGeneratesGravityFlipPlan);
            Run("priority 1 gravity globe uses dedicated option", ref failed, PriorityOneGravityGlobeUsesDedicatedOption);
            Run("priority 1 gravity globe outranks mounts", ref failed, PriorityOneGravityGlobeOutranksMounts);
            Run("safe landing mount cancel clears when already unmounted", ref failed, SafeLandingMountCancelClearsWhenAlreadyUnmounted);
            Run("safe landing mount cancel imminent collision bypasses stable wait", ref failed, SafeLandingMountCancelImminentCollisionBypassesStableWait);
            Run("safe landing mount cancel waits when collision is still distant", ref failed, SafeLandingMountCancelWaitsWhenCollisionIsDistant);
            Run("priority 2 temporary horseshoe generates inventory apply plan", ref failed, PriorityTwoTemporaryHorseshoeGeneratesInventoryApplyPlan);
            Run("priority 2 temporary wings generates inventory apply plan", ref failed, PriorityTwoTemporaryWingsGeneratesInventoryApplyPlan);
            Run("priority 2 temporary fairy boots targets leg equipment slot", ref failed, PriorityTwoTemporaryFairyBootsTargetsLegEquipmentSlot);
            Run("priority 2 temporary double jump records refresh expectation", ref failed, PriorityTwoTemporaryDoubleJumpRecordsRefreshExpectation);
            Run("priority 2 temporary rocket boots detects terraspark fallback id", ref failed, PriorityTwoTemporaryRocketBootsDetectsTerrasparkFallbackId);
            Run("priority 2 temporary rocket boots records post apply verification", ref failed, PriorityTwoTemporaryRocketBootsRecordsPostApplyVerification);
            Run("temporary rocket boots pulse keeps active hold ticks", ref failed, TemporaryRocketBootsPulseKeepsActiveHoldTicks);
            Run("gravity flip pulse starts from press", ref failed, GravityFlipPulseStartsFromPress);
            Run("simulated jump pulse presses after release without final release", ref failed, SimulatedJumpPulsePressesAfterReleaseWithoutFinalRelease);
            Run("priority 2 temporary gravity globe records restore expectation", ref failed, PriorityTwoTemporaryGravityGlobeRecordsRestoreExpectation);
            Run("gravity globe default migration enables option", ref failed, GravityGlobeDefaultMigrationEnablesOption);
            Run("priority 3 umbrella request targets hotbar without activation pulse", ref failed, PriorityThreeUmbrellaRequestTargetsHotbarWithoutActivationPulse);
            Run("priority 4 inventory grapple generates grapple plan", ref failed, PriorityFourInventoryGrappleGeneratesGrapplePlan);
            Run("priority 4 grapple waits behind priority 1", ref failed, PriorityFourGrappleWaitsBehindPriorityOne);
            Run("priority 5 teleport rod generates use plan", ref failed, PriorityFiveTeleportRodGeneratesUsePlan);
            Run("priority 5 teleport rod uses direct inventory slot", ref failed, PriorityFiveTeleportRodUsesDirectInventorySlot);
            Run("priority 5 teleport rod waits behind grapple", ref failed, PriorityFiveTeleportRodWaitsBehindGrapple);
            Run("priority 4 grapple targets landing surface slope contact", ref failed, PriorityFourGrappleTargetsLandingSurfaceSlopeContact);
            Run("priority 4 grapple flat surface follows right motion", ref failed, PriorityFourGrappleFlatSurfaceFollowsRightMotion);
            Run("priority 4 grapple flat surface follows left motion", ref failed, PriorityFourGrappleFlatSurfaceFollowsLeftMotion);
            Run("priority 4 grapple with slope uses gentle motion bias", ref failed, PriorityFourGrappleWithSlopeUsesGentleMotionBias);
            Run("priority 4 grapple unknown surface uses motion lead and contact y", ref failed, PriorityFourGrappleUnknownSurfaceUsesMotionLeadAndContactY);
            Run("priority 5 teleport rod does not run when grapple too early", ref failed, PriorityFiveTeleportRodDoesNotRunWhenGrappleTooEarly);
            Run("priority 5 teleport rod waits behind valid grapple", ref failed, PriorityFiveTeleportRodWaitsBehindValidGrapple);
            Run("priority 5 teleport rod can fallback when grapple too late", ref failed, PriorityFiveTeleportRodCanFallbackWhenGrappleTooLate);
            Run("safe landing resolve grapple hook speed prefers equipped shoot speed over table", ref failed, SafeLandingResolveGrappleHookSpeedPrefersEquippedShootSpeedOverFallbackTable);
            Run("safe landing resolve grapple hook speed falls back to table only when shoot speed missing", ref failed, SafeLandingResolveGrappleHookSpeedFallsBackToTableOnlyWhenShootSpeedMissing);
            Run("safe landing resolve grapple hook speed uses equipped fallback before inventory shoot speed", ref failed, SafeLandingResolveGrappleHookSpeedUsesEquippedFallbackBeforeInventoryShootSpeed);
            Run("safe landing resolve grapple hook speed uses inventory shoot speed when no equipped grapple", ref failed, SafeLandingResolveGrappleHookSpeedUsesInventoryShootSpeedWhenNoEquippedGrapple);
            Run("safe landing resolve grapple hook speed falls back to default only when unknown", ref failed, SafeLandingResolveGrappleHookSpeedFallsBackToDefaultOnlyWhenUnknown);
            Run("safe landing grapple too early blocks lower priority but not ready", ref failed, SafeLandingGrappleTooEarlyBlocksLowerPriorityButNotReady);
            Run("safe landing grapple too late allows teleport rod fallback", ref failed, SafeLandingGrappleTooLateAllowsTeleportRodFallback);
            Run("safe landing grapple too slow allows priority 5 fallback", ref failed, SafeLandingGrappleTooSlowAllowsPriority5Fallback);
            Run("safe landing grapple only too late still submits when teleport rod disabled", ref failed, SafeLandingGrappleOnlyTooLateStillSubmitsWhenTeleportRodDisabled);
            Run("safe landing grapple only too slow still submits when teleport rod unavailable", ref failed, SafeLandingGrappleOnlyTooSlowStillSubmitsWhenTeleportRodUnavailable);
            Run("safe landing grapple speed unavailable allows priority 5", ref failed, SafeLandingGrappleSpeedUnavailableAllowsPriority5);
            Run("safe landing p1 to p3 block lower priority not destroyed", ref failed, SafeLandingP1toP3BlockLowerPriorityNotDestroyed);
            Run("priority 1 still blocks lower priority when timing not ready", ref failed, PriorityOneStillBlocksLowerPriorityWhenTimingNotReady);
            Run("priority lower strategy waits behind higher timing window", ref failed, PriorityLowerStrategyWaitsBehindHigherTimingWindow);
            Run("safe landing clears repeated rescue when landing changes", ref failed, SafeLandingClearsRepeatedRescueWhenLandingChanges);
            Run("safe landing teleport rod request stale when landing changes", ref failed, SafeLandingTeleportRodRequestStaleWhenLandingChanges);
            Run("safe landing teleport rod request keeps same landing", ref failed, SafeLandingTeleportRodRequestKeepsSameLanding);
            Run("safe landing request builder preserves old metadata keys", ref failed, SafeLandingRequestBuilderPreservesOldMetadataKeys);
            Run("safe landing recovery keeps item when restore target changed", ref failed, SafeLandingRecoveryKeepsItemWhenRestoreTargetChanged);
            Run("channel resolver maps unknown action to global exclusive", ref failed, ChannelResolverUnknownActionDefaultsGlobalExclusive);
            Run("channel resolver maps diagnostic noop to none", ref failed, ChannelResolverDiagnosticNoopUsesNoChannel);
            Run("channel resolver maps use hotbar item to use item and hotbar", ref failed, ChannelResolverUseHotbarItemUsesItemAndHotbar);
            Run("channel resolver maps inventory slot and conflicts with use item", ref failed, ChannelResolverInventorySlotConflictsWithUseItem);
            Run("channel resolver maps safe landing quick mount", ref failed, ChannelResolverSafeLandingQuickMount);
            Run("channel resolver maps safe landing gravity flip", ref failed, ChannelResolverSafeLandingGravityFlip);
            Run("channel resolver maps safe landing grapple", ref failed, ChannelResolverSafeLandingGrapple);
            Run("channel resolver maps dash to dash and direction", ref failed, ChannelResolverDashUsesDashAndDirection);
            Run("channel resolver maps magic string raw input", ref failed, ChannelResolverMagicStringUsesPulseBridge);
            Run("channel resolver maps auto harvest sustained raw input", ref failed, ChannelResolverAutoHarvestSustainedUse);
            Run("channel resolver maps auto capture sustained raw input", ref failed, ChannelResolverAutoCaptureCritterSustainedUse);
            Run("channel resolver maps shop to npc and inventory", ref failed, ChannelResolverShopUsesNpcAndInventory);
            Run("channel resolver maps trash slot to inventory", ref failed, ChannelResolverTrashSlotUsesInventory);
            Run("channel resolver maps reforge to npc and inventory", ref failed, ChannelResolverReforgeUsesNpcAndInventory);
            Run("quick rename increments trailing numeric suffix", ref failed, QuickRenameIncrementsTrailingNumericSuffix);
            Run("auto stack detects only increased item types", ref failed, AutoStackDetectsOnlyIncreasedItemTypes);
            Run("auto stack ignores favorite toggle and unstackable moves", ref failed, AutoStackIgnoresFavoriteToggleAndUnstackableMoves);
            Run("auto stack final signature excludes unstackable items", ref failed, AutoStackFinalSignatureExcludesUnstackableItems);
            Run("auto stack request uses chest selective quick stack metadata", ref failed, AutoStackRequestUsesChestSelectiveQuickStackMetadata);
            Run("auto stack allows player inventory open", ref failed, AutoStackAllowsPlayerInventoryOpen);
            Run("auto stack still blocks chest UI", ref failed, AutoStackStillBlocksChestUi);
            Run("auto stack uses short inventory-open settle window", ref failed, AutoStackUsesShortInventoryOpenSettleWindow);
            Run("auto sell default list is conservative fishing junk", ref failed, AutoSellDefaultListIsConservativeFishingJunk);
            Run("auto sell request uses shop metadata", ref failed, AutoSellRequestUsesShopMetadata);
            Run("auto sell allows player inventory open", ref failed, AutoSellAllowsPlayerInventoryOpen);
            Run("auto sell candidates use inventory snapshot", ref failed, AutoSellCandidatesUseInventorySnapshot);
            Run("auto discard default list is empty", ref failed, AutoDiscardDefaultListIsEmpty);
            Run("auto discard request uses trash metadata", ref failed, AutoDiscardRequestUsesTrashMetadata);
            Run("auto discard allows player inventory open", ref failed, AutoDiscardAllowsPlayerInventoryOpen);
            Run("auto discard candidates use inventory snapshot", ref failed, AutoDiscardCandidatesUseInventorySnapshot);
            Run("quick bag open request uses inventory slot metadata", ref failed, QuickBagOpenRequestUsesInventorySlotMetadata);
            Run("quick bag open yields after batch when cleanup enabled", ref failed, QuickBagOpenYieldsAfterBatchWhenCleanupEnabled);
            Run("auto deposit coins request uses chest metadata", ref failed, AutoDepositCoinsRequestUsesChestMetadata);
            Run("auto deposit coins candidates use inventory snapshot", ref failed, AutoDepositCoinsCandidatesUseInventorySnapshot);
            Run("auto extractinator request uses item use metadata", ref failed, AutoExtractinatorRequestUsesItemUseMetadata);
            Run("keep favorited request uses inventory slot metadata", ref failed, KeepFavoritedRequestUsesInventorySlotMetadata);
            Run("keep favorited manual unfavorite clears tracking", ref failed, KeepFavoritedManualUnfavoriteClearsTracking);
            Run("keep favorited restores trash round trip", ref failed, KeepFavoritedRestoresTrashRoundTrip);
            Run("quick reforge prefixes normalize blanks and duplicates", ref failed, QuickReforgePrefixesNormalizeBlanksAndDuplicates);
            Run("quick reforge prefix matching accepts full affix names", ref failed, QuickReforgePrefixMatchingAcceptsFullAffixNames);
            Run("quick reforge request uses reforge metadata", ref failed, QuickReforgeRequestUsesReforgeMetadata);
            Run("auto tax collect request uses npc metadata", ref failed, AutoTaxCollectRequestUsesNpcMetadata);
            Run("feature catalog exposes travel menu", ref failed, FeatureCatalogExposesTravelMenu);
            Run("travel menu runtime path is resumed", ref failed, TravelMenuRuntimePathIsResumed);
            Run("feature catalog exposes auto discard", ref failed, FeatureCatalogExposesAutoDiscard);
            Run("feature catalog exposes quick reforge", ref failed, FeatureCatalogExposesQuickReforge);
            Run("feature catalog exposes auto tax collect", ref failed, FeatureCatalogExposesAutoTaxCollect);
            Run("feature catalog exposes auto mining", ref failed, FeatureCatalogExposesAutoMining);
            Run("feature catalog exposes auto capture critter", ref failed, FeatureCatalogExposesAutoCaptureCritter);
            Run("feature catalog exposes auto harvest", ref failed, FeatureCatalogExposesAutoHarvest);
            Run("auto mining scanner links three-tile gaps", ref failed, AutoMiningScannerLinksThreeTileGaps);
            Run("auto mining scanner keeps inactive mined seed connectivity", ref failed, AutoMiningScannerKeepsInactiveMinedSeedConnectivity);
            Run("auto mining selected slot switch interrupts selection", ref failed, AutoMiningSelectedSlotSwitchInterruptsSelection);
            Run("auto mining request uses item use metadata", ref failed, AutoMiningRequestUsesItemUseMetadata);
            Run("auto capture critter request uses sustained raw input metadata", ref failed, AutoCaptureCritterRequestUsesSustainedRawInputMetadata);
            Run("auto capture critter range uses bug net reach", ref failed, AutoCaptureCritterRangeUsesBugNetReach);
            Run("auto capture critter restore pole keeps fishing slot selected", ref failed, AutoCaptureCritterRestorePoleKeepsFishingSlotSelected);
            Run("selected item state force selection updates hotbar state", ref failed, SelectedItemStateForceSelectionUpdatesHotbarState);
            Run("fishing loadout restore attempted keeps session for retry", ref failed, FishingLoadoutRestoreAttemptedKeepsSessionForRetry);
            Run("auto capture critter recognizes bug net item type", ref failed, AutoCaptureCritterRecognizesBugNetItemType);
            Run("auto capture critter manual mode requires held bug net", ref failed, AutoCaptureCritterManualModeRequiresHeldBugNet);
            Run("auto capture critter tick enqueues request when nearby", ref failed, AutoCaptureCritterTickEnqueuesRequestWhenNearby);
            Run("auto harvest maps exact herb seeds", ref failed, AutoHarvestMapsExactHerbSeeds);
            Run("auto harvest request uses sustained raw input metadata", ref failed, AutoHarvestRequestUsesSustainedRawInputMetadata);
            Run("auto harvest replant request uses exact seed metadata", ref failed, AutoHarvestReplantRequestUsesExactSeedMetadata);
            Run("auto mining targets nearest reachable frontier tile", ref failed, AutoMiningTargetsNearestReachableFrontierTile);
            Run("auto mining reach excludes rectangle corners outside helper radius", ref failed, AutoMiningReachExcludesRectangleCornersOutsideHelperRadius);
            Run("auto mining green reach respects pick power", ref failed, AutoMiningGreenReachRespectsPickPower);
            Run("worldgen debug viewer and developer menu are always available", ref failed, WorldGenDebugViewerAndDeveloperMenuAlwaysAvailable);
            Run("diagnostic snapshot writes worldgen debug state", ref failed, DiagnosticSnapshotWritesWorldGenDebugState);
            Run("diagnostic snapshot writes auto stack state", ref failed, DiagnosticSnapshotWritesAutoStackState);
            Run("diagnostic snapshot writes auto deposit coins state", ref failed, DiagnosticSnapshotWritesAutoDepositCoinsState);
            Run("diagnostic snapshot writes auto tax collect state", ref failed, DiagnosticSnapshotWritesAutoTaxCollectState);
            Run("diagnostic snapshot writes auto capture critter state", ref failed, DiagnosticSnapshotWritesAutoCaptureCritterState);
            Run("diagnostic snapshot writes auto harvest state", ref failed, DiagnosticSnapshotWritesAutoHarvestState);
            Run("performance hitch recorder detects runtime gaps", ref failed, PerformanceHitchRecorderDetectsRuntimeGaps);
            Run("diagnostic snapshot writes performance hitch state", ref failed, DiagnosticSnapshotWritesPerformanceHitchState);
            Run("feature catalog exposes implemented misc inventory automation", ref failed, FeatureCatalogExposesImplementedMiscInventoryAutomation);
            Run("feature catalog exposes goblin execution", ref failed, FeatureCatalogExposesGoblinExecution);
            Run("first-run app settings defaults match requested UI baseline", ref failed, FirstRunAppSettingsDefaultsMatchRequestedUiBaseline);
            Run("auto capture critter mode aliases preserve legacy bool", ref failed, AutoCaptureCritterModeAliasesPreserveLegacyBool);
            Run("app settings code-domain aliases preserve misc storage", ref failed, AppSettingsCodeDomainAliasesPreserveMiscStorage);
            Run("game state read options map coin automation to coins profile", ref failed, GameStateReadOptionsMapCoinAutomationToCoinsProfile);
            Run("game state read options keep auto tax collect lightweight", ref failed, GameStateReadOptionsKeepAutoTaxCollectLightweight);
            Run("game state read options merge capture and stack profiles", ref failed, GameStateReadOptionsMergeCaptureAndStackProfiles);
            Run("game state read options keep diagnostics full profile", ref failed, GameStateReadOptionsKeepDiagnosticsFullProfile);
            Run("diagnostic snapshot writes game state read profiles", ref failed, DiagnosticSnapshotWritesGameStateReadProfiles);
            Run("runtime settings snapshot normalizes hot path fields", ref failed, RuntimeSettingsSnapshotNormalizesHotPathFields);
            Run("runtime settings snapshot builds game state profile", ref failed, RuntimeSettingsSnapshotBuildsGameStateProfile);
            Run("runtime settings snapshot splits fishing dispatch layers", ref failed, RuntimeSettingsSnapshotSplitsFishingDispatchLayers);
            Run("runtime fishing dispatch skips filter-only settings", ref failed, RuntimeFishingDispatchSkipsFilterOnlySettings);
            Run("fishing residual state keeps runtime dispatch alive", ref failed, FishingResidualStateKeepsRuntimeDispatchAlive);
            Run("runtime settings snapshot provider rebuilds after config mutation", ref failed, RuntimeSettingsSnapshotProviderRebuildsAfterConfigMutation);
            Run("runtime settings snapshot provider skips disabled list hashes", ref failed, RuntimeSettingsSnapshotProviderSkipsDisabledListHashes);
            Run("runtime service scheduler honors cadence and disabled cleanup", ref failed, RuntimeServiceSchedulerHonorsCadenceAndDisabledCleanup);
            Run("runtime input focus guard uses game state focus", ref failed, RuntimeInputFocusGuardUsesGameStateFocus);
            Run("combat auto clicker item use request allows combat aim", ref failed, CombatAutoClickerItemUseRequestAllowsCombatAim);
            Run("combat perfect revolver ItemCheck takeover mirrors helper cadence", ref failed, CombatPerfectRevolverItemCheckTakeoverMirrorsHelperCadence);
            Run("combat perfect revolver schedules only in fire window", ref failed, CombatPerfectRevolverSchedulesOnlyInFireWindow);
            Run("combat goblin execution allows only tinkerer when enabled", ref failed, CombatGoblinExecutionAllowsOnlyTinkererWhenEnabled);
            Run("travel menu diagnostics clone keeps scoped hook fields", ref failed, TravelMenuDiagnosticsCloneKeepsScopedHookFields);
            Run("travel menu ItemCheck guard suppresses world use and restores click", ref failed, TravelMenuItemCheckGuardSuppressesWorldUseAndRestoresClick);
            Run("travel menu CreativeUI world input guard does not override mouse state", ref failed, TravelMenuCreativeUiWorldInputGuardDoesNotOverrideMouseState);
            Run("travel menu CreativeUI Draw restore preserves vanilla mouse interface", ref failed, TravelMenuCreativeUiDrawRestorePreservesVanillaMouseInterface);
            Run("travel menu CreativeUI Draw release pulse ignores other inventory buttons", ref failed, TravelMenuCreativeUiDrawReleasePulseIgnoresOtherInventoryButtons);
            Run("travel menu CreativeUI input bypass clears using item gate", ref failed, TravelMenuCreativeUiInputBypassClearsUsingItemGate);
            Run("travel menu CreativeUI release pulse is once per mouse hold", ref failed, TravelMenuCreativeUiReleasePulseIsOncePerMouseHold);
            Run("travel menu reads godmode power fallback from creative flag", ref failed, TravelMenuGodmodePowerReadUsesCreativeFlagFallback);
            Run("travel menu reports disabled godmode power fallback from creative flag", ref failed, TravelMenuGodmodePowerReadDisabledWhenCreativeFlagOff);
            Run("travel menu state store recovers active marker from backup", ref failed, TravelMenuStateStoreRecoversActiveMarkerFromBackup);
            Run("channel arbiter acquires and releases a lease", ref failed, ChannelArbiterAcquireRelease);
            Run("channel arbiter blocks conflicting channels", ref failed, ChannelArbiterBlocksConflicts);
            Run("channel arbiter allows non-conflicting channels", ref failed, ChannelArbiterAllowsNonConflicting);
            Run("try enqueue accepts normal request", ref failed, TryEnqueueAcceptsNormalRequest);
            Run("try enqueue rejects duplicate pending admission key", ref failed, TryEnqueueRejectsDuplicatePendingAdmissionKey);
            Run("try enqueue rejects duplicate running admission key", ref failed, TryEnqueueRejectsDuplicateRunningAdmissionKey);
            Run("try enqueue reports item use bridge busy", ref failed, TryEnqueueReportsItemUseBridgeBusy);
            Run("try enqueue bridge busy keeps pending count unchanged", ref failed, TryEnqueueBridgeBusyKeepsPendingCountUnchanged);
            Run("try enqueue derives queue expiration", ref failed, TryEnqueueDerivesQueueExpiration);
            Run("try enqueue allows distinct empty-source default keys", ref failed, TryEnqueueAllowsDistinctEmptySourceDefaultKeys);
            Run("legacy enqueue still accepts while channel busy", ref failed, LegacyEnqueueStillAcceptsWhileChannelBusy);
            Run("pending queue timeout expires before start", ref failed, PendingQueueTimeoutExpiresBeforeStart);
            Run("pending expiration does not cancel executor", ref failed, PendingExpirationDoesNotCancelExecutor);
            Run("pending queue timeout expires while running", ref failed, PendingQueueTimeoutExpiresWhileRunning);
            Run("pending expiration while running does not start or cancel pending executor", ref failed, PendingExpirationWhileRunningDoesNotStartOrCancelPendingExecutor);
            Run("running lease survives pending expiration", ref failed, RunningLeaseSurvivesPendingExpiration);
            Run("scheduler keeps priority then created order", ref failed, SchedulerKeepsPriorityThenCreatedOrder);
            Run("pending lower priority same channel does not block admission", ref failed, PendingLowerPrioritySameChannelDoesNotBlockAdmission);
            Run("simulated jump request has queue timeout", ref failed, SimulatedJumpRequestHasQueueTimeout);
            Run("continuous dash request has queue timeout", ref failed, ContinuousDashRequestHasQueueTimeout);
            Run("auto facing request has queue timeout", ref failed, AutoFacingRequestHasQueueTimeout);
            Run("combat aim diagnostics metadata keeps stable field names", ref failed, CombatAimDiagnosticsMetadataKeepsStableFieldNames);
            Run("combat aim weapon family resolver classifies requested families", ref failed, CombatAimWeaponFamilyResolverClassifiesRequestedFamilies);
            Run("combat aim weapon family diagnostics emits metadata fields", ref failed, CombatAimWeaponFamilyDiagnosticsEmitsMetadataFields);
            Run("combat aim skip reasons normalize to stable strings", ref failed, CombatAimSkipReasonsNormalizeToStableStrings);
            Run("persistent cursor policy rejects ordinary projectile weapons", ref failed, PersistentCursorPolicyRejectsOrdinaryProjectileWeapons);
            Run("persistent cursor policy allows channel projectile scoped only", ref failed, PersistentCursorPolicyAllowsChannelProjectileScopedOnly);
            Run("persistent cursor policy allows special projectile scoped only", ref failed, PersistentCursorPolicyAllowsSpecialProjectileScopedOnly);
            Run("persistent cursor policy rejects placement summons and sentries", ref failed, PersistentCursorPolicyRejectsPlacementSummonsAndSentries);
            Run("persistent cursor policy preserves yoyo eligibility", ref failed, PersistentCursorPolicyPreservesYoyoEligibility);
            Run("projectile cursor match accepts only local channel projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalChannelProjectile);
            Run("projectile cursor match accepts only local special weapon projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalSpecialWeaponProjectile);
            Run("projectile cursor match accepts only local flail release projectile", ref failed, ProjectileCursorMatchAcceptsOnlyLocalFlailReleaseProjectile);
            Run("combat aim scoped cursor diagnostics keeps ownership fields", ref failed, CombatAimScopedCursorDiagnosticsKeepsOwnershipFields);
            Run("flail policy only accepts non-yoyo channel aiStyle 15", ref failed, FlailPolicyOnlyAcceptsNonYoyoChannelAiStyle15);
            Run("flail control preserves hold spin and releases on physical release", ref failed, FlailControlPreservesHoldSpinAndReleasesOnPhysicalRelease);
            Run("flail ItemCheck takeover skips hold spin", ref failed, FlailItemCheckTakeoverSkipsHoldSpin);
            Run("flail ReleaseHold ItemCheck takeover arms projectile tail before runtime update", ref failed, FlailReleaseHoldItemCheckTakeoverArmsProjectileTailBeforeRuntimeUpdate);
            Run("flail ItemCheck takeover applies physical release scope", ref failed, FlailItemCheckTakeoverAppliesPhysicalReleaseScope);
            Run("flail stuck projectile retries release after physical release", ref failed, FlailStuckProjectileRetriesReleaseAfterPhysicalRelease);
            Run("flail cached release aims after target selection loss", ref failed, FlailCachedReleaseAimsAfterTargetSelectionLoss);
            Run("flail release cursor tail keeps ProjectileAI scoped aim", ref failed, FlailReleaseCursorTailKeepsProjectileAiScopedAim);
            Run("flail cached release rejects yoyo and normal channel", ref failed, FlailCachedReleaseRejectsYoyoAndNormalChannel);
            Run("special projectile rules distinguish weapon and ammo projectiles", ref failed, SpecialProjectileRulesDistinguishWeaponAndAmmoProjectiles);
            Run("special dual projectile rejects Vortex ammo bullet scoped cursor", ref failed, SpecialDualProjectileRejectsVortexAmmoBulletScopedCursor);
            Run("special dual projectile matches Vortex controller and rocket scoped cursor", ref failed, SpecialDualProjectileMatchesVortexControllerAndRocketScopedCursor);
            Run("Onyx Blaster stays ItemCheck spread path without special scoped cursor", ref failed, OnyxBlasterStaysItemCheckSpreadPathWithoutSpecialScopedCursor);
            Run("ordinary shotgun family stays out of special projectile scoped cursor", ref failed, OrdinaryShotgunFamilyStaysOutOfSpecialProjectileScopedCursor);
            Run("special projectile tail keeps scoped aim after use window", ref failed, SpecialProjectileTailKeepsScopedAimAfterUseWindow);
            Run("special projectile tail matches Xenopopper bubble scoped cursor", ref failed, SpecialProjectileTailMatchesXenopopperBubbleScopedCursor);
            Run("special projectile tail uses Xenopopper bubble ProjectileKill scoped cursor", ref failed, SpecialProjectileTailUsesXenopopperBubbleProjectileKillScopedCursor);
            Run("special projectile tail active bubble refreshes fixed tail window", ref failed, SpecialProjectileTailActiveBubbleRefreshesFixedTailWindow);
            Run("special projectile tail uses recomputed aim after target moves", ref failed, SpecialProjectileTailUsesRecomputedAimAfterTargetMoves);
            Run("special dual projectile tail recomputes aim for moving assist target", ref failed, SpecialDualProjectileTailRecomputesAimForMovingAssistTarget);
            Run("special projectile tail expires inactive bubble and ignores ammo bullet", ref failed, SpecialProjectileTailExpiresInactiveBubbleAndIgnoresAmmoBullet);
            Run("combat aim itemcheck log throttle keeps independent keys", ref failed, CombatAimItemCheckLogThrottleKeepsIndependentKeys);
            Run("release hold target dummy validation respects track dummy", ref failed, ReleaseHoldTargetDummyValidationRespectsTrackDummy);
            Run("input action queue releases channel after terminal start", ref failed, InputActionQueueReleasesChannelAfterTerminalStart);
            Run("input action queue releases channel after start exception", ref failed, InputActionQueueReleasesChannelAfterStartException);
            Run("input action queue releases channel after cancel", ref failed, InputActionQueueReleasesChannelAfterCancel);
            Run("input action queue clear releases pending and running leases", ref failed, InputActionQueueClearReleasesPendingAndRunningLeases);
            Run("diagnostic noop does not acquire channel lease", ref failed, DiagnosticNoopDoesNotAcquireChannelLease);
            Run("fishing bobber observer selects latest observation", ref failed, FishingBobberObserverSelectsLatestObservation);
            Run("fishing bobber observer tie-breaks by WhoAmI", ref failed, FishingBobberObserverTieBreaksByWhoAmI);
            Run("fishing bobber observer remove missing rebuilds latest", ref failed, FishingBobberObserverRemoveMissingRebuildsLatest);
            Run("fishing bobber observer empty scan clears observations", ref failed, FishingBobberObserverEmptyScanClearsObservations);
            Run("information fishing bobber uses fresh observer", ref failed, InformationFishingBobberUsesFreshObserver);
            Run("fishing fallback scan gate skips fresh hook observations", ref failed, FishingFallbackScanGateSkipsFreshHookObservations);
            Run("fishing fallback scan gate keeps old fallback for sensitive stages", ref failed, FishingFallbackScanGateKeepsOldFallbackForSensitiveStages);
            Run("fishing session waits for bobber liquid", ref failed, FishingSessionWaitsForBobberLiquid);
            Run("fishing filter skip holds selection until bobber gone", ref failed, FishingFilterSkipHoldsSelectionUntilBobberGone);
            Run("fishing auto equipment water skips lava hook and covered parts", ref failed, FishingAutoEquipmentWaterSkipsLavaHookAndCoveredParts);
            Run("fishing auto equipment lava prefers lavaproof bag over hook", ref failed, FishingAutoEquipmentLavaPrefersLavaproofBagOverHook);
            Run("fishing auto equipment keeps stackable tackle bags", ref failed, FishingAutoEquipmentKeepsStackableTackleBags);
            Run("fishing auto equipment lava uses hook without lavaproof bag", ref failed, FishingAutoEquipmentLavaUsesHookWithoutLavaproofBag);
            Run("fishing auto equipment deduplicates luck and bobbers", ref failed, FishingAutoEquipmentDeduplicatesLuckAndBobbers);
            Run("fishing auto equipment capacity keeps highest scores", ref failed, FishingAutoEquipmentCapacityKeepsHighestScores);
            Run("fishing auto equipment replaces covered lower accessory first", ref failed, FishingAutoEquipmentReplacesCoveredLowerAccessoryFirst);
            Run("fishing auto equipment manual inventory interaction restores after bobber gone", ref failed, FishingAutoEquipmentManualInventoryInteractionStopsKeepingRodAppliedWithoutBobber);
            Run("fishing auto equipment restore keeps pending when original moved", ref failed, FishingAutoEquipmentRestoreKeepsPendingWhenOriginalMovedByUser);
            Run("fishing auto equipment restore completes when original already back", ref failed, FishingAutoEquipmentRestoreCompletesWhenOriginalAlreadyBack);
            Run("fishing filter requires sonar buff", ref failed, FishingFilterRequiresSonarBuff);
            Run("enemy segment labels hide middle segments", ref failed, EnemySegmentLabelsHideMiddleSegments);
            Run("known worm segment labels use Terraria NPC roles", ref failed, KnownWormSegmentLabelsUseTerrariaNpcRoles);
            Run("information sign text all mode keeps vanilla line cap", ref failed, InformationSignTextAllModeKeepsVanillaLineCap);
            Run("information sign text line mode respects configured lines", ref failed, InformationSignTextLineModeRespectsConfiguredLines);
            Run("information sign text character mode truncates before wrapping", ref failed, InformationSignTextCharacterModeTruncatesBeforeWrapping);
            Run("information sign text centers each displayed line on sign", ref failed, InformationSignTextCentersEachDisplayedLineOnSign);
            Run("information sign text layout cache reuses wrapped lines", ref failed, InformationSignTextLayoutCacheReusesWrappedLines);
            Run("information page content height includes limit rows", ref failed, InformationPageContentHeightIncludesLimitRows);
            Run("UI clipped single line text avoids bottom stick", ref failed, UiClippedSingleLineTextAvoidsBottomStick);
            Run("information status panel layout cache reuses prepared rows", ref failed, InformationStatusPanelLayoutCacheReusesPreparedRows);
            Run("UI text renderer fast path keeps safe fallbacks", ref failed, UiTextRendererFastPathKeepsSafeFallbacks);
            Run("UI text renderer font signature change clears caches", ref failed, UiTextRendererFontSignatureChangeClearsCaches);
            Run("information tombstone text defaults to red and splits tile type", ref failed, InformationTombstoneTextDefaultsToRedAndSplitsTileType);
            Run("information mana crystal highlight defaults to off and uses tile id", ref failed, InformationManaCrystalHighlightDefaultsToOffAndUsesTileId);
            Run("information tile access reads cached tile members", ref failed, InformationTileAccessReadsCachedTileMembers);
            Run("information tile highlight cache signature tracks bounds settings and world", ref failed, InformationTileHighlightCacheSignatureTracksBoundsSettingsAndWorld);
            Run("information tile highlight cache keeps safety refresh", ref failed, InformationTileHighlightCacheKeepsSafetyRefresh);
            Run("information fishing catch query key tracks environment", ref failed, InformationFishingCatchQueryKeyTracksEnvironment);
            Run("legacy UI page layout cache ignores window position", ref failed, LegacyUiPageLayoutCacheIgnoresWindowPosition);
            Run("legacy UI page layout cache dirties on scroll size and state", ref failed, LegacyUiPageLayoutCacheDirtiesOnScrollSizeAndState);
            Run("legacy UI page layout cache dirties on font generation", ref failed, LegacyUiPageLayoutCacheDirtiesOnFontGeneration);
            Run("legacy UI hover layout token ignores window and content position", ref failed, LegacyUiHoverLayoutTokenIgnoresWindowAndContentPosition);
            Run("legacy UI hover layout token dirties on page size scroll settings and font", ref failed, LegacyUiHoverLayoutTokenDirtiesOnPageSizeScrollSettingsAndFont);
            Run("legacy UI element frame reuses pooled elements", ref failed, LegacyUiElementFrameReusesPooledElements);
            Run("legacy UI hover cache reuses stable hover id", ref failed, LegacyUiHoverCacheReusesStableHoverId);
            Run("legacy UI context hover uses cached element id", ref failed, LegacyUiContextHoverUsesCachedElementId);
            Run("diagnostic mouse state reader reuses snapshot within draw frame", ref failed, DiagnosticMouseStateReaderReusesSnapshotWithinDrawFrame);
            Run("diagnostic mouse state reader refreshes on new fast draw frame", ref failed, DiagnosticMouseStateReaderRefreshesOnNewFastDrawFrame);
            Run("diagnostic mouse state reader refreshes when draw frame changes under same update", ref failed, DiagnosticMouseStateReaderRefreshesWhenDrawFrameChangesUnderSameUpdate);
            Run("UI mouse capture service short-circuits within draw frame", ref failed, UiMouseCaptureServiceShortCircuitsWithinDrawFrame);
            Run("UI mouse capture service rewrites capture and suppress on next draw frame", ref failed, UiMouseCaptureServiceRewritesCaptureAndSuppressOnNextDrawFrame);
            Run("combat performance caches stable metadata only", ref failed, CombatPerformanceCachesStableMetadataOnly);
            Run("runtime performance diagnostics records slowest operation", ref failed, RuntimePerformanceDiagnosticsRecordsSlowestOperation);
            Run("information NPC label snapshot reuses movement only", ref failed, InformationNpcLabelSnapshotReusesMovementOnly);
            Run("information chest labels cache signature changes with mode and player-world records", ref failed, InformationChestLabelsCacheSignatureChangesWithModeAndKnownKeys);
            Run("player-world behavior records isolate opened chests", ref failed, PlayerWorldBehaviorRecordsIsolateOpenedChests);
            Run("legacy opened chest keys migrate to current player-world only", ref failed, LegacyOpenedChestKeysMigrateToCurrentPlayerWorldOnly);
            Run("information chest key parsing survives world rename with same id", ref failed, InformationChestKeyParsingSurvivesWorldRenameWithSameId);
            Run("information chest tile fallback detects basic container ids", ref failed, InformationChestTileFallbackDetectsBasicContainerIds);
            Run("information chest tile fallback normalizes two by two frame origin", ref failed, InformationChestTileFallbackNormalizesTwoByTwoFrameOrigin);
            Run("information chest labels frame limit allows dense rooms", ref failed, InformationChestLabelsFrameLimitAllowsDenseRooms);
            Run("information chest labels draw order prioritizes screen center", ref failed, InformationChestLabelsDrawOrderPrioritizesScreenCenter);
            Run("information chest label sort cache dirties on source and movement threshold", ref failed, InformationChestLabelSortCacheDirtiesOnSourceAndMovementThreshold);
            Run("information chest label cache cull covers bucket movement", ref failed, InformationChestLabelCacheCullCoversBucketMovement);
            Run("information luck breakdown follows Terraria source formula", ref failed, InformationLuckBreakdownFollowsTerrariaSourceFormula);
            Run("information luck breakdown wraps source details", ref failed, InformationLuckBreakdownWrapsSourceDetails);
            Run("combat equipment warning matches requested equipment names", ref failed, CombatEquipmentWarningMatchesRequestedEquipmentNames);
            Run("combat equipment warning prompts only on hazard entry", ref failed, CombatEquipmentWarningPromptsOnlyOnHazardEntry);
            Run("fishing filter nested scroll bubbles when list cannot move", ref failed, FishingFilterNestedScrollBubblesWhenListCannotMove);
            Run("legacy UI window capture accepts scaled screen coordinates", ref failed, LegacyUiWindowCaptureAcceptsScaledScreenCoordinates);

            if (failed == 0)
            {
                Console.WriteLine("All JueMingZ tests passed.");
                return 0;
            }

            Console.WriteLine("JueMingZ tests failed: " + failed);
            return 1;
        }

        private static void Run(string name, ref int failed, Action test)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL " + name + ": " + error.Message);
            }
        }

    }
}
