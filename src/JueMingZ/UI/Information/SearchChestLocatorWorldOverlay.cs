using System;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Search.ChestLocator;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI.Information
{
    public static class SearchChestLocatorWorldOverlay
    {
        public static bool DrawInterfaceLayer()
        {
            try
            {
                var snapshot = SearchChestLocatorUiState.GetSnapshot();
                if (!ChestItemLocatorOverlayService.NeedsWorldContext(snapshot))
                {
                    ChestItemLocatorOverlayService.RecordSnapshotSkip(snapshot);
                    return true;
                }

                InformationWorldContext context;
                string skipReason;
                if (!InformationWorldContextProvider.TryBuild(out context, out skipReason))
                {
                    ChestItemLocatorOverlayService.RecordContextSkip(snapshot, skipReason);
                    return true;
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("SearchChestLocatorWorldOverlay", true, out spriteBatch))
                {
                    ChestItemLocatorOverlayService.RecordDrawGuardSkip(snapshot);
                    return true;
                }

                ChestItemLocatorOverlayService.DrawWorldOverlay(spriteBatch, context, snapshot);
            }
            catch (Exception error)
            {
                UiDrawLifecycleGuard.RecordDrawException("SearchChestLocatorWorldOverlay", error);
                LogThrottle.ErrorThrottled(
                    "search-chest-locator-world-overlay-error",
                    TimeSpan.FromSeconds(10),
                    "SearchChestLocatorWorldOverlay",
                    "Search chest locator world overlay draw failed; exception swallowed.", error);
            }

            return true;
        }
    }
}
