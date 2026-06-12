using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Search;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Bootstrap
{
    public static class HookInstaller
    {
        private const string HarmonyId = "JueMingZ.RuntimeUpdate.M1";
        private static int _updateHookInstalled;
        private static int _updateHookInstalling;

        public static HookInstallResult Install()
        {
            return InstallUpdateHook();
        }

        public static HookInstallResult InstallUpdateHook()
        {
            if (_updateHookInstalled == 1)
            {
                return HookInstallResult.Success("Update Hook already installed.", HookDiagnostics.UpdateHookMethod);
            }

            if (Interlocked.Exchange(ref _updateHookInstalling, 1) == 1)
            {
                return HookInstallResult.Skipped("Update Hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; Update Hook cannot install.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("HookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; Update Hook cannot install.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("HookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var mainType = GameMode.FindTerrariaMainType();
                if (mainType == null)
                {
                    const string message = "Terraria.Main type not found; Update Hook cannot install.";
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("HookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var updateMethod = FindUpdateMethod(mainType);
                if (updateMethod == null)
                {
                    var message = "No Terraria.Main.Update(GameTime) candidate found. Candidates: " +
                                  SafeBootstrapInstaller.FormatCandidateList(mainType, "Update");
                    HookDiagnostics.MarkSkipped(message);
                    Logger.Warn("HookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(updateMethod);
                var prefixMethod = typeof(RuntimeUpdatePostfix).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(RuntimeUpdatePostfix).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("RuntimeUpdatePostfix Prefix/Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, updateMethod, prefix, postfix);

                Interlocked.Exchange(ref _updateHookInstalled, 1);
                var successMessage = "Update Hook installed: " + signature;
                HookDiagnostics.MarkUpdateHookSucceeded(signature, successMessage);
                Logger.Info("HookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Update Hook installation failed.";
                HookDiagnostics.MarkFailed(message, error);
                Logger.Error("HookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _updateHookInstalling, 0);
            }
        }

        private static MethodInfo FindUpdateMethod(Type mainType)
        {
            return mainType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "Update", StringComparison.Ordinal))
                .Where(IsUpdateGameTimeMethod)
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool IsUpdateGameTimeMethod(MethodInfo method)
        {
            if (method == null || method.ContainsGenericParameters || method.IsAbstract || method.IsStatic)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 1 &&
                   string.Equals(parameters[0].ParameterType.Name, "GameTime", StringComparison.Ordinal);
        }

        private static class RuntimeUpdatePostfix
        {
            private static void Prefix()
            {
                try
                {
                    UiInputFrameClock.BeginUpdateFrame("Main.Update.Prefix");
                    TerrariaUiMouseCompat.UpdateActiveTriggerSuppressionPrefixGuard();
                    DiagnosticUiInteractionBridge.UpdatePrefixGuard();
                    LegacyUiInput.UpdatePrefixGuard();
                    SearchItemPickRuntimeService.UpdatePrefixGuard();
                    MapQuickAnnouncementRuntimeService.UpdatePrefixGuard();
                    CombatPerfectRevolverService.UpdatePrefixGuard();
                    CombatAimPersistentCursorService.BeginFrame();
                }
                catch (Exception error)
                {
                    RuntimeDiagnostics.RecordError("RuntimeUpdatePrefix", error);
                    LogThrottle.ErrorThrottled(
                        "runtime-update-prefix-error",
                        TimeSpan.FromSeconds(10),
                        "HookInstaller",
                        "Runtime Update prefix UI guard failed; exception swallowed.", error);
                }
            }

            private static void Postfix()
            {
                try
                {
                    CombatAimPersistentCursorService.EndFrame();
                    JueMingZRuntime.Update();
                }
                catch (Exception error)
                {
                    RuntimeDiagnostics.RecordError("RuntimeUpdatePostfix", error);
                    LogThrottle.ErrorThrottled(
                        "runtime-update-postfix-error",
                        TimeSpan.FromSeconds(10),
                        "HookInstaller",
                        "Runtime Update Hook failed; exception swallowed.", error);
                }
            }
        }
    }

    public sealed class HookInstallResult
    {
        public bool Attempted { get; set; }
        public bool Succeeded { get; set; }
        public string Message { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;

        public static HookInstallResult Skipped(string message)
        {
            return new HookInstallResult
            {
                Attempted = true,
                Succeeded = false,
                Message = message ?? string.Empty
            };
        }

        public static HookInstallResult Success(string message, string methodName)
        {
            return new HookInstallResult
            {
                Attempted = true,
                Succeeded = true,
                Message = message ?? string.Empty,
                MethodName = methodName ?? string.Empty
            };
        }

        public static HookInstallResult Failed(string message, Exception error)
        {
            return new HookInstallResult
            {
                Attempted = true,
                Succeeded = false,
                Message = message ?? string.Empty,
                Error = error == null ? string.Empty : error.ToString()
            };
        }
    }
}
