using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.NpcServices
{
    public sealed class AutoTaxCollectorServiceDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int LastTargetNpcIndex { get; set; }
        public int LastTargetWhoAmI { get; set; }
        public string LastTargetName { get; set; }
        public int LastTaxMoney { get; set; }
        public string LastRequestId { get; set; }

        public AutoTaxCollectorServiceDiagnostics()
        {
            LastDecision = string.Empty;
            LastTargetNpcIndex = -1;
            LastTargetWhoAmI = -1;
            LastTargetName = string.Empty;
            LastRequestId = string.Empty;
        }
    }

    public static class AutoTaxCollectorService
    {
        private const long CheckIntervalTicks = 30;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static int _lastTargetNpcIndex = -1;
        private static int _lastTargetWhoAmI = -1;
        private static string _lastTargetName = string.Empty;
        private static int _lastTaxMoney;
        private static string _lastRequestId = string.Empty;

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
                RecordDecision("exception:" + error.GetType().Name, null, string.Empty);
                RuntimeDiagnostics.RecordError("AutoTaxCollectorService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-tax-collector-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoTaxCollectorService",
                    "Auto tax collector service failed; exception swallowed.", error);
            }
        }

        public static void ClearState(string decision)
        {
            ClearTracking(decision);
        }

        internal static InputActionRequest BuildAutoTaxCollectRequestForTesting(TaxCollectorTarget target)
        {
            return BuildAutoTaxCollectRequest(target);
        }

        internal static string GetExecutionBlockedReasonForTesting(GameStateSnapshot snapshot)
        {
            return GetExecutionBlockedReason(snapshot);
        }

        public static AutoTaxCollectorServiceDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoTaxCollectorServiceDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    LastTargetNpcIndex = _lastTargetNpcIndex,
                    LastTargetWhoAmI = _lastTargetWhoAmI,
                    LastTargetName = _lastTargetName,
                    LastTaxMoney = _lastTaxMoney,
                    LastRequestId = _lastRequestId
                };
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.NpcAutoTaxCollectEnabled)
            {
                ClearTracking("disabled");
                return;
            }

            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            if (!ShouldScan(tick))
            {
                return;
            }

            if (queue == null)
            {
                RecordDecision("queue unavailable", null, string.Empty);
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
                RecordDecision("player unavailable", null, string.Empty);
                return;
            }

            var blockedReason = GetExecutionBlockedReason(gameState);
            if (!string.IsNullOrWhiteSpace(blockedReason))
            {
                RecordDecision(blockedReason, null, string.Empty);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", null, string.Empty);
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision("local player unavailable", null, string.Empty);
                return;
            }

            TaxCollectorTarget target;
            string message;
            if (!TaxCollectorServiceCompat.TryFindReachableTaxCollector(player, out target, out message))
            {
                RecordDecision(message, null, string.Empty);
                return;
            }

            var request = BuildAutoTaxCollectRequest(target);
            var requestId = queue.Enqueue(request);
            RecordDecision("submitted auto tax collect request", target, requestId.ToString());
        }

        private static InputActionRequest BuildAutoTaxCollectRequest(TaxCollectorTarget target)
        {
            target = target ?? new TaxCollectorTarget();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.NpcInteract,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.NpcAutoTaxCollect,
                Description = "Auto collect tax collector money",
                QueueTimeout = TimeSpan.FromMilliseconds(500),
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.NpcAutoTaxCollect
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.NpcAutoTaxCollect;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["ExecutionMode"] = "TaxCollectorChatCollect";
            request.Metadata["Interaction"] = "TaxCollect";
            request.Metadata["NpcIndex"] = target.NpcIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["NpcType"] = TaxCollectorServiceCompat.TaxCollectorNpcType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["NpcWhoAmI"] = target.WhoAmI.ToString(CultureInfo.InvariantCulture);
            request.Metadata["NpcName"] = target.Name ?? string.Empty;
            request.Metadata["TaxMoneyBefore"] = target.TaxMoney.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static string GetExecutionBlockedReason(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return "blocked: not in world";
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                return "blocked: mouse item held";
            }

            if (snapshot.Ui == null)
            {
                return string.Empty;
            }

            if (snapshot.Ui.IsInMainMenu)
            {
                return "blocked: main menu";
            }

            if (snapshot.Ui.ChatOpen)
            {
                return "blocked: chat open";
            }

            if (snapshot.Ui.ChestOpen)
            {
                return "blocked: chest UI open";
            }

            if (snapshot.Ui.NpcChatOpen)
            {
                return "blocked: NPC chat open";
            }

            return string.Empty;
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
                   queueSnapshot.PendingCount > 0 ||
                   queueSnapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastDecision = decision ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastTargetNpcIndex = -1;
                _lastTargetWhoAmI = -1;
                _lastTargetName = string.Empty;
                _lastTaxMoney = 0;
                _lastRequestId = string.Empty;
            }
        }

        private static void RecordDecision(string decision, TaxCollectorTarget target, string requestId)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastTargetNpcIndex = target == null ? -1 : target.NpcIndex;
                _lastTargetWhoAmI = target == null ? -1 : target.WhoAmI;
                _lastTargetName = target == null ? string.Empty : target.Name ?? string.Empty;
                _lastTaxMoney = target == null ? 0 : target.TaxMoney;
                _lastRequestId = requestId ?? string.Empty;
            }
        }
    }
}
