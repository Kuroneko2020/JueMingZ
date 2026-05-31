using System;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class TravelMenuSaveGuardHookCallbacks
    {
        private static void PlayerSavePrefix(object[] __args, ref TravelMenuSaveGuardState __state)
        {
            try
            {
                TravelMenuService.BeginSaveGuard("Player.SavePlayer", out __state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuSaveGuardHookCallbacks.PlayerSavePrefix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-save-player-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuSaveGuardHookCallbacks",
                    "Travel menu player save guard prefix failed; exception swallowed.", error);
            }
        }

        private static void PlayerSavePostfix(object[] __args, ref TravelMenuSaveGuardState __state)
        {
            try
            {
                TravelMenuService.EndSaveGuard(__state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuSaveGuardHookCallbacks.PlayerSavePostfix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-save-player-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuSaveGuardHookCallbacks",
                    "Travel menu player save guard postfix failed; exception swallowed.", error);
            }
        }

        private static void WorldSavePrefix(object[] __args, ref TravelMenuSaveGuardState __state)
        {
            try
            {
                TravelMenuService.BeginSaveGuard("WorldFile.SaveWorld", out __state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuSaveGuardHookCallbacks.WorldSavePrefix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-save-world-prefix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuSaveGuardHookCallbacks",
                    "Travel menu world save guard prefix failed; exception swallowed.", error);
            }
        }

        private static void WorldSavePostfix(object[] __args, ref TravelMenuSaveGuardState __state)
        {
            try
            {
                TravelMenuService.EndSaveGuard(__state);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("TravelMenuSaveGuardHookCallbacks.WorldSavePostfix", error);
                LogThrottle.ErrorThrottled(
                    "travel-menu-save-world-postfix-failed",
                    TimeSpan.FromSeconds(10),
                    "TravelMenuSaveGuardHookCallbacks",
                    "Travel menu world save guard postfix failed; exception swallowed.", error);
            }
        }
    }
}
