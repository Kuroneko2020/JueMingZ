using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Automation.MapEnhancement
{
    internal static class MapDirectionHintTargetService
    {
        internal const string ServiceName = "map-direction-hints-targeting";
        internal const long ScanCadenceTicks = 15;
        internal const int MaxObservedNpcs = 200;

        private static readonly object SyncRoot = new object();
        private static readonly IMapDirectionHintNpcObservationProvider DefaultProvider =
            new TerrariaNpcObservationProvider();
        private static IMapDirectionHintNpcObservationProvider _providerForTesting;
        private static MapDirectionHintTargetSnapshot _snapshot =
            MapDirectionHintTargetSnapshot.Empty("notInitialized", "map direction hint target service not initialized");
        private static MapDirectionHintRenderSnapshot _renderSnapshot =
            MapDirectionHintRenderSnapshot.Empty("notInitialized", "map direction hint target service not initialized");
        private static long _nextScanTick;

        public static void Tick(RuntimeSettingsSnapshot settings, GameStateSnapshot gameState, long runtimeTick)
        {
            settings = settings ?? RuntimeSettingsSnapshotProvider.GetCurrent();
            var rareEnabled = settings != null && settings.MapRareCreatureDirectionEnabled;
            var travellingEnabled = settings != null && settings.MapTravellingMerchantDirectionEnabled;
            var enabled = rareEnabled || travellingEnabled;
            var normalizedTick = Math.Max(0L, runtimeTick);

            if (!enabled)
            {
                Publish(BuildStatusSnapshot(
                    false,
                    rareEnabled,
                    travellingEnabled,
                    "disabled",
                    "map direction hints are disabled",
                    normalizedTick,
                    0L));
                return;
            }

            if (gameState == null || !gameState.IsInWorld || gameState.IsInMainMenu)
            {
                Publish(BuildStatusSnapshot(
                    true,
                    rareEnabled,
                    travellingEnabled,
                    "notInWorld",
                    "map direction hints scan only runs in world",
                    normalizedTick,
                    0L));
                return;
            }

            lock (SyncRoot)
            {
                if (normalizedTick < _nextScanTick)
                {
                    return;
                }

                _nextScanTick = normalizedTick + ScanCadenceTicks;
            }

            MapDirectionHintNpcObservation[] observations;
            string message;
            try
            {
                var provider = GetProvider();
                if (!provider.TryRead(out observations, out message))
                {
                    Publish(BuildStatusSnapshot(
                        true,
                        rareEnabled,
                        travellingEnabled,
                        "scanUnavailable",
                        message,
                        normalizedTick,
                        normalizedTick + ScanCadenceTicks));
                    return;
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "map-direction-hints-target-scan-failed",
                    TimeSpan.FromSeconds(30),
                    "MapDirectionHintTargetService",
                    "Map direction hint NPC scan failed: " + error.Message);
                Publish(BuildStatusSnapshot(
                    true,
                    rareEnabled,
                    travellingEnabled,
                    "scanFailed",
                    error.Message,
                    normalizedTick,
                    normalizedTick + ScanCadenceTicks));
                return;
            }

            Publish(BuildSnapshot(
                observations,
                rareEnabled,
                travellingEnabled,
                normalizedTick,
                normalizedTick + ScanCadenceTicks,
                string.IsNullOrWhiteSpace(message) ? "ready" : message,
                MapRareCreatureDirectionPlayerContext.FromRuntime(gameState),
                MapTravellingMerchantWorldContext.FromRuntime()));
        }

        public static MapDirectionHintTargetSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _snapshot == null
                    ? MapDirectionHintTargetSnapshot.Empty("notInitialized", "map direction hint target service not initialized")
                    : _snapshot.Clone();
            }
        }

        public static MapDirectionHintRenderSnapshot GetRenderSnapshot()
        {
            lock (SyncRoot)
            {
                return _renderSnapshot ??
                       MapDirectionHintRenderSnapshot.Empty("notInitialized", "map direction hint target service not initialized");
            }
        }

        internal static MapDirectionHintTargetSnapshot BuildSnapshotForTesting(
            MapDirectionHintNpcObservation[] observations,
            bool rareEnabled,
            bool travellingEnabled,
            long scanTick,
            long nextScanTick,
            string message,
            MapRareCreatureDirectionPlayerContext rareContext = null)
        {
            return BuildSnapshot(
                observations,
                rareEnabled,
                travellingEnabled,
                scanTick,
                nextScanTick,
                message,
                rareContext ?? MapRareCreatureDirectionPlayerContext.Unavailable("test player context unavailable"),
                MapTravellingMerchantWorldContext.Unavailable());
        }

        internal static void SetObservationProviderForTesting(IMapDirectionHintNpcObservationProvider provider)
        {
            lock (SyncRoot)
            {
                _providerForTesting = provider;
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _snapshot = MapDirectionHintTargetSnapshot.Empty("notInitialized", "map direction hint target service not initialized");
                _renderSnapshot = MapDirectionHintRenderSnapshot.Empty("notInitialized", "map direction hint target service not initialized");
                _providerForTesting = null;
                _nextScanTick = 0L;
            }
        }

        private static IMapDirectionHintNpcObservationProvider GetProvider()
        {
            lock (SyncRoot)
            {
                return _providerForTesting ?? DefaultProvider;
            }
        }

        private static void Publish(MapDirectionHintTargetSnapshot snapshot)
        {
            snapshot = snapshot ?? MapDirectionHintTargetSnapshot.Empty("scanUnavailable", "map direction hint snapshot unavailable");
            lock (SyncRoot)
            {
                _snapshot = snapshot.Clone();
                // Draw reads this immutable, target-only projection source so it never clones the full NPC observation array per frame.
                _renderSnapshot = MapDirectionHintRenderSnapshot.FromTargetSnapshot(snapshot);
                _nextScanTick = snapshot.Enabled ? Math.Max(0L, snapshot.NextScanTick) : 0L;
            }

            MapDirectionHintDiagnostics.RecordTravellingMerchantTarget(snapshot.TravellingMerchantTarget);
            MapDirectionHintDiagnostics.RecordRareCreatureTarget(snapshot.RareCreatureTarget);
        }

        private static MapDirectionHintTargetSnapshot BuildStatusSnapshot(
            bool enabled,
            bool rareEnabled,
            bool travellingEnabled,
            string status,
            string message,
            long scanTick,
            long nextScanTick)
        {
            return new MapDirectionHintTargetSnapshot
            {
                Enabled = enabled,
                RareCreatureEnabled = rareEnabled,
                TravellingMerchantEnabled = travellingEnabled,
                Status = status ?? string.Empty,
                Message = message ?? string.Empty,
                LastScanTick = Math.Max(0L, scanTick),
                NextScanTick = Math.Max(0L, nextScanTick),
                CapturedUtc = DateTime.UtcNow,
                Npcs = new MapDirectionHintNpcObservation[0],
                RareCreatureTarget = MapRareCreatureDirectionTarget.Disabled(status, message),
                TravellingMerchantTarget = MapTravellingMerchantDirectionTarget.Disabled(status, message)
            };
        }

        private static MapDirectionHintTargetSnapshot BuildSnapshot(
            MapDirectionHintNpcObservation[] observations,
            bool rareEnabled,
            bool travellingEnabled,
            long scanTick,
            long nextScanTick,
            string message,
            MapRareCreatureDirectionPlayerContext rareContext,
            MapTravellingMerchantWorldContext worldContext)
        {
            var normalized = NormalizeObservations(observations);
            var snapshot = new MapDirectionHintTargetSnapshot
            {
                Enabled = rareEnabled || travellingEnabled,
                RareCreatureEnabled = rareEnabled,
                TravellingMerchantEnabled = travellingEnabled,
                Status = "ready",
                Message = message ?? string.Empty,
                LastScanTick = Math.Max(0L, scanTick),
                NextScanTick = Math.Max(0L, nextScanTick),
                NpcCount = normalized.Length,
                CapturedUtc = DateTime.UtcNow,
                Npcs = normalized,
                RareCreatureTarget = MapRareCreatureDirectionTargetResolver.Resolve(
                    rareEnabled,
                    normalized,
                    rareContext ?? MapRareCreatureDirectionPlayerContext.Unavailable("rare creature player context unavailable"),
                    scanTick),
                TravellingMerchantTarget = MapTravellingMerchantDirectionTargetResolver.Resolve(
                    travellingEnabled,
                    normalized,
                    worldContext ?? MapTravellingMerchantWorldContext.Unavailable(),
                    scanTick)
            };

            for (var index = 0; index < normalized.Length; index++)
            {
                var npc = normalized[index];
                if (npc == null)
                {
                    continue;
                }

                if (npc.Active)
                {
                    snapshot.ActiveNpcCount++;
                }

                if (npc.Rarity > 0 && npc.Active && !npc.Hidden)
                {
                    snapshot.RareCandidateCount++;
                }

                if (npc.TownNpc)
                {
                    snapshot.TownNpcCount++;
                }

                if (npc.Hidden)
                {
                    snapshot.HiddenNpcCount++;
                }
            }

            return snapshot;
        }

        private static MapDirectionHintNpcObservation[] NormalizeObservations(MapDirectionHintNpcObservation[] observations)
        {
            if (observations == null || observations.Length == 0)
            {
                return new MapDirectionHintNpcObservation[0];
            }

            var normalized = new List<MapDirectionHintNpcObservation>(Math.Min(observations.Length, MaxObservedNpcs));
            for (var index = 0; index < observations.Length && normalized.Count < MaxObservedNpcs; index++)
            {
                var observation = observations[index];
                if (observation == null)
                {
                    continue;
                }

                normalized.Add(observation.Clone());
            }

            return normalized.ToArray();
        }

        private sealed class TerrariaNpcObservationProvider : IMapDirectionHintNpcObservationProvider
        {
            public bool TryRead(out MapDirectionHintNpcObservation[] observations, out string message)
            {
                observations = new MapDirectionHintNpcObservation[0];
                message = string.Empty;
                var npcs = TerrariaMainCompat.Npcs;
                if (npcs == null)
                {
                    message = "npcArrayUnavailable";
                    return false;
                }

                var result = new List<MapDirectionHintNpcObservation>(Math.Min(npcs.Length, MaxObservedNpcs));
                for (var index = 0; index < npcs.Length && result.Count < MaxObservedNpcs; index++)
                {
                    var npc = npcs[index];
                    if (npc == null)
                    {
                        continue;
                    }

                    var active = TerrariaNpcReadCompat.IsActive(npc);
                    if (!active)
                    {
                        continue;
                    }

                    var center = TerrariaNpcReadCompat.Center(npc);
                    result.Add(new MapDirectionHintNpcObservation
                    {
                        Active = active,
                        Type = TerrariaNpcReadCompat.Type(npc),
                        WhoAmI = TerrariaNpcReadCompat.WhoAmI(npc),
                        CenterX = center.X,
                        CenterY = center.Y,
                        DisplayName = TerrariaNpcReadCompat.Name(npc),
                        Rarity = TerrariaNpcReadCompat.Rarity(npc),
                        TownNpc = TerrariaNpcReadCompat.IsTownNpc(npc),
                        Homeless = TerrariaNpcReadCompat.IsHomeless(npc),
                        HomeTileX = TerrariaNpcReadCompat.HomeTileX(npc),
                        HomeTileY = TerrariaNpcReadCompat.HomeTileY(npc),
                        Hidden = TerrariaNpcReadCompat.IsHidden(npc)
                    });
                }

                observations = result.ToArray();
                return true;
            }
        }
    }

    internal interface IMapDirectionHintNpcObservationProvider
    {
        bool TryRead(out MapDirectionHintNpcObservation[] observations, out string message);
    }
}
