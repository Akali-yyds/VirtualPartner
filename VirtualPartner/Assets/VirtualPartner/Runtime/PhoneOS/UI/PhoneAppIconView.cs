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

        public void ApplyStyle(PhoneOSStyle style, bool dockIcon = false)
        {
            if (style == null)
                return;

            var cellSize = dockIcon ? style.DockCellSize : style.AppGridCellSize;
            var iconSize = dockIcon ? style.DockIconSize : style.AppIconSize;

            var layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = cellSize.x;
                layoutElement.preferredHeight = cellSize.y;
            }

            if (iconImage != null)
                iconImage.rectTransform.sizeDelta = iconSize;
            if (labelText != null)
            {
                labelText.color = dockIcon ? style.DockIconLabelColor : style.PrimaryTextColor;
                labelText.fontSize = Mathf.RoundToInt(style.AppIconLabelFontSize);
            }
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
