using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneSettingsApplier : MonoBehaviour
    {
        [SerializeField] private PhoneSettingsService settingsService;
        [SerializeField] private PhoneWallpaperCatalog wallpaperCatalog;
        [SerializeField] private Image wallpaperImage;
        [SerializeField] private GameObject dockRoot;
        [SerializeField] private PhoneStatusBarView statusBarView;
        [SerializeField] private PhoneClockWidgetView clockWidgetView;

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
            return wallpaperCatalog != null ? wallpaperCatalog.Find(wallpaperId) : null;
        }

        private void ApplyWallpaper(string wallpaperId)
        {
            if (wallpaperImage == null)
                return;

            var option = FindWallpaperOption(wallpaperId);
            if (option == null || option.Sprite == null)
            {
                Debug.LogWarning($"[PhoneOS] Cannot apply wallpaper '{wallpaperId}': catalog entry or sprite is missing.", this);
                return;
            }

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
