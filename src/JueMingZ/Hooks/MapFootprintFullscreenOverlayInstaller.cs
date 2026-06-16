using System;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class MapFootprintFullscreenOverlayInstaller
    {
        internal const string HookTargetName = "Terraria.Main.OnPostFullscreenMapDraw";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Map footprint playback overlay draw hook already installed.", HookTargetName);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Map footprint playback overlay draw hook installation is already in progress.");
            }

            try
            {
                Main.OnPostFullscreenMapDraw -= MapFootprintPlaybackOverlay.DrawFullscreenMapLayer;
                Main.OnPostFullscreenMapDraw += MapFootprintPlaybackOverlay.DrawFullscreenMapLayer;
                Interlocked.Exchange(ref _installed, 1);
                const string successMessage = "Map footprint playback overlay draw hook installed via Terraria.Main.OnPostFullscreenMapDraw.";
                Logger.Info("MapFootprintFullscreenOverlayInstaller", successMessage);
                return HookInstallResult.Success(successMessage, HookTargetName);
            }
            catch (Exception error)
            {
                const string message = "Map footprint playback overlay draw hook installation failed.";
                Logger.Error("MapFootprintFullscreenOverlayInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }
    }
}
