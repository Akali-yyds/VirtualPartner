using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneDisclosure : MonoBehaviour
    {
        public GameObject[] items;
        public TMP_Text label;
        public string title;
        public bool expanded;
        private void Start(){GetComponent<Button>().onClick.AddListener(Toggle);Apply();}
        public void Toggle(){expanded=!expanded;Apply();}
        public void Collapse(){expanded=false;Apply();}
        public void Apply(){foreach(var item in items)if(item!=null)item.SetActive(expanded);if(label!=null)label.text=title+(expanded?"  −":"  +");}
    }
}
