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

namespace JueMingZ.Automation.WorldAutomation
{
    // Auto mining resolves reachable targets and queues use-tile work; it must not write input flags or Tile state directly.
    public static class AutoMiningService
    {
        private const int ScanRadiusTiles = 80;
        private const int CancelDistanceTiles = 30;
        private const long UseIntervalTicks = 4;
        private const long ManualObservationLifetimeTicks = 30;
        private static readonly object SyncRoot = new object();
        private static AutoMiningVeinSelection _selection;
        private static ManualMiningObservation _manualObservation;
        private static long _lastUseTick = -UseIntervalTicks;
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
                        Y = _selection.Tiles[index].Y
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
            lock (SyncRoot)
            {
                _selection = null;
                _manualObservation = null;
                RecordDecisionLocked(string.IsNullOrWhiteSpace(reason) ? "selection cleared" : reason);
            }
        }

        internal static InputActionRequest BuildMiningRequestForTesting(int tileX, int tileY, int selectedSlot, string sourceHotkey)
        {
            return BuildMiningRequest(tileX, tileY, selectedSlot, 0, 0, sourceHotkey);
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

        internal static void ObserveManualTileMined(int tileX, int tileY, int tileType, int pickItemType, int pickSlot)
        {
            if (tileType < 0 || pickItemType <= 0)
            {
                return;
            }

            var tick = AutoMiningCompat.ReadGameUpdateCount();
            lock (SyncRoot)
            {
                if (_selection != null || tick <= _ignoreManualObservationUntilTick)
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
                    SeenTick = tick
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
                ClearSelection("disabled");
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
                ClearSelection("player unavailable");
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
                RecordDecision("manual observation consumed: no remaining vein tiles");
            }
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
            var scan = AutoMiningVeinScanner.Scan(
                tileX,
                tileY,
                tileType,
                minX,
                minY,
                maxX,
                maxY,
                (x, y, type) => AutoMiningCompat.IsActiveTileOfType(tiles, x, y, type));

            if (scan == null || scan.Tiles.Count <= 0)
            {
                RecordDecision("selection failed: no matching vein tiles");
                return false;
            }

            var selection = new AutoMiningVeinSelection
            {
                TileType = tileType,
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

            if (selection == null || selection.Tiles.Count <= 0)
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
                ClearSelection("selection cancelled: player left vein range");
                return;
            }

            if (tick - _lastUseTick < UseIntervalTicks || queue == null)
            {
                return;
            }

            Type mainType;
            object tiles;
            int maxTilesX;
            int maxTilesY;
            if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
            {
                RecordDecision("waiting: " + message);
                return;
            }

            object player;
            if (!AutoMiningCompat.TryGetLocalPlayer(out player, out message))
            {
                RecordDecision("waiting: " + message);
                return;
            }

            AutoMiningTile target;
            int remaining;
            if (!TryFindNextTarget(selection, tiles, player, pickaxe.PickPower, pickaxe.TileBoost, out target, out remaining))
            {
                if (remaining <= 0)
                {
                    ClearSelection("selection complete");
                }
                else
                {
                    RecordDecision("waiting for mining reach");
                }

                return;
            }

            InputActionAdmissionResult admission;
            var request = BuildMiningRequest(target.X, target.Y, pickaxe.SelectedSlot, selection.TileType, remaining, selection.SourceHotkey);
            if (!queue.TryEnqueue(request, out admission))
            {
                RecordDecision("queue denied: " + (admission == null ? "unknown" : admission.Summary));
                return;
            }

            _lastUseTick = tick;
            _ignoreManualObservationUntilTick = tick + 12;
            RecordDecision("submitted mining target " + target.X.ToString(CultureInfo.InvariantCulture) + "," + target.Y.ToString(CultureInfo.InvariantCulture));
        }

        private static bool TryFindNextTarget(
            AutoMiningVeinSelection selection,
            object tiles,
            object player,
            int pickPower,
            int pickTileBoost,
            out AutoMiningTile target,
            out int remaining)
        {
            return TryChooseNextTarget(
                selection.Tiles,
                tile => AutoMiningCompat.IsActiveTileOfType(tiles, tile.X, tile.Y, selection.TileType),
                tile => AutoMiningCompat.CanMineTileWithPickaxe(player, tile.X, tile.Y, selection.TileType, pickPower, pickTileBoost),
                player,
                out target,
                out remaining);
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

        private static InputActionRequest BuildMiningRequest(int tileX, int tileY, int selectedSlot, int tileType, int remaining, string sourceHotkey)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.ItemUse,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureIds.WorldAutomationAutoMining,
                Description = "Auto mine ore tile",
                Timeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.WorldAutomationAutoMining
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.WorldAutomationAutoMining;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["SourceHotkey"] = sourceHotkey ?? string.Empty;
            request.Metadata[ActionMetadataKeys.TargetSlot] = selectedSlot.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.RequireSelectedSlotUnchanged] = "true";
            request.Metadata[ActionMetadataKeys.WorldX] = (tileX * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = (tileY * AutoMiningCompat.TileSize + AutoMiningCompat.TileSize / 2f).ToString(CultureInfo.InvariantCulture);
            request.Metadata["ApplyMainMouseLeftForItemCheck"] = "true";
            request.Metadata["AllowEarlyItemCheck"] = "true";
            request.Metadata["EarlyItemCheckWindowTicks"] = "2";
            request.Metadata["AutoMiningTileX"] = tileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningTileY"] = tileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningTileType"] = tileType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["AutoMiningRemainingTiles"] = remaining.ToString(CultureInfo.InvariantCulture);
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
        }
    }
}
