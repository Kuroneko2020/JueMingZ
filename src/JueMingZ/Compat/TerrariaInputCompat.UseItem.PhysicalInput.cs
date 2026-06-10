using System;
using System.Runtime.InteropServices;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        private static bool? _physicalMouseLeftHeldOverrideForTesting;
        private static bool? _physicalMouseRightHeldOverrideForTesting;

        internal static void SetPhysicalMouseButtonOverridesForTesting(bool? leftHeld, bool? rightHeld)
        {
            _physicalMouseLeftHeldOverrideForTesting = leftHeld;
            _physicalMouseRightHeldOverrideForTesting = rightHeld;
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

        public static bool TryReadPhysicalMouseRightHeld(out bool held)
        {
            held = IsRightButtonDownFallback();
            try
            {
                bool mainMouseRight;
                if (TryGetStaticBool(TerrariaRuntimeTypes.MainType, "mouseRight", out mainMouseRight))
                {
                    held = held || mainMouseRight;
                }

                var playerInputType = FindType("Terraria.GameInput.PlayerInput");
                if (playerInputType == null)
                {
                    return ClearInputError();
                }

                var triggers = GetStatic(playerInputType, "Triggers");
                var current = triggers == null ? null : GetMember(triggers, "Current");
                bool mouseRight;
                if (current != null && TryGetBool(current, "MouseRight", out mouseRight))
                {
                    held = held || mouseRight;
                }

                return ClearInputError();
            }
            catch (Exception error)
            {
                return Fail("Cannot read physical mouse right state: " + error.Message);
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
            if (_physicalMouseLeftHeldOverrideForTesting.HasValue)
            {
                return _physicalMouseLeftHeldOverrideForTesting.Value;
            }

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

        private static bool IsRightButtonDownFallback()
        {
            if (_physicalMouseRightHeldOverrideForTesting.HasValue)
            {
                return _physicalMouseRightHeldOverrideForTesting.Value;
            }

            if (!TerrariaMainCompat.AllowsInputProcessing)
            {
                return false;
            }

            try
            {
                return (GetAsyncKeyState(VkRightButton) & 0x8000) != 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
