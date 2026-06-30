using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneAppWindowView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;

        private void Awake()
        {
            ApplyStyle();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        public void Bind(PhoneAppDefinition definition)
        {
            if (definition == null)
                return;

            SetTitle(definition.DisplayName);
            SetDescription(definition.DisplayName + " App Coming Soon");
        }

        public void SetTitle(string title)
        {
            if (titleText != null)
                titleText.text = title ?? string.Empty;
        }

        public void SetDescription(string description)
        {
            if (descriptionText != null)
                descriptionText.text = description ?? string.Empty;
        }

        public void ApplyStyle()
        {
            if (style == null)
                return;

            if (backgroundImage != null)
            {
                backgroundImage.sprite = null;
                backgroundImage.color = Color.white;
                backgroundImage.type = Image.Type.Simple;
                backgroundImage.raycastTarget = false;
            }

            if (titleText != null)
            {
                titleText.color = style.PrimaryTextColor;
                titleText.fontSize = Mathf.RoundToInt(style.WidgetMediumFontSize + 2f);
                titleText.raycastTarget = false;
            }

            if (descriptionText != null)
            {
                descriptionText.color = style.SecondaryTextColor;
                descriptionText.fontSize = Mathf.RoundToInt(style.WidgetMediumFontSize);
                descriptionText.raycastTarget = false;
            }
        }
    }
}
