using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Threading;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Common;
using JueMingZ.Diagnostics;

namespace JueMingZ.Config
{
    public static class ConfigService
    {
        private static readonly object SyncRoot = new object();
        private static readonly int[] LegacyQuickItemConchFamilyItemTypes = { 4263, 4819, 5358, 5359, 5360, 5361, 5437 };

        public static string ConfigDirectory { get; private set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Terraria",
            "JueMing-Z",
            "config");

        public static string AppSettingsPath { get; private set; } = Path.Combine(ConfigDirectory, "appsettings.json");
        public static string FeatureSettingsPath { get; private set; } = Path.Combine(ConfigDirectory, "features.json");
        public static string HotkeySettingsPath { get; private set; } = Path.Combine(ConfigDirectory, "hotkeys.json");

        public static AppSettings AppSettings { get; private set; } = AppSettings.CreateDefault();
        public static FeatureSettings FeatureSettings { get; private set; } = FeatureSettings.CreateDefault();
        public static HotkeySettings HotkeySettings { get; private set; } = HotkeySettings.CreateDefault();
        public static ConfigSaveSummary LastSaveSummary { get; private set; }

        public static void Initialize()
        {
            // Load and migrate all config files under one lock; hot paths read RuntimeSettingsSnapshot instead.
            lock (SyncRoot)
            {
                try
                {
                    Directory.CreateDirectory(ConfigDirectory);
                    AppSettings = LoadOrCreate(AppSettingsPath, AppSettings.CreateDefault, MigrateAppSettings);
                    FeatureSettings = LoadOrCreate(FeatureSettingsPath, FeatureSettings.CreateDefault, MigrateFeatureSettings);
                    SynchronizeFeatureSettingsFromAppSettingsLocked();
                    HotkeySettings = LoadOrCreate(HotkeySettingsPath, HotkeySettings.CreateDefault, MigrateHotkeySettings);
                    Save("features.json", FeatureSettingsPath, FeatureSettings);
                    Logger.Configure(AppSettings.LogLevel, AppSettings.EnableTraceLog);
                    Logger.Info("ConfigService", "Config loaded: " + ConfigDirectory);
                }
                catch (Exception error)
                {
                    AppSettings = AppSettings.CreateDefault();
                    FeatureSettings = FeatureSettings.CreateDefault();
                    HotkeySettings = HotkeySettings.CreateDefault();
                    Logger.Warn("ConfigService", "Config initialization failed; using defaults.");
                    Logger.Debug("ConfigService", error.ToString());
                }
            }
        }

        public static ConfigSaveSummary SaveAll()
        {
            lock (SyncRoot)
            {
                SynchronizeFeatureSettingsFromAppSettingsLocked();
                var appSettings = Save("appsettings.json", AppSettingsPath, AppSettings);
                var featureSettings = Save("features.json", FeatureSettingsPath, FeatureSettings);
                var hotkeySettings = Save("hotkeys.json", HotkeySettingsPath, HotkeySettings);
                var summary = BuildSaveSummary(appSettings, featureSettings, hotkeySettings);
                LastSaveSummary = summary;

                if (summary.Succeeded)
                {
                    Logger.Info("ConfigService", summary.Summary);
                }
                else
                {
                    Logger.Warn("ConfigService", summary.Summary);
                    LogFailedSave(appSettings);
                    LogFailedSave(featureSettings);
                    LogFailedSave(hotkeySettings);
                }

                return summary;
            }
        }

        public static int CountAppSettingsEnabledFeatures()
        {
            lock (SyncRoot)
            {
                return CountAppSettingsEnabledFeaturesLocked();
            }
        }

        public static int CountFeatureSettingsEnabledFeatures()
        {
            lock (SyncRoot)
            {
                if (FeatureSettings == null || FeatureSettings.EnabledByFeatureId == null)
                {
                    return 0;
                }

                var count = 0;
                foreach (var pair in FeatureSettings.EnabledByFeatureId)
                {
                    if (pair.Value)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static int CountEffectiveEnabledFeatures()
        {
            lock (SyncRoot)
            {
                var featureCount = 0;
                if (FeatureSettings != null && FeatureSettings.EnabledByFeatureId != null)
                {
                    foreach (var pair in FeatureSettings.EnabledByFeatureId)
                    {
                        if (pair.Value)
                        {
                            featureCount++;
                        }
                    }
                }

                return Math.Max(CountAppSettingsEnabledFeaturesLocked(), featureCount);
            }
        }

        private static void SynchronizeFeatureSettingsFromAppSettingsLocked()
        {
            if (FeatureSettings == null)
            {
                FeatureSettings = FeatureSettings.CreateDefault();
            }

            if (FeatureSettings.EnabledByFeatureId == null)
            {
                FeatureSettings.EnabledByFeatureId = new System.Collections.Generic.Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }

            var settings = AppSettings ?? AppSettings.CreateDefault();
            SetFeatureEnabledLocked("buff.auto_heal", settings.AutoHealEnabled || !string.Equals(settings.AutoHealMode, "Off", StringComparison.OrdinalIgnoreCase));
            SetFeatureEnabledLocked("buff.auto_mana", settings.AutoManaEnabled || string.Equals(settings.AutoManaMode, "ManaFlower", StringComparison.OrdinalIgnoreCase));
            SetFeatureEnabledLocked("buff.auto_buff", settings.AutoBuffEnabled);
            SetFeatureEnabledLocked("buff.nurse_auto_heal", settings.AutoNurseEnabled);
            SetFeatureEnabledLocked("buff.auto_station_buff", settings.AutoStationBuffEnabled);
            SetFeatureEnabledLocked(FeatureIds.WorldAutomationTravelMenu, settings.WorldAutomationTravelMenuEnabled);
            SetFeatureEnabledLocked(FeatureIds.DiagnosticsWorldGenDebugViewer, settings.DiagnosticsWorldGenDebugViewerEnabled);
            SetFeatureEnabledLocked(FeatureIds.DiagnosticsDeveloperDebugCommands, settings.DiagnosticsDeveloperDebugCommandsEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryQuickItemHotkeys, settings.InventoryQuickItemHotkeysEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryAutoStack, settings.InventoryAutoStackEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryAutoSell, settings.InventoryAutoSellEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryAutoDiscard, settings.InventoryAutoDiscardEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryQuickBagOpen, settings.InventoryQuickBagOpenEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryAutoDepositCoins, settings.InventoryAutoDepositCoinsEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryAutoExtractinator, settings.InventoryAutoExtractinatorEnabled);
            SetFeatureEnabledLocked(FeatureIds.InventoryKeepFavorited, settings.InventoryKeepFavoritedEnabled);
            SetFeatureEnabledLocked(FeatureIds.NpcAutoReforge, settings.NpcAutoReforgeEnabled);
            SetFeatureEnabledLocked(FeatureIds.NpcAutoTaxCollect, settings.NpcAutoTaxCollectEnabled);
            SetFeatureEnabledLocked(FeatureIds.WorldAutomationAutoMining, settings.WorldAutomationAutoMiningEnabled);
            SetFeatureEnabledLocked(FeatureIds.WorldAutomationAutoCaptureCritter, settings.WorldAutomationAutoCaptureCritterEnabled);
            SetFeatureEnabledLocked(FeatureIds.WorldAutomationAutoHarvest, settings.WorldAutomationAutoHarvestEnabled);
            SetFeatureEnabledLocked("combat.auto_aim", settings.CombatAimAssistRadius > 0 || settings.CursorAimRadius > 0 || settings.PlayerAimRadius > 0);
            SetFeatureEnabledLocked(FeatureIds.CombatAutoClicker, settings.CombatAutoClickerEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatFlailCombo, settings.CombatFlailComboEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatPerfectRevolver, settings.CombatPerfectRevolverEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatMagicStringClicker, settings.CombatMagicStringClickerEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatAutoFacing, settings.CombatAutoFacingEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatEquipmentWarning, settings.CombatEquipmentWarningEnabled);
            SetFeatureEnabledLocked(FeatureIds.CombatGoblinExecution, settings.CombatGoblinExecutionEnabled);
            SetFeatureEnabledLocked("information.enemy_name_labels", settings.InformationEnemyNameLabelsEnabled);
            SetFeatureEnabledLocked("information.critter_name_labels", settings.InformationCritterNameLabelsEnabled);
            SetFeatureEnabledLocked("information.npc_name_labels", !string.Equals(settings.InformationNpcNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase));
            SetFeatureEnabledLocked("information.chest_name_labels", !string.Equals(settings.InformationChestNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase));
            SetFeatureEnabledLocked("information.sign_text_labels", InformationSignTextModes.IsEnabled(settings.InformationSignTextLabelsMode));
            SetFeatureEnabledLocked("information.tombstone_text_labels", InformationSignTextModes.IsEnabled(settings.InformationTombstoneTextLabelsMode));
            SetFeatureEnabledLocked("information.highlight_life_crystal", settings.InformationHighlightLifeCrystalEnabled);
            SetFeatureEnabledLocked("information.highlight_mana_crystal", settings.InformationHighlightManaCrystalEnabled);
            SetFeatureEnabledLocked(FeatureIds.InformationHighlightDigtoise, settings.InformationHighlightDigtoiseEnabled);
            SetFeatureEnabledLocked("information.highlight_life_fruit", settings.InformationHighlightLifeFruitEnabled);
            SetFeatureEnabledLocked("information.highlight_dragon_egg", settings.InformationHighlightDragonEggEnabled);
            SetFeatureEnabledLocked("information.info_panel_position", false);
            SetFeatureEnabledLocked("information.biome_display", settings.InformationBiomeDisplayEnabled);
            SetFeatureEnabledLocked("information.world_infection", settings.InformationWorldInfectionEnabled);
            SetFeatureEnabledLocked("information.luck_value", settings.InformationLuckValueEnabled);
            SetFeatureEnabledLocked("information.fishing_catches", settings.InformationFishingCatchesEnabled);
            SetFeatureEnabledLocked("information.fishing_filtered_catches", settings.InformationFishingFilteredCatchesEnabled);
            SetFeatureEnabledLocked("information.angler_quest", settings.InformationAnglerQuestEnabled);
            SetFeatureEnabledLocked("fishing.auto_fish", settings.FishingAutoFishEnabled);
            SetFeatureEnabledLocked("fishing.auto_loadout", settings.FishingAutoLoadoutEnabled);
            SetFeatureEnabledLocked("fishing.auto_equipment", settings.FishingAutoEquipmentEnabled);
            SetFeatureEnabledLocked("fishing.auto_store_quest_fish", FishingAutoStoreModes.IsEnabled(settings.FishingAutoStoreMode));
            SetFeatureEnabledLocked("fishing.filter", !string.Equals(FishingFilterModes.Normalize(settings.FishingFilterMode), FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase));
            SetFeatureEnabledLocked("movement.simulated_multi_jump", settings.MovementSimulatedMultiJumpEnabled);
            SetFeatureEnabledLocked("movement.continuous_dash", settings.MovementContinuousDashEnabled);
            SetFeatureEnabledLocked("movement.teleport_correction", settings.MovementTeleportCorrectionEnabled);
            SetFeatureEnabledLocked("movement.fall_protection", settings.MovementSafeLandingEnabled);
        }

        private static void SetFeatureEnabledLocked(string featureId, bool enabled)
        {
            if (!string.IsNullOrWhiteSpace(featureId))
            {
                FeatureSettings.EnabledByFeatureId[featureId] = enabled;
            }
        }

        private static int CountAppSettingsEnabledFeaturesLocked()
        {
            var settings = AppSettings ?? AppSettings.CreateDefault();
            var count = 0;
            if (settings.AutoHealEnabled || !string.Equals(settings.AutoHealMode, "Off", StringComparison.OrdinalIgnoreCase)) count++;
            if (settings.AutoManaEnabled || string.Equals(settings.AutoManaMode, "ManaFlower", StringComparison.OrdinalIgnoreCase)) count++;
            if (settings.AutoBuffEnabled) count++;
            if (settings.AutoNurseEnabled) count++;
            if (settings.AutoStationBuffEnabled) count++;
            if (settings.WorldAutomationTravelMenuEnabled) count++;
            if (settings.DiagnosticsWorldGenDebugViewerEnabled) count++;
            if (settings.DiagnosticsDeveloperDebugCommandsEnabled) count++;
            if (settings.InventoryQuickItemHotkeysEnabled) count++;
            if (settings.InventoryAutoStackEnabled) count++;
            if (settings.InventoryAutoSellEnabled) count++;
            if (settings.InventoryAutoDiscardEnabled) count++;
            if (settings.InventoryQuickBagOpenEnabled) count++;
            if (settings.InventoryAutoDepositCoinsEnabled) count++;
            if (settings.InventoryAutoExtractinatorEnabled) count++;
            if (settings.InventoryKeepFavoritedEnabled) count++;
            if (settings.NpcAutoReforgeEnabled) count++;
            if (settings.NpcAutoTaxCollectEnabled) count++;
            if (settings.WorldAutomationAutoMiningEnabled) count++;
            if (settings.WorldAutomationAutoCaptureCritterEnabled) count++;
            if (settings.WorldAutomationAutoHarvestEnabled) count++;
            if (settings.CombatAimAssistRadius > 0 || settings.CursorAimRadius > 0 || settings.PlayerAimRadius > 0) count++;
            if (settings.CombatAutoClickerEnabled) count++;
            if (settings.CombatFlailComboEnabled) count++;
            if (settings.CombatPerfectRevolverEnabled) count++;
            if (settings.CombatMagicStringClickerEnabled) count++;
            if (settings.CombatAutoFacingEnabled) count++;
            if (settings.CombatEquipmentWarningEnabled) count++;
            if (settings.CombatGoblinExecutionEnabled) count++;
            if (settings.InformationEnemyNameLabelsEnabled) count++;
            if (settings.InformationCritterNameLabelsEnabled) count++;
            if (!string.Equals(settings.InformationNpcNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase)) count++;
            if (!string.Equals(settings.InformationChestNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase)) count++;
            if (InformationSignTextModes.IsEnabled(settings.InformationSignTextLabelsMode)) count++;
            if (InformationSignTextModes.IsEnabled(settings.InformationTombstoneTextLabelsMode)) count++;
            if (settings.InformationHighlightLifeCrystalEnabled) count++;
            if (settings.InformationHighlightManaCrystalEnabled) count++;
            if (settings.InformationHighlightDigtoiseEnabled) count++;
            if (settings.InformationHighlightLifeFruitEnabled) count++;
            if (settings.InformationHighlightDragonEggEnabled) count++;
            if (settings.InformationBiomeDisplayEnabled) count++;
            if (settings.InformationWorldInfectionEnabled) count++;
            if (settings.InformationLuckValueEnabled) count++;
            if (settings.InformationFishingCatchesEnabled) count++;
            if (settings.InformationFishingFilteredCatchesEnabled) count++;
            if (settings.InformationAnglerQuestEnabled) count++;
            if (settings.FishingAutoFishEnabled) count++;
            if (settings.FishingAutoLoadoutEnabled) count++;
            if (settings.FishingAutoEquipmentEnabled) count++;
            if (FishingAutoStoreModes.IsEnabled(settings.FishingAutoStoreMode)) count++;
            if (!string.Equals(FishingFilterModes.Normalize(settings.FishingFilterMode), FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase)) count++;
            if (settings.MovementSimulatedMultiJumpEnabled) count++;
            if (settings.MovementContinuousDashEnabled) count++;
            if (settings.MovementTeleportCorrectionEnabled) count++;
            if (settings.MovementSafeLandingEnabled) count++;
            return count;
        }

        private static T LoadOrCreate<T>(string path, Func<T> createDefault, Action<T> migrate) where T : class
        {
            if (!File.Exists(path))
            {
                var defaultValue = createDefault();
                migrate(defaultValue);
                Save(GetFileName(path), path, defaultValue);
                return defaultValue;
            }

            try
            {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var serializer = CreateSerializer(typeof(T));
                    var value = serializer.ReadObject(stream) as T;
                    if (value == null)
                    {
                        throw new InvalidDataException("JSON content was empty or did not match the expected type.");
                    }

                    migrate(value);
                    Save(GetFileName(path), path, value);
                    return value;
                }
            }
            catch (Exception error)
            {
                BackupBadConfig(path);
                Logger.Warn("ConfigService", "Config read failed; bad file was backed up and defaults will be used: " + path);
                Logger.Debug("ConfigService", error.ToString());

                var defaultValue = createDefault();
                migrate(defaultValue);
                Save(GetFileName(path), path, defaultValue);
                return defaultValue;
            }
        }

        private static ConfigFileSaveResult Save<T>(string name, string path, T value) where T : class
        {
            string tempPath = null;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                tempPath = path + ".tmp-" + GetProcessIdSafe() + "-" + Guid.NewGuid().ToString("N");

                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    var serializer = CreateSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    stream.Flush(true);
                }

                Exception lastError = null;
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        ReplaceConfigFile(tempPath, path);
                        lastError = null;
                        break;
                    }
                    catch (IOException error)
                    {
                        lastError = error;
                        Thread.Sleep(50 * attempt);
                    }
                    catch (UnauthorizedAccessException error)
                    {
                        lastError = error;
                        Thread.Sleep(50 * attempt);
                    }
                }

                if (lastError != null)
                {
                    var errorMessage = "target file remained busy after 3 retries: " + lastError.Message;
                    Logger.Warn("ConfigService", "Config write skipped because target file is busy: " + path);
                    Logger.Debug("ConfigService", lastError.ToString());
                    return ConfigFileSaveResult.Failure(name, path, errorMessage);
                }

                if (!File.Exists(path))
                {
                    return ConfigFileSaveResult.Failure(name, path, "target file was not present after save");
                }

                return ConfigFileSaveResult.Success(name, path);
            }
            catch (Exception error)
            {
                Logger.Warn("ConfigService", "Config write failed; continuing with in-memory settings: " + path);
                Logger.Debug("ConfigService", error.ToString());
                return ConfigFileSaveResult.Failure(name, path, error.GetType().Name + ": " + error.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath))
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch (Exception cleanupError)
                    {
                        Logger.Debug("ConfigService", "Temporary config cleanup failed: " + tempPath + Environment.NewLine + cleanupError);
                    }
                }
            }
        }

        private static ConfigSaveSummary BuildSaveSummary(
            ConfigFileSaveResult appSettings,
            ConfigFileSaveResult featureSettings,
            ConfigFileSaveResult hotkeySettings)
        {
            var succeeded = IsSaveSucceeded(appSettings) &&
                            IsSaveSucceeded(featureSettings) &&
                            IsSaveSucceeded(hotkeySettings);
            return new ConfigSaveSummary
            {
                Utc = DateTime.UtcNow,
                Succeeded = succeeded,
                AppSettings = appSettings,
                FeatureSettings = featureSettings,
                HotkeySettings = hotkeySettings,
                Summary = succeeded
                    ? "Config save succeeded: appsettings.json, features.json, hotkeys.json."
                    : "Config save failed for one or more files; see per-file diagnostics."
            };
        }

        private static bool IsSaveSucceeded(ConfigFileSaveResult result)
        {
            return result != null && result.Succeeded;
        }

        private static void LogFailedSave(ConfigFileSaveResult result)
        {
            if (result == null || result.Succeeded)
            {
                return;
            }

            Logger.Warn(
                "ConfigService",
                "Config save failed: " + result.Name + " path=" + result.Path + " error=" + result.Error);
        }

        private static void ReplaceConfigFile(string tempPath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                try
                {
                    File.Replace(tempPath, targetPath, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                }
                catch (FileNotFoundException)
                {
                }
            }

            File.Copy(tempPath, targetPath, true);
        }

        private static string GetFileName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFileName(path);
            }
            catch
            {
                return path;
            }
        }

        private static DataContractJsonSerializer CreateSerializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
        }

        private static void BackupBadConfig(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                var backupPath = path + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                File.Copy(path, backupPath, false);
            }
            catch (Exception error)
            {
                Logger.Warn("ConfigService", "Bad config backup failed: " + path);
                Logger.Debug("ConfigService", error.ToString());
            }
        }

        private static int GetProcessIdSafe()
        {
            try
            {
                return System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch
            {
                return 0;
            }
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private static void MigrateAppSettings(AppSettings settings)
        {
            // Migrations normalize legacy aliases once at load/save rather than every runtime tick.
            if (settings.ConfigVersion <= 0)
            {
                settings.ConfigVersion = 1;
            }

            if (string.IsNullOrWhiteSpace(settings.LogLevel))
            {
                settings.LogLevel = "Info";
            }

            if (settings.DiagnosticInputTestSlot < 0)
            {
                Logger.Warn("ConfigService", "DiagnosticInputTestSlot below 0; clamped to 0.");
                settings.DiagnosticInputTestSlot = 0;
            }

            if (settings.DiagnosticInputTestSlot > 9)
            {
                Logger.Warn("ConfigService", "DiagnosticInputTestSlot above 9; clamped to 9.");
                settings.DiagnosticInputTestSlot = 9;
            }

            settings.AutoHealThresholdPercent = Clamp(settings.AutoHealThresholdPercent <= 0 ? 50 : settings.AutoHealThresholdPercent, 1, 100);
            settings.AutoManaThresholdPercent = Clamp(settings.AutoManaThresholdPercent <= 0 ? 35 : settings.AutoManaThresholdPercent, 1, 100);
            settings.AutoHealCooldownTicks = settings.AutoHealCooldownTicks <= 0 ? 120 : settings.AutoHealCooldownTicks;
            settings.AutoManaCooldownTicks = settings.AutoManaCooldownTicks <= 0 ? 8 : Math.Min(settings.AutoManaCooldownTicks, 8);
            settings.AutoBuffCooldownTicks = settings.AutoBuffCooldownTicks <= 0 ? 1800 : settings.AutoBuffCooldownTicks;
            settings.AutoHealMode = NormalizeAutoHealMode(settings.AutoHealMode, settings.AutoHealEnabled);
            settings.AutoManaMode = NormalizeAutoManaMode(settings.AutoManaMode, settings.AutoManaEnabled);
            settings.AutoHealEnabled = !string.Equals(settings.AutoHealMode, "Off", StringComparison.OrdinalIgnoreCase);
            settings.AutoManaEnabled = string.Equals(settings.AutoManaMode, "ManaFlower", StringComparison.OrdinalIgnoreCase);
            settings.AutoHealBlockedItemTypes = NormalizePositiveItemIds(settings.AutoHealBlockedItemTypes);
            settings.AutoManaBlockedItemTypes = NormalizePositiveItemIds(settings.AutoManaBlockedItemTypes);
            settings.CombatAimAssistRadius = Clamp(settings.CombatAimAssistRadius, 0, 50);
            settings.AimRangeOrigin = CombatAimModes.NormalizeRangeOrigin(settings.AimRangeOrigin);
            settings.AimTargetPriority = CombatAimModes.NormalizeTargetPriority(settings.AimTargetPriority);
            var migratingCombatAimAdvancedSettings = !settings.CombatAimSplitRadiusMigrated;
            if (migratingCombatAimAdvancedSettings)
            {
                settings.CursorAimRadius = settings.CombatAimAssistRadius;
                settings.PlayerAimRadius = settings.CombatAimAssistRadius;
                if (settings.ReleaseHoldTicks <= 0)
                {
                    settings.ReleaseHoldTicks = 8;
                }

                settings.PersistentCursorAimEnabled = true;
                settings.CombatAimSplitRadiusMigrated = true;
            }

            settings.CursorAimRadius = Clamp(settings.CursorAimRadius, 0, 50);
            settings.PlayerAimRadius = Clamp(settings.PlayerAimRadius, 0, 50);
            settings.ReleaseHoldTicks = Clamp(settings.ReleaseHoldTicks, 0, 20);
            settings.CombatAimAssistRadius = settings.CursorAimRadius;
            settings.InformationNpcNameLabelsMode = NormalizeInformationNpcNameLabelsMode(settings.InformationNpcNameLabelsMode);
            settings.InformationChestNameLabelsMode = NormalizeInformationChestNameLabelsMode(settings.InformationChestNameLabelsMode, settings.InformationChestNameLabelsEnabled);
            settings.InformationChestNameLabelsEnabled = !string.Equals(settings.InformationChestNameLabelsMode, "Off", StringComparison.OrdinalIgnoreCase);
            settings.InformationSignTextLabelsMode = InformationSignTextModes.Normalize(settings.InformationSignTextLabelsMode);
            settings.InformationSignTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationSignTextMaxLines);
            settings.InformationSignTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationSignTextMaxCharacters);
            settings.InformationTombstoneTextLabelsMode = InformationSignTextModes.Normalize(settings.InformationTombstoneTextLabelsMode);
            settings.InformationTombstoneTextMaxLines = InformationSignTextModes.ClampLines(settings.InformationTombstoneTextMaxLines);
            settings.InformationTombstoneTextMaxCharacters = InformationSignTextModes.ClampCharacters(settings.InformationTombstoneTextMaxCharacters);
            if (settings.InformationPanelPositionInitialized)
            {
                settings.InformationPanelX = Clamp(settings.InformationPanelX, 0, 4096);
                settings.InformationPanelY = Clamp(settings.InformationPanelY, 0, 4096);
            }
            else
            {
                settings.InformationPanelX = Clamp(settings.InformationPanelX <= 0 ? 20 : settings.InformationPanelX, 0, 4096);
                settings.InformationPanelY = Clamp(settings.InformationPanelY <= 0 ? 320 : settings.InformationPanelY, 0, 4096);
            }
            settings.InformationEnemyNameColor = NormalizeHexOrDefault(settings.InformationEnemyNameColor, "#CD5C5C");
            settings.InformationCritterNameColor = NormalizeHexOrDefault(settings.InformationCritterNameColor, "#5DADEC");
            if (string.Equals(settings.InformationCritterNameColor, "#FFD966", StringComparison.OrdinalIgnoreCase))
            {
                settings.InformationCritterNameColor = "#5DADEC";
            }

            settings.InformationNpcNameColor = NormalizeHexOrDefault(settings.InformationNpcNameColor, "#90EE90");
            settings.InformationChestNameColor = NormalizeHexOrDefault(settings.InformationChestNameColor, "#FFA500");
            settings.InformationSignTextColor = NormalizeHexOrDefault(settings.InformationSignTextColor, "#E6C16A");
            settings.InformationTombstoneTextColor = NormalizeHexOrDefault(settings.InformationTombstoneTextColor, "#FF5555");
            settings.InformationLifeCrystalHighlightColor = NormalizeHexOrDefault(settings.InformationLifeCrystalHighlightColor, "#FF69B4");
            settings.InformationManaCrystalHighlightColor = NormalizeHexOrDefault(settings.InformationManaCrystalHighlightColor, "#66CCFF");
            settings.InformationLifeFruitHighlightColor = NormalizeHexOrDefault(settings.InformationLifeFruitHighlightColor, "#7CFC00");
            settings.InformationDragonEggHighlightColor = NormalizeHexOrDefault(settings.InformationDragonEggHighlightColor, "#9370DB");
            settings.InformationBiomeTextColor = NormalizeHexOrDefault(settings.InformationBiomeTextColor, "#90EE90");
            settings.InformationWorldInfectionTextColor = NormalizeHexOrDefault(settings.InformationWorldInfectionTextColor, "#DDA0DD");
            settings.InformationLuckTextColor = NormalizeHexOrDefault(settings.InformationLuckTextColor, "#FAFAD2");
            settings.InformationFishingCatchesTextColor = NormalizeHexOrDefault(settings.InformationFishingCatchesTextColor, "#87CEFA");
            settings.InformationFishingFilteredCatchesTextColor = NormalizeHexOrDefault(settings.InformationFishingFilteredCatchesTextColor, "#FFB366");
            settings.InformationAnglerQuestTextColor = NormalizeHexOrDefault(settings.InformationAnglerQuestTextColor, "#E0FFFF");
            settings.InformationEnemyNameFontScale = NormalizeInformationFontScale(settings.InformationEnemyNameFontScale, 0.70d);
            settings.InformationCritterNameFontScale = NormalizeInformationFontScale(settings.InformationCritterNameFontScale, 0.70d);
            settings.InformationNpcNameFontScale = NormalizeInformationFontScale(settings.InformationNpcNameFontScale, 0.70d);
            settings.InformationChestNameFontScale = NormalizeInformationFontScale(settings.InformationChestNameFontScale, 0.70d);
            settings.InformationSignTextFontScale = NormalizeInformationFontScale(settings.InformationSignTextFontScale, 0.70d);
            settings.InformationTombstoneTextFontScale = NormalizeInformationFontScale(settings.InformationTombstoneTextFontScale, 0.70d);
            settings.InformationBiomeTextFontScale = NormalizeInformationFontScale(settings.InformationBiomeTextFontScale, 0.72d);
            settings.InformationWorldInfectionTextFontScale = NormalizeInformationFontScale(settings.InformationWorldInfectionTextFontScale, 0.72d);
            settings.InformationLuckTextFontScale = NormalizeInformationFontScale(settings.InformationLuckTextFontScale, 0.72d);
            settings.InformationFishingCatchesTextFontScale = NormalizeInformationFontScale(settings.InformationFishingCatchesTextFontScale, 0.72d);
            settings.InformationFishingFilteredCatchesTextFontScale = NormalizeInformationFontScale(settings.InformationFishingFilteredCatchesTextFontScale, 0.72d);
            settings.InformationAnglerQuestTextFontScale = NormalizeInformationFontScale(settings.InformationAnglerQuestTextFontScale, 0.72d);
            if (double.IsNaN(settings.UiTextVerticalOffsetOverride) || double.IsInfinity(settings.UiTextVerticalOffsetOverride))
            {
                settings.UiTextVerticalOffsetOverride = 0d;
            }
            settings.UiTextVerticalOffsetOverride = Math.Max(-12d, Math.Min(12d, settings.UiTextVerticalOffsetOverride));
            if (settings.InformationKnownChestKeys == null)
            {
                settings.InformationKnownChestKeys = new System.Collections.Generic.List<string>();
            }

            settings.InventoryAutoSellItemIds = NormalizeAutoSellItemIds(settings.InventoryAutoSellItemIds);
            settings.InventoryAutoDiscardItemIds = NormalizeAutoDiscardItemIds(settings.InventoryAutoDiscardItemIds);
            settings.NpcAutoReforgePrefixes = NormalizeQuickReforgePrefixes(settings.NpcAutoReforgePrefixes);
            settings.WorldAutomationAutoMiningMode = AutoMiningModes.Normalize(settings.WorldAutomationAutoMiningMode);
            settings.WorldAutomationAutoCaptureCritterMode = AutoCaptureCritterModes.Normalize(settings.WorldAutomationAutoCaptureCritterMode, settings.MiscAutoCaptureCritterEnabled);
            settings.MiscWorldGenDebugViewerEnabled = true;
            settings.MiscDeveloperDebugCommandsEnabled = true;

            if (settings.FishingAutoLoadoutEnabled && settings.FishingAutoEquipmentEnabled)
            {
                settings.FishingAutoEquipmentEnabled = false;
            }

            settings.FishingAutoStoreMode = FishingAutoStoreModes.Normalize(settings.FishingAutoStoreMode, settings.FishingAutoStoreQuestFishEnabled);
            settings.FishingAutoStoreQuestFishEnabled = FishingAutoStoreModes.IsEnabled(settings.FishingAutoStoreMode);
            settings.FishingFilterMode = FishingFilterModes.Normalize(settings.FishingFilterMode);
            settings.FishingFilterMatchMode = FishingFilterMatchModes.Normalize(settings.FishingFilterMatchMode);
            settings.FishingFilterCrateRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterCrateRule);
            settings.FishingFilterQuestFishRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterQuestFishRule);
            settings.FishingFilterEnemyRule = FishingFilterSpecialRuleModes.Normalize(settings.FishingFilterEnemyRule);
            settings.FishingFilterAllowExactEntries = NormalizeFishingFilterExactEntries(settings.FishingFilterAllowExactEntries);
            settings.FishingFilterDenyExactEntries = NormalizeFishingFilterExactEntries(settings.FishingFilterDenyExactEntries);
            settings.FishingFilterAllowKeywords = NormalizeFishingFilterKeywords(settings.FishingFilterAllowKeywords);
            settings.FishingFilterDenyKeywords = NormalizeFishingFilterKeywords(settings.FishingFilterDenyKeywords);
            settings.FishingFilterPresets = NormalizeFishingFilterPresets(settings.FishingFilterPresets);
            if (!settings.FishingFilterCutRodSkipDefaultMigrated)
            {
                settings.FishingFilterCutRodSkipEnabled = true;
                settings.FishingFilterCutRodSkipDefaultMigrated = true;
            }

            settings.MovementContinuousDashMode = MovementContinuousDashModes.Normalize(settings.MovementContinuousDashMode);
            if (!settings.MovementSafeLandingOptionsDefaultMigrated)
            {
                MovementSafeLandingOptionCatalog.ApplyDefaultOptions(settings);
            }

            if (!settings.MovementSafeLandingPriority1ExpansionDefaultMigrated)
            {
                settings.MovementSafeLandingFlyingCarpetEnabled = true;
                settings.MovementSafeLandingPriority1ExpansionDefaultMigrated = true;
            }

            if (!settings.MovementSafeLandingGravityGlobeDefaultMigrated)
            {
                settings.MovementSafeLandingGravityGlobeEnabled = true;
                settings.MovementSafeLandingGravityGlobeDefaultMigrated = true;
            }

            settings.OperationWindowX = Clamp(settings.OperationWindowX <= 0 ? 420 : settings.OperationWindowX, 0, 4096);
            settings.OperationWindowY = Clamp(settings.OperationWindowY <= 0 ? 120 : settings.OperationWindowY, 0, 4096);
            settings.OperationWindowWidth = Clamp(settings.OperationWindowWidth <= 0 ? 520 : settings.OperationWindowWidth, 360, 1600);
            settings.OperationWindowHeight = Clamp(settings.OperationWindowHeight <= 0 ? 420 : settings.OperationWindowHeight, 260, 1200);
            settings.LegacyMainWindowX = Clamp(settings.LegacyMainWindowX <= 0 ? 320 : settings.LegacyMainWindowX, 0, 4096);
            settings.LegacyMainWindowY = Clamp(settings.LegacyMainWindowY <= 0 ? 80 : settings.LegacyMainWindowY, 0, 4096);
            settings.LegacyMainWindowWidth = 600;
            settings.LegacyMainWindowHeight = 750;
            if (string.IsNullOrWhiteSpace(settings.LegacySelectedPageId))
            {
                settings.LegacySelectedPageId = "buff";
            }

            if (settings.AutoBuffWhitelist == null)
            {
                settings.AutoBuffWhitelist = new System.Collections.Generic.List<BuffPotionWhitelistEntry>();
            }
        }

        private static List<FishingFilterExactEntry> NormalizeFishingFilterExactEntries(List<FishingFilterExactEntry> entries)
        {
            var normalized = new List<FishingFilterExactEntry>();
            if (entries == null)
            {
                return normalized;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    continue;
                }

                var kind = NormalizeFishingFilterExactKind(entry.Kind);
                if (string.IsNullOrEmpty(kind) || entry.Id <= 0)
                {
                    continue;
                }

                var key = kind + ":" + entry.Id.ToString(CultureInfo.InvariantCulture);
                if (!seen.Add(key))
                {
                    continue;
                }

                normalized.Add(new FishingFilterExactEntry
                {
                    Kind = kind,
                    Id = entry.Id,
                    DisplayNameSnapshot = string.IsNullOrWhiteSpace(entry.DisplayNameSnapshot)
                        ? string.Empty
                        : entry.DisplayNameSnapshot.Trim()
                });
            }

            return normalized;
        }

        private static string NormalizeFishingFilterExactKind(string kind)
        {
            if (string.Equals(kind, "Item", StringComparison.OrdinalIgnoreCase))
            {
                return "Item";
            }

            if (string.Equals(kind, "NPC", StringComparison.OrdinalIgnoreCase))
            {
                return "NPC";
            }

            return string.Empty;
        }

        private static List<string> NormalizeFishingFilterKeywords(List<string> keywords)
        {
            var normalized = new List<string>();
            if (keywords == null)
            {
                return normalized;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < keywords.Count; index++)
            {
                var keyword = keywords[index];
                if (keyword == null)
                {
                    continue;
                }

                var text = keyword.Trim();
                if (text.Length <= 0 || !seen.Add(text))
                {
                    continue;
                }

                normalized.Add(text);
            }

            return normalized;
        }

        private static List<FishingFilterPreset> NormalizeFishingFilterPresets(List<FishingFilterPreset> presets)
        {
            var normalized = new List<FishingFilterPreset>();
            if (presets == null)
            {
                return normalized;
            }

            var order = new List<string>();
            var byKey = new Dictionary<string, FishingFilterPreset>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < presets.Count; index++)
            {
                var preset = presets[index];
                if (preset == null)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(preset.Name) ? string.Empty : preset.Name.Trim();
                if (name.Length <= 0)
                {
                    continue;
                }

                var filterMode = NormalizeFishingFilterPresetFilterScope(preset.FilterModeScope);
                if (string.IsNullOrWhiteSpace(filterMode))
                {
                    continue;
                }

                var matchMode = FishingFilterMatchModes.Normalize(preset.MatchModeScope);
                var exactEntries = NormalizeFishingFilterExactEntries(preset.ExactEntries);
                var keywords = NormalizeFishingFilterKeywords(preset.Keywords);
                var updatedAt = string.IsNullOrWhiteSpace(preset.UpdatedAt) ? string.Empty : preset.UpdatedAt.Trim();
                var clone = new FishingFilterPreset
                {
                    Name = name,
                    FilterModeScope = filterMode,
                    MatchModeScope = matchMode,
                    ExactEntries = exactEntries,
                    Keywords = keywords,
                    UpdatedAt = updatedAt
                };

                var key = BuildFishingFilterPresetKey(filterMode, matchMode, name);
                if (byKey.ContainsKey(key))
                {
                    order.Remove(key);
                }

                byKey[key] = clone;
                order.Add(key);
            }

            for (var index = 0; index < order.Count; index++)
            {
                normalized.Add(byKey[order[index]]);
            }

            return normalized;
        }

        private static string NormalizeFishingFilterPresetFilterScope(string filterMode)
        {
            var normalized = FishingFilterModes.Normalize(filterMode);
            if (string.Equals(normalized, FishingFilterModes.Disabled, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return string.Equals(normalized, FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase)
                ? FishingFilterModes.DenyList
                : FishingFilterModes.AllowList;
        }

        private static string BuildFishingFilterPresetKey(string filterMode, string matchMode, string name)
        {
            return (filterMode ?? string.Empty) + "|" +
                   (matchMode ?? string.Empty) + "|" +
                   (name ?? string.Empty).Trim();
        }

        private static void MigrateFeatureSettings(FeatureSettings settings)
        {
            if (settings.ConfigVersion <= 0)
            {
                settings.ConfigVersion = 1;
            }

            if (settings.EnabledByFeatureId == null)
            {
                settings.EnabledByFeatureId = new System.Collections.Generic.Dictionary<string, bool>();
            }
        }

        private static void MigrateHotkeySettings(HotkeySettings settings)
        {
            if (settings.ConfigVersion <= 0)
            {
                settings.ConfigVersion = 1;
            }

            if (settings.HotkeysByFeatureId == null)
            {
                settings.HotkeysByFeatureId = new System.Collections.Generic.Dictionary<string, string>();
            }

            if (settings.ConfigVersion < 3 &&
                IsLegacyDefaultQuickItemHotkeyBindings(settings.QuickItemHotkeyBindings))
            {
                settings.QuickItemHotkeyBindings = new List<QuickItemHotkeyBinding>();
            }

            settings.QuickItemHotkeyBindings = NormalizeQuickItemHotkeyBindings(settings.QuickItemHotkeyBindings);
            if (settings.ConfigVersion < 3)
            {
                settings.ConfigVersion = 3;
            }
        }

        private static List<QuickItemHotkeyBinding> NormalizeQuickItemHotkeyBindings(List<QuickItemHotkeyBinding> bindings)
        {
            var normalized = new List<QuickItemHotkeyBinding>();
            var source = bindings ?? new List<QuickItemHotkeyBinding>();
            for (var index = 0; index < source.Count; index++)
            {
                var entry = source[index];
                if (entry == null)
                {
                    continue;
                }

                var hotkey = string.IsNullOrWhiteSpace(entry.Hotkey) ? string.Empty : entry.Hotkey.Trim();
                var itemTypes = NormalizeQuickItemBindingItemTypes(entry.ItemTypes);

                normalized.Add(new QuickItemHotkeyBinding
                {
                    Hotkey = hotkey,
                    ItemTypes = itemTypes,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                        ? string.Empty
                        : entry.DisplayName.Trim(),
                    Enabled = entry.Enabled
                });
            }

            return normalized;
        }

        private static List<int> NormalizeAutoSellItemIds(List<int> itemIds)
        {
            var source = itemIds ?? new List<int> { 2337, 2338, 2339 };
            var normalized = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var itemId = source[index];
                if (itemId <= 0 ||
                    (itemId >= 71 && itemId <= 74) ||
                    !seen.Add(itemId))
                {
                    continue;
                }

                normalized.Add(itemId);
            }

            return normalized;
        }

        private static List<int> NormalizeAutoDiscardItemIds(List<int> itemIds)
        {
            var source = itemIds ?? new List<int>();
            var normalized = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var itemId = source[index];
                if (itemId <= 0 ||
                    (itemId >= 71 && itemId <= 74) ||
                    !seen.Add(itemId))
                {
                    continue;
                }

                normalized.Add(itemId);
            }

            return normalized;
        }

        private static List<int> NormalizePositiveItemIds(List<int> itemIds)
        {
            var source = itemIds ?? new List<int>();
            var normalized = new List<int>();
            var seen = new HashSet<int>();
            for (var index = 0; index < source.Count; index++)
            {
                var itemId = source[index];
                if (itemId <= 0 || !seen.Add(itemId))
                {
                    continue;
                }

                normalized.Add(itemId);
            }

            return normalized;
        }

        private static List<string> NormalizeQuickReforgePrefixes(List<string> prefixes)
        {
            var source = prefixes ?? new List<string>();
            var normalized = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < source.Count; index++)
            {
                var raw = source[index];
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var value = raw.Trim();
                if (value.Length <= 0 || !seen.Add(value))
                {
                    continue;
                }

                normalized.Add(value);
            }

            return normalized;
        }

        private static List<int> NormalizeQuickItemBindingItemTypes(List<int> itemTypes)
        {
            var normalized = new List<int>();
            if (itemTypes == null)
            {
                return normalized;
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < itemTypes.Count; index++)
            {
                var itemType = itemTypes[index];
                if (itemType <= 0 || !seen.Add(itemType))
                {
                    continue;
                }

                normalized.Add(itemType);
                break;
            }

            return normalized;
        }

        private static bool IsLegacyDefaultQuickItemHotkeyBindings(List<QuickItemHotkeyBinding> bindings)
        {
            if (bindings == null || bindings.Count != 1)
            {
                return false;
            }

            var entry = bindings[0];
            if (entry == null ||
                !string.Equals((entry.Hotkey ?? string.Empty).Trim(), "Ctrl+Alt+B", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var itemTypes = entry.ItemTypes;
            if (itemTypes == null || itemTypes.Count != LegacyQuickItemConchFamilyItemTypes.Length)
            {
                return false;
            }

            var seen = new HashSet<int>();
            for (var index = 0; index < itemTypes.Count; index++)
            {
                seen.Add(itemTypes[index]);
            }

            for (var index = 0; index < LegacyQuickItemConchFamilyItemTypes.Length; index++)
            {
                if (!seen.Contains(LegacyQuickItemConchFamilyItemTypes[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeAutoHealMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return "Off";
            }

            if (string.Equals(mode, "Quick", StringComparison.OrdinalIgnoreCase))
            {
                return "Quick";
            }

            if (string.Equals(mode, "Smart", StringComparison.OrdinalIgnoreCase))
            {
                return "Smart";
            }

            return legacyEnabled ? "Quick" : "Off";
        }

        private static string NormalizeAutoManaMode(string mode, bool legacyEnabled)
        {
            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return "Off";
            }

            if (string.Equals(mode, "ManaFlower", StringComparison.OrdinalIgnoreCase))
            {
                return "ManaFlower";
            }

            return legacyEnabled ? "ManaFlower" : "Off";
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

        private static string NormalizeInformationChestNameLabelsMode(string mode, bool legacyEnabled)
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

            if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase))
            {
                return legacyEnabled ? "Opened" : "Off";
            }

            return legacyEnabled ? "Opened" : "Off";
        }

        private static string NormalizeHexOrDefault(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (!text.StartsWith("#", StringComparison.Ordinal))
            {
                text = "#" + text;
            }

            if (text.Length != 7)
            {
                return fallback;
            }

            for (var index = 1; index < text.Length; index++)
            {
                var c = text[index];
                var hex =
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return fallback;
                }
            }

            return text.ToUpperInvariant();
        }

        private static double NormalizeInformationFontScale(double value, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                value = fallback;
            }

            if (value < 0.50d)
            {
                return 0.50d;
            }

            return value > 1.80d ? 1.80d : Math.Round(value, 2);
        }
    }
}
