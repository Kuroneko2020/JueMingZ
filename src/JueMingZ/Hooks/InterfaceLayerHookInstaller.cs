using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Hooks
{
    public static class InterfaceLayerHookInstaller
    {
        private const string HarmonyId = "JueMingZ.InterfaceLayer.M2_1";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Interface layer hook already installed.", HookDiagnostics.InterfaceLayerHookMethod);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Interface layer hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; Interface layer hook cannot install.";
                    HookDiagnostics.MarkInterfaceLayerHookSkipped(message);
                    Logger.Warn("InterfaceLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; Interface layer hook cannot install.";
                    HookDiagnostics.MarkInterfaceLayerHookSkipped(message);
                    Logger.Warn("InterfaceLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var mainType = GameMode.FindTerrariaMainType();
                if (mainType == null)
                {
                    const string message = "Terraria.Main type not found; Interface layer hook cannot install.";
                    HookDiagnostics.MarkInterfaceLayerHookSkipped(message);
                    Logger.Warn("InterfaceLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindSetupDrawInterfaceLayersMethod(mainType);
                if (target == null)
                {
                    var message = "No SetupDrawInterfaceLayers candidate found. Candidates: " +
                                  SafeBootstrapInstaller.FormatCandidateList(mainType, "SetupDrawInterfaceLayers", "DrawInterface");
                    HookDiagnostics.MarkInterfaceLayerHookSkipped(message);
                    Logger.Warn("InterfaceLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var postfixMethod = typeof(InterfaceLayerHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("InterfaceLayerHookCallbacks.Postfix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("InterfaceLayerHookInstaller", "Interface layer hook target resolved: " + signature);

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Interface layer hook installed: " + signature;
                HookDiagnostics.MarkInterfaceLayerHookSucceeded(signature, successMessage);
                Logger.Info("InterfaceLayerHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Interface layer hook installation failed.";
                HookDiagnostics.MarkInterfaceLayerHookFailed(message, error);
                Logger.Error("InterfaceLayerHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindSetupDrawInterfaceLayersMethod(Type mainType)
        {
            return mainType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "SetupDrawInterfaceLayers", StringComparison.Ordinal))
                .Where(method => !method.IsStatic && !method.IsAbstract && !method.ContainsGenericParameters)
                .Where(method => method.ReturnType == typeof(void))
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }
    }
}
