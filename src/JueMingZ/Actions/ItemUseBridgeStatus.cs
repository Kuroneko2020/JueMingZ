namespace JueMingZ.Actions
{
    public enum ItemUseBridgeStatus
    {
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
