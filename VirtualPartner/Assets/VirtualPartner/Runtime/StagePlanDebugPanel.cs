using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StagePlanDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private TextAsset basicSample;
        [SerializeField] private TextAsset fullSample;

        [Header("Display")]
        [SerializeField] private bool standaloneVisible = true;
        [SerializeField] private bool minimized;
        [SerializeField, TextArea(8, 16)] private string stagePlanJson;
        [SerializeField] private Rect windowRect = new Rect(820f, 20f, 480f, 620f);

        [Header("Last Result")]
        [SerializeField] private bool valid;
        [SerializeField] private int errorCount;
        [SerializeField] private int warningCount;
        [SerializeField] private int validStageCount;
        [SerializeField] private int validActionCount;
        [SerializeField, TextArea(4, 10)] private string lastResult = "Not validated.";

        private Vector2 jsonScroll;
        private Vector2 resultScroll;
        private Vector2 expandedWindowSize = new Vector2(480f, 620f);

        public void SetStandaloneVisible(bool visible)
        {
            standaloneVisible = visible;
        }

        private void Start()
        {
            if (string.IsNullOrWhiteSpace(stagePlanJson) && basicSample != null)
                stagePlanJson = basicSample.text;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !standaloneVisible)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP StagePlan" : "VirtualPartner StagePlan");
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
            DrawValidationResult();
            GUI.DragWindow();
        }

        private void DrawCompactStatus()
        {
            GUILayout.Label(valid ? $"Valid {validStageCount}/{validActionCount}" : $"Invalid {errorCount} error(s)");
        }

        private void DrawStatus()
        {
            GUILayout.Label($"Profile: {(characterProfile == null ? "Missing" : characterProfile.CharacterId)}");
            GUILayout.Label($"Valid: {valid}  Errors: {errorCount}  Warnings: {warningCount}");
            GUILayout.Label($"Effective Stages: {validStageCount}  Effective Actions: {validActionCount}");
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Basic"))
                LoadSample(basicSample);
            if (GUILayout.Button("Load Full"))
                LoadSample(fullSample);
            if (GUILayout.Button("Paste Clipboard"))
                stagePlanJson = GUIUtility.systemCopyBuffer;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate"))
                ValidateCurrentJson();
            if (GUILayout.Button("Clear"))
                Clear();
            GUILayout.EndHorizontal();
        }

        private void DrawJsonEditor()
        {
            GUILayout.Label("StagePlan JSON");
            jsonScroll = GUILayout.BeginScrollView(jsonScroll, GUILayout.Height(300f));
            stagePlanJson = GUILayout.TextArea(stagePlanJson ?? string.Empty);
            GUILayout.EndScrollView();
        }

        private void DrawValidationResult()
        {
            GUILayout.Label("Validation Result");
            resultScroll = GUILayout.BeginScrollView(resultScroll, GUILayout.Height(170f));
            GUILayout.TextArea(lastResult ?? string.Empty);
            GUILayout.EndScrollView();
        }

        private void LoadSample(TextAsset sample)
        {
            if (sample == null)
                return;

            stagePlanJson = sample.text;
            lastResult = "Sample loaded. Press Validate.";
        }

        private void ValidateCurrentJson()
        {
            var result = StagePlanValidator.Validate(stagePlanJson, characterProfile);
            valid = result.IsValid;
            errorCount = result.ErrorCount;
            warningCount = result.WarningCount;
            validStageCount = result.ValidStageCount;
            validActionCount = result.ValidActionCount;
            lastResult = FormatResult(result);

            if (valid)
                Debug.Log($"[VirtualPartner] StagePlan validation passed. stages={validStageCount}, actions={validActionCount}, warnings={warningCount}", this);
            else
                Debug.LogWarning($"[VirtualPartner] StagePlan validation failed. errors={errorCount}, warnings={warningCount}", this);
        }

        private void Clear()
        {
            stagePlanJson = string.Empty;
            valid = false;
            errorCount = 0;
            warningCount = 0;
            validStageCount = 0;
            validActionCount = 0;
            lastResult = "Cleared.";
        }

        private string FormatResult(StagePlanValidationResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine(result.IsValid ? "Valid" : "Invalid");
            builder.AppendLine($"Effective Stages: {result.ValidStageCount}");
            builder.AppendLine($"Effective Actions: {result.ValidActionCount}");
            builder.AppendLine($"Errors: {result.ErrorCount}");
            for (var i = 0; i < result.Errors.Count; i++)
                builder.AppendLine($"- {result.Errors[i]}");

            builder.AppendLine($"Warnings: {result.WarningCount}");
            for (var i = 0; i < result.Warnings.Count; i++)
                builder.AppendLine($"- {result.Warnings[i]}");

            return builder.ToString();
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 240f;
                windowRect.height = 78f;
                return;
            }

            windowRect.width = Mathf.Max(400f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(420f, expandedWindowSize.y);
        }
    }
}
