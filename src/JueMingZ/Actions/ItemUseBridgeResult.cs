using System;
using JueMingZ.Compat;

namespace JueMingZ.Actions
{
    public sealed class ItemUseBridgeResult
    {
        public static readonly ItemUseBridgeResult None = new ItemUseBridgeResult();

        public Guid RequestId { get; set; }
        public ItemUseBridgeStatus Status { get; set; }
        public string ResultCode { get; set; }
        public string Message { get; set; }
        public long DurationMs { get; set; }
        public int TargetSlot { get; set; }
        public int OriginalSelectedSlot { get; set; }
        public int SelectedSlotAtUseStart { get; set; }
        public bool ConsumedByItemCheck { get; set; }
        public bool SlotSwitchAttempted { get; set; }
        public bool SlotSwitchSucceeded { get; set; }
        public ItemUseVerificationState BeforeState { get; set; }
        public ItemUseVerificationState AfterState { get; set; }
        public DateTime UpdatedUtc { get; set; }

        public ItemUseBridgeResult()
        {
            Status = ItemUseBridgeStatus.None;
            ResultCode = string.Empty;
            Message = string.Empty;
            TargetSlot = -1;
            OriginalSelectedSlot = -1;
            SelectedSlotAtUseStart = -1;
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
