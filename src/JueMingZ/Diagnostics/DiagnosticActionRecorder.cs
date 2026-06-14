using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using JueMingZ.Actions;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.GameState;
using JueMingZ.UI;

namespace JueMingZ.Diagnostics
{
    public static class DiagnosticActionRecorder
    {
        private static readonly object SyncRoot = new object();
        private static readonly object WriteQueueSyncRoot = new object();
        private static readonly System.Collections.Generic.Queue<PendingActionEventWrite> PendingWrites =
            new System.Collections.Generic.Queue<PendingActionEventWrite>();
        private const int MaxPendingWrites = 2048;
        private static string _testRunId = "uninitialized";
        private static DateTime _lastWriteFailureUtc = DateTime.MinValue;
        private static DateTime? _lastActionEventWrittenAtUtc;
        private static bool _writeWorkerScheduled;

        public static string TestRunId { get { lock (SyncRoot) { return _testRunId; } } }
        public static DateTime? LastActionEventWrittenAtUtc { get { lock (SyncRoot) { return _lastActionEventWrittenAtUtc; } } }

        public static string ActionEventsPath
        {
            get
            {
                return Path.Combine(
                    DiagnosticSnapshotWriter.DiagnosticsDirectory,
                    "action-events-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".jsonl");
            }
        }

        public static void Initialize(string testRunId)
        {
            lock (SyncRoot)
            {
                _testRunId = string.IsNullOrWhiteSpace(testRunId) ? "uninitialized" : testRunId;
            }
        }

        public static void RecordQueueResult(InputActionResult result)
        {
            if (result == null || result.ActionEventRecorded)
            {
                return;
            }

            RecordCustomEvent(
                result.RequestId,
                string.IsNullOrWhiteSpace(result.Scenario) ? result.Kind.ToString() : result.Scenario,
                result.Kind.ToString(),
                result.SourceHotkey,
                result.Status.ToString(),
                string.IsNullOrWhiteSpace(result.ResultCode) ? InputActionResult.MapResultCode(result.Status) : result.ResultCode,
                result.Message,
                result.DurationMs,
                "{}",
                "{}",
                "{}",
                result.SourceKind,
                result.SourceUi,
                result.ButtonId,
                result.ButtonLabel);
        }

        public static void RecordHotkeyEvent(string hotkey, string scenario, DiagnosticResultCode resultCode, string message)
        {
            RecordCustomEvent(
                Guid.Empty,
                scenario,
                "Diagnostic",
                hotkey,
                resultCode.ToString(),
                resultCode.ToString(),
                message,
                0,
                "{}",
                "{}",
                "{}",
                "Hotkey",
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static void RecordActionEvent(
            Guid requestId,
            string scenario,
            string actionKind,
            string sourceHotkey,
            string result,
            string resultCode,
            string message,
            long durationMs,
            ItemUseVerificationState before,
            ItemUseVerificationState after,
            string verificationJson,
            string sourceKind = "",
            string sourceUi = "",
            string buttonId = "",
            string buttonLabel = "")
        {
            RecordCustomEvent(
                requestId,
                scenario,
                actionKind,
                sourceHotkey,
                result,
                resultCode,
                message,
                durationMs,
                BuildStateJson(before),
                BuildStateJson(after),
                string.IsNullOrWhiteSpace(verificationJson) ? "{}" : verificationJson,
                sourceKind,
                sourceUi,
                buttonId,
                buttonLabel);
        }

        public static void RecordCustomEvent(
            Guid requestId,
            string scenario,
            string actionKind,
            string sourceHotkey,
            string result,
            string resultCode,
            string message,
            long durationMs,
            string beforeJson,
            string afterJson,
            string verificationJson,
            string sourceKind = "",
            string sourceUi = "",
            string buttonId = "",
            string buttonLabel = "")
        {
            // Action events describe lifecycle edges only; callers must not record every Running tick.
            try
            {
                var line = BuildEventJson(
                    requestId,
                    scenario,
                    actionKind,
                    sourceHotkey,
                    result,
                    resultCode,
                    message,
                    durationMs,
                    beforeJson,
                    afterJson,
                    verificationJson,
                    sourceKind,
                    sourceUi,
                    buttonId,
                    buttonLabel);

                EnqueueWrite(ActionEventsPath, line);
            }
            catch (Exception error)
            {
                if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                {
                    _lastWriteFailureUtc = DateTime.UtcNow;
                    Logger.Warn("DiagnosticActionRecorder", "Action event write failed: " + error.Message);
                }
            }
        }

        private static void EnqueueWrite(string path, string line)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(line))
            {
                return;
            }

            // Keep disk IO off gameplay paths and bound the queue if diagnostics briefly outpace writes.
            lock (WriteQueueSyncRoot)
            {
                if (PendingWrites.Count >= MaxPendingWrites)
                {
                    PendingWrites.Dequeue();
                }

                PendingWrites.Enqueue(new PendingActionEventWrite(path, line));
                if (_writeWorkerScheduled)
                {
                    return;
                }

                _writeWorkerScheduled = true;
                ThreadPool.QueueUserWorkItem(FlushPendingWrites);
            }
        }

        private static void FlushPendingWrites(object ignored)
        {
            while (true)
            {
                PendingActionEventWrite[] batch;
                lock (WriteQueueSyncRoot)
                {
                    if (PendingWrites.Count == 0)
                    {
                        _writeWorkerScheduled = false;
                        return;
                    }

                    batch = PendingWrites.ToArray();
                    PendingWrites.Clear();
                }

                try
                {
                    WriteBatch(batch);
                }
                catch (Exception error)
                {
                    if (DateTime.UtcNow - _lastWriteFailureUtc > TimeSpan.FromSeconds(30))
                    {
                        _lastWriteFailureUtc = DateTime.UtcNow;
                        Logger.Warn("DiagnosticActionRecorder", "Action event async write failed: " + error.Message);
                    }
                }
            }
        }

        private static void WriteBatch(PendingActionEventWrite[] batch)
        {
            if (batch == null || batch.Length == 0)
            {
                return;
            }

            Directory.CreateDirectory(DiagnosticSnapshotWriter.DiagnosticsDirectory);
            var index = 0;
            while (index < batch.Length)
            {
                var path = batch[index].Path;
                var builder = new StringBuilder();
                do
                {
                    builder.AppendLine(batch[index].Line);
                    index++;
                }
                while (index < batch.Length && string.Equals(path, batch[index].Path, StringComparison.OrdinalIgnoreCase));

                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(builder.ToString());
                }

                lock (SyncRoot)
                {
                    _lastActionEventWrittenAtUtc = DateTime.UtcNow;
                }
            }
        }

        public static string BuildMouseStateJson(int mouseX, int mouseY, bool playerInputCaptured, int playerInputX, int playerInputY, bool tileCaptured, int tileX, int tileY)
        {
            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "mouseX", mouseX.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "mouseY", mouseY.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "playerInputMouseCaptured", playerInputCaptured ? "true" : "false", true);
            AppendRaw(builder, "playerInputMouseX", playerInputX.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "playerInputMouseY", playerInputY.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "tileTargetCaptured", tileCaptured ? "true" : "false", true);
            AppendRaw(builder, "tileTargetX", tileX.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "tileTargetY", tileY.ToString(CultureInfo.InvariantCulture), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildEventJson(
            Guid requestId,
            string scenario,
            string actionKind,
            string sourceHotkey,
            string result,
            string resultCode,
            string message,
            long durationMs,
            string beforeJson,
            string afterJson,
            string verificationJson,
            string sourceKind,
            string sourceUi,
            string buttonId,
            string buttonLabel)
        {
            var safeSourceKind = string.IsNullOrWhiteSpace(sourceKind)
                ? (string.IsNullOrWhiteSpace(sourceHotkey) ? "Unknown" : "Hotkey")
                : sourceKind;
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "time", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), true);
            AppendString(builder, "testRunId", TestRunId, true);
            AppendString(builder, "requestId", requestId == Guid.Empty ? string.Empty : requestId.ToString(), true);
            AppendString(builder, "scenario", scenario ?? string.Empty, true);
            AppendString(builder, "actionKind", actionKind ?? string.Empty, true);
            AppendString(builder, "sourceHotkey", sourceHotkey ?? string.Empty, true);
            AppendString(builder, "result", result ?? string.Empty, true);
            AppendString(builder, "resultCode", resultCode ?? string.Empty, true);
            AppendString(builder, "message", message ?? string.Empty, true);
            AppendRaw(builder, "durationMs", durationMs.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "source", BuildSourceJson(safeSourceKind, sourceUi, buttonId, buttonLabel, sourceHotkey), true);
            AppendRaw(builder, "environment", BuildEnvironmentJson(), true);
            AppendRaw(builder, "hooks", BuildHooksJson(), true);
            AppendRaw(builder, "before", string.IsNullOrWhiteSpace(beforeJson) ? "{}" : beforeJson, true);
            AppendRaw(builder, "after", string.IsNullOrWhiteSpace(afterJson) ? "{}" : afterJson, true);
            AppendRaw(builder, "verification", string.IsNullOrWhiteSpace(verificationJson) ? "{}" : verificationJson, false);
            builder.Append("}");
            return builder.ToString();
        }

        private struct PendingActionEventWrite
        {
            public PendingActionEventWrite(string path, string line)
            {
                Path = path;
                Line = line;
            }

            public readonly string Path;
            public readonly string Line;
        }

        private static string BuildHooksJson()
        {
            return "{" +
                   "\"runtimeUpdateHookInstalled\":" + (HookDiagnostics.HookUpdateInstalled ? "true" : "false") + "," +
                   "\"interfaceLayerHookInstalled\":" + (HookDiagnostics.InterfaceLayerHookInstalled ? "true" : "false") + "," +
                   "\"itemCheckHookInstalled\":" + (HookDiagnostics.ItemCheckHookInstalled ? "true" : "false") + "," +
                   "\"playerDeathHookInstalled\":" + (HookDiagnostics.PlayerDeathHookInstalled ? "true" : "false") + "," +
                   "\"playerDeathMarkerLayerInstalled\":" + (PlayerWorldDeathMarkerDiagnostics.GetSnapshot().LayerInstalled ? "true" : "false") + "," +
                   "\"teleportRodHookInstalled\":" + (HookDiagnostics.TeleportRodHookInstalled ? "true" : "false") +
                   "}";
        }

        private static string BuildSourceJson(string sourceKind, string sourceUi, string buttonId, string buttonLabel, string hotkey)
        {
            var isButton = string.Equals(sourceKind, "Button", StringComparison.OrdinalIgnoreCase);
            var isUi = isButton || string.Equals(sourceKind, "UI", StringComparison.OrdinalIgnoreCase);
            var builder = new StringBuilder();
            builder.Append("{");
            AppendString(builder, "kind", sourceKind ?? string.Empty, true);
            AppendString(builder, "ui", sourceUi ?? string.Empty, true);
            AppendString(builder, "sourceUi", sourceUi ?? string.Empty, true);
            AppendString(builder, "buttonId", buttonId ?? string.Empty, true);
            AppendString(builder, "buttonLabel", buttonLabel ?? string.Empty, true);
            AppendString(builder, "uiWindow", isUi ? DiagnosticInteractionDiagnostics.LastUiWindow : string.Empty, true);
            AppendString(builder, "uiElementId", isUi ? DiagnosticInteractionDiagnostics.LastUiElementId : string.Empty, true);
            AppendRaw(builder, "mouseCaptured", isUi ? BoolRaw(DiagnosticInteractionDiagnostics.LastMouseCaptured || DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded) : "false", true);
            AppendString(builder, "hitTestMode", isUi ? DiagnosticInteractionDiagnostics.LastButtonHitTestMode : string.Empty, true);
            AppendRaw(builder, "hitTestConflict", isUi ? BoolRaw(DiagnosticInteractionDiagnostics.HitTestConflict) : "false", true);
            AppendString(builder, "candidateHits", isUi ? DiagnosticInteractionDiagnostics.HitTestCandidateSummary : string.Empty, true);
            AppendRaw(builder, "visualRect", isUi ? BuildRectJson(DiagnosticInteractionDiagnostics.HoveredButtonVisualX, DiagnosticInteractionDiagnostics.HoveredButtonVisualY, DiagnosticInteractionDiagnostics.HoveredButtonVisualWidth, DiagnosticInteractionDiagnostics.HoveredButtonVisualHeight) : "null", true);
            AppendRaw(builder, "hitRect", isUi ? BuildRectJson(DiagnosticInteractionDiagnostics.HoveredButtonHitX, DiagnosticInteractionDiagnostics.HoveredButtonHitY, DiagnosticInteractionDiagnostics.HoveredButtonHitWidth, DiagnosticInteractionDiagnostics.HoveredButtonHitHeight) : "null", true);
            AppendString(builder, "clickSource", isUi ? DiagnosticInteractionDiagnostics.LastButtonClickSource : string.Empty, true);
            AppendRaw(builder, "terrariaMouseX", isUi ? IntRaw(DiagnosticInteractionDiagnostics.TerrariaMouseX) : "null", true);
            AppendRaw(builder, "terrariaMouseY", isUi ? IntRaw(DiagnosticInteractionDiagnostics.TerrariaMouseY) : "null", true);
            AppendRaw(builder, "osClientMouseX", isUi ? IntRaw(DiagnosticInteractionDiagnostics.OsClientMouseX) : "null", true);
            AppendRaw(builder, "osClientMouseY", isUi ? IntRaw(DiagnosticInteractionDiagnostics.OsClientMouseY) : "null", true);
            AppendString(builder, "hotkey", hotkey ?? string.Empty, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildStateJson(ItemUseVerificationState state)
        {
            if (state == null)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "selectedSlot", state.SelectedSlot.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemType", state.ItemType.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "itemName", state.ItemName ?? string.Empty, true);
            AppendRaw(builder, "itemStack", state.ItemStack.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "life", state.Life.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "mana", state.Mana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffCount", state.ActiveBuffCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffTypes", string.IsNullOrWhiteSpace(state.BuffTypesJson) ? "[]" : state.BuffTypesJson, true);
            AppendRaw(builder, "buffTimeTotal", state.BuffTimeTotal.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemAnimation", state.ItemAnimation.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "itemTime", state.ItemTime.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "reuseDelay", state.ReuseDelay.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "playerActive", state.PlayerActive ? "true" : "false", true);
            AppendRaw(builder, "playerDead", state.PlayerDead ? "true" : "false", true);
            AppendRaw(builder, "playerGhost", state.PlayerGhost ? "true" : "false", true);
            AppendRaw(builder, "selectedSlotDisplay", SlotDisplayRaw(state.SelectedSlot), true);
            AppendRaw(builder, "lifeMax", state.LifeMax.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "manaMax", state.ManaMax.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "useStyle", state.UseStyle.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "consumable", state.Consumable ? "true" : "false", true);
            AppendRaw(builder, "healLife", state.HealLife.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "healMana", state.HealMana.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffType", state.BuffType.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "buffTime", state.BuffTime.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "createTile", state.CreateTile.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "createWall", state.CreateWall.ToString(CultureInfo.InvariantCulture), false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BuildEnvironmentJson()
        {
            var snapshot = GameStateReader.LastSnapshot;
            var player = snapshot == null ? null : snapshot.Player;
            var inventory = snapshot == null ? null : snapshot.Inventory;
            var ui = snapshot == null ? null : snapshot.Ui;
            var selectedSlot = inventory == null ? -1 : inventory.SelectedItemSlot;
            var diagnosticSlot = ConfigService.AppSettings == null ? 0 : ConfigService.AppSettings.DiagnosticInputTestSlot;

            var builder = new StringBuilder();
            builder.Append("{");
            AppendRaw(builder, "inMainMenu", BoolRaw(snapshot != null && snapshot.IsInMainMenu), true);
            AppendRaw(builder, "inWorld", BoolRaw(snapshot != null && snapshot.IsInWorld), true);
            AppendRaw(builder, "overlayVisible", BoolRaw(DiagnosticsOverlay.Visible), true);
            AppendRaw(builder, "diagnosticInputTests", BoolRaw(ConfigService.AppSettings != null && ConfigService.AppSettings.EnableDiagnosticInputTests), true);
            AppendRaw(builder, "isMultiplayer", BoolRaw(snapshot != null && snapshot.NetMode != 0), true);
            AppendRaw(builder, "playerActive", BoolRaw(player != null && player.Active), true);
            AppendRaw(builder, "playerDead", BoolRaw(player != null && player.Dead), true);
            AppendRaw(builder, "playerGhost", BoolRaw(player != null && player.Ghost), true);
            AppendRaw(builder, "chatOpen", BoolRaw(ui != null && ui.ChatOpen), true);
            AppendRaw(builder, "inventoryOpen", BoolRaw(ui != null && ui.PlayerInventoryOpen), true);
            AppendRaw(builder, "chestOpen", BoolRaw(ui != null && ui.ChestOpen), true);
            AppendRaw(builder, "npcChatOpen", BoolRaw(ui != null && ui.NpcChatOpen), true);
            AppendRaw(builder, "playerLife", IntRaw(player == null ? 0 : player.Life), true);
            AppendRaw(builder, "playerLifeMax", IntRaw(player == null ? 0 : player.LifeMax), true);
            AppendRaw(builder, "playerMana", IntRaw(player == null ? 0 : player.Mana), true);
            AppendRaw(builder, "playerManaMax", IntRaw(player == null ? 0 : player.ManaMax), true);
            AppendRaw(builder, "selectedSlot", SlotRaw(selectedSlot), true);
            AppendRaw(builder, "selectedSlotDisplay", SlotDisplayRaw(selectedSlot), true);
            AppendRaw(builder, "diagnosticTestSlot", SlotRaw(diagnosticSlot), true);
            AppendRaw(builder, "diagnosticTestSlotDisplay", SlotDisplayRaw(diagnosticSlot), true);
            AppendRaw(builder, "lastMouseX", IntRaw(DiagnosticInteractionDiagnostics.LastMouseX), true);
            AppendRaw(builder, "lastMouseY", IntRaw(DiagnosticInteractionDiagnostics.LastMouseY), true);
            AppendRaw(builder, "terrariaMouseX", IntRaw(DiagnosticInteractionDiagnostics.TerrariaMouseX), true);
            AppendRaw(builder, "terrariaMouseY", IntRaw(DiagnosticInteractionDiagnostics.TerrariaMouseY), true);
            AppendRaw(builder, "terrariaLeftDown", BoolRaw(DiagnosticInteractionDiagnostics.TerrariaLeftDown), true);
            AppendRaw(builder, "terrariaLeftReleaseAvailable", BoolRaw(DiagnosticInteractionDiagnostics.TerrariaLeftReleaseAvailable), true);
            AppendRaw(builder, "terrariaLeftRelease", BoolRaw(DiagnosticInteractionDiagnostics.TerrariaLeftRelease), true);
            AppendRaw(builder, "osClientMouseX", IntRaw(DiagnosticInteractionDiagnostics.OsClientMouseX), true);
            AppendRaw(builder, "osClientMouseY", IntRaw(DiagnosticInteractionDiagnostics.OsClientMouseY), true);
            AppendRaw(builder, "osLeftDown", BoolRaw(DiagnosticInteractionDiagnostics.OsLeftDown), true);
            AppendRaw(builder, "uiScale", DiagnosticInteractionDiagnostics.UiScale.ToString("0.###", CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "uiScaleAvailable", BoolRaw(DiagnosticInteractionDiagnostics.UiScaleAvailable), true);
            AppendRaw(builder, "uiScaleMatrixAvailable", BoolRaw(DiagnosticInteractionDiagnostics.UiScaleMatrixAvailable), true);
            AppendString(builder, "mouseReadMode", DiagnosticInteractionDiagnostics.MouseReadMode, true);
            AppendString(builder, "mouseReadLastError", DiagnosticInteractionDiagnostics.MouseReadLastError, true);
            AppendString(builder, "hitTestMode", DiagnosticInteractionDiagnostics.HitTestMode, true);
            AppendRaw(builder, "hitTestX", IntRaw(DiagnosticInteractionDiagnostics.HitTestX), true);
            AppendRaw(builder, "hitTestY", IntRaw(DiagnosticInteractionDiagnostics.HitTestY), true);
            AppendRaw(builder, "hitTestConflict", BoolRaw(DiagnosticInteractionDiagnostics.HitTestConflict), true);
            AppendString(builder, "candidateHits", DiagnosticInteractionDiagnostics.HitTestCandidateSummary, true);
            AppendString(builder, "clickSource", DiagnosticInteractionDiagnostics.ClickSource, true);
            AppendString(builder, "hoveredButtonId", DiagnosticInteractionDiagnostics.HoveredButtonId, true);
            AppendString(builder, "hoveredButtonLabel", DiagnosticInteractionDiagnostics.HoveredButtonLabel, true);
            AppendString(builder, "hoveredButtonHint", DiagnosticInteractionDiagnostics.HoveredButtonHint, true);
            AppendRaw(builder, "hoveredButtonEnabled", BoolRaw(DiagnosticInteractionDiagnostics.HoveredButtonEnabled), true);
            AppendRaw(builder, "hoveredButtonVisualRect", BuildRectJson(DiagnosticInteractionDiagnostics.HoveredButtonVisualX, DiagnosticInteractionDiagnostics.HoveredButtonVisualY, DiagnosticInteractionDiagnostics.HoveredButtonVisualWidth, DiagnosticInteractionDiagnostics.HoveredButtonVisualHeight), true);
            AppendRaw(builder, "hoveredButtonHitRect", BuildRectJson(DiagnosticInteractionDiagnostics.HoveredButtonHitX, DiagnosticInteractionDiagnostics.HoveredButtonHitY, DiagnosticInteractionDiagnostics.HoveredButtonHitWidth, DiagnosticInteractionDiagnostics.HoveredButtonHitHeight), true);
            AppendRaw(builder, "uiMouseReadAvailable", BoolRaw(!string.Equals(DiagnosticInteractionDiagnostics.MouseReadMode, "none", StringComparison.OrdinalIgnoreCase)), true);
            AppendString(builder, "uiMouseReadLastMessage", string.IsNullOrWhiteSpace(DiagnosticInteractionDiagnostics.MouseReadLastError) ? DiagnosticInteractionDiagnostics.MouseReadMode : DiagnosticInteractionDiagnostics.MouseReadLastError, true);
            AppendRaw(builder, "uiMouseCaptureAvailable", BoolRaw(TerrariaUiMouseCompat.UiMouseCaptureAvailable), true);
            AppendString(builder, "uiMouseCaptureLastMessage", TerrariaUiMouseCompat.UiMouseCaptureLastMessage, true);
            AppendRaw(builder, "uiClickSuppressionAttempted", BoolRaw(DiagnosticInteractionDiagnostics.UiClickSuppressionAttempted), true);
            AppendString(builder, "uiClickSuppressionMode", DiagnosticInteractionDiagnostics.UiClickSuppressionMode, true);
            AppendRaw(builder, "uiClickSuppressionSucceeded", BoolRaw(DiagnosticInteractionDiagnostics.UiClickSuppressionSucceeded), true);
            AppendRaw(builder, "uiPrimitiveRendererReady", BoolRaw(UiPrimitiveRenderer.Ready), true);
            AppendString(builder, "uiPrimitiveRendererLastMessage", UiPrimitiveRenderer.LastError, false);
            builder.Append("}");
            return builder.ToString();
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string SlotRaw(int slot)
        {
            return slot >= 0 && slot <= 9 ? slot.ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string SlotDisplayRaw(int slot)
        {
            return slot >= 0 && slot <= 9 ? (slot + 1).ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string BuildRectJson(int x, int y, int width, int height)
        {
            return "{" +
                   "\"x\":" + IntRaw(x) + "," +
                   "\"y\":" + IntRaw(y) + "," +
                   "\"width\":" + IntRaw(width) + "," +
                   "\"height\":" + IntRaw(height) +
                   "}";
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":\"").Append(Escape(value ?? string.Empty)).Append("\"");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static void AppendRaw(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("\"").Append(Escape(name)).Append("\":").Append(value ?? "null");
            if (comma)
            {
                builder.Append(",");
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
