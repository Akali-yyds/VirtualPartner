using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TimelineDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TimelinePlayer timelinePlayer;
        [SerializeField] private TextAsset sampleTimeline;

        [Header("Runtime Status")]
        [SerializeField] private bool standaloneVisible = true;
        [SerializeField] private bool minimized;
        [SerializeField, TextArea(8, 16)] private string timelineJson;
        [SerializeField] private Rect windowRect = new Rect(400f, 20f, 440f, 560f);

        private Vector2 jsonScroll;
        private Vector2 expandedWindowSize = new Vector2(440f, 560f);

        public void SetStandaloneVisible(bool visible)
        {
            standaloneVisible = visible;
        }

        private void Start()
        {
            if (!ValidateReferences())
                return;

            if (string.IsNullOrWhiteSpace(timelineJson) && sampleTimeline != null)
                timelineJson = sampleTimeline.text;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !standaloneVisible)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP Timeline" : "VirtualPartner Timeline");
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
            DrawControls();
            DrawJsonEditor();
            GUI.DragWindow();
        }

        public void DrawEmbedded()
        {
            if (timelinePlayer == null)
            {
                GUILayout.Label("TimelinePlayer: Missing");
                return;
            }

            DrawStatus();
            DrawControls();
            DrawJsonEditor();
        }

        private void DrawCompactStatus()
        {
            if (timelinePlayer == null)
            {
                GUILayout.Label("Timeline: Missing");
                return;
            }

            GUILayout.Label(timelinePlayer.IsPlaying ? $"Playing {timelinePlayer.CurrentTime:0.00}s" : timelinePlayer.StatusText);
        }

        private void DrawStatus()
        {
            if (timelinePlayer == null)
            {
                GUILayout.Label("TimelinePlayer: Missing");
                return;
            }

            GUILayout.Label($"Status: {timelinePlayer.StatusText}");
            GUILayout.Label($"Owner: {FormatOwner(timelinePlayer.ActiveOwnerId)}");
            GUILayout.Label($"Time: {timelinePlayer.CurrentTime:0.00}s  Segment: {timelinePlayer.CurrentSegmentStatus}");
            GUILayout.Label($"Errors: {timelinePlayer.ErrorCount}  Warnings: {timelinePlayer.WarningCount}");

            if (!string.IsNullOrWhiteSpace(timelinePlayer.LastMessage))
                GUILayout.Label($"Last: {timelinePlayer.LastMessage}");
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Sample"))
                LoadSample();
            if (GUILayout.Button("Paste"))
                timelineJson = GUIUtility.systemCopyBuffer;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate"))
                timelinePlayer.ValidateTimelineJson(timelineJson);
            if (GUILayout.Button("Play"))
                timelinePlayer.PlayJson(timelineJson);
            if (GUILayout.Button("Replace"))
                timelinePlayer.ReplaceJson(timelineJson);
            if (GUILayout.Button("Stop"))
                timelinePlayer.StopTimeline();
            GUILayout.EndHorizontal();
        }

        private void DrawJsonEditor()
        {
            GUILayout.Label("Timeline JSON");
            jsonScroll = GUILayout.BeginScrollView(jsonScroll, GUILayout.Height(360f));
            timelineJson = GUILayout.TextArea(timelineJson);
            GUILayout.EndScrollView();
        }

        private void LoadSample()
        {
            if (sampleTimeline == null)
                return;

            timelineJson = sampleTimeline.text;
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

        private static string FormatOwner(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? "External" : ownerId;
        }

        private bool ValidateReferences()
        {
            if (timelinePlayer == null)
                return Fail("TimelinePlayer reference is missing.");
            if (sampleTimeline == null)
                Debug.LogWarning("[VirtualPartner] TimelineDebugPanel has no sample TextAsset.", this);

            return true;
        }

        private bool Fail(string message)
        {
            Debug.LogError($"[VirtualPartner] TimelineDebugPanel failed: {message}", this);
            enabled = false;
            return false;
        }
    }
}
