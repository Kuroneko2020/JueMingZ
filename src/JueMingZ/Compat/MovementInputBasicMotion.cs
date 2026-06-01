using System;

namespace JueMingZ.Compat
{
    public sealed class MovementInputBasicMotion
    {
        public MovementInputBasicMotion()
        {
            PlayerStateFailureReason = string.Empty;
            PositionFailureReason = string.Empty;
            VelocityFailureReason = string.Empty;
            GravityDirectionFailureReason = string.Empty;
            DimensionsFailureReason = string.Empty;
            WhoAmIFailureReason = string.Empty;
            Width = 20;
            Height = 42;
            WhoAmI = -1;
            GravityDirection = 1f;
        }

        public bool PlayerStateAvailable { get; set; }
        public bool PlayerActive { get; set; }
        public bool PlayerDead { get; set; }
        public bool PlayerGhost { get; set; }
        public bool PlayerCrowdControlled { get; set; }
        public string PlayerStateFailureReason { get; set; }

        public bool PositionAvailable { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public string PositionFailureReason { get; set; }

        public bool VelocityAvailable { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public string VelocityFailureReason { get; set; }

        public bool GravityDirectionAvailable { get; set; }
        public float GravityDirection { get; set; }
        public string GravityDirectionFailureReason { get; set; }

        public bool DimensionsAvailable { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string DimensionsFailureReason { get; set; }

        public bool WhoAmIAvailable { get; set; }
        public int WhoAmI { get; set; }
        public string WhoAmIFailureReason { get; set; }

        public bool PlayerControllable
        {
            get { return PlayerStateAvailable && PlayerActive && !PlayerDead && !PlayerGhost && !PlayerCrowdControlled; }
        }

        public string FailureSummary
        {
            get
            {
                var summary = string.Empty;
                AppendFailure(ref summary, PlayerStateFailureReason);
                AppendFailure(ref summary, PositionFailureReason);
                AppendFailure(ref summary, VelocityFailureReason);
                AppendFailure(ref summary, GravityDirectionFailureReason);
                AppendFailure(ref summary, DimensionsFailureReason);
                AppendFailure(ref summary, WhoAmIFailureReason);
                return summary;
            }
        }

        private static void AppendFailure(ref string summary, string failure)
        {
            if (string.IsNullOrWhiteSpace(failure))
            {
                return;
            }

            summary = string.IsNullOrWhiteSpace(summary) ? failure : summary + ";" + failure;
        }
    }
}
