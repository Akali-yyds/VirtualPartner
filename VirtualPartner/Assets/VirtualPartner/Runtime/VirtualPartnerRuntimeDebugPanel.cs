using System.Collections.Generic;
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
            Momotalk,
            Tts,
            Asr,
            Memory,
            Character,
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
        [SerializeField] private TtsManager ttsManager;
        [SerializeField] private AsrManager asrManager;
        [SerializeField] private MemorySystem memorySystem;
        [SerializeField] private MomotalkUIManager momotalkUIManager;

        [Header("Embedded Panels")]
        [SerializeField] private StagePlanDebugPanel stagePlanPanel;
        [SerializeField] private LlmInteractionDebugPanel llmPanel;
        [SerializeField] private AutonomousBehaviorDebugPanel fsmPanel;
        [SerializeField] private RootLocomotionDebugPanel rootPanel;
        [SerializeField] private VirtualPartnerBoneDebugPanel bonePanel;

        [Header("Display")]
        [SerializeField] private bool hideLegacyStandalonePanels = true;
        [SerializeField] private bool visible;
        [SerializeField] private bool minimized;
        [SerializeField] private DebugSection selectedSection;
        [SerializeField] private Rect windowRect = new Rect(20f, 20f, 780f, 620f);

        private Vector2 contentScroll;
        private Vector2 expandedWindowSize = new Vector2(780f, 620f);
        private int debugMouthIndex;
        private string mouthDebugMessage;
        private string ttsDebugText = "Teacher, we can continue now.";
        private string ttsDebugMessage;
        private string asrDebugMessage;
        private Vector2 memoryRawScroll;
        private MomotalkConversationController momotalkConversationController;
        private readonly List<CharacterRuntimeContext> debugCharacterContexts = new List<CharacterRuntimeContext>();

        public bool Visible => visible;

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public void ToggleVisible()
        {
            SetVisible(!visible);
        }

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
            SpeechMouthDriver mouthDriver,
            TtsManager tts,
            AsrManager asr,
            MemorySystem memory,
            MomotalkUIManager momotalk)
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
            ttsManager = tts;
            asrManager = asr;
            memorySystem = memory;
            momotalkUIManager = momotalk;
            momotalkConversationController = momotalkUIManager != null
                ? momotalkUIManager.GetComponent<MomotalkConversationController>()
                : null;
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
            if (!Application.isPlaying || !visible)
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
            DrawSectionButton(DebugSection.Momotalk, "Momotalk");
            DrawSectionButton(DebugSection.Tts, "TTS");
            DrawSectionButton(DebugSection.Asr, "ASR");
            DrawSectionButton(DebugSection.Memory, "Memory");
            DrawSectionButton(DebugSection.Character, "Character");
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
                case DebugSection.Momotalk:
                    DrawMomotalk();
                    break;
                case DebugSection.Tts:
                    DrawTts();
                    break;
                case DebugSection.Asr:
                    DrawAsr();
                    break;
                case DebugSection.Memory:
                    DrawMemory();
                    break;
                case DebugSection.Character:
                    DrawCharacter();
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
            DrawMomotalkOverview();
            GUILayout.Space(8f);
            DrawTtsOverview();
            GUILayout.Space(8f);
            DrawAsrOverview();
            GUILayout.Space(8f);
            DrawMemoryOverview();
            GUILayout.Space(8f);
            DrawCharacterOverview();
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

        private void DrawMomotalkOverview()
        {
            ResolveMomotalkReferences();

            GUILayout.Label("Momotalk");
            if (momotalkUIManager == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Open: {momotalkUIManager.IsOpen}  Loading: {momotalkUIManager.LoadingVisible}  Page: {momotalkUIManager.VisiblePageName}");
            GUILayout.Label($"Selected: {FormatCharacterLabel(momotalkUIManager.SelectedCharacterId, momotalkUIManager.SelectedCharacterName)}");
            if (momotalkConversationController != null)
            {
                GUILayout.Label($"Conversation: {FormatEmpty(momotalkConversationController.CurrentCharacterId)}  Unread: {momotalkConversationController.TotalUnreadCount}");
                GUILayout.Label($"Requests active: {momotalkConversationController.ActiveRequestCount}  pending: {momotalkConversationController.PendingRequestCount}  playing: {momotalkConversationController.PlayingRequestCount}");
            }
        }

        private void DrawTtsOverview()
        {
            GUILayout.Label("TTS");
            if (ttsManager == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {ttsManager.StatusText}  Mode: {ttsManager.ModeText}  Audio: {(ttsManager.AudioSourcePlaying ? "Playing" : "Stopped")}");
            GUILayout.Label($"Provider: {ttsManager.CurrentProvider}  Voice: {ttsManager.CurrentVoiceId}  Emotion: {ttsManager.CurrentEmotion}");
            GUILayout.Label($"Duration: {ttsManager.Elapsed:0.00}/{ttsManager.Duration:0.00}s  Cached: {ttsManager.Cached}");
            GUILayout.Label($"Health: {ttsManager.HealthStatusText}");
            if (!string.IsNullOrWhiteSpace(ttsManager.LatestError))
                GUILayout.Label($"Latest Error: {ttsManager.LatestError}");
        }

        private void DrawAsrOverview()
        {
            GUILayout.Label("ASR");
            if (asrManager == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {asrManager.Status}  Result: {asrManager.ResultMode}  Provider: {asrManager.ProviderMode}  Active: {asrManager.Active}");
            GUILayout.Label($"Unity Session: {asrManager.UnitySessionToken}  Server Session: {asrManager.ServerSessionId}");
            GUILayout.Label($"Service: {asrManager.ServiceUrl}  Poll: {asrManager.StatusPollIntervalSeconds:0.00}s  Timeout: {asrManager.AsrSessionTimeoutSeconds:0.#}s");
            GUILayout.Label($"Elapsed: {asrManager.Elapsed:0.00}s  Text: {asrManager.LatestText}");
            GUILayout.Label($"Input RMS: latest={asrManager.LatestRms:0.0000} peak={asrManager.PeakRms:0.0000} speech={asrManager.SpeechDetected}");
            GUILayout.Label($"Health: {asrManager.HealthStatusText}");
            GUILayout.Label($"Runtime: {asrManager.RuntimeStatusText}");
            GUILayout.Label($"Audio Input: {asrManager.AudioInputStatusText}");
            if (!string.IsNullOrWhiteSpace(asrManager.LatestError))
                GUILayout.Label($"Latest Error: {asrManager.LatestError}");
            if (!string.IsNullOrWhiteSpace(asrManager.LastMessage))
                GUILayout.Label($"Last: {asrManager.LastMessage}");
        }

        private void DrawMemoryOverview()
        {
            GUILayout.Label("Memory");
            if (memorySystem == null)
            {
                GUILayout.Label("Missing");
                return;
            }

            GUILayout.Label($"Status: {memorySystem.StatusText}  Processing: {memorySystem.Processing}  Queue: {memorySystem.QueueCount}");
            GUILayout.Label($"Prompt chars: {memorySystem.LoadedMemoryPromptChars}/{memorySystem.MaxMemoryPromptChars}  Truncated: {memorySystem.MemoryPromptTruncated}");
            if (!string.IsNullOrWhiteSpace(memorySystem.LastMessage))
                GUILayout.Label($"Last: {memorySystem.LastMessage}");
        }

        private void DrawCharacterOverview()
        {
            GUILayout.Label("Character");
            CharacterRegistry.GetRegisteredContexts(debugCharacterContexts);
            GUILayout.Label($"Registered: {debugCharacterContexts.Count}");
            if (debugCharacterContexts.Count > 0)
                GUILayout.Label($"First: {FormatContextLabel(debugCharacterContexts[0])}");
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
                GUILayout.Label($"Speech Mouth: {(speechMouthDriver.Active ? "Active" : "Idle")} {(speechMouthDriver.AudioRmsMode ? "RMS" : "Text")} {speechMouthDriver.Elapsed:0.00}/{speechMouthDriver.Duration:0.00}s pose={speechMouthDriver.CurrentPoseSet}");
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
                GUILayout.Label($"RMS: {speechMouthDriver.CurrentRms:0.000} openness={speechMouthDriver.SmoothedOpenness:0.000}");
                GUILayout.Label($"Speech Driver: {speechMouthDriver.LastMessage}");
            }

            if (expressionActionExecutor != null)
                GUILayout.Label($"Expression Executor: {expressionActionExecutor.LastMessage}");
            GUILayout.Label($"Mouth Controller: {mouthTextureController.LastMessage}");
            if (!string.IsNullOrWhiteSpace(mouthDebugMessage))
                GUILayout.Label($"Last Debug: {mouthDebugMessage}");
        }

        private void DrawTts()
        {
            GUILayout.Label("TTS");
            DrawTtsOverview();
            GUILayout.Space(8f);

            if (ttsManager == null)
                return;

            var failMode = GUILayout.Toggle(ttsManager.ForceMockFailure, "Force Mock Failure");
            if (failMode != ttsManager.ForceMockFailure)
                ttsManager.SetMockFailureMode(failMode);

            var use3D = GUILayout.Toggle(ttsManager.Use3DAudio, "Use 3D Audio");
            if (use3D != ttsManager.Use3DAudio)
                ttsManager.SetUse3DAudio(use3D);

            GUILayout.Label($"Service: {ttsManager.ServiceUrl}  Timeout: {ttsManager.RequestTimeoutSeconds}s  Health Timeout: {ttsManager.HealthTimeoutSeconds}s");
            GUILayout.Label($"Session: {ttsManager.CurrentSessionId}  MockTTS Enabled: {ttsManager.MockTtsEnabled}");
            GUILayout.Label($"Text: {ttsManager.CurrentTextSummary}");
            GUILayout.Label($"AudioSource: {ttsManager.AudioSourceState}");
            GUILayout.Label($"Cache Key: {ttsManager.CacheKey}");
            GUILayout.Label($"Cache Path: {ttsManager.CachePath}");
            GUILayout.Label($"Cached: {ttsManager.Cached}");
            GUILayout.Label($"Provider Version: {ttsManager.ProviderVersion}");
            GUILayout.Label($"Voice Hashes: ref={ttsManager.ReferenceAudioHash} prompt={ttsManager.PromptTextHash}");
            GUILayout.Label($"Lang: prompt={ttsManager.PromptLang} text={ttsManager.TextLang}");
            GUILayout.Label($"Stream: active={ttsManager.StreamingPlayback} paused={ttsManager.StreamingPausedForBuffer} underruns={ttsManager.StreamingUnderrunCount} mode={ttsManager.StreamingModeInUse} sr={ttsManager.StreamingSampleRate} buffered={ttsManager.StreamingBufferedSeconds:0.00}s written={ttsManager.StreamingWrittenSeconds:0.00}s bytes={ttsManager.StreamingReceivedBytes}");
            GUILayout.Label($"Last: {ttsManager.LastMessage}");

            GUILayout.Space(8f);
            GUILayout.Label("Debug Test Text");
            ttsDebugText = GUILayout.TextField(ttsDebugText);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Health Check", GUILayout.Width(120f)))
            {
                ttsManager.RequestHealthCheck();
                ttsDebugMessage = "Health check requested.";
            }
            if (GUILayout.Button("Real TTS Test", GUILayout.Width(130f)))
                StartDebugTts(false);
            if (GUILayout.Button("Warmup Test", GUILayout.Width(120f)))
                StartDebugTts(false);
            if (GUILayout.Button("Mock Failure Test", GUILayout.Width(140f)))
                StartDebugTts(true);
            if (GUILayout.Button("Stop TTS", GUILayout.Width(100f)))
            {
                ttsManager.StopSpeech("Stopped from TTS debug panel.");
                ttsDebugMessage = ttsManager.LastMessage;
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(ttsDebugMessage))
                GUILayout.Label($"Debug: {ttsDebugMessage}");
        }

        private void DrawAsr()
        {
            GUILayout.Label("ASR");
            DrawAsrOverview();
            GUILayout.Space(8f);

            if (asrManager == null)
                return;

            var useMock = GUILayout.Toggle(asrManager.ProviderMode == AsrProviderMode.Mock, "Use MockASR");
            var providerMode = useMock ? AsrProviderMode.Mock : AsrProviderMode.RealService;
            if (providerMode != asrManager.ProviderMode)
                asrManager.SetProviderMode(providerMode);

            var autoSend = GUILayout.Toggle(asrManager.ResultMode == AsrResultMode.AutoSendToLlm, "AutoSendToLlm");
            var targetMode = autoSend ? AsrResultMode.AutoSendToLlm : AsrResultMode.FillInputOnly;
            if (targetMode != asrManager.ResultMode)
                asrManager.SetResultMode(targetMode);

            var unavailable = GUILayout.Toggle(asrManager.AsrUnavailable, "ASR Unavailable");
            if (unavailable != asrManager.AsrUnavailable)
                asrManager.SetUnavailable(unavailable);

            var failMode = GUILayout.Toggle(asrManager.ForceMockFailure, "Force Mock Failure");
            if (failMode != asrManager.ForceMockFailure)
                asrManager.SetMockFailureMode(failMode);

            GUILayout.Label("Mock Text");
            var mockText = GUILayout.TextField(asrManager.MockText);
            if (mockText != asrManager.MockText)
                asrManager.SetMockText(mockText);

            GUILayout.Space(6f);
            GUILayout.Label($"Service: {asrManager.ServiceUrl}");
            GUILayout.Label($"Health: {asrManager.HealthStatusText}");
            GUILayout.Label($"Runtime: {asrManager.RuntimeStatusText}");
            GUILayout.Label($"Engine: {asrManager.EngineStatusText}");
            GUILayout.Label($"Model: {asrManager.ModelStatusText}");
            GUILayout.Label($"VAD: {asrManager.VadStatusText}");
            GUILayout.Label($"Mic: {asrManager.MicrophoneStatusText}");
            GUILayout.Label($"Audio Input: {asrManager.AudioInputStatusText}");
            GUILayout.Label($"Remote Status: {asrManager.ServiceStatusText}");
            GUILayout.Label($"Input RMS: latest={asrManager.LatestRms:0.0000} peak={asrManager.PeakRms:0.0000} speech={asrManager.SpeechDetected}");
            GUILayout.Label($"Unity Session: {asrManager.UnitySessionToken}");
            GUILayout.Label($"Server Session: {asrManager.ServerSessionId}");
            GUILayout.Label($"Durations: listening={asrManager.ListeningSeconds:0.00}s recognizing={asrManager.RecognizingSeconds:0.00}s");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Health Check", GUILayout.Width(120f)))
            {
                asrManager.RequestHealthCheck();
                asrDebugMessage = "ASR health check requested.";
            }
            if (GUILayout.Button("Start Real", GUILayout.Width(120f)))
            {
                if (asrManager.StartRealRecognition(out var failureReason))
                    asrDebugMessage = asrManager.LastMessage;
                else
                    asrDebugMessage = failureReason;
            }
            if (GUILayout.Button("Start Mock", GUILayout.Width(120f)))
            {
                if (asrManager.StartMockRecognition(out var failureReason))
                    asrDebugMessage = asrManager.LastMessage;
                else
                    asrDebugMessage = failureReason;
            }
            if (GUILayout.Button("Cancel", GUILayout.Width(100f)))
            {
                asrManager.CancelRecognition();
                asrDebugMessage = asrManager.LastMessage;
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(asrDebugMessage))
                GUILayout.Label($"Debug: {asrDebugMessage}");
        }

        private void DrawMemory()
        {
            GUILayout.Label("Memory");
            DrawMemoryOverview();
            GUILayout.Space(8f);

            if (memorySystem == null)
                return;

            GUILayout.Label($"Config: {memorySystem.ConfigStatus}");
            GUILayout.Label($"Config Path: {memorySystem.ConfigPath}");
            GUILayout.Label($"Memory Folder: {memorySystem.MemoryRootPath}");
            GUILayout.Label($"Latest Decision: {memorySystem.LatestDecision}");
            GUILayout.Label($"Latest Write Path: {memorySystem.LatestWritePath}");
            GUILayout.Label($"Parse Error: {memorySystem.LatestParseError}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Memory", GUILayout.Width(130f)))
                memorySystem.ReloadMemory();
            if (GUILayout.Button("Judge Last Turn", GUILayout.Width(140f)))
                memorySystem.QueueLatestTurnForDebug();
            if (GUILayout.Button("Open Memory Folder", GUILayout.Width(160f)))
                memorySystem.OpenMemoryFolder();
            if (GUILayout.Button("Clear Latest Decision", GUILayout.Width(170f)))
                memorySystem.ClearLatestDecision();
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Latest Raw MemoryJudge Response");
            memoryRawScroll = GUILayout.BeginScrollView(memoryRawScroll, GUILayout.Height(160f));
            GUILayout.TextArea(memorySystem.LatestRawMemoryJudgeResponse);
            GUILayout.EndScrollView();
        }

        private void DrawMomotalk()
        {
            GUILayout.Label("Momotalk");
            DrawMomotalkOverview();
            GUILayout.Space(8f);

            if (momotalkUIManager == null)
                return;

            GUILayout.Label($"Last Page: {momotalkUIManager.LastPageName}");
            GUILayout.Label($"Has Selected Conversation: {momotalkUIManager.HasSelectedConversation}");
            GUILayout.Label($"LLM History Context Messages: {momotalkUIManager.LlmHistoryContextMessageCount}");
            GUILayout.Label($"Show Replaced System Message: {momotalkUIManager.ShowReplacedSystemMessage}");

            if (momotalkConversationController != null)
            {
                GUILayout.Label($"Current Conversation: {FormatEmpty(momotalkConversationController.CurrentCharacterId)}");
                GUILayout.Label($"Any Unread: {momotalkConversationController.HasAnyUnread()}  Total Unread: {momotalkConversationController.TotalUnreadCount}");
                GUILayout.Label($"History Path: {FormatEmpty(momotalkConversationController.LastHistoryPath)}");
            }
            else
            {
                GUILayout.Label("Conversation Controller: Missing");
            }

            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open", GUILayout.Width(90f)))
                momotalkUIManager.Open();
            if (GUILayout.Button("Close", GUILayout.Width(90f)))
                momotalkUIManager.Close();
            if (GUILayout.Button("Show Contacts", GUILayout.Width(130f)))
                momotalkUIManager.ShowContactList();

            var previousGuiEnabled = GUI.enabled;
            GUI.enabled = momotalkConversationController != null;
            if (GUILayout.Button("Open History Folder", GUILayout.Width(170f)))
                momotalkConversationController.OpenHistoryFolder();
            GUI.enabled = previousGuiEnabled;
            GUILayout.EndHorizontal();
        }

        private void DrawCharacter()
        {
            GUILayout.Label("Character");
            DrawCharacterOverview();
            GUILayout.Space(8f);
            ResolveMomotalkReferences();

            if (momotalkUIManager != null)
                GUILayout.Label($"Momotalk Selected: {FormatCharacterLabel(momotalkUIManager.SelectedCharacterId, momotalkUIManager.SelectedCharacterName)}");
            if (momotalkConversationController != null)
                GUILayout.Label($"Momotalk Conversation: {FormatEmpty(momotalkConversationController.CurrentCharacterId)}");

            CharacterRegistry.GetRegisteredContexts(debugCharacterContexts);
            if (debugCharacterContexts.Count == 0)
            {
                GUILayout.Label("No registered character.");
                return;
            }

            for (var i = 0; i < debugCharacterContexts.Count; i++)
            {
                var context = debugCharacterContexts[i];
                var profile = context != null ? context.Profile : null;
                GUILayout.Space(8f);
                GUILayout.Label($"[{i}] {FormatContextLabel(context)}");
                GUILayout.Label($"Profile Asset: {FormatObjectName(profile)}  Status: {FormatEmpty(profile != null ? profile.MomotalkStatus : string.Empty)}");
                GUILayout.Label($"Profile Links: BoneMap={FormatPresence(profile != null ? profile.BoneMapProfile : null)}  Preset={FormatPresence(profile != null ? profile.PresetAnimationProfile : null)}  Locomotion={FormatPresence(profile != null ? profile.LocomotionProfile : null)}  FSM={FormatPresence(profile != null ? profile.FsmProfile : null)}  Voice={FormatPresence(profile != null ? profile.VoiceProfile : null)}");
                GUILayout.Label($"Runtime Root: {FormatObjectName(context != null ? context.RuntimeRoot : null)}");
                GUILayout.Label($"Runtime: StagePlan={FormatPresence(context != null ? context.StagePlanPlayer : null)}  Action={FormatPresence(context != null ? context.ActionCoordinator : null)}  Avatar={FormatPresence(context != null ? context.AvatarPoseApplier : null)}  Root={FormatPresence(context != null ? context.RootOrientationController : null)}");
                GUILayout.Label($"Runtime: Locomotion={FormatPresence(context != null ? context.LocomotionActionExecutor : null)}  FSM={FormatPresence(context != null ? context.AutonomousBehaviorScheduler : null)}  Bubble={FormatPresence(context != null ? context.SpeechBubbleView : null)}");
                GUILayout.Label($"Face/Voice: Mouth={FormatPresence(context != null ? context.MouthTextureController : null)}  Expression={FormatPresence(context != null ? context.ExpressionActionExecutor : null)}  SpeechMouth={FormatPresence(context != null ? context.SpeechMouthDriver : null)}");
            }
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

        private void StartDebugTts(bool fail)
        {
            if (ttsManager == null)
            {
                ttsDebugMessage = "TtsManager missing.";
                return;
            }

            ttsManager.SetMockFailureMode(fail);
            var action = new StagePlanActionDto
            {
                type = "speech",
                text = string.IsNullOrWhiteSpace(ttsDebugText) ? "Mock TTS debug test." : ttsDebugText,
                emotion = "neutral",
                speed = 1f
            };

            if (ttsManager.StartSpeech(action, 1f, out var failureReason))
                ttsDebugMessage = ttsManager.LastMessage;
            else
                ttsDebugMessage = failureReason;
        }

        private string FormatActiveBone()
        {
            if (actionCoordinator == null || string.IsNullOrWhiteSpace(actionCoordinator.ActiveBoneName))
                return "-";

            return $"{actionCoordinator.ActiveBoneName} ({actionCoordinator.ActiveOwner})";
        }

        private void ResolveMomotalkReferences()
        {
            if (momotalkUIManager == null)
                momotalkUIManager = FindFirstObjectByType<MomotalkUIManager>();
            if (momotalkConversationController == null && momotalkUIManager != null)
                momotalkConversationController = momotalkUIManager.GetComponent<MomotalkConversationController>();
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        private static string FormatCharacterLabel(string characterId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return "-";
            if (string.IsNullOrWhiteSpace(displayName))
                return characterId;

            return $"{displayName} ({characterId})";
        }

        private static string FormatContextLabel(CharacterRuntimeContext context)
        {
            if (context == null)
                return "Missing";

            var profile = context.Profile;
            return FormatCharacterLabel(context.CharacterId, profile != null ? profile.DisplayName : string.Empty);
        }

        private static string FormatObjectName(Object value)
        {
            return value != null ? value.name : "Missing";
        }

        private static string FormatPresence(Object value)
        {
            return value != null ? "OK" : "Missing";
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
            if (ttsManager == null)
                ttsManager = GetComponent<TtsManager>();
            if (asrManager == null)
                asrManager = GetComponent<AsrManager>();
            if (memorySystem == null)
                memorySystem = GetComponent<MemorySystem>();
            if (momotalkUIManager == null)
                momotalkUIManager = FindFirstObjectByType<MomotalkUIManager>();
            if (momotalkConversationController == null && momotalkUIManager != null)
                momotalkConversationController = momotalkUIManager.GetComponent<MomotalkConversationController>();
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
