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
    public static class MovementSimulatedJumpService
    {
        private const string FeatureId = FeatureIds.MovementSimulatedMultiJump;
        private const int TriggerCooldownTicks = 2;
        private static readonly TimeSpan PendingQueueTimeout = TimeSpan.FromMilliseconds(150);
        private static readonly object SyncRoot = new object();
        private static readonly object DiagnosticsSyncRoot = new object();
        private static bool _releasedForGroundContact;
        private static bool _releasedForAerialOpportunity;
        private static long _nextAllowedTick;
        private static MovementSimulatedJumpDiagnosticInfo _diagnostics = new MovementSimulatedJumpDiagnosticInfo();

        public static MovementSimulatedJumpDiagnosticInfo GetDiagnostics()
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
                if (!settingsSnapshot.MovementSimulatedMultiJumpEnabled)
                {
                    MovementSimulatedJumpPulseCompat.CancelSimulatedJumpPulse(Guid.Empty, "movement.simulated_multi_jump disabled");
                    ResetState();
                    RecordDecision(false, "disabled", "disabled", tick, null, null, false, false, string.Empty);
                    return;
                }

                if (queue == null)
                {
                    RecordDecision(true, "skipped", "queueUnavailable", tick, null, null, false, false, string.Empty);
                    return;
                }

                if (snapshot == null || !snapshot.IsInWorld || snapshot.IsInMainMenu)
                {
                    ResetState();
                    RecordDecision(true, "skipped", "notInWorld", tick, null, null, false, false, string.Empty);
                    return;
                }

                if (LegacyTextInput.IsAnyFocused)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "textInput:legacyUi", tick, null, null, false, true, "legacyUi");
                    return;
                }

                bool textFocused;
                string textReason;
                TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);
                if (textFocused)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "textInput:" + textReason, tick, null, null, false, true, textReason);
                    return;
                }

                var queueSnapshot = queue.GetFastState();
                if (queue.IsSourcePendingOrRunning(FeatureId))
                {
                    RecordDecision(true, "skipped", "sourceBusy", tick, queueSnapshot, null, false, false, string.Empty);
                    return;
                }

                if (queue.IsAnyChannelBusy(InputActionChannel.UseItem | InputActionChannel.BridgeItemUse | InputActionChannel.BridgeUseItemPulse))
                {
                    RecordDecision(true, "skipped", "useItemChannelBusy", tick, queueSnapshot, null, false, false, string.Empty);
                    return;
                }

                object player;
                if (!TerrariaInputCompat.TryGetLocalPlayer(out player) || player == null)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "localPlayerUnavailable", tick, queueSnapshot, null, false, false, string.Empty);
                    return;
                }

                JumpInputProfile profile;
                if (!TerrariaInputCompat.TryReadJumpInputProfile(player, out profile))
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "jumpProfile:" + TerrariaInputCompat.LastInputCompatError, tick, queueSnapshot, null, false, false, string.Empty);
                    return;
                }

                if (!profile.PlayerControllable)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "playerNotControllable", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                if (!profile.ControlJump)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "jumpNotHeld", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                if (profile.ControlDown)
                {
                    ResetOpportunityFlags();
                    RecordDecision(true, "skipped", "downJumpPassthrough", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                if (!profile.HasAvailableJumpOpportunity)
                {
                    RefreshOpportunityFlags(profile);
                    RecordDecision(true, "skipped", "noAvailableJumpOpportunity", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                if (tick < GetNextAllowedTick())
                {
                    RecordDecision(true, "skipped", "cooldown", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                string triggerReason;
                if (!TryResolveTriggerReason(profile, out triggerReason))
                {
                    RecordDecision(true, "skipped", "opportunityAlreadyReleased", tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                var request = CreateRequest(triggerReason, profile);

                InputActionAdmissionResult admission;
                if (!queue.TryEnqueue(request, out admission))
                {
                    RecordDecision(true, "skipped", "admissionDenied:" + (admission == null ? "unknown" : admission.Reason), tick, queueSnapshot, profile, false, false, string.Empty);
                    return;
                }

                MarkReleased(profile, tick);
                RecordDecision(true, "submitted", string.Empty, tick, queueSnapshot, profile, true, false, string.Empty);
            }
            catch (Exception error)
            {
                ResetOpportunityFlags();
                RecordDecision(true, "exception", "exception:" + error.GetType().Name, tick, null, null, false, false, string.Empty);
                RuntimeDiagnostics.RecordError("MovementSimulatedJumpService.Tick", error);
                LogThrottle.ErrorThrottled(
                    "movement-simulated-jump-tick-failed",
                    TimeSpan.FromSeconds(10),
                    "MovementSimulatedJumpService",
                    "Movement simulated jump tick failed; exception swallowed.", error);
            }
        }

        internal static InputActionRequest CreateRequestForTesting(string triggerReason)
        {
            return CreateRequest(triggerReason ?? string.Empty, null);
        }

        private static InputActionRequest CreateRequest(string triggerReason, JumpInputProfile profile)
        {
            var request = new InputActionRequest
            {
                Kind = InputActionKind.Jump,
                Priority = InputActionPriority.Low,
                SourceFeatureId = FeatureId,
                Description = "Movement simulated multi-jump primes vanilla jump input",
                QueueTimeout = PendingQueueTimeout,
                Timeout = TimeSpan.FromMilliseconds(150),
                IsExclusive = true
            };
            request.Metadata[ActionMetadataKeys.Scenario] = ScenarioNames.MovementSimulatedMultiJump;
            request.Metadata[ActionMetadataKeys.SourceKind] = "Automation";
            request.Metadata["TriggerReason"] = triggerReason ?? string.Empty;
            request.AdmissionKey = FeatureId + "|Jump|" + (triggerReason ?? string.Empty);
            if (profile != null)
            {
                request.Metadata["JumpCapabilitySummary"] = profile.CapabilitySummary;
                request.Metadata["GroundedOrSliding"] = profile.GroundedOrSliding ? "true" : "false";
                request.Metadata["AerialJumpWindow"] = profile.AerialJumpWindow ? "true" : "false";
                request.Metadata["HasAirJump"] = profile.HasAirJump ? "true" : "false";
                request.Metadata["HasRocketJump"] = profile.HasRocketJump ? "true" : "false";
                request.Metadata["HasWingFlight"] = profile.HasWingFlight ? "true" : "false";
                request.Metadata["MountActive"] = profile.MountActive ? "true" : "false";
                request.Metadata["MountCanFly"] = profile.MountCanFly ? "true" : "false";
                request.Metadata["MountCanFlyKnown"] = profile.MountCanFlyKnown ? "true" : "false";
            }

            return request;
        }

        private static bool TryResolveTriggerReason(JumpInputProfile profile, out string triggerReason)
        {
            triggerReason = string.Empty;
            if (profile == null)
            {
                return false;
            }

            lock (SyncRoot)
            {
                if (profile.GroundedOrSliding)
                {
                    _releasedForAerialOpportunity = false;
                    if (_releasedForGroundContact)
                    {
                        return false;
                    }

                    triggerReason = "ground";
                    return true;
                }

                _releasedForGroundContact = false;
                if (_releasedForAerialOpportunity)
                {
                    return false;
                }

                if (profile.HasAirJump)
                {
                    triggerReason = "airJump";
                }
                else if (profile.HasRocketJump)
                {
                    triggerReason = "rocketBoots";
                }
                else if (profile.HasWingFlight)
                {
                    triggerReason = "wings";
                }
                else if (profile.HasMountOpportunity)
                {
                    triggerReason = "mount";
                }
                else
                {
                    triggerReason = "aerial";
                }

                return true;
            }
        }

        private static void MarkReleased(JumpInputProfile profile, long tick)
        {
            lock (SyncRoot)
            {
                if (profile != null && profile.GroundedOrSliding)
                {
                    _releasedForGroundContact = true;
                    _releasedForAerialOpportunity = false;
                }
                else
                {
                    _releasedForGroundContact = false;
                    _releasedForAerialOpportunity = true;
                }

                _nextAllowedTick = tick + TriggerCooldownTicks;
            }
        }

        private static void RefreshOpportunityFlags(JumpInputProfile profile)
        {
            lock (SyncRoot)
            {
                if (profile == null || !profile.ControlJump || profile.ControlDown || !profile.PlayerControllable)
                {
                    _releasedForGroundContact = false;
                    _releasedForAerialOpportunity = false;
                    return;
                }

                if (profile.GroundedOrSliding)
                {
                    _releasedForAerialOpportunity = false;
                }
                else
                {
                    _releasedForGroundContact = false;
                    if (!profile.AerialJumpWindow)
                    {
                        _releasedForAerialOpportunity = false;
                    }
                }
            }
        }

        private static long GetNextAllowedTick()
        {
            lock (SyncRoot)
            {
                return _nextAllowedTick;
            }
        }

        private static void ResetState()
        {
            lock (SyncRoot)
            {
                _releasedForGroundContact = false;
                _releasedForAerialOpportunity = false;
                _nextAllowedTick = 0;
            }
        }

        private static void ResetOpportunityFlags()
        {
            lock (SyncRoot)
            {
                _releasedForGroundContact = false;
                _releasedForAerialOpportunity = false;
            }
        }

        private static void RecordDecision(
            bool enabled,
            string decision,
            string reason,
            long tick,
            InputActionQueueFastState queueSnapshot,
            JumpInputProfile profile,
            bool submitted,
            bool textInputFocused,
            string textInputReason)
        {
            lock (DiagnosticsSyncRoot)
            {
                var current = _diagnostics == null ? new MovementSimulatedJumpDiagnosticInfo() : _diagnostics.Clone();
                current.Enabled = enabled;
                current.LastTriggered = submitted;
                current.LastDecision = decision ?? string.Empty;
                current.LastSkipReason = submitted ? string.Empty : reason ?? string.Empty;
                current.LastDecisionUtc = DateTime.UtcNow;
                current.LastTick = tick;
                current.PendingActionCount = queueSnapshot == null ? 0 : queueSnapshot.PendingCount;
                current.RunningActionKind = queueSnapshot == null ? string.Empty : queueSnapshot.RunningActionKind ?? string.Empty;
                current.ItemUseBridgeBusy = ItemUseBridge.PendingRequestId != Guid.Empty;
                current.TextInputFocused = textInputFocused;
                current.TextInputReason = textInputReason ?? string.Empty;

                if (profile != null)
                {
                    current.JumpHeld = profile.ControlJump;
                    current.DownHeld = profile.ControlDown;
                    current.PlayerControllable = profile.PlayerControllable;
                    current.AvailableJumpOpportunity = profile.HasAvailableJumpOpportunity;
                    current.GroundedOrSliding = profile.GroundedOrSliding;
                    current.AerialJumpWindow = profile.AerialJumpWindow;
                    current.HasAirJump = profile.HasAirJump;
                    current.HasRocketJump = profile.HasRocketJump;
                    current.HasWingFlight = profile.HasWingFlight;
                    current.MountActive = profile.MountActive;
                    current.MountCanFly = profile.MountCanFly;
                    current.MountCanFlyKnown = profile.MountCanFlyKnown;
                    current.CapabilitySummary = profile.CapabilitySummary;
                }
                else
                {
                    current.JumpHeld = false;
                    current.DownHeld = false;
                    current.PlayerControllable = false;
                    current.AvailableJumpOpportunity = false;
                    current.GroundedOrSliding = false;
                    current.AerialJumpWindow = false;
                    current.HasAirJump = false;
                    current.HasRocketJump = false;
                    current.HasWingFlight = false;
                    current.MountActive = false;
                    current.MountCanFly = false;
                    current.MountCanFlyKnown = false;
                    current.CapabilitySummary = string.Empty;
                }

                if (submitted)
                {
                    current.SubmittedCount++;
                    current.LastTriggerUtc = DateTime.UtcNow;
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
