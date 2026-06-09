using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Actions
{
    public static class AutoMiningSustainedUseBridge
    {
        // Auto mining only emulates vanilla held pickaxe use; tile mutation remains owned by Terraria ItemCheck/PickTile.
        private const int TargetStaleMilliseconds = 250;
        private static readonly TimeSpan DefaultDeadManTimeout = TimeSpan.FromMinutes(10);
        private static readonly object SyncRoot = new object();
        private static AutoMiningSustainedUseState _active;
        private static AutoMiningSustainedUseTarget _desiredTarget;
        private static AutoMiningSustainedUseBridgeSnapshot _lastSnapshot = AutoMiningSustainedUseBridgeSnapshot.None;

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

        public static void SetDesiredTarget(AutoMiningSustainedUseTarget target)
        {
            lock (SyncRoot)
            {
                _desiredTarget = target == null ? null : target.Clone();
                if (_active != null && !_active.Finished && _desiredTarget != null)
                {
                    _active.TargetRefreshCount++;
                    _active.Message = "Auto mining sustained target refreshed.";
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
                        ? "Auto mining sustained target cleared."
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
                message = "AutoMiningSustainedUseBridge request id is empty.";
                return false;
            }

            AutoMiningSustainedUseTarget target;
            lock (SyncRoot)
            {
                if (_active != null && !_active.Finished && _active.RequestId != requestId)
                {
                    message = "AutoMiningSustainedUseBridge is busy with request " + _active.RequestId;
                    return false;
                }

                target = _desiredTarget == null ? null : _desiredTarget.Clone();
                if (target == null || !target.IsValid)
                {
                    message = "Auto mining sustained use did not start: no target.";
                    return false;
                }

                _active = new AutoMiningSustainedUseState
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    Scenario = scenario ?? string.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? DefaultDeadManTimeout : timeout,
                    Status = InputActionStatus.Running,
                    ResultCode = DiagnosticResultCode.Queued,
                    Message = "Auto mining sustained use started.",
                    LastTarget = target,
                    LastAppliedTick = long.MinValue
                };
                _lastSnapshot = BuildSnapshotLocked(_active);
            }

            message = "AutoMiningSustainedUseBridge started: request=" + requestId +
                      ", slot=" + target.PickSlot.ToString(CultureInfo.InvariantCulture) +
                      ", itemType=" + target.PickItemType.ToString(CultureInfo.InvariantCulture) +
                      ", tile=" + target.TileX.ToString(CultureInfo.InvariantCulture) +
                      "," + target.TileY.ToString(CultureInfo.InvariantCulture);
            Logger.Info("AutoMiningSustainedUseBridge", message);
            return true;
        }

        public static AutoMiningSustainedUseBridgeSnapshot Update(Guid requestId, bool worldInputBlocked, string blockReason)
        {
            AutoMiningSustainedUseState state;
            AutoMiningSustainedUseTarget target;
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
                    string.IsNullOrWhiteSpace(blockReason) ? "Auto mining sustained use stopped: world input is blocked." : blockReason);
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.TimedOut,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.TimedOut,
                    state.ApplyCount > 0
                        ? "Auto mining sustained use completed after its dead-man timeout."
                        : "Auto mining sustained use timed out before ItemCheck applied it.");
            }

            if (target == null || !target.IsValid)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped: no refreshed target.");
            }

            if ((DateTime.UtcNow - target.UpdatedUtc).TotalMilliseconds > TargetStaleMilliseconds)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped: target was not refreshed.");
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.LastTarget = target.Clone();
                    _active.Message = "Auto mining sustained use running.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return _lastSnapshot;
        }

        public static bool TryApplyItemCheckUse(object player, out AutoMiningSustainedUseApplyResult result)
        {
            result = null;
            AutoMiningSustainedUseState state;
            AutoMiningSustainedUseTarget target;
            lock (SyncRoot)
            {
                state = _active;
                target = _desiredTarget == null ? null : _desiredTarget.Clone();
            }

            if (state == null || state.Finished || target == null || !target.IsValid)
            {
                return false;
            }

            if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            if ((DateTime.UtcNow - target.UpdatedUtc).TotalMilliseconds > TargetStaleMilliseconds)
            {
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto mining sustained use skipped ItemCheck: target was stale.");
                return false;
            }

            bool held;
            if (TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out held) && held)
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped because the player is using an item.");
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use failed to read selected slot: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (selectedSlot != target.PickSlot)
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped: player switched hotbar slot.");
                return false;
            }

            ItemUseVerificationState before;
            if (!TerrariaInputCompat.TryReadItemUseVerificationState(player, target.PickSlot, out before))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use failed to read item state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!before.PlayerActive || before.PlayerDead || before.PlayerGhost)
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use stopped: player unavailable.");
                return false;
            }

            if (before.ItemType != target.PickItemType || before.ItemStack <= 0)
            {
                Complete(state.RequestId, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "Auto mining sustained use stopped: pickaxe changed.");
                return false;
            }

            string targetMessage;
            if (!IsTargetStillMineableForItemCheck(player, target, out targetMessage))
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped: " + targetMessage);
                return false;
            }

            UseItemInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out restoreState))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            MouseTargetInputState mouseRestoreState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out mouseRestoreState))
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use failed to capture mouse target: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!TerrariaInputCompat.TryApplyAutoMiningSustainedUseForItemCheck(player, target.PickSlot, target.WorldX, target.WorldY, target.Direction))
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Auto mining sustained use failed to apply input override: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            int appliedTileX;
            int appliedTileY;
            // ItemCheck will mine Player.tileTargetX/Y, so the scoped mouse override
            // must resolve to the same exact ore before we allow controlUseItem through.
            // This is a sync guard only; reach, pick power and CanKillTile are gated before this point.
            if (!TerrariaInputCompat.TryReadTileTarget(out appliedTileX, out appliedTileY) ||
                appliedTileX != target.TileX ||
                appliedTileY != target.TileY)
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseRestoreState);
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Auto mining sustained use stopped: tile target did not resolve to the selected ore.");
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
                    _active.Message = "Auto mining sustained ItemCheck input applied.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }

            result = new AutoMiningSustainedUseApplyResult
            {
                RequestId = state.RequestId,
                RestoreState = restoreState,
                MouseRestoreState = mouseRestoreState
            };
            return true;
        }

        internal static bool IsTargetStillMineableForTesting(object player, AutoMiningSustainedUseTarget target, out string message)
        {
            return IsTargetStillMineableForItemCheck(player, target, out message);
        }

        internal static bool IsTargetStillMineableForTesting(object player, object tiles, AutoMiningSustainedUseTarget target, out string message)
        {
            return IsTargetStillMineableForItemCheck(player, tiles, target, out message);
        }

        private static bool IsTargetStillMineableForItemCheck(object player, AutoMiningSustainedUseTarget target, out string message)
        {
            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                message = "tile context unavailable: " + message;
                return false;
            }

            return IsTargetStillMineableForItemCheck(player, tiles, target, out message);
        }

        private static bool IsTargetStillMineableForItemCheck(object player, object tiles, AutoMiningSustainedUseTarget target, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "player unavailable.";
                return false;
            }

            if (target == null || !target.IsValid)
            {
                message = "target unavailable.";
                return false;
            }

            bool active;
            int actualType;
            if (!AutoMiningCompat.TryReadTile(tiles, target.TileX, target.TileY, out active, out actualType) || !active)
            {
                message = "target tile is no longer active.";
                return false;
            }

            if (actualType != target.TileType)
            {
                message = "target tile type changed.";
                return false;
            }

            // This is the ItemCheck-side empty-swing guard: before writing any input flag,
            // prove the exact target tile can still be hit by the recorded held pickaxe.
            if (!AutoMiningCompat.CanMineTileWithPickaxe(player, target.TileX, target.TileY, actualType, target.PickPower, target.TileBoost))
            {
                message = "target tile is no longer mineable.";
                return false;
            }

            return true;
        }

        public static void Cancel(Guid requestId, string reason)
        {
            Complete(requestId, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, reason ?? "Auto mining sustained use cancelled.");
        }

        private static AutoMiningSustainedUseBridgeSnapshot Complete(Guid requestId, InputActionStatus status, DiagnosticResultCode resultCode, string message)
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
                _active = null;
                Logger.Info("AutoMiningSustainedUseBridge", "Auto mining sustained use finished: " + status + " / " + _lastSnapshot.Message);
                return _lastSnapshot;
            }
        }

        private static AutoMiningSustainedUseBridgeSnapshot BuildSnapshotLocked(AutoMiningSustainedUseState state)
        {
            if (state == null)
            {
                return AutoMiningSustainedUseBridgeSnapshot.None;
            }

            var finishedUtc = state.FinishedUtc == default(DateTime) ? DateTime.UtcNow : state.FinishedUtc;
            var target = state.LastTarget;
            return new AutoMiningSustainedUseBridgeSnapshot
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
                PickSlot = target == null ? -1 : target.PickSlot,
                PickItemType = target == null ? 0 : target.PickItemType,
                PickItemName = target == null ? string.Empty : target.PickItemName ?? string.Empty,
                TileX = target == null ? -1 : target.TileX,
                TileY = target == null ? -1 : target.TileY,
                TileType = target == null ? 0 : target.TileType,
                SourceMode = target == null ? string.Empty : target.SourceMode ?? string.Empty,
                SourceHotkey = target == null ? string.Empty : target.SourceHotkey ?? string.Empty,
                WorldX = target == null ? 0f : target.WorldX,
                WorldY = target == null ? 0f : target.WorldY
            };
        }

        private sealed class AutoMiningSustainedUseState
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
            public AutoMiningSustainedUseTarget LastTarget;
        }
    }

    public sealed class AutoMiningSustainedUseTarget
    {
        public int PickSlot { get; set; }
        public int PickItemType { get; set; }
        public string PickItemName { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileType { get; set; }
        public int PickPower { get; set; }
        public int TileBoost { get; set; }
        public string SourceMode { get; set; }
        public string SourceHotkey { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
        public int Direction { get; set; }
        public long UpdatedTick { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public bool IsValid
        {
            get { return PickSlot >= 0 && PickItemType > 0 && TileX >= 0 && TileY >= 0; }
        }

        public AutoMiningSustainedUseTarget Clone()
        {
            return new AutoMiningSustainedUseTarget
            {
                PickSlot = PickSlot,
                PickItemType = PickItemType,
                PickItemName = PickItemName ?? string.Empty,
                TileX = TileX,
                TileY = TileY,
                TileType = TileType,
                PickPower = PickPower,
                TileBoost = TileBoost,
                SourceMode = SourceMode ?? string.Empty,
                SourceHotkey = SourceHotkey ?? string.Empty,
                WorldX = WorldX,
                WorldY = WorldY,
                Direction = Direction,
                UpdatedTick = UpdatedTick,
                UpdatedUtc = UpdatedUtc == default(DateTime) ? DateTime.UtcNow : UpdatedUtc
            };
        }
    }

    public sealed class AutoMiningSustainedUseApplyResult
    {
        public Guid RequestId { get; set; }
        public UseItemInputState RestoreState { get; set; }
        public MouseTargetInputState MouseRestoreState { get; set; }
    }

    public sealed class AutoMiningSustainedUseBridgeSnapshot
    {
        public static readonly AutoMiningSustainedUseBridgeSnapshot None = new AutoMiningSustainedUseBridgeSnapshot
        {
            Status = InputActionStatus.NotApplicable,
            ResultCode = DiagnosticResultCode.NotApplicable,
            Message = string.Empty,
            SourceFeatureId = string.Empty,
            Scenario = string.Empty,
            PickItemName = string.Empty,
            PickSlot = -1,
            TileX = -1,
            TileY = -1,
            SourceMode = string.Empty,
            SourceHotkey = string.Empty,
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
        public int PickSlot { get; set; }
        public int PickItemType { get; set; }
        public string PickItemName { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int TileType { get; set; }
        public string SourceMode { get; set; }
        public string SourceHotkey { get; set; }
        public float WorldX { get; set; }
        public float WorldY { get; set; }
    }
}
