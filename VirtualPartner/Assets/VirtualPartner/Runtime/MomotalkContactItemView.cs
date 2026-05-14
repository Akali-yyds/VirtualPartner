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
        [SerializeField] private Text unreadText;
        [SerializeField] private Image unreadBackground;
        [SerializeField] private Image stickyPinImage;

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
                avatarImage = MomotalkAvatarUtility.EnsureCircularAvatarImage(avatarImage);
                var avatar = profile != null ? profile.AvatarIcon : null;
                MomotalkAvatarUtility.SetAvatar(avatarImage, avatar, avatar != null);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
                button.onClick.AddListener(HandleClick);
            }

            SetConversationState(profile != null ? profile.MomotalkStatus : string.Empty, 0);
        }

        public void SetConversationState(string summary, int unreadCount)
        {
            if (statusText != null)
                statusText.text = string.IsNullOrWhiteSpace(summary) ? "Available" : summary;

            EnsureUnreadBadge();
            var hasUnread = unreadCount > 0;
            if (unreadBackground != null)
                unreadBackground.gameObject.SetActive(hasUnread);
            if (unreadText != null)
            {
                unreadText.gameObject.SetActive(hasUnread);
                unreadText.text = unreadCount > 99 ? "99+" : unreadCount.ToString();
            }
        }

        public void SetStickyState(bool sticky, Sprite pinSprite)
        {
            EnsureStickyPin();
            if (stickyPinImage == null)
                return;

            stickyPinImage.sprite = pinSprite;
            stickyPinImage.gameObject.SetActive(sticky && pinSprite != null);
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

        private void EnsureUnreadBadge()
        {
            if (unreadText != null && unreadBackground != null)
                return;

            var badgeRoot = new GameObject("UnreadBadge", typeof(RectTransform), typeof(Image));
            badgeRoot.transform.SetParent(transform, false);
            var badgeRect = badgeRoot.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 0.5f);
            badgeRect.anchorMax = new Vector2(1f, 0.5f);
            badgeRect.pivot = new Vector2(1f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(-28f, 0f);
            badgeRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 54f);
            badgeRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 42f);

            unreadBackground = badgeRoot.GetComponent<Image>();
            unreadBackground.color = new Color(0.95f, 0.18f, 0.28f, 1f);

            var textObject = new GameObject("UnreadText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(badgeRoot.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            unreadText = textObject.GetComponent<Text>();
            unreadText.alignment = TextAnchor.MiddleCenter;
            unreadText.color = Color.white;
            unreadText.fontSize = 24;
            unreadText.raycastTarget = false;
        }

        private void EnsureStickyPin()
        {
            if (stickyPinImage != null)
                return;

            var pinRoot = new GameObject("StickyPin", typeof(RectTransform), typeof(Image));
            pinRoot.transform.SetParent(transform, false);
            var pinRect = pinRoot.GetComponent<RectTransform>();
            pinRect.anchorMin = new Vector2(1f, 1f);
            pinRect.anchorMax = new Vector2(1f, 1f);
            pinRect.pivot = new Vector2(1f, 1f);
            pinRect.anchoredPosition = new Vector2(-28f, -20f);
            pinRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 38f);
            pinRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 38f);

            stickyPinImage = pinRoot.GetComponent<Image>();
            stickyPinImage.color = new Color(0.12f, 0.72f, 0.48f, 1f);
            stickyPinImage.preserveAspect = true;
            stickyPinImage.raycastTarget = false;
            stickyPinImage.gameObject.SetActive(false);
        }
    }
}
