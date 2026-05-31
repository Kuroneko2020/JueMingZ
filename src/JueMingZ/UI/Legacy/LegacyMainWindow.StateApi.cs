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
        public static void ToggleInformationStylePopup(string featureId)
        {
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                return;
            }

            if (string.Equals(_informationStylePopupFeatureId, featureId, StringComparison.Ordinal))
            {
                LegacyHexColorInput.ClearFocus();
                _informationStylePopupFeatureId = string.Empty;
                return;
            }

            LegacyHexColorInput.ClearFocus();
            _informationStylePopupFeatureId = featureId;
        }

        public static void FocusInformationStyleColorInput(string featureId)
        {
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                return;
            }

            _informationStylePopupFeatureId = featureId;
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            LegacyHexColorInput.Focus("information-style-html:" + featureId, InformationStyleHelper.GetColorHex(settings, featureId));
        }

        public static void ClearInformationStyleColorInputFocus()
        {
            LegacyHexColorInput.ClearFocus();
        }

        public static void ToggleMovementSafeLandingConfigPopup()
        {
            _movementSafeLandingConfigOpen = !_movementSafeLandingConfigOpen;
        }

        public static bool IsDeveloperEasterEggConfirmPending()
        {
            return _developerEasterEggConfirmPending;
        }

        public static void SetDeveloperEasterEggConfirmPending(bool pending)
        {
            _developerEasterEggConfirmPending = pending;
        }

        public static bool IsWorldGenerationDetailsHintAlternate()
        {
            return _worldGenerationDetailsHintAlternate;
        }

        public static void ToggleWorldGenerationDetailsHint()
        {
            _worldGenerationDetailsHintAlternate = !_worldGenerationDetailsHintAlternate;
        }

        public static void SetWorldGenerationDetailsHintAlternate(bool alternate)
        {
            _worldGenerationDetailsHintAlternate = alternate;
        }

        public static void OpenQuickItemPicker(int bindingIndex)
        {
            if (bindingIndex < 0)
            {
                CloseQuickItemPicker();
                return;
            }

            CloseAutoSellPicker();
            CloseAutoDiscardPicker();
            StopAutoMiningHotkeyCapture();
            _quickItemPickerBindingIndex = bindingIndex;
            _quickItemPickerOpen = true;
            _quickItemPickerCandidateCache = null;
            _quickItemPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void CloseQuickItemPicker()
        {
            _quickItemPickerOpen = false;
            _quickItemPickerBindingIndex = -1;
            _quickItemPickerCandidateCache = null;
            _quickItemPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void OpenAutoSellPicker(int itemIndex)
        {
            if (itemIndex < 0)
            {
                CloseAutoSellPicker();
                return;
            }

            CloseQuickItemPicker();
            CloseAutoDiscardPicker();
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            _autoSellPickerIndex = itemIndex;
            _autoSellPickerOpen = true;
            AutoSellPickerPendingItemTypes.Clear();
            _autoSellPickerCandidateCache = null;
            _autoSellPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void OpenAutoSellAddPicker()
        {
            CloseQuickItemPicker();
            CloseAutoDiscardPicker();
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            _autoSellPickerIndex = -1;
            _autoSellPickerOpen = true;
            AutoSellPickerPendingItemTypes.Clear();
            _autoSellPickerCandidateCache = null;
            _autoSellPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void CloseAutoSellPicker()
        {
            _autoSellPickerOpen = false;
            _autoSellPickerIndex = -1;
            AutoSellPickerPendingItemTypes.Clear();
            _autoSellPickerCandidateCache = null;
            _autoSellPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static bool ToggleAutoSellPickerPendingItemType(int itemType)
        {
            return TogglePendingItemType(AutoSellPickerPendingItemTypes, itemType);
        }

        public static List<int> GetAutoSellPickerPendingItemTypes()
        {
            return new List<int>(AutoSellPickerPendingItemTypes);
        }

        public static void OpenAutoDiscardPicker(int itemIndex)
        {
            if (itemIndex < 0)
            {
                CloseAutoDiscardPicker();
                return;
            }

            CloseQuickItemPicker();
            CloseAutoSellPicker();
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            _autoDiscardPickerIndex = itemIndex;
            _autoDiscardPickerOpen = true;
            AutoDiscardPickerPendingItemTypes.Clear();
            _autoDiscardPickerCandidateCache = null;
            _autoDiscardPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void OpenAutoDiscardAddPicker()
        {
            CloseQuickItemPicker();
            CloseAutoSellPicker();
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            _autoDiscardPickerIndex = -1;
            _autoDiscardPickerOpen = true;
            AutoDiscardPickerPendingItemTypes.Clear();
            _autoDiscardPickerCandidateCache = null;
            _autoDiscardPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static void CloseAutoDiscardPicker()
        {
            _autoDiscardPickerOpen = false;
            _autoDiscardPickerIndex = -1;
            AutoDiscardPickerPendingItemTypes.Clear();
            _autoDiscardPickerCandidateCache = null;
            _autoDiscardPickerCandidateCacheUtc = DateTime.MinValue;
        }

        public static bool ToggleAutoDiscardPickerPendingItemType(int itemType)
        {
            return TogglePendingItemType(AutoDiscardPickerPendingItemTypes, itemType);
        }

        public static List<int> GetAutoDiscardPickerPendingItemTypes()
        {
            return new List<int>(AutoDiscardPickerPendingItemTypes);
        }

        public static bool NeedsMiscInventorySnapshot()
        {
            return _quickItemPickerOpen || _autoSellPickerOpen || _autoDiscardPickerOpen;
        }

        public static void StartQuickItemHotkeyCapture(int bindingIndex)
        {
            if (bindingIndex < 0)
            {
                StopQuickItemHotkeyCapture();
                return;
            }

            _quickItemHotkeyCaptureActive = true;
            _quickItemHotkeyCaptureBindingIndex = bindingIndex;
            _autoMiningHotkeyCaptureActive = false;
            AutoMiningCaptureWasDown.Clear();
            QuickItemCaptureWasDown.Clear();
        }

        public static void StopQuickItemHotkeyCapture()
        {
            _quickItemHotkeyCaptureActive = false;
            _quickItemHotkeyCaptureBindingIndex = -1;
            QuickItemCaptureWasDown.Clear();
        }

        public static void StartAutoMiningHotkeyCapture()
        {
            CloseQuickItemPicker();
            CloseAutoSellPicker();
            CloseAutoDiscardPicker();
            StopQuickItemHotkeyCapture();
            _autoMiningHotkeyCaptureActive = true;
            AutoMiningCaptureWasDown.Clear();
        }

        public static void StopAutoMiningHotkeyCapture()
        {
            _autoMiningHotkeyCaptureActive = false;
            AutoMiningCaptureWasDown.Clear();
        }

        private static bool TogglePendingItemType(List<int> itemTypes, int itemType)
        {
            if (itemTypes == null || itemType <= 0)
            {
                return false;
            }

            var index = itemTypes.IndexOf(itemType);
            if (index >= 0)
            {
                itemTypes.RemoveAt(index);
                return false;
            }

            itemTypes.Add(itemType);
            return true;
        }
    }
}
