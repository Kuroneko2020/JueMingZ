using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.GameState;

namespace JueMingZ.Automation.Fishing
{
    internal static class FishingLoadoutService
    {
        private const long LoadoutRetryIntervalTicks = 60;
        private static readonly object SyncRoot = new object();
        private static bool _loadoutSessionActive;
        private static int _originalLoadoutIndex = -1;
        private static int _targetLoadoutIndex = -1;
        private static Guid _switchRequestId = Guid.Empty;
        private static Guid _restoreRequestId = Guid.Empty;
        private static long _lastSwitchAttemptTick;
        private static long _lastRestoreAttemptTick;

        public static int OriginalLoadoutIndex
        {
            get { lock (SyncRoot) { return _originalLoadoutIndex; } }
        }

        public static int TargetLoadoutIndex
        {
            get { lock (SyncRoot) { return _targetLoadoutIndex; } }
        }

        internal static bool HasResidualState
        {
            get
            {
                lock (SyncRoot)
                {
                    return _loadoutSessionActive ||
                           _switchRequestId != Guid.Empty ||
                           _restoreRequestId != Guid.Empty;
                }
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, bool enabled, bool sessionActive, long tick, string inactiveReason = null)
        {
            if (!enabled || !sessionActive)
            {
                TryRestore(queue, snapshot, tick, string.IsNullOrWhiteSpace(inactiveReason) ? (enabled ? "sessionExit" : "disabled") : inactiveReason);
                return;
            }

            if (queue == null || snapshot == null || !snapshot.IsInWorld || snapshot.Player == null ||
                !snapshot.Player.Exists || !snapshot.Player.Active || snapshot.Player.Dead || snapshot.Player.Ghost)
            {
                return;
            }

            if (IsQueueBusy(queue))
            {
                return;
            }

            object player;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
            {
                return;
            }

            int current;
            if (!FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out current))
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!_loadoutSessionActive)
                {
                    _loadoutSessionActive = true;
                    _originalLoadoutIndex = current;
                    _targetLoadoutIndex = -1;
                    _switchRequestId = Guid.Empty;
                    _restoreRequestId = Guid.Empty;
                    _lastSwitchAttemptTick = 0;
                    _lastRestoreAttemptTick = 0;
                }
            }

            var best = FindBestLoadout(player);
            if (best == null || best.Score <= 0 || best.LoadoutIndex == current)
            {
                lock (SyncRoot)
                {
                    _targetLoadoutIndex = best == null ? -1 : best.LoadoutIndex;
                }

                return;
            }

            lock (SyncRoot)
            {
                _targetLoadoutIndex = best.LoadoutIndex;
                if (_switchRequestId != Guid.Empty || HasRecentAttempt(tick, _lastSwitchAttemptTick))
                {
                    return;
                }
            }

            var request = CreateLoadoutRequest(
                ScenarioNames.FishingAutoLoadoutSwitch,
                best.LoadoutIndex,
                current,
                "bestScore:" + best.Score.ToString(CultureInfo.InvariantCulture));
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                return;
            }

            lock (SyncRoot)
            {
                _switchRequestId = request.RequestId;
                _lastSwitchAttemptTick = tick;
            }
        }

        public static void TryRestore(InputActionQueue queue, GameStateSnapshot snapshot, long tick, string reason)
        {
            int original;
            lock (SyncRoot)
            {
                if (!_loadoutSessionActive || _originalLoadoutIndex < 0)
                {
                    _loadoutSessionActive = false;
                    _targetLoadoutIndex = -1;
                    return;
                }

                original = _originalLoadoutIndex;
                if (_restoreRequestId != Guid.Empty || HasRecentAttempt(tick, _lastRestoreAttemptTick))
                {
                    return;
                }
            }

            if (queue == null || snapshot == null || !snapshot.IsInWorld || IsQueueBusy(queue))
            {
                return;
            }

            object player;
            int current;
            int count;
            if (!TerrariaInputCompat.TryGetLocalPlayer(out player) ||
                !FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out current) ||
                !FishingLoadoutCompat.TryGetLoadoutCount(player, out count))
            {
                ClearIfUnavailable();
                return;
            }

            if (original < 0 || original >= count)
            {
                ClearIfUnavailable();
                return;
            }

            if (current == original)
            {
                ClearIfUnavailable();
                return;
            }

            var request = CreateLoadoutRequest(
                ScenarioNames.FishingAutoLoadoutRestore,
                original,
                current,
                reason ?? "sessionExit");
            InputActionAdmissionResult admission;
            if (!queue.TryEnqueue(request, out admission))
            {
                return;
            }

            lock (SyncRoot)
            {
                _restoreRequestId = request.RequestId;
                _lastRestoreAttemptTick = tick;
            }
        }

        public static void OnActionCompleted(InputActionResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Scenario))
            {
                return;
            }

            var restoreCompleted = false;
            var clearRestoreSession = false;
            var originalForVerification = -1;
            lock (SyncRoot)
            {
                if (result.RequestId == _switchRequestId)
                {
                    _switchRequestId = Guid.Empty;
                }

                if (result.RequestId == _restoreRequestId)
                {
                    _restoreRequestId = Guid.Empty;
                    restoreCompleted = true;
                    originalForVerification = _originalLoadoutIndex;
                    clearRestoreSession =
                        result.Status == InputActionStatus.Succeeded ||
                        result.Status == InputActionStatus.NotApplicable;
                }
            }

            if (restoreCompleted &&
                !clearRestoreSession &&
                result.Status == InputActionStatus.AttemptedButUnverified &&
                originalForVerification >= 0)
            {
                clearRestoreSession = IsCurrentLoadout(originalForVerification);
            }

            if (restoreCompleted && clearRestoreSession)
            {
                ClearSession();
            }
        }

        internal static void ResetForTesting()
        {
            ClearSession();
        }

        internal static void SetRestoreSessionForTesting(Guid restoreRequestId, int originalLoadoutIndex, int targetLoadoutIndex)
        {
            lock (SyncRoot)
            {
                _loadoutSessionActive = true;
                _originalLoadoutIndex = originalLoadoutIndex;
                _targetLoadoutIndex = targetLoadoutIndex;
                _switchRequestId = Guid.Empty;
                _restoreRequestId = restoreRequestId;
                _lastSwitchAttemptTick = 0;
                _lastRestoreAttemptTick = 0;
            }
        }

        internal static bool IsSessionActiveForTesting()
        {
            lock (SyncRoot)
            {
                return _loadoutSessionActive;
            }
        }

        private static FishingLoadoutScore FindBestLoadout(object player)
        {
            int count;
            if (!FishingLoadoutCompat.TryGetLoadoutCount(player, out count))
            {
                return null;
            }

            FishingLoadoutScore best = null;
            for (var index = 0; index < count; index++)
            {
                var score = FishingLoadoutScorer.Score(player, index);
                if (best == null ||
                    score.Score > best.Score ||
                    score.Score == best.Score && score.Score > 0 && score.LoadoutIndex > best.LoadoutIndex)
                {
                    best = score;
                }
            }

            return best;
        }

        private static InputActionRequest CreateLoadoutRequest(string scenario, int target, int original, string reason)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.InventorySlot,
                Priority = InputActionPriority.Normal,
                DuplicatePolicy = InputActionDuplicatePolicy.CoalescePending,
                SourceFeatureId = FeatureIds.FishingAutoLoadout,
                Description = scenario,
                QueueTimeout = TimeSpan.FromSeconds(2),
                AdmissionKey = FeatureIds.FishingAutoLoadout + "|" + scenario,
                Timeout = TimeSpan.FromSeconds(3)
            };
            request.Metadata[ActionMetadataKeys.Scenario] = scenario;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["TargetLoadoutIndex"] = target.ToString(CultureInfo.InvariantCulture);
            request.Metadata["OriginalLoadoutIndex"] = original.ToString(CultureInfo.InvariantCulture);
            request.Metadata["Reason"] = reason ?? string.Empty;
            return request;
        }

        private static bool IsQueueBusy(InputActionQueue queue)
        {
            var snapshot = queue == null ? null : queue.GetFastState();
            return snapshot == null ||
                   snapshot.PendingCount > 0 || snapshot.HasRunningAction;
        }

        private static bool HasRecentAttempt(long tick, long lastAttemptTick)
        {
            return lastAttemptTick > 0 && tick >= lastAttemptTick && tick - lastAttemptTick < LoadoutRetryIntervalTicks;
        }

        private static bool IsCurrentLoadout(int expectedLoadoutIndex)
        {
            object player;
            int current;
            return expectedLoadoutIndex >= 0 &&
                   TerrariaInputCompat.TryGetLocalPlayer(out player) &&
                   FishingLoadoutCompat.TryGetCurrentLoadoutIndex(player, out current) &&
                   current == expectedLoadoutIndex;
        }

        private static void ClearSession()
        {
            lock (SyncRoot)
            {
                ClearSessionLocked();
            }
        }

        private static void ClearIfUnavailable()
        {
            lock (SyncRoot)
            {
                ClearSessionLocked();
            }
        }

        private static void ClearSessionLocked()
        {
            _loadoutSessionActive = false;
            _originalLoadoutIndex = -1;
            _targetLoadoutIndex = -1;
            _switchRequestId = Guid.Empty;
            _restoreRequestId = Guid.Empty;
            _lastSwitchAttemptTick = 0;
            _lastRestoreAttemptTick = 0;
        }
    }
}
