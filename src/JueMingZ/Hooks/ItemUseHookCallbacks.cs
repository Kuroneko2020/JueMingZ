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
        private struct ItemUseHookState
        {
            public bool BridgeApplied;
            public bool AimApplied;
            public bool PulseApplied;
            public bool PulsePressed;
            public bool PulseAllowCombatAim;
            public bool AutoHarvestSustainedUseApplied;
            public bool AutoCaptureCritterSustainedUseApplied;
            public bool PerfectRevolverTakeoverApplied;
            public bool FlailTakeoverApplied;
            public bool TravelMenuItemCheckGuardApplied;
            public bool BridgePendingAtStart;
            public Guid RequestId;
            public Guid PulseRequestId;
            public Guid AutoHarvestSustainedUseRequestId;
            public Guid AutoCaptureCritterSustainedUseRequestId;
            public UseItemInputState RestoreState;
            public UseItemInputState PulseRestoreState;
            public UseItemInputState AutoHarvestSustainedUseRestoreState;
            public UseItemInputState AutoCaptureCritterSustainedUseRestoreState;
            public MouseTargetInputState AimRestoreState;
            public MouseTargetInputState BridgeMouseRestoreState;
            public MouseTargetInputState AutoHarvestSustainedUseMouseRestoreState;
            public MouseTargetInputState AutoCaptureCritterSustainedUseMouseRestoreState;
            public int AutoCaptureCritterSustainedUseRestoreSlot;
            public bool AutoCaptureCritterSustainedUseDirectionCaptured;
            public int AutoCaptureCritterSustainedUseOriginalDirection;
            public TerrariaInputCompat.ScopedUseItemTakeover PerfectRevolverTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover FlailTakeover;
            public TerrariaInputCompat.ScopedUseItemTakeover TravelMenuItemCheckGuard;
            public ItemUseBridgeContext Context;
            public CombatAimItemCheckDecision AimDecision;
            public AutoBuffFollowItemUseObservation FollowUseObservation;
            public ItemCheckInputProbeFrame InputProbeBefore;
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

                if (TryApplyPerfectRevolverTakeover(__instance, ref __state))
                {
                    if (__state.PerfectRevolverTakeoverApplied && __state.PerfectRevolverTakeover != null && __state.PerfectRevolverTakeover.Pressed)
                    {
                        TryApplyCombatAim(__instance, ref __state, false);
                    }

                    return;
                }

                var bridgePendingAtStart = __state.BridgePendingAtStart;
                if (!bridgePendingAtStart)
                {
                    AutoBuffFollowService.TryCaptureManualItemUse(__instance, out __state.FollowUseObservation);
                }

                ItemUseBridgeContext context;
                if (!ItemUseBridge.TryBeginFromItemCheck(__instance, out context))
                {
                    if (bridgePendingAtStart || ItemUseBridge.PendingRequestId != Guid.Empty)
                    {
                        return;
                    }

                    AutoHarvestSustainedUseApplyResult autoHarvestUse;
                    AutoCaptureCritterSustainedUseApplyResult autoCaptureUse;
                    if (AutoCaptureCritterSustainedUseBridge.TryApplyItemCheckUse(__instance, out autoCaptureUse) && autoCaptureUse != null)
                    {
                        __state.FollowUseObservation = null;
                        __state.AutoCaptureCritterSustainedUseApplied = true;
                        __state.AutoCaptureCritterSustainedUseRequestId = autoCaptureUse.RequestId;
                        __state.AutoCaptureCritterSustainedUseRestoreState = autoCaptureUse.RestoreState;
                        __state.AutoCaptureCritterSustainedUseMouseRestoreState = autoCaptureUse.MouseRestoreState;
                        __state.AutoCaptureCritterSustainedUseRestoreSlot = autoCaptureUse.RestoreSelectedSlot;
                        __state.AutoCaptureCritterSustainedUseDirectionCaptured = autoCaptureUse.DirectionCaptured;
                        __state.AutoCaptureCritterSustainedUseOriginalDirection = autoCaptureUse.OriginalDirection;
                        return;
                    }

                    if (AutoHarvestSustainedUseBridge.TryApplyItemCheckUse(__instance, out autoHarvestUse) && autoHarvestUse != null)
                    {
                        __state.FollowUseObservation = null;
                        __state.AutoHarvestSustainedUseApplied = true;
                        __state.AutoHarvestSustainedUseRequestId = autoHarvestUse.RequestId;
                        __state.AutoHarvestSustainedUseRestoreState = autoHarvestUse.RestoreState;
                        __state.AutoHarvestSustainedUseMouseRestoreState = autoHarvestUse.MouseRestoreState;
                        return;
                    }

                    UseItemPulseApplyResult pulse;
                    if (UseItemPulseBridge.TryApplyItemCheckPulse(__instance, out pulse) && pulse != null)
                    {
                        __state.PulseApplied = true;
                        __state.PulseRequestId = pulse.RequestId;
                        __state.PulsePressed = pulse.Pressed;
                        __state.PulseAllowCombatAim = pulse.AllowCombatAim;
                        __state.PulseRestoreState = pulse.RestoreState;
                    }

                    if (__state.PulseApplied && !__state.PulsePressed)
                    {
                        return;
                    }

                    if (!__state.PulseApplied || __state.PulseAllowCombatAim)
                    {
                        TryApplyCombatAim(__instance, ref __state, false);
                        TryApplyFlailCachedReleaseAim(__instance, ref __state);
                        TryApplyFlailTakeover(__instance, ref __state);
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
                    state.AutoHarvestSustainedUseApplied,
                    state.AutoHarvestSustainedUseRequestId,
                    state.AutoCaptureCritterSustainedUseApplied,
                    state.AutoCaptureCritterSustainedUseRequestId,
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

        private static void TryApplyFlailTakeover(object player, ref ItemUseHookState state)
        {
            if (!state.AimApplied || state.AimDecision == null || state.FlailTakeoverApplied)
            {
                return;
            }

            TerrariaInputCompat.ScopedUseItemTakeover takeover;
            if (CombatAimFlailControlService.TryBeginItemCheckTakeover(player, state.AimDecision, out takeover))
            {
                state.FlailTakeoverApplied = true;
                state.FlailTakeover = takeover;
            }
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
