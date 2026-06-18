using System.Collections.Generic;
using System.Runtime.Serialization;

namespace JueMingZ.Config
{
    [DataContract]
    public sealed class HotkeySettings
    {
        [DataMember(Order = 1)]
        public int ConfigVersion { get; set; } = 4;

        [DataMember(Order = 2)]
        public Dictionary<string, string> HotkeysByFeatureId { get; set; } = new Dictionary<string, string>();

        [DataMember(Order = 3)]
        public List<QuickItemHotkeyBinding> QuickItemHotkeyBindings { get; set; } = CreateDefaultQuickItemHotkeyBindings();

        [DataMember(Order = 4)]
        public Dictionary<string, string> ToggleHotkeysByTargetId { get; set; } = new Dictionary<string, string>();

        [DataMember(Order = 5)]
        public Dictionary<string, string> LastNonOffModeByTargetId { get; set; } = new Dictionary<string, string>();

        public static HotkeySettings CreateDefault()
        {
            return new HotkeySettings
            {
                ConfigVersion = 4,
                HotkeysByFeatureId = new Dictionary<string, string>(),
                QuickItemHotkeyBindings = CreateDefaultQuickItemHotkeyBindings(),
                // Feature-toggle hotkeys intentionally live outside HotkeysByFeatureId;
                // automation.auto_mining already uses that legacy table as its mining trigger.
                ToggleHotkeysByTargetId = new Dictionary<string, string>(),
                LastNonOffModeByTargetId = new Dictionary<string, string>()
            };
        }

        public static List<QuickItemHotkeyBinding> CreateDefaultQuickItemHotkeyBindings()
        {
            return new List<QuickItemHotkeyBinding>();
        }
    }

    [DataContract]
    public sealed class QuickItemHotkeyBinding
    {
        [DataMember(Order = 1)]
        public string Hotkey { get; set; } = string.Empty;

        [DataMember(Order = 2)]
        public List<int> ItemTypes { get; set; } = new List<int>();

        [DataMember(Order = 3)]
        public string DisplayName { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public bool Enabled { get; set; } = true;
    }
}
