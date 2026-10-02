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
        [SerializeField] private VerticalLayoutGroup bubbleLayout;
        [SerializeField] private LayoutElement bubbleLayoutElement;
        [SerializeField] private Text senderText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text timeText;
        [SerializeField] private Image imagePreviewBackground;
        [SerializeField] private Text imagePreviewText;
        [SerializeField] private LayoutElement imagePreviewLayoutElement;
        [SerializeField] private float minBubbleWidth = 76f;
        [SerializeField] private float maxBubbleWidth = 292f;

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
                bubbleBackground.type = bubbleBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                bubbleBackground.color = ResolveBubbleColor(isUser);
                bubbleBackground.raycastTarget = false;
            }

            ApplyBubblePadding();

            if (senderText != null)
            {
                senderText.text = string.Empty;
                senderText.gameObject.SetActive(false);
            }

            ApplyImagePreview(message);

            if (messageText != null)
            {
                var text = message.IsImageMessage ? message.CaptionText : message.MessageText;
                messageText.text = text ?? string.Empty;
                messageText.gameObject.SetActive(!string.IsNullOrWhiteSpace(messageText.text));
            }

            if (timeText != null)
            {
                timeText.text = message.IsUser && !string.IsNullOrWhiteSpace(message.TimeText)
                    ? message.TimeText + "  \u2713\u2713"
                    : message.TimeText ?? string.Empty;
                timeText.alignment = TextAnchor.LowerRight;
            }

            ApplyText(senderText, theme != null ? theme.SecondaryTextColor : (Color)new Color32(0x7A, 0x7F, 0x87, 0xFF), 11, false);
            ApplyText(
                messageText,
                theme != null ? theme.PrimaryTextColor : (Color)new Color32(0x20, 0x21, 0x24, 0xFF),
                theme != null ? theme.MessageFontSize : 14,
                true);
            ApplyText(
                timeText,
                theme != null ? theme.MutedTextColor : (Color)new Color32(0x86, 0x96, 0xA0, 0xFF),
                theme != null ? theme.MessageTimeFontSize : 11,
                false);

            ApplyBubbleWidth(message);

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
            if (bubbleLayout == null && bubbleBackground != null)
                bubbleLayout = bubbleBackground.GetComponent<VerticalLayoutGroup>();
            if (bubbleLayoutElement == null && bubbleBackground != null)
                bubbleLayoutElement = bubbleBackground.GetComponent<LayoutElement>();
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

        private void ApplyBubblePadding()
        {
            if (bubbleLayout == null)
                return;

            var horizontal = Mathf.RoundToInt(theme != null ? theme.BubbleHorizontalPadding : 12f);
            var vertical = Mathf.RoundToInt(theme != null ? theme.BubbleVerticalPadding : 7f);
            bubbleLayout.padding.left = horizontal;
            bubbleLayout.padding.right = horizontal;
            bubbleLayout.padding.top = vertical;
            bubbleLayout.padding.bottom = vertical;
            bubbleLayout.spacing = 3;
            bubbleLayout.childAlignment = TextAnchor.UpperLeft;
        }

        private void ApplyImagePreview(MomotalkMessageData message)
        {
            var isImage = message != null && message.IsImageMessage;
            if (!isImage)
            {
                if (imagePreviewBackground != null)
                    imagePreviewBackground.gameObject.SetActive(false);
                return;
            }

            EnsureImagePreview();

            if (imagePreviewBackground == null)
                return;

            imagePreviewBackground.gameObject.SetActive(true);
            imagePreviewBackground.sprite = theme != null ? theme.ImageMessageMaskSprite : null;
            imagePreviewBackground.type = imagePreviewBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            imagePreviewBackground.color = WithAlpha(theme != null ? theme.AccentColor : (Color)new Color32(0x00, 0xA8, 0x84, 0xFF), 0.18f);
            imagePreviewBackground.raycastTarget = false;

            if (imagePreviewLayoutElement != null)
            {
                imagePreviewLayoutElement.minHeight = theme != null ? theme.ImageMessageMaxHeight : 160f;
                imagePreviewLayoutElement.preferredHeight = theme != null ? theme.ImageMessageMaxHeight : 160f;
                imagePreviewLayoutElement.flexibleWidth = 1;
            }

            if (imagePreviewText != null)
            {
                imagePreviewText.text = string.IsNullOrWhiteSpace(message.ImagePreviewLabel)
                    ? "Image preview"
                    : message.ImagePreviewLabel;
                imagePreviewText.color = theme != null ? theme.AccentColor : (Color)new Color32(0x00, 0xA8, 0x84, 0xFF);
                imagePreviewText.fontSize = theme != null ? theme.ContactPreviewFontSize : 13;
                imagePreviewText.alignment = TextAnchor.MiddleCenter;
                imagePreviewText.raycastTarget = false;
            }
        }

        private void EnsureImagePreview()
        {
            if (imagePreviewBackground != null)
                return;

            if (bubbleBackground == null)
                return;

            var preview = new GameObject("ImagePreview", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            preview.transform.SetParent(bubbleBackground.transform, false);

            imagePreviewBackground = preview.GetComponent<Image>();
            imagePreviewLayoutElement = preview.GetComponent<LayoutElement>();

            var previewTransform = preview.transform as RectTransform;
            if (previewTransform != null)
            {
                previewTransform.anchorMin = new Vector2(0f, 0.5f);
                previewTransform.anchorMax = new Vector2(1f, 0.5f);
                previewTransform.pivot = new Vector2(0.5f, 0.5f);
            }

            var label = new GameObject("ImagePreviewText", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(preview.transform, false);
            imagePreviewText = label.GetComponent<Text>();
            if (messageText != null)
                imagePreviewText.font = messageText.font;

            var labelTransform = label.transform as RectTransform;
            if (labelTransform != null)
            {
                labelTransform.anchorMin = Vector2.zero;
                labelTransform.anchorMax = Vector2.one;
                labelTransform.offsetMin = Vector2.zero;
                labelTransform.offsetMax = Vector2.zero;
            }

            var insertIndex = senderText != null ? senderText.transform.GetSiblingIndex() + 1 : 0;
            preview.transform.SetSiblingIndex(insertIndex);
        }

        private void ApplyBubbleWidth(MomotalkMessageData message)
        {
            if (bubbleLayoutElement == null)
                return;

            var minWidth = theme != null ? theme.BubbleMinWidth : minBubbleWidth;
            var maxWidth = theme != null ? theme.BubbleMaxWidth : maxBubbleWidth;
            float preferred;

            if (message != null && message.IsImageMessage)
            {
                preferred = theme != null ? theme.ImageMessageMaxWidth : maxWidth;
            }
            else if (messageText != null)
            {
                var text = messageText.text ?? string.Empty;
                var fontSize = theme != null ? theme.MessageFontSize : 14;
                var horizontalPadding = theme != null ? theme.BubbleHorizontalPadding * 2f : 28f;
                preferred = EstimateTextWidth(text, fontSize) + horizontalPadding;
                if (timeText != null)
                    preferred = Mathf.Max(preferred, EstimateTextWidth(timeText.text, theme != null ? theme.MessageTimeFontSize : 11) + horizontalPadding + 24f);
            }
            else
            {
                preferred = minWidth;
            }

            bubbleLayoutElement.minWidth = minWidth;
            bubbleLayoutElement.preferredWidth = Mathf.Clamp(preferred, minWidth, maxWidth);
        }

        private static float EstimateTextWidth(string text, int fontSize)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0f;

            var longestLine = 0f;
            var currentLine = 0f;
            for (var i = 0; i < text.Length; i++)
            {
                var character = text[i];
                if (character == '\n')
                {
                    longestLine = Mathf.Max(longestLine, currentLine);
                    currentLine = 0f;
                    continue;
                }

                currentLine += character == ' ' ? fontSize * 0.32f : fontSize * 0.48f;
            }

            longestLine = Mathf.Max(longestLine, currentLine);
            return longestLine;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
