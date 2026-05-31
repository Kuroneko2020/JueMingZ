using System;
using System.Collections.Generic;
using JueMingZ.Actions.Channels;

namespace JueMingZ.Actions
{
    public sealed class InputActionRequest
    {
        public Guid RequestId { get; set; }
        public InputActionKind Kind { get; set; }
        public InputActionPriority Priority { get; set; }
        public string SourceFeatureId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedUtc { get; set; }
        public TimeSpan QueueTimeout { get; set; }
        public DateTime QueueExpiresUtc { get; set; }
        public string AdmissionKey { get; set; }
        public TimeSpan Timeout { get; set; }
        // The current queue scheduler still executes all actions serially.
        // IsExclusive is legacy metadata; channel arbitration decides input ownership.
        // IsExclusive=false still does not enable parallel execution in Phase 1.
        public bool IsExclusive { get; set; }
        public InputActionChannel RequiredChannels { get; set; }
        public InputActionChannel ConflictChannels { get; set; }
        public Dictionary<string, string> Metadata { get; set; }

        public InputActionRequest()
        {
            RequestId = Guid.NewGuid();
            Kind = InputActionKind.None;
            Priority = InputActionPriority.Normal;
            SourceFeatureId = string.Empty;
            Description = string.Empty;
            CreatedUtc = DateTime.UtcNow;
            QueueTimeout = TimeSpan.Zero;
            QueueExpiresUtc = default(DateTime);
            AdmissionKey = string.Empty;
            Timeout = TimeSpan.FromSeconds(5);
            IsExclusive = true;
            RequiredChannels = InputActionChannel.None;
            ConflictChannels = InputActionChannel.None;
            Metadata = new Dictionary<string, string>();
        }

        public static InputActionRequest CreateDiagnosticNoop(string sourceFeatureId, string description)
        {
            return new InputActionRequest
            {
                Kind = InputActionKind.DiagnosticNoop,
                Priority = InputActionPriority.Low,
                SourceFeatureId = sourceFeatureId ?? string.Empty,
                Description = description ?? "Diagnostic noop"
            };
        }
    }
}
