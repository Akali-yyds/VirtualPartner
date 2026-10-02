using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhonePreviewSettings : MonoBehaviour
    {
        public Slider height;
        public Toggle timeFormat, dock, autoSend;
        public TMP_Text heightLabel;
        private PhonePresentationShell shell;
        private void Start()
        {
            shell = GetComponentInParent<PhonePresentationShell>();
            height.SetValueWithoutNotify(shell.HeightFraction);
            timeFormat.SetIsOnWithoutNotify(shell.Use24HourTime);
            dock.SetIsOnWithoutNotify(shell.ShowDock);
            autoSend.SetIsOnWithoutNotify(shell.AutoSendVoice);
            foreach(var visual in GetComponentsInChildren<PhoneSwitch>(true)) visual.Apply(visual.toggle.isOn);
            UpdateHeightLabel(height.value);
            height.onValueChanged.AddListener(SetHeight);
            timeFormat.onValueChanged.AddListener(shell.Set24Hour);
            dock.onValueChanged.AddListener(shell.SetDock);
            autoSend.onValueChanged.AddListener(shell.SetAutoSend);
        }
        private void SetHeight(float value) { UpdateHeightLabel(value); shell.SetHeight(value); }
        private void UpdateHeightLabel(float value) => heightLabel.text = $"{value:P0}";
        public void SetWallpaper(string id) => GetComponentInParent<PhonePresentationShell>().SetWallpaper(id);
    }
}
