using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneSwitch : MonoBehaviour
    {
        public Toggle toggle;
        public Image track;
        public RectTransform thumb;
        private void OnEnable() { if(toggle!=null){toggle.onValueChanged.AddListener(Apply);Apply(toggle.isOn);} }
        private void OnDisable() { if(toggle!=null)toggle.onValueChanged.RemoveListener(Apply); }
        public void Apply(bool value)
        {
            if(track!=null)track.color=value?(Color)new Color32(222,120,153,255):new Color32(214,208,215,255);
            if(thumb!=null)thumb.anchoredPosition=new Vector2(value?24:4,-4);
        }
    }
}
