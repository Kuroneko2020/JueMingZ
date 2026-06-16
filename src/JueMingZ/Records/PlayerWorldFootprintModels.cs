using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldFootprintConstants
    {
        public const int SchemaVersion = 1;
        public const long TicksPerSecond = 60L;
        public const long MaxRetainedHours = 200L;
        public const long MaxRetainedTicks = MaxRetainedHours * 60L * 60L * TicksPerSecond;

        public static string FormatUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    [DataContract]
    public sealed class PlayerWorldFootprintFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PairId { get; set; }

        [DataMember(Order = 3)]
        public int WorldSizeX { get; set; }

        [DataMember(Order = 4)]
        public int WorldSizeY { get; set; }

        [DataMember(Order = 5)]
        public long TimelineStartTicks { get; set; }

        [DataMember(Order = 6)]
        public long TimelineEndTicks { get; set; }

        [DataMember(Order = 7)]
        public long MaxRetainedTicks { get; set; }

        [DataMember(Order = 8)]
        public List<PlayerWorldFootprintSegment> Segments { get; set; }

        [DataMember(Order = 9)]
        public string LastUpdatedUtc { get; set; }

        public PlayerWorldFootprintFile()
        {
            SchemaVersion = PlayerWorldFootprintConstants.SchemaVersion;
            PairId = string.Empty;
            MaxRetainedTicks = PlayerWorldFootprintConstants.MaxRetainedTicks;
            Segments = new List<PlayerWorldFootprintSegment>();
            LastUpdatedUtc = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldFootprintSegment
    {
        [DataMember(Order = 1)]
        public string SegmentId { get; set; }

        [DataMember(Order = 2)]
        public long StartTicks { get; set; }

        [DataMember(Order = 3)]
        public long EndTicks { get; set; }

        [DataMember(Order = 4)]
        public string BreakReason { get; set; }

        [DataMember(Order = 5)]
        public List<PlayerWorldFootprintPoint> Points { get; set; }

        public PlayerWorldFootprintSegment()
        {
            SegmentId = string.Empty;
            BreakReason = string.Empty;
            Points = new List<PlayerWorldFootprintPoint>();
        }
    }

    [DataContract]
    public sealed class PlayerWorldFootprintPoint
    {
        [DataMember(Order = 1)]
        public double TileX { get; set; }

        [DataMember(Order = 2)]
        public double TileY { get; set; }

        [DataMember(Order = 3)]
        public long StartTicks { get; set; }

        [DataMember(Order = 4)]
        public long DurationTicks { get; set; }

        [DataMember(Order = 5)]
        public int Flags { get; set; }
    }

    internal sealed class PlayerWorldFootprintReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public bool RetentionTrimmed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public long TimelineStartTicks { get; set; }
        public long TimelineEndTicks { get; set; }
        public long MaxRetainedTicks { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }
        public List<PlayerWorldFootprintSegment> Segments { get; set; }
        public int DataSignature { get; set; }

        public PlayerWorldFootprintReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            MaxRetainedTicks = PlayerWorldFootprintConstants.MaxRetainedTicks;
            Segments = new List<PlayerWorldFootprintSegment>();
        }
    }

    internal sealed class PlayerWorldFootprintWriteResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool Changed { get; set; }
        public bool RetentionTrimmed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int SegmentCount { get; set; }
        public int PointCount { get; set; }
        public string Operation { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldFootprintWriteResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            Operation = string.Empty;
        }
    }
}
