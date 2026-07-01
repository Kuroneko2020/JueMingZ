using System;
using System.Collections.Generic;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyParser
    {
        public static bool TryParse(string text, out HotkeyChord chord)
        {
            var result = Parse(text);
            chord = result.Chord;
            return result.Succeeded;
        }

        public static HotkeyParseResult Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return HotkeyParseResult.Fail(HotkeyParseFailureReason.MissingPrimaryKey, string.Empty);
            }

            var modifiers = new Dictionary<string, HotkeyToken>(StringComparer.Ordinal);
            HotkeyToken primary = null;
            var parts = text.Split('+');
            for (var index = 0; index < parts.Length; index++)
            {
                var raw = parts[index] == null ? string.Empty : parts[index].Trim();
                if (raw.Length <= 0)
                {
                    return HotkeyParseResult.Fail(HotkeyParseFailureReason.InvalidToken, raw);
                }

                if (HotkeyUnsupportedTokenPolicy.IsUnsupportedToken(raw))
                {
                    return HotkeyParseResult.Fail(HotkeyParseFailureReason.UnsupportedToken, raw);
                }

                HotkeyToken token;
                if (!HotkeyTokenCatalog.TryGetToken(raw, out token))
                {
                    return HotkeyParseResult.Fail(HotkeyParseFailureReason.InvalidToken, raw);
                }

                if (token.IsModifier)
                {
                    if (modifiers.ContainsKey(token.Canonical))
                    {
                        return HotkeyParseResult.Fail(HotkeyParseFailureReason.DuplicateModifier, token.Canonical);
                    }

                    modifiers[token.Canonical] = token;
                    continue;
                }

                if (primary != null)
                {
                    return HotkeyParseResult.Fail(HotkeyParseFailureReason.TooManyPrimaryKeys, token.Canonical);
                }

                primary = token;
            }

            if (primary == null)
            {
                return HotkeyParseResult.Fail(HotkeyParseFailureReason.MissingPrimaryKey, string.Empty);
            }

            if (HotkeyReservedKeyPolicy.IsReservedPrimary(primary))
            {
                return HotkeyParseResult.Fail(HotkeyParseFailureReason.ReservedKey, primary.Canonical);
            }

            var sortedModifiers = new List<HotkeyToken>(modifiers.Values);
            sortedModifiers.Sort(CompareModifiers);
            return HotkeyParseResult.Success(new HotkeyChord(sortedModifiers, primary));
        }

        private static int CompareModifiers(HotkeyToken left, HotkeyToken right)
        {
            return HotkeyTokenCatalog.GetModifierSortIndex(left).CompareTo(HotkeyTokenCatalog.GetModifierSortIndex(right));
        }
    }
}
