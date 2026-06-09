using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    internal enum LegacyUiOverlayKind
    {
        Popup = 0,
        Modal = 1
    }

    internal sealed class LegacyUiOverlayRequest
    {
        public string Id { get; set; }
        public string OwnerPageId { get; set; }
        public LegacyUiRect Bounds { get; set; }
        public LegacyUiOverlayKind Kind { get; set; }
        public int ZIndex { get; set; }
        public int CacheSignature { get; set; }
        public Action<LegacyUiContext, LegacyUiOverlayRequest> Draw { get; set; }
        public Func<LegacyMouseSnapshot, int, bool> TryConsumeScroll { get; set; }

        public LegacyUiOverlayRequest()
        {
            Id = string.Empty;
            OwnerPageId = string.Empty;
            Kind = LegacyUiOverlayKind.Popup;
        }

        public bool Modal
        {
            get { return Kind == LegacyUiOverlayKind.Modal; }
        }
    }

    internal sealed class LegacyUiOverlayCoordinator
    {
        public static readonly LegacyUiOverlayCoordinator Current = new LegacyUiOverlayCoordinator();

        private readonly List<LegacyUiOverlayRequest> _requests = new List<LegacyUiOverlayRequest>(8);
        private readonly List<LegacyUiOverlayRange> _ranges = new List<LegacyUiOverlayRange>(8);
        private readonly List<LegacyUiOverlayRequest> _sorted = new List<LegacyUiOverlayRequest>(8);
        private readonly List<LegacyUiOverlayRequest> _lastRequests = new List<LegacyUiOverlayRequest>(8);
        private string _activePageId = string.Empty;
        private int _nextSequence;
        private int _lastStackSignature;

        public int LastStackSignature
        {
            get { return _lastStackSignature; }
        }

        public void BeginFrame(string selectedPageId)
        {
            _activePageId = selectedPageId ?? string.Empty;
            _requests.Clear();
            _ranges.Clear();
            _sorted.Clear();
            _nextSequence = 0;
        }

        public bool Register(LegacyUiOverlayRequest request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.Id) ||
                request.Bounds.Width <= 0 ||
                request.Bounds.Height <= 0)
            {
                return false;
            }

            var owner = string.IsNullOrWhiteSpace(request.OwnerPageId) ? _activePageId : request.OwnerPageId;
            if (!string.IsNullOrWhiteSpace(owner) &&
                !string.IsNullOrWhiteSpace(_activePageId) &&
                !string.Equals(owner, _activePageId, StringComparison.Ordinal))
            {
                return false;
            }

            _requests.Add(new LegacyUiOverlayRequest
            {
                Id = request.Id,
                OwnerPageId = owner ?? string.Empty,
                Bounds = request.Bounds,
                Kind = request.Kind,
                ZIndex = request.ZIndex,
                CacheSignature = request.CacheSignature,
                Draw = request.Draw,
                TryConsumeScroll = request.TryConsumeScroll
            });
            return true;
        }

        public void DrawOverlays(
            object spriteBatch,
            LegacyMouseSnapshot mouse,
            LegacyUiRect windowRect,
            string selectedPageId,
            AppSettings settings,
            IList<LegacyUiElement> elements)
        {
            if (_requests.Count <= 0)
            {
                return;
            }

            _sorted.Clear();
            for (var index = 0; index < _requests.Count; index++)
            {
                _sorted.Add(_requests[index]);
            }

            _sorted.Sort(CompareRequestsForDraw);
            for (var index = 0; index < _sorted.Count; index++)
            {
                var request = _sorted[index];
                var start = elements == null ? 0 : elements.Count;
                string blockerId = null;
                if (request.Modal)
                {
                    blockerId = request.Id + ":modal-blocker";
                    LegacyUiElementFrame.Add(
                        elements,
                        blockerId,
                        request.Id,
                        "blocker",
                        request.Bounds,
                        true,
                        false,
                        0,
                        0,
                        0,
                        null,
                        null,
                        null);
                }

                if (request.Draw != null)
                {
                    // Overlay callbacks run after page content so visual and hit-test
                    // order share one top-level contract instead of page append order.
                    var context = new LegacyUiContext(
                        spriteBatch,
                        mouse,
                        windowRect,
                        selectedPageId,
                        settings,
                        elements);
                    request.Draw(context, request);
                }

                var end = elements == null ? start : elements.Count;
                _ranges.Add(new LegacyUiOverlayRange(request, start, end, blockerId, _nextSequence++));
            }
        }

        public LegacyUiElement ResolveHoveredElement(
            LegacyUiElement preferred,
            IList<LegacyUiElement> elements,
            LegacyMouseSnapshot mouse)
        {
            var overlayElement = FindTopElement(elements, mouse);
            return overlayElement ?? preferred;
        }

        public bool TryResolveClickElement(
            IList<LegacyUiElement> elements,
            LegacyMouseSnapshot mouse,
            out LegacyUiElement element,
            out bool blocked)
        {
            element = FindTopElement(elements, mouse);
            blocked = element != null &&
                      (string.Equals(element.Kind, "blocker", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(element.Kind, "slider", StringComparison.OrdinalIgnoreCase));
            return element != null;
        }

        public bool ShouldBlockMainScroll(LegacyMouseSnapshot mouse, int scrollDelta)
        {
            return TryConsumeOrBlockScroll(_requests, mouse, scrollDelta) ||
                   (_requests.Count <= 0 && TryConsumeOrBlockScroll(_lastRequests, mouse, scrollDelta));
        }

        public void EndFrame()
        {
            _lastStackSignature = BuildStackSignature(_requests);
            _lastRequests.Clear();
            for (var index = 0; index < _requests.Count; index++)
            {
                var request = _requests[index];
                _lastRequests.Add(new LegacyUiOverlayRequest
                {
                    Id = request.Id,
                    OwnerPageId = request.OwnerPageId,
                    Bounds = request.Bounds,
                    Kind = request.Kind,
                    ZIndex = request.ZIndex,
                    CacheSignature = request.CacheSignature,
                    TryConsumeScroll = request.TryConsumeScroll
                });
            }
        }

        internal static int BuildStackSignatureForTesting(IList<LegacyUiOverlayRequest> requests)
        {
            return BuildStackSignature(requests);
        }

        internal LegacyUiElement FindTopElementForTesting(IList<LegacyUiElement> elements, LegacyMouseSnapshot mouse)
        {
            return FindTopElement(elements, mouse);
        }

        internal void ResetForTesting()
        {
            _requests.Clear();
            _ranges.Clear();
            _sorted.Clear();
            _lastRequests.Clear();
            _activePageId = string.Empty;
            _nextSequence = 0;
            _lastStackSignature = 0;
        }

        private LegacyUiElement FindTopElement(IList<LegacyUiElement> elements, LegacyMouseSnapshot mouse)
        {
            if (mouse == null || elements == null || elements.Count <= 0 || _ranges.Count <= 0)
            {
                return null;
            }

            for (var rangeIndex = _ranges.Count - 1; rangeIndex >= 0; rangeIndex--)
            {
                var range = _ranges[rangeIndex];
                var end = Math.Min(range.EndElementIndex, elements.Count);
                for (var elementIndex = end - 1; elementIndex >= range.StartElementIndex; elementIndex--)
                {
                    var element = elements[elementIndex];
                    if (element != null && element.Enabled && element.Rect.Contains(mouse.X, mouse.Y))
                    {
                        return element;
                    }
                }

                if (range.Request.Modal && range.Request.Bounds.Contains(mouse.X, mouse.Y))
                {
                    return FindBlockerElement(elements, range);
                }
            }

            return null;
        }

        private static LegacyUiElement FindBlockerElement(IList<LegacyUiElement> elements, LegacyUiOverlayRange range)
        {
            if (elements == null || string.IsNullOrWhiteSpace(range.BlockerElementId))
            {
                return null;
            }

            var end = Math.Min(range.EndElementIndex, elements.Count);
            for (var index = range.StartElementIndex; index < end; index++)
            {
                var element = elements[index];
                if (element != null && string.Equals(element.Id, range.BlockerElementId, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            return null;
        }

        private static bool TryConsumeOrBlockScroll(IList<LegacyUiOverlayRequest> requests, LegacyMouseSnapshot mouse, int scrollDelta)
        {
            if (requests == null || mouse == null || scrollDelta == 0)
            {
                return false;
            }

            LegacyUiOverlayRequest best = null;
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                if (request == null ||
                    !request.Modal ||
                    !request.Bounds.Contains(mouse.X, mouse.Y) ||
                    (best != null && request.ZIndex < best.ZIndex))
                {
                    continue;
                }

                best = request;
            }

            if (best == null)
            {
                return false;
            }

            if (best.TryConsumeScroll != null && best.TryConsumeScroll(mouse, scrollDelta))
            {
                return true;
            }

            return true;
        }

        private static int CompareRequestsForDraw(LegacyUiOverlayRequest left, LegacyUiOverlayRequest right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            var z = left.ZIndex.CompareTo(right.ZIndex);
            if (z != 0)
            {
                return z;
            }

            return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
        }

        private static int BuildStackSignature(IList<LegacyUiOverlayRequest> requests)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, requests == null ? 0 : requests.Count);
                if (requests != null)
                {
                    for (var index = 0; index < requests.Count; index++)
                    {
                        var request = requests[index];
                        if (request == null)
                        {
                            AddHash(ref hash, 0);
                            continue;
                        }

                        AddHash(ref hash, request.Id);
                        AddHash(ref hash, request.OwnerPageId);
                        AddHash(ref hash, request.Bounds.X);
                        AddHash(ref hash, request.Bounds.Y);
                        AddHash(ref hash, request.Bounds.Width);
                        AddHash(ref hash, request.Bounds.Height);
                        AddHash(ref hash, request.Modal);
                        AddHash(ref hash, request.ZIndex);
                        AddHash(ref hash, request.CacheSignature);
                    }
                }

                return hash;
            }
        }

        private static void AddHash(ref int hash, int value)
        {
            hash = hash * 31 + value;
        }

        private static void AddHash(ref int hash, bool value)
        {
            hash = hash * 31 + (value ? 1 : 0);
        }

        private static void AddHash(ref int hash, string value)
        {
            hash = hash * 31 + StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
        }

        private sealed class LegacyUiOverlayRange
        {
            public LegacyUiOverlayRange(
                LegacyUiOverlayRequest request,
                int startElementIndex,
                int endElementIndex,
                string blockerElementId,
                int sequence)
            {
                Request = request;
                StartElementIndex = startElementIndex;
                EndElementIndex = endElementIndex;
                BlockerElementId = blockerElementId ?? string.Empty;
                Sequence = sequence;
            }

            public LegacyUiOverlayRequest Request { get; private set; }
            public int StartElementIndex { get; private set; }
            public int EndElementIndex { get; private set; }
            public string BlockerElementId { get; private set; }
            public int Sequence { get; private set; }
        }
    }
}
