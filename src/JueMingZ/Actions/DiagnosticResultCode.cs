namespace JueMingZ.Actions
{
    public enum DiagnosticResultCode
    {
        Queued,
        Succeeded,
        AttemptedButUnverified,
        BlockedByEnvironment,
        BlockedByCooldown,
        BlockedByUi,
        MissingRequiredItem,
        NotApplicable,
        Failed,
        TimedOut,
        NotImplemented
    }
}
