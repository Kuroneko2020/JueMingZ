using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationPlayerDetectionService
    {
        internal static bool HasMetalDetector(object player)
        {
            var typedPlayer = player as Player;
            if (typedPlayer != null && TerrariaPlayerReadCompat.HasMetalDetector(typedPlayer))
            {
                return true;
            }

            bool value;
            if (InformationReflection.TryReadBool(player, "accOreFinder", out value) && value)
            {
                return true;
            }

            return InformationReflection.TryReadBool(player, "accOreFinderGold", out value) && value;
        }
    }
}
