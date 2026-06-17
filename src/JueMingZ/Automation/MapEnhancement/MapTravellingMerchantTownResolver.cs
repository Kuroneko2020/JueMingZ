using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapTravellingMerchantTownResolver
    {
        internal const string SourcePylon = "pylon";
        internal const string SourceTownCluster = "townCluster";
        internal const string SourcePointBiome = "pointBiome";
        internal const string SourceUnknown = "unknown";

        private const double PylonMatchMaxDistanceTiles = 120d;
        private const double TownClusterMaxDistanceTiles = 120d;
        private const double TownNpcHomeMaxDistanceTiles = 100d;
        private const int RequiredTownNpcClusterCount = 2;
        private const int CoastTileDistance = 380;
        private const int UnderworldTileDistanceFromBottom = 200;
        private const int SurfaceBufferTiles = 40;

        private static readonly object SyncRoot = new object();
        private static readonly IMapTravellingMerchantPylonProvider DefaultPylonProvider =
            new TerrariaPylonProvider();
        private static IMapTravellingMerchantPylonProvider _pylonProviderForTesting;

        public static MapTravellingMerchantTownLabelResult Resolve(
            MapDirectionHintNpcObservation merchant,
            MapDirectionHintNpcObservation[] observations,
            MapTravellingMerchantWorldContext worldContext)
        {
            MapTravellingMerchantPylonSnapshot[] pylons;
            string pylonMessage;
            var provider = GetPylonProvider();
            if (!provider.TryRead(out pylons, out pylonMessage))
            {
                pylons = new MapTravellingMerchantPylonSnapshot[0];
            }

            return ResolveForTesting(merchant, observations, pylons, worldContext);
        }

        internal static MapTravellingMerchantTownLabelResult ResolveForTesting(
            MapDirectionHintNpcObservation merchant,
            MapDirectionHintNpcObservation[] observations,
            MapTravellingMerchantPylonSnapshot[] pylons,
            MapTravellingMerchantWorldContext worldContext)
        {
            if (merchant == null || !merchant.Active)
            {
                return Unknown("merchantUnavailable");
            }

            var merchantTileX = merchant.CenterX / 16f;
            var merchantTileY = merchant.CenterY / 16f;
            if (!IsFinite(merchantTileX) || !IsFinite(merchantTileY))
            {
                return Unknown("merchantPositionUnavailable");
            }

            MapTravellingMerchantPylonSnapshot matchedPylon;
            double pylonDistance;
            if (TryFindNearestPylon(merchantTileX, merchantTileY, pylons, out matchedPylon, out pylonDistance))
            {
                var pylonLabel = PylonTypeToTownLabel(matchedPylon.PylonType);
                if (!string.IsNullOrWhiteSpace(pylonLabel))
                {
                    return new MapTravellingMerchantTownLabelResult
                    {
                        Label = pylonLabel,
                        Source = SourcePylon,
                        Confidence = "high",
                        MatchedPylonType = PylonTypeToName(matchedPylon.PylonType),
                        MatchedPylonDistanceTiles = pylonDistance,
                        NearbyTownNpcCount = CountNearbyTownNpcs(merchantTileX, merchantTileY, observations)
                    };
                }
            }

            var pointBiome = InferPointBiome(merchantTileX, merchantTileY, worldContext);
            var nearbyTownNpcCount = CountNearbyTownNpcs(merchantTileX, merchantTileY, observations);
            if (nearbyTownNpcCount >= RequiredTownNpcClusterCount && pointBiome.HasLabel)
            {
                return new MapTravellingMerchantTownLabelResult
                {
                    Label = pointBiome.TownLabel,
                    Source = SourceTownCluster,
                    Confidence = "medium",
                    MatchedPylonType = string.Empty,
                    MatchedPylonDistanceTiles = -1d,
                    NearbyTownNpcCount = nearbyTownNpcCount
                };
            }

            if (pointBiome.HasLabel)
            {
                return new MapTravellingMerchantTownLabelResult
                {
                    Label = pointBiome.NearbyLabel,
                    Source = SourcePointBiome,
                    Confidence = "low",
                    MatchedPylonType = string.Empty,
                    MatchedPylonDistanceTiles = -1d,
                    NearbyTownNpcCount = nearbyTownNpcCount
                };
            }

            return Unknown("locationEvidenceUnavailable", nearbyTownNpcCount);
        }

        internal static void SetPylonProviderForTesting(IMapTravellingMerchantPylonProvider provider)
        {
            lock (SyncRoot)
            {
                _pylonProviderForTesting = provider;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _pylonProviderForTesting = null;
            }
        }

        internal static string PylonTypeToTownLabelForTesting(int pylonType)
        {
            return PylonTypeToTownLabel(pylonType);
        }

        private static IMapTravellingMerchantPylonProvider GetPylonProvider()
        {
            lock (SyncRoot)
            {
                return _pylonProviderForTesting ?? DefaultPylonProvider;
            }
        }

        private static bool TryFindNearestPylon(
            float merchantTileX,
            float merchantTileY,
            MapTravellingMerchantPylonSnapshot[] pylons,
            out MapTravellingMerchantPylonSnapshot matched,
            out double distanceTiles)
        {
            matched = null;
            distanceTiles = -1d;
            if (pylons == null || pylons.Length == 0)
            {
                return false;
            }

            for (var index = 0; index < pylons.Length; index++)
            {
                var pylon = pylons[index];
                if (pylon == null)
                {
                    continue;
                }

                var label = PylonTypeToTownLabel(pylon.PylonType);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                var distance = Distance(merchantTileX, merchantTileY, pylon.TileX, pylon.TileY);
                if (distance > PylonMatchMaxDistanceTiles)
                {
                    continue;
                }

                if (matched == null || distance < distanceTiles)
                {
                    matched = pylon;
                    distanceTiles = distance;
                }
            }

            return matched != null;
        }

        private static int CountNearbyTownNpcs(
            float merchantTileX,
            float merchantTileY,
            MapDirectionHintNpcObservation[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < observations.Length; index++)
            {
                var npc = observations[index];
                if (npc == null ||
                    !npc.Active ||
                    npc.Type == MapTravellingMerchantDirectionTargetResolver.TravellingMerchantNpcType ||
                    !npc.TownNpc ||
                    npc.Homeless ||
                    npc.HomeTileX < 0 ||
                    npc.HomeTileY < 0)
                {
                    continue;
                }

                var npcTileX = npc.CenterX / 16f;
                var npcTileY = npc.CenterY / 16f;
                if (Distance(npcTileX, npcTileY, npc.HomeTileX, npc.HomeTileY) > TownNpcHomeMaxDistanceTiles)
                {
                    continue;
                }

                if (Distance(merchantTileX, merchantTileY, npc.HomeTileX, npc.HomeTileY) <= TownClusterMaxDistanceTiles)
                {
                    count++;
                }
            }

            return count;
        }

        private static PointBiomeLabel InferPointBiome(
            float merchantTileX,
            float merchantTileY,
            MapTravellingMerchantWorldContext worldContext)
        {
            if (worldContext == null || !worldContext.HasWorldFacts)
            {
                return PointBiomeLabel.None;
            }

            var maxTilesX = Math.Max(1, worldContext.MaxTilesX);
            var maxTilesY = Math.Max(1, worldContext.MaxTilesY);
            var worldSurface = worldContext.WorldSurfaceTileY;
            if (worldSurface <= 0d || worldSurface >= maxTilesY)
            {
                return PointBiomeLabel.None;
            }

            if (merchantTileY >= maxTilesY - UnderworldTileDistanceFromBottom)
            {
                return new PointBiomeLabel("地狱城镇", "地狱附近");
            }

            if ((merchantTileX <= CoastTileDistance || merchantTileX >= maxTilesX - CoastTileDistance) &&
                merchantTileY <= worldSurface + SurfaceBufferTiles)
            {
                return new PointBiomeLabel("海洋城镇", "海洋附近");
            }

            if (merchantTileY > worldSurface + SurfaceBufferTiles)
            {
                return new PointBiomeLabel("地下城镇", "地下附近");
            }

            return new PointBiomeLabel("森林城镇", "森林附近");
        }

        private static string PylonTypeToTownLabel(int pylonType)
        {
            switch (pylonType)
            {
                case 0:
                    return "森林城镇";
                case 1:
                    return "丛林城镇";
                case 2:
                    return "神圣城镇";
                case 3:
                    return "地下城镇";
                case 4:
                    return "海洋城镇";
                case 5:
                    return "沙漠城镇";
                case 6:
                    return "雪原城镇";
                case 7:
                    return "蘑菇城镇";
                case 8:
                    return "万能城镇";
                case 9:
                    return "地狱城镇";
                case 10:
                    return "微光城镇";
                default:
                    return string.Empty;
            }
        }

        private static string PylonTypeToName(int pylonType)
        {
            switch (pylonType)
            {
                case 0:
                    return "SurfacePurity";
                case 1:
                    return "Jungle";
                case 2:
                    return "Hallow";
                case 3:
                    return "Underground";
                case 4:
                    return "Beach";
                case 5:
                    return "Desert";
                case 6:
                    return "Snow";
                case 7:
                    return "GlowingMushroom";
                case 8:
                    return "Victory";
                case 9:
                    return "Underworld";
                case 10:
                    return "Shimmer";
                default:
                    return string.Empty;
            }
        }

        private static MapTravellingMerchantTownLabelResult Unknown(string message)
        {
            return Unknown(message, 0);
        }

        private static MapTravellingMerchantTownLabelResult Unknown(string message, int nearbyTownNpcCount)
        {
            return new MapTravellingMerchantTownLabelResult
            {
                Label = "环境未知",
                Source = SourceUnknown,
                Confidence = "none",
                MatchedPylonType = string.Empty,
                MatchedPylonDistanceTiles = -1d,
                NearbyTownNpcCount = nearbyTownNpcCount,
                Message = message ?? string.Empty
            };
        }

        private static double Distance(double leftX, double leftY, double rightX, double rightY)
        {
            var dx = leftX - rightX;
            var dy = leftY - rightY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class TerrariaPylonProvider : IMapTravellingMerchantPylonProvider
        {
            public bool TryRead(out MapTravellingMerchantPylonSnapshot[] pylons, out string message)
            {
                pylons = new MapTravellingMerchantPylonSnapshot[0];
                message = string.Empty;

                try
                {
                    var pylonSystem = Main.PylonSystem;
                    if (pylonSystem == null || pylonSystem.Pylons == null)
                    {
                        message = "pylonSystemUnavailable";
                        return false;
                    }

                    var source = pylonSystem.Pylons;
                    var result = new List<MapTravellingMerchantPylonSnapshot>(source.Count);
                    for (var index = 0; index < source.Count; index++)
                    {
                        var pylon = source[index];
                        result.Add(new MapTravellingMerchantPylonSnapshot
                        {
                            TileX = pylon.PositionInTiles.X,
                            TileY = pylon.PositionInTiles.Y,
                            PylonType = (int)pylon.TypeOfPylon
                        });
                    }

                    pylons = result.ToArray();
                    return true;
                }
                catch (Exception error)
                {
                    message = "pylonReadFailed:" + error.Message;
                    return false;
                }
            }
        }

        private struct PointBiomeLabel
        {
            public static readonly PointBiomeLabel None = new PointBiomeLabel(string.Empty, string.Empty);

            public readonly string TownLabel;
            public readonly string NearbyLabel;

            public PointBiomeLabel(string townLabel, string nearbyLabel)
            {
                TownLabel = townLabel ?? string.Empty;
                NearbyLabel = nearbyLabel ?? string.Empty;
            }

            public bool HasLabel
            {
                get { return !string.IsNullOrWhiteSpace(TownLabel) && !string.IsNullOrWhiteSpace(NearbyLabel); }
            }
        }
    }

    internal interface IMapTravellingMerchantPylonProvider
    {
        bool TryRead(out MapTravellingMerchantPylonSnapshot[] pylons, out string message);
    }

    internal sealed class MapTravellingMerchantPylonSnapshot
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int PylonType { get; set; }
    }

    internal sealed class MapTravellingMerchantWorldContext
    {
        public bool HasWorldFacts { get; set; }
        public int MaxTilesX { get; set; }
        public int MaxTilesY { get; set; }
        public double WorldSurfaceTileY { get; set; }

        public static MapTravellingMerchantWorldContext Unavailable()
        {
            return new MapTravellingMerchantWorldContext();
        }

        public static MapTravellingMerchantWorldContext FromRuntime()
        {
            try
            {
                var maxTilesX = TerrariaMainCompat.MaxTilesX;
                var maxTilesY = TerrariaMainCompat.MaxTilesY;
                var worldSurface = TerrariaMainCompat.WorldSurfaceTileY;
                return new MapTravellingMerchantWorldContext
                {
                    HasWorldFacts = maxTilesX > 0 && maxTilesY > 0 && worldSurface > 0d,
                    MaxTilesX = maxTilesX,
                    MaxTilesY = maxTilesY,
                    WorldSurfaceTileY = worldSurface
                };
            }
            catch
            {
                return Unavailable();
            }
        }
    }

    internal sealed class MapTravellingMerchantTownLabelResult
    {
        public string Label { get; set; }
        public string Source { get; set; }
        public string Confidence { get; set; }
        public string MatchedPylonType { get; set; }
        public double MatchedPylonDistanceTiles { get; set; }
        public int NearbyTownNpcCount { get; set; }
        public string Message { get; set; }

        public MapTravellingMerchantTownLabelResult()
        {
            Label = "环境未知";
            Source = MapTravellingMerchantTownResolver.SourceUnknown;
            Confidence = "none";
            MatchedPylonType = string.Empty;
            MatchedPylonDistanceTiles = -1d;
            Message = string.Empty;
        }
    }
}
