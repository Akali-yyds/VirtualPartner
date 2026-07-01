using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class PhoneWallpaperOption
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;

        public string Id => PhoneWallpaperCatalog.NormalizeWallpaperId(id);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public Sprite Sprite => sprite;
    }

    [CreateAssetMenu(fileName = "PhoneWallpaperCatalog", menuName = "Virtual Partner/Phone OS/Wallpaper Catalog")]
    public sealed class PhoneWallpaperCatalog : ScriptableObject
    {
        [SerializeField] private PhoneWallpaperOption[] wallpaperOptions;

        public IReadOnlyList<PhoneWallpaperOption> Options => wallpaperOptions ?? Array.Empty<PhoneWallpaperOption>();

        public string DefaultWallpaperId
        {
            get
            {
                var fallback = FindFirstOption();
                return fallback != null ? fallback.Id : PhoneSettingsDefaults.WallpaperId;
            }
        }

        public PhoneWallpaperOption Find(string wallpaperId)
        {
            var exact = FindExact(wallpaperId);
            return exact ?? FindFirstOption();
        }

        public bool Contains(string wallpaperId)
        {
            return FindExact(wallpaperId) != null;
        }

        public string NormalizeKnownWallpaperId(string wallpaperId)
        {
            var normalized = NormalizeWallpaperId(wallpaperId);
            return Contains(normalized) ? normalized : DefaultWallpaperId;
        }

        public static string NormalizeWallpaperId(string wallpaperId)
        {
            return string.IsNullOrWhiteSpace(wallpaperId)
                ? PhoneSettingsDefaults.WallpaperId
                : wallpaperId.Trim();
        }

        private PhoneWallpaperOption FindExact(string wallpaperId)
        {
            var normalized = NormalizeWallpaperId(wallpaperId);
            var options = Options;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option != null && string.Equals(option.Id, normalized, StringComparison.Ordinal))
                    return option;
            }

            return null;
        }

        private PhoneWallpaperOption FindFirstOption()
        {
            var options = Options;
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] != null)
                    return options[i];
            }

            return null;
        }
    }
}
