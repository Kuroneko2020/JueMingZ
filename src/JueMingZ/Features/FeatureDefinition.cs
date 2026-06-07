using System.Collections.Generic;
using JueMingZ.Actions;

namespace JueMingZ.Features
{
    public sealed class FeatureDefinition
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DetailedNotes { get; set; }
        // CodeDomain is implementation ownership; UserCategory is the F5 page and may intentionally differ.
        public FeatureCodeDomain CodeDomain { get; set; }
        public FeatureUserCategory UserCategory { get; set; }
        public FeatureMultiplayerSupport MultiplayerSupport { get; set; }
        public IReadOnlyList<InputActionKind> RequiredActions { get; set; }
        public IReadOnlyList<GameStateKind> RequiredGameState { get; set; }
        public bool DefaultEnabled { get; set; }
        public bool VisibleInMainUi { get; set; }
        public bool VisibleInUi
        {
            get { return VisibleInMainUi; }
            set { VisibleInMainUi = value; }
        }
        public bool HasHotkey { get; set; }
        public bool HotkeyListVisible { get; set; }
        public string HotkeyDisplayName { get; set; }
        public bool HasConfig { get; set; }
        public FeatureConfigUiKind ConfigUiKind { get; set; }
        public bool HasCustomUi { get; set; }
        public bool IsImplemented { get; set; }
        public bool IsInternalPlatform { get; set; }
        public FeatureLifecycleStatus LifecycleStatus { get; set; }
        public string ExclusiveGroup { get; set; }
        public int Priority { get; set; }
        public string Notes { get; set; }

        public FeatureDefinition()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            Description = string.Empty;
            DetailedNotes = string.Empty;
            RequiredActions = new List<InputActionKind> { InputActionKind.None };
            RequiredGameState = new List<GameStateKind> { GameStateKind.None };
            MultiplayerSupport = FeatureMultiplayerSupport.BlockedUntilImplemented;
            VisibleInMainUi = true;
            HotkeyDisplayName = string.Empty;
            ConfigUiKind = FeatureConfigUiKind.None;
            LifecycleStatus = FeatureLifecycleStatus.Planned;
            ExclusiveGroup = string.Empty;
            Notes = string.Empty;
        }
    }
}
