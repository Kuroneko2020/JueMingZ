using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static partial class FishingFilterUiState
    {
        public static void EnsureModeSignature(AppSettings settings)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                if (string.Equals(_modeSignature, signature, StringComparison.Ordinal))
                {
                    return;
                }

                ResetLocked();
                _modeSignature = signature;
            }

            LegacyTextInput.ClearFocus();
        }

        public static void Reset()
        {
            lock (SyncRoot)
            {
                ResetLocked();
                _modeSignature = string.Empty;
            }

            LegacyTextInput.ClearFocus();
        }

        public static string BuildModeSignature(AppSettings settings)
        {
            settings = settings ?? AppSettings.CreateDefault();
            return FishingFilterModes.Normalize(settings.FishingFilterMode) + "|" +
                   FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
        }

        private static void ResetLocked()
        {
            ResetPickerLocked();
            ResetPresetListLocked();
            _presetSaveNotice = string.Empty;
            _presetSaveNoticeExpiresUtcTicks = 0;
        }

        private static void ClampScrollLocked()
        {
            _pickerScrollOffset = Clamp(_pickerScrollOffset, 0, _pickerMaxScroll);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
