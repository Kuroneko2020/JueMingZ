using JueMingZ.Compat;

namespace JueMingZ.Automation.Movement
{
    public static partial class MovementSafeLandingService
    {
        private static bool HasActiveSafeLandingJumpPulse()
        {
            SafeLandingJumpPulseSnapshot pulse;
            return MovementSafeLandingCompat.TryGetAnySafeLandingJumpPulseSnapshot(out pulse) &&
                   pulse != null &&
                   pulse.Active;
        }

        private static bool TryReadJumpInputProfile(
            MovementInputFrameCache.MovementInputFrame inputFrame,
            object player,
            out JumpInputProfile profile,
            out string failureReason)
        {
            profile = null;
            failureReason = string.Empty;
            if (inputFrame != null && inputFrame.TryGetJumpProfile(out profile, out failureReason))
            {
                return true;
            }

            if (TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) && profile != null)
            {
                failureReason = string.Empty;
                return true;
            }

            failureReason = FirstNonEmpty(TerrariaInputCompat.LastInputCompatError, failureReason);
            return false;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }
    }
}
