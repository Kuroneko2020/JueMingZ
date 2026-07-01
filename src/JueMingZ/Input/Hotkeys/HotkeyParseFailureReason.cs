namespace JueMingZ.Input.Hotkeys
{
    public enum HotkeyParseFailureReason
    {
        None,
        ReservedKey,
        InvalidToken,
        UnsupportedToken,
        DuplicateModifier,
        MissingPrimaryKey,
        TooManyPrimaryKeys
    }
}
