using System;
using System.Text;
using JueMingZ.Config;

namespace JueMingZ.Automation.Information
{
    internal static class MapQuickAnnouncementTextSafety
    {
        public const int MaxBodyLength = 180;

        public static string BuildColoredAnnouncement(string rawBody, string colorHex)
        {
            var body = SanitizeBody(rawBody);
            if (body.Length <= 0)
            {
                body = "这里什么都没有";
            }

            var normalizedColor = MapQuickAnnouncementSettings.NormalizeColorHex(colorHex);
            var tagColor = normalizedColor.Length == 7 ? normalizedColor.Substring(1) : "FFD966";
            return "[c/" + tagColor + ":" + body + "]";
        }

        public static string SanitizeBody(string rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(rawBody.Length);
            var previousWasSpace = false;
            for (var index = 0; index < rawBody.Length; index++)
            {
                var ch = rawBody[index];
                if (ch == '[' || ch == ']')
                {
                    continue;
                }

                if (char.IsControl(ch) || char.IsWhiteSpace(ch))
                {
                    if (!previousWasSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                        previousWasSpace = true;
                    }

                    continue;
                }

                builder.Append(ch);
                previousWasSpace = false;
            }

            var text = builder.ToString().Trim();

            // The final message is wrapped in a controlled color tag, but the
            // raw body is still normalized away from command-looking text so a
            // future no-color path cannot accidentally become a slash command.
            while (text.Length > 0 && text[0] == '/')
            {
                text = text.Substring(1).TrimStart();
            }

            if (text.Length > MaxBodyLength)
            {
                text = text.Substring(0, MaxBodyLength).TrimEnd();
            }

            return text;
        }

        public static bool IsAirTarget(MapQuickAnnouncementResolveResult result)
        {
            return result != null && result.Kind == MapQuickAnnouncementTargetKind.Air;
        }
    }
}
