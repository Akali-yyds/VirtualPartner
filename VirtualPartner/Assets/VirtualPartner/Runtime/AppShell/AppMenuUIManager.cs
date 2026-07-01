#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    [DisallowMultipleComponent]
    public sealed class AppMenuUIManager : MonoBehaviour
    {
        private const int SortingOrder = 1000;

        private AppLifecycleController lifecycleController;
        private GameObject canvasObject;
        private GameObject mainPanelObject;
        private GameObject confirmPanelObject;
        private Button continueButton;
        private Button quitButton;
        private Button cancelQuitButton;
        private Button confirmQuitButton;
        private bool isOpen;

        public bool IsOpen => isOpen;
        public bool IsQuitConfirmationVisible => confirmPanelObject != null && confirmPanelObject.activeSelf;

        public void Configure(AppLifecycleController lifecycle)
        {
            lifecycleController = lifecycle;
            EnsureUi();
            CloseMenu();
        }

        private void Awake()
        {
            EnsureUi();
            CloseMenu();
        }

        private void OnDestroy()
        {
            if (continueButton != null)
                continueButton.onClick.RemoveListener(CloseMenu);
            if (quitButton != null)
                quitButton.onClick.RemoveListener(RequestQuit);
            if (cancelQuitButton != null)
                cancelQuitButton.onClick.RemoveListener(CancelQuit);
            if (confirmQuitButton != null)
                confirmQuitButton.onClick.RemoveListener(ConfirmQuit);
        }

        private void Update()
        {
            if (!EscapePressedThisFrame())
                return;

            if (isOpen)
            {
                if (IsQuitConfirmationVisible)
                    ShowMainMenu();
                else
                    CloseMenu();
                return;
            }

            if (TryClearFocusedInputField())
                return;

            OpenMenu();
        }

        public void OpenMenu()
        {
            EnsureUi();
            isOpen = true;
            canvasObject.SetActive(true);
            ShowMainMenu();
            SetInteractable(true);
        }

        public void CloseMenu()
        {
            isOpen = false;
            if (canvasObject != null)
                canvasObject.SetActive(false);
        }

        public void ToggleMenu()
        {
            if (isOpen)
                CloseMenu();
            else
                OpenMenu();
        }

        public void ShowMainMenu()
        {
            EnsureUi();
            mainPanelObject.SetActive(true);
            confirmPanelObject.SetActive(false);
            SetSelected(continueButton);
        }

        public void ShowQuitConfirmation()
        {
            EnsureUi();
            if (!isOpen)
            {
                isOpen = true;
                canvasObject.SetActive(true);
            }

            mainPanelObject.SetActive(false);
            confirmPanelObject.SetActive(true);
            SetSelected(cancelQuitButton);
        }

        public void SetInteractable(bool interactable)
        {
            if (continueButton != null)
                continueButton.interactable = interactable;
            if (quitButton != null)
                quitButton.interactable = interactable;
            if (cancelQuitButton != null)
                cancelQuitButton.interactable = interactable;
            if (confirmQuitButton != null)
                confirmQuitButton.interactable = interactable;
        }

        private void RequestQuit()
        {
            if (lifecycleController != null)
                lifecycleController.RequestQuit();
        }

        private void CancelQuit()
        {
            if (lifecycleController != null)
                lifecycleController.CancelQuit();
            else
                ShowMainMenu();
        }

        private void ConfirmQuit()
        {
            if (lifecycleController != null)
                lifecycleController.ConfirmQuit();
        }

        private void EnsureUi()
        {
            if (canvasObject != null)
                return;

            var canvasRect = new GameObject("AppMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            canvasRect.SetParent(transform, false);
            canvasObject = canvasRect.gameObject;

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var overlay = CreateRect("DimOverlay", canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var overlayImage = overlay.gameObject.AddComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
            overlayImage.raycastTarget = true;

            mainPanelObject = CreateMainPanel(overlay).gameObject;
            confirmPanelObject = CreateConfirmPanel(overlay).gameObject;

            continueButton.onClick.AddListener(CloseMenu);
            quitButton.onClick.AddListener(RequestQuit);
            cancelQuitButton.onClick.AddListener(CancelQuit);
            confirmQuitButton.onClick.AddListener(ConfirmQuit);
        }

        private RectTransform CreateMainPanel(RectTransform parent)
        {
            var panel = CreatePanel(parent, "AppMenuPanel", new Vector2(440f, 300f));
            CreateText(panel, "Title", "VirtualPartner", 34, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 96f), new Vector2(360f, 56f));
            continueButton = CreateButton(panel, "ContinueButton", "继续", new Vector2(0f, 18f), new Color(0.2f, 0.58f, 0.92f, 1f), Color.white);
            quitButton = CreateButton(panel, "QuitButton", "退出", new Vector2(0f, -62f), new Color(0.92f, 0.28f, 0.32f, 1f), Color.white);
            return panel;
        }

        private RectTransform CreateConfirmPanel(RectTransform parent)
        {
            var panel = CreatePanel(parent, "QuitConfirmPanel", new Vector2(500f, 280f));
            CreateText(panel, "Title", "确定要退出 VirtualPartner 吗？", 28, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, 58f), new Vector2(420f, 90f));
            cancelQuitButton = CreateButton(panel, "CancelButton", "取消", new Vector2(-110f, -62f), new Color(0.72f, 0.75f, 0.8f, 1f), new Color(0.1f, 0.12f, 0.16f, 1f), new Vector2(160f, 56f));
            confirmQuitButton = CreateButton(panel, "ConfirmQuitButton", "确认退出", new Vector2(110f, -62f), new Color(0.92f, 0.28f, 0.32f, 1f), Color.white, new Vector2(160f, 56f));
            return panel;
        }

        private RectTransform CreatePanel(RectTransform parent, string name, Vector2 size)
        {
            var panel = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var image = panel.gameObject.AddComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            image.raycastTarget = true;
            return panel;
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Color background, Color textColor)
        {
            return CreateButton(parent, name, label, position, background, textColor, new Vector2(260f, 58f));
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Color background, Color textColor, Vector2 size)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            rect.gameObject.AddComponent<MomotalkUIButtonFeedback>();
            CreateText(rect, "Text", label, 26, textColor, TextAnchor.MiddleCenter, Vector2.zero, size);
            return button;
        }

        private Text CreateText(RectTransform parent, string name, string value, int fontSize, Color color, TextAnchor alignment, Vector2 position, Vector2 size)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value;
            text.font = GetUiFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            if (anchorMin == Vector2.zero && anchorMax == Vector2.one)
            {
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return rect;
        }

        private static Font GetUiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return null;
        }

        private static void SetSelected(Button button)
        {
            if (EventSystem.current == null || button == null || !button.isActiveAndEnabled)
                return;

            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private static bool TryClearFocusedInputField()
        {
            var inputField = GetFocusedInputFieldFromSelection();
            if (inputField == null)
                inputField = FindFocusedInputField();
            if (inputField == null)
                return false;

            inputField.DeactivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            return true;
        }

        private static InputField GetFocusedInputFieldFromSelection()
        {
            var current = EventSystem.current;
            if (current == null || current.currentSelectedGameObject == null)
                return null;

            var inputField = current.currentSelectedGameObject.GetComponent<InputField>();
            if (inputField == null)
                inputField = current.currentSelectedGameObject.GetComponentInParent<InputField>();
            return inputField;
        }

        private static InputField FindFocusedInputField()
        {
            var inputFields = FindObjectsByType<InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (var i = 0; i < inputFields.Length; i++)
            {
                if (inputFields[i] != null && inputFields[i].isFocused)
                    return inputFields[i];
            }

            return null;
        }

        private static bool EscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }
    }
}
