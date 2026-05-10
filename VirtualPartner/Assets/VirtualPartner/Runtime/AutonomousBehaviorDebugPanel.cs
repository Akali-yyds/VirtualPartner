using UnityEngine;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AutonomousBehaviorDebugPanel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AutonomousBehaviorScheduler scheduler;

        [Header("Runtime Status")]
        [SerializeField] private bool minimized;
        [SerializeField] private Rect windowRect = new Rect(20f, 420f, 320f, 210f);

        private Vector2 expandedWindowSize = new Vector2(320f, 210f);

        private void OnGUI()
        {
            if (!Application.isPlaying)
                return;

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindow,
                minimized ? "VP FSM" : "VirtualPartner FSM");
        }

        private void DrawWindow(int windowId)
        {
            if (GUILayout.Button(minimized ? "Maximize" : "Minimize"))
                ToggleMinimized();

            if (scheduler == null)
            {
                GUILayout.Label("Scheduler: Missing");
                GUI.DragWindow();
                return;
            }

            DrawStatus();
            if (!minimized)
                DrawControls();

            GUI.DragWindow();
        }

        private void DrawStatus()
        {
            GUILayout.Label($"Enabled: {scheduler.SchedulerActive}");
            GUILayout.Label($"State: {scheduler.State}");
            GUILayout.Label($"Wait: {scheduler.WaitRemaining:0.0}s");
            GUILayout.Label($"Action: {scheduler.CurrentActionName}");
            GUILayout.Label($"Interaction: {scheduler.IsInUserInteraction}  {scheduler.InteractionRemaining:0.0}s");

            if (!string.IsNullOrWhiteSpace(scheduler.LastMessage))
                GUILayout.Label($"Last: {scheduler.LastMessage}");
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(scheduler.SchedulerActive ? "Disable" : "Enable"))
                scheduler.SetSchedulerActive(!scheduler.SchedulerActive);
            if (GUILayout.Button("Enter Interaction"))
                scheduler.EnterUserInteraction();
            if (GUILayout.Button("Exit Interaction"))
                scheduler.ExitUserInteraction();
            GUILayout.EndHorizontal();
        }

        private void ToggleMinimized()
        {
            minimized = !minimized;

            if (minimized)
            {
                expandedWindowSize = new Vector2(windowRect.width, windowRect.height);
                windowRect.width = 220f;
                windowRect.height = 132f;
                return;
            }

            windowRect.width = Mathf.Max(260f, expandedWindowSize.x);
            windowRect.height = Mathf.Max(180f, expandedWindowSize.y);
        }
    }
}
