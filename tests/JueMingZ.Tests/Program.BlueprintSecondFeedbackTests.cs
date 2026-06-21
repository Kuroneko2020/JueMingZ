using System;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void BlueprintSecondFeedbackStage07AuditContractsStayWired()
        {
            try
            {
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                LegacyMainUiState.SetVisible(true);

                var settings = AppSettings.CreateDefault();
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintCreateActionElementIdForTesting(),
                    Label = "蓝图:创建蓝图:开始",
                    Kind = "button",
                    MouseCaptured = true
                });

                if (!string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal) ||
                    !BlueprintCreationMaskState.GetSnapshot().Active ||
                    LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Expected stage 02 create action to enter creation mode and close F5.");
                }

                ClickTileForBlueprintCreation(21, 22);
                var exited = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
                var preservedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!exited.Succeeded ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                    preservedMask.Active ||
                    preservedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(preservedMask, 21, 22))
                {
                    throw new InvalidOperationException("Expected stage 02 repeated create toggle to exit while preserving the pending mask.");
                }

                var emptyCreateFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(1280, 720, blueprintCreationActive: true));
                AssertBlueprintHandheldButtons(
                    emptyCreateFrame,
                    BlueprintHandheldActionBarState.ButtonIdSave,
                    BlueprintHandheldActionBarState.ButtonIdExitCreate);
                AssertBlueprintHandheldButtonState(
                    emptyCreateFrame,
                    BlueprintHandheldActionBarState.ButtonIdSave,
                    "保存蓝图",
                    BlueprintHandheldActionBarState.SaveDisabledTooltip,
                    false);

                var selectedCreateFrame = BuildBlueprintHandheldFrame(
                    true,
                    BlueprintSettings.DefaultToolItemId,
                    BlueprintHandheldEnvironment(
                        1280,
                        720,
                        blueprintCreationActive: true,
                        blueprintCreationHasPendingSelection: true,
                        blueprintCreationSelectedCount: 2));
                AssertBlueprintHandheldButtons(
                    selectedCreateFrame,
                    BlueprintHandheldActionBarState.ButtonIdSave,
                    BlueprintHandheldActionBarState.ButtonIdExitCreate,
                    BlueprintHandheldActionBarState.ButtonIdClearSelection);

                var overlayContract = BlueprintCreationOverlay.GetVisualContractForTesting();
                AssertContains(overlayContract, "world-hover");
                AssertContains(overlayContract, "air-select");
                AssertContains(overlayContract, "lower-saturation-lower-alpha-no-border");
                AssertContains(overlayContract, "continuous-row-runs");

                var promptContract = BlueprintCreationPromptService.GetLocalPromptContractForTesting();
                AssertContains(promptContract, "no-chat");
                AssertContains(promptContract, "no-network");
                AssertContains(promptContract, "no-player-state");

                string normalized;
                if (FeatureToggleHotkeyChord.TryNormalize("Backspace", out normalized) ||
                    FeatureToggleHotkeyChord.TryNormalize("Ctrl+Backspace", out normalized) ||
                    QuickItemHotkeyService.TryNormalizeHotkeyForTesting("Backspace", out normalized) ||
                    QuickItemHotkeyService.TryNormalizeHotkeyForTesting("Ctrl+Backspace", out normalized) ||
                    !string.IsNullOrWhiteSpace(MapQuickAnnouncementSettings.NormalizeKeyboardKey("Backspace")) ||
                    !string.IsNullOrWhiteSpace(MapQuickAnnouncementSettings.NormalizeTriggerKey("Backspace")))
                {
                    throw new InvalidOperationException("Expected stage 06 Backspace to remain clear-only and unavailable as a saved hotkey token.");
                }
            }
            finally
            {
                LegacyMainUiState.SetVisible(false);
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
            }
        }
    }
}
