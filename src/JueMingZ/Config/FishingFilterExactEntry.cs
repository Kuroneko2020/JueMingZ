using System.Runtime.Serialization;

namespace JueMingZ.Config
{
    [DataContract]
    public sealed class FishingFilterExactEntry
    {
        [DataMember(Order = 1)]
        public string Kind { get; set; }

        [DataMember(Order = 2)]
        public int Id { get; set; }

        // Exact matching uses Kind and Id; this display snapshot is only a UI hint.
        [DataMember(Order = 3)]
        public string DisplayNameSnapshot { get; set; }

        public FishingFilterExactEntry()
        {
            Kind = string.Empty;
            DisplayNameSnapshot = string.Empty;
        }
    }
}
