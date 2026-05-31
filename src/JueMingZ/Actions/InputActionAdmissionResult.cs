using System;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public enum InputActionAdmissionDecision
    {
        Accepted,
        DeniedDuplicatePendingOrRunning,
        DeniedRunningChannelConflict,
        DeniedBridgeBusy,
        DeniedExpiredBeforeEnqueue,
        DeniedInvalidRequest
    }

    public sealed class InputActionAdmissionResult
    {
        public static readonly InputActionAdmissionResult Empty = new InputActionAdmissionResult
        {
            Accepted = false,
            Decision = InputActionAdmissionDecision.DeniedInvalidRequest,
            Reason = string.Empty
        };

        public bool Accepted { get; set; }
        public Guid RequestId { get; set; }
        public InputActionKind Kind { get; set; }
        public string SourceFeatureId { get; set; }
        public string Scenario { get; set; }
        public string AdmissionKey { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public InputActionChannel ConflictChannels { get; set; }
        public InputActionChannel BlockingChannels { get; set; }
        public InputActionAdmissionDecision Decision { get; set; }
        public string Reason { get; set; }
        public string PendingConflictSummary { get; set; }
        public string RunningConflictSummary { get; set; }
        public string BridgeBusySummary { get; set; }
        public bool DuplicatePendingOrRunning { get; set; }
        public bool ExpiredBeforeEnqueue { get; set; }

        public InputActionAdmissionResult()
        {
            SourceFeatureId = string.Empty;
            Scenario = string.Empty;
            AdmissionKey = string.Empty;
            Reason = string.Empty;
            PendingConflictSummary = string.Empty;
            RunningConflictSummary = string.Empty;
            BridgeBusySummary = string.Empty;
        }

        public string Status
        {
            get { return Accepted ? "Accepted" : "Denied"; }
        }

        public string Summary
        {
            get
            {
                return Status +
                       " decision=" + Decision +
                       " key=" + (AdmissionKey ?? string.Empty) +
                       " required=" + InputActionChannelFormatter.Format(RequiredChannels) +
                       " blocking=" + InputActionChannelFormatter.Format(BlockingChannels) +
                       (string.IsNullOrWhiteSpace(Reason) ? string.Empty : " reason=" + Reason);
            }
        }
    }
}
