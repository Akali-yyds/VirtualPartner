using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class PhoneWallpaperOption
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;

        public string Id => string.IsNullOrWhiteSpace(id) ? PhoneSettingsDefaults.WallpaperId : id;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public Sprite Sprite => sprite;
    }

    [DisallowMultipleComponent]
    public sealed class PhoneSettingsApplier : MonoBehaviour
    {
        [SerializeField] private PhoneSettingsService settingsService;
        [SerializeField] private Image wallpaperImage;
        [SerializeField] private GameObject dockRoot;
        [SerializeField] private PhoneStatusBarView statusBarView;
        [SerializeField] private PhoneClockWidgetView clockWidgetView;
        [SerializeField] private PhoneWallpaperOption[] wallpaperOptions;

        public IReadOnlyList<PhoneWallpaperOption> WallpaperOptions => wallpaperOptions ?? Array.Empty<PhoneWallpaperOption>();

        private void Awake()
        {
            ResolveService();
        }

        private void OnEnable()
        {
            ResolveService();
            if (settingsService != null)
            {
                settingsService.OnSettingsChanged += Apply;
                Apply(settingsService.Current);
            }
        }

        private void OnDisable()
        {
            if (settingsService != null)
                settingsService.OnSettingsChanged -= Apply;
        }

        public void Apply(PhoneSettingsData data)
        {
            var settings = data ?? new PhoneSettingsData();
            settings.Normalize();

            ApplyWallpaper(settings.wallpaperId);

            if (statusBarView != null)
                statusBarView.SetUse24HourTime(settings.use24HourTime);
            if (clockWidgetView != null)
                clockWidgetView.SetUse24HourTime(settings.use24HourTime);
            if (dockRoot != null && dockRoot.activeSelf != settings.showDock)
                dockRoot.SetActive(settings.showDock);
        }

        public PhoneWallpaperOption FindWallpaperOption(string wallpaperId)
        {
            if (wallpaperOptions == null || wallpaperOptions.Length == 0)
                return null;

            var normalized = string.IsNullOrWhiteSpace(wallpaperId) ? PhoneSettingsDefaults.WallpaperId : wallpaperId.Trim();
            for (var i = 0; i < wallpaperOptions.Length; i++)
            {
                var option = wallpaperOptions[i];
                if (option != null && string.Equals(option.Id, normalized, StringComparison.Ordinal))
                    return option;
            }

            return wallpaperOptions[0];
        }

        private void ApplyWallpaper(string wallpaperId)
        {
            if (wallpaperImage == null)
                return;

            var option = FindWallpaperOption(wallpaperId);
            if (option == null || option.Sprite == null)
                return;

            wallpaperImage.sprite = option.Sprite;
            wallpaperImage.type = Image.Type.Simple;
            wallpaperImage.preserveAspect = false;
        }

        private void ResolveService()
        {
            if (settingsService == null)
                settingsService = GetComponent<PhoneSettingsService>();
        }
    }
}
