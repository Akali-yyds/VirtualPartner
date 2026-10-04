using UnityEngine;
using UnityEngine.UI;

namespace VirtualPartner.Runtime.PhoneOS
{
    public sealed class PhonePagePresentation : MonoBehaviour,IPhoneTaskReset
    {
        public bool upgraded;
        public int presentationVersion;
        public void ResetTaskView(){foreach(var disclosure in GetComponentsInChildren<PhoneDisclosure>(true))disclosure.Collapse();}
        private void Start()
        {
            var app=GetComponent<PhonePreviewApp>();var chat=GetComponent<PhoneLiveMomotalk>();
            if(chat==null)return;
            Bind("ClearHistory/Confirm",()=>PhoneModal.Show(app,"Clear chat history?","This removes messages for "+chat.nameLabel.text+" and cancels its pending request. Long-term memory is kept.",chat.ClearHistory,"Clear"));
            Bind("ClearMemory/Confirm",()=>PhoneModal.Show(app,"Clear long-term memory?","This removes saved memory for "+chat.nameLabel.text+". Chat history is kept.",chat.ClearMemory,"Clear"));
        }
        private void Bind(string path,UnityEngine.Events.UnityAction action){var b=transform.Find(path).GetComponent<Button>();b.onClick=new Button.ButtonClickedEvent();b.onClick.AddListener(action);}
    }
}
