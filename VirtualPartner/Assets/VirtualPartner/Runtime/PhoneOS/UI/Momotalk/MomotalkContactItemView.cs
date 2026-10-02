using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkContactItemView : MonoBehaviour
    {
        [SerializeField] private MomotalkTheme theme;
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text avatarText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text previewText;
        [SerializeField] private Text timeText;
        [SerializeField] private GameObject unreadRoot;
        [SerializeField] private Image unreadBackground;
        [SerializeField] private Text unreadText;

        private Action<string, string> selected;
        private string contactId;
        private string displayName;
        private Color avatarColor = Color.clear;

        public void Bind(string id, string name, string preview, string time, int unreadCount, Action<string, string> onSelected)
        {
            Bind(id, name, preview, time, unreadCount, Color.clear, onSelected);
        }

        public void Bind(string id, string name, string preview, string time, int unreadCount, Color avatarTint, Action<string, string> onSelected)
        {
            ResolveReferences();

            contactId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(name) ? "Toki" : name;
            selected = onSelected;
            avatarColor = avatarTint;

            if (avatarText != null)
                avatarText.text = displayName.Substring(0, 1).ToUpperInvariant();
            if (nameText != null)
                nameText.text = displayName;
            if (previewText != null)
                previewText.text = preview ?? string.Empty;
            if (timeText != null)
                timeText.text = time ?? string.Empty;
            if (unreadText != null)
                unreadText.text = Mathf.Max(1, unreadCount).ToString();
            if (unreadRoot != null)
                unreadRoot.SetActive(unreadCount > 0);

            ApplyTheme(true);

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyTheme(true);
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyTheme(false);
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        private void HandleClicked()
        {
            selected?.Invoke(contactId, displayName);
        }

        private void ResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
        }

        private void ApplyTheme(bool applyLayout)
        {
            if (applyLayout)
                ApplyLayoutTokens();

            if (backgroundImage != null)
            {
                backgroundImage.sprite = theme != null ? theme.ContactItemBackground : null;
                backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.color = theme != null ? theme.ContactItemColor : Color.white;
                backgroundImage.raycastTarget = true;
            }

            if (button != null && backgroundImage != null)
            {
                button.targetGraphic = backgroundImage;
                var colors = button.colors;
                colors.highlightedColor = new Color32(0xF6, 0xF8, 0xF9, 0xFF);
                colors.pressedColor = new Color32(0xEA, 0xEE, 0xF0, 0xFF);
                colors.selectedColor = colors.highlightedColor;
                button.colors = colors;
            }

            if (avatarImage != null)
            {
                avatarImage.color = avatarColor.a > 0f
                    ? avatarColor
                    : (theme != null ? theme.AccentColor : (Color)new Color32(0x00, 0xA8, 0x84, 0xFF));
                avatarImage.raycastTarget = false;
            }

            ApplyText(
                nameText,
                theme != null ? theme.PrimaryTextColor : (Color)new Color32(0x20, 0x21, 0x24, 0xFF),
                theme != null ? theme.ContactNameFontSize : 16,
                false);
            ApplyText(
                previewText,
                theme != null ? theme.SecondaryTextColor : (Color)new Color32(0x66, 0x77, 0x81, 0xFF),
                theme != null ? theme.ContactPreviewFontSize : 13,
                false);
            ApplyText(
                timeText,
                theme != null ? theme.MutedTextColor : (Color)new Color32(0x86, 0x96, 0xA0, 0xFF),
                theme != null ? theme.MessageTimeFontSize : 11,
                false);
            ApplyText(avatarText, Color.white, 15, false);
            ApplyText(unreadText, Color.white, 11, false);

            if (unreadBackground != null)
            {
                unreadBackground.sprite = theme != null ? theme.UnreadBadgeSprite : null;
                unreadBackground.type = unreadBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                unreadBackground.color = theme != null ? theme.UnreadBadgeColor : new Color32(0x25, 0xD3, 0x66, 0xFF);
                unreadBackground.raycastTarget = false;
            }
        }

        private void ApplyLayoutTokens()
        {
            var itemHeight = theme != null ? theme.ContactItemHeight : 74f;
            if (layoutElement != null)
            {
                layoutElement.minHeight = itemHeight;
                layoutElement.preferredHeight = itemHeight;
            }

            var rect = transform as RectTransform;
            if (rect != null)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, itemHeight);

            SetRectSize(avatarImage != null ? avatarImage.rectTransform : null, theme != null ? theme.AvatarSize : 48f);
            SetContactTextRects();

            var badgeSize = theme != null ? theme.UnreadBadgeSize : 20f;
            if (unreadRoot != null)
                SetRectSize(unreadRoot.transform as RectTransform, badgeSize);
        }

        private void SetContactTextRects()
        {
            var avatarSize = theme != null ? theme.AvatarSize : 48f;
            var left = Mathf.RoundToInt(18f + avatarSize + 12f);
            var right = 72f;

            SetTextRect(nameText != null ? nameText.rectTransform : null, left, right, 9f, 30f);
            SetTextRect(previewText != null ? previewText.rectTransform : null, left, right + 18f, 34f, 22f);
            SetTextRect(timeText != null ? timeText.rectTransform : null, 0f, 14f, 10f, 24f, true);
        }

        private static void SetRectSize(RectTransform rect, float size)
        {
            if (rect == null)
                return;

            rect.sizeDelta = new Vector2(size, size);
        }

        private static void SetTextRect(RectTransform rect, float left, float right, float top, float height, bool alignRight = false)
        {
            if (rect == null)
                return;

            if (alignRight)
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-right, -top);
                rect.sizeDelta = new Vector2(60f, height);
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -(top + height));
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void ApplyText(Text text, Color color, int fontSize, bool wrap)
        {
            if (text == null)
                return;

            text.color = color;
            text.fontSize = fontSize;
            text.raycastTarget = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (text.name == "NameText")
                text.fontStyle = FontStyle.Bold;
        }
    }
}
