using System;
using System.Collections.Generic;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal enum MapQuickAnnouncementVisibilityVerdict
    {
        Visible = 0,
        CircuitOnly = 1,
        EchoNativeAllowed = 2,
        Invisible = 3,
        Unavailable = 4
    }

    internal enum MapQuickAnnouncementVisibilityLayer
    {
        Tile = 0,
        Wall = 1,
        Liquid = 2,
        Circuit = 3
    }

    internal sealed class MapQuickAnnouncementVisibilityRequest
    {
        public MapQuickAnnouncementVisibilityRequest()
        {
            TileX = -1;
            TileY = -1;
        }

        public int TileX { get; set; }
        public int TileY { get; set; }
        public Tile RawTile { get; set; }
        public MapQuickAnnouncementTileTarget Tile { get; set; }
        public MapQuickAnnouncementWallTarget Wall { get; set; }
        public object PerspectivePlayer { get; set; }
    }

    internal sealed class MapQuickAnnouncementLayerVisibility
    {
        public MapQuickAnnouncementLayerVisibility(
            MapQuickAnnouncementVisibilityLayer layer,
            MapQuickAnnouncementVisibilityVerdict verdict,
            string reason)
        {
            Layer = layer;
            Verdict = verdict;
            Reason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason.Trim();
        }

        public MapQuickAnnouncementVisibilityLayer Layer { get; private set; }
        public MapQuickAnnouncementVisibilityVerdict Verdict { get; private set; }
        public string Reason { get; private set; }

        public bool AllowsAnnouncement
        {
            get
            {
                return Verdict == MapQuickAnnouncementVisibilityVerdict.Visible ||
                       Verdict == MapQuickAnnouncementVisibilityVerdict.CircuitOnly ||
                       Verdict == MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed;
            }
        }
    }

    internal sealed class MapQuickAnnouncementVisibilityDecision
    {
        public MapQuickAnnouncementVisibilityDecision()
        {
            Tile = Hidden(MapQuickAnnouncementVisibilityLayer.Tile, "tile:notPresent");
            Wall = Hidden(MapQuickAnnouncementVisibilityLayer.Wall, "wall:notPresent");
            Liquid = Hidden(MapQuickAnnouncementVisibilityLayer.Liquid, "liquid:notPresent");
            Circuit = Hidden(MapQuickAnnouncementVisibilityLayer.Circuit, "circuit:notPresent");
            Reasons = new List<string>();
            EmptyAirReason = string.Empty;
        }

        public MapQuickAnnouncementLayerVisibility Tile { get; set; }
        public MapQuickAnnouncementLayerVisibility Wall { get; set; }
        public MapQuickAnnouncementLayerVisibility Liquid { get; set; }
        public MapQuickAnnouncementLayerVisibility Circuit { get; set; }
        public IList<string> Reasons { get; private set; }
        public bool EmptyAirVisible { get; set; }
        public string EmptyAirReason { get; set; }

        public bool HasAnnounceableWorldLayer
        {
            get
            {
                return (Tile != null && Tile.AllowsAnnouncement) ||
                       (Wall != null && Wall.AllowsAnnouncement) ||
                       (Liquid != null && Liquid.AllowsAnnouncement);
            }
        }

        public bool HasCircuitOnlyLayer
        {
            get
            {
                return Circuit != null &&
                       Circuit.Verdict == MapQuickAnnouncementVisibilityVerdict.CircuitOnly;
            }
        }

        public bool HasAnyAnnounceableLayer
        {
            get { return HasAnnounceableWorldLayer || HasCircuitOnlyLayer; }
        }

        public void AddReason(string reason)
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                Reasons.Add(reason.Trim());
            }
        }

        internal static MapQuickAnnouncementLayerVisibility Hidden(
            MapQuickAnnouncementVisibilityLayer layer,
            string reason)
        {
            return new MapQuickAnnouncementLayerVisibility(
                layer,
                MapQuickAnnouncementVisibilityVerdict.Invisible,
                reason);
        }

        internal static MapQuickAnnouncementLayerVisibility Unavailable(
            MapQuickAnnouncementVisibilityLayer layer,
            string reason)
        {
            return new MapQuickAnnouncementLayerVisibility(
                layer,
                MapQuickAnnouncementVisibilityVerdict.Unavailable,
                reason);
        }

        internal static MapQuickAnnouncementLayerVisibility Visible(
            MapQuickAnnouncementVisibilityLayer layer,
            string reason)
        {
            return new MapQuickAnnouncementLayerVisibility(
                layer,
                MapQuickAnnouncementVisibilityVerdict.Visible,
                reason);
        }

        internal static MapQuickAnnouncementLayerVisibility CircuitOnly(string reason)
        {
            return new MapQuickAnnouncementLayerVisibility(
                MapQuickAnnouncementVisibilityLayer.Circuit,
                MapQuickAnnouncementVisibilityVerdict.CircuitOnly,
                reason);
        }

        internal static MapQuickAnnouncementLayerVisibility EchoNativeAllowed(
            MapQuickAnnouncementVisibilityLayer layer,
            string reason)
        {
            return new MapQuickAnnouncementLayerVisibility(
                layer,
                MapQuickAnnouncementVisibilityVerdict.EchoNativeAllowed,
                reason);
        }
    }
}
