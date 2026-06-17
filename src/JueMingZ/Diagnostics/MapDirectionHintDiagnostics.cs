using System;
using JueMingZ.Automation.MapEnhancement;

namespace JueMingZ.Diagnostics
{
    internal static class MapDirectionHintDiagnostics
    {
        private static readonly object SyncRoot = new object();
        private static MapDirectionHintDiagnosticsSnapshot _snapshot = new MapDirectionHintDiagnosticsSnapshot();

        public static void RecordRareCreatureTarget(MapRareCreatureDirectionTarget target)
        {
            lock (SyncRoot)
            {
                target = target ?? MapRareCreatureDirectionTarget.Disabled("targetUnavailable", "rare creature target unavailable");
                _snapshot.MapRareCreatureDirectionEnabled = target.Enabled;
                _snapshot.MapRareCreatureDirectionStatus = target.Status ?? string.Empty;
                _snapshot.MapRareCreatureDirectionMessage = target.Message ?? string.Empty;
                _snapshot.MapRareCreatureDirectionGateReason = target.GateReason ?? string.Empty;
                _snapshot.MapRareCreatureDirectionHasLifeformAnalyzer = target.HasLifeformAnalyzer;
                _snapshot.MapRareCreatureDirectionInfoAccessoryHidden = target.InfoAccessoryHidden;
                _snapshot.MapRareCreatureDirectionTargetActive = target.Active;
                _snapshot.MapRareCreatureDirectionTargetWhoAmI = target.WhoAmI;
                _snapshot.MapRareCreatureDirectionTargetType = target.Type;
                _snapshot.MapRareCreatureDirectionTargetName = target.DisplayName ?? string.Empty;
                _snapshot.MapRareCreatureDirectionTargetRarity = target.Rarity;
                _snapshot.MapRareCreatureDirectionTargetWorldX = target.CenterX;
                _snapshot.MapRareCreatureDirectionTargetWorldY = target.CenterY;
                _snapshot.MapRareCreatureDirectionDistancePixels = target.DistancePixels;
                _snapshot.MapRareCreatureDirectionDistanceText = target.DistanceText ?? string.Empty;
                _snapshot.MapRareCreatureDirectionLastScanTick = target.LastScanTick;
                _snapshot.MapRareCreatureDirectionLastTargetUtc = target.CapturedUtc;
            }
        }

        public static void RecordRareCreatureProjection(
            MapRareCreatureDirectionProjection projection,
            string drawStatus)
        {
            lock (SyncRoot)
            {
                projection = projection ?? MapRareCreatureDirectionProjection.Empty(drawStatus);
                _snapshot.MapRareCreatureDirectionDrawStatus =
                    string.IsNullOrWhiteSpace(drawStatus) ? projection.Status : drawStatus;
                _snapshot.MapRareCreatureDirectionOnScreen = projection.OnScreen;
                _snapshot.MapRareCreatureDirectionShouldDrawLabel = projection.ShouldDrawLabel;
                _snapshot.MapRareCreatureDirectionArrowScreenX = projection.ArrowX;
                _snapshot.MapRareCreatureDirectionArrowScreenY = projection.ArrowY;
                _snapshot.MapRareCreatureDirectionDirectionX = projection.DirectionX;
                _snapshot.MapRareCreatureDirectionDirectionY = projection.DirectionY;
                _snapshot.MapRareCreatureDirectionArrowGlyph = projection.ArrowGlyph ?? string.Empty;
                _snapshot.MapRareCreatureDirectionLabelLine1 = projection.LabelLine1 ?? string.Empty;
                _snapshot.MapRareCreatureDirectionLabelLine2 = projection.LabelLine2 ?? string.Empty;
                _snapshot.MapRareCreatureDirectionLastDrawUtc = DateTime.UtcNow;
            }
        }

        public static void RecordTravellingMerchantTarget(MapTravellingMerchantDirectionTarget target)
        {
            lock (SyncRoot)
            {
                target = target ?? MapTravellingMerchantDirectionTarget.Disabled("targetUnavailable", "travelling merchant target unavailable");
                _snapshot.MapTravellingMerchantDirectionEnabled = target.Enabled;
                _snapshot.MapTravellingMerchantDirectionStatus = target.Status ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionMessage = target.Message ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionTargetActive = target.Active;
                _snapshot.MapTravellingMerchantDirectionTargetWhoAmI = target.WhoAmI;
                _snapshot.MapTravellingMerchantDirectionTargetType = target.Type;
                _snapshot.MapTravellingMerchantDirectionTargetName = target.DisplayName ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionTargetWorldX = target.CenterX;
                _snapshot.MapTravellingMerchantDirectionTargetWorldY = target.CenterY;
                _snapshot.MapTravellingMerchantDirectionTownLabel = target.TownLabel ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionTownLabelSource = target.TownLabelSource ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionTownLabelConfidence = target.TownLabelConfidence ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionMatchedPylonType = target.MatchedPylonType ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionMatchedPylonDistanceTiles = target.MatchedPylonDistanceTiles;
                _snapshot.MapTravellingMerchantDirectionNearbyTownNpcCount = target.NearbyTownNpcCount;
                _snapshot.MapTravellingMerchantDirectionLastScanTick = target.LastScanTick;
                _snapshot.MapTravellingMerchantDirectionLastTargetUtc = target.CapturedUtc;
            }
        }

        public static void RecordTravellingMerchantProjection(
            MapTravellingMerchantDirectionProjection projection,
            string drawStatus)
        {
            lock (SyncRoot)
            {
                projection = projection ?? MapTravellingMerchantDirectionProjection.Empty(drawStatus);
                _snapshot.MapTravellingMerchantDirectionDrawStatus =
                    string.IsNullOrWhiteSpace(drawStatus) ? projection.Status : drawStatus;
                _snapshot.MapTravellingMerchantDirectionOnScreen = projection.OnScreen;
                _snapshot.MapTravellingMerchantDirectionDistancePixels = projection.DistancePixels;
                _snapshot.MapTravellingMerchantDirectionDistanceText = projection.DistanceText ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionEdgeScreenX = projection.EdgeX;
                _snapshot.MapTravellingMerchantDirectionEdgeScreenY = projection.EdgeY;
                _snapshot.MapTravellingMerchantDirectionDirectionX = projection.DirectionX;
                _snapshot.MapTravellingMerchantDirectionDirectionY = projection.DirectionY;
                _snapshot.MapTravellingMerchantDirectionLabelLine1 = projection.LabelLine1 ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionLabelLine2 = projection.LabelLine2 ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionLabelLine3 = projection.LabelLine3 ?? string.Empty;
                _snapshot.MapTravellingMerchantDirectionLastDrawUtc = DateTime.UtcNow;
            }
        }

        public static MapDirectionHintDiagnosticsSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _snapshot.Clone();
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = new MapDirectionHintDiagnosticsSnapshot();
            }
        }
    }

    internal sealed class MapDirectionHintDiagnosticsSnapshot
    {
        public bool MapRareCreatureDirectionEnabled { get; set; }
        public string MapRareCreatureDirectionStatus { get; set; }
        public string MapRareCreatureDirectionMessage { get; set; }
        public string MapRareCreatureDirectionGateReason { get; set; }
        public bool MapRareCreatureDirectionHasLifeformAnalyzer { get; set; }
        public bool MapRareCreatureDirectionInfoAccessoryHidden { get; set; }
        public bool MapRareCreatureDirectionTargetActive { get; set; }
        public int MapRareCreatureDirectionTargetWhoAmI { get; set; }
        public int MapRareCreatureDirectionTargetType { get; set; }
        public string MapRareCreatureDirectionTargetName { get; set; }
        public int MapRareCreatureDirectionTargetRarity { get; set; }
        public double MapRareCreatureDirectionTargetWorldX { get; set; }
        public double MapRareCreatureDirectionTargetWorldY { get; set; }
        public bool MapRareCreatureDirectionOnScreen { get; set; }
        public bool MapRareCreatureDirectionShouldDrawLabel { get; set; }
        public double MapRareCreatureDirectionDistancePixels { get; set; }
        public string MapRareCreatureDirectionDistanceText { get; set; }
        public double MapRareCreatureDirectionArrowScreenX { get; set; }
        public double MapRareCreatureDirectionArrowScreenY { get; set; }
        public double MapRareCreatureDirectionDirectionX { get; set; }
        public double MapRareCreatureDirectionDirectionY { get; set; }
        public string MapRareCreatureDirectionArrowGlyph { get; set; }
        public string MapRareCreatureDirectionLabelLine1 { get; set; }
        public string MapRareCreatureDirectionLabelLine2 { get; set; }
        public long MapRareCreatureDirectionLastScanTick { get; set; }
        public string MapRareCreatureDirectionDrawStatus { get; set; }
        public DateTime? MapRareCreatureDirectionLastTargetUtc { get; set; }
        public DateTime? MapRareCreatureDirectionLastDrawUtc { get; set; }
        public bool MapTravellingMerchantDirectionEnabled { get; set; }
        public string MapTravellingMerchantDirectionStatus { get; set; }
        public string MapTravellingMerchantDirectionMessage { get; set; }
        public bool MapTravellingMerchantDirectionTargetActive { get; set; }
        public int MapTravellingMerchantDirectionTargetWhoAmI { get; set; }
        public int MapTravellingMerchantDirectionTargetType { get; set; }
        public string MapTravellingMerchantDirectionTargetName { get; set; }
        public double MapTravellingMerchantDirectionTargetWorldX { get; set; }
        public double MapTravellingMerchantDirectionTargetWorldY { get; set; }
        public bool MapTravellingMerchantDirectionOnScreen { get; set; }
        public double MapTravellingMerchantDirectionDistancePixels { get; set; }
        public string MapTravellingMerchantDirectionDistanceText { get; set; }
        public double MapTravellingMerchantDirectionEdgeScreenX { get; set; }
        public double MapTravellingMerchantDirectionEdgeScreenY { get; set; }
        public double MapTravellingMerchantDirectionDirectionX { get; set; }
        public double MapTravellingMerchantDirectionDirectionY { get; set; }
        public string MapTravellingMerchantDirectionLabelLine1 { get; set; }
        public string MapTravellingMerchantDirectionLabelLine2 { get; set; }
        public string MapTravellingMerchantDirectionLabelLine3 { get; set; }
        public string MapTravellingMerchantDirectionTownLabel { get; set; }
        public string MapTravellingMerchantDirectionTownLabelSource { get; set; }
        public string MapTravellingMerchantDirectionTownLabelConfidence { get; set; }
        public string MapTravellingMerchantDirectionMatchedPylonType { get; set; }
        public double MapTravellingMerchantDirectionMatchedPylonDistanceTiles { get; set; }
        public int MapTravellingMerchantDirectionNearbyTownNpcCount { get; set; }
        public long MapTravellingMerchantDirectionLastScanTick { get; set; }
        public string MapTravellingMerchantDirectionDrawStatus { get; set; }
        public DateTime? MapTravellingMerchantDirectionLastTargetUtc { get; set; }
        public DateTime? MapTravellingMerchantDirectionLastDrawUtc { get; set; }

        public MapDirectionHintDiagnosticsSnapshot()
        {
            MapRareCreatureDirectionStatus = string.Empty;
            MapRareCreatureDirectionMessage = string.Empty;
            MapRareCreatureDirectionGateReason = string.Empty;
            MapRareCreatureDirectionTargetWhoAmI = -1;
            MapRareCreatureDirectionTargetName = string.Empty;
            MapRareCreatureDirectionDistanceText = string.Empty;
            MapRareCreatureDirectionArrowGlyph = string.Empty;
            MapRareCreatureDirectionLabelLine1 = string.Empty;
            MapRareCreatureDirectionLabelLine2 = string.Empty;
            MapRareCreatureDirectionLastScanTick = -1L;
            MapRareCreatureDirectionDrawStatus = string.Empty;
            MapTravellingMerchantDirectionStatus = string.Empty;
            MapTravellingMerchantDirectionMessage = string.Empty;
            MapTravellingMerchantDirectionTargetWhoAmI = -1;
            MapTravellingMerchantDirectionTargetType = 0;
            MapTravellingMerchantDirectionTargetName = string.Empty;
            MapTravellingMerchantDirectionDistanceText = string.Empty;
            MapTravellingMerchantDirectionLabelLine1 = string.Empty;
            MapTravellingMerchantDirectionLabelLine2 = string.Empty;
            MapTravellingMerchantDirectionLabelLine3 = string.Empty;
            MapTravellingMerchantDirectionTownLabel = string.Empty;
            MapTravellingMerchantDirectionTownLabelSource = string.Empty;
            MapTravellingMerchantDirectionTownLabelConfidence = string.Empty;
            MapTravellingMerchantDirectionMatchedPylonType = string.Empty;
            MapTravellingMerchantDirectionMatchedPylonDistanceTiles = -1d;
            MapTravellingMerchantDirectionLastScanTick = -1L;
            MapTravellingMerchantDirectionDrawStatus = string.Empty;
        }

        public MapDirectionHintDiagnosticsSnapshot Clone()
        {
            return new MapDirectionHintDiagnosticsSnapshot
            {
                MapRareCreatureDirectionEnabled = MapRareCreatureDirectionEnabled,
                MapRareCreatureDirectionStatus = MapRareCreatureDirectionStatus ?? string.Empty,
                MapRareCreatureDirectionMessage = MapRareCreatureDirectionMessage ?? string.Empty,
                MapRareCreatureDirectionGateReason = MapRareCreatureDirectionGateReason ?? string.Empty,
                MapRareCreatureDirectionHasLifeformAnalyzer = MapRareCreatureDirectionHasLifeformAnalyzer,
                MapRareCreatureDirectionInfoAccessoryHidden = MapRareCreatureDirectionInfoAccessoryHidden,
                MapRareCreatureDirectionTargetActive = MapRareCreatureDirectionTargetActive,
                MapRareCreatureDirectionTargetWhoAmI = MapRareCreatureDirectionTargetWhoAmI,
                MapRareCreatureDirectionTargetType = MapRareCreatureDirectionTargetType,
                MapRareCreatureDirectionTargetName = MapRareCreatureDirectionTargetName ?? string.Empty,
                MapRareCreatureDirectionTargetRarity = MapRareCreatureDirectionTargetRarity,
                MapRareCreatureDirectionTargetWorldX = MapRareCreatureDirectionTargetWorldX,
                MapRareCreatureDirectionTargetWorldY = MapRareCreatureDirectionTargetWorldY,
                MapRareCreatureDirectionOnScreen = MapRareCreatureDirectionOnScreen,
                MapRareCreatureDirectionShouldDrawLabel = MapRareCreatureDirectionShouldDrawLabel,
                MapRareCreatureDirectionDistancePixels = MapRareCreatureDirectionDistancePixels,
                MapRareCreatureDirectionDistanceText = MapRareCreatureDirectionDistanceText ?? string.Empty,
                MapRareCreatureDirectionArrowScreenX = MapRareCreatureDirectionArrowScreenX,
                MapRareCreatureDirectionArrowScreenY = MapRareCreatureDirectionArrowScreenY,
                MapRareCreatureDirectionDirectionX = MapRareCreatureDirectionDirectionX,
                MapRareCreatureDirectionDirectionY = MapRareCreatureDirectionDirectionY,
                MapRareCreatureDirectionArrowGlyph = MapRareCreatureDirectionArrowGlyph ?? string.Empty,
                MapRareCreatureDirectionLabelLine1 = MapRareCreatureDirectionLabelLine1 ?? string.Empty,
                MapRareCreatureDirectionLabelLine2 = MapRareCreatureDirectionLabelLine2 ?? string.Empty,
                MapRareCreatureDirectionLastScanTick = MapRareCreatureDirectionLastScanTick,
                MapRareCreatureDirectionDrawStatus = MapRareCreatureDirectionDrawStatus ?? string.Empty,
                MapRareCreatureDirectionLastTargetUtc = MapRareCreatureDirectionLastTargetUtc,
                MapRareCreatureDirectionLastDrawUtc = MapRareCreatureDirectionLastDrawUtc,
                MapTravellingMerchantDirectionEnabled = MapTravellingMerchantDirectionEnabled,
                MapTravellingMerchantDirectionStatus = MapTravellingMerchantDirectionStatus ?? string.Empty,
                MapTravellingMerchantDirectionMessage = MapTravellingMerchantDirectionMessage ?? string.Empty,
                MapTravellingMerchantDirectionTargetActive = MapTravellingMerchantDirectionTargetActive,
                MapTravellingMerchantDirectionTargetWhoAmI = MapTravellingMerchantDirectionTargetWhoAmI,
                MapTravellingMerchantDirectionTargetType = MapTravellingMerchantDirectionTargetType,
                MapTravellingMerchantDirectionTargetName = MapTravellingMerchantDirectionTargetName ?? string.Empty,
                MapTravellingMerchantDirectionTargetWorldX = MapTravellingMerchantDirectionTargetWorldX,
                MapTravellingMerchantDirectionTargetWorldY = MapTravellingMerchantDirectionTargetWorldY,
                MapTravellingMerchantDirectionOnScreen = MapTravellingMerchantDirectionOnScreen,
                MapTravellingMerchantDirectionDistancePixels = MapTravellingMerchantDirectionDistancePixels,
                MapTravellingMerchantDirectionDistanceText = MapTravellingMerchantDirectionDistanceText ?? string.Empty,
                MapTravellingMerchantDirectionEdgeScreenX = MapTravellingMerchantDirectionEdgeScreenX,
                MapTravellingMerchantDirectionEdgeScreenY = MapTravellingMerchantDirectionEdgeScreenY,
                MapTravellingMerchantDirectionDirectionX = MapTravellingMerchantDirectionDirectionX,
                MapTravellingMerchantDirectionDirectionY = MapTravellingMerchantDirectionDirectionY,
                MapTravellingMerchantDirectionLabelLine1 = MapTravellingMerchantDirectionLabelLine1 ?? string.Empty,
                MapTravellingMerchantDirectionLabelLine2 = MapTravellingMerchantDirectionLabelLine2 ?? string.Empty,
                MapTravellingMerchantDirectionLabelLine3 = MapTravellingMerchantDirectionLabelLine3 ?? string.Empty,
                MapTravellingMerchantDirectionTownLabel = MapTravellingMerchantDirectionTownLabel ?? string.Empty,
                MapTravellingMerchantDirectionTownLabelSource = MapTravellingMerchantDirectionTownLabelSource ?? string.Empty,
                MapTravellingMerchantDirectionTownLabelConfidence = MapTravellingMerchantDirectionTownLabelConfidence ?? string.Empty,
                MapTravellingMerchantDirectionMatchedPylonType = MapTravellingMerchantDirectionMatchedPylonType ?? string.Empty,
                MapTravellingMerchantDirectionMatchedPylonDistanceTiles = MapTravellingMerchantDirectionMatchedPylonDistanceTiles,
                MapTravellingMerchantDirectionNearbyTownNpcCount = MapTravellingMerchantDirectionNearbyTownNpcCount,
                MapTravellingMerchantDirectionLastScanTick = MapTravellingMerchantDirectionLastScanTick,
                MapTravellingMerchantDirectionDrawStatus = MapTravellingMerchantDirectionDrawStatus ?? string.Empty,
                MapTravellingMerchantDirectionLastTargetUtc = MapTravellingMerchantDirectionLastTargetUtc,
                MapTravellingMerchantDirectionLastDrawUtc = MapTravellingMerchantDirectionLastDrawUtc
            };
        }
    }
}
