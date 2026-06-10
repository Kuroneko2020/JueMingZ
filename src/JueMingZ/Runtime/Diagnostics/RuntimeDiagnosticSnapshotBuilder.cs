using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingZ.Actions;
using JueMingZ.Automation.Fishing;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Features;
using JueMingZ.GameState;
using JueMingZ.Input;

namespace JueMingZ.Runtime
{
    internal sealed class RuntimeDiagnosticSnapshotContext
    {
        public bool Initialized { get; set; }
        public string Version { get; set; }
        public string TestRunId { get; set; }
        public RuntimeState State { get; set; }
        public FeatureManager FeatureManager { get; set; }
        public InputActionQueue ActionQueue { get; set; }
        public int FeatureCatalogCount { get; set; }
        public int ImplementedFeatureCount { get; set; }
        public int VisibleFeatureCount { get; set; }
        public int HotkeyVisibleFeatureCount { get; set; }
        public Dictionary<string, int> UserCategoryCounts { get; set; }
        public Dictionary<string, int> CodeDomainCounts { get; set; }
    }

    internal static partial class RuntimeDiagnosticSnapshotBuilder
    {
        public static DiagnosticSnapshot Build(RuntimeDiagnosticSnapshotContext context)
        {
            context = context ?? new RuntimeDiagnosticSnapshotContext();
            var source = BuildSource(context);
            var snapshot = new DiagnosticSnapshot();
            WriteBootstrapAndGameState(snapshot, source);
            WriteActionQueue(snapshot, source);
            WriteDiagnosticUi(snapshot, source);
            WritePerformanceAndConfig(snapshot, source);
            WriteInventoryInformationFishing(snapshot, source);
            WriteMovement(snapshot, source);
            WriteCombatAndRecovery(snapshot, source);
            return snapshot;
        }

        private static RuntimeDiagnosticSnapshotSource BuildSource(RuntimeDiagnosticSnapshotContext context)
        {
            var featureManager = context.FeatureManager;
            var actionQueue = context.ActionQueue;
            var state = context.State;
            var actionSnapshot = actionQueue == null ? InputActionQueueSnapshot.Empty : actionQueue.GetSnapshot();
            var gameState = GameStateReader.LastSnapshot ?? GameStateSnapshot.Unknown("Not read yet.");
            var diagnosticSlot = ConfigService.AppSettings.DiagnosticInputTestSlot;
            var settingsSnapshot = RuntimeSettingsSnapshotProvider.GetCurrent();

            return new RuntimeDiagnosticSnapshotSource
            {
                Context = context,
                FeatureInfo = featureManager == null ? FeatureManagerDiagnosticInfo.Empty : featureManager.GetDiagnosticInfo(),
                ActionSnapshot = actionSnapshot,
                GameState = gameState,
                LateBootstrapCompleted = state != null && state.LateBootstrapCompleted,
                DiagnosticSlot = diagnosticSlot,
                DiagnosticSlotInfo = DiagnosticHotbarInfo.FromSnapshot(gameState, diagnosticSlot),
                LastActionUserMessage = GetLastActionUserMessage(actionSnapshot),
                LastActionResultCode = GetLastActionResultCode(actionSnapshot),
                LastActionKind = GetLastActionKind(actionSnapshot),
                SettingsSnapshot = settingsSnapshot,
                FishingHasResidualState = FishingAutomationService.HasResidualState
            };
        }

        private static bool IsGameInputAvailable(GameStateSnapshot snapshot)
        {
            if (snapshot != null && snapshot.Ui != null)
            {
                return snapshot.Ui.GameInputAvailable;
            }

            return TerrariaMainCompat.AllowsInputProcessing;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static string GetLastActionUserMessage(InputActionQueueSnapshot actionSnapshot)
        {
            var hotkeyMessage = DiagnosticActionHotkeyService.LastDiagnosticHotkeyMessage;
            var hotkeyUtc = DiagnosticActionHotkeyService.LastDiagnosticHotkeyUtc;
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (hotkeyUtc.HasValue &&
                hotkeyUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(hotkeyMessage))
            {
                return hotkeyMessage;
            }

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonLabel))
            {
                return FirstNonEmpty(
                    DiagnosticInteractionDiagnostics.LastButtonMessage,
                    "已点击按钮：" + DiagnosticInteractionDiagnostics.LastButtonLabel + "，等待队列处理。");
            }

            return FirstNonEmpty(actionSnapshot == null ? string.Empty : actionSnapshot.LastActionMessage, hotkeyMessage);
        }

        private static string GetLastActionResultCode(InputActionQueueSnapshot actionSnapshot)
        {
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonResultCode))
            {
                return DiagnosticInteractionDiagnostics.LastButtonResultCode;
            }

            return actionSnapshot == null ? string.Empty : actionSnapshot.LastActionResultCode;
        }

        private static string GetLastActionKind(InputActionQueueSnapshot actionSnapshot)
        {
            var lastResultUtc = actionSnapshot == null || actionSnapshot.LastResult == null
                ? DateTime.MinValue
                : actionSnapshot.LastResult.FinishedUtc;

            if (DiagnosticInteractionDiagnostics.LastButtonClickUtc.HasValue &&
                DiagnosticInteractionDiagnostics.LastButtonClickUtc.Value > lastResultUtc &&
                !string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.LastButtonLabel))
            {
                return DiagnosticInteractionDiagnostics.LastButtonLabel;
            }

            return actionSnapshot == null ? string.Empty : actionSnapshot.LastActionKind;
        }

        private static string GetProcessName()
        {
            try
            {
                return Process.GetCurrentProcess().ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class RuntimeDiagnosticSnapshotSource
        {
            public RuntimeDiagnosticSnapshotContext Context { get; set; }
            public FeatureManagerDiagnosticInfo FeatureInfo { get; set; }
            public InputActionQueueSnapshot ActionSnapshot { get; set; }
            public GameStateSnapshot GameState { get; set; }
            public bool LateBootstrapCompleted { get; set; }
            public int DiagnosticSlot { get; set; }
            public DiagnosticHotbarSlotInfo DiagnosticSlotInfo { get; set; }
            public string LastActionUserMessage { get; set; }
            public string LastActionResultCode { get; set; }
            public string LastActionKind { get; set; }
            public RuntimeSettingsSnapshot SettingsSnapshot { get; set; }
            public bool FishingHasResidualState { get; set; }
        }
    }
}
