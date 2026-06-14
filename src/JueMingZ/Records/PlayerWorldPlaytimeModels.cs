using System;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldPlaytimeConstants
    {
        public const int SchemaVersion = 1;
        public const double DayLengthTicks = 54000d;
        public const double NightLengthTicks = 32400d;
        public const double FullDayTicks = DayLengthTicks + NightLengthTicks;
        public const string TimeSemantics = "observedWorldClockDelta;includesSleepAndTimeAcceleration;integerDays=floor(totalGameTicks/86400)";
    }

    [DataContract]
    public sealed class PlayerWorldPlaytimeFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PairId { get; set; }

        [DataMember(Order = 3)]
        public double TotalGameTicks { get; set; }

        [DataMember(Order = 4)]
        public int WholeDayCount { get; set; }

        [DataMember(Order = 5)]
        public string TimeSemantics { get; set; }

        [DataMember(Order = 6)]
        public string LastSampleUtc { get; set; }

        [DataMember(Order = 7)]
        public bool LastSampleDayTime { get; set; }

        [DataMember(Order = 8)]
        public double LastSampleWorldTime { get; set; }

        [DataMember(Order = 9)]
        public double LastSampleDayRate { get; set; }

        [DataMember(Order = 10)]
        public double LastSampleCyclePosition { get; set; }

        [DataMember(Order = 11)]
        public long LastSampleRuntimeTick { get; set; }

        [DataMember(Order = 12)]
        public double LastAcceptedDeltaGameTicks { get; set; }

        [DataMember(Order = 13)]
        public string LastSkippedDeltaReason { get; set; }

        [DataMember(Order = 14)]
        public bool LastWriteSucceeded { get; set; }

        [DataMember(Order = 15)]
        public string LastWriteStatus { get; set; }

        [DataMember(Order = 16)]
        public string LastWriteMessage { get; set; }

        [DataMember(Order = 17)]
        public string LastUpdatedUtc { get; set; }

        public PlayerWorldPlaytimeFile()
        {
            SchemaVersion = PlayerWorldPlaytimeConstants.SchemaVersion;
            PairId = string.Empty;
            TimeSemantics = PlayerWorldPlaytimeConstants.TimeSemantics;
            LastSampleUtc = string.Empty;
            LastSkippedDeltaReason = string.Empty;
            LastWriteStatus = string.Empty;
            LastWriteMessage = string.Empty;
            LastUpdatedUtc = string.Empty;
        }
    }

    internal sealed class PlayerWorldClockSample
    {
        public bool DayTime { get; set; }
        public double WorldTime { get; set; }
        public double DayRate { get; set; }
        public long RuntimeTick { get; set; }
        public DateTime SampleUtc { get; set; }

        public double CyclePosition
        {
            get { return DayTime ? WorldTime : PlayerWorldPlaytimeConstants.DayLengthTicks + WorldTime; }
        }

        public string ClockText
        {
            get
            {
                return (DayTime ? "day" : "night") +
                       ";time=" + WorldTime.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) +
                       ";dayRate=" + DayRate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public PlayerWorldClockSample()
        {
            DayRate = 1d;
            SampleUtc = DateTime.UtcNow;
        }
    }

    internal sealed class PlayerWorldPlaytimeReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool ReadFailed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public double TotalGameTicks { get; set; }
        public int WholeDayCount { get; set; }
        public int DataSignature { get; set; }
        public DateTime LastReadUtc { get; set; }
        public DateTime? LastSampleUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldPlaytimeReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            LastReadUtc = DateTime.UtcNow;
        }
    }

    internal sealed class PlayerWorldPlaytimeUpdateResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool Accumulated { get; set; }
        public bool Flushed { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public double TotalGameTicks { get; set; }
        public int WholeDayCount { get; set; }
        public double DeltaGameTicks { get; set; }
        public string LastSkippedDeltaReason { get; set; }
        public string ClockText { get; set; }
        public DateTime? LastSampleUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldPlaytimeUpdateResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            LastSkippedDeltaReason = string.Empty;
            ClockText = string.Empty;
        }
    }
}
