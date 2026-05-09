using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualPartnerBoneDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;

        [Header("Runtime Status")]
        [SerializeField] private bool apply;
        [SerializeField] private bool minimized;
        [SerializeField] private Vector3 rotation;
        [SerializeField] private int selectedIndex;
        [SerializeField, TextArea(4, 8)] private string exportedJson;
        [SerializeField] private int semanticConfigCount;
        [SerializeField] private int controlInstanceCount;
        [SerializeField] private int missingInstanceCount;
        [SerializeField] private bool debugOverlayActive;
        [SerializeField] private string activeDebugBone;
        [SerializeField] private Rect windowRect = new Rect(20f, 20f, 360f, 560f);

        private readonly List<BoneMapInstance> controlInstances = new List<BoneMapInstance>();
        private Vector2 boneListScroll;
        private Vector2 expandedWindowSize = new Vector2(360f, 560f);

        private void Start()
        {
            apply = false;
            debugOverlayActive = false;
            activeDebugBone = string.Empty;

            if (!ValidateReferences())
                return;

            RefreshControlInstances();
        }

        private void LateUpdate()
        {
            if (!apply)
            {
                EndDebugOverlay();
                return;
            }

            if (selectedIndex < 0 || selectedIndex >= controlInstances.Count)
                return;

            var instance = controlInstances[selectedIndex];
            var clampedRotation = instance.Entry.ClampRotation(rotation);
            var mirrorSign = instance.Entry.GetMirrorSign(instance.Side);

            if (!avatarPoseApplier.HasBaseRotation)
                return;

            if (!avatarPoseApplier.ApplySemanticBoneRotation(instance.Transform, clampedRotation, mirrorSign))
                return;

            BeginDebugOverlay(instance.DisplayName);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP Bone Debug" : "VirtualPartner Bone Debug");
        }

        public void BeginDebugOverlay()
        {
            debugOverlayActive = true;
        }

        public void EndDebugOverlay()
        {
            debugOverlayActive = false;
            activeDebugBone = string.Empty;
        }

        private void BeginDebugOverlay(string boneName)
        {
            debugOverlayActive = true;
            activeDebugBone = boneName;
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (minimized)
            {
                GUILayout.Label(apply ? "Apply: On" : "Apply: Off");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"Configs: {semanticConfigCount}  Instances: {controlInstanceCount}  Missing: {missingInstanceCount}");
            GUILayout.Label($"BaseRotation: {(avatarPoseApplier != null && avatarPoseApplier.HasBaseRotation ? "Ready" : "Waiting")}");
            GUILayout.Label($"Overlay: {(debugOverlayActive ? "On" : "Off")}  Active: {(string.IsNullOrEmpty(activeDebugBone) ? "-" : activeDebugBone)}");

            var nextApply = GUILayout.Toggle(apply, "Apply Debug Overlay");
            if (apply && !nextApply)
                EndDebugOverlay();
            apply = nextApply;

            if (GUILayout.Button("Refresh BoneMap"))
                RefreshControlInstances();

            DrawBoneList();
            DrawRotationControls();
            DrawExportControls();

            GUI.DragWindow();
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 220f;
                windowRect.height = 72f;
                return;
            }

            windowRect.width = Mathf.Max(320f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(360f, expandedWindowSize.y);
        }

        private void DrawBoneList()
        {
            GUILayout.Label("Controlled Bone");
            boneListScroll = GUILayout.BeginScrollView(boneListScroll, GUILayout.Height(140f));

            for (var i = 0; i < controlInstances.Count; i++)
            {
                var previousColor = GUI.backgroundColor;
                if (i == selectedIndex)
                    GUI.backgroundColor = Color.cyan;

                if (GUILayout.Button(controlInstances[i].DisplayName))
                    selectedIndex = i;

                GUI.backgroundColor = previousColor;
            }

            GUILayout.EndScrollView();
        }

        private void DrawRotationControls()
        {
            if (selectedIndex < 0 || selectedIndex >= controlInstances.Count)
                return;

            var entry = controlInstances[selectedIndex].Entry;
            rotation = entry.ClampRotation(rotation);

            GUILayout.Label($"Selected: {controlInstances[selectedIndex].DisplayName}");

            var x = rotation.x;
            var y = rotation.y;
            var z = rotation.z;

            DrawAxisSlider("X", ref x, entry, 0);
            DrawAxisSlider("Y", ref y, entry, 1);
            DrawAxisSlider("Z", ref z, entry, 2);

            rotation = entry.ClampRotation(new Vector3(x, y, z));

            if (GUILayout.Button("Zero"))
                rotation = Vector3.zero;
        }

        private static void DrawAxisSlider(string label, ref float value, BoneMapEntry entry, int axis)
        {
            var enabled = entry.IsAxisEnabled(axis);
            GUI.enabled = enabled;

            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(20f));
            value = GUILayout.HorizontalSlider(value, entry.RangeMin, entry.RangeMax);
            GUILayout.Label(value.ToString("0.0", CultureInfo.InvariantCulture), GUILayout.Width(52f));
            GUILayout.EndHorizontal();

            if (!enabled)
                value = 0f;

            GUI.enabled = true;
        }

        private void DrawExportControls()
        {
            if (GUILayout.Button("Export JSON"))
                ExportCurrentJson();

            if (!string.IsNullOrEmpty(exportedJson))
                GUILayout.TextArea(exportedJson, GUILayout.Height(110f));
        }

        private void ExportCurrentJson()
        {
            if (selectedIndex < 0 || selectedIndex >= controlInstances.Count)
                return;

            var instance = controlInstances[selectedIndex];
            var clampedRotation = instance.Entry.ClampRotation(rotation);
            exportedJson = BuildBonePoseJson(instance, clampedRotation);
            GUIUtility.systemCopyBuffer = exportedJson;
            Debug.Log($"[VirtualPartner] Debug bone pose exported:\n{exportedJson}", this);
        }

        private void RefreshControlInstances()
        {
            missingInstanceCount = boneMapProfile.BuildControlInstances(boneRoot, controlInstances);
            semanticConfigCount = boneMapProfile.SemanticConfigCount;
            controlInstanceCount = controlInstances.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, controlInstances.Count - 1));

            if (missingInstanceCount > 0)
                Debug.LogWarning($"[VirtualPartner] BoneMap resolved with {missingInstanceCount} missing debug bone instance(s).", this);
        }

        private bool ValidateReferences()
        {
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.");

            return true;
        }

        private bool Fail(string message)
        {
            Debug.LogError($"[VirtualPartner] Bone debug panel failed: {message}", this);
            enabled = false;
            return false;
        }

        private static string BuildBonePoseJson(BoneMapInstance instance, Vector3 semanticRotation)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"type\": \"bonePose\",");
            builder.AppendLine("  \"bones\": [");
            builder.AppendLine("    {");
            builder.Append("      \"bone\": \"").Append(instance.SemanticBone).AppendLine("\",");

            if (instance.Side != BoneSide.None)
                builder.Append("      \"side\": \"").Append(instance.Side).AppendLine("\",");

            builder.Append("      \"rotation\": { \"x\": ")
                .Append(FormatFloat(semanticRotation.x))
                .Append(", \"y\": ")
                .Append(FormatFloat(semanticRotation.y))
                .Append(", \"z\": ")
                .Append(FormatFloat(semanticRotation.z))
                .AppendLine(" }");
            builder.AppendLine("    }");
            builder.AppendLine("  ]");
            builder.Append("}");
            return builder.ToString();
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
