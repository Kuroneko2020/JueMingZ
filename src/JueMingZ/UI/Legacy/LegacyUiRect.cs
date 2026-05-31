namespace JueMingZ.UI.Legacy
{
    public struct LegacyUiRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public LegacyUiRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Right { get { return X + Width; } }
        public int Bottom { get { return Y + Height; } }
        public int CenterX { get { return X + Width / 2; } }
        public int CenterY { get { return Y + Height / 2; } }

        public bool Contains(int x, int y)
        {
            return x >= X && y >= Y && x < X + Width && y < Y + Height;
        }

        public bool Intersects(LegacyUiRect other)
        {
            return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
        }

        public LegacyUiRect Intersect(LegacyUiRect other)
        {
            var x = System.Math.Max(X, other.X);
            var y = System.Math.Max(Y, other.Y);
            var right = System.Math.Min(Right, other.Right);
            var bottom = System.Math.Min(Bottom, other.Bottom);
            return new LegacyUiRect(x, y, System.Math.Max(0, right - x), System.Math.Max(0, bottom - y));
        }

        public LegacyUiRect Inset(int amount)
        {
            return new LegacyUiRect(X + amount, Y + amount, Width - amount * 2, Height - amount * 2);
        }
    }
}
