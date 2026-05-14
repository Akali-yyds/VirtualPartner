using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [CreateAssetMenu(menuName = "VirtualPartner/Preset Animation Profile")]
    public sealed class PresetAnimationProfile : ScriptableObject
    {
        [SerializeField] private PresetAnimationEntry[] entries = Array.Empty<PresetAnimationEntry>();

        public IReadOnlyList<PresetAnimationEntry> Entries => entries;

        public bool TryBuildClipBinding(
            string actionName,
            Transform boneRoot,
            out PresetAnimationClipBinding binding,
            out string reason)
        {
            binding = null;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(actionName))
            {
                reason = "Animation name is missing.";
                return false;
            }

            var entry = FindEntry(actionName);
            if (entry == null)
            {
                reason = $"Preset animation '{actionName}' is not registered.";
                return false;
            }

            if (!entry.AllowCall)
            {
                reason = $"Preset animation '{entry.ActionName}' is disabled.";
                return false;
            }

            if (entry.Clip == null)
            {
                reason = $"Preset animation '{entry.ActionName}' has no clip.";
                return false;
            }

            if (boneRoot == null)
            {
                reason = "Bone root reference is missing.";
                return false;
            }

            var targets = new List<PresetAnimationBoneTarget>();
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

                targets.Add(new PresetAnimationBoneTarget(path, transform));
            }

            if (targets.Count == 0)
            {
                reason = $"Preset animation '{entry.ActionName}' has no resolved bone paths.";
                return false;
            }

            if (missingCount > 0)
            {
                reason = $"Preset animation '{entry.ActionName}' has {missingCount} missing bone path(s).";
                return false;
            }

            binding = new PresetAnimationClipBinding(entry, targets.ToArray());
            return true;
        }

        private PresetAnimationEntry FindEntry(string actionName)
        {
            if (entries == null)
                return null;

            var trimmedName = actionName.Trim();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.ActionName, trimmedName, StringComparison.OrdinalIgnoreCase))
                    return entry;
            }

            return null;
        }
    }

    [Serializable]
    public sealed class PresetAnimationEntry
    {
        [SerializeField] private string actionName;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private bool loop;
        [SerializeField] private bool allowCall = true;
        [SerializeField] private string[] bonePaths = Array.Empty<string>();

        public string ActionName => actionName;
        public AnimationClip Clip => clip;
        public bool Loop => loop;
        public bool AllowCall => allowCall;
        public IReadOnlyList<string> BonePaths => bonePaths ?? Array.Empty<string>();

#if UNITY_EDITOR
        internal void SetBonePaths(string[] paths)
        {
            bonePaths = paths ?? Array.Empty<string>();
        }
#endif
    }

    public sealed class PresetAnimationClipBinding
    {
        public PresetAnimationClipBinding(PresetAnimationEntry entry, PresetAnimationBoneTarget[] targets)
        {
            Entry = entry;
            Targets = targets;
        }

        public PresetAnimationEntry Entry { get; }
        public PresetAnimationBoneTarget[] Targets { get; }
        public string ActionName => Entry.ActionName;
        public AnimationClip Clip => Entry.Clip;
        public bool Loop => Entry.Loop;
    }

    public sealed class PresetAnimationBoneTarget
    {
        public PresetAnimationBoneTarget(string path, Transform transform)
        {
            Path = path;
            Transform = transform;
        }

        public string Path { get; }
        public Transform Transform { get; }
        public string DisplayName => Transform != null ? Transform.name : Path;
    }

    public sealed class PresetAnimationBonePose
    {
        public PresetAnimationBonePose(Transform bone, string displayName, Quaternion localRotation, Vector3 localPosition)
        {
            Bone = bone;
            DisplayName = displayName;
            LocalRotation = localRotation;
            LocalPosition = localPosition;
        }

        public Transform Bone { get; }
        public string DisplayName { get; }
        public Quaternion LocalRotation { get; }
        public Vector3 LocalPosition { get; }
    }
}
