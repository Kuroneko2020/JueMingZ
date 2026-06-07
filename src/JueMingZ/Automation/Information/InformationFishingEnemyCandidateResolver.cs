using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingZ.Automation.Fishing.Filtering;

namespace JueMingZ.Automation.Information
{
    internal static class InformationFishingEnemyCandidateResolver
    {
        private static readonly string[] VanillaFishableEnemyNpcIdFields =
        {
            "ZombieMerman",
            "EyeballFlyingFish",
            "GoblinShark",
            "BloodEelHead",
            "BloodNautilus",
            "TownSlimeRed"
        };

        private static readonly object SyncRoot = new object();
        private static List<FishableEnemyDefinition> _cachedDefinitions;

        public static void AddFishableEnemyCandidates(IList<FishingCatchCandidate> candidates)
        {
            if (candidates == null)
            {
                return;
            }

            var definitions = GetDefinitions();
            for (var index = 0; index < definitions.Count; index++)
            {
                AddIfMissing(candidates, definitions[index]);
            }
        }

        public static void AddMatchingFishableEnemyCandidates(
            IList<FishingCatchCandidate> candidates,
            string query,
            bool hasIdSearch,
            int searchId)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(query))
            {
                return;
            }

            var searchText = query.Trim();
            var definitions = GetDefinitions();
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                if (!Matches(definition, searchText, hasIdSearch, searchId))
                {
                    continue;
                }

                AddIfMissing(candidates, definition);
            }
        }

        internal static IList<FishingCatchCandidate> ResolveFishableEnemyCandidatesForTesting()
        {
            var result = new List<FishingCatchCandidate>();
            AddFishableEnemyCandidates(result);
            return result;
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _cachedDefinitions = null;
            }
        }

        private static List<FishableEnemyDefinition> GetDefinitions()
        {
            lock (SyncRoot)
            {
                if (_cachedDefinitions != null)
                {
                    return _cachedDefinitions;
                }

                _cachedDefinitions = BuildDefinitions();
                return _cachedDefinitions;
            }
        }

        private static List<FishableEnemyDefinition> BuildDefinitions()
        {
            var result = new List<FishableEnemyDefinition>();
            var seen = new HashSet<int>();
            var npcIdType = InformationReflection.FindType("Terraria.ID.NPCID");
            if (npcIdType == null)
            {
                return result;
            }

            // These NPCs are reported by sonar through a negative bobber
            // localAI[1], so exact filter pickers must offer NPC entries too.
            for (var index = 0; index < VanillaFishableEnemyNpcIdFields.Length; index++)
            {
                var fieldName = VanillaFishableEnemyNpcIdFields[index];
                var npcId = InformationFishingCatchResolver.ToInt(InformationReflection.GetStaticMember(npcIdType, fieldName), 0);
                if (npcId <= 0 || !seen.Add(npcId))
                {
                    continue;
                }

                var displayName = InformationNpcNameCompat.ResolveTypeName(npcId);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    displayName = "#" + npcId.ToString(CultureInfo.InvariantCulture);
                }

                result.Add(new FishableEnemyDefinition(
                    npcId,
                    displayName.Trim(),
                    ResolveInternalNpcName(npcIdType, npcId, fieldName)));
            }

            return result;
        }

        private static string ResolveInternalNpcName(Type npcIdType, int npcId, string fallbackName)
        {
            var search = InformationReflection.GetStaticMember(npcIdType, "Search");
            var raw = InformationFishingCatchResolver.InvokeInstance(search, "GetName", new object[] { npcId });
            var name = raw == null ? string.Empty : Convert.ToString(raw, CultureInfo.InvariantCulture) ?? string.Empty;
            return string.IsNullOrWhiteSpace(name) ? (fallbackName ?? string.Empty) : name.Trim();
        }

        private static bool Matches(FishableEnemyDefinition definition, string query, bool hasIdSearch, int searchId)
        {
            if (definition == null || string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            if (hasIdSearch && definition.Id == searchId)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(definition.DisplayName) &&
                definition.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(definition.InternalName) &&
                   definition.InternalName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddIfMissing(IList<FishingCatchCandidate> candidates, FishableEnemyDefinition definition)
        {
            if (definition == null || definition.Id <= 0 || HasCandidate(candidates, definition.Id))
            {
                return;
            }

            candidates.Add(new FishingCatchCandidate
            {
                Kind = FishingCatchKinds.NPC,
                Id = definition.Id,
                DisplayName = definition.DisplayName,
                DisplayNameSnapshot = definition.DisplayName,
                IsEnemy = true
            });
        }

        private static bool HasCandidate(IList<FishingCatchCandidate> candidates, int npcId)
        {
            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = candidates[index];
                if (candidate != null &&
                    candidate.Id == npcId &&
                    string.Equals(candidate.Kind, FishingCatchKinds.NPC, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FishableEnemyDefinition
        {
            public int Id { get; private set; }
            public string DisplayName { get; private set; }
            public string InternalName { get; private set; }

            public FishableEnemyDefinition(int id, string displayName, string internalName)
            {
                Id = id;
                DisplayName = displayName ?? string.Empty;
                InternalName = internalName ?? string.Empty;
            }
        }
    }
}
