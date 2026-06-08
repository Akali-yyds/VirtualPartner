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
            if (GUILayout.Button("Reload Config"))
                llmRelay.ReloadConfig();
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

            if (llmRelay != null)
                GUILayout.Label($"Config Path: {llmRelay.ConfigPath}");
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
