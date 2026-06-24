using System;
using JueMingZ.Automation.Blueprint;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingZ.UI
{
    internal static class BlueprintProjectionGhostRenderer
    {
        private const int TileSize = 16;
        private const int TilePass = 1;
        private const int WallPass = 0;
        private const int WirePass = 2;

        public static bool DrawLayer(
            object spriteBatch,
            BlueprintProjectionCellSnapshot layer,
            int x,
            int y,
            int clipWidth,
            int clipHeight,
            int r,
            int g,
            int b,
            int alpha,
            int borderAlpha)
        {
            if (layer == null || !ShouldDrawLayer(layer.Status))
            {
                return false;
            }

            if (string.Equals(layer.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return DrawWall(spriteBatch, layer, x, y, clipWidth, clipHeight, r, g, b, alpha, borderAlpha);
            }

            if (string.Equals(layer.LayerKind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return DrawWire(spriteBatch, layer, x, y, clipWidth, clipHeight, r, g, b, alpha, borderAlpha);
            }

            if (string.Equals(layer.LayerKind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return DrawActuator(spriteBatch, x, y, clipWidth, clipHeight, r, g, b, alpha, borderAlpha);
            }

            return DrawTile(spriteBatch, layer, x, y, clipWidth, clipHeight, r, g, b, alpha, borderAlpha);
        }

        internal static bool ShouldDrawLayerForTesting(string status)
        {
            return ShouldDrawLayer(status);
        }

        internal static int ResolveLayerDrawPassForTesting(string layerKind)
        {
            return ResolveLayerDrawPass(layerKind);
        }

        internal static int[] ResolveTileSourceRectForTesting(int frameX, int frameY, int textureWidth, int textureHeight, bool halfBrick)
        {
            int sourceX;
            int sourceY;
            int sourceWidth;
            int sourceHeight;
            int destYOffset;
            int destHeight;
            ResolveTileSourceRect(frameX, frameY, textureWidth, textureHeight, halfBrick, out sourceX, out sourceY, out sourceWidth, out sourceHeight, out destYOffset, out destHeight);
            return new[] { sourceX, sourceY, sourceWidth, sourceHeight, destYOffset, destHeight };
        }

        internal static bool ShouldDrawLayer(string status)
        {
            return !string.Equals(status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal) &&
                   !string.Equals(status, BlueprintProjectionLayerStatuses.Completed, StringComparison.Ordinal);
        }

        internal static int ResolveLayerDrawPass(string layerKind)
        {
            if (string.Equals(layerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return WallPass;
            }

            if (string.Equals(layerKind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layerKind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return WirePass;
            }

            return TilePass;
        }

        private static bool DrawTile(object spriteBatch, BlueprintProjectionCellSnapshot layer, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha, int borderAlpha)
        {
            var texture = ResolveTileTexture(layer);
            if (texture == null)
            {
                return false;
            }

            int textureWidth;
            int textureHeight;
            if (!UiPrimitiveRenderer.TryReadTextureDimensions(texture, out textureWidth, out textureHeight))
            {
                return false;
            }

            int sourceX;
            int sourceY;
            int sourceWidth;
            int sourceHeight;
            int destYOffset;
            int destHeight;
            ResolveTileSourceRect(layer.FrameX, layer.FrameY, textureWidth, textureHeight, layer.HalfBrick, out sourceX, out sourceY, out sourceWidth, out sourceHeight, out destYOffset, out destHeight);
            var effectiveAlpha = ResolveCoatingAlpha(layer, alpha);
            var ok = false;
            if (layer.Slope > 0 && !layer.HalfBrick && sourceWidth >= TileSize && sourceHeight >= TileSize)
            {
                ok |= DrawSlope(spriteBatch, texture, layer.Slope, x, y, sourceX, sourceY, clipWidth, clipHeight, r, g, b, effectiveAlpha);
            }
            else
            {
                ok |= UiPrimitiveRenderer.DrawTextureSourceRectClipped(
                    spriteBatch,
                    texture,
                    x,
                    y + destYOffset,
                    TileSize,
                    destHeight,
                    sourceX,
                    sourceY,
                    sourceWidth,
                    sourceHeight,
                    0,
                    0,
                    clipWidth,
                    clipHeight,
                    r,
                    g,
                    b,
                    effectiveAlpha);
            }

            if (layer.Inactive)
            {
                ok |= DrawInactiveMarker(spriteBatch, x, y, clipWidth, clipHeight, r, g, b, Math.Min(255, borderAlpha + 18));
            }

            if (HasCoatingFlag(layer.CoatingFlags, BlueprintCaptureCoatingFlags.Invisible))
            {
                ok |= DrawInvisibleCoatingMarker(spriteBatch, x, y, clipWidth, clipHeight, r, g, b, borderAlpha);
            }

            if (borderAlpha > 0)
            {
                ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, r, g, b, borderAlpha);
            }

            return ok;
        }

        private static bool DrawWall(object spriteBatch, BlueprintProjectionCellSnapshot layer, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha, int borderAlpha)
        {
            var texture = ResolveWallTexture(layer);
            if (texture == null)
            {
                return false;
            }

            int textureWidth;
            int textureHeight;
            if (!UiPrimitiveRenderer.TryReadTextureDimensions(texture, out textureWidth, out textureHeight))
            {
                return false;
            }

            var sourceWidth = Math.Max(1, Math.Min(32, textureWidth));
            var sourceHeight = Math.Max(1, Math.Min(32, textureHeight));
            var effectiveAlpha = ResolveCoatingAlpha(layer, Math.Max(48, alpha - 22));
            var ok = UiPrimitiveRenderer.DrawTextureSourceRectClipped(
                spriteBatch,
                texture,
                x,
                y,
                TileSize,
                TileSize,
                0,
                0,
                sourceWidth,
                sourceHeight,
                0,
                0,
                clipWidth,
                clipHeight,
                r,
                g,
                b,
                effectiveAlpha);
            if (HasCoatingFlag(layer.CoatingFlags, BlueprintCaptureCoatingFlags.Invisible))
            {
                ok |= DrawInvisibleCoatingMarker(spriteBatch, x, y, clipWidth, clipHeight, r, g, b, borderAlpha);
            }

            if (borderAlpha > 0)
            {
                ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x, y, TileSize, TileSize, 1, 0, 0, clipWidth, clipHeight, r, g, b, Math.Max(1, borderAlpha - 42));
            }

            return ok;
        }

        private static bool DrawWire(object spriteBatch, BlueprintProjectionCellSnapshot layer, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha, int borderAlpha)
        {
            var flags = layer.ContentId;
            var lineAlpha = Math.Min(255, Math.Max(alpha + 32, borderAlpha));
            var ok = false;
            if ((flags & BlueprintCaptureWireFlags.Red) != 0)
            {
                ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 2, y + 4, TileSize - 4, 2, 0, 0, clipWidth, clipHeight, r, g, b, lineAlpha);
            }

            if ((flags & BlueprintCaptureWireFlags.Blue) != 0)
            {
                ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 4, y + 2, 2, TileSize - 4, 0, 0, clipWidth, clipHeight, r, g, b, lineAlpha);
            }

            if ((flags & BlueprintCaptureWireFlags.Green) != 0)
            {
                ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 2, y + 10, TileSize - 4, 2, 0, 0, clipWidth, clipHeight, r, g, b, lineAlpha);
            }

            if ((flags & BlueprintCaptureWireFlags.Yellow) != 0)
            {
                ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 10, y + 2, 2, TileSize - 4, 0, 0, clipWidth, clipHeight, r, g, b, lineAlpha);
            }

            return borderAlpha > 0
                ? ok || UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x + 3, y + 3, TileSize - 6, TileSize - 6, 1, 0, 0, clipWidth, clipHeight, r, g, b, borderAlpha)
                : ok;
        }

        private static bool DrawActuator(object spriteBatch, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha, int borderAlpha)
        {
            var texture = ResolveTexture(TextureAssets.Actuator);
            var ok = false;
            if (texture != null)
            {
                int textureWidth;
                int textureHeight;
                if (UiPrimitiveRenderer.TryReadTextureDimensions(texture, out textureWidth, out textureHeight))
                {
                    ok |= UiPrimitiveRenderer.DrawTextureSourceRectClipped(
                        spriteBatch,
                        texture,
                        x,
                        y,
                        TileSize,
                        TileSize,
                        0,
                        0,
                        Math.Min(TileSize, textureWidth),
                        Math.Min(TileSize, textureHeight),
                        0,
                        0,
                        clipWidth,
                        clipHeight,
                        r,
                        g,
                        b,
                        alpha);
                }
            }

            ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 5, y + 5, 6, 6, 0, 0, clipWidth, clipHeight, r, g, b, Math.Min(255, alpha + 24));
            if (borderAlpha > 0)
            {
                ok |= UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, x + 4, y + 4, 8, 8, 1, 0, 0, clipWidth, clipHeight, r, g, b, borderAlpha);
            }

            return ok;
        }

        private static bool DrawSlope(object spriteBatch, Texture2D texture, int slope, int x, int y, int sourceX, int sourceY, int clipWidth, int clipHeight, int r, int g, int b, int alpha)
        {
            var ok = false;
            for (var row = 0; row < TileSize; row++)
            {
                int localX;
                int width;
                ResolveSlopeRow(slope, row, out localX, out width);
                if (width <= 0)
                {
                    continue;
                }

                ok |= UiPrimitiveRenderer.DrawTextureSourceRectClipped(
                    spriteBatch,
                    texture,
                    x + localX,
                    y + row,
                    width,
                    1,
                    sourceX + localX,
                    sourceY + row,
                    width,
                    1,
                    0,
                    0,
                    clipWidth,
                    clipHeight,
                    r,
                    g,
                    b,
                    alpha);
            }

            return ok;
        }

        private static void ResolveSlopeRow(int slope, int row, out int localX, out int width)
        {
            row = Math.Max(0, Math.Min(TileSize - 1, row));
            if (slope == 1)
            {
                localX = 0;
                width = row + 1;
                return;
            }

            if (slope == 2)
            {
                width = row + 1;
                localX = TileSize - width;
                return;
            }

            if (slope == 3)
            {
                localX = 0;
                width = TileSize - row;
                return;
            }

            if (slope == 4)
            {
                localX = row;
                width = TileSize - row;
                return;
            }

            localX = 0;
            width = TileSize;
        }

        private static bool DrawInactiveMarker(object spriteBatch, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha)
        {
            var ok = false;
            for (var offset = 2; offset < TileSize - 2; offset += 4)
            {
                ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + offset, y + TileSize - 3 - offset, 2, 2, 0, 0, clipWidth, clipHeight, r, g, b, alpha);
            }

            return ok;
        }

        private static bool DrawInvisibleCoatingMarker(object spriteBatch, int x, int y, int clipWidth, int clipHeight, int r, int g, int b, int alpha)
        {
            var ok = false;
            ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 2, y + 2, 3, 1, 0, 0, clipWidth, clipHeight, r, g, b, alpha);
            ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + 2, y + 2, 1, 3, 0, 0, clipWidth, clipHeight, r, g, b, alpha);
            ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + TileSize - 5, y + 2, 3, 1, 0, 0, clipWidth, clipHeight, r, g, b, alpha);
            ok |= UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + TileSize - 3, y + 2, 1, 3, 0, 0, clipWidth, clipHeight, r, g, b, alpha);
            return ok;
        }

        private static void ResolveTileSourceRect(
            int frameX,
            int frameY,
            int textureWidth,
            int textureHeight,
            bool halfBrick,
            out int sourceX,
            out int sourceY,
            out int sourceWidth,
            out int sourceHeight,
            out int destYOffset,
            out int destHeight)
        {
            textureWidth = Math.Max(0, textureWidth);
            textureHeight = Math.Max(0, textureHeight);
            sourceWidth = Math.Max(1, Math.Min(TileSize, textureWidth));
            sourceHeight = Math.Max(1, Math.Min(TileSize, textureHeight));
            sourceX = frameX >= 0 && frameX + sourceWidth <= textureWidth ? frameX : 0;
            sourceY = frameY >= 0 && frameY + sourceHeight <= textureHeight ? frameY : 0;
            destYOffset = 0;
            destHeight = TileSize;

            if (halfBrick && sourceHeight >= TileSize && sourceY + TileSize <= textureHeight)
            {
                sourceY += TileSize / 2;
                sourceHeight = TileSize / 2;
                destYOffset = TileSize / 2;
                destHeight = TileSize / 2;
            }
        }

        private static Texture2D ResolveTileTexture(BlueprintProjectionCellSnapshot layer)
        {
            if (layer == null || layer.ContentId < 0)
            {
                return null;
            }

            var paint = Math.Max(0, layer.PaintId);
            if (paint > 0)
            {
                try
                {
                    if (Main.instance != null && Main.instance.TilePaintSystem != null)
                    {
                        var painted = Main.instance.TilePaintSystem.TryGetTileAndRequestIfNotReady(layer.ContentId, Math.Max(0, layer.Style), paint);
                        if (painted != null)
                        {
                            return painted;
                        }
                    }
                }
                catch
                {
                }
            }

            return ResolveTexture(TextureAssets.Tile, layer.ContentId);
        }

        private static Texture2D ResolveWallTexture(BlueprintProjectionCellSnapshot layer)
        {
            if (layer == null || layer.ContentId < 0)
            {
                return null;
            }

            var paint = Math.Max(0, layer.PaintId);
            if (paint > 0)
            {
                try
                {
                    if (Main.instance != null && Main.instance.TilePaintSystem != null)
                    {
                        var painted = Main.instance.TilePaintSystem.TryGetWallAndRequestIfNotReady(layer.ContentId, paint);
                        if (painted != null)
                        {
                            return painted;
                        }
                    }
                }
                catch
                {
                }
            }

            return ResolveTexture(TextureAssets.Wall, layer.ContentId);
        }

        private static Texture2D ResolveTexture(ReLogic.Content.Asset<Texture2D>[] assets, int contentId)
        {
            if (assets == null || contentId < 0 || contentId >= assets.Length)
            {
                return null;
            }

            return ResolveTexture(assets[contentId]);
        }

        private static Texture2D ResolveTexture(ReLogic.Content.Asset<Texture2D> asset)
        {
            if (asset == null)
            {
                return null;
            }

            try
            {
                return asset.Value;
            }
            catch
            {
                return null;
            }
        }

        private static int ResolveCoatingAlpha(BlueprintProjectionCellSnapshot layer, int alpha)
        {
            if (layer == null)
            {
                return alpha;
            }

            if (HasCoatingFlag(layer.CoatingFlags, BlueprintCaptureCoatingFlags.Invisible))
            {
                alpha = Math.Min(alpha, 96);
            }

            if (HasCoatingFlag(layer.CoatingFlags, BlueprintCaptureCoatingFlags.Fullbright))
            {
                alpha = Math.Min(255, alpha + 32);
            }

            return alpha;
        }

        private static bool HasCoatingFlag(int flags, int flag)
        {
            return (flags & flag) == flag;
        }
    }
}
