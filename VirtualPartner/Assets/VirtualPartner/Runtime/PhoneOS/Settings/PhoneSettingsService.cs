using System;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneSettingsService : MonoBehaviour
    {
        [SerializeField] private PhoneWallpaperCatalog wallpaperCatalog;
        [SerializeField] private PhoneSettingsData current = new PhoneSettingsData();

        public PhoneSettingsData Current => current.Clone();

        public event Action<PhoneSettingsData> OnSettingsChanged;

        private void Awake()
        {
            current = PhoneSettingsStore.Load();
            EnsureLoadedWallpaperIsKnown();
            RaiseSettingsChanged();
        }

        public void SetWallpaper(string wallpaperId)
        {
            var normalized = NormalizeWallpaperId(wallpaperId, true);

            if (string.Equals(current.wallpaperId, normalized, StringComparison.Ordinal))
                return;

            current.wallpaperId = normalized;
            Commit();
        }

        public void SetUse24HourTime(bool value)
        {
            if (current.use24HourTime == value)
                return;

            current.use24HourTime = value;
            Commit();
        }

        public void SetShowDock(bool value)
        {
            if (current.showDock == value)
                return;

            current.showDock = value;
            Commit();
        }

        private void Commit()
        {
            current.Normalize();
            PhoneSettingsStore.Save(current);
            RaiseSettingsChanged();
        }

        private void EnsureLoadedWallpaperIsKnown()
        {
            var normalized = NormalizeWallpaperId(current.wallpaperId, false);
            if (string.Equals(current.wallpaperId, normalized, StringComparison.Ordinal))
                return;

            Debug.LogWarning($"[PhoneOS] Saved wallpaper '{current.wallpaperId}' is not registered. Falling back to '{normalized}'.", this);
            current.wallpaperId = normalized;
            PhoneSettingsStore.Save(current);
        }

        private string NormalizeWallpaperId(string wallpaperId, bool logInvalid)
        {
            var normalized = PhoneWallpaperCatalog.NormalizeWallpaperId(wallpaperId);
            if (wallpaperCatalog == null || wallpaperCatalog.Contains(normalized))
                return normalized;

            var fallback = wallpaperCatalog.DefaultWallpaperId;
            if (logInvalid)
                Debug.LogWarning($"[PhoneOS] Ignored unknown wallpaper '{normalized}'. Falling back to '{fallback}'.", this);

            return fallback;
        }

        private void RaiseSettingsChanged()
        {
            OnSettingsChanged?.Invoke(Current);
        }
    }
}
