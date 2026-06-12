using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal static class ChestItemLocatorSectionRequestService
    {
        private const int SectionWidthTiles = 200;
        private const int SectionHeightTiles = 150;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, ulong> LastSentTickBySectionKey = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private static ChestItemLocatorSectionRequestDiagnostics _diagnostics = new ChestItemLocatorSectionRequestDiagnostics();

        public static ChestItemLocatorSectionRequestResult TryRequestForQuery(
            InformationWorldContext context,
            AppSettings settings,
            long queryVersion)
        {
            return TryRequestForQuery(
                context,
                ChestItemLocatorSectionRequestOptions.FromSettings(settings),
                null,
                queryVersion);
        }

        internal static ChestItemLocatorSectionRequestResult TryRequestForTesting(
            InformationWorldContext context,
            ChestItemLocatorSectionRequestOptions options,
            ChestItemLocatorSectionRequestPorts ports,
            long queryVersion)
        {
            return TryRequestForQuery(context, options, ports, queryVersion);
        }

        public static ChestItemLocatorSectionRequestDiagnostics GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                LastSentTickBySectionKey.Clear();
                _diagnostics = new ChestItemLocatorSectionRequestDiagnostics();
            }
        }

        private static ChestItemLocatorSectionRequestResult TryRequestForQuery(
            InformationWorldContext context,
            ChestItemLocatorSectionRequestOptions options,
            ChestItemLocatorSectionRequestPorts ports,
            long queryVersion)
        {
            options = options ?? ChestItemLocatorSectionRequestOptions.Default;
            ports = NormalizePorts(ports);

            var netMode = SafeReadNetMode(ports);
            var multiplayerClient = netMode == 1;
            var tick = context == null ? 0 : context.GameUpdateCount;
            if (!options.Enabled)
            {
                return Record(new ChestItemLocatorSectionRequestResult(
                    false,
                    multiplayerClient,
                    false,
                    false,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusDisabled,
                    string.Empty,
                    -1,
                    -1,
                    string.Empty,
                    queryVersion,
                    tick,
                    0));
            }

            if (!multiplayerClient)
            {
                return Record(new ChestItemLocatorSectionRequestResult(
                    true,
                    false,
                    false,
                    false,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusNotMultiplayerClient,
                    string.Empty,
                    -1,
                    -1,
                    string.Empty,
                    queryVersion,
                    tick,
                    0));
            }

            if (context == null)
            {
                return Record(new ChestItemLocatorSectionRequestResult(
                    true,
                    true,
                    false,
                    false,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusContextUnavailable,
                    "contextUnavailable",
                    -1,
                    -1,
                    string.Empty,
                    queryVersion,
                    tick,
                    0));
            }

            int sectionX;
            int sectionY;
            string failureReason;
            if (!TryBuildPlayerSection(context, out sectionX, out sectionY, out failureReason))
            {
                return Record(new ChestItemLocatorSectionRequestResult(
                    true,
                    true,
                    false,
                    false,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusInvalidSection,
                    failureReason,
                    sectionX,
                    sectionY,
                    string.Empty,
                    queryVersion,
                    tick,
                    0));
            }

            var sectionKey = BuildSectionKey(context, sectionX, sectionY);
            lock (SyncRoot)
            {
                ulong lastSentTick;
                if (LastSentTickBySectionKey.TryGetValue(sectionKey, out lastSentTick) &&
                    tick >= lastSentTick &&
                    tick - lastSentTick < options.CooldownTicks)
                {
                    return RecordLocked(new ChestItemLocatorSectionRequestResult(
                        true,
                        true,
                        false,
                        false,
                        true,
                        ChestItemLocatorSectionRequestResult.StatusThrottled,
                        string.Empty,
                        sectionX,
                        sectionY,
                        sectionKey,
                        queryVersion,
                        tick,
                        options.CooldownTicks - (tick - lastSentTick)));
                }
            }

            if (!ports.TryRequestSectionData(sectionX, sectionY, out failureReason))
            {
                return Record(new ChestItemLocatorSectionRequestResult(
                    true,
                    true,
                    true,
                    false,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusFailed,
                    failureReason,
                    sectionX,
                    sectionY,
                    sectionKey,
                    queryVersion,
                    tick,
                    0));
            }

            lock (SyncRoot)
            {
                LastSentTickBySectionKey[sectionKey] = tick;
                return RecordLocked(new ChestItemLocatorSectionRequestResult(
                    true,
                    true,
                    true,
                    true,
                    false,
                    ChestItemLocatorSectionRequestResult.StatusSent,
                    string.Empty,
                    sectionX,
                    sectionY,
                    sectionKey,
                    queryVersion,
                    tick,
                    0));
            }
        }

        private static ChestItemLocatorSectionRequestResult Record(ChestItemLocatorSectionRequestResult result)
        {
            lock (SyncRoot)
            {
                return RecordLocked(result);
            }
        }

        private static ChestItemLocatorSectionRequestResult RecordLocked(ChestItemLocatorSectionRequestResult result)
        {
            _diagnostics = ChestItemLocatorSectionRequestDiagnostics.FromResult(result);
            return result;
        }

        private static ChestItemLocatorSectionRequestPorts NormalizePorts(ChestItemLocatorSectionRequestPorts ports)
        {
            if (ports == null)
            {
                ports = new ChestItemLocatorSectionRequestPorts();
            }

            if (ports.GetNetMode == null)
            {
                ports.GetNetMode = () => TerrariaMainCompat.NetMode;
            }

            if (ports.TryRequestSectionData == null)
            {
                ports.TryRequestSectionData = TerrariaNetworkCompat.TryRequestChestSectionData;
            }

            return ports;
        }

        private static int SafeReadNetMode(ChestItemLocatorSectionRequestPorts ports)
        {
            try
            {
                return ports.GetNetMode == null ? 0 : ports.GetNetMode();
            }
            catch
            {
                return 0;
            }
        }

        private static bool TryBuildPlayerSection(
            InformationWorldContext context,
            out int sectionX,
            out int sectionY,
            out string failureReason)
        {
            sectionX = -1;
            sectionY = -1;
            failureReason = string.Empty;
            if (context == null)
            {
                failureReason = "contextUnavailable";
                return false;
            }

            var tileX = (int)Math.Floor(context.PlayerCenterX / 16f);
            var tileY = (int)Math.Floor(context.PlayerCenterY / 16f);
            if (tileX < 0 || tileY < 0)
            {
                failureReason = "playerTileOutOfWorld";
                return false;
            }

            sectionX = tileX / SectionWidthTiles;
            sectionY = tileY / SectionHeightTiles;
            return true;
        }

        private static string BuildSectionKey(InformationWorldContext context, int sectionX, int sectionY)
        {
            var world = context == null ? string.Empty : FirstNonEmpty(context.WorldRecordKey, context.WorldKey);
            return world + ":" +
                   sectionX.ToString(CultureInfo.InvariantCulture) + ":" +
                   sectionY.ToString(CultureInfo.InvariantCulture);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
