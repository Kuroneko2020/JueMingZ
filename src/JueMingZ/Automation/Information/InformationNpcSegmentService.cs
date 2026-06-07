using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Compat;
using Terraria;

namespace JueMingZ.Automation.Information
{
    internal static class InformationNpcSegmentService
    {
        internal static bool ShouldDrawEnemySegmentLabel(int groupSize, int neighborCount)
        {
            return groupSize < 3 || neighborCount < 2;
        }

        internal static bool ShouldDrawEnemyNpcTypeLabelForTesting(int npcType)
        {
            return GetKnownSegmentRole(npcType) != NpcSegmentRole.Body;
        }

        internal static NpcSegmentRole GetKnownSegmentRole(int npcType)
        {
            switch (npcType)
            {
                case 7:   // DevourerHead
                case 10:  // GiantWormHead
                case 13:  // EaterofWorldsHead
                case 39:  // BoneSerpentHead
                case 87:  // WyvernHead
                case 95:  // DiggerHead
                case 98:  // SeekerHead
                case 117: // LeechHead
                case 134: // TheDestroyer
                case 402: // StardustWormHead
                case 412: // SolarCrawltipedeHead
                case 454: // CultistDragonHead
                case 510: // DuneSplicerHead
                case 513: // TombCrawlerHead
                case 621: // BloodEelHead
                    return NpcSegmentRole.Head;

                case 9:   // DevourerTail
                case 12:  // GiantWormTail
                case 15:  // EaterofWorldsTail
                case 41:  // BoneSerpentTail
                case 92:  // WyvernTail
                case 97:  // DiggerTail
                case 100: // SeekerTail
                case 119: // LeechTail
                case 136: // TheDestroyerTail
                case 404: // StardustWormTail
                case 414: // SolarCrawltipedeTail
                case 459: // CultistDragonTail
                case 512: // DuneSplicerTail
                case 515: // TombCrawlerTail
                case 623: // BloodEelTail
                    return NpcSegmentRole.Tail;

                case 8:   // DevourerBody
                case 11:  // GiantWormBody
                case 14:  // EaterofWorldsBody
                case 40:  // BoneSerpentBody
                case 88:  // WyvernLegs
                case 89:  // WyvernBody
                case 90:  // WyvernBody2
                case 91:  // WyvernBody3
                case 96:  // DiggerBody
                case 99:  // SeekerBody
                case 118: // LeechBody
                case 135: // TheDestroyerBody
                case 403: // StardustWormBody
                case 413: // SolarCrawltipedeBody
                case 455: // CultistDragonBody1
                case 456: // CultistDragonBody2
                case 457: // CultistDragonBody3
                case 458: // CultistDragonBody4
                case 511: // DuneSplicerBody
                case 514: // TombCrawlerBody
                case 622: // BloodEelBody
                    return NpcSegmentRole.Body;

                default:
                    return NpcSegmentRole.Unknown;
            }
        }

        internal static Dictionary<int, NpcSegmentInfo> BuildNpcSegmentInfos(NPC[] npcs, int count)
        {
            var result = new Dictionary<int, NpcSegmentInfo>();
            for (var index = 0; npcs != null && index < count && index < npcs.Length; index++)
            {
                var npc = npcs[index];
                if (!TerrariaNpcReadCompat.IsActive(npc))
                {
                    continue;
                }

                var whoAmI = TerrariaNpcReadCompat.WhoAmI(npc);
                if (whoAmI < 0)
                {
                    whoAmI = index;
                }

                var realLife = TerrariaNpcReadCompat.RealLife(npc);
                var info = new NpcSegmentInfo
                {
                    Index = index,
                    WhoAmI = whoAmI,
                    RealLife = realLife,
                    GroupKey = ResolveSegmentGroupKey(index, whoAmI, realLife),
                    References = ReadNpcReferences(npc, count)
                };
                result[index] = info;
            }

            CompleteNpcSegmentInfoCounts(result);
            return result;
        }

        internal static Dictionary<int, NpcSegmentInfo> BuildNpcSegmentInfos(object npcs, int count)
        {
            var result = new Dictionary<int, NpcSegmentInfo>();
            for (var index = 0; index < count; index++)
            {
                var npc = InformationReflection.GetIndexedValue(npcs, index);
                if (npc == null || !IsNpcActive(npc))
                {
                    continue;
                }

                int whoAmI;
                if (!InformationReflection.TryReadInt(npc, "whoAmI", out whoAmI))
                {
                    whoAmI = index;
                }

                int realLife;
                if (!InformationReflection.TryReadInt(npc, "realLife", out realLife))
                {
                    realLife = -1;
                }

                var info = new NpcSegmentInfo
                {
                    Index = index,
                    WhoAmI = whoAmI,
                    RealLife = realLife,
                    GroupKey = ResolveSegmentGroupKey(index, whoAmI, realLife),
                    References = ReadNpcReferences(npc, count)
                };
                result[index] = info;
            }

            CompleteNpcSegmentInfoCounts(result);
            return result;
        }

        internal static int ResolveSegmentGroupKey(int index, int whoAmI, int realLife)
        {
            if (realLife >= 0)
            {
                return realLife;
            }

            return whoAmI >= 0 ? whoAmI : index;
        }

        private static void CompleteNpcSegmentInfoCounts(Dictionary<int, NpcSegmentInfo> result)
        {
            foreach (var pair in result)
            {
                var info = pair.Value;
                info.GroupSize = CountSegmentGroupMembers(result, info.GroupKey);
                info.NeighborCount = CountSegmentNeighbors(result, info);
            }
        }

        private static int[] ReadNpcReferences(object npc, int npcCount)
        {
            var ai = InformationReflection.GetMember(npc, "ai");
            var result = new int[4];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = ReadNpcReference(ai, index, npcCount);
            }

            return result;
        }

        private static int[] ReadNpcReferences(NPC npc, int npcCount)
        {
            var ai = TerrariaNpcReadCompat.Ai(npc);
            var result = new int[4];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = ReadNpcReference(ai, index, npcCount);
            }

            return result;
        }

        private static int ReadNpcReference(float[] ai, int index, int npcCount)
        {
            if (ai == null || index < 0 || index >= ai.Length)
            {
                return -1;
            }

            var value = ai[index];
            var rounded = (int)Math.Round(value);
            if (Math.Abs(value - rounded) > 0.001f ||
                rounded < 0 ||
                rounded >= npcCount)
            {
                return -1;
            }

            return rounded;
        }

        private static int ReadNpcReference(object ai, int index, int npcCount)
        {
            var raw = InformationReflection.GetIndexedValue(ai, index);
            if (raw == null)
            {
                return -1;
            }

            try
            {
                var value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
                var rounded = (int)Math.Round(value);
                if (Math.Abs(value - rounded) > 0.001f ||
                    rounded < 0 ||
                    rounded >= npcCount)
                {
                    return -1;
                }

                return rounded;
            }
            catch
            {
                return -1;
            }
        }

        private static int CountSegmentGroupMembers(Dictionary<int, NpcSegmentInfo> infos, int groupKey)
        {
            var count = 0;
            foreach (var pair in infos)
            {
                if (pair.Value != null && pair.Value.GroupKey == groupKey)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSegmentNeighbors(Dictionary<int, NpcSegmentInfo> infos, NpcSegmentInfo current)
        {
            if (infos == null || current == null)
            {
                return 0;
            }

            var neighbors = new List<int>();
            AddForwardSegmentNeighbors(infos, current, neighbors);
            foreach (var pair in infos)
            {
                var other = pair.Value;
                if (other == null || other.Index == current.Index || other.GroupKey != current.GroupKey)
                {
                    continue;
                }

                if (ReferencesSegment(other, current))
                {
                    AddUniqueNeighbor(neighbors, other.Index);
                }
            }

            return neighbors.Count;
        }

        private static void AddForwardSegmentNeighbors(Dictionary<int, NpcSegmentInfo> infos, NpcSegmentInfo current, IList<int> neighbors)
        {
            for (var index = 0; current.References != null && index < current.References.Length; index++)
            {
                var reference = current.References[index];
                NpcSegmentInfo target;
                if (TryGetSegmentInfoByReference(infos, reference, out target) &&
                    target.Index != current.Index &&
                    target.GroupKey == current.GroupKey)
                {
                    AddUniqueNeighbor(neighbors, target.Index);
                }
            }
        }

        private static bool ReferencesSegment(NpcSegmentInfo source, NpcSegmentInfo target)
        {
            if (source == null || target == null || source.References == null)
            {
                return false;
            }

            for (var index = 0; index < source.References.Length; index++)
            {
                var reference = source.References[index];
                if (reference == target.Index || (target.WhoAmI >= 0 && reference == target.WhoAmI))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetSegmentInfoByReference(Dictionary<int, NpcSegmentInfo> infos, int reference, out NpcSegmentInfo info)
        {
            info = null;
            if (reference < 0 || infos == null)
            {
                return false;
            }

            if (infos.TryGetValue(reference, out info))
            {
                return true;
            }

            foreach (var pair in infos)
            {
                if (pair.Value != null && pair.Value.WhoAmI == reference)
                {
                    info = pair.Value;
                    return true;
                }
            }

            info = null;
            return false;
        }

        private static void AddUniqueNeighbor(IList<int> neighbors, int index)
        {
            for (var existing = 0; existing < neighbors.Count; existing++)
            {
                if (neighbors[existing] == index)
                {
                    return;
                }
            }

            neighbors.Add(index);
        }

        private static bool IsNpcActive(object npc)
        {
            var typedNpc = npc as NPC;
            if (typedNpc != null)
            {
                return TerrariaNpcReadCompat.IsActive(typedNpc);
            }

            bool active;
            return InformationReflection.TryReadBool(npc, "active", out active) && active;
        }
    }
}
