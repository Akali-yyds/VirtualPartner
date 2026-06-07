using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    internal static class MomotalkUIStyle
    {
        private const string TextureRoot = "VirtualPartner/UI/Momotalk/Textures/";
        private const string IconRoot = "VirtualPartner/UI/Momotalk/Icons/";

        private static readonly Dictionary<string, Sprite> FileSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> RoundedSprites = new Dictionary<string, Sprite>();

        public static readonly Color TopBarPink = new Color32(0xFA, 0x97, 0xAD, 0xFF);
        public static readonly Color LoadingPink = new Color32(0xFA, 0x97, 0xAD, 0xFF);
        public static readonly Color TextPrimary = new Color32(0x2A, 0x32, 0x3E, 0xFF);
        public static readonly Color TextSecondary = new Color32(0x87, 0x92, 0x9E, 0xFF);
        public static readonly Color Border = new Color32(0xD8, 0xDD, 0xE5, 0xFF);
        public static readonly Color IconMuted = new Color32(0x9D, 0xA5, 0xAF, 0xFF);
        public static readonly Color IconDisabled = new Color32(0xC2, 0xC7, 0xCE, 0xFF);
        public static readonly Color ContactSelected = new Color32(0xE1, 0xE7, 0xEC, 0xFF);
        public static readonly Color InputBackground = new Color32(0xF4, 0xF5, 0xF7, 0xFF);
        public static readonly Color InputFieldBackground = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color CharacterBubble = new Color32(0x4B, 0x5A, 0x6F, 0xFF);
        public static readonly Color UserBubble = new Color32(0x4A, 0x8A, 0xC6, 0xFF);
        public static readonly Color SystemBubble = new Color32(0xF1, 0xF4, 0xF7, 0xFF);
        public static readonly Color Danger = new Color32(0xF5, 0x51, 0x61, 0xFF);
        public static readonly Color Success = new Color32(0x26, 0xB8, 0x78, 0xFF);

        public static Sprite Texture(string fileName, float border = 0f)
        {
            return LoadSprite(TextureRoot + fileName, border);
        }

        public static Sprite Icon(string fileName)
        {
            return LoadSprite(IconRoot + fileName, 0f);
        }

        public static Sprite Rounded(Color color, int radius, int size = 64)
        {
            var key = ColorUtility.ToHtmlStringRGBA(color) + ":" + radius + ":" + size;
            if (RoundedSprites.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var clear = new Color(color.r, color.g, color.b, 0f);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x < radius ? radius - x : (x >= size - radius ? x - (size - radius - 1) : 0);
                    var dy = y < radius ? radius - y : (y >= size - radius ? y - (size - radius - 1) : 0);
                    var inCorner = dx * dx + dy * dy <= radius * radius;
                    texture.SetPixel(x, y, inCorner ? color : clear);
                }
            }

            texture.Apply();
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "MomotalkRounded_" + key;
            RoundedSprites[key] = sprite;
            return sprite;
        }

        public static void ApplySliced(Image image, Sprite sprite, Color color, bool raycastTarget)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.preserveAspect = false;
            image.raycastTarget = raycastTarget;
        }

        public static void ApplySolid(Image image, Color color, bool raycastTarget)
        {
            if (image == null)
                return;

            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.preserveAspect = false;
            image.raycastTarget = raycastTarget;
        }

        public static void ApplyRounded(Image image, Color color, int radius, bool raycastTarget)
        {
            ApplySliced(image, Rounded(Color.white, radius), color, raycastTarget);
        }

        public static void ApplyText(Text text, int fontSize, Color color, TextAnchor alignment, Font font = null)
        {
            if (text == null)
                return;

            if (font != null)
                text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
        }

        public static void ApplySoftOutline(Graphic graphic, Color color, float distance)
        {
            if (graphic == null)
                return;

            var outline = graphic.GetComponent<Outline>();
            if (outline == null)
                outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = false;
        }

        public static void ApplySoftShadow(Graphic graphic, Color color, Vector2 distance)
        {
            if (graphic == null)
                return;

            var shadow = graphic.GetComponent<Shadow>();
            if (shadow == null)
                shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = false;
        }

        public static Graphic StyleChildIcon(Transform parent, string childName, Vector2 size, Color color)
        {
            if (parent == null || string.IsNullOrWhiteSpace(childName))
                return null;

            var child = parent.Find(childName) as RectTransform;
            if (child == null)
                return null;

            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

            var graphic = child.GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = color;
                graphic.raycastTarget = true;
            }

            var image = graphic as Image;
            if (image != null)
            {
                image.preserveAspect = true;
                image.type = Image.Type.Simple;
            }

            return graphic;
        }

        public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        private static Sprite LoadSprite(string relativeAssetPath, float border)
        {
            var key = relativeAssetPath + ":" + border.ToString("F1");
            if (FileSprites.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;

            var path = Path.Combine(Application.dataPath, relativeAssetPath);
            if (!File.Exists(path))
                return null;

            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    return null;

                texture.wrapMode = TextureWrapMode.Clamp;
                var vectorBorder = border > 0f ? new Vector4(border, border, border, border) : Vector4.zero;
                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect,
                    vectorBorder);
                sprite.name = Path.GetFileNameWithoutExtension(relativeAssetPath);
                FileSprites[key] = sprite;
                return sprite;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[VirtualPartner] Momotalk sprite load failed ({relativeAssetPath}): {exception.Message}");
                return null;
            }
        }

    }
}
