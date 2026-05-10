using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Locomotion Profile")]
    public sealed class LocomotionProfile : ScriptableObject
    {
        [SerializeField] private LocomotionEntry[] entries = Array.Empty<LocomotionEntry>();

        public bool TryBuildClipBinding(
            string mode,
            Transform boneRoot,
            out LocomotionClipBinding binding,
            out string reason)
        {
            binding = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(mode))
            {
                reason = "Locomotion mode is missing.";
                return false;
            }

            var entry = FindEntry(mode);
            if (entry == null)
            {
                reason = $"Locomotion mode '{mode}' is not registered.";
                return false;
            }

            if (entry.Clip == null)
            {
                reason = $"Locomotion mode '{entry.Mode}' has no clip.";
                return false;
            }

            if (boneRoot == null)
            {
                reason = "Bone root reference is missing.";
                return false;
            }

            var targets = new List<LocomotionBoneTarget>();
            var missingCount = 0;
            var paths = entry.BonePaths;
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    missingCount++;
                    continue;
                }

                var transform = boneRoot.Find(path);
                if (transform == null)
                {
                    missingCount++;
                    continue;
                }

                targets.Add(new LocomotionBoneTarget(path, transform));
            }

            if (targets.Count == 0)
            {
                reason = $"Locomotion mode '{entry.Mode}' has no resolved bone paths.";
                return false;
            }

            if (missingCount > 0)
            {
                reason = $"Locomotion mode '{entry.Mode}' has {missingCount} missing bone path(s).";
                return false;
            }

            binding = new LocomotionClipBinding(entry, targets.ToArray());
            return true;
        }

        private LocomotionEntry FindEntry(string mode)
        {
            if (entries == null)
                return null;

            var trimmedMode = mode.Trim();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.Mode, trimmedMode, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class LocomotionEntry
    {
        [SerializeField] private string mode;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private float speed = 1f;
        [SerializeField] private bool loop = true;
        [SerializeField] private string[] bonePaths = Array.Empty<string>();

        public string Mode => mode;
        public AnimationClip Clip => clip;
        public float Speed => Mathf.Max(0f, speed);
        public bool Loop => loop;
        public IReadOnlyList<string> BonePaths => bonePaths ?? Array.Empty<string>();
    }

    public sealed class LocomotionClipBinding
    {
        public LocomotionClipBinding(LocomotionEntry entry, LocomotionBoneTarget[] targets)
        {
            Entry = entry;
            Targets = targets;
        }

        public LocomotionEntry Entry { get; }
        public LocomotionBoneTarget[] Targets { get; }
        public string Mode => Entry.Mode;
        public AnimationClip Clip => Entry.Clip;
        public float Speed => Entry.Speed;
        public bool Loop => Entry.Loop;
    }

    public sealed class LocomotionBoneTarget
    {
        public LocomotionBoneTarget(string path, Transform transform)
        {
            Path = path;
            Transform = transform;
        }

        public string Path { get; }
        public Transform Transform { get; }
        public string DisplayName => Transform != null ? Transform.name : Path;
    }

    public sealed class LocomotionBonePose
    {
        public LocomotionBonePose(Transform bone, string displayName, Quaternion localRotation)
        {
            Bone = bone;
            DisplayName = displayName;
            LocalRotation = localRotation;
        }

        public Transform Bone { get; }
        public string DisplayName { get; }
        public Quaternion LocalRotation { get; }
    }
}
