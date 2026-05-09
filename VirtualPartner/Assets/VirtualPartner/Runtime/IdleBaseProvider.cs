using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class IdleBaseProvider : MonoBehaviour
    {
        [SerializeField] private AnimationClip idleClip;

        [Header("Runtime Status")]
        [SerializeField] private bool isPlaying;
        [SerializeField] private float currentTime;
        [SerializeField] private float clipLength;

        public AnimationClip Clip => idleClip;
        public bool IsPlaying => isPlaying;
        public float CurrentTime => currentTime;

        public void Configure(AnimationClip clip)
        {
            idleClip = clip;
            currentTime = 0f;
            clipLength = idleClip != null ? idleClip.length : 0f;
            isPlaying = false;
        }

        public void Play()
        {
            currentTime = 0f;
            clipLength = idleClip != null ? idleClip.length : 0f;
            isPlaying = idleClip != null && clipLength > 0f;
        }

        public void Stop()
        {
            isPlaying = false;
        }

        public float Advance(float deltaTime)
        {
            if (!isPlaying || idleClip == null || clipLength <= 0f)
                return currentTime;

            currentTime = Mathf.Repeat(currentTime + Mathf.Max(0f, deltaTime), clipLength);
            return currentTime;
        }
    }
}
