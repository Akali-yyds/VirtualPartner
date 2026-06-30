using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneShellView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Image phoneShadowImage;
        [SerializeField] private Image phoneFrameImage;
        [SerializeField] private RectTransform phoneScreenMask;

        private void Awake()
        {
            ApplyStyle();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        public void ApplyStyle()
        {
            if (style == null)
                return;

            ApplyShellImage(phoneShadowImage, style.PhoneShadowSprite, style.PhoneShadowTint, true);
            ApplyShellImage(phoneFrameImage, style.PhoneFrameSprite, style.PhoneFrameTint, false);
            ApplyScreenInsets();
        }

        private static void ApplyShellImage(Image image, Sprite sprite, Color tint, bool preserveAspect)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
        }

        private void ApplyScreenInsets()
        {
            if (phoneScreenMask == null)
                return;

            var insets = style.ScreenViewportInsets;
            phoneScreenMask.anchorMin = Vector2.zero;
            phoneScreenMask.anchorMax = Vector2.one;
            phoneScreenMask.pivot = new Vector2(0.5f, 0.5f);
            phoneScreenMask.offsetMin = new Vector2(insets.x, insets.w);
            phoneScreenMask.offsetMax = new Vector2(-insets.z, -insets.y);
            phoneScreenMask.localScale = Vector3.one;
        }
    }
}
