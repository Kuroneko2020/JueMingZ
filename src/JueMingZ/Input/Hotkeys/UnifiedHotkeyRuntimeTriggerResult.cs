namespace JueMingZ.Input.Hotkeys
{
    public struct UnifiedHotkeyRuntimeTriggerResult
    {
        private UnifiedHotkeyRuntimeTriggerResult(
            string bindingId,
            string resultCode,
            string reason,
            bool down,
            bool wasDown,
            bool pressedEdge,
            UnifiedHotkeyRuntimeBinding binding)
        {
            BindingId = bindingId ?? string.Empty;
            ResultCode = resultCode ?? string.Empty;
            Reason = reason ?? string.Empty;
            Down = down;
            WasDown = wasDown;
            PressedEdge = pressedEdge;
            Binding = binding;
        }

        public string BindingId { get; private set; }
        public string ResultCode { get; private set; }
        public string Reason { get; private set; }
        public bool Down { get; private set; }
        public bool WasDown { get; private set; }
        public bool PressedEdge { get; private set; }
        public UnifiedHotkeyRuntimeBinding Binding { get; private set; }

        public string Display
        {
            get { return Binding == null ? string.Empty : Binding.Display; }
        }

        public static UnifiedHotkeyRuntimeTriggerResult Missing(string bindingId)
        {
            return new UnifiedHotkeyRuntimeTriggerResult(bindingId, "missingBinding", "missingBinding", false, false, false, null);
        }

        public static UnifiedHotkeyRuntimeTriggerResult Idle(string bindingId, bool down, bool wasDown, UnifiedHotkeyRuntimeBinding binding)
        {
            return new UnifiedHotkeyRuntimeTriggerResult(bindingId, "idle", string.Empty, down, wasDown, false, binding);
        }

        public static UnifiedHotkeyRuntimeTriggerResult Blocked(string bindingId, string reason, bool down, bool wasDown, UnifiedHotkeyRuntimeBinding binding)
        {
            return new UnifiedHotkeyRuntimeTriggerResult(bindingId, "blocked", reason, down, wasDown, true, binding);
        }

        public static UnifiedHotkeyRuntimeTriggerResult Triggered(string bindingId, UnifiedHotkeyRuntimeBinding binding)
        {
            return new UnifiedHotkeyRuntimeTriggerResult(bindingId, "triggered", string.Empty, true, false, true, binding);
        }
    }
}
