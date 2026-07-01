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
        [SerializeField] private Image topBarBackground;
        [SerializeField] private Image peachMarkImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text activeTabText;
        [SerializeField] private Text secondaryTabText;
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
                pageBackground.color = theme != null ? Color.white : new Color32(0xFA, 0xFB, 0xFD, 0xFF);
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

            if (peachMarkImage != null)
            {
                peachMarkImage.sprite = theme != null ? theme.PeachMarkSprite : null;
                peachMarkImage.color = Color.white;
                peachMarkImage.raycastTarget = false;
            }

            ApplyText(titleText, Color.white, 19, false);
            ApplyText(activeTabText, Color.white, 13, false);
            ApplyText(secondaryTabText, new Color(1f, 1f, 1f, 0.64f), 13, false);
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
