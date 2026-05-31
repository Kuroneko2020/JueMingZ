using System;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public static class FishingStatusPromptOverlay
    {
        public static bool DrawInterfaceLayer()
        {
            try
            {
                string text;
                bool startPrompt;
                double progress;
                double alpha;
                if (!FishingStatusPromptService.TryGetPrompt(out text, out startPrompt, out progress, out alpha))
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("FishingStatusPromptOverlay", true, out spriteBatch))
                {
                    return true;
                }

                DrawPrompt(spriteBatch, text, startPrompt, progress, alpha);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("FishingStatusPromptOverlay", error);
                LogThrottle.ErrorThrottled(
                    "fishing-status-prompt-draw-failed",
                    TimeSpan.FromSeconds(10),
                    "FishingStatusPromptOverlay",
                    "Fishing status prompt draw failed; exception swallowed.", error);
            }

            return true;
        }

        private static void DrawPrompt(object spriteBatch, string text, bool startPrompt, double progress, double alpha)
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
            if (!InformationReflection.TryReadVectorMember(player, "Top", out topX, out topY))
            {
                if (!InformationReflection.TryReadVectorMember(player, "Center", out topX, out topY))
                {
                    return;
                }

                int height;
                if (InformationReflection.TryReadInt(player, "height", out height))
                {
                    topY -= Math.Max(0, height) * 0.5f;
                }
            }

            var scale = 1.12f;
            var textWidth = UiTextRenderer.EstimateTextWidth(text, scale);
            var drawX = (float)Math.Round(topX - screenX - textWidth * 0.5f);
            var drawY = (float)Math.Round(topY - screenY - 42f - progress * 20d);
            var a = Math.Max(0, Math.Min(255, (int)Math.Round(255d * alpha)));
            if (a <= 0)
            {
                return;
            }

            UiTextRenderer.DrawText(spriteBatch, text, drawX + 2f, drawY + 2f, 16, 18, 24, Math.Min(210, a), scale);
            if (startPrompt)
            {
                UiTextRenderer.DrawText(spriteBatch, text, drawX, drawY, 255, 238, 95, a, scale);
            }
            else
            {
                UiTextRenderer.DrawText(spriteBatch, text, drawX, drawY, 255, 118, 86, a, scale);
            }
        }
    }
}
