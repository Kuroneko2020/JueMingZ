namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementLandingSurfaceHit
    {
        private string _summary;

        public bool Found { get; set; }
        public int ImpactDistancePixels { get; set; }
        public float ImpactTicks { get; set; }
        public float ProjectedPlayerLeftX { get; set; }
        public float ProjectedPlayerRightX { get; set; }
        public float ProjectedPlayerBottomY { get; set; }
        public float ContactWorldX { get; set; }
        public float ContactWorldY { get; set; }
        public int ContactTileX { get; set; }
        public int ContactTileY { get; set; }
        public string SurfaceKind { get; set; }
        public int SlopeType { get; set; }
        public string SlopeDirection { get; set; }
        public string ContactSample { get; set; }
        public bool MovingIntoSlope { get; set; }
        public bool MovingWithSlope { get; set; }
        public float SurfaceNormalX { get; set; }
        public float SurfaceNormalY { get; set; }
        public string Summary
        {
            get
            {
                if (_summary == null)
                {
                    _summary = BuildSummary();
                }

                return _summary;
            }

            set
            {
                _summary = value ?? string.Empty;
            }
        }

        public MovementLandingSurfaceHit()
        {
            Found = false;
            ImpactDistancePixels = -1;
            ImpactTicks = -1f;
            ContactTileX = -1;
            ContactTileY = -1;
            SurfaceKind = string.Empty;
            SlopeDirection = string.Empty;
            ContactSample = string.Empty;
        }

        public string BuildSummary()
        {
            if (!Found)
            {
                return string.Empty;
            }

            var surfaceKind = string.IsNullOrEmpty(SurfaceKind) ? "unknown" : SurfaceKind;
            var slopeDirection = string.IsNullOrEmpty(SlopeDirection) ? "unknown" : SlopeDirection;
            var contactSample = string.IsNullOrEmpty(ContactSample) ? "unknown" : ContactSample;
            return surfaceKind +
                   (slopeDirection.Length > 0 ? " " + slopeDirection : string.Empty) +
                   " contact=" + contactSample +
                   " movingIntoSlope=" + BoolText(MovingIntoSlope) +
                   " movingWithSlope=" + BoolText(MovingWithSlope) +
                   " world=(" + ContactWorldX.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) +
                   "," + ContactWorldY.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
