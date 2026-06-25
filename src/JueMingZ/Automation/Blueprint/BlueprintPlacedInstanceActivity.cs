using System;

namespace JueMingZ.Automation.Blueprint
{
    internal static class BlueprintPlacedInstanceActivity
    {
        public static int ResolveActionableCount(int cachedPlacedInstanceCount, BlueprintProjectionSnapshot projection)
        {
            if (projection != null && projection.LoadSucceeded)
            {
                return Math.Max(0, projection.EffectiveLayerCount);
            }

            var projectionFallback = projection == null ? 0 : projection.VisibleInstanceCount;
            return Math.Max(0, Math.Max(cachedPlacedInstanceCount, projectionFallback));
        }

        public static bool HasActionablePlacedBlueprint(int cachedPlacedInstanceCount, BlueprintProjectionSnapshot projection)
        {
            return ResolveActionableCount(cachedPlacedInstanceCount, projection) > 0;
        }
    }
}
