namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static TerrariaUiInputContext ReadUiInputContext(object player)
        {
            var context = new TerrariaUiInputContext();
            var mainType = TerrariaRuntimeTypes.MainType;
            if (mainType == null)
            {
                context.MainTypeUnavailable = true;
                return context;
            }

            bool value;
            context.GameMenu = TryGetStaticBool(mainType, "gameMenu", out value) && value;
            context.ChatOpen = (TryGetStaticBool(mainType, "chatMode", out value) && value) ||
                               (TryGetStaticBool(mainType, "drawingPlayerChat", out value) && value);

            var npcChatText = GetStatic(mainType, "npcChatText");
            context.NpcChatOpen = npcChatText != null && !string.IsNullOrEmpty(npcChatText.ToString());
            context.PlayerInventoryOpen = TryGetStaticBool(mainType, "playerInventory", out value) && value;

            int chest;
            context.ChestOpen = TryGetInt(player, "chest", out chest) && chest >= 0;
            context.PlayerMouseInterface = TryGetBool(player, "mouseInterface", out value) && value;
            context.MainMouseInterface = TryGetStaticBool(mainType, "mouseInterface", out value) && value;
            context.MainBlockMouse = TryGetStaticBool(mainType, "blockMouse", out value) && value;
            return context;
        }

        public static bool IsMouseInputCapturedByUi(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            reason = context.MouseCaptureReason;
            return context.MouseCapturedByUi;
        }

        public static bool IsInputBlockingUiActive(object player, out string reason)
        {
            var context = ReadUiInputContext(player);
            if (context.MainTypeUnavailable)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            if (context.GameMenu)
            {
                reason = "gameMenu";
                return true;
            }

            if (context.MouseCapturedByUi)
            {
                reason = context.MouseCaptureReason;
                return true;
            }

            reason = string.Empty;
            return false;
        }

        public static bool IsWorldRightClickInteractionActive(object player, out string reason)
        {
            var mainType = TerrariaRuntimeTypes.MainType ?? FindType("Terraria.Main");
            if (mainType == null)
            {
                reason = "mainTypeUnavailable";
                return true;
            }

            bool value;
            if (TryGetStaticBool(mainType, "SmartInteractShowingGenuine", out value) && value)
            {
                reason = "smartInteractGenuine";
                return true;
            }

            int target;
            if ((TryGetStaticInt(mainType, "SmartInteractNPC", out target) && target != -1) ||
                (TryGetStaticInt(mainType, "SmartInteractProj", out target) && target != -1))
            {
                reason = "smartInteractTarget";
                return true;
            }

            if (TryGetBool(player, "tileInteractionHappened", out value) && value)
            {
                reason = "tileInteractionHappened";
                return true;
            }

            reason = string.Empty;
            return false;
        }
    }
}
