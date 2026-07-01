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
        [SerializeField] private Vector2 buttonHitSize = new Vector2(56f, 36f);

        private void Awake()
        {
            Initialize(false);
        }

        private void OnEnable()
        {
            Initialize(false);
        }

        private void Start()
        {
            ConfigureButtonHitTargets(true);
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        private void Initialize(bool configureHitTargets)
        {
            KeepNavigationOnTop();
            ApplyStyle();
            if (configureHitTargets)
                ConfigureButtonHitTargets(true);
            BindButton(backButton, HandleBackClicked);
            BindButton(homeButton, HandleHomeClicked);
            BindButton(recentButton, HandleRecentClicked);
        }

        private void ConfigureButtonHitTargets(bool addMissingImage)
        {
            ConfigureButtonHitTarget(backButton, addMissingImage);
            ConfigureButtonHitTarget(homeButton, addMissingImage);
            ConfigureButtonHitTarget(recentButton, addMissingImage);
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

        private void ConfigureButtonHitTarget(Button button, bool addMissingImage)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image == null && addMissingImage)
                image = button.gameObject.AddComponent<Image>();
            if (image == null)
                return;

            WarnIfButtonHitRectTooSmall(button);
            image.raycastTarget = true;
            var color = image.color;
            color.a = 0f;
            image.color = color;

            if (button.targetGraphic == null)
                button.targetGraphic = image;
        }

        private void WarnIfButtonHitRectTooSmall(Button button)
        {
            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
                return;

            var minSize = new Vector2(Mathf.Max(1f, buttonHitSize.x), Mathf.Max(1f, buttonHitSize.y));
            if (rectTransform.rect.width >= minSize.x && rectTransform.rect.height >= minSize.y)
                return;

            Debug.LogWarning(
                $"[PhoneOS] Navigation button '{button.name}' hit rect is smaller than {minSize.x:0}x{minSize.y:0}. Adjust the prefab RectTransform instead of runtime resizing.",
                button);
        }

        private void KeepNavigationOnTop()
        {
            transform.SetAsLastSibling();
        }

        private void HandleBackClicked()
        {
            DispatchBack();
        }

        private void HandleHomeClicked()
        {
            DispatchHome();
        }

        private void HandleRecentClicked()
        {
            DispatchRecent();
        }

        private void DispatchBack()
        {
            if (phoneOSController != null)
            {
                phoneOSController.HandleBackPressed();
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Back", this);
        }

        private void DispatchHome()
        {
            if (phoneOSController != null)
            {
                phoneOSController.HandleHomePressed();
                return;
            }

            Debug.Log("[PhoneOS] Navigation clicked: Home", this);
        }

        private void DispatchRecent()
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
