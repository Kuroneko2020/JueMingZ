using System;
using System.Globalization;
using JueMingZ.Actions;
using JueMingZ.Actions.Channels;
using JueMingZ.Common;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.GameState;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Movement
{
    // Continuous dash reads intent and queues direction/dash work; it must not write player velocity or control flags here.
    public static class MovementContinuousDashService
    {
        private const string FeatureId = FeatureIds.MovementContinuousDash;
        private const int DoubleTapWindowTicks = 18;
        private const int DoubleTapHoldReleaseGraceTicks = 2;
        private static readonly TimeSpan PendingQueueTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static int _previousHeldDirection;
        private static bool _previousLeftHeld;
        private static bool _previousRightHeld;
        private static long _lastLeftPressTick;
        private static long _lastRightPressTick;
        private static int _lastTapDirection;
        private static long _lastTapTick;
        private static int _armedDirection;
        private static long _armedLastHeldTick;
        private static string _lastArmedCancelReason = string.Empty;
        private static long _armedCancelCount;
        private static MovementContinuousDashDiagnosticInfo _diagnostics = new MovementContinuousDashDiagnosticInfo();

        public static MovementContinuousDashDiagnosticInfo GetDiagnostics()
        {
            lock (DiagnosticsSyncRoot)
            {
                return _diagnostics.Clone();
            }
        }

        public static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState)
        {
            Tick(queue, snapshot, runtimeState, RuntimeSettingsSnapshotProvider.GetCurrent());
        }

        internal static void Tick(InputActionQueue queue, GameStateSnapshot snapshot, RuntimeState runtimeState, RuntimeSettingsSnapshot settingsSnapshot)
        {
            var tick = runtimeState == null ? 0 : runtimeState.UpdateCount;
            try
            {
                settingsSnapshot = settingsSnapshot ?? RuntimeSettingsSnapshotProvider.GetCurrent();
                var mode = settingsSnapshot.MovementContinuousDashMode;
                if (!settingsSnapshot.MovementContinuousDashEnabled)
                {
                    ResetArming("disabled");
                    object disabledPlayer;
                    TerrariaInputCompat.TryGetLocalPlayer(out disabledPlayer);
                    TerrariaDashCompat.ResetAllPulseState(disabledPlayer, "disabled");
                    RecordDecision(false, mode, "disabled", "disabled", tick, null, null, false, false, string.Empty);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
                {
                    ResetArming("notInWorld");
                    object worldPlayer;
                    TerrariaInputCompat.TryGetLocalPlayer(out worldPlayer);
                    TerrariaDashCompat.ResetAllPulseState(worldPlayer, "notInWorld");
                    RecordDecision(true, mode, "skipped", "notInWorld", tick, null, null, false, false, string.Empty);
                    return;
                }

                if (LegacyTextInput.IsAnyFocused)
                {
                    ResetArming("textInput:legacyUi");
                    RecordDecision(true, mode, "skipped", "textInput:legacyUi", tick, null, null, false, true, "legacyUi");
                    return;
                }

                bool textFocused;
                string textReason;
                TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
                if (textFocused)
                {
                    ResetArming("textInput:" + textReason);
                    RecordDecision(true, mode, "skipped", "textInput:" + textReason, tick, null, null, false, true, textReason);
                    return;
                }

                if (queue == null)
                {
                    RecordDecision(true, mode, "skipped", "queueUnavailable", tick, null, null, false, false, string.Empty);
                    return;
                }

                var inputFrame = MovementInputFrameCache.GetOrCreate(runtimeState, settingsSnapshot);
                object player;
                if (inputFrame == null || !inputFrame.TryGetPlayer(out player))
                {
                    if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                    {
                        ResetArming("localPlayerUnavailable");
                        RecordDecision(true, mode, "skipped", "localPlayerUnavailable", tick, null, null, false, false, string.Empty);
                        return;
                    }
                }

                TerrariaDashCompat.ResetImmediatePulseIfStale(player);

                DashInputProfile profile;
                string profileFailureReason = string.Empty;
                if (inputFrame == null || !inputFrame.TryGetDashProfile(out profile, out profileFailureReason))
                {
                    if (!TerrariaDashCompat.TryReadDashInputProfile(player, out profile))
                    {
                        var reason = FirstNonEmpty(TerrariaDashCompat.LastDashCompatError, profileFailureReason);
                        ResetArming("dashProfile:" + reason);
                        RecordDecision(true, mode, "skipped", "dashProfile:" + reason, tick, null, null, false, false, string.Empty);
                        return;
                    }
                }

                int effectiveDirection;
                var gateReason = EvaluateDirectionAndMode(profile, mode, tick, out effectiveDirection);
                if (!string.IsNullOrWhiteSpace(gateReason))
                {
                    RecordDecision(true, mode, "skipped", gateReason, tick, null, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                if (!profile.PlayerControllable)
                {
                    ResetArming("playerNotControllable");
                    RecordDecision(true, mode, "skipped", "playerNotControllable", tick, null, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                if (!profile.HasDashAbility)
                {
                    RecordDecision(true, mode, "skipped", "noDashAbility", tick, null, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                if (!profile.DashCooldownReady)
                {
                    RecordDecision(true, mode, "skipped", "dashCooldown", tick, null, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                if (TerrariaDashCompat.HasQueuedContinuousDashPulse())
                {
                    RecordDecision(true, mode, "skipped", "pulseQueued", tick, null, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                var queueSnapshot = queue.GetFastState();
                if (queue.IsSourcePendingOrRunning(FeatureId))
                {
                    RecordDecision(true, mode, "skipped", "sourceBusy", tick, queueSnapshot, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                if (queue.IsAnyChannelBusy(InputActionChannel.UseItem | InputActionChannel.BridgeItemUse | InputActionChannel.BridgeUseItemPulse))
                {
                    RecordDecision(true, mode, "skipped", "useItemChannelBusy", tick, queueSnapshot, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                var request = CreateRequest(mode, effectiveDirection, profile.DashDelay, profile.DashType, profile.DashAbilitySource, profile.CapabilitySummary, GetArmedDirection());

                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    RecordDecision(true, mode, "skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, profile, false, false, string.Empty, effectiveDirection);
                    return;
                }

                RecordDecision(true, mode, "submitted", string.Empty, tick, queueSnapshot, profile, true, false, string.Empty, effectiveDirection);
            }
            catch (Exception error)
            {
                ResetArming("exception:" + error.GetType().Name);
                RecordDecision(true, string.Empty, "exception", "exception:" + error.GetType().Name, tick, null, null, false, false, string.Empty);
                RuntimeDiagnostics.RecordError("MovementContinuousDashService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "movement-continuous-dash-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementContinuousDashService",
                    "Movement continuous dash tick failed; exception swallowed.", error);
            }
        }

        internal static InputActionRequest CreateRequestForTesting(string mode, int heldDirection)
        {
            return CreateRequest(mode, heldDirection, 0, 0, string.Empty, string.Empty, 0);
        }

        private static InputActionRequest CreateRequest(
            string mode,
            int heldDirection,
            int dashDelay,
            int dashType,
            string dashAbilitySource,
            string capabilitySummary,
            int armedDirection)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Dash,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureId,
                Description = "Movement continuous dash queues a vanilla dash pulse",
                QueueTimeout = PendingQueueTimeout,
                Timeout = TimeSpan.FromMilliseconds(250),
                IsExclusive = true
            };
            var safeMode = MovementContinuousDashModes.Normalize(mode);
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.MovementContinuousDash;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["ContinuousDashMode"] = safeMode;
            request.Metadata["DashDirection"] = heldDirection.ToString(CultureInfo.InvariantCulture);
            request.Metadata["DashDelay"] = dashDelay.ToString(CultureInfo.InvariantCulture);
            request.Metadata["DashType"] = dashType.ToString(CultureInfo.InvariantCulture);
            request.Metadata["DashAbilitySource"] = dashAbilitySource ?? string.Empty;
            request.Metadata["DashCapabilitySummary"] = capabilitySummary ?? string.Empty;
            request.Metadata["ArmedDirection"] = armedDirection.ToString(CultureInfo.InvariantCulture);
            request.AdmissionKey = FeatureId + "|Dash|" + safeMode + "|" + heldDirection.ToString(CultureInfo.InvariantCulture);
            return request;
        }

        private static string EvaluateDirectionAndMode(DashInputProfile profile, string mode, long tick, out int effectiveDirection)
        {
            effectiveDirection = 0;
            if (profile == null)
            {
                ResetArming("profileUnavailable");
                return "profileUnavailable";
            }

            var input = UpdateHorizontalInputState(profile, tick);
            effectiveDirection = input.Direction;

            if (input.Direction == 0)
            {
                if (string.Equals(mode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
                {
                    if (!profile.PlayerControllable)
                    {
                        ResetArming("playerNotControllable");
                        return "playerNotControllable";
                    }

                    return ReleaseDirectionForDoubleTap("directionNotHeld", tick);
                }

                ResetArming("directionNotHeld");
                return "directionNotHeld";
            }

            if (!string.Equals(mode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
            {
                ResetDoubleTapArming("modeHoldDirection");
                SetPreviousDirection(input.Direction);
                return string.Empty;
            }

            return UpdateDoubleTapArming(input.Direction, input.PressEdge, tick);
        }

        private static HorizontalInputState UpdateHorizontalInputState(DashInputProfile profile, long tick)
        {
            lock (SyncRoot)
            {
                var leftHeld = profile != null && profile.ControlLeft;
                var rightHeld = profile != null && profile.ControlRight;
                var leftPressed = leftHeld && !_previousLeftHeld;
                var rightPressed = rightHeld && !_previousRightHeld;

                if (leftPressed)
                {
                    _lastLeftPressTick = tick;
                }

                if (rightPressed)
                {
                    _lastRightPressTick = tick;
                }

                var direction = ResolveDominantDirectionLocked(profile, leftHeld, rightHeld);
                var pressedDirection = ResolvePressedDirectionLocked(profile, leftPressed, rightPressed);
                _previousLeftHeld = leftHeld;
                _previousRightHeld = rightHeld;

                return new HorizontalInputState(direction, pressedDirection != 0 && pressedDirection == direction);
            }
        }

        private static int ResolveDominantDirectionLocked(DashInputProfile profile, bool leftHeld, bool rightHeld)
        {
            if (leftHeld && !rightHeld)
            {
                return -1;
            }

            if (rightHeld && !leftHeld)
            {
                return 1;
            }

            if (!leftHeld && !rightHeld)
            {
                return 0;
            }

            if (_lastLeftPressTick > _lastRightPressTick)
            {
                return -1;
            }

            if (_lastRightPressTick > _lastLeftPressTick)
            {
                return 1;
            }

            if (profile != null && profile.CurrentDirection != 0 && profile.IsDirectionHeld(profile.CurrentDirection))
            {
                return profile.CurrentDirection;
            }

            return rightHeld ? 1 : -1;
        }

        private static int ResolvePressedDirectionLocked(DashInputProfile profile, bool leftPressed, bool rightPressed)
        {
            if (leftPressed && !rightPressed)
            {
                return -1;
            }

            if (rightPressed && !leftPressed)
            {
                return 1;
            }

            if (!leftPressed && !rightPressed)
            {
                return 0;
            }

            if (profile != null && profile.CurrentDirection != 0 && profile.IsDirectionHeld(profile.CurrentDirection))
            {
                return profile.CurrentDirection;
            }

            return 1;
        }

        private static string UpdateDoubleTapArming(int direction, bool pressEdge, long tick)
        {
            lock (SyncRoot)
            {
                if (pressEdge)
                {
                    if (_armedDirection != 0 && _armedDirection != direction)
                    {
                        CancelArmedLocked("directionSwitched");
                        _lastTapDirection = direction;
                        _lastTapTick = tick;
                        _previousHeldDirection = direction;
                        return "doubleTapNotArmed";
                    }

                    if (_lastTapDirection == direction &&
                        _lastTapTick > 0 &&
                        tick >= _lastTapTick &&
                        tick - _lastTapTick <= DoubleTapWindowTicks)
                    {
                        _armedDirection = direction;
                        _armedLastHeldTick = tick;
                        _lastArmedCancelReason = string.Empty;
                    }
                    else
                    {
                        _lastTapDirection = direction;
                        _lastTapTick = tick;
                    }
                }
                else if (_armedDirection != 0 && _armedDirection != direction)
                {
                    CancelArmedLocked("directionSwitched");
                    _previousHeldDirection = direction;
                    return "doubleTapNotArmed";
                }

                _previousHeldDirection = direction;
                if (_armedDirection == direction)
                {
                    _armedLastHeldTick = tick;
                }

                return _armedDirection == direction ? string.Empty : "doubleTapNotArmed";
            }
        }

        private static int GetArmedDirection()
        {
            lock (SyncRoot)
            {
                return _armedDirection;
            }
        }

        private static void SetPreviousDirection(int direction)
        {
            lock (SyncRoot)
            {
                _previousHeldDirection = direction;
            }
        }

        private static void ResetDoubleTapArming(string reason)
        {
            lock (SyncRoot)
            {
                _previousHeldDirection = 0;
                _lastTapDirection = 0;
                _lastTapTick = 0;
                CancelArmedLocked(reason);
            }
        }

        private static void ResetArming(string reason)
        {
            lock (SyncRoot)
            {
                _previousHeldDirection = 0;
                _previousLeftHeld = false;
                _previousRightHeld = false;
                _lastLeftPressTick = 0;
                _lastRightPressTick = 0;
                _lastTapDirection = 0;
                _lastTapTick = 0;
                CancelArmedLocked(reason);
            }
        }

        private static string ReleaseDirectionForDoubleTap(string reason, long tick)
        {
            lock (SyncRoot)
            {
                if (CanKeepArmedThroughTransientDirectionGapLocked(tick))
                {
                    _previousHeldDirection = 0;
                    return "directionNotHeldGrace";
                }

                _previousHeldDirection = 0;
                if (_armedDirection != 0)
                {
                    _lastTapDirection = 0;
                    _lastTapTick = 0;
                }

                CancelArmedLocked(reason);
                return reason ?? string.Empty;
            }
        }

        private static bool CanKeepArmedThroughTransientDirectionGapLocked(long tick)
        {
            return _armedDirection != 0 &&
                   _armedLastHeldTick > 0 &&
                   tick >= _armedLastHeldTick &&
                   tick - _armedLastHeldTick <= DoubleTapHoldReleaseGraceTicks;
        }

        private static void CancelArmedLocked(string reason)
        {
            if (_armedDirection != 0)
            {
                _armedCancelCount++;
            }

            _armedDirection = 0;
            _armedLastHeldTick = 0;
            _lastArmedCancelReason = reason ?? string.Empty;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : second ?? string.Empty;
        }

        private static void RecordDecision(
            bool enabled,
            string mode,
            string decision,
            string reason,
            long tick,
            InputActionQueueFastState queueSnapshot,
            DashInputProfile profile,
            bool submitted,
            bool textInputFocused,
            string textInputReason,
            int effectiveDirection = 0)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new MovementContinuousDashDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.Mode = MovementContinuousDashModes.Normalize(mode);
                current.LastTriggered = submitted;
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;
                current.RunningActionKind = queueSnapshot == null ? string.Empty : queueSnapshot.RunningActionKind ?? string.Empty;
                current.TextInputFocused = textInputFocused;
                current.TextInputReason = textInputReason ?? string.Empty;

                if (profile != null)
                {
                    current.PlayerControllable = profile.PlayerControllable;
                    current.LeftHeld = profile.ControlLeft;
                    current.RightHeld = profile.ControlRight;
                    current.HeldDirection = effectiveDirection != 0 ? effectiveDirection : profile.HeldDirection;
                    current.HasDashAbility = profile.HasDashAbility;
                    current.DashAbilitySource = profile.DashAbilitySource ?? string.Empty;
                    current.DashType = profile.DashType;
                    current.DashDelay = profile.DashDelay;
                    current.DashCooldownReady = profile.DashCooldownReady;
                    current.MountActive = profile.MountActive;
                    current.MountType = profile.MountType;
                    current.MountCanDashKnown = profile.MountCanDashKnown;
                    current.MountCanDash = profile.MountCanDash;
                    current.CapabilitySummary = profile.CapabilitySummary ?? string.Empty;
                }
                else
                {
                    current.PlayerControllable = false;
                    current.LeftHeld = false;
                    current.RightHeld = false;
                    current.HeldDirection = 0;
                    current.HasDashAbility = false;
                    current.DashAbilitySource = string.Empty;
                    current.DashType = 0;
                    current.DashDelay = 0;
                    current.DashCooldownReady = false;
                    current.MountActive = false;
                    current.MountType = -1;
                    current.MountCanDashKnown = false;
                    current.MountCanDash = false;
                    current.CapabilitySummary = string.Empty;
                }

                current.ArmedDirection = GetArmedDirection();
                lock (SyncRoot)
                {
                    current.ArmedCancelReason = _lastArmedCancelReason;
                    current.ArmedCancelCount = _armedCancelCount;
                }

                current.DashMovementHookInstalled = TerrariaDashCompat.DashMovementHookInstalled;
                current.DashMovementHookMessage = TerrariaDashCompat.DashMovementHookMessage;
                current.QueuedPulsePending = TerrariaDashCompat.HasQueuedContinuousDashPulse();
                current.LastPulseApplied = TerrariaDashCompat.LastPulseApplySucceeded;
                current.LastPulseDirection = TerrariaDashCompat.LastPulseApplyDirection;
                current.LastPulseUtc = TerrariaDashCompat.LastPulseApplyUtc;
                current.LastPulseMessage = TerrariaDashCompat.LastPulseApplyMessage;
                current.LastPulseWasFallback = TerrariaDashCompat.LastPulseWasFallback;
                current.LastPulseResetMessage = TerrariaDashCompat.LastPulseResetMessage;
                current.LastCompatError = TerrariaDashCompat.LastDashCompatError;

                if (submitted)
                {
                    current.SubmittedCount++;
                    current.LastTriggerUtc = DateTime.UtcNow;
                    current.LastTriggerDirection = effectiveDirection != 0 ? effectiveDirection : profile == null ? 0 : profile.HeldDirection;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }

        internal static void ResetArmingForTesting()
        {
            ResetArming("testing");
        }

        internal static string EvaluateDirectionAndModeForTesting(DashInputProfile profile, string mode, long tick)
        {
            int effectiveDirection;
            return EvaluateDirectionAndMode(profile, mode, tick, out effectiveDirection);
        }

        internal static string EvaluateDirectionAndModeForTesting(DashInputProfile profile, string mode, long tick, out int effectiveDirection)
        {
            return EvaluateDirectionAndMode(profile, mode, tick, out effectiveDirection);
        }

        internal static int ArmedDirectionForTesting
        {
            get { return GetArmedDirection(); }
        }

        private struct HorizontalInputState
        {
            internal HorizontalInputState(int direction, bool pressEdge)
            {
                Direction = direction;
                PressEdge = pressEdge;
            }

            internal readonly int Direction;
            internal readonly bool PressEdge;
        }
    }
}
