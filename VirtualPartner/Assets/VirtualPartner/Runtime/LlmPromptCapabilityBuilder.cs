using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    /// <summary>
    /// Generates the "Runtime Generated Capabilities" prompt section: controllable
    /// bones, primary direction effects, hand/wrist orientation hints, preset
    /// animations, locomotion modes, and expressions.
    /// </summary>
    public sealed class LlmPromptCapabilityBuilder
    {
        private const float AxisComponentThreshold = 0.1f;
        private const float PrimaryEffectAngle = 90f;
        private const float TwistDotThreshold = 0.98f;

        private BoneMapProfile boneMapProfile;
        private CharacterProfile characterProfile;
        private Transform boneRoot;
        private AvatarPoseApplier avatarPoseApplier;
        private PresetAnimationProfile presetAnimationProfile;
        private LocomotionProfile locomotionProfile;

        private readonly List<BoneMapInstance> promptAxisInstances = new List<BoneMapInstance>();
        private readonly List<Transform> baseRotationChain = new List<Transform>();

        public void Configure(
            BoneMapProfile boneProfile,
            CharacterProfile profile,
            Transform root,
            AvatarPoseApplier poseApplier,
            PresetAnimationProfile presetProfile,
            LocomotionProfile locomotion)
        {
            boneMapProfile = boneProfile;
            characterProfile = profile;
            boneRoot = root;
            avatarPoseApplier = poseApplier;
            presetAnimationProfile = presetProfile;
            locomotionProfile = locomotion;
        }

        public void Append(StringBuilder builder, string presetActionsPromptText)
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
            builder.AppendLine("### Hand And Wrist Orientation Hints");
            AppendHandAndWristOrientationHints(builder);

            builder.AppendLine();
            builder.AppendLine("### Preset Animations");
            AppendPresetAnimations(builder, presetActionsPromptText);

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

        private void AppendPresetAnimations(StringBuilder builder, string presetActionsPromptText)
        {
            if (presetAnimationProfile == null || presetAnimationProfile.Entries == null)
                return;

            var descriptions = ParsePresetDescriptions(presetActionsPromptText);
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
            builder.AppendLine("- A 'roll' axis twists the segment: it does not change where the limb points, but it rotates the segment's facing. The value shown is where the segment's side reference faces after the roll. Use roll axes to orient hands, wrists, and palms.");
            builder.AppendLine("- Combining several axes at once (including roll) and using larger angles is expected for expressive gestures and hand poses; do not restrict yourself to a single small axis when the pose needs more.");
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

                var rollReferenceLocal = ComputeRollReferenceLocal(primaryLocalDirection);
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
                AppendSingleAxisEffect(builder, "x+", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(0), new Vector3(PrimaryEffectAngle, 0f, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "x-", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(0), new Vector3(-PrimaryEffectAngle, 0f, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "y+", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(1), new Vector3(0f, PrimaryEffectAngle, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "y-", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(1), new Vector3(0f, -PrimaryEffectAngle, 0f), baseDirection);
                AppendSingleAxisEffect(builder, "z+", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, PrimaryEffectAngle), baseDirection);
                AppendSingleAxisEffect(builder, "z-", basePoseRotation, primaryLocalDirection, rollReferenceLocal, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, -PrimaryEffectAngle), baseDirection);
                builder.AppendLine();
            }
        }

        private void AppendHandAndWristOrientationHints(StringBuilder builder)
        {
            if (boneMapProfile == null || boneRoot == null || avatarPoseApplier == null)
            {
                builder.AppendLine("- Hand orientation hints unavailable: references missing.");
                return;
            }

            if (!avatarPoseApplier.HasBaseRotation)
            {
                builder.AppendLine("- BaseRotation not captured yet.");
                return;
            }

            boneMapProfile.BuildControlInstances(boneRoot, promptAxisInstances);
            builder.AppendLine("- Use Clavicle, UpperArm, and Forearm to place the hand near the target area.");
            builder.AppendLine("- Use Forearm and Hand axes, especially roll-like axes, to orient the wrist or palm.");
            builder.AppendLine("- `facingRef` is a perpendicular orientation reference for the segment in character space. It is approximate palm/wrist-facing guidance, not an IK target.");
            builder.AppendLine("- Side bones list side=L only. Use the same semantic values for side=R; runtime mirrors them internally.");

            var wroteAny = false;
            for (var i = 0; i < promptAxisInstances.Count; i++)
            {
                var instance = promptAxisInstances[i];
                if (instance == null || instance.Entry == null)
                    continue;
                if (instance.Entry.UsesPairedPaths || instance.Side != BoneSide.L)
                    continue;
                if (instance.SemanticBone != SemanticBone.Forearm && instance.SemanticBone != SemanticBone.Hand)
                    continue;
                if (!TryGetPrimaryLocalDirection(instance.SemanticBone, out var primaryLocalDirection))
                    continue;
                if (!TryGetBasePoseRotation(instance.Transform, out var basePoseRotation))
                    continue;

                var facingReferenceLocal = ComputeRollReferenceLocal(primaryLocalDirection);
                var basePointing = basePoseRotation * primaryLocalDirection;
                var baseFacing = basePoseRotation * facingReferenceLocal;

                builder.Append("- ")
                    .Append(instance.SemanticBone)
                    .Append(" side=L pointing=(");
                AppendDirectionComponents(builder, basePointing);
                builder.Append(") facingRef=(");
                AppendDirectionComponents(builder, baseFacing);
                builder.Append(") facingEffects:");
                AppendFacingEffect(builder, "x+", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(0), new Vector3(PrimaryEffectAngle, 0f, 0f));
                AppendFacingEffect(builder, "x-", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(0), new Vector3(-PrimaryEffectAngle, 0f, 0f));
                AppendFacingEffect(builder, "y+", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(1), new Vector3(0f, PrimaryEffectAngle, 0f));
                AppendFacingEffect(builder, "y-", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(1), new Vector3(0f, -PrimaryEffectAngle, 0f));
                AppendFacingEffect(builder, "z+", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, PrimaryEffectAngle));
                AppendFacingEffect(builder, "z-", basePoseRotation, facingReferenceLocal, instance.Entry.IsAxisEnabled(2), new Vector3(0f, 0f, -PrimaryEffectAngle));
                builder.AppendLine();
                wroteAny = true;
            }

            if (!wroteAny)
                builder.AppendLine("- no Forearm or Hand controls configured");
        }

        private static void AppendSingleAxisEffect(
            StringBuilder builder,
            string label,
            Quaternion basePoseRotation,
            Vector3 primaryLocalDirection,
            Vector3 rollReferenceLocal,
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
                // Twist/roll axis: pointing direction is unchanged, so report how the
                // segment's perpendicular side reference reorients instead.
                var effectRoll = basePoseRotation * (Quaternion.Euler(semanticRotation) * rollReferenceLocal);
                builder.Append("roll(");
                AppendDirectionComponents(builder, effectRoll);
                builder.Append(')');
                return;
            }

            builder.Append('(');
            AppendDirectionComponents(builder, effectDirection);
            builder.Append(')');
        }

        private static void AppendFacingEffect(
            StringBuilder builder,
            string label,
            Quaternion basePoseRotation,
            Vector3 facingReferenceLocal,
            bool enabled,
            Vector3 semanticRotation)
        {
            builder.Append(' ')
                .Append(label)
                .Append('=');

            if (!enabled)
            {
                builder.Append("disabled");
                return;
            }

            var effectFacing = basePoseRotation * (Quaternion.Euler(semanticRotation) * facingReferenceLocal);
            builder.Append('(');
            AppendDirectionComponents(builder, effectFacing);
            builder.Append(')');
        }

        // A unit local direction perpendicular to the primary (long-axis) direction, used
        // to describe how a roll/twist reorients the segment's facing.
        private static Vector3 ComputeRollReferenceLocal(Vector3 primaryLocalDirection)
        {
            var reference = Vector3.up - Vector3.Project(Vector3.up, primaryLocalDirection);
            if (reference.sqrMagnitude < 0.0001f)
                reference = Vector3.forward - Vector3.Project(Vector3.forward, primaryLocalDirection);

            return reference.sqrMagnitude < 0.0001f ? Vector3.forward : reference.normalized;
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

        private static Dictionary<string, string> ParsePresetDescriptions(string promptText)
        {
            var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatAxisFloat(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
