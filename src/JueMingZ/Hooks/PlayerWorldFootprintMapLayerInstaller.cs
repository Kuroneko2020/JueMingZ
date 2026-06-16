using System;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class PlayerWorldFootprintMapLayerInstaller
    {
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Player-world footprint map layer already installed.", "Terraria.Main.MapIcons.AddLayer");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Player-world footprint map layer installation is already in progress.");
            }

            try
            {
                if (Main.MapIcons == null)
                {
                    const string message = "Terraria.Main.MapIcons is unavailable; footprint map layer cannot install.";
                    Logger.Warn("PlayerWorldFootprintMapLayerInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                Main.MapIcons.AddLayer(new PlayerWorldFootprintMapLayer());
                Interlocked.Exchange(ref _installed, 1);
                const string successMessage = "Player-world footprint map layer installed via Terraria.Main.MapIcons.AddLayer.";
                Logger.Info("PlayerWorldFootprintMapLayerInstaller", successMessage);
                return HookInstallResult.Success(successMessage, "Terraria.Main.MapIcons.AddLayer");
            }
            catch (Exception error)
            {
                const string message = "Player-world footprint map layer installation failed.";
                Logger.Error("PlayerWorldFootprintMapLayerInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }
    }
}
