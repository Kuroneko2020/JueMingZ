using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Actions
{
    public static class AutoHarvestSustainedUseBridge
    {
        // Services may refresh the target, but this bridge owns the scoped ItemCheck
        // input write and the restore state for the active request.
        private const int TargetStaleMilliseconds = 250;
        private static readonly object SyncRoot = new object();
        private static AutoHarvestSustainedUseState _active;
        private static AutoHarvestSustainedUseTarget _desiredTarget;
        private static AutoHarvestSustainedUseBridgeSnapshot _lastSnapshot = AutoHarvestSustainedUseBridgeSnapshot.None;

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

        public static void SetDesiredTarget(AutoHarvestSustainedUseTarget target)
        {
            lock (SyncRoot)
            {
                _desiredTarget = target == null ? null : target.Clone();
                if (_active != null && !_active.Finished && _desiredTarget != null)
                {
                    _active.TargetRefreshCount++;
                    _active.Message = "Auto harvest sustained target refreshed.";
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
                        ? "Auto harvest sustained target cleared."
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
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "AutoHarvestSustainedUseBridge request id is empty.";
                return false;
            }

            AutoHarvestSustainedUseTarget target;
            lock (SyncRoot)
            {
                if (_active != null && !_active.Finished && _active.RequestId != requestId)
                {
                    message = "AutoHarvestSustainedUseBridge is busy with request " + _active.RequestId;
                    return false;
                }

                target = _desiredTarget == null ? null : _desiredTarget.Clone();
                if (target == null || !target.IsValid)
                {
                    message = "Auto harvest sustained use did not start: no target.";
                    return false;
                }

                _active = new AutoHarvestSustainedUseState
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    Scenario = scenario ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : timeout,
                    Status = InputActionStatus.Running,
                    ResultCode = DiagnosticResultCode.Queued,
                    Message = "Auto harvest sustained use started.",
                    LastTarget = target,
                    LastAppliedTick = long.MinValue
                };
                _lastSnapshot = BuildSnapshotLocked(_active);
            }

            message = "AutoHarvestSustainedUseBridge started: request=" + requestId +
                      ", slot=" + target.ToolSlot.ToString(CultureInfo.InvariantCulture) +
                      ", itemType=" + target.ToolItemType.ToString(CultureInfo.InvariantCulture) +
                      ", tile=" + target.TileX.ToString(CultureInfo.InvariantCulture) +
                      "," + target.TileY.ToString(CultureInfo.InvariantCulture);
            Logger.Info("AutoHarvestSustainedUseBridge", message);
            return true;
        }

        public static AutoHarvestSustainedUseBridgeSnapshot Update(Guid requestId, bool worldInputBlocked, string blockReason)
        {
            AutoHarvestSustainedUseState state;
            AutoHarvestSustainedUseTarget target;
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
                    string.IsNullOrWhiteSpace(blockReason) ? "Auto harvest sustained use stopped: world input is blocked." : blockReason);
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.TimedOut,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.TimedOut,
                    state.ApplyCount > 0
                        ? "Auto harvest sustained use completed after its short burst window."
                        : "Auto harvest sustained use timed out before ItemCheck applied it.");
            }

            if (target == null || !target.IsValid)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto harvest sustained use stopped: no refreshed target.");
            }

            if ((DateTime.UtcNow - target.UpdatedUtc).TotalMilliseconds > TargetStaleMilliseconds)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto harvest sustained use stopped: target was not refreshed.");
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.LastTarget = target.Clone();
                    _active.Message = "Auto harvest sustained use running.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return _lastSnapshot;
        }

        public static bool TryApplyItemCheckUse(object player, out AutoHarvestSustainedUseApplyResult result)
        {
            result = null;
            AutoHarvestSustainedUseState state;
            AutoHarvestSustainedUseTarget target;
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
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto harvest sustained use skipped ItemCheck: target was stale.");
                return false;
            }

            bool held;
            if (TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out held) && held)
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto harvest sustained use stopped because the player is using an item.");
                return false;
            }

            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, target.ToolSlot, out before))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto harvest sustained use failed to read item state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!before.PlayerActive || before.PlayerDead || before.PlayerGhost)
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto harvest sustained use stopped: player unavailable.");
                return false;
            }

            if (before.ItemType != target.ToolItemType || before.ItemStack <= 0)
            {
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto harvest sustained use stopped: regrowth tool changed.");
                return false;
            }

            UseItemInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out restoreState))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto harvest sustained use failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            MouseTargetInputState mouseRestoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out mouseRestoreState))
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto harvest sustained use failed to capture mouse target: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!TerrariaInputCompat.TryApplyAutoHarvestSustainedUseForItemCheck(player, target.ToolSlot, target.WorldX, target.WorldY, target.Direction))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto harvest sustained use failed to apply input override: " + TerrariaInputCompat.LastInputCompatError);
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
                    _active.Message = "Auto harvest sustained ItemCheck input applied.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }

            result = new AutoHarvestSustainedUseApplyResult
            {
                RequestId = state.RequestId,
                RestoreState = restoreState,
                MouseRestoreState = mouseRestoreState
            };
            return true;
        }

        public static void Cancel(Guid requestId, string reason)
        {
            Complete(requestId, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, reason ?? "Auto harvest sustained use cancelled.");
        }

        private static AutoHarvestSustainedUseBridgeSnapshot Complete(Guid requestId, InputActionStatus status, DiagnosticResultCode resultCode, string message)
        {
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
                // Completing releases the active request id before the queue records its
                // terminal result, so later admission sees the bridge as free.
                _active = null;
                Logger.Info("AutoHarvestSustainedUseBridge", "Auto harvest sustained use finished: " + status + " / " + _lastSnapshot.Message);
                return _lastSnapshot;
            }
        }

        private static AutoHarvestSustainedUseBridgeSnapshot BuildSnapshotLocked(AutoHarvestSustainedUseState state)
        {
            if (state == null)
            {
                return AutoHarvestSustainedUseBridgeSnapshot.None;
            }

            var finishedUtc = state.FinishedUtc == default(DateTime) ? DateTime.UtcNow : state.FinishedUtc;
            var target = state.LastTarget;
            return new AutoHarvestSustainedUseBridgeSnapshot
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
                ToolSlot = target == null ? -1 : target.ToolSlot,
                ToolItemType = target == null ? 0 : target.ToolItemType,
                ToolItemName = target == null ? string.Empty : target.ToolItemName ?? string.Empty,
                TileX = target == null ? -1 : target.TileX,
                TileY = target == null ? -1 : target.TileY,
                SeedItemType = target == null ? 0 : target.SeedItemType,
                WorldX = target == null ? 0f : target.WorldX,
                WorldY = target == null ? 0f : target.WorldY
            };
        }

        private sealed class AutoHarvestSustainedUseState
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
            public AutoHarvestSustainedUseTarget LastTarget;
        }
    }

    public sealed class AutoHarvestSustainedUseTarget
    {
        public int ToolSlot { get; set; }
        public int ToolItemType { get; set; }
        public string ToolItemName { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int SeedItemType { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int Direction { get; set; }
        public long UpdatedTick { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public bool IsValid
        {
            get { return ToolSlot >= 0 && ToolItemType > 0 && TileX >= 0 && TileY >= 0; }
        }

        public AutoHarvestSustainedUseTarget Clone()
        {
            return new AutoHarvestSustainedUseTarget
            {
                ToolSlot = ToolSlot,
                ToolItemType = ToolItemType,
                ToolItemName = ToolItemName ?? string.Empty,
                TileX = TileX,
                TileY = TileY,
                SeedItemType = SeedItemType,
                WorldX = WorldX,
                WorldY = WorldY,
                Direction = Direction,
                UpdatedTick = UpdatedTick,
                UpdatedUtc = UpdatedUtc == default(DateTime) ? DateTime.UtcNow : UpdatedUtc
            };
        }
    }

    public sealed class AutoHarvestSustainedUseApplyResult
    {
        public Guid RequestId { get; set; }
        public UseItemInputState RestoreState { get; set; }
        public MouseTargetInputState MouseRestoreState { get; set; }
    }

    public sealed class AutoHarvestSustainedUseBridgeSnapshot
    {
        public static readonly AutoHarvestSustainedUseBridgeSnapshot None = new AutoHarvestSustainedUseBridgeSnapshot
        {
            Status = InputActionStatus.NotApplicable,
            ResultCode = DiagnosticResultCode.NotApplicable,
            Message = string.Empty,
            SourceFeatureId = string.Empty,
            Scenario = string.Empty,
            ToolItemName = string.Empty,
            ToolSlot = -1,
            TileX = -1,
            TileY = -1,
            LastAppliedTick = long.MinValue
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
        public int ToolSlot { get; set; }
        public int ToolItemType { get; set; }
        public string ToolItemName { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int SeedItemType { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }
}
