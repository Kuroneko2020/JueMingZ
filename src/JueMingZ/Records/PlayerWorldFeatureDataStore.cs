using System;
using System.IO;
using System.Runtime.Serialization.Json;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    public static class PlayerWorldFeatureDataStore
    {
        private static Action<string, Type> _writeObserverForTesting;

        public static bool TryWriteJson<T>(string path, T value, out string message)
            where T : class
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                message = "target path unavailable";
                return false;
            }

            if (value == null)
            {
                message = "value unavailable";
                return false;
            }

            NotifyWriteObserverForTesting(path, typeof(T));
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
                    var serializer = CreateSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }

                ReplaceCompletedTempFile(tempPath, path);
                message = "saved";
                return true;
            }
            catch (Exception error)
            {
                TryDeleteTemp(tempPath);
                message = error.GetType().Name + ": " + error.Message;
                LogThrottle.WarnThrottled(
                    "player-world-feature-data-save-failed:" + path,
                    TimeSpan.FromSeconds(30),
                    "PlayerWorldFeatureDataStore",
                    "Player-world feature data save failed: " + message);
                return false;
            }
        }

        public static bool TryReadJson<T>(string path, out T value, out string message)
            where T : class
        {
            value = null;
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                message = "target path unavailable";
                return false;
            }

            try
            {
                if (!File.Exists(path))
                {
                    message = "missing";
                    return false;
                }

                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var serializer = CreateSerializer(typeof(T));
                    value = serializer.ReadObject(stream) as T;
                }

                if (value == null)
                {
                    message = "empty";
                    return false;
                }

                message = "loaded";
                return true;
            }
            catch (Exception error)
            {
                message = error.GetType().Name + ": " + error.Message;
                LogThrottle.WarnThrottled(
                    "player-world-feature-data-read-failed:" + path,
                    TimeSpan.FromSeconds(30),
                    "PlayerWorldFeatureDataStore",
                    "Player-world feature data read failed: " + message);
                return false;
            }
        }

        internal static bool TryWriteJsonForTesting<T>(string path, T value, out string message)
            where T : class
        {
            return TryWriteJson(path, value, out message);
        }

        internal static void SetWriteObserverForTesting(Action<string, Type> observer)
        {
            _writeObserverForTesting = observer;
        }

        internal static void ResetTestingHooks()
        {
            _writeObserverForTesting = null;
        }

        private static void NotifyWriteObserverForTesting(string path, Type type)
        {
            var observer = _writeObserverForTesting;
            if (observer == null)
            {
                return;
            }

            observer(path, type);
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type);
        }

        private static void ReplaceCompletedTempFile(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                // Player/world identity files gate later feature writes. A failed
                // replace must preserve the old identity instead of overwriting it.
                File.Replace(tempPath, targetPath, null);
            }
            catch (FileNotFoundException)
            {
                if (!File.Exists(targetPath))
                {
                    File.Move(tempPath, targetPath);
                    return;
                }

                throw;
            }
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
