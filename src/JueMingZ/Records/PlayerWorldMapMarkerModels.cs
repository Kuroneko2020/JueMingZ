using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace JueMingZ.Records
{
    public static class PlayerWorldMapMarkerConstants
    {
        public const int SchemaVersion = 1;
        public const int MaxMarkersPerPair = 256;
        public const int MaxCachedMarkers = 512;
        public const int MaxNameTextUnits = 10;
        public const int DefaultIconItemId = 8;
        public const int LegacyFallenStarIconItemId = 75;
        public const int ReplacementBedIconItemId = 224;

        private static readonly int[] IconWhitelist = { 8, 48, 50, ReplacementBedIconItemId, 171, 393, 966, 29 };

        public static bool IsAllowedIconItemId(int itemId)
        {
            for (var index = 0; index < IconWhitelist.Length; index++)
            {
                if (IconWhitelist[index] == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        public static int NormalizeIconItemId(int itemId)
        {
            if (itemId == LegacyFallenStarIconItemId)
            {
                return ReplacementBedIconItemId;
            }

            return IsAllowedIconItemId(itemId) ? itemId : DefaultIconItemId;
        }

        public static string NormalizeName(string name)
        {
            var text = string.IsNullOrEmpty(name) ? string.Empty : name.Trim();
            if (text.Length <= MaxNameTextUnits)
            {
                return text;
            }

            return text.Substring(0, MaxNameTextUnits);
        }

        public static string FormatUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }

    [DataContract]
    public sealed class PlayerWorldMapMarkerFile
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
        public List<PlayerWorldMapMarkerRecord> Markers { get; set; }

        [DataMember(Order = 6)]
        public string LastUpdatedUtc { get; set; }

        public PlayerWorldMapMarkerFile()
        {
            SchemaVersion = PlayerWorldMapMarkerConstants.SchemaVersion;
            PairId = string.Empty;
            Markers = new List<PlayerWorldMapMarkerRecord>();
            LastUpdatedUtc = string.Empty;
        }
    }

    [DataContract]
    public sealed class PlayerWorldMapMarkerRecord
    {
        [DataMember(Order = 1)]
        public string MarkerId { get; set; }

        [DataMember(Order = 2)]
        public int TileX { get; set; }

        [DataMember(Order = 3)]
        public int TileY { get; set; }

        [DataMember(Order = 4)]
        public int IconItemId { get; set; }

        [DataMember(Order = 5)]
        public string Name { get; set; }

        [DataMember(Order = 6)]
        public string CreatedUtc { get; set; }

        [DataMember(Order = 7)]
        public string UpdatedUtc { get; set; }

        [DataMember(Order = 8)]
        public int SortOrder { get; set; }

        public PlayerWorldMapMarkerRecord()
        {
            MarkerId = string.Empty;
            Name = string.Empty;
            CreatedUtc = string.Empty;
            UpdatedUtc = string.Empty;
        }
    }

    internal sealed class PlayerWorldMapMarkerReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool ReadFailed { get; set; }
        public bool WriteFailed { get; set; }
        public bool CulledByCacheLimit { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int WorldSizeX { get; set; }
        public int WorldSizeY { get; set; }
        public int MarkerCount { get; set; }
        public int TotalMarkerCount { get; set; }
        public string LastOperation { get; set; }
        public DateTime? LastReadUtc { get; set; }
        public DateTime? LastWriteUtc { get; set; }
        public List<PlayerWorldMapMarkerRecord> Markers { get; set; }

        public PlayerWorldMapMarkerReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            LastOperation = string.Empty;
            Markers = new List<PlayerWorldMapMarkerRecord>();
        }
    }

    internal sealed class PlayerWorldMapMarkerWriteResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool LimitExceeded { get; set; }
        public bool Changed { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int MarkerCount { get; set; }
        public string Operation { get; set; }
        public DateTime? LastWriteUtc { get; set; }

        public PlayerWorldMapMarkerWriteResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            Operation = string.Empty;
        }
    }
}
