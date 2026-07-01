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
        [SerializeField] private PhoneWallpaperCatalog wallpaperCatalog;
        [SerializeField] private PhoneAppWindowView windowView;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private SettingsSectionView sectionPrefab;
        [SerializeField] private SettingsOptionButtonView optionButtonPrefab;

        private const float PagePaddingX = 16f;
        private const float PagePaddingTop = 14f;
        private const float SectionGap = 12f;
        private const float SectionHeight = 88f;
        private const float OptionGap = 8f;

        private readonly List<SettingsOptionButtonView> wallpaperButtons = new List<SettingsOptionButtonView>();
        private readonly List<SettingsOptionButtonView> timeButtons = new List<SettingsOptionButtonView>();
        private SettingsOptionButtonView showDockButton;
        private PhoneSettingsService settingsService;
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

            if (sectionPrefab == null || optionButtonPrefab == null)
            {
                Debug.LogWarning("[PhoneOS] SettingsAppView is missing section or option button prefab references.", this);
                return;
            }

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

        private float BuildWallpaperSection(float y)
        {
            var row = CreateSection("Wallpaper", y);
            if (row == null)
                return y + SectionHeight + SectionGap;

            var options = ResolveWallpaperOptions();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = CreateOptionButton(row, option.Id, option.DisplayName, i, options.Count, SetWallpaper);
                if (button != null)
                    wallpaperButtons.Add(button);
            }

            return y + SectionHeight + SectionGap;
        }

        private float BuildTimeFormatSection(float y)
        {
            var row = CreateSection("Time Format", y);
            if (row == null)
                return y + SectionHeight + SectionGap;

            var hour24 = CreateOptionButton(row, "24", "24-hour", 0, 2, _ => SetUse24HourTime(true));
            if (hour24 != null)
                timeButtons.Add(hour24);

            var hour12 = CreateOptionButton(row, "12", "12-hour", 1, 2, _ => SetUse24HourTime(false));
            if (hour12 != null)
                timeButtons.Add(hour12);

            return y + SectionHeight + SectionGap;
        }

        private void BuildHomeScreenSection(float y)
        {
            var row = CreateSection("Home Screen", y);
            if (row == null)
                return;

            showDockButton = CreateOptionButton(row, "showDock", "Show Dock", 0, 1, _ => ToggleDock());
        }

        private List<PhoneWallpaperOption> ResolveWallpaperOptions()
        {
            var results = new List<PhoneWallpaperOption>();
            if (wallpaperCatalog != null)
            {
                var options = wallpaperCatalog.Options;
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] != null)
                        results.Add(options[i]);
                }
            }

            if (results.Count == 0)
            {
                Debug.LogWarning("[PhoneOS] SettingsAppView has no wallpaper options. Check PhoneWallpaperCatalog.", this);
                results.Add(new PhoneWallpaperOption());
            }

            return results;
        }

        private RectTransform CreateSection(string title, float y)
        {
            var section = Instantiate(sectionPrefab, contentRoot);
            section.name = "Section_" + title.Replace(" ", string.Empty);
            section.Configure(title, style);

            var rect = section.transform as RectTransform;
            SetTopStretch(rect, PagePaddingX, y, PagePaddingX, SectionHeight);
            return section.OptionsRoot;
        }

        private SettingsOptionButtonView CreateOptionButton(
            RectTransform parent,
            string optionId,
            string label,
            int index,
            int count,
            Action<string> onSelected)
        {
            if (parent == null)
                return null;

            var option = Instantiate(optionButtonPrefab, parent);
            option.name = "Option_" + label.Replace(" ", string.Empty);
            option.Bind(optionId, label, onSelected);

            var rect = option.transform as RectTransform;
            SetHorizontalSlot(rect, index, count, OptionGap);
            return option;
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
                SetButtonState(wallpaperButtons[i], string.Equals(wallpaperButtons[i].OptionId, settings.wallpaperId, StringComparison.Ordinal), canInteract);

            for (var i = 0; i < timeButtons.Count; i++)
                SetButtonState(timeButtons[i], (timeButtons[i].OptionId == "24") == settings.use24HourTime, canInteract);

            if (showDockButton != null)
            {
                showDockButton.SetLabel(settings.showDock ? "Show Dock: On" : "Show Dock: Off");
                SetButtonState(showDockButton, settings.showDock, canInteract);
            }
        }

        private void SetButtonState(SettingsOptionButtonView option, bool selected, bool interactable)
        {
            if (option == null)
                return;

            option.SetState(selected, interactable, style);
        }
    }
}
