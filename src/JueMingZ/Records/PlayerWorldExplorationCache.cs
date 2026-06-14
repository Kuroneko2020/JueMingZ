using System;
using System.IO;
using JueMingZ.Diagnostics;

namespace JueMingZ.Records
{
    internal static class PlayerWorldExplorationCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(500);
        private static string _cachedPairId = string.Empty;
        private static string _cachedPath = string.Empty;
        private static long _cachedLength = -1L;
        private static DateTime _cachedWriteUtc = DateTime.MinValue;
        private static DateTime _nextRefreshUtc = DateTime.MinValue;
        private static PlayerWorldExplorationReadResult _cachedResult;
        private static int _lastStateSignature;

        public static int LastStateSignature
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastStateSignature;
                }
            }
        }

        public static PlayerWorldExplorationReadResult ReadCurrent()
        {
            lock (SyncRoot)
            {
                PlayerWorldIdentityResolution identity;
                if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out identity) ||
                    identity == null ||
                    !identity.IsResolved ||
                    string.IsNullOrWhiteSpace(identity.PairId))
                {
                    var failed = BuildIdentityFailure(identity);
                    RecordDiagnosticsLocked(failed);
                    return CloneResult(failed, failed.Status);
                }

                var result = ReadForPairLocked(identity.PairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        internal static PlayerWorldExplorationReadResult ReadForPairForTesting(string pairId)
        {
            lock (SyncRoot)
            {
                var result = ReadForPairLocked(pairId, DateTime.UtcNow);
                RecordDiagnosticsLocked(result);
                return CloneResult(result, result.Status);
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedPairId = string.Empty;
                _cachedPath = string.Empty;
                _cachedLength = -1L;
                _cachedWriteUtc = DateTime.MinValue;
                _nextRefreshUtc = DateTime.MinValue;
                _cachedResult = null;
                _lastStateSignature = 0;
            }
        }

        private static PlayerWorldExplorationReadResult ReadForPairLocked(string pairId, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(pairId))
            {
                return BuildIdentityFailure(null);
            }

            PlayerWorldExplorationReadResult memory;
            int memorySignature;
            if (PlayerWorldExplorationService.TryGetInMemoryForPair(pairId, out memory, out memorySignature) && memory != null)
            {
                memory.DataSignature = memorySignature;
                return memory;
            }

            var path = PlayerWorldFeatureDataRoot.BuildPlayerWorldFeatureFilePath(pairId, PlayerWorldFeatureDataRoot.ExplorationSummaryFileName);
            FileInfo info = null;
            if (File.Exists(path))
            {
                info = new FileInfo(path);
            }

            var length = info == null ? -1L : info.Length;
            var writeUtc = info == null ? DateTime.MinValue : info.LastWriteTimeUtc;
            if (_cachedResult != null &&
                now < _nextRefreshUtc &&
                string.Equals(_cachedPairId, pairId, StringComparison.Ordinal) &&
                string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                _cachedLength == length &&
                _cachedWriteUtc == writeUtc)
            {
                return CloneResult(_cachedResult, "cached");
            }

            var result = Load(pairId, path, length, writeUtc, now);
            _cachedPairId = pairId;
            _cachedPath = path;
            _cachedLength = length;
            _cachedWriteUtc = writeUtc;
            _cachedResult = CloneResult(result, result.Status);
            _nextRefreshUtc = now + RefreshInterval;
            return result;
        }

        private static PlayerWorldExplorationReadResult Load(string pairId, string path, long length, DateTime writeUtc, DateTime now)
        {
            var result = new PlayerWorldExplorationReadResult
            {
                Succeeded = true,
                IdentityResolved = true,
                Status = "loaded",
                Message = "loaded",
                PairId = pairId ?? string.Empty,
                LastReadUtc = now,
                LastWriteUtc = writeUtc == DateTime.MinValue ? (DateTime?)null : writeUtc
            };
            var control = PlayerWorldExplorationService.GetControlSnapshot();
            if (control != null)
            {
                result.ScanMode = control.ScanMode;
                result.ControlState = control.ControlState;
                result.ScanTileCap = control.ScanTileCap;
                result.TimeBudgetMs = control.TimeBudgetMs;
                result.CurrentCadenceTicks = control.CurrentCadenceTicks;
                result.BackoffApplied = control.BackoffApplied;
                result.LastScanElapsedMs = control.LastScanElapsedMs;
                result.LastScanTileCount = control.LastScanTileCount;
                result.LastUserCommand = control.LastUserCommand;
                result.AutoRescanDisabled = control.AutoRescanDisabled;
            }

            PlayerWorldExplorationSummaryFile file;
            string message;
            if (PlayerWorldFeatureDataStore.TryReadJson(path, out file, out message) && file != null)
            {
                if (!string.IsNullOrWhiteSpace(file.PairId) &&
                    !string.Equals(file.PairId, pairId, StringComparison.Ordinal))
                {
                    result.Succeeded = false;
                    result.ReadFailed = true;
                    result.Status = "readFailed";
                    result.Message = "pairMismatch";
                }
                else
                {
                    FillFromFile(result, file);
                    result.LastWriteUtc = TryParseUtc(file.LastUpdatedUtc) ?? result.LastWriteUtc;
                }
            }
            else if (string.Equals(message, "missing", StringComparison.OrdinalIgnoreCase))
            {
                result.Status = "missing";
                result.Message = "missing";
            }
            else
            {
                result.Succeeded = false;
                result.ReadFailed = true;
                result.Status = "readFailed";
                result.Message = message ?? string.Empty;
            }

            result.DataSignature = BuildSignature(result, length, writeUtc);
            return result;
        }

        private static void FillFromFile(PlayerWorldExplorationReadResult result, PlayerWorldExplorationSummaryFile file)
        {
            if (result == null || file == null)
            {
                return;
            }

            var total = Math.Max(0L, file.TotalTileCount);
            result.WorldWidth = Math.Max(0, file.WorldWidth);
            result.WorldHeight = Math.Max(0, file.WorldHeight);
            result.TotalTileCount = total;
            result.RevealedTileCount = ClampLong(file.RevealedTileCount, 0L, total);
            result.WorkingRevealedTileCount = ClampLong(file.WorkingRevealedTileCount, 0L, total);
            result.ScannedTileCount = ClampLong(file.ScannedTileCount, 0L, total);
            result.NextTileIndex = ClampLong(file.NextTileIndex, 0L, total);
            result.LastScannedTileBudget = Math.Max(0, file.LastScannedTileBudget);
            result.ScanComplete = file.ScanComplete;
            result.LastScanUtc = TryParseUtc(file.LastScanUtc);
            result.LastCompletedScanUtc = TryParseUtc(file.LastCompletedScanUtc);
            result.RevealedPercent = CalculatePercent(result.RevealedTileCount, total);
        }

        private static PlayerWorldExplorationReadResult BuildIdentityFailure(PlayerWorldIdentityResolution identity)
        {
            return new PlayerWorldExplorationReadResult
            {
                Succeeded = false,
                IdentityResolved = false,
                Status = "identityUnavailable",
                Message = identity == null ? "identity unavailable" : identity.FailureReason,
                LastReadUtc = DateTime.UtcNow
            };
        }

        private static PlayerWorldExplorationReadResult CloneResult(PlayerWorldExplorationReadResult source, string status)
        {
            var clone = new PlayerWorldExplorationReadResult();
            if (source == null)
            {
                clone.Status = status ?? string.Empty;
                return clone;
            }

            clone.Succeeded = source.Succeeded;
            clone.IdentityResolved = source.IdentityResolved;
            clone.ReadFailed = source.ReadFailed;
            clone.WriteFailed = source.WriteFailed;
            clone.Status = string.IsNullOrWhiteSpace(status) ? source.Status : status;
            clone.Message = source.Message ?? string.Empty;
            clone.PairId = source.PairId ?? string.Empty;
            clone.WorldWidth = source.WorldWidth;
            clone.WorldHeight = source.WorldHeight;
            clone.TotalTileCount = source.TotalTileCount;
            clone.RevealedTileCount = source.RevealedTileCount;
            clone.WorkingRevealedTileCount = source.WorkingRevealedTileCount;
            clone.ScannedTileCount = source.ScannedTileCount;
            clone.NextTileIndex = source.NextTileIndex;
            clone.LastScannedTileBudget = source.LastScannedTileBudget;
            clone.ScanMode = source.ScanMode ?? string.Empty;
            clone.ControlState = source.ControlState ?? string.Empty;
            clone.ScanTileCap = source.ScanTileCap;
            clone.TimeBudgetMs = source.TimeBudgetMs;
            clone.CurrentCadenceTicks = source.CurrentCadenceTicks;
            clone.BackoffApplied = source.BackoffApplied;
            clone.LastScanElapsedMs = source.LastScanElapsedMs;
            clone.LastScanTileCount = source.LastScanTileCount;
            clone.LastUserCommand = source.LastUserCommand ?? string.Empty;
            clone.AutoRescanDisabled = source.AutoRescanDisabled;
            clone.ScanComplete = source.ScanComplete;
            clone.RevealedPercent = source.RevealedPercent;
            clone.DataSignature = source.DataSignature;
            clone.LastReadUtc = source.LastReadUtc;
            clone.LastScanUtc = source.LastScanUtc;
            clone.LastCompletedScanUtc = source.LastCompletedScanUtc;
            clone.LastWriteUtc = source.LastWriteUtc;
            return clone;
        }

        private static int BuildSignature(PlayerWorldExplorationReadResult result, long length, DateTime writeUtc)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(result == null ? string.Empty : result.PairId ?? string.Empty);
                hash = hash * 31 + length.GetHashCode();
                hash = hash * 31 + writeUtc.Ticks.GetHashCode();
                hash = hash * 31 + (result == null ? 0 : result.WorldWidth);
                hash = hash * 31 + (result == null ? 0 : result.WorldHeight);
                hash = hash * 31 + (result == null ? 0 : result.TotalTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.RevealedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.WorkingRevealedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.ScannedTileCount.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.NextTileIndex.GetHashCode());
                hash = hash * 31 + (result != null && result.ScanComplete ? 1 : 0);
                hash = hash * 31 + (result != null && result.ReadFailed ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(result == null ? string.Empty : result.ScanMode ?? string.Empty);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(result == null ? string.Empty : result.ControlState ?? string.Empty);
                hash = hash * 31 + (result == null ? 0 : result.ScanTileCap);
                hash = hash * 31 + (result == null ? 0 : result.TimeBudgetMs.GetHashCode());
                hash = hash * 31 + (result == null ? 0 : result.CurrentCadenceTicks.GetHashCode());
                hash = hash * 31 + (result != null && result.BackoffApplied ? 1 : 0);
                hash = hash * 31 + StringComparer.Ordinal.GetHashCode(result == null ? string.Empty : result.LastUserCommand ?? string.Empty);
                return hash;
            }
        }

        private static void RecordDiagnosticsLocked(PlayerWorldExplorationReadResult result)
        {
            if (result == null)
            {
                return;
            }

            _lastStateSignature = result.DataSignature;
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
                result.LastScannedTileBudget,
                result.ScanMode,
                result.ControlState,
                string.Equals(result.ControlState, PlayerWorldExplorationControlStates.PausedByUser, StringComparison.Ordinal),
                string.Equals(result.ControlState, PlayerWorldExplorationControlStates.IdleComplete, StringComparison.Ordinal),
                result.LastScanElapsedMs,
                result.LastScanTileCount,
                result.TimeBudgetMs,
                result.CurrentCadenceTicks,
                result.BackoffApplied,
                result.LastUserCommand,
                result.AutoRescanDisabled,
                result.RevealedPercent,
                result.ScanComplete,
                result.ReadFailed,
                false,
                result.LastScanUtc,
                result.LastCompletedScanUtc,
                result.LastWriteUtc);
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

        private static DateTime? TryParseUtc(string value)
        {
            DateTime parsed;
            if (DateTime.TryParse(
                    value ?? string.Empty,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                    out parsed))
            {
                return parsed.ToUniversalTime();
            }

            return null;
        }
    }
}
