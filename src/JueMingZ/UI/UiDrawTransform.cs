using System;
using Microsoft.Xna.Framework;

namespace JueMingZ.UI
{
    public static class UiDrawTransform
    {
        private const float IdentityTolerance = 0.0001f;

        [ThreadStatic]
        private static TransformState _current;

        public static IDisposable Begin(float scaleX, float scaleY)
        {
            // Transform scopes must be nested and disposed so retained UI scaling cannot
            // leak into unrelated overlay draws.
            var previous = _current;
            var safeScaleX = NormalizeScale(scaleX);
            var safeScaleY = NormalizeScale(scaleY);
            if (previous != null)
            {
                safeScaleX *= previous.ScaleX;
                safeScaleY *= previous.ScaleY;
            }

            _current = new TransformState(safeScaleX, safeScaleY);
            return new Scope(previous);
        }

        internal static void TransformRectangleForTesting(
            int x,
            int y,
            int width,
            int height,
            out int transformedX,
            out int transformedY,
            out int transformedWidth,
            out int transformedHeight)
        {
            var rectangle = TransformRectangle(x, y, width, height);
            transformedX = rectangle.X;
            transformedY = rectangle.Y;
            transformedWidth = rectangle.Width;
            transformedHeight = rectangle.Height;
        }

        internal static float TransformScaleForTesting(float scale)
        {
            return TransformScale(scale);
        }

        internal static bool ActiveForTesting
        {
            get { return IsActive; }
        }

        internal static Rectangle TransformRectangle(int x, int y, int width, int height)
        {
            if (!IsActive)
            {
                return new Rectangle(x, y, width, height);
            }

            var left = TransformCoordinate(x, _current.ScaleX);
            var top = TransformCoordinate(y, _current.ScaleY);
            var right = TransformCoordinate(x + width, _current.ScaleX);
            var bottom = TransformCoordinate(y + height, _current.ScaleY);
            return new Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }

        internal static Vector2 TransformVector(float x, float y)
        {
            if (!IsActive)
            {
                return new Vector2(x, y);
            }

            return new Vector2(
                (float)Math.Round(x * _current.ScaleX),
                (float)Math.Round(y * _current.ScaleY));
        }

        internal static float TransformScale(float scale)
        {
            if (!IsActive)
            {
                return scale;
            }

            return scale * Math.Min(_current.ScaleX, _current.ScaleY);
        }

        private static bool IsActive
        {
            get
            {
                return _current != null &&
                       (Math.Abs(_current.ScaleX - 1f) > IdentityTolerance ||
                        Math.Abs(_current.ScaleY - 1f) > IdentityTolerance);
            }
        }

        private static int TransformCoordinate(int value, float scale)
        {
            return (int)Math.Round(value * scale);
        }

        private static float NormalizeScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0.01f)
            {
                return 1f;
            }

            return scale;
        }

        private sealed class Scope : IDisposable
        {
            private readonly TransformState _previous;
            private bool _disposed;

            public Scope(TransformState previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _current = _previous;
                _disposed = true;
            }
        }

        private sealed class TransformState
        {
            public readonly float ScaleX;
            public readonly float ScaleY;

            public TransformState(float scaleX, float scaleY)
            {
                ScaleX = scaleX;
                ScaleY = scaleY;
            }
        }
    }
}
