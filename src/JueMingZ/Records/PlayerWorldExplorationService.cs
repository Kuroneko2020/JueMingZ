using System;
using System.Globalization;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Records
{
    public static class PlayerWorldExplorationService
    {
        internal const int ScanTileBudget = PlayerWorldExplorationConstants.ScanTileBudget;
        internal const long ScanCadenceTicks = PlayerWorldExplorationConstants.ScanCadenceTicks;
        internal const long FlushCadenceTicks = PlayerWorldExplorationConstants.FlushCadenceTicks;
        internal const long RescanCadenceTicks = PlayerWorldExplorationConstants.RescanCadenceTicks;

        private static readonly object SyncRoot = new object();
        private static IPlayerWorldExplorationMapReader _mapReader = PlayerWorldExplorationMapReader.Instance;
        private static string _currentPairId = string.Empty;
        private static PlayerWorldExplorationSummaryFile _currentFile;
        private static bool _dirty;
        private static long _nextScanTick;
        private static long _nextFlushTick;
        private static long _nextRescanTick;
        private static int _lastStateSignature;
        private static DateTime? _lastWriteUtc;

        public static void Tick(GameStateSnapshot snapshot, RuntimeState state)
        {
            var runtimeTick = state == null ? 0 : state.UpdateCount;
            if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
            {
                FlushAndPause("notInWorld");
                return;
            }

            lock (SyncRoot)
            {
                if (runtimeTick >= 0 && runtimeTick < _nextScanTick)
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
                FlushAndPause(identity == null ? "identityUnavailable" : identity.FailureReason);
                return;
            }

            int width;
            int height;
            string dimensionsMessage = string.Empty;
            if (_mapReader == null || !_mapReader.TryReadDimensions(out width, out height, out dimensionsMessage))
            {
                FlushAndPause("mapUnavailable:" + (dimensionsMessage ?? string.Empty));
                return;
            }

            lock (SyncRoot)
            {
                ProcessScanLocked(identity.PairId, width, height, _mapReader, runtimeTick, false);
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
            out PlayerWorldExplorationReadResult result,
            out int stateSignature)
        {
            lock (SyncRoot)
            {
                if (_currentFile == null ||
                    string.IsNullOrWhiteSpace(pairId) ||
                    !string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
                {
                    result = null;
                    stateSignature = 0;
                    return false;
                }

                result = BuildReadResultFromFile(_currentFile, "memory", "memory", true, false, false, DateTime.UtcNow, _lastWriteUtc);
                stateSignature = _lastStateSignature;
                result.DataSignature = stateSignature;
                return true;
            }
        }

        internal static PlayerWorldExplorationScanResult ProcessScanForTesting(
            string pairId,
            int width,
            int height,
            IPlayerWorldExplorationMapReader reader,
            long runtimeTick,
            bool forceFlush)
        {
            lock (SyncRoot)
            {
                return ProcessScanLocked(pairId, width, height, reader, runtimeTick, forceFlush);
            }
        }

        internal static void SetMapReaderForTesting(IPlayerWorldExplorationMapReader reader)
        {
            lock (SyncRoot)
            {
                _mapReader = reader ?? PlayerWorldExplorationMapReader.Instance;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _mapReader = PlayerWorldExplorationMapReader.Instance;
                _currentPairId = string.Empty;
                _currentFile = null;
                _dirty = false;
                _nextScanTick = 0;
                _nextFlushTick = 0;
                _nextRescanTick = 0;
                _lastStateSignature = 0;
                _lastWriteUtc = null;
            }
        }

        private static void FlushAndPause(string reason)
        {
            lock (SyncRoot)
            {
                FlushLocked(reason);
                RecordDiagnosticsLocked(BuildResult(
                    false,
                    false,
                    false,
                    _currentFile == null ? false : _currentFile.ScanComplete,
                    false,
                    false,
                    string.IsNullOrWhiteSpace(reason) ? "paused" : reason,
                    reason ?? string.Empty,
                    0,
                    0));
                _currentPairId = string.Empty;
                _currentFile = null;
                _dirty = false;
                _nextScanTick = 0;
                _nextFlushTick = 0;
                _nextRescanTick = 0;
            }
        }

        private static PlayerWorldExplorationScanResult ProcessScanLocked(
            string pairId,
            int width,
            int height,
            IPlayerWorldExplorationMapReader reader,
            long runtimeTick,
            bool forceFlush)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                var failed = BuildResult(false, false, false, false, false, false, "identityUnavailable", "pairId unavailable", 0, 0);
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (reader == null || width <= 0 || height <= 0)
            {
                var failed = BuildResult(false, true, false, false, false, false, "mapUnavailable", "world size unavailable", 0, 0);
                RecordDiagnosticsLocked(failed);
                return failed;
            }

            if (!string.Equals(_currentPairId, pairId, StringComparison.Ordinal))
            {
                FlushLocked("pairChanged");
                LoadPairLocked(pairId, width, height);
            }

            EnsureFileShape(_currentFile, pairId, width, height);
            if (_currentFile.WorldWidth != width || _currentFile.WorldHeight != height)
            {
                ResetFileLocked(pairId, width, height, "worldSizeChanged", false);
            }

            if (_currentFile.ScanComplete)
            {
                if (_nextRescanTick <= 0)
                {
                    _nextRescanTick = runtimeTick + RescanCadenceTicks;
                }

                if (!forceFlush && runtimeTick < _nextRescanTick)
                {
                    _nextScanTick = runtimeTick + ScanCadenceTicks;
                    var complete = BuildResult(true, true, false, true, false, false, "complete", "last scan complete", 0, 0);
                    RecordDiagnosticsLocked(complete);
                    return complete;
                }

                ResetFileLocked(pairId, width, height, "rescan", true);
            }

            var total = CalculateTotal(width, height);
            var tilesScanned = 0;
            var revealedThisTick = 0;
            var scanFailed = false;
            var scanMessage = string.Empty;
            var now = DateTime.UtcNow;

            while (tilesScanned < ScanTileBudget && _currentFile.NextTileIndex < total)
            {
                var index = _currentFile.NextTileIndex;
                var x = (int)(index % width);
                var y = (int)(index / width);
                bool revealed;
                string readMessage;
                if (!reader.TryIsRevealed(x, y, out revealed, out readMessage))
                {
                    scanFailed = true;
                    scanMessage = readMessage ?? string.Empty;
                    break;
                }

                _currentFile.NextTileIndex++;
                _currentFile.ScannedTileCount++;
                tilesScanned++;
                if (revealed)
                {
                    _currentFile.WorkingRevealedTileCount++;
                    revealedThisTick++;
                }
            }

            _currentFile.LastScanUtc = FormatUtc(now);
            _currentFile.LastScannedTileBudget = ScanTileBudget;
            _currentFile.TotalTileCount = total;
            ClampCounters(_currentFile);
            if (string.IsNullOrWhiteSpace(_currentFile.LastCompletedScanUtc))
            {
                _currentFile.RevealedTileCount = _currentFile.WorkingRevealedTileCount;
            }

            var completedThisTick = !scanFailed && _currentFile.NextTileIndex >= total;
            if (completedThisTick)
            {
                _currentFile.ScanComplete = true;
                _currentFile.ScannedTileCount = total;
                _currentFile.NextTileIndex = total;
                _currentFile.RevealedTileCount = _currentFile.WorkingRevealedTileCount;
                _currentFile.LastCompletedScanUtc = FormatUtc(now);
                _nextRescanTick = runtimeTick + RescanCadenceTicks;
            }

            _dirty = _dirty || tilesScanned > 0 || scanFailed || completedThisTick;
            _nextScanTick = runtimeTick + ScanCadenceTicks;
            UpdateStateSignatureLocked();

            var flushAttempted = forceFlush || completedThisTick || runtimeTick >= _nextFlushTick;
            var flushed = false;
            var writeFailed = false;
            if (flushAttempted)
            {
                flushed = FlushLocked(completedThisTick ? "scanComplete" : "cadenceFlush");
                writeFailed = !flushed && _dirty;
            }

            var status = scanFailed ? "mapReadFailed" : (completedThisTick ? "complete" : "scanning");
            var message = scanFailed ? scanMessage : (completedThisTick ? "scan complete" : "scan in progress");
            if (writeFailed && !scanFailed)
            {
                status = "writeFailed";
                message = _currentFile == null ? "write failed" : _currentFile.LastWriteMessage;
            }

            var result = BuildResult(
                !scanFailed && !writeFailed,
                true,
                completedThisTick && string.Equals(_currentFile.LastResetReason, "worldSizeChanged", StringComparison.Ordinal),
                _currentFile != null && _currentFile.ScanComplete,
                false,
                writeFailed,
                status,
                message,
                tilesScanned,
                revealedThisTick);
            RecordDiagnosticsLocked(result);
            return result;
        }

        private static void LoadPairLocked(string pairId, int width, int height)
        {
            _currentPairId = pairId ?? string.Empty;
            _currentFile = LoadFile(pairId, width, height);
            _dirty = false;
            _lastWriteUtc = TryParseUtc(_currentFile.LastUpdatedUtc);
            _nextFlushTick = 0;
            _nextRescanTick = 0;
            UpdateStateSignatureLocked();
        }

        private static PlayerWorldExplorationSummaryFile LoadFile(string pairId, int width, int height)
        {
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
            PlayerWorldExplorationSummaryFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                if (!string.IsNullOrWhiteSpace(file.PairId) &&
                    !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
                {
                    return CreateFile(pairId, width, height, "readPairMismatch");
                }

                EnsureFileShape(file, pairId, width, height);
                return file;
            }

            return CreateFile(
                pairId,
                width,
                height,
                string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase) ? "missing" : "readFailed:" + (message ?? string.Empty));
        }

        private static bool FlushLocked(string reason)
        {
            if (!_dirty || _currentFile == null || string.IsNullOrWhiteSpace(_currentPairId))
            {
                return false;
            }

            EnsureFileShape(_currentFile, _currentPairId, _currentFile.WorldWidth, _currentFile.WorldHeight);
            var now = DateTime.UtcNow;
            _currentFile.LastUpdatedUtc = FormatUtc(now);
            _currentFile.LastWriteSucceeded = true;
            _currentFile.LastWriteStatus = "saved";
            _currentFile.LastWriteMessage = string.IsNullOrWhiteSpace(reason) ? "saved" : reason;
            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(_currentPairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
            string message;
            var saved = PlayerWorldFeatureDataStore.TryWriteJson(path, _currentFile, out message);
            if (saved)
            {
                _dirty = false;
                _lastWriteUtc = now;
                _nextFlushTick = _nextScanTick + FlushCadenceTicks;
            }
            else
            {
                _currentFile.LastWriteSucceeded = false;
                _currentFile.LastWriteStatus = "failed";
                _currentFile.LastWriteMessage = message ?? string.Empty;
            }

            UpdateStateSignatureLocked();
            return saved;
        }

        private static void ResetFileLocked(string pairId, int width, int height, string reason, bool preservePublishedCount)
        {
            var previousRevealed = preservePublishedCount && _currentFile != null ? Math.Max(0L, _currentFile.RevealedTileCount) : 0L;
            var previousCompletedUtc = preservePublishedCount && _currentFile != null ? _currentFile.LastCompletedScanUtc : string.Empty;
            _currentFile = CreateFile(pairId, width, height, reason);
            _currentFile.RevealedTileCount = previousRevealed;
            _currentFile.LastCompletedScanUtc = previousCompletedUtc ?? string.Empty;
            _dirty = true;
            UpdateStateSignatureLocked();
        }

        private static PlayerWorldExplorationSummaryFile CreateFile(string pairId, int width, int height, string reason)
        {
            var total = CalculateTotal(width, height);
            return new PlayerWorldExplorationSummaryFile
            {
                PairId = pairId ?? string.Empty,
                WorldWidth = Math.Max(0, width),
                WorldHeight = Math.Max(0, height),
                TotalTileCount = total,
                RevealedTileCount = 0L,
                WorkingRevealedTileCount = 0L,
                ScannedTileCount = 0L,
                NextTileIndex = 0L,
                ScanComplete = false,
                LastResetReason = reason ?? string.Empty,
                ScanSemantics = PlayerWorldExplorationConstants.ScanSemantics
            };
        }

        private static void EnsureFileShape(PlayerWorldExplorationSummaryFile file, string pairId, int width, int height)
        {
            if (file == null)
            {
                return;
            }

            file.SchemaVersion = PlayerWorldExplorationConstants.SchemaVersion;
            file.PairId = pairId ?? string.Empty;
            if (file.WorldWidth <= 0)
            {
                file.WorldWidth = Math.Max(0, width);
            }

            if (file.WorldHeight <= 0)
            {
                file.WorldHeight = Math.Max(0, height);
            }

            file.TotalTileCount = CalculateTotal(file.WorldWidth, file.WorldHeight);
            file.LastCompletedScanUtc = file.LastCompletedScanUtc ?? string.Empty;
            file.LastScanUtc = file.LastScanUtc ?? string.Empty;
            file.LastResetReason = file.LastResetReason ?? string.Empty;
            file.LastWriteStatus = file.LastWriteStatus ?? string.Empty;
            file.LastWriteMessage = file.LastWriteMessage ?? string.Empty;
            file.LastUpdatedUtc = file.LastUpdatedUtc ?? string.Empty;
            file.ScanSemantics = PlayerWorldExplorationConstants.ScanSemantics;
            ClampCounters(file);
        }

        private static void ClampCounters(PlayerWorldExplorationSummaryFile file)
        {
            if (file == null)
            {
                return;
            }

            var total = Math.Max(0L, file.TotalTileCount);
            file.RevealedTileCount = ClampLong(file.RevealedTileCount, 0L, total);
            file.WorkingRevealedTileCount = ClampLong(file.WorkingRevealedTileCount, 0L, total);
            file.ScannedTileCount = ClampLong(file.ScannedTileCount, 0L, total);
            file.NextTileIndex = ClampLong(file.NextTileIndex, 0L, total);
            if (file.ScanComplete)
            {
                file.ScannedTileCount = total;
                file.NextTileIndex = total;
            }
        }

        private static PlayerWorldExplorationReadResult BuildReadResultFromFile(
            PlayerWorldExplorationSummaryFile file,
            string status,
            string message,
            bool identityResolved,
            bool readFailed,
            bool writeFailed,
            DateTime readUtc,
            DateTime? writeUtc)
        {
            var result = new PlayerWorldExplorationReadResult
            {
                Succeeded = !readFailed && !writeFailed,
                IdentityResolved = identityResolved,
                ReadFailed = readFailed,
                WriteFailed = writeFailed,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                PairId = file == null ? string.Empty : file.PairId,
                WorldWidth = file == null ? 0 : file.WorldWidth,
                WorldHeight = file == null ? 0 : file.WorldHeight,
                TotalTileCount = file == null ? 0L : file.TotalTileCount,
                RevealedTileCount = file == null ? 0L : file.RevealedTileCount,
                WorkingRevealedTileCount = file == null ? 0L : file.WorkingRevealedTileCount,
                ScannedTileCount = file == null ? 0L : file.ScannedTileCount,
                NextTileIndex = file == null ? 0L : file.NextTileIndex,
                LastScannedTileBudget = file == null ? 0 : file.LastScannedTileBudget,
                ScanComplete = file != null && file.ScanComplete,
                LastReadUtc = readUtc,
                LastScanUtc = file == null ? null : TryParseUtc(file.LastScanUtc),
                LastCompletedScanUtc = file == null ? null : TryParseUtc(file.LastCompletedScanUtc),
                LastWriteUtc = writeUtc
            };
            result.RevealedPercent = CalculatePercent(result.RevealedTileCount, result.TotalTileCount);
            result.DataSignature = BuildSignature(result);
            return result;
        }

        private static PlayerWorldExplorationScanResult BuildResult(
            bool succeeded,
            bool identityResolved,
            bool reset,
            bool scanComplete,
            bool readFailed,
            bool writeFailed,
            string status,
            string message,
            int tilesScanned,
            int revealedThisTick)
        {
            var result = new PlayerWorldExplorationScanResult
            {
                Succeeded = succeeded,
                IdentityResolved = identityResolved,
                Reset = reset,
                ScanComplete = scanComplete,
                ReadFailed = readFailed,
                WriteFailed = writeFailed,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                PairId = _currentPairId ?? string.Empty,
                WorldWidth = _currentFile == null ? 0 : _currentFile.WorldWidth,
                WorldHeight = _currentFile == null ? 0 : _currentFile.WorldHeight,
                TotalTileCount = _currentFile == null ? 0L : _currentFile.TotalTileCount,
                RevealedTileCount = _currentFile == null ? 0L : _currentFile.RevealedTileCount,
                WorkingRevealedTileCount = _currentFile == null ? 0L : _currentFile.WorkingRevealedTileCount,
                ScannedTileCount = _currentFile == null ? 0L : _currentFile.ScannedTileCount,
                NextTileIndex = _currentFile == null ? 0L : _currentFile.NextTileIndex,
                TilesScannedThisTick = Math.Max(0, tilesScanned),
                RevealedTilesThisTick = Math.Max(0, revealedThisTick),
                LastScanUtc = _currentFile == null ? null : TryParseUtc(_currentFile.LastScanUtc),
                LastCompletedScanUtc = _currentFile == null ? null : TryParseUtc(_currentFile.LastCompletedScanUtc),
                LastWriteUtc = _lastWriteUtc
            };
            result.RevealedPercent = CalculatePercent(result.RevealedTileCount, result.TotalTileCount);
            return result;
        }

        private static void RecordDiagnosticsLocked(PlayerWorldExplorationScanResult result)
        {
            if (result == null)
            {
                return;
            }

            PlayerWorldExplorationDiagnostics.Record(
                result.Status,
                result.Message,
                result.PairId,
                result.WorldWidth,
                result.WorldHeight,
                result.TotalTileCount,
                result.RevealedTileCount,
                result.WorkingRevealedTileCount,
                result.ScannedTileCount,
                result.NextTileIndex,
                result.TilesScannedThisTick,
                result.RevealedPercent,
                result.ScanComplete,
                result.ReadFailed,
                result.WriteFailed,
                result.LastScanUtc,
                result.LastCompletedScanUtc,
                result.LastWriteUtc);
        }

        private static void UpdateStateSignatureLocked()
        {
            var read = BuildReadResultFromFile(_currentFile, _currentFile == null ? string.Empty : (_currentFile.ScanComplete ? "complete" : "scanning"), string.Empty, _currentFile != null, false, false, DateTime.UtcNow, _lastWriteUtc);
            _lastStateSignature = read.DataSignature;
        }

        private static int BuildSignature(PlayerWorldExplorationReadResult result)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(result == null ? string.Empty : result.PairId ?? string.Empty);
                hash = hash * 31 + (result == null ? 0 : result.WorldWidth);
                hash = hash * 31 + (result == null ? 0 : result.WorldHeight);
                hash = hash * 31 + (result == null ? 0 : result.TotalTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.RevealedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.WorkingRevealedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.ScannedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.NextTileIndex.GetHashCode());
                hash = hash * 31 + (result != null && result.ScanComplete ? 1 : 0);
                hash = hash * 31 + (result != null && result.ReadFailed ? 1 : 0);
                hash = hash * 31 + (result != null && result.WriteFailed ? 1 : 0);
                return hash;
            }
        }

        private static long CalculateTotal(int width, int height)
        {
            return Math.Max(0L, (long)Math.Max(0, width) * Math.Max(0, height));
        }

        private static double CalculatePercent(long revealed, long total)
        {
            if (total <= 0L)
            {
                return 0d;
            }

            return Math.Max(0d, Math.Min(100d, revealed * 100d / total));
        }

        private static long ClampLong(long value, long min, long max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
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
