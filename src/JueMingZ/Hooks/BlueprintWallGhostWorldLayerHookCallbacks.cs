using System;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.Hooks
{
    internal static class BlueprintWallGhostWorldLayerHookCallbacks
    {
        private static void Postfix()
        {
            try
            {
                BlueprintProjectionWallWorldOverlay.DrawWorldLayer();
                BlueprintPlacementPreviewWallWorldOverlay.DrawWorldLayer();
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "blueprint-wall-ghost-world-layer-hook-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintWallGhostWorldLayerHookCallbacks",
                    "Blueprint wall ghost world layer hook failed; exception swallowed.", error);
            }
        }

        internal static string GetRouteNameForTesting()
        {
            return "BlueprintProjectionWallWorldOverlay.DrawWorldLayer+BlueprintPlacementPreviewWallWorldOverlay.DrawWorldLayer";
        }
    }
}
