using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualPartner.Runtime
{
    // Thread-safe ring buffer that receives PCM16 little-endian bytes from the TTS
    // streaming download (network thread) and feeds float samples to a streaming
    // AudioClip (audio thread). All public members lock a single gate so producer and
    // consumer can run on different threads safely.
    public sealed class StreamingPcmBuffer
    {
        private readonly object gate = new object();
        private readonly MemoryStream rawStream = new MemoryStream();
        private float[] samples;
        private int readIndex;
        private int writeIndex;
        private int availableSamples;
        private int pendingByte = -1;
        private long receivedBytes;
        private long totalSamplesWritten;
        private long totalSamplesRead;
        private bool completed;

        public StreamingPcmBuffer(int sampleRate, int channels, int initialCapacitySamples)
        {
            SampleRate = Mathf.Max(8000, sampleRate);
            Channels = Mathf.Max(1, channels);
            samples = new float[Mathf.Max(1024, initialCapacitySamples)];
        }

        public int SampleRate { get; }
        public int Channels { get; }

        public long ReceivedBytes
        {
            get
            {
                lock (gate)
                    return receivedBytes;
            }
        }

        public long RawByteCount
        {
            get
            {
                lock (gate)
                    return rawStream.Length;
            }
        }

        public long TotalSamplesWritten
        {
            get
            {
                lock (gate)
                    return totalSamplesWritten;
            }
        }

        public float BufferedSeconds
        {
            get
            {
                lock (gate)
                    return availableSamples / (float)Mathf.Max(1, SampleRate);
            }
        }

        public float WrittenSeconds
        {
            get
            {
                lock (gate)
                    return totalSamplesWritten / (float)Mathf.Max(1, SampleRate);
            }
        }

        public bool Completed
        {
            get
            {
                lock (gate)
                    return completed;
            }
        }

        public bool IsDrained
        {
            get
            {
                lock (gate)
                    return completed && availableSamples <= 0 && totalSamplesRead >= totalSamplesWritten;
            }
        }

        public void AppendPcm16(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
                return;

            lock (gate)
            {
                var safeLength = Mathf.Min(dataLength, data.Length);
                rawStream.Write(data, 0, safeLength);
                receivedBytes += safeLength;
                EnsureCapacity((safeLength + 1) / 2 + 1);

                var offset = 0;
                if (pendingByte >= 0 && offset < safeLength)
                {
                    EnqueueSample(ToFloatSample(pendingByte, data[offset]));
                    pendingByte = -1;
                    offset++;
                }

                while (offset + 1 < safeLength)
                {
                    EnqueueSample(ToFloatSample(data[offset], data[offset + 1]));
                    offset += 2;
                }

                if (offset < safeLength)
                    pendingByte = data[offset];
            }
        }

        public void Read(float[] data)
        {
            if (data == null)
                return;

            lock (gate)
            {
                for (var i = 0; i < data.Length; i++)
                {
                    if (availableSamples > 0)
                    {
                        data[i] = samples[readIndex];
                        readIndex = (readIndex + 1) % samples.Length;
                        availableSamples--;
                        totalSamplesRead++;
                    }
                    else
                    {
                        data[i] = 0f;
                    }
                }
            }
        }

        public void MarkCompleted()
        {
            lock (gate)
                completed = true;
        }

        public byte[] GetRawBytes()
        {
            lock (gate)
                return rawStream.ToArray();
        }

        private void EnsureCapacity(int samplesToAdd)
        {
            var free = samples.Length - availableSamples;
            if (free >= samplesToAdd)
                return;

            var newLength = samples.Length;
            while (newLength - availableSamples < samplesToAdd)
                newLength *= 2;

            var expanded = new float[newLength];
            for (var i = 0; i < availableSamples; i++)
                expanded[i] = samples[(readIndex + i) % samples.Length];

            samples = expanded;
            readIndex = 0;
            writeIndex = availableSamples;
        }

        private void EnqueueSample(float value)
        {
            samples[writeIndex] = value;
            writeIndex = (writeIndex + 1) % samples.Length;
            availableSamples++;
            totalSamplesWritten++;
        }

        private static float ToFloatSample(int lowByte, int highByte)
        {
            var raw = (short)((lowByte & 0xff) | ((highByte & 0xff) << 8));
            return Mathf.Clamp(raw / 32768f, -1f, 1f);
        }
    }

    // DownloadHandlerScript with a pre-allocated buffer so streamed TTS chunks are
    // forwarded into the StreamingPcmBuffer without per-chunk allocations.
    public sealed class StreamingPcmDownloadHandler : DownloadHandlerScript
    {
        public StreamingPcmDownloadHandler(StreamingPcmBuffer buffer)
            : base(new byte[4096])
        {
            Buffer = buffer;
        }

        public StreamingPcmBuffer Buffer { get; }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            Buffer?.AppendPcm16(data, dataLength);
            return true;
        }

        protected override void CompleteContent()
        {
            Buffer?.MarkCompleted();
        }
    }
}
