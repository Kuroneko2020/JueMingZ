using System;
using System.Collections.Generic;
using JueMingZ.Automation.Information;
using JueMingZ.Config;
using JueMingZ.Diagnostics;

namespace JueMingZ.Automation.Fishing.Filtering
{
    // Presets seed filter policy only; they must not be treated as confirmed vanilla catch outcomes.
    internal static class FishingFilterDefaultPresets
    {
        public const string LowFishingPowerJunkName = "低渔力垃圾";
        private static readonly object SyncRoot = new object();
        private static FishingFilterPreset _lowFishingPowerJunkPreset;

        public static bool TryGetLowFishingPowerJunkPreset(out FishingFilterPreset preset)
        {
            lock (SyncRoot)
            {
                if (_lowFishingPowerJunkPreset != null)
                {
                    preset = ClonePreset(_lowFishingPowerJunkPreset);
                    return true;
                }

                var itemIdType = InformationReflection.FindType("Terraria.ID.ItemID");
                if (itemIdType == null)
                {
                    LogDefaultPresetUnavailable("Terraria.ID.ItemID type not found.");
                    preset = null;
                    return false;
                }

                int oldShoe;
                int seaweed;
                int tinCan;
                if (!InformationReflection.TryReadStaticInt(itemIdType, "OldShoe", out oldShoe) || oldShoe <= 0 ||
                    !InformationReflection.TryReadStaticInt(itemIdType, "Seaweed", out seaweed) || seaweed <= 0 ||
                    !InformationReflection.TryReadStaticInt(itemIdType, "TinCan", out tinCan) || tinCan <= 0)
                {
                    LogDefaultPresetUnavailable("ItemID.OldShoe / Seaweed / TinCan reflection failed.");
                    preset = null;
                    return false;
                }

                _lowFishingPowerJunkPreset = new FishingFilterPreset
                {
                    Name = LowFishingPowerJunkName,
                    FilterModeScope = FishingFilterModes.DenyList,
                    MatchModeScope = FishingFilterMatchModes.Exact,
                    ExactEntries = new List<FishingFilterExactEntry>
                    {
                        new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = oldShoe, DisplayNameSnapshot = "Old Shoe" },
                        new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = seaweed, DisplayNameSnapshot = "Seaweed" },
                        new FishingFilterExactEntry { Kind = FishingCatchKinds.Item, Id = tinCan, DisplayNameSnapshot = "Tin Can" }
                    },
                    Keywords = new List<string>(),
                    UpdatedAt = string.Empty
                };

                preset = ClonePreset(_lowFishingPowerJunkPreset);
                return true;
            }
        }

        public static bool IsLowFishingPowerJunkScope(string filterMode, string matchMode)
        {
            return string.Equals(FishingFilterModes.Normalize(filterMode), FishingFilterModes.DenyList, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(FishingFilterMatchModes.Normalize(matchMode), FishingFilterMatchModes.Exact, StringComparison.OrdinalIgnoreCase);
        }

        private static FishingFilterPreset ClonePreset(FishingFilterPreset preset)
        {
            if (preset == null)
            {
                return null;
            }

            var clone = new FishingFilterPreset
            {
                Name = preset.Name ?? string.Empty,
                FilterModeScope = preset.FilterModeScope ?? string.Empty,
                MatchModeScope = preset.MatchModeScope ?? string.Empty,
                ExactEntries = new List<FishingFilterExactEntry>(),
                Keywords = new List<string>(),
                UpdatedAt = preset.UpdatedAt ?? string.Empty
            };

            if (preset.ExactEntries != null)
            {
                for (var index = 0; index < preset.ExactEntries.Count; index++)
                {
                    var entry = preset.ExactEntries[index];
                    if (entry == null)
                    {
                        continue;
                    }

                    clone.ExactEntries.Add(new FishingFilterExactEntry
                    {
                        Kind = entry.Kind ?? string.Empty,
                        Id = entry.Id,
                        DisplayNameSnapshot = entry.DisplayNameSnapshot ?? string.Empty
                    });
                }
            }

            if (preset.Keywords != null)
            {
                for (var index = 0; index < preset.Keywords.Count; index++)
                {
                    clone.Keywords.Add(preset.Keywords[index] ?? string.Empty);
                }
            }

            return clone;
        }

        private static void LogDefaultPresetUnavailable(string reason)
        {
            LogThrottle.WarnThrottled(
                "fishing-filter-low-junk-preset-unavailable",
                TimeSpan.FromSeconds(30),
                "FishingFilterDefaultPresets",
                "Default fishing filter preset was not generated: " + reason);
        }
    }
}
