using System;
using System.Collections.Generic;
using JueMingZ.Actions;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.Features;
using JueMingZ.Input;
using JueMingZ.Runtime;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;
using JueMingZ.UI.Legacy.Framework;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private const int BlueprintVkAlt = 0x12;
        private const int BlueprintVkB = 0x42;

        private static void FeatureCatalogExposesBlueprintEntryAsPlannedPlaceholder()
        {
            var registry = FeatureRegistry.CreateDefault();
            FeatureDefinition feature;
            if (!registry.TryGet(FeatureIds.BlueprintMain, out feature) || feature == null)
            {
                throw new InvalidOperationException("Expected blueprint main feature to be registered.");
            }

            if (feature.IsImplemented || feature.VisibleInMainUi || feature.HasHotkey || feature.HotkeyListVisible)
            {
                throw new InvalidOperationException("Blueprint main must remain a planned placeholder after the entry stage.");
            }

            if (feature.LifecycleStatus != FeatureLifecycleStatus.Planned ||
                feature.ConfigUiKind != FeatureConfigUiKind.Placeholder)
            {
                throw new InvalidOperationException("Blueprint main must expose planned lifecycle and placeholder config metadata.");
            }

            if (feature.CodeDomain != FeatureCodeDomain.Blueprint ||
                feature.UserCategory != FeatureUserCategory.Blueprint)
            {
                throw new InvalidOperationException("Blueprint main must stay in Blueprint code-domain and Blueprint UI category.");
            }

            if (feature.RequiredActions.Count != 1 || feature.RequiredActions[0] != InputActionKind.BlueprintAutoPlace)
            {
                throw new InvalidOperationException("Blueprint main must expose only the contract-level auto placement action while remaining planned.");
            }

            AssertContains(feature.Notes, "ActionQueue 契约");
        }

        private static void BlueprintEntrySettingsDefaultAndNormalize()
        {
            var settings = AppSettings.CreateDefault();
            if (settings.BlueprintToolItemId != BlueprintSettings.DefaultToolItemId ||
                settings.BlueprintHandheldEntryEnabled ||
                settings.BlueprintAutoPlacementEnabled ||
                settings.BlueprintReplacementEnabled ||
                settings.BlueprintReplacementTorchesEnabled ||
                settings.BlueprintReplacementPlatformsEnabled ||
                settings.BlueprintReplacementWorkBenchesEnabled ||
                settings.BlueprintReplacementChairsEnabled ||
                settings.BlueprintReplacementDoorsEnabled ||
                settings.BlueprintReplacementTablesEnabled ||
                settings.BlueprintReplacementChestsEnabled ||
                settings.BlueprintReplacementSignsEnabled)
            {
                throw new InvalidOperationException("Expected blueprint entry settings to default to tool item 23 and disabled toggles/categories.");
            }

            if (BlueprintSettings.NormalizeToolItemId(0) != BlueprintSettings.DefaultToolItemId ||
                BlueprintSettings.NormalizeToolItemId(BlueprintSettings.MaxToolItemId + 1) != BlueprintSettings.DefaultToolItemId)
            {
                throw new InvalidOperationException("Expected invalid blueprint tool item ids to normalize to default.");
            }

            if (BlueprintSettings.AdjustToolItemId(BlueprintSettings.MinToolItemId, -10) != BlueprintSettings.MinToolItemId ||
                BlueprintSettings.AdjustToolItemId(BlueprintSettings.MaxToolItemId, 10) != BlueprintSettings.MaxToolItemId)
            {
                throw new InvalidOperationException("Expected blueprint tool item adjustments to clamp within bounds.");
            }
        }

        private static void RuntimeSettingsSnapshotCarriesBlueprintEntrySettings()
        {
            var settings = AppSettings.CreateDefault();
            settings.BlueprintToolItemId = 0;
            settings.BlueprintHandheldEntryEnabled = true;
            settings.BlueprintAutoPlacementEnabled = true;
            settings.BlueprintReplacementEnabled = true;
            settings.BlueprintReplacementTorchesEnabled = true;
            settings.BlueprintReplacementPlatformsEnabled = true;
            settings.BlueprintReplacementWorkBenchesEnabled = true;
            settings.BlueprintReplacementChairsEnabled = true;
            settings.BlueprintReplacementDoorsEnabled = true;
            settings.BlueprintReplacementTablesEnabled = true;
            settings.BlueprintReplacementChestsEnabled = true;
            settings.BlueprintReplacementSignsEnabled = true;

            var snapshot = RuntimeSettingsSnapshot.FromSettings(settings);
            if (snapshot.BlueprintToolItemId != BlueprintSettings.DefaultToolItemId ||
                !snapshot.BlueprintHandheldEntryEnabled ||
                !snapshot.BlueprintAutoPlacementEnabled ||
                !snapshot.BlueprintReplacementEnabled ||
                !snapshot.BlueprintReplacementTorchesEnabled ||
                !snapshot.BlueprintReplacementPlatformsEnabled ||
                !snapshot.BlueprintReplacementWorkBenchesEnabled ||
                !snapshot.BlueprintReplacementChairsEnabled ||
                !snapshot.BlueprintReplacementDoorsEnabled ||
                !snapshot.BlueprintReplacementTablesEnabled ||
                !snapshot.BlueprintReplacementChestsEnabled ||
                !snapshot.BlueprintReplacementSignsEnabled)
            {
                throw new InvalidOperationException("Expected runtime snapshot to normalize and carry blueprint entry settings.");
            }
        }

        private static void BlueprintEntryStateOpensLibraryWithoutGameplayAction()
        {
            BlueprintEntryState.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            var snapshot = BlueprintEntryState.GetSnapshot(settings);
            AssertStringEquals(snapshot.Mode, BlueprintEntryModes.Tool, "initial blueprint entry mode");

            var create = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.StartCreate, settings);
            if (!create.Succeeded ||
                create.PlaceholderOnly ||
                !string.Equals(create.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint start-create to enter the implemented 05 mask selection state.");
            }

            var library = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenLibrary, settings);
            if (!library.Succeeded ||
                library.PlaceholderOnly ||
                !string.Equals(library.ResultCode, "libraryOpened", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint library command to open the 04 UI without gameplay action.");
            }
        }

        private static void BlueprintDirectEntryHotkeyIsDisabled()
        {
            BlueprintEntryHotkeyService.ResetForTesting();
            BlueprintEntryState.ResetForTesting();
            var settings = AppSettings.CreateDefault();
            var hotkeys = HotkeySettings.CreateDefault();
            hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintMain] = "Alt+B";

            var result = BlueprintEntryHotkeyService.TickForTesting(
                settings,
                hotkeys,
                BlueprintEntryDownKeys(BlueprintVkAlt, BlueprintVkB),
                true,
                string.Empty,
                false);
            if (result.Triggered ||
                result.Applied ||
                !string.Equals(result.Reason, "directEntryHotkeyDisabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint direct entry hotkey service to stay disabled.");
            }

            var applied = BlueprintEntryState.ApplyCommand(BlueprintEntryCommands.OpenEntryHotkey, settings);
            if (applied.Succeeded ||
                !string.Equals(applied.ResultCode, "directEntryHotkeyDisabled", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint direct entry command to reject old hotkey opens.");
            }
        }

        private static void BlueprintDirectEntryHotkeyIsNotAConflictSource()
        {
            var hotkeys = HotkeySettings.CreateDefault();
            hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintMain] = "Alt+B";
            FeatureToggleHotkeyConflict conflict;
            if (FeatureToggleHotkeyConflictRegistry.TryFindConflict(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.InventoryAutoStack,
                    "Alt+B",
                    out conflict))
            {
                throw new InvalidOperationException("Disabled blueprint direct entry hotkeys must not block other hotkey bindings.");
            }

            string message;
            bool changed;
            if (LegacyMainWindow.TryApplyBlueprintEntryHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    "Alt+B",
                    out message,
                    out changed) ||
                changed ||
                !message.Contains("已停用"))
            {
                throw new InvalidOperationException("Expected blueprint direct entry hotkey save to be rejected as disabled.");
            }
        }

        private static void BlueprintEntryUiContractsExposeBlockerAndCapture()
        {
            if (LegacyMainWindow.CalculateBlueprintContentHeightForTesting() <= 0)
            {
                throw new InvalidOperationException("Expected blueprint page to have a stable content height.");
            }

            AssertStringEquals(
                LegacyMainWindow.GetBlueprintLibraryOpenElementIdForTesting(),
                "blueprint-action-entry:open-library",
                "blueprint library action open row command id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintPlacedOpenElementIdForTesting(),
                "blueprint-entry:open-placed",
                "placed blueprint open row command id");
            AssertStringEquals(
                LegacyMainWindow.BuildBlueprintLibraryNameInputIdForTesting("template-ime"),
                "blueprint-library:name-input:template-ime",
                "blueprint library rename input id");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "main-menu-hotkey-open-row");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "same-content-submenu");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage06-two-column-fixed-cards");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "preview-scales-to-fit");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage07-name-edit-delete-confirm");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage08-import-long-button-export-real");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "stage09-layout-use-real-template-snapshot");
            AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "main-menu-open-row");
            AssertContains(LegacyMainWindow.GetBlueprintCreationVisualContractForTesting(), "drag-toggle");
            AssertContains(LegacyMainWindow.GetBlueprintCreationVisualContractForTesting(), "air-select");
            if (LegacyMainWindow.GetBlueprintLibraryPageSizeForTesting() != 6)
            {
                throw new InvalidOperationException("Expected blueprint library to keep a stable six-template page size.");
            }

            if (LegacyMainWindow.GetBlueprintMenuOpenRowCountForTesting() != 1)
            {
                throw new InvalidOperationException("Expected blueprint menu to keep only the placed-blueprint deferred open row after the library row becomes an action hotkey row.");
            }

            if (LegacyMainWindow.GetBlueprintActionShortcutRowCountForTesting() != 3)
            {
                throw new InvalidOperationException("Expected blueprint menu to expose create/save/library action shortcut rows.");
            }

            AssertStringEquals(
                LegacyMainWindow.GetBlueprintCreateActionHotkeyElementIdForTesting(),
                "blueprint-action-hotkey:" + FeatureIds.BlueprintCreateAction,
                "blueprint create action hotkey field id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintSaveActionHotkeyElementIdForTesting(),
                "blueprint-action-hotkey:" + FeatureIds.BlueprintSaveAction,
                "blueprint save action hotkey field id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintLibraryActionHotkeyElementIdForTesting(),
                "blueprint-action-hotkey:" + FeatureIds.BlueprintLibraryAction,
                "blueprint library action hotkey field id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintCreateActionElementIdForTesting(),
                "blueprint-action-entry:" + BlueprintEntryCommands.StartCreate,
                "blueprint create action button id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintSaveActionElementIdForTesting(),
                "blueprint-action-entry:" + BlueprintEntryCommands.FinishCreateSave,
                "blueprint save action button id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintLibraryActionElementIdForTesting(),
                "blueprint-action-entry:" + BlueprintEntryCommands.OpenLibrary,
                "blueprint library action button id");
            AssertContains(LegacyMainWindow.GetBlueprintActionShortcutVisualContractForTesting(), "auto-mining-hotkey-shape");
            AssertContains(LegacyMainWindow.GetBlueprintActionShortcutVisualContractForTesting(), "real-create-save-library-entry");
            AssertContains(LegacyMainWindow.GetBlueprintActionShortcutVisualContractForTesting(), "short-hotkey-fields");

            if (LegacyMainWindow.GetBlueprintActionHotkeyInputMaxWidthForTesting() >= 160 ||
                LegacyMainWindow.ResolveBlueprintActionHotkeyInputWidthForTesting(400) != LegacyMainWindow.GetBlueprintActionHotkeyInputMaxWidthForTesting() ||
                LegacyMainWindow.ResolveBlueprintActionHotkeyInputWidthForTesting(96) != 96)
            {
                throw new InvalidOperationException("Expected blueprint create/save/library action hotkey fields to share the shortened width cap without breaking narrow rows.");
            }

            var hotkeyTooltips = LegacyMainWindow.GetBlueprintActionHotkeyTooltipLinesForTesting();
            if (hotkeyTooltips == null ||
                hotkeyTooltips.Length != 3 ||
                !string.Equals(hotkeyTooltips[0], "双击录入采集按键。", StringComparison.Ordinal) ||
                !string.Equals(hotkeyTooltips[1], "Esc 取消录入。", StringComparison.Ordinal) ||
                !string.Equals(hotkeyTooltips[2], "Backspace 删除绑定。", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint action hotkey fields to keep the auto-mining trigger capture tooltip shape.");
            }

            var buttonTooltips = LegacyMainWindow.GetBlueprintCreateSaveButtonTooltipsForTesting();
            if (buttonTooltips == null ||
                buttonTooltips.Length != 2 ||
                !string.Equals(buttonTooltips[0], "左键按住滑动选区，可多选", StringComparison.Ordinal) ||
                !string.Equals(buttonTooltips[1], "保存当前选区为蓝图", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint create/save action button tooltips to match the stage-03 contract.");
            }

            LegacyMainWindow.StartAutoMiningHotkeyCapture();
            LegacyMainWindow.StartBlueprintEntryHotkeyCapture();
            if (!LegacyMainWindow.IsAutoMiningHotkeyCaptureActiveForTesting() ||
                LegacyMainWindow.IsBlueprintEntryHotkeyCaptureActiveForTesting() ||
                !LegacyMainWindow.IsAnyHotkeyCaptureActive())
            {
                throw new InvalidOperationException("Disabled blueprint direct entry capture must not steal auto mining capture.");
            }

            LegacyMainWindow.StopAutoMiningHotkeyCapture();
            LegacyMainWindow.StartBlueprintActionHotkeyCapture(FeatureIds.BlueprintCreateAction);
            if (!LegacyMainWindow.IsBlueprintEntryHotkeyCaptureActiveForTesting() ||
                !string.Equals(LegacyMainWindow.GetBlueprintHotkeyCaptureTargetIdForTesting(), FeatureIds.BlueprintCreateAction, StringComparison.Ordinal) ||
                !LegacyMainWindow.IsAnyHotkeyCaptureActive())
            {
                throw new InvalidOperationException("Expected blueprint action hotkey capture to reuse the blueprint capture lane with an action target id.");
            }

            LegacyMainWindow.StopBlueprintEntryHotkeyCapture();
            LegacyMainWindow.StopAutoMiningHotkeyCapture();
        }

        private static void BlueprintLibrarySubmenuAndShortcutRowsOpenSameUiState()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-library-submenu-entry");
            try
            {
                ConfigService.Initialize();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();

                var templateStore = new BlueprintTemplateLibraryStore();
                BlueprintLibraryUiState.SetStoreForTesting(templateStore, true);
                var projectionReader = new FakeBlueprintWorldTileReader();
                var inventoryReader = new FakeBlueprintMaterialInventoryReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    new BlueprintWorldInstanceStore(),
                    BlueprintPlacementWorldContext.Success("pair-library-submenu", "world-library-submenu"),
                    projectionReader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventoryReader, true);

                if (BlueprintLibraryUiState.IsOpen)
                {
                    throw new InvalidOperationException("Expected blueprint library submenu to start closed.");
                }

                LegacyMainUiState.SetVisible(false);
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintLibraryActionElementIdForTesting(),
                    Label = "蓝图:蓝图库:打开",
                    Kind = "button",
                    MouseCaptured = true
                });

                var snapshot = BlueprintLibraryUiState.GetSnapshot();
                if (!snapshot.IsOpen ||
                    !LegacyMainUiState.Visible ||
                    !string.Equals(LegacyMainUiState.SelectedPageId, "blueprint", StringComparison.Ordinal) ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected F5 library row to open the blueprint library submenu without changing gameplay entry mode.");
                }

                var viewport = new LegacyUiRect(12, 34, 456, 278);
                var submenuBounds = LegacyMainWindow.GetBlueprintLibrarySubmenuContentBoundsForTesting(viewport);
                if (submenuBounds.X != viewport.X ||
                    submenuBounds.Y != viewport.Y ||
                    submenuBounds.Width != viewport.Width ||
                    submenuBounds.Height != viewport.Height)
                {
                    throw new InvalidOperationException("Expected blueprint library submenu to use the same F5 content viewport instead of nesting a card-sized page.");
                }

                LegacyUiActionService.HandleBlueprintLibraryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintLibraryBackElementIdForTesting(),
                    Label = "蓝图库:返回",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (BlueprintLibraryUiState.IsOpen)
                {
                    throw new InvalidOperationException("Expected blueprint library back command to return to the blueprint main menu.");
                }

                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(
                    BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdOpenLibrary, "打开蓝图库"));
                if (!BlueprintLibraryUiState.IsOpen ||
                    !LegacyMainUiState.Visible ||
                    !string.Equals(LegacyMainUiState.SelectedPageId, "blueprint", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected handheld open-library to reveal the same blueprint library submenu as the F5 row.");
                }

                BlueprintLibraryUiState.CloseLibrary();
                var hotkeys = HotkeySettings.CreateDefault();
                hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintLibraryAction] = "Alt+L";
                var libraryHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    new Dictionary<int, bool> { [0x12] = true, ['L'] = true },
                    true,
                    string.Empty,
                    false);
                if (!libraryHotkey.Triggered ||
                    !libraryHotkey.Applied ||
                    !string.Equals(libraryHotkey.Action, BlueprintEntryCommands.OpenLibrary, StringComparison.Ordinal) ||
                    !BlueprintLibraryUiState.IsOpen ||
                    !LegacyMainUiState.Visible ||
                    !string.Equals(LegacyMainUiState.SelectedPageId, "blueprint", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint.library action hotkey to open the same blueprint library submenu.");
                }

                if (projectionReader.ReadCount != 0 || inventoryReader.ReadCount != 0)
                {
                    throw new InvalidOperationException("Opening the blueprint library submenu must not refresh projection, material, world instance, or inventory scans.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintPlacedInstanceUiState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                LegacyMainUiState.SetVisible(false);
                ConfigService.ResetSettingsForTesting();
                BlueprintTemplateLibraryStore.ResetTestingHooks();
                restore();
            }
        }

        private static void BlueprintActionHotkeysUseSeparateKeysAndConflictSources()
        {
            var hotkeys = HotkeySettings.CreateDefault();
            string message;
            bool changed;
            if (!LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintCreateAction,
                    "Alt+C",
                    out message,
                    out changed) ||
                !changed ||
                !string.Equals(hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintCreateAction], "Alt+C", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint create action hotkey to save under its own action key.");
            }

            if (hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintMain) ||
                hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.WorldAutomationAutoMining))
            {
                throw new InvalidOperationException("Blueprint action hotkey save must not overwrite blueprint.main or auto-mining trigger hotkeys.");
            }

            AssertStringEquals(
                LegacyMainWindow.GetBlueprintActionHotkeyDisplayForTesting(hotkeys, FeatureIds.BlueprintCreateAction),
                "Alt+C",
                "blueprint create action hotkey display");

            if (LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintSaveAction,
                    "Alt+C",
                    out message,
                    out changed) ||
                !message.Contains("蓝图创建快捷键"))
            {
                throw new InvalidOperationException("Expected blueprint save action hotkey to conflict with the create action hotkey.");
            }

            if (!LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintSaveAction,
                    "Alt+S",
                    out message,
                    out changed) ||
                !changed)
            {
                throw new InvalidOperationException("Expected blueprint save action hotkey to save under its own action key.");
            }

            if (!LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintLibraryAction,
                    "Alt+L",
                    out message,
                    out changed) ||
                !changed ||
                !string.Equals(hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintLibraryAction], "Alt+L", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint library action hotkey to save under its own action key.");
            }

            FeatureToggleHotkeyConflict conflict;
            if (!FeatureToggleHotkeyConflictRegistry.TryFindConflict(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    "buff.auto_heal",
                    "Alt+S",
                    out conflict) ||
                conflict.ConflictType != FeatureToggleHotkeyConflictType.BlueprintAction ||
                !string.Equals(conflict.OwnerTargetId, FeatureIds.BlueprintSaveAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected feature toggle conflict registry to report blueprint action hotkeys.");
            }

            if (!FeatureToggleHotkeyConflictRegistry.TryFindConflict(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    "buff.auto_heal",
                    "Alt+L",
                    out conflict) ||
                conflict.ConflictType != FeatureToggleHotkeyConflictType.BlueprintAction ||
                !string.Equals(conflict.OwnerTargetId, FeatureIds.BlueprintLibraryAction, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected feature toggle conflict registry to report blueprint library action hotkeys.");
            }

            var autoMiningSettings = HotkeySettings.CreateDefault();
            autoMiningSettings.HotkeysByFeatureId[FeatureIds.WorldAutomationAutoMining] = "Ctrl+F8";
            if (LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    autoMiningSettings,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintCreateAction,
                    "Ctrl+F8",
                    out message,
                    out changed) ||
                !message.Contains("自动挖矿 的采集按键"))
            {
                throw new InvalidOperationException("Expected blueprint action hotkeys to respect the auto-mining trigger conflict source.");
            }
        }

        private static void BlueprintActionHotkeyBackspaceClearContract()
        {
            var hotkeys = HotkeySettings.CreateDefault();
            string message;
            bool changed;
            if (!LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintCreateAction,
                    "Alt+C",
                    out message,
                    out changed) ||
                !LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintSaveAction,
                    "Alt+S",
                    out message,
                    out changed) ||
                !LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintLibraryAction,
                    "Alt+L",
                    out message,
                    out changed))
            {
                throw new InvalidOperationException("Expected blueprint action hotkeys to be seedable before Backspace clear.");
            }

            if (!LegacyMainWindow.TryClearBlueprintActionHotkeyForTesting(hotkeys, FeatureIds.BlueprintCreateAction, out changed) ||
                !changed ||
                hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintCreateAction) ||
                !hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintSaveAction) ||
                !hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintLibraryAction))
            {
                throw new InvalidOperationException("Expected Backspace clear to remove only the targeted blueprint action hotkey.");
            }

            if (!LegacyMainWindow.TryClearBlueprintActionHotkeyForTesting(hotkeys, FeatureIds.BlueprintLibraryAction, out changed) ||
                !changed ||
                hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintLibraryAction) ||
                !hotkeys.HotkeysByFeatureId.ContainsKey(FeatureIds.BlueprintSaveAction))
            {
                throw new InvalidOperationException("Expected Backspace clear to remove only the targeted blueprint library action hotkey.");
            }

            if (!LegacyMainWindow.TryClearBlueprintActionHotkeyForTesting(hotkeys, FeatureIds.BlueprintCreateAction, out changed) ||
                changed)
            {
                throw new InvalidOperationException("Expected clearing an already-empty blueprint action hotkey to be a no-op.");
            }

            if (LegacyMainWindow.TryApplyBlueprintActionHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.BlueprintCreateAction,
                    "Backspace",
                    out message,
                    out changed) ||
                changed)
            {
                throw new InvalidOperationException("Backspace must not be saved as a blueprint action hotkey.");
            }
        }

        private static void BlueprintCreateSaveActionCommandsEnterMaskAndSaveWithoutProjectionScan()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-action-shortcuts-real");
            try
            {
                ConfigService.Initialize();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintCaptureService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();

                var projectionReader = new FakeBlueprintWorldTileReader();
                var captureReader = new FakeBlueprintWorldTileReader();
                captureReader.Set(10, 20, new BlueprintWorldTileSnapshot
                {
                    Active = true,
                    TileType = 1,
                    TileMaterialItemId = 3,
                    TileDisplayName = "Dirt Block"
                });
                var inventoryReader = new FakeBlueprintMaterialInventoryReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    new BlueprintWorldInstanceStore(),
                    BlueprintPlacementWorldContext.Success("pair-action-shortcut", "world-action-shortcut"),
                    projectionReader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventoryReader, true);
                BlueprintCaptureService.SetCaptureDependenciesForTesting(captureReader, new BlueprintTemplateLibraryStore());

                LegacyMainUiState.SetVisible(true);
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintCreateActionElementIdForTesting(),
                    Label = "蓝图:创建蓝图:开始",
                    Kind = "button",
                    MouseCaptured = true
                });
                var entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                var creation = BlueprintCreationMaskState.GetSnapshot();
                if (!string.Equals(entry.Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal) ||
                    !creation.Active ||
                    creation.SelectedCount != 0)
                {
                    throw new InvalidOperationException("Expected stage-06 F5 create action to enter real mask creation mode.");
                }

                if (LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Expected stage-02 F5 create action to close the F5 menu after entering creation mode.");
                }

                ClickTileForBlueprintCreation(10, 20);
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintSaveActionElementIdForTesting(),
                    Label = "蓝图:保存蓝图:保存",
                    Kind = "button",
                    MouseCaptured = true
                });

                entry = BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault());
                creation = BlueprintCreationMaskState.GetSnapshot();
                if (!string.Equals(entry.Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                    creation.Active ||
                    creation.CompletedPendingCapture ||
                    string.IsNullOrWhiteSpace(entry.SelectedTemplateId) ||
                    captureReader.ReadCount <= 0)
                {
                    throw new InvalidOperationException("Expected stage-06 F5 save action to capture the pending mask and return to tool mode.");
                }

                if (projectionReader.ReadCount != 0 || inventoryReader.ReadCount != 0)
                {
                    throw new InvalidOperationException("Stage-06 create/save actions must not refresh blueprint projection or material inventory caches.");
                }

                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintCaptureService.ResetForTesting();
                BlueprintCaptureService.SetCaptureDependenciesForTesting(captureReader, new BlueprintTemplateLibraryStore());
                BlueprintLibraryUiState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();

                var hotkeys = HotkeySettings.CreateDefault();
                hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintCreateAction] = "Alt+C";
                hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintSaveAction] = "Alt+S";
                var downKeys = new Dictionary<int, bool>
                {
                    [0x12] = true,
                    ['C'] = true
                };
                var createHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                if (!createHotkey.Triggered ||
                    !createHotkey.Applied ||
                    !string.Equals(createHotkey.Action, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal) ||
                    !BlueprintCreationMaskState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected blueprint create action hotkey to enter real mask creation mode.");
                }

                var heldHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                if (heldHotkey.Triggered)
                {
                    throw new InvalidOperationException("Expected blueprint action hotkey to fire only on the press edge.");
                }

                ClickTileForBlueprintCreation(10, 20);
                downKeys['C'] = false;
                BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                downKeys['C'] = true;
                var exitHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                var exitedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!exitHotkey.Triggered ||
                    !exitHotkey.Applied ||
                    !string.Equals(exitHotkey.Action, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal) ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                    exitedMask.Active ||
                    exitedMask.CompletedPendingCapture ||
                    exitedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(exitedMask, 10, 20))
                {
                    throw new InvalidOperationException("Expected repeated blueprint create hotkey to exit creation while preserving the selected mask.");
                }

                downKeys['C'] = false;
                BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                downKeys['C'] = true;
                var resumeHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                var resumedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!resumeHotkey.Triggered ||
                    !resumeHotkey.Applied ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(AppSettings.CreateDefault()).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal) ||
                    !resumedMask.Active ||
                    resumedMask.CompletedPendingCapture ||
                    resumedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(resumedMask, 10, 20))
                {
                    throw new InvalidOperationException("Expected blueprint create hotkey to re-enter creation with the preserved mask still visible.");
                }

                downKeys['C'] = false;
                downKeys['S'] = true;
                var saveHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    downKeys,
                    true,
                    string.Empty,
                    false);
                if (!saveHotkey.Triggered ||
                    !saveHotkey.Applied ||
                    !string.Equals(saveHotkey.Action, BlueprintEntryCommands.FinishCreateSave, StringComparison.Ordinal) ||
                    BlueprintCreationMaskState.GetSnapshot().CompletedPendingCapture)
                {
                    throw new InvalidOperationException("Expected blueprint save action hotkey to capture and save the pending mask.");
                }

                downKeys['S'] = false;
                var blocked = BlueprintEntryHotkeyService.TickForTesting(
                    AppSettings.CreateDefault(),
                    hotkeys,
                    new Dictionary<int, bool> { [0x12] = true, ['C'] = true },
                    true,
                    string.Empty,
                    true);
                if (!blocked.Triggered ||
                    blocked.Applied ||
                    blocked.DiagnosticResultCode != DiagnosticResultCode.BlockedByUi)
                {
                    throw new InvalidOperationException("Expected blueprint action hotkey to be blocked while text input is focused.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintCaptureService.ResetForTesting();
                BlueprintLibraryUiState.ResetForTesting();
                LegacyMainUiState.SetVisible(false);
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                ConfigService.ResetSettingsForTesting();
                restore();
            }
        }

        private static void BlueprintCreateActionButtonSyncsExitStateWithSharedToggle()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-create-exit-sync");
            try
            {
                ConfigService.Initialize();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var projectionReader = new FakeBlueprintWorldTileReader();
                var inventoryReader = new FakeBlueprintMaterialInventoryReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    new BlueprintWorldInstanceStore(),
                    BlueprintPlacementWorldContext.Success("pair-create-exit-sync", "world-create-exit-sync"),
                    projectionReader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventoryReader, true);

                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintCreateActionButtonTextForTesting(settings),
                    "开始",
                    "blueprint create action initial button text");
                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintCreateActionButtonTooltipForTesting(settings),
                    "左键按住滑动选区，可多选",
                    "blueprint create action initial tooltip");

                LegacyMainUiState.SetVisible(true);
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintCreateActionElementIdForTesting(),
                    Label = "蓝图:创建蓝图:开始",
                    Kind = "button",
                    MouseCaptured = true
                });

                AssertStringEquals(
                    BlueprintEntryState.GetSnapshot(settings).Mode,
                    BlueprintEntryModes.Creating,
                    "F5 create action enters shared create state");
                if (!BlueprintCreationMaskState.GetSnapshot().Active || LegacyMainUiState.Visible)
                {
                    throw new InvalidOperationException("Expected F5 create action to enter creation mode and close the F5 menu.");
                }

                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintCreateActionButtonTextForTesting(settings),
                    "退出",
                    "blueprint create action creating button text");
                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintCreateActionButtonTooltipForTesting(settings),
                    "退出创建蓝图",
                    "blueprint create action creating tooltip");

                ClickTileForBlueprintCreation(14, 15);
                LegacyMainUiState.SetVisible(true);
                LegacyUiActionService.HandleBlueprintActionEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintCreateActionElementIdForTesting(),
                    Label = "蓝图:创建蓝图:退出",
                    Kind = "button",
                    MouseCaptured = true
                });

                var exitedMask = BlueprintCreationMaskState.GetSnapshot();
                if (!string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Tool, StringComparison.Ordinal) ||
                    exitedMask.Active ||
                    exitedMask.CompletedPendingCapture ||
                    exitedMask.SelectedCount != 1 ||
                    !HasBlueprintCell(exitedMask, 14, 15))
                {
                    throw new InvalidOperationException("Expected F5 exit action to leave creation mode while preserving the selected mask.");
                }

                AssertStringEquals(
                    LegacyMainWindow.GetBlueprintCreateActionButtonTextForTesting(settings),
                    "开始",
                    "blueprint create action restored button text");

                BlueprintEntryHotkeyService.ResetForTesting();
                var hotkeys = HotkeySettings.CreateDefault();
                hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintCreateAction] = "Alt+C";
                var createHotkey = BlueprintEntryHotkeyService.TickForTesting(
                    settings,
                    hotkeys,
                    new Dictionary<int, bool> { [0x12] = true, ['C'] = true },
                    true,
                    string.Empty,
                    false);
                if (!createHotkey.Triggered ||
                    !createHotkey.Applied ||
                    !string.Equals(createHotkey.Action, BlueprintEntryCommands.StartCreate, StringComparison.Ordinal) ||
                    !string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected blueprint.create action hotkey to enter the same create toggle state.");
                }

                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                LegacyUiActionService.HandleBlueprintHandheldActionBarCommandForTesting(
                    BuildBlueprintHandheldCommand(BlueprintHandheldActionBarState.ButtonIdCreate, "创建蓝图"));
                if (!string.Equals(BlueprintEntryState.GetSnapshot(settings).Mode, BlueprintEntryModes.Creating, StringComparison.Ordinal) ||
                    !BlueprintCreationMaskState.GetSnapshot().Active)
                {
                    throw new InvalidOperationException("Expected handheld create command to enter the same create toggle state.");
                }

                if (projectionReader.ReadCount != 0 || inventoryReader.ReadCount != 0)
                {
                    throw new InvalidOperationException("Create/exit button state sync must not refresh blueprint projection or material caches.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                BlueprintCreationMaskState.ResetForTesting();
                BlueprintEntryHotkeyService.ResetForTesting();
                BlueprintHandheldActionBarState.ResetInteractionForTesting();
                LegacyMainUiState.SetVisible(false);
                ConfigService.ResetSettingsForTesting();
                restore();
            }
        }

        private static void BlueprintMenuUiStateDoesNotRefreshProjectionOrMaterials()
        {
            var restore = PushTemporaryConfigDirectory("blueprint-menu-no-scan");
            try
            {
                ConfigService.Initialize();
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintEntryState.ResetForTesting();
                LegacyMainWindow.ResetRetainedFrameModelForTesting();
                LegacyMainWindow.ResetPageLayoutCacheForTesting();

                var store = new BlueprintWorldInstanceStore();
                BlueprintWorldInstanceRecord instance;
                RequireBlueprintSuccess(
                    store.CreateInstanceFromTemplate(
                        "pair-menu-noscan",
                        "world-menu-noscan",
                        CreateSingleMaterialTemplate("菜单轻量状态", 21, 501, 2),
                        3,
                        4,
                        0,
                        out instance),
                    "create menu no-scan blueprint instance");

                var reader = new FakeBlueprintWorldTileReader();
                var inventoryReader = new FakeBlueprintMaterialInventoryReader();
                BlueprintProjectionService.SetDependenciesForTesting(
                    store,
                    BlueprintPlacementWorldContext.Success("pair-menu-noscan", "world-menu-noscan"),
                    reader,
                    true);
                BlueprintMaterialService.SetInventoryReaderForTesting(inventoryReader, true);

                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                var height = LegacyMainWindow.CalculateBlueprintContentHeightForTesting();
                var window = new LegacyUiRect(40, 50, LegacyUiMetrics.DefaultWidth, LegacyUiMetrics.DefaultHeight);
                var content = new LegacyUiRect(58, 134, 520, 180);
                var area = LegacyScrollArea.Create(content, height, 0);
                LegacyMainWindow.BuildRetainedFrameModelSnapshotForTesting(
                    "blueprint",
                    window,
                    content,
                    area,
                    settings,
                    height,
                    new List<LegacyUiElement>());
                BlueprintProjectionService.BuildUiStateJson();
                BlueprintMaterialService.BuildUiStateJson();
                BlueprintProjectionOverlay.DrawInterfaceLayer();
                BlueprintMaterialWindowState.Show();
                BlueprintMaterialWindowOverlay.DrawInterfaceLayer();
                BlueprintMaterialWindowOverlay.ShouldSuppressHotbarScrollFromHook();
                LegacyUiActionService.HandleBlueprintEntryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = "blueprint-entry:" + BlueprintEntryCommands.OpenLibrary,
                    Label = "打开",
                    Kind = "button",
                    MouseCaptured = true,
                    Rect = new LegacyUiRect(70, 160, 64, 24)
                });

                if (reader.ReadCount != 0 || inventoryReader.ReadCount != 0)
                {
                    throw new InvalidOperationException("Blueprint main-menu layout, overlay draw, material-window draw, and action state must not refresh projection tiles or material inventory.");
                }

                var projection = BlueprintProjectionService.GetSnapshot();
                if (projection == null || reader.ReadCount <= 0)
                {
                    throw new InvalidOperationException("Expected explicit blueprint projection refresh to read world tiles.");
                }

                var inventoryReadsBefore = inventoryReader.ReadCount;
                BlueprintMaterialService.ForceRefreshForMaterialWindow();
                if (inventoryReader.ReadCount <= inventoryReadsBefore)
                {
                    throw new InvalidOperationException("Expected explicit material-window refresh to read material inventory.");
                }

                var projectionReadsAfterRefresh = reader.ReadCount;
                var inventoryReadsAfterRefresh = inventoryReader.ReadCount;
                BlueprintMaterialWindowOverlay.DrawInterfaceLayer();
                BlueprintMaterialWindowOverlay.ShouldSuppressHotbarScrollFromHook();
                if (reader.ReadCount != projectionReadsAfterRefresh || inventoryReader.ReadCount != inventoryReadsAfterRefresh)
                {
                    throw new InvalidOperationException("Expected material-window draw and hit-test to reuse the explicit refresh snapshot.");
                }
            }
            finally
            {
                BlueprintProjectionService.ResetForTesting();
                BlueprintMaterialService.ResetForTesting();
                BlueprintMaterialWindowOverlay.ResetForTesting();
                ConfigService.ResetSettingsForTesting();
                restore();
            }
        }

        private static void BlueprintReplacementConfigMenuContracts()
        {
            if (LegacyMainWindow.GetBlueprintTopSettingRowCountForTesting() != 3)
            {
                throw new InvalidOperationException("Expected blueprint top menu to keep only handheld entry, auto placement, and same-kind replacement rows.");
            }

            AssertContains(LegacyMainWindow.GetBlueprintReplacementVisualContractForTesting(), "config-popup");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintReplacementConfigPopupElementIdForTesting(),
                "blueprint-replacement-config-popup",
                "blueprint replacement config popup id");

            var restore = PushTemporaryConfigDirectory("blueprint-replacement-config-menu");
            try
            {
                ConfigService.ResetSettingsForTesting();
                if (ConfigService.AppSettings.BlueprintReplacementTorchesEnabled)
                {
                    throw new InvalidOperationException("Expected blueprint replacement categories to default to disabled.");
                }

                LegacyUiActionService.HandleBlueprintReplacementCategoryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintReplacementOptionElementIdForTesting(BlueprintReplacementCategories.Torch),
                    Label = "同类替换:火把",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (!ConfigService.AppSettings.BlueprintReplacementTorchesEnabled)
                {
                    throw new InvalidOperationException("Expected blueprint replacement config checkbox to toggle the torch category on.");
                }

                LegacyUiActionService.HandleBlueprintReplacementCategoryCommandForTesting(new LegacyUiCommand
                {
                    ElementId = LegacyMainWindow.GetBlueprintReplacementOptionElementIdForTesting(BlueprintReplacementCategories.Torch),
                    Label = "同类替换:火把",
                    Kind = "button",
                    MouseCaptured = true
                });
                if (ConfigService.AppSettings.BlueprintReplacementTorchesEnabled)
                {
                    throw new InvalidOperationException("Expected blueprint replacement config checkbox to toggle the torch category off.");
                }
            }
            finally
            {
                ConfigService.ResetSettingsForTesting();
                restore();
            }
        }

        private static Dictionary<int, bool> BlueprintEntryDownKeys(params int[] keys)
        {
            var down = new Dictionary<int, bool>();
            for (var index = 0; keys != null && index < keys.Length; index++)
            {
                down[keys[index]] = true;
            }

            return down;
        }
    }
}
