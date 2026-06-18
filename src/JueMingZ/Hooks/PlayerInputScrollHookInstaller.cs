using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Search;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Hooks
{
    public static class PlayerInputScrollHookInstaller
    {
        // Scroll interception is UI/input timing sensitive. Install only verified
        // PlayerInput targets so hotbar and F5 scroll handling keep vanilla priority.
        private const string HarmonyId = "JueMingZ.PlayerInputScroll.0026";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("PlayerInput scroll hook already installed.", "Terraria.GameInput.PlayerInput");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("PlayerInput scroll hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; PlayerInput scroll hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("PlayerInputScrollHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; PlayerInput scroll hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("PlayerInputScrollHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerInputType = FindType("Terraria.GameInput.PlayerInput");
                if (playerInputType == null)
                {
                    const string message = "Terraria.GameInput.PlayerInput type not found; PlayerInput scroll hook cannot install.";
                    RecordInstall(false, message);
                    Logger.Warn("PlayerInputScrollHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var targets = FindPlayerInputScrollMethods(playerInputType);
                if (targets.Length == 0)
                {
                    var message = "No PlayerInput scroll input method candidate found. Candidates: " +
                                  SafeBootstrapInstaller.FormatCandidateList(playerInputType, "Input");
                    RecordInstall(false, message);
                    Logger.Warn("PlayerInputScrollHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var postfixMethod = typeof(PlayerInputScrollHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("PlayerInputScrollHookCallbacks.Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                for (var index = 0; index < targets.Length; index++)
                {
                    SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, targets[index], null, postfix);
                }

                Interlocked.Exchange(ref _installed, 1);
                var signature = string.Join("; ", targets.Select(SafeBootstrapInstaller.FormatMethodSignature).ToArray());
                var successMessage = "PlayerInput scroll hook installed: " + signature;
                RecordInstall(true, successMessage);
                Logger.Info("PlayerInputScrollHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "PlayerInput scroll hook installation failed.";
                RecordInstall(false, message + " " + error.Message);
                Logger.Error("PlayerInputScrollHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo[] FindPlayerInputScrollMethods(Type playerInputType)
        {
            return playerInputType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(method => IsTargetName(method.Name))
                .Where(method => !method.ContainsGenericParameters && !method.IsAbstract)
                .OrderBy(method => MethodPriority(method.Name))
                .ThenBy(method => method.GetParameters().Length)
                .ToArray();
        }

        private static bool IsTargetName(string name)
        {
            return string.Equals(name, "MouseInput", StringComparison.Ordinal) ||
                   string.Equals(name, "UpdateInput", StringComparison.Ordinal) ||
                   string.Equals(name, "Update", StringComparison.Ordinal);
        }

        private static int MethodPriority(string name)
        {
            if (string.Equals(name, "MouseInput", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(name, "UpdateInput", StringComparison.Ordinal))
            {
                return 1;
            }

            return 2;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static void RecordInstall(bool resolved, string message)
        {
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                resolved ? "Ui.PlayerInputScrollHookResolved" : "Ui.PlayerInputScrollHookFallback",
                "UI",
                string.Empty,
                resolved ? "Succeeded" : "NotApplicable",
                resolved ? "Succeeded" : "HookUnavailable",
                message ?? string.Empty,
                0,
                "{}",
                "{\"playerInputScrollHookResolved\":" + (resolved ? "true" : "false") + "}",
                "{\"playerInputScrollHookResolved\":" + (resolved ? "true" : "false") + "}",
                "UI",
                "LegacyMainWindow",
                string.Empty,
                string.Empty);
        }

        private static class PlayerInputScrollHookCallbacks
        {
            private static void Postfix(MethodBase __originalMethod)
            {
                TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionAfterPlayerInputGuard();
                MapFootprintPlaybackOverlay.UpdateAfterPlayerInputGuard();
                UserNotesPinnedOverlay.UpdateAfterPlayerInputGuard();
                SearchItemPickRuntimeService.UpdateAfterPlayerInputGuard();
                LegacyUiInput.UpdateAfterPlayerInputGuard("PlayerInputScrollHook.Postfix");
            }
        }
    }
}
