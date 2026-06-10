using System;
using JueMingZ.Actions.Channels;
using JueMingZ.Common;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions
{
    public sealed partial class InputActionQueue
    {
        private InputActionResult TryStartNextActionLocked(GameStateSnapshot snapshot)
        {
            // Priority and bucket sorting only choose the next pending request; active
            // running actions are not preempted here.
            if (_running != null)
            {
                return null;
            }

            ExpirePendingLocked(DateTime.UtcNow);
            ExpireCleanupLeaseLocked(DateTime.UtcNow);
            if (_pending.Count == 0)
            {
                return null;
            }

            InputActionChannelDecision selectedDecision;
            var request = SelectNextStartableActionLocked(out selectedDecision);
            if (request == null)
            {
                return null;
            }

            if (selectedDecision != null)
            {
                _lastChannelDecision = selectedDecision;
            }

            _lastSchedulerSelectedRequest = BuildRequestOwnerSummary(request);
            _lastSchedulerFairnessBucket = InputActionScheduler.ResolveBucketName(request);

            InputActionChannelLease lease;
            InputActionChannelDecision acquireDecision;
            if (!_channelArbiter.TryAcquire(request, out lease, out acquireDecision))
            {
                _lastChannelDecision = acquireDecision;
                return null;
            }

            _pending.Remove(request);
            _running = new InputActionExecution
            {
                Request = request,
                Status = InputActionStatus.Running,
                StartedUtc = DateTime.UtcNow,
                LastUpdateUtc = DateTime.UtcNow,
                Message = "Running"
            };
            _runningChannelLease = lease;
            if (_running.State != null)
            {
                _running.State["ChannelRequired"] = InputActionChannelFormatter.Format(acquireDecision.RequiredChannels);
                _running.State["ChannelConflicts"] = InputActionChannelFormatter.Format(acquireDecision.ConflictChannels);
                _running.State["ChannelDecision"] = acquireDecision.Summary;
            }

            _lastChannelDecision = acquireDecision;
            Logger.Info("InputActionQueue", "Input action started: " + request.Kind + " / " + request.Description);

            var executor = GetExecutor(request.Kind);
            InputActionExecutionStepResult step;
            try
            {
                step = executor.Start(_running, snapshot);
            }
            catch (Exception error)
            {
                _running.Status = InputActionStatus.Failed;
                _running.Message = "Action start failed: " + error.Message;
                _running.Error = error;
                Logger.Error("InputActionQueue", "Input action start failed: " + request.Kind, error);
                return InputActionResult.FromExecution(_running, InputActionStatus.Failed, _running.Message, error);
            }

            _running.Status = step.Status;
            _running.Message = step.Message ?? string.Empty;
            _running.Error = step.Error;
            if (step.IsTerminal)
            {
                return InputActionResult.FromExecution(_running, step.Status, step.Message, step.Error);
            }

            return null;
        }

        private InputActionResult UpdateRunningActionLocked(GameStateSnapshot snapshot)
        {
            if (_running == null || _running.Request == null)
            {
                return null;
            }

            var timeout = _running.Request.Timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : _running.Request.Timeout;
            if (DateTime.UtcNow - _running.StartedUtc > timeout)
            {
                return CancelRunningLocked(InputActionStatus.TimedOut, "Action timed out.");
            }

            var executor = GetExecutor(_running.Request.Kind);
            var step = executor.Update(_running, snapshot);
            _running.Status = step.Status;
            _running.Message = step.Message ?? string.Empty;
            _running.Error = step.Error;
            return step.IsTerminal
                ? InputActionResult.FromExecution(_running, step.Status, step.Message, step.Error)
                : null;
        }

        private InputActionResult CancelRunningLocked(InputActionStatus finalStatus, string reason)
        {
            if (_running == null)
            {
                return null;
            }

            var running = _running;
            var message = reason ?? "Action cancelled.";
            Exception error = null;
            try
            {
                var step = GetExecutor(running.Request.Kind).Cancel(running, reason);
                if (step != null)
                {
                    if (!string.IsNullOrWhiteSpace(step.Message))
                    {
                        message = step.Message;
                    }

                    error = step.Error;
                    running.Message = message;
                    running.Error = error;
                }
                else
                {
                    running.Message = message;
                }
            }
            catch (Exception cancelError)
            {
                error = cancelError;
                message = (string.IsNullOrWhiteSpace(reason) ? "Action cancelled." : reason) +
                          " Executor cancel cleanup failed: " + cancelError.Message;
                running.Message = message;
                running.Error = cancelError;
                Logger.Error(
                    "InputActionQueue",
                    "Input action cancel cleanup failed; completing action as " + finalStatus + ".",
                    cancelError);
            }

            return InputActionResult.FromExecution(running, finalStatus, message, error);
        }

        private void CompleteLocked(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            if (_running != null)
            {
                _running.Status = result.Status;
                _running.FinishedUtc = result.FinishedUtc;
                _running.Message = result.Message;
                _running.Error = result.Error;
            }

            if (_runningChannelLease != null)
            {
                _channelArbiter.Release(_runningChannelLease, result.Status.ToString());
                _runningChannelLease = null;
            }

            // Failed or unverified terminal states keep a short channel lease before
            // recording the receipt, so resource recovery is visible to later admission.
            MaybeCreateCleanupLeaseLocked(result);
            RecordResultLocked(result);
            _running = null;
        }

        private void RecordResultLocked(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            _resultStore.Record(result);

            Logger.Info(
                "InputActionQueue",
                "Input action finished: " + result.Kind + " / " + result.Status + " / " + result.Message);
            DiagnosticActionRecorder.RecordQueueResult(result);
        }
    }
}
