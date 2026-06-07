using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    internal enum MomotalkTopBarActionSlot
    {
        Left,
        Right
    }

    internal static class MomotalkTopBarLayout
    {
        public const float Height = 120f;
        public const float ActionCenterInset = 78f;
        public const float ActionButtonSize = 78f;
        public const float BackIconSize = 50f;
        public const float DetailIconSize = 60f;
        public const float BrandIconSize = 48f;
        public const float CenterTitleSidePadding = 150f;
        public const float LeadingTitleLeft = 120f;
        public const int CenterTitleFontSize = 38;
        public const int LeadingTitleFontSize = 48;

        public static void ApplyRoot(RectTransform topBar)
        {
            if (topBar == null)
                return;

            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.offsetMin = new Vector2(0f, -Height);
            topBar.offsetMax = Vector2.zero;
            MomotalkUIStyle.ApplySolid(topBar.GetComponent<Image>(), MomotalkUIStyle.TopBarPink, false);
        }

        public static Button ConfigureAction(
            RectTransform topBar,
            Button existingButton,
            string buttonName,
            string iconName,
            string iconFileName,
            MomotalkTopBarActionSlot slot,
            float iconSize)
        {
            if (topBar == null)
                return existingButton;

            var buttonRect = existingButton != null && existingButton.transform.IsChildOf(topBar)
                ? existingButton.transform as RectTransform
                : topBar.Find(buttonName) as RectTransform;
            if (buttonRect == null)
            {
                buttonRect = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(MomotalkUIButtonFeedback)).GetComponent<RectTransform>();
                buttonRect.SetParent(topBar, false);
            }

            buttonRect.anchorMin = new Vector2(slot == MomotalkTopBarActionSlot.Left ? 0f : 1f, 0.5f);
            buttonRect.anchorMax = buttonRect.anchorMin;
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(slot == MomotalkTopBarActionSlot.Left ? ActionCenterInset : -ActionCenterInset, 0f);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ActionButtonSize);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ActionButtonSize);

            var hitImage = buttonRect.GetComponent<Image>();
            if (hitImage == null)
                hitImage = buttonRect.gameObject.AddComponent<Image>();
            MomotalkUIStyle.ApplySolid(hitImage, new Color(1f, 1f, 1f, 0f), true);

            var button = buttonRect.GetComponent<Button>();
            if (button == null)
                button = buttonRect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitImage;

            var textChildren = buttonRect.GetComponentsInChildren<Text>(true);
            for (var i = 0; i < textChildren.Length; i++)
                textChildren[i].gameObject.SetActive(false);

            var iconRect = buttonRect.Find(iconName) as RectTransform;
            if (iconRect == null)
            {
                iconRect = new GameObject(iconName, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                iconRect.SetParent(buttonRect, false);
            }

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, iconSize);
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, iconSize);

            var icon = iconRect.GetComponent<Image>();
            icon.sprite = MomotalkUIStyle.Icon(iconFileName);
            icon.color = Color.white;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
        }

        public static Text ConfigureCenterTitle(RectTransform topBar, Text existingTitle, string titleName, string title, Font font)
        {
            var text = ResolveTitle(topBar, existingTitle, titleName);
            if (text == null)
                return null;

            if (title != null)
                text.text = title;
            MomotalkUIStyle.ApplyText(text, CenterTitleFontSize, Color.white, TextAnchor.MiddleCenter, font);

            var rect = text.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(CenterTitleSidePadding, 0f);
            rect.offsetMax = new Vector2(-CenterTitleSidePadding, 0f);
            return text;
        }

        public static Text ConfigureLeadingTitle(RectTransform topBar, Text existingTitle, string titleName, string title, Font font)
        {
            var text = ResolveTitle(topBar, existingTitle, titleName);
            if (text == null)
                return null;

            if (title != null)
                text.text = title;
            MomotalkUIStyle.ApplyText(text, LeadingTitleFontSize, Color.white, TextAnchor.MiddleLeft, font);

            var rect = text.transform as RectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(LeadingTitleLeft, 0f);
            rect.offsetMax = new Vector2(-CenterTitleSidePadding, 0f);
            return text;
        }

        public static void ConfigureLeadingVisual(RectTransform topBar, string visualName)
        {
            if (topBar == null)
                return;

            var visualRect = topBar.Find(visualName) as RectTransform;
            if (visualRect == null)
                return;

            visualRect.anchorMin = new Vector2(0f, 0.5f);
            visualRect.anchorMax = visualRect.anchorMin;
            visualRect.pivot = new Vector2(0.5f, 0.5f);
            visualRect.anchoredPosition = new Vector2(ActionCenterInset, 0f);
            visualRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BrandIconSize);
            visualRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BrandIconSize);
        }

        private static Text ResolveTitle(RectTransform topBar, Text existingTitle, string titleName)
        {
            if (topBar == null)
                return null;

            if (existingTitle != null && existingTitle.transform.IsChildOf(topBar))
                return existingTitle;

            var titleRect = topBar.Find(titleName) as RectTransform;
            if (titleRect == null)
            {
                titleRect = new GameObject(titleName, typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
                titleRect.SetParent(topBar, false);
            }

            var text = titleRect.GetComponent<Text>();
            if (text == null)
                text = titleRect.gameObject.AddComponent<Text>();
            return text;
        }
    }
}
