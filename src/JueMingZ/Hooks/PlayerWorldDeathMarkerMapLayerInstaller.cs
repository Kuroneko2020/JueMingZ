using System;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class PlayerWorldDeathMarkerMapLayerInstaller
    {
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Player-world death marker map layer already installed.", "Terraria.Main.MapIcons.AddLayer");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Player-world death marker map layer installation is already in progress.");
            }

            try
            {
                if (Main.MapIcons == null)
                {
                    const string message = "Terraria.Main.MapIcons is unavailable; death marker map layer cannot install.";
                    PlayerWorldDeathMarkerDiagnostics.MarkLayerSkipped(message);
                    Logger.Warn("PlayerWorldDeathMarkerMapLayerInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                Main.MapIcons.AddLayer(new PlayerWorldDeathMarkerMapLayer());
                Interlocked.Exchange(ref _installed, 1);
                const string successMessage = "Player-world death marker map layer installed via Terraria.Main.MapIcons.AddLayer.";
                PlayerWorldDeathMarkerDiagnostics.MarkLayerInstalled(successMessage);
                Logger.Info("PlayerWorldDeathMarkerMapLayerInstaller", successMessage);
                return HookInstallResult.Success(successMessage, "Terraria.Main.MapIcons.AddLayer");
            }
            catch (Exception error)
            {
                const string message = "Player-world death marker map layer installation failed.";
                PlayerWorldDeathMarkerDiagnostics.MarkLayerFailed(message, error);
                Logger.Error("PlayerWorldDeathMarkerMapLayerInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        internal static void ResetForTesting()
        {
            Interlocked.Exchange(ref _installed, 0);
            Interlocked.Exchange(ref _installing, 0);
        }
    }
}
