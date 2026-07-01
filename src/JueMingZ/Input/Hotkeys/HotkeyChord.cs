using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class HotkeyChord
    {
        public HotkeyChord(IList<HotkeyToken> modifiers, HotkeyToken primaryKey)
        {
            var copy = new List<HotkeyToken>();
            for (var index = 0; modifiers != null && index < modifiers.Count; index++)
            {
                if (modifiers[index] != null)
                {
                    copy.Add(modifiers[index]);
                }
            }

            Modifiers = new ReadOnlyCollection<HotkeyToken>(copy);
            PrimaryKey = primaryKey;
            Normalized = BuildNormalized(Modifiers, PrimaryKey);
            Display = HotkeyDisplayFormatter.FormatChord(this);
        }

        public IReadOnlyList<HotkeyToken> Modifiers { get; private set; }
        public HotkeyToken PrimaryKey { get; private set; }
        public string Normalized { get; private set; }
        public string Display { get; private set; }

        private static string BuildNormalized(IReadOnlyList<HotkeyToken> modifiers, HotkeyToken primaryKey)
        {
            var parts = new List<string>();
            for (var index = 0; modifiers != null && index < modifiers.Count; index++)
            {
                parts.Add(modifiers[index].Canonical);
            }

            if (primaryKey != null)
            {
                parts.Add(primaryKey.Canonical);
            }

            return string.Join("+", parts.ToArray());
        }
    }
}
