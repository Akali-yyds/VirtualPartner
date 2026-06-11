using System.Globalization;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LlmInteractionDebugPanel : MonoBehaviour
    {
        private const int ResponsePreviewCharLimit = 6000;

        [Header("References")]
        [SerializeField] private LlmRelay llmRelay;

        [Header("Runtime Status")]
        [SerializeField] private bool standaloneVisible = true;
        [SerializeField] private bool minimized;
        [SerializeField, TextArea(2, 5)] private string userText = "打个招呼";
        [SerializeField] private string promptCopyStatus;
        [SerializeField] private Rect windowRect = new Rect(20f, 260f, 430f, 520f);

        private Vector2 responseScroll;
        private Vector2 stagePlanScroll;
        private Vector2 expandedWindowSize = new Vector2(430f, 520f);
        private string cachedRawResponseSource;
        private string cachedRawResponsePreview;
        private string cachedStagePlanSource;
        private string cachedStagePlanPreview;
        private bool configEditorInitialized;
        private bool showApiKey;
        private string editApiKey = string.Empty;
        private string editModel = string.Empty;
        private string editChatCompletionsUrl = string.Empty;
        private string editBaseUrl = string.Empty;
        private bool editUseJsonResponseFormat = true;
        private bool editSupportsDeveloperRole;
        private string editInteractionTimeoutSeconds = "10";
        private string configEditStatus;

        public void SetStandaloneVisible(bool visible)
        {
            standaloneVisible = visible;
        }

        public void Configure(LlmRelay relay)
        {
            llmRelay = relay;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !standaloneVisible)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP LLM" : "VirtualPartner LLM");
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (minimized)
            {
                DrawCompactStatus();
                GUI.DragWindow();
                return;
            }

            DrawStatus();
            DrawInput();
            DrawResponse();
            GUI.DragWindow();
        }

        public void DrawEmbedded()
        {
            if (llmRelay == null)
            {
                GUILayout.Label("LlmRelay: Missing");
                return;
            }

            DrawStatus();
            DrawInput();
            DrawResponse();
        }

        public void DrawApiConfigEmbedded()
        {
            if (llmRelay == null)
            {
                GUILayout.Label("LlmRelay: Missing");
                return;
            }

            DrawConfigEditor();
        }

        private void DrawCompactStatus()
        {
            if (llmRelay == null)
            {
                GUILayout.Label("LLM: Missing");
                return;
            }

            GUILayout.Label(llmRelay.StatusText);
        }

        private void DrawStatus()
        {
            if (llmRelay == null)
            {
                GUILayout.Label("LlmRelay: Missing");
                return;
            }

            GUILayout.Label($"Status: {llmRelay.StatusText}");
            GUILayout.Label($"Config: {llmRelay.ConfigStatus}");
            GUILayout.Label($"Request: latest {llmRelay.LatestRequestId}  pending {(llmRelay.RequestPending ? llmRelay.PendingRequestId.ToString() : "-")}");
            GUILayout.Label($"Interaction timeout: {llmRelay.InteractionTimeoutSeconds:0.#}s  LLM StagePlan: {(llmRelay.IsLlmStagePlanPlaying ? "Playing" : "No")}");
            GUILayout.Label($"Streaming: {(llmRelay.StreamStagePlans ? "On" : "Off")}  active {llmRelay.StreamingRequestActive}  started {llmRelay.StreamingStagePlanStarted}  buffer {llmRelay.StreamInitialStageBufferCount}");
            GUILayout.Label($"Stream stages: parsed {llmRelay.StreamingParsedStageCount}  buffered {llmRelay.StreamingBufferedStageCount}  appended {llmRelay.StreamingAppendedStageCount}");

            if (!string.IsNullOrWhiteSpace(llmRelay.LastError))
                GUILayout.Label($"Last Error: {llmRelay.LastError}");
            if (!string.IsNullOrWhiteSpace(llmRelay.LastStreamingStatus))
                GUILayout.Label($"Stream: {llmRelay.LastStreamingStatus}");
            if (!string.IsNullOrWhiteSpace(promptCopyStatus))
                GUILayout.Label($"Prompt: {promptCopyStatus}");
        }

        private void DrawInput()
        {
            GUILayout.Label("User Text");
            userText = GUILayout.TextArea(userText, GUILayout.Height(64f));

            GUILayout.BeginHorizontal();
            GUI.enabled = llmRelay != null && !llmRelay.RequestPending;
            if (GUILayout.Button("Submit"))
                llmRelay.Submit(userText);
            GUI.enabled = llmRelay != null;
            if (GUILayout.Button("Stop LLM StagePlan"))
                llmRelay.StopLlmStagePlan();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUI.enabled = llmRelay != null;
            if (GUILayout.Button("Copy Prompt"))
            {
                llmRelay.CopyFinalPromptToClipboard();
                promptCopyStatus = llmRelay.LastPromptStatus;
            }
            GUI.enabled = true;
        }

        private void DrawConfigEditor()
        {
            if (llmRelay == null)
                return;

            EnsureConfigEditorInitialized();

            GUILayout.Label("API Config");
            GUILayout.Label($"Status: {llmRelay.ConfigStatus}");
            GUILayout.Label($"Config Path: {llmRelay.ConfigPath}");
            DrawTextFieldRow("Model", ref editModel);
            DrawTextFieldRow("Base URL", ref editBaseUrl);
            DrawTextFieldRow("Chat URL", ref editChatCompletionsUrl);

            GUILayout.Label("API Key");
            GUILayout.BeginHorizontal();
            editApiKey = showApiKey
                ? GUILayout.TextField(editApiKey)
                : GUILayout.PasswordField(editApiKey, '*');
            showApiKey = GUILayout.Toggle(showApiKey, "Show", GUILayout.Width(64f));
            GUILayout.EndHorizontal();

            editUseJsonResponseFormat = GUILayout.Toggle(editUseJsonResponseFormat, "Use JSON response_format");
            editSupportsDeveloperRole = GUILayout.Toggle(editSupportsDeveloperRole, "Use developer role");
            DrawTextFieldRow("Interaction Timeout", ref editInteractionTimeoutSeconds);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload File"))
            {
                llmRelay.ReloadConfig();
                LoadCurrentConfigIntoEditor("Reloaded config file.");
            }
            if (GUILayout.Button("Load Current"))
                LoadCurrentConfigIntoEditor("Loaded current config.");

            var previousEnabled = GUI.enabled;
            GUI.enabled = !llmRelay.ConfigTestPending;
            if (GUILayout.Button(llmRelay.ConfigTestPending ? "Testing..." : "Test"))
                StartConfigTestFromEditor();
            GUI.enabled = previousEnabled;

            if (GUILayout.Button("Save"))
                SaveConfigFromEditor();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(configEditStatus))
                GUILayout.Label(configEditStatus);
            if (!string.IsNullOrWhiteSpace(llmRelay.ConfigTestStatus))
                GUILayout.Label($"Test: {llmRelay.ConfigTestStatus}");
        }

        private void EnsureConfigEditorInitialized()
        {
            if (configEditorInitialized)
                return;

            LoadCurrentConfigIntoEditor(string.Empty);
        }

        private void LoadCurrentConfigIntoEditor(string status)
        {
            if (llmRelay == null)
                return;

            var draft = llmRelay.CreateConfigDraft();
            editApiKey = draft.apiKey ?? string.Empty;
            editModel = draft.model ?? string.Empty;
            editChatCompletionsUrl = draft.chatCompletionsUrl ?? string.Empty;
            editBaseUrl = draft.baseUrl ?? string.Empty;
            editUseJsonResponseFormat = draft.useJsonResponseFormat;
            editSupportsDeveloperRole = draft.supportsDeveloperRole;
            editInteractionTimeoutSeconds = draft.interactionTimeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture);
            configEditStatus = status ?? string.Empty;
            configEditorInitialized = true;
        }

        private void StartConfigTestFromEditor()
        {
            if (!TryBuildConfigDraftFromEditor(out var draft, out var failureReason))
            {
                configEditStatus = failureReason;
                return;
            }

            if (llmRelay.StartConfigTest(draft))
                configEditStatus = "Config test started.";
            else
                configEditStatus = llmRelay.ConfigTestStatus;
        }

        private void SaveConfigFromEditor()
        {
            if (!TryBuildConfigDraftFromEditor(out var draft, out var failureReason))
            {
                configEditStatus = failureReason;
                return;
            }

            if (llmRelay.SaveConfig(draft, out var message))
                LoadCurrentConfigIntoEditor(message);
            else
                configEditStatus = message;
        }

        private bool TryBuildConfigDraftFromEditor(out LlmRelayConfigDraft draft, out string failureReason)
        {
            draft = null;
            failureReason = string.Empty;

            if (!float.TryParse(editInteractionTimeoutSeconds, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeoutSeconds)
                || timeoutSeconds <= 0f)
            {
                failureReason = "Interaction Timeout must be a positive number.";
                return false;
            }

            draft = new LlmRelayConfigDraft
            {
                apiKey = editApiKey,
                model = editModel,
                chatCompletionsUrl = editChatCompletionsUrl,
                baseUrl = editBaseUrl,
                useJsonResponseFormat = editUseJsonResponseFormat,
                supportsDeveloperRole = editSupportsDeveloperRole,
                interactionTimeoutSeconds = timeoutSeconds
            };
            return true;
        }

        private static void DrawTextFieldRow(string label, ref string value)
        {
            GUILayout.Label(label);
            value = GUILayout.TextField(value ?? string.Empty);
        }

        private void DrawResponse()
        {
            if (llmRelay == null)
                return;

            if (llmRelay.RequestPending || llmRelay.StreamingRequestActive)
            {
                GUILayout.Space(8f);
                GUILayout.Label("LLM response is loading.");
                GUILayout.Label("Raw response and extracted StagePlan will appear after the request completes.");
                if (!string.IsNullOrWhiteSpace(llmRelay.LastStreamingStatus))
                    GUILayout.Label($"Stream: {llmRelay.LastStreamingStatus}");
                return;
            }

            DrawResponseTextArea(
                "Last Raw Response",
                llmRelay.LastRawResponse,
                ref responseScroll,
                ref cachedRawResponseSource,
                ref cachedRawResponsePreview);

            DrawResponseTextArea(
                "Last Extracted StagePlan",
                llmRelay.LastExtractedStagePlan,
                ref stagePlanScroll,
                ref cachedStagePlanSource,
                ref cachedStagePlanPreview);
        }

        private static void DrawResponseTextArea(
            string title,
            string text,
            ref Vector2 scroll,
            ref string cachedSource,
            ref string cachedPreview)
        {
            text = text ?? string.Empty;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{title} ({text.Length} chars)");
            var previousEnabled = GUI.enabled;
            GUI.enabled = !string.IsNullOrEmpty(text);
            if (GUILayout.Button("Copy", GUILayout.Width(72f)))
                GUIUtility.systemCopyBuffer = text;
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(120f));
            GUILayout.TextArea(GetPreview(text, ref cachedSource, ref cachedPreview));
            GUILayout.EndScrollView();

            if (text.Length > ResponsePreviewCharLimit)
                GUILayout.Label($"Preview truncated to {ResponsePreviewCharLimit} chars. Use Copy for full text.");
        }

        private static string GetPreview(string text, ref string cachedSource, ref string cachedPreview)
        {
            if (ReferenceEquals(text, cachedSource))
                return cachedPreview;

            cachedSource = text;
            cachedPreview = BuildPreview(text);
            return cachedPreview;
        }

        private static string BuildPreview(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= ResponsePreviewCharLimit)
                return text ?? string.Empty;

            var headLength = ResponsePreviewCharLimit / 2;
            var tailLength = ResponsePreviewCharLimit - headLength;
            return text.Substring(0, headLength)
                + "\n\n... preview truncated ...\n\n"
                + text.Substring(text.Length - tailLength, tailLength);
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 220f;
                windowRect.height = 74f;
                return;
            }

            windowRect.width = Mathf.Max(360f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(360f, expandedWindowSize.y);
        }
    }
}
