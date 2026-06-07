using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime
{
    internal sealed class MomotalkInfoToggleElements
    {
        public Toggle Toggle;
        public Image Track;
        public RectTransform Knob;
    }

    internal static class MomotalkInfoRowLayout
    {
        public const float RowWidth = 820f;
        public const float RowHeight = 132f;
        public const float IconCircleSize = 82f;
        public const float IconCircleX = 84f;
        public const float RowTextX = 150f;
        public const float ArrowX = -34f;
        public const float ToggleX = -34f;
        public const float ToggleWidth = 112f;
        public const float ToggleHeight = 58f;
        public const float ToggleKnobSize = 46f;

        public static RectTransform CreateRow(RectTransform root, string name, float y)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            row.SetParent(root, false);
            row.anchorMin = new Vector2(0.5f, 1f);
            row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, y);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, RowWidth);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, RowHeight);

            var image = row.GetComponent<Image>();
            MomotalkUIStyle.ApplyRounded(image, Color.white, 18, true);
            MomotalkUIStyle.ApplySoftShadow(image, new Color(0.23f, 0.28f, 0.35f, 0.08f), new Vector2(0f, -4f));
            return row;
        }

        public static RectTransform CreateIconCircle(RectTransform row, Color color)
        {
            var iconCircle = new GameObject("IconCircle", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            iconCircle.SetParent(row, false);
            iconCircle.anchorMin = new Vector2(0f, 0.5f);
            iconCircle.anchorMax = new Vector2(0f, 0.5f);
            iconCircle.pivot = new Vector2(0.5f, 0.5f);
            iconCircle.anchoredPosition = new Vector2(IconCircleX, 0f);
            iconCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, IconCircleSize);
            iconCircle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, IconCircleSize);

            var image = iconCircle.GetComponent<Image>();
            image.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            image.color = color;
            image.raycastTarget = false;
            return iconCircle;
        }

        public static Image CreateIcon(RectTransform iconCircle, string name, Sprite sprite, float size, Color color)
        {
            var iconRect = iconCircle.Find(name) as RectTransform;
            if (iconRect == null)
            {
                iconRect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                iconRect.SetParent(iconCircle, false);
            }

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            iconRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

            var image = iconRect.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static void CreateTitleBlock(RectTransform row, string title, string subtitle, Font font)
        {
            var titleText = CreateText(row, "Title", title, 32, new Color(0.08f, 0.1f, 0.14f, 1f), TextAnchor.MiddleLeft, font);
            var titleRect = titleText.transform as RectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 0.5f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(RowTextX, 24f);
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 560f);
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 50f);

            var subtitleText = CreateText(row, "Subtitle", subtitle, 24, new Color(0.42f, 0.45f, 0.5f, 1f), TextAnchor.MiddleLeft, font);
            var subtitleRect = subtitleText.transform as RectTransform;
            subtitleRect.anchorMin = new Vector2(0f, 0.5f);
            subtitleRect.anchorMax = new Vector2(1f, 0.5f);
            subtitleRect.pivot = new Vector2(0f, 0.5f);
            subtitleRect.anchoredPosition = new Vector2(RowTextX, -24f);
            subtitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 560f);
            subtitleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 44f);
        }

        public static Text CreateArrow(RectTransform row, Font font)
        {
            var arrow = CreateText(row, "Arrow", ">", 42, new Color(0.62f, 0.64f, 0.68f, 1f), TextAnchor.MiddleCenter, font);
            var arrowRect = arrow.transform as RectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0.5f);
            arrowRect.anchorMax = new Vector2(1f, 0.5f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(ArrowX, 0f);
            arrowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 60f);
            arrowRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80f);
            return arrow;
        }

        public static MomotalkInfoToggleElements CreateToggle(RectTransform row)
        {
            var toggleRect = new GameObject("StickyToggle", typeof(RectTransform), typeof(Toggle)).GetComponent<RectTransform>();
            toggleRect.SetParent(row, false);
            toggleRect.anchorMin = new Vector2(1f, 0.5f);
            toggleRect.anchorMax = new Vector2(1f, 0.5f);
            toggleRect.pivot = new Vector2(1f, 0.5f);
            toggleRect.anchoredPosition = new Vector2(ToggleX, 0f);
            toggleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ToggleWidth);
            toggleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ToggleHeight);

            var track = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            track.transform.SetParent(toggleRect, false);
            var trackRect = track.transform as RectTransform;
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = Vector2.zero;
            trackRect.offsetMax = Vector2.zero;
            track.raycastTarget = true;

            var knob = new GameObject("Knob", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            knob.SetParent(toggleRect, false);
            knob.anchorMin = new Vector2(0.5f, 0.5f);
            knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ToggleKnobSize);
            knob.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ToggleKnobSize);
            var knobImage = knob.GetComponent<Image>();
            knobImage.sprite = MomotalkChatMessageView.GetCircleMaskSprite();
            knobImage.color = Color.white;
            knobImage.raycastTarget = false;

            var toggle = toggleRect.GetComponent<Toggle>();
            toggle.targetGraphic = track;
            toggle.graphic = null;
            return new MomotalkInfoToggleElements
            {
                Toggle = toggle,
                Track = track,
                Knob = knob
            };
        }

        public static Button ConfigureButton(RectTransform row)
        {
            var button = row.GetComponent<Button>();
            if (button == null)
                button = row.gameObject.AddComponent<Button>();

            button.targetGraphic = row.GetComponent<Image>();
            if (row.GetComponent<MomotalkUIButtonFeedback>() == null)
                row.gameObject.AddComponent<MomotalkUIButtonFeedback>();
            return button;
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize, Color color, TextAnchor alignment, Font font)
        {
            var existing = parent.Find(name) as RectTransform;
            var textComponent = existing != null
                ? existing.GetComponent<Text>()
                : new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            if (existing == null)
                textComponent.transform.SetParent(parent, false);

            textComponent.text = text;
            MomotalkUIStyle.ApplyText(textComponent, fontSize, color, alignment, font);
            return textComponent;
        }
    }
}
