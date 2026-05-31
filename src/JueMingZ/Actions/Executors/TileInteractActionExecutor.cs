using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;

namespace JueMingZ.Actions.Executors
{
    public sealed class TileInteractActionExecutor : InputActionExecutorBase
    {
        private static readonly Dictionary<Guid, MouseTargetInputState> CapturedMouseStates = new Dictionary<Guid, MouseTargetInputState>();

        private sealed class StationInteractTarget
        {
            public int TileX { get; set; }
            public int TileY { get; set; }
            public int TileType { get; set; }
            public int BuffType { get; set; }
        }

        public override InputActionKind Kind { get { return InputActionKind.TileInteract; } }

        public override InputActionExecutionStepResult Start(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            var startedUtc = DateTime.UtcNow;
            if (IsBlockedForWorldInput(snapshot))
            {
                return Finish(execution, startedUtc, InputActionStatus.BlockedByUi, DiagnosticResultCode.BlockedByUi, "TileInteract blocked by world/UI state.", false);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "Local player unavailable for TileInteract.", false);
            }

            MouseTargetInputState mouseState;
            if (!TerrariaInputCompat.TryCaptureMouseTargetState(player, out mouseState))
            {
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, TerrariaInputCompat.LastInputCompatError, false);
            }

            var targets = GetStationTargets(execution);
            if (targets.Count <= 0)
            {
                CleanupInteraction(execution, player, mouseState);
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.BlockedByEnvironment, "TileInteract had no station target.", false);
            }

            var primaryTarget = targets[0];
            string primaryPointMessage;
            var primaryPoints = StationBuffCompat.BuildInteractionPoints(primaryTarget.TileX, primaryTarget.TileY, primaryTarget.TileType, out primaryPointMessage);
            if (primaryPoints == null || primaryPoints.Count <= 0)
            {
                primaryPoints = new List<Tuple<int, int>> { Tuple.Create(primaryTarget.TileX, primaryTarget.TileY) };
            }

            var primaryPoint = primaryPoints.Count > 1 ? primaryPoints[1] : primaryPoints[0];
            if (execution != null && execution.Request != null)
            {
                TerrariaInputCompat.BeginTileInteractionOverride(execution.Request.RequestId, primaryPoint.Item1, primaryPoint.Item2);
                SetState(execution, "TileInteractionOverrideArmed", "true");
            }

            if (!TerrariaInputCompat.TrySetMouseWorldPosition(primaryPoint.Item1 * 16f + 8f, primaryPoint.Item2 * 16f + 8f) ||
                !TerrariaInputCompat.TrySetUseTile(player, true))
            {
                CleanupInteraction(execution, player, mouseState);
                return Finish(execution, startedUtc, InputActionStatus.Failed, DiagnosticResultCode.Failed, TerrariaInputCompat.LastInputCompatError, false);
            }

            bool inventoryOpenBefore;
            var inventoryStateCaptured = TerrariaInputCompat.TryReadPlayerInventoryOpen(out inventoryOpenBefore);
            SetState(execution, "StartedUtcTicks", startedUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionTargetCount", targets.Count.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionBatchTargets", FormatTargets(targets));
            SetState(execution, "PlayerInventoryStateCaptured", inventoryStateCaptured ? "true" : "false");
            SetState(execution, "PlayerInventoryOpenBefore", inventoryOpenBefore ? "true" : "false");

            var tileCheckInvoked = false;
            var tileCheckX = primaryPoint.Item1;
            var tileCheckY = primaryPoint.Item2;
            var tileCheckMessage = string.Empty;
            var tileUseInvoked = false;
            var tileUseX = primaryPoint.Item1;
            var tileUseY = primaryPoint.Item2;
            var tileUseMessage = string.Empty;
            var useAttempts = 0;
            var succeededTargets = 0;
            var anyDirectBuffActive = false;
            var inputPrimed = false;
            var inputPrimeMessage = string.Empty;
            var pointSummary = new StringBuilder();
            var batchMessages = new StringBuilder();

            for (var targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                var target = targets[targetIndex];
                string interactionPointMessage;
                var interactionPoints = StationBuffCompat.BuildInteractionPoints(target.TileX, target.TileY, target.TileType, out interactionPointMessage);
                if (interactionPoints == null || interactionPoints.Count <= 0)
                {
                    interactionPoints = new List<Tuple<int, int>> { Tuple.Create(target.TileX, target.TileY) };
                }

                AppendTargetPoints(pointSummary, target, interactionPoints);
                var targetSucceeded = target.BuffType > 0 && PlayerBuffCompat.HasActiveBuff(player, target.BuffType);
                if (!targetSucceeded)
                {
                    for (var index = 0; index < interactionPoints.Count; index++)
                    {
                        var point = interactionPoints[index];
                        if (execution != null && execution.Request != null)
                        {
                            TerrariaInputCompat.BeginTileInteractionOverride(execution.Request.RequestId, point.Item1, point.Item2);
                        }

                        if (!TerrariaInputCompat.TrySetMouseWorldPosition(point.Item1 * 16f + 8f, point.Item2 * 16f + 8f))
                        {
                            tileUseMessage = TerrariaInputCompat.LastInputCompatError;
                            continue;
                        }

                        TerrariaInputCompat.TrySetUseTile(player, true);
                        string primeMessageForPoint;
                        var primedForPoint = TerrariaInputCompat.TryPrimeTileInteractionAttempt(player, out primeMessageForPoint);
                        inputPrimed = inputPrimed || primedForPoint;
                        inputPrimeMessage = primeMessageForPoint;

                        bool checkInvokedForPoint;
                        string checkMessageForPoint;
                        TerrariaInputCompat.TryInvokeTileInteractionCheck(player, point.Item1, point.Item2, out checkInvokedForPoint, out checkMessageForPoint);
                        tileCheckInvoked = tileCheckInvoked || checkInvokedForPoint;
                        tileCheckX = point.Item1;
                        tileCheckY = point.Item2;
                        tileCheckMessage = checkMessageForPoint;
                        if (checkInvokedForPoint)
                        {
                            tileUseInvoked = true;
                            tileUseX = point.Item1;
                            tileUseY = point.Item2;
                            tileUseMessage = "Player.TileInteractionsUse invoked through TileInteractionsCheck.";
                        }

                        useAttempts++;
                        if (target.BuffType > 0 && PlayerBuffCompat.HasActiveBuff(player, target.BuffType))
                        {
                            targetSucceeded = true;
                            anyDirectBuffActive = true;
                            break;
                        }

                        var primedAgain = TerrariaInputCompat.TryPrimeTileInteractionAttempt(player, out primeMessageForPoint);
                        inputPrimed = inputPrimed || primedAgain;
                        inputPrimeMessage = primeMessageForPoint;
                        bool useInvokedForPoint;
                        string useMessageForPoint;
                        TerrariaInputCompat.TryInvokeTileInteractionUse(player, point.Item1, point.Item2, out useInvokedForPoint, out useMessageForPoint);
                        tileUseInvoked = tileUseInvoked || useInvokedForPoint;
                        tileUseX = point.Item1;
                        tileUseY = point.Item2;
                        tileUseMessage = useMessageForPoint;

                        if (target.BuffType > 0 && PlayerBuffCompat.HasActiveBuff(player, target.BuffType))
                        {
                            targetSucceeded = true;
                            anyDirectBuffActive = true;
                            break;
                        }
                    }
                }

                if (StationBuffCompat.OpensInventoryPanel(target.TileType))
                {
                    SetState(execution, "PlayerInventoryRestoreNeeded", "true");
                    RestorePlayerInventoryIfNeeded(execution);
                }

                if (targetSucceeded)
                {
                    succeededTargets++;
                }

                AppendBatchMessage(batchMessages, target, targetSucceeded, interactionPointMessage);
            }

            var allTargetsActive = succeededTargets >= targets.Count && targets.Count > 0;
            SetState(execution, "TileInteractionCheckInvoked", tileCheckInvoked ? "true" : "false");
            SetState(execution, "TileInteractionCheckX", tileCheckX.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionCheckY", tileCheckY.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionCheckMessage", tileCheckMessage);
            SetState(execution, "TileInteractionUseInvoked", tileUseInvoked ? "true" : "false");
            SetState(execution, "TileInteractionUseX", tileUseX.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionUseY", tileUseY.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionUseMessage", tileUseMessage);
            SetState(execution, "TileInteractionUseAttempts", useAttempts.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionUsePoints", pointSummary.ToString());
            SetState(execution, "TileInteractionPointMessage", primaryPointMessage);
            SetState(execution, "TileInteractionDirectBuffActive", allTargetsActive ? "true" : "false");
            SetState(execution, "TileInteractionAnyDirectBuffActive", anyDirectBuffActive ? "true" : "false");
            SetState(execution, "TileInteractionInputPrimed", inputPrimed ? "true" : "false");
            SetState(execution, "TileInteractionInputPrimeMessage", inputPrimeMessage);
            SetState(execution, "TileInteractionSucceededCount", succeededTargets.ToString(CultureInfo.InvariantCulture));
            SetState(execution, "TileInteractionBatchMessages", batchMessages.ToString());

            if (allTargetsActive)
            {
                CleanupInteraction(execution, player, mouseState);
                return Finish(
                    execution,
                    startedUtc,
                    InputActionStatus.Succeeded,
                    DiagnosticResultCode.Succeeded,
                    targets.Count > 1
                        ? "TileInteract applied " + succeededTargets.ToString(CultureInfo.InvariantCulture) + " station buffs."
                        : "TileInteract applied station buff.",
                    true);
            }

            if (execution != null && execution.Request != null)
            {
                var holdPoint = Tuple.Create(tileUseX, tileUseY);
                TerrariaInputCompat.BeginTileInteractionOverride(execution.Request.RequestId, holdPoint.Item1, holdPoint.Item2);
                TerrariaInputCompat.TrySetMouseWorldPosition(holdPoint.Item1 * 16f + 8f, holdPoint.Item2 * 16f + 8f);
                CapturedMouseStates[execution.Request.RequestId] = mouseState;
            }

            return InputActionExecutionStepResult.Running("TileInteract press held for vanilla tile interaction.");
        }

        public override InputActionExecutionStepResult Update(InputActionExecution execution, GameStateSnapshot snapshot)
        {
            if (execution.UpdateCount < 1)
            {
                return InputActionExecutionStepResult.Running("TileInteract waiting for vanilla update to process use tile.");
            }

            var startedUtc = GetStartedUtc(execution);
            object player;
            TerrariaInputCompat.TryGetLocalPlayer(out player);

            MouseTargetInputState mouseState = null;
            if (execution != null && execution.Request != null && CapturedMouseStates.TryGetValue(execution.Request.RequestId, out mouseState))
            {
                CapturedMouseStates.Remove(execution.Request.RequestId);
            }

            CleanupInteraction(execution, player, mouseState);

            var active = AreExpectedStationBuffsActive(execution, player);
            var targetCount = GetStateInt(execution, "TileInteractionTargetCount", 1);
            var succeededCount = CountActiveStationTargets(execution, player);
            if (succeededCount > GetStateInt(execution, "TileInteractionSucceededCount", 0))
            {
                SetState(execution, "TileInteractionSucceededCount", succeededCount.ToString(CultureInfo.InvariantCulture));
            }

            return Finish(
                execution,
                startedUtc,
                active ? InputActionStatus.Succeeded : InputActionStatus.AttemptedButUnverified,
                active ? DiagnosticResultCode.Succeeded : DiagnosticResultCode.AttemptedButUnverified,
                active
                    ? (targetCount > 1 ? "TileInteract applied " + targetCount.ToString(CultureInfo.InvariantCulture) + " station buffs." : "TileInteract applied station buff.")
                    : "TileInteract clicked station furniture, but only " + succeededCount.ToString(CultureInfo.InvariantCulture) + "/" + targetCount.ToString(CultureInfo.InvariantCulture) + " buff(s) were observed.",
                active);
        }

        public override InputActionExecutionStepResult Cancel(InputActionExecution execution, string reason)
        {
            object player;
            TerrariaInputCompat.TryGetLocalPlayer(out player);

            MouseTargetInputState mouseState = null;
            if (execution != null && execution.Request != null && CapturedMouseStates.TryGetValue(execution.Request.RequestId, out mouseState))
            {
                CapturedMouseStates.Remove(execution.Request.RequestId);
            }

            CleanupInteraction(execution, player, mouseState);
            return base.Cancel(execution, reason);
        }

        private static void CleanupInteraction(InputActionExecution execution, object player, MouseTargetInputState mouseState)
        {
            if (player != null)
            {
                TerrariaInputCompat.TryReleaseUseTile(player);
            }

            if (execution != null && execution.Request != null)
            {
                TerrariaInputCompat.EndTileInteractionOverride(execution.Request.RequestId);
            }

            if (mouseState != null)
            {
                TerrariaInputCompat.TryRestoreMouseTargetState(mouseState);
            }

            RestorePlayerInventoryIfNeeded(execution);
        }

        private static void RestorePlayerInventoryIfNeeded(InputActionExecution execution)
        {
            if (!GetStateBool(execution, "PlayerInventoryRestoreNeeded", false))
            {
                return;
            }

            SetState(execution, "PlayerInventoryRestoreAttempted", "true");
            if (!GetStateBool(execution, "PlayerInventoryStateCaptured", false))
            {
                SetState(execution, "PlayerInventoryRestored", "false");
                SetState(execution, "PlayerInventoryRestoreMessage", "Main.playerInventory state was not captured.");
                return;
            }

            var openBefore = GetStateBool(execution, "PlayerInventoryOpenBefore", false);
            string restoreMessage;
            var restored = TerrariaInputCompat.TrySetPlayerInventoryOpen(openBefore, out restoreMessage);
            SetState(execution, "PlayerInventoryRestored", restored ? "true" : "false");
            SetState(execution, "PlayerInventoryRestoreMessage", restoreMessage);
        }

        private static bool AreExpectedStationBuffsActive(InputActionExecution execution, object player)
        {
            if (player == null)
            {
                return GetStateBool(execution, "TileInteractionDirectBuffActive", false);
            }

            var targets = GetStationTargets(execution);
            if (targets.Count <= 0)
            {
                var expectedBuffType = GetMetadataInt(execution, "BuffType", 0);
                return expectedBuffType > 0 && PlayerBuffCompat.HasActiveBuff(player, expectedBuffType);
            }

            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target.BuffType <= 0 || !PlayerBuffCompat.HasActiveBuff(player, target.BuffType))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountActiveStationTargets(InputActionExecution execution, object player)
        {
            if (player == null)
            {
                return GetStateInt(execution, "TileInteractionSucceededCount", 0);
            }

            var targets = GetStationTargets(execution);
            var count = 0;
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target.BuffType > 0 && PlayerBuffCompat.HasActiveBuff(player, target.BuffType))
                {
                    count++;
                }
            }

            return count;
        }

        private InputActionExecutionStepResult Finish(InputActionExecution execution, DateTime startedUtc, InputActionStatus status, DiagnosticResultCode code, string message, bool buffActive)
        {
            SetResultCode(execution, code);
            MarkActionEventRecorded(execution);
            var duration = (long)(DateTime.UtcNow - startedUtc).TotalMilliseconds;
            DiagnosticActionRecorder.RecordCustomEvent(
                execution == null || execution.Request == null ? Guid.Empty : execution.Request.RequestId,
                GetMetadataString(execution, "Scenario", "AutoRecovery.AutoStationBuff"),
                InputActionKind.TileInteract.ToString(),
                GetMetadataString(execution, "SourceHotkey", string.Empty),
                status.ToString(),
                code.ToString(),
                message ?? string.Empty,
                duration,
                BuildBeforeJson(execution),
                BuildAfterJson(execution, buffActive, code.ToString(), message),
                BuildVerificationJson(execution, buffActive),
                GetMetadataString(execution, "SourceKind", string.Empty),
                GetMetadataString(execution, "SourceUi", string.Empty),
                GetMetadataString(execution, "ButtonId", string.Empty),
                GetMetadataString(execution, "ButtonLabel", string.Empty));

            return InputActionExecutionStepResult.Complete(status, message ?? string.Empty);
        }

        private static string BuildBeforeJson(InputActionExecution execution)
        {
            return "{" +
                   "\"interaction\":\"StationBuff\"," +
                   "\"tileX\":" + IntRaw(GetMetadataInt(execution, "TileX", -1)) + "," +
                   "\"tileY\":" + IntRaw(GetMetadataInt(execution, "TileY", -1)) + "," +
                   "\"tileType\":" + IntRaw(GetMetadataInt(execution, "TileType", 0)) + "," +
                   "\"buffType\":" + IntRaw(GetMetadataInt(execution, "BuffType", 0)) + "," +
                   "\"tileInteractionTargetCount\":" + IntRaw(GetStateInt(execution, "TileInteractionTargetCount", GetMetadataInt(execution, "StationBuffTargetCount", 1))) + "," +
                   "\"tileInteractionBatchTargets\":\"" + EscapeJson(GetStateString(execution, "TileInteractionBatchTargets", GetMetadataString(execution, "StationBuffTargets", string.Empty))) + "\"," +
                   "\"buffActiveBefore\":false" +
                   "}";
        }

        private static string BuildAfterJson(InputActionExecution execution, bool buffActive, string resultCode, string message)
        {
            return "{" +
                   "\"interaction\":\"StationBuff\"," +
                   "\"tileX\":" + IntRaw(GetMetadataInt(execution, "TileX", -1)) + "," +
                   "\"tileY\":" + IntRaw(GetMetadataInt(execution, "TileY", -1)) + "," +
                   "\"tileType\":" + IntRaw(GetMetadataInt(execution, "TileType", 0)) + "," +
                   "\"buffType\":" + IntRaw(GetMetadataInt(execution, "BuffType", 0)) + "," +
                   "\"tileInteractionTargetCount\":" + IntRaw(GetStateInt(execution, "TileInteractionTargetCount", 1)) + "," +
                   "\"tileInteractionSucceededCount\":" + IntRaw(GetStateInt(execution, "TileInteractionSucceededCount", buffActive ? GetStateInt(execution, "TileInteractionTargetCount", 1) : 0)) + "," +
                   "\"tileInteractionBatchTargets\":\"" + EscapeJson(GetStateString(execution, "TileInteractionBatchTargets", string.Empty)) + "\"," +
                   "\"tileInteractionBatchMessages\":\"" + EscapeJson(GetStateString(execution, "TileInteractionBatchMessages", string.Empty)) + "\"," +
                   "\"tileInteractionOverrideArmed\":" + BoolRaw(GetStateBool(execution, "TileInteractionOverrideArmed", false)) + "," +
                   "\"tileInteractionCheckInvoked\":" + BoolRaw(GetStateBool(execution, "TileInteractionCheckInvoked", false)) + "," +
                   "\"tileInteractionCheckX\":" + IntRaw(GetStateInt(execution, "TileInteractionCheckX", -1)) + "," +
                   "\"tileInteractionCheckY\":" + IntRaw(GetStateInt(execution, "TileInteractionCheckY", -1)) + "," +
                   "\"tileInteractionCheckMessage\":\"" + EscapeJson(GetStateString(execution, "TileInteractionCheckMessage", string.Empty)) + "\"," +
                   "\"tileInteractionUseInvoked\":" + BoolRaw(GetStateBool(execution, "TileInteractionUseInvoked", false)) + "," +
                   "\"tileInteractionUseX\":" + IntRaw(GetStateInt(execution, "TileInteractionUseX", -1)) + "," +
                   "\"tileInteractionUseY\":" + IntRaw(GetStateInt(execution, "TileInteractionUseY", -1)) + "," +
                   "\"tileInteractionUseAttempts\":" + IntRaw(GetStateInt(execution, "TileInteractionUseAttempts", 0)) + "," +
                   "\"tileInteractionUsePoints\":\"" + EscapeJson(GetStateString(execution, "TileInteractionUsePoints", string.Empty)) + "\"," +
                   "\"tileInteractionPointMessage\":\"" + EscapeJson(GetStateString(execution, "TileInteractionPointMessage", string.Empty)) + "\"," +
                   "\"tileInteractionDirectBuffActive\":" + BoolRaw(GetStateBool(execution, "TileInteractionDirectBuffActive", false)) + "," +
                   "\"tileInteractionAnyDirectBuffActive\":" + BoolRaw(GetStateBool(execution, "TileInteractionAnyDirectBuffActive", false)) + "," +
                   "\"tileInteractionInputPrimed\":" + BoolRaw(GetStateBool(execution, "TileInteractionInputPrimed", false)) + "," +
                   "\"tileInteractionInputPrimeMessage\":\"" + EscapeJson(GetStateString(execution, "TileInteractionInputPrimeMessage", string.Empty)) + "\"," +
                   "\"tileInteractionUseMessage\":\"" + EscapeJson(GetStateString(execution, "TileInteractionUseMessage", string.Empty)) + "\"," +
                   "\"playerInventoryOpenBefore\":" + BoolRaw(GetStateBool(execution, "PlayerInventoryOpenBefore", false)) + "," +
                   "\"playerInventoryRestoreAttempted\":" + BoolRaw(GetStateBool(execution, "PlayerInventoryRestoreAttempted", false)) + "," +
                   "\"playerInventoryRestored\":" + BoolRaw(GetStateBool(execution, "PlayerInventoryRestored", false)) + "," +
                   "\"playerInventoryRestoreMessage\":\"" + EscapeJson(GetStateString(execution, "PlayerInventoryRestoreMessage", string.Empty)) + "\"," +
                   "\"buffActiveAfter\":" + BoolRaw(buffActive) + "," +
                   "\"resultCode\":\"" + EscapeJson(resultCode) + "\"," +
                   "\"message\":\"" + EscapeJson(message) + "\"" +
                   "}";
        }

        private static string BuildVerificationJson(InputActionExecution execution, bool buffActive)
        {
            return "{" +
                   "\"observableChange\":" + BoolRaw(buffActive) + "," +
                   "\"changedFields\":" + (buffActive ? "[\"buffs\"]" : "[]") + "," +
                   "\"tileInteractionTargetCount\":" + IntRaw(GetStateInt(execution, "TileInteractionTargetCount", 1)) + "," +
                   "\"tileInteractionSucceededCount\":" + IntRaw(GetStateInt(execution, "TileInteractionSucceededCount", buffActive ? GetStateInt(execution, "TileInteractionTargetCount", 1) : 0)) + "," +
                   "\"tileInteractionOverrideArmed\":" + BoolRaw(GetStateBool(execution, "TileInteractionOverrideArmed", false)) + "," +
                   "\"tileInteractionCheckInvoked\":" + BoolRaw(GetStateBool(execution, "TileInteractionCheckInvoked", false)) + "," +
                   "\"tileInteractionUseInvoked\":" + BoolRaw(GetStateBool(execution, "TileInteractionUseInvoked", false)) + "," +
                   "\"tileInteractionInputPrimed\":" + BoolRaw(GetStateBool(execution, "TileInteractionInputPrimed", false)) + "," +
                   "\"tileInteractionUseAttempts\":" + IntRaw(GetStateInt(execution, "TileInteractionUseAttempts", 0)) + "," +
                   "\"tileInteractionUseX\":" + IntRaw(GetStateInt(execution, "TileInteractionUseX", -1)) + "," +
                   "\"tileInteractionUseY\":" + IntRaw(GetStateInt(execution, "TileInteractionUseY", -1)) + "," +
                   "\"playerInventoryRestoreAttempted\":" + BoolRaw(GetStateBool(execution, "PlayerInventoryRestoreAttempted", false)) + "," +
                   "\"playerInventoryRestored\":" + BoolRaw(GetStateBool(execution, "PlayerInventoryRestored", false)) + "," +
                   "\"tileInteractSubmitted\":true" +
                   "}";
        }

        private static List<StationInteractTarget> GetStationTargets(InputActionExecution execution)
        {
            var targets = ParseStationTargets(GetMetadataString(execution, "StationBuffTargets", string.Empty));
            if (targets.Count > 0)
            {
                return targets;
            }

            targets.Add(new StationInteractTarget
            {
                TileX = GetMetadataInt(execution, "TileX", -1),
                TileY = GetMetadataInt(execution, "TileY", -1),
                TileType = GetMetadataInt(execution, "TileType", 0),
                BuffType = GetMetadataInt(execution, "BuffType", 0)
            });
            return targets;
        }

        private static List<StationInteractTarget> ParseStationTargets(string raw)
        {
            var result = new List<StationInteractTarget>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var parts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var fields = parts[index].Split(new[] { ',' }, StringSplitOptions.None);
                if (fields.Length < 4)
                {
                    continue;
                }

                int tileX;
                int tileY;
                int tileType;
                int buffType;
                if (!int.TryParse(fields[0], out tileX) ||
                    !int.TryParse(fields[1], out tileY) ||
                    !int.TryParse(fields[2], out tileType) ||
                    !int.TryParse(fields[3], out buffType))
                {
                    continue;
                }

                result.Add(new StationInteractTarget
                {
                    TileX = tileX,
                    TileY = tileY,
                    TileType = tileType,
                    BuffType = buffType
                });
            }

            return result;
        }

        private static string FormatTargets(List<StationInteractTarget> targets)
        {
            if (targets == null || targets.Count <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (target == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(";");
                }

                builder.Append(target.TileX.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.TileY.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.TileType.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(target.BuffType.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void AppendTargetPoints(StringBuilder builder, StationInteractTarget target, List<Tuple<int, int>> points)
        {
            if (builder == null || target == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("|");
            }

            builder.Append(target.TileType.ToString(CultureInfo.InvariantCulture));
            builder.Append("@");
            builder.Append(target.TileX.ToString(CultureInfo.InvariantCulture));
            builder.Append(",");
            builder.Append(target.TileY.ToString(CultureInfo.InvariantCulture));
            builder.Append("[");
            builder.Append(FormatInteractionPoints(points));
            builder.Append("]");
        }

        private static void AppendBatchMessage(StringBuilder builder, StationInteractTarget target, bool succeeded, string pointMessage)
        {
            if (builder == null || target == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("|");
            }

            builder.Append(target.TileType.ToString(CultureInfo.InvariantCulture));
            builder.Append(":");
            builder.Append(target.BuffType.ToString(CultureInfo.InvariantCulture));
            builder.Append(succeeded ? ":active" : ":missing");
            if (!string.IsNullOrWhiteSpace(pointMessage))
            {
                builder.Append(":");
                builder.Append(pointMessage.Replace("|", "/"));
            }
        }

        private static void SetState(InputActionExecution execution, string key, string value)
        {
            if (execution != null && execution.State != null)
            {
                execution.State[key] = value ?? string.Empty;
            }
        }

        private static DateTime GetStartedUtc(InputActionExecution execution)
        {
            if (execution == null || execution.State == null)
            {
                return DateTime.UtcNow;
            }

            string value;
            long ticks;
            return execution.State.TryGetValue("StartedUtcTicks", out value) && long.TryParse(value, out ticks)
                ? new DateTime(ticks, DateTimeKind.Utc)
                : execution.StartedUtc;
        }

        private static bool GetStateBool(InputActionExecution execution, string key, bool fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            if (!execution.State.TryGetValue(key, out value))
            {
                return fallback;
            }

            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string GetStateString(InputActionExecution execution, string key, string fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            return execution.State.TryGetValue(key, out value) ? value : fallback;
        }

        private static int GetStateInt(InputActionExecution execution, string key, int fallback)
        {
            if (execution == null || execution.State == null || string.IsNullOrWhiteSpace(key))
            {
                return fallback;
            }

            string value;
            int parsed;
            return execution.State.TryGetValue(key, out value) && int.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string FormatInteractionPoints(List<Tuple<int, int>> points)
        {
            if (points == null || points.Count <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var index = 0; index < points.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(";");
                }

                builder.Append(points[index].Item1.ToString(CultureInfo.InvariantCulture));
                builder.Append(",");
                builder.Append(points[index].Item2.ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string IntRaw(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string BoolRaw(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }
}
