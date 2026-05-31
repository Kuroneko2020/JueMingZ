using System;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class CombatEquipmentWarningPromptOverlay
    {
        private const float PromptVerticalOffset = 28f;

        public static bool DrawInterfaceLayer()
        {
            try
            {
                string text;
                double progress;
                double alpha;
                if (!CombatEquipmentWarningService.TryGetPrompt(out text, out progress, out alpha))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("CombatEquipmentWarningPromptOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawPrompt(spriteBatch, text, progress, alpha);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("CombatEquipmentWarningPromptOverlay", error);
                LogThrottle.ErrorThrottled(
                    "combat-equipment-warning-prompt-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "CombatEquipmentWarningPromptOverlay",
                    "Combat equipment warning prompt draw failed; exception swallowed.", error);
            }

            return true;
        }

        private static void DrawPrompt(object spriteBatch, string text, double progress, double alpha)
        {
            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return;
            }

            var mainType = TerrariaRuntimeTypes.MainType;
            float screenX;
            float screenY;
            InformationReflection.TryReadVector2(InformationReflection.GetStaticMember(mainType, "screenPosition"), out screenX, out screenY);

            float topX;
            float topY;
            if (!TryReadPlayerTop(player, out topX, out topY))
            {
                return;
            }

            const float scale = 0.96f;
            var textWidth = UiTextRenderer.EstimateTextWidth(text, scale);
            var drawX = (float)Math.Round(topX - screenX - textWidth * 0.5f);
            var drawY = CalculatePromptDrawY(topY, screenY, progress);
            var a = Math.Max(0, Math.Min(255, (int)Math.Round(255d * alpha)));
            if (a <= 0)
            {
                return;
            }

            UiTextRenderer.DrawText(spriteBatch, text, drawX + 2f, drawY + 2f, 16, 18, 24, Math.Min(210, a), scale);
            UiTextRenderer.DrawText(spriteBatch, text, drawX, drawY, 255, 216, 98, a, scale);
        }

        internal static float CalculatePromptDrawYForTesting(float topY, float screenY, double progress)
        {
            return CalculatePromptDrawY(topY, screenY, progress);
        }

        private static float CalculatePromptDrawY(float topY, float screenY, double progress)
        {
            return (float)Math.Round(topY - screenY - PromptVerticalOffset);
        }

        private static bool TryReadPlayerTop(object player, out float topX, out float topY)
        {
            int x;
            int y;
            int width;
            int height;
            if (InformationReflection.TryReadRectangle(InformationReflection.GetMember(player, "Hitbox"), out x, out y, out width, out height) &&
                width > 0 &&
                height > 0)
            {
                topX = x + width * 0.5f;
                topY = y;
                return true;
            }

            if (InformationReflection.TryReadVectorMember(player, "Top", out topX, out topY))
            {
                return true;
            }

            if (!InformationReflection.TryReadVectorMember(player, "Center", out topX, out topY))
            {
                topX = 0f;
                topY = 0f;
                return false;
            }

            int playerHeight;
            if (InformationReflection.TryReadInt(player, "height", out playerHeight))
            {
                topY -= Math.Max(0, playerHeight) * 0.5f;
            }

            return true;
        }
    }
}
