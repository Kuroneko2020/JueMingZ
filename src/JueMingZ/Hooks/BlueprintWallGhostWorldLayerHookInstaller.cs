using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Hooks
{
    public static class BlueprintWallGhostWorldLayerHookInstaller
    {
        internal const string HookTargetName = "Terraria.Main.DoDraw_WallsAndBlacks";
        private const string HarmonyId = "JueMingZ.BlueprintWallGhostWorldLayer";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Blueprint wall ghost world layer hook already installed.", HookTargetName);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Blueprint wall ghost world layer hook installation is already in progress.");
            }

            try
            {
                var mainType = GameMode.FindTerrariaMainType();
                if (mainType == null)
                {
                    const string message = "Terraria.Main type not found; blueprint wall ghost world layer hook skipped.";
                    Logger.Warn("BlueprintWallGhostWorldLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindDoDrawWallsAndBlacksMethod(mainType);
                if (target == null)
                {
                    var message = "No DoDraw_WallsAndBlacks candidate found. Candidates: " +
                                  SafeBootstrapInstaller.FormatCandidateList(mainType, "DoDraw_WallsAndBlacks", "DrawWalls");
                    Logger.Warn("BlueprintWallGhostWorldLayerHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var postfixMethod = typeof(BlueprintWallGhostWorldLayerHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("BlueprintWallGhostWorldLayerHookCallbacks.Postfix");
                }

                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                Logger.Info("BlueprintWallGhostWorldLayerHookInstaller", "Blueprint wall ghost world layer hook target resolved: " + signature);
                var harmonyAssembly = HarmonyBridge.Patch(HarmonyId, target, null, postfixMethod, null);
                Logger.Info("BlueprintWallGhostWorldLayerHookInstaller", "Harmony typed bridge found / loaded: " + harmonyAssembly);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Blueprint wall ghost world layer hook installed: " + signature;
                Logger.Info("BlueprintWallGhostWorldLayerHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Blueprint wall ghost world layer hook installation failed.";
                Logger.Error("BlueprintWallGhostWorldLayerHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        internal static string GetHookTargetNameForTesting()
        {
            return HookTargetName;
        }

        private static MethodInfo FindDoDrawWallsAndBlacksMethod(Type mainType)
        {
            return mainType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, "DoDraw_WallsAndBlacks", StringComparison.Ordinal))
                .Where(method => !method.IsStatic && !method.IsAbstract && !method.ContainsGenericParameters)
                .Where(method => method.ReturnType == typeof(void))
                .Where(method => method.GetParameters().Length == 0)
                .FirstOrDefault();
        }
    }
}
