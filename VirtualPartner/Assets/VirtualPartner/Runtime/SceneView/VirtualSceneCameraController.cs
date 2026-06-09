using Unity.Cinemachine;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualSceneCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera sceneCamera;
        [SerializeField] private CinemachineOrbitalFollow orbitalFollow;
        [SerializeField] private Transform focusTarget;
        [SerializeField] private Camera outputCamera;

        [Header("Orbit")]
        [SerializeField] private float orbitDegreesPerUnit = 0.25f;
        [SerializeField] private Vector2 verticalRange = new Vector2(-8f, 42f);

        [Header("Zoom")]
        [SerializeField] private float zoomUnitsPerUnit = 0.03f;
        [SerializeField] private float minRadius = 2.5f;
        [SerializeField] private float maxRadius = 12f;

        [Header("Pan")]
        [SerializeField] private float panUnitsPerUnit = 0.01f;
        [SerializeField] private bool panBoundsEnabled;
        [SerializeField] private Bounds panBounds = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));

        [Header("Debug")]
        [SerializeField] private bool enableDebugInput;

        private Vector3 defaultFocusPosition;
        private float defaultHorizontal;
        private float defaultVertical;
        private float defaultRadius;

        public CinemachineCamera SceneCamera => sceneCamera;
        public Transform FocusTarget => focusTarget;

        public void SetPanBounds(Bounds bounds, float padding)
        {
            padding = Mathf.Max(0f, padding);
            bounds.Expand(new Vector3(padding * 2f, 0f, padding * 2f));
            panBounds = bounds;
        }

        public void SetPanBoundsEnabled(bool enabled)
        {
            panBoundsEnabled = enabled;
            if (focusTarget != null)
                focusTarget.position = ClampFocusPosition(focusTarget.position);
        }

        public void Orbit(Vector2 delta)
        {
            if (orbitalFollow == null)
                return;

            var horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Value = horizontal.ClampValue(horizontal.Value + delta.x * orbitDegreesPerUnit);
            orbitalFollow.HorizontalAxis = horizontal;

            var vertical = orbitalFollow.VerticalAxis;
            vertical.Range = verticalRange;
            vertical.Value = vertical.ClampValue(vertical.Value - delta.y * orbitDegreesPerUnit);
            orbitalFollow.VerticalAxis = vertical;
        }

        public void Zoom(float delta)
        {
            if (orbitalFollow == null)
                return;

            orbitalFollow.Radius = Mathf.Clamp(orbitalFollow.Radius - delta * zoomUnitsPerUnit, minRadius, maxRadius);
        }

        public void Pan(Vector2 delta)
        {
            if (focusTarget == null)
                return;

            var cameraTransform = outputCamera != null ? outputCamera.transform : transform;
            var right = cameraTransform.right;
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            var nextPosition = focusTarget.position + (-right * delta.x + -forward * delta.y) * panUnitsPerUnit;
            focusTarget.position = ClampFocusPosition(nextPosition);
        }

        public void Focus(Bounds bounds)
        {
            if (focusTarget == null)
                return;

            focusTarget.position = ClampFocusPosition(bounds.center);
            if (orbitalFollow == null)
                return;

            var radius = Mathf.Max(bounds.extents.magnitude * 1.35f, minRadius);
            orbitalFollow.Radius = Mathf.Clamp(radius, minRadius, maxRadius);
        }

        public void ResetView()
        {
            if (focusTarget != null)
                focusTarget.position = defaultFocusPosition;

            if (orbitalFollow == null)
                return;

            var horizontal = orbitalFollow.HorizontalAxis;
            horizontal.Value = defaultHorizontal;
            orbitalFollow.HorizontalAxis = horizontal;

            var vertical = orbitalFollow.VerticalAxis;
            vertical.Range = verticalRange;
            vertical.Value = defaultVertical;
            orbitalFollow.VerticalAxis = vertical;

            orbitalFollow.Radius = defaultRadius;
        }

        private void Awake()
        {
            CaptureDefaults();
        }

        private void Reset()
        {
            outputCamera = Camera.main;
            sceneCamera = GetComponent<CinemachineCamera>();
            orbitalFollow = GetComponent<CinemachineOrbitalFollow>();
            if (sceneCamera != null)
                focusTarget = sceneCamera.Target.TrackingTarget;
            CaptureDefaults();
        }

        private void OnValidate()
        {
            minRadius = Mathf.Max(0.01f, minRadius);
            maxRadius = Mathf.Max(minRadius, maxRadius);
            verticalRange.y = Mathf.Max(verticalRange.x, verticalRange.y);
            panBounds.size = new Vector3(
                Mathf.Max(0.01f, panBounds.size.x),
                Mathf.Max(0.01f, panBounds.size.y),
                Mathf.Max(0.01f, panBounds.size.z));
            CaptureDefaults();
        }

        private void Update()
        {
            if (!enableDebugInput)
                return;

            var orbit = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow))
                orbit.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow))
                orbit.x += 1f;
            if (Input.GetKey(KeyCode.UpArrow))
                orbit.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow))
                orbit.y -= 1f;
            if (orbit != Vector2.zero)
                Orbit(orbit * Time.deltaTime * 240f);

            var zoom = 0f;
            if (Input.GetKey(KeyCode.Equals))
                zoom += 1f;
            if (Input.GetKey(KeyCode.Minus))
                zoom -= 1f;
            if (Mathf.Abs(zoom) > 0f)
                Zoom(zoom * Time.deltaTime * 240f);
        }

        private void CaptureDefaults()
        {
            if (focusTarget != null)
                defaultFocusPosition = focusTarget.position;

            if (orbitalFollow == null)
                return;

            defaultHorizontal = orbitalFollow.HorizontalAxis.Value;
            defaultVertical = orbitalFollow.VerticalAxis.Value;
            defaultRadius = Mathf.Clamp(orbitalFollow.Radius, minRadius, maxRadius);
        }

        private Vector3 ClampFocusPosition(Vector3 position)
        {
            if (!panBoundsEnabled)
                return position;

            var min = panBounds.min;
            var max = panBounds.max;
            position.x = Mathf.Clamp(position.x, min.x, max.x);
            position.z = Mathf.Clamp(position.z, min.z, max.z);
            return position;
        }
    }
}
