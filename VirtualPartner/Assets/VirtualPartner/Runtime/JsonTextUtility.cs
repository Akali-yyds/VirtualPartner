using System;
using System.Text;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Shared helpers for the project's hand-built JSON paths: string escaping,
    /// code-fence stripping, and balanced-brace object extraction. Centralized so
    /// LlmRelay, MemoryJudgeClient, TtsManager, the streaming stage parser,
    /// AsrManager, and AutonomousBehaviorScheduler all behave identically.
    /// </summary>
    public static class JsonTextUtility
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 16);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns the first balanced JSON object found in <paramref name="content"/>,
        /// honoring string literals and escape sequences.
        /// </summary>
        public static bool TryExtractFirstJsonObject(string content, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var start = content.IndexOf('{');
            if (start < 0)
                return false;

            return TryExtractJsonObjectAt(content, start, out json, out _);
        }

        /// <summary>
        /// Extracts the balanced JSON object that starts at <paramref name="start"/>.
        /// <paramref name="endIndex"/> receives the index of the closing brace.
        /// </summary>
        public static bool TryExtractJsonObjectAt(string text, int start, out string json, out int endIndex)
        {
            json = string.Empty;
            endIndex = -1;

            if (string.IsNullOrEmpty(text) || start < 0 || start >= text.Length || text[start] != '{')
                return false;

            var depth = 0;
            var inString = false;
            var escaping = false;
            for (var i = start; i < text.Length; i++)
            {
                var c = text[i];
                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaping = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth != 0)
                    continue;

                endIndex = i;
                json = text.Substring(start, i - start + 1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Strips a leading/trailing Markdown code fence (```), returning the inner
        /// content. Returns the trimmed input unchanged when no fence is present.
        /// </summary>
        public static string StripCodeFence(string content)
        {
            var trimmed = content == null ? string.Empty : content.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
                return trimmed;

            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline < 0 || lastFence <= firstNewline)
                return trimmed;

            return trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
        }
    }
}
