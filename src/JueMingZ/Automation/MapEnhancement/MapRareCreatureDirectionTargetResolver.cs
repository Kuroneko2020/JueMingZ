using System;
using JueMingZ.Compat;
using JueMingZ.GameState;
using Microsoft.Xna.Framework;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapRareCreatureDirectionTargetResolver
    {
        internal const float MaxDistancePixels = 1300f;
        internal const int LifeformAnalyzerInfoIndex = 11;

        public static MapRareCreatureDirectionTarget Resolve(
            bool enabled,
            MapDirectionHintNpcObservation[] observations,
            MapRareCreatureDirectionPlayerContext playerContext,
            long scanTick)
        {
            if (!enabled)
            {
                return MapRareCreatureDirectionTarget.Disabled(
                    "disabled",
                    "rare creature direction is disabled");
            }

            playerContext = playerContext ?? MapRareCreatureDirectionPlayerContext.Unavailable("player context unavailable");
            if (!playerContext.Available)
            {
                return GateBlocked(
                    "playerUnavailable",
                    playerContext.Message,
                    playerContext,
                    scanTick);
            }

            if (!playerContext.HasLifeformAnalyzer)
            {
                return GateBlocked(
                    "lifeformAnalyzerMissing",
                    "lifeform analyzer capability is not available",
                    playerContext,
                    scanTick);
            }

            if (playerContext.InfoAccessoryHidden)
            {
                return GateBlocked(
                    "infoAccessoryHidden",
                    "lifeform analyzer info accessory is hidden",
                    playerContext,
                    scanTick);
            }

            MapDirectionHintNpcObservation rare;
            float distancePixels;
            if (!TryFindRareCreature(observations, playerContext, out rare, out distancePixels))
            {
                return new MapRareCreatureDirectionTarget
                {
                    Enabled = true,
                    Active = false,
                    Status = "targetUnavailable",
                    Message = "no rare creature target visible within 1300 pixels",
                    GateReason = "ready",
                    HasLifeformAnalyzer = true,
                    InfoAccessoryHidden = false,
                    LastScanTick = Math.Max(0L, scanTick),
                    CapturedUtc = DateTime.UtcNow
                };
            }

            return new MapRareCreatureDirectionTarget
            {
                Enabled = true,
                Active = true,
                Status = "targetReady",
                Message = "rare creature target resolved",
                GateReason = "ready",
                HasLifeformAnalyzer = true,
                InfoAccessoryHidden = false,
                WhoAmI = rare.WhoAmI,
                Type = rare.Type,
                CenterX = rare.CenterX,
                CenterY = rare.CenterY,
                DisplayName = string.IsNullOrWhiteSpace(rare.DisplayName) ? "稀有生物" : rare.DisplayName,
                Rarity = rare.Rarity,
                DistancePixels = distancePixels,
                DistanceText = JueMingZ.UI.MapDirectionHintProjection.FormatApproxTileDistance(distancePixels),
                LastScanTick = Math.Max(0L, scanTick),
                CapturedUtc = DateTime.UtcNow
            };
        }

        internal static bool TryBuildProjectionForTesting(
            MapRareCreatureDirectionTarget target,
            MapDirectionHintScreenContext screen,
            out MapRareCreatureDirectionProjection projection)
        {
            return TryBuildProjectionForTesting(
                MapRareCreatureDirectionRenderTarget.FromTarget(target),
                screen,
                out projection);
        }

        internal static bool TryBuildProjectionForTesting(
            MapRareCreatureDirectionRenderTarget target,
            MapDirectionHintScreenContext screen,
            out MapRareCreatureDirectionProjection projection)
        {
            projection = MapRareCreatureDirectionProjection.Empty("targetUnavailable");
            if (!target.Enabled)
            {
                projection = MapRareCreatureDirectionProjection.Empty("disabled");
                return false;
            }

            if (!target.Active)
            {
                projection = MapRareCreatureDirectionProjection.Empty(target.Status);
                return false;
            }

            if (screen == null || !screen.IsValid)
            {
                projection = MapRareCreatureDirectionProjection.Empty("screenUnavailable");
                return false;
            }

            var screenPosition = new Vector2(screen.ScreenX, screen.ScreenY);
            var targetWorld = new Vector2(target.CenterX, target.CenterY);
            var targetScreen = JueMingZ.UI.MapDirectionHintProjection.WorldToScreen(targetWorld, screenPosition);
            var playerScreen = JueMingZ.UI.MapDirectionHintProjection.WorldToScreen(
                new Vector2(screen.PlayerCenterX, screen.PlayerCenterY),
                screenPosition);
            var onScreen = JueMingZ.UI.MapDirectionHintProjection.IsScreenPointOnScreen(
                targetScreen,
                new Rectangle(0, 0, Math.Max(1, screen.ScreenWidth), Math.Max(1, screen.ScreenHeight)),
                16);
            var anchor = JueMingZ.UI.MapDirectionHintProjection.BuildPlayerArrowAnchor(
                playerScreen,
                targetScreen,
                46f);

            projection = new MapRareCreatureDirectionProjection
            {
                Status = onScreen ? "onScreenArrowOnly" : "offScreenArrowLabelReady",
                OnScreen = onScreen,
                ShouldDraw = anchor.HasDirection,
                ShouldDrawLabel = !onScreen && anchor.HasDirection,
                DistancePixels = target.DistancePixels,
                DistanceText = string.IsNullOrWhiteSpace(target.DistanceText)
                    ? JueMingZ.UI.MapDirectionHintProjection.FormatApproxTileDistance(target.DistancePixels)
                    : target.DistanceText,
                ArrowX = anchor.Position.X,
                ArrowY = anchor.Position.Y,
                DirectionX = anchor.Direction.X,
                DirectionY = anchor.Direction.Y,
                ArrowGlyph = DirectionToGlyph(anchor.Direction),
                LabelLine1 = string.IsNullOrWhiteSpace(target.DisplayName) ? "稀有生物" : target.DisplayName,
                LabelLine2 = string.IsNullOrWhiteSpace(target.DistanceText)
                    ? JueMingZ.UI.MapDirectionHintProjection.FormatApproxTileDistance(target.DistancePixels)
                    : target.DistanceText,
                TargetRarity = target.Rarity
            };

            return anchor.HasDirection;
        }

        private static MapRareCreatureDirectionTarget GateBlocked(
            string reason,
            string message,
            MapRareCreatureDirectionPlayerContext playerContext,
            long scanTick)
        {
            return new MapRareCreatureDirectionTarget
            {
                Enabled = true,
                Active = false,
                Status = "gateBlocked",
                Message = message ?? string.Empty,
                GateReason = reason ?? string.Empty,
                HasLifeformAnalyzer = playerContext != null && playerContext.HasLifeformAnalyzer,
                InfoAccessoryHidden = playerContext == null || playerContext.InfoAccessoryHidden,
                LastScanTick = Math.Max(0L, scanTick),
                CapturedUtc = DateTime.UtcNow
            };
        }

        private static bool TryFindRareCreature(
            MapDirectionHintNpcObservation[] observations,
            MapRareCreatureDirectionPlayerContext playerContext,
            out MapDirectionHintNpcObservation rare,
            out float distancePixels)
        {
            rare = null;
            distancePixels = 0f;
            if (observations == null || observations.Length == 0 || playerContext == null)
            {
                return false;
            }

            var player = new Vector2(playerContext.PlayerCenterX, playerContext.PlayerCenterY);
            var maxDistanceSquared = MaxDistancePixels * MaxDistancePixels;
            var bestDistanceSquared = float.MaxValue;
            for (var index = 0; index < observations.Length; index++)
            {
                var npc = observations[index];
                if (npc == null ||
                    !npc.Active ||
                    npc.Hidden ||
                    npc.Rarity <= 0)
                {
                    continue;
                }

                var target = new Vector2(npc.CenterX, npc.CenterY);
                var distanceSquared = DistanceSquared(player, target);
                if (distanceSquared < 0f || distanceSquared >= maxDistanceSquared)
                {
                    continue;
                }

                if (rare == null ||
                    npc.Rarity > rare.Rarity ||
                    (npc.Rarity == rare.Rarity && distanceSquared < bestDistanceSquared - 0.001f))
                {
                    rare = npc;
                    bestDistanceSquared = distanceSquared;
                }
            }

            if (rare == null)
            {
                return false;
            }

            distancePixels = (float)Math.Sqrt(bestDistanceSquared);
            return true;
        }

        private static float DistanceSquared(Vector2 left, Vector2 right)
        {
            if (!IsFinite(left) || !IsFinite(right))
            {
                return -1f;
            }

            var delta = right - left;
            return delta.X * delta.X + delta.Y * delta.Y;
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.X) &&
                   !float.IsInfinity(value.X) &&
                   !float.IsNaN(value.Y) &&
                   !float.IsInfinity(value.Y);
        }

        private static string DirectionToGlyph(Vector2 direction)
        {
            if (!IsFinite(direction) || direction.LengthSquared() <= 0.0001f)
            {
                return "→";
            }

            var angle = Math.Atan2(direction.Y, direction.X);
            var sector = ((int)Math.Round(angle / (Math.PI / 4d), MidpointRounding.AwayFromZero) + 8) % 8;
            switch (sector)
            {
                case 0:
                    return "→";
                case 1:
                    return "↘";
                case 2:
                    return "↓";
                case 3:
                    return "↙";
                case 4:
                    return "←";
                case 5:
                    return "↖";
                case 6:
                    return "↑";
                default:
                    return "↗";
            }
        }
    }

    internal sealed class MapRareCreatureDirectionPlayerContext
    {
        public bool Available { get; set; }
        public string Message { get; set; }
        public bool HasLifeformAnalyzer { get; set; }
        public bool InfoAccessoryHidden { get; set; }
        public float PlayerCenterX { get; set; }
        public float PlayerCenterY { get; set; }

        public MapRareCreatureDirectionPlayerContext()
        {
            Message = string.Empty;
            InfoAccessoryHidden = true;
        }

        public static MapRareCreatureDirectionPlayerContext Unavailable(string message)
        {
            return new MapRareCreatureDirectionPlayerContext
            {
                Available = false,
                Message = message ?? string.Empty,
                InfoAccessoryHidden = true
            };
        }

        public static MapRareCreatureDirectionPlayerContext FromRuntime(GameStateSnapshot gameState)
        {
            try
            {
                Terraria.Player player;
                if (!TerrariaMainCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    return Unavailable("local player unavailable");
                }

                var center = TerrariaPlayerReadCompat.Center(player);
                return new MapRareCreatureDirectionPlayerContext
                {
                    Available = gameState == null || gameState.IsInWorld,
                    Message = gameState == null || gameState.IsInWorld ? string.Empty : "player is not in world",
                    HasLifeformAnalyzer = TerrariaPlayerReadCompat.HasLifeformAnalyzer(player),
                    InfoAccessoryHidden = TerrariaPlayerReadCompat.IsInfoAccessoryHidden(player, MapRareCreatureDirectionTargetResolver.LifeformAnalyzerInfoIndex),
                    PlayerCenterX = center.X,
                    PlayerCenterY = center.Y
                };
            }
            catch (Exception error)
            {
                return Unavailable("rare creature player gate unavailable: " + error.Message);
            }
        }
    }

    internal sealed class MapRareCreatureDirectionProjection
    {
        public string Status { get; set; }
        public bool OnScreen { get; set; }
        public bool ShouldDraw { get; set; }
        public bool ShouldDrawLabel { get; set; }
        public float DistancePixels { get; set; }
        public string DistanceText { get; set; }
        public float ArrowX { get; set; }
        public float ArrowY { get; set; }
        public float DirectionX { get; set; }
        public float DirectionY { get; set; }
        public string ArrowGlyph { get; set; }
        public string LabelLine1 { get; set; }
        public string LabelLine2 { get; set; }
        public int TargetRarity { get; set; }

        public MapRareCreatureDirectionProjection()
        {
            Status = string.Empty;
            DistanceText = string.Empty;
            ArrowGlyph = string.Empty;
            LabelLine1 = string.Empty;
            LabelLine2 = string.Empty;
        }

        public static MapRareCreatureDirectionProjection Empty(string status)
        {
            return new MapRareCreatureDirectionProjection
            {
                Status = status ?? string.Empty
            };
        }
    }
}
