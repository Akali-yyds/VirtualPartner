using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Parses a StagePlan JSON document incrementally as streamed deltas arrive,
    /// emitting each complete stage object inside the "stages" array as soon as it
    /// closes. Extracted verbatim from LlmRelay (no behavior change).
    /// </summary>
    internal sealed class StagePlanIncrementalParser
    {
        private readonly StringBuilder content = new StringBuilder(8192);
        private readonly Queue<string> stages = new Queue<string>();
        private bool stagesArrayFound;
        private bool stagesArrayClosed;
        private int stageScanIndex;

        public string Content => content.ToString();

        public void Append(string delta)
        {
            if (string.IsNullOrEmpty(delta))
                return;

            content.Append(delta);
            ScanForStages();
        }

        public bool TryDequeueStage(out string stageJson)
        {
            if (stages.Count == 0)
            {
                stageJson = string.Empty;
                return false;
            }

            stageJson = stages.Dequeue();
            return true;
        }

        private void ScanForStages()
        {
            if (stagesArrayClosed)
                return;

            var text = content.ToString();
            if (!stagesArrayFound)
            {
                if (!TryFindStagesArrayStart(text, out stageScanIndex))
                    return;

                stagesArrayFound = true;
            }

            while (stageScanIndex < text.Length)
            {
                stageScanIndex = SkipWhitespaceAndCommas(text, stageScanIndex);
                if (stageScanIndex >= text.Length)
                    return;

                if (text[stageScanIndex] == ']')
                {
                    stagesArrayClosed = true;
                    return;
                }

                if (text[stageScanIndex] != '{')
                    return;

                if (!JsonTextUtility.TryExtractJsonObjectAt(text, stageScanIndex, out var stageJson, out var objectEndIndex))
                    return;

                stages.Enqueue(stageJson);
                stageScanIndex = objectEndIndex + 1;
            }
        }

        private static bool TryFindStagesArrayStart(string text, out int arrayContentStart)
        {
            arrayContentStart = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '"')
                    continue;

                if (!TryReadJsonStringToken(text, i, out var token, out var nextIndex, out var complete))
                    return false;

                if (!complete)
                    return false;

                i = nextIndex - 1;
                if (!string.Equals(token, "stages", StringComparison.Ordinal))
                    continue;

                var colonIndex = SkipWhitespace(text, nextIndex);
                if (colonIndex >= text.Length)
                    return false;
                if (text[colonIndex] != ':')
                    continue;

                var arrayIndex = SkipWhitespace(text, colonIndex + 1);
                if (arrayIndex >= text.Length)
                    return false;
                if (text[arrayIndex] != '[')
                    continue;

                arrayContentStart = arrayIndex + 1;
                return true;
            }

            return false;
        }

        private static bool TryReadJsonStringToken(
            string text,
            int quoteIndex,
            out string token,
            out int nextIndex,
            out bool complete)
        {
            token = string.Empty;
            nextIndex = quoteIndex;
            complete = false;

            if (quoteIndex < 0 || quoteIndex >= text.Length || text[quoteIndex] != '"')
                return false;

            var builder = new StringBuilder();
            var escaping = false;
            for (var i = quoteIndex + 1; i < text.Length; i++)
            {
                var c = text[i];
                if (escaping)
                {
                    builder.Append(c);
                    escaping = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (c == '"')
                {
                    token = builder.ToString();
                    nextIndex = i + 1;
                    complete = true;
                    return true;
                }

                builder.Append(c);
            }

            return true;
        }

        private static int SkipWhitespaceAndCommas(string text, int index)
        {
            while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
                index++;

            return index;
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            return index;
        }
    }
}
