using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void LegacyUiOverlayCoordinatorDrawsAfterPageContent()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(10, 10, 80, 24))
            };
            var drawSawPageContent = false;

            coordinator.BeginFrame("misc");
            coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "test-overlay",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(20, 20, 100, 80),
                Kind = LegacyUiOverlayKind.Modal,
                ZIndex = 10,
                CacheSignature = 1,
                Draw = (context, request) =>
                {
                    drawSawPageContent = context.Elements.Count == 2;
                    context.RegisterElement("test-overlay:child", "Child", "button", new LegacyUiRect(30, 30, 60, 20), true, false, 0, 0, 0, null);
                }
            });
            coordinator.DrawOverlays(null, new LegacyMouseSnapshot { X = 35, Y = 35, ReadAvailable = true }, new LegacyUiRect(0, 0, 200, 200), "misc", AppSettings.CreateDefault(), elements);
            coordinator.EndFrame();

            if (!drawSawPageContent)
            {
                throw new InvalidOperationException("Expected overlay drawing to run after page content and its modal blocker registration.");
            }

            if (elements.Count != 3 ||
                elements[0].Id != "page:button" ||
                elements[1].Id != "test-overlay:modal-blocker" ||
                elements[2].Id != "test-overlay:child")
            {
                throw new InvalidOperationException("Expected overlay elements to be appended above normal page elements with blocker before children.");
            }

            coordinator.ResetForTesting();
        }

        private static void LegacyUiOverlayModalBlockerStopsLowerHoverAndClick()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            var mouse = new LegacyMouseSnapshot
            {
                X = 35,
                Y = 35,
                LeftPressed = true,
                ReadAvailable = true
            };
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(20, 20, 100, 80))
            };

            coordinator.BeginFrame("misc");
            coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "modal",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(20, 20, 100, 80),
                Kind = LegacyUiOverlayKind.Modal,
                CacheSignature = 2
            });
            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 200, 200), "misc", AppSettings.CreateDefault(), elements);

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != "modal:modal-blocker")
            {
                throw new InvalidOperationException("Expected modal blocker to own hover before the lower page button.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected modal blocker to stop click dispatch to the lower page button.");
            }

            coordinator.ResetForTesting();
        }

        private static void LegacyUiOverlayChildControlsBeatModalBlocker()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            var mouse = new LegacyMouseSnapshot
            {
                X = 45,
                Y = 45,
                LeftPressed = true,
                ReadAvailable = true
            };
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(20, 20, 120, 80))
            };

            coordinator.BeginFrame("misc");
            coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "modal",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(20, 20, 120, 80),
                Kind = LegacyUiOverlayKind.Modal,
                CacheSignature = 3,
                Draw = (context, request) =>
                {
                    context.RegisterElement("modal:child", "Child", "button", new LegacyUiRect(35, 35, 50, 24), true, false, 0, 0, 0, null);
                }
            });
            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 200, 200), "misc", AppSettings.CreateDefault(), elements);

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != "modal:child")
            {
                throw new InvalidOperationException("Expected modal child controls to win hover over the modal blocker.");
            }

            if (blocked || clickId != "modal:child")
            {
                throw new InvalidOperationException("Expected modal child controls to receive click before the modal blocker.");
            }

            coordinator.ResetForTesting();
        }

        private static void LegacyUiOverlayModalBlocksMainScroll()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            var mouse = new LegacyMouseSnapshot
            {
                X = 45,
                Y = 45,
                ScrollDelta = -120,
                ReadAvailable = true
            };
            var innerScrollConsumed = false;

            coordinator.BeginFrame("misc");
            coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "modal-scroll",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(20, 20, 120, 80),
                Kind = LegacyUiOverlayKind.Modal,
                CacheSignature = 4,
                TryConsumeScroll = (snapshot, delta) =>
                {
                    innerScrollConsumed = delta == -120 && snapshot.X == mouse.X;
                    return true;
                }
            });

            if (!coordinator.ShouldBlockMainScroll(mouse, mouse.ScrollDelta) || !innerScrollConsumed)
            {
                throw new InvalidOperationException("Expected modal overlay scroll viewport to consume wheel before the main page scrolls.");
            }

            coordinator.EndFrame();
            coordinator.BeginFrame("misc");
            if (!coordinator.ShouldBlockMainScroll(mouse, mouse.ScrollDelta))
            {
                throw new InvalidOperationException("Expected the previous overlay stack to keep blocking main scroll before the next draw registers requests.");
            }

            coordinator.EndFrame();
            coordinator.ResetForTesting();
        }

        private static void LegacyUiOverlayStackDirtiesHoverTokenAndRetainedFrame()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetRetainedFrameModelForTesting();
            LegacyMainWindow.ResetPageLayoutCacheForTesting();

            var settings = AppSettings.CreateDefault();
            var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
            var content = new LegacyUiRect(58, 134, 520, 180);
            var area = LegacyScrollArea.Create(content, 700, 24);
            var firstToken = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("misc", window, content, area, settings);
            var firstFrame = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting(
                "misc",
                window,
                content,
                area,
                settings,
                700,
                new List<LegacyUiElement> { CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(20, 20, 80, 24)) });

            coordinator.BeginFrame("misc");
            coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "modal-signature",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(20, 20, 120, 80),
                Kind = LegacyUiOverlayKind.Modal,
                CacheSignature = 5
            });
            coordinator.EndFrame();

            var changedToken = LegacyMainWindow.BuildFrameHoverLayoutTokenForTesting("misc", window, content, area, settings);
            var changedFrame = LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting(
                "misc",
                window,
                content,
                area,
                settings,
                700,
                new List<LegacyUiElement> { CreateLegacyUiElementForTesting("page:button", "Page", "button", new LegacyUiRect(20, 20, 80, 24)) });

            if (changedToken == firstToken)
            {
                throw new InvalidOperationException("Expected opening a modal overlay to dirty the hover layout token.");
            }

            if (changedFrame.CacheMissCount <= firstFrame.CacheMissCount ||
                changedFrame.CacheHitCount != firstFrame.CacheHitCount)
            {
                throw new InvalidOperationException("Expected opening a modal overlay to dirty the retained frame model signature.");
            }

            coordinator.ResetForTesting();
        }

        private static void LegacyAutoCaptureConfigOverlayBlocksLowerHoverAndClick()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoCaptureCritterConfigPopupForTesting();
            var viewport = new LegacyUiRect(20, 20, 360, 180);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var anchor = new LegacyUiRect(300, 60, 50, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("misc-auto-harvest-mode:On", "自动收获:开启", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("misc");
            if (!LegacyMainWindow.RegisterAutoCaptureCritterConfigPopupOverlayForTesting(area, anchor))
            {
                throw new InvalidOperationException("Expected auto capture config popup to register as a modal overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 500, 300), "misc", AppSettings.CreateDefault(), elements);
            var popup = FindLegacyUiElementForTesting(elements, "misc-auto-capture-critter-config-popup");
            mouse.X = popup.Rect.X + 12;
            mouse.Y = popup.Rect.Y + 38;

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || string.Equals(hovered.Id, "misc-auto-harvest-mode:On", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto capture modal overlay to own hover above later misc rows.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected auto capture modal overlay to block clicks from reaching later misc rows.");
            }

            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoCaptureCritterConfigPopupForTesting();
        }

        private static void LegacyAutoCaptureConfigOverlayCheckboxStaysClickable()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoCaptureCritterConfigPopupForTesting();
            var viewport = new LegacyUiRect(20, 20, 360, 180);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var anchor = new LegacyUiRect(300, 60, 50, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("misc-quick-bag-open-mode:On", "持续开袋:开启", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("misc");
            if (!LegacyMainWindow.RegisterAutoCaptureCritterConfigPopupOverlayForTesting(area, anchor))
            {
                throw new InvalidOperationException("Expected auto capture config popup to register as a modal overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 500, 300), "misc", AppSettings.CreateDefault(), elements);
            var option = FindLegacyUiElementForTesting(elements, "misc-auto-capture-critter-option:bait");
            mouse.X = option.Rect.X + Math.Max(1, option.Rect.Width / 2);
            mouse.Y = option.Rect.Y + Math.Max(1, option.Rect.Height / 2);

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != "misc-auto-capture-critter-option:bait")
            {
                throw new InvalidOperationException("Expected auto capture category checkbox to win hover over the modal blocker.");
            }

            if (blocked || clickId != "misc-auto-capture-critter-option:bait")
            {
                throw new InvalidOperationException("Expected auto capture category checkbox to remain clickable inside the modal overlay.");
            }

            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoCaptureCritterConfigPopupForTesting();
        }

        private static LegacyUiElement FindLegacyUiElementForTesting(IList<LegacyUiElement> elements, string id)
        {
            if (elements != null)
            {
                for (var index = 0; index < elements.Count; index++)
                {
                    var element = elements[index];
                    if (element != null && string.Equals(element.Id, id, StringComparison.Ordinal))
                    {
                        return element;
                    }
                }
            }

            throw new InvalidOperationException("Expected legacy UI element was not registered: " + id);
        }
    }
}
