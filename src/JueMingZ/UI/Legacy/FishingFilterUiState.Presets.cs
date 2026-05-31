using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static partial class FishingFilterUiState
    {
        public static void TogglePresetList(AppSettings settings)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                if (!string.Equals(_modeSignature, signature, StringComparison.Ordinal))
                {
                    ResetLocked();
                    _modeSignature = signature;
                }

                if (_presetListOpen)
                {
                    ResetPresetListLocked();
                }
                else
                {
                    ResetPickerLocked();
                    _presetListOpen = true;
                    _presetScrollOffset = 0;
                    _presetMaxScroll = 0;
                    _presetViewport = new LegacyUiRect();
                }
            }

            LegacyTextInput.ClearFocus();
        }

        public static void ClosePresetList(AppSettings settings)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                if (!string.Equals(_modeSignature, signature, StringComparison.Ordinal))
                {
                    ResetLocked();
                }

                _modeSignature = signature;
                ResetPresetListLocked();
            }
        }

        public static void ShowPresetSaveNotice(AppSettings settings, string message)
        {
            var signature = BuildModeSignature(settings);
            lock (SyncRoot)
            {
                if (!string.Equals(_modeSignature, signature, StringComparison.Ordinal))
                {
                    ResetLocked();
                    _modeSignature = signature;
                }

                _presetSaveNotice = string.IsNullOrWhiteSpace(message) ? "已保存" : message.Trim();
                _presetSaveNoticeExpiresUtcTicks = DateTime.UtcNow.AddSeconds(1.8).Ticks;
            }
        }

        public static void ClearPresetSaveNotice()
        {
            lock (SyncRoot)
            {
                _presetSaveNotice = string.Empty;
                _presetSaveNoticeExpiresUtcTicks = 0;
            }
        }

        private static void ResetPresetListLocked()
        {
            _presetListOpen = false;
            _presetScrollOffset = 0;
            _presetMaxScroll = 0;
            _presetViewport = new LegacyUiRect();
            _entryScrollOffset = 0;
            _entryMaxScroll = 0;
            _entryViewport = new LegacyUiRect();
        }
    }
}
