using System;
using JueMingZ.Actions;
using JueMingZ.Automation.AutoRecovery;
using JueMingZ.Automation.Combat;
using JueMingZ.Automation.Fishing;
using JueMingZ.Automation.Information;
using JueMingZ.Automation.InventoryAndItems;
using JueMingZ.Automation.Movement;
using JueMingZ.Automation.NpcServices;
using JueMingZ.Automation.WorldAutomation;
using JueMingZ.Bootstrap;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;
using JueMingZ.UI;
using JueMingZ.UI.Information;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Runtime
{
    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        private static void WriteBootstrapAndGameState(DiagnosticSnapshot snapshot, RuntimeDiagnosticSnapshotSource source)
        {
            var _initialized = source.Context.Initialized;
            var Version = source.Context.Version;
            var TestRunId = source.Context.TestRunId;
            var State = source.Context.State;
            var _featureCatalogCount = source.Context.FeatureCatalogCount;
            var _implementedFeatureCount = source.Context.ImplementedFeatureCount;
            var _visibleFeatureCount = source.Context.VisibleFeatureCount;
            var _hotkeyVisibleFeatureCount = source.Context.HotkeyVisibleFeatureCount;
            var _userCategoryCounts = source.Context.UserCategoryCounts;
            var _codeDomainCounts = source.Context.CodeDomainCounts;
            var featureInfo = source.FeatureInfo;
            var gameState = source.GameState;
            var lateBootstrapCompleted = source.LateBootstrapCompleted;

            snapshot.Loaded = _initialized;
            snapshot.Version = Version;
            snapshot.RuntimeVersion = Version;
            snapshot.TestRunId = TestRunId;
            snapshot.ProcessName = GetProcessName();
            snapshot.BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            snapshot.LogDirectory = Logger.LogDirectory;
            snapshot.TerrariaDetected = GameMode.IsTerrariaLoaded;
            snapshot.TerrariaVersion = lateBootstrapCompleted ? GameMode.GetTerrariaVersionLateOnly() : "EarlyUnavailable";
            snapshot.NetModeDescription = lateBootstrapCompleted ? GameMode.GetDescriptionLateOnly() : "EarlyUnavailable";
            snapshot.UpdateCount = State == null ? 0 : State.UpdateCount;
            snapshot.LateBootstrapCompleted = lateBootstrapCompleted;
            snapshot.SafeBootstrapStarted = AssemblyLoadTracker.SafeBootstrapStarted;
            snapshot.HarmonyLoaded = DependencyChecker.FindType("HarmonyLib.Harmony", "0Harmony") != null;
            snapshot.SafeBootstrapHookInstalled = HookDiagnostics.SafeBootstrapHookInstalled;
            snapshot.HookUpdateInstalled = HookDiagnostics.HookUpdateInstalled;
            snapshot.DrawHookInstalled = HookDiagnostics.DrawHookInstalled;
            snapshot.InterfaceLayerHookInstalled = HookDiagnostics.InterfaceLayerHookInstalled;
            snapshot.ItemCheckHookInstalled = HookDiagnostics.ItemCheckHookInstalled;
            snapshot.ItemCheckHookMethod = HookDiagnostics.ItemCheckHookMethod;
            snapshot.GoblinExecutionHookInstalled = HookDiagnostics.GoblinExecutionHookInstalled;
            snapshot.GoblinExecutionHookMethod = HookDiagnostics.GoblinExecutionHookMethod;
            snapshot.DiagnosticsOverlayVisible = DiagnosticsOverlay.Visible;
            snapshot.DrawCallCount = DiagnosticsOverlay.DrawCallCount;
            snapshot.LastDrawUtc = DiagnosticsOverlay.LastDrawUtc;
            snapshot.LastUpdateUtc = RuntimeDiagnostics.LastUpdateUtc;
            snapshot.LastHeartbeatUtc = State == null ? null : State.LastHeartbeatUtc;
            snapshot.FeatureCount = featureInfo.TotalFeatures;
            snapshot.EnabledFeatureCount = featureInfo.EnabledFeatures;
            snapshot.AppSettingsEnabledFeatureCount = ConfigService.CountAppSettingsEnabledFeatures();
            snapshot.FeatureSettingsEnabledFeatureCount = ConfigService.CountFeatureSettingsEnabledFeatures();
            snapshot.EffectiveEnabledFeatureCount = ConfigService.CountEffectiveEnabledFeatures();
            snapshot.FeatureCatalogCount = _featureCatalogCount;
            snapshot.ImplementedFeatureCount = _implementedFeatureCount;
            snapshot.VisibleFeatureCount = _visibleFeatureCount;
            snapshot.HotkeyVisibleFeatureCount = _hotkeyVisibleFeatureCount;
            snapshot.UserCategoryCounts = _userCategoryCounts;
            snapshot.CodeDomainCounts = _codeDomainCounts;
            snapshot.FeatureManagerUpdateCount = featureInfo.UpdateCount;
            snapshot.WorldGenDebugViewerConfiguredEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.DiagnosticsWorldGenDebugViewerEnabled;
            snapshot.DeveloperDebugCommandsConfiguredEnabled = ConfigService.AppSettings != null && ConfigService.AppSettings.DiagnosticsDeveloperDebugCommandsEnabled;
            snapshot.WorldGenDebugViewerSessionConfiguredEnabled = WorldGenDebugCompat.WorldGenSessionConfiguredEnabled;
            snapshot.DeveloperDebugCommandsSessionConfiguredEnabled = WorldGenDebugCompat.SessionConfiguredEnabled;
            snapshot.WorldGenDebugAttempted = WorldGenDebugCompat.Attempted;
            snapshot.WorldGenDebugFieldEnabled = WorldGenDebugCompat.Enabled;
            snapshot.WorldGenDebugStatus = WorldGenDebugCompat.Status;
            snapshot.WorldGenDebugMessage = WorldGenDebugCompat.Message;
            snapshot.WorldGenDebugFieldOwner = WorldGenDebugCompat.FieldOwner;
            snapshot.WorldGenDebugLastAttemptUtc = WorldGenDebugCompat.LastAttemptUtc;
            snapshot.IsInMainMenu = gameState.IsInMainMenu;
            snapshot.IsInWorld = gameState.IsInWorld;
            snapshot.GameInputAvailable = IsGameInputAvailable(gameState);
            snapshot.PlayerLife = gameState.Player == null ? 0 : gameState.Player.Life;
            snapshot.PlayerLifeMax = gameState.Player == null ? 0 : gameState.Player.LifeMax;
            snapshot.PlayerMana = gameState.Player == null ? 0 : gameState.Player.Mana;
            snapshot.PlayerManaMax = gameState.Player == null ? 0 : gameState.Player.ManaMax;
            snapshot.SelectedItemType = gameState.Inventory == null || gameState.Inventory.SelectedItem == null ? 0 : gameState.Inventory.SelectedItem.Type;
            snapshot.SelectedItemName = gameState.Inventory == null || gameState.Inventory.SelectedItem == null ? string.Empty : gameState.Inventory.SelectedItem.Name;
            snapshot.InventoryNonEmptyCount = gameState.Inventory == null ? 0 : gameState.Inventory.NonEmptyCount;
            snapshot.ActiveBuffCount = gameState.ActiveBuffs == null ? 0 : gameState.ActiveBuffs.Count;
            snapshot.ActiveNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.ActiveNpcCount;
            snapshot.TownNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.TownNpcCount;
            snapshot.HostileNpcCount = gameState.Npcs == null ? 0 : gameState.Npcs.HostileNpcCount;
            snapshot.CritterCount = gameState.Npcs == null ? 0 : gameState.Npcs.CritterCount;
            snapshot.LastGameStateReadUtc = gameState.LastReadUtc;
            snapshot.LastGameStateReadError = gameState.LastReadError;
            snapshot.LastGameStateInventoryProfile = GameStateReader.LastInventoryProfile.ToString();
            snapshot.LastGameStateNpcProfile = GameStateReader.LastNpcProfile.ToString();
            snapshot.LastGameStateTileProfile = GameStateReader.LastTileProfile.ToString();
        }
    }
}
