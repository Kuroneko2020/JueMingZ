using System;
using JueMingZ.Automation.Movement;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        public static bool TryIsMouseItemPresent()
        {
            object mouseItem;
            if (!TryGetStaticMember(TerrariaRuntimeTypes.MainType, "mouseItem", out mouseItem) || mouseItem == null)
            {
                return false;
            }

            return !CreateSignature(mouseItem).IsAir;
        }

        public static bool TryIsSafeToRestoreTemporaryEquipment(object player, out bool safeToRestore, out string reason)
        {
            safeToRestore = false;
            reason = string.Empty;
            if (player == null)
            {
                reason = "playerUnavailable";
                return false;
            }

            JumpInputProfile profile;
            if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile) || profile == null)
            {
                reason = "jumpProfileUnavailable";
                return false;
            }

            var fallingSpeed = profile.VelocityY * (Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f);
            if (fallingSpeed > 1f)
            {
                reason = "stillFalling";
                return true;
            }

            string groundedReason;
            if (TryIsActuallyGroundedForRestore(player, profile, out groundedReason))
            {
                safeToRestore = true;
                reason = groundedReason;
                return true;
            }

            reason = fallingSpeed < -1f ? "risingAfterRescue" : "waitingForGrounded";
            return true;
        }

        private static bool TryIsActuallyGroundedForRestore(object player, JumpInputProfile profile, out string reason)
        {
            reason = string.Empty;
            if (player == null || profile == null)
            {
                reason = "groundProbeUnavailable";
                return false;
            }

            if (profile.Sliding)
            {
                reason = "sliding";
                return true;
            }

            var gravityDirection = Math.Abs(profile.GravityDirection) > 0.001f ? profile.GravityDirection : 1f;
            var fallingSpeed = profile.VelocityY * gravityDirection;
            if (Math.Abs(fallingSpeed) > 0.05f)
            {
                reason = fallingSpeed > 0f ? "stillMovingDown" : "risingAfterRescue";
                return false;
            }

            float positionX;
            float positionY;
            if (!TryReadVectorMember(player, "position", out positionX, out positionY))
            {
                reason = "groundProbePositionUnavailable";
                return false;
            }

            var width = TryReadIntOrDefault(player, "width", 20);
            var height = TryReadIntOrDefault(player, "height", 42);
            var probeY = positionY + (gravityDirection >= 0f ? 2f : -2f);
            bool solid;
            if (MovementSafeLandingCompat.TryProbeLandingCollision(positionX, probeY, width, height, gravityDirection, Math.Max(1f, Math.Abs(profile.VelocityY)), out solid) && solid)
            {
                reason = "grounded";
                return true;
            }

            reason = "waitingForGrounded";
            return false;
        }
    }
}
