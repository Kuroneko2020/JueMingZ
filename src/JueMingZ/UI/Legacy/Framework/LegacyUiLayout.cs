using System;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.UI.Legacy.Framework
{
    public static class LegacyUiLayout
    {
        public const int RowModeButtonHeight = 24;
        public const int RowLabelTextHeight = 22;

        public static int RowModeButtonY(LegacyUiRect row)
        {
            return row.Y + Math.Max(0, (row.Height - RowModeButtonHeight) / 2);
        }

        public static int RowLabelY(LegacyUiRect row)
        {
            return row.Y + Math.Max(0, (row.Height - RowLabelTextHeight) / 2);
        }

        public static int ModeButtonWidth(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return 64;
            }

            return Math.Max(64, Math.Min(190, UiTextRenderer.EstimateTextWidth(label, LegacyUiMetrics.RowButtonTextScale) + 22));
        }

        public static int TotalModeButtonWidth(string[] labels, int gap)
        {
            if (labels == null || labels.Length <= 0)
            {
                return 0;
            }

            var total = 0;
            for (var index = 0; index < labels.Length; index++)
            {
                total += ModeButtonWidth(labels[index]);
                if (index > 0)
                {
                    total += gap;
                }
            }

            return total;
        }

        public static LegacyUiRect StackItem(LegacyUiRect column, int index, int itemHeight, int gap)
        {
            return new LegacyUiRect(column.X, column.Y + index * (itemHeight + gap), column.Width, itemHeight);
        }

        public static LegacyUiRect RowItem(LegacyUiRect row, int index, int itemWidth, int gap)
        {
            return new LegacyUiRect(row.X + index * (itemWidth + gap), row.Y, itemWidth, row.Height);
        }

        public static void SplitLeftRight(LegacyUiRect rect, int rightWidth, int gap, out LegacyUiRect left, out LegacyUiRect right)
        {
            rightWidth = Math.Max(0, Math.Min(rect.Width, rightWidth));
            right = new LegacyUiRect(rect.Right - rightWidth, rect.Y, rightWidth, rect.Height);
            left = new LegacyUiRect(rect.X, rect.Y, Math.Max(0, rect.Width - rightWidth - Math.Max(0, gap)), rect.Height);
        }

        public static LegacyUiRect EqualWidthButton(LegacyUiRect row, int index, int count, int gap)
        {
            if (count <= 0)
            {
                return new LegacyUiRect(row.X, row.Y, 0, row.Height);
            }

            var totalGap = Math.Max(0, count - 1) * Math.Max(0, gap);
            var width = Math.Max(1, (row.Width - totalGap) / count);
            return new LegacyUiRect(row.X + index * (width + gap), row.Y, width, row.Height);
        }
    }
}
