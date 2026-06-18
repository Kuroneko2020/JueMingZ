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

        public static void ToggleAutoCaptureCritterConfigPopup()
        {
            _autoCaptureCritterConfigOpen = !_autoCaptureCritterConfigOpen;
        }

        public static void ToggleAutoRecoveryItemConfigPopup(string kind)
        {
            kind = NormalizeAutoRecoveryItemConfigKind(kind);
            if (string.IsNullOrEmpty(kind))
            {
                _autoRecoveryItemConfigKind = string.Empty;
                return;
            }

            _autoRecoveryItemConfigKind = string.Equals(_autoRecoveryItemConfigKind, kind, StringComparison.Ordinal)
                ? string.Empty
                : kind;
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
            StopMapQuickAnnouncementHotkeyCapture();
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
            StopMapQuickAnnouncementHotkeyCapture();
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
            StopMapQuickAnnouncementHotkeyCapture();
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
            StopMapQuickAnnouncementHotkeyCapture();
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
            StopMapQuickAnnouncementHotkeyCapture();
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
            return NeedsItemsInventorySnapshot();
        }

        public static bool NeedsItemsInventorySnapshot()
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
            _mapQuickAnnouncementHotkeyCaptureSlot = string.Empty;
            CloseFeatureToggleHotkeyModal();
            AutoMiningCaptureWasDown.Clear();
            QuickItemCaptureWasDown.Clear();
            MapQuickAnnouncementCaptureWasDown.Clear();
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
            StopMapQuickAnnouncementHotkeyCapture();
            CloseFeatureToggleHotkeyModal();
            _autoMiningHotkeyCaptureActive = true;
            AutoMiningCaptureWasDown.Clear();
        }

        public static void StopAutoMiningHotkeyCapture()
        {
            _autoMiningHotkeyCaptureActive = false;
            AutoMiningCaptureWasDown.Clear();
        }

        public static void StartMapQuickAnnouncementHotkeyCapture(string slot)
        {
            var slotId = MapQuickAnnouncementSettings.NormalizeHotkeySlotId(slot);
            if (slotId.Length <= 0)
            {
                StopMapQuickAnnouncementHotkeyCapture();
                return;
            }

            CloseQuickItemPicker();
            CloseAutoSellPicker();
            CloseAutoDiscardPicker();
            StopQuickItemHotkeyCapture();
            StopAutoMiningHotkeyCapture();
            CloseFeatureToggleHotkeyModal();
            _mapQuickAnnouncementHotkeyCaptureSlot = slotId;
            // Seed held keys so the double-click that starts capture cannot become the captured trigger.
            MapQuickAnnouncementHotkeyTokens.SeedCaptureState(MapQuickAnnouncementCaptureWasDown, IsKeyDown);
        }

        public static void StopMapQuickAnnouncementHotkeyCapture()
        {
            _mapQuickAnnouncementHotkeyCaptureSlot = string.Empty;
            MapQuickAnnouncementCaptureWasDown.Clear();
        }

        public static void ToggleMapDeathHistoryPopup()
        {
            _mapDeathHistoryPopupOpen = !_mapDeathHistoryPopupOpen;
            if (_mapDeathHistoryPopupOpen)
            {
                _mapDeathHistoryPageIndex = 0;
            }
        }

        public static void CloseMapDeathHistoryPopup()
        {
            _mapDeathHistoryPopupOpen = false;
        }

        public static int MoveMapDeathHistoryPage(int delta)
        {
            if (!_mapDeathHistoryPopupOpen)
            {
                _mapDeathHistoryPopupOpen = true;
            }

            _mapDeathHistoryPageIndex = Math.Max(0, _mapDeathHistoryPageIndex + delta);
            return _mapDeathHistoryPageIndex;
        }

        public static bool IsMapDeathHistoryPopupOpen()
        {
            return _mapDeathHistoryPopupOpen;
        }

        public static int GetMapDeathHistoryPageIndex()
        {
            return _mapDeathHistoryPageIndex;
        }

        public static int MoveMapCustomMarkerPage(int delta)
        {
            _mapCustomMarkerPageIndex = Math.Max(0, _mapCustomMarkerPageIndex + delta);
            return _mapCustomMarkerPageIndex;
        }

        public static int GetMapCustomMarkerPageIndex()
        {
            return _mapCustomMarkerPageIndex;
        }

        public static void ToggleMapRevealedAreaDetailsPopup()
        {
            _mapRevealedAreaDetailsPopupOpen = !_mapRevealedAreaDetailsPopupOpen;
        }

        public static void CloseMapRevealedAreaDetailsPopup()
        {
            _mapRevealedAreaDetailsPopupOpen = false;
        }

        public static bool IsMapRevealedAreaDetailsPopupOpen()
        {
            return _mapRevealedAreaDetailsPopupOpen;
        }

        internal static void ResetMapDeathHistoryPopupForTesting()
        {
            _mapDeathHistoryPopupOpen = false;
            _mapDeathHistoryAnchor = new LegacyUiRect();
            _mapDeathHistoryAnchorVisible = false;
            _mapDeathHistoryPageIndex = 0;
        }

        internal static void ResetMapCustomMarkerPaginationForTesting()
        {
            _mapCustomMarkerPageIndex = 0;
        }

        internal static void ResetMapRevealedAreaDetailsPopupForTesting()
        {
            _mapRevealedAreaDetailsPopupOpen = false;
            _mapRevealedAreaDetailsAnchor = new LegacyUiRect();
            _mapRevealedAreaDetailsAnchorVisible = false;
        }

        internal static string GetMapQuickAnnouncementHotkeyCaptureSlotForTesting()
        {
            return _mapQuickAnnouncementHotkeyCaptureSlot;
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

        private static string NormalizeAutoRecoveryItemConfigKind(string kind)
        {
            if (string.Equals(kind, "heal", StringComparison.OrdinalIgnoreCase))
            {
                return "heal";
            }

            return string.Equals(kind, "mana", StringComparison.OrdinalIgnoreCase) ? "mana" : string.Empty;
        }
    }
}
