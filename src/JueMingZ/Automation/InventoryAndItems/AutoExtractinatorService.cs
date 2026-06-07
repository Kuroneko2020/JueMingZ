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
    // Extractinator automation may choose an item and enqueue work only; slot writes stay inside the controlled executor path.
    public sealed class AutoExtractinatorDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int ItemSlot { get; set; }
        public int ItemType { get; set; }
        public int ExtractinatorTileX { get; set; }
        public int ExtractinatorTileY { get; set; }
        public int ExtractinatorTileType { get; set; }

        public AutoExtractinatorDiagnostics()
        {
            LastDecision = string.Empty;
            ItemSlot = -1;
            ExtractinatorTileX = -1;
            ExtractinatorTileY = -1;
        }
    }

    public static class AutoExtractinatorService
    {
        private const int ScanRadiusTiles = 12;
        private const long CheckIntervalTicks = 1;
        private const long UseCooldownTicks = 1;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastUseTick = -UseCooldownTicks;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static int _lastItemSlot = -1;
        private static int _lastItemType;
        private static int _lastTileX = -1;
        private static int _lastTileY = -1;
        private static int _lastTileType;

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
                RecordDecision("exception:" + error.GetType().Name, -1, 0, -1, -1, 0);
                RuntimeDiagnostics.RecordError("AutoExtractinatorService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-extractinator-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoExtractinatorService",
                    "Auto extractinator service failed; exception swallowed.",
                    error);
            }
        }

        public static AutoExtractinatorDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoExtractinatorDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    ItemSlot = _lastItemSlot,
                    ItemType = _lastItemType,
                    ExtractinatorTileX = _lastTileX,
                    ExtractinatorTileY = _lastTileY,
                    ExtractinatorTileType = _lastTileType
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
                _lastItemSlot = -1;
                _lastItemType = 0;
                _lastTileX = -1;
                _lastTileY = -1;
                _lastTileType = 0;
            }
        }

        internal static InputActionRequest BuildRequestForTesting(int slot, int itemType, string itemName, int tileX, int tileY, int tileType)
        {
            return BuildRequest(slot, itemType, itemName, tileX, tileY, tileType);
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settingsSnapshot.InventoryAutoExtractinatorEnabled)
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
                RecordDecision("queue unavailable", -1, 0, -1, -1, 0);
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
                RecordDecision(blockedReason, -1, 0, -1, -1, 0);
                return;
            }

            if (tick - _lastUseTick < UseCooldownTicks)
            {
                RecordDecision("cooldown", -1, 0, -1, -1, 0);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", -1, 0, -1, -1, 0);
                return;
            }

            object player;
            string playerMessage;
            if (!AutoMiningCompat.TryGetLocalPlayer(out player, out playerMessage) || player == null)
            {
                RecordDecision("player unavailable: " + playerMessage, -1, 0, -1, -1, 0);
                return;
            }

            var inventory = gameState == null || gameState.Inventory == null ? null : gameState.Inventory.Items;
            var selectedSlot = gameState != null && gameState.Inventory != null ? gameState.Inventory.SelectedItemSlot : -1;
            int slot;
            int itemType;
            int extractMode;
            string itemName;
            string itemMessage;
            if (!AutoExtractinatorCompat.TryFindExtractableInventoryItem(inventory, selectedSlot, out slot, out itemType, out extractMode, out itemName, out itemMessage) &&
                !AutoExtractinatorCompat.TryFindExtractableInventoryItem(player, selectedSlot, out slot, out itemType, out extractMode, out itemName, out itemMessage))
            {
                RecordDecision(itemMessage, -1, 0, -1, -1, 0);
                return;
            }

            int reachBoost;
            string reachMessage;
            if (!AutoExtractinatorCompat.TryReadExtractionReachBoost(player, slot, out reachBoost, out reachMessage))
            {
                reachBoost = 0;
            }

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            string tileContextMessage;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out tileContextMessage))
            {
                RecordDecision("tile context unavailable: " + tileContextMessage, slot, itemType, -1, -1, 0);
                return;
            }

            int tileX;
            int tileY;
            int tileType;
            string targetMessage;
            if (!AutoExtractinatorCompat.TryFindNearestExtractinator(
                tiles,
                maxTilesX,
                maxTilesY,
                player,
                ScanRadiusTiles,
                reachBoost,
                out tileX,
                out tileY,
                out tileType,
                out targetMessage))
            {
                RecordDecision(targetMessage, slot, itemType, -1, -1, 0);
                return;
            }

            var request = BuildRequest(slot, itemType, itemName, tileX, tileY, tileType);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("queue denied: " + (admission == null ? "unknown" : admission.Summary), slot, itemType, tileX, tileY, tileType);
                return;
            }

            _lastUseTick = tick;
            RecordDecision("submitted auto extractinator request", slot, itemType, tileX, tileY, tileType);
        }

        private static InputActionRequest BuildRequest(int slot, int itemType, string itemName, int tileX, int tileY, int tileType)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.InventoryAutoExtractinator,
                Description = "Auto extractinator use pulse",
                QueueTimeout = TimeSpan.FromMilliseconds(250),
                Timeout = TimeSpan.FromMilliseconds(900),
                AdmissionKey = FeatureIds.InventoryAutoExtractinator,
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.MouseTarget |
                                   InputActionChannel.InventorySlot |
                                   InputActionChannel.HotbarSelection |
                                   InputActionChannel.BridgeItemUse
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.InventoryAutoExtractinator;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.TargetSlot] = slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = AutoExtractinatorCompat.TileCenterWorldX(tileX).ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = AutoExtractinatorCompat.TileCenterWorldY(tileY).ToString("0.###", CultureInfo.InvariantCulture);
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["AllowEarlyItemCheck"] = "true";
            request.Metadata["EarlyItemCheckWindowTicks"] = "2";
            request.Metadata["AutoExtractinatorTileX"] = tileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoExtractinatorTileY"] = tileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoExtractinatorTileType"] = tileType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoExtractinatorItemType"] = itemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoExtractinatorItemName"] = itemName ?? string.Empty;
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
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void RecordDecision(string decision, int slot, int itemType, int tileX, int tileY, int tileType)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastDecisionUtc = DateTime.UtcNow;
                _lastItemSlot = slot;
                _lastItemType = itemType;
                _lastTileX = tileX;
                _lastTileY = tileY;
                _lastTileType = tileType;
            }
        }
    }
}
