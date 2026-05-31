namespace JueMingZ.Actions
{
    public enum InputActionStatus
    {
        Pending,
        Running,
        Succeeded,
        AttemptedButUnverified,
        NotApplicable,
        Failed,
        TimedOut,
        Cancelled,
        BlockedByUi,
        BlockedByHigherPriority,
        NotImplemented
    }
}
