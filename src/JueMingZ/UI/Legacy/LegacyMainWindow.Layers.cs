using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy.Controls;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        public static bool DrawInterfaceLayer()
        {
            UiInputFrameClock.BeginDrawFrame("LegacyMainWindow.Draw");
            LegacyTextInput.BeginImeCompositionPanelFrame();
            var elementFrameStarted = false;
            var overlayFrameStarted = false;
            var overlayFrameCompleted = false;
            try
            {
                if (!LegacyMainUiState.Visible)
                {
                    return true;
                }

                if (LegacyMainUiState.HideIfMainMenu("LegacyMainUi.Draw"))
                {
                    return true;
                }

                if (!TerrariaMainCompat.AllowsInputProcessing)
                {
                    LegacyUiInput.ResetInteractionState();
                    UiMouseCaptureService.ReleaseForOperationWindow();
                }

                object spriteBatch;
                if (!UiDrawLifecycleGuard.TryEnterInterfaceDraw("LegacyMainWindow", true, out spriteBatch))
                {
                    return true;
                }

                // This layer draws inside Terraria's interface SpriteBatch phase.
                // Input capture may suppress world clicks, but drawing must not
                // reorder vanilla UI or execute gameplay actions directly.
                var rawMouse = DiagnosticMouseStateReader.Read();
                var scale = LegacyMainUiScale.Resolve(rawMouse);
                var mouse = LegacyUiInput.ReadMouse(rawMouse, scale);
                using (UiDrawTransform.Begin(scale.DrawScaleXFloat, scale.DrawScaleYFloat))
                {
                    var window = LegacyMainUiState.WindowRect;
                    var shell = LegacyMainWindowShell.Create(window);
                    LegacyUiInput.HandleWindowFrame(mouse, shell.TitleRect, shell.ResizeRect);

                    window = LegacyMainUiState.WindowRect;
                    shell = LegacyMainWindowShell.Create(window);
                    var inWindow = LegacyUiInput.IsMouseInWindow(mouse) || LegacyUiInput.IsActiveInteraction();
                    LegacyUiInput.CaptureIfNeeded(inWindow);
                    var selectedPage = LegacyMainUiState.SelectedPageId;
                    var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                    var contentRect = shell.ContentRect;
                    var pageLayout = GetCachedPageLayout(selectedPage, window, contentRect, settings, LegacyMainUiState.ScrollOffset);
                    var contentHeight = pageLayout.ContentHeight;
                    var scrollArea = pageLayout.CreateScrollArea(contentRect);
                    LegacyMainUiState.SetScrollOffset(scrollArea.ScrollOffset, scrollArea.MaxScroll);
                    LegacyUiOverlayCoordinator.Current.BeginFrame(selectedPage);
                    overlayFrameStarted = true;

                    if (inWindow && mouse.ScrollDelta != 0)
                    {
                        var scrollSnapshot = TerrariaUiMouseCompat.ReadScrollSnapshot(mouse.ScrollDelta);
                        if (LegacyUiOverlayCoordinator.Current.ShouldBlockMainScroll(mouse, mouse.ScrollDelta))
                        {
                            LegacyUiInput.CaptureIfNeeded(true);
                            LegacyHotbarScrollGuard.RestoreLateUiWheelIfNeeded(scrollSnapshot, inWindow, LegacyUiInput.IsActiveInteraction());
                            LegacyUiInput.SuppressHotbarScroll();
                        }
                        else if (string.Equals(selectedPage, "fishing", StringComparison.Ordinal) &&
                            (FishingFilterUiState.TryConsumePickerScroll(mouse) ||
                             FishingFilterUiState.TryConsumePresetScroll(mouse) ||
                             FishingFilterUiState.TryConsumeEntryScroll(mouse)))
                        {
                            LegacyUiInput.CaptureIfNeeded(true);
                            LegacyHotbarScrollGuard.RestoreLateUiWheelIfNeeded(scrollSnapshot, inWindow, LegacyUiInput.IsActiveInteraction());
                            LegacyUiInput.SuppressHotbarScroll();
                        }
                        else
                        {
                            var before = LegacyMainUiState.ScrollOffset;
                            var scrollDelta = -mouse.ScrollDelta / 3;
                            if (scrollDelta == 0)
                            {
                                scrollDelta = mouse.ScrollDelta > 0 ? -40 : 40;
                            }

                            LegacyMainUiState.ScrollBy(scrollDelta, scrollArea.MaxScroll);
                            pageLayout = GetCachedPageLayout(selectedPage, window, contentRect, settings, LegacyMainUiState.ScrollOffset);
                            contentHeight = pageLayout.ContentHeight;
                            scrollArea = pageLayout.CreateScrollArea(contentRect);
                            LegacyUiInput.CaptureIfNeeded(true);
                            var restored = LegacyHotbarScrollGuard.RestoreLateUiWheelIfNeeded(scrollSnapshot, inWindow, LegacyUiInput.IsActiveInteraction());
                            var suppressed = LegacyUiInput.SuppressHotbarScroll();
                            if (before != LegacyMainUiState.ScrollOffset)
                            {
                                RecordUiScroll(mouse.ScrollDelta, before, LegacyMainUiState.ScrollOffset, suppressed || restored);
                            }
                        }
                    }

                    LegacyUiInput.HandleScrollbarDrag(mouse, scrollArea);
                    pageLayout = GetCachedPageLayout(selectedPage, window, contentRect, settings, LegacyMainUiState.ScrollOffset);
                    contentHeight = pageLayout.ContentHeight;
                    scrollArea = pageLayout.CreateScrollArea(contentRect);
                    BeginRetainedFrameModel(selectedPage, window, contentRect, scrollArea, settings, contentHeight);
                    var elements = PrepareFrameElements();
                    elementFrameStarted = true;
                    var frameContext = new LegacyUiContext(spriteBatch, mouse, window, selectedPage, settings, elements);
                    frameContext.SetContentRect(contentRect);
                    BeginFrameHoverCache(mouse, selectedPage, window, contentRect, scrollArea, settings);

                    DrawFrame(spriteBatch, window, shell.TitleRect, shell.ResizeRect);

                    DrawTabs(frameContext);

                    LegacyUiTheme.DrawContentPanel(spriteBatch, contentRect);

                    LegacyUiElement hoveredElement = null;
                    if (string.Equals(selectedPage, "home", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawItemsPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "buff", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawBuffPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "combat", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawCombatPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "misc", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawMiscPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "about", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawAboutPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "information", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawInformationPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "fishing", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawFishingPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "movement", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawMovementPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "map_enhancement", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawMapEnhancementPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else if (string.Equals(selectedPage, "search", StringComparison.Ordinal))
                    {
                        hoveredElement = DrawSearchPage(spriteBatch, scrollArea, mouse, elements);
                    }
                    else
                    {
                        hoveredElement = DrawEmptyPage(spriteBatch, scrollArea, selectedPage, mouse);
                    }

                    if (!string.Equals(selectedPage, "about", StringComparison.Ordinal) || scrollArea.NeedsScroll)
                    {
                        LegacyUiTheme.DrawScrollbar(spriteBatch, scrollArea.ScrollbarTrack, scrollArea.ScrollbarThumb);
                    }

                    if (!string.Equals(selectedPage, "about", StringComparison.Ordinal))
                    {
                        DrawFooter(spriteBatch, window);
                    }

                    LegacyUiOverlayCoordinator.Current.DrawOverlays(spriteBatch, mouse, window, selectedPage, settings, elements);
                    hoveredElement = ResolveFrameHoveredElement(hoveredElement, elements, mouse);
                    if (hoveredElement != null)
                    {
                        DrawTooltip(spriteBatch, hoveredElement, mouse);
                    }

                    if (LegacyUiInput.ShouldSuppressVanillaMouseText(mouse))
                    {
                        UiMouseCaptureService.SuppressMouseTextForOperationWindow();
                    }

                    HandleClicks(elements, mouse, shell.TitleRect, shell.ResizeRect);
                    LegacyUiInput.FinishFrame(mouse, inWindow);
                    FinishRetainedFrameModel(elements);
                    LegacyUiOverlayCoordinator.Current.EndFrame();
                    overlayFrameCompleted = true;
                }
            }
            catch (Exception error)
            {
                CancelRetainedFrameModel();
                UiDrawLifecycleGuard.RecordDrawException("LegacyMainWindow", error);
                LogThrottle.ErrorThrottled(
                    "legacy-main-window-draw-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyMainWindow",
                    "Legacy main window draw failed.", error);
            }
            finally
            {
                if (elementFrameStarted)
                {
                    FinishFrameElements();
                }

                if (overlayFrameStarted && !overlayFrameCompleted)
                {
                    LegacyUiOverlayCoordinator.Current.EndFrame();
                }
            }

            return true;
        }

        public static bool DrawInputGuardLayer()
        {
            UiInputFrameClock.BeginDrawFrame("LegacyMainWindow.InputGuard");
            try
            {
                if (!LegacyMainUiState.Visible)
                {
                    return true;
                }

                if (!LegacyMainUiState.HideIfMainMenu("LegacyMainUi.InputGuard"))
                {
                    // Guard layers only claim mouse ownership for the visible F5
                    // window; they do not run button commands or mutate game state.
                    LegacyUiInput.CaptureCurrentMouseForWindow("LegacyMainUi.InputGuard");
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-main-window-input-guard-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyMainWindow",
                    "Legacy main window input guard failed.", error);
            }

            return true;
        }

        public static bool DrawMouseTextGuardLayer()
        {
            UiInputFrameClock.BeginDrawFrame("LegacyMainWindow.MouseTextGuard");
            try
            {
                if (!LegacyMainUiState.Visible)
                {
                    return true;
                }

                if (!LegacyMainUiState.HideIfMainMenu("LegacyMainUi.MouseTextGuard"))
                {
                    LegacyUiInput.SuppressCurrentMouseTextForWindow("LegacyMainUi.MouseTextGuard");
                }
            }
            catch (Exception error)
            {
                LogThrottle.ErrorThrottled(
                    "legacy-main-window-mouse-text-guard-error",
                    TimeSpan.FromSeconds(10),
                    "LegacyMainWindow",
                    "Legacy main window mouse text guard failed.", error);
            }

            return true;
        }
    }
}
