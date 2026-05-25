using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public sealed class MemoryWriteResult
    {
        public bool Wrote { get; set; }
        public bool SkippedDuplicate { get; set; }
        public string Path { get; set; }
        public string Message { get; set; }
    }

    public sealed class MemoryPromptContextResult
    {
        public string Text { get; set; }
        public int CharacterCount { get; set; }
        public bool Truncated { get; set; }
        public string RootPath { get; set; }
    }

    public sealed class MarkdownMemoryStore
    {
        private const string RelativeFolder = "UserData/Memory";

        private static readonly string[] Categories =
        {
            "user_profile",
            "preferences",
            "project_notes",
            "relationship_notes",
            "important_events",
            "conversation_summaries"
        };

        private static readonly string[] Importances =
        {
            "low",
            "medium",
            "high",
            "core"
        };

        public string GetRootPath(string characterId)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, RelativeFolder, NormalizeCharacterId(characterId));
        }

        public string GetCategoryPath(string characterId, string category)
        {
            return Path.Combine(GetRootPath(characterId), NormalizeCategory(category) + ".md");
        }

        public bool ValidateDecision(MemoryJudgeDecision decision, out string failureReason)
        {
            if (decision == null)
                return Fail("MemoryJudge decision is missing.", out failureReason);

            if (!decision.shouldRemember)
            {
                failureReason = string.Empty;
                return true;
            }

            if (!IsAllowedCategory(decision.category))
                return Fail($"Memory category '{decision.category}' is invalid.", out failureReason);
            if (!IsAllowedImportance(decision.importance))
                return Fail($"Memory importance '{decision.importance}' is invalid.", out failureReason);
            if (string.IsNullOrWhiteSpace(decision.title))
                return Fail("Memory title is empty.", out failureReason);
            if (string.IsNullOrWhiteSpace(decision.memoryText))
                return Fail("Memory text is empty.", out failureReason);

            failureReason = string.Empty;
            return true;
        }

        public MemoryWriteResult Append(string characterId, int requestId, MemoryJudgeDecision decision)
        {
            var result = new MemoryWriteResult();
            if (!ValidateDecision(decision, out var failureReason))
            {
                result.Message = failureReason;
                return result;
            }

            if (!decision.shouldRemember)
            {
                result.Message = "MemoryJudge decided not to remember this turn.";
                return result;
            }

            var rootPath = GetRootPath(characterId);
            Directory.CreateDirectory(rootPath);
            EnsureCategoryFiles(characterId);

            var path = GetCategoryPath(characterId, decision.category);
            result.Path = path;

            var memoryText = NormalizeText(decision.memoryText);
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path, Encoding.UTF8);
                if (existing.Contains(memoryText))
                {
                    result.SkippedDuplicate = true;
                    result.Message = "Duplicate memoryText skipped.";
                    return result;
                }
            }

            var timestamp = DateTime.UtcNow.ToString("o");
            var builder = new StringBuilder(512);
            builder.AppendLine();
            builder.Append("## ")
                .Append(timestamp)
                .Append(" | ")
                .Append(SanitizeLine(decision.title))
                .Append(" | ")
                .Append(NormalizeImportance(decision.importance))
                .AppendLine();
            builder.AppendLine();
            builder.Append("timestamp: ").AppendLine(timestamp);
            builder.Append("importance: ").AppendLine(NormalizeImportance(decision.importance));
            builder.Append("title: ").AppendLine(SanitizeLine(decision.title));
            builder.AppendLine("memoryText:");
            builder.AppendLine(memoryText);
            builder.Append("source requestId: ").AppendLine(requestId.ToString());
            builder.AppendLine("reason:");
            builder.AppendLine(NormalizeText(decision.reason));

            File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
            result.Wrote = true;
            result.Message = "Memory written.";
            return result;
        }

        public MemoryPromptContextResult BuildPromptContext(string characterId, int maxChars)
        {
            EnsureCategoryFiles(characterId);

            var result = new MemoryPromptContextResult
            {
                RootPath = GetRootPath(characterId)
            };
            var limit = Mathf.Max(0, maxChars);
            if (limit <= 0)
                return result;

            var core = LoadEntriesByImportance(characterId, "core");
            var high = LoadEntriesByImportance(characterId, "high");
            if (core.Count == 0 && high.Count == 0)
                return result;

            var builder = new StringBuilder(Mathf.Min(limit, 4096));
            AppendPromptIntro(builder);
            AppendEntries(builder, core, limit, ref result);
            if (!result.Truncated)
                AppendEntries(builder, high, limit, ref result);

            result.Text = builder.ToString().Trim();
            result.CharacterCount = result.Text.Length;
            return result;
        }

        public void EnsureCategoryFiles(string characterId)
        {
            var rootPath = GetRootPath(characterId);
            Directory.CreateDirectory(rootPath);

            for (var i = 0; i < Categories.Length; i++)
            {
                var path = GetCategoryPath(characterId, Categories[i]);
                if (File.Exists(path))
                    continue;

                var title = "# " + Categories[i] + Environment.NewLine;
                File.WriteAllText(path, title, Encoding.UTF8);
            }
        }

        public int Clear(string characterId)
        {
            var rootPath = GetRootPath(characterId);
            Directory.CreateDirectory(rootPath);

            var cleared = 0;
            for (var i = 0; i < Categories.Length; i++)
            {
                var category = Categories[i];
                var path = GetCategoryPath(characterId, category);
                var title = "# " + category + Environment.NewLine;
                File.WriteAllText(path, title, Encoding.UTF8);
                cleared++;
            }

            return cleared;
        }

        private static void AppendPromptIntro(StringBuilder builder)
        {
            builder.AppendLine("Use these long-term memories naturally as background context.");
            builder.AppendLine("Do not say \"according to memory\" or reveal memory mechanics unless the user asks.");
            builder.AppendLine();
        }

        private static void AppendEntries(
            StringBuilder builder,
            List<string> entries,
            int limit,
            ref MemoryPromptContextResult result)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                var next = entry.Trim() + Environment.NewLine + Environment.NewLine;
                if (builder.Length + next.Length > limit)
                {
                    var remaining = limit - builder.Length;
                    if (remaining > 0)
                        builder.Append(next.Substring(0, remaining));
                    result.Truncated = true;
                    return;
                }

                builder.Append(next);
            }
        }

        private List<string> LoadEntriesByImportance(string characterId, string importance)
        {
            var entries = new List<string>();
            for (var i = 0; i < Categories.Length; i++)
            {
                var path = GetCategoryPath(characterId, Categories[i]);
                if (!File.Exists(path))
                    continue;

                var text = File.ReadAllText(path, Encoding.UTF8);
                ExtractEntries(text, Categories[i], importance, entries);
            }

            return entries;
        }

        private static void ExtractEntries(string fileText, string category, string importance, List<string> entries)
        {
            if (string.IsNullOrWhiteSpace(fileText))
                return;

            var sections = fileText.Split(new[] { "\n## " }, StringSplitOptions.None);
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (string.IsNullOrWhiteSpace(section))
                    continue;
                if (section.IndexOf("importance: " + importance, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var normalized = section.Trim();
                if (i > 0 && !normalized.StartsWith("## ", StringComparison.Ordinal))
                    normalized = "## " + normalized;
                entries.Add("category: " + category + Environment.NewLine + normalized);
            }
        }

        private static bool IsAllowedCategory(string category)
        {
            var normalized = NormalizeCategory(category);
            for (var i = 0; i < Categories.Length; i++)
            {
                if (Categories[i] == normalized)
                    return true;
            }

            return false;
        }

        private static bool IsAllowedImportance(string importance)
        {
            var normalized = NormalizeImportance(importance);
            for (var i = 0; i < Importances.Length; i++)
            {
                if (Importances[i] == normalized)
                    return true;
            }

            return false;
        }

        private static string NormalizeCharacterId(string characterId)
        {
            return string.IsNullOrWhiteSpace(characterId) ? "unknown" : characterId.Trim().ToLowerInvariant();
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrWhiteSpace(category) ? string.Empty : category.Trim().ToLowerInvariant();
        }

        private static string NormalizeImportance(string importance)
        {
            return string.IsNullOrWhiteSpace(importance) ? string.Empty : importance.Trim().ToLowerInvariant();
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string SanitizeLine(string value)
        {
            return NormalizeText(value).Replace("\r", " ").Replace("\n", " ");
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }
}
