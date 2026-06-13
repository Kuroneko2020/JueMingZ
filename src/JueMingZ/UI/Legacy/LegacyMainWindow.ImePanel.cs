namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static bool TryAttachLegacyTextInputImePanel(string inputId, LegacyUiRect inputRect, LegacyUiRect clip)
        {
            LegacyUiRect anchor;
            if (!TryResolveLegacyTextInputImeAnchor(inputRect, clip, out anchor))
            {
                return false;
            }

            return LegacyTextInput.TryAttachImeCompositionPanel(inputId, anchor);
        }

        private static bool TryResolveLegacyTextInputImeAnchor(LegacyUiRect inputRect, LegacyUiRect clip, out LegacyUiRect anchor)
        {
            anchor = new LegacyUiRect();
            if (inputRect.Width <= 0 || inputRect.Height <= 0)
            {
                return false;
            }

            if (clip.Width <= 0 || clip.Height <= 0)
            {
                anchor = inputRect;
                return true;
            }

            var visible = inputRect.Intersect(clip);
            if (visible.Width <= 0 || visible.Height <= 0)
            {
                return false;
            }

            // All LegacyTextInput users feed candidate-panel anchors through this
            // shared draw-layer helper so page files never need IME-specific math.
            anchor = visible;
            return true;
        }

        internal static bool TryAttachLegacyTextInputImePanelForTesting(string inputId, LegacyUiRect inputRect, LegacyUiRect clip)
        {
            return TryAttachLegacyTextInputImePanel(inputId, inputRect, clip);
        }
    }
}
