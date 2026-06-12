using System;
using JueMingZ.Compat;

namespace JueMingZ.Automation.Information
{
    internal sealed class MapQuickAnnouncementDiagnosticsSnapshot
    {
        public MapQuickAnnouncementDiagnosticsSnapshot()
        {
            LastResultCode = string.Empty;
            LastTargetKind = string.Empty;
            LastTargetName = string.Empty;
            LastTargetSummary = string.Empty;
            LastResolveDetail = string.Empty;
            LastTargetSource = string.Empty;
            LastUiHoverSource = string.Empty;
            LastUiHoverState = string.Empty;
            LastUiHoverHookStatus = string.Empty;
            LastPendingState = string.Empty;
            LastPlacementLookupSource = string.Empty;
            LastFallbackReason = string.Empty;
            LastFailureReason = string.Empty;
            LastHotkeySummary = string.Empty;
            LastInputConsumeResult = string.Empty;
            LastHoverCacheAgeUpdates = -1;
        }

        public bool LastTriggered { get; set; }
        public string LastResultCode { get; set; }
        public string LastTargetKind { get; set; }
        public string LastTargetName { get; set; }
        public string LastTargetSummary { get; set; }
        public int LastTargetCount { get; set; }
        public string LastResolveDetail { get; set; }
        public string LastTargetSource { get; set; }
        public string LastUiHoverSource { get; set; }
        public string LastUiHoverState { get; set; }
        public string LastUiHoverHookStatus { get; set; }
        public string LastPendingState { get; set; }
        public int LastHoverCacheAgeUpdates { get; set; }
        public string LastPlacementLookupSource { get; set; }
        public string LastFallbackReason { get; set; }
        public bool LastIsAir { get; set; }
        public bool LastCooldownBlocked { get; set; }
        public bool LastSendSucceeded { get; set; }
        public string LastFailureReason { get; set; }
        public string LastHotkeySummary { get; set; }
        public bool LastInputConsumed { get; set; }
        public string LastInputConsumeResult { get; set; }
        public DateTime? LastDecisionUtc { get; set; }

        public MapQuickAnnouncementDiagnosticsSnapshot Clone()
        {
            return new MapQuickAnnouncementDiagnosticsSnapshot
            {
                LastTriggered = LastTriggered,
                LastResultCode = LastResultCode,
                LastTargetKind = LastTargetKind,
                LastTargetName = LastTargetName,
                LastTargetSummary = LastTargetSummary,
                LastTargetCount = LastTargetCount,
                LastResolveDetail = LastResolveDetail,
                LastTargetSource = LastTargetSource,
                LastUiHoverSource = LastUiHoverSource,
                LastUiHoverState = LastUiHoverState,
                LastUiHoverHookStatus = LastUiHoverHookStatus,
                LastPendingState = LastPendingState,
                LastHoverCacheAgeUpdates = LastHoverCacheAgeUpdates,
                LastPlacementLookupSource = LastPlacementLookupSource,
                LastFallbackReason = LastFallbackReason,
                LastIsAir = LastIsAir,
                LastCooldownBlocked = LastCooldownBlocked,
                LastSendSucceeded = LastSendSucceeded,
                LastFailureReason = LastFailureReason,
                LastHotkeySummary = LastHotkeySummary,
                LastInputConsumed = LastInputConsumed,
                LastInputConsumeResult = LastInputConsumeResult,
                LastDecisionUtc = LastDecisionUtc
            };
        }
    }

    internal static class MapQuickAnnouncementDiagnostics
    {
        private const int MaxSummaryLength = 96;
        private static readonly object SyncRoot = new object();
        private static MapQuickAnnouncementDiagnosticsSnapshot _last =
            new MapQuickAnnouncementDiagnosticsSnapshot();

        public static MapQuickAnnouncementDiagnosticsSnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return _last.Clone();
            }
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _last = new MapQuickAnnouncementDiagnosticsSnapshot();
            }
        }

        internal static void RecordRuntimeResult(MapQuickAnnouncementRuntimeResult result, DateTime utcNow)
        {
            if (result == null || !ShouldRecord(result))
            {
                return;
            }

            var resolveDetail = Summarize(result.ResolveDetail);
            var detailSummary = ResolveDetailSummary.Parse(resolveDetail);
            var snapshot = new MapQuickAnnouncementDiagnosticsSnapshot
            {
                LastTriggered = result.Triggered,
                LastResultCode = FirstNonEmpty(result.ResultCode, result.DeliveryResultCode, result.SkipReason),
                LastTargetKind = Summarize(result.TargetKind),
                LastTargetName = Summarize(result.TargetName),
                LastTargetSummary = Summarize(FirstNonEmpty(result.TargetSummary, result.TargetName, result.ResolveDetail)),
                LastTargetCount = Math.Max(0, result.TargetCount),
                LastResolveDetail = resolveDetail,
                LastTargetSource = detailSummary.TargetSource,
                LastUiHoverSource = detailSummary.UiHoverSource,
                LastUiHoverState = Summarize(FirstNonEmpty(detailSummary.UiHoverState, result.UiHoverReadStatus)),
                LastUiHoverHookStatus = Summarize(TerrariaUiMouseCompat.ItemSlotHoverHookStatus),
                LastPendingState = Summarize(result.PendingState),
                LastHoverCacheAgeUpdates = detailSummary.HoverCacheAgeUpdates,
                LastPlacementLookupSource = detailSummary.PlacementLookupSource,
                LastFallbackReason = detailSummary.FallbackReason,
                LastIsAir = result.IsAir,
                LastCooldownBlocked = result.DeliveryResult != null && result.DeliveryResult.CooldownBlocked,
                LastSendSucceeded = result.Delivered,
                LastFailureReason = Summarize(FirstNonEmpty(result.DeliveryFailureReason, result.SkipReason)),
                LastHotkeySummary = Summarize(
                    result.HotkeyState == null ? result.TriggerKey : result.HotkeyState.Signature),
                LastInputConsumed = result.InputConsumed,
                LastInputConsumeResult = Summarize(BuildInputConsumeResult(result)),
                LastDecisionUtc = utcNow.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
                    : utcNow.ToUniversalTime()
            };

            lock (SyncRoot)
            {
                _last = snapshot;
            }
        }

        private static bool ShouldRecord(MapQuickAnnouncementRuntimeResult result)
        {
            return result.Triggered ||
                   result.ResolveAttempted ||
                   result.InputConsumeAttempted ||
                   result.DeliveryResult != null ||
                   (result.HotkeyState != null &&
                    result.HotkeyState.Triggered &&
                    !string.IsNullOrWhiteSpace(result.SkipReason));
        }

        private static string BuildInputConsumeResult(MapQuickAnnouncementRuntimeResult result)
        {
            if (result == null || !result.InputConsumeAttempted)
            {
                return "notAttempted";
            }

            var message = string.IsNullOrWhiteSpace(result.InputConsumeMessage)
                ? string.Empty
                : ":" + result.InputConsumeMessage;
            return (result.InputConsumed ? "consumed" : "failed") + message;
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return string.IsNullOrWhiteSpace(first) ? (second ?? string.Empty) : first;
        }

        private static string FirstNonEmpty(string first, string second, string third)
        {
            return FirstNonEmpty(FirstNonEmpty(first, second), third);
        }

        private static string Summarize(string value)
        {
            var sanitized = MapQuickAnnouncementTextSafety.SanitizeBody(value);
            if (sanitized.Length <= MaxSummaryLength)
            {
                return sanitized;
            }

            return sanitized.Substring(0, MaxSummaryLength);
        }

        private sealed class ResolveDetailSummary
        {
            public ResolveDetailSummary()
            {
                TargetSource = string.Empty;
                UiHoverSource = string.Empty;
                UiHoverState = string.Empty;
                PlacementLookupSource = string.Empty;
                FallbackReason = string.Empty;
                HoverCacheAgeUpdates = -1;
            }

            public string TargetSource { get; private set; }
            public string UiHoverSource { get; private set; }
            public string UiHoverState { get; private set; }
            public int HoverCacheAgeUpdates { get; private set; }
            public string PlacementLookupSource { get; private set; }
            public string FallbackReason { get; private set; }

            public static ResolveDetailSummary Parse(string detail)
            {
                var summary = new ResolveDetailSummary();
                if (string.IsNullOrWhiteSpace(detail))
                {
                    return summary;
                }

                var segments = detail.Split(';');
                var head = segments.Length == 0 ? detail.Trim() : segments[0].Trim();
                var headParts = head.Split(':');
                summary.TargetSource = headParts.Length == 0 ? head : headParts[0].Trim();

                if (headParts.Length > 1)
                {
                    summary.PlacementLookupSource = headParts[1].Trim();
                }

                if (string.Equals(summary.TargetSource, "uiItem", StringComparison.Ordinal))
                {
                    summary.UiHoverState = "freshItem";
                }
                else if (string.Equals(summary.TargetSource, "uiSlot", StringComparison.Ordinal) &&
                         string.Equals(summary.PlacementLookupSource, "empty", StringComparison.Ordinal))
                {
                    summary.UiHoverState = "freshEmptySlot";
                }

                for (var index = 1; index < segments.Length; index++)
                {
                    ApplyKeyValue(summary, segments[index]);
                }

                if ((string.Equals(summary.TargetSource, "tile", StringComparison.Ordinal) ||
                     string.Equals(summary.TargetSource, "wall", StringComparison.Ordinal)) &&
                    !string.IsNullOrWhiteSpace(summary.PlacementLookupSource) &&
                    !string.Equals(summary.PlacementLookupSource, "placementItem", StringComparison.Ordinal))
                {
                    summary.FallbackReason = "placementItemMiss:" + summary.PlacementLookupSource;
                }
                else if (string.Equals(summary.TargetSource, "air", StringComparison.Ordinal))
                {
                    summary.FallbackReason = "noTarget:air";
                }

                return summary;
            }

            private static void ApplyKeyValue(ResolveDetailSummary summary, string segment)
            {
                if (summary == null || string.IsNullOrWhiteSpace(segment))
                {
                    return;
                }

                var separator = segment.IndexOf('=');
                if (separator <= 0)
                {
                    return;
                }

                var key = segment.Substring(0, separator).Trim();
                var value = segment.Substring(separator + 1).Trim();
                if (string.Equals(key, "source", StringComparison.Ordinal))
                {
                    summary.UiHoverSource = value;
                    return;
                }

                if (string.Equals(key, "ageUpdates", StringComparison.Ordinal))
                {
                    int age;
                    if (int.TryParse(value, out age))
                    {
                        summary.HoverCacheAgeUpdates = age;
                    }
                }
            }
        }
    }
}
