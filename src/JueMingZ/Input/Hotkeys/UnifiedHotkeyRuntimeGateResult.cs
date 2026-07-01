namespace JueMingZ.Input.Hotkeys
{
    public struct UnifiedHotkeyRuntimeGateResult
    {
        private UnifiedHotkeyRuntimeGateResult(bool blocked, string reason, string detail)
        {
            Blocked = blocked;
            Reason = reason ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public bool Blocked { get; private set; }
        public string Reason { get; private set; }
        public string Detail { get; private set; }

        public static UnifiedHotkeyRuntimeGateResult Allow()
        {
            return new UnifiedHotkeyRuntimeGateResult(false, string.Empty, string.Empty);
        }

        public static UnifiedHotkeyRuntimeGateResult Block(string reason)
        {
            return Block(reason, string.Empty);
        }

        public static UnifiedHotkeyRuntimeGateResult Block(string reason, string detail)
        {
            return new UnifiedHotkeyRuntimeGateResult(true, reason, detail);
        }
    }
}
