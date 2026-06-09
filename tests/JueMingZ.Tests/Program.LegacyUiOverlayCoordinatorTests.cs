using System;
using System.Collections.Generic;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
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

        private static void LegacyUiOverlayRequestRejectsInvalidContract()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            coordinator.BeginFrame("misc");

            if (coordinator.Register(null))
            {
                throw new InvalidOperationException("Expected null overlay requests to be rejected.");
            }

            if (coordinator.Register(new LegacyUiOverlayRequest
            {
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(10, 10, 80, 40),
                Kind = LegacyUiOverlayKind.Modal
            }))
            {
                throw new InvalidOperationException("Expected overlay requests without a stable id to be rejected.");
            }

            if (coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "zero-bounds",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(10, 10, 0, 40),
                Kind = LegacyUiOverlayKind.Modal
            }))
            {
                throw new InvalidOperationException("Expected overlay requests without visible bounds to be rejected.");
            }

            if (coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "stale-page",
                OwnerPageId = "fishing",
                Bounds = new LegacyUiRect(10, 10, 80, 40),
                Kind = LegacyUiOverlayKind.Modal
            }))
            {
                throw new InvalidOperationException("Expected overlay requests from inactive pages to be rejected.");
            }

            if (!coordinator.Register(new LegacyUiOverlayRequest
            {
                Id = "valid-modal",
                OwnerPageId = "misc",
                Bounds = new LegacyUiRect(10, 10, 80, 40),
                Kind = LegacyUiOverlayKind.Modal,
                CacheSignature = 42
            }))
            {
                throw new InvalidOperationException("Expected a stable active-page overlay request to be accepted.");
            }

            coordinator.EndFrame();
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

        private static void LegacyInformationStyleOverlayBlocksLowerHoverAndKeepsButtonClickable()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetInformationStylePopupForTesting();
            var viewport = new LegacyUiRect(20, 20, 460, 300);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var anchor = new LegacyUiRect(360, 130, 64, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("information-toggle:lower:On", "Lower", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("information");
            if (!LegacyMainWindow.RegisterInformationStylePopupOverlayForTesting(area, anchor, InformationStyleHelper.EnemyNameFeatureId))
            {
                throw new InvalidOperationException("Expected information style popup to register as a modal overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 560, 380), "information", AppSettings.CreateDefault(), elements);
            var popup = FindLegacyUiElementForTesting(elements, "information-style-popup");
            mouse.X = popup.Rect.X + 12;
            mouse.Y = popup.Rect.Y + 38;

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || string.Equals(hovered.Id, "information-toggle:lower:On", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected information style overlay to own hover above lower rows.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected information style overlay to block lower row clicks.");
            }

            var reset = FindLegacyUiElementForTesting(elements, "information-style-reset:" + InformationStyleHelper.EnemyNameFeatureId);
            mouse.X = reset.Rect.X + Math.Max(1, reset.Rect.Width / 2);
            mouse.Y = reset.Rect.Y + Math.Max(1, reset.Rect.Height / 2);
            hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != reset.Id)
            {
                throw new InvalidOperationException("Expected information style reset button to win hover over the modal blocker.");
            }

            if (blocked || clickId != reset.Id)
            {
                throw new InvalidOperationException("Expected information style reset button to remain clickable inside the overlay.");
            }

            coordinator.ResetForTesting();
            LegacyMainWindow.ResetInformationStylePopupForTesting();
        }

        private static void LegacyAutoRecoveryConfigOverlayBlocksLowerHoverAndKeepsCloseClickable()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoRecoveryItemConfigPopupForTesting();
            var viewport = new LegacyUiRect(20, 20, 460, 300);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var anchor = new LegacyUiRect(360, 70, 64, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("auto-buff-mode:On", "Lower", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("buff");
            if (!LegacyMainWindow.RegisterAutoRecoveryItemConfigPopupOverlayForTesting(area, anchor, "heal"))
            {
                throw new InvalidOperationException("Expected auto recovery item config popup to register as a modal overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 560, 380), "buff", AppSettings.CreateDefault(), elements);
            var popup = FindLegacyUiElementForTesting(elements, "auto-recovery-item-config-popup");
            mouse.X = popup.Rect.X + 12;
            mouse.Y = popup.Rect.Y + 38;

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || string.Equals(hovered.Id, "auto-buff-mode:On", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto recovery modal overlay to own hover above lower buff rows.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected auto recovery modal overlay to block lower buff row clicks.");
            }

            var close = FindLegacyUiElementForTesting(elements, "auto-recovery-item-config:heal");
            mouse.X = close.Rect.X + Math.Max(1, close.Rect.Width / 2);
            mouse.Y = close.Rect.Y + Math.Max(1, close.Rect.Height / 2);
            hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != close.Id)
            {
                throw new InvalidOperationException("Expected auto recovery close button to win hover over the modal blocker.");
            }

            if (blocked || clickId != close.Id)
            {
                throw new InvalidOperationException("Expected auto recovery close button to remain clickable inside the overlay.");
            }

            coordinator.ResetForTesting();
            LegacyMainWindow.ResetAutoRecoveryItemConfigPopupForTesting();
        }

        private static void LegacyMovementSafeLandingOverlayBlocksLowerHoverAndKeepsOptionClickable()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            LegacyMainWindow.ResetMovementSafeLandingConfigPopupForTesting();
            var viewport = new LegacyUiRect(20, 20, 500, 320);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var anchor = new LegacyUiRect(410, 90, 64, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("movement-toggle:lower:On", "Lower", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("movement");
            if (!LegacyMainWindow.RegisterMovementSafeLandingConfigPopupOverlayForTesting(area, anchor))
            {
                throw new InvalidOperationException("Expected safe landing config popup to register as a modal overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 600, 400), "movement", AppSettings.CreateDefault(), elements);
            var popup = FindLegacyUiElementForTesting(elements, "movement-safe-landing-config-popup");
            mouse.X = popup.Rect.X + 12;
            mouse.Y = popup.Rect.Y + 38;

            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || string.Equals(hovered.Id, "movement-toggle:lower:On", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected safe landing modal overlay to own hover above lower movement rows.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected safe landing modal overlay to block lower movement row clicks.");
            }

            var option = FindLegacyUiElementForTesting(elements, "movement-safe-landing-option:" + MovementSafeLandingOptionCatalog.Grapple);
            mouse.X = option.Rect.X + Math.Max(1, option.Rect.Width / 2);
            mouse.Y = option.Rect.Y + Math.Max(1, option.Rect.Height / 2);
            hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != option.Id)
            {
                throw new InvalidOperationException("Expected safe landing option to win hover over the modal blocker.");
            }

            if (blocked || clickId != option.Id)
            {
                throw new InvalidOperationException("Expected safe landing option to remain clickable inside the overlay.");
            }

            coordinator.ResetForTesting();
            LegacyMainWindow.ResetMovementSafeLandingConfigPopupForTesting();
        }

        private static void LegacyFishingPickerOverlayBlocksLowerHoverAndKeepsNestedScroll()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            FishingFilterUiState.Reset();
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            var candidates = new List<FishingCatchCandidate>();
            for (var index = 0; index < 20; index++)
            {
                candidates.Add(new FishingCatchCandidate
                {
                    Kind = FishingCatchKinds.Item,
                    Id = 600 + index,
                    DisplayName = "Fish " + index
                });
            }

            FishingFilterUiState.OpenPicker(settings, candidates, string.Empty);
            var viewport = new LegacyUiRect(20, 20, 360, 220);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var picker = new LegacyUiRect(40, 50, 280, 118);
            var anchor = new LegacyUiRect(260, 176, 54, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("fishing-filter-exact-entry:delete:lower", "Lower", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("fishing");
            if (!LegacyMainWindow.RegisterFishingFilterExactPickerOverlayForTesting(area, picker, anchor))
            {
                throw new InvalidOperationException("Expected fishing exact picker to register as an overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 460, 300), "fishing", settings, elements);
            mouse.X = picker.X + 12;
            mouse.Y = picker.Y + 10;
            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || string.Equals(hovered.Id, "fishing-filter-exact-entry:delete:lower", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing picker overlay to own hover above lower list entries.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected fishing picker blocker to stop lower list entry clicks.");
            }

            var candidate = FindLegacyUiElementWithPrefixForTesting(elements, "fishing-filter-exact-picker:toggle:");
            mouse.X = candidate.Rect.X + Math.Max(1, candidate.Rect.Width / 2);
            mouse.Y = candidate.Rect.Y + Math.Max(1, candidate.Rect.Height / 2);
            hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || hovered.Id != candidate.Id)
            {
                throw new InvalidOperationException("Expected fishing picker candidate to win hover over the picker blocker.");
            }

            if (blocked || clickId != candidate.Id)
            {
                throw new InvalidOperationException("Expected fishing picker candidate to remain clickable inside the overlay.");
            }

            mouse.ScrollDelta = -120;
            if (!FishingFilterUiState.TryConsumePickerScroll(mouse))
            {
                throw new InvalidOperationException("Expected fishing picker overlay draw to publish its internal scroll viewport.");
            }

            coordinator.EndFrame();
            coordinator.ResetForTesting();
            FishingFilterUiState.Reset();
        }

        private static void LegacyFishingPresetOverlayBlocksLowerHoverAndKeepsRowsClickable()
        {
            var coordinator = LegacyUiOverlayCoordinator.Current;
            coordinator.ResetForTesting();
            FishingFilterUiState.Reset();
            var settings = AppSettings.CreateDefault();
            settings.FishingFilterMode = FishingFilterModes.AllowList;
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Exact;
            settings.FishingFilterPresets.Add(new FishingFilterPreset
            {
                Name = "Test Preset",
                FilterModeScope = FishingFilterModes.AllowList,
                MatchModeScope = FishingFilterMatchModes.Exact,
                ExactEntries = new List<FishingFilterExactEntry>
                {
                    new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = 2290, DisplayNameSnapshot = "Test Fish" }
                }
            });
            FishingFilterUiState.TogglePresetList(settings);
            var viewport = new LegacyUiRect(20, 20, 360, 220);
            var area = LegacyScrollArea.Create(viewport, 620, 0);
            var preset = new LegacyUiRect(40, 50, 280, 118);
            var anchor = new LegacyUiRect(260, 176, 54, 24);
            var elements = new List<LegacyUiElement>
            {
                CreateLegacyUiElementForTesting("fishing-filter-keyword:delete:lower", "Lower", "button", viewport)
            };
            var mouse = new LegacyMouseSnapshot
            {
                ReadAvailable = true,
                LeftPressed = true
            };

            coordinator.BeginFrame("fishing");
            if (!LegacyMainWindow.RegisterFishingFilterPresetListOverlayForTesting(area, preset, anchor, settings, FishingFilterModes.AllowList, FishingFilterMatchModes.Exact))
            {
                throw new InvalidOperationException("Expected fishing preset list to register as an overlay.");
            }

            coordinator.DrawOverlays(null, mouse, new LegacyUiRect(0, 0, 460, 300), "fishing", settings, elements);
            mouse.X = preset.X + 12;
            mouse.Y = preset.Y + 10;
            var hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            bool blocked;
            var clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            if (hovered == null || string.Equals(hovered.Id, "fishing-filter-keyword:delete:lower", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected fishing preset overlay to own hover above lower entries.");
            }

            if (!blocked || clickId.Length != 0)
            {
                throw new InvalidOperationException("Expected fishing preset blocker to stop lower entry clicks.");
            }

            var apply = FindLegacyUiElementForTesting(elements, "fishing-filter-preset:apply:saved-0");
            mouse.X = apply.Rect.X + Math.Max(1, apply.Rect.Width / 2);
            mouse.Y = apply.Rect.Y + Math.Max(1, apply.Rect.Height / 2);
            hovered = LegacyUiElementFrame.ResolveHoveredElement(null, elements, mouse, coordinator);
            clickId = LegacyMainWindow.ResolveClickableElementIdForTesting(elements, mouse, out blocked);
            coordinator.EndFrame();

            if (hovered == null || hovered.Id != apply.Id)
            {
                throw new InvalidOperationException("Expected fishing preset row to win hover over the preset blocker.");
            }

            if (blocked || clickId != apply.Id)
            {
                throw new InvalidOperationException("Expected fishing preset row to remain clickable inside the overlay.");
            }

            coordinator.ResetForTesting();
            FishingFilterUiState.Reset();
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

        private static LegacyUiElement FindLegacyUiElementWithPrefixForTesting(IList<LegacyUiElement> elements, string prefix)
        {
            if (elements != null)
            {
                for (var index = 0; index < elements.Count; index++)
                {
                    var element = elements[index];
                    if (element != null && element.Id != null && element.Id.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return element;
                    }
                }
            }

            throw new InvalidOperationException("Expected legacy UI element prefix was not registered: " + prefix);
        }
    }
}
