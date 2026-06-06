using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.BuffAndRecovery;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Fishing.Filtering;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void RefreshCandidates(LegacyUiCommand command)
        {
            var before = BuildBuffBeforeJson(null);
            var scan = LegacyMainUiState.RefreshBuffCandidates("Ui.RefreshCandidates");
            var ok = scan != null && scan.PlayerAvailable;
            Record(
                command,
                "BuffPotion.ScanCandidates",
                "BuffPotion",
                ok ? "Succeeded" : "BlockedByEnvironment",
                ok
                    ? "Buff potion candidates refreshed: " + scan.Candidates.Count.ToString(CultureInfo.InvariantCulture) + "."
                    : "Buff potion scan failed: " + (scan == null ? "scan unavailable" : FirstNonEmpty(scan.Error, scan.Message)),
                before,
                BuildScanAfterJson(scan),
                "{\"submitted\":false,\"candidateCount\":" + (scan == null ? "0" : scan.Candidates.Count.ToString(CultureInfo.InvariantCulture)) + "}",
                "Button");
        }

        private static void AddCandidate(LegacyUiCommand command)
        {
            var candidate = command.Candidate;
            var before = BuildBuffBeforeJson(candidate);
            string message;
            var changed = BuffPotionWhitelistService.Add(candidate, out message);
            if (changed)
            {
                AutoRecoveryService.RequestImmediateAutoBuffReconcile("WhitelistAdded");
            }

            Record(
                command,
                "BuffPotion.AddWhitelist",
                "BuffPotion",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildWhitelistAfterJson("add", candidate),
                "{\"submitted\":false,\"immediateReconcileTriggered\":" + BoolRaw(changed) + ",\"triggerReason\":\"WhitelistAdded\",\"selectedItemType\":" + IntRaw(candidate == null ? 0 : candidate.ItemType) + ",\"buffType\":" + IntRaw(candidate == null ? 0 : candidate.BuffType) + "}",
                "Button");
        }

        private static void RemoveWhitelistEntry(LegacyUiCommand command)
        {
            var entry = command.WhitelistEntry;
            var candidate = new BuffPotionCandidate
            {
                ItemType = entry == null ? 0 : entry.ItemType,
                BuffType = entry == null ? 0 : entry.BuffType,
                ItemName = entry == null ? string.Empty : entry.ItemName,
                BuffName = entry == null ? string.Empty : entry.BuffName
            };
            var before = BuildBuffBeforeJson(candidate);
            string message;
            var changed = BuffPotionWhitelistService.Remove(candidate, out message);
            Record(
                command,
                "BuffPotion.RemoveWhitelist",
                "BuffPotion",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildWhitelistAfterJson("remove", candidate),
                "{\"submitted\":false,\"selectedItemType\":" + IntRaw(candidate.ItemType) + ",\"buffType\":" + IntRaw(candidate.BuffType) + "}",
                "Button");
        }

        private static void ClearWhitelist(LegacyUiCommand command)
        {
            var before = BuildBuffBeforeJson(null);
            var removed = BuffPotionWhitelistService.Clear();
            Record(
                command,
                "BuffPotion.ClearWhitelist",
                "BuffPotion",
                "Succeeded",
                "Buff potion whitelist cleared; removed " + removed.ToString(CultureInfo.InvariantCulture) + " entries.",
                before,
                BuildWhitelistAfterJson("clear", null),
                "{\"submitted\":false,\"removedCount\":" + removed.ToString(CultureInfo.InvariantCulture) + "}",
                "Button");
        }

        private static void Record(
            LegacyUiCommand command,
            string scenario,
            string actionKind,
            string resultCode,
            string message,
            string beforeJson,
            string afterJson,
            string verificationJson,
            string sourceKind)
        {
            RecordSource(command, sourceKind);
            DiagnosticActionRecorder.RecordCustomEvent(
                Guid.Empty,
                scenario,
                actionKind,
                string.Empty,
                resultCode,
                resultCode,
                message,
                0,
                string.IsNullOrWhiteSpace(beforeJson) ? "{}" : beforeJson,
                string.IsNullOrWhiteSpace(afterJson) ? "{}" : afterJson,
                string.IsNullOrWhiteSpace(verificationJson) ? "{}" : verificationJson,
                sourceKind,
                "LegacyMainWindow",
                string.Equals(sourceKind, "Button", StringComparison.OrdinalIgnoreCase) ? command.ElementId : string.Empty,
                string.Equals(sourceKind, "Button", StringComparison.OrdinalIgnoreCase) ? command.Label : string.Empty);
        }

        private static void RecordSource(LegacyUiCommand command, string sourceKind)
        {
            if (command == null)
            {
                return;
            }

            var button = new DiagnosticTestButton
            {
                Id = command.ElementId,
                Label = command.Label,
                X = command.Rect.X,
                Y = command.Rect.Y,
                Width = command.Rect.Width,
                Height = command.Rect.Height,
                Enabled = true
            };
            DiagnosticInteractionDiagnostics.RecordUiClickSuppression(true, "LegacyMainUi.Action", command.MouseCaptured, true, true);
            DiagnosticInteractionDiagnostics.RecordButton(
                command.ElementId,
                command.Label,
                "LegacyMainUi",
                "Mouse",
                command.MouseX,
                command.MouseY,
                false,
                command.ElementId,
                button);
        }

        private static string BuildAutoRecoveryBeforeJson()
        {
            return BuildAutoRecoveryStateJson(AutoRecoveryService.GetStateSnapshot());
        }

        private static string BuildAutoRecoveryAfterJson()
        {
            return BuildAutoRecoveryStateJson(AutoRecoveryService.GetStateSnapshot());
        }

        private static string BuildUiOptionStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var travelMenu = TravelMenuService.GetDiagnostics();
            var worldGenViewerEnabled = settings.DiagnosticsWorldGenDebugViewerEnabled;
            var debugCommandsEnabled = settings.DiagnosticsDeveloperDebugCommandsEnabled;
            var quickItemBindingCount = ConfigService.HotkeySettings == null || ConfigService.HotkeySettings.QuickItemHotkeyBindings == null
                ? 0
                : ConfigService.HotkeySettings.QuickItemHotkeyBindings.Count;
            return "{" +
                   "\"autoNurseEnabled\":" + BoolRaw(settings.AutoNurseEnabled) + "," +
                   "\"autoStationBuffEnabled\":" + BoolRaw(settings.AutoStationBuffEnabled) + "," +
                   "\"autoBuffFollowAddEnabled\":" + BoolRaw(settings.AutoBuffFollowAddEnabled) + "," +
                   "\"autoBuffFollowRemoveEnabled\":" + BoolRaw(settings.AutoBuffFollowRemoveEnabled) + "," +
                   "\"miscTravelMenuEnabled\":" + BoolRaw(settings.WorldAutomationTravelMenuEnabled) + "," +
                   "\"miscTravelMenuSessionActive\":" + BoolRaw(travelMenu != null && travelMenu.SessionActive) + "," +
                   "\"miscTravelMenuSaveGuardHookInstalled\":" + BoolRaw(travelMenu != null && travelMenu.SaveGuardHookInstalled) + "," +
                   "\"miscTravelMenuCreativeUiHookInstalled\":" + BoolRaw(travelMenu != null && travelMenu.CreativeUiHookInstalled) + "," +
                   "\"miscTravelMenuScopedPowerHookInstalled\":" + BoolRaw(travelMenu != null && travelMenu.ScopedPowerHookInstalled) + "," +
                   "\"miscWorldGenDebugViewerEnabled\":" + BoolRaw(worldGenViewerEnabled) + "," +
                   "\"miscWorldGenDebugViewerSessionConfiguredEnabled\":" + BoolRaw(WorldGenDebugCompat.WorldGenSessionConfiguredEnabled) + "," +
                   "\"miscWorldGenDebugViewerRestartRequired\":" + BoolRaw(WorldGenDebugCompat.IsWorldGenRestartRequired(worldGenViewerEnabled)) + "," +
                   "\"miscWorldGenDebugFieldEnabled\":" + BoolRaw(WorldGenDebugCompat.Enabled) + "," +
                   "\"miscDebugCommandsEnabled\":" + BoolRaw(debugCommandsEnabled) + "," +
                   "\"miscDebugCommandsSessionConfiguredEnabled\":" + BoolRaw(WorldGenDebugCompat.SessionConfiguredEnabled) + "," +
                   "\"miscDebugCommandsRestartRequired\":" + BoolRaw(WorldGenDebugCompat.IsRestartRequired(debugCommandsEnabled)) + "," +
                   "\"miscQuickItemHotkeysEnabled\":" + BoolRaw(settings.InventoryQuickItemHotkeysEnabled) + "," +
                   "\"miscQuickItemHotkeysBindingCount\":" + IntRaw(quickItemBindingCount) + "," +
                   "\"miscAutoMiningMode\":\"" + EscapeJson(AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode)) + "\"," +
                   "\"miscAutoCaptureCritterMode\":\"" + EscapeJson(AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled)) + "\"," +
                   "\"miscAutoCaptureCritterEnabled\":" + BoolRaw(settings.WorldAutomationAutoCaptureCritterEnabled) + "," +
                   "\"miscAutoHarvestEnabled\":" + BoolRaw(settings.WorldAutomationAutoHarvestEnabled) + "," +
                   "\"miscAutoStackEnabled\":" + BoolRaw(settings.InventoryAutoStackEnabled) + "," +
                   "\"miscQuickBagOpenEnabled\":" + BoolRaw(settings.InventoryQuickBagOpenEnabled) + "," +
                   "\"miscAutoExtractinatorEnabled\":" + BoolRaw(settings.InventoryAutoExtractinatorEnabled) + "," +
                   "\"miscKeepFavoritedEnabled\":" + BoolRaw(settings.InventoryKeepFavoritedEnabled) + "," +
                   "\"miscAutoSellEnabled\":" + BoolRaw(settings.InventoryAutoSellEnabled) + "," +
                   "\"miscAutoSellItemCount\":" + IntRaw(CountValidAutoSellItems(settings.InventoryAutoSellItemIds)) + "," +
                   "\"miscAutoDiscardEnabled\":" + BoolRaw(settings.InventoryAutoDiscardEnabled) + "," +
                   "\"miscAutoDiscardItemCount\":" + IntRaw(CountValidAutoDiscardItems(settings.InventoryAutoDiscardItemIds)) + "," +
                   "\"miscQuickReforgeEnabled\":" + BoolRaw(settings.NpcAutoReforgeEnabled) + "," +
                   "\"miscQuickReforgePrefixCount\":" + IntRaw(CountValidQuickReforgePrefixes(settings.NpcAutoReforgePrefixes)) + "," +
                   "\"miscQuickReforgeInputActive\":" + BoolRaw(LegacyTextInput.IsFocused("misc-quick-reforge:prefix")) + "," +
                   "\"miscAutoTaxCollectEnabled\":" + BoolRaw(settings.NpcAutoTaxCollectEnabled) + "," +
                   "\"miscDeveloperEasterEggPending\":" + BoolRaw(LegacyMainWindow.IsDeveloperEasterEggConfirmPending()) +
                   "}";
        }

        private static string BuildCombatUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return "{" +
                   "\"combatAimAssistRadius\":" + IntRaw(settings.CombatAimAssistRadius) + "," +
                   "\"aimRangeOrigin\":\"" + EscapeJson(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin)) + "\"," +
                   "\"activeRangeMode\":\"" + (string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase) ? "PlayerScreen" : "CursorSlider") + "\"," +
                   "\"aimTargetPriority\":\"" + EscapeJson(CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority)) + "\"," +
                   "\"cursorAimRadius\":" + IntRaw(settings.CursorAimRadius) + "," +
                   "\"playerAimRadius\":" + IntRaw(settings.PlayerAimRadius) + "," +
                   "\"sliderDisabled\":" + BoolRaw(string.Equals(CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin), CombatAimModes.RangeOriginPlayer, StringComparison.OrdinalIgnoreCase)) + "," +
                   "\"releaseHoldTicks\":" + IntRaw(settings.ReleaseHoldTicks) + "," +
                   "\"persistentCursorAimEnabled\":" + BoolRaw(settings.PersistentCursorAimEnabled) + "," +
                   "\"combatAimTrackDummyEnabled\":" + BoolRaw(settings.CombatAimTrackDummyEnabled) + "," +
                   "\"combatAimMarkerEnabled\":" + BoolRaw(settings.CombatAimMarkerEnabled) + "," +
                   "\"combatAutoClickerEnabled\":" + BoolRaw(settings.CombatAutoClickerEnabled) + "," +
                   "\"combatFlailComboEnabled\":" + BoolRaw(settings.CombatFlailComboEnabled) + "," +
                   "\"combatPerfectRevolverEnabled\":" + BoolRaw(settings.CombatPerfectRevolverEnabled) + "," +
                   "\"combatMagicStringClickerEnabled\":" + BoolRaw(settings.CombatMagicStringClickerEnabled) + "," +
                   "\"combatAutoFacingEnabled\":" + BoolRaw(settings.CombatAutoFacingEnabled) + "," +
                   "\"combatEquipmentWarningEnabled\":" + BoolRaw(settings.CombatEquipmentWarningEnabled) + "," +
                   "\"combatGoblinExecutionEnabled\":" + BoolRaw(settings.CombatGoblinExecutionEnabled) +
                   "}";
        }

        private static string BuildMovementUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return "{" +
                   "\"movementSimulatedMultiJumpEnabled\":" + BoolRaw(settings.MovementSimulatedMultiJumpEnabled) + "," +
                   "\"movementContinuousDashEnabled\":" + BoolRaw(settings.MovementContinuousDashEnabled) + "," +
                   "\"movementContinuousDashMode\":\"" + EscapeJson(MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode)) + "\"," +
                   "\"movementTeleportCorrectionEnabled\":" + BoolRaw(settings.MovementTeleportCorrectionEnabled) + "," +
                   "\"movementSafeLandingEnabled\":" + BoolRaw(settings.MovementSafeLandingEnabled) + "," +
                   "\"movementSafeLandingConfigSummary\":\"" + EscapeJson(MovementSafeLandingOptionCatalog.BuildConfigSummary(settings)) + "\"," +
                   "\"movementSimulatedMultiJumpImplemented\":true," +
                   "\"movementContinuousDashImplemented\":true," +
                   "\"movementTeleportCorrectionImplemented\":true," +
                   "\"movementSafeLandingImplemented\":true" +
                   "}";
        }

        private static string BuildFishingUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var allowExactCount = settings.FishingFilterAllowExactEntries == null ? 0 : settings.FishingFilterAllowExactEntries.Count;
            var denyExactCount = settings.FishingFilterDenyExactEntries == null ? 0 : settings.FishingFilterDenyExactEntries.Count;
            var allowKeywordCount = settings.FishingFilterAllowKeywords == null ? 0 : settings.FishingFilterAllowKeywords.Count;
            var denyKeywordCount = settings.FishingFilterDenyKeywords == null ? 0 : settings.FishingFilterDenyKeywords.Count;
            var filterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            var matchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            var currentActiveListCount = ResolveFishingFilterActiveListCount(
                filterMode,
                matchMode,
                allowExactCount,
                denyExactCount,
                allowKeywordCount,
                denyKeywordCount);
            var activeExactCount = ResolveFishingFilterActiveExactCount(filterMode, allowExactCount, denyExactCount);
            FishingFilterUiState.EnsureModeSignature(settings);
            var activeKeywordCount = ResolveFishingFilterActiveKeywordCount(filterMode, allowKeywordCount, denyKeywordCount);
            var keywordInputActive = LegacyTextInput.IsFocused(FishingFilterUiState.KeywordInputId);
            var globalSearchInputActive = LegacyTextInput.IsFocused(FishingFilterUiState.GlobalSearchInputId);
            var presetNameInputActive = LegacyTextInput.IsFocused(FishingFilterUiState.PresetNameInputId);
            var quickRenameInputActive = LegacyTextInput.IsFocused("fishing-quick-rename:name");
            string currentPlayerName;
            string currentPlayerNameMessage;
            var currentPlayerNameAvailable = PlayerRenameCompat.TryReadCurrentPlayerName(out currentPlayerName, out currentPlayerNameMessage);
            var visiblePresetCount = CountVisibleFishingFilterPresets(settings, filterMode, matchMode);
            return "{" +
                   "\"fishingAutoFishEnabled\":" + BoolRaw(settings.FishingAutoFishEnabled) + "," +
                   "\"fishingAutoLoadoutEnabled\":" + BoolRaw(settings.FishingAutoLoadoutEnabled) + "," +
                   "\"fishingAutoEquipmentEnabled\":" + BoolRaw(settings.FishingAutoEquipmentEnabled) + "," +
                   "\"fishingAutoStoreQuestFishEnabled\":" + BoolRaw(FishingAutoStoreModes.IsEnabled(settings.FishingAutoStoreMode)) + "," +
                   "\"fishingAutoStoreMode\":\"" + EscapeJson(FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled)) + "\"," +
                   "\"fishingFilterCutRodSkipEnabled\":" + BoolRaw(settings.FishingFilterCutRodSkipEnabled) + "," +
                   "\"fishingFilterMode\":\"" + EscapeJson(filterMode) + "\"," +
                   "\"fishingFilterMatchMode\":\"" + EscapeJson(matchMode) + "\"," +
                   "\"fishingFilterCrateRule\":\"" + EscapeJson(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule)) + "\"," +
                   "\"fishingFilterQuestFishRule\":\"" + EscapeJson(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule)) + "\"," +
                   "\"fishingFilterEnemyRule\":\"" + EscapeJson(FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule)) + "\"," +
                   "\"fishingAutoFishImplemented\":true," +
                   "\"fishingAutoLoadoutImplemented\":true," +
                   "\"fishingAutoStoreQuestFishImplemented\":true," +
                   "\"fishingAutoEquipmentImplemented\":true," +
                   "\"fishingFilterImplemented\":true," +
                   "\"allowExactCount\":" + IntRaw(allowExactCount) + "," +
                   "\"denyExactCount\":" + IntRaw(denyExactCount) + "," +
                   "\"allowKeywordCount\":" + IntRaw(allowKeywordCount) + "," +
                   "\"denyKeywordCount\":" + IntRaw(denyKeywordCount) + "," +
                   "\"activeExactCount\":" + IntRaw(activeExactCount) + "," +
                   "\"activeKeywordCount\":" + IntRaw(activeKeywordCount) + "," +
                   "\"currentActiveListCount\":" + IntRaw(currentActiveListCount) + "," +
                   "\"fishingFilterListCount\":" + IntRaw(currentActiveListCount) + "," +
                   "\"keywordInputActive\":" + BoolRaw(keywordInputActive) + "," +
                   "\"keywordDraftLength\":" + IntRaw(keywordInputActive ? LegacyTextInput.DraftLength : 0) + "," +
                   "\"globalSearchInputActive\":" + BoolRaw(globalSearchInputActive) + "," +
                   "\"globalSearchDraftLength\":" + IntRaw(globalSearchInputActive ? LegacyTextInput.DraftLength : 0) + "," +
                   "\"presetNameInputActive\":" + BoolRaw(presetNameInputActive) + "," +
                   "\"presetNameDraftLength\":" + IntRaw(presetNameInputActive ? LegacyTextInput.DraftLength : 0) + "," +
                   "\"quickRenameInputActive\":" + BoolRaw(quickRenameInputActive) + "," +
                   "\"quickRenameDraftLength\":" + IntRaw(quickRenameInputActive ? LegacyTextInput.DraftLength : 0) + "," +
                   "\"currentPlayerNameAvailable\":" + BoolRaw(currentPlayerNameAvailable) + "," +
                   "\"currentPlayerName\":\"" + EscapeJson(currentPlayerNameAvailable ? currentPlayerName : string.Empty) + "\"," +
                   "\"currentPlayerNameMessage\":\"" + EscapeJson(currentPlayerNameAvailable ? string.Empty : currentPlayerNameMessage) + "\"," +
                   "\"presetListOpen\":" + BoolRaw(FishingFilterUiState.PresetListOpen) + "," +
                   "\"visiblePresetCount\":" + IntRaw(visiblePresetCount) + "," +
                   "\"pickerSource\":\"" + EscapeJson(FishingFilterUiState.PickerSource) + "\"," +
                   "\"pickerOpen\":" + BoolRaw(FishingFilterUiState.PickerOpen) + "," +
                   "\"pickerCandidateCount\":" + IntRaw(FishingFilterUiState.PickerCandidateCount) + "," +
                   "\"pickerSelectedCount\":" + IntRaw(FishingFilterUiState.PickerSelectedCount) +
                   "}";
        }

        private static int ResolveFishingFilterActiveListCount(
            string filterMode,
            string matchMode,
            int allowExactCount,
            int denyExactCount,
            int allowKeywordCount,
            int denyKeywordCount)
        {
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(matchMode, FishingFilterMatchModes.Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                    ? denyKeywordCount
                    : allowKeywordCount;
            }

            return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? denyExactCount
                : allowExactCount;
        }

        private static int ResolveFishingFilterActiveKeywordCount(string filterMode, int allowKeywordCount, int denyKeywordCount)
        {
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? denyKeywordCount
                : allowKeywordCount;
        }

        private static int ResolveFishingFilterActiveExactCount(string filterMode, int allowExactCount, int denyExactCount)
        {
            if (string.Equals(filterMode, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            return string.Equals(filterMode, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? denyExactCount
                : allowExactCount;
        }

        private static string BuildInformationUiStateJson()
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return "{" +
                   "\"informationEnemyNameLabelsEnabled\":" + BoolRaw(settings.InformationEnemyNameLabelsEnabled) + "," +
                   "\"informationCritterNameLabelsEnabled\":" + BoolRaw(settings.InformationCritterNameLabelsEnabled) + "," +
                   "\"informationNpcNameLabelsMode\":\"" + EscapeJson(NormalizeInformationNpcNameLabelsMode(settings.InformationNpcNameLabelsMode)) + "\"," +
                   "\"informationChestNameLabelsEnabled\":" + BoolRaw(settings.InformationChestNameLabelsEnabled) + "," +
                   "\"informationChestNameLabelsMode\":\"" + EscapeJson(NormalizeInformationChestNameLabelsMode(settings.InformationChestNameLabelsMode)) + "\"," +
                   "\"informationSignTextLabelsMode\":\"" + EscapeJson(NormalizeInformationSignTextLabelsMode(settings.InformationSignTextLabelsMode)) + "\"," +
                   "\"informationSignTextMaxLines\":" + IntRaw(InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines)) + "," +
                   "\"informationSignTextMaxCharacters\":" + IntRaw(InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters)) + "," +
                   "\"informationTombstoneTextLabelsMode\":\"" + EscapeJson(NormalizeInformationSignTextLabelsMode(settings.InformationTombstoneTextLabelsMode)) + "\"," +
                   "\"informationTombstoneTextMaxLines\":" + IntRaw(InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines)) + "," +
                   "\"informationTombstoneTextMaxCharacters\":" + IntRaw(InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters)) + "," +
                   "\"informationHighlightLifeCrystalEnabled\":" + BoolRaw(settings.InformationHighlightLifeCrystalEnabled) + "," +
                   "\"informationHighlightManaCrystalEnabled\":" + BoolRaw(settings.InformationHighlightManaCrystalEnabled) + "," +
                   "\"informationHighlightDigtoiseEnabled\":" + BoolRaw(settings.InformationHighlightDigtoiseEnabled) + "," +
                   "\"informationHighlightLifeFruitEnabled\":" + BoolRaw(settings.InformationHighlightLifeFruitEnabled) + "," +
                   "\"informationHighlightDragonEggEnabled\":" + BoolRaw(settings.InformationHighlightDragonEggEnabled) + "," +
                   "\"informationBiomeDisplayEnabled\":" + BoolRaw(settings.InformationBiomeDisplayEnabled) + "," +
                   "\"informationWorldInfectionEnabled\":" + BoolRaw(settings.InformationWorldInfectionEnabled) + "," +
                   "\"informationLuckValueEnabled\":" + BoolRaw(settings.InformationLuckValueEnabled) + "," +
                   "\"informationFishingCatchesEnabled\":" + BoolRaw(settings.InformationFishingCatchesEnabled) + "," +
                   "\"informationFishingFilteredCatchesEnabled\":" + BoolRaw(settings.InformationFishingFilteredCatchesEnabled) + "," +
                   "\"informationAnglerQuestEnabled\":" + BoolRaw(settings.InformationAnglerQuestEnabled) + "," +
                   "\"informationPanelX\":" + IntRaw(settings.InformationPanelX) + "," +
                   "\"informationPanelY\":" + IntRaw(settings.InformationPanelY) + "," +
                   "\"informationPanelPositionInitialized\":" + BoolRaw(settings.InformationPanelPositionInitialized) +
                   "}";
        }

        private static string BuildInformationStyleStateJson(string featureId)
        {
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            if (!InformationStyleHelper.IsConfigurable(featureId))
            {
                return "{\"featureId\":\"" + EscapeJson(featureId) + "\",\"configurable\":false}";
            }

            var color = InformationStyleHelper.GetColor(settings, featureId);
            int hue;
            int saturation;
            int lightness;
            InformationStyleHelper.ColorToHsl(color, out hue, out saturation, out lightness);
            return "{" +
                   "\"featureId\":\"" + EscapeJson(featureId) + "\"," +
                   "\"displayName\":\"" + EscapeJson(InformationStyleHelper.GetDisplayName(featureId)) + "\"," +
                   "\"configurable\":true," +
                   "\"color\":\"" + EscapeJson(InformationStyleHelper.GetColorHex(settings, featureId)) + "\"," +
                   "\"fontScale\":" + InformationStyleHelper.GetFontScale(settings, featureId).ToString("0.00", CultureInfo.InvariantCulture) + "," +
                   "\"hue\":" + IntRaw(hue) + "," +
                   "\"saturation\":" + IntRaw(saturation) + "," +
                   "\"lightness\":" + IntRaw(lightness) +
                   "}";
        }

        private static string BuildAutoRecoveryStateJson(AutoRecoveryState state)
        {
            if (state == null)
            {
                return "{}";
            }

            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            return "{" +
                   "\"autoHealEnabled\":" + BoolRaw(state.AutoHealEnabled) + "," +
                   "\"autoManaEnabled\":" + BoolRaw(state.AutoManaEnabled) + "," +
                   "\"autoBuffEnabled\":" + BoolRaw(state.AutoBuffEnabled) + "," +
                   "\"autoNurseEnabled\":" + BoolRaw(state.AutoNurseEnabled) + "," +
                   "\"autoStationBuffEnabled\":" + BoolRaw(state.AutoStationBuffEnabled) + "," +
                   "\"autoHealMode\":\"" + EscapeJson(state.AutoHealMode) + "\"," +
                   "\"autoManaMode\":\"" + EscapeJson(state.AutoManaMode) + "\"," +
                   "\"autoHealThresholdPercent\":" + IntRaw(state.AutoHealThresholdPercent) + "," +
                   "\"autoManaThresholdPercent\":" + IntRaw(state.AutoManaThresholdPercent) + "," +
                   "\"autoBuffCooldownTicks\":" + IntRaw(state.AutoBuffCooldownTicks) + "," +
                   "\"autoHealBlockedItemTypeCount\":" + IntRaw(AutoRecoveryItemFilter.CountBlockedHealItems(settings)) + "," +
                   "\"autoManaBlockedItemTypeCount\":" + IntRaw(AutoRecoveryItemFilter.CountBlockedManaItems(settings)) + "," +
                   "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) +
                   "}";
        }

        private static string BuildBuffBeforeJson(BuffPotionCandidate candidate)
        {
            return "{" +
                   "\"candidateCount\":" + IntRaw(LegacyMainUiState.CandidateCount) + "," +
                   "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) + "," +
                   "\"selectedItemType\":" + IntRaw(candidate == null ? 0 : candidate.ItemType) + "," +
                   "\"buffType\":" + IntRaw(candidate == null ? 0 : candidate.BuffType) +
                   "}";
        }

        private static string BuildScanAfterJson(BuffPotionScanResult scan)
        {
            if (scan == null)
            {
                return "{\"candidateCount\":0,\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) + "}";
            }

            return "{" +
                   "\"candidateCount\":" + IntRaw(scan.Candidates.Count) + "," +
                   "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) + "," +
                   "\"playerAvailable\":" + BoolRaw(scan.PlayerAvailable) + "," +
                   "\"voidBagScanned\":" + BoolRaw(scan.VoidBagScanned) + "," +
                   "\"networkMode\":\"" + EscapeJson(scan.NetworkMode) + "\"," +
                   "\"message\":\"" + EscapeJson(scan.Message) + "\"," +
                   "\"error\":\"" + EscapeJson(scan.Error) + "\"" +
                   "}";
        }

        private static string BuildWhitelistAfterJson(string operation, BuffPotionCandidate candidate)
        {
            return "{" +
                   "\"operation\":\"" + EscapeJson(operation) + "\"," +
                   "\"candidateCount\":" + IntRaw(LegacyMainUiState.CandidateCount) + "," +
                   "\"whitelistCount\":" + IntRaw(BuffPotionWhitelistService.Count) + "," +
                   "\"selectedItemType\":" + IntRaw(candidate == null ? 0 : candidate.ItemType) + "," +
                   "\"buffType\":" + IntRaw(candidate == null ? 0 : candidate.BuffType) + "," +
                   "\"itemName\":\"" + EscapeJson(candidate == null ? string.Empty : candidate.ItemName) + "\"," +
                   "\"buffName\":\"" + EscapeJson(candidate == null ? string.Empty : candidate.BuffName) + "\"" +
                   "}";
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static bool IsOnMode(string value)
        {
            return string.Equals(value, "On", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "Enabled", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeInformationNpcNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase))
            {
                return "Name";
            }

            if (string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return "Type";
            }

            return "Off";
        }

        private static string NormalizeInformationChestNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                return "Always";
            }

            if (string.Equals(mode, "Opened", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Known", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "Open", StringComparison.OrdinalIgnoreCase))
            {
                return "Opened";
            }

            return "Off";
        }

        private static string NormalizeInformationSignTextLabelsMode(string mode)
        {
            return InformationSignTextModes.Normalize(mode);
        }

        private static string SignTextModeMessage(string mode)
        {
            if (string.Equals(mode, InformationSignTextModes.All, StringComparison.OrdinalIgnoreCase))
            {
                return "牌子显示已切换为全部显示。";
            }

            if (string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                return "牌子显示已切换为显示前几行。";
            }

            if (string.Equals(mode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                return "牌子显示已切换为显示前几个字。";
            }

            return "牌子显示已关闭。";
        }

        private static string TombstoneTextModeMessage(string mode)
        {
            if (string.Equals(mode, InformationSignTextModes.All, StringComparison.OrdinalIgnoreCase))
            {
                return "墓碑显示已切换为全部显示。";
            }

            if (string.Equals(mode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                return "墓碑显示已切换为显示前几行。";
            }

            if (string.Equals(mode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                return "墓碑显示已切换为显示前几个字。";
            }

            return "墓碑显示已关闭。";
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
