using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class TileInteractionHookCallbacks
    {
        private struct TileInteractionHookState
        {
            public bool Applied;
            public MouseTargetInputState RestoreState;
        }

        private static void Prefix(object __instance, ref TileInteractionHookState __state)
        {
            __state = new TileInteractionHookState();

            try
            {
                MouseTargetInputState restoreState;
                string message;
                if (!TerrariaInputCompat.TryApplyTileInteractionOverride(__instance, out restoreState, out message))
                {
                    return;
                }

                __state.Applied = true;
                __state.RestoreState = restoreState;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TileInteractionHookCallbacks.Prefix", error);
                LogThrottle.ErrorThrottled(
                    "tile-interaction-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TileInteractionHookCallbacks",
                    "Tile interaction prefix failed; exception swallowed.", error);
            }
        }

        private static void Postfix(object __instance, ref TileInteractionHookState __state)
        {
            if (!__state.Applied)
            {
                return;
            }

            try
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(__state.RestoreState);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TileInteractionHookCallbacks.Postfix", error);
                LogThrottle.ErrorThrottled(
                    "tile-interaction-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TileInteractionHookCallbacks",
                    "Tile interaction postfix restore failed; exception swallowed.", error);
            }
        }
    }
}
