using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed class InputActionQueueFastState
    {
        public static readonly InputActionQueueFastState Empty = new InputActionQueueFastState();

        public int PendingCount { get; set; }
        public long UpdateCount { get; set; }
        public bool HasRunningAction { get; set; }
        public InputActionKind RunningActionKindValue { get; set; }
        public long LastInputActionUpdateMs { get; set; }
        public InputActionResult LastResult { get; set; }
        public int ChannelLeaseCount { get; set; }
        public bool HasChannelLease { get; set; }
        public InputActionChannel OccupiedChannelsValue { get; set; }
        public InputActionChannel RunningLeaseChannelsValue { get; set; }
        public InputActionChannel BridgeBusyChannelsValue { get; set; }
        public InputActionChannel CleanupLeaseChannelsValue { get; set; }
        public bool IsBridgeBusy { get; set; }
        public int OccupiedChannelCount { get; set; }
        public int RunningLeaseChannelCount { get; set; }
        public int BridgeBusyChannelCount { get; set; }
        public string RunningActionSource { get; set; }
        public string CleanupLeaseOwner { get; set; }

        public string RunningActionKind
        {
            get { return HasRunningAction ? RunningActionKindValue.ToString() : string.Empty; }
        }

        public string OccupiedChannels
        {
            get { return InputActionChannelFormatter.Format(OccupiedChannelsValue); }
        }

        public string BridgeBusyChannels
        {
            get { return InputActionChannelFormatter.Format(BridgeBusyChannelsValue); }
        }

        public string CleanupLeaseChannels
        {
            get { return InputActionChannelFormatter.Format(CleanupLeaseChannelsValue); }
        }

        public string LastActionKind
        {
            get { return LastResult == null ? string.Empty : LastResult.Kind.ToString(); }
        }

        public string LastActionResultCode
        {
            get { return LastResult == null ? string.Empty : LastResult.ResultCode ?? string.Empty; }
        }

        public string LastActionResult
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                return LastResult.Status + ": " + LastResult.Message;
            }
        }

        public InputActionQueueFastState()
        {
            RunningActionKindValue = InputActionKind.None;
            OccupiedChannelsValue = InputActionChannel.None;
            RunningLeaseChannelsValue = InputActionChannel.None;
            BridgeBusyChannelsValue = InputActionChannel.None;
            CleanupLeaseChannelsValue = InputActionChannel.None;
            CleanupLeaseOwner = string.Empty;
        }
    }

    public sealed class InputActionQueueSnapshot
    {
        public static readonly InputActionQueueSnapshot Empty = new InputActionQueueSnapshot();

        public int PendingCount { get; set; }
        public long UpdateCount { get; set; }
        public string RunningAction { get; set; }
        public string RunningActionKind { get; set; }
        public string RunningActionSource { get; set; }
        public string RunningActionStatus { get; set; }
        public InputActionResult LastResult { get; set; }
        public string LastActionKind { get { return LastResult == null ? string.Empty : LastResult.Kind.ToString(); } }
        public string LastActionResultCode { get { return LastResult == null ? string.Empty : LastResult.ResultCode ?? string.Empty; } }
        public long LastActionDurationMs { get { return LastResult == null ? 0 : LastResult.DurationMs; } }
        public int RecentResultsCount { get; set; }
        public long LastInputActionUpdateMs { get; set; }
        public string RecentActionLine1 { get; set; }
        public string RecentActionLine2 { get; set; }
        public string RecentActionLine3 { get; set; }
        public int ActionQueueChannelLeaseCount { get; set; }
        public string ActionQueueRunningChannels { get; set; }
        public string ActionQueueOccupiedChannels { get; set; }
        public string ActionQueueBridgeBusyChannels { get; set; }
        public string ActionQueueRunningLeaseChannels { get; set; }
        public int ActionQueueBlockedPendingCount { get; set; }
        public string ActionQueueLastChannelDecision { get; set; }
        public string ActionQueueLastChannelBlockedReason { get; set; }
        public string ActionQueueChannelOwnerSummary { get; set; }
        public string ActionQueueBridgeBusySummary { get; set; }
        public string ActionQueuePendingChannelSummary { get; set; }
        public string ActionQueuePendingOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionStatus { get; set; }
        public string ActionQueueLastAdmissionDecision { get; set; }
        public string ActionQueueLastAdmissionReason { get; set; }
        public string ActionQueueLastAdmissionKind { get; set; }
        public string ActionQueueLastAdmissionSource { get; set; }
        public string ActionQueueLastAdmissionScenario { get; set; }
        public string ActionQueueLastAdmissionKey { get; set; }
        public string ActionQueueLastAdmissionRequiredChannels { get; set; }
        public string ActionQueueLastAdmissionBlockingChannels { get; set; }
        public string ActionQueueLastAdmissionConflictChannels { get; set; }
        public string ActionQueueLastAdmissionPendingConflictSummary { get; set; }
        public string ActionQueueLastAdmissionRunningConflictSummary { get; set; }
        public string ActionQueueLastAdmissionBridgeBusySummary { get; set; }
        public string ActionQueueLastAdmissionOwnerSummary { get; set; }
        public string ActionQueueLastAdmissionSupersededRequestId { get; set; }
        public string ActionQueueLastAdmissionCoalescedRequestId { get; set; }
        public int ActionQueueSupersededPendingCount { get; set; }
        public int ActionQueueCoalescedPendingCount { get; set; }
        public string SchedulerLastSelectedRequest { get; set; }
        public string SchedulerLastSupersededRequest { get; set; }
        public string SchedulerLastFairnessBucket { get; set; }
        public int BackgroundRequestCoalescedCount { get; set; }
        public int ExpiredPendingDroppedCount { get; set; }
        public int ActionQueueCleanupLeaseCount { get; set; }
        public string ActionQueueCleanupLeaseChannels { get; set; }
        public string ActionQueueLastCleanupOwner { get; set; }
        public string ActionQueueLastCleanupReason { get; set; }
        public int ActionQueueDirectEnqueueCount { get; set; }
        public string ActionQueueLastDirectEnqueueKind { get; set; }
        public string ActionQueueLastDirectEnqueueSource { get; set; }
        public string ActionQueueLastDirectEnqueueScenario { get; set; }
        public string ActionQueueLastDirectEnqueueAdmissionKey { get; set; }
        public string ActionQueueLastDirectEnqueueRequiredChannels { get; set; }
        public int ActionQueueExpiredPendingCount { get; set; }
        public string ActionQueueLastPendingExpiryReason { get; set; }

        public string LastActionResult
        {
            get
            {
                if (LastResult == null)
                {
                    return string.Empty;
                }

                return LastResult.Status + ": " + LastResult.Message;
            }
        }

        public string LastActionStatus
        {
            get { return LastResult == null ? "none" : LastResult.Status.ToString(); }
        }

        public string LastActionMessage
        {
            get { return LastResult == null ? string.Empty : LastResult.Message ?? string.Empty; }
        }

        public InputActionQueueSnapshot()
        {
            RunningAction = string.Empty;
            RunningActionKind = string.Empty;
            RunningActionSource = string.Empty;
            RunningActionStatus = string.Empty;
            RecentActionLine1 = string.Empty;
            RecentActionLine2 = string.Empty;
            RecentActionLine3 = string.Empty;
            ActionQueueRunningChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueOccupiedChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueBridgeBusyChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueRunningLeaseChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastChannelDecision = string.Empty;
            ActionQueueLastChannelBlockedReason = string.Empty;
            ActionQueueChannelOwnerSummary = string.Empty;
            ActionQueueBridgeBusySummary = string.Empty;
            ActionQueuePendingChannelSummary = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueuePendingOwnerSummary = string.Empty;
            ActionQueueLastAdmissionStatus = string.Empty;
            ActionQueueLastAdmissionDecision = string.Empty;
            ActionQueueLastAdmissionReason = string.Empty;
            ActionQueueLastAdmissionKind = string.Empty;
            ActionQueueLastAdmissionSource = string.Empty;
            ActionQueueLastAdmissionScenario = string.Empty;
            ActionQueueLastAdmissionKey = string.Empty;
            ActionQueueLastAdmissionRequiredChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionBlockingChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionConflictChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastAdmissionPendingConflictSummary = string.Empty;
            ActionQueueLastAdmissionRunningConflictSummary = string.Empty;
            ActionQueueLastAdmissionBridgeBusySummary = string.Empty;
            ActionQueueLastAdmissionOwnerSummary = string.Empty;
            ActionQueueLastAdmissionSupersededRequestId = string.Empty;
            ActionQueueLastAdmissionCoalescedRequestId = string.Empty;
            SchedulerLastSelectedRequest = string.Empty;
            SchedulerLastSupersededRequest = string.Empty;
            SchedulerLastFairnessBucket = string.Empty;
            ActionQueueCleanupLeaseChannels = InputActionChannelFormatter.Format(InputActionChannel.None);
            ActionQueueLastCleanupOwner = string.Empty;
            ActionQueueLastCleanupReason = string.Empty;
            ActionQueueLastDirectEnqueueKind = string.Empty;
            ActionQueueLastDirectEnqueueSource = string.Empty;
            ActionQueueLastDirectEnqueueScenario = string.Empty;
            ActionQueueLastDirectEnqueueAdmissionKey = string.Empty;
            ActionQueueLastDirectEnqueueRequiredChannels = string.Empty;
            ActionQueueLastPendingExpiryReason = string.Empty;
        }
    }
}
