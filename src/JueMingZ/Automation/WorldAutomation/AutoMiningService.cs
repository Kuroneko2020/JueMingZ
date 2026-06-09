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
using JueMingZ.Runtime;

namespace JueMingZ.Automation.WorldAutomation
{
    // Auto mining resolves reachable targets and queues use-tile work; it must not write input flags or Tile state directly.
    public static class AutoMiningService
    {
        private const int ScanRadiusTiles = 80;
        private const int CancelDistanceTiles = 30;
        // Gravity relocation is an event-bounded repair for vanilla-fallen silt/slush; do not turn these caps into per-tick world scanning.
        private const int GravityRelocationSearchDepthTiles = 18;
        private const int GravityRelocationHorizontalOffsetTiles = 1;
        private const int GravityRelocationLifetimeTicks = 45;
        private const int GravityRelocationSourceLimitPerTick = 8;
        private const int GravityRelocationMaxResolvedTilesPerTick = 24;
        private const int SustainedQueueTimeoutMilliseconds = 100;
        // Pending mining input should expire quickly, but a running held-pickaxe session
        // is ended by target refresh/stale gates; this timeout is only a dead-man guard.
        private static readonly TimeSpan SustainedRequestTimeout = TimeSpan.FromMinutes(10);
        private const long ManualObservationLifetimeTicks = 30;
        private static readonly object SyncRoot = new object();
        private static AutoMiningVeinSelection _selection;
        private static ManualMiningObservation _manualObservation;
        private static long _ignoreManualObservationUntilTick;
        private static string _lastDecision = string.Empty;

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
                RecordDecision("exception:" + error.GetType().Name);
                RuntimeDiagnostics.RecordError("AutoMiningService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "auto-mining-service-error",
                    TimeSpan.FromSeconds(10),
                    "AutoMiningService",
                    "Auto mining service failed; exception swallowed.", error);
            }
        }

        public static bool ShouldDrawWorldOverlay()
        {
            var settings = RuntimeSettingsSnapshotProvider.GetCurrent();
            if (!settings.WorldAutomationAutoMiningEnabled)
            {
                return false;
            }

            lock (SyncRoot)
            {
                return _selection != null && _selection.Tiles.Count > 0;
            }
        }

        public static AutoMiningOverlaySnapshot GetOverlaySnapshot()
        {
            lock (SyncRoot)
            {
                if (_selection == null)
                {
                    return null;
                }

                var tiles = new List<AutoMiningOverlayTile>(_selection.Tiles.Count);
                for (var index = 0; index < _selection.Tiles.Count; index++)
                {
                    tiles.Add(new AutoMiningOverlayTile
                    {
                        X = _selection.Tiles[index].X,
                        Y = _selection.Tiles[index].Y,
                        TileType = _selection.Tiles[index].TileType
                    });
                }

                return new AutoMiningOverlaySnapshot
                {
                    TileType = _selection.TileType,
                    PickPower = _selection.PickPower,
                    PickTileBoost = _selection.PickTileBoost,
                    Mode = _selection.SourceMode,
                    Tiles = tiles
                };
            }
        }

        public static void ClearSelection(string reason)
        {
            var safeReason = string.IsNullOrWhiteSpace(reason) ? "selection cleared" : reason;
            lock (SyncRoot)
            {
                _selection = null;
                _manualObservation = null;
                RecordDecisionLocked(safeReason);
            }

            AutoMiningSustainedUseBridge.ClearDesiredTarget(safeReason);
        }

        internal static InputActionRequest BuildSustainedMiningRequestForTesting(int tileX, int tileY, int selectedSlot, int pickItemType, string sourceMode, string sourceHotkey)
        {
            return BuildSustainedMiningRequest(tileX, tileY, selectedSlot, pickItemType, 0, 0, sourceMode, sourceHotkey);
        }

        internal static bool IsSelectedSlotInterruptForTesting(int selectionPickSlot, int selectedSlot)
        {
            return IsSelectedSlotInterrupt(selectionPickSlot, selectedSlot);
        }

        internal static AutoMiningTile ChooseNextTargetForTesting(
            IList<AutoMiningTile> tiles,
            Func<AutoMiningTile, bool> isActive,
            Func<AutoMiningTile, bool> isReachable,
            float playerCenterX,
            float playerCenterY,
            out int remaining)
        {
            AutoMiningTile target;
            AutoMiningTargetSelector.TryChooseNextTarget(
                tiles,
                isActive,
                isReachable,
                playerCenterX,
                playerCenterY,
                out target,
                out remaining);
            return target;
        }

        internal static int RefreshSelectionTilesForTesting(AutoMiningVeinSelection selection, AutoMiningTileReader readTile)
        {
            return RefreshSelectionTiles(selection, readTile, 0);
        }

        internal static int RefreshSelectionTilesForTesting(AutoMiningVeinSelection selection, AutoMiningTileReader readTile, long tick)
        {
            return RefreshSelectionTiles(selection, readTile, tick);
        }

        internal static int GetPendingGravityRelocationCountForTesting(AutoMiningVeinSelection selection)
        {
            return CountPendingGravityRelocations(selection);
        }

        internal static bool ShouldIgnoreManualObservationForTesting(
            AutoMiningVeinSelection selection,
            long tick,
            long ignoreUntilTick,
            int tileX,
            int tileY)
        {
            string reason;
            return ShouldIgnoreManualObservation(selection, tick, ignoreUntilTick, tileX, tileY, out reason);
        }

        internal static void ObserveManualTileMined(int tileX, int tileY, int tileType, int pickItemType, int pickSlot)
        {
            if (tileType < 0 || pickItemType <= 0)
            {
                return;
            }

            var tick = AutoMiningCompat.ReadGameUpdateCount();
            lock (SyncRoot)
            {
                string ignoreReason;
                if (ShouldIgnoreManualObservation(_selection, tick, _ignoreManualObservationUntilTick, tileX, tileY, out ignoreReason))
                {
                    return;
                }

                _manualObservation = new ManualMiningObservation
                {
                    TileX = tileX,
                    TileY = tileY,
                    TileType = tileType,
                    PickItemType = pickItemType,
                    PickSlot = pickSlot,
                    SeenTick = tick,
                    ReplacesExistingSelection = _selection != null
                };
                RecordDecisionLocked(
                    "manual mined ore observed: " +
                    tileX.ToString(CultureInfo.InvariantCulture) +
                    "," +
                    tileY.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void TickCore(InputActionQueue queue, GameStateSnapshot gameState, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var settings = settingsSnapshot.SourceSettings ?? AppSettings.CreateDefault();
            var mode = settingsSnapshot.WorldAutomationAutoMiningMode;
            var tick = runtimeState == null ? AutoMiningCompat.ReadGameUpdateCount() : runtimeState.UpdateCount;

            if (!AutoMiningModes.IsEnabled(mode))
            {
                CancelSelectionAndActions(queue, "disabled");
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
                CancelSelectionAndActions(queue, "player unavailable");
                return;
            }

            int selectedSlot;
            string selectedSlotMessage;
            if (!AutoMiningCompat.TryGetSelectedSlot(out selectedSlot, out selectedSlotMessage))
            {
                CancelSelectionAndActions(queue, "selection cancelled: " + selectedSlotMessage);
                return;
            }

            AutoMiningVeinSelection activeSelection;
            lock (SyncRoot)
            {
                activeSelection = _selection;
            }

            if (activeSelection != null && IsSelectedSlotInterrupt(activeSelection.PickSlot, selectedSlot))
            {
                CancelSelectionAndActions(queue, "selection cancelled: player switched hotbar slot");
                return;
            }

            AutoMiningPickaxeProfile pickaxe;
            string pickaxeMessage;
            if (!AutoMiningCompat.TryGetSelectedPickaxe(out pickaxe, out pickaxeMessage))
            {
                CancelSelectionAndActions(queue, "pickaxe unavailable: " + pickaxeMessage);
                return;
            }

            if (string.Equals(mode, AutoMiningModes.Hotkey, StringComparison.Ordinal))
            {
                TryHandleHotkeySelection(settings, pickaxe, tick);
            }
            else if (string.Equals(mode, AutoMiningModes.Auto, StringComparison.Ordinal))
            {
                TryHandleManualMiningSelection(pickaxe, tick);
            }

            TryRunMining(queue, pickaxe, tick);
        }

        private static void TryHandleHotkeySelection(AppSettings settings, AutoMiningPickaxeProfile pickaxe, long tick)
        {
            var hotkeys = ConfigService.HotkeySettings == null ? null : ConfigService.HotkeySettings.HotkeysByFeatureId;
            string hotkey;
            if (hotkeys == null ||
                !hotkeys.TryGetValue(FeatureIds.WorldAutomationAutoMining, out hotkey) ||
                string.IsNullOrWhiteSpace(hotkey))
            {
                return;
            }

            string display;
            if (!AutoMiningHotkeyInput.TryConsumePressed(hotkey, out display))
            {
                return;
            }

            int tileX;
            int tileY;
            int tileType;
            string message;
            if (!AutoMiningCompat.TryGetCursorMineableOre(out tileX, out tileY, out tileType, out message))
            {
                RecordDecision("hotkey ignored: " + message);
                return;
            }

            TrySelectVein(tileX, tileY, tileType, AutoMiningModes.Hotkey, display, pickaxe, tick);
        }

        private static void TryHandleManualMiningSelection(AutoMiningPickaxeProfile pickaxe, long tick)
        {
            if (tick <= _ignoreManualObservationUntilTick)
            {
                return;
            }

            ManualMiningObservation observation;
            lock (SyncRoot)
            {
                observation = _manualObservation;
            }

            if (observation == null)
            {
                return;
            }

            if (observation.PickItemType != pickaxe.ItemType)
            {
                lock (SyncRoot)
                {
                    if (ReferenceEquals(_manualObservation, observation))
                    {
                        _manualObservation = null;
                    }
                }

                RecordDecision("manual observation cleared: pickaxe changed");
                return;
            }

            if (observation.PickSlot >= 0 && observation.PickSlot != pickaxe.SelectedSlot)
            {
                lock (SyncRoot)
                {
                    if (ReferenceEquals(_manualObservation, observation))
                    {
                        _manualObservation = null;
                    }
                }

                RecordDecision("manual observation cleared: player switched hotbar slot");
                return;
            }

            if (tick - observation.SeenTick > ManualObservationLifetimeTicks)
            {
                lock (SyncRoot)
                {
                    if (ReferenceEquals(_manualObservation, observation))
                    {
                        _manualObservation = null;
                    }
                }

                RecordDecision("manual observation expired");
                return;
            }

            var selected = TrySelectVein(
                observation.TileX,
                observation.TileY,
                observation.TileType,
                AutoMiningModes.Auto,
                string.Empty,
                pickaxe,
                tick);
            lock (SyncRoot)
            {
                if (ReferenceEquals(_manualObservation, observation))
                {
                    _manualObservation = null;
                }
            }

            if (!selected)
            {
                if (observation.ReplacesExistingSelection)
                {
                    ClearSelection("manual observation consumed: replacement vein has no remaining tiles");
                    return;
                }

                RecordDecision("manual observation consumed: no remaining vein tiles");
            }
        }

        private static bool ShouldIgnoreManualObservation(
            AutoMiningVeinSelection selection,
            long tick,
            long ignoreUntilTick,
            int tileX,
            int tileY,
            out string reason)
        {
            reason = string.Empty;
            if (selection != null && IsTrackedSelectionCoordinate(selection, tileX, tileY))
            {
                // PickTile also fires for our sustained held-pickaxe target. Only coordinates already in
                // the active selection are self-noise; a newly mined ore elsewhere is the user's reselect signal.
                reason = "active selection tile";
                return true;
            }

            if (selection == null && tick <= ignoreUntilTick)
            {
                reason = "recent auto mining tick";
                return true;
            }

            return false;
        }

        private static bool IsTrackedSelectionCoordinate(AutoMiningVeinSelection selection, int tileX, int tileY)
        {
            if (selection == null || selection.Tiles == null)
            {
                return false;
            }

            for (var index = 0; index < selection.Tiles.Count; index++)
            {
                var tile = selection.Tiles[index];
                if (tile != null && tile.X == tileX && tile.Y == tileY)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TrySelectVein(int tileX, int tileY, int tileType, string sourceMode, string sourceHotkey, AutoMiningPickaxeProfile pickaxe, long tick)
        {
            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            string message;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                RecordDecision("selection failed: " + message);
                return false;
            }

            var minX = Math.Max(0, tileX - ScanRadiusTiles);
            var minY = Math.Max(0, tileY - ScanRadiusTiles);
            var maxX = Math.Min(maxTilesX - 1, tileX + ScanRadiusTiles);
            var maxY = Math.Min(maxTilesY - 1, tileY + ScanRadiusTiles);
            var matchGroup = AutoMiningTileMatchGroup.ForSeedTileType(tileType);
            var scan = AutoMiningVeinScanner.Scan(
                tileX,
                tileY,
                tileType,
                minX,
                minY,
                maxX,
                maxY,
                (int x, int y, out bool active, out int actualType) => AutoMiningCompat.TryReadTile(tiles, x, y, out active, out actualType));

            if (scan == null || scan.Tiles.Count <= 0)
            {
                RecordDecision("selection failed: no matching vein tiles");
                return false;
            }

            var selection = new AutoMiningVeinSelection
            {
                TileType = tileType,
                MatchGroup = matchGroup,
                PickItemType = pickaxe.ItemType,
                PickSlot = pickaxe.SelectedSlot,
                PickPower = pickaxe.PickPower,
                PickTileBoost = pickaxe.TileBoost,
                MinX = scan.MinX,
                MinY = scan.MinY,
                MaxX = scan.MaxX,
                MaxY = scan.MaxY,
                CreatedTick = tick,
                SourceMode = sourceMode ?? string.Empty,
                SourceHotkey = sourceHotkey ?? string.Empty
            };
            selection.Tiles.AddRange(scan.Tiles);

            lock (SyncRoot)
            {
                _selection = selection;
                RecordDecisionLocked("selected vein: " + scan.Tiles.Count.ToString(CultureInfo.InvariantCulture) + " tiles");
            }

            return true;
        }

        private static void TryRunMining(InputActionQueue queue, AutoMiningPickaxeProfile pickaxe, long tick)
        {
            AutoMiningVeinSelection selection;
            lock (SyncRoot)
            {
                selection = _selection;
            }

            if (selection == null ||
                (selection.Tiles.Count <= 0 && CountPendingGravityRelocations(selection) <= 0))
            {
                return;
            }

            if (pickaxe == null ||
                !pickaxe.IsUsablePickaxe ||
                pickaxe.ItemType != selection.PickItemType ||
                pickaxe.SelectedSlot != selection.PickSlot)
            {
                CancelSelectionAndActions(queue, "selection cancelled: pickaxe changed");
                return;
            }

            string message;
            int playerTileX;
            int playerTileY;
            if (!AutoMiningCompat.TryGetPlayerCenterTile(out playerTileX, out playerTileY, out message))
            {
                ClearSelection("selection cancelled: " + message);
                return;
            }

            if (DistanceOutsideBounds(playerTileX, playerTileY, selection) > CancelDistanceTiles)
            {
                CancelSelectionAndActions(queue, "selection cancelled: player left vein range");
                return;
            }

            if (queue == null)
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("auto mining queue unavailable");
                RecordDecision("queue unavailable");
                return;
            }

            if (HasPendingWorkThatShouldPreemptMining(queue))
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("queue pending; releasing auto mining");
                RecordDecision("queue busy");
                return;
            }

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("tile context unavailable");
                RecordDecision("waiting: " + message);
                return;
            }

            object player;
            if (!AutoMiningCompat.TryGetLocalPlayer(out player, out message))
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("player unavailable");
                RecordDecision("waiting: " + message);
                return;
            }

            AutoMiningTile target;
            int remaining;
            int targetTileType;
            if (!TryFindNextTarget(selection, tiles, player, pickaxe.PickPower, pickaxe.TileBoost, tick, out target, out targetTileType, out remaining))
            {
                if (remaining <= 0)
                {
                    ClearSelection("selection complete");
                }
                else
                {
                    AutoMiningSustainedUseBridge.ClearDesiredTarget("waiting for mining reach");
                    RecordDecision("waiting for mining reach");
                }

                return;
            }

            if (!TryValidateSustainedMiningTarget(tiles, selection, target, player, pickaxe, out targetTileType))
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("waiting for validated mining target");
                RecordDecision("waiting: target not ready for sustained mining");
                return;
            }

            var desiredTarget = BuildSustainedUseTarget(selection, pickaxe, target, targetTileType, player, tick);
            AutoMiningSustainedUseBridge.SetDesiredTarget(desiredTarget);
            if (!EnsureSustainedMiningRequest(queue, pickaxe, target, targetTileType, remaining, selection.SourceMode, selection.SourceHotkey))
            {
                AutoMiningSustainedUseBridge.ClearDesiredTarget("auto mining sustained request was not admitted");
                RecordDecision("queue denied sustained mining");
                return;
            }

            _ignoreManualObservationUntilTick = tick + 12;
            RecordDecision("sustained mining target refreshed " + target.X.ToString(CultureInfo.InvariantCulture) + "," + target.Y.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryFindNextTarget(
            AutoMiningVeinSelection selection,
            object tiles,
            object player,
            int pickPower,
            int pickTileBoost,
            long tick,
            out AutoMiningTile target,
            out int targetTileType,
            out int remaining)
        {
            targetTileType = -1;
            RefreshSelectionTiles(
                selection,
                (int x, int y, out bool active, out int actualType) => AutoMiningCompat.TryReadTile(tiles, x, y, out active, out actualType),
                tick);

            if (selection == null || selection.Tiles.Count <= 0)
            {
                target = null;
                remaining = CountPendingGravityRelocations(selection);
                return false;
            }

            AutoMiningCompat.MiningReachProfile reachProfile;
            if (!AutoMiningCompat.TryBuildMiningTakeoverReachProfile(player, pickTileBoost, out reachProfile))
            {
                target = null;
                remaining = selection.Tiles.Count;
                return false;
            }

            var found = TryChooseNextTarget(
                selection.Tiles,
                tile => IsActiveSelectionTile(tiles, selection, tile),
                tile => IsReachableSelectionTile(tiles, selection, tile, reachProfile, pickPower),
                player,
                out target,
                out remaining);
            if (!found || target == null)
            {
                return false;
            }

            return TryReadSelectionTileType(tiles, selection, target, out targetTileType);
        }

        private static bool TryValidateSustainedMiningTarget(
            object tiles,
            AutoMiningVeinSelection selection,
            AutoMiningTile target,
            object player,
            AutoMiningPickaxeProfile pickaxe,
            out int targetTileType)
        {
            targetTileType = -1;
            int actualType;
            if (selection == null ||
                target == null ||
                player == null ||
                pickaxe == null ||
                !pickaxe.IsUsablePickaxe ||
                !TryReadSelectionTileType(tiles, selection, target, out actualType))
            {
                return false;
            }

            // This is the last cheap service-layer gate before ItemCheck ownership:
            // the target must still be the selected ore and still pass the same mineability rule as green overlay.
            if (!AutoMiningCompat.CanMineTileWithPickaxe(player, target.X, target.Y, actualType, pickaxe.PickPower, pickaxe.TileBoost))
            {
                return false;
            }

            target.TileType = actualType;
            targetTileType = actualType;
            return true;
        }

        private static int RefreshSelectionTiles(AutoMiningVeinSelection selection, AutoMiningTileReader readTile, long tick)
        {
            if (selection == null || selection.Tiles == null || readTile == null)
            {
                return 0;
            }

            RemoveExpiredGravityRelocations(selection, tick);
            for (var index = selection.Tiles.Count - 1; index >= 0; index--)
            {
                var tile = selection.Tiles[index];
                if (tile == null)
                {
                    selection.Tiles.RemoveAt(index);
                    continue;
                }

                int actualType;
                if (TryReadMatchingSelectionTile(readTile, selection, tile.X, tile.Y, out actualType))
                {
                    tile.TileType = actualType;
                    continue;
                }

                selection.Tiles.RemoveAt(index);
                TryQueueGravityRelocation(selection, tile, tick);
            }

            var added = ResolvePendingGravityRelocations(selection, readTile, tick);

            RecalculateSelectionBounds(selection);
            return added;
        }

        private static void TryQueueGravityRelocation(
            AutoMiningVeinSelection selection,
            AutoMiningTile source,
            long tick)
        {
            if (selection == null ||
                selection.PendingGravityRelocations == null ||
                source == null ||
                !AutoMiningCompat.IsGravityAffectedMiningTileType(source.TileType) ||
                !AutoMiningCompat.IsPickPowerSufficientForTile(source.TileType, source.Y, selection.PickPower))
            {
                return;
            }

            for (var index = 0; index < selection.PendingGravityRelocations.Count; index++)
            {
                var pending = selection.PendingGravityRelocations[index];
                if (pending != null &&
                    pending.SourceX == source.X &&
                    pending.SourceY == source.Y &&
                    pending.TileType == source.TileType)
                {
                    return;
                }
            }

            selection.PendingGravityRelocations.Add(new AutoMiningGravityRelocation
            {
                SourceX = source.X,
                SourceY = source.Y,
                TileType = source.TileType,
                CreatedTick = tick
            });
        }

        private static int ResolvePendingGravityRelocations(
            AutoMiningVeinSelection selection,
            AutoMiningTileReader readTile,
            long tick)
        {
            if (selection == null ||
                selection.PendingGravityRelocations == null ||
                selection.PendingGravityRelocations.Count <= 0 ||
                readTile == null)
            {
                return 0;
            }

            var trackedTileKeys = BuildSelectionTileKeySet(selection);
            var added = 0;
            var processed = 0;
            var index = 0;
            while (index < selection.PendingGravityRelocations.Count &&
                   processed < GravityRelocationSourceLimitPerTick &&
                   added < GravityRelocationMaxResolvedTilesPerTick)
            {
                var pending = selection.PendingGravityRelocations[index];
                if (pending == null || IsGravityRelocationExpired(pending, tick))
                {
                    selection.PendingGravityRelocations.RemoveAt(index);
                    continue;
                }

                processed++;
                int relocatedX;
                int relocatedY;
                int actualType;
                if (TryFindRelocatedGravityTile(selection, readTile, pending, trackedTileKeys, out relocatedX, out relocatedY, out actualType))
                {
                    selection.PendingGravityRelocations.RemoveAt(index);
                    var relocatedKey = EncodeTileKey(relocatedX, relocatedY);
                    if (trackedTileKeys.Add(relocatedKey))
                    {
                        selection.Tiles.Add(new AutoMiningTile(relocatedX, relocatedY, actualType));
                        added++;
                    }

                    continue;
                }

                index++;
            }

            return added;
        }

        private static bool TryFindRelocatedGravityTile(
            AutoMiningVeinSelection selection,
            AutoMiningTileReader readTile,
            AutoMiningGravityRelocation pending,
            HashSet<long> trackedTileKeys,
            out int relocatedX,
            out int relocatedY,
            out int actualType)
        {
            relocatedX = -1;
            relocatedY = -1;
            actualType = -1;
            if (selection == null || readTile == null || pending == null)
            {
                return false;
            }

            for (var dy = 1; dy <= GravityRelocationSearchDepthTiles; dy++)
            {
                if (TryAcceptRelocatedGravityTile(selection, readTile, pending.SourceX, pending.SourceY + dy, trackedTileKeys, out actualType))
                {
                    relocatedX = pending.SourceX;
                    relocatedY = pending.SourceY + dy;
                    return true;
                }
            }

            for (var dy = 1; dy <= GravityRelocationSearchDepthTiles; dy++)
            {
                for (var dx = -GravityRelocationHorizontalOffsetTiles; dx <= GravityRelocationHorizontalOffsetTiles; dx++)
                {
                    if (dx == 0)
                    {
                        continue;
                    }

                    var x = pending.SourceX + dx;
                    var y = pending.SourceY + dy;
                    if (TryAcceptRelocatedGravityTile(selection, readTile, x, y, trackedTileKeys, out actualType))
                    {
                        relocatedX = x;
                        relocatedY = y;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryAcceptRelocatedGravityTile(
            AutoMiningVeinSelection selection,
            AutoMiningTileReader readTile,
            int x,
            int y,
            HashSet<long> trackedTileKeys,
            out int actualType)
        {
            actualType = -1;
            if (x < 0 ||
                y < 0 ||
                // A falling column can pass through coordinates already kept in the selection; skip them and keep scanning for the new untracked landing tile.
                (trackedTileKeys != null && trackedTileKeys.Contains(EncodeTileKey(x, y))) ||
                !TryReadMatchingSelectionTile(readTile, selection, x, y, out actualType) ||
                !AutoMiningCompat.IsGravityAffectedMiningTileType(actualType))
            {
                return false;
            }

            return AutoMiningCompat.IsPickPowerSufficientForTile(actualType, y, selection.PickPower);
        }

        private static HashSet<long> BuildSelectionTileKeySet(AutoMiningVeinSelection selection)
        {
            var keys = new HashSet<long>();
            if (selection == null || selection.Tiles == null)
            {
                return keys;
            }

            for (var index = 0; index < selection.Tiles.Count; index++)
            {
                var tile = selection.Tiles[index];
                if (tile != null)
                {
                    keys.Add(EncodeTileKey(tile.X, tile.Y));
                }
            }

            return keys;
        }

        private static void RemoveExpiredGravityRelocations(AutoMiningVeinSelection selection, long tick)
        {
            if (selection == null || selection.PendingGravityRelocations == null)
            {
                return;
            }

            for (var index = selection.PendingGravityRelocations.Count - 1; index >= 0; index--)
            {
                var pending = selection.PendingGravityRelocations[index];
                if (pending == null || IsGravityRelocationExpired(pending, tick))
                {
                    selection.PendingGravityRelocations.RemoveAt(index);
                }
            }
        }

        private static bool IsGravityRelocationExpired(AutoMiningGravityRelocation pending, long tick)
        {
            return pending != null &&
                   tick >= pending.CreatedTick &&
                   tick - pending.CreatedTick > GravityRelocationLifetimeTicks;
        }

        private static int CountPendingGravityRelocations(AutoMiningVeinSelection selection)
        {
            return selection == null || selection.PendingGravityRelocations == null
                ? 0
                : selection.PendingGravityRelocations.Count;
        }

        private static bool TryReadMatchingSelectionTile(
            AutoMiningTileReader readTile,
            AutoMiningVeinSelection selection,
            int x,
            int y,
            out int actualType)
        {
            actualType = -1;
            bool active;
            return selection != null &&
                   readTile != null &&
                   readTile(x, y, out active, out actualType) &&
                   active &&
                   selection.Matches(actualType);
        }

        private static bool IsActiveSelectionTile(object tiles, AutoMiningVeinSelection selection, AutoMiningTile tile)
        {
            int actualType;
            return TryReadSelectionTileType(tiles, selection, tile, out actualType);
        }

        private static bool IsReachableSelectionTile(
            object tiles,
            AutoMiningVeinSelection selection,
            AutoMiningTile tile,
            AutoMiningCompat.MiningReachProfile reachProfile,
            int pickPower)
        {
            int actualType;
            return TryReadSelectionTileType(tiles, selection, tile, out actualType) &&
                   AutoMiningCompat.CanMineTileWithPickaxe(reachProfile, tile.X, tile.Y, actualType, pickPower);
        }

        private static bool TryReadSelectionTileType(
            object tiles,
            AutoMiningVeinSelection selection,
            AutoMiningTile tile,
            out int actualType)
        {
            actualType = -1;
            return selection != null &&
                   tile != null &&
                   AutoMiningCompat.TryReadActiveTileMatchingGroup(tiles, tile.X, tile.Y, selection.MatchGroup, out actualType);
        }

        private static long EncodeTileKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private static void RecalculateSelectionBounds(AutoMiningVeinSelection selection)
        {
            if (selection == null || selection.Tiles == null || selection.Tiles.Count <= 0)
            {
                return;
            }

            var initialized = false;
            for (var index = 0; index < selection.Tiles.Count; index++)
            {
                var tile = selection.Tiles[index];
                if (tile == null)
                {
                    continue;
                }

                if (!initialized)
                {
                    selection.MinX = tile.X;
                    selection.MinY = tile.Y;
                    selection.MaxX = tile.X;
                    selection.MaxY = tile.Y;
                    initialized = true;
                    continue;
                }

                selection.MinX = Math.Min(selection.MinX, tile.X);
                selection.MinY = Math.Min(selection.MinY, tile.Y);
                selection.MaxX = Math.Max(selection.MaxX, tile.X);
                selection.MaxY = Math.Max(selection.MaxY, tile.Y);
            }
        }

        private static bool TryChooseNextTarget(
            IList<AutoMiningTile> tiles,
            Func<AutoMiningTile, bool> isActive,
            Func<AutoMiningTile, bool> isReachable,
            object player,
            out AutoMiningTile target,
            out int remaining)
        {
            float playerCenterX;
            float playerCenterY;
            if (!AutoMiningCompat.TryGetMiningCenterWorld(player, out playerCenterX, out playerCenterY))
            {
                playerCenterX = 0f;
                playerCenterY = 0f;
            }

            return AutoMiningTargetSelector.TryChooseNextTarget(
                tiles,
                isActive,
                isReachable,
                playerCenterX,
                playerCenterY,
                out target,
                out remaining);
        }

        private static AutoMiningSustainedUseTarget BuildSustainedUseTarget(
            AutoMiningVeinSelection selection,
            AutoMiningPickaxeProfile pickaxe,
            AutoMiningTile target,
            int targetTileType,
            object player,
            long tick)
        {
            selection = selection ?? new AutoMiningVeinSelection();
            pickaxe = pickaxe ?? new AutoMiningPickaxeProfile();
            target = target ?? new AutoMiningTile(-1, -1);
            var worldX = target.X * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            var worldY = target.Y * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f;
            float playerCenterX;
            float playerCenterY;
            var direction = 0;
            if (AutoMiningCompat.TryGetMiningCenterWorld(player, out playerCenterX, out playerCenterY))
            {
                direction = worldX >= playerCenterX ? 1 : -1;
            }

            return new AutoMiningSustainedUseTarget
            {
                PickSlot = pickaxe.SelectedSlot,
                PickItemType = pickaxe.ItemType,
                PickItemName = pickaxe.ItemName ?? string.Empty,
                TileX = target.X,
                TileY = target.Y,
                TileType = targetTileType,
                PickPower = pickaxe.PickPower,
                TileBoost = pickaxe.TileBoost,
                SourceMode = selection.SourceMode ?? string.Empty,
                SourceHotkey = selection.SourceHotkey ?? string.Empty,
                WorldX = worldX,
                WorldY = worldY,
                Direction = direction,
                UpdatedTick = tick,
                UpdatedUtc = DateTime.UtcNow
            };
        }

        private static bool EnsureSustainedMiningRequest(
            InputActionQueue queue,
            AutoMiningPickaxeProfile pickaxe,
            AutoMiningTile target,
            int tileType,
            int remaining,
            string sourceMode,
            string sourceHotkey)
        {
            if (queue == null)
            {
                return false;
            }

            var snapshot = queue.GetFastState();
            if (IsRunningAutoMiningSustainedUse(snapshot))
            {
                return true;
            }

            if (snapshot == null ||
                snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                ItemUseBridge.PendingRequestId != Guid.Empty)
            {
                return false;
            }

            var request = BuildSustainedMiningRequest(
                target == null ? -1 : target.X,
                target == null ? -1 : target.Y,
                pickaxe == null ? -1 : pickaxe.SelectedSlot,
                pickaxe == null ? 0 : pickaxe.ItemType,
                tileType,
                remaining,
                sourceMode,
                sourceHotkey);
            InputActionAdmissionResult admission;
            return queue.TryEnqueue(request, out admission);
        }

        private static bool HasPendingWorkThatShouldPreemptMining(InputActionQueue queue)
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

            return !IsRunningAutoMiningSustainedUse(snapshot);
        }

        private static bool IsRunningAutoMiningSustainedUse(InputActionQueueFastState snapshot)
        {
            return snapshot != null &&
                   string.Equals(snapshot.RunningActionKind, InputActionKind.RawInput.ToString(), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(snapshot.RunningActionSource, FeatureIds.WorldAutomationAutoMining, StringComparison.OrdinalIgnoreCase);
        }

        private static InputActionRequest BuildSustainedMiningRequest(int tileX, int tileY, int selectedSlot, int pickItemType, int tileType, int remaining, string sourceMode, string sourceHotkey)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.RawInput,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoMining,
                Description = "Auto mine ore tile sustained use",
                QueueTimeout = TimeSpan.FromMilliseconds(SustainedQueueTimeoutMilliseconds),
                Timeout = SustainedRequestTimeout,
                AdmissionKey = FeatureIds.WorldAutomationAutoMining + ".sustained",
                RequiredChannels = InputActionChannel.UseItem |
                                   InputActionChannel.MouseTarget |
                                   InputActionChannel.InventorySlot |
                                   InputActionChannel.HotbarSelection |
                                   InputActionChannel.RawInput
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoMining;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.RawInputMode] = "AutoMiningSustainedUse";
            request.Metadata["SourceHotkey"] = sourceHotkey ?? string.Empty;
            request.Metadata[ActionMetadataKeys.TargetSlot] = selectedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.RequireSelectedSlotUnchanged] = "true";
            request.Metadata[ActionMetadataKeys.WorldX] = (tileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = (tileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f).ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningAction"] = "SustainedUse";
            request.Metadata["AutoMiningPickItemType"] = pickItemType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningTileX"] = tileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningTileY"] = tileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningTileType"] = tileType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningRemainingTiles"] = remaining.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningMode"] = sourceMode ?? string.Empty;
            return request;
        }

        private static int DistanceOutsideBounds(int x, int y, AutoMiningVeinSelection selection)
        {
            var dx = x < selection.MinX ? selection.MinX - x : (x > selection.MaxX ? x - selection.MaxX : 0);
            var dy = y < selection.MinY ? selection.MinY - y : (y > selection.MaxY ? y - selection.MaxY : 0);
            return Math.Max(dx, dy);
        }

        private static bool IsSelectedSlotInterrupt(int selectionPickSlot, int selectedSlot)
        {
            return selectionPickSlot >= 0 && selectedSlot >= 0 && selectedSlot != selectionPickSlot;
        }

        private static void CancelSelectionAndActions(InputActionQueue queue, string reason)
        {
            ClearSelection(reason);
            var cancelled = queue == null ? 0 : queue.CancelBySource(FeatureIds.WorldAutomationAutoMining);
            if (cancelled > 0)
            {
                RecordDecision((string.IsNullOrWhiteSpace(reason) ? "selection cancelled" : reason) +
                               "; cancelled actions=" +
                               cancelled.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static void RecordDecision(string decision)
        {
            lock (SyncRoot)
            {
                RecordDecisionLocked(decision);
            }
        }

        private static void RecordDecisionLocked(string decision)
        {
            _lastDecision = decision ?? string.Empty;
        }

        private sealed class ManualMiningObservation
        {
            public int TileX;
            public int TileY;
            public int TileType;
            public int PickItemType;
            public int PickSlot;
            public long SeenTick;
            public bool ReplacesExistingSelection;
        }
    }
}
