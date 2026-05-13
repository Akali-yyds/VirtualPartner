using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [Serializable]
    public sealed class StagePlanRootDto
    {
        public string schemaVersion;
        public string type;
        public StagePlanMetadataDto metadata;
        public StagePlanStageDto[] stages;
    }

    [Serializable]
    public sealed class StagePlanMetadataDto
    {
        public string intent;
        public string mood;
    }

    [Serializable]
    public sealed class StagePlanStageDto
    {
        public StagePlanActionDto[] actions;
    }

    [Serializable]
    public sealed class StagePlanActionDto
    {
        public string type;
        public string text;
        public string emotion;
        public float speed;
        public string voiceId;
        public string name;
        public float duration;
        public StagePlanBonePoseDto[] bones;
        public string target;
        public string mode;
    }

    [Serializable]
    public sealed class StagePlanBonePoseDto
    {
        public string bone;
        public string side;
        public StagePlanRotationDto rotation;
    }

    [Serializable]
    public sealed class StagePlanRotationDto
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    public sealed class StagePlanValidationResult
    {
        public StagePlanValidationResult(
            StagePlanRootDto root,
            bool isValid,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings,
            int validStageCount,
            int validActionCount)
        {
            Root = root;
            IsValid = isValid;
            Errors = errors;
            Warnings = warnings;
            ValidStageCount = validStageCount;
            ValidActionCount = validActionCount;
        }

        public StagePlanRootDto Root { get; }
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int ErrorCount => Errors.Count;
        public int WarningCount => Warnings.Count;
        public int ValidStageCount { get; }
        public int ValidActionCount { get; }
    }
}
