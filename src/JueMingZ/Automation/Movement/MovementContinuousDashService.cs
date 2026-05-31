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
    public static class MovementContinuousDashService
    {
        private const string FeatureId = FeatureIds.MovementContinuousDash;
        private const int DoubleTapWindowTicks = 18;
        private static readonly TimeSpan PendingQueueTimeout = TimeSpan.FromMilliseconds(200);
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static int _previousHeldDirection;
        private static int _lastTapDirection;
        private static long _lastTapTick;
        private static int _armedDirection;
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

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    ResetArming("localPlayerUnavailable");
                    RecordDecision(true, mode, "skipped", "localPlayerUnavailable", tick, null, null, false, false, string.Empty);
                    return;
                }

                TerrariaDashCompat.ResetImmediatePulseIfStale(player);

                DashInputProfile profile;
                if (!TerrariaDashCompat.TryReadDashInputProfile(player, out profile))
                {
                    ResetArming("dashProfile:" + TerrariaDashCompat.LastDashCompatError);
                    RecordDecision(true, mode, "skipped", "dashProfile:" + TerrariaDashCompat.LastDashCompatError, tick, null, null, false, false, string.Empty);
                    return;
                }

                var gateReason = EvaluateDirectionAndMode(profile, mode, tick);
                if (!string.IsNullOrWhiteSpace(gateReason))
                {
                    RecordDecision(true, mode, "skipped", gateReason, tick, null, profile, false, false, string.Empty);
                    return;
                }

                if (!profile.PlayerControllable)
                {
                    ResetArming("playerNotControllable");
                    RecordDecision(true, mode, "skipped", "playerNotControllable", tick, null, profile, false, false, string.Empty);
                    return;
                }

                if (!profile.HasDashAbility)
                {
                    RecordDecision(true, mode, "skipped", "noDashAbility", tick, null, profile, false, false, string.Empty);
                    return;
                }

                if (!profile.DashCooldownReady)
                {
                    RecordDecision(true, mode, "skipped", "dashCooldown", tick, null, profile, false, false, string.Empty);
                    return;
                }

                if (TerrariaDashCompat.HasQueuedContinuousDashPulse())
                {
                    RecordDecision(true, mode, "skipped", "pulseQueued", tick, null, profile, false, false, string.Empty);
                    return;
                }

                var queueSnapshot = queue.GetFastState();
                if (queue.IsSourcePendingOrRunning(FeatureId))
                {
                    RecordDecision(true, mode, "skipped", "sourceBusy", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                if (queue.IsAnyChannelBusy(InputActionChannel.UseItem | InputActionChannel.BridgeItemUse | InputActionChannel.BridgeUseItemPulse))
                {
                    RecordDecision(true, mode, "skipped", "useItemChannelBusy", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                var request = CreateRequest(mode, profile.HeldDirection, profile.DashDelay, profile.DashType, profile.DashAbilitySource, profile.CapabilitySummary, GetArmedDirection());

                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    RecordDecision(true, mode, "skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                RecordDecision(true, mode, "submitted", string.Empty, tick, queueSnapshot, profile, true, false, string.Empty);
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

        private static string EvaluateDirectionAndMode(DashInputProfile profile, string mode, long tick)
        {
            if (profile == null)
            {
                ResetArming("profileUnavailable");
                return "profileUnavailable";
            }

            if (!profile.ExclusiveHorizontalHeld)
            {
                if (profile.ControlLeft && profile.ControlRight)
                {
                    ResetArming("bothDirectionsHeld");
                    return "bothDirectionsHeld";
                }

                if (string.Equals(mode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
                {
                    ReleaseDirectionForDoubleTap("directionNotHeld");
                    return "directionNotHeld";
                }

                ResetArming("directionNotHeld");
                return profile.ControlLeft && profile.ControlRight ? "bothDirectionsHeld" : "directionNotHeld";
            }

            if (!string.Equals(mode, MovementContinuousDashModes.DoubleTapAndHold, StringComparison.Ordinal))
            {
                ResetArming("modeHoldDirection");
                SetPreviousDirection(profile.HeldDirection);
                return string.Empty;
            }

            return UpdateDoubleTapArming(profile.HeldDirection, tick);
        }

        private static string UpdateDoubleTapArming(int direction, long tick)
        {
            lock (SyncRoot)
            {
                var edge = direction != 0 && direction != _previousHeldDirection;
                if (edge)
                {
                    if (_previousHeldDirection != 0 && _previousHeldDirection != direction)
                    {
                        CancelArmedLocked("directionSwitched");
                        _lastTapDirection = direction;
                        _lastTapTick = tick;
                        _previousHeldDirection = direction;
                        return "doubleTapNotArmed";
                    }

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
                        _lastArmedCancelReason = string.Empty;
                    }
                    else
                    {
                        _lastTapDirection = direction;
                        _lastTapTick = tick;
                    }
                }

                _previousHeldDirection = direction;
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

        private static void ResetArming(string reason)
        {
            lock (SyncRoot)
            {
                _previousHeldDirection = 0;
                _lastTapDirection = 0;
                _lastTapTick = 0;
                CancelArmedLocked(reason);
            }
        }

        private static void ReleaseDirectionForDoubleTap(string reason)
        {
            lock (SyncRoot)
            {
                _previousHeldDirection = 0;
                if (_armedDirection != 0)
                {
                    _lastTapDirection = 0;
                    _lastTapTick = 0;
                }

                CancelArmedLocked(reason);
            }
        }

        private static void CancelArmedLocked(string reason)
        {
            if (_armedDirection != 0)
            {
                _armedCancelCount++;
            }

            _armedDirection = 0;
            _lastArmedCancelReason = reason ?? string.Empty;
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
            string textInputReason)
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
                    current.HeldDirection = profile.HeldDirection;
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
                    current.LastTriggerDirection = profile == null ? 0 : profile.HeldDirection;
                }
                else
                {
                    current.SkippedCount++;
                }

                _diagnostics = current;
            }
        }
    }
}
