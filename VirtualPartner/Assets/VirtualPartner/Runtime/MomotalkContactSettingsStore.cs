using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class MomotalkContactSettingsEntry
    {
        public string characterId;
        public bool stickyOnTop;
    }

    [Serializable]
    public sealed class MomotalkContactSettingsFile
    {
        public int version = 1;
        public List<MomotalkContactSettingsEntry> contacts = new List<MomotalkContactSettingsEntry>();
    }

    public sealed class MomotalkContactSettingsStore
    {
        private const int Version = 1;
        private const string RelativePath = "UserData/Momotalk/contact_settings.json";

        private MomotalkContactSettingsFile cache;
        private bool loaded;

        public string LastResolvedPath { get; private set; }

        public bool IsSticky(string characterId)
        {
            EnsureLoaded();
            var entry = FindEntry(characterId);
            return entry != null && entry.stickyOnTop;
        }

        public void SetSticky(string characterId, bool sticky)
        {
            var normalizedId = NormalizeCharacterId(characterId);
            if (string.IsNullOrWhiteSpace(normalizedId))
                return;

            EnsureLoaded();
            var entry = FindEntry(normalizedId);
            if (entry == null)
            {
                entry = new MomotalkContactSettingsEntry { characterId = normalizedId };
                cache.contacts.Add(entry);
            }

            entry.stickyOnTop = sticky;
            Save();
        }

        private void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            var path = GetPath();
            if (!File.Exists(path))
            {
                cache = new MomotalkContactSettingsFile();
                return;
            }

            try
            {
                cache = JsonUtility.FromJson<MomotalkContactSettingsFile>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VirtualPartner] Momotalk contact settings load failed: {exception.Message}");
                cache = new MomotalkContactSettingsFile();
            }

            if (cache == null)
                cache = new MomotalkContactSettingsFile();
            if (cache.contacts == null)
                cache.contacts = new List<MomotalkContactSettingsEntry>();
        }

        private void Save()
        {
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            cache.version = Version;
            File.WriteAllText(path, JsonUtility.ToJson(cache, true), Encoding.UTF8);
        }

        private MomotalkContactSettingsEntry FindEntry(string characterId)
        {
            var normalizedId = NormalizeCharacterId(characterId);
            if (cache == null || cache.contacts == null || string.IsNullOrWhiteSpace(normalizedId))
                return null;

            for (var i = 0; i < cache.contacts.Count; i++)
            {
                var entry = cache.contacts[i];
                if (entry != null && string.Equals(entry.characterId, normalizedId, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }

        private string GetPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            LastResolvedPath = Path.Combine(projectRoot, RelativePath);
            return LastResolvedPath;
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? string.Empty : characterId.Trim().ToLowerInvariant();
        }
    }
}
