using System;
using JueMingZ.Actions;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void Dispatch(LegacyUiCommand command, InputActionQueue queue, GameStateSnapshot snapshot)
        {
            if (command == null)
            {
                return;
            }

            // Element id order is the legacy command protocol; keep prefix routes
            // before broad switch fallbacks unless a test locks the new ordering.
            // Domain handlers may update settings, clear service state, or submit
            // queue requests; game-state mutations still belong to Actions/Compat.
            if (string.Equals(command.Kind, "tab", StringComparison.OrdinalIgnoreCase))
            {
                var pageId = command.ElementId.StartsWith("tab:", StringComparison.Ordinal) ? command.ElementId.Substring(4) : "buff";
                var before = LegacyMainUiState.BuildUiStateJson();
                LegacyTextInput.ClearFocus();
                LegacyMainUiState.SelectPage(pageId);
                Record(
                    command,
                    "Ui.Page.Select",
                    "UI",
                    "Succeeded",
                    "Selected page " + pageId + ".",
                    before,
                    LegacyMainUiState.BuildUiStateJson(),
                    "{\"pageId\":\"" + EscapeJson(pageId) + "\"}",
                    "Button");
                return;
            }

            if (command.ElementId.StartsWith("auto-heal-mode:", StringComparison.Ordinal))
            {
                SetAutoHealMode(command, command.ElementId.Substring("auto-heal-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("auto-mana-mode:", StringComparison.Ordinal))
            {
                SetAutoManaMode(command, command.ElementId.Substring("auto-mana-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("auto-recovery-item-config:", StringComparison.Ordinal))
            {
                ToggleAutoRecoveryItemConfig(command, command.ElementId.Substring("auto-recovery-item-config:".Length));
                return;
            }

            if (command.ElementId.StartsWith("auto-recovery-item-option:", StringComparison.Ordinal))
            {
                ToggleAutoRecoveryItemOption(command, command.ElementId.Substring("auto-recovery-item-option:".Length));
                return;
            }

            if (command.ElementId.StartsWith("auto-buff-mode:", StringComparison.Ordinal))
            {
                SetAutoBuffEnabled(command, IsOnMode(command.ElementId.Substring("auto-buff-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("auto-nurse-mode:", StringComparison.Ordinal))
            {
                SetAutoNurseEnabled(command, IsOnMode(command.ElementId.Substring("auto-nurse-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("auto-station-buff-mode:", StringComparison.Ordinal))
            {
                SetAutoStationBuffEnabled(command, IsOnMode(command.ElementId.Substring("auto-station-buff-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-auto-clicker-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "autoClicker", IsOnMode(command.ElementId.Substring("combat-auto-clicker-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-flail-combo-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "flailCombo", IsOnMode(command.ElementId.Substring("combat-flail-combo-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-phaseblade-quick-switch-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "phasebladeQuickSwitch", IsOnMode(command.ElementId.Substring("combat-phaseblade-quick-switch-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-perfect-revolver-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "perfectRevolver", IsOnMode(command.ElementId.Substring("combat-perfect-revolver-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-magic-string-clicker-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "magicStringClicker", IsOnMode(command.ElementId.Substring("combat-magic-string-clicker-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-auto-facing-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "autoFacing", IsOnMode(command.ElementId.Substring("combat-auto-facing-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-equipment-warning-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "equipmentWarning", IsOnMode(command.ElementId.Substring("combat-equipment-warning-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-auto-boss-damage-report-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "autoBossDamageReport", IsOnMode(command.ElementId.Substring("combat-auto-boss-damage-report-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("combat-goblin-execution-mode:", StringComparison.Ordinal))
            {
                SetCombatFeatureEnabled(command, "goblinExecution", IsOnMode(command.ElementId.Substring("combat-goblin-execution-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("fishing-toggle:", StringComparison.Ordinal))
            {
                SetFishingFeatureEnabled(command, command.ElementId.Substring("fishing-toggle:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-store-mode:", StringComparison.Ordinal))
            {
                SetFishingAutoStoreMode(command, command.ElementId.Substring("fishing-store-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-cut-rod-skip-mode:", StringComparison.Ordinal))
            {
                SetFishingCutRodSkipEnabled(command, IsOnMode(command.ElementId.Substring("fishing-cut-rod-skip-mode:".Length)));
                return;
            }

            if (command.ElementId.StartsWith("fishing-quick-rename:", StringComparison.Ordinal))
            {
                HandleFishingQuickRename(command, queue);
                return;
            }

            if (command.ElementId.StartsWith("movement-toggle:", StringComparison.Ordinal))
            {
                SetMovementFeatureEnabled(command, command.ElementId.Substring("movement-toggle:".Length));
                return;
            }

            if (command.ElementId.StartsWith("movement-safe-landing-mode:", StringComparison.Ordinal))
            {
                SetMovementSafeLandingMode(command, command.ElementId.Substring("movement-safe-landing-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("movement-safe-landing-option:", StringComparison.Ordinal))
            {
                ToggleMovementSafeLandingOption(command, command.ElementId.Substring("movement-safe-landing-option:".Length));
                return;
            }

            if (command.ElementId.StartsWith("movement-continuous-dash-mode:", StringComparison.Ordinal))
            {
                SetMovementContinuousDashMode(command, command.ElementId.Substring("movement-continuous-dash-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-mode:", StringComparison.Ordinal))
            {
                SetFishingFilterMode(command, command.ElementId.Substring("fishing-filter-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-rule:", StringComparison.Ordinal))
            {
                SetFishingFilterSpecialRule(command, command.ElementId.Substring("fishing-filter-rule:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-match-mode:", StringComparison.Ordinal))
            {
                SetFishingFilterMatchMode(command, command.ElementId.Substring("fishing-filter-match-mode:".Length));
                return;
            }

            if (string.Equals(command.ElementId, "fishing-filter-list:clear", StringComparison.Ordinal))
            {
                ClearFishingFilterActiveList(command);
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-exact-picker:", StringComparison.Ordinal))
            {
                HandleFishingFilterExactPicker(command, command.ElementId.Substring("fishing-filter-exact-picker:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-exact-entry:delete:", StringComparison.Ordinal))
            {
                DeleteFishingFilterExactEntry(command, command.ElementId.Substring("fishing-filter-exact-entry:delete:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-keyword:", StringComparison.Ordinal))
            {
                HandleFishingFilterKeyword(command, command.ElementId.Substring("fishing-filter-keyword:".Length));
                return;
            }

            if (command.ElementId.StartsWith("fishing-filter-preset:", StringComparison.Ordinal))
            {
                HandleFishingFilterPreset(command, command.ElementId.Substring("fishing-filter-preset:".Length));
                return;
            }

            if (command.ElementId.StartsWith("search-chest-locator:", StringComparison.Ordinal))
            {
                HandleSearchChestLocatorCommand(command);
                return;
            }

            if (command.ElementId.StartsWith("search-query:", StringComparison.Ordinal))
            {
                HandleSearchQueryCommand(command);
                return;
            }

            if (command.ElementId.StartsWith("information-toggle:", StringComparison.Ordinal))
            {
                SetInformationFeatureEnabled(command, command.ElementId.Substring("information-toggle:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-npc-name-label-mode:", StringComparison.Ordinal))
            {
                SetInformationNpcNameLabelsMode(command, command.ElementId.Substring("information-npc-name-label-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-chest-name-label-mode:", StringComparison.Ordinal))
            {
                SetInformationChestNameLabelsMode(command, command.ElementId.Substring("information-chest-name-label-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-sign-text-label-mode:", StringComparison.Ordinal))
            {
                SetInformationSignTextLabelsMode(command, command.ElementId.Substring("information-sign-text-label-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-tombstone-text-label-mode:", StringComparison.Ordinal))
            {
                SetInformationTombstoneTextLabelsMode(command, command.ElementId.Substring("information-tombstone-text-label-mode:".Length));
                return;
            }

            if (string.Equals(command.ElementId, "information-sign-text-limit-decrease", StringComparison.Ordinal))
            {
                AdjustInformationSignTextLimit(command, -1);
                return;
            }

            if (string.Equals(command.ElementId, "information-sign-text-limit-increase", StringComparison.Ordinal))
            {
                AdjustInformationSignTextLimit(command, 1);
                return;
            }

            if (string.Equals(command.ElementId, "information-tombstone-text-limit-decrease", StringComparison.Ordinal))
            {
                AdjustInformationTombstoneTextLimit(command, -1);
                return;
            }

            if (string.Equals(command.ElementId, "information-tombstone-text-limit-increase", StringComparison.Ordinal))
            {
                AdjustInformationTombstoneTextLimit(command, 1);
                return;
            }

            if (command.ElementId.StartsWith("information-style-config:", StringComparison.Ordinal))
            {
                ToggleInformationStyleConfig(command, command.ElementId.Substring("information-style-config:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-style-h:", StringComparison.Ordinal))
            {
                SetInformationStyleHsl(command, command.ElementId.Substring("information-style-h:".Length), "h");
                return;
            }

            if (command.ElementId.StartsWith("information-style-s:", StringComparison.Ordinal))
            {
                SetInformationStyleHsl(command, command.ElementId.Substring("information-style-s:".Length), "s");
                return;
            }

            if (command.ElementId.StartsWith("information-style-l:", StringComparison.Ordinal))
            {
                SetInformationStyleHsl(command, command.ElementId.Substring("information-style-l:".Length), "l");
                return;
            }

            if (command.ElementId.StartsWith("information-style-html:", StringComparison.Ordinal))
            {
                FocusInformationStyleColorInput(command, command.ElementId.Substring("information-style-html:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-style-reset:", StringComparison.Ordinal))
            {
                ResetInformationStyle(command, command.ElementId.Substring("information-style-reset:".Length));
                return;
            }

            if (command.ElementId.StartsWith("information-style-font-decrease:", StringComparison.Ordinal))
            {
                AdjustInformationStyleFont(command, command.ElementId.Substring("information-style-font-decrease:".Length), -1);
                return;
            }

            if (command.ElementId.StartsWith("information-style-font-increase:", StringComparison.Ordinal))
            {
                AdjustInformationStyleFont(command, command.ElementId.Substring("information-style-font-increase:".Length), 1);
                return;
            }

            if (command.ElementId.StartsWith("map-quick-announcement-mode:", StringComparison.Ordinal))
            {
                HandleMapQuickAnnouncementMode(command, command.ElementId.Substring("map-quick-announcement-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-death-history:", StringComparison.Ordinal))
            {
                HandleMapDeathHistoryCommand(command, command.ElementId.Substring("map-death-history:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-revealed-area-ratio:", StringComparison.Ordinal))
            {
                HandleMapRevealedAreaRatioCommand(command, command.ElementId.Substring("map-revealed-area-ratio:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-persistent-death-markers-mode:", StringComparison.Ordinal))
            {
                HandleMapPersistentDeathMarkersMode(command, command.ElementId.Substring("map-persistent-death-markers-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-custom-markers-mode:", StringComparison.Ordinal))
            {
                HandleMapCustomMarkersMode(command, command.ElementId.Substring("map-custom-markers-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-custom-markers-page:", StringComparison.Ordinal))
            {
                HandleMapCustomMarkersPage(command, command.ElementId.Substring("map-custom-markers-page:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-custom-marker:", StringComparison.Ordinal))
            {
                HandleMapCustomMarkerCommand(command, command.ElementId.Substring("map-custom-marker:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-footprints-display-mode:", StringComparison.Ordinal))
            {
                HandleMapFootprintsDisplayMode(command, command.ElementId.Substring("map-footprints-display-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("map-quick-announcement-key:", StringComparison.Ordinal))
            {
                HandleMapQuickAnnouncementKeySlot(command, command.ElementId.Substring("map-quick-announcement-key:".Length));
                return;
            }

            if (string.Equals(command.ElementId, "about-copy-feedback-group", StringComparison.Ordinal))
            {
                HandleAboutCopyFeedbackGroup(command);
                return;
            }

            if (command.ElementId.StartsWith("misc-developer-easter-egg:", StringComparison.Ordinal))
            {
                HandleMiscDeveloperEasterEgg(command, command.ElementId.Substring("misc-developer-easter-egg:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-debug-commands-mode:", StringComparison.Ordinal))
            {
                HandleMiscDebugCommandsMode(command, command.ElementId.Substring("misc-debug-commands-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-item-hotkeys-row:", StringComparison.Ordinal))
            {
                HandleMiscQuickItemHotkeysRow(command, command.ElementId.Substring("misc-quick-item-hotkeys-row:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-stack-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoStackMode(command, command.ElementId.Substring("misc-auto-stack-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-tax-collect-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoTaxCollectMode(command, command.ElementId.Substring("misc-auto-tax-collect-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-bag-open-mode:", StringComparison.Ordinal))
            {
                HandleMiscQuickBagOpenMode(command, command.ElementId.Substring("misc-quick-bag-open-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-deposit-coins-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoDepositCoinsMode(command, command.ElementId.Substring("misc-auto-deposit-coins-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-extractinator-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoExtractinatorMode(command, command.ElementId.Substring("misc-auto-extractinator-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-keep-favorited-mode:", StringComparison.Ordinal))
            {
                HandleMiscKeepFavoritedMode(command, command.ElementId.Substring("misc-keep-favorited-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-mining:", StringComparison.Ordinal))
            {
                HandleMiscAutoMiningCommand(command, command.ElementId.Substring("misc-auto-mining:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-mining-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoMiningMode(command, command.ElementId.Substring("misc-auto-mining-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-capture-critter-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoCaptureCritterMode(command, command.ElementId.Substring("misc-auto-capture-critter-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-capture-critter-config:", StringComparison.Ordinal))
            {
                ToggleMiscAutoCaptureCritterConfig(command, command.ElementId.Substring("misc-auto-capture-critter-config:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-capture-critter-option:", StringComparison.Ordinal))
            {
                ToggleMiscAutoCaptureCritterOption(command, command.ElementId.Substring("misc-auto-capture-critter-option:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-harvest-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoHarvestMode(command, command.ElementId.Substring("misc-auto-harvest-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-sell-row:", StringComparison.Ordinal))
            {
                HandleMiscAutoSellRow(command, command.ElementId.Substring("misc-auto-sell-row:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-sell:", StringComparison.Ordinal))
            {
                HandleMiscAutoSellCommand(command, command.ElementId.Substring("misc-auto-sell:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-sell-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoSellMode(command, command.ElementId.Substring("misc-auto-sell-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-discard-row:", StringComparison.Ordinal))
            {
                HandleMiscAutoDiscardRow(command, command.ElementId.Substring("misc-auto-discard-row:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-discard:", StringComparison.Ordinal))
            {
                HandleMiscAutoDiscardCommand(command, command.ElementId.Substring("misc-auto-discard:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-auto-discard-mode:", StringComparison.Ordinal))
            {
                HandleMiscAutoDiscardMode(command, command.ElementId.Substring("misc-auto-discard-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-reforge:", StringComparison.Ordinal))
            {
                HandleMiscQuickReforgeCommand(command, command.ElementId.Substring("misc-quick-reforge:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-reforge-mode:", StringComparison.Ordinal))
            {
                HandleMiscQuickReforgeMode(command, command.ElementId.Substring("misc-quick-reforge-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-world-generation-details:", StringComparison.Ordinal))
            {
                HandleMiscWorldGenerationDetails(command, command.ElementId.Substring("misc-world-generation-details:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-item-hotkeys:", StringComparison.Ordinal))
            {
                HandleMiscQuickItemHotkeysCommand(command, command.ElementId.Substring("misc-quick-item-hotkeys:".Length), snapshot);
                return;
            }

            if (command.ElementId.StartsWith("misc-quick-item-hotkeys-mode:", StringComparison.Ordinal))
            {
                HandleMiscQuickItemHotkeysMode(command, command.ElementId.Substring("misc-quick-item-hotkeys-mode:".Length));
                return;
            }

            if (command.ElementId.StartsWith("misc-travel-menu-mode:", StringComparison.Ordinal))
            {
                HandleMiscTravelMenu(command, command.ElementId.Substring("misc-travel-menu-mode:".Length));
                return;
            }

            switch (command.ElementId)
            {
                case "auto-heal-toggle":
                    ToggleAutoHeal(command);
                    break;
                case "auto-mana-toggle":
                    ToggleAutoMana(command);
                    break;
                case "auto-buff-toggle":
                    ToggleAutoBuff(command);
                    break;
                case "auto-heal-threshold":
                    SetThreshold(command, true);
                    break;
                case "auto-mana-threshold":
                    SetThreshold(command, false);
                    break;
                case "auto-buff-cooldown":
                    SetAutoBuffCooldown(command);
                    break;
                case "buff-refresh-candidates":
                    RefreshCandidates(command);
                    break;
                case "buff-clear-whitelist":
                    ClearWhitelist(command);
                    break;
                case "buff-follow-add-toggle":
                    ToggleAutoBuffFollowOption(command, true);
                    break;
                case "buff-follow-remove-toggle":
                    ToggleAutoBuffFollowOption(command, false);
                    break;
                case "combat-aim-radius":
                    SetCombatAimRadius(command);
                    break;
                case "combat-phaseblade-quick-switch-interval":
                    SetCombatPhasebladeQuickSwitchInterval(command);
                    break;
                case "combat-aim-priority-toggle":
                    ToggleCombatAimPriority(command);
                    break;
                case "combat-aim-origin-toggle":
                    ToggleCombatAimOrigin(command);
                    break;
                case "combat-aim-track-dummy-toggle":
                    ToggleCombatAimOption(command, true);
                    break;
                case "combat-aim-marker-toggle":
                    ToggleCombatAimOption(command, false);
                    break;
                case "information-info-panel-position-start":
                    StartInformationPanelPosition(command);
                    break;
                default:
                    if (string.Equals(command.Kind, "candidate", StringComparison.OrdinalIgnoreCase))
                    {
                        AddCandidate(command);
                    }
                    else if (string.Equals(command.Kind, "whitelist", StringComparison.OrdinalIgnoreCase))
                    {
                        RemoveWhitelistEntry(command);
                    }

                    break;
            }
        }
    }
}
