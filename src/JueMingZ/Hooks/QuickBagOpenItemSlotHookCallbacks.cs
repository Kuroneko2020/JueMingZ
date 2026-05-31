using System;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Diagnostics;

namespace JueMingZ.Hooks
{
    internal static class QuickBagOpenItemSlotHookCallbacks
    {
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
