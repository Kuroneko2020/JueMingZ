namespace JueMingZ.Input.Hotkeys
{
    public sealed class HotkeyCaptureResult
    {
        private HotkeyCaptureResult(
            HotkeyCaptureResultKind kind,
            string resultCode,
            string normalized,
            string display,
            string token)
        {
            Kind = kind;
            ResultCode = resultCode ?? string.Empty;
            Normalized = normalized ?? string.Empty;
            Display = display ?? string.Empty;
            Token = token ?? string.Empty;
        }

        public HotkeyCaptureResultKind Kind { get; private set; }
        public string ResultCode { get; private set; }
        public string Normalized { get; private set; }
        public string Display { get; private set; }
        public string Token { get; private set; }

        public bool HasResult
        {
            get { return Kind != HotkeyCaptureResultKind.None; }
        }

        public static HotkeyCaptureResult None()
        {
            return new HotkeyCaptureResult(HotkeyCaptureResultKind.None, "none", string.Empty, string.Empty, string.Empty);
        }

        public static HotkeyCaptureResult Captured(HotkeyParseResult parse)
        {
            return new HotkeyCaptureResult(
                HotkeyCaptureResultKind.Captured,
                "captured",
                parse == null ? string.Empty : parse.Normalized,
                parse == null ? string.Empty : parse.Display,
                string.Empty);
        }

        public static HotkeyCaptureResult Cleared()
        {
            return new HotkeyCaptureResult(HotkeyCaptureResultKind.Cleared, "cleared", string.Empty, string.Empty, "Backspace");
        }

        public static HotkeyCaptureResult Cancelled()
        {
            return new HotkeyCaptureResult(HotkeyCaptureResultKind.Cancelled, "cancelled", string.Empty, string.Empty, "Esc");
        }

        public static HotkeyCaptureResult Failed(string resultCode, string token)
        {
            return new HotkeyCaptureResult(HotkeyCaptureResultKind.Failed, resultCode, string.Empty, string.Empty, token);
        }
    }
}
