using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class MomotalkChatMessageRecord
    {
        public string messageId;
        public string sender;
        public string text;
        public string timestampUtc;
        public int requestId;
        public int stageIndex = -1;
        public int actionIndex = -1;
        public string status;
    }

    [Serializable]
    public sealed class MomotalkChatHistoryFile
    {
        public int version = 1;
        public string characterId;
        public List<MomotalkChatMessageRecord> messages = new List<MomotalkChatMessageRecord>();
    }

    public sealed class MomotalkHistoryStore
    {
        private const int Version = 1;
        private const int MaxStoredMessages = 200;
        private const int DefaultLoadCount = 50;
        private const string RelativeFolder = "UserData/ChatHistory";

        public string LastResolvedPath { get; private set; }

        public List<MomotalkChatMessageRecord> LoadRecent(string characterId)
        {
            var all = LoadAll(characterId);
            var start = Mathf.Max(0, all.Count - DefaultLoadCount);
            return all.GetRange(start, all.Count - start);
        }

        public List<MomotalkChatMessageRecord> LoadAll(string characterId)
        {
            var path = GetPath(characterId);
            if (!File.Exists(path))
                return new List<MomotalkChatMessageRecord>();

            try
            {
                var file = JsonUtility.FromJson<MomotalkChatHistoryFile>(File.ReadAllText(path, Encoding.UTF8));
                if (file == null || file.messages == null)
                    return new List<MomotalkChatMessageRecord>();

                return file.messages;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VirtualPartner] Momotalk history load failed: {exception.Message}");
                return new List<MomotalkChatMessageRecord>();
            }
        }

        public void Append(string characterId, MomotalkChatMessageRecord record)
        {
            if (record == null)
                return;

            var messages = LoadAll(characterId);
            messages.Add(record);
            Save(characterId, messages);
        }

        public void Clear(string characterId)
        {
            var path = GetPath(characterId);
            if (File.Exists(path))
                File.Delete(path);
        }

        public void Save(string characterId, List<MomotalkChatMessageRecord> messages)
        {
            if (messages == null)
                messages = new List<MomotalkChatMessageRecord>();

            while (messages.Count > MaxStoredMessages)
                messages.RemoveAt(0);

            var path = GetPath(characterId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var file = new MomotalkChatHistoryFile
            {
                version = Version,
                characterId = NormalizeCharacterId(characterId),
                messages = messages
            };

            File.WriteAllText(path, JsonUtility.ToJson(file, true), Encoding.UTF8);
        }

        public string GetLastSummary(string characterId, string fallback)
        {
            var messages = LoadAll(characterId);
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                var text = messages[i] != null ? messages[i].text : string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                return text.Length > 34 ? text.Substring(0, 34) + "..." : text;
            }

            return fallback;
        }

        public string BuildPromptContext(string characterId, int messageCount)
        {
            if (messageCount <= 0)
                return string.Empty;

            var messages = LoadAll(characterId);
            var filtered = new List<MomotalkChatMessageRecord>();
            for (var i = 0; i < messages.Count; i++)
            {
                var message = messages[i];
                if (message == null || string.IsNullOrWhiteSpace(message.text))
                    continue;
                if (message.sender != "user" && message.sender != "character")
                    continue;

                filtered.Add(message);
            }

            var start = Mathf.Max(0, filtered.Count - messageCount);
            var builder = new StringBuilder(512);
            for (var i = start; i < filtered.Count; i++)
            {
                var message = filtered[i];
                builder.Append(message.sender == "user" ? "User: " : "Character: ")
                    .AppendLine(message.text);
            }

            return builder.ToString().Trim();
        }

        public string GetPath(string characterId)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var path = Path.Combine(projectRoot, RelativeFolder, NormalizeCharacterId(characterId) + ".json");
            LastResolvedPath = path;
            return path;
        }

        public static MomotalkChatMessageRecord CreateMessage(
            string sender,
            string text,
            string status,
            int requestId,
            int stageIndex,
            int actionIndex)
        {
            return new MomotalkChatMessageRecord
            {
                messageId = "msg_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                sender = sender,
                text = text ?? string.Empty,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                requestId = requestId,
                stageIndex = stageIndex,
                actionIndex = actionIndex,
                status = status ?? string.Empty
            };
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? "unknown" : characterId.Trim().ToLowerInvariant();
        }
    }
}
