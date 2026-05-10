using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RootLocomotionDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform root;
        [SerializeField] private RootOrientationController rootOrientationController;
        [SerializeField] private LocomotionActionExecutor locomotionActionExecutor;

        [Header("Runtime Status")]
        [SerializeField] private bool minimized;
        [SerializeField] private Rect windowRect = new Rect(860f, 20f, 300f, 190f);

        private Vector2 expandedWindowSize = new Vector2(300f, 190f);

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP Root" : "VirtualPartner Root");
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (minimized)
            {
                GUILayout.Label(locomotionActionExecutor != null && locomotionActionExecutor.IsActive
                    ? $"Move: {locomotionActionExecutor.ActiveMode}"
                    : "Root Ready");
                GUI.DragWindow();
                return;
            }

            DrawStatus();
            DrawControls();
            GUI.DragWindow();
        }

        private void DrawStatus()
        {
            if (!IsUsable(root))
            {
                GUILayout.Label("Root: Missing");
                return;
            }

            GUILayout.Label($"Pos: {root.position.x:0.00}, {root.position.y:0.00}, {root.position.z:0.00}");
            GUILayout.Label($"Yaw: {root.eulerAngles.y:0.0}");

            if (rootOrientationController != null)
            {
                GUILayout.Label($"Turn: {(rootOrientationController.IsTurning ? rootOrientationController.ActiveTarget : "-")}");
                GUILayout.Label($"Interaction: {(rootOrientationController.IsInUserInteraction ? "On" : "Off")}");
            }

            if (locomotionActionExecutor != null)
            {
                GUILayout.Label($"Move: {(locomotionActionExecutor.IsActive ? locomotionActionExecutor.ActiveMode : "-")}");
                GUILayout.Label($"Move Time: {locomotionActionExecutor.Elapsed:0.00}/{locomotionActionExecutor.Duration:0.00}");
            }
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = rootOrientationController != null;
            if (GUILayout.Button("Enter Interaction"))
                rootOrientationController.EnterUserInteraction();
            if (GUILayout.Button("Exit"))
                rootOrientationController.ExitUserInteraction();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private static bool IsUsable(Transform candidate)
        {
            if (candidate == null)
                return false;

            try
            {
                _ = candidate.position;
                return true;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 180f;
                windowRect.height = 74f;
                return;
            }

            windowRect.width = Mathf.Max(260f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(160f, expandedWindowSize.y);
        }
    }
}
