using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    internal sealed class MomotalkContactSearchElements
    {
        public InputField InputField;
        public Text InputText;
        public Text Placeholder;
        public Image Background;
    }

    internal static class MomotalkContactListLayout
    {
        public const float SearchY = -190f;
        public const float ListRootY = -290f;
        public const float ContactItemHeight = 184f;

        private const float SearchTextLeft = 96f;
        private const float SearchTextRight = 32f;

        public static void ApplyHeader(CanvasGroup contactListView, Font font)
        {
            var header = FindChildRecursive(contactListView != null ? contactListView.transform : null, "Header") as RectTransform;
            if (header == null)
                return;

            MomotalkTopBarLayout.ApplyRoot(header);
            MomotalkTopBarLayout.ConfigureLeadingVisual(header, "PeachMark");
            MomotalkTopBarLayout.ConfigureLeadingTitle(header, null, "Title", null, font);
        }

        public static MomotalkContactSearchElements ApplySearch(CanvasGroup contactListView, Font font)
        {
            var searchBar = FindChildRecursive(contactListView != null ? contactListView.transform : null, "SearchBar") as RectTransform;
            if (searchBar == null)
                return null;

            searchBar.anchoredPosition = new Vector2(0f, SearchY);

            var inputField = searchBar.GetComponent<InputField>();
            if (inputField == null)
                inputField = searchBar.gameObject.AddComponent<InputField>();

            var placeholder = EnsureText(searchBar, "SearchPlaceholder");
            placeholder.text = "Type / to search";
            ApplySearchText(placeholder, new Color(0.55f, 0.57f, 0.64f, 1f), font);

            var inputText = EnsureText(searchBar, "SearchInputText");
            ApplySearchText(inputText, MomotalkUIStyle.TextPrimary, font);

            var background = searchBar.GetComponent<Image>();
            if (background != null)
            {
                MomotalkUIStyle.ApplyRounded(background, Color.white, 28, true);
                MomotalkUIStyle.ApplySoftOutline(background, MomotalkUIStyle.Border, 2f);
                background.raycastTarget = true;
            }

            inputField.textComponent = inputText;
            inputField.placeholder = placeholder;
            inputField.contentType = InputField.ContentType.Standard;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.interactable = true;
            inputField.targetGraphic = background;

            return new MomotalkContactSearchElements
            {
                InputField = inputField,
                InputText = inputText,
                Placeholder = placeholder,
                Background = background
            };
        }

        public static void ApplyListRoot(CanvasGroup contactListView)
        {
            var listRoot = FindChildRecursive(contactListView != null ? contactListView.transform : null, "ContactListRoot") as RectTransform;
            if (listRoot != null)
                listRoot.anchoredPosition = new Vector2(0f, ListRootY);
        }

        public static void ApplyContactItemTemplate(MomotalkContactItemView template)
        {
            if (template == null)
                return;

            var templateRect = template.transform as RectTransform;
            if (templateRect != null)
                templateRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ContactItemHeight);

            var itemImage = template.GetComponent<Image>();
            MomotalkUIStyle.ApplySliced(itemImage, MomotalkUIStyle.Texture("contact_item_bg.png", 18f), Color.white, true);
            if (itemImage != null)
                MomotalkUIStyle.ApplySoftShadow(itemImage, new Color(0.22f, 0.27f, 0.34f, 0.06f), new Vector2(0f, -3f));
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
            textRect.offsetMin = new Vector2(SearchTextLeft, 0f);
            textRect.offsetMax = new Vector2(-SearchTextRight, 0f);
            return textRect.GetComponent<Text>();
        }

        private static void ApplySearchText(Text text, Color color, Font font)
        {
            MomotalkUIStyle.ApplyText(text, 28, color, TextAnchor.MiddleLeft, font);
            if (text == null)
                return;

            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == childName)
                    return child;

                var match = FindChildRecursive(child, childName);
                if (match != null)
                    return match;
            }

            return null;
        }
    }
}
