using System;
using JueMingZ.Automation.Information;
using JueMingZ.Diagnostics;
using JueMingZ.UI;

namespace JueMingZ.UI.Information
{
    public static class InformationStatusPanelOverlay
    {
        public static bool DrawInterfaceLayer()
        {
            try
            {
                if (!InformationOverlayService.ShouldDrawStatusPanel())
                {
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("InformationStatusPanelOverlay", true, out spriteBatch))
                {
                    return true;
                }

                InformationOverlayService.DrawStatusPanel(spriteBatch);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("InformationStatusPanelOverlay", error);
                LogThrottle.ErrorThrottled(
                    "information-status-panel-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "InformationStatusPanelOverlay",
                    "Information status panel draw failed; exception swallowed.", error);
            }

            return true;
        }
    }
}
