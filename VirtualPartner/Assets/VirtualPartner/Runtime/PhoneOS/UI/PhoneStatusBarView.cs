using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneStatusBarView : MonoBehaviour
    {
        [SerializeField] private Text timeText;
        [SerializeField] private Text batteryText;
        [SerializeField] private Text wifiText;
        [SerializeField] private Text signalText;
        [SerializeField] private bool useSystemTime = true;
        [SerializeField] private bool use24HourTime = true;

        private float nextTimeRefresh;

        private void OnEnable()
        {
            Refresh();
        }

        private void Update()
        {
            if (!useSystemTime || Time.unscaledTime < nextTimeRefresh)
                return;

            Refresh();
        }

        public void Refresh()
        {
            nextTimeRefresh = Time.unscaledTime + 1f;
            if (timeText != null)
                timeText.text = FormatTime(useSystemTime ? System.DateTime.Now : new System.DateTime(2026, 6, 30, 18, 30, 0));
            if (batteryText != null)
                batteryText.text = "100%";
            if (wifiText != null)
                wifiText.text = "WiFi";
            if (signalText != null)
                signalText.text = "5G";
        }

        public void SetUse24HourTime(bool value)
        {
            if (use24HourTime == value)
                return;

            use24HourTime = value;
            Refresh();
        }

        private string FormatTime(System.DateTime value)
        {
            return value.ToString(use24HourTime ? "HH:mm" : "h:mm tt", CultureInfo.InvariantCulture);
        }
    }
}
