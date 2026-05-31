using System;
using System.Collections.Generic;

namespace JueMingZ.Diagnostics
{
    public static class LogThrottle
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, DateTime> LastLogUtcByKey = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static bool ShouldLog(string key, TimeSpan interval)
        {
            var safeKey = string.IsNullOrWhiteSpace(key) ? "default" : key;
            var now = DateTime.UtcNow;

            lock (SyncRoot)
            {
                DateTime lastLogUtc;
                if (!LastLogUtcByKey.TryGetValue(safeKey, out lastLogUtc) || now - lastLogUtc >= interval)
                {
                    LastLogUtcByKey[safeKey] = now;
                    return true;
                }
            }

            return false;
        }

        public static void InfoThrottled(string key, TimeSpan interval, string source, string message)
        {
            if (ShouldLog(key, interval))
            {
                Logger.Info(source, message);
            }
        }

        public static void WarnThrottled(string key, TimeSpan interval, string source, string message)
        {
            if (ShouldLog(key, interval))
            {
                Logger.Warn(source, message);
            }
        }

        public static void ErrorThrottled(string key, TimeSpan interval, string source, string message, Exception error = null)
        {
            if (ShouldLog(key, interval))
            {
                Logger.Error(source, message, error);
            }
        }
    }
}
