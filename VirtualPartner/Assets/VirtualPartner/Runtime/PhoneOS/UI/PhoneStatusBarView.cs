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
                timeText.text = useSystemTime ? System.DateTime.Now.ToString("HH:mm") : "4:44";
            if (batteryText != null)
                batteryText.text = "100%";
            if (wifiText != null)
                wifiText.text = "WiFi";
            if (signalText != null)
                signalText.text = "5G";
        }
    }
}
