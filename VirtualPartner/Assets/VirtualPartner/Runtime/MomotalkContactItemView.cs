using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    public sealed class MomotalkContactItemView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Text nameText;
        [SerializeField] private Text statusText;

        private CharacterRuntimeContext context;
        private Action<CharacterRuntimeContext> onClicked;

        public void Configure(CharacterRuntimeContext runtimeContext, Action<CharacterRuntimeContext> clickHandler)
        {
            context = runtimeContext;
            onClicked = clickHandler;

            var profile = runtimeContext != null ? runtimeContext.Profile : null;
            if (nameText != null)
                nameText.text = profile != null ? profile.DisplayName : string.Empty;
            if (statusText != null)
                statusText.text = profile != null ? profile.MomotalkStatus : string.Empty;
            if (avatarImage != null)
            {
                avatarImage.sprite = profile != null ? profile.AvatarIcon : null;
                avatarImage.enabled = avatarImage.sprite != null;
            }

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
            if (context != null)
                onClicked?.Invoke(context);
        }
    }
}
