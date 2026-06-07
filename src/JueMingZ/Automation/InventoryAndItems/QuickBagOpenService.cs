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
    // Quick bag open resolves the requested bag slot, then lets ActionQueue own mouse target and item-use input.
    public sealed class QuickBagOpenDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int BagSlot { get; set; }
        public int BagItemType { get; set; }
        public string BagItemName { get; set; }

        public QuickBagOpenDiagnostics()
        {
            LastDecision = string.Empty;
            BagSlot = -1;
            BagItemName = string.Empty;
        }
    }

    public static class QuickBagOpenService
    {
        private const long CheckIntervalTicks = 1;
        private const long UseCooldownTicks = 1;
        private const int RapidOpenRepeatCount = 8;
        private const long CleanupYieldTicks = 24;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastUseTick = -UseCooldownTicks;
        private static long _cleanupYieldUntilTick = -1;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static int _lastBagSlot = -1;
        private static int _lastBagItemType;
        private static string _lastBagItemName = string.Empty;

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
                RuntimeDiagnostics.RecordError("QuickBagOpenService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "quick-bag-open-service-error",
                    TimeSpan.FromSeconds(10),
                    "QuickBagOpenService",
                    "Quick bag open service failed; exception swallowed.",
                    error);
            }
        }

        public static void ClearState(string reason)
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastUseTick = -UseCooldownTicks;
                _cleanupYieldUntilTick = -1;
                _lastDecision = string.IsNullOrWhiteSpace(reason) ? "cleared" : reason;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastBagSlot = -1;
                _lastBagItemType = 0;
                _lastBagItemName = string.Empty;
            }
        }

        public static QuickBagOpenDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new QuickBagOpenDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    BagSlot = _lastBagSlot,
                    BagItemType = _lastBagItemType,
                    BagItemName = _lastBagItemName
                };
            }
        }

        internal static InputActionRequest BuildRequestForTesting(int slot, int itemType, string itemName)
        {
            return BuildRequest(slot, itemType, itemName);
        }

        internal static bool IsCleanupYieldActiveForTesting(long tick)
        {
            return IsCleanupYieldActive(tick);
        }

        internal static void BeginCleanupYieldForTesting(long tick, bool cleanupEnabled)
        {
            BeginCleanupYield(tick, cleanupEnabled);
        }

        internal static bool IsCleanupYieldActiveForAutomation(long tick)
        {
            return IsCleanupYieldActive(tick);
        }

        public static void HandleItemSlotRightClickPrefix(object inventory, int context, int slot)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.InventoryQuickBagOpenEnabled)
            {
                QuickBagOpenCompat.ResetHookPulseCooldown();
                return;
            }

            var tick = JueMingZRuntime.State == null ? 0 : JueMingZRuntime.State.UpdateCount;
            if (IsCleanupYieldActive(tick))
            {
                QuickBagOpenCompat.ResetHookPulseCooldown();
                RecordDecision("hook: yielding for inventory cleanup", slot, 0, string.Empty);
                return;
            }

            int itemType;
            string itemName;
            string message;
            if (!QuickBagOpenCompat.TryApplyItemSlotRightClickReleasePulse(inventory, context, slot, out itemType, out itemName, out message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    RecordDecision("hook: " + message, slot, itemType, itemName);
                }

                return;
            }

            RecordDecision("hook pulse: ItemSlot.RightClick release", slot, itemType, itemName);
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.InventoryQuickBagOpenEnabled)
            {
                ClearState("disabled");
                return;
            }

            var cleanupEnabled = IsCleanupAutomationEnabled(settingsSnapshot);
            if (!cleanupEnabled)
            {
                ClearCleanupYield();
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

            string inputMessage;
            if (!QuickBagOpenCompat.TryIsRapidOpenInputActive(out inputMessage))
            {
                RecordDecision("input gate: " + inputMessage, -1, 0, string.Empty);
                return;
            }

            if (cleanupEnabled && IsCleanupYieldActive(tick))
            {
                RecordDecision("yielding for inventory cleanup", -1, 0, string.Empty);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", -1, 0, string.Empty);
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                RecordDecision("player unavailable: " + TerrariaInputCompat.LastInputCompatError, -1, 0, string.Empty);
                return;
            }

            var preferredType = 0;
            QuickBagOpenCompat.TryReadHoveredItemType(out preferredType);

            int slot;
            int itemType;
            int stack;
            string itemName;
            string bagMessage;
            if (!QuickBagOpenCompat.TryFindBagSlot(player, preferredType, out slot, out itemType, out stack, out itemName, out bagMessage))
            {
                RecordDecision(bagMessage, -1, 0, string.Empty);
                return;
            }

            var request = BuildRequest(slot, itemType, itemName);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("queue denied: " + (admission == null ? "unknown" : admission.Summary), slot, itemType, itemName);
                return;
            }

            _lastUseTick = tick;
            BeginCleanupYield(tick, cleanupEnabled);
            RecordDecision(cleanupEnabled ? "submitted quick bag open; yielding for inventory cleanup" : "submitted quick bag open", slot, itemType, itemName);
        }

        private static InputActionRequest BuildRequest(int slot, int itemType, string itemName)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryQuickBagOpen,
                Description = "Quick bag open pulse",
                QueueTimeout = TimeSpan.FromMilliseconds(100),
                Timeout = TimeSpan.FromMilliseconds(700),
                AdmissionKey = FeatureIds.InventoryQuickBagOpen,
                RequiredChannels = InputActionChannel.InventorySlot
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryQuickBagOpen;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.TargetSlot] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["QuickBagOpenItemType"] = itemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["QuickBagOpenItemName"] = itemName ?? string.Empty;
            request.Metadata["QuickBagOpenRepeatCount"] = RapidOpenRepeatCount.ToString(CultureInfo.InvariantCulture);
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
                   !snapshot.Player.Ghost;
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
                return "blocked: ui unavailable";
            }

            if (!snapshot.Ui.PlayerInventoryOpen)
            {
                return "blocked: inventory closed";
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

            if (snapshot.Ui.ChestOpen)
            {
                return "blocked: chest open";
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
            var fast = queue == null ? null : queue.GetFastState();
            return fast == null || fast.PendingCount > 0 || fast.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static bool IsCleanupAutomationEnabled(RuntimeSettingsSnapshot settingsSnapshot)
        {
            return settingsSnapshot != null &&
                   (settingsSnapshot.InventoryAutoStackEnabled ||
                    settingsSnapshot.InventoryAutoSellEnabled ||
                    settingsSnapshot.InventoryAutoDiscardEnabled);
        }

        private static void BeginCleanupYield(long tick, bool cleanupEnabled)
        {
            lock (SyncRoot)
            {
                _cleanupYieldUntilTick = cleanupEnabled
                    ? tick + CleanupYieldTicks
                    : -1;
            }
        }

        private static void ClearCleanupYield()
        {
            lock (SyncRoot)
            {
                _cleanupYieldUntilTick = -1;
            }
        }

        private static bool IsCleanupYieldActive(long tick)
        {
            lock (SyncRoot)
            {
                return _cleanupYieldUntilTick >= 0 &&
                       tick >= 0 &&
                       tick <= _cleanupYieldUntilTick;
            }
        }

        private static void RecordDecision(string decision, int slot, int itemType, string itemName)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastBagSlot = slot;
                _lastBagItemType = itemType;
                _lastBagItemName = itemName ?? string.Empty;
            }
        }
    }
}
