using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JueMingZ.Diagnostics
{
    public static partial class DiagnosticSnapshotWriter
    {
        private static readonly object SyncRoot = new object();
        private static readonly object WriteIoSyncRoot = new object();
        private static DateTime _lastWriteUtc = DateTime.MinValue;

        public static string DiagnosticsDirectory { get; private set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria",
            "JueMing-Z",
            "diagnostics");

        public static string RuntimeSnapshotPath { get; private set; } = Path.Combine(DiagnosticsDirectory, "runtime-snapshot.json");

        public static bool ShouldWriteNow()
        {
            // runtime-snapshot.json is a low-frequency overwrite, not an append log or per-tick event stream.
            lock (SyncRoot)
            {
                return DateTime.UtcNow - _lastWriteUtc >= TimeSpan.FromSeconds(5);
            }
        }

        public static void WriteThrottled(DiagnosticSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                var now = DateTime.UtcNow;
                if (now - _lastWriteUtc < TimeSpan.FromSeconds(5))
                {
                    return;
                }

                _lastWriteUtc = now;
            }

            try
            {
                ThreadPool.QueueUserWorkItem(WriteSnapshotOnBackground, snapshot);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "diagnostic-snapshot-write-failed",
                    TimeSpan.FromSeconds(30),
                    "DiagnosticSnapshotWriter",
                    "Diagnostic snapshot write failed: " + error.Message);
            }
        }

        private static void WriteSnapshotOnBackground(object state)
        {
            var snapshot = state as DiagnosticSnapshot;
            if (snapshot == null)
            {
                return;
            }

            try
            {
                lock (WriteIoSyncRoot)
                {
                    Directory.CreateDirectory(DiagnosticsDirectory);
                    var tempPath = RuntimeSnapshotPath + ".tmp-" + Guid.NewGuid().ToString("N");
                    File.WriteAllText(tempPath, ToJson(snapshot), Encoding.UTF8);
                    File.Copy(tempPath, RuntimeSnapshotPath, true);
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }

                LogThrottle.InfoThrottled(
                    "diagnostic-snapshot-written",
                    TimeSpan.FromSeconds(10),
                    "DiagnosticSnapshotWriter",
                    "diagnostic snapshot written: " + RuntimeSnapshotPath);
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "diagnostic-snapshot-write-failed",
                    TimeSpan.FromSeconds(30),
                    "DiagnosticSnapshotWriter",
                    "Diagnostic snapshot async write failed: " + error.Message);
            }
        }
    }
}
