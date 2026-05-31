using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Config
{
    [DataContract]
    public sealed class FeatureSettings
    {
        [DataMember(Order = 1)]
        public int ConfigVersion { get; set; } = 1;

        [DataMember(Order = 2)]
        public Dictionary<string, bool> EnabledByFeatureId { get; set; } = new Dictionary<string, bool>();

        public static FeatureSettings CreateDefault()
        {
            return new FeatureSettings
            {
                ConfigVersion = 1,
                EnabledByFeatureId = new Dictionary<string, bool>()
            };
        }
    }
}
