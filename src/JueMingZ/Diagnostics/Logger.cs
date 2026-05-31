using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace JueMingZ.Diagnostics
{
    public static class Logger
    {
        private const long MaxLogBytes = 5L * 1024L * 1024L;
        private const int MaxLogFiles = 10;
        private const int MaxPendingWrites = 4096;
        private static readonly object SyncRoot = new object();
        private static readonly object WriteQueueSyncRoot = new object();
        private static readonly Queue<string> PendingWrites = new Queue<string>();
        private static bool _initialized;
        private static string _currentLogFile;
        private static bool _writeWorkerScheduled;
        private static DateTime _lastWriteFailureUtc = DateTime.MinValue;

        public static LogLevel MinimumLevel { get; private set; } = LogLevel.Info;

        public static string LogDirectory { get; private set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria",
            "JueMing-Z",
            "logs");

        public static void Initialize()
        {
            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                try
                {
                    Directory.CreateDirectory(LogDirectory);
                    _currentLogFile = SelectLogFile();
                    PruneOldLogs();
                    _initialized = true;
                }
                catch
                {
                    _initialized = false;
                }
            }
        }

        public static void Configure(string levelName, bool enableTrace)
        {
            if (enableTrace)
            {
                SetMinimumLevel(LogLevel.Trace);
                return;
            }

            LogLevel parsed;
            if (!string.IsNullOrWhiteSpace(levelName) &&
                Enum.TryParse(levelName, true, out parsed))
            {
                SetMinimumLevel(parsed);
            }
        }

        public static void SetMinimumLevel(LogLevel level)
        {
            MinimumLevel = level;
        }

        public static void Trace(string source, string message)
        {
            Log(LogLevel.Trace, source, message, null);
        }

        public static void Debug(string source, string message)
        {
            Log(LogLevel.Debug, source, message, null);
        }

        public static void Info(string source, string message)
        {
            Log(LogLevel.Info, source, message, null);
        }

        public static void Warn(string source, string message)
        {
            Log(LogLevel.Warn, source, message, null);
        }

        public static void Error(string source, string message, Exception error = null)
        {
            Log(LogLevel.Error, source, message, error);
        }

        public static void Fatal(string source, string message, Exception error = null)
        {
            Log(LogLevel.Fatal, source, message, error);
        }

        public static void Log(LogLevel level, string source, string message, Exception error)
        {
            if (level < MinimumLevel)
            {
                return;
            }

            var safeSource = string.IsNullOrWhiteSpace(source) ? "Unknown" : source;
            var safeMessage = message ?? string.Empty;
            var line = FormatLine(level, safeSource, safeMessage, error);
            EnqueueWrite(line);
        }

        private static void EnqueueWrite(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            lock (WriteQueueSyncRoot)
            {
                if (PendingWrites.Count >= MaxPendingWrites)
                {
                    PendingWrites.Dequeue();
                }

                PendingWrites.Enqueue(line);
                if (_writeWorkerScheduled)
                {
                    return;
                }

                _writeWorkerScheduled = true;
                try
                {
                    ThreadPool.QueueUserWorkItem(FlushPendingWrites);
                }
                catch (Exception writeError)
                {
                    _writeWorkerScheduled = false;
                    ReportWriteFailure(writeError);
                }
            }
        }

        private static void FlushPendingWrites(object ignored)
        {
            while (true)
            {
                string[] batch;
                lock (WriteQueueSyncRoot)
                {
                    if (PendingWrites.Count == 0)
                    {
                        _writeWorkerScheduled = false;
                        return;
                    }

                    batch = PendingWrites.ToArray();
                    PendingWrites.Clear();
                }

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception writeError)
                {
                    ReportWriteFailure(writeError);
                }
            }
        }

        private static void WriteBatch(string[] batch)
        {
            if (batch == null || batch.Length == 0)
            {
                return;
            }

            EnsureInitializedNoThrow();

            lock (SyncRoot)
            {
                RotateIfNeeded();
                if (string.IsNullOrEmpty(_currentLogFile))
                {
                    return;
                }

                using (var stream = new FileStream(_currentLogFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    for (var index = 0; index < batch.Length; index++)
                    {
                        writer.Write(batch[index]);
                    }
                }
            }
        }

        private static void ReportWriteFailure(Exception writeError)
        {
            if (DateTime.UtcNow - _lastWriteFailureUtc < TimeSpan.FromSeconds(30))
            {
                return;
            }

            _lastWriteFailureUtc = DateTime.UtcNow;
            try
            {
                System.Diagnostics.Debug.WriteLine(writeError);
            }
            catch
            {
            }
        }

        private static string FormatLine(LogLevel level, string source, string message, Exception error)
        {
            var builder = new StringBuilder();
            builder
                .Append('[')
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append("][")
                .Append(level.ToString().ToUpperInvariant())
                .Append("][")
                .Append(source)
                .Append("] ")
                .Append(message);

            if (error != null)
            {
                builder.AppendLine();
                builder.Append(error);
            }

            builder.AppendLine();
            return builder.ToString();
        }

        private static void EnsureInitializedNoThrow()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Initialize();
            }
            catch
            {
            }
        }

        private static string SelectLogFile()
        {
            var datePart = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var baseFile = Path.Combine(LogDirectory, "jueming-z-" + datePart + ".log");
            if (!File.Exists(baseFile) || new FileInfo(baseFile).Length < MaxLogBytes)
            {
                return baseFile;
            }

            var timePart = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
            for (var index = 1; index < 1000; index++)
            {
                var candidate = Path.Combine(LogDirectory, "jueming-z-" + datePart + "-" + timePart + "-" + index + ".log");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(LogDirectory, "jueming-z-" + datePart + "-" + Guid.NewGuid().ToString("N") + ".log");
        }

        private static void RotateIfNeeded()
        {
            if (string.IsNullOrEmpty(_currentLogFile))
            {
                _currentLogFile = SelectLogFile();
            }

            if (File.Exists(_currentLogFile) && new FileInfo(_currentLogFile).Length >= MaxLogBytes)
            {
                _currentLogFile = SelectLogFile();
                PruneOldLogs();
            }
        }

        private static void PruneOldLogs()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    return;
                }

                var files = Directory.GetFiles(LogDirectory, "jueming-z-*.log")
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Skip(MaxLogFiles)
                    .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
