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
            StagePlan,
            Fsm,
            Root,
            Bone,
            ExpressionMouth
        }

        [Header("Runtime References")]
        [SerializeField] private LlmRelay llmRelay;
        [SerializeField] private StagePlanPlayer stagePlanPlayer;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private MovementConstraintController movementConstraintController;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private MouthTextureController mouthTextureController;
        [SerializeField] private ExpressionActionExecutor expressionActionExecutor;
        [SerializeField] private SpeechMouthDriver speechMouthDriver;

        [Header("Embedded Panels")]
        [SerializeField] private StagePlanDebugPanel stagePlanPanel;
        [SerializeField] private LlmInteractionDebugPanel llmPanel;
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
        private int debugMouthIndex;
        private string mouthDebugMessage;

        private void Awake()
        {
            ResolveEmbeddedPanels();
        }

        public void Configure(
            LlmRelay relay,
            StagePlanPlayer player,
            AutonomousBehaviorScheduler scheduler,
            RootOrientationController orientationController,
            LocomotionActionExecutor locomotionExecutor,
            MovementConstraintController constraintController,
            ActionCoordinator coordinator,
            StagePlanDebugPanel stagePlanDebugPanel,
            LlmInteractionDebugPanel llmDebugPanel,
            AutonomousBehaviorDebugPanel fsmDebugPanel,
            RootLocomotionDebugPanel rootDebugPanel,
            VirtualPartnerBoneDebugPanel boneDebugPanel,
            MouthTextureController mouthController,
            ExpressionActionExecutor expressionExecutor,
            SpeechMouthDriver mouthDriver)
        {
            llmRelay = relay;
            stagePlanPlayer = player;
            autonomousBehaviorScheduler = scheduler;
            rootOrientationController = orientationController;
            locomotionActionExecutor = locomotionExecutor;
            movementConstraintController = constraintController;
            actionCoordinator = coordinator;
            stagePlanPanel = stagePlanDebugPanel;
            llmPanel = llmDebugPanel;
            fsmPanel = fsmDebugPanel;
            rootPanel = rootDebugPanel;
            bonePanel = boneDebugPanel;
            mouthTextureController = mouthController;
            expressionActionExecutor = expressionExecutor;
            speechMouthDriver = mouthDriver;
            ApplyLegacyPanelVisibility();
        }

        private void Start()
        {
            ResolveEmbeddedPanels();
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
            DrawSectionButton(DebugSection.StagePlan, "StagePlan");
            DrawSectionButton(DebugSection.Fsm, "FSM");
            DrawSectionButton(DebugSection.Root, "Root");
            DrawSectionButton(DebugSection.Bone, "Bone");
            DrawSectionButton(DebugSection.ExpressionMouth, "Expr/Mouth");
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
                case DebugSection.StagePlan:
                    DrawEmbeddedStagePlan();
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
                case DebugSection.ExpressionMouth:
                    DrawExpressionMouth();
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

        private void DrawEmbeddedStagePlan()
        {
            if (stagePlanPanel == null)
            {
                DrawStagePlanOverview();
                return;
            }

            stagePlanPanel.DrawEmbedded();
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
            GUILayout.Label(stagePlanPlayer != null && stagePlanPlayer.IsPlaying
                ? $"StagePlan {FormatOwner(stagePlanPlayer.ActiveOwnerId)} {stagePlanPlayer.CurrentStageStatus}"
                : "Runtime Ready");
        }

        private void DrawOverview()
        {
            GUILayout.Label("Overview");
            DrawLlmOverview();
            GUILayout.Space(8f);
            DrawStagePlanOverview();
            GUILayout.Space(8f);
            DrawFsmOverview();
            GUILayout.Space(8f);
            DrawRootOverview();
            GUILayout.Space(8f);
            DrawBoneOverview();
            GUILayout.Space(8f);
            DrawMouthOverview();
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
            GUILayout.Label($"LLM StagePlan: {(llmRelay.IsLlmStagePlanPlaying ? "Playing" : "No")}  Timeout: {llmRelay.InteractionTimeoutSeconds:0.#}s");
            if (!string.IsNullOrWhiteSpace(llmRelay.LastError))
                GUILayout.Label($"Last Error: {llmRelay.LastError}");
        }

        private void DrawStagePlanOverview()
        {
            GUILayout.Label("StagePlan");
            if (stagePlanPlayer == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {stagePlanPlayer.StatusText}  Owner: {FormatOwner(stagePlanPlayer.ActiveOwnerId)}");
            GUILayout.Label($"Stage: {stagePlanPlayer.CurrentStageStatus}  Actions: {stagePlanPlayer.TerminalActionCount}/{stagePlanPlayer.ActiveActionCount}");
            GUILayout.Label($"Errors: {stagePlanPlayer.ErrorCount}  Warnings: {stagePlanPlayer.WarningCount}");
            GUILayout.Label(
                $"Results C/F/I/S/O: {stagePlanPlayer.CompletedCount}/{stagePlanPlayer.FailedCount}/{stagePlanPlayer.InterruptedCount}/{stagePlanPlayer.SkippedCount}/{stagePlanPlayer.OwnershipDeniedCount}");
            if (!string.IsNullOrWhiteSpace(stagePlanPlayer.LastMessage))
                GUILayout.Label($"Last: {stagePlanPlayer.LastMessage}");
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

            GUILayout.Label($"Debug: {actionCoordinator.DebugOwnedBoneCount}  StagePlan: {actionCoordinator.StagePlanBonePoseOwnedBoneCount}");
            GUILayout.Label($"Locomotion: {actionCoordinator.LocomotionOwnedBoneCount}  Preset: {actionCoordinator.PresetAnimationOwnedBoneCount}");
            GUILayout.Label($"Transitions: {actionCoordinator.ActiveTransitionCount}  Active: {FormatActiveBone()}");
        }

        private void DrawMouthOverview()
        {
            GUILayout.Label("Expression/Mouth");
            if (mouthTextureController == null)
            {
                GUILayout.Label("Mouth: Missing");
                return;
            }

            GUILayout.Label($"Mouth: {mouthTextureController.CurrentSource} index={mouthTextureController.CurrentMouthIndex}");
            if (expressionActionExecutor != null)
                GUILayout.Label($"Expression: {expressionActionExecutor.CurrentExpression} pose={expressionActionExecutor.CurrentMouthPose}");
            if (speechMouthDriver != null)
                GUILayout.Label($"Speech Mouth: {(speechMouthDriver.Active ? "Active" : "Idle")} {speechMouthDriver.Elapsed:0.00}/{speechMouthDriver.Duration:0.00}s pose={speechMouthDriver.CurrentPoseSet}");
        }

        private void DrawExpressionMouth()
        {
            GUILayout.Label("Expression/Mouth");
            DrawMouthOverview();
            GUILayout.Space(8f);

            if (mouthTextureController == null)
                return;

            GUILayout.Label("Debug Mouth Override");
            debugMouthIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(debugMouthIndex, -1, 63, GUILayout.Width(300f)));
            GUILayout.Label($"Index: {debugMouthIndex}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Debug", GUILayout.Width(120f)))
            {
                mouthTextureController.SetDebugMouthIndex(debugMouthIndex);
                mouthDebugMessage = mouthTextureController.LastMessage;
            }
            if (GUILayout.Button("Release Debug", GUILayout.Width(120f)))
            {
                mouthTextureController.ReleaseDebugOverride();
                mouthDebugMessage = mouthTextureController.LastMessage;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Expression Test");
            DrawExpressionButton("neutral");
            DrawExpressionButton("smile");
            DrawExpressionButton("thinking");
            DrawExpressionButton("surprised");
            DrawExpressionButton("embarrassed");
            if (GUILayout.Button("Clear Expression", GUILayout.Width(140f)) && expressionActionExecutor != null)
            {
                expressionActionExecutor.ClearExpression();
                mouthDebugMessage = expressionActionExecutor.LastMessage;
            }

            GUILayout.Space(8f);
            if (speechMouthDriver != null)
            {
                GUILayout.Label($"Fallback Duration: min={speechMouthDriver.MinDuration:0.##}s max={speechMouthDriver.MaxDuration:0.##}s sec/char={speechMouthDriver.SecondsPerCharacter:0.###}");
                GUILayout.Label($"Random Open Mouth: {speechMouthDriver.RandomizeOpenMouthIndex}");
                GUILayout.Label($"Speech Driver: {speechMouthDriver.LastMessage}");
            }

            if (expressionActionExecutor != null)
                GUILayout.Label($"Expression Executor: {expressionActionExecutor.LastMessage}");
            GUILayout.Label($"Mouth Controller: {mouthTextureController.LastMessage}");
            if (!string.IsNullOrWhiteSpace(mouthDebugMessage))
                GUILayout.Label($"Last Debug: {mouthDebugMessage}");
        }

        private void DrawExpressionButton(string expressionName)
        {
            if (!GUILayout.Button(expressionName, GUILayout.Width(140f)))
                return;

            if (expressionActionExecutor == null)
            {
                mouthDebugMessage = "ExpressionActionExecutor missing.";
                return;
            }

            if (expressionActionExecutor.StartExpression(expressionName, 0.3f, out var failureReason))
                mouthDebugMessage = expressionActionExecutor.LastMessage;
            else
                mouthDebugMessage = failureReason;
        }

        private string FormatActiveBone()
        {
            if (actionCoordinator == null || string.IsNullOrWhiteSpace(actionCoordinator.ActiveBoneName))
                return "-";

            return $"{actionCoordinator.ActiveBoneName} ({actionCoordinator.ActiveOwner})";
        }

        private void ApplyLegacyPanelVisibility()
        {
            ResolveEmbeddedPanels();

            if (!hideLegacyStandalonePanels)
                return;

            if (llmPanel != null)
                llmPanel.SetStandaloneVisible(false);
            if (stagePlanPanel != null)
                stagePlanPanel.SetStandaloneVisible(false);
            if (fsmPanel != null)
                fsmPanel.SetStandaloneVisible(false);
            if (rootPanel != null)
                rootPanel.SetStandaloneVisible(false);
            if (bonePanel != null)
                bonePanel.SetStandaloneVisible(false);
        }

        private void ResolveEmbeddedPanels()
        {
            if (stagePlanPanel == null)
                stagePlanPanel = GetComponent<StagePlanDebugPanel>();
            if (llmPanel == null)
                llmPanel = GetComponent<LlmInteractionDebugPanel>();
            if (fsmPanel == null)
                fsmPanel = GetComponent<AutonomousBehaviorDebugPanel>();
            if (rootPanel == null)
                rootPanel = GetComponent<RootLocomotionDebugPanel>();
            if (bonePanel == null)
                bonePanel = GetComponent<VirtualPartnerBoneDebugPanel>();
            if (mouthTextureController == null)
                mouthTextureController = GetComponent<MouthTextureController>();
            if (expressionActionExecutor == null)
                expressionActionExecutor = GetComponent<ExpressionActionExecutor>();
            if (speechMouthDriver == null)
                speechMouthDriver = GetComponent<SpeechMouthDriver>();
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
