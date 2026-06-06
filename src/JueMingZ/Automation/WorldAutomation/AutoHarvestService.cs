using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.WorldAutomation
{
    public static class AutoHarvestService
    {
        private const int ScanRadiusTiles = 10;
        private const int MaxPendingReplants = 48;
        private const long CheckIntervalTicks = 1;
        private const long UseCooldownTicks = 12;
        private const long ReplantWindowTicks = 600;
        private static readonly object SyncRoot = new object();
        private static readonly List<AutoHarvestPendingReplant> PendingReplants = new List<AutoHarvestPendingReplant>();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastUseTick = -UseCooldownTicks;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static string _lastAction = string.Empty;
        private static int _lastToolSlot = -1;
        private static int _lastToolItemType;
        private static int _lastTargetTileX = -1;
        private static int _lastTargetTileY = -1;
        private static int _lastTargetSeedItemType;

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
                RecordDecision("exception:" + error.GetType().Name, null, null, string.Empty);
                RuntimeDiagnostics.RecordError("AutoHarvestService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-harvest-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoHarvestService",
                    "Auto harvest service failed; exception swallowed.", error);
            }
        }

        public static AutoHarvestDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoHarvestDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    LastAction = _lastAction,
                    ToolSlot = _lastToolSlot,
                    ToolItemType = _lastToolItemType,
                    TargetTileX = _lastTargetTileX,
                    TargetTileY = _lastTargetTileY,
                    TargetSeedItemType = _lastTargetSeedItemType,
                    PendingReplantCount = PendingReplants.Count
                };
            }
        }

        public static void ClearState(string reason)
        {
            AutoHarvestSustainedUseBridge.ClearDesiredTarget(string.IsNullOrWhiteSpace(reason) ? "state cleared" : reason);
            ClearTracking(string.IsNullOrWhiteSpace(reason) ? "state cleared" : reason);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                PendingReplants.Clear();
                _lastScanTick = -CheckIntervalTicks;
                _lastUseTick = -UseCooldownTicks;
                _lastDecision = string.Empty;
                _lastDecisionUtc = null;
                _lastAction = string.Empty;
                _lastToolSlot = -1;
                _lastToolItemType = 0;
                _lastTargetTileX = -1;
                _lastTargetTileY = -1;
                _lastTargetSeedItemType = 0;
            }

            AutoHarvestSustainedUseBridge.ClearDesiredTarget("auto harvest reset for testing");
        }

        internal static bool TryResolveSeedItemTypeForTesting(int herbStyle, out int seedItemType)
        {
            return AutoHarvestCompat.TryGetSeedItemTypeForHerbStyle(herbStyle, out seedItemType);
        }

        internal static bool IsRegrowthToolForTesting(int itemType)
        {
            return AutoHarvestCompat.IsRegrowthToolItemType(itemType);
        }

        internal static InputActionRequest BuildHarvestRequestForTesting(int toolSlot, int toolItemType, int tileX, int tileY, int tileType, int herbStyle, int seedItemType)
        {
            return BuildHarvestRequest(
                new AutoHarvestToolCandidate
                {
                    Slot = toolSlot,
                    ItemType = toolItemType,
                    ItemName = "Staff of Regrowth",
                    Stack = 1
                },
                new AutoHarvestHerbTarget
                {
                    TileX = tileX,
                    TileY = tileY,
                    TileType = tileType,
                    HerbStyle = herbStyle,
                    SeedItemType = seedItemType
                });
        }

        internal static InputActionRequest BuildSustainedHarvestRequestForTesting(int toolSlot, int toolItemType, int tileX, int tileY, int tileType, int herbStyle, int seedItemType)
        {
            return BuildSustainedHarvestRequest(
                new AutoHarvestToolCandidate
                {
                    Slot = toolSlot,
                    ItemType = toolItemType,
                    ItemName = "Staff of Regrowth",
                    Stack = 1
                },
                new AutoHarvestHerbTarget
                {
                    TileX = tileX,
                    TileY = tileY,
                    TileType = tileType,
                    HerbStyle = herbStyle,
                    SeedItemType = seedItemType
                });
        }

        internal static InputActionRequest BuildReplantRequestForTesting(int seedSlot, int seedItemType, int tileX, int tileY, int herbStyle)
        {
            return BuildReplantRequest(
                seedSlot,
                seedItemType,
                new AutoHarvestPendingReplant
                {
                    TileX = tileX,
                    TileY = tileY,
                    HerbStyle = herbStyle,
                    SeedItemType = seedItemType
                });
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var tick = runtimeState == null ? AutoMiningCompat.ReadGameUpdateCount() : runtimeState.UpdateCount;

            if (!settingsSnapshot.WorldAutomationAutoHarvestEnabled)
            {
                RecordFairnessUnavailable(tick, "disabled");
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("auto harvest disabled");
                ClearTracking("disabled");
                return;
            }

            if (!ShouldScan(tick))
            {
                return;
            }

            PruneExpiredPendingReplants(tick);

            if (queue == null)
            {
                RecordFairnessUnavailable(tick, "queue unavailable");
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("auto harvest queue unavailable");
                RecordDecision("queue unavailable", null, null, string.Empty);
                return;
            }

            if (!CanRun(gameState))
            {
                RecordFairnessUnavailable(tick, "player unavailable");
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("auto harvest player unavailable");
                ClearTracking("player unavailable");
                return;
            }

            var blockedReason = GetExecutionBlockedReason(gameState);
            if (!string.IsNullOrEmpty(blockedReason))
            {
                RecordFairnessUnavailable(tick, blockedReason);
                AutoHarvestSustainedUseBridge.ClearDesiredTarget(blockedReason);
                RecordDecision(blockedReason, null, null, string.Empty);
                return;
            }

            AutoHarvestToolCandidate tool;
            string toolMessage;
            if (!TryFindRegrowthTool(gameState, out tool, out toolMessage))
            {
                RecordFairnessUnavailable(tick, toolMessage);
                AutoHarvestSustainedUseBridge.ClearDesiredTarget(toolMessage);
                RecordDecision(toolMessage, null, null, string.Empty);
                return;
            }

            if (HasPendingWorkThatShouldPreemptHarvest(queue))
            {
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("queue pending; releasing auto harvest");
                RecordDecision("queue busy", tool, null, string.Empty);
                return;
            }

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            string message;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("tile context unavailable");
                RecordDecision("tile context unavailable: " + message, tool, null, string.Empty);
                return;
            }

            object player;
            if (!AutoMiningCompat.TryGetLocalPlayer(out player, out message))
            {
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("player unavailable");
                RecordDecision("player unavailable: " + message, tool, null, string.Empty);
                return;
            }

            bool playerUsingItem;
            if (TerrariaInputCompat.TryReadPhysicalUseItemHeld(player, out playerUsingItem) && playerUsingItem)
            {
                AutoHarvestSustainedUseBridge.ClearDesiredTarget("player is using item");
                RecordDecision("blocked: player is using item", tool, null, string.Empty);
                return;
            }

            AutoHarvestHerbTarget target;
            if (TryFindHarvestTarget(tiles, maxTilesX, maxTilesY, player, tool, out target, out message))
            {
                var queueSnapshot = queue.GetFastState();
                var activeContinuation = IsRunningAutoHarvestSustainedUse(queueSnapshot);
                if (WorldAutomationFairnessCoordinator.ShouldDeferRuntimeSubmission(
                        WorldAutomationFairnessKind.AutoHarvest,
                        tick,
                        activeContinuation))
                {
                    AutoHarvestSustainedUseBridge.ClearDesiredTarget("fairness deferred to auto capture");
                    RecordDecision("fairness deferred to auto capture", tool, target, "Harvest");
                    return;
                }

                var desiredTarget = BuildSustainedUseTarget(tool, target, player, tick);
                AutoHarvestSustainedUseBridge.SetDesiredTarget(desiredTarget);
                if (!EnsureSustainedHarvestRequest(queue, tool, target))
                {
                    AutoHarvestSustainedUseBridge.ClearDesiredTarget("auto harvest sustained request was not admitted");
                    RecordDecision("queue denied sustained harvest", tool, target, "Harvest");
                    return;
                }

                AddOrRefreshPendingReplant(target, tick);
                RecordDecision("sustained harvest target refreshed", tool, target, "Harvest");
                return;
            }

            RecordFairnessUnavailable(tick, message);
            AutoHarvestSustainedUseBridge.ClearDesiredTarget(message);
            if (tick - _lastUseTick < UseCooldownTicks)
            {
                RecordDecision("cooldown", tool, null, string.Empty);
                return;
            }

            if (IsQueueBusy(queue))
            {
                RecordDecision("queue busy", tool, null, string.Empty);
                return;
            }

            if (TrySubmitPendingReplant(queue, gameState, tiles, player, tool, tick))
            {
                return;
            }

            RecordDecision(message, tool, null, string.Empty);
        }

        private static void RecordFairnessUnavailable(long tick, string reason)
        {
            WorldAutomationFairnessCoordinator.RecordCandidateUnavailable(
                WorldAutomationFairnessKind.AutoHarvest,
                tick,
                reason ?? string.Empty);
        }

        private static bool TrySubmitPendingReplant(InputActionQueue queue, GameStateSnapshot gameState, object tiles, object player, AutoHarvestToolCandidate tool, long tick)
        {
            AutoHarvestPendingReplant pending = null;
            AutoHarvestPlantSpot blockedSpot = null;
            lock (SyncRoot)
            {
                for (var index = PendingReplants.Count - 1; index >= 0; index--)
                {
                    var current = PendingReplants[index];
                    AutoHarvestPlantSpot spot;
                    if (!AutoHarvestCompat.TryReadPlantSpot(tiles, current.TileX, current.TileY, out spot) || spot == null || !spot.Supported)
                    {
                        PendingReplants.RemoveAt(index);
                        continue;
                    }

                    if (spot.HasSameHerb(current.HerbStyle))
                    {
                        PendingReplants.RemoveAt(index);
                        continue;
                    }

                    if (!AutoHarvestCompat.IsPlantingSpotEmpty(spot))
                    {
                        PendingReplants.RemoveAt(index);
                        blockedSpot = spot;
                        continue;
                    }

                    if (!AutoMiningCompat.IsTileInMiningReach(player, current.TileX, current.TileY, tool == null ? 0 : tool.TileBoost))
                    {
                        continue;
                    }

                    pending = current;
                    break;
                }
            }

            if (pending == null)
            {
                if (blockedSpot != null)
                {
                    RecordDecision("pending replant dropped: spot occupied", tool, null, "Replant");
                }

                return false;
            }

            int seedSlot;
            if (!TryFindSeedSlot(gameState, pending.SeedItemType, out seedSlot))
            {
                RecordDecision("pending replant waiting for matching seed", tool, PendingToTarget(pending), "Replant");
                return false;
            }

            var request = BuildReplantRequest(seedSlot, pending.SeedItemType, pending);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("replant denied: " + (admission == null ? "unknown" : admission.Summary), tool, PendingToTarget(pending), "Replant");
                return true;
            }

            _lastUseTick = tick;
            RecordDecision("submitted replant request", tool, PendingToTarget(pending), "Replant");
            return true;
        }

        private static bool TryFindRegrowthTool(GameStateSnapshot gameState, out AutoHarvestToolCandidate tool, out string message)
        {
            tool = null;
            message = string.Empty;
            var inventory = gameState == null || gameState.Inventory == null
                ? null
                : gameState.Inventory.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            var selectedSlot = gameState.Inventory.SelectedItemSlot;
            for (var index = 0; index < inventory.Count && index < 50; index++)
            {
                var item = inventory[index];
                if (item == null || item.Type <= 0 || item.Stack <= 0 || !AutoHarvestCompat.IsRegrowthToolItemType(item.Type))
                {
                    continue;
                }

                var candidate = new AutoHarvestToolCandidate
                {
                    Slot = item.SlotIndex,
                    ItemType = item.Type,
                    ItemName = item.Name ?? string.Empty,
                    Stack = item.Stack,
                    TileBoost = 0,
                    IsSelected = item.SlotIndex == selectedSlot
                };

                if (tool == null || IsBetterToolCandidate(candidate, tool))
                {
                    tool = candidate;
                }
            }

            if (tool == null)
            {
                message = "no Staff of Regrowth or Axe of Regrowth in inventory";
                return false;
            }

            return true;
        }

        private static bool IsBetterToolCandidate(AutoHarvestToolCandidate candidate, AutoHarvestToolCandidate current)
        {
            if (candidate == null)
            {
                return false;
            }

            if (current == null)
            {
                return true;
            }

            if (candidate.IsSelected != current.IsSelected)
            {
                return candidate.IsSelected;
            }

            if (candidate.ItemType != current.ItemType)
            {
                return candidate.ItemType == AutoHarvestCompat.AxeOfRegrowthItemType;
            }

            return candidate.Slot < current.Slot;
        }

        private static bool TryFindHarvestTarget(object tiles, int maxTilesX, int maxTilesY, object player, AutoHarvestToolCandidate tool, out AutoHarvestHerbTarget target, out string message)
        {
            target = null;
            message = string.Empty;
            int centerTileX;
            int centerTileY;
            if (!AutoMiningCompat.TryGetPlayerCenterTile(out centerTileX, out centerTileY, out message))
            {
                message = "player tile unavailable: " + message;
                return false;
            }

            float playerCenterX;
            float playerCenterY;
            if (!AutoMiningCompat.TryGetMiningCenterWorld(player, out playerCenterX, out playerCenterY))
            {
                playerCenterX = centerTileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
                playerCenterY = centerTileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            }

            var minX = Math.Max(0, centerTileX - ScanRadiusTiles);
            var maxX = Math.Min(maxTilesX - 1, centerTileX + ScanRadiusTiles);
            var minY = Math.Max(0, centerTileY - ScanRadiusTiles);
            var maxY = Math.Min(maxTilesY - 2, centerTileY + ScanRadiusTiles);
            var bestDistance = float.MaxValue;
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    AutoHarvestHerbTarget current;
                    if (!AutoHarvestCompat.TryReadHarvestableHerb(tiles, x, y, out current))
                    {
                        continue;
                    }

                    if (!AutoMiningCompat.IsTileInMiningReach(player, x, y, tool == null ? 0 : tool.TileBoost))
                    {
                        continue;
                    }

                    var dx = AutoHarvestCompat.TileCenterWorldX(x) - playerCenterX;
                    var dy = AutoHarvestCompat.TileCenterWorldY(y) - playerCenterY;
                    var distance = dx * dx + dy * dy;
                    if (target == null ||
                        distance < bestDistance ||
                        (Math.Abs(distance - bestDistance) < 0.001f && (y < target.TileY || (y == target.TileY && x < target.TileX))))
                    {
                        target = current;
                        bestDistance = distance;
                    }
                }
            }

            if (target == null)
            {
                message = "no harvestable potted herb nearby";
                return false;
            }

            return true;
        }

        private static bool TryFindSeedSlot(GameStateSnapshot gameState, int seedItemType, out int seedSlot)
        {
            seedSlot = -1;
            var inventory = gameState == null || gameState.Inventory == null
                ? null
                : gameState.Inventory.Items;
            if (inventory == null || inventory.Count <= 0 || seedItemType <= 0)
            {
                return false;
            }

            for (var index = 0; index < inventory.Count && index < 50; index++)
            {
                var item = inventory[index];
                if (item != null && item.Type == seedItemType && item.Stack > 0)
                {
                    seedSlot = item.SlotIndex;
                    return seedSlot >= 0;
                }
            }

            return false;
        }

        private static InputActionRequest BuildHarvestRequest(AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target)
        {
            tool = tool ?? new AutoHarvestToolCandidate();
            target = target ?? new AutoHarvestHerbTarget();
            var request = CreateItemUseRequest("Auto harvest herb", FeatureIds.WorldAutomationAutoHarvest + ".harvest");
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoHarvest;
            request.Metadata[ActionMetadataKeys.TargetSlot] = tool.Slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = AutoHarvestCompat.TileCenterWorldX(target.TileX).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = AutoHarvestCompat.TileCenterWorldY(target.TileY).ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestAction"] = "Harvest";
            request.Metadata["AutoHarvestToolItemType"] = tool.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileX"] = target.TileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileY"] = target.TileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileType"] = target.TileType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestHerbStyle"] = target.HerbStyle.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSeedItemType"] = target.SeedItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSupportTileType"] = target.SupportTileType.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static InputActionRequest BuildSustainedHarvestRequest(AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target)
        {
            tool = tool ?? new AutoHarvestToolCandidate();
            target = target ?? new AutoHarvestHerbTarget();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoHarvest,
                Description = "Auto harvest sustained use",
                QueueTimeout = TimeSpan.FromMilliseconds(100),
                Timeout = TimeSpan.FromMilliseconds(1500),
                AdmissionKey = FeatureIds.WorldAutomationAutoHarvest + ".harvest.sustained",
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.MouseTarget |
                                   InputActionChannel.InventorySlot |
                                   InputActionChannel.HotbarSelection |
                                   InputActionChannel.RawInput
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoHarvest;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoHarvestSustainedUse";
            request.Metadata[ActionMetadataKeys.TargetSlot] = tool.Slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = AutoHarvestCompat.TileCenterWorldX(target.TileX).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = AutoHarvestCompat.TileCenterWorldY(target.TileY).ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestAction"] = "HarvestSustainedUse";
            request.Metadata["AutoHarvestToolItemType"] = tool.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileX"] = target.TileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileY"] = target.TileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileType"] = target.TileType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestHerbStyle"] = target.HerbStyle.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSeedItemType"] = target.SeedItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSupportTileType"] = target.SupportTileType.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static InputActionRequest BuildReplantRequest(int seedSlot, int seedItemType, AutoHarvestPendingReplant pending)
        {
            pending = pending ?? new AutoHarvestPendingReplant();
            var request = CreateItemUseRequest("Auto replant harvested herb", FeatureIds.WorldAutomationAutoHarvest + ".replant");
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoHarvestReplant;
            request.Metadata[ActionMetadataKeys.TargetSlot] = seedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = AutoHarvestCompat.TileCenterWorldX(pending.TileX).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = AutoHarvestCompat.TileCenterWorldY(pending.TileY).ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestAction"] = "Replant";
            request.Metadata["AutoHarvestTileX"] = pending.TileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestTileY"] = pending.TileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestHerbStyle"] = pending.HerbStyle.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSeedItemType"] = seedItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoHarvestSupportTileType"] = pending.SupportTileType.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static InputActionRequest CreateItemUseRequest(string description, string admissionKey)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoHarvest,
                Description = description ?? "Auto harvest",
                QueueTimeout = TimeSpan.FromMilliseconds(300),
                Timeout = TimeSpan.FromSeconds(2),
                AdmissionKey = admissionKey ?? FeatureIds.WorldAutomationAutoHarvest,
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.BridgeItemUse |
                                   InputActionChannel.MouseTarget |
                                   InputActionChannel.InventorySlot |
                                   InputActionChannel.HotbarSelection
            };
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["AllowEarlyItemCheck"] = "true";
            request.Metadata["EarlyItemCheckWindowTicks"] = "2";
            return request;
        }

        private static void AddOrRefreshPendingReplant(AutoHarvestHerbTarget target, long tick)
        {
            if (target == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                for (var index = 0; index < PendingReplants.Count; index++)
                {
                    var pending = PendingReplants[index];
                    if (pending != null && pending.Matches(target))
                    {
                        pending.HerbStyle = target.HerbStyle;
                        pending.SeedItemType = target.SeedItemType;
                        pending.SupportTileType = target.SupportTileType;
                        pending.CreatedTick = tick;
                        pending.ExpiresTick = tick + ReplantWindowTicks;
                        return;
                    }
                }

                PendingReplants.Add(new AutoHarvestPendingReplant
                {
                    TileX = target.TileX,
                    TileY = target.TileY,
                    HerbStyle = target.HerbStyle,
                    SeedItemType = target.SeedItemType,
                    SupportTileType = target.SupportTileType,
                    CreatedTick = tick,
                    ExpiresTick = tick + ReplantWindowTicks
                });

                while (PendingReplants.Count > MaxPendingReplants)
                {
                    PendingReplants.RemoveAt(0);
                }
            }
        }

        private static AutoHarvestSustainedUseTarget BuildSustainedUseTarget(AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target, object player, long tick)
        {
            tool = tool ?? new AutoHarvestToolCandidate();
            target = target ?? new AutoHarvestHerbTarget();
            var worldX = AutoHarvestCompat.TileCenterWorldX(target.TileX);
            var worldY = AutoHarvestCompat.TileCenterWorldY(target.TileY);
            float playerCenterX;
            float playerCenterY;
            var direction = 0;
            if (AutoMiningCompat.TryGetMiningCenterWorld(player, out playerCenterX, out playerCenterY))
            {
                direction = worldX >= playerCenterX ? 1 : -1;
            }

            return new AutoHarvestSustainedUseTarget
            {
                ToolSlot = tool.Slot,
                ToolItemType = tool.ItemType,
                ToolItemName = tool.ItemName ?? string.Empty,
                TileX = target.TileX,
                TileY = target.TileY,
                SeedItemType = target.SeedItemType,
                WorldX = worldX,
                WorldY = worldY,
                Direction = direction,
                UpdatedTick = tick,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private static bool EnsureSustainedHarvestRequest(InputActionQueue queue, AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target)
        {
            if (queue == null)
            {
                return false;
            }

            var snapshot = queue.GetFastState();
            if (IsRunningAutoHarvestSustainedUse(snapshot))
            {
                return true;
            }

            if (snapshot == null ||
                snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                ItemUseBridge.PendingRequestId != Guid.Empty)
            {
                return false;
            }

            var request = BuildSustainedHarvestRequest(tool, target);
            InputActionAdmissionResult admission;
            return queue.TryEnqueue(request, out admission);
        }

        private static void PruneExpiredPendingReplants(long tick)
        {
            lock (SyncRoot)
            {
                for (var index = PendingReplants.Count - 1; index >= 0; index--)
                {
                    var pending = PendingReplants[index];
                    if (pending == null || tick > pending.ExpiresTick || tick < pending.CreatedTick)
                    {
                        PendingReplants.RemoveAt(index);
                    }
                }
            }
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

            if (snapshot.Ui.NpcChatOpen)
            {
                return "blocked: NPC chat open";
            }

            if (snapshot.Ui.ChestOpen)
            {
                return "blocked: chest UI open";
            }

            if (snapshot.Ui.PlayerInventoryOpen)
            {
                return "blocked: player inventory UI open";
            }

            return string.Empty;
        }

        private static bool HasPendingWorkThatShouldPreemptHarvest(InputActionQueue queue)
        {
            var snapshot = queue == null ? null : queue.GetFastState();
            if (snapshot == null)
            {
                return true;
            }

            if (snapshot.PendingCount > 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(snapshot.RunningActionKind))
            {
                return false;
            }

            return !IsRunningAutoHarvestSustainedUse(snapshot);
        }

        private static bool IsRunningAutoHarvestSustainedUse(InputActionQueueFastState snapshot)
        {
            return snapshot != null &&
                   string.Equals(snapshot.RunningActionKind, InputActionKind.RawInput.ToString(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(snapshot.RunningActionSource, FeatureIds.WorldAutomationAutoHarvest, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
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

        private static AutoHarvestHerbTarget PendingToTarget(AutoHarvestPendingReplant pending)
        {
            if (pending == null)
            {
                return null;
            }

            return new AutoHarvestHerbTarget
            {
                TileX = pending.TileX,
                TileY = pending.TileY,
                HerbStyle = pending.HerbStyle,
                SeedItemType = pending.SeedItemType,
                SupportTileType = pending.SupportTileType
            };
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                PendingReplants.Clear();
                _lastScanTick = -CheckIntervalTicks;
                RecordDecisionLocked(decision, null, null, string.Empty);
            }
        }

        private static void RecordDecision(string decision, AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target, string action)
        {
            lock (SyncRoot)
            {
                RecordDecisionLocked(decision, tool, target, action);
            }
        }

        private static void RecordDecisionLocked(string decision, AutoHarvestToolCandidate tool, AutoHarvestHerbTarget target, string action)
        {
            _lastDecision = decision ?? string.Empty;
            _lastDecisionUtc = DateTime.UtcNow;
            _lastAction = action ?? string.Empty;
            _lastToolSlot = tool == null ? -1 : tool.Slot;
            _lastToolItemType = tool == null ? 0 : tool.ItemType;
            _lastTargetTileX = target == null ? -1 : target.TileX;
            _lastTargetTileY = target == null ? -1 : target.TileY;
            _lastTargetSeedItemType = target == null ? 0 : target.SeedItemType;
        }
    }
}
