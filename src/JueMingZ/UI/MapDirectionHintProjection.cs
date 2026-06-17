using System;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    internal static class MapDirectionHintProjection
    {
        private const float TileSizePixels = 16f;

        public static Vector2 WorldToScreen(Vector2 worldPoint, Vector2 screenPosition)
        {
            return worldPoint - screenPosition;
        }

        public static bool IsWorldPointOnScreen(
            Vector2 worldPoint,
            Vector2 screenPosition,
            int screenWidth,
            int screenHeight,
            int paddingPixels)
        {
            return IsScreenPointOnScreen(
                WorldToScreen(worldPoint, screenPosition),
                new Rectangle(0, 0, Math.Max(1, screenWidth), Math.Max(1, screenHeight)),
                paddingPixels);
        }

        public static bool IsScreenPointOnScreen(Vector2 screenPoint, Rectangle screen, int paddingPixels)
        {
            var normalized = NormalizeScreen(screen);
            var padding = Math.Max(0, paddingPixels);
            return IsFinite(screenPoint) &&
                   screenPoint.X >= normalized.Left - padding &&
                   screenPoint.X <= normalized.Right + padding &&
                   screenPoint.Y >= normalized.Top - padding &&
                   screenPoint.Y <= normalized.Bottom + padding;
        }

        public static MapDirectionHintArrowAnchor BuildPlayerArrowAnchor(
            Vector2 playerScreenCenter,
            Vector2 targetScreenPoint,
            float radiusPixels)
        {
            var result = new MapDirectionHintArrowAnchor
            {
                Position = playerScreenCenter,
                Direction = Vector2.Zero,
                DistancePixels = Distance(playerScreenCenter, targetScreenPoint),
                HasDirection = false
            };

            var direction = targetScreenPoint - playerScreenCenter;
            if (!TryNormalize(direction, out direction))
            {
                return result;
            }

            result.Direction = direction;
            result.Position = playerScreenCenter + direction * Math.Max(0f, radiusPixels);
            result.HasDirection = true;
            return result;
        }

        public static MapDirectionHintEdgeProjection ClampToEllipseEdge(
            Rectangle screen,
            Vector2 targetScreenPoint,
            float horizontalInsetPixels,
            float verticalInsetPixels)
        {
            var normalized = NormalizeScreen(screen);
            var center = new Vector2(
                normalized.Left + normalized.Width / 2f,
                normalized.Top + normalized.Height / 2f);
            var radiusX = Math.Max(1f, normalized.Width / 2f - Math.Max(0f, horizontalInsetPixels));
            var radiusY = Math.Max(1f, normalized.Height / 2f - Math.Max(0f, verticalInsetPixels));
            var vector = targetScreenPoint - center;
            Vector2 direction;
            var hasDirection = TryNormalize(vector, out direction);
            if (!hasDirection)
            {
                return new MapDirectionHintEdgeProjection
                {
                    Position = center,
                    Direction = Vector2.Zero,
                    TargetInside = true,
                    HasDirection = false
                };
            }

            var normalizedX = vector.X / radiusX;
            var normalizedY = vector.Y / radiusY;
            var length = (float)Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
            if (length <= 1f)
            {
                return new MapDirectionHintEdgeProjection
                {
                    Position = targetScreenPoint,
                    Direction = direction,
                    TargetInside = true,
                    HasDirection = true
                };
            }

            return new MapDirectionHintEdgeProjection
            {
                Position = center + vector / length,
                Direction = direction,
                TargetInside = false,
                HasDirection = true
            };
        }

        public static string FormatApproxTileDistance(float distancePixels)
        {
            if (float.IsNaN(distancePixels) || float.IsInfinity(distancePixels) || distancePixels <= 0f)
            {
                return "约0格";
            }

            var tiles = (int)Math.Round(distancePixels / TileSizePixels, MidpointRounding.AwayFromZero);
            return "约" + Math.Max(1, tiles).ToString(System.Globalization.CultureInfo.InvariantCulture) + "格";
        }

        private static Rectangle NormalizeScreen(Rectangle screen)
        {
            return new Rectangle(screen.X, screen.Y, Math.Max(1, screen.Width), Math.Max(1, screen.Height));
        }

        private static bool TryNormalize(Vector2 value, out Vector2 normalized)
        {
            normalized = Vector2.Zero;
            if (!IsFinite(value))
            {
                return false;
            }

            var length = value.Length();
            if (length <= 0.0001f)
            {
                return false;
            }

            normalized = value / length;
            return IsFinite(normalized);
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

    internal struct MapDirectionHintArrowAnchor
    {
        public Vector2 Position { get; set; }
        public Vector2 Direction { get; set; }
        public float DistancePixels { get; set; }
        public bool HasDirection { get; set; }
    }

    internal struct MapDirectionHintEdgeProjection
    {
        public Vector2 Position { get; set; }
        public Vector2 Direction { get; set; }
        public bool TargetInside { get; set; }
        public bool HasDirection { get; set; }
    }
}
