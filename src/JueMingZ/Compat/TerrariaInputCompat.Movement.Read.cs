using System;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TryReadHorizontalMovementDirection(object player, out int direction, out bool leftHeld, out bool rightHeld)
        {
            direction = 0;
            leftHeld = false;
            rightHeld = false;
            if (player == null)
            {
                return Fail("Cannot read horizontal movement: player unavailable.");
            }

            if (!TryGetBool(player, "controlLeft", out leftHeld))
            {
                return Fail("Cannot read player.controlLeft.");
            }

            if (!TryGetBool(player, "controlRight", out rightHeld))
            {
                return Fail("Cannot read player.controlRight.");
            }

            if (leftHeld == rightHeld)
            {
                return ClearInputError();
            }

            direction = rightHeld ? 1 : -1;
            return ClearInputError();
        }

        internal static bool TryReadPlayerWhoAmI(object player, out int whoAmI)
        {
            whoAmI = -1;
            return player != null && TryGetInt(player, "whoAmI", out whoAmI);
        }

        internal static bool TryReadMovementBasicMotion(object player, out MovementInputBasicMotion motion)
        {
            motion = new MovementInputBasicMotion();
            if (player == null)
            {
                return Fail("Cannot read movement basic motion: player unavailable.");
            }

            bool boolValue;
            var activeAvailable = TryGetBool(player, "active", out boolValue);
            motion.PlayerActive = activeAvailable && boolValue;
            var deadAvailable = TryGetBool(player, "dead", out boolValue);
            motion.PlayerDead = deadAvailable && boolValue;
            var ghostAvailable = TryGetBool(player, "ghost", out boolValue);
            motion.PlayerGhost = ghostAvailable && boolValue;
            var ccAvailable = TryGetBool(player, "CCed", out boolValue);
            motion.PlayerCrowdControlled = ccAvailable && boolValue;
            motion.PlayerStateAvailable = activeAvailable && deadAvailable && ghostAvailable && ccAvailable;
            if (!motion.PlayerStateAvailable)
            {
                motion.PlayerStateFailureReason = "playerStateUnavailable";
            }

            int intValue;
            if (TryGetInt(player, "whoAmI", out intValue))
            {
                motion.WhoAmI = intValue;
                motion.WhoAmIAvailable = true;
            }
            else
            {
                motion.WhoAmIFailureReason = "whoAmIUnavailable";
            }

            float floatValue;
            if (TryGetFloat(player, "gravDir", out floatValue))
            {
                motion.GravityDirection = Math.Abs(floatValue) > 0.001f ? floatValue : 1f;
                motion.GravityDirectionAvailable = true;
            }
            else
            {
                motion.GravityDirectionFailureReason = "gravDirUnavailable";
            }

            var position = GetMember(player, "position");
            float x;
            float y;
            if (TryReadVector2(position, out x, out y))
            {
                motion.PositionX = x;
                motion.PositionY = y;
                motion.PositionAvailable = true;
            }
            else
            {
                motion.PositionFailureReason = "positionUnavailable";
            }

            var velocity = GetMember(player, "velocity");
            if (TryReadVector2(velocity, out x, out y))
            {
                motion.VelocityX = x;
                motion.VelocityY = y;
                motion.VelocityAvailable = true;
            }
            else
            {
                motion.VelocityFailureReason = "velocityUnavailable";
            }

            var widthAvailable = TryGetInt(player, "width", out intValue);
            if (widthAvailable)
            {
                motion.Width = intValue;
            }

            var heightAvailable = TryGetInt(player, "height", out intValue);
            if (heightAvailable)
            {
                motion.Height = intValue;
            }

            motion.DimensionsAvailable = widthAvailable && heightAvailable;
            if (!motion.DimensionsAvailable)
            {
                motion.DimensionsFailureReason = "dimensionsUnavailable";
            }

            return ClearInputError();
        }
    }
}
