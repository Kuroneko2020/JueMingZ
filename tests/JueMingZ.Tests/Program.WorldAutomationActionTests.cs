using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Actions.Executors;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.GameState.Inventory;
using JueMingZ.GameState.Npcs;
using JueMingZ.GameState.Player;
using JueMingZ.GameState.Ui;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using Terraria.ID;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private const int AutoMiningVkAlt = 0x12;
        private const int AutoMiningVkK = 0x4B;

        private static void AutoMiningScannerLinksThreeTileGaps()
        {
            var points = new HashSet<string>(StringComparer.Ordinal)
            {
                "10,10",
                "13,10",
                "17,10"
            };

            var scan = AutoMiningVeinScanner.Scan(
                10,
                10,
                7,
                0,
                0,
                32,
                32,
                (int x, int y, out bool active, out int actualType) =>
                {
                    actualType = 7;
                    active = points.Contains(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture));
                    return true;
                });

            if (scan.Tiles.Count != 2 ||
                scan.MinX != 10 ||
                scan.MaxX != 13)
            {
                throw new InvalidOperationException("Auto mining vein scanner should link same-type ore within three tiles and stop past the gap.");
            }
        }

        private static void AutoMiningScannerKeepsInactiveMinedSeedConnectivity()
        {
            var points = new HashSet<string>(StringComparer.Ordinal)
            {
                "10,10",
                "14,10"
            };

            var scan = AutoMiningVeinScanner.Scan(
                12,
                10,
                7,
                0,
                0,
                32,
                32,
                (int x, int y, out bool active, out int actualType) =>
                {
                    actualType = 7;
                    active = points.Contains(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture));
                    return true;
                });

            if (scan.Tiles.Count != 2 ||
                scan.MinX != 10 ||
                scan.MaxX != 14)
            {
                throw new InvalidOperationException("Auto mode should keep both remaining ore sides selected even when the manually mined seed tile itself is gone.");
            }
        }

        private static void AutoMiningScannerGroupsGemClusterTiles()
        {
            var tileTypes = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "10,10", 63 },
                { "12,10", 178 },
                { "14,10", 566 }
            };

            var scan = AutoMiningVeinScanner.Scan(
                10,
                10,
                63,
                0,
                0,
                32,
                32,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = tileTypes.TryGetValue(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture), out actualType);
                    return true;
                });

            if (scan.Tiles.Count != 3 ||
                scan.MinX != 10 ||
                scan.MaxX != 14)
            {
                throw new InvalidOperationException("Auto mining gem cluster should scan normal gems, surface gems and amber stone as one selection.");
            }

            if (!ContainsTileType(scan.Tiles, 63) ||
                !ContainsTileType(scan.Tiles, 178) ||
                !ContainsTileType(scan.Tiles, 566))
            {
                throw new InvalidOperationException("Auto mining gem cluster tiles must keep their actual tile types.");
            }
        }

        private static void AutoMiningScannerKeepsNormalOreSingleType()
        {
            var tileTypes = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                { "10,10", 7 },
                { "12,10", 6 }
            };

            var scan = AutoMiningVeinScanner.Scan(
                10,
                10,
                7,
                0,
                0,
                32,
                32,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = tileTypes.TryGetValue(x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture), out actualType);
                    return true;
                });

            if (scan.Tiles.Count != 1 ||
                scan.Tiles[0].TileType != 7)
            {
                throw new InvalidOperationException("Auto mining normal ores must not mix neighboring ore tile types into the selected vein.");
            }
        }

        private static void AutoMiningTargetUsesActualTileTypeForPickPower()
        {
            var tiles = new List<AutoMiningTile>
            {
                new AutoMiningTile(10, 400, 111)
            };

            int remaining;
            var blocked = AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile => AutoMiningCompat.IsPickPowerSufficientForTileForTesting(tile.TileType, tile.Y, 149),
                168f,
                6408f,
                out remaining);

            if (blocked != null || remaining != 1)
            {
                throw new InvalidOperationException("Auto mining target selection must apply pick power to the target tile's actual type.");
            }

            var allowed = AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile => AutoMiningCompat.IsPickPowerSufficientForTileForTesting(tile.TileType, tile.Y, 150),
                168f,
                6408f,
                out remaining);

            if (allowed == null || allowed.TileType != 111)
            {
                throw new InvalidOperationException("Auto mining should allow a target once the pickaxe meets that actual tile type's requirement.");
            }
        }

        private static void AutoMiningFallbackRecognizesExtraOreAndGravityTiles()
        {
            if (!AutoMiningCompat.IsMineableOreTileType(56) ||
                !AutoMiningCompat.IsMineableOreTileType(404) ||
                !AutoMiningCompat.IsMineableOreTileType(123) ||
                !AutoMiningCompat.IsMineableOreTileType(224))
            {
                throw new InvalidOperationException("Auto mining fallback list must recognize obsidian, desert fossil, silt and slush.");
            }

            if (AutoMiningCompat.IsMineableOreTileType(53) ||
                AutoMiningCompat.IsMineableOreTileType(147))
            {
                throw new InvalidOperationException("Auto mining fallback list must not expand into sand or mud.");
            }

            if (!AutoMiningCompat.IsGravityAffectedMiningTileType(123) ||
                !AutoMiningCompat.IsGravityAffectedMiningTileType(224) ||
                AutoMiningCompat.IsGravityAffectedMiningTileType(56) ||
                AutoMiningCompat.IsGravityAffectedMiningTileType(404))
            {
                throw new InvalidOperationException("Auto mining gravity handling must stay scoped to silt and slush.");
            }

            if (AutoMiningCompat.IsPickPowerSufficientForTileForTesting(56, 400, 54) ||
                !AutoMiningCompat.IsPickPowerSufficientForTileForTesting(56, 400, 55))
            {
                throw new InvalidOperationException("Auto mining must preserve Terraria's 55 pick power gate for obsidian.");
            }
        }

        private static void AutoMiningHotkeyInputTriggersAndDebounces()
        {
            AutoMiningHotkeyInput.ResetForTesting();
            var now = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);

            var first = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                true,
                now);
            if (!first.PressedEdge ||
                !first.Accepted ||
                !string.Equals(first.Display, "Alt+K", StringComparison.Ordinal) ||
                !string.Equals(first.Reason, "accepted", StringComparison.Ordinal) ||
                first.DiagnosticResultCode != DiagnosticResultCode.Succeeded)
            {
                throw new InvalidOperationException("Auto mining hotkey input should accept a fresh trigger edge.");
            }

            var held = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                true,
                now.AddMilliseconds(50));
            if (held.PressedEdge || held.Accepted || !string.Equals(held.Reason, "held", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto mining hotkey input must debounce held keys.");
            }

            AutoMiningHotkeyInput.ConsumePressedForTesting("Alt+K", AutoMiningDownKeys(), false, string.Empty, true, now.AddMilliseconds(60));
            var tooSoon = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                true,
                now.AddMilliseconds(100));
            if (!tooSoon.PressedEdge || tooSoon.Accepted || !string.Equals(tooSoon.Reason, "debounce", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto mining hotkey input must reject rapid re-presses inside the debounce window.");
            }

            AutoMiningHotkeyInput.ConsumePressedForTesting("Alt+K", AutoMiningDownKeys(), false, string.Empty, true, now.AddMilliseconds(260));
            var afterWindow = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                true,
                now.AddMilliseconds(300));
            if (!afterWindow.PressedEdge || !afterWindow.Accepted)
            {
                throw new InvalidOperationException("Auto mining hotkey input should re-arm after release and debounce expiry.");
            }
        }

        private static void AutoMiningHotkeyInputReportsBlockedReasons()
        {
            AutoMiningHotkeyInput.ResetForTesting();
            var now = new DateTime(2026, 6, 20, 0, 1, 0, DateTimeKind.Utc);
            var blocked = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                true,
                "textInputFocused",
                true,
                now);
            if (!blocked.PressedEdge ||
                blocked.Accepted ||
                !string.Equals(blocked.Reason, "textInputFocused", StringComparison.Ordinal) ||
                blocked.DiagnosticResultCode != DiagnosticResultCode.BlockedByUi)
            {
                throw new InvalidOperationException("Auto mining hotkey input should report text-input gate blockers.");
            }

            var heldAfterGate = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                true,
                now.AddMilliseconds(300));
            if (heldAfterGate.PressedEdge || heldAfterGate.Accepted)
            {
                throw new InvalidOperationException("Auto mining hotkey input must require release after a blocked press.");
            }

            AutoMiningHotkeyInput.ResetForTesting();
            var unfocused = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                false,
                string.Empty,
                false,
                now);
            if (!unfocused.PressedEdge ||
                unfocused.Accepted ||
                !string.Equals(unfocused.Reason, "notForeground", StringComparison.Ordinal) ||
                unfocused.DiagnosticResultCode != DiagnosticResultCode.BlockedByEnvironment)
            {
                throw new InvalidOperationException("Auto mining hotkey input should report lost-focus blockers.");
            }

            AutoMiningHotkeyInput.ResetForTesting();
            var gameInputUnavailable = AutoMiningHotkeyInput.ConsumePressedForTesting(
                "Alt+K",
                AutoMiningDownKeys(AutoMiningVkAlt, AutoMiningVkK),
                true,
                "gameInputUnavailable",
                true,
                now);
            if (!gameInputUnavailable.PressedEdge ||
                gameInputUnavailable.Accepted ||
                !string.Equals(gameInputUnavailable.Reason, "gameInputUnavailable", StringComparison.Ordinal) ||
                gameInputUnavailable.DiagnosticResultCode != DiagnosticResultCode.BlockedByEnvironment)
            {
                throw new InvalidOperationException("Auto mining hotkey input should report game-input blockers.");
            }
        }

        private static void AutoMiningRefreshTracksNearbyGravityTileAfterVanillaFall()
        {
            var selection = new AutoMiningVeinSelection
            {
                TileType = 123,
                MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(123),
                PickPower = 1,
                MinX = 10,
                MinY = 10,
                MaxX = 10,
                MaxY = 10
            };
            selection.Tiles.Add(new AutoMiningTile(10, 10, 123));

            var added = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y == 12;
                    actualType = active ? 123 : -1;
                    return true;
                });

            if (added != 1 ||
                selection.Tiles.Count != 1 ||
                !ContainsTile(selection.Tiles, 10, 12) ||
                ContainsTile(selection.Tiles, 10, 10) ||
                selection.MinX != 10 ||
                selection.MaxX != 10 ||
                selection.MinY != 12 ||
                selection.MaxY != 12)
            {
                throw new InvalidOperationException("Auto mining should drop the stale silt coordinate and observe only a nearby vanilla-settled tile.");
            }
        }

        private static void AutoMiningRefreshRelocatesGravityTileBeyondOldThreeTileRadius()
        {
            var selection = new AutoMiningVeinSelection
            {
                TileType = 224,
                MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(224),
                PickPower = 1,
                MinX = 10,
                MinY = 10,
                MaxX = 10,
                MaxY = 10
            };
            selection.Tiles.Add(new AutoMiningTile(10, 10, 224));

            var added = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y == 16;
                    actualType = active ? 224 : -1;
                    return true;
                },
                200);

            if (added != 1 ||
                selection.Tiles.Count != 1 ||
                !ContainsTile(selection.Tiles, 10, 16) ||
                AutoMiningService.GetPendingGravityRelocationCountForTesting(selection) != 0 ||
                selection.MinY != 16 ||
                selection.MaxY != 16)
            {
                throw new InvalidOperationException("Auto mining should relocate slush that falls beyond the old three-tile refresh radius.");
            }
        }

        private static void AutoMiningRefreshKeepsShiftedGravityColumnMarked()
        {
            var selection = new AutoMiningVeinSelection
            {
                TileType = 224,
                MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(224),
                PickPower = 1,
                MinX = 10,
                MinY = 10,
                MaxX = 10,
                MaxY = 12
            };
            selection.Tiles.Add(new AutoMiningTile(10, 10, 224));
            selection.Tiles.Add(new AutoMiningTile(10, 11, 224));
            selection.Tiles.Add(new AutoMiningTile(10, 12, 224));

            var added = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y >= 11 && y <= 13;
                    actualType = active ? 224 : -1;
                    return true;
                },
                300);

            if (added != 1 ||
                selection.Tiles.Count != 3 ||
                ContainsTile(selection.Tiles, 10, 10) ||
                !ContainsTile(selection.Tiles, 10, 11) ||
                !ContainsTile(selection.Tiles, 10, 12) ||
                !ContainsTile(selection.Tiles, 10, 13) ||
                AutoMiningService.GetPendingGravityRelocationCountForTesting(selection) != 0 ||
                selection.MinY != 11 ||
                selection.MaxY != 13)
            {
                throw new InvalidOperationException("Auto mining should keep shifted slush columns marked instead of consuming relocation on already tracked lower tiles.");
            }
        }

        private static void AutoMiningRefreshExpiresOutOfRangeGravityRelocation()
        {
            var selection = new AutoMiningVeinSelection
            {
                TileType = 123,
                MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(123),
                PickPower = 1,
                MinX = 10,
                MinY = 10,
                MaxX = 10,
                MaxY = 10
            };
            selection.Tiles.Add(new AutoMiningTile(10, 10, 123));

            var first = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y == 40;
                    actualType = active ? 123 : -1;
                    return true;
                },
                100);

            if (first != 0 ||
                selection.Tiles.Count != 0 ||
                AutoMiningService.GetPendingGravityRelocationCountForTesting(selection) != 1)
            {
                throw new InvalidOperationException("Auto mining should keep an unresolved gravity relocation pending instead of scanning past its bounded range.");
            }

            var second = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y == 40;
                    actualType = active ? 123 : -1;
                    return true;
                },
                146);

            if (second != 0 ||
                selection.Tiles.Count != 0 ||
                AutoMiningService.GetPendingGravityRelocationCountForTesting(selection) != 0)
            {
                throw new InvalidOperationException("Auto mining should expire unresolved gravity relocation instead of keeping stale targets indefinitely.");
            }
        }

        private static void AutoMiningRefreshKeepsNormalOreFromGravityRescan()
        {
            var selection = new AutoMiningVeinSelection
            {
                TileType = 7,
                MatchGroup = AutoMiningTileMatchGroup.ForSeedTileType(7),
                MinX = 10,
                MinY = 10,
                MaxX = 10,
                MaxY = 10
            };
            selection.Tiles.Add(new AutoMiningTile(10, 10, 7));

            var added = AutoMiningService.RefreshSelectionTilesForTesting(
                selection,
                (int x, int y, out bool active, out int actualType) =>
                {
                    active = x == 10 && y == 12;
                    actualType = active ? 7 : -1;
                    return true;
                });

            if (added != 0 ||
                selection.Tiles.Count != 0 ||
                AutoMiningService.GetPendingGravityRelocationCountForTesting(selection) != 0)
            {
                throw new InvalidOperationException("Auto mining must not perform gravity-style nearby refresh for ordinary ore selections.");
            }
        }

        private static void AutoMiningSelectedSlotSwitchInterruptsSelection()
        {
            if (!AutoMiningService.IsSelectedSlotInterruptForTesting(2, 3))
            {
                throw new InvalidOperationException("Auto mining should treat a player hotbar switch away from the pickaxe slot as an interrupt.");
            }

            if (AutoMiningService.IsSelectedSlotInterruptForTesting(2, 2))
            {
                throw new InvalidOperationException("Auto mining should keep mining while the recorded pickaxe slot remains selected.");
            }

            if (AutoMiningService.IsSelectedSlotInterruptForTesting(-1, 2) ||
                AutoMiningService.IsSelectedSlotInterruptForTesting(2, -1))
            {
                throw new InvalidOperationException("Auto mining should not interrupt on invalid slot sentinel values.");
            }
        }

        private static void AutoMiningManualObservationCanReselectOutsideActiveVein()
        {
            var selection = new AutoMiningVeinSelection();
            selection.Tiles.Add(new AutoMiningTile(10, 10, 7));
            selection.Tiles.Add(new AutoMiningTile(11, 10, 7));

            if (!AutoMiningService.ShouldIgnoreManualObservationForTesting(selection, 100, 112, 10, 10))
            {
                throw new InvalidOperationException("Auto mining should ignore PickTile observations from its own active selection.");
            }

            if (AutoMiningService.ShouldIgnoreManualObservationForTesting(selection, 100, 112, 40, 40))
            {
                throw new InvalidOperationException("Auto mining auto mode must allow a newly mined ore outside the active selection to reselect the vein.");
            }

            if (!AutoMiningService.ShouldIgnoreManualObservationForTesting(null, 100, 112, 40, 40))
            {
                throw new InvalidOperationException("Auto mining should keep the short no-selection self-noise guard after its own mining tick.");
            }
        }

        private static void AutoMiningAutoModeObservationSubmitsSustainedRequest()
        {
            var previousTile = Terraria.Main.tile;
            var previousMaxTilesX = Terraria.Main.maxTilesX;
            var previousMaxTilesY = Terraria.Main.maxTilesY;
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousPlayer0 = Terraria.Main.player[0];
            var previousMyPlayer = Terraria.Main.myPlayer;
            var previousGameUpdateCount = Terraria.Main.GameUpdateCount;
            var restoreMainType = PushFakeTerrariaMainType();

            try
            {
                AutoMiningService.ResetForTesting();
                Terraria.Main.tile = new object[64, 64];
                Terraria.Main.maxTilesX = 64;
                Terraria.Main.maxTilesY = 64;
                Terraria.Main.GameUpdateCount = 1000;
                SetTestTile(11, 10, true, 7);
                SetTestTile(12, 10, true, 7);

                var player = new Terraria.Player
                {
                    whoAmI = 0,
                    selectedItem = 2,
                    position = new Terraria.TestVector2 { X = 160f, Y = 160f },
                    width = 20,
                    height = 42
                };
                player.inventory[2] = new FakeItem
                {
                    type = 777,
                    stack = 1,
                    Name = "Test Pickaxe",
                    pick = 35
                };
                Terraria.Main.LocalPlayer = player;
                Terraria.Main.player[0] = player;
                Terraria.Main.myPlayer = 0;

                AutoMiningService.ObserveManualTileMined(10, 10, 7, 777, 2);
                var settings = AppSettings.CreateDefault();
                settings.WorldAutomationAutoMiningMode = AutoMiningModes.Auto;
                var queue = new InputActionQueue();
                var gameState = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Player = new PlayerStateSnapshot
                    {
                        Exists = true,
                        Active = true,
                        PositionX = 160f,
                        PositionY = 160f
                    }
                };

                AutoMiningService.Tick(
                    queue,
                    gameState,
                    new RuntimeState { UpdateCount = 1001 },
                    RuntimeSettingsSnapshot.FromSettings(settings));

                var queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 1)
                {
                    var failureDiagnostics = AutoMiningService.GetDiagnostics();
                    throw new InvalidOperationException(
                        "Expected Auto mode PickTile observation to submit sustained mining, got " +
                        queueSnapshot.PendingCount.ToString(CultureInfo.InvariantCulture) +
                        ". LastDecision=" +
                        (failureDiagnostics == null ? "<null>" : failureDiagnostics.LastDecision));
                }

                var diagnostics = AutoMiningService.GetDiagnostics();
                if (diagnostics == null ||
                    diagnostics.LastDecision == null ||
                    diagnostics.LastDecision.IndexOf("sustained mining target refreshed", StringComparison.Ordinal) < 0)
                {
                    throw new InvalidOperationException("Auto mining Auto mode should refresh a sustained target after the manual PickTile observation.");
                }

                var overlay = AutoMiningService.GetOverlaySnapshot();
                if (overlay == null ||
                    !string.Equals(overlay.Mode, AutoMiningModes.Auto, StringComparison.Ordinal) ||
                    overlay.Tiles == null ||
                    overlay.Tiles.Count <= 0)
                {
                    throw new InvalidOperationException("Auto mining Auto mode should keep the observed vein selection for overlay and target refresh.");
                }
            }
            finally
            {
                AutoMiningService.ResetForTesting();
                Terraria.Main.tile = previousTile;
                Terraria.Main.maxTilesX = previousMaxTilesX;
                Terraria.Main.maxTilesY = previousMaxTilesY;
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.player[0] = previousPlayer0;
                Terraria.Main.myPlayer = previousMyPlayer;
                Terraria.Main.GameUpdateCount = previousGameUpdateCount;
                restoreMainType();
            }
        }

        private static void AutoMiningRequestUsesSustainedRawInputMetadata()
        {
            var request = AutoMiningService.BuildSustainedMiningRequestForTesting(12, 34, 2, 777, AutoMiningModes.Hotkey, "Ctrl+M");
            if (request.Kind != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Auto mining must use RawInput sustained action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoMining, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoMining);
            AssertMetadata(request, ActionMetadataKeys.RawInputMode, "AutoMiningSustainedUse");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "2");
            AssertMetadata(request, ActionMetadataKeys.RequireSelectedSlotUnchanged, "true");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "200");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "552");
            AssertMetadata(request, "AutoMiningAction", "SustainedUse");
            AssertMetadata(request, "AutoMiningPickItemType", "777");
            AssertMetadata(request, "AutoMiningTileX", "12");
            AssertMetadata(request, "AutoMiningTileY", "34");
            AssertMetadata(request, "AutoMiningMode", AutoMiningModes.Hotkey);
            AssertMetadata(request, "SourceHotkey", "Ctrl+M");

            if (request.QueueTimeout != TimeSpan.FromMilliseconds(100))
            {
                throw new InvalidOperationException("Auto mining pending input should still expire quickly before it starts.");
            }

            if (request.Timeout < TimeSpan.FromMinutes(5))
            {
                throw new InvalidOperationException("Auto mining sustained use must not recycle on a short burst timeout while a large vein is still refreshing targets.");
            }
        }

        private static void AutoCaptureCritterRequestUsesSustainedRawInputMetadata()
        {
            var request = AutoCaptureCritterService.BuildCaptureRequestForTesting(12, 1991, 1, 7, 616, 120.5f, 88.25f, true);
            if (request.Kind != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Auto capture critter must use sustained RawInput action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoCaptureCritter, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoCaptureCritter);
            AssertMetadata(request, ActionMetadataKeys.RawInputMode, "AutoCaptureCritterSustainedUse");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "12");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "120.5");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "88.25");
            AssertMetadata(request, "AutoCaptureCritterAction", "SustainedUse");
            AssertMetadata(request, "AutoCaptureCritterNpcIndex", "7");
            AssertMetadata(request, "AutoCaptureCritterNpcType", "616");
            AssertMetadata(request, "BugNetCatchTool", "1");
            AssertMetadata(request, "FishingProtection", "true");
            AssertMetadata(request, "AutoCaptureCritterMode", AutoCaptureCritterModes.Auto);
        }

        private static void AutoCaptureCritterRangeUsesBugNetReach()
        {
            if (!AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 136f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Standard bug net reach should include critters intersecting the vanilla-like swing envelope.");
            }

            if (!AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 148f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Standard bug net reach should include critters within the one-tile trigger padding.");
            }

            if (AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 170f, 108f, 16, 16, 1))
            {
                throw new InvalidOperationException("Auto capture critter must not swing beyond the one-tile trigger padding.");
            }

            if (!AutoCaptureCritterService.IsWithinCaptureRangeForTesting(100f, 100f, 164f, 108f, 16, 16, 2))
            {
                throw new InvalidOperationException("Golden bug net reach should extend slightly beyond the standard bug net envelope.");
            }
        }

        private static void AutoCaptureCritterRestorePoleKeepsFishingSlotSelected()
        {
            var request = AutoCaptureCritterService.BuildRestorePoleRequestForTesting(1, 2294);
            if (request.Kind != InputActionKind.SelectHotbarSlot)
            {
                throw new InvalidOperationException("Fishing pole restore must keep using SelectHotbarSlot.");
            }

            AssertMetadata(request, "Slot", "1");
            AssertMetadata(request, "KeepSelected", "true");
        }

        private static void FishingFilterSkipHoldsSelectionUntilBobberGone()
        {
            var request = FishingAutomationService.BuildFilterSkipRequestForTesting(0, 1234, 1, 2294);
            if (request.Kind != InputActionKind.SelectHotbarSlot)
            {
                throw new InvalidOperationException("Fishing filter skip must keep using SelectHotbarSlot.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.FishingFilter, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.FishingFilterSkip);
            AssertMetadata(request, "Slot", "0");
            AssertMetadata(request, "PreferImmediateSelection", "true");
            AssertMetadata(request, "HoldUntilFishingBobberGone", "true");
            AssertMetadata(request, "FishingBobberIdentity", "1234");
            AssertMetadata(request, "FishingPoleSlot", "1");
            AssertMetadata(request, "FishingPoleItemType", "2294");
            AssertMetadata(request, "MaxBobberGoneWaitTicks", "90");
            if (request.Timeout < TimeSpan.FromSeconds(4))
            {
                throw new InvalidOperationException("Fishing filter skip timeout must cover bobber-gone hold and restore.");
            }
        }

        private static void FishingFilterNaturalWaitDoesNotForceTimeoutPull()
        {
            if (FishingAutomationService.ShouldForcePullFilterSkipTimeoutForTesting(true, true, 100, 190))
            {
                throw new InvalidOperationException("Natural filter skip wait must not turn into a timeout pull.");
            }
        }

        private static void FishingFilterNaturalWaitClearsAfterBiteExpires()
        {
            if (!FishingAutomationService.ShouldClearNaturalFilterSkipWaitForTesting(true, true, 42, 42, 0f))
            {
                throw new InvalidOperationException("Natural filter skip wait must clear when the same bobber is no longer hooked.");
            }

            if (FishingAutomationService.ShouldClearNaturalFilterSkipWaitForTesting(true, true, 42, 42, -1f))
            {
                throw new InvalidOperationException("Natural filter skip wait must keep waiting while the same bobber is still hooked.");
            }

            if (FishingAutomationService.ShouldClearNaturalFilterSkipWaitForTesting(true, false, 42, 42, 0f))
            {
                throw new InvalidOperationException("Cut-rod skip wait must still wait for bobber disappearance or restore.");
            }

            if (FishingAutomationService.ShouldClearNaturalFilterSkipWaitForTesting(true, true, 42, 43, 0f))
            {
                throw new InvalidOperationException("Natural filter skip wait must not clear from another bobber.");
            }
        }

        private static void FishingFilterCutRodSkipKeepsTimeoutProtection()
        {
            if (FishingAutomationService.ShouldForcePullFilterSkipTimeoutForTesting(true, false, 100, 189))
            {
                throw new InvalidOperationException("Filter skip timeout protection must not fire before the configured wait window.");
            }

            if (!FishingAutomationService.ShouldForcePullFilterSkipTimeoutForTesting(true, false, 100, 190))
            {
                throw new InvalidOperationException("Cut-rod filter skip must keep timeout protection after the wait window.");
            }
        }

        private static void SelectedItemStateForceSelectionUpdatesHotbarState()
        {
            var player = new TestSelectedItemStatePlayer(1);
            if (!TerrariaPlayerSelectionCompat.TryForceInventorySlotSelection(player, 0))
            {
                throw new InvalidOperationException("Expected selectedItemState force selection to support hotbar fallback: " + TerrariaPlayerSelectionCompat.LastError);
            }

            if (player.selectedItem != 0)
            {
                throw new InvalidOperationException("selectedItemState direct fallback did not update selected item.");
            }

            if (player.selectedItemState.HotbarForTesting != 0 ||
                player.selectedItemState.BufferedForTesting != -1 ||
                player.selectedItemState.OverriddenForTesting != -1)
            {
                throw new InvalidOperationException("selectedItemState direct fallback did not refresh hotbar and clear pending selection state.");
            }
        }

        private static void SelectedItemStateRequestAllowsDeferredSelection()
        {
            var player = new TestDeferredSelectedItemStatePlayer(1);
            bool selectedImmediately;
            if (!TerrariaPlayerSelectionCompat.TryRequestInventorySlotSelection(player, 9, out selectedImmediately))
            {
                throw new InvalidOperationException("Expected selectedItemState request selection to accept deferred selection: " + TerrariaPlayerSelectionCompat.LastError);
            }

            if (selectedImmediately)
            {
                throw new InvalidOperationException("Deferred selectedItemState request should not report immediate selection.");
            }

            if (player.selectedItem != 1 || player.selectedItemState.PendingForTesting != 9)
            {
                throw new InvalidOperationException("Deferred selectedItemState request should only buffer the target slot before Terraria applies it.");
            }

            player.ApplyPendingSelectionForTesting();
            int selectedItem;
            if (!TerrariaPlayerSelectionCompat.TryGetSelectedItem(player, out selectedItem) || selectedItem != 9)
            {
                throw new InvalidOperationException("Deferred selectedItemState request did not become visible after the pending selection was applied.");
            }

            if (TerrariaPlayerSelectionCompat.TrySelectInventorySlot(player, 8))
            {
                throw new InvalidOperationException("Immediate selected item selection should fail when selectedItemState only buffers the request.");
            }
        }

        private static void FishingLoadoutRestoreAttemptedKeepsSessionForRetry()
        {
            var requestId = Guid.NewGuid();
            FishingLoadoutService.ResetForTesting();
            FishingLoadoutService.SetRestoreSessionForTesting(requestId, 1, 0);
            FishingLoadoutService.OnActionCompleted(new InputActionResult
            {
                RequestId = requestId,
                Kind = InputActionKind.InventorySlot,
                Scenario = ScenarioNames.FishingAutoLoadoutRestore,
                Status = InputActionStatus.AttemptedButUnverified
            });

            // Restore sessions survive unverified terminal results so slot cleanup
            // can retry instead of stranding the fishing loadout in a partial state.
            if (!FishingLoadoutService.IsSessionActiveForTesting())
            {
                throw new InvalidOperationException("AttemptedButUnverified restore must keep fishing loadout session active for retry.");
            }

            var succeededRequestId = Guid.NewGuid();
            FishingLoadoutService.SetRestoreSessionForTesting(succeededRequestId, 1, 0);
            FishingLoadoutService.OnActionCompleted(new InputActionResult
            {
                RequestId = succeededRequestId,
                Kind = InputActionKind.InventorySlot,
                Scenario = ScenarioNames.FishingAutoLoadoutRestore,
                Status = InputActionStatus.Succeeded
            });

            if (FishingLoadoutService.IsSessionActiveForTesting())
            {
                throw new InvalidOperationException("Succeeded restore should clear fishing loadout session.");
            }

            FishingLoadoutService.ResetForTesting();
        }

        private sealed class TestSelectedItemStatePlayer
        {
            public TestSelectedItemState selectedItemState;

            public TestSelectedItemStatePlayer(int selectedItem)
            {
                selectedItemState = new TestSelectedItemState(selectedItem, selectedItem, 7, 8);
            }

            public int selectedItem
            {
                get { return selectedItemState.SelectedForTesting; }
            }
        }

        private sealed class TestDeferredSelectedItemStatePlayer
        {
            public TestDeferredSelectedItemState selectedItemState;

            public TestDeferredSelectedItemStatePlayer(int selectedItem)
            {
                selectedItemState = new TestDeferredSelectedItemState(selectedItem);
            }

            public int selectedItem
            {
                get { return selectedItemState.SelectedForTesting; }
            }

            public void ApplyPendingSelectionForTesting()
            {
                selectedItemState.ApplyPendingForTesting();
            }
        }

        private struct TestSelectedItemState
        {
            private int selected;
            private int hotbar;
            private int buffered;
            private int overridden;

            public TestSelectedItemState(int selected, int hotbar, int buffered, int overridden)
            {
                this.selected = selected;
                this.hotbar = hotbar;
                this.buffered = buffered;
                this.overridden = overridden;
            }

            public int SelectedForTesting
            {
                get { return selected; }
            }

            public int HotbarForTesting
            {
                get { return hotbar; }
            }

            public int BufferedForTesting
            {
                get { return buffered; }
            }

            public int OverriddenForTesting
            {
                get { return overridden; }
            }
        }

        private sealed class TestDeferredSelectedItemState
        {
            private int selected;
            private int hotbar;
            private int buffered;
            private int overridden;

            public TestDeferredSelectedItemState(int selected)
            {
                this.selected = selected;
                hotbar = selected;
                buffered = -1;
                overridden = -1;
            }

            public void Select(int slot)
            {
                buffered = slot;
            }

            public void ApplyPendingForTesting()
            {
                if (buffered < 0)
                {
                    return;
                }

                selected = buffered;
                hotbar = buffered;
                buffered = -1;
                overridden = -1;
            }

            public int SelectedForTesting
            {
                get { return selected; }
            }

            public int PendingForTesting
            {
                get { return buffered; }
            }

            public int HotbarForTesting
            {
                get { return hotbar; }
            }

            public int OverriddenForTesting
            {
                get { return overridden; }
            }
        }

        private static void AutoCaptureCritterRecognizesBugNetItemType()
        {
            var snapshot = new GameStateSnapshot
            {
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 6, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 7, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };

            InputActionRequest request;
            string message;
            if (!AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, out request, out message))
            {
                throw new InvalidOperationException("Expected bug net item type to produce a capture request: " + message);
            }

            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "6");
            AssertMetadata(request, "BugNetCatchTool", "1");
            AssertMetadata(request, "AutoCaptureCritterNpcIndex", "7");
        }

        private static void AutoCaptureCritterManualModeRequiresHeldBugNet()
        {
            var snapshot = new GameStateSnapshot
            {
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Inventory = new InventorySnapshot
                {
                    SelectedItemSlot = 0,
                    SelectedItem = new InventoryItemSnapshot { SlotIndex = 0, Type = 4, Name = "Copper Shortsword", Stack = 1 },
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = 4, Name = "Copper Shortsword", Stack = 1 },
                        new InventoryItemSnapshot { SlotIndex = 1 },
                        new InventoryItemSnapshot { SlotIndex = 2 },
                        new InventoryItemSnapshot { SlotIndex = 3 },
                        new InventoryItemSnapshot { SlotIndex = 4 },
                        new InventoryItemSnapshot { SlotIndex = 5 },
                        new InventoryItemSnapshot { SlotIndex = 6, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 7, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };

            InputActionRequest request;
            string message;
            if (AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Manual, out request, out message))
            {
                throw new InvalidOperationException("Manual auto capture mode must not use a bug net from backpack when the selected item is not a bug net.");
            }

            snapshot.Inventory.SelectedItemSlot = 6;
            snapshot.Inventory.SelectedItem = snapshot.Inventory.Items[6];
            if (!AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Manual, out request, out message))
            {
                throw new InvalidOperationException("Manual auto capture mode should accept the selected bug net: " + message);
            }

            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "6");
            AssertMetadata(request, "AutoCaptureCritterMode", AutoCaptureCritterModes.Manual);
            AssertMetadata(request, "BugNetCatchTool", "1");
        }

        private static void AutoCaptureCritterManualInventoryOpenRequiresSelectedHotbarBugNet()
        {
            var queue = new InputActionQueue();
            var snapshot = new GameStateSnapshot
            {
                IsInWorld = true,
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Ui = new UiStateSnapshot
                {
                    PlayerInventoryOpen = true
                },
                Inventory = new InventorySnapshot
                {
                    SelectedItemSlot = 0,
                    SelectedItem = new InventoryItemSnapshot { SlotIndex = 0, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 },
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 0, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 12, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };

            var settings = AppSettings.CreateDefault();
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Manual;
            try
            {
                AutoCaptureCritterService.ResetForTesting();
                AutoCaptureCritterService.Tick(
                    queue,
                    snapshot,
                    new RuntimeState { UpdateCount = 100 },
                    RuntimeSettingsSnapshot.FromSettings(settings));

                var queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 1)
                {
                    throw new InvalidOperationException("Manual auto capture should run with inventory open when the selected hotbar item is a bug net.");
                }

                var diagnostics = AutoCaptureCritterService.GetDiagnostics();
                AssertStringEquals(diagnostics.LastDecision, "submitted sustained capture request", "manual inventory-open capture decision");
                if (diagnostics.BugNetSlot != 0 || diagnostics.TargetNpcIndex != 12)
                {
                    throw new InvalidOperationException("Manual inventory-open capture should keep the selected hotbar bug net and target NPC.");
                }

                AutoCaptureCritterService.ResetForTesting();
                queue = new InputActionQueue();
                settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;
                AutoCaptureCritterService.Tick(
                    queue,
                    snapshot,
                    new RuntimeState { UpdateCount = 200 },
                    RuntimeSettingsSnapshot.FromSettings(settings));

                queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 0)
                {
                    throw new InvalidOperationException("Auto capture Auto mode must remain blocked while the player inventory is open.");
                }

                diagnostics = AutoCaptureCritterService.GetDiagnostics();
                AssertStringEquals(diagnostics.LastDecision, "blocked: player inventory UI open", "auto inventory-open capture decision");
            }
            finally
            {
                AutoCaptureCritterService.ResetForTesting();
            }
        }

        private static void AutoCaptureCritterCategoryDefaultsEnableAllOptions()
        {
            var settings = AppSettings.CreateDefault();
            var options = AutoCaptureCritterCategoryCatalog.Options;
            if (options == null || options.Length != 8)
            {
                throw new InvalidOperationException("Auto capture critter config must expose the requested eight UI categories.");
            }

            if (AutoCaptureCritterCategoryCatalog.CountDisabled(settings) != 0)
            {
                throw new InvalidOperationException("Auto capture critter category defaults must keep old capture behavior enabled.");
            }

            for (var index = 0; index < options.Length; index++)
            {
                var option = options[index];
                if (option == null || string.IsNullOrWhiteSpace(option.Label) || !AutoCaptureCritterCategoryCatalog.GetEnabled(settings, option.Id))
                {
                    throw new InvalidOperationException("Auto capture critter category default must be enabled: " + (option == null ? "<null>" : option.Id));
                }
            }
        }

        private static void AutoCaptureCritterCategoriesSeparateSpecialBait()
        {
            AutoCaptureCritterCategoryCatalog.ResetForTesting();
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.Worm, CatchItem = Terraria.ID.ItemID.Worm, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.Bait,
                "worm bait category");
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.TruffleWorm, CatchItem = Terraria.ID.ItemID.TruffleWorm, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.TruffleWorm,
                "truffle worm category");
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.EmpressButterfly, CatchItem = Terraria.ID.ItemID.EmpressButterfly, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.EmpressButterfly,
                "empress butterfly category");
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.FairyCritterBlue, CatchItem = Terraria.ID.ItemID.FairyCritterBlue, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.Fairy,
                "fairy category");
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.GoldWorm, CatchItem = Terraria.ID.ItemID.GoldWorm, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.GoldCritter,
                "gold critter category");
            AssertStringEquals(
                AutoCaptureCritterCategoryCatalog.Classify(new NpcSnapshot { Type = Terraria.ID.NPCID.GemBunnyRuby, CatchItem = Terraria.ID.ItemID.GemBunnyRuby, Critter = true }).Id,
                AutoCaptureCritterCategoryCatalog.GemCritter,
                "gem critter category");

            var settings = AppSettings.CreateDefault();
            settings.MiscAutoCaptureCritterBaitEnabled = false;
            if (AutoCaptureCritterCategoryCatalog.IsEnabledFor(settings, new NpcSnapshot { Type = Terraria.ID.NPCID.Worm, CatchItem = Terraria.ID.ItemID.Worm, Critter = true }) ||
                !AutoCaptureCritterCategoryCatalog.IsEnabledFor(settings, new NpcSnapshot { Type = Terraria.ID.NPCID.TruffleWorm, CatchItem = Terraria.ID.ItemID.TruffleWorm, Critter = true }))
            {
                throw new InvalidOperationException("Disabling bait must not disable truffle worms.");
            }

            settings.MiscAutoCaptureCritterBaitEnabled = true;
            settings.MiscAutoCaptureCritterTruffleWormEnabled = false;
            if (!AutoCaptureCritterCategoryCatalog.IsEnabledFor(settings, new NpcSnapshot { Type = Terraria.ID.NPCID.Worm, CatchItem = Terraria.ID.ItemID.Worm, Critter = true }) ||
                AutoCaptureCritterCategoryCatalog.IsEnabledFor(settings, new NpcSnapshot { Type = Terraria.ID.NPCID.TruffleWorm, CatchItem = Terraria.ID.ItemID.TruffleWorm, Critter = true }))
            {
                throw new InvalidOperationException("Disabling truffle worms must not disable ordinary bait.");
            }
        }

        private static void AutoCaptureCritterDisabledCategoryBlocksRequest()
        {
            var snapshot = CreateAutoCaptureCritterSnapshot(new NpcSnapshot
            {
                Active = true,
                WhoAmI = 7,
                Type = Terraria.ID.NPCID.Bunny,
                CatchItem = Terraria.ID.ItemID.Bunny,
                Critter = true,
                PositionX = 136f,
                PositionY = 108f,
                CenterX = 144f,
                CenterY = 116f,
                Width = 16,
                Height = 16
            });
            var settings = AppSettings.CreateDefault();
            settings.MiscAutoCaptureCritterNormalCritterEnabled = false;

            InputActionRequest request;
            string message;
            if (AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Auto, settings, out request, out message))
            {
                throw new InvalidOperationException("Disabled normal critter category must block bunny capture requests.");
            }

            AssertStringEquals(message, "catchable critters disabled by auto capture config", "disabled normal critter message");

            settings.MiscAutoCaptureCritterNormalCritterEnabled = true;
            if (!AutoCaptureCritterService.TryBuildCaptureRequestForTesting(snapshot, AutoCaptureCritterModes.Auto, settings, out request, out message))
            {
                throw new InvalidOperationException("Re-enabled normal critter category should allow bunny capture: " + message);
            }
        }

        private static GameStateSnapshot CreateAutoCaptureCritterSnapshot(NpcSnapshot critter)
        {
            return new GameStateSnapshot
            {
                Player = new PlayerStateSnapshot
                {
                    Exists = true,
                    Active = true,
                    PositionX = 100f,
                    PositionY = 100f
                },
                Inventory = new InventorySnapshot
                {
                    Items = new List<InventoryItemSnapshot>
                    {
                        new InventoryItemSnapshot { SlotIndex = 6, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                    }
                },
                Npcs = new NpcSummarySnapshot
                {
                    CatchableCritters = new List<NpcSnapshot> { critter }
                }
            };
        }

        private static void AutoCaptureCritterTickEnqueuesRequestWhenNearby()
        {
            var originalMode = ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode;
            try
            {
                AutoCaptureCritterService.ResetForTesting();
                ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Auto;

                var queue = new InputActionQueue();
                var snapshot = new GameStateSnapshot
                {
                    IsInWorld = true,
                    Player = new PlayerStateSnapshot
                    {
                        Exists = true,
                        Active = true,
                        PositionX = 100f,
                        PositionY = 100f
                    },
                    Inventory = new InventorySnapshot
                    {
                        Items = new List<InventoryItemSnapshot>
                        {
                            new InventoryItemSnapshot { SlotIndex = 8, Type = TerrariaBugNetCompat.BugNetItemType, Name = "Bug Net", Stack = 1 }
                        }
                    },
                    Npcs = new NpcSummarySnapshot
                    {
                    CatchableCritters = new List<NpcSnapshot>
                    {
                        new NpcSnapshot { Active = true, WhoAmI = 12, Type = 616, CatchItem = 4464, PositionX = 136f, PositionY = 108f, CenterX = 144f, CenterY = 116f, Width = 16, Height = 16 }
                    }
                }
            };
                var runtime = new RuntimeState
                {
                    UpdateCount = 100
                };

                AutoCaptureCritterService.Tick(queue, snapshot, runtime);

                var queueSnapshot = queue.GetSnapshot();
                if (queueSnapshot.PendingCount != 1)
                {
                    throw new InvalidOperationException("Expected auto capture critter to enqueue one request, got " + queueSnapshot.PendingCount.ToString(CultureInfo.InvariantCulture) + ".");
                }

                var diagnostics = AutoCaptureCritterService.GetDiagnostics();
                AssertStringEquals(diagnostics.LastDecision, "submitted sustained capture request", "auto capture last decision");
                if (diagnostics.BugNetSlot != 8 || diagnostics.TargetNpcIndex != 12)
                {
                    throw new InvalidOperationException("Expected diagnostics to keep the selected bug net slot and target NPC.");
                }
            }
            finally
            {
                ConfigService.AppSettings.WorldAutomationAutoCaptureCritterMode = originalMode;
                AutoCaptureCritterService.ResetForTesting();
            }
        }

        private static void AutoHarvestMapsExactHerbSeeds()
        {
            int seed;
            if (!AutoHarvestService.TryResolveSeedItemTypeForTesting(0, out seed) || seed != 307)
            {
                throw new InvalidOperationException("Daybloom herb style should map to Daybloom Seeds.");
            }

            if (!AutoHarvestService.TryResolveSeedItemTypeForTesting(6, out seed) || seed != 2357)
            {
                throw new InvalidOperationException("Shiverthorn herb style should map to Shiverthorn Seeds.");
            }

            if (AutoHarvestService.TryResolveSeedItemTypeForTesting(7, out seed))
            {
                throw new InvalidOperationException("Unknown herb styles must not map to a random seed.");
            }

            if (!AutoHarvestService.IsRegrowthToolForTesting(213) ||
                !AutoHarvestService.IsRegrowthToolForTesting(5295) ||
                AutoHarvestService.IsRegrowthToolForTesting(1991))
            {
                throw new InvalidOperationException("Auto harvest must only accept Staff of Regrowth or Axe of Regrowth as harvest tools.");
            }
        }

        private static void AutoHarvestRequestUsesSustainedRawInputMetadata()
        {
            var request = AutoHarvestService.BuildSustainedHarvestRequestForTesting(4, 213, 20, 30, 84, 2, 309);
            if (request.Kind != InputActionKind.RawInput)
            {
                throw new InvalidOperationException("Auto harvest must use RawInput sustained action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoHarvest, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvest);
            AssertMetadata(request, ActionMetadataKeys.RawInputMode, "AutoHarvestSustainedUse");
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "4");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "328");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "488");
            AssertMetadata(request, "AutoHarvestAction", "HarvestSustainedUse");
            AssertMetadata(request, "AutoHarvestToolItemType", "213");
            AssertMetadata(request, "AutoHarvestTileX", "20");
            AssertMetadata(request, "AutoHarvestTileY", "30");
            AssertMetadata(request, "AutoHarvestHerbStyle", "2");
            AssertMetadata(request, "AutoHarvestSeedItemType", "309");
        }

        private static void AutoHarvestReplantRequestUsesExactSeedMetadata()
        {
            var request = AutoHarvestService.BuildReplantRequestForTesting(12, 2357, 42, 64, 6);
            if (request.Kind != InputActionKind.ItemUse)
            {
                throw new InvalidOperationException("Auto harvest replant must use ItemUse action.");
            }

            AssertStringEquals(request.SourceFeatureId, FeatureIds.WorldAutomationAutoHarvest, "source feature id");
            AssertMetadata(request, ActionMetadataKeys.Scenario, ScenarioNames.WorldAutomationAutoHarvestReplant);
            AssertMetadata(request, ActionMetadataKeys.TargetSlot, "12");
            AssertMetadata(request, ActionMetadataKeys.WorldX, "680");
            AssertMetadata(request, ActionMetadataKeys.WorldY, "1032");
            AssertMetadata(request, "AutoHarvestAction", "Replant");
            AssertMetadata(request, "AutoHarvestTileX", "42");
            AssertMetadata(request, "AutoHarvestTileY", "64");
            AssertMetadata(request, "AutoHarvestHerbStyle", "6");
            AssertMetadata(request, "AutoHarvestSeedItemType", "2357");
        }

        private static void AutoMiningTargetsNearestReachableFrontierTile()
        {
            var tiles = new List<AutoMiningTile>
            {
                new AutoMiningTile(9, 9),
                new AutoMiningTile(10, 9),
                new AutoMiningTile(11, 9),
                new AutoMiningTile(9, 10),
                new AutoMiningTile(10, 10),
                new AutoMiningTile(11, 10),
                new AutoMiningTile(9, 11),
                new AutoMiningTile(10, 11),
                new AutoMiningTile(11, 11),
                new AutoMiningTile(14, 10)
            };

            int remaining;
            var target = AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile => tile.X != 14,
                168f,
                168f,
                out remaining);

            if (remaining != 10)
            {
                throw new InvalidOperationException("Auto mining remaining count should include all active vein tiles.");
            }

            if (target == null || target.X != 10 || target.Y != 9)
            {
                throw new InvalidOperationException("Auto mining should skip the enclosed center tile and choose the nearest reachable frontier tile.");
            }
        }

        private static void AutoMiningSkipsReachChecksForInteriorTiles()
        {
            var tiles = new List<AutoMiningTile>
            {
                new AutoMiningTile(9, 9),
                new AutoMiningTile(10, 9),
                new AutoMiningTile(11, 9),
                new AutoMiningTile(9, 10),
                new AutoMiningTile(10, 10),
                new AutoMiningTile(11, 10),
                new AutoMiningTile(9, 11),
                new AutoMiningTile(10, 11),
                new AutoMiningTile(11, 11)
            };

            var reachChecks = 0;
            int remaining;
            AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile =>
                {
                    reachChecks++;
                    if (tile.X == 10 && tile.Y == 10)
                    {
                        throw new InvalidOperationException("Auto mining must not run expensive reach checks for enclosed interior tiles.");
                    }

                    return true;
                },
                168f,
                168f,
                out remaining);

            if (reachChecks != 8)
            {
                throw new InvalidOperationException("Auto mining should only reach-check the eight frontier tiles in a 3x3 vein.");
            }
        }

        private static void AutoMiningRefusesReachableInteriorFallback()
        {
            var tiles = new List<AutoMiningTile>
            {
                new AutoMiningTile(9, 9),
                new AutoMiningTile(10, 9),
                new AutoMiningTile(11, 9),
                new AutoMiningTile(9, 10),
                new AutoMiningTile(10, 10),
                new AutoMiningTile(11, 10),
                new AutoMiningTile(9, 11),
                new AutoMiningTile(10, 11),
                new AutoMiningTile(11, 11)
            };

            int remaining;
            var target = AutoMiningService.ChooseNextTargetForTesting(
                tiles,
                tile => true,
                tile => tile.X == 10 && tile.Y == 10,
                168f,
                168f,
                out remaining);

            if (target != null || remaining != 9)
            {
                throw new InvalidOperationException("Auto mining must wait instead of handing an enclosed non-frontier tile to sustained ItemCheck.");
            }
        }

        private static void AutoMiningSustainedUseValidatesExactMineableTarget()
        {
            var player = new Terraria.Player
            {
                position = new Terraria.TestVector2 { X = 160f, Y = 160f },
                width = 20,
                height = 42
            };
            Terraria.Main.tile = new object[64, 64];
            SetTestTile(11, 11, true, 7);

            var target = new AutoMiningSustainedUseTarget
            {
                PickSlot = 0,
                PickItemType = 777,
                TileX = 11,
                TileY = 11,
                TileType = 7,
                PickPower = 35,
                TileBoost = 0,
                UpdatedUtc = DateTime.UtcNow
            };

            string message;
            if (!AutoMiningSustainedUseBridge.IsTargetStillMineableForTesting(player, Terraria.Main.tile, target, out message))
            {
                throw new InvalidOperationException("Auto mining sustained target should pass when the exact selected ore is active, reachable and pickable: " + message);
            }

            SetTestTile(11, 11, false, 7);
            if (AutoMiningSustainedUseBridge.IsTargetStillMineableForTesting(player, Terraria.Main.tile, target, out message))
            {
                throw new InvalidOperationException("Auto mining sustained target must fail closed when the target tile is no longer active.");
            }

            SetTestTile(11, 11, true, 8);
            if (AutoMiningSustainedUseBridge.IsTargetStillMineableForTesting(player, Terraria.Main.tile, target, out message))
            {
                throw new InvalidOperationException("Auto mining sustained target must fail closed when the target tile type changed.");
            }

            SetTestTile(11, 11, true, 111);
            target.TileType = 111;
            target.PickPower = 149;
            if (AutoMiningSustainedUseBridge.IsTargetStillMineableForTesting(player, Terraria.Main.tile, target, out message))
            {
                throw new InvalidOperationException("Auto mining sustained target must fail closed when the held pickaxe cannot hurt the target type.");
            }

            SetTestTile(30, 11, true, 7);
            target.TileX = 30;
            target.TileY = 11;
            target.TileType = 7;
            target.PickPower = 35;
            if (AutoMiningSustainedUseBridge.IsTargetStillMineableForTesting(player, Terraria.Main.tile, target, out message))
            {
                throw new InvalidOperationException("Auto mining sustained target must fail closed when the target is outside mining reach.");
            }
        }

        private static void AutoMiningReachExcludesRectangleCornersOutsideHelperRadius()
        {
            var centerX = 170f;
            var centerY = 181f;
            var maxDistanceWorld = 80f;

            if (!AutoMiningCompat.IsTileInsideReachShapeForTesting(11, 7, 5, 6, 16, 16, centerX, centerY, maxDistanceWorld))
            {
                throw new InvalidOperationException("Auto mining should keep tiles that are inside both the reach rectangle and helper-style radius.");
            }

            if (AutoMiningCompat.IsTileInsideReachShapeForTesting(16, 6, 5, 6, 16, 16, centerX, centerY, maxDistanceWorld))
            {
                throw new InvalidOperationException("Auto mining must not mark rectangle corner tiles green when they are outside the helper-style mining radius.");
            }
        }

        private static void AutoMiningReachUsesVanillaTileRegionWhenAvailable()
        {
            var player = new Terraria.Player
            {
                position = new Terraria.TestVector2 { X = 160f, Y = 160f },
                width = 20,
                height = 42
            };

            int left;
            int top;
            int right;
            int bottom;
            if (!AutoMiningCompat.TryGetVanillaTileReachRegionForTesting(player, 2, 100, 100, out left, out top, out right, out bottom))
            {
                throw new InvalidOperationException("Auto mining should resolve Terraria TileReachCheckSettings.Simple.GetTileRegion when it is available.");
            }

            if (left != 3 || top != 4 || right != 18 || bottom != 18)
            {
                throw new InvalidOperationException("Auto mining vanilla reach region should preserve LX/LY/HX/HY order.");
            }

            string source;
            if (!AutoMiningCompat.IsTileInMiningReachForTesting(player, 18, 18, 2, out source) ||
                !string.Equals(source, "vanillaTileReachRegion", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto mining should mark the vanilla TileReachCheckSettings edge as reachable.");
            }

            if (AutoMiningCompat.IsTileInMiningReachForTesting(player, 19, 18, 2, out source))
            {
                throw new InvalidOperationException("Auto mining must not expand beyond the vanilla TileReachCheckSettings region.");
            }
        }

        private static void AutoMiningTakeoverRejectsVanillaEdgeOutsideStrictRadius()
        {
            var player = new Terraria.Player
            {
                position = new Terraria.TestVector2 { X = 160f, Y = 160f },
                width = 20,
                height = 42
            };

            if (!AutoMiningCompat.IsTileInMiningReachForTesting(player, 5, 11, 0, out var source) ||
                !string.Equals(source, "vanillaTileReachRegion", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Test setup expected the vanilla reach rectangle edge to remain detectable.");
            }

            if (AutoMiningCompat.CanMineTileWithPickaxe(player, 5, 11, 7, 35, 0))
            {
                throw new InvalidOperationException("Auto mining takeover must reject rectangle-edge tiles outside the strict mining radius.");
            }

            if (!AutoMiningCompat.CanMineTileWithPickaxe(player, 6, 11, 7, 35, 0))
            {
                throw new InvalidOperationException("Auto mining takeover should still allow a nearby tile inside both vanilla reach and strict radius.");
            }
        }

        private static void AutoMiningTakeoverPreservesNegativeTileBoost()
        {
            var player = new Terraria.Player
            {
                position = new Terraria.TestVector2 { X = 160f, Y = 160f },
                width = 20,
                height = 42
            };

            if (AutoMiningCompat.CanMineTileWithPickaxe(player, 10, 6, 7, 35, -1))
            {
                throw new InvalidOperationException("Auto mining takeover must not expand a negative pickaxe tileBoost to zero.");
            }

            if (!AutoMiningCompat.CanMineTileWithPickaxe(player, 10, 7, 7, 35, -1))
            {
                throw new InvalidOperationException("Auto mining takeover should still allow the shrunken vanilla reach boundary.");
            }
        }

        private static void AutoMiningReachKeepsFallbackDetectableWhenVanillaRegionUnavailable()
        {
            var player = new AutoMiningFallbackReachPlayer
            {
                position = new AutoMiningFallbackReachVector { X = 160f, Y = 160f },
                width = 20,
                height = 42
            };

            string source;
            if (!AutoMiningCompat.IsTileInMiningReachForTesting(player, 11, 7, 0, out source) ||
                !string.Equals(source, "fallbackMiningRange", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Auto mining fallback reach should remain available and detectable when vanilla region cannot accept the player type.");
            }

            if (AutoMiningCompat.IsTileInMiningReachForTesting(player, 16, 6, 0, out source))
            {
                throw new InvalidOperationException("Auto mining fallback must keep the conservative helper-style radius instead of expanding reach.");
            }
        }

        private static void AutoMiningGreenReachRespectsPickPower()
        {
            if (AutoMiningCompat.IsPickPowerSufficientForTileForTesting(111, 400, 149))
            {
                throw new InvalidOperationException("Auto mining must keep adamantite tiles red until the held pickaxe reaches the required power.");
            }

            if (!AutoMiningCompat.IsPickPowerSufficientForTileForTesting(111, 400, 150))
            {
                throw new InvalidOperationException("Auto mining should allow green reach once the held pickaxe meets the tile requirement.");
            }
        }

        private static void AutoMiningItemCheckOverrideSyncsExactTileTarget()
        {
            var restoreMainType = PushFakeTerrariaMainType();
            try
            {
                var player = new Terraria.Player
                {
                    selectedItem = 2
                };
                Terraria.Main.screenPosition = new Terraria.TestVector2 { X = 0f, Y = 0f };
                Terraria.Player.tileTargetX = 0;
                Terraria.Player.tileTargetY = 0;

                if (!TerrariaInputCompat.TryApplyAutoMiningSustainedUseForItemCheck(player, 2, 6 * 16f + 8f, 11 * 16f + 8f, 0))
                {
                    throw new InvalidOperationException("Auto mining ItemCheck override should apply in the test harness: " + TerrariaInputCompat.LastInputCompatError);
                }

                int tileX;
                int tileY;
                if (!TerrariaInputCompat.TryReadTileTarget(out tileX, out tileY) ||
                    tileX != 6 ||
                    tileY != 11)
                {
                    throw new InvalidOperationException("Auto mining ItemCheck override must sync Player.tileTargetX/Y to the exact selected ore tile.");
                }
            }
            finally
            {
                restoreMainType();
            }
        }

        private static void AutoMiningOverlayUsesLowAlphaGreenRedStyle()
        {
            var reachable = AutoMiningOverlayService.ResolveTileStyleForTesting(true);
            var unreachable = AutoMiningOverlayService.ResolveTileStyleForTesting(false);
            var reachableDraw = AutoMiningOverlayService.ResolveTileDrawStyleForTesting(true);
            var unreachableDraw = AutoMiningOverlayService.ResolveTileDrawStyleForTesting(false);

            if (reachable.R != 150 ||
                reachable.G != 216 ||
                reachable.B != 138)
            {
                throw new InvalidOperationException("Auto mining reachable overlay should use the requested 96D88A muted green tint.");
            }

            if (unreachable.R != 240 ||
                unreachable.G != 160 ||
                unreachable.B != 142)
            {
                throw new InvalidOperationException("Auto mining unreachable overlay should use the requested F0A08E muted red tint.");
            }

            if (reachable.FillAlpha != 64 ||
                unreachable.FillAlpha != 64)
            {
                throw new InvalidOperationException("Auto mining overlay fill alpha must stay transparent while remaining visible in darker world lighting.");
            }

            if (reachableDraw.R != 38 ||
                reachableDraw.G != 54 ||
                reachableDraw.B != 35 ||
                unreachableDraw.R != 60 ||
                unreachableDraw.G != 40 ||
                unreachableDraw.B != 36)
            {
                throw new InvalidOperationException("Auto mining overlay must premultiply muted tints for AlphaBlend without making dark caves swallow the marker.");
            }

            if (reachable.BorderAlpha != 0 ||
                unreachable.BorderAlpha != 0 ||
                reachableDraw.BorderAlpha != 0 ||
                unreachableDraw.BorderAlpha != 0)
            {
                throw new InvalidOperationException("Auto mining overlay must not draw per-tile borders; dense selections should not become a grid.");
            }
        }

        private static Dictionary<int, bool> AutoMiningDownKeys(params int[] keys)
        {
            var down = new Dictionary<int, bool>();
            for (var index = 0; keys != null && index < keys.Length; index++)
            {
                down[keys[index]] = true;
            }

            return down;
        }

        private sealed class AutoMiningFallbackReachPlayer
        {
            public AutoMiningFallbackReachVector position;
            public int width;
            public int height;
        }

        private sealed class AutoMiningFallbackReachVector
        {
            public float X;
            public float Y;
        }


    }
}
