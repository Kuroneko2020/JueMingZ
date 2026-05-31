using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Automation.Fishing;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.WorldAutomation
{
    public sealed class AutoCaptureCritterDiagnostics
    {
        public string LastDecision { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int BugNetSlot { get; set; }
        public int BugNetItemType { get; set; }
        public int TargetNpcIndex { get; set; }
        public int TargetNpcType { get; set; }
        public string FishingProtectionState { get; set; }

        public AutoCaptureCritterDiagnostics()
        {
            LastDecision = string.Empty;
            BugNetSlot = -1;
            TargetNpcIndex = -1;
            FishingProtectionState = string.Empty;
        }
    }

    public static class AutoCaptureCritterService
    {
        private const long CheckIntervalTicks = 4;
        private const long UseCooldownTicks = 18;
        private const long BobberCheckDelayTicks = 8;
        private const int PlayerWidthPixels = 20;
        private const int DefaultCritterWidthPixels = 16;
        private const int DefaultCritterHeightPixels = 16;
        private const float SustainedTargetMaxDistancePixels = 192f;
        private static readonly object SyncRoot = new object();
        private static long _lastScanTick = -CheckIntervalTicks;
        private static long _lastUseTick = -UseCooldownTicks;
        private static Guid _captureRequestId = Guid.Empty;
        private static Guid _recastRequestId = Guid.Empty;
        private static Guid _restorePoleRequestId = Guid.Empty;
        private static int _activeTargetNpcIndex = -1;
        private static FishingProtectionState _fishingProtection;
        private static string _lastDecision = string.Empty;
        private static DateTime? _lastDecisionUtc;
        private static int _lastBugNetSlot = -1;
        private static int _lastBugNetItemType;
        private static int _lastTargetNpcIndex = -1;
        private static int _lastTargetNpcType;

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
                RecordDecision("exception:" + error.GetType().Name, null, null);
                RuntimeDiagnostics.RecordError("AutoCaptureCritterService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-capture-critter-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoCaptureCritterService",
                    "Auto capture critter service failed; exception swallowed.", error);
            }
        }

        public static void AfterActionQueueUpdate(InputActionQueueFastState queueSnapshot, long tick)
        {
            var result = queueSnapshot == null ? null : queueSnapshot.LastResult;
            if (result == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_captureRequestId != Guid.Empty && result.RequestId == _captureRequestId)
                {
                    _captureRequestId = Guid.Empty;
                    _activeTargetNpcIndex = -1;
                    if (_fishingProtection != null &&
                        _fishingProtection.CaptureRequestId == result.RequestId)
                    {
                        _fishingProtection.WaitingForBobberCheck = true;
                        _fishingProtection.CaptureCompletedTick = tick;
                        RecordDecisionLocked("capture completed; checking fishing bobber: " + result.Status, null, null);
                    }
                }

                if (_recastRequestId != Guid.Empty && result.RequestId == _recastRequestId)
                {
                    _recastRequestId = Guid.Empty;
                    _fishingProtection = null;
                    RecordDecisionLocked("fishing bobber recast completed: " + result.Status, null, null);
                }

                if (_restorePoleRequestId != Guid.Empty && result.RequestId == _restorePoleRequestId)
                {
                    _restorePoleRequestId = Guid.Empty;
                    RecordDecisionLocked("fishing pole restore completed: " + result.Status, null, null);
                }
            }
        }

        public static AutoCaptureCritterDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return new AutoCaptureCritterDiagnostics
                {
                    LastDecision = _lastDecision,
                    LastDecisionUtc = _lastDecisionUtc,
                    BugNetSlot = _lastBugNetSlot,
                    BugNetItemType = _lastBugNetItemType,
                    TargetNpcIndex = _lastTargetNpcIndex,
                    TargetNpcType = _lastTargetNpcType,
                    FishingProtectionState = _fishingProtection == null ? string.Empty : _fishingProtection.ToSummary()
                };
            }
        }

        public static void ClearState(string reason)
        {
            ClearTracking(string.IsNullOrWhiteSpace(reason) ? "cleared" : reason);
        }

        internal static bool HasFishingProtectionInFlight()
        {
            lock (SyncRoot)
            {
                return _captureRequestId != Guid.Empty ||
                       AutoCaptureCritterSustainedUseBridge.HasActiveUse ||
                       _recastRequestId != Guid.Empty ||
                       _restorePoleRequestId != Guid.Empty ||
                       (_fishingProtection != null &&
                        (_fishingProtection.WaitingForBobberCheck ||
                         _fishingProtection.RecastSubmitted ||
                         _fishingProtection.CaptureRequestId != Guid.Empty));
            }
        }

        internal static InputActionRequest BuildCaptureRequestForTesting(int bugNetSlot, int bugNetItemType, int catchTool, int npcIndex, int npcType, float worldX, float worldY, bool fishingProtected)
        {
            return BuildCaptureRequest(
                new BugNetCandidate
                {
                    Slot = bugNetSlot,
                    ItemType = bugNetItemType,
                    ItemName = "Bug Net",
                    CatchTool = catchTool
                },
                new NpcSnapshot
                {
                    WhoAmI = npcIndex,
                    Type = npcType,
                    CenterX = worldX,
                    CenterY = worldY,
                    CatchItem = 1
                },
                fishingProtected,
                AutoCaptureCritterModes.Auto);
        }

        internal static bool IsWithinCaptureRangeForTesting(float playerCenterX, float playerCenterY, float critterCenterX, float critterCenterY, int catchTool)
        {
            return IsWithinCaptureRangeForTesting(
                playerCenterX - PlayerWidthPixels * 0.5f,
                playerCenterY - 21f,
                critterCenterX - DefaultCritterWidthPixels * 0.5f,
                critterCenterY - DefaultCritterHeightPixels * 0.5f,
                DefaultCritterWidthPixels,
                DefaultCritterHeightPixels,
                catchTool);
        }

        internal static bool IsWithinCaptureRangeForTesting(
            float playerPositionX,
            float playerPositionY,
            float critterPositionX,
            float critterPositionY,
            int critterWidth,
            int critterHeight,
            int catchTool)
        {
            var player = new PlayerStateSnapshot
            {
                PositionX = playerPositionX,
                PositionY = playerPositionY
            };
            var width = critterWidth > 0 ? critterWidth : DefaultCritterWidthPixels;
            var height = critterHeight > 0 ? critterHeight : DefaultCritterHeightPixels;
            var critter = new NpcSnapshot
            {
                PositionX = critterPositionX,
                PositionY = critterPositionY,
                Width = width,
                Height = height,
                CenterX = critterPositionX + width * 0.5f,
                CenterY = critterPositionY + height * 0.5f
            };
            return IsWithinCaptureRange(
                player,
                critter,
                new BugNetCandidate
                {
                    ItemType = ResolveBugNetItemTypeForCatchTool(catchTool),
                    CatchTool = catchTool
                });
        }

        internal static InputActionRequest BuildRestorePoleRequestForTesting(int poleSlot, int poleItemType)
        {
            return BuildRestorePoleRequest(
                new FishingProtectionState
                {
                    PoleSlot = poleSlot,
                    PoleItemType = poleItemType
                });
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastScanTick = -CheckIntervalTicks;
                _lastUseTick = -UseCooldownTicks;
                _captureRequestId = Guid.Empty;
                _recastRequestId = Guid.Empty;
                _restorePoleRequestId = Guid.Empty;
                _activeTargetNpcIndex = -1;
                _fishingProtection = null;
                _lastDecision = string.Empty;
                _lastDecisionUtc = null;
                _lastBugNetSlot = -1;
                _lastBugNetItemType = 0;
                _lastTargetNpcIndex = -1;
                _lastTargetNpcType = 0;
            }

            AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget("auto capture reset for testing");
        }

        internal static bool TryBuildCaptureRequestForTesting(GameStateSnapshot gameState, out InputActionRequest request, out string message)
        {
            return TryBuildCaptureRequestForTesting(gameState, AutoCaptureCritterModes.Auto, out request, out message);
        }

        internal static bool TryBuildCaptureRequestForTesting(GameStateSnapshot gameState, string mode, out InputActionRequest request, out string message)
        {
            request = null;
            message = string.Empty;
            mode = AutoCaptureCritterModes.Normalize(mode);

            BugNetCandidate bugNet;
            if (!TryFindBestBugNet(gameState, mode, out bugNet, out message))
            {
                return false;
            }

            NpcSnapshot target;
            if (!TryFindCaptureTarget(gameState, bugNet, out target, out message))
            {
                return false;
            }

            request = BuildCaptureRequest(bugNet, target, false, mode);
            return true;
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            var mode = settingsSnapshot.WorldAutomationAutoCaptureCritterMode;

            if (string.Equals(mode, AutoCaptureCritterModes.Off, StringComparison.Ordinal))
            {
                ClearTracking("disabled");
                return;
            }

            if (TryHandleFishingProtection(queue, tick))
            {
                return;
            }

            if (!ShouldScan(tick))
            {
                return;
            }

            if (queue == null)
            {
                RecordDecision("queue unavailable", null, null);
                return;
            }

            if (!CanRun(gameState))
            {
                ClearTracking("player unavailable");
                return;
            }

            var blockedReason = GetExecutionBlockedReason(gameState);
            if (!string.IsNullOrEmpty(blockedReason))
            {
                AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget(blockedReason);
                RecordDecision(blockedReason, null, null);
                return;
            }

            BugNetCandidate bugNet;
            string bugNetMessage;
            if (!TryFindBestBugNet(gameState, mode, out bugNet, out bugNetMessage))
            {
                AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget(bugNetMessage);
                RecordDecision(bugNetMessage, null, null);
                return;
            }

            NpcSnapshot target;
            string targetMessage;
            var captureInFlight = HasCaptureRequestInFlight();
            if (captureInFlight)
            {
                if (!TryFindActiveSustainedTarget(gameState, bugNet, out target, out targetMessage))
                {
                    AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget(targetMessage);
                    RecordDecision(targetMessage, bugNet, null);
                    return;
                }
            }
            else if (!TryFindCaptureTarget(gameState, bugNet, out target, out targetMessage))
            {
                AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget(targetMessage);
                RecordDecision(targetMessage, bugNet, null);
                return;
            }

            AutoCaptureCritterSustainedUseBridge.SetDesiredTarget(BuildSustainedUseTarget(bugNet, target, gameState.Player, tick, mode));
            if (!captureInFlight && tick - _lastUseTick < UseCooldownTicks)
            {
                RecordDecision("cooldown", bugNet, target);
                return;
            }

            var fishingProtection = captureInFlight ? null : TryCreateFishingProtection();
            if (!EnsureSustainedCaptureRequest(queue, bugNet, target, fishingProtection, tick, mode))
            {
                AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget("auto capture sustained request was not admitted");
                RecordDecision("queue denied sustained capture", bugNet, target);
                return;
            }

            RecordDecision(captureInFlight ? "sustained capture target refreshed" : "submitted sustained capture request", bugNet, target);
        }

        private static bool TryFindBestBugNet(GameStateSnapshot gameState, string mode, out BugNetCandidate bugNet, out string message)
        {
            bugNet = null;
            message = string.Empty;
            mode = AutoCaptureCritterModes.Normalize(mode);
            if (string.Equals(mode, AutoCaptureCritterModes.Manual, StringComparison.Ordinal))
            {
                return TryFindHeldBugNet(gameState, out bugNet, out message);
            }

            var inventory = gameState == null || gameState.Inventory == null
                ? null
                : gameState.Inventory.Items;
            if (inventory == null || inventory.Count <= 0)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            for (var index = 0; index < inventory.Count && index < 50; index++)
            {
                var item = inventory[index];
                if (item == null || item.Type <= 0 || item.Stack <= 0)
                {
                    continue;
                }

                int catchToolTier;
                if (!TerrariaBugNetCompat.TryResolveCatchToolTier(item.Type, item.CatchTool, out catchToolTier))
                {
                    continue;
                }

                if (bugNet == null ||
                    catchToolTier > bugNet.CatchTool ||
                    (catchToolTier == bugNet.CatchTool && item.SlotIndex < bugNet.Slot))
                {
                    bugNet = new BugNetCandidate
                    {
                        Slot = item.SlotIndex,
                        ItemType = item.Type,
                        ItemName = item.Name ?? string.Empty,
                        CatchTool = catchToolTier
                    };
                }
            }

            if (bugNet == null)
            {
                message = "no bug net in inventory";
                return false;
            }

            return true;
        }

        private static bool TryFindHeldBugNet(GameStateSnapshot gameState, out BugNetCandidate bugNet, out string message)
        {
            bugNet = null;
            message = string.Empty;
            var inventory = gameState == null ? null : gameState.Inventory;
            if (inventory == null)
            {
                message = "inventory snapshot unavailable";
                return false;
            }

            var selectedSlot = inventory.SelectedItemSlot;
            if (selectedSlot < 0 || selectedSlot >= 50)
            {
                message = "selected item is not a bug net";
                return false;
            }

            var selected = inventory.SelectedItem;
            if ((selected == null || selected.SlotIndex != selectedSlot) &&
                inventory.Items != null &&
                selectedSlot < inventory.Items.Count)
            {
                selected = inventory.Items[selectedSlot];
            }

            if (selected == null || selected.Type <= 0 || selected.Stack <= 0)
            {
                message = "selected item is not a bug net";
                return false;
            }

            int catchToolTier;
            if (!TerrariaBugNetCompat.TryResolveCatchToolTier(selected.Type, selected.CatchTool, out catchToolTier))
            {
                message = "selected item is not a bug net";
                return false;
            }

            bugNet = new BugNetCandidate
            {
                Slot = selectedSlot,
                ItemType = selected.Type,
                ItemName = selected.Name ?? string.Empty,
                CatchTool = catchToolTier
            };
            return true;
        }

        private static bool TryFindCaptureTarget(GameStateSnapshot gameState, BugNetCandidate bugNet, out NpcSnapshot target, out string message)
        {
            target = null;
            message = string.Empty;
            var critters = gameState == null || gameState.Npcs == null
                ? null
                : gameState.Npcs.CatchableCritters;
            if (critters == null || critters.Count <= 0)
            {
                message = "no catchable critter nearby";
                return false;
            }

            var bestDistance = float.MaxValue;
            var player = gameState.Player;
            if (player == null)
            {
                message = "player snapshot unavailable";
                return false;
            }

            var playerCenterX = player.PositionX + PlayerWidthPixels * 0.5f;
            var playerCenterY = player.PositionY + 21f;
            for (var index = 0; index < critters.Count; index++)
            {
                var critter = critters[index];
                if (critter == null || !critter.Active || critter.CatchItem <= 0)
                {
                    continue;
                }

                if (!IsWithinCaptureRange(player, critter, bugNet))
                {
                    continue;
                }

                var dx = critter.CenterX - playerCenterX;
                var dy = critter.CenterY - playerCenterY;
                var distance = dx * dx + dy * dy;
                if (target == null ||
                    distance < bestDistance ||
                    (Math.Abs(distance - bestDistance) < 0.001f && critter.WhoAmI < target.WhoAmI))
                {
                    target = critter;
                    bestDistance = distance;
                }
            }

            if (target == null)
            {
                message = "catchable critters outside bug net range";
                return false;
            }

            return true;
        }

        private static bool TryFindActiveSustainedTarget(GameStateSnapshot gameState, BugNetCandidate bugNet, out NpcSnapshot target, out string message)
        {
            target = null;
            message = string.Empty;

            int activeTargetNpcIndex;
            lock (SyncRoot)
            {
                activeTargetNpcIndex = _activeTargetNpcIndex;
            }

            if (activeTargetNpcIndex < 0)
            {
                return TryFindCaptureTarget(gameState, bugNet, out target, out message);
            }

            var critters = gameState == null || gameState.Npcs == null
                ? null
                : gameState.Npcs.CatchableCritters;
            if (critters == null || critters.Count <= 0)
            {
                message = "sustained capture target disappeared";
                return false;
            }

            for (var index = 0; index < critters.Count; index++)
            {
                var critter = critters[index];
                if (critter == null ||
                    critter.WhoAmI != activeTargetNpcIndex ||
                    !critter.Active ||
                    critter.CatchItem <= 0)
                {
                    continue;
                }

                if (!IsWithinSustainedTrackingRange(gameState == null ? null : gameState.Player, critter))
                {
                    message = "sustained capture target outside tracking range";
                    return false;
                }

                target = critter;
                return true;
            }

            message = "sustained capture target disappeared";
            return false;
        }

        private static bool IsWithinSustainedTrackingRange(PlayerStateSnapshot player, NpcSnapshot critter)
        {
            if (player == null || critter == null)
            {
                return false;
            }

            var playerCenterX = player.PositionX + PlayerWidthPixels * 0.5f;
            var playerCenterY = player.PositionY + 21f;
            var dx = critter.CenterX - playerCenterX;
            var dy = critter.CenterY - playerCenterY;
            var max = SustainedTargetMaxDistancePixels;
            return dx * dx + dy * dy <= max * max;
        }

        private static InputActionRequest BuildCaptureRequest(BugNetCandidate bugNet, NpcSnapshot target, bool fishingProtected, string mode)
        {
            bugNet = bugNet ?? new BugNetCandidate();
            target = target ?? new NpcSnapshot();
            mode = AutoCaptureCritterModes.Normalize(mode);
            var request = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoCaptureCritter,
                Description = "Auto capture critter sustained use",
                QueueTimeout = TimeSpan.FromMilliseconds(150),
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.WorldAutomationAutoCaptureCritter + ".sustained",
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.MouseTarget |
                                   InputActionChannel.InventorySlot |
                                   InputActionChannel.HotbarSelection |
                                   InputActionChannel.RawInput
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoCaptureCritter;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoCaptureCritterSustainedUse";
            request.Metadata[ActionMetadataKeys.TargetSlot] = bugNet.Slot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = target.CenterX.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = target.CenterY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoCaptureCritterAction"] = "SustainedUse";
            request.Metadata["AutoCaptureCritterNpcIndex"] = target.WhoAmI.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoCaptureCritterNpcType"] = target.Type.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoCaptureCritterCatchItem"] = target.CatchItem.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BugNetItemType"] = bugNet.ItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BugNetCatchTool"] = bugNet.CatchTool.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingProtection"] = fishingProtected ? "true" : "false";
            request.Metadata["AutoCaptureCritterMode"] = mode;
            return request;
        }

        private static AutoCaptureCritterSustainedUseTarget BuildSustainedUseTarget(BugNetCandidate bugNet, NpcSnapshot target, PlayerStateSnapshot player, long tick, string mode)
        {
            bugNet = bugNet ?? new BugNetCandidate();
            target = target ?? new NpcSnapshot();
            mode = AutoCaptureCritterModes.Normalize(mode);
            var playerCenterX = player == null ? 0f : player.PositionX + PlayerWidthPixels * 0.5f;
            return new AutoCaptureCritterSustainedUseTarget
            {
                BugNetSlot = bugNet.Slot,
                BugNetItemType = bugNet.ItemType,
                BugNetItemName = bugNet.ItemName ?? string.Empty,
                NpcIndex = target.WhoAmI,
                NpcType = target.Type,
                CatchItem = target.CatchItem,
                WorldX = target.CenterX,
                WorldY = target.CenterY,
                Direction = target.CenterX >= playerCenterX ? 1 : -1,
                RestoreOriginalStateOnComplete = string.Equals(mode, AutoCaptureCritterModes.Auto, StringComparison.Ordinal),
                UpdatedTick = tick,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private static bool EnsureSustainedCaptureRequest(InputActionQueue queue, BugNetCandidate bugNet, NpcSnapshot target, FishingProtectionState fishingProtection, long tick, string mode)
        {
            if (queue == null)
            {
                return false;
            }

            if (HasCaptureRequestInFlight())
            {
                return true;
            }

            var snapshot = queue.GetFastState();
            if (snapshot == null ||
                snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                ItemUseBridge.PendingRequestId != Guid.Empty)
            {
                return false;
            }

            var request = BuildCaptureRequest(bugNet, target, fishingProtection != null, mode);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                return false;
            }

            lock (SyncRoot)
            {
                _captureRequestId = request.RequestId;
                _activeTargetNpcIndex = target == null ? -1 : target.WhoAmI;
                if (fishingProtection != null)
                {
                    fishingProtection.CaptureRequestId = request.RequestId;
                    _fishingProtection = fishingProtection;
                }
            }

            _lastUseTick = tick;
            return true;
        }

        private static bool HasCaptureRequestInFlight()
        {
            lock (SyncRoot)
            {
                return _captureRequestId != Guid.Empty || AutoCaptureCritterSustainedUseBridge.HasActiveUse;
            }
        }

        private static bool TryHandleFishingProtection(InputActionQueue queue, long tick)
        {
            FishingProtectionState protection;
            lock (SyncRoot)
            {
                protection = _fishingProtection;
            }

            if (protection == null || !protection.WaitingForBobberCheck)
            {
                return false;
            }

            if (tick - protection.CaptureCompletedTick < BobberCheckDelayTicks)
            {
                RecordDecision("waiting before fishing bobber check", null, null);
                return true;
            }

            if (IsBobberStillPresent(protection.BobberIdentity, tick))
            {
                bool poleSelected;
                string restoreReason;
                if (!TryEnsureFishingPoleSelected(queue, protection, out poleSelected, out restoreReason))
                {
                    lock (SyncRoot)
                    {
                        _fishingProtection = null;
                    }

                    RecordDecision("fishing bobber protection dropped: " + restoreReason, null, null);
                    return false;
                }

                if (!poleSelected)
                {
                    RecordDecision("waiting for fishing pole restore: " + restoreReason, null, null);
                    return true;
                }

                lock (SyncRoot)
                {
                    _fishingProtection = null;
                }

                RecordDecision("fishing bobber preserved", null, null);
                return true;
            }

            if (protection.RecastSubmitted)
            {
                RecordDecision("waiting for fishing bobber recast result", null, null);
                return true;
            }

            if (queue == null || IsQueueBusy(queue))
            {
                RecordDecision("waiting to recast fishing bobber: queue busy", null, null);
                return true;
            }

            bool readyForRecast;
            string ensureReason;
            if (!TryEnsureFishingPoleSelected(queue, protection, out readyForRecast, out ensureReason))
            {
                lock (SyncRoot)
                {
                    _fishingProtection = null;
                }

                RecordDecision("fishing bobber recast skipped: " + ensureReason, null, null);
                return false;
            }

            if (!readyForRecast)
            {
                RecordDecision("waiting for fishing pole restore before recast: " + ensureReason, null, null);
                return true;
            }

            var request = BuildRecastRequest(protection);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("fishing bobber recast denied: " + (admission == null ? "unknown" : admission.Summary), null, null);
                return true;
            }

            lock (SyncRoot)
            {
                _recastRequestId = request.RequestId;
                if (_fishingProtection != null)
                {
                    _fishingProtection.RecastSubmitted = true;
                    _fishingProtection.RecastRequestId = request.RequestId;
                }
            }

            RecordDecision("submitted fishing bobber recast", null, null);
            return true;
        }

        private static InputActionRequest BuildRecastRequest(FishingProtectionState protection)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.WorldAutomationAutoCaptureCritter,
                Description = "Recast fishing bobber after auto capture",
                QueueTimeout = TimeSpan.FromSeconds(1),
                Timeout = TimeSpan.FromSeconds(3),
                AdmissionKey = FeatureIds.WorldAutomationAutoCaptureCritter + ".recast",
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.BridgeItemUse |
                                   InputActionChannel.MouseTarget
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoCaptureCritterRecast;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.TargetSlot] = protection.PoleSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.RequireSelectedSlotUnchanged] = "true";
            request.Metadata[ActionMetadataKeys.WorldX] = protection.CastWorldX.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = protection.CastWorldY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["AllowEarlyItemCheck"] = "true";
            request.Metadata["EarlyItemCheckWindowTicks"] = "2";
            request.Metadata["FishingBobberIdentity"] = protection.BobberIdentity.ToString(CultureInfo.InvariantCulture);
            request.Metadata["FishingPoleItemType"] = protection.PoleItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoCaptureCritterRecast"] = "true";
            return request;
        }

        private static bool TryEnsureFishingPoleSelected(
            InputActionQueue queue,
            FishingProtectionState protection,
            out bool readyForUse,
            out string reason)
        {
            readyForUse = false;
            reason = string.Empty;
            if (protection == null)
            {
                reason = "fishingProtectionUnavailable";
                return false;
            }

            if (IsExpectedFishingPoleSelected(protection))
            {
                readyForUse = true;
                reason = "alreadySelected";
                return true;
            }

            if (protection.PoleSlot < 0 || protection.PoleSlot > 9)
            {
                reason = "unsupportedPoleSlot:" + protection.PoleSlot.ToString(CultureInfo.InvariantCulture);
                return false;
            }

            lock (SyncRoot)
            {
                if (_restorePoleRequestId != Guid.Empty)
                {
                    reason = "restorePending";
                    return true;
                }
            }

            if (queue == null)
            {
                reason = "restoreQueueUnavailable";
                return true;
            }

            if (IsQueueBusy(queue))
            {
                reason = "restoreQueueBusy";
                return true;
            }

            var request = BuildRestorePoleRequest(protection);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                reason = "restoreDenied:" + (admission == null ? "unknown" : admission.Summary);
                return true;
            }

            lock (SyncRoot)
            {
                _restorePoleRequestId = request.RequestId;
            }

            reason = "restoreSubmitted";
            return true;
        }

        private static bool IsExpectedFishingPoleSelected(FishingProtectionState protection)
        {
            if (protection == null)
            {
                return false;
            }

            int poleSlot;
            int poleItemType;
            int fishingPole;
            return TerrariaFishingCompat.TryReadSelectedFishingPole(out poleSlot, out poleItemType, out fishingPole) &&
                   poleSlot == protection.PoleSlot &&
                   poleItemType == protection.PoleItemType;
        }

        private static InputActionRequest BuildRestorePoleRequest(FishingProtectionState protection)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.SelectHotbarSlot,
                Priority = InputActionPriority.Normal,
                SourceFeatureId = FeatureIds.WorldAutomationAutoCaptureCritter,
                Description = "Restore fishing pole slot after auto capture",
                QueueTimeout = TimeSpan.FromMilliseconds(300),
                Timeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.WorldAutomationAutoCaptureCritter + ".restore_pole",
                RequiredChannels = InputActionChannel.HotbarSelection
            };
            request.Metadata[ActionMetadataKeys.Scenario] = "WorldAutomation.AutoCaptureCritter.RestorePole";
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["Slot"] = protection == null
                ? "-1"
                : protection.PoleSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata["KeepSelected"] = "true";
            return request;
        }

        private static FishingProtectionState TryCreateFishingProtection()
        {
            int poleSlot;
            int poleItemType;
            int fishingPole;
            if (!TerrariaFishingCompat.TryReadSelectedFishingPole(out poleSlot, out poleItemType, out fishingPole))
            {
                return null;
            }

            FishingBobberObservation observation;
            if (!TryGetLatestBobber(out observation) || observation == null)
            {
                return null;
            }

            var castWorldX = observation.CenterX;
            var castWorldY = observation.CenterY;
            var fishing = FishingAutomationService.GetDiagnostics();
            if (fishing != null &&
                fishing.FishingSessionActive &&
                (Math.Abs(fishing.FishingCastWorldX) > 0.001f || Math.Abs(fishing.FishingCastWorldY) > 0.001f))
            {
                castWorldX = fishing.FishingCastWorldX;
                castWorldY = fishing.FishingCastWorldY;
            }

            return new FishingProtectionState
            {
                PoleSlot = poleSlot,
                PoleItemType = poleItemType,
                BobberIdentity = observation.Identity,
                CastWorldX = castWorldX,
                CastWorldY = castWorldY
            };
        }

        private static bool TryGetLatestBobber(out FishingBobberObservation observation)
        {
            observation = null;
            List<FishingBobberObservation> scanned;
            if (TerrariaFishingCompat.TryScanLocalBobbers(out scanned) && scanned != null && scanned.Count > 0)
            {
                observation = SelectLatestBobber(scanned);
                return observation != null;
            }

            return FishingBobberObserver.TryGetLatest(out observation);
        }

        private static FishingBobberObservation SelectLatestBobber(IReadOnlyList<FishingBobberObservation> observations)
        {
            FishingBobberObservation best = null;
            if (observations == null)
            {
                return null;
            }

            for (var index = 0; index < observations.Count; index++)
            {
                var current = observations[index];
                if (current == null)
                {
                    continue;
                }

                if (best == null ||
                    current.GameUpdateCount > best.GameUpdateCount ||
                    (current.GameUpdateCount == best.GameUpdateCount && current.WhoAmI > best.WhoAmI))
                {
                    best = current;
                }
            }

            return best;
        }

        private static bool IsBobberStillPresent(int identity, long tick)
        {
            if (identity < 0)
            {
                return false;
            }

            List<FishingBobberObservation> scanned;
            if (TerrariaFishingCompat.TryScanLocalBobbers(out scanned) && scanned != null)
            {
                for (var index = 0; index < scanned.Count; index++)
                {
                    if (scanned[index] != null && scanned[index].Identity == identity)
                    {
                        return true;
                    }
                }

                return false;
            }

            FishingBobberObservation observation;
            return FishingBobberObserver.TryGetByIdentity(identity, out observation) &&
                   observation != null &&
                   tick - observation.GameUpdateCount <= 12;
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

        private static bool IsWithinCaptureRange(PlayerStateSnapshot player, NpcSnapshot critter, BugNetCandidate bugNet)
        {
            if (player == null || critter == null || bugNet == null)
            {
                return false;
            }

            BugNetReachProfile profile;
            if (!TryResolveBugNetReachProfile(bugNet.ItemType, bugNet.CatchTool, out profile))
            {
                return false;
            }

            var playerCenterX = player.PositionX + PlayerWidthPixels * 0.5f;
            var direction = critter.CenterX >= playerCenterX ? 1 : -1;
            var critterRect = CreateCritterRect(critter);
            var reachRect = BuildBugNetReachEnvelope(player.PositionX, player.PositionY, direction, profile);
            return RectIntersects(reachRect, critterRect);
        }

        private static IntRect BuildBugNetReachEnvelope(float playerPositionX, float playerPositionY, int direction, BugNetReachProfile profile)
        {
            var envelope = BuildBugNetPhaseHitbox(playerPositionX, playerPositionY, direction, profile, 0);
            for (var phase = 1; phase < 3; phase++)
            {
                envelope = UnionRect(envelope, BuildBugNetPhaseHitbox(playerPositionX, playerPositionY, direction, profile, phase));
            }

            return envelope;
        }

        private static IntRect BuildBugNetPhaseHitbox(float playerPositionX, float playerPositionY, int direction, BugNetReachProfile profile, int phase)
        {
            var itemLocationX = playerPositionX + PlayerWidthPixels * 0.5f;
            var itemLocationY = phase == 0 ? playerPositionY + 24f : playerPositionY + 10f;
            if (phase < 2)
            {
                itemLocationX += (profile.FrameWidth * 0.5f - 10f) * direction;
            }
            else
            {
                itemLocationX -= (profile.FrameWidth * 0.5f - 6f) * direction;
            }

            var width = ScaleDimension(profile.FrameWidth, profile.Scale);
            var height = ScaleDimension(profile.FrameHeight, profile.Scale);
            var rect = new IntRect((int)itemLocationX, (int)itemLocationY, width, height);
            if (direction == -1)
            {
                rect.X -= rect.Width;
            }

            rect.Y -= rect.Height;

            if (phase == 0)
            {
                if (direction == -1)
                {
                    rect.X -= (int)(rect.Width * 0.4f);
                }

                rect.Width = (int)(rect.Width * 1.4f);
                rect.Y += (int)(rect.Height * 0.5f);
                rect.Height = (int)(rect.Height * 1.1f);
                return rect;
            }

            if (phase == 2)
            {
                if (direction == 1)
                {
                    rect.X -= (int)(rect.Width * 1.2f);
                }

                rect.Width *= 2;
                rect.Y -= (int)(rect.Height * 0.4f);
                rect.Height = (int)(rect.Height * 1.4f);
            }

            return rect;
        }

        private static IntRect CreateCritterRect(NpcSnapshot critter)
        {
            var width = critter.Width > 0 ? critter.Width : DefaultCritterWidthPixels;
            var height = critter.Height > 0 ? critter.Height : DefaultCritterHeightPixels;
            var x = critter.PositionX;
            var y = critter.PositionY;
            if (Math.Abs(x) < 0.001f && Math.Abs(y) < 0.001f && (Math.Abs(critter.CenterX) > 0.001f || Math.Abs(critter.CenterY) > 0.001f))
            {
                x = critter.CenterX - width * 0.5f;
                y = critter.CenterY - height * 0.5f;
            }

            return new IntRect((int)x, (int)y, width, height);
        }

        private static bool TryResolveBugNetReachProfile(int itemType, int catchTool, out BugNetReachProfile profile)
        {
            switch (itemType)
            {
                case TerrariaBugNetCompat.GoldenBugNetItemType:
                    profile = new BugNetReachProfile(24, 28, 1.15f);
                    return true;
                case TerrariaBugNetCompat.LavaproofBugNetItemType:
                    profile = new BugNetReachProfile(24, 28, 0.85f);
                    return true;
                case TerrariaBugNetCompat.BugNetItemType:
                    profile = new BugNetReachProfile(24, 28, 1f);
                    return true;
            }

            switch (Math.Min(Math.Max(0, catchTool), 3))
            {
                case 2:
                    profile = new BugNetReachProfile(24, 28, 1.15f);
                    return true;
                case 3:
                    profile = new BugNetReachProfile(24, 28, 0.85f);
                    return true;
                case 1:
                    profile = new BugNetReachProfile(24, 28, 1f);
                    return true;
                default:
                    profile = default(BugNetReachProfile);
                    return false;
            }
        }

        private static int ResolveBugNetItemTypeForCatchTool(int catchTool)
        {
            switch (Math.Min(Math.Max(0, catchTool), 3))
            {
                case 2:
                    return TerrariaBugNetCompat.GoldenBugNetItemType;
                case 3:
                    return TerrariaBugNetCompat.LavaproofBugNetItemType;
                default:
                    return TerrariaBugNetCompat.BugNetItemType;
            }
        }

        private static int ScaleDimension(int value, float scale)
        {
            return Math.Max(1, (int)(value * scale));
        }

        private static IntRect UnionRect(IntRect left, IntRect right)
        {
            var minX = Math.Min(left.Left, right.Left);
            var minY = Math.Min(left.Top, right.Top);
            var maxX = Math.Max(left.Right, right.Right);
            var maxY = Math.Max(left.Bottom, right.Bottom);
            return new IntRect(minX, minY, maxX - minX, maxY - minY);
        }

        private static bool RectIntersects(IntRect left, IntRect right)
        {
            return left.Left < right.Right &&
                   right.Left < left.Right &&
                   left.Top < right.Bottom &&
                   right.Top < left.Bottom;
        }

        private static void ClearTracking(string decision)
        {
            lock (SyncRoot)
            {
                _captureRequestId = Guid.Empty;
                _recastRequestId = Guid.Empty;
                _restorePoleRequestId = Guid.Empty;
                _activeTargetNpcIndex = -1;
                _fishingProtection = null;
                _lastScanTick = -CheckIntervalTicks;
                RecordDecisionLocked(decision, null, null);
            }

            AutoCaptureCritterSustainedUseBridge.ClearDesiredTarget(decision);
        }

        private static void RecordDecision(string decision, BugNetCandidate bugNet, NpcSnapshot target)
        {
            lock (SyncRoot)
            {
                RecordDecisionLocked(decision, bugNet, target);
            }
        }

        private static void RecordDecisionLocked(string decision, BugNetCandidate bugNet, NpcSnapshot target)
        {
            _lastDecision = decision ?? string.Empty;
            _lastDecisionUtc = DateTime.UtcNow;
            _lastBugNetSlot = bugNet == null ? -1 : bugNet.Slot;
            _lastBugNetItemType = bugNet == null ? 0 : bugNet.ItemType;
            _lastTargetNpcIndex = target == null ? -1 : target.WhoAmI;
            _lastTargetNpcType = target == null ? 0 : target.Type;
        }

        private sealed class BugNetCandidate
        {
            public int Slot;
            public int ItemType;
            public string ItemName;
            public int CatchTool;
        }

        private sealed class FishingProtectionState
        {
            public Guid CaptureRequestId;
            public Guid RecastRequestId;
            public bool WaitingForBobberCheck;
            public bool RecastSubmitted;
            public long CaptureCompletedTick;
            public int PoleSlot;
            public int PoleItemType;
            public int BobberIdentity;
            public float CastWorldX;
            public float CastWorldY;

            public string ToSummary()
            {
                return "waiting=" + (WaitingForBobberCheck ? "true" : "false") +
                       ",recast=" + (RecastSubmitted ? "true" : "false") +
                       ",poleSlot=" + PoleSlot.ToString(CultureInfo.InvariantCulture) +
                       ",poleItemType=" + PoleItemType.ToString(CultureInfo.InvariantCulture) +
                       ",bobber=" + BobberIdentity.ToString(CultureInfo.InvariantCulture);
            }
        }

        private struct IntRect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;

            public IntRect(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = Math.Max(1, width);
                Height = Math.Max(1, height);
            }

            public int Left { get { return X; } }
            public int Top { get { return Y; } }
            public int Right { get { return X + Width; } }
            public int Bottom { get { return Y + Height; } }
        }

        private struct BugNetReachProfile
        {
            public readonly int FrameWidth;
            public readonly int FrameHeight;
            public readonly float Scale;

            public BugNetReachProfile(int frameWidth, int frameHeight, float scale)
            {
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                Scale = scale;
            }
        }
    }
}
