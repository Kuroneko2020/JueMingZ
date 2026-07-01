using System;
using System.IO;
using System.Text;
using JueMingZ.Config;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeySettingsDefaultsUseCanonicalQuickAnnouncement()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            AssertStringEquals(
                settings.GetBinding(UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger),
                "LAlt+LShift+MouseLeft",
                "unified quick announcement default");

            if (settings.BindingsById == null || settings.BindingsById.Count != 1)
            {
                throw new InvalidOperationException("Expected unified hotkey defaults to contain only quick announcement.");
            }

            if (settings.BindingsById.ContainsValue("Alt+Shift+MouseLeft"))
            {
                throw new InvalidOperationException("Unified hotkey defaults must not write generic Alt/Shift tokens.");
            }

            if (string.IsNullOrWhiteSpace(settings.CreateCacheSignature()))
            {
                throw new InvalidOperationException("Expected unified hotkey settings to expose a cache signature.");
            }
        }

        private static void UnifiedHotkeySettingsDoNotMigrateLegacyHotkeys()
        {
            var restore = PushTemporaryConfigDirectory("unified-hotkeys-legacy");
            try
            {
                var legacy = HotkeySettings.CreateDefault();
                legacy.HotkeysByFeatureId["legacy.auto_mining"] = "Ctrl+Alt+M";
                legacy.QuickItemHotkeyBindings.Add(new QuickItemHotkeyBinding
                {
                    Hotkey = "Ctrl+Alt+B",
                    DisplayName = "legacy quick item"
                });

                WriteConfigJson(ConfigService.AppSettingsPath, AppSettings.CreateDefault());
                WriteConfigJson(ConfigService.FeatureSettingsPath, FeatureSettings.CreateDefault());
                WriteConfigJson(ConfigService.HotkeySettingsPath, legacy);

                ConfigService.Initialize();

                var unified = ConfigService.UnifiedHotkeySettings;
                if (unified == null)
                {
                    throw new InvalidOperationException("Expected unified hotkey settings to be initialized.");
                }

                AssertStringEquals(
                    unified.GetBinding(UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger),
                    "LAlt+LShift+MouseLeft",
                    "unified quick announcement default after legacy load");

                if (unified.BindingsById == null || unified.BindingsById.Count != 1)
                {
                    throw new InvalidOperationException("Legacy hotkeys must not be migrated into unified hotkey settings.");
                }

                if (!File.Exists(ConfigService.UnifiedHotkeySettingsPath))
                {
                    throw new InvalidOperationException("Expected unified-hotkeys.json to be created for the new config.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void UnifiedHotkeySettingsAcceptsOnlyCatalogTokens()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult result;
            if (!settings.TrySetBinding("inventory.quick_item.slot1", "RCtrl+NumPlus", out result))
            {
                throw new InvalidOperationException("Expected unified hotkey settings to accept catalog token chord.");
            }

            AssertStringEquals(result.ResultCode, "updated", "unified hotkey valid update result");
            AssertStringEquals(result.Normalized, "RCtrl+NumPlus", "unified hotkey valid normalized");
            AssertStringEquals(result.Display, "RCtrl + Num+", "unified hotkey valid display");
            AssertStringEquals(settings.GetBinding("inventory.quick_item.slot1"), "RCtrl+NumPlus", "unified hotkey stored normalized");

            AssertUnifiedHotkeySetFailure(settings, "inventory.quick_item.slot2", "Ctrl+K", "unsupportedToken");
            AssertUnifiedHotkeySetFailure(settings, "inventory.quick_item.slot2", "NotAKey", "invalidToken");
            AssertUnifiedHotkeySetFailure(settings, "inventory.quick_item.slot2", "LCtrl+F5", "reservedKey");
            AssertStringEquals(settings.GetBinding("inventory.quick_item.slot2"), string.Empty, "failed unified hotkey save does not mutate");
        }

        private static void UnifiedHotkeySettingsCacheSignatureTracksBindings()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            var initial = settings.CreateCacheSignature();

            UnifiedHotkeyBindingUpdateResult result;
            if (!settings.TrySetBinding("inventory.quick_item.slot1", "LCtrl+Num1", out result))
            {
                throw new InvalidOperationException("Expected cache signature test binding to save.");
            }

            var changed = settings.CreateCacheSignature();
            if (string.Equals(initial, changed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected cache signature to change after binding update.");
            }

            if (!settings.TrySetBinding("inventory.quick_item.slot1", string.Empty, out result))
            {
                throw new InvalidOperationException("Expected cache signature test binding to clear.");
            }

            AssertStringEquals(settings.CreateCacheSignature(), initial, "unified hotkey cache signature after clear");
        }

        private static void ConfigServiceUnifiedHotkeySaveFailureReturnsSaveFailed()
        {
            var restore = PushTemporaryConfigDirectory("unified-hotkeys-save-failure");
            try
            {
                ConfigService.Initialize();
                var blockedTempPath = Path.Combine(ConfigService.ConfigDirectory, "blocked-temp");
                Directory.CreateDirectory(blockedTempPath);

                ConfigService.SetSaveTempPathFactoryForTesting(path => blockedTempPath);
                UnifiedHotkeyBindingUpdateResult result;
                var succeeded = ConfigService.TrySaveUnifiedHotkeyBinding("inventory.quick_item.slot1", "LCtrl+Num1", out result);
                if (succeeded || result == null)
                {
                    throw new InvalidOperationException("Expected unified hotkey save to fail when temp file creation fails.");
                }

                AssertStringEquals(result.ResultCode, "saveFailed", "unified hotkey save failure result");
                if (string.IsNullOrWhiteSpace(result.Message))
                {
                    throw new InvalidOperationException("Expected unified hotkey save failure to preserve the underlying error message.");
                }

                var summary = ConfigService.LastSaveSummary;
                if (summary == null ||
                    summary.UnifiedHotkeySettings == null ||
                    summary.UnifiedHotkeySettings.Succeeded ||
                    string.IsNullOrWhiteSpace(summary.UnifiedHotkeySettings.Error))
                {
                    throw new InvalidOperationException("Expected unified hotkey save failure diagnostics in LastSaveSummary.");
                }
            }
            finally
            {
                ConfigService.SetSaveTempPathFactoryForTesting(null);
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void AssertUnifiedHotkeySetFailure(
            UnifiedHotkeySettings settings,
            string bindingId,
            string chordText,
            string expectedResultCode)
        {
            UnifiedHotkeyBindingUpdateResult result;
            if (settings.TrySetBinding(bindingId, chordText, out result))
            {
                throw new InvalidOperationException("Expected unified hotkey " + chordText + " to fail.");
            }

            AssertStringEquals(result.ResultCode, expectedResultCode, "unified hotkey failure result");
        }
    }
}
