using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementTargetResolver
    {
        public static MapQuickAnnouncementResolveResult Resolve(MapQuickAnnouncementResolveContext context)
        {
            context = context ?? new MapQuickAnnouncementResolveContext();

            if (context.UiItem != null && context.UiItem.IsActive)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.UiItem,
                    MapQuickAnnouncementTextBuilder.BuildItemText(context.UiItem.Stack, context.UiItem.Name),
                    BuildUiItemDetail(context.UiItem),
                    context.UiItem.Name,
                    context.UiItem.Stack);
            }

            if (context.UiSlot != null && context.UiSlot.IsKnown && !context.UiSlot.HasActiveItem)
            {
                return BuildUiEmptySlotResult(context.UiSlot);
            }

            var actors = ResolveActorsAtMouse(context);
            if (actors.Count > 0)
            {
                return BuildResult(
                    MapQuickAnnouncementTargetKind.Actor,
                    MapQuickAnnouncementTextBuilder.BuildActorText(actors),
                    "actor:" + actors.Count.ToString(CultureInfo.InvariantCulture),
                    BuildActorSummary(actors),
                    actors.Count);
            }

            var item = ResolveHitWorldItem(context);
            if (item != null)
            {
                var stack = ResolveNearbyStack(context, item);
                return BuildResult(
                    MapQuickAnnouncementTargetKind.WorldItem,
                    MapQuickAnnouncementTextBuilder.BuildItemText(stack, item.Name),
                    "worldItem",
                    item.Name,
                    stack);
            }

            var visibility = ResolveVisibilityDecision(context);
            var filteredTile = FilterTileTarget(context.Tile, visibility);
            if (filteredTile != null && filteredTile.HasAnyLayer)
            {
                return WithVisibilitySummary(
                    BuildResult(
                        MapQuickAnnouncementTargetKind.Tile,
                        MapQuickAnnouncementTextBuilder.BuildTileText(filteredTile),
                        BuildTileDetail(filteredTile),
                        BuildTileTargetName(filteredTile),
                        1),
                    visibility,
                    false);
            }

            if (context.Wall != null &&
                context.Wall.Active &&
                AllowsWorldLayer(visibility == null ? null : visibility.Wall))
            {
                return WithVisibilitySummary(
                    BuildResult(
                        MapQuickAnnouncementTargetKind.Wall,
                        MapQuickAnnouncementTextBuilder.BuildWallText(context.Wall),
                        BuildWallDetail(context.Wall),
                        context.Wall.WallName,
                        1),
                    visibility,
                    false);
            }

            if (ShouldBuildVisibilityBlockedResult(context, visibility))
            {
                return BuildVisibilityBlockedResult(context, visibility);
            }

            var airText = MapQuickAnnouncementTextBuilder.BuildAirText(ResolveAirPhraseIndex(context));
            return BuildResult(
                MapQuickAnnouncementTargetKind.Air,
                airText,
                "air",
                airText,
                0);
        }

        public static bool TryResolveCurrent(out MapQuickAnnouncementResolveResult result, out string skipReason)
        {
            result = null;
            skipReason = string.Empty;

            MapQuickAnnouncementResolveContext context;
            if (!TryBuildCurrentContext(out context, out skipReason))
            {
                return false;
            }

            result = Resolve(context);
            return result != null && (result.SuppressDelivery || !string.IsNullOrWhiteSpace(result.Body));
        }

        internal static bool TryCaptureCurrentTriggerContext(out MapQuickAnnouncementTriggerContext context, out string skipReason)
        {
            context = null;
            skipReason = string.Empty;

            InformationWorldContext worldContext;
            if (!InformationWorldContextProvider.TryBuild(InformationWorldContextProfile.Status, out worldContext, out skipReason))
            {
                return false;
            }

            var mouseScreenX = TerrariaMainCompat.MouseX;
            var mouseScreenY = TerrariaMainCompat.MouseY;
            var mouseWorldX = worldContext.ScreenX + mouseScreenX;
            var mouseWorldY = worldContext.ScreenY + mouseScreenY;
            context = new MapQuickAnnouncementTriggerContext
            {
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY,
                MouseScreenX = mouseScreenX,
                MouseScreenY = mouseScreenY,
                MouseTileX = (int)Math.Floor(mouseWorldX / TerrariaTileReadCompat.TileSize),
                MouseTileY = (int)Math.Floor(mouseWorldY / TerrariaTileReadCompat.TileSize),
                GameUpdateCount = worldContext.GameUpdateCount
            };
            return true;
        }

        internal static MapQuickAnnouncementResolveAttempt ResolveUiHoverFromPending(
            MapQuickAnnouncementPendingRequest pending,
            ulong currentGameUpdateCount)
        {
            if (pending == null || pending.TriggerContext == null || !pending.TriggerContext.Succeeded)
            {
                return MapQuickAnnouncementResolveAttempt.Failed("pendingTriggerContextUnavailable");
            }

            var context = CreateContextFromTrigger(pending.TriggerContext, currentGameUpdateCount);
            string uiHoverReadStatus;
            if (!AddUiHoverItem(context, out uiHoverReadStatus))
            {
                return MapQuickAnnouncementResolveAttempt.Failed("uiHoverPending", uiHoverReadStatus);
            }

            return MapQuickAnnouncementResolveAttempt.Success(Resolve(context), uiHoverReadStatus);
        }

        internal static bool TryResolvePendingFallback(
            MapQuickAnnouncementPendingRequest pending,
            ulong currentGameUpdateCount,
            out MapQuickAnnouncementResolveResult result,
            out string skipReason)
        {
            result = null;
            skipReason = string.Empty;
            if (pending == null || pending.TriggerContext == null || !pending.TriggerContext.Succeeded)
            {
                skipReason = "pendingTriggerContextUnavailable";
                return false;
            }

            var context = CreateContextFromTrigger(pending.TriggerContext, currentGameUpdateCount);
            AddPlayers(context);
            AddNpcs(context);
            AddWorldItems(context);
            AddTileAndWall(context);
            result = Resolve(context);
            return result != null && !string.IsNullOrWhiteSpace(result.Body);
        }

        internal static int ResolveAirPhraseIndexForTesting(MapQuickAnnouncementResolveContext context)
        {
            return ResolveAirPhraseIndex(context ?? new MapQuickAnnouncementResolveContext());
        }

        private static MapQuickAnnouncementResolveResult BuildResult(
            MapQuickAnnouncementTargetKind kind,
            string body,
            string detail,
            string targetName,
            int targetCount)
        {
            var resolvedBody = string.IsNullOrWhiteSpace(body) ? MapQuickAnnouncementTextBuilder.BuildAirText(0) : body;
            var resolvedKind = string.IsNullOrWhiteSpace(body) ? MapQuickAnnouncementTargetKind.Air : kind;
            return new MapQuickAnnouncementResolveResult
            {
                Kind = resolvedKind,
                Body = resolvedBody,
                Detail = detail ?? string.Empty,
                TargetName = string.IsNullOrWhiteSpace(targetName) ? resolvedBody : targetName,
                TargetCount = Math.Max(0, targetCount)
            };
        }

        private static MapQuickAnnouncementResolveResult BuildUiEmptySlotResult(MapQuickAnnouncementUiSlotTarget slot)
        {
            return new MapQuickAnnouncementResolveResult
            {
                Kind = MapQuickAnnouncementTargetKind.None,
                Body = string.Empty,
                Detail = BuildUiSlotDetail(slot),
                TargetName = "UI空槽",
                TargetCount = 0,
                SuppressDelivery = true,
                FailureReason = "uiEmptySlot"
            };
        }

        private static string BuildActorSummary(IList<MapQuickAnnouncementActorTarget> actors)
        {
            if (actors == null || actors.Count == 0)
            {
                return string.Empty;
            }

            if (actors.Count == 1)
            {
                return MapQuickAnnouncementTextBuilder.BuildActorPart(actors[0]);
            }

            return actors.Count.ToString(CultureInfo.InvariantCulture) + " actors";
        }

        private static string BuildUiItemDetail(MapQuickAnnouncementItemTarget item)
        {
            if (item == null)
            {
                return "uiItem";
            }

            var source = string.IsNullOrWhiteSpace(item.HoverSource)
                ? "unknown"
                : item.HoverSource.Trim();
            return "uiItem;source=" + source +
                   ";context=" + item.HoverContext.ToString(CultureInfo.InvariantCulture) +
                   ";slot=" + item.HoverSlot.ToString(CultureInfo.InvariantCulture) +
                   ";ageUpdates=" + Math.Max(0, item.HoverAgeUpdates).ToString(CultureInfo.InvariantCulture);
        }

        private static string BuildUiSlotDetail(MapQuickAnnouncementUiSlotTarget slot)
        {
            if (slot == null)
            {
                return "uiSlot:empty";
            }

            var source = string.IsNullOrWhiteSpace(slot.HoverSource)
                ? "unknown"
                : slot.HoverSource.Trim();
            return "uiSlot:empty;source=" + source +
                   ";context=" + slot.HoverContext.ToString(CultureInfo.InvariantCulture) +
                   ";slot=" + slot.HoverSlot.ToString(CultureInfo.InvariantCulture) +
                   ";ageUpdates=" + Math.Max(0, slot.HoverAgeUpdates).ToString(CultureInfo.InvariantCulture);
        }

        private static MapQuickAnnouncementVisibilityDecision ResolveVisibilityDecision(
            MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (context.VisibilityDecision != null)
            {
                return context.VisibilityDecision;
            }

            if (context.VisibilityRequest != null)
            {
                context.VisibilityDecision = MapQuickAnnouncementVisibilityService.Evaluate(context.VisibilityRequest);
                return context.VisibilityDecision;
            }

            if (context.Tile == null && context.Wall == null)
            {
                return null;
            }

            context.VisibilityDecision = MapQuickAnnouncementVisibilityService.Evaluate(
                new MapQuickAnnouncementVisibilityRequest
                {
                    TileX = context.MouseTileX,
                    TileY = context.MouseTileY,
                    Tile = context.Tile,
                    Wall = context.Wall
                });
            return context.VisibilityDecision;
        }

        private static MapQuickAnnouncementTileTarget FilterTileTarget(
            MapQuickAnnouncementTileTarget tile,
            MapQuickAnnouncementVisibilityDecision visibility)
        {
            if (tile == null)
            {
                return null;
            }

            var filtered = new MapQuickAnnouncementTileTarget();
            if (tile.Active && AllowsWorldLayer(visibility == null ? null : visibility.Tile))
            {
                filtered.Active = true;
                filtered.TileType = tile.TileType;
                filtered.TileStyle = tile.TileStyle;
                filtered.FrameX = tile.FrameX;
                filtered.FrameY = tile.FrameY;
                filtered.TileName = tile.TileName;
                filtered.NameSource = tile.NameSource;
            }

            if (tile.HasLiquid && AllowsWorldLayer(visibility == null ? null : visibility.Liquid))
            {
                filtered.LiquidAmount = tile.LiquidAmount;
                filtered.LiquidType = tile.LiquidType;
            }

            if (tile.HasCircuitLayer && AllowsCircuitLayer(visibility == null ? null : visibility.Circuit))
            {
                // Circuit-only is a user exception, not proof that the same
                // tile/wall/liquid is visible.
                filtered.RedWire = tile.RedWire;
                filtered.BlueWire = tile.BlueWire;
                filtered.GreenWire = tile.GreenWire;
                filtered.YellowWire = tile.YellowWire;
                filtered.Actuator = tile.Actuator;
            }

            return filtered.HasAnyLayer ? filtered : null;
        }

        private static bool AllowsWorldLayer(MapQuickAnnouncementLayerVisibility layer)
        {
            return layer != null &&
                   (layer.Verdict == MapQuickAnnouncementVisibilityVerdict.Visible ||
                    layer.Verdict == MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed);
        }

        private static bool AllowsCircuitLayer(MapQuickAnnouncementLayerVisibility layer)
        {
            return layer != null &&
                   layer.Verdict == MapQuickAnnouncementVisibilityVerdict.CircuitOnly;
        }

        private static bool ShouldBuildVisibilityBlockedResult(
            MapQuickAnnouncementResolveContext context,
            MapQuickAnnouncementVisibilityDecision visibility)
        {
            if (context == null || visibility == null || visibility.HasAnyAnnounceableLayer)
            {
                return false;
            }

            if (context.Tile != null && context.Tile.HasAnyLayer)
            {
                return true;
            }

            if (context.Wall != null && context.Wall.Active)
            {
                return true;
            }

            return IsInvisibleAir(context, visibility);
        }

        private static MapQuickAnnouncementResolveResult BuildVisibilityBlockedResult(
            MapQuickAnnouncementResolveContext context,
            MapQuickAnnouncementVisibilityDecision visibility)
        {
            var invisibleAir = IsInvisibleAir(context, visibility);
            var body = MapQuickAnnouncementTextBuilder.BuildInvisibleWorldText();
            return WithVisibilitySummary(
                new MapQuickAnnouncementResolveResult
                {
                    Kind = invisibleAir ? MapQuickAnnouncementTargetKind.Air : MapQuickAnnouncementTargetKind.Tile,
                    Body = body,
                    Detail = BuildVisibilityBlockedDetail(visibility, invisibleAir),
                    TargetName = body,
                    TargetCount = 0,
                    FailureReason = "visibilityBlocked"
                },
                visibility,
                invisibleAir);
        }

        private static bool IsInvisibleAir(
            MapQuickAnnouncementResolveContext context,
            MapQuickAnnouncementVisibilityDecision visibility)
        {
            if (context == null || visibility == null || visibility.EmptyAirVisible)
            {
                return false;
            }

            var hasTileLayer = context.Tile != null && context.Tile.HasAnyLayer;
            var hasWallLayer = context.Wall != null && context.Wall.Active;
            return !hasTileLayer && !hasWallLayer &&
                   (context.Tile != null ||
                    context.VisibilityDecision != null ||
                    context.VisibilityRequest != null);
        }

        private static string BuildVisibilityBlockedDetail(
            MapQuickAnnouncementVisibilityDecision visibility,
            bool invisibleAir)
        {
            return "visibilityBlocked;reason=" +
                   (invisibleAir ? "invisible-air" : "world") +
                   ";tile=" + FormatVisibilityLayer(visibility == null ? null : visibility.Tile) +
                   ";wall=" + FormatVisibilityLayer(visibility == null ? null : visibility.Wall) +
                   ";liquid=" + FormatVisibilityLayer(visibility == null ? null : visibility.Liquid) +
                   ";circuit=" + FormatVisibilityLayer(visibility == null ? null : visibility.Circuit);
        }

        private static string FormatVisibilityLayer(MapQuickAnnouncementLayerVisibility layer)
        {
            if (layer == null)
            {
                return "none";
            }

            return layer.Verdict.ToString() + ":" + (layer.Reason ?? string.Empty);
        }

        private static MapQuickAnnouncementResolveResult WithVisibilitySummary(
            MapQuickAnnouncementResolveResult result,
            MapQuickAnnouncementVisibilityDecision visibility,
            bool invisibleAir)
        {
            if (result == null)
            {
                return null;
            }

            var summary = BuildVisibilitySummary(visibility, invisibleAir);
            result.VisibilityVerdict = summary.Verdict;
            result.VisibilityReason = summary.Reason;
            result.VisibleLayers = summary.VisibleLayers;
            result.BlockedLayers = summary.BlockedLayers;
            result.CircuitOnly = summary.CircuitOnly;
            result.EchoGate = summary.EchoGate;
            result.InvisibleAir = invisibleAir;
            result.VisibilityUnavailableReason = summary.UnavailableReason;
            return result;
        }

        private static VisibilitySummary BuildVisibilitySummary(
            MapQuickAnnouncementVisibilityDecision visibility,
            bool invisibleAir)
        {
            var summary = new VisibilitySummary();
            if (visibility == null)
            {
                summary.Verdict = invisibleAir ? "Invisible" : string.Empty;
                summary.Reason = invisibleAir ? "air:noVisibleEvidence" : string.Empty;
                summary.EchoGate = string.Empty;
                return summary;
            }

            var visibleLayers = new List<string>();
            var blockedLayers = new List<string>();
            AddLayerSummary(visibility.Tile, "tile", visibleLayers, blockedLayers);
            AddLayerSummary(visibility.Wall, "wall", visibleLayers, blockedLayers);
            AddLayerSummary(visibility.Liquid, "liquid", visibleLayers, blockedLayers);
            AddLayerSummary(visibility.Circuit, "circuit", visibleLayers, blockedLayers);

            summary.VisibleLayers = string.Join(",", visibleLayers.ToArray());
            summary.BlockedLayers = string.Join(",", blockedLayers.ToArray());
            summary.CircuitOnly = IsCircuitOnlySummary(visibility, visibleLayers);
            summary.EchoGate = ResolveEchoGate(visibility);
            summary.UnavailableReason = ResolveUnavailableReason(visibility);
            summary.Verdict = ResolveOverallVisibilityVerdict(
                visibility,
                invisibleAir,
                summary.CircuitOnly,
                visibleLayers.Count > 0,
                !string.IsNullOrWhiteSpace(summary.UnavailableReason));
            summary.Reason = ResolveOverallVisibilityReason(
                visibility,
                summary.Verdict,
                invisibleAir,
                summary.UnavailableReason);
            return summary;
        }

        private static void AddLayerSummary(
            MapQuickAnnouncementLayerVisibility layer,
            string layerName,
            ICollection<string> visibleLayers,
            ICollection<string> blockedLayers)
        {
            if (layer == null || !IsPresentLayer(layer))
            {
                return;
            }

            if (layer.AllowsAnnouncement)
            {
                visibleLayers.Add(layerName);
                return;
            }

            if (layer.Verdict == MapQuickAnnouncementVisibilityVerdict.Invisible ||
                layer.Verdict == MapQuickAnnouncementVisibilityVerdict.Unavailable)
            {
                blockedLayers.Add(layerName);
            }
        }

        private static bool IsPresentLayer(MapQuickAnnouncementLayerVisibility layer)
        {
            return layer != null &&
                   !string.Equals(layer.Reason, "tile:notPresent", StringComparison.Ordinal) &&
                   !string.Equals(layer.Reason, "wall:notPresent", StringComparison.Ordinal) &&
                   !string.Equals(layer.Reason, "liquid:notPresent", StringComparison.Ordinal) &&
                   !string.Equals(layer.Reason, "circuit:notPresent", StringComparison.Ordinal);
        }

        private static bool IsCircuitOnlySummary(
            MapQuickAnnouncementVisibilityDecision visibility,
            ICollection<string> visibleLayers)
        {
            return visibility != null &&
                   visibility.HasCircuitOnlyLayer &&
                   visibleLayers.Count == 1 &&
                   visibleLayers.Contains("circuit");
        }

        private static string ResolveOverallVisibilityVerdict(
            MapQuickAnnouncementVisibilityDecision visibility,
            bool invisibleAir,
            bool circuitOnly,
            bool hasVisibleLayer,
            bool hasUnavailableLayer)
        {
            if (invisibleAir)
            {
                return MapQuickAnnouncementVisibilityVerdict.Invisible.ToString();
            }

            if (circuitOnly)
            {
                return MapQuickAnnouncementVisibilityVerdict.CircuitOnly.ToString();
            }

            if (HasLayerVerdict(visibility, MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed))
            {
                return MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed.ToString();
            }

            if (hasVisibleLayer)
            {
                return MapQuickAnnouncementVisibilityVerdict.Visible.ToString();
            }

            if (hasUnavailableLayer)
            {
                return MapQuickAnnouncementVisibilityVerdict.Unavailable.ToString();
            }

            return MapQuickAnnouncementVisibilityVerdict.Invisible.ToString();
        }

        private static string ResolveOverallVisibilityReason(
            MapQuickAnnouncementVisibilityDecision visibility,
            string verdict,
            bool invisibleAir,
            string unavailableReason)
        {
            if (invisibleAir)
            {
                return string.IsNullOrWhiteSpace(visibility == null ? null : visibility.EmptyAirReason)
                    ? "air:noVisibleEvidence"
                    : visibility.EmptyAirReason;
            }

            if (string.Equals(verdict, MapQuickAnnouncementVisibilityVerdict.CircuitOnly.ToString(), StringComparison.Ordinal))
            {
                return ReasonOrEmpty(visibility == null ? null : visibility.Circuit);
            }

            if (string.Equals(verdict, MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed.ToString(), StringComparison.Ordinal))
            {
                return FirstReasonWithVerdict(visibility, MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed);
            }

            if (string.Equals(verdict, MapQuickAnnouncementVisibilityVerdict.Visible.ToString(), StringComparison.Ordinal))
            {
                return FirstReasonWithVerdict(visibility, MapQuickAnnouncementVisibilityVerdict.Visible);
            }

            if (string.Equals(verdict, MapQuickAnnouncementVisibilityVerdict.Unavailable.ToString(), StringComparison.Ordinal))
            {
                return unavailableReason;
            }

            return FirstBlockedReason(visibility);
        }

        private static bool HasLayerVerdict(
            MapQuickAnnouncementVisibilityDecision visibility,
            MapQuickAnnouncementVisibilityVerdict verdict)
        {
            return LayerHasVerdict(visibility == null ? null : visibility.Tile, verdict) ||
                   LayerHasVerdict(visibility == null ? null : visibility.Wall, verdict) ||
                   LayerHasVerdict(visibility == null ? null : visibility.Liquid, verdict) ||
                   LayerHasVerdict(visibility == null ? null : visibility.Circuit, verdict);
        }

        private static bool LayerHasVerdict(
            MapQuickAnnouncementLayerVisibility layer,
            MapQuickAnnouncementVisibilityVerdict verdict)
        {
            return layer != null && IsPresentLayer(layer) && layer.Verdict == verdict;
        }

        private static string FirstReasonWithVerdict(
            MapQuickAnnouncementVisibilityDecision visibility,
            MapQuickAnnouncementVisibilityVerdict verdict)
        {
            var reason = ReasonIfVerdict(visibility == null ? null : visibility.Tile, verdict);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            reason = ReasonIfVerdict(visibility == null ? null : visibility.Wall, verdict);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            reason = ReasonIfVerdict(visibility == null ? null : visibility.Liquid, verdict);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            return ReasonIfVerdict(visibility == null ? null : visibility.Circuit, verdict);
        }

        private static string ReasonIfVerdict(
            MapQuickAnnouncementLayerVisibility layer,
            MapQuickAnnouncementVisibilityVerdict verdict)
        {
            return LayerHasVerdict(layer, verdict) ? ReasonOrEmpty(layer) : string.Empty;
        }

        private static string FirstBlockedReason(MapQuickAnnouncementVisibilityDecision visibility)
        {
            var reason = FirstBlockedLayerReason(visibility == null ? null : visibility.Tile);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            reason = FirstBlockedLayerReason(visibility == null ? null : visibility.Wall);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            reason = FirstBlockedLayerReason(visibility == null ? null : visibility.Liquid);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason;
            }

            return FirstBlockedLayerReason(visibility == null ? null : visibility.Circuit);
        }

        private static string FirstBlockedLayerReason(MapQuickAnnouncementLayerVisibility layer)
        {
            if (layer == null || !IsPresentLayer(layer))
            {
                return string.Empty;
            }

            return layer.Verdict == MapQuickAnnouncementVisibilityVerdict.Invisible ||
                   layer.Verdict == MapQuickAnnouncementVisibilityVerdict.Unavailable
                ? ReasonOrEmpty(layer)
                : string.Empty;
        }

        private static string ResolveUnavailableReason(MapQuickAnnouncementVisibilityDecision visibility)
        {
            return FirstReasonWithVerdict(visibility, MapQuickAnnouncementVisibilityVerdict.Unavailable);
        }

        private static string ResolveEchoGate(MapQuickAnnouncementVisibilityDecision visibility)
        {
            if (visibility == null)
            {
                return string.Empty;
            }

            if (AnyReasonContains(visibility, "echoNative"))
            {
                return "echoNative";
            }

            if (AnyReasonContains(visibility, "echoView"))
            {
                return "echoVisible";
            }

            if (AnyReasonContains(visibility, "hiddenWithoutEchoView"))
            {
                return "hiddenWithoutEchoView";
            }

            return "none";
        }

        private static bool AnyReasonContains(
            MapQuickAnnouncementVisibilityDecision visibility,
            string token)
        {
            return ReasonContains(visibility == null ? null : visibility.Tile, token) ||
                   ReasonContains(visibility == null ? null : visibility.Wall, token) ||
                   ReasonContains(visibility == null ? null : visibility.Liquid, token) ||
                   ReasonContains(visibility == null ? null : visibility.Circuit, token) ||
                   ReasonListContains(visibility == null ? null : visibility.Reasons, token);
        }

        private static bool ReasonContains(
            MapQuickAnnouncementLayerVisibility layer,
            string token)
        {
            return layer != null &&
                   layer.Reason != null &&
                   layer.Reason.IndexOf(token, StringComparison.Ordinal) >= 0;
        }

        private static bool ReasonListContains(
            IEnumerable<string> reasons,
            string token)
        {
            if (reasons == null)
            {
                return false;
            }

            foreach (var reason in reasons)
            {
                if (reason != null && reason.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReasonOrEmpty(MapQuickAnnouncementLayerVisibility layer)
        {
            return layer == null ? string.Empty : (layer.Reason ?? string.Empty);
        }

        private sealed class VisibilitySummary
        {
            public VisibilitySummary()
            {
                Verdict = string.Empty;
                Reason = string.Empty;
                VisibleLayers = string.Empty;
                BlockedLayers = string.Empty;
                EchoGate = string.Empty;
                UnavailableReason = string.Empty;
            }

            public string Verdict { get; set; }
            public string Reason { get; set; }
            public string VisibleLayers { get; set; }
            public string BlockedLayers { get; set; }
            public bool CircuitOnly { get; set; }
            public string EchoGate { get; set; }
            public string UnavailableReason { get; set; }
        }

        private static string BuildTileTargetName(MapQuickAnnouncementTileTarget tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            if (tile.Active)
            {
                return tile.TileName;
            }

            if (tile.HasLiquid)
            {
                return MapQuickAnnouncementTextBuilder.BuildLiquidName(tile.LiquidType);
            }

            return tile.HasCircuitLayer ? "电路层" : string.Empty;
        }

        private static string BuildTileDetail(MapQuickAnnouncementTileTarget tile)
        {
            if (tile == null)
            {
                return "tile";
            }

            if (!tile.Active)
            {
                if (tile.HasLiquid)
                {
                    return "tile:liquid;liquid=" +
                           MapQuickAnnouncementTextBuilder.BuildLiquidName(tile.LiquidType) +
                           ":" +
                           tile.LiquidAmount.ToString(CultureInfo.InvariantCulture);
                }

                if (tile.HasCircuitLayer)
                {
                    return "tile:circuitOnly" + BuildCircuitDetail(tile);
                }

                return "tile";
            }

            var source = string.IsNullOrWhiteSpace(tile.NameSource) ? "unknown" : tile.NameSource.Trim();
            var detail = "tile:" + source +
                         ";type=" + tile.TileType.ToString(CultureInfo.InvariantCulture) +
                         ";style=" + tile.TileStyle.ToString(CultureInfo.InvariantCulture) +
                         ";frame=" + tile.FrameX.ToString(CultureInfo.InvariantCulture) +
                         "," + tile.FrameY.ToString(CultureInfo.InvariantCulture);
            if (tile.HasLiquid)
            {
                detail += ";liquid=" +
                          MapQuickAnnouncementTextBuilder.BuildLiquidName(tile.LiquidType) +
                          ":" +
                          tile.LiquidAmount.ToString(CultureInfo.InvariantCulture);
            }

            return detail;
        }

        private static string BuildCircuitDetail(MapQuickAnnouncementTileTarget tile)
        {
            if (tile == null || !tile.HasCircuitLayer)
            {
                return string.Empty;
            }

            var parts = new List<string>(5);
            if (tile.RedWire)
            {
                parts.Add("red");
            }

            if (tile.BlueWire)
            {
                parts.Add("blue");
            }

            if (tile.GreenWire)
            {
                parts.Add("green");
            }

            if (tile.YellowWire)
            {
                parts.Add("yellow");
            }

            if (tile.Actuator)
            {
                parts.Add("actuator");
            }

            return parts.Count == 0 ? string.Empty : ";circuit=" + string.Join(",", parts.ToArray());
        }

        private static string BuildWallDetail(MapQuickAnnouncementWallTarget wall)
        {
            if (wall == null)
            {
                return "wall";
            }

            var source = string.IsNullOrWhiteSpace(wall.NameSource) ? "unknown" : wall.NameSource.Trim();
            return "wall:" + source +
                   ";type=" + wall.WallType.ToString(CultureInfo.InvariantCulture);
        }

        private static List<MapQuickAnnouncementActorTarget> ResolveActorsAtMouse(MapQuickAnnouncementResolveContext context)
        {
            var result = new List<MapQuickAnnouncementActorTarget>();
            if (context == null || context.Actors == null)
            {
                return result;
            }

            for (var index = 0; index < context.Actors.Count; index++)
            {
                var actor = context.Actors[index];
                if (actor != null && actor.Contains(context.MouseWorldX, context.MouseWorldY))
                {
                    result.Add(actor);
                }
            }

            return result;
        }

        private static MapQuickAnnouncementWorldItemTarget ResolveHitWorldItem(MapQuickAnnouncementResolveContext context)
        {
            if (context == null || context.WorldItems == null)
            {
                return null;
            }

            for (var index = 0; index < context.WorldItems.Count; index++)
            {
                var item = context.WorldItems[index];
                if (item != null && item.Contains(context.MouseWorldX, context.MouseWorldY))
                {
                    return item;
                }
            }

            return null;
        }

        private static int ResolveNearbyStack(MapQuickAnnouncementResolveContext context, MapQuickAnnouncementWorldItemTarget hitItem)
        {
            if (context == null || context.WorldItems == null || hitItem == null)
            {
                return hitItem == null ? 0 : Math.Max(1, hitItem.Stack);
            }

            var stack = 0;
            for (var index = 0; index < context.WorldItems.Count; index++)
            {
                var item = context.WorldItems[index];
                if (item == null ||
                    !item.IsActive ||
                    item.ItemType != hitItem.ItemType ||
                    Math.Abs(item.TileX - hitItem.TileX) > 1 ||
                    Math.Abs(item.TileY - hitItem.TileY) > 1)
                {
                    continue;
                }

                stack += Math.Max(1, item.Stack);
            }

            return Math.Max(1, stack);
        }

        private static int ResolveAirPhraseIndex(MapQuickAnnouncementResolveContext context)
        {
            var count = MapQuickAnnouncementTextBuilder.AirPhraseCount;
            if (count <= 0)
            {
                return 0;
            }

            if (context != null && context.AirPhraseIndex >= 0)
            {
                return context.AirPhraseIndex % count;
            }

            unchecked
            {
                var seed = 2166136261u;
                if (context != null)
                {
                    seed = Mix(seed, (uint)context.MouseTileX);
                    seed = Mix(seed, (uint)context.MouseTileY);
                    seed = Mix(seed, (uint)context.GameUpdateCount);
                    seed = Mix(seed, (uint)(context.GameUpdateCount >> 32));
                }

                return (int)(seed % (uint)count);
            }
        }

        private static uint Mix(uint seed, uint value)
        {
            unchecked
            {
                seed ^= value;
                seed *= 16777619u;
                return seed;
            }
        }

        private static bool TryBuildCurrentContext(out MapQuickAnnouncementResolveContext context, out string skipReason)
        {
            context = null;
            skipReason = string.Empty;

            MapQuickAnnouncementTriggerContext triggerContext;
            if (!TryCaptureCurrentTriggerContext(out triggerContext, out skipReason))
            {
                return false;
            }

            context = CreateContextFromTrigger(triggerContext, triggerContext.GameUpdateCount);

            AddUiHoverItem(context);
            AddPlayers(context);
            AddNpcs(context);
            AddWorldItems(context);
            AddTileAndWall(context);
            return true;
        }

        private static MapQuickAnnouncementResolveContext CreateContextFromTrigger(
            MapQuickAnnouncementTriggerContext triggerContext,
            ulong gameUpdateCount)
        {
            triggerContext = triggerContext ?? new MapQuickAnnouncementTriggerContext();
            return new MapQuickAnnouncementResolveContext
            {
                MouseWorldX = triggerContext.MouseWorldX,
                MouseWorldY = triggerContext.MouseWorldY,
                MouseScreenX = triggerContext.MouseScreenX,
                MouseScreenY = triggerContext.MouseScreenY,
                MouseTileX = triggerContext.MouseTileX,
                MouseTileY = triggerContext.MouseTileY,
                GameUpdateCount = gameUpdateCount
            };
        }

        internal static bool TryAddUiHoverItemForTesting(MapQuickAnnouncementResolveContext context)
        {
            return AddUiHoverItem(context);
        }

        private static bool AddUiHoverItem(MapQuickAnnouncementResolveContext context)
        {
            string uiHoverReadStatus;
            return AddUiHoverItem(context, out uiHoverReadStatus);
        }

        private static bool AddUiHoverItem(MapQuickAnnouncementResolveContext context, out string uiHoverReadStatus)
        {
            TerrariaUiHoverSlotSnapshot slotSnapshot;
            TerrariaUiHoverSlotReadResult readResult = null;
            uiHoverReadStatus = string.Empty;
            if (context == null ||
                !TerrariaUiMouseCompat.TryReadFreshHoverSlotSnapshot(
                    context.GameUpdateCount,
                    context.MouseScreenX,
                    context.MouseScreenY,
                    out slotSnapshot,
                    out readResult) ||
                slotSnapshot == null)
            {
                uiHoverReadStatus = BuildUiHoverReadStatus(readResult);
                return false;
            }

            uiHoverReadStatus = BuildUiHoverReadStatus(readResult);
            context.UiSlot = new MapQuickAnnouncementUiSlotTarget
            {
                IsKnown = true,
                HasActiveItem = slotSnapshot.HasActiveItem,
                HoverSource = NormalizeHoverSource(slotSnapshot.Source),
                HoverContext = slotSnapshot.Context,
                HoverSlot = slotSnapshot.Slot,
                HoverAgeUpdates = ResolveHoverAgeUpdates(context.GameUpdateCount, slotSnapshot.GameUpdateCount)
            };

            var snapshot = slotSnapshot.ItemSnapshot;
            if (!slotSnapshot.HasActiveItem || snapshot == null)
            {
                return true;
            }

            context.UiItem = new MapQuickAnnouncementItemTarget
            {
                ItemType = snapshot.ItemType,
                Stack = snapshot.Stack,
                Name = MapQuickAnnouncementNameResolver.ResolveItemName(snapshot.ItemType, snapshot.Name),
                HoverSource = NormalizeHoverSource(snapshot.Source),
                HoverContext = snapshot.Context,
                HoverSlot = snapshot.Slot,
                HoverAgeUpdates = ResolveHoverAgeUpdates(context.GameUpdateCount, snapshot.GameUpdateCount)
            };
            return true;
        }

        private static string BuildUiHoverReadStatus(TerrariaUiHoverSlotReadResult readResult)
        {
            if (readResult == null || string.IsNullOrWhiteSpace(readResult.Status))
            {
                return TerrariaUiMouseCompat.ItemSlotHoverHookInstalled ? "unknown" : "hookNotInstalled";
            }

            if (!TerrariaUiMouseCompat.ItemSlotHoverHookInstalled &&
                string.Equals(readResult.Status, "noSnapshot", StringComparison.Ordinal))
            {
                return "hookNotInstalled";
            }

            return readResult.Status.Trim();
        }

        private static string NormalizeHoverSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return "unknown";
            }

            var trimmed = source.Trim();
            var separator = trimmed.IndexOf(':');
            return separator > 0 ? trimmed.Substring(0, separator) : trimmed;
        }

        private static int ResolveHoverAgeUpdates(ulong currentGameUpdateCount, ulong snapshotGameUpdateCount)
        {
            if (currentGameUpdateCount < snapshotGameUpdateCount)
            {
                return -1;
            }

            var age = currentGameUpdateCount - snapshotGameUpdateCount;
            return age > int.MaxValue ? int.MaxValue : (int)age;
        }

        private static void AddPlayers(MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var players = TerrariaMainCompat.Players;
            if (players == null)
            {
                return;
            }

            var localIndex = TerrariaMainCompat.MyPlayerIndex;
            for (var index = 0; index < players.Length; index++)
            {
                var player = players[index];
                if (!TerrariaPlayerReadCompat.IsActive(player) ||
                    TerrariaPlayerReadCompat.IsDead(player) ||
                    TerrariaPlayerReadCompat.IsGhost(player))
                {
                    continue;
                }

                var hitbox = TerrariaPlayerReadCompat.Hitbox(player);
                context.Actors.Add(new MapQuickAnnouncementActorTarget
                {
                    IsPlayer = true,
                    IsLocalPlayer = TerrariaPlayerReadCompat.WhoAmI(player) == localIndex || index == localIndex,
                    WhoAmI = TerrariaPlayerReadCompat.WhoAmI(player),
                    Name = TerrariaPlayerReadCompat.Name(player),
                    Life = TerrariaPlayerReadCompat.CurrentLife(player),
                    LifeMax = TerrariaPlayerReadCompat.MaxLife(player),
                    Mana = TerrariaPlayerReadCompat.CurrentMana(player),
                    ManaMax = TerrariaPlayerReadCompat.MaxMana(player),
                    HitboxX = hitbox.X,
                    HitboxY = hitbox.Y,
                    HitboxWidth = hitbox.Width,
                    HitboxHeight = hitbox.Height
                });
            }
        }

        private static void AddNpcs(MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var npcs = TerrariaMainCompat.Npcs;
            if (npcs == null)
            {
                return;
            }

            for (var index = 0; index < npcs.Length; index++)
            {
                var npc = npcs[index];
                if (!TerrariaNpcReadCompat.IsActive(npc) ||
                    TerrariaNpcReadCompat.IsHidden(npc) ||
                    TerrariaNpcReadCompat.Life(npc) <= 0)
                {
                    continue;
                }

                var hitbox = TerrariaNpcReadCompat.Hitbox(npc);
                var type = TerrariaNpcReadCompat.Type(npc);
                var name = TerrariaNpcReadCompat.Name(npc);
                context.Actors.Add(new MapQuickAnnouncementActorTarget
                {
                    IsPlayer = false,
                    IsTownNpc = TerrariaNpcReadCompat.IsTownNpc(npc),
                    Type = type,
                    WhoAmI = TerrariaNpcReadCompat.WhoAmI(npc),
                    Name = name,
                    TypeName = MapQuickAnnouncementNameResolver.ResolveNpcTypeName(type, name),
                    Life = TerrariaNpcReadCompat.Life(npc),
                    LifeMax = TerrariaNpcReadCompat.LifeMax(npc),
                    HitboxX = hitbox.X,
                    HitboxY = hitbox.Y,
                    HitboxWidth = hitbox.Width,
                    HitboxHeight = hitbox.Height
                });
            }
        }

        private static void AddWorldItems(MapQuickAnnouncementResolveContext context)
        {
            if (context == null)
            {
                return;
            }

            var items = InformationReflection.GetStaticMember(TerrariaRuntimeTypes.MainType, "item");
            var count = GetCollectionCount(items);
            for (var index = 0; index < count; index++)
            {
                var rawItem = InformationReflection.GetIndexedValue(items, index);
                MapQuickAnnouncementWorldItemTarget item;
                if (TryReadWorldItem(rawItem, out item))
                {
                    context.WorldItems.Add(item);
                }
            }
        }

        private static bool TryReadWorldItem(object rawItem, out MapQuickAnnouncementWorldItemTarget item)
        {
            item = null;
            if (rawItem == null)
            {
                return false;
            }

            int type;
            int stack;
            if (!InformationReflection.TryReadInt(rawItem, "type", out type) ||
                !InformationReflection.TryReadInt(rawItem, "stack", out stack) ||
                type <= 0 ||
                stack <= 0)
            {
                return false;
            }

            float x;
            float y;
            if (!InformationReflection.TryReadVectorMember(rawItem, "position", out x, out y))
            {
                return false;
            }

            int width;
            int height;
            if (!InformationReflection.TryReadInt(rawItem, "width", out width))
            {
                width = 16;
            }

            if (!InformationReflection.TryReadInt(rawItem, "height", out height))
            {
                height = 16;
            }

            if (width <= 0)
            {
                width = 16;
            }

            if (height <= 0)
            {
                height = 16;
            }

            var centerX = x + width * 0.5f;
            var centerY = y + height * 0.5f;
            var fallbackName = FirstNonEmpty(
                InformationReflection.TryReadString(rawItem, "Name"),
                InformationReflection.TryReadString(rawItem, "HoverName"),
                InformationReflection.TryReadString(rawItem, "name"));
            item = new MapQuickAnnouncementWorldItemTarget
            {
                ItemType = type,
                Stack = stack,
                Name = MapQuickAnnouncementNameResolver.ResolveItemName(type, fallbackName),
                HitboxX = x,
                HitboxY = y,
                HitboxWidth = width,
                HitboxHeight = height,
                TileX = (int)Math.Floor(centerX / TerrariaTileReadCompat.TileSize),
                TileY = (int)Math.Floor(centerY / TerrariaTileReadCompat.TileSize)
            };
            return true;
        }

        private static void AddTileAndWall(MapQuickAnnouncementResolveContext context)
        {
            if (context == null ||
                !TerrariaMainCompat.IsTileCoordinateInWorld(context.MouseTileX, context.MouseTileY))
            {
                return;
            }

            Tile tile;
            if (!TerrariaTileReadCompat.TryGetTile(context.MouseTileX, context.MouseTileY, out tile))
            {
                context.VisibilityDecision = BuildUnavailableVisibilityDecision("tileReadFailed");
                return;
            }

            var active = TerrariaTileReadCompat.IsActive(tile);
            var tileType = TerrariaTileReadCompat.Type(tile);
            var frameX = TerrariaTileReadCompat.FrameX(tile);
            var frameY = TerrariaTileReadCompat.FrameY(tile);
            var tileStyle = MapQuickAnnouncementTileStyleResolver.ResolveTileStyle(tileType, frameX, frameY);
            string tileNameSource = string.Empty;
            var tileName = active
                ? MapQuickAnnouncementNameResolver.ResolveTileName(tileType, tileStyle, string.Empty, out tileNameSource)
                : string.Empty;
            context.Tile = new MapQuickAnnouncementTileTarget
            {
                Active = active,
                TileType = tileType,
                TileStyle = tileStyle,
                FrameX = frameX,
                FrameY = frameY,
                TileName = tileName,
                NameSource = active ? tileNameSource : string.Empty,
                RedWire = TerrariaTileReadCompat.HasRedWire(tile),
                BlueWire = TerrariaTileReadCompat.HasBlueWire(tile),
                GreenWire = TerrariaTileReadCompat.HasGreenWire(tile),
                YellowWire = TerrariaTileReadCompat.HasYellowWire(tile),
                Actuator = TerrariaTileReadCompat.HasActuator(tile),
                LiquidAmount = TerrariaTileReadCompat.LiquidAmount(tile),
                LiquidType = TerrariaTileReadCompat.LiquidType(tile)
            };

            var wallType = TerrariaTileReadCompat.Wall(tile);
            if (wallType > 0)
            {
                string wallNameSource;
                context.Wall = new MapQuickAnnouncementWallTarget
                {
                    Active = true,
                    WallType = wallType,
                    WallName = MapQuickAnnouncementNameResolver.ResolveWallName(wallType, string.Empty, out wallNameSource),
                    NameSource = wallNameSource
                };
            }

            context.VisibilityRequest = new MapQuickAnnouncementVisibilityRequest
            {
                TileX = context.MouseTileX,
                TileY = context.MouseTileY,
                RawTile = tile,
                Tile = context.Tile,
                Wall = context.Wall
            };
        }

        private static MapQuickAnnouncementVisibilityDecision BuildUnavailableVisibilityDecision(string reason)
        {
            reason = string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason.Trim();
            var decision = new MapQuickAnnouncementVisibilityDecision
            {
                Tile = MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Tile,
                    reason),
                Wall = MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Wall,
                    reason),
                Liquid = MapQuickAnnouncementVisibilityDecision.Unavailable(
                    MapQuickAnnouncementVisibilityLayer.Liquid,
                    reason)
            };
            decision.AddReason("compat:" + reason);
            return decision;
        }

        private static int GetCollectionCount(object source)
        {
            if (source == null)
            {
                return 0;
            }

            var array = source as Array;
            if (array != null)
            {
                return array.Rank == 1 ? array.GetLength(0) : 0;
            }

            var collection = source as System.Collections.ICollection;
            return collection == null ? 0 : collection.Count;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (var index = 0; index < values.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }
    }
}
