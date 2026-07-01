using System;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class PhoneSettingsData
    {
        public string wallpaperId = PhoneSettingsDefaults.WallpaperId;
        public bool use24HourTime = PhoneSettingsDefaults.Use24HourTime;
        public bool showDock = PhoneSettingsDefaults.ShowDock;

        public PhoneSettingsData Clone()
        {
            return new PhoneSettingsData
            {
                wallpaperId = wallpaperId,
                use24HourTime = use24HourTime,
                showDock = showDock,
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
    }
}
