using System;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions
{
    public static class UseItemPulseBridge
    {
        // RawInput pulse ownership lives here; this is not a general input override.
        // Each pulse must be visible to channels and restored by the ItemCheck scope.
        private static readonly object SyncRoot = new object();
        private static PulseState _active;
        private static UseItemPulseBridgeSnapshot _lastSnapshot = UseItemPulseBridgeSnapshot.None;

        public static bool HasActivePulse
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

        public static bool TryBegin(
            Guid requestId,
            string sourceFeatureId,
            string scenario,
            int selectedSlot,
            int expectedItemType,
            string itemName,
            int intervalTicks,
            bool requireUseItemHeld,
            bool allowCombatAim,
            TimeSpan timeout,
            out string message)
        {
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "UseItemPulseBridge request id is empty.";
                return false;
            }

            if (selectedSlot < 0 || selectedSlot > 9)
            {
                message = "UseItemPulseBridge only supports hotbar slots 0-9.";
                return false;
            }

            if (expectedItemType <= 0)
            {
                message = "UseItemPulseBridge expected item type is empty.";
                return false;
            }

            var safeInterval = Clamp(intervalTicks <= 0 ? 2 : intervalTicks, 2, 12);
            lock (SyncRoot)
            {
                if (_active != null && !_active.Finished && _active.RequestId != requestId)
                {
                    message = "UseItemPulseBridge is busy with request " + _active.RequestId;
                    return false;
                }

                _active = new PulseState
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    Scenario = scenario ?? string.Empty,
                    SelectedSlot = selectedSlot,
                    ExpectedItemType = expectedItemType,
                    ItemName = itemName ?? string.Empty,
                    IntervalTicks = safeInterval,
                    RequireUseItemHeld = requireUseItemHeld,
                    AllowCombatAim = allowCombatAim,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(15) : timeout,
                    Status = InputActionStatus.Running,
                    ResultCode = DiagnosticResultCode.Queued,
                    Message = "Use item pulse bridge started.",
                    NextPressTick = long.MinValue,
                    LastAppliedTick = long.MinValue
                };

                _lastSnapshot = BuildSnapshotLocked(_active);
            }

            message = "UseItemPulseBridge started: request=" + requestId +
                      ", slot=" + selectedSlot.ToString(CultureInfo.InvariantCulture) +
                      ", itemType=" + expectedItemType.ToString(CultureInfo.InvariantCulture) +
                      ", intervalTicks=" + safeInterval.ToString(CultureInfo.InvariantCulture);
            Logger.Info("UseItemPulseBridge", message);
            return true;
        }

        public static UseItemPulseBridgeSnapshot Update(Guid requestId, bool worldInputBlocked, string blockReason)
        {
            PulseState state;
            lock (SyncRoot)
            {
                state = _active;
                if (state == null || state.RequestId != requestId || state.Finished)
                {
                    return _lastSnapshot;
                }
            }

            if (worldInputBlocked)
            {
                return Complete(requestId, state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.BlockedByUi,
                    state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.BlockedByUi,
                    string.IsNullOrWhiteSpace(blockReason) ? "Use item pulse stopped because world input is blocked." : blockReason);
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                return Complete(requestId, InputActionStatus.TimedOut, DiagnosticResultCode.TimedOut, "Use item pulse timed out.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse stopped: local player unavailable.");
            }

            bool active;
            bool dead;
            bool ghost;
            GameStateReflection.TryGetBool(player, "active", out active);
            GameStateReflection.TryGetBool(player, "dead", out dead);
            GameStateReflection.TryGetBool(player, "ghost", out ghost);
            if (!active || dead || ghost)
            {
                return Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse stopped: player unavailable.");
            }

            bool held;
            if (!TerrariaInputCompat.TryReadUseItemHeld(player, out held))
            {
                return Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse stopped: cannot read held use input.");
            }

            if (state.RequireUseItemHeld && !held)
            {
                return Complete(
                    requestId,
                    state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    state.PressPulseCount > 0
                        ? "Use item pulse stopped after use input was released."
                        : "Use item pulse did not start because use input was released.");
            }

            if (!IsStillSelected(player, state.SelectedSlot, state.ExpectedItemType))
            {
                return Complete(
                    requestId,
                    state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Use item pulse stopped because selected item changed.");
            }

            bool magicString;
            if (GameStateReflection.TryGetBool(player, "magicString", out magicString) && !magicString)
            {
                return Complete(
                    requestId,
                    state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Use item pulse stopped because magicString accessory flag is no longer active.");
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.Message = "Use item pulse running.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return _lastSnapshot;
        }

        public static bool TryApplyItemCheckPulse(object player, out UseItemPulseApplyResult result)
        {
            result = null;
            PulseState state;
            lock (SyncRoot)
            {
                state = _active;
                if (state == null || state.Finished)
                {
                    return false;
                }
            }

            if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            // The actual press/release mutation is scoped to Player.ItemCheck; return
            // the captured state so the hook can restore it after the pulse.
            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                Complete(state.RequestId, InputActionStatus.TimedOut, DiagnosticResultCode.TimedOut, "Use item pulse timed out before ItemCheck.");
                return false;
            }

            bool held;
            if (state.RequireUseItemHeld)
            {
                if (!TerrariaInputCompat.TryReadUseItemHeld(player, out held))
                {
                    Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse cannot read held use input.");
                    return false;
                }

                if (!held)
                {
                    Complete(
                        state.RequestId,
                        state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                        state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                        "Use item pulse stopped before ItemCheck because use input was released.");
                    return false;
                }
            }

            if (!IsStillSelected(player, state.SelectedSlot, state.ExpectedItemType))
            {
                Complete(
                    state.RequestId,
                    state.PressPulseCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.PressPulseCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Use item pulse stopped before ItemCheck because selected item changed.");
                return false;
            }

            UseItemInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out restoreState))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            long gameUpdateCount;
            if (!TerrariaInputCompat.TryReadGameUpdateCount(out gameUpdateCount))
            {
                gameUpdateCount = state.LastAppliedTick == long.MinValue ? 0 : state.LastAppliedTick + 1;
            }

            bool press;
            lock (SyncRoot)
            {
                if (_active == null || _active.RequestId != state.RequestId || _active.Finished)
                {
                    TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                    return false;
                }

                if (_active.NextPressTick == long.MinValue || gameUpdateCount >= _active.NextPressTick)
                {
                    press = true;
                    _active.NextPressTick = gameUpdateCount + _active.IntervalTicks;
                }
                else
                {
                    press = false;
                }
            }

            if (!TerrariaInputCompat.TryApplyUseItemPulseForItemCheck(player, press))
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Use item pulse failed to apply input override: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == state.RequestId && !_active.Finished)
                {
                    _active.ItemCheckCount++;
                    if (press)
                    {
                        _active.PressPulseCount++;
                    }
                    else
                    {
                        _active.ReleasePulseCount++;
                    }

                    _active.LastAppliedPress = press;
                    _active.LastAppliedTick = gameUpdateCount;
                    _active.Message = press ? "Use item pulse applied press tick." : "Use item pulse applied release tick.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }

            result = new UseItemPulseApplyResult
            {
                RequestId = state.RequestId,
                RestoreState = restoreState,
                Pressed = press,
                AllowCombatAim = state.AllowCombatAim
            };
            return true;
        }

        public static void Cancel(Guid requestId, string reason)
        {
            Complete(requestId, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, reason ?? "Use item pulse cancelled.");
        }

        private static UseItemPulseBridgeSnapshot Complete(Guid requestId, InputActionStatus status, DiagnosticResultCode resultCode, string message)
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
                Logger.Info("UseItemPulseBridge", "Use item pulse finished: " + status + " / " + _lastSnapshot.Message);
                return _lastSnapshot;
            }
        }

        private static bool IsStillSelected(object player, int selectedSlot, int expectedItemType)
        {
            int currentSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out currentSlot) || currentSlot != selectedSlot)
            {
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot < 0 || selectedSlot >= inventory.Count)
            {
                return false;
            }

            var item = inventory[selectedSlot];
            int itemType;
            return item != null &&
                   GameStateReflection.TryGetInt(item, "type", out itemType) &&
                   itemType == expectedItemType;
        }

        private static UseItemPulseBridgeSnapshot BuildSnapshotLocked(PulseState state)
        {
            if (state == null)
            {
                return UseItemPulseBridgeSnapshot.None;
            }

            var finishedUtc = state.FinishedUtc == default(DateTime) ? DateTime.UtcNow : state.FinishedUtc;
            return new UseItemPulseBridgeSnapshot
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
                SelectedSlot = state.SelectedSlot,
                ExpectedItemType = state.ExpectedItemType,
                ItemName = state.ItemName ?? string.Empty,
                IntervalTicks = state.IntervalTicks,
                RequireUseItemHeld = state.RequireUseItemHeld,
                AllowCombatAim = state.AllowCombatAim,
                ItemCheckCount = state.ItemCheckCount,
                PressPulseCount = state.PressPulseCount,
                ReleasePulseCount = state.ReleasePulseCount,
                LastAppliedPress = state.LastAppliedPress,
                LastAppliedTick = state.LastAppliedTick
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class PulseState
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
            public int SelectedSlot;
            public int ExpectedItemType;
            public string ItemName;
            public int IntervalTicks;
            public bool RequireUseItemHeld;
            public bool AllowCombatAim;
            public long NextPressTick;
            public int ItemCheckCount;
            public int PressPulseCount;
            public int ReleasePulseCount;
            public bool LastAppliedPress;
            public long LastAppliedTick;
            public bool Finished;
        }
    }

    public sealed class UseItemPulseApplyResult
    {
        public Guid RequestId { get; set; }
        public UseItemInputState RestoreState { get; set; }
        public bool Pressed { get; set; }
        public bool AllowCombatAim { get; set; }
    }

    public sealed class UseItemPulseBridgeSnapshot
    {
        public static readonly UseItemPulseBridgeSnapshot None = new UseItemPulseBridgeSnapshot
        {
            Status = InputActionStatus.NotApplicable,
            ResultCode = DiagnosticResultCode.NotApplicable,
            Message = string.Empty,
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
        public int SelectedSlot { get; set; }
        public int ExpectedItemType { get; set; }
        public string ItemName { get; set; }
        public int IntervalTicks { get; set; }
        public bool RequireUseItemHeld { get; set; }
        public bool AllowCombatAim { get; set; }
        public int ItemCheckCount { get; set; }
        public int PressPulseCount { get; set; }
        public int ReleasePulseCount { get; set; }
        public bool LastAppliedPress { get; set; }
        public long LastAppliedTick { get; set; }
    }
}
