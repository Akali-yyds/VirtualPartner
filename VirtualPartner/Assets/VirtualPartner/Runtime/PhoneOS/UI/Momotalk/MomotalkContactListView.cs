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

        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Button tokiButton;
        [SerializeField] private Image cardBackground;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text subtitleText;

        private Action<string, string> contactSelected;

        public void Bind(Action<string, string> onContactSelected)
        {
            ResolveReferences();
            contactSelected = onContactSelected;

            if (titleText != null)
                titleText.text = TokiDisplayName;
            if (subtitleText != null)
                subtitleText.text = "PhoneOS preview contact";

            ApplyStyle();

            if (tokiButton != null)
            {
                tokiButton.onClick.RemoveListener(HandleTokiClicked);
                tokiButton.onClick.AddListener(HandleTokiClicked);
            }
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

        private void OnDestroy()
        {
            if (tokiButton != null)
                tokiButton.onClick.RemoveListener(HandleTokiClicked);
        }

        private void HandleTokiClicked()
        {
            contactSelected?.Invoke(TokiContactId, TokiDisplayName);
        }

        private void ResolveReferences()
        {
            if (tokiButton == null)
                tokiButton = GetComponentInChildren<Button>(true);
        }

        private void ApplyStyle()
        {
            if (cardBackground != null)
            {
                cardBackground.sprite = style != null ? style.RoundedPanelSprite : null;
                cardBackground.type = cardBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                cardBackground.color = style != null ? style.PanelColor : new Color(1f, 1f, 1f, 0.94f);
                cardBackground.raycastTarget = true;
            }

            if (avatarImage != null)
            {
                avatarImage.color = new Color32(0xFF, 0xA9, 0xC8, 0xFF);
                avatarImage.raycastTarget = false;
            }

            if (titleText != null)
            {
                titleText.color = style != null ? style.PrimaryTextColor : (Color)new Color32(0x24, 0x28, 0x2C, 0xFF);
                titleText.raycastTarget = false;
                titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                titleText.verticalOverflow = VerticalWrapMode.Truncate;
            }

            if (subtitleText != null)
            {
                subtitleText.color = style != null ? style.SecondaryTextColor : (Color)new Color32(0x66, 0x6A, 0x70, 0xFF);
                subtitleText.raycastTarget = false;
                subtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                subtitleText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }
    }
}
