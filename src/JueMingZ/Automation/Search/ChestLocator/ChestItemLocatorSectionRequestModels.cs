using System;
using System.Globalization;
using JueMingZ.Config;

namespace JueMingZ.Automation.Search.ChestLocator
{
    internal delegate bool ChestItemLocatorTryRequestSectionData(int sectionX, int sectionY, out string failureReason);

    internal sealed class ChestItemLocatorSectionRequestOptions
    {
        public const ulong DefaultCooldownTicks = 300;

        public static readonly ChestItemLocatorSectionRequestOptions Default =
            new ChestItemLocatorSectionRequestOptions(true, DefaultCooldownTicks);

        public bool Enabled { get; private set; }

        public ulong CooldownTicks { get; private set; }

        public ChestItemLocatorSectionRequestOptions(bool enabled, ulong cooldownTicks)
        {
            Enabled = enabled;
            CooldownTicks = cooldownTicks == 0 ? DefaultCooldownTicks : cooldownTicks;
        }

        public static ChestItemLocatorSectionRequestOptions FromSettings(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return new ChestItemLocatorSectionRequestOptions(
                settings.SearchChestLocatorMultiplayerSectionRequestEnabled,
                DefaultCooldownTicks);
        }
    }

    internal sealed class ChestItemLocatorSectionRequestResult
    {
        public const string StatusDisabled = "disabled";
        public const string StatusNotMultiplayerClient = "notMultiplayerClient";
        public const string StatusContextUnavailable = "contextUnavailable";
        public const string StatusInvalidSection = "invalidSection";
        public const string StatusSent = "sent";
        public const string StatusThrottled = "throttled";
        public const string StatusFailed = "failed";

        public bool Enabled { get; private set; }
        public bool MultiplayerClient { get; private set; }
        public bool Attempted { get; private set; }
        public bool Sent { get; private set; }
        public bool Throttled { get; private set; }
        public string Status { get; private set; }
        public string FailureReason { get; private set; }
        public int SectionX { get; private set; }
        public int SectionY { get; private set; }
        public string SectionKey { get; private set; }
        public long QueryVersion { get; private set; }
        public ulong RequestTick { get; private set; }
        public ulong CooldownRemainingTicks { get; private set; }

        public ChestItemLocatorSectionRequestResult(
            bool enabled,
            bool multiplayerClient,
            bool attempted,
            bool sent,
            bool throttled,
            string status,
            string failureReason,
            int sectionX,
            int sectionY,
            string sectionKey,
            long queryVersion,
            ulong requestTick,
            ulong cooldownRemainingTicks)
        {
            Enabled = enabled;
            MultiplayerClient = multiplayerClient;
            Attempted = attempted;
            Sent = sent;
            Throttled = throttled;
            Status = string.IsNullOrWhiteSpace(status) ? StatusFailed : status;
            FailureReason = failureReason ?? string.Empty;
            SectionX = sectionX;
            SectionY = sectionY;
            SectionKey = sectionKey ?? string.Empty;
            QueryVersion = queryVersion;
            RequestTick = requestTick;
            CooldownRemainingTicks = cooldownRemainingTicks;
        }

        public string BuildCompactSummary()
        {
            return Status + ";section=" +
                   SectionX.ToString(CultureInfo.InvariantCulture) + "," +
                   SectionY.ToString(CultureInfo.InvariantCulture) +
                   ";sent=" + (Sent ? "1" : "0") +
                   ";throttled=" + (Throttled ? "1" : "0");
        }
    }

    internal sealed class ChestItemLocatorSectionRequestDiagnostics
    {
        public bool Enabled { get; set; }
        public bool MultiplayerClient { get; set; }
        public bool Attempted { get; set; }
        public bool Sent { get; set; }
        public bool Throttled { get; set; }
        public string Status { get; set; }
        public string FailureReason { get; set; }
        public int SectionX { get; set; }
        public int SectionY { get; set; }
        public string SectionKey { get; set; }
        public long QueryVersion { get; set; }
        public ulong RequestTick { get; set; }
        public ulong CooldownRemainingTicks { get; set; }

        public ChestItemLocatorSectionRequestDiagnostics()
        {
            Status = string.Empty;
            FailureReason = string.Empty;
            SectionKey = string.Empty;
        }

        public ChestItemLocatorSectionRequestDiagnostics Clone()
        {
            return new ChestItemLocatorSectionRequestDiagnostics
            {
                Enabled = Enabled,
                MultiplayerClient = MultiplayerClient,
                Attempted = Attempted,
                Sent = Sent,
                Throttled = Throttled,
                Status = Status ?? string.Empty,
                FailureReason = FailureReason ?? string.Empty,
                SectionX = SectionX,
                SectionY = SectionY,
                SectionKey = SectionKey ?? string.Empty,
                QueryVersion = QueryVersion,
                RequestTick = RequestTick,
                CooldownRemainingTicks = CooldownRemainingTicks
            };
        }

        public static ChestItemLocatorSectionRequestDiagnostics FromResult(ChestItemLocatorSectionRequestResult result)
        {
            result = result ?? new ChestItemLocatorSectionRequestResult(
                false,
                false,
                false,
                false,
                false,
                string.Empty,
                string.Empty,
                -1,
                -1,
                string.Empty,
                0,
                0,
                0);

            return new ChestItemLocatorSectionRequestDiagnostics
            {
                Enabled = result.Enabled,
                MultiplayerClient = result.MultiplayerClient,
                Attempted = result.Attempted,
                Sent = result.Sent,
                Throttled = result.Throttled,
                Status = result.Status,
                FailureReason = result.FailureReason,
                SectionX = result.SectionX,
                SectionY = result.SectionY,
                SectionKey = result.SectionKey,
                QueryVersion = result.QueryVersion,
                RequestTick = result.RequestTick,
                CooldownRemainingTicks = result.CooldownRemainingTicks
            };
        }
    }

    internal sealed class ChestItemLocatorSectionRequestPorts
    {
        public Func<int> GetNetMode { get; set; }

        public ChestItemLocatorTryRequestSectionData TryRequestSectionData { get; set; }
    }
}
