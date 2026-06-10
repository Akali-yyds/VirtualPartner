using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class RuntimeDebugPanelToggleButton : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button toggleButton;
        [SerializeField] private VirtualPartnerRuntimeDebugPanel debugPanel;

        [Header("Startup")]
        [SerializeField] private bool closePanelOnAwake = true;

        [Header("Visual")]
        [SerializeField] private Image buttonBackground;
        [SerializeField] private Color closedColor = new Color32(0x5B, 0x6A, 0x7E, 0xFF);
        [SerializeField] private Color openColor = new Color32(0x2F, 0x9D, 0x86, 0xFF);

        private void Awake()
        {
            ResolveReferences();
            if (closePanelOnAwake && debugPanel != null)
                debugPanel.SetVisible(false);
            RefreshVisual();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleDebugPanel);
            RefreshVisual();
        }

        private void OnDisable()
        {
            if (toggleButton != null)
                toggleButton.onClick.RemoveListener(ToggleDebugPanel);
        }

        public void ToggleDebugPanel()
        {
            ResolveReferences();
            if (debugPanel == null)
                return;

            debugPanel.ToggleVisible();
            RefreshVisual();
        }

        private void ResolveReferences()
        {
            if (toggleButton == null)
                toggleButton = GetComponent<Button>();
            if (buttonBackground == null)
                buttonBackground = GetComponent<Image>();
            if (debugPanel == null)
                debugPanel = FindFirstObjectByType<VirtualPartnerRuntimeDebugPanel>(FindObjectsInactive.Include);
        }

        private void RefreshVisual()
        {
            if (buttonBackground != null)
                buttonBackground.color = debugPanel != null && debugPanel.Visible ? openColor : closedColor;
        }
    }
}
