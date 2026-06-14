using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldIdentitySourceKind
    {
        public const string PlayerPathHash = "playerPathHash";
        public const string PlayerDisplayNameFallback = "playerDisplayNameFallback";
        public const string WorldUniqueId = "worldUniqueId";
        public const string WorldMapFileName = "worldMapFileName";
        public const string WorldIdPathHash = "worldIdPathHash";
        public const string WorldPathHashFallback = "worldPathHashFallback";
        public const string WorldIdDisplayNameFallback = "worldIdDisplayNameFallback";
        public const string WorldDisplayNameFallback = "worldDisplayNameFallback";
    }

    public sealed class PlayerWorldIdentityFacts
    {
        public string PlayerPath { get; set; }
        public bool PlayerIsCloudSave { get; set; }
        public string PlayerName { get; set; }
        public string WorldPath { get; set; }
        public bool WorldIsCloudSave { get; set; }
        public string WorldName { get; set; }
        public string WorldUniqueId { get; set; }
        public int WorldId { get; set; }
        public bool HasWorldId { get; set; }
        public string MapFileName { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public string MainWorldName { get; set; }
        public int MainWorldId { get; set; }
        public bool HasMainWorldId { get; set; }
        public string MainWorldPathName { get; set; }

        public PlayerWorldIdentityFacts()
        {
            PlayerPath = string.Empty;
            PlayerName = string.Empty;
            WorldPath = string.Empty;
            WorldName = string.Empty;
            WorldUniqueId = string.Empty;
            MapFileName = string.Empty;
            MainWorldName = string.Empty;
            MainWorldPathName = string.Empty;
        }
    }

    public sealed class PlayerWorldIdentityResolution
    {
        public bool IsResolved { get; set; }
        public string FailureReason { get; set; }
        public string PlayerId { get; set; }
        public string WorldId { get; set; }
        public string PairId { get; set; }
        public string PlayerDisplayName { get; set; }
        public string WorldDisplayName { get; set; }
        public string PlayerIdentitySourceKind { get; set; }
        public string WorldIdentitySourceKind { get; set; }
        public string PlayerPathHash { get; set; }
        public string WorldPathHash { get; set; }
        public bool PlayerIsCloudSave { get; set; }
        public bool WorldIsCloudSave { get; set; }
        public string WorldUniqueId { get; set; }
        public string MapFileName { get; set; }
        public int TerrariaWorldId { get; set; }
        public bool HasTerrariaWorldId { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public bool IdentityFilesWritten { get; set; }
        public string StorageMessage { get; set; }
        public string DiagnosticSummary { get; set; }

        public PlayerWorldIdentityResolution()
        {
            FailureReason = string.Empty;
            PlayerId = string.Empty;
            WorldId = string.Empty;
            PairId = string.Empty;
            PlayerDisplayName = string.Empty;
            WorldDisplayName = string.Empty;
            PlayerIdentitySourceKind = string.Empty;
            WorldIdentitySourceKind = string.Empty;
            PlayerPathHash = string.Empty;
            WorldPathHash = string.Empty;
            WorldUniqueId = string.Empty;
            MapFileName = string.Empty;
            StorageMessage = string.Empty;
            DiagnosticSummary = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldIdentityAlias
    {
        [DataMember(Order = 1)]
        public string Kind { get; set; }

        [DataMember(Order = 2)]
        public string Value { get; set; }

        [DataMember(Order = 3)]
        public string FirstSeenUtc { get; set; }

        [DataMember(Order = 4)]
        public string LastSeenUtc { get; set; }

        public PlayerWorldIdentityAlias()
        {
            Kind = string.Empty;
            Value = string.Empty;
            FirstSeenUtc = string.Empty;
            LastSeenUtc = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerIdentityFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PlayerId { get; set; }

        [DataMember(Order = 3)]
        public string IdentitySourceKind { get; set; }

        [DataMember(Order = 4)]
        public string DisplayName { get; set; }

        [DataMember(Order = 5)]
        public string PathHash { get; set; }

        [DataMember(Order = 6)]
        public bool IsCloudSave { get; set; }

        [DataMember(Order = 7)]
        public string FirstSeenUtc { get; set; }

        [DataMember(Order = 8)]
        public string LastSeenUtc { get; set; }

        [DataMember(Order = 9)]
        public List<PlayerWorldIdentityAlias> ObservedAliases { get; set; }

        [DataMember(Order = 10)]
        public string DiagnosticSummary { get; set; }

        public PlayerIdentityFile()
        {
            SchemaVersion = 1;
            PlayerId = string.Empty;
            IdentitySourceKind = string.Empty;
            DisplayName = string.Empty;
            PathHash = string.Empty;
            FirstSeenUtc = string.Empty;
            LastSeenUtc = string.Empty;
            ObservedAliases = new List<PlayerWorldIdentityAlias>();
            DiagnosticSummary = string.Empty;
        }
    }

    [DataContract]
    public sealed class WorldIdentityFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string WorldId { get; set; }

        [DataMember(Order = 3)]
        public string IdentitySourceKind { get; set; }

        [DataMember(Order = 4)]
        public string DisplayName { get; set; }

        [DataMember(Order = 5)]
        public string PathHash { get; set; }

        [DataMember(Order = 6)]
        public bool IsCloudSave { get; set; }

        [DataMember(Order = 7)]
        public string UniqueId { get; set; }

        [DataMember(Order = 8)]
        public string MapFileName { get; set; }

        [DataMember(Order = 9)]
        public int TerrariaWorldId { get; set; }

        [DataMember(Order = 10)]
        public bool HasTerrariaWorldId { get; set; }

        [DataMember(Order = 11)]
        public int WorldSizeX { get; set; }

        [DataMember(Order = 12)]
        public int WorldSizeY { get; set; }

        [DataMember(Order = 13)]
        public string FirstSeenUtc { get; set; }

        [DataMember(Order = 14)]
        public string LastSeenUtc { get; set; }

        [DataMember(Order = 15)]
        public List<PlayerWorldIdentityAlias> ObservedAliases { get; set; }

        [DataMember(Order = 16)]
        public string DiagnosticSummary { get; set; }

        public WorldIdentityFile()
        {
            SchemaVersion = 1;
            WorldId = string.Empty;
            IdentitySourceKind = string.Empty;
            DisplayName = string.Empty;
            PathHash = string.Empty;
            UniqueId = string.Empty;
            MapFileName = string.Empty;
            FirstSeenUtc = string.Empty;
            LastSeenUtc = string.Empty;
            ObservedAliases = new List<PlayerWorldIdentityAlias>();
            DiagnosticSummary = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldIdentityFile
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public string PairId { get; set; }

        [DataMember(Order = 3)]
        public string PlayerId { get; set; }

        [DataMember(Order = 4)]
        public string WorldId { get; set; }

        [DataMember(Order = 5)]
        public string PlayerDisplayName { get; set; }

        [DataMember(Order = 6)]
        public string WorldDisplayName { get; set; }

        [DataMember(Order = 7)]
        public string FirstSeenUtc { get; set; }

        [DataMember(Order = 8)]
        public string LastSeenUtc { get; set; }

        [DataMember(Order = 9)]
        public string DiagnosticSummary { get; set; }

        public PlayerWorldIdentityFile()
        {
            SchemaVersion = 1;
            PairId = string.Empty;
            PlayerId = string.Empty;
            WorldId = string.Empty;
            PlayerDisplayName = string.Empty;
            WorldDisplayName = string.Empty;
            FirstSeenUtc = string.Empty;
            LastSeenUtc = string.Empty;
            DiagnosticSummary = string.Empty;
        }
    }
}
