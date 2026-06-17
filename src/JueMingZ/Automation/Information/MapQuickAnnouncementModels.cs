using System;
using System.Collections.Generic;

namespace JueMingZ.Automation.Information
{
    internal enum MapQuickAnnouncementTargetKind
    {
        None = 0,
        UiItem = 1,
        Actor = 2,
        WorldItem = 3,
        Tile = 4,
        Wall = 5,
        Air = 6
    }

    internal sealed class MapQuickAnnouncementResolveContext
    {
        public MapQuickAnnouncementResolveContext()
        {
            Actors = new List<MapQuickAnnouncementActorTarget>();
            WorldItems = new List<MapQuickAnnouncementWorldItemTarget>();
            AirPhraseIndex = -1;
        }

        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public int MouseTileX { get; set; }
        public int MouseTileY { get; set; }
        public ulong GameUpdateCount { get; set; }
        public int AirPhraseIndex { get; set; }
        public MapQuickAnnouncementItemTarget UiItem { get; set; }
        public MapQuickAnnouncementUiSlotTarget UiSlot { get; set; }
        public IList<MapQuickAnnouncementActorTarget> Actors { get; private set; }
        public IList<MapQuickAnnouncementWorldItemTarget> WorldItems { get; private set; }
        public MapQuickAnnouncementTileTarget Tile { get; set; }
        public MapQuickAnnouncementWallTarget Wall { get; set; }
        public MapQuickAnnouncementVisibilityRequest VisibilityRequest { get; set; }
        public MapQuickAnnouncementVisibilityDecision VisibilityDecision { get; set; }
    }

    internal sealed class MapQuickAnnouncementResolveResult
    {
        public MapQuickAnnouncementTargetKind Kind { get; set; }
        public string Body { get; set; }
        public string Detail { get; set; }
        public string TargetName { get; set; }
        public int TargetCount { get; set; }
        public bool SuppressDelivery { get; set; }
        public string FailureReason { get; set; }
        public string VisibilityVerdict { get; set; }
        public string VisibilityReason { get; set; }
        public string VisibleLayers { get; set; }
        public string BlockedLayers { get; set; }
        public bool CircuitOnly { get; set; }
        public string EchoGate { get; set; }
        public bool InvisibleAir { get; set; }
        public string VisibilityUnavailableReason { get; set; }

        public MapQuickAnnouncementResolveResult()
        {
            Kind = MapQuickAnnouncementTargetKind.None;
            Body = string.Empty;
            Detail = string.Empty;
            TargetName = string.Empty;
            FailureReason = string.Empty;
            VisibilityVerdict = string.Empty;
            VisibilityReason = string.Empty;
            VisibleLayers = string.Empty;
            BlockedLayers = string.Empty;
            EchoGate = string.Empty;
            VisibilityUnavailableReason = string.Empty;
        }
    }

    internal sealed class MapQuickAnnouncementItemTarget
    {
        public int ItemType { get; set; }
        public int Stack { get; set; }
        public string Name { get; set; }
        public string HoverSource { get; set; }
        public int HoverContext { get; set; }
        public int HoverSlot { get; set; }
        public int HoverAgeUpdates { get; set; }

        public bool IsActive
        {
            get { return ItemType > 0 && Stack > 0; }
        }
    }

    internal sealed class MapQuickAnnouncementUiSlotTarget
    {
        public bool IsKnown { get; set; }
        public bool HasActiveItem { get; set; }
        public string HoverSource { get; set; }
        public int HoverContext { get; set; }
        public int HoverSlot { get; set; }
        public int HoverAgeUpdates { get; set; }
    }

    internal sealed class MapQuickAnnouncementTriggerContext
    {
        public MapQuickAnnouncementTriggerContext()
        {
            Succeeded = true;
            FailureReason = string.Empty;
        }

        public bool Succeeded { get; set; }
        public string FailureReason { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public int MouseTileX { get; set; }
        public int MouseTileY { get; set; }
        public ulong GameUpdateCount { get; set; }

        public static MapQuickAnnouncementTriggerContext Failed(string reason)
        {
            return new MapQuickAnnouncementTriggerContext
            {
                Succeeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "triggerContextUnavailable" : reason
            };
        }
    }

    internal sealed class MapQuickAnnouncementActorTarget
    {
        public bool IsPlayer { get; set; }
        public bool IsLocalPlayer { get; set; }
        public bool IsTownNpc { get; set; }
        public int Type { get; set; }
        public int WhoAmI { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public int Life { get; set; }
        public int LifeMax { get; set; }
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public float HitboxX { get; set; }
        public float HitboxY { get; set; }
        public float HitboxWidth { get; set; }
        public float HitboxHeight { get; set; }

        public bool Contains(float x, float y)
        {
            return MapQuickAnnouncementGeometry.Contains(HitboxX, HitboxY, HitboxWidth, HitboxHeight, x, y);
        }
    }

    internal sealed class MapQuickAnnouncementWorldItemTarget
    {
        public int ItemType { get; set; }
        public int Stack { get; set; }
        public string Name { get; set; }
        public float HitboxX { get; set; }
        public float HitboxY { get; set; }
        public float HitboxWidth { get; set; }
        public float HitboxHeight { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }

        public bool IsActive
        {
            get { return ItemType > 0 && Stack > 0; }
        }

        public bool Contains(float x, float y)
        {
            return IsActive && MapQuickAnnouncementGeometry.Contains(HitboxX, HitboxY, HitboxWidth, HitboxHeight, x, y);
        }
    }

    internal sealed class MapQuickAnnouncementTileTarget
    {
        public bool Active { get; set; }
        public int TileType { get; set; }
        public int TileStyle { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public string TileName { get; set; }
        public string NameSource { get; set; }
        public bool RedWire { get; set; }
        public bool BlueWire { get; set; }
        public bool GreenWire { get; set; }
        public bool YellowWire { get; set; }
        public bool Actuator { get; set; }
        public int LiquidAmount { get; set; }
        public int LiquidType { get; set; }

        public bool HasLiquid
        {
            get { return LiquidAmount > 0; }
        }

        public bool HasCircuitLayer
        {
            get { return RedWire || BlueWire || GreenWire || YellowWire || Actuator; }
        }

        public bool HasWorldLayer
        {
            get { return Active || HasLiquid; }
        }

        public bool HasAnyLayer
        {
            get { return HasWorldLayer || HasCircuitLayer; }
        }
    }

    internal sealed class MapQuickAnnouncementWallTarget
    {
        public bool Active { get; set; }
        public int WallType { get; set; }
        public string WallName { get; set; }
        public string NameSource { get; set; }
    }

    internal static class MapQuickAnnouncementGeometry
    {
        public static bool Contains(float x, float y, float width, float height, float pointX, float pointY)
        {
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            return pointX >= x &&
                   pointY >= y &&
                   pointX < x + width &&
                   pointY < y + height;
        }
    }
}
