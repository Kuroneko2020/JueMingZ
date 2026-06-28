using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.GameState;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Blueprint
{
    internal sealed class BlueprintAutoPlacementCandidate
    {
        public BlueprintAutoPlacementCandidate()
        {
            InstanceId = string.Empty;
            InstanceName = string.Empty;
            LayerKind = string.Empty;
            CoverageGroup = string.Empty;
            MaterialExecutionScope = string.Empty;
            ReplacementCategory = string.Empty;
            AdmissionKey = string.Empty;
        }

        public string InstanceId { get; set; }
        public string InstanceName { get; set; }
        public int LayerOrder { get; set; }
        public int WorldTileX { get; set; }
        public int WorldTileY { get; set; }
        public int RelativeX { get; set; }
        public int RelativeY { get; set; }
        public string LayerKind { get; set; }
        public string CoverageGroup { get; set; }
        public int ContentId { get; set; }
        public int Style { get; set; }
        public int FrameX { get; set; }
        public int FrameY { get; set; }
        public int PaintId { get; set; }
        public int CoatingFlags { get; set; }
        public int Slope { get; set; }
        public bool HalfBrick { get; set; }
        public bool Inactive { get; set; }
        public int MaterialItemId { get; set; }
        public int OriginalMaterialItemId { get; set; }
        public int MaterialStack { get; set; }
        public int MaterialAvailableStack { get; set; }
        public string MaterialExecutionScope { get; set; }
        public bool ReplacementApplied { get; set; }
        public string ReplacementCategory { get; set; }
        public int PlacementPhase { get; set; }
        public string AdmissionKey { get; set; }

        public BlueprintAutoPlacementCandidate Clone()
        {
            return new BlueprintAutoPlacementCandidate
            {
                InstanceId = InstanceId ?? string.Empty,
                InstanceName = InstanceName ?? string.Empty,
                LayerOrder = LayerOrder,
                WorldTileX = WorldTileX,
                WorldTileY = WorldTileY,
                RelativeX = RelativeX,
                RelativeY = RelativeY,
                LayerKind = LayerKind ?? string.Empty,
                CoverageGroup = CoverageGroup ?? string.Empty,
                ContentId = ContentId,
                Style = Style,
                FrameX = FrameX,
                FrameY = FrameY,
                PaintId = PaintId,
                CoatingFlags = CoatingFlags,
                Slope = Slope,
                HalfBrick = HalfBrick,
                Inactive = Inactive,
                MaterialItemId = MaterialItemId,
                OriginalMaterialItemId = OriginalMaterialItemId,
                MaterialStack = MaterialStack,
                MaterialAvailableStack = MaterialAvailableStack,
                MaterialExecutionScope = MaterialExecutionScope ?? string.Empty,
                ReplacementApplied = ReplacementApplied,
                ReplacementCategory = ReplacementCategory ?? string.Empty,
                PlacementPhase = PlacementPhase,
                AdmissionKey = AdmissionKey ?? string.Empty
            };
        }
    }

    internal sealed class BlueprintAutoPlacementSnapshot
    {
        public BlueprintAutoPlacementSnapshot()
        {
            ResultCode = string.Empty;
            Message = string.Empty;
            WorldPairKey = string.Empty;
            WorldKey = string.Empty;
            ProjectionResultCode = string.Empty;
            SelectedInstanceId = string.Empty;
            SelectedInstanceName = string.Empty;
            SelectedLayerKind = string.Empty;
            SelectedReplacementCategory = string.Empty;
            LastAdmissionStatus = string.Empty;
            LastAdmissionReason = string.Empty;
            LastAdmissionKey = string.Empty;
            LastRequestId = string.Empty;
            LastResultCode = string.Empty;
            LastResolvedUtc = DateTime.UtcNow;
        }

        public bool Enabled { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public string WorldPairKey { get; set; }
        public string WorldKey { get; set; }
        public string ProjectionResultCode { get; set; }
        public int CandidateCount { get; set; }
        public int SkippedFulfilledLayerCount { get; set; }
        public int SkippedConflictLayerCount { get; set; }
        public int SkippedUnavailableLayerCount { get; set; }
        public int SkippedUnsupportedLayerCount { get; set; }
        public int SkippedNoMaterialLayerCount { get; set; }
        public int SkippedInsufficientMaterialLayerCount { get; set; }
        public int SkippedVoidBagOnlyLayerCount { get; set; }
        public string SelectedInstanceId { get; set; }
        public string SelectedInstanceName { get; set; }
        public int SelectedLayerOrder { get; set; }
        public string SelectedLayerKind { get; set; }
        public int SelectedWorldTileX { get; set; }
        public int SelectedWorldTileY { get; set; }
        public int SelectedMaterialItemId { get; set; }
        public int SelectedOriginalMaterialItemId { get; set; }
        public int SelectedMaterialStack { get; set; }
        public int SelectedMaterialAvailableStack { get; set; }
        public bool SelectedReplacementApplied { get; set; }
        public string SelectedReplacementCategory { get; set; }
        public string LastAdmissionStatus { get; set; }
        public string LastAdmissionReason { get; set; }
        public string LastAdmissionKey { get; set; }
        public string LastRequestId { get; set; }
        public int SubmittedCount { get; set; }
        public int DeniedCount { get; set; }
        public int FailClosedCount { get; set; }
        public int SucceededCount { get; set; }
        public int AttemptedButUnverifiedCount { get; set; }
        public string LastResultCode { get; set; }
        public double LastResolveElapsedMs { get; set; }
        public DateTime? LastResolvedUtc { get; set; }

        public BlueprintAutoPlacementSnapshot Clone()
        {
            return new BlueprintAutoPlacementSnapshot
            {
                Enabled = Enabled,
                ResultCode = ResultCode ?? string.Empty,
                Message = Message ?? string.Empty,
                WorldPairKey = WorldPairKey ?? string.Empty,
                WorldKey = WorldKey ?? string.Empty,
                ProjectionResultCode = ProjectionResultCode ?? string.Empty,
                CandidateCount = CandidateCount,
                SkippedFulfilledLayerCount = SkippedFulfilledLayerCount,
                SkippedConflictLayerCount = SkippedConflictLayerCount,
                SkippedUnavailableLayerCount = SkippedUnavailableLayerCount,
                SkippedUnsupportedLayerCount = SkippedUnsupportedLayerCount,
                SkippedNoMaterialLayerCount = SkippedNoMaterialLayerCount,
                SkippedInsufficientMaterialLayerCount = SkippedInsufficientMaterialLayerCount,
                SkippedVoidBagOnlyLayerCount = SkippedVoidBagOnlyLayerCount,
                SelectedInstanceId = SelectedInstanceId ?? string.Empty,
                SelectedInstanceName = SelectedInstanceName ?? string.Empty,
                SelectedLayerOrder = SelectedLayerOrder,
                SelectedLayerKind = SelectedLayerKind ?? string.Empty,
                SelectedWorldTileX = SelectedWorldTileX,
                SelectedWorldTileY = SelectedWorldTileY,
                SelectedMaterialItemId = SelectedMaterialItemId,
                SelectedOriginalMaterialItemId = SelectedOriginalMaterialItemId,
                SelectedMaterialStack = SelectedMaterialStack,
                SelectedMaterialAvailableStack = SelectedMaterialAvailableStack,
                SelectedReplacementApplied = SelectedReplacementApplied,
                SelectedReplacementCategory = SelectedReplacementCategory ?? string.Empty,
                LastAdmissionStatus = LastAdmissionStatus ?? string.Empty,
                LastAdmissionReason = LastAdmissionReason ?? string.Empty,
                LastAdmissionKey = LastAdmissionKey ?? string.Empty,
                LastRequestId = LastRequestId ?? string.Empty,
                SubmittedCount = SubmittedCount,
                DeniedCount = DeniedCount,
                FailClosedCount = FailClosedCount,
                SucceededCount = SucceededCount,
                AttemptedButUnverifiedCount = AttemptedButUnverifiedCount,
                LastResultCode = LastResultCode ?? string.Empty,
                LastResolveElapsedMs = LastResolveElapsedMs,
                LastResolvedUtc = LastResolvedUtc
            };
        }
    }

    internal sealed class BlueprintAutoPlacementTickResult
    {
        public BlueprintAutoPlacementTickResult()
        {
            Snapshot = new BlueprintAutoPlacementSnapshot();
        }

        public BlueprintAutoPlacementSnapshot Snapshot { get; set; }
        public BlueprintAutoPlacementCandidate Candidate { get; set; }
        public InputActionRequest Request { get; set; }
        public InputActionAdmissionResult Admission { get; set; }
        public string ProjectionSignature { get; set; }
        public bool Submitted { get; set; }
    }

    internal static class BlueprintAutoPlacementService
    {
        private const int QueueTimeoutMs = 750;
        private const int RequestTimeoutMs = 1500;
        private static readonly object SyncRoot = new object();
        private static BlueprintAutoPlacementSnapshot _lastSnapshot = CreateIdleSnapshot();
        private static string _lastSubmittedAdmissionKey = string.Empty;
        private static string _lastSubmittedProjectionSignature = string.Empty;
        private const int MaxWallUnverifiedRetrySubmissions = 1;
        private static string _wallUnverifiedRetryAdmissionKey = string.Empty;
        private static string _wallUnverifiedRetryProjectionSignature = string.Empty;
        private static int _wallUnverifiedRetrySubmissionCount;
        private static int _submittedCount;
        private static int _deniedCount;
        private static int _failClosedCount;
        private static int _succeededCount;
        private static int _attemptedButUnverifiedCount;

        internal static void Tick(InputActionQueue actionQueue, GameStateSnapshot gameState, RuntimeSettingsSnapshot settings)
        {
            TickCore(actionQueue, gameState, settings);
        }

        public static BlueprintAutoPlacementSnapshot GetDiagnostics()
        {
            lock (SyncRoot)
            {
                return _lastSnapshot.Clone();
            }
        }

        public static string BuildUiStateJson()
        {
            var snapshot = GetDiagnostics();
            var builder = new StringBuilder();
            builder.Append('{');
            AppendRaw(builder, "enabled", BoolRaw(snapshot.Enabled), false);
            AppendString(builder, "resultCode", snapshot.ResultCode, true);
            AppendString(builder, "message", snapshot.Message, true);
            AppendRaw(builder, "candidateCount", snapshot.CandidateCount.ToString(CultureInfo.InvariantCulture), true);
            AppendString(builder, "selectedInstanceId", snapshot.SelectedInstanceId, true);
            AppendString(builder, "selectedLayerKind", snapshot.SelectedLayerKind, true);
            AppendRaw(builder, "selectedWorldTileX", snapshot.SelectedWorldTileX.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedWorldTileY", snapshot.SelectedWorldTileY.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedMaterialItemId", snapshot.SelectedMaterialItemId.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedOriginalMaterialItemId", snapshot.SelectedOriginalMaterialItemId.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "selectedReplacementApplied", BoolRaw(snapshot.SelectedReplacementApplied), true);
            AppendString(builder, "selectedReplacementCategory", snapshot.SelectedReplacementCategory, true);
            AppendString(builder, "lastAdmissionStatus", snapshot.LastAdmissionStatus, true);
            AppendString(builder, "lastAdmissionReason", snapshot.LastAdmissionReason, true);
            AppendString(builder, "lastAdmissionKey", snapshot.LastAdmissionKey, true);
            AppendString(builder, "lastResultCode", snapshot.LastResultCode, true);
            AppendRaw(builder, "submittedCount", snapshot.SubmittedCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "deniedCount", snapshot.DeniedCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "failClosedCount", snapshot.FailClosedCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "succeededCount", snapshot.SucceededCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "attemptedButUnverifiedCount", snapshot.AttemptedButUnverifiedCount.ToString(CultureInfo.InvariantCulture), true);
            AppendRaw(builder, "skippedVoidBagOnlyLayerCount", snapshot.SkippedVoidBagOnlyLayerCount.ToString(CultureInfo.InvariantCulture), true);
            builder.Append('}');
            return builder.ToString();
        }

        public static void RecordExecutorResult(InputActionExecution execution, DiagnosticResultCode resultCode, string resultCodeText, string message)
        {
            lock (SyncRoot)
            {
                var normalized = resultCodeText ?? string.Empty;
                if (string.Equals(normalized, "succeeded", StringComparison.Ordinal))
                {
                    _succeededCount++;
                }
                else if (string.Equals(normalized, "attemptedButUnverified", StringComparison.Ordinal))
                {
                    _attemptedButUnverifiedCount++;
                    ArmWallUnverifiedRetryIfNeededLocked(execution, message);
                }
                else
                {
                    _failClosedCount++;
                    ClearWallUnverifiedRetryIfSameRequestLocked(execution);
                }

                if (string.Equals(normalized, "succeeded", StringComparison.Ordinal))
                {
                    ClearWallUnverifiedRetryIfSameRequestLocked(execution);
                }

                var snapshot = _lastSnapshot == null ? CreateIdleSnapshot() : _lastSnapshot.Clone();
                snapshot.ResultCode = string.IsNullOrWhiteSpace(normalized) ? "failClosed" : normalized;
                snapshot.Message = string.IsNullOrWhiteSpace(message)
                    ? "Blueprint auto placement executor completed."
                    : message;
                snapshot.LastRequestId = execution == null || execution.Request == null
                    ? string.Empty
                    : execution.Request.RequestId.ToString();
                snapshot.LastResultCode = resultCode.ToString();
                snapshot.FailClosedCount = _failClosedCount;
                snapshot.SucceededCount = _succeededCount;
                snapshot.AttemptedButUnverifiedCount = _attemptedButUnverifiedCount;
                snapshot.SubmittedCount = _submittedCount;
                snapshot.DeniedCount = _deniedCount;
                snapshot.LastResolvedUtc = DateTime.UtcNow;
                _lastSnapshot = snapshot;
            }
        }

        internal static BlueprintAutoPlacementTickResult TickForTesting(
            InputActionQueue actionQueue,
            GameStateSnapshot gameState,
            RuntimeSettingsSnapshot settings)
        {
            return TickCore(actionQueue, gameState, settings);
        }

        internal static BlueprintAutoPlacementTickResult ResolveCandidatesForTesting(RuntimeSettingsSnapshot settings)
        {
            return ResolveCandidate(settings);
        }

        internal static InputActionRequest BuildRequestForTesting(
            BlueprintAutoPlacementCandidate candidate,
            string worldPairKey,
            string worldKey)
        {
            return BuildRequest(candidate, worldPairKey, worldKey);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _lastSnapshot = CreateIdleSnapshot();
                _lastSubmittedAdmissionKey = string.Empty;
                _lastSubmittedProjectionSignature = string.Empty;
                _wallUnverifiedRetryAdmissionKey = string.Empty;
                _wallUnverifiedRetryProjectionSignature = string.Empty;
                _wallUnverifiedRetrySubmissionCount = 0;
                _submittedCount = 0;
                _deniedCount = 0;
                _failClosedCount = 0;
                _succeededCount = 0;
                _attemptedButUnverifiedCount = 0;
            }
        }

        private static BlueprintAutoPlacementTickResult TickCore(
            InputActionQueue actionQueue,
            GameStateSnapshot gameState,
            RuntimeSettingsSnapshot settings)
        {
            var enabled = settings != null && settings.BlueprintAutoPlacementEnabled;
            var result = ResolveCandidate(settings);
            var snapshot = result.Snapshot ?? new BlueprintAutoPlacementSnapshot();

            if (!enabled)
            {
                snapshot.ResultCode = "disabled";
                snapshot.Message = "蓝图自动摆放总开关关闭。";
                SaveSnapshot(snapshot);
                return result;
            }

            if (IsBlockedForWorldInput(gameState))
            {
                snapshot.ResultCode = "blockedByWorldInput";
                snapshot.Message = "世界输入不可用，蓝图自动摆放不提交动作。";
                SaveSnapshot(snapshot);
                return result;
            }

            if (actionQueue == null)
            {
                snapshot.ResultCode = "noActionQueue";
                snapshot.Message = "ActionQueue 不可用，蓝图自动摆放 fail-closed。";
                SaveSnapshot(snapshot);
                return result;
            }

            if (result.Candidate == null)
            {
                SaveSnapshot(snapshot);
                return result;
            }

            var wallUnverifiedRetry = false;
            lock (SyncRoot)
            {
                if (string.Equals(_lastSubmittedAdmissionKey, result.Candidate.AdmissionKey, StringComparison.Ordinal) &&
                    string.Equals(_lastSubmittedProjectionSignature, result.ProjectionSignature ?? string.Empty, StringComparison.Ordinal))
                {
                    if (CanSubmitWallUnverifiedRetryLocked(result.Candidate, result.ProjectionSignature))
                    {
                        wallUnverifiedRetry = true;
                    }
                    else
                    {
                        snapshot.ResultCode = "waitingForProjectionChange";
                        snapshot.Message = "蓝图自动摆放已提交同一候选，等待投影变化后再提交。";
                        ApplyCounters(snapshot);
                        _lastSnapshot = snapshot.Clone();
                        return result;
                    }
                }
            }

            var request = BuildRequest(result.Candidate, snapshot.WorldPairKey, snapshot.WorldKey);
            result.Request = request;
            InputActionAdmissionResult admission;
            var accepted = actionQueue.TryEnqueue(request, out admission);
            result.Admission = admission;
            result.Submitted = accepted;
            snapshot.LastAdmissionStatus = accepted ? "Accepted" : "Denied";
            snapshot.LastAdmissionReason = admission == null ? "unknown" : admission.Reason ?? string.Empty;
            snapshot.LastAdmissionKey = request.AdmissionKey ?? string.Empty;
            snapshot.LastRequestId = request.RequestId.ToString();
            snapshot.LastResultCode = accepted ? DiagnosticResultCode.Queued.ToString() : DiagnosticResultCode.Failed.ToString();

            lock (SyncRoot)
            {
                if (accepted)
                {
                    _submittedCount++;
                    if (wallUnverifiedRetry)
                    {
                        _wallUnverifiedRetrySubmissionCount++;
                    }
                    else
                    {
                        ClearWallUnverifiedRetryIfDifferentLocked(request.AdmissionKey, result.ProjectionSignature);
                    }

                    _lastSubmittedAdmissionKey = request.AdmissionKey ?? string.Empty;
                    _lastSubmittedProjectionSignature = result.ProjectionSignature ?? string.Empty;
                    snapshot.ResultCode = "submitted";
                    snapshot.Message = wallUnverifiedRetry
                        ? "蓝图墙自动摆放候选在未验证后进行一次有界重试，仍等待受控 ItemCheck 摆放与投影复验。"
                        : "蓝图自动摆放候选已提交 ActionQueue，等待受控 ItemCheck 摆放与投影复验。";
                }
                else
                {
                    _deniedCount++;
                    snapshot.ResultCode = "admissionDenied";
                    snapshot.Message = "蓝图自动摆放候选未通过 ActionQueue admission：" + snapshot.LastAdmissionReason;
                }

                ApplyCounters(snapshot);
                _lastSnapshot = snapshot.Clone();
            }

            return result;
        }

        private static bool CanSubmitWallUnverifiedRetryLocked(BlueprintAutoPlacementCandidate candidate, string projectionSignature)
        {
            if (!IsWallCandidate(candidate) ||
                candidate == null ||
                string.IsNullOrWhiteSpace(candidate.AdmissionKey) ||
                _wallUnverifiedRetrySubmissionCount >= MaxWallUnverifiedRetrySubmissions)
            {
                return false;
            }

            return string.Equals(_wallUnverifiedRetryAdmissionKey, candidate.AdmissionKey, StringComparison.Ordinal) &&
                   string.Equals(_wallUnverifiedRetryProjectionSignature, projectionSignature ?? string.Empty, StringComparison.Ordinal);
        }

        private static void ArmWallUnverifiedRetryIfNeededLocked(InputActionExecution execution, string message)
        {
            if (!IsWallAutoPlacementRequest(execution) ||
                (message ?? string.Empty).StartsWith("wallFrameRefreshFailed:", StringComparison.Ordinal))
            {
                return;
            }

            var admissionKey = GetRequestAdmissionKey(execution);
            if (string.IsNullOrWhiteSpace(admissionKey) ||
                !string.Equals(admissionKey, _lastSubmittedAdmissionKey, StringComparison.Ordinal))
            {
                return;
            }

            if (!string.Equals(_wallUnverifiedRetryAdmissionKey, admissionKey, StringComparison.Ordinal) ||
                !string.Equals(_wallUnverifiedRetryProjectionSignature, _lastSubmittedProjectionSignature, StringComparison.Ordinal))
            {
                _wallUnverifiedRetryAdmissionKey = admissionKey;
                _wallUnverifiedRetryProjectionSignature = _lastSubmittedProjectionSignature ?? string.Empty;
                _wallUnverifiedRetrySubmissionCount = 0;
            }
        }

        private static void ClearWallUnverifiedRetryIfSameRequestLocked(InputActionExecution execution)
        {
            var admissionKey = GetRequestAdmissionKey(execution);
            if (!string.IsNullOrWhiteSpace(admissionKey) &&
                string.Equals(_wallUnverifiedRetryAdmissionKey, admissionKey, StringComparison.Ordinal))
            {
                ClearWallUnverifiedRetryLocked();
            }
        }

        private static void ClearWallUnverifiedRetryIfDifferentLocked(string admissionKey, string projectionSignature)
        {
            if (string.IsNullOrWhiteSpace(_wallUnverifiedRetryAdmissionKey))
            {
                return;
            }

            if (!string.Equals(_wallUnverifiedRetryAdmissionKey, admissionKey ?? string.Empty, StringComparison.Ordinal) ||
                !string.Equals(_wallUnverifiedRetryProjectionSignature, projectionSignature ?? string.Empty, StringComparison.Ordinal))
            {
                ClearWallUnverifiedRetryLocked();
            }
        }

        private static void ClearWallUnverifiedRetryLocked()
        {
            _wallUnverifiedRetryAdmissionKey = string.Empty;
            _wallUnverifiedRetryProjectionSignature = string.Empty;
            _wallUnverifiedRetrySubmissionCount = 0;
        }

        private static bool IsWallCandidate(BlueprintAutoPlacementCandidate candidate)
        {
            return candidate != null &&
                   string.Equals(candidate.LayerKind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase) &&
                   candidate.ContentId > 0;
        }

        private static bool IsWallAutoPlacementRequest(InputActionExecution execution)
        {
            return string.Equals(
                GetRequestMetadata(execution, ActionMetadataKeys.BlueprintLayerKind),
                BlueprintLayerKinds.Wall,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRequestAdmissionKey(InputActionExecution execution)
        {
            return execution == null || execution.Request == null
                ? string.Empty
                : execution.Request.AdmissionKey ?? string.Empty;
        }

        private static string GetRequestMetadata(InputActionExecution execution, string key)
        {
            if (execution == null ||
                execution.Request == null ||
                execution.Request.Metadata == null ||
                string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            string value;
            return execution.Request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static BlueprintAutoPlacementTickResult ResolveCandidate(RuntimeSettingsSnapshot settings)
        {
            var enabled = settings != null && settings.BlueprintAutoPlacementEnabled;
            var replacementSettings = BlueprintReplacementRuleService.FromSettings(settings == null ? null : settings.SourceSettings);
            var operationStart = Stopwatch.GetTimestamp();
            var snapshot = new BlueprintAutoPlacementSnapshot
            {
                Enabled = enabled,
                ResultCode = enabled ? "ready" : "disabled",
                Message = enabled ? "蓝图自动摆放候选已解析。" : "蓝图自动摆放总开关关闭。",
                LastResolvedUtc = DateTime.UtcNow
            };

            var result = new BlueprintAutoPlacementTickResult
            {
                Snapshot = snapshot,
                ProjectionSignature = string.Empty
            };
            if (!enabled)
            {
                FinishResolveSnapshot(snapshot, operationStart);
                return result;
            }

            var projection = BlueprintProjectionService.GetSnapshot();
            snapshot.WorldPairKey = projection == null ? string.Empty : projection.WorldPairKey ?? string.Empty;
            snapshot.WorldKey = projection == null ? string.Empty : projection.WorldKey ?? string.Empty;
            snapshot.ProjectionResultCode = projection == null ? string.Empty : projection.ResultCode ?? string.Empty;
            result.ProjectionSignature = projection == null ? string.Empty : projection.Signature ?? string.Empty;

            if (projection == null || !projection.LoadSucceeded)
            {
                snapshot.ResultCode = "projectionUnavailable";
                snapshot.Message = projection == null ? "蓝图投影不可用。" : projection.Message ?? string.Empty;
                FinishResolveSnapshot(snapshot, operationStart);
                return result;
            }

            var materials = BlueprintMaterialService.GetSnapshot();
            if (materials == null || !materials.LoadSucceeded)
            {
                CountProjectionSkips(projection, snapshot, null, replacementSettings);
                snapshot.ResultCode = "materialsUnavailable";
                snapshot.Message = materials == null ? "蓝图材料统计不可用。" : materials.Message ?? string.Empty;
                FinishResolveSnapshot(snapshot, operationStart);
                return result;
            }

            if (!materials.InventoryReadSucceeded)
            {
                CountProjectionSkips(projection, snapshot, null, replacementSettings);
                snapshot.ResultCode = "inventoryUnavailable";
                snapshot.Message = "蓝图自动摆放无法读取材料库存：" + (materials.InventoryReadMessage ?? string.Empty);
                FinishResolveSnapshot(snapshot, operationStart);
                return result;
            }

            var candidates = BuildCandidates(projection, materials, snapshot, replacementSettings);
            candidates.Sort(CompareCandidates);
            snapshot.CandidateCount = candidates.Count;
            if (candidates.Count <= 0)
            {
                if (snapshot.SkippedVoidBagOnlyLayerCount > 0)
                {
                    snapshot.ResultCode = "voidBagMaterialNotExecutable";
                    snapshot.Message = "材料面板可见虚空袋材料，但自动摆放当前只能从主背包 0-49 使用物品；请先把材料移入主背包。";
                }
                else
                {
                    snapshot.ResultCode = "noCandidate";
                    snapshot.Message = "当前有效投影没有可提交的自动摆放候选。";
                }

                FinishResolveSnapshot(snapshot, operationStart);
                return result;
            }

            var selected = candidates[0];
            FillSelected(snapshot, selected);
            result.Candidate = selected.Clone();
            FinishResolveSnapshot(snapshot, operationStart);
            return result;
        }

        private static List<BlueprintAutoPlacementCandidate> BuildCandidates(
            BlueprintProjectionSnapshot projection,
            BlueprintMaterialSnapshot materials,
            BlueprintAutoPlacementSnapshot snapshot,
            BlueprintReplacementSettings replacementSettings)
        {
            var candidates = new List<BlueprintAutoPlacementCandidate>();
            var mainAvailability = BuildMainInventoryAvailabilityMap(materials);
            var combinedAvailability = BuildCombinedInventoryAvailabilityMap(materials);
            var layers = projection == null ? null : projection.AllProjectedLayers;
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
                {
                    snapshot.SkippedFulfilledLayerCount++;
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    snapshot.SkippedConflictLayerCount++;
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Unavailable, StringComparison.Ordinal))
                {
                    snapshot.SkippedUnavailableLayerCount++;
                    continue;
                }

                if (!string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
                {
                    continue;
                }

                string supportReason;
                if (!IsStage14SupportedLayer(layer, out supportReason))
                {
                    snapshot.SkippedUnsupportedLayerCount++;
                    continue;
                }

                if (layer.MaterialItemId <= 0 || layer.MaterialStack <= 0)
                {
                    snapshot.SkippedNoMaterialLayerCount++;
                    continue;
                }

                BlueprintReplacementMaterialChoice materialChoice;
                if (!BlueprintReplacementRuleService.TryChooseMaterialForAutoPlacement(layer, replacementSettings, mainAvailability, out materialChoice) ||
                    materialChoice == null ||
                    materialChoice.MaterialItemId <= 0)
                {
                    if (CanBeSatisfiedOnlyWithVoidBagContribution(layer, replacementSettings, mainAvailability, combinedAvailability))
                    {
                        snapshot.SkippedVoidBagOnlyLayerCount++;
                    }
                    else
                    {
                        snapshot.SkippedInsufficientMaterialLayerCount++;
                    }

                    continue;
                }

                candidates.Add(new BlueprintAutoPlacementCandidate
                {
                    InstanceId = layer.InstanceId ?? string.Empty,
                    InstanceName = layer.InstanceName ?? string.Empty,
                    LayerOrder = layer.LayerOrder,
                    WorldTileX = layer.WorldTileX,
                    WorldTileY = layer.WorldTileY,
                    RelativeX = layer.RelativeX,
                    RelativeY = layer.RelativeY,
                    LayerKind = layer.LayerKind ?? string.Empty,
                    CoverageGroup = layer.CoverageGroup ?? string.Empty,
                    ContentId = layer.ContentId,
                    Style = layer.Style,
                    FrameX = layer.FrameX,
                    FrameY = layer.FrameY,
                    PaintId = layer.PaintId,
                    CoatingFlags = layer.CoatingFlags,
                    Slope = layer.Slope,
                    HalfBrick = layer.HalfBrick,
                    Inactive = layer.Inactive,
                    MaterialItemId = materialChoice.MaterialItemId,
                    OriginalMaterialItemId = materialChoice.OriginalMaterialItemId > 0 ? materialChoice.OriginalMaterialItemId : layer.MaterialItemId,
                    MaterialStack = layer.MaterialStack,
                    MaterialAvailableStack = materialChoice.AvailableStack,
                    MaterialExecutionScope = "mainInventory0-49",
                    ReplacementApplied = materialChoice.ReplacementApplied,
                    ReplacementCategory = materialChoice.Category ?? string.Empty,
                    PlacementPhase = ResolvePlacementPhase(layer),
                    AdmissionKey = BuildAdmissionKey(projection, layer)
                });
            }

            return candidates;
        }

        private static void CountProjectionSkips(
            BlueprintProjectionSnapshot projection,
            BlueprintAutoPlacementSnapshot snapshot,
            IDictionary<int, int> availability,
            BlueprintReplacementSettings replacementSettings)
        {
            var layers = projection == null ? null : projection.AllProjectedLayers;
            for (var index = 0; layers != null && index < layers.Count; index++)
            {
                var layer = layers[index];
                if (layer == null)
                {
                    continue;
                }

                if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Fulfilled, StringComparison.Ordinal))
                {
                    snapshot.SkippedFulfilledLayerCount++;
                }
                else if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Conflict, StringComparison.Ordinal))
                {
                    snapshot.SkippedConflictLayerCount++;
                }
                else if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Unavailable, StringComparison.Ordinal))
                {
                    snapshot.SkippedUnavailableLayerCount++;
                }
                else if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal) &&
                         !IsStage14SupportedLayer(layer, out _))
                {
                    snapshot.SkippedUnsupportedLayerCount++;
                }
                else if (string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal) &&
                         (layer.MaterialItemId <= 0 || layer.MaterialStack <= 0))
                {
                    snapshot.SkippedNoMaterialLayerCount++;
                }
                else if (availability != null &&
                         string.Equals(layer.Status, BlueprintProjectionLayerStatuses.Missing, StringComparison.Ordinal))
                {
                    BlueprintReplacementMaterialChoice materialChoice;
                    if (!BlueprintReplacementRuleService.TryChooseMaterialForAutoPlacement(layer, replacementSettings, availability, out materialChoice) ||
                        materialChoice == null ||
                        materialChoice.AvailableStack < layer.MaterialStack)
                    {
                        snapshot.SkippedInsufficientMaterialLayerCount++;
                    }
                }
            }
        }

        private static Dictionary<int, int> BuildMainInventoryAvailabilityMap(BlueprintMaterialSnapshot materials)
        {
            var map = new Dictionary<int, int>();
            var items = materials == null ? null : materials.Items;
            for (var index = 0; items != null && index < items.Count; index++)
            {
                var item = items[index];
                if (item == null || item.ItemId <= 0)
                {
                    continue;
                }

                map[item.ItemId] = item.MainInventoryStack;
            }

            return map;
        }

        private static Dictionary<int, int> BuildCombinedInventoryAvailabilityMap(BlueprintMaterialSnapshot materials)
        {
            var map = new Dictionary<int, int>();
            var items = materials == null ? null : materials.Items;
            for (var index = 0; items != null && index < items.Count; index++)
            {
                var item = items[index];
                if (item == null || item.ItemId <= 0)
                {
                    continue;
                }

                map[item.ItemId] = item.AvailableStack;
            }

            return map;
        }

        private static bool CanBeSatisfiedOnlyWithVoidBagContribution(
            BlueprintProjectionCellSnapshot layer,
            BlueprintReplacementSettings replacementSettings,
            IDictionary<int, int> mainAvailability,
            IDictionary<int, int> combinedAvailability)
        {
            BlueprintReplacementMaterialChoice combinedChoice;
            if (!BlueprintReplacementRuleService.TryChooseMaterialForAutoPlacement(layer, replacementSettings, combinedAvailability, out combinedChoice) ||
                combinedChoice == null ||
                combinedChoice.MaterialItemId <= 0 ||
                combinedChoice.AvailableStack < layer.MaterialStack)
            {
                return false;
            }

            // ItemUseBridge can only drive vanilla item use from main inventory slots 0-49.
            // Void Bag availability stays visible in material UI, but it cannot become an
            // auto-placement candidate until there is a queue-owned inventory movement path.
            return GetAvailableStack(mainAvailability, combinedChoice.MaterialItemId) < layer.MaterialStack;
        }

        private static int GetAvailableStack(IDictionary<int, int> availability, int itemId)
        {
            if (availability == null || itemId <= 0)
            {
                return 0;
            }

            int value;
            return availability.TryGetValue(itemId, out value) ? Math.Max(0, value) : 0;
        }

        internal static bool IsStage13SupportedLayerForTesting(BlueprintProjectionCellSnapshot layer, out string reason)
        {
            return IsStage14SupportedLayer(layer, out reason);
        }

        internal static bool IsStage14SupportedLayerForTesting(BlueprintProjectionCellSnapshot layer, out string reason)
        {
            return IsStage14SupportedLayer(layer, out reason);
        }

        internal static bool IsStage15SupportedLayerForTesting(BlueprintProjectionCellSnapshot layer, out string reason)
        {
            return IsStage14SupportedLayer(layer, out reason);
        }

        internal static bool IsStage13SupportedCandidate(BlueprintAutoPlacementCandidate candidate, out string reason)
        {
            return IsStage14SupportedCandidate(candidate, out reason);
        }

        internal static bool IsStage14SupportedCandidate(BlueprintAutoPlacementCandidate candidate, out string reason)
        {
            reason = string.Empty;
            if (candidate == null)
            {
                reason = "candidateMissing";
                return false;
            }

            return IsStage14SupportedShape(
                candidate.LayerKind,
                candidate.ContentId,
                candidate.PaintId,
                candidate.CoatingFlags,
                candidate.Slope,
                candidate.HalfBrick,
                candidate.Inactive,
                out reason);
        }

        internal static bool IsStage15SupportedCandidate(BlueprintAutoPlacementCandidate candidate, out string reason)
        {
            return IsStage14SupportedCandidate(candidate, out reason);
        }

        private static bool IsStage14SupportedLayer(BlueprintProjectionCellSnapshot layer, out string reason)
        {
            reason = string.Empty;
            if (layer == null)
            {
                reason = "layerMissing";
                return false;
            }

            return IsStage14SupportedShape(
                layer.LayerKind,
                layer.ContentId,
                layer.PaintId,
                layer.CoatingFlags,
                layer.Slope,
                layer.HalfBrick,
                layer.Inactive,
                out reason);
        }

        private static bool IsStage14SupportedShape(
            string layerKind,
            int contentId,
            int paintId,
            int coatingFlags,
            int slope,
            bool halfBrick,
            bool inactive,
            out string reason)
        {
            reason = string.Empty;
            if (contentId <= 0)
            {
                reason = "invalidContent";
                return false;
            }

            var kind = layerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                if (paintId != 0 || coatingFlags != 0)
                {
                    reason = "wallPaintOrCoating";
                    return false;
                }

                return true;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                if (paintId != 0 || coatingFlags != 0 || slope != 0 || halfBrick || inactive)
                {
                    reason = "tileDecorationOrShape";
                    return false;
                }

                return true;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                if (!IsStage14SupportedObject(contentId, out reason))
                {
                    return false;
                }

                if (paintId != 0 || coatingFlags != 0 || slope != 0 || halfBrick || inactive)
                {
                    reason = "objectDecorationOrShape";
                    return false;
                }

                return true;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return contentId > 0;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                reason = "wireRequiresMechanicalTool";
                return false;
            }

            reason = "unsupportedLayerKind";
            return false;
        }

        private static bool IsStage14SupportedObject(int tileId, out string reason)
        {
            reason = string.Empty;
            // Terraria 1.4.5.6 TileID constants verified from references:
            // Torches=4, ClosedDoor=10, Tables=14, Chairs=15, WorkBenches=18,
            // Containers=21, Signs=55, Dressers=88, Rope=213, MinecartTrack=314,
            // Containers2=467 and Tables2=469 all use the vanilla item-use path.
            if (tileId == 4 ||
                tileId == 10 ||
                tileId == 14 ||
                tileId == 15 ||
                tileId == 18 ||
                tileId == 21 ||
                tileId == 55 ||
                tileId == 88 ||
                tileId == 213 ||
                tileId == 314 ||
                tileId == 467 ||
                tileId == 469)
            {
                return true;
            }

            if (tileId == 11)
            {
                reason = "openDoorDeferred";
                return false;
            }

            reason = "objectUnsupportedByStage14Allowlist";
            return false;
        }

        private static InputActionRequest BuildRequest(
            BlueprintAutoPlacementCandidate candidate,
            string worldPairKey,
            string worldKey)
        {
            candidate = candidate ?? new BlueprintAutoPlacementCandidate();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.BlueprintAutoPlace,
                Priority = InputActionPriority.Low,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.BlueprintMain,
                Description = "Blueprint auto placement contract: " +
                              (candidate.LayerKind ?? string.Empty) +
                              " @" +
                              candidate.WorldTileX.ToString(CultureInfo.InvariantCulture) +
                              "," +
                              candidate.WorldTileY.ToString(CultureInfo.InvariantCulture),
                QueueTimeout = TimeSpan.FromMilliseconds(QueueTimeoutMs),
                Timeout = TimeSpan.FromMilliseconds(RequestTimeoutMs),
                IsExclusive = false,
                AdmissionKey = candidate.AdmissionKey ?? string.Empty
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.BlueprintAutoPlace;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata[ActionMetadataKeys.BlueprintInstanceId] = candidate.InstanceId ?? string.Empty;
            request.Metadata[ActionMetadataKeys.BlueprintInstanceName] = candidate.InstanceName ?? string.Empty;
            request.Metadata[ActionMetadataKeys.BlueprintLayerKind] = candidate.LayerKind ?? string.Empty;
            request.Metadata[ActionMetadataKeys.BlueprintLayerOrder] = candidate.LayerOrder.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldX] = candidate.WorldTileX.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.WorldY] = candidate.WorldTileY.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintRelativeX] = candidate.RelativeX.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintRelativeY] = candidate.RelativeY.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintContentId] = candidate.ContentId.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintStyle] = candidate.Style.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintMaterialItemId] = candidate.MaterialItemId.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintOriginalMaterialItemId] = (candidate.OriginalMaterialItemId > 0 ? candidate.OriginalMaterialItemId : candidate.MaterialItemId).ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintMaterialStack] = candidate.MaterialStack.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintMaterialAvailableStack] = candidate.MaterialAvailableStack.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintMaterialExecutionScope] = string.IsNullOrWhiteSpace(candidate.MaterialExecutionScope) ? "mainInventory0-49" : candidate.MaterialExecutionScope;
            request.Metadata[ActionMetadataKeys.BlueprintReplacementApplied] = candidate.ReplacementApplied ? "true" : "false";
            request.Metadata[ActionMetadataKeys.BlueprintReplacementCategory] = candidate.ReplacementCategory ?? string.Empty;
            request.Metadata[ActionMetadataKeys.BlueprintAdmissionKey] = request.AdmissionKey ?? string.Empty;
            request.Metadata["BlueprintWorldPairKey"] = worldPairKey ?? string.Empty;
            request.Metadata["BlueprintWorldKey"] = worldKey ?? string.Empty;
            request.Metadata[ActionMetadataKeys.BlueprintFrameX] = candidate.FrameX.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintFrameY] = candidate.FrameY.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintPaintId] = candidate.PaintId.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintCoatingFlags] = candidate.CoatingFlags.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintSlope] = candidate.Slope.ToString(CultureInfo.InvariantCulture);
            request.Metadata[ActionMetadataKeys.BlueprintHalfBrick] = candidate.HalfBrick ? "true" : "false";
            request.Metadata[ActionMetadataKeys.BlueprintInactive] = candidate.Inactive ? "true" : "false";
            request.Metadata["BlueprintPlacementPhase"] = candidate.PlacementPhase.ToString(CultureInfo.InvariantCulture);
            request.Metadata["BlueprintContractStage"] = "15";
            return request;
        }

        private static string BuildAdmissionKey(BlueprintProjectionSnapshot projection, BlueprintProjectionCellSnapshot layer)
        {
            return "blueprint.auto-place|" +
                   (projection == null ? string.Empty : projection.WorldPairKey ?? string.Empty) + "|" +
                   (projection == null ? string.Empty : projection.WorldKey ?? string.Empty) + "|" +
                   (layer == null ? string.Empty : layer.InstanceId ?? string.Empty) + "|" +
                   (layer == null ? 0 : layer.LayerOrder).ToString(CultureInfo.InvariantCulture) + "|" +
                   (layer == null ? 0 : layer.WorldTileX).ToString(CultureInfo.InvariantCulture) + "," +
                   (layer == null ? 0 : layer.WorldTileY).ToString(CultureInfo.InvariantCulture) + "|" +
                   (layer == null ? string.Empty : layer.LayerKind ?? string.Empty) + "|" +
                   (layer == null ? 0 : layer.ContentId).ToString(CultureInfo.InvariantCulture) + "|" +
                   (layer == null ? 0 : layer.Style).ToString(CultureInfo.InvariantCulture);
        }

        private static int CompareCandidates(BlueprintAutoPlacementCandidate left, BlueprintAutoPlacementCandidate right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            var layerOrderCompare = right.LayerOrder.CompareTo(left.LayerOrder);
            if (layerOrderCompare != 0) return layerOrderCompare;

            var phaseCompare = left.PlacementPhase.CompareTo(right.PlacementPhase);
            if (phaseCompare != 0) return phaseCompare;

            var yCompare = left.WorldTileY.CompareTo(right.WorldTileY);
            if (yCompare != 0) return yCompare;

            var xCompare = left.WorldTileX.CompareTo(right.WorldTileX);
            if (xCompare != 0) return xCompare;

            var kindCompare = string.Compare(left.LayerKind, right.LayerKind, StringComparison.Ordinal);
            if (kindCompare != 0) return kindCompare;

            return string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
        }

        private static void FillSelected(BlueprintAutoPlacementSnapshot snapshot, BlueprintAutoPlacementCandidate candidate)
        {
            if (snapshot == null || candidate == null)
            {
                return;
            }

            snapshot.SelectedInstanceId = candidate.InstanceId ?? string.Empty;
            snapshot.SelectedInstanceName = candidate.InstanceName ?? string.Empty;
            snapshot.SelectedLayerOrder = candidate.LayerOrder;
            snapshot.SelectedLayerKind = candidate.LayerKind ?? string.Empty;
            snapshot.SelectedWorldTileX = candidate.WorldTileX;
            snapshot.SelectedWorldTileY = candidate.WorldTileY;
            snapshot.SelectedMaterialItemId = candidate.MaterialItemId;
            snapshot.SelectedOriginalMaterialItemId = candidate.OriginalMaterialItemId > 0 ? candidate.OriginalMaterialItemId : candidate.MaterialItemId;
            snapshot.SelectedMaterialStack = candidate.MaterialStack;
            snapshot.SelectedMaterialAvailableStack = candidate.MaterialAvailableStack;
            snapshot.SelectedReplacementApplied = candidate.ReplacementApplied;
            snapshot.SelectedReplacementCategory = candidate.ReplacementCategory ?? string.Empty;
            snapshot.LastAdmissionKey = candidate.AdmissionKey ?? string.Empty;
        }

        private static int ResolvePlacementPhase(BlueprintProjectionCellSnapshot layer)
        {
            if (layer == null)
            {
                return 99;
            }

            var kind = layer.LayerKind ?? string.Empty;
            if (string.Equals(kind, BlueprintLayerKinds.Tile, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wall, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Object, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Wire, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(kind, BlueprintLayerKinds.Actuator, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 99;
        }

        private static bool IsBlockedForWorldInput(GameStateSnapshot snapshot)
        {
            if (snapshot == null || !snapshot.IsInWorld)
            {
                return true;
            }

            return snapshot.Ui != null && snapshot.Ui.HasBlockingUi;
        }

        private static void SaveSnapshot(BlueprintAutoPlacementSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                ApplyCounters(snapshot);
                _lastSnapshot = (snapshot ?? CreateIdleSnapshot()).Clone();
            }
        }

        private static void ApplyCounters(BlueprintAutoPlacementSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.SubmittedCount = _submittedCount;
            snapshot.DeniedCount = _deniedCount;
            snapshot.FailClosedCount = _failClosedCount;
            snapshot.SucceededCount = _succeededCount;
            snapshot.AttemptedButUnverifiedCount = _attemptedButUnverifiedCount;
        }

        private static void FinishResolveSnapshot(BlueprintAutoPlacementSnapshot snapshot, long operationStart)
        {
            if (snapshot == null)
            {
                return;
            }

            snapshot.LastResolveElapsedMs = ElapsedMilliseconds(operationStart, Stopwatch.GetTimestamp());
            ApplyCounters(snapshot);
            BlueprintDiagnostics.RecordAutoPlacementCandidateScan(snapshot);
        }

        private static double ElapsedMilliseconds(long start, long end)
        {
            if (start <= 0 || end <= start)
            {
                return 0;
            }

            return (end - start) * 1000.0 / Stopwatch.Frequency;
        }

        private static BlueprintAutoPlacementSnapshot CreateIdleSnapshot()
        {
            return new BlueprintAutoPlacementSnapshot
            {
                ResultCode = "idle",
                Message = "蓝图自动摆放尚未运行。"
            };
        }

        private static string GetMetadataString(InputActionExecution execution, string key)
        {
            if (execution == null || execution.Request == null || execution.Request.Metadata == null)
            {
                return string.Empty;
            }

            string value;
            return execution.Request.Metadata.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
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

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (var index = 0; index < value.Length; index++)
            {
                var ch = value[index];
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
