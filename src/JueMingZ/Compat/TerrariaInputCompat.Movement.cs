using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TrySetControlDown(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot set controlDown: player unavailable.");
            }

            // Controlled input write: safe landing may suppress down for a few ticks during an emergency jump.
            return SetMember(player, "controlDown", pressed)
                ? ClearInputError()
                : Fail("Cannot set player.controlDown.");
        }

        public static bool TryReadPlayerDirection(object player, out int direction)
        {
            direction = 0;
            if (player == null)
            {
                return Fail("Cannot read player direction: player unavailable.");
            }

            int rawDirection;
            if (!TryGetInt(player, "direction", out rawDirection))
            {
                return Fail("Cannot read player.direction.");
            }

            direction = rawDirection >= 0 ? 1 : -1;
            return ClearInputError();
        }

        public static bool TryChangePlayerDirection(object player, int direction, out int beforeDirection, out int afterDirection, out string method)
        {
            return TryChangePlayerDirection(player, direction, false, out beforeDirection, out afterDirection, out method);
        }

        public static bool TryChangePlayerDirection(object player, int direction, bool allowFieldFallbackAfterChangeDir, out int beforeDirection, out int afterDirection, out string method)
        {
            beforeDirection = 0;
            afterDirection = 0;
            method = string.Empty;
            if (player == null)
            {
                return Fail("Cannot change player direction: player unavailable.");
            }

            if (direction == 0)
            {
                return Fail("Cannot change player direction: direction is 0.");
            }

            var normalized = direction >= 0 ? 1 : -1;
            TryReadPlayerDirection(player, out beforeDirection);
            if (beforeDirection == normalized)
            {
                afterDirection = beforeDirection;
                method = "AlreadyFacing";
                return ClearInputError();
            }

            if (EnsureChangeDirMethod(player))
            {
                try
                {
                    // Controlled facing write: prefer Terraria.Player.ChangeDir so itemRotation and pulley state stay coherent.
                    _changeDirMethod.Invoke(player, new object[] { normalized });
                    method = "Player.ChangeDir";
                }
                catch (Exception error)
                {
                    return Fail("Player.ChangeDir failed: " + error.Message);
                }
            }
            else
            {
                // Fallback only if the original helper is unavailable in this Terraria build.
                if (!SetMember(player, "direction", normalized))
                {
                    return false;
                }

                method = "directionFieldFallback";
            }

            if (!TryReadPlayerDirection(player, out afterDirection))
            {
                afterDirection = normalized;
                return ClearInputError();
            }

            if (afterDirection != normalized && allowFieldFallbackAfterChangeDir)
            {
                // Controlled facing fallback: some item-use paths keep ChangeDir from sticking until itemAnimation ends.
                if (SetMember(player, "direction", normalized) && TryReadPlayerDirection(player, out afterDirection))
                {
                    method = string.IsNullOrWhiteSpace(method)
                        ? "directionFieldFallback"
                        : method + "+directionFieldFallback";
                }
            }

            return afterDirection == normalized
                ? ClearInputError()
                : Fail("Player direction did not match requested direction after " + method + ".");
        }

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

        public static bool TryPrimeJumpReleaseForNextTick(object player, bool applyRocketRelease, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot prime jump release: player unavailable.";
                return Fail(message);
            }

            // Controlled input write: simulated multi-jump only toggles vanilla jump input state.
            var ok = SetMember(player, "controlJump", false);
            ok &= SetMember(player, "releaseJump", true);

            var rocketReleaseApplied = false;
            if (applyRocketRelease)
            {
                // Optional controlled input write: rocketRelease exists in Terraria 1.4.x and is ignored if absent.
                rocketReleaseApplied = TrySetMemberIfExists(player, "rocketRelease", true);
            }

            var triggersSynced = TrySyncPlayerInputJumpTriggers(false, false, true, out var triggerMessage);

            message = ok
                ? "Jump release primed" + (rocketReleaseApplied ? " with rocket release." : ".")
                : "Jump release prime failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryPressPrimedJumpForNextTick(object player, bool applyRocketRelease, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot press primed jump: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", true);
            ok &= SetMember(player, "releaseJump", true);

            var rocketReleaseApplied = false;
            if (applyRocketRelease)
            {
                rocketReleaseApplied = TrySetMemberIfExists(player, "rocketRelease", true);
            }

            var triggersSynced = TrySyncPlayerInputJumpTriggers(true, true, false, out var triggerMessage);

            message = ok
                ? "Primed jump press armed" + (rocketReleaseApplied ? " with rocket release." : ".")
                : "Primed jump press failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryHoldJumpInput(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot hold jump input: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", true);
            var triggersSynced = TrySyncPlayerInputJumpTriggers(true, false, false, out var triggerMessage);
            message = ok ? "Jump input held." : "Jump input hold failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryReleaseJumpInput(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot release jump input: player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, "controlJump", false);
            ok &= SetMember(player, "releaseJump", true);
            var triggersSynced = TrySyncPlayerInputJumpTriggers(false, false, true, out var triggerMessage);
            message = ok ? "Jump input released." : "Jump input release failed: " + LastInputCompatError;
            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        public static bool TryPrimeQuickMountReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", false, false, true, "Quick mount release primed.", "Quick mount release prime failed", out message);
        }

        public static bool TryPressPrimedQuickMountForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", true, true, false, "Primed quick mount press armed.", "Primed quick mount press failed", out message);
        }

        public static bool TryHoldQuickMountInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", true, false, false, "Quick mount input held.", "Quick mount input hold failed", out message);
        }

        public static bool TryReleaseQuickMountInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlMount", "QuickMount", false, false, true, "Quick mount input released.", "Quick mount input release failed", out message);
        }

        public static bool TryPrimeGrappleReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", false, false, true, "Grapple release primed.", "Grapple release prime failed", out message);
        }

        public static bool TryPressPrimedGrappleForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", true, true, false, "Primed grapple press armed.", "Primed grapple press failed", out message);
        }

        public static bool TryHoldGrappleInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", true, false, false, "Grapple input held.", "Grapple input hold failed", out message);
        }

        public static bool TryReleaseGrappleInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlHook", "Grapple", false, false, true, "Grapple input released.", "Grapple input release failed", out message);
        }

        public static bool TryPrimeUpReleaseForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", false, false, true, "Up input release primed.", "Up input release prime failed", out message);
        }

        public static bool TryPressPrimedUpForNextTick(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", true, true, false, "Primed up input press armed.", "Primed up input press failed", out message);
        }

        public static bool TryHoldUpInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", true, false, false, "Up input held.", "Up input hold failed", out message);
        }

        public static bool TryReleaseUpInput(object player, out string message)
        {
            return TrySetNamedControlInput(player, "controlUp", "Up", false, false, true, "Up input released.", "Up input release failed", out message);
        }

        public static bool TryReleaseSafeLandingControlInputs(object player, out string message)
        {
            string jumpMessage;
            string mountMessage;
            string grappleMessage;
            string upMessage;
            var jumpOk = TryReleaseJumpInput(player, out jumpMessage);
            var mountOk = TryReleaseQuickMountInput(player, out mountMessage);
            var grappleOk = TryReleaseGrappleInput(player, out grappleMessage);
            var upOk = TryReleaseUpInput(player, out upMessage);
            message = (jumpMessage ?? string.Empty) + " " +
                      (mountMessage ?? string.Empty) + " " +
                      (grappleMessage ?? string.Empty) + " " +
                      (upMessage ?? string.Empty);
            return jumpOk || mountOk || grappleOk || upOk;
        }

        private static bool TrySetNamedControlInput(object player, string controlFieldName, string playerInputTriggerName, bool current, bool justPressed, bool justReleased, string successMessage, string failurePrefix, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = failurePrefix + ": player unavailable.";
                return Fail(message);
            }

            var ok = SetMember(player, controlFieldName, current);
            var releaseFieldName = ResolveReleaseFieldName(controlFieldName);
            var releaseSynced = false;
            if (!string.IsNullOrWhiteSpace(releaseFieldName) && (justPressed || justReleased || !current))
            {
                // Quick mount / grapple are one-shot vanilla control inputs. Their Terraria handlers also gate on
                // releaseMount / releaseHook in several versions, so keep those release flags primed when emitting
                // a synthetic press or release. Missing fields are allowed: PlayerInput trigger sync remains the
                // fallback path.
                releaseSynced = TrySetMemberIfExists(player, releaseFieldName, true);
            }

            var triggersSynced = TrySyncPlayerInputTrigger(playerInputTriggerName, current, justPressed, justReleased, out var triggerMessage);
            message = ok ? successMessage : failurePrefix + ": " + LastInputCompatError;
            if (releaseSynced)
            {
                message += " " + releaseFieldName + " primed.";
            }

            message = AppendPlayerInputTriggerSyncMessage(message, triggersSynced, triggerMessage);
            return ok ? ClearInputError() : false;
        }

        private static string ResolveReleaseFieldName(string controlFieldName)
        {
            if (string.Equals(controlFieldName, "controlMount", StringComparison.Ordinal))
            {
                return "releaseMount";
            }

            if (string.Equals(controlFieldName, "controlHook", StringComparison.Ordinal))
            {
                return "releaseHook";
            }

            if (string.Equals(controlFieldName, "controlUp", StringComparison.Ordinal))
            {
                return "releaseUp";
            }

            if (string.Equals(controlFieldName, "controlDown", StringComparison.Ordinal))
            {
                return "releaseDown";
            }

            return string.Empty;
        }

        public static void BeginAutoFacingDirectionOverride(Guid requestId, int direction, int selectedSlot, int itemType, TimeSpan duration)
        {
            lock (AutoFacingOverrideSync)
            {
                if (direction == 0)
                {
                    _autoFacingOverrideRequestId = Guid.Empty;
                    _autoFacingOverrideDirection = 0;
                    _autoFacingOverrideSelectedSlot = -1;
                    _autoFacingOverrideItemType = 0;
                    _autoFacingOverrideExpiresUtc = DateTime.MinValue;
                    _lastAutoFacingOverrideMessage = "AutoFacing ItemCheck direction override cleared: direction was 0.";
                    return;
                }

                var ttl = duration <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(750) : duration;
                _autoFacingOverrideRequestId = requestId;
                _autoFacingOverrideDirection = direction >= 0 ? 1 : -1;
                _autoFacingOverrideSelectedSlot = selectedSlot;
                _autoFacingOverrideItemType = itemType;
                _autoFacingOverrideExpiresUtc = DateTime.UtcNow.Add(ttl);
                _lastAutoFacingOverrideMessage = "AutoFacing ItemCheck direction override armed.";
            }
        }

        public static bool TryApplyAutoFacingDirectionOverrideForItemCheck(object player, out bool applied, out string message)
        {
            applied = false;
            message = string.Empty;
            Guid requestId;
            int direction;
            int selectedSlot;
            int itemType;
            lock (AutoFacingOverrideSync)
            {
                if (_autoFacingOverrideRequestId == Guid.Empty || _autoFacingOverrideDirection == 0)
                {
                    return false;
                }

                if (DateTime.UtcNow > _autoFacingOverrideExpiresUtc)
                {
                    _autoFacingOverrideRequestId = Guid.Empty;
                    _autoFacingOverrideDirection = 0;
                    _autoFacingOverrideSelectedSlot = -1;
                    _autoFacingOverrideItemType = 0;
                    _autoFacingOverrideExpiresUtc = DateTime.MinValue;
                    message = "AutoFacing ItemCheck direction override expired.";
                    _lastAutoFacingOverrideMessage = message;
                    return false;
                }

                requestId = _autoFacingOverrideRequestId;
                direction = _autoFacingOverrideDirection;
                selectedSlot = _autoFacingOverrideSelectedSlot;
                itemType = _autoFacingOverrideItemType;
            }

            if (player == null || !TryIsLocalPlayer(player))
            {
                message = "AutoFacing ItemCheck direction override skipped for non-local player.";
                _lastAutoFacingOverrideMessage = message;
                return false;
            }

            int currentSlot;
            if (!TryGetSelectedItem(player, out currentSlot))
            {
                message = "AutoFacing ItemCheck direction override skipped: " + LastInputCompatError;
                _lastAutoFacingOverrideMessage = message;
                return false;
            }

            if (selectedSlot >= 0 && currentSlot != selectedSlot)
            {
                ClearAutoFacingDirectionOverride(requestId, "AutoFacing ItemCheck direction override cleared: selected slot changed.");
                message = _lastAutoFacingOverrideMessage;
                return false;
            }

            if (itemType > 0 && !SelectedItemTypeMatches(player, currentSlot, itemType))
            {
                ClearAutoFacingDirectionOverride(requestId, "AutoFacing ItemCheck direction override cleared: selected item changed.");
                message = _lastAutoFacingOverrideMessage;
                return false;
            }

            int beforeDirection;
            int afterDirection;
            string method;
            if (!TryChangePlayerDirection(player, direction, out beforeDirection, out afterDirection, out method))
            {
                message = "AutoFacing ItemCheck direction override failed: " + LastInputCompatError;
                _lastAutoFacingOverrideMessage = message;
                return false;
            }

            applied = afterDirection == (direction >= 0 ? 1 : -1);
            message = applied
                ? "AutoFacing ItemCheck direction override applied via " + method + "."
                : "AutoFacing ItemCheck direction override attempted via " + method + ".";
            _lastAutoFacingOverrideMessage = message;
            return true;
        }

        private static void ClearAutoFacingDirectionOverride(Guid requestId, string message)
        {
            lock (AutoFacingOverrideSync)
            {
                if (_autoFacingOverrideRequestId != Guid.Empty && requestId != Guid.Empty && _autoFacingOverrideRequestId != requestId)
                {
                    return;
                }

                _autoFacingOverrideRequestId = Guid.Empty;
                _autoFacingOverrideDirection = 0;
                _autoFacingOverrideSelectedSlot = -1;
                _autoFacingOverrideItemType = 0;
                _autoFacingOverrideExpiresUtc = DateTime.MinValue;
                _lastAutoFacingOverrideMessage = message ?? string.Empty;
            }
        }

        private static bool SelectedItemTypeMatches(object player, int selectedSlot, int expectedItemType)
        {
            if (player == null || selectedSlot < 0 || expectedItemType <= 0)
            {
                return false;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory == null || selectedSlot >= inventory.Count)
            {
                return false;
            }

            var item = inventory[selectedSlot];
            int itemType;
            return item != null && TryGetInt(item, "type", out itemType) && itemType == expectedItemType;
        }

        private static int CountEnabledAirJumpFlags(object player)
        {
            if (player == null || !EnsureAirJumpFields(player))
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < _airJumpFields.Length; index++)
            {
                var field = _airJumpFields[index];
                if (field == null)
                {
                    continue;
                }

                try
                {
                    if (Convert.ToBoolean(field.GetValue(player)))
                    {
                        count++;
                    }
                }
                catch
                {
                }
            }

            return count;
        }

        private static bool EnsureAirJumpFields(object player)
        {
            if (_airJumpFieldsResolved)
            {
                return _airJumpFields.Length > 0;
            }

            _airJumpFieldsResolved = true;
            if (player == null)
            {
                return false;
            }

            try
            {
                var fields = player.GetType().GetFields(InstanceMemberFlags);
                var matches = new List<FieldInfo>();
                for (var index = 0; index < fields.Length; index++)
                {
                    var field = fields[index];
                    if (field == null || field.FieldType != typeof(bool))
                    {
                        continue;
                    }

                    var name = field.Name ?? string.Empty;
                    if (name.StartsWith("canJumpAgain", StringComparison.Ordinal) ||
                        name.StartsWith("CanJumpAgain", StringComparison.Ordinal))
                    {
                        matches.Add(field);
                    }
                }

                _airJumpFields = matches.ToArray();
                if (_airJumpFields.Length == 0)
                {
                    Logger.Debug("TerrariaInputCompat", "No Player.canJumpAgain_* fields found; air jump detection will be conservative.");
                }

                return _airJumpFields.Length > 0;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Air jump field scan failed: " + error.Message);
                _airJumpFields = new FieldInfo[0];
                return false;
            }
        }

        private static bool TryReadCanUseBootFlyingAbilities(object player, out bool value)
        {
            value = false;
            if (player == null)
            {
                return false;
            }

            if (!_bootFlyingMethodResolved)
            {
                _bootFlyingMethodResolved = true;
                _bootFlyingMethod = player.GetType().GetMethod(
                    "CanUseBootFlyingAbilities",
                    InstanceMemberFlags,
                    null,
                    Type.EmptyTypes,
                    null);
                if (_bootFlyingMethod == null)
                {
                    Logger.Debug("TerrariaInputCompat", "Player.CanUseBootFlyingAbilities() not found; rocket jump detection will use field fallback.");
                }
            }

            if (_bootFlyingMethod == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(_bootFlyingMethod.Invoke(player, null));
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Player.CanUseBootFlyingAbilities() failed: " + error.Message);
                return false;
            }
        }

        private static void ReadMountJumpProfile(object player, JumpInputProfile profile)
        {
            if (player == null || profile == null)
            {
                return;
            }

            var mount = GetMember(player, "mount");
            if (mount == null)
            {
                return;
            }

            bool boolValue;
            int intValue;
            profile.MountActive = TryGetBoolByNames(mount, out boolValue, "Active", "active", "_active") && boolValue;
            profile.MountType = TryGetIntByNames(mount, out intValue, "Type", "type", "_type") ? intValue : -1;

            if (TryGetBoolByNames(mount, out boolValue, "CanFly", "canFly", "_canFly"))
            {
                profile.MountCanFly = boolValue;
                profile.MountCanFlyKnown = true;
            }

            var mountData = GetMember(mount, "_data") ?? GetMember(mount, "data") ?? GetMember(mount, "Data");
            if (mountData != null)
            {
                if (TryGetIntByNames(mountData, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    profile.MountCanFly = true;
                    profile.MountCanFlyKnown = true;
                }

                if (TryGetBoolByNames(mountData, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    profile.MountCanFly = true;
                    profile.MountCanFlyKnown = true;
                }
            }

            if (profile.MountType >= 0 && TryResolveMountNoFallDamage(profile.MountType, out boolValue))
            {
                profile.MountNoFallDamage = boolValue;
                profile.MountNoFallDamageKnown = true;
            }

            profile.HasMountOpportunity = profile.MountActive &&
                                          profile.MountCanFlyKnown &&
                                          profile.MountCanFly &&
                                          profile.AerialJumpWindow;
        }

        private static void ReadEquippedMovementAssistProfile(object player, JumpInputProfile profile)
        {
            if (player == null || profile == null)
            {
                return;
            }

            var miscEquips = GetMember(player, "miscEquips") as IList;
            object item;
            int itemType;
            int mountType;
            bool canFly;

            if (TryGetItemAt(miscEquips, 3, out item) && TryReadItemType(item, out itemType) && itemType > 0)
            {
                profile.EquippedMountItemType = itemType;
                if (TryReadItemMountType(item, out mountType) && mountType >= 0)
                {
                    profile.EquippedMountType = mountType;
                    if (TryResolveMountCanFly(mountType, out canFly))
                    {
                        profile.EquippedMountCanFly = canFly;
                        profile.EquippedMountCanFlyKnown = true;
                    }

                    if (TryResolveMountNoFallDamage(mountType, out bool noFallDamage))
                    {
                        profile.EquippedMountNoFallDamage = noFallDamage;
                        profile.EquippedMountNoFallDamageKnown = true;
                    }
                }
            }

            profile.HasEquippedFlyingMountOpportunity = profile.PlayerControllable &&
                                                        !profile.MountActive &&
                                                        profile.EquippedMountItemType > 0 &&
                                                        profile.EquippedMountCanFlyKnown &&
                                                        profile.EquippedMountCanFly;
            profile.HasEquippedSafeMountOpportunity = profile.PlayerControllable &&
                                                      !profile.MountActive &&
                                                      profile.EquippedMountItemType > 0 &&
                                                      profile.EquippedMountCanFlyKnown &&
                                                      !profile.EquippedMountCanFly &&
                                                      profile.EquippedMountNoFallDamageKnown &&
                                                      profile.EquippedMountNoFallDamage;

            if (TryGetItemAt(miscEquips, 4, out item) &&
                TryReadItemType(item, out itemType) &&
                itemType > 0 &&
                IsGrappleItem(item, itemType))
            {
                profile.HasEquippedGrapple = true;
                profile.EquippedGrappleItemType = itemType;
                profile.EquippedGrappleShootSpeed = TryReadItemShootSpeed(item, out var equippedShootSpeed) ? equippedShootSpeed : 0f;
                profile.EquippedGrappleProjectileType = TryReadItemShoot(item, out var equippedShoot) ? equippedShoot : 0;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory != null)
            {
                var maxQuickGrappleInventorySlot = Math.Min(inventory.Count, 58);
                for (var index = 0; index < maxQuickGrappleInventorySlot; index++)
                {
                    item = inventory[index];
                    if (TryReadItemType(item, out itemType) && itemType > 0 && IsGrappleItem(item, itemType))
                    {
                        profile.HasInventoryGrapple = true;
                        profile.InventoryGrappleItemType = itemType;
                        profile.InventoryGrappleShootSpeed = TryReadItemShootSpeed(item, out var inventoryShootSpeed) ? inventoryShootSpeed : 0f;
                        profile.InventoryGrappleProjectileType = TryReadItemShoot(item, out var inventoryShoot) ? inventoryShoot : 0;
                        break;
                    }
                }
            }

            profile.HasAnyGrapple = profile.HasEquippedGrapple || profile.HasInventoryGrapple;
        }

        private static bool TryGetItemAt(IList items, int index, out object item)
        {
            item = null;
            if (items == null || index < 0 || index >= items.Count)
            {
                return false;
            }

            item = items[index];
            return item != null;
        }

        private static bool TryReadItemType(object item, out int itemType)
        {
            itemType = 0;
            if (item == null)
            {
                return false;
            }

            if (!TryGetIntByNames(item, out itemType, "type", "Type", "netID", "NetID"))
            {
                return false;
            }

            int stack;
            if (TryGetIntByNames(item, out stack, "stack", "Stack") && stack <= 0)
            {
                return false;
            }

            bool isAir;
            if (TryGetBoolByNames(item, out isAir, "IsAir", "isAir") && isAir)
            {
                return false;
            }

            return itemType > 0;
        }

        private static bool TryReadItemShootSpeed(object item, out float shootSpeed)
        {
            return TryGetFloatByNames(item, out shootSpeed, "shootSpeed", "ShootSpeed");
        }

        private static bool TryReadItemShoot(object item, out int shoot)
        {
            return TryGetIntByNames(item, out shoot, "shoot", "Shoot");
        }

        private static bool TryReadItemMountType(object item, out int mountType)
        {
            mountType = -1;
            if (item == null)
            {
                return false;
            }

            if (!TryGetIntByNames(item, out mountType, "mountType", "MountType", "mountId", "MountId"))
            {
                return false;
            }

            return mountType >= 0;
        }

        private static bool TryResolveMountCanFly(int mountType, out bool canFly)
        {
            canFly = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                bool boolValue;
                int intValue;
                float floatValue;
                if (TryGetIntByNames(data, out intValue, "flightTimeMax", "FlightTimeMax") && intValue > 0)
                {
                    canFly = true;
                    return true;
                }

                if (TryGetBoolByNames(data, out boolValue, "usesHover", "UsesHover", "canFly", "CanFly") && boolValue)
                {
                    canFly = true;
                    return true;
                }

                if (TryGetFloatByNames(data, out floatValue, "flySpeed", "FlySpeed") && floatValue > 0.1f)
                {
                    canFly = true;
                    return true;
                }

                canFly = false;
                return true;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Mount fly detection failed: " + error.Message);
                canFly = false;
                return false;
            }
        }

        private static bool TryResolveMountNoFallDamage(int mountType, out bool noFallDamage)
        {
            noFallDamage = false;
            if (mountType < 0)
            {
                return false;
            }

            try
            {
                var mountTypeType = FindType("Terraria.Mount");
                var mounts = mountTypeType == null ? null : GetStatic(mountTypeType, "mounts") as Array;
                if (mounts == null || mountType >= mounts.Length)
                {
                    return false;
                }

                var data = mounts.GetValue(mountType);
                if (data == null)
                {
                    return false;
                }

                float fallDamage;
                if (TryGetFloatByNames(data, out fallDamage, "fallDamage", "FallDamage"))
                {
                    noFallDamage = fallDamage <= 0.001f;
                    return true;
                }

                return false;
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Mount no-fall detection failed: " + error.Message);
                noFallDamage = false;
                return false;
            }
        }

        private static bool IsGrappleItem(object item, int itemType)
        {
            if (itemType <= 0)
            {
                return false;
            }

            int shoot;
            if (TryGetIntByNames(item, out shoot, "shoot", "Shoot") && IsHookProjectile(shoot))
            {
                return true;
            }

            try
            {
                var setsType = FindType("Terraria.ID.ItemID+Sets");
                var flags = setsType == null ? null : GetStatic(setsType, "IsAGrapplingHook") as Array;
                if (flags != null && itemType >= 0 && itemType < flags.Length)
                {
                    var raw = flags.GetValue(itemType);
                    if (raw is bool)
                    {
                        return (bool)raw;
                    }
                }
            }
            catch
            {
            }

            if (TryGetIntByNames(item, out shoot, "shoot", "Shoot") && shoot > 0)
            {
                var name = ReadItemName(item);
                if (ContainsGrappleNameHint(name))
                {
                    return true;
                }
            }

            return ContainsGrappleNameHint(ReadItemName(item));
        }

        private static bool IsHookProjectile(int projectileType)
        {
            if (projectileType <= 0)
            {
                return false;
            }

            try
            {
                var mainType = TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
                var flags = mainType == null ? null : GetStatic(mainType, "projHook") as Array;
                if (flags == null || projectileType < 0 || projectileType >= flags.Length)
                {
                    return false;
                }

                var raw = flags.GetValue(projectileType);
                return raw is bool && (bool)raw;
            }
            catch
            {
                return false;
            }
        }

        private static string ReadItemName(object item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var value = GetMember(item, "Name") ?? GetMember(item, "HoverName") ?? GetMember(item, "name");
            return value == null ? string.Empty : value.ToString();
        }

        private static bool ContainsGrappleNameHint(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.IndexOf("hook", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("grapple", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("钩", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("抓", StringComparison.Ordinal) >= 0;
        }

        private static bool EnsureChangeDirMethod(object player)
        {
            if (_changeDirResolved)
            {
                return _changeDirMethod != null;
            }

            _changeDirResolved = true;
            if (player == null)
            {
                return false;
            }

            _changeDirMethod = player.GetType().GetMethod(
                "ChangeDir",
                InstanceMemberFlags,
                null,
                new[] { typeof(int) },
                null);
            if (_changeDirMethod == null)
            {
                Logger.Debug("TerrariaInputCompat", "Player.ChangeDir(int) not found; direction field fallback may be used.");
            }

            return _changeDirMethod != null;
        }
    }
}
