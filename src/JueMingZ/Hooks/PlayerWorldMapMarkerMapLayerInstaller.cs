using System;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class PlayerWorldMapMarkerMapLayerInstaller
    {
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Player-world map marker layer already installed.", "Terraria.Main.MapIcons.AddLayer");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Player-world map marker layer installation is already in progress.");
            }

            try
            {
                if (Main.MapIcons == null)
                {
                    const string message = "Terraria.Main.MapIcons is unavailable; map marker layer cannot install.";
                    Logger.Warn("PlayerWorldMapMarkerMapLayerInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                Main.MapIcons.AddLayer(new PlayerWorldMapMarkerMapLayer());
                Interlocked.Exchange(ref _installed, 1);
                const string successMessage = "Player-world map marker layer installed via Terraria.Main.MapIcons.AddLayer.";
                Logger.Info("PlayerWorldMapMarkerMapLayerInstaller", successMessage);
                return HookInstallResult.Success(successMessage, "Terraria.Main.MapIcons.AddLayer");
            }
            catch (Exception error)
            {
                const string message = "Player-world map marker layer installation failed.";
                Logger.Error("PlayerWorldMapMarkerMapLayerInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }
    }
}
