using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class MemoryJudgeDecision
    {
        public bool shouldRemember;
        public string category;
        public string importance;
        public string title;
        public string memoryText;
        public string reason;
    }

    public sealed class MemoryJudgeRequest
    {
        public string CharacterId { get; set; }
        public string CharacterName { get; set; }
        public int RequestId { get; set; }
        public string UserText { get; set; }
        public string CharacterSpeechText { get; set; }
        public string ExistingMemoryContext { get; set; }
    }

    public sealed class MemoryJudgeResult
    {
        public bool Success { get; set; }
        public MemoryJudgeDecision Decision { get; set; }
        public string Error { get; set; }
        public string RawResponse { get; set; }
        public string ExtractedJson { get; set; }
        public string ParseError { get; set; }
    }

    public sealed class MemoryJudgeClient
    {
        private const string ConfigRelativePath = "UserSettings/VirtualPartnerLlmConfig.json";
        private const int RequestTimeoutSeconds = 120;

        private MemoryJudgeConfig config = new MemoryJudgeConfig();
        private UnityWebRequest activeRequest;
        private string configPath;

        public string ConfigPath => configPath;
        public string ConfigStatus { get; private set; } = "Not loaded.";
        public bool ConfigReady { get; private set; }
        public string LastRawResponse { get; private set; }
        public string LastParseError { get; private set; }

        public bool ReloadConfig()
        {
            ConfigReady = false;
            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ConfigRelativePath);

            if (!File.Exists(configPath))
            {
                config = new MemoryJudgeConfig();
                ConfigStatus = $"Missing config: {configPath}";
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<MemoryJudgeConfig>(File.ReadAllText(configPath, Encoding.UTF8)) ?? new MemoryJudgeConfig();
            }
            catch (Exception exception)
            {
                config = new MemoryJudgeConfig();
                ConfigStatus = $"Config parse failed: {exception.Message}";
                return false;
            }

            config.Normalize();
            ConfigReady = config.IsReady(out var reason);
            ConfigStatus = ConfigReady ? "Ready." : reason;
            return ConfigReady;
        }

        public IEnumerator Judge(MemoryJudgeRequest judgeRequest, Action<MemoryJudgeResult> onComplete)
        {
            var result = new MemoryJudgeResult();
            LastRawResponse = string.Empty;
            LastParseError = string.Empty;

            if (!ConfigReady && !ReloadConfig())
            {
                result.Error = ConfigStatus;
                onComplete?.Invoke(result);
                yield break;
            }

            var requestJson = BuildRequestJson(judgeRequest);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            using (var request = new UnityWebRequest(config.GetChatCompletionsUrl(), "POST"))
            {
                activeRequest = request;
                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + config.apiKey);

                yield return request.SendWebRequest();

                activeRequest = null;
                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                LastRawResponse = responseText;
                result.RawResponse = responseText;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    result.Error = $"MemoryJudge request failed: HTTP {request.responseCode} {request.error}";
                    onComplete?.Invoke(result);
                    yield break;
                }

                if (!TryExtractAssistantContent(responseText, out var content, out var contentFailure))
                {
                    result.Error = contentFailure;
                    result.ParseError = contentFailure;
                    LastParseError = contentFailure;
                    onComplete?.Invoke(result);
                    yield break;
                }

                if (!TryExtractMemoryJson(content, out var json, out var jsonFailure))
                {
                    result.Error = jsonFailure;
                    result.ParseError = jsonFailure;
                    LastParseError = jsonFailure;
                    onComplete?.Invoke(result);
                    yield break;
                }

                result.ExtractedJson = json;
                try
                {
                    result.Decision = JsonUtility.FromJson<MemoryJudgeDecision>(json);
                }
                catch (Exception exception)
                {
                    result.ParseError = $"MemoryJudge JSON parse failed: {exception.Message}";
                    result.Error = result.ParseError;
                    LastParseError = result.ParseError;
                    onComplete?.Invoke(result);
                    yield break;
                }

                if (result.Decision == null)
                {
                    result.ParseError = "MemoryJudge JSON parse returned no object.";
                    result.Error = result.ParseError;
                    LastParseError = result.ParseError;
                    onComplete?.Invoke(result);
                    yield break;
                }

                result.Success = true;
                onComplete?.Invoke(result);
            }
        }

        public void Abort()
        {
            if (activeRequest == null)
                return;

            activeRequest.Abort();
            activeRequest.Dispose();
            activeRequest = null;
        }

        private string BuildRequestJson(MemoryJudgeRequest judgeRequest)
        {
            var systemPrompt = "You are MemoryJudge for a Unity virtual partner. Return only one valid JSON object.";
            var developerPrompt = BuildDeveloperPrompt(judgeRequest);
            var combinedSystemPrompt = config.supportsDeveloperRole
                ? systemPrompt
                : systemPrompt + "\n\n" + developerPrompt;

            var builder = new StringBuilder(4096);
            builder.Append("{\"model\":\"").Append(JsonTextUtility.Escape(config.model)).Append("\",\"messages\":[");
            AppendMessage(builder, "system", combinedSystemPrompt);
            if (config.supportsDeveloperRole)
            {
                builder.Append(',');
                AppendMessage(builder, "developer", developerPrompt);
            }

            builder.Append(',');
            AppendMessage(builder, "user", BuildUserPayload(judgeRequest));
            builder.Append(']');
            if (config.useJsonResponseFormat)
                builder.Append(",\"response_format\":{\"type\":\"json_object\"}");
            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildDeveloperPrompt(MemoryJudgeRequest request)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("Decide whether this completed Momotalk conversation turn contains one long-term memory worth saving.");
            builder.AppendLine("Return exactly this JSON shape:");
            builder.AppendLine("{\"shouldRemember\":false,\"category\":\"project_notes\",\"importance\":\"high\",\"title\":\"\",\"memoryText\":\"\",\"reason\":\"\"}");
            builder.AppendLine();
            builder.AppendLine("Allowed category values:");
            builder.AppendLine("user_profile, preferences, project_notes, relationship_notes, important_events, conversation_summaries");
            builder.AppendLine("Allowed importance values:");
            builder.AppendLine("low, medium, high, core");
            builder.AppendLine();
            builder.AppendLine("Strict rules:");
            builder.AppendLine("- Remember only long-term useful facts, stable preferences, confirmed project decisions, relationship notes, important events, or useful summaries.");
            builder.AppendLine("- Do not remember ordinary greetings, small talk, temporary feelings, one-off emotions, transient plans, or low-value chat.");
            builder.AppendLine("- Do not remember sensitive private information unless the user explicitly asks the character to remember it.");
            builder.AppendLine("- Do not record guesses, inferences, or unconfirmed speculation as fact.");
            builder.AppendLine("- If the user explicitly asks to remember something, you may record it if it is safe and useful.");
            builder.AppendLine("- If there is nothing worth remembering, set shouldRemember=false and leave title/memoryText empty.");
            builder.AppendLine("- If remembering, write exactly one concise memory item.");
            builder.AppendLine("- Use the same language as the conversation when possible.");
            builder.AppendLine();
            builder.AppendLine("Existing core/high memory context for duplicate awareness:");
            builder.AppendLine(string.IsNullOrWhiteSpace(request != null ? request.ExistingMemoryContext : string.Empty)
                ? "(none)"
                : request.ExistingMemoryContext);
            return builder.ToString();
        }

        private static string BuildUserPayload(MemoryJudgeRequest request)
        {
            if (request == null)
                return string.Empty;

            var builder = new StringBuilder(2048);
            builder.Append("characterId: ").AppendLine(request.CharacterId ?? string.Empty);
            builder.Append("characterName: ").AppendLine(request.CharacterName ?? string.Empty);
            builder.Append("source requestId: ").AppendLine(request.RequestId.ToString());
            builder.AppendLine();
            builder.AppendLine("User message:");
            builder.AppendLine(request.UserText ?? string.Empty);
            builder.AppendLine();
            builder.AppendLine("Character speech:");
            builder.AppendLine(request.CharacterSpeechText ?? string.Empty);
            return builder.ToString();
        }

        private static bool TryExtractAssistantContent(string responseText, out string content, out string failureReason)
        {
            content = string.Empty;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                failureReason = "MemoryJudge response is empty.";
                return false;
            }

            ChatCompletionResponse response = null;
            try
            {
                response = JsonUtility.FromJson<ChatCompletionResponse>(responseText);
            }
            catch (Exception exception)
            {
                failureReason = $"MemoryJudge response parse failed: {exception.Message}";
                return false;
            }

            if (response == null)
            {
                failureReason = "MemoryJudge response parse returned no object.";
                return false;
            }

            if (response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
            {
                failureReason = $"MemoryJudge LLM error: {response.error.message}";
                return false;
            }

            if (response.choices == null || response.choices.Length == 0 || response.choices[0] == null || response.choices[0].message == null)
            {
                failureReason = "MemoryJudge response has no assistant message.";
                return false;
            }

            content = response.choices[0].message.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                failureReason = "MemoryJudge assistant message is empty.";
                return false;
            }

            return true;
        }

        private static bool TryExtractMemoryJson(string content, out string json, out string failureReason)
        {
            json = string.Empty;
            failureReason = string.Empty;

            var trimmed = JsonTextUtility.StripCodeFence(content);
            if (JsonTextUtility.TryExtractFirstJsonObject(trimmed, out json))
                return true;

            failureReason = "MemoryJudge content does not contain a JSON object.";
            return false;
        }

        private static void AppendMessage(StringBuilder builder, string role, string content)
        {
            builder.Append("{\"role\":\"")
                .Append(JsonTextUtility.Escape(role))
                .Append("\",\"content\":\"")
                .Append(JsonTextUtility.Escape(content))
                .Append("\"}");
        }

        [Serializable]
        private sealed class MemoryJudgeConfig
        {
            public string apiKey;
            public string model;
            public string chatCompletionsUrl;
            public string baseUrl;
            public bool useJsonResponseFormat = true;
            public bool supportsDeveloperRole;

            public void Normalize()
            {
                apiKey = apiKey == null ? string.Empty : apiKey.Trim();
                model = model == null ? string.Empty : model.Trim();
                chatCompletionsUrl = chatCompletionsUrl == null ? string.Empty : chatCompletionsUrl.Trim();
                baseUrl = baseUrl == null ? string.Empty : baseUrl.Trim();
            }

            public bool IsReady(out string reason)
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    reason = "apiKey is missing.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    reason = "model is missing.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(GetChatCompletionsUrl()))
                {
                    reason = "chatCompletionsUrl or baseUrl is missing.";
                    return false;
                }

                reason = string.Empty;
                return true;
            }

            public string GetChatCompletionsUrl()
            {
                if (!string.IsNullOrWhiteSpace(chatCompletionsUrl))
                    return chatCompletionsUrl;

                if (string.IsNullOrWhiteSpace(baseUrl))
                    return string.Empty;

                return baseUrl.TrimEnd('/') + "/v1/chat/completions";
            }
        }

        [Serializable]
        private sealed class ChatCompletionResponse
        {
            public ChatCompletionChoice[] choices;
            public ChatCompletionError error;
        }

        [Serializable]
        private sealed class ChatCompletionChoice
        {
            public ChatCompletionMessage message;
        }

        [Serializable]
        private sealed class ChatCompletionMessage
        {
            public string content;
        }

        [Serializable]
        private sealed class ChatCompletionError
        {
            public string message;
        }
    }
}
