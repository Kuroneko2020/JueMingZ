using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using Terraria.GameContent;

namespace JueMingZ.Automation.Combat
{
    internal static class CombatAutoBossDamageReportService
    {
        internal const string CommandText = "/bossdamage";
        private static readonly int[] EmptyAttemptIds = new int[0];
        private static readonly object SyncRoot = new object();
        private static readonly CombatAutoBossDamageReportState State = new CombatAutoBossDamageReportState();
        private static CombatAutoBossDamageReportDiagnosticInfo _diagnostics =
            CombatAutoBossDamageReportDiagnosticInfo.CreateDefault();
        private static long _sentCount;
        private static long _skippedCount;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            try
            {
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var enabled = settingsSnapshot.CombatAutoBossDamageReportEnabled;
                var inWorld = snapshot != null && snapshot.IsInWorld;
                List<int> attemptIds = null;
                var readFailureReason = string.Empty;
                if (enabled && inWorld && !TryReadRecentAttemptIds(out attemptIds, out readFailureReason))
                {
                    PublishDiagnostic(
                        enabled,
                        CombatAutoBossDamageReportDecision.Create(
                            false,
                            "unavailable",
                            readFailureReason,
                            0,
                            0,
                            0),
                        false,
                        false,
                        readFailureReason);
                    return;
                }

                if (!enabled || !inWorld)
                {
                    attemptIds = null;
                }

                CombatAutoBossDamageReportDecision decision;
                lock (SyncRoot)
                {
                    decision = EvaluateForTesting(enabled, inWorld, attemptIds, State);
                }

                var sendAttempted = false;
                var sendSucceeded = false;
                var failureReason = string.Empty;
                if (decision.ShouldSend)
                {
                    sendAttempted = true;
                    sendSucceeded = TerrariaChatAnnouncementCompat.Instance.TrySendChat(CommandText, out failureReason);
                    if (sendSucceeded)
                    {
                        _sentCount++;
                    }
                    else
                    {
                        LogThrottle.WarnThrottled(
                            "combat-auto-boss-damage-report-send-failed",
                            TimeSpan.FromSeconds(10),
                            "CombatAutoBossDamageReportService",
                            "Auto boss damage report failed through vanilla chat path: " + failureReason);
                    }
                }
                else
                {
                    _skippedCount++;
                }

                PublishDiagnostic(enabled, decision, sendAttempted, sendSucceeded, failureReason);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("CombatAutoBossDamageReportService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "combat-auto-boss-damage-report-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAutoBossDamageReportService",
                    "Auto boss damage report tick failed; exception swallowed.", error);
                PublishDiagnostic(
                    false,
                    CombatAutoBossDamageReportDecision.Create(
                        false,
                        "exception",
                        error.Message,
                        0,
                        0,
                        0),
                    false,
                    false,
                    error.Message);
            }
        }

        internal static CombatAutoBossDamageReportDecision EvaluateForTesting(
            bool enabled,
            bool inWorld,
            IList<int> recentAttemptIds,
            CombatAutoBossDamageReportState state)
        {
            state = state ?? new CombatAutoBossDamageReportState();
            recentAttemptIds = recentAttemptIds ?? EmptyAttemptIds;
            if (!enabled)
            {
                state.Clear();
                return CombatAutoBossDamageReportDecision.Create(false, "disabled", "feature disabled", recentAttemptIds.Count, 0, 0);
            }

            if (!inWorld)
            {
                state.Clear();
                return CombatAutoBossDamageReportDecision.Create(false, "outOfWorld", "not in world", recentAttemptIds.Count, 0, 0);
            }

            PruneReportedAttemptIds(state, recentAttemptIds);
            if (!state.Initialized)
            {
                AddAllAttempts(state, recentAttemptIds);
                state.Initialized = true;
                return CombatAutoBossDamageReportDecision.Create(
                    false,
                    recentAttemptIds.Count > 0 ? "baseline" : "noRecentAttempts",
                    recentAttemptIds.Count > 0 ? "existing recent attempts were baselined" : "no recent attempts",
                    recentAttemptIds.Count,
                    0,
                    0);
            }

            if (recentAttemptIds.Count <= 0)
            {
                return CombatAutoBossDamageReportDecision.Create(false, "noRecentAttempts", "no recent attempts", 0, 0, 0);
            }

            var newAttemptCount = 0;
            var firstNewAttemptId = 0;
            for (var index = 0; index < recentAttemptIds.Count; index++)
            {
                var attemptId = recentAttemptIds[index];
                if (state.ReportedAttemptIds.Add(attemptId))
                {
                    if (newAttemptCount == 0)
                    {
                        firstNewAttemptId = attemptId;
                    }

                    newAttemptCount++;
                }
            }

            if (newAttemptCount <= 0)
            {
                return CombatAutoBossDamageReportDecision.Create(
                    false,
                    "alreadyReported",
                    "all recent attempts already reported",
                    recentAttemptIds.Count,
                    0,
                    0);
            }

            return CombatAutoBossDamageReportDecision.Create(
                true,
                "send",
                "new recent boss damage attempt detected",
                recentAttemptIds.Count,
                newAttemptCount,
                firstNewAttemptId);
        }

        internal static CombatAutoBossDamageReportDiagnosticInfo GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        private static bool TryReadRecentAttemptIds(out List<int> attemptIds, out string failureReason)
        {
            attemptIds = new List<int>(3);
            failureReason = string.Empty;
            try
            {
                var recentAttempts = NPCDamageTracker.RecentAttempts();
                if (recentAttempts == null)
                {
                    return true;
                }

                foreach (var attempt in recentAttempts)
                {
                    if (attempt != null)
                    {
                        attemptIds.Add(RuntimeHelpers.GetHashCode(attempt));
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                failureReason = error.Message;
                return false;
            }
        }

        private static void PublishDiagnostic(
            bool enabled,
            CombatAutoBossDamageReportDecision decision,
            bool sendAttempted,
            bool sendSucceeded,
            string failureReason)
        {
            decision = decision ?? CombatAutoBossDamageReportDecision.Create(false, string.Empty, string.Empty, 0, 0, 0);
            lock (SyncRoot)
            {
                _diagnostics = new CombatAutoBossDamageReportDiagnosticInfo
                {
                    Enabled = enabled,
                    LastDecision = decision.Decision,
                    LastReason = decision.Reason,
                    LastDecisionUtc = DateTime.UtcNow,
                    LastRecentAttemptCount = decision.RecentAttemptCount,
                    LastNewAttemptCount = decision.NewAttemptCount,
                    LastAttemptKey = decision.AttemptKey,
                    LastSendAttempted = sendAttempted,
                    LastSendSucceeded = sendSucceeded,
                    LastFailureReason = failureReason ?? string.Empty,
                    SentCount = _sentCount,
                    SkippedCount = _skippedCount
                };
            }
        }

        private static void AddAllAttempts(CombatAutoBossDamageReportState state, IList<int> recentAttemptIds)
        {
            for (var index = 0; index < recentAttemptIds.Count; index++)
            {
                state.ReportedAttemptIds.Add(recentAttemptIds[index]);
            }
        }

        private static void PruneReportedAttemptIds(CombatAutoBossDamageReportState state, IList<int> recentAttemptIds)
        {
            if (state.ReportedAttemptIds.Count <= 0)
            {
                return;
            }

            var staleIds = new List<int>(state.ReportedAttemptIds.Count);
            foreach (var attemptId in state.ReportedAttemptIds)
            {
                if (!ContainsAttemptId(recentAttemptIds, attemptId))
                {
                    staleIds.Add(attemptId);
                }
            }

            for (var index = 0; index < staleIds.Count; index++)
            {
                state.ReportedAttemptIds.Remove(staleIds[index]);
            }
        }

        private static bool ContainsAttemptId(IList<int> recentAttemptIds, int attemptId)
        {
            for (var index = 0; index < recentAttemptIds.Count; index++)
            {
                if (recentAttemptIds[index] == attemptId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class CombatAutoBossDamageReportState
    {
        public CombatAutoBossDamageReportState()
        {
            ReportedAttemptIds = new HashSet<int>();
        }

        public bool Initialized { get; set; }
        public HashSet<int> ReportedAttemptIds { get; private set; }

        public void Clear()
        {
            Initialized = false;
            ReportedAttemptIds.Clear();
        }
    }

    internal sealed class CombatAutoBossDamageReportDecision
    {
        private CombatAutoBossDamageReportDecision()
        {
        }

        public bool ShouldSend { get; private set; }
        public string Decision { get; private set; }
        public string Reason { get; private set; }
        public int RecentAttemptCount { get; private set; }
        public int NewAttemptCount { get; private set; }
        public int AttemptKey { get; private set; }

        public static CombatAutoBossDamageReportDecision Create(
            bool shouldSend,
            string decision,
            string reason,
            int recentAttemptCount,
            int newAttemptCount,
            int attemptKey)
        {
            return new CombatAutoBossDamageReportDecision
            {
                ShouldSend = shouldSend,
                Decision = decision ?? string.Empty,
                Reason = reason ?? string.Empty,
                RecentAttemptCount = recentAttemptCount,
                NewAttemptCount = newAttemptCount,
                AttemptKey = attemptKey
            };
        }
    }

    internal sealed class CombatAutoBossDamageReportDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public string LastDecision { get; set; }
        public string LastReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int LastRecentAttemptCount { get; set; }
        public int LastNewAttemptCount { get; set; }
        public int LastAttemptKey { get; set; }
        public bool LastSendAttempted { get; set; }
        public bool LastSendSucceeded { get; set; }
        public string LastFailureReason { get; set; }
        public long SentCount { get; set; }
        public long SkippedCount { get; set; }

        public static CombatAutoBossDamageReportDiagnosticInfo CreateDefault()
        {
            return new CombatAutoBossDamageReportDiagnosticInfo
            {
                LastDecision = string.Empty,
                LastReason = string.Empty,
                LastFailureReason = string.Empty
            };
        }

        public CombatAutoBossDamageReportDiagnosticInfo Clone()
        {
            return new CombatAutoBossDamageReportDiagnosticInfo
            {
                Enabled = Enabled,
                LastDecision = LastDecision ?? string.Empty,
                LastReason = LastReason ?? string.Empty,
                LastDecisionUtc = LastDecisionUtc,
                LastRecentAttemptCount = LastRecentAttemptCount,
                LastNewAttemptCount = LastNewAttemptCount,
                LastAttemptKey = LastAttemptKey,
                LastSendAttempted = LastSendAttempted,
                LastSendSucceeded = LastSendSucceeded,
                LastFailureReason = LastFailureReason ?? string.Empty,
                SentCount = SentCount,
                SkippedCount = SkippedCount
            };
        }
    }
}
