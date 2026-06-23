using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintJsonSafeFileStore
    {
        private static readonly object TestingSyncRoot = new object();
        private static Func<string, string> _tempPathFactoryForTesting;
        private static Func<string, bool> _commitFailurePredicateForTesting;

        public static BlueprintStorageOperationResult TryRead<T>(string path, out T value)
            where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(path))
            {
                return BlueprintStorageOperationResult.Failure("invalidPath", "path unavailable", string.Empty);
            }

            try
            {
                if (!File.Exists(path))
                {
                    return BlueprintStorageOperationResult.Success("missing", "missing", path);
                }

                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    value = CreateSerializer(typeof(T)).ReadObject(stream) as T;
                }

                if (value == null)
                {
                    return BlueprintStorageOperationResult.Failure("empty", "empty", path);
                }

                return BlueprintStorageOperationResult.Success("loaded", "loaded", path);
            }
            catch (Exception error)
            {
                var message = error.GetType().Name + ": " + error.Message;
                LogThrottle.WarnThrottled(
                    "blueprint-json-read-failed:" + path,
                    TimeSpan.FromSeconds(30),
                    "BlueprintStorage",
                    "Blueprint JSON read failed: " + message);
                return BlueprintStorageOperationResult.Failure("readFailed", message, path);
            }
        }

        public static BlueprintStorageOperationResult TryWrite<T>(string path, T value)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return BlueprintStorageOperationResult.Failure("invalidPath", "path unavailable", string.Empty);
            }

            if (value == null)
            {
                return BlueprintStorageOperationResult.Failure("invalidValue", "value unavailable", path);
            }

            var tempPath = CreateTempPath(path);
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempDirectory = Path.GetDirectoryName(tempPath);
                if (!string.IsNullOrWhiteSpace(tempDirectory))
                {
                    Directory.CreateDirectory(tempDirectory);
                }

                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    CreateSerializer(typeof(T)).WriteObject(stream, value);
                    stream.Flush(true);
                }

                CommitPreparedFileWithRetry(tempPath, path);
                return BlueprintStorageOperationResult.Success("saved", "saved", path);
            }
            catch (Exception error)
            {
                TryDelete(tempPath);
                var message = error.GetType().Name + ": " + error.Message;
                LogThrottle.WarnThrottled(
                    "blueprint-json-write-failed:" + path,
                    TimeSpan.FromSeconds(30),
                    "BlueprintStorage",
                    "Blueprint JSON write failed: " + message);
                return BlueprintStorageOperationResult.Failure("writeFailed", message, path);
            }
        }

        internal static void SetTempPathFactoryForTesting(Func<string, string> factory)
        {
            lock (TestingSyncRoot)
            {
                _tempPathFactoryForTesting = factory;
            }
        }

        internal static void SetCommitFailurePredicateForTesting(Func<string, bool> predicate)
        {
            lock (TestingSyncRoot)
            {
                _commitFailurePredicateForTesting = predicate;
            }
        }

        internal static void ResetTestingHooks()
        {
            lock (TestingSyncRoot)
            {
                _tempPathFactoryForTesting = null;
                _commitFailurePredicateForTesting = null;
            }
        }

        private static void CommitPreparedFile(string tempPath, string targetPath)
        {
            if (ShouldFailCommit(targetPath))
            {
                throw new IOException("simulated commit failure: " + targetPath);
            }

            // Blueprint storage must fail closed: a prepared temp file is never allowed
            // to replace a good template or world-instance file unless commit succeeds.
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            try
            {
                File.Replace(tempPath, targetPath, null, true);
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

        private static void CommitPreparedFileWithRetry(string tempPath, string targetPath)
        {
            IOException lastIoError = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    CommitPreparedFile(tempPath, targetPath);
                    return;
                }
                catch (IOException error)
                {
                    lastIoError = error;
                    if (attempt >= 3)
                    {
                        throw;
                    }

                    Thread.Sleep(15 * (attempt + 1));
                }
            }

            if (lastIoError != null)
            {
                throw lastIoError;
            }
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type);
        }

        private static string CreateTempPath(string targetPath)
        {
            Func<string, string> factory;
            lock (TestingSyncRoot)
            {
                factory = _tempPathFactoryForTesting;
            }

            return factory != null
                ? factory(targetPath)
                : Path.Combine(
                    Path.GetDirectoryName(targetPath) ?? string.Empty,
                    "~jmz-blueprint-" + Guid.NewGuid().ToString("N") + ".tmp");
        }

        private static bool ShouldFailCommit(string targetPath)
        {
            Func<string, bool> predicate;
            lock (TestingSyncRoot)
            {
                predicate = _commitFailurePredicateForTesting;
            }

            return predicate != null && predicate(targetPath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
