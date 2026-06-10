using System;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Runtime
{
    internal static class RuntimeStartupDiagnosticNoop
    {
        private static readonly object SyncRoot = new object();
        private static bool _queued;

        public static void QueueIfReady(RuntimeState state, InputActionQueue actionQueue)
        {
            if (state == null || !state.LateBootstrapCompleted || actionQueue == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_queued)
                {
                    return;
                }

                _queued = true;
            }

            var request = InputActionRequest.CreateDiagnosticNoop(
                "diagnostics.health_check",
                "M5 queue startup check");
            InputActionAdmissionResult admission;
            if (!actionQueue.TryEnqueue(request, out admission))
            {
                LogThrottle.WarnThrottled(
                    "startup-diagnostic-noop-admission-denied",
                    TimeSpan.FromSeconds(30),
                    "JueMingZRuntime",
                    "Startup diagnostic noop admission denied: " + (admission == null ? "unknown" : admission.Summary));
            }
        }
    }
}
