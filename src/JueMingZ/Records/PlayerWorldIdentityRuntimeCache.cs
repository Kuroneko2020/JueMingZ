using System;
using System.Globalization;
using System.Text;

namespace JueMingZ.Records
{
    public static class PlayerWorldIdentityRuntimeCache
    {
        internal const long MaintenancePersistCadenceTicks = 1800;

        private static readonly object SyncRoot = new object();
        private static PlayerWorldIdentityResolution _cachedResolution;
        private static string _cachedSignature = string.Empty;
        private static long _nextPersistTick;
        private static int _persistAttemptCountForTesting;

        public static bool TryResolveCurrentCached(long runtimeTick, out PlayerWorldIdentityResolution resolution)
        {
            PlayerWorldIdentityResolution current;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out current) ||
                current == null ||
                !current.IsResolved ||
                string.IsNullOrWhiteSpace(current.PairId))
            {
                ClearCachedResolution();
                resolution = current;
                return false;
            }

            return ResolveCached(current, runtimeTick, out resolution);
        }

        internal static bool TryResolveCachedForTesting(
            PlayerWorldIdentityFacts facts,
            long runtimeTick,
            out PlayerWorldIdentityResolution resolution)
        {
            PlayerWorldIdentityResolution current;
            if (!PlayerWorldIdentityResolver.TryResolveForTesting(facts, out current) ||
                current == null ||
                !current.IsResolved ||
                string.IsNullOrWhiteSpace(current.PairId))
            {
                ClearCachedResolution();
                resolution = current;
                return false;
            }

            return ResolveCached(current, runtimeTick, out resolution);
        }

        internal static int PersistAttemptCountForTesting
        {
            get
            {
                lock (SyncRoot)
                {
                    return _persistAttemptCountForTesting;
                }
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedResolution = null;
                _cachedSignature = string.Empty;
                _nextPersistTick = 0;
                _persistAttemptCountForTesting = 0;
            }
        }

        private static bool ResolveCached(
            PlayerWorldIdentityResolution current,
            long runtimeTick,
            out PlayerWorldIdentityResolution resolution)
        {
            var signature = BuildSignature(current);
            var normalizedTick = Math.Max(0L, runtimeTick);
            lock (SyncRoot)
            {
                if (_cachedResolution != null &&
                    string.Equals(_cachedSignature, signature, StringComparison.Ordinal) &&
                    normalizedTick < _nextPersistTick)
                {
                    resolution = _cachedResolution;
                    return true;
                }

                // Persisting identity files is intentionally edge/maintenance-only;
                // high-frequency services must not drag JSON safe writes into cadence ticks.
                _persistAttemptCountForTesting++;
                PlayerWorldIdentityResolver.TryPersistResolved(current);
                _cachedResolution = current;
                _cachedSignature = signature;
                _nextPersistTick = normalizedTick + MaintenancePersistCadenceTicks;
                resolution = _cachedResolution;
                return true;
            }
        }

        private static void ClearCachedResolution()
        {
            lock (SyncRoot)
            {
                _cachedResolution = null;
                _cachedSignature = string.Empty;
                _nextPersistTick = 0;
            }
        }

        private static string BuildSignature(PlayerWorldIdentityResolution resolution)
        {
            if (resolution == null || !resolution.IsResolved)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(256);
            Append(builder, resolution.PlayerId);
            Append(builder, resolution.WorldId);
            Append(builder, resolution.PairId);
            Append(builder, resolution.PlayerDisplayName);
            Append(builder, resolution.WorldDisplayName);
            Append(builder, resolution.PlayerIdentitySourceKind);
            Append(builder, resolution.WorldIdentitySourceKind);
            Append(builder, resolution.PlayerPathHash);
            Append(builder, resolution.WorldPathHash);
            Append(builder, resolution.PlayerIsCloudSave ? "playerCloud" : "playerLocal");
            Append(builder, resolution.WorldIsCloudSave ? "worldCloud" : "worldLocal");
            Append(builder, resolution.WorldUniqueId);
            Append(builder, resolution.MapFileName);
            Append(builder, resolution.HasTerrariaWorldId ? resolution.TerrariaWorldId.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Append(builder, resolution.WorldSizeX.ToString(CultureInfo.InvariantCulture));
            Append(builder, resolution.WorldSizeY.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string value)
        {
            builder.Append(value ?? string.Empty);
            builder.Append('\n');
        }
    }
}
