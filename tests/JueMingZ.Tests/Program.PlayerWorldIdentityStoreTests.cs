using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Records;
using JueMingZ.Runtime;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void PlayerWorldIdentityResolverSeparatesSameDisplayNamesByStableSources()
        {
            PlayerWorldIdentityResolution first;
            if (!PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Terraria\Players\Hero.plr", "Hero", @"C:\Terraria\Worlds\Shared.wld", "Shared", "11111111-1111-1111-1111-111111111111", "11111111-1111-1111-1111-111111111111", 1001),
                out first))
            {
                throw new InvalidOperationException("Expected first identity to resolve.");
            }

            PlayerWorldIdentityResolution second;
            if (!PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Terraria\PlayersCopy\Hero.plr", "Hero", @"C:\Terraria\WorldsCopy\Shared.wld", "Shared", "22222222-2222-2222-2222-222222222222", "22222222-2222-2222-2222-222222222222", 1001),
                out second))
            {
                throw new InvalidOperationException("Expected second identity to resolve.");
            }

            if (string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Same display-name players with different paths must not merge.");
            }

            if (string.Equals(first.WorldId, second.WorldId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Same display-name worlds with different unique ids must not merge.");
            }

            if (string.Equals(first.PairId, second.PairId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Different player-world identities must not share a pair id.");
            }
        }

        private static void PlayerWorldIdentityResolverHonorsWorldSourcePriority()
        {
            PlayerWorldIdentityResolution unique;
            PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Players\A.plr", "A", @"C:\Worlds\W.wld", "W", "33333333-3333-3333-3333-333333333333", "map-from-guid", 77),
                out unique);
            AssertStringEquals(unique.WorldIdentitySourceKind, PlayerWorldIdentitySourceKind.WorldUniqueId, "world unique id priority");

            PlayerWorldIdentityResolution map;
            PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Players\A.plr", "A", @"C:\Worlds\W.wld", "W", string.Empty, "map-from-file", 77),
                out map);
            AssertStringEquals(map.WorldIdentitySourceKind, PlayerWorldIdentitySourceKind.WorldMapFileName, "world map file priority");

            PlayerWorldIdentityResolution idPath;
            PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Players\A.plr", "A", @"C:\Worlds\W.wld", "W", string.Empty, string.Empty, 77),
                out idPath);
            AssertStringEquals(idPath.WorldIdentitySourceKind, PlayerWorldIdentitySourceKind.WorldIdPathHash, "world id and path fallback priority");
        }

        private static void PlayerWorldIdentityResolverFallbackDoesNotUseUnknownBucket()
        {
            PlayerWorldIdentityResolution resolution;
            if (!PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(string.Empty, "Fallback Hero", string.Empty, "Fallback World", string.Empty, "fallback-map", 0),
                out resolution))
            {
                throw new InvalidOperationException("Expected fallback identity to resolve without path.");
            }

            AssertStringEquals(resolution.PlayerIdentitySourceKind, PlayerWorldIdentitySourceKind.PlayerDisplayNameFallback, "player fallback source");
            AssertStringEquals(resolution.WorldIdentitySourceKind, PlayerWorldIdentitySourceKind.WorldMapFileName, "world fallback source");
            AssertDoesNotContain(resolution.PlayerId, "unknown", "player fallback id");
            AssertDoesNotContain(resolution.WorldId, "unknown", "world fallback id");
            AssertDoesNotContain(resolution.PairId, "unknown", "pair fallback id");
        }

        private static void PlayerWorldIdentityPairIdIsStable()
        {
            PlayerWorldIdentityResolution first;
            PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"C:\Terraria\Players\Case.plr", "Case", @"C:\Terraria\Worlds\CaseWorld.wld", "Case World", "44444444-4444-4444-4444-444444444444", string.Empty, 1234),
                out first);

            PlayerWorldIdentityResolution second;
            PlayerWorldIdentityResolver.TryResolveForTesting(
                BuildIdentityFacts(@"c:/terraria/players/case.plr", "Case", @"c:/terraria/worlds/caseworld.wld", "Case World", "44444444-4444-4444-4444-444444444444", string.Empty, 1234),
                out second);

            AssertStringEquals(first.PlayerId, second.PlayerId, "normalized player path id");
            AssertStringEquals(first.WorldId, second.WorldId, "normalized world id");
            AssertStringEquals(first.PairId, second.PairId, "stable pair id");
        }

        private static void PlayerWorldIdentityStoreWritesExpectedDirectories()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldIdentityResolution resolution;
                if (!PlayerWorldIdentityResolver.TryResolveAndPersist(
                    BuildIdentityFacts(@"C:\Players\Store.plr", "Store Player", @"C:\Worlds\Store.wld", "Store World", "55555555-5555-5555-5555-555555555555", "store-map", 555),
                    out resolution))
                {
                    throw new InvalidOperationException("Expected persisted identity to resolve.");
                }

                if (!resolution.IdentityFilesWritten)
                {
                    throw new InvalidOperationException("Expected identity files to be written: " + resolution.StorageMessage);
                }

                var playerPath = PlayerWorldFeatureDataRoot.BuildPlayerIdentityPath(resolution.PlayerId);
                var worldPath = PlayerWorldFeatureDataRoot.BuildWorldIdentityPath(resolution.WorldId);
                var pairPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldIdentityPath(resolution.PairId);
                var deathPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(resolution.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);

                AssertPathUnderRoot(playerPath, root, "player identity path");
                AssertPathUnderRoot(worldPath, root, "world identity path");
                AssertPathUnderRoot(pairPath, root, "pair identity path");
                AssertPathUnderRoot(deathPath, root, "death feature path");

                if (!File.Exists(playerPath) || !File.Exists(worldPath) || !File.Exists(pairPath))
                {
                    throw new InvalidOperationException("Expected all three identity files to exist.");
                }

                var playerFile = ReadJsonFile<PlayerIdentityFile>(playerPath);
                var worldFile = ReadJsonFile<WorldIdentityFile>(worldPath);
                var pairFile = ReadJsonFile<PlayerWorldIdentityFile>(pairPath);

                AssertStringEquals(playerFile.PlayerId, resolution.PlayerId, "stored player id");
                AssertStringEquals(worldFile.WorldId, resolution.WorldId, "stored world id");
                AssertStringEquals(pairFile.PairId, resolution.PairId, "stored pair id");
                AssertStringEquals(pairFile.PlayerId, resolution.PlayerId, "stored pair player id");
                AssertStringEquals(pairFile.WorldId, resolution.WorldId, "stored pair world id");
                if (playerFile.ObservedAliases == null || playerFile.ObservedAliases.Count == 0)
                {
                    throw new InvalidOperationException("Expected player identity aliases to be recorded.");
                }
            });
        }

        private static void PlayerWorldIdentitySafeWriteFailureKeepsExistingFile()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var path = PlayerWorldFeatureDataRoot.BuildPlayerIdentityPath("player-save-failure");
                string message;
                if (!PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                    path,
                    new PlayerIdentityFile
                    {
                        PlayerId = "player-save-failure",
                        DisplayName = "old"
                    },
                    out message))
                {
                    throw new InvalidOperationException("Expected initial identity write to succeed: " + message);
                }

                var originalJson = File.ReadAllText(path, Encoding.UTF8);
                bool saved;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    saved = PlayerWorldFeatureDataStore.TryWriteJsonForTesting(
                        path,
                        new PlayerIdentityFile
                        {
                            PlayerId = "player-save-failure",
                            DisplayName = "new"
                        },
                        out message);
                }

                if (saved)
                {
                    throw new InvalidOperationException("Expected locked identity replacement to fail.");
                }

                var afterFailureJson = File.ReadAllText(path, Encoding.UTF8);
                AssertStringEquals(afterFailureJson, originalJson, "identity file bytes after failed replacement");
                var tempFiles = Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + ".tmp-*");
                if (tempFiles.Length != 0)
                {
                    throw new InvalidOperationException("Expected failed identity replacement to clean temporary files.");
                }
            });
        }

        private static void PlayerWorldIdentityRuntimeCacheThrottlesExplorationIdentityPersistence()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreIdentity = PushFakePlayerWorldIdentity(
                    @"C:\Players\HotPath.plr",
                    "Hot Path",
                    @"C:\Worlds\HotPath.wld",
                    "Hot Path World",
                    "66666666-6666-6666-6666-666666666666",
                    "hot-path-map",
                    666,
                    5,
                    4);
                try
                {
                    ResetPlayerWorldIdentityHotPathState();
                    var identityWriteCount = 0;
                    PlayerWorldFeatureDataStore.SetWriteObserverForTesting((path, type) =>
                    {
                        if (IsIdentityWritePath(path))
                        {
                            identityWriteCount++;
                        }
                    });

                    var reader = FakeExplorationMapReader.CreateStriped(5, 4);
                    PlayerWorldExplorationService.SetMapReaderForTesting(reader);
                    var snapshot = BuildInWorldSnapshot();
                    for (var tick = 0L; tick <= 50L; tick += 10L)
                    {
                        PlayerWorldExplorationService.Tick(snapshot, new RuntimeState { UpdateCount = tick });
                    }

                    if (PlayerWorldIdentityRuntimeCache.PersistAttemptCountForTesting != 1)
                    {
                        throw new InvalidOperationException("Exploration scan cadence must not persist identity on repeated cached ticks.");
                    }

                    if (identityWriteCount != 3)
                    {
                        throw new InvalidOperationException("Expected one identity persist batch with three files, got " + identityWriteCount + ".");
                    }
                }
                finally
                {
                    PlayerWorldFeatureDataStore.ResetTestingHooks();
                    ResetPlayerWorldIdentityHotPathState();
                    restoreIdentity();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void PlayerWorldIdentityRuntimeCacheThrottlesPlaytimeIdentityPersistence()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreIdentity = PushFakePlayerWorldIdentity(
                    @"C:\Players\PlaytimeHot.plr",
                    "Playtime Hot",
                    @"C:\Worlds\PlaytimeHot.wld",
                    "Playtime Hot World",
                    "88888888-8888-8888-8888-888888888888",
                    "playtime-hot-map",
                    888,
                    20,
                    10);
                try
                {
                    ResetPlayerWorldIdentityHotPathState();
                    var identityWriteCount = 0;
                    PlayerWorldFeatureDataStore.SetWriteObserverForTesting((path, type) =>
                    {
                        if (IsIdentityWritePath(path))
                        {
                            identityWriteCount++;
                        }
                    });

                    var snapshot = BuildInWorldSnapshot();
                    Terraria.Main.dayTime = true;
                    Terraria.Main.dayRate = 60;
                    Terraria.Main.time = 1000d;
                    PlayerWorldPlaytimeService.Tick(snapshot, new RuntimeState { UpdateCount = 0 });
                    Terraria.Main.time = 2000d;
                    PlayerWorldPlaytimeService.Tick(snapshot, new RuntimeState { UpdateCount = 60 });
                    Terraria.Main.time = 3000d;
                    PlayerWorldPlaytimeService.Tick(snapshot, new RuntimeState { UpdateCount = 120 });

                    if (PlayerWorldIdentityRuntimeCache.PersistAttemptCountForTesting != 1)
                    {
                        throw new InvalidOperationException("Playtime sample cadence must not persist identity on repeated cached ticks.");
                    }

                    if (identityWriteCount != 3)
                    {
                        throw new InvalidOperationException("Expected one playtime identity persist batch with three files, got " + identityWriteCount + ".");
                    }
                }
                finally
                {
                    PlayerWorldFeatureDataStore.ResetTestingHooks();
                    ResetPlayerWorldIdentityHotPathState();
                    restoreIdentity();
                    restoreRuntimeTypes();
                }
            });
        }

        private static void PlayerWorldIdentityRuntimeCachePersistsAfterPairChange()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                PlayerWorldIdentityRuntimeCache.ResetForTesting();
                var identityWriteCount = 0;
                PlayerWorldFeatureDataStore.SetWriteObserverForTesting((path, type) =>
                {
                    if (IsIdentityWritePath(path))
                    {
                        identityWriteCount++;
                    }
                });

                try
                {
                    PlayerWorldIdentityResolution first;
                    if (!PlayerWorldIdentityRuntimeCache.TryResolveCachedForTesting(
                            BuildIdentityFacts(
                                @"C:\Players\PairA.plr",
                                "Pair A",
                                @"C:\Worlds\PairA.wld",
                                "Pair World A",
                                "99999999-9999-9999-9999-999999999999",
                                "pair-a-map",
                                999),
                            0,
                            out first))
                    {
                        throw new InvalidOperationException("Expected first cached identity to resolve.");
                    }

                    PlayerWorldIdentityResolution second;
                    if (!PlayerWorldIdentityRuntimeCache.TryResolveCachedForTesting(
                            BuildIdentityFacts(
                                @"C:\Players\PairB.plr",
                                "Pair B",
                                @"C:\Worlds\PairB.wld",
                                "Pair World B",
                                "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
                                "pair-b-map",
                                1000),
                            10,
                            out second))
                    {
                        throw new InvalidOperationException("Expected changed cached identity to resolve.");
                    }

                    if (string.Equals(first.PairId, second.PairId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Pair change test requires two distinct pair ids.");
                    }

                    if (PlayerWorldIdentityRuntimeCache.PersistAttemptCountForTesting != 2 || identityWriteCount != 6)
                    {
                        throw new InvalidOperationException("Pair changes must persist a new identity batch exactly once.");
                    }

                    if (!File.Exists(PlayerWorldFeatureDataRoot.BuildPlayerWorldIdentityPath(first.PairId)) ||
                        !File.Exists(PlayerWorldFeatureDataRoot.BuildPlayerWorldIdentityPath(second.PairId)))
                    {
                        throw new InvalidOperationException("Pair identity files must exist for both cached pairs.");
                    }
                }
                finally
                {
                    PlayerWorldFeatureDataStore.ResetTestingHooks();
                    PlayerWorldIdentityRuntimeCache.ResetForTesting();
                }
            });
        }

        private static void PlayerWorldIdentityRuntimeCacheFailClosedForExplorationAndPlaytime()
        {
            WithTemporaryPlayerWorldDataRoot(root =>
            {
                var restoreRuntimeTypes = PushFakeTerrariaMainType();
                var restoreIdentity = PushFakePlayerWorldIdentity(
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    0,
                    0,
                    0);
                try
                {
                    ResetPlayerWorldIdentityHotPathState();
                    var reader = new FakeExplorationMapReader(5, 4);
                    PlayerWorldExplorationService.SetMapReaderForTesting(reader);
                    var snapshot = BuildInWorldSnapshot();

                    PlayerWorldExplorationService.Tick(snapshot, new RuntimeState { UpdateCount = 0 });
                    PlayerWorldPlaytimeService.Tick(snapshot, new RuntimeState { UpdateCount = 0 });

                    var exploration = PlayerWorldExplorationDiagnostics.GetSnapshot();
                    if (reader.ReadCount != 0 ||
                        !string.IsNullOrWhiteSpace(exploration.LastPairId) ||
                        (exploration.LastStatus ?? string.Empty).IndexOf("IdentityUnavailable", StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException("Exploration must fail closed before reading map tiles when identity is unavailable.");
                    }

                    var playtime = PlayerWorldPlaytimeDiagnostics.GetSnapshot();
                    if (!string.IsNullOrWhiteSpace(playtime.LastPairId) ||
                        (playtime.LastStatus ?? string.Empty).IndexOf("IdentityUnavailable", StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException("Playtime must fail closed before sampling world time when identity is unavailable.");
                    }

                    if (Directory.Exists(PlayerWorldFeatureDataRoot.PlayerWorldsDirectory) &&
                        Directory.GetFiles(PlayerWorldFeatureDataRoot.PlayerWorldsDirectory, "*", SearchOption.AllDirectories).Length != 0)
                    {
                        throw new InvalidOperationException("Identity-unavailable services must not create player-world data files.");
                    }
                }
                finally
                {
                    ResetPlayerWorldIdentityHotPathState();
                    restoreIdentity();
                    restoreRuntimeTypes();
                }
            });
        }

        private static PlayerWorldIdentityFacts BuildIdentityFacts(
            string playerPath,
            string playerName,
            string worldPath,
            string worldName,
            string uniqueId,
            string mapFileName,
            int worldId)
        {
            return new PlayerWorldIdentityFacts
            {
                PlayerPath = playerPath,
                PlayerName = playerName,
                WorldPath = worldPath,
                WorldName = worldName,
                WorldUniqueId = uniqueId,
                MapFileName = mapFileName,
                WorldId = worldId,
                HasWorldId = worldId > 0,
                WorldSizeX = 4200,
                WorldSizeY = 1200
            };
        }

        private static GameStateSnapshot BuildInWorldSnapshot()
        {
            return new GameStateSnapshot
            {
                TerrariaDetected = true,
                IsInWorld = true,
                IsInMainMenu = false
            };
        }

        private static void ResetPlayerWorldIdentityHotPathState()
        {
            PlayerWorldIdentityRuntimeCache.ResetForTesting();
            PlayerWorldExplorationService.ResetForTesting();
            PlayerWorldExplorationCache.ResetForTesting();
            PlayerWorldExplorationDiagnostics.ResetForTesting();
            PlayerWorldPlaytimeService.ResetForTesting();
            PlayerWorldPlaytimeCache.ResetForTesting();
            PlayerWorldPlaytimeDiagnostics.ResetForTesting();
        }

        private static bool IsIdentityWritePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var root = Path.GetFullPath(PlayerWorldFeatureDataRoot.DataRootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var relative = full.Substring(root.Length).Replace('\\', '/');
            return relative.StartsWith(PlayerWorldFeatureDataRoot.PlayerDirectoryName + "/", StringComparison.Ordinal) ||
                   relative.StartsWith(PlayerWorldFeatureDataRoot.WorldDirectoryName + "/", StringComparison.Ordinal) ||
                   (relative.StartsWith(PlayerWorldFeatureDataRoot.PlayerWorldDirectoryName + "/", StringComparison.Ordinal) &&
                    relative.EndsWith("/" + PlayerWorldFeatureDataRoot.PairIdentityFileName, StringComparison.Ordinal));
        }

        private static Action PushFakePlayerWorldIdentity(
            string playerPath,
            string playerName,
            string worldPath,
            string worldName,
            string uniqueId,
            string mapFileName,
            int worldId,
            int width,
            int height)
        {
            var previousPlayerFile = Terraria.Main.ActivePlayerFileData;
            var previousWorldFile = Terraria.Main.ActiveWorldFileData;
            var previousGameMenu = Terraria.Main.gameMenu;
            var previousWorldId = Terraria.Main.worldID;
            var previousMaxTilesX = Terraria.Main.maxTilesX;
            var previousMaxTilesY = Terraria.Main.maxTilesY;
            var previousWorldName = Terraria.Main.worldName;
            var previousWorldNameClean = Terraria.Main.worldNameClean;
            var previousWorldPathName = Terraria.Main.worldPathName;
            var previousLocalPlayer = Terraria.Main.LocalPlayer;
            var previousMyPlayer = Terraria.Main.myPlayer;
            var previousPlayers = Terraria.Main.player;

            Terraria.Main.gameMenu = false;
            Terraria.Main.ActivePlayerFileData = new FakePlayerFileData
            {
                Path = playerPath ?? string.Empty,
                Name = playerName ?? string.Empty
            };
            Terraria.Main.ActiveWorldFileData = new FakeWorldFileData
            {
                Path = worldPath ?? string.Empty,
                Name = worldName ?? string.Empty,
                UniqueId = uniqueId ?? string.Empty,
                MapFileName = mapFileName ?? string.Empty,
                WorldId = worldId,
                WorldSizeX = width,
                WorldSizeY = height
            };
            Terraria.Main.worldID = worldId;
            Terraria.Main.maxTilesX = width;
            Terraria.Main.maxTilesY = height;
            Terraria.Main.worldName = worldName ?? string.Empty;
            Terraria.Main.worldNameClean = worldName ?? string.Empty;
            Terraria.Main.worldPathName = worldPath ?? string.Empty;
            Terraria.Main.myPlayer = 0;
            Terraria.Main.LocalPlayer = new Terraria.Player { name = playerName ?? string.Empty, Name = playerName ?? string.Empty };
            Terraria.Main.player = new object[256];
            Terraria.Main.player[0] = Terraria.Main.LocalPlayer;

            return () =>
            {
                Terraria.Main.ActivePlayerFileData = previousPlayerFile;
                Terraria.Main.ActiveWorldFileData = previousWorldFile;
                Terraria.Main.gameMenu = previousGameMenu;
                Terraria.Main.worldID = previousWorldId;
                Terraria.Main.maxTilesX = previousMaxTilesX;
                Terraria.Main.maxTilesY = previousMaxTilesY;
                Terraria.Main.worldName = previousWorldName;
                Terraria.Main.worldNameClean = previousWorldNameClean;
                Terraria.Main.worldPathName = previousWorldPathName;
                Terraria.Main.LocalPlayer = previousLocalPlayer;
                Terraria.Main.myPlayer = previousMyPlayer;
                Terraria.Main.player = previousPlayers;
            };
        }

        private sealed class FakePlayerFileData
        {
            public string Path = string.Empty;
            public bool IsCloudSave;
            public string Name = string.Empty;
        }

        private sealed class FakeWorldFileData
        {
            public string Path = string.Empty;
            public bool IsCloudSave;
            public string Name = string.Empty;
            public string UniqueId = string.Empty;
            public int WorldId;
            public string MapFileName = string.Empty;
            public int WorldSizeX;
            public int WorldSizeY;
        }

        private static T ReadJsonFile<T>(string path)
            where T : class
        {
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                var value = serializer.ReadObject(stream) as T;
                if (value == null)
                {
                    throw new InvalidOperationException("Expected JSON file to deserialize: " + path);
                }

                return value;
            }
        }

        private static void WithTemporaryPlayerWorldDataRoot(Action<string> body)
        {
            var directory = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(),
                "JueMingZ.Tests",
                "player-world-identity-" + Guid.NewGuid().ToString("N")));
            try
            {
                PlayerWorldFeatureDataRoot.SetDataRootDirectoryForTesting(directory);
                body(directory);
            }
            finally
            {
                PlayerWorldFeatureDataRoot.ResetForTesting();
                TryDeletePlayerWorldDataRoot(directory);
            }
        }

        private static void AssertPathUnderRoot(string path, string root, string label)
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(label + " escaped data root: " + fullPath);
            }
        }

        private static void AssertDoesNotContain(string value, string unexpected, string label)
        {
            if ((value ?? string.Empty).IndexOf(unexpected, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(label + " unexpectedly contained '" + unexpected + "'.");
            }
        }

        private static void TryDeletePlayerWorldDataRoot(string directory)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }
    }
}
