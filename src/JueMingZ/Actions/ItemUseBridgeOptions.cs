namespace JueMingZ.Actions
{
    public sealed class ItemUseBridgeOptions
    {
        // Options record the verified setup and restore contract for one bridge
        // use; downstream code must not infer broader inventory mutation rights.
        public int SelectedSlotAtUseStart { get; set; }
        public bool SlotSwitchAttempted { get; set; }
        public bool SlotSwitchSucceeded { get; set; }
        public string SlotSwitchMethod { get; set; }
        public int SlotSwitchBefore { get; set; }
        public int SlotSwitchAfter { get; set; }
        public bool WaitForMouseReleaseAttempted { get; set; }
        public bool WaitedForMouseRelease { get; set; }
        public int MouseReleaseWaitTicks { get; set; }
        public bool SkipSelectInItemCheck { get; set; }
        public bool RequireUseItemHeld { get; set; }
        public bool ApplyMainMouseLeftForItemCheck { get; set; }
        public bool AllowEarlyItemCheck { get; set; }
        public int EarlyItemCheckWindowTicks { get; set; }
        public bool AllowCombatAim { get; set; }
        public bool HasMouseScreenTarget { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public bool HasMouseWorldTarget { get; set; }
        public float MouseWorldX { get; set; }
        public float MouseWorldY { get; set; }
        public int RestoreSelectedSlotOverride { get; set; }
        public bool UiClickSuppressionAttempted { get; set; }
        public string UiClickSuppressionMode { get; set; }
        public bool UiClickSuppressionSucceeded { get; set; }
        public bool UiMouseCaptureAvailableAtClick { get; set; }
        public string HitTestModeAtClick { get; set; }
        public string ClickSourceAtClick { get; set; }

        public ItemUseBridgeOptions()
        {
            SelectedSlotAtUseStart = -1;
            SlotSwitchMethod = string.Empty;
            SlotSwitchBefore = -1;
            SlotSwitchAfter = -1;
            RestoreSelectedSlotOverride = -1;
            EarlyItemCheckWindowTicks = 0;
            UiClickSuppressionMode = string.Empty;
            HitTestModeAtClick = string.Empty;
            ClickSourceAtClick = string.Empty;
        }
    }
}
