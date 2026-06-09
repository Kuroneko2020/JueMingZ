using System;
using JueMingZ.Automation.Combat;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class CombatAimMarkerOverlay
    {
        public static bool DrawInterfaceLayer()
        {
            try
            {
                var selection = CombatAutoAimService.CurrentSelection;
                long tick;
                if (TerrariaInputCompat.TryReadGameUpdateCount(out tick))
                {
                    CombatAimTargetSelection cachedSelection;
                    if (CombatAimDecisionCache.TryGetRecentMarkerSelection(tick, out cachedSelection))
                    {
                        selection = cachedSelection;
                    }
                }

                if (selection == null || !selection.MarkerEnabled || selection.Target == null)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("CombatAimMarkerOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawMarker(spriteBatch, selection);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("CombatAimMarkerOverlay", error);
                LogThrottle.ErrorThrottled(
                    "combat-aim-marker-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatAimMarkerOverlay",
                    "Combat aim marker draw failed; exception swallowed.", error);
            }

            return true;
        }

        private static void DrawMarker(object spriteBatch, CombatAimTargetSelection selection)
        {
            var target = selection.Target;
            CombatTargetSnapshot freshTarget;
            if (!CombatAimTargetReader.TryReadTargetByIdentity(
                    target.WhoAmI,
                    target.Type,
                    selection.TrackDummy,
                    out freshTarget,
                    out var refreshSkipReason))
            {
                CombatAimTargetLockService.Clear();
                selection.Target = null;
                selection.ResultCode = "TargetInvalid";
                selection.SkipReason = string.IsNullOrWhiteSpace(refreshSkipReason) ? "markerTargetInvalid" : refreshSkipReason;
                return;
            }

            target = freshTarget;

            float screenX;
            float screenY;
            int screenWidth;
            int screenHeight;
            if (!CombatAimTargetReader.TryReadScreenState(out screenX, out screenY, out screenWidth, out screenHeight))
            {
                screenX = selection.ScreenPositionX;
                screenY = selection.ScreenPositionY;
                screenWidth = selection.ScreenWidth;
                screenHeight = selection.ScreenHeight;
            }

            var centerX = (int)Math.Round(target.CenterX - screenX);
            var centerY = (int)Math.Round(target.CenterY - screenY);

            if (screenWidth > 0 && screenHeight > 0)
            {
                if (centerX < -96 || centerY < -96 || centerX > screenWidth + 96 || centerY > screenHeight + 96)
                {
                    return;
                }
            }

            selection.Target = target;
            selection.ScreenPositionX = screenX;
            selection.ScreenPositionY = screenY;
            selection.ScreenWidth = screenWidth;
            selection.ScreenHeight = screenHeight;

            object texture;
            if (VanillaUiSkinCompat.TryGetLockOnCursorTexture(out texture) &&
                DrawVanillaLockOnMarker(spriteBatch, texture, selection, centerX, centerY))
            {
                return;
            }

            DrawFallbackMarker(spriteBatch, selection, centerX, centerY);
        }

        private static bool DrawVanillaLockOnMarker(object spriteBatch, object texture, CombatAimTargetSelection selection, int centerX, int centerY)
        {
            int textureWidth;
            int textureHeight;
            if (!UiPrimitiveRenderer.TryReadTextureDimensions(texture, out textureWidth, out textureHeight) ||
                textureWidth <= 0 ||
                textureHeight < 28)
            {
                return false;
            }

            var target = selection.Target;
            var targetSpan = Math.Min(Math.Max(8, target.Width), Math.Max(8, target.Height)) + 20;
            var baseScale = 1f;
            if (targetSpan < 70)
            {
                baseScale *= targetSpan / 70f;
            }

            var pulse = 0.94f + (float)Math.Sin(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 2d) * 0.06f;
            var scaleX = 0.58f * baseScale * pulse;
            var scaleY = baseScale * pulse;
            var radius = Math.Max(24f, targetSpan / 2f);
            var rotationBase = (float)(DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 0.5d);
            var ok = false;

            for (var index = 0; index < 3; index++)
            {
                var rotation = (float)(Math.PI * 2d / 3d * index) + rotationBase;
                var markerX = centerX + (float)Math.Cos(rotation) * radius;
                var markerY = centerY + (float)Math.Sin(rotation) * radius;
                var drawRotation = rotation + (float)(Math.PI / 2d);

                ok |= UiPrimitiveRenderer.DrawTextureSourceRectRotated(
                    spriteBatch,
                    texture,
                    markerX,
                    markerY,
                    0,
                    0,
                    textureWidth,
                    12,
                    255,
                    238,
                    185,
                    220,
                    drawRotation,
                    textureWidth / 2f,
                    6f,
                    scaleX,
                    scaleY);

                ok |= UiPrimitiveRenderer.DrawTextureSourceRectRotated(
                    spriteBatch,
                    texture,
                    markerX,
                    markerY,
                    0,
                    16,
                    textureWidth,
                    12,
                    255,
                    255,
                    255,
                    190,
                    drawRotation,
                    textureWidth / 2f,
                    6f,
                    scaleX,
                    scaleY);
            }

            return ok;
        }

        private static void DrawFallbackMarker(object spriteBatch, CombatAimTargetSelection selection, int centerX, int centerY)
        {
            var target = selection.Target;
            var targetSpan = Math.Min(Math.Max(8, target.Width), Math.Max(8, target.Height)) + 20;
            var radius = Math.Max(18, targetSpan / 2);
            var rotationBase = DateTime.UtcNow.TimeOfDay.TotalSeconds * Math.PI * 0.5d;

            for (var index = 0; index < 3; index++)
            {
                var rotation = Math.PI * 2d / 3d * index + rotationBase;
                var x = centerX + (int)Math.Round(Math.Cos(rotation) * radius);
                var y = centerY + (int)Math.Round(Math.Sin(rotation) * radius);
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x - 6, y - 1, 13, 2, 255, 238, 148, 225);
                UiPrimitiveRenderer.DrawFilledRect(spriteBatch, x - 1, y - 6, 2, 13, 255, 238, 148, 225);
            }

            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, centerX - 5, centerY - 5, 11, 11, 1, 255, 248, 190, 180);
        }
    }
}
