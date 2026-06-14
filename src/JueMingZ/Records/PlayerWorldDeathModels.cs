using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldDeathSourceKind
    {
        public const string Unknown = "unknown";
        public const string Npc = "npc";
        public const string Projectile = "projectile";
        public const string Player = "player";
        public const string Other = "other";
        public const string Custom = "custom";
    }

    [DataContract]
    public sealed class PlayerWorldDeathEvent
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string EventId { get; set; }

        [DataMember(Order = 3)]
        public string RealTimeUtc { get; set; }

        [DataMember(Order = 4)]
        public string RealTimeLocalText { get; set; }

        [DataMember(Order = 5)]
        public int PlayerTileX { get; set; }

        [DataMember(Order = 6)]
        public int PlayerTileY { get; set; }

        [DataMember(Order = 7)]
        public float PlayerWorldX { get; set; }

        [DataMember(Order = 8)]
        public float PlayerWorldY { get; set; }

        [DataMember(Order = 9)]
        public string DeathText { get; set; }

        [DataMember(Order = 10)]
        public double Damage { get; set; }

        [DataMember(Order = 11)]
        public int HitDirection { get; set; }

        [DataMember(Order = 12)]
        public bool Pvp { get; set; }

        [DataMember(Order = 13)]
        public string SourceKind { get; set; }

        [DataMember(Order = 14)]
        public int SourceNpcType { get; set; }

        [DataMember(Order = 15)]
        public int SourceProjectileType { get; set; }

        [DataMember(Order = 16)]
        public string SourcePlayerName { get; set; }

        [DataMember(Order = 17)]
        public int SourceOtherIndex { get; set; }

        [DataMember(Order = 18)]
        public string SourceCustomReason { get; set; }

        [DataMember(Order = 19)]
        public string IdentityPairId { get; set; }

        public PlayerWorldDeathEvent()
        {
            SchemaVersion = 1;
            EventId = string.Empty;
            RealTimeUtc = string.Empty;
            RealTimeLocalText = string.Empty;
            PlayerTileX = -1;
            PlayerTileY = -1;
            PlayerWorldX = -1f;
            PlayerWorldY = -1f;
            DeathText = string.Empty;
            SourceKind = PlayerWorldDeathSourceKind.Unknown;
            SourceNpcType = -1;
            SourceProjectileType = -1;
            SourcePlayerName = string.Empty;
            SourceOtherIndex = -1;
            SourceCustomReason = string.Empty;
            IdentityPairId = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldDeathSummaryFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PairId { get; set; }

        [DataMember(Order = 3)]
        public int DeathCount { get; set; }

        [DataMember(Order = 4)]
        public string LastEventId { get; set; }

        [DataMember(Order = 5)]
        public string LastDeathUtc { get; set; }

        [DataMember(Order = 6)]
        public string LastDeathLocalText { get; set; }

        [DataMember(Order = 7)]
        public bool LastWriteSucceeded { get; set; }

        [DataMember(Order = 8)]
        public string LastWriteStatus { get; set; }

        [DataMember(Order = 9)]
        public string LastWriteMessage { get; set; }

        [DataMember(Order = 10)]
        public bool DeathHistoryReadFailed { get; set; }

        [DataMember(Order = 11)]
        public string DeathHistoryReadMessage { get; set; }

        [DataMember(Order = 12)]
        public bool DeathSummaryReadFailed { get; set; }

        [DataMember(Order = 13)]
        public string DeathSummaryReadMessage { get; set; }

        [DataMember(Order = 14)]
        public string LastUpdatedUtc { get; set; }

        public PlayerWorldDeathSummaryFile()
        {
            SchemaVersion = 1;
            PairId = string.Empty;
            LastEventId = string.Empty;
            LastDeathUtc = string.Empty;
            LastDeathLocalText = string.Empty;
            LastWriteStatus = string.Empty;
            LastWriteMessage = string.Empty;
            DeathHistoryReadMessage = string.Empty;
            DeathSummaryReadMessage = string.Empty;
            LastUpdatedUtc = string.Empty;
        }
    }

    public sealed class PlayerWorldDeathSourceSnapshot
    {
        public string SourceKind { get; set; }
        public int SourceNpcType { get; set; }
        public int SourceProjectileType { get; set; }
        public string SourcePlayerName { get; set; }
        public int SourceOtherIndex { get; set; }
        public string SourceCustomReason { get; set; }

        public PlayerWorldDeathSourceSnapshot()
        {
            SourceKind = PlayerWorldDeathSourceKind.Unknown;
            SourceNpcType = -1;
            SourceProjectileType = -1;
            SourcePlayerName = string.Empty;
            SourceOtherIndex = -1;
            SourceCustomReason = string.Empty;
        }
    }

    public sealed class PlayerWorldDeathRecordResult
    {
        public bool Succeeded { get; set; }
        public bool EventWritten { get; set; }
        public bool SummaryWritten { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public string EventId { get; set; }
        public int DeathCount { get; set; }
        public bool DeathHistoryReadFailed { get; set; }

        public PlayerWorldDeathRecordResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            EventId = string.Empty;
        }
    }

    public sealed class PlayerWorldDeathRecordDiagnostics
    {
        public string LastRecordStatus { get; set; }
        public string LastRecordMessage { get; set; }
        public string LastEventId { get; set; }
        public string LastPairId { get; set; }
        public int LastDeathCount { get; set; }
        public bool DeathHistoryReadFailed { get; set; }

        public PlayerWorldDeathRecordDiagnostics()
        {
            LastRecordStatus = string.Empty;
            LastRecordMessage = string.Empty;
            LastEventId = string.Empty;
            LastPairId = string.Empty;
        }
    }
}
