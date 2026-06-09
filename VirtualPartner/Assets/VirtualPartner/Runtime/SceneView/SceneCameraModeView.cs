using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SceneCameraModeView : MonoBehaviour
    {
        [Header("Groups")]
        [SerializeField] private CanvasGroup entryGroup;
        [SerializeField] private CanvasGroup modeGroup;

        [Header("Buttons")]
        [SerializeField] private Button entryButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button resetButton;

        private bool modeActive;
        private bool entryVisible = true;

        public Button EntryButton => entryButton;
        public Button ExitButton => exitButton;
        public Button ResetButton => resetButton;

        public void SetModeActive(bool active)
        {
            modeActive = active;
            ApplyVisibility();
        }

        public void SetEntryVisible(bool visible)
        {
            entryVisible = visible;
            ApplyVisibility();
        }

        public void SetInteractable(bool interactable)
        {
            if (entryButton != null)
                entryButton.interactable = interactable;
            if (exitButton != null)
                exitButton.interactable = interactable;
            if (resetButton != null)
                resetButton.interactable = interactable;
        }

        private void Awake()
        {
            ApplyVisibility();
        }

        private void OnValidate()
        {
            if (entryGroup == null && entryButton != null)
                entryGroup = entryButton.GetComponentInParent<CanvasGroup>();
            if (modeGroup == null && exitButton != null)
                modeGroup = exitButton.GetComponentInParent<CanvasGroup>();
        }

        private void ApplyVisibility()
        {
            SetGroupVisible(entryGroup, entryVisible && !modeActive);
            SetGroupVisible(modeGroup, modeActive);
        }

        private static void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            group.gameObject.SetActive(visible);
        }
    }
}
