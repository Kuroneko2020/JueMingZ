using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void LegacyUiElementFrameReusesPooledElements()
        {
            LegacyUiElementFrame.ResetForTesting();
            var elements = new List<LegacyUiElement>();

            LegacyUiElementFrame.BeginElementFrame();
            var first = LegacyUiElementFrame.Add(
                elements,
                "first",
                "First",
                "button",
                new LegacyUiRect(10, 10, 20, 20),
                true,
                true,
                7,
                1,
                9,
                new[] { "old" },
                null,
                null);
            LegacyUiElementFrame.EndElementFrame();

            elements.Clear();
            LegacyUiElementFrame.BeginElementFrame();
            var second = LegacyUiElementFrame.Add(
                elements,
                "second",
                "Second",
                "label",
                new LegacyUiRect(30, 30, 40, 40),
                false,
                false,
                0,
                0,
                0,
                null,
                null,
                null);
            LegacyUiElementFrame.EndElementFrame();

            if (!object.ReferenceEquals(first, second))
            {
                throw new InvalidOperationException("Expected the second frame to reuse the first frame element instance.");
            }

            if (LegacyUiElementFrame.CreatedCount != 1)
            {
                throw new InvalidOperationException("Expected the pool to allocate only one element for identical frame demand.");
            }

            if (LegacyUiElementFrame.LastFrameElementCount != 1)
            {
                throw new InvalidOperationException("Expected the frame diagnostics to keep the last registered element count.");
            }

            if (second.Id != "second" ||
                second.Label != "Second" ||
                second.Kind != "label" ||
                second.Enabled ||
                second.Selected ||
                second.IntValue != 0 ||
                second.TooltipLines != null)
            {
                throw new InvalidOperationException("Expected pooled elements to reset all per-control state before reuse.");
            }
        }

        private static void LegacyUiHoverCacheReusesStableHoverId()
        {
            LegacyUiElementFrame.ResetForTesting();
            var mouse = new LegacyMouseSnapshot
            {
                X = 15,
                Y = 15,
                ReadAvailable = true
            };
            var elements = new List<LegacyUiElement>();

            LegacyUiElementFrame.BeginElementFrame();
            LegacyUiElementFrame.BeginHoverFrame(mouse, 42, string.Empty);
            var first = LegacyUiElementFrame.Add(elements, "hover-a", "A", "button", new LegacyUiRect(10, 10, 20, 20), true, false, 0, 0, 0, null, null, null);
            var firstHovered = LegacyUiElementFrame.IsHovered(first.Id, first.Rect, mouse);
            LegacyUiElementFrame.RecordHover(first, firstHovered);
            var resolvedFirst = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse);
            LegacyUiElementFrame.EndElementFrame();

            if (!firstHovered || !object.ReferenceEquals(first, resolvedFirst))
            {
                throw new InvalidOperationException("Expected first hover frame to resolve the element under the mouse.");
            }

            elements.Clear();
            LegacyUiElementFrame.BeginElementFrame();
            LegacyUiElementFrame.BeginHoverFrame(mouse, 42, string.Empty);
            if (!LegacyUiElementFrame.HoverReuseActive)
            {
                throw new InvalidOperationException("Expected identical mouse and layout state to enable hover reuse.");
            }

            if (LegacyUiElementFrame.HoverReuseCount != 1)
            {
                throw new InvalidOperationException("Expected hover reuse diagnostics to count stable hover frames.");
            }

            var nonMatching = LegacyUiElementFrame.Add(elements, "hover-b", "B", "button", new LegacyUiRect(10, 10, 20, 20), true, false, 0, 0, 0, null, null, null);
            if (LegacyUiElementFrame.IsHovered(nonMatching.Id, nonMatching.Rect, mouse))
            {
                throw new InvalidOperationException("Expected hover reuse to skip non-matching element ids under the same point.");
            }

            var matching = LegacyUiElementFrame.Add(elements, "hover-a", "A", "button", new LegacyUiRect(10, 10, 20, 20), true, false, 0, 0, 0, null, null, null);
            var matchingHovered = LegacyUiElementFrame.IsHovered(matching.Id, matching.Rect, mouse);
            LegacyUiElementFrame.RecordHover(matching, matchingHovered);
            var resolvedSecond = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse);
            LegacyUiElementFrame.EndElementFrame();

            if (!matchingHovered || !object.ReferenceEquals(matching, resolvedSecond))
            {
                throw new InvalidOperationException("Expected hover reuse to resolve the current-frame element with the cached id.");
            }
        }

        private static void LegacyUiContextHoverUsesCachedElementId()
        {
            LegacyUiElementFrame.ResetForTesting();
            var mouse = new LegacyMouseSnapshot
            {
                X = 15,
                Y = 15,
                ReadAvailable = true
            };
            var elements = new List<LegacyUiElement>();

            LegacyUiElementFrame.BeginElementFrame();
            LegacyUiElementFrame.BeginHoverFrame(mouse, 7, string.Empty);
            var firstContext = new LegacyUiContext(null, mouse, new LegacyUiRect(0, 0, 100, 100), "test", null, elements);
            firstContext.RegisterElement("hover-a", "A", "button", new LegacyUiRect(10, 10, 20, 20), true, false, 0, 0, 0, null);
            LegacyUiElementFrame.ResolveHoveredElement(firstContext.HoveredElement, elements, mouse);
            LegacyUiElementFrame.EndElementFrame();

            elements.Clear();
            LegacyUiElementFrame.BeginElementFrame();
            LegacyUiElementFrame.BeginHoverFrame(mouse, 7, string.Empty);
            if (!LegacyUiElementFrame.HoverReuseActive)
            {
                throw new InvalidOperationException("Expected identical context hover input to enable reuse.");
            }

            var secondContext = new LegacyUiContext(null, mouse, new LegacyUiRect(0, 0, 100, 100), "test", null, elements);
            if (secondContext.IsElementHovered("hover-b", new LegacyUiRect(10, 10, 20, 20)))
            {
                throw new InvalidOperationException("Expected context hover checks to skip non-cached ids while reuse is active.");
            }

            var matching = secondContext.IsElementHovered("hover-a", new LegacyUiRect(10, 10, 20, 20));
            LegacyUiElementFrame.EndElementFrame();

            if (!matching)
            {
                throw new InvalidOperationException("Expected context hover checks to accept the cached visible element id.");
            }
        }

        private static void LegacyUiTooltipCacheReusesStableHoverModel()
        {
            LegacyMainWindow.ResetHoverTooltipCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var element = new LegacyUiElement
            {
                Id = "tooltip:test",
                Label = "Tooltip test",
                Kind = "button",
                TooltipLines = new[] { "same tooltip" },
                TooltipContentSignature = 101
            };

            var first = LegacyMainWindow.BuildTooltipModelForTesting(element, "misc", settings);
            var second = LegacyMainWindow.BuildTooltipModelForTesting(element, "misc", settings);

            if (first == null || second == null || !object.ReferenceEquals(first, second))
            {
                throw new InvalidOperationException("Expected stable hover tooltip model to be reused.");
            }

            if (LegacyMainWindow.HoverTooltipCacheMissCount != 1 ||
                LegacyMainWindow.HoverTooltipCacheHitCount != 1 ||
                LegacyMainWindow.HoverTooltipDiagnosticSuppressedCount != 1)
            {
                throw new InvalidOperationException("Expected tooltip cache diagnostics to count one miss, one hit, and one suppressed stable-hover diagnostic.");
            }
        }

        private static void LegacyUiTooltipCacheDirtiesOnContentChange()
        {
            LegacyMainWindow.ResetHoverTooltipCacheForTesting();
            var settings = AppSettings.CreateDefault();
            var firstElement = new LegacyUiElement
            {
                Id = "tooltip:test",
                Label = "Tooltip test",
                Kind = "button",
                TooltipLines = new[] { "first tooltip" },
                TooltipContentSignature = 201
            };
            var changedElement = new LegacyUiElement
            {
                Id = "tooltip:test",
                Label = "Tooltip test",
                Kind = "button",
                TooltipLines = new[] { "changed tooltip" },
                TooltipContentSignature = 202
            };

            var first = LegacyMainWindow.BuildTooltipModelForTesting(firstElement, "misc", settings);
            var changed = LegacyMainWindow.BuildTooltipModelForTesting(changedElement, "misc", settings);

            if (first == null || changed == null || object.ReferenceEquals(first, changed))
            {
                throw new InvalidOperationException("Expected tooltip content changes to rebuild the cached model.");
            }

            if (changed.Lines.Length != 1 || changed.Lines[0] != "changed tooltip")
            {
                throw new InvalidOperationException("Expected rebuilt tooltip model to expose the changed content immediately.");
            }

            if (LegacyMainWindow.HoverTooltipCacheMissCount != 2 ||
                LegacyMainWindow.HoverTooltipCacheHitCount != 0)
            {
                throw new InvalidOperationException("Expected tooltip content changes to be recorded as cache misses, not stale hits.");
            }
        }
    }
}
