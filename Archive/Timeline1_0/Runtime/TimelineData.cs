using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class TimelineRootDto
    {
        public string schemaVersion;
        public TimelineSegmentDto[] timeline;
    }

    [Serializable]
    public sealed class TimelineSegmentDto
    {
        public float start;
        public float end;
        public TimelineActionDto[] actions;
    }

    [Serializable]
    public sealed class TimelineActionDto
    {
        public string type;
        public string text;
        public TimelineBonePoseDto[] bones;
        public string name;
        public string target;
        public string mode;
    }

    [Serializable]
    public sealed class TimelineBonePoseDto
    {
        public string bone;
        public string side;
        public TimelineRotationDto rotation;
    }

    [Serializable]
    public sealed class TimelineRotationDto
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    public sealed class TimelineValidationResult
    {
        public TimelineValidationResult(
            TimelineRootDto root,
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            Root = root;
            IsValid = isValid;
            Errors = errors;
            Warnings = warnings;
        }

        public TimelineRootDto Root { get; }
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int ErrorCount => Errors.Count;
        public int WarningCount => Warnings.Count;
    }
}
