using System;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    internal sealed class MomotalkInputBarElements
    {
        public RectTransform Bar;
        public RectTransform InputRoot;
        public Image InputBackground;
        public InputField InputField;
        public Text InputText;
        public Text Placeholder;
        public Button MicButton;
        public Graphic MicGraphic;
        public Button SendButton;
        public Graphic SendGraphic;
    }

    internal static class MomotalkInputBarLayout
    {
        public const float Height = 108f;

        private const float MicX = 55f;
        private const float ImageX = -118f;
        private const float SendX = -48f;
        private const float FieldLeft = 100f;
        private const float FieldRight = 174f;
        private const float FieldHeight = 60f;
        private const float MicIconSize = 54f;
        private const float ActionIconSize = 58f;
        private const float TextPaddingX = 24f;

        public static RectTransform EnsureBar(RectTransform chatRoot)
        {
            if (chatRoot == null)
                return null;

            var inputBar = chatRoot.Find("InputBar") as RectTransform;
            if (inputBar == null)
            {
                inputBar = new GameObject("InputBar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                inputBar.SetParent(chatRoot, false);
            }

            return inputBar;
        }

        public static MomotalkInputBarElements Apply(RectTransform inputBar, Font font)
        {
            if (inputBar == null)
                return null;

            ApplyRoot(inputBar);
            RemoveUnexpectedChildren(inputBar);

            var micRect = EnsureChild(inputBar, "MicIcon", typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback));
            ApplyIcon(micRect, MomotalkUIStyle.Icon("mic_icon.png"), MicIconSize, new Vector2(MicX, 0f), true);

            var inputRoot = EnsureChild(inputBar, "DisabledInputField", typeof(Image), typeof(InputField));
            ApplyInputFieldRoot(inputRoot);

            var imageRect = EnsureChild(inputBar, "ImageIcon", typeof(Image));
            ApplyIcon(imageRect, MomotalkUIStyle.Icon("image_icon.png"), ActionIconSize, new Vector2(ImageX, 0f), false);

            var sendRect = EnsureChild(inputBar, "SendIcon", typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback));
            ApplyIcon(sendRect, MomotalkUIStyle.Icon("send_icon.png"), ActionIconSize, new Vector2(SendX, 0f), true);

            var inputText = EnsureText(inputRoot, "InputText");
            ApplyFieldText(inputText, MomotalkUIStyle.TextPrimary, font);

            var placeholder = EnsureText(inputRoot, "Placeholder");
            placeholder.text = "Aa";
            ApplyFieldText(placeholder, new Color(0.56f, 0.60f, 0.66f, 0.72f), font);

            return new MomotalkInputBarElements
            {
                Bar = inputBar,
                InputRoot = inputRoot,
                InputBackground = inputRoot.GetComponent<Image>(),
                InputField = inputRoot.GetComponent<InputField>(),
                InputText = inputText,
                Placeholder = placeholder,
                MicButton = micRect.GetComponent<Button>(),
                MicGraphic = micRect.GetComponent<Graphic>(),
                SendButton = sendRect.GetComponent<Button>(),
                SendGraphic = sendRect.GetComponent<Graphic>()
            };
        }

        private static void ApplyRoot(RectTransform inputBar)
        {
            inputBar.anchorMin = new Vector2(0f, 0f);
            inputBar.anchorMax = new Vector2(1f, 0f);
            inputBar.pivot = new Vector2(0.5f, 0f);
            inputBar.offsetMin = Vector2.zero;
            inputBar.offsetMax = new Vector2(0f, Height);
            inputBar.SetAsLastSibling();

            var barImage = inputBar.GetComponent<Image>();
            if (barImage == null)
                barImage = inputBar.gameObject.AddComponent<Image>();
            MomotalkUIStyle.ApplySolid(barImage, Color.white, true);
        }

        private static void ApplyInputFieldRoot(RectTransform inputRoot)
        {
            inputRoot.anchorMin = new Vector2(0f, 0.5f);
            inputRoot.anchorMax = new Vector2(1f, 0.5f);
            inputRoot.pivot = new Vector2(0.5f, 0.5f);
            inputRoot.anchoredPosition = Vector2.zero;
            inputRoot.offsetMin = new Vector2(FieldLeft, -FieldHeight * 0.5f);
            inputRoot.offsetMax = new Vector2(-FieldRight, FieldHeight * 0.5f);

            var inputGraphic = inputRoot.GetComponent<Image>();
            MomotalkUIStyle.ApplyRounded(inputGraphic, MomotalkUIStyle.InputFieldBackground, 30, true);
            MomotalkUIStyle.ApplySoftOutline(inputGraphic, MomotalkUIStyle.Border, 1f);
        }

        private static Text EnsureText(RectTransform parent, string name)
        {
            var textRect = parent.Find(name) as RectTransform;
            if (textRect == null)
            {
                textRect = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                textRect.SetParent(parent, false);
            }

            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(TextPaddingX, 0f);
            textRect.offsetMax = new Vector2(-TextPaddingX, 0f);
            return textRect.GetComponent<Text>();
        }

        private static void ApplyFieldText(Text text, Color color, Font font)
        {
            MomotalkUIStyle.ApplyText(text, 25, color, TextAnchor.MiddleLeft, font);
            if (text == null)
                return;

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static RectTransform EnsureChild(RectTransform parent, string childName, params Type[] componentTypes)
        {
            var child = parent.Find(childName) as RectTransform;
            if (child == null)
            {
                var types = new Type[componentTypes.Length + 1];
                types[0] = typeof(RectTransform);
                for (var i = 0; i < componentTypes.Length; i++)
                    types[i + 1] = componentTypes[i];
                child = new GameObject(childName, types).GetComponent<RectTransform>();
                child.SetParent(parent, false);
            }
            else
            {
                for (var i = 0; i < componentTypes.Length; i++)
                {
                    if (child.GetComponent(componentTypes[i]) == null)
                        child.gameObject.AddComponent(componentTypes[i]);
                }
            }

            return child;
        }

        private static void ApplyIcon(RectTransform rect, Sprite sprite, float size, Vector2 anchoredPosition, bool raycastTarget)
        {
            rect.anchorMin = new Vector2(anchoredPosition.x < 0f ? 1f : 0f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

            var image = rect.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = raycastTarget;
            }

            var button = rect.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = image;
            }
        }

        private static void RemoveUnexpectedChildren(RectTransform inputBar)
        {
            for (var i = inputBar.childCount - 1; i >= 0; i--)
            {
                var child = inputBar.GetChild(i);
                if (child.name == "MicIcon"
                    || child.name == "DisabledInputField"
                    || child.name == "ImageIcon"
                    || child.name == "SendIcon")
                    continue;

                DestroyUiObject(child.gameObject);
            }
        }

        private static void DestroyUiObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(gameObject);
            else
                UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }
}
