using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class UiPrimitiveRenderer
    {
        private static readonly object SyncRoot = new object();
        private static bool _ready;
        private static DateTime _nextRetryUtc = DateTime.MinValue;
        private static Texture2D _magicPixel;
        private static string _lastMessage = "UI primitive renderer has not initialized.";
        private static readonly Dictionary<object, TextureAlphaCacheEntry> TextureAlphaCache = new Dictionary<object, TextureAlphaCacheEntry>();

        public static bool Ready
        {
            get { lock (SyncRoot) { return _ready; } }
        }

        public static string LastError
        {
            get { lock (SyncRoot) { return _lastMessage; } }
        }

        public static bool EnsureReady(object spriteBatch)
        {
            if (spriteBatch == null)
            {
                return Fail("SpriteBatch is unavailable.");
            }

            if (!(spriteBatch is SpriteBatch))
            {
                return Fail("SpriteBatch type is unavailable.");
            }

            lock (SyncRoot)
            {
                if (_ready)
                {
                    string staleReason;
                    if (!IsTextureUsable(_magicPixel, out staleReason))
                    {
                        ResetLocked("MagicPixel texture became unavailable: " + staleReason);
                        return false;
                    }

                    return true;
                }

                if (DateTime.UtcNow < _nextRetryUtc)
                {
                    return false;
                }

                try
                {
                    _magicPixel = ResolveMagicPixel();
                    if (_magicPixel == null)
                    {
                        return FailLocked("MagicPixel texture was not found.");
                    }

                    _ready = true;
                    _lastMessage = "UI primitive renderer ready.";
                    return true;
                }
                catch (Exception error)
                {
                    return FailLocked("UI primitive renderer init failed: " + error.Message);
                }
            }
        }

        public static void InvalidateCachedResources(string reason)
        {
            lock (SyncRoot)
            {
                ResetLocked(reason ?? "UI primitive renderer resources invalidated.");
            }
        }

        public static bool DrawFilledRect(object spriteBatch, int x, int y, int width, int height, int r, int g, int b, int a)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            try
            {
                var batch = (SpriteBatch)spriteBatch;
                batch.Draw(_magicPixel, new Rectangle(x, y, width, height), CreateColor(r, g, b, a));
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawFilledRect failed: " + error.Message);
            }
        }

        public static bool DrawFilledRectClipped(object spriteBatch, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            int ix;
            int iy;
            int iw;
            int ih;
            if (!TryIntersect(x, y, width, height, clipX, clipY, clipWidth, clipHeight, out ix, out iy, out iw, out ih))
            {
                return false;
            }

            return DrawFilledRect(spriteBatch, ix, iy, iw, ih, r, g, b, a);
        }

        public static bool DrawTextureRect(object spriteBatch, object texture, int x, int y, int width, int height, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            try
            {
                var batch = (SpriteBatch)spriteBatch;
                var texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    return Fail("DrawTextureRect failed: Texture2D type is unavailable.");
                }

                batch.Draw(texture2D, new Rectangle(x, y, width, height), CreateColor(r, g, b, a));
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawTextureRect failed: " + error.Message);
            }
        }

        public static bool DrawTextureRectClipped(object spriteBatch, object texture, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            int ix;
            int iy;
            int iw;
            int ih;
            if (texture == null || !TryIntersect(x, y, width, height, clipX, clipY, clipWidth, clipHeight, out ix, out iy, out iw, out ih))
            {
                return false;
            }

            if (ix == x && iy == y && iw == width && ih == height)
            {
                return DrawTextureRect(spriteBatch, texture, x, y, width, height, r, g, b, a);
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            try
            {
                var textureWidth = ReadTextureDimension(texture, "Width", width);
                var textureHeight = ReadTextureDimension(texture, "Height", height);
                var sx = (int)Math.Round((ix - x) * (textureWidth / (double)Math.Max(1, width)));
                var sy = (int)Math.Round((iy - y) * (textureHeight / (double)Math.Max(1, height)));
                var sw = Math.Max(1, (int)Math.Round(iw * (textureWidth / (double)Math.Max(1, width))));
                var sh = Math.Max(1, (int)Math.Round(ih * (textureHeight / (double)Math.Max(1, height))));
                var batch = (SpriteBatch)spriteBatch;
                var texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    return Fail("DrawTextureRectClipped failed: Texture2D type is unavailable.");
                }

                batch.Draw(
                    texture2D,
                    new Rectangle(ix, iy, iw, ih),
                    new Rectangle(sx, sy, sw, sh),
                    CreateColor(r, g, b, a));
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawTextureRectClipped failed: " + error.Message);
            }
        }

        public static bool DrawTextureContained(object spriteBatch, object texture, int x, int y, int width, int height, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            int drawX;
            int drawY;
            int drawWidth;
            int drawHeight;
            CalculateContainedRect(texture, x, y, width, height, out drawX, out drawY, out drawWidth, out drawHeight);
            return DrawTextureRect(spriteBatch, texture, drawX, drawY, drawWidth, drawHeight, r, g, b, a);
        }

        public static bool DrawTextureContainedClipped(object spriteBatch, object texture, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            int drawX;
            int drawY;
            int drawWidth;
            int drawHeight;
            CalculateContainedRect(texture, x, y, width, height, out drawX, out drawY, out drawWidth, out drawHeight);
            return DrawTextureRectClipped(spriteBatch, texture, drawX, drawY, drawWidth, drawHeight, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
        }

        public static bool DrawTextureTiled(object spriteBatch, object texture, int x, int y, int width, int height, int r, int g, int b, int a)
        {
            return DrawTextureTiledClipped(spriteBatch, texture, x, y, width, height, x, y, width, height, r, g, b, a);
        }

        public static bool DrawTextureTiledClipped(object spriteBatch, object texture, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            int textureWidth;
            int textureHeight;
            if (!TryReadTextureDimensions(texture, out textureWidth, out textureHeight) || textureWidth <= 0 || textureHeight <= 0)
            {
                return DrawTextureRectClipped(spriteBatch, texture, x, y, width, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var ok = false;
            var tileWidth = Math.Max(1, textureWidth);
            var tileHeight = Math.Max(1, textureHeight);
            for (var tileY = y; tileY < y + height; tileY += tileHeight)
            {
                var destHeight = Math.Min(tileHeight, y + height - tileY);
                for (var tileX = x; tileX < x + width; tileX += tileWidth)
                {
                    var destWidth = Math.Min(tileWidth, x + width - tileX);
                    ok |= DrawTextureSourceRectClipped(
                        spriteBatch,
                        texture,
                        tileX,
                        tileY,
                        destWidth,
                        destHeight,
                        0,
                        0,
                        destWidth,
                        destHeight,
                        clipX,
                        clipY,
                        clipWidth,
                        clipHeight,
                        r,
                        g,
                        b,
                        a);
                }
            }

            return ok;
        }

        public static bool DrawTextureNineSliceOrTiled(object spriteBatch, object texture, int x, int y, int width, int height, int r, int g, int b, int a)
        {
            return DrawTextureNineSliceOrTiledClipped(spriteBatch, texture, x, y, width, height, x, y, width, height, r, g, b, a);
        }

        public static bool DrawTextureNineSliceOrTiledClipped(object spriteBatch, object texture, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            int textureWidth;
            int textureHeight;
            if (!TryReadTextureDimensions(texture, out textureWidth, out textureHeight) ||
                textureWidth < 12 ||
                textureHeight < 12)
            {
                return DrawTextureTiledClipped(spriteBatch, texture, x, y, width, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var border = Math.Max(3, Math.Min(12, Math.Min(textureWidth, textureHeight) / 3));
            border = Math.Min(border, Math.Min(width, height) / 2);
            if (border <= 0)
            {
                return DrawTextureTiledClipped(spriteBatch, texture, x, y, width, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var centerSourceWidth = Math.Max(1, textureWidth - border * 2);
            var centerSourceHeight = Math.Max(1, textureHeight - border * 2);
            var centerDestWidth = Math.Max(1, width - border * 2);
            var centerDestHeight = Math.Max(1, height - border * 2);
            var ok = false;

            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x, y, border, border, 0, 0, border, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + border, y, centerDestWidth, border, border, 0, centerSourceWidth, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + width - border, y, border, border, textureWidth - border, 0, border, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);

            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x, y + border, border, centerDestHeight, 0, border, border, centerSourceHeight, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + border, y + border, centerDestWidth, centerDestHeight, border, border, centerSourceWidth, centerSourceHeight, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + width - border, y + border, border, centerDestHeight, textureWidth - border, border, border, centerSourceHeight, clipX, clipY, clipWidth, clipHeight, r, g, b, a);

            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x, y + height - border, border, border, 0, textureHeight - border, border, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + border, y + height - border, centerDestWidth, border, border, textureHeight - border, centerSourceWidth, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawTextureSourceRectClipped(spriteBatch, texture, x + width - border, y + height - border, border, border, textureWidth - border, textureHeight - border, border, border, clipX, clipY, clipWidth, clipHeight, r, g, b, a);

            return ok || DrawTextureTiledClipped(spriteBatch, texture, x, y, width, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
        }

        public static bool TryReadTextureDimensions(object texture, out int width, out int height)
        {
            width = ReadTextureDimension(texture, "Width", 0);
            height = ReadTextureDimension(texture, "Height", 0);
            return width > 0 && height > 0;
        }

        public static bool DrawTextureSourceRectRotated(
            object spriteBatch,
            object texture,
            float x,
            float y,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            int r,
            int g,
            int b,
            int a,
            float rotation,
            float originX,
            float originY,
            float scaleX,
            float scaleY)
        {
            if (texture == null || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            try
            {
                var batch = (SpriteBatch)spriteBatch;
                var texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    return Fail("DrawTextureSourceRectRotated failed: Texture2D type is unavailable.");
                }

                batch.Draw(
                    texture2D,
                    new Vector2(x, y),
                    new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                    CreateColor(r, g, b, a),
                    rotation,
                    new Vector2(originX, originY),
                    new Vector2(scaleX, scaleY),
                    SpriteEffects.None,
                    0f);
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawTextureSourceRectRotated failed: " + error.Message);
            }
        }

        public static bool DrawRectBorder(object spriteBatch, int x, int y, int width, int height, int thickness, int r, int g, int b, int a)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var line = Math.Max(1, thickness);
            var ok = DrawFilledRect(spriteBatch, x, y, width, line, r, g, b, a);
            ok &= DrawFilledRect(spriteBatch, x, y + height - line, width, line, r, g, b, a);
            ok &= DrawFilledRect(spriteBatch, x, y, line, height, r, g, b, a);
            ok &= DrawFilledRect(spriteBatch, x + width - line, y, line, height, r, g, b, a);
            return ok;
        }

        public static bool DrawRectBorderClipped(object spriteBatch, int x, int y, int width, int height, int thickness, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var line = Math.Max(1, thickness);
            var ok = DrawFilledRectClipped(spriteBatch, x, y, width, line, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawFilledRectClipped(spriteBatch, x, y + height - line, width, line, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawFilledRectClipped(spriteBatch, x, y, line, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            ok |= DrawFilledRectClipped(spriteBatch, x + width - line, y, line, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            return ok;
        }

        public static bool DrawRoundedRect(object spriteBatch, int x, int y, int width, int height, int radius, int r, int g, int b, int a)
        {
            return DrawRoundedRectClipped(spriteBatch, x, y, width, height, int.MinValue / 2, int.MinValue / 2, int.MaxValue, int.MaxValue, radius, r, g, b, a);
        }

        public static bool DrawRoundedRectBorderClipped(object spriteBatch, int x, int y, int width, int height, int radius, int thickness, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            var line = Math.Max(1, Math.Min(thickness, Math.Max(1, Math.Min(width, height) / 2)));
            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            if (radius <= 1)
            {
                return DrawRectBorderClipped(spriteBatch, x, y, width, height, line, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var innerWidth = width - line * 2;
            var innerHeight = height - line * 2;
            if (innerWidth <= 0 || innerHeight <= 0)
            {
                return DrawRoundedRectClipped(spriteBatch, x, y, width, height, clipX, clipY, clipWidth, clipHeight, radius, r, g, b, a);
            }

            var innerRadius = Math.Max(0, Math.Min(radius - line, Math.Min(innerWidth, innerHeight) / 2));
            var ok = false;
            for (var row = 0; row < height; row++)
            {
                var outerInset = RoundedInsetForRow(row, height, radius);
                var outerLeft = x + outerInset;
                var outerRight = x + width - outerInset;
                if (outerRight <= outerLeft)
                {
                    continue;
                }

                if (row < line || row >= height - line)
                {
                    ok |= DrawFilledRectClipped(spriteBatch, outerLeft, y + row, outerRight - outerLeft, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                    continue;
                }

                var innerRow = row - line;
                var innerInset = RoundedInsetForRow(innerRow, innerHeight, innerRadius);
                var innerLeft = Math.Max(outerLeft, x + line + innerInset);
                var innerRight = Math.Min(outerRight, x + line + innerWidth - innerInset);
                if (innerRight <= innerLeft)
                {
                    ok |= DrawFilledRectClipped(spriteBatch, outerLeft, y + row, outerRight - outerLeft, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                    continue;
                }

                if (innerLeft > outerLeft)
                {
                    ok |= DrawFilledRectClipped(spriteBatch, outerLeft, y + row, innerLeft - outerLeft, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                }

                if (innerRight < outerRight)
                {
                    ok |= DrawFilledRectClipped(spriteBatch, innerRight, y + row, outerRight - innerRight, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                }
            }

            return ok;
        }

        public static bool DrawTextureShapeBorderClipped(object spriteBatch, object texture, int x, int y, int width, int height, int inset, int thickness, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            if (texture == null || width <= 0 || height <= 0 || thickness <= 0)
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            int textureWidth;
            int textureHeight;
            if (!TryReadTextureDimensions(texture, out textureWidth, out textureHeight) ||
                textureWidth <= 0 ||
                textureHeight <= 0)
            {
                return false;
            }

            TextureAlphaCacheEntry alphaCache;
            if (!TryGetTextureAlphaCache(texture, textureWidth, textureHeight, out alphaCache))
            {
                return false;
            }

            var safeInset = Math.Max(0, Math.Min(inset, Math.Max(0, Math.Min(width, height) / 2 - 1)));
            var line = Math.Max(1, Math.Min(thickness, Math.Max(1, Math.Min(width, height) / 2 - safeInset)));
            var left = x + safeInset;
            var top = y + safeInset;
            var right = x + width - safeInset;
            var bottom = y + height - safeInset;
            int ix;
            int iy;
            int iw;
            int ih;
            if (!TryIntersect(left, top, right - left, bottom - top, clipX, clipY, clipWidth, clipHeight, out ix, out iy, out iw, out ih))
            {
                return false;
            }

            var slice = Math.Max(3, Math.Min(12, Math.Min(textureWidth, textureHeight) / 3));
            slice = Math.Min(slice, Math.Min(width, height) / 2);
            var ok = false;
            for (var py = iy; py < iy + ih; py++)
            {
                var localY = py - y;
                var inHorizontalBand = localY < safeInset + line || localY >= height - safeInset - line;
                var runStart = -1;
                for (var px = ix; px < ix + iw; px++)
                {
                    var localX = px - x;
                    var inVerticalBand = localX < safeInset + line || localX >= width - safeInset - line;
                    var draw = false;
                    if (inHorizontalBand || inVerticalBand)
                    {
                        var sourceX = MapNineSliceLocalToSource(localX, width, textureWidth, slice);
                        var sourceY = MapNineSliceLocalToSource(localY, height, textureHeight, slice);
                        var alpha = alphaCache.Alpha[sourceY * textureWidth + sourceX];
                        draw = alpha > 16;
                    }

                    if (draw)
                    {
                        if (runStart < 0)
                        {
                            runStart = px;
                        }
                    }
                    else if (runStart >= 0)
                    {
                        ok |= DrawFilledRectClipped(spriteBatch, runStart, py, px - runStart, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                        runStart = -1;
                    }
                }

                if (runStart >= 0)
                {
                    ok |= DrawFilledRectClipped(spriteBatch, runStart, py, ix + iw - runStart, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                }
            }

            return ok;
        }

        public static bool DrawRoundedRectClipped(object spriteBatch, int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, int radius, int r, int g, int b, int a)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            if (radius <= 1)
            {
                return DrawFilledRectClipped(spriteBatch, x, y, width, height, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var ok = false;
            for (var row = 0; row < radius; row++)
            {
                var inset = RoundedInset(row, radius);
                ok |= DrawFilledRectClipped(spriteBatch, x + inset, y + row, width - inset * 2, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
                ok |= DrawFilledRectClipped(spriteBatch, x + inset, y + height - row - 1, width - inset * 2, 1, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            var middleY = y + radius;
            var middleHeight = height - radius * 2;
            if (middleHeight > 0)
            {
                ok |= DrawFilledRectClipped(spriteBatch, x, middleY, width, middleHeight, clipX, clipY, clipWidth, clipHeight, r, g, b, a);
            }

            return ok;
        }

        private static Rectangle CreateRectangle(int x, int y, int width, int height)
        {
            return new Rectangle(x, y, width, height);
        }

        private static bool DrawTextureSourceRectClipped(object spriteBatch, object texture, int destX, int destY, int destWidth, int destHeight, int sourceX, int sourceY, int sourceWidth, int sourceHeight, int clipX, int clipY, int clipWidth, int clipHeight, int r, int g, int b, int a)
        {
            int ix;
            int iy;
            int iw;
            int ih;
            if (texture == null || destWidth <= 0 || destHeight <= 0 ||
                !TryIntersect(destX, destY, destWidth, destHeight, clipX, clipY, clipWidth, clipHeight, out ix, out iy, out iw, out ih))
            {
                return false;
            }

            if (!EnsureReady(spriteBatch))
            {
                return false;
            }

            try
            {
                var sx = sourceX + (int)Math.Round((ix - destX) * (sourceWidth / (double)Math.Max(1, destWidth)));
                var sy = sourceY + (int)Math.Round((iy - destY) * (sourceHeight / (double)Math.Max(1, destHeight)));
                var sw = Math.Max(1, (int)Math.Round(iw * (sourceWidth / (double)Math.Max(1, destWidth))));
                var sh = Math.Max(1, (int)Math.Round(ih * (sourceHeight / (double)Math.Max(1, destHeight))));
                var batch = (SpriteBatch)spriteBatch;
                var texture2D = texture as Texture2D;
                if (texture2D == null)
                {
                    return Fail("DrawTextureSourceRectClipped failed: Texture2D type is unavailable.");
                }

                batch.Draw(
                    texture2D,
                    new Rectangle(ix, iy, iw, ih),
                    new Rectangle(sx, sy, sw, sh),
                    CreateColor(r, g, b, a));
                return true;
            }
            catch (Exception error)
            {
                return Fail("DrawTextureSourceRectClipped failed: " + error.Message);
            }
        }

        private static object CreateSourceRectangleArgument(MethodInfo drawMethod, object source)
        {
            var parameters = drawMethod.GetParameters();
            return parameters[2].ParameterType.IsGenericType
                ? Activator.CreateInstance(parameters[2].ParameterType, source)
                : source;
        }

        private static Vector2 CreateVector2(float x, float y)
        {
            return new Vector2(x, y);
        }

        private static void CalculateContainedRect(object texture, int x, int y, int width, int height, out int drawX, out int drawY, out int drawWidth, out int drawHeight)
        {
            var textureWidth = ReadTextureDimension(texture, "Width", width);
            var textureHeight = ReadTextureDimension(texture, "Height", height);
            if (textureWidth <= 0 || textureHeight <= 0)
            {
                textureWidth = width;
                textureHeight = height;
            }

            var scale = Math.Min(width / (double)Math.Max(1, textureWidth), height / (double)Math.Max(1, textureHeight));
            drawWidth = Math.Max(1, (int)Math.Round(textureWidth * scale));
            drawHeight = Math.Max(1, (int)Math.Round(textureHeight * scale));
            drawX = x + (width - drawWidth) / 2;
            drawY = y + (height - drawHeight) / 2;
        }

        private static Color CreateColor(int r, int g, int b, int a)
        {
            return new Color(ClampColorComponent(r), ClampColorComponent(g), ClampColorComponent(b), ClampColorComponent(a));
        }

        private static int ClampColorComponent(int value)
        {
            return Math.Max(0, Math.Min(255, value));
        }

        private static MethodInfo FindSpriteBatchDraw(Type spriteBatchType, object texture, Type rectangleType, Type colorType)
        {
            var methods = spriteBatchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "Draw", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 3)
                {
                    continue;
                }

                if (!parameters[0].ParameterType.IsInstanceOfType(texture) &&
                    !parameters[0].ParameterType.IsAssignableFrom(texture.GetType()))
                {
                    continue;
                }

                if (parameters[1].ParameterType.FullName != rectangleType.FullName ||
                    parameters[2].ParameterType.FullName != colorType.FullName)
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static MethodInfo FindSpriteBatchDrawWithSource(Type spriteBatchType, object texture, Type rectangleType, Type colorType)
        {
            var methods = spriteBatchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "Draw", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 4)
                {
                    continue;
                }

                if (!parameters[0].ParameterType.IsInstanceOfType(texture) &&
                    !parameters[0].ParameterType.IsAssignableFrom(texture.GetType()))
                {
                    continue;
                }

                if (parameters[1].ParameterType.FullName != rectangleType.FullName ||
                    parameters[3].ParameterType.FullName != colorType.FullName)
                {
                    continue;
                }

                var sourceType = parameters[2].ParameterType;
                if (sourceType.FullName == rectangleType.FullName ||
                    (sourceType.IsGenericType &&
                     sourceType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                     sourceType.GetGenericArguments()[0].FullName == rectangleType.FullName))
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindSpriteBatchDrawVectorWithSource(Type spriteBatchType, object texture, Type rectangleType, Type colorType, Type vector2Type, out object spriteEffectsNone)
        {
            spriteEffectsNone = null;
            var methods = spriteBatchType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (var index = 0; index < methods.Length; index++)
            {
                var method = methods[index];
                if (!string.Equals(method.Name, "Draw", StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 9)
                {
                    continue;
                }

                if (!parameters[0].ParameterType.IsInstanceOfType(texture) &&
                    !parameters[0].ParameterType.IsAssignableFrom(texture.GetType()))
                {
                    continue;
                }

                if (parameters[1].ParameterType.FullName != vector2Type.FullName ||
                    parameters[3].ParameterType.FullName != colorType.FullName ||
                    parameters[5].ParameterType.FullName != vector2Type.FullName ||
                    parameters[6].ParameterType.FullName != vector2Type.FullName)
                {
                    continue;
                }

                var sourceType = parameters[2].ParameterType;
                if (sourceType.FullName != rectangleType.FullName &&
                    (!sourceType.IsGenericType ||
                     sourceType.GetGenericTypeDefinition() != typeof(Nullable<>) ||
                     sourceType.GetGenericArguments()[0].FullName != rectangleType.FullName))
                {
                    continue;
                }

                if (!parameters[7].ParameterType.IsEnum)
                {
                    continue;
                }

                try
                {
                    spriteEffectsNone = Enum.Parse(parameters[7].ParameterType, "None");
                }
                catch
                {
                    spriteEffectsNone = Activator.CreateInstance(parameters[7].ParameterType);
                }

                return method;
            }

            return null;
        }

        private static ConstructorInfo FindColorConstructor(Type colorType)
        {
            var constructors = colorType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            for (var index = 0; index < constructors.Length; index++)
            {
                var ctor = constructors[index];
                var parameters = ctor.GetParameters();
                if (parameters.Length != 4)
                {
                    continue;
                }

                if (IsColorComponent(parameters[0].ParameterType) &&
                    IsColorComponent(parameters[1].ParameterType) &&
                    IsColorComponent(parameters[2].ParameterType) &&
                    IsColorComponent(parameters[3].ParameterType))
                {
                    return ctor;
                }
            }

            return null;
        }

        private static bool IsColorComponent(Type type)
        {
            return type == typeof(byte) ||
                   type == typeof(int) ||
                   type == typeof(float) ||
                   type == typeof(double);
        }

        private static Texture2D ResolveMagicPixel()
        {
            try
            {
                return TextureAssets.MagicPixel.Value;
            }
            catch (Exception error)
            {
                _lastMessage = "MagicPixel asset value unavailable: " + error.Message;
                return null;
            }
        }

        private static object TryGetStaticMember(Type type, string name)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            try
            {
                var field = type.GetField(name, flags);
                if (field != null)
                {
                    return field.GetValue(null);
                }

                var property = type.GetProperty(name, flags);
                if (property != null && property.CanRead)
                {
                    return property.GetValue(null, null);
                }
            }
            catch (Exception error)
            {
                _lastMessage = "Read static member " + name + " failed: " + error.Message;
            }

            return null;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static int ReadTextureDimension(object texture, string name, int fallback)
        {
            var texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                return fallback;
            }

            return string.Equals(name, "Width", StringComparison.Ordinal)
                ? texture2D.Width
                : string.Equals(name, "Height", StringComparison.Ordinal)
                    ? texture2D.Height
                    : fallback;
        }

        private static bool TryIntersect(int x, int y, int width, int height, int clipX, int clipY, int clipWidth, int clipHeight, out int ix, out int iy, out int iw, out int ih)
        {
            var right = x + width;
            var bottom = y + height;
            var clipRight = clipX + clipWidth;
            var clipBottom = clipY + clipHeight;
            ix = Math.Max(x, clipX);
            iy = Math.Max(y, clipY);
            var ir = Math.Min(right, clipRight);
            var ib = Math.Min(bottom, clipBottom);
            iw = ir - ix;
            ih = ib - iy;
            return iw > 0 && ih > 0;
        }

        private static bool IsTextureUsable(object texture, out string reason)
        {
            reason = string.Empty;
            var texture2D = texture as Texture2D;
            if (texture2D == null)
            {
                reason = "texture is null";
                return false;
            }

            try
            {
                if (texture2D.IsDisposed)
                {
                    reason = "texture is disposed";
                    return false;
                }

                if (texture2D.Width <= 0 || texture2D.Height <= 0)
                {
                    reason = "texture dimensions are unavailable";
                    return false;
                }

                return true;
            }
            catch (Exception error)
            {
                reason = error.Message;
                return false;
            }
        }

        private static int RoundedInset(int row, int radius)
        {
            var dy = radius - row - 1;
            var inside = Math.Max(0, radius * radius - dy * dy);
            return Math.Max(0, radius - (int)Math.Round(Math.Sqrt(inside)));
        }

        private static int RoundedInsetForRow(int row, int height, int radius)
        {
            if (radius <= 1 || row < 0 || row >= height)
            {
                return 0;
            }

            if (row < radius)
            {
                return RoundedInset(row, radius);
            }

            if (row >= height - radius)
            {
                return RoundedInset(height - row - 1, radius);
            }

            return 0;
        }

        private static int MapNineSliceLocalToSource(int local, int destSize, int sourceSize, int border)
        {
            if (destSize <= 1 || sourceSize <= 1)
            {
                return 0;
            }

            var safeLocal = Math.Max(0, Math.Min(destSize - 1, local));
            var safeBorder = Math.Max(0, Math.Min(border, Math.Min(destSize, sourceSize) / 2));
            if (safeBorder <= 0 || destSize <= safeBorder * 2 || sourceSize <= safeBorder * 2)
            {
                return Math.Max(0, Math.Min(sourceSize - 1, (int)Math.Round(safeLocal * ((sourceSize - 1) / (double)(destSize - 1)))));
            }

            if (safeLocal < safeBorder)
            {
                return Math.Max(0, Math.Min(sourceSize - 1, safeLocal));
            }

            if (safeLocal >= destSize - safeBorder)
            {
                return Math.Max(0, Math.Min(sourceSize - 1, sourceSize - (destSize - safeLocal)));
            }

            var centerDest = Math.Max(1, destSize - safeBorder * 2);
            var centerSource = Math.Max(1, sourceSize - safeBorder * 2);
            var centerLocal = safeLocal - safeBorder;
            return Math.Max(0, Math.Min(sourceSize - 1, safeBorder + (int)Math.Floor(centerLocal * (centerSource / (double)centerDest))));
        }

        private static bool TryGetTextureAlphaCache(object texture, int width, int height, out TextureAlphaCacheEntry entry)
        {
            entry = null;
            var texture2D = texture as Texture2D;
            if (texture2D == null || width <= 0 || height <= 0)
            {
                return false;
            }

            lock (SyncRoot)
            {
                TextureAlphaCacheEntry cached;
                if (TextureAlphaCache.TryGetValue(texture, out cached) &&
                    cached.Width == width &&
                    cached.Height == height &&
                    cached.Alpha != null &&
                    cached.Alpha.Length == width * height)
                {
                    entry = cached;
                    return true;
                }

                Color[] pixels;
                if (!TryGetTexturePixels(texture2D, width * height, out pixels))
                {
                    return false;
                }

                var alpha = new byte[width * height];
                for (var index = 0; index < alpha.Length; index++)
                {
                    alpha[index] = pixels[index].A;
                }

                entry = new TextureAlphaCacheEntry
                {
                    Width = width,
                    Height = height,
                    Alpha = alpha
                };
                TextureAlphaCache[texture] = entry;
                return true;
            }
        }

        private static bool TryGetTexturePixels(object texture, int pixelCount, out Color[] pixels)
        {
            pixels = null;
            var texture2D = texture as Texture2D;
            if (texture2D == null || pixelCount <= 0)
            {
                return false;
            }

            try
            {
                var data = new Color[pixelCount];
                texture2D.GetData(data);
                pixels = data;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool Fail(string message)
        {
            lock (SyncRoot)
            {
                return FailLocked(message);
            }
        }

        private static bool FailLocked(string message)
        {
            ResetLocked(message ?? "UI primitive renderer unavailable.");
            LogThrottle.WarnThrottled(
                "ui-primitive-renderer-unavailable",
                TimeSpan.FromSeconds(10),
                "UiPrimitiveRenderer",
                _lastMessage);
            return false;
        }

        private static void ResetLocked(string message)
        {
            _ready = false;
            _magicPixel = null;
            TextureAlphaCache.Clear();
            _lastMessage = message ?? "UI primitive renderer unavailable.";
            _nextRetryUtc = DateTime.UtcNow.AddSeconds(1);
        }

        private sealed class TextureAlphaCacheEntry
        {
            public int Width;
            public int Height;
            public byte[] Alpha;
        }
    }
}
