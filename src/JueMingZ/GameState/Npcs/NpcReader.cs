using System;
using System.Collections.Generic;
using JueMingZ.Compat;
using JueMingZ.Diagnostics;
using TerrariaNpc = Terraria.NPC;

namespace JueMingZ.GameState.Npcs
{
    public static class NpcReader
    {
        public static NpcSummarySnapshot Read(Type mainType)
        {
            return Read(mainType, false);
        }

        public static NpcSummarySnapshot Read(Type mainType, bool includeCatchableCritters)
        {
            return mainType == null
                ? new NpcSummarySnapshot()
                : Read(includeCatchableCritters ? NpcReadProfile.CatchableCrittersOnly : NpcReadProfile.CountsOnly);
        }

        public static NpcSummarySnapshot Read(bool includeCatchableCritters)
        {
            return Read(includeCatchableCritters ? NpcReadProfile.CatchableCrittersOnly : NpcReadProfile.CountsOnly);
        }

        public static NpcSummarySnapshot Read(NpcReadProfile profile)
        {
            var summary = new NpcSummarySnapshot();
            // Reader profiles bound NPC scan cost; unavailable collections
            // produce empty snapshots rather than fake NPC state.
            if (profile == NpcReadProfile.None)
            {
                return summary;
            }

            try
            {
                var npcs = TerrariaMainCompat.Npcs;
                if (npcs == null)
                {
                    return summary;
                }

                var includeCounts = Has(profile, NpcReadProfile.Counts);
                var includeCatchableCritters = Has(profile, NpcReadProfile.CatchableCritters);
                var catchableCritters = includeCatchableCritters ? new List<NpcSnapshot>() : null;
                foreach (var npc in npcs)
                {
                    if (npc == null)
                    {
                        continue;
                    }

                    var active = TerrariaNpcReadCompat.IsActive(npc);
                    if (!active)
                    {
                        continue;
                    }

                    bool townNpc;
                    bool friendly;
                    bool critter;
                    if (includeCounts)
                    {
                        summary.ActiveNpcCount++;
                        townNpc = TerrariaNpcReadCompat.IsTownNpc(npc);
                        friendly = TerrariaNpcReadCompat.IsFriendly(npc);
                        critter = TerrariaNpcReadCompat.IsCritter(npc);

                        if (townNpc)
                        {
                            summary.TownNpcCount++;
                        }

                        if (critter)
                        {
                            summary.CritterCount++;
                        }

                        if (!townNpc && !friendly && !critter)
                        {
                            summary.HostileNpcCount++;
                        }
                    }
                    else
                    {
                        townNpc = false;
                        friendly = false;
                        critter = false;
                    }

                    if (catchableCritters != null)
                    {
                        var catchItem = TerrariaNpcReadCompat.CatchItem(npc);
                        if (catchItem > 0)
                        {
                            // Catchable critter snapshots are read-only targeting
                            // inputs; capture actions still go through ActionQueue.
                            if (!includeCounts)
                            {
                                townNpc = TerrariaNpcReadCompat.IsTownNpc(npc);
                                friendly = TerrariaNpcReadCompat.IsFriendly(npc);
                                critter = TerrariaNpcReadCompat.IsCritter(npc);
                            }

                            catchableCritters.Add(ReadNpcSnapshot(
                                npc,
                                profile,
                                TerrariaNpcReadCompat.Type(npc),
                                active,
                                townNpc,
                                friendly,
                                critter,
                                catchItem));
                        }
                    }
                }

                if (catchableCritters != null)
                {
                    summary.CatchableCritters = catchableCritters;
                }
            }
            catch (Exception error)
            {
                LogThrottle.WarnThrottled(
                    "npc-state-read-failed",
                    TimeSpan.FromSeconds(30),
                    "NpcReader",
                    "NPC state read failed: " + error.Message);
            }

            return summary;
        }

        private static NpcSnapshot ReadNpcSnapshot(
            TerrariaNpc npc,
            NpcReadProfile profile,
            int type,
            bool active,
            bool townNpc,
            bool friendly,
            bool critter,
            int catchItem)
        {
            var includePositions = Has(profile, NpcReadProfile.Positions);

            var snapshot = new NpcSnapshot
            {
                WhoAmI = TerrariaNpcReadCompat.WhoAmI(npc),
                Type = type,
                Active = active,
                TownNpc = townNpc,
                Friendly = friendly,
                Hostile = !townNpc && !friendly && !critter,
                Critter = critter,
                CatchItem = catchItem
            };

            if (Has(profile, NpcReadProfile.Names))
            {
                snapshot.Name = TerrariaNpcReadCompat.Name(npc);
            }

            if (includePositions)
            {
                var position = TerrariaNpcReadCompat.Position(npc);
                var center = TerrariaNpcReadCompat.Center(npc);
                snapshot.PositionX = position.X;
                snapshot.PositionY = position.Y;
                snapshot.CenterX = center.X;
                snapshot.CenterY = center.Y;
                snapshot.Width = TerrariaNpcReadCompat.Width(npc);
                snapshot.Height = TerrariaNpcReadCompat.Height(npc);
            }

            return snapshot;
        }

        private static bool Has(NpcReadProfile profile, NpcReadProfile flag)
        {
            return (profile & flag) == flag;
        }
    }
}
