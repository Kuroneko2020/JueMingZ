using System;
using System.Collections.Generic;

namespace JueMingZ.Input.Hotkeys
{
    public static class HotkeyCaptureService
    {
        private const string BackspaceToken = "Backspace";
        private const string EscToken = "Esc";

        private static readonly IReadOnlyList<HotkeyToken> CaptureTokens = BuildCaptureTokens();
        private static readonly IReadOnlyList<HotkeyToken> ModifierTokens = BuildModifierTokens();

        public static void Seed(HotkeyCaptureSession session, Func<int, bool> isKeyDown)
        {
            if (session == null)
            {
                return;
            }

            session.Clear();
            for (var index = 0; index < CaptureTokens.Count; index++)
            {
                var token = CaptureTokens[index];
                session.WasDown[token.VirtualKey] = isKeyDown != null && isKeyDown(token.VirtualKey);
            }
        }

        public static HotkeyCaptureResult Update(HotkeyCaptureSession session, Func<int, bool> isKeyDown)
        {
            if (session == null)
            {
                return HotkeyCaptureResult.None();
            }

            HotkeyToken primary;
            if (!TryCapturePrimaryEdge(session, isKeyDown, out primary))
            {
                return HotkeyCaptureResult.None();
            }

            var tokens = BuildCurrentChordTokens(primary, isKeyDown);
            return EvaluateTokens(tokens);
        }

        public static HotkeyCaptureResult EvaluateTokens(IList<string> tokens)
        {
            if (tokens == null || tokens.Count <= 0)
            {
                return HotkeyCaptureResult.None();
            }

            var parts = new List<string>();
            for (var index = 0; index < tokens.Count; index++)
            {
                var token = string.IsNullOrWhiteSpace(tokens[index]) ? string.Empty : tokens[index].Trim();
                if (token.Length > 0)
                {
                    parts.Add(token);
                }
            }

            if (parts.Count <= 0)
            {
                return HotkeyCaptureResult.None();
            }

            var primary = parts[parts.Count - 1];
            // Clear/cancel reserved keys are handled before parser so they never persist as normal chords.
            if (string.Equals(primary, BackspaceToken, StringComparison.Ordinal))
            {
                return parts.Count == 1
                    ? HotkeyCaptureResult.Cleared()
                    : HotkeyCaptureResult.Failed("reservedKey", primary);
            }

            if (string.Equals(primary, EscToken, StringComparison.Ordinal))
            {
                return parts.Count == 1
                    ? HotkeyCaptureResult.Cancelled()
                    : HotkeyCaptureResult.Failed("reservedKey", primary);
            }

            var parse = HotkeyParser.Parse(string.Join("+", parts.ToArray()));
            return parse.Succeeded
                ? HotkeyCaptureResult.Captured(parse)
                : HotkeyCaptureResult.Failed(parse.Reason, parse.Token);
        }

        private static bool TryCapturePrimaryEdge(
            HotkeyCaptureSession session,
            Func<int, bool> isKeyDown,
            out HotkeyToken primary)
        {
            primary = null;
            // Capture follows the catalog order and reacts only to fresh non-modifier edges.
            // This lets callers seed the starter mouse press from the UI button without saving it.
            for (var index = 0; index < CaptureTokens.Count; index++)
            {
                var token = CaptureTokens[index];
                var isDown = isKeyDown != null && isKeyDown(token.VirtualKey);
                bool wasDown;
                session.WasDown.TryGetValue(token.VirtualKey, out wasDown);
                session.WasDown[token.VirtualKey] = isDown;

                if (!isDown || wasDown || token.IsModifier)
                {
                    continue;
                }

                primary = token;
                return true;
            }

            return false;
        }

        private static List<string> BuildCurrentChordTokens(HotkeyToken primary, Func<int, bool> isKeyDown)
        {
            var tokens = new List<string>();
            // Save the actual left/right modifier tokens reported by the input layer. AltGr is not
            // synthesized into generic Ctrl/Alt, so diagnostics can show the platform source.
            for (var index = 0; index < ModifierTokens.Count; index++)
            {
                var modifier = ModifierTokens[index];
                if (isKeyDown != null && isKeyDown(modifier.VirtualKey))
                {
                    tokens.Add(modifier.Canonical);
                }
            }

            if (primary != null)
            {
                tokens.Add(primary.Canonical);
            }

            return tokens;
        }

        private static IReadOnlyList<HotkeyToken> BuildCaptureTokens()
        {
            var tokens = new List<HotkeyToken>();
            var all = HotkeyTokenCatalog.AllTokens;
            for (var index = 0; index < all.Count; index++)
            {
                var token = all[index];
                if (token != null && token.VirtualKey > 0)
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        private static IReadOnlyList<HotkeyToken> BuildModifierTokens()
        {
            var tokens = new List<HotkeyToken>();
            var all = HotkeyTokenCatalog.AllTokens;
            for (var index = 0; index < all.Count; index++)
            {
                var token = all[index];
                if (token != null && token.IsModifier)
                {
                    tokens.Add(token);
                }
            }

            tokens.Sort((left, right) => HotkeyTokenCatalog.GetModifierSortIndex(left).CompareTo(HotkeyTokenCatalog.GetModifierSortIndex(right)));
            return tokens;
        }
    }
}
