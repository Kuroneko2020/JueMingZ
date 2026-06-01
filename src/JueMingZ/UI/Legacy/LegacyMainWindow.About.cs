using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Runtime;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const string AboutWechatResource = "JueMingZ.Assets.About.wechat.png";
        private const string AboutAlipayResource = "JueMingZ.Assets.About.alipay.jpg";
        internal const string AboutFeedbackGroupNumber = "915753352";

        private static LegacyUiElement DrawAboutPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var baseClip = area.Viewport;
            var clip = new LegacyUiRect(
                baseClip.X + (LegacyUiMetrics.ScrollbarWidth + 8) / 2,
                baseClip.Y,
                baseClip.Width,
                baseClip.Height);
            var availableHeight = Math.Max(1, area.Viewport.Height);
            var canvas = new LegacyUiRect(clip.X, area.ToScreenY(0), clip.Width, availableHeight);
            DrawAboutBackdrop(spriteBatch, canvas, clip);

            var y = 0;
            var headerHeight = 78;
            var outerGap = 8;
            var footerHeight = 54;
            var columnHeight = Math.Max(360, availableHeight - headerHeight - outerGap - footerHeight - outerGap);
            var header = new LegacyUiRect(clip.X, area.ToScreenY(y), clip.Width, headerHeight);
            DrawAboutHeader(spriteBatch, header, clip);
            y += header.Height + outerGap;

            var gap = 14;
            var leftWidth = Math.Min(258, Math.Max(220, (clip.Width - gap) / 2));
            var rightWidth = Math.Max(1, clip.Width - leftWidth - gap);
            var left = new LegacyUiRect(clip.X, area.ToScreenY(y), leftWidth, columnHeight);
            var rightX = left.Right + gap;
            DrawAboutPortraitPlaceholder(spriteBatch, left, clip);

            var infoHeight = Math.Min(178, Math.Max(164, columnHeight * 40 / 100));
            var feedbackHeight = 78;
            var cardGap = 8;
            var infoCard = new LegacyUiRect(rightX, area.ToScreenY(y), rightWidth, infoHeight);
            DrawAboutInfoCard(spriteBatch, infoCard, clip);

            LegacyUiElement hovered = null;
            var feedbackCard = new LegacyUiRect(rightX, infoCard.Bottom + cardGap, rightWidth, feedbackHeight);
            hovered = DrawAboutFeedbackCard(spriteBatch, feedbackCard, clip, mouse, elements) ?? hovered;

            var qrCard = new LegacyUiRect(rightX, feedbackCard.Bottom + cardGap, rightWidth, columnHeight - infoCard.Height - feedbackCard.Height - cardGap * 2);
            DrawAboutQrCard(spriteBatch, qrCard, clip);
            y += columnHeight + outerGap;

            var footer = new LegacyUiRect(clip.X, area.ToScreenY(y), clip.Width, footerHeight);
            DrawAboutFooter(spriteBatch, footer, clip);
            return hovered;
        }

        private static int CalculateAboutContentHeight(LegacyUiRect contentRect)
        {
            return Math.Max(1, contentRect.Height - LegacyUiMetrics.ContentPadding * 2);
        }

        private static void DrawAboutBackdrop(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, 8, palette.ContentR, palette.ContentG, palette.ContentB, Math.Min(218, palette.ContentA));
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 6, palette.RowR, palette.RowG, palette.RowB, 76);
            DrawAboutDottedLine(spriteBatch, rect.X + 18, rect.Y + 20, rect.Width - 36, clip, palette.BorderR, palette.BorderG, palette.BorderB, 54);
            DrawAboutDottedLine(spriteBatch, rect.X + 18, rect.Bottom - 20, rect.Width - 36, clip, palette.BorderR, palette.BorderG, palette.BorderB, 54);
            DrawAboutTinyStars(spriteBatch, rect.Inset(14), clip, 28, 64);
            DrawAboutSparkle(spriteBatch, rect.X + rect.Width / 5, rect.Y + 34, clip, 2, palette.BorderR, palette.BorderG, palette.BorderB, 104);
            DrawAboutSparkle(spriteBatch, rect.Right - rect.Width / 5, rect.Bottom - 34, clip, 2, palette.BorderR, palette.BorderG, palette.BorderB, 104);
        }

        private static void DrawAboutHeader(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 238);
            DrawAboutCorner(spriteBatch, rect, clip, 14);
            DrawAboutTinyStars(spriteBatch, rect.Inset(12), clip, 14, 96);
            DrawAboutSparkle(spriteBatch, rect.X + 82, rect.Y + 28, clip, 3, 255, 226, 148, 220);
            DrawAboutSparkle(spriteBatch, rect.Right - 86, rect.Y + 30, clip, 3, 255, 226, 148, 220);
            DrawAboutDottedLine(spriteBatch, rect.X + 26, rect.CenterY + 2, 128, clip, palette.BorderR, palette.BorderG, palette.BorderB, 112);
            DrawAboutDottedLine(spriteBatch, rect.Right - 154, rect.CenterY + 2, 128, clip, palette.BorderR, palette.BorderG, palette.BorderB, 112);
            DrawAboutPixelLogo(spriteBatch, rect, clip);
        }

        private static void DrawAboutPortraitPlaceholder(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 232);
            DrawAboutCorner(spriteBatch, rect, clip, 14);
            var inner = rect.Inset(12);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, inner.X, inner.Y, inner.Width, inner.Height, clip.X, clip.Y, clip.Width, clip.Height, 6, palette.PanelR, palette.PanelG, palette.PanelB, 104);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, inner.X + 6, inner.Y + 6, inner.Width - 12, inner.Height - 12, 1, clip.X, clip.Y, clip.Width, clip.Height, palette.BorderR, palette.BorderG, palette.BorderB, 94);
            DrawAboutTinyStars(spriteBatch, inner.Inset(10), clip, 18, 78);
            DrawAboutDottedLine(spriteBatch, inner.X + 22, inner.Y + 24, inner.Width - 44, clip, palette.BorderR, palette.BorderG, palette.BorderB, 82);
            DrawAboutDottedLine(spriteBatch, inner.X + 22, inner.Bottom - 24, inner.Width - 44, clip, palette.BorderR, palette.BorderG, palette.BorderB, 82);

            var mainLabel = "广 告 位 招 租";
            var subLabel = "(还没想好这块咋做)";
            var labelWidth = inner.Width - 24;
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                mainLabel,
                inner.X + 12,
                inner.CenterY - 26,
                labelWidth,
                22,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                214,
                224,
                238,
                166,
                FitAboutTextScale(mainLabel, labelWidth, 0.80f, 0.58f));
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                subLabel,
                inner.X + 12,
                inner.CenterY,
                labelWidth,
                22,
                clip.X,
                clip.Y,
                clip.Width,
                clip.Height,
                188,
                202,
                226,
                148,
                FitAboutTextScale(subLabel, labelWidth, 0.62f, 0.46f));
            DrawAboutDottedLine(spriteBatch, inner.X + 26, inner.CenterY + 34, inner.Width - 52, clip, palette.BorderR, palette.BorderG, palette.BorderB, 74);
            DrawAboutSparkle(spriteBatch, inner.X + 34, inner.Y + 34, clip, 2, palette.BorderR, palette.BorderG, palette.BorderB, 126);
            DrawAboutSparkle(spriteBatch, inner.Right - 34, inner.Bottom - 34, clip, 2, palette.BorderR, palette.BorderG, palette.BorderB, 126);
        }

        private static void DrawAboutInfoCard(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 228);

            var x = rect.X + 18;
            var y = rect.Y + 10;
            var width = rect.Width - 36;
            var version = JueMingZRuntime.Version;
            DrawAboutInlineIcon(spriteBatch, new LegacyUiRect(x, y + 1, 20, 20), clip, "info", 230);
            UiTextRenderer.DrawTextClipped(spriteBatch, version, x + 28, y + 1, width - 28, 19, clip.X, clip.Y, clip.Width, clip.Height, 255, 238, 190, 255, FitAboutTextScale(version, width - 28, 0.60f, 0.43f));
            y += 26;
            DrawAboutDottedLine(spriteBatch, x, y - 5, width, clip, palette.BorderR, palette.BorderG, palette.BorderB, 104);

            var textAreaTop = y + 4;
            var textAreaBottom = rect.Bottom - 12;
            var lineHeight = 23;
            var blankGap = 12;
            var textBlockHeight = lineHeight * 4 + blankGap;
            var textY = textAreaTop + Math.Max(0, (textAreaBottom - textAreaTop - textBlockHeight) / 2);
            DrawAboutCenteredTextLine(spriteBatch, clip, "为 Terraria 玩家打造的轻量辅助工具。", x, textY, width, 0.74f, 238, 238, 226, 248);
            textY += lineHeight;
            DrawAboutCenteredTextLine(spriteBatch, clip, "追求稳定、简洁，并尽量保留原版体验。", x, textY, width, 0.72f, 222, 230, 240, 240);
            textY += lineHeight + blankGap;
            DrawAboutCenteredTextLine(spriteBatch, clip, "项目仍在持续打磨中。", x, textY, width, 0.72f, 222, 230, 240, 240);
            textY += lineHeight;
            DrawAboutCenteredTextLine(spriteBatch, clip, "欢迎反馈问题与建议，一起把它做得更好。", x, textY, width, 0.68f, 210, 222, 238, 236);
        }

        private static LegacyUiElement DrawAboutFeedbackCard(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 224);
            DrawAboutCenteredTitle(spriteBatch, rect, clip, "QQ 问题反馈群", "message", rect.Y + 8, 0.70f);
            DrawAboutDottedLine(spriteBatch, rect.X + 34, rect.Y + 31, rect.Width - 68, clip, palette.BorderR, palette.BorderG, palette.BorderB, 96);
            var numberRect = new LegacyUiRect(rect.X + 28, rect.Y + 42, rect.Width - 56, 26);
            var hovered = IsFrameElementHovered("about-copy-feedback-group", numberRect, mouse);
            var pressed = hovered && mouse.LeftDown;
            var fillAlpha = hovered ? 158 : 122;
            var borderAlpha = hovered ? 214 : 152;
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, numberRect.X, numberRect.Y, numberRect.Width, numberRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 6, palette.PanelR, palette.PanelG, palette.PanelB, fillAlpha);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, numberRect.X + 1, numberRect.Y + 1, numberRect.Width - 2, numberRect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 5, palette.RowR, palette.RowG, palette.RowB, pressed ? 96 : 68);
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, numberRect.X + 1, numberRect.Y + 1, numberRect.Width - 2, numberRect.Height - 2, 1, clip.X, clip.Y, clip.Width, clip.Height, palette.BorderR, palette.BorderG, palette.BorderB, borderAlpha);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, numberRect.X + 8, numberRect.Bottom - 5, numberRect.Width - 16, 1, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, hovered ? 58 : 36);
            DrawAboutSparkle(spriteBatch, numberRect.X + 18, numberRect.CenterY, clip, 2, 255, 226, 150, hovered ? 240 : 190);
            DrawAboutSparkle(spriteBatch, numberRect.Right - 18, numberRect.CenterY, clip, 2, 255, 226, 150, hovered ? 240 : 190);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, AboutFeedbackGroupNumber, numberRect.X + 28, numberRect.Y + 2, numberRect.Width - 56, numberRect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 226, 150, 255, FitAboutTextScale(AboutFeedbackGroupNumber, numberRect.Width - 56, 0.88f, 0.66f));

            var element = AddFrameElement(elements, "about-copy-feedback-group", "复制QQ群号", "button", numberRect);
            RecordFrameElementHover(element, hovered);

            return hovered ? element : null;
        }

        private static void DrawAboutQrCard(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 224);
            DrawAboutInlineIcon(spriteBatch, new LegacyUiRect(rect.X + 15, rect.Y + 9, 20, 20), clip, "heart", 236);
            DrawAboutTextLine(spriteBatch, clip, "喜欢的话，可以请我喝杯奶茶。", rect.X + 42, rect.Y + 9, rect.Width - 58, 0.68f, 238, 238, 226, 250);
            DrawAboutDottedLine(spriteBatch, rect.X + 38, rect.Y + 34, rect.Width - 56, clip, palette.BorderR, palette.BorderG, palette.BorderB, 96);

            var contentX = rect.X + 13;
            var contentY = rect.Y + 42;
            var availableWidth = rect.Width - 26;
            var gap = 16;
            var qrSize = Math.Max(76, Math.Min(96, Math.Min((availableWidth - gap) / 2, rect.Height - 66)));
            var totalWidth = qrSize * 2 + gap;
            var startX = contentX + Math.Max(0, (availableWidth - totalWidth) / 2);
            var qrY = contentY;
            DrawAboutQrSlot(spriteBatch, new LegacyUiRect(startX, qrY, qrSize, qrSize + 22), clip, AboutWechatResource, "微信");
            DrawAboutQrSlot(spriteBatch, new LegacyUiRect(startX + qrSize + gap, qrY, qrSize, qrSize + 22), clip, AboutAlipayResource, "支付宝");

            var lineX = startX + qrSize + gap / 2;
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, lineX, qrY + 10, 1, Math.Max(30, qrSize - 20), clip.X, clip.Y, clip.Width, clip.Height, 176, 192, 226, 78);
        }

        private static void DrawAboutFooter(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            DrawAboutPanel(spriteBatch, rect, clip, 218);
            DrawAboutCorner(spriteBatch, rect, clip, 10);
            DrawAboutTinyStars(spriteBatch, rect.Inset(14), clip, 14, 90);

            const string firstLine = "感谢每一位使用与支持决明Z的冒险者！";
            const string secondLine = "你们的反馈与陪伴，是我们持续前进的动力！";
            var firstScale = FitAboutTextScale(firstLine, rect.Width - 120, 0.66f, 0.54f);
            var firstWidth = UiTextRenderer.EstimateTextWidth(firstLine, firstScale);
            var centerX = rect.CenterX;
            var ornamentGap = 16;
            var lineY = rect.Y + 27;
            var leftLineX = rect.X + 30;
            var leftLineWidth = Math.Max(18, centerX - firstWidth / 2 - ornamentGap - leftLineX);
            var rightLineX = centerX + firstWidth / 2 + ornamentGap;
            var rightLineWidth = Math.Max(18, rect.Right - 30 - rightLineX);
            DrawAboutOrnamentLine(spriteBatch, leftLineX, lineY, leftLineWidth, clip, palette.BorderR, palette.BorderG, palette.BorderB, 104);
            DrawAboutOrnamentLine(spriteBatch, rightLineX, lineY, rightLineWidth, clip, palette.BorderR, palette.BorderG, palette.BorderB, 104);
            DrawAboutSparkle(spriteBatch, centerX - firstWidth / 2 - 10, lineY, clip, 2, 255, 226, 156, 220);
            DrawAboutSparkle(spriteBatch, centerX + firstWidth / 2 + 10, lineY, clip, 2, 255, 226, 156, 220);

            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, firstLine, rect.X + 18, rect.Y + 7, rect.Width - 36, 20, clip.X, clip.Y, clip.Width, clip.Height, 238, 238, 226, 248, firstScale);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, secondLine, rect.X + 18, rect.Y + 28, rect.Width - 36, 20, clip.X, clip.Y, clip.Width, clip.Height, 204, 216, 236, 236, FitAboutTextScale(secondLine, rect.Width - 80, 0.60f, 0.50f));
        }

        private static void DrawAboutPanel(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int alpha)
        {
            if (!rect.Intersects(clip))
            {
                return;
            }

            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, 8, palette.BorderR, palette.BorderG, palette.BorderB, Math.Min(236, alpha));
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4, clip.X, clip.Y, clip.Width, clip.Height, 6, palette.ContentR, palette.ContentG, palette.ContentB, Math.Min(232, alpha));
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10, clip.X, clip.Y, clip.Width, clip.Height, 4, palette.RowR, palette.RowG, palette.RowB, 70);
        }

        private static void DrawAboutTinyStars(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int count, int alpha)
        {
            if (!rect.Intersects(clip) || count <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                return;
            }

            var palette = VanillaUiSkinCompat.GetSkinPalette();
            var width = Math.Max(1, rect.Width);
            var height = Math.Max(1, rect.Height);
            for (var index = 0; index < count; index++)
            {
                var hash = ComputeAboutHash(rect.X, rect.Y, rect.Width, rect.Height, index + 1);
                var starX = rect.X + (int)(hash % (uint)width);
                var starY = rect.Y + (int)((hash >> 8) % (uint)height);
                var size = ((hash >> 17) & 1u) == 0u ? 1 : 2;
                var tint = (int)((hash >> 19) & 3u);
                var starA = Math.Min(255, alpha + (int)((hash >> 21) & 31u));

                var starR = palette.BorderR;
                var starG = palette.BorderG;
                var starB = palette.BorderB;
                if (tint == 1)
                {
                    starR = 255;
                    starG = 226;
                    starB = 156;
                }
                else if (tint == 2)
                {
                    starR = 198;
                    starG = 214;
                    starB = 255;
                }
                else if (tint == 3)
                {
                    starR = 216;
                    starG = 178;
                    starB = 255;
                }

                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, starX, starY, size, size, clip.X, clip.Y, clip.Width, clip.Height, starR, starG, starB, starA);
                if ((hash & 7u) == 0u)
                {
                    DrawAboutSparkle(spriteBatch, starX + size / 2, starY + size / 2, clip, 1, starR, starG, starB, Math.Min(255, starA + 26));
                }
            }
        }

        private static void DrawAboutPixelLogo(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip)
        {
            var logoWidth = Math.Max(142, Math.Min(rect.Width - 174, 214));
            var logoHeight = 42;
            var logoRect = new LegacyUiRect(rect.CenterX - logoWidth / 2, rect.CenterY - logoHeight / 2, logoWidth, logoHeight);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, logoRect.X, logoRect.Y, logoRect.Width, logoRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 6, 68, 72, 156, 92);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, logoRect.X + 1, logoRect.Y + 1, logoRect.Width - 2, logoRect.Height - 2, clip.X, clip.Y, clip.Width, clip.Height, 5, 52, 58, 144, 84);

            const string title = "决明Z";
            var scale = FitAboutTextScale(title, logoRect.Width - 24, 1.42f, 1.12f);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, title, logoRect.X + 10, logoRect.Y + 6, logoRect.Width - 20, logoRect.Height - 8, clip.X, clip.Y, clip.Width, clip.Height, 44, 30, 84, 210, scale);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, title, logoRect.X + 8, logoRect.Y + 4, logoRect.Width - 16, logoRect.Height - 8, clip.X, clip.Y, clip.Width, clip.Height, 255, 238, 204, 255, scale);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, logoRect.X + 16, logoRect.Y + 9, 4, 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 226, 156, 220);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, logoRect.Right - 20, logoRect.Bottom - 13, 4, 4, clip.X, clip.Y, clip.Width, clip.Height, 255, 226, 156, 220);

            DrawAboutSparkle(spriteBatch, logoRect.X + 12, logoRect.CenterY - 2, clip, 2, 255, 226, 156, 218);
            DrawAboutSparkle(spriteBatch, logoRect.Right - 12, logoRect.CenterY - 2, clip, 2, 255, 226, 156, 218);
        }

        private static void DrawAboutInlineIcon(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, string iconKind, int alpha)
        {
            if (!rect.Intersects(clip))
            {
                return;
            }

            var palette = VanillaUiSkinCompat.GetSkinPalette();
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, clip.X, clip.Y, clip.Width, clip.Height, 4, palette.PanelR, palette.PanelG, palette.PanelB, Math.Min(180, alpha));
            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X, rect.Y, rect.Width, rect.Height, 1, clip.X, clip.Y, clip.Width, clip.Height, palette.BorderR, palette.BorderG, palette.BorderB, Math.Min(220, alpha + 8));
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 3, rect.Y + 3, Math.Max(1, rect.Width - 6), 1, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 54);
            DrawAboutSmallIcon(spriteBatch, rect.Inset(2), clip, iconKind);
        }

        private static void DrawAboutCenteredTitle(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, string title, string iconKind, int y, float scale)
        {
            var iconSize = 20;
            var textWidth = UiTextRenderer.EstimateTextWidth(title, scale);
            var totalWidth = iconSize + 7 + textWidth;
            var startX = rect.X + Math.Max(12, (rect.Width - totalWidth) / 2);
            DrawAboutInlineIcon(spriteBatch, new LegacyUiRect(startX, y + 1, iconSize, iconSize), clip, iconKind, 232);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, startX + iconSize + 7, y + 1, Math.Max(1, rect.Right - startX - iconSize - 20), 20, clip.X, clip.Y, clip.Width, clip.Height, 238, 238, 226, 250, scale);
        }

        private static uint ComputeAboutHash(int x, int y, int width, int height, int index)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)y) * 16777619u;
                hash = (hash ^ (uint)width) * 16777619u;
                hash = (hash ^ (uint)height) * 16777619u;
                hash = (hash ^ (uint)index) * 16777619u;
                return hash;
            }
        }

        private static void DrawAboutSectionTitle(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, string title, string iconKind)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            var icon = new LegacyUiRect(rect.X + 16, rect.Y + 15, 15, 15);
            DrawAboutSmallIcon(spriteBatch, icon, clip, iconKind);
            UiTextRenderer.DrawTextClipped(spriteBatch, title, rect.X + 39, rect.Y + 10, rect.Width - 54, 24, clip.X, clip.Y, clip.Width, clip.Height, 238, 238, 226, 250, 0.74f);
            DrawAboutDottedLine(spriteBatch, rect.X + 38, rect.Y + 34, rect.Width - 56, clip, palette.BorderR, palette.BorderG, palette.BorderB, 95);
        }

        private static void DrawAboutQrSlot(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, string resourceName, string label)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            var qrRect = new LegacyUiRect(rect.X, rect.Y, rect.Width, rect.Width);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, qrRect.X - 4, qrRect.Y - 4, qrRect.Width + 8, qrRect.Height + 8, clip.X, clip.Y, clip.Width, clip.Height, 5, palette.BorderR, palette.BorderG, palette.BorderB, 178);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, qrRect.X - 2, qrRect.Y - 2, qrRect.Width + 4, qrRect.Height + 4, clip.X, clip.Y, clip.Width, clip.Height, 4, palette.PanelR, palette.PanelG, palette.PanelB, 160);
            UiPrimitiveRenderer.DrawRoundedRectClipped(spriteBatch, qrRect.X, qrRect.Y, qrRect.Width, qrRect.Height, clip.X, clip.Y, clip.Width, clip.Height, 4, 242, 244, 250, 248);
            DrawAboutQrCorners(spriteBatch, qrRect, clip, palette.BorderR, palette.BorderG, palette.BorderB, 220);

            object texture;
            if (UiEmbeddedTextureLoader.TryGetTexture(spriteBatch, resourceName, out texture))
            {
                UiPrimitiveRenderer.DrawTextureContainedClipped(spriteBatch, texture, qrRect.X + 6, qrRect.Y + 6, qrRect.Width - 12, qrRect.Height - 12, clip.X, clip.Y, clip.Width, clip.Height, 255, 255, 255, 255);
            }
            else
            {
                UiTextRenderer.DrawCenteredTextClipped(spriteBatch, "二维码", qrRect.X + 4, qrRect.Y + qrRect.Height / 2 - 10, qrRect.Width - 8, 20, clip.X, clip.Y, clip.Width, clip.Height, 90, 96, 122, 230, 0.58f);
            }

            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, qrRect.X + 3, qrRect.Y + 3, qrRect.Width - 6, qrRect.Height - 6, 1, clip.X, clip.Y, clip.Width, clip.Height, 84, 96, 154, 150);

            var labelY = qrRect.Bottom + 7;
            var dashWidth = Math.Max(14, (rect.Width - 74) / 2);
            DrawAboutDottedLine(spriteBatch, rect.X + 6, labelY + 11, dashWidth, clip, palette.BorderR, palette.BorderG, palette.BorderB, 90);
            DrawAboutDottedLine(spriteBatch, rect.Right - 6 - dashWidth, labelY + 11, dashWidth, clip, palette.BorderR, palette.BorderG, palette.BorderB, 90);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.CenterX, labelY + 10, 1, 1, clip.X, clip.Y, clip.Width, clip.Height, 255, 226, 156, 230);
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, label, rect.X + 2, labelY, rect.Width - 4, 20, clip.X, clip.Y, clip.Width, clip.Height, 238, 238, 226, 255, FitAboutTextScale(label, rect.Width - 8, 0.66f, 0.52f));
        }

        private static void DrawAboutQrCorners(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int r, int g, int b, int a)
        {
            var length = Math.Max(8, rect.Width / 6);
            var inset = -2;
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + inset, rect.Y + inset, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + inset, rect.Y + inset, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - inset - length, rect.Y + inset, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - inset - 2, rect.Y + inset, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + inset, rect.Bottom - inset - 2, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + inset, rect.Bottom - inset - length, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - inset - length, rect.Bottom - inset - 2, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - inset - 2, rect.Bottom - inset - length, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
        }

        private static void DrawAboutTextLine(object spriteBatch, LegacyUiRect clip, string text, int x, int y, int width, float scale, int r, int g, int b, int a)
        {
            UiTextRenderer.DrawTextClipped(spriteBatch, text, x, y, width, 22, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a, FitAboutTextScale(text, width, scale, 0.50f));
        }

        private static void DrawAboutCenteredTextLine(object spriteBatch, LegacyUiRect clip, string text, int x, int y, int width, float scale, int r, int g, int b, int a)
        {
            UiTextRenderer.DrawCenteredTextClipped(spriteBatch, text, x, y, width, 23, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a, FitAboutTextScale(text, width, scale, 0.56f));
        }

        private static void DrawAboutCorner(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, int length)
        {
            var palette = VanillaUiSkinCompat.GetSkinPalette();
            var r = palette.BorderR;
            var g = palette.BorderG;
            var b = palette.BorderB;
            var a = 166;
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 10, rect.Y + 10, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 10, rect.Y + 10, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - 10 - length, rect.Y + 10, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - 12, rect.Y + 10, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 10, rect.Bottom - 12, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 10, rect.Bottom - 10 - length, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - 10 - length, rect.Bottom - 12, length, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.Right - 12, rect.Bottom - 10 - length, 2, length, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
        }

        private static void DrawAboutDottedLine(object spriteBatch, int x, int y, int width, LegacyUiRect clip, int r, int g, int b, int a)
        {
            if (width <= 0)
            {
                return;
            }

            for (var dotX = x; dotX < x + width; dotX += 8)
            {
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, dotX, y, 3, 1, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            }
        }

        private static void DrawAboutOrnamentLine(object spriteBatch, int x, int y, int width, LegacyUiRect clip, int r, int g, int b, int a)
        {
            if (width <= 0)
            {
                return;
            }

            DrawAboutDottedLine(spriteBatch, x, y, width, clip, r, g, b, a);
            if (width < 30)
            {
                return;
            }

            DrawAboutSparkle(spriteBatch, x + Math.Max(8, width / 4), y, clip, 1, 255, 226, 156, Math.Min(230, a + 54));
            DrawAboutSparkle(spriteBatch, x + width / 2, y, clip, 2, r, g, b, Math.Min(230, a + 42));
            DrawAboutSparkle(spriteBatch, x + Math.Min(width - 8, width * 3 / 4), y, clip, 1, 255, 226, 156, Math.Min(230, a + 54));
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x, y - 2, 2, 5, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, Math.Min(190, a + 28));
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, x + width - 2, y - 2, 2, 5, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, Math.Min(190, a + 28));
        }

        private static void DrawAboutSparkle(object spriteBatch, int centerX, int centerY, LegacyUiRect clip, int size, int r, int g, int b, int a)
        {
            var safeSize = Math.Max(1, size);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX - safeSize, centerY, safeSize * 2 + 1, 1, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, centerX, centerY - safeSize, 1, safeSize * 2 + 1, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, a);
        }

        private static void DrawAboutSmallIcon(object spriteBatch, LegacyUiRect rect, LegacyUiRect clip, string iconKind)
        {
            var r = string.Equals(iconKind, "heart", StringComparison.Ordinal) ? 255 : 214;
            var g = string.Equals(iconKind, "heart", StringComparison.Ordinal) ? 148 : 194;
            var b = string.Equals(iconKind, "heart", StringComparison.Ordinal) ? 164 : 246;
            if (string.Equals(iconKind, "message", StringComparison.Ordinal))
            {
                UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 6, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 220);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 5, rect.Bottom - 5, 5, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 220);
                return;
            }

            if (string.Equals(iconKind, "heart", StringComparison.Ordinal))
            {
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 4, rect.Y + 4, 3, 3, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 230);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 9, rect.Y + 4, 3, 3, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 230);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 3, rect.Y + 7, 10, 3, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 230);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 5, rect.Y + 10, 6, 3, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 230);
                UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 7, rect.Y + 13, 2, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 230);
                return;
            }

            UiPrimitiveRenderer.DrawRectBorderClipped(spriteBatch, rect.X + 3, rect.Y + 2, rect.Width - 6, rect.Height - 4, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 220);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 6, rect.Y + 6, rect.Width - 12, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 220);
            UiPrimitiveRenderer.DrawFilledRectClipped(spriteBatch, rect.X + 6, rect.Y + 10, rect.Width - 12, 2, clip.X, clip.Y, clip.Width, clip.Height, r, g, b, 180);
        }

        private static float FitAboutTextScale(string text, int width, float preferred, float min)
        {
            var scale = preferred;
            while (scale > min && UiTextRenderer.EstimateTextWidth(text, scale) > width)
            {
                scale -= 0.02f;
            }

            return Math.Max(min, scale);
        }
    }
}
