using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    internal static class FeatureToggleHotkeyService
    {
        private const string Scenario = "Hotkey.FeatureToggle";
        private const int VkAlt = 0x12;
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, FeatureToggleHotkeyRuntimeBinding> BindingCacheByTarget =
            new Dictionary<string, FeatureToggleHotkeyRuntimeBinding>(StringComparer.Ordinal);
        private static readonly List<FeatureToggleHotkeyRuntimeBinding> BindingCache =
            new List<FeatureToggleHotkeyRuntimeBinding>(64);
        private static readonly Dictionary<string, bool> WasDownByTargetId =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private static readonly FeatureToggleHotkeyRuntimeBinding[] EmptyBindings =
            new FeatureToggleHotkeyRuntimeBinding[0];
        private static FeatureToggleHotkeyRuntimeBinding[] BindingSnapshot = EmptyBindings;

        public static bool HasActiveBindings
        {
            get
            {
                var settings = ConfigService.HotkeySettings;
                return settings != null &&
                       settings.ToggleHotkeysByTargetId != null &&
                       settings.ToggleHotkeysByTargetId.Count > 0;
            }
        }

        public static void Tick(GameStateSnapshot gameState)
        {
            var result = TickCore(
                ConfigService.AppSettings ?? AppSettings.CreateDefault(),
                ConfigService.HotkeySettings ?? HotkeySettings.CreateDefault(),
                gameState,
                IsRuntimeGateAvailable(gameState, out var gateReason),
                gateReason,
                IsCurrentProcessForeground(),
                IsKeyDown,
                true,
                true);
            if (result != null)
            {
                // Result is intentionally ignored here; diagnostics are event-level
                // and tests use TickForTesting to inspect decisions without I/O.
            }
        }

        internal static FeatureToggleHotkeyDispatchResult TickForTesting(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            IDictionary<int, bool> downKeys,
            bool gameInputAvailable,
            string gateReason)
        {
            return TickCore(
                appSettings ?? AppSettings.CreateDefault(),
                hotkeySettings ?? HotkeySettings.CreateDefault(),
                null,
                gameInputAvailable,
                gateReason,
                true,
                key => downKeys != null && downKeys.TryGetValue(key, out var down) && down,
                false,
                false);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                BindingCacheByTarget.Clear();
                BindingCache.Clear();
                WasDownByTargetId.Clear();
                BindingSnapshot = EmptyBindings;
            }
        }

        private static FeatureToggleHotkeyDispatchResult TickCore(
            AppSettings appSettings,
            HotkeySettings hotkeySettings,
            GameStateSnapshot gameState,
            bool gameInputAvailable,
            string gateReason,
            bool foreground,
            Func<int, bool> isKeyDown,
            bool saveConfig,
            bool recordDiagnostic)
        {
            var bindings = RefreshBindings(hotkeySettings);
            if (bindings.Length <= 0)
            {
                return FeatureToggleHotkeyDispatchResult.NoOp;
            }

            var effectiveGateReason = ResolveGateReason(gameState, gameInputAvailable, gateReason, foreground);
            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var down = IsChordDown(binding.Chord, isKeyDown);
                var wasDown = GetWasDown(binding.TargetId);
                SetWasDown(binding.TargetId, down);

                if (!down || wasDown)
                {
                    continue;
                }

                FeatureToggleHotkeyDispatchResult result;
                if (!string.IsNullOrWhiteSpace(effectiveGateReason))
                {
                    result = FeatureToggleHotkeyDispatchResult.Blocked(
                        binding,
                        effectiveGateReason,
                        IsUiGateReason(effectiveGateReason)
                            ? DiagnosticResultCode.BlockedByUi
                            : DiagnosticResultCode.BlockedByEnvironment);
                }
                else
                {
                    var apply = FeatureToggleProfile.Toggle(appSettings, hotkeySettings, binding.TargetId, saveConfig);
                    result = FeatureToggleHotkeyDispatchResult.FromApply(binding, apply);
                    if (apply.Applied && saveConfig && !apply.ConfigSaved)
                    {
                        ConfigService.SaveAll();
                    }
                }

                if (recordDiagnostic)
                {
                    DiagnosticActionRecorder.RecordHotkeyEvent(
                        binding.Chord.Display,
                        Scenario,
                        result.DiagnosticResultCode,
                        result.Message);
                }

                return result;
            }

            return FeatureToggleHotkeyDispatchResult.NoOp;
        }

        private static FeatureToggleHotkeyRuntimeBinding[] RefreshBindings(HotkeySettings settings)
        {
            lock (SyncRoot)
            {
                var source = settings == null ? null : settings.ToggleHotkeysByTargetId;
                if (source == null || source.Count <= 0)
                {
                    BindingCacheByTarget.Clear();
                    BindingCache.Clear();
                    WasDownByTargetId.Clear();
                    BindingSnapshot = EmptyBindings;
                    return BindingSnapshot;
                }

                var changed = BindingCacheByTarget.Count != source.Count;
                if (!changed)
                {
                    foreach (var pair in source)
                    {
                        string targetId;
                        if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(pair.Key, out targetId) ||
                            !BindingCacheByTarget.TryGetValue(targetId, out var binding) ||
                            !string.Equals(binding.SourceChord, pair.Value ?? string.Empty, StringComparison.Ordinal))
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (!changed)
                {
                    return BindingSnapshot;
                }

                BindingCacheByTarget.Clear();
                BindingCache.Clear();
                foreach (var pair in source)
                {
                    string targetId;
                    FeatureToggleHotkeyChord chord;
                    if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeTargetId(pair.Key, out targetId) ||
                        !FeatureToggleHotkeyChord.TryParse(pair.Value, out chord))
                    {
                        continue;
                    }

                    var binding = new FeatureToggleHotkeyRuntimeBinding(
                        targetId,
                        FeatureToggleHotkeyTargetCatalog.GetDisplayName(targetId),
                        pair.Value ?? string.Empty,
                        chord);
                    BindingCacheByTarget[targetId] = binding;
                    BindingCache.Add(binding);
                }

                PruneWasDownLocked();
                BindingSnapshot = BindingCache.Count <= 0 ? EmptyBindings : BindingCache.ToArray();
                return BindingSnapshot;
            }
        }

        private static void PruneWasDownLocked()
        {
            if (WasDownByTargetId.Count <= 0)
            {
                return;
            }

            var stale = new List<string>();
            foreach (var pair in WasDownByTargetId)
            {
                if (!BindingCacheByTarget.ContainsKey(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }

            for (var index = 0; index < stale.Count; index++)
            {
                WasDownByTargetId.Remove(stale[index]);
            }
        }

        private static bool GetWasDown(string targetId)
        {
            lock (SyncRoot)
            {
                return WasDownByTargetId.TryGetValue(targetId, out var value) && value;
            }
        }

        private static void SetWasDown(string targetId, bool down)
        {
            lock (SyncRoot)
            {
                WasDownByTargetId[targetId] = down;
            }
        }

        private static bool IsRuntimeGateAvailable(GameStateSnapshot gameState, out string reason)
        {
            reason = ResolveGameStateGateReason(gameState);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                return false;
            }

            if (LegacyTextInput.IsAnyFocused || LegacyHexColorInput.IsAnyFocused)
            {
                reason = "textInputFocused";
                return false;
            }

            if (LegacyUiInput.IsActiveInteraction())
            {
                reason = "legacyUiActive";
                return false;
            }

            if (LegacyUiOverlayCoordinator.Current.HasAnyActiveModal())
            {
                reason = "legacyModalActive";
                return false;
            }

            if (LegacyMainWindow.IsAnyHotkeyCaptureActive())
            {
                reason = "hotkeyCaptureActive";
                return false;
            }

            return true;
        }

        private static string ResolveGateReason(
            GameStateSnapshot gameState,
            bool gameInputAvailable,
            string gateReason,
            bool foreground)
        {
            if (!foreground)
            {
                return "notForeground";
            }

            if (!gameInputAvailable)
            {
                return string.IsNullOrWhiteSpace(gateReason) ? "gameInputUnavailable" : gateReason;
            }

            return ResolveGameStateGateReason(gameState);
        }

        private static string ResolveGameStateGateReason(GameStateSnapshot gameState)
        {
            if (gameState == null)
            {
                return string.Empty;
            }

            if (!gameState.IsInWorld)
            {
                return "notInWorld";
            }

            if (gameState.IsInMainMenu)
            {
                return "mainMenu";
            }

            var ui = gameState.Ui;
            if (ui == null)
            {
                return "uiUnavailable";
            }

            if (!ui.GameInputAvailable)
            {
                return "gameInputUnavailable";
            }

            if (ui.IsInMainMenu)
            {
                return "mainMenu";
            }

            if (ui.ChatOpen)
            {
                return "chatOpen";
            }

            return string.Empty;
        }

        private static bool IsUiGateReason(string reason)
        {
            return string.Equals(reason, "textInputFocused", StringComparison.Ordinal) ||
                   string.Equals(reason, "legacyUiActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "legacyModalActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "hotkeyCaptureActive", StringComparison.Ordinal) ||
                   string.Equals(reason, "chatOpen", StringComparison.Ordinal);
        }

        private static bool IsChordDown(FeatureToggleHotkeyChord chord, Func<int, bool> isKeyDown)
        {
            if (chord == null || isKeyDown == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(chord.Modifier) && !isKeyDown(ModifierToVirtualKey(chord.Modifier)))
            {
                return false;
            }

            var key = MainKeyToVirtualKey(chord.Key);
            return key > 0 && isKeyDown(key);
        }

        private static int ModifierToVirtualKey(string modifier)
        {
            if (string.Equals(modifier, "Alt", StringComparison.Ordinal))
            {
                return VkAlt;
            }

            if (string.Equals(modifier, "Ctrl", StringComparison.Ordinal))
            {
                return VkControl;
            }

            return string.Equals(modifier, "Shift", StringComparison.Ordinal) ? VkShift : 0;
        }

        private static int MainKeyToVirtualKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return 0;
            }

            if (key.Length == 1)
            {
                var ch = char.ToUpperInvariant(key[0]);
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9'))
                {
                    return ch;
                }
            }

            if (key.Length >= 2 &&
                key[0] == 'F' &&
                int.TryParse(key.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey) &&
                functionKey >= 1 &&
                functionKey <= 24)
            {
                return 0x70 + functionKey - 1;
            }

            return 0;
        }

        private static bool IsKeyDown(int virtualKey)
        {
            return virtualKey > 0 && (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsCurrentProcessForeground()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                {
                    return true;
                }

                int processId;
                GetWindowThreadProcessId(foregroundWindow, out processId);
                return processId == Process.GetCurrentProcess().Id;
            }
            catch
            {
                return true;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr windowHandle, out int processId);
    }

    internal static class FeatureToggleProfile
    {
        public static FeatureToggleApplyResult Toggle(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            string targetId,
            bool saveConfig)
        {
            settings = settings ?? AppSettings.CreateDefault();
            hotkeySettings = hotkeySettings ?? HotkeySettings.CreateDefault();

            switch (targetId)
            {
                case "buff.auto_heal":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoRecoverySettings.NormalizeHealMode(settings.AutoHealMode, settings.AutoHealEnabled),
                        mode => AutoRecoveryService.SetAutoHealMode(mode),
                        AutoRecoverySettings.HealModeOff,
                        true);
                case "buff.auto_mana":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoRecoverySettings.NormalizeManaMode(settings.AutoManaMode, settings.AutoManaEnabled),
                        mode => AutoRecoveryService.SetAutoManaMode(mode),
                        AutoRecoverySettings.ManaModeOff,
                        true);
                case "buff.nurse_auto_heal":
                    return ToggleBinary(settings.AutoNurseEnabled, value => AutoRecoveryService.SetAutoNurseEnabled(value), targetId, true);
                case "buff.auto_station_buff":
                    return ToggleBinary(settings.AutoStationBuffEnabled, value => AutoRecoveryService.SetAutoStationBuffEnabled(value), targetId, true);
                case "buff.auto_buff":
                    return ToggleBinary(settings.AutoBuffEnabled, value =>
                    {
                        if (settings.AutoBuffEnabled != value)
                        {
                            AutoRecoveryService.ToggleAutoBuff();
                        }
                    }, targetId, true);
                case FeatureIds.InventoryQuickItemHotkeys:
                    return ToggleBinary(settings.InventoryQuickItemHotkeysEnabled, value => settings.InventoryQuickItemHotkeysEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoStack:
                    return ToggleBinary(settings.InventoryAutoStackEnabled, value => settings.InventoryAutoStackEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoSell:
                    return ToggleBinary(settings.InventoryAutoSellEnabled, value => settings.InventoryAutoSellEnabled = value, targetId, false);
                case FeatureIds.InventoryAutoDiscard:
                    return ToggleBinary(settings.InventoryAutoDiscardEnabled, value => settings.InventoryAutoDiscardEnabled = value, targetId, false);
                case FeatureIds.WorldAutomationAutoCaptureCritter:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled),
                        mode =>
                        {
                            settings.WorldAutomationAutoCaptureCritterMode = mode;
                            if (string.Equals(mode, AutoCaptureCritterModes.Off, StringComparison.Ordinal))
                            {
                                AutoCaptureCritterService.ClearState("feature toggle hotkey disabled");
                            }
                        },
                        AutoCaptureCritterModes.Off,
                        false);
                case FeatureIds.WorldAutomationAutoHarvest:
                    return ToggleBinary(settings.WorldAutomationAutoHarvestEnabled, value =>
                    {
                        settings.WorldAutomationAutoHarvestEnabled = value;
                        if (!value)
                        {
                            AutoHarvestService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryQuickBagOpen:
                    return ToggleBinary(settings.InventoryQuickBagOpenEnabled, value =>
                    {
                        settings.InventoryQuickBagOpenEnabled = value;
                        if (!value)
                        {
                            QuickBagOpenService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryAutoDepositCoins:
                    return ToggleBinary(settings.InventoryAutoDepositCoinsEnabled, value =>
                    {
                        settings.InventoryAutoDepositCoinsEnabled = value;
                        if (!value)
                        {
                            AutoDepositCoinsService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryAutoExtractinator:
                    return ToggleBinary(settings.InventoryAutoExtractinatorEnabled, value =>
                    {
                        settings.InventoryAutoExtractinatorEnabled = value;
                        if (!value)
                        {
                            AutoExtractinatorService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.InventoryKeepFavorited:
                    return ToggleBinary(settings.InventoryKeepFavoritedEnabled, value =>
                    {
                        settings.InventoryKeepFavoritedEnabled = value;
                        if (!value)
                        {
                            KeepFavoritedService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.NpcAutoReforge:
                    return ToggleBinary(settings.NpcAutoReforgeEnabled, value => settings.NpcAutoReforgeEnabled = value, targetId, false);
                case FeatureIds.WorldAutomationAutoMining:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode),
                        mode =>
                        {
                            settings.WorldAutomationAutoMiningMode = mode;
                            if (string.Equals(mode, AutoMiningModes.Off, StringComparison.Ordinal))
                            {
                                AutoMiningService.ClearSelection("feature toggle hotkey disabled");
                            }
                        },
                        AutoMiningModes.Off,
                        false,
                        mode => ValidateAutoMiningMode(mode, hotkeySettings));
                case FeatureIds.NpcAutoTaxCollect:
                    return ToggleBinary(settings.NpcAutoTaxCollectEnabled, value =>
                    {
                        settings.NpcAutoTaxCollectEnabled = value;
                        if (!value)
                        {
                            AutoTaxCollectorService.ClearState("feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.WorldAutomationTravelMenu:
                    return ToggleBinary(settings.WorldAutomationTravelMenuEnabled, value => settings.WorldAutomationTravelMenuEnabled = value, targetId, false);
                case FeatureIds.MapPersistentDeathMarkers:
                    return ToggleBinary(settings.MapPersistentDeathMarkersEnabled, value => settings.MapPersistentDeathMarkersEnabled = value, targetId, false);
                case FeatureIds.MapFootprints:
                    return ToggleBinary(settings.MapFootprintsDisplayEnabled, value => settings.MapFootprintsDisplayEnabled = value, targetId, false);
                case FeatureIds.MapRareCreatureDirection:
                    return ToggleBinary(settings.MapRareCreatureDirectionEnabled, value => settings.MapRareCreatureDirectionEnabled = value, targetId, false);
                case FeatureIds.MapTravellingMerchantDirection:
                    return ToggleBinary(settings.MapTravellingMerchantDirectionEnabled, value => settings.MapTravellingMerchantDirectionEnabled = value, targetId, false);
                case FeatureIds.MapQuickAnnouncement:
                    return ToggleBinary(settings.MapQuickAnnouncementEnabled, value => settings.MapQuickAnnouncementEnabled = value, targetId, false);
                case FeatureIds.MapCustomMarkers:
                    return ToggleBinary(settings.MapCustomMarkersEnabled, value => settings.MapCustomMarkersEnabled = value, targetId, false);
                case FeatureIds.FishingAutoFish:
                    return ToggleBinary(settings.FishingAutoFishEnabled, value => settings.FishingAutoFishEnabled = value, targetId, false);
                case FeatureIds.FishingAutoLoadout:
                    return ToggleBinary(settings.FishingAutoLoadoutEnabled, value =>
                    {
                        settings.FishingAutoLoadoutEnabled = value;
                        if (value)
                        {
                            settings.FishingAutoEquipmentEnabled = false;
                        }
                    }, targetId, false);
                case FeatureIds.FishingAutoEquipment:
                    return ToggleBinary(settings.FishingAutoEquipmentEnabled, value =>
                    {
                        settings.FishingAutoEquipmentEnabled = value;
                        if (value)
                        {
                            settings.FishingAutoLoadoutEnabled = false;
                        }
                    }, targetId, false);
                case FeatureIds.FishingAutoStoreQuestFish:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled),
                        mode =>
                        {
                            settings.FishingAutoStoreMode = mode;
                            settings.FishingAutoStoreQuestFishEnabled = FishingAutoStoreModes.IsEnabled(mode);
                        },
                        FishingAutoStoreModes.Off,
                        false);
                case "fishing.cut_rod_skip":
                    return ToggleBinary(settings.FishingFilterCutRodSkipEnabled, value => settings.FishingFilterCutRodSkipEnabled = value, targetId, false);
                case "information.enemy_name_labels":
                    return ToggleBinary(settings.InformationEnemyNameLabelsEnabled, value => settings.InformationEnemyNameLabelsEnabled = value, targetId, false);
                case "information.critter_name_labels":
                    return ToggleBinary(settings.InformationCritterNameLabelsEnabled, value => settings.InformationCritterNameLabelsEnabled = value, targetId, false);
                case "information.npc_name_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => NormalizeInformationNpcNameLabelsMode(settings.InformationNpcNameLabelsMode),
                        mode => settings.InformationNpcNameLabelsMode = mode,
                        "Off",
                        false);
                case "information.chest_name_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => NormalizeInformationChestNameLabelsMode(settings.InformationChestNameLabelsMode, settings.InformationChestNameLabelsEnabled),
                        mode =>
                        {
                            settings.InformationChestNameLabelsMode = mode;
                            settings.InformationChestNameLabelsEnabled = !string.Equals(mode, "Off", StringComparison.Ordinal);
                        },
                        "Off",
                        false);
                case "information.sign_text_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => InformationSignTextModes.Normalize(settings.InformationSignTextLabelsMode),
                        mode =>
                        {
                            settings.InformationSignTextLabelsMode = mode;
                            settings.InformationSignTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines);
                            settings.InformationSignTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters);
                        },
                        InformationSignTextModes.Off,
                        false);
                case "information.tombstone_text_labels":
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => InformationSignTextModes.Normalize(settings.InformationTombstoneTextLabelsMode),
                        mode =>
                        {
                            settings.InformationTombstoneTextLabelsMode = mode;
                            settings.InformationTombstoneTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines);
                            settings.InformationTombstoneTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters);
                        },
                        InformationSignTextModes.Off,
                        false);
                case "information.highlight_life_crystal":
                    return ToggleBinary(settings.InformationHighlightLifeCrystalEnabled, value => settings.InformationHighlightLifeCrystalEnabled = value, targetId, false);
                case "information.highlight_mana_crystal":
                    return ToggleBinary(settings.InformationHighlightManaCrystalEnabled, value => settings.InformationHighlightManaCrystalEnabled = value, targetId, false);
                case FeatureIds.InformationHighlightDigtoise:
                    return ToggleBinary(settings.InformationHighlightDigtoiseEnabled, value => settings.InformationHighlightDigtoiseEnabled = value, targetId, false);
                case "information.highlight_life_fruit":
                    return ToggleBinary(settings.InformationHighlightLifeFruitEnabled, value => settings.InformationHighlightLifeFruitEnabled = value, targetId, false);
                case "information.highlight_dragon_egg":
                    return ToggleBinary(settings.InformationHighlightDragonEggEnabled, value => settings.InformationHighlightDragonEggEnabled = value, targetId, false);
                case "information.biome_display":
                    return ToggleBinary(settings.InformationBiomeDisplayEnabled, value => settings.InformationBiomeDisplayEnabled = value, targetId, false);
                case "information.world_infection":
                    return ToggleBinary(settings.InformationWorldInfectionEnabled, value => settings.InformationWorldInfectionEnabled = value, targetId, false);
                case "information.luck_value":
                    return ToggleBinary(settings.InformationLuckValueEnabled, value => settings.InformationLuckValueEnabled = value, targetId, false);
                case "information.fishing_catches":
                    return ToggleBinary(settings.InformationFishingCatchesEnabled, value => settings.InformationFishingCatchesEnabled = value, targetId, false);
                case "information.fishing_filtered_catches":
                    return ToggleBinary(settings.InformationFishingFilteredCatchesEnabled, value => settings.InformationFishingFilteredCatchesEnabled = value, targetId, false);
                case "information.angler_quest":
                    return ToggleBinary(settings.InformationAnglerQuestEnabled, value => settings.InformationAnglerQuestEnabled = value, targetId, false);
                case FeatureIds.CombatAutoClicker:
                    return ToggleBinary(settings.CombatAutoClickerEnabled, value => settings.CombatAutoClickerEnabled = value, targetId, false);
                case FeatureIds.CombatFlailCombo:
                    return ToggleBinary(settings.CombatFlailComboEnabled, value => settings.CombatFlailComboEnabled = value, targetId, false);
                case FeatureIds.CombatPhasebladeQuickSwitch:
                    return ToggleBinary(settings.CombatPhasebladeQuickSwitchEnabled, value => settings.CombatPhasebladeQuickSwitchEnabled = value, targetId, false);
                case FeatureIds.CombatPerfectRevolver:
                    return ToggleBinary(settings.CombatPerfectRevolverEnabled, value => settings.CombatPerfectRevolverEnabled = value, targetId, false);
                case FeatureIds.CombatMagicStringClicker:
                    return ToggleBinary(settings.CombatMagicStringClickerEnabled, value => settings.CombatMagicStringClickerEnabled = value, targetId, false);
                case FeatureIds.CombatAutoFacing:
                    return ToggleBinary(settings.CombatAutoFacingEnabled, value => settings.CombatAutoFacingEnabled = value, targetId, false);
                case FeatureIds.CombatEquipmentWarning:
                    return ToggleBinary(settings.CombatEquipmentWarningEnabled, value => settings.CombatEquipmentWarningEnabled = value, targetId, false);
                case FeatureIds.CombatAutoBossDamageReport:
                    return ToggleBinary(settings.CombatAutoBossDamageReportEnabled, value => settings.CombatAutoBossDamageReportEnabled = value, targetId, false);
                case FeatureIds.CombatGoblinExecution:
                    return ToggleBinary(settings.CombatGoblinExecutionEnabled, value => settings.CombatGoblinExecutionEnabled = value, targetId, false);
                case FeatureIds.MovementSimulatedMultiJump:
                    return ToggleBinary(settings.MovementSimulatedMultiJumpEnabled, value =>
                    {
                        settings.MovementSimulatedMultiJumpEnabled = value;
                        if (!value)
                        {
                            MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(Guid.Empty, "feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                case FeatureIds.MovementContinuousDash:
                    return ToggleMulti(
                        settings,
                        hotkeySettings,
                        targetId,
                        () => settings.MovementContinuousDashEnabled ? MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode) : MovementContinuousDashModes.Off,
                        mode =>
                        {
                            var off = string.Equals(mode, MovementContinuousDashModes.Off, StringComparison.Ordinal);
                            settings.MovementContinuousDashEnabled = !off;
                            if (!off)
                            {
                                settings.MovementContinuousDashMode = MovementContinuousDashModes.Normalize(mode);
                            }
                        },
                        MovementContinuousDashModes.Off,
                        false);
                case FeatureIds.MovementTeleportCorrection:
                    return ToggleBinary(settings.MovementTeleportCorrectionEnabled, value => settings.MovementTeleportCorrectionEnabled = value, targetId, false);
                case FeatureIds.MovementSafeLanding:
                    return ToggleBinary(settings.MovementSafeLandingEnabled, value =>
                    {
                        settings.MovementSafeLandingEnabled = value;
                        if (!value)
                        {
                            MovementSafeLandingCompat.CancelSafeLandingJumpPulse(Guid.Empty, "feature toggle hotkey disabled");
                        }
                    }, targetId, false);
                default:
                    return FeatureToggleApplyResult.Blocked(targetId, "unknownTarget");
            }
        }

        private static FeatureToggleApplyResult ToggleBinary(
            bool current,
            Action<bool> setValue,
            string targetId,
            bool setterSaves)
        {
            var next = !current;
            setValue(next);
            return FeatureToggleApplyResult.CreateApplied(targetId, current ? "On" : "Off", next ? "On" : "Off", next, setterSaves);
        }

        private static FeatureToggleApplyResult ToggleMulti(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            string targetId,
            Func<string> getMode,
            Action<string> setMode,
            string offMode,
            bool setterSaves)
        {
            return ToggleMulti(settings, hotkeySettings, targetId, getMode, setMode, offMode, setterSaves, null);
        }

        private static FeatureToggleApplyResult ToggleMulti(
            AppSettings settings,
            HotkeySettings hotkeySettings,
            string targetId,
            Func<string> getMode,
            Action<string> setMode,
            string offMode,
            bool setterSaves,
            Func<string, FeatureToggleModeGateResult> modeGate)
        {
            var current = getMode == null ? offMode : getMode();
            if (!IsOffMode(current, offMode))
            {
                string normalizedCurrent;
                if (!FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(targetId, current, out normalizedCurrent))
                {
                    return FeatureToggleApplyResult.Blocked(targetId, "invalidCurrentMode");
                }

                var gate = modeGate == null ? FeatureToggleModeGateResult.Allow : modeGate(normalizedCurrent);
                if (!gate.Allowed)
                {
                    return FeatureToggleApplyResult.Blocked(targetId, gate.Reason);
                }

                EnsureLastModeMap(hotkeySettings)[targetId] = normalizedCurrent;
                setMode(offMode);
                return FeatureToggleApplyResult.CreateApplied(targetId, normalizedCurrent, offMode, false, setterSaves);
            }

            string lastMode;
            if (hotkeySettings.LastNonOffModeByTargetId == null ||
                !hotkeySettings.LastNonOffModeByTargetId.TryGetValue(targetId, out lastMode) ||
                !FeatureToggleHotkeyTargetCatalog.TryNormalizeLastNonOffMode(targetId, lastMode, out lastMode))
            {
                // Multi-mode hotkeys intentionally do not guess a default mode.
                return FeatureToggleApplyResult.Blocked(targetId, "noLastNonOffMode");
            }

            var restoreGate = modeGate == null ? FeatureToggleModeGateResult.Allow : modeGate(lastMode);
            if (!restoreGate.Allowed)
            {
                return FeatureToggleApplyResult.Blocked(targetId, restoreGate.Reason);
            }

            setMode(lastMode);
            return FeatureToggleApplyResult.CreateApplied(targetId, offMode, lastMode, true, setterSaves);
        }

        private static Dictionary<string, string> EnsureLastModeMap(HotkeySettings hotkeySettings)
        {
            if (hotkeySettings.LastNonOffModeByTargetId == null)
            {
                hotkeySettings.LastNonOffModeByTargetId = new Dictionary<string, string>();
            }

            return hotkeySettings.LastNonOffModeByTargetId;
        }

        private static FeatureToggleModeGateResult ValidateAutoMiningMode(string mode, HotkeySettings hotkeySettings)
        {
            if (!string.Equals(mode, AutoMiningModes.Hotkey, StringComparison.Ordinal))
            {
                return FeatureToggleModeGateResult.Allow;
            }

            var hotkeys = hotkeySettings == null ? null : hotkeySettings.HotkeysByFeatureId;
            string triggerHotkey;
            return hotkeys != null &&
                   hotkeys.TryGetValue(FeatureIds.WorldAutomationAutoMining, out triggerHotkey) &&
                   !string.IsNullOrWhiteSpace(triggerHotkey)
                ? FeatureToggleModeGateResult.Allow
                : FeatureToggleModeGateResult.Blocked("missingMiningTriggerHotkey");
        }

        private static bool IsOffMode(string mode, string offMode)
        {
            return string.Equals(mode ?? string.Empty, offMode ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeInformationNpcNameLabelsMode(string mode)
        {
            if (string.Equals(mode, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return "Type";
            }

            return string.Equals(mode, "Name", StringComparison.OrdinalIgnoreCase) ? "Name" : "Off";
        }

        private static string NormalizeInformationChestNameLabelsMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                return "Always";
            }

            if (string.Equals(mode, "Opened", StringComparison.OrdinalIgnoreCase) || legacyEnabled)
            {
                return "Opened";
            }

            return "Off";
        }
    }

    internal sealed class FeatureToggleHotkeyRuntimeBinding
    {
        public FeatureToggleHotkeyRuntimeBinding(
            string targetId,
            string displayName,
            string sourceChord,
            FeatureToggleHotkeyChord chord)
        {
            TargetId = targetId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            SourceChord = sourceChord ?? string.Empty;
            Chord = chord;
        }

        public string TargetId { get; private set; }
        public string DisplayName { get; private set; }
        public string SourceChord { get; private set; }
        public FeatureToggleHotkeyChord Chord { get; private set; }
    }

    internal sealed class FeatureToggleApplyResult
    {
        private FeatureToggleApplyResult()
        {
            TargetId = string.Empty;
            PreviousMode = string.Empty;
            NewMode = string.Empty;
            Reason = string.Empty;
        }

        public bool Applied { get; private set; }
        public bool NewEnabled { get; private set; }
        public bool ConfigSaved { get; private set; }
        public string TargetId { get; private set; }
        public string PreviousMode { get; private set; }
        public string NewMode { get; private set; }
        public string Reason { get; private set; }

        public static FeatureToggleApplyResult CreateApplied(
            string targetId,
            string previousMode,
            string newMode,
            bool newEnabled,
            bool configSaved)
        {
            return new FeatureToggleApplyResult
            {
                Applied = true,
                TargetId = targetId ?? string.Empty,
                PreviousMode = previousMode ?? string.Empty,
                NewMode = newMode ?? string.Empty,
                NewEnabled = newEnabled,
                ConfigSaved = configSaved
            };
        }

        public static FeatureToggleApplyResult Blocked(string targetId, string reason)
        {
            return new FeatureToggleApplyResult
            {
                Applied = false,
                TargetId = targetId ?? string.Empty,
                Reason = reason ?? string.Empty
            };
        }
    }

    internal sealed class FeatureToggleHotkeyDispatchResult
    {
        public static readonly FeatureToggleHotkeyDispatchResult NoOp =
            new FeatureToggleHotkeyDispatchResult
            {
                Triggered = false,
                DiagnosticResultCode = DiagnosticResultCode.NotApplicable,
                Message = string.Empty
            };

        public bool Triggered { get; private set; }
        public bool Applied { get; private set; }
        public bool NewEnabled { get; private set; }
        public string TargetId { get; private set; }
        public string DisplayName { get; private set; }
        public string Chord { get; private set; }
        public string Reason { get; private set; }
        public string NewMode { get; private set; }
        public string Message { get; private set; }
        public DiagnosticResultCode DiagnosticResultCode { get; private set; }

        public static FeatureToggleHotkeyDispatchResult FromApply(
            FeatureToggleHotkeyRuntimeBinding binding,
            FeatureToggleApplyResult apply)
        {
            binding = binding ?? new FeatureToggleHotkeyRuntimeBinding(string.Empty, string.Empty, string.Empty, null);
            apply = apply ?? FeatureToggleApplyResult.Blocked(binding.TargetId, "unknown");
            return new FeatureToggleHotkeyDispatchResult
            {
                Triggered = true,
                Applied = apply.Applied,
                NewEnabled = apply.NewEnabled,
                TargetId = binding.TargetId,
                DisplayName = binding.DisplayName,
                Chord = binding.Chord == null ? string.Empty : binding.Chord.Display,
                Reason = apply.Applied ? "applied" : apply.Reason,
                NewMode = apply.NewMode,
                DiagnosticResultCode = apply.Applied ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.NotApplicable,
                Message = apply.Applied
                    ? binding.DisplayName + " feature toggle hotkey switched to " + apply.NewMode + "."
                    : binding.DisplayName + " feature toggle hotkey ignored: " + apply.Reason + "."
            };
        }

        public static FeatureToggleHotkeyDispatchResult Blocked(
            FeatureToggleHotkeyRuntimeBinding binding,
            string reason,
            DiagnosticResultCode resultCode)
        {
            binding = binding ?? new FeatureToggleHotkeyRuntimeBinding(string.Empty, string.Empty, string.Empty, null);
            return new FeatureToggleHotkeyDispatchResult
            {
                Triggered = true,
                Applied = false,
                TargetId = binding.TargetId,
                DisplayName = binding.DisplayName,
                Chord = binding.Chord == null ? string.Empty : binding.Chord.Display,
                Reason = reason ?? string.Empty,
                DiagnosticResultCode = resultCode,
                Message = binding.DisplayName + " feature toggle hotkey blocked: " + (reason ?? string.Empty) + "."
            };
        }
    }

    internal struct FeatureToggleModeGateResult
    {
        public static readonly FeatureToggleModeGateResult Allow = new FeatureToggleModeGateResult(true, string.Empty);

        private FeatureToggleModeGateResult(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
        }

        public bool Allowed { get; private set; }
        public string Reason { get; private set; }

        public static FeatureToggleModeGateResult Blocked(string reason)
        {
            return new FeatureToggleModeGateResult(false, reason);
        }
    }
}
