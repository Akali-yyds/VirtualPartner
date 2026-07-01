using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    public static class PhoneSettingsStore
    {
        private const string PlayerPrefsKey = "VirtualPartner.PhoneOS.Settings.v1";

        public static PhoneSettingsData Load()
        {
            var data = new PhoneSettingsData();
            if (!PlayerPrefs.HasKey(PlayerPrefsKey))
                return data;

            var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return data;

            try
            {
                data = JsonUtility.FromJson<PhoneSettingsData>(json) ?? new PhoneSettingsData();
            }
            catch
            {
                data = new PhoneSettingsData();
            }

            data.Normalize();
            return data;
        }

        public static void Save(PhoneSettingsData data)
        {
            var safeData = data != null ? data.Clone() : new PhoneSettingsData();
            safeData.Normalize();
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(safeData));
            PlayerPrefs.Save();
        }
    }
}
