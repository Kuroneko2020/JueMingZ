using System;
using System.Collections;
using System.Globalization;
using System.Text;
using JueMingZ.Automation.Blueprint;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class BlueprintAutoPlaceActionExecutor : InputActionExecutorBase
    {
        private const string StateBridgeQueued = "BlueprintAutoPlaceBridgeQueued";
        private static IBlueprintAutoPlaceExecutionDriver _testingDriver;

        public override InputActionKind Kind { get { return InputActionKind.BlueprintAutoPlace; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var candidate = BuildCandidateFromMetadata(execution);
            if (IsBlockedForWorldInput(snapshot))
            {
                return Finish(
                    execution,
                    candidate,
                    null,
                    null,
                    null,
                    InputActionStatus.BlockedByUi,
                    DiagnosticResultCode.BlockedByUi,
                    "failClosed",
                    "蓝图自动摆放未执行：当前不在世界内，或 UI 正在阻挡世界输入。");
            }

            string supportReason;
            if (!BlueprintAutoPlacementService.IsStage15SupportedCandidate(candidate, out supportReason))
            {
                return Finish(
                    execution,
                    candidate,
                    null,
                    null,
                    null,
                    InputActionStatus.NotApplicable,
                    DiagnosticResultCode.NotApplicable,
                    "failClosed",
                    "蓝图自动摆放跳过：当前层不属于 15 阶段保守摆放范围（" + supportReason + "）。");
            }

            var driver = ResolveDriver();
            BlueprintProjectionCellSnapshot currentLayer;
            string currentMessage;
            if (!TryResolveCurrentLayer(driver.ForceRefreshProjection(), candidate, out currentLayer, out currentMessage))
            {
                return Finish(execution, candidate, null, null, null, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "failClosed", currentMessage);
            }

            if (string.Equals(currentLayer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
            {
                return Finish(execution, candidate, null, null, currentLayer, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "failClosed", "蓝图自动摆放跳过：目标层已完成。");
            }

            if (!string.Equals(currentLayer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
            {
                return Finish(execution, candidate, null, null, currentLayer, InputActionStatus.NotApplicable, DiagnosticResultCode.NotApplicable, "failClosed", "蓝图自动摆放跳过：目标层当前状态不是 missing，而是 " + currentLayer.Status + "。");
            }

            BlueprintAutoPlaceUsePlan plan;
            DiagnosticResultCode failureCode;
            string message;
            if (!driver.TryBeginUse(execution, candidate, out plan, out failureCode, out message))
            {
                return Finish(
                    execution,
                    candidate,
                    plan,
                    null,
                    currentLayer,
                    failureCode == DiagnosticResultCode.MissingRequiredItem ? InputActionStatus.NotApplicable : InputActionStatus.Failed,
                    failureCode,
                    "failClosed",
                    message);
            }

            SavePlan(execution, plan);
            SetResultCode(execution, DiagnosticResultCode.Queued);
            return InputActionExecutionStepResult.Running("蓝图自动摆放已交给 ItemUseBridge，等待 Player.ItemCheck 消费。");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (!string.Equals(GetExecutionStateString(execution, StateBridgeQueued, string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return InputActionExecutionStepResult.Running("蓝图自动摆放等待 bridge 初始化。");
            }

            var driver = ResolveDriver();
            var result = driver.GetResult(execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId);
            if (result.Status == ItemUseBridgeStatus.WaitingForItemCheck ||
                result.Status == ItemUseBridgeStatus.Consumed ||
                result.Status == ItemUseBridgeStatus.None)
            {
                return InputActionExecutionStepResult.Running(string.IsNullOrWhiteSpace(result.Message) ? "蓝图自动摆放等待 ItemCheck。" : result.Message);
            }

            driver.ReleaseUseItem();
            var candidate = BuildCandidateFromMetadata(execution);
            var plan = LoadPlan(execution);
            BlueprintProjectionCellSnapshot currentLayer;
            string currentMessage;
            var hasLayer = TryResolveCurrentLayer(driver.ForceRefreshProjection(), candidate, out currentLayer, out currentMessage);
            var fulfilled = hasLayer && string.Equals(currentLayer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal);

            if (fulfilled)
            {
                return Finish(
                    execution,
                    candidate,
                    plan,
                    result,
                    currentLayer,
                    InputActionStatus.Succeeded,
                    DiagnosticResultCode.Succeeded,
                    "succeeded",
                    "蓝图自动摆放已通过受控 ItemCheck 摆放，并由投影复验确认。");
            }

            if (result.Status == ItemUseBridgeStatus.Expired)
            {
                return Finish(execution, candidate, plan, result, currentLayer, InputActionStatus.TimedOut, DiagnosticResultCode.TimedOut, "failClosed", result.Message);
            }

            if (result.Status == ItemUseBridgeStatus.Failed || result.Status == ItemUseBridgeStatus.Cancelled)
            {
                return Finish(execution, candidate, plan, result, currentLayer, result.Status == ItemUseBridgeStatus.Cancelled ? InputActionStatus.Cancelled : InputActionStatus.Failed, DiagnosticResultCode.Failed, "failClosed", result.Message);
            }

            return Finish(
                execution,
                candidate,
                plan,
                result,
                currentLayer,
                InputActionStatus.AttemptedButUnverified,
                DiagnosticResultCode.AttemptedButUnverified,
                "attemptedButUnverified",
                string.IsNullOrWhiteSpace(currentMessage)
                    ? "蓝图自动摆放已尝试 ItemCheck，但投影复验未确认目标层完成。"
                    : currentMessage);
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            var driver = ResolveDriver();
            var requestId = execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId;
            driver.Cancel(requestId, reason ?? "Blueprint auto placement cancelled.");
            driver.ReleaseUseItem();
            var candidate = BuildCandidateFromMetadata(execution);
            var plan = LoadPlan(execution);
            return Finish(execution, candidate, plan, null, null, InputActionStatus.Cancelled, DiagnosticResultCode.Failed, "failClosed", reason ?? "蓝图自动摆放已取消。");
        }

        internal static void SetExecutionDriverForTesting(IBlueprintAutoPlaceExecutionDriver driver)
        {
            _testingDriver = driver;
        }

        internal static string GetMetadataStringForDriver(InputActionExecution execution, string key, string fallback)
        {
            return GetMetadataString(execution, key, fallback);
        }

        private static IBlueprintAutoPlaceExecutionDriver ResolveDriver()
        {
            return _testingDriver ?? BlueprintAutoPlaceTerrariaExecutionDriver.Instance;
        }

        private static InputActionExecutionStepResult Finish(
            InputActionExecution execution,
            BlueprintAutoPlacementCandidate candidate,
            BlueprintAutoPlaceUsePlan plan,
            ItemUseBridgeResult bridgeResult,
            BlueprintProjectionCellSnapshot currentLayer,
            InputActionStatus status,
            DiagnosticResultCode code,
            string serviceResultCode,
            string message)
        {
            SetResultCode(execution, code);
            RecordActionEvent(execution, candidate, plan, bridgeResult, currentLayer, status, code, message);
            MarkActionEventRecorded(execution);
            BlueprintAutoPlacementService.RecordExecutorResult(execution, code, serviceResultCode, message);
            return InputActionExecutionStepResult.Complete(status, message);
        }

        private static void RecordActionEvent(
            InputActionExecution execution,
            BlueprintAutoPlacementCandidate candidate,
            BlueprintAutoPlaceUsePlan plan,
            ItemUseBridgeResult bridgeResult,
            BlueprintProjectionCellSnapshot currentLayer,
            InputActionStatus status,
            DiagnosticResultCode code,
            string message)
        {
            var requestId = execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId;
            var durationMs = execution == null || execution.StartedUtc == DateTime.MinValue
                ? 0
                : (long)(DateTime.UtcNow - execution.StartedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                requestId,
                ScenarioNames.BlueprintAutoPlace,
                InputActionKind.BlueprintAutoPlace.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                durationMs,
                BuildBeforeJson(candidate, plan),
                BuildAfterJson(bridgeResult, currentLayer),
                BuildVerificationJson(candidate, plan, bridgeResult, currentLayer),
                GetMetadataString(execution, ActionMetadataKeys.SourceKind, "Automation"),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));
        }

        private static bool TryResolveCurrentLayer(
            BlueprintProjectionSnapshot projection,
            BlueprintAutoPlacementCandidate candidate,
            out BlueprintProjectionCellSnapshot layer,
            out string message)
        {
            layer = null;
            message = string.Empty;
            if (candidate == null)
            {
                message = "蓝图自动摆放跳过：请求缺少候选 metadata。";
                return false;
            }

            if (projection == null || !projection.LoadSucceeded)
            {
                message = projection == null ? "蓝图自动摆放跳过：投影不可用。" : "蓝图自动摆放跳过：投影不可用：" + projection.Message;
                return false;
            }

            var layers = projection.AllProjectedLayers;
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var current = layers[index];
                if (MatchesCandidate(current, candidate))
                {
                    layer = current;
                    return true;
                }
            }

            message = "蓝图自动摆放跳过：投影中已找不到目标层。";
            return false;
        }

        private static bool MatchesCandidate(BlueprintProjectionCellSnapshot layer, BlueprintAutoPlacementCandidate candidate)
        {
            return layer != null &&
                   candidate != null &&
                   string.Equals(layer.InstanceId ?? string.Empty, candidate.InstanceId ?? string.Empty, StringComparison.Ordinal) &&
                   layer.LayerOrder == candidate.LayerOrder &&
                   layer.WorldTileX == candidate.WorldTileX &&
                   layer.WorldTileY == candidate.WorldTileY &&
                   string.Equals(layer.LayerKind ?? string.Empty, candidate.LayerKind ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   layer.ContentId == candidate.ContentId &&
                   layer.Style == candidate.Style;
        }

        private static BlueprintAutoPlacementCandidate BuildCandidateFromMetadata(InputActionExecution execution)
        {
            return new BlueprintAutoPlacementCandidate
            {
                InstanceId = GetMetadataString(execution, ActionMetadataKeys.BlueprintInstanceId, string.Empty),
                InstanceName = GetMetadataString(execution, ActionMetadataKeys.BlueprintInstanceName, string.Empty),
                LayerOrder = GetMetadataInt(execution, ActionMetadataKeys.BlueprintLayerOrder, 0),
                WorldTileX = GetMetadataInt(execution, ActionMetadataKeys.WorldX, 0),
                WorldTileY = GetMetadataInt(execution, ActionMetadataKeys.WorldY, 0),
                RelativeX = GetMetadataInt(execution, ActionMetadataKeys.BlueprintRelativeX, 0),
                RelativeY = GetMetadataInt(execution, ActionMetadataKeys.BlueprintRelativeY, 0),
                LayerKind = GetMetadataString(execution, ActionMetadataKeys.BlueprintLayerKind, string.Empty),
                ContentId = GetMetadataInt(execution, ActionMetadataKeys.BlueprintContentId, 0),
                Style = GetMetadataInt(execution, ActionMetadataKeys.BlueprintStyle, 0),
                FrameX = GetMetadataInt(execution, ActionMetadataKeys.BlueprintFrameX, 0),
                FrameY = GetMetadataInt(execution, ActionMetadataKeys.BlueprintFrameY, 0),
                PaintId = GetMetadataInt(execution, ActionMetadataKeys.BlueprintPaintId, 0),
                CoatingFlags = GetMetadataInt(execution, ActionMetadataKeys.BlueprintCoatingFlags, 0),
                Slope = GetMetadataInt(execution, ActionMetadataKeys.BlueprintSlope, 0),
                HalfBrick = GetMetadataBool(execution, ActionMetadataKeys.BlueprintHalfBrick),
                Inactive = GetMetadataBool(execution, ActionMetadataKeys.BlueprintInactive),
                MaterialItemId = GetMetadataInt(execution, ActionMetadataKeys.BlueprintMaterialItemId, 0),
                OriginalMaterialItemId = GetMetadataInt(execution, ActionMetadataKeys.BlueprintOriginalMaterialItemId, 0),
                MaterialStack = GetMetadataInt(execution, ActionMetadataKeys.BlueprintMaterialStack, 0),
                MaterialAvailableStack = GetMetadataInt(execution, ActionMetadataKeys.BlueprintMaterialAvailableStack, 0),
                MaterialExecutionScope = GetMetadataString(execution, ActionMetadataKeys.BlueprintMaterialExecutionScope, string.Empty),
                ReplacementApplied = GetMetadataBool(execution, ActionMetadataKeys.BlueprintReplacementApplied),
                ReplacementCategory = GetMetadataString(execution, ActionMetadataKeys.BlueprintReplacementCategory, string.Empty),
                AdmissionKey = GetMetadataString(execution, ActionMetadataKeys.BlueprintAdmissionKey, string.Empty)
            };
        }

        private static bool GetMetadataBool(InputActionExecution execution, string key)
        {
            return string.Equals(GetMetadataString(execution, key, string.Empty), "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void SavePlan(InputActionExecution execution, BlueprintAutoPlaceUsePlan plan)
        {
            if (execution == null || execution.State == null || plan == null)
            {
                return;
            }

            execution.State[StateBridgeQueued] = "true";
            execution.State["BlueprintAutoPlaceMaterialSlot"] = plan.MaterialSlot.ToString(CultureInfo.InvariantCulture);
            execution.State["BlueprintAutoPlaceMaterialItemName"] = plan.MaterialItemName ?? string.Empty;
            execution.State["BlueprintAutoPlaceMaterialStack"] = plan.MaterialStack.ToString(CultureInfo.InvariantCulture);
            execution.State["BlueprintAutoPlaceOriginalSelectedSlot"] = plan.OriginalSelectedSlot.ToString(CultureInfo.InvariantCulture);
            execution.State["BlueprintAutoPlaceMouseWorldX"] = plan.MouseWorldX.ToString("0.###", CultureInfo.InvariantCulture);
            execution.State["BlueprintAutoPlaceMouseWorldY"] = plan.MouseWorldY.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static BlueprintAutoPlaceUsePlan LoadPlan(InputActionExecution execution)
        {
            if (execution == null || execution.State == null)
            {
                return null;
            }

            if (!string.Equals(GetExecutionStateString(execution, StateBridgeQueued, string.Empty), "true", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new BlueprintAutoPlaceUsePlan
            {
                MaterialSlot = GetExecutionStateInt(execution, "BlueprintAutoPlaceMaterialSlot", -1),
                MaterialItemName = GetExecutionStateString(execution, "BlueprintAutoPlaceMaterialItemName", string.Empty),
                MaterialStack = GetExecutionStateInt(execution, "BlueprintAutoPlaceMaterialStack", 0),
                OriginalSelectedSlot = GetExecutionStateInt(execution, "BlueprintAutoPlaceOriginalSelectedSlot", -1),
                MouseWorldX = GetExecutionStateFloat(execution, "BlueprintAutoPlaceMouseWorldX", 0f),
                MouseWorldY = GetExecutionStateFloat(execution, "BlueprintAutoPlaceMouseWorldY", 0f)
            };
        }

        private static string GetExecutionStateString(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null)
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) ? value ?? fallback : fallback;
        }

        private static int GetExecutionStateInt(InputActionExecution execution, string key, int fallback)
        {
            var raw = GetExecutionStateString(execution, key, string.Empty);
            int parsed;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static float GetExecutionStateFloat(InputActionExecution execution, string key, float fallback)
        {
            var raw = GetExecutionStateString(execution, key, string.Empty);
            float parsed;
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
        }

        private static string BuildBeforeJson(BlueprintAutoPlacementCandidate candidate, BlueprintAutoPlaceUsePlan plan)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendString(builder, "instanceId", candidate == null ? string.Empty : candidate.InstanceId, false);
            AppendString(builder, "layerKind", candidate == null ? string.Empty : candidate.LayerKind, true);
            AppendRaw(builder, "worldTileX", IntRaw(candidate == null ? 0 : candidate.WorldTileX), true);
            AppendRaw(builder, "worldTileY", IntRaw(candidate == null ? 0 : candidate.WorldTileY), true);
            AppendRaw(builder, "contentId", IntRaw(candidate == null ? 0 : candidate.ContentId), true);
            AppendRaw(builder, "style", IntRaw(candidate == null ? 0 : candidate.Style), true);
            AppendRaw(builder, "materialItemId", IntRaw(candidate == null ? 0 : candidate.MaterialItemId), true);
            AppendRaw(builder, "originalMaterialItemId", IntRaw(candidate == null ? 0 : candidate.OriginalMaterialItemId), true);
            AppendRaw(builder, "materialStack", IntRaw(candidate == null ? 0 : candidate.MaterialStack), true);
            AppendRaw(builder, "replacementApplied", BoolRaw(candidate != null && candidate.ReplacementApplied), true);
            AppendString(builder, "replacementCategory", candidate == null ? string.Empty : candidate.ReplacementCategory, true);
            AppendString(builder, "materialExecutionScope", candidate == null ? string.Empty : candidate.MaterialExecutionScope, true);
            AppendRaw(builder, "materialSlot", SlotRaw(plan == null ? -1 : plan.MaterialSlot), true);
            AppendString(builder, "materialItemName", plan == null ? string.Empty : plan.MaterialItemName, true);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildAfterJson(ItemUseBridgeResult bridgeResult, BlueprintProjectionCellSnapshot currentLayer)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendString(builder, "bridgeStatus", bridgeResult == null ? string.Empty : bridgeResult.Status.ToString(), false);
            AppendString(builder, "bridgeResultCode", bridgeResult == null ? string.Empty : bridgeResult.ResultCode, true);
            AppendString(builder, "projectionStatus", currentLayer == null ? string.Empty : currentLayer.Status, true);
            AppendRaw(builder, "projectionFulfilled", BoolRaw(currentLayer != null && string.Equals(currentLayer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal)), true);
            AppendRaw(builder, "itemCheckConsumed", BoolRaw(bridgeResult != null && bridgeResult.ConsumedByItemCheck), true);
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildVerificationJson(BlueprintAutoPlacementCandidate candidate, BlueprintAutoPlaceUsePlan plan, ItemUseBridgeResult bridgeResult, BlueprintProjectionCellSnapshot currentLayer)
        {
            var builder = new StringBuilder();
            builder.Append('{');
            AppendString(builder, "stage", "15", false);
            AppendRaw(builder, "contractOnly", "false", true);
            AppendRaw(builder, "directWorldMutationAttempted", "false", true);
            AppendRaw(builder, "inventoryMutationAttempted", "false", true);
            AppendRaw(builder, "networkPacketAttempted", "false", true);
            AppendRaw(builder, "vanillaItemUseAttempted", BoolRaw(bridgeResult != null && bridgeResult.Status != ItemUseBridgeStatus.None), true);
            AppendRaw(builder, "projectionVerified", BoolRaw(currentLayer != null && string.Equals(currentLayer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal)), true);
            AppendRaw(builder, "targetSlot", SlotRaw(plan == null ? -1 : plan.MaterialSlot), true);
            AppendRaw(builder, "originalSelectedSlot", SlotRaw(plan == null ? -1 : plan.OriginalSelectedSlot), true);
            AppendRaw(builder, "mouseWorldX", FloatRaw(plan == null ? (candidate == null ? 0f : candidate.WorldTileX * 16f + 8f) : plan.MouseWorldX), true);
            AppendRaw(builder, "mouseWorldY", FloatRaw(plan == null ? (candidate == null ? 0f : candidate.WorldTileY * 16f + 8f) : plan.MouseWorldY), true);
            AppendRaw(builder, "replacementApplied", BoolRaw(candidate != null && candidate.ReplacementApplied), true);
            AppendString(builder, "replacementCategory", candidate == null ? string.Empty : candidate.ReplacementCategory, true);
            AppendRaw(builder, "originalMaterialItemId", IntRaw(candidate == null ? 0 : candidate.OriginalMaterialItemId), true);
            AppendRaw(builder, "materialItemId", IntRaw(candidate == null ? 0 : candidate.MaterialItemId), true);
            AppendString(builder, "materialExecutionScope", candidate == null ? string.Empty : candidate.MaterialExecutionScope, true);
            AppendString(builder, "admissionKey", candidate == null ? string.Empty : candidate.AdmissionKey, true);
            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string name, string value, bool comma)
        {
            AppendComma(builder, comma);
            builder.Append('"').Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value)).Append('"');
        }

        private static void AppendRaw(StringBuilder builder, string name, string raw, bool comma)
        {
            AppendComma(builder, comma);
            builder.Append('"').Append(EscapeJson(name)).Append("\":").Append(string.IsNullOrWhiteSpace(raw) ? "null" : raw);
        }

        private static void AppendComma(StringBuilder builder, bool comma)
        {
            if (comma)
            {
                builder.Append(',');
            }
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FloatRaw(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string SlotRaw(int slot)
        {
            return TerrariaInputCompat.IsSupportedItemUseSlot(slot) ? slot.ToString(CultureInfo.InvariantCulture) : "null";
        }

        private static string EscapeJson(string value)
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

    internal interface IBlueprintAutoPlaceExecutionDriver
    {
        bool TryBeginUse(
            InputActionExecution execution,
            BlueprintAutoPlacementCandidate candidate,
            out BlueprintAutoPlaceUsePlan plan,
            out DiagnosticResultCode failureCode,
            out string message);

        ItemUseBridgeResult GetResult(Guid requestId);

        void Cancel(Guid requestId, string reason);

        void ReleaseUseItem();

        BlueprintProjectionSnapshot ForceRefreshProjection();
    }

    internal sealed class BlueprintAutoPlaceUsePlan
    {
        public int MaterialSlot { get; set; }
        public int MaterialStack { get; set; }
        public string MaterialItemName { get; set; }
        public int OriginalSelectedSlot { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }

        public BlueprintAutoPlaceUsePlan()
        {
            MaterialSlot = -1;
            MaterialItemName = string.Empty;
            OriginalSelectedSlot = -1;
        }
    }

    internal sealed class BlueprintAutoPlaceTerrariaExecutionDriver : IBlueprintAutoPlaceExecutionDriver
    {
        public static readonly BlueprintAutoPlaceTerrariaExecutionDriver Instance = new BlueprintAutoPlaceTerrariaExecutionDriver();

        private BlueprintAutoPlaceTerrariaExecutionDriver()
        {
        }

        public bool TryBeginUse(
            InputActionExecution execution,
            BlueprintAutoPlacementCandidate candidate,
            out BlueprintAutoPlaceUsePlan plan,
            out DiagnosticResultCode failureCode,
            out string message)
        {
            plan = null;
            failureCode = DiagnosticResultCode.Failed;
            message = string.Empty;
            if (execution == null || execution.Request == null)
            {
                message = "蓝图自动摆放无法开始：请求为空。";
                return false;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                failureCode = DiagnosticResultCode.BlockedByEnvironment;
                message = "蓝图自动摆放无法开始：local player 不可用：" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            int originalSelectedSlot;
            if (!TerrariaInputCompat.TryGetSelectedItem(player, out originalSelectedSlot))
            {
                failureCode = DiagnosticResultCode.BlockedByEnvironment;
                message = "蓝图自动摆放无法读取 selectedItem：" + TerrariaInputCompat.LastInputCompatError;
                return false;
            }

            int materialSlot;
            int materialStack;
            string materialName;
            if (!TryFindMainInventoryMaterial(player, candidate.MaterialItemId, Math.Max(1, candidate.MaterialStack), out materialSlot, out materialStack, out materialName, out message))
            {
                failureCode = DiagnosticResultCode.MissingRequiredItem;
                return false;
            }

            var mouseWorldX = candidate.WorldTileX * 16f + 8f;
            var mouseWorldY = candidate.WorldTileY * 16f + 8f;
            plan = new BlueprintAutoPlaceUsePlan
            {
                MaterialSlot = materialSlot,
                MaterialStack = materialStack,
                MaterialItemName = materialName ?? string.Empty,
                OriginalSelectedSlot = originalSelectedSlot,
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY
            };

            var options = new ItemUseBridgeOptions
            {
                SelectedSlotAtUseStart = originalSelectedSlot,
                SlotSwitchAttempted = originalSelectedSlot != materialSlot,
                SlotSwitchSucceeded = false,
                SlotSwitchMethod = "ItemCheckScopedSelection",
                SlotSwitchBefore = originalSelectedSlot,
                SlotSwitchAfter = materialSlot,
                SkipSelectInItemCheck = false,
                ApplyMainMouseLeftForItemCheck = true,
                AllowEarlyItemCheck = false,
                HasMouseWorldTarget = true,
                MouseWorldX = mouseWorldX,
                MouseWorldY = mouseWorldY,
                RestoreSelectedSlotOverride = originalSelectedSlot
            };

            if (!ItemUseBridge.TryEnqueueUseSelectedItem(
                execution.Request.RequestId,
                execution.Request.SourceFeatureId,
                materialSlot,
                candidate.MaterialItemId,
                materialStack,
                materialName,
                execution.Request.Timeout,
                originalSelectedSlot,
                InputActionKind.BlueprintAutoPlace,
                ScenarioNames.BlueprintAutoPlace,
                BlueprintAutoPlaceActionExecutor.GetMetadataStringForDriver(execution, "SourceHotkey", string.Empty),
                BlueprintAutoPlaceActionExecutor.GetMetadataStringForDriver(execution, ActionMetadataKeys.SourceKind, "Automation"),
                BlueprintAutoPlaceActionExecutor.GetMetadataStringForDriver(execution, "SourceUi", string.Empty),
                BlueprintAutoPlaceActionExecutor.GetMetadataStringForDriver(execution, "ButtonId", string.Empty),
                BlueprintAutoPlaceActionExecutor.GetMetadataStringForDriver(execution, "ButtonLabel", string.Empty),
                options,
                out message))
            {
                failureCode = DiagnosticResultCode.BlockedByEnvironment;
                return false;
            }

            return true;
        }

        public ItemUseBridgeResult GetResult(Guid requestId)
        {
            return ItemUseBridge.GetResult(requestId);
        }

        public void Cancel(Guid requestId, string reason)
        {
            ItemUseBridge.Cancel(requestId, reason);
        }

        public void ReleaseUseItem()
        {
            object player;
            if (TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null)
            {
                TerrariaInputCompat.TryReleaseUseItem(player);
            }
        }

        public BlueprintProjectionSnapshot ForceRefreshProjection()
        {
            return BlueprintProjectionService.ForceRefreshForAutoPlacement();
        }

        private static bool TryFindMainInventoryMaterial(
            object player,
            int materialItemId,
            int requiredStack,
            out int materialSlot,
            out int materialStack,
            out string materialName,
            out string message)
        {
            materialSlot = -1;
            materialStack = 0;
            materialName = string.Empty;
            message = string.Empty;
            if (materialItemId <= 0 || requiredStack <= 0)
            {
                message = "蓝图自动摆放材料 id 或数量无效。";
                return false;
            }

            IList inventory;
            if (!InventoryMutationCompat.TryGetContainerItems(player, "Inventory", out inventory, out message) || inventory == null)
            {
                message = "蓝图自动摆放无法读取主背包：" + message;
                return false;
            }

            var limit = Math.Min(50, inventory.Count);
            for (var slot = 0; slot < limit; slot++)
            {
                var item = inventory[slot];
                int itemType;
                int stack;
                int buffType;
                int buffTime;
                bool summon;
                string itemName;
                if (!InventoryMutationCompat.TryReadItemFields(item, out itemType, out itemName, out stack, out buffType, out buffTime, out summon))
                {
                    continue;
                }

                if (itemType == materialItemId && stack >= requiredStack)
                {
                    materialSlot = slot;
                    materialStack = stack;
                    materialName = itemName ?? string.Empty;
                    return true;
                }
            }

            message = "蓝图自动摆放未在主背包 0-49 可使用格找到材料 itemId=" +
                      materialItemId.ToString(CultureInfo.InvariantCulture) +
                      "，数量至少 " + requiredStack.ToString(CultureInfo.InvariantCulture) + "。";
            return false;
        }
    }
}
