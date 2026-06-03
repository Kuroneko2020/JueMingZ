using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.GameState.Buffs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static readonly int[] StationBuffTypesForTests = { 29, 93, 150, 159, 348, 192, 366 };

        private static void AutoStationBuffCooldownFastSkipAvoidsScan()
        {
            WithStationBuffWorld(
                player =>
                {
                    AutoRecoveryService.ResetAutoStationBuffDiagnosticsForTesting();
                    StationBuffCompat.ResetDiagnosticsForTesting();
                    StationBuffCompat.SetTileCollectionOverrideForTesting(Terraria.Main.tile);
                    AutoRecoveryService.SetAutoStationBuffLastTickForTesting(100);

                    var secondQueue = TickAutoStationBuff(105, new List<BuffSnapshot>());
                    if (secondQueue.GetFastState().PendingCount != 0)
                    {
                        throw new InvalidOperationException("Cooldown fast skip must not enqueue another TileInteract.");
                    }

                    var scan = StationBuffCompat.GetDiagnostics();
                    if (scan.ScanCount != 0)
                    {
                        throw new InvalidOperationException("Cooldown fast skip must avoid station tile scan.");
                    }

                    var state = AutoRecoveryService.GetStateSnapshot();
                    if (state.AutoStationBuffCooldownFastSkipCount <= 0)
                    {
                        throw new InvalidOperationException("Cooldown fast skip counter was not incremented.");
                    }
                });
        }

        private static void AutoStationBuffActiveBuffFastSkipAvoidsScan()
        {
            WithStationBuffWorld(
                player =>
                {
                    AutoRecoveryService.ResetAutoStationBuffDiagnosticsForTesting();
                    StationBuffCompat.ResetDiagnosticsForTesting();
                    StationBuffCompat.SetTileCollectionOverrideForTesting(Terraria.Main.tile);

                    var queue = TickAutoStationBuff(200, BuildActiveStationBuffSnapshots());
                    if (queue.GetFastState().PendingCount != 0)
                    {
                        throw new InvalidOperationException("Active buff fast skip must not enqueue TileInteract.");
                    }

                    var scan = StationBuffCompat.GetDiagnostics();
                    if (scan.ScanCount != 0)
                    {
                        throw new InvalidOperationException("Active buff fast skip must avoid station tile scan.");
                    }

                    var state = AutoRecoveryService.GetStateSnapshot();
                    if (state.AutoStationBuffActiveBuffFastSkipCount <= 0)
                    {
                        throw new InvalidOperationException("Active buff fast skip counter was not incremented.");
                    }
                });
        }

        private static void StationBuffScanCacheReusesReachableTarget()
        {
            WithStationBuffWorld(
                player =>
                {
                    StationBuffCompat.ResetDiagnosticsForTesting();
                    StationBuffCompat.SetTileCollectionOverrideForTesting(Terraria.Main.tile);
                    List<StationBuffTarget> first;
                    string firstMessage;
                    if (!StationBuffCompat.TryFindMissingStationBuffs(player, StationBuffCompat.AllKnownStationBuffMask, 300, out first, out firstMessage) ||
                        first == null ||
                        first.Count <= 0)
                    {
                        throw new InvalidOperationException("Expected first station scan to find a reachable target: " + firstMessage);
                    }

                    List<StationBuffTarget> second;
                    string secondMessage;
                    if (!StationBuffCompat.TryFindMissingStationBuffs(player, StationBuffCompat.AllKnownStationBuffMask, 301, out second, out secondMessage) ||
                        second == null ||
                        second.Count <= 0)
                    {
                        throw new InvalidOperationException("Expected cached station scan to find a reachable target: " + secondMessage);
                    }

                    var scan = StationBuffCompat.GetDiagnostics();
                    if (scan.ScanCount != 1 || scan.CacheHitCount != 1 || scan.CacheMissCount != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected one scan, one cache hit, one cache miss; got scan=" + scan.ScanCount +
                            ", hit=" + scan.CacheHitCount +
                            ", miss=" + scan.CacheMissCount + ".");
                    }

                    if (first[0].TileX != second[0].TileX || first[0].TileY != second[0].TileY)
                    {
                        throw new InvalidOperationException("Cached station target changed unexpectedly.");
                    }
                });
        }

        private static void StationBuffTileReadFallbackPreservesScanResult()
        {
            WithStationBuffWorld(
                player =>
                {
                    StationBuffCompat.ResetDiagnosticsForTesting();
                    StationBuffCompat.SetTileCollectionOverrideForTesting(Terraria.Main.tile);
                    StationBuffCompat.SetTileFastPathDisabledForTesting(true);
                    try
                    {
                        List<StationBuffTarget> targets;
                        string message;
                        if (!StationBuffCompat.TryFindMissingStationBuffs(player, StationBuffCompat.AllKnownStationBuffMask, 400, out targets, out message) ||
                            targets == null ||
                            targets.Count <= 0)
                        {
                            throw new InvalidOperationException("Expected fallback station scan to find a reachable target: " + message);
                        }

                        if (targets[0].TileType != 125 || targets[0].BuffType != 29)
                        {
                            throw new InvalidOperationException("Fallback station scan returned the wrong target.");
                        }

                        var scan = StationBuffCompat.GetDiagnostics();
                        if (string.IsNullOrWhiteSpace(scan.TileFastPathStatus) ||
                            scan.TileFastPathStatus.IndexOf("fallback", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            throw new InvalidOperationException("Expected fallback tile read status, got " + scan.TileFastPathStatus + ".");
                        }
                    }
                    finally
                    {
                        StationBuffCompat.SetTileFastPathDisabledForTesting(false);
                    }
                });
        }

        private static void AutoStationBuffRequestsActiveBuffSnapshot()
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoStationBuffEnabled = true;

            var options = JueMingZRuntime.BuildGameStateReadOptionsForTesting(settings, false);
            if (!options.IncludeActiveBuffs)
            {
                throw new InvalidOperationException("Auto station buff must request active buff snapshots for fast skip.");
            }
        }

        private static InputActionQueue TickAutoStationBuff(long tick, IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            var settings = AppSettings.CreateDefault();
            settings.AutoStationBuffEnabled = true;

            var queue = new InputActionQueue();
            var runtimeState = new RuntimeState { UpdateCount = tick };
            AutoRecoveryService.Tick(
                queue,
                BuildAutoStationBuffSnapshot(activeBuffs),
                runtimeState,
                RuntimeSettingsSnapshot.FromSettings(settings));
            return queue;
        }

        private static GameStateSnapshot BuildAutoStationBuffSnapshot(IReadOnlyList<BuffSnapshot> activeBuffs)
        {
            return new GameStateSnapshot
            {
                TerrariaDetected = true,
                IsInWorld = true,
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    Life = 400,
                    LifeMax = 400,
                    Mana = 200,
                    ManaMax = 200
                },
                Ui = new UiStateSnapshot(),
                ActiveBuffs = activeBuffs ?? new List<BuffSnapshot>()
            };
        }

        private static List<BuffSnapshot> BuildActiveStationBuffSnapshots()
        {
            var result = new List<BuffSnapshot>();
            for (var index = 0; index < StationBuffTypesForTests.Length; index++)
            {
                result.Add(new BuffSnapshot
                {
                    BuffType = StationBuffTypesForTests[index],
                    BuffTime = 600
                });
            }

            return result;
        }

        private static void WithStationBuffWorld(Action<Terraria.Player> action)
        {
            var previousTile = Terraria.Main.tile;
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousPlayers = Terraria.Main.player;
            var previousMyPlayer = Terraria.Main.myPlayer;
            try
            {
                var player = new Terraria.Player
                {
                    position = new Terraria.TestVector2 { X = 12 * 16, Y = 12 * 16 },
                    width = 20,
                    height = 42
                };
                var tiles = new object[30, 30];
                tiles[12, 12] = new Terraria.Tile
                {
                    activeValue = true,
                    type = 125,
                    frameX = 0,
                    frameY = 0
                };

                Terraria.Main.tile = tiles;
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.myPlayer = 0;
                Terraria.Main.player = new object[256];
                Terraria.Main.player[0] = player;
                StationBuffCompat.SetTileCollectionOverrideForTesting(tiles);

                action(player);
            }
            finally
            {
                Terraria.Main.tile = previousTile;
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.player = previousPlayers;
                Terraria.Main.myPlayer = previousMyPlayer;
                StationBuffCompat.SetTileFastPathDisabledForTesting(false);
                StationBuffCompat.SetTileCollectionOverrideForTesting(null);
            }
        }
    }
}
