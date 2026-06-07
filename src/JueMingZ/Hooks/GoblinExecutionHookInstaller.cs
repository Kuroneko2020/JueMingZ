using System;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingZ.Hooks
{
    public static class GoblinExecutionHookInstaller
    {
        // Both IL anchors must be proven before the feature is marked hook-ready.
        // Partial or failed installation leaves the compat path disabled.
        private const string HarmonyId = "JueMingZ.GoblinExecution.0001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Goblin execution hooks already installed.", HookDiagnostics.GoblinExecutionHookMethod);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Goblin execution hook installation is already in progress.");
            }

            try
            {
                CombatGoblinExecutionCompat.SetHookReady(false);

                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; goblin execution hooks cannot install.";
                    HookDiagnostics.MarkGoblinExecutionHookSkipped(message);
                    Logger.Warn("GoblinExecutionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; goblin execution hooks cannot install.";
                    HookDiagnostics.MarkGoblinExecutionHookSkipped(message);
                    Logger.Warn("GoblinExecutionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerTarget = FindPlayerProcessHitAgainstNpcMethod();
                if (playerTarget == null)
                {
                    const string message = "Terraria.Player.ProcessHitAgainstNPC target not found; goblin execution player hook cannot install.";
                    HookDiagnostics.MarkGoblinExecutionHookSkipped(message);
                    Logger.Warn("GoblinExecutionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var projectileTarget = FindProjectileDamagePveInnerMethod();
                if (projectileTarget == null)
                {
                    const string message = "Terraria.Projectile.Damage_PVE_Inner target not found; goblin execution projectile hook cannot install.";
                    HookDiagnostics.MarkGoblinExecutionHookSkipped(message);
                    Logger.Warn("GoblinExecutionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var playerTranspilerMethod = typeof(GoblinExecutionHookCallbacks).GetMethod(
                    "PlayerProcessHitAgainstNpcTranspiler",
                    BindingFlags.Static | BindingFlags.NonPublic);
                var projectileTranspilerMethod = typeof(GoblinExecutionHookCallbacks).GetMethod(
                    "ProjectileDamagePveInnerTranspiler",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (playerTranspilerMethod == null || projectileTranspilerMethod == null)
                {
                    throw new MissingMethodException("GoblinExecutionHookCallbacks transpilers");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var playerTranspiler = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, playerTranspilerMethod);
                var projectileTranspiler = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, projectileTranspilerMethod);

                GoblinExecutionHookCallbacks.ResetPatchState();
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, playerTarget, null, null, playerTranspiler);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, projectileTarget, null, null, projectileTranspiler);

                var playerApplied = GoblinExecutionHookCallbacks.PlayerProcessHitPatchApplied;
                var projectileApplied = GoblinExecutionHookCallbacks.ProjectileDamagePatchApplied;
                var signature = SafeBootstrapInstaller.FormatMethodSignature(playerTarget) + " | " +
                                SafeBootstrapInstaller.FormatMethodSignature(projectileTarget);
                if (!playerApplied || !projectileApplied)
                {
                    var message = "Goblin execution hooks were not fully applied; playerPatch=" + playerApplied +
                                  ", projectilePatch=" + projectileApplied + ". Feature remains fail-safe disabled.";
                    HookDiagnostics.MarkGoblinExecutionHookSkipped(message);
                    Logger.Warn("GoblinExecutionHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                CombatGoblinExecutionCompat.SetHookReady(true);
                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Goblin execution hooks installed: " + signature;
                HookDiagnostics.MarkGoblinExecutionHookSucceeded(signature, successMessage);
                Logger.Info("GoblinExecutionHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                CombatGoblinExecutionCompat.SetHookReady(false);
                const string message = "Goblin execution hook installation failed.";
                HookDiagnostics.MarkGoblinExecutionHookFailed(message, error);
                Logger.Error("GoblinExecutionHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindPlayerProcessHitAgainstNpcMethod()
        {
            return typeof(Player).GetMethod(
                "ProcessHitAgainstNPC",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Item), typeof(Rectangle), typeof(int), typeof(float), typeof(int) },
                null);
        }

        private static MethodInfo FindProjectileDamagePveInnerMethod()
        {
            return typeof(Projectile).GetMethod(
                "Damage_PVE_Inner",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(NPC), typeof(Rectangle), typeof(float), typeof(int[]), typeof(bool).MakeByRefType() },
                null);
        }
    }
}
