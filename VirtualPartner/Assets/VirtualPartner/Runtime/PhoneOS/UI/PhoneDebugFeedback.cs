using UnityEngine;
using UnityEngine.UI;
namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhoneDebugFeedback:MonoBehaviour
    {
        public PhoneLiveDebug debug;
        private void Start(){GetComponent<Button>().onClick.AddListener(()=>{PhoneModal.Show(debug.GetComponent<PhonePreviewApp>(),"Operation result",debug.feedback.text);});}
    }
}
