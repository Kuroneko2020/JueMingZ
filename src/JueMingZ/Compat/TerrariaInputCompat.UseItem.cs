namespace JueMingZ.Compat
{
    public static partial class TerrariaInputCompat
    {
        public static bool TrySetUseItem(object player, bool pressed)
        {
            // Controlled input write: player.controlUseItem / player.releaseUseItem.
            var ok = SetMember(player, "controlUseItem", pressed);
            if (pressed)
            {
                SetMember(player, "releaseUseItem", false);
            }

            return ok;
        }
    }
}
