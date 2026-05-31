using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.Combat;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class ProjectileAiHookInstaller
    {
        private const string HarmonyId = "JueMingZ.ProjectileAI.0054";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Projectile AI hook already installed.", CombatAimPersistentCursorService.PersistentHook);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Projectile AI hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; Projectile AI hook cannot install.";
                    CombatAimPersistentCursorService.MarkHookFailed(message);
                    Logger.Warn("ProjectileAiHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; Projectile AI hook cannot install.";
                    CombatAimPersistentCursorService.MarkHookFailed(message);
                    Logger.Warn("ProjectileAiHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var projectileType = FindType("Terraria.Projectile");
                if (projectileType == null)
                {
                    const string message = "Terraria.Projectile type not found; Projectile AI hook cannot install.";
                    CombatAimPersistentCursorService.MarkHookFailed(message);
                    Logger.Warn("ProjectileAiHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var hookName = PersistentCursorHooks.ProjectileAI;
                var target = FindProjectileAiMethod(projectileType);
                if (target == null)
                {
                    hookName = PersistentCursorHooks.AI099;
                    target = FindYoyoAiMethod(projectileType);
                }

                if (target == null)
                {
                    var message = "No Projectile.AI candidate found. Candidates: " + FormatAiCandidates(projectileType);
                    CombatAimPersistentCursorService.MarkHookFailed(message);
                    Logger.Warn("ProjectileAiHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(ProjectileAiHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var postfixMethod = typeof(ProjectileAiHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null || postfixMethod == null)
                {
                    throw new MissingMethodException("ProjectileAiHookCallbacks Prefix/Postfix");
                }

                var killTarget = FindProjectileKillMethod(projectileType);
                var killPrefixMethod = typeof(ProjectileKillHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                var killPostfixMethod = typeof(ProjectileKillHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (killPrefixMethod == null || killPostfixMethod == null)
                {
                    throw new MissingMethodException("ProjectileKillHookCallbacks Prefix/Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, postfix);

                var killSignature = string.Empty;
                if (killTarget != null)
                {
                    var killPrefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, killPrefixMethod);
                    var killPostfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, killPostfixMethod);
                    SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, killTarget, killPrefix, killPostfix);
                    killSignature = SafeBootstrapInstaller.FormatMethodSignature(killTarget);
                }
                else
                {
                    Logger.Warn("ProjectileAiHookInstaller", "No Projectile.Kill candidate found. Candidates: " + FormatKillCandidates(projectileType));
                }

                Interlocked.Exchange(ref _installed, 1);
                CombatAimPersistentCursorService.MarkHookInstalled(hookName);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Projectile AI hook installed: " + hookName + " -> " + signature;
                if (!string.IsNullOrWhiteSpace(killSignature))
                {
                    successMessage += "; Projectile Kill hook installed: " + PersistentCursorHooks.ProjectileKill + " -> " + killSignature;
                }
                Logger.Info("ProjectileAiHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, hookName);
            }
            catch (Exception error)
            {
                const string message = "Projectile AI hook installation failed.";
                CombatAimPersistentCursorService.MarkHookFailed(error.Message);
                Logger.Error("ProjectileAiHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindYoyoAiMethod(Type projectileType)
        {
            return projectileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsParameterlessVoidInstance)
                .FirstOrDefault(method =>
                    string.Equals(method.Name, "AI_099", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(method.Name, "AI_99", StringComparison.OrdinalIgnoreCase) ||
                    method.Name.IndexOf("Yoyo", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static MethodInfo FindProjectileAiMethod(Type projectileType)
        {
            return projectileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsParameterlessVoidInstance)
                .OrderBy(method => string.Equals(method.Name, "AI", StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(method => method.Name)
                .FirstOrDefault(method => string.Equals(method.Name, "AI", StringComparison.Ordinal));
        }

        private static MethodInfo FindProjectileKillMethod(Type projectileType)
        {
            return projectileType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(IsParameterlessVoidInstance)
                .FirstOrDefault(method => string.Equals(method.Name, "Kill", StringComparison.Ordinal));
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
                    .Where(method => method.Name.IndexOf("AI", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(SafeBootstrapInstaller.FormatMethodSignature)
                    .ToArray();
                return candidates.Length == 0 ? "<none>" : string.Join(" | ", candidates);
            }
            catch (Exception error)
            {
                return "<failed to list candidates: " + error.Message + ">";
            }
        }

        private static string FormatKillCandidates(Type projectileType)
        {
            try
            {
                var candidates = projectileType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(method => method.Name.IndexOf("Kill", StringComparison.OrdinalIgnoreCase) >= 0)
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
