using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Input
{
    public static partial class LegacyUiActionService
    {
        private static void HandleMiscQuickItemHotkeysMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryQuickItemHotkeysEnabled != enabled;
            settings.InventoryQuickItemHotkeysEnabled = enabled;
            ConfigService.SaveAll();
            var bindingCount = ConfigService.HotkeySettings == null || ConfigService.HotkeySettings.QuickItemHotkeyBindings == null
                ? 0
                : ConfigService.HotkeySettings.QuickItemHotkeyBindings.Count;
            var message = changed
                ? (enabled
                    ? "Quick item hotkeys enabled."
                    : "Quick item hotkeys disabled.")
                : (enabled
                    ? "Quick item hotkeys already enabled."
                    : "Quick item hotkeys already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscQuickItemHotkeys",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"inventory.quick_item_hotkeys\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"bindingCount\":" + IntRaw(bindingCount) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoStackMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoStackEnabled != enabled;
            settings.InventoryAutoStackEnabled = enabled;
            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto stack enabled." : "Auto stack disabled.")
                : (enabled ? "Auto stack already enabled." : "Auto stack already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoStack",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoStack) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscQuickBagOpenMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryQuickBagOpenEnabled != enabled;
            settings.InventoryQuickBagOpenEnabled = enabled;
            if (!enabled)
            {
                QuickBagOpenService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Quick bag open enabled." : "Quick bag open disabled.")
                : (enabled ? "Quick bag open already enabled." : "Quick bag open already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscQuickBagOpen",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryQuickBagOpen) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoDepositCoinsMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoDepositCoinsEnabled != enabled;
            settings.InventoryAutoDepositCoinsEnabled = enabled;
            if (!enabled)
            {
                AutoDepositCoinsService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto deposit coins enabled." : "Auto deposit coins disabled.")
                : (enabled ? "Auto deposit coins already enabled." : "Auto deposit coins already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoDepositCoins",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoDepositCoins) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoTaxCollectMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.NpcAutoTaxCollectEnabled != enabled;
            settings.NpcAutoTaxCollectEnabled = enabled;
            if (!enabled)
            {
                AutoTaxCollectorService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto tax collect enabled." : "Auto tax collect disabled.")
                : (enabled ? "Auto tax collect already enabled." : "Auto tax collect already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoTaxCollect",
                "NpcServices",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.NpcAutoTaxCollect) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscAutoExtractinatorMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryAutoExtractinatorEnabled != enabled;
            settings.InventoryAutoExtractinatorEnabled = enabled;
            if (!enabled)
            {
                AutoExtractinatorService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Auto extractinator enabled." : "Auto extractinator disabled.")
                : (enabled ? "Auto extractinator already enabled." : "Auto extractinator already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscAutoExtractinator",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryAutoExtractinator) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }

        private static void HandleMiscKeepFavoritedMode(LegacyUiCommand command, string payload)
        {
            var before = BuildUiOptionStateJson();
            var enabled = IsOnMode(payload);
            var settings = ConfigService.AppSettings ?? AppSettings.CreateDefault();
            var changed = settings.InventoryKeepFavoritedEnabled != enabled;
            settings.InventoryKeepFavoritedEnabled = enabled;
            if (!enabled)
            {
                KeepFavoritedService.ClearState("ui disabled");
            }

            ConfigService.SaveAll();
            var message = changed
                ? (enabled ? "Keep favorited enabled." : "Keep favorited disabled.")
                : (enabled ? "Keep favorited already enabled." : "Keep favorited already disabled.");

            Record(
                command,
                "Ui.Toggle.MiscKeepFavorited",
                "Inventory",
                changed ? "Succeeded" : "NotApplicable",
                message,
                before,
                BuildUiOptionStateJson(),
                "{\"submitted\":false,\"implemented\":true,\"featureId\":\"" + EscapeJson(FeatureIds.InventoryKeepFavorited) + "\",\"enabled\":" + BoolRaw(enabled) + ",\"changed\":" + BoolRaw(changed) + ",\"mouseCaptured\":" + BoolRaw(command.MouseCaptured) + "}",
                "Button");
        }
    }
}
