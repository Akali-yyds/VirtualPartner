using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    [DisallowMultipleComponent]
    public sealed class PhoneClockWidgetView : MonoBehaviour
    {
        [SerializeField] private PhoneOSStyle style;
        [SerializeField] private Text timeText;
        [SerializeField] private Text dateText;
        [SerializeField] private bool useSystemTime = true;
        [SerializeField] private bool use24HourTime = true;

        private float nextRefreshTime;

        private void OnEnable()
        {
            ApplyStyle();
            Refresh();
        }

        private void Update()
        {
            if (!useSystemTime || Time.unscaledTime < nextRefreshTime)
                return;

            Refresh();
        }

        private void OnValidate()
        {
            ApplyStyle();
        }

        public void Refresh()
        {
            nextRefreshTime = Time.unscaledTime + 1f;
            var now = useSystemTime ? DateTime.Now : new DateTime(2026, 6, 30, 9, 41, 0);

            if (timeText != null)
                timeText.text = FormatTime(now);
            if (dateText != null)
                dateText.text = now.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        }

        public void SetUse24HourTime(bool value)
        {
            if (use24HourTime == value)
                return;

            use24HourTime = value;
            ApplyStyle();
            Refresh();
        }

        private void ApplyStyle()
        {
            if (style == null)
                return;

            if (timeText != null)
            {
                timeText.color = style.PrimaryTextColor;
                timeText.fontSize = Mathf.RoundToInt(style.ClockLargeFontSize);
                timeText.fontStyle = FontStyle.Bold;
                timeText.resizeTextForBestFit = false;
                timeText.horizontalOverflow = HorizontalWrapMode.Overflow;
                timeText.verticalOverflow = VerticalWrapMode.Overflow;
                timeText.lineSpacing = 0.8f;
            }

            if (dateText != null)
            {
                dateText.color = style.PrimaryTextColor;
                dateText.fontSize = Mathf.RoundToInt(style.WidgetSmallFontSize);
                dateText.fontStyle = FontStyle.Bold;
                dateText.horizontalOverflow = HorizontalWrapMode.Overflow;
                dateText.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        private string FormatTime(DateTime value)
        {
            return value.ToString(use24HourTime ? "HH\nmm" : "h:mm\ntt", CultureInfo.InvariantCulture);
        }
    }
}
