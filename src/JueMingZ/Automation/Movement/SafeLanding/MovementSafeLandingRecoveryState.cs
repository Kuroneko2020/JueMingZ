using System.Collections.Generic;
using System.Globalization;

namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingRecoveryState
    {
        public List<MovementSafeLandingEquipmentMoveRecord> TemporaryEquipmentRecords { get; private set; }
        public bool GravityRestorePending { get; set; }
        public float GravityOriginalDirection { get; set; }
        public bool MountCancelPending { get; set; }
        public bool DescentRescueGuardActive { get; set; }
        public long DescentRescueGuardTicks { get; set; }
        public string DescentRescueGuardStrategy { get; set; }
        public string TemporaryEquipmentLastDecision { get; set; }
        public string TemporaryEquipmentLastSkipReason { get; set; }
        public string GravityLastDecision { get; set; }
        public string GravityLastSkipReason { get; set; }

        public MovementSafeLandingRecoveryState()
        {
            TemporaryEquipmentRecords = new List<MovementSafeLandingEquipmentMoveRecord>();
            GravityOriginalDirection = 1f;
            TemporaryEquipmentLastDecision = string.Empty;
            TemporaryEquipmentLastSkipReason = string.Empty;
            GravityLastDecision = string.Empty;
            GravityLastSkipReason = string.Empty;
            DescentRescueGuardStrategy = string.Empty;
        }

        public string BuildSummary()
        {
            return "temporaryRecords=" + TemporaryEquipmentRecords.Count.ToString(CultureInfo.InvariantCulture) +
                   ",temporaryDecision=" + (TemporaryEquipmentLastDecision ?? string.Empty) +
                   ",temporarySkip=" + (TemporaryEquipmentLastSkipReason ?? string.Empty) +
                   ",mountCancelPending=" + Bool(MountCancelPending) +
                   ",descentGuardActive=" + Bool(DescentRescueGuardActive) +
                   ",descentGuardTicks=" + DescentRescueGuardTicks.ToString(CultureInfo.InvariantCulture) +
                   ",descentGuardStrategy=" + (DescentRescueGuardStrategy ?? string.Empty) +
                   ",gravityRestorePending=" + Bool(GravityRestorePending) +
                   ",gravityOriginalDirection=" + GravityOriginalDirection.ToString("0.###", CultureInfo.InvariantCulture) +
                   ",gravityDecision=" + (GravityLastDecision ?? string.Empty) +
                   ",gravitySkip=" + (GravityLastSkipReason ?? string.Empty);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
