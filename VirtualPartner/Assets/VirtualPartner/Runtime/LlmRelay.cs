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
        private const float AxisComponentThreshold = 0.1f;
        private const float PrimaryEffectAngle = 90f;
        private const float TwistDotThreshold = 0.98f;
        private const string PromptFolderName = "Prompts";
        private const string CharacterPromptFileName = "character.md";
        private const string StagePlanRulesPromptFileName = "stageplan-rules.md";
        private const string ParameterBonesPromptFileName = "parameter-bones.md";
        private const string PresetActionsPromptFileName = "preset-actions.md";
        private const string LocomotionPromptFileName = "locomotion.md";
        private const string ExamplesPromptFileName = "examples.md";

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
        [SerializeField] private string lastError;
        [SerializeField] private string lastPromptStatus;
        [SerializeField] private string lastStreamingStatus;
        [SerializeField, TextArea(4, 10)] private string lastRawResponse;
        [SerializeField, TextArea(4, 10)] private string lastExtractedStagePlan;

        private LlmRelayConfig config = new LlmRelayConfig();
        private UnityWebRequest activeRequest;
        private Coroutine activeCoroutine;
        private string configPath;
        private readonly List<BoneMapInstance> promptAxisInstances = new List<BoneMapInstance>();
        private readonly List<Transform> baseRotationChain = new List<Transform>();

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
            configPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ConfigRelativePath);
            initialized = ValidateReferences();

            if (initialized)
                ReloadConfig();
        }

        public bool ReloadConfig()
        {
            configLoaded = false;
            configReady = false;

            if (string.IsNullOrWhiteSpace(configPath))
                configPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), ConfigRelativePath);

            var path = configPath;
            if (!File.Exists(path))
            {
                config = new LlmRelayConfig();
                configStatus = $"Missing config: {path}";
                statusText = "Config missing.";
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
                return false;
            }

            config.Normalize();
            configLoaded = true;
            configReady = config.IsReady(out var reason);
            configStatus = configReady ? "Ready." : reason;
            statusText = configReady ? "Ready." : "Config incomplete.";

            if (autonomousBehaviorScheduler != null)
                autonomousBehaviorScheduler.SetUserInteractionTimeout(config.InteractionTimeoutSeconds);

            return configReady;
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
                lastRawResponse = textHandler.RawText;
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
                lastExtractedStagePlan = stageParser.Content;

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
            var builder = new StringBuilder(12288);
            AppendTargetCharacterSection(builder);
            AppendPromptSection(builder, "Character", LoadPromptText(characterPrompt, CharacterPromptFileName), false);
            AppendPromptSection(builder, "StagePlan Rules", LoadPromptText(stagePlanRulesPrompt, StagePlanRulesPromptFileName), true);
            AppendPromptSection(builder, "Parameter Bone Rules", LoadPromptText(parameterBonesPrompt, ParameterBonesPromptFileName), true);
            AppendPromptSection(builder, "Preset Action Rules", LoadPromptText(presetActionsPrompt, PresetActionsPromptFileName), true);
            AppendPromptSection(builder, "Locomotion Rules", LoadPromptText(locomotionPrompt, LocomotionPromptFileName), true);
            AppendPromptSection(builder, "Format Examples", LoadPromptText(examplesPrompt, ExamplesPromptFileName), true);
            AppendPromptSection(builder, "Long Term Memory", BuildMemoryPromptContext(), false);
            AppendPromptSection(builder, "Recent Momotalk Chat Context", historyContext, false);
            AppendRuntimeCapabilities(builder);
            return builder.ToString();
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

        private void AppendRuntimeCapabilities(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("## Runtime Generated Capabilities");
            builder.AppendLine("Use this generated list as the source of truth for exact callable names, enabled axes, ranges, durations, and scopes. This section is capability data, not motion examples.");

            builder.AppendLine();
            builder.AppendLine("### Controllable Semantic Bones");
            AppendBoneCapabilities(builder);

            builder.AppendLine();
            builder.AppendLine("### Primary Direction Single-Axis Effects");
            AppendPrimaryDirectionEffects(builder);

            builder.AppendLine();
            builder.AppendLine("### Preset Animations");
            AppendPresetAnimations(builder);

            builder.AppendLine();
            builder.AppendLine("### Locomotion Modes");
            AppendLocomotionModes(builder);

            builder.AppendLine();
            builder.AppendLine("### Expressions");
            AppendExpressions(builder);
        }

        private void AppendExpressions(StringBuilder builder)
        {
            var expressionProfile = characterProfile != null ? characterProfile.ExpressionProfile : null;
            if (expressionProfile == null || expressionProfile.Entries == null)
            {
                builder.AppendLine("- none configured");
                return;
            }

            var wroteAny = false;
            for (var i = 0; i < expressionProfile.Entries.Count; i++)
            {
                var entry = expressionProfile.Entries[i];
                if (entry == null || !entry.Enabled || string.IsNullOrWhiteSpace(entry.ExpressionName))
                    continue;

                builder.AppendLine("- " + entry.ExpressionName.Trim());
                wroteAny = true;
            }

            if (!wroteAny)
                builder.AppendLine("- none configured");
        }

        private void AppendLocomotionModes(StringBuilder builder)
        {
            if (locomotionProfile == null || locomotionProfile.Entries == null)
            {
                builder.AppendLine("- walk");
                builder.AppendLine("- run");
                return;
            }

            for (var i = 0; i < locomotionProfile.Entries.Count; i++)
            {
                var entry = locomotionProfile.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Mode))
                    continue;

                builder.AppendLine("- " + entry.Mode);
            }
        }

        private void AppendPresetAnimations(StringBuilder builder)
        {
            if (presetAnimationProfile == null || presetAnimationProfile.Entries == null)
                return;

            var descriptions = ParsePresetDescriptions();
            var totalSemanticConfigCount = boneMapProfile != null
                ? Mathf.Max(1, boneMapProfile.SemanticConfigCount)
                : 1;

            for (var i = 0; i < presetAnimationProfile.Entries.Count; i++)
            {
                var entry = presetAnimationProfile.Entries[i];
                if (entry == null || !entry.AllowCall || string.IsNullOrWhiteSpace(entry.ActionName))
                    continue;

                descriptions.TryGetValue(entry.ActionName, out var description);
                var duration = entry.Clip != null ? entry.Clip.length : 0f;
                var scope = GetPresetScope(entry, totalSemanticConfigCount);

                builder.Append("- ")
                    .Append(entry.ActionName)
                    .Append(" | duration=")
                    .Append(FormatFloat(duration))
                    .Append("s | scope=")
                    .Append(scope);

                if (!string.IsNullOrWhiteSpace(description))
                    builder.Append(" | description=").Append(description);

                builder.AppendLine();
            }
        }

        private void AppendBasePoseLocalAxisDirections(StringBuilder builder)
        {
            if (boneMapProfile == null || boneRoot == null || avatarPoseApplier == null)
            {
                builder.AppendLine("- Base axis directions unavailable: references missing.");
                return;
            }

            if (!avatarPoseApplier.HasBaseRotation)
            {
                builder.AppendLine("- BaseRotation not captured yet.");
                return;
            }

            var missingCount = boneMapProfile.BuildControlInstances(boneRoot, promptAxisInstances);
            if (missingCount > 0)
                builder.AppendLine("- Note: " + missingCount.ToString(CultureInfo.InvariantCulture) + " configured bone instance(s) are missing.");

            builder.AppendLine("- Direction components use character space: Right, Up, Forward.");
            builder.AppendLine("- Components below " + FormatAxisFloat(AxisComponentThreshold) + " are omitted.");
            builder.AppendLine("- Side bones list side=L only. For side=R, runtime mirrors the values; do not manually invert them.");

            for (var i = 0; i < promptAxisInstances.Count; i++)
            {
                var instance = promptAxisInstances[i];
                if (instance == null || instance.Entry == null)
                    continue;
                if (instance.SemanticBone == SemanticBone.Eye || instance.Entry.UsesPairedPaths)
                    continue;
                if (instance.Entry.HasSide && instance.Side != BoneSide.L)
                    continue;

                builder.Append("- ")
                    .Append(instance.SemanticBone);

                if (instance.Entry.HasSide)
                    builder.Append(" side=L");
                else
                    builder.Append(" side=none");

                if (!TryGetBasePoseRotation(instance.Transform, out var basePoseRotation))
                {
                    builder.AppendLine(" axis directions unavailable: BaseRotation missing.");
                    continue;
                }

                AppendAxisDirection(builder, "+X", basePoseRotation * Vector3.right, instance.Entry.IsAxisEnabled(0));
                AppendAxisDirection(builder, "+Y", basePoseRotation * Vector3.up, instance.Entry.IsAxisEnabled(1));
                AppendAxisDirection(builder, "+Z", basePoseRotation * Vector3.forward, instance.Entry.IsAxisEnabled(2));
                builder.AppendLine();
            }

            if (HasSemanticBone(SemanticBone.Eye))
                builder.AppendLine("- Eye side=none: paired eye control. Use X/Y only; Z is disabled. Axis directions are not listed because both eyes are controlled together.");
        }

        private void AppendPrimaryDirectionEffects(StringBuilder builder)
        {
            if (boneMapProfile == null || boneRoot == null || avatarPoseApplier == null)
            {
                builder.AppendLine("- Primary direction effects unavailable: references missing.");
                return;
            }

            if (!avatarPoseApplier.HasBaseRotation)
            {
                builder.AppendLine("- BaseRotation not captured yet.");
                return;
            }

            boneMapProfile.BuildControlInstances(boneRoot, promptAxisInstances);
            builder.AppendLine("- Effects describe the visible primary segment direction after a single-axis semantic rotation.");
            builder.AppendLine("- Side bones list side=L only. Use the same semantic values for side=R; runtime mirrors them internally.");

            for (var i = 0; i < promptAxisInstances.Count; i++)
            {
                var instance = promptAxisInstances[i];
                if (instance == null || instance.Entry == null)
                    continue;
                if (instance.SemanticBone == SemanticBone.Eye || instance.Entry.UsesPairedPaths)
                    continue;
                if (instance.Entry.HasSide && instance.Side != BoneSide.L)
                    continue;
                if (!TryGetPrimaryLocalDirection(instance.SemanticBone, out var primaryLocalDirection))
                    continue;
                if (!TryGetBasePoseRotation(instance.Transform, out var basePoseRotation))
                    continue;

                var baseDirection = basePoseRotation * primaryLocalDirection;
                builder.Append("- ")
                    .Append(instance.SemanticBone);

                if (instance.Entry.HasSide)
                    builder.Append(" side=L");
                else
                    builder.Append(" side=none");

                builder.Append(" primary=(");
                AppendDirectionComponents(builder, baseDirection);
                builder.Append(") effects:");
                AppendSingleAxisEffect(builder, "x+", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(0), new Vector3(PrimaryEffectAngle, 0f, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "x-", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(0), new Vector3(-PrimaryEffectAngle, 0f, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "y+", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(1), new Vector3(0f, PrimaryEffectAngle, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "y-", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(1), new Vector3(0f, -PrimaryEffectAngle, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "z+", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, PrimaryEffectAngle), baseDirection);
                AppendSingleAxisEffect(builder, "z-", basePoseRotation, primaryLocalDirection, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, -PrimaryEffectAngle), baseDirection);
                builder.AppendLine();
            }
        }

        private static void AppendSingleAxisEffect(
            StringBuilder builder,
            string label,
            Quaternion basePoseRotation,
            Vector3 primaryLocalDirection,
            bool enabled,
            Vector3 semanticRotation,
            Vector3 baseDirection)
        {
            builder.Append(' ')
                .Append(label)
                .Append('=');

            if (!enabled)
            {
                builder.Append("disabled");
                return;
            }

            var effectDirection = basePoseRotation * (Quaternion.Euler(semanticRotation) * primaryLocalDirection);
            if (Vector3.Dot(baseDirection.normalized, effectDirection.normalized) >= TwistDotThreshold)
            {
                builder.Append("twist/no swing");
                return;
            }

            builder.Append('(');
            AppendDirectionComponents(builder, effectDirection);
            builder.Append(')');
        }

        private static bool TryGetPrimaryLocalDirection(SemanticBone semanticBone, out Vector3 direction)
        {
            switch (semanticBone)
            {
                case SemanticBone.Pelvis:
                case SemanticBone.Spine:
                case SemanticBone.Chest:
                case SemanticBone.Neck:
                case SemanticBone.Clavicle:
                case SemanticBone.UpperArm:
                case SemanticBone.Forearm:
                case SemanticBone.Hand:
                case SemanticBone.Thigh:
                case SemanticBone.Calf:
                case SemanticBone.Toe:
                    direction = -Vector3.right;
                    return true;
                case SemanticBone.Head:
                    direction = Vector3.up;
                    return true;
                case SemanticBone.Foot:
                    direction = new Vector3(-0.69f, 0.73f, 0f).normalized;
                    return true;
                default:
                    direction = Vector3.zero;
                    return false;
            }
        }

        private bool TryGetBasePoseRotation(Transform bone, out Quaternion basePoseRotation)
        {
            basePoseRotation = Quaternion.identity;
            baseRotationChain.Clear();

            var current = bone;
            while (current != null)
            {
                baseRotationChain.Add(current);
                if (current == boneRoot)
                    break;

                current = current.parent;
            }

            if (baseRotationChain.Count == 0 || baseRotationChain[baseRotationChain.Count - 1] != boneRoot)
                return false;

            for (var i = baseRotationChain.Count - 1; i >= 0; i--)
            {
                if (!avatarPoseApplier.TryGetBaseRotation(baseRotationChain[i], out var baseRotation))
                    return false;

                basePoseRotation *= baseRotation;
            }

            return true;
        }

        private bool HasSemanticBone(SemanticBone semanticBone)
        {
            if (boneMapProfile == null || boneMapProfile.Entries == null)
                return false;

            for (var i = 0; i < boneMapProfile.Entries.Count; i++)
            {
                var entry = boneMapProfile.Entries[i];
                if (entry != null && entry.SemanticBone == semanticBone)
                    return true;
            }

            return false;
        }

        private static void AppendAxisDirection(StringBuilder builder, string axisName, Vector3 direction, bool enabled)
        {
            builder.Append(' ')
                .Append(axisName)
                .Append('=');

            if (!enabled)
            {
                builder.Append("disabled");
                return;
            }

            builder.Append('(');
            AppendDirectionComponents(builder, direction);
            builder.Append(')');
        }

        private static void AppendDirectionComponents(StringBuilder builder, Vector3 direction)
        {
            var wrote = false;
            AppendDirectionComponent(builder, direction.x, "Right", AxisComponentThreshold, ref wrote);
            AppendDirectionComponent(builder, direction.y, "Up", AxisComponentThreshold, ref wrote);
            AppendDirectionComponent(builder, direction.z, "Forward", AxisComponentThreshold, ref wrote);

            if (!wrote)
                AppendDominantDirectionComponent(builder, direction);
        }

        private static void AppendDominantDirectionComponent(StringBuilder builder, Vector3 direction)
        {
            var absoluteX = Mathf.Abs(direction.x);
            var absoluteY = Mathf.Abs(direction.y);
            var absoluteZ = Mathf.Abs(direction.z);
            var wrote = false;

            if (absoluteX >= absoluteY && absoluteX >= absoluteZ)
            {
                AppendDirectionComponent(builder, direction.x, "Right", 0f, ref wrote);
                return;
            }

            if (absoluteY >= absoluteZ)
            {
                AppendDirectionComponent(builder, direction.y, "Up", 0f, ref wrote);
                return;
            }

            AppendDirectionComponent(builder, direction.z, "Forward", 0f, ref wrote);
        }

        private static void AppendDirectionComponent(
            StringBuilder builder,
            float value,
            string label,
            float threshold,
            ref bool wrote)
        {
            if (Mathf.Abs(value) < threshold)
                return;

            if (wrote)
                builder.Append(", ");

            builder.Append(value >= 0f ? "+" : "-")
                .Append(label)
                .Append(' ')
                .Append(FormatAxisFloat(Mathf.Abs(value)));
            wrote = true;
        }

        private void AppendBoneCapabilities(StringBuilder builder)
        {
            if (boneMapProfile == null || boneMapProfile.Entries == null)
                return;

            for (var i = 0; i < boneMapProfile.Entries.Count; i++)
            {
                var entry = boneMapProfile.Entries[i];
                if (entry == null)
                    continue;

                builder.Append("- ").Append(entry.SemanticBone);
                if (entry.HasSide)
                    builder.Append(" side=L/R");
                else
                    builder.Append(" side=none");
                builder.Append(" axes=");
                var wroteAxis = false;
                AppendAxis(builder, entry, 0, "x", ref wroteAxis);
                AppendAxis(builder, entry, 1, "y", ref wroteAxis);
                AppendAxis(builder, entry, 2, "z", ref wroteAxis);
                if (!wroteAxis)
                    builder.Append("none");
                builder.Append(" range=")
                    .Append(FormatFloat(entry.RangeMin))
                    .Append("..")
                    .Append(FormatFloat(entry.RangeMax));
                builder.AppendLine();
            }
        }

        private Dictionary<string, string> ParsePresetDescriptions()
        {
            var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var promptText = LoadPromptText(presetActionsPrompt, PresetActionsPromptFileName);
            if (string.IsNullOrWhiteSpace(promptText))
                return descriptions;

            var lines = promptText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("|", StringComparison.Ordinal) || !line.EndsWith("|", StringComparison.Ordinal))
                    continue;
                if (line.Contains("---"))
                    continue;

                var cells = line.Trim('|').Split('|');
                if (cells.Length < 2)
                    continue;

                var actionName = cells[0].Trim();
                var description = cells[1].Trim();
                if (string.Equals(actionName, "Action", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                descriptions[actionName] = description;
            }

            return descriptions;
        }

        private string GetPresetScope(PresetAnimationEntry entry, int totalSemanticConfigCount)
        {
            var coveredCount = CountCoveredSemanticConfigs(entry);
            return coveredCount >= Mathf.CeilToInt(totalSemanticConfigCount * 0.6f)
                ? "fullBody"
                : "partial";
        }

        private int CountCoveredSemanticConfigs(PresetAnimationEntry presetEntry)
        {
            if (boneMapProfile == null || boneMapProfile.Entries == null || presetEntry == null)
                return 0;

            var count = 0;
            for (var i = 0; i < boneMapProfile.Entries.Count; i++)
            {
                var entry = boneMapProfile.Entries[i];
                if (entry == null)
                    continue;

                if (PresetCoversEntry(presetEntry, entry))
                    count++;
            }

            return count;
        }

        private static bool PresetCoversEntry(PresetAnimationEntry presetEntry, BoneMapEntry boneEntry)
        {
            if (presetEntry.BonePaths == null)
                return false;

            for (var i = 0; i < presetEntry.BonePaths.Count; i++)
            {
                var presetPath = presetEntry.BonePaths[i];
                if (PathMatchesConfiguredBone(presetPath, boneEntry.Path))
                    return true;
                if (PathMatchesConfiguredBone(presetPath, boneEntry.LeftPath))
                    return true;
                if (PathMatchesConfiguredBone(presetPath, boneEntry.RightPath))
                    return true;

                var pairedPaths = boneEntry.PairedPaths;
                for (var pairedIndex = 0; pairedIndex < pairedPaths.Count; pairedIndex++)
                {
                    if (PathMatchesConfiguredBone(presetPath, pairedPaths[pairedIndex]))
                        return true;
                }
            }

            return false;
        }

        private static bool PathMatchesConfiguredBone(string presetPath, string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(presetPath) || string.IsNullOrWhiteSpace(configuredPath))
                return false;

            var normalizedPresetPath = presetPath.Replace("\\", "/").Trim();
            var normalizedConfiguredPath = configuredPath.Replace("\\", "/").Trim();

            return string.Equals(normalizedPresetPath, normalizedConfiguredPath, StringComparison.OrdinalIgnoreCase)
                || normalizedPresetPath.EndsWith("/" + normalizedConfiguredPath, StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendAxis(StringBuilder builder, BoneMapEntry entry, int axis, string axisName, ref bool wroteAxis)
        {
            if (!entry.IsAxisEnabled(axis))
                return;

            if (wroteAxis)
                builder.Append('/');
            builder.Append(axisName);
            wroteAxis = true;
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

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatAxisFloat(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private sealed class StreamingTextDownloadHandler : DownloadHandlerScript
        {
            private readonly object gate = new object();
            private readonly Queue<string> chunks = new Queue<string>();
            private readonly StringBuilder rawText = new StringBuilder(8192);
            private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
            private readonly char[] charBuffer = new char[8192];

            public StreamingTextDownloadHandler()
                : base(new byte[4096])
            {
            }

            public string RawText
            {
                get
                {
                    lock (gate)
                        return rawText.ToString();
                }
            }

            public bool TryDequeueChunk(out string chunk)
            {
                lock (gate)
                {
                    if (chunks.Count == 0)
                    {
                        chunk = string.Empty;
                        return false;
                    }

                    chunk = chunks.Dequeue();
                    return true;
                }
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                    return true;

                lock (gate)
                {
                    var charCount = decoder.GetChars(data, 0, dataLength, charBuffer, 0, false);
                    if (charCount <= 0)
                        return true;

                    var chunk = new string(charBuffer, 0, charCount);
                    chunks.Enqueue(chunk);
                    rawText.Append(chunk);
                    return true;
                }
            }

            protected override void CompleteContent()
            {
                lock (gate)
                {
                    var charCount = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, true);
                    if (charCount <= 0)
                        return;

                    var chunk = new string(charBuffer, 0, charCount);
                    chunks.Enqueue(chunk);
                    rawText.Append(chunk);
                }
            }
        }

        private sealed class StreamingSseParser
        {
            private readonly StringBuilder lineBuffer = new StringBuilder(1024);
            private readonly StringBuilder eventData = new StringBuilder(1024);

            public bool Append(string chunk, out List<string> payloads, out string failureReason)
            {
                payloads = new List<string>();
                failureReason = string.Empty;
                if (string.IsNullOrEmpty(chunk))
                    return true;

                lineBuffer.Append(chunk);
                while (TryPopLine(out var line))
                    ProcessLine(line, payloads);

                return true;
            }

            public bool Complete(out List<string> payloads, out string failureReason)
            {
                payloads = new List<string>();
                failureReason = string.Empty;

                if (lineBuffer.Length > 0)
                {
                    var line = lineBuffer.ToString();
                    lineBuffer.Length = 0;
                    ProcessLine(TrimLineEnding(line), payloads);
                }

                FlushEvent(payloads);
                return true;
            }

            private bool TryPopLine(out string line)
            {
                for (var i = 0; i < lineBuffer.Length; i++)
                {
                    if (lineBuffer[i] != '\n')
                        continue;

                    line = TrimLineEnding(lineBuffer.ToString(0, i + 1));
                    lineBuffer.Remove(0, i + 1);
                    return true;
                }

                line = string.Empty;
                return false;
            }

            private void ProcessLine(string line, List<string> payloads)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushEvent(payloads);
                    return;
                }

                if (line.StartsWith(":", StringComparison.Ordinal))
                    return;

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    return;

                var data = line.Substring(5);
                if (data.StartsWith(" ", StringComparison.Ordinal))
                    data = data.Substring(1);

                if (eventData.Length > 0)
                    eventData.Append('\n');
                eventData.Append(data);
            }

            private void FlushEvent(List<string> payloads)
            {
                if (eventData.Length == 0)
                    return;

                payloads.Add(eventData.ToString());
                eventData.Length = 0;
            }

            private static string TrimLineEnding(string line)
            {
                return line.TrimEnd('\r', '\n');
            }
        }

        private sealed class StagePlanIncrementalParser
        {
            private readonly StringBuilder content = new StringBuilder(8192);
            private readonly Queue<string> stages = new Queue<string>();
            private bool stagesArrayFound;
            private bool stagesArrayClosed;
            private int stageScanIndex;

            public string Content => content.ToString();

            public void Append(string delta)
            {
                if (string.IsNullOrEmpty(delta))
                    return;

                content.Append(delta);
                ScanForStages();
            }

            public bool TryDequeueStage(out string stageJson)
            {
                if (stages.Count == 0)
                {
                    stageJson = string.Empty;
                    return false;
                }

                stageJson = stages.Dequeue();
                return true;
            }

            private void ScanForStages()
            {
                if (stagesArrayClosed)
                    return;

                var text = content.ToString();
                if (!stagesArrayFound)
                {
                    if (!TryFindStagesArrayStart(text, out stageScanIndex))
                        return;

                    stagesArrayFound = true;
                }

                while (stageScanIndex < text.Length)
                {
                    stageScanIndex = SkipWhitespaceAndCommas(text, stageScanIndex);
                    if (stageScanIndex >= text.Length)
                        return;

                    if (text[stageScanIndex] == ']')
                    {
                        stagesArrayClosed = true;
                        return;
                    }

                    if (text[stageScanIndex] != '{')
                        return;

                    if (!JsonTextUtility.TryExtractJsonObjectAt(text, stageScanIndex, out var stageJson, out var objectEndIndex))
                        return;

                    stages.Enqueue(stageJson);
                    stageScanIndex = objectEndIndex + 1;
                }
            }

            private static bool TryFindStagesArrayStart(string text, out int arrayContentStart)
            {
                arrayContentStart = -1;
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] != '"')
                        continue;

                    if (!TryReadJsonStringToken(text, i, out var token, out var nextIndex, out var complete))
                        return false;

                    if (!complete)
                        return false;

                    i = nextIndex - 1;
                    if (!string.Equals(token, "stages", StringComparison.Ordinal))
                        continue;

                    var colonIndex = SkipWhitespace(text, nextIndex);
                    if (colonIndex >= text.Length)
                        return false;
                    if (text[colonIndex] != ':')
                        continue;

                    var arrayIndex = SkipWhitespace(text, colonIndex + 1);
                    if (arrayIndex >= text.Length)
                        return false;
                    if (text[arrayIndex] != '[')
                        continue;

                    arrayContentStart = arrayIndex + 1;
                    return true;
                }

                return false;
            }

            private static bool TryReadJsonStringToken(
                string text,
                int quoteIndex,
                out string token,
                out int nextIndex,
                out bool complete)
            {
                token = string.Empty;
                nextIndex = quoteIndex;
                complete = false;

                if (quoteIndex < 0 || quoteIndex >= text.Length || text[quoteIndex] != '"')
                    return false;

                var builder = new StringBuilder();
                var escaping = false;
                for (var i = quoteIndex + 1; i < text.Length; i++)
                {
                    var c = text[i];
                    if (escaping)
                    {
                        builder.Append(c);
                        escaping = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaping = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        token = builder.ToString();
                        nextIndex = i + 1;
                        complete = true;
                        return true;
                    }

                    builder.Append(c);
                }

                return true;
            }

            private static int SkipWhitespaceAndCommas(string text, int index)
            {
                while (index < text.Length && (char.IsWhiteSpace(text[index]) || text[index] == ','))
                    index++;

                return index;
            }

            private static int SkipWhitespace(string text, int index)
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;

                return index;
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
