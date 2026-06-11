using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingZ.Bootstrap;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    public static class MapQuickAnnouncementItemSlotHoverHookInstaller
    {
        private const string HarmonyId = "JueMingZ.MapQuickAnnouncement.ItemSlotMouseHover.0001";
        private static int _installed;
        private static int _installing;

        public static HookInstallResult Install()
        {
            if (_installed == 1)
            {
                return HookInstallResult.Success("Quick announcement ItemSlot.MouseHover hook already installed.", string.Empty);
            }

            if (Interlocked.Exchange(ref _installing, 1) == 1)
            {
                return HookInstallResult.Skipped("Quick announcement ItemSlot.MouseHover hook installation is already in progress.");
            }

            try
            {
                Assembly harmonyAssembly;
                DependencyResolver.TryLoadAssemblyBySimpleName("0Harmony", out harmonyAssembly);

                var harmonyType = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony");
                if (harmonyType == null)
                {
                    const string message = "Harmony not found; quick announcement ItemSlot.MouseHover hook cannot install.";
                    Logger.Warn("MapQuickAnnouncementItemSlotHoverHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var harmonyMethodType = DependencyChecker.FindType("HarmonyLib.HarmonyMethod", "0Harmony") ??
                                        harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", false);
                if (harmonyMethodType == null)
                {
                    const string message = "HarmonyMethod not found; quick announcement ItemSlot.MouseHover hook cannot install.";
                    Logger.Warn("MapQuickAnnouncementItemSlotHoverHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                if (!TerrariaRuntimeTypes.EnsureInitializedLateOnly())
                {
                    var message = "Terraria runtime types unavailable; quick announcement ItemSlot.MouseHover hook cannot install: " + TerrariaRuntimeTypes.LastError;
                    Logger.Warn("MapQuickAnnouncementItemSlotHoverHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var itemSlotType = TerrariaTypeCache.Find("Terraria.UI.ItemSlot");
                if (itemSlotType == null)
                {
                    const string message = "Terraria.UI.ItemSlot type not found; quick announcement ItemSlot.MouseHover hook cannot install.";
                    Logger.Warn("MapQuickAnnouncementItemSlotHoverHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var target = FindMouseHoverMethod(itemSlotType);
                if (target == null)
                {
                    var message = "No Terraria.UI.ItemSlot.MouseHover(Item[],int,int) hook candidate found. Candidates: " + FormatMouseHoverCandidates(itemSlotType);
                    Logger.Warn("MapQuickAnnouncementItemSlotHoverHookInstaller", message);
                    return HookInstallResult.Skipped(message);
                }

                var postfixMethod = typeof(MapQuickAnnouncementItemSlotHoverHookCallbacks).GetMethod("Postfix", BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod == null)
                {
                    throw new MissingMethodException("MapQuickAnnouncementItemSlotHoverHookCallbacks.Postfix");
                }

                var harmony = SafeBootstrapInstaller.CreateHarmonyInstance(harmonyType, HarmonyId);
                var postfix = SafeBootstrapInstaller.CreateHarmonyMethod(harmonyMethodType, postfixMethod);
                SafeBootstrapInstaller.PatchWithHarmony(harmonyType, harmony, target, postfix);

                Interlocked.Exchange(ref _installed, 1);
                var signature = SafeBootstrapInstaller.FormatMethodSignature(target);
                var successMessage = "Quick announcement ItemSlot.MouseHover hook installed: " + signature;
                Logger.Info("MapQuickAnnouncementItemSlotHoverHookInstaller", successMessage);
                return HookInstallResult.Success(successMessage, signature);
            }
            catch (Exception error)
            {
                const string message = "Quick announcement ItemSlot.MouseHover hook installation failed.";
                Logger.Error("MapQuickAnnouncementItemSlotHoverHookInstaller", message, error);
                return HookInstallResult.Failed(message, error);
            }
            finally
            {
                Interlocked.Exchange(ref _installing, 0);
            }
        }

        private static MethodInfo FindMouseHoverMethod(Type itemSlotType)
        {
            return itemSlotType
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(method => string.Equals(method.Name, "MouseHover", StringComparison.Ordinal))
                .Where(IsInventoryMouseHoverCandidate)
                .OrderBy(method => method.IsPublic ? 0 : 1)
                .FirstOrDefault();
        }

        private static bool IsInventoryMouseHoverCandidate(MethodInfo method)
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

        private static string FormatMouseHoverCandidates(Type itemSlotType)
        {
            try
            {
                var candidates = itemSlotType
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(method => method.Name.IndexOf("MouseHover", StringComparison.OrdinalIgnoreCase) >= 0)
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
