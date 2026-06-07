using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class MovementSafeLandingPlayerUpdateHookInstaller
    {
        // Safe-landing installation is a guarded Player.Update prefix. Missing
        // runtime types keep rescue pulses inactive rather than guessing player fields.
        private const string HarmonyId = "JueMingZ.MovementSafeLanding.PlayerUpdate.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Safe landing Player.Update hook already installed.", MovementSafeLandingCompat.PlayerUpdateHookMessage);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Safe landing Player.Update hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; safe landing Player.Update hook cannot install.";
                    MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message);
                    Logger.Warn("MovementSafeLandingPlayerUpdateHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; safe landing Player.Update hook cannot install.";
                    MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message);
                    Logger.Warn("MovementSafeLandingPlayerUpdateHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; safe landing Player.Update hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message);
                    Logger.Warn("MovementSafeLandingPlayerUpdateHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; safe landing Player.Update hook cannot install.";
                    MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message);
                    Logger.Warn("MovementSafeLandingPlayerUpdateHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindPlayerUpdateMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.Update(int) candidate found. Candidates: " + FormatUpdateCandidates(playerType);
                    MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message);
                    Logger.Warn("MovementSafeLandingPlayerUpdateHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(MovementSafeLandingPlayerUpdateHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null)
                {
                    throw new MissingMethodException("MovementSafeLandingPlayerUpdateHookCallbacks.Prefix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("MovementSafeLandingPlayerUpdateHookInstaller", "Safe landing Player.Update hook target resolved: " + signature);

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, null);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Safe landing Player.Update hook installed: " + signature;
                MovementSafeLandingCompat.MarkPlayerUpdateHookResult(true, successMessage);
                Logger.Info("MovementSafeLandingPlayerUpdateHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Safe landing Player.Update hook installation failed.";
                MovementSafeLandingCompat.MarkPlayerUpdateHookResult(false, message + " " + error.Message);
                Logger.Error("MovementSafeLandingPlayerUpdateHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindPlayerUpdateMethod(Type playerType)
        {
            var exact = playerType.GetMethod(
                "Update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(int) },
                null);
            if (IsPlayerUpdateCandidate(exact))
            {
                return exact;
            }

            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsPlayerUpdateCandidate)
                .OrderBy(method => string.Equals(method.Name, "Update", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault();
        }

        private static bool IsPlayerUpdateCandidate(MethodInfo method)
        {
            if (method == null ||
                method.IsStatic ||
                method.IsAbstract ||
                method.IsSpecialName ||
                method.ContainsGenericParameters ||
                method.ReturnType != typeof(void) ||
                !string.Equals(method.Name, "Update", StringComparison.Ordinal))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(int);
        }

        private static string FormatUpdateCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0)
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
