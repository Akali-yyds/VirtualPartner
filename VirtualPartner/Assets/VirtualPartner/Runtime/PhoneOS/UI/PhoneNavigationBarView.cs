using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneNavigationBarView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Button backButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button recentButton;
        [SerializeField] private Image backIcon;
        [SerializeField] private Image homeIcon;
        [SerializeField] private Image recentIcon;

        private void Awake()
        {
            ApplyStyle();
            BindButton(backButton, "Back");
            BindButton(homeButton, "Home");
            BindButton(recentButton, "Recent");
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        private void ApplyStyle()
        {
            if (style == null)
                return;

            ApplyIcon(backIcon, style.NavigationBackIcon);
            ApplyIcon(homeIcon, style.NavigationHomeIcon);
            ApplyIcon(recentIcon, style.NavigationRecentIcon);
        }

        private void ApplyIcon(Image icon, Sprite sprite)
        {
            if (icon == null)
                return;

            icon.sprite = sprite;
            icon.color = style.NavigationIconColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private void BindButton(Button button, string actionName)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Debug.Log($"[PhoneOS] Navigation clicked: {actionName}", this));
        }
    }
}
