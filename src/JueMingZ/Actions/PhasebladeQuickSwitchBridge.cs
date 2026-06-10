using System;
using System.Collections;
using System.Globalization;
using JueMingZ.Automation.Combat;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions
{
    public static class PhasebladeQuickSwitchBridge
    {
        // Bridge ownership is the only mutable runtime state for phaseblade quick
        // switch. It never stores or restores an original slot; release leaves the
        // player on the last selected phaseblade as required by the feature plan.
        private static readonly TimeSpan DefaultDeadManTimeout = TimeSpan.FromMinutes(5);
        private static readonly object SyncRoot = new object();
        private static PhasebladeQuickSwitchBridgeState _active;
        private static PhasebladeQuickSwitchBridgeSnapshot _lastSnapshot = PhasebladeQuickSwitchBridgeSnapshot.None;

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

        public static PhasebladeQuickSwitchBridgeSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _lastSnapshot == null ? PhasebladeQuickSwitchBridgeSnapshot.None.Clone() : _lastSnapshot.Clone();
            }
        }

        public static bool TryBegin(
            Guid requestId,
            string sourceFeatureId,
            string scenario,
            int intervalTicks,
            bool allowCombatAim,
            TimeSpan timeout,
            out string message)
        {
            message = string.Empty;
            if (requestId == Guid.Empty)
            {
                message = "PhasebladeQuickSwitchBridge request id is empty.";
                return false;
            }

            var safeInterval = CombatPhasebladeQuickSwitchSettings.NormalizeIntervalTicks(intervalTicks);
            lock (SyncRoot)
            {
                if (_active != null && !_active.Finished && _active.RequestId != requestId)
                {
                    message = "PhasebladeQuickSwitchBridge is busy with request " + _active.RequestId;
                    return false;
                }

                _active = new PhasebladeQuickSwitchBridgeState
                {
                    RequestId = requestId,
                    SourceFeatureId = sourceFeatureId ?? string.Empty,
                    Scenario = string.IsNullOrWhiteSpace(scenario) ? ScenarioNames.CombatPhasebladeQuickSwitch : scenario,
                    CreatedUtc = DateTime.UtcNow,
                    Timeout = timeout <= TimeSpan.Zero ? DefaultDeadManTimeout : timeout,
                    Status = InputActionStatus.Running,
                    ResultCode = DiagnosticResultCode.Queued,
                    Message = "Phaseblade quick switch started.",
                    IntervalTicks = safeInterval,
                    AllowCombatAim = allowCombatAim,
                    MachineState = CombatPhasebladeQuickSwitchState.Idle(),
                    LastAppliedTick = long.MinValue,
                    LastSelectedSlot = -1,
                    LastTargetSlot = -1,
                    PendingSwitchTargetSlot = -1,
                    LastRestoreSucceeded = true
                };
                _lastSnapshot = BuildSnapshotLocked(_active);
            }

            message = "PhasebladeQuickSwitchBridge started: request=" + requestId +
                      ", intervalTicks=" + safeInterval.ToString(CultureInfo.InvariantCulture) +
                      ", allowCombatAim=" + allowCombatAim.ToString();
            Logger.Info("PhasebladeQuickSwitchBridge", message);
            return true;
        }

        public static PhasebladeQuickSwitchBridgeSnapshot Update(Guid requestId, bool worldInputBlocked, string blockReason)
        {
            PhasebladeQuickSwitchBridgeState state;
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
                return RequestStopOrComplete(
                    requestId,
                    InputActionStatus.BlockedByUi,
                    DiagnosticResultCode.BlockedByUi,
                    string.IsNullOrWhiteSpace(blockReason) ? "Phaseblade quick switch stopped: world input is blocked." : blockReason);
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                return Complete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.TimedOut,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.TimedOut,
                    state.ApplyCount > 0
                        ? "Phaseblade quick switch completed after dead-man timeout."
                        : "Phaseblade quick switch timed out before ItemCheck applied it.");
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch stopped: local player unavailable.");
            }

            bool rightHeld;
            if (!TerrariaInputCompat.TryReadPhysicalMouseRightHeld(out rightHeld))
            {
                return Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch stopped: right input unavailable: " + TerrariaInputCompat.LastInputCompatError);
            }

            if (!rightHeld)
            {
                return RequestStopOrComplete(
                    requestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    state.ApplyCount > 0
                        ? "Phaseblade quick switch stopped because right input was released."
                        : "Phaseblade quick switch did not start because right input was released.");
            }

            if (TryProgressPendingSwitch(requestId, player, out var switchSnapshot))
            {
                return switchSnapshot;
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.Message = "Phaseblade quick switch running.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return _lastSnapshot;
        }

        public static bool TryApplyItemCheckUse(object player, out PhasebladeQuickSwitchApplyResult result)
        {
            result = null;
            PhasebladeQuickSwitchBridgeState state;
            lock (SyncRoot)
            {
                state = _active;
            }

            if (state == null || state.Finished)
            {
                return false;
            }

            if (player == null || !TerrariaInputCompat.TryIsLocalPlayer(player))
            {
                return false;
            }

            if (DateTime.UtcNow - state.CreatedUtc > state.Timeout)
            {
                Complete(state.RequestId, InputActionStatus.TimedOut, DiagnosticResultCode.TimedOut, "Phaseblade quick switch timed out before ItemCheck.");
                return false;
            }

            if (state.StopRequested && state.LastAppliedPress)
            {
                return TryApplyScopedInput(
                    player,
                    state,
                    false,
                    PhasebladeQuickSwitchStates.ReleaseCurrent,
                    "stopRelease",
                    CombatPhasebladeQuickSwitchState.Idle(),
                    out result);
            }

            string frameMessage;
            CombatPhasebladeQuickSwitchFrame frame;
            if (!TryReadFrame(player, state.IntervalTicks, out frame, out frameMessage))
            {
                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Phaseblade quick switch stopped: " + frameMessage);
                return false;
            }

            var decision = CombatPhasebladeQuickSwitchService.Decide(state.MachineState, frame);
            if (decision.ResetState)
            {
                if (state.LastAppliedPress)
                {
                    return TryApplyScopedInput(
                        player,
                        state,
                        false,
                        PhasebladeQuickSwitchStates.ReleaseCurrent,
                        "stopRelease:" + decision.Reason,
                        CombatPhasebladeQuickSwitchState.Idle(),
                        out result);
                }

                Complete(
                    state.RequestId,
                    state.ApplyCount > 0 ? InputActionStatus.Succeeded : InputActionStatus.NotApplicable,
                    state.ApplyCount > 0 ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                    "Phaseblade quick switch stopped: " + decision.Reason);
                return false;
            }

            if (decision.SwitchNext)
            {
                lock (SyncRoot)
                {
                    if (_active != null && _active.RequestId == state.RequestId && !_active.Finished)
                    {
                        _active.MachineState = decision.NextState;
                        _active.PendingSwitchTargetSlot = decision.TargetSlot;
                        _active.LastTargetSlot = decision.TargetSlot;
                        _active.LastSelectedSlot = frame.SelectedSlot;
                        _active.LastDecisionState = decision.State;
                        _active.LastDecisionReason = decision.Reason;
                        _active.Message = "Phaseblade quick switch queued hotbar selection.";
                        _lastSnapshot = BuildSnapshotLocked(_active);
                    }
                }

                return false;
            }

            if (!decision.PressCurrent && !decision.ReleaseCurrent)
            {
                lock (SyncRoot)
                {
                    if (_active != null && _active.RequestId == state.RequestId && !_active.Finished)
                    {
                        _active.MachineState = decision.NextState;
                        _active.LastSelectedSlot = frame.SelectedSlot;
                        _active.LastDecisionState = decision.State;
                        _active.LastDecisionReason = decision.Reason;
                        if (!string.Equals(decision.State, PhasebladeQuickSwitchStates.SwitchNext, StringComparison.Ordinal))
                        {
                            _active.PendingSwitchTargetSlot = -1;
                        }

                        _active.Message = "Phaseblade quick switch waiting: " + decision.Reason;
                        _lastSnapshot = BuildSnapshotLocked(_active);
                    }
                }

                return false;
            }

            return TryApplyScopedInput(
                player,
                state,
                decision.PressCurrent,
                decision.State,
                decision.Reason,
                decision.NextState,
                out result);
        }

        private static bool TryApplyScopedInput(
            object player,
            PhasebladeQuickSwitchBridgeState state,
            bool pressed,
            string decisionState,
            string reason,
            CombatPhasebladeQuickSwitchState nextState,
            out PhasebladeQuickSwitchApplyResult result)
        {
            result = null;
            int selectedSlot;
            TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot);

            long tick;
            if (!TerrariaInputCompat.TryReadGameUpdateCount(out tick))
            {
                tick = 0;
            }

            UseItemInputState restoreState;
            if (!TerrariaInputCompat.TryCaptureUseItemInputState(player, out restoreState))
            {
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch failed to capture input state: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            if (!TerrariaInputCompat.TryApplyPhasebladeQuickSwitchForItemCheck(player, pressed))
            {
                TerrariaInputCompat.TryRestoreUseItemInputState(player, restoreState);
                Complete(state.RequestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch failed to apply input override: " + TerrariaInputCompat.LastInputCompatError);
                return false;
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == state.RequestId && !_active.Finished)
                {
                    _active.ApplyCount++;
                    if (pressed)
                    {
                        _active.PressCount++;
                    }
                    else
                    {
                        _active.ReleaseCount++;
                    }

                    _active.LastAppliedPress = pressed;
                    _active.LastAppliedTick = tick;
                    _active.LastSelectedSlot = selectedSlot;
                    _active.LastItemType = ReadItemType(player, selectedSlot);
                    _active.LastDecisionState = string.IsNullOrWhiteSpace(decisionState)
                        ? pressed ? PhasebladeQuickSwitchStates.PressCurrent : PhasebladeQuickSwitchStates.ReleaseCurrent
                        : decisionState;
                    _active.LastDecisionReason = reason ?? string.Empty;
                    _active.MachineState = nextState ?? CombatPhasebladeQuickSwitchState.Idle();
                    _active.Message = pressed
                        ? "Phaseblade quick switch applied press tick."
                        : "Phaseblade quick switch applied release tick.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }

            result = new PhasebladeQuickSwitchApplyResult
            {
                RequestId = state.RequestId,
                RestoreState = restoreState,
                Pressed = pressed,
                Released = !pressed,
                AllowCombatAim = state.AllowCombatAim
            };
            return true;
        }

        public static void RecordRestoreStatus(Guid requestId, bool restored)
        {
            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.LastRestoreSucceeded = restored;
                    if (restored)
                    {
                        _active.RestoreSuccessCount++;
                    }

                    _lastSnapshot = BuildSnapshotLocked(_active);
                }
            }
        }

        public static void Cancel(Guid requestId, string reason)
        {
            Complete(requestId, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, reason ?? "Phaseblade quick switch cancelled.");
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _active = null;
                _lastSnapshot = PhasebladeQuickSwitchBridgeSnapshot.None;
            }
        }

        private static bool TryProgressPendingSwitch(Guid requestId, object player, out PhasebladeQuickSwitchBridgeSnapshot snapshot)
        {
            snapshot = null;
            PhasebladeQuickSwitchBridgeState state;
            lock (SyncRoot)
            {
                state = _active;
            }

            if (state == null || state.RequestId != requestId || state.Finished || state.PendingSwitchTargetSlot < 0)
            {
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                snapshot = Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch failed to read selected slot while switching: " + TerrariaInputCompat.LastInputCompatError);
                return true;
            }

            if (selectedSlot != state.PendingSwitchTargetSlot)
            {
                bool selectedImmediately;
                if (!TerrariaInputCompat.TryRequestInventorySlotSelection(player, state.PendingSwitchTargetSlot, out selectedImmediately))
                {
                    snapshot = Complete(requestId, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Phaseblade quick switch failed to request hotbar slot: " + TerrariaInputCompat.LastInputCompatError);
                    return true;
                }
            }

            lock (SyncRoot)
            {
                if (_active != null && _active.RequestId == requestId && !_active.Finished)
                {
                    _active.SwitchRequestCount++;
                    _active.LastSwitchMethod = TerrariaInputCompat.LastSelectionMethod ?? string.Empty;
                    _active.Message = "Phaseblade quick switch requested hotbar slot.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    snapshot = _lastSnapshot;
                }
            }

            return true;
        }

        private static PhasebladeQuickSwitchBridgeSnapshot RequestStopOrComplete(
            Guid requestId,
            InputActionStatus status,
            DiagnosticResultCode resultCode,
            string message)
        {
            lock (SyncRoot)
            {
                if (_active == null || _active.RequestId != requestId)
                {
                    return _lastSnapshot;
                }

                _active.StopRequested = true;
                _active.StopReason = message ?? string.Empty;
                if (_active.LastAppliedPress)
                {
                    _active.Message = "Phaseblade quick switch stopping; waiting for release scope.";
                    _lastSnapshot = BuildSnapshotLocked(_active);
                    return _lastSnapshot;
                }
            }

            return Complete(requestId, status, resultCode, message);
        }

        private static PhasebladeQuickSwitchBridgeSnapshot Complete(Guid requestId, InputActionStatus status, DiagnosticResultCode resultCode, string message)
        {
            lock (SyncRoot)
            {
                if (_active == null || _active.RequestId != requestId)
                {
                    return _lastSnapshot;
                }

                if (_active.LastAppliedPress && !TryClearPostItemCheckUseState())
                {
                    _active.LastRestoreSucceeded = false;
                }

                _active.Status = status;
                _active.ResultCode = resultCode;
                _active.Message = message ?? string.Empty;
                _active.LastAppliedPress = false;
                _active.Finished = true;
                _active.FinishedUtc = DateTime.UtcNow;
                _lastSnapshot = BuildSnapshotLocked(_active);
                _active = null;
                Logger.Info("PhasebladeQuickSwitchBridge", "Phaseblade quick switch finished: " + status + " / " + _lastSnapshot.Message);
                return _lastSnapshot;
            }
        }

        private static bool TryReadFrame(object player, int intervalTicks, out CombatPhasebladeQuickSwitchFrame frame, out string message)
        {
            frame = null;
            message = string.Empty;

            bool rightHeld;
            if (!TerrariaInputCompat.TryReadPhysicalMouseRightHeld(out rightHeld))
            {
                message = "right input unavailable: " + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            int selectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out selectedSlot))
            {
                message = "selected slot unavailable: " + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null)
            {
                message = "inventory unavailable";
                return false;
            }

            var eligibleSlots = new int[CombatPhasebladeQuickSwitchService.HotbarSlotCount];
            var eligibleCount = FindEligibleSlots(inventory, eligibleSlots);
            long tick;
            if (!TerrariaInputCompat.TryReadGameUpdateCount(out tick))
            {
                tick = 0;
            }

            frame = new CombatPhasebladeQuickSwitchFrame
            {
                Enabled = true,
                RightHeld = rightHeld,
                SafeContext = true,
                SelectedSlot = selectedSlot,
                EligibleSlots = eligibleSlots,
                EligibleSlotCount = eligibleCount,
                ItemReady = CombatFlailRuntime.CanUseSelectedItem(player),
                Tick = tick,
                IntervalTicks = intervalTicks
            };
            return true;
        }

        private static int FindEligibleSlots(IList inventory, int[] destination)
        {
            if (inventory == null || destination == null)
            {
                return 0;
            }

            var count = 0;
            var limit = Math.Min(CombatPhasebladeQuickSwitchService.HotbarSlotCount, inventory.Count);
            for (var slot = 0; slot < limit && count < destination.Length; slot++)
            {
                var item = inventory[slot];
                int itemType;
                string itemName;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                if (InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon) &&
                    CombatPhasebladeQuickSwitchService.IsEligibleHotbarItem(itemType, stack))
                {
                    destination[count++] = slot;
                }
            }

            return count;
        }

        private static int ReadItemType(object player, int selectedSlot)
        {
            var inventory = GameStateReflection.AsList(GameStateReflection.GetMember(player, "inventory"));
            if (inventory == null || selectedSlot < 0 || selectedSlot >= inventory.Count)
            {
                return 0;
            }

            int itemType;
            string itemName;
            int stack;
            int buffType;
            int buffTime;
            bool summon;
            return InventoryMutationCompat.TryReadItemFields(inventory[selectedSlot], out itemType, out itemName, out stack, out buffType, out buffTime, out summon)
                ? itemType
                : 0;
        }

        private static bool TryClearPostItemCheckUseState()
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return false;
            }

            return TerrariaInputCompat.TryApplyPhasebladeQuickSwitchPostItemCheckState(player, false);
        }

        private static PhasebladeQuickSwitchBridgeSnapshot BuildSnapshotLocked(PhasebladeQuickSwitchBridgeState state)
        {
            if (state == null)
            {
                return PhasebladeQuickSwitchBridgeSnapshot.None;
            }

            var finishedUtc = state.FinishedUtc == default(DateTime) ? DateTime.UtcNow : state.FinishedUtc;
            return new PhasebladeQuickSwitchBridgeSnapshot
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
                IntervalTicks = state.IntervalTicks,
                AllowCombatAim = state.AllowCombatAim,
                ApplyCount = state.ApplyCount,
                PressCount = state.PressCount,
                ReleaseCount = state.ReleaseCount,
                SwitchRequestCount = state.SwitchRequestCount,
                RestoreSuccessCount = state.RestoreSuccessCount,
                LastAppliedPress = state.LastAppliedPress,
                LastRestoreSucceeded = state.LastRestoreSucceeded,
                LastAppliedTick = state.LastAppliedTick,
                LastSelectedSlot = state.LastSelectedSlot,
                LastTargetSlot = state.LastTargetSlot,
                PendingSwitchTargetSlot = state.PendingSwitchTargetSlot,
                LastItemType = state.LastItemType,
                LastDecisionState = state.LastDecisionState ?? string.Empty,
                LastDecisionReason = state.LastDecisionReason ?? string.Empty,
                LastSwitchMethod = state.LastSwitchMethod ?? string.Empty
            };
        }

        private sealed class PhasebladeQuickSwitchBridgeState
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
            public int IntervalTicks;
            public bool AllowCombatAim;
            public CombatPhasebladeQuickSwitchState MachineState;
            public bool StopRequested;
            public string StopReason;
            public int ApplyCount;
            public int PressCount;
            public int ReleaseCount;
            public int SwitchRequestCount;
            public int RestoreSuccessCount;
            public bool LastAppliedPress;
            public bool LastRestoreSucceeded;
            public long LastAppliedTick;
            public int LastSelectedSlot;
            public int LastTargetSlot;
            public int PendingSwitchTargetSlot;
            public int LastItemType;
            public string LastDecisionState;
            public string LastDecisionReason;
            public string LastSwitchMethod;
        }
    }

    public sealed class PhasebladeQuickSwitchApplyResult
    {
        public Guid RequestId { get; set; }
        public UseItemInputState RestoreState { get; set; }
        public bool Pressed { get; set; }
        public bool Released { get; set; }
        public bool AllowCombatAim { get; set; }
    }

    public sealed class PhasebladeQuickSwitchBridgeSnapshot
    {
        public static readonly PhasebladeQuickSwitchBridgeSnapshot None = new PhasebladeQuickSwitchBridgeSnapshot
        {
            Status = InputActionStatus.NotApplicable,
            ResultCode = DiagnosticResultCode.NotApplicable,
            Message = string.Empty,
            SourceFeatureId = string.Empty,
            Scenario = string.Empty,
            LastAppliedTick = long.MinValue,
            LastSelectedSlot = -1,
            LastTargetSlot = -1,
            PendingSwitchTargetSlot = -1,
            LastDecisionState = string.Empty,
            LastDecisionReason = string.Empty,
            LastSwitchMethod = string.Empty,
            LastRestoreSucceeded = true
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
        public int IntervalTicks { get; set; }
        public bool AllowCombatAim { get; set; }
        public int ApplyCount { get; set; }
        public int PressCount { get; set; }
        public int ReleaseCount { get; set; }
        public int SwitchRequestCount { get; set; }
        public int RestoreSuccessCount { get; set; }
        public bool LastAppliedPress { get; set; }
        public bool LastRestoreSucceeded { get; set; }
        public long LastAppliedTick { get; set; }
        public int LastSelectedSlot { get; set; }
        public int LastTargetSlot { get; set; }
        public int PendingSwitchTargetSlot { get; set; }
        public int LastItemType { get; set; }
        public string LastDecisionState { get; set; }
        public string LastDecisionReason { get; set; }
        public string LastSwitchMethod { get; set; }

        public PhasebladeQuickSwitchBridgeSnapshot Clone()
        {
            return (PhasebladeQuickSwitchBridgeSnapshot)MemberwiseClone();
        }
    }
}
