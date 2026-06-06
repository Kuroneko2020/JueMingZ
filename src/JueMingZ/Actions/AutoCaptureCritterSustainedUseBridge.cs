using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Actions
{
    public static class AutoCaptureCritterSustainedUseBridge
    {
        // Services may refresh the target, but this bridge owns the scoped ItemCheck
        // input write and the restore state for the active request.
        private const int TargetStaleMilliseconds = 300;
        private static readonly object SyncRoot = new object();
        private static AutoCaptureCritterSustainedUseState _active;
        private static AutoCaptureCritterSustainedUseTarget _desiredTarget;
        private static AutoCaptureCritterSustainedUseBridgeSnapshot _lastSnapshot = AutoCaptureCritterSustainedUseBridgeSnapshot.None;

        public static bool HasActiveUse
        {
            get
            {
                lock (SyncRoot)
                {
                    return _active != null && !_active.Finished;
                }
            }
        }

        public static Guid ActiveRequestId
        {
            get
            {
                lock (SyncRoot)
                {
                    return _active == null || _active.Finished ? Guid.Empty : _active.RequestId;
                }
            }
        }

        public static void SetDesiredTarget(AutoCaptureCritterSustainedUseTarget target)
        {
            lock (SyncRoot)
            {
                _desiredTarget = target == null ? null : target.Clone();
                if (_active != null && !_active.Finished && _desiredTarget != null)
                {
                    _active.TargetRefreshCount++;
                    _active.Message = "Auto capture critter sustained target refreshed.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }
        }

        public static void ClearDesiredTarget(string reason)
        {
            lock (SyncRoot)
            {
                _desiredTarget = null;
                if (_active != null && !_active.Finished)
                {
                    _active.Message = string.IsNullOrWhiteSpace(reason)
                        ? "Auto capture critter sustained target cleared."
                        : reason;
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }
        }

        public static bool TryBegin(
            Guid requestId,
            string sourceFeatureId,
            string scenario,
            TimeSpan timeout,
            out string message)
        {
            return TryBegin(
                requestId,
                sourceFeatureId,
                scenario,
                timeout,
                -1,
                false,
                0,
                out message);
        }

        public static bool TryBegin(
            Guid requestId,
            string sourceFeatureId,
            string scenario,
            TimeSpan timeout,
            int originalSelectedSlotOverride,
            bool originalDirectionCapturedOverride,
            int originalDirectionOverride,
            out string message)
        {
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "AutoCaptureCritterSustainedUseBridge request id is empty.";
                return false;
            }

            AutoCaptureCritterSustainedUseTarget target;
            object player = null;
            int originalSelectedSlot = -1;
            int originalDirection = 0;
            var originalDirectionCaptured = false;
            lock (SyncRoot)
            {
                if (_active != null && !_active.Finished && _active.RequestId != requestId)
                {
                    message = "AutoCaptureCritterSustainedUseBridge is busy with request " + _active.RequestId;
                    return false;
                }

                target = _desiredTarget == null ? null : _desiredTarget.Clone();
            }

            if (target == null || !target.IsValid)
            {
                message = "Auto capture critter sustained use did not start: no target.";
                return false;
            }

            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TerrariaInputCompat.TryGetSelectedItem(player, out originalSelectedSlot);
                originalDirectionCaptured = TerrariaInputCompat.TryReadPlayerDirection(player, out originalDirection);
            }

            if (TerrariaInputCompat.IsSupportedItemUseSlot(originalSelectedSlotOverride))
            {
                originalSelectedSlot = originalSelectedSlotOverride;
            }

            if (originalDirectionCapturedOverride && originalDirectionOverride != 0)
            {
                originalDirectionCaptured = true;
                originalDirection = originalDirectionOverride >= 0 ? 1 : -1;
            }

            lock (SyncRoot)
            {
                _active = new AutoCaptureCritterSustainedUseState
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    Scenario = scenario ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : timeout,
                    Status = InputActionStatus.Running,
                    ResultCode = DiagnosticResultCode.Queued,
                    Message = "Auto capture critter sustained use started.",
                    LastTarget = target,
                    LastAppliedTick = long.MinValue,
                    OriginalSelectedSlot = originalSelectedSlot,
                    OriginalDirection = originalDirection,
                    OriginalDirectionCaptured = originalDirectionCaptured,
                    RestoreOriginalStateOnComplete = target.RestoreOriginalStateOnComplete
                };
                _lastSnapshot = BuildSnapshotLocked(_active);
            }

            message = "AutoCaptureCritterSustainedUseBridge started: request=" + requestId +
                      ", slot=" + target.BugNetSlot.ToString(CultureInfo.InvariantCulture) +
                      ", itemType=" + target.BugNetItemType.ToString(CultureInfo.InvariantCulture) +
                      ", npc=" + target.NpcIndex.ToString(CultureInfo.InvariantCulture) +
                      ", type=" + target.NpcType.ToString(CultureInfo.InvariantCulture);
            Logger.Info("AutoCaptureCritterSustainedUseBridge", message);
            return true;
        }

        public static AutoCaptureCritterSustainedUseBridgeSnapshot Update(Guid requestId, bool worldInputBlocked, string blockReason)
        {
            AutoCaptureCritterSustainedUseState state;
            AutoCaptureCritterSustainedUseTarget target;
            lock (SyncRoot)
            {
                state = _active;
                if (state == null || state.RequestId != requestId || state.Finished)
                {
                    return _lastSnapshot;
                }

                target = _desiredTarget == null ? null : _desiredTarget.Clone();
            }

            if (worldInputBlocked)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.BlockedByUi,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.BlockedByUi,
                    string.IsNullOrWhiteSpace(blockReason) ? "Auto capture critter sustained use stopped: world input is blocked." : blockReason);
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.TimedOut,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.TimedOut,
                    state.ApplyCount > 0
                        ? "Auto capture critter sustained use completed after its burst window."
                        : "Auto capture critter sustained use timed out before ItemCheck applied it.");
            }

            if (target == null || !target.IsValid)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto capture critter sustained use stopped: no refreshed target.");
            }

            if ((DateTime.UtcNow - target.UpdatedUtc).TotalMilliseconds > TargetStaleMilliseconds)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto capture critter sustained use stopped: target was not refreshed.");
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.LastTarget = target.Clone();
                    _active.Message = "Auto capture critter sustained use running.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return _lastSnapshot;
        }

        public static bool TryApplyItemCheckUse(object player, out AutoCaptureCritterSustainedUseApplyResult result)
        {
            result = null;
            AutoCaptureCritterSustainedUseState state;
            AutoCaptureCritterSustainedUseTarget target;
            lock (SyncRoot)
            {
                state = _active;
                target = _desiredTarget == null ? null : _desiredTarget.Clone();
            }

            if (state == null || state.Finished || target == null || !target.IsValid)
            {
                return false;
            }

            // Apply only inside ItemCheck after capturing use input and mouse target;
            // the caller must restore the returned states after the hook finishes.
            if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            if ((DateTime.UtcNow - target.UpdatedUtc).TotalMilliseconds > TargetStaleMilliseconds)
            {
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto capture critter sustained use skipped ItemCheck: target was stale.");
                return false;
            }

            bool held;
            if (TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out held) && held)
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto capture critter sustained use stopped because the player is using an item.");
                return false;
            }

            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, target.BugNetSlot, out before))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to read item state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!before.PlayerActive || before.PlayerDead || before.PlayerGhost)
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use stopped: player unavailable.");
                return false;
            }

            if (before.ItemType != target.BugNetItemType || before.ItemStack <= 0)
            {
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto capture critter sustained use stopped: bug net changed.");
                return false;
            }

            UseItemInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out restoreState))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            MouseTargetInputState mouseRestoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out mouseRestoreState))
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState, target.BugNetSlot);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to capture mouse target: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            int originalDirection;
            var directionCaptured = TerrariaInputCompat.TryReadPlayerDirection(player, out originalDirection);
            if (!TerrariaInputCompat.TryApplyAutoCaptureCritterSustainedUseForItemCheck(player, target.BugNetSlot, target.WorldX, target.WorldY, target.Direction))
            {
                var applyError = TerrariaInputCompat.LastInputCompatError;
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState, target.BugNetSlot);
                if (directionCaptured)
                {
                    int beforeDirection;
                    int afterDirection;
                    string method;
                    TerrariaInputCompat.TryChangePlayerDirection(player, originalDirection, false, out beforeDirection, out afterDirection, out method);
                }

                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto capture critter sustained use failed to apply input override: " + applyError);
                return false;
            }

            long gameUpdateCount;
            if (!TerrariaInputCompat.TryReadGameUpdateCount(out gameUpdateCount))
            {
                gameUpdateCount = target.UpdatedTick;
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == state.RequestId && !_active.Finished)
                {
                    _active.ApplyCount++;
                    _active.LastAppliedTick = gameUpdateCount;
                    _active.LastTarget = target.Clone();
                    _active.Status = InputActionStatus.Running;
                    _active.ResultCode = DiagnosticResultCode.Queued;
                    _active.Message = "Auto capture critter sustained ItemCheck input applied.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }

            result = new AutoCaptureCritterSustainedUseApplyResult
            {
                RequestId = state.RequestId,
                RestoreState = restoreState,
                MouseRestoreState = mouseRestoreState,
                RestoreSelectedSlot = target.BugNetSlot,
                DirectionCaptured = directionCaptured,
                OriginalDirection = originalDirection
            };
            return true;
        }

        public static void Cancel(Guid requestId, string reason)
        {
            Complete(requestId, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, reason ?? "Auto capture critter sustained use cancelled.");
        }

        private static AutoCaptureCritterSustainedUseBridgeSnapshot Complete(Guid requestId, InputActionStatus status, DiagnosticResultCode resultCode, string message)
        {
            AutoCaptureCritterSustainedUseBridgeSnapshot snapshot;
            lock (SyncRoot)
            {
                if (_active == null || _active.RequestId != requestId)
                {
                    return _lastSnapshot;
                }

                _active.Status = status;
                _active.ResultCode = resultCode;
                _active.Message = message ?? string.Empty;
                _active.Finished = true;
                _active.FinishedUtc = DateTime.UtcNow;
                _lastSnapshot = BuildSnapshotLocked(_active);
                snapshot = _lastSnapshot;
                _active = null;
                _desiredTarget = null;
            }

            // Completing releases the active request id before the queue records its
            // terminal result, so later admission sees the bridge as free.
            RestoreOriginalSelectionAndDirection(snapshot);
            Logger.Info("AutoCaptureCritterSustainedUseBridge", "Auto capture critter sustained use finished: " + status + " / " + snapshot.Message);
            return snapshot;
        }

        private static void RestoreOriginalSelectionAndDirection(AutoCaptureCritterSustainedUseBridgeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return;
            }

            if (!snapshot.RestoreOriginalStateOnComplete)
            {
                return;
            }

            if (TerrariaInputCompat.IsSupportedItemUseSlot(snapshot.OriginalSelectedSlot))
            {
                TerrariaInputCompat.TrySelectInventorySlot(player, snapshot.OriginalSelectedSlot);
            }

            if (snapshot.OriginalDirectionCaptured && snapshot.OriginalDirection != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                TerrariaInputCompat.TryChangePlayerDirection(player, snapshot.OriginalDirection, false, out beforeDirection, out afterDirection, out method);
            }
        }

        private static AutoCaptureCritterSustainedUseBridgeSnapshot BuildSnapshotLocked(AutoCaptureCritterSustainedUseState state)
        {
            if (state == null)
            {
                return AutoCaptureCritterSustainedUseBridgeSnapshot.None;
            }

            var finishedUtc = state.FinishedUtc == default(DateTime) ? DateTime.UtcNow : state.FinishedUtc;
            var target = state.LastTarget;
            return new AutoCaptureCritterSustainedUseBridgeSnapshot
            {
                RequestId = state.RequestId,
                SourceFeatureId = state.SourceFeatureId,
                Scenario = state.Scenario,
                Status = state.Status,
                ResultCode = state.ResultCode,
                Message = state.Message ?? string.Empty,
                CreatedUtc = state.CreatedUtc,
                FinishedUtc = state.Finished ? finishedUtc : default(DateTime),
                DurationMs = (long)((state.Finished ? finishedUtc : DateTime.UtcNow) - state.CreatedUtc).TotalMilliseconds,
                ApplyCount = state.ApplyCount,
                TargetRefreshCount = state.TargetRefreshCount,
                LastAppliedTick = state.LastAppliedTick,
                BugNetSlot = target == null ? -1 : target.BugNetSlot,
                BugNetItemType = target == null ? 0 : target.BugNetItemType,
                BugNetItemName = target == null ? string.Empty : target.BugNetItemName ?? string.Empty,
                NpcIndex = target == null ? -1 : target.NpcIndex,
                NpcType = target == null ? 0 : target.NpcType,
                CatchItem = target == null ? 0 : target.CatchItem,
                WorldX = target == null ? 0f : target.WorldX,
                WorldY = target == null ? 0f : target.WorldY,
                Direction = target == null ? 0 : target.Direction,
                RestoreOriginalStateOnComplete = state.RestoreOriginalStateOnComplete,
                OriginalSelectedSlot = state.OriginalSelectedSlot,
                OriginalDirectionCaptured = state.OriginalDirectionCaptured,
                OriginalDirection = state.OriginalDirection
            };
        }

        private sealed class AutoCaptureCritterSustainedUseState
        {
            public Guid RequestId;
            public string SourceFeatureId;
            public string Scenario;
            public DateTime CreatedUtc;
            public DateTime FinishedUtc;
            public TimeSpan Timeout;
            public InputActionStatus Status;
            public DiagnosticResultCode ResultCode;
            public string Message;
            public bool Finished;
            public int ApplyCount;
            public int TargetRefreshCount;
            public long LastAppliedTick;
            public AutoCaptureCritterSustainedUseTarget LastTarget;
            public int OriginalSelectedSlot;
            public bool OriginalDirectionCaptured;
            public int OriginalDirection;
            public bool RestoreOriginalStateOnComplete;
        }
    }

    public sealed class AutoCaptureCritterSustainedUseTarget
    {
        public int BugNetSlot { get; set; }
        public int BugNetItemType { get; set; }
        public string BugNetItemName { get; set; }
        public int NpcIndex { get; set; }
        public int NpcType { get; set; }
        public int CatchItem { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int Direction { get; set; }
        public bool RestoreOriginalStateOnComplete { get; set; }
        public long UpdatedTick { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public bool IsValid
        {
            get { return BugNetSlot >= 0 && BugNetItemType > 0 && NpcIndex >= 0; }
        }

        public AutoCaptureCritterSustainedUseTarget Clone()
        {
            return new AutoCaptureCritterSustainedUseTarget
            {
                BugNetSlot = BugNetSlot,
                BugNetItemType = BugNetItemType,
                BugNetItemName = BugNetItemName ?? string.Empty,
                NpcIndex = NpcIndex,
                NpcType = NpcType,
                CatchItem = CatchItem,
                WorldX = WorldX,
                WorldY = WorldY,
                Direction = Direction,
                RestoreOriginalStateOnComplete = RestoreOriginalStateOnComplete,
                UpdatedTick = UpdatedTick,
                UpdatedUtc = UpdatedUtc == default(DateTime) ? DateTime.UtcNow : UpdatedUtc
            };
        }
    }

    public sealed class AutoCaptureCritterSustainedUseApplyResult
    {
        public Guid RequestId { get; set; }
        public UseItemInputState RestoreState { get; set; }
        public MouseTargetInputState MouseRestoreState { get; set; }
        public int RestoreSelectedSlot { get; set; }
        public bool DirectionCaptured { get; set; }
        public int OriginalDirection { get; set; }
    }

    public sealed class AutoCaptureCritterSustainedUseBridgeSnapshot
    {
        public static readonly AutoCaptureCritterSustainedUseBridgeSnapshot None = new AutoCaptureCritterSustainedUseBridgeSnapshot
        {
            Status = InputActionStatus.NotApplicable,
            ResultCode = DiagnosticResultCode.NotApplicable,
            Message = string.Empty,
            SourceFeatureId = string.Empty,
            Scenario = string.Empty,
            BugNetItemName = string.Empty,
            BugNetSlot = -1,
            NpcIndex = -1,
            LastAppliedTick = long.MinValue,
            OriginalSelectedSlot = -1
        };

        public Guid RequestId { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public InputActionStatus Status { get; set; }
        public DiagnosticResultCode ResultCode { get; set; }
        public string Message { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime FinishedUtc { get; set; }
        public long DurationMs { get; set; }
        public int ApplyCount { get; set; }
        public int TargetRefreshCount { get; set; }
        public long LastAppliedTick { get; set; }
        public int BugNetSlot { get; set; }
        public int BugNetItemType { get; set; }
        public string BugNetItemName { get; set; }
        public int NpcIndex { get; set; }
        public int NpcType { get; set; }
        public int CatchItem { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int Direction { get; set; }
        public bool RestoreOriginalStateOnComplete { get; set; }
        public int OriginalSelectedSlot { get; set; }
        public bool OriginalDirectionCaptured { get; set; }
        public int OriginalDirection { get; set; }
    }
}
