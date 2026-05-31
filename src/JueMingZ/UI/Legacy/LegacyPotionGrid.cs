using System;
using System.Globalization;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Compat;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static class LegacyPotionGrid
    {
        public const int CardHeight = 68;
        public const int CellWidth = 56;
        public const int CellHeight = 56;
        public const int IconSize = 42;

        public static void DrawCandidate(object spriteBatch, LegacyUiRect rect, BuffPotionCandidate candidate, bool hovered)
        {
            if (candidate == null)
            {
                return;
            }

            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 25, 35, 68, hovered ? 210 : 170);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, hovered ? 2 : 1, hovered ? 222 : 92, hovered ? 232 : 118, hovered ? 250 : 176, hovered ? 240 : 210);
            DrawIconCell(spriteBatch, new LegacyUiRect(rect.X + 8, rect.Y + 10, LegacyUiMetrics.GridCellSize, LegacyUiMetrics.GridCellSize), candidate.ItemType, hovered, candidate.IsWhitelisted);

            var name = Shorten(candidate.ItemName, 14);
            var buff = Shorten(candidate.BuffName, 14);
            UiTextRenderer.DrawText(spriteBatch, name, rect.X + 64, rect.Y + 9, 242, 238, 218, 255, 0.82f);
            UiTextRenderer.DrawText(spriteBatch, "Buff: " + buff, rect.X + 64, rect.Y + 27, 202, 220, 242, 255, 0.76f);
            UiTextRenderer.DrawText(spriteBatch, "x" + candidate.Stack.ToString(CultureInfo.InvariantCulture) + " " + SourceText(candidate.SourceContainer) + " #" + (candidate.SourceSlot + 1).ToString(CultureInfo.InvariantCulture), rect.X + 64, rect.Y + 44, 188, 202, 226, 255, 0.72f);
            UiTextRenderer.DrawText(spriteBatch, StatusText(candidate), rect.X + 64, rect.Y + 58, candidate.IsWhitelisted ? 236 : 190, candidate.IsActive ? 190 : 224, candidate.IsWhitelisted ? 146 : 198, 255, 0.66f);
        }

        public static void DrawCandidateCell(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, BuffPotionCandidate candidate, bool hovered)
        {
            if (candidate == null)
            {
                return;
            }

            DrawPotionCell(spriteBatch, rect, clip, candidate.ItemType, hovered, false, false, candidate.IsActive);
        }

        public static void DrawWhitelistCell(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, BuffPotionWhitelistEntry entry, BuffPotionCandidate liveCandidate, bool hovered)
        {
            DrawWhitelistCell(spriteBatch, rect, clip, entry, liveCandidate, hovered, IsBuffActiveNow(entry == null ? 0 : entry.BuffType));
        }

        public static void DrawWhitelistCell(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, BuffPotionWhitelistEntry entry, BuffPotionCandidate liveCandidate, bool hovered, bool active)
        {
            if (entry == null)
            {
                return;
            }

            var missing = liveCandidate == null;
            DrawPotionCell(spriteBatch, rect, clip, entry.ItemType, hovered, false, missing, active);
        }

        public static void DrawWhitelistEntry(object spriteBatch, LegacyUiRect rect, BuffPotionWhitelistEntry entry, BuffPotionCandidate liveCandidate, bool hovered)
        {
            DrawWhitelistEntry(spriteBatch, rect, entry, liveCandidate, hovered, IsBuffActiveNow(entry == null ? 0 : entry.BuffType));
        }

        public static void DrawWhitelistEntry(object spriteBatch, LegacyUiRect rect, BuffPotionWhitelistEntry entry, BuffPotionCandidate liveCandidate, bool hovered, bool active)
        {
            if (entry == null)
            {
                return;
            }

            var stack = liveCandidate == null ? 0 : liveCandidate.Stack;
            UiPrimitiveRenderer.DrawFilledRect(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 33, 43, 74, hovered ? 218 : 178);
            UiPrimitiveRenderer.DrawRectBorder(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, hovered ? 2 : 1, hovered ? 230 : 120, hovered ? 224 : 146, hovered ? 184 : 190, hovered ? 245 : 218);
            DrawIconCell(spriteBatch, new LegacyUiRect(rect.X + 8, rect.Y + 10, LegacyUiMetrics.GridCellSize, LegacyUiMetrics.GridCellSize), entry.ItemType, hovered, false);

            UiTextRenderer.DrawText(spriteBatch, Shorten(entry.ItemName, 14), rect.X + 64, rect.Y + 9, 244, 236, 206, 255, 0.82f);
            UiTextRenderer.DrawText(spriteBatch, "Buff: " + Shorten(entry.BuffName, 14), rect.X + 64, rect.Y + 27, 206, 220, 242, 255, 0.76f);
            UiTextRenderer.DrawText(spriteBatch, "已有: " + (active ? "是" : "否") + "  背包: " + (stack > 0 ? "x" + stack.ToString(CultureInfo.InvariantCulture) : "未找到"), rect.X + 64, rect.Y + 44, 198, 210, 230, 255, 0.72f);
            UiTextRenderer.DrawText(spriteBatch, "点击移除", rect.X + 64, rect.Y + 58, 235, 212, 156, 255, 0.66f);
        }

        private static void DrawIconCell(object spriteBatch, LegacyUiRect rect, int itemType, bool hovered, bool selected)
        {
            LegacyUiTheme.DrawCell(spriteBatch, rect, hovered, selected);
            object itemTexture;
            if (VanillaUiSkinCompat.TryGetItemTexture(itemType, out itemTexture))
            {
                UiPrimitiveRenderer.DrawTextureContained(spriteBatch, itemTexture, rect.X + 6, rect.Y + 6, rect.Width - 12, rect.Height - 12, 255, 255, 255, 255);
            }
            else
            {
                UiTextRenderer.DrawCenteredText(spriteBatch, itemType.ToString(CultureInfo.InvariantCulture), rect.X + 4, rect.Y + 16, rect.Width - 8, 20, 236, 236, 218, 255, 0.68f);
            }
        }

        private static void DrawPotionCell(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int itemType, bool hovered, bool selected, bool missing, bool active)
        {
            if (!rect.Intersects(clip))
            {
                return;
            }

            LegacyUiTheme.DrawCellClipped(spriteBatch, rect, hovered, selected, missing, clip);
            var iconRect = new LegacyUiRect(rect.X + (rect.Width - IconSize) / 2, rect.Y + (rect.Height - IconSize) / 2, IconSize, IconSize);

            object itemTexture;
            if (VanillaUiSkinCompat.TryGetItemTexture(itemType, out itemTexture))
            {
                UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, itemTexture, iconRect.X + 1, iconRect.Y + 1, iconRect.Width - 2, iconRect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
            }
            else
            {
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, itemType.ToString(CultureInfo.InvariantCulture), iconRect.X, iconRect.Y + 5, iconRect.Width, 18, clip.X, clip.Y, clip.Width, clip.Height, 236, 236, 218, 255, 0.58f);
            }

            if (missing)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.Right - 15, rect.Y + 5, 10, 10, clip.X, clip.Y, clip.Width, clip.Height, 5, 236, 58, 66, 248);
            }
            else if (active)
            {
                UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.Right - 15, rect.Y + 5, 10, 10, clip.X, clip.Y, clip.Width, clip.Height, 5, 126, 226, 156, 245);
            }
        }

        private static string SourceText(string source)
        {
            if (string.Equals(source, "VoidBag", StringComparison.OrdinalIgnoreCase))
            {
                return "虚空袋";
            }

            return "背包";
        }

        private static string StatusText(BuffPotionCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            return (candidate.IsActive ? "已有Buff" : "缺Buff") + " / " + (candidate.IsWhitelisted ? "已加入" : "未加入");
        }

        private static bool IsBuffActiveNow(int buffType)
        {
            return buffType > 0 && BuffPotionDiagnostics.GetCurrentActiveBuffTypes().Contains(buffType);
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxLength - 1) + "...";
        }
    }
}
