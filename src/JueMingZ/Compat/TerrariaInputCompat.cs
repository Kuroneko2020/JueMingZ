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
    public static class TerrariaInputCompat
    {
        private const BindingFlags InstanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int VkLeftButton = 0x01;
        private static string _lastError = string.Empty;
        private static bool _playerInputMouseResolved;
        private static Type _playerInputType;
        private static FieldInfo _playerInputMouseXField;
        private static FieldInfo _playerInputMouseYField;
        private static PropertyInfo _playerInputMouseXProperty;
        private static PropertyInfo _playerInputMouseYProperty;
        private static bool _tileTargetResolved;
        private static FieldInfo _tileTargetXField;
        private static FieldInfo _tileTargetYField;
        private static bool _tileInteractionUseResolved;
        private static MethodInfo _tileInteractionUseMethod;
        private static bool _tileInteractionCheckResolved;
        private static MethodInfo _tileInteractionCheckMethod;
        private static bool _changeDirResolved;
        private static MethodInfo _changeDirMethod;
        private static bool _airJumpFieldsResolved;
        private static FieldInfo[] _airJumpFields = new FieldInfo[0];
        private static bool _bootFlyingMethodResolved;
        private static MethodInfo _bootFlyingMethod;
        private static bool _playerInputJumpTriggersResolved;
        private static FieldInfo _playerInputTriggersField;
        private static PropertyInfo _playerInputTriggersProperty;
        private static readonly object TileInteractionOverrideSync = new object();
        private static Guid _tileInteractionOverrideRequestId = Guid.Empty;
        private static int _tileInteractionOverrideTileX = -1;
        private static int _tileInteractionOverrideTileY = -1;
        private static DateTime _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
        private static string _lastTileInteractionOverrideMessage = string.Empty;
        private static readonly object AutoFacingOverrideSync = new object();
        private static Guid _autoFacingOverrideRequestId = Guid.Empty;
        private static int _autoFacingOverrideDirection;
        private static int _autoFacingOverrideSelectedSlot = -1;
        private static int _autoFacingOverrideItemType;
        private static DateTime _autoFacingOverrideExpiresUtc = DateTime.MinValue;
        private static string _lastAutoFacingOverrideMessage = string.Empty;
        private static readonly object AutoClickerUseItemSuppressionSync = new object();
        private static DateTime _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;

        public static bool InputCompatReady { get; private set; }
        public static bool SelectedItemGetterReady { get { return TerrariaPlayerSelectionCompat.SelectedItemGetterReady; } }
        public static bool SelectedItemSelectorReady { get { return TerrariaPlayerSelectionCompat.SelectedItemSelectorReady; } }
        public static bool SelectedItemAccessorReady { get { return TerrariaPlayerSelectionCompat.SelectedItemAccessorReady; } }
        public static string LastSelectionMethod { get { return TerrariaPlayerSelectionCompat.LastSelectionMethod; } }
        public static string PlayerTypeName { get; private set; } = string.Empty;
        public static string LastInputCompatError { get { return _lastError; } }
        public static string LastTileInteractionOverrideMessage { get { return _lastTileInteractionOverrideMessage; } }
        public static string LastAutoFacingOverrideMessage { get { return _lastAutoFacingOverrideMessage; } }

        public sealed class ScopedUseItemTakeover : IDisposable
        {
            private bool _disposed;

            internal ScopedUseItemTakeover(object player, bool pressed, string scopeName)
            {
                Player = player;
                Pressed = pressed;
                ScopeName = scopeName ?? string.Empty;
            }

            internal object Player { get; private set; }
            public bool Captured { get; internal set; }
            public bool Applied { get; internal set; }
            public bool Pressed { get; private set; }
            public string ScopeName { get; private set; }
            public bool PlayerControlUseItemCaptured { get; internal set; }
            public bool PlayerControlUseItem { get; internal set; }
            public bool PlayerReleaseUseItemCaptured { get; internal set; }
            public bool PlayerReleaseUseItem { get; internal set; }
            public bool PlayerChannelCaptured { get; internal set; }
            public bool PlayerChannel { get; internal set; }
            public bool MainMouseLeftCaptured { get; internal set; }
            public bool MainMouseLeft { get; internal set; }
            public bool MainMouseLeftReleaseCaptured { get; internal set; }
            public bool MainMouseLeftRelease { get; internal set; }
            public bool SuppressedPhysicalInput { get; internal set; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                TerrariaInputCompat.TryRestoreScopedUseItemTakeover(this);
            }
        }

        public static bool TryGetLocalPlayer(out object player)
        {
            player = null;
            try
            {
                if (!TerrariaMemberCache.EnsureInitializedLateOnly())
                {
                    return Fail(TerrariaMemberCache.LastError);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                player = GetStatic(mainType, "LocalPlayer");
                if (IsTerrariaPlayer(player))
                {
                    InputCompatReady = true;
                    return true;
                }

                var players = GetStatic(mainType, "player") as System.Collections.IList;
                var myPlayer = 0;
                var rawMyPlayer = GetStatic(mainType, "myPlayer");
                if (rawMyPlayer != null)
                {
                    myPlayer = Convert.ToInt32(rawMyPlayer);
                }

                if (players == null || myPlayer < 0 || myPlayer >= players.Count)
                {
                    return Fail("Local player unavailable.");
                }

                player = players[myPlayer];
                if (!IsTerrariaPlayer(player))
                {
                    return Fail("Local player object is not Terraria.Player.");
                }

                InputCompatReady = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        public static bool TryGetSelectedItem(object player, out int selectedItem)
        {
            var ok = TerrariaPlayerSelectionCompat.TryGetSelectedItem(player, out selectedItem);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TrySetSelectedItem(object player, int slot)
        {
            return TrySelectInventorySlot(player, slot);
        }

        public static bool TrySelectInventorySlot(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TrySelectInventorySlot(player, slot);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TryRequestInventorySlotSelection(object player, int slot, out bool selectedImmediately)
        {
            selectedImmediately = false;
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TryRequestInventorySlotSelection(player, slot, out selectedImmediately);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool TryForceInventorySlotSelection(object player, int slot)
        {
            if (!IsSupportedItemUseSlot(slot))
            {
                return Fail("Item use slot out of range: " + slot);
            }

            var ok = TerrariaPlayerSelectionCompat.TryForceInventorySlotSelection(player, slot);
            return ok ? ClearSelectionError() : SelectionFail(TerrariaPlayerSelectionCompat.LastError);
        }

        public static bool IsSupportedItemUseSlot(int slot)
        {
            return (slot >= 0 && slot < 50) || slot == 58;
        }

        public static bool TrySetMouseScreenPosition(int x, int y)
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Terraria.Main unavailable.");
            }

            // Controlled input write: Main.mouseX / Main.mouseY are only written here.
            var ok = SetStatic(mainType, "mouseX", x) & SetStatic(mainType, "mouseY", y);
            TrySetPlayerInputMousePosition(x, y);
            float screenX;
            float screenY;
            if (TryGetScreenPosition(out screenX, out screenY))
            {
                TrySetTileTargetWorldPosition(x + screenX, y + screenY);
            }

            return ok;
        }

        public static bool TryGetScreenPosition(out float x, out float y)
        {
            x = 0f;
            y = 0f;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Terraria.Main unavailable.");
            }

            var screenPosition = GetStatic(mainType, "screenPosition");
            return TryReadVector2(screenPosition, out x, out y);
        }

        public static bool TryWorldToScreen(float worldX, float worldY, out int screenX, out int screenY)
        {
            screenX = 0;
            screenY = 0;
            if (!TryGetScreenPosition(out var screenXFloat, out var screenYFloat))
            {
                return false;
            }

            screenX = (int)Math.Round(worldX - screenXFloat);
            screenY = (int)Math.Round(worldY - screenYFloat);
            return true;
        }

        public static bool TrySetMouseWorldPosition(float worldX, float worldY)
        {
            int screenX;
            int screenY;
            if (!TryWorldToScreen(worldX, worldY, out screenX, out screenY))
            {
                return false;
            }

            var ok = TrySetMouseScreenPosition(screenX, screenY);
            TrySetTileTargetWorldPosition(worldX, worldY);
            return ok;
        }

        public static bool TrySetUseItem(object player, bool pressed)
        {
            // Controlled input write: player.controlUseItem / player.releaseUseItem.
            var ok = SetMember(player, "controlUseItem", pressed);
            if (pressed)
            {
                SetMember(player, "releaseUseItem", false);
            }

            return ok;
        }

        public static bool TryApplyUseItemTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed);

            return ok ? ClearInputError() : Fail("Cannot apply use item takeover input override.");
        }

        public static bool TryApplyUseItemClickTakeover(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item click takeover: player unavailable.");
            }

            var ok = ApplyUseItemTakeoverFields(player, pressed, false);

            return ok ? ClearInputError() : Fail("Cannot apply use item click takeover input override.");
        }

        public static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, true, out takeover);
        }

        public static bool TryBeginScopedUseItemClickTakeover(object player, bool pressed, string scopeName, out ScopedUseItemTakeover takeover)
        {
            return TryBeginScopedUseItemTakeover(player, pressed, scopeName, false, out takeover);
        }

        private static bool TryBeginScopedUseItemTakeover(object player, bool pressed, string scopeName, bool applyChannel, out ScopedUseItemTakeover takeover)
        {
            takeover = null;
            if (player == null)
            {
                return Fail("Cannot begin scoped use item takeover: player unavailable.");
            }

            var state = new ScopedUseItemTakeover(player, pressed, scopeName);
            bool value;
            if (TryGetBool(player, "controlUseItem", out value))
            {
                state.PlayerControlUseItemCaptured = true;
                state.PlayerControlUseItem = value;
            }

            if (TryGetBool(player, "releaseUseItem", out value))
            {
                state.PlayerReleaseUseItemCaptured = true;
                state.PlayerReleaseUseItem = value;
            }

            if (applyChannel && TryGetBool(player, "channel", out value))
            {
                state.PlayerChannelCaptured = true;
                state.PlayerChannel = value;
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (TryGetStaticBool(mainType, "mouseLeft", out value))
                {
                    state.MainMouseLeftCaptured = true;
                    state.MainMouseLeft = value;
                }

                if (TryGetStaticBool(mainType, "mouseLeftRelease", out value))
                {
                    state.MainMouseLeftReleaseCaptured = true;
                    state.MainMouseLeftRelease = value;
                }
            }

            state.Captured = state.PlayerControlUseItemCaptured ||
                             state.PlayerReleaseUseItemCaptured ||
                             state.PlayerChannelCaptured ||
                             state.MainMouseLeftCaptured ||
                             state.MainMouseLeftReleaseCaptured;
            state.SuppressedPhysicalInput = !pressed &&
                                            ((state.PlayerControlUseItemCaptured && state.PlayerControlUseItem) ||
                                             (state.MainMouseLeftCaptured && state.MainMouseLeft));

            if (!state.PlayerControlUseItemCaptured || !state.PlayerReleaseUseItemCaptured)
            {
                return Fail("Cannot begin scoped use item takeover: player use item fields unavailable.");
            }

            if (!ApplyUseItemTakeoverFields(player, pressed, applyChannel))
            {
                TryRestoreScopedUseItemTakeover(state);
                return Fail("Cannot begin scoped use item takeover: " + LastInputCompatError);
            }

            state.Applied = true;
            takeover = state;
            return ClearInputError();
        }

        public static bool TryRestoreScopedUseItemTakeover(ScopedUseItemTakeover takeover)
        {
            if (takeover == null || !takeover.Captured || takeover.Player == null)
            {
                return false;
            }

            var ok = true;
            if (takeover.PlayerControlUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "controlUseItem", takeover.PlayerControlUseItem);
            }

            if (takeover.PlayerReleaseUseItemCaptured)
            {
                ok &= SetMember(takeover.Player, "releaseUseItem", takeover.PlayerReleaseUseItem);
            }

            if (takeover.PlayerChannelCaptured)
            {
                ok &= TrySetMemberIfExists(takeover.Player, "channel", takeover.PlayerChannel);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                if (takeover.MainMouseLeftCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeft", takeover.MainMouseLeft);
                }

                if (takeover.MainMouseLeftReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeftRelease", takeover.MainMouseLeftRelease);
                }
            }

            return ok ? ClearInputError() : Fail("Cannot restore scoped use item takeover.");
        }

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

        public static bool TryReadTextInputFocus(out bool focused, out string reason)
        {
            focused = false;
            reason = string.Empty;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                focused = true;
                reason = "mainTypeUnavailable";
                return false;
            }

            bool boolValue;
            if ((TryGetStaticBool(mainType, "chatMode", out boolValue) && boolValue) ||
                (TryGetStaticBool(mainType, "drawingPlayerChat", out boolValue) && boolValue))
            {
                focused = true;
                reason = "chat";
                return true;
            }

            var playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (TryGetStaticBool(playerInputType, "WritingText", out boolValue) && boolValue)
            {
                focused = true;
                reason = "playerInputWritingText";
                return true;
            }

            if (GetStatic(mainType, "CurrentInputTextTakerOverride") != null)
            {
                focused = true;
                reason = "currentInputTextTakerOverride";
                return true;
            }

            if (IsStaticTextEditActive(mainType, "editSign"))
            {
                focused = true;
                reason = "editSign";
                return true;
            }

            if (IsStaticTextEditActive(mainType, "editChest"))
            {
                focused = true;
                reason = "editChest";
                return true;
            }

            return true;
        }

        private static bool IsStaticTextEditActive(Type type, string name)
        {
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                return (bool)raw;
            }

            try
            {
                return Convert.ToInt32(raw) >= 0;
            }
            catch
            {
                return false;
            }
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

        public static bool TryReadUseItemHeld(object player, out bool held)
        {
            held = false;
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return ClearInputError();
            }

            if (player == null)
            {
                return Fail("Cannot read use item state: player unavailable.");
            }

            if (TryGetBool(player, "controlUseItem", out held))
            {
                held = held || IsSuppressedUseItemHeld() || IsLeftButtonDownFallback();
                return ClearInputError();
            }

            if (IsSuppressedUseItemHeld())
            {
                held = true;
                return ClearInputError();
            }

            if (IsLeftButtonDownFallback())
            {
                held = true;
                return ClearInputError();
            }

            return Fail("Cannot read player.controlUseItem.");
        }

        public static bool TryReadUseItemReleased(object player, out bool released)
        {
            released = false;
            if (player == null)
            {
                return Fail("Cannot read use item release state: player unavailable.");
            }

            return TryGetBool(player, "releaseUseItem", out released)
                ? ClearInputError()
                : Fail("Cannot read player.releaseUseItem.");
        }

        public static bool TryReadDelayUseItem(object player, out bool delayUseItem)
        {
            delayUseItem = false;
            if (player == null)
            {
                return Fail("Cannot read delayUseItem: player unavailable.");
            }

            if (TryGetBool(player, "delayUseItem", out delayUseItem))
            {
                return ClearInputError();
            }

            delayUseItem = false;
            return ClearInputError();
        }

        public static bool TryReadCombatAimUseInputSnapshot(object player, out CombatAimUseInputSnapshot snapshot)
        {
            snapshot = new CombatAimUseInputSnapshot();
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                snapshot.Available = true;
                snapshot.UseItemHeld = false;
                snapshot.UseItemReleased = true;
                snapshot.Reason = "gameInputUnavailable";
                return ClearInputError();
            }

            if (player == null)
            {
                snapshot.Reason = "playerUnavailable";
                return Fail("Cannot read combat aim use input: player unavailable.");
            }

            try
            {
                bool held;
                if (!TryGetBool(player, "controlUseItem", out held))
                {
                    snapshot.Reason = "useItemHeldUnavailable";
                    return Fail("Cannot read player.controlUseItem.");
                }

                bool released;
                if (!TryGetBool(player, "releaseUseItem", out released))
                {
                    snapshot.Reason = "useItemReleasedUnavailable";
                    return Fail("Cannot read player.releaseUseItem.");
                }

                int itemAnimation;
                int itemTime;
                TryGetInt(player, "itemAnimation", out itemAnimation);
                TryGetInt(player, "itemTime", out itemTime);

                int selectedSlot;
                TryGetSelectedItem(player, out selectedSlot);

                var itemType = 0;
                var inventory = GetMember(player, "inventory") as IList;
                if (inventory != null && selectedSlot >= 0 && selectedSlot < inventory.Count)
                {
                    var item = inventory[selectedSlot];
                    if (item != null)
                    {
                        TryGetInt(item, "type", out itemType);
                    }
                }

                long gameUpdateCount;
                TryReadGameUpdateCount(out gameUpdateCount);

                snapshot.Available = true;
                snapshot.UseItemHeld = held || IsSuppressedUseItemHeld();
                snapshot.UseItemReleased = released;
                snapshot.ItemAnimation = itemAnimation;
                snapshot.ItemTime = itemTime;
                snapshot.SelectedSlot = selectedSlot;
                snapshot.ItemType = itemType;
                snapshot.GameUpdateCount = gameUpdateCount;
                snapshot.Reason = string.Empty;
                return ClearInputError();
            }
            catch (Exception error)
            {
                snapshot.Reason = "snapshotFailed:" + error.Message;
                return Fail("Read combat aim use input failed: " + error.Message);
            }
        }

        public static bool TryReadPhysicalUseItemHeld(object player, out bool held)
        {
            held = false;
            if (player == null)
            {
                return Fail("Cannot read physical use item state: player unavailable.");
            }

            bool controlUseItem;
            TryGetBool(player, "controlUseItem", out controlUseItem);

            bool mainMouseLeft;
            TryGetStaticBool(TerrariaRuntimeTypes.MainType, "mouseLeft", out mainMouseLeft);

            held = controlUseItem || mainMouseLeft || IsLeftButtonDownFallback();
            return ClearInputError();
        }

        public static bool TryReadPhysicalMouseLeftHeld(out bool held)
        {
            held = IsLeftButtonDownFallback();
            try
            {
                var playerInputType = FindType("Terraria.GameInput.PlayerInput");
                if (playerInputType == null)
                {
                    return ClearInputError();
                }

                var triggers = GetStatic(playerInputType, "Triggers");
                var current = triggers == null ? null : GetMember(triggers, "Current");
                bool mouseLeft;
                if (current != null && TryGetBool(current, "MouseLeft", out mouseLeft))
                {
                    held = held || mouseLeft;
                }

                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Cannot read physical mouse left state: " + error.Message);
            }
        }

        public static bool TrySuppressHeldUseItemForPerfectRevolver(object player)
        {
            var ok = SuppressHeldUseItemForQueuedCombat(player);
            ArmPerfectRevolverSuppressedUseItemHeld();
            return ok;
        }

        public static void ClearPerfectRevolverSuppressedUseItem()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;
            }
        }

        public static bool TryReadGameUpdateCount(out long gameUpdateCount)
        {
            gameUpdateCount = 0;
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return Fail("Cannot read Main.GameUpdateCount: Terraria.Main unavailable.");
            }

            try
            {
                var raw = GetStatic(mainType, "GameUpdateCount");
                if (raw == null)
                {
                    raw = GetStatic(mainType, "gameUpdateCount");
                }

                if (raw == null)
                {
                    return Fail("Cannot read Main.GameUpdateCount.");
                }

                gameUpdateCount = Convert.ToInt64(raw);
                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Read Main.GameUpdateCount failed: " + error.Message);
            }
        }

        public static TerrariaUiInputContext ReadUiInputContext(object player)
        {
            var context = new TerrariaUiInputContext();
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                context.MainTypeUnavailable = true;
                return context;
            }

            bool value;
            context.GameMenu = TryGetStaticBool(mainType, "gameMenu", out value) && value;
            context.ChatOpen = (TryGetStaticBool(mainType, "chatMode", out value) && value) ||
                               (TryGetStaticBool(mainType, "drawingPlayerChat", out value) && value);

            var npcChatText = GetStatic(mainType, "npcChatText");
            context.NpcChatOpen = npcChatText != null && !string.IsNullOrEmpty(npcChatText.ToString());
            context.PlayerInventoryOpen = TryGetStaticBool(mainType, "playerInventory", out value) && value;

            int chest;
            context.ChestOpen = TryGetInt(player, "chest", out chest) && chest >= 0;
            context.PlayerMouseInterface = TryGetBool(player, "mouseInterface", out value) && value;
            context.MainMouseInterface = TryGetStaticBool(mainType, "mouseInterface", out value) && value;
            context.MainBlockMouse = TryGetStaticBool(mainType, "blockMouse", out value) && value;
            return context;
        }

        public static bool IsMouseInputCapturedByUi(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            reason = context.MouseCaptureReason;
            return context.MouseCapturedByUi;
        }

        public static bool IsInputBlockingUiActive(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            if (context.GameMenu)
            {
                reason = "gameMenu";
                return true;
            }

            if (context.MouseCapturedByUi)
            {
                reason = context.MouseCaptureReason;
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static bool TryIsLocalPlayer(object player)
        {
            if (player == null)
            {
                return false;
            }

            object localPlayer;
            if (TryGetLocalPlayer(out localPlayer) && ReferenceEquals(localPlayer, player))
            {
                return true;
            }

            try
            {
                int whoAmI;
                var rawMyPlayer = GetStatic(TerrariaRuntimeTypes.MainType, "myPlayer");
                if (rawMyPlayer != null && TryGetInt(player, "whoAmI", out whoAmI))
                {
                    return whoAmI == Convert.ToInt32(rawMyPlayer);
                }
            }
            catch
            {
            }

            return false;
        }

        public static bool TryCaptureUseItemInputState(object player, out UseItemInputState state)
        {
            state = new UseItemInputState();
            if (player == null)
            {
                return Fail("Cannot capture use item input: player unavailable.");
            }

            try
            {
                state.MouseX = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseX", 0);
                state.MouseY = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseY", 0);

                if (EnsurePlayerInputMouseAccessors())
                {
                    int playerInputX;
                    int playerInputY;
                    if (TryGetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, out playerInputX) &&
                        TryGetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, out playerInputY))
                    {
                        state.PlayerInputMouseCaptured = true;
                        state.PlayerInputMouseX = playerInputX;
                        state.PlayerInputMouseY = playerInputY;
                    }
                }

                if (EnsureTileTargetAccessors())
                {
                    int tileX;
                    int tileY;
                    if (TryGetOptionalStatic(_tileTargetXField, null, out tileX) &&
                        TryGetOptionalStatic(_tileTargetYField, null, out tileY))
                    {
                        state.TileTargetCaptured = true;
                        state.TileTargetX = tileX;
                        state.TileTargetY = tileY;
                    }
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                bool mouseButton;
                if (TryGetStaticBool(mainType, "mouseLeft", out mouseButton))
                {
                    state.MainMouseLeftCaptured = true;
                    state.MainMouseLeft = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseRight", out mouseButton))
                {
                    state.MainMouseRightCaptured = true;
                    state.MainMouseRight = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseLeftRelease", out mouseButton))
                {
                    state.MainMouseLeftReleaseCaptured = true;
                    state.MainMouseLeftRelease = mouseButton;
                }

                if (TryGetStaticBool(mainType, "mouseRightRelease", out mouseButton))
                {
                    state.MainMouseRightReleaseCaptured = true;
                    state.MainMouseRightRelease = mouseButton;
                }

                int selectedSlot;
                if (!TryGetSelectedItem(player, out selectedSlot))
                {
                    return false;
                }

                state.SelectedSlot = selectedSlot;
                TryGetBool(player, "controlUseItem", out var held);
                TryGetBool(player, "releaseUseItem", out var released);
                state.UseItemHeld = held;
                state.UseItemReleased = released;
                state.Captured = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail("Capture use item input failed: " + error.Message);
            }
        }

        public static bool TryApplyUseItemOverrideForItemCheck(object player, ItemUseBridgeContext context)
        {
            if (player == null || context == null)
            {
                return Fail("Cannot apply use item override: missing player or context.");
            }

            var ok = true;
            if (context.SkipSelectInItemCheck)
            {
                int selectedSlot;
                if (!TryGetSelectedItem(player, out selectedSlot))
                {
                    return false;
                }

                var expectedSlot = context.ExpectedSelectedSlot >= 0
                    ? context.ExpectedSelectedSlot
                    : context.TargetSlot;
                if (expectedSlot >= 0 && selectedSlot != expectedSlot)
                {
                    return Fail("ItemCheck reached but selectedSlot was not targetSlot. selectedSlot=" +
                                selectedSlot + ", expected=" + expectedSlot + ".");
                }
            }
            else if (context.TargetSlot >= 0)
            {
                ok &= TrySelectInventorySlot(player, context.TargetSlot);
                if (ok && context.TargetSlot != 58)
                {
                    int selectedSlot;
                    if (!TryGetSelectedItem(player, out selectedSlot))
                    {
                        return false;
                    }

                    if (selectedSlot != context.TargetSlot)
                    {
                        return Fail(
                            "ItemCheck input override failed to select target slot. selectedSlot=" +
                            selectedSlot + ", targetSlot=" + context.TargetSlot + ".");
                    }
                }
            }

            if (context.HasMouseWorldTarget)
            {
                ok &= TrySetMouseWorldPosition(context.MouseWorldX, context.MouseWorldY);
            }
            else if (context.HasMouseScreenTarget)
            {
                ok &= TrySetMouseScreenPosition(context.MouseScreenX, context.MouseScreenY);
            }

            if (context.HasMouseWorldTarget || context.HasMouseScreenTarget)
            {
                SuppressSmartInteractionState();
            }

            // Controlled input write: player.controlUseItem / player.releaseUseItem for Player.ItemCheck bridge.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            if (context.ApplyMainMouseLeftForItemCheck)
            {
                TryApplyMainMouseLeftForItemUseBridge();
            }
            else
            {
                TrySuppressMainMouseButtonsForItemUseBridge();
            }
            return ok;
        }

        public static bool TryApplyAutoHarvestSustainedUseForItemCheck(object player, int targetSlot, float mouseWorldX, float mouseWorldY, int direction)
        {
            if (player == null)
            {
                return Fail("Cannot apply auto harvest sustained use: player unavailable.");
            }

            var ok = true;
            ok &= TrySelectInventorySlot(player, targetSlot);
            ok &= TrySetMouseWorldPosition(mouseWorldX, mouseWorldY);
            SuppressSmartInteractionState();

            if (direction != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                TryChangePlayerDirection(player, direction, false, out beforeDirection, out afterDirection, out method);
            }

            // Controlled input write: auto harvest RawInput session supplies one scoped Player.ItemCheck use tick.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            TryApplyMainMouseLeftForItemUseBridge();
            return ok ? ClearInputError() : Fail("Cannot apply auto harvest sustained use input override.");
        }

        public static bool TryApplyAutoCaptureCritterSustainedUseForItemCheck(object player, int targetSlot, float mouseWorldX, float mouseWorldY, int direction)
        {
            if (player == null)
            {
                return Fail("Cannot apply auto capture critter sustained use: player unavailable.");
            }

            var ok = true;
            ok &= TrySelectInventorySlot(player, targetSlot);
            ok &= TrySetMouseWorldPosition(mouseWorldX, mouseWorldY);
            SuppressSmartInteractionState();

            if (direction != 0)
            {
                int beforeDirection;
                int afterDirection;
                string method;
                ok &= TryChangePlayerDirection(player, direction, true, out beforeDirection, out afterDirection, out method);
            }

            // Controlled input write: auto capture supplies one scoped Player.ItemCheck use tick while keeping the bug net selected.
            ok &= SetMember(player, "controlUseItem", true);
            ok &= SetMember(player, "releaseUseItem", true);
            TryApplyMainMouseLeftForItemUseBridge();
            return ok ? ClearInputError() : Fail("Cannot apply auto capture critter sustained use input override.");
        }

        public static bool TryApplyUseItemPulseForItemCheck(object player, bool pressed)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item pulse: player unavailable.");
            }

            // Controlled input write: player.controlUseItem / player.releaseUseItem for queued RawInput pulse.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", true);
            return ok ? ClearInputError() : Fail("Cannot apply use item pulse input override.");
        }

        public static bool TryApplyUseItemReleaseForItemCheck(object player)
        {
            if (player == null)
            {
                return Fail("Cannot apply use item release: player unavailable.");
            }

            // Controlled input write: release-only phase for vanilla delayUseItem gates.
            var ok = SetMember(player, "controlUseItem", false);
            ok &= SetMember(player, "releaseUseItem", true);
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", false);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
                TrySetStaticIfExists(mainType, "mouseRight", false);
                TrySetStaticIfExists(mainType, "mouseRightRelease", false);
            }

            return ok ? ClearInputError() : Fail("Cannot apply use item release input override.");
        }

        public static bool TryRestoreUseItemInputState(object player, UseItemInputState state)
        {
            return TryRestoreUseItemInputState(player, state, -1);
        }

        public static bool TryRestoreUseItemInputState(object player, UseItemInputState state, int restoreSelectedSlotOverride)
        {
            if (player == null || state == null || !state.Captured)
            {
                return false;
            }

            var ok = true;
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseX", state.MouseX);
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseY", state.MouseY);

            if (state.PlayerInputMouseCaptured)
            {
                TrySetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, state.PlayerInputMouseX);
                TrySetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, state.PlayerInputMouseY);
            }

            if (state.TileTargetCaptured)
            {
                TrySetOptionalStatic(_tileTargetXField, null, state.TileTargetX);
                TrySetOptionalStatic(_tileTargetYField, null, state.TileTargetY);
            }

            var restoreSlot = IsSupportedItemUseSlot(restoreSelectedSlotOverride)
                ? restoreSelectedSlotOverride
                : state.SelectedSlot;
            if (IsSupportedItemUseSlot(restoreSlot))
            {
                ok &= TrySelectInventorySlot(player, restoreSlot);
            }

            // Controlled input write: restore player.controlUseItem / player.releaseUseItem after Player.ItemCheck bridge.
            ok &= SetMember(player, "controlUseItem", state.UseItemHeld);
            ok &= SetMember(player, "releaseUseItem", state.UseItemReleased);
            if (state.MainMouseLeftCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseLeft", state.MainMouseLeft);
            }

            if (state.MainMouseRightCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseRight", state.MainMouseRight);
            }

            if (state.MainMouseLeftReleaseCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseLeftRelease", state.MainMouseLeftRelease);
            }

            if (state.MainMouseRightReleaseCaptured)
            {
                ok &= TrySetStaticIfExists(TerrariaRuntimeTypes.MainType, "mouseRightRelease", state.MainMouseRightRelease);
            }

            return ok;
        }

        public static bool TryRestoreUseItemButtonInputState(object player, UseItemInputState state)
        {
            if (player == null || state == null || !state.Captured)
            {
                return false;
            }

            var ok = true;
            ok &= SetMember(player, "controlUseItem", state.UseItemHeld);
            ok &= SetMember(player, "releaseUseItem", state.UseItemReleased);

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                if (state.MainMouseLeftCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeft", state.MainMouseLeft);
                }

                if (state.MainMouseRightCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRight", state.MainMouseRight);
                }

                if (state.MainMouseLeftReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseLeftRelease", state.MainMouseLeftRelease);
                }

                if (state.MainMouseRightReleaseCaptured)
                {
                    ok &= TrySetStaticIfExists(mainType, "mouseRightRelease", state.MainMouseRightRelease);
                }
            }

            return ok ? ClearInputError() : Fail("Cannot restore use item button input state.");
        }

        public static bool TryCaptureMouseTargetState(out MouseTargetInputState state)
        {
            return TryCaptureMouseTargetState(null, out state);
        }

        public static bool TryCaptureMouseTargetState(object player, out MouseTargetInputState state)
        {
            state = new MouseTargetInputState();
            try
            {
                state.MouseX = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseX", 0);
                state.MouseY = GetStaticInt(TerrariaRuntimeTypes.MainType, "mouseY", 0);

                if (EnsurePlayerInputMouseAccessors())
                {
                    int playerInputX;
                    int playerInputY;
                    if (TryGetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, out playerInputX) &&
                        TryGetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, out playerInputY))
                    {
                        state.PlayerInputMouseCaptured = true;
                        state.PlayerInputMouseX = playerInputX;
                        state.PlayerInputMouseY = playerInputY;
                    }
                }

                if (EnsureTileTargetAccessors())
                {
                    int tileX;
                    int tileY;
                    if (TryGetOptionalStatic(_tileTargetXField, null, out tileX) &&
                        TryGetOptionalStatic(_tileTargetYField, null, out tileY))
                    {
                        state.TileTargetCaptured = true;
                        state.TileTargetX = tileX;
                        state.TileTargetY = tileY;
                    }
                }

                CaptureSmartInteractionState(state);
                CaptureTileInteractionInputState(player, state);
                state.Captured = true;
                return true;
            }
            catch (Exception error)
            {
                return Fail("Capture mouse target state failed: " + error.Message);
            }
        }

        public static bool TryApplyMouseTargetOverride(MouseTargetInputState state)
        {
            if (state == null || !state.Captured)
            {
                return Fail("Cannot apply mouse target override: state was not captured.");
            }

            return TrySetMouseScreenPosition(state.MouseX + 1, state.MouseY);
        }

        public static bool TryRestoreMouseTargetState(MouseTargetInputState state)
        {
            if (state == null || !state.Captured)
            {
                return Fail("Cannot restore mouse target state: state was not captured.");
            }

            var ok = true;
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseX", state.MouseX);
            ok &= SetStatic(TerrariaRuntimeTypes.MainType, "mouseY", state.MouseY);

            if (state.PlayerInputMouseCaptured)
            {
                TrySetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, state.PlayerInputMouseX);
                TrySetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, state.PlayerInputMouseY);
            }

            if (state.TileTargetCaptured)
            {
                TrySetOptionalStatic(_tileTargetXField, null, state.TileTargetX);
                TrySetOptionalStatic(_tileTargetYField, null, state.TileTargetY);
            }

            RestoreSmartInteractionState(state);
            RestoreTileInteractionInputState(state);
            return ok;
        }

        public static bool TryReadItemUseVerificationState(object player, int slot, out ItemUseVerificationState state)
        {
            state = new ItemUseVerificationState();
            if (player == null)
            {
                return Fail("Cannot read item use verification: player unavailable.");
            }

            try
            {
                TryGetBool(player, "active", out var active);
                TryGetBool(player, "dead", out var dead);
                TryGetBool(player, "ghost", out var ghost);
                TryGetInt(player, "itemAnimation", out var itemAnimation);
                TryGetInt(player, "itemTime", out var itemTime);
                TryGetInt(player, "reuseDelay", out var reuseDelay);
                TryGetInt(player, "statLife", out var life);
                TryGetInt(player, "statLifeMax2", out var lifeMax);
                TryGetInt(player, "statMana", out var mana);
                TryGetInt(player, "statManaMax2", out var manaMax);
                TryGetSelectedItem(player, out var selectedSlot);

                state.PlayerActive = active;
                state.PlayerDead = dead;
                state.PlayerGhost = ghost;
                state.ItemAnimation = itemAnimation;
                state.ItemTime = itemTime;
                state.ReuseDelay = reuseDelay;
                state.Life = life;
                state.LifeMax = lifeMax;
                state.Mana = mana;
                state.ManaMax = manaMax;
                state.SelectedSlot = selectedSlot;

                ReadInventoryItemSummary(player, slot, state);
                ReadBuffSummary(player, state);
                return true;
            }
            catch (Exception error)
            {
                return Fail("Read item use verification failed: " + error.Message);
            }
        }

        public static bool TryReadRecoveryCooldowns(object player, out int potionDelay, out bool manaSickness, out int manaSickTime)
        {
            potionDelay = 0;
            manaSickness = false;
            manaSickTime = 0;
            if (player == null)
            {
                return Fail("Cannot read recovery cooldowns: player unavailable.");
            }

            var any = false;
            int value;
            if (TryGetInt(player, "potionDelay", out value))
            {
                potionDelay = value;
                any = true;
            }

            bool boolValue;
            if (TryGetBool(player, "manaSick", out boolValue))
            {
                manaSickness = boolValue;
                any = true;
            }

            if (TryGetBool(player, "manaSickness", out boolValue))
            {
                manaSickness = manaSickness || boolValue;
                any = true;
            }

            if (TryGetInt(player, "manaSickTime", out value))
            {
                manaSickTime = value;
                any = true;
            }

            if (TryGetInt(player, "manaSicknessTime", out value))
            {
                manaSickTime = Math.Max(manaSickTime, value);
                any = true;
            }

            return any;
        }

        public static bool TrySetUseTile(object player, bool pressed)
        {
            // Controlled input write: player.controlUseTile / player.releaseUseTile.
            var ok = SetMember(player, "controlUseTile", pressed);
            if (pressed)
            {
                SetMember(player, "releaseUseTile", true);
            }

            return ok;
        }

        public static bool TryPrimeTileInteractionAttempt(object player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot prime tile interaction: player unavailable.";
                return Fail(message);
            }

            // Controlled input write: these fields are Terraria's one-click tile interaction gate.
            var ok = SetMember(player, "controlUseTile", true);
            ok &= SetMember(player, "releaseUseTile", true);
            ok &= SetMember(player, "tileInteractAttempted", true);
            message = ok
                ? "Tile interaction input primed."
                : "Tile interaction input prime failed: " + LastInputCompatError;
            return ok;
        }

        public static void BeginTileInteractionOverride(Guid requestId, int tileX, int tileY)
        {
            lock (TileInteractionOverrideSync)
            {
                _tileInteractionOverrideRequestId = requestId;
                _tileInteractionOverrideTileX = tileX;
                _tileInteractionOverrideTileY = tileY;
                _tileInteractionOverrideExpiresUtc = DateTime.UtcNow.AddSeconds(2);
                _lastTileInteractionOverrideMessage = "Tile interaction mouse override armed for tile " + tileX + "," + tileY + ".";
            }
        }

        public static void EndTileInteractionOverride(Guid requestId)
        {
            lock (TileInteractionOverrideSync)
            {
                if (_tileInteractionOverrideRequestId != Guid.Empty && _tileInteractionOverrideRequestId != requestId)
                {
                    return;
                }

                _tileInteractionOverrideRequestId = Guid.Empty;
                _tileInteractionOverrideTileX = -1;
                _tileInteractionOverrideTileY = -1;
                _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
            }
        }

        public static bool TryApplyTileInteractionOverride(object player, out MouseTargetInputState restoreState, out string message)
        {
            restoreState = null;
            message = string.Empty;
            int tileX;
            int tileY;
            lock (TileInteractionOverrideSync)
            {
                if (_tileInteractionOverrideRequestId == Guid.Empty)
                {
                    return false;
                }

                if (DateTime.UtcNow > _tileInteractionOverrideExpiresUtc)
                {
                    _tileInteractionOverrideRequestId = Guid.Empty;
                    _tileInteractionOverrideTileX = -1;
                    _tileInteractionOverrideTileY = -1;
                    _tileInteractionOverrideExpiresUtc = DateTime.MinValue;
                    message = "Tile interaction mouse override expired.";
                    _lastTileInteractionOverrideMessage = message;
                    return false;
                }

                tileX = _tileInteractionOverrideTileX;
                tileY = _tileInteractionOverrideTileY;
            }

            if (player == null || !TryIsLocalPlayer(player))
            {
                message = "Tile interaction mouse override skipped for non-local player.";
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            if (!TryCaptureMouseTargetState(player, out restoreState))
            {
                message = "Tile interaction mouse override capture failed: " + LastInputCompatError;
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            var worldX = tileX * 16f + 8f;
            var worldY = tileY * 16f + 8f;
            if (!TrySetMouseWorldPosition(worldX, worldY))
            {
                TryRestoreMouseTargetState(restoreState);
                restoreState = null;
                message = "Tile interaction mouse override apply failed: " + LastInputCompatError;
                _lastTileInteractionOverrideMessage = message;
                return false;
            }

            SuppressSmartInteractionState();
            string primeMessage;
            TryPrimeTileInteractionAttempt(player, out primeMessage);
            message = "Tile interaction mouse override applied for tile " + tileX + "," + tileY + ".";
            _lastTileInteractionOverrideMessage = message;
            return true;
        }

        public static bool TryInvokeTileInteractionUse(object player, int tileX, int tileY, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot invoke tile interaction: player unavailable.";
                return Fail(message);
            }

            if (!EnsureTileInteractionUseMethod(player))
            {
                message = "Player.TileInteractionsUse(int,int) was not found.";
                return Fail(message);
            }

            try
            {
                _tileInteractionUseMethod.Invoke(player, new object[] { tileX, tileY });
                invoked = true;
                message = "Player.TileInteractionsUse invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "Player.TileInteractionsUse failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return Fail(message);
            }
        }

        public static bool TryInvokeTileInteractionCheck(object player, int tileX, int tileY, out bool invoked, out string message)
        {
            invoked = false;
            message = string.Empty;
            if (player == null)
            {
                message = "Cannot invoke tile interaction check: player unavailable.";
                return Fail(message);
            }

            if (!EnsureTileInteractionCheckMethod(player))
            {
                message = "Player.TileInteractionsCheck(int,int) was not found.";
                return Fail(message);
            }

            try
            {
                _tileInteractionCheckMethod.Invoke(player, new object[] { tileX, tileY });
                invoked = true;
                message = "Player.TileInteractionsCheck invoked.";
                return true;
            }
            catch (Exception error)
            {
                message = "Player.TileInteractionsCheck failed: " + (error.InnerException == null ? error.Message : error.InnerException.Message);
                return Fail(message);
            }
        }

        public static bool TryInvokeTileInteractionUseAtPlayerPosition(object player, out bool invoked, out int playerTileX, out int playerTileY, out string message)
        {
            invoked = false;
            playerTileX = -1;
            playerTileY = -1;
            if (!TryGetPlayerCenterTile(player, out playerTileX, out playerTileY))
            {
                message = "Cannot invoke tile interaction: player center tile is unavailable.";
                return Fail(message);
            }

            return TryInvokeTileInteractionUse(player, playerTileX, playerTileY, out invoked, out message);
        }

        public static bool TryInvokeTileInteractionCheckAtPlayerPosition(object player, out bool invoked, out int playerTileX, out int playerTileY, out string message)
        {
            invoked = false;
            playerTileX = -1;
            playerTileY = -1;
            if (!TryGetPlayerCenterTile(player, out playerTileX, out playerTileY))
            {
                message = "Cannot invoke tile interaction check: player center tile is unavailable.";
                return Fail(message);
            }

            return TryInvokeTileInteractionCheck(player, playerTileX, playerTileY, out invoked, out message);
        }

        public static bool TryGetPlayerCenterTile(object player, out int tileX, out int tileY)
        {
            tileX = -1;
            tileY = -1;
            if (player == null)
            {
                return Fail("Cannot read player center tile: player unavailable.");
            }

            var position = GetMember(player, "position");
            float x;
            float y;
            if (!TryReadVector2(position, out x, out y))
            {
                return Fail("Cannot read player center tile: player position unavailable.");
            }

            int width;
            int height;
            TryGetInt(player, "width", out width);
            TryGetInt(player, "height", out height);

            tileX = (int)((x + width / 2f) / 16f);
            tileY = (int)((y + height / 2f) / 16f);
            tileX = Clamp(tileX, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesX", tileX + 1) - 1);
            tileY = Clamp(tileY, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesY", tileY + 1) - 1);
            return ClearInputError();
        }

        public static bool TryReleaseUseItem(object player)
        {
            var ok = SetMember(player, "controlUseItem", false);
            SetMember(player, "releaseUseItem", true);
            return ok;
        }

        public static bool TryReleaseUseTile(object player)
        {
            var ok = SetMember(player, "controlUseTile", false);
            SetMember(player, "releaseUseTile", true);
            return ok;
        }

        public static bool TryReadPlayerInventoryOpen(out bool open)
        {
            open = false;
            if (TerrariaRuntimeTypes.MainType == null)
            {
                return Fail("Cannot read Main.playerInventory: Terraria.Main unavailable.");
            }

            return TryGetStaticBool(TerrariaRuntimeTypes.MainType, "playerInventory", out open)
                ? ClearInputError()
                : Fail("Cannot read Main.playerInventory.");
        }

        public static bool TrySetPlayerInventoryOpen(bool open, out string message)
        {
            message = string.Empty;
            if (TerrariaRuntimeTypes.MainType == null)
            {
                message = "Cannot set Main.playerInventory: Terraria.Main unavailable.";
                return Fail(message);
            }

            var ok = SetStatic(TerrariaRuntimeTypes.MainType, "playerInventory", open);
            if (ok)
            {
                ClearInputError();
            }

            message = ok
                ? "Main.playerInventory restored to " + (open ? "open" : "closed") + "."
                : "Main.playerInventory restore failed: " + LastInputCompatError;
            return ok;
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
                   name.IndexOf("��", StringComparison.Ordinal) >= 0 ||
                   name.IndexOf("ץ", StringComparison.Ordinal) >= 0;
        }

        private static bool TryGetBoolByNames(object instance, out bool value, params string[] names)
        {
            value = false;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetBool(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetIntByNames(object instance, out int value, params string[] names)
        {
            value = 0;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetInt(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFloatByNames(object instance, out float value, params string[] names)
        {
            value = 0f;
            if (instance == null || names == null)
            {
                return false;
            }

            for (var index = 0; index < names.Length; index++)
            {
                if (TryGetFloat(instance, names[index], out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetStatic(Type type, string name)
        {
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    return field.GetValue(null);
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property))
                {
                    return property.GetValue(null, null);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static object GetMember(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            var type = instance.GetType();
            if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
            {
                return field.GetValue(instance);
            }

            return TerrariaMemberCache.TryGetProperty(type, name, false, out var property)
                ? property.GetValue(instance, null)
                : null;
        }

        private static bool IsTerrariaPlayer(object player)
        {
            if (player == null)
            {
                return false;
            }

            var type = player.GetType();
            PlayerTypeName = type.FullName ?? type.Name;
            var expected = TerrariaRuntimeTypes.PlayerType;
            if (expected == null)
            {
                return string.Equals(PlayerTypeName, "Terraria.Player", StringComparison.Ordinal);
            }

            return expected.IsAssignableFrom(type);
        }

        private static bool SetStatic(Type type, string name, object value)
        {
            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                return Fail("Static member not found: " + name);
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        private static bool TrySetStaticIfExists(Type type, string name, object value)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, true, out var property) && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed)
        {
            return ApplyUseItemTakeoverFields(player, pressed, true);
        }

        private static bool ApplyUseItemTakeoverFields(object player, bool pressed, bool applyChannel)
        {
            // Controlled input write: combat takeover must override both Player and Main mouse use state for this tick/scope.
            var ok = SetMember(player, "controlUseItem", pressed);
            ok &= SetMember(player, "releaseUseItem", true);
            if (applyChannel)
            {
                TrySetMemberIfExists(player, "channel", pressed);
            }

            var mainType = ResolveMainTypeForInputWrite();
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", pressed);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
            }

            return ok;
        }

        private static Type ResolveMainTypeForInputWrite()
        {
            return FindType("Terraria.Main");
        }

        private static void TrySuppressMainMouseButtonsForItemUseBridge()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            // Controlled input write: isolate queued ItemCheck pulses from the user's held physical mouse button.
            TrySetStaticIfExists(mainType, "mouseLeft", false);
            TrySetStaticIfExists(mainType, "mouseRight", false);
            TrySetStaticIfExists(mainType, "mouseLeftRelease", false);
            TrySetStaticIfExists(mainType, "mouseRightRelease", false);
        }

        private static void TryApplyMainMouseLeftForItemUseBridge()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            // Controlled input write: some vanilla item paths still check Main.mouseLeftRelease during ItemCheck.
            TrySetStaticIfExists(mainType, "mouseLeft", true);
            TrySetStaticIfExists(mainType, "mouseLeftRelease", true);
            TrySetStaticIfExists(mainType, "mouseRight", false);
            TrySetStaticIfExists(mainType, "mouseRightRelease", false);
        }

        private static bool SuppressHeldUseItemForQueuedCombat(object player)
        {
            var ok = SetMember(player, "controlUseItem", false);
            ok &= SetMember(player, "releaseUseItem", true);

            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType != null)
            {
                TrySetStaticIfExists(mainType, "mouseLeft", false);
                TrySetStaticIfExists(mainType, "mouseLeftRelease", false);
            }

            return ok;
        }

        private static void ArmPerfectRevolverSuppressedUseItemHeld()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
            }
        }

        private static bool IsSuppressedUseItemHeld()
        {
            lock (AutoClickerUseItemSuppressionSync)
            {
                var now = DateTime.UtcNow;
                var perfectRevolverHeld = _perfectRevolverSuppressedUseItemHeldUntilUtc > now;
                if (!perfectRevolverHeld)
                {
                    _perfectRevolverSuppressedUseItemHeldUntilUtc = DateTime.MinValue;
                }

                return perfectRevolverHeld;
            }
        }

        private static bool IsLeftButtonDownFallback()
        {
            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkLeftButton) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool SetMember(object instance, string name, object value)
        {
            if (instance == null)
            {
                return Fail("Instance unavailable for " + name);
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                return Fail("Member not found: " + name);
            }
            catch (Exception error)
            {
                return Fail(error.Message);
            }
        }

        private static bool TrySetMemberIfExists(object instance, string name, object value)
        {
            if (instance == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    field.SetValue(instance, value);
                    return true;
                }

                if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property) && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    return true;
                }
            }
            catch (Exception error)
            {
                Logger.Debug("TerrariaInputCompat", "Optional member set failed for " + name + ": " + error.Message);
            }

            return false;
        }

        private static bool TryGetInt(object instance, string name, out int value)
        {
            value = 0;
            if (instance == null)
            {
                return false;
            }

            try
            {
                var type = instance.GetType();
                object raw = null;
                if (TerrariaMemberCache.TryGetField(type, name, false, out var field))
                {
                    raw = field.GetValue(instance);
                }
                else if (TerrariaMemberCache.TryGetProperty(type, name, false, out var property))
                {
                    raw = property.GetValue(instance, null);
                }

                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToInt32(raw);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryGetFloat(object instance, string name, out float value)
        {
            value = 0f;
            if (instance == null)
            {
                return false;
            }

            try
            {
                var raw = GetMember(instance, name);
                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToSingle(raw);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryGetBool(object instance, string name, out bool value)
        {
            value = false;
            var raw = GetMember(instance, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch (Exception error)
            {
                Fail(error.Message);
                return false;
            }
        }

        private static bool TryReadVector2(object vector, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (vector == null)
            {
                return false;
            }

            var type = vector.GetType();
            try
            {
                if (TerrariaMemberCache.TryGetField(type, "X", false, out var xField) &&
                    TerrariaMemberCache.TryGetField(type, "Y", false, out var yField))
                {
                    x = Convert.ToSingle(xField.GetValue(vector));
                    y = Convert.ToSingle(yField.GetValue(vector));
                    return true;
                }
            }
            catch (Exception error)
            {
                Fail(error.Message);
            }

            return false;
        }

        private static bool TrySyncPlayerInputJumpTriggers(bool currentJump, bool justPressed, bool justReleased, out string message)
        {
            return TrySyncPlayerInputTrigger("Jump", currentJump, justPressed, justReleased, out message);
        }

        private static bool TrySyncPlayerInputTrigger(string triggerName, bool current, bool justPressed, bool justReleased, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                message = "PlayerInput trigger sync skipped: trigger name is empty.";
                return false;
            }

            object triggersPack;
            if (!TryGetPlayerInputTriggersPack(out triggersPack, out message))
            {
                return false;
            }

            var currentSynced = TrySetPlayerInputTriggerSet(triggersPack, "Current", triggerName, current);
            var justPressedSynced = TrySetPlayerInputTriggerSet(triggersPack, "JustPressed", triggerName, justPressed);
            var justReleasedSynced = TrySetPlayerInputTriggerSet(triggersPack, "JustReleased", triggerName, justReleased);
            var oldSynced = true;
            if (justPressed)
            {
                oldSynced = TrySetPlayerInputTriggerSet(triggersPack, "Old", triggerName, false);
            }
            else if (justReleased)
            {
                oldSynced = TrySetPlayerInputTriggerSet(triggersPack, "Old", triggerName, true);
            }

            var synced = currentSynced && justPressedSynced && justReleasedSynced && oldSynced;
            message = synced
                ? "PlayerInput " + triggerName + " triggers synced."
                : "PlayerInput " + triggerName + " trigger sync incomplete.";
            return synced;
        }

        private static bool TryGetPlayerInputTriggersPack(out object triggersPack, out string message)
        {
            triggersPack = null;
            message = string.Empty;
            if (!EnsurePlayerInputJumpTriggerAccessors())
            {
                message = "PlayerInput jump trigger sync skipped: PlayerInput.Triggers unavailable.";
                return false;
            }

            try
            {
                if (_playerInputTriggersField != null)
                {
                    triggersPack = _playerInputTriggersField.GetValue(null);
                }
                else if (_playerInputTriggersProperty != null && _playerInputTriggersProperty.CanRead)
                {
                    triggersPack = _playerInputTriggersProperty.GetValue(null, null);
                }

                if (triggersPack == null)
                {
                    message = "PlayerInput jump trigger sync skipped: Triggers pack is null.";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                message = "PlayerInput jump trigger sync failed: " + error.Message;
                Logger.Debug("TerrariaInputCompat", message);
                return false;
            }
        }

        private static bool TrySetPlayerInputJumpTriggerSet(object triggersPack, string setName, bool value)
        {
            return TrySetPlayerInputTriggerSet(triggersPack, setName, "Jump", value);
        }

        private static bool TrySetPlayerInputTriggerSet(object triggersPack, string setName, string triggerName, bool value)
        {
            var triggerSet = GetMember(triggersPack, setName);
            return triggerSet != null && TrySetMemberIfExists(triggerSet, triggerName, value);
        }

        private static bool EnsurePlayerInputJumpTriggerAccessors()
        {
            if (_playerInputJumpTriggersResolved)
            {
                return _playerInputTriggersField != null || _playerInputTriggersProperty != null;
            }

            _playerInputJumpTriggersResolved = true;
            var playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (playerInputType == null)
            {
                Logger.Debug("TerrariaInputCompat", "Terraria.GameInput.PlayerInput not found; jump trigger sync skipped.");
                return false;
            }

            _playerInputTriggersField = playerInputType.GetField("Triggers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputTriggersProperty = _playerInputTriggersField == null
                ? playerInputType.GetProperty("Triggers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            return _playerInputTriggersField != null || _playerInputTriggersProperty != null;
        }

        private static string AppendPlayerInputTriggerSyncMessage(string message, bool synced, string syncMessage)
        {
            if (string.IsNullOrWhiteSpace(syncMessage))
            {
                return message ?? string.Empty;
            }

            return (message ?? string.Empty) + " " + syncMessage;
        }

        private static void TrySetPlayerInputMousePosition(int x, int y)
        {
            if (!EnsurePlayerInputMouseAccessors())
            {
                return;
            }

            TrySetOptionalStatic(_playerInputMouseXField, _playerInputMouseXProperty, x);
            TrySetOptionalStatic(_playerInputMouseYField, _playerInputMouseYProperty, y);
        }

        private static bool EnsurePlayerInputMouseAccessors()
        {
            if (_playerInputMouseResolved)
            {
                return _playerInputMouseXField != null || _playerInputMouseXProperty != null;
            }

            _playerInputMouseResolved = true;
            _playerInputType = FindType("Terraria.GameInput.PlayerInput");
            if (_playerInputType == null)
            {
                Logger.Debug("TerrariaInputCompat", "Terraria.GameInput.PlayerInput not found; mouse sync fallback skipped.");
                return false;
            }

            _playerInputMouseXField = _playerInputType.GetField("MouseX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputMouseYField = _playerInputType.GetField("MouseY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _playerInputMouseXProperty = _playerInputMouseXField == null
                ? _playerInputType.GetProperty("MouseX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            _playerInputMouseYProperty = _playerInputMouseYField == null
                ? _playerInputType.GetProperty("MouseY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                : null;
            return _playerInputMouseXField != null || _playerInputMouseXProperty != null;
        }

        private static void TrySetTileTargetWorldPosition(float worldX, float worldY)
        {
            if (!EnsureTileTargetAccessors())
            {
                return;
            }

            var tileX = (int)(worldX / 16f);
            var tileY = (int)(worldY / 16f);
            tileX = Clamp(tileX, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesX", tileX + 1) - 1);
            tileY = Clamp(tileY, 0, GetStaticInt(TerrariaRuntimeTypes.MainType, "maxTilesY", tileY + 1) - 1);
            TrySetOptionalStatic(_tileTargetXField, null, tileX);
            TrySetOptionalStatic(_tileTargetYField, null, tileY);
        }

        private static void CaptureSmartInteractionState(MouseTargetInputState state)
        {
            if (state == null || TerrariaRuntimeTypes.MainType == null)
            {
                return;
            }

            bool genuine;
            bool fake;
            bool wantedMouse;
            bool wantedGamePad;
            bool showing;
            var captured = false;
            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartInteractShowingGenuine", out genuine))
            {
                state.SmartInteractShowingGenuine = genuine;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartInteractShowingFake", out fake))
            {
                state.SmartInteractShowingFake = fake;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_Mouse", out wantedMouse))
            {
                state.SmartCursorWantedMouse = wantedMouse;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_GamePad", out wantedGamePad))
            {
                state.SmartCursorWantedGamePad = wantedGamePad;
                captured = true;
            }

            if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "SmartCursorShowing", out showing))
            {
                state.SmartCursorShowing = showing;
                captured = true;
            }

            state.SmartStateCaptured = captured;
        }

        private static void SuppressSmartInteractionState()
        {
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                return;
            }

            SetOptionalStatic(mainType, "SmartInteractShowingGenuine", false);
            SetOptionalStatic(mainType, "SmartInteractShowingFake", false);
            SetOptionalStatic(mainType, "SmartCursorWanted_Mouse", false);
            SetOptionalStatic(mainType, "SmartCursorWanted_GamePad", false);
            SetOptionalStatic(mainType, "SmartCursorShowing", false);
        }

        private static void RestoreSmartInteractionState(MouseTargetInputState state)
        {
            if (state == null || !state.SmartStateCaptured || TerrariaRuntimeTypes.MainType == null)
            {
                return;
            }

            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartInteractShowingGenuine", state.SmartInteractShowingGenuine);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartInteractShowingFake", state.SmartInteractShowingFake);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_Mouse", state.SmartCursorWantedMouse);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorWanted_GamePad", state.SmartCursorWantedGamePad);
            SetOptionalStatic(TerrariaRuntimeTypes.MainType, "SmartCursorShowing", state.SmartCursorShowing);
        }

        private static void CaptureTileInteractionInputState(object player, MouseTargetInputState state)
        {
            if (player == null || state == null)
            {
                return;
            }

            bool controlUseTile;
            bool releaseUseTile;
            bool tileInteractAttempted;
            var captured = false;
            if (TryGetBool(player, "controlUseTile", out controlUseTile))
            {
                state.ControlUseTile = controlUseTile;
                captured = true;
            }

            if (TryGetBool(player, "releaseUseTile", out releaseUseTile))
            {
                state.ReleaseUseTile = releaseUseTile;
                captured = true;
            }

            if (TryGetBool(player, "tileInteractAttempted", out tileInteractAttempted))
            {
                state.TileInteractAttempted = tileInteractAttempted;
                captured = true;
            }

            state.TileInteractionInputCaptured = captured;
        }

        private static void RestoreTileInteractionInputState(MouseTargetInputState state)
        {
            if (state == null || !state.TileInteractionInputCaptured)
            {
                return;
            }

            object player;
            if (!TryGetLocalPlayer(out player) || player == null)
            {
                return;
            }

            SetMember(player, "controlUseTile", state.ControlUseTile);
            SetMember(player, "releaseUseTile", state.ReleaseUseTile);
            SetMember(player, "tileInteractAttempted", state.TileInteractAttempted);
        }

        private static void SetOptionalStatic(Type type, string name, object value)
        {
            if (type == null)
            {
                return;
            }

            try
            {
                if (TerrariaMemberCache.TryGetField(type, name, true, out var field))
                {
                    field.SetValue(null, value);
                }
            }
            catch (Exception error)
            {
                if (LogThrottle.ShouldLog("optional-smart-static-set-failed", TimeSpan.FromSeconds(30)))
                {
                    Logger.Debug("TerrariaInputCompat", "Optional smart static set failed for " + name + ": " + error.Message);
                }
            }
        }

        private static bool EnsureTileTargetAccessors()
        {
            if (_tileTargetResolved)
            {
                return _tileTargetXField != null && _tileTargetYField != null;
            }

            _tileTargetResolved = true;
            var playerType = TerrariaRuntimeTypes.PlayerType;
            if (playerType == null)
            {
                return false;
            }

            _tileTargetXField = playerType.GetField("tileTargetX", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            _tileTargetYField = playerType.GetField("tileTargetY", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (_tileTargetXField == null || _tileTargetYField == null)
            {
                Logger.Debug("TerrariaInputCompat", "Player.tileTargetX/Y not found; tile target sync skipped.");
            }

            return _tileTargetXField != null && _tileTargetYField != null;
        }

        private static bool EnsureTileInteractionUseMethod(object player)
        {
            if (_tileInteractionUseResolved)
            {
                return _tileInteractionUseMethod != null;
            }

            _tileInteractionUseResolved = true;
            if (player == null)
            {
                return false;
            }

            _tileInteractionUseMethod = player.GetType().GetMethod(
                "TileInteractionsUse",
                InstanceMemberFlags,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            return _tileInteractionUseMethod != null;
        }

        private static bool EnsureTileInteractionCheckMethod(object player)
        {
            if (_tileInteractionCheckResolved)
            {
                return _tileInteractionCheckMethod != null;
            }

            _tileInteractionCheckResolved = true;
            if (player == null)
            {
                return false;
            }

            _tileInteractionCheckMethod = player.GetType().GetMethod(
                "TileInteractionsCheck",
                InstanceMemberFlags,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            return _tileInteractionCheckMethod != null;
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

        private static void TrySetOptionalStatic(FieldInfo field, PropertyInfo property, int value)
        {
            try
            {
                if (field != null)
                {
                    field.SetValue(null, value);
                }
                else if (property != null && property.CanWrite)
                {
                    property.SetValue(null, value, null);
                }
            }
            catch (Exception error)
            {
                if (LogThrottle.ShouldLog("optional-input-static-set-failed", TimeSpan.FromSeconds(30)))
                {
                    Logger.Debug("TerrariaInputCompat", "Optional input static set failed: " + error.Message);
                }
            }
        }

        private static bool TryGetOptionalStatic(FieldInfo field, PropertyInfo property, out int value)
        {
            value = 0;
            try
            {
                object raw = null;
                if (field != null)
                {
                    raw = field.GetValue(null);
                }
                else if (property != null && property.CanRead)
                {
                    raw = property.GetValue(null, null);
                }

                if (raw == null)
                {
                    return false;
                }

                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ReadInventoryItemSummary(object player, int slot, ItemUseVerificationState state)
        {
            if (slot < 0)
            {
                return;
            }

            var inventory = GetMember(player, "inventory") as IList;
            if (inventory == null || slot >= inventory.Count)
            {
                return;
            }

            var item = inventory[slot];
            if (item == null)
            {
                return;
            }

            TryGetInt(item, "type", out var type);
            TryGetInt(item, "stack", out var stack);
            TryGetInt(item, "useStyle", out var useStyle);
            TryGetBool(item, "consumable", out var consumable);
            TryGetInt(item, "healLife", out var healLife);
            TryGetInt(item, "healMana", out var healMana);
            TryGetInt(item, "buffType", out var buffType);
            TryGetInt(item, "buffTime", out var buffTime);
            var createTile = -1;
            var createWall = -1;
            TryGetInt(item, "createTile", out createTile);
            TryGetInt(item, "createWall", out createWall);
            state.ItemType = type;
            state.ItemStack = stack;
            state.UseStyle = useStyle;
            state.Consumable = consumable;
            state.HealLife = healLife;
            state.HealMana = healMana;
            state.BuffType = buffType;
            state.BuffTime = buffTime;
            state.CreateTile = createTile;
            state.CreateWall = createWall;
            var name = GetMember(item, "Name") ?? GetMember(item, "name");
            state.ItemName = name == null ? string.Empty : name.ToString();
        }

        private static void ReadBuffSummary(object player, ItemUseVerificationState state)
        {
            var buffTypes = GetMember(player, "buffType") as IList;
            var buffTimes = GetMember(player, "buffTime") as IList;
            if (buffTypes == null || buffTimes == null)
            {
                return;
            }

            var max = Math.Min(buffTypes.Count, buffTimes.Count);
            var count = 0;
            var total = 0;
            var types = new StringBuilder();
            types.Append("[");
            var firstType = true;
            for (var index = 0; index < max; index++)
            {
                var type = Convert.ToInt32(buffTypes[index]);
                var time = Convert.ToInt32(buffTimes[index]);
                if (type <= 0 || time <= 0)
                {
                    continue;
                }

                count++;
                total += time;
                if (!firstType)
                {
                    types.Append(",");
                }

                types.Append(type);
                firstType = false;
            }

            types.Append("]");
            state.ActiveBuffCount = count;
            state.BuffTimeTotal = total;
            state.BuffTypesJson = types.ToString();
        }

        private static int GetStaticInt(Type type, string name, int fallback)
        {
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(raw);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool TryGetStaticInt(Type type, string name, out int value)
        {
            value = 0;
            var raw = GetStatic(type, name);
            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToInt32(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetStaticBool(Type type, string name, out bool value)
        {
            value = false;
            object raw;
            try
            {
                raw = GetStatic(type, name);
            }
            catch
            {
                return false;
            }

            if (raw == null)
            {
                return false;
            }

            try
            {
                value = Convert.ToBoolean(raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool Fail(string message)
        {
            _lastError = message ?? string.Empty;
            InputCompatReady = false;
            LogThrottle.WarnThrottled(
                "terraria-input-compat-failed-" + (_lastError ?? string.Empty),
                TimeSpan.FromSeconds(30),
                "TerrariaInputCompat",
                "Input compat failed: " + _lastError);
            return false;
        }

        private static bool SelectionFail(string message)
        {
            _lastError = string.IsNullOrWhiteSpace(message) ? "selected item compat failed." : message;
            LogThrottle.WarnThrottled(
                "terraria-input-selection-failed-" + _lastError,
                TimeSpan.FromSeconds(30),
                "TerrariaInputCompat",
                "Selected item compat failed: " + _lastError);
            return false;
        }

        private static bool ClearSelectionError()
        {
            _lastError = string.Empty;
            InputCompatReady = true;
            return true;
        }

        private static bool ClearInputError()
        {
            _lastError = string.Empty;
            InputCompatReady = true;
            return true;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
