using System;
using System.Collections.Generic;
using System.Threading;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiFrameModel _retainedFrameModel;
        private static LegacyUiFrameModel _retainedFrameReplayModel;
        private static LegacyUiFrameModelSignature _activeRetainedFrameSignature;
        private static bool _retainedFrameReplayActive;
        private static bool _retainedFrameFallbackThisFrame;
        private static int _retainedFrameReplayIndex;
        private static int _retainedFrameOffsetX;
        private static int _retainedFrameOffsetY;
        private static int _retainedFrameLastVisibleElementCount;
        private static long _retainedFrameCacheHitCount;
        private static long _retainedFrameCacheMissCount;
        private static long _retainedFrameFallbackCount;

        internal static long RetainedFrameCacheHitCount
        {
            get { return Interlocked.Read(ref _retainedFrameCacheHitCount); }
        }

        internal static long RetainedFrameCacheMissCount
        {
            get { return Interlocked.Read(ref _retainedFrameCacheMissCount); }
        }

        internal static long RetainedFrameFallbackCount
        {
            get { return Interlocked.Read(ref _retainedFrameFallbackCount); }
        }

        internal static int RetainedFrameVisibleElementCount
        {
            get { return _retainedFrameLastVisibleElementCount; }
        }

        private static void BeginRetainedFrameModel(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings,
            int pageContentHeight)
        {
            settings = settings ?? AppSettings.CreateDefault();
            _activeRetainedFrameSignature = BuildRetainedFrameModelSignature(selectedPage, windowRect, contentRect, scrollArea, settings, pageContentHeight);
            _retainedFrameReplayModel = null;
            _retainedFrameReplayActive = false;
            _retainedFrameFallbackThisFrame = false;
            _retainedFrameReplayIndex = 0;
            _retainedFrameOffsetX = 0;
            _retainedFrameOffsetY = 0;

            var model = _retainedFrameModel;
            if (model != null &&
                model.Signature != null &&
                model.Signature.Matches(_activeRetainedFrameSignature))
            {
                _retainedFrameReplayModel = model;
                _retainedFrameReplayActive = true;
                _retainedFrameOffsetX = windowRect.X - model.Signature.WindowX;
                _retainedFrameOffsetY = windowRect.Y - model.Signature.WindowY;
                Interlocked.Increment(ref _retainedFrameCacheHitCount);
                return;
            }

            Interlocked.Increment(ref _retainedFrameCacheMissCount);
        }

        internal static bool TryReplayRetainedFrameElement(
            IList<LegacyUiElement> elements,
            string id,
            string label,
            string kind,
            LegacyUiRect rect,
            bool enabled,
            bool selected,
            int intValue,
            int minValue,
            int maxValue,
            string[] tooltipLines,
            BuffPotionCandidate candidate,
            BuffPotionWhitelistEntry whitelistEntry,
            out LegacyUiElement element)
        {
            element = null;
            if (!_retainedFrameReplayActive || _retainedFrameReplayModel == null)
            {
                return false;
            }

            LegacyUiFrameElementSnapshot snapshot;
            if (!_retainedFrameReplayModel.TryGetElement(_retainedFrameReplayIndex, out snapshot) ||
                !snapshot.Matches(id, kind))
            {
                AbortRetainedFrameReplay();
                return false;
            }

            _retainedFrameReplayIndex++;
            var replayRect = snapshot.OffsetRect(_retainedFrameOffsetX, _retainedFrameOffsetY);
            if (!SameRect(replayRect, rect))
            {
                AbortRetainedFrameReplay();
                return false;
            }

            element = LegacyUiElementFrame.Acquire();
            element.Reset(
                snapshot.Id,
                label ?? snapshot.Label,
                kind ?? snapshot.Kind,
                replayRect,
                enabled,
                selected,
                intValue,
                minValue,
                maxValue,
                tooltipLines,
                candidate,
                whitelistEntry);

            if (elements != null)
            {
                elements.Add(element);
            }

            return true;
        }

        private static void AbortRetainedFrameReplay()
        {
            if (!_retainedFrameFallbackThisFrame)
            {
                Interlocked.Increment(ref _retainedFrameFallbackCount);
            }

            _retainedFrameFallbackThisFrame = true;
            _retainedFrameReplayActive = false;
            _retainedFrameReplayModel = null;
        }

        private static void FinishRetainedFrameModel(IList<LegacyUiElement> elements)
        {
            if (_activeRetainedFrameSignature == null)
            {
                return;
            }

            _retainedFrameModel = LegacyUiFrameModel.Capture(_activeRetainedFrameSignature, elements);
            _retainedFrameLastVisibleElementCount = _retainedFrameModel.ElementCount;
            _activeRetainedFrameSignature = null;
            _retainedFrameReplayModel = null;
            _retainedFrameReplayActive = false;
            _retainedFrameReplayIndex = 0;
        }

        private static void CancelRetainedFrameModel()
        {
            _activeRetainedFrameSignature = null;
            _retainedFrameReplayModel = null;
            _retainedFrameReplayActive = false;
            _retainedFrameReplayIndex = 0;
        }

        private static LegacyUiFrameModelSignature BuildRetainedFrameModelSignature(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings,
            int pageContentHeight)
        {
            settings = settings ?? AppSettings.CreateDefault();
            var visibleStart = scrollArea == null ? 0 : scrollArea.ScrollOffset;
            var visibleEnd = scrollArea == null ? 0 : scrollArea.ScrollOffset + scrollArea.Viewport.Height;
            return new LegacyUiFrameModelSignature(
                selectedPage,
                windowRect.X,
                windowRect.Y,
                windowRect.Width,
                windowRect.Height,
                contentRect.Width,
                contentRect.Height,
                pageContentHeight,
                scrollArea == null ? 0 : scrollArea.ScrollOffset,
                scrollArea == null ? 0 : scrollArea.MaxScroll,
                visibleStart,
                visibleEnd,
                settings.ConfigVersion,
                BuildPageStateSignature(selectedPage, settings),
                BuildFrameHoverLayoutToken(selectedPage, windowRect, contentRect, scrollArea, settings),
                LegacyUiOverlayCoordinator.Current.LastStackSignature,
                UiTextRenderer.FontSignatureForLayoutCache,
                UiTextRenderer.CacheGenerationForLayoutCache);
        }

        private static bool SameRect(LegacyUiRect left, LegacyUiRect right)
        {
            return left.X == right.X &&
                   left.Y == right.Y &&
                   left.Width == right.Width &&
                   left.Height == right.Height;
        }

        internal static LegacyUiRetainedFrameModelSnapshot BuildRetainedFrameModelSnapshotForTesting(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings,
            int pageContentHeight,
            IList<LegacyUiElement> sourceElements)
        {
            BeginRetainedFrameModel(selectedPage, windowRect, contentRect, scrollArea, settings, pageContentHeight);
            var elements = PrepareFrameElements();
            try
            {
                if (sourceElements != null)
                {
                    for (var index = 0; index < sourceElements.Count; index++)
                    {
                        var source = sourceElements[index];
                        if (source == null)
                        {
                            continue;
                        }

                        AddFrameElement(
                            elements,
                            source.Id,
                            source.Label,
                            source.Kind,
                            source.Rect,
                            source.Enabled,
                            source.Selected,
                            source.IntValue,
                            source.MinValue,
                            source.MaxValue,
                            source.TooltipLines,
                            source.Candidate,
                            source.WhitelistEntry);
                    }
                }

                FinishRetainedFrameModel(elements);
                return new LegacyUiRetainedFrameModelSnapshot
                {
                    CacheHitCount = RetainedFrameCacheHitCount,
                    CacheMissCount = RetainedFrameCacheMissCount,
                    FallbackCount = RetainedFrameFallbackCount,
                    VisibleElementCount = RetainedFrameVisibleElementCount,
                    FirstElementRect = elements.Count > 0 && elements[0] != null ? elements[0].Rect : new LegacyUiRect(),
                    FirstElementId = elements.Count > 0 && elements[0] != null ? elements[0].Id : string.Empty
                };
            }
            finally
            {
                FinishFrameElements();
            }
        }

        internal static void ResetRetainedFrameModelForTesting()
        {
            _retainedFrameModel = null;
            _retainedFrameReplayModel = null;
            _activeRetainedFrameSignature = null;
            _retainedFrameReplayActive = false;
            _retainedFrameFallbackThisFrame = false;
            _retainedFrameReplayIndex = 0;
            _retainedFrameOffsetX = 0;
            _retainedFrameOffsetY = 0;
            _retainedFrameLastVisibleElementCount = 0;
            Interlocked.Exchange(ref _retainedFrameCacheHitCount, 0);
            Interlocked.Exchange(ref _retainedFrameCacheMissCount, 0);
            Interlocked.Exchange(ref _retainedFrameFallbackCount, 0);
        }

        internal sealed class LegacyUiRetainedFrameModelSnapshot
        {
            public long CacheHitCount { get; set; }
            public long CacheMissCount { get; set; }
            public long FallbackCount { get; set; }
            public int VisibleElementCount { get; set; }
            public LegacyUiRect FirstElementRect { get; set; }
            public string FirstElementId { get; set; }
        }

        private sealed class LegacyUiFrameModel
        {
            private readonly List<LegacyUiFrameElementSnapshot> _elements;

            private LegacyUiFrameModel(LegacyUiFrameModelSignature signature, List<LegacyUiFrameElementSnapshot> elements)
            {
                Signature = signature;
                _elements = elements ?? new List<LegacyUiFrameElementSnapshot>();
            }

            public LegacyUiFrameModelSignature Signature { get; private set; }

            public int ElementCount
            {
                get { return _elements.Count; }
            }

            public static LegacyUiFrameModel Capture(LegacyUiFrameModelSignature signature, IList<LegacyUiElement> elements)
            {
                var snapshots = new List<LegacyUiFrameElementSnapshot>(elements == null ? 0 : elements.Count);
                if (elements != null)
                {
                    for (var index = 0; index < elements.Count; index++)
                    {
                        var element = elements[index];
                        if (element != null)
                        {
                            snapshots.Add(new LegacyUiFrameElementSnapshot(element));
                        }
                    }
                }

                return new LegacyUiFrameModel(signature, snapshots);
            }

            public bool TryGetElement(int index, out LegacyUiFrameElementSnapshot element)
            {
                if (index >= 0 && index < _elements.Count)
                {
                    element = _elements[index];
                    return true;
                }

                element = null;
                return false;
            }
        }

        private sealed class LegacyUiFrameElementSnapshot
        {
            public LegacyUiFrameElementSnapshot(LegacyUiElement element)
            {
                Id = element == null ? string.Empty : element.Id ?? string.Empty;
                Label = element == null ? string.Empty : element.Label ?? string.Empty;
                Kind = element == null ? string.Empty : element.Kind ?? string.Empty;
                Rect = element == null ? new LegacyUiRect() : element.Rect;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string Kind { get; private set; }
            public LegacyUiRect Rect { get; private set; }

            public bool Matches(string id, string kind)
            {
                return string.Equals(Id, id ?? string.Empty, StringComparison.Ordinal) &&
                       string.Equals(Kind, kind ?? string.Empty, StringComparison.Ordinal);
            }

            public LegacyUiRect OffsetRect(int offsetX, int offsetY)
            {
                return new LegacyUiRect(Rect.X + offsetX, Rect.Y + offsetY, Rect.Width, Rect.Height);
            }
        }

        private sealed class LegacyUiFrameModelSignature
        {
            public LegacyUiFrameModelSignature(
                string pageId,
                int windowX,
                int windowY,
                int windowWidth,
                int windowHeight,
                int contentWidth,
                int contentHeight,
                int pageContentHeight,
                int scrollOffset,
                int maxScroll,
                int visibleStart,
                int visibleEnd,
                int settingsVersion,
                int pageStateSignature,
                int layoutToken,
                int overlayStackSignature,
                string fontSignature,
                int fontCacheGeneration)
            {
                PageId = pageId ?? string.Empty;
                WindowX = windowX;
                WindowY = windowY;
                WindowWidth = windowWidth;
                WindowHeight = windowHeight;
                ContentWidth = contentWidth;
                ContentHeight = contentHeight;
                PageContentHeight = pageContentHeight;
                ScrollOffset = scrollOffset;
                MaxScroll = maxScroll;
                VisibleStart = visibleStart;
                VisibleEnd = visibleEnd;
                SettingsVersion = settingsVersion;
                PageStateSignature = pageStateSignature;
                LayoutToken = layoutToken;
                OverlayStackSignature = overlayStackSignature;
                FontSignature = fontSignature ?? string.Empty;
                FontCacheGeneration = fontCacheGeneration;
            }

            public string PageId { get; private set; }
            public int WindowX { get; private set; }
            public int WindowY { get; private set; }
            public int WindowWidth { get; private set; }
            public int WindowHeight { get; private set; }
            public int ContentWidth { get; private set; }
            public int ContentHeight { get; private set; }
            public int PageContentHeight { get; private set; }
            public int ScrollOffset { get; private set; }
            public int MaxScroll { get; private set; }
            public int VisibleStart { get; private set; }
            public int VisibleEnd { get; private set; }
            public int SettingsVersion { get; private set; }
            public int PageStateSignature { get; private set; }
            public int LayoutToken { get; private set; }
            public int OverlayStackSignature { get; private set; }
            public string FontSignature { get; private set; }
            public int FontCacheGeneration { get; private set; }

            public bool Matches(LegacyUiFrameModelSignature other)
            {
                return other != null &&
                       WindowWidth == other.WindowWidth &&
                       WindowHeight == other.WindowHeight &&
                       ContentWidth == other.ContentWidth &&
                       ContentHeight == other.ContentHeight &&
                       PageContentHeight == other.PageContentHeight &&
                       ScrollOffset == other.ScrollOffset &&
                       MaxScroll == other.MaxScroll &&
                       VisibleStart == other.VisibleStart &&
                       VisibleEnd == other.VisibleEnd &&
                       SettingsVersion == other.SettingsVersion &&
                       PageStateSignature == other.PageStateSignature &&
                       LayoutToken == other.LayoutToken &&
                       OverlayStackSignature == other.OverlayStackSignature &&
                       FontCacheGeneration == other.FontCacheGeneration &&
                       string.Equals(PageId, other.PageId, StringComparison.Ordinal) &&
                       string.Equals(FontSignature, other.FontSignature, StringComparison.Ordinal);
            }
        }
    }
}
