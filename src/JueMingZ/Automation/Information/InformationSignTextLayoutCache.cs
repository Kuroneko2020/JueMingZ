using System;
using System.Collections.Generic;
using JueMingZ.Config;
using JueMingZ.UI;

namespace JueMingZ.Automation.Information
{
    internal static class InformationSignTextLayoutCache
    {
        private const int CacheLimit = 512;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<SignTextLayoutKey, SignTextLayout> Cache = new Dictionary<SignTextLayoutKey, SignTextLayout>();
        private static int _rebuildCount;
        private static string _fontSignature = string.Empty;
        private static int _cacheGeneration;
        private static long _hitCount;
        private static long _missCount;

        internal static long HitCount
        {
            get { lock (SyncRoot) { return _hitCount; } }
        }

        internal static long MissCount
        {
            get { lock (SyncRoot) { return _missCount; } }
        }

        internal static SignTextLayout GetOrBuild(string text, int textHash, string mode, int maxLines, int maxCharacters, float scale)
        {
            // Sign and tombstone layouts are cached by text, mode, scale, and
            // font generation; drawing must not rebuild layout every frame.
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var normalizedMode = NormalizeMode(mode);
            if (string.Equals(normalizedMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fontSignature = UiTextRenderer.FontSignatureForLayoutCache;
            var cacheGeneration = UiTextRenderer.CacheGenerationForLayoutCache;
            var key = new SignTextLayoutKey(
                text,
                textHash,
                normalizedMode,
                InformationSignTextModes.ClampLines(maxLines),
                InformationSignTextModes.ClampCharacters(maxCharacters),
                ScaleKey(scale),
                fontSignature,
                cacheGeneration);

            SignTextLayout cached;
            lock (SyncRoot)
            {
                ClearIfFontChangedLocked(fontSignature, cacheGeneration);
                if (Cache.TryGetValue(key, out cached))
                {
                    unchecked
                    {
                        _hitCount++;
                    }

                    return cached;
                }
            }

            var layout = BuildLayout(text, normalizedMode, maxLines, maxCharacters, scale);
            if (layout == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                ClearIfFontChangedLocked(fontSignature, cacheGeneration);
                if (Cache.TryGetValue(key, out cached))
                {
                    unchecked
                    {
                        _hitCount++;
                    }

                    return cached;
                }

                if (Cache.Count >= CacheLimit)
                {
                    Cache.Clear();
                }

                Cache[key] = layout;
                unchecked
                {
                    _rebuildCount++;
                    _missCount++;
                }
            }

            return layout;
        }

        internal static string[] BuildDisplayLinesForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            IList<string> lines;
            return TryBuildDisplayLines(text, NormalizeMode(mode), maxLines, maxCharacters, scale, out lines)
                ? ToArray(lines)
                : new string[0];
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                Cache.Clear();
                _rebuildCount = 0;
                _hitCount = 0;
                _missCount = 0;
                _fontSignature = UiTextRenderer.FontSignatureForLayoutCache;
                _cacheGeneration = UiTextRenderer.CacheGenerationForLayoutCache;
            }
        }

        internal static InformationSignTextLayoutSnapshot BuildSnapshotForTesting(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            var layout = GetOrBuild(text, HashText(text), mode, maxLines, maxCharacters, scale);
            int rebuildCount;
            lock (SyncRoot)
            {
                rebuildCount = _rebuildCount;
            }

            if (layout == null)
            {
                return new InformationSignTextLayoutSnapshot(0, string.Empty, 0, 0, 0, rebuildCount);
            }

            return new InformationSignTextLayoutSnapshot(
                layout.DisplayLines.Length,
                layout.DisplayLines.Length <= 0 ? string.Empty : layout.DisplayLines[0],
                layout.LineWidths.Length <= 0 ? 0 : layout.LineWidths[0],
                layout.LineHeight,
                layout.TotalHeight,
                rebuildCount);
        }

        internal static float CalculateLineX(float signCenterX, int lineWidth, int screenWidth)
        {
            var width = Math.Max(0, lineWidth);
            var drawX = signCenterX - width / 2f;
            var maxX = Math.Max(4f, screenWidth - width - 4f);
            if (drawX < 4f)
            {
                return 4f;
            }

            return drawX > maxX ? maxX : drawX;
        }

        internal static int HashText(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                if (text == null)
                {
                    return hash;
                }

                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                return hash;
            }
        }

        private static void ClearIfFontChangedLocked(string fontSignature, int cacheGeneration)
        {
            if (_cacheGeneration == cacheGeneration &&
                string.Equals(_fontSignature, fontSignature ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }

            Cache.Clear();
            _fontSignature = fontSignature ?? string.Empty;
            _cacheGeneration = cacheGeneration;
        }

        private static SignTextLayout BuildLayout(string text, string mode, int maxLines, int maxCharacters, float scale)
        {
            IList<string> lines;
            if (!TryBuildDisplayLines(text, mode, maxLines, maxCharacters, scale, out lines))
            {
                return null;
            }

            var displayLines = ToArray(lines);
            var lineWidths = new int[displayLines.Length];
            var hasVisibleText = false;
            for (var index = 0; index < displayLines.Length; index++)
            {
                var width = UiTextRenderer.EstimateTextWidth(displayLines[index], scale);
                lineWidths[index] = width;
                hasVisibleText |= width > 0;
            }

            var lineHeight = Math.Max(16, UiTextRenderer.EstimateTextHeight(scale) + 5);
            return new SignTextLayout(displayLines, lineWidths, lineHeight, lineHeight * displayLines.Length, scale, hasVisibleText);
        }

        private static bool TryBuildDisplayLines(string text, string mode, int maxLines, int maxCharacters, float scale, out IList<string> lines)
        {
            lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalizedMode = NormalizeMode(mode);
            if (string.Equals(normalizedMode, InformationSignTextModes.Off, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var displayText = NormalizeLineBreaks(text);
            var characterLimited = false;
            if (string.Equals(normalizedMode, InformationSignTextModes.Characters, StringComparison.OrdinalIgnoreCase))
            {
                var limit = InformationSignTextModes.ClampCharacters(maxCharacters);
                if (displayText.Length > limit)
                {
                    displayText = displayText.Substring(0, limit).TrimEnd();
                    characterLimited = true;
                }
            }

            var lineLimit = InformationSignTextModes.VanillaDisplayMaxLines;
            if (string.Equals(normalizedMode, InformationSignTextModes.Lines, StringComparison.OrdinalIgnoreCase))
            {
                lineLimit = InformationSignTextModes.ClampLines(maxLines);
            }

            var truncatedByLines = WrapText(displayText, lineLimit, scale, lines);
            if (lines.Count <= 0)
            {
                return false;
            }

            if (characterLimited || truncatedByLines)
            {
                lines[lines.Count - 1] = AppendEllipsisToFit(lines[lines.Count - 1], scale);
            }

            return true;
        }

        private static bool WrapText(string text, int maxLines, float scale, IList<string> lines)
        {
            // Wrap to the vanilla display width budget so structural splits do
            // not change visible sign or tombstone text.
            var source = text ?? string.Empty;
            var paragraphs = source.Split('\n');
            var truncated = false;
            var width = Math.Max(80, (int)Math.Round(InformationSignTextModes.VanillaDisplayWidthPixels * Math.Max(0.1f, scale)));
            for (var paragraphIndex = 0; paragraphIndex < paragraphs.Length; paragraphIndex++)
            {
                if (lines.Count >= maxLines)
                {
                    truncated = HasRemainingParagraphText(paragraphs, paragraphIndex);
                    break;
                }

                var paragraph = paragraphs[paragraphIndex] ?? string.Empty;
                if (paragraph.Length <= 0)
                {
                    lines.Add(string.Empty);
                    continue;
                }

                var offset = 0;
                while (offset < paragraph.Length)
                {
                    if (lines.Count >= maxLines)
                    {
                        truncated = true;
                        break;
                    }

                    var take = FindWrappedTakeCount(paragraph, offset, width, scale);
                    var line = paragraph.Substring(offset, take).TrimEnd();
                    if (line.Length > 0 || paragraph.Length == 0)
                    {
                        lines.Add(line);
                    }

                    offset += take;
                    while (offset < paragraph.Length && char.IsWhiteSpace(paragraph[offset]))
                    {
                        offset++;
                    }
                }
            }

            return truncated;
        }

        private static int FindWrappedTakeCount(string text, int offset, int maxWidth, float scale)
        {
            var best = 1;
            var lastBreak = -1;
            for (var index = offset; index < text.Length; index++)
            {
                var current = text[index];
                if (char.IsWhiteSpace(current))
                {
                    lastBreak = index;
                }

                var length = index - offset + 1;
                if (UiTextRenderer.EstimateTextWidth(text.Substring(offset, length), scale) <= maxWidth)
                {
                    best = length;
                    continue;
                }

                if (lastBreak >= offset)
                {
                    return Math.Max(1, lastBreak - offset);
                }

                return best;
            }

            return Math.Max(1, text.Length - offset);
        }

        private static bool HasRemainingParagraphText(string[] paragraphs, int startIndex)
        {
            if (paragraphs == null)
            {
                return false;
            }

            for (var index = startIndex; index < paragraphs.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(paragraphs[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static string AppendEllipsisToFit(string value, float scale)
        {
            var text = value ?? string.Empty;
            var maxWidth = Math.Max(80, (int)Math.Round(InformationSignTextModes.VanillaDisplayWidthPixels * Math.Max(0.1f, scale)));
            const string suffix = "...";
            while (text.Length > 0 && UiTextRenderer.EstimateTextWidth(text + suffix, scale) > maxWidth)
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text.Length <= 0 ? suffix : text + suffix;
        }

        private static string NormalizeLineBreaks(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static string NormalizeMode(string mode)
        {
            return InformationSignTextModes.Normalize(mode);
        }

        private static string[] ToArray(IList<string> values)
        {
            if (values == null || values.Count <= 0)
            {
                return new string[0];
            }

            var result = new string[values.Count];
            for (var index = 0; index < values.Count; index++)
            {
                result[index] = values[index] ?? string.Empty;
            }

            return result;
        }

        private static int ScaleKey(float scale)
        {
            return (int)Math.Round(scale * 10000f);
        }
    }
}
