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

        [DataMember(Order = 3)]
        public string DisplayNameSnapshot { get; set; }

        public FishingFilterExactEntry()
        {
            Kind = string.Empty;
            DisplayNameSnapshot = string.Empty;
        }
    }
}
