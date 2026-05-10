using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TimelinePlayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BoneMapProfile boneMapProfile;
        [SerializeField] private PresetAnimationProfile presetAnimationProfile;
        [SerializeField] private LocomotionProfile locomotionProfile;
        [SerializeField] private GameObject characterRoot;
        [SerializeField] private Transform boneRoot;
        [SerializeField] private AvatarPoseApplier avatarPoseApplier;
        [SerializeField] private ActionCoordinator actionCoordinator;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;
        [SerializeField] private SpeechBubbleView speechBubbleView;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool playing;
        [SerializeField] private float currentTime;
        [SerializeField] private int activeSegmentIndex = -1;
        [SerializeField] private int validationErrorCount;
        [SerializeField] private int validationWarningCount;
        [SerializeField] private int runtimeWarningCount;
        [SerializeField] private int activePresetAnimationCount;
        [SerializeField] private bool locomotionActive;
        [SerializeField] private string currentSegmentStatus = "-";
        [SerializeField] private string statusText = "Idle";
        [SerializeField] private string lastMessage;
        [SerializeField] private string activeOwnerId;

        private readonly List<BoneMapInstance> controlInstances = new List<BoneMapInstance>();
        private readonly Dictionary<Transform, ActiveTimelineBone> activeBones = new Dictionary<Transform, ActiveTimelineBone>();
        private readonly List<Transform> releaseBuffer = new List<Transform>();
        private readonly List<ActivePresetAnimation> activePresetAnimations = new List<ActivePresetAnimation>();
        private readonly List<PresetAnimationBonePose> sampledPresetPoses = new List<PresetAnimationBonePose>();
        private readonly List<string> displacedPresetIds = new List<string>();
        private readonly List<PresetTransformSnapshot> presetTransformSnapshots = new List<PresetTransformSnapshot>();

        private TimelineSegmentDto[] activeTimeline;
        private float timelineEnd;
        private int presetSequence;

        public bool IsPlaying => playing;
        public float CurrentTime => currentTime;
        public int ActiveSegmentIndex => activeSegmentIndex;
        public int ErrorCount => validationErrorCount;
        public int WarningCount => validationWarningCount + runtimeWarningCount;
        public string CurrentSegmentStatus => currentSegmentStatus;
        public string StatusText => statusText;
        public string LastMessage => lastMessage;
        public string ActiveOwnerId => activeOwnerId;
        public bool LocomotionActive => locomotionActive;

        public void Configure(
            BoneMapProfile profile,
            PresetAnimationProfile presetProfile,
            LocomotionProfile locomotion,
            GameObject character,
            Transform root,
            AvatarPoseApplier poseApplier,
            ActionCoordinator coordinator,
            RootOrientationController orientationController,
            LocomotionActionExecutor locomotionExecutor,
            SpeechBubbleView speechView)
        {
            boneMapProfile = profile;
            presetAnimationProfile = presetProfile;
            locomotionProfile = locomotion;
            characterRoot = character;
            boneRoot = root;
            avatarPoseApplier = poseApplier;
            actionCoordinator = coordinator;
            rootOrientationController = orientationController;
            locomotionActionExecutor = locomotionExecutor;
            speechBubbleView = speechView;
            initialized = ValidateReferences();

            if (!initialized)
                return;

            var missingCount = boneMapProfile.BuildControlInstances(boneRoot, controlInstances);
            if (missingCount > 0)
                RecordWarning($"Timeline BoneMap resolved with {missingCount} missing bone instance(s).");

            statusText = "Ready";
        }

        public void Configure(
            BoneMapProfile profile,
            Transform root,
            ActionCoordinator coordinator,
            SpeechBubbleView speechView)
        {
            Configure(
                profile,
                presetAnimationProfile,
                locomotionProfile,
                characterRoot,
                root,
                avatarPoseApplier,
                coordinator,
                rootOrientationController,
                locomotionActionExecutor,
                speechView);
        }

        public TimelineValidationResult ValidateTimelineJson(string json)
        {
            var result = TimelineValidator.Validate(json);
            validationErrorCount = result.ErrorCount;
            validationWarningCount = result.WarningCount;
            runtimeWarningCount = 0;
            lastMessage = result.IsValid ? "Validation passed." : "Validation failed.";
            statusText = result.IsValid ? "Validated" : "Validation Failed";

            LogValidationMessages(result);
            return result;
        }

        public bool PlayJson(string json)
        {
            return StartTimeline(json, string.Empty);
        }

        public bool ReplaceJson(string json)
        {
            return StartTimeline(json, string.Empty);
        }

        public bool PlayJsonForOwner(string json, string ownerId)
        {
            return StartTimeline(json, ownerId);
        }

        public bool IsOwnerPlaying(string ownerId)
        {
            return playing && SameOwner(activeOwnerId, ownerId);
        }

        public bool StopTimelineForOwner(string ownerId)
        {
            if (!IsOwnerPlaying(ownerId))
                return false;

            StopTimeline();
            return true;
        }

        public void StopTimeline()
        {
            StopActiveTimeline(true, true);
            statusText = "Stopped";
            lastMessage = "Timeline stopped.";
        }

        public void ManualUpdate(float deltaTime)
        {
            ManualUpdate(deltaTime, null, 0f);
        }

        public void ManualUpdate(float deltaTime, AnimationClip idleClip, float idleTime)
        {
            if (!playing || activeTimeline == null)
                return;

            currentTime += Mathf.Max(0f, deltaTime);
            var nextSegmentIndex = FindActiveSegmentIndex(currentTime);
            if (nextSegmentIndex != activeSegmentIndex)
                EnterSegment(nextSegmentIndex);

            if (rootOrientationController != null)
                rootOrientationController.ManualUpdate(deltaTime);
            if (locomotionActionExecutor != null)
            {
                locomotionActionExecutor.ManualUpdate(deltaTime, idleClip, idleTime);
                locomotionActive = locomotionActionExecutor.IsActive;
            }
            UpdateActivePresetAnimations(idleClip, idleTime);
            StopPresetAnimationsThatLostOwnership();

            if (currentTime >= timelineEnd && nextSegmentIndex < 0)
            {
                playing = false;
                activeOwnerId = string.Empty;
                statusText = "Finished";
                lastMessage = "Timeline finished.";
            }
        }

        private bool StartTimeline(string json, string ownerId)
        {
            if (!initialized && !ValidateReferences())
                return false;

            if (controlInstances.Count == 0)
                boneMapProfile.BuildControlInstances(boneRoot, controlInstances);

            var result = ValidateTimelineJson(json);
            if (!result.IsValid)
                return false;

            StopActiveTimeline(true, true);

            activeTimeline = SortSegments(result.Root.timeline);
            timelineEnd = GetTimelineEnd(activeTimeline);
            currentTime = 0f;
            activeSegmentIndex = -2;
            playing = true;
            runtimeWarningCount = 0;
            activeOwnerId = NormalizeOwnerId(ownerId);
            statusText = "Playing";
            lastMessage = "Timeline started.";

            EnterSegment(FindActiveSegmentIndex(currentTime));
            return true;
        }

        private void StopActiveTimeline(bool clearSpeech, bool releaseTimelineBones)
        {
            playing = false;
            currentTime = 0f;
            activeSegmentIndex = -1;
            activePresetAnimationCount = 0;
            locomotionActive = false;
            currentSegmentStatus = "-";
            activeTimeline = null;
            timelineEnd = 0f;
            activeOwnerId = string.Empty;

            if (clearSpeech && speechBubbleView != null)
                speechBubbleView.Clear();

            if (releaseTimelineBones)
            {
                ReleaseActiveBones();
                ReleaseActivePresetAnimations();
                if (locomotionActionExecutor != null)
                    locomotionActionExecutor.StopLocomotion();
                if (rootOrientationController != null)
                    rootOrientationController.StopTimelineFacing();
                if (actionCoordinator != null)
                {
                    actionCoordinator.ReleaseAllTimeline();
                    actionCoordinator.ReleaseAllPresetAnimations();
                    actionCoordinator.ReleaseAllLocomotion();
                }
            }
            else
            {
                activeBones.Clear();
                activePresetAnimations.Clear();
                locomotionActive = false;
            }
        }

        private void EnterSegment(int segmentIndex)
        {
            activeSegmentIndex = segmentIndex;
            if (locomotionActionExecutor != null)
                locomotionActionExecutor.StopLocomotion();
            if (rootOrientationController != null)
                rootOrientationController.CompleteTimelineFacing();
            locomotionActive = false;

            if (segmentIndex < 0 || activeTimeline == null || segmentIndex >= activeTimeline.Length)
            {
                currentSegmentStatus = "-";
                if (speechBubbleView != null)
                    speechBubbleView.Clear();
                ReleaseActiveBones();
                ReleaseActivePresetAnimations();
                return;
            }

            var segment = activeTimeline[segmentIndex];
            currentSegmentStatus = $"{segmentIndex}: {segment.start:0.###}-{segment.end:0.###}";
            ReleaseActivePresetAnimations();
            ApplySpeech(segment, segmentIndex);
            ApplyDesiredBones(BuildDesiredBones(segment, segmentIndex));
            BuildActivePresetAnimations(segment, segmentIndex);
            ApplyFacing(segment, segmentIndex);
            StartLocomotion(segment, segmentIndex);
        }

        private void ApplySpeech(TimelineSegmentDto segment, int segmentIndex)
        {
            if (speechBubbleView == null)
                return;

            var speechText = string.Empty;
            var speechCount = 0;

            for (var i = 0; i < segment.actions.Length; i++)
            {
                var action = segment.actions[i];
                if (action == null || NormalizeType(action.type) != "speech")
                    continue;

                speechCount++;
                if (string.IsNullOrWhiteSpace(speechText))
                    speechText = action.text;
            }

            if (speechCount > 1)
                RecordWarning($"Segment {segmentIndex} has multiple speech actions. The first non-empty text is shown.");

            if (string.IsNullOrWhiteSpace(speechText))
                speechBubbleView.Clear();
            else
                speechBubbleView.Show(speechText);
        }

        private void ApplyFacing(TimelineSegmentDto segment, int segmentIndex)
        {
            if (rootOrientationController == null)
                return;

            for (var i = 0; i < segment.actions.Length; i++)
            {
                var action = segment.actions[i];
                if (action == null || NormalizeType(action.type) != "facing")
                    continue;

                var duration = Mathf.Max(0f, segment.end - segment.start);
                if (!rootOrientationController.RequestTimelineFacing(action.target, duration, out var failureReason))
                    RecordWarning($"Segment {segmentIndex} facing skipped: {failureReason}");
                return;
            }
        }

        private void StartLocomotion(TimelineSegmentDto segment, int segmentIndex)
        {
            if (locomotionActionExecutor == null)
                return;

            for (var i = 0; i < segment.actions.Length; i++)
            {
                var action = segment.actions[i];
                if (action == null || NormalizeType(action.type) != "locomotion")
                    continue;

                var duration = Mathf.Max(0f, segment.end - segment.start);
                if (!locomotionActionExecutor.StartLocomotion(action.mode, duration, out var failureReason))
                {
                    RecordWarning($"Segment {segmentIndex} locomotion skipped: {failureReason}");
                    locomotionActive = false;
                    return;
                }

                locomotionActive = true;
                return;
            }
        }

        private Dictionary<Transform, DesiredTimelineBone> BuildDesiredBones(TimelineSegmentDto segment, int segmentIndex)
        {
            var desiredBones = new Dictionary<Transform, DesiredTimelineBone>();

            for (var actionIndex = 0; actionIndex < segment.actions.Length; actionIndex++)
            {
                var action = segment.actions[actionIndex];
                if (action == null)
                    continue;

                if (NormalizeType(action.type) != "bonepose")
                    continue;

                if (action.bones == null)
                    continue;

                for (var boneIndex = 0; boneIndex < action.bones.Length; boneIndex++)
                {
                    var bonePose = action.bones[boneIndex];
                    if (!TryBuildDesiredBone(segmentIndex, bonePose, out var desiredBone))
                        continue;

                    if (desiredBones.ContainsKey(desiredBone.Instance.Transform))
                        RecordWarning($"Segment {segmentIndex} has duplicate target for {desiredBone.Instance.DisplayName}. The last one wins.");

                    desiredBones[desiredBone.Instance.Transform] = desiredBone;
                }
            }

            return desiredBones;
        }

        private void BuildActivePresetAnimations(TimelineSegmentDto segment, int segmentIndex)
        {
            for (var actionIndex = 0; actionIndex < segment.actions.Length; actionIndex++)
            {
                var action = segment.actions[actionIndex];
                if (action == null || NormalizeType(action.type) != "animation")
                    continue;

                if (!presetAnimationProfile.TryBuildClipBinding(action.name, boneRoot, out var binding, out var reason))
                {
                    RecordWarning($"Segment {segmentIndex} animation skipped: {reason}");
                    continue;
                }

                presetSequence++;
                var instanceId = $"{segmentIndex}:{actionIndex}:{binding.ActionName}:{presetSequence}";
                activePresetAnimations.Add(new ActivePresetAnimation(instanceId, binding, segment.start, segment.end));
            }

            activePresetAnimationCount = activePresetAnimations.Count;
        }

        private void UpdateActivePresetAnimations(AnimationClip idleClip, float idleTime)
        {
            for (var i = 0; i < activePresetAnimations.Count; i++)
            {
                var activePreset = activePresetAnimations[i];
                if (currentTime >= activePreset.SegmentEnd)
                {
                    StopActivePresetAnimationAt(i, true);
                    i--;
                    continue;
                }

                var clipTime = activePreset.GetClipTime(currentTime);
                if (!TrySamplePresetAnimation(activePreset.Binding, clipTime, idleClip, idleTime, sampledPresetPoses, out var sampleFailure))
                {
                    RecordWarning($"Preset animation '{activePreset.Binding.ActionName}' stopped: {sampleFailure}");
                    StopActivePresetAnimationAt(i, true);
                    i--;
                    continue;
                }

                displacedPresetIds.Clear();
                if (!actionCoordinator.RequestPresetAnimation(
                        activePreset.InstanceId,
                        activePreset.Binding.ActionName,
                        sampledPresetPoses,
                        displacedPresetIds,
                        out var requestFailure))
                {
                    RecordWarning($"Preset animation '{activePreset.Binding.ActionName}' stopped: {requestFailure}");
                    StopActivePresetAnimationAt(i, true);
                    i--;
                    continue;
                }

                activePreset.Started = true;
                RemoveDisplacedPresetAnimations(displacedPresetIds);
                var currentIndex = IndexOfPresetAnimation(activePreset.InstanceId);
                if (currentIndex >= 0)
                    i = currentIndex;
                activePresetAnimationCount = activePresetAnimations.Count;
            }
        }

        private bool TrySamplePresetAnimation(
            PresetAnimationClipBinding binding,
            float clipTime,
            AnimationClip idleClip,
            float idleTime,
            List<PresetAnimationBonePose> poses,
            out string failureReason)
        {
            poses.Clear();
            failureReason = string.Empty;

            if (binding == null || binding.Clip == null)
            {
                failureReason = "Clip binding is missing.";
                return false;
            }

            if (characterRoot == null)
            {
                failureReason = "Character root reference is missing.";
                return false;
            }

            if (boneRoot == null)
            {
                failureReason = "Bone root reference is missing.";
                return false;
            }

            var rootTransform = characterRoot.transform;
            var rootPosition = rootTransform.position;
            var rootRotation = rootTransform.rotation;
            var rootLocalScale = rootTransform.localScale;
            CapturePresetTransformSnapshot();

            try
            {
                binding.Clip.SampleAnimation(characterRoot, clipTime);

                var targets = binding.Targets;
                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target == null || target.Transform == null)
                    {
                        failureReason = $"Preset animation '{binding.ActionName}' has a missing target transform.";
                        return false;
                    }

                    poses.Add(new PresetAnimationBonePose(target.Transform, target.DisplayName, target.Transform.localRotation));
                }
            }
            catch (Exception exception)
            {
                failureReason = exception.Message;
                return false;
            }
            finally
            {
                RestorePresetTransformSnapshot();
                rootTransform.position = rootPosition;
                rootTransform.rotation = rootRotation;
                rootTransform.localScale = rootLocalScale;
            }

            return poses.Count > 0;
        }

        private void CapturePresetTransformSnapshot()
        {
            presetTransformSnapshots.Clear();

            if (boneRoot == null)
                return;

            var transforms = boneRoot.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                presetTransformSnapshots.Add(new PresetTransformSnapshot(transforms[i]));
        }

        private void RestorePresetTransformSnapshot()
        {
            for (var i = 0; i < presetTransformSnapshots.Count; i++)
                presetTransformSnapshots[i].Restore();

            presetTransformSnapshots.Clear();
        }

        private void StopPresetAnimationsThatLostOwnership()
        {
            for (var i = 0; i < activePresetAnimations.Count; i++)
            {
                var activePreset = activePresetAnimations[i];
                if (!activePreset.Started)
                    continue;

                if (StillOwnsAllPresetBones(activePreset))
                    continue;

                RecordWarning($"Preset animation '{activePreset.Binding.ActionName}' stopped because a required bone was taken by a higher priority owner.");
                StopActivePresetAnimationAt(i, true);
                i--;
            }
        }

        private bool StillOwnsAllPresetBones(ActivePresetAnimation activePreset)
        {
            var targets = activePreset.Binding.Targets;
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || !actionCoordinator.IsPresetAnimationOwner(target.Transform, activePreset.InstanceId))
                    return false;
            }

            return true;
        }

        private void RemoveDisplacedPresetAnimations(List<string> presetIds)
        {
            for (var idIndex = 0; idIndex < presetIds.Count; idIndex++)
            {
                var presetId = presetIds[idIndex];
                for (var i = activePresetAnimations.Count - 1; i >= 0; i--)
                {
                    if (activePresetAnimations[i].InstanceId != presetId)
                        continue;

                    RecordWarning($"Preset animation '{activePresetAnimations[i].Binding.ActionName}' stopped because a later preset animation took one of its bones.");
                    activePresetAnimations.RemoveAt(i);
                }
            }
        }

        private int IndexOfPresetAnimation(string presetId)
        {
            for (var i = 0; i < activePresetAnimations.Count; i++)
            {
                if (activePresetAnimations[i].InstanceId == presetId)
                    return i;
            }

            return -1;
        }

        private void StopActivePresetAnimationAt(int index, bool releaseOwner)
        {
            if (index < 0 || index >= activePresetAnimations.Count)
                return;

            var activePreset = activePresetAnimations[index];
            if (releaseOwner && actionCoordinator != null)
                actionCoordinator.ReleasePresetAnimation(activePreset.InstanceId);

            activePresetAnimations.RemoveAt(index);
            activePresetAnimationCount = activePresetAnimations.Count;
        }

        private void ReleaseActivePresetAnimations()
        {
            if (actionCoordinator != null)
            {
                for (var i = 0; i < activePresetAnimations.Count; i++)
                    actionCoordinator.ReleasePresetAnimation(activePresetAnimations[i].InstanceId);
            }

            activePresetAnimations.Clear();
            activePresetAnimationCount = 0;
        }

        private bool TryBuildDesiredBone(
            int segmentIndex,
            TimelineBonePoseDto bonePose,
            out DesiredTimelineBone desiredBone)
        {
            desiredBone = null;

            if (bonePose == null)
            {
                RecordWarning($"Segment {segmentIndex} bonePose contains a null bone entry.");
                return false;
            }

            if (bonePose.rotation == null)
            {
                RecordWarning($"Segment {segmentIndex} bonePose '{bonePose.bone}' is missing rotation.");
                return false;
            }

            if (!TryResolveBoneInstance(bonePose.bone, bonePose.side, out var instance, out var reason))
            {
                RecordWarning($"Segment {segmentIndex} bonePose skipped: {reason}");
                return false;
            }

            var rotation = bonePose.rotation.ToVector3();
            var clampedRotation = instance.Entry.ClampRotation(rotation);
            if (!Approximately(rotation, clampedRotation))
                RecordWarning($"Segment {segmentIndex} {instance.DisplayName} rotation was clamped from {Format(rotation)} to {Format(clampedRotation)}.");

            desiredBone = new DesiredTimelineBone(instance, clampedRotation);
            return true;
        }

        private void ApplyDesiredBones(Dictionary<Transform, DesiredTimelineBone> desiredBones)
        {
            releaseBuffer.Clear();
            foreach (var pair in activeBones)
            {
                if (!desiredBones.ContainsKey(pair.Key))
                    releaseBuffer.Add(pair.Key);
            }

            for (var i = 0; i < releaseBuffer.Count; i++)
            {
                var transform = releaseBuffer[i];
                var activeBone = activeBones[transform];
                if (!activeBone.Blocked)
                    actionCoordinator.ReleaseTimeline(activeBone.Instance);
                activeBones.Remove(transform);
            }

            foreach (var desiredBone in desiredBones.Values)
            {
                activeBones.TryGetValue(desiredBone.Instance.Transform, out var activeBone);
                if (activeBone != null && activeBone.SameTarget(desiredBone))
                    continue;

                if (actionCoordinator.RequestTimelineBonePose(desiredBone.Instance, desiredBone.Rotation, out var failureReason))
                {
                    activeBones[desiredBone.Instance.Transform] = new ActiveTimelineBone(desiredBone.Instance, desiredBone.Rotation, false);
                    continue;
                }

                if (activeBone != null && !activeBone.Blocked)
                    actionCoordinator.ReleaseTimeline(activeBone.Instance);

                activeBones[desiredBone.Instance.Transform] = new ActiveTimelineBone(desiredBone.Instance, desiredBone.Rotation, true);
                RecordWarning($"Timeline request skipped for {desiredBone.Instance.DisplayName}: {failureReason}");
            }
        }

        private void ReleaseActiveBones()
        {
            foreach (var activeBone in activeBones.Values)
            {
                if (!activeBone.Blocked)
                    actionCoordinator.ReleaseTimeline(activeBone.Instance);
            }

            activeBones.Clear();
        }

        private bool TryResolveBoneInstance(
            string boneName,
            string sideName,
            out BoneMapInstance instance,
            out string reason)
        {
            instance = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(boneName))
            {
                reason = "Bone name is missing.";
                return false;
            }

            if (!Enum.TryParse(boneName, true, out SemanticBone semanticBone))
            {
                reason = $"Unknown semantic bone '{boneName}'.";
                return false;
            }

            var side = BoneSide.None;
            if (!string.IsNullOrWhiteSpace(sideName) && !Enum.TryParse(sideName, true, out side))
            {
                reason = $"Invalid side '{sideName}' for {semanticBone}.";
                return false;
            }

            var hasSideVariant = false;
            for (var i = 0; i < controlInstances.Count; i++)
            {
                var candidate = controlInstances[i];
                if (candidate.SemanticBone != semanticBone)
                    continue;

                if (candidate.Side != BoneSide.None)
                    hasSideVariant = true;

                if (candidate.Side != side)
                    continue;

                instance = candidate;
                return true;
            }

            reason = hasSideVariant && side == BoneSide.None
                ? $"{semanticBone} requires side L or R."
                : $"BoneMap has no instance for {semanticBone} {side}.";
            return false;
        }

        private int FindActiveSegmentIndex(float time)
        {
            if (activeTimeline == null)
                return -1;

            for (var i = 0; i < activeTimeline.Length; i++)
            {
                var segment = activeTimeline[i];
                if (segment != null && segment.start <= time && time < segment.end)
                    return i;
            }

            return -1;
        }

        private static TimelineSegmentDto[] SortSegments(TimelineSegmentDto[] segments)
        {
            var sorted = new TimelineSegmentDto[segments.Length];
            Array.Copy(segments, sorted, segments.Length);
            Array.Sort(sorted, (left, right) => left.start.CompareTo(right.start));
            return sorted;
        }

        private static float GetTimelineEnd(TimelineSegmentDto[] segments)
        {
            var end = 0f;
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                    end = Mathf.Max(end, segments[i].end);
            }

            return end;
        }

        private bool ValidateReferences()
        {
            if (boneMapProfile == null)
                return Fail("BoneMapProfile reference is missing.");
            if (presetAnimationProfile == null)
                return Fail("PresetAnimationProfile reference is missing.");
            if (locomotionProfile == null)
                return Fail("LocomotionProfile reference is missing.");
            if (characterRoot == null)
                return Fail("Character root reference is missing.");
            if (boneRoot == null)
                return Fail("Bone root reference is missing.");
            if (avatarPoseApplier == null)
                return Fail("AvatarPoseApplier reference is missing.");
            if (actionCoordinator == null)
                return Fail("ActionCoordinator reference is missing.");
            if (rootOrientationController == null)
                return Fail("RootOrientationController reference is missing.");
            if (locomotionActionExecutor == null)
                return Fail("LocomotionActionExecutor reference is missing.");
            if (speechBubbleView == null)
                return Fail("SpeechBubbleView reference is missing.");

            initialized = true;
            return true;
        }

        private bool Fail(string message)
        {
            statusText = "Failed";
            lastMessage = message;
            Debug.LogError($"[VirtualPartner] TimelinePlayer failed: {message}", this);
            enabled = false;
            return false;
        }

        private void LogValidationMessages(TimelineValidationResult result)
        {
            for (var i = 0; i < result.Errors.Count; i++)
                Debug.LogError($"[VirtualPartner] Timeline validation error: {result.Errors[i]}", this);
            for (var i = 0; i < result.Warnings.Count; i++)
                Debug.LogWarning($"[VirtualPartner] Timeline validation warning: {result.Warnings[i]}", this);
        }

        private void RecordWarning(string message)
        {
            runtimeWarningCount++;
            lastMessage = message;
            Debug.LogWarning($"[VirtualPartner] Timeline warning: {message}", this);
        }

        private static string NormalizeType(string actionType)
        {
            return string.IsNullOrWhiteSpace(actionType)
                ? string.Empty
                : actionType.Trim().ToLowerInvariant();
        }

        private static string NormalizeOwnerId(string ownerId)
        {
            return string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : ownerId.Trim();
        }

        private static bool SameOwner(string left, string right)
        {
            return string.Equals(
                NormalizeOwnerId(left),
                NormalizeOwnerId(right),
                StringComparison.Ordinal);
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return Mathf.Abs(left.x - right.x) < 0.001f
                && Mathf.Abs(left.y - right.y) < 0.001f
                && Mathf.Abs(left.z - right.z) < 0.001f;
        }

        private static string Format(Vector3 value)
        {
            return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
        }

        private sealed class DesiredTimelineBone
        {
            public DesiredTimelineBone(BoneMapInstance instance, Vector3 rotation)
            {
                Instance = instance;
                Rotation = rotation;
            }

            public BoneMapInstance Instance { get; }
            public Vector3 Rotation { get; }
        }

        private sealed class ActiveTimelineBone
        {
            public ActiveTimelineBone(BoneMapInstance instance, Vector3 rotation, bool blocked)
            {
                Instance = instance;
                Rotation = rotation;
                Blocked = blocked;
            }

            public BoneMapInstance Instance { get; }
            public Vector3 Rotation { get; }
            public bool Blocked { get; }

            public bool SameTarget(DesiredTimelineBone desired)
            {
                return Instance.Transform == desired.Instance.Transform
                    && !Blocked
                    && Approximately(Rotation, desired.Rotation);
            }
        }

        private sealed class ActivePresetAnimation
        {
            public ActivePresetAnimation(
                string instanceId,
                PresetAnimationClipBinding binding,
                float segmentStart,
                float segmentEnd)
            {
                InstanceId = instanceId;
                Binding = binding;
                SegmentStart = segmentStart;
                SegmentEnd = segmentEnd;
            }

            public string InstanceId { get; }
            public PresetAnimationClipBinding Binding { get; }
            public float SegmentStart { get; }
            public float SegmentEnd { get; }
            public bool Started { get; set; }

            public float GetClipTime(float timelineTime)
            {
                var clip = Binding.Clip;
                var elapsed = Mathf.Max(0f, timelineTime - SegmentStart);
                if (clip == null || clip.length <= 0f)
                    return 0f;

                return Binding.Loop
                    ? Mathf.Repeat(elapsed, clip.length)
                    : Mathf.Min(elapsed, clip.length);
            }
        }

        private readonly struct PresetTransformSnapshot
        {
            public PresetTransformSnapshot(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }

            public Transform Transform { get; }
            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 LocalScale { get; }

            public void Restore()
            {
                if (Transform == null)
                    return;

                Transform.localPosition = LocalPosition;
                Transform.localRotation = LocalRotation;
                Transform.localScale = LocalScale;
            }
        }
    }
}
