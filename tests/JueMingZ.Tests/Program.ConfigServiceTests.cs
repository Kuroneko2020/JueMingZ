using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using JueMingZ.Common;
using JueMingZ.Config;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void ConfigServiceInitializeMigratesExistingConfigAfterReadStreamCloses()
        {
            var restore = PushTemporaryConfigDirectory("config-load-migration");
            try
            {
                var settings = AppSettings.CreateDefault();
                settings.DiagnosticInputTestSlot = 99;
                WriteConfigJson(ConfigService.AppSettingsPath, settings);

                ConfigService.Initialize();

                var saved = ReadConfigJson<AppSettings>(ConfigService.AppSettingsPath);
                if (saved.DiagnosticInputTestSlot != 9)
                {
                    throw new InvalidOperationException("Expected migrated appsettings.json to be saved after closing the read stream.");
                }

                if (ConfigService.LastSaveSummary == null ||
                    ConfigService.LastSaveSummary.AppSettings == null ||
                    !ConfigService.LastSaveSummary.AppSettings.Succeeded)
                {
                    throw new InvalidOperationException("Expected config initialize migration save diagnostics to succeed.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceInitializeBadJsonKeepsOriginalAndProtectsFeatures()
        {
            var restore = PushTemporaryConfigDirectory("config-bad-json");
            try
            {
                var originalAppSettings = "{ broken appsettings";
                File.WriteAllText(ConfigService.AppSettingsPath, originalAppSettings, Encoding.UTF8);
                WriteFeatureSettings(true);

                ConfigService.Initialize();

                AssertFileTextEquals(ConfigService.AppSettingsPath, originalAppSettings, "bad appsettings original");
                AssertBadConfigBackupExists(ConfigService.AppSettingsPath, originalAppSettings, "bad appsettings backup");
                AssertFeatureSetting(FeatureIds.MapQuickAnnouncement, true, "bad appsettings feature protection");
                AssertLastAppSettingsSaveFailed("bad appsettings read failure");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceInitializeBusyConfigKeepsOriginalAndProtectsFeatures()
        {
            var restore = PushTemporaryConfigDirectory("config-busy-read");
            try
            {
                var settings = AppSettings.CreateDefault();
                settings.MapQuickAnnouncementEnabled = true;
                WriteConfigJson(ConfigService.AppSettingsPath, settings);
                WriteFeatureSettings(true);
                var originalAppSettings = File.ReadAllText(ConfigService.AppSettingsPath, Encoding.UTF8);

                using (File.Open(ConfigService.AppSettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    ConfigService.Initialize();
                }

                AssertFileTextEquals(ConfigService.AppSettingsPath, originalAppSettings, "busy appsettings original");
                AssertFeatureSetting(FeatureIds.MapQuickAnnouncement, true, "busy appsettings feature protection");
                AssertLastAppSettingsSaveFailed("busy appsettings read failure");
                AssertNoConfigTempFiles("busy appsettings read failure");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceInitializeMissingAppSettingsDoesNotClearExistingFeatures()
        {
            var restore = PushTemporaryConfigDirectory("config-missing-app-existing-features");
            try
            {
                WriteFeatureSettings(true);

                ConfigService.Initialize();

                if (!File.Exists(ConfigService.AppSettingsPath))
                {
                    throw new InvalidOperationException("Expected missing appsettings.json to be created.");
                }

                AssertFeatureSetting(FeatureIds.MapQuickAnnouncement, true, "missing appsettings feature protection");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceSaveAllBusyTargetDoesNotBreakOriginal()
        {
            var restore = PushTemporaryConfigDirectory("config-save-busy");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, AppSettings.CreateDefault());
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();
                var originalAppSettings = File.ReadAllText(ConfigService.AppSettingsPath, Encoding.UTF8);
                ConfigService.AppSettings.MapQuickAnnouncementEnabled = true;

                ConfigSaveSummary summary;
                using (File.Open(ConfigService.AppSettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    summary = ConfigService.SaveAll();
                }

                if (summary == null ||
                    summary.AppSettings == null ||
                    summary.AppSettings.Succeeded)
                {
                    throw new InvalidOperationException("Expected SaveAll to report appsettings failure while target file is busy.");
                }

                AssertFileTextEquals(ConfigService.AppSettingsPath, originalAppSettings, "busy save original");
                AssertNoConfigTempFiles("busy save cleanup");
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceSaveAllTempWriteFailureDoesNotBreakOriginal()
        {
            var restore = PushTemporaryConfigDirectory("config-save-temp-failure");
            try
            {
                WriteConfigJson(ConfigService.AppSettingsPath, AppSettings.CreateDefault());
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, HotkeySettings.CreateDefault());
                ConfigService.Initialize();
                var originalAppSettings = File.ReadAllText(ConfigService.AppSettingsPath, Encoding.UTF8);
                var blockedTempPath = Path.Combine(ConfigService.ConfigDirectory, "blocked-temp");
                Directory.CreateDirectory(blockedTempPath);

                ConfigService.SetSaveTempPathFactoryForTesting(path => blockedTempPath);
                ConfigService.AppSettings.MapQuickAnnouncementEnabled = true;
                var summary = ConfigService.SaveAll();

                if (summary == null ||
                    summary.AppSettings == null ||
                    summary.AppSettings.Succeeded)
                {
                    throw new InvalidOperationException("Expected SaveAll to report appsettings failure when temp file creation fails.");
                }

                AssertFileTextEquals(ConfigService.AppSettingsPath, originalAppSettings, "temp write failure original");
            }
            finally
            {
                ConfigService.SetSaveTempPathFactoryForTesting(null);
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void ConfigServiceLoadFailureDoesNotPolluteNextTemporaryDirectory()
        {
            var restoreFailedScope = PushTemporaryConfigDirectory("config-load-failure-pollution");
            try
            {
                File.WriteAllText(ConfigService.AppSettingsPath, "{ broken appsettings", Encoding.UTF8);
                ConfigService.Initialize();
                AssertLastAppSettingsSaveFailed("intentional load failure");
            }
            finally
            {
                restoreFailedScope();
            }

            var restoreCleanScope = PushTemporaryConfigDirectory("config-load-failure-clean-next");
            try
            {
                var summary = ConfigService.SaveAll();
                if (summary == null || !summary.Succeeded)
                {
                    throw new InvalidOperationException("Expected new temporary config directory to reset load failure guards.");
                }

                AssertConfigFilesExistInCurrentTestDirectory("load failure guard reset");
            }
            finally
            {
                restoreCleanScope();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void WriteFeatureSettings(bool mapQuickAnnouncementEnabled)
        {
            var features = FeatureSettings.CreateDefault();
            features.EnabledByFeatureId[FeatureIds.MapQuickAnnouncement] = mapQuickAnnouncementEnabled;
            WriteConfigJson(ConfigService.FeatureSettingsPath, features);
        }

        private static void AssertFeatureSetting(string featureId, bool expected, string label)
        {
            var features = ReadConfigJson<FeatureSettings>(ConfigService.FeatureSettingsPath);
            bool actual;
            if (features.EnabledByFeatureId == null ||
                !features.EnabledByFeatureId.TryGetValue(featureId, out actual) ||
                actual != expected)
            {
                throw new InvalidOperationException("Expected " + label + " to keep " + featureId + "=" + expected + ".");
            }
        }

        private static void AssertLastAppSettingsSaveFailed(string label)
        {
            var summary = ConfigService.LastSaveSummary;
            if (summary == null ||
                summary.AppSettings == null ||
                summary.AppSettings.Succeeded ||
                string.IsNullOrWhiteSpace(summary.AppSettings.Error))
            {
                throw new InvalidOperationException("Expected appsettings save diagnostics to fail for " + label + ".");
            }
        }

        private static void AssertFileTextEquals(string path, string expected, string label)
        {
            var actual = File.ReadAllText(path, Encoding.UTF8);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected " + label + " file content to remain unchanged.");
            }
        }

        private static void AssertBadConfigBackupExists(string path, string expectedContent, string label)
        {
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            var backups = Directory.GetFiles(directory, fileName + ".bad-*");
            for (var index = 0; index < backups.Length; index++)
            {
                var text = File.ReadAllText(backups[index], Encoding.UTF8);
                if (string.Equals(text, expectedContent, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Expected " + label + " to be copied to a .bad backup.");
        }

        private static void AssertNoConfigTempFiles(string label)
        {
            var files = Directory.Exists(ConfigService.ConfigDirectory)
                ? Directory.GetFiles(ConfigService.ConfigDirectory, "*.tmp-*")
                : new string[0];
            if (files.Length > 0)
            {
                throw new InvalidOperationException("Expected no leftover config temp files after " + label + ".");
            }
        }

        private static void WriteConfigJson<T>(string path, T value)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                CreateConfigJsonSerializer(typeof(T)).WriteObject(stream, value);
            }
        }

        private static T ReadConfigJson<T>(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return (T)CreateConfigJsonSerializer(typeof(T)).ReadObject(stream);
            }
        }

        private static DataContractJsonSerializer CreateConfigJsonSerializer(Type type)
        {
            return new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            });
        }
    }
}
