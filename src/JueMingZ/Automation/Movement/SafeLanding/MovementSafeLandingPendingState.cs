using System;

namespace JueMingZ.Automation.Movement
{
    internal sealed class MovementSafeLandingPendingState
    {
        public Guid TemporaryEquipmentApplyRequestId { get; set; }
        public Guid TemporaryEquipmentActivationRequestId { get; set; }
        public Guid TemporaryEquipmentRestoreRequestId { get; set; }
        public Guid MountActivationRequestId { get; set; }
        public Guid MountCancelRequestId { get; set; }
        public Guid GravityActivationRequestId { get; set; }
        public Guid GravityRestoreRequestId { get; set; }

        public string Summary
        {
            get
            {
                return "apply=" + Id(TemporaryEquipmentApplyRequestId) +
                       ",activation=" + Id(TemporaryEquipmentActivationRequestId) +
                       ",restore=" + Id(TemporaryEquipmentRestoreRequestId) +
                       ",mountCancel=" + Id(MountCancelRequestId) +
                       ",gravityRestore=" + Id(GravityRestoreRequestId);
            }
        }

        private static string Id(Guid value)
        {
            return value == Guid.Empty ? "none" : value.ToString("N");
        }
    }
}
