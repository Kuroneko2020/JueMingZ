using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class DebugUiLocalizationHookInstaller
    {
        // These hooks target optional debug UI types. If any required type or
        // method is absent, localization is skipped rather than patched by name only.
        private const string HarmonyId = "JueMingZ.DebugUiLocalization.0297";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Debug UI localization hooks already installed.", "UIWorldGenDebug.Update / TooltipElement.DrawSelf / UIDebugCommandsList.BuildPage");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Debug UI localization hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; Debug UI localization hooks cannot install.";
                    Logger.Warn("DebugUiLocalizationHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; Debug UI localization hooks cannot install.";
                    Logger.Warn("DebugUiLocalizationHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var worldGenDebugType = FindType("Terraria.GameContent.UI.States.UIWorldGenDebug");
                var tooltipType = FindType("Terraria.GameContent.UI.States.UIWorldGenDebug+TooltipElement");
                var debugCommandsListType = FindType("Terraria.GameContent.UI.States.UIDebugCommandsList");
                if (worldGenDebugType == null || tooltipType == null || debugCommandsListType == null)
                {
                    const string message = "WorldGen Debug UI types not found; Debug UI localization hooks skipped.";
                    Logger.Warn("DebugUiLocalizationHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var worldGenUpdate = worldGenDebugType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(method => string.Equals(method.Name, "Update", StringComparison.Ordinal) &&
                                              method.GetParameters().Length == 1);
                var tooltipDrawSelf = tooltipType.GetMethod("DrawSelf", BindingFlags.Instance | BindingFlags.NonPublic);
                var debugCommandsBuildPage = debugCommandsListType.GetMethod("BuildPage", BindingFlags.Instance | BindingFlags.NonPublic);

                if (worldGenUpdate == null || tooltipDrawSelf == null || debugCommandsBuildPage == null)
                {
                    var message = "Debug UI localization targets missing. worldGenUpdate=" + (worldGenUpdate != null) +
                                  ", tooltipDrawSelf=" + (tooltipDrawSelf != null) +
                                  ", debugCommandsBuildPage=" + (debugCommandsBuildPage != null) + ".";
                    Logger.Warn("DebugUiLocalizationHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var callbacksType = typeof(DebugUiLocalizationHookCallbacks);
                var worldGenUpdatePostfixMethod = callbacksType.GetMethod("WorldGenDebugUpdatePostfix", BindingFlags.Static | BindingFlags.NonPublic);
                var tooltipDrawPrefixMethod = callbacksType.GetMethod("WorldGenTooltipDrawPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var debugCommandsBuildPagePostfixMethod = callbacksType.GetMethod("DebugCommandsListBuildPagePostfix", BindingFlags.Static | BindingFlags.NonPublic);

                if (worldGenUpdatePostfixMethod == null || tooltipDrawPrefixMethod == null || debugCommandsBuildPagePostfixMethod == null)
                {
                    throw new MissingMethodException("DebugUiLocalizationHookCallbacks methods not found.");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);

                var worldGenUpdatePostfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, worldGenUpdatePostfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, worldGenUpdate, null, worldGenUpdatePostfix);

                var tooltipDrawPrefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, tooltipDrawPrefixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, tooltipDrawSelf, tooltipDrawPrefix, null);

                var debugCommandsBuildPagePostfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, debugCommandsBuildPagePostfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, debugCommandsBuildPage, null, debugCommandsBuildPagePostfix);

                Interlocked.Exchange(ref _installed, 1);
                var successMessage = "Debug UI localization hooks installed: " +
                                     SafeBootstrapInstaller.FormatMethodSignature(worldGenUpdate) + " | " +
                                     SafeBootstrapInstaller.FormatMethodSignature(tooltipDrawSelf) + " | " +
                                     SafeBootstrapInstaller.FormatMethodSignature(debugCommandsBuildPage);
                Logger.Info("DebugUiLocalizationHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, "DebugUiLocalization");
            }
            catch (Exception error)
            {
                const string message = "Debug UI localization hook installation failed.";
                Logger.Error("DebugUiLocalizationHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

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
