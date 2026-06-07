using System;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class TravelMenuCreativeUiHookCallbacks
    {
        // CreativeUI scopes are temporary travel-menu guards. Overrides must fall
        // back to vanilla whenever the service says the scoped menu is inactive.
        private static void CreativeUiUpdatePrefix(ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.BeginScopedJourney("CreativeUI.Update", out __state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.CreativeUiUpdatePrefix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-creative-ui-update-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Travel menu CreativeUI.Update prefix failed; exception swallowed.", error);
            }
        }

        private static void CreativeUiUpdatePostfix(ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.EndScopedJourney(__state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.CreativeUiUpdatePostfix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-creative-ui-update-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Travel menu CreativeUI.Update postfix failed; exception swallowed.", error);
            }
        }

        private static void CreativeUiDrawPrefix(ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.BeginScopedJourney("CreativeUI.Draw", out __state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.CreativeUiDrawPrefix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-creative-ui-draw-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Travel menu CreativeUI.Draw prefix failed; exception swallowed.", error);
            }
        }

        private static void CreativeUiDrawPostfix(ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.EndScopedJourney(__state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.CreativeUiDrawPostfix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-creative-ui-draw-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Travel menu CreativeUI.Draw postfix failed; exception swallowed.", error);
            }
        }

        private static bool PlayerInputIgnoreMouseInterfacePrefix(ref bool __result)
        {
            try
            {
                if (!TravelMenuService.ShouldOverrideIgnoreMouseInterfaceForTravelMenu())
                {
                    return true;
                }

                __result = false;
                LogThrottle.InfoThrottled(
                    "travel-menu-ignore-mouse-interface-override-active",
                    TimeSpan.FromSeconds(3),
                    "TravelMenuCreativeUiHookCallbacks",
                    "PlayerInput.IgnoreMouseInterface forced false for scoped travel menu CreativeUI.");
                return false;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.PlayerInputIgnoreMouseInterfacePrefix", error);
                LogThrottle.WarnThrottled(
                    "travel-menu-ignore-mouse-interface-override-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "IgnoreMouseInterface override prefix failed; fallback to original getter.");
                return true;
            }
        }

        private static bool ItemsSacrificesForEachItemWithResearchProgressPrefix(Action<int> action)
        {
            try
            {
                if (!TravelMenuService.ShouldOverrideCreativeResearchForTravelMenu())
                {
                    return true;
                }

                int emittedCount;
                string message;
                if (!TravelMenuCompat.TryInvokeCreativeResearchProgressForAllItems(action, out emittedCount, out message))
                {
                    LogThrottle.WarnThrottled(
                        "travel-menu-virtual-research-foreach-fallback",
                        TimeSpan.FromSeconds(5),
                        "TravelMenuCreativeUiHookCallbacks",
                        "Virtual research list override fallback to original tracker: " + message);
                    return true;
                }

                LogThrottle.InfoThrottled(
                    "travel-menu-virtual-research-foreach-active",
                    TimeSpan.FromSeconds(3),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Virtual research list override active; emitted items=" + emittedCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
                return false;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.ItemsSacrificesForEachItemWithResearchProgressPrefix", error);
                LogThrottle.WarnThrottled(
                    "travel-menu-virtual-research-foreach-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Virtual research list override failed; fallback to original tracker.");
                return true;
            }
        }

        private static bool ItemsSacrificesTryGetSacrificeNumbersPrefix(int itemId, ref int amountWeHave, ref int amountNeededTotal, ref bool __result)
        {
            try
            {
                if (!TravelMenuService.ShouldOverrideCreativeResearchForTravelMenu())
                {
                    return true;
                }

                int cap;
                if (!TravelMenuCompat.TryGetCreativeResearchSacrificeCap(itemId, out cap) || cap <= 0)
                {
                    return true;
                }

                amountNeededTotal = cap;
                amountWeHave = cap;
                __result = true;
                return false;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.ItemsSacrificesTryGetSacrificeNumbersPrefix", error);
                LogThrottle.WarnThrottled(
                    "travel-menu-virtual-research-try-get-sacrifice-numbers-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Virtual research sacrifice-count override failed; fallback to original tracker.");
                return true;
            }
        }

        private static bool ItemsSacrificesIsFullyResearchedPrefix(int itemId, ref bool __result)
        {
            try
            {
                if (!TravelMenuService.ShouldOverrideCreativeResearchForTravelMenu())
                {
                    return true;
                }

                int cap;
                if (!TravelMenuCompat.TryGetCreativeResearchSacrificeCap(itemId, out cap) || cap <= 0)
                {
                    return true;
                }

                __result = true;
                return false;
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuCreativeUiHookCallbacks.ItemsSacrificesIsFullyResearchedPrefix", error);
                LogThrottle.WarnThrottled(
                    "travel-menu-virtual-research-is-fully-researched-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuCreativeUiHookCallbacks",
                    "Virtual research unlocked-state override failed; fallback to original tracker.");
                return true;
            }
        }
    }
}
