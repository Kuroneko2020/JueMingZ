using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Hooks
{
    public static class ScrollHotbarHookInstaller
    {
        // Hotbar scroll suppression is a narrow input hook. Missing PlayerInput
        // targets must leave vanilla scrolling untouched instead of blocking globally.
        private const string HarmonyId = "JueMingZ.ScrollHotbar.0024";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("ScrollHotbar hook already installed.", "Terraria.Player.ScrollHotbar");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("ScrollHotbar hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; ScrollHotbar hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("ScrollHotbarHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; ScrollHotbar hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("ScrollHotbarHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly() || TerrariaRuntimeTypes.PlayerType == null)
                {
                    var message = "Terraria.Player type not found; ScrollHotbar hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("ScrollHotbarHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var targets = FindScrollHotbarMethods(TerrariaRuntimeTypes.PlayerType);
                if (targets.Length == 0)
                {
                    var message = "No Terraria.Player.ScrollHotbar candidate found. Candidates: " +
                                  SafeBootstrapInstaller.FormatCandidateList(TerrariaRuntimeTypes.PlayerType, "ScrollHotbar");
                    RecordInstall(false, message);
                    Logger.Warn("ScrollHotbarHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(ScrollHotbarHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null)
                {
                    throw new MissingMethodException("ScrollHotbarHookCallbacks.Prefix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                for (var index = 0; index < targets.Length; index++)
                {
                    SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, targets[index], prefix, null);
                }

                Interlocked.Exchange(ref _installed, 1);
                var signature = string.Join("; ", targets.Select(SafeBootstrapInstaller.FormatMethodSignature).ToArray());
                var successMessage = "ScrollHotbar hook installed: " + signature;
                RecordInstall(true, successMessage);
                Logger.Info("ScrollHotbarHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "ScrollHotbar hook installation failed.";
                RecordInstall(false, message + " " + error.Message);
                Logger.Error("ScrollHotbarHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo[] FindScrollHotbarMethods(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "ScrollHotbar", StringComparison.Ordinal))
                .Where(method => !method.ContainsGenericParameters && !method.IsAbstract)
                .OrderBy(method => method.GetParameters().Length)
                .ThenBy(method => method.IsStatic ? 1 : 0)
                .ToArray();
        }

        private static void RecordInstall(bool resolved, string message)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                resolved ? "Ui.HotbarScrollHookResolved" : "Ui.HotbarScrollHookFallback",
                "UI",
                string.Empty,
                resolved ? "Succeeded" : "NotApplicable",
                resolved ? "Succeeded" : "HookUnavailable",
                message ?? string.Empty,
                0,
                "{}",
                "{\"scrollHotbarHookResolved\":" + (resolved ? "true" : "false") + "}",
                "{\"scrollHotbarHookResolved\":" + (resolved ? "true" : "false") + "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static class ScrollHotbarHookCallbacks
        {
            private static bool Prefix()
            {
                var suppress = LegacyUiInput.ShouldSuppressHotbarScrollFromHook();
                if (suppress)
                {
                    TerrariaUiMouseCompat.MarkScrollHotbarHookSuppressed();
                    return false;
                }

                return true;
            }
        }
    }
}
