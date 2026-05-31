using System;
using System.Collections.Generic;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyMainWindow
    {
        private static List<LegacyUiElement> PrepareFrameElements()
        {
            FrameElements.Clear();
            return FrameElements;
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
    }
}
