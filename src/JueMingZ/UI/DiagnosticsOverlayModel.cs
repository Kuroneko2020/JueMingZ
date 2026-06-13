using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.UI
{
    public sealed class DiagnosticsOverlayModel
    {
        public long UpdateCount { get; set; }
        public string Version { get; set; }
        public bool LateBootstrapCompleted { get; set; }
        public bool UpdateHookInstalled { get; set; }
        public bool DrawHookInstalled { get; set; }
        public bool InterfaceLayerHookInstalled { get; set; }
        public bool ItemCheckHookInstalled { get; set; }
        public bool DiagnosticsOverlayVisible { get; set; }
        public long DrawCallCount { get; set; }
        public bool HarmonyLoaded { get; set; }
        public string NetModeDescription { get; set; }
        public int FeatureCatalogCount { get; set; }
        public int UserCategoryCount { get; set; }
        public int CodeDomainCount { get; set; }
        public bool IsInMainMenu { get; set; }
        public bool IsInWorld { get; set; }
        public int PlayerLife { get; set; }
        public int PlayerLifeMax { get; set; }
        public int PlayerMana { get; set; }
        public int PlayerManaMax { get; set; }
        public int InventoryNonEmptyCount { get; set; }
        public int ActiveBuffCount { get; set; }
        public long ActionQueueUpdateCount { get; set; }
        public long FeatureManagerUpdateCount { get; set; }
        public int PendingActionCount { get; set; }
        public string RunningAction { get; set; }
        public string RunningActionKind { get; set; }
        public string RunningActionStatus { get; set; }
        public string LastActionKind { get; set; }
        public string LastActionResult { get; set; }
        public string LastActionResultCode { get; set; }
        public string LastActionUserMessage { get; set; }
        public long LastActionDurationMs { get; set; }
        public string RecentActionLine1 { get; set; }
        public string RecentActionLine2 { get; set; }
        public string RecentActionLine3 { get; set; }
        public string ItemUseBridgeLastStatus { get; set; }
        public string ItemUseBridgeLastMessage { get; set; }
        public bool EnableDiagnosticInputTests { get; set; }
        public bool DiagnosticInputSkipped { get; set; }
        public string DiagnosticInputGateStatus { get; set; }
        public string DiagnosticInputSkipReason { get; set; }
        public int DiagnosticInputTestSlot { get; set; }
        public int DiagnosticInputTestSlotDisplay { get; set; }
        public int DiagnosticTestSlotItemType { get; set; }
        public string DiagnosticTestSlotItemName { get; set; }
        public int DiagnosticTestSlotItemStack { get; set; }
        public string DiagnosticTestSlotSuitability { get; set; }
        public string DiagnosticTestSlotHint { get; set; }
        public string ActionEventsPath { get; set; }
        public string LastDiagnosticSourceKind { get; set; }
        public string LastDiagnosticButtonId { get; set; }
        public string LastDiagnosticButtonLabel { get; set; }
        public string LastButtonResultCode { get; set; }
        public string LastButtonMessage { get; set; }
        public bool UiPrimitiveRendererReady { get; set; }
        public string UiPrimitiveRendererLastMessage { get; set; }
        public bool UiMouseReadAvailable { get; set; }
        public string UiMouseReadLastMessage { get; set; }
        public bool UiMouseCaptureAvailable { get; set; }
        public string UiMouseCaptureLastMessage { get; set; }
        public bool UiClickSuppressionAttempted { get; set; }
        public string UiClickSuppressionMode { get; set; }
        public bool UiClickSuppressionSucceeded { get; set; }
        public int LastMouseX { get; set; }
        public int LastMouseY { get; set; }
        public int TerrariaMouseX { get; set; }
        public int TerrariaMouseY { get; set; }
        public bool TerrariaLeftDown { get; set; }
        public bool TerrariaLeftReleaseAvailable { get; set; }
        public bool TerrariaLeftRelease { get; set; }
        public int OsClientMouseX { get; set; }
        public int OsClientMouseY { get; set; }
        public bool OsLeftDown { get; set; }
        public double UiScale { get; set; }
        public bool UiScaleAvailable { get; set; }
        public bool UiScaleMatrixAvailable { get; set; }
        public string MouseReadMode { get; set; }
        public string MouseReadLastError { get; set; }
        public string HitTestMode { get; set; }
        public int HitTestX { get; set; }
        public int HitTestY { get; set; }
        public bool HitTestConflict { get; set; }
        public string HitTestCandidateSummary { get; set; }
        public string ClickSource { get; set; }
        public string LastButtonHitTestMode { get; set; }
        public string LastButtonClickSource { get; set; }
        public string HoveredButtonId { get; set; }
        public string HoveredButtonLabel { get; set; }
        public string HoveredButtonHint { get; set; }
        public bool HoveredButtonEnabled { get; set; }
        public int HoveredButtonVisualX { get; set; }
        public int HoveredButtonVisualY { get; set; }
        public int HoveredButtonVisualWidth { get; set; }
        public int HoveredButtonVisualHeight { get; set; }
        public int HoveredButtonHitX { get; set; }
        public int HoveredButtonHitY { get; set; }
        public int HoveredButtonHitWidth { get; set; }
        public int HoveredButtonHitHeight { get; set; }
        public string LastDiagnosticHotkey { get; set; }
        public string LastDiagnosticHotkeyMessage { get; set; }
        public string QuickActionLastKind { get; set; }
        public string QuickActionLastStatus { get; set; }
        public string QuickActionLastResultCode { get; set; }
        public string QuickActionLastMessage { get; set; }
        public string MouseTargetLastStatus { get; set; }
        public string MouseTargetLastResultCode { get; set; }
        public string MouseTargetLastMessage { get; set; }
        public double LastRuntimeUpdateMs { get; set; }
        public double LastGameStateReadMs { get; set; }
        public double LastActionQueueUpdateMs { get; set; }
        public bool ReflectionCacheReady { get; set; }
        public int ReflectionCacheMissCount { get; set; }
        public bool InputCompatReady { get; set; }
        public bool SelectedItemGetterReady { get; set; }
        public bool SelectedItemSelectorReady { get; set; }
        public bool SelectedItemAccessorReady { get; set; }
        public string PlayerTypeName { get; set; }
        public string LastInputCompatError { get; set; }
        public bool AutoHealEnabled { get; set; }
        public bool AutoManaEnabled { get; set; }
        public bool AutoBuffEnabled { get; set; }
        public bool AutoNurseEnabled { get; set; }
        public bool AutoStationBuffEnabled { get; set; }
        public int AutoHealThresholdPercent { get; set; }
        public int AutoManaThresholdPercent { get; set; }
        public int AutoHealCooldownTicks { get; set; }
        public int AutoManaCooldownTicks { get; set; }
        public int AutoBuffCooldownTicks { get; set; }
        public string LastAutoHealResult { get; set; }
        public string LastAutoManaResult { get; set; }
        public string LastAutoBuffResult { get; set; }
        public string LastAutoNurseResult { get; set; }
        public string LastAutoStationBuffResult { get; set; }
        public long LastAutoHealTick { get; set; }
        public long LastAutoManaTick { get; set; }
        public long LastAutoBuffTick { get; set; }
        public long LastAutoNurseTick { get; set; }
        public long LastAutoStationBuffTick { get; set; }
        public int LastAutoBuffCountBefore { get; set; }
        public int LastAutoBuffCountAfter { get; set; }
        public string LastError { get; set; }

        public static DiagnosticsOverlayModel FromSnapshot(DiagnosticSnapshot snapshot)
        {
            // Snapshot mapping is display-only; missing fields become labels instead
            // of feeding gameplay decisions back into services.
            if (snapshot == null)
            {
                return new DiagnosticsOverlayModel();
            }

            return new DiagnosticsOverlayModel
            {
                UpdateCount = snapshot.UpdateCount,
                Version = string.IsNullOrWhiteSpace(snapshot.RuntimeVersion) ? snapshot.Version : snapshot.RuntimeVersion,
                LateBootstrapCompleted = snapshot.LateBootstrapCompleted,
                UpdateHookInstalled = snapshot.HookUpdateInstalled,
                DrawHookInstalled = snapshot.DrawHookInstalled,
                InterfaceLayerHookInstalled = snapshot.InterfaceLayerHookInstalled,
                ItemCheckHookInstalled = snapshot.ItemCheckHookInstalled,
                DiagnosticsOverlayVisible = snapshot.DiagnosticsOverlayVisible,
                DrawCallCount = snapshot.DrawCallCount,
                HarmonyLoaded = snapshot.HarmonyLoaded,
                NetModeDescription = snapshot.NetModeDescription,
                FeatureCatalogCount = snapshot.FeatureCatalogCount,
                UserCategoryCount = snapshot.UserCategoryCounts == null ? 0 : snapshot.UserCategoryCounts.Count,
                CodeDomainCount = snapshot.CodeDomainCounts == null ? 0 : snapshot.CodeDomainCounts.Count,
                IsInMainMenu = snapshot.IsInMainMenu,
                IsInWorld = snapshot.IsInWorld,
                PlayerLife = snapshot.PlayerLife,
                PlayerLifeMax = snapshot.PlayerLifeMax,
                PlayerMana = snapshot.PlayerMana,
                PlayerManaMax = snapshot.PlayerManaMax,
                InventoryNonEmptyCount = snapshot.InventoryNonEmptyCount,
                ActiveBuffCount = snapshot.ActiveBuffCount,
                ActionQueueUpdateCount = snapshot.ActionQueueUpdateCount,
                FeatureManagerUpdateCount = snapshot.FeatureManagerUpdateCount,
                PendingActionCount = snapshot.PendingActionCount,
                RunningAction = string.IsNullOrWhiteSpace(snapshot.RunningAction) ? "none" : snapshot.RunningAction,
                RunningActionKind = string.IsNullOrWhiteSpace(snapshot.RunningActionKind) ? "none" : snapshot.RunningActionKind,
                RunningActionStatus = string.IsNullOrWhiteSpace(snapshot.RunningActionStatus) ? "none" : snapshot.RunningActionStatus,
                LastActionKind = string.IsNullOrWhiteSpace(snapshot.LastActionKind) ? "none" : snapshot.LastActionKind,
                LastActionResult = string.IsNullOrWhiteSpace(snapshot.LastActionResult) ? "none" : snapshot.LastActionResult,
                LastActionResultCode = string.IsNullOrWhiteSpace(snapshot.LastActionResultCode) ? "none" : snapshot.LastActionResultCode,
                LastActionUserMessage = string.IsNullOrWhiteSpace(snapshot.LastActionUserMessage) ? "暂无动作结果。" : snapshot.LastActionUserMessage,
                LastActionDurationMs = snapshot.LastActionDurationMs,
                RecentActionLine1 = string.IsNullOrWhiteSpace(snapshot.RecentActionLine1) ? "none" : snapshot.RecentActionLine1,
                RecentActionLine2 = string.IsNullOrWhiteSpace(snapshot.RecentActionLine2) ? "none" : snapshot.RecentActionLine2,
                RecentActionLine3 = string.IsNullOrWhiteSpace(snapshot.RecentActionLine3) ? "none" : snapshot.RecentActionLine3,
                ItemUseBridgeLastStatus = string.IsNullOrWhiteSpace(snapshot.ItemUseBridgeLastStatus) ? "none" : snapshot.ItemUseBridgeLastStatus,
                ItemUseBridgeLastMessage = string.IsNullOrWhiteSpace(snapshot.ItemUseBridgeLastMessage) ? "none" : snapshot.ItemUseBridgeLastMessage,
                EnableDiagnosticInputTests = snapshot.EnableDiagnosticInputTests,
                DiagnosticInputSkipped = snapshot.DiagnosticInputSkipped,
                DiagnosticInputGateStatus = string.IsNullOrWhiteSpace(snapshot.DiagnosticInputGateStatus) ? "unknown" : snapshot.DiagnosticInputGateStatus,
                DiagnosticInputSkipReason = string.IsNullOrWhiteSpace(snapshot.DiagnosticInputSkipReason) ? "none" : snapshot.DiagnosticInputSkipReason,
                DiagnosticInputTestSlot = snapshot.DiagnosticInputTestSlot,
                DiagnosticInputTestSlotDisplay = snapshot.DiagnosticInputTestSlotDisplay,
                DiagnosticTestSlotItemType = snapshot.DiagnosticTestSlotItemType,
                DiagnosticTestSlotItemName = string.IsNullOrWhiteSpace(snapshot.DiagnosticTestSlotItemName) ? "空" : snapshot.DiagnosticTestSlotItemName,
                DiagnosticTestSlotItemStack = snapshot.DiagnosticTestSlotItemStack,
                DiagnosticTestSlotSuitability = string.IsNullOrWhiteSpace(snapshot.DiagnosticTestSlotSuitability) ? "不确定" : snapshot.DiagnosticTestSlotSuitability,
                DiagnosticTestSlotHint = string.IsNullOrWhiteSpace(snapshot.DiagnosticTestSlotHint) ? "建议用武器、工具或药水测试，避免用方块或火把。" : snapshot.DiagnosticTestSlotHint,
                ActionEventsPath = string.IsNullOrWhiteSpace(snapshot.ActionEventsPath) ? "none" : snapshot.ActionEventsPath,
                LastDiagnosticSourceKind = string.IsNullOrWhiteSpace(snapshot.LastDiagnosticSourceKind) ? "none" : snapshot.LastDiagnosticSourceKind,
                LastDiagnosticButtonId = string.IsNullOrWhiteSpace(snapshot.LastDiagnosticButtonId) ? "none" : snapshot.LastDiagnosticButtonId,
                LastDiagnosticButtonLabel = string.IsNullOrWhiteSpace(snapshot.LastDiagnosticButtonLabel) ? "none" : snapshot.LastDiagnosticButtonLabel,
                LastButtonResultCode = string.IsNullOrWhiteSpace(snapshot.LastButtonResultCode) ? "none" : snapshot.LastButtonResultCode,
                LastButtonMessage = string.IsNullOrWhiteSpace(snapshot.LastButtonMessage) ? "none" : snapshot.LastButtonMessage,
                UiPrimitiveRendererReady = snapshot.UiPrimitiveRendererReady,
                UiPrimitiveRendererLastMessage = string.IsNullOrWhiteSpace(snapshot.UiPrimitiveRendererLastMessage) ? "none" : snapshot.UiPrimitiveRendererLastMessage,
                UiMouseReadAvailable = snapshot.UiMouseReadAvailable,
                UiMouseReadLastMessage = string.IsNullOrWhiteSpace(snapshot.UiMouseReadLastMessage) ? "none" : snapshot.UiMouseReadLastMessage,
                UiMouseCaptureAvailable = snapshot.UiMouseCaptureAvailable,
                UiMouseCaptureLastMessage = string.IsNullOrWhiteSpace(snapshot.UiMouseCaptureLastMessage) ? "none" : snapshot.UiMouseCaptureLastMessage,
                UiClickSuppressionAttempted = snapshot.UiClickSuppressionAttempted,
                UiClickSuppressionMode = string.IsNullOrWhiteSpace(snapshot.UiClickSuppressionMode) ? "none" : snapshot.UiClickSuppressionMode,
                UiClickSuppressionSucceeded = snapshot.UiClickSuppressionSucceeded,
                LastMouseX = snapshot.LastMouseX,
                LastMouseY = snapshot.LastMouseY,
                TerrariaMouseX = snapshot.TerrariaMouseX,
                TerrariaMouseY = snapshot.TerrariaMouseY,
                TerrariaLeftDown = snapshot.TerrariaLeftDown,
                TerrariaLeftReleaseAvailable = snapshot.TerrariaLeftReleaseAvailable,
                TerrariaLeftRelease = snapshot.TerrariaLeftRelease,
                OsClientMouseX = snapshot.OsClientMouseX,
                OsClientMouseY = snapshot.OsClientMouseY,
                OsLeftDown = snapshot.OsLeftDown,
                UiScale = snapshot.UiScale <= 0d ? 1d : snapshot.UiScale,
                UiScaleAvailable = snapshot.UiScaleAvailable,
                UiScaleMatrixAvailable = snapshot.UiScaleMatrixAvailable,
                MouseReadMode = string.IsNullOrWhiteSpace(snapshot.MouseReadMode) ? "none" : snapshot.MouseReadMode,
                MouseReadLastError = string.IsNullOrWhiteSpace(snapshot.MouseReadLastError) ? "none" : snapshot.MouseReadLastError,
                HitTestMode = string.IsNullOrWhiteSpace(snapshot.HitTestMode) ? "none" : snapshot.HitTestMode,
                HitTestX = snapshot.HitTestX,
                HitTestY = snapshot.HitTestY,
                HitTestConflict = snapshot.HitTestConflict,
                HitTestCandidateSummary = string.IsNullOrWhiteSpace(snapshot.HitTestCandidateSummary) ? "none" : snapshot.HitTestCandidateSummary,
                ClickSource = string.IsNullOrWhiteSpace(snapshot.ClickSource) ? "none" : snapshot.ClickSource,
                LastButtonHitTestMode = string.IsNullOrWhiteSpace(snapshot.LastButtonHitTestMode) ? "none" : snapshot.LastButtonHitTestMode,
                LastButtonClickSource = string.IsNullOrWhiteSpace(snapshot.LastButtonClickSource) ? "none" : snapshot.LastButtonClickSource,
                HoveredButtonId = string.IsNullOrWhiteSpace(snapshot.HoveredButtonId) ? "none" : snapshot.HoveredButtonId,
                HoveredButtonLabel = string.IsNullOrWhiteSpace(snapshot.HoveredButtonLabel) ? "none" : snapshot.HoveredButtonLabel,
                HoveredButtonHint = string.IsNullOrWhiteSpace(snapshot.HoveredButtonHint) ? "none" : snapshot.HoveredButtonHint,
                HoveredButtonEnabled = snapshot.HoveredButtonEnabled,
                HoveredButtonVisualX = snapshot.HoveredButtonVisualX,
                HoveredButtonVisualY = snapshot.HoveredButtonVisualY,
                HoveredButtonVisualWidth = snapshot.HoveredButtonVisualWidth,
                HoveredButtonVisualHeight = snapshot.HoveredButtonVisualHeight,
                HoveredButtonHitX = snapshot.HoveredButtonHitX,
                HoveredButtonHitY = snapshot.HoveredButtonHitY,
                HoveredButtonHitWidth = snapshot.HoveredButtonHitWidth,
                HoveredButtonHitHeight = snapshot.HoveredButtonHitHeight,
                LastDiagnosticHotkey = string.IsNullOrWhiteSpace(snapshot.LastDiagnosticHotkey) ? "none" : snapshot.LastDiagnosticHotkey,
                LastDiagnosticHotkeyMessage = string.IsNullOrWhiteSpace(snapshot.LastDiagnosticHotkeyMessage) ? "none" : snapshot.LastDiagnosticHotkeyMessage,
                QuickActionLastKind = string.IsNullOrWhiteSpace(snapshot.QuickActionLastKind) ? "none" : snapshot.QuickActionLastKind,
                QuickActionLastStatus = string.IsNullOrWhiteSpace(snapshot.QuickActionLastStatus) ? "none" : snapshot.QuickActionLastStatus,
                QuickActionLastResultCode = string.IsNullOrWhiteSpace(snapshot.QuickActionLastResultCode) ? "none" : snapshot.QuickActionLastResultCode,
                QuickActionLastMessage = string.IsNullOrWhiteSpace(snapshot.QuickActionLastMessage) ? "none" : snapshot.QuickActionLastMessage,
                MouseTargetLastStatus = string.IsNullOrWhiteSpace(snapshot.MouseTargetLastStatus) ? "none" : snapshot.MouseTargetLastStatus,
                MouseTargetLastResultCode = string.IsNullOrWhiteSpace(snapshot.MouseTargetLastResultCode) ? "none" : snapshot.MouseTargetLastResultCode,
                MouseTargetLastMessage = string.IsNullOrWhiteSpace(snapshot.MouseTargetLastMessage) ? "none" : snapshot.MouseTargetLastMessage,
                LastRuntimeUpdateMs = snapshot.LastRuntimeUpdateMs,
                LastGameStateReadMs = snapshot.LastGameStateReadMs,
                LastActionQueueUpdateMs = snapshot.LastActionQueueUpdateMs,
                ReflectionCacheReady = snapshot.ReflectionCacheReady,
                ReflectionCacheMissCount = snapshot.ReflectionCacheMissCount,
                InputCompatReady = snapshot.InputCompatReady,
                SelectedItemGetterReady = snapshot.SelectedItemGetterReady,
                SelectedItemSelectorReady = snapshot.SelectedItemSelectorReady,
                SelectedItemAccessorReady = snapshot.SelectedItemAccessorReady,
                PlayerTypeName = string.IsNullOrWhiteSpace(snapshot.PlayerTypeName) ? "none" : snapshot.PlayerTypeName,
                LastInputCompatError = string.IsNullOrWhiteSpace(snapshot.LastInputCompatError) ? "none" : snapshot.LastInputCompatError,
                AutoHealEnabled = snapshot.AutoHealEnabled,
                AutoManaEnabled = snapshot.AutoManaEnabled,
                AutoBuffEnabled = snapshot.AutoBuffEnabled,
                AutoNurseEnabled = snapshot.AutoNurseEnabled,
                AutoStationBuffEnabled = snapshot.AutoStationBuffEnabled,
                AutoHealThresholdPercent = snapshot.AutoHealThresholdPercent,
                AutoManaThresholdPercent = snapshot.AutoManaThresholdPercent,
                AutoHealCooldownTicks = snapshot.AutoHealCooldownTicks,
                AutoManaCooldownTicks = snapshot.AutoManaCooldownTicks,
                AutoBuffCooldownTicks = snapshot.AutoBuffCooldownTicks,
                LastAutoHealResult = string.IsNullOrWhiteSpace(snapshot.LastAutoHealResult) ? "none" : snapshot.LastAutoHealResult,
                LastAutoManaResult = string.IsNullOrWhiteSpace(snapshot.LastAutoManaResult) ? "none" : snapshot.LastAutoManaResult,
                LastAutoBuffResult = string.IsNullOrWhiteSpace(snapshot.LastAutoBuffResult) ? "none" : snapshot.LastAutoBuffResult,
                LastAutoNurseResult = string.IsNullOrWhiteSpace(snapshot.LastAutoNurseResult) ? "none" : snapshot.LastAutoNurseResult,
                LastAutoStationBuffResult = string.IsNullOrWhiteSpace(snapshot.LastAutoStationBuffResult) ? "none" : snapshot.LastAutoStationBuffResult,
                LastAutoHealTick = snapshot.LastAutoHealTick,
                LastAutoManaTick = snapshot.LastAutoManaTick,
                LastAutoBuffTick = snapshot.LastAutoBuffTick,
                LastAutoNurseTick = snapshot.LastAutoNurseTick,
                LastAutoStationBuffTick = snapshot.LastAutoStationBuffTick,
                LastAutoBuffCountBefore = snapshot.LastAutoBuffCountBefore,
                LastAutoBuffCountAfter = snapshot.LastAutoBuffCountAfter,
                LastError = string.IsNullOrWhiteSpace(snapshot.LastError) ? "none" : snapshot.LastError
            };
        }
    }
}
