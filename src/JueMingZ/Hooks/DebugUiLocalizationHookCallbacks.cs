using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class DebugUiLocalizationHookCallbacks
    {
        // Debug UI callbacks may localize reflected UI objects in place, but any
        // missing shape falls back to vanilla so world generation UI stays usable.
        private static void WorldGenDebugUpdatePostfix(object __instance)
        {
            try
            {
                DebugUiLocalizationCompat.LocalizeWorldGenUi(__instance);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("DebugUiLocalizationHookCallbacks.WorldGenDebugUpdatePostfix", error);
                LogThrottle.ErrorThrottled(
                    "debug-ui-localization-worldgen-update-failed",
                    TimeSpan.FromSeconds(10),
                    "DebugUiLocalizationHookCallbacks",
                    "WorldGen Debug localization postfix failed; exception swallowed.", error);
            }
        }

        private static void WorldGenTooltipDrawPrefix(object __instance)
        {
            try
            {
                DebugUiLocalizationCompat.EnsureWorldGenTooltipDelegatesWrapped(__instance);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("DebugUiLocalizationHookCallbacks.WorldGenTooltipDrawPrefix", error);
                LogThrottle.ErrorThrottled(
                    "debug-ui-localization-tooltip-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "DebugUiLocalizationHookCallbacks",
                    "WorldGen tooltip localization prefix failed; exception swallowed.", error);
            }
        }

        private static void DebugCommandsListBuildPagePostfix(object __instance)
        {
            try
            {
                DebugUiLocalizationCompat.LocalizeDebugCommandsListUi(__instance);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("DebugUiLocalizationHookCallbacks.DebugCommandsListBuildPagePostfix", error);
                LogThrottle.ErrorThrottled(
                    "debug-ui-localization-debug-commands-page-failed",
                    TimeSpan.FromSeconds(10),
                    "DebugUiLocalizationHookCallbacks",
                    "Debug Commands list localization postfix failed; exception swallowed.", error);
            }
        }
    }
}
