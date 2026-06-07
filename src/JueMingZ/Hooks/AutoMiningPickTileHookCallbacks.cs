using System;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class AutoMiningPickTileHookCallbacks
    {
        // PickTile is an observation boundary for auto mining. Prefix snapshots
        // the vanilla attempt; postfix must record outcome, not force tile changes.
        private struct PickTileHookState
        {
            public bool Track;
            public int TileX;
            public int TileY;
            public int TileType;
            public int PickItemType;
            public int PickSlot;
        }

        private static void Prefix(object __instance, object[] __args, ref PickTileHookState __state)
        {
            __state = new PickTileHookState();

            try
            {
                if (__instance == null || __args == null || __args.Length < 3 || !TerrariaInputCompat.TryIsLocalPlayer(__instance))
                {
                    return;
                }

                int tileX;
                int tileY;
                try
                {
                    tileX = Convert.ToInt32(__args[0]);
                    tileY = Convert.ToInt32(__args[1]);
                }
                catch
                {
                    return;
                }

                AutoMiningPickaxeProfile pickaxe;
                string message;
                if (!AutoMiningCompat.TryGetSelectedPickaxe(__instance, out pickaxe, out message))
                {
                    return;
                }

                Type mainType;
                object tiles;
                int maxTilesX;
                int maxTilesY;
                if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
                {
                    return;
                }

                bool active;
                int tileType;
                if (!AutoMiningCompat.TryReadTile(tiles, tileX, tileY, out active, out tileType) ||
                    !active ||
                    !AutoMiningCompat.IsMineableOreTileType(tileType))
                {
                    return;
                }

                __state.Track = true;
                __state.TileX = tileX;
                __state.TileY = tileY;
                __state.TileType = tileType;
                __state.PickItemType = pickaxe.ItemType;
                __state.PickSlot = pickaxe.SelectedSlot;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("AutoMiningPickTileHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "auto-mining-picktile-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "AutoMiningPickTileHookCallbacks",
                    "Auto mining PickTile prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(ref PickTileHookState __state)
        {
            if (!__state.Track)
            {
                return;
            }

            try
            {
                Type mainType;
                object tiles;
                int maxTilesX;
                int maxTilesY;
                string message;
                if (!AutoMiningCompat.TryGetTileContext(out mainType, out tiles, out maxTilesX, out maxTilesY, out message))
                {
                    return;
                }

                bool active;
                int tileType;
                if (AutoMiningCompat.TryReadTile(tiles, __state.TileX, __state.TileY, out active, out tileType) &&
                    active &&
                    tileType == __state.TileType)
                {
                    return;
                }

                AutoMiningService.ObserveManualTileMined(
                    __state.TileX,
                    __state.TileY,
                    __state.TileType,
                    __state.PickItemType,
                    __state.PickSlot);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("AutoMiningPickTileHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "auto-mining-picktile-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "AutoMiningPickTileHookCallbacks",
                    "Auto mining PickTile postfix failed; exception swallowed.", error);
            }
        }
    }
}
