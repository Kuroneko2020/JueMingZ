using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    public static class PlayerWorldDeathRecorder
    {
        private static readonly object SyncRoot = new object();
        private static PlayerWorldDeathRecordDiagnostics _lastDiagnostics = new PlayerWorldDeathRecordDiagnostics();

        public static PlayerWorldDeathRecordDiagnostics LastDiagnostics
        {
            get
            {
                lock (SyncRoot)
                {
                    return CloneDiagnostics(_lastDiagnostics);
                }
            }
        }

        public static bool TryRecordCurrentDeathFromHook(
            object player,
            object damageSource,
            double damage,
            int hitDirection,
            bool pvp,
            out PlayerWorldDeathRecordResult result)
        {
            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityResolver.TryResolveCurrent(out identity))
            {
                result = Failure("identityUnavailable", identity == null ? "identity unavailable" : identity.FailureReason);
                RecordDiagnostics(result);
                return false;
            }

            PlayerWorldDeathEvent deathEvent;
            string buildMessage;
            if (!PlayerWorldDeathEventBuilder.TryBuildFromHook(
                    identity,
                    player,
                    damageSource,
                    damage,
                    hitDirection,
                    pvp,
                    DateTime.UtcNow,
                    out deathEvent,
                    out buildMessage))
            {
                result = Failure("eventBuildFailed", buildMessage);
                result.PairId = identity.PairId;
                RecordDiagnostics(result);
                return false;
            }

            return TryRecordResolvedDeath(identity, deathEvent, true, out result);
        }

        internal static bool TryRecordDeathForTesting(
            PlayerWorldIdentityResolution identity,
            PlayerWorldDeathEvent deathEvent,
            bool persistentDeathMarkersEnabled,
            out PlayerWorldDeathRecordResult result)
        {
            return TryRecordResolvedDeath(identity, deathEvent, persistentDeathMarkersEnabled, out result);
        }

        internal static string SerializeEventForTesting(PlayerWorldDeathEvent deathEvent)
        {
            return SerializeEvent(deathEvent);
        }

        internal static int CountDeathEventLinesForTesting(string path)
        {
            int count;
            string message;
            return TryCountReadableEventLines(path, out count, out message) ? count : -1;
        }

        private static bool TryRecordResolvedDeath(
            PlayerWorldIdentityResolution identity,
            PlayerWorldDeathEvent deathEvent,
            bool persistentDeathMarkersEnabled,
            out PlayerWorldDeathRecordResult result)
        {
            if (identity == null || !identity.IsResolved || string.IsNullOrWhiteSpace(identity.PairId))
            {
                result = Failure("identityUnavailable", identity == null ? "identity unavailable" : identity.FailureReason);
                RecordDiagnostics(result);
                return false;
            }

            if (deathEvent == null)
            {
                result = Failure("eventUnavailable", "death event unavailable");
                result.PairId = identity.PairId;
                RecordDiagnostics(result);
                return false;
            }

            if (string.IsNullOrWhiteSpace(deathEvent.IdentityPairId))
            {
                deathEvent.IdentityPairId = identity.PairId;
            }

            if (!string.Equals(deathEvent.IdentityPairId, identity.PairId, StringComparison.Ordinal))
            {
                result = Failure("pairMismatch", "death event pair id does not match resolved identity");
                result.PairId = identity.PairId;
                result.EventId = deathEvent.EventId;
                RecordDiagnostics(result);
                return false;
            }

            lock (SyncRoot)
            {
                var deathsPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathEventsFileName);
                var summaryPath = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(identity.PairId, PlayerWorldFeatureDataRoot.DeathSummaryFileName);

                string appendMessage;
                var eventWritten = TryAppendEventLine(deathsPath, deathEvent, out appendMessage);

                int readableCount;
                string historyReadMessage;
                var historyReadable = TryCountReadableEventLines(deathsPath, out readableCount, out historyReadMessage);

                bool summaryReadFailed;
                string summaryReadMessage;
                var summary = LoadSummary(summaryPath, out summaryReadFailed, out summaryReadMessage);
                UpdateSummary(summary, identity.PairId, deathEvent, eventWritten, appendMessage, historyReadable, readableCount, historyReadMessage, summaryReadFailed, summaryReadMessage);

                string summaryWriteMessage;
                var summaryWritten = PlayerWorldFeatureDataStore.TryWriteJson(summaryPath, summary, out summaryWriteMessage);

                result = new PlayerWorldDeathRecordResult
                {
                    Succeeded = eventWritten && summaryWritten,
                    EventWritten = eventWritten,
                    SummaryWritten = summaryWritten,
                    Status = eventWritten && summaryWritten ? "saved" : (eventWritten ? "summarySaveFailed" : "eventAppendFailed"),
                    Message = "event=" + appendMessage + ";summary=" + summaryWriteMessage + ";markerDisplay=" + (persistentDeathMarkersEnabled ? "enabled" : "disabled"),
                    PairId = identity.PairId,
                    EventId = deathEvent.EventId,
                    DeathCount = summary.DeathCount,
                    DeathHistoryReadFailed = !historyReadable
                };

                if (!eventWritten)
                {
                    LogThrottle.WarnThrottled(
                        "player-world-death-event-append-failed:" + identity.PairId,
                        TimeSpan.FromSeconds(30),
                        "PlayerWorldDeathRecorder",
                        "Player-world death event append failed: " + appendMessage);
                }

                if (!summaryWritten)
                {
                    LogThrottle.WarnThrottled(
                        "player-world-death-summary-save-failed:" + identity.PairId,
                        TimeSpan.FromSeconds(30),
                        "PlayerWorldDeathRecorder",
                        "Player-world death summary save failed: " + summaryWriteMessage);
                }

                RecordDiagnostics(result);
                return result.Succeeded;
            }
        }

        private static bool TryAppendEventLine(string path, PlayerWorldDeathEvent deathEvent, out string message)
        {
            message = string.Empty;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var line = SerializeEvent(deathEvent) + Environment.NewLine;
                var bytes = Encoding.UTF8.GetBytes(line);
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                message = "saved";
                return true;
            }
            catch (Exception error)
            {
                message = error.GetType().Name + ": " + error.Message;
                return false;
            }
        }

        private static PlayerWorldDeathSummaryFile LoadSummary(string path, out bool readFailed, out string message)
        {
            readFailed = false;
            message = string.Empty;
            PlayerWorldDeathSummaryFile summary;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out summary, out message))
            {
                return summary;
            }

            readFailed = !string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase);
            return new PlayerWorldDeathSummaryFile();
        }

        private static void UpdateSummary(
            PlayerWorldDeathSummaryFile summary,
            string pairId,
            PlayerWorldDeathEvent deathEvent,
            bool eventWritten,
            string appendMessage,
            bool historyReadable,
            int readableCount,
            string historyReadMessage,
            bool summaryReadFailed,
            string summaryReadMessage)
        {
            if (summary == null)
            {
                summary = new PlayerWorldDeathSummaryFile();
            }

            summary.SchemaVersion = 1;
            summary.PairId = pairId ?? string.Empty;
            if (historyReadable)
            {
                summary.DeathCount = readableCount;
            }
            else if (eventWritten)
            {
                summary.DeathCount = Math.Max(0, summary.DeathCount) + 1;
            }

            if (eventWritten && deathEvent != null)
            {
                summary.LastEventId = deathEvent.EventId ?? string.Empty;
                summary.LastDeathUtc = deathEvent.RealTimeUtc ?? string.Empty;
                summary.LastDeathLocalText = deathEvent.RealTimeLocalText ?? string.Empty;
            }

            summary.LastWriteSucceeded = eventWritten;
            summary.LastWriteStatus = eventWritten ? "saved" : "appendFailed";
            summary.LastWriteMessage = appendMessage ?? string.Empty;
            summary.DeathHistoryReadFailed = !historyReadable;
            summary.DeathHistoryReadMessage = historyReadMessage ?? string.Empty;
            summary.DeathSummaryReadFailed = summaryReadFailed;
            summary.DeathSummaryReadMessage = summaryReadMessage ?? string.Empty;
            summary.LastUpdatedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool TryCountReadableEventLines(string path, out int count, out string message)
        {
            count = 0;
            message = string.Empty;
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    message = "missing";
                    return true;
                }

                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    var lineNumber = 0;
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();
                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (!TryDeserializeEventLine(line))
                        {
                            message = "invalidLine:" + lineNumber;
                            return false;
                        }

                        count++;
                    }
                }

                message = "loaded";
                return true;
            }
            catch (Exception error)
            {
                message = error.GetType().Name + ": " + error.Message;
                return false;
            }
        }

        private static bool TryDeserializeEventLine(string line)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(line ?? string.Empty);
                using (var stream = new MemoryStream(bytes))
                {
                    var serializer = new DataContractJsonSerializer(typeof(PlayerWorldDeathEvent));
                    return serializer.ReadObject(stream) is PlayerWorldDeathEvent;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string SerializeEvent(PlayerWorldDeathEvent deathEvent)
        {
            if (deathEvent == null)
            {
                return "{}";
            }

            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(PlayerWorldDeathEvent));
                serializer.WriteObject(stream, deathEvent);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static PlayerWorldDeathRecordResult Failure(string status, string message)
        {
            return new PlayerWorldDeathRecordResult
            {
                Succeeded = false,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty
            };
        }

        private static void RecordDiagnostics(PlayerWorldDeathRecordResult result)
        {
            if (result == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _lastDiagnostics = new PlayerWorldDeathRecordDiagnostics
                {
                    LastRecordStatus = result.Status,
                    LastRecordMessage = result.Message,
                    LastEventId = result.EventId,
                    LastPairId = result.PairId,
                    LastDeathCount = result.DeathCount,
                    DeathHistoryReadFailed = result.DeathHistoryReadFailed
                };
            }
        }

        private static PlayerWorldDeathRecordDiagnostics CloneDiagnostics(PlayerWorldDeathRecordDiagnostics source)
        {
            if (source == null)
            {
                return new PlayerWorldDeathRecordDiagnostics();
            }

            return new PlayerWorldDeathRecordDiagnostics
            {
                LastRecordStatus = source.LastRecordStatus,
                LastRecordMessage = source.LastRecordMessage,
                LastEventId = source.LastEventId,
                LastPairId = source.LastPairId,
                LastDeathCount = source.LastDeathCount,
                DeathHistoryReadFailed = source.DeathHistoryReadFailed
            };
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastDiagnostics = new PlayerWorldDeathRecordDiagnostics();
            }
        }
    }
}
