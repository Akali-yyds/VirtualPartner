using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualPartnerRuntimeDebugPanel : MonoBehaviour
    {
        private enum DebugSection
        {
            Overview,
            Llm,
            Timeline,
            Fsm,
            Root,
            Bone
        }

        [Header("Runtime References")]
        [SerializeField] private LlmRelay llmRelay;
        [SerializeField] private TimelinePlayer timelinePlayer;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private MovementConstraintController movementConstraintController;
        [SerializeField] private ActionCoordinator actionCoordinator;

        [Header("Embedded Panels")]
        [SerializeField] private LlmInteractionDebugPanel llmPanel;
        [SerializeField] private TimelineDebugPanel timelinePanel;
        [SerializeField] private AutonomousBehaviorDebugPanel fsmPanel;
        [SerializeField] private RootLocomotionDebugPanel rootPanel;
        [SerializeField] private VirtualPartnerBoneDebugPanel bonePanel;

        [Header("Display")]
        [SerializeField] private bool hideLegacyStandalonePanels = true;
        [SerializeField] private bool minimized;
        [SerializeField] private DebugSection selectedSection;
        [SerializeField] private Rect windowRect = new Rect(20f, 20f, 780f, 620f);

        private Vector2 contentScroll;
        private Vector2 expandedWindowSize = new Vector2(780f, 620f);

        public void Configure(
            LlmRelay relay,
            TimelinePlayer player,
            AutonomousBehaviorScheduler scheduler,
            RootOrientationController orientationController,
            LocomotionActionExecutor locomotionExecutor,
            MovementConstraintController constraintController,
            ActionCoordinator coordinator,
            LlmInteractionDebugPanel llmDebugPanel,
            TimelineDebugPanel timelineDebugPanel,
            AutonomousBehaviorDebugPanel fsmDebugPanel,
            RootLocomotionDebugPanel rootDebugPanel,
            VirtualPartnerBoneDebugPanel boneDebugPanel)
        {
            llmRelay = relay;
            timelinePlayer = player;
            autonomousBehaviorScheduler = scheduler;
            rootOrientationController = orientationController;
            locomotionActionExecutor = locomotionExecutor;
            movementConstraintController = constraintController;
            actionCoordinator = coordinator;
            llmPanel = llmDebugPanel;
            timelinePanel = timelineDebugPanel;
            fsmPanel = fsmDebugPanel;
            rootPanel = rootDebugPanel;
            bonePanel = boneDebugPanel;
            ApplyLegacyPanelVisibility();
        }

        private void Start()
        {
            ApplyLegacyPanelVisibility();
        }

        private void OnValidate()
        {
            if (Application.isPlaying)
                ApplyLegacyPanelVisibility();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP Debug" : "VirtualPartner Runtime Debug");
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (minimized)
            {
                DrawCompactOverview();
                GUI.DragWindow();
                return;
            }

            GUILayout.BeginHorizontal();
            DrawSectionList();
            GUILayout.BeginVertical();
            contentScroll = GUILayout.BeginScrollView(contentScroll);
            DrawSelectedSection();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        private void DrawSectionList()
        {
            GUILayout.BeginVertical(GUILayout.Width(130f));
            DrawSectionButton(DebugSection.Overview, "Overview");
            DrawSectionButton(DebugSection.Llm, "LLM");
            DrawSectionButton(DebugSection.Timeline, "Timeline");
            DrawSectionButton(DebugSection.Fsm, "FSM");
            DrawSectionButton(DebugSection.Root, "Root");
            DrawSectionButton(DebugSection.Bone, "Bone");
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawSectionButton(DebugSection section, string label)
        {
            var previousColor = GUI.backgroundColor;
            if (selectedSection == section)
                GUI.backgroundColor = Color.cyan;

            if (GUILayout.Button(label, GUILayout.Height(30f)))
                selectedSection = section;

            GUI.backgroundColor = previousColor;
        }

        private void DrawSelectedSection()
        {
            switch (selectedSection)
            {
                case DebugSection.Llm:
                    DrawEmbeddedLlm();
                    break;
                case DebugSection.Timeline:
                    DrawEmbeddedTimeline();
                    break;
                case DebugSection.Fsm:
                    DrawEmbeddedFsm();
                    break;
                case DebugSection.Root:
                    DrawEmbeddedRoot();
                    break;
                case DebugSection.Bone:
                    DrawEmbeddedBone();
                    break;
                default:
                    DrawOverview();
                    break;
            }
        }

        private void DrawEmbeddedLlm()
        {
            if (llmPanel == null)
            {
                GUILayout.Label("LLM panel: Missing");
                return;
            }

            llmPanel.DrawEmbedded();
        }

        private void DrawEmbeddedTimeline()
        {
            if (timelinePanel == null)
            {
                GUILayout.Label("Timeline panel: Missing");
                return;
            }

            timelinePanel.DrawEmbedded();
        }

        private void DrawEmbeddedFsm()
        {
            if (fsmPanel == null)
            {
                GUILayout.Label("FSM panel: Missing");
                return;
            }

            fsmPanel.DrawEmbedded();
        }

        private void DrawEmbeddedRoot()
        {
            if (rootPanel == null)
            {
                GUILayout.Label("Root panel: Missing");
                return;
            }

            rootPanel.DrawEmbedded();
        }

        private void DrawEmbeddedBone()
        {
            if (bonePanel == null)
            {
                GUILayout.Label("Bone panel: Missing");
                return;
            }

            bonePanel.DrawEmbedded();
        }

        private void DrawCompactOverview()
        {
            GUILayout.Label(timelinePlayer != null && timelinePlayer.IsPlaying
                ? $"Timeline {FormatOwner(timelinePlayer.ActiveOwnerId)} {timelinePlayer.CurrentTime:0.00}s"
                : "Runtime Ready");
        }

        private void DrawOverview()
        {
            GUILayout.Label("Overview");
            DrawLlmOverview();
            GUILayout.Space(8f);
            DrawTimelineOverview();
            GUILayout.Space(8f);
            DrawFsmOverview();
            GUILayout.Space(8f);
            DrawRootOverview();
            GUILayout.Space(8f);
            DrawBoneOverview();
        }

        private void DrawLlmOverview()
        {
            GUILayout.Label("LLM");
            if (llmRelay == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {llmRelay.StatusText}");
            GUILayout.Label($"Config: {llmRelay.ConfigStatus}");
            GUILayout.Label($"Request: latest {llmRelay.LatestRequestId}  pending {(llmRelay.RequestPending ? llmRelay.PendingRequestId.ToString() : "-")}");
            GUILayout.Label($"LLM Timeline: {(llmRelay.IsLlmTimelinePlaying ? "Playing" : "No")}  Timeout: {llmRelay.InteractionTimeoutSeconds:0.#}s");
            if (!string.IsNullOrWhiteSpace(llmRelay.LastError))
                GUILayout.Label($"Last Error: {llmRelay.LastError}");
        }

        private void DrawTimelineOverview()
        {
            GUILayout.Label("Timeline");
            if (timelinePlayer == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {timelinePlayer.StatusText}  Owner: {FormatOwner(timelinePlayer.ActiveOwnerId)}");
            GUILayout.Label($"Time: {timelinePlayer.CurrentTime:0.00}s  Segment: {timelinePlayer.CurrentSegmentStatus}");
            GUILayout.Label($"Errors: {timelinePlayer.ErrorCount}  Warnings: {timelinePlayer.WarningCount}");
            if (!string.IsNullOrWhiteSpace(timelinePlayer.LastMessage))
                GUILayout.Label($"Last: {timelinePlayer.LastMessage}");
        }

        private void DrawFsmOverview()
        {
            GUILayout.Label("FSM");
            if (autonomousBehaviorScheduler == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Enabled: {autonomousBehaviorScheduler.SchedulerActive}  State: {autonomousBehaviorScheduler.State}");
            GUILayout.Label($"Wait: {autonomousBehaviorScheduler.WaitRemaining:0.0}s  Action: {autonomousBehaviorScheduler.CurrentActionName}");
            GUILayout.Label($"Interaction: {autonomousBehaviorScheduler.IsInUserInteraction}  {autonomousBehaviorScheduler.InteractionRemaining:0.0}s");
            if (!string.IsNullOrWhiteSpace(autonomousBehaviorScheduler.LastMessage))
                GUILayout.Label($"Last: {autonomousBehaviorScheduler.LastMessage}");
        }

        private void DrawRootOverview()
        {
            GUILayout.Label("Root");
            var root = rootOrientationController != null ? rootOrientationController.Root : null;
            if (root == null)
            {
                GUILayout.Label("Root: Missing");
            }
            else
            {
                GUILayout.Label($"Pos: {root.position.x:0.00}, {root.position.y:0.00}, {root.position.z:0.00}  Yaw: {root.eulerAngles.y:0.0}");
            }

            if (rootOrientationController != null)
                GUILayout.Label($"Turn: {(rootOrientationController.IsTurning ? rootOrientationController.ActiveTarget : "-")}  Interaction: {rootOrientationController.IsInUserInteraction}");

            if (locomotionActionExecutor != null)
                GUILayout.Label($"Move: {(locomotionActionExecutor.IsActive ? locomotionActionExecutor.ActiveMode : "-")}  {locomotionActionExecutor.Elapsed:0.00}/{locomotionActionExecutor.Duration:0.00}");

            if (movementConstraintController != null)
                GUILayout.Label($"Constraint: {(movementConstraintController.IsConstraintActive ? "Active" : "Disabled")}  {(movementConstraintController.LastResult ? "Allowed" : "Blocked")}  {movementConstraintController.LastReason}");
        }

        private void DrawBoneOverview()
        {
            GUILayout.Label("Bone Owners");
            if (actionCoordinator == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Debug: {actionCoordinator.DebugOwnedBoneCount}  Timeline: {actionCoordinator.TimelineBonePoseOwnedBoneCount}");
            GUILayout.Label($"Locomotion: {actionCoordinator.LocomotionOwnedBoneCount}  Preset: {actionCoordinator.PresetAnimationOwnedBoneCount}");
            GUILayout.Label($"Transitions: {actionCoordinator.ActiveTransitionCount}  Active: {FormatActiveBone()}");
        }

        private string FormatActiveBone()
        {
            if (actionCoordinator == null || string.IsNullOrWhiteSpace(actionCoordinator.ActiveBoneName))
                return "-";

            return $"{actionCoordinator.ActiveBoneName} ({actionCoordinator.ActiveOwner})";
        }

        private void ApplyLegacyPanelVisibility()
        {
            if (!hideLegacyStandalonePanels)
                return;

            if (llmPanel != null)
                llmPanel.SetStandaloneVisible(false);
            if (timelinePanel != null)
                timelinePanel.SetStandaloneVisible(false);
            if (fsmPanel != null)
                fsmPanel.SetStandaloneVisible(false);
            if (rootPanel != null)
                rootPanel.SetStandaloneVisible(false);
            if (bonePanel != null)
                bonePanel.SetStandaloneVisible(false);
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 260f;
                windowRect.height = 78f;
                return;
            }

            windowRect.width = Mathf.Max(560f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(420f, expandedWindowSize.y);
        }

        private static string FormatOwner(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? "External" : ownerId;
        }
    }
}
