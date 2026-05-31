using System;

namespace JueMingZ.Automation.Movement
{
    public sealed class MovementTeleportCorrectionDiagnosticInfo
    {
        public bool Enabled { get; set; }
        public bool HookInstalled { get; set; }
        public string HookMethod { get; set; }
        public string HookMessage { get; set; }
        public string LastDecision { get; set; }
        public string LastSkipReason { get; set; }
        public DateTime? LastDecisionUtc { get; set; }
        public int ItemType { get; set; }
        public string ItemName { get; set; }
        public float OriginalMouseWorldX { get; set; }
        public float OriginalMouseWorldY { get; set; }
        public int OriginalMouseScreenX { get; set; }
        public int OriginalMouseScreenY { get; set; }
        public float OriginalTopLeftX { get; set; }
        public float OriginalTopLeftY { get; set; }
        public bool OriginalSafe { get; set; }
        public int SearchRadiusPixels { get; set; }
        public int SearchStepPixels { get; set; }
        public int CandidateCount { get; set; }
        public int ValidCandidateCount { get; set; }
        public float NearestCandidateDistance { get; set; }
        public float CorrectedTopLeftX { get; set; }
        public float CorrectedTopLeftY { get; set; }
        public float CorrectedMouseWorldX { get; set; }
        public float CorrectedMouseWorldY { get; set; }
        public int CorrectedMouseScreenX { get; set; }
        public int CorrectedMouseScreenY { get; set; }
        public bool MouseCaptureSucceeded { get; set; }
        public bool MouseApplySucceeded { get; set; }
        public bool MouseRestoreSucceeded { get; set; }
        public bool VanillaContinued { get; set; }
        public string LastCompatError { get; set; }
        public long AppliedCount { get; set; }
        public long SkippedCount { get; set; }

        public MovementTeleportCorrectionDiagnosticInfo()
        {
            HookMethod = string.Empty;
            HookMessage = string.Empty;
            LastDecision = string.Empty;
            LastSkipReason = string.Empty;
            ItemName = string.Empty;
            LastCompatError = string.Empty;
            OriginalMouseScreenX = -1;
            OriginalMouseScreenY = -1;
            CorrectedMouseScreenX = -1;
            CorrectedMouseScreenY = -1;
        }

        public MovementTeleportCorrectionDiagnosticInfo Clone()
        {
            return new MovementTeleportCorrectionDiagnosticInfo
            {
                Enabled = Enabled,
                HookInstalled = HookInstalled,
                HookMethod = HookMethod,
                HookMessage = HookMessage,
                LastDecision = LastDecision,
                LastSkipReason = LastSkipReason,
                LastDecisionUtc = LastDecisionUtc,
                ItemType = ItemType,
                ItemName = ItemName,
                OriginalMouseWorldX = OriginalMouseWorldX,
                OriginalMouseWorldY = OriginalMouseWorldY,
                OriginalMouseScreenX = OriginalMouseScreenX,
                OriginalMouseScreenY = OriginalMouseScreenY,
                OriginalTopLeftX = OriginalTopLeftX,
                OriginalTopLeftY = OriginalTopLeftY,
                OriginalSafe = OriginalSafe,
                SearchRadiusPixels = SearchRadiusPixels,
                SearchStepPixels = SearchStepPixels,
                CandidateCount = CandidateCount,
                ValidCandidateCount = ValidCandidateCount,
                NearestCandidateDistance = NearestCandidateDistance,
                CorrectedTopLeftX = CorrectedTopLeftX,
                CorrectedTopLeftY = CorrectedTopLeftY,
                CorrectedMouseWorldX = CorrectedMouseWorldX,
                CorrectedMouseWorldY = CorrectedMouseWorldY,
                CorrectedMouseScreenX = CorrectedMouseScreenX,
                CorrectedMouseScreenY = CorrectedMouseScreenY,
                MouseCaptureSucceeded = MouseCaptureSucceeded,
                MouseApplySucceeded = MouseApplySucceeded,
                MouseRestoreSucceeded = MouseRestoreSucceeded,
                VanillaContinued = VanillaContinued,
                LastCompatError = LastCompatError,
                AppliedCount = AppliedCount,
                SkippedCount = SkippedCount
            };
        }
    }
}
