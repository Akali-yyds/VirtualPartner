using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    public sealed class MomotalkChatMessageView : MonoBehaviour
    {
        private const float BubbleHorizontalPadding = 40f;
        private const float BubbleVerticalPadding = 40f;
        private const float MessageBubbleMinHeight = 84f;
        private const float SystemBubbleMinHeight = 84f;
        private const float TypingBubbleMinHeight = 76f;

        [SerializeField] private Image avatarImage;
        [SerializeField] private Image bubbleImage;
        [SerializeField] private Text messageText;
        [SerializeField] private Image[] typingDots;
        [SerializeField] private bool typing;
        [SerializeField] private int requestId;

        private static Sprite userBubbleSprite;
        private static Sprite characterBubbleSprite;
        private static Sprite systemBubbleSprite;
        private static Sprite typingBubbleSprite;
        private static Sprite circleMaskSprite;

        public int RequestId => requestId;
        public bool IsTyping => typing;

        private void Update()
        {
            if (!typing || typingDots == null)
                return;

            for (var i = 0; i < typingDots.Length; i++)
            {
                if (typingDots[i] == null)
                    continue;

                var phase = Time.unscaledTime * 5f - i * 0.65f;
                var alpha = 0.45f + Mathf.Abs(Mathf.Sin(phase)) * 0.45f;
                var color = typingDots[i].color;
                color.a = alpha;
                typingDots[i].color = color;
            }
        }

        public void Bind(MomotalkChatMessageRecord record, Sprite avatarIcon)
        {
            if (record == null)
                return;

            requestId = record.requestId;
            typing = false;
            SetTypingDotsVisible(false);

            if (messageText != null)
            {
                messageText.gameObject.SetActive(true);
                messageText.text = record.text ?? string.Empty;
                messageText.color = GetTextColor(record.sender);
            }

            if (bubbleImage != null)
            {
                if (record.sender == "user")
                    bubbleImage.sprite = GetUserBubbleSprite();
                else if (record.sender == "system")
                    bubbleImage.sprite = GetSystemBubbleSprite();
                else
                    bubbleImage.sprite = GetCharacterBubbleSprite();

                var layout = bubbleImage.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = record.sender == "system"
                        ? 650f
                        : Mathf.Clamp((record.text != null ? record.text.Length : 0) * 22f + 132f, 210f, 620f);
                    layout.minHeight = record.sender == "system"
                        ? SystemBubbleMinHeight
                        : MessageBubbleMinHeight;
                    ApplyTextDrivenHeight(layout, record.text);
                }

                var fitter = bubbleImage.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (avatarImage != null)
            {
                var showAvatar = record.sender == "character" && avatarIcon != null;
                MomotalkAvatarUtility.SetAvatar(avatarImage, avatarIcon, showAvatar);
            }
        }

        public void BindTyping(int typingRequestId, Sprite avatarIcon)
        {
            requestId = typingRequestId;
            typing = true;
            if (messageText != null)
            {
                messageText.text = string.Empty;
                messageText.gameObject.SetActive(false);
            }

            if (bubbleImage != null)
            {
                bubbleImage.sprite = GetTypingBubbleSprite();
                var layout = bubbleImage.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = 136f;
                    layout.minHeight = TypingBubbleMinHeight;
                    layout.preferredHeight = TypingBubbleMinHeight;
                }

                var fitter = bubbleImage.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                    fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            if (avatarImage != null)
                MomotalkAvatarUtility.SetAvatar(avatarImage, avatarIcon, avatarIcon != null);

            SetTypingDotsVisible(true);
        }

        public void Initialize(Image avatar, Image bubble, Text text, Image[] dots)
        {
            avatarImage = avatar;
            bubbleImage = bubble;
            messageText = text;
            typingDots = dots;
        }

        private void SetTypingDotsVisible(bool visible)
        {
            if (typingDots == null)
                return;

            for (var i = 0; i < typingDots.Length; i++)
            {
                if (typingDots[i] != null)
                    typingDots[i].gameObject.SetActive(visible);
            }
        }

        private void ApplyTextDrivenHeight(LayoutElement bubbleLayout, string text)
        {
            if (bubbleLayout == null || messageText == null)
                return;

            var textWidth = Mathf.Max(32f, bubbleLayout.preferredWidth - BubbleHorizontalPadding);
            var settings = messageText.GetGenerationSettings(new Vector2(textWidth, 0f));
            var preferredTextHeight = messageText.cachedTextGeneratorForLayout.GetPreferredHeight(text ?? string.Empty, settings) / messageText.pixelsPerUnit;
            var preferredBubbleHeight = Mathf.Max(bubbleLayout.minHeight, Mathf.Ceil(preferredTextHeight + BubbleVerticalPadding));
            bubbleLayout.preferredHeight = preferredBubbleHeight;

            var rowLayout = GetComponent<LayoutElement>();
            if (rowLayout == null)
                return;

            var avatarHeight = GetAvatarPreferredHeight();
            var rowHeight = Mathf.Max(rowLayout.minHeight, preferredBubbleHeight, avatarHeight);
            rowLayout.minHeight = rowHeight;
            rowLayout.preferredHeight = rowHeight;
        }

        private float GetAvatarPreferredHeight()
        {
            if (avatarImage == null)
                return 0f;

            var root = avatarImage.name == "AvatarImage" && avatarImage.transform.parent != null
                ? avatarImage.transform.parent
                : avatarImage.transform;
            var layout = root.GetComponent<LayoutElement>();
            return layout != null ? layout.preferredHeight : 0f;
        }

        private static Sprite GetUserBubbleSprite()
        {
            return userBubbleSprite != null
                ? userBubbleSprite
                : userBubbleSprite = MomotalkUIStyle.Rounded(MomotalkUIStyle.UserBubble, 18);
        }

        private static Sprite GetCharacterBubbleSprite()
        {
            return characterBubbleSprite != null
                ? characterBubbleSprite
                : characterBubbleSprite = MomotalkUIStyle.Rounded(MomotalkUIStyle.CharacterBubble, 18);
        }

        private static Sprite GetSystemBubbleSprite()
        {
            return systemBubbleSprite != null
                ? systemBubbleSprite
                : systemBubbleSprite = MomotalkUIStyle.Rounded(MomotalkUIStyle.SystemBubble, 14);
        }

        private static Sprite GetTypingBubbleSprite()
        {
            return typingBubbleSprite != null
                ? typingBubbleSprite
                : typingBubbleSprite = MomotalkUIStyle.Rounded(MomotalkUIStyle.CharacterBubble, 18);
        }

        private static Color GetTextColor(string sender)
        {
            if (sender == "system")
                return MomotalkUIStyle.TextSecondary;

            return Color.white;
        }

        internal static Sprite GetCircleMaskSprite()
        {
            if (circleMaskSprite != null)
                return circleMaskSprite;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var center = (size - 1) * 0.5f;
            var radius = center - 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = Mathf.Clamp01(radius + 1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            circleMaskSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            circleMaskSprite.name = "MomotalkCircleMask";
            return circleMaskSprite;
        }
    }

    internal static class MomotalkAvatarUtility
    {
        public static Image EnsureCircularAvatarImage(Image image)
        {
            if (image == null)
                return null;

            if (image.name == "AvatarImage" && image.transform.parent != null && image.transform.parent.GetComponent<Mask>() != null)
                return image;

            var maskImage = image;
            maskImage.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            maskImage.type = Image.Type.Simple;
            maskImage.color = Color.white;
            maskImage.preserveAspect = false;
            maskImage.raycastTarget = false;

            var mask = maskImage.GetComponent<Mask>();
            if (mask == null)
                mask = maskImage.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var child = maskImage.rectTransform.Find("AvatarImage") as RectTransform;
            if (child == null)
            {
                child = new GameObject("AvatarImage", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                child.SetParent(maskImage.transform, false);
            }

            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;

            var avatarImage = child.GetComponent<Image>();
            avatarImage.type = Image.Type.Simple;
            avatarImage.preserveAspect = false;
            avatarImage.raycastTarget = false;
            return avatarImage;
        }

        public static void SetAvatar(Image avatarImage, Sprite avatarIcon, bool visible)
        {
            if (avatarImage == null)
                return;

            var shouldShow = visible && avatarIcon != null;
            avatarImage.sprite = shouldShow ? avatarIcon : null;
            avatarImage.enabled = shouldShow;
            ApplyCoverCrop(avatarImage, avatarIcon);

            var root = GetAvatarRoot(avatarImage);
            if (root != null)
                root.SetActive(shouldShow);
        }

        private static void ApplyCoverCrop(Image avatarImage, Sprite avatarIcon)
        {
            if (avatarImage == null)
                return;

            var rect = avatarImage.rectTransform;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (avatarIcon == null || avatarIcon.rect.height <= 0f)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                return;
            }

            var parent = rect.parent as RectTransform;
            var parentSize = parent != null ? parent.rect.size : Vector2.one;
            var parentAspect = parentSize.x > 0f && parentSize.y > 0f ? parentSize.x / parentSize.y : 1f;
            var spriteAspect = avatarIcon.rect.width / avatarIcon.rect.height;

            if (spriteAspect > parentAspect)
            {
                var scale = spriteAspect / parentAspect;
                var extra = (scale - 1f) * 0.5f;
                rect.anchorMin = new Vector2(-extra, 0f);
                rect.anchorMax = new Vector2(1f + extra, 1f);
                return;
            }

            var heightScale = parentAspect / spriteAspect;
            var heightExtra = (heightScale - 1f) * 0.5f;
            rect.anchorMin = new Vector2(0f, -heightExtra);
            rect.anchorMax = new Vector2(1f, 1f + heightExtra);
        }

        private static GameObject GetAvatarRoot(Image avatarImage)
        {
            if (avatarImage == null)
                return null;

            if (avatarImage.name == "AvatarImage"
                && avatarImage.transform.parent != null
                && avatarImage.transform.parent.GetComponent<Mask>() != null)
                return avatarImage.transform.parent.gameObject;

            return avatarImage.gameObject;
        }
    }
}
