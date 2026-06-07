using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Thread-safe streaming download handler used by LlmRelay to receive SSE bytes
    /// off the network thread and hand decoded text chunks to the main thread.
    /// Extracted verbatim from LlmRelay (no behavior change).
    /// </summary>
    internal sealed class StreamingTextDownloadHandler : DownloadHandlerScript
    {
        private readonly object gate = new object();
        private readonly Queue<string> chunks = new Queue<string>();
        private readonly StringBuilder rawText = new StringBuilder(8192);
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        private readonly char[] charBuffer = new char[8192];

        public StreamingTextDownloadHandler()
            : base(new byte[4096])
        {
        }

        public string RawText
        {
            get
            {
                lock (gate)
                    return rawText.ToString();
            }
        }

        public bool TryDequeueChunk(out string chunk)
        {
            lock (gate)
            {
                if (chunks.Count == 0)
                {
                    chunk = string.Empty;
                    return false;
                }

                chunk = chunks.Dequeue();
                return true;
            }
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
                return true;

            lock (gate)
            {
                var charCount = decoder.GetChars(data, 0, dataLength, charBuffer, 0, false);
                if (charCount <= 0)
                    return true;

                var chunk = new string(charBuffer, 0, charCount);
                chunks.Enqueue(chunk);
                rawText.Append(chunk);
                return true;
            }
        }

        protected override void CompleteContent()
        {
            lock (gate)
            {
                var charCount = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, true);
                if (charCount <= 0)
                    return;

                var chunk = new string(charBuffer, 0, charCount);
                chunks.Enqueue(chunk);
                rawText.Append(chunk);
            }
        }
    }
}
