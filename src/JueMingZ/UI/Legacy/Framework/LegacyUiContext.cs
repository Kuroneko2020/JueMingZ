using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy.Framework
{
    public sealed class LegacyUiContext
    {
        public object SpriteBatch { get; private set; }
        public LegacyMouseSnapshot Mouse { get; private set; }
        public LegacyUiRect WindowRect { get; private set; }
        public LegacyUiRect ContentRect { get; private set; }
        public LegacyScrollArea ScrollArea { get; private set; }
        public LegacyUiRect ClipRect { get; private set; }
        public bool HasClip { get; private set; }
        public IList<LegacyUiElement> Elements { get; private set; }
        public string SelectedPageId { get; private set; }
        public AppSettings Settings { get; private set; }
        public LegacyUiElement HoveredElement { get; private set; }

        public LegacyUiContext(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            LegacyUiRect windowRect,
            string selectedPageId,
            AppSettings settings,
            IList<LegacyUiElement> elements)
        {
            SpriteBatch = spriteBatch;
            Mouse = mouse;
            WindowRect = windowRect;
            SelectedPageId = selectedPageId ?? string.Empty;
            Settings = settings;
            Elements = elements ?? new List<LegacyUiElement>();
            ClipRect = windowRect;
            HasClip = false;
        }

        public static LegacyUiContext ForScrollArea(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            LegacyScrollArea area,
            IList<LegacyUiElement> elements,
            AppSettings settings)
        {
            var context = new LegacyUiContext(
                spriteBatch,
                mouse,
                LegacyMainUiState.WindowRect,
                LegacyMainUiState.SelectedPageId,
                settings,
                elements);
            context.SetScrollArea(area);
            return context;
        }

        public void SetFrame(LegacyUiRect windowRect, string selectedPageId, AppSettings settings)
        {
            WindowRect = windowRect;
            SelectedPageId = selectedPageId ?? string.Empty;
            Settings = settings;
        }

        public void SetContentRect(LegacyUiRect contentRect)
        {
            ContentRect = contentRect;
        }

        public LegacyUiContext CreateUnclippedCopy()
        {
            var context = new LegacyUiContext(SpriteBatch, Mouse, WindowRect, SelectedPageId, Settings, Elements);
            context.SetContentRect(ContentRect);
            return context;
        }

        public void SetScrollArea(LegacyScrollArea area)
        {
            ScrollArea = area;
            if (area == null)
            {
                HasClip = false;
                ClipRect = WindowRect;
                return;
            }

            HasClip = true;
            ClipRect = area.Viewport;
            ContentRect = area.ContentRect;
        }

        public bool IsRectVisible(LegacyUiRect rect)
        {
            return !HasClip || rect.Intersects(ClipRect);
        }

        public LegacyUiRect ResolveHitRect(LegacyUiRect rect)
        {
            if (!HasClip)
            {
                return rect;
            }

            var hit = rect.Intersect(ClipRect);
            return hit.Width > 0 && hit.Height > 0 ? hit : rect;
        }

        public bool IsMouseOver(LegacyUiRect rect)
        {
            if (Mouse == null)
            {
                return false;
            }

            if (HasClip)
            {
                var hit = rect.Intersect(ClipRect);
                return hit.Width > 0 && hit.Height > 0 && hit.Contains(Mouse.X, Mouse.Y);
            }

            return rect.Contains(Mouse.X, Mouse.Y);
        }

        public bool IsElementHovered(string id, LegacyUiRect rect)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return IsMouseOver(rect);
            }

            return LegacyUiElementFrame.IsHovered(id, ResolveHitRect(rect), Mouse);
        }

        public LegacyUiElement RegisterElement(
            string id,
            string label,
            string kind,
            LegacyUiRect rect,
            bool enabled,
            bool selected,
            int intValue,
            int minValue,
            int maxValue,
            string[] tooltipLines)
        {
            var hitRect = ResolveHitRect(rect);
            var element = LegacyUiElementFrame.Add(
                Elements,
                id,
                label,
                kind,
                hitRect,
                enabled,
                selected,
                intValue,
                minValue,
                maxValue,
                tooltipLines,
                null,
                null);
            var hovered = LegacyUiElementFrame.IsHovered(id, hitRect, Mouse);
            LegacyUiElementFrame.RecordHover(element, hovered);
            if (hovered)
            {
                HoveredElement = element;
            }

            return element;
        }

        public void RegisterElement(LegacyUiElement element)
        {
            if (element == null)
            {
                return;
            }

            Elements.Add(element);
            var hovered = IsElementHovered(element.Id, element.Rect);
            LegacyUiElementFrame.RecordHover(element, hovered);
            if (hovered)
            {
                HoveredElement = element;
            }
        }

        public void SetHoveredElement(LegacyUiElement element)
        {
            if (element != null)
            {
                HoveredElement = element;
            }
        }
    }
}
