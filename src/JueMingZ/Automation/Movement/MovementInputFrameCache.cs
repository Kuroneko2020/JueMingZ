using System;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;

namespace JueMingZ.Automation.Movement
{
    internal static class MovementInputFrameCache
    {
        private const int FeatureMaskSimulatedJump = 1;
        private const int FeatureMaskContinuousDash = 2;
        private const int FeatureMaskSafeLanding = 4;
        private const int FeatureMaskTeleportCorrection = 8;
        private const int FeatureMaskDashDoubleTap = 16;
        private static readonly object SyncRoot = new object();
        private static MovementInputFrame _currentFrame;
        private static long _createdFrameCount;
        private static long _jumpProfileReadCount;
        private static long _dashProfileReadCount;
        private static long _basicMotionReadCount;

        public static MovementInputFrame GetOrCreate(RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            var updateCount = runtimeState == null ? 0 : runtimeState.UpdateCount;
            var featureMask = BuildFeatureMask(settingsSnapshot);

            try
            {
                object player;
                var playerAvailable = TerrariaInputCompat.TryGetLocalPlayer(out player) && player != null;
                var playerError = playerAvailable ? string.Empty : TerrariaInputCompat.LastInputCompatError;
                var whoAmI = -1;
                if (playerAvailable)
                {
                    TerrariaInputCompat.TryReadPlayerWhoAmI(player, out whoAmI);
                }

                lock (SyncRoot)
                {
                    if (_currentFrame != null && _currentFrame.Matches(updateCount, featureMask, playerAvailable ? player : null, whoAmI, playerAvailable))
                    {
                        return _currentFrame;
                    }

                    var frame = new MovementInputFrame(updateCount, featureMask, playerAvailable ? player : null, whoAmI, playerAvailable, playerError);
                    _currentFrame = frame;
                    _createdFrameCount++;
                    return frame;
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MovementInputFrameCache.GetOrCreate", error);
                LogThrottle.ErrorThrottled(
                    "movement-input-frame-cache-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementInputFrameCache",
                    "Movement input frame cache failed; services will use their old read paths.", error);
                return null;
            }
        }

        internal static int BuildFeatureMask(RuntimeSettingsSnapshot settingsSnapshot)
        {
            var mask = 0;
            if (settingsSnapshot == null)
            {
                return mask;
            }

            if (settingsSnapshot.MovementSimulatedMultiJumpEnabled)
            {
                mask |= FeatureMaskSimulatedJump;
            }

            if (settingsSnapshot.MovementContinuousDashEnabled)
            {
                mask |= FeatureMaskContinuousDash;
            }

            if (settingsSnapshot.MovementSafeLandingEnabled)
            {
                mask |= FeatureMaskSafeLanding;
            }

            if (settingsSnapshot.MovementTeleportCorrectionEnabled)
            {
                mask |= FeatureMaskTeleportCorrection;
            }

            if (string.Equals(settingsSnapshot.MovementContinuousDashMode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
            {
                mask |= FeatureMaskDashDoubleTap;
            }

            return mask;
        }

        internal static MovementInputFrame CreateForTesting(long updateCount, int featureMask, object player, int whoAmI)
        {
            return new MovementInputFrame(updateCount, featureMask, player, whoAmI, player != null, player == null ? "localPlayerUnavailable" : string.Empty);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _currentFrame = null;
                _createdFrameCount = 0;
                _jumpProfileReadCount = 0;
                _dashProfileReadCount = 0;
                _basicMotionReadCount = 0;
            }
        }

        internal static long CreatedFrameCountForTesting
        {
            get { lock (SyncRoot) { return _createdFrameCount; } }
        }

        internal static long JumpProfileReadCountForTesting
        {
            get { lock (SyncRoot) { return _jumpProfileReadCount; } }
        }

        internal static long DashProfileReadCountForTesting
        {
            get { lock (SyncRoot) { return _dashProfileReadCount; } }
        }

        internal static long BasicMotionReadCountForTesting
        {
            get { lock (SyncRoot) { return _basicMotionReadCount; } }
        }

        private static void IncrementJumpProfileReadCount()
        {
            lock (SyncRoot)
            {
                _jumpProfileReadCount++;
            }
        }

        private static void IncrementDashProfileReadCount()
        {
            lock (SyncRoot)
            {
                _dashProfileReadCount++;
            }
        }

        private static void IncrementBasicMotionReadCount()
        {
            lock (SyncRoot)
            {
                _basicMotionReadCount++;
            }
        }

        internal sealed class MovementInputFrame
        {
            private readonly WeakReference _playerReference;
            private bool _jumpProfileResolved;
            private bool _jumpProfileAvailable;
            private JumpInputProfile _jumpProfile;
            private string _jumpProfileFailureReason = string.Empty;
            private bool _dashProfileResolved;
            private bool _dashProfileAvailable;
            private DashInputProfile _dashProfile;
            private string _dashProfileFailureReason = string.Empty;
            private bool _basicMotionResolved;
            private MovementInputBasicMotion _basicMotion;
            private string _basicMotionFailureReason = string.Empty;

            internal MovementInputFrame(
                long updateCount,
                int featureMask,
                object player,
                int whoAmI,
                bool localPlayerAvailable,
                string localPlayerFailureReason)
            {
                UpdateCount = updateCount;
                FeatureMask = featureMask;
                _playerReference = player == null ? null : new WeakReference(player);
                PlayerWhoAmI = whoAmI;
                LocalPlayerAvailable = localPlayerAvailable;
                LocalPlayerFailureReason = localPlayerFailureReason ?? string.Empty;
            }

            public long UpdateCount { get; private set; }
            public int FeatureMask { get; private set; }
            public int PlayerWhoAmI { get; private set; }
            public bool LocalPlayerAvailable { get; private set; }
            public string LocalPlayerFailureReason { get; private set; }

            public bool Matches(long updateCount, int featureMask, object player, int whoAmI, bool localPlayerAvailable)
            {
                if (UpdateCount != updateCount || FeatureMask != featureMask || LocalPlayerAvailable != localPlayerAvailable)
                {
                    return false;
                }

                if (!localPlayerAvailable)
                {
                    return true;
                }

                if (PlayerWhoAmI != whoAmI)
                {
                    return false;
                }

                object cachedPlayer;
                return TryGetPlayer(out cachedPlayer) && object.ReferenceEquals(cachedPlayer, player);
            }

            public bool TryGetPlayer(out object player)
            {
                player = null;
                if (!LocalPlayerAvailable || _playerReference == null)
                {
                    return false;
                }

                player = _playerReference.Target;
                return player != null;
            }

            public bool TryGetJumpProfile(out JumpInputProfile profile, out string failureReason)
            {
                profile = null;
                failureReason = string.Empty;
                object player;
                if (!TryGetPlayer(out player))
                {
                    failureReason = FirstNonEmpty(LocalPlayerFailureReason, "localPlayerUnavailable");
                    return false;
                }

                if (!_jumpProfileResolved)
                {
                    IncrementJumpProfileReadCount();
                    _jumpProfileAvailable = TerrariaInputCompat.TryReadJumpInputProfile(player, out _jumpProfile) && _jumpProfile != null;
                    _jumpProfileFailureReason = _jumpProfileAvailable ? string.Empty : TerrariaInputCompat.LastInputCompatError;
                    _jumpProfileResolved = true;
                }

                profile = _jumpProfile;
                failureReason = _jumpProfileFailureReason ?? string.Empty;
                return _jumpProfileAvailable;
            }

            public bool TryGetDashProfile(out DashInputProfile profile, out string failureReason)
            {
                profile = null;
                failureReason = string.Empty;
                object player;
                if (!TryGetPlayer(out player))
                {
                    failureReason = FirstNonEmpty(LocalPlayerFailureReason, "localPlayerUnavailable");
                    return false;
                }

                if (!_dashProfileResolved)
                {
                    IncrementDashProfileReadCount();
                    _dashProfileAvailable = TerrariaDashCompat.TryReadDashInputProfile(player, out _dashProfile) && _dashProfile != null;
                    _dashProfileFailureReason = _dashProfileAvailable ? string.Empty : TerrariaDashCompat.LastDashCompatError;
                    _dashProfileResolved = true;
                }

                profile = _dashProfile;
                failureReason = _dashProfileFailureReason ?? string.Empty;
                return _dashProfileAvailable;
            }

            public MovementInputBasicMotion GetBasicMotion(out string failureReason)
            {
                failureReason = string.Empty;
                object player;
                if (!TryGetPlayer(out player))
                {
                    failureReason = FirstNonEmpty(LocalPlayerFailureReason, "localPlayerUnavailable");
                    return null;
                }

                if (!_basicMotionResolved)
                {
                    IncrementBasicMotionReadCount();
                    if (!TerrariaInputCompat.TryReadMovementBasicMotion(player, out _basicMotion))
                    {
                        _basicMotionFailureReason = TerrariaInputCompat.LastInputCompatError;
                    }
                    else
                    {
                        _basicMotionFailureReason = _basicMotion == null ? "basicMotionUnavailable" : _basicMotion.FailureSummary;
                    }

                    _basicMotionResolved = true;
                }

                failureReason = _basicMotionFailureReason ?? string.Empty;
                return _basicMotion;
            }

            private static string FirstNonEmpty(string first, string second)
            {
                return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
            }
        }
    }
}
