using System;

namespace JueMingZ.Actions
{
    public enum WorldAutomationFairnessKind
    {
        None,
        AutoCaptureCritter,
        AutoHarvest
    }

    public sealed class WorldAutomationFairnessSnapshot
    {
        public string LastWinner { get; set; }
        public string LastFairnessBucket { get; set; }
        public string FairnessDebt { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public WorldAutomationFairnessSnapshot()
        {
            LastWinner = string.Empty;
            LastFairnessBucket = string.Empty;
            FairnessDebt = string.Empty;
        }
    }

    public static class WorldAutomationFairnessCoordinator
    {
        private const long CandidateStaleTicks = 8;
        private const int MaxConsecutiveWins = 2;
        private static readonly object SyncRoot = new object();
        private static WorldAutomationFairnessKind _lastWinner = WorldAutomationFairnessKind.None;
        private static long _lastCaptureCandidateTick = long.MinValue;
        private static long _lastHarvestCandidateTick = long.MinValue;
        private static long _runtimeGrantTick = long.MinValue;
        private static WorldAutomationFairnessKind _runtimeGrantWinner = WorldAutomationFairnessKind.None;
        private static int _captureConsecutiveWins;
        private static int _harvestConsecutiveWins;
        private static int _captureDebt;
        private static int _harvestDebt;
        private static string _lastFairnessBucket = string.Empty;
        private static DateTime? _lastDecisionUtc;

        public static bool ShouldDeferRuntimeSubmission(WorldAutomationFairnessKind kind, long tick, bool activeSessionContinuation)
        {
            if (kind == WorldAutomationFairnessKind.None)
            {
                return false;
            }

            lock (SyncRoot)
            {
                RecordCandidateLocked(kind, tick);
                if (activeSessionContinuation)
                {
                    _lastFairnessBucket = "P4:activeSessionContinuation:" + FormatKind(kind);
                    _lastDecisionUtc = DateTime.UtcNow;
                    return false;
                }

                var winner = ResolveRuntimeWinnerLocked(kind, tick);
                var defer = winner != WorldAutomationFairnessKind.None && winner != kind;
                _lastFairnessBucket = defer
                    ? "P5:worldAutomationFairnessDeferred:" + FormatKind(kind) + "->" + FormatKind(winner)
                    : "P5:worldAutomationFairnessGranted:" + FormatKind(kind);
                _lastDecisionUtc = DateTime.UtcNow;
                if (!defer)
                {
                    RecordWinnerLocked(kind, BothCandidatesReadyLocked(tick));
                }

                return defer;
            }
        }

        public static void RecordCandidateUnavailable(WorldAutomationFairnessKind kind, long tick, string reason)
        {
            if (kind == WorldAutomationFairnessKind.None)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (kind == WorldAutomationFairnessKind.AutoCaptureCritter)
                {
                    _lastCaptureCandidateTick = long.MinValue;
                }
                else if (kind == WorldAutomationFairnessKind.AutoHarvest)
                {
                    _lastHarvestCandidateTick = long.MinValue;
                }

                _lastFairnessBucket = "P5:worldAutomationCandidateUnavailable:" +
                                      FormatKind(kind) +
                                      (string.IsNullOrWhiteSpace(reason) ? string.Empty : ":" + reason);
                _lastDecisionUtc = DateTime.UtcNow;
                if (_runtimeGrantWinner == kind && _runtimeGrantTick <= tick)
                {
                    _runtimeGrantWinner = WorldAutomationFairnessKind.None;
                    _runtimeGrantTick = long.MinValue;
                }
            }
        }

        public static WorldAutomationFairnessKind ResolveItemCheckWriterOwner(bool autoCaptureActive, bool autoHarvestActive)
        {
            lock (SyncRoot)
            {
                if (autoCaptureActive && autoHarvestActive)
                {
                    var winner = ChooseWinnerLocked(true);
                    _lastFairnessBucket = "P5:itemCheckWriterFairness:" + FormatKind(winner);
                    _lastDecisionUtc = DateTime.UtcNow;
                    RecordWinnerLocked(winner, true);
                    return winner;
                }

                if (autoCaptureActive)
                {
                    _lastFairnessBucket = "P5:itemCheckWriterSingle:autoCapture";
                    _lastDecisionUtc = DateTime.UtcNow;
                    RecordWinnerLocked(WorldAutomationFairnessKind.AutoCaptureCritter, false);
                    return WorldAutomationFairnessKind.AutoCaptureCritter;
                }

                if (autoHarvestActive)
                {
                    _lastFairnessBucket = "P5:itemCheckWriterSingle:autoHarvest";
                    _lastDecisionUtc = DateTime.UtcNow;
                    RecordWinnerLocked(WorldAutomationFairnessKind.AutoHarvest, false);
                    return WorldAutomationFairnessKind.AutoHarvest;
                }

                return WorldAutomationFairnessKind.None;
            }
        }

        public static WorldAutomationFairnessSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new WorldAutomationFairnessSnapshot
                {
                    LastWinner = FormatKind(_lastWinner),
                    LastFairnessBucket = _lastFairnessBucket,
                    FairnessDebt = "autoCapture=" + _captureDebt + "; autoHarvest=" + _harvestDebt,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastWinner = WorldAutomationFairnessKind.None;
                _lastCaptureCandidateTick = long.MinValue;
                _lastHarvestCandidateTick = long.MinValue;
                _runtimeGrantTick = long.MinValue;
                _runtimeGrantWinner = WorldAutomationFairnessKind.None;
                _captureConsecutiveWins = 0;
                _harvestConsecutiveWins = 0;
                _captureDebt = 0;
                _harvestDebt = 0;
                _lastFairnessBucket = string.Empty;
                _lastDecisionUtc = null;
            }
        }

        private static WorldAutomationFairnessKind ResolveRuntimeWinnerLocked(WorldAutomationFairnessKind requested, long tick)
        {
            if (_runtimeGrantTick == tick && _runtimeGrantWinner != WorldAutomationFairnessKind.None)
            {
                return _runtimeGrantWinner;
            }

            var captureReady = IsCandidateFreshLocked(WorldAutomationFairnessKind.AutoCaptureCritter, tick);
            var harvestReady = IsCandidateFreshLocked(WorldAutomationFairnessKind.AutoHarvest, tick);
            WorldAutomationFairnessKind winner;
            if (captureReady && harvestReady)
            {
                winner = ChooseWinnerLocked(true);
            }
            else
            {
                winner = requested;
            }

            _runtimeGrantTick = tick;
            _runtimeGrantWinner = winner;
            return winner;
        }

        private static WorldAutomationFairnessKind ChooseWinnerLocked(bool bothReady)
        {
            if (!bothReady)
            {
                return WorldAutomationFairnessKind.None;
            }

            if (_captureConsecutiveWins >= MaxConsecutiveWins)
            {
                return WorldAutomationFairnessKind.AutoHarvest;
            }

            if (_harvestConsecutiveWins >= MaxConsecutiveWins)
            {
                return WorldAutomationFairnessKind.AutoCaptureCritter;
            }

            if (_lastWinner == WorldAutomationFairnessKind.AutoCaptureCritter)
            {
                return WorldAutomationFairnessKind.AutoHarvest;
            }

            if (_lastWinner == WorldAutomationFairnessKind.AutoHarvest)
            {
                return WorldAutomationFairnessKind.AutoCaptureCritter;
            }

            return WorldAutomationFairnessKind.AutoCaptureCritter;
        }

        private static void RecordCandidateLocked(WorldAutomationFairnessKind kind, long tick)
        {
            if (kind == WorldAutomationFairnessKind.AutoCaptureCritter)
            {
                _lastCaptureCandidateTick = tick;
            }
            else if (kind == WorldAutomationFairnessKind.AutoHarvest)
            {
                _lastHarvestCandidateTick = tick;
            }
        }

        private static bool BothCandidatesReadyLocked(long tick)
        {
            return IsCandidateFreshLocked(WorldAutomationFairnessKind.AutoCaptureCritter, tick) &&
                   IsCandidateFreshLocked(WorldAutomationFairnessKind.AutoHarvest, tick);
        }

        private static bool IsCandidateFreshLocked(WorldAutomationFairnessKind kind, long tick)
        {
            var candidateTick = kind == WorldAutomationFairnessKind.AutoCaptureCritter
                ? _lastCaptureCandidateTick
                : _lastHarvestCandidateTick;
            return candidateTick != long.MinValue &&
                   tick >= candidateTick &&
                   tick - candidateTick <= CandidateStaleTicks;
        }

        private static void RecordWinnerLocked(WorldAutomationFairnessKind winner, bool bothReady)
        {
            if (winner == WorldAutomationFairnessKind.None)
            {
                return;
            }

            _lastWinner = winner;
            if (winner == WorldAutomationFairnessKind.AutoCaptureCritter)
            {
                _captureConsecutiveWins++;
                _harvestConsecutiveWins = 0;
                _captureDebt = 0;
                if (bothReady)
                {
                    _harvestDebt++;
                }
            }
            else if (winner == WorldAutomationFairnessKind.AutoHarvest)
            {
                _harvestConsecutiveWins++;
                _captureConsecutiveWins = 0;
                _harvestDebt = 0;
                if (bothReady)
                {
                    _captureDebt++;
                }
            }
        }

        private static string FormatKind(WorldAutomationFairnessKind kind)
        {
            switch (kind)
            {
                case WorldAutomationFairnessKind.AutoCaptureCritter:
                    return "AutoCaptureCritter";
                case WorldAutomationFairnessKind.AutoHarvest:
                    return "AutoHarvest";
                default:
                    return string.Empty;
            }
        }
    }
}
