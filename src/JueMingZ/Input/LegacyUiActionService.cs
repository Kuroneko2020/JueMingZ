using System;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static readonly object ActionDiagnosticsSyncRoot = new object();
        private static long _actionUpdateSkippedCount;
        private static long _actionUpdateRanCount;
        private static int _pendingCommandCountLast;
        private static int _dispatchedCommandCountLast;
        private static double _dispatchElapsedMsLast;
        private static long _dragFrameActionSkipCount;

        public static long ActionUpdateSkippedCount
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _actionUpdateSkippedCount; } }
        }

        public static long ActionUpdateRanCount
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _actionUpdateRanCount; } }
        }

        public static int PendingCommandCountLast
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _pendingCommandCountLast; } }
        }

        public static int DispatchedCommandCountLast
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _dispatchedCommandCountLast; } }
        }

        public static double DispatchElapsedMsLast
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _dispatchElapsedMsLast; } }
        }

        public static long CommandCoalescedCount
        {
            get { return LegacyUiInput.CommandCoalescedCount; }
        }

        public static long DragFrameActionSkipCount
        {
            get { lock (ActionDiagnosticsSyncRoot) { return _dragFrameActionSkipCount; } }
        }

        public static void Update(InputActionQueue queue, GameStateSnapshot snapshot)
        {
            // Runtime action phase drains LegacyUiInput commands; the draw layer only
            // captures hit tests and queues commands.
            try
            {
                var gate = LegacyUiInput.GetActionUpdateGateSnapshot();
                RecordActionGateEntry(gate);
                if (!gate.NeedsActionUpdateThisFrame)
                {
                    RecordActionUpdateSkipped();
                    return;
                }

                RecordActionUpdateRan();
                if (gate.HasWindowDragOrResizeInteraction && gate.PendingCommandCount <= 0)
                {
                    RecordDragFrameActionSkip();
                    return;
                }

                var dispatchStart = Stopwatch.GetTimestamp();
                var dispatched = 0;
                LegacyUiCommand command;
                while (LegacyUiInput.TryDrainCommand(out command))
                {
                    dispatched++;
                    Dispatch(command, queue, snapshot);
                }

                RecordDispatchResult(dispatched, GetElapsedMilliseconds(dispatchStart, Stopwatch.GetTimestamp()));
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-ui-action-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyUiActionService",
                    "Legacy UI action dispatch failed.", error);
            }
        }

        internal static void ResetActionUpdateDiagnosticsForTesting()
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _actionUpdateSkippedCount = 0;
                _actionUpdateRanCount = 0;
                _pendingCommandCountLast = 0;
                _dispatchedCommandCountLast = 0;
                _dispatchElapsedMsLast = 0d;
                _dragFrameActionSkipCount = 0;
            }
        }

        private static void RecordActionGateEntry(LegacyUiActionUpdateGateSnapshot gate)
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _pendingCommandCountLast = gate == null ? 0 : gate.PendingCommandCount;
                _dispatchedCommandCountLast = 0;
                _dispatchElapsedMsLast = 0d;
            }
        }

        private static void RecordActionUpdateSkipped()
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _actionUpdateSkippedCount++;
            }
        }

        private static void RecordActionUpdateRan()
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _actionUpdateRanCount++;
            }
        }

        private static void RecordDragFrameActionSkip()
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _dragFrameActionSkipCount++;
            }
        }

        private static void RecordDispatchResult(int dispatched, double elapsedMs)
        {
            lock (ActionDiagnosticsSyncRoot)
            {
                _dispatchedCommandCountLast = dispatched;
                _dispatchElapsedMsLast = elapsedMs;
            }
        }

        private static double GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            return (endTimestamp - startTimestamp) * 1000d / Stopwatch.Frequency;
        }
    }
}
