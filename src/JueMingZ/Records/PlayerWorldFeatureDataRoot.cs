using System;
using System.IO;

namespace JueMingZ.Records
{
    public static class PlayerWorldFeatureDataRoot
    {
        public const string PlayerDirectoryName = "players";
        public const string WorldDirectoryName = "worlds";
        public const string PlayerWorldDirectoryName = "player-worlds";
        public const string PairIdentityFileName = "identity.json";
        public const string DeathEventsFileName = "deaths.jsonl";
        public const string DeathSummaryFileName = "death-summary.json";
        public const string PlaytimeFileName = "playtime.json";
        public const string ExplorationSummaryFileName = "exploration-summary.json";
        public const string MapMarkersFileName = "map-markers.json";
        public const string FootprintsFileName = "footprints.json";

        private static readonly object SyncRoot = new object();
        private static string _dataRootDirectory;

        public static string DataRootDirectory
        {
            get
            {
                lock (SyncRoot)
                {
                    if (string.IsNullOrWhiteSpace(_dataRootDirectory))
                    {
                        _dataRootDirectory = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games",
                            "Terraria",
                            "JueMing-Z",
                            "data");
                    }

                    return _dataRootDirectory;
                }
            }
        }

        public static string PlayersDirectory
        {
            get { return Path.Combine(DataRootDirectory, PlayerDirectoryName); }
        }

        public static string WorldsDirectory
        {
            get { return Path.Combine(DataRootDirectory, WorldDirectoryName); }
        }

        public static string PlayerWorldsDirectory
        {
            get { return Path.Combine(DataRootDirectory, PlayerWorldDirectoryName); }
        }

        public static string BuildPlayerIdentityPath(string playerId)
        {
            EnsureSafeIdentifier(playerId, "playerId");
            return Path.Combine(PlayersDirectory, playerId + ".json");
        }

        public static string BuildWorldIdentityPath(string worldId)
        {
            EnsureSafeIdentifier(worldId, "worldId");
            return Path.Combine(WorldsDirectory, worldId + ".json");
        }

        public static string BuildPlayerWorldDirectory(string pairId)
        {
            EnsureSafeIdentifier(pairId, "pairId");
            return Path.Combine(PlayerWorldsDirectory, pairId);
        }

        public static string BuildPlayerWorldIdentityPath(string pairId)
        {
            return Path.Combine(BuildPlayerWorldDirectory(pairId), PairIdentityFileName);
        }

        public static string BuildPlayerWorldFeatureFilePath(string pairId, string fileName)
        {
            EnsureSafeIdentifier(pairId, "pairId");
            EnsureSafeFileName(fileName, "fileName");
            return Path.Combine(BuildPlayerWorldDirectory(pairId), fileName);
        }

        internal static void SetDataRootDirectoryForTesting(string path)
        {
            lock (SyncRoot)
            {
                _dataRootDirectory = path;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _dataRootDirectory = null;
            }
        }

        private static void EnsureSafeIdentifier(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier is required.", argumentName);
            }

            EnsureNoPathCharacters(value, argumentName);
        }

        private static void EnsureSafeFileName(string value, string argumentName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("File name is required.", argumentName);
            }

            if (!string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal))
            {
                throw new ArgumentException("Feature data file name must not contain a path.", argumentName);
            }

            EnsureNoPathCharacters(value, argumentName);
        }

        private static void EnsureNoPathCharacters(string value, string argumentName)
        {
            if (value.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                value.IndexOf(Path.AltDirectorySeparatorChar) >= 0 ||
                value.IndexOf(':') >= 0 ||
                value.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("Value must stay within the player-world data root.", argumentName);
            }
        }
    }
}
