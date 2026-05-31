using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class ItemUseHookInstaller
    {
        private const string HarmonyId = "JueMingZ.ItemCheck.M6_0";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("ItemCheck hook already installed.", HookDiagnostics.ItemCheckHookMethod);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("ItemCheck hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; ItemCheck hook cannot install.";
                    HookDiagnostics.MarkItemCheckHookSkipped(message);
                    Logger.Warn("ItemUseHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; ItemCheck hook cannot install.";
                    HookDiagnostics.MarkItemCheckHookSkipped(message);
                    Logger.Warn("ItemUseHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; ItemCheck hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    HookDiagnostics.MarkItemCheckHookSkipped(message);
                    Logger.Warn("ItemUseHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; ItemCheck hook cannot install.";
                    HookDiagnostics.MarkItemCheckHookSkipped(message);
                    Logger.Warn("ItemUseHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindItemCheckMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.ItemCheck candidate found. Candidates: " + FormatItemCheckCandidates(playerType);
                    HookDiagnostics.MarkItemCheckHookSkipped(message);
                    Logger.Warn("ItemUseHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(ItemUseHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(ItemUseHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("ItemUseHookCallbacks Prefix/Postfix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("ItemUseHookInstaller", "ItemCheck hook target resolved: " + signature);

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "ItemCheck hook installed: " + signature;
                HookDiagnostics.MarkItemCheckHookSucceeded(signature, successMessage);
                Logger.Info("ItemUseHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "ItemCheck hook installation failed.";
                HookDiagnostics.MarkItemCheckHookFailed(message, error);
                Logger.Error("ItemUseHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindItemCheckMethod(Type playerType)
        {
            var exact = playerType.GetMethod(
                "ItemCheck",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (exact != null && IsItemCheckCandidate(exact))
            {
                return exact;
            }

            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsItemCheckCandidate)
                .OrderBy(method => string.Equals(method.Name, "ItemCheck", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault();
        }

        private static bool IsItemCheckCandidate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            return method.GetParameters().Length == 0 &&
                   method.Name.IndexOf("ItemCheck", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatItemCheckCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("ItemCheck", StringComparison.OrdinalIgnoreCase) >= 0)
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
