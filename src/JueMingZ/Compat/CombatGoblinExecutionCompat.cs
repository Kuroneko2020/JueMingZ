using JueMingZ.Config;
using Terraria;

namespace JueMingZ.Compat
{
    internal static class CombatGoblinExecutionCompat
    {
        // Hook readiness and feature settings both gate this NPC rule; default
        // false keeps hook failure from changing combat behavior.
        internal const int GoblinTinkererNpcType = 107;
        internal const int BoundGoblinNpcType = 105;

        private static volatile bool _hookReady;

        public static bool ShouldAllowGoblinExecution(NPC npc)
        {
            if (!_hookReady)
            {
                return false;
            }

            var settings = ConfigService.AppSettings;
            if (settings == null || !settings.CombatGoblinExecutionEnabled)
            {
                return false;
            }

            return npc != null && npc.type == GoblinTinkererNpcType;
        }

        internal static void SetHookReady(bool ready)
        {
            _hookReady = ready;
        }

        internal static bool ShouldAllowGoblinExecutionForTesting(bool hookReady, bool enabled, int npcType)
        {
            return hookReady && enabled && npcType == GoblinTinkererNpcType;
        }
    }
}
