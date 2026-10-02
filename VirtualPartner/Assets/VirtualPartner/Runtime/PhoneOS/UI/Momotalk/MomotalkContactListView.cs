using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class MomotalkContactListView : MonoBehaviour
    {
        private const string TokiContactId = "toki";
        private const string TokiDisplayName = "Toki";
        private const string TokiPreview = "This is the new PhoneOS Momotalk preview.";
        private const string TokiTime = "18:25";
        private const string VisualContactPrefix = "VisualReferenceContact_";

        private static readonly VisualContact[] VisualReferenceContacts =
        {
            new VisualContact("sophia", "Sophia", "I am just an artificial intelligence.", "10:07 PM", 1, new Color32(0xB8, 0x8B, 0x6B, 0xFF)),
            new VisualContact("mask-guy", "Mask Guy", "I am but a man in a mask.", "9:56 PM", 1, new Color32(0x17, 0x17, 0x1B, 0xFF)),
            new VisualContact("siri", "Siri", "[photo] Here you go :)", "9:47 PM", 1, new Color32(0x48, 0x7C, 0xB8, 0xFF)),
            new VisualContact("spider-man", "Spider Man", "Thanks for having my back blue, see you later.", "9:20 PM", 1, new Color32(0xD7, 0x2E, 0x38, 0xFF)),
            new VisualContact("apoc-guy", "Apoc Guy", "Hello Dave, Long time no see !", "10:16 PM", 0, new Color32(0xA9, 0xA3, 0xA7, 0xFF)),
            new VisualContact("sydney", "Sydney", "[photo] Have you ever been here ?", "9:46 PM", 0, new Color32(0xE9, 0x5B, 0x79, 0xFF)),
            new VisualContact("andrew", "Andrew", "Yup, that would do the job. And here is mine too...", "9:29 PM", 0, new Color32(0x0F, 0x59, 0x63, 0xFF)),
            new VisualContact("tony-stark", "Tony Stark", "Okay, i will", "9:27 PM", 0, new Color32(0x50, 0x72, 0x89, 0xFF))
        };

        [SerializeField] private MomotalkTheme theme;
        [SerializeField] private bool visualReferenceContacts = true;
        [SerializeField] private Image pageBackground;
        [SerializeField] private Image appBarBackground;
        [SerializeField] private Image tabBarBackground;
        [SerializeField] private Image peachMarkImage;
        [SerializeField] private Text titleText;
        [SerializeField] private Text searchButtonText;
        [SerializeField] private Text moreButtonText;
        [SerializeField] private Image searchButtonBackground;
        [SerializeField] private Image moreButtonBackground;
        [SerializeField] private Text messagesTabText;
        [SerializeField] private Text statusTabText;
        [SerializeField] private Text callsTabText;
        [SerializeField] private Image activeTabUnderline;
        [SerializeField] private ScrollRect contactScrollRect;
        [SerializeField] private Image floatingActionButtonBackground;
        [SerializeField] private Text floatingActionButtonText;
        [SerializeField] private Button floatingActionButton;
        [SerializeField] private MomotalkContactItemView tokiItem;

        private Action<string, string> contactSelected;
        private readonly List<MomotalkContactItemView> visualContactItems = new List<MomotalkContactItemView>();

        public void Bind(Action<string, string> onContactSelected)
        {
            ResolveReferences();
            contactSelected = onContactSelected;

            ApplyStyle(true);
            RebuildContacts();
        }

        private void Awake()
        {
            ResolveReferences();
            ApplyStyle(true);
        }

        private void OnValidate()
        {
            ResolveReferences();
            ApplyStyle(false);
        }

        private void ResolveReferences()
        {
            if (tokiItem == null)
                tokiItem = GetComponentInChildren<MomotalkContactItemView>(true);
            if (contactScrollRect == null)
                contactScrollRect = GetComponentInChildren<ScrollRect>(true);

            var fab = FindDescendant(transform, "FloatingActionButton");
            if (fab != null)
            {
                if (floatingActionButton == null)
                    floatingActionButton = fab.GetComponent<Button>();
                if (floatingActionButtonBackground == null)
                    floatingActionButtonBackground = fab.GetComponent<Image>();
                if (floatingActionButtonText == null)
                    floatingActionButtonText = fab.GetComponentInChildren<Text>(true);
            }

            ResolveButtonText("SearchButton", ref searchButtonText, ref searchButtonBackground);
            ResolveButtonText("MoreButton", ref moreButtonText, ref moreButtonBackground);
        }

        private void ApplyStyle(bool applyLayout)
        {
            if (pageBackground != null)
            {
                pageBackground.sprite = theme != null ? theme.ContactListBackground : null;
                pageBackground.type = pageBackground.sprite != null ? Image.Type.Simple : Image.Type.Simple;
                pageBackground.color = theme != null ? theme.ContactBackgroundColor : Color.white;
                pageBackground.raycastTarget = false;
            }

            ApplyBarImage(appBarBackground, theme != null ? theme.AppBarDarkColor : (Color)new Color32(0x07, 0x5E, 0x54, 0xFF));
            ApplyBarImage(tabBarBackground, theme != null ? theme.TopBarColor : (Color)new Color32(0x12, 0x8C, 0x7E, 0xFF));

            if (activeTabUnderline != null)
            {
                activeTabUnderline.color = Color.white;
                activeTabUnderline.raycastTarget = false;
            }

            if (peachMarkImage != null)
            {
                peachMarkImage.gameObject.SetActive(false);
            }

            if (titleText != null)
                titleText.text = "WhatsApp";
            if (messagesTabText != null)
                messagesTabText.text = "CHATS";
            if (statusTabText != null)
                statusTabText.text = "STATUS";
            if (callsTabText != null)
                callsTabText.text = "CALLS";

            ApplyText(titleText, Color.white, theme != null ? theme.TitleFontSize : 20, false);
            ApplyText(messagesTabText, Color.white, theme != null ? theme.TabFontSize : 13, false);
            ApplyText(statusTabText, new Color(1f, 1f, 1f, 0.72f), theme != null ? theme.TabFontSize : 13, false);
            ApplyText(callsTabText, new Color(1f, 1f, 1f, 0.72f), theme != null ? theme.TabFontSize : 13, false);
            ApplyHeaderAction(searchButtonText, searchButtonBackground, "O");
            ApplyHeaderAction(moreButtonText, moreButtonBackground, "...");

            if (applyLayout)
                ApplyLayoutTokens();

            ApplyFloatingActionButton(applyLayout);
        }

        private void ApplyBarImage(Image image, Color color)
        {
            if (image == null)
                return;

            image.sprite = theme != null ? theme.TopBarBackground : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
        }

        private void ApplyLayoutTokens()
        {
            var appBarHeight = theme != null ? theme.AppBarHeight : 56f;
            var tabBarHeight = theme != null ? theme.TabBarHeight : 48f;

            SetTopBarRect(appBarBackground != null ? appBarBackground.rectTransform : null, 0f, appBarHeight);
            SetTopBarRect(tabBarBackground != null ? tabBarBackground.rectTransform : null, -appBarHeight, tabBarHeight);

            if (contactScrollRect != null)
                SetStretchOffsets(contactScrollRect.transform as RectTransform, appBarHeight + tabBarHeight, 0f);
        }

        private void RebuildContacts()
        {
            if (tokiItem == null)
                return;

            ClearVisualContactClones();

            if (!visualReferenceContacts)
            {
                tokiItem.Bind(TokiContactId, TokiDisplayName, TokiPreview, TokiTime, 1, contactSelected);
                return;
            }

            var parent = tokiItem.transform.parent;
            for (var i = 0; i < VisualReferenceContacts.Length; i++)
            {
                var contact = VisualReferenceContacts[i];
                var item = i == 0 ? tokiItem : Instantiate(tokiItem, parent);
                if (i > 0)
                {
                    item.name = VisualContactPrefix + contact.Id;
                    visualContactItems.Add(item);
                }

                item.gameObject.SetActive(true);
                item.Bind(contact.Id, contact.Name, contact.Preview, contact.Time, contact.UnreadCount, contact.AvatarColor, contactSelected);
            }

            if (contactScrollRect != null)
                contactScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ClearVisualContactClones()
        {
            for (var i = visualContactItems.Count - 1; i >= 0; i--)
                DestroyContactClone(visualContactItems[i]);
            visualContactItems.Clear();

            if (tokiItem == null || tokiItem.transform.parent == null)
                return;

            var parent = tokiItem.transform.parent;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null || !child.name.StartsWith(VisualContactPrefix, StringComparison.Ordinal))
                    continue;

                DestroyContactClone(child.GetComponent<MomotalkContactItemView>());
            }
        }

        private static void DestroyContactClone(MomotalkContactItemView item)
        {
            if (item == null)
                return;

            var go = item.gameObject;
            go.SetActive(false);
            go.transform.SetParent(null, false);
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        private void ApplyFloatingActionButton(bool applyLayout)
        {
            var size = theme != null ? theme.FloatingActionButtonSize : 56f;
            var edgeInset = 20f;

            if (floatingActionButtonBackground != null)
            {
                floatingActionButtonBackground.sprite = theme != null ? theme.FabBackgroundSprite : null;
                floatingActionButtonBackground.type = floatingActionButtonBackground.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                floatingActionButtonBackground.color = theme != null ? theme.AccentColor : new Color32(0x00, 0xA8, 0x84, 0xFF);
                floatingActionButtonBackground.raycastTarget = true;
                if (applyLayout)
                {
                    var rect = floatingActionButtonBackground.rectTransform;
                    rect.sizeDelta = new Vector2(size, size);
                    rect.anchoredPosition = new Vector2(-(edgeInset + size * 0.5f), edgeInset + size * 0.5f);
                }
            }

            if (floatingActionButton != null && floatingActionButtonBackground != null)
            {
                floatingActionButton.targetGraphic = floatingActionButtonBackground;
                var colors = floatingActionButton.colors;
                colors.highlightedColor = new Color32(0x1E, 0xB9, 0x95, 0xFF);
                colors.pressedColor = new Color32(0x00, 0x8F, 0x72, 0xFF);
                colors.selectedColor = colors.highlightedColor;
                floatingActionButton.colors = colors;
            }

            if (floatingActionButtonText != null)
            {
                floatingActionButtonText.text = "+";
                floatingActionButtonText.color = Color.white;
                floatingActionButtonText.fontSize = 28;
                floatingActionButtonText.raycastTarget = false;
            }
        }

        private static void SetTopBarRect(RectTransform rect, float y, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, height);
        }

        private static void SetStretchOffsets(RectTransform rect, float top, float bottom)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(0f, bottom);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private void ResolveButtonText(string objectName, ref Text text, ref Image background)
        {
            var buttonTransform = FindDescendant(transform, objectName);
            if (buttonTransform == null)
                return;

            if (text == null)
                text = buttonTransform.GetComponentInChildren<Text>(true);
            if (background == null)
                background = buttonTransform.GetComponent<Image>();
        }

        private static void ApplyHeaderAction(Text text, Image background, string label)
        {
            if (background != null)
            {
                var color = Color.white;
                color.a = 0f;
                background.color = color;
                background.raycastTarget = true;
            }

            if (text == null)
                return;

            text.text = label;
            text.color = Color.white;
            text.fontSize = label.Length > 1 ? 18 : 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private static Transform FindDescendant(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
                return null;

            if (root.name == childName)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var match = FindDescendant(root.GetChild(i), childName);
                if (match != null)
                    return match;
            }

            return null;
        }

        private static void ApplyText(Text text, Color color, int fontSize, bool wrap)
        {
            if (text == null)
                return;

            text.color = color;
            text.fontSize = fontSize;
            text.raycastTarget = false;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private readonly struct VisualContact
        {
            public VisualContact(string id, string name, string preview, string time, int unreadCount, Color avatarColor)
            {
                Id = id;
                Name = name;
                Preview = preview;
                Time = time;
                UnreadCount = unreadCount;
                AvatarColor = avatarColor;
            }

            public string Id { get; }
            public string Name { get; }
            public string Preview { get; }
            public string Time { get; }
            public int UnreadCount { get; }
            public Color AvatarColor { get; }
        }
    }
}
