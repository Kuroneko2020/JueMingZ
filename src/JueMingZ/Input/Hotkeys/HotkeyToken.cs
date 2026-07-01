using System;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class HotkeyToken
    {
        public HotkeyToken(string canonical, string displayName, HotkeyTokenKind kind, HotkeyTokenRole role, int virtualKey)
        {
            if (string.IsNullOrWhiteSpace(canonical))
            {
                throw new ArgumentException("Hotkey token canonical name is required.", "canonical");
            }

            Canonical = canonical.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Canonical : displayName.Trim();
            Kind = kind;
            Role = role;
            VirtualKey = virtualKey;
        }

        public string Canonical { get; private set; }
        public string DisplayName { get; private set; }
        public HotkeyTokenKind Kind { get; private set; }
        public HotkeyTokenRole Role { get; private set; }
        public int VirtualKey { get; private set; }

        public bool IsModifier
        {
            get { return Role == HotkeyTokenRole.Modifier; }
        }
    }
}
