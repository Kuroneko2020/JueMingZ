using System;
using System.IO;
using System.Reflection;
using JueMingZ.Actions;
using JueMingZ.Config;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void TestProcessConfigDirectoryIsolatedFromUserDocuments()
        {
            AssertConfigServiceUsesTestDirectory("test process config isolation");
            if (string.IsNullOrWhiteSpace(ProcessTestConfigDirectory) ||
                !string.Equals(ConfigService.ConfigDirectory, ProcessTestConfigDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Expected ConfigService to use the process test config directory before tests run.");
            }
        }

        private static void TestConfigIsolationGuardRejectsRealDocumentsDirectory()
        {
            var previousConfigDirectory = ConfigService.ConfigDirectory;
            var previousAppSettingsPath = ConfigService.AppSettingsPath;
            var previousFeatureSettingsPath = ConfigService.FeatureSettingsPath;
            var previousHotkeySettingsPath = ConfigService.HotkeySettingsPath;
            try
            {
                SetConfigDirectoryForTesting(RealUserConfigDirectory);
                var rejected = false;
                try
                {
                    AssertConfigServiceUsesTestDirectory("intentional real config injection");
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                if (!rejected)
                {
                    throw new InvalidOperationException("Expected the config isolation guard to reject the real Documents config directory.");
                }
            }
            finally
            {
                SetConfigDirectoryForTesting(
                    previousConfigDirectory,
                    previousAppSettingsPath,
                    previousFeatureSettingsPath,
                    previousHotkeySettingsPath);
            }
        }

        private static void ConfigServiceSaveAllWritesOnlyTestConfigDirectory()
        {
            var restore = PushTemporaryConfigDirectory("save-all");
            try
            {
                AssertConfigServiceUsesTestDirectory("save-all setup");
                var summary = ConfigService.SaveAll();
                AssertSaveSummaryUsesTestDirectory(summary, "save-all");
                AssertConfigFilesExistInCurrentTestDirectory("save-all");
            }
            finally
            {
                restore();
            }
        }

        private static void ConfigServiceInitializeWritesOnlyTestConfigDirectory()
        {
            var restore = PushTemporaryConfigDirectory("initialize");
            try
            {
                AssertConfigServiceUsesTestDirectory("initialize setup");
                ConfigService.Initialize();
                AssertConfigServiceUsesTestDirectory("initialize after call");
                AssertConfigFilesExistInCurrentTestDirectory("initialize");
            }
            finally
            {
                restore();
            }
        }

        private static void OperationWindowStateSaveWritesOnlyTestConfigDirectory()
        {
            var restore = PushTemporaryConfigDirectory("operation-window-save");
            try
            {
                AssertConfigServiceUsesTestDirectory("operation window setup");
                var method = typeof(OperationWindowState).GetMethod(
                    "SaveLocked",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (method == null)
                {
                    throw new InvalidOperationException("OperationWindowState.SaveLocked reflection hook missing.");
                }

                method.Invoke(null, new object[0]);
                AssertSaveSummaryUsesTestDirectory(ConfigService.LastSaveSummary, "operation window save");
                AssertConfigFilesExistInCurrentTestDirectory("operation window save");
            }
            finally
            {
                restore();
            }
        }

        private static void LegacyUiFeatureToggleSaveWritesOnlyTestConfigDirectory()
        {
            var restore = PushTemporaryConfigDirectory("legacy-ui-feature-toggle");
            try
            {
                AssertConfigServiceUsesTestDirectory("legacy UI feature toggle setup");
                var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
                settings.MapQuickAnnouncementEnabled = false;
                LegacyUiInput.ResetInteractionState();
                LegacyUiInput.ResetActionUpdateGateStateForTesting();
                LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
                LegacyUiInput.EnqueueClick(
                    new LegacyUiElement
                    {
                        Id = "map-quick-announcement-mode:On",
                        Label = "开启",
                        Kind = "button",
                        Rect = new LegacyUiRect(8, 8, 64, 24),
                        Enabled = true
                    },
                    new LegacyMouseSnapshot
                    {
                        X = 16,
                        Y = 16,
                        LeftDown = true,
                        LeftPressed = true,
                        ReadAvailable = true,
                        WindowHit = true
                    },
                    true);

                LegacyUiActionService.Update(new InputActionQueue(), null);

                if (!settings.MapQuickAnnouncementEnabled ||
                    LegacyUiActionService.DispatchedCommandCountLast != 1)
                {
                    throw new InvalidOperationException("Expected legacy UI feature toggle action to dispatch and enable map quick announcement.");
                }

                AssertSaveSummaryUsesTestDirectory(ConfigService.LastSaveSummary, "legacy UI feature toggle");
                AssertConfigFilesExistInCurrentTestDirectory("legacy UI feature toggle");
            }
            finally
            {
                LegacyUiInput.ResetInteractionState();
                LegacyUiActionService.ResetActionUpdateDiagnosticsForTesting();
                restore();
            }
        }

        private static void AssertConfigFilesExistInCurrentTestDirectory(string label)
        {
            AssertConfigFileExists(ConfigService.AppSettingsPath, label + " appsettings");
            AssertConfigFileExists(ConfigService.FeatureSettingsPath, label + " features");
            AssertConfigFileExists(ConfigService.HotkeySettingsPath, label + " hotkeys");
        }

        private static void AssertConfigFileExists(string path, string label)
        {
            AssertPathUnderConfigDirectory(path, label);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Expected " + label + " file to exist under test config directory: " + path);
            }
        }
    }
}
