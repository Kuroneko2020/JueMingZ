using System;
using System.Collections.Generic;
using JueMingZ.Automation.Movement;
using JueMingZ.Diagnostics;

namespace JueMingZ.Compat
{
    internal static partial class MovementSafeLandingEquipmentCompat
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentPlan> ApplyPlans = new Dictionary<Guid, MovementSafeLandingEquipmentPlan>();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentActionResult> ApplyResults = new Dictionary<Guid, MovementSafeLandingEquipmentActionResult>();
        private static readonly Dictionary<Guid, RestoreRequest> RestoreRequests = new Dictionary<Guid, RestoreRequest>();
        private static readonly Dictionary<Guid, MovementSafeLandingEquipmentActionResult> RestoreResults = new Dictionary<Guid, MovementSafeLandingEquipmentActionResult>();

        private sealed class RestoreRequest
        {
            public List<MovementSafeLandingEquipmentMoveRecord> Records;
            public string Reason;
        }

        public static void RegisterApplyPlan(Guid requestId, MovementSafeLandingEquipmentPlan plan)
        {
            if (requestId == Guid.Empty || plan == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                ApplyPlans[requestId] = ClonePlan(plan);
            }
        }

        public static void RegisterRestoreRequest(Guid requestId, IList<MovementSafeLandingEquipmentMoveRecord> records, string reason)
        {
            if (requestId == Guid.Empty || records == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                RestoreRequests[requestId] = new RestoreRequest
                {
                    Records = CopyRecords(records),
                    Reason = reason ?? string.Empty
                };
            }
        }

        // Apply consumes a registered plan exactly once and verifies source and
        // target signatures before writing any temporary equipment swap.
        public static bool TryApplyRegisteredPlan(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            MovementSafeLandingEquipmentPlan plan;
            lock (SyncRoot)
            {
                if (!ApplyPlans.TryGetValue(requestId, out plan))
                {
                    result = BuildResult("applySkipped", "applyPlanUnavailable", "Safe landing temporary equipment apply plan unavailable.");
                    ApplyResults[requestId] = result;
                    return false;
                }

                ApplyPlans.Remove(requestId);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("applySkipped", "playerUnavailable", "Local player unavailable for safe landing temporary equipment apply.");
                StoreApplyResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("applyBlocked", "blockedByMouseItem", "Safe landing temporary equipment apply blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                StoreApplyResult(requestId, result);
                return false;
            }

            result = ApplyPlan(player, plan);
            StoreApplyResult(requestId, result);
            return result.AppliedMoveCount > 0;
        }

        // Restore records are the recovery anchor; blocked restores keep the
        // records pending instead of claiming success.
        public static bool TryRestoreRegisteredRecords(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            RestoreRequest request;
            lock (SyncRoot)
            {
                if (!RestoreRequests.TryGetValue(requestId, out request))
                {
                    result = BuildResult("restoreSkipped", "restoreRequestUnavailable", "Safe landing temporary equipment restore request unavailable.");
                    RestoreResults[requestId] = result;
                    return false;
                }

                RestoreRequests.Remove(requestId);
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                result = BuildResult("restoreSkipped", "playerUnavailable", "Local player unavailable for safe landing temporary equipment restore.");
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            if (TryIsMouseItemPresent())
            {
                result = BuildResult("restoreBlocked", "blockedByMouseItem", "Safe landing temporary equipment restore blocked because Main.mouseItem is not empty.");
                result.BlockedByMouseItem = true;
                result.Records.AddRange(CopyRecords(request.Records));
                result.PendingRestoreCount = result.Records.Count;
                StoreRestoreResult(requestId, result);
                return false;
            }

            result = RestoreRecords(player, request);
            StoreRestoreResult(requestId, result);
            return result.PendingRestoreCount == 0;
        }

        public static bool TryPeekApplyResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                return ApplyResults.TryGetValue(requestId, out result);
            }
        }

        public static bool TryTakeApplyResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!ApplyResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                ApplyResults.Remove(requestId);
                return true;
            }
        }

        public static bool TryTakeRestoreResult(Guid requestId, out MovementSafeLandingEquipmentActionResult result)
        {
            result = null;
            if (requestId == Guid.Empty)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (!RestoreResults.TryGetValue(requestId, out result))
                {
                    return false;
                }

                RestoreResults.Remove(requestId);
                return true;
            }
        }

        public static void UpdateApplyPulseResult(Guid requestId, SafeLandingJumpPulseSnapshot pulse, bool notRequired)
        {
            if (requestId == Guid.Empty)
            {
                return;
            }

            lock (SyncRoot)
            {
                MovementSafeLandingEquipmentActionResult result;
                if (!ApplyResults.TryGetValue(requestId, out result) || result == null)
                {
                    return;
                }

                result.PulseNotRequired = notRequired;
                if (pulse != null)
                {
                    result.PulseQueued = true;
                    result.PulseCompleted = pulse.Completed;
                    result.PulseFailed = pulse.Failed;
                    result.PulseStatus = pulse.Status ?? string.Empty;
                    result.PulsePhase = pulse.Phase ?? string.Empty;
                    result.PulseApplySite = pulse.LastApplySite ?? string.Empty;
                    result.PulseMessage = pulse.LastMessage ?? string.Empty;
                }
            }
        }
    }
}
