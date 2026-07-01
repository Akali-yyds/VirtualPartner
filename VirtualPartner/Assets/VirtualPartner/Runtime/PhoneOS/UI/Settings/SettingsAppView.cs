using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class SettingsAppView : MonoBehaviour, IPhoneApp
    {
        [SerializeField] private string appId = "settings";
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private PhoneAppWindowView windowView;
        [SerializeField] private RectTransform contentRoot;

        private const float PagePaddingX = 16f;
        private const float PagePaddingTop = 14f;
        private const float SectionGap = 12f;
        private const float SectionHeight = 88f;
        private const float SectionPaddingX = 12f;
        private const float SectionPaddingTop = 10f;
        private const float SectionTitleHeight = 20f;
        private const float SectionTitleGap = 8f;
        private const float OptionRowHeight = 38f;
        private const float OptionGap = 8f;

        private readonly List<OptionButton> wallpaperButtons = new List<OptionButton>();
        private readonly List<OptionButton> timeButtons = new List<OptionButton>();
        private OptionButton showDockButton;
        private PhoneSettingsService settingsService;
        private PhoneSettingsApplier settingsApplier;
        private bool subscribed;

        public string AppId => appId;

        public void OnOpen(object args = null)
        {
            ResolveDependencies();
            BuildContent();
            Subscribe();

            if (windowView != null)
            {
                windowView.SetTitle("Settings");
                windowView.SetDescription(string.Empty);
            }

            RefreshControls(settingsService != null ? settingsService.Current : new PhoneSettingsData());
        }

        public void OnClose()
        {
            Unsubscribe();
        }

        public void OnPause()
        {
        }

        public void OnResume()
        {
            RefreshControls(settingsService != null ? settingsService.Current : new PhoneSettingsData());
        }

        public bool OnBackPressed()
        {
            return false;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void ResolveDependencies()
        {
            if (windowView == null)
                windowView = GetComponent<PhoneAppWindowView>();
            if (settingsService == null)
                settingsService = GetComponentInParent<PhoneSettingsService>();
            if (settingsApplier == null)
                settingsApplier = GetComponentInParent<PhoneSettingsApplier>();
            if (contentRoot == null)
                contentRoot = transform as RectTransform;
        }

        private void Subscribe()
        {
            if (settingsService == null || subscribed)
                return;

            settingsService.OnSettingsChanged += RefreshControls;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (settingsService != null && subscribed)
                settingsService.OnSettingsChanged -= RefreshControls;

            subscribed = false;
        }

        private void BuildContent()
        {
            if (contentRoot == null)
                return;

            ClearContent();
            ConfigureContentRoot();

            var y = PagePaddingTop;
            y = BuildWallpaperSection(y);
            y = BuildTimeFormatSection(y);
            BuildHomeScreenSection(y);
        }

        private void ClearContent()
        {
            wallpaperButtons.Clear();
            timeButtons.Clear();
            showDockButton = null;

            var contentText = contentRoot.GetComponent<Text>();
            if (contentText != null)
                contentText.enabled = false;

            for (var i = contentRoot.childCount - 1; i >= 0; i--)
                DestroyContentChild(contentRoot.GetChild(i).gameObject);
        }

        private void ConfigureContentRoot()
        {
            var layout = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;

            var fitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;
        }

        private float BuildWallpaperSection(float y)
        {
            RectTransform row;
            CreateSection("Wallpaper", y, out row);

            var options = ResolveWallpaperOptions();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = CreateOptionButton(row, option.DisplayName, i, options.Count);
                button.Id = option.Id;
                var wallpaperId = option.Id;
                button.Button.onClick.AddListener(() => SetWallpaper(wallpaperId));
                wallpaperButtons.Add(button);
            }

            return y + SectionHeight + SectionGap;
        }

        private float BuildTimeFormatSection(float y)
        {
            RectTransform row;
            CreateSection("Time Format", y, out row);

            var hour24 = CreateOptionButton(row, "24-hour", 0, 2);
            hour24.Id = "24";
            hour24.Button.onClick.AddListener(() => SetUse24HourTime(true));
            timeButtons.Add(hour24);

            var hour12 = CreateOptionButton(row, "12-hour", 1, 2);
            hour12.Id = "12";
            hour12.Button.onClick.AddListener(() => SetUse24HourTime(false));
            timeButtons.Add(hour12);

            return y + SectionHeight + SectionGap;
        }

        private void BuildHomeScreenSection(float y)
        {
            RectTransform row;
            CreateSection("Home Screen", y, out row);

            showDockButton = CreateOptionButton(row, "Show Dock", 0, 1);
            showDockButton.Id = "showDock";
            showDockButton.Button.onClick.AddListener(ToggleDock);
        }

        private List<PhoneWallpaperOption> ResolveWallpaperOptions()
        {
            var results = new List<PhoneWallpaperOption>();
            if (settingsApplier != null)
            {
                var options = settingsApplier.WallpaperOptions;
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] != null)
                        results.Add(options[i]);
                }
            }

            if (results.Count == 0)
                results.Add(new PhoneWallpaperOption());

            return results;
        }

        private RectTransform CreateSection(string title, float y, out RectTransform row)
        {
            var section = CreateRect("Section_" + title.Replace(" ", string.Empty), contentRoot);
            SetTopStretch(section, PagePaddingX, y, PagePaddingX, SectionHeight);

            var sectionImage = section.gameObject.AddComponent<Image>();
            sectionImage.sprite = style != null ? style.RoundedPanelSprite : null;
            sectionImage.type = sectionImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            sectionImage.color = style != null ? style.PanelColor : new Color(1f, 1f, 1f, 0.92f);
            sectionImage.raycastTarget = false;

            var titleText = CreateText("Title", section, title, 14, TextAnchor.MiddleLeft);
            titleText.color = style != null ? style.SecondaryTextColor : new Color32(0x66, 0x6A, 0x70, 0xFF);
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopStretch(titleText.rectTransform, SectionPaddingX, SectionPaddingTop, SectionPaddingX, SectionTitleHeight);

            row = CreateRect("Options", section);
            SetTopStretch(
                row,
                SectionPaddingX,
                SectionPaddingTop + SectionTitleHeight + SectionTitleGap,
                SectionPaddingX,
                OptionRowHeight);

            return section;
        }

        private OptionButton CreateOptionButton(RectTransform parent, string label, int index, int count)
        {
            var rect = CreateRect("Option_" + label.Replace(" ", string.Empty), parent);
            SetHorizontalSlot(rect, index, count, OptionGap);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = style != null ? style.RoundedPanelSprite : null;
            image.type = image.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText("Label", rect, label, 13, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            return new OptionButton(button, image, text, label);
        }

        private Text CreateText(string name, RectTransform parent, string value, int size, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = value ?? string.Empty;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = style != null ? style.PrimaryTextColor : new Color32(0x24, 0x28, 0x2C, 0xFF);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private RectTransform CreateRect(string name, RectTransform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            gameObject.layer = parent != null ? parent.gameObject.layer : this.gameObject.layer;

            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void SetTopStretch(RectTransform rect, float left, float top, float right, float height)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void SetHorizontalSlot(RectTransform rect, int index, int count, float gap)
        {
            if (rect == null)
                return;

            var safeCount = Mathf.Max(1, count);
            var safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            var minX = safeIndex / (float)safeCount;
            var maxX = (safeIndex + 1) / (float)safeCount;
            var halfGap = safeCount > 1 ? gap * 0.5f : 0f;

            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(safeIndex == 0 ? 0f : halfGap, 0f);
            rect.offsetMax = new Vector2(safeIndex == safeCount - 1 ? 0f : -halfGap, 0f);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void DestroyContentChild(GameObject child)
        {
            if (child == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(child);
            else
                UnityEngine.Object.DestroyImmediate(child);
        }

        private void SetWallpaper(string wallpaperId)
        {
            settingsService?.SetWallpaper(wallpaperId);
        }

        private void SetUse24HourTime(bool value)
        {
            settingsService?.SetUse24HourTime(value);
        }

        private void ToggleDock()
        {
            if (settingsService == null)
                return;

            var data = settingsService.Current;
            settingsService.SetShowDock(!data.showDock);
        }

        private void RefreshControls(PhoneSettingsData data)
        {
            var settings = data ?? new PhoneSettingsData();
            settings.Normalize();
            var canInteract = settingsService != null;

            for (var i = 0; i < wallpaperButtons.Count; i++)
                SetButtonState(wallpaperButtons[i], string.Equals(wallpaperButtons[i].Id, settings.wallpaperId, StringComparison.Ordinal), canInteract);

            for (var i = 0; i < timeButtons.Count; i++)
                SetButtonState(timeButtons[i], (timeButtons[i].Id == "24") == settings.use24HourTime, canInteract);

            if (showDockButton != null)
            {
                showDockButton.Label.text = settings.showDock ? "Show Dock: On" : "Show Dock: Off";
                SetButtonState(showDockButton, settings.showDock, canInteract);
            }
        }

        private void SetButtonState(OptionButton option, bool selected, bool interactable)
        {
            if (option == null)
                return;

            option.Button.interactable = interactable;
            option.Background.color = selected
                ? (style != null ? style.PrimaryTextColor : new Color32(0x24, 0x28, 0x2C, 0xFF))
                : new Color(1f, 1f, 1f, interactable ? 0.86f : 0.46f);
            option.Label.color = selected
                ? Color.white
                : (style != null ? style.PrimaryTextColor : new Color32(0x24, 0x28, 0x2C, 0xFF));

            if (option.Id != "showDock")
                option.Label.text = option.BaseLabel;
        }

        private sealed class OptionButton
        {
            public OptionButton(Button button, Image background, Text label, string baseLabel)
            {
                Button = button;
                Background = background;
                Label = label;
                BaseLabel = baseLabel;
            }

            public string Id { get; set; }
            public string BaseLabel { get; }
            public Button Button { get; }
            public Image Background { get; }
            public Text Label { get; }
        }
    }
}
