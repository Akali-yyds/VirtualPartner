using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        private enum NavigationAction
        {
            None,
            Recent,
            Home,
            Back,
        }

        private bool suppressNextButtonClick;

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

        private void Update()
        {
            if (!TryGetPointerPressed(out var screenPoint))
                return;

            var action = ResolveNavigationAction(screenPoint);
            if (action == NavigationAction.None)
                return;

            suppressNextButtonClick = true;
            DispatchNavigationAction(action);
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

            EnsureButtonHitRect(button);
            image.raycastTarget = true;
            var color = image.color;
            color.a = 0f;
            image.color = color;

            if (button.targetGraphic == null)
                button.targetGraphic = image;
        }

        private void EnsureButtonHitRect(Button button)
        {
            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
                return;

            var minSize = new Vector2(Mathf.Max(1f, buttonHitSize.x), Mathf.Max(1f, buttonHitSize.y));
            if (rectTransform.rect.width >= minSize.x && rectTransform.rect.height >= minSize.y)
                return;

            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minSize.x);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minSize.y);
        }

        private void KeepNavigationOnTop()
        {
            transform.SetAsLastSibling();
        }

        private bool TryGetPointerPressed(out Vector2 screenPoint)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPoint = mouse.position.ReadValue();
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                screenPoint = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                screenPoint = Input.mousePosition;
                return true;
            }
#endif

            screenPoint = Vector2.zero;
            return false;
        }

        private NavigationAction ResolveNavigationAction(Vector2 screenPoint)
        {
            if (ContainsScreenPoint(recentButton, screenPoint))
                return NavigationAction.Recent;
            if (ContainsScreenPoint(homeButton, screenPoint))
                return NavigationAction.Home;
            if (ContainsScreenPoint(backButton, screenPoint))
                return NavigationAction.Back;

            return ResolveNavigationActionFromBar(screenPoint);
        }

        private bool ContainsScreenPoint(Button button, Vector2 screenPoint)
        {
            if (button == null || !button.isActiveAndEnabled || !button.interactable)
                return false;

            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, ResolveEventCamera(rectTransform));
        }

        private NavigationAction ResolveNavigationActionFromBar(Vector2 screenPoint)
        {
            var rectTransform = transform as RectTransform;
            if (rectTransform == null)
                return NavigationAction.None;

            var eventCamera = ResolveEventCamera(rectTransform);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var localPoint))
                return NavigationAction.None;

            if (!rectTransform.rect.Contains(localPoint))
                return NavigationAction.None;

            var normalizedX = Mathf.InverseLerp(rectTransform.rect.xMin, rectTransform.rect.xMax, localPoint.x);
            if (normalizedX < 1f / 3f)
                return NavigationAction.Recent;
            if (normalizedX < 2f / 3f)
                return NavigationAction.Home;

            return NavigationAction.Back;
        }

        private static Camera ResolveEventCamera(RectTransform rectTransform)
        {
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return canvas.worldCamera;
        }

        private void DispatchNavigationAction(NavigationAction action)
        {
            switch (action)
            {
                case NavigationAction.Recent:
                    DispatchRecent();
                    break;
                case NavigationAction.Home:
                    DispatchHome();
                    break;
                case NavigationAction.Back:
                    DispatchBack();
                    break;
            }
        }

        private void HandleBackClicked()
        {
            if (ConsumeSuppressedButtonClick())
                return;

            DispatchBack();
        }

        private void HandleHomeClicked()
        {
            if (ConsumeSuppressedButtonClick())
                return;

            DispatchHome();
        }

        private void HandleRecentClicked()
        {
            if (ConsumeSuppressedButtonClick())
                return;

            DispatchRecent();
        }

        private bool ConsumeSuppressedButtonClick()
        {
            if (!suppressNextButtonClick)
                return false;

            suppressNextButtonClick = false;
            return true;
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
