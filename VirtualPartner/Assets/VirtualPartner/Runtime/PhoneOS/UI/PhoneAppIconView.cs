using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneAppIconView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text labelText;
        [SerializeField] private Button button;

        private PhoneAppDefinition definition;
        private Action<PhoneAppDefinition> clicked;

        public PhoneAppDefinition Definition => definition;

        public void ApplyStyle(PhoneOSStyle style)
        {
            if (style == null)
                return;

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = style.AppGridCellSize.x;
                layoutElement.preferredHeight = style.AppGridCellSize.y;
            }

            if (iconImage != null)
                iconImage.rectTransform.sizeDelta = style.AppIconSize;
            if (labelText != null)
                labelText.color = style.PrimaryTextColor;
        }

        public void Bind(PhoneAppDefinition appDefinition, Action<PhoneAppDefinition> onClicked)
        {
            definition = appDefinition;
            clicked = onClicked;

            if (iconImage != null)
            {
                iconImage.sprite = appDefinition != null ? appDefinition.Icon : null;
                iconImage.enabled = iconImage.sprite != null;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

            if (labelText != null)
            {
                labelText.text = appDefinition != null ? appDefinition.DisplayName : string.Empty;
                labelText.raycastTarget = false;
            }

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClick);
        }

        private void HandleClick()
        {
            if (definition == null)
                return;

            clicked?.Invoke(definition);
        }
    }
}
