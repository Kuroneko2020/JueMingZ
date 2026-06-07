using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class QuickBagOpenItemSlotHookInstaller
    {
        // ItemSlot reflection must match the right-click helper exactly. If the
        // signature changes, skip so inventory UI behavior stays vanilla.
        private const string HarmonyId = "JueMingZ.QuickBagOpen.ItemSlotRightClick.0001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Quick bag ItemSlot.RightClick hook already installed.", string.Empty);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Quick bag ItemSlot.RightClick hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; quick bag ItemSlot.RightClick hook cannot install.";
                    Logger.Warn("QuickBagOpenItemSlotHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; quick bag ItemSlot.RightClick hook cannot install.";
                    Logger.Warn("QuickBagOpenItemSlotHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; quick bag ItemSlot.RightClick hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    Logger.Warn("QuickBagOpenItemSlotHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var itemSlotType = TerrariaTypeCache.Find("Terraria.UI.ItemSlot");
                if (itemSlotType == null)
                {
                    const string message = "Terraria.UI.ItemSlot type not found; quick bag ItemSlot.RightClick hook cannot install.";
                    Logger.Warn("QuickBagOpenItemSlotHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindRightClickMethod(itemSlotType);
                if (target == null)
                {
                    var message = "No Terraria.UI.ItemSlot.RightClick(Item[],int,int) hook candidate found. Candidates: " + FormatRightClickCandidates(itemSlotType);
                    Logger.Warn("QuickBagOpenItemSlotHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var prefixMethod = typeof(QuickBagOpenItemSlotHookCallbacks).GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod == null)
                {
                    throw new MissingMethodException("QuickBagOpenItemSlotHookCallbacks.Prefix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var prefix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, prefixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, prefix, null);

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Quick bag ItemSlot.RightClick hook installed: " + signature;
                Logger.Info("QuickBagOpenItemSlotHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Quick bag ItemSlot.RightClick hook installation failed.";
                Logger.Error("QuickBagOpenItemSlotHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindRightClickMethod(Type itemSlotType)
        {
            return itemSlotType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "RightClick", StringComparison.Ordinal))
                .Where(IsInventoryRightClickCandidate)
                .OrderBy(method => method.IsPublic ? 0 : 1)
                .FirstOrDefault();
        }

        private static bool IsInventoryRightClickCandidate(MethodInfo method)
        {
            if (method == null || !method.IsStatic || method.IsAbstract || method.IsSpecialName || method.ContainsGenericParameters)
            {
                return false;
            }

            if (method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 3 &&
                   parameters[0].ParameterType.IsArray &&
                   parameters[1].ParameterType == typeof(int) &&
                   parameters[2].ParameterType == typeof(int);
        }

        private static string FormatRightClickCandidates(Type itemSlotType)
        {
            try
            {
                var candidates = itemSlotType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method => method.Name.IndexOf("RightClick", StringComparison.OrdinalIgnoreCase) >= 0)
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
