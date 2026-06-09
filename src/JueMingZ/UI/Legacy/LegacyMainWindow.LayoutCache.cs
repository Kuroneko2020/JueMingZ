using System;
using System.Collections.Generic;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Config;
using JueMingZ.UI;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static LegacyUiPageLayoutSignature _pageLayoutCacheSignature;
        private static LegacyUiPageLayoutCache _pageLayoutCache;
        private static bool _pageLayoutCacheValid;
        private static int _pageLayoutCacheRebuildCount;
        private static int _pageLayoutCacheHitCount;

        internal static long PageLayoutCacheHitCount
        {
            get { return _pageLayoutCacheHitCount; }
        }

        internal static long PageLayoutCacheMissCount
        {
            get { return _pageLayoutCacheRebuildCount; }
        }

        private static List<LegacyUiElement> PrepareFrameElements()
        {
            LegacyUiElementFrame.BeginElementFrame();
            FrameElements.Clear();
            return FrameElements;
        }

        private static void FinishFrameElements()
        {
            LegacyUiElementFrame.EndElementFrame();
        }

        private static void BeginFrameHoverCache(
            LegacyMouseSnapshot mouse,
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings)
        {
            LegacyUiElementFrame.BeginHoverFrame(
                mouse,
                BuildFrameHoverLayoutToken(selectedPage, windowRect, contentRect, scrollArea, settings),
                LegacyUiInput.ActiveMode);
        }

        private static LegacyUiElement ResolveFrameHoveredElement(
            LegacyUiElement preferred,
            List<LegacyUiElement> elements,
            LegacyMouseSnapshot mouse)
        {
            return LegacyUiElementFrame.ResolveHoveredElement(preferred, elements, mouse, LegacyUiOverlayCoordinator.Current);
        }

        private static LegacyUiElement AddFrameElement(
            List<LegacyUiElement> elements,
            string id,
            string label,
            string kind,
            LegacyUiRect rect,
            bool enabled = true,
            bool selected = false,
            int intValue = 0,
            int minValue = 0,
            int maxValue = 0,
            string[] tooltipLines = null,
            BuffPotionCandidate candidate = null,
            BuffPotionWhitelistEntry whitelistEntry = null)
        {
            return LegacyUiElementFrame.Add(
                elements,
                id,
                label,
                kind,
                rect,
                enabled,
                selected,
                intValue,
                minValue,
                maxValue,
                tooltipLines,
                candidate,
                whitelistEntry);
        }

        private static bool IsFrameElementHovered(string id, LegacyUiRect rect, LegacyMouseSnapshot mouse)
        {
            return LegacyUiElementFrame.IsHovered(id, rect, mouse);
        }

        private static void RecordFrameElementHover(LegacyUiElement element, bool hovered)
        {
            LegacyUiElementFrame.RecordHover(element, hovered);
        }

        private static int BuildFrameHoverLayoutToken(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, selectedPage);
                AddHash(ref hash, windowRect.Width);
                AddHash(ref hash, windowRect.Height);
                AddHash(ref hash, contentRect.Width);
                AddHash(ref hash, contentRect.Height);
                AddHash(ref hash, scrollArea == null ? 0 : scrollArea.ScrollOffset);
                AddHash(ref hash, scrollArea == null ? 0 : scrollArea.MaxScroll);
                AddHash(ref hash, settings == null ? 0 : settings.ConfigVersion);
                AddHash(ref hash, BuildPageStateSignature(selectedPage, settings));
                AddHash(ref hash, UiTextRenderer.FontSignatureForLayoutCache);
                AddHash(ref hash, UiTextRenderer.CacheGenerationForLayoutCache);
                AddHash(ref hash, _pageLayoutCacheRebuildCount);
                AddHash(ref hash, LegacyUiOverlayCoordinator.Current.LastStackSignature);
                return hash;
            }
        }

        private static LegacyUiPageLayoutCache GetCachedPageLayout(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            AppSettings settings,
            int requestedScrollOffset)
        {
            // Layout signatures include scroll, settings, and font generation so retained
            // frames are reused only when hit-test geometry still matches draw geometry.
            selectedPage = selectedPage ?? string.Empty;
            settings = settings ?? AppSettings.CreateDefault();

            var contentHeight = CalculateCachedContentHeight(selectedPage, contentRect, settings);
            var relativeContentRect = new LegacyUiRect(0, 0, contentRect.Width, contentRect.Height);
            var relativeArea = LegacyScrollArea.Create(relativeContentRect, contentHeight, requestedScrollOffset);
            var signature = BuildPageLayoutSignature(
                selectedPage,
                windowRect,
                contentRect,
                settings,
                contentHeight,
                relativeArea.ScrollOffset);

            if (_pageLayoutCacheValid &&
                _pageLayoutCacheSignature != null &&
                _pageLayoutCacheSignature.Equals(signature) &&
                _pageLayoutCache != null)
            {
                unchecked
                {
                    _pageLayoutCacheHitCount++;
                }

                return _pageLayoutCache;
            }

            _pageLayoutCache = new LegacyUiPageLayoutCache(
                signature,
                contentHeight,
                relativeArea.ScrollOffset,
                relativeArea.MaxScroll,
                relativeArea.Viewport,
                relativeArea.ScrollbarTrack,
                relativeArea.ScrollbarThumb);
            _pageLayoutCacheSignature = signature;
            _pageLayoutCacheValid = true;
            unchecked
            {
                _pageLayoutCacheRebuildCount++;
            }

            return _pageLayoutCache;
        }

        private static int CalculateCachedContentHeight(string selectedPage, LegacyUiRect contentRect, AppSettings settings)
        {
            selectedPage = selectedPage ?? string.Empty;
            settings = settings ?? AppSettings.CreateDefault();
            var signature = BuildContentHeightSignature(selectedPage, settings);
            var settingsVersion = settings.ConfigVersion;

            if (_contentHeightCacheValid &&
                string.Equals(_contentHeightCachePageId, selectedPage, StringComparison.Ordinal) &&
                _contentHeightCacheWidth == contentRect.Width &&
                _contentHeightCacheHeight == contentRect.Height &&
                _contentHeightCacheSettingsVersion == settingsVersion &&
                _contentHeightCacheSignature == signature)
            {
                return _contentHeightCacheValue;
            }

            var value = CalculateContentHeightUncached(selectedPage, contentRect, settings);
            _contentHeightCachePageId = selectedPage;
            _contentHeightCacheWidth = contentRect.Width;
            _contentHeightCacheHeight = contentRect.Height;
            _contentHeightCacheSettingsVersion = settingsVersion;
            _contentHeightCacheSignature = signature;
            _contentHeightCacheValue = value;
            _contentHeightCacheValid = true;
            return value;
        }

        private static int CalculateContentHeightUncached(string selectedPage, LegacyUiRect contentRect, AppSettings settings)
        {
            if (string.Equals(selectedPage, "buff", StringComparison.Ordinal))
            {
                return CalculateBuffContentHeight(contentRect);
            }

            if (string.Equals(selectedPage, "combat", StringComparison.Ordinal))
            {
                return CalculateCombatContentHeight();
            }

            if (string.Equals(selectedPage, "misc", StringComparison.Ordinal))
            {
                return CalculateMiscContentHeight(contentRect);
            }

            if (string.Equals(selectedPage, "about", StringComparison.Ordinal))
            {
                return CalculateAboutContentHeight(contentRect);
            }

            if (string.Equals(selectedPage, "information", StringComparison.Ordinal))
            {
                return CalculateInformationContentHeight(settings);
            }

            if (string.Equals(selectedPage, "fishing", StringComparison.Ordinal))
            {
                return CalculateFishingContentHeight(contentRect);
            }

            if (string.Equals(selectedPage, "movement", StringComparison.Ordinal))
            {
                return CalculateMovementContentHeight();
            }

            return contentRect.Height - LegacyUiMetrics.ContentPadding * 2;
        }

        private static int BuildContentHeightSignature(string selectedPage, AppSettings settings)
        {
            unchecked
            {
                var hash = 17;
                AddHash(ref hash, selectedPage);

                if (string.Equals(selectedPage, "buff", StringComparison.Ordinal))
                {
                    AddHash(ref hash, LegacyMainUiState.AvailableCandidateCount);
                    AddHash(ref hash, LegacyMainUiState.WhitelistCount);
                }
                else if (string.Equals(selectedPage, "information", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.InformationSignTextLabelsMode);
                    AddHash(ref hash, settings.InformationTombstoneTextLabelsMode);
                }
                else if (string.Equals(selectedPage, "misc", StringComparison.Ordinal))
                {
                    AddHash(ref hash, _quickItemPickerOpen);
                    AddHash(ref hash, _quickItemHotkeyCaptureActive);
                    AddHash(ref hash, _autoSellPickerOpen);
                    AddHash(ref hash, _autoDiscardPickerOpen);
                    AddHash(ref hash, _autoMiningHotkeyCaptureActive);
                    AddHash(ref hash, _autoCaptureCritterConfigOpen);
                    AddHash(ref hash, Count(ConfigService.HotkeySettings == null ? null : ConfigService.HotkeySettings.QuickItemHotkeyBindings));
                    AddHash(ref hash, Count(GetAutoSellItemIds()));
                    AddHash(ref hash, Count(GetAutoDiscardItemIds()));
                    AddHash(ref hash, Count(GetQuickReforgePrefixes()));
                    AddHash(ref hash, _quickItemPickerOpen ? Count(GetQuickItemPickerCandidates()) : 0);
                    AddHash(ref hash, _autoSellPickerOpen ? Count(GetAutoSellPickerCandidates()) : 0);
                    AddHash(ref hash, _autoDiscardPickerOpen ? Count(GetAutoDiscardPickerCandidates()) : 0);
                }

                return hash;
            }
        }

        private static LegacyUiPageLayoutSignature BuildPageLayoutSignature(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            AppSettings settings,
            int contentHeight,
            int scrollOffset)
        {
            return new LegacyUiPageLayoutSignature(
                selectedPage,
                windowRect.Width,
                windowRect.Height,
                contentRect.Width,
                contentRect.Height,
                contentHeight,
                scrollOffset,
                settings == null ? 0 : settings.ConfigVersion,
                BuildPageStateSignature(selectedPage, settings),
                UiTextRenderer.FontSignatureForLayoutCache,
                UiTextRenderer.CacheGenerationForLayoutCache);
        }

        private static int BuildPageStateSignature(string selectedPage, AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            unchecked
            {
                var hash = BuildContentHeightSignature(selectedPage, settings);
                if (string.Equals(selectedPage, "buff", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.AutoHealMode);
                    AddHash(ref hash, settings.AutoManaMode);
                    AddHash(ref hash, settings.AutoHealEnabled);
                    AddHash(ref hash, settings.AutoManaEnabled);
                    AddHash(ref hash, settings.AutoBuffEnabled);
                    AddHash(ref hash, settings.AutoNurseEnabled);
                    AddHash(ref hash, settings.AutoStationBuffEnabled);
                    AddHash(ref hash, _autoRecoveryItemConfigKind);
                    AddIntListHash(ref hash, settings.AutoHealBlockedItemTypes);
                    AddIntListHash(ref hash, settings.AutoManaBlockedItemTypes);
                }
                else if (string.Equals(selectedPage, "information", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.InformationEnemyNameLabelsEnabled);
                    AddHash(ref hash, settings.InformationCritterNameLabelsEnabled);
                    AddHash(ref hash, settings.InformationNpcNameLabelsMode);
                    AddHash(ref hash, settings.InformationChestNameLabelsMode);
                    AddHash(ref hash, settings.InformationSignTextLabelsMode);
                    AddHash(ref hash, settings.InformationSignTextMaxLines);
                    AddHash(ref hash, settings.InformationSignTextMaxCharacters);
                    AddHash(ref hash, settings.InformationTombstoneTextLabelsMode);
                    AddHash(ref hash, settings.InformationTombstoneTextMaxLines);
                    AddHash(ref hash, settings.InformationTombstoneTextMaxCharacters);
                    AddHash(ref hash, settings.InformationHighlightLifeCrystalEnabled);
                    AddHash(ref hash, settings.InformationHighlightManaCrystalEnabled);
                    AddHash(ref hash, settings.InformationHighlightDigtoiseEnabled);
                    AddHash(ref hash, settings.InformationHighlightLifeFruitEnabled);
                    AddHash(ref hash, settings.InformationHighlightDragonEggEnabled);
                    AddHash(ref hash, settings.InformationBiomeDisplayEnabled);
                    AddHash(ref hash, settings.InformationWorldInfectionEnabled);
                    AddHash(ref hash, settings.InformationLuckValueEnabled);
                    AddHash(ref hash, settings.InformationFishingCatchesEnabled);
                    AddHash(ref hash, settings.InformationFishingFilteredCatchesEnabled);
                    AddHash(ref hash, settings.InformationAnglerQuestEnabled);
                    AddHash(ref hash, _informationStylePopupFeatureId);
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.EnemyNameFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.CritterNameFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.NpcNameFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.ChestNameFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.SignTextFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.TombstoneTextFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.BiomeDisplayFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.WorldInfectionFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.LuckValueFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingCatchesFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId));
                    AddHash(ref hash, InformationStyleHelper.GetColorHex(settings, InformationStyleHelper.AnglerQuestFeatureId));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.EnemyNameFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.CritterNameFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.NpcNameFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.ChestNameFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.SignTextFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.TombstoneTextFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.BiomeDisplayFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.WorldInfectionFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.LuckValueFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingCatchesFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.FishingFilteredCatchesFeatureId)));
                    AddHash(ref hash, ScaleSignature(InformationStyleHelper.GetFontScale(settings, InformationStyleHelper.AnglerQuestFeatureId)));
                }
                else if (string.Equals(selectedPage, "fishing", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.FishingAutoFishEnabled);
                    AddHash(ref hash, settings.FishingAutoLoadoutEnabled);
                    AddHash(ref hash, settings.FishingAutoEquipmentEnabled);
                    AddHash(ref hash, settings.FishingAutoStoreMode);
                    AddHash(ref hash, settings.FishingFilterCutRodSkipEnabled);
                    AddHash(ref hash, settings.FishingFilterMode);
                    AddHash(ref hash, settings.FishingFilterMatchMode);
                    AddHash(ref hash, settings.FishingFilterCrateRule);
                    AddHash(ref hash, settings.FishingFilterEnemyRule);
                    AddHash(ref hash, settings.FishingFilterQuestFishRule);
                    AddHash(ref hash, Count(settings.FishingFilterAllowExactEntries));
                    AddHash(ref hash, Count(settings.FishingFilterDenyExactEntries));
                    AddHash(ref hash, Count(settings.FishingFilterAllowKeywords));
                    AddHash(ref hash, Count(settings.FishingFilterDenyKeywords));
                    AddHash(ref hash, Count(settings.FishingFilterPresets));
                    AddHash(ref hash, LegacyTextInput.IsFocused(FishingQuickRenameTextInputId));
                    AddHash(ref hash, LegacyTextInput.IsFocused(FishingFilterUiState.KeywordInputId));
                    AddHash(ref hash, LegacyTextInput.IsFocused(FishingFilterUiState.GlobalSearchInputId));
                    AddHash(ref hash, LegacyTextInput.IsFocused(FishingFilterUiState.PresetNameInputId));
                    AddHash(ref hash, FishingFilterUiState.PickerOpen);
                    AddHash(ref hash, FishingFilterUiState.PickerCandidateCount);
                    AddHash(ref hash, FishingFilterUiState.PickerSelectedCount);
                    AddHash(ref hash, FishingFilterUiState.PickerScrollOffset);
                    AddHash(ref hash, FishingFilterUiState.PickerSource);
                    AddHash(ref hash, FishingFilterUiState.GlobalSearchQuery);
                    AddHash(ref hash, FishingFilterUiState.PresetListOpen);
                    AddHash(ref hash, FishingFilterUiState.PresetScrollOffset);
                    AddHash(ref hash, FishingFilterUiState.EntryScrollOffset);
                    AddHash(ref hash, FishingFilterUiState.PresetSaveNotice);
                }
                else if (string.Equals(selectedPage, "misc", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.WorldAutomationAutoCaptureCritterMode);
                    AddHash(ref hash, settings.MiscAutoCaptureCritterEnabled);
                    AddHash(ref hash, _autoCaptureCritterConfigOpen);
                    var options = AutoCaptureCritterCategoryCatalog.Options;
                    if (options != null)
                    {
                        for (var index = 0; index < options.Length; index++)
                        {
                            var option = options[index];
                            AddHash(ref hash, option == null ? string.Empty : option.Id);
                            AddHash(ref hash, option != null && AutoCaptureCritterCategoryCatalog.GetEnabled(settings, option.Id));
                        }
                    }
                }
                else if (string.Equals(selectedPage, "movement", StringComparison.Ordinal))
                {
                    AddHash(ref hash, settings.MovementSimulatedMultiJumpEnabled);
                    AddHash(ref hash, settings.MovementContinuousDashEnabled);
                    AddHash(ref hash, settings.MovementContinuousDashMode);
                    AddHash(ref hash, settings.MovementTeleportCorrectionEnabled);
                    AddHash(ref hash, settings.MovementSafeLandingEnabled);
                    AddHash(ref hash, _movementSafeLandingConfigOpen);
                    var options = MovementSafeLandingOptionCatalog.Options;
                    if (options != null)
                    {
                        for (var index = 0; index < options.Length; index++)
                        {
                            var option = options[index];
                            AddHash(ref hash, option == null ? string.Empty : option.Id);
                            AddHash(ref hash, option != null && MovementSafeLandingOptionCatalog.GetEnabled(settings, option.Id));
                        }
                    }
                }

                return hash;
            }
        }

        private static int ScaleSignature(double scale)
        {
            return (int)Math.Round(scale * 1000d);
        }

        private static LegacyUiRect OffsetRect(LegacyUiRect rect, int offsetX, int offsetY)
        {
            return new LegacyUiRect(rect.X + offsetX, rect.Y + offsetY, rect.Width, rect.Height);
        }

        private static int Count<T>(ICollection<T> values)
        {
            return values == null ? 0 : values.Count;
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

        private static void AddIntListHash(ref int hash, IList<int> values)
        {
            AddHash(ref hash, values == null ? 0 : values.Count);
            if (values == null)
            {
                return;
            }

            for (var index = 0; index < values.Count; index++)
            {
                AddHash(ref hash, values[index]);
            }
        }

        internal static LegacyUiPageLayoutSnapshot BuildPageLayoutSnapshotForTesting(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            int requestedScrollOffset,
            AppSettings settings)
        {
            var cache = GetCachedPageLayout(selectedPage, windowRect, contentRect, settings, requestedScrollOffset);
            var area = cache.CreateScrollArea(contentRect);
            return new LegacyUiPageLayoutSnapshot
            {
                PageId = cache.Signature.PageId,
                WindowWidth = cache.Signature.WindowWidth,
                WindowHeight = cache.Signature.WindowHeight,
                ContentWidth = cache.Signature.ContentWidth,
                ContentRectHeight = cache.Signature.ContentHeight,
                ContentHeight = cache.ContentHeight,
                PageStateSignature = cache.Signature.PageStateSignature,
                FontSignature = cache.Signature.FontSignature,
                FontCacheGeneration = cache.Signature.FontCacheGeneration,
                ScrollOffset = area.ScrollOffset,
                MaxScroll = area.MaxScroll,
                Viewport = area.Viewport,
                ScrollbarTrack = area.ScrollbarTrack,
                ScrollbarThumb = area.ScrollbarThumb,
                RebuildCount = _pageLayoutCacheRebuildCount,
                HitCount = _pageLayoutCacheHitCount
            };
        }

        internal static void ResetPageLayoutCacheForTesting()
        {
            _pageLayoutCacheSignature = null;
            _pageLayoutCache = null;
            _pageLayoutCacheValid = false;
            _pageLayoutCacheRebuildCount = 0;
            _pageLayoutCacheHitCount = 0;
            _contentHeightCacheValid = false;
        }

        internal static int BuildFrameHoverLayoutTokenForTesting(
            string selectedPage,
            LegacyUiRect windowRect,
            LegacyUiRect contentRect,
            LegacyScrollArea scrollArea,
            AppSettings settings)
        {
            return BuildFrameHoverLayoutToken(selectedPage, windowRect, contentRect, scrollArea, settings);
        }

        internal sealed class LegacyUiPageLayoutSnapshot
        {
            public string PageId { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public int ContentWidth { get; set; }
            public int ContentRectHeight { get; set; }
            public int ContentHeight { get; set; }
            public int PageStateSignature { get; set; }
            public string FontSignature { get; set; }
            public int FontCacheGeneration { get; set; }
            public int ScrollOffset { get; set; }
            public int MaxScroll { get; set; }
            public LegacyUiRect Viewport { get; set; }
            public LegacyUiRect ScrollbarTrack { get; set; }
            public LegacyUiRect ScrollbarThumb { get; set; }
            public int RebuildCount { get; set; }
            public int HitCount { get; set; }
        }

        private sealed class LegacyUiPageLayoutCache
        {
            public LegacyUiPageLayoutCache(
                LegacyUiPageLayoutSignature signature,
                int contentHeight,
                int scrollOffset,
                int maxScroll,
                LegacyUiRect viewport,
                LegacyUiRect scrollbarTrack,
                LegacyUiRect scrollbarThumb)
            {
                Signature = signature;
                ContentHeight = contentHeight;
                ScrollOffset = scrollOffset;
                MaxScroll = maxScroll;
                RelativeViewport = viewport;
                RelativeScrollbarTrack = scrollbarTrack;
                RelativeScrollbarThumb = scrollbarThumb;
            }

            public LegacyUiPageLayoutSignature Signature { get; private set; }
            public int ContentHeight { get; private set; }
            public int ScrollOffset { get; private set; }
            public int MaxScroll { get; private set; }
            private LegacyUiRect RelativeViewport { get; set; }
            private LegacyUiRect RelativeScrollbarTrack { get; set; }
            private LegacyUiRect RelativeScrollbarThumb { get; set; }

            public LegacyScrollArea CreateScrollArea(LegacyUiRect contentRect)
            {
                return new LegacyScrollArea
                {
                    ContentRect = contentRect,
                    Viewport = OffsetRect(RelativeViewport, contentRect.X, contentRect.Y),
                    ScrollbarTrack = OffsetRect(RelativeScrollbarTrack, contentRect.X, contentRect.Y),
                    ScrollbarThumb = OffsetRect(RelativeScrollbarThumb, contentRect.X, contentRect.Y),
                    ScrollOffset = ScrollOffset,
                    MaxScroll = MaxScroll
                };
            }
        }

        private sealed class LegacyUiPageLayoutSignature
        {
            public LegacyUiPageLayoutSignature(
                string pageId,
                int windowWidth,
                int windowHeight,
                int contentWidth,
                int contentHeight,
                int pageContentHeight,
                int scrollOffset,
                int settingsVersion,
                int pageStateSignature,
                string fontSignature,
                int fontCacheGeneration)
            {
                PageId = pageId ?? string.Empty;
                WindowWidth = windowWidth;
                WindowHeight = windowHeight;
                ContentWidth = contentWidth;
                ContentHeight = contentHeight;
                PageContentHeight = pageContentHeight;
                ScrollOffset = scrollOffset;
                SettingsVersion = settingsVersion;
                PageStateSignature = pageStateSignature;
                FontSignature = fontSignature ?? string.Empty;
                FontCacheGeneration = fontCacheGeneration;
            }

            public string PageId { get; private set; }
            public int WindowWidth { get; private set; }
            public int WindowHeight { get; private set; }
            public int ContentWidth { get; private set; }
            public int ContentHeight { get; private set; }
            public int PageContentHeight { get; private set; }
            public int ScrollOffset { get; private set; }
            public int SettingsVersion { get; private set; }
            public int PageStateSignature { get; private set; }
            public string FontSignature { get; private set; }
            public int FontCacheGeneration { get; private set; }

            public bool Equals(LegacyUiPageLayoutSignature other)
            {
                return other != null &&
                       WindowWidth == other.WindowWidth &&
                       WindowHeight == other.WindowHeight &&
                       ContentWidth == other.ContentWidth &&
                       ContentHeight == other.ContentHeight &&
                       PageContentHeight == other.PageContentHeight &&
                       ScrollOffset == other.ScrollOffset &&
                       SettingsVersion == other.SettingsVersion &&
                       PageStateSignature == other.PageStateSignature &&
                       FontCacheGeneration == other.FontCacheGeneration &&
                       string.Equals(PageId, other.PageId, StringComparison.Ordinal) &&
                       string.Equals(FontSignature, other.FontSignature, StringComparison.Ordinal);
            }
        }
    }
}
