using System.Collections.Generic;

namespace JueMingZ.Automation.Combat
{
    // Read results carry targeting evidence and skip reasons only; they do not imply an item-use action was accepted.
    public sealed class CombatAimReadResult
    {
        public bool CanSearch { get; set; }
        public bool HasCursorWorld { get; set; }
        public float CursorWorldX { get; set; }
        public float CursorWorldY { get; set; }
        public int MouseScreenX { get; set; }
        public int MouseScreenY { get; set; }
        public float ScreenPositionX { get; set; }
        public float ScreenPositionY { get; set; }
        public int ScreenWidth { get; set; }
        public int ScreenHeight { get; set; }
        public List<CombatTargetSnapshot> Candidates { get; private set; }
        public string SkipReason { get; set; } = string.Empty;

        public CombatAimReadResult()
        {
            Candidates = new List<CombatTargetSnapshot>();
        }
    }
}
