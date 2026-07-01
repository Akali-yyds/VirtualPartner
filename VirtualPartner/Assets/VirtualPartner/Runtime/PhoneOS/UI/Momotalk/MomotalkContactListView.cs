using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkContactListView : MonoBehaviour
    {
        private const string TokiContactId = "toki";
        private const string TokiDisplayName = "Toki";
        private const string TokiPreview = "This is the new PhoneOS Momotalk preview.";
        private const string TokiTime = "18:25";

        [SerializeField] private MomotalkTheme theme;
        [SerializeField] private Image pageBackground;
        [SerializeField] private Image appBarBackground;
        [SerializeField] private Image tabBarBackground;
        [SerializeField] private Image peachMarkImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messagesTabText;
        [SerializeField] private Text statusTabText;
        [SerializeField] private Text callsTabText;
        [SerializeField] private Image activeTabUnderline;
        [SerializeField] private MomotalkContactItemView tokiItem;

        private Action<string, string> contactSelected;

        public void Bind(Action<string, string> onContactSelected)
        {
            ResolveReferences();
            contactSelected = onContactSelected;

            ApplyStyle();
            if (tokiItem != null)
                tokiItem.Bind(TokiContactId, TokiDisplayName, TokiPreview, TokiTime, 1, contactSelected);
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyStyle();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyStyle();
        }

        private void ResolveReferences()
        {
            if (tokiItem == null)
                tokiItem = GetComponentInChildren<MomotalkContactItemView>(true);
        }

        private void ApplyStyle()
        {
            if (pageBackground != null)
            {
                pageBackground.sprite = theme != null ? theme.ContactListBackground : null;
                pageBackground.type = pageBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                pageBackground.color = theme != null ? theme.ContactBackgroundColor : new Color32(0xFF, 0xF7, 0xFA, 0xFF);
                pageBackground.raycastTarget = false;
            }

            ApplyBarImage(appBarBackground);
            ApplyBarImage(tabBarBackground);

            if (activeTabUnderline != null)
            {
                activeTabUnderline.color = Color.white;
                activeTabUnderline.raycastTarget = false;
            }

            if (peachMarkImage != null)
            {
                peachMarkImage.sprite = theme != null ? theme.PeachMarkSprite : null;
                peachMarkImage.color = Color.white;
                peachMarkImage.raycastTarget = false;
            }

            ApplyText(titleText, Color.white, 19, false);
            ApplyText(messagesTabText, Color.white, 13, false);
            ApplyText(statusTabText, new Color(1f, 1f, 1f, 0.68f), 13, false);
            ApplyText(callsTabText, new Color(1f, 1f, 1f, 0.68f), 13, false);
        }

        private void ApplyBarImage(Image image)
        {
            if (image == null)
                return;

            image.sprite = theme != null ? theme.TopBarBackground : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = theme != null ? theme.TopBarColor : new Color32(0xF7, 0x8F, 0xB3, 0xFF);
            image.raycastTarget = false;
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
