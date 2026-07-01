using System;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class PhoneSettingsData
    {
        public string wallpaperId = PhoneSettingsDefaults.WallpaperId;
        public bool use24HourTime = PhoneSettingsDefaults.Use24HourTime;
        public bool showDock = PhoneSettingsDefaults.ShowDock;
        public bool showDebugAppOnHome = PhoneSettingsDefaults.ShowDebugAppOnHome;
        public bool showWidgets = PhoneSettingsDefaults.ShowWidgets;

        public PhoneSettingsData Clone()
        {
            return new PhoneSettingsData
            {
                wallpaperId = wallpaperId,
                use24HourTime = use24HourTime,
                showDock = showDock,
                showDebugAppOnHome = showDebugAppOnHome,
                showWidgets = showWidgets,
            };
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(wallpaperId))
                wallpaperId = PhoneSettingsDefaults.WallpaperId;
        }
    }

    public static class PhoneSettingsDefaults
    {
        public const string WallpaperId = "pink";
        public const bool Use24HourTime = true;
        public const bool ShowDock = true;
        public const bool ShowDebugAppOnHome = true;
        public const bool ShowWidgets = true;
    }
}
