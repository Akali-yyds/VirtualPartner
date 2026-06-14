using System;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VirtualSceneCameraInputDriver : MonoBehaviour
    {
        private enum DragMode
        {
            None,
            Orbit,
            Pan
        }

        [Header("References")]
        [SerializeField] private VirtualSceneCameraController cameraController;

        [Header("Input")]
        [SerializeField] private bool inputEnabled;
        [SerializeField] private bool ignorePointerStartedOverUi = true;
        [SerializeField] private float orbitSensitivity = 1f;
        [SerializeField] private float panSensitivity = 1f;

        [Header("Zoom")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Zoom delta sent to the camera controller for each mouse-wheel notch. Increase this if wheel zoom feels too slow.")]
        private float wheelZoomStep = 20f;
        [SerializeField, Min(0.01f)]
        [Tooltip("Closest allowed camera orbit radius. Lower values allow stronger zoom-in.")]
        private float minZoomRadius = 0.8f;
        [SerializeField, Min(0.01f)]
        [Tooltip("Farthest allowed camera orbit radius. Higher values allow more zoom-out.")]
        private float maxZoomRadius = 24f;

        private DragMode dragMode;
        private Vector2 lastPointerPosition;

        public event Action ExitRequested;
        public event Action ResetRequested;

        public bool InputEnabled => inputEnabled;

        public void Configure(VirtualSceneCameraController controller)
        {
            cameraController = controller;
            ApplyZoomLimits();
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
                SetDragMode(DragMode.None);
        }

        private void Update()
        {
            if (!inputEnabled || cameraController == null)
                return;

#if ENABLE_INPUT_SYSTEM
            HandleInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER
            HandleLegacyInput();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void HandleInputSystem()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                    ExitRequested?.Invoke();
                if (keyboard.rKey.wasPressedThisFrame)
                    ResetRequested?.Invoke();
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            var pointerPosition = mouse.position.ReadValue();
            if (BeginDragRequested(mouse, keyboard, out var requestedMode))
            {
                SetDragMode(ShouldIgnorePointerStart() ? DragMode.None : requestedMode);
                lastPointerPosition = pointerPosition;
            }

            if (dragMode != DragMode.None && !IsDragButtonHeld(mouse, keyboard, dragMode))
                SetDragMode(DragMode.None);

            if (dragMode != DragMode.None)
            {
                var delta = pointerPosition - lastPointerPosition;
                lastPointerPosition = pointerPosition;
                ApplyDrag(delta);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
                cameraController.Zoom(GetScrollSteps(scroll) * wheelZoomStep);
        }

        private static bool BeginDragRequested(Mouse mouse, Keyboard keyboard, out DragMode requestedMode)
        {
            if (mouse.rightButton.wasPressedThisFrame)
            {
                requestedMode = DragMode.Orbit;
                return true;
            }

            if (mouse.middleButton.wasPressedThisFrame)
            {
                requestedMode = DragMode.Pan;
                return true;
            }

            if (mouse.leftButton.wasPressedThisFrame && ShiftHeld(keyboard))
            {
                requestedMode = DragMode.Pan;
                return true;
            }

            requestedMode = DragMode.None;
            return false;
        }

        private static bool IsDragButtonHeld(Mouse mouse, Keyboard keyboard, DragMode mode)
        {
            if (mode == DragMode.Orbit)
                return mouse.rightButton.isPressed;

            return mouse.middleButton.isPressed || (mouse.leftButton.isPressed && ShiftHeld(keyboard));
        }

        private static bool ShiftHeld(Keyboard keyboard)
        {
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleLegacyInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                ExitRequested?.Invoke();
            if (Input.GetKeyDown(KeyCode.R))
                ResetRequested?.Invoke();

            var pointerPosition = (Vector2)Input.mousePosition;
            if (Input.GetMouseButtonDown(1))
                BeginLegacyDrag(pointerPosition, DragMode.Orbit);
            else if (Input.GetMouseButtonDown(2) || (Input.GetMouseButtonDown(0) && ShiftHeldLegacy()))
                BeginLegacyDrag(pointerPosition, DragMode.Pan);

            if (dragMode != DragMode.None && !IsLegacyDragButtonHeld(dragMode))
                dragMode = DragMode.None;

            if (dragMode != DragMode.None)
            {
                var delta = pointerPosition - lastPointerPosition;
                lastPointerPosition = pointerPosition;
                ApplyDrag(delta);
            }

            var scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !IsPointerOverUi())
                cameraController.Zoom(GetScrollSteps(scroll) * wheelZoomStep);
        }

        private void BeginLegacyDrag(Vector2 pointerPosition, DragMode requestedMode)
        {
            SetDragMode(ShouldIgnorePointerStart() ? DragMode.None : requestedMode);
            lastPointerPosition = pointerPosition;
        }

        private static bool IsLegacyDragButtonHeld(DragMode mode)
        {
            if (mode == DragMode.Orbit)
                return Input.GetMouseButton(1);

            return Input.GetMouseButton(2) || (Input.GetMouseButton(0) && ShiftHeldLegacy());
        }

        private static bool ShiftHeldLegacy()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }
#endif

        private void ApplyDrag(Vector2 delta)
        {
            if (delta == Vector2.zero)
                return;

            if (dragMode == DragMode.Orbit)
                cameraController.Orbit(delta * orbitSensitivity);
            else if (dragMode == DragMode.Pan)
                cameraController.Pan(delta * panSensitivity);
        }

        private void SetDragMode(DragMode mode)
        {
            if (dragMode == mode)
                return;

            if (dragMode == DragMode.Pan)
                cameraController?.EndPan();

            dragMode = mode;

            if (dragMode == DragMode.Pan)
                cameraController?.BeginPan();
        }

        private bool ShouldIgnorePointerStart()
        {
            return ignorePointerStartedOverUi && IsPointerOverUi();
        }

        private void OnValidate()
        {
            orbitSensitivity = Mathf.Max(0.01f, orbitSensitivity);
            panSensitivity = Mathf.Max(0.01f, panSensitivity);
            wheelZoomStep = Mathf.Max(0.01f, wheelZoomStep);
            minZoomRadius = Mathf.Max(0.01f, minZoomRadius);
            maxZoomRadius = Mathf.Max(minZoomRadius, maxZoomRadius);
            ApplyZoomLimits();
        }

        private void ApplyZoomLimits()
        {
            if (cameraController != null)
                cameraController.SetZoomRadiusLimits(minZoomRadius, maxZoomRadius);
        }

        private static float GetScrollSteps(float scrollDelta)
        {
            return Mathf.Abs(scrollDelta) > 10f ? scrollDelta / 120f : scrollDelta;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
