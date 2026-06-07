using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static class MovementSimulatedJumpPulseCompat
    {
        // Jump pulses are applied only from Player.Update hook phases so input
        // writes follow vanilla ordering and can be restored.
        private static readonly object SyncRoot = new object();
        private static SimulatedJumpPulseState _queuedPulse;

        public static bool QueueSimulatedJumpPulse(Guid requestId, string triggerReason, bool applyRocketRelease, out string message)
        {
            lock (SyncRoot)
            {
                if (_queuedPulse != null && !_queuedPulse.IsTerminal)
                {
                    message = "A simulated jump pulse is already queued.";
                    return false;
                }

                _queuedPulse = new SimulatedJumpPulseState
                {
                    RequestId = requestId,
                    TriggerReason = triggerReason ?? string.Empty,
                    ApplyRocketRelease = applyRocketRelease,
                    Phase = SimulatedJumpPulsePhase.Release,
                    Status = "queued",
                    LastMessage = "Queued for Player.Update prefix.",
                    QueuedUtc = DateTime.UtcNow
                };

                message = _queuedPulse.LastMessage;
                return true;
            }
        }

        public static bool TryGetSimulatedJumpPulseSnapshot(Guid requestId, out SimulatedJumpPulseSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                snapshot = _queuedPulse == null ? null : _queuedPulse.ToSnapshot();
                if (snapshot == null)
                {
                    return false;
                }

                return requestId == Guid.Empty || snapshot.RequestId == requestId;
            }
        }

        public static void CancelSimulatedJumpPulse(Guid requestId, string reason)
        {
            lock (SyncRoot)
            {
                if (_queuedPulse == null ||
                    (requestId != Guid.Empty && _queuedPulse.RequestId != requestId) ||
                    _queuedPulse.IsTerminal)
                {
                    return;
                }

                _queuedPulse.Failed = true;
                _queuedPulse.Status = "cancelled";
                _queuedPulse.LastMessage = "Simulated jump pulse cancelled: " + (reason ?? string.Empty);
                _queuedPulse.LastApplySite = "cancelled";
                _queuedPulse.CompletedUtc = DateTime.UtcNow;
                _queuedPulse.Phase = SimulatedJumpPulsePhase.Failed;
            }
        }

        public static bool ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate(object player)
        {
            SimulatedJumpPulseState pulse;
            lock (SyncRoot)
            {
                pulse = _queuedPulse;
                if (pulse == null || pulse.IsTerminal)
                {
                    return false;
                }
            }

            try
            {
                if (player == null)
                {
                    FinishQueuedPulse(pulse.RequestId, false, "Player.Update prefix received null player.", "Player.Update:failed");
                    return false;
                }

                string message;
                bool applied;
                if (pulse.Phase == SimulatedJumpPulsePhase.Release)
                {
                    applied = TerrariaInputCompat.TryPrimeJumpReleaseForNextTick(player, pulse.ApplyRocketRelease, out message);
                    UpdateQueuedPulse(pulse.RequestId, applied, message, "Player.Update:release", applied ? SimulatedJumpPulsePhase.Press : SimulatedJumpPulsePhase.Failed);
                    return applied;
                }

                if (pulse.Phase == SimulatedJumpPulsePhase.Press)
                {
                    applied = TerrariaInputCompat.TryPressPrimedJumpForNextTick(player, pulse.ApplyRocketRelease, out message);
                    FinishQueuedPulse(pulse.RequestId, applied, message, applied ? "Player.Update:press" : "Player.Update:failed");
                    return applied;
                }

                return false;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementSimulatedJumpPulseCompat.ApplyQueuedSimulatedJumpPulseBeforePlayerUpdate", error);
                FinishQueuedPulse(pulse.RequestId, false, "Simulated jump pulse failed: " + error.GetType().Name + ": " + error.Message, "Player.Update:exception");
                return false;
            }
        }

        private static void UpdateQueuedPulse(Guid requestId, bool applied, string message, string applySite, SimulatedJumpPulsePhase nextPhase)
        {
            lock (SyncRoot)
            {
                if (_queuedPulse == null || _queuedPulse.RequestId != requestId || _queuedPulse.IsTerminal)
                {
                    return;
                }

                _queuedPulse.LastApplySite = applySite ?? string.Empty;
                _queuedPulse.LastMessage = message ?? string.Empty;
                _queuedPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedPulse.Status = applied ? "applying" : "failed";
                if (!applied)
                {
                    _queuedPulse.Failed = true;
                    _queuedPulse.CompletedUtc = DateTime.UtcNow;
                    _queuedPulse.Phase = SimulatedJumpPulsePhase.Failed;
                    return;
                }

                if (_queuedPulse.Phase == SimulatedJumpPulsePhase.Release)
                {
                    _queuedPulse.ReleaseApplied = true;
                }
                else if (_queuedPulse.Phase == SimulatedJumpPulsePhase.Press)
                {
                    _queuedPulse.PressApplied = true;
                }

                _queuedPulse.Phase = nextPhase;
            }
        }

        private static void FinishQueuedPulse(Guid requestId, bool succeeded, string message, string applySite)
        {
            lock (SyncRoot)
            {
                if (_queuedPulse == null || _queuedPulse.RequestId != requestId || _queuedPulse.IsTerminal)
                {
                    return;
                }

                if (_queuedPulse.Phase == SimulatedJumpPulsePhase.Press)
                {
                    _queuedPulse.PressApplied = succeeded;
                }

                _queuedPulse.Completed = succeeded;
                _queuedPulse.Failed = !succeeded;
                _queuedPulse.Status = succeeded ? "completed" : "failed";
                _queuedPulse.LastMessage = message ?? string.Empty;
                _queuedPulse.LastApplySite = applySite ?? string.Empty;
                _queuedPulse.LastAppliedUtc = DateTime.UtcNow;
                _queuedPulse.CompletedUtc = DateTime.UtcNow;
                _queuedPulse.Phase = succeeded ? SimulatedJumpPulsePhase.Completed : SimulatedJumpPulsePhase.Failed;
            }
        }

        private enum SimulatedJumpPulsePhase
        {
            Release,
            Press,
            Completed,
            Failed
        }

        private sealed class SimulatedJumpPulseState
        {
            public Guid RequestId { get; set; }
            public string TriggerReason { get; set; }
            public bool ApplyRocketRelease { get; set; }
            public bool ReleaseApplied { get; set; }
            public bool PressApplied { get; set; }
            public bool Completed { get; set; }
            public bool Failed { get; set; }
            public string Status { get; set; }
            public string LastApplySite { get; set; }
            public string LastMessage { get; set; }
            public DateTime QueuedUtc { get; set; }
            public DateTime LastAppliedUtc { get; set; }
            public DateTime CompletedUtc { get; set; }
            public SimulatedJumpPulsePhase Phase { get; set; }

            public bool IsTerminal
            {
                get { return Completed || Failed || Phase == SimulatedJumpPulsePhase.Completed || Phase == SimulatedJumpPulsePhase.Failed; }
            }

            public SimulatedJumpPulseSnapshot ToSnapshot()
            {
                return new SimulatedJumpPulseSnapshot
                {
                    RequestId = RequestId,
                    TriggerReason = TriggerReason ?? string.Empty,
                    ApplyRocketRelease = ApplyRocketRelease,
                    ReleaseApplied = ReleaseApplied,
                    PressApplied = PressApplied,
                    Completed = Completed,
                    Failed = Failed,
                    Status = Status ?? string.Empty,
                    Phase = Phase.ToString(),
                    LastApplySite = LastApplySite ?? string.Empty,
                    LastMessage = LastMessage ?? string.Empty,
                    QueuedUtc = QueuedUtc,
                    LastAppliedUtc = LastAppliedUtc,
                    CompletedUtc = CompletedUtc
                };
            }
        }
    }

    public sealed class SimulatedJumpPulseSnapshot
    {
        public Guid RequestId { get; set; }
        public string TriggerReason { get; set; }
        public bool ApplyRocketRelease { get; set; }
        public bool ReleaseApplied { get; set; }
        public bool PressApplied { get; set; }
        public bool Completed { get; set; }
        public bool Failed { get; set; }
        public string Status { get; set; }
        public string Phase { get; set; }
        public string LastApplySite { get; set; }
        public string LastMessage { get; set; }
        public DateTime QueuedUtc { get; set; }
        public DateTime LastAppliedUtc { get; set; }
        public DateTime CompletedUtc { get; set; }
    }
}
