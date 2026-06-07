using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class TravelMenuCreativeUiHookInstaller
    {
        // Travel menu hooks patch several optional CreativeUI members. Missing
        // members disable only their scoped override, not unrelated UI behavior.
        private const string HarmonyId = "JueMingZ.TravelMenuCreativeUi.001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Travel menu CreativeUI hooks already installed.", "CreativeUI.Update / CreativeUI.Draw");
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Travel menu CreativeUI hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    return Skip("Harmony not found; travel menu CreativeUI hooks cannot install.");
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    return Skip("HarmonyMethod not found; travel menu CreativeUI hooks cannot install.");
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    return Skip("Terraria runtime types unavailable; travel menu CreativeUI hooks cannot install: " + TerrariaRuntimeTypes.LastError);
                }

                var mainType = TerrariaRuntimeTypes.MainType;
                var creativeUiType = mainType == null ? null : mainType.Assembly.GetType("Terraria.GameContent.Creative.CreativeUI", false);
                var updateMethod = creativeUiType == null ? null : FindInstanceVoidMethod(creativeUiType, "Update", "GameTime");
                var drawMethod = creativeUiType == null ? null : FindInstanceVoidMethod(creativeUiType, "Draw", "SpriteBatch");
                var itemSacrificesTrackerType = mainType == null ? null : mainType.Assembly.GetType("Terraria.GameContent.Creative.ItemsSacrificedUnlocksTracker", false);
                var itemSacrificesForEachMethod = FindInstanceMethod(itemSacrificesTrackerType, "ForEachItemWithResearchProgress", typeof(void), typeof(Action<int>));
                var itemSacrificesTryGetNumbersMethod = FindInstanceMethod(itemSacrificesTrackerType, "TryGetSacrificeNumbers", typeof(bool), typeof(int), typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
                var itemSacrificesIsFullyResearchedMethod = FindInstanceMethod(itemSacrificesTrackerType, "IsFullyResearched", typeof(bool), typeof(int));
                var playerInputType = mainType == null ? null : mainType.Assembly.GetType("Terraria.GameInput.PlayerInput", false);
                var ignoreMouseInterfaceGetter = FindStaticPropertyGetter(playerInputType, "IgnoreMouseInterface", typeof(bool));
                if (updateMethod == null || drawMethod == null)
                {
                    var message = "Travel menu CreativeUI hook targets missing. update=" + (updateMethod != null) + ", draw=" + (drawMethod != null) + ".";
                    TravelMenuService.RecordCreativeUiHook(false, message);
                    Logger.Warn("TravelMenuCreativeUiHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var callbacks = typeof(TravelMenuCreativeUiHookCallbacks);
                var updatePrefix = callbacks.GetMethod("CreativeUiUpdatePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var updatePostfix = callbacks.GetMethod("CreativeUiUpdatePostfix", BindingFlags.Static | BindingFlags.NonPublic);
                var drawPrefix = callbacks.GetMethod("CreativeUiDrawPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var drawPostfix = callbacks.GetMethod("CreativeUiDrawPostfix", BindingFlags.Static | BindingFlags.NonPublic);
                var ignoreMouseInterfacePrefix = callbacks.GetMethod("PlayerInputIgnoreMouseInterfacePrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var itemSacrificesForEachPrefix = callbacks.GetMethod("ItemsSacrificesForEachItemWithResearchProgressPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var itemSacrificesTryGetNumbersPrefix = callbacks.GetMethod("ItemsSacrificesTryGetSacrificeNumbersPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                var itemSacrificesIsFullyResearchedPrefix = callbacks.GetMethod("ItemsSacrificesIsFullyResearchedPrefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (updatePrefix == null || updatePostfix == null || drawPrefix == null || drawPostfix == null || ignoreMouseInterfacePrefix == null ||
                    itemSacrificesForEachPrefix == null || itemSacrificesTryGetNumbersPrefix == null || itemSacrificesIsFullyResearchedPrefix == null)
                {
                    throw new MissingMethodException("Travel menu CreativeUI callback methods not found.");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                SafeBootstrapInstaller.PatchWithHarmony(
                    harmonyType,
                    harmony,
                    updateMethod,
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, updatePrefix),
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, updatePostfix));
                SafeBootstrapInstaller.PatchWithHarmony(
                    harmonyType,
                    harmony,
                    drawMethod,
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, drawPrefix),
                    SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, drawPostfix));

                var virtualResearchHookSignature = "virtual research hooks unavailable";
                var virtualResearchHookInstalledCount = 0;
                if (itemSacrificesTrackerType != null)
                {
                    if (itemSacrificesForEachMethod != null)
                    {
                        SafeBootstrapInstaller.PatchWithHarmony(
                            harmonyType,
                            harmony,
                            itemSacrificesForEachMethod,
                            SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, itemSacrificesForEachPrefix),
                            null);
                        virtualResearchHookInstalledCount++;
                    }

                    if (itemSacrificesTryGetNumbersMethod != null)
                    {
                        SafeBootstrapInstaller.PatchWithHarmony(
                            harmonyType,
                            harmony,
                            itemSacrificesTryGetNumbersMethod,
                            SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, itemSacrificesTryGetNumbersPrefix),
                            null);
                        virtualResearchHookInstalledCount++;
                    }

                    if (itemSacrificesIsFullyResearchedMethod != null)
                    {
                        SafeBootstrapInstaller.PatchWithHarmony(
                            harmonyType,
                            harmony,
                            itemSacrificesIsFullyResearchedMethod,
                            SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, itemSacrificesIsFullyResearchedPrefix),
                            null);
                        virtualResearchHookInstalledCount++;
                    }

                    virtualResearchHookSignature =
                        (itemSacrificesForEachMethod == null ? "ForEachItemWithResearchProgress missing" : SafeBootstrapInstaller.FormatMethodSignature(itemSacrificesForEachMethod)) +
                        " | " +
                        (itemSacrificesTryGetNumbersMethod == null ? "TryGetSacrificeNumbers missing" : SafeBootstrapInstaller.FormatMethodSignature(itemSacrificesTryGetNumbersMethod)) +
                        " | " +
                        (itemSacrificesIsFullyResearchedMethod == null ? "IsFullyResearched missing" : SafeBootstrapInstaller.FormatMethodSignature(itemSacrificesIsFullyResearchedMethod));
                }
                else
                {
                    Logger.Warn("TravelMenuCreativeUiHookInstaller", "ItemsSacrificedUnlocksTracker type not found; virtual research hooks skipped.");
                }

                var ignoreHookInstalled = false;
                if (ignoreMouseInterfaceGetter != null)
                {
                    SafeBootstrapInstaller.PatchWithHarmony(
                        harmonyType,
                        harmony,
                        ignoreMouseInterfaceGetter,
                        SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, ignoreMouseInterfacePrefix),
                        null);
                    ignoreHookInstalled = true;
                }
                else
                {
                    Logger.Warn("TravelMenuCreativeUiHookInstaller", "PlayerInput.IgnoreMouseInterface getter not found; scoped getter override hook skipped.");
                }

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(updateMethod) +
                                " | " + SafeBootstrapInstaller.FormatMethodSignature(drawMethod) +
                                (ignoreHookInstalled
                                    ? " | " + SafeBootstrapInstaller.FormatMethodSignature(ignoreMouseInterfaceGetter)
                                    : " | PlayerInput.IgnoreMouseInterface getter unavailable") +
                                " | virtualResearch=" + virtualResearchHookInstalledCount + "/3 [" + virtualResearchHookSignature + "]";
                var successMessage = "Travel menu CreativeUI hooks installed: " + signature;
                TravelMenuService.RecordCreativeUiHook(true, successMessage);
                Logger.Info("TravelMenuCreativeUiHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                var message = "Travel menu CreativeUI hook installation failed: " + error.Message;
                TravelMenuService.RecordCreativeUiHook(false, message);
                Logger.Error("TravelMenuCreativeUiHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static HookInstallResult Skip(string message)
        {
            TravelMenuService.RecordCreativeUiHook(false, message);
            Logger.Warn("TravelMenuCreativeUiHookInstaller", message);
            return HookInstallResult.Skipped(message);
        }

        private static MethodInfo FindInstanceVoidMethod(Type type, string name, string singleParameterTypeName)
        {
            if (type == null)
            {
                return null;
            }

            return type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, name, StringComparison.Ordinal))
                .Where(method => !method.ContainsGenericParameters && !method.IsAbstract && method.ReturnType == typeof(void))
                .Where(method =>
                {
                    var parameters = method.GetParameters();
                    return parameters.Length == 1 &&
                           string.Equals(parameters[0].ParameterType.Name, singleParameterTypeName, StringComparison.Ordinal);
                })
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private static MethodInfo FindStaticPropertyGetter(Type type, string propertyName, Type returnType)
        {
            if (type == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (property == null)
            {
                return null;
            }

            if (returnType != null && property.PropertyType != returnType)
            {
                return null;
            }

            return property.GetGetMethod(true);
        }

        private static MethodInfo FindInstanceMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var expected = parameterTypes ?? Type.EmptyTypes;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal) ||
                    method.ContainsGenericParameters ||
                    method.IsAbstract)
                {
                    continue;
                }

                if (returnType != null && method.ReturnType != returnType)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != expected.Length)
                {
                    continue;
                }

                var allMatch = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != expected[i])
                    {
                        allMatch = false;
                        break;
                    }
                }

                if (allMatch)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
