namespace JueMingZ.Input.Hotkeys
{
    public sealed class HotkeyParseResult
    {
        private HotkeyParseResult(HotkeyChord chord, HotkeyParseFailureReason failureReason, string token)
        {
            Chord = chord;
            FailureReason = failureReason;
            Token = token ?? string.Empty;
        }

        public bool Succeeded
        {
            get { return Chord != null && FailureReason == HotkeyParseFailureReason.None; }
        }

        public HotkeyChord Chord { get; private set; }
        public HotkeyParseFailureReason FailureReason { get; private set; }
        public string Token { get; private set; }

        public string Normalized
        {
            get { return Chord == null ? string.Empty : Chord.Normalized; }
        }

        public string Display
        {
            get { return Chord == null ? string.Empty : Chord.Display; }
        }

        public string Reason
        {
            get { return ToReasonString(FailureReason); }
        }

        public static HotkeyParseResult Success(HotkeyChord chord)
        {
            return new HotkeyParseResult(chord, HotkeyParseFailureReason.None, string.Empty);
        }

        public static HotkeyParseResult Fail(HotkeyParseFailureReason reason, string token)
        {
            return new HotkeyParseResult(null, reason, token);
        }

        public static string ToReasonString(HotkeyParseFailureReason reason)
        {
            switch (reason)
            {
                case HotkeyParseFailureReason.ReservedKey:
                    return "reservedKey";
                case HotkeyParseFailureReason.InvalidToken:
                    return "invalidToken";
                case HotkeyParseFailureReason.UnsupportedToken:
                    return "unsupportedToken";
                case HotkeyParseFailureReason.DuplicateModifier:
                    return "duplicateModifier";
                case HotkeyParseFailureReason.MissingPrimaryKey:
                    return "missingPrimaryKey";
                case HotkeyParseFailureReason.TooManyPrimaryKeys:
                    return "tooManyPrimaryKeys";
                default:
                    return string.Empty;
            }
        }
    }
}
