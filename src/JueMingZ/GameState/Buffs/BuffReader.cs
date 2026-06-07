using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using TerrariaPlayer = Terraria.Player;

namespace JueMingZ.GameState.Buffs
{
    public static class BuffReader
    {
        // Buff snapshots are read-only inputs for recovery gates; applying or
        // removing buffs stays in Action/Compat mutation paths.
        public static IReadOnlyList<BuffSnapshot> Read(TerrariaPlayer player)
        {
            var buffs = new List<BuffSnapshot>();
            if (player == null)
            {
                return buffs;
            }

            try
            {
                var buffTypes = TerrariaPlayerReadCompat.BuffTypes(player);
                var buffTimes = TerrariaPlayerReadCompat.BuffTimes(player);
                if (buffTypes == null || buffTimes == null)
                {
                    return buffs;
                }

                var max = Math.Min(buffTypes.Length, buffTimes.Length);
                for (var index = 0; index < max; index++)
                {
                    var type = buffTypes[index];
                    var time = buffTimes[index];
                    if (type <= 0 || time <= 0)
                    {
                        continue;
                    }

                    buffs.Add(new BuffSnapshot
                    {
                        BuffType = type,
                        BuffTime = time,
                        BuffName = string.Empty
                    });
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "buff-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "BuffReader",
                    "Buff state read failed: " + error.Message);
            }

            return buffs;
        }
    }
}
