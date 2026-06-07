using System;
using System.Collections.Generic;
using System.Text;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Incremental Server-Sent Events parser: accumulates streamed text, splits it
    /// into lines, and emits "data:" payloads per SSE event. Extracted verbatim from
    /// LlmRelay (no behavior change).
    /// </summary>
    internal sealed class StreamingSseParser
    {
        private readonly StringBuilder lineBuffer = new StringBuilder(1024);
        private readonly StringBuilder eventData = new StringBuilder(1024);

        public bool Append(string chunk, out List<string> payloads, out string failureReason)
        {
            payloads = new List<string>();
            failureReason = string.Empty;
            if (string.IsNullOrEmpty(chunk))
                return true;

            lineBuffer.Append(chunk);
            while (TryPopLine(out var line))
                ProcessLine(line, payloads);

            return true;
        }

        public bool Complete(out List<string> payloads, out string failureReason)
        {
            payloads = new List<string>();
            failureReason = string.Empty;

            if (lineBuffer.Length > 0)
            {
                var line = lineBuffer.ToString();
                lineBuffer.Length = 0;
                ProcessLine(TrimLineEnding(line), payloads);
            }

            FlushEvent(payloads);
            return true;
        }

        private bool TryPopLine(out string line)
        {
            for (var i = 0; i < lineBuffer.Length; i++)
            {
                if (lineBuffer[i] != '\n')
                    continue;

                line = TrimLineEnding(lineBuffer.ToString(0, i + 1));
                lineBuffer.Remove(0, i + 1);
                return true;
            }

            line = string.Empty;
            return false;
        }

        private void ProcessLine(string line, List<string> payloads)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushEvent(payloads);
                return;
            }

            if (line.StartsWith(":", StringComparison.Ordinal))
                return;

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                return;

            var data = line.Substring(5);
            if (data.StartsWith(" ", StringComparison.Ordinal))
                data = data.Substring(1);

            if (eventData.Length > 0)
                eventData.Append('\n');
            eventData.Append(data);
        }

        private void FlushEvent(List<string> payloads)
        {
            if (eventData.Length == 0)
                return;

            payloads.Add(eventData.ToString());
            eventData.Length = 0;
        }

        private static string TrimLineEnding(string line)
        {
            return line.TrimEnd('\r', '\n');
        }
    }
}
