using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class LlmRelayConfigDraft
    {
        public string apiKey;
        public string model;
        public string chatCompletionsUrl;
        public string baseUrl;
        public bool useJsonResponseFormat = true;
        public bool supportsDeveloperRole;
        public float interactionTimeoutSeconds = 10f;
    }

    public sealed class LlmSubmitResult
    {
        public LlmSubmitResult(bool accepted, int requestId, string message)
        {
            Accepted = accepted;
            RequestId = requestId;
            Message = message ?? string.Empty;
        }

        public bool Accepted { get; }
        public int RequestId { get; }
        public string Message { get; }
    }

    public sealed class LlmRequestFailure
    {
        public LlmRequestFailure(int requestId, string message)
        {
            RequestId = requestId;
            Message = message ?? string.Empty;
        }

        public int RequestId { get; }
        public string Message { get; }
    }

    [DisallowMultipleComponent]
    public sealed class LlmRelay : MonoBehaviour
    {
        public const string LlmOwnerId = "LLM";

        private const string ConfigRelativePath = "UserSettings/VirtualPartnerLlmConfig.json";
        private const float DefaultInteractionTimeout = 10f;
        private const int RequestTimeoutSeconds = 120;
        private const int ConfigTestTimeoutSeconds = 30;
        private const string PromptFolderName = "Prompts";
        private const string CharacterPromptFileName = "character.md";
        private const string StagePlanRulesPromptFileName = "stageplan-rules.md";
        private const string ParameterBonesPromptFileName = "parameter-bones.md";
        private const string PresetActionsPromptFileName = "preset-actions.md";
        private const string LocomotionPromptFileName = "locomotion.md";
        private const string ExamplesPromptFileName = "examples.md";
        private const string NamedGesturesPromptFileName = "named-gestures.md";

        [Header("References")]
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private CharacterProfile characterProfile;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private PresetAnimationProfile presetAnimationProfile;
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private StagePlanPlayer stagePlanPlayer;
        [SerializeField] private AutonomousBehaviorScheduler autonomousBehaviorScheduler;
        [SerializeField] private MemorySystem memorySystem;

        [Header("Prompt TextAssets")]
        [SerializeField] private TextAsset characterPrompt;
        [SerializeField] private TextAsset stagePlanRulesPrompt;
        [SerializeField] private TextAsset parameterBonesPrompt;
        [SerializeField] private TextAsset presetActionsPrompt;
        [SerializeField] private TextAsset locomotionPrompt;
        [SerializeField] private TextAsset examplesPrompt;
        [SerializeField] private TextAsset namedGesturesPrompt;

        [Header("LLM Streaming")]
        [SerializeField] private bool streamStagePlans = true;
        [SerializeField] private int streamInitialStageBufferCount = 2;
        [SerializeField] private bool streamHoldLastPoseWhileWaiting = true;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool configLoaded;
        [SerializeField] private bool configReady;
        [SerializeField] private bool requestPending;
        [SerializeField] private bool streamingRequestActive;
        [SerializeField] private bool streamingStagePlanStarted;
        [SerializeField] private int streamingParsedStageCount;
        [SerializeField] private int streamingBufferedStageCount;
        [SerializeField] private int streamingAppendedStageCount;
        [SerializeField] private int latestRequestId;
        [SerializeField] private int pendingRequestId;
        [SerializeField] private string statusText = "Not configured.";
        [SerializeField] private string configStatus = "Not loaded.";
        [SerializeField] private bool configTestPending;
        [SerializeField] private string configTestStatus;
        [SerializeField] private string lastError;
        [SerializeField] private string lastPromptStatus;
        [SerializeField] private string lastStreamingStatus;
        [SerializeField, TextArea(4, 10)] private string lastRawResponse;
        [SerializeField, TextArea(4, 10)] private string lastExtractedStagePlan;

        private LlmRelayConfig config = new LlmRelayConfig();
        private UnityWebRequest activeRequest;
        private UnityWebRequest activeConfigTestRequest;
        private Coroutine activeCoroutine;
        private Coroutine configTestCoroutine;
        private string configPath;
        private readonly LlmPromptCapabilityBuilder capabilityBuilder = new LlmPromptCapabilityBuilder();

        public bool Initialized => initialized;
        public bool ConfigLoaded => configLoaded;
        public bool ConfigReady => configReady;
        public bool RequestPending => requestPending;
        public bool StreamingRequestActive => streamingRequestActive;
        public bool StreamingStagePlanStarted => streamingStagePlanStarted;
        public int StreamingParsedStageCount => streamingParsedStageCount;
        public int StreamingBufferedStageCount => streamingBufferedStageCount;
        public int StreamingAppendedStageCount => streamingAppendedStageCount;
        public int LatestRequestId => latestRequestId;
        public int PendingRequestId => pendingRequestId;
        public string StatusText => statusText;
        public string ConfigStatus => configStatus;
        public bool ConfigTestPending => configTestPending;
        public string ConfigTestStatus => configTestStatus;
        public string LastError => lastError;
        public string LastPromptStatus => lastPromptStatus;
        public string LastStreamingStatus => lastStreamingStatus;
        public string LastRawResponse => lastRawResponse;
        public string LastExtractedStagePlan => lastExtractedStagePlan;
        public float InteractionTimeoutSeconds => config.InteractionTimeoutSeconds;
        public bool StreamStagePlans => streamStagePlans;
        public int StreamInitialStageBufferCount => Mathf.Max(1, streamInitialStageBufferCount);
        public bool StreamHoldLastPoseWhileWaiting => streamHoldLastPoseWhileWaiting;
        public string ConfigPath => configPath;
        public bool IsLlmStagePlanPlaying => stagePlanPlayer != null && stagePlanPlayer.IsOwnerPlaying(LlmOwnerId);

        public event Action<LlmRequestFailure> RequestFailed;

        public void Configure(
            BoneMapProfile boneProfile,
            CharacterProfile profile,
            Transform root,
            AvatarPoseApplier poseApplier,
            PresetAnimationProfile presetProfile,
            LocomotionProfile locomotion,
            StagePlanPlayer stagePlayer,
            AutonomousBehaviorScheduler scheduler,
            MemorySystem memory = null)
        {
            boneMapProfile = boneProfile;
            characterProfile = profile;
            boneRoot = root;
            avatarPoseApplier = poseApplier;
            presetAnimationProfile = presetProfile;
            locomotionProfile = locomotion;
            stagePlanPlayer = stagePlayer;
            autonomousBehaviorScheduler = scheduler;
            memorySystem = memory;
            ConfigureCapabilityBuilder();
            EnsureConfigPath();
            initialized = ValidateReferences();

            if (initialized)
                ReloadConfig();
        }

        public bool ReloadConfig()
        {
            configLoaded = false;
            configReady = false;

            EnsureConfigPath();

            var path = configPath;
            if (!File.Exists(path))
            {
                config = new LlmRelayConfig();
                configStatus = $"Missing config: {path}";
                statusText = "Config missing.";
                if (memorySystem != null)
                    memorySystem.ReloadJudgeConfig();
                return false;
            }

            try
            {
                config = JsonUtility.FromJson<LlmRelayConfig>(File.ReadAllText(path, Encoding.UTF8)) ?? new LlmRelayConfig();
            }
            catch (Exception exception)
            {
                config = new LlmRelayConfig();
                configStatus = $"Config parse failed: {exception.Message}";
                statusText = "Config failed.";
                if (memorySystem != null)
                    memorySystem.ReloadJudgeConfig();
                return false;
            }

            config.Normalize();
            configLoaded = true;
            configReady = config.IsReady(out var reason);
            configStatus = configReady ? "Ready." : reason;
            statusText = configReady ? "Ready." : "Config incomplete.";

            if (autonomousBehaviorScheduler != null)
                autonomousBehaviorScheduler.SetUserInteractionTimeout(config.InteractionTimeoutSeconds);
            if (memorySystem != null)
                memorySystem.ReloadJudgeConfig();

            return configReady;
        }

        public LlmRelayConfigDraft CreateConfigDraft()
        {
            EnsureConfigPath();
            return config != null ? config.ToDraft() : new LlmRelayConfigDraft();
        }

        public bool SaveConfig(LlmRelayConfigDraft draft, out string message)
        {
            message = string.Empty;
            var nextConfig = LlmRelayConfig.FromDraft(draft);
            nextConfig.Normalize();
            if (!nextConfig.IsReady(out var reason))
            {
                message = $"Cannot save config: {reason}";
                configStatus = message;
                statusText = "Config incomplete.";
                return false;
            }

            EnsureConfigPath();
            try
            {
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(configPath, JsonUtility.ToJson(nextConfig, true), Encoding.UTF8);
            }
            catch (Exception exception)
            {
                message = $"Config save failed: {exception.Message}";
                configStatus = message;
                statusText = "Config save failed.";
                return false;
            }

            config = nextConfig;
            configLoaded = true;
            configReady = true;
            configStatus = "Saved and ready.";
            statusText = "Ready.";
            if (autonomousBehaviorScheduler != null)
                autonomousBehaviorScheduler.SetUserInteractionTimeout(config.InteractionTimeoutSeconds);
            if (memorySystem != null)
                memorySystem.ReloadJudgeConfig();

            message = $"Saved config: {configPath}";
            return true;
        }

        public bool StartConfigTest(LlmRelayConfigDraft draft)
        {
            if (configTestPending || configTestCoroutine != null)
            {
                configTestStatus = "Config test is already running.";
                return false;
            }

            var testConfig = LlmRelayConfig.FromDraft(draft);
            testConfig.Normalize();
            if (!testConfig.IsReady(out var reason))
            {
                configTestStatus = $"Cannot test config: {reason}";
                return false;
            }

            configTestCoroutine = StartCoroutine(TestConfigRoutine(testConfig));
            return true;
        }

        private void OnDisable()
        {
            if (requestPending || activeCoroutine != null || activeRequest != null)
                StopPendingRequest();

            StopConfigTest();
        }

        public bool Submit(string userText)
        {
            return SubmitWithResult(userText).Accepted;
        }

        public LlmSubmitResult SubmitWithResult(string userText)
        {
            return SubmitWithResult(userText, string.Empty);
        }

        public LlmSubmitResult SubmitWithResult(string userText, string historyContext)
        {
            lastError = string.Empty;

            if (!initialized && !ValidateReferences())
                return FailSubmitResult("LlmRelay references are missing.");

            if (string.IsNullOrWhiteSpace(userText))
                return FailSubmitResult("User text is empty.");

            if (autonomousBehaviorScheduler != null)
            {
                autonomousBehaviorScheduler.SetUserInteractionTimeout(config.InteractionTimeoutSeconds);
                autonomousBehaviorScheduler.EnterUserInteraction();
            }

            if (!configReady && !ReloadConfig())
                return FailSubmitResult(configStatus);

            latestRequestId++;
            pendingRequestId = latestRequestId;
            requestPending = true;
            statusText = $"Request {latestRequestId} pending.";
            lastRawResponse = string.Empty;
            lastExtractedStagePlan = string.Empty;
            ResetStreamingStatus();

            activeCoroutine = StartCoroutine(SendRequest(latestRequestId, userText.Trim(), historyContext));
            return new LlmSubmitResult(true, latestRequestId, statusText);
        }

        public bool StopLlmStagePlan()
        {
            if (stagePlanPlayer == null)
            {
                lastError = "StagePlanPlayer reference is missing.";
                return false;
            }

            if (stagePlanPlayer.StopStagePlanForOwner(LlmOwnerId))
            {
                if (autonomousBehaviorScheduler != null)
                    autonomousBehaviorScheduler.EnterUserInteraction();

                statusText = "LLM StagePlan stopped.";
                lastError = string.Empty;
                return true;
            }

            statusText = stagePlanPlayer.IsPlaying
                ? $"Current StagePlan owner is '{FormatOwner(stagePlanPlayer.ActiveOwnerId)}', not '{LlmOwnerId}'."
                : "No LLM StagePlan is playing.";
            lastError = statusText;
            return false;
        }

        public void ManualUpdate(float deltaTime)
        {
            if (autonomousBehaviorScheduler == null)
                return;

            if (requestPending || IsLlmStagePlanPlaying)
                autonomousBehaviorScheduler.KeepUserInteractionAlive();
        }

        public void StopPendingRequest()
        {
            latestRequestId++;
            pendingRequestId = 0;
            requestPending = false;
            ResetStreamingStatus();

            if (activeCoroutine != null)
            {
                StopCoroutine(activeCoroutine);
                activeCoroutine = null;
            }

            if (activeRequest != null)
            {
                activeRequest.Abort();
                activeRequest.Dispose();
                activeRequest = null;
            }
        }

        private void StopConfigTest()
        {
            if (configTestCoroutine != null)
            {
                StopCoroutine(configTestCoroutine);
                configTestCoroutine = null;
            }

            if (activeConfigTestRequest != null)
            {
                activeConfigTestRequest.Abort();
                activeConfigTestRequest.Dispose();
                activeConfigTestRequest = null;
            }

            configTestPending = false;
        }

        private void CompleteConfigTest()
        {
            activeConfigTestRequest = null;
            configTestPending = false;
            configTestCoroutine = null;
        }

        public bool CopyFinalPromptToClipboard()
        {
            var prompt = BuildPromptPreview();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                lastPromptStatus = "Prompt is empty.";
                return false;
            }

            GUIUtility.systemCopyBuffer = prompt;
            lastPromptStatus = $"Prompt copied: {prompt.Length.ToString(CultureInfo.InvariantCulture)} chars.";
            statusText = lastPromptStatus;
            return true;
        }

        public string BuildPromptPreview()
        {
            var systemPrompt = BuildSystemPrompt();
            var developerPrompt = BuildDeveloperPrompt();
            var builder = new StringBuilder(systemPrompt.Length + developerPrompt.Length + 64);
            builder.AppendLine("[system]");
            builder.AppendLine(systemPrompt);
            builder.AppendLine();
            builder.AppendLine("[developer]");
            builder.Append(developerPrompt);
            return builder.ToString();
        }

        private IEnumerator SendRequest(int requestId, string userText, string historyContext)
        {
            if (streamStagePlans)
            {
                yield return SendStreamingRequest(requestId, userText, historyContext);
                yield break;
            }

            yield return SendBufferedRequest(requestId, userText, historyContext);
        }

        private IEnumerator TestConfigRoutine(LlmRelayConfig testConfig)
        {
            configTestPending = true;
            configTestStatus = "Testing API config...";

            var requestJson = BuildConfigTestRequestJson(testConfig.model);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            using (var request = new UnityWebRequest(testConfig.GetChatCompletionsUrl(), "POST"))
            {
                activeConfigTestRequest = request;
                request.timeout = ConfigTestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + testConfig.apiKey);

                yield return request.SendWebRequest();

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    configTestStatus = $"Test failed: HTTP {request.responseCode} {request.error}. {BuildStatusPreview(responseText)}";
                    CompleteConfigTest();
                    yield break;
                }

                if (!TryExtractAssistantContent(responseText, out var content, out var failureReason))
                {
                    configTestStatus = $"Test failed: {failureReason}";
                    CompleteConfigTest();
                    yield break;
                }

                configTestStatus = $"Test succeeded: HTTP {request.responseCode}. Assistant: {BuildStatusPreview(content)}";
            }

            CompleteConfigTest();
        }

        private IEnumerator SendBufferedRequest(int requestId, string userText, string historyContext)
        {
            var requestJson = BuildRequestJson(userText, historyContext, false);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            using (var request = new UnityWebRequest(config.GetChatCompletionsUrl(), "POST"))
            {
                activeRequest = request;
                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + config.apiKey);

                yield return request.SendWebRequest();

                if (requestId != latestRequestId)
                {
                    if (activeRequest == request)
                        activeRequest = null;
                    yield break;
                }

                requestPending = false;
                pendingRequestId = 0;
                activeCoroutine = null;
                activeRequest = null;

                var responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                lastRawResponse = responseText;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    RecordError(requestId, $"Request {requestId} failed: HTTP {request.responseCode} {request.error}");
                    yield break;
                }

                if (!TryExtractAssistantContent(responseText, out var content, out var responseFailure))
                {
                    RecordError(requestId, responseFailure);
                    yield break;
                }

                if (!TryExtractStagePlanJson(content, out var stagePlanJson, out var stagePlanFailure))
                {
                    RecordError(requestId, stagePlanFailure);
                    yield break;
                }

                lastExtractedStagePlan = stagePlanJson;

                var validationResult = StagePlanValidator.Validate(stagePlanJson, characterProfile);
                if (!validationResult.IsValid)
                {
                    RecordError(requestId, FormatStagePlanValidationFailure(validationResult));
                    yield break;
                }

                if (stagePlanPlayer == null || !stagePlanPlayer.ReplaceJsonForOwner(stagePlanJson, LlmOwnerId, requestId))
                {
                    RecordError(requestId, stagePlanPlayer != null ? stagePlanPlayer.LastMessage : "StagePlanPlayer reference is missing.");
                    yield break;
                }

                statusText = $"Request {requestId} StagePlan playing.";
                lastError = string.Empty;
            }
        }

        private IEnumerator SendStreamingRequest(int requestId, string userText, string historyContext)
        {
            var requestJson = BuildRequestJson(userText, historyContext, true);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            var textHandler = new StreamingTextDownloadHandler();
            var sseParser = new StreamingSseParser();
            var stageParser = new StagePlanIncrementalParser();
            var bufferedStageJsons = new List<string>();

            using (var request = new UnityWebRequest(config.GetChatCompletionsUrl(), "POST"))
            {
                activeRequest = request;
                streamingRequestActive = true;
                lastStreamingStatus = "Streaming request started.";
                statusText = $"Request {requestId} streaming.";

                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = textHandler;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + config.apiKey);

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (requestId != latestRequestId)
                    {
                        ClearRequestStateIfCurrent(requestId, request);
                        yield break;
                    }

                    if (!DrainStreamingChunks(requestId, textHandler, sseParser, stageParser, bufferedStageJsons, false, out var drainFailure))
                    {
                        FailStreamingRequest(requestId, drainFailure, request);
                        yield break;
                    }

                    yield return null;
                }

                if (requestId != latestRequestId)
                {
                    ClearRequestStateIfCurrent(requestId, request);
                    yield break;
                }

                if (!DrainStreamingChunks(requestId, textHandler, sseParser, stageParser, bufferedStageJsons, true, out var finalDrainFailure))
                {
                    FailStreamingRequest(requestId, finalDrainFailure, request);
                    yield break;
                }

                ClearRequestStateIfCurrent(requestId, request);
                lastRawResponse = textHandler.RawText;
                lastExtractedStagePlan = stageParser.Content;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    FailStreamingRequest(requestId, $"Request {requestId} streaming failed: HTTP {request.responseCode} {request.error}", null);
                    yield break;
                }

                if (!TryExtractStagePlanJson(stageParser.Content, out var fullStagePlanJson, out var extractFailure))
                {
                    FailStreamingRequest(requestId, extractFailure, null);
                    yield break;
                }

                lastExtractedStagePlan = fullStagePlanJson;
                var validationResult = StagePlanValidator.Validate(fullStagePlanJson, characterProfile);
                if (!validationResult.IsValid)
                {
                    FailStreamingRequest(requestId, FormatStagePlanValidationFailure(validationResult), null);
                    yield break;
                }

                if (streamingParsedStageCount <= 0)
                {
                    FailStreamingRequest(requestId, "Streaming StagePlan contained no complete stages.", null);
                    yield break;
                }

                if (!streamingStagePlanStarted
                    && !StartBufferedStreamingStages(requestId, bufferedStageJsons, out var startFailure))
                {
                    FailStreamingRequest(requestId, startFailure, null);
                    yield break;
                }

                if (stagePlanPlayer == null || !stagePlanPlayer.CompleteStreamingForOwner(LlmOwnerId, requestId))
                {
                    FailStreamingRequest(requestId, stagePlanPlayer != null ? stagePlanPlayer.LastMessage : "StagePlanPlayer reference is missing.", null);
                    yield break;
                }

                streamingRequestActive = false;
                lastStreamingStatus = $"Streaming complete. stages={streamingParsedStageCount.ToString(CultureInfo.InvariantCulture)}.";
                statusText = $"Request {requestId} streaming complete.";
                lastError = string.Empty;
            }
        }

        private bool DrainStreamingChunks(
            int requestId,
            StreamingTextDownloadHandler textHandler,
            StreamingSseParser sseParser,
            StagePlanIncrementalParser stageParser,
            List<string> bufferedStageJsons,
            bool complete,
            out string failureReason)
        {
            failureReason = string.Empty;

            while (textHandler.TryDequeueChunk(out var chunk))
            {
                if (!sseParser.Append(chunk, out var payloads, out failureReason))
                    return false;

                if (!ProcessStreamingPayloads(requestId, payloads, stageParser, bufferedStageJsons, out failureReason))
                    return false;
            }

            if (!complete)
                return true;

            if (!sseParser.Complete(out var finalPayloads, out failureReason))
                return false;

            return ProcessStreamingPayloads(requestId, finalPayloads, stageParser, bufferedStageJsons, out failureReason);
        }

        private bool ProcessStreamingPayloads(
            int requestId,
            List<string> payloads,
            StagePlanIncrementalParser stageParser,
            List<string> bufferedStageJsons,
            out string failureReason)
        {
            failureReason = string.Empty;
            for (var i = 0; i < payloads.Count; i++)
            {
                var payload = payloads[i];
                if (string.IsNullOrWhiteSpace(payload))
                    continue;

                if (payload.Trim() == "[DONE]")
                {
                    lastStreamingStatus = "SSE done received.";
                    continue;
                }

                if (!TryExtractStreamingDelta(payload, out var deltaContent, out failureReason))
                    return false;

                if (string.IsNullOrEmpty(deltaContent))
                    continue;

                stageParser.Append(deltaContent);

                while (stageParser.TryDequeueStage(out var stageJson))
                {
                    if (!HandleStreamingStage(requestId, stageJson, bufferedStageJsons, out failureReason))
                        return false;
                }
            }

            return true;
        }

        private bool HandleStreamingStage(
            int requestId,
            string stageJson,
            List<string> bufferedStageJsons,
            out string failureReason)
        {
            failureReason = string.Empty;
            streamingParsedStageCount++;
            var stagePlanJson = BuildStagePlanJsonFromStageJsons(new[] { stageJson });
            var validationResult = StagePlanValidator.Validate(stagePlanJson, characterProfile);
            if (!validationResult.IsValid)
            {
                failureReason = FormatStagePlanValidationFailure(validationResult);
                return false;
            }

            if (!streamingStagePlanStarted)
            {
                bufferedStageJsons.Add(stageJson);
                streamingBufferedStageCount = bufferedStageJsons.Count;
                lastStreamingStatus = $"Buffered streaming stage {streamingBufferedStageCount.ToString(CultureInfo.InvariantCulture)}/{StreamInitialStageBufferCount.ToString(CultureInfo.InvariantCulture)}.";
                if (bufferedStageJsons.Count < StreamInitialStageBufferCount)
                    return true;

                return StartBufferedStreamingStages(requestId, bufferedStageJsons, out failureReason);
            }

            if (stagePlanPlayer == null || !stagePlanPlayer.AppendStreamingJsonForOwner(stagePlanJson, LlmOwnerId, requestId))
            {
                failureReason = stagePlanPlayer != null ? stagePlanPlayer.LastMessage : "StagePlanPlayer reference is missing.";
                return false;
            }

            streamingAppendedStageCount++;
            lastStreamingStatus = $"Appended streaming stage {streamingAppendedStageCount.ToString(CultureInfo.InvariantCulture)}.";
            return true;
        }

        private bool StartBufferedStreamingStages(int requestId, List<string> bufferedStageJsons, out string failureReason)
        {
            failureReason = string.Empty;
            if (bufferedStageJsons == null || bufferedStageJsons.Count == 0)
            {
                failureReason = "No buffered streaming stages are available.";
                return false;
            }

            var stagePlanJson = BuildStagePlanJsonFromStageJsons(bufferedStageJsons);
            if (stagePlanPlayer == null || !stagePlanPlayer.StartStreamingJsonForOwner(stagePlanJson, LlmOwnerId, requestId, streamHoldLastPoseWhileWaiting))
            {
                failureReason = stagePlanPlayer != null ? stagePlanPlayer.LastMessage : "StagePlanPlayer reference is missing.";
                return false;
            }

            streamingStagePlanStarted = true;
            streamingAppendedStageCount += bufferedStageJsons.Count;
            streamingBufferedStageCount = 0;
            bufferedStageJsons.Clear();
            lastStreamingStatus = $"Streaming playback started with {streamingAppendedStageCount.ToString(CultureInfo.InvariantCulture)} stage(s).";
            statusText = $"Request {requestId} streaming StagePlan playing.";
            return true;
        }

        private void FailStreamingRequest(int requestId, string message, UnityWebRequest request)
        {
            if (request != null && !request.isDone)
                request.Abort();

            ClearRequestStateIfCurrent(requestId, request);
            if (stagePlanPlayer != null)
                stagePlanPlayer.StopStagePlanForOwner(LlmOwnerId);

            streamingRequestActive = false;
            lastStreamingStatus = "Streaming failed.";
            RecordError(requestId, message);
        }

        private void ClearRequestStateIfCurrent(int requestId, UnityWebRequest request)
        {
            if (requestId != latestRequestId)
                return;

            requestPending = false;
            pendingRequestId = 0;
            activeCoroutine = null;
            streamingRequestActive = false;
            if (activeRequest == request)
                activeRequest = null;
        }

        private string BuildRequestJson(string userText, string historyContext, bool streaming)
        {
            var systemPrompt = BuildSystemPrompt();
            var developerPrompt = BuildDeveloperPrompt(historyContext);
            var combinedSystemPrompt = config.supportsDeveloperRole
                ? systemPrompt
                : systemPrompt + "\n\n" + developerPrompt;

            var builder = new StringBuilder(4096);
            builder.Append("{\"model\":\"").Append(JsonTextUtility.Escape(config.model)).Append("\",\"messages\":[");
            AppendMessage(builder, "system", combinedSystemPrompt);

            if (config.supportsDeveloperRole)
            {
                builder.Append(',');
                AppendMessage(builder, "developer", developerPrompt);
            }

            builder.Append(',');
            AppendMessage(builder, "user", userText);
            builder.Append(']');

            if (config.useJsonResponseFormat)
                builder.Append(",\"response_format\":{\"type\":\"json_object\"}");

            if (streaming)
                builder.Append(",\"stream\":true");

            builder.Append('}');
            return builder.ToString();
        }

        private static string BuildConfigTestRequestJson(string model)
        {
            var builder = new StringBuilder(192);
            builder.Append("{\"model\":\"")
                .Append(JsonTextUtility.Escape(model))
                .Append("\",\"messages\":[{\"role\":\"user\",\"content\":\"Reply with exactly OK.\"}]}");
            return builder.ToString();
        }

        private string BuildSystemPrompt()
        {
            return $"You control {GetTargetCharacterName()} in Unity. Return only one valid JSON StagePlan 2.0 object. Do not use Markdown, comments, or explanation.";
        }

        private string BuildDeveloperPrompt()
        {
            return BuildDeveloperPrompt(string.Empty);
        }

        private string BuildDeveloperPrompt(string historyContext)
        {
            ConfigureCapabilityBuilder();

            var builder = new StringBuilder(12288);
            AppendTargetCharacterSection(builder);
            AppendPromptSection(builder, "Character", LoadPromptText(characterPrompt, CharacterPromptFileName), false);
            AppendPromptSection(builder, "StagePlan Rules", LoadPromptText(stagePlanRulesPrompt, StagePlanRulesPromptFileName), true);
            AppendPromptSection(builder, "Parameter Bone Rules", LoadPromptText(parameterBonesPrompt, ParameterBonesPromptFileName), true);
            AppendPromptSection(builder, "Preset Action Rules", LoadPromptText(presetActionsPrompt, PresetActionsPromptFileName), true);
            AppendPromptSection(builder, "Locomotion Rules", LoadPromptText(locomotionPrompt, LocomotionPromptFileName), true);
            AppendPromptSection(builder, "Format Examples", LoadPromptText(examplesPrompt, ExamplesPromptFileName), true);
            AppendPromptSection(builder, "Named Gestures", LoadPromptText(namedGesturesPrompt, NamedGesturesPromptFileName), false);
            AppendPromptSection(builder, "Long Term Memory", BuildMemoryPromptContext(), false);
            AppendPromptSection(builder, "Recent Momotalk Chat Context", historyContext, false);
            capabilityBuilder.Append(builder, LoadPromptText(presetActionsPrompt, PresetActionsPromptFileName));
            return builder.ToString();
        }

        private void ConfigureCapabilityBuilder()
        {
            capabilityBuilder.Configure(
                boneMapProfile,
                characterProfile,
                boneRoot,
                avatarPoseApplier,
                presetAnimationProfile,
                locomotionProfile);
        }

        private string BuildMemoryPromptContext()
        {
            return memorySystem != null
                ? memorySystem.BuildPromptContext(GetTargetCharacterId())
                : string.Empty;
        }

        private void AppendTargetCharacterSection(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("## Target Character");
            if (characterProfile == null)
            {
                builder.AppendLine("The current target character is not configured.");
                return;
            }

            builder.Append("Current target: ")
                .Append(GetTargetCharacterName())
                .AppendLine(".");
            builder.AppendLine("Use this target for tone and behavior context only. Do not output characterId in JSON.");
        }

        private static void AppendPromptSection(StringBuilder builder, string title, string promptText, bool includeWhenEmpty)
        {
            var text = promptText == null ? string.Empty : promptText.Trim();
            if (string.IsNullOrWhiteSpace(text) && !includeWhenEmpty)
                return;

            builder.AppendLine();
            builder.Append("## ").AppendLine(title);
            if (string.IsNullOrWhiteSpace(text))
                builder.AppendLine("(empty)");
            else
                builder.AppendLine(text);
        }

        private void ResetStreamingStatus()
        {
            streamingRequestActive = false;
            streamingStagePlanStarted = false;
            streamingParsedStageCount = 0;
            streamingBufferedStageCount = 0;
            streamingAppendedStageCount = 0;
            lastStreamingStatus = string.Empty;
        }

        private static string BuildStagePlanJsonFromStageJsons(IReadOnlyList<string> stageJsons)
        {
            var builder = new StringBuilder(256);
            builder.Append("{\"schemaVersion\":\"2.0\",\"type\":\"stagePlan\",\"stages\":[");
            for (var i = 0; i < stageJsons.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append(stageJsons[i]);
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private bool TryExtractStreamingDelta(string payload, out string deltaContent, out string failureReason)
        {
            deltaContent = string.Empty;
            failureReason = string.Empty;

            ChatCompletionStreamResponse response = null;
            try
            {
                response = JsonUtility.FromJson<ChatCompletionStreamResponse>(payload);
            }
            catch (Exception exception)
            {
                failureReason = $"LLM stream payload parse failed: {exception.Message}";
                return false;
            }

            if (response == null)
            {
                failureReason = "LLM stream payload parse returned no object.";
                return false;
            }

            if (response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
            {
                failureReason = $"LLM stream error: {response.error.message}";
                return false;
            }

            if (response.choices == null || response.choices.Length == 0)
                return true;

            var choice = response.choices[0];
            if (choice == null || choice.delta == null)
                return true;

            deltaContent = choice.delta.content ?? string.Empty;
            return true;
        }

        private bool TryExtractAssistantContent(string responseText, out string content, out string failureReason)
        {
            content = string.Empty;
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                failureReason = "LLM response is empty.";
                return false;
            }

            ChatCompletionResponse response = null;
            try
            {
                response = JsonUtility.FromJson<ChatCompletionResponse>(responseText);
            }
            catch (Exception exception)
            {
                failureReason = $"LLM response parse failed: {exception.Message}";
                return false;
            }

            if (response == null)
            {
                failureReason = "LLM response parse returned no object.";
                return false;
            }

            if (response.error != null && !string.IsNullOrWhiteSpace(response.error.message))
            {
                failureReason = $"LLM error: {response.error.message}";
                return false;
            }

            if (response.choices == null || response.choices.Length == 0 || response.choices[0] == null || response.choices[0].message == null)
            {
                failureReason = "LLM response has no assistant message.";
                return false;
            }

            content = response.choices[0].message.content;
            if (string.IsNullOrWhiteSpace(content))
            {
                failureReason = "LLM assistant message is empty.";
                return false;
            }

            return true;
        }

        private static bool TryExtractStagePlanJson(string content, out string stagePlanJson, out string failureReason)
        {
            stagePlanJson = string.Empty;
            failureReason = string.Empty;

            var trimmed = JsonTextUtility.StripCodeFence(content);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                failureReason = "LLM returned empty content.";
                return false;
            }

            if (JsonTextUtility.TryExtractFirstJsonObject(trimmed, out stagePlanJson))
                return true;

            failureReason = "LLM content does not contain a JSON object.";
            return false;
        }

        private static string FormatStagePlanValidationFailure(StagePlanValidationResult result)
        {
            if (result == null)
                return "StagePlan validation failed.";

            var builder = new StringBuilder(256);
            builder.Append("StagePlan validation failed.");
            for (var i = 0; i < result.Errors.Count; i++)
                builder.Append(' ').Append(result.Errors[i]);

            return builder.ToString();
        }

        private void RecordError(int requestId, string message)
        {
            lastError = string.IsNullOrWhiteSpace(message) ? "Unknown LLM error." : message;
            statusText = "Error.";
            Debug.LogWarning($"[VirtualPartner] LlmRelay: {lastError}", this);
            RequestFailed?.Invoke(new LlmRequestFailure(requestId, lastError));
        }

        private LlmSubmitResult FailSubmitResult(string message)
        {
            RecordError(0, message);
            return new LlmSubmitResult(false, 0, lastError);
        }

        private bool ValidateReferences()
        {
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (characterProfile == null)
                return Fail("CharacterProfile reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.");
            if (presetAnimationProfile == null)
                return Fail("PresetAnimationProfile reference is missing.");
            if (locomotionProfile == null)
                return Fail("LocomotionProfile reference is missing.");
            if (stagePlanPlayer == null)
                return Fail("StagePlanPlayer reference is missing.");
            if (autonomousBehaviorScheduler == null)
                return Fail("AutonomousBehaviorScheduler reference is missing.");

            ConfigureCapabilityBuilder();
            initialized = true;
            return true;
        }

        private bool Fail(string message)
        {
            initialized = false;
            statusText = "Failed.";
            lastError = message;
            Debug.LogError($"[VirtualPartner] LlmRelay failed: {message}", this);
            return false;
        }

        private static void AppendMessage(StringBuilder builder, string role, string content)
        {
            builder.Append("{\"role\":\"")
                .Append(JsonTextUtility.Escape(role))
                .Append("\",\"content\":\"")
                .Append(JsonTextUtility.Escape(content))
                .Append("\"}");
        }

        private static string FormatOwner(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId) ? "External" : ownerId;
        }

        private string GetTargetCharacterName()
        {
            if (characterProfile == null)
                return "the current character";

            if (!string.IsNullOrWhiteSpace(characterProfile.DisplayName))
                return characterProfile.DisplayName.Trim();

            return string.IsNullOrWhiteSpace(characterProfile.CharacterId)
                ? "the current character"
                : characterProfile.CharacterId.Trim();
        }

        private string GetTargetCharacterId()
        {
            return characterProfile != null && !string.IsNullOrWhiteSpace(characterProfile.CharacterId)
                ? characterProfile.CharacterId.Trim()
                : string.Empty;
        }

        private void EnsureConfigPath()
        {
            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ConfigRelativePath);
        }

        private static string BuildStatusPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();
            const int limit = 320;
            return text.Length <= limit ? text : text.Substring(0, limit) + "...";
        }

        private static string LoadPromptText(TextAsset promptAsset, string fileName)
        {
            if (promptAsset != null)
                return promptAsset.text;

            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            var path = Path.Combine(Application.dataPath, "VirtualPartner", PromptFolderName, fileName);
            if (!File.Exists(path))
                return string.Empty;

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }


        [Serializable]
        private sealed class LlmRelayConfig
        {
            public string apiKey;
            public string model;
            public string chatCompletionsUrl;
            public string baseUrl;
            public bool useJsonResponseFormat = true;
            public bool supportsDeveloperRole;
            public float interactionTimeoutSeconds = DefaultInteractionTimeout;

            public float InteractionTimeoutSeconds => interactionTimeoutSeconds > 0f
                ? interactionTimeoutSeconds
                : DefaultInteractionTimeout;

            public static LlmRelayConfig FromDraft(LlmRelayConfigDraft draft)
            {
                if (draft == null)
                    return new LlmRelayConfig();

                return new LlmRelayConfig
                {
                    apiKey = draft.apiKey,
                    model = draft.model,
                    chatCompletionsUrl = draft.chatCompletionsUrl,
                    baseUrl = draft.baseUrl,
                    useJsonResponseFormat = draft.useJsonResponseFormat,
                    supportsDeveloperRole = draft.supportsDeveloperRole,
                    interactionTimeoutSeconds = draft.interactionTimeoutSeconds
                };
            }

            public LlmRelayConfigDraft ToDraft()
            {
                return new LlmRelayConfigDraft
                {
                    apiKey = apiKey,
                    model = model,
                    chatCompletionsUrl = chatCompletionsUrl,
                    baseUrl = baseUrl,
                    useJsonResponseFormat = useJsonResponseFormat,
                    supportsDeveloperRole = supportsDeveloperRole,
                    interactionTimeoutSeconds = InteractionTimeoutSeconds
                };
            }

            public void Normalize()
            {
                apiKey = apiKey == null ? string.Empty : apiKey.Trim();
                model = model == null ? string.Empty : model.Trim();
                chatCompletionsUrl = chatCompletionsUrl == null ? string.Empty : chatCompletionsUrl.Trim();
                baseUrl = baseUrl == null ? string.Empty : baseUrl.Trim();
                if (interactionTimeoutSeconds <= 0f)
                    interactionTimeoutSeconds = DefaultInteractionTimeout;
            }

            public bool IsReady(out string reason)
            {
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    reason = "apiKey is missing.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(model))
                {
                    reason = "model is missing.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(GetChatCompletionsUrl()))
                {
                    reason = "chatCompletionsUrl or baseUrl is missing.";
                    return false;
                }

                reason = string.Empty;
                return true;
            }

            public string GetChatCompletionsUrl()
            {
                if (!string.IsNullOrWhiteSpace(chatCompletionsUrl))
                    return chatCompletionsUrl;

                if (string.IsNullOrWhiteSpace(baseUrl))
                    return string.Empty;

                return baseUrl.TrimEnd('/') + "/v1/chat/completions";
            }
        }

        [Serializable]
        private sealed class ChatCompletionResponse
        {
            public ChatCompletionChoice[] choices;
            public ChatCompletionError error;
        }

        [Serializable]
        private sealed class ChatCompletionChoice
        {
            public ChatCompletionMessage message;
        }

        [Serializable]
        private sealed class ChatCompletionMessage
        {
            public string content;
        }

        [Serializable]
        private sealed class ChatCompletionStreamResponse
        {
            public ChatCompletionStreamChoice[] choices;
            public ChatCompletionError error;
        }

        [Serializable]
        private sealed class ChatCompletionStreamChoice
        {
            public ChatCompletionDelta delta;
            public string finish_reason;
        }

        [Serializable]
        private sealed class ChatCompletionDelta
        {
            public string role;
            public string content;
        }

        [Serializable]
        private sealed class ChatCompletionError
        {
            public string message;
        }
    }
}
