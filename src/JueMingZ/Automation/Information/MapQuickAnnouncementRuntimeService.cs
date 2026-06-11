using System;
using System.Runtime.InteropServices;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;
using JueMingZ.Runtime;
using JueMingZ.UI.Legacy;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementRuntimeService
    {
        private static readonly object SyncRoot = new object();
        private static readonly MapQuickAnnouncementHotkeyStateMachine HotkeyStateMachine =
            new MapQuickAnnouncementHotkeyStateMachine();

        public static void UpdatePrefixGuard()
        {
            try
            {
                MapQuickAnnouncementRuntimeResult result;
                lock (SyncRoot)
                {
                    result = Tick(BuildCurrentInput(), HotkeyStateMachine, BuildCurrentPorts());
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
                HotkeyStateMachine.Reset();
                MapQuickAnnouncementDiagnostics.ResetForTesting();
            }
        }

        internal static MapQuickAnnouncementRuntimeResult TickForTesting(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementRuntimePorts ports)
        {
            return TickForTesting(input, new MapQuickAnnouncementHotkeyStateMachine(), ports);
        }

        internal static MapQuickAnnouncementRuntimeResult TickForTesting(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementHotkeyStateMachine stateMachine,
            MapQuickAnnouncementRuntimePorts ports)
        {
            return Tick(input, stateMachine ?? new MapQuickAnnouncementHotkeyStateMachine(), ports);
        }

        private static MapQuickAnnouncementRuntimeResult Tick(
            MapQuickAnnouncementRuntimeInput input,
            MapQuickAnnouncementHotkeyStateMachine stateMachine,
            MapQuickAnnouncementRuntimePorts ports)
        {
            input = input ?? new MapQuickAnnouncementRuntimeInput();
            ports = ports ?? new MapQuickAnnouncementRuntimePorts();
            stateMachine = stateMachine ?? new MapQuickAnnouncementHotkeyStateMachine();

            var result = new MapQuickAnnouncementRuntimeResult
            {
                ResultCode = "skipped"
            };

            if (!input.FeatureEnabled)
            {
                stateMachine.Reset();
                return result.Skip("disabled");
            }

            if (!input.IsInWorld)
            {
                stateMachine.Reset();
                return result.Skip("notInWorld");
            }

            if (!input.GameInputAvailable)
            {
                result.HotkeyState = stateMachine.Update(input.Hotkey, _ => false);
                return result.Skip("gameInputUnavailable");
            }

            var hotkeyState = stateMachine.Update(input.Hotkey, token => IsTokenDown(ports, token));
            result.HotkeyState = hotkeyState;
            result.TriggerKey = input.Hotkey == null ? string.Empty : input.Hotkey.TriggerKey ?? string.Empty;

            string blockedReason;
            if (IsResponseBlocked(input, out blockedReason))
            {
                result.Skip(blockedReason);
                if (hotkeyState != null && hotkeyState.Triggered)
                {
                    MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                }

                return result;
            }

            if (hotkeyState == null || !hotkeyState.IsValid)
            {
                return result.Skip("invalidHotkey");
            }

            if (!hotkeyState.Triggered)
            {
                return result.Skip(hotkeyState.CombinationHeld ? "latchedUntilRelease" : "notTriggered");
            }

            result.Triggered = true;
            result.ResultCode = "triggered";
            var triggerKey = input.Hotkey == null ? string.Empty : input.Hotkey.TriggerKey ?? string.Empty;
            if (IsMouseTrigger(triggerKey))
            {
                result.InputConsumeAttempted = true;
                var consume = ports.ConsumeTriggerInput == null
                    ? MapQuickAnnouncementInputConsumeResult.Failed("consume port unavailable")
                    : ports.ConsumeTriggerInput(triggerKey);
                consume = consume ?? MapQuickAnnouncementInputConsumeResult.Failed("consume result unavailable");
                result.InputConsumed = consume.Succeeded;
                result.InputConsumeMessage = consume.Message ?? string.Empty;
            }

            var resolve = ports.ResolveCurrent == null
                ? MapQuickAnnouncementResolveAttempt.Failed("resolve port unavailable")
                : ports.ResolveCurrent();
            resolve = resolve ?? MapQuickAnnouncementResolveAttempt.Failed("resolve result unavailable");
            result.ResolveAttempted = true;
            if (!resolve.Succeeded || resolve.Result == null)
            {
                result.ResultCode = "resolveFailed";
                result.SkipReason = string.IsNullOrWhiteSpace(resolve.FailureReason)
                    ? "resolveFailed"
                    : resolve.FailureReason;
                MapQuickAnnouncementDiagnostics.RecordRuntimeResult(result, ports.UtcNow);
                return result;
            }

            result.ResolveDetail = resolve.Result.Detail ?? string.Empty;
            result.TargetKind = resolve.Result.Kind.ToString();
            result.TargetName = resolve.Result.TargetName ?? string.Empty;
            result.TargetSummary = resolve.Result.Body ?? string.Empty;
            result.TargetCount = resolve.Result.TargetCount;
            result.IsAir = resolve.Result.Kind == MapQuickAnnouncementTargetKind.Air;
            var deliveryOptions = MapQuickAnnouncementDeliveryOptions.Create(
                input.ColorHex,
                input.CooldownMilliseconds,
                input.AirCooldownMilliseconds);
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
                   string.Equals(token, "Mouse5", StringComparison.OrdinalIgnoreCase);
        }

        private static MapQuickAnnouncementRuntimeInput BuildCurrentInput()
        {
            var settings = RuntimeSettingsSnapshotProvider.GetCurrent();
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
                Hotkey = new MapQuickAnnouncementHotkey(
                    settings.MapQuickAnnouncementHotkeySlot1,
                    settings.MapQuickAnnouncementHotkeySlot2,
                    settings.MapQuickAnnouncementTriggerKey),
                ColorHex = settings.MapQuickAnnouncementColorHex,
                CooldownMilliseconds = settings.MapQuickAnnouncementCooldownMilliseconds,
                AirCooldownMilliseconds = settings.MapQuickAnnouncementAirCooldownMilliseconds
            };
        }

        private static MapQuickAnnouncementRuntimePorts BuildCurrentPorts()
        {
            return new MapQuickAnnouncementRuntimePorts
            {
                UtcNow = DateTime.UtcNow,
                ResolveCurrent = ResolveCurrent,
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
        public MapQuickAnnouncementHotkey Hotkey { get; set; }
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
        public string DeliveryResultCode { get; set; }
        public string DeliveryFailureReason { get; set; }
        public int CooldownRemainingMilliseconds { get; set; }
        public MapQuickAnnouncementHotkeyState HotkeyState { get; set; }
        public MapQuickAnnouncementDeliveryResult DeliveryResult { get; set; }

        public MapQuickAnnouncementRuntimeResult Skip(string reason)
        {
            SkipReason = reason ?? string.Empty;
            ResultCode = "skipped";
            return this;
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

        public static MapQuickAnnouncementResolveAttempt Success(MapQuickAnnouncementResolveResult result)
        {
            return new MapQuickAnnouncementResolveAttempt
            {
                Succeeded = result != null,
                Result = result,
                FailureReason = result == null ? "resolve result unavailable" : string.Empty
            };
        }

        public static MapQuickAnnouncementResolveAttempt Failed(string reason)
        {
            return new MapQuickAnnouncementResolveAttempt
            {
                Succeeded = false,
                FailureReason = string.IsNullOrWhiteSpace(reason) ? "resolve failed" : reason
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
