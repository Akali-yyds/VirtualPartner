using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneDebugOption : MonoBehaviour
    {
        public PhoneLiveDebug debug;public string option;private Toggle toggle;
        private IEnumerator Start(){toggle=GetComponent<Toggle>();yield return null;toggle.SetIsOnWithoutNotify(debug.ReadOption(option));GetComponent<PhoneSwitch>().Apply(toggle.isOn);toggle.onValueChanged.AddListener(value=>debug.SetOption(option,value));}
    }
}
