using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI.Legacy
{
    public static partial class LegacyUiInput
    {
        public static int GetSliderDisplayValue(string sliderId, int fallbackValue)
        {
            lock (SyncRoot)
            {
                if (string.Equals(_activeMode, "slider", StringComparison.Ordinal) &&
                    string.Equals(_activeSliderId, sliderId, StringComparison.Ordinal))
                {
                    return _activeSliderValue;
                }

                if (!string.IsNullOrWhiteSpace(_pendingSliderId) &&
                    string.Equals(_pendingSliderId, sliderId, StringComparison.Ordinal))
                {
                    return _pendingSliderValue;
                }

                return fallbackValue;
            }
        }

        public static void ClearPendingSlider(string sliderId)
        {
            lock (SyncRoot)
            {
                if (string.IsNullOrWhiteSpace(sliderId) || string.Equals(_pendingSliderId, sliderId, StringComparison.Ordinal))
                {
                    _pendingSliderId = string.Empty;
                    _pendingSliderValue = 0;
                }
            }
        }

        public static void BeginOrUpdateSlider(LegacyMouseSnapshot mouse, LegacyUiElement sliderElement)
        {
            if (mouse == null || sliderElement == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (mouse.LeftPressed && sliderElement.Rect.Contains(mouse.X, mouse.Y))
                {
                    _activeMode = "slider";
                    _activeSliderId = sliderElement.Id;
                    _activeSliderValue = LegacySlider.ValueFromMouse(sliderElement.Rect, mouse.X, sliderElement.MinValue, sliderElement.MaxValue);
                }

                if (mouse.LeftDown &&
                    string.Equals(_activeMode, "slider", StringComparison.Ordinal) &&
                    string.Equals(_activeSliderId, sliderElement.Id, StringComparison.Ordinal))
                {
                    _activeSliderValue = LegacySlider.ValueFromMouse(sliderElement.Rect, mouse.X, sliderElement.MinValue, sliderElement.MaxValue);
                }

                if (mouse.LeftReleased &&
                    string.Equals(_activeMode, "slider", StringComparison.Ordinal) &&
                    string.Equals(_activeSliderId, sliderElement.Id, StringComparison.Ordinal))
                {
                    var command = CreateCommand(sliderElement, mouse, true);
                    command.IntValue = _activeSliderValue;
                    _pendingSliderId = _activeSliderId;
                    _pendingSliderValue = _activeSliderValue;
                    EnqueueLocked(command);
                    _activeMode = string.Empty;
                    _activeSliderId = string.Empty;
                }
            }
        }

        public static void CancelActiveSlider(string sliderId)
        {
            lock (SyncRoot)
            {
                if (!string.Equals(_activeMode, "slider", StringComparison.Ordinal))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(sliderId) &&
                    !string.Equals(_activeSliderId, sliderId, StringComparison.Ordinal))
                {
                    return;
                }

                _activeMode = string.Empty;
                _activeSliderId = string.Empty;
                _activeSliderValue = 0;
                if (string.IsNullOrWhiteSpace(sliderId) || string.Equals(_pendingSliderId, sliderId, StringComparison.Ordinal))
                {
                    _pendingSliderId = string.Empty;
                    _pendingSliderValue = 0;
                }
            }
        }
    }
}
