using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public static class StagePlanValidator
    {
        private const string SupportedSchemaVersion = "2.0";
        private const string SupportedType = "stagePlan";

        public static StagePlanValidationResult Validate(string json, CharacterProfile characterProfile)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var validStageCount = 0;
            var validActionCount = 0;

            if (characterProfile == null)
            {
                errors.Add("CharacterProfile is missing.");
                return BuildResult(null, errors, warnings, validStageCount, validActionCount);
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("StagePlan JSON is empty.");
                return BuildResult(null, errors, warnings, validStageCount, validActionCount);
            }

            if (ContainsForbiddenKey(json, "timeline"))
                errors.Add("StagePlan 2.0 must not contain timeline.");
            if (ContainsForbiddenKey(json, "stageId"))
                errors.Add("StagePlan 2.0 must not contain stageId. Runtime generates stageIndex from array order.");
            if (ContainsForbiddenKey(json, "start"))
                errors.Add("StagePlan 2.0 must not contain start.");
            if (ContainsForbiddenKey(json, "end"))
                errors.Add("StagePlan 2.0 must not contain end.");

            StagePlanRootDto root = null;
            try
            {
                root = JsonUtility.FromJson<StagePlanRootDto>(json);
            }
            catch (Exception exception)
            {
                errors.Add($"StagePlan JSON parse failed: {exception.Message}");
            }

            if (root == null)
            {
                errors.Add("StagePlan JSON parse returned no root object.");
                return BuildResult(null, errors, warnings, validStageCount, validActionCount);
            }

            if (string.IsNullOrWhiteSpace(root.schemaVersion))
                errors.Add("StagePlan JSON must contain schemaVersion.");
            else if (root.schemaVersion != SupportedSchemaVersion)
                errors.Add($"schemaVersion must be {SupportedSchemaVersion}.");

            if (string.IsNullOrWhiteSpace(root.type))
                errors.Add("StagePlan JSON must contain type.");
            else if (root.type != SupportedType)
                errors.Add($"type must be {SupportedType}.");

            if (root.stages == null || root.stages.Length == 0)
            {
                errors.Add("stages must contain at least one stage.");
                return BuildResult(root, errors, warnings, validStageCount, validActionCount);
            }

            ValidateStages(root.stages, characterProfile, errors, warnings, ref validStageCount, ref validActionCount);

            if (validActionCount == 0)
                errors.Add("StagePlan must contain at least one valid action.");

            return BuildResult(root, errors, warnings, validStageCount, validActionCount);
        }

        private static void ValidateStages(
            StagePlanStageDto[] stages,
            CharacterProfile characterProfile,
            List<string> errors,
            List<string> warnings,
            ref int validStageCount,
            ref int validActionCount)
        {
            for (var stageIndex = 0; stageIndex < stages.Length; stageIndex++)
            {
                var stage = stages[stageIndex];
                var stageLabel = FormatStageLabel(stageIndex);

                if (stage == null)
                {
                    warnings.Add($"Stage {stageIndex} is null and will be skipped.");
                    continue;
                }

                if (stage.actions == null)
                {
                    errors.Add($"{stageLabel} must contain actions.");
                    continue;
                }

                if (stage.actions.Length == 0)
                {
                    warnings.Add($"{stageLabel} has empty actions and will be skipped.");
                    continue;
                }

                var speechCount = CountSpeechActions(stage.actions);
                if (speechCount > 1)
                    errors.Add($"{stageLabel} must not contain more than one speech action.");

                var stageValidActionCount = 0;
                for (var actionIndex = 0; actionIndex < stage.actions.Length; actionIndex++)
                {
                    if (!ValidateAction(stageLabel, actionIndex, stage.actions[actionIndex], characterProfile, errors, warnings))
                        continue;

                    stageValidActionCount++;
                    validActionCount++;
                }

                if (stageValidActionCount > 0)
                    validStageCount++;
                else
                    warnings.Add($"{stageLabel} has no valid actions and will be skipped.");
            }
        }

        private static bool ValidateAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            CharacterProfile characterProfile,
            List<string> errors,
            List<string> warnings)
        {
            if (action == null)
            {
                warnings.Add($"{stageLabel} action {actionIndex} is null and will be skipped.");
                return false;
            }

            var actionType = NormalizeType(action.type);
            if (string.IsNullOrEmpty(actionType))
            {
                warnings.Add($"{stageLabel} action {actionIndex} is missing type and will be skipped.");
                return false;
            }

            switch (actionType)
            {
                case "speech":
                    return ValidateSpeechAction(stageLabel, actionIndex, action, errors, warnings);
                case "expression":
                    return ValidateExpressionAction(stageLabel, actionIndex, action, warnings);
                case "bonepose":
                    return ValidateBonePoseAction(stageLabel, actionIndex, action, characterProfile, warnings);
                case "animation":
                    return ValidateAnimationAction(stageLabel, actionIndex, action, characterProfile, warnings);
                case "facing":
                    return ValidateFacingAction(stageLabel, actionIndex, action, warnings);
                case "locomotion":
                    return ValidateLocomotionAction(stageLabel, actionIndex, action, characterProfile, warnings);
                default:
                    warnings.Add($"{stageLabel} action {actionIndex} type '{action.type}' is unknown and will be skipped.");
                    return false;
            }
        }

        private static bool ValidateSpeechAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            List<string> errors,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(action.text))
            {
                errors.Add($"{stageLabel} speech action {actionIndex} must contain text.");
                return false;
            }

            if (action.speed < 0f)
            {
                warnings.Add($"{stageLabel} speech action {actionIndex} has invalid speed and will be skipped.");
                return false;
            }

            if (!IsValidEmotion(action.emotion))
            {
                warnings.Add($"{stageLabel} speech action {actionIndex} has invalid emotion and will be skipped.");
                return false;
            }

            return true;
        }

        private static bool ValidateExpressionAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(action.name))
            {
                warnings.Add($"{stageLabel} expression action {actionIndex} is missing name and will be skipped.");
                return false;
            }

            if (!IsSupportedExpression(action.name))
            {
                warnings.Add($"{stageLabel} expression '{action.name}' is unknown and will be skipped.");
                return false;
            }

            WarnDurationDefault(stageLabel, actionIndex, action, warnings);
            return true;
        }

        private static bool ValidateBonePoseAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            CharacterProfile characterProfile,
            List<string> warnings)
        {
            if (characterProfile.BoneMapProfile == null)
            {
                warnings.Add($"{stageLabel} bonePose action {actionIndex} requires BoneMapProfile and will be skipped.");
                return false;
            }

            if (action.bones == null || action.bones.Length == 0)
            {
                warnings.Add($"{stageLabel} bonePose action {actionIndex} has no bones and will be skipped.");
                return false;
            }

            var validBoneCount = 0;
            for (var boneIndex = 0; boneIndex < action.bones.Length; boneIndex++)
            {
                if (ValidateBoneTarget(stageLabel, actionIndex, boneIndex, action.bones[boneIndex], characterProfile.BoneMapProfile, warnings))
                    validBoneCount++;
            }

            if (validBoneCount == 0)
            {
                warnings.Add($"{stageLabel} bonePose action {actionIndex} has no valid bones and will be skipped.");
                return false;
            }

            WarnDurationDefault(stageLabel, actionIndex, action, warnings);
            return true;
        }

        private static bool ValidateAnimationAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            CharacterProfile characterProfile,
            List<string> warnings)
        {
            if (characterProfile.PresetAnimationProfile == null)
            {
                warnings.Add($"{stageLabel} animation action {actionIndex} requires PresetAnimationProfile and will be skipped.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(action.name))
            {
                warnings.Add($"{stageLabel} animation action {actionIndex} is missing name and will be skipped.");
                return false;
            }

            if (!TryFindAnimationEntry(characterProfile.PresetAnimationProfile, action.name, out var reason))
            {
                warnings.Add($"{stageLabel} animation '{action.name}' {reason} and will be skipped.");
                return false;
            }

            return true;
        }

        private static bool ValidateFacingAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(action.target))
            {
                warnings.Add($"{stageLabel} facing action {actionIndex} is missing target and will be skipped.");
                return false;
            }

            if (!IsSupportedFacingTarget(action.target))
            {
                warnings.Add($"{stageLabel} facing target '{action.target}' is unknown and will be skipped.");
                return false;
            }

            WarnDurationDefault(stageLabel, actionIndex, action, warnings);
            return true;
        }

        private static bool ValidateLocomotionAction(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            CharacterProfile characterProfile,
            List<string> warnings)
        {
            if (characterProfile.LocomotionProfile == null)
            {
                warnings.Add($"{stageLabel} locomotion action {actionIndex} requires LocomotionProfile and will be skipped.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(action.mode))
            {
                warnings.Add($"{stageLabel} locomotion action {actionIndex} is missing mode and will be skipped.");
                return false;
            }

            if (!TryFindLocomotionEntry(characterProfile.LocomotionProfile, action.mode, out var reason))
            {
                warnings.Add($"{stageLabel} locomotion mode '{action.mode}' {reason} and will be skipped.");
                return false;
            }

            WarnDurationDefault(stageLabel, actionIndex, action, warnings);
            return true;
        }

        private static bool ValidateBoneTarget(
            string stageLabel,
            int actionIndex,
            int boneIndex,
            StagePlanBonePoseDto bonePose,
            BoneMapProfile boneMapProfile,
            List<string> warnings)
        {
            if (bonePose == null)
            {
                warnings.Add($"{stageLabel} bonePose action {actionIndex} bone {boneIndex} is null and will be skipped.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(bonePose.bone))
            {
                warnings.Add($"{stageLabel} bonePose action {actionIndex} bone {boneIndex} is missing bone and will be skipped.");
                return false;
            }

            if (!Enum.TryParse(bonePose.bone.Trim(), true, out SemanticBone semanticBone))
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' is unknown and will be skipped.");
                return false;
            }

            if (!TryFindBoneMapEntry(boneMapProfile, semanticBone, out var entry))
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' is not registered and will be skipped.");
                return false;
            }

            if (!TryParseBoneSide(bonePose.side, out var side))
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' has invalid side and will be skipped.");
                return false;
            }

            if (entry.HasSide && side == BoneSide.None)
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' requires side L or R and will be skipped.");
                return false;
            }

            if (!entry.HasSide && side != BoneSide.None)
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' does not support side and will be skipped.");
                return false;
            }

            if (bonePose.rotation == null)
            {
                warnings.Add($"{stageLabel} bonePose bone '{bonePose.bone}' is missing rotation and will be skipped.");
                return false;
            }

            return true;
        }

        private static int CountSpeechActions(StagePlanActionDto[] actions)
        {
            var count = 0;
            for (var i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null && NormalizeType(actions[i].type) == "speech")
                    count++;
            }

            return count;
        }

        private static bool TryFindAnimationEntry(PresetAnimationProfile profile, string actionName, out string reason)
        {
            var entries = profile.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !SameName(entry.ActionName, actionName))
                    continue;

                if (!entry.AllowCall)
                {
                    reason = "is disabled";
                    return false;
                }

                if (entry.Clip == null)
                {
                    reason = "has no clip";
                    return false;
                }

                reason = string.Empty;
                return true;
            }

            reason = "is not registered";
            return false;
        }

        private static bool TryFindLocomotionEntry(LocomotionProfile profile, string mode, out string reason)
        {
            var entries = profile.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !SameName(entry.Mode, mode))
                    continue;

                if (entry.Clip == null)
                {
                    reason = "has no clip";
                    return false;
                }

                reason = string.Empty;
                return true;
            }

            reason = "is not registered";
            return false;
        }

        private static bool TryFindBoneMapEntry(BoneMapProfile profile, SemanticBone semanticBone, out BoneMapEntry result)
        {
            var entries = profile.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.SemanticBone != semanticBone)
                    continue;

                result = entry;
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryParseBoneSide(string value, out BoneSide side)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                side = BoneSide.None;
                return true;
            }

            return Enum.TryParse(value.Trim(), true, out side);
        }

        private static void WarnDurationDefault(
            string stageLabel,
            int actionIndex,
            StagePlanActionDto action,
            List<string> warnings)
        {
            if (action.duration > 0f)
                return;

            warnings.Add($"{stageLabel} action {actionIndex} has duration <= 0. Later runtime should default this to 1 second.");
        }

        private static bool IsSupportedExpression(string expression)
        {
            var normalized = NormalizeType(expression);
            return normalized == "neutral"
                || normalized == "smile"
                || normalized == "thinking"
                || normalized == "surprised"
                || normalized == "embarrassed";
        }

        private static bool IsSupportedFacingTarget(string target)
        {
            var normalized = NormalizeType(target);
            return normalized == "camera"
                || normalized == "screenleft"
                || normalized == "screenright"
                || normalized == "screenforward"
                || normalized == "screenbackward";
        }

        private static bool IsValidEmotion(string emotion)
        {
            if (string.IsNullOrWhiteSpace(emotion))
                return true;

            return Regex.IsMatch(emotion.Trim(), "^[A-Za-z0-9_-]{1,32}$");
        }

        private static bool ContainsForbiddenKey(string json, string key)
        {
            return Regex.IsMatch(json, $"\"{Regex.Escape(key)}\"\\s*:", RegexOptions.IgnoreCase);
        }

        private static string NormalizeType(string actionType)
        {
            return string.IsNullOrWhiteSpace(actionType)
                ? string.Empty
                : actionType.Trim().ToLowerInvariant();
        }

        private static bool SameName(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatStageLabel(int index)
        {
            return $"stageIndex {index}";
        }

        private static StagePlanValidationResult BuildResult(
            StagePlanRootDto root,
            List<string> errors,
            List<string> warnings,
            int validStageCount,
            int validActionCount)
        {
            return new StagePlanValidationResult(root, errors.Count == 0, errors, warnings, validStageCount, validActionCount);
        }
    }
}
