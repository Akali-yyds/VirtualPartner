using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Expression Profile")]
    public sealed class ExpressionProfile : ScriptableObject
    {
        [SerializeField] private ExpressionProfileEntry[] entries = Array.Empty<ExpressionProfileEntry>();

        public IReadOnlyList<ExpressionProfileEntry> Entries => entries;

        public bool TryFindEntry(string expressionName, out ExpressionProfileEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(expressionName) || entries == null)
                return false;

            for (var i = 0; i < entries.Length; i++)
            {
                var candidate = entries[i];
                if (candidate == null || !SameName(candidate.ExpressionName, expressionName))
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        public bool IsExpressionEnabled(string expressionName)
        {
            return TryFindEntry(expressionName, out var entry) && entry.Enabled;
        }

        private static bool SameName(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Serializable]
    public sealed class ExpressionProfileEntry
    {
        [SerializeField] private string expressionName = "neutral";
        [SerializeField] private bool enabled = true;
        [SerializeField] private string mouthPoseName = "neutral";
        [SerializeField] private ExpressionFaceBonePose[] faceBonePoses = Array.Empty<ExpressionFaceBonePose>();

        public string ExpressionName => expressionName;
        public bool Enabled => enabled;
        public string MouthPoseName => mouthPoseName;
        public IReadOnlyList<ExpressionFaceBonePose> FaceBonePoses => faceBonePoses;
    }

    [Serializable]
    public sealed class ExpressionFaceBonePose
    {
        [SerializeField] private SemanticBone bone;
        [SerializeField] private BoneSide side;
        [SerializeField] private Vector3 rotation;

        public SemanticBone Bone => bone;
        public BoneSide Side => side;
        public Vector3 Rotation => rotation;
    }
}
