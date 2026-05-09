using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AvatarPoseApplier : MonoBehaviour
    {
        [SerializeField] private GameObject targetRoot;
        [SerializeField] private Transform boneRoot;

        [Header("Runtime Status")]
        [SerializeField] private bool hasBaseRotation;
        [SerializeField] private int registeredBoneCount;
        [SerializeField] private bool isApplying;
        [SerializeField] private float lastSampleTime;

        private Transform[] registeredBones;
        private Quaternion[] baseLocalRotations;

        public bool HasBaseRotation => hasBaseRotation;
        public int RegisteredBoneCount => registeredBoneCount;
        public bool IsApplying => isApplying;
        public float LastSampleTime => lastSampleTime;

        public void Configure(GameObject target, Transform root)
        {
            targetRoot = target;
            boneRoot = root;
            hasBaseRotation = false;
            registeredBoneCount = 0;
            isApplying = false;
            lastSampleTime = 0f;
            registeredBones = null;
            baseLocalRotations = null;
        }

        public int CaptureBaseRotations()
        {
            if (boneRoot == null)
            {
                hasBaseRotation = false;
                registeredBoneCount = 0;
                return 0;
            }

            registeredBones = boneRoot.GetComponentsInChildren<Transform>(true);
            baseLocalRotations = new Quaternion[registeredBones.Length];

            for (var i = 0; i < registeredBones.Length; i++)
                baseLocalRotations[i] = registeredBones[i].localRotation;

            registeredBoneCount = registeredBones.Length;
            hasBaseRotation = registeredBoneCount > 0;
            return registeredBoneCount;
        }

        public void ApplyIdle(AnimationClip clip, float sampleTime)
        {
            if (!hasBaseRotation || targetRoot == null || clip == null)
                return;

            clip.SampleAnimation(targetRoot, sampleTime);
            lastSampleTime = sampleTime;
            isApplying = true;
        }

        public bool TryGetBaseRotation(Transform bone, out Quaternion baseRotation)
        {
            baseRotation = Quaternion.identity;

            if (!hasBaseRotation || bone == null || registeredBones == null || baseLocalRotations == null)
                return false;

            for (var i = 0; i < registeredBones.Length; i++)
            {
                if (registeredBones[i] != bone)
                    continue;

                baseRotation = baseLocalRotations[i];
                return true;
            }

            return false;
        }

        public bool TryBuildSemanticBoneRotation(
            Transform bone,
            Vector3 semanticRotation,
            Vector3 mirrorSign,
            out Quaternion localRotation)
        {
            localRotation = Quaternion.identity;

            if (!TryGetBaseRotation(bone, out var baseRotation))
                return false;

            var mirroredRotation = new Vector3(
                semanticRotation.x * mirrorSign.x,
                semanticRotation.y * mirrorSign.y,
                semanticRotation.z * mirrorSign.z);

            localRotation = baseRotation * Quaternion.Euler(mirroredRotation);
            return true;
        }

        public bool ApplyBoneLocalRotation(Transform bone, Quaternion localRotation)
        {
            if (!TryGetBaseRotation(bone, out _))
                return false;

            bone.localRotation = localRotation;
            isApplying = true;
            return true;
        }
    }
}
