using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkChatView : MonoBehaviour
    {
        [SerializeField] private MomotalkTheme theme;
        [SerializeField] private Image pageBackground;
        [SerializeField] private Image topBarBackground;
        [SerializeField] private Image chatBackgroundPattern;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text avatarText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect messageScrollRect;
        [SerializeField] private RectTransform messageContent;
        [SerializeField] private VerticalLayoutGroup messageContentLayout;
        [SerializeField] private GameObject dateChipTemplate;
        [SerializeField] private MomotalkChatBubbleView bubblePrefab;
        [SerializeField] private Image inputBarBackground;
        [SerializeField] private InputField inputField;
        [SerializeField] private Text inputPlaceholderText;
        [SerializeField] private Button sendButton;
        [SerializeField] private Image sendButtonBackground;
        [SerializeField] private Text sendButtonText;
        [SerializeField] private Text backButtonText;
        [SerializeField] private Text moreButtonText;
        [SerializeField] private Text callButtonText;
        [SerializeField] private Text emojiButtonText;
        [SerializeField] private Text attachButtonText;
        [SerializeField] private Text cameraButtonText;

        private string contactName = "Toki";
        private System.Action backRequested;
        private RectTransform doodlePatternRoot;

        public void Bind(System.Action onBackRequested)
        {
            backRequested = onBackRequested;
            RegisterButtonListeners();
        }

        public void SetContact(string displayName)
        {
            contactName = string.IsNullOrWhiteSpace(displayName) ? "Toki" : displayName;
            RefreshContent();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyStyle(true);
            RefreshContent();
            RegisterButtonListeners();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ApplyStyle(true);
            RegisterButtonListeners();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyStyle(false);
            if (titleText != null)
                titleText.text = contactName;
            if (statusText != null)
                statusText.text = string.Empty;
        }

        private void OnDestroy()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(HandleSendClicked);
            if (backButton != null)
                backButton.onClick.RemoveListener(HandleBackClicked);
        }

        private void HandleSendClicked()
        {
            Debug.Log("Real chat is not connected in Stage 5.", this);
        }

        private void HandleBackClicked()
        {
            backRequested?.Invoke();
        }

        private void ResolveReferences()
        {
            if (inputPlaceholderText == null && inputField != null)
                inputPlaceholderText = inputField.placeholder as Text;
            if (sendButton == null)
                sendButton = GetComponentInChildren<Button>(true);
            if (sendButtonBackground == null && sendButton != null)
                sendButtonBackground = sendButton.GetComponent<Image>();
            if (messageScrollRect != null && messageContent == null)
                messageContent = messageScrollRect.content;
            if (messageContentLayout == null && messageContent != null)
                messageContentLayout = messageContent.GetComponent<VerticalLayoutGroup>();
            if (dateChipTemplate == null && messageContent != null)
            {
                var dateChip = messageContent.Find("DateChipTemplate");
                if (dateChip != null)
                    dateChipTemplate = dateChip.gameObject;
            }

            if (backButtonText == null)
                backButtonText = FindTextUnder("BackButton");
            if (moreButtonText == null)
                moreButtonText = FindTextUnder("MoreButton");
            if (callButtonText == null)
                callButtonText = FindTextUnder("CallButton");
            if (emojiButtonText == null)
                emojiButtonText = FindTextUnder("EmojiButtonPlaceholder");
            if (attachButtonText == null)
                attachButtonText = FindTextUnder("AttachButton");
            if (cameraButtonText == null)
                cameraButtonText = FindTextUnder("CameraButtonPlaceholder");
        }

        private void RefreshContent()
        {
            if (titleText != null)
                titleText.text = contactName;
            if (statusText != null)
                statusText.text = string.Empty;
            if (avatarText != null)
                avatarText.text = contactName.Substring(0, 1).ToUpperInvariant();

            if (inputPlaceholderText != null)
                inputPlaceholderText.text = "Message";

            ApplyActionText();
            RebuildMessages();
        }

        private void ApplyStyle(bool applyLayout)
        {
            if (pageBackground != null)
            {
                pageBackground.sprite = theme != null ? theme.ChatBackgroundPattern : null;
                pageBackground.type = pageBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                pageBackground.color = theme != null ? theme.ChatBackgroundColor : new Color32(0xEC, 0xE5, 0xDD, 0xFF);
                pageBackground.raycastTarget = false;
            }

            if (chatBackgroundPattern != null)
            {
                chatBackgroundPattern.sprite = theme != null ? theme.ChatBackgroundPattern : null;
                chatBackgroundPattern.type = chatBackgroundPattern.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                chatBackgroundPattern.color = chatBackgroundPattern.sprite != null ? new Color(1f, 1f, 1f, 0.08f) : Color.clear;
                chatBackgroundPattern.raycastTarget = false;
            }

            if (topBarBackground != null)
            {
                topBarBackground.sprite = theme != null ? theme.TopBarBackground : null;
                topBarBackground.type = topBarBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                topBarBackground.color = theme != null ? theme.AppBarDarkColor : new Color32(0x07, 0x5E, 0x54, 0xFF);
                topBarBackground.raycastTarget = false;
            }

            if (avatarImage != null)
            {
                avatarImage.color = theme != null ? theme.AccentColor : (Color)new Color32(0x00, 0xA8, 0x84, 0xFF);
                avatarImage.raycastTarget = false;
            }

            if (inputBarBackground != null)
            {
                inputBarBackground.sprite = null;
                inputBarBackground.type = Image.Type.Simple;
                inputBarBackground.color = theme != null ? theme.ChatBackgroundColor : new Color32(0xEC, 0xE5, 0xDD, 0xFF);
                inputBarBackground.raycastTarget = false;
            }

            ApplyInputFieldStyle();
            ApplySendButtonStyle(applyLayout);
            ApplyActionText();
            if (applyLayout)
            {
                ApplyLayoutTokens();
                EnsureDoodlePattern();
            }

            ApplyTextStyle(titleText, Color.white, 18);
            ApplyTextStyle(statusText, new Color(1f, 1f, 1f, 0.76f), 1);
            ApplyTextStyle(avatarText, Color.white, 18);
            ApplyTextStyle(inputPlaceholderText, theme != null ? theme.MutedTextColor : (Color)new Color32(0x86, 0x96, 0xA0, 0xFF), theme != null ? theme.InputFontSize : 14);
        }

        private void RegisterButtonListeners()
        {
            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(HandleSendClicked);
                sendButton.onClick.AddListener(HandleSendClicked);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
                backButton.onClick.AddListener(HandleBackClicked);
            }
        }

        private void ApplyInputFieldStyle()
        {
            if (inputField == null)
                return;

            var inputBackground = inputField.GetComponent<Image>();
            if (inputBackground != null)
            {
                inputBackground.sprite = theme != null ? theme.InputBarBackground : null;
                inputBackground.type = inputBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                inputBackground.color = Color.white;
                inputBackground.raycastTarget = true;
            }

            if (inputField.textComponent != null)
                ApplyTextStyle(inputField.textComponent, theme != null ? theme.PrimaryTextColor : (Color)new Color32(0x20, 0x21, 0x24, 0xFF), theme != null ? theme.InputFontSize : 14);

            if (inputPlaceholderText != null)
                inputPlaceholderText.text = "Message";
        }

        private void ApplySendButtonStyle(bool applyLayout)
        {
            var size = theme != null ? theme.InputPillHeight : 46f;
            if (sendButtonBackground != null)
            {
                sendButtonBackground.sprite = theme != null ? theme.FabBackgroundSprite : null;
                sendButtonBackground.type = sendButtonBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                sendButtonBackground.color = theme != null ? theme.AccentColor : new Color32(0x00, 0xA8, 0x84, 0xFF);
                sendButtonBackground.raycastTarget = true;
                if (applyLayout)
                    sendButtonBackground.rectTransform.sizeDelta = new Vector2(size, size);
            }

            if (sendButton != null && sendButtonBackground != null)
            {
                sendButton.targetGraphic = sendButtonBackground;
                var colors = sendButton.colors;
                colors.highlightedColor = new Color32(0x1E, 0xB9, 0x95, 0xFF);
                colors.pressedColor = new Color32(0x00, 0x8F, 0x72, 0xFF);
                colors.selectedColor = colors.highlightedColor;
                sendButton.colors = colors;
            }

            if (sendButtonText != null)
                sendButtonText.text = string.Empty;
        }

        private void ApplyActionText()
        {
            SetIconText(backButtonText, "<", 22);
            SetIconText(moreButtonText, "...", 18);
            SetIconText(callButtonText, "Call", 11);

            var muted = theme != null ? theme.MutedTextColor : (Color)new Color32(0x86, 0x96, 0xA0, 0xFF);
            SetIconText(emojiButtonText, ":)", 15, muted);
            SetIconText(attachButtonText, "\u222A", 18, muted);
            SetIconText(cameraButtonText, "\u25A3", 17, muted);
            SetIconText(sendButtonText, "\u266A", 20, Color.white);
        }

        private void ApplyLayoutTokens()
        {
            var appBarHeight = theme != null ? theme.AppBarHeight : 56f;
            var inputBarHeight = theme != null ? theme.InputBarHeight : 62f;
            var inputPillHeight = theme != null ? theme.InputPillHeight : 46f;

            SetTopBarRect(topBarBackground != null ? topBarBackground.rectTransform : null, appBarHeight);
            SetBottomBarRect(inputBarBackground != null ? inputBarBackground.rectTransform : null, inputBarHeight);

            if (messageScrollRect != null)
                SetStretchOffsets(messageScrollRect.transform as RectTransform, appBarHeight, inputBarHeight);

            if (messageContentLayout != null)
            {
                var horizontal = Mathf.RoundToInt(theme != null ? theme.ChatHorizontalPadding : 12f);
                messageContentLayout.padding.left = horizontal;
                messageContentLayout.padding.right = horizontal;
                messageContentLayout.padding.top = 10;
                messageContentLayout.padding.bottom = 12;
                messageContentLayout.spacing = theme != null ? theme.MessageSpacing : 6f;
            }

            if (inputField != null)
            {
                SetRectHeight(inputField.transform as RectTransform, inputPillHeight);
                ApplyInputBarLayout(inputPillHeight);
            }

            if (avatarImage != null)
                avatarImage.rectTransform.sizeDelta = new Vector2(theme != null ? theme.ChatAvatarSize : 40f, theme != null ? theme.ChatAvatarSize : 40f);

            if (doodlePatternRoot != null)
                SetStretchOffsets(doodlePatternRoot, appBarHeight, inputBarHeight);
        }

        private void EnsureDoodlePattern()
        {
            if (doodlePatternRoot != null)
            {
                SetStretchOffsets(doodlePatternRoot, theme != null ? theme.AppBarHeight : 56f, theme != null ? theme.InputBarHeight : 58f);
                return;
            }

            var root = new GameObject("DoodlePattern", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            root.transform.SetSiblingIndex(0);

            doodlePatternRoot = root.GetComponent<RectTransform>();
            SetStretchOffsets(doodlePatternRoot, theme != null ? theme.AppBarHeight : 56f, theme != null ? theme.InputBarHeight : 58f);

            var canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0.12f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            var labels = new[]
            {
                "o", "+", "x", "[]", "()", "<>",
                "~~", "...", "//", "::", "=", "*"
            };

            var font = titleText != null && titleText.font != null
                ? titleText.font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");

            for (var row = 0; row < 10; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    var label = new GameObject("Doodle", typeof(RectTransform), typeof(Text));
                    label.transform.SetParent(doodlePatternRoot, false);

                    var rect = label.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2((col + 0.5f) / 4f, 1f - ((row + 0.5f) / 10f));
                    rect.anchorMax = rect.anchorMin;
                    rect.sizeDelta = new Vector2(86f, 28f);
                    rect.anchoredPosition = new Vector2(row % 2 == 0 ? 12f : -12f, 0f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, ((row + col) % 3 - 1) * 10f);

                    var text = label.GetComponent<Text>();
                    text.font = font;
                    text.text = labels[(row * 4 + col) % labels.Length];
                    text.fontSize = 13 + ((row + col) % 3) * 2;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.color = new Color32(0x8A, 0x83, 0x78, 0xFF);
                    text.raycastTarget = false;
                }
            }
        }

        private void ApplyInputBarLayout(float inputPillHeight)
        {
            var sendSize = Mathf.Clamp(inputPillHeight, 40f, 46f);
            var inputRight = sendSize + 14f;
            var iconSize = Mathf.Max(34f, inputPillHeight - 8f);

            SetStretchPill(inputField != null ? inputField.transform as RectTransform : null, 8f, inputRight, inputPillHeight);
            SetInputTextInsets(inputField != null ? inputField.textComponent != null ? inputField.textComponent.rectTransform : null : null, 48f, 90f);
            SetInputTextInsets(inputPlaceholderText != null ? inputPlaceholderText.rectTransform : null, 48f, 90f);

            SetEdgeButtonRect(emojiButtonText != null ? emojiButtonText.transform.parent as RectTransform : null, true, 31f, iconSize);
            SetEdgeButtonRect(attachButtonText != null ? attachButtonText.transform.parent as RectTransform : null, false, sendSize + 72f, iconSize);
            SetEdgeButtonRect(cameraButtonText != null ? cameraButtonText.transform.parent as RectTransform : null, false, sendSize + 38f, iconSize);
            SetEdgeButtonRect(sendButton != null ? sendButton.transform as RectTransform : null, false, 8f + sendSize * 0.5f, sendSize);
        }

        private void RebuildMessages()
        {
            if (messageContent == null || bubblePrefab == null)
                return;

            for (var i = messageContent.childCount - 1; i >= 0; i--)
            {
                var child = messageContent.GetChild(i);
                if (child == bubblePrefab.transform || (dateChipTemplate != null && child == dateChipTemplate.transform))
                    continue;

                DestroyContentChild(child.gameObject);
            }

            bubblePrefab.gameObject.SetActive(false);
            if (dateChipTemplate != null)
                dateChipTemplate.SetActive(false);

            var messages = MomotalkMessageData.CreateStage5Preview(contactName);
            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].IsDateChip)
                {
                    CreateDateChip(messages[i].MessageText);
                    continue;
                }

                var bubble = Instantiate(bubblePrefab, messageContent);
                bubble.name = messages[i].IsUser ? "Bubble_You" : "Bubble_" + contactName;
                bubble.Bind(messages[i], theme);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
            if (messageScrollRect != null)
                messageScrollRect.verticalNormalizedPosition = 0f;
        }

        private void CreateDateChip(string label)
        {
            if (dateChipTemplate == null)
                return;

            var dateChip = Instantiate(dateChipTemplate, messageContent);
            dateChip.name = "DateChip_" + (string.IsNullOrWhiteSpace(label) ? "Today" : label);
            ApplyDateChip(dateChip, label);
            dateChip.SetActive(true);
        }

        private static void ApplyDateChip(GameObject dateChip, string label)
        {
            if (dateChip == null)
                return;

            var graphics = dateChip.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;

            var background = dateChip.GetComponent<Image>();
            if (background != null)
                background.color = new Color32(0xE1, 0xF3, 0xFB, 0xE8);

            var text = dateChip.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = string.IsNullOrWhiteSpace(label) ? "Today" : label;
                text.fontSize = 11;
                text.color = new Color32(0x4F, 0x68, 0x75, 0xFF);
            }
        }

        private Text FindTextUnder(string objectName)
        {
            var target = FindDescendant(transform, objectName);
            return target != null ? target.GetComponentInChildren<Text>(true) : null;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindDescendant(root.GetChild(i), childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static void SetIconText(Text text, string value, int fontSize)
        {
            SetIconText(text, value, fontSize, Color.white);
        }

        private static void SetIconText(Text text, string value, int fontSize, Color color)
        {
            if (text == null)
                return;

            text.gameObject.SetActive(true);
            text.enabled = true;
            text.text = value;
            text.color = color;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private static void SetStretchPill(RectTransform rect, float left, float right, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, 0f);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void SetInputTextInsets(RectTransform rect, float left, float right)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, 2f);
            rect.offsetMax = new Vector2(-right, -2f);
        }

        private static void SetEdgeButtonRect(RectTransform rect, bool leftAligned, float centerOffset, float size)
        {
            if (rect == null)
                return;

            var anchorX = leftAligned ? 0f : 1f;
            rect.anchorMin = new Vector2(anchorX, 0.5f);
            rect.anchorMax = new Vector2(anchorX, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(leftAligned ? centerOffset : -centerOffset, 0f);
            rect.sizeDelta = new Vector2(size, size);
        }

        private static void SetTopBarRect(RectTransform rect, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void SetBottomBarRect(RectTransform rect, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void SetStretchOffsets(RectTransform rect, float top, float bottom)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private static void SetRectHeight(RectTransform rect, float height)
        {
            if (rect == null)
                return;

            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
        }

        private static void DestroyContentChild(GameObject child)
        {
            if (child == null)
                return;

            child.SetActive(false);
            child.transform.SetParent(null, false);

            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        private static void ApplyTextStyle(Text text, Color color, int fontSize)
        {
            if (text == null)
                return;

            text.color = color;
            text.fontSize = fontSize;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
