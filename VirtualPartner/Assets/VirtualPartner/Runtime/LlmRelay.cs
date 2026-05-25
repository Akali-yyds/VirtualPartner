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

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool configLoaded;
        [SerializeField] private bool configReady;
        [SerializeField] private bool requestPending;
        [SerializeField] private int latestRequestId;
        [SerializeField] private int pendingRequestId;
        [SerializeField] private string statusText = "Not configured.";
        [SerializeField] private string configStatus = "Not loaded.";
        [SerializeField] private string lastError;
        [SerializeField] private string lastPromptStatus;
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
        public int LatestRequestId => latestRequestId;
        public int PendingRequestId => pendingRequestId;
        public string StatusText => statusText;
        public string ConfigStatus => configStatus;
        public string LastError => lastError;
        public string LastPromptStatus => lastPromptStatus;
        public string LastRawResponse => lastRawResponse;
        public string LastExtractedStagePlan => lastExtractedStagePlan;
        public float InteractionTimeoutSeconds => config.InteractionTimeoutSeconds;
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
            var requestJson = BuildRequestJson(userText, historyContext);
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

        private string BuildRequestJson(string userText)
        {
            return BuildRequestJson(userText, string.Empty);
        }

        private string BuildRequestJson(string userText, string historyContext)
        {
            var systemPrompt = BuildSystemPrompt();
            var developerPrompt = BuildDeveloperPrompt(historyContext);
            var combinedSystemPrompt = config.supportsDeveloperRole
                ? systemPrompt
                : systemPrompt + "\n\n" + developerPrompt;

            var builder = new StringBuilder(4096);
            builder.Append("{\"model\":\"").Append(EscapeJson(config.model)).Append("\",\"messages\":[");
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

            var trimmed = StripCodeFence(content);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                failureReason = "LLM returned empty content.";
                return false;
            }

            if (TryExtractFirstJsonObject(trimmed, out stagePlanJson))
                return true;

            failureReason = "LLM content does not contain a JSON object.";
            return false;
        }

        private static bool TryExtractFirstJsonObject(string content, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var start = content.IndexOf('{');
            if (start < 0)
                return false;

            var depth = 0;
            var inString = false;
            var escaping = false;
            for (var i = start; i < content.Length; i++)
            {
                var c = content[i];
                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaping = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                {
                    json = content.Substring(start, i - start + 1);
                    return true;
                }
            }

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

        private static string StripCodeFence(string content)
        {
            var trimmed = content == null ? string.Empty : content.Trim();
            if (!trimmed.StartsWith("```", StringComparison.Ordinal))
                return trimmed;

            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline < 0 || lastFence <= firstNewline)
                return trimmed;

            return trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
        }

        private void RecordError(string message)
        {
            RecordError(0, message);
        }

        private void RecordError(int requestId, string message)
        {
            lastError = string.IsNullOrWhiteSpace(message) ? "Unknown LLM error." : message;
            statusText = "Error.";
            Debug.LogWarning($"[VirtualPartner] LlmRelay: {lastError}", this);
            RequestFailed?.Invoke(new LlmRequestFailure(requestId, lastError));
        }

        private bool FailSubmit(string message)
        {
            return FailSubmitResult(message).Accepted;
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
                .Append(EscapeJson(role))
                .Append("\",\"content\":\"")
                .Append(EscapeJson(content))
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

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 16);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
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
        private sealed class ChatCompletionError
        {
            public string message;
        }
    }
}
