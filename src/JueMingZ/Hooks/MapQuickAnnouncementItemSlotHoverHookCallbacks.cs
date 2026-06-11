using System;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class MapQuickAnnouncementItemSlotHoverHookCallbacks
    {
        // ItemSlot.MouseHover is the vanilla-owned hit-test point. The hook only
        // snapshots read-only item facts so quick announcement does not guess UI layouts.
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 3)
                {
                    return;
                }

                var inv = __args[0];
                var context = Convert.ToInt32(__args[1]);
                var slot = Convert.ToInt32(__args[2]);
                TerrariaUiMouseCompat.TryCaptureItemSlotHoverSnapshot(inv, context, slot);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MapQuickAnnouncementItemSlotHoverHookCallbacks.Postfix", error);
                LogThrottle.WarnThrottled(
                    "map-quick-announcement-item-slot-hover-hook-error",
                    TimeSpan.FromSeconds(10),
                    "MapQuickAnnouncementItemSlotHoverHookCallbacks",
                    "Quick announcement ItemSlot.MouseHover hook failed: " + error.Message);
            }
        }

        internal static void PostfixForTesting(object[] args)
        {
            Postfix(args);
        }
    }
}
