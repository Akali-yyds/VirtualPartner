using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneNavigationBarView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private PhoneOSController phoneOSController;
        [SerializeField] private Button backButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button recentButton;
        [SerializeField] private Image backIcon;
        [SerializeField] private Image homeIcon;
        [SerializeField] private Image recentIcon;

        private void Awake()
        {
            ApplyStyle();
            BindButton(backButton, HandleBackClicked);
            BindButton(homeButton, HandleHomeClicked);
            BindButton(recentButton, HandleRecentClicked);
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

        private void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void HandleBackClicked()
        {
            if (phoneOSController != null)
            {
                phoneOSController.HandleBackPressed();
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Back", this);
        }

        private void HandleHomeClicked()
        {
            if (phoneOSController != null)
            {
                phoneOSController.HandleHomePressed();
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Home", this);
        }

        private void HandleRecentClicked()
        {
            if (phoneOSController != null)
            {
                phoneOSController.HandleRecentPressed();
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Recent", this);
        }
    }
}
