using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JueMingZ.Compat;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class UnifiedHotkeyRuntimeInputState
    {
        public static readonly UnifiedHotkeyRuntimeInputState Physical =
            new UnifiedHotkeyRuntimeInputState(IsPhysicalKeyDown);

        private readonly Func<int, bool> _isKeyDown;

        public UnifiedHotkeyRuntimeInputState(Func<int, bool> isKeyDown)
        {
            _isKeyDown = isKeyDown;
        }

        public bool IsDown(int virtualKey)
        {
            return virtualKey > 0 && _isKeyDown != null && _isKeyDown(virtualKey);
        }

        public static UnifiedHotkeyRuntimeInputState FromDictionary(IDictionary<int, bool> downKeys)
        {
            return new UnifiedHotkeyRuntimeInputState(
                key => downKeys != null && downKeys.TryGetValue(key, out var down) && down);
        }

        private static bool IsPhysicalKeyDown(int virtualKey)
        {
            return virtualKey > 0 &&
                   TerrariaMainCompat.AllowsInputProcessing &&
                   (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }
}
