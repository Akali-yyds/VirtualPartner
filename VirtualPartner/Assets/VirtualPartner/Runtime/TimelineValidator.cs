using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public static class TimelineValidator
    {
        private const string SupportedSchemaVersion = "1.0";

        public static TimelineValidationResult Validate(string json)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add("Timeline JSON is empty.");
                return new TimelineValidationResult(null, false, errors, warnings);
            }

            if (!json.Contains("\"schemaVersion\""))
                errors.Add("Timeline JSON must contain schemaVersion.");
            if (!json.Contains("\"timeline\""))
                errors.Add("Timeline JSON must contain timeline.");

            TimelineRootDto root = null;
            try
            {
                root = JsonUtility.FromJson<TimelineRootDto>(json);
            }
            catch (Exception exception)
            {
                errors.Add($"Timeline JSON parse failed: {exception.Message}");
            }

            if (root == null)
            {
                errors.Add("Timeline JSON parse returned no root object.");
                return new TimelineValidationResult(null, false, errors, warnings);
            }

            if (root.schemaVersion != SupportedSchemaVersion)
                errors.Add($"schemaVersion must be {SupportedSchemaVersion}.");

            if (root.timeline == null || root.timeline.Length == 0)
            {
                errors.Add("timeline must contain at least one segment.");
                return new TimelineValidationResult(root, errors.Count == 0, errors, warnings);
            }

            ValidateSegments(root.timeline, errors, warnings);
            return new TimelineValidationResult(root, errors.Count == 0, errors, warnings);
        }

        private static void ValidateSegments(
            TimelineSegmentDto[] segments,
            List<string> errors,
            List<string> warnings)
        {
            var orderedSegments = new List<TimelineSegmentDto>(segments);
            orderedSegments.Sort((left, right) => left.start.CompareTo(right.start));

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (segment == null)
                {
                    warnings.Add($"Segment {i} is null and will be skipped.");
                    continue;
                }

                if (segment.end <= segment.start)
                    errors.Add($"Segment {i} must have end greater than start.");

                if (segment.actions == null)
                    errors.Add($"Segment {i} must contain actions.");
                else
                    ValidateActions(i, segment.actions, warnings);
            }

            for (var i = 1; i < orderedSegments.Count; i++)
            {
                var previous = orderedSegments[i - 1];
                var current = orderedSegments[i];
                if (previous == null || current == null)
                    continue;

                if (current.start < previous.end)
                    errors.Add($"Timeline segments overlap near start {current.start:0.###}.");
            }
        }

        private static void ValidateActions(
            int segmentIndex,
            TimelineActionDto[] actions,
            List<string> warnings)
        {
            for (var i = 0; i < actions.Length; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    warnings.Add($"Segment {segmentIndex} action {i} is null and will be skipped.");
                    continue;
                }

                var actionType = NormalizeType(action.type);
                if (string.IsNullOrEmpty(actionType))
                {
                    warnings.Add($"Segment {segmentIndex} action {i} is missing type and will be skipped.");
                    continue;
                }

                if (actionType == "speech")
                {
                    if (string.IsNullOrWhiteSpace(action.text))
                        warnings.Add($"Segment {segmentIndex} speech action has empty text.");
                    continue;
                }

                if (actionType == "bonepose")
                {
                    if (action.bones == null || action.bones.Length == 0)
                        warnings.Add($"Segment {segmentIndex} bonePose action has no bones.");
                    continue;
                }

                if (actionType == "animation" || actionType == "facing" || actionType == "locomotion")
                {
                    warnings.Add($"Segment {segmentIndex} action '{action.type}' is recognized but unsupported in stage 4.");
                    continue;
                }

                warnings.Add($"Segment {segmentIndex} action '{action.type}' is unknown and will be skipped.");
            }
        }

        private static string NormalizeType(string actionType)
        {
            return string.IsNullOrWhiteSpace(actionType)
                ? string.Empty
                : actionType.Trim().ToLowerInvariant();
        }
    }
}
