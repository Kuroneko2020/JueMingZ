using System;

namespace JueMingZ.Config
{
    public sealed class ConfigSaveSummary
    {
        public DateTime Utc { get; set; }
        public bool Succeeded { get; set; }
        public ConfigFileSaveResult AppSettings { get; set; }
        public ConfigFileSaveResult FeatureSettings { get; set; }
        public ConfigFileSaveResult HotkeySettings { get; set; }
        public string Summary { get; set; }

        public ConfigSaveSummary()
        {
            Utc = DateTime.UtcNow;
            Summary = string.Empty;
        }
    }
}
