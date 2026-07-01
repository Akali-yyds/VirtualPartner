using System;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneSettingsService : MonoBehaviour
    {
        [SerializeField] private PhoneSettingsData current = new PhoneSettingsData();

        public PhoneSettingsData Current => current.Clone();

        public event Action<PhoneSettingsData> OnSettingsChanged;

        private void Awake()
        {
            current = PhoneSettingsStore.Load();
            RaiseSettingsChanged();
        }

        public void SetWallpaper(string wallpaperId)
        {
            var normalized = string.IsNullOrWhiteSpace(wallpaperId)
                ? PhoneSettingsDefaults.WallpaperId
                : wallpaperId.Trim();

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

        private void RaiseSettingsChanged()
        {
            OnSettingsChanged?.Invoke(Current);
        }
    }
}
