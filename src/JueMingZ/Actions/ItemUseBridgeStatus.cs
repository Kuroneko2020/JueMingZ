namespace JueMingZ.Actions
{
    public enum ItemUseBridgeStatus
    {
        // Bridge status mirrors the ItemCheck lifecycle; terminal cleanup must run
        // before callers observe Succeeded, Failed, Expired, or Cancelled.
        None,
        WaitingForItemCheck,
        Consumed,
        Succeeded,
        AttemptedButUnverified,
        Failed,
        Expired,
        Cancelled
    }
}
