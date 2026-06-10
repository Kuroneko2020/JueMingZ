using System;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        private static void WriteDiagnosticUi(DiagnosticSnapshot snapshot, RuntimeDiagnosticSnapshotSource source)
        {
            var diagnosticSlot = source.DiagnosticSlot;
            var diagnosticSlotInfo = source.DiagnosticSlotInfo;

            snapshot.EnableDiagnosticInputTests = ConfigService.AppSettings.EnableDiagnosticInputTests;
            snapshot.DiagnosticInputTestSlot = diagnosticSlot;
            snapshot.DiagnosticInputTestSlotDisplay = diagnosticSlot + 1;
            snapshot.DiagnosticTestSlot = diagnosticSlot;
            snapshot.DiagnosticTestSlotDisplay = diagnosticSlot + 1;
            snapshot.DiagnosticTestSlotItemType = diagnosticSlotInfo.ItemType;
            snapshot.DiagnosticTestSlotItemName = diagnosticSlotInfo.IsEmpty ? string.Empty : diagnosticSlotInfo.ItemName;
            snapshot.DiagnosticTestSlotItemStack = diagnosticSlotInfo.ItemStack;
            snapshot.DiagnosticTestSlotSuitability = diagnosticSlotInfo.Suitability;
            snapshot.DiagnosticTestSlotHint = diagnosticSlotInfo.Hint;
            snapshot.ActionEventsPath = DiagnosticActionRecorder.ActionEventsPath;
            snapshot.LastActionEventWrittenAtUtc = DiagnosticActionRecorder.LastActionEventWrittenAtUtc;
            snapshot.LastDiagnosticSourceKind = DiagnosticInteractionDiagnostics.LastSourceKind;
            snapshot.LastDiagnosticButtonId = DiagnosticInteractionDiagnostics.LastButtonId;
            snapshot.LastDiagnosticButtonLabel = DiagnosticInteractionDiagnostics.LastButtonLabel;
            snapshot.LastButtonClickUtc = DiagnosticInteractionDiagnostics.LastButtonClickUtc;
            snapshot.LastButtonResultCode = DiagnosticInteractionDiagnostics.LastButtonResultCode;
            snapshot.LastButtonMessage = DiagnosticInteractionDiagnostics.LastButtonMessage;
            snapshot.UiPrimitiveRendererReady = UiPrimitiveRenderer.Ready;
            snapshot.UiPrimitiveRendererLastMessage = UiPrimitiveRenderer.LastError;
            snapshot.UiMouseReadAvailable = !string.Equals(DiagnosticInteractionDiagnostics.MouseReadMode, "none", StringComparison.OrdinalIgnoreCase);
            snapshot.UiMouseReadLastMessage = string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.MouseReadLastError)
                ? DiagnosticInteractionDiagnostics.MouseReadMode
                : DiagnosticInteractionDiagnostics.MouseReadLastError;
            snapshot.UiMouseCaptureAvailable = TerrariaUiMouseCompat.UiMouseCaptureAvailable;
            snapshot.UiMouseCaptureLastMessage = TerrariaUiMouseCompat.UiMouseCaptureLastMessage;
            snapshot.UiClickSuppressionAttempted = DiagnosticInteractionDiagnostics.UiClickSuppressionAttempted;
            snapshot.UiClickSuppressionMode = DiagnosticInteractionDiagnostics.UiClickSuppressionMode;
            snapshot.UiClickSuppressionSucceeded = DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded;
            snapshot.ButtonHoverAtUpdatePrefix = DiagnosticInteractionDiagnostics.ButtonHoverAtUpdatePrefix;
            snapshot.OverlayHoverAtUpdatePrefix = DiagnosticInteractionDiagnostics.OverlayHoverAtUpdatePrefix;
            snapshot.LastMouseX = DiagnosticInteractionDiagnostics.LastMouseX;
            snapshot.LastMouseY = DiagnosticInteractionDiagnostics.LastMouseY;
            snapshot.TerrariaMouseX = DiagnosticInteractionDiagnostics.TerrariaMouseX;
            snapshot.TerrariaMouseY = DiagnosticInteractionDiagnostics.TerrariaMouseY;
            snapshot.TerrariaLeftDown = DiagnosticInteractionDiagnostics.TerrariaLeftDown;
            snapshot.TerrariaLeftReleaseAvailable = DiagnosticInteractionDiagnostics.TerrariaLeftReleaseAvailable;
            snapshot.TerrariaLeftRelease = DiagnosticInteractionDiagnostics.TerrariaLeftRelease;
            snapshot.OsClientMouseX = DiagnosticInteractionDiagnostics.OsClientMouseX;
            snapshot.OsClientMouseY = DiagnosticInteractionDiagnostics.OsClientMouseY;
            snapshot.OsLeftDown = DiagnosticInteractionDiagnostics.OsLeftDown;
            snapshot.UiScale = DiagnosticInteractionDiagnostics.UiScale;
            snapshot.UiScaleAvailable = DiagnosticInteractionDiagnostics.UiScaleAvailable;
            snapshot.UiScaleMatrixAvailable = DiagnosticInteractionDiagnostics.UiScaleMatrixAvailable;
            snapshot.MouseReadMode = DiagnosticInteractionDiagnostics.MouseReadMode;
            snapshot.MouseReadLastError = DiagnosticInteractionDiagnostics.MouseReadLastError;
            snapshot.HitTestMode = DiagnosticInteractionDiagnostics.HitTestMode;
            snapshot.HitTestX = DiagnosticInteractionDiagnostics.HitTestX;
            snapshot.HitTestY = DiagnosticInteractionDiagnostics.HitTestY;
            snapshot.HitTestConflict = DiagnosticInteractionDiagnostics.HitTestConflict;
            snapshot.HitTestCandidateSummary = DiagnosticInteractionDiagnostics.HitTestCandidateSummary;
            snapshot.ClickSource = DiagnosticInteractionDiagnostics.ClickSource;
            snapshot.LastButtonHitTestMode = DiagnosticInteractionDiagnostics.LastButtonHitTestMode;
            snapshot.LastButtonClickSource = DiagnosticInteractionDiagnostics.LastButtonClickSource;
            snapshot.HoveredButtonId = DiagnosticInteractionDiagnostics.HoveredButtonId;
            snapshot.HoveredButtonLabel = DiagnosticInteractionDiagnostics.HoveredButtonLabel;
            snapshot.HoveredButtonHint = DiagnosticInteractionDiagnostics.HoveredButtonHint;
            snapshot.HoveredButtonEnabled = DiagnosticInteractionDiagnostics.HoveredButtonEnabled;
            snapshot.HoveredButtonVisualX = DiagnosticInteractionDiagnostics.HoveredButtonVisualX;
            snapshot.HoveredButtonVisualY = DiagnosticInteractionDiagnostics.HoveredButtonVisualY;
            snapshot.HoveredButtonVisualWidth = DiagnosticInteractionDiagnostics.HoveredButtonVisualWidth;
            snapshot.HoveredButtonVisualHeight = DiagnosticInteractionDiagnostics.HoveredButtonVisualHeight;
            snapshot.HoveredButtonHitX = DiagnosticInteractionDiagnostics.HoveredButtonHitX;
            snapshot.HoveredButtonHitY = DiagnosticInteractionDiagnostics.HoveredButtonHitY;
            snapshot.HoveredButtonHitWidth = DiagnosticInteractionDiagnostics.HoveredButtonHitWidth;
            snapshot.HoveredButtonHitHeight = DiagnosticInteractionDiagnostics.HoveredButtonHitHeight;
            snapshot.LegacyUiLayoutCacheHitCount = LegacyMainWindow.PageLayoutCacheHitCount;
            snapshot.LegacyUiLayoutCacheMissCount = LegacyMainWindow.PageLayoutCacheMissCount;
            snapshot.LegacyUiLastFrameVisibleElementCount = LegacyUiElementFrame.LastFrameElementCount;
            snapshot.LegacyUiHoverReuseCount = LegacyUiElementFrame.HoverReuseCount;
            snapshot.LegacyUiHoverTooltipCacheHitCount = LegacyMainWindow.HoverTooltipCacheHitCount;
            snapshot.LegacyUiHoverTooltipCacheMissCount = LegacyMainWindow.HoverTooltipCacheMissCount;
            snapshot.LegacyUiHoverDiagnosticSuppressedCount = LegacyMainWindow.HoverTooltipDiagnosticSuppressedCount;
            snapshot.LegacyUiScrollSnapshotSkippedCount = LegacyUiInput.ScrollSnapshotSkippedCount;
            snapshot.LegacyUiScrollEventCoalescedCount = LegacyUiInput.ScrollEventCoalescedCount;
            snapshot.LegacyUiRetainedFrameCacheHitCount = LegacyMainWindow.RetainedFrameCacheHitCount;
            snapshot.LegacyUiRetainedFrameCacheMissCount = LegacyMainWindow.RetainedFrameCacheMissCount;
            snapshot.LegacyUiRetainedFrameFallbackCount = LegacyMainWindow.RetainedFrameFallbackCount;
            snapshot.LegacyUiRetainedFrameVisibleElementCount = LegacyMainWindow.RetainedFrameVisibleElementCount;
            snapshot.LegacyUiActionUpdateSkippedCount = LegacyUiActionService.ActionUpdateSkippedCount;
            snapshot.LegacyUiActionUpdateRanCount = LegacyUiActionService.ActionUpdateRanCount;
            snapshot.LegacyUiPendingCommandCountLast = LegacyUiActionService.PendingCommandCountLast;
            snapshot.LegacyUiDispatchedCommandCountLast = LegacyUiActionService.DispatchedCommandCountLast;
            snapshot.LegacyUiDispatchElapsedMsLast = LegacyUiActionService.DispatchElapsedMsLast;
            snapshot.LegacyUiCommandCoalescedCount = LegacyUiActionService.CommandCoalescedCount;
            snapshot.LegacyUiDragFrameActionSkipCount = LegacyUiActionService.DragFrameActionSkipCount;
            snapshot.LastDiagnosticHotkey = DiagnosticActionHotkeyService.LastDiagnosticHotkey;
            snapshot.LastDiagnosticHotkeyUtc = DiagnosticActionHotkeyService.LastDiagnosticHotkeyUtc;
            snapshot.LastDiagnosticHotkeyMessage = DiagnosticActionHotkeyService.LastDiagnosticHotkeyMessage;
            snapshot.QuickActionLastKind = QuickActionDiagnostics.LastKind;
            snapshot.QuickActionLastStatus = QuickActionDiagnostics.LastStatus;
            snapshot.QuickActionLastResultCode = QuickActionDiagnostics.LastResultCode;
            snapshot.QuickActionLastMessage = QuickActionDiagnostics.LastMessage;
            snapshot.MouseTargetLastStatus = MouseTargetDiagnostics.LastStatus;
            snapshot.MouseTargetLastResultCode = MouseTargetDiagnostics.LastResultCode;
            snapshot.MouseTargetLastMessage = MouseTargetDiagnostics.LastMessage;
        }
    }
}
