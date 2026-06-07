using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class AutoMiningPickTileHookInstaller
    {
        // Install only the exact Player.PickTile shape we can verify. Missing
        // Harmony, Terraria types, or callback methods must skip instead of guessing.
        private const string HarmonyId = "JueMingZ.AutoMining.PickTile.0001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Auto mining PickTile hook already installed.", string.Empty);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Auto mining PickTile hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; auto mining PickTile hook cannot install.";
                    Logger.Warn("AutoMiningPickTileHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; auto mining PickTile hook cannot install.";
                    Logger.Warn("AutoMiningPickTileHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; auto mining PickTile hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    Logger.Warn("AutoMiningPickTileHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; auto mining PickTile hook cannot install.";
                    Logger.Warn("AutoMiningPickTileHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindPickTileMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.PickTile(int,int,int) hook candidate found. Candidates: " + FormatPickTileCandidates(playerType);
                    Logger.Warn("AutoMiningPickTileHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(AutoMiningPickTileHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(AutoMiningPickTileHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("AutoMiningPickTileHookCallbacks Prefix/Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Auto mining PickTile hook installed: " + signature;
                Logger.Info("AutoMiningPickTileHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Auto mining PickTile hook installation failed.";
                Logger.Error("AutoMiningPickTileHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindPickTileMethod(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "PickTile", StringComparison.Ordinal))
                .Where(IsThreeIntVoidMethod)
                .OrderBy(method => method.IsPublic ? 0 : 1)
                .FirstOrDefault();
        }

        private static bool IsThreeIntVoidMethod(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 3 &&
                   parameters[0].ParameterType == typeof(int) &&
                   parameters[1].ParameterType == typeof(int) &&
                   parameters[2].ParameterType == typeof(int);
        }

        private static string FormatPickTileCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("PickTile", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(SafeBootstrapInstaller.FormatMethodSignature)
                    .ToArray();
                return candidates.Length == 0 ? "<none>" : string.Join(" | ", candidates);
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }
    }
}
