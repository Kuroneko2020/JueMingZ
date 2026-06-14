using System;
using System.Globalization;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Records
{
    public static class PlayerWorldPlaytimeService
    {
        internal const long SampleCadenceTicks = 60;
        internal const long FlushCadenceTicks = 1800;
        internal const long MaxSampleGapTicks = 600;
        internal const double MinAbnormalJumpThreshold = 7200d;

        private static readonly object SyncRoot = new object();
        private static string _currentPairId = string.Empty;
        private static PlayerWorldPlaytimeFile _currentFile;
        private static PlayerWorldClockSample _lastSample;
        private static bool _dirty;
        private static long _nextSampleTick;
        private static long _nextFlushTick;
        private static int _lastStateSignature;
        private static DateTime? _lastWriteUtc;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState state)
        {
            var runtimeTick = state == null ? 0 : state.UpdateCount;
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                FlushAndBreakSampling("notInWorld");
                return;
            }

            lock (SyncRoot)
            {
                if (runtimeTick >= 0 && runtimeTick < _nextSampleTick)
                {
                    return;
                }
            }

            PlayerWorldIdentityResolution identity;
            if (!PlayerWorldIdentityResolver.TryResolveCurrent(out identity) ||
                identity == null ||
                !identity.IsResolved ||
                string.IsNullOrWhiteSpace(identity.PairId))
            {
                FlushAndBreakSampling(identity == null ? "identityUnavailable" : identity.FailureReason);
                return;
            }

            PlayerWorldClockSample sample;
            string clockMessage;
            if (!PlayerWorldPlaytimeClockReader.TryReadCurrent(runtimeTick, out sample, out clockMessage))
            {
                FlushAndBreakSampling("clockUnavailable:" + clockMessage);
                return;
            }

            lock (SyncRoot)
            {
                ProcessSampleLocked(identity.PairId, sample, false);
            }
        }

        public static void FlushPending()
        {
            lock (SyncRoot)
            {
                FlushLocked("explicitFlush");
            }
        }

        internal static bool TryGetInMemoryForPair(
            string pairId,
            out double totalGameTicks,
            out int wholeDayCount,
            out DateTime? lastSampleUtc,
            out DateTime? lastWriteUtc,
            out int stateSignature)
        {
            lock (SyncRoot)
            {
                if (_currentFile == null ||
                    string.IsNullOrWhiteSpace(pairId) ||
                    !string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
                {
                    totalGameTicks = 0d;
                    wholeDayCount = 0;
                    lastSampleUtc = null;
                    lastWriteUtc = null;
                    stateSignature = 0;
                    return false;
                }

                totalGameTicks = Math.Max(0d, _currentFile.TotalGameTicks);
                wholeDayCount = Math.Max(0, _currentFile.WholeDayCount);
                lastSampleUtc = TryParseUtc(_currentFile.LastSampleUtc);
                lastWriteUtc = _lastWriteUtc;
                stateSignature = _lastStateSignature;
                return true;
            }
        }

        internal static PlayerWorldPlaytimeUpdateResult ProcessSampleForTesting(
            string pairId,
            PlayerWorldClockSample sample,
            bool forceFlush)
        {
            lock (SyncRoot)
            {
                return ProcessSampleLocked(pairId, sample, forceFlush);
            }
        }

        internal static bool TryCalculateDeltaForTesting(
            PlayerWorldClockSample previous,
            PlayerWorldClockSample current,
            out double deltaGameTicks,
            out string skipReason)
        {
            return TryCalculateDelta(previous, current, out deltaGameTicks, out skipReason);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _currentPairId = string.Empty;
                _currentFile = null;
                _lastSample = null;
                _dirty = false;
                _nextSampleTick = 0;
                _nextFlushTick = 0;
                _lastStateSignature = 0;
                _lastWriteUtc = null;
            }
        }

        private static void FlushAndBreakSampling(string reason)
        {
            lock (SyncRoot)
            {
                FlushLocked(reason);
                RecordDiagnosticsLocked(BuildResult(
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    string.IsNullOrWhiteSpace(reason) ? "samplingPaused" : reason,
                    reason ?? string.Empty,
                    0d,
                    _currentFile == null ? string.Empty : _currentFile.LastSkippedDeltaReason));
                _currentPairId = string.Empty;
                _currentFile = null;
                _lastSample = null;
                _dirty = false;
                _nextSampleTick = 0;
                _nextFlushTick = 0;
            }
        }

        private static PlayerWorldPlaytimeUpdateResult ProcessSampleLocked(string pairId, PlayerWorldClockSample sample, bool forceFlush)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = BuildResult(false, false, false, false, false, false, "identityUnavailable", "pairId unavailable", 0d, "identityUnavailable");
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (sample == null)
            {
                var failed = BuildResult(false, true, false, false, false, false, "clockUnavailable", "clock sample unavailable", 0d, "clockUnavailable");
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (!string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
            {
                FlushLocked("pairChanged");
                LoadPairLocked(pairId);
                _lastSample = CloneSample(sample);
                _nextSampleTick = sample.RuntimeTick + SampleCadenceTicks;
                _nextFlushTick = sample.RuntimeTick + FlushCadenceTicks;
                UpdateSampleFields(_currentFile, sample, 0d, "pairSeed");
                UpdateStateSignatureLocked();
                var seeded = BuildResult(true, true, false, false, false, false, "seeded", "pairChanged", 0d, string.Empty);
                RecordDiagnosticsLocked(seeded);
                return seeded;
            }

            double delta;
            string skipReason;
            var accepted = TryCalculateDelta(_lastSample, sample, out delta, out skipReason);
            _lastSample = CloneSample(sample);
            _nextSampleTick = sample.RuntimeTick + SampleCadenceTicks;

            if (!accepted)
            {
                if (_currentFile != null)
                {
                    _currentFile.LastSkippedDeltaReason = skipReason ?? string.Empty;
                    UpdateSampleFields(_currentFile, sample, 0d, _currentFile.LastSkippedDeltaReason);
                    _dirty = true;
                }

                UpdateStateSignatureLocked();
                var skipped = BuildResult(true, true, false, false, false, false, "deltaSkipped", skipReason, 0d, skipReason);
                RecordDiagnosticsLocked(skipped);
                if (forceFlush || sample.RuntimeTick >= _nextFlushTick)
                {
                    FlushLocked("deltaSkippedFlush");
                }

                return skipped;
            }

            if (delta <= 0d)
            {
                UpdateSampleFields(_currentFile, sample, 0d, string.Empty);
                UpdateStateSignatureLocked();
                var idle = BuildResult(true, true, false, false, false, false, "noClockProgress", "no clock progress", 0d, string.Empty);
                RecordDiagnosticsLocked(idle);
                return idle;
            }

            EnsureFileShape(_currentFile, pairId);
            _currentFile.TotalGameTicks = Math.Max(0d, _currentFile.TotalGameTicks) + delta;
            _currentFile.WholeDayCount = CalculateWholeDays(_currentFile.TotalGameTicks);
            _currentFile.LastSkippedDeltaReason = string.Empty;
            UpdateSampleFields(_currentFile, sample, delta, string.Empty);
            _dirty = true;
            UpdateStateSignatureLocked();

            var flushAttempted = forceFlush || sample.RuntimeTick >= _nextFlushTick;
            var flushed = false;
            var writeFailed = false;
            if (flushAttempted)
            {
                flushed = FlushLocked("cadenceFlush");
                writeFailed = !flushed && _dirty;
            }

            var result = BuildResult(
                !writeFailed,
                true,
                true,
                flushed,
                false,
                writeFailed,
                writeFailed ? "writeFailed" : (flushed ? "saved" : "accumulated"),
                writeFailed ? (_currentFile == null ? "write failed" : _currentFile.LastWriteMessage) : (flushed ? "saved" : "pending flush"),
                delta,
                string.Empty);
            RecordDiagnosticsLocked(result);
            return result;
        }

        private static void LoadPairLocked(string pairId)
        {
            _currentPairId = pairId ?? string.Empty;
            _currentFile = LoadFile(pairId);
            EnsureFileShape(_currentFile, pairId);
            _dirty = false;
            _lastWriteUtc = TryParseUtc(_currentFile.LastUpdatedUtc);
            UpdateStateSignatureLocked();
        }

        private static PlayerWorldPlaytimeFile LoadFile(string pairId)
        {
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
            PlayerWorldPlaytimeFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                if (!string.IsNullOrWhiteSpace(file.PairId) &&
                    !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
                {
                    var fresh = CreateFile(pairId);
                    fresh.LastSkippedDeltaReason = "readPairMismatch";
                    return fresh;
                }

                EnsureFileShape(file, pairId);
                return file;
            }

            var created = CreateFile(pairId);
            if (!string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase))
            {
                created.LastSkippedDeltaReason = "readFailed:" + (message ?? string.Empty);
            }

            return created;
        }

        private static bool FlushLocked(string reason)
        {
            if (!_dirty || _currentFile == null || string.IsNullOrWhiteSpace(_currentPairId))
            {
                return false;
            }

            EnsureFileShape(_currentFile, _currentPairId);
            var now = DateTime.UtcNow;
            _currentFile.LastUpdatedUtc = FormatUtc(now);
            _currentFile.LastWriteSucceeded = true;
            _currentFile.LastWriteStatus = "saved";
            _currentFile.LastWriteMessage = string.IsNullOrWhiteSpace(reason) ? "saved" : reason;
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(_currentPairId, PlayerWorldFeatureDataRoot.PlaytimeFileName);
            string message;
            var saved = PlayerWorldFeatureDataStore.TryWriteJson(path, _currentFile, out message);
            if (saved)
            {
                _dirty = false;
                _currentFile.LastWriteMessage = message ?? string.Empty;
                _lastWriteUtc = now;
                _nextFlushTick = (_lastSample == null ? 0 : _lastSample.RuntimeTick) + FlushCadenceTicks;
            }
            else
            {
                _currentFile.LastWriteSucceeded = false;
                _currentFile.LastWriteStatus = "failed";
                _currentFile.LastWriteMessage = message ?? string.Empty;
            }

            UpdateStateSignatureLocked();
            var result = BuildResult(
                saved,
                true,
                false,
                saved,
                false,
                !saved,
                saved ? "saved" : "writeFailed",
                string.IsNullOrWhiteSpace(reason) ? message : reason + ";" + message,
                0d,
                _currentFile.LastSkippedDeltaReason);
            RecordDiagnosticsLocked(result);
            return saved;
        }

        private static bool TryCalculateDelta(
            PlayerWorldClockSample previous,
            PlayerWorldClockSample current,
            out double deltaGameTicks,
            out string skipReason)
        {
            deltaGameTicks = 0d;
            skipReason = string.Empty;
            if (previous == null || current == null)
            {
                skipReason = "sampleUnavailable";
                return false;
            }

            if (current.RuntimeTick <= previous.RuntimeTick)
            {
                skipReason = "runtimeTickBackwards";
                return false;
            }

            var tickGap = current.RuntimeTick - previous.RuntimeTick;
            if (tickGap > MaxSampleGapTicks)
            {
                skipReason = "sampleGapTooLarge";
                return false;
            }

            var previousPosition = previous.CyclePosition;
            var currentPosition = current.CyclePosition;
            if (!IsValidCyclePosition(previousPosition) || !IsValidCyclePosition(currentPosition))
            {
                skipReason = "cyclePositionInvalid";
                return false;
            }

            if (currentPosition >= previousPosition)
            {
                deltaGameTicks = currentPosition - previousPosition;
            }
            else if (previousPosition >= PlayerWorldPlaytimeConstants.DayLengthTicks &&
                     currentPosition < PlayerWorldPlaytimeConstants.DayLengthTicks)
            {
                deltaGameTicks = currentPosition + PlayerWorldPlaytimeConstants.FullDayTicks - previousPosition;
            }
            else
            {
                skipReason = "timeBackwards";
                return false;
            }

            if (deltaGameTicks <= 0d)
            {
                return true;
            }

            var maxExpected = CalculateMaxAcceptedDelta(previous, current, tickGap);
            if (deltaGameTicks > maxExpected)
            {
                deltaGameTicks = 0d;
                skipReason = "abnormalJump";
                return false;
            }

            return true;
        }

        private static double CalculateMaxAcceptedDelta(PlayerWorldClockSample previous, PlayerWorldClockSample current, long tickGap)
        {
            var rate = Math.Max(1d, Math.Max(previous == null ? 1d : previous.DayRate, current == null ? 1d : current.DayRate));
            var expected = Math.Max(MinAbnormalJumpThreshold, tickGap * rate * 3d);
            return Math.Min(PlayerWorldPlaytimeConstants.FullDayTicks / 2d, expected);
        }

        private static bool IsValidCyclePosition(double position)
        {
            return !double.IsNaN(position) &&
                   !double.IsInfinity(position) &&
                   position >= 0d &&
                   position <= PlayerWorldPlaytimeConstants.FullDayTicks + 1d;
        }

        private static void EnsureFileShape(PlayerWorldPlaytimeFile file, string pairId)
        {
            if (file == null)
            {
                return;
            }

            file.SchemaVersion = PlayerWorldPlaytimeConstants.SchemaVersion;
            file.PairId = pairId ?? string.Empty;
            file.TotalGameTicks = Math.Max(0d, file.TotalGameTicks);
            file.WholeDayCount = CalculateWholeDays(file.TotalGameTicks);
            file.TimeSemantics = PlayerWorldPlaytimeConstants.TimeSemantics;
            file.LastSampleUtc = file.LastSampleUtc ?? string.Empty;
            file.LastSkippedDeltaReason = file.LastSkippedDeltaReason ?? string.Empty;
            file.LastWriteStatus = file.LastWriteStatus ?? string.Empty;
            file.LastWriteMessage = file.LastWriteMessage ?? string.Empty;
            file.LastUpdatedUtc = file.LastUpdatedUtc ?? string.Empty;
        }

        private static PlayerWorldPlaytimeFile CreateFile(string pairId)
        {
            return new PlayerWorldPlaytimeFile
            {
                PairId = pairId ?? string.Empty,
                TotalGameTicks = 0d,
                WholeDayCount = 0,
                TimeSemantics = PlayerWorldPlaytimeConstants.TimeSemantics
            };
        }

        private static void UpdateSampleFields(PlayerWorldPlaytimeFile file, PlayerWorldClockSample sample, double delta, string skipReason)
        {
            if (file == null || sample == null)
            {
                return;
            }

            file.LastSampleUtc = FormatUtc(sample.SampleUtc);
            file.LastSampleDayTime = sample.DayTime;
            file.LastSampleWorldTime = sample.WorldTime;
            file.LastSampleDayRate = sample.DayRate;
            file.LastSampleCyclePosition = sample.CyclePosition;
            file.LastSampleRuntimeTick = sample.RuntimeTick;
            file.LastAcceptedDeltaGameTicks = Math.Max(0d, delta);
            if (!string.IsNullOrWhiteSpace(skipReason) && !string.Equals(skipReason, "pairSeed", StringComparison.Ordinal))
            {
                file.LastSkippedDeltaReason = skipReason;
            }
        }

        private static PlayerWorldPlaytimeUpdateResult BuildResult(
            bool succeeded,
            bool identityResolved,
            bool accumulated,
            bool flushed,
            bool readFailed,
            bool writeFailed,
            string status,
            string message,
            double delta,
            string skippedReason)
        {
            return new PlayerWorldPlaytimeUpdateResult
            {
                Succeeded = succeeded,
                IdentityResolved = identityResolved,
                Accumulated = accumulated,
                Flushed = flushed,
                ReadFailed = readFailed,
                WriteFailed = writeFailed,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                PairId = _currentPairId ?? string.Empty,
                TotalGameTicks = _currentFile == null ? 0d : Math.Max(0d, _currentFile.TotalGameTicks),
                WholeDayCount = _currentFile == null ? 0 : Math.Max(0, _currentFile.WholeDayCount),
                DeltaGameTicks = Math.Max(0d, delta),
                LastSkippedDeltaReason = skippedReason ?? string.Empty,
                ClockText = _lastSample == null ? string.Empty : _lastSample.ClockText,
                LastSampleUtc = _lastSample == null ? (DateTime?)null : _lastSample.SampleUtc,
                LastWriteUtc = _lastWriteUtc
            };
        }

        private static void RecordDiagnosticsLocked(PlayerWorldPlaytimeUpdateResult result)
        {
            if (result == null)
            {
                return;
            }

            PlayerWorldPlaytimeDiagnostics.Record(
                result.Status,
                result.Message,
                result.PairId,
                result.TotalGameTicks,
                result.WholeDayCount,
                result.ReadFailed,
                result.WriteFailed,
                result.DeltaGameTicks,
                result.LastSkippedDeltaReason,
                result.LastSampleUtc,
                result.LastWriteUtc);
        }

        private static void UpdateStateSignatureLocked()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(_currentPairId ?? string.Empty);
                hash = hash * 31 + (_currentFile == null ? 0 : _currentFile.TotalGameTicks.GetHashCode());
                hash = hash * 31 + (_currentFile == null ? 0 : _currentFile.WholeDayCount);
                hash = hash * 31 + (_currentFile == null ? 0 : StringComparer.Ordinal.GetHashCode(_currentFile.LastSkippedDeltaReason ?? string.Empty));
                hash = hash * 31 + (_dirty ? 1 : 0);
                _lastStateSignature = hash;
            }
        }

        private static PlayerWorldClockSample CloneSample(PlayerWorldClockSample sample)
        {
            if (sample == null)
            {
                return null;
            }

            return new PlayerWorldClockSample
            {
                DayTime = sample.DayTime,
                WorldTime = sample.WorldTime,
                DayRate = sample.DayRate,
                RuntimeTick = sample.RuntimeTick,
                SampleUtc = sample.SampleUtc
            };
        }

        private static int CalculateWholeDays(double totalGameTicks)
        {
            return (int)Math.Floor(Math.Max(0d, totalGameTicks) / PlayerWorldPlaytimeConstants.FullDayTicks);
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        private static DateTime? TryParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(
                    value ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }
    }
}
