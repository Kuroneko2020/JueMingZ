using System;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Records;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Map;
using Terraria.UI;

namespace JueMingZ.Hooks
{
    internal sealed class PlayerWorldMapMarkerMapLayer : IMapLayer
    {
        internal const int MaxDrawnMarkers = PlayerWorldMapMarkerConstants.MaxMarkersPerPair;

        public void Draw(ref MapOverlayDrawContext context, ref string text)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.MapCustomMarkersEnabled || !Main.mapFullscreen)
            {
                return;
            }

            // Map layer drawing is strictly read-only: marker creation and file
            // writes are handled by the runtime interaction service.
            var read = PlayerWorldMapMarkerCache.ReadCurrent();
            if (read == null || read.Markers == null || read.Markers.Count <= 0)
            {
                return;
            }

            var screen = new Rectangle(0, 0, Math.Max(1, Main.screenWidth), Math.Max(1, Main.screenHeight));
            var drawn = 0;
            for (var index = 0; index < read.Markers.Count; index++)
            {
                var marker = read.Markers[index];
                if (marker == null)
                {
                    continue;
                }

                Texture2D texture;
                if (!TryGetItemTexture(marker.IconItemId, out texture))
                {
                    continue;
                }

                var frame = new SpriteFrame((byte)1, (byte)1);
                var tilePosition = new Vector2(marker.TileX + 0.5f, marker.TileY + 0.5f);
                var region = context.GetUnclampedDrawRegion(texture, tilePosition, frame, 1f, Alignment.Center);
                var visible = IsVisibleOnScreen(region, screen);
                PlayerWorldMapMarkerTraceRecorder.RecordDrawIfPending(
                    marker.MarkerId,
                    marker.TileX,
                    marker.TileY,
                    marker.IconItemId,
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height,
                    screen.Width,
                    screen.Height,
                    visible,
                    visible ? string.Empty : "offscreen");
                if (!visible)
                {
                    continue;
                }

                if (drawn >= MaxDrawnMarkers)
                {
                    break;
                }

                var result = context.Draw(texture, tilePosition, frame, Alignment.Center);
                drawn++;
                if (result.IsMouseOver)
                {
                    text = string.IsNullOrWhiteSpace(marker.Name)
                        ? "地图标记：" + PlayerWorldMapMarkerStyles.GetDisplayName(marker.IconItemId)
                        : marker.Name;
                }
            }
        }

        private static bool TryGetItemTexture(int itemId, out Texture2D texture)
        {
            texture = null;
            itemId = PlayerWorldMapMarkerConstants.NormalizeIconItemId(itemId);
            try
            {
                if (TextureAssets.Item == null ||
                    itemId <= 0 ||
                    itemId >= TextureAssets.Item.Length ||
                    TextureAssets.Item[itemId] == null ||
                    !TextureAssets.Item[itemId].IsLoaded)
                {
                    itemId = PlayerWorldMapMarkerConstants.DefaultIconItemId;
                }

                if (TextureAssets.Item == null ||
                    itemId <= 0 ||
                    itemId >= TextureAssets.Item.Length ||
                    TextureAssets.Item[itemId] == null ||
                    !TextureAssets.Item[itemId].IsLoaded)
                {
                    return false;
                }

                texture = TextureAssets.Item[itemId].Value;
                return texture != null;
            }
            catch
            {
                texture = null;
                return false;
            }
        }

        private static bool IsVisibleOnScreen(
            Rectangle region,
            Rectangle screen)
        {
            return region.Width > 0 &&
                   region.Height > 0 &&
                   region.Intersects(screen);
        }
    }
}
