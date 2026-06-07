using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class TravelMenuSaveGuardHookInstaller
    {
        // Player and world save hooks are installed together for one scoped guard.
        // If either target is missing, skip so save ordering remains vanilla.
        private const string HarmonyId = "JueMingZ.TravelMenuSaveGuard.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Travel menu save guard hooks already installed.", "Player.SavePlayer / WorldFile.SaveWorld");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Travel menu save guard hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    return Skip("Harmony not found; travel menu save guard hooks cannot install.");
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    return Skip("HarmonyMethod not found; travel menu save guard hooks cannot install.");
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    return Skip("Terraria runtime types unavailable; travel menu save guard hooks cannot install: " + TerrariaRuntimeTypes.LastError);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                var mainType = TerrariaRuntimeTypes.MainType;
                var worldFileType = mainType == null ? null : mainType.Assembly.GetType("Terraria.IO.WorldFile", false);
                var playerSave = playerType == null ? null : FindPlayerSaveMethod(playerType);
                var worldSave = worldFileType == null ? null : FindWorldSaveMethod(worldFileType);
                if (playerSave == null || worldSave == null)
                {
                    var message = "Travel menu save guard targets missing. playerSave=" + (playerSave != null) + ", worldSave=" + (worldSave != null) + ".";
                    TravelMenuService.RecordSaveGuardHook(false, message);
                    Logger.Warn("TravelMenuSaveGuardHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var callbacks = typeof(TravelMenuSaveGuardHookCallbacks);
                var playerPrefix = callbacks.GetMethod("PlayerSavePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var playerPostfix = callbacks.GetMethod("PlayerSavePostfix", BindingFlags.Static | BindingFlags.NonPublic);
                var worldPrefix = callbacks.GetMethod("WorldSavePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var worldPostfix = callbacks.GetMethod("WorldSavePostfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (playerPrefix == null || playerPostfix == null || worldPrefix == null || worldPostfix == null)
                {
                    throw new MissingMethodException("Travel menu save guard callback methods not found.");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                SafeBootstrapInstaller.PatchWithHarmony(
                    harmonyType,
                    harmony,
                    playerSave,
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, playerPrefix),
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, playerPostfix));
                SafeBootstrapInstaller.PatchWithHarmony(
                    harmonyType,
                    harmony,
                    worldSave,
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, worldPrefix),
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, worldPostfix));

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(playerSave) + " | " + SafeBootstrapInstaller.FormatMethodSignature(worldSave);
                var successMessage = "Travel menu save guard hooks installed: " + signature;
                TravelMenuService.RecordSaveGuardHook(true, successMessage);
                Logger.Info("TravelMenuSaveGuardHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                var message = "Travel menu save guard hook installation failed: " + error.Message;
                TravelMenuService.RecordSaveGuardHook(false, message);
                Logger.Error("TravelMenuSaveGuardHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static HookInstallResult Skip(string message)
        {
            TravelMenuService.RecordSaveGuardHook(false, message);
            Logger.Warn("TravelMenuSaveGuardHookInstaller", message);
            return HookInstallResult.Skipped(message);
        }

        private static MethodInfo FindPlayerSaveMethod(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "SavePlayer", StringComparison.Ordinal))
                .Where(method => method.ReturnType == typeof(void))
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length >= 1 &&
                           parameters.Length <= 2 &&
                           parameters[0].ParameterType.FullName == "Terraria.IO.PlayerFileData";
                })
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private static MethodInfo FindWorldSaveMethod(Type worldFileType)
        {
            return worldFileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "SaveWorld", StringComparison.Ordinal))
                .Where(method => method.ReturnType == typeof(void))
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length < 1 || parameters.Length > 3)
                    {
                        return false;
                    }

                    for (var index = 0; index < parameters.Length; index++)
                    {
                        if (parameters[index].ParameterType != typeof(bool))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .OrderByDescending(method => method.GetParameters().Length)
                .FirstOrDefault();
        }
    }
}
