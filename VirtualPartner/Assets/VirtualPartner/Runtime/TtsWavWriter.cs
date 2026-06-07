using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    // PCM16 little-endian WAV writing utilities shared by the TTS pipeline.
    public static class TtsWavWriter
    {
        // Writes a complete PCM16 mono/stereo WAV (RIFF header + data) into the stream.
        public static void WritePcm16Wav(Stream stream, byte[] pcmBytes, int sampleRate, int channels)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var payload = pcmBytes ?? Array.Empty<byte>();
            var resolvedChannels = Mathf.Max(1, channels);
            var resolvedSampleRate = Mathf.Max(8000, sampleRate);
            var byteRate = resolvedSampleRate * resolvedChannels * 2;
            var blockAlign = resolvedChannels * 2;

            WriteAscii(stream, "RIFF");
            WriteInt32LE(stream, 36 + payload.Length);
            WriteAscii(stream, "WAVE");
            WriteAscii(stream, "fmt ");
            WriteInt32LE(stream, 16);
            WriteInt16LE(stream, 1);
            WriteInt16LE(stream, resolvedChannels);
            WriteInt32LE(stream, resolvedSampleRate);
            WriteInt32LE(stream, byteRate);
            WriteInt16LE(stream, blockAlign);
            WriteInt16LE(stream, 16);
            WriteAscii(stream, "data");
            WriteInt32LE(stream, payload.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private static void WriteAscii(Stream stream, string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt16LE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
        }

        private static void WriteInt32LE(Stream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xff));
            stream.WriteByte((byte)((value >> 8) & 0xff));
            stream.WriteByte((byte)((value >> 16) & 0xff));
            stream.WriteByte((byte)((value >> 24) & 0xff));
        }
    }
}
