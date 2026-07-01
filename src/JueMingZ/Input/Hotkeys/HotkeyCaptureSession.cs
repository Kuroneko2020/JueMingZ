using System.Collections.Generic;

namespace JueMingZ.Input.Hotkeys
{
    public sealed class HotkeyCaptureSession
    {
        private readonly Dictionary<int, bool> _wasDown = new Dictionary<int, bool>();

        internal Dictionary<int, bool> WasDown
        {
            get { return _wasDown; }
        }

        public void Clear()
        {
            _wasDown.Clear();
        }
    }
}
