using System;
using System.Collections.Generic;
using System.Threading;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static class LegacyUiElementFrame
    {
        private static readonly List<LegacyUiElement> Pool = new List<LegacyUiElement>(128);
        private static int _nextPoolIndex;
        private static bool _active;
        private static int _lastFrameElementCount;
        private static long _hoverReuseCount;

        private static bool _hoverFrameActive;
        private static bool _hoverReuseActive;
        private static bool _hoverResolved;
        private static LegacyUiElement _currentHoveredElement;
        private static string _currentHoverId = string.Empty;
        private static int _currentMouseX;
        private static int _currentMouseY;
        private static bool _currentLeftDown;
        private static bool _currentLeftPressed;
        private static bool _currentLeftReleased;
        private static int _currentScrollDelta;
        private static int _currentLayoutToken;
        private static string _currentActiveMode = string.Empty;

        private static bool _lastHoverValid;
        private static string _lastHoverId = string.Empty;
        private static int _lastMouseX;
        private static int _lastMouseY;
        private static bool _lastLeftDown;
        private static bool _lastLeftPressed;
        private static bool _lastLeftReleased;
        private static int _lastScrollDelta;
        private static int _lastLayoutToken;
        private static string _lastActiveMode = string.Empty;

        public static bool HoverReuseActive
        {
            get { return _hoverReuseActive; }
        }

        public static int CreatedCount
        {
            get { return Pool.Count; }
        }

        public static int LastFrameElementCount
        {
            get { return _lastFrameElementCount; }
        }

        public static long HoverReuseCount
        {
            get { return Interlocked.Read(ref _hoverReuseCount); }
        }

        public static void BeginElementFrame()
        {
            _nextPoolIndex = 0;
            _active = true;
            _hoverFrameActive = false;
            _hoverReuseActive = false;
            _hoverResolved = false;
            _currentHoveredElement = null;
            _currentHoverId = string.Empty;
        }

        public static void EndElementFrame()
        {
            _lastFrameElementCount = _active ? _nextPoolIndex : 0;
            _active = false;
            _hoverFrameActive = false;
            _hoverReuseActive = false;
            _hoverResolved = false;
            _currentHoveredElement = null;
            _currentHoverId = string.Empty;
        }

        public static void BeginHoverFrame(LegacyMouseSnapshot mouse, int layoutToken, string activeMode)
        {
            _hoverFrameActive = true;
            _hoverResolved = false;
            _currentHoveredElement = null;
            _currentHoverId = _lastHoverId ?? string.Empty;
            _currentMouseX = mouse == null ? -1 : mouse.X;
            _currentMouseY = mouse == null ? -1 : mouse.Y;
            _currentLeftDown = mouse != null && mouse.LeftDown;
            _currentLeftPressed = mouse != null && mouse.LeftPressed;
            _currentLeftReleased = mouse != null && mouse.LeftReleased;
            _currentScrollDelta = mouse == null ? 0 : mouse.ScrollDelta;
            _currentLayoutToken = layoutToken;
            _currentActiveMode = activeMode ?? string.Empty;
            _hoverReuseActive =
                mouse != null &&
                _lastHoverValid &&
                !string.IsNullOrWhiteSpace(_currentHoverId) &&
                _lastMouseX == _currentMouseX &&
                _lastMouseY == _currentMouseY &&
                _lastLeftDown == _currentLeftDown &&
                _lastLeftPressed == _currentLeftPressed &&
                _lastLeftReleased == _currentLeftReleased &&
                _lastScrollDelta == _currentScrollDelta &&
                _lastLayoutToken == _currentLayoutToken &&
                string.Equals(_lastActiveMode, _currentActiveMode, StringComparison.Ordinal);
            if (_hoverReuseActive)
            {
                Interlocked.Increment(ref _hoverReuseCount);
            }
        }

        public static LegacyUiElement Acquire()
        {
            if (!_active)
            {
                return new LegacyUiElement();
            }

            if (_nextPoolIndex >= Pool.Count)
            {
                Pool.Add(new LegacyUiElement());
            }

            return Pool[_nextPoolIndex++];
        }

        public static LegacyUiElement Add(
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
            BuffPotionWhitelistEntry whitelistEntry)
        {
            var element = Acquire();
            element.Reset(id, label, kind, rect, enabled, selected, intValue, minValue, maxValue, tooltipLines, candidate, whitelistEntry);
            if (elements != null)
            {
                elements.Add(element);
            }

            return element;
        }

        public static bool IsHovered(string id, LegacyUiRect rect, LegacyMouseSnapshot mouse)
        {
            if (mouse == null || rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            if (_hoverFrameActive && _hoverReuseActive && !string.Equals(id ?? string.Empty, _currentHoverId, StringComparison.Ordinal))
            {
                return false;
            }

            return rect.Contains(mouse.X, mouse.Y);
        }

        public static void RecordHover(LegacyUiElement element, bool hovered)
        {
            if (!_hoverFrameActive || element == null || !hovered)
            {
                return;
            }

            _currentHoveredElement = element;
            if (_hoverReuseActive && string.Equals(element.Id, _currentHoverId, StringComparison.Ordinal))
            {
                _hoverResolved = true;
            }
        }

        public static LegacyUiElement ResolveHoveredElement(
            LegacyUiElement preferred,
            IList<LegacyUiElement> elements,
            LegacyMouseSnapshot mouse)
        {
            var hovered = preferred ?? _currentHoveredElement;
            if (_hoverFrameActive && _hoverReuseActive && !_hoverResolved && hovered == null)
            {
                hovered = FindHoveredElement(elements, mouse);
            }

            if (_hoverFrameActive)
            {
                _lastHoverValid = mouse != null;
                _lastHoverId = hovered == null ? string.Empty : hovered.Id ?? string.Empty;
                _lastMouseX = _currentMouseX;
                _lastMouseY = _currentMouseY;
                _lastLeftDown = _currentLeftDown;
                _lastLeftPressed = _currentLeftPressed;
                _lastLeftReleased = _currentLeftReleased;
                _lastScrollDelta = _currentScrollDelta;
                _lastLayoutToken = _currentLayoutToken;
                _lastActiveMode = _currentActiveMode;
            }

            return hovered;
        }

        public static void ResetForTesting()
        {
            Pool.Clear();
            _nextPoolIndex = 0;
            _active = false;
            _hoverFrameActive = false;
            _hoverReuseActive = false;
            _hoverResolved = false;
            _currentHoveredElement = null;
            _currentHoverId = string.Empty;
            _lastHoverValid = false;
            _lastHoverId = string.Empty;
            _lastActiveMode = string.Empty;
            _lastFrameElementCount = 0;
            Interlocked.Exchange(ref _hoverReuseCount, 0);
        }

        private static LegacyUiElement FindHoveredElement(IList<LegacyUiElement> elements, LegacyMouseSnapshot mouse)
        {
            if (elements == null || mouse == null)
            {
                return null;
            }

            for (var index = elements.Count - 1; index >= 0; index--)
            {
                var element = elements[index];
                if (element != null && element.Rect.Contains(mouse.X, mouse.Y))
                {
                    return element;
                }
            }

            return null;
        }
    }
}
