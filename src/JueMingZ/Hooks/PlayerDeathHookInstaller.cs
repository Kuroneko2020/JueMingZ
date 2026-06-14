using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class PlayerDeathHookInstaller
    {
        // Death recording must hook the vanilla death moment. If this exact
        // signature is unavailable, records fail closed instead of guessing.
        private const string HarmonyId = "JueMingZ.PlayerDeath.0001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Player death hook already installed.", HookDiagnostics.PlayerDeathHookMethod);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Player death hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; Player.KillMe hook cannot install.";
                    HookDiagnostics.MarkPlayerDeathHookSkipped(message);
                    Logger.Warn("PlayerDeathHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; Player.KillMe hook cannot install.";
                    HookDiagnostics.MarkPlayerDeathHookSkipped(message);
                    Logger.Warn("PlayerDeathHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; Player.KillMe hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    HookDiagnostics.MarkPlayerDeathHookSkipped(message);
                    Logger.Warn("PlayerDeathHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; Player.KillMe hook cannot install.";
                    HookDiagnostics.MarkPlayerDeathHookSkipped(message);
                    Logger.Warn("PlayerDeathHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindKillMeMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.KillMe(PlayerDeathReason,double,int,bool) candidate found. Candidates: " + FormatKillMeCandidates(playerType);
                    HookDiagnostics.MarkPlayerDeathHookSkipped(message);
                    Logger.Warn("PlayerDeathHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(PlayerDeathHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(PlayerDeathHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("PlayerDeathHookCallbacks Prefix/Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Player death hook installed: " + signature;
                HookDiagnostics.MarkPlayerDeathHookSucceeded(signature, successMessage);
                Logger.Info("PlayerDeathHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Player death hook installation failed.";
                HookDiagnostics.MarkPlayerDeathHookFailed(message, error);
                Logger.Error("PlayerDeathHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindKillMeMethod(Type playerType)
        {
            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsKillMeCandidate)
                .OrderBy(method => string.Equals(method.Name, "KillMe", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault();
        }

        private static bool IsKillMeCandidate(MethodInfo method)
        {
            if (method == null || method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (!string.Equals(method.Name, "KillMe", StringComparison.Ordinal) || method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 4 &&
                   string.Equals(parameters[0].ParameterType.FullName, "Terraria.DataStructures.PlayerDeathReason", StringComparison.Ordinal) &&
                   parameters[1].ParameterType == typeof(double) &&
                   parameters[2].ParameterType == typeof(int) &&
                   parameters[3].ParameterType == typeof(bool);
        }

        private static string FormatKillMeCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("KillMe", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(SafeBootstrapInstaller.FormatMethodSignature)
                    .ToArray();
                return candidates.Length == 0 ? "<none>" : string.Join(" | ", candidates);
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }

        internal static string GetSelectedKillMeSignatureForTesting(Type playerType)
        {
            return SafeBootstrapInstaller.FormatMethodSignature(FindKillMeMethod(playerType));
        }
    }
}
