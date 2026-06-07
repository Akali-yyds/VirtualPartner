using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    // Resolved cache identity (display key + on-disk path) for one TTS request.
    public readonly struct TtsCacheInfo
    {
        public TtsCacheInfo(string key, string path)
        {
            Key = key ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public string Key { get; }
        public string Path { get; }
    }

    // Owns TTS audio cache identity derivation and atomic cache file writes.
    public static class TtsCache
    {
        // Builds the cache key and file path from the resolved request signature.
        public static TtsCacheInfo BuildInfo(
            string characterId,
            string provider,
            string voiceId,
            string emotion,
            float speed,
            string text,
            string engineVersion,
            string refHash,
            string promptHash,
            string promptLang,
            string textLang,
            string fallbackVersion)
        {
            var speedText = speed.ToString("0.###", CultureInfo.InvariantCulture);
            var versionText = string.IsNullOrWhiteSpace(engineVersion)
                ? (fallbackVersion ?? string.Empty)
                : engineVersion.Trim();
            var rawKey = string.Join(
                "|",
                characterId,
                provider,
                voiceId,
                text ?? string.Empty,
                emotion,
                speedText,
                refHash ?? string.Empty,
                promptHash ?? string.Empty,
                promptLang ?? string.Empty,
                textLang ?? string.Empty,
                versionText);
            var hash = Hash(rawKey);

            var key = string.Join(
                "/",
                characterId,
                provider,
                voiceId,
                emotion,
                speedText,
                versionText,
                hash);
            var path = Path.Combine(
                GetProjectUserDataRoot(),
                "TTSCache",
                SanitizePathSegment(characterId),
                $"{hash}.wav");
            return new TtsCacheInfo(key, path);
        }

        // Atomically writes raw audio bytes (already a complete container, e.g. wav) to the cache path.
        public static bool TryWriteBytes(byte[] audioBytes, string targetPath, out string failureReason)
        {
            failureReason = string.Empty;
            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = targetPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                File.WriteAllBytes(tempPath, audioBytes);

                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);

                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"TTS cache write failed: {exception.Message}";
                return false;
            }
        }

        // Atomically wraps raw PCM16 bytes in a wav container and writes to the cache path.
        public static bool TryWritePcm16Wav(byte[] pcmBytes, int sampleRate, int channels, string targetPath, out string failureReason)
        {
            failureReason = string.Empty;
            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                failureReason = "TTS stream cache write failed: audio is empty.";
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = targetPath + ".tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                using (var stream = File.Create(tempPath))
                {
                    TtsWavWriter.WritePcm16Wav(stream, pcmBytes, sampleRate, channels);
                }

                if (File.Exists(targetPath))
                    File.Replace(tempPath, targetPath, null);
                else
                    File.Move(tempPath, targetPath);

                return true;
            }
            catch (Exception exception)
            {
                failureReason = $"TTS stream cache write failed: {exception.Message}";
                return false;
            }
        }

        private static string GetProjectUserDataRoot()
        {
            var projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return Path.Combine(Application.persistentDataPath, "VirtualPartner", "UserData");

            return Path.Combine(projectRoot.FullName, "UserData");
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(16);
                for (var i = 0; i < 8 && i < bytes.Length; i++)
                    builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));

                return builder.ToString();
            }
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "unknown";

            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            return builder.ToString();
        }
    }
}
