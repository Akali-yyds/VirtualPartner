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

        public void Bind(string id, string name, string preview, string time, int unreadCount, Action<string, string> onSelected)
        {
            ResolveReferences();

            contactId = id ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(name) ? "Toki" : name;
            selected = onSelected;

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

            ApplyTheme();

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyTheme();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyTheme();
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
        }

        private void ApplyTheme()
        {
            if (backgroundImage != null)
            {
                backgroundImage.sprite = theme != null ? theme.ContactItemBackground : null;
                backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                backgroundImage.color = theme != null ? Color.white : new Color32(0xE5, 0xEA, 0xEF, 0xFF);
                backgroundImage.raycastTarget = true;
            }

            if (avatarImage != null)
            {
                avatarImage.color = new Color32(0xD8, 0xF3, 0xFA, 0xFF);
                avatarImage.raycastTarget = false;
            }

            ApplyText(nameText, theme != null ? theme.PrimaryTextColor : (Color)new Color32(0x2B, 0x33, 0x42, 0xFF), 16, false);
            ApplyText(previewText, theme != null ? theme.SecondaryTextColor : (Color)new Color32(0x8C, 0x95, 0xA3, 0xFF), 12, true);
            ApplyText(timeText, theme != null ? theme.SecondaryTextColor : (Color)new Color32(0x8C, 0x95, 0xA3, 0xFF), 11, false);
            ApplyText(avatarText, Color.white, 18, false);
            ApplyText(unreadText, Color.white, 11, false);

            if (unreadBackground != null)
            {
                unreadBackground.sprite = theme != null ? theme.UnreadBadgeSprite : null;
                unreadBackground.type = unreadBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                unreadBackground.color = theme != null ? theme.UnreadBadgeColor : new Color32(0xF7, 0x88, 0xA6, 0xFF);
                unreadBackground.raycastTarget = false;
            }
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
        }
    }
}
