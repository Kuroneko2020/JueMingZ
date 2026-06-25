using System;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Records;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintWorldInstanceLifecycleResult
    {
        public BlueprintWorldInstanceLifecycleResult()
        {
            ResultCode = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            PlacedLoadResultCode = string.Empty;
            ProjectionResultCode = string.Empty;
        }

        public bool Refreshed { get; set; }
        public string ResultCode { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string PlacedLoadResultCode { get; set; }
        public string ProjectionResultCode { get; set; }
        public int PlacedInstanceCount { get; set; }
        public int ProjectionInstanceCount { get; set; }
        public int RefreshCount { get; set; }
    }

    internal static class BlueprintWorldInstanceLifecycleService
    {
        private static readonly object SyncRoot = new object();
        private static string _lastWorldPairKey = string.Empty;
        private static string _lastWorldKey = string.Empty;
        private static int _refreshCount;

        public static void Tick(GameStateSnapshot gameState)
        {
            TickCore(gameState, ResolveCurrentWorldContext());
        }

        internal static BlueprintWorldInstanceLifecycleResult TickForTesting(
            GameStateSnapshot gameState,
            BlueprintPlacementWorldContext context)
        {
            return TickCore(gameState, context);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastWorldPairKey = string.Empty;
                _lastWorldKey = string.Empty;
                _refreshCount = 0;
            }
        }

        private static BlueprintWorldInstanceLifecycleResult TickCore(
            GameStateSnapshot gameState,
            BlueprintPlacementWorldContext context)
        {
            if (gameState == null || !gameState.IsInWorld || gameState.IsInMainMenu)
            {
                ResetObservedWorld();
                return CreateResult(false, "worldUnavailable", string.Empty, string.Empty, null, null);
            }

            if (context == null ||
                !context.Succeeded ||
                string.IsNullOrWhiteSpace(context.WorldPairKey) ||
                string.IsNullOrWhiteSpace(context.WorldKey))
            {
                ResetObservedWorld();
                return CreateResult(
                    false,
                    context == null ? "identityUnavailable" : context.FailureReason,
                    string.Empty,
                    string.Empty,
                    null,
                    null);
            }

            var pairKey = context.WorldPairKey ?? string.Empty;
            var worldKey = context.WorldKey ?? string.Empty;
            lock (SyncRoot)
            {
                if (string.Equals(_lastWorldPairKey, pairKey, StringComparison.Ordinal) &&
                    string.Equals(_lastWorldKey, worldKey, StringComparison.Ordinal))
                {
                    return CreateResult(false, "unchanged", pairKey, worldKey, null, null);
                }

                _lastWorldPairKey = pairKey;
                _lastWorldKey = worldKey;
            }

            BlueprintPlacedInstanceCachedSummary placed = null;
            BlueprintProjectionSnapshot projection = null;
            var resultCode = "refreshed";

            try
            {
                placed = BlueprintPlacedInstanceUiState.RefreshForWorldLifecycle();
            }
            catch (Exception error)
            {
                resultCode = "placedRefreshFailed";
                LogThrottle.WarnThrottled(
                    "blueprint-world-lifecycle-placed-refresh-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintWorldInstanceLifecycleService",
                    "Blueprint placed instance lifecycle refresh failed after world identity changed. " + error.Message);
            }

            try
            {
                projection = BlueprintProjectionService.RefreshAfterWorldIdentityChanged();
            }
            catch (Exception error)
            {
                resultCode = string.Equals(resultCode, "refreshed", StringComparison.Ordinal)
                    ? "projectionRefreshFailed"
                    : "placedAndProjectionRefreshFailed";
                LogThrottle.WarnThrottled(
                    "blueprint-world-lifecycle-projection-refresh-failed",
                    TimeSpan.FromSeconds(10),
                    "BlueprintWorldInstanceLifecycleService",
                    "Blueprint projection lifecycle refresh failed after world identity changed. " + error.Message);
            }

            lock (SyncRoot)
            {
                unchecked { _refreshCount++; }
                return CreateResult(true, resultCode, pairKey, worldKey, placed, projection);
            }
        }

        private static BlueprintPlacementWorldContext ResolveCurrentWorldContext()
        {
            PlayerWorldIdentityResolution resolution;
            if (!PlayerWorldIdentityResolver.TryResolveCurrentReadOnly(out resolution) ||
                resolution == null ||
                !resolution.IsResolved ||
                string.IsNullOrWhiteSpace(resolution.PairId) ||
                string.IsNullOrWhiteSpace(resolution.WorldId))
            {
                return BlueprintPlacementWorldContext.Failure(resolution == null ? "identityUnavailable" : resolution.FailureReason);
            }

            return BlueprintPlacementWorldContext.Success(resolution.PairId, resolution.WorldId);
        }

        private static void ResetObservedWorld()
        {
            lock (SyncRoot)
            {
                _lastWorldPairKey = string.Empty;
                _lastWorldKey = string.Empty;
            }
        }

        private static BlueprintWorldInstanceLifecycleResult CreateResult(
            bool refreshed,
            string resultCode,
            string worldPairKey,
            string worldKey,
            BlueprintPlacedInstanceCachedSummary placed,
            BlueprintProjectionSnapshot projection)
        {
            return new BlueprintWorldInstanceLifecycleResult
            {
                Refreshed = refreshed,
                ResultCode = string.IsNullOrWhiteSpace(resultCode) ? string.Empty : resultCode,
                WorldPairKey = worldPairKey ?? string.Empty,
                WorldKey = worldKey ?? string.Empty,
                PlacedLoadResultCode = placed == null ? string.Empty : placed.LoadResultCode ?? string.Empty,
                ProjectionResultCode = projection == null ? string.Empty : projection.ResultCode ?? string.Empty,
                PlacedInstanceCount = placed == null ? 0 : placed.InstanceCount,
                ProjectionInstanceCount = projection == null ? 0 : projection.InstanceCount,
                RefreshCount = _refreshCount
            };
        }
    }
}
