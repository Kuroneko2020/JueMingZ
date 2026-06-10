using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
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
