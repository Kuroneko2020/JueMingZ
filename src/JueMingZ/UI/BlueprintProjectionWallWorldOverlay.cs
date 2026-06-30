using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class BlueprintProjectionWallWorldOverlay
    {
        private const string VisualContract = "blueprint-wall-ghost-world-layer+world-draw-guard+complete-target-layer+before-terraria-foreground+before-projection-foreground+draw-cache-only";

        public static bool DrawWorldLayer()
        {
            try
            {
                var snapshot = BlueprintProjectionService.GetCachedSnapshotForDraw();
                var floating = BlueprintPlacedInstanceTransformState.GetFloatingProjectionForDraw();
                if (!HasDrawableLayers(snapshot) && !HasDrawableLayers(floating))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterWorldDraw("BlueprintProjectionWallWorldOverlay", out spriteBatch))
                {
                    return true;
                }

                // "Bottom wall layer" means a complete wall target drawn before
                // vanilla tile/object foreground, not a late topmost interface pass.
                BlueprintProjectionOverlay.DrawProjectionWorldWallLayer(spriteBatch, snapshot, floating);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("BlueprintProjectionWallWorldOverlay", error);
                LogThrottle.ErrorThrottled(
                    "blueprint-projection-wall-world-overlay-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintProjectionWallWorldOverlay",
                    "Blueprint projection wall world layer draw failed; exception swallowed.", error);
            }

            return true;
        }

        internal static string GetVisualContractForTesting()
        {
            return VisualContract;
        }

        private static bool HasDrawableLayers(BlueprintProjectionSnapshot snapshot)
        {
            return snapshot != null &&
                   snapshot.LoadSucceeded &&
                   snapshot.ProjectedLayers != null &&
                   snapshot.ProjectedLayers.Count > 0;
        }
    }
}
