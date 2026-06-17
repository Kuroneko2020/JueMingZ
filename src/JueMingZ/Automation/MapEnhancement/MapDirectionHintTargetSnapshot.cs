using System;

namespace JueMingZ.Automation.MapEnhancement
{
    internal sealed class MapDirectionHintTargetSnapshot
    {
        public bool Enabled { get; set; }
        public bool RareCreatureEnabled { get; set; }
        public bool TravellingMerchantEnabled { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public long LastScanTick { get; set; }
        public long NextScanTick { get; set; }
        public int NpcCount { get; set; }
        public int ActiveNpcCount { get; set; }
        public int RareCandidateCount { get; set; }
        public int TownNpcCount { get; set; }
        public int HiddenNpcCount { get; set; }
        public DateTime CapturedUtc { get; set; }
        public MapDirectionHintNpcObservation[] Npcs { get; set; }
        public MapRareCreatureDirectionTarget RareCreatureTarget { get; set; }
        public MapTravellingMerchantDirectionTarget TravellingMerchantTarget { get; set; }

        public MapDirectionHintTargetSnapshot()
        {
            Status = string.Empty;
            Message = string.Empty;
            CapturedUtc = DateTime.UtcNow;
            Npcs = new MapDirectionHintNpcObservation[0];
            RareCreatureTarget = MapRareCreatureDirectionTarget.Disabled("notInitialized", "rare creature direction target not initialized");
            TravellingMerchantTarget = MapTravellingMerchantDirectionTarget.Disabled("notInitialized", "travelling merchant direction target not initialized");
        }

        public static MapDirectionHintTargetSnapshot Empty(string status, string message)
        {
            return new MapDirectionHintTargetSnapshot
            {
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                Npcs = new MapDirectionHintNpcObservation[0],
                RareCreatureTarget = MapRareCreatureDirectionTarget.Disabled(status, message),
                TravellingMerchantTarget = MapTravellingMerchantDirectionTarget.Disabled(status, message)
            };
        }

        public MapDirectionHintTargetSnapshot Clone()
        {
            var clone = new MapDirectionHintTargetSnapshot
            {
                Enabled = Enabled,
                RareCreatureEnabled = RareCreatureEnabled,
                TravellingMerchantEnabled = TravellingMerchantEnabled,
                Status = Status ?? string.Empty,
                Message = Message ?? string.Empty,
                LastScanTick = LastScanTick,
                NextScanTick = NextScanTick,
                NpcCount = NpcCount,
                ActiveNpcCount = ActiveNpcCount,
                RareCandidateCount = RareCandidateCount,
                TownNpcCount = TownNpcCount,
                HiddenNpcCount = HiddenNpcCount,
                CapturedUtc = CapturedUtc,
                Npcs = new MapDirectionHintNpcObservation[Npcs == null ? 0 : Npcs.Length],
                RareCreatureTarget = RareCreatureTarget == null
                    ? MapRareCreatureDirectionTarget.Disabled("notInitialized", "rare creature direction target not initialized")
                    : RareCreatureTarget.Clone(),
                TravellingMerchantTarget = TravellingMerchantTarget == null
                    ? MapTravellingMerchantDirectionTarget.Disabled("notInitialized", "travelling merchant direction target not initialized")
                    : TravellingMerchantTarget.Clone()
            };

            for (var index = 0; Npcs != null && index < Npcs.Length; index++)
            {
                clone.Npcs[index] = Npcs[index] == null ? null : Npcs[index].Clone();
            }

            return clone;
        }
    }

    internal sealed class MapDirectionHintNpcObservation
    {
        public bool Active { get; set; }
        public int Type { get; set; }
        public int WhoAmI { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public string DisplayName { get; set; }
        public int Rarity { get; set; }
        public bool TownNpc { get; set; }
        public bool Homeless { get; set; }
        public int HomeTileX { get; set; }
        public int HomeTileY { get; set; }
        public bool Hidden { get; set; }

        public MapDirectionHintNpcObservation()
        {
            DisplayName = string.Empty;
            WhoAmI = -1;
            HomeTileX = -1;
            HomeTileY = -1;
        }

        public MapDirectionHintNpcObservation Clone()
        {
            return new MapDirectionHintNpcObservation
            {
                Active = Active,
                Type = Type,
                WhoAmI = WhoAmI,
                CenterX = CenterX,
                CenterY = CenterY,
                DisplayName = DisplayName ?? string.Empty,
                Rarity = Rarity,
                TownNpc = TownNpc,
                Homeless = Homeless,
                HomeTileX = HomeTileX,
                HomeTileY = HomeTileY,
                Hidden = Hidden
            };
        }
    }

    internal sealed class MapRareCreatureDirectionTarget
    {
        public bool Enabled { get; set; }
        public bool Active { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string GateReason { get; set; }
        public bool HasLifeformAnalyzer { get; set; }
        public bool InfoAccessoryHidden { get; set; }
        public int WhoAmI { get; set; }
        public int Type { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public string DisplayName { get; set; }
        public int Rarity { get; set; }
        public float DistancePixels { get; set; }
        public string DistanceText { get; set; }
        public long LastScanTick { get; set; }
        public DateTime CapturedUtc { get; set; }

        public MapRareCreatureDirectionTarget()
        {
            Status = string.Empty;
            Message = string.Empty;
            GateReason = string.Empty;
            WhoAmI = -1;
            Type = 0;
            DisplayName = string.Empty;
            DistanceText = string.Empty;
            LastScanTick = -1L;
            CapturedUtc = DateTime.UtcNow;
        }

        public static MapRareCreatureDirectionTarget Disabled(string status, string message)
        {
            return new MapRareCreatureDirectionTarget
            {
                Enabled = false,
                Active = false,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                GateReason = status ?? string.Empty
            };
        }

        public MapRareCreatureDirectionTarget Clone()
        {
            return new MapRareCreatureDirectionTarget
            {
                Enabled = Enabled,
                Active = Active,
                Status = Status ?? string.Empty,
                Message = Message ?? string.Empty,
                GateReason = GateReason ?? string.Empty,
                HasLifeformAnalyzer = HasLifeformAnalyzer,
                InfoAccessoryHidden = InfoAccessoryHidden,
                WhoAmI = WhoAmI,
                Type = Type,
                CenterX = CenterX,
                CenterY = CenterY,
                DisplayName = DisplayName ?? string.Empty,
                Rarity = Rarity,
                DistancePixels = DistancePixels,
                DistanceText = DistanceText ?? string.Empty,
                LastScanTick = LastScanTick,
                CapturedUtc = CapturedUtc
            };
        }
    }

    internal sealed class MapTravellingMerchantDirectionTarget
    {
        public bool Enabled { get; set; }
        public bool Active { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public int WhoAmI { get; set; }
        public int Type { get; set; }
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public string DisplayName { get; set; }
        public string LabelLine1 { get; set; }
        public string LabelLine2 { get; set; }
        public string LabelLine3 { get; set; }
        public string TownLabel { get; set; }
        public string TownLabelSource { get; set; }
        public string TownLabelConfidence { get; set; }
        public string MatchedPylonType { get; set; }
        public double MatchedPylonDistanceTiles { get; set; }
        public int NearbyTownNpcCount { get; set; }
        public long LastScanTick { get; set; }
        public DateTime CapturedUtc { get; set; }

        public MapTravellingMerchantDirectionTarget()
        {
            Status = string.Empty;
            Message = string.Empty;
            WhoAmI = -1;
            Type = 0;
            DisplayName = string.Empty;
            LabelLine1 = string.Empty;
            LabelLine2 = string.Empty;
            LabelLine3 = string.Empty;
            TownLabel = string.Empty;
            TownLabelSource = "unknown";
            TownLabelConfidence = "none";
            MatchedPylonType = string.Empty;
            MatchedPylonDistanceTiles = -1d;
            CapturedUtc = DateTime.UtcNow;
        }

        public static MapTravellingMerchantDirectionTarget Disabled(string status, string message)
        {
            return new MapTravellingMerchantDirectionTarget
            {
                Enabled = false,
                Active = false,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                TownLabel = "环境未知",
                LabelLine1 = "旅商",
                LabelLine3 = "环境未知"
            };
        }

        public MapTravellingMerchantDirectionTarget Clone()
        {
            return new MapTravellingMerchantDirectionTarget
            {
                Enabled = Enabled,
                Active = Active,
                Status = Status ?? string.Empty,
                Message = Message ?? string.Empty,
                WhoAmI = WhoAmI,
                Type = Type,
                CenterX = CenterX,
                CenterY = CenterY,
                DisplayName = DisplayName ?? string.Empty,
                LabelLine1 = LabelLine1 ?? string.Empty,
                LabelLine2 = LabelLine2 ?? string.Empty,
                LabelLine3 = LabelLine3 ?? string.Empty,
                TownLabel = TownLabel ?? string.Empty,
                TownLabelSource = TownLabelSource ?? string.Empty,
                TownLabelConfidence = TownLabelConfidence ?? string.Empty,
                MatchedPylonType = MatchedPylonType ?? string.Empty,
                MatchedPylonDistanceTiles = MatchedPylonDistanceTiles,
                NearbyTownNpcCount = NearbyTownNpcCount,
                LastScanTick = LastScanTick,
                CapturedUtc = CapturedUtc
            };
        }
    }
}
