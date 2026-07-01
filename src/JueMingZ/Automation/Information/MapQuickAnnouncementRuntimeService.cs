using System;
using System.Runtime.InteropServices;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Input.Hotkeys;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementRuntimeService
    {
        private const ulong MouseHoverPendingTtlUpdates = 6;
        private static readonly object SyncRoot = new object();
        private static readonly MapQuickAnnouncementRuntimeState RuntimeState =
            new MapQuickAnnouncementRuntimeState();

        public static void UpdatePrefixGuard()
        {
            try
            {
                MapQuickAnnouncementRuntimeResult result;
                lock (SyncRoot)
                {
                    result = Tick(BuildCurrentInput(), RuntimeState, BuildCurrentPorts());
                }

                if (result != null &&
                    result.InputConsumeAttempted &&
                    !result.InputConsumed)
                {
                    LogThrottle.WarnThrottled(
                        "map-quick-announcement-input-consume-failed",
                        TimeSpan.FromSeconds(10),
                        "MapQuickAnnouncementRuntime",
                        "Quick announcement triggered but mouse input consumption failed: " +
                        (result.InputConsumeMessage ?? string.Empty));
                }
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.RecordError("MapQuickAnnouncementRuntimeService.UpdatePrefixGuard", error);
                LogThrottle.ErrorThrottled(
                    "map-quick-announcement-runtime-error",
                    TimeSpan.FromSeconds(10),
                    "MapQuickAnnouncementRuntime",
                    "Quick announcement runtime trigger failed; exception swallowed.", error);
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                RuntimeState.Reset();
                MapQuickAnnouncementDiagnostics.ResetForTesting();
            }
        }

        internal static MapQuickAnnouncementRuntimeResult TickForTesting(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimePorts ports)
        {
            return TickForTesting(input, new MapQuickAnnouncementRuntimeState(), ports);
        }

        internal static MapQuickAnnouncementRuntimeResult TickForTesting(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementHotkeyStateMachine stateMachine,
            MapQuickAnnouncementRuntimePorts ports)
        {
            return TickForTesting(input, new MapQuickAnnouncementRuntimeState(stateMachine), ports);
        }

        internal static MapQuickAnnouncementRuntimeResult TickForTesting(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimeState state,
            MapQuickAnnouncementRuntimePorts ports)
        {
            return Tick(input, state ?? new MapQuickAnnouncementRuntimeState(), ports);
        }

        private static MapQuickAnnouncementRuntimeResult Tick(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimeState state,
            MapQuickAnnouncementRuntimePorts ports)
        {
            input = input ?? new MapQuickAnnouncementRuntimeInput();
            ports = ports ?? new MapQuickAnnouncementRuntimePorts();
            state = state ?? new MapQuickAnnouncementRuntimeState();

            var result = new MapQuickAnnouncementRuntimeResult
            {
                ResultCode = "skipped"
            };

            if (!input.FeatureEnabled)
            {
                state.Reset();
                return result.Skip("disabled");
            }

            if (!input.IsInWorld)
            {
                state.Reset();
                return result.Skip("notInWorld");
            }

            if (!input.GameInputAvailable)
            {
                state.ClearPending();
                result.HotkeyState = ResolveHotkeyState(input, state, ports, false);
                if (TryRecordUnifiedBlockedTrigger(input, state, result, ports.UtcNow))
                {
                    return result;
                }

                return result.Skip("gameInputUnavailable");
            }

            var hotkeyState = ResolveHotkeyState(input, state, ports, true);
            result.HotkeyState = hotkeyState;
            result.TriggerKey = ResolveTriggerKey(input);
            if (TryRecordUnifiedBlockedTrigger(input, state, result, ports.UtcNow))
            {
                return result;
            }

            string blockedReason;
            if (IsResponseBlocked(input, out blockedReason))
            {
                state.ClearPending();
                result.Skip(blockedReason);
                if (hotkeyState != null && hotkeyState.Triggered)
                {
                    MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                }

                return result;
            }

            if (state.PendingRequest != null)
            {
                var pendingResult = ContinuePendingMouseRequest(input, state, ports);
                if (pendingResult != null)
                {
                    return pendingResult;
                }

                return result.Skip("pendingUiHover");
            }

            if (hotkeyState == null || !hotkeyState.IsValid)
            {
                state.ClearPending();
                return result.Skip("invalidHotkey");
            }

            if (!hotkeyState.Triggered)
            {
                return result.Skip(hotkeyState.CombinationHeld ? "latchedUntilRelease" : "notTriggered");
            }

            result.Triggered = true;
            result.ResultCode = "triggered";
            var triggerKey = ResolveTriggerKey(input);
            if (IsMouseTrigger(triggerKey))
            {
                result.InputConsumeAttempted = true;
                var consume = ports.ConsumeTriggerInput == null
                    ? MapQuickAnnouncementInputConsumeResult.Failed("consume port unavailable")
                    : ports.ConsumeTriggerInput(triggerKey);
                consume = consume ?? MapQuickAnnouncementInputConsumeResult.Failed("consume result unavailable");
                result.InputConsumed = consume.Succeeded;
                result.InputConsumeMessage = consume.Message ?? string.Empty;

                var triggerContext = ports.CaptureTriggerContext == null
                    ? MapQuickAnnouncementTriggerContext.Failed("trigger context port unavailable")
                    : ports.CaptureTriggerContext();
                triggerContext = triggerContext ?? MapQuickAnnouncementTriggerContext.Failed("trigger context unavailable");
                if (!triggerContext.Succeeded)
                {
                    result.ResultCode = "resolveFailed";
                    result.SkipReason = string.IsNullOrWhiteSpace(triggerContext.FailureReason)
                        ? "triggerContextUnavailable"
                        : triggerContext.FailureReason;
                    MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                    return result;
                }

                state.PendingRequest = MapQuickAnnouncementPendingRequest.Create(
                    triggerKey,
                    hotkeyState == null ? string.Empty : hotkeyState.Signature,
                    triggerContext,
                    input,
                    consume,
                    ports.UtcNow);

                var pendingResult = ContinuePendingMouseRequest(input, state, ports);
                if (pendingResult != null)
                {
                    return pendingResult;
                }

                result.ResultCode = "pendingUiHover";
                result.SkipReason = "waitingForUiHover";
                result.PendingRequestActive = true;
                result.PendingState = "waitingForUiHover";
                result.UiHoverReadStatus = state.PendingRequest == null
                    ? string.Empty
                    : state.PendingRequest.LastUiHoverReadStatus;
                MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                return result;
            }

            return ResolveAndDeliver(
                result,
                ports.ResolveCurrent == null
                ? MapQuickAnnouncementResolveAttempt.Failed("resolve port unavailable")
                : ports.ResolveCurrent(),
                input.ColorHex,
                input.CooldownMilliseconds,
                input.AirCooldownMilliseconds,
                ports);
        }

        private static MapQuickAnnouncementHotkeyState ResolveHotkeyState(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimeState state,
            MapQuickAnnouncementRuntimePorts ports,
            bool allowTokenReads)
        {
            input = input ?? new MapQuickAnnouncementRuntimeInput();
            state = state ?? new MapQuickAnnouncementRuntimeState();
            if (input.UseUnifiedHotkeyRuntime)
            {
                return CreateHotkeyStateFromUnifiedTrigger(input.UnifiedHotkeyTrigger, input.UnifiedHotkeySignature);
            }

            Func<string, bool> isTokenDown = allowTokenReads
                ? new Func<string, bool>(token => IsTokenDown(ports, token))
                : new Func<string, bool>(_ => false);
            return state.HotkeyStateMachine.Update(input.Hotkey, isTokenDown);
        }

        private static bool TryRecordUnifiedBlockedTrigger(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimeState state,
            MapQuickAnnouncementRuntimeResult result,
            DateTime utcNow)
        {
            if (input == null ||
                result == null ||
                !input.UseUnifiedHotkeyRuntime ||
                !input.UnifiedHotkeyTrigger.PressedEdge ||
                !string.Equals(input.UnifiedHotkeyTrigger.ResultCode, "blocked", StringComparison.Ordinal))
            {
                return false;
            }

            if (state != null)
            {
                state.ClearPending();
            }

            result.Triggered = true;
            result.ResultCode = "blocked";
            result.SkipReason = string.IsNullOrWhiteSpace(input.UnifiedHotkeyTrigger.Reason)
                ? "blocked"
                : input.UnifiedHotkeyTrigger.Reason;
            result.TriggerKey = ResolveTriggerKey(input);
            if (result.HotkeyState == null)
            {
                result.HotkeyState = CreateHotkeyStateFromUnifiedTrigger(
                    input.UnifiedHotkeyTrigger,
                    input.UnifiedHotkeySignature);
            }

            MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, utcNow);
            return true;
        }

        private static MapQuickAnnouncementHotkeyState CreateHotkeyStateFromUnifiedTrigger(
            UnifiedHotkeyRuntimeTriggerResult trigger,
            string signature)
        {
            var hasBinding = trigger.Binding != null;
            var triggered = string.Equals(trigger.ResultCode, "triggered", StringComparison.Ordinal);
            return new MapQuickAnnouncementHotkeyState
            {
                IsValid = hasBinding,
                Slot1Down = trigger.Down,
                Slot2Down = trigger.Down,
                TriggerDown = trigger.Down,
                CombinationHeld = hasBinding && trigger.Down,
                TriggerPressedEdge = trigger.PressedEdge,
                Triggered = triggered,
                LatchedUntilRelease = hasBinding && trigger.Down && !triggered,
                Signature = string.IsNullOrWhiteSpace(signature) ? trigger.Display : signature
            };
        }

        private static string ResolveTriggerKey(MapQuickAnnouncementRuntimeInput input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            return input.UseUnifiedHotkeyRuntime
                ? input.UnifiedHotkeyTriggerKey ?? string.Empty
                : (input.Hotkey == null ? string.Empty : input.Hotkey.TriggerKey ?? string.Empty);
        }

        private static MapQuickAnnouncementRuntimeResult ContinuePendingMouseRequest(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimeState state,
            MapQuickAnnouncementRuntimePorts ports)
        {
            var pending = state == null ? null : state.PendingRequest;
            if (pending == null)
            {
                return null;
            }

            var result = CreateResultFromPending(pending);
            var currentGameUpdateCount = input == null ? pending.TriggerGameUpdateCount : input.GameUpdateCount;
            if (currentGameUpdateCount < pending.TriggerGameUpdateCount)
            {
                currentGameUpdateCount = pending.TriggerGameUpdateCount;
            }

            var uiResolve = ports.ResolvePendingUi == null
                ? MapQuickAnnouncementResolveAttempt.Failed("ui hover resolve port unavailable")
                : ports.ResolvePendingUi(pending, currentGameUpdateCount);
            uiResolve = uiResolve ?? MapQuickAnnouncementResolveAttempt.Failed("ui hover resolve result unavailable");
            pending.UpdateLastUiHoverReadStatus(uiResolve.UiHoverReadStatus);
            result.UiHoverReadStatus = uiResolve.UiHoverReadStatus;
            if (uiResolve.Succeeded && uiResolve.Result != null)
            {
                state.ClearPending();
                result.PendingState = uiResolve.Result.SuppressDelivery &&
                                      string.Equals(uiResolve.Result.FailureReason, "uiEmptySlot", StringComparison.Ordinal)
                    ? "uiEmptySlot"
                    : "resolvedUiHover";
                return ResolveAndDeliver(
                    result,
                    uiResolve,
                    pending.ColorHex,
                    pending.CooldownMilliseconds,
                    pending.AirCooldownMilliseconds,
                    ports);
            }

            if (!IsPendingExpired(pending, currentGameUpdateCount))
            {
                return null;
            }

            var fallbackResolve = ports.ResolvePendingFallback == null
                ? MapQuickAnnouncementResolveAttempt.Failed("pending fallback resolve port unavailable")
                : ports.ResolvePendingFallback(pending, currentGameUpdateCount);
            state.ClearPending();
            result.PendingState = "expiredFallback";
            return ResolveAndDeliver(
                result,
                fallbackResolve,
                pending.ColorHex,
                pending.CooldownMilliseconds,
                pending.AirCooldownMilliseconds,
                ports);
        }

        private static MapQuickAnnouncementRuntimeResult ResolveAndDeliver(
            MapQuickAnnouncementRuntimeResult result,
            MapQuickAnnouncementResolveAttempt resolve,
            string colorHex,
            int cooldownMilliseconds,
            int airCooldownMilliseconds,
            MapQuickAnnouncementRuntimePorts ports)
        {
            resolve = resolve ?? MapQuickAnnouncementResolveAttempt.Failed("resolve result unavailable");
            result.ResolveAttempted = true;
            if (!resolve.Succeeded || resolve.Result == null)
            {
                result.ResultCode = "resolveFailed";
                result.SkipReason = string.IsNullOrWhiteSpace(resolve.FailureReason)
                    ? "resolveFailed"
                    : resolve.FailureReason;
                result.UiHoverReadStatus = FirstNonEmpty(result.UiHoverReadStatus, resolve.UiHoverReadStatus);
                MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                return result;
            }

            result.UiHoverReadStatus = FirstNonEmpty(result.UiHoverReadStatus, resolve.UiHoverReadStatus);
            result.ResolveDetail = resolve.Result.Detail ?? string.Empty;
            result.TargetKind = resolve.Result.Kind.ToString();
            result.TargetName = resolve.Result.TargetName ?? string.Empty;
            result.TargetSummary = resolve.Result.Body ?? string.Empty;
            result.TargetCount = resolve.Result.TargetCount;
            result.IsAir = resolve.Result.Kind == MapQuickAnnouncementTargetKind.Air;
            result.VisibilityVerdict = resolve.Result.VisibilityVerdict;
            result.VisibilityReason = resolve.Result.VisibilityReason;
            result.VisibleLayers = resolve.Result.VisibleLayers;
            result.BlockedLayers = resolve.Result.BlockedLayers;
            result.CircuitOnly = resolve.Result.CircuitOnly;
            result.EchoGate = resolve.Result.EchoGate;
            result.InvisibleAir = resolve.Result.InvisibleAir;
            result.VisibilityUnavailableReason = resolve.Result.VisibilityUnavailableReason;
            if (resolve.Result.SuppressDelivery)
            {
                result.ResultCode = string.IsNullOrWhiteSpace(resolve.Result.FailureReason)
                    ? "suppressed"
                    : resolve.Result.FailureReason;
                result.SkipReason = result.ResultCode;
                MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                return result;
            }

            var deliveryOptions = MapQuickAnnouncementDeliveryOptions.Create(
                colorHex,
                cooldownMilliseconds,
                airCooldownMilliseconds);
            var delivery = ports.Deliver == null
                ? new MapQuickAnnouncementDeliveryResult
                {
                    ResultCode = "sendFailed",
                    FailureReason = "delivery port unavailable"
                }
                : ports.Deliver(resolve.Result, deliveryOptions, ports.UtcNow);
            delivery = delivery ?? new MapQuickAnnouncementDeliveryResult
            {
                ResultCode = "sendFailed",
                FailureReason = "delivery result unavailable"
            };

            result.DeliveryResult = delivery;
            result.DeliveryResultCode = delivery.ResultCode ?? string.Empty;
            result.DeliveryFailureReason = delivery.FailureReason ?? string.Empty;
            result.CooldownRemainingMilliseconds = delivery.CooldownRemainingMilliseconds;
            result.Delivered = delivery.Sent;
            result.ResultCode = delivery.ResultCode ?? string.Empty;
            MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
            return result;
        }

        private static bool IsPendingExpired(MapQuickAnnouncementPendingRequest pending, ulong currentGameUpdateCount)
        {
            if (pending == null)
            {
                return true;
            }

            if (currentGameUpdateCount < pending.TriggerGameUpdateCount)
            {
                return false;
            }

            return currentGameUpdateCount - pending.TriggerGameUpdateCount >= MouseHoverPendingTtlUpdates;
        }

        private static MapQuickAnnouncementRuntimeResult CreateResultFromPending(MapQuickAnnouncementPendingRequest pending)
        {
            var result = new MapQuickAnnouncementRuntimeResult
            {
                Triggered = true,
                ResultCode = "pendingUiHover",
                TriggerKey = pending == null ? string.Empty : pending.TriggerKey,
                InputConsumeAttempted = pending != null && pending.InputConsumeAttempted,
                InputConsumed = pending != null && pending.InputConsumed,
                InputConsumeMessage = pending == null ? string.Empty : pending.InputConsumeMessage,
                PendingRequestActive = true,
                PendingState = "active",
                UiHoverReadStatus = pending == null ? string.Empty : pending.LastUiHoverReadStatus
            };

            if (pending != null)
            {
                result.HotkeyState = new MapQuickAnnouncementHotkeyState
                {
                    IsValid = true,
                    Slot1Down = true,
                    Slot2Down = true,
                    TriggerDown = true,
                    CombinationHeld = true,
                    TriggerPressedEdge = true,
                    Triggered = true,
                    LatchedUntilRelease = true,
                    Signature = pending.HotkeySummary
                };
            }

            return result;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? (second ?? string.Empty) : first;
        }

        private static bool IsResponseBlocked(MapQuickAnnouncementRuntimeInput input, out string reason)
        {
            reason = string.Empty;
            if (input.LegacyUiVisible)
            {
                reason = "legacyUiVisible";
                return true;
            }

            if (input.LegacyUiActiveInteraction)
            {
                reason = "legacyUiInteraction";
                return true;
            }

            if (input.LegacyTextInputFocused)
            {
                reason = "legacyTextInput";
                return true;
            }

            if (input.SearchItemSelectionPending)
            {
                reason = "searchItemSelection";
                return true;
            }

            if (input.TerrariaTextInputFocused)
            {
                reason = string.IsNullOrWhiteSpace(input.TerrariaTextInputReason)
                    ? "terrariaTextInput"
                    : "terrariaTextInput:" + input.TerrariaTextInputReason;
                return true;
            }

            if (input.NpcChatOpen)
            {
                reason = "npcChat";
                return true;
            }

            return false;
        }

        private static bool IsTokenDown(MapQuickAnnouncementRuntimePorts ports, string token)
        {
            if (ports != null && ports.IsTokenDown != null)
            {
                return ports.IsTokenDown(token);
            }

            return IsPhysicalTokenDown(token);
        }

        private static bool IsPhysicalTokenDown(string token)
        {
            int virtualKey;
            if (!TerrariaMainCompat.AllowsInputProcessing ||
                !MapQuickAnnouncementHotkeyTokens.TryGetVirtualKey(token, out virtualKey))
            {
                return false;
            }

            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        private static bool IsMouseTrigger(string token)
        {
            return string.Equals(token, "MouseLeft", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseRight", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseMiddle", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse4", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseX1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "MouseX2", StringComparison.OrdinalIgnoreCase);
        }

        private static MapQuickAnnouncementRuntimeInput BuildCurrentInput()
        {
            var settings = RuntimeSettingsSnapshotProvider.GetCurrent();
            // The old three AppSettings slots remain a UI display compatibility shape; production
            // trigger evaluation consumes the canonical map.quick_announcement.trigger binding.
            var unifiedTrigger = UnifiedHotkeyRuntimeService.QueryBinding(UnifiedHotkeyBindingIds.MapQuickAnnouncementTrigger);
            bool textFocused;
            string textReason;
            TerrariaInputCompat.TryReadTextInputFocus(out textFocused, out textReason);

            return new MapQuickAnnouncementRuntimeInput
            {
                FeatureEnabled = settings.MapQuickAnnouncementEnabled,
                IsInWorld = TerrariaMainCompat.IsWorldReady,
                GameInputAvailable = TerrariaMainCompat.AllowsInputProcessing,
                LegacyUiVisible = LegacyMainUiState.Visible,
                LegacyUiActiveInteraction = LegacyUiInput.IsActiveInteraction(),
                LegacyTextInputFocused = LegacyTextInput.IsAnyFocused,
                SearchItemSelectionPending = SearchItemQueryUiState.IsSelectionPending,
                TerrariaTextInputFocused = textFocused,
                TerrariaTextInputReason = textReason,
                NpcChatOpen = !string.IsNullOrEmpty(TerrariaMainCompat.NpcChatText),
                GameUpdateCount = TerrariaMainCompat.GameUpdateCount,
                Hotkey = new MapQuickAnnouncementHotkey(string.Empty, string.Empty, ResolveUnifiedTriggerKey(unifiedTrigger)),
                UseUnifiedHotkeyRuntime = true,
                UnifiedHotkeyTrigger = unifiedTrigger,
                UnifiedHotkeyTriggerKey = ResolveUnifiedTriggerKey(unifiedTrigger),
                UnifiedHotkeySignature = unifiedTrigger.Display,
                ColorHex = settings.MapQuickAnnouncementColorHex,
                CooldownMilliseconds = settings.MapQuickAnnouncementCooldownMilliseconds,
                AirCooldownMilliseconds = settings.MapQuickAnnouncementAirCooldownMilliseconds
            };
        }

        private static string ResolveUnifiedTriggerKey(UnifiedHotkeyRuntimeTriggerResult trigger)
        {
            var primary = trigger.Binding == null ||
                          trigger.Binding.Chord == null ||
                          trigger.Binding.Chord.PrimaryKey == null
                ? string.Empty
                : trigger.Binding.Chord.PrimaryKey.Canonical;
            switch (primary)
            {
                case "MouseX1":
                    return "Mouse4";
                case "MouseX2":
                    return "Mouse5";
                case "Esc":
                    return "Escape";
                default:
                    return primary ?? string.Empty;
            }
        }

        private static MapQuickAnnouncementRuntimePorts BuildCurrentPorts()
        {
            return new MapQuickAnnouncementRuntimePorts
            {
                UtcNow = DateTime.UtcNow,
                ResolveCurrent = ResolveCurrent,
                CaptureTriggerContext = CaptureTriggerContext,
                ResolvePendingUi = ResolvePendingUi,
                ResolvePendingFallback = ResolvePendingFallback,
                ConsumeTriggerInput = ConsumeTriggerInput,
                Deliver = (resolveResult, options, utcNow) =>
                    MapQuickAnnouncementDeliveryService.Shared.TryDeliver(resolveResult, options, utcNow)
            };
        }

        private static MapQuickAnnouncementResolveAttempt ResolveCurrent()
        {
            MapQuickAnnouncementResolveResult result;
            string skipReason;
            if (!MapQuickAnnouncementTargetResolver.TryResolveCurrent(out result, out skipReason))
            {
                return MapQuickAnnouncementResolveAttempt.Failed(skipReason);
            }

            return MapQuickAnnouncementResolveAttempt.Success(result);
        }

        private static MapQuickAnnouncementTriggerContext CaptureTriggerContext()
        {
            MapQuickAnnouncementTriggerContext context;
            string skipReason;
            return MapQuickAnnouncementTargetResolver.TryCaptureCurrentTriggerContext(out context, out skipReason)
                ? context
                : MapQuickAnnouncementTriggerContext.Failed(skipReason);
        }

        private static MapQuickAnnouncementResolveAttempt ResolvePendingUi(
            MapQuickAnnouncementPendingRequest pending,
            ulong currentGameUpdateCount)
        {
            return MapQuickAnnouncementTargetResolver.ResolveUiHoverFromPending(pending, currentGameUpdateCount);
        }

        private static MapQuickAnnouncementResolveAttempt ResolvePendingFallback(
            MapQuickAnnouncementPendingRequest pending,
            ulong currentGameUpdateCount)
        {
            MapQuickAnnouncementResolveResult result;
            string skipReason;
            if (!MapQuickAnnouncementTargetResolver.TryResolvePendingFallback(
                    pending,
                    currentGameUpdateCount,
                    out result,
                    out skipReason))
            {
                return MapQuickAnnouncementResolveAttempt.Failed(skipReason);
            }

            return MapQuickAnnouncementResolveAttempt.Success(result);
        }

        private static MapQuickAnnouncementInputConsumeResult ConsumeTriggerInput(string triggerKey)
        {
            string message;
            return TerrariaUiMouseCompat.TryConsumeMouseTriggerInput(triggerKey, out message)
                ? MapQuickAnnouncementInputConsumeResult.Success(message)
                : MapQuickAnnouncementInputConsumeResult.Failed(message);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }

    internal sealed class MapQuickAnnouncementRuntimeInput
    {
        public MapQuickAnnouncementRuntimeInput()
        {
            Hotkey = MapQuickAnnouncementSettings.CreateDefaultHotkey();
            ColorHex = MapQuickAnnouncementSettings.DefaultAnnouncementColorHex;
            CooldownMilliseconds = MapQuickAnnouncementSettings.DefaultCooldownMilliseconds;
            AirCooldownMilliseconds = MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds;
            TerrariaTextInputReason = string.Empty;
            UnifiedHotkeyTriggerKey = string.Empty;
            UnifiedHotkeySignature = string.Empty;
        }

        public bool FeatureEnabled { get; set; }
        public bool IsInWorld { get; set; }
        public bool GameInputAvailable { get; set; }
        public bool LegacyUiVisible { get; set; }
        public bool LegacyUiActiveInteraction { get; set; }
        public bool LegacyTextInputFocused { get; set; }
        public bool SearchItemSelectionPending { get; set; }
        public bool TerrariaTextInputFocused { get; set; }
        public string TerrariaTextInputReason { get; set; }
        public bool NpcChatOpen { get; set; }
        public ulong GameUpdateCount { get; set; }
        public MapQuickAnnouncementHotkey Hotkey { get; set; }
        public bool UseUnifiedHotkeyRuntime { get; set; }
        public UnifiedHotkeyRuntimeTriggerResult UnifiedHotkeyTrigger { get; set; }
        public string UnifiedHotkeyTriggerKey { get; set; }
        public string UnifiedHotkeySignature { get; set; }
        public string ColorHex { get; set; }
        public int CooldownMilliseconds { get; set; }
        public int AirCooldownMilliseconds { get; set; }
    }

    internal sealed class MapQuickAnnouncementRuntimePorts
    {
        public MapQuickAnnouncementRuntimePorts()
        {
            UtcNow = DateTime.UtcNow;
        }

        public Func<string, bool> IsTokenDown { get; set; }
        public Func<MapQuickAnnouncementResolveAttempt> ResolveCurrent { get; set; }
        public Func<MapQuickAnnouncementTriggerContext> CaptureTriggerContext { get; set; }
        public Func<MapQuickAnnouncementPendingRequest, ulong, MapQuickAnnouncementResolveAttempt> ResolvePendingUi { get; set; }
        public Func<MapQuickAnnouncementPendingRequest, ulong, MapQuickAnnouncementResolveAttempt> ResolvePendingFallback { get; set; }
        public Func<string, MapQuickAnnouncementInputConsumeResult> ConsumeTriggerInput { get; set; }
        public Func<MapQuickAnnouncementResolveResult, MapQuickAnnouncementDeliveryOptions, DateTime, MapQuickAnnouncementDeliveryResult> Deliver { get; set; }
        public DateTime UtcNow { get; set; }
    }

    internal sealed class MapQuickAnnouncementRuntimeResult
    {
        public MapQuickAnnouncementRuntimeResult()
        {
            ResultCode = string.Empty;
            SkipReason = string.Empty;
            TriggerKey = string.Empty;
            InputConsumeMessage = string.Empty;
            ResolveDetail = string.Empty;
            TargetKind = string.Empty;
            TargetName = string.Empty;
            TargetSummary = string.Empty;
            DeliveryResultCode = string.Empty;
            DeliveryFailureReason = string.Empty;
            PendingState = string.Empty;
            UiHoverReadStatus = string.Empty;
            VisibilityVerdict = string.Empty;
            VisibilityReason = string.Empty;
            VisibleLayers = string.Empty;
            BlockedLayers = string.Empty;
            EchoGate = string.Empty;
            VisibilityUnavailableReason = string.Empty;
        }

        public bool Triggered { get; set; }
        public bool Delivered { get; set; }
        public bool ResolveAttempted { get; set; }
        public bool InputConsumeAttempted { get; set; }
        public bool InputConsumed { get; set; }
        public string InputConsumeMessage { get; set; }
        public string TriggerKey { get; set; }
        public string ResultCode { get; set; }
        public string SkipReason { get; set; }
        public string ResolveDetail { get; set; }
        public string TargetKind { get; set; }
        public string TargetName { get; set; }
        public string TargetSummary { get; set; }
        public int TargetCount { get; set; }
        public bool IsAir { get; set; }
        public string VisibilityVerdict { get; set; }
        public string VisibilityReason { get; set; }
        public string VisibleLayers { get; set; }
        public string BlockedLayers { get; set; }
        public bool CircuitOnly { get; set; }
        public string EchoGate { get; set; }
        public bool InvisibleAir { get; set; }
        public string VisibilityUnavailableReason { get; set; }
        public string DeliveryResultCode { get; set; }
        public string DeliveryFailureReason { get; set; }
        public int CooldownRemainingMilliseconds { get; set; }
        public bool PendingRequestActive { get; set; }
        public string PendingState { get; set; }
        public string UiHoverReadStatus { get; set; }
        public MapQuickAnnouncementHotkeyState HotkeyState { get; set; }
        public MapQuickAnnouncementDeliveryResult DeliveryResult { get; set; }

        public MapQuickAnnouncementRuntimeResult Skip(string reason)
        {
            SkipReason = reason ?? string.Empty;
            ResultCode = "skipped";
            return this;
        }
    }

    internal sealed class MapQuickAnnouncementRuntimeState
    {
        public MapQuickAnnouncementRuntimeState()
            : this(new MapQuickAnnouncementHotkeyStateMachine())
        {
        }

        public MapQuickAnnouncementRuntimeState(MapQuickAnnouncementHotkeyStateMachine hotkeyStateMachine)
        {
            HotkeyStateMachine = hotkeyStateMachine ?? new MapQuickAnnouncementHotkeyStateMachine();
        }

        public MapQuickAnnouncementHotkeyStateMachine HotkeyStateMachine { get; private set; }
        public MapQuickAnnouncementPendingRequest PendingRequest { get; set; }

        public void ClearPending()
        {
            PendingRequest = null;
        }

        public void Reset()
        {
            HotkeyStateMachine.Reset();
            PendingRequest = null;
        }
    }

    internal sealed class MapQuickAnnouncementPendingRequest
    {
        private MapQuickAnnouncementPendingRequest()
        {
            TriggerKey = string.Empty;
            HotkeySummary = string.Empty;
            InputConsumeMessage = string.Empty;
            ColorHex = MapQuickAnnouncementSettings.DefaultAnnouncementColorHex;
            CooldownMilliseconds = MapQuickAnnouncementSettings.DefaultCooldownMilliseconds;
            AirCooldownMilliseconds = MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds;
        }

        public string TriggerKey { get; private set; }
        public string HotkeySummary { get; private set; }
        public MapQuickAnnouncementTriggerContext TriggerContext { get; private set; }
        public ulong TriggerGameUpdateCount { get; private set; }
        public bool InputConsumeAttempted { get; private set; }
        public bool InputConsumed { get; private set; }
        public string InputConsumeMessage { get; private set; }
        public string LastUiHoverReadStatus { get; private set; }
        public string ColorHex { get; private set; }
        public int CooldownMilliseconds { get; private set; }
        public int AirCooldownMilliseconds { get; private set; }
        public DateTime CreatedUtc { get; private set; }

        public static MapQuickAnnouncementPendingRequest Create(
            string triggerKey,
            string hotkeySummary,
            MapQuickAnnouncementTriggerContext triggerContext,
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementInputConsumeResult consume,
            DateTime utcNow)
        {
            triggerContext = triggerContext ?? MapQuickAnnouncementTriggerContext.Failed("trigger context unavailable");
            input = input ?? new MapQuickAnnouncementRuntimeInput();
            consume = consume ?? MapQuickAnnouncementInputConsumeResult.Failed("consume result unavailable");

            return new MapQuickAnnouncementPendingRequest
            {
                TriggerKey = triggerKey ?? string.Empty,
                HotkeySummary = hotkeySummary ?? string.Empty,
                TriggerContext = triggerContext,
                TriggerGameUpdateCount = triggerContext.GameUpdateCount,
                InputConsumeAttempted = true,
                InputConsumed = consume.Succeeded,
                InputConsumeMessage = consume.Message ?? string.Empty,
                ColorHex = input.ColorHex,
                CooldownMilliseconds = input.CooldownMilliseconds,
                AirCooldownMilliseconds = input.AirCooldownMilliseconds,
                CreatedUtc = utcNow.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                    : utcNow.ToUniversalTime()
            };
        }

        public void UpdateLastUiHoverReadStatus(string status)
        {
            LastUiHoverReadStatus = status ?? string.Empty;
        }
    }

    internal sealed class MapQuickAnnouncementResolveAttempt
    {
        private MapQuickAnnouncementResolveAttempt()
        {
            FailureReason = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public MapQuickAnnouncementResolveResult Result { get; private set; }
        public string FailureReason { get; private set; }
        public string UiHoverReadStatus { get; private set; }

        public static MapQuickAnnouncementResolveAttempt Success(MapQuickAnnouncementResolveResult result)
        {
            return Success(result, string.Empty);
        }

        public static MapQuickAnnouncementResolveAttempt Success(MapQuickAnnouncementResolveResult result, string uiHoverReadStatus)
        {
            return new MapQuickAnnouncementResolveAttempt
            {
                Succeeded = result != null,
                Result = result,
                FailureReason = result == null ? "resolve result unavailable" : string.Empty,
                UiHoverReadStatus = uiHoverReadStatus ?? string.Empty
            };
        }

        public static MapQuickAnnouncementResolveAttempt Failed(string reason)
        {
            return Failed(reason, string.Empty);
        }

        public static MapQuickAnnouncementResolveAttempt Failed(string reason, string uiHoverReadStatus)
        {
            return new MapQuickAnnouncementResolveAttempt
            {
                Succeeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "resolve failed" : reason,
                UiHoverReadStatus = uiHoverReadStatus ?? string.Empty
            };
        }
    }

    internal sealed class MapQuickAnnouncementInputConsumeResult
    {
        private MapQuickAnnouncementInputConsumeResult()
        {
            Message = string.Empty;
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }

        public static MapQuickAnnouncementInputConsumeResult Success(string message)
        {
            return new MapQuickAnnouncementInputConsumeResult
            {
                Succeeded = true,
                Message = message ?? string.Empty
            };
        }

        public static MapQuickAnnouncementInputConsumeResult Failed(string message)
        {
            return new MapQuickAnnouncementInputConsumeResult
            {
                Succeeded = false,
                Message = string.IsNullOrWhiteSpace(message) ? "input consume failed" : message
            };
        }
    }
}
