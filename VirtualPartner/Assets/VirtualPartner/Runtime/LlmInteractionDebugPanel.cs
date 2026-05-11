using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class LlmInteractionDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LlmRelay llmRelay;

        [Header("Runtime Status")]
        [SerializeField] private bool minimized;
        [SerializeField, TextArea(2, 5)] private string userText = "打个招呼";
        [SerializeField] private string promptCopyStatus;
        [SerializeField] private Rect windowRect = new Rect(20f, 260f, 430f, 520f);

        private Vector2 responseScroll;
        private Vector2 timelineScroll;
        private Vector2 expandedWindowSize = new Vector2(430f, 520f);

        public void Configure(LlmRelay relay)
        {
            llmRelay = relay;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
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
            GUILayout.Label($"Interaction timeout: {llmRelay.InteractionTimeoutSeconds:0.#}s  LLM timeline: {(llmRelay.IsLlmTimelinePlaying ? "Playing" : "No")}");

            if (!string.IsNullOrWhiteSpace(llmRelay.LastError))
                GUILayout.Label($"Last Error: {llmRelay.LastError}");
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
            if (GUILayout.Button("Stop LLM Timeline"))
                llmRelay.StopLlmTimeline();
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

            GUILayout.Label("Last Raw Response");
            responseScroll = GUILayout.BeginScrollView(responseScroll, GUILayout.Height(120f));
            GUILayout.TextArea(llmRelay.LastRawResponse);
            GUILayout.EndScrollView();

            GUILayout.Label("Last Extracted Timeline");
            timelineScroll = GUILayout.BeginScrollView(timelineScroll, GUILayout.Height(120f));
            GUILayout.TextArea(llmRelay.LastExtractedTimeline);
            GUILayout.EndScrollView();
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
