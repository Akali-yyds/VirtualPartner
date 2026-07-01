using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class SettingsOptionButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Text labelText;

        private Action<string> selected;
        private string optionId;
        private string baseLabel;

        public string OptionId => optionId;

        public void Bind(string id, string label, Action<string> onSelected)
        {
            ResolveReferences();

            optionId = id ?? string.Empty;
            baseLabel = label ?? string.Empty;
            selected = onSelected;

            if (labelText != null)
                labelText.text = baseLabel;

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
        }

        public void SetLabel(string value)
        {
            ResolveReferences();
            if (labelText != null)
                labelText.text = value ?? baseLabel ?? string.Empty;
        }

        public void SetState(bool isSelected, bool interactable, PhoneOSStyle style)
        {
            ResolveReferences();

            if (button != null)
                button.interactable = interactable;

            if (backgroundImage != null)
            {
                backgroundImage.sprite = style != null ? style.RoundedPanelSprite : null;
                backgroundImage.type = backgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                backgroundImage.raycastTarget = true;
                backgroundImage.color = isSelected
                    ? (style != null ? style.PrimaryTextColor : new Color32(0x24, 0x28, 0x2C, 0xFF))
                    : new Color(1f, 1f, 1f, interactable ? 0.86f : 0.46f);
            }

            if (labelText != null)
            {
                labelText.color = isSelected
                    ? Color.white
                    : (style != null ? style.PrimaryTextColor : new Color32(0x24, 0x28, 0x2C, 0xFF));
                labelText.raycastTarget = false;
                labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
                labelText.verticalOverflow = VerticalWrapMode.Truncate;
            }
        }

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleClicked);
        }

        private void HandleClicked()
        {
            selected?.Invoke(optionId);
        }

        private void ResolveReferences()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
        }
    }
}
