using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkChatBubbleView : MonoBehaviour
    {
        [SerializeField] private MomotalkTheme theme;
        [SerializeField] private HorizontalLayoutGroup rowLayout;
        [SerializeField] private Image bubbleBackground;
        [SerializeField] private Text senderText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text timeText;

        public void Bind(MomotalkMessageData data, MomotalkTheme overrideTheme)
        {
            ResolveReferences();

            if (overrideTheme != null)
                theme = overrideTheme;

            var message = data ?? new MomotalkMessageData("Toki", string.Empty, string.Empty, false);
            var isUser = message.IsUser;

            if (rowLayout != null)
                rowLayout.childAlignment = isUser ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            if (bubbleBackground != null)
            {
                bubbleBackground.sprite = ResolveBubbleSprite(isUser);
                bubbleBackground.type = bubbleBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                bubbleBackground.color = bubbleBackground.sprite != null ? Color.white : ResolveBubbleColor(isUser);
                bubbleBackground.raycastTarget = false;
            }

            if (senderText != null)
            {
                senderText.text = isUser ? string.Empty : message.SenderName;
                senderText.gameObject.SetActive(!isUser);
            }

            if (messageText != null)
                messageText.text = message.MessageText ?? string.Empty;
            if (timeText != null)
                timeText.text = message.TimeText ?? string.Empty;

            ApplyText(senderText, theme != null ? theme.PrimaryTextColor : (Color)new Color32(0x2B, 0x33, 0x42, 0xFF), 11, false);
            ApplyText(messageText, Color.white, 13, true);
            ApplyText(timeText, new Color(1f, 1f, 1f, 0.82f), 10, false);

            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (rowLayout == null)
                rowLayout = GetComponent<HorizontalLayoutGroup>();
        }

        private Sprite ResolveBubbleSprite(bool isUser)
        {
            if (theme == null)
                return null;

            return isUser ? theme.RightBubbleSprite : theme.LeftBubbleSprite;
        }

        private Color ResolveBubbleColor(bool isUser)
        {
            if (theme == null)
                return isUser ? new Color32(0x5A, 0x98, 0xD4, 0xFF) : new Color32(0x56, 0x66, 0x7C, 0xFF);

            return isUser ? theme.RightBubbleColor : theme.LeftBubbleColor;
        }

        private static void ApplyText(Text text, Color color, int fontSize, bool wrap)
        {
            if (text == null)
                return;

            text.color = color;
            text.fontSize = fontSize;
            text.raycastTarget = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = wrap ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
        }
    }
}
