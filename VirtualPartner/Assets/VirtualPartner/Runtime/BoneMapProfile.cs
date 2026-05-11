using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum SemanticBone
    {
        Pelvis,
        Spine,
        Chest,
        Neck,
        Head,
        Clavicle,
        UpperArm,
        Forearm,
        Hand,
        Thigh,
        Calf,
        Foot,
        Toe,
        Eye
    }

    public enum BoneSide
    {
        None,
        L,
        R
    }

    [CreateAssetMenu(menuName = "VirtualPartner/Bone Map Profile")]
    public sealed class BoneMapProfile : ScriptableObject
    {
        [SerializeField] private BoneMapEntry[] entries = Array.Empty<BoneMapEntry>();

        public IReadOnlyList<BoneMapEntry> Entries => entries;
        public int SemanticConfigCount => entries == null ? 0 : entries.Length;

        public int ControlInstanceCount
        {
            get
            {
                if (entries == null)
                    return 0;

                var count = 0;
                for (var i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null)
                        count += entries[i].ControlInstanceCount;
                }

                return count;
            }
        }

        public int BuildControlInstances(Transform boneRoot, List<BoneMapInstance> results)
        {
            results.Clear();

            if (boneRoot == null || entries == null)
                return 0;

            var missingCount = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null)
                    continue;

                if (entry.HasSide)
                {
                    AddInstance(boneRoot, results, entry, BoneSide.L, entry.LeftPath, ref missingCount);
                    AddInstance(boneRoot, results, entry, BoneSide.R, entry.RightPath, ref missingCount);
                    continue;
                }

                if (entry.UsesPairedPaths)
                {
                    AddPairedInstance(boneRoot, results, entry, ref missingCount);
                    continue;
                }

                AddInstance(boneRoot, results, entry, BoneSide.None, entry.Path, ref missingCount);
            }

            return missingCount;
        }

        private static void AddPairedInstance(
            Transform boneRoot,
            List<BoneMapInstance> results,
            BoneMapEntry entry,
            ref int missingCount)
        {
            var paths = entry.PairedPaths;
            if (paths == null || paths.Count == 0)
            {
                missingCount++;
                return;
            }

            var transforms = new List<Transform>();
            var resolvedPaths = new List<string>();
            var missingPairMember = false;

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];
                if (string.IsNullOrWhiteSpace(path))
                {
                    missingCount++;
                    missingPairMember = true;
                    continue;
                }

                var transform = FindBone(boneRoot, path);
                if (transform == null)
                {
                    missingCount++;
                    missingPairMember = true;
                    continue;
                }

                transforms.Add(transform);
                resolvedPaths.Add(path);
            }

            if (missingPairMember || transforms.Count == 0)
                return;

            results.Add(new BoneMapInstance(
                entry,
                BoneSide.None,
                resolvedPaths[0],
                transforms.ToArray(),
                resolvedPaths.ToArray()));
        }

        private static void AddInstance(
            Transform boneRoot,
            List<BoneMapInstance> results,
            BoneMapEntry entry,
            BoneSide side,
            string path,
            ref int missingCount)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                missingCount++;
                return;
            }

            var transform = FindBone(boneRoot, path);
            if (transform == null)
            {
                missingCount++;
                return;
            }

            results.Add(new BoneMapInstance(entry, side, path, transform));
        }

        private static Transform FindBone(Transform root, string path)
        {
            if (root == null || string.IsNullOrWhiteSpace(path))
                return null;

            var direct = root.Find(path);
            if (direct != null)
                return direct;

            var children = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i].name == path)
                    return children[i];
            }

            return null;
        }
    }

    [Serializable]
    public sealed class BoneMapEntry
    {
        [SerializeField] private SemanticBone semanticBone;
        [SerializeField] private bool hasSide;
        [SerializeField] private string path;
        [SerializeField] private string leftPath;
        [SerializeField] private string rightPath;
        [SerializeField] private string[] pairedPaths = Array.Empty<string>();
        [SerializeField] private Vector3 axes = Vector3.one;
        [Tooltip("Stage 2 debug initial range only. Later stages will tighten final LLM capability ranges.")]
        [SerializeField] private Vector2 range = new Vector2(-45f, 45f);
        [SerializeField] private Vector3 rightMirrorSign = new Vector3(-1f, -1f, 1f);

        public SemanticBone SemanticBone => semanticBone;
        public bool HasSide => hasSide;
        public string Path => path;
        public string LeftPath => leftPath;
        public string RightPath => rightPath;
        public IReadOnlyList<string> PairedPaths => pairedPaths ?? Array.Empty<string>();
        public bool UsesPairedPaths => !hasSide && pairedPaths != null && pairedPaths.Length > 0;
        public float RangeMin => range.x;
        public float RangeMax => range.y;

        public int ControlInstanceCount
        {
            get
            {
                if (!hasSide)
                {
                    if (UsesPairedPaths)
                        return 1;

                    return string.IsNullOrWhiteSpace(path) ? 0 : 1;
                }

                var count = 0;
                if (!string.IsNullOrWhiteSpace(leftPath))
                    count++;
                if (!string.IsNullOrWhiteSpace(rightPath))
                    count++;
                return count;
            }
        }

        public bool IsAxisEnabled(int axis)
        {
            return Mathf.Abs(axes[axis]) > 0.5f;
        }

        public Vector3 ClampRotation(Vector3 rotation)
        {
            return new Vector3(
                ClampAxis(rotation.x, 0),
                ClampAxis(rotation.y, 1),
                ClampAxis(rotation.z, 2));
        }

        public Vector3 GetMirrorSign(BoneSide side)
        {
            return side == BoneSide.R ? rightMirrorSign : Vector3.one;
        }

        private float ClampAxis(float value, int axis)
        {
            if (!IsAxisEnabled(axis))
                return 0f;

            return Mathf.Clamp(value, range.x, range.y);
        }
    }

    public sealed class BoneMapInstance
    {
        public BoneMapInstance(BoneMapEntry entry, BoneSide side, string path, Transform transform)
            : this(entry, side, path, new[] { transform }, new[] { path })
        {
        }

        public BoneMapInstance(BoneMapEntry entry, BoneSide side, string path, Transform[] transforms, string[] paths)
        {
            Entry = entry;
            Side = side;
            Path = path;
            Transforms = transforms ?? Array.Empty<Transform>();
            Paths = paths ?? Array.Empty<string>();
        }

        public BoneMapEntry Entry { get; }
        public BoneSide Side { get; }
        public string Path { get; }
        public Transform Transform => Transforms.Count > 0 ? Transforms[0] : null;
        public IReadOnlyList<Transform> Transforms { get; }
        public IReadOnlyList<string> Paths { get; }
        public SemanticBone SemanticBone => Entry.SemanticBone;
        public string DisplayName => Side == BoneSide.None ? SemanticBone.ToString() : $"{SemanticBone} {Side}";
    }
}
