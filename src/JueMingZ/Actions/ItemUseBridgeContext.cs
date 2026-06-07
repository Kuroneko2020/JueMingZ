using System;
using JueMingZ.Compat;

namespace JueMingZ.Actions
{
    public sealed class ItemUseBridgeContext
    {
        // Context is the handoff into Player.ItemCheck. Preparing a slot or
        // target here still does not permit services to synthesize their own click.
        public Guid RequestId { get; set; }
        public string SourceFeatureId { get; set; }
        public int TargetSlot { get; set; }
        public int ExpectedItemType { get; set; }
        public int ExpectedStack { get; set; }
        public bool HasMouseScreenTarget { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public bool HasMouseWorldTarget { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public bool SkipSelectInItemCheck { get; set; }
        public bool ApplyMainMouseLeftForItemCheck { get; set; }
        public bool AllowCombatAim { get; set; }
        public int ExpectedSelectedSlot { get; set; }
        public int RestoreSelectedSlot { get; set; }
        public ItemUseVerificationState BeforeState { get; set; }

        public ItemUseBridgeContext()
        {
            SourceFeatureId = string.Empty;
            ExpectedSelectedSlot = -1;
            RestoreSelectedSlot = -1;
        }
    }
}
