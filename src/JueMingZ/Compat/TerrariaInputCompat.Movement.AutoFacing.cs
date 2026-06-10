using System;
using System.Collections;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
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
    }
}
