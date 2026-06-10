using System;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TryReadJumpInputProfile(object player, out JumpInputProfile profile)
        {
            profile = new JumpInputProfile();
            if (player == null)
            {
                return Fail("Cannot read jump input: player unavailable.");
            }

            bool boolValue;
            int intValue;
            float floatValue;
            profile.PlayerActive = !TryGetBool(player, "active", out boolValue) || boolValue;
            profile.PlayerDead = TryGetBool(player, "dead", out boolValue) && boolValue;
            profile.PlayerGhost = TryGetBool(player, "ghost", out boolValue) && boolValue;
            profile.PlayerCrowdControlled = TryGetBool(player, "CCed", out boolValue) && boolValue;

            if (!TryGetBool(player, "controlJump", out boolValue))
            {
                return Fail("Cannot read player.controlJump.");
            }

            profile.ControlJump = boolValue;
            profile.ReleaseJump = TryGetBool(player, "releaseJump", out boolValue) && boolValue;
            profile.ControlDown = TryGetBool(player, "controlDown", out boolValue) && boolValue;
            profile.Sliding = TryGetBool(player, "sliding", out boolValue) && boolValue;
            profile.JumpTicksRemaining = TryGetInt(player, "jump", out intValue) ? intValue : 0;
            profile.GravityDirection = TryGetFloat(player, "gravDir", out floatValue) && Math.Abs(floatValue) > 0.001f
                ? floatValue
                : 1f;

            var velocity = GetMember(player, "velocity");
            float velocityX;
            float velocityY;
            if (TryReadVector2(velocity, out velocityX, out velocityY))
            {
                profile.VelocityY = velocityY;
            }

            profile.GroundedOrSliding = Math.Abs(profile.VelocityY) < 0.001f || profile.Sliding;
            var verticalSpeed = profile.VelocityY * profile.GravityDirection;
            profile.AerialJumpWindow = !profile.GroundedOrSliding &&
                                       profile.JumpTicksRemaining <= 0 &&
                                       verticalSpeed > -1f;

            profile.AirJumpFlagCount = CountEnabledAirJumpFlags(player);
            profile.HasAirJump = profile.AirJumpFlagCount > 0;

            if (TryReadCanUseBootFlyingAbilities(player, out boolValue))
            {
                profile.CanUseBootFlyingAbilities = boolValue;
                profile.CanUseBootFlyingAbilitiesKnown = true;
            }
            else
            {
                profile.CanUseBootFlyingAbilities = true;
                profile.CanUseBootFlyingAbilitiesKnown = false;
            }

            profile.RocketBoots = TryGetInt(player, "rocketBoots", out intValue) ? intValue : 0;
            profile.RocketTime = TryGetFloat(player, "rocketTime", out floatValue) ? floatValue : 0f;
            profile.RocketDelay = TryGetInt(player, "rocketDelay", out intValue) ? intValue : 0;
            if (TryGetBool(player, "canRocket", out boolValue))
            {
                profile.CanRocket = boolValue;
                profile.CanRocketKnown = true;
            }
            else
            {
                profile.CanRocket = true;
                profile.CanRocketKnown = false;
            }

            profile.RocketRelease = TryGetBool(player, "rocketRelease", out boolValue) && boolValue;
            profile.HasRocketBootsAvailable = profile.CanUseBootFlyingAbilities &&
                                               profile.RocketBoots > 0 &&
                                               profile.RocketTime > 0f &&
                                               profile.RocketDelay <= 0 &&
                                               profile.CanRocket;
            profile.HasRocketJump = profile.HasRocketBootsAvailable && !profile.RocketRelease;

            profile.HasFlyingCarpet = TryGetBool(player, "carpet", out boolValue) && boolValue;
            profile.FlyingCarpetCanStart = TryGetBool(player, "canCarpet", out boolValue) && boolValue;
            profile.FlyingCarpetTime = TryGetInt(player, "carpetTime", out intValue) ? intValue : 0;
            profile.HasFlyingCarpetAvailable = profile.PlayerControllable &&
                                               profile.AerialJumpWindow &&
                                               profile.HasFlyingCarpet &&
                                               (profile.FlyingCarpetCanStart || profile.FlyingCarpetTime > 0);

            profile.HasGravityGlobe = TryGetBool(player, "gravControl2", out boolValue) && boolValue;
            profile.HasGravityFlipOpportunity = profile.PlayerControllable &&
                                                profile.HasGravityGlobe &&
                                                profile.AerialJumpWindow;

            profile.WingsLogic = TryGetInt(player, "wingsLogic", out intValue) ? intValue : 0;
            profile.WingTime = TryGetFloat(player, "wingTime", out floatValue) ? floatValue : 0f;
            profile.HasWingFlight = profile.WingsLogic > 0 && profile.WingTime > 0f;

            ReadMountJumpProfile(player, profile);
            ReadEquippedMovementAssistProfile(player, profile);
            return ClearInputError();
        }
    }
}
