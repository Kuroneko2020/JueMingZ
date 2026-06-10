using System;
using System.Collections;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
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
    }
}
