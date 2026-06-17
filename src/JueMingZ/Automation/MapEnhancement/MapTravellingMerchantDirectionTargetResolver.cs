using System;
using Microsoft.Xna.Framework;
using Terraria.ID;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapTravellingMerchantDirectionTargetResolver
    {
        internal const int TravellingMerchantNpcType = NPCID.TravellingMerchant;

        public static MapTravellingMerchantDirectionTarget Resolve(
            bool enabled,
            MapDirectionHintNpcObservation[] observations,
            MapTravellingMerchantWorldContext worldContext,
            long scanTick)
        {
            if (!enabled)
            {
                return MapTravellingMerchantDirectionTarget.Disabled(
                    "disabled",
                    "travelling merchant direction is disabled");
            }

            MapDirectionHintNpcObservation merchant;
            if (!TryFindMerchant(observations, out merchant))
            {
                return new MapTravellingMerchantDirectionTarget
                {
                    Enabled = true,
                    Active = false,
                    Status = "targetUnavailable",
                    Message = "travelling merchant NPC was not visible to this client",
                    LabelLine1 = "旅商",
                    LabelLine3 = "环境未知",
                    TownLabel = "环境未知",
                    TownLabelSource = MapTravellingMerchantTownResolver.SourceUnknown,
                    TownLabelConfidence = "none",
                    LastScanTick = Math.Max(0L, scanTick)
                };
            }

            var town = MapTravellingMerchantTownResolver.Resolve(merchant, observations, worldContext);
            return BuildTargetForTesting(merchant, town, scanTick);
        }

        internal static MapTravellingMerchantDirectionTarget BuildTargetForTesting(
            MapDirectionHintNpcObservation merchant,
            MapTravellingMerchantTownLabelResult town,
            long scanTick)
        {
            if (merchant == null)
            {
                return new MapTravellingMerchantDirectionTarget
                {
                    Enabled = true,
                    Active = false,
                    Status = "targetUnavailable",
                    Message = "travelling merchant NPC was not visible to this client",
                    LabelLine1 = "旅商",
                    LabelLine3 = "环境未知",
                    TownLabel = "环境未知",
                    TownLabelSource = MapTravellingMerchantTownResolver.SourceUnknown,
                    TownLabelConfidence = "none",
                    LastScanTick = Math.Max(0L, scanTick)
                };
            }

            town = town ?? new MapTravellingMerchantTownLabelResult();
            return new MapTravellingMerchantDirectionTarget
            {
                Enabled = true,
                Active = true,
                Status = "targetReady",
                Message = "travelling merchant target resolved",
                WhoAmI = merchant.WhoAmI,
                Type = merchant.Type,
                CenterX = merchant.CenterX,
                CenterY = merchant.CenterY,
                DisplayName = string.IsNullOrWhiteSpace(merchant.DisplayName) ? "旅商" : merchant.DisplayName,
                LabelLine1 = "旅商",
                LabelLine2 = string.Empty,
                LabelLine3 = string.IsNullOrWhiteSpace(town.Label) ? "环境未知" : town.Label,
                TownLabel = string.IsNullOrWhiteSpace(town.Label) ? "环境未知" : town.Label,
                TownLabelSource = string.IsNullOrWhiteSpace(town.Source)
                    ? MapTravellingMerchantTownResolver.SourceUnknown
                    : town.Source,
                TownLabelConfidence = string.IsNullOrWhiteSpace(town.Confidence) ? "none" : town.Confidence,
                MatchedPylonType = town.MatchedPylonType ?? string.Empty,
                MatchedPylonDistanceTiles = town.MatchedPylonDistanceTiles,
                NearbyTownNpcCount = town.NearbyTownNpcCount,
                LastScanTick = Math.Max(0L, scanTick),
                CapturedUtc = DateTime.UtcNow
            };
        }

        internal static bool TryBuildProjectionForTesting(
            MapTravellingMerchantDirectionTarget target,
            MapDirectionHintScreenContext screen,
            out MapTravellingMerchantDirectionProjection projection)
        {
            return TryBuildProjectionForTesting(
                MapTravellingMerchantDirectionRenderTarget.FromTarget(target),
                screen,
                out projection);
        }

        internal static bool TryBuildProjectionForTesting(
            MapTravellingMerchantDirectionRenderTarget target,
            MapDirectionHintScreenContext screen,
            out MapTravellingMerchantDirectionProjection projection)
        {
            projection = MapTravellingMerchantDirectionProjection.Empty("targetUnavailable");
            if (!target.Enabled)
            {
                projection = MapTravellingMerchantDirectionProjection.Empty("disabled");
                return false;
            }

            if (!target.Active)
            {
                projection = MapTravellingMerchantDirectionProjection.Empty("targetUnavailable");
                return false;
            }

            if (screen == null || !screen.IsValid)
            {
                projection = MapTravellingMerchantDirectionProjection.Empty("screenUnavailable");
                return false;
            }

            var screenPosition = new Vector2(screen.ScreenX, screen.ScreenY);
            var targetWorld = new Vector2(target.CenterX, target.CenterY);
            var targetScreen = JueMingZ.UI.MapDirectionHintProjection.WorldToScreen(targetWorld, screenPosition);
            var onScreen = JueMingZ.UI.MapDirectionHintProjection.IsScreenPointOnScreen(
                targetScreen,
                new Rectangle(0, 0, Math.Max(1, screen.ScreenWidth), Math.Max(1, screen.ScreenHeight)),
                16);
            var player = new Vector2(screen.PlayerCenterX, screen.PlayerCenterY);
            var distancePixels = Distance(player, targetWorld);
            var distanceText = JueMingZ.UI.MapDirectionHintProjection.FormatApproxTileDistance(distancePixels);
            var edge = JueMingZ.UI.MapDirectionHintProjection.ClampToEllipseEdge(
                new Rectangle(0, 0, Math.Max(1, screen.ScreenWidth), Math.Max(1, screen.ScreenHeight)),
                targetScreen,
                48f,
                42f);

            projection = new MapTravellingMerchantDirectionProjection
            {
                Status = onScreen ? "onScreenHidden" : "edgeLabelReady",
                OnScreen = onScreen,
                ShouldDraw = !onScreen && edge.HasDirection,
                DistancePixels = distancePixels,
                DistanceText = distanceText,
                EdgeX = edge.Position.X,
                EdgeY = edge.Position.Y,
                DirectionX = edge.Direction.X,
                DirectionY = edge.Direction.Y,
                LabelLine1 = string.IsNullOrWhiteSpace(target.LabelLine1) ? "旅商" : target.LabelLine1,
                LabelLine2 = distanceText,
                LabelLine3 = string.IsNullOrWhiteSpace(target.LabelLine3) ? "环境未知" : target.LabelLine3
            };

            return true;
        }

        private static bool TryFindMerchant(
            MapDirectionHintNpcObservation[] observations,
            out MapDirectionHintNpcObservation merchant)
        {
            merchant = null;
            if (observations == null || observations.Length == 0)
            {
                return false;
            }

            for (var index = 0; index < observations.Length; index++)
            {
                var npc = observations[index];
                if (npc == null ||
                    !npc.Active ||
                    npc.Hidden ||
                    npc.Type != TravellingMerchantNpcType)
                {
                    continue;
                }

                if (merchant == null ||
                    StableWhoAmI(npc) < StableWhoAmI(merchant))
                {
                    merchant = npc;
                }
            }

            return merchant != null;
        }

        private static int StableWhoAmI(MapDirectionHintNpcObservation npc)
        {
            return npc == null || npc.WhoAmI < 0 ? int.MaxValue : npc.WhoAmI;
        }

        private static float Distance(Vector2 left, Vector2 right)
        {
            if (!IsFinite(left) || !IsFinite(right))
            {
                return 0f;
            }

            return Vector2.Distance(left, right);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.X) &&
                   !float.IsInfinity(value.X) &&
                   !float.IsNaN(value.Y) &&
                   !float.IsInfinity(value.Y);
        }
    }

    internal sealed class MapDirectionHintScreenContext
    {
        public float ScreenX { get; set; }
        public float ScreenY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }

        public bool IsValid
        {
            get
            {
                return ScreenWidth > 0 &&
                       ScreenHeight > 0 &&
                       IsFinite(ScreenX) &&
                       IsFinite(ScreenY) &&
                       IsFinite(PlayerCenterX) &&
                       IsFinite(PlayerCenterY);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    internal sealed class MapTravellingMerchantDirectionProjection
    {
        public string Status { get; set; }
        public bool OnScreen { get; set; }
        public bool ShouldDraw { get; set; }
        public float DistancePixels { get; set; }
        public string DistanceText { get; set; }
        public float EdgeX { get; set; }
        public float EdgeY { get; set; }
        public float DirectionX { get; set; }
        public float DirectionY { get; set; }
        public string LabelLine1 { get; set; }
        public string LabelLine2 { get; set; }
        public string LabelLine3 { get; set; }

        public MapTravellingMerchantDirectionProjection()
        {
            Status = string.Empty;
            DistanceText = string.Empty;
            LabelLine1 = string.Empty;
            LabelLine2 = string.Empty;
            LabelLine3 = string.Empty;
        }

        public static MapTravellingMerchantDirectionProjection Empty(string status)
        {
            return new MapTravellingMerchantDirectionProjection
            {
                Status = status ?? string.Empty
            };
        }
    }
}
