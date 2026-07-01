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
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text statusText;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect messageScrollRect;
        [SerializeField] private RectTransform messageContent;
        [SerializeField] private MomotalkChatBubbleView bubblePrefab;
        [SerializeField] private Image inputBarBackground;
        [SerializeField] private InputField inputField;
        [SerializeField] private Text inputPlaceholderText;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text sendButtonText;

        private string contactName = "Toki";
        private System.Action backRequested;

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
            ApplyStyle();
            RefreshContent();
            RegisterButtonListeners();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyStyle();
            if (titleText != null)
                titleText.text = contactName;
            if (statusText != null)
                statusText.text = "Available";
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
            Debug.Log("[PhoneOS] Momotalk preview Send clicked. Real chat is not connected in Stage 5.", this);
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
            if (messageScrollRect != null && messageContent == null)
                messageContent = messageScrollRect.content;
        }

        private void RefreshContent()
        {
            if (titleText != null)
                titleText.text = contactName;
            if (statusText != null)
                statusText.text = "Available";

            if (inputPlaceholderText != null)
                inputPlaceholderText.text = "Type a message...";
            if (sendButtonText != null)
                sendButtonText.text = "Send";

            RebuildMessages();
        }

        private void ApplyStyle()
        {
            if (pageBackground != null)
            {
                pageBackground.color = theme != null ? theme.ContactBackgroundColor : new Color32(0xFA, 0xF2, 0xF5, 0xFF);
                pageBackground.raycastTarget = false;
            }

            if (topBarBackground != null)
            {
                topBarBackground.sprite = theme != null ? theme.TopBarBackground : null;
                topBarBackground.type = topBarBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                topBarBackground.color = topBarBackground.sprite != null
                    ? Color.white
                    : (theme != null ? theme.TopBarColor : (Color)new Color32(0xF7, 0x88, 0xA6, 0xFF));
                topBarBackground.raycastTarget = false;
            }

            if (avatarImage != null)
            {
                avatarImage.color = new Color32(0xF8, 0xD3, 0xDF, 0xFF);
                avatarImage.raycastTarget = false;
            }

            if (inputBarBackground != null)
            {
                inputBarBackground.sprite = theme != null ? theme.InputBarBackground : null;
                inputBarBackground.type = inputBarBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                inputBarBackground.color = inputBarBackground.sprite != null
                    ? Color.white
                    : (theme != null ? theme.InputBarColor : (Color)new Color32(0xF0, 0xF1, 0xF3, 0xFF));
                inputBarBackground.raycastTarget = false;
            }

            ApplyTextStyle(titleText, Color.white, 16);
            ApplyTextStyle(statusText, new Color(1f, 1f, 1f, 0.76f), 11);
            ApplyTextStyle(inputPlaceholderText, theme != null ? theme.SecondaryTextColor : (Color)new Color32(0x8C, 0x95, 0xA3, 0xFF), 12);
            ApplyTextStyle(sendButtonText, Color.white, 13);
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

        private void RebuildMessages()
        {
            if (messageContent == null || bubblePrefab == null)
                return;

            for (var i = messageContent.childCount - 1; i >= 0; i--)
            {
                var child = messageContent.GetChild(i);
                if (child == bubblePrefab.transform)
                    continue;

                DestroyContentChild(child.gameObject);
            }

            bubblePrefab.gameObject.SetActive(false);
            var messages = MomotalkMessageData.CreateStage5Preview(contactName);
            for (var i = 0; i < messages.Count; i++)
            {
                var bubble = Instantiate(bubblePrefab, messageContent);
                bubble.name = messages[i].IsUser ? "Bubble_You" : "Bubble_" + contactName;
                bubble.Bind(messages[i], theme);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(messageContent);
            if (messageScrollRect != null)
                messageScrollRect.verticalNormalizedPosition = 0f;
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
