using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static partial class FishingFilterUiState
    {
        public const string KeywordInputId = "fishing-filter-keyword-input";
        public const string GlobalSearchInputId = "fishing-filter-global-search-input";
        public const string PresetNameInputId = "fishing-filter-preset-name-input";
        public const string PickerSourceCurrent = "CurrentWater";
        public const string PickerSourceGlobal = "GlobalSearch";
        private static readonly object SyncRoot = new object();
        private static readonly List<FishingCatchCandidate> PickerCandidates = new List<FishingCatchCandidate>();
        private static readonly HashSet<string> PickerSelectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _modeSignature = string.Empty;
        private static string _pickerSource = PickerSourceCurrent;
        private static string _globalSearchQuery = string.Empty;
        private static string _pickerMessage = string.Empty;
        private static bool _pickerOpen;
        private static int _pickerScrollOffset;
        private static int _pickerMaxScroll;
        private static LegacyUiRect _pickerViewport;
        private static bool _presetListOpen;
        private static int _presetScrollOffset;
        private static int _presetMaxScroll;
        private static LegacyUiRect _presetViewport;
        private static string _presetSaveNotice = string.Empty;
        private static long _presetSaveNoticeExpiresUtcTicks;
        private static int _entryScrollOffset;
        private static int _entryMaxScroll;
        private static LegacyUiRect _entryViewport;

        public static bool PickerOpen
        {
            get { lock (SyncRoot) { return _pickerOpen; } }
        }

        public static int PickerCandidateCount
        {
            get { lock (SyncRoot) { return PickerCandidates.Count; } }
        }

        public static int PickerSelectedCount
        {
            get { lock (SyncRoot) { return PickerSelectedKeys.Count; } }
        }

        public static int PickerScrollOffset
        {
            get { lock (SyncRoot) { return _pickerScrollOffset; } }
        }

        public static string PickerMessage
        {
            get { lock (SyncRoot) { return _pickerMessage; } }
        }

        public static string PickerSource
        {
            get { lock (SyncRoot) { return _pickerSource; } }
        }

        public static string GlobalSearchQuery
        {
            get { lock (SyncRoot) { return _globalSearchQuery; } }
        }

        public static bool PresetListOpen
        {
            get { lock (SyncRoot) { return _presetListOpen; } }
        }

        public static int PresetScrollOffset
        {
            get { lock (SyncRoot) { return _presetScrollOffset; } }
        }

        public static int EntryScrollOffset
        {
            get { lock (SyncRoot) { return _entryScrollOffset; } }
        }

        public static string PresetSaveNotice
        {
            get
            {
                lock (SyncRoot)
                {
                    if (string.IsNullOrWhiteSpace(_presetSaveNotice))
                    {
                        return string.Empty;
                    }

                    if (_presetSaveNoticeExpiresUtcTicks > 0 &&
                        DateTime.UtcNow.Ticks >= _presetSaveNoticeExpiresUtcTicks)
                    {
                        _presetSaveNotice = string.Empty;
                        _presetSaveNoticeExpiresUtcTicks = 0;
                        return string.Empty;
                    }

                    return _presetSaveNotice;
                }
            }
        }

        private static bool TryConsumeNestedScroll(LegacyMouseSnapshot mouse, int scrollDelta, FishingFilterScrollTarget target)
        {
            lock (SyncRoot)
            {
                if ((target == FishingFilterScrollTarget.Any || target == FishingFilterScrollTarget.Picker) &&
                    _pickerOpen &&
                    TryScrollViewportLocked(_pickerViewport, _pickerMaxScroll, ref _pickerScrollOffset, mouse, scrollDelta))
                {
                    return true;
                }

                if ((target == FishingFilterScrollTarget.Any || target == FishingFilterScrollTarget.Preset) &&
                    _presetListOpen &&
                    TryScrollViewportLocked(_presetViewport, _presetMaxScroll, ref _presetScrollOffset, mouse, scrollDelta))
                {
                    return true;
                }

                if ((target == FishingFilterScrollTarget.Any || target == FishingFilterScrollTarget.Entry) &&
                    TryScrollViewportLocked(_entryViewport, _entryMaxScroll, ref _entryScrollOffset, mouse, scrollDelta))
                {
                    return true;
                }

                return false;
            }
        }

        private enum FishingFilterScrollTarget
        {
            Any,
            Picker,
            Preset,
            Entry
        }
    }
}
