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
            try
            {
                var drawInformation = InformationOverlayService.ShouldDrawWorldOverlay();
                var drawAutoMining = AutoMiningService.ShouldDrawWorldOverlay();
                if (!drawInformation && !drawAutoMining)
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("InformationWorldOverlay", true, out spriteBatch))
                {
                    return true;
                }

                if (drawInformation)
                {
                    InformationOverlayService.DrawWorldOverlay(spriteBatch);
                }

                if (drawAutoMining)
                {
                    AutoMiningOverlayService.DrawWorldOverlay(spriteBatch);
                }
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("InformationWorldOverlay", error);
                LogThrottle.ErrorThrottled(
                    "information-world-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "InformationWorldOverlay",
                    "Information world overlay draw failed; exception swallowed.", error);
            }

            return true;
        }
    }
}
