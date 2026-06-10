using System;
using JueMingZ.Actions;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class ItemUseHookCallbacks
    {
        // Player.ItemCheck is the only frame where scoped item-use writers may
        // mutate input; every prefix takeover must be paired with postfix restore.
        private struct ItemUseHookState
        {
            public bool BridgeApplied;
            public bool AimApplied;
            public bool PulseApplied;
            public bool PulsePressed;
            public bool PulseAllowCombatAim;
            public bool AutoMiningSustainedUseApplied;
            public bool AutoHarvestSustainedUseApplied;
            public bool AutoCaptureCritterSustainedUseApplied;
            public bool PhasebladeQuickSwitchApplied;
            public bool PhasebladeQuickSwitchPressed;
            public bool PhasebladeQuickSwitchReleased;
            public bool PhasebladeQuickSwitchAllowCombatAim;
            public bool AutoClickerTakeoverApplied;
            public bool FlailComboTakeoverApplied;
            public bool PerfectRevolverTakeoverApplied;
            public bool FlailTakeoverApplied;
            public bool TravelMenuItemCheckGuardApplied;
            public bool BridgePendingAtStart;
            public Guid RequestId;
            public Guid PulseRequestId;
            public Guid AutoMiningSustainedUseRequestId;
            public Guid AutoHarvestSustainedUseRequestId;
            public Guid AutoCaptureCritterSustainedUseRequestId;
            public Guid PhasebladeQuickSwitchRequestId;
            public UseItemInputState RestoreState;
            public UseItemInputState PulseRestoreState;
            public UseItemInputState AutoMiningSustainedUseRestoreState;
            public UseItemInputState AutoHarvestSustainedUseRestoreState;
            public UseItemInputState AutoCaptureCritterSustainedUseRestoreState;
            public UseItemInputState PhasebladeQuickSwitchRestoreState;
            public MouseTargetInputState AimRestoreState;
            public MouseTargetInputState BridgeMouseRestoreState;
            public MouseTargetInputState AutoMiningSustainedUseMouseRestoreState;
            public MouseTargetInputState AutoHarvestSustainedUseMouseRestoreState;
            public MouseTargetInputState AutoCaptureCritterSustainedUseMouseRestoreState;
            public int AutoCaptureCritterSustainedUseRestoreSlot;
            public bool AutoCaptureCritterSustainedUseDirectionCaptured;
            public int AutoCaptureCritterSustainedUseOriginalDirection;
            public TerrariaInputCompat.ScopedUseItemTakeover AutoClickerTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover FlailComboTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover PerfectRevolverTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover FlailTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover TravelMenuItemCheckGuard;
            public ItemUseBridgeContext Context;
            public CombatAimItemCheckDecision AimDecision;
            public AutoBuffFollowItemUseObservation FollowUseObservation;
            public ItemCheckInputProbeFrame InputProbeBefore;
            public ItemCheckWriterDecision WriterDecision;
        }

        private static void Prefix(object __instance, ref ItemUseHookState __state)
        {
            __state = new ItemUseHookState();

            try
            {
                if (__instance == null || !TerrariaInputCompat.TryIsLocalPlayer(__instance))
                {
                    return;
                }

                __state.BridgePendingAtStart = ItemUseBridge.PendingRequestId != Guid.Empty;
                __state.InputProbeBefore = CombatAutoClickerItemCheckInputProbe.CapturePrefix(__instance, __state.BridgePendingAtStart);

                if (TryApplyTravelMenuItemCheckGuard(__instance, ref __state))
                {
                    return;
                }

                bool autoFacingApplied;
                string autoFacingMessage;
                TerrariaInputCompat.TryApplyAutoFacingDirectionOverrideForItemCheck(__instance, out autoFacingApplied, out autoFacingMessage);
                CombatAutoFacingService.TryApplyManualMovementFacingForItemCheck(__instance, out autoFacingApplied, out autoFacingMessage);

                var bridgePendingAtStart = __state.BridgePendingAtStart;
                if (!bridgePendingAtStart)
                {
                    AutoBuffFollowService.TryCaptureManualItemUse(__instance, out __state.FollowUseObservation);
                }

                ItemUseBridgeContext context;
                if (!ItemUseBridge.TryBeginFromItemCheck(__instance, out context))
                {
                    // Prefix selects the only active scoped ItemCheck writer for
                    // this frame; every applied takeover must be restored below.
                    __state.WriterDecision = ItemCheckWriterArbiter.ResolveOwner(BuildItemCheckWriterContext(bridgePendingAtStart));
                    if (TryApplyArbitratedActiveWriter(__instance, ref __state, __state.WriterDecision))
                    {
                        return;
                    }

                    if (__state.WriterDecision != null &&
                        __state.WriterDecision.Owner != ItemCheckWriterKind.None)
                    {
                        CombatItemCheckAutoClickService.RecordExternalSkip(__state.WriterDecision.Reason);
                        return;
                    }

                    // Combat writers are fallbacks. Bridge, world automation,
                    // and pulse owners must finish or restore before they run.
                    if (TryApplyPerfectRevolverTakeover(__instance, ref __state))
                    {
                        ItemCheckWriterArbiter.RecordApplied(
                            ItemCheckWriterKind.CombatPerfectRevolver,
                            Guid.Empty,
                            __state.PerfectRevolverTakeover != null && __state.PerfectRevolverTakeover.Pressed ? "press" : "release",
                            "perfectRevolverTakeover");
                        if (__state.PerfectRevolverTakeoverApplied && __state.PerfectRevolverTakeover != null && __state.PerfectRevolverTakeover.Pressed)
                        {
                            TryApplyCombatAim(__instance, ref __state, false);
                        }

                        return;
                    }

                    if (ShouldAttemptFlailComboTakeover(
                            __state.BridgePendingAtStart,
                            ItemUseBridge.PendingRequestId != Guid.Empty,
                            __state.PulseApplied,
                            __state.AutoMiningSustainedUseApplied,
                            __state.AutoHarvestSustainedUseApplied,
                            __state.AutoCaptureCritterSustainedUseApplied) &&
                        TryApplyFlailComboTakeover(__instance, ref __state))
                    {
                        ItemCheckWriterArbiter.RecordApplied(
                            ItemCheckWriterKind.CombatFlailCombo,
                            Guid.Empty,
                            __state.FlailComboTakeover != null && __state.FlailComboTakeover.Pressed ? "press" : "release",
                            "flailComboTakeover");
                        CombatItemCheckAutoClickService.RecordExternalSkip("flailComboTakeover");
                        if (__state.FlailComboTakeoverApplied && __state.FlailComboTakeover != null)
                        {
                            TryApplyCombatAim(__instance, ref __state, false);
                            if (__state.FlailComboTakeover.Pressed)
                            {
                                TryRememberFlailComboPressAim(ref __state);
                            }
                            else
                            {
                                TryRememberFlailComboReleaseTail(ref __state);
                            }
                        }

                        return;
                    }

                    if (ShouldAttemptAutoClickerTakeover(
                            __state.BridgePendingAtStart,
                            ItemUseBridge.PendingRequestId != Guid.Empty,
                            __state.PulseApplied,
                            __state.AutoMiningSustainedUseApplied,
                            __state.AutoHarvestSustainedUseApplied,
                            __state.AutoCaptureCritterSustainedUseApplied) &&
                        TryApplyAutoClickerTakeover(__instance, ref __state))
                    {
                        ItemCheckWriterArbiter.RecordApplied(
                            ItemCheckWriterKind.CombatItemCheckAutoClicker,
                            Guid.Empty,
                            __state.AutoClickerTakeover != null && __state.AutoClickerTakeover.Pressed ? "press" : "release",
                            "autoClickerTakeover");
                        if (__state.AutoClickerTakeoverApplied && __state.AutoClickerTakeover != null && __state.AutoClickerTakeover.Pressed)
                        {
                            TryApplyCombatAim(__instance, ref __state, false);
                        }

                        return;
                    }

                    if (!__state.PulseApplied || __state.PulseAllowCombatAim)
                    {
                        TryApplyCombatAim(__instance, ref __state, false);
                        TryApplyFlailCachedReleaseAim(__instance, ref __state);
                        if (TryApplyFlailTakeover(__instance, ref __state))
                        {
                            ItemCheckWriterArbiter.RecordApplied(
                                ItemCheckWriterKind.CombatFlailRelease,
                                Guid.Empty,
                                "release",
                                "flailReleaseTakeover");
                        }
                        else if (__state.AimApplied)
                        {
                            ItemCheckWriterArbiter.RecordApplied(
                                ItemCheckWriterKind.CombatAim,
                                Guid.Empty,
                                "aimOnly",
                                "combatAimOnly");
                        }
                    }

                    return;
                }

                __state.FollowUseObservation = null;

                UseItemInputState restoreState;
                if (!TerrariaInputCompat.TryCaptureUseItemInputState(__instance, out restoreState))
                {
                    ItemUseBridge.Fail(context.RequestId, "Failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                    return;
                }

                MouseTargetInputState mouseRestoreState = null;
                if ((context.HasMouseWorldTarget || context.HasMouseScreenTarget) &&
                    !TerrariaInputCompat.TryCaptureMouseTargetState(__instance, out mouseRestoreState))
                {
                    TerrariaInputCompat.TryRestoreUseItemInputState(__instance, restoreState);
                    ItemUseBridge.Fail(context.RequestId, "Failed to capture mouse target state: " + TerrariaInputCompat.LastInputCompatError);
                    return;
                }

                if (!TerrariaInputCompat.TryApplyUseItemOverrideForItemCheck(__instance, context))
                {
                    if (mouseRestoreState != null)
                    {
                        TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                    }

                    TerrariaInputCompat.TryRestoreUseItemInputState(__instance, restoreState);
                    ItemUseBridge.Fail(context.RequestId, "Failed to apply ItemCheck input override: " + TerrariaInputCompat.LastInputCompatError);
                    return;
                }

                __state.BridgeApplied = true;
                __state.RequestId = context.RequestId;
                __state.RestoreState = restoreState;
                __state.BridgeMouseRestoreState = mouseRestoreState;
                __state.Context = context;
                ItemCheckWriterArbiter.RecordApplied(
                    ItemCheckWriterKind.ItemUseBridge,
                    context.RequestId,
                    "press",
                    "itemUseBridgeApplied");

                if (context.AllowCombatAim)
                {
                    TryApplyCombatAim(__instance, ref __state, true);
                    TryApplyFlailCachedReleaseAim(__instance, ref __state);
                    TryApplyFlailTakeover(__instance, ref __state);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "itemcheck-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(object __instance, ref ItemUseHookState __state)
        {
            RecordInputProbe(__instance, ref __state);

            if (__state.TravelMenuItemCheckGuardApplied)
            {
                RestoreTravelMenuItemCheckGuard(__state.TravelMenuItemCheckGuard);
                return;
            }

            if (!__state.BridgeApplied)
            {
                // The paired postfix is the restore contract for hook-side
                // scoped writes; do not move these restores out of this phase.
                if (__state.FlailComboTakeoverApplied)
                {
                    RestoreFlailComboTakeover(__state.FlailComboTakeover);
                }

                if (__state.AutoClickerTakeoverApplied)
                {
                    RestoreAutoClickerTakeover(__state.AutoClickerTakeover);
                }

                if (__state.PerfectRevolverTakeoverApplied)
                {
                    RestorePerfectRevolverTakeover(__state.PerfectRevolverTakeover);
                }

                if (__state.FlailTakeoverApplied)
                {
                    RestoreFlailTakeover(__state.FlailTakeover);
                }

                if (__state.AimApplied)
                {
                    RestoreCombatAim(__state.AimDecision, __state.AimRestoreState);
                }

                if (__state.PulseApplied)
                {
                    RestorePulseInput(__instance, __state.PulseRestoreState);
                }

                if (__state.PhasebladeQuickSwitchApplied)
                {
                    RestorePhasebladeQuickSwitchUse(
                        __instance,
                        __state.PhasebladeQuickSwitchRequestId,
                        __state.PhasebladeQuickSwitchRestoreState,
                        __state.PhasebladeQuickSwitchPressed);
                }

                if (__state.AutoMiningSustainedUseApplied)
                {
                    RestoreAutoMiningSustainedUse(__instance, __state.AutoMiningSustainedUseRestoreState, __state.AutoMiningSustainedUseMouseRestoreState);
                }

                if (__state.AutoHarvestSustainedUseApplied)
                {
                    RestoreAutoHarvestSustainedUse(__instance, __state.AutoHarvestSustainedUseRestoreState, __state.AutoHarvestSustainedUseMouseRestoreState);
                }

                if (__state.AutoCaptureCritterSustainedUseApplied)
                {
                    RestoreAutoCaptureCritterSustainedUse(
                        __instance,
                        __state.AutoCaptureCritterSustainedUseRestoreState,
                        __state.AutoCaptureCritterSustainedUseMouseRestoreState,
                        __state.AutoCaptureCritterSustainedUseRestoreSlot,
                        __state.AutoCaptureCritterSustainedUseDirectionCaptured,
                        __state.AutoCaptureCritterSustainedUseOriginalDirection);
                }

                if (__state.FollowUseObservation != null)
                {
                    AutoBuffFollowService.CompleteManualItemUse(__instance, __state.FollowUseObservation);
                }

                return;
            }

            try
            {
                try
                {
                    if (__state.AutoClickerTakeoverApplied)
                    {
                        RestoreAutoClickerTakeover(__state.AutoClickerTakeover);
                    }

                    if (__state.FlailComboTakeoverApplied)
                    {
                        RestoreFlailComboTakeover(__state.FlailComboTakeover);
                    }

                    if (__state.PerfectRevolverTakeoverApplied)
                    {
                        RestorePerfectRevolverTakeover(__state.PerfectRevolverTakeover);
                    }

                    if (__state.FlailTakeoverApplied)
                    {
                        RestoreFlailTakeover(__state.FlailTakeover);
                    }

                    var restoreSlot = __state.Context == null ? -1 : __state.Context.RestoreSelectedSlot;
                    TerrariaInputCompat.TryRestoreUseItemInputState(__instance, __state.RestoreState, restoreSlot);
                }
                catch (Exception restoreError)
                {
                    RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.Restore", restoreError);
                    LogThrottle.ErrorThrottled(
                        "itemcheck-restore-failed",
                        TimeSpan.FromSeconds(10),
                        "ItemUseHookCallbacks",
                        "ItemCheck input restore failed; exception swallowed.", restoreError);
                }

                // Notify the bridge only after input and slot restore has run;
                // terminal results must not race ahead of cleanup.
                ItemUseBridge.NotifyItemCheckFinished(__instance, __state.RequestId);

                if (__state.AimApplied)
                {
                    RestoreCombatAim(__state.AimDecision, __state.AimRestoreState);
                }

                if (__state.BridgeMouseRestoreState != null)
                {
                    RestoreBridgeMouseTarget(__state.BridgeMouseRestoreState);
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.Postfix", error);
                ItemUseBridge.Fail(__state.RequestId, "ItemCheck postfix notify failed: " + error.Message);
                LogThrottle.ErrorThrottled(
                    "itemcheck-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck postfix failed; exception swallowed.", error);
            }
        }

        internal static bool ShouldAttemptAutoClickerTakeoverForTesting(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied)
        {
            return ShouldAttemptAutoClickerTakeover(
                bridgePendingAtStart,
                bridgePendingNow,
                pulseApplied,
                autoMiningApplied,
                autoHarvestApplied,
                autoCaptureApplied);
        }

        internal static bool ShouldAttemptFlailComboTakeoverForTesting(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied)
        {
            return ShouldAttemptFlailComboTakeover(
                bridgePendingAtStart,
                bridgePendingNow,
                pulseApplied,
                autoMiningApplied,
                autoHarvestApplied,
                autoCaptureApplied);
        }

        private static void RecordInputProbe(object player, ref ItemUseHookState state)
        {
            try
            {
                CombatAutoClickerItemCheckInputProbe.RecordPostfix(
                    player,
                    state.InputProbeBefore,
                    state.BridgePendingAtStart,
                    state.BridgeApplied,
                    state.BridgeApplied ? state.RequestId : Guid.Empty,
                    state.Context == null ? string.Empty : state.Context.SourceFeatureId,
                    state.PulseApplied,
                    state.PulsePressed,
                    state.PulseRequestId,
                    state.AutoMiningSustainedUseApplied,
                    state.AutoMiningSustainedUseRequestId,
                    state.AutoHarvestSustainedUseApplied,
                    state.AutoHarvestSustainedUseRequestId,
                    state.AutoCaptureCritterSustainedUseApplied,
                    state.AutoCaptureCritterSustainedUseRequestId,
                    state.AutoClickerTakeoverApplied,
                    state.PerfectRevolverTakeoverApplied,
                    state.FlailTakeoverApplied,
                    state.TravelMenuItemCheckGuardApplied,
                    state.AimApplied);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.InputProbe", error);
                LogThrottle.ErrorThrottled(
                    "itemcheck-input-probe-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck input probe failed; exception swallowed.", error);
            }
        }

        private static bool TryApplyTravelMenuItemCheckGuard(object player, ref ItemUseHookState state)
        {
            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            string message;
            if (!TravelMenuService.TryBeginCreativeUiItemCheckGuard(player, out takeover, out message))
            {
                return false;
            }

            state.TravelMenuItemCheckGuardApplied = true;
            state.TravelMenuItemCheckGuard = takeover;
            ItemCheckWriterArbiter.RecordApplied(
                ItemCheckWriterKind.TravelMenuGuard,
                Guid.Empty,
                "guard",
                string.IsNullOrWhiteSpace(message) ? "travelMenuGuard" : message);
            return true;
        }

        private static void RestoreTravelMenuItemCheckGuard(TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            try
            {
                string message;
                if (!TravelMenuCompat.TryRestoreScopedCreativeUiWorldItemUseGuard(takeover, out message))
                {
                    LogThrottle.WarnThrottled(
                        "travel-menu-itemcheck-guard-restore-failed",
                        TimeSpan.FromSeconds(5),
                        "ItemUseHookCallbacks",
                        message);
                }
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.TravelMenuItemCheckGuardRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "travel-menu-itemcheck-guard-restore-exception",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Travel menu ItemCheck guard restore failed; exception swallowed.", restoreError);
            }
        }

        private static bool TryApplyPerfectRevolverTakeover(object player, ref ItemUseHookState state)
        {
            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatPerfectRevolverService.TryBeginItemCheckTakeover(player, out takeover))
            {
                return false;
            }

            state.PerfectRevolverTakeoverApplied = true;
            state.PerfectRevolverTakeover = takeover;
            return true;
        }

        private static bool TryApplyAutoClickerTakeover(object player, ref ItemUseHookState state)
        {
            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatItemCheckAutoClickService.TryBeginItemCheckTakeover(player, out takeover))
            {
                return false;
            }

            state.AutoClickerTakeoverApplied = true;
            state.AutoClickerTakeover = takeover;
            return true;
        }

        private static bool TryApplyFlailComboTakeover(object player, ref ItemUseHookState state)
        {
            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (!CombatFlailComboService.TryBeginItemCheckTakeover(player, out takeover))
            {
                return false;
            }

            state.FlailComboTakeoverApplied = true;
            state.FlailComboTakeover = takeover;
            return true;
        }

        private static bool ShouldAttemptAutoClickerTakeover(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied)
        {
            ItemCheckWriterDecision decision;
            return !ItemCheckWriterArbiter.IsBlockedByActiveOwner(
                ItemCheckWriterKind.CombatItemCheckAutoClicker,
                new ItemCheckWriterArbiterContext
                {
                    BridgePendingAtStart = bridgePendingAtStart,
                    BridgePendingNow = bridgePendingNow,
                    UseItemPulseActive = pulseApplied,
                    AutoMiningActive = autoMiningApplied,
                    AutoHarvestActive = autoHarvestApplied,
                    AutoCaptureCritterActive = autoCaptureApplied,
                    PhasebladeQuickSwitchActive = PhasebladeQuickSwitchBridge.HasActiveUse
                },
                out decision);
        }

        private static bool ShouldAttemptFlailComboTakeover(
            bool bridgePendingAtStart,
            bool bridgePendingNow,
            bool pulseApplied,
            bool autoMiningApplied,
            bool autoHarvestApplied,
            bool autoCaptureApplied)
        {
            ItemCheckWriterDecision decision;
            return !ItemCheckWriterArbiter.IsBlockedByActiveOwner(
                ItemCheckWriterKind.CombatFlailCombo,
                new ItemCheckWriterArbiterContext
                {
                    BridgePendingAtStart = bridgePendingAtStart,
                    BridgePendingNow = bridgePendingNow,
                    UseItemPulseActive = pulseApplied,
                    AutoMiningActive = autoMiningApplied,
                    AutoHarvestActive = autoHarvestApplied,
                    AutoCaptureCritterActive = autoCaptureApplied,
                    PhasebladeQuickSwitchActive = PhasebladeQuickSwitchBridge.HasActiveUse
                },
                out decision);
        }

        private static bool TryApplyFlailTakeover(object player, ref ItemUseHookState state)
        {
            if (!state.AimApplied || state.AimDecision == null || state.FlailTakeoverApplied)
            {
                return false;
            }

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (CombatAimFlailControlService.TryBeginItemCheckTakeover(player, state.AimDecision, out takeover))
            {
                state.FlailTakeoverApplied = true;
                state.FlailTakeover = takeover;
                return true;
            }

            return false;
        }

        private static ItemCheckWriterArbiterContext BuildItemCheckWriterContext(bool bridgePendingAtStart)
        {
            return new ItemCheckWriterArbiterContext
            {
                BridgePendingAtStart = bridgePendingAtStart,
                BridgePendingNow = ItemUseBridge.PendingRequestId != Guid.Empty,
                UseItemPulseActive = UseItemPulseBridge.HasActivePulse,
                AutoMiningActive = AutoMiningSustainedUseBridge.HasActiveUse,
                AutoCaptureCritterActive = AutoCaptureCritterSustainedUseBridge.HasActiveUse,
                AutoHarvestActive = AutoHarvestSustainedUseBridge.HasActiveUse,
                PhasebladeQuickSwitchActive = PhasebladeQuickSwitchBridge.HasActiveUse
            };
        }

        private static bool TryApplyArbitratedActiveWriter(
            object player,
            ref ItemUseHookState state,
            ItemCheckWriterDecision decision)
        {
            if (decision == null || decision.Owner == ItemCheckWriterKind.None)
            {
                return false;
            }

            if (decision.Owner == ItemCheckWriterKind.ItemUseBridge)
            {
                CombatItemCheckAutoClickService.RecordExternalSkip(decision.Reason);
                return true;
            }

            if (decision.Owner == ItemCheckWriterKind.AutoMiningSustainedUse)
            {
                AutoMiningSustainedUseApplyResult autoMiningUse;
                if (AutoMiningSustainedUseBridge.TryApplyItemCheckUse(player, out autoMiningUse) && autoMiningUse != null)
                {
                    state.FollowUseObservation = null;
                    state.AutoMiningSustainedUseApplied = true;
                    state.AutoMiningSustainedUseRequestId = autoMiningUse.RequestId;
                    state.AutoMiningSustainedUseRestoreState = autoMiningUse.RestoreState;
                    state.AutoMiningSustainedUseMouseRestoreState = autoMiningUse.MouseRestoreState;
                    ItemCheckWriterArbiter.RecordApplied(
                        ItemCheckWriterKind.AutoMiningSustainedUse,
                        autoMiningUse.RequestId,
                        "sustainedUse",
                        decision.Reason,
                        decision.BlockedCandidatesSummary);
                }

                CombatItemCheckAutoClickService.RecordExternalSkip(decision.Reason);
                return true;
            }

            if (decision.Owner == ItemCheckWriterKind.AutoCaptureCritterSustainedUse)
            {
                AutoCaptureCritterSustainedUseApplyResult autoCaptureUse;
                if (AutoCaptureCritterSustainedUseBridge.TryApplyItemCheckUse(player, out autoCaptureUse) && autoCaptureUse != null)
                {
                    state.FollowUseObservation = null;
                    state.AutoCaptureCritterSustainedUseApplied = true;
                    state.AutoCaptureCritterSustainedUseRequestId = autoCaptureUse.RequestId;
                    state.AutoCaptureCritterSustainedUseRestoreState = autoCaptureUse.RestoreState;
                    state.AutoCaptureCritterSustainedUseMouseRestoreState = autoCaptureUse.MouseRestoreState;
                    state.AutoCaptureCritterSustainedUseRestoreSlot = autoCaptureUse.RestoreSelectedSlot;
                    state.AutoCaptureCritterSustainedUseDirectionCaptured = autoCaptureUse.DirectionCaptured;
                    state.AutoCaptureCritterSustainedUseOriginalDirection = autoCaptureUse.OriginalDirection;
                    ItemCheckWriterArbiter.RecordApplied(
                        ItemCheckWriterKind.AutoCaptureCritterSustainedUse,
                        autoCaptureUse.RequestId,
                        "sustainedUse",
                        decision.Reason,
                        decision.BlockedCandidatesSummary);
                }

                CombatItemCheckAutoClickService.RecordExternalSkip(decision.Reason);
                return true;
            }

            if (decision.Owner == ItemCheckWriterKind.AutoHarvestSustainedUse)
            {
                AutoHarvestSustainedUseApplyResult autoHarvestUse;
                if (AutoHarvestSustainedUseBridge.TryApplyItemCheckUse(player, out autoHarvestUse) && autoHarvestUse != null)
                {
                    state.FollowUseObservation = null;
                    state.AutoHarvestSustainedUseApplied = true;
                    state.AutoHarvestSustainedUseRequestId = autoHarvestUse.RequestId;
                    state.AutoHarvestSustainedUseRestoreState = autoHarvestUse.RestoreState;
                    state.AutoHarvestSustainedUseMouseRestoreState = autoHarvestUse.MouseRestoreState;
                    ItemCheckWriterArbiter.RecordApplied(
                        ItemCheckWriterKind.AutoHarvestSustainedUse,
                        autoHarvestUse.RequestId,
                        "sustainedUse",
                        decision.Reason,
                        decision.BlockedCandidatesSummary);
                }

                CombatItemCheckAutoClickService.RecordExternalSkip(decision.Reason);
                return true;
            }

            if (decision.Owner == ItemCheckWriterKind.CombatPhasebladeQuickSwitch)
            {
                PhasebladeQuickSwitchApplyResult phasebladeUse;
                if (PhasebladeQuickSwitchBridge.TryApplyItemCheckUse(player, out phasebladeUse) && phasebladeUse != null)
                {
                    state.FollowUseObservation = null;
                    state.PhasebladeQuickSwitchApplied = true;
                    state.PhasebladeQuickSwitchRequestId = phasebladeUse.RequestId;
                    state.PhasebladeQuickSwitchPressed = phasebladeUse.Pressed;
                    state.PhasebladeQuickSwitchReleased = phasebladeUse.Released;
                    state.PhasebladeQuickSwitchAllowCombatAim = phasebladeUse.AllowCombatAim;
                    state.PhasebladeQuickSwitchRestoreState = phasebladeUse.RestoreState;
                    ItemCheckWriterArbiter.RecordApplied(
                        ItemCheckWriterKind.CombatPhasebladeQuickSwitch,
                        phasebladeUse.RequestId,
                        phasebladeUse.Pressed ? "press" : "release",
                        decision.Reason,
                        decision.BlockedCandidatesSummary);

                    // Phaseblade quick switch reuses the normal ItemCheck aim
                    // path on both press and release ticks; release-on-use
                    // swords need the existing release-hold aim edge intact.
                    if (phasebladeUse.AllowCombatAim && (phasebladeUse.Pressed || phasebladeUse.Released))
                    {
                        TryApplyCombatAim(player, ref state, false);
                    }
                }

                CombatItemCheckAutoClickService.RecordExternalSkip(decision.Reason);
                return true;
            }

            if (decision.Owner == ItemCheckWriterKind.UseItemPulseBridge)
            {
                UseItemPulseApplyResult pulse;
                if (UseItemPulseBridge.TryApplyItemCheckPulse(player, out pulse) && pulse != null)
                {
                    state.PulseApplied = true;
                    state.PulseRequestId = pulse.RequestId;
                    state.PulsePressed = pulse.Pressed;
                    state.PulseAllowCombatAim = pulse.AllowCombatAim;
                    state.PulseRestoreState = pulse.RestoreState;
                    ItemCheckWriterArbiter.RecordApplied(
                        ItemCheckWriterKind.UseItemPulseBridge,
                        pulse.RequestId,
                        pulse.Pressed ? "press" : "release",
                        decision.Reason,
                        decision.BlockedCandidatesSummary);

                    if (pulse.Pressed && pulse.AllowCombatAim)
                    {
                        TryApplyCombatAim(player, ref state, false);
                        TryApplyFlailCachedReleaseAim(player, ref state);
                        TryApplyFlailTakeover(player, ref state);
                    }
                }

                return true;
            }

            return false;
        }

        private static void TryApplyCombatAim(object player, ref ItemUseHookState state, bool allowItemUseBridgePending)
        {
            CombatAimItemCheckDecision decision;
            if (!CombatAimItemCheckService.TryCreateAimDecision(player, allowItemUseBridgePending, out decision))
            {
                return;
            }

            TryApplyCombatAimDecision(ref state, decision);
        }

        private static void TryApplyFlailCachedReleaseAim(object player, ref ItemUseHookState state)
        {
            if (state.AimApplied)
            {
                return;
            }

            CombatAimItemCheckDecision decision;
            if (!CombatAimFlailControlService.TryCreateCachedReleaseDecision(player, out decision))
            {
                return;
            }

            TryApplyCombatAimDecision(ref state, decision);
        }

        private static void TryRememberFlailComboReleaseTail(ref ItemUseHookState state)
        {
            if (state.FlailComboTakeover == null ||
                state.FlailComboTakeover.Pressed)
            {
                return;
            }

            if (state.AimApplied &&
                state.AimDecision != null &&
                CombatAimFlailControlService.TryRememberExistingItemCheckReleaseTail(state.AimDecision, "FlailComboItemCheck"))
            {
                return;
            }

            CombatAimFlailControlService.TryRememberFlailComboPressReleaseTail("FlailComboItemCheck");
        }

        private static void TryRememberFlailComboPressAim(ref ItemUseHookState state)
        {
            if (state.FlailComboTakeover == null ||
                !state.FlailComboTakeover.Pressed ||
                !state.AimApplied ||
                state.AimDecision == null)
            {
                return;
            }

            CombatAimFlailControlService.TryRememberFlailComboPressAim(state.AimDecision);
        }

        private static void TryApplyCombatAimDecision(ref ItemUseHookState state, CombatAimItemCheckDecision decision)
        {
            if (decision == null)
            {
                return;
            }

            MouseTargetInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(out restoreState))
            {
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat ItemCheck aim failed to capture mouse state: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    false);
                return;
            }

            if (!TerrariaInputCompat.TrySetMouseWorldPosition(decision.AimWorldX, decision.AimWorldY))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat ItemCheck aim failed to apply mouse target: " + TerrariaInputCompat.LastInputCompatError,
                    false,
                    true);
                return;
            }

            CombatAimPersistentCursorService.RememberSpecialProjectileTail(decision);
            state.AimApplied = true;
            state.AimRestoreState = restoreState;
            state.AimDecision = decision;
        }

        private static void RestoreCombatAim(CombatAimItemCheckDecision decision, MouseTargetInputState restoreState)
        {
            try
            {
                var restored = TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    restored ? "Applied" : "AttemptedButUnverified",
                    restored ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.AttemptedButUnverified,
                    restored
                        ? "Combat ItemCheck aim temporarily targeted the selected NPC and restored mouse state."
                        : "Combat ItemCheck aim applied, but mouse state restore was not fully verified: " + TerrariaInputCompat.LastInputCompatError,
                    true,
                    restored);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.AimRestore", restoreError);
                CombatAimItemCheckService.RecordItemCheckAim(
                    decision,
                    "Failed",
                    DiagnosticResultCode.Failed,
                    "Combat ItemCheck aim restore failed: " + restoreError.Message,
                    true,
                    false);
                LogThrottle.ErrorThrottled(
                    "itemcheck-aim-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Combat ItemCheck aim restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestorePulseInput(object player, UseItemInputState restoreState)
        {
            try
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.PulseRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-pulse-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck pulse input restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestorePhasebladeQuickSwitchUse(object player, Guid requestId, UseItemInputState restoreState, bool pressed)
        {
            try
            {
                var restored = TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                var postItemCheckStateApplied = TerrariaInputCompat.TryApplyPhasebladeQuickSwitchPostItemCheckState(player, pressed);
                PhasebladeQuickSwitchBridge.RecordRestoreStatus(requestId, restored && postItemCheckStateApplied);
            }
            catch (Exception restoreError)
            {
                PhasebladeQuickSwitchBridge.RecordRestoreStatus(requestId, false);
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.PhasebladeQuickSwitchRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-phaseblade-quick-switch-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Phaseblade quick switch ItemCheck input restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreAutoMiningSustainedUse(object player, UseItemInputState restoreState, MouseTargetInputState mouseRestoreState)
        {
            try
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.AutoMiningSustainedUseRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-auto-mining-sustained-use-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Auto mining ItemCheck input restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreAutoHarvestSustainedUse(object player, UseItemInputState restoreState, MouseTargetInputState mouseRestoreState)
        {
            try
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.AutoHarvestSustainedUseRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-auto-harvest-sustained-use-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Auto harvest ItemCheck input restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreAutoCaptureCritterSustainedUse(
            object player,
            UseItemInputState restoreState,
            MouseTargetInputState mouseRestoreState,
            int restoreSelectedSlot,
            bool directionCaptured,
            int originalDirection)
        {
            try
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState, restoreSelectedSlot);
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                if (directionCaptured && originalDirection != 0)
                {
                    int beforeDirection;
                    int afterDirection;
                    string method;
                    TerrariaInputCompat.TryChangePlayerDirection(player, originalDirection, false, out beforeDirection, out afterDirection, out method);
                }
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.AutoCaptureCritterSustainedUseRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-auto-capture-critter-sustained-use-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "Auto capture critter ItemCheck input restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreBridgeMouseTarget(MouseTargetInputState restoreState)
        {
            try
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(restoreState);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.MouseRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-mouse-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck mouse target restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestorePerfectRevolverTakeover(TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            try
            {
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.PerfectRevolverTakeoverRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-perfect-revolver-takeover-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck perfect revolver takeover restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreAutoClickerTakeover(TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            try
            {
                var restored = TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
                CombatItemCheckAutoClickService.RecordRestoreStatus(restored);
            }
            catch (Exception restoreError)
            {
                CombatItemCheckAutoClickService.RecordRestoreStatus(false);
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.AutoClickerTakeoverRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-auto-clicker-takeover-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck auto clicker takeover restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreFlailComboTakeover(TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            try
            {
                var restored = TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
                CombatFlailComboService.RecordRestoreStatus(restored);
            }
            catch (Exception restoreError)
            {
                CombatFlailComboService.RecordRestoreStatus(false);
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.FlailComboTakeoverRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-flail-combo-takeover-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck flail combo takeover restore failed; exception swallowed.", restoreError);
            }
        }

        private static void RestoreFlailTakeover(TerrariaInputCompat.ScopedUseItemTakeover takeover)
        {
            try
            {
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(takeover);
            }
            catch (Exception restoreError)
            {
                RuntimeDiagnostics.RecordError("ItemUseHookCallbacks.FlailTakeoverRestore", restoreError);
                LogThrottle.ErrorThrottled(
                    "itemcheck-flail-takeover-restore-failed",
                    TimeSpan.FromSeconds(10),
                    "ItemUseHookCallbacks",
                    "ItemCheck flail takeover restore failed; exception swallowed.", restoreError);
            }
        }
    }
}
