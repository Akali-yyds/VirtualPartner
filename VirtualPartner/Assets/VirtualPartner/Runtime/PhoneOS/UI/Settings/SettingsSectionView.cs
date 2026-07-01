using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class SettingsSectionView : MonoBehaviour
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform optionsRoot;

        public RectTransform OptionsRoot
        {
            get
            {
                ResolveReferences();
                return optionsRoot;
            }
        }

        public void Configure(string title, PhoneOSStyle style)
        {
            ResolveReferences();

            if (backgroundImage != null)
            {
                backgroundImage.sprite = style != null ? style.RoundedPanelSprite : null;
                backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.color = style != null ? style.PanelColor : new Color(1f, 1f, 1f, 0.92f);
                backgroundImage.raycastTarget = false;
            }

            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
                titleText.color = style != null ? style.SecondaryTextColor : new Color32(0x66, 0x6A, 0x70, 0xFF);
                titleText.raycastTarget = false;
                titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                titleText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private void ResolveReferences()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            if (optionsRoot == null)
                optionsRoot = transform as RectTransform;
        }
    }
}
