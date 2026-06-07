using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class BuffRemovalHookInstaller
    {
        // DelBuff signatures vary across Terraria builds, so installation is
        // fail-closed: no verified target means no hook, not a broader patch.
        private const string HarmonyId = "JueMingZ.BuffRemoval.0041";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Buff removal hook already installed.", string.Empty);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Buff removal hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; buff removal hook cannot install.";
                    Logger.Warn("BuffRemovalHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; buff removal hook cannot install.";
                    Logger.Warn("BuffRemovalHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; buff removal hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    Logger.Warn("BuffRemovalHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; buff removal hook cannot install.";
                    Logger.Warn("BuffRemovalHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindDelBuffMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.DelBuff(int) hook candidate found. Candidates: " + FormatBuffRemovalCandidates(playerType);
                    Logger.Warn("BuffRemovalHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(BuffRemovalHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null)
                {
                    throw new MissingMethodException("BuffRemovalHookCallbacks.Prefix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, null);

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Buff removal hook installed: " + signature;
                Logger.Info("BuffRemovalHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Buff removal hook installation failed.";
                Logger.Error("BuffRemovalHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindDelBuffMethod(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "DelBuff", StringComparison.Ordinal))
                .Where(IsSingleIntVoidMethod)
                .OrderBy(method => method.IsPublic ? 0 : 1)
                .FirstOrDefault();
        }

        private static bool IsSingleIntVoidMethod(MethodInfo method)
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
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(int);
        }

        private static string FormatBuffRemovalCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("Buff", StringComparison.OrdinalIgnoreCase) >= 0)
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
