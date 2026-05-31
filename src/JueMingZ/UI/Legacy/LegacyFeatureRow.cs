namespace JueMingZ.UI.Legacy
{
    public static class LegacyFeatureRow
    {
        public static void DrawLabel(object spriteBatch, LegacyUiRect row, string label, string value)
        {
            LegacyUiTheme.DrawRow(spriteBatch, row);
            var labelWidth = string.IsNullOrWhiteSpace(value) ? row.Width - 20 : 128;
            UiTextRenderer.DrawAlignedText(spriteBatch, label ?? string.Empty, row.X + 10, row.Y, labelWidth, row.Height, UiTextHorizontalAlignment.Left, 238, 238, 226, 255, 0.9f);
            if (!string.IsNullOrWhiteSpace(value))
            {
                UiTextRenderer.DrawAlignedText(spriteBatch, value, row.X + 148, row.Y, row.Width - 158, row.Height, UiTextHorizontalAlignment.Left, 206, 218, 238, 255, 0.85f);
            }
        }
    }
}
