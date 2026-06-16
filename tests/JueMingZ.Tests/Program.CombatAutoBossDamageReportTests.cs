using System;
using JueMingZ.Automation.Combat;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void CombatAutoBossDamageReportBaselinesExistingAttempts()
        {
            var state = new CombatAutoBossDamageReportState();

            var decision = CombatAutoBossDamageReportService.EvaluateForTesting(
                true,
                true,
                new[] { 101 },
                state);

            if (decision.ShouldSend ||
                !string.Equals(decision.Decision, "baseline", StringComparison.Ordinal) ||
                !state.Initialized ||
                !state.ReportedAttemptIds.Contains(101))
            {
                throw new InvalidOperationException("Auto boss damage report must baseline existing attempts without sending.");
            }
        }

        private static void CombatAutoBossDamageReportSendsNewAttemptOnce()
        {
            var state = new CombatAutoBossDamageReportState();

            CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new int[0], state);
            var send = CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new[] { 201 }, state);
            var repeat = CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new[] { 201 }, state);

            if (!send.ShouldSend ||
                !string.Equals(send.Decision, "send", StringComparison.Ordinal) ||
                send.NewAttemptCount != 1 ||
                send.AttemptKey != 201)
            {
                throw new InvalidOperationException("Auto boss damage report must send when a new recent attempt appears.");
            }

            if (repeat.ShouldSend ||
                !string.Equals(repeat.Decision, "alreadyReported", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto boss damage report must not resend the same recent attempt.");
            }
        }

        private static void CombatAutoBossDamageReportDisabledReenableBaselines()
        {
            var state = new CombatAutoBossDamageReportState();

            CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new int[0], state);
            var send = CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new[] { 301 }, state);
            var disabled = CombatAutoBossDamageReportService.EvaluateForTesting(false, true, new[] { 301 }, state);
            var disabledClearedState = !state.Initialized && state.ReportedAttemptIds.Count == 0;
            var reenabled = CombatAutoBossDamageReportService.EvaluateForTesting(true, true, new[] { 301 }, state);

            if (!send.ShouldSend)
            {
                throw new InvalidOperationException("Auto boss damage report setup must send the first new attempt before disable.");
            }

            if (!disabledClearedState ||
                !string.Equals(disabled.Decision, "disabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto boss damage report disabled tick must clear and report disabled.");
            }

            if (reenabled.ShouldSend ||
                !string.Equals(reenabled.Decision, "baseline", StringComparison.Ordinal) ||
                !state.ReportedAttemptIds.Contains(301))
            {
                throw new InvalidOperationException("Auto boss damage report must baseline current attempts when re-enabled.");
            }
        }
    }
}
