using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SceneCameraModeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SceneCameraModeView view;
        [SerializeField] private VirtualSceneCameraController cameraController;
        [SerializeField] private VirtualSceneCameraInputDriver inputDriver;
        [SerializeField] private MomotalkUIManager momotalkUIManager;

        [Header("Pan Bounds")]
        [SerializeField] private Transform panBoundsSourceRoot;
        [SerializeField] private float panBoundsPadding = 0.8f;
        [SerializeField] private bool configurePanBoundsOnAwake = true;

        private bool modeActive;

        public bool ModeActive => modeActive;

        public void EnterMode()
        {
            if (modeActive)
                return;

            modeActive = true;
            if (momotalkUIManager != null)
            {
                if (momotalkUIManager.IsOpen)
                    momotalkUIManager.Close();
                momotalkUIManager.SetOpenButtonSuppressed(true);
            }

            if (view != null)
                view.SetModeActive(true);
            if (inputDriver != null)
                inputDriver.SetInputEnabled(true);

            ClearSelectedUi();
        }

        public void ExitMode()
        {
            if (!modeActive)
                return;

            modeActive = false;
            if (inputDriver != null)
                inputDriver.SetInputEnabled(false);
            if (view != null)
                view.SetModeActive(false);
            if (momotalkUIManager != null)
                momotalkUIManager.SetOpenButtonSuppressed(false);

            ClearSelectedUi();
        }

        public void ResetView()
        {
            if (cameraController != null)
                cameraController.ResetView();
        }

        private void Awake()
        {
            if (inputDriver != null)
                inputDriver.Configure(cameraController);
            if (configurePanBoundsOnAwake)
                ConfigurePanBounds();
            if (view != null)
                view.SetModeActive(false);
            if (inputDriver != null)
                inputDriver.SetInputEnabled(false);
        }

        private void OnEnable()
        {
            if (view != null)
            {
                if (view.EntryButton != null)
                    view.EntryButton.onClick.AddListener(EnterMode);
                if (view.ExitButton != null)
                    view.ExitButton.onClick.AddListener(ExitMode);
                if (view.ResetButton != null)
                    view.ResetButton.onClick.AddListener(ResetView);
            }

            if (inputDriver != null)
            {
                inputDriver.ExitRequested += ExitMode;
                inputDriver.ResetRequested += ResetView;
            }
        }

        private void OnDisable()
        {
            if (view != null)
            {
                if (view.EntryButton != null)
                    view.EntryButton.onClick.RemoveListener(EnterMode);
                if (view.ExitButton != null)
                    view.ExitButton.onClick.RemoveListener(ExitMode);
                if (view.ResetButton != null)
                    view.ResetButton.onClick.RemoveListener(ResetView);
            }

            if (inputDriver != null)
            {
                inputDriver.ExitRequested -= ExitMode;
                inputDriver.ResetRequested -= ResetView;
                inputDriver.SetInputEnabled(false);
            }

            if (modeActive && momotalkUIManager != null)
                momotalkUIManager.SetOpenButtonSuppressed(false);
            modeActive = false;
        }

        private void ConfigurePanBounds()
        {
            if (cameraController == null || panBoundsSourceRoot == null)
                return;

            if (!TryGetRendererBounds(panBoundsSourceRoot, out var bounds))
                return;

            cameraController.SetPanBounds(bounds, panBoundsPadding);
            cameraController.SetPanBoundsEnabled(true);
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            if (root == null)
                return false;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        private static void ClearSelectedUi()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
