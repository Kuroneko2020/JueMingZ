using System.Collections.Generic;
using JueMingZ.Actions;

namespace JueMingZ.Features
{
    public sealed class FeatureDefinitionBuilder
    {
        private readonly FeatureDefinition _definition;
        private bool _lifecycleExplicit;

        private FeatureDefinitionBuilder(string id, string displayName, string description)
        {
            _definition = new FeatureDefinition
            {
                Id = id ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                Description = description ?? string.Empty,
                HotkeyDisplayName = displayName ?? string.Empty
            };
        }

        public static FeatureDefinitionBuilder Create(string id, string displayName, string description)
        {
            return new FeatureDefinitionBuilder(id, displayName, description);
        }

        public FeatureDefinitionBuilder Domain(FeatureCodeDomain value)
        {
            _definition.CodeDomain = value;
            return this;
        }

        public FeatureDefinitionBuilder Category(FeatureUserCategory value)
        {
            _definition.UserCategory = value;
            return this;
        }

        public FeatureDefinitionBuilder Actions(params InputActionKind[] values)
        {
            _definition.RequiredActions = Normalize(values, InputActionKind.None);
            return this;
        }

        public FeatureDefinitionBuilder GameState(params GameStateKind[] values)
        {
            _definition.RequiredGameState = Normalize(values, GameStateKind.None);
            return this;
        }

        public FeatureDefinitionBuilder Multiplayer(FeatureMultiplayerSupport value)
        {
            _definition.MultiplayerSupport = value;
            return this;
        }

        public FeatureDefinitionBuilder Config(FeatureConfigUiKind value)
        {
            _definition.ConfigUiKind = value;
            _definition.HasConfig = value != FeatureConfigUiKind.None;
            return this;
        }

        public FeatureDefinitionBuilder Hotkey(bool hasHotkey, bool listVisible = false, string displayName = null)
        {
            _definition.HasHotkey = hasHotkey;
            _definition.HotkeyListVisible = listVisible;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                _definition.HotkeyDisplayName = displayName;
            }

            return this;
        }

        public FeatureDefinitionBuilder DefaultEnabled(bool value)
        {
            _definition.DefaultEnabled = value;
            return this;
        }

        public FeatureDefinitionBuilder VisibleInMainUi(bool value)
        {
            _definition.VisibleInMainUi = value;
            return this;
        }

        public FeatureDefinitionBuilder Implemented(bool value)
        {
            _definition.IsImplemented = value;
            return this;
        }

        public FeatureDefinitionBuilder Lifecycle(FeatureLifecycleStatus value)
        {
            _definition.LifecycleStatus = value;
            _lifecycleExplicit = true;
            return this;
        }

        public FeatureDefinitionBuilder InternalPlatform(bool value = true)
        {
            _definition.IsInternalPlatform = value;
            return this;
        }

        public FeatureDefinitionBuilder ExclusiveGroup(string value)
        {
            _definition.ExclusiveGroup = value ?? string.Empty;
            return this;
        }

        public FeatureDefinitionBuilder Priority(int value)
        {
            _definition.Priority = value;
            return this;
        }

        public FeatureDefinitionBuilder Notes(string value)
        {
            _definition.Notes = value ?? string.Empty;
            _definition.DetailedNotes = value ?? string.Empty;
            return this;
        }

        public FeatureDefinitionBuilder DetailedNotes(string value)
        {
            _definition.DetailedNotes = value ?? string.Empty;
            return this;
        }

        public FeatureDefinition Build()
        {
            if (!_lifecycleExplicit)
            {
                if (_definition.IsInternalPlatform)
                {
                    _definition.LifecycleStatus = FeatureLifecycleStatus.InternalPlatform;
                }
                else
                {
                    _definition.LifecycleStatus = _definition.IsImplemented
                        ? FeatureLifecycleStatus.Implemented
                        : FeatureLifecycleStatus.Planned;
                }
            }

            if (_definition.LifecycleStatus == FeatureLifecycleStatus.Planned ||
                _definition.LifecycleStatus == FeatureLifecycleStatus.Suspended)
            {
                _definition.VisibleInMainUi = false;
                _definition.HasHotkey = false;
                _definition.HotkeyListVisible = false;
            }

            if (string.IsNullOrWhiteSpace(_definition.HotkeyDisplayName))
            {
                _definition.HotkeyDisplayName = _definition.DisplayName;
            }

            if (_definition.ConfigUiKind == FeatureConfigUiKind.None)
            {
                _definition.HasConfig = false;
            }

            return _definition;
        }

        private static IReadOnlyList<T> Normalize<T>(IEnumerable<T> values, T fallback)
        {
            var list = new List<T>();
            if (values != null)
            {
                foreach (var value in values)
                {
                    list.Add(value);
                }
            }

            if (list.Count == 0)
            {
                list.Add(fallback);
            }

            return list;
        }
    }
}
