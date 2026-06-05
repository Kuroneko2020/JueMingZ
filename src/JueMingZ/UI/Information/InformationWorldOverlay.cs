using System;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.UI.Information
{
    public static class InformationWorldOverlay
    {
        public static bool DrawInterfaceLayer()
        {
            var keepDrawing = true;
            keepDrawing &= DrawInformationInterfaceLayer();
            keepDrawing &= DrawAutoMiningInterfaceLayer();
            return keepDrawing;
        }

        public static bool DrawInformationInterfaceLayer()
        {
            try
            {
                if (!InformationOverlayService.ShouldDrawWorldOverlay())
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("InformationWorldOverlay.Information", true, out spriteBatch))
                {
                    return true;
                }

                InformationOverlayService.DrawWorldOverlay(spriteBatch);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("InformationWorldOverlay.Information", error);
                LogThrottle.ErrorThrottled(
                    "information-world-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "InformationWorldOverlay",
                    "Information world overlay draw failed; exception swallowed.", error);
            }

            return true;
        }

        public static bool DrawAutoMiningInterfaceLayer()
        {
            try
            {
                if (!AutoMiningService.ShouldDrawWorldOverlay())
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("InformationWorldOverlay.AutoMining", true, out spriteBatch))
                {
                    return true;
                }

                AutoMiningOverlayService.DrawWorldOverlay(spriteBatch);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("InformationWorldOverlay.AutoMining", error);
                LogThrottle.ErrorThrottled(
                    "auto-mining-world-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "InformationWorldOverlay",
                    "Auto mining world overlay draw failed; exception swallowed.", error);
            }

            return true;
        }
    }
}
