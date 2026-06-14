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
    internal sealed class PlayerWorldDeathMarkerMapLayer : IMapLayer
    {
        internal const int MaxDrawnMarkers = 256;

        public void Draw(ref MapOverlayDrawContext context, ref string text)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!settings.MapPersistentDeathMarkersEnabled)
            {
                PlayerWorldDeathMarkerDiagnostics.RecordDraw("disabled", "persistent death markers disabled", string.Empty, 0, 0, false, false);
                return;
            }

            if (!Main.mapFullscreen)
            {
                PlayerWorldDeathMarkerDiagnostics.RecordDraw("notFullscreen", "persistent death markers draw only on fullscreen map", string.Empty, 0, 0, false, false);
                return;
            }

            if (TextureAssets.MapDeath == null || !TextureAssets.MapDeath.IsLoaded)
            {
                PlayerWorldDeathMarkerDiagnostics.RecordDraw("textureUnavailable", "TextureAssets.MapDeath is not loaded", string.Empty, 0, 0, false, false);
                return;
            }

            var read = PlayerWorldDeathMarkerCache.ReadCurrentMarkers(PlayerWorldDeathMarkerCache.DefaultMaxMarkers);
            if (read == null || read.Markers == null || read.Markers.Count <= 0)
            {
                PlayerWorldDeathMarkerDiagnostics.RecordDraw(
                    read == null ? "readUnavailable" : read.Status,
                    read == null ? "death marker read unavailable" : read.Message,
                    read == null ? string.Empty : read.PairId,
                    0,
                    0,
                    read != null && read.CulledByLimit,
                    read != null && read.HistoryReadFailed);
                return;
            }

            var texture = TextureAssets.MapDeath.Value;
            var frame = new SpriteFrame((byte)1, (byte)1);
            var screen = new Rectangle(0, 0, Math.Max(1, Main.screenWidth), Math.Max(1, Main.screenHeight));
            var drawn = 0;
            var drawLimited = false;

            for (var index = 0; index < read.Markers.Count; index++)
            {
                var marker = read.Markers[index];
                if (marker == null)
                {
                    continue;
                }

                if (!IsVisibleOnScreen(context, texture, frame, marker.TilePosition, screen))
                {
                    continue;
                }

                if (drawn >= MaxDrawnMarkers)
                {
                    drawLimited = true;
                    break;
                }

                var drawResult = context.Draw(texture, marker.TilePosition, frame, Alignment.Center);
                drawn++;
                if (drawResult.IsMouseOver)
                {
                    text = string.IsNullOrWhiteSpace(marker.Tooltip) ? "死亡点" : marker.Tooltip;
                }
            }

            PlayerWorldDeathMarkerDiagnostics.RecordDraw(
                read.Status,
                read.Message,
                read.PairId,
                read.MarkerCount,
                drawn,
                read.CulledByLimit || drawLimited,
                read.HistoryReadFailed);
        }

        private static bool IsVisibleOnScreen(
            MapOverlayDrawContext context,
            Texture2D texture,
            SpriteFrame frame,
            Vector2 tilePosition,
            Rectangle screen)
        {
            var region = context.GetUnclampedDrawRegion(texture, tilePosition, frame, 1f, Alignment.Center);
            return region.Width > 0 &&
                   region.Height > 0 &&
                   region.Intersects(screen);
        }
    }
}
