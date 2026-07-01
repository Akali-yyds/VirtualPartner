using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkChatView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Text titleText;
        [SerializeField] private Text[] messageTexts;
        [SerializeField] private InputField inputField;
        [SerializeField] private Text inputPlaceholderText;
        [SerializeField] private Button sendButton;
        [SerializeField] private Text sendButtonText;

        private string contactName = "Toki";

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

            if (sendButton != null)
            {
                sendButton.onClick.RemoveListener(HandleSendClicked);
                sendButton.onClick.AddListener(HandleSendClicked);
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyStyle();
            RefreshContent();
        }

        private void OnDestroy()
        {
            if (sendButton != null)
                sendButton.onClick.RemoveListener(HandleSendClicked);
        }

        private void HandleSendClicked()
        {
            Debug.Log("[PhoneOS] Momotalk preview Send clicked. Real chat is not connected in Stage 4.", this);
        }

        private void ResolveReferences()
        {
            if (inputPlaceholderText == null && inputField != null)
                inputPlaceholderText = inputField.placeholder as Text;
            if (sendButton == null)
                sendButton = GetComponentInChildren<Button>(true);
        }

        private void RefreshContent()
        {
            if (titleText != null)
                titleText.text = contactName;

            if (messageTexts != null && messageTexts.Length >= 3)
            {
                messageTexts[0].text = contactName + ": Hello, Sensei.";
                messageTexts[1].text = "You: Hi " + contactName + ".";
                messageTexts[2].text = contactName + ": This is the new PhoneOS Momotalk preview.";
            }

            if (inputPlaceholderText != null)
                inputPlaceholderText.text = "Type a message...";
            if (sendButtonText != null)
                sendButtonText.text = "Send";
        }

        private void ApplyStyle()
        {
            ApplyTextStyle(titleText, style != null ? style.PrimaryTextColor : (Color)new Color32(0x24, 0x28, 0x2C, 0xFF), 18);

            if (messageTexts != null)
            {
                for (var i = 0; i < messageTexts.Length; i++)
                {
                    var messageColor = i == 1
                        ? Color.white
                        : (style != null ? style.PrimaryTextColor : (Color)new Color32(0x24, 0x28, 0x2C, 0xFF));
                    ApplyTextStyle(messageTexts[i], messageColor, 13);
                }
            }

            ApplyTextStyle(inputPlaceholderText, style != null ? style.MutedTextColor : (Color)new Color32(0x8A, 0x8D, 0x93, 0xFF), 12);
            ApplyTextStyle(sendButtonText, Color.white, 13);
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
