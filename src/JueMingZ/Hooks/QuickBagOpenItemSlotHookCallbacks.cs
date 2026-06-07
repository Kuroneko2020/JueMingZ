using System;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class QuickBagOpenItemSlotHookCallbacks
    {
        // ItemSlot.RightClick prefix only recognizes the vanilla UI gesture for
        // quick bag opening; inventory mutation remains in the service/compat path.
        private static void Prefix(object[] __args)
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
                QuickBagOpenService.HandleItemSlotRightClickPrefix(inv, context, slot);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("QuickBagOpenItemSlotHookCallbacks.Prefix", error);
                LogThrottle.WarnThrottled(
                    "quick-bag-open-item-slot-hook-error",
                    TimeSpan.FromSeconds(10),
                    "QuickBagOpenItemSlotHookCallbacks",
                    "Quick bag open ItemSlot.RightClick hook failed: " + error.Message);
            }
        }
    }
}
