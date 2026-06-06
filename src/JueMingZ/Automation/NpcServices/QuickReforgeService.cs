using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.NpcServices
{
    public sealed class QuickReforgeServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public string LastTargetPrefixes { get; set; }
        public string LastMatchedPrefix { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public QuickReforgeServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastTargetPrefixes = string.Empty;
            LastMatchedPrefix = string.Empty;
        }
    }

    public static class QuickReforgeService
    {
        private const long CheckIntervalTicks = 1;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static string _lastDecision = string.Empty;
        private static string _lastTargetPrefixes = string.Empty;
        private static string _lastMatchedPrefix = string.Empty;
        private static DateTime? _lastDecisionUtc;

        public static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState)
        {
            Tick(queue, gameState, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            try
            {
                TickCore(queue, gameState, runtimeState, settingsSnapshot);
            }
            catch (Exception error)
            {
                RecordDecision("exception:" + error.GetType().Name, string.Empty, string.Empty);
                RuntimeDiagnostics.RecordError("QuickReforgeService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "quick-reforge-service-error",
                    TimeSpan.FromSeconds(10),
                    "QuickReforgeService",
                    "Quick reforge service failed; exception swallowed.", error);
            }
        }

        internal static List<string> NormalizeTargetPrefixesForTesting(IList<string> prefixes)
        {
            return NormalizeTargetPrefixes(prefixes);
        }

        internal static InputActionRequest BuildQuickReforgeRequestForTesting(IReadOnlyList<string> prefixes, string currentAffix)
        {
            return BuildQuickReforgeRequest(prefixes, currentAffix);
        }

        public static QuickReforgeServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new QuickReforgeServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastTargetPrefixes = _lastTargetPrefixes,
                    LastMatchedPrefix = _lastMatchedPrefix,
                    LastDecisionUtc = _lastDecisionUtc
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
            if (!settingsSnapshot.NpcAutoReforgeEnabled)
            {
                ClearTracking("disabled");
                return;
            }

            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            if (!ShouldScan(tick))
            {
                return;
            }

            var prefixes = NormalizeTargetPrefixes(settings.NpcAutoReforgePrefixes);
            if (prefixes.Count <= 0)
            {
                RecordDecision("quick reforge list empty", string.Empty, string.Empty);
                return;
            }

            if (queue == null)
            {
                RecordDecision("queue unavailable", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            if (gameState == null ||
                !gameState.IsInWorld ||
                gameState.Player == null ||
                !gameState.Player.Exists ||
                !gameState.Player.Active ||
                gameState.Player.Dead ||
                gameState.Player.Ghost)
            {
                RecordDecision("player unavailable", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            if (gameState.Ui != null &&
                (gameState.Ui.IsInMainMenu || gameState.Ui.ChatOpen || gameState.Ui.ChestOpen))
            {
                RecordDecision("blocked by UI", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision("local player unavailable", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            bool useItemHeld;
            if (!TerrariaInputCompat.TryReadUseItemHeld(player, out useItemHeld) || !useItemHeld)
            {
                RecordDecision("reforge key not held", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            bool ready;
            string readyMessage;
            string currentAffix;
            if (!ReforgeCompat.TryReadReforgeReadyState(out ready, out readyMessage, out currentAffix))
            {
                RecordDecision("reforge compat unavailable: " + readyMessage, JoinPrefixes(prefixes), string.Empty);
                return;
            }

            if (!ready)
            {
                RecordDecision(readyMessage, JoinPrefixes(prefixes), string.Empty);
                return;
            }

            string matchedPrefix;
            if (ReforgeCompat.TryMatchTargetPrefixText(prefixes, currentAffix, out matchedPrefix))
            {
                RecordDecision("already matched target prefix", JoinPrefixes(prefixes), matchedPrefix);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", JoinPrefixes(prefixes), string.Empty);
                return;
            }

            var request = BuildQuickReforgeRequest(prefixes, currentAffix);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("quick reforge admission denied: " + (admission == null ? "unknown" : admission.Reason), JoinPrefixes(prefixes), string.Empty);
                return;
            }

            RecordDecision("submitted quick reforge request", JoinPrefixes(prefixes), string.Empty);
        }

        private static InputActionRequest BuildQuickReforgeRequest(IReadOnlyList<string> prefixes, string currentAffix)
        {
            prefixes = prefixes ?? new List<string>();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Reforge,
                Priority = InputActionPriority.High,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.NpcAutoReforge,
                Description = "Quick reforge until target prefix is matched",
                QueueTimeout = TimeSpan.FromMilliseconds(300),
                Timeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.NpcAutoReforge
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.NpcQuickReforge;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["TargetPrefixes"] = JoinPrefixes(prefixes);
            request.Metadata["CurrentAffix"] = currentAffix ?? string.Empty;
            return request;
        }

        private static List<string> NormalizeTargetPrefixes(IList<string> prefixes)
        {
            var source = prefixes ?? new List<string>();
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < source.Count; index++)
            {
                var raw = source[index];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var value = raw.Trim();
                if (value.Length <= 0 || !seen.Add(value))
                {
                    continue;
                }

                normalized.Add(value);
            }

            return normalized;
        }

        private static bool ShouldScan(long tick)
        {
            lock (SyncRoot)
            {
                if (tick - _lastScanTick >= CheckIntervalTicks || tick < _lastScanTick)
                {
                    _lastScanTick = tick;
                    return true;
                }

                return false;
            }
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var queueSnapshot = queue == null ? null : queue.GetFastState();
            return queueSnapshot == null ||
                   queueSnapshot.PendingCount > 0 || queueSnapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastDecision = decision ?? string.Empty;
                _lastTargetPrefixes = string.Empty;
                _lastMatchedPrefix = string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static void RecordDecision(string decision, string targetPrefixes, string matchedPrefix)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastTargetPrefixes = targetPrefixes ?? string.Empty;
                _lastMatchedPrefix = matchedPrefix ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
            }
        }

        private static string JoinPrefixes(IReadOnlyList<string> prefixes)
        {
            if (prefixes == null || prefixes.Count <= 0)
            {
                return string.Empty;
            }

            var parts = new string[prefixes.Count];
            for (var index = 0; index < prefixes.Count; index++)
            {
                parts[index] = prefixes[index] ?? string.Empty;
            }

            return string.Join(",", parts);
        }
    }
}
