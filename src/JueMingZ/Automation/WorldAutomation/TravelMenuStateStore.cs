using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.WorldAutomation
{
    [DataContract]
    public sealed class TravelMenuStateFile
    {
        [DataMember(Order = 1)]
        public int Version { get; set; } = 1;

        [DataMember(Order = 2)]
        public List<TravelMenuRestoreMarker> Markers { get; set; } = new List<TravelMenuRestoreMarker>();
    }

    [DataContract]
    public sealed class TravelMenuRestoreMarker
    {
        [DataMember(Order = 1)]
        public bool Active { get; set; }

        [DataMember(Order = 2)]
        public string PlayerPath { get; set; }

        [DataMember(Order = 3)]
        public string WorldPath { get; set; }

        [DataMember(Order = 4)]
        public string PlayerName { get; set; }

        [DataMember(Order = 5)]
        public string WorldName { get; set; }

        [DataMember(Order = 6)]
        public int OriginalPlayerDifficulty { get; set; }

        [DataMember(Order = 7)]
        public int OriginalWorldGameMode { get; set; }

        [DataMember(Order = 8)]
        public int OriginalMainGameMode { get; set; }

        [DataMember(Order = 9)]
        public string CreatedUtc { get; set; }

        [DataMember(Order = 10)]
        public string LastSeenUtc { get; set; }

        [DataMember(Order = 11)]
        public string LastRestoreUtc { get; set; }

        [DataMember(Order = 12)]
        public string LastMessage { get; set; }

        public TravelMenuRestoreMarker()
        {
            PlayerPath = string.Empty;
            WorldPath = string.Empty;
            PlayerName = string.Empty;
            WorldName = string.Empty;
            CreatedUtc = string.Empty;
            LastSeenUtc = string.Empty;
            LastRestoreUtc = string.Empty;
            LastMessage = string.Empty;
        }
    }

    public static class TravelMenuStateStore
    {
        private static readonly object SyncRoot = new object();
        private static TravelMenuStateFile _state;

        public static string StatePath
        {
            get { return Path.Combine(ConfigService.ConfigDirectory, "travel-menu-state.json"); }
        }

        private static string BackupPath
        {
            get { return StatePath + ".bak"; }
        }

        public static bool TryFindActiveMarker(TravelMenuContext context, out TravelMenuRestoreMarker marker)
        {
            marker = null;
            if (context == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                var state = LoadLocked();
                marker = FindLocked(state, context, true);
                return marker != null;
            }
        }

        public static void UpsertActiveMarker(TravelMenuContext context, string message)
        {
            if (context == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var state = LoadLocked();
                var marker = FindLocked(state, context, false);
                var now = FormatUtc(DateTime.UtcNow);
                if (marker == null)
                {
                    marker = new TravelMenuRestoreMarker();
                    state.Markers.Add(marker);
                    marker.CreatedUtc = now;
                }

                marker.Active = true;
                marker.PlayerPath = context.PlayerPath ?? string.Empty;
                marker.WorldPath = context.WorldPath ?? string.Empty;
                marker.PlayerName = context.PlayerName ?? string.Empty;
                marker.WorldName = context.WorldName ?? string.Empty;
                marker.OriginalPlayerDifficulty = context.PlayerDifficulty;
                marker.OriginalWorldGameMode = context.WorldGameMode;
                marker.OriginalMainGameMode = context.MainGameMode;
                marker.LastSeenUtc = now;
                marker.LastMessage = message ?? string.Empty;
                SaveLocked(state);
            }
        }

        public static void MarkRestored(TravelMenuContext context, string message)
        {
            if (context == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var state = LoadLocked();
                var marker = FindLocked(state, context, true);
                if (marker == null)
                {
                    return;
                }

                marker.Active = false;
                marker.LastRestoreUtc = FormatUtc(DateTime.UtcNow);
                marker.LastMessage = message ?? string.Empty;
                SaveLocked(state);
            }
        }

        private static TravelMenuRestoreMarker FindLocked(TravelMenuStateFile state, TravelMenuContext context, bool activeOnly)
        {
            if (state == null || state.Markers == null || context == null)
            {
                return null;
            }

            for (var index = state.Markers.Count - 1; index >= 0; index--)
            {
                var marker = state.Markers[index];
                if (marker == null)
                {
                    continue;
                }

                if (activeOnly && !marker.Active)
                {
                    continue;
                }

                if (SameKey(marker.PlayerPath, context.PlayerPath) &&
                    SameKey(marker.WorldPath, context.WorldPath))
                {
                    return marker;
                }
            }

            return null;
        }

        private static bool SameKey(string left, string right)
        {
            return string.Equals(
                NormalizePath(left),
                NormalizePath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        private static TravelMenuStateFile LoadLocked()
        {
            if (_state != null)
            {
                EnsureStateShape(_state);
                return _state;
            }

            try
            {
                var path = StatePath;
                if (!File.Exists(path))
                {
                    if (TryLoadFromFile(BackupPath, out _state))
                    {
                        SaveLocked(_state);
                        return _state;
                    }

                    _state = new TravelMenuStateFile();
                    SaveLocked(_state);
                    return _state;
                }

                if (TryLoadFromFile(path, out _state))
                {
                    return _state;
                }

                if (TryLoadFromFile(BackupPath, out _state))
                {
                    Logger.Warn("TravelMenuStateStore", "Travel menu state primary read failed; recovered from backup.");
                    SaveLocked(_state);
                    return _state;
                }

                _state = new TravelMenuStateFile();
                return _state;
            }
            catch (Exception error)
            {
                Logger.Warn("TravelMenuStateStore", "Travel menu state read failed; using empty state: " + error.Message);
                _state = new TravelMenuStateFile();
                return _state;
            }
        }

        private static bool TryLoadFromFile(string path, out TravelMenuStateFile state)
        {
            state = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var serializer = new DataContractJsonSerializer(typeof(TravelMenuStateFile));
                    state = serializer.ReadObject(stream) as TravelMenuStateFile ?? new TravelMenuStateFile();
                    EnsureStateShape(state);
                    return true;
                }
            }
            catch (Exception error)
            {
                Logger.Warn("TravelMenuStateStore", "Travel menu state file read failed: " + path + " " + error.Message);
                state = null;
                return false;
            }
        }

        private static void SaveLocked(TravelMenuStateFile state)
        {
            string tempPath = null;
            try
            {
                EnsureStateShape(state);
                var path = StatePath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var serializer = new DataContractJsonSerializer(typeof(TravelMenuStateFile));
                    serializer.WriteObject(stream, state);
                    stream.Flush(true);
                }

                ReplaceStateFile(tempPath, path);
                tempPath = null;
                TryCopyFile(path, BackupPath);
            }
            catch (Exception error)
            {
                Logger.Warn("TravelMenuStateStore", "Travel menu state save failed: " + error.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempPath))
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch (Exception cleanupError)
                    {
                        Logger.Debug("TravelMenuStateStore", "Travel menu state temp cleanup failed: " + cleanupError.Message);
                    }
                }
            }
        }

        private static void ReplaceStateFile(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                File.Replace(tempPath, targetPath, null, true);
            }
            catch (Exception error)
            {
                Logger.Warn("TravelMenuStateStore", "Travel menu state atomic replace failed; falling back to copy: " + error.Message);
                File.Copy(tempPath, targetPath, true);
                File.Delete(tempPath);
            }
        }

        private static void TryCopyFile(string sourcePath, string targetPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath) &&
                    !string.IsNullOrWhiteSpace(targetPath) &&
                    File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, targetPath, true);
                }
            }
            catch (Exception error)
            {
                Logger.Warn("TravelMenuStateStore", "Travel menu state backup copy failed: " + error.Message);
            }
        }

        private static void EnsureStateShape(TravelMenuStateFile state)
        {
            if (state == null)
            {
                return;
            }

            if (state.Markers == null)
            {
                state.Markers = new List<TravelMenuRestoreMarker>();
            }
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
