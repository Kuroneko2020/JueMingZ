using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class MovementDashHookInstaller
    {
        private const string HarmonyId = "JueMingZ.MovementDash.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("DashMovement hook already installed.", TerrariaDashCompat.DashMovementHookMessage);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("DashMovement hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; DashMovement hook cannot install.";
                    TerrariaDashCompat.MarkDashMovementHookResult(false, message);
                    Logger.Warn("MovementDashHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; DashMovement hook cannot install.";
                    TerrariaDashCompat.MarkDashMovementHookResult(false, message);
                    Logger.Warn("MovementDashHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; DashMovement hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    TerrariaDashCompat.MarkDashMovementHookResult(false, message);
                    Logger.Warn("MovementDashHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerType = TerrariaRuntimeTypes.PlayerType;
                if (playerType == null)
                {
                    const string message = "Terraria.Player type not found; DashMovement hook cannot install.";
                    TerrariaDashCompat.MarkDashMovementHookResult(false, message);
                    Logger.Warn("MovementDashHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindDashMovementMethod(playerType);
                if (target == null)
                {
                    var message = "No Terraria.Player.DashMovement candidate found. Candidates: " + FormatDashMovementCandidates(playerType);
                    TerrariaDashCompat.MarkDashMovementHookResult(false, message);
                    Logger.Warn("MovementDashHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(MovementDashHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(MovementDashHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("MovementDashHookCallbacks Prefix/Postfix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("MovementDashHookInstaller", "DashMovement hook target resolved: " + signature);

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "DashMovement hook installed: " + signature;
                TerrariaDashCompat.MarkDashMovementHookResult(true, successMessage);
                Logger.Info("MovementDashHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "DashMovement hook installation failed.";
                TerrariaDashCompat.MarkDashMovementHookResult(false, message + " " + error.Message);
                Logger.Error("MovementDashHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindDashMovementMethod(Type playerType)
        {
            var exact = playerType.GetMethod(
                "DashMovement",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (exact != null && IsDashMovementCandidate(exact))
            {
                return exact;
            }

            return playerType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsDashMovementCandidate)
                .OrderBy(method => string.Equals(method.Name, "DashMovement", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault();
        }

        private static bool IsDashMovementCandidate(MethodInfo method)
        {
            return method != null &&
                   !method.IsStatic &&
                   !method.IsAbstract &&
                   !method.IsSpecialName &&
                   !method.ContainsGenericParameters &&
                   method.ReturnType == typeof(void) &&
                   method.GetParameters().Length == 0 &&
                   method.Name.IndexOf("DashMovement", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatDashMovementCandidates(Type playerType)
        {
            try
            {
                var candidates = playerType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("Dash", StringComparison.OrdinalIgnoreCase) >= 0)
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
