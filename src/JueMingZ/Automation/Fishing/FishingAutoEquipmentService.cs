using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Fishing
{
    internal static class FishingAutoEquipmentService
    {
        private static readonly object SyncRoot = new object();
        private const int MaxRestoreAttemptCount = 3;
        private static bool _hasSession;
        private static bool _applied;
        private static bool _pendingRestore;
        private static bool _manualInventoryInteractionDetected;
        private static bool _stillHoldingOriginalRod;
        private static bool _forceRestoreWhenPossible;
        private static Guid _applyRequestId = Guid.Empty;
        private static Guid _restoreRequestId = Guid.Empty;
        private static long _lastApplyAttemptTick;
        private static long _lastRestoreAttemptTick;
        private static int _restoreAttemptCount;
        private static FishingAutoEquipmentSessionInfo _session;
        private static readonly List<FishingAutoEquipmentMoveRecord> Records = new List<FishingAutoEquipmentMoveRecord>();
        private static string _lastDecision = string.Empty;
        private static string _lastSkipReason = string.Empty;
        private static int _appliedMoveCount;
        private static int _pendingRestoreCount;

        public static bool HasPendingRestore
        {
            get
            {
                lock (SyncRoot)
                {
                    return _pendingRestore || Records.Count > 0 || _restoreRequestId != Guid.Empty;
                }
            }
        }

        public static bool Applied
        {
            get { lock (SyncRoot) { return _applied; } }
        }

        public static int PendingRestoreCount
        {
            get { lock (SyncRoot) { return _pendingRestoreCount; } }
        }

        public static string LastDecision
        {
            get { lock (SyncRoot) { return _lastDecision; } }
        }

        public static string LastSkipReason
        {
            get { lock (SyncRoot) { return _lastSkipReason; } }
        }

        public static int AppliedMoveCount
        {
            get { lock (SyncRoot) { return _appliedMoveCount; } }
        }

        public static bool StillHoldingOriginalRod
        {
            get { lock (SyncRoot) { return _stillHoldingOriginalRod; } }
        }

        public static bool ManualInventoryInteractionDetected
        {
            get { lock (SyncRoot) { return _manualInventoryInteractionDetected; } }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, bool enabled, bool hasLocalBobber, FishingLiquidKind liquidKind, long tick, string disabledReason = "disabled")
        {
            if (snapshot == null || !snapshot.IsInWorld || snapshot.Player == null || !snapshot.Player.Exists || !snapshot.Player.Active)
            {
                Record("skipped", "notInWorldOrPlayerUnavailable");
                return;
            }

            if (snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                lock (SyncRoot)
                {
                    if (Records.Count > 0)
                    {
                        _forceRestoreWhenPossible = true;
                        _pendingRestore = true;
                    }
                }

                Record("paused", "playerDeadOrGhost");
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                Record("skipped", "playerUnavailable");
                return;
            }

            if (FishingAutoEquipmentCompat.TryIsMouseItemPresent())
            {
                lock (SyncRoot)
                {
                    _manualInventoryInteractionDetected = true;
                    _stillHoldingOriginalRod = false;
                }

                Record("paused", "blockedByMouseItem");
                return;
            }

            if (!enabled)
            {
                if (HasRecordsOrInflight())
                {
                    TryEnqueueRestore(queue, tick, string.IsNullOrWhiteSpace(disabledReason) ? "disabled" : disabledReason);
                }
                else
                {
                    ClearSession(false);
                    Record("idle", string.IsNullOrWhiteSpace(disabledReason) ? "disabled" : disabledReason);
                }

                return;
            }

            if (HasRestoreInflight() || HasApplyInflight())
            {
                Record("waiting", HasRestoreInflight() ? "restoreInFlight" : "applyInFlight");
                return;
            }

            if (HasRecords())
            {
                if (ShouldForceRestoreWhenPossible())
                {
                    TryEnqueueRestore(queue, tick, "deathRecovery");
                    return;
                }

                if (HasLoadoutChanged(player))
                {
                    TryEnqueueRestore(queue, tick, "loadoutChangedDuringAutoEquipment");
                    return;
                }

                bool stillHolding;
                string holdReason;
                FishingAutoEquipmentCompat.TryIsStillHoldingOriginalRod(player, GetSession(), out stillHolding, out holdReason);
                var manualInventoryInteractionDetected = HasManualInventoryInteractionDetected();
                lock (SyncRoot)
                {
                    _stillHoldingOriginalRod = stillHolding;
                }

                if (ShouldKeepApplied(hasLocalBobber, stillHolding, manualInventoryInteractionDetected))
                {
                    Record(hasLocalBobber ? "keepingApplied" : "keepingAppliedWithoutBobber", stillHolding ? "stillHoldingOriginalRod" : string.Empty);
                    return;
                }

                TryEnqueueRestore(queue, tick, manualInventoryInteractionDetected ? "manualInventoryInteractionEnded" : "leftOriginalRod");
                return;
            }

            if (HasSessionWithoutRecords())
            {
                bool stillHolding;
                string holdReason;
                FishingAutoEquipmentCompat.TryIsStillHoldingOriginalRod(player, GetSession(), out stillHolding, out holdReason);
                lock (SyncRoot)
                {
                    _stillHoldingOriginalRod = stillHolding;
                }

                if (hasLocalBobber || stillHolding)
                {
                    Record("sessionWaiting", stillHolding ? "stillHoldingOriginalRod" : string.Empty);
                    return;
                }

                ClearSession(false);
                Record("sessionCleared", "leftOriginalRodWithoutRecords");
                return;
            }

            if (!hasLocalBobber)
            {
                Record("idle", "noLocalBobber");
                return;
            }

            if (tick - _lastApplyAttemptTick < 30)
            {
                Record("skipped", "applyCooldown");
                return;
            }

            FishingAutoEquipmentSessionInfo session;
            string sessionMessage;
            if (!FishingAutoEquipmentCompat.TryCaptureSessionInfo(player, out session, out sessionMessage))
            {
                Record("skipped", sessionMessage);
                return;
            }

            FishingAutoEquipmentPlan plan;
            string planMessage;
            if (!FishingAutoEquipmentCompat.TryBuildApplyPlan(player, session, liquidKind, out plan, out planMessage) || plan == null)
            {
                Record("skipped", string.IsNullOrWhiteSpace(planMessage) ? "planBuildFailed" : planMessage);
                return;
            }

            StartSession(session);
            if (plan.Moves.Count == 0)
            {
                Record("sessionStartedNoMoves", string.IsNullOrWhiteSpace(plan.SkipReason) ? "noBetterCandidate" : plan.SkipReason);
                return;
            }

            TryEnqueueApply(queue, plan, tick);
        }

        public static void OnActionCompleted(InputActionResult result)
        {
            if (result == null)
            {
                return;
            }

            var handled = false;
            lock (SyncRoot)
            {
                if (result.RequestId == _applyRequestId)
                {
                    _applyRequestId = Guid.Empty;
                    handled = true;
                }
                else if (result.RequestId == _restoreRequestId)
                {
                    _restoreRequestId = Guid.Empty;
                    handled = true;
                }
            }

            if (!handled)
            {
                return;
            }

            FishingAutoEquipmentActionResult actionResult;
            if (FishingAutoEquipmentCompat.TryTakeApplyResult(result.RequestId, out actionResult))
            {
                ApplyCompleted(actionResult);
                return;
            }

            if (FishingAutoEquipmentCompat.TryTakeRestoreResult(result.RequestId, out actionResult))
            {
                RestoreCompleted(actionResult);
            }
        }

        private static void TryEnqueueApply(InputActionQueue queue, FishingAutoEquipmentPlan plan, long tick)
        {
            if (queue == null)
            {
                Record("skipped", "queueUnavailable");
                return;
            }

            if (IsQueueBusy(queue))
            {
                Record("skipped", "queueBusyBeforeApply");
                return;
            }

            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoEquipment,
                Description = "Fishing auto equipment apply",
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingAutoEquipment + "|apply",
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingAutoEquipmentApply;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["OriginalSelectedItemIndex"] = plan.Session.OriginalSelectedItemIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["OriginalLoadoutIndex"] = plan.Session.OriginalLoadoutIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PlannedMoveCount"] = plan.Moves.Count.ToString(CultureInfo.InvariantCulture);
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                Record("skipped", "applyAdmissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            FishingAutoEquipmentCompat.RegisterApplyPlan(request.RequestId, plan);
            lock (SyncRoot)
            {
                _applyRequestId = request.RequestId;
                _lastApplyAttemptTick = tick;
            }

            Record("submittedApply", string.Empty);
        }

        private static void TryEnqueueRestore(InputActionQueue queue, long tick, string reason)
        {
            if (queue == null)
            {
                Record("skipped", "queueUnavailable");
                return;
            }

            if (IsQueueBusy(queue))
            {
                Record("skipped", "queueBusyBeforeRestore");
                return;
            }

            lock (SyncRoot)
            {
                if (_restoreRequestId != Guid.Empty)
                {
                    return;
                }

                if (_lastRestoreAttemptTick > 0 && tick >= _lastRestoreAttemptTick && tick - _lastRestoreAttemptTick < 30)
                {
                    return;
                }

                if (Records.Count == 0)
                {
                    _pendingRestore = false;
                    return;
                }

                if (_restoreAttemptCount >= MaxRestoreAttemptCount)
                {
                    AbandonRestoreLocked("restoreAttemptLimit");
                    return;
                }
            }

            var session = GetSession();
            if (session == null)
            {
                Record("skipped", "sessionUnavailableForRestore");
                return;
            }

            var records = GetRecordsCopy();
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoEquipment,
                Description = "Fishing auto equipment restore",
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingAutoEquipment + "|restore",
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.FishingAutoEquipmentRestore;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["OriginalSelectedItemIndex"] = session.OriginalSelectedItemIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["OriginalLoadoutIndex"] = session.OriginalLoadoutIndex.ToString(CultureInfo.InvariantCulture);
            request.Metadata["PendingRestoreCount"] = records.Count.ToString(CultureInfo.InvariantCulture);
            request.Metadata["Reason"] = reason ?? string.Empty;
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                Record("skipped", "restoreAdmissionDenied:" + (admission == null ? "unknown" : admission.Reason));
                return;
            }

            FishingAutoEquipmentCompat.RegisterRestoreRequest(request.RequestId, session, records, reason);
            lock (SyncRoot)
            {
                _restoreRequestId = request.RequestId;
                _lastRestoreAttemptTick = tick;
                _restoreAttemptCount++;
                _pendingRestore = true;
            }

            Record("submittedRestore", reason ?? string.Empty);
        }

        private static void ApplyCompleted(FishingAutoEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                Records.Clear();
                if (result != null && result.Records != null)
                {
                    Records.AddRange(result.Records);
                }

                _applied = Records.Count > 0;
                _pendingRestore = Records.Count > 0;
                _pendingRestoreCount = Records.Count;
                _appliedMoveCount = result == null ? 0 : result.AppliedMoveCount;
                _restoreAttemptCount = 0;
                _lastDecision = result == null ? "applyCompleted" : result.Decision;
                _lastSkipReason = result == null ? string.Empty : result.SkipReason;
            }
        }

        private static void RestoreCompleted(FishingAutoEquipmentActionResult result)
        {
            lock (SyncRoot)
            {
                var pauseWithoutBudgetCost = result != null &&
                                             (result.BlockedByMouseItem ||
                                              result.LoadoutChangedDuringAutoEquipment ||
                                              (result.PendingRestoreCount > 0 &&
                                               (result.UserChangedManagedSlotCount > 0 ||
                                                result.OriginalMovedByUserCount > 0)));
                if (pauseWithoutBudgetCost && _restoreAttemptCount > 0)
                {
                    _restoreAttemptCount--;
                    if (result.BlockedByMouseItem || result.LoadoutChangedDuringAutoEquipment)
                    {
                        _lastRestoreAttemptTick = 0;
                    }
                }

                Records.Clear();
                if (result != null && result.Records != null)
                {
                    Records.AddRange(result.Records);
                }

                _pendingRestoreCount = Records.Count;
                _pendingRestore = Records.Count > 0;
                _applied = Records.Count > 0;
                _appliedMoveCount = Records.Count > 0 ? _appliedMoveCount : 0;
                _lastDecision = result == null ? "restoreCompleted" : result.Decision;
                _lastSkipReason = result == null ? string.Empty : result.SkipReason;
                if (Records.Count == 0)
                {
                    ClearSessionLocked(false);
                }
            }
        }

        private static void StartSession(FishingAutoEquipmentSessionInfo session)
        {
            lock (SyncRoot)
            {
                _hasSession = true;
                _session = session;
                _applied = false;
                _pendingRestore = false;
                _manualInventoryInteractionDetected = false;
                _stillHoldingOriginalRod = true;
                _forceRestoreWhenPossible = false;
                Records.Clear();
                _pendingRestoreCount = 0;
                _appliedMoveCount = 0;
                _restoreAttemptCount = 0;
            }
        }

        private static void AbandonRestoreLocked(string reason)
        {
            Records.Clear();
            _pendingRestore = false;
            _pendingRestoreCount = 0;
            _applied = false;
            _appliedMoveCount = 0;
            _restoreRequestId = Guid.Empty;
            _restoreAttemptCount = 0;
            _lastDecision = "restoreAbandoned";
            _lastSkipReason = reason ?? string.Empty;
            _hasSession = false;
            _session = null;
            _forceRestoreWhenPossible = false;
        }

        private static void ClearSession(bool preserveRecords)
        {
            lock (SyncRoot)
            {
                ClearSessionLocked(preserveRecords);
            }
        }

        private static void ClearSessionLocked(bool preserveRecords)
        {
            _hasSession = false;
            _applied = false;
            _pendingRestore = preserveRecords && Records.Count > 0;
            _manualInventoryInteractionDetected = false;
            _stillHoldingOriginalRod = false;
            _forceRestoreWhenPossible = preserveRecords && _forceRestoreWhenPossible;
            _applyRequestId = Guid.Empty;
            _restoreRequestId = Guid.Empty;
            _session = null;
            if (!preserveRecords)
            {
                Records.Clear();
                _pendingRestoreCount = 0;
                _appliedMoveCount = 0;
                _restoreAttemptCount = 0;
            }
        }

        private static bool HasRecords()
        {
            lock (SyncRoot)
            {
                return Records.Count > 0;
            }
        }

        private static bool HasRecordsOrInflight()
        {
            lock (SyncRoot)
            {
                return Records.Count > 0 || _applyRequestId != Guid.Empty || _restoreRequestId != Guid.Empty;
            }
        }

        private static bool HasApplyInflight()
        {
            lock (SyncRoot)
            {
                return _applyRequestId != Guid.Empty;
            }
        }

        private static bool HasRestoreInflight()
        {
            lock (SyncRoot)
            {
                return _restoreRequestId != Guid.Empty;
            }
        }

        private static bool HasSessionWithoutRecords()
        {
            lock (SyncRoot)
            {
                return _hasSession && Records.Count == 0;
            }
        }

        private static bool ShouldForceRestoreWhenPossible()
        {
            lock (SyncRoot)
            {
                return _forceRestoreWhenPossible;
            }
        }

        private static bool HasManualInventoryInteractionDetected()
        {
            lock (SyncRoot)
            {
                return _manualInventoryInteractionDetected;
            }
        }

        private static bool ShouldKeepApplied(bool hasLocalBobber, bool stillHoldingOriginalRod, bool manualInventoryInteractionDetected)
        {
            return hasLocalBobber || (stillHoldingOriginalRod && !manualInventoryInteractionDetected);
        }

        internal static bool ShouldRestoreWithoutBobberForTesting(bool stillHoldingOriginalRod, bool manualInventoryInteractionDetected)
        {
            return !ShouldKeepApplied(false, stillHoldingOriginalRod, manualInventoryInteractionDetected);
        }

        private static FishingAutoEquipmentSessionInfo GetSession()
        {
            lock (SyncRoot)
            {
                return _session;
            }
        }

        private static List<FishingAutoEquipmentMoveRecord> GetRecordsCopy()
        {
            lock (SyncRoot)
            {
                return new List<FishingAutoEquipmentMoveRecord>(Records);
            }
        }

        private static bool HasLoadoutChanged(object player)
        {
            var session = GetSession();
            if (session == null || session.OriginalLoadoutIndex < 0)
            {
                return false;
            }

            int current;
            return FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out current) &&
                   current != session.OriginalLoadoutIndex;
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction ||
                   ItemUseBridge.PendingRequestId != Guid.Empty;
        }

        private static void Record(string decision, string skipReason)
        {
            lock (SyncRoot)
            {
                _lastDecision = decision ?? string.Empty;
                _lastSkipReason = skipReason ?? string.Empty;
            }
        }
    }
}
