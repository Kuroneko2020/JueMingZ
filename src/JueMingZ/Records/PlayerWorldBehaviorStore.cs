using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    public sealed class PlayerWorldBehaviorContext
    {
        public string PlayerKey { get; set; }
        public string WorldKey { get; set; }
        public string PlayerName { get; set; }
        public string WorldName { get; set; }

        public PlayerWorldBehaviorContext()
        {
            PlayerKey = string.Empty;
            WorldKey = string.Empty;
            PlayerName = string.Empty;
            WorldName = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldBehaviorFile
    {
        [DataMember(Order = 1)]
        public int Version { get; set; } = 1;

        [DataMember(Order = 2)]
        public string PlayerKey { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public string WorldKey { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public string PlayerName { get; set; } = string.Empty;

        [DataMember(Order = 5)]
        public string WorldName { get; set; } = string.Empty;

        [DataMember(Order = 6)]
        public string CreatedUtc { get; set; } = string.Empty;

        [DataMember(Order = 7)]
        public string LastUpdatedUtc { get; set; } = string.Empty;

        [DataMember(Order = 8)]
        public List<PlayerWorldOpenedChestRecord> OpenedChests { get; set; } = new List<PlayerWorldOpenedChestRecord>();
    }

    [DataContract]
    public sealed class PlayerWorldOpenedChestRecord
    {
        [DataMember(Order = 1)]
        public int X { get; set; }

        [DataMember(Order = 2)]
        public int Y { get; set; }

        [DataMember(Order = 3)]
        public string FirstOpenedUtc { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public string Source { get; set; } = string.Empty;
    }

    public static class PlayerWorldBehaviorStore
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, PlayerWorldBehaviorFile> Cache = new Dictionary<string, PlayerWorldBehaviorFile>(StringComparer.Ordinal);

        private static string _behaviorDirectory;

        public static string BehaviorDirectory
        {
            get
            {
                lock (SyncRoot)
                {
                    if (string.IsNullOrWhiteSpace(_behaviorDirectory))
                    {
                        _behaviorDirectory = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "My Games",
                            "Terraria",
                            "JueMing-Z",
                            "data",
                            "behavior");
                    }

                    return _behaviorDirectory;
                }
            }
        }

        public static bool IsUsable(PlayerWorldBehaviorContext context)
        {
            return context != null &&
                   !string.IsNullOrWhiteSpace(context.PlayerKey) &&
                   !string.IsNullOrWhiteSpace(context.WorldKey);
        }

        public static string BuildIdentityKey(string path, string fallback)
        {
            var cleanPath = NormalizePath(path);
            if (!string.IsNullOrWhiteSpace(cleanPath))
            {
                return "path:" + cleanPath;
            }

            var cleanFallback = NormalizeKeyPart(fallback);
            return string.IsNullOrWhiteSpace(cleanFallback) ? string.Empty : "value:" + cleanFallback;
        }

        public static bool TryRecordOpenedChest(PlayerWorldBehaviorContext context, int x, int y, string source, out bool added, out string message)
        {
            added = false;
            message = string.Empty;
            if (!IsUsable(context))
            {
                message = "player/world behavior context unavailable";
                return false;
            }

            if (x <= 0 || y <= 0)
            {
                message = "invalid chest coordinates";
                return false;
            }

            lock (SyncRoot)
            {
                var file = LoadLocked(context);
                if (ContainsOpenedChestLocked(file, x, y))
                {
                    return true;
                }

                var now = FormatUtc(DateTime.UtcNow);
                EnsureFileShape(file);
                file.OpenedChests.Add(new PlayerWorldOpenedChestRecord
                {
                    X = x,
                    Y = y,
                    FirstOpenedUtc = now,
                    Source = source ?? string.Empty
                });

                TouchIdentity(file, context, now);
                SaveLocked(context, file);
                added = true;
                return true;
            }
        }

        public static int ImportOpenedChests(PlayerWorldBehaviorContext context, IList<PlayerWorldOpenedChestRecord> records, string source)
        {
            if (!IsUsable(context) || records == null || records.Count == 0)
            {
                return 0;
            }

            lock (SyncRoot)
            {
                var file = LoadLocked(context);
                EnsureFileShape(file);

                var now = FormatUtc(DateTime.UtcNow);
                var added = 0;
                for (var index = 0; index < records.Count; index++)
                {
                    var record = records[index];
                    if (record == null || record.X <= 0 || record.Y <= 0 || ContainsOpenedChestLocked(file, record.X, record.Y))
                    {
                        continue;
                    }

                    file.OpenedChests.Add(new PlayerWorldOpenedChestRecord
                    {
                        X = record.X,
                        Y = record.Y,
                        FirstOpenedUtc = string.IsNullOrWhiteSpace(record.FirstOpenedUtc) ? now : record.FirstOpenedUtc,
                        Source = string.IsNullOrWhiteSpace(record.Source) ? source ?? string.Empty : record.Source
                    });
                    added++;
                }

                if (added > 0)
                {
                    TouchIdentity(file, context, now);
                    SaveLocked(context, file);
                }

                return added;
            }
        }

        public static bool TryMarkFirstVisit(PlayerWorldBehaviorContext context, out bool isFirstVisit, out string message)
        {
            isFirstVisit = false;
            message = string.Empty;
            if (!IsUsable(context))
            {
                message = "player/world behavior context unavailable";
                return false;
            }

            lock (SyncRoot)
            {
                var filePath = BuildScopedFilePath(context);
                if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
                {
                    isFirstVisit = true;
                }

                var now = FormatUtc(DateTime.UtcNow);
                var file = LoadLocked(context);
                TouchIdentity(file, context, now);
                if (isFirstVisit || string.IsNullOrWhiteSpace(file.LastUpdatedUtc))
                {
                    SaveLocked(context, file);
                }

                return true;
            }
        }

        public static bool TryMarkPlayerFirstLoad(PlayerWorldBehaviorContext context, out bool isFirstVisit, out string message)
        {
            isFirstVisit = false;
            message = string.Empty;
            if (context == null || string.IsNullOrWhiteSpace(context.PlayerKey))
            {
                message = "player behavior context unavailable";
                return false;
            }

            var playerFirstLoadContext = new PlayerWorldBehaviorContext
            {
                PlayerKey = context.PlayerKey,
                WorldKey = "player-first-load",
                PlayerName = context.PlayerName ?? string.Empty,
                WorldName = "all-worlds"
            };

            lock (SyncRoot)
            {
                var filePath = BuildScopedFilePath(playerFirstLoadContext);
                if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath))
                {
                    isFirstVisit = true;
                }

                var now = FormatUtc(DateTime.UtcNow);
                var file = LoadLocked(playerFirstLoadContext);
                TouchIdentity(file, playerFirstLoadContext, now);
                if (isFirstVisit || string.IsNullOrWhiteSpace(file.LastUpdatedUtc))
                {
                    SaveLocked(playerFirstLoadContext, file);
                }

                return true;
            }
        }

        public static List<PlayerWorldOpenedChestRecord> GetOpenedChests(PlayerWorldBehaviorContext context)
        {
            if (!IsUsable(context))
            {
                return new List<PlayerWorldOpenedChestRecord>();
            }

            lock (SyncRoot)
            {
                var file = LoadLocked(context);
                EnsureFileShape(file);
                return CloneOpenedChests(file.OpenedChests);
            }
        }

        public static bool ContainsOpenedChest(PlayerWorldBehaviorContext context, int x, int y)
        {
            if (!IsUsable(context))
            {
                return false;
            }

            lock (SyncRoot)
            {
                var file = LoadLocked(context);
                return ContainsOpenedChestLocked(file, x, y);
            }
        }

        public static string BuildOpenedChestsHash(PlayerWorldBehaviorContext context)
        {
            var records = GetOpenedChests(context);
            if (records.Count == 0)
            {
                return "0";
            }

            records.Sort((left, right) =>
            {
                var yCompare = left.Y.CompareTo(right.Y);
                return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
            });

            unchecked
            {
                var hash = 17;
                for (var index = 0; index < records.Count; index++)
                {
                    hash = hash * 31 + records[index].X;
                    hash = hash * 31 + records[index].Y;
                }

                return records.Count.ToString(CultureInfo.InvariantCulture) + ":" + hash.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal static string BuildScopedFileNameForTesting(string playerKey, string worldKey)
        {
            return BuildScopedFileName(playerKey, worldKey);
        }

        internal static void SetBehaviorDirectoryForTesting(string path)
        {
            lock (SyncRoot)
            {
                _behaviorDirectory = path;
                Cache.Clear();
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _behaviorDirectory = null;
                Cache.Clear();
            }
        }

        private static PlayerWorldBehaviorFile LoadLocked(PlayerWorldBehaviorContext context)
        {
            var fileName = BuildScopedFileName(context.PlayerKey, context.WorldKey);
            PlayerWorldBehaviorFile cached;
            if (Cache.TryGetValue(fileName, out cached))
            {
                EnsureFileShape(cached);
                return cached;
            }

            var path = Path.Combine(BehaviorDirectory, fileName);
            try
            {
                if (!File.Exists(path))
                {
                    var created = CreateNewFile(context);
                    Cache[fileName] = created;
                    return created;
                }

                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PlayerWorldBehaviorFile));
                    var loaded = serializer.ReadObject(stream) as PlayerWorldBehaviorFile ?? CreateNewFile(context);
                    EnsureFileShape(loaded);
                    Cache[fileName] = loaded;
                    return loaded;
                }
            }
            catch (Exception error)
            {
                Logger.Warn("PlayerWorldBehaviorStore", "Behavior record read failed; using in-memory empty record: " + error.Message);
                var fallback = CreateNewFile(context);
                Cache[fileName] = fallback;
                return fallback;
            }
        }

        private static void SaveLocked(PlayerWorldBehaviorContext context, PlayerWorldBehaviorFile file)
        {
            var fileName = BuildScopedFileName(context.PlayerKey, context.WorldKey);
            var path = Path.Combine(BehaviorDirectory, fileName);
            var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PlayerWorldBehaviorFile));
                    serializer.WriteObject(stream, file);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
                Cache[fileName] = file;
            }
            catch (Exception error)
            {
                Logger.Warn("PlayerWorldBehaviorStore", "Behavior record save failed: " + error.Message);
                TryDeleteTemp(tempPath);
            }
        }

        private static PlayerWorldBehaviorFile CreateNewFile(PlayerWorldBehaviorContext context)
        {
            var now = FormatUtc(DateTime.UtcNow);
            var file = new PlayerWorldBehaviorFile
            {
                CreatedUtc = now
            };
            TouchIdentity(file, context, now);
            return file;
        }

        private static void TouchIdentity(PlayerWorldBehaviorFile file, PlayerWorldBehaviorContext context, string now)
        {
            if (file == null || context == null)
            {
                return;
            }

            file.Version = 1;
            file.PlayerKey = context.PlayerKey ?? string.Empty;
            file.WorldKey = context.WorldKey ?? string.Empty;
            file.PlayerName = context.PlayerName ?? string.Empty;
            file.WorldName = context.WorldName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(file.CreatedUtc))
            {
                file.CreatedUtc = now;
            }

            file.LastUpdatedUtc = now;
            EnsureFileShape(file);
        }

        private static bool ContainsOpenedChestLocked(PlayerWorldBehaviorFile file, int x, int y)
        {
            if (file == null || file.OpenedChests == null)
            {
                return false;
            }

            for (var index = 0; index < file.OpenedChests.Count; index++)
            {
                var record = file.OpenedChests[index];
                if (record != null && record.X == x && record.Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<PlayerWorldOpenedChestRecord> CloneOpenedChests(IList<PlayerWorldOpenedChestRecord> records)
        {
            var result = new List<PlayerWorldOpenedChestRecord>();
            if (records == null)
            {
                return result;
            }

            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                if (record == null)
                {
                    continue;
                }

                result.Add(new PlayerWorldOpenedChestRecord
                {
                    X = record.X,
                    Y = record.Y,
                    FirstOpenedUtc = record.FirstOpenedUtc ?? string.Empty,
                    Source = record.Source ?? string.Empty
                });
            }

            return result;
        }

        private static void EnsureFileShape(PlayerWorldBehaviorFile file)
        {
            if (file == null)
            {
                return;
            }

            if (file.OpenedChests == null)
            {
                file.OpenedChests = new List<PlayerWorldOpenedChestRecord>();
            }
        }

        private static string BuildScopedFileName(string playerKey, string worldKey)
        {
            return Sha256Hex((playerKey ?? string.Empty) + "\n" + (worldKey ?? string.Empty)) + ".json";
        }

        private static string BuildScopedFilePath(PlayerWorldBehaviorContext context)
        {
            return !IsUsable(context)
                ? string.Empty
                : Path.Combine(BehaviorDirectory, BuildScopedFileName(context.PlayerKey, context.WorldKey));
        }

        private static string Sha256Hex(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var index = 0; index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Trim().Replace('\\', '/').ToLowerInvariant();
        }

        private static string NormalizeKeyPart(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        private static void TryDeleteTemp(string tempPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}
