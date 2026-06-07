using System;
using System.Reflection;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class TravelMenuScopedJourneyHookCallbacks
    {
        // Scoped journey prefixes/postfixes only bracket vanilla travel-menu calls;
        // every BeginScopedJourney must be ended with the captured state.
        private static void ScopedJourneyPrefix(MethodBase __originalMethod, ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.BeginScopedJourney(FormatScope(__originalMethod), out __state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuScopedJourneyHookCallbacks.ScopedJourneyPrefix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-scoped-journey-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuScopedJourneyHookCallbacks",
                    "Travel menu scoped journey prefix failed; exception swallowed.", error);
            }
        }

        private static void ScopedJourneyPostfix(MethodBase __originalMethod, ref TravelMenuScopedJourneyState __state)
        {
            try
            {
                TravelMenuService.EndScopedJourney(__state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuScopedJourneyHookCallbacks.ScopedJourneyPostfix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-scoped-journey-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuScopedJourneyHookCallbacks",
                    "Travel menu scoped journey postfix failed; exception swallowed.", error);
            }
        }

        private static string FormatScope(MethodBase method)
        {
            if (method == null)
            {
                return "unknown";
            }

            var declaringType = method.DeclaringType == null ? string.Empty : method.DeclaringType.FullName;
            return declaringType + "." + method.Name;
        }
    }
}
