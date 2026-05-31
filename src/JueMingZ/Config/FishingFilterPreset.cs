using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Config
{
    [DataContract]
    public sealed class FishingFilterPreset
    {
        [DataMember(Order = 1)]
        public string Name { get; set; }

        [DataMember(Order = 2)]
        public string FilterModeScope { get; set; }

        [DataMember(Order = 3)]
        public string MatchModeScope { get; set; }

        [DataMember(Order = 4)]
        public List<FishingFilterExactEntry> ExactEntries { get; set; }

        [DataMember(Order = 5)]
        public List<string> Keywords { get; set; }

        [DataMember(Order = 6)]
        public string UpdatedAt { get; set; }

        public FishingFilterPreset()
        {
            Name = string.Empty;
            FilterModeScope = FishingFilterModes.AllowList;
            MatchModeScope = FishingFilterMatchModes.Exact;
            ExactEntries = new List<FishingFilterExactEntry>();
            Keywords = new List<string>();
            UpdatedAt = string.Empty;
        }
    }
}
