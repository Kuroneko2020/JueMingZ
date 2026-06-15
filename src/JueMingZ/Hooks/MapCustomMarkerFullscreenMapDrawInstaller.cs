using System;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class MapCustomMarkerFullscreenMapDrawInstaller
    {
        internal const string HookTargetName = "Terraria.Main.OnPostFullscreenMapDraw";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Map marker fullscreen picker draw hook already installed.", HookTargetName);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Map marker fullscreen picker draw hook installation is already in progress.");
            }

            try
            {
                Main.OnPostFullscreenMapDraw -= MapCustomMarkerStylePickerOverlay.DrawFullscreenMapLayer;
                Main.OnPostFullscreenMapDraw += MapCustomMarkerStylePickerOverlay.DrawFullscreenMapLayer;
                Interlocked.Exchange(ref _installed, 1);
                const string successMessage = "Map marker fullscreen picker draw hook installed via Terraria.Main.OnPostFullscreenMapDraw.";
                Logger.Info("MapCustomMarkerFullscreenMapDrawInstaller", successMessage);
                return HookInstallResult.Success(successMessage, HookTargetName);
            }
            catch (Exception error)
            {
                const string message = "Map marker fullscreen picker draw hook installation failed.";
                Logger.Error("MapCustomMarkerFullscreenMapDrawInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        internal static string GetHookTargetNameForTesting()
        {
            return HookTargetName;
        }
    }
}
