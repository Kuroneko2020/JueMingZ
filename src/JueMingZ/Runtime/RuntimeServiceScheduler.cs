using System;
using System.Collections.Generic;

namespace JueMingZ.Runtime
{
    // Owns only Runtime's per-service cadence and enable-edge bookkeeping; service business rules stay in their services.
    internal static class RuntimeServiceScheduler
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, bool> LastEnabled =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> LastRunTick =
            new Dictionary<string, long>(StringComparer.Ordinal);

        public static bool ShouldRun(string serviceName, bool enabled, int cadenceTicks, long updateTick)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                return enabled;
            }

            if (cadenceTicks <= 0)
            {
                cadenceTicks = 1;
            }

            lock (SyncRoot)
            {
                bool wasEnabled;
                LastEnabled.TryGetValue(serviceName, out wasEnabled);

                if (!enabled)
                {
                    LastEnabled[serviceName] = false;
                    if (wasEnabled)
                    {
                        LastRunTick[serviceName] = updateTick;
                        return true;
                    }

                    return false;
                }

                LastEnabled[serviceName] = true;
                if (!wasEnabled)
                {
                    LastRunTick[serviceName] = updateTick;
                    return true;
                }

                long lastRunTick;
                if (!LastRunTick.TryGetValue(serviceName, out lastRunTick) ||
                    updateTick < lastRunTick ||
                    updateTick - lastRunTick >= cadenceTicks)
                {
                    LastRunTick[serviceName] = updateTick;
                    return true;
                }

                return false;
            }
        }

        public static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                LastEnabled.Clear();
                LastRunTick.Clear();
            }
        }
    }
}
