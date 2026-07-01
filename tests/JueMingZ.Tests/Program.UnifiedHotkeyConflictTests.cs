using System;
using JueMingZ.Common;
using JueMingZ.Config;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Tests
{
    internal static partial class Program
    {
        private static void UnifiedHotkeyPoliciesExposeStage05Differences()
        {
            UnifiedHotkeyFeaturePolicy policy;
            string owner;
            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(
                    UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_heal"),
                    out policy,
                    out owner) ||
                policy.MaxModifierCount != 1 ||
                policy.AllowMousePrimary ||
                policy.BlocksTerrariaOriginalConflicts)
            {
                throw new InvalidOperationException("Expected feature-toggle policy to keep one-modifier keyboard-only strategy without Terraria hard fail.");
            }

            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(
                    UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                    out policy,
                    out owner) ||
                policy.MaxModifierCount < 2 ||
                !policy.AllowMousePrimary ||
                !string.Equals(owner, "快捷物品 1", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected quick item policy to allow multi-modifier mouse bindings.");
            }

            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(
                    UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                    out policy,
                    out owner) ||
                !string.Equals(policy.PolicyId, UnifiedHotkeyFeaturePolicyCatalog.AutoMiningTriggerPolicyId, StringComparison.Ordinal) ||
                !policy.AllowMousePrimary)
            {
                throw new InvalidOperationException("Expected auto-mining trigger policy to stay separate from feature toggle policy.");
            }

            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(
                    UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger,
                    out policy,
                    out owner) ||
                policy.MaxModifierCount != 2 ||
                !policy.AllowMousePrimary)
            {
                throw new InvalidOperationException("Expected quick announcement policy to preserve the two-prefix-plus-trigger shape.");
            }

            if (!UnifiedHotkeyFeaturePolicyCatalog.TryDescribeBinding(
                    UnifiedHotkeyBindingIds.ForBlueprintAction(FeatureIds.BlueprintCreateAction),
                    out policy,
                    out owner) ||
                policy.AllowMousePrimary ||
                policy.RuntimeGateSummary.IndexOf("F5", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("Expected blueprint action policy to document F5 / gate relation and reject mouse primary keys.");
            }
        }

        private static void UnifiedHotkeyConflictRegistryBuildsEnabledRegistrations()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult update;
            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.ForQuickItemSlot(0), "LCtrl+Num1", out update))
            {
                throw new InvalidOperationException("Expected quick item sample binding to save for registration test.");
            }

            var registrations = UnifiedHotkeyConflictRegistry.BuildRegistrations(settings);
            var foundQuickAnnouncement = false;
            var foundQuickItem = false;
            for (var index = 0; index < registrations.Count; index++)
            {
                var registration = registrations[index];
                if (registration == null || !registration.Enabled || registration.Chord == null)
                {
                    continue;
                }

                if (string.Equals(registration.BindingId, UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger, StringComparison.Ordinal) &&
                    string.Equals(registration.PolicyId, UnifiedHotkeyFeaturePolicyCatalog.QuickAnnouncementPolicyId, StringComparison.Ordinal))
                {
                    foundQuickAnnouncement = true;
                }

                if (string.Equals(registration.BindingId, UnifiedHotkeyBindingIds.ForQuickItemSlot(0), StringComparison.Ordinal) &&
                    string.Equals(registration.OwnerDisplayName, "快捷物品 1", StringComparison.Ordinal))
                {
                    foundQuickItem = true;
                }
            }

            if (!foundQuickAnnouncement || !foundQuickItem)
            {
                throw new InvalidOperationException("Expected unified hotkey conflict registry to expose enabled binding registrations.");
            }
        }

        private static void UnifiedHotkeyConflictRegistryReportsInternalConflicts()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyConflict conflict;
            if (!UnifiedHotkeyConflictRegistry.TryFindConflict(
                    settings,
                    UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                    "LAlt+LShift+MouseLeft",
                    out conflict) ||
                !string.Equals(conflict.ResultCode, "conflictWith:快捷宣告", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected default quick announcement chord to be an internal conflict source.");
            }

            UnifiedHotkeyBindingUpdateResult update;
            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_heal"), "LCtrl+K", out update))
            {
                throw new InvalidOperationException("Expected feature-toggle sample binding to save for conflict test.");
            }

            if (!UnifiedHotkeyConflictRegistry.TryFindConflict(
                    settings,
                    UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                    "LCtrl+K",
                    out conflict) ||
                !string.Equals(conflict.BindingId, UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_heal"), StringComparison.Ordinal) ||
                !string.Equals(conflict.PolicyId, UnifiedHotkeyFeaturePolicyCatalog.FeatureTogglePolicyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected auto-mining trigger to conflict with existing feature-toggle binding.");
            }
        }

        private static void UnifiedHotkeyConfigServiceRejectsConflictAndPolicyMismatch()
        {
            var restore = PushTemporaryConfigDirectory("unified-hotkeys-conflict-policy");
            try
            {
                ConfigService.Initialize();

                UnifiedHotkeyBindingUpdateResult result;
                if (!ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                        "LCtrl+RAlt+MouseX1",
                        out result))
                {
                    throw new InvalidOperationException("Expected quick item policy to allow multi-modifier mouse binding.");
                }

                if (ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.WorldAutomationAutoMiningTrigger,
                        "LCtrl+RAlt+MouseX1",
                        out result) ||
                    result == null ||
                    !string.Equals(result.ResultCode, "conflictWith:快捷物品 1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected unified save to reject duplicate internal chord with conflictWith owner.");
                }

                if (ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_mana"),
                        "LCtrl+MouseLeft",
                        out result) ||
                    result == null ||
                    !string.Equals(result.ResultCode, "invalidToken", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected feature-toggle policy to reject mouse primary keys.");
                }

                if (ConfigService.TrySaveUnifiedHotkeyBinding(
                        UnifiedHotkeyBindingIds.ForFeatureToggleTarget("buff.auto_mana"),
                        "LCtrl+RAlt+K",
                        out result) ||
                    result == null ||
                    !string.Equals(result.ResultCode, "invalidToken", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected feature-toggle policy to reject multi-modifier chords.");
                }
            }
            finally
            {
                restore();
                ConfigService.ResetSettingsForTesting();
            }
        }

        private static void UnifiedHotkeyRegistryIgnoresEmptyBindingsAndTerrariaOriginalConflicts()
        {
            var settings = UnifiedHotkeySettings.CreateDefault();
            UnifiedHotkeyBindingUpdateResult update;
            if (!settings.TrySetBinding(UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger, string.Empty, out update))
            {
                throw new InvalidOperationException("Expected quick announcement binding to clear for empty-conflict test.");
            }

            UnifiedHotkeyConflict conflict;
            if (UnifiedHotkeyConflictRegistry.TryFindConflict(
                    settings,
                    UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                    "LAlt+LShift+MouseLeft",
                    out conflict))
            {
                throw new InvalidOperationException("Expected empty unified hotkey bindings to be ignored by conflict registry.");
            }

            UnifiedHotkeyBindingUpdateResult failure;
            if (!UnifiedHotkeyConflictRegistry.TryValidateBinding(
                    settings,
                    UnifiedHotkeyBindingIds.ForQuickItemSlot(0),
                    "Space",
                    out failure))
            {
                throw new InvalidOperationException("Expected Terraria original keybind overlap such as Space to remain non-blocking.");
            }
        }

        private static void UnifiedHotkeyUiHelperReportsConflictReason()
        {
            var message = LegacyMainWindow.BuildUnifiedHotkeyFailureMessageForTesting("conflictWith:快捷物品 1");
            if (message.IndexOf("快捷物品 1", StringComparison.Ordinal) < 0 ||
                message.IndexOf("conflictWith:", StringComparison.Ordinal) >= 0)
            {
                throw new InvalidOperationException("Expected unified UI helper to show conflict owner without exposing conflictWith code.");
            }
        }
    }
}
