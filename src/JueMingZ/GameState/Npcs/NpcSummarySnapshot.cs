using System.Collections.Generic;

namespace JueMingZ.GameState.Npcs
{
    public sealed class NpcSummarySnapshot
    {
        public int ActiveNpcCount { get; set; }
        public int TownNpcCount { get; set; }
        public int HostileNpcCount { get; set; }
        public int CritterCount { get; set; }
        public IReadOnlyList<NpcSnapshot> CatchableCritters { get; set; }

        public NpcSummarySnapshot()
        {
            CatchableCritters = new List<NpcSnapshot>();
        }
    }
}
