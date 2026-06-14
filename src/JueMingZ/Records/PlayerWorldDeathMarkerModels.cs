using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingZ.Records
{
    internal sealed class PlayerWorldDeathMarker
    {
        public string EventId { get; set; }
        public string PairId { get; set; }
        public Vector2 TilePosition { get; set; }
        public string Tooltip { get; set; }

        public PlayerWorldDeathMarker()
        {
            EventId = string.Empty;
            PairId = string.Empty;
            Tooltip = string.Empty;
        }
    }

    internal sealed class PlayerWorldDeathMarkerReadResult
    {
        public bool Succeeded { get; set; }
        public bool IdentityResolved { get; set; }
        public bool HistoryReadFailed { get; set; }
        public bool CulledByLimit { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string PairId { get; set; }
        public int TotalEventCount { get; set; }
        public int MarkerCount { get; set; }
        public List<PlayerWorldDeathMarker> Markers { get; set; }

        public PlayerWorldDeathMarkerReadResult()
        {
            Status = string.Empty;
            Message = string.Empty;
            PairId = string.Empty;
            Markers = new List<PlayerWorldDeathMarker>();
        }
    }
}
