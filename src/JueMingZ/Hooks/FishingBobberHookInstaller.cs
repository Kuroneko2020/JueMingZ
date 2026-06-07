using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Fishing;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class FishingBobberHookInstaller
    {
        // Projectile.AI is a hot path; install only the verified callback and
        // keep missing reflection as a skipped hook, not a runtime scan loop.
        private const string HarmonyId = "JueMingZ.FishingBobber.0063";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                FishingAutomationDiagnostics.MarkHookInstalled();
                return HookInstallResult.Success("Fishing bobber hook already installed.", "Terraria.Projectile fishing bobber AI");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Fishing bobber hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; fishing bobber hook cannot install.";
                    FishingAutomationDiagnostics.MarkHookSkipped();
                    Logger.Warn("FishingBobberHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; fishing bobber hook cannot install.";
                    FishingAutomationDiagnostics.MarkHookSkipped();
                    Logger.Warn("FishingBobberHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var projectileType = FindType("Terraria.Projectile");
                if (projectileType == null)
                {
                    const string message = "Terraria.Projectile type not found; fishing bobber hook cannot install.";
                    FishingAutomationDiagnostics.MarkHookSkipped();
                    Logger.Warn("FishingBobberHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindFishingBobberAiMethod(projectileType) ?? FindProjectileAiMethod(projectileType);
                if (target == null)
                {
                    var message = "No Projectile fishing bobber AI candidate found. Candidates: " + FormatAiCandidates(projectileType);
                    FishingAutomationDiagnostics.MarkHookSkipped();
                    Logger.Warn("FishingBobberHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var postfixMethod = typeof(FishingBobberHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("FishingBobberHookCallbacks.Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, null, postfix);

                Interlocked.Exchange(ref _installed, 1);
                FishingAutomationDiagnostics.MarkHookInstalled();
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Fishing bobber hook installed: " + signature;
                Logger.Info("FishingBobberHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Fishing bobber hook installation failed.";
                FishingAutomationDiagnostics.MarkHookSkipped();
                Logger.Error("FishingBobberHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindFishingBobberAiMethod(Type projectileType)
        {
            return projectileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsParameterlessVoidInstance)
                .Where(method =>
                    method.Name.IndexOf("AI_061", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    method.Name.IndexOf("AI_61", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    method.Name.IndexOf("FishingBobber", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(method => method.Name.IndexOf("AI_061", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(method => method.Name.IndexOf("FishingBobber", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault();
        }

        private static MethodInfo FindProjectileAiMethod(Type projectileType)
        {
            return projectileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsParameterlessVoidInstance)
                .FirstOrDefault(method => string.Equals(method.Name, "AI", StringComparison.Ordinal));
        }

        private static bool IsParameterlessVoidInstance(MethodInfo method)
        {
            return method != null &&
                   !method.IsStatic &&
                   !method.IsAbstract &&
                   !method.ContainsGenericParameters &&
                   method.ReturnType == typeof(void) &&
                   method.GetParameters().Length == 0;
        }

        private static string FormatAiCandidates(Type projectileType)
        {
            try
            {
                var candidates = projectileType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     method.Name.IndexOf("Bobber", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(SafeBootstrapInstaller.FormatMethodSignature)
                    .ToArray();
                return candidates.Length == 0 ? "<none>" : string.Join(" | ", candidates);
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }

        private static Type FindType(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    var type = assemblies[index].GetType(fullName, false);
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
    }
}
