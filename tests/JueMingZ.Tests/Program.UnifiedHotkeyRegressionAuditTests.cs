using System;
using JueMingZ.Config;
using JueMingZ.Input.Hotkeys;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeyRegressionAuditContractsStayWired()
        {
            HotkeyTokenCatalogCoversStandardKeyboardMouseTokens();
            HotkeyParserNormalizesTokensAndAliases();
            HotkeyParserReportsFailureReasons();
            HotkeyDisplayFormatterKeepsMainAndNumpadDistinct();
            UnifiedHotkeyCaptureEvaluatesClearCancelAndFailureReasons();
            UnifiedHotkeyCaptureReadsLeftRightModifiersNumpadAndMouse();
            UnifiedHotkeyCaptureSeedPreventsStarterMouseClick();
            UnifiedHotkeyUiHelpersReadNewBindingsInPlace();
            UnifiedHotkeyUiReasonMessagesUsePlayerReadableCopy();
            UnifiedHotkeySettingsDefaultsUseCanonicalQuickAnnouncement();
            UnifiedHotkeySettingsDoNotMigrateLegacyHotkeys();
            UnifiedHotkeySettingsAcceptsOnlyCatalogTokens();
            UnifiedHotkeySettingsCacheSignatureTracksBindings();
            ConfigServiceUnifiedHotkeySaveFailureReturnsSaveFailed();
            UnifiedHotkeyPoliciesExposeStage05Differences();
            UnifiedHotkeyConflictRegistryBuildsEnabledRegistrations();
            UnifiedHotkeyConflictRegistryReportsInternalConflicts();
            UnifiedHotkeyConfigServiceRejectsConflictAndPolicyMismatch();
            UnifiedHotkeyRegistryIgnoresEmptyBindingsAndTerrariaOriginalConflicts();
            UnifiedHotkeyUiHelperReportsConflictReason();
            UnifiedHotkeyRuntimeCacheRefreshesOnlyWhenSignatureChanges();
            UnifiedHotkeyRuntimeTriggerDetectsPressedEdgesAndMouseMiddle();
            UnifiedHotkeyRuntimeGateReturnsRequiredReasons();
            UnifiedHotkeyRuntimeGateBlocksTriggerAndConfigChangeSwapsBinding();
            UnifiedHotkeyRuntimeSwitchFeatureToggleUsesUnifiedOnly();
            UnifiedHotkeyRuntimeSwitchBlueprintUsesUnifiedOnly();
            UnifiedHotkeyRuntimeSwitchActionBindingsUseUnifiedIds();
            UnifiedHotkeyRuntimeSwitchQuickAnnouncementConsumesUnifiedTrigger();
            UnifiedHotkeyRuntimeSwitchQuickAnnouncementRecordsBlockedReason();
            UnifiedHotkeyRegressionAuditLocksReasonMetadataFields();
        }

        private static void UnifiedHotkeyRegressionAuditLocksReasonMetadataFields()
        {
            var playerMessage = UnifiedHotkeyReasonCatalog.BuildRuntimeGateMessage(
                "快捷物品 1",
                "terrariaTextInput:chat");
            AssertContains(playerMessage, "正在输入文字");
            AssertDoesNotContain(playerMessage, UnifiedHotkeyRuntimeGate.TextInputFocused);

            var metadata = UnifiedHotkeyReasonCatalog.BuildDiagnosticMetadataJson(
                "bindingId", UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                "resultCode", "BlockedByUi",
                "hotkeyResultCode", "blocked",
                "reason", "terrariaTextInput:chat",
                "reasonCode", UnifiedHotkeyReasonCatalog.NormalizeRuntimeReasonCode("terrariaTextInput:chat"),
                "blockedReason", UnifiedHotkeyRuntimeGate.TextInputFocused,
                "playerMessage", playerMessage);

            AssertContains(metadata, "\"bindingId\":\"inventory.quick_item.slot1\"");
            AssertContains(metadata, "\"hotkeyResultCode\":\"blocked\"");
            AssertContains(metadata, "\"reasonCode\":\"textInputFocused\"");
            AssertContains(metadata, "\"blockedReason\":\"textInputFocused\"");
            AssertContains(metadata, "\"playerMessage\":\"");
        }
    }
}
