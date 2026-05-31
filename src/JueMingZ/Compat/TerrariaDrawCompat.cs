using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class TerrariaDrawCompat
    {
        public static SpriteBatch SpriteBatch
        {
            get { return Main.spriteBatch; }
        }

        public static Vector2 ScreenPosition
        {
            get { return Main.screenPosition; }
        }

        public static Rectangle ScreenRectangle
        {
            get { return new Rectangle(0, 0, Main.screenWidth, Main.screenHeight); }
        }

        public static bool DrawToScreen
        {
            get { return Main.drawToScreen; }
        }

        public static bool TryGetSpriteBatch(out SpriteBatch spriteBatch)
        {
            spriteBatch = Main.spriteBatch;
            return spriteBatch != null && !spriteBatch.IsDisposed;
        }

        public static Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return worldPosition - Main.screenPosition;
        }

        public static Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return screenPosition + Main.screenPosition;
        }

        public static bool IsTextureReady(Texture2D texture)
        {
            return texture != null && !texture.IsDisposed && texture.Width > 0 && texture.Height > 0;
        }

        public static bool TryMeasureString(DynamicSpriteFont font, string text, out Vector2 size)
        {
            size = Vector2.Zero;
            if (font == null)
            {
                return false;
            }

            size = font.MeasureString(text ?? string.Empty);
            return true;
        }
    }
}
