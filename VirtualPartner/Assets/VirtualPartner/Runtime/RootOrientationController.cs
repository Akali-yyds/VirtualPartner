using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RootOrientationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform root;
        [SerializeField] private Camera referenceCamera;

        [Header("Settings")]
        [SerializeField] private float turnDuration = 0.3f;

        [Header("Runtime Status")]
        [SerializeField] private bool turning;
        [SerializeField] private string activeTarget;
        [SerializeField] private float turnElapsed;
        [SerializeField] private bool inUserInteraction;
        [SerializeField] private float currentYaw;

        private Quaternion turnFrom;
        private Quaternion turnTo;
        private float activeDuration;

        public bool IsTurning => turning;
        public bool IsInUserInteraction => inUserInteraction;
        public string ActiveTarget => activeTarget;
        public float CurrentYaw => currentYaw;
        public float DefaultTurnDuration => Mathf.Max(0f, turnDuration);
        public Transform Root => root;

        public void Configure(Transform rootTransform, Camera camera)
        {
            root = rootTransform;
            referenceCamera = camera != null ? camera : Camera.main;
            turning = false;
            activeTarget = string.Empty;
            turnElapsed = 0f;
            inUserInteraction = false;
            RefreshYaw();
        }

        public bool RequestTimelineFacing(string target, float duration, out string failureReason)
        {
            return BeginTurn(target, duration, false, out failureReason);
        }

        public bool EnterUserInteraction()
        {
            if (inUserInteraction)
                return false;

            inUserInteraction = true;
            return BeginTurn("camera", DefaultTurnDuration, true, out _);
        }

        public void ExitUserInteraction()
        {
            inUserInteraction = false;
        }

        public void StopTimelineFacing()
        {
            turning = false;
            activeTarget = string.Empty;
            turnElapsed = 0f;
            activeDuration = 0f;
            RefreshYaw();
        }

        public void CompleteTimelineFacing()
        {
            if (turning && root != null)
                root.rotation = turnTo;

            turning = false;
            activeTarget = string.Empty;
            turnElapsed = 0f;
            activeDuration = 0f;
            RefreshYaw();
        }

        public void ManualUpdate(float deltaTime)
        {
            if (root == null)
                return;

            if (!turning)
            {
                RefreshYaw();
                return;
            }

            turnElapsed += Mathf.Max(0f, deltaTime);
            var progress = activeDuration <= 0f ? 1f : Mathf.Clamp01(turnElapsed / activeDuration);
            var smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            root.rotation = Quaternion.Slerp(turnFrom, turnTo, smoothProgress);

            if (progress >= 1f)
                turning = false;

            RefreshYaw();
        }

        private bool BeginTurn(string target, float duration, bool autoTurn, out string failureReason)
        {
            failureReason = string.Empty;

            if (root == null)
            {
                failureReason = "Root reference is missing.";
                return false;
            }

            if (!TryResolveDirection(target, out var direction, out failureReason))
                return false;

            turnFrom = root.rotation;
            turnTo = Quaternion.LookRotation(direction, Vector3.up);
            activeDuration = Mathf.Max(0f, duration);
            turnElapsed = 0f;
            activeTarget = autoTurn ? "AutoTurnToCamera" : target;

            if (activeDuration <= 0f)
            {
                root.rotation = turnTo;
                turning = false;
                RefreshYaw();
                return true;
            }

            turning = true;
            RefreshYaw();
            return true;
        }

        private bool TryResolveDirection(string target, out Vector3 direction, out string reason)
        {
            direction = Vector3.zero;
            reason = string.Empty;

            if (referenceCamera == null)
                referenceCamera = Camera.main;

            if (referenceCamera == null)
            {
                reason = "Reference camera is missing.";
                return false;
            }

            var normalizedTarget = string.IsNullOrWhiteSpace(target)
                ? string.Empty
                : target.Trim().ToLowerInvariant();

            if (normalizedTarget == "camera")
                direction = referenceCamera.transform.position - root.position;
            else if (normalizedTarget == "screenright")
                direction = referenceCamera.transform.right;
            else if (normalizedTarget == "screenleft")
                direction = -referenceCamera.transform.right;
            else if (normalizedTarget == "screenforward")
                direction = referenceCamera.transform.forward;
            else if (normalizedTarget == "screenbackward")
                direction = -referenceCamera.transform.forward;
            else
            {
                reason = $"Unknown facing target '{target}'.";
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                reason = $"Facing target '{target}' has no horizontal direction.";
                return false;
            }

            direction.Normalize();
            return true;
        }

        private void RefreshYaw()
        {
            if (root == null)
            {
                currentYaw = 0f;
                return;
            }

            currentYaw = root.eulerAngles.y;
        }
    }
}
