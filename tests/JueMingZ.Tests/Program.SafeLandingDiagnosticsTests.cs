using System;
using JueMingZ.Automation.Movement;
using JueMingZ.Config;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void SafeLandingConfigSummaryCacheHitsAndDirties()
        {
            MovementSafeLandingOptionCatalog.ResetConfigSummaryCacheForTesting();
            var settings = AppSettings.CreateDefault();
            settings.MovementSafeLandingEnabled = true;

            var first = MovementSafeLandingOptionCatalog.BuildConfigSummary(settings);
            long hitCount;
            long missCount;
            MovementSafeLandingOptionCatalog.GetConfigSummaryCacheStats(out hitCount, out missCount);
            AssertStringEquals(first, "12/12", "initial safe landing config summary");
            AssertLongEquals(hitCount, 0, "initial config summary cache hit count");
            AssertLongEquals(missCount, 1, "initial config summary cache miss count");

            var second = MovementSafeLandingOptionCatalog.BuildConfigSummary(settings);
            MovementSafeLandingOptionCatalog.GetConfigSummaryCacheStats(out hitCount, out missCount);
            AssertStringEquals(second, first, "cached safe landing config summary");
            AssertLongEquals(hitCount, 1, "config summary cache hit after repeated read");
            AssertLongEquals(missCount, 1, "config summary cache miss after repeated read");

            settings.MovementSafeLandingGrappleEnabled = false;
            var changed = MovementSafeLandingOptionCatalog.BuildConfigSummary(settings);
            MovementSafeLandingOptionCatalog.GetConfigSummaryCacheStats(out hitCount, out missCount);
            AssertStringEquals(changed, "11/12", "dirty safe landing config summary");
            AssertLongEquals(hitCount, 1, "config summary cache hit after dirty read");
            AssertLongEquals(missCount, 2, "config summary cache miss after dirty read");
        }

        private static void SafeLandingCheapSkipSuppressesRepeatedDiagnostics()
        {
            MovementSafeLandingService.ResetDiagnosticsForTesting();

            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(1, "notFallingFastEnough:cheap", true);
            var first = MovementSafeLandingService.GetDiagnostics();
            AssertLongEquals(first.CheapPrecheckSkipCount, 1, "first cheap skip count");
            AssertLongEquals(first.CheapSkipDiagnosticWrittenCount, 1, "first cheap skip diagnostic write count");
            AssertLongEquals(first.CheapSkipDiagnosticSuppressedCount, 0, "first cheap skip suppressed count");
            AssertStringEquals(first.CheapSkipLastReason, "notFallingFastEnough:cheap", "first cheap skip last reason");
            if (string.IsNullOrWhiteSpace(first.RecoveryStateSummary))
            {
                throw new InvalidOperationException("Expected first cheap skip to keep full recovery summary.");
            }

            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(2, "notFallingFastEnough:cheap", true);
            var second = MovementSafeLandingService.GetDiagnostics();
            AssertLongEquals(second.CheapPrecheckSkipCount, 2, "second cheap skip count");
            AssertLongEquals(second.CheapSkipDiagnosticWrittenCount, 1, "second cheap skip diagnostic write count");
            AssertLongEquals(second.CheapSkipDiagnosticSuppressedCount, 1, "second cheap skip suppressed count");
            AssertLongEquals(second.RecoverySummarySkippedCount, 1, "second cheap skip recovery summary skipped count");
            AssertLongEquals(second.LastTick, 2, "second cheap skip last tick");
            AssertStringEquals(second.LastSkipReason, "notFallingFastEnough:cheap", "second cheap skip last reason");

            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(31, "notFallingFastEnough:cheap", true);
            var heartbeat = MovementSafeLandingService.GetDiagnostics();
            AssertLongEquals(heartbeat.CheapPrecheckSkipCount, 3, "heartbeat cheap skip count");
            AssertLongEquals(heartbeat.CheapSkipDiagnosticWrittenCount, 2, "heartbeat cheap skip diagnostic write count");
            AssertLongEquals(heartbeat.CheapSkipDiagnosticSuppressedCount, 1, "heartbeat cheap skip suppressed count");
            AssertLongEquals(heartbeat.CheapSkipDiagnosticCadenceTicks, 30, "cheap skip diagnostic cadence");
        }

        private static void SafeLandingCheapSkipReasonChangeWritesDiagnostics()
        {
            MovementSafeLandingService.ResetDiagnosticsForTesting();

            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(10, "notFallingFastEnough:cheap", true);
            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(11, "playerNotControllable:cheap", false);
            var diagnostics = MovementSafeLandingService.GetDiagnostics();
            AssertLongEquals(diagnostics.CheapPrecheckSkipCount, 2, "reason change cheap skip count");
            AssertLongEquals(diagnostics.CheapSkipDiagnosticWrittenCount, 2, "reason change diagnostic write count");
            AssertLongEquals(diagnostics.CheapSkipDiagnosticSuppressedCount, 0, "reason change suppressed count");
            AssertStringEquals(diagnostics.CheapSkipLastReason, "playerNotControllable:cheap", "reason change last reason");
            AssertStringEquals(diagnostics.LastSkipReason, "playerNotControllable:cheap", "reason change snapshot reason");
            if (diagnostics.PlayerControllable)
            {
                throw new InvalidOperationException("Expected reason change full diagnostic to refresh player controllable state.");
            }
        }

        private static void SafeLandingCheapSkipWritesAfterException()
        {
            MovementSafeLandingService.ResetDiagnosticsForTesting();

            MovementSafeLandingService.RecordFullDecisionForTesting("exception", "exception:test", 5);
            MovementSafeLandingService.RecordCheapPrecheckSkipForTesting(6, "notFallingFastEnough:cheap", true);
            var diagnostics = MovementSafeLandingService.GetDiagnostics();
            AssertStringEquals(diagnostics.LastDecision, "skipped", "post-exception cheap skip decision");
            AssertLongEquals(diagnostics.CheapSkipDiagnosticWrittenCount, 1, "post-exception cheap skip diagnostic write count");
            AssertLongEquals(diagnostics.CheapSkipDiagnosticSuppressedCount, 0, "post-exception cheap skip suppressed count");
            AssertLongEquals(diagnostics.CheapPrecheckSkipCount, 1, "post-exception cheap skip count");
        }

        private static void SafeLandingSubmittedPathKeepsFullDiagnostics()
        {
            MovementSafeLandingService.ResetDiagnosticsForTesting();

            MovementSafeLandingService.RecordFullDecisionForTesting("submitted", string.Empty, 15);
            var diagnostics = MovementSafeLandingService.GetDiagnostics();
            AssertStringEquals(diagnostics.LastDecision, "submitted", "submitted full diagnostic decision");
            if (!diagnostics.LastTriggered)
            {
                throw new InvalidOperationException("Expected submitted full diagnostic to mark last triggered.");
            }

            if (string.IsNullOrWhiteSpace(diagnostics.StageSummary) ||
                string.IsNullOrWhiteSpace(diagnostics.RecoveryStateSummary))
            {
                throw new InvalidOperationException("Expected submitted full diagnostic to keep stage and recovery summaries.");
            }

            AssertLongEquals(diagnostics.CheapSkipDiagnosticWrittenCount, 0, "submitted cheap skip diagnostic write count");
            AssertLongEquals(diagnostics.CheapSkipDiagnosticSuppressedCount, 0, "submitted cheap skip suppressed count");
        }
    }
}
