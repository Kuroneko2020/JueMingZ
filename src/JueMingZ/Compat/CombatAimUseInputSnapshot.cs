namespace JueMingZ.Compat
{
    public sealed class CombatAimUseInputSnapshot
    {
        public bool Available { get; set; }
        public bool UseItemHeld { get; set; }
        public bool UseItemReleased { get; set; }
        public int ItemAnimation { get; set; }
        public int ItemTime { get; set; }
        public int SelectedSlot { get; set; }
        public int ItemType { get; set; }
        public long GameUpdateCount { get; set; }
        public string Reason { get; set; }

        public CombatAimUseInputSnapshot()
        {
            SelectedSlot = -1;
            Reason = string.Empty;
        }
    }
}
