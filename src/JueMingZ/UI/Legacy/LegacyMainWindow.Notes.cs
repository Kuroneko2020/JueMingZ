using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Information.Notes;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private const int NotesHeaderButtonWidth = 52;
        private const int NotesDeleteButtonWidth = 44;
        private const int NotesCardButtonGap = 5;

        private static LegacyUiElement DrawNotesPage(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            UserNotesUiState.EnsureLoaded();
            UserNotesUiState.UpdateActiveEditorForDraw();
            var snapshot = UserNotesUiState.Snapshot;
            var layout = UserNotesUiState.BuildLayout(area.Viewport.Width, area.Viewport.Height);
            RegisterNotesEditOutsideElement(area, elements);
            var hovered = DrawNotesAddButton(spriteBatch, area, mouse, elements) ?? null;
            UserNotesUiState.BeginBodyViewportFrame();

            if (snapshot.Notes == null || snapshot.Notes.Count <= 0 || layout.Cards == null)
            {
                return hovered;
            }

            for (var index = 0; index < layout.Cards.Count; index++)
            {
                var card = layout.Cards[index];
                var note = FindNoteForCard(snapshot, card == null ? string.Empty : card.NoteId);
                if (note == null || card == null)
                {
                    continue;
                }

                var cardRect = new LegacyUiRect(area.Viewport.X + card.X, area.ToScreenY(card.Y), card.Width, card.Height);
                if (!area.IsVisible(cardRect))
                {
                    continue;
                }

                hovered = DrawNotesCard(spriteBatch, area, mouse, elements, note, card, cardRect) ?? hovered;
            }

            return hovered;
        }

        private static void RegisterNotesEditOutsideElement(LegacyScrollArea area, List<LegacyUiElement> elements)
        {
            if (!UserNotesUiState.HasActiveEditor || area == null || elements == null)
            {
                return;
            }

            AddFrameElement(
                elements,
                UserNotesUiState.EditOutsideElementId,
                "笔记:保存编辑",
                "button",
                area.Viewport,
                enabled: true,
                selected: false);
        }

        private static LegacyUiElement DrawNotesAddButton(object spriteBatch, LegacyScrollArea area, LegacyMouseSnapshot mouse, List<LegacyUiElement> elements)
        {
            var rect = new LegacyUiRect(area.Viewport.X, area.ToScreenY(0), area.Viewport.Width, 34);
            if (!area.IsVisible(rect))
            {
                return null;
            }

            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(UserNotesUiState.AddButtonId, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, false, true, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                "+",
                rect.X + 4,
                rect.Y,
                rect.Width - 8,
                rect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                238,
                238,
                226,
                255,
                0.92f);

            var element = AddFrameElement(elements, UserNotesUiState.AddButtonId, "笔记:新增", "button", elementRect);
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static LegacyUiElement DrawNotesCard(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            UserNoteSnapshot note,
            UserNoteCardLayout layout,
            LegacyUiRect card)
        {
            LegacyUiTheme.DrawRowClipped(spriteBatch, card, area.Viewport);
            var hovered = (LegacyUiElement)null;
            var buttonY = card.Y + 6;
            var deleteConfirm = UserNotesUiState.IsDeleteConfirming(note.NoteId);
            var deleteRect = new LegacyUiRect(card.Right - NotesDeleteButtonWidth - 7, buttonY, NotesDeleteButtonWidth, RowModeButtonHeight);
            var pinRect = new LegacyUiRect(deleteRect.X - NotesHeaderButtonWidth - NotesCardButtonGap, buttonY, NotesHeaderButtonWidth, RowModeButtonHeight);
            var titleWidth = Math.Max(1, pinRect.X - card.X - 14);
            var titleRect = new LegacyUiRect(card.X + 8, card.Y + 7, titleWidth, 22);
            var title = UserNotesUiState.GetTitleDisplayText(note);
            var titleEditing = UserNotesUiState.IsEditingTitle(note.NoteId);
            UiTextRenderer.DrawTextClipped(
                spriteBatch,
                titleEditing ? title : UiTextRenderer.Ellipsize(title, titleWidth, 0.76f),
                titleRect.X,
                titleRect.Y,
                titleWidth,
                22,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                244,
                238,
                210,
                255,
                0.76f);
            var titleHit = titleRect.Intersect(area.Viewport);
            if (titleHit.Width > 0 && titleHit.Height > 0)
            {
                var titleElement = AddFrameElement(
                    elements,
                    UserNotesUiState.TitleElementPrefix + note.NoteId,
                    "笔记:标题",
                    "button",
                    titleHit,
                    enabled: true,
                    selected: titleEditing);
                var titleHovered = IsFrameElementHovered(titleElement.Id, titleHit, mouse);
                RecordFrameElementHover(titleElement, titleHovered);
                if (titleHovered)
                {
                    hovered = titleElement;
                }
            }

            if (titleEditing)
            {
                UserNotesUiState.TryAttachActiveEditorImePanel(note.NoteId, "title", titleRect.Intersect(area.Viewport));
            }

            hovered = DrawNotesSmallButton(
                spriteBatch,
                area,
                mouse,
                elements,
                pinRect,
                UserNotesUiState.PinButtonPrefix + note.NoteId,
                note.PinnedState != null && note.PinnedState.Pinned ? "已悬挂" : "悬挂",
                true,
                note.PinnedState != null && note.PinnedState.Pinned) ?? hovered;
            hovered = DrawNotesSmallButton(
                spriteBatch,
                area,
                mouse,
                elements,
                deleteRect,
                UserNotesUiState.DeleteButtonPrefix + note.NoteId,
                deleteConfirm ? "确认" : "删除",
                true,
                deleteConfirm) ?? hovered;

            var bodyRect = new LegacyUiRect(
                card.X + 8,
                card.Y + layout.BodyY,
                Math.Max(1, card.Width - 16),
                layout.BodyHeight);
            LegacyUiTheme.DrawSubPanelClipped(spriteBatch, bodyRect, area.Viewport);
            var bodyTextViewport = UserNotesUiState.ResolveBodyTextViewport(bodyRect);
            UserNotesUiState.SetBodyViewport(note.NoteId, bodyTextViewport.Intersect(area.Viewport), layout.BodyContentHeight);
            DrawNotesBodyPreview(spriteBatch, area, note, layout, bodyRect);

            var bodyHit = bodyTextViewport.Intersect(area.Viewport);
            if (bodyHit.Width > 0 && bodyHit.Height > 0)
            {
                var bodyEditing = UserNotesUiState.IsEditingBody(note.NoteId);
                var bodyElement = AddFrameElement(
                    elements,
                    UserNotesUiState.BodyElementPrefix + note.NoteId,
                    "笔记:正文",
                    "button",
                    bodyHit,
                    enabled: true,
                    selected: bodyEditing);
                var bodyHovered = IsFrameElementHovered(bodyElement.Id, bodyHit, mouse);
                RecordFrameElementHover(bodyElement, bodyHovered);
                if (bodyHovered)
                {
                    hovered = bodyElement;
                }
            }

            return hovered;
        }

        private static LegacyUiElement DrawNotesSmallButton(
            object spriteBatch,
            LegacyScrollArea area,
            LegacyMouseSnapshot mouse,
            List<LegacyUiElement> elements,
            LegacyUiRect rect,
            string id,
            string label,
            bool enabled,
            bool selected)
        {
            var hit = rect.Intersect(area.Viewport);
            var elementRect = hit.Width > 0 && hit.Height > 0 ? hit : rect;
            var hovered = IsFrameElementHovered(id, elementRect, mouse);
            LegacyUiTheme.DrawButtonClipped(spriteBatch, rect, hovered, hovered && mouse.LeftDown, selected, enabled, area.Viewport);
            UiTextRenderer.DrawCenteredTextClipped(
                spriteBatch,
                label,
                rect.X + 2,
                rect.Y,
                rect.Width - 4,
                rect.Height,
                area.Viewport.X,
                area.Viewport.Y,
                area.Viewport.Width,
                area.Viewport.Height,
                selected ? LegacyUiTheme.SelectedTextR : 230,
                selected ? LegacyUiTheme.SelectedTextG : 232,
                selected ? LegacyUiTheme.SelectedTextB : 224,
                255,
                FitNotesButtonTextScale(label, rect.Width));
            var element = AddFrameElement(elements, id, "笔记:" + label, "button", elementRect, enabled: enabled, selected: selected);
            RecordFrameElementHover(element, hovered);
            return hovered ? element : null;
        }

        private static void DrawNotesBodyPreview(object spriteBatch, LegacyScrollArea area, UserNoteSnapshot note, UserNoteCardLayout layout, LegacyUiRect bodyRect)
        {
            var scrollOffset = UserNotesUiState.GetBodyScrollOffset(note == null ? string.Empty : note.NoteId);
            var bodyEditing = UserNotesUiState.IsEditingBody(note == null ? string.Empty : note.NoteId);
            var textViewport = UserNotesUiState.ResolveBodyTextViewport(bodyRect);
            var textClip = textViewport.Intersect(area.Viewport);
            if (textClip.Width <= 0 || textClip.Height <= 0)
            {
                return;
            }

            var lines = bodyEditing
                ? UserNotesUiState.BuildCardBodyLinesForDrawing(
                    UserNotesUiState.GetBodyDisplayText(note),
                    textViewport.Width)
                : layout.BodyLines ?? new string[0];
            var textR = string.IsNullOrWhiteSpace(note == null ? null : note.Body) ? 182 : 218;
            var textG = string.IsNullOrWhiteSpace(note == null ? null : note.Body) ? 194 : 226;
            var textB = string.IsNullOrWhiteSpace(note == null ? null : note.Body) ? 212 : 236;
            if (bodyEditing)
            {
                textR = 244;
                textG = 238;
                textB = 210;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                var lineY = textViewport.Y + index * UserNotesUiState.BodyLineHeightForLayout - scrollOffset;
                if (lineY + UserNotesUiState.BodyLineHeightForLayout < textClip.Y || lineY > textClip.Bottom)
                {
                    continue;
                }

                UiTextRenderer.DrawTextClipped(
                    spriteBatch,
                    lines[index],
                    textViewport.X,
                    lineY,
                    textViewport.Width,
                    UserNotesUiState.BodyLineHeightForLayout,
                    textClip.X,
                    textClip.Y,
                    textClip.Width,
                    textClip.Height,
                    textR,
                    textG,
                    textB,
                    238,
                    UserNotesUiState.BodyTextScaleForLayout);
            }

            if (bodyEditing)
            {
                var anchorY = UserNotesUiState.ResolveBodyEditorImeLineY(note == null ? string.Empty : note.NoteId, textViewport);
                UserNotesUiState.TryAttachActiveEditorImePanel(
                    note == null ? string.Empty : note.NoteId,
                    "body",
                    new LegacyUiRect(textViewport.X, anchorY, textViewport.Width, UserNotesUiState.BodyLineHeightForLayout));
            }
        }

        private static UserNoteSnapshot FindNoteForCard(UserNotesSnapshot snapshot, string noteId)
        {
            if (snapshot == null || snapshot.Notes == null || string.IsNullOrWhiteSpace(noteId))
            {
                return null;
            }

            for (var index = 0; index < snapshot.Notes.Count; index++)
            {
                var note = snapshot.Notes[index];
                if (note != null && string.Equals(note.NoteId, noteId, StringComparison.OrdinalIgnoreCase))
                {
                    return note;
                }
            }

            return null;
        }

        private static int CalculateNotesContentHeight(LegacyUiRect contentRect)
        {
            var viewportWidth = Math.Max(1, contentRect.Width - LegacyUiMetrics.ContentPadding * 2 - LegacyUiMetrics.ScrollbarWidth - 8);
            var viewportHeight = Math.Max(1, contentRect.Height - LegacyUiMetrics.ContentPadding * 2);
            return UserNotesUiState.BuildLayout(viewportWidth, viewportHeight).ContentHeight;
        }

        private static float FitNotesButtonTextScale(string label, int width)
        {
            var scale = width <= 44 ? 0.56f : 0.60f;
            var measured = UiTextRenderer.EstimateTextWidth(label, scale);
            return measured <= width - 4 ? scale : Math.Max(0.46f, scale * (width - 4) / Math.Max(1, measured));
        }

        internal static int CalculateNotesContentHeightForTesting(LegacyUiRect contentRect)
        {
            return CalculateNotesContentHeight(contentRect);
        }

        internal static UserNotesPageLayout BuildNotesLayoutForTesting(int viewportWidth, int viewportHeight)
        {
            return UserNotesUiState.BuildLayout(viewportWidth, viewportHeight);
        }
    }
}
