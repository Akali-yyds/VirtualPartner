using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhonePreviewAction : MonoBehaviour
    {
        public TMP_Text feedback;
        public TMP_InputField text;
        public Slider slider;
        public float resetValue;
        public void Preview() { if(feedback != null) feedback.text = "Preview only — no service action was run."; }
        public void Copy() { if(text != null) GUIUtility.systemCopyBuffer = text.text; if(feedback != null) feedback.text = "Copied to clipboard."; }
        public void Paste() { if(text != null) text.text = GUIUtility.systemCopyBuffer; }
        public void Clear() { if(text != null) text.text = string.Empty; }
        public void ResetValue() { if(slider != null) slider.value = resetValue; }
        public void ShowSecret(bool show)
        { if(text == null)return; text.contentType = show ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password; text.ForceLabelUpdate(); }
        public void UpdateValue(float value) { if(feedback != null) feedback.text = value.ToString("0.0"); }
    }
}
