namespace JueMingZ.Compat
{
    public sealed class DashInputProfile
    {
        public bool PlayerActive { get; set; }
        public bool PlayerDead { get; set; }
        public bool PlayerGhost { get; set; }
        public bool PlayerCrowdControlled { get; set; }
        public bool PlayerNoItems { get; set; }
        public bool PlayerFrozen { get; set; }
        public bool PlayerStoned { get; set; }
        public bool PlayerWebbed { get; set; }
        public bool ControlLeft { get; set; }
        public bool ControlRight { get; set; }
        public bool ControlDash { get; set; }
        public bool ReleaseDash { get; set; }
        public int HeldDirection { get; set; }
        public int CurrentDirection { get; set; }
        public int DashDelay { get; set; }
        public int DashType { get; set; }
        public bool HasCurrentDashType { get; set; }
        public bool DashCooldownReady { get; set; }
        public bool MountActive { get; set; }
        public int MountType { get; set; }
        public bool MountCanDashKnown { get; set; }
        public bool MountCanDash { get; set; }
        public bool MountAllowsDashContext { get; set; }
        public bool HasAccessoryDash { get; set; }
        public bool HasArmorDash { get; set; }
        public bool HasMountDash { get; set; }
        public int FallbackDashType { get; set; }
        public bool HasDashAbility { get; set; }
        public string DashAbilitySource { get; set; }
        public string CapabilitySummary { get; set; }

        public DashInputProfile()
        {
            DashAbilitySource = string.Empty;
            CapabilitySummary = string.Empty;
            MountType = -1;
        }

        public bool PlayerControllable
        {
            get
            {
                return PlayerActive &&
                       !PlayerDead &&
                       !PlayerGhost &&
                       !PlayerCrowdControlled &&
                       !PlayerNoItems &&
                       !PlayerFrozen &&
                       !PlayerStoned &&
                       !PlayerWebbed;
            }
        }

        public bool ExclusiveHorizontalHeld
        {
            get { return HeldDirection != 0 && ControlLeft != ControlRight; }
        }

        public bool IsDirectionHeld(int direction)
        {
            return direction < 0 ? ControlLeft : direction > 0 && ControlRight;
        }

        public bool CanDashInDirection(int direction)
        {
            return PlayerControllable &&
                   IsDirectionHeld(direction) &&
                   DashCooldownReady &&
                   HasDashAbility;
        }

        public bool DashReady
        {
            get { return PlayerControllable && ExclusiveHorizontalHeld && DashCooldownReady && HasDashAbility; }
        }
    }
}
