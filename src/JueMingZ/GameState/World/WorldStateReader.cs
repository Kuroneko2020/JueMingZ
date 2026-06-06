using System;
using JueMingZ.Diagnostics;

namespace JueMingZ.GameState.World
{
    public static class WorldStateReader
    {
        public static WorldStateSnapshot Read(Type mainType, bool isInWorld)
        {
            var snapshot = new WorldStateSnapshot { WorldAvailable = isInWorld };
            // World summary is read-only display/context data; read failures keep
            // the snapshot sparse instead of inventing world identity.
            if (mainType == null)
            {
                return snapshot;
            }

            try
            {
                var worldName = GameStateReflection.GetStaticMember(mainType, "worldName") ??
                                GameStateReflection.GetStaticMember(mainType, "worldNameClean");
                snapshot.WorldName = worldName == null ? string.Empty : worldName.ToString();
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "world-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "WorldStateReader",
                    "World state read failed: " + error.Message);
            }

            return snapshot;
        }
    }
}
