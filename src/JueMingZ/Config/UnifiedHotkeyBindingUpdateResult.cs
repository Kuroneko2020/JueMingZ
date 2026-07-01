namespace JueMingZ.Config
{
    public sealed class UnifiedHotkeyBindingUpdateResult
    {
        private UnifiedHotkeyBindingUpdateResult(
            bool succeeded,
            bool changed,
            string resultCode,
            string bindingId,
            string normalized,
            string display,
            string message)
        {
            Succeeded = succeeded;
            Changed = changed;
            ResultCode = resultCode ?? string.Empty;
            BindingId = bindingId ?? string.Empty;
            Normalized = normalized ?? string.Empty;
            Display = display ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; private set; }
        public bool Changed { get; private set; }
        public string ResultCode { get; private set; }
        public string BindingId { get; private set; }
        public string Normalized { get; private set; }
        public string Display { get; private set; }
        public string Message { get; private set; }

        public static UnifiedHotkeyBindingUpdateResult Updated(
            string bindingId,
            string normalized,
            string display,
            bool changed)
        {
            return new UnifiedHotkeyBindingUpdateResult(
                true,
                changed,
                changed ? "updated" : "unchanged",
                bindingId,
                normalized,
                display,
                string.Empty);
        }

        public static UnifiedHotkeyBindingUpdateResult Cleared(string bindingId, bool changed)
        {
            return new UnifiedHotkeyBindingUpdateResult(
                true,
                changed,
                changed ? "cleared" : "alreadyEmpty",
                bindingId,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static UnifiedHotkeyBindingUpdateResult Failure(
            string resultCode,
            string bindingId,
            string message)
        {
            return new UnifiedHotkeyBindingUpdateResult(
                false,
                false,
                resultCode,
                bindingId,
                string.Empty,
                string.Empty,
                message);
        }

        public static UnifiedHotkeyBindingUpdateResult SaveFailed(string bindingId, string message)
        {
            return Failure("saveFailed", bindingId, message);
        }
    }
}
