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
        [SerializeField] private ActionCoordinator actionCoordinator;
        [Tooltip("Optional. Held while posing to stop the FSM from auto-turning the character. Auto-resolved at runtime if unset.")]
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;

        [Header("Runtime Status")]
        [SerializeField] private bool standaloneVisible = true;
        [SerializeField] private bool apply;
        [SerializeField] private bool minimized;
        [SerializeField] private Vector3 rotation;
        [SerializeField] private int selectedIndex;
        [SerializeField] private int pinnedCount;
        [SerializeField, TextArea(4, 8)] private string exportedJson;
        [SerializeField] private int semanticConfigCount;
        [SerializeField] private int controlInstanceCount;
        [SerializeField] private int missingInstanceCount;
        [SerializeField] private bool debugOverlayActive;
        [SerializeField] private string activeDebugBone;
        [SerializeField] private float exportDuration = 1.0f;
        [SerializeField] private Rect windowRect = new Rect(20f, 20f, 360f, 640f);

        private readonly List<BoneMapInstance> controlInstances = new List<BoneMapInstance>();

        // Multi-hold ("pin") state: every pinned bone is kept applied each frame so a
        // full multi-bone pose can be dialed in live. Keyed by index into controlInstances.
        private readonly Dictionary<int, Vector3> heldRotations = new Dictionary<int, Vector3>();
        private readonly HashSet<int> appliedIndices = new HashSet<int>();
        private readonly HashSet<int> desiredIndices = new HashSet<int>();
        private readonly List<int> indexBuffer = new List<int>();

        private Vector2 boneListScroll;
        private Vector2 expandedWindowSize = new Vector2(360f, 640f);
        private int lastSelectedIndex = -1;

        public void SetStandaloneVisible(bool visible)
        {
            standaloneVisible = visible;
        }

        private void Start()
        {
            apply = false;
            debugOverlayActive = false;
            activeDebugBone = string.Empty;
            lastSelectedIndex = -1;

            if (!ValidateReferences())
                return;

            if (autonomousBehaviorScheduler == null)
                autonomousBehaviorScheduler = FindFirstObjectByType<AutonomousBehaviorScheduler>();

            RefreshControlInstances();
        }

        private void Update()
        {
            SyncSelection();
            ApplyDesiredOverlays();
            SuppressAutonomousTurnWhilePosing();
            pinnedCount = heldRotations.Count;
        }

        private void OnDisable()
        {
            ReleaseAllApplied();
        }

        // When the selection changes to a pinned bone, load its stored rotation so the
        // sliders continue editing that bone's actual held value.
        private void SyncSelection()
        {
            if (selectedIndex == lastSelectedIndex)
                return;

            lastSelectedIndex = selectedIndex;
            if (heldRotations.TryGetValue(selectedIndex, out var held))
                rotation = held;
        }

        // Build the desired Debug-overlay set (all pinned bones, plus the live-previewed
        // selected bone when Apply is on), then reconcile against what is applied.
        private void ApplyDesiredOverlays()
        {
            if (actionCoordinator == null)
                return;

            // Live-editing a pinned + selected bone keeps its stored value in sync.
            if (apply && IsValidIndex(selectedIndex) && heldRotations.ContainsKey(selectedIndex))
                heldRotations[selectedIndex] = rotation;

            desiredIndices.Clear();
            foreach (var pair in heldRotations)
                desiredIndices.Add(pair.Key);
            if (apply && IsValidIndex(selectedIndex))
                desiredIndices.Add(selectedIndex);

            // Release bones that were applied last frame but are no longer desired.
            indexBuffer.Clear();
            foreach (var idx in appliedIndices)
            {
                if (!desiredIndices.Contains(idx))
                    indexBuffer.Add(idx);
            }

            for (var i = 0; i < indexBuffer.Count; i++)
            {
                var idx = indexBuffer[i];
                if (IsValidIndex(idx))
                    actionCoordinator.ReleaseDebug(controlInstances[idx]);
                appliedIndices.Remove(idx);
            }

            // Apply / refresh every desired bone.
            foreach (var idx in desiredIndices)
            {
                if (!IsValidIndex(idx))
                    continue;

                var rot = GetRotationForIndex(idx);
                if (actionCoordinator.RequestDebug(controlInstances[idx], rot))
                    appliedIndices.Add(idx);
            }
        }

        private Vector3 GetRotationForIndex(int index)
        {
            // The live-previewed selected bone uses the working rotation; everything else
            // (pinned bones) uses its stored held value.
            if (index == selectedIndex && apply)
                return rotation;
            return heldRotations.TryGetValue(index, out var held) ? held : Vector3.zero;
        }

        // While any debug overlay is active (live preview or pinned bones), keep the FSM
        // in the user-interaction state so it stops auto-turning the character and holds
        // it facing the camera. The interaction timeout is refreshed every frame; once
        // posing stops, the FSM resumes autonomously after the timeout.
        private void SuppressAutonomousTurnWhilePosing()
        {
            if (autonomousBehaviorScheduler == null)
                return;

            if (apply || heldRotations.Count > 0)
                autonomousBehaviorScheduler.KeepUserInteractionAlive();
        }

        private void ReleaseAllApplied()
        {
            if (actionCoordinator != null)
            {
                foreach (var idx in appliedIndices)
                {
                    if (IsValidIndex(idx))
                        actionCoordinator.ReleaseDebug(controlInstances[idx]);
                }
            }

            appliedIndices.Clear();
            EndDebugOverlay();
        }

        private void RefreshSelectedStatus()
        {
            var selectedInstance = GetSelectedInstance();
            if (selectedInstance == null || actionCoordinator == null)
            {
                debugOverlayActive = false;
                activeDebugBone = string.Empty;
                return;
            }

            debugOverlayActive = actionCoordinator.GetOwner(selectedInstance.Transform) == BoneOwner.Debug;
            activeDebugBone = selectedInstance.DisplayName;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !standaloneVisible)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP Bone Debug" : "VirtualPartner Bone Debug");
        }

        private void EndDebugOverlay()
        {
            debugOverlayActive = false;
            activeDebugBone = string.Empty;
        }

        private BoneMapInstance GetSelectedInstance()
        {
            return IsValidIndex(selectedIndex) ? controlInstances[selectedIndex] : null;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < controlInstances.Count;
        }

        private void DrawWindow(int windowId)
        {
            RefreshSelectedStatus();

            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (minimized)
            {
                GUILayout.Label($"Apply: {(apply ? "On" : "Off")}  Pins: {heldRotations.Count}");
                GUI.DragWindow();
                return;
            }

            DrawEmbedded(true);
            GUI.DragWindow();
        }

        public void DrawEmbedded()
        {
            DrawEmbedded(false);
        }

        public void DrawEmbedded(bool allowBoneMapRefresh)
        {
            RefreshSelectedStatus();

            GUILayout.Label($"Configs: {semanticConfigCount}  Instances: {controlInstanceCount}  Missing: {missingInstanceCount}");
            GUILayout.Label($"Coordinator: {(actionCoordinator != null ? "Ready" : "Missing")}");
            GUILayout.Label($"Overlay: {(debugOverlayActive ? "On" : "Off")}  Active: {(string.IsNullOrEmpty(activeDebugBone) ? "-" : activeDebugBone)}");
            GUILayout.Label($"Pinned bones held: {heldRotations.Count}  Applied: {appliedIndices.Count}");

            var nextApply = GUILayout.Toggle(apply, "Apply Debug Overlay (live preview selected)");
            apply = nextApply;

            if (allowBoneMapRefresh)
            {
                if (GUILayout.Button("Refresh BoneMap"))
                    RefreshControlInstances();
            }
            else if (GUILayout.Button("Refresh UI"))
            {
                RefreshSelectedStatus();
            }

            DrawBoneList();
            DrawPinControls();
            DrawRotationControls();
            DrawExportControls();
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
            GUILayout.Label("Controlled Bone (* = pinned)");
            boneListScroll = GUILayout.BeginScrollView(boneListScroll, GUILayout.Height(140f));

            for (var i = 0; i < controlInstances.Count; i++)
            {
                var previousColor = GUI.backgroundColor;
                var isPinned = heldRotations.ContainsKey(i);
                if (i == selectedIndex)
                    GUI.backgroundColor = Color.cyan;
                else if (isPinned)
                    GUI.backgroundColor = Color.green;

                var label = isPinned ? "* " + controlInstances[i].DisplayName : controlInstances[i].DisplayName;
                if (GUILayout.Button(label))
                    selectedIndex = i;

                GUI.backgroundColor = previousColor;
            }

            GUILayout.EndScrollView();
        }

        private void DrawPinControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Pin Selected"))
                PinSelected();
            if (GUILayout.Button("Pin L/R Pair"))
                PinSelectedPair();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unpin Selected"))
                UnpinSelected();
            if (GUILayout.Button("Clear Pins"))
                ClearPins();
            GUILayout.EndHorizontal();
        }

        private void PinSelected()
        {
            if (!IsValidIndex(selectedIndex))
                return;

            heldRotations[selectedIndex] = controlInstances[selectedIndex].Entry.ClampRotation(rotation);
        }

        // Pin the selected bone and, for side bones, also pin its mirrored side with the
        // same semantic rotation (runtime mirrors R internally) for easy symmetric poses.
        private void PinSelectedPair()
        {
            if (!IsValidIndex(selectedIndex))
                return;

            var selected = controlInstances[selectedIndex];
            var clamped = selected.Entry.ClampRotation(rotation);
            heldRotations[selectedIndex] = clamped;

            if (selected.Side == BoneSide.None)
                return;

            var mirrorIndex = FindMirrorIndex(selected);
            if (mirrorIndex >= 0)
                heldRotations[mirrorIndex] = controlInstances[mirrorIndex].Entry.ClampRotation(clamped);
        }

        private int FindMirrorIndex(BoneMapInstance instance)
        {
            if (instance == null || instance.Side == BoneSide.None)
                return -1;

            var mirrorSide = instance.Side == BoneSide.L ? BoneSide.R : BoneSide.L;
            for (var i = 0; i < controlInstances.Count; i++)
            {
                var candidate = controlInstances[i];
                if (candidate.SemanticBone == instance.SemanticBone && candidate.Side == mirrorSide)
                    return i;
            }

            return -1;
        }

        private void UnpinSelected()
        {
            heldRotations.Remove(selectedIndex);
        }

        private void ClearPins()
        {
            heldRotations.Clear();
        }

        private void DrawRotationControls()
        {
            if (!IsValidIndex(selectedIndex))
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Selected"))
                ExportSelectedJson();
            if (GUILayout.Button("Export All Pinned (StagePlan)"))
                ExportPinnedStagePlan();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(exportedJson))
                GUILayout.TextArea(exportedJson, GUILayout.Height(120f));
        }

        private void ExportSelectedJson()
        {
            if (!IsValidIndex(selectedIndex))
                return;

            var instance = controlInstances[selectedIndex];
            var clampedRotation = instance.Entry.ClampRotation(rotation);
            exportedJson = BuildSingleBonePoseJson(instance, clampedRotation);
            GUIUtility.systemCopyBuffer = exportedJson;
            Debug.Log($"[VirtualPartner] Debug bone pose exported:\n{exportedJson}", this);
        }

        // Export every currently-held bone (plus the live-previewed selected bone when
        // Apply is on) as a single ready-to-play StagePlan, matching what is on screen.
        private void ExportPinnedStagePlan()
        {
            indexBuffer.Clear();
            foreach (var pair in heldRotations)
                indexBuffer.Add(pair.Key);
            if (apply && IsValidIndex(selectedIndex) && !heldRotations.ContainsKey(selectedIndex))
                indexBuffer.Add(selectedIndex);

            if (indexBuffer.Count == 0)
            {
                exportedJson = "No pinned bones to export. Pin bones first.";
                return;
            }

            indexBuffer.Sort();
            exportedJson = BuildStagePlanJson(indexBuffer);
            GUIUtility.systemCopyBuffer = exportedJson;
            Debug.Log($"[VirtualPartner] Debug pinned StagePlan exported:\n{exportedJson}", this);
        }

        private void RefreshControlInstances()
        {
            ReleaseAllApplied();
            heldRotations.Clear();

            missingInstanceCount = boneMapProfile.BuildControlInstances(boneRoot, controlInstances);
            semanticConfigCount = boneMapProfile.SemanticConfigCount;
            controlInstanceCount = controlInstances.Count;
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, controlInstances.Count - 1));
            lastSelectedIndex = -1;

            if (missingInstanceCount > 0)
                Debug.LogWarning($"[VirtualPartner] BoneMap resolved with {missingInstanceCount} missing debug bone instance(s).", this);
        }

        private bool ValidateReferences()
        {
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (actionCoordinator == null)
                return Fail("ActionCoordinator reference is missing.");

            return true;
        }

        private bool Fail(string message)
        {
            Debug.LogError($"[VirtualPartner] Bone debug panel failed: {message}", this);
            enabled = false;
            return false;
        }

        private static string BuildSingleBonePoseJson(BoneMapInstance instance, Vector3 semanticRotation)
        {
            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"type\": \"bonePose\",");
            builder.AppendLine("  \"bones\": [");
            AppendBoneEntry(builder, instance, semanticRotation, "    ", true);
            builder.AppendLine("  ]");
            builder.Append("}");
            return builder.ToString();
        }

        private string BuildStagePlanJson(List<int> indices)
        {
            var builder = new StringBuilder();
            builder.Append("{\"schemaVersion\":\"2.0\",\"type\":\"stagePlan\",\"metadata\":{\"intent\":\"debug_pose\",\"mood\":\"neutral\"},\"stages\":[{\"actions\":[{\"type\":\"bonePose\",\"duration\":")
                .Append(FormatFloat(Mathf.Max(0.1f, exportDuration)))
                .Append(",\"bones\":[");

            var wroteAny = false;
            for (var i = 0; i < indices.Count; i++)
            {
                var idx = indices[i];
                if (!IsValidIndex(idx))
                    continue;

                var instance = controlInstances[idx];
                var rot = instance.Entry.ClampRotation(GetRotationForIndex(idx));

                if (wroteAny)
                    builder.Append(',');
                AppendInlineBoneEntry(builder, instance, rot);
                wroteAny = true;
            }

            builder.Append("]}]}]}");
            return builder.ToString();
        }

        private static void AppendBoneEntry(StringBuilder builder, BoneMapInstance instance, Vector3 rotation, string indent, bool last)
        {
            builder.Append(indent).AppendLine("{");
            builder.Append(indent).Append("  \"bone\": \"").Append(instance.SemanticBone).AppendLine("\",");
            if (instance.Side != BoneSide.None)
                builder.Append(indent).Append("  \"side\": \"").Append(instance.Side).AppendLine("\",");
            builder.Append(indent)
                .Append("  \"rotation\": { \"x\": ").Append(FormatFloat(rotation.x))
                .Append(", \"y\": ").Append(FormatFloat(rotation.y))
                .Append(", \"z\": ").Append(FormatFloat(rotation.z))
                .AppendLine(" }");
            builder.Append(indent).AppendLine(last ? "}" : "},");
        }

        private static void AppendInlineBoneEntry(StringBuilder builder, BoneMapInstance instance, Vector3 rotation)
        {
            builder.Append("{\"bone\":\"").Append(instance.SemanticBone).Append('"');
            if (instance.Side != BoneSide.None)
                builder.Append(",\"side\":\"").Append(instance.Side).Append('"');
            builder.Append(",\"rotation\":{\"x\":").Append(FormatFloat(rotation.x))
                .Append(",\"y\":").Append(FormatFloat(rotation.y))
                .Append(",\"z\":").Append(FormatFloat(rotation.z))
                .Append("}}");
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
