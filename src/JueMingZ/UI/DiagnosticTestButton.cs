namespace JueMingZ.UI
{
    public sealed class DiagnosticTestButton
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Hint { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int HitPaddingX { get; set; }
        public int HitPaddingY { get; set; }
        public bool Enabled { get; set; }

        public DiagnosticTestButton()
        {
            Id = string.Empty;
            Label = string.Empty;
            Hint = string.Empty;
            Enabled = true;
        }

        public int HitX { get { return X - HitPaddingX; } }
        public int HitY { get { return Y - HitPaddingY; } }
        public int HitWidth { get { return Width + HitPaddingX * 2; } }
        public int HitHeight { get { return Height + HitPaddingY * 2; } }
        public int CenterX { get { return X + Width / 2; } }
        public int CenterY { get { return Y + Height / 2; } }

        public bool Contains(int x, int y)
        {
            return ContainsHit(x, y);
        }

        public bool ContainsVisual(int x, int y)
        {
            return x >= X &&
                   y >= Y &&
                   x < X + Width &&
                   y < Y + Height;
        }

        public bool ContainsHit(int x, int y)
        {
            return x >= HitX &&
                   y >= HitY &&
                   x < HitX + HitWidth &&
                   y < HitY + HitHeight;
        }

        public double DistanceToCenter(int x, int y)
        {
            var dx = x - CenterX;
            var dy = y - CenterY;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
