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

        private static void BlueprintEntryHotkeyBlocksTextInputAndDebounces()
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
                true);
            if (!result.Triggered || result.Applied || !string.Equals(result.Reason, "textInputFocused", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint entry hotkey to be blocked while text input is focused.");
            }

            BlueprintEntryHotkeyService.TickForTesting(settings, hotkeys, BlueprintEntryDownKeys(), true, string.Empty, false);
            result = BlueprintEntryHotkeyService.TickForTesting(
                settings,
                hotkeys,
                BlueprintEntryDownKeys(BlueprintVkAlt, BlueprintVkB),
                true,
                string.Empty,
                false);
            if (!result.Triggered || !result.Applied || !string.Equals(result.Reason, "opened", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected blueprint entry hotkey to open the entry shell after gate clears.");
            }

            var held = BlueprintEntryHotkeyService.TickForTesting(
                settings,
                hotkeys,
                BlueprintEntryDownKeys(BlueprintVkAlt, BlueprintVkB),
                true,
                string.Empty,
                false);
            if (held.Triggered)
            {
                throw new InvalidOperationException("Expected blueprint entry hotkey to debounce held keys.");
            }
        }

        private static void BlueprintEntryHotkeyConflictRegistryReportsEntryOwner()
        {
            var hotkeys = HotkeySettings.CreateDefault();
            hotkeys.HotkeysByFeatureId[FeatureIds.BlueprintMain] = "Alt+B";
            FeatureToggleHotkeyConflict conflict;
            if (!FeatureToggleHotkeyConflictRegistry.TryFindConflict(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    FeatureIds.InventoryAutoStack,
                    "Alt+B",
                    out conflict) ||
                conflict.ConflictType != FeatureToggleHotkeyConflictType.BlueprintEntry)
            {
                throw new InvalidOperationException("Expected feature toggle hotkey conflicts to include the blueprint entry hotkey.");
            }

            string message;
            bool changed;
            if (!LegacyMainWindow.TryApplyBlueprintEntryHotkeyForTesting(
                    hotkeys,
                    AppSettings.CreateDefault(),
                    "Alt+B",
                    out message,
                    out changed) ||
                changed)
            {
                throw new InvalidOperationException("Expected blueprint entry hotkey self-save to be accepted as unchanged.");
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
                "blueprint-entry:open-library",
                "blueprint library open row command id");
            AssertStringEquals(
                LegacyMainWindow.GetBlueprintPlacedOpenElementIdForTesting(),
                "blueprint-entry:open-placed",
                "placed blueprint open row command id");
            AssertStringEquals(
                LegacyMainWindow.BuildBlueprintLibraryNameInputIdForTesting("template-ime"),
                "blueprint-library:name-input:template-ime",
                "blueprint library rename input id");
            AssertContains(LegacyMainWindow.GetBlueprintLibraryVisualContractForTesting(), "main-menu-open-row");
            AssertContains(LegacyMainWindow.GetBlueprintPlacedInstanceVisualContractForTesting(), "main-menu-open-row");
            AssertContains(LegacyMainWindow.GetBlueprintCreationVisualContractForTesting(), "drag-toggle");
            if (LegacyMainWindow.GetBlueprintLibraryPageSizeForTesting() != 6)
            {
                throw new InvalidOperationException("Expected blueprint library to keep a stable six-template page size.");
            }

            if (LegacyMainWindow.GetBlueprintMenuOpenRowCountForTesting() != 2)
            {
                throw new InvalidOperationException("Expected blueprint menu to keep library and placed-blueprint open rows.");
            }

            LegacyMainWindow.StartAutoMiningHotkeyCapture();
            LegacyMainWindow.StartBlueprintEntryHotkeyCapture();
            if (LegacyMainWindow.IsAutoMiningHotkeyCaptureActiveForTesting() ||
                !LegacyMainWindow.IsBlueprintEntryHotkeyCaptureActiveForTesting() ||
                !LegacyMainWindow.IsAnyHotkeyCaptureActive())
            {
                throw new InvalidOperationException("Expected blueprint hotkey capture to be mutually exclusive with auto mining capture.");
            }

            LegacyMainWindow.StopBlueprintEntryHotkeyCapture();
            LegacyMainWindow.StopAutoMiningHotkeyCapture();
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
