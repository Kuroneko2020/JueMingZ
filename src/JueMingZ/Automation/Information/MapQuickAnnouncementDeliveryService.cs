using System;
using JueMingZ.Compat;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Information
{
    internal interface IMapQuickAnnouncementChatSink
    {
        bool TrySendChat(string text, out string failureReason);
    }

    internal interface IMapQuickAnnouncementCooldownPromptSink
    {
        bool TryShowCooldownPrompt(string text, out string failureReason);
    }

    internal sealed class MapQuickAnnouncementDeliveryOptions
    {
        public const int DefaultPromptThrottleMilliseconds = 500;

        private MapQuickAnnouncementDeliveryOptions()
        {
        }

        public string ColorHex { get; private set; }
        public int CooldownMilliseconds { get; private set; }
        public int AirCooldownMilliseconds { get; private set; }
        public int PromptThrottleMilliseconds { get; private set; }

        public static MapQuickAnnouncementDeliveryOptions Create(
            string colorHex,
            int cooldownMilliseconds,
            int airCooldownMilliseconds)
        {
            return Create(colorHex, cooldownMilliseconds, airCooldownMilliseconds, DefaultPromptThrottleMilliseconds);
        }

        public static MapQuickAnnouncementDeliveryOptions Create(
            string colorHex,
            int cooldownMilliseconds,
            int airCooldownMilliseconds,
            int promptThrottleMilliseconds)
        {
            return new MapQuickAnnouncementDeliveryOptions
            {
                ColorHex = MapQuickAnnouncementSettings.NormalizeColorHex(colorHex),
                CooldownMilliseconds = MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(
                    cooldownMilliseconds,
                    MapQuickAnnouncementSettings.DefaultCooldownMilliseconds),
                AirCooldownMilliseconds = MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(
                    airCooldownMilliseconds,
                    MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds),
                PromptThrottleMilliseconds = MapQuickAnnouncementSettings.NormalizeCooldownMilliseconds(
                    promptThrottleMilliseconds,
                    DefaultPromptThrottleMilliseconds)
            };
        }
    }

    internal sealed class MapQuickAnnouncementDeliveryResult
    {
        public MapQuickAnnouncementDeliveryResult()
        {
            ResultCode = string.Empty;
            ChatText = string.Empty;
            FailureReason = string.Empty;
        }

        public bool Sent { get; set; }
        public bool CooldownBlocked { get; set; }
        public bool CooldownPromptAttempted { get; set; }
        public bool CooldownPromptShown { get; set; }
        public int CooldownRemainingMilliseconds { get; set; }
        public string ResultCode { get; set; }
        public string ChatText { get; set; }
        public string FailureReason { get; set; }
    }

    internal sealed class MapQuickAnnouncementCooldownDecision
    {
        public bool Allowed { get; set; }
        public bool PromptAllowed { get; set; }
        public int RemainingMilliseconds { get; set; }
    }

    internal sealed class MapQuickAnnouncementCooldownState
    {
        private DateTime _nextAnySendUtc = DateTime.MinValue;
        private DateTime _nextAirSendUtc = DateTime.MinValue;
        private DateTime _nextPromptUtc = DateTime.MinValue;

        public MapQuickAnnouncementCooldownDecision Check(
            bool isAir,
            DateTime utcNow,
            MapQuickAnnouncementDeliveryOptions options)
        {
            options = options ?? MapQuickAnnouncementDeliveryOptions.Create(
                MapQuickAnnouncementSettings.DefaultAnnouncementColorHex,
                MapQuickAnnouncementSettings.DefaultCooldownMilliseconds,
                MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds);

            var blockedUntil = _nextAnySendUtc;
            if (isAir && _nextAirSendUtc > blockedUntil)
            {
                blockedUntil = _nextAirSendUtc;
            }

            if (utcNow >= blockedUntil)
            {
                return new MapQuickAnnouncementCooldownDecision
                {
                    Allowed = true,
                    PromptAllowed = false,
                    RemainingMilliseconds = 0
                };
            }

            return new MapQuickAnnouncementCooldownDecision
            {
                Allowed = false,
                PromptAllowed = utcNow >= _nextPromptUtc,
                RemainingMilliseconds = RemainingMilliseconds(utcNow, blockedUntil)
            };
        }

        public void MarkSent(bool isAir, DateTime utcNow, MapQuickAnnouncementDeliveryOptions options)
        {
            options = options ?? MapQuickAnnouncementDeliveryOptions.Create(
                MapQuickAnnouncementSettings.DefaultAnnouncementColorHex,
                MapQuickAnnouncementSettings.DefaultCooldownMilliseconds,
                MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds);

            _nextAnySendUtc = utcNow.AddMilliseconds(options.CooldownMilliseconds);
            if (isAir)
            {
                _nextAirSendUtc = utcNow.AddMilliseconds(options.AirCooldownMilliseconds);
            }
        }

        public void MarkPromptAttempted(DateTime utcNow, MapQuickAnnouncementDeliveryOptions options)
        {
            options = options ?? MapQuickAnnouncementDeliveryOptions.Create(
                MapQuickAnnouncementSettings.DefaultAnnouncementColorHex,
                MapQuickAnnouncementSettings.DefaultCooldownMilliseconds,
                MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds);

            _nextPromptUtc = utcNow.AddMilliseconds(options.PromptThrottleMilliseconds);
        }

        public void Reset()
        {
            _nextAnySendUtc = DateTime.MinValue;
            _nextAirSendUtc = DateTime.MinValue;
            _nextPromptUtc = DateTime.MinValue;
        }

        private static int RemainingMilliseconds(DateTime utcNow, DateTime blockedUntilUtc)
        {
            if (utcNow >= blockedUntilUtc)
            {
                return 0;
            }

            var milliseconds = (blockedUntilUtc - utcNow).TotalMilliseconds;
            if (milliseconds >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return Math.Max(1, (int)Math.Ceiling(milliseconds));
        }
    }

    internal sealed class MapQuickAnnouncementDeliveryService
    {
        private const string CooldownPromptText = "宣告冷却ing";
        private readonly IMapQuickAnnouncementChatSink _chatSink;
        private readonly IMapQuickAnnouncementCooldownPromptSink _promptSink;
        private readonly MapQuickAnnouncementCooldownState _cooldownState;

        public static readonly MapQuickAnnouncementDeliveryService Shared =
            new MapQuickAnnouncementDeliveryService(
                TerrariaChatAnnouncementCompat.Instance,
                TerrariaChatAnnouncementCompat.Instance);

        public MapQuickAnnouncementDeliveryService(
            IMapQuickAnnouncementChatSink chatSink,
            IMapQuickAnnouncementCooldownPromptSink promptSink)
            : this(chatSink, promptSink, new MapQuickAnnouncementCooldownState())
        {
        }

        public MapQuickAnnouncementDeliveryService(
            IMapQuickAnnouncementChatSink chatSink,
            IMapQuickAnnouncementCooldownPromptSink promptSink,
            MapQuickAnnouncementCooldownState cooldownState)
        {
            _chatSink = chatSink;
            _promptSink = promptSink;
            _cooldownState = cooldownState ?? new MapQuickAnnouncementCooldownState();
        }

        public MapQuickAnnouncementDeliveryResult TryDeliver(
            MapQuickAnnouncementResolveResult resolveResult,
            MapQuickAnnouncementDeliveryOptions options,
            DateTime utcNow)
        {
            options = options ?? MapQuickAnnouncementDeliveryOptions.Create(
                MapQuickAnnouncementSettings.DefaultAnnouncementColorHex,
                MapQuickAnnouncementSettings.DefaultCooldownMilliseconds,
                MapQuickAnnouncementSettings.DefaultAirCooldownMilliseconds);

            var rawBody = resolveResult == null ? string.Empty : resolveResult.Body;
            var chatText = MapQuickAnnouncementTextSafety.BuildColoredAnnouncement(rawBody, options.ColorHex);
            if (string.IsNullOrWhiteSpace(chatText))
            {
                return new MapQuickAnnouncementDeliveryResult
                {
                    ResultCode = "invalidText",
                    FailureReason = "empty announcement text"
                };
            }

            var isAir = MapQuickAnnouncementTextSafety.IsAirTarget(resolveResult);
            var cooldown = _cooldownState.Check(isAir, utcNow, options);
            if (!cooldown.Allowed)
            {
                return HandleCooldownBlocked(cooldown, options, utcNow);
            }

            var failureReason = string.Empty;
            if (_chatSink == null || !_chatSink.TrySendChat(chatText, out failureReason))
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason) ? "chat sink unavailable" : failureReason;
                LogThrottle.WarnThrottled(
                    "map-quick-announcement-send-failed",
                    TimeSpan.FromSeconds(30),
                    "MapQuickAnnouncementDelivery",
                    "Quick announcement chat send failed: " + failureReason);

                return new MapQuickAnnouncementDeliveryResult
                {
                    ResultCode = "sendFailed",
                    ChatText = chatText,
                    FailureReason = failureReason
                };
            }

            _cooldownState.MarkSent(isAir, utcNow, options);
            return new MapQuickAnnouncementDeliveryResult
            {
                Sent = true,
                ResultCode = "sent",
                ChatText = chatText
            };
        }

        public void ResetCooldownForTesting()
        {
            _cooldownState.Reset();
        }

        private MapQuickAnnouncementDeliveryResult HandleCooldownBlocked(
            MapQuickAnnouncementCooldownDecision cooldown,
            MapQuickAnnouncementDeliveryOptions options,
            DateTime utcNow)
        {
            var result = new MapQuickAnnouncementDeliveryResult
            {
                CooldownBlocked = true,
                ResultCode = "cooldown",
                CooldownRemainingMilliseconds = cooldown.RemainingMilliseconds
            };

            if (!cooldown.PromptAllowed)
            {
                return result;
            }

            _cooldownState.MarkPromptAttempted(utcNow, options);
            result.CooldownPromptAttempted = true;

            var promptFailure = string.Empty;
            result.CooldownPromptShown =
                _promptSink != null && _promptSink.TryShowCooldownPrompt(CooldownPromptText, out promptFailure);
            if (!result.CooldownPromptShown)
            {
                result.FailureReason = string.IsNullOrWhiteSpace(promptFailure)
                    ? "cooldown prompt sink unavailable"
                    : promptFailure;
            }

            return result;
        }
    }
}
