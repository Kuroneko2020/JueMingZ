using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Movement;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class TeleportRodHookInstaller
    {
        private const string HarmonyId = "JueMingZ.TeleportRodCorrection.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Teleport rod hook already installed.", HookDiagnostics.TeleportRodHookMethod);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Teleport rod hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; teleport rod hook cannot install.";
                    HookDiagnostics.MarkTeleportRodHookSkipped(message);
                    MovementTeleportCorrectionService.RecordHookMissing(message);
                    Logger.Warn("TeleportRodHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; teleport rod hook cannot install.";
                    HookDiagnostics.MarkTeleportRodHookSkipped(message);
                    MovementTeleportCorrectionService.RecordHookMissing(message);
                    Logger.Warn("TeleportRodHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; teleport rod hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    HookDiagnostics.MarkTeleportRodHookSkipped(message);
                    MovementTeleportCorrectionService.RecordHookMissing(message);
                    Logger.Warn("TeleportRodHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; teleport rod hook cannot install.";
                    HookDiagnostics.MarkTeleportRodHookSkipped(message);
                    MovementTeleportCorrectionService.RecordHookMissing(message);
                    Logger.Warn("TeleportRodHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindTeleportRodMethod(playerType);
                if (target == null)
                {
                    var message = "No reliable Terraria.Player.ItemCheck_UseTeleportRod candidate found. Candidates: " + FormatTeleportCandidates(playerType);
                    HookDiagnostics.MarkTeleportRodHookSkipped(message);
                    MovementTeleportCorrectionService.RecordHookMissing(message);
                    Logger.Warn("TeleportRodHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(TeleportRodHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(TeleportRodHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("TeleportRodHookCallbacks Prefix/Postfix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("TeleportRodHookInstaller", "Teleport rod hook target resolved: " + signature);

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Teleport rod hook installed: " + signature;
                HookDiagnostics.MarkTeleportRodHookSucceeded(signature, successMessage);
                Logger.Info("TeleportRodHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Teleport rod hook installation failed.";
                HookDiagnostics.MarkTeleportRodHookFailed(message, error);
                MovementTeleportCorrectionService.RecordHookMissing(message + " " + error.Message);
                Logger.Error("TeleportRodHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindTeleportRodMethod(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsTeleportRodMethodCandidate)
                .OrderBy(method => string.Equals(method.Name, "ItemCheck_UseTeleportRod", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool IsTeleportRodMethodCandidate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.ReturnType != typeof(void) && method.ReturnType != typeof(bool))
            {
                return false;
            }

            var name = method.Name ?? string.Empty;
            if (!string.Equals(name, "ItemCheck_UseTeleportRod", StringComparison.Ordinal) &&
                (name.IndexOf("ItemCheck", StringComparison.OrdinalIgnoreCase) < 0 ||
                 name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Any(parameter => string.Equals(parameter.ParameterType.FullName, "Terraria.Item", StringComparison.Ordinal));
        }

        private static string FormatTeleportCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method =>
                    {
                        var name = method.Name ?? string.Empty;
                        return name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               name.IndexOf("ItemCheck", StringComparison.OrdinalIgnoreCase) >= 0;
                    })
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
