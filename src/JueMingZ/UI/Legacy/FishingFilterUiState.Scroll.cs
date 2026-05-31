using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Config;

namespace JueMingZ.UI.Legacy
{
    internal static partial class FishingFilterUiState
    {
        public static void SetPickerViewport(LegacyUiRect viewport, int contentHeight)
        {
            lock (SyncRoot)
            {
                _pickerViewport = viewport;
                _pickerMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                ClampScrollLocked();
            }
        }

        public static void ClearPickerViewport()
        {
            lock (SyncRoot)
            {
                _pickerViewport = new LegacyUiRect();
                _pickerMaxScroll = 0;
                _pickerScrollOffset = 0;
            }
        }

        public static void SetPresetViewport(LegacyUiRect viewport, int contentHeight)
        {
            lock (SyncRoot)
            {
                _presetViewport = viewport;
                _presetMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                _presetScrollOffset = Clamp(_presetScrollOffset, 0, _presetMaxScroll);
            }
        }

        public static void ClearPresetViewport()
        {
            lock (SyncRoot)
            {
                _presetViewport = new LegacyUiRect();
                _presetMaxScroll = 0;
                _presetScrollOffset = 0;
            }
        }

        public static void SetEntryViewport(LegacyUiRect viewport, int contentHeight)
        {
            lock (SyncRoot)
            {
                _entryViewport = viewport;
                _entryMaxScroll = Math.Max(0, contentHeight - Math.Max(0, viewport.Height));
                _entryScrollOffset = Clamp(_entryScrollOffset, 0, _entryMaxScroll);
            }
        }

        public static void ClearEntryViewport()
        {
            lock (SyncRoot)
            {
                _entryViewport = new LegacyUiRect();
                _entryMaxScroll = 0;
                _entryScrollOffset = 0;
            }
        }

        public static bool TryConsumePickerScroll(LegacyMouseSnapshot mouse)
        {
            if (mouse == null || mouse.ScrollDelta == 0)
            {
                return false;
            }

            return TryConsumeNestedScroll(mouse, mouse.ScrollDelta, FishingFilterScrollTarget.Picker);
        }

        public static bool TryConsumeEntryScroll(LegacyMouseSnapshot mouse)
        {
            if (mouse == null || mouse.ScrollDelta == 0)
            {
                return false;
            }

            return TryConsumeNestedScroll(mouse, mouse.ScrollDelta, FishingFilterScrollTarget.Entry);
        }

        public static bool TryConsumePresetScroll(LegacyMouseSnapshot mouse)
        {
            if (mouse == null || mouse.ScrollDelta == 0)
            {
                return false;
            }

            return TryConsumeNestedScroll(mouse, mouse.ScrollDelta, FishingFilterScrollTarget.Preset);
        }

        public static bool TryConsumeNestedScroll(LegacyMouseSnapshot mouse, int scrollDelta)
        {
            if (mouse == null || scrollDelta == 0)
            {
                return false;
            }

            return TryConsumeNestedScroll(mouse, scrollDelta, FishingFilterScrollTarget.Any);
        }

        private static bool TryScrollViewportLocked(LegacyUiRect viewport, int maxScroll, ref int scrollOffset, LegacyMouseSnapshot mouse, int rawScrollDelta)
        {
            if (viewport.Width <= 0 ||
                viewport.Height <= 0 ||
                mouse == null ||
                !viewport.Contains(mouse.X, mouse.Y))
            {
                return false;
            }

            var next = Clamp(scrollOffset + ConvertWheelDelta(rawScrollDelta), 0, Math.Max(0, maxScroll));
            if (next == scrollOffset)
            {
                return false;
            }

            scrollOffset = next;
            return true;
        }

        private static int ConvertWheelDelta(int rawScrollDelta)
        {
            var scrollDelta = -rawScrollDelta / 3;
            if (scrollDelta == 0)
            {
                scrollDelta = rawScrollDelta > 0 ? -40 : 40;
            }

            return scrollDelta;
        }
    }
}
