using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.InventoryAndItems
{
    // Keep-favorited detects policy drift from snapshots and queues a slot action; it must not flip inventory flags directly.
    public sealed class KeepFavoritedDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int Slot { get; set; }
        public int ItemType { get; set; }
        public string Signature { get; set; }

        public KeepFavoritedDiagnostics()
        {
            LastDecision = string.Empty;
            Slot = -1;
            Signature = string.Empty;
        }
    }

    public static class KeepFavoritedService
    {
        private const long CheckIntervalTicks = 2;
        private const long UseCooldownTicks = 2;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastUseTick = -UseCooldownTicks;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static int _lastSlot = -1;
        private static int _lastItemType;
        private static string _lastSignature = string.Empty;

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
                RecordDecision("exception:" + error.GetType().Name, -1, 0, string.Empty);
                RuntimeDiagnostics.RecordError("KeepFavoritedService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "keep-favorited-service-error",
                    TimeSpan.FromSeconds(10),
                    "KeepFavoritedService",
                    "Keep favorited service failed; exception swallowed.",
                    error);
            }
        }

        public static KeepFavoritedDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new KeepFavoritedDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    Slot = _lastSlot,
                    ItemType = _lastItemType,
                    Signature = _lastSignature
                };
            }
        }

        public static void ClearState(string reason)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastUseTick = -UseCooldownTicks;
                _lastDecision = string.IsNullOrWhiteSpace(reason) ? "cleared" : reason;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastSlot = -1;
                _lastItemType = 0;
                _lastSignature = string.Empty;
                KeepFavoritedCompat.ClearState();
            }
        }

        internal static InputActionRequest BuildRequestForTesting(int slot, int itemType, string signature)
        {
            return BuildRequest("Inventory", slot, itemType, signature);
        }

        internal static InputActionRequest BuildRequestForTesting(string container, int slot, int itemType, string signature)
        {
            return BuildRequest(container, slot, itemType, signature);
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.InventoryKeepFavoritedEnabled)
            {
                ClearState("disabled");
                return;
            }

            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            if (!ShouldScan(tick))
            {
                return;
            }

            if (queue == null)
            {
                RecordDecision("queue unavailable", -1, 0, string.Empty);
                return;
            }

            if (!CanRun(gameState))
            {
                ClearState("player unavailable");
                return;
            }

            var blockedReason = GetBlockedReason(gameState);
            if (!string.IsNullOrWhiteSpace(blockedReason))
            {
                RecordDecision(blockedReason, -1, 0, string.Empty);
                return;
            }

            if (tick - _lastUseTick < UseCooldownTicks)
            {
                RecordDecision("cooldown", -1, 0, string.Empty);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", -1, 0, string.Empty);
                return;
            }

            string container;
            int slot;
            int itemType;
            string signature;
            string message;
            if (!KeepFavoritedCompat.TryFindLostFavoritedSlot(gameState, tick, out container, out slot, out itemType, out signature, out message))
            {
                RecordDecision(message, -1, 0, string.Empty);
                return;
            }

            var request = BuildRequest(container, slot, itemType, signature);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("queue denied: " + (admission == null ? "unknown" : admission.Summary), slot, itemType, signature);
                return;
            }

            _lastUseTick = tick;
            RecordDecision("submitted keep favorited request", slot, itemType, signature);
        }

        private static InputActionRequest BuildRequest(string container, int slot, int itemType, string signature)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryKeepFavorited,
                Description = "Keep favorited restore pulse",
                QueueTimeout = TimeSpan.FromMilliseconds(100),
                Timeout = TimeSpan.FromMilliseconds(700),
                AdmissionKey = FeatureIds.InventoryKeepFavorited,
                RequiredChannels = InputActionChannel.InventorySlot
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryKeepFavorited;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.TargetSlot] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["SourceContainer"] = string.IsNullOrWhiteSpace(container) ? "Inventory" : container;
            request.Metadata["KeepFavoritedContainer"] = string.IsNullOrWhiteSpace(container) ? "Inventory" : container;
            request.Metadata["KeepFavoritedItemType"] = itemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["KeepFavoritedSignature"] = signature ?? string.Empty;
            return request;
        }

        private static bool CanRun(GameStateSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.IsInWorld &&
                   snapshot.Player != null &&
                   snapshot.Player.Exists &&
                   snapshot.Player.Active &&
                   !snapshot.Player.Dead &&
                   !snapshot.Player.Ghost &&
                   snapshot.Inventory != null &&
                   snapshot.Inventory.Items != null;
        }

        private static string GetBlockedReason(GameStateSnapshot snapshot)
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
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void RecordDecision(string decision, int slot, int itemType, string signature)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastSlot = slot;
                _lastItemType = itemType;
                _lastSignature = signature ?? string.Empty;
            }
        }
    }
}
